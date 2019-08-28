using System.Configuration;

namespace OneClickF5Login.Configuration
{
    public class VpnLoginConfigurationSection : ConfigurationSection
    {

        public static VpnLoginConfigurationSection GetConfig()
        {
            return (VpnLoginConfigurationSection)System.Configuration.ConfigurationManager.GetSection("VpnLoginSettings") ?? new VpnLoginConfigurationSection();
        }

        [System.Configuration.ConfigurationProperty("VpnLoginList")]
        [ConfigurationCollection(typeof(VpnLoginConfigurationElementCollection), AddItemName = "VpnLoginItem")]
        public VpnLoginConfigurationElementCollection VpnLoginList
        {
            get
            {
                var o = this["VpnLoginList"];
                return o as VpnLoginConfigurationElementCollection ;
            }
        }

    }
}
