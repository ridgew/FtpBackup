using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using log4net;

namespace SurfingDataSyn
{
    /// <summary>
    /// 运行Ftp操作并返回指定类型的结果
    /// </summary>
    /// <typeparam name="TResult">操作结果</typeparam>
    /// <param name="ftpRequest">ftp请求</param>
    /// <returns></returns>
    public delegate TResult FtpExecute<TResult>(FtpWebRequest ftpRequest);

    public class FtpCmd
    {
        /// <summary>
        /// FTP日志目录
        /// </summary>
        static ILog FtpLog = LogManager.GetLogger(typeof(FtpCmd));


        /// <summary>
        /// Builds the FTP request.
        /// </summary>
        /// <param name="BaseUriStr">基Uri字符串：如ftp://192.168.1.190:21</param>
        /// <param name="AimUriStr">目标目录名，相对路径：如/FTF</param>
        /// <param name="UserName">用户名</param>
        /// <param name="UserPwd">用户密码</param>
        /// <param name="timeout">超时毫秒数</param>
        /// <returns></returns>
        static FtpWebRequest buildFtpRequest(string BaseUriStr, string AimUriStr, string UserName, string UserPwd, int timeout)
        {
            Uri BaseUri = new Uri(BaseUriStr);
            FtpWebRequest ftpRequest = (FtpWebRequest)WebRequest.Create(new Uri(BaseUri, AimUriStr));
            ftpRequest.KeepAlive = true;
            ftpRequest.Timeout = timeout;
            ftpRequest.UsePassive = Convert.ToBoolean(ConfigurationManager.AppSettings["System.Net.FtpWebRequest.UsePassive"] ?? "true");
            ftpRequest.Credentials = new NetworkCredential(UserName, UserPwd);
            return ftpRequest;
        }

        #region 创建文件夹
        /// <summary>
        /// 获取对于ftp连接对象的布尔运行结果
        /// </summary>
        /// <param name="ftpRequest">ftp连接对象</param>
        /// <param name="exec">The exec.</param>
        /// <returns></returns>
        public static bool GetBoolResult(FtpWebRequest ftpRequest, FtpExecute<bool> exec)
        {
            return exec(ftpRequest);
        }

        /// <summary>
        /// 运行目录创建
        /// </summary>
        /// <param name="ftp">The FTP.</param>
        /// <returns></returns>
        public static bool ExecuteFtpDirectory(FtpWebRequest ftp)
        {
            ftp.Method = WebRequestMethods.Ftp.MakeDirectory;
            FtpWebResponse resp = null;
            bool result = false;
            try
            {
                resp = (FtpWebResponse)ftp.GetResponse();
                result = resp.StatusCode == FtpStatusCode.PathnameCreated;
            }
            catch (WebException e)
            {
                if (e.Response != null)
                {
                    resp = (FtpWebResponse)e.Response;
                    result = resp.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable; ////文件已存在，返回True
                }
            }
            if (resp != null) resp.Close();
            return result;
        }

        /// <summary>
        /// 创建文件夹:不实现级联创建
        /// 返回：true成功，false失败
        /// </summary>
        /// <param name="BaseUriStr">基Uri字符串：如ftp://192.168.1.190:21</param>
        /// <param name="AimUriStr">目标目录名，相对路径：如/FTF</param>
        /// <param name="UserName">用户名</param>
        /// <param name="UserPwd">用户密码</param>
        /// <returns></returns>
        public static bool CreateFtpDirectory(string BaseUriStr, string AimUriStr, string UserName, string UserPwd)
        {
            return GetBoolResult(buildFtpRequest(BaseUriStr, AimUriStr, UserName, UserPwd, 10000), ExecuteFtpDirectory);
        }

        /// <summary>
        /// 创建文件夹:实现级联创建
        /// 返回：true成功，false失败
        /// </summary>
        /// <param name="BaseUriStr">基Uri字符串：如ftp://192.168.1.190:21</param>
        /// <param name="AimUriStr">目标目录名，相对路径：如/FTF/FTF1/FTF2/FTF3</param>
        /// <param name="UserName">用户名</param>
        /// <param name="UserPwd">用户密码</param>
        /// <returns></returns>
        public static bool CreateFtpListDirectory(string BaseUriStr, string AimUriStr, string UserName, string UserPwd)
        {
            string[] AimUriArray = AimUriStr.TrimStart('/').Split('/');
            string AimUriCache = string.Empty;
            for (int i = 0; i < AimUriArray.Length; i++)
            {
                AimUriCache += "/" + AimUriArray[i];
                if (CreateFtpDirectory(BaseUriStr, AimUriCache, UserName, UserPwd))
                {
                    continue;
                }
                else
                {
                    if (CreateFtpDirectory(BaseUriStr, AimUriCache, UserName, UserPwd))
                    {
                        continue;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        #endregion

        #region 删除文件夹
        /// <summary>
        /// 删除文件夹,不实现级联删除
        /// 返回：true成功，false失败
        /// </summary>
        /// <param name="BaseUriStr">基Uri字符串：如ftp://192.168.1.190:21</param>
        /// <param name="AimUriStr">目标目录，相对路径：如/FTF/FTFDEL</param>
        /// <param name="UserName">用户名</param>
        /// <param name="UserPwd">用户密码</param>
        /// <returns></returns>
        public static bool DeleteFtpDirectory(string BaseUriStr, string AimUriStr, string UserName, string UserPwd)
        {
            FtpWebRequest FtpRequest = buildFtpRequest(BaseUriStr, AimUriStr, UserName, UserPwd, 2000);
            FtpRequest.Method = WebRequestMethods.Ftp.RemoveDirectory;

            try
            {
                FtpWebResponse FtpResponse = (FtpWebResponse)FtpRequest.GetResponse();
                if (FtpResponse.StatusCode == FtpStatusCode.FileActionOK)
                {
                    FtpResponse.Close();
                    return true;
                }
                else
                {
                    FtpResponse.Close();
                    return false;
                }
            }
            catch (WebException e)
            {

                FtpWebResponse FtpResponse = (FtpWebResponse)e.Response;
                //如果返回信息表示文件不可操作或不存在，表明文件夹已经被删除
                if (FtpResponse.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                {
                    FtpResponse.Close();
                    return true;
                }
                else
                {
                    //返回其他错误：可能出现问题
                    FtpResponse.Close();
                    return false;
                }
            }
        }
        /// <summary>
        /// 删除文件夹，实现级联删除
        /// 返回：true成功，false失败
        /// </summary>
        /// <param name="BaseUriStr">基Uri字符串：如ftp://192.168.1.190:21</param>
        /// <param name="AimUriStr">目标目录，相对路径：如/FTF/FTFDEL</param>
        /// <param name="UserName">用户名</param>
        /// <param name="UserPwd">用户密码</param>
        /// <returns></returns>
        public static bool DeleteFtpListDirectory(string BaseUriStr, string AimUriStr, string UserName, string UserPwd)
        {
            List<string> DirectoryDetailList = ListFtpDirectory(BaseUriStr, AimUriStr, UserName, UserPwd);
            foreach (string ListDetail in DirectoryDetailList)
            {
                if (ListDetail.EndsWith("|D"))
                {
                    //删除文件夹内容
                    if (ListFtpDirectory(BaseUriStr, AimUriStr + "/" + ListDetail.Split('|')[0], UserName, UserPwd).Count == 0)
                    {
                        if (!DeleteFtpDirectory(BaseUriStr, AimUriStr + "/" + ListDetail.Split('|')[0], UserName, UserPwd))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (!DeleteFtpListDirectory(BaseUriStr, AimUriStr + "/" + ListDetail.Split('|')[0], UserName, UserPwd))
                        {
                            return false;
                        }
                    }
                }
                if (ListDetail.EndsWith("|F"))
                {
                    //删除文件
                    if (!DeleteFtpFile(BaseUriStr, AimUriStr + "/" + ListDetail.Split('|')[0], UserName, UserPwd))
                    {
                        return false;
                    }
                }
            }
            //删除当前文件夹
            if (!DeleteFtpDirectory(BaseUriStr, AimUriStr, UserName, UserPwd))
            {
                return false;
            }

            return true;
        }
        #endregion

        #region 获取文件夹内文件和文件夹列表信息
        /// <summary>
        /// 获取文件夹内文件信息
        /// </summary>
        /// <param name="BaseUriStr">基Uri：如ftp://192.168.1.190:21</param>
        /// <param name="AimUriStr">目标目录，相对路径：如/FTF/FTF1</param>
        /// <param name="UserName">用户名</param>
        /// <param name="UserPwd">用户密码</param>
        /// <returns></returns>
        public static List<string> ListFtpDirectory(string BaseUriStr, string AimUriStr, string UserName, string UserPwd)
        {
            FtpWebRequest FtpRequest = buildFtpRequest(BaseUriStr, AimUriStr, UserName, UserPwd, 10000);
            FtpRequest.Method = WebRequestMethods.Ftp.ListDirectoryDetails;

            try
            {
                FtpWebResponse FtpResponse = (FtpWebResponse)FtpRequest.GetResponse();
                StreamReader srd = new StreamReader(FtpResponse.GetResponseStream(), Encoding.GetEncoding("GB2312"));
                string ResponseBackStr = srd.ReadToEnd();
                srd.Close();
                FtpResponse.Close();

                string[] ListDetails = ResponseBackStr.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                List<string> RtnList = new List<string>();
                foreach (string ListDetail in ListDetails)
                {
                    if (ListDetail.StartsWith("d") && (!ListDetail.EndsWith(".")))
                    {
                        string FtpDirName = ListDetail.Substring(ListDetail.IndexOf(':') + 3).TrimStart();
                        RtnList.Add(FtpDirName + "|D");
                    }
                    else if (ListDetail.StartsWith("-"))
                    {
                        string FtpDirName = ListDetail.Substring(ListDetail.IndexOf(':') + 3).TrimStart();
                        RtnList.Add(FtpDirName + "|F");
                    }
                }

                return RtnList;
            }
            catch (WebException e)
            {
                if (e.Response != null)
                {
                    FtpWebResponse FtpResponse = (FtpWebResponse)e.Response;
                    FtpResponse.Close();
                }
                return new List<string>();
            }
        }
        #endregion

        #region 删除指定文件
        /// <summary>
        /// 删除指定文件
        /// </summary>
        /// <param name="BaseUriStr">基Uri字符串，如ftp://192.168.1.190:21</param>
        /// <param name="AimUriStr">目标目录，相对路径：如/FTF/FTFDEL</param>
        /// <param name="UserName">用户名</param>
        /// <param name="UserPwd">用户密码</param>
        /// <returns></returns>
        public static bool DeleteFtpFile(string BaseUriStr, string AimUriStr, string UserName, string UserPwd)
        {
            FtpWebRequest FtpRequest = buildFtpRequest(BaseUriStr, AimUriStr, UserName, UserPwd, 2000);
            FtpRequest.Method = WebRequestMethods.Ftp.DeleteFile;

            FtpWebResponse resp = null;
            bool result = false;
            try
            {
                resp = (FtpWebResponse)FtpRequest.GetResponse();
                result = resp.StatusCode == FtpStatusCode.FileActionOK;
            }
            catch (WebException e)
            {
                if (e.Response != null)
                {
                    resp = (FtpWebResponse)e.Response;
                    result = resp.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable;
                }
            }
            if (resp != null) resp.Close();
            return result;
        }
        #endregion

        static FtpWebRequest createCopy(FtpWebRequest request)
        {
            FtpWebRequest ftpRequest = (FtpWebRequest)WebRequest.Create(request.RequestUri);
            ftpRequest.KeepAlive = request.KeepAlive;
            ftpRequest.Timeout = request.Timeout;
            ftpRequest.UsePassive = request.UsePassive;
            ftpRequest.Credentials = request.Credentials;
            return ftpRequest;
        }

        static string trimLocalAppPath(string filePath)
        {
            string appBase = AppDomain.CurrentDomain.BaseDirectory;
            if (filePath.StartsWith(appBase, StringComparison.InvariantCultureIgnoreCase))
            {
                return filePath.Substring(appBase.Length);
            }
            else
            {
                return filePath;
            }
        }

        #region 上传文件
        /// <summary>
        /// 对特定的FTP请求对象执行上传文件操作
        /// </summary>
        /// <param name="SrcFilePath">本地文件路径</param>
        /// <param name="ftpRequest">当前ftp请求对象</param>
        /// <returns></returns>
        public static bool ExecuteUploadFtpFile(string SrcFilePath, FtpWebRequest ftpRequest)
        {
            bool result = false;
            FtpWebResponse resp = null;
            ftpRequest.Method = WebRequestMethods.Ftp.UploadFile;
            try
            {
                using (Stream stm = ftpRequest.GetRequestStream())
                {
                    byte[] ftpBuffer = new byte[Convert.ToInt32(ConfigurationManager.AppSettings["SurfingDataSyn.FtpCmd.FtpBufferSize"] ?? "40960")];
                    int readOffSet = 0, currentRead = 0;
                    using (FileStream fs = new FileStream(SrcFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        long totalLen = fs.Length;
                        int totalTryTimes = 5;

                        //设置上传长度
                        ftpRequest.ContentLength = totalLen;
                        //FtpRequest.ContentOffset //断点续传?

                        if (totalLen <= Convert.ToInt64(ConfigurationManager.AppSettings["SurfingDataSyn.FtpCmd.SplitUploadSize"] ?? "4194304"))
                        {
                            try
                            {
                                byte[] fileBytes = new byte[totalLen];
                                int totalRead = fs.Read(fileBytes, 0, fileBytes.Length);
                                stm.Write(fileBytes, 0, totalRead);
                                FtpLog.DebugFormat("* 文件{0}已上传{1}字节.", trimLocalAppPath(SrcFilePath), totalRead);
                            }
                            catch (Exception slnEx)
                            {
                                FtpLog.Error("* 出现上传错误:", slnEx);
                            }
                        }
                        else
                        {
                            #region 分段上传
                            while ((currentRead = fs.Read(ftpBuffer, 0, ftpBuffer.Length)) > 0)
                            {
                                int currentTryTimes = 0;
                                FtpLog.DebugFormat("* 本次读取{0}字节上传...", currentRead);

                                #region 上传错误处理
                            RetryUpload:
                                try
                                {
                                    stm.Write(ftpBuffer, 0, currentRead);
                                }
                                catch (Exception uploadEx)
                                {
                                    if (uploadEx.InnerException != null && uploadEx.InnerException is SocketException)
                                    {
                                        SocketException socketExp = uploadEx.InnerException as SocketException;
                                        FtpLog.ErrorFormat("* 出现Socket错误{0}.", socketExp.SocketErrorCode);
                                        if (socketExp.SocketErrorCode == SocketError.WouldBlock && currentTryTimes < totalTryTimes)
                                        {
                                            currentTryTimes++;
                                            System.Threading.Thread.Sleep(100);
                                            FtpLog.DebugFormat("上传错误：开始第{0}次重试！", currentTryTimes);
                                            goto RetryUpload;
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        FtpLog.Error("* 出现上传错误:", uploadEx);
                                        break;
                                    }
                                }
                                #endregion

                                if (currentTryTimes >= totalTryTimes)
                                    break;

                                readOffSet += currentRead;

                                FtpLog.DebugFormat("* 文件{2}已上传{0}/{1} [约{3}].", readOffSet, totalLen, trimLocalAppPath(SrcFilePath),
                                    (Convert.ToDouble(readOffSet) / Convert.ToDouble(totalLen)).ToString("P2"));
                            }
                            #endregion
                        }
                        fs.Close();
                    }
                    stm.Close();
                }

                resp = (FtpWebResponse)ftpRequest.GetResponse();
                if (resp.StatusCode != FtpStatusCode.ClosingData)
                {
                    result = false;
                }
                else
                {
                    resp.Close();
                    resp = null;
                    result = ExecuteGetFtpFileSize(createCopy(ftpRequest)) == new FileInfo(SrcFilePath).Length; //验证文件大小
                }
            }
            catch (WebException e)
            {
                result = false;
                if (e.Response != null)
                    resp = (FtpWebResponse)e.Response;
            }
            if (resp != null) resp.Close();
            return result;
        }

        /// <summary>
        /// 上传文件到指定位置
        /// </summary>
        /// <param name="BaseUriStr">基Uri字符串，如ftp://192.168.1.190:21</param>
        /// <param name="AimUriStr">目标位置，相对路径：如/FTF/FTFDEL/K.pdf</param>
        /// <param name="UserName">用户名</param>
        /// <param name="UserPwd">用户密码</param>
        /// <param name="SrcFilePath">源文件位置</param>
        /// <returns></returns>
        public static bool UploadFtpFile(string BaseUriStr, string AimUriStr, string UserName, string UserPwd, string SrcFilePath)
        {
            FtpWebRequest FtpRequest = buildFtpRequest(BaseUriStr, AimUriStr, UserName, UserPwd, 10000);
            return ExecuteUploadFtpFile(SrcFilePath, FtpRequest);
        }

        /// <summary>
        /// 获取远程文件的字节大小
        /// </summary>
        /// <param name="FtpRequest"></param>
        /// <returns></returns>
        public static int ExecuteGetFtpFileSize(FtpWebRequest FtpRequest)
        {
            FtpRequest.Method = WebRequestMethods.Ftp.GetFileSize;
            try
            {
                FtpWebResponse resp = (FtpWebResponse)FtpRequest.GetResponse();
                long FileSize = resp.ContentLength;
                resp.Close();
                return Convert.ToInt32(FileSize);
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// 获取文件尺寸
        /// 返回文件字节数
        /// </summary>
        /// <param name="BaseUriStr">基Uri字符串，如ftp://192.168.1.190:21</param>
        /// <param name="AimUriStr">目标位置，相对路径：如/FTF/FTFDEL/K.pdf</param>
        /// <param name="UserName">用户名</param>
        /// <param name="UserPwd">用户密码</param>
        /// <returns></returns>
        public static int GetFtpFileSize(string BaseUriStr, string AimUriStr, string UserName, string UserPwd)
        {
            FtpWebRequest FtpRequest = buildFtpRequest(BaseUriStr, AimUriStr, UserName, UserPwd, 2000);
            return new FtpExecute<int>(ExecuteGetFtpFileSize)(FtpRequest);
        }
        #endregion

        #region 文件重命名
        /// <summary>
        /// 文件重命名、文件移动
        /// </summary>
        /// <param name="BaseUriStr">基Uri字符串，如ftp://192.168.1.190:21</param>
        /// <param name="SrcUriStr">源位置，相对路径：如/FTF/FTFDEL/K.pdf</param>
        /// <param name="AimUriStr">目标位置，相对路径：如/FTF/FTFDEL/K.pdf</param>
        /// <param name="UserName">用户名</param>
        /// <param name="UserPwd">用户密码</param>
        /// <returns></returns>
        public static bool RenameFtpFile(string BaseUriStr, string SrcUriStr, string AimUriStr, string UserName, string UserPwd)
        {
            FtpWebRequest FtpRequest = buildFtpRequest(BaseUriStr, AimUriStr, UserName, UserPwd, 10000);
            FtpRequest.Method = WebRequestMethods.Ftp.Rename;
            FtpRequest.RenameTo = AimUriStr;
            try
            {
                FtpWebResponse FtpResponse = (FtpWebResponse)FtpRequest.GetResponse();
                if (FtpResponse.StatusCode == FtpStatusCode.FileActionOK)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (WebException e)
            {
                if (e.Response != null)
                {
                    FtpWebResponse resp = (FtpWebResponse)e.Response;
                    resp.Close();
                }
                return false;
            }
        }
        #endregion
    }
}