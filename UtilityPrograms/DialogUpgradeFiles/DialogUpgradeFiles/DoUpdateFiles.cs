using DialogUpgradeFiles.Database;
using DialogUpgradeFiles.DataStructures;
using DialogUpgradeFiles.Enums;
using DialogUpgradeFiles.QuickPaste;
using DialogUpgradeFiles.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DialogUpgradeFiles.Constant;
using File = System.IO.File;

namespace DialogUpgradeFiles
{
    public partial class DialogUpgradeFilesAndFolders
    {
        #region Tdb Template: Upgrade it
        // Load the specified database template and then the associated images. 
        // templateDatabasePath is the Fully qualified path to the template database file.
        // Returns true only if both the template and image database file are loaded (regardless of whether any images were loaded) , false otherwise
        private async Task<UpgradeResultsEnum> UDBUpgradeTemplatesInDatabaseFilesAsync(string originalDatabasePath, bool removeImageQualityColumn, string timelapseVersion)
        {
            // Check that the file exists and is a data or template file
            DebugFeedback("Checking: " + originalDatabasePath);
            DatabaseTypeEnum fileType = UDBCheckFilePath(originalDatabasePath);
            switch (fileType)
            {
                case DatabaseTypeEnum.DoesNotExist:
                    return UpgradeResultsEnum.FileNotFound;
                case DatabaseTypeEnum.InvalidExtension:
                    return UpgradeResultsEnum.InvalidFile;
            }

            // We now do the copy elsewhere
            string newdatabasePath = originalDatabasePath;
            // Try to open the database.
            // Importantly, this also repairs various aspects of the Template Table in both the .ddb and .tdb files
            // Side effects of TryCreateOrOpenAsync of a template:
            // -- PragmaQuickCheck to see if its a valid database (but could be empty)
            // -- loads the controls
            // -- checks and repairs any empty labels or data labels in the template table(s)
            // -- ensures UtcOffset is not visible (this doesn't really matter, as we will remove that anyways)
            // -- ensures DateTime default is in the new format
            // -- various backwards compatability checks, including
            // ---- add a RelativePath control to pre v2.1 databases if one hasn't already been inserted
            // ---- add DateTime and UtcOffset controls to pre v2.1.0.5 databases if they haven't already been inserted
            // ---- check to ensure that the image quality choice list in the template matches the expected default value
            // ---- ensure a DeleteFlag control exists, replacing the MarkForDeletion data label used in pre 2.1.0.4 templates if necessary
            // As we can't have out parameters in an async method, we return the state and the desired templateDatabase as a tuple
            DebugFeedback("Opening: " + newdatabasePath);
            Tuple<bool, TemplateDatabase> tupleResult = await TemplateDatabase.TryCreateOrOpenAsync(newdatabasePath).ConfigureAwait(true);
            this.templateDatabase = tupleResult.Item2;
            if (!tupleResult.Item1)
            {
                // The template couldn't be loaded
                DebugFeedback(false, "Could not load database: " + newdatabasePath);
                return UpgradeResultsEnum.InvalidFile;
            }
            // Get the controls from the template table
            this.templateDatabase.GetControlsSortedByControlOrder();

            // Check if its an upgraded template that was opened with a pre2.3 version of Timelapse, which would re-insert the UTCOffset type...
            // If so, this would have to be fixed. So create a flag for this. We do this by testing if there is no Folder or ImageQuality field (for redundancy) as
            // those were deleted in 2.3 onwards, but that still includes a UtcOffset field
            bool existsUTCInUpdatedTemplate = null == this.templateDatabase.GetControlFromTemplateTable(DatabaseColumn.Folder)
                                              && null == this.templateDatabase.GetControlFromTemplateTable(DatabaseColumn.ImageQuality)
                                              && null != this.templateDatabase.GetControlFromTemplateTable(DatabaseColumn.UtcOffset);

            // Do this as part of normal upgrade (i.e., without the special  UtcOffset case)
            if (false == existsUTCInUpdatedTemplate)
            {
                // Modify these rows: Image Quality
                DebugFeedback("Modifying various template rows in: " + newdatabasePath);
                UDBTemplateModifyRows(this.templateDatabase, removeImageQualityColumn);


                // Modify these rows: DefaultValue if needed
                UDBTemplateEnsureDefaultsForChoices(this.templateDatabase);
                // Delete these rows: Image Quality
                DebugFeedback("Deleting unneeded rows from: " + newdatabasePath);
                UDBTemplateDeleteRows(this.templateDatabase, removeImageQualityColumn);
            }
            else
            {
                DebugFeedback("Deleting UtcOffsetRows from: " + newdatabasePath);
                UDBTemplateDeleteUtcOffsetRow(this.templateDatabase);
            }

            DatabaseTypeEnum dbType = Path.GetExtension(newdatabasePath) == ".tdb"
                ? DatabaseTypeEnum.Template
                : DatabaseTypeEnum.Data;

            // Add a new table with the timelapse version if its the .tdb file
            if (dbType == DatabaseTypeEnum.Template)
            {
                if (false == existsUTCInUpdatedTemplate)
                {
                    UDBTemplateAddTimelapseTemplateInfoTable(this.templateDatabase, timelapseVersion);
                }
            }

            if (dbType == DatabaseTypeEnum.Template)
            {
                DebugFeedback("TemplateFile Upgrading complete: " + newdatabasePath);
                return UpgradeResultsEnum.Upgraded;
            }
            DebugFeedback("Template table in DDB Upgraded: " + newdatabasePath);

            // Upgrade the DataTable
            return await UDBOpenDatabaseAsync(this, newdatabasePath, this.IsDeleteImageQualityRequested, this.TimelapseVersion);
        }
        #endregion

        #region File Helper: Check that the file exists and is a data or template file
        public static DatabaseTypeEnum UDBCheckFilePath(string filePath)
        {
            if (IsCondition.IsPathLengthTooLong(filePath, FilePathTypeEnum.DDB) || File.Exists(filePath) == false)
            {
                return DatabaseTypeEnum.DoesNotExist;
            }
            string extension = Path.GetExtension(filePath);
            if (extension == ".tdb")
            {
                return DatabaseTypeEnum.Template;
            }
            if (extension == ".ddb")
            {
                return DatabaseTypeEnum.Data;
            }
            return DatabaseTypeEnum.InvalidExtension;
        }
        #endregion

        #region Template helper: Modify ImageQuality and convert Items in Choices as a JSON
        public static void UDBTemplateModifyRows(TemplateDatabase templateDB, bool removeImageQualityColumn)
        {
            templateDB.GetControlsSortedByControlOrder();
            foreach (ControlRow control in templateDB.Controls)
            {
                if (control.Type == Constant.DatabaseColumn.ImageQuality && false == removeImageQualityColumn)
                {
                    // ImageQuality - change to a Dark? Flag
                    control.Label = "Dark?";
                    control.DataLabel = "Dark";
                    control.Type = Constant.Control.Flag;
                    control.DefaultValue = Constant.BooleanValue.False;
                    control.Tooltip = "True if the image is dark, usually populated by a Timelapse option in the Edit menu";
                    control.Visible = true;
                    control.Width = 20;
                    control.SetChoices(new List<string>());
                    templateDB.SyncControlToDatabase(control);
                }
                else if (control.Type == Constant.Control.FixedChoice)
                {
                    Choices choices = new Choices
                    {
                        ChoiceList = control.GetChoices(out bool includesEmptyChoice),
                        IncludeEmptyChoice = includesEmptyChoice
                    };
                    control.SetChoicesAsJson(choices.GetAsJson);

                    // We need to check the default value to make sure it matches what is allowed
                    if (false == includesEmptyChoice)
                    {
                        if (string.IsNullOrEmpty(control.DefaultValue))
                        {
                            // when we don't allow an empty choice, we can't have an empty default
                            // So allow empty defaults in the choice list
                            choices.IncludeEmptyChoice = true;
                            control.SetChoicesAsJson(choices.GetAsJson);
                        }
                        else if (false == choices.Contains(control.DefaultValue))
                        {
                            // we also can't have a non-matching default value
                            // Easiest thing to do is to clear the default and leave the IncludeEmptyChoice alone
                            control.DefaultValue = String.Empty;
                        }
                    }
                    else if (false == choices.Contains(control.DefaultValue))
                    {
                        // We allow an empty choice, but we have a non-matching default value
                        // As above, easiest thing to do is to clear the default
                        control.DefaultValue = String.Empty;
                    }
                    templateDB.SyncControlToDatabase(control);
                }
            }
        }
        #endregion

        #region UDBTemplateEnsureDefaultsForChoices
        private void UDBTemplateEnsureDefaultsForChoices(TemplateDatabase templateDB)
        {
            templateDB.GetControlsSortedByControlOrder();
            try
            {
                foreach (ControlRow control in templateDB.Controls)
                {
                    string defaultToUse = String.Empty;
                    if (control.Type == Constant.Control.FixedChoice || control.Type == Constant.Control.Choice)
                    {
                        // We are only interested in choice controls
                        List<string> choices = control.GetChoices(out bool includesEmptyChoice);
                        if (false == includesEmptyChoice && (string.IsNullOrEmpty(control.DefaultValue) || false == choices.Contains(control.DefaultValue)))
                        {
                            // when we don't allow an empty choice, Cant have an empty default or a non-matching default value
                            if (choices.Count > 0)
                            {
                                defaultToUse = choices[0];
                            }
                            // undefined if  choice list is empty!
                        }
                        else if (includesEmptyChoice && (false == string.IsNullOrEmpty(control.DefaultValue) || false == choices.Contains(control.DefaultValue)))
                        {
                            // when we allow an empty choice, we can only have an empty default or a non-matching default value
                            defaultToUse = String.Empty;
                        }
                    }
                }
            }
            catch
            {
            }
        }
        #endregion

        #region Template helper: Add the TemplateInfoTable
        public static void UDBTemplateAddTimelapseTemplateInfoTable(TemplateDatabase templateDB, string timelapseVersion)
        {
            // Create the table
            SchemaColumnDefinition schemaColumnDefinition = new SchemaColumnDefinition(Constant.DatabaseColumn.VersionCompatabily, Sql.Text, Constant.DatabaseValues.VersionNumberMinimum);
            List<SchemaColumnDefinition> schemaColumnDefinitions = new List<SchemaColumnDefinition>
            {
                schemaColumnDefinition
            };
            templateDB.Database.CreateTable(Constant.DBTables.TemplateInfo, schemaColumnDefinitions);

            // Insert values
            ColumnTuple columnTuple = new ColumnTuple(Constant.DatabaseColumn.VersionCompatabily, timelapseVersion);
            List<ColumnTuple> columnTuples = new List<ColumnTuple>
            {
                columnTuple
            };
            List<List<ColumnTuple>> insertionStatements = new List<List<ColumnTuple>>
            {
                columnTuples
            };
            templateDB.Database.Insert(Constant.DBTables.TemplateInfo, insertionStatements);
        }
        #endregion

        #region Template helper: Delete particular rows: Date, Time, UtcOffset, ImageQuality, Fodler
        public static void UDBTemplateDeleteRows(TemplateDatabase templateDB, bool removeImageQualityColumn)
        {
            List<string> DataLabelsToRemove = new List<string>
            {
                Constant.DatabaseColumn.Date,
                Constant.DatabaseColumn.Time,
                Constant.DatabaseColumn.UtcOffset,
                Constant.DatabaseColumn.ImageQuality,
                Constant.DatabaseColumn.Folder
            };

            foreach (string dataLabelToRemove in DataLabelsToRemove)
            {
                if (dataLabelToRemove == Constant.DatabaseColumn.ImageQuality && false == removeImageQualityColumn)
                {
                    continue;
                }
                templateDB.GetControlsSortedByControlOrder();
                if (templateDB.Controls.Any(x => x.DataLabel == dataLabelToRemove))
                {
                    ControlRow control = templateDB.Controls.First(x => x.DataLabel == dataLabelToRemove);
                    templateDB.UpgradeTemplateRemoveControl(control);
                }
            }
        }

        public static void UDBTemplateDeleteUtcOffsetRow(TemplateDatabase templateDB)
        {
            templateDB.GetControlsSortedByControlOrder();
            if (templateDB.Controls.Any(x => x.DataLabel == Constant.DatabaseColumn.UtcOffset))
            {
                ControlRow control = templateDB.Controls.First(x => x.DataLabel == Constant.DatabaseColumn.UtcOffset);
                if (null != control)
                {
                    templateDB.UpgradeTemplateRemoveControl(control);
                }
            }
        }
        #endregion

        #region Ddb database: Upgrade it
        public static async Task<UpgradeResultsEnum> UDBOpenDatabaseAsync(DialogUpgradeFilesAndFolders timelapse, string filePath, bool removeImageQualityColumn, string timelapseVersion)
        {
            bool success;
            // This statement does a bunch of stuff that needs to be trimmed out (I think)
            // -- remove backup file stuff
            // -- remove (but check for side effects) DateTimeKind = DateTimeKind.Utc but see https://thomaslevesque.com/2015/06/28/how-to-retrieve-dates-as-utc-in-sqlite/
            FileDatabase fileDatabase = new FileDatabase(filePath);

            // Upgrade the database to pre-2.3.0.0 version, to ensure we are starting with a level playing field

            // Feedback in UI lop
            await Task.Delay(Constant.BusyState.SleepTime);
            if (timelapse.CancelUpgrade)
            {
                return UpgradeResultsEnum.Cancelled;
            }

            fileDatabase.GetControlsSortedByControlOrder();

            // Special case: This is an upgraded file, but as it was opened with a pre 2.3 version.
            // Because of that, the upgraded file now contains these columns that has to be removed:
            // UtcOffset
            // WhiteSpaceTrimmed
            // QuickPasteXML
            // TimeZone
            if (fileDatabase.Database.SchemaIsColumnInTable(Constant.DBTables.FileData, Constant.DatabaseColumn.UtcOffset)
                && fileDatabase.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.RootFolder))
            {
                // Special case: This is an upgraded file, but as it was opened with a pre 2.3 version it now contains a  UtcOffset column that has to be removed
                success = fileDatabase.Database.SchemaDeleteColumn(Constant.DBTables.FileData, Constant.DatabaseColumn.UtcOffset);

                // Recreate the indexes
                fileDatabase.IndexDropForFileAndRelativePathIfExists();
                fileDatabase.IndexCreateForFileAndRelativePathIfNotExists();
                timelapse.DebugFeedback(success, "Data Table: UtcOffset schema and data deleted and indexes recreated: " + filePath);
                if (!success)
                {
                    return UpgradeResultsEnum.Failed;
                }
                // Remove Timezone from ImageSetTable
                success = fileDatabase.Database.SchemaDeleteColumn(Constant.DBTables.ImageSet, Constant.DatabaseColumn.TimeZone);
                timelapse.DebugFeedback(success, "Data Table: Timezone schema and data deleted: " + filePath);
                if (!success)
                {
                    timelapse.DebugFeedback(success, "Data Table: Timezone schema and data not in table: " + filePath);
                }
                else
                {
                    timelapse.DebugFeedback(success, "Data Table: Timezone schema and data deleted " + filePath);
                }
                await Task.Delay(Constant.BusyState.SleepTime);

                // Remove WhiteSpaceTrimmed from ImageSetTable
                success = fileDatabase.Database.SchemaDeleteColumn(Constant.DBTables.ImageSet, Constant.DatabaseColumn.WhiteSpaceTrimmed);
                if (!success)
                {
                    timelapse.DebugFeedback(success, "Data Table: WhiteSpaceTrimmed schema and data not in table: " + filePath);
                }
                else
                {
                    timelapse.DebugFeedback(success, "Data Table: WhiteSpaceTrimmed schema and data deleted " + filePath);
                }
                await Task.Delay(Constant.BusyState.SleepTime);

                // Remove QuickPasteXML from ImageSetTable
                success = fileDatabase.Database.SchemaDeleteColumn(Constant.DBTables.ImageSet, Constant.DatabaseColumn.QuickPasteXML);
                if (!success)
                {
                    timelapse.DebugFeedback(success, "Data Table: QuickPasteXML schema and data not in table: " + filePath);
                }
                else
                {
                    timelapse.DebugFeedback(success, "Data Table: QuickPasteXML schema and data deleted " + filePath);
                }
                await Task.Delay(Constant.BusyState.SleepTime);
                return UpgradeResultsEnum.Upgraded;
            }

            // General upgrade case
            // We first upgrade the file to the pre2.3 standard.
            await fileDatabase.UDBUpgradeDatabasesForBackwardsCompatabilityAsync(timelapseVersion);

            // Because the control names may have changed with the above
            fileDatabase.GetControlsSortedByControlOrder();
            await Task.Delay(Constant.BusyState.SleepTime);

            // Delete the following columns from the DataTable and its schema
            // Note: We can probably do this in a single operation rather than three ...

            // Remove Date from DataTable
            if (false == fileDatabase.Database.SchemaIsColumnInTable(Constant.DBTables.FileData, Constant.DatabaseColumn.Date))
            {
                timelapse.DebugFeedback("Data Table: No Date column to delete: " + filePath); ;
            }
            else
            {
                success = fileDatabase.Database.SchemaDeleteColumn(Constant.DBTables.FileData, Constant.DatabaseColumn.Date);
                timelapse.DebugFeedback(success, "Data Table: Date schema and data deleted: " + filePath);
                if (!success)
                {
                    return UpgradeResultsEnum.Failed;
                }
            }
            await Task.Delay(Constant.BusyState.SleepTime);

            // Remove Time from DataTable
            if (false == fileDatabase.Database.SchemaIsColumnInTable(Constant.DBTables.FileData, Constant.DatabaseColumn.Time))
            {
                timelapse.DebugFeedback("Data Table: No Time column to delete: " + filePath); ;
            }
            else
            {
                success = fileDatabase.Database.SchemaDeleteColumn(Constant.DBTables.FileData,
                    Constant.DatabaseColumn.Time);
                timelapse.DebugFeedback(success, " Data Table: Time schema and data deleted: " + filePath);
                if (!success)
                {
                    return UpgradeResultsEnum.Failed;
                }
            }

            await Task.Delay(Constant.BusyState.SleepTime);

            // Remove UtcOffset from DataTable
            if (false == fileDatabase.Database.SchemaIsColumnInTable(Constant.DBTables.FileData, Constant.DatabaseColumn.UtcOffset))
            {
                timelapse.DebugFeedback("Data Table: No UtcOffset column to delete: " + filePath); ;
            }
            else
            {
                success = fileDatabase.Database.SchemaDeleteColumn(Constant.DBTables.FileData, Constant.DatabaseColumn.UtcOffset);
                timelapse.DebugFeedback(success, "Data Table: UtcOffset schema and data deleted: " + filePath);
                if (!success)
                {
                    return UpgradeResultsEnum.Failed;
                }
            }

            await Task.Delay(Constant.BusyState.SleepTime);

            // Remove Folder from DataTable
            if (false == fileDatabase.Database.SchemaIsColumnInTable(Constant.DBTables.FileData, Constant.DatabaseColumn.Folder))
            {
                timelapse.DebugFeedback("Data Table: No Folder column to delete: " + filePath); ;
            }
            else
            {
                success = fileDatabase.Database.SchemaDeleteColumn(Constant.DBTables.FileData,
                    Constant.DatabaseColumn.Folder);
                timelapse.DebugFeedback(success,
                    "Data Table: Folder schema and data deleted from DataTable: " + filePath);
                if (!success)
                {
                    return UpgradeResultsEnum.Failed;
                }
            }

            await Task.Delay(Constant.BusyState.SleepTime);

            // Check if a cancel has occured
            if (timelapse.CancelUpgrade)
            {
                return UpgradeResultsEnum.Cancelled;
            }

            // Upgrade the DataTable Schema for DateTime default value
            Dictionary<SchemaAttributesEnum, string> attributes = new Dictionary<SchemaAttributesEnum, string>();
            string defaultDate = DateTimeHandler.ToStringDefaultDateTime(new DateTime(1900, 1, 1, 12, 0, 0, 0));
            attributes.Add(SchemaAttributesEnum.Default, defaultDate);
            success = fileDatabase.Database.SchemaAlterColumn(Constant.DBTables.FileData, Constant.DatabaseColumn.DateTime, attributes);
            timelapse.DebugFeedback(success, "DataTable: DateTime changed default to a standard format: " + filePath);
            if (!success)
            {
                return UpgradeResultsEnum.Failed;
            }
            await Task.Delay(Constant.BusyState.SleepTime);

            // Either Remove or Alter the ImageQuality Column
            if (removeImageQualityColumn)
            {
                // Remove Folder from DataTable
                if (false == fileDatabase.Database.SchemaIsColumnInTable(Constant.DBTables.FileData, Constant.DatabaseColumn.ImageQuality))
                {
                    timelapse.DebugFeedback("Data Table: No ImageQuality column to delete: " + filePath); ;
                }
                else
                {
                    // Remove the Image Quality Column
                    success = fileDatabase.Database.SchemaDeleteColumn(Constant.DBTables.FileData,
                        Constant.DatabaseColumn.ImageQuality);
                    timelapse.DebugFeedback(success, "DataTable: ImageQuality schema and data deleted: " + filePath);
                    if (!success)
                    {
                        return UpgradeResultsEnum.Failed;
                    }
                }

                await Task.Delay(Constant.BusyState.SleepTime);
            }
            else
            {
                // Upgrade the DataTable Schema for ImageQuality, and its data values
                // -- Change Ok/Dark data Values to false, true
                // -- then change the schema for ImageQualtiy to Dark DefaultValue false
                fileDatabase.GetControlsSortedByControlOrder();
                fileDatabase.UpgradeImageQualityValuesToDarkValues();
                attributes.Clear();
                attributes.Add(SchemaAttributesEnum.Name, Constant.DatabaseColumn.Dark);
                attributes.Add(SchemaAttributesEnum.Default, Constant.BooleanValue.False);
                success = fileDatabase.Database.SchemaAlterColumn(Constant.DBTables.FileData, Constant.DatabaseColumn.ImageQuality, attributes);
                timelapse.DebugFeedback(success, "DataTable: ImageQuality changed to Dark with Default of false: " + filePath);
                if (!success)
                {
                    return UpgradeResultsEnum.Failed;
                }
                await Task.Delay(Constant.BusyState.SleepTime);
            }

            // Check if a cancel has occured
            if (timelapse.CancelUpgrade)
            {
                return UpgradeResultsEnum.Cancelled;
            }

            // Upgrade Detection.Conf and Detection.BoundingBox if a comma decimal separator was used instead of a . separator
            fileDatabase.UpgradeDetectionConfFromCommasToDecimalsIfNeeded();

            // MARKER TABLE MANIPULATION
            // Upgrade the Marker table to make its Id a foreign-key to the DataTable, and to output the points list as a Json structure
            // This is so that we will only have rows that actually contain non-empty marker data
            UpgradeMarkerTableToJsonFormat(fileDatabase);

            // IMAGE SET TABLE MANIPULATION
            // Remove Timezone from ImageSetTable
            if (false == fileDatabase.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.TimeZone))
            {
                timelapse.DebugFeedback("Image Set Table: No TimeZone column to delete: " + filePath); ;
            }
            else
            {
                success = fileDatabase.Database.SchemaDeleteColumn(Constant.DBTables.ImageSet,
                    Constant.DatabaseColumn.TimeZone);
                timelapse.DebugFeedback(success, "Data Table: Timezone schema and data deleted: " + filePath);
                if (!success)
                {
                    return UpgradeResultsEnum.Failed;
                }
            }

            await Task.Delay(Constant.BusyState.SleepTime);

            // Remove WhiteSpaceTrimmed from ImageSetTable
            if (false == fileDatabase.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.WhiteSpaceTrimmed))
            {
                timelapse.DebugFeedback("Image Set Table: No WhiteSpaceTrimmed column to delete: " + filePath); ;
            }
            else
            {
                success = fileDatabase.Database.SchemaDeleteColumn(Constant.DBTables.ImageSet,
                    Constant.DatabaseColumn.WhiteSpaceTrimmed);
                timelapse.DebugFeedback(success,
                    "ImageSet Table: WhiteSpaceTrimmed schema and data deleted: " + filePath);
                if (!success)
                {
                    return UpgradeResultsEnum.Failed;
                }
            }

            await Task.Delay(Constant.BusyState.SleepTime);

            // Check if a cancel has occured
            if (timelapse.CancelUpgrade)
            {
                return UpgradeResultsEnum.Cancelled;
            }

            // Remove Magnifier from ImageSetTable
            if (false == fileDatabase.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.MagnifyingGlass))
            {
                timelapse.DebugFeedback("Image Set Table: No MagnifyingGlass column to delete: " + filePath); ;
            }
            else
            {
                success = fileDatabase.Database.SchemaDeleteColumn(Constant.DBTables.ImageSet,
                    Constant.DatabaseColumn.MagnifyingGlass);
                timelapse.DebugFeedback(success,
                    "ImageSet Table: MagnifyingGlass schema and data deleted: " + filePath);
                if (!success)
                {
                    return UpgradeResultsEnum.Failed;
                }
            }

            await Task.Delay(Constant.BusyState.SleepTime);

            // Add RootFolder to the ImageSetTable (which replaces the Folder column in the DataTable
            // Calculate the current root folder. This should be better than just getting it from the database, as it is more recent???
            string absolutePathPart = fileDatabase.FolderPath.TrimEnd(Path.DirectorySeparatorChar) + @"\";
            string rootFolder = Path.GetDirectoryName(absolutePathPart);
            rootFolder = string.IsNullOrEmpty(rootFolder)
                ? String.Empty
                : Path.GetFileName(rootFolder);
            SchemaColumnDefinition scd = new SchemaColumnDefinition("RootFolder", "Text", String.Empty);
            fileDatabase.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.ImageSet, scd);
            fileDatabase.Database.SetColumnToACommonValue(Constant.DBTables.ImageSet, "RootFolder", rootFolder);
            timelapse.DebugFeedback("ImageSet Table: Root folder added: " + filePath);
            await Task.Delay(Constant.BusyState.SleepTime);

            // Check if a cancel has occured
            if (timelapse.CancelUpgrade)
            {
                return UpgradeResultsEnum.Cancelled;
            }

            // Delete the Filter and SelectedFOlder columns from the ImageSetTable
            if (false == fileDatabase.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.SelectedFolder))
            {
                timelapse.DebugFeedback("Image Set Table: No SelectedFolder column to delete: " + filePath); ;
            }
            else
            {
                success = fileDatabase.Database.SchemaDeleteColumn(Constant.DBTables.ImageSet,
                    Constant.DatabaseColumn.SelectedFolder);
                timelapse.DebugFeedback(success, "ImageSet Table: SelectedFolder column and data deleted: " + filePath);
                if (!success)
                {
                    return UpgradeResultsEnum.Failed;
                }
            }

            if (false == fileDatabase.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.Selection))
            {
                timelapse.DebugFeedback("Image Set Table: No Selection column to delete: " + filePath); ;
            }
            else
            {
                success = fileDatabase.Database.SchemaDeleteColumn(Constant.DBTables.ImageSet,
                    Constant.DatabaseColumn.Selection);
                timelapse.DebugFeedback(success,
                    "ImageSet Table: Selection column schema and data deleted: " + filePath);
                if (!success)
                {
                    return UpgradeResultsEnum.Failed;
                }
            }

            await Task.Delay(Constant.BusyState.SleepTime);

            // Add a SearchTerm column to the ImageSetTable
            SchemaColumnDefinition scd2 = new SchemaColumnDefinition(Constant.DatabaseColumn.SearchTerms, "Text", "{}");
            fileDatabase.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.ImageSet, scd2);
            timelapse.DebugFeedback("ImageSet Table: SearchTerm added: " + filePath);

            await Task.Delay(Constant.BusyState.SleepTime);

            // Check if a cancel has occured
            if (timelapse.CancelUpgrade)
            {
                return UpgradeResultsEnum.Cancelled;
            }

            // Add a RootFolder column to the ImageSetTable
            if (false == fileDatabase.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.RootFolder))
            {
                SchemaColumnDefinition scd3 = new SchemaColumnDefinition(Constant.DatabaseColumn.BoundingBoxDisplayThreshold, Sql.Text, "");
                fileDatabase.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.ImageSet, scd3);
                timelapse.DebugFeedback("ImageSet Table: BBDisplayThreshold added: " + filePath);
            }
            // Add a BBoxThresholdDefault column to the ImageSetTable
            if (false == fileDatabase.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.BoundingBoxDisplayThreshold))
            {
                SchemaColumnDefinition scd3 = new SchemaColumnDefinition(Constant.DatabaseColumn.BoundingBoxDisplayThreshold, Sql.Real, Constant.DetectionValues.BoundingBoxDisplayThresholdDefault);
                fileDatabase.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.ImageSet, scd3);
                timelapse.DebugFeedback("ImageSet Table: BBDisplayThreshold added: " + filePath);
            }

            // Convert the SortTerm field to JSON
            string sortTermAsJson = fileDatabase.ImageSet.SortTermsAsJson;
            fileDatabase.Database.Upgrade(Constant.DBTables.ImageSet, new ColumnTuple(Constant.DatabaseColumn.SortTerms, sortTermAsJson));

            /// Convert QuickPaste to JSON
            // Get the QuickPasteXML from the database, populate the QuickPaste datastructure with it, and 
            // write it out to the (renamed from QuickPasteXML column) QuickPasteTerms column
            string quickPasteEntriesAsJson = "[]"; // The empty quickpaste structure
            try
            {
                if (fileDatabase.ImageSet?.QuickPasteXML != null)
                {
                    string xml = fileDatabase.ImageSet.QuickPasteXML;
                    List<QuickPasteEntry> quickPasteEntries = QuickPasteOperations.QuickPasteEntriesFromXML(fileDatabase, xml);

                    if (quickPasteEntries?.Count > 0)
                    {
                        quickPasteEntriesAsJson = JsonConvert.SerializeObject(quickPasteEntries, Formatting.Indented);
                    }
                }
            }
            catch
            {
                // if there is no QuickPaste row in the existing database to use, it will just set it to the default
            }
            fileDatabase.Database.SchemaRenameColumn(Constant.DBTables.ImageSet, Constant.DatabaseColumn.QuickPasteXML, Constant.DatabaseColumn.QuickPasteTerms);
            fileDatabase.Database.Upgrade(Constant.DBTables.ImageSet, new ColumnTuple(Constant.DatabaseColumn.QuickPasteTerms, quickPasteEntriesAsJson));
            timelapse.DebugFeedback("ImageSet Table: Quickpaste Updated: " + filePath);

            await Task.Delay(Constant.BusyState.SleepTime);

            // Check if a cancel has occured
            if (timelapse.CancelUpgrade)
            {
                return UpgradeResultsEnum.Cancelled;
            }
            // Info table manipulation. These columns were added as of v2.2.5.2, so we have to check if they were there and add them 
            // (as an older version of the .ddb may not have them)
            if (fileDatabase.Database.TableExists(Constant.DBTables.Info))
            {
                if (false == fileDatabase.Database.SchemaIsColumnInTable(Constant.DBTables.Info, Constant.InfoColumns.DetectorVersion))
                {
                    fileDatabase.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.Info, new SchemaColumnDefinition(Constant.InfoColumns.DetectorVersion, Sql.StringType, Constant.DetectionValues.MDVersionUnknown));
                }
                if (false == fileDatabase.Database.SchemaIsColumnInTable(Constant.DBTables.Info, Constant.InfoColumns.TypicalDetectionThreshold))
                {
                    fileDatabase.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.Info, new SchemaColumnDefinition(Constant.InfoColumns.TypicalDetectionThreshold, Sql.Real, Constant.DetectionValues.DefaultTypicalDetectionThresholdIfUnknown));
                }
                if (false == fileDatabase.Database.SchemaIsColumnInTable(Constant.DBTables.Info, Constant.InfoColumns.ConservativeDetectionThreshold))
                {
                    fileDatabase.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.Info, new SchemaColumnDefinition(Constant.InfoColumns.ConservativeDetectionThreshold, Sql.Real, Constant.DetectionValues.DefaultConservativeDetectionThresholdIfUnknown));
                }
                if (false == fileDatabase.Database.SchemaIsColumnInTable(Constant.DBTables.Info, Constant.InfoColumns.TypicalClassificationThreshold))
                {
                    fileDatabase.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.Info, new SchemaColumnDefinition(Constant.InfoColumns.TypicalClassificationThreshold, Sql.Real, Constant.DetectionValues.DefaultTypicalClassificationThresholdIfUnknown));
                }
                timelapse.DebugFeedback("Info Table: Quickpaste Updated: " + filePath);
            }

            return UpgradeResultsEnum.Upgraded;
        }
        #endregion

        #region Ddb database helper: Upgrade the MarkerTable 
        // Upgrade the Marker table to make its Id a foreign-key to the DataTable
        // This is so that we will only have rows that actually contain non-empty marker data
        // Also change the format of points to a JSON list of points
        private static void UpgradeMarkerTableToJsonFormat(FileDatabase fileDatabase)
        {
            // 1a. Get the DataLabels for counters, as these will comprise the columns in the Marker table
            List<string> columns = new List<string>();
            List<string> columnsWithTypes = new List<string>();
            string tmpMarkerTableName = Constant.DBTables.Markers + "Temp";

            // 1b. Collect the various counter controls so we can add them to the schema
            foreach (ControlRow control in fileDatabase.Controls)
            {
                if (control.Type == Constant.Control.Counter)
                {
                    columns.Add(control.DataLabel);
                    columnsWithTypes.Add(control.DataLabel + " TEXT DEFAULT '[]'");
                }
            }

            // 2. Create the Table, essentially the Id (which will match the DataTable row Id) followed by the DataLabels of the counters
            //    Form: "Create TABLE MarkerTableNew (Id INTEGER PRIMARY KEY, <comma-separated column names> Foreign Key(Id) REFERENCES DataTable(Id) ON DELETE CASCADE)";
            string cmd = Sql.CreateTable + tmpMarkerTableName + Sql.OpenParenthesis + Constant.DatabaseColumn.ID + Sql.IntegerType + Sql.PrimaryKey + Sql.Comma;
            if (columnsWithTypes.Count > 0)
            {
                cmd += String.Join(",", columnsWithTypes) + ", ";
            }
            cmd += Sql.ForeignKey + Sql.OpenParenthesis + Constant.DatabaseColumn.ID + Sql.CloseParenthesis + Sql.References + Constant.DBTables.FileData + Sql.OpenParenthesis + Constant.DatabaseColumn.ID + Sql.CloseParenthesis;
            cmd += Sql.OnDeleteCascade + Sql.CloseParenthesis;
            fileDatabase.Database.ExecuteNonQuery(cmd);


            // 3. Load the existing data from the original Markers table into the new table as a Json structure
            //    Rows whose columns all have empty values are skipped, i.e., not added to the table
            List<string> sqlCommands = new List<string>();      // A list of sql commands

            // Load the markers table from the database
            fileDatabase.MarkersLoadRowsFromDatabase();
            foreach (MarkerRow row in fileDatabase.Markers)
            {
                bool hasValues = false;
                List<List<Point>> cellsInRow = new List<List<Point>>(); // A list of all cells, with each cell listing the point entries
                // Note that we have to handle the special case where the order of cells differs from the schema column order.
                // When that happens,  marker data may be inserted in the wrong column
                // This occurs when:
                // - a pre-2.3 tdb and ddb contains multiple counters
                // - the control order is changed from the original one (as recorded in the template)
                // - the ddb is then updated. 
                // To remedy this, we should use the counter order as indicated in columns (which is the new schema order)
                // rather than in row.DataLabels (which is the old column order). As a safety check, we do compare the column names to make
                // sure they both contain the same thing, albeit in perhaps different order.
                // This test checks to see if both have the same datalabels, where order doesn't matter.
                //if (Enumerable.SequenceEqual(columns.OrderBy(e => e), row.DataLabels.OrderBy(e => e)))
                //{
                //    System.Diagnostics.Debug.Print("Problem: Marker columns are not in the same place as Marker data");
                //}
                foreach (string dataLabel in columns)
                {
                    // Collect the cell values (i.e., the points) corresponding to each data label
                    string cellValue = row[dataLabel];
                    List<Point> pointsInCell = new List<Point>();  // Each entry represents a cell
                    if (false == string.IsNullOrEmpty(cellValue))
                    {
                        // We have at least one value, so we will want to create a row for it.
                        hasValues = true;

                        // Extract the points in each cell as a list
                        List<string> m = cellValue.Split('|').ToList();
                        foreach (string scoords in m)
                        {
                            pointsInCell.Add(Point.Parse(scoords));
                        }
                    }
                    // Add the points in each cell into the list of cells. This includes 'empty' pointsInCell lists, which will be written in the Json as '[]'
                    cellsInRow.Add(pointsInCell);
                }

                // Create a command adding a new populated row only if at least one cell had a non-empty value in it. 
                if (hasValues)
                {
                    List<string> rowPointListAsJson = new List<string>();
                    foreach (List<Point> markerCellAsList in cellsInRow)
                    {
                        rowPointListAsJson.Add(Sql.Quote(JsonConvert.SerializeObject(markerCellAsList)));
                    }
                    sqlCommands.Add(
                        $" Insert into {tmpMarkerTableName} ( {string.Join(",", columns)}, Id ) VALUES ({string.Join(",", rowPointListAsJson)},{row.ID})");
                }
            }
            // We should now have all the commands populating the new Notes table. 
            if (sqlCommands.Count > 0)
            {
                fileDatabase.Database.ExecuteNonQueryWrappedInBeginEnd(sqlCommands);
            }

            // 4. Drop the original markers table and rename the new markers table to the old one
            fileDatabase.Database.DropTable(Constant.DBTables.Markers);
            fileDatabase.Database.SchemaRenameTable(tmpMarkerTableName, Constant.DBTables.Markers);
        }
        #endregion Upgrade the MarkerTable
    }
}
