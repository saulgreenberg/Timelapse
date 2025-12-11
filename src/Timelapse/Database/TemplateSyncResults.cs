using System;
using System.Collections.Generic;
using Timelapse.DataTables;

namespace Timelapse.Database
{
    /// <summary>
    /// This class will eventually hold the level-specific data labels that should be added/deleted/renamed in case of a mismatch
    /// between the .ddb and the .tdb template tables. The initial key is usually the level.
    /// Basically this is a data structure for storing information about efforts to sync the templates
    /// </summary>
    public class TemplateSyncResults
    {
        #region Public Properties
        // These lists contain differences between the affected MetadataInfo rows between the .tdb and .ddb template levels
        public List<Tuple<MetadataInfoRow, int, int>> InfoRowsInDdbToRenumber { get; set; } // DDB rows,, oldDdbLevel, newTdbLevel
        public List<MetadataInfoRow> InfoRowsInDdbToDelete { get; set; } // co
        public List<Tuple<MetadataInfoRow, MetadataInfoRow>> InfoRowsCommon { get; set; }  // TDB rows, DDB rows
        public List<MetadataInfoRow> InfoRowsInTdbToAdd { get; set; } // TDB Rows
        public List<Tuple<MetadataInfoRow, MetadataInfoRow>> InfoRowsWithNameChanges { get; set; } // TDB rows, DDB rows
        public List<Tuple<MetadataInfoRow, MetadataInfoRow>> InfoRowsWithDifferentGuidSameAlias { get; set; } // TDB rows, DDB rows

        // Various state variables concerning differences between the InfoHierarchy tructure
        public bool InfoHierarchyIncompatibleDifferences { get; set; } // Iincompatible differences between the info hierarchies
        public bool InfoHierarchyTdbDiffersOnlyWithAppendedLevels { get; set; } // Iincompatible differences between the info hierarchies

        // The last level in the level list sequence that is in common between the Tdb and DDb
        public int LastLevelInCommon { get; set; } = 0;

        // These  lists collect information about possible mismatches between the .tdb and the .ddb template controls,
        // and what should eventually be added, deleted or renamed
        // For the ones with a 'ByLevel' suffix:
        // - The key is the level number
        // - level 0 is reserved for the image-level controls

        // Lists of data label differences between Tdb and Ddb
        public Dictionary<int, Dictionary<string, string>> DataLabelsInTdbButNotDdbByLevel { get; set; } // Level,  DataLabel, Type
        public Dictionary<int, Dictionary<string, string>> DataLabelsInDdbButNotTdbByLevel { get; set; }  // Level, DataLabel, Type

        // Lists of data labels to eventually  add or delete
        public Dictionary<int, List<String>> DataLabelsToAddByLevel { get; set; } // Level, Datalabels to add
        public Dictionary<int, List<String>> DataLabelsToDeleteByLevel { get; set; } // Level, Datalabels to deleted
        public Dictionary<int, List<KeyValuePair<string, string>>> DataLabelsToRenameByLevel { get; set; } // Level, Datalabels to Rename


        // These lists collect error messages and warnings concerning control synchronization between the templates
        public Dictionary<int, List<string>> ControlSynchronizationErrorsByLevel { get; }   // Level, human-readable error strings to print
        public Dictionary<int, List<string>> ControlSynchronizationWarningsByLevel { get; } // Level, human-readable warning strings to print

        // Signals whether or not to use the template found in the Image database instead of the Template database
        public bool UseTdbTemplate { get; set; }    // true if we should use the Tdb template
        public bool SyncRequiredAsNonCriticalDataFieldAttributesDiffer { get; set; } //  True if we should silently update the DDB template with the TDB template due to data field differences
        public bool SyncRequiredAsFolderLevelsDiffer { get; set; } //  True if we should update the DDB template with the TDB template due to level differences
        public bool SyncRequiredAsDataLabelsDiffer => GetSyncRequiredAsDataLabelsDiffer(); // True if the data fields differ in images and/or levels
        public bool SyncRequiredToUpdateInfoTableGuids;
        #endregion

        #region Constructors
        // ReSharper disable once ConvertConstructorToMemberInitializers
        public TemplateSyncResults()
        {
            InfoRowsInDdbToRenumber = [];
            InfoRowsInDdbToDelete = [];
            InfoRowsCommon = [];
            InfoRowsInTdbToAdd = [];
            InfoRowsWithNameChanges = [];
            InfoRowsWithDifferentGuidSameAlias = [];  // Form: tdbRow, ddbRow
                                                                            DataLabelsInTdbButNotDdbByLevel = [];
            DataLabelsInDdbButNotTdbByLevel = [];

            DataLabelsToAddByLevel = [];
            DataLabelsToDeleteByLevel = [];
            DataLabelsToRenameByLevel = [];

            ControlSynchronizationErrorsByLevel = [];
            ControlSynchronizationWarningsByLevel = [];

            UseTdbTemplate = true;
            SyncRequiredAsNonCriticalDataFieldAttributesDiffer = false;
            SyncRequiredAsFolderLevelsDiffer = false;
            InfoHierarchyIncompatibleDifferences = false;
            InfoHierarchyTdbDiffersOnlyWithAppendedLevels = false;
            SyncRequiredToUpdateInfoTableGuids = false;
        }
        #endregion

        #region Private helpers
        // Invoked by the SyncRequiredAsDataLabelsDiffer getter
        private bool GetSyncRequiredAsDataLabelsDiffer()
        {
            foreach (KeyValuePair<int, Dictionary<string, string>> kvp in DataLabelsInTdbButNotDdbByLevel)
            {
                if (DataLabelsInTdbButNotDdbByLevel[kvp.Key].Count > 0)
                {
                    return true;
                }
            }
            foreach (KeyValuePair<int, Dictionary<string, string>> kvp in DataLabelsInDdbButNotTdbByLevel)
            {
                if (DataLabelsInDdbButNotTdbByLevel[kvp.Key].Count > 0)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion
    }
}
