using Microsoft.Win32;

namespace Timelapse.State
{
    /// <summary>
    /// Base class for manipulating application's user preferences and related information in the registry.
    /// </summary>
    public class UserRegistrySettings
    {
        #region Private Properties
        private readonly string keyPath;
        #endregion

        #region Constructor
        public UserRegistrySettings(string keyPath)
        {
            this.keyPath = keyPath;
        }
        #endregion

        #region Public Methods
        public RegistryKey OpenRegistryKey()
        {
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(this.keyPath, true) 
                                      ?? Registry.CurrentUser.CreateSubKey(this.keyPath);
            return registryKey;
        }
        #endregion
    }
}
