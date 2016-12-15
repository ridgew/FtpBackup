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
    /// ����Ftp����������ָ�����͵Ľ��
    /// </summary>
    /// <typeparam name="TResult">�������</typeparam>
    /// <param name="ftpRequest">ftp����</param>
    /// <returns></returns>
    public delegate TResult FtpExecute<TResult>(FtpWebRequest ftpRequest);

    public class FtpCmd
    {
        /// <summary>
        /// FTP��־Ŀ¼
        /// </summary>
        static ILog FtpLog = LogManager.GetLogger(typeof(FtpCmd));


        /// <summary>
        /// Builds the FTP request.
        /// </summary>
        /// <param name="BaseUriStr">��Uri�ַ�������ftp://192.168.1.190:21</param>
        /// <param name="AimUriStr">Ŀ��Ŀ¼�������·������/FTF</param>
        /// <param name="UserName">�û���</param>
        /// <param name="UserPwd">�û�����</param>
        /// <param name="timeout">��ʱ������</param>
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

        #region �����ļ���
        /// <summary>
        /// ��ȡ����ftp���Ӷ���Ĳ������н��
        /// </summary>
        /// <param name="ftpRequest">ftp���Ӷ���</param>
        /// <param name="exec">The exec.</param>
        /// <returns></returns>
        public static bool GetBoolResult(FtpWebRequest ftpRequest, FtpExecute<bool> exec)
        {
            return exec(ftpRequest);
        }

        /// <summary>
        /// ����Ŀ¼����
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
                    result = resp.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable; ////�ļ��Ѵ��ڣ�����True
                }
            }
            if (resp != null) resp.Close();
            return result;
        }

        /// <summary>
        /// �����ļ���:��ʵ�ּ�������
        /// ���أ�true�ɹ���falseʧ��
        /// </summary>
        /// <param name="BaseUriStr">��Uri�ַ�������ftp://192.168.1.190:21</param>
        /// <param name="AimUriStr">Ŀ��Ŀ¼�������·������/FTF</param>
        /// <param name="UserName">�û���</param>
        /// <param name="UserPwd">�û�����</param>
        /// <returns></returns>
        public static bool CreateFtpDirectory(string BaseUriStr, string AimUriStr, string UserName, string UserPwd)
        {
            return GetBoolResult(buildFtpRequest(BaseUriStr, AimUriStr, UserName, UserPwd, 10000), ExecuteFtpDirectory);
        }

        /// <summary>
        /// �����ļ���:ʵ�ּ�������
        /// ���أ�true�ɹ���falseʧ��
        /// </summary>
        /// <param name="BaseUriStr">��Uri�ַ�������ftp://192.168.1.190:21</param>
        /// <param name="AimUriStr">Ŀ��Ŀ¼�������·������/FTF/FTF1/FTF2/FTF3</param>
        /// <param name="UserName">�û���</param>
        /// <param name="UserPwd">�û�����</param>
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

        #region ɾ���ļ���
        /// <summary>
        /// ɾ���ļ���,��ʵ�ּ���ɾ��
        /// ���أ�true�ɹ���falseʧ��
        /// </summary>
        /// <param name="BaseUriStr">��Uri�ַ�������ftp://192.168.1.190:21</param>
        /// <param name="AimUriStr">Ŀ��Ŀ¼�����·������/FTF/FTFDEL</param>
        /// <param name="UserName">�û���</param>
        /// <param name="UserPwd">�û�����</param>
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
                //���������Ϣ��ʾ�ļ����ɲ����򲻴��ڣ������ļ����Ѿ���ɾ��
                if (FtpResponse.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                {
                    FtpResponse.Close();
                    return true;
                }
                else
                {
                    //�����������󣺿��ܳ�������
                    FtpResponse.Close();
                    return false;
                }
            }
        }
        /// <summary>
        /// ɾ���ļ��У�ʵ�ּ���ɾ��
        /// ���أ�true�ɹ���falseʧ��
        /// </summary>
        /// <param name="BaseUriStr">��Uri�ַ�������ftp://192.168.1.190:21</param>
        /// <param name="AimUriStr">Ŀ��Ŀ¼�����·������/FTF/FTFDEL</param>
        /// <param name="UserName">�û���</param>
        /// <param name="UserPwd">�û�����</param>
        /// <returns></returns>
        public static bool DeleteFtpListDirectory(string BaseUriStr, string AimUriStr, string UserName, string UserPwd)
        {
            List<string> DirectoryDetailList = ListFtpDirectory(BaseUriStr, AimUriStr, UserName, UserPwd);
            foreach (string ListDetail in DirectoryDetailList)
            {
                if (ListDetail.EndsWith("|D"))
                {
                    //ɾ���ļ�������
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
                    //ɾ���ļ�
                    if (!DeleteFtpFile(BaseUriStr, AimUriStr + "/" + ListDetail.Split('|')[0], UserName, UserPwd))
                    {
                        return false;
                    }
                }
            }
            //ɾ����ǰ�ļ���
            if (!DeleteFtpDirectory(BaseUriStr, AimUriStr, UserName, UserPwd))
            {
                return false;
            }

            return true;
        }
        #endregion

        #region ��ȡ�ļ������ļ����ļ����б���Ϣ
        /// <summary>
        /// ��ȡ�ļ������ļ���Ϣ
        /// </summary>
        /// <param name="BaseUriStr">��Uri����ftp://192.168.1.190:21</param>
        /// <param name="AimUriStr">Ŀ��Ŀ¼�����·������/FTF/FTF1</param>
        /// <param name="UserName">�û���</param>
        /// <param name="UserPwd">�û�����</param>
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

        #region ɾ��ָ���ļ�
        /// <summary>
        /// ɾ��ָ���ļ�
        /// </summary>
        /// <param name="BaseUriStr">��Uri�ַ�������ftp://192.168.1.190:21</param>
        /// <param name="AimUriStr">Ŀ��Ŀ¼�����·������/FTF/FTFDEL</param>
        /// <param name="UserName">�û���</param>
        /// <param name="UserPwd">�û�����</param>
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

        #region �ϴ��ļ�
        /// <summary>
        /// ���ض���FTP�������ִ���ϴ��ļ�����
        /// </summary>
        /// <param name="SrcFilePath">�����ļ�·��</param>
        /// <param name="ftpRequest">��ǰftp�������</param>
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

                        //�����ϴ�����
                        ftpRequest.ContentLength = totalLen;
                        //FtpRequest.ContentOffset //�ϵ�����?

                        if (totalLen <= Convert.ToInt64(ConfigurationManager.AppSettings["SurfingDataSyn.FtpCmd.SplitUploadSize"] ?? "4194304"))
                        {
                            try
                            {
                                byte[] fileBytes = new byte[totalLen];
                                int totalRead = fs.Read(fileBytes, 0, fileBytes.Length);
                                stm.Write(fileBytes, 0, totalRead);
                                FtpLog.DebugFormat("* �ļ�{0}���ϴ�{1}�ֽ�.", trimLocalAppPath(SrcFilePath), totalRead);
                            }
                            catch (Exception slnEx)
                            {
                                FtpLog.Error("* �����ϴ�����:", slnEx);
                            }
                        }
                        else
                        {
                            #region �ֶ��ϴ�
                            while ((currentRead = fs.Read(ftpBuffer, 0, ftpBuffer.Length)) > 0)
                            {
                                int currentTryTimes = 0;
                                FtpLog.DebugFormat("* ���ζ�ȡ{0}�ֽ��ϴ�...", currentRead);

                                #region �ϴ�������
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
                                        FtpLog.ErrorFormat("* ����Socket����{0}.", socketExp.SocketErrorCode);
                                        if (socketExp.SocketErrorCode == SocketError.WouldBlock && currentTryTimes < totalTryTimes)
                                        {
                                            currentTryTimes++;
                                            System.Threading.Thread.Sleep(100);
                                            FtpLog.DebugFormat("�ϴ����󣺿�ʼ��{0}�����ԣ�", currentTryTimes);
                                            goto RetryUpload;
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        FtpLog.Error("* �����ϴ�����:", uploadEx);
                                        break;
                                    }
                                }
                                #endregion

                                if (currentTryTimes >= totalTryTimes)
                                    break;

                                readOffSet += currentRead;

                                FtpLog.DebugFormat("* �ļ�{2}���ϴ�{0}/{1} [Լ{3}].", readOffSet, totalLen, trimLocalAppPath(SrcFilePath),
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
                    result = ExecuteGetFtpFileSize(createCopy(ftpRequest)) == new FileInfo(SrcFilePath).Length; //��֤�ļ���С
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
        /// �ϴ��ļ���ָ��λ��
        /// </summary>
        /// <param name="BaseUriStr">��Uri�ַ�������ftp://192.168.1.190:21</param>
        /// <param name="AimUriStr">Ŀ��λ�ã����·������/FTF/FTFDEL/K.pdf</param>
        /// <param name="UserName">�û���</param>
        /// <param name="UserPwd">�û�����</param>
        /// <param name="SrcFilePath">Դ�ļ�λ��</param>
        /// <returns></returns>
        public static bool UploadFtpFile(string BaseUriStr, string AimUriStr, string UserName, string UserPwd, string SrcFilePath)
        {
            FtpWebRequest FtpRequest = buildFtpRequest(BaseUriStr, AimUriStr, UserName, UserPwd, 10000);
            return ExecuteUploadFtpFile(SrcFilePath, FtpRequest);
        }

        /// <summary>
        /// ��ȡԶ���ļ����ֽڴ�С
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
        /// ��ȡ�ļ��ߴ�
        /// �����ļ��ֽ���
        /// </summary>
        /// <param name="BaseUriStr">��Uri�ַ�������ftp://192.168.1.190:21</param>
        /// <param name="AimUriStr">Ŀ��λ�ã����·������/FTF/FTFDEL/K.pdf</param>
        /// <param name="UserName">�û���</param>
        /// <param name="UserPwd">�û�����</param>
        /// <returns></returns>
        public static int GetFtpFileSize(string BaseUriStr, string AimUriStr, string UserName, string UserPwd)
        {
            FtpWebRequest FtpRequest = buildFtpRequest(BaseUriStr, AimUriStr, UserName, UserPwd, 2000);
            return new FtpExecute<int>(ExecuteGetFtpFileSize)(FtpRequest);
        }
        #endregion

        #region �ļ�������
        /// <summary>
        /// �ļ����������ļ��ƶ�
        /// </summary>
        /// <param name="BaseUriStr">��Uri�ַ�������ftp://192.168.1.190:21</param>
        /// <param name="SrcUriStr">Դλ�ã����·������/FTF/FTFDEL/K.pdf</param>
        /// <param name="AimUriStr">Ŀ��λ�ã����·������/FTF/FTFDEL/K.pdf</param>
        /// <param name="UserName">�û���</param>
        /// <param name="UserPwd">�û�����</param>
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