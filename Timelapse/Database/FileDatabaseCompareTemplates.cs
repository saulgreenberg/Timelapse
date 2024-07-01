using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Util;

namespace Timelapse.Database
{
    public partial class FileDatabase
    {
        #region Compare the level hierarchies i.e., additions, deletions, renames, etc
        private void MetadataCompareLevelHierarchyStructureBetweenTemplates(CommonDatabase tdbDatabase, TemplateSyncResults templateSyncResults)
        {
            // shortcut declarations
            DataTableBackedList<MetadataInfoRow> ddbInfo = this.MetadataInfo;
            DataTableBackedList<MetadataInfoRow> tdbInfo = tdbDatabase.MetadataInfo;

            // Get the max levels for the tdb and ddb MetadataInfo tables. 
            // If the table doesn't exist or there are no rows in it, the max level will be 0
            // TODO note that we currently don't check if there are any controls associated with each level - Not sure if we need to

            // Compare the DDB template to the TDB template and produce a list of
            // - ddb rows to delete (e.g., if its not in the tdb)
            // - ddb rows to renumber (if its common to both but their level numbers differ)
            foreach (MetadataInfoRow ddbRow in ddbInfo)
            {
                // Is this row's guid or (non-empty) Alias present in both the ddb and tdb? 
                // TODO This may be error prone as there is no guarantee that the alias match is correct> Could be more stringent by checking for level equality.
                // Guids aren't equal, so check if aliases are equal and if so assume they are the same
                MetadataInfoRow tdbRow = tdbInfo.FirstOrDefault(s => s.Guid == ddbRow.Guid) ?? tdbInfo.FirstOrDefault(s => s.Alias == ddbRow.Alias);
                if (tdbRow != null)
                {
                    // Yes, the row is present
                    // Add this pair to the Common list as they are common to both
                    templateSyncResults.InfoRowsCommon.Add(new Tuple<MetadataInfoRow, MetadataInfoRow>(tdbRow, ddbRow));

                    // Check: Different levels?
                    if (tdbRow.Level != ddbRow.Level)
                    {
                        // Yes. add this common pair to the Renumber list as their level number differs
                        templateSyncResults.InfoRowsInDdbToRenumber.Add(new Tuple<MetadataInfoRow, int, int>(ddbRow, ddbRow.Level, tdbRow.Level));
                        templateSyncResults.SyncRequiredAsFolderLevelsDiffer = true;
                    }

                    // Check: Unequal aliases?
                    if (tdbRow.Alias != ddbRow.Alias)
                    {
                        // Yes. Add this common pair to the NameChanges list as their (non-empty which means user-defined) alias has changed
                        templateSyncResults.InfoRowsWithNameChanges.Add(new Tuple<MetadataInfoRow, MetadataInfoRow>(tdbRow, ddbRow));
                        templateSyncResults.SyncRequiredAsFolderLevelsDiffer = true;
                    }
                }
                else
                {
                    // No, as the row is absent in the tdb, we should mark it for deletion
                    templateSyncResults.InfoRowsInDdbToDelete.Add(ddbRow);
                    templateSyncResults.SyncRequiredAsFolderLevelsDiffer = true;
                }
                templateSyncResults.SyncRequiredToUpdateInfoTableGuids = true;
            }

            // Compare the guids and aliases. If the aliases are all on the same level but at least one guid differs, then
            // we should indicate that the guids likely need updating.
            if (ddbInfo.RowCount == tdbInfo.RowCount)
            {
                bool guidDiffers = false;
                bool aliasDiffers = false;
                for (int i = 0; i < ddbInfo.RowCount; i++)
                {
                    if (tdbInfo[i].Alias != ddbInfo[i].Alias)
                    {
                        // We just need to notice one alias difference
                        aliasDiffers = true;
                        break;
                    }
                    if (tdbInfo[i].Guid != ddbInfo[i].Guid)
                    {
                        guidDiffers = true;
                    }
                }
                templateSyncResults.SyncRequiredToUpdateInfoTableGuids = false == aliasDiffers && guidDiffers;
            }

            // Now compile the Add list, by collecting rows only in the Tdb,
            // which is determined by seeing if the TDB row is absent from the DDB
            foreach (MetadataInfoRow tdbRow in tdbInfo)
            {
                // Is this tdb's row's guid or the (non-empty) Alias absent in the ddb? 
                // TODO This may be error prone as there is no guarantee that the alias mismatcj is actually a valid mismatch
                MetadataInfoRow ddbRow = ddbInfo.FirstOrDefault(s => s.Guid == tdbRow.Guid);
                if (null == ddbRow)
                {
                    if (string.IsNullOrWhiteSpace(tdbRow.Alias))
                    {
                        // Guid is not in ddb row, and there is no alias to check against the ddb row
                        // So we assume its an added row
                        templateSyncResults.InfoRowsInTdbToAdd.Add(tdbRow);
                        templateSyncResults.SyncRequiredAsFolderLevelsDiffer = true;
                    }
                    else
                    {
                        ddbRow = ddbInfo.FirstOrDefault(s => s.Alias == tdbRow.Alias);
                        if (null != ddbRow)
                        {
                            // TODO Test could be more stringent by demanding the levels be equal as well
                            // Guid is not in ddb, but its alias is in the ddb
                            // So we  assume its a renamed row
                            templateSyncResults.InfoRowsWithDifferentGuidSameAlias.Add(new Tuple<MetadataInfoRow, MetadataInfoRow>(tdbRow, ddbRow));
                        }
                        else
                        {
                            // Guid is not in ddb, and neither is its alias 
                            // So we have to assume its an added row 
                            templateSyncResults.InfoRowsInTdbToAdd.Add(tdbRow);
                            templateSyncResults.SyncRequiredAsFolderLevelsDiffer = true;
                        }
                    }
                }
            }

            // Finally, compare the alias names in the Add list to those in the Deleted list.
            // If they are the same, it is probable that this pair should be considered a Rename instead of two distinct New/Delete items
            // TODO: Have this become an operation verified by the user. For example, this may not work if the user had not intended to created an alias i.e., may consider it a rename when it is actually a New/Delete
            for (int level = templateSyncResults.InfoRowsInTdbToAdd.Count - 1; level >= 0; level--)
            {
                MetadataInfoRow tdbRow = templateSyncResults.InfoRowsInTdbToAdd[level];
                if (string.IsNullOrEmpty(tdbRow.Alias))
                {
                    // Ignore empty aliases as we cannot determine if this is a rename or not
                    continue;
                }
                MetadataInfoRow ddbRow = templateSyncResults.InfoRowsInDdbToDelete.FirstOrDefault(s => s.Alias == tdbRow.Alias);
                if (ddbRow == null)
                {
                    continue;
                }
                // Aliases are the same, even though GUID differs.
                // TODO We assume same aliases are really the same level, although it really should be something we should verify with the user
                templateSyncResults.InfoRowsWithDifferentGuidSameAlias.Add(new Tuple<MetadataInfoRow, MetadataInfoRow>(tdbRow, ddbRow));
                templateSyncResults.InfoRowsInTdbToAdd.Remove(tdbRow);
                templateSyncResults.InfoRowsInDdbToDelete.Remove(ddbRow);
            }

            // Set a few other fields if the hierarchy has changed,
            // Check: Any changes>
            if (templateSyncResults.InfoRowsInDdbToRenumber.Count != 0 || templateSyncResults.InfoRowsInDdbToDelete.Count != 0 || templateSyncResults.InfoRowsInTdbToAdd.Count != 0)
            {
                // Yes. Check: Are the changes only levels appended at the end?
                if (templateSyncResults.InfoRowsInTdbToAdd.Count != 0 && templateSyncResults.InfoRowsInDdbToRenumber.Count == 0 && templateSyncResults.InfoRowsInDdbToDelete.Count == 0)
                {
                    // Yes. If its added to the end, we can handle that.
                    templateSyncResults.InfoHierarchyTdbDiffersOnlyWithAppendedLevels = true;
                }
                else
                {
                    // No. Changes are incompatable as hierarchical differences will lead to a loss of data for subsequent levels 
                    // TODO: We really should check if deleted levels occur at the end, where there is no data associated with it and allow that, or generate warnings and delete the data.
                    templateSyncResults.InfoHierarchyIncompatableDifferences = true;
                }
            }
        }
        #endregion 

        #region Compare the image-level template controls
        // Compare image-level templates held by the ddb vs tdb database and return the results in the templateSyncResults structure.
        private async Task CompareImageControlsBetweenTemplates(CommonDatabase tdbDatabase, TemplateSyncResults templateSyncResults)
        {
            await Task.Run(() =>
            {

                int level = 0; // We are only considering the image-level data field controls here

                // Get the datalabels in the various templates 
                Dictionary<string, string> tdbDataLabels = tdbDatabase.GetTypedDataLabelsExceptIDInSpreadsheetOrderFromControls();
                Dictionary<string, string> ddbDataLabels = this.GetTypedDataLabelsExceptIDInSpreadsheetOrderFromControls();

                templateSyncResults.DataLabelsInTdbButNotDdbByLevel[level] = Compare.Dictionary1ExceptDictionary2(tdbDataLabels, ddbDataLabels);
                templateSyncResults.DataLabelsInDdbButNotTdbByLevel[level] = Compare.Dictionary1ExceptDictionary2(ddbDataLabels, tdbDataLabels);

                // Check for differences between the Template Table in the .tdb and .ddb database.
                bool areNewColumnsInTdbTemplate = templateSyncResults.DataLabelsInTdbButNotDdbByLevel[level].Count > 0;
                bool areDeletedColumnsInTdbTemplate = templateSyncResults.DataLabelsInDdbButNotTdbByLevel[level].Count > 0;

                // Synchronization Issues 1: Mismatch control types. Unable to update as there is at least one control type mismatch 
                // We need to check that the dataLabels in the .ddb template are of the same type as those in the .tdb template
                // If they are not, then we need to flag that.
                foreach (string dataLabel in ddbDataLabels.Keys)
                {
                    // Check: Is the .ddb control absent from the .tdb template
                    if (!tdbDataLabels.ContainsKey(dataLabel))
                    {
                        // Yes. This will be dealt with later
                        continue;
                    }

                    // Check: are the control types different?
                    ControlRow ddbDatabaseControl = this.GetControlFromControls(dataLabel);
                    ControlRow tdbControl = tdbDatabase.GetControlFromControls(dataLabel);
                    if (ddbDatabaseControl.Type != tdbControl.Type)
                    {
                        // Allow the following changes to data types
                        if ((ddbDatabaseControl.Type == Constant.Control.AlphaNumeric && tdbControl.Type == Constant.Control.Note) || // Alpha -> Note
                            (ddbDatabaseControl.Type == Constant.Control.AlphaNumeric && tdbControl.Type == Constant.Control.MultiLine) || // Alpha -> Multiline

                            (ddbDatabaseControl.Type == Constant.Control.Note && tdbControl.Type == Constant.Control.MultiLine) || // Note -> Multiline

                            (ddbDatabaseControl.Type == Constant.Control.MultiLine && tdbControl.Type == Constant.Control.Note) || // MultiLine -> Note

                            (ddbDatabaseControl.Type == Constant.Control.Counter && tdbControl.Type == Constant.Control.IntegerPositive) || // Count -> IntPos  
                            (ddbDatabaseControl.Type == Constant.Control.Counter && tdbControl.Type == Constant.Control.IntegerAny) || // Count -> IntAny  
                            (ddbDatabaseControl.Type == Constant.Control.Counter && tdbControl.Type == Constant.Control.DecimalPositive) || // Count -> DecPos  
                            (ddbDatabaseControl.Type == Constant.Control.Counter && tdbControl.Type == Constant.Control.DecimalAny) || // Count -> DecAny  
                            (ddbDatabaseControl.Type == Constant.Control.Counter && tdbControl.Type == Constant.Control.Note) || // Count -> Note  
                            (ddbDatabaseControl.Type == Constant.Control.Counter && tdbControl.Type == Constant.Control.MultiLine) || // Count -> MultiLine  
                            (ddbDatabaseControl.Type == Constant.Control.Counter && tdbControl.Type == Constant.Control.AlphaNumeric) || // Count -> AlphaNumeric  

                            (ddbDatabaseControl.Type == Constant.Control.IntegerPositive && tdbControl.Type == Constant.Control.IntegerAny) || // IntPos -> Int        
                            (ddbDatabaseControl.Type == Constant.Control.IntegerPositive && tdbControl.Type == Constant.Control.DecimalPositive) || // IntPos -> DecPos
                            (ddbDatabaseControl.Type == Constant.Control.IntegerPositive && tdbControl.Type == Constant.Control.DecimalAny) || // IntPos -> Dec
                            (ddbDatabaseControl.Type == Constant.Control.IntegerPositive && tdbControl.Type == Constant.Control.Counter) || // IntPos -> Count       
                            (ddbDatabaseControl.Type == Constant.Control.IntegerPositive && tdbControl.Type == Constant.Control.Note) || // IntPos -> Note          
                            (ddbDatabaseControl.Type == Constant.Control.IntegerPositive && tdbControl.Type == Constant.Control.MultiLine) || // IntPos -> MultLine
                            (ddbDatabaseControl.Type == Constant.Control.IntegerPositive &&
                             tdbControl.Type ==
                             Constant.Control
                                 .AlphaNumeric) || // IntPos -> AlphaNumeric                                (ddbDatabaseControl.Type == Constant.Control.IntegerAny && tdbControl.Type == Constant.Control.DecimalAny)   ||        // Int -> Dec

                            (ddbDatabaseControl.Type == Constant.Control.IntegerAny && tdbControl.Type == Constant.Control.DecimalAny) || // IntAny -> DecAny     
                            (ddbDatabaseControl.Type == Constant.Control.IntegerAny && tdbControl.Type == Constant.Control.Note) || // IntAny -> Note          
                            (ddbDatabaseControl.Type == Constant.Control.IntegerAny && tdbControl.Type == Constant.Control.MultiLine) || // IntAny -> MultLine
                            (ddbDatabaseControl.Type == Constant.Control.IntegerAny && tdbControl.Type == Constant.Control.AlphaNumeric) || // IntAny -> AlphaNumeric

                            (ddbDatabaseControl.Type == Constant.Control.DecimalPositive && tdbControl.Type == Constant.Control.DecimalAny) || // DecPos -> DecAny
                            (ddbDatabaseControl.Type == Constant.Control.DecimalPositive && tdbControl.Type == Constant.Control.Note) || // DecPos -> Note          
                            (ddbDatabaseControl.Type == Constant.Control.DecimalPositive && tdbControl.Type == Constant.Control.MultiLine) || // DecPos -> MultLine
                            (ddbDatabaseControl.Type == Constant.Control.DecimalPositive && tdbControl.Type == Constant.Control.AlphaNumeric) || // DecPos -> AlphaNumeric

                            (ddbDatabaseControl.Type == Constant.Control.DecimalAny && tdbControl.Type == Constant.Control.Note) || // DecAny -> Note          
                            (ddbDatabaseControl.Type == Constant.Control.DecimalAny && tdbControl.Type == Constant.Control.MultiLine) || // DecAny -> MultLine
                            (ddbDatabaseControl.Type == Constant.Control.DecimalAny && tdbControl.Type == Constant.Control.AlphaNumeric) // DecAny -> AlphaNumeric
                           )
                        {
                            templateSyncResults.SyncRequiredAsNonCriticalDataFieldAttributesDiffer = true;
                        }
                        else
                        {
                            // Yes. This is an error. Generate an error message, which because its now non-empty, also serves to signal the error.
                            AddStringToDictionaryWithListStringByLevel(templateSyncResults.ControlSynchronizationErrorsByLevel, level,
                                $"  \u2022  The field with DataLabel '{dataLabel}' is of type '{ddbDatabaseControl.Type}' in the data (.ddb) file but of type '{tdbControl.Type}' in the template (.tdb).{Environment.NewLine}");
                        }
                    }

                    // Check: item(s) in the Choice list removed? 
                    List<string> ddbDatabaseChoices = Choices.ChoicesFromJson(ddbDatabaseControl.List).GetAsListWithOptionalEmptyAsNewLine;
                    List<string> tdbChoices = Choices.ChoicesFromJson(tdbControl.List).GetAsListWithOptionalEmptyAsNewLine;
                    List<string> tdbChoiceValuesThatAreAbsent = ddbDatabaseChoices.Except(tdbChoices).ToList();
                    if (tdbChoiceValuesThatAreAbsent.Count > 0)
                    {
                        // Yes. Add a warning that the removed values not being displayable in the Choice control's menu
                        AddStringToDictionaryWithListStringByLevel(templateSyncResults.ControlSynchronizationWarningsByLevel, level,
                            $"  \u2022 Choice:    {dataLabel} no longer includes these list values, so it will not display or allow those values to be entered.");
                        string absentItemsAsString = string.Join<string>(", ", tdbChoiceValuesThatAreAbsent).Replace(Environment.NewLine, "<Empty>");
                        AddStringToDictionaryWithListStringByLevel(templateSyncResults.ControlSynchronizationWarningsByLevel, level,
                            $"      -  {absentItemsAsString}");
                    }

                    // Check: Any other changed values in any of the columns that may affect the UI appearance. 
                    if (ddbDatabaseControl.ControlOrder != tdbControl.ControlOrder ||
                        ddbDatabaseControl.SpreadsheetOrder != tdbControl.SpreadsheetOrder ||
                        ddbDatabaseControl.DefaultValue != tdbControl.DefaultValue ||
                        ddbDatabaseControl.Label != tdbControl.Label ||
                        ddbDatabaseControl.Tooltip != tdbControl.Tooltip ||
                        ddbDatabaseControl.Width != tdbControl.Width ||
                        ddbDatabaseControl.Copyable != tdbControl.Copyable ||
                        ddbDatabaseControl.Visible != tdbControl.Visible ||
                        ddbDatabaseControl.ExportToCSV != tdbControl.ExportToCSV ||
                        tdbChoices.Except(ddbDatabaseChoices).ToList().Count > 0)
                    {
                        // Yes. Signal syncing of the template is required
                        templateSyncResults.SyncRequiredAsNonCriticalDataFieldAttributesDiffer = true;
                    }
                }

                // Synchronization Issues 2: Unresolved warnings due to existence of other new / deleted columns.
                if (templateSyncResults.ControlSynchronizationErrorsByLevel.Count > 0)
                {
                    if (areNewColumnsInTdbTemplate)
                    {
                        string warning = "  \u2022 ";
                        warning += templateSyncResults.DataLabelsInTdbButNotDdbByLevel[level].Count.ToString();
                        warning += (templateSyncResults.DataLabelsInTdbButNotDdbByLevel[level].Count == 1)
                            ? " new control was found in your .tdb template file: "
                            : " new controls were found in your .tdb template file: ";
                        warning +=
                            $"'{string.Join(", ", templateSyncResults.DataLabelsInTdbButNotDdbByLevel[level].Keys)}'";
                        AddStringToDictionaryWithListStringByLevel(templateSyncResults.ControlSynchronizationWarningsByLevel, level,
                            warning);
                    }
                    if (areDeletedColumnsInTdbTemplate)
                    {
                        string warning = "  \u2022 ";
                        warning += templateSyncResults.DataLabelsInDdbButNotTdbByLevel[level].Count.ToString();
                        warning += (templateSyncResults.DataLabelsInDdbButNotTdbByLevel[level].Count == 1)
                            ? " data field in your .ddb data file has no corresponding control in your .tdb template file: "
                            : " data fields in your .ddb data file have no corresponding controls in your .tdb template file: ";
                        warning += $"'{string.Join(", ", templateSyncResults.DataLabelsInDdbButNotTdbByLevel[level].Keys)}'";
                        AddStringToDictionaryWithListStringByLevel(templateSyncResults.ControlSynchronizationWarningsByLevel, level,
                            warning);
                    }
                }
            }).ConfigureAwait(true);
        }
        #endregion

        #region Compare the common Metadata Level controls
        // Compare the levels in common between the .tdb and .ddb tables
        // It does not consider other levels that were either added or deleted
        // Side effects in templateSyncResults:
        // - DataLabelsInTdbButNotDdbByLevel is filled in
        // - DataLabelsInDdbButNotTdbByLevel is filled in
        // - ControlSynchronizationErrorsByLevel - error messages added concerning differences between a datalabel's type
        // - ControlSynchronizationWarningsByLevel - warning messages added concerning deleted items in a tdb data label's choice list
        // - SyncRequiredAsNonCriticalDataFieldAttributesDiffer - flag indicating that a sync is required due to differences between non-critical data field attributes
        private async Task MetadataCompareCommonFolderLevelControlsBetweenTemplates(CommonDatabase tdbDatabase, TemplateSyncResults templateSyncResults)
        {
            await Task.Run(() =>
            {

                // Check #1: Levels differences between .ddb and .tdb:
                //    Adding, deleting or moving rows can lead to a loss of data if those levels and the ones below it contain data
                //    1A. Find the last row in the level sequence where both the ddb and tdb row are at the same level
                //        This should account for deleted rows
                //        If the result is 0 then there are no levels in common.
                if (templateSyncResults.InfoRowsInDdbToRenumber.Count > 0)
                {
                    for (int i = 1; i < this.GetMetadataInfoTableMaxLevel(); i++)
                    {
                        int level = i;
                        if (templateSyncResults.InfoRowsCommon.Any(s => s.Item1.Level == level && s.Item2.Level == level))
                        {
                            // the level is present, so set it to this level
                            templateSyncResults.LastLevelInCommon = i;
                        }
                        else
                        {
                            // When a level isn't in common, we are done
                            break;
                        }
                    }
                }

                // Item1 is tdb row, Item2 is corresponding ddb row
                foreach (Tuple<MetadataInfoRow, MetadataInfoRow> commonRow in templateSyncResults.InfoRowsCommon)
                {
                    int tdbLevel = commonRow.Item1.Level; // Should be from 1, 2, etc. 
                    int ddbLevel = commonRow.Item2.Level; // Should be from 1, 2, etc. 

                    Dictionary<string, string> tdbDataLabels = tdbDatabase.GetTypedDataLabelsExceptIDInSpreadsheetOrderFromMetadataControls(tdbLevel);
                    Dictionary<string, string> ddbDataLabels = this.GetTypedDataLabelsExceptIDInSpreadsheetOrderFromMetadataControls(ddbLevel);
                    templateSyncResults.DataLabelsInTdbButNotDdbByLevel[tdbLevel] = Compare.Dictionary1ExceptDictionary2(tdbDataLabels, ddbDataLabels);
                    templateSyncResults.DataLabelsInDdbButNotTdbByLevel[tdbLevel] = Compare.Dictionary1ExceptDictionary2(ddbDataLabels, tdbDataLabels);


                    // Synchronization Checks #1 between .ddb and .tdb :
                    // A. Mismatch control types => Sync Error. Generate error: unable to do the sync as there is at least one control type mismatch 
                    // B. Choice list item removed => Sync Warning. Generate warning that a choice list with the removed item may result in some previously entered date not being displayable
                    // C. Non-Critical data field attributes differ => No Warning, but flag SyncRequiredAsNonCriticalDataFieldAttributesDiffer
                    foreach (string dataLabel in ddbDataLabels.Keys)
                    {
                        // Check: is the .ddb dataLabel absent from .tdb template? 
                        if (!tdbDataLabels.ContainsKey(dataLabel))
                        {
                            // Yes. This will be dealt with later
                            continue;
                        }

                        // A. Mismatch control types ? => Sync Error.
                        //    We compare each dataLabel's type in the .ddb vs .tdb template to see if they are the same.
                        //    If they are not, then we need to flag that.
                        MetadataControlRow ddbDatabaseControl = this.GetControlFromMetadataControls(dataLabel, ddbLevel);
                        MetadataControlRow tdbControl = tdbDatabase.GetControlFromMetadataControls(dataLabel, tdbLevel);
                        if (ddbDatabaseControl.Type != tdbControl.Type)
                        {
                            // Allow the following changes to data types
                            if ((ddbDatabaseControl.Type == Constant.Control.AlphaNumeric && tdbControl.Type == Constant.Control.Note) ||             // Alpha -> Note
                                 (ddbDatabaseControl.Type == Constant.Control.AlphaNumeric && tdbControl.Type == Constant.Control.MultiLine) ||         // Alpha -> Multiline

                                 (ddbDatabaseControl.Type == Constant.Control.Note && tdbControl.Type == Constant.Control.MultiLine) ||                 // Note -> Multiline

                                 (ddbDatabaseControl.Type == Constant.Control.MultiLine && tdbControl.Type == Constant.Control.Note) ||                 // MultiLine -> Note

                                 (ddbDatabaseControl.Type == Constant.Control.Counter && tdbControl.Type == Constant.Control.IntegerPositive) ||        // Count -> IntPos  
                                 (ddbDatabaseControl.Type == Constant.Control.Counter && tdbControl.Type == Constant.Control.IntegerAny) ||             // Count -> IntAny  
                                 (ddbDatabaseControl.Type == Constant.Control.Counter && tdbControl.Type == Constant.Control.DecimalPositive) ||        // Count -> DecPos  
                                 (ddbDatabaseControl.Type == Constant.Control.Counter && tdbControl.Type == Constant.Control.DecimalAny) ||             // Count -> DecAny  
                                 (ddbDatabaseControl.Type == Constant.Control.Counter && tdbControl.Type == Constant.Control.Note) ||                   // Count -> Note  
                                 (ddbDatabaseControl.Type == Constant.Control.Counter && tdbControl.Type == Constant.Control.MultiLine) ||              // Count -> MultiLine  
                                 (ddbDatabaseControl.Type == Constant.Control.Counter && tdbControl.Type == Constant.Control.AlphaNumeric) ||           // Count -> AlphaNumeric  

                                 (ddbDatabaseControl.Type == Constant.Control.IntegerPositive && tdbControl.Type == Constant.Control.IntegerAny) ||     // IntPos -> Int        
                                 (ddbDatabaseControl.Type == Constant.Control.IntegerPositive && tdbControl.Type == Constant.Control.DecimalPositive) || // IntPos -> DecPos
                                 (ddbDatabaseControl.Type == Constant.Control.IntegerPositive && tdbControl.Type == Constant.Control.DecimalAny) ||     // IntPos -> Dec
                                 (ddbDatabaseControl.Type == Constant.Control.IntegerPositive && tdbControl.Type == Constant.Control.Counter) ||        // IntPos -> Count       
                                 (ddbDatabaseControl.Type == Constant.Control.IntegerPositive && tdbControl.Type == Constant.Control.Note) ||           // IntPos -> Note          
                                 (ddbDatabaseControl.Type == Constant.Control.IntegerPositive && tdbControl.Type == Constant.Control.MultiLine) ||      // IntPos -> MultLine
                                 (ddbDatabaseControl.Type == Constant.Control.IntegerPositive && tdbControl.Type == Constant.Control.AlphaNumeric) ||   // IntPos -> AlphaNumeric                                (ddbDatabaseControl.Type == Constant.Control.IntegerAny && tdbControl.Type == Constant.Control.DecimalAny)   ||        // Int -> Dec

                                 (ddbDatabaseControl.Type == Constant.Control.IntegerAny && tdbControl.Type == Constant.Control.DecimalAny) ||          // IntAny -> DecAny     
                                 (ddbDatabaseControl.Type == Constant.Control.IntegerAny && tdbControl.Type == Constant.Control.Note) ||                // IntAny -> Note          
                                 (ddbDatabaseControl.Type == Constant.Control.IntegerAny && tdbControl.Type == Constant.Control.MultiLine) ||           // IntAny -> MultLine
                                 (ddbDatabaseControl.Type == Constant.Control.IntegerAny && tdbControl.Type == Constant.Control.AlphaNumeric) ||        // IntAny -> AlphaNumeric

                                 (ddbDatabaseControl.Type == Constant.Control.DecimalPositive && tdbControl.Type == Constant.Control.DecimalAny) ||      // DecPos -> DecAny
                                 (ddbDatabaseControl.Type == Constant.Control.DecimalPositive && tdbControl.Type == Constant.Control.Note) ||            // DecPos -> Note          
                                 (ddbDatabaseControl.Type == Constant.Control.DecimalPositive && tdbControl.Type == Constant.Control.MultiLine) ||       // DecPos -> MultLine
                                 (ddbDatabaseControl.Type == Constant.Control.DecimalPositive && tdbControl.Type == Constant.Control.AlphaNumeric) ||    // DecPos -> AlphaNumeric

                                 (ddbDatabaseControl.Type == Constant.Control.DecimalAny && tdbControl.Type == Constant.Control.Note) ||                 // DecAny -> Note          
                                 (ddbDatabaseControl.Type == Constant.Control.DecimalAny && tdbControl.Type == Constant.Control.MultiLine) ||            // DecAny -> MultLine
                                 (ddbDatabaseControl.Type == Constant.Control.DecimalAny && tdbControl.Type == Constant.Control.AlphaNumeric)            // DecAny -> AlphaNumeric
                                )
                            {
                                templateSyncResults.SyncRequiredAsNonCriticalDataFieldAttributesDiffer = true;
                            }
                            else
                            {
                                // Fatal Syncronization Error: We found a mismatched type
                                AddStringToDictionaryWithListStringByLevel(templateSyncResults.ControlSynchronizationErrorsByLevel, tdbLevel,
                                    $"  \u2022 The field with DataLabel '{dataLabel}' is of type '{ddbDatabaseControl.Type}' in the data (.ddb) file but of type '{tdbControl.Type}' in the template (.tdb).{Environment.NewLine}");

                                // Don't bother checking for other possible warnings below, as we only display critical errors when they occur.
                                // While we could include the warnings, as simpler message is perhaps better.
                                continue;
                            }
                        }

                        // B. Choice list item removed? => Sync Warning.
                        //    We compare each dataLabel's choice list in the .ddb vs .tdb to see if the .tdb's list no longer has one or more items

                        List<string> ddbDatabaseChoices = Choices.ChoicesFromJson(ddbDatabaseControl.List).GetAsListWithOptionalEmptyAsNewLine;
                        List<string> tdbChoices = Choices.ChoicesFromJson(tdbControl.List).GetAsListWithOptionalEmptyAsNewLine;
                        List<string> tdbChoiceValuesThatAreAbsent = ddbDatabaseChoices.Except(tdbChoices).ToList();
                        if (tdbChoiceValuesThatAreAbsent.Count > 0)
                        {
                            AddStringToDictionaryWithListStringByLevel(templateSyncResults.ControlSynchronizationWarningsByLevel, tdbLevel,
                                $"  \u2022 Choice:    {dataLabel} no longer includes these list values, so it will not display or allow those values to be entered.");
                            string absentItemsAsString = string.Join<string>(", ", tdbChoiceValuesThatAreAbsent).Replace(Environment.NewLine, "<Empty>");
                            AddStringToDictionaryWithListStringByLevel(templateSyncResults.ControlSynchronizationWarningsByLevel, tdbLevel,
                                $"      {absentItemsAsString}");
                        }

                        // C. Non-Critical data field attributes differ? => No Warning, but flag SyncRequiredAsNonCriticalDataFieldAttributesDiffer
                        // Check if there are any other changed values in any of the columns that may affect the UI appearance. If there are, then we need to signal syncing of the template
                        if (ddbDatabaseControl.ControlOrder != tdbControl.ControlOrder ||
                            ddbDatabaseControl.SpreadsheetOrder != tdbControl.SpreadsheetOrder ||
                            ddbDatabaseControl.DefaultValue != tdbControl.DefaultValue ||
                            ddbDatabaseControl.Label != tdbControl.Label ||
                            ddbDatabaseControl.Tooltip != tdbControl.Tooltip ||
                            ddbDatabaseControl.Visible != tdbControl.Visible ||
                            ddbDatabaseControl.ExportToCSV != tdbControl.ExportToCSV ||
                            tdbChoices.Except(ddbDatabaseChoices).ToList().Count > 0)
                        {
                            templateSyncResults.SyncRequiredAsNonCriticalDataFieldAttributesDiffer = true;
                        }
                    }
                }
            }).ConfigureAwait(true);
        }
        #endregion

        #region Static private utilities
        // Helper method to add a string to a list held by templateSyncResults
        private static void AddStringToDictionaryWithListStringByLevel(Dictionary<int, List<string>> dictionary, int level, string stringToAdd)
        {
            if (false == dictionary.ContainsKey(level))
            {
                // Create the key with an empty list if we don't yet have one
                dictionary.Add(level, new List<string>());
            }
            dictionary[level].Add(stringToAdd);
        }
        #endregion

        #region UNUSED TO DELETE Compare the MetadataInfo Tables
        // TODO NOT USED - DELETE WHEN SURE WE DONT NEED IT

        // Compare the metadata info tables and return the differences, if any
        // Prior code should guarantee that metadata infotables both exist (with or without rows)
        // and that their corresponding metadataInfo structures are available

        //public List<string> MetadataCompareInfoTables(CommonDatabase templateDatabase)
        //{
        //    List<string> problems = new List<string>();
        //    //bool noTdbMetadata =  null == templateDatabase.MetadataInfo || templateDatabase.MetadataInfo.RowCount == 0; 
        //    //bool noDdbMetadata = null == this.MetadataInfo || this.MetadataInfo.RowCount == 0;

        //    //if (noTdbMetadata && noDdbMetadata)
        //    //{
        //    //    // since neither have any metdata, there is nothing to do.
        //    //    return problems;
        //    //}


        //    // shortcut declarations
        //    DataTableBackedList<MetadataInfoRow> ddbInfo = this.MetadataInfo;
        //    DataTableBackedList<MetadataInfoRow> tdbInfo = templateDatabase.MetadataInfo;

        //    // Compare the GUIDs one by one
        //    int ddbCount = ddbInfo.RowCount;
        //    int tdbCount = tdbInfo.RowCount;
        //    if (tdbCount != ddbCount)
        //    {
        //        problems.Add($"MetadataInfo counts differ. tdb:{tdbCount}, ddb:{ddbCount}");
        //        for (int i = 0; i < tdbCount; i++)
        //        {
        //            // levels by GUID in tdb that are not found in ddb
        //            MetadataInfoRow tdbRow = templateDatabase.MetadataInfo[i];
        //            if (false == this.MetadataInfo.Any(s => s.Guid == tdbRow.Guid))
        //            {
        //                problems.Add($"TDB Level {tdbRow.Level} not found in DDB ");
        //            }
        //        }

        //        for (int i = 0; i < ddbCount; i++)
        //        {
        //            // levels by GUID in ddb that are not found in ddb
        //            MetadataInfoRow ddbRow = this.MetadataInfo[i];
        //            if (false == templateDatabase.MetadataInfo.Any(s => s.Guid == ddbRow.Guid))
        //            {
        //                problems.Add($"DDB Level {ddbRow.Level} not found in TDB ");
        //            }
        //        }
        //    }
        //    else
        //    {
        //        // Same number of levels. Compare them.
        //        problems.Add("MetadataInfo counts are the same. Looking for differences");
        //        // Compare GUIDs one by one if they are the same
        //        for (int i = 0; i < tdbCount; i++)
        //        {
        //            MetadataInfoRow ddbRow = this.MetadataInfo[i];
        //            MetadataInfoRow tdbRow = templateDatabase.MetadataInfo[i];
        //            if (false == FileDatabase.CompareInfoRow(ddbRow, tdbRow, out Tuple<bool, bool, bool, bool, bool> rowComparison))
        //            {
        //                // the levels differ. Indicate where the difference is.
        //                string messasge = $"tdb Level {tdbRow.Level} differs from ddb level1:"
        //                                  + (rowComparison.Item2 ? "" : $" Level number ({tdbRow.Level}, {ddbRow.Level})")
        //                                  + (rowComparison.Item3 ? "" : $" Guid ({tdbRow.Guid}, {ddbRow.Guid})")
        //                                  + (rowComparison.Item4 ? "" : $" Alias ({tdbRow.Alias}, {ddbRow.Alias})")
        //                                  + (rowComparison.Item5 ? "" : $" Ignore ({tdbRow.Ignore}, {ddbRow.Ignore})");
        //                problems.Add(messasge);
        //            }
        //        }
        //    }

        //    return problems;
        //}
        #endregion
    }
}
