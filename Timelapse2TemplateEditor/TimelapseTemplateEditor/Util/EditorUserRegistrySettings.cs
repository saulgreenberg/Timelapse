using Microsoft.Win32;
using System;
using Timelapse.Util;

namespace Timelapse.Editor.Util
{
    internal class EditorUserRegistrySettings : UserRegistrySettings
    {
        // same key as Timelapse uses; intentional as both Timelapse and template editor are released together
        public DateTime MostRecentCheckForUpdates { get; set; }

        public RecencyOrderedList<string> MostRecentTemplates { get; private set; }

        public bool SuppressWarningToUpdateDBFilesToSQLPrompt { get; set; } // Redundant with the Timelapse variable, but lets it be set from the editor as well
        public EditorUserRegistrySettings()
            : this(Constant.WindowRegistryKeys.RootKey)
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
            }
        }

        public void WriteToRegistry()
        {
            using (RegistryKey registryKey = this.OpenRegistryKey())
            {
                registryKey.Write(Constant.WindowRegistryKeys.MostRecentCheckForUpdates, this.MostRecentCheckForUpdates);
                registryKey.Write(EditorConstant.Registry.EditorKey.MostRecentlyUsedTemplates, this.MostRecentTemplates);
                registryKey.Write(Constant.WindowRegistryKeys.SuppressWarningToUpdateDBFilesToSQLPrompt, this.SuppressWarningToUpdateDBFilesToSQLPrompt);
            }
        }
    }
}
