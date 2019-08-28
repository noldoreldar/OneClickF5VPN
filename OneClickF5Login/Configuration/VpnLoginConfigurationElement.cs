using System.Configuration;

namespace OneClickF5Login.Configuration
{
    public class VpnLoginConfigurationElement : ConfigurationElement
    {

        [ConfigurationProperty("uid", IsRequired = true)]
        public string Uid
        {
            get => this["uid"] as string;
            set => this["uid"] = value;
        }

        [ConfigurationProperty("username", IsRequired = false)]
        public string Username
        {
            get => this["username"] as string;
            set => this["username"] = value;
        }

        [ConfigurationProperty("password", IsRequired = false)]
        public string Password
        {
            get => this["password"] as string;
            set => this["password"] = value;
        }

        [ConfigurationProperty("rememberPassword", IsRequired = false)]
        public bool? RememberPassword  {
            get => this["rememberPassword"] as bool?;
            set => this["rememberPassword"] = value;
        }

        [ConfigurationProperty("chromeDataPath", IsRequired = false)]
        public string ChromeDataPath {
            get => this["chromeDataPath"] as string;
            set => this["chromeDataPath"] = value;
        }


        [ConfigurationProperty("chromePath", IsRequired = false)]
        public string ChromePath {
            get => this["chromePath"] as string;
            set => this["chromePath"] = value;
        }

        
        [ConfigurationProperty("otpKey", IsRequired = false)]
        public string OtpKey {
            get => this["otpKey"] as string;
            set => this["otpKey"] = value;
        }

        [ConfigurationProperty("showBrowser", IsRequired = false)]
        public bool? ShowBrowser  {
            get => this["showBrowser"] as bool?;
            set => this["showBrowser"] = value;
        }
    }
}
