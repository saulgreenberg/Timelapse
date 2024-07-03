using System;
using System.Collections.Generic;

namespace DialogUpgradeFiles.Database
{
    /// <summary>
    /// This class will eventually hold the data labels that should be added/deleted/renamed in case of a mismatch
    /// between the .ddb and the .tdb template tables
    /// Basically a data structure for storing information about efforts to sync the templates
    /// </summary>
    public class TemplateSyncResults
    {
        #region Public Properties
        // These  lists collect information about possible mismatches between the .tdb and the .ddb template, and what should eventually be added, deleted or renamed
        public Dictionary<string, string> DataLabelsInTemplateButNotImageDatabase { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> DataLabelsInImageButNotTemplateDatabase { get; set; } = new Dictionary<string, string>();

        // These  lists collect information about what should eventually be added, deleted or renamed
        public List<String> DataLabelsToAdd { get; set; } = new List<String>();
        public List<String> DataLabelsToDelete { get; set; } = new List<String>();
        public List<KeyValuePair<string, string>> DataLabelsToRename { get; set; } = new List<KeyValuePair<string, string>>();

        // These lists collect error messages and warnings concerning control synchronization between the templates
        public List<string> ControlSynchronizationErrors { get; } = new List<string>();
        public List<string> ControlSynchronizationWarnings { get; } = new List<string>();

        // Signals whether or not to use the template found in the Image database instead of the Template database
        public bool UseTemplateDBTemplate { get; set; } = true;

        // Signals whether a silent update of the Image database template should be performed at the minimum
        public bool SyncRequiredAsNonCriticalFieldsDiffer { get; set; } = false;

        // ReSharper disable once UnusedMember.Global
        public bool SyncRequiredAsDataLabelsDiffer => this.DataLabelsInTemplateButNotImageDatabase.Count > 0 || this.DataLabelsInImageButNotTemplateDatabase.Count > 0;

        public bool SyncRequiredAsChoiceMenusDiffer { get; set; } = false;

        #endregion
    }
}
