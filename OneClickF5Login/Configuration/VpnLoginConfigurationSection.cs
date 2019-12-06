using System.Configuration;

namespace OneClickF5Login.Configuration
{
    public class VpnLoginConfigurationSection : ConfigurationSection
    {

        public static VpnLoginConfigurationSection GetConfig()
        {
            return (VpnLoginConfigurationSection)ConfigurationManager.GetSection("VpnLoginSettings") ?? new VpnLoginConfigurationSection();
        }

        [ConfigurationProperty("VpnLoginList")]
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
