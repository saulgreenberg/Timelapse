using System;
using System.Windows;
using Microsoft.Win32;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.Extensions;
using Timelapse.State;

namespace TimelapseTemplateEditor.EditorCode
{
    public class EditorUserRegistrySettings : UserRegistrySettings
    {
        // same key as Timelapse uses; intentional as both Timelapse and template editor are released together
        public DateTime MostRecentCheckForUpdates { get; set; }

        public RecencyOrderedList<string> MostRecentTemplates { get; private set; }

        public bool SuppressWarningToUpdateDBFilesToSQLPrompt { get; set; } // Redundant with the Timelapse variable, but lets it be set from the editor as well

        // This redundantly read/writes the suppress value shared with Timelapse
        public bool SuppressOpeningWithOlderTimelapseVersionDialog { get; set; }

        public Rect EditorWindowPosition { get; set; }

        public EditorUserRegistrySettings() : this(WindowRegistryKeys.RootKey)
        {
        }

        internal EditorUserRegistrySettings(string keyPath)
            : base(keyPath)
        {
            ReadFromRegistry();
        }
        
        public void ReadFromRegistry()
        {
            using (RegistryKey registryKey = OpenRegistryKey())
            {
                MostRecentCheckForUpdates = registryKey.GetDateTime(WindowRegistryKeys.MostRecentCheckForUpdates, DateTime.Now);
                MostRecentTemplates = registryKey.GetRecencyOrderedList(EditorConstant.Registry.EditorKey.MostRecentlyUsedTemplates);
                this.EditorWindowPosition = registryKey.GetRect(EditorConstant.Registry.EditorKey.EditorWindowPosition, new Rect(0.0, 0.0, 1350.0, 900.0));

                SuppressWarningToUpdateDBFilesToSQLPrompt = registryKey.GetBoolean(WindowRegistryKeys.SuppressWarningToUpdateDBFilesToSQLPrompt, false);
                SuppressOpeningWithOlderTimelapseVersionDialog = registryKey.GetBoolean(WindowRegistryKeys.SuppressOpeningWithOlderTimelapseVersionDialog, false);
            }
        }

        public void WriteToRegistry()
        {
            using (RegistryKey registryKey = OpenRegistryKey())
            {
                registryKey.Write(WindowRegistryKeys.MostRecentCheckForUpdates, MostRecentCheckForUpdates);
                registryKey.Write(EditorConstant.Registry.EditorKey.MostRecentlyUsedTemplates, MostRecentTemplates);
                registryKey.Write(WindowRegistryKeys.SuppressWarningToUpdateDBFilesToSQLPrompt, SuppressWarningToUpdateDBFilesToSQLPrompt);
                registryKey.Write(WindowRegistryKeys.SuppressOpeningWithOlderTimelapseVersionDialog, SuppressOpeningWithOlderTimelapseVersionDialog);
                registryKey.Write(EditorConstant.Registry.EditorKey.EditorWindowPosition, EditorWindowPosition);
            }
        }
    }
}
