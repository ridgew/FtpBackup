using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using System.Text;

namespace SurfingDataSyn
{
    [Serializable]
    public class FileBackUp
    {
        static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FileBackUp.config");

        /// <summary>
        /// 加载
        /// </summary>
        public static FileBackUp Load()
        {
            FileBackUp exp = new FileBackUp();
            using (FileStream fs = new FileStream(ConfigPath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Length > 5)
                {
                    XmlSerializer xs = new XmlSerializer(typeof(FileBackUp));
                    exp = (FileBackUp)xs.Deserialize(fs);
                }
                fs.Close();
            }
            return exp;
        }

        /// <summary>
        /// 保存
        /// </summary>
        public void Save()
        {
            using (FileStream fs = new FileStream(ConfigPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            {
                XmlSerializer xs = new XmlSerializer(typeof(FileBackUp));
                xs.Serialize(fs, this);
                fs.Close();
            }
        }

        /// <summary>
        /// 备份目录设置
        /// </summary>
        [XmlElement(ElementName = "Direcotry")]
        public DirBackSetting[] Direcotries { get; set; }

    }

    [Serializable]
    public class DirBackSetting
    {
        [XmlAttribute]
        public string BaseDir { get; set; }

        /// <summary>
        /// 只在星期几运行的限制
        /// </summary>
        [XmlAttribute]
        public string ExecuteWeekDays { get; set; }

        /// <summary>
        /// 是否禁止运行
        /// </summary>
        [XmlAttribute]
        public string Disabled { get; set; }

        /// <summary>
        /// 只在每月几号运行的限制
        /// </summary>
        [XmlAttribute]
        public string ExecuteDays { get; set; }

        /// <summary>
        /// 包含文件匹配模式
        /// </summary>
        public string[] IncludePathPattern { get; set; }

        /// <summary>
        /// 排除的从属目录
        /// </summary>
        public string[] ExcludeDir { get; set; }

        /// <summary>
        /// 排除的从属文件
        /// </summary>
        public string[] ExcludePath { get; set; }

        /// <summary>
        /// 判断当前是否需要备份
        /// </summary>
        /// <param name="reason">相关原因描述</param>
        /// <returns></returns>
        public bool NeedBackUpNow(ref string reason)
        {
            if (Disabled != null && Convert.ToBoolean(Disabled))
            {
                reason = "已禁止";
                return false;
            }

            StringBuilder sb = new StringBuilder();
            if (string.IsNullOrEmpty(ExecuteWeekDays) && string.IsNullOrEmpty(ExecuteDays))
            {
                return true;
            }
            else
            {
                if (!string.IsNullOrEmpty(ExecuteWeekDays))
                {
                    sb.AppendFormat("运行星期数{0};", ExecuteWeekDays);
                    string[] allDayWeeks = ExecuteWeekDays.Split(new char[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string setWkd in allDayWeeks)
                    {
                        if ((DayOfWeek)Convert.ToInt32(setWkd) == DateTime.Now.DayOfWeek)
                            return true;
                    }
                }

                if (!string.IsNullOrEmpty(ExecuteDays))
                {
                    sb.AppendFormat("运行日期数{0};", ExecuteDays);
                    string[] allDays = ExecuteDays.Split(new char[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string setDay in allDays)
                    {
                        if (Convert.ToInt32(setDay) == DateTime.Now.Day)
                            return true;
                    }
                }
            }
            reason = sb.ToString().TrimEnd(';');
            return false;
        }

        static bool StringMatchInArray(string testString, string[] patterArr)
        {
            bool result = false;
            foreach (string pat in patterArr)
            {
                //允许{}操作符号的时间参数
                string tempPattern = Regex.Replace(pat, "\\{[^\\}]+\\}", m => DateTime.Now.ToString(m.Value.Trim('{', '}')));
                //Console.WriteLine(tempPattern);
                if (Regex.IsMatch(testString, tempPattern, RegexOptions.IgnoreCase))
                {
                    result = true;
                    break;
                }
            }
            return result;
        }

        static void ExecuteListFileCreate(ref long totalLength, ref List<string> ruleList, DirBackSetting rule, DirectoryInfo rootDir, DirectoryInfo currentDir)
        {
            bool useInclude = rule.IncludePathPattern != null && rule.IncludePathPattern.Length > 0;
            bool useExclueFile = rule.ExcludePath != null && rule.ExcludePath.Length > 0;
            bool useExclueDir = rule.ExcludeDir != null && rule.ExcludeDir.Length > 0;

            foreach (FileInfo fi in currentDir.GetFiles())
            {
                string testFilePath = fi.FullName.Substring(rootDir.FullName.Length).TrimStart('\\', '/');
                //Program.DebugLog.DebugFormat("测试文件字符:{0}", testFilePath);
                if (useInclude && !StringMatchInArray(testFilePath, rule.IncludePathPattern))
                {
                    Program.InfoLog.InfoFormat("*忽略了备份文件{0}", fi.FullName);
                    continue;
                }

                if (useExclueFile && StringMatchInArray(testFilePath, rule.ExcludePath))
                {
                    Program.InfoLog.InfoFormat("*忽略了备份文件{0}", fi.FullName);
                    continue;
                }

                totalLength += fi.Length;
                ruleList.Add(fi.FullName);
            }

            foreach (DirectoryInfo di in currentDir.GetDirectories())
            {
                string testDirPath = di.FullName.Substring(rootDir.FullName.Length).TrimStart('\\', '/');
                if (useExclueDir && StringMatchInArray(testDirPath, rule.ExcludeDir))
                {
                    //Program.DebugLog.DebugFormat("测试目录字符:{0}", testDirPath);
                    Program.InfoLog.InfoFormat("*忽略了备份目录{0}", di.FullName);
                    continue;
                }
                ExecuteListFileCreate(ref totalLength, ref ruleList, rule, rootDir, di);
            }
        }

        /// <summary>
        /// 根据规则创建符合条件的文件列表
        /// </summary>
        public static void ExecuteListCreate(ref long totalLength, ref List<string> ruleList, DirBackSetting rule)
        {
            DirectoryInfo currentDir = new DirectoryInfo(rule.BaseDir);
            if (!currentDir.Exists)
            {
                return;
            }
            else
            {
                ExecuteListFileCreate(ref totalLength, ref ruleList, rule, currentDir, currentDir);
            }
        }
    }
}
