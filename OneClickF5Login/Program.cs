using System;
using System.Configuration;
using System.Threading;
using System.Windows.Forms;
using OneClickF5Login.Configuration;

namespace OneClickF5Login
{
    static class Program
    {
        public static Mutex Mutex1 { get; private set; }

        public const string SectionName = "VpnLoginSettings";

        private static Form1 _form;

        [STAThread]
        static void Main()
        {
            const string appName = "OneClickF5Login";

            Mutex1 = new Mutex(true, appName, out var createdNew);
            if (!createdNew)  
            {  
                //app is already running! Exiting the application  
                return;  
            }  
            SecureSection();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            _form = new Form1();
            Application.Run(_form);
        }

        public static void UnSecureSection()
        {
            var section = GetSettingsSection();
            section.SectionInformation.UnprotectSection();
            section.SectionInformation.ForceSave = true;
            section.CurrentConfiguration.Save(ConfigurationSaveMode.Full);
        }

        public static void SecureSection()
        {
            var section = GetSettingsSection();
            if (section.SectionInformation.IsProtected) return;
            if (section.ElementInformation.IsLocked) return;
            section.SectionInformation.ProtectSection("DataProtectionConfigurationProvider");
            section.SectionInformation.ForceSave = true;
            section.CurrentConfiguration.Save(ConfigurationSaveMode.Full);
        }

        public static VpnLoginConfigurationSection GetSettingsSection()
        {
            var appConfiguration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            if (appConfiguration.GetSection(SectionName) is VpnLoginConfigurationSection section) return section;
            appConfiguration.Sections.Add(SectionName, new VpnLoginConfigurationSection() { LockItem = false });
            appConfiguration.Save(ConfigurationSaveMode.Full);
            section = appConfiguration.GetSection(SectionName) as VpnLoginConfigurationSection;
            return section;
        }

        private static void OnProcessExit (object sender, EventArgs e)
        {
            _form?.CloseBrowserDriver();
        }
    }
}
