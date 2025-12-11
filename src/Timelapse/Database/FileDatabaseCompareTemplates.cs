using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Timelapse.Constant;
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
            DataTableBackedList<MetadataInfoRow> ddbInfo = MetadataInfo;
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
                    templateSyncResults.InfoRowsCommon.Add(new(tdbRow, ddbRow));

                    // Check: Different levels?
                    if (tdbRow.Level != ddbRow.Level)
                    {
                        // Yes. add this common pair to the Renumber list as their level number differs
                        templateSyncResults.InfoRowsInDdbToRenumber.Add(new(ddbRow, ddbRow.Level, tdbRow.Level));
                        templateSyncResults.SyncRequiredAsFolderLevelsDiffer = true;
                    }

                    // Check: Unequal aliases?
                    if (tdbRow.Alias != ddbRow.Alias)
                    {
                        // Yes. Add this common pair to the NameChanges list as their (non-empty which means user-defined) alias has changed
                        templateSyncResults.InfoRowsWithNameChanges.Add(new(tdbRow, ddbRow));
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
                            templateSyncResults.InfoRowsWithDifferentGuidSameAlias.Add(new(tdbRow, ddbRow));
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
                templateSyncResults.InfoRowsWithDifferentGuidSameAlias.Add(new(tdbRow, ddbRow));
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
                    // No. Changes are incompatible as hierarchical differences will lead to a loss of data for subsequent levels 
                    // TODO: We really should check if deleted levels occur at the end, where there is no data associated with it and allow that, or generate warnings and delete the data.
                    templateSyncResults.InfoHierarchyIncompatibleDifferences = true;
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
                Dictionary<string, string> ddbDataLabels = GetTypedDataLabelsExceptIDInSpreadsheetOrderFromControls();

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
                    ControlRow ddbControl = GetControlFromControls(dataLabel);
                    ControlRow tdbControl = tdbDatabase.GetControlFromControls(dataLabel);
                    if (ddbControl.Type != tdbControl.Type)
                    {
                        // Allow the following changes to data types
                        if (IsTypeConversionCompatible(ddbControl.Type, tdbControl.Type))
                        {
                            templateSyncResults.SyncRequiredAsNonCriticalDataFieldAttributesDiffer = true;
                        }
                        else
                        {
                            // Yes. This is an error. Generate an error message, which because its now non-empty, also serves to signal the error.
                            string msg = GenerateErrorMessageForIncompatibleTypes(dataLabel, ddbControl.Type, tdbControl.Type);
                            AddStringToDictionaryWithListStringByLevel(templateSyncResults.ControlSynchronizationErrorsByLevel, level, msg);
                        }
                    }

                    // Check: item(s) in the Choice list are present in the tdb but not in the ddb (which means those choices can't be displayed or entered)? 
                    bool choiceListsDiffer = false;
                    if (ddbControl.Type == Constant.Control.FixedChoice || ddbControl.Type == Constant.Control.MultiChoice)
                    {
                        List<string> ddbDatabaseChoices = Choices.ChoicesFromJson(ddbControl.List).GetAsListWithOptionalEmptyAsNewLine;
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
                            choiceListsDiffer = tdbChoices.Except(ddbDatabaseChoices).ToList().Count > 0;
                        }
                    }
                    // Check: Any other changed values in any of the columns that may affect the UI appearance. 
                    if (ddbControl.ControlOrder != tdbControl.ControlOrder ||
                        ddbControl.SpreadsheetOrder != tdbControl.SpreadsheetOrder ||
                        ddbControl.DefaultValue != tdbControl.DefaultValue ||
                        ddbControl.Label != tdbControl.Label ||
                        ddbControl.Tooltip != tdbControl.Tooltip ||
                        ddbControl.Width != tdbControl.Width ||
                        ddbControl.Copyable != tdbControl.Copyable ||
                        ddbControl.Visible != tdbControl.Visible ||
                        ddbControl.ExportToCSV != tdbControl.ExportToCSV ||
                        choiceListsDiffer)
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
                    for (int i = 1; i < GetMetadataInfoTableMaxLevel(); i++)
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
                    Dictionary<string, string> ddbDataLabels = GetTypedDataLabelsExceptIDInSpreadsheetOrderFromMetadataControls(ddbLevel);
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
                        MetadataControlRow ddbControl = GetControlFromMetadataControls(dataLabel, ddbLevel);
                        MetadataControlRow tdbControl = tdbDatabase.GetControlFromMetadataControls(dataLabel, tdbLevel);
                        if (ddbControl.Type != tdbControl.Type)
                        {
                            if (IsTypeConversionCompatible(ddbControl.Type, tdbControl.Type))
                            {
                                templateSyncResults.SyncRequiredAsNonCriticalDataFieldAttributesDiffer = true;
                            }
                            else
                            {
                                // Incompatible Syncronization Error: We found an Incompatible type switch
                                string msg = GenerateErrorMessageForIncompatibleTypes(dataLabel, ddbControl.Type, tdbControl.Type);
                                AddStringToDictionaryWithListStringByLevel(templateSyncResults.ControlSynchronizationErrorsByLevel, tdbLevel, msg);

                                // Don't bother checking for other possible warnings below, as we only display critical errors when they occur.
                                // While we could include the warnings, as simpler message is perhaps better.
                                continue;
                            }
                        }

                        // B. Choice list item removed? => Sync Warning.
                        //    We compare each dataLabel's choice list in the .ddb vs .tdb to see if the .tdb's list no longer has one or more items

                        List<string> ddbDatabaseChoices = Choices.ChoicesFromJson(ddbControl.List).GetAsListWithOptionalEmptyAsNewLine;
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
                        if (ddbControl.ControlOrder != tdbControl.ControlOrder ||
                            ddbControl.SpreadsheetOrder != tdbControl.SpreadsheetOrder ||
                            ddbControl.DefaultValue != tdbControl.DefaultValue ||
                            ddbControl.Label != tdbControl.Label ||
                            ddbControl.Tooltip != tdbControl.Tooltip ||
                            ddbControl.Visible != tdbControl.Visible ||
                            ddbControl.ExportToCSV != tdbControl.ExportToCSV ||
                            tdbChoices.Except(ddbDatabaseChoices).ToList().Count > 0)
                        {
                            templateSyncResults.SyncRequiredAsNonCriticalDataFieldAttributesDiffer = true;
                        }
                    }
                }
            }).ConfigureAwait(true);
        }
        #endregion

        #region Static private utilities for above
        // Helper method to add a string to a list held by templateSyncResults
        private static void AddStringToDictionaryWithListStringByLevel(Dictionary<int, List<string>> dictionary, int level, string stringToAdd)
        {
            if (false == dictionary.ContainsKey(level))
            {
                // Create the key with an empty list if we don't yet have one
                dictionary.Add(level, []);
            }
            dictionary[level].Add(stringToAdd);
        }

        // Return true iff  a type conversion between a tdbControl's type vs the existing ddb Control type is compatable
        private static bool IsTypeConversionCompatible(string ddbControlType, string tdbControlType)
        {
            return
                   (ddbControlType == Control.Flag && tdbControlType == Control.Note) || // Flag -> Note
                   (ddbControlType == Control.Flag && tdbControlType == Control.MultiLine) || // Flag -> Multiline
                   (ddbControlType == Control.Flag && tdbControlType == Control.AlphaNumeric) || // Flag -> Alpha

                   (ddbControlType == Control.AlphaNumeric && tdbControlType == Control.Note) || // Alpha -> Note
                   (ddbControlType == Control.AlphaNumeric && tdbControlType == Control.MultiLine) || // Alpha -> Multiline

                   (ddbControlType == Control.Note && tdbControlType == Control.MultiLine) || // Note -> Multiline

                   (ddbControlType == Control.MultiLine && tdbControlType == Control.Note) || // MultiLine -> Note

                   (ddbControlType == Control.FixedChoice && tdbControlType == Control.Note) || // FixedChoice -> Note
                   (ddbControlType == Control.FixedChoice && tdbControlType == Control.MultiLine) || // FixedChoice -> Multiline
                   (ddbControlType == Control.FixedChoice && tdbControlType == Control.MultiChoice) || // FixedChoice -> MultiChoice

                   (ddbControlType == Control.MultiChoice && tdbControlType == Control.Note) || // MultiChoice -> Note
                   (ddbControlType == Control.MultiChoice && tdbControlType == Control.MultiLine) || // MultiChoice -> Multiline

                   (ddbControlType == Control.Counter && tdbControlType == Control.IntegerPositive) || // Count -> IntPos  
                   (ddbControlType == Control.Counter && tdbControlType == Control.IntegerAny) || // Count -> IntAny  
                   (ddbControlType == Control.Counter && tdbControlType == Control.DecimalPositive) || // Count -> DecPos  
                   (ddbControlType == Control.Counter && tdbControlType == Control.DecimalAny) || // Count -> DecAny  
                   (ddbControlType == Control.Counter && tdbControlType == Control.Note) || // Count -> Note  
                   (ddbControlType == Control.Counter && tdbControlType == Control.MultiLine) || // Count -> MultiLine  
                   (ddbControlType == Control.Counter && tdbControlType == Control.AlphaNumeric) || // Count -> AlphaNumeric  

                   (ddbControlType == Control.IntegerPositive && tdbControlType == Control.IntegerAny) || // IntPos -> Int        
                   (ddbControlType == Control.IntegerPositive && tdbControlType == Control.DecimalPositive) || // IntPos -> DecPos
                   (ddbControlType == Control.IntegerPositive && tdbControlType == Control.DecimalAny) || // IntPos -> Dec
                   (ddbControlType == Control.IntegerPositive && tdbControlType == Control.Counter) || // IntPos -> Count       
                   (ddbControlType == Control.IntegerPositive && tdbControlType == Control.Note) || // IntPos -> Note          
                   (ddbControlType == Control.IntegerPositive && tdbControlType == Control.MultiLine) || // IntPos -> MultLine
                   (ddbControlType == Control.IntegerPositive && tdbControlType == Control.AlphaNumeric) || // IntPos -> AlphaNumeric

                   (ddbControlType == Control.IntegerAny && tdbControlType == Control.DecimalAny) || // IntAny -> DecAny     
                   (ddbControlType == Control.IntegerAny && tdbControlType == Control.Note) || // IntAny -> Note          
                   (ddbControlType == Control.IntegerAny && tdbControlType == Control.MultiLine) || // IntAny -> MultLine
                   (ddbControlType == Control.IntegerAny && tdbControlType == Control.AlphaNumeric) || // IntAny -> AlphaNumeric

                   (ddbControlType == Control.DecimalPositive && tdbControlType == Control.DecimalAny) || // DecPos -> DecAny
                   (ddbControlType == Control.DecimalPositive && tdbControlType == Control.Note) || // DecPos -> Note          
                   (ddbControlType == Control.DecimalPositive && tdbControlType == Control.MultiLine) || // DecPos -> MultLine
                   (ddbControlType == Control.DecimalPositive && tdbControlType == Control.AlphaNumeric) || // DecPos -> AlphaNumeric

                   (ddbControlType == Control.DecimalAny && tdbControlType == Control.Note) || // DecAny -> Note          
                   (ddbControlType == Control.DecimalAny && tdbControlType == Control.MultiLine) || // DecAny -> MultLine
                   (ddbControlType == Control.DecimalAny && tdbControlType == Control.AlphaNumeric); // DecAny -> AlphaNumeric

        }

        // Given Incompatable type changes, where a tdb Control wants to change a ddb control to a type that is incompatible with its data,
        // compose and return an error message that explains the incompatibility.
        private static string GenerateErrorMessageForIncompatibleTypes(string dataLabel, string ddbControlType, string tdbControlType)
        {
            // Yes. This is an error. Generate an error message, which because its now non-empty, also serves to signal the error.
            string msg = $"  \u2022 Your template wants to redefine your '{dataLabel}' data from {ddbControlType}\u21D2{tdbControlType}.{Environment.NewLine}";

            // various controls x-> Alpha
            if (ddbControlType != Control.AlphaNumeric && ddbControlType != Control.Flag && !IsCondition.IsNumberType(ddbControlType)
                && tdbControlType == Control.AlphaNumeric)
            {
                msg += $"     Problem: {tdbControlType} only allows <A:z, 0-9, -, _>, while your existing {ddbControlType} data can contain other characters.";
            }

            // Any non - number control x-> number
            else if (false == IsCondition.IsNumberType(ddbControlType) && IsCondition.IsNumberType(tdbControlType))
            {
                msg += $"     Problem: {tdbControlType} only allows numbers, while your existing {ddbControlType} data can contain non-numbers.";
            }

            // Any number any control x-> positive number
            else if ((ddbControlType == Control.DecimalAny || ddbControlType == Control.IntegerAny)
                     &&
                     (tdbControlType == Control.DecimalPositive || tdbControlType == Control.IntegerPositive || tdbControlType == Control.Counter))            // Any non-numer control -> number
            {
                msg += $"     Problem: {tdbControlType} only allows positive numbers, while your existing {ddbControlType} data can contain negative numbers.";
            }

            // Any non - Choice control x-> choiceControl
            else if (false == IsCondition.IsChoicesType(ddbControlType) && IsCondition.IsChoicesType(tdbControlType))
            {
                msg += $"     Problem: {tdbControlType} only allows text that matches menu selections, while your existing {ddbControlType} data can contain arbitrary text.";
            }

            // MultiChoice x-> FixedChoice
            else if (ddbControlType == Control.MultiChoice &&
                    (tdbControlType == Control.FixedChoice || tdbControlType == Control.IntegerPositive || tdbControlType == Control.Counter))            // Any non-numer control -> number
            {
                msg += $"     Problem: {tdbControlType} allows only a single selection, while your existing {ddbControlType} data can comprise multiple selections.";
            }
            // Any non - Date control x-> DateControl
            else if (false == IsCondition.IsDateTimeType(ddbControlType) && IsCondition.IsDateTimeType(tdbControlType))
            {
                msg += $"     Problem: {tdbControlType} only allows date-related data, while your existing {ddbControlType} data can contain non-date text.";
            }

            // Any DateTime control to a different DateTime Control
            // Note this needs to be after the above if statement to work
            else if (IsCondition.IsDateTimeType(tdbControlType))
            {
                msg += $"     Problem: {tdbControlType} formats date-related data differently from your existing {ddbControlType} data.";
            }

            // Any control -> flag
            else if (tdbControlType == Control.Flag)
            {
                msg += $"     Problem: {tdbControlType} only allows true/false values, while your existing {ddbControlType} data can contain non-true/false text.";
            }

            // To catch things I may have missed above
            else
            {
                msg += $"     Problem: {tdbControlType} only allows values that match its type, while your existing {ddbControlType} data can contain other values.";
            }

            return msg;
        }
    }
    #endregion
}
