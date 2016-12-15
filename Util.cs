using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;

namespace SurfingDataSyn
{
    public static class Util
    {
        public static bool ProtectSection(string exePath, string sectionName)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(exePath);
            ConfigurationSection section = config.GetSection(sectionName);
            if (section != null && !section.SectionInformation.IsProtected)
            {
                section.SectionInformation.ProtectSection("RsaProtectedConfigurationProvider");
                config.Save();
                return true;
            }
            return false;
        }

        public static bool UnProtectSection(string exePath, string sectionName)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(exePath);
            ConfigurationSection section = config.GetSection(sectionName);
            if (section != null && section.SectionInformation.IsProtected)
            {
                section.SectionInformation.UnprotectSection();
                config.Save();
                return true;
            }
            return false;
        }
    }
}
