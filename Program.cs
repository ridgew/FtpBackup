using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using log4net;
using SevenZip;

namespace SurfingDataSyn
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = typeof(Program).Assembly.GetName().Name;

            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            DebugLog.DebugFormat("* 任务启动...");

            if (args != null && args.Length > 0)
            {
                foreach (string localFile in args)
                {
                    if (localFile == "enc" || localFile == "dec")
                    {
                        if (localFile == "enc")
                        {
                            Util.ProtectSection(System.Reflection.Assembly.GetExecutingAssembly().Location, "appSettings");
                        }
                        else
                        {
                            Util.UnProtectSection(System.Reflection.Assembly.GetExecutingAssembly().Location, "appSettings");
                        }
                    }
                    else
                    {
                        if (File.Exists(localFile))
                        {
                            FtpUploadLocal(localFile);
                        }
                        else
                        {
                            DebugLog.DebugFormat("* 传递了非文件路径的参数{0}...", localFile);
                        }
                    }
                }
            }
            else
            {
                string newFileName = string.Concat(DateTime.Now.ToString("yyyyMMddHHmmss"), ".7z");
                string exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigurationManager.AppSettings["SynDataBackDir"]);
                if (!Directory.Exists(exportDir))
                    Directory.CreateDirectory(exportDir);
                string exportFilePath = Path.Combine(exportDir, newFileName);
                DebugLog.DebugFormat("* 备份文件保存在{0}", exportFilePath);

                bool backupSuccess = CreateBackupArchive(exportFilePath);
                if (!backupSuccess)
                {
                    DebugLog.DebugFormat("* 本次备份数据失败！");
                }
                else
                {
                    FtpUploadLocal(exportFilePath);
                }
            }

            DebugLog.DebugFormat("* 任务退出.");
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ErrorLog.Error("* 发生未处理的异常:", (Exception)e.ExceptionObject);
        }

        /// <summary>
        /// 调试日志
        /// </summary>
        internal static ILog DebugLog = LogManager.GetLogger("DebugLog");
        internal static ILog ErrorLog = LogManager.GetLogger("ErrorLog");
        internal static ILog InfoLog = LogManager.GetLogger("InfoLog");


        static FtpWebRequest CreateFtpRequest(string appendPath)
        {
            FtpWebRequest ftpReq = (FtpWebRequest)WebRequest.Create(string.Concat("ftp://",
                ConfigurationManager.AppSettings["FtpHost"] ?? "118.123.205.181:21", appendPath));

            ftpReq.KeepAlive = true;
            ftpReq.ReadWriteTimeout = Convert.ToInt32(ConfigurationManager.AppSettings["System.Net.FtpWebRequest.ReadWriteTimeout"] ?? "100000");
            ftpReq.UsePassive = Convert.ToBoolean(ConfigurationManager.AppSettings["System.Net.FtpWebRequest.UsePassive"] ?? "true");

            ftpReq.Credentials = new NetworkCredential(ConfigurationManager.AppSettings["FtpUserName"] ?? "gwregister",
                ConfigurationManager.AppSettings["FtpPassword"] ?? "gwsoft#com.cn");

            return ftpReq;
        }

        static void FtpUploadLocal(string localFilePath)
        {
            string dayRootFullPath = string.Concat(ConfigurationManager.AppSettings["ExportBaseDir"] ?? "/", DateTime.Now.ToString("yyyyMMdd"));
            DebugLog.DebugFormat("* FTP数据保存完整目录地址为{0}", dayRootFullPath);

            DebugLog.DebugFormat("* 开始FTP创建目录，如不存在...");
            if (FtpCmd.CreateFtpListDirectory(string.Concat("ftp://",
                ConfigurationManager.AppSettings["FtpHost"] ?? "118.123.215.174:21"),
                dayRootFullPath,
                ConfigurationManager.AppSettings["FtpUserName"] ?? "clientbak",
                ConfigurationManager.AppSettings["FtpPassword"] ?? "client!&$"))
            {
                string ftpRootPath = string.Concat(dayRootFullPath, "/", Path.GetFileName(localFilePath));
                DebugLog.DebugFormat("* 准备FTP上传本地文件 {0} 到 {1} ...", localFilePath, ftpRootPath);
                bool ftpResult = FtpCmd.ExecuteUploadFtpFile(localFilePath, CreateFtpRequest(ftpRootPath));
                DebugLog.DebugFormat("* ☆上传文件 {0} {1}！", localFilePath, ftpResult ? "成功" : "失败");
            }
            else
            {
                DebugLog.DebugFormat("* FTP创建完整目录地址{0}失败", dayRootFullPath);
            }
        }

        /// <summary>
        /// 主要的创建备份逻辑
        /// </summary>
        static bool CreateBackupArchive(string archivePath)
        {
            DebugLog.DebugFormat("* 加载备份配置...");
            FileBackUp bkSeting = FileBackUp.Load();
            List<string> allBackFileList = new List<string>();
            long totalLength = 0L;
            string totalLenStr = "0kb";

            #region 创建备份文件集合
            if (bkSeting.Direcotries == null || bkSeting.Direcotries.Length < 1)
            {
                InfoLog.Info("因备份目录没有设置退出备份包创建！");
                return false;
            }
            else
            {
                foreach (DirBackSetting dirSet in bkSeting.Direcotries)
                {
                    string reason = "";
                    if (dirSet.NeedBackUpNow(ref reason))
                    {
                        DirBackSetting.ExecuteListCreate(ref totalLength, ref allBackFileList, dirSet);
                    }
                    else
                    {
                        InfoLog.InfoFormat("根据规则:{1}, 目录[{0}]没有创建备份！", dirSet.BaseDir, reason);
                    }
                }

                totalLenStr = ((float)totalLength / 1024.00f).ToString("0.00") + "kb";
                if (totalLength > 1024 * 1024)
                {
                    totalLenStr = ((float)totalLength / (1024.00 * 1024.00f)).ToString("0.00") + "Mb";
                }
                InfoLog.InfoFormat("备份文件列表已创建，总计文件长度{0}！", totalLenStr);
            }
            #endregion


            DateTime beginDateTime = DateTime.Now;

            SevenZipCompressor sc = new SevenZipCompressor();
            sc.EncryptHeaders = true;
            sc.ZipEncryptionMethod = ZipEncryptionMethod.Aes256;
            sc.ArchiveFormat = OutArchiveFormat.SevenZip;

            DateTime lastReportTime = DateTime.Now;
            byte lastPercentDone = 0;
            sc.Compressing += (s, p) =>
            {
                if (p.PercentDone > lastPercentDone && (DateTime.Now - lastReportTime).TotalSeconds > 1)
                {
                    InfoLog.InfoFormat("*压缩进度:{0}%", p.PercentDone);
                    lastReportTime = DateTime.Now;
                    lastPercentDone = p.PercentDone;
                }
            };

            sc.CompressionFinished += (s, e) =>
            {
                InfoLog.InfoFormat("*压缩原始数据{0}完成，全部耗时:{1}。", totalLenStr, DateTime.Now - beginDateTime);
            };

            //if (totalLength > (1024 * 1024 * 20))
            //{
            //    InfoLog.Info("总文件大小超过20M，不执行压缩");
            //    return false;
            //}

            try
            {
                sc.CompressFilesEncrypted(archivePath,
                    ConfigurationManager.AppSettings["SurfingDataSyn.FileBackUp.ArchivePassword"] ?? "gwsoftctmarket",
                    allBackFileList.ToArray());
            }
            catch (Exception cpe)
            {
                ErrorLog.Error("*创建备份压缩包出现错误", cpe);
                return false;
            }
            return true;
        }

    }
}
