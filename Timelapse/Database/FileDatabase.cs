using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Configuration;
using System.Windows;
using System.Windows.Controls;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using Newtonsoft.Json;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Images;
using Timelapse.Recognition;
using Timelapse.SearchingAndSorting;
using Timelapse.Standards;
using Timelapse.Util;
using Control = Timelapse.Constant.Control;
using File = System.IO.File;
using Path = System.IO.Path;

namespace Timelapse.Database
{
    // FileDatabase manages the Timelapse data held in datatables and the .ddb files.
    // It also acts as a go-between with the database, where it forms Timelapse-specific SQL requests to the SQL wrapper
    public partial class FileDatabase : CommonDatabase
    {
        #region Private variables

        private DataGrid boundGrid;
        private bool disposed;
        private DataRowChangeEventHandler onFileDataTableRowChanged;

        // These two dictionaries mirror the contents of the detectionCategory and classificationCategory database table
        // for faster access
        public Dictionary<string, string> detectionCategoriesDictionary;
        public Dictionary<string, string> classificationCategoriesDictionary;
        public Dictionary<string, string> classificationDescriptionsDictionary;
        public DataTable detectionDataTable; // Mirrors the database detection table
        #endregion

        #region Properties

        // The current file selection (All, Custom, etc.)
        public FileSelectionEnum FileSelectionEnum { get; set; } = FileSelectionEnum.All;
        public CustomSelection CustomSelection { get; set; }

        /// <summary>Gets the file name of the database on disk.</summary>
        public string FileName { get; private set; }

        /// <summary>Get the complete path to the folder containing the images.</summary>
        public string RootPathToImages { get; }
        /// <summary>Get the complete path to the folder containing the database files.</summary>
        public string RootPathToDatabase { get; }

        public Dictionary<string, string> DataLabelFromStandardControlType { get; }

        public Dictionary<string, FileTableColumn> FileTableColumnsByDataLabel { get; }

        // contains the results of the data query
        public FileTable FileTable { get; private set; }

        public ImageSetRow ImageSet { get; private set; }

        // A list of potential shortcuts found to the image folder. If there is only one, then its a valid shortcut
        public List<string> ShortcutFoldersFound { get; private set; }

        // Whether a shortcut to the image folder is being
        public bool IsShortcutToImageFolder { get; private set; }
        // contains the markers
        public DataTables.DataTableBackedList<MarkerRow> Markers { get; private set; }

        // contains the current metadata tables by level
        public Dictionary<int, DataTables.DataTableBackedList<MetadataRow>> MetadataTablesByLevel { get; private set; }

        // Return the selected folder (if any)
        public string GetSelectedFolder
        {
            get
            {
                if (CustomSelection == null)
                {
                    return string.Empty;
                }

                return CustomSelection.GetRelativePathFolder;
            }
        }

        // Get all the relative paths in the current selection from the FileTable
        private IEnumerable<string> relativePathsInCurrentSelection;
        public IEnumerable<string> GetRelativePathsInCurrentSelection
        {
            get
            {
                return relativePathsInCurrentSelection ?? (relativePathsInCurrentSelection = this.FileTable.Select(o => o.RelativePath).Distinct());
            }
            private set => relativePathsInCurrentSelection = value;
        }

        #endregion

        #region Create or Open the Database
        private FileDatabase(string filePath, bool useShortcuts)
                    : base(filePath)
        {
            DataLabelFromStandardControlType = new Dictionary<string, string>();
            disposed = false;
            RootPathToDatabase = Path.GetDirectoryName(filePath);
            if (useShortcuts == false)
            {
                RootPathToImages = RootPathToDatabase;
                this.ShortcutFoldersFound = null;
            }
            else
            {
                // Get the shortcuts. If we have one, we can use that. If we have more than one, we have a problem
                List<string> shortcutPathsToImages = ShortcutFiles.GetUniqueFolderTargetsFromPath(RootPathToDatabase);
                if (shortcutPathsToImages.Count == 0)
                {
                    // No shortcuts, so use the root folder to search for images
                    RootPathToImages = RootPathToDatabase;
                }
                else if (shortcutPathsToImages.Count == 1)
                {
                    // We have a single shortcut, which means we should use that for our images.
                    // By setting MultipleShortcuts to the destination folder paths, higher level callers can examine that
                    // to see if there are multiple shortcuts, and if so abort and generate an error dialog.
                    RootPathToImages = shortcutPathsToImages[0];
                    this.ShortcutFoldersFound = shortcutPathsToImages;
                    this.IsShortcutToImageFolder = true;
                }
                else if (shortcutPathsToImages.Count > 1)
                {
                    // We have multiple shortcuts, which is a problem as we don't know what folder to use for our images.
                    // By setting MultipleShortcuts to the destination folder paths, higher level callers can examine that
                    // to see if there are multiple shortcuts, and if so abort and generate an error dialog.
                    RootPathToImages = RootPathToDatabase; //likely not needed as we will be aborting...
                    this.ShortcutFoldersFound = shortcutPathsToImages;
                }
            }

            FileName = Path.GetFileName(filePath);
            FileTableColumnsByDataLabel = new Dictionary<string, FileTableColumn>();
        }

        public static async Task<FileDatabase> CreateEmptyDatabase(string ddbFilePath, CommonDatabase templateDatabase)
        {
            // The ddbFilePath
            FilesFolders.TryDeleteFileIfExists(ddbFilePath);

            // initialize the database if it's newly created
            FileDatabase fileDatabase = new FileDatabase(ddbFilePath, false);
            await fileDatabase.OnDatabaseCreatedAsync(templateDatabase).ConfigureAwait(true);
            return fileDatabase;
        }

        public static async Task<FileDatabase> CreateOrOpenAsync(string filePath, CommonDatabase templateDatabase, CustomSelectionOperatorEnum customSelectionTermCombiningOperator, TemplateSyncResults templateSyncResults, bool backupFileJustMade, bool checkForShortcuts)
        {
            // check for an existing database before instantiating the database as SQL wrapper instantiation creates the database file
            bool populateDatabase = !File.Exists(filePath);

            FileDatabase fileDatabase = new FileDatabase(filePath, checkForShortcuts);
            if (fileDatabase.ShortcutFoldersFound != null && fileDatabase.ShortcutFoldersFound.Count > 1)
            {
                // Multiple shortcuts are present, so we don't know what folder to use for our image set.
                // Abort where the error will be handled by a higher level caller
                return fileDatabase;
            }
            if (backupFileJustMade)
            {
                // if a backup of the db was very recently made, we just update it in this version to avoid doubly creating a backup file
                // a bit of a hack, but it works.
                fileDatabase.mostRecentBackup = DateTime.Now;
            }
            if (populateDatabase)
            {
                // initialize the database if it's newly created
                await fileDatabase.OnDatabaseCreatedAsync(templateDatabase).ConfigureAwait(true);
            }
            else
            {
                // if it's an existing database check if it needs updating to current structure and load data tables
                // await fileDatabase.LoadControlsFromTemplateDBSortedByControlOrderAsync();
                await fileDatabase.OnExistingDatabaseOpenedAsync(templateDatabase, templateSyncResults).ConfigureAwait(true);
            }

            // ensure all tables have been loaded from the database
            if (fileDatabase.ImageSet == null)
            {
                fileDatabase.ImageSetLoadFromDatabase();
            }
            if (fileDatabase.Markers == null)
            {
                fileDatabase.MarkersLoadRowsFromDatabase();
            }

            // Load the various metadata info and template structures from their corresponding tables
            await fileDatabase.LoadMetadataControlsAndInfoFromTemplateTDBSortedByControlOrderAsync();
            if (fileDatabase.MetadataTablesByLevel == null)
            {
                fileDatabase.MetadataTableLoadRowsFromDatabase();
            }

            fileDatabase.CustomSelection = new CustomSelection(fileDatabase.Controls, customSelectionTermCombiningOperator);
            if (false == fileDatabase.PopulateDataLabelMaps())
            {
                // This happens if there is an unrecognized Control type
                return null;
            }

            // Recreate the indexes if they don't exist
            // This could happen as a result of upgrading to 2.3
            if (false == fileDatabase.Database.IndexExists(DatabaseValues.IndexRelativePath))
            {
                fileDatabase.IndexDropForFileAndRelativePathIfExists();
                fileDatabase.IndexCreateForFileAndRelativePathIfNotExists();
            }
            return fileDatabase;
        }

        /// <summary>
        /// Make an empty Data Table based on the information in the Template Table.
        /// Assumes that the database has already been opened and that the Template Table is loaded, where the DataLabel always has a valid value.
        /// Then create both the ImageSet table and the Markers table
        /// </summary>
        protected override async Task OnDatabaseCreatedAsync(CommonDatabase existingTemplateDatabase)
        {
            // copy the template's TemplateTable
            await base.OnDatabaseCreatedAsync(existingTemplateDatabase).ConfigureAwait(true);

            // Create the DataTable from the template
            // First, define the creation string based on the contents of the template. 
            List<SchemaColumnDefinition> schemaColumnDefinitions = new List<SchemaColumnDefinition>
            {
                new SchemaColumnDefinition(DatabaseColumn.ID, Sql.CreationStringPrimaryKey)  // It begins with the ID integer primary key
            };
            foreach (ControlRow control in Controls)
            {
                schemaColumnDefinitions.Add(CreateFileDataColumnDefinition(control));
            }
            Database.CreateTable(DBTables.FileData, schemaColumnDefinitions);

            // Create the ImageSetTable and initialize a single row in it
            schemaColumnDefinitions.Clear();
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(DatabaseColumn.ID, Sql.CreationStringPrimaryKey));  // It begins with the ID integer primary key
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(DatabaseColumn.RootFolder, Sql.Text, string.Empty));
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(DatabaseColumn.Log, Sql.Text, DatabaseValues.ImageSetDefaultLog));
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(DatabaseColumn.MostRecentFileID, Sql.Text));
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(DatabaseColumn.VersionCompatibility, Sql.Text));  // Records the highest Timelapse version number ever used to open this database
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(DatabaseColumn.BackwardsCompatibility, Sql.Text));  // Records the earliest Timelapse version number that is backwards compatible with this database
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(DatabaseColumn.SortTerms, Sql.Text, DatabaseValues.DefaultSortTerms));        // A JSON description of the sort terms
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(DatabaseColumn.SearchTerms, Sql.Text, DatabaseValues.DefaultSearchTerms));        // A JSON description of the search terms
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(DatabaseColumn.QuickPasteTerms, Sql.Text));        // A comma-separated list of 4 sort terms
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(DatabaseColumn.BoundingBoxDisplayThreshold, Sql.Real, RecognizerValues.BoundingBoxDisplayThresholdDefault));        // A comma-separated list of 4 sort terms
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(DatabaseColumn.Standard, Sql.Text, string.Empty));        // The standard used to create the template, if any
            Database.CreateTable(DBTables.ImageSet, schemaColumnDefinitions);

            // Populate the data for the image set with defaults
            // VersionCompatabily
            Version timelapseCurrentVersionNumber = VersionChecks.GetTimelapseCurrentVersionNumber();
            List<ColumnTuple> columnsToUpdate = new List<ColumnTuple>
            {
                new ColumnTuple(DatabaseColumn.RootFolder, Path.GetFileName(RootPathToDatabase)),
                new ColumnTuple(DatabaseColumn.Log, DatabaseValues.ImageSetDefaultLog),
                new ColumnTuple(DatabaseColumn.MostRecentFileID, DatabaseValues.InvalidID),
                //new ColumnTuple(Constant.DatabaseColumn.Selection, allImages.ToString()),
                new ColumnTuple(DatabaseColumn.VersionCompatibility, timelapseCurrentVersionNumber.ToString()),
                new ColumnTuple(DatabaseColumn.BackwardsCompatibility, Constant.DatabaseValues.VersionNumberBackwardsCompatible),
                new ColumnTuple(DatabaseColumn.SortTerms, DatabaseValues.DefaultSortTerms),
                new ColumnTuple(DatabaseColumn.SearchTerms, DatabaseValues.DefaultSearchTerms),
                new ColumnTuple(DatabaseColumn.QuickPasteTerms, DatabaseValues.DefaultQuickPasteJSON),
                new ColumnTuple(DatabaseColumn.Standard, existingTemplateDatabase.GetTemplateStandard())
            };
            List<List<ColumnTuple>> insertionStatements = new List<List<ColumnTuple>>
            {
                columnsToUpdate
            };
            Database.Insert(DBTables.ImageSet, insertionStatements);

            ImageSetLoadFromDatabase();

            // create the Files table
            // This is necessary as files can't be added unless the Files Column is available.  Thus SelectFiles() has to be called after the ImageSetTable is created
            // so that the selection can be persisted.
            await SelectFilesAsync(FileSelectionEnum.All).ConfigureAwait(true);

            BindToDataGrid();

            // Create the MarkersTable and initialize it from the template table
            // TODO: SHOULDN'T MARKERS TABLE BE A FOREIGN KEY??? TO CHECK WHY NOT
            schemaColumnDefinitions.Clear();
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(DatabaseColumn.ID, Sql.CreationStringPrimaryKey));  // It begins with the ID integer primary key
            foreach (ControlRow control in Controls)
            {
                if (control.Type.Equals(Control.Counter))
                {
                    schemaColumnDefinitions.Add(new SchemaColumnDefinition(control.DataLabel, Sql.Text, string.Empty));
                }
            }
            Database.CreateTable(DBTables.Markers, schemaColumnDefinitions);
        }

        protected async Task OnExistingDatabaseOpenedAsync(CommonDatabase templateDatabase, TemplateSyncResults templateSyncResults)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(templateDatabase, nameof(templateDatabase));
            ThrowIf.IsNullArgument(templateSyncResults, nameof(templateSyncResults));

            // Perform TemplateTable initializations.
            await base.LoadControlsFromTemplateDBSortedByControlOrderAsync();

            // PART 1. Update the various template tables as needed

            // Condition 1: using old ddb template?
            if (false == templateSyncResults.UseTdbTemplate)
            {
                // Since we are using the ddb template, we don't have to do anything else here.
                return;
            }

            // Since we are going to update things, we should try to create a backup.
            CreateBackupIfNeeded();

            // Condition 2: Using new template, where template and/metadata tables need updating
            // If directed to use the template / metadata templates found in the template database, replace those tables in the ddb
            // with the ones in the tdb. We do this by first dropping those tables, and then recreating the database. 
            // check and repair differences between the .tdb and .ddb template tables due to  missing or added controls 
            // If we don't replace the tables, then check to see if we should update the MetadataInfo table guids to make them
            // the same as in the new template. We only do this if the aliases and levels are the same and if at least one guid differs.
            bool isDatabaseRecreated = false;
            if (templateSyncResults.SyncRequiredAsDataLabelsDiffer)
            {
                // DataLabels between the TemplateTable in the .tdb and .ddb database differ. 
                // Update the .ddb Template table by dropping the .ddb template tables and replacing it with the ones in the .tdb table. 
                Database.DropTable(DBTables.Template);
                isDatabaseRecreated = true;
            }
            if (templateSyncResults.SyncRequiredAsFolderLevelsDiffer && false == templateSyncResults.InfoHierarchyIncompatibleDifferences)
            {
                Database.DropTable(DBTables.MetadataTemplate);
                Database.DropTable(DBTables.MetadataInfo);
                isDatabaseRecreated = true;

            }
            else if (templateSyncResults.SyncRequiredToUpdateInfoTableGuids)
            {
                // We can keep the MetadataInfo table as is (as it hasn't been dropped), but we should update the guids to match what is in the template
                // since at least one guid differs but the aliases at each level are the same.
                if (null != templateDatabase.MetadataInfo)
                {
                    List<ColumnTuplesWithWhere> updateQueryList = new List<ColumnTuplesWithWhere>();
                    foreach (MetadataInfoRow row in templateDatabase.MetadataInfo)
                    {
                        ColumnTuplesWithWhere ctww = new ColumnTuplesWithWhere();
                        ctww.SetWhere(new ColumnTuple(Control.Level, row.Level));
                        ctww.Columns.Add(new ColumnTuple(Control.Guid, row.Guid)); // Populate the data 
                        updateQueryList.Add(ctww);
                    }
                    Database.Update(DBTables.MetadataInfo, updateQueryList);
                }
            }

            if (isDatabaseRecreated)
            {
                // Since the template in the ddb has been completely replaced, there is no need to check if a sync is 
                // required by other differences between the controls
                await base.OnDatabaseCreatedAsync(templateDatabase).ConfigureAwait(true);

                // If a new Metadata level was added at the end, we have to create the table(s) representing it
                if (templateSyncResults.InfoHierarchyTdbDiffersOnlyWithAppendedLevels)
                {
                    CreateFolderMetadataTablesIfNeeded();
                }
            }
            // At this point, we should have the final filled-in tables, including the Template, FolderDataInfo, FolderDataTemplateTable, and actual Levels tables

            // PART 2. Update the various Data and Levels tables as needed if controls have changed

            // Condition 1: Added controls.
            // The tdb template tables contains one or more datalabels not found in the ddb template tables
            // That is, the .tdb defines additional controls for one or more of the image controls and/or  levels controls 
            // Action: For each new control in the template table or level tables, 
            //           - add a corresponding data column in the ImageTable or level tables
            //           - if it is a counter, add a corresponding data column in the MarkerTable
            foreach (KeyValuePair<int, List<string>> kvp in templateSyncResults.DataLabelsToAddByLevel)
            {
                int level = kvp.Key;
                if (level == 0 && templateSyncResults.DataLabelsToAddByLevel.TryGetValue(level, out var value))
                {
                    // Image level: Handle additional image data controls 
                    foreach (string dataLabel in value)
                    {
                        long id = GetControlIDFromControls(dataLabel);
                        ControlRow control = Controls.Find(id);
                        SchemaColumnDefinition columnDefinition = CreateFileDataColumnDefinition(control);
                        Database.SchemaAddColumnToEndOfTable(DBTables.FileData, columnDefinition);

                        if (control.Type == Control.Counter)
                        {
                            SchemaColumnDefinition markerColumnDefinition = new SchemaColumnDefinition(dataLabel, Sql.Text, DatabaseValues.DefaultMarkerValue);
                            Database.SchemaAddColumnToEndOfTable(DBTables.Markers, markerColumnDefinition);
                        }
                    }
                }

                else if (false == templateSyncResults.InfoHierarchyIncompatibleDifferences
                         && templateSyncResults.DataLabelsToAddByLevel.TryGetValue(level, out var value1))
                {
                    // Metadata levels: Handle additional metadata controls by level
                    foreach (string dataLabel in value1)
                    {
                        // As the table already exists, we just add the column definition
                        MetadataControlRow control = GetControlFromMetadataControls(dataLabel, level);
                        SchemaColumnDefinition columnDefinition = CreateFileDataColumnDefinition(control);
                        Database.SchemaAddColumnToEndOfTable(
                            MetadataComposeTableNameFromLevel(level),
                            columnDefinition);
                    }
                }
            }

            // Condition 2a: Deleted Controls (for the image level only)
            // The ddb template tables contain one or more controls not found in the tdb template tables.
            // That is, .ddb DataTable contain one or more columns that now have no corresponding control 
            // Action: Delete those data columns from the DataTable and/or Levels tables
            int countOfLevelTablesDeleted = 0;
            foreach (KeyValuePair<int, List<string>> kvp in templateSyncResults.DataLabelsToDeleteByLevel)
            {
                int level = kvp.Key;
                if (level == 0 && templateSyncResults.DataLabelsToDeleteByLevel.TryGetValue(level, out var value))
                {
                    // Image level: Handle deleted image data controls 
                    foreach (string dataLabel in value)
                    {
                        Database.SchemaDeleteColumn(DBTables.FileData, dataLabel);

                        // Delete the markers column associated with this data label (if it exists) from the Markers table
                        // Note that we do this for all column types, even though only counters have an associated entry in the Markers table.
                        // This is because we can't get the type of the data label as it no longer exists in the Template.
                        if (Database.SchemaIsColumnInTable(DBTables.Markers, dataLabel))
                        {
                            Database.SchemaDeleteColumn(DBTables.Markers, dataLabel);
                            // Delete any empty rows from the Marker Table
                            string where = string.Empty;
                            foreach (ControlRow controlRow in Controls.Where(x => x.Type == Control.Counter))
                            {
                                if (controlRow.Type == Control.Counter)
                                {
                                    if (where != string.Empty)
                                    {
                                        where += Sql.And;
                                    }

                                    where += controlRow.DataLabel + Sql.Equal + Sql.Quote(DatabaseValues.DefaultMarkerValue);
                                }
                            }
                            Database.DeleteRows(DBTables.Markers, where);
                        }
                    }
                }

                else if (false == templateSyncResults.InfoHierarchyIncompatibleDifferences
                         && templateSyncResults.DataLabelsToDeleteByLevel.ContainsKey(level))
                {
                    // Metadata level: Handle deleted metadata controls by level
                    int deleteCount = templateSyncResults.DataLabelsToDeleteByLevel[level].Count;
                    if (deleteCount > 0)
                    {
                        string tableName = MetadataComposeTableNameFromLevel(level);
                        int columnsInTable = Database.SchemaGetColumns(tableName).Count;
                        if (columnsInTable - deleteCount == 2)
                        {
                            // Drop this level's table as there are no longer any controls defined within it
                            Database.DropTable(tableName);
                            countOfLevelTablesDeleted++;
                        }
                        else
                        {
                            // Remove the control from the table
                            foreach (string dataLabel in templateSyncResults.DataLabelsToDeleteByLevel[level])
                            {
                                Database.SchemaDeleteColumn(tableName, dataLabel);
                            }
                        }
                    }
                }
            }

            // Condition 2b
            // If all level tables were dropped,
            // we should clear the FolderDataInfo table, i.e., return it back to its virgin empty state
            if (countOfLevelTablesDeleted == GetMetadataInfoTableLevels().Count)
            {
                Database.DeleteAllRowsInTables(new List<string> { DBTables.MetadataInfo });
            }

            // Condition 3: Renamed Controls
            // The user indicated that the following controls (add/delete) are actually renamed controls
            // Action: Rename those data columns
            foreach (KeyValuePair<int, List<KeyValuePair<string, string>>> kvp in templateSyncResults.DataLabelsToRenameByLevel)
            {
                int level = kvp.Key;
                if (level == 0 && templateSyncResults.DataLabelsToRenameByLevel.TryGetValue(level, out var value))
                {
                    // Image level: Handle Renamed image data controls
                    foreach (KeyValuePair<string, string> dataLabelToRename in value)
                    {
                        // Rename the column associated with that data label from the FileData table
                        Database.SchemaRenameColumn(DBTables.FileData, dataLabelToRename.Key, dataLabelToRename.Value);

                        // Rename the markers column associated with this data label (if it exists) from the Markers table
                        // Note that we do this for all column types, even though only counters have an associated entry in the Markers table.
                        // This is because its easiest to code, as the function handles attempts to delete a column that isn't there (which also returns false).
                        if (Database.SchemaIsColumnInTable(DBTables.Markers, dataLabelToRename.Key))
                        {
                            Database.SchemaRenameColumn(DBTables.Markers, dataLabelToRename.Key, dataLabelToRename.Value);
                        }
                    }
                }

                else if (false == templateSyncResults.InfoHierarchyIncompatibleDifferences
                         && templateSyncResults.DataLabelsToRenameByLevel.TryGetValue(level, out var value1))
                {
                    // Metadata level: Handle Renamed metadata controls
                    foreach (KeyValuePair<string, string> dataLabelToRename in value1)
                    {
                        // Rename the column associated with that data label from the given Level table
                        Database.SchemaRenameColumn(
                            MetadataComposeTableNameFromLevel(level),
                            dataLabelToRename.Key, dataLabelToRename.Value);
                    }
                }
            }

            // Condition 4: Non-critical control updates. Sync individual controls, but only if they differ
            // If we have not replaced the various template tables, we need to check for
            // non-critical updates in the template's row (e.g., that only change the defaults or order or UI attributes as labels, tooltips, visibility, etc.). 
            // To do this, we compare all the template row values, one by one.
            // If there are any differences at all in a row, synchronize (update) that ddb control so that it is the same as the tdb control 
            if (false == isDatabaseRecreated)
            {
                // Image level: Compare Image data controls and sync each if needed
                // Refetch the data labels if needed, as they may have changed due to previous repairs
                List<string> dataLabels = GetDataLabelsExceptIDInSpreadsheetOrderFromControls();
                foreach (string dataLabel in dataLabels)
                {
                    ControlRow ddbControl = GetControlFromControls(dataLabel);
                    ControlRow tdbControl = templateDatabase.GetControlFromControls(dataLabel);

                    // If a control type has changed, we need to clear the search terms as it
                    // stores the current search term by its old value. We could, of course, try to change
                    // it to the new type but that could introduce complications with its stored search value.
                    if (ddbControl.Type != tdbControl.Type)
                    {
                        ColumnTuple columnToUpdate = new ColumnTuple(Constant.DatabaseColumn.SearchTerms, string.Empty);
                        this.Database.Update(Constant.DBTables.ImageSet, columnToUpdate);
                    }

                    // This does the ddb row update. 
                    if (ddbControl.TryUpdateThisControlRowToMatch(tdbControl))
                    {
                        // The control row was updated, so synchronize it to the database
                        SyncControlToDatabase(ddbControl);
                    }
                }

                // Metadata levels: Compare Level data controls and sync each if needed
                if (false == templateSyncResults.InfoHierarchyIncompatibleDifferences)
                {
                    await LoadMetadataControlsAndInfoFromTemplateTDBSortedByControlOrderAsync();
                    foreach (MetadataInfoRow row in MetadataInfo)
                    {
                        Dictionary<string, string> typedDataLabels = GetTypedDataLabelsExceptIDInSpreadsheetOrderFromMetadataControls(row.Level);
                        foreach (string dataLabel in typedDataLabels.Keys)
                        {
                            MetadataControlRow ddbControl = GetControlFromMetadataControls(dataLabel, row.Level);
                            MetadataControlRow tdbControl = templateDatabase.GetControlFromMetadataControls(dataLabel, row.Level);

                            // This does the ddb row update. 
                            if (ddbControl.TryUpdateThisControlRowToMatch(tdbControl))
                            {
                                // The control row was updated, so synchronize it to the database
                                SyncMetadataControlsToDatabase(ddbControl);
                            }
                        }
                    }
                }
            }

            // Version 2.3.2.9 changed how recognition tables are managed.
            // Update those tables to their new format if needed
            this.UpdateOldStyleRecognitionTablesIfNeeded();
        }

        private static SchemaColumnDefinition CreateFileDataColumnDefinition(CommonControlRow control)
        {
            if (control.DataLabel == DatabaseColumn.DateTime)
            {
                if (DateTimeHandler.TryParseDatabaseDateTime(control.DefaultValue, out _))
                {
                    return new SchemaColumnDefinition(control.DataLabel, "DATETIME", control.DefaultValue);
                }

                // The date/time is malformed, so just use the default. Not optimal, but...
                return new SchemaColumnDefinition(control.DataLabel, "DATETIME", DateTimeHandler.ToStringDatabaseDateTime(ControlDefault.DateTimeDefaultValue));
            }

            if (control.Type == Control.IntegerPositive ||
                control.Type == Control.IntegerAny ||
                control.Type == Control.DecimalPositive ||
                control.Type == Control.DecimalAny)
            {
                // TODO Prefer  it as a number-type, but it breaks the DataRow
                return new SchemaColumnDefinition(control.DataLabel, "TEXT", "");
            }

            if (string.IsNullOrWhiteSpace(control.DefaultValue))
            {
                return new SchemaColumnDefinition(control.DataLabel, Sql.Text, string.Empty);
            }
            return new SchemaColumnDefinition(control.DataLabel, Sql.Text, control.DefaultValue);
        }

        /// <summary>
        /// Create lookup tables that allow us to retrieve a key from a type and vice versa
        /// </summary>
        private bool PopulateDataLabelMaps()
        {
            foreach (ControlRow control in Controls)
            {
                FileTableColumn column = FileTableColumn.CreateColumnMatchingControlRowsType(control);
                if (column?.DataLabel == null || FileTableColumnsByDataLabel.ContainsKey(column.DataLabel))
                {
                    //MessageBox.Show("Oops in PopulateDataLabelMaps");
                    // this occurs if the control is not one of the recognized Type
                    return false;
                }
                FileTableColumnsByDataLabel.Add(column.DataLabel, column);
                // don't type map user defined controls as if there are multiple ones the key would not be unique
                if (IsCondition.IsStandardControlType(column.ControlType))
                {
                    DataLabelFromStandardControlType.Add(column.ControlType, column.DataLabel);
                }
            }
            return true;
        }
        #endregion

        #region Upgrade Databases and Templates
        public static async Task<FileDatabase> UpgradeDatabasesAndCompareTemplates(string filePath, CommonDatabase templateDatabase, TemplateSyncResults templateSyncResults)
        {
            // If the file doesn't exist, then no immediate action is needed
            if (!File.Exists(filePath))
            {
                return null;
            }

            FileDatabase fileDatabase = new FileDatabase(filePath, false);
            if (fileDatabase.Database.PragmaGetQuickCheck() == false || fileDatabase.DoesTableExist(DBTables.FileData) == false)
            {
                // The database file is likely corrupt, possibly due to missing a key table, is an empty file, or is otherwise unreadable
                fileDatabase.Dispose();
                return null;
            }
            await fileDatabase.UpgradeDatabasesAndCompareTemplatesAsync(templateDatabase, templateSyncResults).ConfigureAwait(true);
            return fileDatabase;
        }

        protected async Task UpgradeDatabasesAndCompareTemplatesAsync(CommonDatabase templateDatabase, TemplateSyncResults templateSyncResults)
        {

            // Check the arguments for null 
            ThrowIf.IsNullArgument(templateDatabase, nameof(templateDatabase));
            ThrowIf.IsNullArgument(templateSyncResults, nameof(templateSyncResults));

            // This backup check forces Timelapse to create, if needed, special checkpoint files for both .ddb and .tdb files before any templates or databases are updated.
            // This is usually reserved for non-backwards compatable database changes, just in case.
            // Sample code to create a special backup file. Replace the version number with thedesired one.
            //if (this.TryGetImageSetVersionNumber(out string imageSetVersionNumber, false))
            //{
            //    string criticalVersionNumber = "2.2.5.0";
            //    if (VersionChecks.IsVersion1GreaterThanVersion2(criticalVersionNumber, imageSetVersionNumber))
            //    {
            //        // Create the special backups file
            //        string criticalVersionNumberFileAddition = "pre-v" + criticalVersionNumber;

            //        // the .ddb file
            //        FileBackup.TryCreateBackup(Path.GetDirectoryName(this.FilePath), Path.GetFileName(this.FilePath), false, criticalVersionNumberFileAddition);
            //        this.mostRecentBackup = DateTime.Now;

            //        // the .tdb file - note that this will have been upgraded. I'm not sure how to get the original version, but the updates shouldn't be critical to this purpose 
            //        FileBackup.TryCreateBackup(Path.GetDirectoryName(this.FilePath), Path.GetFileName(templateDatabase.FilePath), false, criticalVersionNumberFileAddition);
            //        templateDatabase.mostRecentBackup = DateTime.Now;
            //    }
            //}

            // Note that the templateDatabase are guaranteed to have the MetadataTables and their corresponding data structures included
            // as definded in the tdb, or as empty tables/datastructures if they are not present in the tdb file (e.g., an old format tdb)
            await UpgradeDatabasesForBackwardsCompatabilityAsync(templateDatabase).ConfigureAwait(true);

            // Note that at this point the ddb database from older to newer formats to preserve backwards compatability
            // At this point, the ddb database will include metadata template tables if they were missing. As well, their corresponding data structures
            // will be non-null and populated with any template metadata .

            // Perform TemplateTable initializations
            await base.LoadControlsFromTemplateDBSortedByControlOrderAsync();

            // Compare the image-level controls for differences between the tdb and the ddb templates
            await CompareImageControlsBetweenTemplates(templateDatabase, templateSyncResults);

            // Compare the folder hierarchy structure for differences between the tdb and the ddb templates
            // - To do that we need to load the Filedatabase's metadata data structures based from the .ddb template
            // - Prior code should guarantee that both metadata tables and their data structures
            //   exist for both the Filedatabase and the templateDatabase to reflect their respective template contents
            await LoadMetadataControlsAndInfoFromTemplateTDBSortedByControlOrderAsync();
            MetadataCompareLevelHierarchyStructureBetweenTemplates(templateDatabase, templateSyncResults);

            // Compare each folder-level data fields for differences between the tdb and the ddb templates
            await MetadataCompareCommonFolderLevelControlsBetweenTemplates(templateDatabase, templateSyncResults);
        }

        // Only invoke this when we know the templateDBs are in sync, and the templateDB matches the FileDB (i.e., same control rows/columns) except for one or more defaults.
        public void UpgradeFileDBSchemaDefaultsFromTemplate()
        {
            // Initialize a schema 
            List<SchemaColumnDefinition> columnDefinitions = new List<SchemaColumnDefinition>
            {
                new SchemaColumnDefinition(DatabaseColumn.ID, Sql.CreationStringPrimaryKey)  // It begins with the ID integer primary key
            };

            // Add the schema for the columns from the FileDB table
            foreach (ControlRow control in Controls)
            {
                columnDefinitions.Add(CreateFileDataColumnDefinition(control));
            }

            // Replace the schema in the File DB table with the schema defined by the column definitions.
            Database.SchemaAlterTableWithNewColumnDefinitions(DBTables.FileData, columnDefinitions);
        }

        // Upgrade the database as needed from older to newer formats to preserve backwards compatability 
        private async Task UpgradeDatabasesForBackwardsCompatabilityAsync(CommonDatabase templateDatabase)
        {
            // Some comparisons are triggered by comparing
            // - the version number stored in the DB's ImageSetTable 
            // - the current Timelapse program version of the 
            // - particular version numbers where known changes occured 
            // Note: if we can't retrieve the version number from the image set, then set it to a very low version number to guarantee all checks will be made
            //if (false == this.TryGetImageSetVersionNumber(out string imageSetVersionNumber, true))
            //{
            //    imageSetVersionNumber = Constant.DatabaseValues.VersionNumberMinimum;
            //}
            //// Get the version of the current Timelapse program
            //string timelapseVersionNumberAsString = VersionChecks.GetTimelapseCurrentVersionNumber().ToString();

            // Code below checks and repairs backward compatability
            await Task.Run(() =>
            {
                // If the ExportToCSV column isn't in the template, it means we are opening up 
                // an old version of the template. Update the table by adding a new ExportToCSV column filled with the appropriate default
                AddExportToCSVColumnIfNeeded(Database);

                // If there is no TemplateInfo table or a single row within it, create one
                AddTemplateInfoTableOrRowIfNeeded(templateDatabase.Database);

                // If the Standards column isn't in the ImageSet table or in the template's TemplateInfo table, add it
                // Note: It should have been added previously to the template table
                AddStandardToTemplateInfoColumnIfNeeded(templateDatabase.Database);
                AddStandardToImageSetColumnIfNeeded(Database);

                // If the BackwardsCompatibility column isn't in the ImageSet table or in the template's TemplateInfo table, add it
                // Note:  BackwardsCompatibility should have been added previously to the template table in DoCreateOrOpenAsync
                AddBackwardsCompatibilityToTemplateInfoColumnIfNeeded(templateDatabase.Database);
                AddBackwardsCompatibilityToImageSetColumnIfNeeded(Database);

                // If there are no metadata tables in the Ddb database, create them and their corresponding data structures
                // Note that this is specific to the metadata tables in the DDB database, which may differ from table contents (if any) that are present in the TDB database
                CreateEmptyMetadataTablesIfNeeded();

                // If the Detections Table is missing the frame_number column, add it.
                AddDetectionsVideoTableIfNeeded(Database);

                // See pre-2.2.2.5 version for example code
            }).ConfigureAwait(true);
        }
        #endregion

        #region Add Files to the Database
        // Add file rows to the database. This generates an SQLite command in the form of:
        // INSERT INTO DataTable (columnnames) (imageRow1Values) (imageRow2Values)... for example,
        // INSERT INTO DataTable ( File, RelativePath, Folder, ... ) VALUES   
        // ( 'IMG_1.JPG', 'relpath', 'folderfoo', ...) ,  
        // ( 'IMG_2.JPG', 'relpath', 'folderfoo', ...)
        // ...
        public void AddFiles(List<ImageRow> files, Action<ImageRow, int> onFileAdded)
        {
            if (files == null)
            {
                // Nothing to do
                return;
            }

            StringBuilder queryColumns = new StringBuilder(Sql.InsertInto + DBTables.FileData + Sql.OpenParenthesis); // INSERT INTO DataTable (
            Dictionary<string, string> defaultValueLookup = GetDefaultControlValueLookup();

            // Create a comma-separated lists of column names
            // e.g., ... File, RelativePath, Folder, DateTime, ..., 
            foreach (string columnName in FileTable.ColumnNames)
            {
                if (columnName == DatabaseColumn.ID)
                {
                    // skip the ID column as it's not associated with a data label and doesn't need to be set as it's autoincrement
                    continue;
                }
                queryColumns.Append(columnName);
                queryColumns.Append(Sql.Comma);
            }

            queryColumns.Remove(queryColumns.Length - 2, 2); // Remove trailing ", "
            queryColumns.Append(Sql.CloseParenthesis + Sql.Values);

            // We should now have a partial SQL expression in the form of: INSERT INTO DataTable ( File, RelativePath, Folder, DateTime, ... )  VALUES 
            // Create a dataline from each of the image properties, add it to a list of data lines, then do a multiple insert of the list of datalines to the database
            // We limit the datalines to RowsPerInsert
            int fileCount = files.Count;
            for (int image = 0; image < fileCount; image += DatabaseValues.RowsPerInsert)
            {
                StringBuilder queryValues = new StringBuilder();

                // This loop creates a dataline containing this image's property values, e.g., ( 'IMG_1.JPG', 'relpath', 'folderfoo', ...) ,  
                for (int insertIndex = image; (insertIndex < (image + DatabaseValues.RowsPerInsert)) && (insertIndex < fileCount); insertIndex++)
                {
                    queryValues.Append(Sql.OpenParenthesis);

                    foreach (string columnName in FileTable.ColumnNames)
                    {
                        // Fill up each column in order
                        if (columnName == DatabaseColumn.ID)
                        {
                            // don't specify an ID in the insert statement as it's an autoincrement primary key
                            continue;
                        }

                        // If a control's field already has a value in it, use that. Otherwise populate it with its default value.

                        string controlType = FileTableColumnsByDataLabel[columnName].ControlType;
                        ImageRow imageProperties = files[insertIndex];
                        switch (controlType)
                        {
                            case DatabaseColumn.File:
                                // The File should always be filled in, so use that.
                                queryValues.Append($"{Sql.Quote(imageProperties.File)}{Sql.Comma}");
                                break;

                            case DatabaseColumn.RelativePath:
                                // The RelativePath should always be filled in, so use that.
                                queryValues.Append($"{Sql.Quote(imageProperties.RelativePath)}{Sql.Comma}");
                                break;

                            case DatabaseColumn.DateTime:
                                // The DateTime should always be filled in, so use that.
                                queryValues.Append($"{Sql.Quote(DateTimeHandler.ToStringDatabaseDateTime(imageProperties.DateTime))}{Sql.Comma}");
                                break;

                            case DatabaseColumn.DeleteFlag:
                                string dataLabel = DataLabelFromStandardControlType[DatabaseColumn.DeleteFlag];
                                string deleteFlagValue = imageProperties.GetValueDisplayString(columnName);
                                if (null != deleteFlagValue && deleteFlagValue != defaultValueLookup[columnName] && Boolean.TryParse(deleteFlagValue, out _))
                                {
                                    // use the current value, if it exists
                                    queryValues.Append($"{Sql.Quote(deleteFlagValue)}{Sql.Comma}");
                                }
                                else
                                {
                                    // Default as specified in the template file, which should be "false"
                                    queryValues.Append($"{Sql.Quote(defaultValueLookup[dataLabel])}{Sql.Comma}");
                                }
                                break;

                            case Control.Flag:
                                string flagValue = imageProperties.GetValueDisplayString(columnName);
                                if (null != flagValue && flagValue != defaultValueLookup[columnName] && Boolean.TryParse(flagValue, out _))
                                {
                                    // use the current value, if it exists and is valid
                                    queryValues.Append($"{Sql.Quote(flagValue)}{Sql.Comma}");
                                }
                                else
                                {
                                    // Default as specified in the template file, which should be "false"
                                    queryValues.Append($"{Sql.Quote(defaultValueLookup[columnName])}{Sql.Comma}");
                                }
                                break;

                            // Find and then add the customizable types, populating it with their default values.

                            case Control.Note:
                            case Control.MultiLine:
                                // use the current text value, if it exists and is valid
                                string value = imageProperties.GetValueDisplayString(columnName);
                                if (false == string.IsNullOrWhiteSpace(value) && value != defaultValueLookup[columnName])
                                {
                                    // Use the current value, if it exists
                                    queryValues.Append($"{Sql.Quote(imageProperties.GetValueDisplayString(columnName))}{Sql.Comma}");
                                }
                                else
                                {
                                    // Use its defaults
                                    queryValues.Append($"{Sql.Quote(defaultValueLookup[columnName])}{Sql.Comma}");
                                }
                                break;

                            // Find and then add the customizable types, populating it with their default values.
                            case Control.AlphaNumeric:
                                // use the current alphanumeric value, if it exists and is valid
                                string alphaNumericValue = imageProperties.GetValueDisplayString(columnName);
                                if (false == string.IsNullOrWhiteSpace(alphaNumericValue) && alphaNumericValue != defaultValueLookup[columnName] && IsCondition.IsAlphaNumeric(alphaNumericValue))
                                {
                                    // Use the current value, if it exists
                                    queryValues.Append($"{Sql.Quote(imageProperties.GetValueDisplayString(columnName))}{Sql.Comma}");
                                }
                                else
                                {
                                    // Use its defaults
                                    queryValues.Append($"{Sql.Quote(defaultValueLookup[columnName])}{Sql.Comma}");
                                }
                                break;

                            case Control.FixedChoice:
                            case Control.MultiChoice:
                                // Initialize choices to its values
                                string choiceValue = imageProperties.GetValueDisplayString(columnName);
                                if (false == string.IsNullOrWhiteSpace(choiceValue) && choiceValue != defaultValueLookup[columnName])
                                {
                                    // Use the current value, if it exists
                                    queryValues.Append($"{Sql.Quote(choiceValue)}{Sql.Comma}");
                                }
                                else
                                {
                                    // Use its defaults
                                    queryValues.Append($"{Sql.Quote(defaultValueLookup[columnName])}{Sql.Comma}");
                                }
                                break;

                            case Control.IntegerAny:
                                // Initialize an intAny field to its values
                                string intAnyValue = imageProperties.GetValueDisplayString(columnName);
                                if (false == string.IsNullOrWhiteSpace(intAnyValue) && intAnyValue != defaultValueLookup[columnName] && IsCondition.IsInteger(intAnyValue))
                                {
                                    // Use the current value, if it exists and is valid
                                    queryValues.Append($"{Sql.Quote(intAnyValue)}{Sql.Comma}");
                                }
                                else
                                {
                                    // Use its defaults
                                    queryValues.Append($"{Sql.Quote(defaultValueLookup[columnName])}{Sql.Comma}");
                                }
                                break;

                            case Control.Counter:
                            case Control.IntegerPositive:
                                // Initialize an intPositive field to its values
                                string intPositiveValue = imageProperties.GetValueDisplayString(columnName);
                                if (false == string.IsNullOrWhiteSpace(intPositiveValue) && intPositiveValue != defaultValueLookup[columnName] && IsCondition.IsIntegerPositive(intPositiveValue))
                                {
                                    // Use the current value, if it exists and is valid
                                    // Note that we didn't do the positive/negative test! 
                                    queryValues.Append($"{Sql.Quote(intPositiveValue)}{Sql.Comma}");
                                }
                                else
                                {
                                    // Use its defaults
                                    queryValues.Append($"{Sql.Quote(defaultValueLookup[columnName])}{Sql.Comma}");
                                }
                                break;

                            case Control.DecimalAny:
                                // Initialize a number field to its values
                                string decimalAnyValue = imageProperties.GetValueDisplayString(columnName);
                                if (false == string.IsNullOrWhiteSpace(decimalAnyValue) && decimalAnyValue != defaultValueLookup[columnName] && IsCondition.IsDecimal(decimalAnyValue))
                                {
                                    // Use the current value, if it exists and is valid
                                    queryValues.Append($"{Sql.Quote(decimalAnyValue)}{Sql.Comma}");
                                }
                                else
                                {
                                    // Use its defaults
                                    queryValues.Append($"{Sql.Quote(defaultValueLookup[columnName])}{Sql.Comma}");
                                }
                                break;
                            case Control.DecimalPositive:
                                // Initialize a number field to its values
                                string decimalPositive = imageProperties.GetValueDisplayString(columnName);
                                if (false == string.IsNullOrWhiteSpace(decimalPositive) && decimalPositive != defaultValueLookup[columnName] && IsCondition.IsDecimalPositive(decimalPositive))
                                {
                                    // Use the current value, if it exists and is valid

                                    queryValues.Append($"{Sql.Quote(decimalPositive)}{Sql.Comma}");
                                }
                                else
                                {
                                    // Use its defaults
                                    queryValues.Append($"{Sql.Quote(defaultValueLookup[columnName])}{Sql.Comma}");
                                }
                                break;

                            // All Date conditions are identical except for the check to ensure that the current value (if any) is in the valid DateTime/Date/Time format
                            case Control.DateTime_:
                                string dateTimeValue = imageProperties.GetValueDisplayString(columnName);
                                if (false == string.IsNullOrWhiteSpace(dateTimeValue) && dateTimeValue != defaultValueLookup[columnName] && IsCondition.IsDateTime(dateTimeValue))
                                {
                                    // Use the current value, if it exists and is valid
                                    queryValues.Append($"{Sql.Quote(imageProperties.GetValueDisplayString(columnName))}{Sql.Comma}");
                                }
                                else
                                {
                                    queryValues.Append($"{Sql.Quote(defaultValueLookup[columnName])}{Sql.Comma}");
                                }
                                break;
                            case Control.Date_:
                                string dateValue = imageProperties.GetValueDisplayString(columnName);
                                if (false == string.IsNullOrWhiteSpace(dateValue) && dateValue != defaultValueLookup[columnName] && IsCondition.IsDate(dateValue))
                                {
                                    // Use the current value, if it exists and is valid
                                    queryValues.Append($"{Sql.Quote(imageProperties.GetValueDisplayString(columnName))}{Sql.Comma}");
                                }
                                else
                                {
                                    queryValues.Append($"{Sql.Quote(defaultValueLookup[columnName])}{Sql.Comma}");
                                }
                                break;
                            case Control.Time_:
                                string timeValue = imageProperties.GetValueDisplayString(columnName);
                                if (false == string.IsNullOrWhiteSpace(timeValue) && timeValue != defaultValueLookup[columnName] && IsCondition.IsTime(timeValue))
                                {
                                    // Use the current value, if it exists and is valid
                                    queryValues.Append($"{Sql.Quote(imageProperties.GetValueDisplayString(columnName))}{Sql.Comma}");
                                }
                                else
                                {
                                    queryValues.Append($"{Sql.Quote(defaultValueLookup[columnName])}{Sql.Comma}");
                                }
                                break;

                            default:
                                TracePrint.PrintMessage($"Unhandled control type '{controlType}' in AddImages.");
                                break;
                        }
                    }

                    // Remove trailing commam then add " ) ,"
                    queryValues.Remove(queryValues.Length - 2, 2); // Remove ", "
                    queryValues.Append(Sql.CloseParenthesis + Sql.Comma);
                }

                // Remove trailing comma.
                queryValues.Remove(queryValues.Length - 2, 2); // Remove ", "

                // Create the entire SQL command (limited to RowsPerInsert datalines)
                string command = queryColumns + queryValues.ToString();

                CreateBackupIfNeeded();
                Database.ExecuteNonQuery(command);

                if (onFileAdded != null)
                {
                    int lastImageInserted = Math.Min(fileCount - 1, image + DatabaseValues.RowsPerInsert);
                    onFileAdded.Invoke(files[lastImageInserted], lastImageInserted);
                }
            }
        }

        /// <summary>
        /// Returns a dictionary populated with control default values based on the control data label.
        /// </summary>
        private Dictionary<string, string> GetDefaultControlValueLookup()
        {
            Dictionary<string, string> results = new Dictionary<string, string>();
            foreach (ControlRow control in Controls)
            {
                if (!results.ContainsKey(control.DataLabel))
                {
                    results.Add(control.DataLabel, control.DefaultValue);
                }
            }
            return results;
        }
        #endregion

        #region Get the ID of the last row inserted into the database
        public long GetLastInsertedRow(string datatable, string intfield)
        {
            return Database.ScalarGetMaxValueAsLong(datatable, intfield);
        }
        #endregion

        #region Exists (all return true or false)
        /// <summary>
        /// Return true/false if the relativePath and filename exist in the Database DataTable  
        /// </summary>
        /// <param name="relativePath"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public bool ExistsRelativePathAndFileInDataTable(string relativePath, string filename)
        {
            // Form: Select Exists(Select 1 from DataTable where RelativePath='cameras\Camera1' AND File='IMG_001.JPG')
            string query = Sql.SelectExists + Sql.OpenParenthesis + Sql.SelectOne + Sql.From + DBTables.FileData;
            query += Sql.Where + DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(relativePath);
            query += Sql.And + DatabaseColumn.File + Sql.Equal + Sql.Quote(filename) + Sql.CloseParenthesis;
            return Database.ScalarBoolFromOneOrZero(query);
        }
        #endregion

        #region Select Files in the file table
        /// <summary> 
        /// Rebuild the file table with all files in the database table which match the specified selection.
        /// CODECLEANUP:  should probably merge all 'special cases' of selection (e.g., detections, etc.) into a single class so they are treated the same way, 
        /// eg., to simplify CountAllFilesMatchingSelectionCondition vs SelectFilesAsync
        /// PERFORMANCE can be a slow query on very large databases. Could check for better SQL expressions or database design, but need an SQL expert for that
        /// </summary>
        public async Task SelectFilesAsync(FileSelectionEnum selection)
        {
            string query = string.Empty;
            this.ResetAfterPossibleRelativePathChanges();
            GlobalReferences.MainWindow.ImageDogear = new ImageDogear(GlobalReferences.MainWindow.DataHandler);

            if (CustomSelection == null)
            {
                // If no custom selections are configure, then just use a standard query
                query += $"{Sql.SelectStarFrom} {DBTables.FileData}";
            }
            else
            {
                if (CustomSelection.RandomSample > 0)
                {
                    // Select * from DataTable WHERE id IN (SELECT id FROM (
                    query += SqlPhrase.GetRandomSamplePrefix();
                }

                // If its a pre-configured selection type, set the search terms to match that selection type
                CustomSelection.SetSearchTermsFromSelection(selection, GetSelectedFolder);

                if (GlobalReferences.DetectionsExists && CustomSelection.ShowMissingDetections)
                {
                    // MISSING DETECTIONS 
                    // Create a partial query that returns all missing detections
                    // Form: SELECT DataTable.* FROM DataTable LEFT JOIN Detections ON DataTable.ID = Detections.Id WHERE Detections.Id IS NULL
                    query += SqlPhrase.SelectMissingDetections(SelectTypesEnum.Star);
                }
                else if (GlobalReferences.DetectionsExists && CustomSelection.RecognitionSelections.UseRecognition && CustomSelection.RecognitionSelections.RecognitionType == RecognitionType.Detection)
                {
                    // DETECTIONS
                    // Create a partial query that returns detections matching some conditions
                    // Form: SELECT DataTable.* FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id
                    query += SqlPhrase.SelectDetections(SelectTypesEnum.Star);
                }
                else if (GlobalReferences.DetectionsExists && CustomSelection.RecognitionSelections.UseRecognition && CustomSelection.RecognitionSelections.RecognitionType == RecognitionType.Classification)
                {
                    // CLASSIFICATIONS 
                    // Same form as Detections but with error checks
                    if (null == this.detectionCategoriesDictionary)
                    {
                        // Error
                        return;
                    }
                    query += SqlPhrase.SelectDetections(SelectTypesEnum.Star);
                }
                else
                {
                    // Standard query (ie., no detections, no missing detections, no classifications 
                    query += $"{Sql.SelectStarFrom} {DBTables.FileData}";
                }
            }

            if (CustomSelection != null) // && (GlobalReferences.DetectionsExists == false || CustomSelection.ShowMissingDetections == false))
            {
                if (GlobalReferences.DetectionsExists == false || CustomSelection.ShowMissingDetections == false)
                {
                    // Standard where 
                    string whereExpression = CustomSelection.GetFilesWhere();
                    if (string.IsNullOrEmpty(whereExpression) == false)
                    {
                        query += whereExpression;
                    }
                }
                else if (GlobalReferences.DetectionsExists || CustomSelection.ShowMissingDetections)
                {
                    // Show missing recognitions is selected: the where clause should only include the data fields (i.e., no recognition conditions), if any
                    string where = CustomSelection.GetFilesWhere(true, true);
                    if (!string.IsNullOrEmpty(where))
                    {
                        query += $"{Sql.And} {where}";
                    }
                }
            }

            // EPISODES-related addition to query.
            // If EpisodeShowAllIfAnyMatch is turned on, and the Episode Note field contains values in the Episode format (e.g.) 25:1/8....
            // We construct a wrapper for selecting files where all files in an episode have at least one file matching the surrounded search condition 
            if (CustomSelection != null && CustomSelection.EpisodeShowAllIfAnyMatch && CustomSelection.EpisodeNoteField != string.Empty)
            {
                string frontWrapper = SqlPhrase.CountOrSelectFilesInEpisodeIfOneFileMatchesFrontWrapper(DBTables.FileData, CustomSelection.EpisodeNoteField, false);
                string backWrapper = $"{Sql.CloseParenthesis}{Sql.CloseParenthesis}";
                query = $"{frontWrapper} {query} {backWrapper}";
            }

            // Sort by primary and secondary sort criteria if an image set is actually initialized (i.e., not null)
            if (ImageSet != null)
            {
                // If the use click the Rank by Detection or Classification confidence, we have to create a sort term for that
                // If so, we will insert that into the normal sort term string shortly
                string rankSortingTerm = string.Empty;

                if (CustomSelection != null && CustomSelection.RecognitionSelections.UseRecognition
                    && CustomSelection.RecognitionSelections.RankByDetectionConfidence)
                {
                    // Detections and classifications: Override any sorting as we have asked to rank the results by detections and then classifications confidence values
                    //term[0] = DatabaseColumn.RelativePath;
                    rankSortingTerm = $"{DBTables.Detections}.{DetectionColumns.Conf}{Sql.Descending},{DBTables.Detections}.{DetectionColumns.ClassificationConf}{Sql.Descending}";
                }
                else if (CustomSelection != null
                         && CustomSelection.RecognitionSelections.UseRecognition
                         && CustomSelection.RecognitionSelections.RecognitionType == RecognitionType.Classification
                         && CustomSelection.RecognitionSelections.RankByClassificationConfidence)
                {
                    // Classifications selected: Override any sorting as we have asked to rank the results by classification confidence values (using detection values as a secondary sort)
                    rankSortingTerm = $"{DBTables.Detections}.{DetectionColumns.ClassificationConf}{Sql.Descending},{DBTables.Detections}.{DetectionColumns.Conf}{Sql.Descending}";

                }
                // Get the specified sort order. We do this by retrieving the two sort terms
                // Given the format of the corrected DateTime
                string[] term = { string.Empty, string.Empty, string.Empty, string.Empty };
                SortTerm[] sortTerm = new SortTerm[2];
                for (int i = 0; i <= 1; i++)
                {
                    sortTerm[i] = ImageSet.GetSortTerm(i);
                    // If we see an empty data label, we don't have to construct any more terms as there will be nothing more to sort
                    if (string.IsNullOrEmpty(sortTerm[i].DataLabel))
                    {
                        if (i == 0)
                        {
                            // If the first term is not set, reset the sort back to the default
                            ResetSortTermsToDefault(term);
                        }
                        break;
                    }

                    if (sortTerm[i].DataLabel == DatabaseColumn.DateTime)
                    {
                        term[i] = $"datetime({DatabaseColumn.DateTime})";

                        // DUPLICATE RECORDS Special case if DateTime is the first search term and there is no 2nd search term. 
                        // If there are multiple files with the same date/time and one of them is a duplicate,
                        // then the duplicate may not necessarily appear in a sequence, as ambiguities just use the ID (a duplicate is created with a new ID that may be very distant from the original record).
                        // So, we default the final sort term to 'File'. However, if this is not the first search term, it can be over-written 
                        term[2] = DatabaseColumn.File;
                    }
                    else if (sortTerm[i].DataLabel == DatabaseColumn.File)
                    {
                        // File: the modified term creates a file path by concatenating relative path and file
                        term[i] =
                            $"{DatabaseColumn.RelativePath}{Sql.Comma}{DatabaseColumn.File}";
                    }

                    else if (sortTerm[i].DataLabel != DatabaseColumn.ID
                             && false == CustomSelection?.SearchTerms?.Exists(x => x.DataLabel == sortTerm[i].DataLabel))
                    {
                        // The Sorting data label doesn't exist (likely because that datalabel was deleted or renamed in the template)
                        // Note: as ID isn't in the list, we have to check that so it can pass through as a sort option
                        // Revert back to the default sort everywhere.
                        ResetSortTermsToDefault(term);
                        break;
                    }
                    else if (sortTerm[i].ControlType == Control.Counter ||
                             sortTerm[i].ControlType == Control.IntegerAny ||
                             sortTerm[i].ControlType == Control.IntegerPositive)
                    {
                        // Its a counter or number type: modify sorting of blanks by transforming it into a '-1' and then by casting it as an integer
                        // Form Cast(COALESCE(NULLIF({sortTerm[i].DataLabel}, ''), '-1') as Integer);
                        term[i] = SqlPhrase.GetCastCoalesceSorttermAsType(sortTerm[i].DataLabel, Sql.AsInteger);
                    }
                    else if (sortTerm[i].ControlType == Control.DecimalAny ||
                             sortTerm[i].ControlType == Control.DecimalPositive)
                    {
                        // Its a decimal type: modify sorting of blanks by transforming it into a '-1' and then by casting it as a decimal
                        // Form: Cast(COALESCE(NULLIF({sortTerm[i].DataLabel}, ''), '-1') as Real)
                        term[i] = term[i] = SqlPhrase.GetCastCoalesceSorttermAsType(sortTerm[i].DataLabel, Sql.AsReal);
                    }
                    else
                    {
                        // Default: just sort by the data label
                        term[i] = sortTerm[i].DataLabel;
                    }
                    // Add Descending sort, if needed. Default is Ascending, so we don't have to add that
                    if (sortTerm[i].IsAscending == BooleanValue.False)
                    {
                        term[i] += Sql.Descending;
                    }
                }

                // Random selection - Add suffix
                if (CustomSelection != null && CustomSelection.RandomSample > 0)
                {
                    query += SqlPhrase.GetRandomSampleSuffix(CustomSelection.RandomSample);
                }

                if (!string.IsNullOrEmpty(rankSortingTerm))
                {
                    // As there is a rank sort term, we insert that at the beginning of the sort order
                    query += SqlPhrase.GetOrderByTerm(rankSortingTerm);
                }
                if (!string.IsNullOrEmpty(term[0]))
                {
                    if (string.IsNullOrEmpty(rankSortingTerm))
                    {
                        // Since there was no rank sort term inserted, we  need to insert an OrderBy
                        query += SqlPhrase.GetOrderByTerm(term[0]);
                    }
                    else
                    {
                        // As we added a rankSorting term, we already have an OrderBy so we just insert a comma
                        query += SqlPhrase.GetCommaThenTerm(term[0]);
                    }

                    // If there is a second sort key, add it here
                    if (!string.IsNullOrEmpty(term[1]))
                    {
                        query += SqlPhrase.GetCommaThenTerm(term[1]);
                    }
                    // If there is a third sort key (which would only ever be 'File') add it here.
                    // NOTE: I am not sure if this will always work on every occassion, but my limited test says its ok.
                    if (!string.IsNullOrEmpty(term[2]))
                    {
                        query += SqlPhrase.GetCommaThenTerm(term[2]);
                    }
                }
            }

            // PERFORMANCE  Running a query on a large database that returns a large datatable is very slow.
            // Async call allows busyindicator to run smoothly
            Debug.Print($"SelectFilesAsync Query: {Environment.NewLine}{query}");
            GlobalReferences.TimelapseState.IsNewSelection = true;
            DataTable filesTable = await Database.GetDataTableFromSelectAsync(query);
            FileTable = new FileTable(filesTable);
        }

        // Used by the above
        // Reset sort terms back to the defaults
        private void ResetSortTermsToDefault(string[] term)
        {

            // The Search terms should contain some of the necessary information
            SearchTerm st1 = CustomSelection.SearchTerms.Find(x => x.DataLabel == DatabaseColumn.RelativePath);
            SearchTerm st2 = CustomSelection.SearchTerms.Find(x => x.DataLabel == DatabaseColumn.DateTime);

            SortTerm s1;
            SortTerm s2;
            if (st1 == null || st2 == null)
            {
                // Just in case the search terms aren't filled in, we use default values.
                // This will work, but the Label may not be the one defined by the use which shouldn't be a big deal
                List<SortTerm> defaultSortTerms = SortTerms.GetDefaultSortTerms();
                s1 = defaultSortTerms[0];
                s2 = defaultSortTerms[1];
            }
            else
            {
                s1 = new SortTerm(st1.DataLabel, st1.Label, st1.ControlType, BooleanValue.True);
                s2 = new SortTerm(st2.DataLabel, st2.Label, st2.ControlType, BooleanValue.True);
            }
            term[0] = s1.DataLabel;
            term[1] = s2.DataLabel;

            // Update the Image Set with the new sort terms
            ImageSet.SetSortTerms(s1, s2);
            UpdateSyncImageSetToDatabase();
        }

        // Select all files in the file table
        public FileTable SelectAllFiles()
        {
            string query = Sql.SelectStarFrom + DBTables.FileData;
            DataTable filesTable = Database.GetDataTableFromSelect(query);
            return new FileTable(filesTable);
        }

        public List<long> SelectFilesByRelativePathAndFileName(string relativePath, string fileName)
        {
            string query = Sql.SelectStarFrom + DBTables.FileData + Sql.Where + DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(relativePath) + Sql.And + DatabaseColumn.File + Sql.Equal + Sql.Quote(fileName);
            DataTable fileTable = Database.GetDataTableFromSelect(query);
            List<long> idList = new List<long>();
            for (int i = 0; i < fileTable.Rows.Count; i++)
            {
                idList.Add((long)fileTable.Rows[i].ItemArray[0]);
            }
            return idList;
        }

        // Check for the existence of missing files in the current selection, and return a list of IDs of those that are missing
        // PERFORMANCE this can be slow if there are many files
        public async Task<SelectMissingFilesResultEnum> SelectMissingFilesFromCurrentlySelectedFiles(IProgress<ProgressBarArguments> progress, CancellationTokenSource cancelTokenSource)
        {
            if (FileTable == null)
            {
                return SelectMissingFilesResultEnum.Cancelled;
            }
            string commaSeparatedListOfIDs = string.Empty;
            SelectMissingFilesResultEnum resultEnum = await Task.Run(() =>
            {
                int fileCount = FileTable.RowCount;
                int i = 0;
                // Check if each file exists. Get all missing files in the selection as a list of file ids, e.g., "1,2,8,10" 
                foreach (ImageRow image in FileTable)
                {
                    // Update the progress bar and populate the detection tables
                    //int percentDone = Convert.ToInt32(i++ * 100.0 / fileCount);
                    if (ReadyToRefresh()) //if (newPercentDone != percentDone)
                    {
                        if (cancelTokenSource.Token.IsCancellationRequested)
                        {
                            return SelectMissingFilesResultEnum.Cancelled;
                        }
                        Thread.Sleep(ThrottleValues.ProgressBarSleepInterval); // Allows the UI thread to update every now and then
                        progress.Report(new ProgressBarArguments(
                            Convert.ToInt32(i++ * 100.0 / fileCount),
                            $"Checking to see which files, if any, are missing (now on {i}/{fileCount})",
                            true, false));
                    }

                    if (!File.Exists(Path.Combine(RootPathToImages, image.RelativePath, image.File)))
                    {
                        commaSeparatedListOfIDs += image.ID + ",";
                    }
                    i++;
                }

                // remove the trailing comma
                commaSeparatedListOfIDs = commaSeparatedListOfIDs.TrimEnd(',');
                return string.IsNullOrEmpty(commaSeparatedListOfIDs)
                    ? SelectMissingFilesResultEnum.NoMissingFiles
                    : SelectMissingFilesResultEnum.MissingFilesFound;
            }).ConfigureAwait(true);

            if (SelectMissingFilesResultEnum.MissingFilesFound == resultEnum)
            {
                // the search for missing files was successful, where missing files were found.
                // So we need to select them in the data table
                FileTable = SelectFilesInDataTableByCommaSeparatedIds(commaSeparatedListOfIDs);
                FileTable.BindDataGrid(boundGrid, onFileDataTableRowChanged);
            }
            return resultEnum;
        }

        public List<string> SelectFileNamesWithRelativePathFromDatabase(string relativePath)
        {
            List<string> files = new List<string>();
            // Form: Select * From DataTable Where RelativePath = '<relativePath>'
            string query = Sql.Select + DatabaseColumn.File + Sql.From + DBTables.FileData + Sql.Where + DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(relativePath);
            DataTable images = Database.GetDataTableFromSelect(query);
            int count = images.Rows.Count;
            for (int i = 0; i < count; i++)
            {
                files.Add((string)images.Rows[i].ItemArray[0]);
            }
            images.Dispose();
            return files;
        }

        // Select only those files that are marked for deletion i.e. DeleteFlag = true
        public FileTable SelectFilesMarkedForDeletion()
        {
            string where = DataLabelFromStandardControlType[DatabaseColumn.DeleteFlag] + "=" + Sql.Quote(BooleanValue.True); // = value
            string query = Sql.SelectStarFrom + DBTables.FileData + Sql.Where + where;
            DataTable filesTable = Database.GetDataTableFromSelect(query);
            return new FileTable(filesTable);
        }

        // Select files with matching IDs where IDs are a comma-separated string i.e.,
        // Select * From DataTable Where  Id IN(1,2,4 )
        public FileTable SelectFilesInDataTableByCommaSeparatedIds(string listOfIds)
        {
            string query = Sql.SelectStarFrom + DBTables.FileData + Sql.WhereIDIn + Sql.OpenParenthesis + listOfIds + Sql.CloseParenthesis;
            DataTable filesTable = Database.GetDataTableFromSelect(query);
            return new FileTable(filesTable);
        }

        public FileTable SelectFileInDataTableById(string id)
        {
            string query = Sql.SelectStarFrom + DBTables.FileData + Sql.WhereIDEquals + Sql.Quote(id) + Sql.LimitOne;
            DataTable filesTable = Database.GetDataTableFromSelect(query);
            return new FileTable(filesTable);
        }

        // A specialized call: Given a relative path and two dates (in database DateTime format without the offset)
        // return a table containing ID, DateTime that matches the relative path and is inbetween the two datetime intervals
        public DataTable GetIDandDateWithRelativePathAndBetweenDates(string relativePath, string lowerDateTime, string uppderDateTime)
        {
            // datetimes are in database format e.g., 2017-06-14T18:36:52.000Z 
            // Form: Select ID,DateTime from DataTable where RelativePath='relativePath' and DateTime BETWEEN 'lowerDateTime' AND 'uppderDateTime' ORDER BY DateTime ORDER BY DateTime  
            string query = Sql.Select + DatabaseColumn.ID + Sql.Comma + DatabaseColumn.DateTime + Sql.From + DBTables.FileData;
            query += Sql.Where + DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(relativePath);
            query += Sql.And + DatabaseColumn.DateTime + Sql.Between + Sql.Quote(lowerDateTime) + Sql.And + Sql.Quote(uppderDateTime);
            query += Sql.OrderBy + DatabaseColumn.DateTime;
            return (Database.GetDataTableFromSelect(query));
        }
        #endregion

        #region Return a new sorted list containing the distinct relative paths in the database
        // Return a new sorted list containing the distinct relative paths in the database,
        // and the (unique) parents of each relative path entry.
        // For example, if the relative paths were a/b, a/b/c, a/b/d and d/c it would return
        // a | a/b | a/b/c, a/b/d | d | d/c
        public List<string> GetFoldersFromRelativePaths()
        {
            List<object> relativePathList = GetDistinctValuesInColumn(DBTables.FileData, DatabaseColumn.RelativePath);
            List<string> allPaths = new List<string>();
            foreach (string relativePath in relativePathList.Cast<String>())
            {
                allPaths.Add(relativePath);
                string parent = string.IsNullOrEmpty(relativePath) ? string.Empty : Path.GetDirectoryName(relativePath);
                while (!string.IsNullOrWhiteSpace(parent))
                {
                    if (!allPaths.Contains(parent))
                    {
                        allPaths.Add(parent);
                    }
                    parent = Path.GetDirectoryName(parent);
                }
            }
            allPaths.Sort();
            return allPaths;
        }

        // GetRelativePaths Async wrapper so we can show progress for a long-running operation
        public async Task<List<string>> AsyncGetRelativePaths()
        {
            // Get the relative paths from the database
            return await Task.Run(GetRelativePaths);
        }

        // Get only the distinct and complete relative paths associated with images
        public List<string> GetRelativePaths()
        {
            List<object> relativePathList = GetDistinctValuesInColumn(DBTables.FileData, DatabaseColumn.RelativePath);
            List<string> allPaths = new List<string>();
            foreach (string relativePath in relativePathList.Cast<String>())
            {
                allPaths.Add(relativePath);
            }
            allPaths.Sort();
            return allPaths;
        }
        #endregion

        #region Get Distinct Values
        public List<object> GetDistinctValuesInColumn(string table, string columnName)
        {
            return Database.GetDistinctValuesInColumn(table, columnName);
        }

        // Return all distinct values from a column in the file table, used for autocompletion
        // Note that this returns distinct values only in the SELECTED files
        // PERFORMANCE - the issue here is that there may be too many distinct entries, which slows down autocompletion. This should thus restrict entries, perhaps by:
        // - check matching substrings before adding, to avoid having too many entries?
        // - only store the longest version of a string. But this would involve more work when adding entries, so likely not worth it.
        public Dictionary<string, string> GetDistinctValuesInSelectedFileTableColumn(string dataLabel, int minimumNumberOfRequiredCharacters)
        {
            Dictionary<string, string> distinctValues = new Dictionary<string, string>();
            foreach (ImageRow row in FileTable)
            {
                string value = row.GetValueDatabaseString(dataLabel);
                if (value.Length < minimumNumberOfRequiredCharacters)
                {
                    continue;
                }
                if (distinctValues.ContainsKey(value) == false)
                {
                    distinctValues.Add(value, string.Empty);
                }
            }
            return distinctValues;
        }

        // Get a list of RelativePath,File where that combination is duplicated in the database, i.e.,
        // a list of duplicate RelativePath,File(s).
        // Form: SELECT RelativePath, File FROM DataTable GROUP BY RelativePath, File HAVING COUNT(*) > 1
        public List<string> GetDistinctRelativePathFileCombinationsDuplicates()
        {
            List<string> listOfDuplicatePaths = new List<string>();
            string relativePathFile = DatabaseColumn.RelativePath + Sql.Comma + DatabaseColumn.File;
            string query = Sql.Select + relativePathFile
                + Sql.From + DBTables.FileData
                + Sql.GroupBy + relativePathFile + Sql.Having + Sql.CountStar + Sql.GreaterThan + "1";
            DataTable dataTable = Database.GetDataTableFromSelect(query);
            for (int i = 0; i < dataTable.Rows.Count; i++)
            {
                listOfDuplicatePaths.Add(Path.Combine((string)dataTable.Rows[i].ItemArray[0], (string)dataTable.Rows[i].ItemArray[1]));
            }
            return listOfDuplicatePaths;
        }
        #endregion

        #region Update Files
        /// <summary>
        /// Update a column value (identified by its key) in an existing row (identified by its ID) 
        /// By default, if the table parameter is not included, we use the TABLEDATA table
        /// </summary>
        public void UpdateFile(long fileID, string dataLabel, string value)
        {
            // update the data table
            ImageRow image = FileTable.Find(fileID);
            image.SetValueFromDatabaseString(dataLabel, value);

            // update the row in the database
            CreateBackupIfNeeded();

            ColumnTuplesWithWhere columnToUpdate = new ColumnTuplesWithWhere();
            columnToUpdate.Columns.Add(new ColumnTuple(dataLabel, value)); // Populate the data 
            columnToUpdate.SetWhere(fileID);
            Database.Update(DBTables.FileData, columnToUpdate);
        }

        // Set one property on all rows in the selected view to a given value
        public void UpdateFiles(ImageRow valueSource, DataEntryControl control)
        {
            // We update the the files using the database format for dates (Time is the same across all of them)
            if (control is DataEntryDateTimeCustom dateTimeCustom)
            {
                if (dateTimeCustom.ContentControl.Value != null)
                {
                    string dateTimeAsDatabaseString = DateTimeHandler.ToStringDatabaseDateTime((DateTime)dateTimeCustom.ContentControl.Value);
                    UpdateFiles(dateTimeAsDatabaseString, control.DataLabel, 0, CountAllCurrentlySelectedFiles - 1);
                }
            }
            else if (control is DataEntryDate date)
            {
                if (date.ContentControl.Value != null)
                {
                    string dateAsDatabaseString = DateTimeHandler.ToStringDatabaseDate((DateTime)date.ContentControl.Value);
                    UpdateFiles(dateAsDatabaseString, control.DataLabel, 0, CountAllCurrentlySelectedFiles - 1);
                }
            }
            // No need to do DataEntryTime as the database and display format are the same.
            else
            {
                UpdateFiles(valueSource, control.DataLabel);
            }
        }

        public void UpdateFiles(ImageRow valueSource, DataEntryControl control, int from, int to)
        {
            // We update the the files using the database format for dates (Time is the same across all of them)
            if (control is DataEntryDateTimeCustom dateTimeCustom)
            {
                if (dateTimeCustom.ContentControl.Value != null)
                {
                    string dateTimeAsDatabaseString = DateTimeHandler.ToStringDatabaseDateTime((DateTime)dateTimeCustom.ContentControl.Value);
                    UpdateFiles(dateTimeAsDatabaseString, control.DataLabel, from, to);
                }
            }
            else if (control is DataEntryDate date)
            {
                if (date.ContentControl.Value != null)
                {
                    string dateAsDatabaseString = DateTimeHandler.ToStringDatabaseDate((DateTime)date.ContentControl.Value);
                    UpdateFiles(dateAsDatabaseString, control.DataLabel, from, to);
                }
            }
            else if (control is DataEntryTime time)
            {
                if (time.ContentControl.Value != null)
                {
                    string timeString = DateTimeHandler.ToStringTime((DateTime)time.ContentControl.Value);
                    UpdateFiles(timeString, control.DataLabel, from, to);
                }
            }
            else
            {
                UpdateFiles(valueSource, control.DataLabel, from, to);
            }
        }

        public void UpdateFiles(ImageRow valueSource, string dataLabel)
        {
            UpdateFiles(valueSource, dataLabel, 0, CountAllCurrentlySelectedFiles - 1);
        }



        // Given a list of column/value pairs (the string,object) and the FILE name indicating a row, update it
        public void UpdateFiles(List<ColumnTuplesWithWhere> filesToUpdate)
        {
            CreateBackupIfNeeded();
            Database.Update(DBTables.FileData, filesToUpdate);
        }

        public void UpdateFiles(ColumnTuplesWithWhere filesToUpdate)
        {
            List<ColumnTuplesWithWhere> imagesToUpdateList = new List<ColumnTuplesWithWhere>
            {
                filesToUpdate
            };
            Database.Update(DBTables.FileData, imagesToUpdateList);
        }

        public void UpdateFiles(ColumnTuple columnToUpdate)
        {
            Database.Update(DBTables.FileData, columnToUpdate);
        }

        // Given a range of selected files, update the field identifed by dataLabel with the value in valueSource
        // Updates are applied to both the datatable (so the user sees the updates immediately) and the database
        public void UpdateFiles(ImageRow valueSource, string dataLabel, int fromIndex, int toIndex)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(valueSource, nameof(valueSource));

            if (fromIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fromIndex));
            }
            if (toIndex < fromIndex || toIndex > CountAllCurrentlySelectedFiles - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(toIndex));
            }

            string value = valueSource.GetValueDatabaseString(dataLabel);
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            for (int index = fromIndex; index <= toIndex; index++)
            {
                // update data table
                ImageRow image = FileTable[index];
                if (null == image)
                {
                    Debug.Print(
                        $"in FileDatabase.UpdateFiles v1: FileTable returned null as there is no index: {index}");
                    continue;
                }
                image.SetValueFromDatabaseString(dataLabel, value);

                // update database
                List<ColumnTuple> columnToUpdate = new List<ColumnTuple> { new ColumnTuple(dataLabel, value) };
                ColumnTuplesWithWhere imageUpdate = new ColumnTuplesWithWhere(columnToUpdate, image.ID);
                imagesToUpdate.Add(imageUpdate);
            }
            CreateBackupIfNeeded();
            Database.Update(DBTables.FileData, imagesToUpdate);
        }

        // Like above, but given a value update the field identified by the data label
        public void UpdateFiles(string value, string dataLabel, int fromIndex, int toIndex)
        {
            if (fromIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fromIndex));
            }
            if (toIndex < fromIndex || toIndex > CountAllCurrentlySelectedFiles - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(toIndex));
            }

            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            for (int index = fromIndex; index <= toIndex; index++)
            {
                // update data table
                ImageRow image = FileTable[index];
                if (null == image)
                {
                    Debug.Print(
                        $"in FileDatabase.UpdateFiles v1: FileTable returned null as there is no index: {index}");
                    continue;
                }
                image.SetValueFromDatabaseString(dataLabel, value);

                // update database
                List<ColumnTuple> columnToUpdate = new List<ColumnTuple> { new ColumnTuple(dataLabel, value) };
                ColumnTuplesWithWhere imageUpdate = new ColumnTuplesWithWhere(columnToUpdate, image.ID);
                imagesToUpdate.Add(imageUpdate);
            }
            CreateBackupIfNeeded();
            Database.Update(DBTables.FileData, imagesToUpdate);
        }


        // Similar to above
        // Given a list of selected files, update the field identifed by dataLabel with the value in valueSource
        // Updates are applied to both the datatable (so the user sees the updates immediately) and the database
        public void UpdateFiles(List<int> fileIndexes, string dataLabel, string value)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(fileIndexes, nameof(fileIndexes));

            if (fileIndexes.Count == 0)
            {
                return;
            }

            // string value = valueSource.GetValueDatabaseString(dataLabel);
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            foreach (int fileIndex in fileIndexes)
            {
                // update data table
                ImageRow image = FileTable[fileIndex];
                if (null == image)
                {
                    Debug.Print(
                        $"in FileDatabase.UpdateFiles v2: FileTable returned null as there is no index: {fileIndex}");
                    continue;
                }
                image.SetValueFromDatabaseString(dataLabel, value);

                // update database
                List<ColumnTuple> columnToUpdate = new List<ColumnTuple> { new ColumnTuple(dataLabel, value) };
                ColumnTuplesWithWhere imageUpdate = new ColumnTuplesWithWhere(columnToUpdate, image.ID);
                imagesToUpdate.Add(imageUpdate);
            }
            CreateBackupIfNeeded();
            Database.Update(DBTables.FileData, imagesToUpdate);
        }
        #endregion

        #region Update Syncing 
        public void UpdateSyncImageSetToDatabase()
        {
            // don't trigger backups on image set updates as none of the properties in the image set table is particularly important
            // For example, this avoids creating a backup when a custom selection is reverted to all when Timelapse exits.
            Database.Update(DBTables.ImageSet, ImageSet.CreateColumnTuplesWithWhereByID());
        }

        public void UpdateSyncMarkerToDatabase(MarkerRow marker)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(marker, nameof(marker));

            CreateBackupIfNeeded();
            Database.Update(DBTables.Markers, marker.CreateColumnTuplesWithWhereByID());
        }
        #endregion

        #region Update Markers
        // The id is the row to update, the datalabels are the labels of each control to updata, 
        // and the markers are the respective point lists for each of those labels
        // ReSharper disable once UnusedMember.Global
        public void UpdateMarkers(List<ColumnTuplesWithWhere> markersToUpdate)
        {
            // update markers in database
            CreateBackupIfNeeded();
            Database.Update(DBTables.Markers, markersToUpdate);

            // Refresh the markers data table
            RefreshMarkers();
        }
        #endregion

        #region Refresh various datatables (markers,detections, classifications)
        // Refresh the Markers DataTable
        public void RefreshMarkers()
        {
            MarkersLoadRowsFromDatabase();
        }

        // Refresh the Detections DataTable (sync and async)
        public void RefreshDetectionsDataTable()
        {
            // This query joins all Detections and DetectionsVideo into a single table. If there is no corresponding DetectionsVideo entry, frame rate / number will be null
            string query = $"{Sql.SelectStarFrom} {DBTables.Detections} {Sql.LeftJoin} {DBTables.DetectionsVideo} {Sql.Using} {Sql.OpenParenthesis} {DetectionColumns.DetectionID} {Sql.CloseParenthesis}";
            detectionDataTable = Database.GetDataTableFromSelect(query);
        }
        public async Task RefreshDetectionsDataTableAsync()
        {
            // This query joins all Detections and DetectionsVideo into a single table. If there is no corresponding DetectionsVideo entry, frame rate / number will be null
            string query = $"{Sql.SelectStarFrom} {DBTables.Detections} {Sql.LeftJoin} {DBTables.DetectionsVideo} {Sql.Using} {Sql.OpenParenthesis} {DetectionColumns.DetectionID} {Sql.CloseParenthesis}";
            detectionDataTable = await Database.GetDataTableFromSelectAsync(query);
        }
        #endregion

        #region Update File Dates and Times
        // Update all selected files with the given time adjustment
        public void UpdateAdjustedFileTimes(TimeSpan adjustment)
        {
            UpdateAdjustedFileTimes(adjustment, 0, CountAllCurrentlySelectedFiles - 1);
        }

        // Update all selected files between the start and end row with the given time adjustment
        public void UpdateAdjustedFileTimes(TimeSpan adjustment, int startRow, int endRow)
        {
            if (adjustment.Milliseconds != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(adjustment), "The current format of the time column does not support milliseconds.");
            }
            UpdateAdjustedFileTimes((fileName, fileIndex, count, imageTime) => imageTime + adjustment, startRow, endRow, CancellationToken.None);
        }

        // Given a time difference in ticks, update all the date/time field in the database
        // Note that it does NOT update the dataTable - this has to be done outside of this routine by regenerating the datatables with whatever selection is being used..
        public void UpdateAdjustedFileTimes(Func<string, int, int, DateTime, DateTime> adjustment, int startRow, int endRow, CancellationToken token)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(adjustment, nameof(adjustment));

            if (IsFileRowInRange(startRow) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(startRow));
            }
            if (IsFileRowInRange(endRow) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(endRow));
            }
            if (endRow < startRow)
            {
                throw new ArgumentOutOfRangeException(nameof(endRow), "endRow must be greater than or equal to startRow.");
            }
            if (CountAllCurrentlySelectedFiles == 0)
            {
                return;
            }

            // We now have an unselected temporary data table
            // Get the original value of each, and update each date by the corrected amount if possible
            List<ImageRow> filesToAdjust = new List<ImageRow>();
            int count = endRow - startRow + 1;
            int fileIndex = 0;
            for (int row = startRow; row <= endRow; ++row)
            {
                if (token.IsCancellationRequested)
                {
                    // A cancel was requested. Clear all pending changes and abort
                    return;
                }
                ImageRow image = FileTable[row];
                DateTime currentImageDateTime = image.DateTime;

                // adjust the date/time
                fileIndex++;
                DateTime newImageDateTime = adjustment.Invoke(image.File, fileIndex, count, currentImageDateTime);
                TimeSpan mostRecentAdjustment = newImageDateTime - currentImageDateTime;
                if (mostRecentAdjustment.Duration() < TimeSpan.FromSeconds(1))
                {
                    // Ignore changes if it results in less than a 1 second change, 
                    continue;
                }
                image.SetDateTime(newImageDateTime);
                filesToAdjust.Add(image);
            }

            if (token.IsCancellationRequested)
            {
                // Don't update the database, as a cancellation was requested.
                return;
            }

            // update the database with the new date/time values
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            foreach (ImageRow image in filesToAdjust)
            {
                imagesToUpdate.Add(image.GetDateTimeColumnTuples());
            }

            if (imagesToUpdate.Count > 0)
            {
                CreateBackupIfNeeded();
                Database.Update(DBTables.FileData, imagesToUpdate);
            }
        }

        // Update all the date fields between the start and end index by swapping the days and months.
        public void UpdateExchangeDayAndMonthInFileDates(int startRow, int endRow)
        {
            if (IsFileRowInRange(startRow) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(startRow));
            }
            if (IsFileRowInRange(endRow) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(endRow));
            }
            if (endRow < startRow)
            {
                throw new ArgumentOutOfRangeException(nameof(endRow), "endRow must be greater than or equal to startRow.");
            }
            if (CountAllCurrentlySelectedFiles == 0)
            {
                return;
            }

            // Get the original date value of each. If we can swap the date order, do so. 
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            for (int row = startRow; row <= endRow; row++)
            {
                ImageRow image = FileTable[row];
                DateTime originalDateTime = image.DateTime;

                if (DateTimeHandler.TrySwapDayMonth(originalDateTime, out DateTime reversedDateTime) == false)
                {
                    continue;
                }

                // Now update the actual database with the new date/time values stored in the temporary table
                image.SetDateTime(reversedDateTime);
                imagesToUpdate.Add(image.GetDateTimeColumnTuples());
            }

            if (imagesToUpdate.Count > 0)
            {
                CreateBackupIfNeeded();
                Database.Update(DBTables.FileData, imagesToUpdate);
            }
        }
        #endregion

        #region Update RelativePaths (used mostly by RelativePathEditor)
        // This method will rename all relative paths matching a prefix. The Query is different depending upon
        // whether its an interior node (i.e., a prefix matching any relative paths with a following path) or a leaf (i.e., no subfolders are under it) 
        // Form:Update DataTable
        // Interior nodes:
        //      Update DataTable SET RelativePath =
        //      'newPrefixPath' || Substr(RelativePath, Length(oldPrefixPath) + 1) where Instr (RelativePath, oldPrefixPath\) == 1
        // Leaf nodes:
        //      Update DataTable SET RelativePath = 'newPrefixPath' where RelativePath = 'oldPrefixPath'
        public void RelativePathReplacePrefix(string oldPrefixPath, string newPrefixPath, bool isInteriorNode)
        {
            string query;
            this.ResetAfterPossibleRelativePathChanges();
            if (isInteriorNode)
            {
                query = Sql.Update + DBTables.FileData
                                   + Sql.Set + DatabaseColumn.RelativePath + Sql.Equal
                                   + Sql.Quote(newPrefixPath) + Sql.Concatenate
                                   + Sql.Substr
                                   + Sql.OpenParenthesis
                                   + DatabaseColumn.RelativePath + Sql.Comma
                                   + Sql.Length + Sql.OpenParenthesis + Sql.Quote(oldPrefixPath) +
                                   Sql.CloseParenthesis + Sql.Plus + "1"
                                   + Sql.CloseParenthesis
                                   + Sql.Where
                                   + Sql.Instr
                                   + Sql.OpenParenthesis
                                   + DatabaseColumn.RelativePath + Sql.Comma
                                   + Sql.Quote(oldPrefixPath + '\\')
                                   + Sql.CloseParenthesis
                                   + Sql.BooleanEquals + "1";
            }
            else
            {
                query = Sql.Update + DBTables.FileData
                                   + Sql.Set + DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(newPrefixPath)
                                   + Sql.Where
                                   + DatabaseColumn.RelativePath + Sql.BooleanEquals + Sql.Quote(oldPrefixPath);
            }
            Database.ExecuteNonQuery(query);
        }
        #endregion

        #region Deletions
        // Delete the data (including markers associated with the images identified by the list of IDs.
        public void DeleteFilesAndMarkers(List<long> fileIDs)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(fileIDs, nameof(fileIDs));

            if (fileIDs.Count < 1)
            {
                // nothing to do
                return;
            }

            List<string> idClauses = new List<string>();
            foreach (long fileID in fileIDs)
            {
                idClauses.Add(DatabaseColumn.ID + " = " + fileID);
            }
            // Delete the data and markers associated with that image
            CreateBackupIfNeeded();
            Database.Delete(DBTables.FileData, idClauses);
            Database.Delete(DBTables.Markers, idClauses);
        }
        #endregion

        #region Schema retrieval
        public Dictionary<string, string> SchemaGetColumnsAndDefaultValues(string tableName)
        {
            return Database.SchemaGetColumnsAndDefaultValues(tableName);
        }

        // ReSharper disable once UnusedMember.Global
        public List<string> SchemaGetColumns(string tableName)
        {
            return Database.SchemaGetColumns(tableName);
        }
        #endregion

        #region Counts or Exists 1 of matching files
        // Return a total count of the currently selected files in the file table.
        public int CountAllCurrentlySelectedFiles => FileTable?.RowCount ?? 0;

        // Return the count of the files matching the fileSelection condition in the entire database
        // Form examples
        // - Select Count(*) FROM (Select * From Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id WHERE <some condition> GROUP BY Detections.Id HAVING  MAX  ( Detections.conf )  <= 0.9)
        // - Select Count(*) FROM (Select * From Classifications INNER JOIN DataTable ON DataTable.Id = Detections.Id  INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID WHERE DataTable.Person<>'true' 
        // AND Classifications.category = 6 GROUP BY Classifications.classificationID HAVING  MAX  (Classifications.conf ) BETWEEN 0.8 AND 1 
        public int CountAllFilesMatchingSelectionCondition(FileSelectionEnum fileSelection)
        {
            string query;

            // PART 1 of Query
            if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && CustomSelection.ShowMissingDetections)
            {
                // MISSING DETECTIONS
                // Create a query that returns a count of missing detections
                // Form: SELECT COUNT ( DataTable.Id ) FROM DataTable LEFT JOIN Detections ON DataTable.ID = Detections.Id WHERE Detections.Id IS NULL 
                query = SqlPhrase.SelectMissingDetections(SelectTypesEnum.Count); ;
            }
            else if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && CustomSelection.RecognitionSelections.UseRecognition && CustomSelection.RecognitionSelections.RecognitionType == RecognitionType.Detection)
            {
                // DETECTIONS 
                // Create a query that returns a count of detections matching some conditions (which can include classifications within a detection)
                // Form: SELECT COUNT  ( * )  FROM  (  SELECT * FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id
                query = SqlPhrase.SelectDetections(SelectTypesEnum.Count);
            }
            else if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && CustomSelection.RecognitionSelections.UseRecognition && CustomSelection.RecognitionSelections.RecognitionType == RecognitionType.Classification)
            {
                // CLASSIFICATIONS
                // Same form as Detections but with error checks
                if (null == this.detectionCategoriesDictionary)
                {
                    // Error
                    return -1;
                }
                query = SqlPhrase.SelectDetections(SelectTypesEnum.Count);
            }
            else
            {
                // STANDARD (NO DETECTIONS/CLASSIFICATIONS)
                // Create a query that returns a count that does not consider detections
                query = Sql.SelectCountStarFrom + DBTables.FileData;
            }

            // PART 2 of Query
            // Now add the Where conditions to the query.
            // If the selection is All, there is no where clause needed.
            if (fileSelection != FileSelectionEnum.All)
            {
                if (GlobalReferences.DetectionsExists)
                {
                    if (CustomSelection.ShowMissingDetections == false)
                    {
                        string where = CustomSelection.GetFilesWhere(); //this.GetFilesConditionalExpression(fileSelection);
                        if (!string.IsNullOrEmpty(where))
                        {
                            query += where;
                        }

                        if (fileSelection == FileSelectionEnum.Custom &&
                            CustomSelection.RecognitionSelections.UseRecognition) // && CustomSelection.RecognitionSelections.RecognitionType != RecognitionType.Empty)
                        {
                            // Add a close parenthesis if we are querying for detections
                            query += Sql.CloseParenthesis;
                        }
                    }
                    else
                    {
                        // Show missing recognitions is selected: the where clause should only include the data fields (i.e., no recognition conditions), if any
                        string where = CustomSelection.GetFilesWhere(true, true);
                        if (!string.IsNullOrEmpty(where))
                        {
                            query += $"{Sql.And} {where}";
                        }
                    }
                }
                else
                {
                    if (GlobalReferences.DetectionsExists == false || CustomSelection.ShowMissingDetections == false)
                    {
                        // Standard where 
                        string whereExpression = CustomSelection.GetFilesWhere();
                        if (string.IsNullOrEmpty(whereExpression) == false)
                        {
                            query += whereExpression;
                        }
                    }
                }
            }

            // EPISODES-related addition to query.
            // If Episodes  is turned on, then the Episode Note field contains values in the Episode format (e.g.) 25:1/8.
            // We construct a wrapper for counting  files where all files in an episode have at least one file matching the surrounded search condition 
            if (CustomSelection.EpisodeShowAllIfAnyMatch && CustomSelection.EpisodeNoteField != string.Empty
                && fileSelection == FileSelectionEnum.Custom)
            {
                // Remove from the front of the string
                query = query.Replace(Sql.SelectCountStarFrom, string.Empty);
                string frontWrapper = SqlPhrase.CountOrSelectFilesInEpisodeIfOneFileMatchesFrontWrapper(DBTables.FileData, CustomSelection.EpisodeNoteField, true);
                string backWrapper = Sql.CloseParenthesis;
                query = frontWrapper + query + backWrapper;
            }
            //Uncommment this to see the actual complete query
            Debug.Print("File Counts: " + query);
            //Debug.Print(XXX2++ + ":DetectionCounts");
            return Database.ScalarGetScalarFromSelectAsInt(query);
        }

        // Return true if even one file matches the fileSelection condition in the entire database
        // NOTE: Currently only used by 1 method to check if deleteflags exists. Check how well this works if other methods start using it.
        // NOTE: This method is somewhat similar to CountAllFilesMatchingSelectionCondition. They could be combined, but its easier for now to keep them separate
        // Form examples
        // -  No detections:  SELECT EXISTS (  SELECT 1  FROM DataTable WHERE  ( DeleteFlag='true' )  )  //
        // -  detections:     SELECT EXISTS (  SELECT 1  FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id WHERE  ( DataTable.DeleteFlag='true' )  GROUP BY Detections.Id HAVING  MAX  ( Detections.conf )  BETWEEN 0.8 AND 1 )
        // -  recognitions:   SELECT EXISTS (  SELECT 1  FROM  (  SELECT DISTINCT DataTable.* FROM Classifications INNER JOIN DataTable ON DataTable.Id = Detections.Id INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 
        //                    WHERE  ( DataTable.DeleteFlag='true' )  AND Classifications.category = 1 GROUP BY Classifications.classificationID HAVING  MAX  ( Classifications.conf )  BETWEEN 0.8 AND 1 )  ) :1
        public bool ExistsFilesMatchingSelectionCondition(FileSelectionEnum fileSelection)
        {
            bool skipWhere = false;
            string query = " SELECT EXISTS ( ";

            // PART 1 of Query
            if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && CustomSelection.ShowMissingDetections)
            {
                // MISSING DETECTIONS
                // Create a query that returns a count of missing detections
                // Form: SELECT COUNT ( DataTable.Id ) FROM DataTable LEFT JOIN Detections ON DataTable.ID = Detections.Id WHERE Detections.Id IS NULL 
                query += SqlPhrase.SelectMissingDetections(SelectTypesEnum.One);
                skipWhere = true;
            }
            else if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && CustomSelection.RecognitionSelections.UseRecognition
                     && (CustomSelection.RecognitionSelections.RecognitionType == RecognitionType.Detection || CustomSelection.RecognitionSelections.RecognitionType == RecognitionType.Classification))
            {
                // DETECTIONS AND CLASSIFICATIONS
                // Create a query that returns a count of detections matching some conditions
                // Form: SELECT COUNT  ( * )  FROM  (  SELECT * FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id
                query += SqlPhrase.SelectDetections(SelectTypesEnum.One);
            }
            else
            {
                // STANDARD (NO DETECTIONS/CLASSIFICATIONS)
                // Create a query that returns a count that does not consider detections
                query += Sql.SelectOne + Sql.From + DBTables.FileData;
            }

            // PART 2 of Query
            // Now add the Where conditions to the query
            if ((GlobalReferences.DetectionsExists && CustomSelection.ShowMissingDetections == false) || skipWhere == false)
            {
                string where = CustomSelection.GetFilesWhere(); //this.GetFilesConditionalExpression(fileSelection);
                if (!string.IsNullOrEmpty(where))
                {
                    query += where;
                }
            }
            query += Sql.CloseParenthesis;

            // Uncommment this to see the actual complete query
            // Debug.Print("File Exists: " + query + ":" + this.Database.ScalarGetScalarFromSelectAsInt(query).ToString() );
            return Database.ScalarGetScalarFromSelectAsInt(query) != 0;
        }

        #endregion

        #region Counts entries with the given relative path

        public int CountAllFilesMatchingRelativePath(string RelativePath)
        {
            string query = Sql.SelectCountStarFrom + DBTables.FileData + Sql.Where + DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(RelativePath);
            return Database.ScalarGetScalarFromSelectAsInt(query);
        }
        #endregion

        #region Exists matching files  
        // Return true if there is at least one file matching the fileSelection condition in the entire database
        // Form examples
        // - Select EXISTS  ( SELECT 1   FROM DataTable WHERE DeleteFlag='true')
        // -     SELECT EXISTS  ( SELECT 1  FROM DataTable WHERE  (RelativePath= 'Station1' OR RelativePath GLOB 'Station1\*') AND DeleteFlag = 'TRUE' COllate nocase)
        // -XXXX SELECT EXISTS  ( SELECT 1  FROM DataTable WHERE  (  ( RelativePath='Station1\\Deployment2' OR RelativePath GLOB 'Station1\\Deployment2\\*' )  AND DeleteFlag='true' )) 
        // The performance of this query depends upon how many rows in the table has to be searched
        // before the first exists appears. If there are no matching rows, the performance is more or
        // less equivalent to COUNT as it has to go through every row. 
        public bool ExistsRowThatMatchesSelectionForAllFilesOrConstrainedRelativePathFiles(FileSelectionEnum fileSelection)
        {
            // Create a term that will be used, if needed, to account for a constrained relative path
            // Term form is: ( RelativePath='relpathValue' OR DataTable.RelativePath GLOB 'relpathValue\*' )
            string constrainToRelativePathTerm = GlobalReferences.MainWindow.Arguments.ConstrainToRelativePath
                    ? CustomSelection.RelativePathGlobToIncludeSubfolders(DatabaseColumn.RelativePath, GlobalReferences.MainWindow.Arguments.RelativePath)
                    : string.Empty;
            string selectionTerm;
            // Common query folderPrefix: SELECT EXISTS  ( SELECT 1  FROM DataTable WHERE 
            string query = Sql.SelectExists + Sql.OpenParenthesis + Sql.SelectOne + Sql.From + DBTables.FileData + Sql.Where;


            // Count the number of deleteFlags
            if (fileSelection == FileSelectionEnum.MarkedForDeletion)
            {
                // Term form is: DeleteFlag = 'TRUE' COllate nocase
                selectionTerm = DatabaseColumn.DeleteFlag + Sql.Equal + Sql.Quote("true") + Sql.CollateNocase;
            }
            else
            {
                // Shouldn't get here, as this should only be used with MarkedForDeletion, Ok, or Dark
                // so essentially a noop
                return false;
            }
            if (string.IsNullOrWhiteSpace(constrainToRelativePathTerm))
            {
                // Form after this:  SELECT EXISTS  (  SELECT 1  FROM DataTable WHERE   DeleteFlag = 'TRUE' COllate nocase )
                query += selectionTerm + Sql.CloseParenthesis;
            }
            else
            {
                // Form after this: SELECT EXISTS  ( SELECT 1  FROM DataTable WHERE  (RelativePath= 'Station1' OR RelativePath GLOB 'Station1\*') AND DeleteFlag = 'TRUE' COllate nocase)
                query += constrainToRelativePathTerm + Sql.And + selectionTerm + Sql.CloseParenthesis;
            }

            // Debug.Print("ExistsRowThatMatchesExactSelection: " + query);
            return Database.ScalarBoolFromOneOrZero(query);
        }
        #endregion

        #region Find: By Filename 
        // Find by file name, forwards and backwards with wrapping
        public int FindByFileName(int currentRow, bool isForward, string filename)
        {
            int rowIndex;

            if (isForward)
            {
                // Find forwards with wrapping
                rowIndex = FindByFileNameForwards(currentRow + 1, CountAllCurrentlySelectedFiles, filename);
                return rowIndex == -1 ? FindByFileNameForwards(0, currentRow - 1, filename) : rowIndex;
            }

            // Find backwards  with wrapping
            rowIndex = FindByFileNameBackwards(currentRow - 1, 0, filename);
            return rowIndex == -1 ? FindByFileNameBackwards(CountAllCurrentlySelectedFiles, currentRow + 1, filename) : rowIndex;
        }

        // Helper for FindByFileName
        private int FindByFileNameForwards(int from, int to, string filename)
        {
            for (int rowIndex = from; rowIndex <= to; rowIndex++)
            {
                if (FileRowContainsFileName(rowIndex, filename) >= 0)
                {
                    return rowIndex;
                }
            }
            return -1;
        }

        // Helper for FindByFileName
        private int FindByFileNameBackwards(int from, int downto, string filename)
        {
            for (int rowIndex = from; rowIndex >= downto; rowIndex--)
            {
                if (FileRowContainsFileName(rowIndex, filename) >= 0)
                {
                    return rowIndex;
                }
            }
            return -1;
        }

        // Helper for FindByFileName
        private int FileRowContainsFileName(int rowIndex, string filename)
        {
            CultureInfo culture = new CultureInfo("en");
            if (IsFileRowInRange(rowIndex) == false)
            {
                return -1;
            }
            return culture.CompareInfo.IndexOf(FileTable[rowIndex].File, filename, CompareOptions.IgnoreCase);
        }
        #endregion

        #region Find first image that begins with this RelativePath prefix or that matches this relative path
        public int FindFirstImageWithRootRelativePath(string relativePathPrefix)
        {
            int index = -1;
            foreach (ImageRow row in this.FileTable)
            {
                index++;
                if (row.RelativePath == relativePathPrefix || row.RelativePath.StartsWith(relativePathPrefix + Path.DirectorySeparatorChar))
                {
                    return index;
                }
            }

            return -1;
        }
        #endregion

        #region Find: Displayable
        // Convenience routine for checking to see if the image in the given row is displayable (i.e., not corrupted or missing)
        public bool IsFileDisplayable(int rowIndex)
        {
            if (IsFileRowInRange(rowIndex) == false)
            {
                return false;
            }
            return FileTable[rowIndex].IsDisplayable(RootPathToImages);
        }

        // Find the next displayable file at or after the provided row in the current image set.
        // If there is no next displayable file, then find the closest previous file before the provided row that is displayable.
        // ReSharper disable once UnusedMember.Global
        public int GetCurrentOrNextDisplayableFile(int startIndex)
        {
            int countAllCurrentlySelectedFiles = CountAllCurrentlySelectedFiles;
            for (int index = startIndex; index < countAllCurrentlySelectedFiles; index++)
            {
                if (IsFileDisplayable(index))
                {
                    return index;
                }
            }
            for (int index = startIndex - 1; index >= 0; index--)
            {
                if (IsFileDisplayable(index))
                {
                    return index;
                }
            }
            return -1;
        }
        #endregion

        #region Find: By Row Index
        // Check if index is within the file row range
        public bool IsFileRowInRange(int imageRowIndex)
        {
            return (imageRowIndex >= 0) && (imageRowIndex < CountAllCurrentlySelectedFiles);
        }

        // Find the image whose ID is closest to the provided ID  in the current image set
        // If the ID does not exist, then return the image row whose ID is just greater than the provided one. 
        // However, if there is no greater ID (i.e., we are at the end) return the last row. 
        public int FindClosestImageRow(long fileID)
        {
            int countAllCurrentlySelectedFiles = CountAllCurrentlySelectedFiles;
            for (int rowIndex = 0, maxCount = countAllCurrentlySelectedFiles; rowIndex < maxCount; ++rowIndex)
            {
                if (FileTable[rowIndex].ID >= fileID)
                {
                    return rowIndex;
                }
            }
            return countAllCurrentlySelectedFiles - 1;
        }

        // Find the file whose ID is closest to the provided ID in the current image set
        // If the ID does not exist, then return the file whose ID is just greater than the provided one. 
        // However, if there is no greater ID (i.e., we are at the end) return the last row. 
        public int GetFileOrNextFileIndex(long fileID)
        {
            // try primary key lookup first as typically the requested ID will be present in the data table
            // (ideally the caller could use the ImageRow found directly, but this doesn't compose with index based navigation)
            ImageRow file = FileTable.Find(fileID);
            if (file != null)
            {
                return FileTable.IndexOf(file);
            }

            // when sorted by ID ascending so an inexact binary search works
            // Sorting by datetime is usually identical to ID sorting in single camera image sets 
            // But no datetime seed is available if direct ID lookup fails.  Thw API can be reworked to provide a datetime hint
            // if this proves too troublesome.
            int firstIndex = 0;
            int lastIndex = CountAllCurrentlySelectedFiles - 1;
            int countAllCurrentlySelectedFiles = CountAllCurrentlySelectedFiles;
            while (firstIndex <= lastIndex)
            {
                int midpointIndex = (firstIndex + lastIndex) / 2;
                file = FileTable[midpointIndex];
                long midpointID = file.ID;

                if (fileID > midpointID)
                {
                    // look at higher index partition next
                    firstIndex = midpointIndex + 1;
                }
                else if (fileID < midpointID)
                {
                    // look at lower index partition next
                    lastIndex = midpointIndex - 1;
                }
                else
                {
                    // found the ID closest to fileID
                    return midpointIndex;
                }
            }

            // all IDs in the selection are smaller than fileID
            if (firstIndex >= countAllCurrentlySelectedFiles)
            {
                return countAllCurrentlySelectedFiles - 1;
            }

            // all IDs in the selection are larger than fileID
            return firstIndex;
        }
        #endregion

        #region Binding the data grid
        // Bind the data grid to an event, using boundGrid and the onFileDataTableRowChanged event 

        // Convenience form that knows which datagrid to use
        public void BindToDataGrid()
        {
            if (FileTable == null)
            {
                return;
            }
            FileTable.BindDataGrid(boundGrid, onFileDataTableRowChanged);
        }

        // Generalized form of the above
        public void BindToDataGrid(DataGrid dataGrid, DataRowChangeEventHandler onRowChanged)
        {
            if (FileTable == null)
            {
                return;
            }
            boundGrid = dataGrid;
            onFileDataTableRowChanged = onRowChanged;
            FileTable.BindDataGrid(dataGrid, onRowChanged);
        }
        #endregion

        #region Index creation and dropping
        public void IndexCreateForDetectionsIfNotExists()
        {
            Database.IndexCreateIfNotExists(DatabaseValues.IndexID, DBTables.Detections, DatabaseColumn.ID);
            // Even though DetectionsVideo has foreign key relation to Detections, we create an index as its not done automatically.
            Database.IndexCreateIfNotExists(DatabaseValues.IndexDetectionVideoID, DBTables.DetectionsVideo, DetectionColumns.DetectionID);
        }

        // static version of the above
        public static void IndexCreateForDetectionsIfNotExists(SQLiteWrapper database)
        {
            database.IndexCreateIfNotExists(DatabaseValues.IndexID, DBTables.Detections, DatabaseColumn.ID);
            // Even though DetectionsVideo has foreign key relation to Detections, we create an index as its not done automatically.
            database.IndexCreateIfNotExists(DatabaseValues.IndexDetectionVideoID, DBTables.DetectionsVideo, DetectionColumns.DetectionID);
        }

        public void IndexCreateForFileAndRelativePathIfNotExists()
        {
            // If even one of the indexes doesn't exist, they would all have to be created
            if (0 == Database.ScalarGetScalarFromSelectAsInt(Sql.SelectCountFromSqliteMasterWhereTypeEqualIndexAndNameEquals + Sql.Quote("IndexFile")))
            {
                List<Tuple<string, string, string>> tuples = new List<Tuple<string, string, string>>
                {
                    new Tuple<string, string, string>(DatabaseValues.IndexRelativePath, DBTables.FileData, DatabaseColumn.RelativePath),
                    new Tuple<string, string, string>(DatabaseValues.IndexFile, DBTables.FileData, DatabaseColumn.File),
                    new Tuple<string, string, string>(DatabaseValues.IndexRelativePathFile, DBTables.FileData, DatabaseColumn.RelativePath + "," + DatabaseColumn.File)
                };
                Database.IndexCreateMultipleIfNotExists(tuples);
            }
        }

        public void IndexDropForFileAndRelativePathIfExists()
        {
            Database.IndexDrop(DatabaseValues.IndexRelativePath);
            Database.IndexDrop(DatabaseValues.IndexFile);
            Database.IndexDrop("IndexRelativePathFile");
        }
        #endregion

        #region File retrieval and manipulation
        public void RenameFileDatabase(string newFileName)
        {
            if (File.Exists(Path.Combine(RootPathToDatabase, FileName)))
            {
                // SAULXXX Should really check for failure, as TryMove will return true/false
                FilesFolders.TryMoveFileIfExists(
                     Path.Combine(RootPathToDatabase, FileName),
                     Path.Combine(RootPathToDatabase, newFileName));  // Change the file name to the new file name
                FileName = newFileName; // Store the file name
                Database = new SQLiteWrapper(Path.Combine(RootPathToDatabase, newFileName));          // Recreate the database connecction
            }
        }

        // Insert one or more rows into a table
        // ReSharper disable once UnusedMember.Local
        private void InsertRows(string table, List<List<ColumnTuple>> insertionStatements)
        {
            CreateBackupIfNeeded();
            Database.Insert(table, insertionStatements);
        }
        #endregion

        #region Markers
        /// <summary>
        /// Get all markers for the specified file.
        /// This is done by getting the marker list associated with all counters representing the current row
        /// It will have a MarkerCounter for each control, even if there may be no metatags in it
        /// </summary>
        public List<MarkersForCounter> MarkersGetMarkersForCurrentFile(long fileID)
        {
            List<MarkersForCounter> markersForAllCounters = new List<MarkersForCounter>();

            // Get the current row number of the id in the marker table
            MarkerRow markersForImage = Markers.Find(fileID);
            if (markersForImage == null)
            {
                return markersForAllCounters;
            }

            // Iterate through the columns, where we create a MarkersForCounter for each control and add it to the MarkersForCounter list
            foreach (string dataLabel in markersForImage.DataLabels)
            {
                // create a marker for each point and add it to the counter 
                MarkersForCounter markersForCounter = new MarkersForCounter(dataLabel);
                string pointList;
                try
                {
                    pointList = markersForImage[dataLabel];
                }
                catch (Exception exception)
                {
                    TracePrint.PrintMessage($"Read of marker failed for dataLabel '{dataLabel}'. {exception}");
                    pointList = string.Empty;
                }
                markersForCounter.ParsePointList(pointList);
                markersForAllCounters.Add(markersForCounter);
            }
            return markersForAllCounters;
        }

        // Get all markers from the Markers table and load it into the data table
        private void MarkersLoadRowsFromDatabase()
        {
            string markersQuery = Sql.SelectStarFrom + DBTables.Markers;
            Markers = new DataTableBackedList<MarkerRow>(Database.GetDataTableFromSelect(markersQuery), row => new MarkerRow(row));
        }

        // Add an empty new row to the marker list if it isnt there. Return true if we added it, otherwise false 
        public bool MarkersTryInsertNewMarkerRow(long imageID)
        {
            if (Markers.Find(imageID) != null)
            {
                // There should already be a row for this, so don't create one
                return false;
            }
            List<ColumnTuple> columns = new List<ColumnTuple>
            {
                new ColumnTuple(DatabaseColumn.ID, imageID.ToString())
            };

            // Set each marker value to its default
            foreach (ControlRow controlRow in Controls)
            {
                if (controlRow.Type == Control.Counter)
                {
                    columns.Add(new ColumnTuple(controlRow.DataLabel, DatabaseValues.DefaultMarkerValue));
                }
            }

            List<List<ColumnTuple>> insertionStatements = new List<List<ColumnTuple>>
            {
                columns
            };
            Database.Insert(DBTables.Markers, insertionStatements);

            // PERFORMANCE: This is inefficient, as it rereads the entire Markers table from the database
            MarkersLoadRowsFromDatabase(); // Update the markers list to include this new row

            return true;
        }

        /// <summary>
        /// Set the list of marker points on the current row in the marker table. 
        /// </summary>
        public void MarkersUpdateMarkerRow(long imageID, MarkersForCounter markersForCounter)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(markersForCounter, nameof(markersForCounter));

            // Find the current row number
            MarkerRow marker = Markers.Find(imageID);
            if (marker == null)
            {
                TracePrint.PrintMessage($"Image ID {imageID} missing in markers table.");
                return;
            }

            // Update the database and datatable
            // Note that I repeated the null check here, as for some reason it was still coming up as a CA1062 warning
            ThrowIf.IsNullArgument(markersForCounter, nameof(markersForCounter));
            marker[markersForCounter.DataLabel] = markersForCounter.GetPointList();
            UpdateSyncMarkerToDatabase(marker);
        }

        /// <summary>
        /// Set the list of marker points on the current row in the marker table. 
        /// </summary>
        public void MarkersRemoveMarkerRow(long imageID)
        {
            // Find the current row number
            MarkerRow marker = Markers.Find(imageID);
            if (marker == null)
            {
                TracePrint.PrintMessage($"Image ID {imageID} missing in markers table.");
                return;
            }
            Markers.RemoveAt(Markers.IndexOf(marker));
            // Update the database and datatable
            // Note that I repeated the null check here, as for some reason it was still coming up as a CA1062 warning
            List<string> whereClauses = new List<string>
            {
               DatabaseColumn.ID + Sql.Equal + imageID
            };
            Database.Delete(DBTables.Markers, whereClauses);
        }
        #endregion

        #region FolderMetadata Schema Creation
        public void CreateFolderMetadataTablesIfNeeded()
        {
            foreach (MetadataInfoRow row in MetadataInfo)
            {
                string tableName = MetadataComposeTableNameFromLevel(row.Level);
                if (Database.TableExists(tableName))
                {
                    // A table representing that level already exists, so no need to do anything
                    continue;
                }
                // A table representing that level does not exist, so we can skip it
                if (false == MetadataControlsByLevel.ContainsKey(row.Level))
                {
                    // TracePrint.PrintMessage($"Key {row.ID} is not present in MetadataControlsByLevel, but its not.");
                    continue;
                }
                TryGenerateFolderMetadataTable(tableName, MetadataControlsByLevel[row.Level]);
            }
        }

        private void TryGenerateFolderMetadataTable(string tableName, DataTableBackedList<MetadataControlRow> metadataControlRows)
        {
            // We don't create empty tables
            // TODO: Is this so?
            if (0 == metadataControlRows.RowCount)
            {
                return;
            }

            // Create the table from the template
            // These columns are common to all FolderMetadataTables
            List<SchemaColumnDefinition> schemaColumnDefinitions = new List<SchemaColumnDefinition>
            {
                new SchemaColumnDefinition(DatabaseColumn.ID, Sql.CreationStringPrimaryKey),  // It begins with the ID integer primary key
                new SchemaColumnDefinition(DatabaseColumn.FolderDataPath, Sql.Text)  // The metdataFolderPath indicated the metadata level's portion of the relative path
            };


            foreach (MetadataControlRow controlRow in metadataControlRows)
            {
                // Create a column  as defined by each MetadataControlRow, but invoked using the base CommonControlRow class 
                schemaColumnDefinitions.Add(CreateFileDataColumnDefinition(controlRow));
            }
            Database.CreateTable(tableName, schemaColumnDefinitions);
        }
        #endregion

        #region Metadata data structure manipulation
        // Get all Metadata for the various metadata tables, by level, and load it into the data table
        public void MetadataTableLoadRowsFromDatabase()
        {
            MetadataTablesByLevel = new Dictionary<int, DataTableBackedList<MetadataRow>>();
            if (null == MetadataInfo)
            {
                // Nothing to load, as there is no MetadataInfo table. 
                return;
            }
            foreach (MetadataInfoRow metadataInfoRow in MetadataInfo)
            {
                // Populate this level's metadata struture from its corresponding table (if the table exists)
                string tableName = MetadataComposeTableNameFromLevel(metadataInfoRow.Level);
                if (Database.TableExists(tableName))
                {
                    string metadataQuery = Sql.SelectStarFrom + tableName;
                    MetadataTablesByLevel.Add(metadataInfoRow.Level, new DataTableBackedList<MetadataRow>(Database.GetDataTableFromSelect(metadataQuery), row => new MetadataRow(row)));
                }
            }
        }

        public void MetadataTableLoadRowsFromDatabase(int level)
        {
            // Clear that level
            if (MetadataTablesByLevel.ContainsKey(level))
            {
                MetadataTablesByLevel.Remove(level);
            }
            // Populate this level's metadata struture from its corresponding table (if the table exists)
            string tableName = MetadataComposeTableNameFromLevel(level);
            if (Database.TableExists(tableName))
            {
                string metadataQuery = Sql.SelectStarFrom + tableName;
                MetadataTablesByLevel.Add(level, new DataTableBackedList<MetadataRow>(Database.GetDataTableFromSelect(metadataQuery), row => new MetadataRow(row)));
            }
        }

        public void MetadataTablesAndDatabaseUpsertRow(int level, string relativePathToCurrentImage, Dictionary<string, string> dataLabelsAndValues)
        {
            string tableName = MetadataComposeTableNameFromLevel(level);
            string query = $"{Sql.SelectStarFrom} {tableName} {Sql.Where} {DatabaseColumn.FolderDataPath} {Sql.Equal} {Sql.Quote(relativePathToCurrentImage)}";
            DataTable dataTable = Database.GetDataTableFromSelect(query);

            List<ColumnTuple> columnTupleList = new List<ColumnTuple>();
            foreach (KeyValuePair<string, string> kvp in dataLabelsAndValues)
            {
                columnTupleList.Add(new ColumnTuple(kvp.Key, kvp.Value));
            }


            if (dataTable.Rows.Count == 0)
            {
                // The row doesn't exist, so insert it
                // Create a list matching fields we need to update
                List<List<ColumnTuple>> newTableTuples = new List<List<ColumnTuple>>
                {
                    columnTupleList
                };
                Database.Insert(tableName, newTableTuples);

                // Now add it to the metadataTable
                MetadataTableLoadRowsFromDatabase(level);
                return;
            }

            // If we get to here, then the row exists. So we just need to update it instead
            ColumnTuplesWithWhere ctww = new ColumnTuplesWithWhere(columnTupleList, (long)dataTable.Rows[0][DatabaseColumn.ID]);
            Database.Update(tableName, ctww);
        }

        public void MetadataUpdateFolderDataPath(int level, string oldpath, string newPath)
        {
            string tableName = MetadataComposeTableNameFromLevel(level);
            if (false == Database.TableExists(tableName))
            {
                return;
            }
            Dictionary<string, string> currentAndNewValuePairs = new Dictionary<string, string>
            {
                { oldpath, newPath }
            };
            Database.UpdateParticularColumnValuesWithNewValues(tableName, DatabaseColumn.FolderDataPath, currentAndNewValuePairs);

        }

        public Dictionary<string, string> MetadataGetDataLabels(int level)
        {
            return MetadataGetDataLabels(level, string.Empty);
        }
        public Dictionary<string, string> MetadataGetDataLabels(int level, string orderByString)
        {
            Dictionary<string, string> dataLabelsAndTypes = new Dictionary<string, string>();
            string query = string.IsNullOrWhiteSpace(orderByString)
                ? $"{Sql.Select} {Control.DataLabel} {Sql.Comma} {Control.Type}{Sql.From} {DBTables.MetadataTemplate} {Sql.Where} {Control.Level} {Sql.Equal} {level}"
                : $"{Sql.Select} {Control.DataLabel} {Sql.Comma} {Control.Type}{Sql.From} {DBTables.MetadataTemplate} {Sql.Where} {Control.Level} {Sql.Equal} {level} {Sql.OrderBy} {orderByString}";

            DataTable datatable = Database.GetDataTableFromSelect(query);
            for (int i = 0; i < datatable.Rows.Count; i++)
            {
                // Dictionary entry is datalabel, type
                dataLabelsAndTypes.Add((string)datatable.Rows[i][0], (string)datatable.Rows[i][1]);
            }
            return dataLabelsAndTypes;
        }

        // Return a dictionary comprised of datalabel, type pairs
        public Dictionary<string, string> MetadataGetDataLabelsInSpreadsheetOrder(int level)
        {
            return MetadataGetDataLabels(level, Control.SpreadsheetOrder);
        }

        // Return a dictionary comprised of datalabel, type pairs but only for rows with its Export flag on
        public Dictionary<string, string> MetadataGetDataLabelsInSpreadsheetOrderForExport(int level)
        {
            Dictionary<string, string> allDataLabelsAndTypes = MetadataGetDataLabelsInSpreadsheetOrder(level);
            Dictionary<string, string> dataLabelsAndTypesForExport = new Dictionary<string, string>();
            foreach (string key in allDataLabelsAndTypes.Keys)
            {
                MetadataControlRow row = GetControlFromMetadataControls(key, level);
                if (row.ExportToCSV)
                {
                    // We only include rows that are flagged for export
                    dataLabelsAndTypesForExport.Add(key, allDataLabelsAndTypes[key]);
                }
            }
            return dataLabelsAndTypesForExport;
        }


        public MetadataRow MetadataTablesGetRow(int level, string relativePathPart)
        {
            DataTableBackedList<MetadataRow> metadataRows = MetadataTablesByLevel[level];
            if (null == metadataRows || metadataRows.RowCount == 0)
            {
                return null;
            }

            foreach (MetadataRow row in metadataRows)
            {
                if (row[DatabaseColumn.FolderDataPath] == relativePathPart)
                {
                    return row;
                }
            }
            return null;
        }
        // Return whether a metadata level exists in the MetadataTables data structure
        public bool MetadataTablesIsLevelPresent(int level)
        {
            return MetadataTablesByLevel.ContainsKey(level);
        }

        // Return whether a metadata level both exists and is populated in the MetadataTables data structure
        public bool MetadataTablesIsLevelPopulated(int level)
        {
            return MetadataTablesIsLevelPresent(level) && MetadataTablesByLevel[level].RowCount > 0;
        }

        public bool MetadataTablesIsLevelAndRelativePathPresent(int level, string relativePathPart)
        {
            if (false == MetadataTablesIsLevelPresent(level))
            {
                return false;
            }
            DataTableBackedList<MetadataRow> metadataRows = MetadataTablesByLevel[level];
            if (null == metadataRows || metadataRows.RowCount == 0)
            {
                return false;
            }

            foreach (MetadataRow row in metadataRows)
            {
                if (row[DatabaseColumn.FolderDataPath] == relativePathPart)
                {
                    return true;
                }
            }
            return false;
        }

        public static string MetadataComposeTableNameFromLevel(int level)
        {
            return $"Level{level}";
        }

        // Return true iff our folder level structure appears to be a CamtrapDP standard.
        // We may want to put in more checks, e.g., to see if some of the required CamtrapDP fields are in a level
        public bool MetadataTablesIsCamtrapDPStandard()
        {
            // Check if the template was created using the CamtrapDP standard
            if (string.IsNullOrEmpty(ImageSet.Standard) || ImageSet.Standard != Constant.Standards.CamtrapDPStandard)
            {
                return false;
            }

            // Just in case, do a few other checks to see if it (sort of) conforms to the CamtrapDP standard.
            // We could make this more robust by checking to see if all the required fields are present, but that is something to do later.
            if (null == MetadataInfo || MetadataInfo.RowCount != 2)
            {
                // Needs to be a metadata table with two levels
                return false;
            }

            bool dataPackagePresent = false;
            bool deploymentPresent = false;

            foreach (MetadataInfoRow row in MetadataInfo)
            {
                if (row.Level == 1 && row.Alias == CamtrapDPConstants.ResourceLevels.DataPackage)
                {
                    dataPackagePresent = true;
                }
                else if (row.Level == 2 && row.Alias == CamtrapDPConstants.ResourceLevels.Deployments)
                {
                    deploymentPresent = true;
                }
            }
            return dataPackagePresent && deploymentPresent;
        }
        #endregion

        #region ImageSet manipulation
        private void ImageSetLoadFromDatabase()
        {
            string imageSetQuery = Sql.SelectStarFrom + DBTables.ImageSet + Sql.Where + DatabaseColumn.ID + " = " + DatabaseValues.ImageSetRowID;
            DataTable imageSetTable = Database.GetDataTableFromSelect(imageSetQuery);
            ImageSet = new ImageSetRow(imageSetTable.Rows[0]);
            imageSetTable.Dispose();
        }
        #endregion

        #region Detections - Populate the Database (with progress bar)
        // To help determine periodic updates to the progress bar 
        private DateTime lastRefreshDateTime = DateTime.Now;
        public bool ReadyToRefresh()
        {
            TimeSpan intervalFromLastRefresh = DateTime.Now - lastRefreshDateTime;
            if (intervalFromLastRefresh > ThrottleValues.ProgressBarRefreshInterval)
            {
                lastRefreshDateTime = DateTime.Now;
                return true;
            }
            return false;
        }

        private void PStream_BytesRead(object sender, ProgressStreamReportEventArgs args)
        {
            Progress<ProgressBarArguments> progressHandler = new Progress<ProgressBarArguments>(value =>
            {
                UpdateProgressBar(GlobalReferences.BusyCancelIndicator, value.PercentDone, value.Message, value.IsCancelEnabled, value.IsIndeterminate);
            });
            IProgress<ProgressBarArguments> progress = progressHandler;

            long current = args.StreamPosition;
            long total = args.StreamLength;
            double p = current / ((double)total);
            if (ReadyToRefresh())
            {
                // Update the progress bar
                progress.Report(new ProgressBarArguments((int)(100 * p), "Reading the recognition file...", true, false));
                Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
            }
        }
        public static void UpdateProgressBar(BusyCancelIndicator busyCancelIndicator, int percent, string message, bool isCancelEnabled, bool isIndeterminate)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Code to run on the GUI thread.
                // Check the arguments for null 
                ThrowIf.IsNullArgument(busyCancelIndicator, nameof(busyCancelIndicator));

                // Set it as a progressive or indeterminate bar
                busyCancelIndicator.IsIndeterminate = isIndeterminate;

                // Set the progress bar position (only visible if determinate)
                busyCancelIndicator.Percent = percent;

                // Update the text message
                busyCancelIndicator.Message = message;

                // Update the cancel button to reflect the cancelEnabled argument
                busyCancelIndicator.CancelButtonIsEnabled = isCancelEnabled;
                busyCancelIndicator.CancelButtonText = isCancelEnabled ? "Cancel" : "Updating the database ...";
            });
        }
        public void InsertDetection(List<List<ColumnTuple>> detectionInsertionStatements)
        {
            Database.Insert(DBTables.Detections, detectionInsertionStatements);
        }

        public void InsertDetectionsVideo(List<List<ColumnTuple>> detectionsVideoInsertionStatements)
        {
            Database.Insert(DBTables.DetectionsVideo, detectionsVideoInsertionStatements);
        }

        // Try to read the recognition data from the Json file into the Recognizer structure,
        // where we trim detections/classifications below a certain confidence level and only keep the best classification
        // A progress bar is displayed
        // Success: returns a filled in Recognizer structure
        // Failure: returns null
        public async Task<Recognizer> JsonDeserializeRecognizerFileAsync(string path)
        {
            if (File.Exists(path) == false)
            {
                return null;
            }

            Recognizer jsonRecognizer = null;
            using (ProgressStream ps = new ProgressStream(File.OpenRead(path), GlobalReferences.CancelTokenSource))
            {
                ps.BytesRead += PStream_BytesRead;

                using (TextReader sr = new StreamReader(ps))
                {
                    TextReader capturedSr = sr;
                    await Task.Run(() =>
                    {
                        try
                        {
                            using (JsonReader reader = new JsonTextReader(capturedSr))
                            {
                                JsonSerializer serializer = new JsonSerializer();
                                jsonRecognizer = serializer.Deserialize<Recognizer>(reader);

                                // trim detections/ classifications below a certain confidence level and only keep the best classification
                                jsonRecognizer = RecognizerTrimAndSortRecognitionsAsNeeded(jsonRecognizer);
                            }
                        }

                        catch (Exception e)
                        {
                            if (e is TaskCanceledException)
                            {
                                GlobalReferences.CancelTokenSource = new CancellationTokenSource();
                                jsonRecognizer = new Recognizer(); // signal cancel by returning a non-null recognizer where info is null
                            }
                            else
                            {
                                jsonRecognizer = null;
                            }
                        }
                    }).ConfigureAwait(true);
                }
            }

            return jsonRecognizer;
        }

        // Trim low-confidence detections and classifications from the recognizer data structure,
        // Trim all but the highest-confidence classification
        // Sort the detections by frame number if its a video (which helps performance when displaying video bounding boxes)
        public Recognizer RecognizerTrimAndSortRecognitionsAsNeeded(Recognizer jsonRecognizer)
        {
            if (jsonRecognizer?.images == null)
            {
                // essentially a no-op
                return jsonRecognizer;
            }
            // Set confidence thresholds, where we will delete detections or classifications less than their respective threshold.
            // This is because detections below that value are rarely useful.
            // We use the default MinimumDetectionValue, unless the conservative_detection_threshold suggests a higher value. 
            // Classifciations are somewhat similar, although they only report a typical_classification_threshold
            double? minimumDetectionConfidence =
               null != jsonRecognizer.info?.detector_metadata?.conservative_detection_threshold &&
                       jsonRecognizer.info.detector_metadata.conservative_detection_threshold / 2.5 > Constant.RecognizerValues.MinimumDetectionValue
                ? jsonRecognizer.info.detector_metadata.conservative_detection_threshold / 2.5
                : Constant.RecognizerValues.MinimumDetectionValue;

            double? minimumClassificationConfidence =
                null != jsonRecognizer.info?.classifier_metadata?.typical_classification_threshold &&
                        jsonRecognizer.info.classifier_metadata.typical_classification_threshold / 2.5 > Constant.RecognizerValues.MinimumClassificationValue
                    ? jsonRecognizer.info.classifier_metadata.typical_classification_threshold / 2.5
                    : Constant.RecognizerValues.MinimumClassificationValue;

            foreach (image image in jsonRecognizer.images)
            {
                if (image.detections == null)
                {
                    continue;
                }
                for (int i = image.detections.Count - 1; i >= 0; i--)
                {
                    // Round the bounding box values upwards three decimal places, as we really don't need massive precision.
                    // Also, make the bounding box slightly larger so its edges don't overlap the detected item
                    if (image.detections[i].bbox != null)
                    {
                        for (int j = 0; j < image.detections[i].bbox.Length; j++)
                        {
                            image.detections[i].bbox[j] += j <= 1 ? -0.002f : 0.004f; // the 2nd two terms are offset, so we need to double the amount
                            image.detections[i].bbox[j] = image.detections[i].bbox[j] < 0 ? 0 : Math.Round(image.detections[i].bbox[j], Constant.RecognizerValues.ConfidenceDecimalPlaces);
                        }
                    }

                    // Round the confidence to three decimal places, as we really don't need massive precision. I suspect even that is excessive
                    image.detections[i].conf = (float)Math.Round(image.detections[i].conf, Constant.RecognizerValues.ConfidenceDecimalPlaces);

                    // Delete detections whose confidence is lower than the confidence threshold
                    // This is done to get rid of detections that have little value.
                    if (image.detections[i].conf < minimumDetectionConfidence)
                    {
                        image.detections.RemoveAt(i);
                        continue;
                    }

                    // For each detection, find the highest confidence classification (if any)
                    detection detection = image.detections[i];
                    object[] highestConfidenceClassification = null;
                    double highestConfidenceFound = -1;
                    for (int j = detection.classifications.Count - 1; j >= 0; j--)
                    {
                        // get the confidence of that classification
                        double conf = double.Parse(detection.classifications[j][1].ToString());

                        if (conf < minimumClassificationConfidence)
                        {
                            // Skip this classification if it is below the minimum confidence threshold
                            continue;
                        }

                        if (conf > highestConfidenceFound)
                        {
                            // We have a new highest confidence classification
                            highestConfidenceFound = conf;
                            highestConfidenceClassification = detection.classifications[j];
                        }
                    }

                    // If we have a highest confidence classification, replace the classification list with just that classification
                    // As we do that, also round its confidence to three decimal places, as we really don't need massive precision.
                    detection.classifications.Clear();
                    if (null != highestConfidenceClassification)
                    {
                        highestConfidenceClassification[1] = Math.Round(highestConfidenceFound, Constant.RecognizerValues.ConfidenceDecimalPlaces);
                        detection.classifications.Add(highestConfidenceClassification);
                    }
                    // At this point there should only be a single classification, with
                    // other lower ranking classifications filtered out
                }
                // Sort the detections by frame number, if any
                // This will later write the detections into the database in sorted order
                // I don't actually know if that will preserve the order when they are read back in, but if it does it may 
                // provide a slight performance advantage for showing bounding boxes on videos
                image.detections.Sort((x, y) => x.frame_number.CompareTo(y.frame_number));
            }

            return jsonRecognizer;
        }
        public async Task<RecognizerImportResultEnum> PopulateRecognitionTablesFromRecognizerAsync(Recognizer jsonRecognizer, List<string> foldersInDBListButNotInJSon, List<string> foldersInJsonButNotInDB, List<string> foldersInBoth, bool tryMerge, IProgress<ProgressBarArguments> progress, CancellationTokenSource cancelTokenSource)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(foldersInDBListButNotInJSon, nameof(foldersInDBListButNotInJSon));
            ThrowIf.IsNullArgument(foldersInJsonButNotInDB, nameof(foldersInJsonButNotInDB));
            ThrowIf.IsNullArgument(foldersInBoth, nameof(foldersInBoth));

            RecognizerImportResultEnum result = await Task.Run(() =>
            {
                try
                {
                    progress.Report(new ProgressBarArguments(0, "Examining database recognitions...", true, true));
                    Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then

                    // Fill in the jsonRecognizer info structure as needed to ensure it is filled in with reasonable values
                    PopulateRecognizerInfoWithDefaultValuesAsNeeded(jsonRecognizer.info);

                    // flag indicating if the detections database already exists

                    bool clearDBRecognitionData = true;

                    // the starting index to be used for inserts using the DetectionID
                    long dbStartingDetectionID = 1;

                    // Resetting these tables to null will force reading the new values into them
                    // TODO: Put this somewhere else in case the user aborts the update!
                    // Also Update this comment: Resetting these tables to null will force reading the new values into them
                    detectionDataTable = null; // to force repopulating the data structure if it already exists.
                    detectionCategoriesDictionary = null;

                    // If we were told to tryMerge and detections exist, we merge the json file detections with the db detections. If so, we also have to do some error checking and possibly updates
                    // for the detection and classification categories and the info structure
                    bool mergeDetections = tryMerge && DetectionsExists();
                    if (mergeDetections)
                    {
                        // Generate several dictionaries reflecting the contents of several detection tables as currently held in the database
                        Dictionary<string, string> dbDetectionCategories = new Dictionary<string, string>();
                        Dictionary<string, string> dbClassificationCategories = new Dictionary<string, string>();
                        Dictionary<string, string> dbClassificationDescriptions = new Dictionary<string, string>();
                        Dictionary<string, object> dbInfoDictionary = new Dictionary<string, object>();
                        RecognitionUtilities.GenerateRecognitionDictionariesFromDB(Database, dbInfoDictionary, dbDetectionCategories, dbClassificationCategories, dbClassificationDescriptions);

                        //
                        // INFO structures
                        //

                        // Step 1. Generate a new info structure that is a best effort combination of the db and json info structure,
                        //         and then update the jsonRecognizer to match that. Note the we do it even if no update is really needed, as its lightweight
                        Dictionary<string, object> newInfoDict = RecognitionUtilities.GenerateBestRecognitionInfoFromTwoInfos(dbInfoDictionary, jsonRecognizer.info);
                        jsonRecognizer.info.detector = (string)newInfoDict[InfoColumns.Detector];
                        jsonRecognizer.info.detector_metadata.megadetector_version = (string)newInfoDict[InfoColumns.DetectorVersion];
                        jsonRecognizer.info.detection_completion_time = (string)newInfoDict[InfoColumns.DetectionCompletionTime];
                        jsonRecognizer.info.classifier = (string)newInfoDict[InfoColumns.Classifier];
                        jsonRecognizer.info.classification_completion_time = (string)newInfoDict[InfoColumns.ClassificationCompletionTime];
                        jsonRecognizer.info.detector_metadata.typical_detection_threshold = (float)newInfoDict[InfoColumns.TypicalDetectionThreshold];
                        jsonRecognizer.info.detector_metadata.conservative_detection_threshold = (float)newInfoDict[InfoColumns.ConservativeDetectionThreshold];
                        jsonRecognizer.info.classifier_metadata.typical_classification_threshold = (float)newInfoDict[InfoColumns.TypicalClassificationThreshold];


                        if (cancelTokenSource.Token.IsCancellationRequested)
                        {
                            return RecognizerImportResultEnum.Cancelled;
                        }


                        // Step 2.DETECTIONS categories: Merge the DB and Json detection categories if they are compatable
                        if (dbDetectionCategories.ContainsKey("0"))
                        {
                            // Remove the 0: Empty key/value pair, as that is artificially generated by timelapse and is not in the JSON
                            dbDetectionCategories.Remove("0");
                        }

                        // Get a Dictionary that indicates if we need to remap the json detection category [key] to a dbDetectionCategory [value]
                        if (RemapAndReplaceCategoryNumbersIfNeeded(dbDetectionCategories, jsonRecognizer.detection_categories, 
                                out Dictionary<string, string> remappedCategoryDict, out Dictionary<string, string> detectionCategoryLookupMappingDict))
                        {
                            // 1st: Replace the json detection category numbers with the new mapping
                            jsonRecognizer.detection_categories = remappedCategoryDict;

                            // 2nd: remap the json detections numbers to the same updated numbers
                            foreach (KeyValuePair<string, string> kvp in detectionCategoryLookupMappingDict)
                            {
                                jsonRecognizer.images.ForEach(image =>
                                {
                                    var matchedDetection = image.detections.FirstOrDefault(detection => detection.category == kvp.Key);
                                    if (null != matchedDetection)
                                    {
                                        matchedDetection.category = kvp.Value;
                                    }
                                });
                            }
                        }


                        // 3rd. Merge the DB and Json detection categories
                        if (Dictionaries.MergeDictionaries(dbDetectionCategories, jsonRecognizer.detection_categories, out Dictionary<string, string> mergedDetectionCategories))
                        {
                            // Debug.Print("merged succeeded for detection categories");
                            jsonRecognizer.detection_categories = new Dictionary<string, string>(mergedDetectionCategories);
                        }
                        else
                        {
                            // Debug.Print("merged failed for detection categories");
                            return RecognizerImportResultEnum.IncompatibleDetectionCategories;
                        }


                        // Step 3. CLASSIFICATION categories: Merge the DB and Json classificaton categories if they are compatable
                       
                        // Get a Dictionary that indicates if we need to remap the json classification category [key] to a dbClassificationCategory [value]
                        if (RemapAndReplaceCategoryNumbersIfNeeded(dbClassificationCategories, jsonRecognizer.classification_categories,
                                out Dictionary<string, string> remappedClassificationCategoryDict, out Dictionary<string,string>classificationCategoryLookupMappingDict))
                        {
                            // 1st: Replace the classification_categories with the new mapping
                            jsonRecognizer.classification_categories = remappedClassificationCategoryDict;

                            // 2nd: Update the classification_category_descriptions (if it exists) to those new numbers as well 
                            if (null != jsonRecognizer.classification_category_descriptions)
                            {
                                Dictionary<string, string> newClassification_category_descriptions = new Dictionary<string, string>();
                                foreach (KeyValuePair<string, string> kvp in jsonRecognizer.classification_category_descriptions)
                                {
                                    // remapped: generate a new item with the new key
                                    newClassification_category_descriptions.Add(
                                        classificationCategoryLookupMappingDict.TryGetValue(kvp.Key, out var newCategoryNumber) 
                                            ? newCategoryNumber 
                                            : kvp.Key, kvp.Value);
                                }

                                jsonRecognizer.classification_category_descriptions = newClassification_category_descriptions;
                            }

                            // 3rd: remap the actual json classification numbers to the new updated category numbers if needed
                            foreach (image image in jsonRecognizer.images)
                            {
                                foreach (detection detection in image.detections)
                                {
                                    foreach (object[] classification in detection.classifications)
                                    {
                                        if (classificationCategoryLookupMappingDict.TryGetValue((string)classification[0], out string newCategoryNumber))
                                        {
                                            classification[0] = newCategoryNumber;
                                        }
                                    }
                                }
                            }
                        }

                        // 4th. Merge the DB and Json clasification categories if they are compatable
                        // Check if the new classfication categories are the same or at least a subset of the old ones.
                        // If they are, then we can just use the existing DB categories as they will apply to the new categories.
                        // Note that this check is jsut here for safety, as the classificaiton categories should always be mergable.
                        if (Dictionaries.MergeDictionaries(dbClassificationCategories, jsonRecognizer.classification_categories, out Dictionary<string, string> mergedClassificationCategories))
                        {
                            // Debug.Print("merged succeeded for classification categories");
                            jsonRecognizer.classification_categories = new Dictionary<string, string>(mergedClassificationCategories);
                        }
                        else
                        {
                            // Should not happen
                            return RecognizerImportResultEnum.IncompatibleClassificationCategories;
                        }

                        // 5th. Merge the DB and Json classification_category_descriptions if they are compatible
                        if (Dictionaries.MergeDictionaries(dbClassificationDescriptions, jsonRecognizer.classification_category_descriptions, out Dictionary<string, string> mergedClassificationCategoryDescriptions))
                        {
                            // Debug.Print("merged succeeded for classification categories");
                            jsonRecognizer.classification_category_descriptions = new Dictionary<string, string>(mergedClassificationCategoryDescriptions);
                        }
                        else
                        {
                            // Should not happen
                            return RecognizerImportResultEnum.IncompatibleClassificationCategories;
                        }

                        clearDBRecognitionData = false; // just to make it more readable

                        progress.Report(new ProgressBarArguments(0, "Retrieving recognitions from the database. Please wait...", true, true)); if (cancelTokenSource.Token.IsCancellationRequested)
                        {
                            return RecognizerImportResultEnum.Cancelled;
                        }
                        Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and the
                        DataTable dbDetectionTable = Database.GetDataTableFromSelect(
                                    Sql.Select + DatabaseColumn.File + Sql.Comma
                                    + DatabaseColumn.RelativePath + Sql.Comma
                                    + DBTables.Detections + ".*"
                                    + Sql.From + DBTables.Detections
                                    + Sql.InnerJoin + DBTables.FileData + Sql.On
                                    + DBTables.FileData + Sql.Dot + DatabaseColumn.ID
                                    + Sql.Equal
                                    + DBTables.Detections + Sql.Dot + DatabaseColumn.ID);
                        if (cancelTokenSource.Token.IsCancellationRequested)
                        {
                            return RecognizerImportResultEnum.Cancelled;
                        }

                        // As we will be inserting records, get the max DetectionID, and add 1 to it. This will be the starting detectionID for insertions

                        long i = 0;
                        long count = dbDetectionTable.Rows.Count;
                        foreach (DataRow dr in dbDetectionTable.Rows)
                        {
                            dbStartingDetectionID = Math.Max(Convert.ToInt64(dr["detectionID"]), dbStartingDetectionID);
                            if (i % 10000 == 0)
                            {
                                if (cancelTokenSource.Token.IsCancellationRequested)
                                {
                                    return RecognizerImportResultEnum.Cancelled;
                                }
                                int percent = Convert.ToInt32(i * 100.0 / count);
                                progress.Report(new ProgressBarArguments(percent,
                                    $"Examining your existing recognitions ({i:N2}/{count:N2})...", true, false)); Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and the
                            }
                            i++;
                        }
                        dbStartingDetectionID++;

                        // Foreach  detection, check if it exists in the database detection table.
                        // If it does, delete all references to that file (via the ID) in the database
                        progress.Report(new ProgressBarArguments(0, "Comparing recognitions. Please wait...", true, true));
                        Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                        if (cancelTokenSource.Token.IsCancellationRequested)
                        {
                            return RecognizerImportResultEnum.Cancelled;
                        }
                        List<string> queries = new List<string>();

                        foreach (image image in jsonRecognizer.images)
                        {
                            // check whether the image file in the json exists in the recognizer table.
                            string file = Path.GetFileName(image.file);
                            string relativePath = Path.GetDirectoryName(image.file);

                            DataRow[] rows = dbDetectionTable.Select(DatabaseColumn.File + Sql.Equal + Sql.Quote(file) + Sql.And + DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(relativePath));
                            if (rows.Length > 0)
                            {
                                // As the file exists in the detections database, delete all instances of its entry via its ID.
                                // We only need to retrieve the ID in the first row, as all others will be the same for that file. 
                                // Note that:
                                // - new entries for that file will be created later from the json
                                // - other existing entries in the DB will remain as is
                                DataRow row = rows[0];
                                // create an query where we delete detections with the file's ID from the database
                                // Form: Delete From Detections Where ID = 'the id of that file'
                                string query = Sql.DeleteFrom + DBTables.Detections + Sql.Where + DatabaseColumn.ID + Sql.Equal + row[DatabaseColumn.ID];
                                queries.Add(query);
                            }
                        }

                        if (queries.Count > 0)
                        {
                            // If the index wasn't created previously, make sure its there as otherwise its painfully slow.
                            IndexCreateForDetectionsIfNotExists();
                            // Delete these detections and classifications
                            Database.ExecuteNonQueryWrappedInBeginEnd(queries, progress, "Removing unneeded recognitions. Please wait...", 500);
                        }
                        // At this point, we have deleted the detections and classifications from those images that are both in the
                        // db and the json, which means were are ready to replace them. 
                    }

                    // At this point the db no longer contains detections for images referenced in the json file

                    // PERFORMANCE This check is likely somewhat slow. Check it on large detection files / dbs 
                    if (CompareRecognizerAndDBFolders(jsonRecognizer, foldersInDBListButNotInJSon, foldersInJsonButNotInDB, foldersInBoth) == false)
                    {
                        // No folders in the detections match folders in the databases. Abort without doing anything.
                        return RecognizerImportResultEnum.Failure;
                    }

                    // Prepare the various detection tables. 
                    RecognitionDatabases.PrepareRecognitionTablesAndColumns(Database, DetectionsExists(), clearDBRecognitionData);

                    // PERFORMANCE This method does two things:
                    // - it walks through the jsonRecognizer data structure to construct sql insertion statements
                    // - it invokes the actual insertion in the database.
                    // Both steps are very slow with a very large JSON of detections that matches folders of images.
                    // (e.g., 225 seconds for 2,000,000 images and their detections). Note that I batch insert 50,000 statements at a time. 

                    // Populate the detection tables, which includes it own progress bar indicator
                    RecognitionDatabases.PopulateTables(jsonRecognizer, this, Database, string.Empty, dbStartingDetectionID, progress, 1000);
                    // DetectionExists needs to be primed if it is to save its DetectionExists state
                    DetectionsExists(true);

                    // The above may have altered the two category dictionaries, so lets update them
                    this.detectionCategoriesDictionary = null;
                    CreateDetectionCategoriesDictionaryIfNeeded();
                    this.classificationCategoriesDictionary = null;
                    this.classificationDescriptionsDictionary = null;
                    CreateClassificationCategoriesDictionaryIfNeeded();
                    return RecognizerImportResultEnum.Success;
                }
                catch
                {
                    return RecognizerImportResultEnum.Failure;
                }
            }).ConfigureAwait(true);
            return result;
        }

        //Remap and replace CategoryNumbers
        // - The values (category names) of dict2 are compared to dict1.
        // - If they are the same, then we check if the category number is the same
        // - if they differ,
        //    return a dictionary (dict2) that remaps the category number of dict2  <Key> to the correct category number of dict1 <Value>
        //    also return a dictionary (dictNewMapping) that maps the old numbers to the new nubmers e.g., 2,6 means 2 is now remapped to 6
        public static bool RemapAndReplaceCategoryNumbersIfNeeded(Dictionary<string, string> dict1, Dictionary<string, string> dict2, out Dictionary<string, string> dict2Remapped, out Dictionary<string,string> dict1To2lookupMapping)
        {
            dict2Remapped = new Dictionary<string, string>();
            dict1To2lookupMapping = new Dictionary<string, string>();

            if (dict1 == null || dict1.Count == 0 || dict2 == null || dict2.Count == 0)
            {
                // At least one of the dictionaries is null or empty
                // so no mapping needed
                return false;
            }

            // Get the maximum category number in dict1, in case we have to remap an unseen category from dict2 
            int maxCategoryNumber = -1;
            foreach (KeyValuePair<string, string> kvp in dict1)
            {
                if (Int32.TryParse(kvp.Key, out int keyAsInt) & keyAsInt > maxCategoryNumber)
                {
                    maxCategoryNumber = keyAsInt;
                }
            }

            maxCategoryNumber++;

            Dictionary<string, string> dict1Flipped;
            Dictionary<string, string> dict2Flipped;
            try
            {
                // Flip the keys and values, as its just easier to work with
                dict1Flipped = dict1.ToDictionary(x => x.Value, x => x.Key);
                dict2Flipped = dict2.ToDictionary(x => x.Value, x => x.Key);
            }
            catch (Exception)
            {
                // Just in case there is a duplicate value in the dictionary
                return false;
            }

            foreach (KeyValuePair<string, string> kvp in dict2Flipped)
            {
                // Check and remap if needed how dict2's category numbers maps to dict 1's category number
                if (dict1Flipped.TryGetValue(kvp.Key, out var dict1CategoryNumber))
                {
                    // use dict1's category number which may be the same or different than dict2's category number
                    dict2Remapped.Add(dict1CategoryNumber, kvp.Key);

                    if (dict1CategoryNumber != kvp.Value)
                    {
                        // If it differs, then its a remapped number
                        dict1To2lookupMapping.Add(kvp.Value, dict1CategoryNumber);
                    }
                }
                else
                {
                    // The category label exists in dict1, so just use dict1's category number
                    // This remaps the number if it is different
                    // generate a new dict2 category number as it doesn't exist in dict1
                    dict2Remapped.Add(maxCategoryNumber.ToString(), kvp.Key);
                    dict1To2lookupMapping.Add(kvp.Value, maxCategoryNumber.ToString());
                    maxCategoryNumber++;
                }
            }
            return dict1To2lookupMapping.Count != 0;
        }
        #endregion

        #region Update Json with default values as needed
        // Update the jsonRecognizer info table as needed to ensure it is filled in with reasonable values
        private static void PopulateRecognizerInfoWithDefaultValuesAsNeeded(info info)
        {
            // If there is no info field in the json file, create a new structure
            // which will eventually be filled in with various default values.
            if (info == null)
            {
                info = new info();
            }

            // Set the jsonRecognizer to the MD version based upon the contents of the read-in
            // value for it (which is just the jsonRecognizer's file name). That file name value gives a 
            // reasonable hint as to what jsonRecognizer is currently in use.
            if (info.detector == null)
            {
                // just to insert a reasonable value into this, just in case
                info.detector = RecognizerValues.MDVersionUnknown;
            }

            if (info.detector_metadata == null)
            {
                // If its not set, this will fill it with reasonable default values,
                // e.g., its likely MD4 with the MD4 defaults, as later versions of MD
                // should fill this field in.
                info.detector_metadata = new detector_metadata();
            }
            else
            {
                // check for null fields or empty fields in this structure, setting them to defaults if needed
                if (string.IsNullOrWhiteSpace(info.detector_metadata.megadetector_version))
                {
                    info.detector_metadata.megadetector_version = RecognizerValues.MDVersionUnknown;
                }
                if (info.detector_metadata.typical_detection_threshold == null)
                {
                    info.detector_metadata.typical_detection_threshold = RecognizerValues.DefaultTypicalDetectionThresholdIfUnknown;
                }
                if (info.detector_metadata.conservative_detection_threshold == null)
                {
                    info.detector_metadata.conservative_detection_threshold = RecognizerValues.DefaultConservativeDetectionThresholdIfUnknown;
                }
            }

            if (info.classifier_metadata == null)
            {
                info.classifier_metadata = new classifier_metadata();
            }
            else
            {
                if (info.classifier_metadata.typical_classification_threshold == null)
                {
                    // If its not set, this will fill it with reasonable default values,
                    // e.g., its likely MD4 with the MD4 defaults, as later versions of MD
                    // should fill this field in.
                }
            }

        }
        #endregion

        #region Detections
        // Return true if there is at least one match between a jsonRecognizer folder and a DB folder
        // Return a list of folder paths missing in the DB but present in the jsonRecognizer file
        private bool CompareRecognizerAndDBFolders(Recognizer recognizer, List<string> foldersInDBListButNotInJSon, List<string> foldersInJsonButNotInDB, List<string> foldersInBoth)
        {
            if (recognizer.images.Count <= 0)
            {
                // No point continuing if there are no jsonRecognizer entries
                return false;
            }

            // Get all distinct folders in the database
            // This operation could b somewhat slow, but ...
            List<string> FoldersInDBList = GetDistinctValuesInColumn(DBTables.FileData, DatabaseColumn.RelativePath).Select(i => i.ToString()).ToList();
            if (FoldersInDBList.Count == 0)
            {
                // No point continuing if there are no folders in the database (i.e., no images)
                return false;
            }

            // Get all distinct folders in the Recognizer 
            // We add a closing slash onto the imageFilePath to terminate any matches
            // e.g., A/B  would also match A/Buzz, which we don't want. But A/B/ won't match that.
            SortedSet<string> foldersInRecognizerList = new SortedSet<string>();
            foreach (image image in recognizer.images)
            {
                string folderpath = Path.GetDirectoryName(image.file);
                if (folderpath == null)
                {
                    // Null if its a root folder e.g. C:\\
                    TracePrint.NullException(nameof(folderpath));
                    return false;
                }
                if (!string.IsNullOrEmpty(folderpath))
                {
                    folderpath += "\\";
                }
                foldersInRecognizerList.Add(folderpath);
            }

            // Compare each folder in the DB against the folders in the jsonRecognizer );
            foreach (string originalFolderDB in FoldersInDBList)
            {
                // Add a closing slash to the folderDB for the same reasons described above
                string modifiedFolderDB = string.Empty;
                if (!string.IsNullOrEmpty(originalFolderDB))
                {
                    modifiedFolderDB = originalFolderDB + "\\";
                }

                if (foldersInRecognizerList.Contains(modifiedFolderDB))
                {
                    // this folder path is in both the jsonRecognizer file and the image set
                    foldersInBoth.Add(modifiedFolderDB);
                }
                else
                {
                    foldersInDBListButNotInJSon.Add(string.IsNullOrEmpty(originalFolderDB)
                        ? "<root folder>"       // An empty strng is the root folder, so make sure we add it
                        : originalFolderDB);    // This folder is in the image set but NOT in the jsonRecognizer
                }
            }
            List<string> tempList = foldersInRecognizerList.Except(foldersInBoth).ToList();
            foreach (string s in tempList)
            {
                foldersInJsonButNotInDB.Add(s);
            }
            // if there is at least one folder in both, it means that we have some recognition data that we can import.
            return foldersInBoth.Count > 0;
        }

        // Get the detections associated with the current file, if any
        // As part of this, create a DetectionTable in memory that mirrors the database table
        public DataRow[] GetDetectionsFromFileID(long fileID)
        {
            if (detectionDataTable == null)
            {
                // PERFORMANCE 0 or more detections can be associated with every image. THus we should expect the number of detections could easily be two or three times the 
                // number of images. With very large databases, retrieving the datatable of detections can be very slow (and can consume significant memory). 
                // While this operation is only done once per image set session, it is still expensive. I suppose I could get it from the database on the fly, but 
                // its important to show detection data (including bounding boxes) as rapidly as possible, such as when a user is quickly scrolling through images.
                // So I am not clear on how to optimize this (although I suspect a thread running in the background when Timelapse is loaded could perhaps do this)
                RefreshDetectionsDataTable();
            }
            // Retrieve the detection from the in-memory datatable.
            // Note that because IDs are in the database as a string, we convert it
            // PERFORMANCE: This takes a bit of time, not much... but could be improved. Not sure if there is an index automatically built on it. If not, do so.
            if (detectionDataTable == null)
            {
                // Shouldn't happen as the above should reset it
                TracePrint.NullException(nameof(detectionDataTable));
                return null;
            }
            return detectionDataTable.Select(DatabaseColumn.ID + Sql.Equal + fileID);
        }

        // Get the detections associated with the current file, if any
        // As part of the, create a DetectionTable in memory that mirrors the database table
        public async Task<DataRow[]> GetDetectionsFromFileIDAsync(long fileID)
        {
            if (detectionDataTable == null)
            {
                // PERFORMANCE 0 or more detections can be associated with every image. THus we should expect the number of detections could easily be two or three times the 
                // number of images. With very large databases, retrieving the datatable of detections can be very slow (and can consume significant memory). 
                // While this operation is only done once per image set session, it is still expensive. I suppose I could get it from the database on the fly, but 
                // its important to show detection data (including bounding boxes) as rapidly as possible, such as when a user is quickly scrolling through images.
                // So I am not clear on how to optimize this (although I suspect a thread running in the background when Timelapse is loaded could perhaps do this)
                await RefreshDetectionsDataTableAsync();
            }
            // Retrieve the detection from the in-memory datatable.
            // Note that because IDs are in the database as a string, we convert it
            // PERFORMANCE: This takes a bit of time, not much... but could be improved. Not sure if there is an index automatically built on it. If not, do so.
            if (detectionDataTable == null)
            {
                // Shouldn't happen as the above should reset it
                TracePrint.NullException(nameof(detectionDataTable));
                return null;
            }
            return await Task.Run(() => detectionDataTable.Select(DatabaseColumn.ID + Sql.Equal + fileID));
        }

        // Return the label that matches the detection category 
        public string GetDetectionLabelFromCategory(string category)
        {
            CreateDetectionCategoriesDictionaryIfNeeded();
            return detectionCategoriesDictionary.TryGetValue(category, out string value) ? value : string.Empty;

        }

        // Get the TypicalDetectionThreshold from the Detection Info table. 
        // If we cannot, return the default value.
        public float GetTypicalDetectionThreshold()
        {
            float? x = null;
            try
            {
                if (Database.TableExists(DBTables.Info) && Database.SchemaIsColumnInTable(DBTables.Info, InfoColumns.TypicalDetectionThreshold))
                {
                    x = Database.ScalarGetFloatValue(DBTables.Info, InfoColumns.TypicalDetectionThreshold);
                }
                return x ?? RecognizerValues.DefaultTypicalDetectionThresholdIfUnknown;
            }
            catch
            {
                return RecognizerValues.DefaultTypicalDetectionThresholdIfUnknown;
            }
        }

        // Get the GetTypicalClassificationThreshold from the Detection Info table. 
        // If we cannot, return the default value
        public float GetTypicalClassificationThreshold()
        {
            float? x = null;
            try
            {
                if (Database.TableExists(DBTables.Info) && Database.SchemaIsColumnInTable(DBTables.Info, InfoColumns.TypicalClassificationThreshold))
                {
                    x = Database.ScalarGetFloatValue(DBTables.Info, InfoColumns.TypicalClassificationThreshold);
                }
                return x ?? RecognizerValues.DefaultTypicalClassificationThresholdIfUnknown;
            }
            catch
            {
                return RecognizerValues.DefaultTypicalClassificationThresholdIfUnknown;
            }
        }

        // Get the ConservativeDetectionThreshold from the Detection Info table. 
        // If we cannot, return the default value
        public float GetConservativeDetectionThreshold()
        {
            float? x = null;
            try
            {
                if (Database.TableExists(DBTables.Info) && Database.SchemaIsColumnInTable(DBTables.Info, InfoColumns.ConservativeDetectionThreshold))
                {
                    x = Database.ScalarGetFloatValue(DBTables.Info, InfoColumns.ConservativeDetectionThreshold);
                }
                return x ?? RecognizerValues.DefaultConservativeDetectionThresholdIfUnknown;
            }
            catch
            {
                return RecognizerValues.DefaultConservativeDetectionThresholdIfUnknown;
            }
        }

        public void CreateDetectionCategoriesDictionaryIfNeeded()
        {
            // Null means we have never tried to create the dictionary. Try to do so.
            if (detectionCategoriesDictionary == null)
            {
                detectionCategoriesDictionary = new Dictionary<string, string>();
                try
                {
                    if (this.DoesTableExist(Constant.DBTables.DetectionCategories) == false)
                    {
                        return;
                    }
                    using (DataTable dataTable = Database.GetDataTableFromSelect(Sql.SelectStarFrom + DBTables.DetectionCategories))
                    {
                        int dataTableRowCount = dataTable.Rows.Count;
                        for (int i = 0; i < dataTableRowCount; i++)
                        {
                            DataRow row = dataTable.Rows[i];
                            detectionCategoriesDictionary.Add((string)row[DetectionCategoriesColumns.Category], (string)row[DetectionCategoriesColumns.Label]);
                        }
                    }
                }
                catch
                {
                    // Should never really get here, but just in case.
                }
            }
        }

        // Create the detection category dictionary to mirror the detection table
        public string GetDetectionCategoryFromLabel(string label)
        {
            try
            {
                CreateDetectionCategoriesDictionaryIfNeeded();
                // A lookup dictionary should now exists, so just return the category value.
                string myKey = detectionCategoriesDictionary.FirstOrDefault(x => x.Value == label).Key;
                return myKey ?? string.Empty;
            }
            catch
            {
                // Should never really get here, but just in case.
                return string.Empty;
            }
        }

        public List<string> GetDetectionLabels()
        {
            List<string> labels = new List<string>();
            CreateDetectionCategoriesDictionaryIfNeeded();
            foreach (KeyValuePair<string, string> entry in detectionCategoriesDictionary)
            {
                labels.Add(entry.Value);
            }
            return labels;
        }

        // Create the classification category dictionary to mirror the detection table
        public void CreateClassificationCategoriesDictionaryIfNeeded()
        {
            // Null means we have never tried to create the dictionary. Try to do so.
            if (classificationCategoriesDictionary == null)
            {
                classificationCategoriesDictionary = new Dictionary<string, string>();
                try
                {
                    if (this.DoesTableExist(Constant.DBTables.ClassificationCategories) == false)
                    {
                        return;
                    }
                    using (DataTable dataTable = Database.GetDataTableFromSelect(Sql.SelectStarFrom + DBTables.ClassificationCategories))
                    {
                        int dataTableRowCount = dataTable.Rows.Count;
                        for (int i = 0; i < dataTableRowCount; i++)
                        {
                            DataRow row = dataTable.Rows[i];
                            classificationCategoriesDictionary.Add((string)row[ClassificationCategoriesColumns.Category], (string)row[ClassificationCategoriesColumns.Label]);
                        }
                    }
                }
                catch
                {
                    // Should never really get here, but just in case.
                }
            }
            // Also try to create the corresponding classification descriptions dictionary
            CreateClassificationDescriptionsDictionaryIfNeeded();
        }

        // Create the classification category dictionary to mirror the detection table
        public void CreateClassificationDescriptionsDictionaryIfNeeded()
        {
            // Null means we have never tried to create the dictionary. Try to do so.
            if (classificationDescriptionsDictionary == null)
            {
                classificationDescriptionsDictionary = new Dictionary<string, string>();
                try
                {
                    if (this.DoesTableExist(Constant.DBTables.ClassificationDescriptions) == false)
                    {
                        return;
                    }
                    using (DataTable dataTable = Database.GetDataTableFromSelect(Sql.SelectStarFrom + DBTables.ClassificationDescriptions))
                    {
                        int dataTableRowCount = dataTable.Rows.Count;
                        for (int i = 0; i < dataTableRowCount; i++)
                        {
                            DataRow row = dataTable.Rows[i];
                            classificationDescriptionsDictionary.Add((string)row[ClassificationCategoriesColumns.Category], (string)row[ClassificationCategoriesColumns.Label]);
                        }
                    }
                }
                catch
                {
                    // Should never really get here, but just in case.
                }
            }
        }

        public List<string> GetClassificationLabels()
        {
            List<string> labels = new List<string>();
            CreateClassificationCategoriesDictionaryIfNeeded();
            foreach (KeyValuePair<string, string> entry in classificationCategoriesDictionary)
            {
                labels.Add(entry.Value);
            }
            labels = labels.OrderBy(q => q).ToList();
            return labels;
        }

        // return the label that matches the detection category 
        public string GetClassificationLabelFromCategory(string category)
        {
            try
            {
                CreateClassificationCategoriesDictionaryIfNeeded();
                // A lookup dictionary should now exists, so just return the category value.
                return classificationCategoriesDictionary.TryGetValue(category, out string value) ? value : string.Empty;
            }
            catch
            {
                // Should never really get here, but just in case.
                return string.Empty;
            }
        }

        public string GetClassificationCategoryFromLabel(string label)
        {
            try
            {
                CreateClassificationCategoriesDictionaryIfNeeded();
                // At this point, a lookup dictionary already exists, so just return the category number.
                string myKey = classificationCategoriesDictionary.FirstOrDefault(x => x.Value == label).Key;
                return myKey ?? string.Empty;
            }
            catch
            {
                // Should never really get here, but just in case.
                return string.Empty;
            }
        }
        // See if detections exist in this instance. We test once, and then save the state (unless forceQuery is true)
        private bool? detectionExists;
        /// <summary>
        /// Return if a non-empty detections table exists. If forceQuery is true, then we always do this via an SQL query vs. refering to previous checks
        /// </summary>
        /// <returns></returns>
        public bool DetectionsExists()
        {
            return DetectionsExists(false);
        }
        public bool DetectionsExists(bool forceQuery)
        {
            if (forceQuery || detectionExists == null)
            {
                detectionExists = Database.TableExistsAndNotEmpty(DBTables.Detections);
            }
            return detectionExists == true;
        }
        #endregion

        #region Detections: Counting
        // Given a Counter DataLabel, count the number of detections associated with each image, and set that image's counter to that count
        public bool DetectionsAddCountToCounter(string counterDataLabel, double confidenceValue, IProgress<ProgressBarArguments> progress)
        {
            if (Database == null || false == DetectionsExists())
            {
                return false;
            }
            progress.Report(new ProgressBarArguments(0, "Counting detections...", false, true));
            Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then

            Dictionary<long, int> dict = new Dictionary<long, int>();
            foreach (ImageRow image in FileTable)
            {
                int count = 0;
                BoundingBoxes bboxes = GlobalReferences.MainWindow.GetBoundingBoxesForCurrentFile(image.ID);
                foreach (BoundingBox bbox in bboxes.Boxes)
                {
                    if (bbox.Confidence >= confidenceValue)
                    {
                        count++;
                    }
                }
                dict.Add(image.ID, count);
                image.SetValueFromDatabaseString(counterDataLabel, count.ToString());
            }

            List<ColumnTuplesWithWhere> columnTuplesWithWhereList = new List<ColumnTuplesWithWhere>();
            foreach (KeyValuePair<long, int> kvp in dict)
            {
                // Update the imageRow in the file table with the new value
                //this.UpdateFile(kvp.Key, counterDataLabel, kvp.Value.ToString());

                // Add a query to update the row in the database
                ColumnTuplesWithWhere columnTuplesWithWhere = new ColumnTuplesWithWhere();
                columnTuplesWithWhere.Columns.Add(new ColumnTuple(counterDataLabel, kvp.Value.ToString()));
                columnTuplesWithWhere.SetWhere(kvp.Key);
                columnTuplesWithWhereList.Add(columnTuplesWithWhere);
            }

            progress.Report(new ProgressBarArguments(0, "Updating database...", false, true));
            Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
            if (columnTuplesWithWhereList.Count > 0)
            {
                // Update the Database
                UpdateFiles(columnTuplesWithWhereList);
                // Force an update of the current image in case the current values have changed

            }
            return true;
        }
        #endregion

        #region BoundingBox Thresholds

        public bool TrySetBoundingBoxDisplayThreshold(float threshold)
        {
            if (false == Database.SchemaIsColumnInTable(DBTables.ImageSet, DatabaseColumn.BoundingBoxDisplayThreshold))
            {
                return false;
            }
            Database.Update(DBTables.ImageSet, new ColumnTuple(DatabaseColumn.BoundingBoxDisplayThreshold, threshold));
            return true;
        }

        public bool TryGetBoundingBoxDisplayThreshold(out float threshold)
        {
            threshold = RecognizerValues.Undefined;
            if (false == Database.SchemaIsColumnInTable(DBTables.ImageSet, DatabaseColumn.BoundingBoxDisplayThreshold))
            {
                return false;
            }

            float? fthreshold = Database.ScalarGetFloatValue(DBTables.ImageSet, DatabaseColumn.BoundingBoxDisplayThreshold);
            if (fthreshold == null)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(fthreshold));
                return false;
            }
            threshold = (float)fthreshold;
            return true;
        }
        #endregion

        #region Quickpaste retrieval
        public static string TryGetQuickPasteJSONFromDatabase(string filePath)
        {
            // Open the database if it exists
            SQLiteWrapper sqliteWrapper = new SQLiteWrapper(filePath);
            if (sqliteWrapper.SchemaIsColumnInTable(DBTables.ImageSet, DatabaseColumn.QuickPasteTerms) == false)
            {
                // The column isn't in the table, so give up
                return string.Empty;
            }

            List<object> listOfObjects = sqliteWrapper.GetDistinctValuesInColumn(DBTables.ImageSet, DatabaseColumn.QuickPasteTerms);
            if (listOfObjects.Count == 1)
            {
                return (string)listOfObjects[0];
            }
            return string.Empty;
        }
        #endregion

        #region CustomSelection: Restoring from JSon
        // Restore the custom selection from the Json stored in the image set table
        public FileSelectionEnum GetCustomSelectionFromJSON()
        {
            this.ResetAfterPossibleRelativePathChanges();
            // We put this in a try/catch. If anything fails, we just revert to the default custom selection (All)
            try
            {
                // Get the stored custom selection, and determine custom selection state (all, relativepath or custom).
                // If there is a problem in the customSelectionFromJson (eg if its null or has no search terms), it will return ALL
                CustomSelection customSelectionFromJson = JsonConvert.DeserializeObject<CustomSelection>(ImageSet.SearchTermsAsJSON);

                // Various checks (including null and several settings that could be confusing to the user)
                if (customSelectionFromJson == null ||
                    customSelectionFromJson.SearchTerms == null ||
                    customSelectionFromJson.SearchTerms.Count == 0)
                //|||| customSelectionFromJson.RandomSample != 0 ||
                //customSelectionFromJson.ShowMissingDetections ||
                //customSelectionFromJson.EpisodeShowAllIfAnyMatch)
                {
                    // Didn't pass the test. Use the default
                    CustomSelection = new CustomSelection(Controls);
                    return FileSelectionEnum.All;
                }

                // Reset both RandomSample and ShowMissingDetections so they are not enabled.
                customSelectionFromJson.RandomSample = 0;
                customSelectionFromJson.ShowMissingDetections = false;

                // At this point, customSelectionFromJson should have a valid value
                List<SearchTerm> stlFromJson = customSelectionFromJson.SearchTerms;

                // Check various recognition settings.
                // If the JSON says recognition is enabled and being used, check if the recognition data is actually there for us
                if (null != customSelectionFromJson?.RecognitionSelections && customSelectionFromJson.RecognitionSelections.AllDetections)
                {
                    // Just ensures that the All detection category number is correctly set if we are trying to use All detections
                    // This arises as sometimes the detection category number isn't saved properly in the DB on close, but I couldn't figure out where that was. So a bit of a hack fix.
                    customSelectionFromJson.RecognitionSelections.DetectionCategoryNumber = Constant.RecognizerValues.AllDetectionCategoryNumber;
                }
                int detectionCategoryAsInt = Int32.MaxValue;
                bool parseResultDetectionCategory = null != customSelectionFromJson.RecognitionSelections?.DetectionCategoryNumber && Int32.TryParse(customSelectionFromJson.RecognitionSelections.DetectionCategoryNumber, out detectionCategoryAsInt);
                
                //bool parseResultClassificationCategory = null != customSelectionFromJson.RecognitionSelections?.ClassificationCategoryNumber && Int32.TryParse(customSelectionFromJson.RecognitionSelections.ClassificationCategoryNumber, out classificationCategoryAsInt);
                if (null == customSelectionFromJson.RecognitionSelections?.UseRecognition ||
                      customSelectionFromJson.RecognitionSelections.UseRecognition &&
                        customSelectionFromJson.RecognitionSelections.UseRecognition &&
                        false == DetectionsExists() ||
                        parseResultDetectionCategory == false ||
                        detectionCategoryAsInt >= detectionCategoriesDictionary?.Count 
                        )
                {
                    // Didn't pass the test. Use the default
                    CustomSelection = new CustomSelection(Controls);
                    return FileSelectionEnum.All;
                }

                // Check that all data labels match. 
                // If they don't, return the default custom selection
                List<string> dataLabelsFromJson = new List<string>();
                List<string> dataLabelsFromControls = new List<string>();
                foreach (SearchTerm stFromJson in stlFromJson)
                {
                    if (false == dataLabelsFromJson.Contains(stFromJson.DataLabel))
                    {
                        dataLabelsFromJson.Add(stFromJson.DataLabel);
                    }
                }
                foreach (ControlRow control in Controls)
                {
                    dataLabelsFromControls.Add(control.DataLabel);
                }
                List<string> firstNotSecond = dataLabelsFromJson.Except(dataLabelsFromControls).ToList();
                List<string> secondNotFirst = dataLabelsFromControls.Except(dataLabelsFromJson).ToList();
                if (firstNotSecond.Count != 0 || secondNotFirst.Count != 0)
                {
                    // Didn't pass the test as that data label is not present. Use the default
                    CustomSelection = new CustomSelection(Controls);
                    return FileSelectionEnum.All;
                }

                // Now check various database values to make sure they are permitted.
                // On the way, see if we are only using the relative path
                bool relativePathIsUsed = false;
                bool deleteFlagIsUsed = false;
                int numberSearchTermsUsed = 0;
                foreach (SearchTerm stFromJson in stlFromJson)
                {
                    // Track whether we are only using the relative path search term
                    if (stFromJson.UseForSearching)
                    {
                        numberSearchTermsUsed++;
                        if (stFromJson.DataLabel == DatabaseColumn.DeleteFlag)
                        {
                            deleteFlagIsUsed = true;
                        }
                        else if (stFromJson.DataLabel == DatabaseColumn.RelativePath)
                        {
                            relativePathIsUsed = true;
                        }
                    }

                    // Fixed choice lists must match, and the value must be in the list 
                    if (stFromJson.ControlType == Control.FixedChoice ||
                        stFromJson.ControlType == Control.MultiChoice)
                    {
                        ControlRow row = GetControlFromControls(stFromJson.DataLabel);
                        Choices choices = Choices.ChoicesFromJson(row.List);

                        if (stFromJson.List.Count != 0 && choices.IncludeEmptyChoice)
                        {
                            // Add an empty item.
                            // Note that if the Json list is empty, then that's the same as allowing an empty string
                            choices.ChoiceList.Add(string.Empty);
                        }

                        firstNotSecond = stFromJson.List.Except(choices.ChoiceList).ToList();
                        secondNotFirst = choices.ChoiceList.Except(stFromJson.List).ToList();
                        // We check:
                        // - for different lists
                        // - for an empty databaseValue when we shouldn't include an empty choice
                        // - for a non empty databaseValue that isn't in the list
                        if (firstNotSecond.Count != 0 || secondNotFirst.Count != 0 ||
                            (stFromJson.DatabaseValue == string.Empty && false == choices.IncludeEmptyChoice) ||
                            (stFromJson.DatabaseValue != string.Empty && false == choices.ChoiceList.Contains(stFromJson.DatabaseValue))
                           )
                        {
                            // Didn't pass the test as some list items or its defaults don't match whats in the template.  Use the default
                            CustomSelection = new CustomSelection(Controls);
                            return FileSelectionEnum.All;
                        }
                    }
                }

                // We have a valid custom selection from the Json, so let's use it.
                CustomSelection = customSelectionFromJson;

                // Set the FileSelectionEnum state
                if (CustomSelection.RecognitionSelections.UseRecognition)
                {
                    // Recognition is always custom
                    return FileSelectionEnum.Custom;
                }

                if (numberSearchTermsUsed > 1 && CustomSelection.TermCombiningOperator != CustomSelectionOperatorEnum.And)
                {
                    // the operator only matters if more than  one term being used
                    return FileSelectionEnum.Custom;
                }

                if (relativePathIsUsed && numberSearchTermsUsed == 1)
                {
                    // As only the relative path is set, we must be using folders
                    return FileSelectionEnum.Folders;
                }

                if (deleteFlagIsUsed && numberSearchTermsUsed == 1)
                {
                    // As only the DeleteFlag is set, we must be using MarkedForDeletion
                    return FileSelectionEnum.MarkedForDeletion;
                }

                if (numberSearchTermsUsed > 0)
                {
                    // Ok, we are at custom.
                    return FileSelectionEnum.Custom;
                }
                // If we get here, then we are All
                return FileSelectionEnum.All;
            }

            catch
            {
                // Something blew up. Use the default
                CustomSelection = new CustomSelection(Controls);
                return FileSelectionEnum.All;
            }
        }
        #endregion

        #region Reset after a selection

        private void ResetAfterPossibleRelativePathChanges()
        {
            this.GetRelativePathsInCurrentSelection = null;
        }
        #endregion

        #region Update Old-style Classification table
        // Timelapse version 2.3.2.9 changed how recognition tables were managed. 
        // Prior versions includef a separate detection and classification table.
        // The new version merges the classification data into the detection table
        // As this breaks backwards compatability, pre2.3.2.9 versions will not be able to open these databases.
        public void UpdateOldStyleRecognitionTablesIfNeeded()
        {
            // First, update the Detection table to include the new columns
            if (this.Database.TableExists(DBTables.Detections))
            {
                // Add the two column to the detection table if they don't exist
                if (false == this.Database.SchemaIsColumnInTable(DBTables.Detections, DetectionColumns.Classification))
                {
                    // add the Classification  column to the detection table
                    this.Database.SchemaAddColumnToEndOfTable(DBTables.Detections, new SchemaColumnDefinition(DetectionColumns.Classification, Sql.Text));
                }
                if (false == this.Database.SchemaIsColumnInTable(DBTables.Detections, Constant.DetectionColumns.ClassificationConf))
                {
                    // add the ClassificationConf  column to the detection table
                    this.Database.SchemaAddColumnToEndOfTable(DBTables.Detections, new SchemaColumnDefinition(DetectionColumns.ClassificationConf, Sql.Real));
                }
            }

            // Now check to see if there are any classifications to update table
            if (this.Database.TableExists(DBTables.Classifications) == false)
            {
                // No need to do anything if the table doesn't exist
                return;
            }

            if (false == this.Database.TableHasContent(DBTables.Classifications))
            {
                // Just delete the classifications table as it has no content, which means there is nothing to update
                this.Database.DropTable(DBTables.Classifications);
                return;
            }

            // The classification table has content.We need to update the detection table columns
            // To do this, for each detection we get its maximum classification confidence (if any) and update the detection table with those values
            string query = $"{Sql.Select} {Constant.ClassificationColumns.ClassificationID}, {Constant.ClassificationColumns.Category}, " +
                           $"{Sql.Max}({Constant.ClassificationColumns.Conf}), {Constant.DetectionColumns.DetectionID} " +
                           $"{Sql.From} {DBTables.Classifications} {Sql.GroupBy}({Constant.DetectionColumns.DetectionID})";
            DataTable dataTable = this.Database.GetDataTableFromSelect(query);

            // Now update each detection as needed
            int dataTableRowCount = dataTable.Rows.Count;
            List<ColumnTuplesWithWhere> columnsTuplesWithWhereList = new List<ColumnTuplesWithWhere>();    // holds columns which have changed for the current control
            string newConfColumnName = $"MAX ({ClassificationColumns.Conf})";
            for (int i = 0; i < dataTableRowCount; i++)
            {
                DataRow row = dataTable.Rows[i];
                if (row[ClassificationColumns.Category] == DBNull.Value) continue;
                if (row[newConfColumnName] == DBNull.Value) continue;
                string category = (string)row[ClassificationColumns.Category];
                float conf = (float)(double)row[newConfColumnName];
                long detectionID = (long)row[Constant.DetectionColumns.DetectionID];
                Debug.Print($"{category} {conf} {detectionID}");
                List<ColumnTuple> columnTupleList = new List<ColumnTuple>
                {
                    new ColumnTuple(DetectionColumns.Classification, category),
                    new ColumnTuple(DetectionColumns.ClassificationConf, conf)
                };

                ColumnTuplesWithWhere columnTupleWithWhere = new ColumnTuplesWithWhere(columnTupleList, new ColumnTuple(Constant.DetectionColumns.DetectionID, detectionID));
                columnsTuplesWithWhereList.Add(columnTupleWithWhere);
            }
            Database.Update(DBTables.Detections, columnsTuplesWithWhereList);

            // Versions prior to 2.3.2.8 will not be able to access the classification data as it is being dropped.
            //  as it crashes if the custom select tries to use classifications.
            // This is why we don't allow versions at or after 2.3.2.9 to open earlier databases.
            this.Database.DropTable(DBTables.Classifications);
            this.Database.Vacuum();
        }
        #endregion

        #region Disposing
        protected override void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                FileTable?.Dispose();
                Markers?.Dispose();
                detectionDataTable?.Dispose();
            }

            base.Dispose(disposing);
            disposed = true;
        }

        public void DisposeAsNeeded()
        {
            try
            {
                // Release the file table
                FileTable?.DisposeAsNeeded(onFileDataTableRowChanged);
                FileTable = null;
                Markers?.DisposeAsNeeded(null);
                Markers = null;
                Controls?.DisposeAsNeeded(null);

                // Release various data tables
                detectionDataTable?.Clear();
                detectionDataTable = null;

                // Release the bound grid
                if (boundGrid != null)
                {
                    boundGrid.DataContext = null;
                    boundGrid.ItemsSource = null;
                    boundGrid = null;
                }

                // Release various dictionaries
                classificationCategoriesDictionary = null;
                detectionCategoriesDictionary = null;
            }
            catch
            {
                Debug.Print("Failed in FileDatabase:DisposeAsNeeded");
            }
        }
        #endregion
    }
}