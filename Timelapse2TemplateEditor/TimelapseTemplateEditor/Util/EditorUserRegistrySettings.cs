using Microsoft.Win32;
using System;
using Timelapse.Util;

namespace Timelapse.Editor.Util
{
    public class EditorUserRegistrySettings : UserRegistrySettings
    {
        // same key as Timelapse uses; intentional as both Timelapse and template editor are released together
        public DateTime MostRecentCheckForUpdates { get; set; }

        public RecencyOrderedList<string> MostRecentTemplates { get; private set; }

        public bool SuppressWarningToUpdateDBFilesToSQLPrompt { get; set; } // Redundant with the Timelapse variable, but lets it be set from the editor as well
        
        // This redundantly read/writes the suppress value shared with Timelapse
        public bool SuppressOpeningWithOlderTimelapseVersionDialog { get; set; }

        public EditorUserRegistrySettings(): this(Constant.WindowRegistryKeys.RootKey)
        {
        }

        internal EditorUserRegistrySettings(string keyPath)
            : base(keyPath)
        {
            this.ReadFromRegistry();
        }

        public void ReadFromRegistry()
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                this.MostRecentCheckForUpdates = registryKey.GetDateTime(Constant.WindowRegistryKeys.MostRecentCheckForUpdates, DateTime.Now);
                this.MostRecentTemplates = registryKey.GetRecencyOrderedList(EditorConstant.Registry.EditorKey.MostRecentlyUsedTemplates);
                this.SuppressWarningToUpdateDBFilesToSQLPrompt = registryKey.GetBoolean(Constant.WindowRegistryKeys.SuppressWarningToUpdateDBFilesToSQLPrompt, false);
                this.SuppressOpeningWithOlderTimelapseVersionDialog = registryKey.GetBoolean(Constant.WindowRegistryKeys.SuppressOpeningWithOlderTimelapseVersionDialog, false);
            }
        }

        public void WriteToRegistry()
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                registryKey.Write(Constant.WindowRegistryKeys.MostRecentCheckForUpdates, this.MostRecentCheckForUpdates);
                registryKey.Write(EditorConstant.Registry.EditorKey.MostRecentlyUsedTemplates, this.MostRecentTemplates);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressWarningToUpdateDBFilesToSQLPrompt, this.SuppressWarningToUpdateDBFilesToSQLPrompt);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressOpeningWithOlderTimelapseVersionDialog, this.SuppressOpeningWithOlderTimelapseVersionDialog);
            }
        }
    }
}
