using System.Configuration;

namespace OneClickF5Login.Configuration
{
    public class VpnLoginConfigurationElementCollection : ConfigurationElementCollection
    {
      

        public new VpnLoginConfigurationElement this[string uid]
        {
            get => (VpnLoginConfigurationElement) BaseGet(uid);
            set
            {
                if(BaseGet(uid) != null)
                {
                    BaseRemoveAt(BaseIndexOf(BaseGet(uid)));
                }
                BaseAdd(value);
            }
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new VpnLoginConfigurationElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((VpnLoginConfigurationElement)element).Uid;
        }
    }
}
