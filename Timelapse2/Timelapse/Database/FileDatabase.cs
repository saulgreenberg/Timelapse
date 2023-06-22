using Newtonsoft.Json;
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
using System.Windows;
using System.Windows.Controls;
using Timelapse.Controls;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Images;
using Timelapse.Recognition;
using Timelapse.SearchingAndSorting;
using Timelapse.Util;
using Path = System.IO.Path;

namespace Timelapse.Database
{
    // FileDatabase manages the Timelapse data held in datatables and the .ddb files.
    // It also acts as a go-between with the database, where it forms Timelapse-specific SQL requests to the SQL wrapper
    public class FileDatabase : TemplateDatabase
    {
        #region Private variables
        private DataGrid boundGrid;
        private bool disposed;
        private DataRowChangeEventHandler onFileDataTableRowChanged;

        // These two dictionaries mirror the contents of the detectionCategory and classificationCategory database table
        // for faster access
        private Dictionary<string, string> detectionCategoriesDictionary;
        private Dictionary<string, string> classificationCategoriesDictionary;
        private DataTable detectionDataTable; // Mirrors the database detection table
        private DataTable classificationsDataTable; // Mirrors the database classification table
        #endregion

        #region Properties 

        // The current file selection (All, Custom, etc.)
        public FileSelectionEnum FileSelectionEnum { get; set; } = FileSelectionEnum.All;
        public CustomSelection CustomSelection { get; private set; }

        /// <summary>Gets the file name of the database on disk.</summary>
        public string FileName { get; private set; }

        /// <summary>Get the complete path to the folder containing the database.</summary>
        public string FolderPath { get; }

        public Dictionary<string, string> DataLabelFromStandardControlType { get; }

        public Dictionary<string, FileTableColumn> FileTableColumnsByDataLabel { get; }

        // contains the results of the data query
        public FileTable FileTable { get; private set; }

        public ImageSetRow ImageSet { get; private set; }

        // contains the markers
        public DataTableBackedList<MarkerRow> Markers { get; private set; }


        // Return the selected folder (if any)
        public string GetSelectedFolder
        {
            get
            {
                if (this.CustomSelection == null)
                {
                    return string.Empty;
                }
                return this.CustomSelection.GetRelativePathFolder;
            }
        }
        #endregion

        #region Create or Open the Database
        private FileDatabase(string filePath)
            : base(filePath)
        {
            this.DataLabelFromStandardControlType = new Dictionary<string, string>();
            this.disposed = false;
            this.FolderPath = Path.GetDirectoryName(filePath);
            this.FileName = Path.GetFileName(filePath);
            this.FileTableColumnsByDataLabel = new Dictionary<string, FileTableColumn>();
        }

        public static async Task<FileDatabase> CreateEmptyDatabase(string ddbFilePath, TemplateDatabase templateDatabase)
        {
            // The ddbFilePath
            FilesFolders.TryDeleteFileIfExists(ddbFilePath);

            // initialize the database if it's newly created
            FileDatabase fileDatabase = new FileDatabase(ddbFilePath);
            await fileDatabase.OnDatabaseCreatedAsync(templateDatabase).ConfigureAwait(true);
            return fileDatabase;
        }

        public static async Task<FileDatabase> CreateOrOpenAsync(string filePath, TemplateDatabase templateDatabase, CustomSelectionOperatorEnum customSelectionTermCombiningOperator, TemplateSyncResults templateSyncResults, bool backupFileJustMade)
        {
            // check for an existing database before instantiating the database as SQL wrapper instantiation creates the database file
            bool populateDatabase = !File.Exists(filePath);

            FileDatabase fileDatabase = new FileDatabase(filePath);
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
            fileDatabase.CustomSelection = new CustomSelection(fileDatabase.Controls, customSelectionTermCombiningOperator);
            if (false == fileDatabase.PopulateDataLabelMaps())
            {
                // This happens if there is an unrecognized Control type
                return null;
            }

            // Recreate the indexes if they don't exist
            // This could happen as a result of upgrading to 2.3
            if (false == fileDatabase.Database.IndexExists(Constant.DatabaseValues.IndexRelativePath))
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
        protected override async Task OnDatabaseCreatedAsync(TemplateDatabase templateDatabase)
        {
            // copy the template's TemplateTable
            await base.OnDatabaseCreatedAsync(templateDatabase).ConfigureAwait(true);

            // Create the DataTable from the template
            // First, define the creation string based on the contents of the template. 
            List<SchemaColumnDefinition> schemaColumnDefinitions = new List<SchemaColumnDefinition>
            {
                new SchemaColumnDefinition(Constant.DatabaseColumn.ID, Sql.CreationStringPrimaryKey)  // It begins with the ID integer primary key
            };
            foreach (ControlRow control in this.Controls)
            {
                schemaColumnDefinitions.Add(CreateFileDataColumnDefinition(control));
            }
            this.Database.CreateTable(Constant.DBTables.FileData, schemaColumnDefinitions);

            // Create the ImageSetTable and initialize a single row in it
            schemaColumnDefinitions.Clear();
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.ID, Sql.CreationStringPrimaryKey));  // It begins with the ID integer primary key
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.RootFolder, Sql.Text, string.Empty));
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.Log, Sql.Text, Constant.DatabaseValues.ImageSetDefaultLog));
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.MostRecentFileID, Sql.Text));
            //schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.Selection, Sql.Text, allImages));
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.VersionCompatabily, Sql.Text));  // Records the highest Timelapse version number ever used to open this database
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.SortTerms, Sql.Text, Constant.DatabaseValues.DefaultSortTerms));        // A JSON description of the sort terms
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.SearchTerms, Sql.Text, Constant.DatabaseValues.DefaultSearchTerms));        // A JSON description of the search terms
            //schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.SelectedFolder, Sql.Text));
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.QuickPasteTerms, Sql.Text));        // A comma-separated list of 4 sort terms
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.BoundingBoxDisplayThreshold, Sql.Real, Constant.RecognizerValues.BoundingBoxDisplayThresholdDefault));        // A comma-separated list of 4 sort terms


            this.Database.CreateTable(Constant.DBTables.ImageSet, schemaColumnDefinitions);

            // Populate the data for the image set with defaults
            // VersionCompatabily
            Version timelapseCurrentVersionNumber = VersionChecks.GetTimelapseCurrentVersionNumber();
            List<ColumnTuple> columnsToUpdate = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.DatabaseColumn.RootFolder, Path.GetFileName(this.FolderPath)),
                new ColumnTuple(Constant.DatabaseColumn.Log, Constant.DatabaseValues.ImageSetDefaultLog),
                new ColumnTuple(Constant.DatabaseColumn.MostRecentFileID, Constant.DatabaseValues.InvalidID),
                //new ColumnTuple(Constant.DatabaseColumn.Selection, allImages.ToString()),
                new ColumnTuple(Constant.DatabaseColumn.VersionCompatabily, timelapseCurrentVersionNumber.ToString()),
                new ColumnTuple(Constant.DatabaseColumn.SortTerms, Constant.DatabaseValues.DefaultSortTerms),
                new ColumnTuple(Constant.DatabaseColumn.SearchTerms, Constant.DatabaseValues.DefaultSearchTerms),
                new ColumnTuple(Constant.DatabaseColumn.QuickPasteTerms, Constant.DatabaseValues.DefaultQuickPasteJSON)
            };
            List<List<ColumnTuple>> insertionStatements = new List<List<ColumnTuple>>
            {
                columnsToUpdate
            };
            this.Database.Insert(Constant.DBTables.ImageSet, insertionStatements);

            this.ImageSetLoadFromDatabase();

            // create the Files table
            // This is necessary as files can't be added unless the Files Column is available.  Thus SelectFiles() has to be called after the ImageSetTable is created
            // so that the selection can be persisted.
            await this.SelectFilesAsync(FileSelectionEnum.All).ConfigureAwait(true);

            this.BindToDataGrid();

            // Create the MarkersTable and initialize it from the template table
            // TODO: SHOULDN'T MARKERS TABLE BE A FOREIGN KEY??? TO CHECK WHY NOT
            schemaColumnDefinitions.Clear();
            schemaColumnDefinitions.Add(new SchemaColumnDefinition(Constant.DatabaseColumn.ID, Sql.CreationStringPrimaryKey));  // It begins with the ID integer primary key
            foreach (ControlRow control in this.Controls)
            {
                if (control.Type.Equals(Constant.Control.Counter))
                {
                    schemaColumnDefinitions.Add(new SchemaColumnDefinition(control.DataLabel, Sql.Text, string.Empty));
                }
            }
            this.Database.CreateTable(Constant.DBTables.Markers, schemaColumnDefinitions);
        }

        protected override async Task OnExistingDatabaseOpenedAsync(TemplateDatabase templateDatabase, TemplateSyncResults templateSyncResults)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(templateDatabase, nameof(templateDatabase));
            ThrowIf.IsNullArgument(templateSyncResults, nameof(templateSyncResults));

            // Perform TemplateTable initializations.
            await base.OnExistingDatabaseOpenedAsync(templateDatabase, null).ConfigureAwait(true);

            // If directed to use the template found in the template database, 
            // check and repair differences between the .tdb and .ddb template tables due to  missing or added controls 
            if (templateSyncResults.UseTemplateDBTemplate)
            {
                // Check for differences between the TemplateTable in the .tdb and .ddb database.
                if (templateSyncResults.SyncRequiredAsDataLabelsDiffer || templateSyncResults.SyncRequiredAsChoiceMenusDiffer)
                {
                    // The TemplateTable in the .tdb and .ddb database differ. 
                    // Update the .ddb Template table by dropping the .ddb template table and replacing it with the .tdb table. 
                    Database.DropTable(Constant.DBTables.Template);
                    await base.OnDatabaseCreatedAsync(templateDatabase).ConfigureAwait(true);
                }

                // Condition 1: the tdb template table contains one or more datalabels not found in the ddb template table
                // That is, the .tdb defines additional controls
                // Action: For each new control in the template table, 
                //           - add a corresponding data column in the ImageTable
                //           - if it is a counter, add a corresponding data column in the MarkerTable

                foreach (string dataLabel in templateSyncResults.DataLabelsToAdd)
                {
                    long id = this.GetControlIDFromTemplateTable(dataLabel);
                    ControlRow control = this.Controls.Find(id);
                    SchemaColumnDefinition columnDefinition = CreateFileDataColumnDefinition(control);
                    this.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.FileData, columnDefinition);

                    if (control.Type == Constant.Control.Counter)
                    {
                        SchemaColumnDefinition markerColumnDefinition = new SchemaColumnDefinition(dataLabel, Sql.Text, Constant.DatabaseValues.DefaultMarkerValue);
                        this.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.Markers, markerColumnDefinition);
                    }
                }

                // Condition 2: The image template table had contained one or more controls not found in the template table.
                // That is, the .ddb DataTable contains data columns that now have no corresponding control 
                // Action: Delete those data columns
                // Redundant check for null, as for some reason the CA1062 warning was still showing up
                ThrowIf.IsNullArgument(templateSyncResults, nameof(templateSyncResults));
                foreach (string dataLabel in templateSyncResults.DataLabelsToDelete)
                {
                    this.Database.SchemaDeleteColumn(Constant.DBTables.FileData, dataLabel);

                    // Delete the markers column associated with this data label (if it exists) from the Markers table
                    // Note that we do this for all column types, even though only counters have an associated entry in the Markers table.
                    // This is because we can't get the type of the data label as it no longer exists in the Template.
                    if (this.Database.SchemaIsColumnInTable(Constant.DBTables.Markers, dataLabel))
                    {
                        this.Database.SchemaDeleteColumn(Constant.DBTables.Markers, dataLabel);
                        // Delete any empty rows from the Marker Table
                        string where = string.Empty;
                        foreach (ControlRow controlRow in this.Controls.Where(x => x.Type == Constant.Control.Counter))
                        {
                            if (controlRow.Type == Constant.Control.Counter)
                            {
                                if (where != string.Empty)
                                {
                                    where += Sql.And;
                                }
                                where += controlRow.DataLabel + Sql.Equal + Sql.Quote(Constant.DatabaseValues.DefaultMarkerValue);
                            }
                        }
                        this.Database.DeleteRows(Constant.DBTables.Markers, where);
                    }
                }

                // Condition 3: The user indicated that the following controls (add/delete) are actually renamed controls
                // Action: Rename those data columns
                foreach (KeyValuePair<string, string> dataLabelToRename in templateSyncResults.DataLabelsToRename)
                {
                    // Rename the column associated with that data label from the FileData table
                    this.Database.SchemaRenameColumn(Constant.DBTables.FileData, dataLabelToRename.Key, dataLabelToRename.Value);

                    // Rename the markers column associated with this data label (if it exists) from the Markers table
                    // Note that we do this for all column types, even though only counters have an associated entry in the Markers table.
                    // This is because its easiest to code, as the function handles attempts to delete a column that isn't there (which also returns false).
                    if (this.Database.SchemaIsColumnInTable(Constant.DBTables.Markers, dataLabelToRename.Key))
                    {
                        this.Database.SchemaRenameColumn(Constant.DBTables.Markers, dataLabelToRename.Key, dataLabelToRename.Value);
                    }
                }

                // Refetch the data labels if needed, as they will have changed due to the repair
                List<string> dataLabels = this.GetDataLabelsExceptIDInSpreadsheetOrder();

                // Condition 4: There are non-critical updates in the template's row (e.g., that only change the UI). 
                // Synchronize the image database's TemplateTable with the template database's TemplateTable 
                // Redundant check for null, as for some reason the CA1062 warning was still showing up
                ThrowIf.IsNullArgument(templateDatabase, nameof(templateDatabase));
                if (templateSyncResults.SyncRequiredAsNonCriticalFieldsDiffer)
                {
                    foreach (string dataLabel in dataLabels)
                    {
                        ControlRow imageDatabaseControl = this.GetControlFromTemplateTable(dataLabel);
                        ControlRow templateControl = templateDatabase.GetControlFromTemplateTable(dataLabel);

                        if (imageDatabaseControl.TryUpdateThisControlRowToMatch(templateControl))
                        {
                            // The control row was updated, so synchronize it to the database
                            this.SyncControlToDatabase(imageDatabaseControl);
                        }
                    }
                }
            }
        }

        private static SchemaColumnDefinition CreateFileDataColumnDefinition(ControlRow control)
        {
            if (control.DataLabel == Constant.DatabaseColumn.DateTime)
            {
                if (DateTimeHandler.TryParseDatabaseDateTime(control.DefaultValue, out _))
                {
                    return new SchemaColumnDefinition(control.DataLabel, "DATETIME", control.DefaultValue);
                }
                else
                {
                    // The date/time is malformed, so just use the default. Not optimal, but...
                   return new SchemaColumnDefinition(control.DataLabel, "DATETIME", DateTimeHandler.ToStringDatabaseDateTime(Constant.ControlDefault.DateTimeDefaultValue));
                }
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
            foreach (ControlRow control in this.Controls)
            {
                FileTableColumn column = FileTableColumn.CreateColumnMatchingControlRowsType(control);
                if (column == null)
                {
                    // this occurs if the control is not one of the recognized Types
                    return false;
                }
                this.FileTableColumnsByDataLabel.Add(column.DataLabel, column);
                // don't type map user defined controls as if there are multiple ones the key would not be unique
                if (Constant.Control.StandardTypes.Contains(column.ControlType))
                {
                    this.DataLabelFromStandardControlType.Add(column.ControlType, column.DataLabel);
                }
            }
            return true;
        }
        #endregion

        #region Upgrade Databases and Templates
        public static async Task<FileDatabase> UpgradeDatabasesAndCompareTemplates(string filePath, TemplateDatabase templateDatabase, TemplateSyncResults templateSyncResults)
        {
            // If the file doesn't exist, then no immediate action is needed
            if (!File.Exists(filePath))
            {
                return null;
            }
            FileDatabase fileDatabase = new FileDatabase(filePath);
            if (fileDatabase.Database.PragmaGetQuickCheck() == false || fileDatabase.TableExists(Constant.DBTables.FileData) == false)
            {
                // The database file is likely corrupt, possibly due to missing a key table, is an empty file, or is otherwise unreadable
                fileDatabase.Dispose();
                return null;
            }
            await fileDatabase.UpgradeDatabasesAndCompareTemplatesAsync(templateDatabase, templateSyncResults).ConfigureAwait(true);
            return fileDatabase;
        }

        protected override async Task UpgradeDatabasesAndCompareTemplatesAsync(TemplateDatabase templateDatabase, TemplateSyncResults templateSyncResults)
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

            // perform TemplateTable initializations and migrations, then check for synchronization issues
            await base.UpgradeDatabasesAndCompareTemplatesAsync(templateDatabase, null).ConfigureAwait(true);

            // Upgrade the database from older to newer formats to preserve backwards compatability
            await this.UpgradeDatabasesForBackwardsCompatabilityAsync().ConfigureAwait(true);

            // Get the datalabels in the various templates 
            Dictionary<string, string> templateDataLabels = templateDatabase.GetTypedDataLabelsExceptIDInSpreadsheetOrder();
            Dictionary<string, string> imageDataLabels = this.GetTypedDataLabelsExceptIDInSpreadsheetOrder();
            templateSyncResults.DataLabelsInTemplateButNotImageDatabase = Compare.Dictionary1ExceptDictionary2(templateDataLabels, imageDataLabels);
            templateSyncResults.DataLabelsInImageButNotTemplateDatabase = Compare.Dictionary1ExceptDictionary2(imageDataLabels, templateDataLabels);

            // Check for differences between the TemplateTable in the .tdb and .ddb database.
            bool areNewColumnsInTemplate = templateSyncResults.DataLabelsInTemplateButNotImageDatabase.Count > 0;
            bool areDeletedColumnsInTemplate = templateSyncResults.DataLabelsInImageButNotTemplateDatabase.Count > 0;

            // Synchronization Issues 1: Mismatch control types. Unable to update as there is at least one control type mismatch 
            // We need to check that the dataLabels in the .ddb template are of the same type as those in the .ttd template
            // If they are not, then we need to flag that.
            foreach (string dataLabel in imageDataLabels.Keys)
            {
                // if the .ddb dataLabel is not in the .tdb template, this will be dealt with later 
                if (!templateDataLabels.ContainsKey(dataLabel))
                {
                    continue;
                }
                ControlRow imageDatabaseControl = this.GetControlFromTemplateTable(dataLabel);
                ControlRow templateControl = templateDatabase.GetControlFromTemplateTable(dataLabel);

                if (imageDatabaseControl.Type != templateControl.Type)
                {
                    templateSyncResults.ControlSynchronizationErrors.Add(
                        $"- The field with DataLabel '{dataLabel}' is of type '{imageDatabaseControl.Type}' in the image data file but of type '{templateControl.Type}' in the template.{Environment.NewLine}");
                }

                // Check if  item(s) in the choice list has been removed. If so, a data field set with the removed value will not be displayable
                List<string> imageDatabaseChoices = Choices.ChoicesFromJson(imageDatabaseControl.List).GetAsListWithOptionalEmptyAsNewLine;
                List<string> templateChoices = Choices.ChoicesFromJson(templateControl.List).GetAsListWithOptionalEmptyAsNewLine;
                List<string> choiceValuesRemovedInTemplate = imageDatabaseChoices.Except(templateChoices).ToList();
                if (choiceValuesRemovedInTemplate.Count > 0)
                {
                    // Add warnings due to changes in the Choice control's menu
                    templateSyncResults.ControlSynchronizationWarnings.Add(
                        $"- As the choice control '{dataLabel}' no longer includes the following menu items, it can't display data with corresponding values:");
                    templateSyncResults.ControlSynchronizationWarnings.Add(
                        $"   {string.Join<string>(", ", choiceValuesRemovedInTemplate)}");
                }

                // Check if there are any other changed values in any of the columns that may affect the UI appearance. If there are, then we need to signal syncing of the template
                if (imageDatabaseControl.ControlOrder != templateControl.ControlOrder ||
                    imageDatabaseControl.SpreadsheetOrder != templateControl.SpreadsheetOrder ||
                    imageDatabaseControl.DefaultValue != templateControl.DefaultValue ||
                    imageDatabaseControl.Label != templateControl.Label ||
                    imageDatabaseControl.Tooltip != templateControl.Tooltip ||
                    imageDatabaseControl.Width != templateControl.Width ||
                    imageDatabaseControl.Copyable != templateControl.Copyable ||
                    imageDatabaseControl.Visible != templateControl.Visible ||
                    templateChoices.Except(imageDatabaseChoices).ToList().Count > 0)
                {
                    templateSyncResults.SyncRequiredAsNonCriticalFieldsDiffer = true;
                }
            }

            // Synchronization Issues 2: Unresolved warnings. Due to existence of other new / deleted columns.
            if (templateSyncResults.ControlSynchronizationErrors.Count > 0)
            {
                if (areNewColumnsInTemplate)
                {
                    string warning = "- ";
                    warning += templateSyncResults.DataLabelsInTemplateButNotImageDatabase.Count.ToString();
                    warning += (templateSyncResults.DataLabelsInTemplateButNotImageDatabase.Count == 1)
                        ? " new control was found in your .tdb template file: "
                        : " new controls were found in your .tdb template file: ";
                    warning +=
                        $"'{string.Join(", ", templateSyncResults.DataLabelsInTemplateButNotImageDatabase.Keys)}'";
                    templateSyncResults.ControlSynchronizationWarnings.Add(warning);
                }
                if (areDeletedColumnsInTemplate)
                {
                    string warning = "- ";
                    warning += templateSyncResults.DataLabelsInImageButNotTemplateDatabase.Count.ToString();
                    warning += (templateSyncResults.DataLabelsInImageButNotTemplateDatabase.Count == 1)
                        ? " data field in your .ddb data file has no corresponding control in your .tdb template file: "
                        : " data fields in your .ddb data file have no corresponding controls in your .tdb template file: ";
                    warning += $"'{string.Join(", ", templateSyncResults.DataLabelsInImageButNotTemplateDatabase.Keys)}'";
                    templateSyncResults.ControlSynchronizationWarnings.Add(warning);
                }
            }
        }

        // Only invoke this when we know the templateDBs are in sync, and the templateDB matches the FileDB (i.e., same control rows/columns) except for one or more defaults.
        public void UpgradeFileDBSchemaDefaultsFromTemplate()
        {
            // Initialize a schema 
            List<SchemaColumnDefinition> columnDefinitions = new List<SchemaColumnDefinition>
            {
                new SchemaColumnDefinition(Constant.DatabaseColumn.ID, Sql.CreationStringPrimaryKey)  // It begins with the ID integer primary key
            };

            // Add the schema for the columns from the FileDB table
            foreach (ControlRow control in this.Controls)
            {
                columnDefinitions.Add(CreateFileDataColumnDefinition(control));
            }

            // Replace the schema in the File DB table with the schema defined by the column definitions.
            this.Database.SchemaAlterTableWithNewColumnDefinitions(Constant.DBTables.FileData, columnDefinitions);
        }

        // Upgrade the database as needed from older to newer formats to preserve backwards compatability 
        private async Task UpgradeDatabasesForBackwardsCompatabilityAsync()
        {
            //bool neverRun = true;
            //if (neverRun)
            //{
            //    // This method is currently a placeholder until we need to do some updating
            //    return;
            //}
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

            //// Add code here that check and repair backward compatability
            await Task.Run(() =>
            {
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

            StringBuilder queryColumns = new StringBuilder(Sql.InsertInto + Constant.DBTables.FileData + Sql.OpenParenthesis); // INSERT INTO DataTable (
            Dictionary<string, string> defaultValueLookup = this.GetDefaultControlValueLookup();

            // Create a comma-separated lists of column names
            // e.g., ... File, RelativePath, Folder, DateTime, ..., 
            foreach (string columnName in this.FileTable.ColumnNames)
            {
                if (columnName == Constant.DatabaseColumn.ID)
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
            for (int image = 0; image < fileCount; image += Constant.DatabaseValues.RowsPerInsert)
            {
                StringBuilder queryValues = new StringBuilder();

                // This loop creates a dataline containing this image's property values, e.g., ( 'IMG_1.JPG', 'relpath', 'folderfoo', ...) ,  
                for (int insertIndex = image; (insertIndex < (image + Constant.DatabaseValues.RowsPerInsert)) && (insertIndex < fileCount); insertIndex++)
                {
                    queryValues.Append(Sql.OpenParenthesis);

                    foreach (string columnName in this.FileTable.ColumnNames)
                    {
                        // Fill up each column in order
                        if (columnName == Constant.DatabaseColumn.ID)
                        {
                            // don't specify an ID in the insert statement as it's an autoincrement primary key
                            continue;
                        }

                        string controlType = this.FileTableColumnsByDataLabel[columnName].ControlType;
                        ImageRow imageProperties = files[insertIndex];
                        switch (controlType)
                        {
                            case Constant.DatabaseColumn.File:
                                queryValues.Append($"{Sql.Quote(imageProperties.File)}{Sql.Comma}");
                                break;

                            case Constant.DatabaseColumn.RelativePath:
                                queryValues.Append($"{Sql.Quote(imageProperties.RelativePath)}{Sql.Comma}");
                                break;

                            case Constant.DatabaseColumn.DateTime:
                                queryValues.Append($"{Sql.Quote(DateTimeHandler.ToStringDatabaseDateTime(imageProperties.DateTime))}{Sql.Comma}");
                                break;

                            case Constant.DatabaseColumn.DeleteFlag:
                                string dataLabel = this.DataLabelFromStandardControlType[Constant.DatabaseColumn.DeleteFlag];

                                // Default as specified in the template file, which should be "false"
                                queryValues.Append($"{Sql.Quote(defaultValueLookup[dataLabel])}{Sql.Comma}");
                                break;

                            // Find and then add the customizable types, populating it with their default values.
                            case Constant.Control.Note:
                                // If a note already has a value in it (e.g., because it was optionally set via its metadata property on load), use that.
                                // Otherwise populate it with its default value.
                                string value = imageProperties.GetValueDisplayString(columnName);
                                if (false == string.IsNullOrEmpty(value) && value != defaultValueLookup[columnName])
                                {
                                    // There is already a value in the note, so use that
                                    queryValues.Append($"{Sql.Quote(imageProperties.GetValueDisplayString(columnName))}{Sql.Comma}");
                                    // Debug.Print("Value is: " + imageProperties.GetValueDisplayString(columnName));
                                }
                                else
                                {
                                    // Use its defaults
                                    queryValues.Append($"{Sql.Quote(defaultValueLookup[columnName])}{Sql.Comma}");
                                }
                                break;
                            case Constant.Control.FixedChoice:
                            case Constant.Control.Flag:
                                // Initialize notes, flags, and fixed choices to the defaults values
                                queryValues.Append($"{Sql.Quote(defaultValueLookup[columnName])}{Sql.Comma}");
                                break;
                            case Constant.Control.Counter:
                                queryValues.Append($"{Sql.Quote(defaultValueLookup[columnName])}{Sql.Comma}");
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

                this.CreateBackupIfNeeded();
                this.Database.ExecuteNonQuery(command);

                if (onFileAdded != null)
                {
                    int lastImageInserted = Math.Min(fileCount - 1, image + Constant.DatabaseValues.RowsPerInsert);
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
            foreach (ControlRow control in this.Controls)
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
        public int GetLastInsertedRow(string datatable, string intfield)
        {
            return this.Database.ScalarGetMaxIntValue(datatable, intfield);
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
            string query = Sql.SelectExists + Sql.OpenParenthesis + Sql.SelectOne + Sql.From + Constant.DBTables.FileData;
            query += Sql.Where + Constant.DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(relativePath);
            query += Sql.And + Constant.DatabaseColumn.File + Sql.Equal + Sql.Quote(filename) + Sql.CloseParenthesis;
            return this.Database.ScalarBoolFromOneOrZero(query);
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

            // Random selection - Add folderPrefix
            //if (this.CustomSelection.RandomSample > 0)
            //{
            //    query += "Select * from DataTable WHERE id IN (SELECT id FROM(";
            //}

            if (this.CustomSelection == null)
            {
                // If no custom selections are configure, then just use a standard query
                query += Sql.SelectStarFrom + Constant.DBTables.FileData;

                // Random selection - Add suffix
                //if (this.CustomSelection.RandomSample > 0)
                //{
                //    query += ") ORDER BY RANDOM() LIMIT 10";
                //}
            }
            else
            {
                if (this.CustomSelection.RandomSample > 0)
                {
                    query += " Select * from DataTable WHERE id IN (SELECT id FROM ( ";
                }

                // If its a pre-configured selection type, set the search terms to match that selection type
                this.CustomSelection.SetSearchTermsFromSelection(selection, this.GetSelectedFolder);


                if (GlobalReferences.DetectionsExists && this.CustomSelection.ShowMissingDetections)
                {
                    // MISSING DETECTIONS 
                    // Create a partial query that returns all missing detections
                    // Form: SELECT DataTable.* FROM DataTable LEFT JOIN Detections ON DataTable.ID = Detections.Id WHERE Detections.Id IS NULL
                    query += SqlPhrase.SelectMissingDetections(SelectTypesEnum.Star);
                }
                else if (GlobalReferences.DetectionsExists && this.CustomSelection.DetectionSelections.Enabled && this.CustomSelection.DetectionSelections.RecognitionType == RecognitionType.Detection)
                {
                    // DETECTIONS
                    // Create a partial query that returns detections matching some conditions
                    // Form: SELECT DataTable.* FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id
                    query += SqlPhrase.SelectDetections(SelectTypesEnum.Star);
                }
                else if (GlobalReferences.DetectionsExists && this.CustomSelection.DetectionSelections.Enabled && this.CustomSelection.DetectionSelections.RecognitionType == RecognitionType.Classification)
                {
                    // CLASSIFICATIONS 
                    // Create a partial query that returns classifications matching some conditions
                    // Form: SELECT DataTable.* FROM Classifications INNER JOIN DataTable ON DataTable.Id = Detections.Id INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 
                    query += SqlPhrase.SelectClassifications(SelectTypesEnum.Star);
                }
                else
                {
                    // Standard query (ie., no detections, no missing detections, no classifications 
                    query += Sql.SelectStarFrom + Constant.DBTables.FileData;
                }
            }

            if (this.CustomSelection != null && (GlobalReferences.DetectionsExists == false || this.CustomSelection.ShowMissingDetections == false))
            {
                string conditionalExpression = this.CustomSelection.GetFilesWhere(); //this.GetFilesConditionalExpression(selection);
                if (string.IsNullOrEmpty(conditionalExpression) == false)
                {
                    query += conditionalExpression;
                }
            }

            // Sort by primary and secondary sort criteria if an image set is actually initialized (i.e., not null)
            if (this.ImageSet != null)
            {
                SortTerm[] sortTerm = new SortTerm[2];
                string[] term = new string[] { string.Empty, string.Empty, string.Empty };
                if (this.CustomSelection != null && this.CustomSelection.DetectionSelections.UseRecognition && this.CustomSelection.DetectionSelections.RecognitionType == RecognitionType.Classification && this.CustomSelection.DetectionSelections.RankByConfidence)
                {
                    // Classifications: Override any sorting as we have asked to rank the results by confidence values
                    term[0] = Constant.DatabaseColumn.RelativePath;
                    term[1] = Constant.DBTables.Classifications + "." + Constant.ClassificationColumns.Conf;
                    term[1] += Sql.Descending;
                }
                else if (this.CustomSelection != null && this.CustomSelection.DetectionSelections.UseRecognition && this.CustomSelection.DetectionSelections.RecognitionType == RecognitionType.Detection && this.CustomSelection.DetectionSelections.RankByConfidence)
                {
                    // Detections: Override any sorting as we have asked to rank the results by confidence values
                    term[0] = Constant.DatabaseColumn.RelativePath;
                    term[1] = Constant.DBTables.Detections + "." + Constant.DetectionColumns.Conf;
                    term[1] += Sql.Descending;
                }
                else
                {
                    // Get the specified sort order. We do this by retrieving the two sort terms
                    // Given the format of the corrected DateTime
                    for (int i = 0; i <= 1; i++)
                    {
                        sortTerm[i] = this.ImageSet.GetSortTerm(i);
                        // If we see an empty data label, we don't have to construct any more terms as there will be nothing more to sort
                        if (string.IsNullOrEmpty(sortTerm[i].DataLabel))
                        {
                            if (i == 0)
                            {
                                // If the first term is not set, reset the sort back to the default
                                this.ResetSortTermsToDefault(term);
                            }
                            break;
                        }
                        else if (sortTerm[i].DataLabel == Constant.DatabaseColumn.DateTime)
                        {
                            term[i] = $"datetime({Constant.DatabaseColumn.DateTime})";

                            // DUPLICATE RECORDS Special case if DateTime is the first search term and there is no 2nd search term. 
                            // If there are multiple files with the same date/time and one of them is a duplicate,
                            // then the duplicate may not necessarily appear in a sequence, as ambiguities just use the ID (a duplicate is created with a new ID that may be very distant from the original record).
                            // So, we default the final sort term to 'File'. However, if this is not the first search term, it can be over-written 
                            term[2] = Constant.DatabaseColumn.File;
                        }
                        else if (sortTerm[i].DataLabel == Constant.DatabaseColumn.File)
                        {
                            // File: the modified term creates a file path by concatenating relative path and file
                            term[i] =
                                $"{Constant.DatabaseColumn.RelativePath}{Sql.Comma}{Constant.DatabaseColumn.File}";
                        }

                        else if (sortTerm[i].DataLabel != Constant.DatabaseColumn.ID
                                 && false == this.CustomSelection?.SearchTerms?.Exists(x => x.DataLabel == sortTerm[i].DataLabel))
                        {

                            // The Sorting data label doesn't exist (likely because that datalabel was deleted or renamed in the template)
                            // Note: as ID isn't in the list, we have to check that so it can pass through as a sort option
                            // Revert back to the default sort everywhere.
                            this.ResetSortTermsToDefault(term);
                            break;
                        }
                        else if (sortTerm[i].ControlType == Constant.Control.Counter)
                        {
                            // Its a counter type: modify sorting of blanks by transforming it into a '-1' and then by casting it as an integer
                            term[i] = $"Cast(COALESCE(NULLIF({sortTerm[i].DataLabel}, ''), '-1') as Integer)";
                        }
                        else
                        {
                            // Default: just sort by the data label
                            term[i] = sortTerm[i].DataLabel;
                        }
                        // Add Descending sort, if needed. Default is Ascending, so we don't have to add that
                        if (sortTerm[i].IsAscending == Constant.BooleanValue.False)
                        {
                            term[i] += Sql.Descending;
                        }
                    }
                }

                // Random selection - Add suffix
                if (this.CustomSelection != null && this.CustomSelection.RandomSample > 0)
                {
                    // Original form is  query += String.Format(" ) ORDER BY RANDOM() LIMIT {0} )", this.CustomSelection.RandomSample);
                    query += Sql.CloseParenthesis + Sql.OrderByRandom + Sql.Limit + this.CustomSelection.RandomSample + Sql.CloseParenthesis;

                }

                if (!string.IsNullOrEmpty(term[0]))
                {
                    query += Sql.OrderBy + term[0];

                    // If there is a second sort key, add it here
                    if (!string.IsNullOrEmpty(term[1]))
                    {
                        query += Sql.Comma + term[1];
                    }
                    // If there is a third sort key (which would only ever be 'File') add it here.
                    // NOTE: I am not sure if this will always work on every occassion, but my limited test says its ok.
                    if (!string.IsNullOrEmpty(term[2]))
                    {
                        query += Sql.Comma + term[2];
                    }
                    //query += Sql.Semicolon;
                }
            }

            // EPISODES-related addition to query.
            // If the Detectionsand Episodes  is turned on, then the Episode Note field contains values in the Episode format (e.g.) 25:1/8.
            // We construct a wrapper for selecting files where all files in an episode have at least one file matching the surrounded search condition 
            if (this.CustomSelection != null && this.CustomSelection.EpisodeShowAllIfAnyMatch && this.CustomSelection.EpisodeNoteField != string.Empty)
            {
                string frontWrapper = SqlPhrase.CountOrSelectFilesInEpisodeIfOneFileMatchesFrontWrapper(Constant.DBTables.FileData, this.CustomSelection.EpisodeNoteField, false);
                string backWrapper = Sql.CloseParenthesis + Sql.CloseParenthesis;
                query = frontWrapper + query + backWrapper;
            }

            // Debug.Print("Select Query: " + query);
            // PERFORMANCE  This seems to be the main performance bottleneck. Running a query on a large database that returns
            // a large datatable (e.g., all files) is very slow. There is likely a better way to do this, but I am not sure what
            // as I am not that savvy in database optimizations.
            // Debug.Print(query);
            DataTable filesTable = await Task.Run(() => this.Database.GetDataTableFromSelect(query)).ConfigureAwait(true);
            this.FileTable = new FileTable(filesTable);
        }

        // Used by the above
        // Reset sort terms back to the defaults
        private void ResetSortTermsToDefault(string[] term)
        {

            // The Search terms should contain some of the necessary information
            SearchTerm st1 = this.CustomSelection.SearchTerms.Find(x => x.DataLabel == Constant.DatabaseColumn.RelativePath);
            SearchTerm st2 = this.CustomSelection.SearchTerms.Find(x => x.DataLabel == Constant.DatabaseColumn.DateTime);

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
                s1 = new SortTerm(st1.DataLabel, st1.Label, st1.ControlType, Constant.BooleanValue.True);
                s2 = new SortTerm(st2.DataLabel, st2.Label, st2.ControlType, Constant.BooleanValue.True);
            }
            term[0] = s1.DataLabel;
            term[1] = s2.DataLabel;

            // Update the Image Set with the new sort terms
            this.ImageSet.SetSortTerms(s1, s2);
            this.UpdateSyncImageSetToDatabase();
        }

        // Select all files in the file table
        public FileTable SelectAllFiles()
        {
            string query = Sql.SelectStarFrom + Constant.DBTables.FileData;
            DataTable filesTable = this.Database.GetDataTableFromSelect(query);
            return new FileTable(filesTable);
        }

        public List<long> SelectFilesByRelativePathAndFileName(string relativePath, string fileName)
        {
            string query = Sql.SelectStarFrom + Constant.DBTables.FileData + Sql.Where + Constant.DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(relativePath) + Sql.And + Constant.DatabaseColumn.File + Sql.Equal + Sql.Quote(fileName);
            DataTable fileTable = this.Database.GetDataTableFromSelect(query);
            List<long> idList = new List<long>();
            for (int i = 0; i < fileTable.Rows.Count; i++)
            {
                idList.Add((long)fileTable.Rows[i].ItemArray[0]);
            }
            return idList;
        }
        // Check for the existence of missing files in the current selection, and return a list of IDs of those that are missing
        // PERFORMANCE this can be slow if there are many files
        public bool SelectMissingFilesFromCurrentlySelectedFiles()
        {
            if (this.FileTable == null)
            {
                return false;
            }
            string commaSeparatedListOfIDs = string.Empty;

            // Check if each file exists. Get all missing files in the selection as a list of file ids, e.g., "1,2,8,10" 
            foreach (ImageRow image in this.FileTable)
            {
                if (!File.Exists(Path.Combine(this.FolderPath, image.RelativePath, image.File)))
                {
                    commaSeparatedListOfIDs += image.ID + ",";
                }
            }
            // remove the trailing comma
            commaSeparatedListOfIDs = commaSeparatedListOfIDs.TrimEnd(',');
            if (string.IsNullOrEmpty(commaSeparatedListOfIDs))
            {
                // No missing files
                return false;
            }
            this.FileTable = this.SelectFilesInDataTableByCommaSeparatedIds(commaSeparatedListOfIDs);
            this.FileTable.BindDataGrid(this.boundGrid, this.onFileDataTableRowChanged);
            return true;
        }

        public List<string> SelectFileNamesWithRelativePathFromDatabase(string relativePath)
        {
            List<string> files = new List<string>();
            // Form: Select * From DataTable Where RelativePath = '<relativePath>'
            string query = Sql.Select + Constant.DatabaseColumn.File + Sql.From + Constant.DBTables.FileData + Sql.Where + Constant.DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(relativePath);
            DataTable images = this.Database.GetDataTableFromSelect(query);
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
            string where = this.DataLabelFromStandardControlType[Constant.DatabaseColumn.DeleteFlag] + "=" + Sql.Quote(Constant.BooleanValue.True); // = value
            string query = Sql.SelectStarFrom + Constant.DBTables.FileData + Sql.Where + where;
            DataTable filesTable = this.Database.GetDataTableFromSelect(query);
            return new FileTable(filesTable);
        }

        // Select files with matching IDs where IDs are a comma-separated string i.e.,
        // Select * From DataTable Where  Id IN(1,2,4 )
        public FileTable SelectFilesInDataTableByCommaSeparatedIds(string listOfIds)
        {
            string query = Sql.SelectStarFrom + Constant.DBTables.FileData + Sql.WhereIDIn + Sql.OpenParenthesis + listOfIds + Sql.CloseParenthesis;
            DataTable filesTable = this.Database.GetDataTableFromSelect(query);
            return new FileTable(filesTable);
        }

        public FileTable SelectFileInDataTableById(string id)
        {
            string query = Sql.SelectStarFrom + Constant.DBTables.FileData + Sql.WhereIDEquals + Sql.Quote(id) + Sql.LimitOne;
            DataTable filesTable = this.Database.GetDataTableFromSelect(query);
            return new FileTable(filesTable);
        }

        // A specialized call: Given a relative path and two dates (in database DateTime format without the offset)
        // return a table containing ID, DateTime that matches the relative path and is inbetween the two datetime intervals
        public DataTable GetIDandDateWithRelativePathAndBetweenDates(string relativePath, string lowerDateTime, string uppderDateTime)
        {
            // datetimes are in database format e.g., 2017-06-14T18:36:52.000Z 
            // Form: Select ID,DateTime from DataTable where RelativePath='relativePath' and DateTime BETWEEN 'lowerDateTime' AND 'uppderDateTime' ORDER BY DateTime ORDER BY DateTime  
            string query = Sql.Select + Constant.DatabaseColumn.ID + Sql.Comma + Constant.DatabaseColumn.DateTime + Sql.From + Constant.DBTables.FileData;
            query += Sql.Where + Constant.DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(relativePath);
            query += Sql.And + Constant.DatabaseColumn.DateTime + Sql.Between + Sql.Quote(lowerDateTime) + Sql.And + Sql.Quote(uppderDateTime);
            query += Sql.OrderBy + Constant.DatabaseColumn.DateTime;
            return (this.Database.GetDataTableFromSelect(query));
        }
        #endregion

        #region Return a new sorted list containing the distinct relative paths in the database
        // Return a new sorted list containing the distinct relative paths in the database,
        // and the (unique) parents of each relative path entry.
        // For example, if the relative paths were a/b, a/b/c, a/b/d and d/c it would return
        // a | a/b | a/b/c, a/b/d | d | d/c
        public List<string> GetFoldersFromRelativePaths()
        {
            List<object> relativePathList = this.GetDistinctValuesInColumn(Constant.DBTables.FileData, Constant.DatabaseColumn.RelativePath);
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

        // Get only the distinct and complete relative paths associated with images
        public List<string> GetRelativePaths()
        {
            List<object> relativePathList = this.GetDistinctValuesInColumn(Constant.DBTables.FileData, Constant.DatabaseColumn.RelativePath);
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
            return this.Database.GetDistinctValuesInColumn(table, columnName);
        }

        // Return all distinct values from a column in the file table, used for autocompletion
        // Note that this returns distinct values only in the SELECTED files
        // PERFORMANCE - the issue here is that there may be too many distinct entries, which slows down autocompletion. This should thus restrict entries, perhaps by:
        // - check matching substrings before adding, to avoid having too many entries?
        // - only store the longest version of a string. But this would involve more work when adding entries, so likely not worth it.
        public Dictionary<string, string> GetDistinctValuesInSelectedFileTableColumn(string dataLabel, int minimumNumberOfRequiredCharacters)
        {
            Dictionary<string, string> distinctValues = new Dictionary<string, string>();
            foreach (ImageRow row in this.FileTable)
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
            string relativePathFile = Constant.DatabaseColumn.RelativePath + Sql.Comma + Constant.DatabaseColumn.File;
            string query = Sql.Select + relativePathFile
                + Sql.From + Constant.DBTables.FileData
                + Sql.GroupBy + relativePathFile + Sql.Having + Sql.CountStar + Sql.GreaterThan + "1";
            DataTable dataTable = this.Database.GetDataTableFromSelect(query);
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
            ImageRow image = this.FileTable.Find(fileID);
            image.SetValueFromDatabaseString(dataLabel, value);

            // update the row in the database
            this.CreateBackupIfNeeded();

            ColumnTuplesWithWhere columnToUpdate = new ColumnTuplesWithWhere();
            columnToUpdate.Columns.Add(new ColumnTuple(dataLabel, value)); // Populate the data 
            columnToUpdate.SetWhere(fileID);
            this.Database.Update(Constant.DBTables.FileData, columnToUpdate);
        }

        // Set one property on all rows in the selected view to a given value
        public void UpdateFiles(ImageRow valueSource, string dataLabel)
        {
            this.UpdateFiles(valueSource, dataLabel, 0, this.CountAllCurrentlySelectedFiles - 1);
        }

        // Given a list of column/value pairs (the string,object) and the FILE name indicating a row, update it
        public void UpdateFiles(List<ColumnTuplesWithWhere> filesToUpdate)
        {
            this.CreateBackupIfNeeded();
            this.Database.Update(Constant.DBTables.FileData, filesToUpdate);
        }

        public void UpdateFiles(ColumnTuplesWithWhere filesToUpdate)
        {
            List<ColumnTuplesWithWhere> imagesToUpdateList = new List<ColumnTuplesWithWhere>
            {
                filesToUpdate
            };
            this.Database.Update(Constant.DBTables.FileData, imagesToUpdateList);
        }

        public void UpdateFiles(ColumnTuple columnToUpdate)
        {
            this.Database.Update(Constant.DBTables.FileData, columnToUpdate);
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
            if (toIndex < fromIndex || toIndex > this.CountAllCurrentlySelectedFiles - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(toIndex));
            }

            string value = valueSource.GetValueDatabaseString(dataLabel);
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            for (int index = fromIndex; index <= toIndex; index++)
            {
                // update data table
                ImageRow image = this.FileTable[index];
                if (null == image)
                {
                    Debug.Print(
                        $"in FileDatabase.UpdateFiles v1: FileTable returned null as there is no index: {index}");
                    continue;
                }
                image.SetValueFromDatabaseString(dataLabel, value);

                // update database
                List<ColumnTuple> columnToUpdate = new List<ColumnTuple>() { new ColumnTuple(dataLabel, value) };
                ColumnTuplesWithWhere imageUpdate = new ColumnTuplesWithWhere(columnToUpdate, image.ID);
                imagesToUpdate.Add(imageUpdate);
            }
            this.CreateBackupIfNeeded();
            this.Database.Update(Constant.DBTables.FileData, imagesToUpdate);
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
                ImageRow image = this.FileTable[fileIndex];
                if (null == image)
                {
                    Debug.Print(
                        $"in FileDatabase.UpdateFiles v2: FileTable returned null as there is no index: {fileIndex}");
                    continue;
                }
                image.SetValueFromDatabaseString(dataLabel, value);

                // update database
                List<ColumnTuple> columnToUpdate = new List<ColumnTuple>() { new ColumnTuple(dataLabel, value) };
                ColumnTuplesWithWhere imageUpdate = new ColumnTuplesWithWhere(columnToUpdate, image.ID);
                imagesToUpdate.Add(imageUpdate);
            }
            this.CreateBackupIfNeeded();
            this.Database.Update(Constant.DBTables.FileData, imagesToUpdate);
        }
        #endregion

        #region Update Syncing 
        public void UpdateSyncImageSetToDatabase()
        {
            // don't trigger backups on image set updates as none of the properties in the image set table is particularly important
            // For example, this avoids creating a backup when a custom selection is reverted to all when Timelapse exits.
            this.Database.Update(Constant.DBTables.ImageSet, this.ImageSet.CreateColumnTuplesWithWhereByID());
        }

        public void UpdateSyncMarkerToDatabase(MarkerRow marker)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(marker, nameof(marker));

            this.CreateBackupIfNeeded();
            this.Database.Update(Constant.DBTables.Markers, marker.CreateColumnTuplesWithWhereByID());
        }
        #endregion

        #region Update Markers
        // The id is the row to update, the datalabels are the labels of each control to updata, 
        // and the markers are the respective point lists for each of those labels
        // ReSharper disable once UnusedMember.Global
        public void UpdateMarkers(List<ColumnTuplesWithWhere> markersToUpdate)
        {
            // update markers in database
            this.CreateBackupIfNeeded();
            this.Database.Update(Constant.DBTables.Markers, markersToUpdate);

            // Refresh the markers data table
            this.RefreshMarkers();
        }
        #endregion

        #region Refresh various datatables (markers,detections, classifications)
        // Refresh the Markers DataTable
        public void RefreshMarkers()
        {
            this.MarkersLoadRowsFromDatabase();
        }

        // Refresh the Detections DataTable
        public void RefreshDetectionsDataTable()
        {
            this.detectionDataTable = this.Database.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.Detections);
        }

        // Refresh the Classifications DataTable
        public void RefreshClassificationsDataTable()
        {
            this.classificationsDataTable = this.Database.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.Classifications);
        }
        #endregion

        #region Update File Dates and Times
        // Update all selected files with the given time adjustment
        public void UpdateAdjustedFileTimes(TimeSpan adjustment)
        {
            this.UpdateAdjustedFileTimes(adjustment, 0, this.CountAllCurrentlySelectedFiles - 1);
        }

        // Update all selected files between the start and end row with the given time adjustment
        public void UpdateAdjustedFileTimes(TimeSpan adjustment, int startRow, int endRow)
        {
            if (adjustment.Milliseconds != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(adjustment), "The current format of the time column does not support milliseconds.");
            }
            this.UpdateAdjustedFileTimes((fileName, fileIndex, count, imageTime) => imageTime + adjustment, startRow, endRow, CancellationToken.None);
        }

        // Given a time difference in ticks, update all the date/time field in the database
        // Note that it does NOT update the dataTable - this has to be done outside of this routine by regenerating the datatables with whatever selection is being used..
        public void UpdateAdjustedFileTimes(Func<string, int, int, DateTime, DateTime> adjustment, int startRow, int endRow, CancellationToken token)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(adjustment, nameof(adjustment));

            if (this.IsFileRowInRange(startRow) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(startRow));
            }
            if (this.IsFileRowInRange(endRow) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(endRow));
            }
            if (endRow < startRow)
            {
                throw new ArgumentOutOfRangeException(nameof(endRow), "endRow must be greater than or equal to startRow.");
            }
            if (this.CountAllCurrentlySelectedFiles == 0)
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
                ImageRow image = this.FileTable[row];
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
                this.CreateBackupIfNeeded();
                this.Database.Update(Constant.DBTables.FileData, imagesToUpdate);
            }
        }

        // Update all the date fields between the start and end index by swapping the days and months.
        public void UpdateExchangeDayAndMonthInFileDates(int startRow, int endRow)
        {
            if (this.IsFileRowInRange(startRow) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(startRow));
            }
            if (this.IsFileRowInRange(endRow) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(endRow));
            }
            if (endRow < startRow)
            {
                throw new ArgumentOutOfRangeException(nameof(endRow), "endRow must be greater than or equal to startRow.");
            }
            if (this.CountAllCurrentlySelectedFiles == 0)
            {
                return;
            }

            // Get the original date value of each. If we can swap the date order, do so. 
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            for (int row = startRow; row <= endRow; row++)
            {
                ImageRow image = this.FileTable[row];
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
                this.CreateBackupIfNeeded();
                this.Database.Update(Constant.DBTables.FileData, imagesToUpdate);
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
            if (isInteriorNode)
            {
                query = Sql.Update + Constant.DBTables.FileData
                                   + Sql.Set + Constant.DatabaseColumn.RelativePath + Sql.Equal
                                   + Sql.Quote(newPrefixPath) + Sql.Concatenate
                                   + Sql.Substr
                                   + Sql.OpenParenthesis
                                   + Constant.DatabaseColumn.RelativePath + Sql.Comma
                                   + Sql.Length + Sql.OpenParenthesis + Sql.Quote(oldPrefixPath) +
                                   Sql.CloseParenthesis + Sql.Plus + "1"
                                   + Sql.CloseParenthesis
                                   + Sql.Where
                                   + Sql.Instr
                                   + Sql.OpenParenthesis
                                   + Constant.DatabaseColumn.RelativePath + Sql.Comma
                                   + Sql.Quote(oldPrefixPath + '\\')
                                   + Sql.CloseParenthesis
                                   + Sql.BooleanEquals + "1";
            }
            else
            {
                query = Sql.Update + Constant.DBTables.FileData
                                   + Sql.Set + Constant.DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(newPrefixPath) 
                                   + Sql.Where
                                   + Constant.DatabaseColumn.RelativePath  + Sql.BooleanEquals + Sql.Quote(oldPrefixPath);
            }
            this.Database.ExecuteNonQuery(query);
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
                idClauses.Add(Constant.DatabaseColumn.ID + " = " + fileID);
            }
            // Delete the data and markers associated with that image
            this.CreateBackupIfNeeded();
            this.Database.Delete(Constant.DBTables.FileData, idClauses);
            this.Database.Delete(Constant.DBTables.Markers, idClauses);
        }
        #endregion

        #region Schema retrieval
        public Dictionary<string, string> SchemaGetColumnsAndDefaultValues(string tableName)
        {
            return this.Database.SchemaGetColumnsAndDefaultValues(tableName);
        }

        // ReSharper disable once UnusedMember.Global
        public List<string> SchemaGetColumns(string tableName)
        {
            return this.Database.SchemaGetColumns(tableName);
        }
        #endregion

        #region Counts or Exists 1 of matching files
        // Return a total count of the currently selected files in the file table.
        public int CountAllCurrentlySelectedFiles => this.FileTable?.RowCount ?? 0;

        // Return the count of the files matching the fileSelection condition in the entire database
        // Form examples
        // - Select Count(*) FROM (Select * From Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id WHERE <some condition> GROUP BY Detections.Id HAVING  MAX  ( Detections.conf )  <= 0.9)
        // - Select Count(*) FROM (Select * From Classifications INNER JOIN DataTable ON DataTable.Id = Detections.Id  INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID WHERE DataTable.Person<>'true' 
        // AND Classifications.category = 6 GROUP BY Classifications.classificationID HAVING  MAX  (Classifications.conf ) BETWEEN 0.8 AND 1 
        public int CountAllFilesMatchingSelectionCondition(FileSelectionEnum fileSelection)
        {
            string query;
            bool skipWhere = false;

            // PART 1 of Query
            if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && this.CustomSelection.ShowMissingDetections)
            {
                // MISSING DETECTIONS
                // Create a query that returns a count of missing detections
                // Form: SELECT COUNT ( DataTable.Id ) FROM DataTable LEFT JOIN Detections ON DataTable.ID = Detections.Id WHERE Detections.Id IS NULL 
                query = SqlPhrase.SelectMissingDetections(SelectTypesEnum.Count);
                skipWhere = true;
            }
            else if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && this.CustomSelection.DetectionSelections.Enabled && this.CustomSelection.DetectionSelections.RecognitionType == RecognitionType.Detection)
            {
                // DETECTIONS
                // Create a query that returns a count of detections matching some conditions
                // Form: SELECT COUNT  ( * )  FROM  (  SELECT * FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id
                query = SqlPhrase.SelectDetections(SelectTypesEnum.Count);
            }
            else if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && this.CustomSelection.DetectionSelections.Enabled && this.CustomSelection.DetectionSelections.RecognitionType == RecognitionType.Classification)
            {
                // CLASSIFICATIONS
                // Create a partial query that returns a count of classifications matching some conditions
                // Form: Select COUNT  ( * )  FROM  (SELECT DISTINCT DataTable.* FROM Classifications INNER JOIN DataTable ON DataTable.Id = Detections.Id INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 
                query = SqlPhrase.SelectClassifications(SelectTypesEnum.Count);
            }
            else
            {
                // STANDARD (NO DETECTIONS/CLASSIFICATIONS)
                // Create a query that returns a count that does not consider detections
                query = Sql.SelectCountStarFrom + Constant.DBTables.FileData;
            }

            // PART 2 of Query
            // Now add the Where conditions to the query.
            // If the selection is All, there is no where clause needed.
            if (fileSelection != FileSelectionEnum.All)
            {
                if ((GlobalReferences.DetectionsExists && this.CustomSelection.ShowMissingDetections == false) || skipWhere == false)
                {
                    string where = this.CustomSelection.GetFilesWhere(); //this.GetFilesConditionalExpression(fileSelection);
                    if (!string.IsNullOrEmpty(where))
                    {
                        query += where;
                    }
                    if (fileSelection == FileSelectionEnum.Custom && this.CustomSelection.DetectionSelections.Enabled)
                    {
                        // Add a close parenthesis if we are querying for detections
                        query += Sql.CloseParenthesis;
                    }
                }
            }

            // EPISODES-related addition to query.
            // If the Detectionsand Episodes  is turned on, then the Episode Note field contains values in the Episode format (e.g.) 25:1/8.
            // We construct a wrapper for counting  files where all files in an episode have at least one file matching the surrounded search condition 
            if (this.CustomSelection.EpisodeShowAllIfAnyMatch && this.CustomSelection.EpisodeNoteField != string.Empty
                && fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && this.CustomSelection.DetectionSelections.Enabled)
            {
                // Remove from the front of the string
                query = query.Replace(Sql.SelectCountStarFrom, string.Empty);
                string frontWrapper = SqlPhrase.CountOrSelectFilesInEpisodeIfOneFileMatchesFrontWrapper(Constant.DBTables.FileData, this.CustomSelection.EpisodeNoteField, true);
                string backWrapper = Sql.CloseParenthesis;
                query = frontWrapper + query + backWrapper;
            }
            // Uncommment this to see the actual complete query
            // Debug.Print("File Counts: " + query);
            return this.Database.ScalarGetCountFromSelect(query);
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
            if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && this.CustomSelection.ShowMissingDetections)
            {
                // MISSING DETECTIONS
                // Create a query that returns a count of missing detections
                // Form: SELECT COUNT ( DataTable.Id ) FROM DataTable LEFT JOIN Detections ON DataTable.ID = Detections.Id WHERE Detections.Id IS NULL 
                query += SqlPhrase.SelectMissingDetections(SelectTypesEnum.One);
                skipWhere = true;
            }
            else if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && this.CustomSelection.DetectionSelections.Enabled && this.CustomSelection.DetectionSelections.RecognitionType == RecognitionType.Detection)
            {
                // DETECTIONS
                // Create a query that returns a count of detections matching some conditions
                // Form: SELECT COUNT  ( * )  FROM  (  SELECT * FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id
                query += SqlPhrase.SelectDetections(SelectTypesEnum.One);
            }
            else if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && this.CustomSelection.DetectionSelections.Enabled && this.CustomSelection.DetectionSelections.RecognitionType == RecognitionType.Classification)
            {
                // CLASSIFICATIONS
                // Create a partial query that returns a count of classifications matching some conditions
                // Form: Select COUNT  ( * )  FROM  (SELECT DISTINCT DataTable.* FROM Classifications INNER JOIN DataTable ON DataTable.Id = Detections.Id INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 
                query += SqlPhrase.SelectClassifications(SelectTypesEnum.One);
            }
            else
            {
                // STANDARD (NO DETECTIONS/CLASSIFICATIONS)
                // Create a query that returns a count that does not consider detections
                query += Sql.SelectOne + Sql.From + Constant.DBTables.FileData;
            }

            // PART 2 of Query
            // Now add the Where conditions to the query
            if ((GlobalReferences.DetectionsExists && this.CustomSelection.ShowMissingDetections == false) || skipWhere == false)
            {
                string where = this.CustomSelection.GetFilesWhere(); //this.GetFilesConditionalExpression(fileSelection);
                if (!string.IsNullOrEmpty(where))
                {
                    query += where;
                }
                if (fileSelection == FileSelectionEnum.Custom && this.CustomSelection.DetectionSelections.Enabled && this.CustomSelection.DetectionSelections.RecognitionType == RecognitionType.Classification)
                {
                    // Add a close parenthesis if we are querying for detections. Not sure where the unbalanced parenthesis is coming from! Needs some checking.
                    query += Sql.CloseParenthesis;
                }
            }
            query += Sql.CloseParenthesis;

            // Uncommment this to see the actual complete query
            //Debug.Print("File Exists: " + query + ":" + this.Database.ScalarGetCountFromSelect(query).ToString() );
            return this.Database.ScalarGetCountFromSelect(query) != 0;
        }

        #endregion

        #region Counts entries with the given relative path

        public int CountAllFilesMatchingRelativePath(string RelativePath)
        {
            string query = Sql.SelectCountStarFrom + Constant.DBTables.FileData + Sql.Where + Constant.DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(RelativePath);
            return this.Database.ScalarGetCountFromSelect(query);
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
                    ? CustomSelection.RelativePathGlobToIncludeSubfolders(Constant.DatabaseColumn.RelativePath, GlobalReferences.MainWindow.Arguments.RelativePath)
                    : string.Empty;
            string selectionTerm;
            // Common query folderPrefix: SELECT EXISTS  ( SELECT 1  FROM DataTable WHERE 
            string query = Sql.SelectExists + Sql.OpenParenthesis + Sql.SelectOne + Sql.From + Constant.DBTables.FileData + Sql.Where;


            // Count the number of deleteFlags
            if (fileSelection == FileSelectionEnum.MarkedForDeletion)
            {
                // Term form is: DeleteFlag = 'TRUE' COllate nocase
                selectionTerm = Constant.DatabaseColumn.DeleteFlag + Sql.Equal + Sql.Quote("true") + Sql.CollateNocase;
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
            return this.Database.ScalarBoolFromOneOrZero(query);
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
                rowIndex = this.FindByFileNameForwards(currentRow + 1, this.CountAllCurrentlySelectedFiles, filename);
                return rowIndex == -1 ? this.FindByFileNameForwards(0, currentRow - 1, filename) : rowIndex;
            }
            else
            {
                // Find backwards  with wrapping
                rowIndex = this.FindByFileNameBackwards(currentRow - 1, 0, filename);
                return rowIndex == -1 ? this.FindByFileNameBackwards(this.CountAllCurrentlySelectedFiles, currentRow + 1, filename) : rowIndex;
            }
        }

        // Helper for FindByFileName
        private int FindByFileNameForwards(int from, int to, string filename)
        {
            for (int rowIndex = from; rowIndex <= to; rowIndex++)
            {
                if (this.FileRowContainsFileName(rowIndex, filename) >= 0)
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
                if (this.FileRowContainsFileName(rowIndex, filename) >= 0)
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
            if (this.IsFileRowInRange(rowIndex) == false)
            {
                return -1;
            }
            return culture.CompareInfo.IndexOf(this.FileTable[rowIndex].File, filename, CompareOptions.IgnoreCase);
        }
        #endregion

        #region Find: Displayable
        // Convenience routine for checking to see if the image in the given row is displayable (i.e., not corrupted or missing)
        public bool IsFileDisplayable(int rowIndex)
        {
            if (this.IsFileRowInRange(rowIndex) == false)
            {
                return false;
            }
            return this.FileTable[rowIndex].IsDisplayable(this.FolderPath);
        }

        // Find the next displayable file at or after the provided row in the current image set.
        // If there is no next displayable file, then find the closest previous file before the provided row that is displayable.
        // ReSharper disable once UnusedMember.Global
        public int GetCurrentOrNextDisplayableFile(int startIndex)
        {
            int countAllCurrentlySelectedFiles = this.CountAllCurrentlySelectedFiles;
            for (int index = startIndex; index < countAllCurrentlySelectedFiles; index++)
            {
                if (this.IsFileDisplayable(index))
                {
                    return index;
                }
            }
            for (int index = startIndex - 1; index >= 0; index--)
            {
                if (this.IsFileDisplayable(index))
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
            return (imageRowIndex >= 0) && (imageRowIndex < this.CountAllCurrentlySelectedFiles);
        }

        // Find the image whose ID is closest to the provided ID  in the current image set
        // If the ID does not exist, then return the image row whose ID is just greater than the provided one. 
        // However, if there is no greater ID (i.e., we are at the end) return the last row. 
        public int FindClosestImageRow(long fileID)
        {
            int countAllCurrentlySelectedFiles = this.CountAllCurrentlySelectedFiles;
            for (int rowIndex = 0, maxCount = countAllCurrentlySelectedFiles; rowIndex < maxCount; ++rowIndex)
            {
                if (this.FileTable[rowIndex].ID >= fileID)
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
            ImageRow file = this.FileTable.Find(fileID);
            if (file != null)
            {
                return this.FileTable.IndexOf(file);
            }

            // when sorted by ID ascending so an inexact binary search works
            // Sorting by datetime is usually identical to ID sorting in single camera image sets 
            // But no datetime seed is available if direct ID lookup fails.  Thw API can be reworked to provide a datetime hint
            // if this proves too troublesome.
            int firstIndex = 0;
            int lastIndex = this.CountAllCurrentlySelectedFiles - 1;
            int countAllCurrentlySelectedFiles = this.CountAllCurrentlySelectedFiles;
            while (firstIndex <= lastIndex)
            {
                int midpointIndex = (firstIndex + lastIndex) / 2;
                file = this.FileTable[midpointIndex];
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
            if (this.FileTable == null)
            {
                return;
            }
            this.FileTable.BindDataGrid(this.boundGrid, this.onFileDataTableRowChanged);
        }

        // Generalized form of the above
        public void BindToDataGrid(DataGrid dataGrid, DataRowChangeEventHandler onRowChanged)
        {
            if (this.FileTable == null)
            {
                return;
            }
            this.boundGrid = dataGrid;
            this.onFileDataTableRowChanged = onRowChanged;
            this.FileTable.BindDataGrid(dataGrid, onRowChanged);
        }
        #endregion

        #region Index creation and dropping
        public void IndexCreateForDetectionsAndClassificationsIfNotExists()
        {
            this.Database.IndexCreateIfNotExists(Constant.DatabaseValues.IndexID, Constant.DBTables.Detections, Constant.DatabaseColumn.ID);
            this.Database.IndexCreateIfNotExists(Constant.DatabaseValues.IndexClassificationID, Constant.DBTables.Classifications, Constant.DetectionColumns.DetectionID);
        }

        public void IndexCreateForFileAndRelativePathIfNotExists()
        {
            // If even one of the indexes doesn't exist, they would all have to be created
            if (0 == this.Database.ScalarGetCountFromSelect(Sql.SelectCountFromSqliteMasterWhereTypeEqualIndexAndNameEquals + Sql.Quote("IndexFile")))
            {
                List<Tuple<string, string, string>> tuples = new List<Tuple<string, string, string>>
                {
                    new Tuple<string, string, string>(Constant.DatabaseValues.IndexRelativePath, Constant.DBTables.FileData, Constant.DatabaseColumn.RelativePath),
                    new Tuple<string, string, string>(Constant.DatabaseValues.IndexFile, Constant.DBTables.FileData, Constant.DatabaseColumn.File),
                    new Tuple<string, string, string>(Constant.DatabaseValues.IndexRelativePathFile, Constant.DBTables.FileData, Constant.DatabaseColumn.RelativePath + "," + Constant.DatabaseColumn.File)
                };
                this.Database.IndexCreateMultipleIfNotExists(tuples);
            }
        }

        public void IndexDropForFileAndRelativePathIfExists()
        {
            this.Database.IndexDrop(Constant.DatabaseValues.IndexRelativePath);
            this.Database.IndexDrop(Constant.DatabaseValues.IndexFile);
            this.Database.IndexDrop("IndexRelativePathFile");
        }
        #endregion

        #region File retrieval and manipulation
        public void RenameFileDatabase(string newFileName)
        {
            if (File.Exists(Path.Combine(this.FolderPath, this.FileName)))
            {
                // SAULXXX Should really check for failure, as TryMove will return true/false
               FilesFolders.TryMoveFileIfExists(
                    Path.Combine(this.FolderPath, this.FileName),
                    Path.Combine(this.FolderPath, newFileName));  // Change the file name to the new file name
                this.FileName = newFileName; // Store the file name
                this.Database = new SQLiteWrapper(Path.Combine(this.FolderPath, newFileName));          // Recreate the database connecction
            }
        }

        // Insert one or more rows into a table
        // ReSharper disable once UnusedMember.Local
        private void InsertRows(string table, List<List<ColumnTuple>> insertionStatements)
        {
            this.CreateBackupIfNeeded();
            this.Database.Insert(table, insertionStatements);
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
            MarkerRow markersForImage = this.Markers.Find(fileID);
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
            string markersQuery = Sql.SelectStarFrom + Constant.DBTables.Markers;
            this.Markers = new DataTableBackedList<MarkerRow>(this.Database.GetDataTableFromSelect(markersQuery), row => new MarkerRow(row));
        }

        // Add an empty new row to the marker list if it isnt there. Return true if we added it, otherwise false 
        public bool MarkersTryInsertNewMarkerRow(long imageID)
        {
            if (this.Markers.Find(imageID) != null)
            {
                // There should already be a row for this, so don't create one
                return false;
            }
            List<ColumnTuple> columns = new List<ColumnTuple>()
            {
                new ColumnTuple(Constant.DatabaseColumn.ID, imageID.ToString())
            };

            // Set each marker value to its default
            foreach (ControlRow controlRow in this.Controls)
            {
                if (controlRow.Type == Constant.Control.Counter)
                {
                    columns.Add(new ColumnTuple(controlRow.DataLabel, Constant.DatabaseValues.DefaultMarkerValue));
                }
            }

            List<List<ColumnTuple>> insertionStatements = new List<List<ColumnTuple>>()
            {
                columns
            };
            this.Database.Insert(Constant.DBTables.Markers, insertionStatements);

            // PERFORMANCE: This is inefficient, as it rereads the entire Markers table from the database
            this.MarkersLoadRowsFromDatabase(); // Update the markers list to include this new row

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
            MarkerRow marker = this.Markers.Find(imageID);
            if (marker == null)
            {
                TracePrint.PrintMessage($"Image ID {imageID} missing in markers table.");
                return;
            }

            // Update the database and datatable
            // Note that I repeated the null check here, as for some reason it was still coming up as a CA1062 warning
            ThrowIf.IsNullArgument(markersForCounter, nameof(markersForCounter));
            marker[markersForCounter.DataLabel] = markersForCounter.GetPointList();
            this.UpdateSyncMarkerToDatabase(marker);
        }

        /// <summary>
        /// Set the list of marker points on the current row in the marker table. 
        /// </summary>
        public void MarkersRemoveMarkerRow(long imageID)
        {
            // Find the current row number
            MarkerRow marker = this.Markers.Find(imageID);
            if (marker == null)
            {
                TracePrint.PrintMessage($"Image ID {imageID} missing in markers table.");
                return;
            }
            this.Markers.RemoveAt(this.Markers.IndexOf(marker));
            // Update the database and datatable
            // Note that I repeated the null check here, as for some reason it was still coming up as a CA1062 warning
            List<string> whereClauses = new List<string>
            {
               Constant.DatabaseColumn.ID + Sql.Equal + imageID
            };
            this.Database.Delete(Constant.DBTables.Markers, whereClauses);
        }
        #endregion

        #region ImageSet manipulation
        private void ImageSetLoadFromDatabase()
        {
            string imageSetQuery = Sql.SelectStarFrom + Constant.DBTables.ImageSet + Sql.Where + Constant.DatabaseColumn.ID + " = " + Constant.DatabaseValues.ImageSetRowID;
            DataTable imageSetTable = this.Database.GetDataTableFromSelect(imageSetQuery);
            this.ImageSet = new ImageSetRow(imageSetTable.Rows[0]);
            imageSetTable.Dispose();
        }

        // Try getting the version number as recorded in the ImageSet datatable.
        // ReSharper disable once UnusedMember.Local
        private bool TryGetImageSetVersionNumber(out string versionNumber, bool forceUpdate)
        {
            versionNumber = string.Empty;
            if (this.Database == null)
            {
                // The database hasn't been loaded yet
                return false;
            }

            if (this.ImageSet == null || forceUpdate)
            {
                // The image set hasn't been loaded yet, so try to load it
                this.ImageSetLoadFromDatabase();
            }
            if (false == this.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.VersionCompatabily))
            {
                // As there is no version column, this must be a really early version.
                // Return some very low number, which should trigger most checks and updates
                versionNumber = Constant.DatabaseValues.VersionNumberMinimum;
                return true;
            }

            if (this.ImageSet == null)
            {
                // This shouldn't happen
                TracePrint.NullException(nameof(this.ImageSet));
                versionNumber = Constant.DatabaseValues.VersionNumberMinimum;
                return true;
            }
            versionNumber = this.ImageSet.VersionCompatability;
            return true;
        }
        #endregion

        #region DETECTION - Populate the Database (with progress bar)
        // To help determine periodic updates to the progress bar 
        private DateTime lastRefreshDateTime = DateTime.Now;
        protected bool ReadyToRefresh()
        {
            TimeSpan intervalFromLastRefresh = DateTime.Now - this.lastRefreshDateTime;
            if (intervalFromLastRefresh > Constant.ThrottleValues.ProgressBarRefreshInterval)
            {
                this.lastRefreshDateTime = DateTime.Now;
                return true;
            }
            return false;
        }

        private void PStream_BytesRead(object sender, ProgressStreamReportEventArgs args)
        {
            Progress<ProgressBarArguments> progressHandler = new Progress<ProgressBarArguments>(value =>
            {
                FileDatabase.UpdateProgressBar(GlobalReferences.BusyCancelIndicator, value.PercentDone, value.Message, value.IsCancelEnabled, value.IsIndeterminate);
            });
            IProgress<ProgressBarArguments> progress = progressHandler;

            long current = args.StreamPosition;
            long total = args.StreamLength;
            double p = current / ((double)total);
            if (this.ReadyToRefresh())
            {
                // Update the progress bar
                progress.Report(new ProgressBarArguments((int)(100 * p), "Reading the recognition file...", true, false));
                Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
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
            this.Database.Insert(Constant.DBTables.Detections, detectionInsertionStatements);
        }

        public void InsertClassifications(List<List<ColumnTuple>> classificationInsertionStatements)
        {
            this.Database.Insert(Constant.DBTables.Classifications, classificationInsertionStatements);
        }

        // Try to read the recognition data from the Json file into the Recognizer structure
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

        public async Task<RecognizerImportResultEnum> PopulateRecognitionTablesFromRecognizerAsync(Recognizer jsonRecognizer, string path, List<string> foldersInDBListButNotInJSon, List<string> foldersInJsonButNotInDB, List<string> foldersInBoth, bool tryMerge, IProgress<ProgressBarArguments> progress, CancellationTokenSource cancelTokenSource)
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
                    Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then

                    // Fill in the jsonRecognizer info structure as needed to ensure it is filled in with reasonable values
                    PopulateRecognizerInfoWithDefaultValuesAsNeeded(jsonRecognizer.info);


                    // flag indicating if the detections database already exists

                    bool clearDBRecognitionData = true;

                    // the starting index to be used for inserts using the DetectionID
                    int dbStartingDetectionID = 1;
                    int dbStartingClassificationID = 1;

                    // Resetting these tables to null will force reading the new values into them
                    // TODO: Put this somewhere else in case the user aborts the update!
                    // Also Update this comment: Resetting these tables to null will force reading the new values into them
                    this.detectionDataTable = null; // to force repopulating the data structure if it already exists.
                    this.detectionCategoriesDictionary = null;
                    this.classificationCategoriesDictionary = null;
                    this.classificationsDataTable = null;

                    // If we were told to tryMerge and detections exist, we merge the json file detecctions with the db detections. If so, we also have to do some error checking and possibly updates
                    // for the detection and classification categories and the info structure
                    bool mergeDetections = tryMerge && this.DetectionsExists();
                    if (mergeDetections)
                    {
                        // Generate several dictionaries reflecting the contents of several detection tables as currently held in the database
                        Dictionary<string, string> dbDetectionCategories = new Dictionary<string, string>();
                        Dictionary<string, string> dbClassificationCategories = new Dictionary<string, string>();
                        Dictionary<string, object> dbInfoDictionary = new Dictionary<string, object>();
                        RecognitionUtilities.GenerateRecognitionDictionariesFromDB(this.Database, dbInfoDictionary, dbDetectionCategories, dbClassificationCategories);

                        // Step 1. Generate a new info structure that is a best effort combination of the db and json info structure,
                        //         and then update the jsonRecognizer to match that. Note the we do it even if no update is really needed, as its lightweight
                        Dictionary<string, object> newInfoDict = RecognitionUtilities.GenerateBestRecognitionInfoFromTwoInfos(dbInfoDictionary, jsonRecognizer.info);
                        jsonRecognizer.info.detector = (string)newInfoDict[Constant.InfoColumns.Detector];
                        jsonRecognizer.info.detector_metadata.megadetector_version = (string)newInfoDict[Constant.InfoColumns.DetectorVersion];
                        jsonRecognizer.info.detection_completion_time = (string)newInfoDict[Constant.InfoColumns.DetectionCompletionTime];
                        jsonRecognizer.info.classifier = (string)newInfoDict[Constant.InfoColumns.Classifier];
                        jsonRecognizer.info.classification_completion_time = (string)newInfoDict[Constant.InfoColumns.ClassificationCompletionTime];
                        jsonRecognizer.info.detector_metadata.typical_detection_threshold = (float)newInfoDict[Constant.InfoColumns.TypicalDetectionThreshold];
                        jsonRecognizer.info.detector_metadata.conservative_detection_threshold = (float)newInfoDict[Constant.InfoColumns.ConservativeDetectionThreshold];
                        jsonRecognizer.info.classifier_metadata.typical_classification_threshold = (float)newInfoDict[Constant.InfoColumns.TypicalClassificationThreshold];


                        if (cancelTokenSource.Token.IsCancellationRequested)
                        {
                            return RecognizerImportResultEnum.Cancelled;
                        }

                        // Step 2. Merge the DB and Json detection categories if they are compatable
                        if (dbDetectionCategories.ContainsKey("0"))
                        {
                            // Remove the 0: Empty key/value pair, as that is artificially generated by timelapse and is not in the JSON
                            dbDetectionCategories.Remove("0");
                        }
                        if (Util.Dictionaries.MergeDictionaries(dbDetectionCategories, jsonRecognizer.detection_categories, out Dictionary<string, string> mergedDetectionCategories))
                        {
                            // Debug.Print("merged succeeded for detection categories");
                            jsonRecognizer.detection_categories = new Dictionary<string, string>(mergedDetectionCategories);
                        }
                        else
                        {
                            // Debug.Print("merged failed for detection categories");
                            return RecognizerImportResultEnum.IncompatableDetectionCategories;
                        }

                        // Step 3. Check if the new classfication categories are the same or at least a subset of the old ones.
                        // If they are, then we can just use the existing DB categories as they will apply to the new categories.
                        if (Util.Dictionaries.MergeDictionaries(dbClassificationCategories, jsonRecognizer.classification_categories, out Dictionary<string, string> mergedClassificationCategories))
                        {
                            // Debug.Print("merged succeeded for classification categories");
                            jsonRecognizer.classification_categories = new Dictionary<string, string>(mergedClassificationCategories);
                        }
                        else
                        {
                            // Debug.Print("merged failed for classification categories");
                            return RecognizerImportResultEnum.IncompatableClassificationCategories;
                        }
                        clearDBRecognitionData = false; // just to make it more readable

                        progress.Report(new ProgressBarArguments(0, "Examining database recognitions (retrieving them)...", true, false));
                        if (cancelTokenSource.Token.IsCancellationRequested)
                        {
                            return RecognizerImportResultEnum.Cancelled;
                        }
                        Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and the
                        DataTable dbDetectionTable = this.Database.GetDataTableFromSelect(
                                    Sql.Select + Constant.DatabaseColumn.File + Sql.Comma
                                    + Constant.DatabaseColumn.RelativePath + Sql.Comma
                                    + Constant.DBTables.Detections + ".*"
                                    + Sql.From + Constant.DBTables.Detections
                                    + Sql.InnerJoin + Constant.DBTables.FileData + Sql.On
                                    + Constant.DBTables.FileData + Sql.Dot + Constant.DatabaseColumn.ID
                                    + Sql.Equal
                                    + Constant.DBTables.Detections + Sql.Dot + Constant.DatabaseColumn.ID);
                        if (cancelTokenSource.Token.IsCancellationRequested)
                        {
                            return RecognizerImportResultEnum.Cancelled;
                        }

                        // As we will be inserting records, get the max DetectionID, and add 1 to it. This will be the starting detectionID for insertions

                        int i = 0;
                        int count = dbDetectionTable.Rows.Count;
                        foreach (DataRow dr in dbDetectionTable.Rows)
                        {
                            dbStartingDetectionID = Math.Max(Convert.ToInt32(dr["detectionID"]), dbStartingDetectionID);
                            if (i % 10000 == 0)
                            {
                                if (cancelTokenSource.Token.IsCancellationRequested)
                                {
                                    return RecognizerImportResultEnum.Cancelled;
                                }
                                int percent = Convert.ToInt32(i * 100.0 / count);
                                progress.Report(new ProgressBarArguments(percent,
                                    $"Examining database recognitions ({i:N2}/{count:N2})...", true, false));
                                Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and the
                            }
                            i++;
                        }
                        dbStartingDetectionID++;

                        // As we may be inserting classification records as well, get the max ClassificationID, and add 1 to it. This will be the starting classificationID for insertions
                        if (this.Database.TableExistsAndNotEmpty(Constant.DBTables.Classifications))
                        {
                            dbStartingClassificationID = this.Database.ScalarGetMaxIntValue(Constant.DBTables.Classifications, Constant.ClassificationColumns.ClassificationID);
                            dbStartingClassificationID++;
                        }

                        // Foreach  detection, check if it exists in the database detection table.
                        // If it does, delete all references to that file (via the ID) in the database
                        List<string> queries = new List<string>();

                        i = 0;
                        count = jsonRecognizer.images.Count;
                        foreach (image image in jsonRecognizer.images)
                        {
                            // check whether the image file in the json exists in the recognizer table.
                            string file = Path.GetFileName(image.file);
                            string relativePath = Path.GetDirectoryName(image.file);

                            if (i % 1000 == 0)
                            {
                                if (cancelTokenSource.Token.IsCancellationRequested)
                                {
                                    return RecognizerImportResultEnum.Cancelled;
                                }
                                int percent = Convert.ToInt32(i * 100.0 / count);
                                progress.Report(new ProgressBarArguments(percent,
                                    $"Comparing recognitions ({i:N0}/{count:N0})...", true, false));
                                Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and the
                            }
                            i++;

                            DataRow[] rows = dbDetectionTable.Select(Constant.DatabaseColumn.File + Sql.Equal + Sql.Quote(file) + Sql.And + Constant.DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(relativePath));
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
                                string query = Sql.DeleteFrom + Constant.DBTables.Detections + Sql.Where + Constant.DatabaseColumn.ID + Sql.Equal + row[Constant.DatabaseColumn.ID];
                                queries.Add(query);
                            }
                        }

                        if (queries.Count > 0)
                        {
                            // If the index wasn't created previously, make sure its there as otherwise its painfully slow.
                            this.IndexCreateForDetectionsAndClassificationsIfNotExists();
                            // Delete these detections and classifications
                            this.Database.ExecuteNonQueryWrappedInBeginEnd(queries, progress, "Removing unneeded recognitions", 1000);
                        }
                        // At this point, we have deleted the detections and classifications from those images that are both in the
                        // db and the json, which means were are ready to replace them. 
                    }

                    // At this point the db no longer contains detections for images referenced in the json file

                    // PERFORMANCE This check is likely somewhat slow. Check it on large detection files / dbs 
                    if (this.CompareRecognizerAndDBFolders(jsonRecognizer, foldersInDBListButNotInJSon, foldersInJsonButNotInDB, foldersInBoth) == false)
                    {
                        // No folders in the detections match folders in the databases. Abort without doing anything.
                        return RecognizerImportResultEnum.Failure;
                    }

                    // Prepare the various detection tables. 
                    RecognitionDatabases.PrepareRecognitionTablesAndColumns(this.Database, this.DetectionsExists(), clearDBRecognitionData);

                    // PERFORMANCE This method does two things:
                    // - it walks through the jsonRecognizer data structure to construct sql insertion statements
                    // - it invokes the actual insertion in the database.
                    // Both steps are very slow with a very large JSON of detections that matches folders of images.
                    // (e.g., 225 seconds for 2,000,000 images and their detections). Note that I batch insert 50,000 statements at a time. 

                    // Update the progress bar and populate the detection tables
                    progress.Report(new ProgressBarArguments(0, "Adding new recognitions...", false, true));
                    Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
                    RecognitionDatabases.PopulateTables(jsonRecognizer, this, this.Database, string.Empty, dbStartingDetectionID, dbStartingClassificationID, progress);

                    // DetectionExists needs to be primed if it is to save its DetectionExists state
                    this.DetectionsExists(true);
                    return RecognizerImportResultEnum.Success;
                }
                catch
                {
                    return RecognizerImportResultEnum.Failure;
                }
            }).ConfigureAwait(true);
            return result;
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
                info.detector = Constant.RecognizerValues.MDVersionUnknown;
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
                    info.detector_metadata.megadetector_version = Constant.RecognizerValues.MDVersionUnknown;
                }
                if (info.detector_metadata.typical_detection_threshold == null)
                {
                    info.detector_metadata.typical_detection_threshold = Constant.RecognizerValues.DefaultTypicalDetectionThresholdIfUnknown;
                }
                if (info.detector_metadata.conservative_detection_threshold == null)
                {
                    info.detector_metadata.conservative_detection_threshold = Constant.RecognizerValues.DefaultConservativeDetectionThresholdIfUnknown;
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
            List<string> FoldersInDBList = this.GetDistinctValuesInColumn(Constant.DBTables.FileData, Constant.DatabaseColumn.RelativePath).Select(i => i.ToString()).ToList();
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
                if (foldersInRecognizerList.Contains(folderpath) == false)
                {
                    foldersInRecognizerList.Add(folderpath);
                }
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
        // As part of the, create a DetectionTable in memory that mirrors the database table
        public DataRow[] GetDetectionsFromFileID(long fileID)
        {
            if (this.detectionDataTable == null)
            {
                // PERFORMANCE 0 or more detections can be associated with every image. THus we should expect the number of detections could easily be two or three times the 
                // number of images. With very large databases, retrieving the datatable of detections can be very slow (and can consume significant memory). 
                // While this operation is only done once per image set session, it is still expensive. I suppose I could get it from the database on the fly, but 
                // its important to show detection data (including bounding boxes) as rapidly as possible, such as when a user is quickly scrolling through images.
                // So I am not clear on how to optimize this (although I suspect a thread running in the background when Timelapse is loaded could perhaps do this)
                this.RefreshDetectionsDataTable();
            }
            // Retrieve the detection from the in-memory datatable.
            // Note that because IDs are in the database as a string, we convert it
            // PERFORMANCE: This takes a bit of time, not much... but could be improved. Not sure if there is an index automatically built on it. If not, do so.
            if (this.detectionDataTable == null)
            {
                // Shouldn't happen as the above should reset it
                TracePrint.NullException(nameof(this.detectionDataTable));
                return null;
            }
            return this.detectionDataTable.Select(Constant.DatabaseColumn.ID + Sql.Equal + fileID);
        }

        // Get the detections associated with the current file, if any
        public DataRow[] GetClassificationsFromDetectionID(long detectionID)
        {
            if (this.classificationsDataTable == null)
            {
                //this.classificationsDataTable = this.Database.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.Classifications);
                this.RefreshClassificationsDataTable();
            }

            if (this.classificationsDataTable == null)
            {
                // Shouldn't happen as the above should reset it
                TracePrint.NullException(nameof(this.classificationsDataTable));
                return null;
            }
            // Note that because IDs are in the database as a string, we convert it
            return this.classificationsDataTable.Select(Constant.ClassificationColumns.DetectionID + Sql.Equal + detectionID);
        }

        // Return the label that matches the detection category 
        public string GetDetectionLabelFromCategory(string category)
        {
            this.CreateDetectionCategoriesDictionaryIfNeeded();
            return this.detectionCategoriesDictionary.TryGetValue(category, out string value) ? value : string.Empty;

        }

        // Get the TypicalDetectionThreshold from the Detection Info table. 
        // If we cannot, return the default value.
        public float GetTypicalDetectionThreshold()
        {
            float? x = null;
            try
            {
                if (this.Database.TableExists(Constant.DBTables.Info) && this.Database.SchemaIsColumnInTable(Constant.DBTables.Info, Constant.InfoColumns.TypicalDetectionThreshold))
                {
                    x = this.Database.ScalarGetFloatValue(Constant.DBTables.Info, Constant.InfoColumns.TypicalDetectionThreshold);
                }
                return x ?? Constant.RecognizerValues.DefaultTypicalDetectionThresholdIfUnknown;
            }
            catch
            {
                return Constant.RecognizerValues.DefaultTypicalDetectionThresholdIfUnknown;
            }
        }

        // Get the GetTypicalClassificationThreshold from the Detection Info table. 
        // If we cannot, return the default value
        public float GetTypicalClassificationThreshold()
        {
            float? x = null;
            try
            {
                if (this.Database.TableExists(Constant.DBTables.Info) && this.Database.SchemaIsColumnInTable(Constant.DBTables.Info, Constant.InfoColumns.TypicalClassificationThreshold))
                {
                    x = this.Database.ScalarGetFloatValue(Constant.DBTables.Info, Constant.InfoColumns.TypicalClassificationThreshold);
                }
                return x ?? Constant.RecognizerValues.DefaultTypicalClassificationThresholdIfUnknown;
            }
            catch
            {
                return Constant.RecognizerValues.DefaultTypicalClassificationThresholdIfUnknown;
            }
        }

        // Get the ConservativeDetectionThreshold from the Detection Info table. 
        // If we cannot, return the default value
        public float GetConservativeDetectionThreshold()
        {
            float? x = null;
            try
            {
                if (this.Database.TableExists(Constant.DBTables.Info) && this.Database.SchemaIsColumnInTable(Constant.DBTables.Info, Constant.InfoColumns.ConservativeDetectionThreshold))
                {
                    x = this.Database.ScalarGetFloatValue(Constant.DBTables.Info, Constant.InfoColumns.ConservativeDetectionThreshold);
                }
                return x ?? Constant.RecognizerValues.DefaultConservativeDetectionThresholdIfUnknown;
            }
            catch
            {
                return Constant.RecognizerValues.DefaultConservativeDetectionThresholdIfUnknown;
            }
        }

        public void CreateDetectionCategoriesDictionaryIfNeeded()
        {
            // Null means we have never tried to create the dictionary. Try to do so.
            if (this.detectionCategoriesDictionary == null)
            {
                this.detectionCategoriesDictionary = new Dictionary<string, string>();
                try
                {
                    using (DataTable dataTable = this.Database.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.DetectionCategories))
                    {
                        int dataTableRowCount = dataTable.Rows.Count;
                        for (int i = 0; i < dataTableRowCount; i++)
                        {
                            DataRow row = dataTable.Rows[i];
                            this.detectionCategoriesDictionary.Add((string)row[Constant.DetectionCategoriesColumns.Category], (string)row[Constant.DetectionCategoriesColumns.Label]);
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
                this.CreateDetectionCategoriesDictionaryIfNeeded();
                // A lookup dictionary should now exists, so just return the category value.
                string myKey = this.detectionCategoriesDictionary.FirstOrDefault(x => x.Value == label).Key;
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
            this.CreateDetectionCategoriesDictionaryIfNeeded();
            foreach (KeyValuePair<string, string> entry in this.detectionCategoriesDictionary)
            {
                labels.Add(entry.Value);
            }
            return labels;
        }

        // Create the classification category dictionary to mirror the detection table
        public void CreateClassificationCategoriesDictionaryIfNeeded()
        {
            // Null means we have never tried to create the dictionary. Try to do so.
            if (this.classificationCategoriesDictionary == null)
            {
                this.classificationCategoriesDictionary = new Dictionary<string, string>();
                try
                {
                    using (DataTable dataTable = this.Database.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.ClassificationCategories))
                    {
                        int dataTableRowCount = dataTable.Rows.Count;
                        for (int i = 0; i < dataTableRowCount; i++)
                        {
                            DataRow row = dataTable.Rows[i];
                            this.classificationCategoriesDictionary.Add((string)row[Constant.ClassificationCategoriesColumns.Category], (string)row[Constant.ClassificationCategoriesColumns.Label]);
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
            this.CreateClassificationCategoriesDictionaryIfNeeded();
            foreach (KeyValuePair<string, string> entry in this.classificationCategoriesDictionary)
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
                this.CreateClassificationCategoriesDictionaryIfNeeded();
                // A lookup dictionary should now exists, so just return the category value.
                return this.classificationCategoriesDictionary.TryGetValue(category, out string value) ? value : string.Empty;
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
                this.CreateClassificationCategoriesDictionaryIfNeeded();
                // At this point, a lookup dictionary already exists, so just return the category number.
                string myKey = this.classificationCategoriesDictionary.FirstOrDefault(x => x.Value == label).Key;
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
            return this.DetectionsExists(false);
        }
        public bool DetectionsExists(bool forceQuery)
        {
            if (forceQuery || this.detectionExists == null)
            {
                this.detectionExists = this.Database.TableExistsAndNotEmpty(Constant.DBTables.Detections);
            }
            return this.detectionExists == true;
        }
        #endregion

        #region Detections: Counting
        // Given a Counter DataLabel, count the number of detections associated with each image, and set that image's counter to that count
        public bool DetectionsAddCountToCounter(string counterDataLabel, double confidenceValue, IProgress<ProgressBarArguments> progress)
        {
            if (this.Database == null || false == this.DetectionsExists())
            {
                return false;
            }
            progress.Report(new ProgressBarArguments(0, "Counting detections...", false, true));
            Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then

            Dictionary<long, int> dict = new Dictionary<long, int>();
            foreach (ImageRow image in this.FileTable)
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
            Thread.Sleep(Constant.ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
            if (columnTuplesWithWhereList.Count > 0)
            {
                // Update the Database
                this.UpdateFiles(columnTuplesWithWhereList);
                // Force an update of the current image in case the current values have changed

            }
            return true;
        }
        #endregion

        #region BoundingBox Thresholds

        public bool TrySetBoundingBoxDisplayThreshold(float threshold)
        {
            if (false == this.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.BoundingBoxDisplayThreshold))
            {
                return false;
            }
            this.Database.Update(Constant.DBTables.ImageSet, new ColumnTuple(Constant.DatabaseColumn.BoundingBoxDisplayThreshold, threshold));
            return true;
        }

        public bool TryGetBoundingBoxDisplayThreshold(out float threshold)
        {
            threshold = Constant.RecognizerValues.Undefined;
            if (false == this.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.BoundingBoxDisplayThreshold))
            {
                return false;
            }

            float? fthreshold = this.Database.ScalarGetFloatValue(Constant.DBTables.ImageSet, Constant.DatabaseColumn.BoundingBoxDisplayThreshold);
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
            if (sqliteWrapper.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.QuickPasteTerms) == false)
            {
                // The column isn't in the table, so give up
                return string.Empty;
            }

            List<object> listOfObjects = sqliteWrapper.GetDistinctValuesInColumn(Constant.DBTables.ImageSet, Constant.DatabaseColumn.QuickPasteTerms);
            if (listOfObjects.Count == 1)
            {
                return (string)listOfObjects[0];
            }
            return string.Empty;
        }
        #endregion

        #region CustomSelection: Restoring from JSon
        // Restor the custom selection from the Json stored in the image set table
        public FileSelectionEnum GetCustomSelectionFromJSON()
        {
            // We put this in a try/catch. If anything fails, we just revert to the default custom selection (All)
            try
            {
                // Get the stored custom selection, and determine custom selection state (all, relativepath or custom).
                // Ig there is a problem in the customSelectionFromJson (eg if its null or has no search terms), it will return ALL
                CustomSelection customSelectionFromJson = JsonConvert.DeserializeObject<CustomSelection>(this.ImageSet.SearchTermsAsJSON);

                // Various checks (including null and several settings that could be confusing to the user)
                if (customSelectionFromJson == null ||
                    customSelectionFromJson.SearchTerms == null ||
                    customSelectionFromJson.SearchTerms.Count == 0 ||
                    customSelectionFromJson.RandomSample != 0 ||
                    customSelectionFromJson.ShowMissingDetections ||
                    customSelectionFromJson.EpisodeShowAllIfAnyMatch)
                {
                    // Didn't pass the test. Use the default
                    this.CustomSelection = new CustomSelection(this.Controls);
                    return FileSelectionEnum.All;
                }

                // At this point, customSelectionFromJson should have a valid value
                List<SearchTerm> stlFromJson = customSelectionFromJson.SearchTerms;

                // Check various recognition settings.
                // If the JSON says recognition is enabled and being used, check if the recognition data is actually there for us
                bool parseResultDetectionCategory = Int32.TryParse(customSelectionFromJson.DetectionSelections.DetectionCategory, out int detectionCategoryAsInt);
                bool parseResultClassificaitonCategory = Int32.TryParse(customSelectionFromJson.DetectionSelections.ClassificationCategory, out int classificationCategoryAsInt);
                if (customSelectionFromJson.DetectionSelections.Enabled &&
                      customSelectionFromJson.DetectionSelections.UseRecognition &&
                      false == this.DetectionsExists() ||
                      parseResultDetectionCategory == false ||
                      detectionCategoryAsInt >= this.detectionCategoriesDictionary?.Count ||
                      parseResultClassificaitonCategory == false ||
                      classificationCategoryAsInt >= this.detectionCategoriesDictionary?.Count
                    )
                {
                    // Didn't pass the test. Use the default
                    this.CustomSelection = new CustomSelection(this.Controls);
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
                foreach (ControlRow control in this.Controls)
                {
                    dataLabelsFromControls.Add(control.DataLabel);
                }
                List<string> firstNotSecond = dataLabelsFromJson.Except(dataLabelsFromControls).ToList();
                List<string> secondNotFirst = dataLabelsFromControls.Except(dataLabelsFromJson).ToList();
                if (firstNotSecond.Count != 0 || secondNotFirst.Count != 0)
                {
                    // Didn't pass the test as that data label is not present. Use the default
                    this.CustomSelection = new CustomSelection(this.Controls);
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
                        if (stFromJson.DataLabel == Constant.DatabaseColumn.DeleteFlag)
                        {
                            deleteFlagIsUsed = true;
                        }
                        else if (stFromJson.DataLabel == Constant.DatabaseColumn.RelativePath)
                        {
                            relativePathIsUsed = true;
                        }
                    }

                    // Fixed choice lists must match, and the value must be in the list 
                    if (stFromJson.ControlType == Constant.Control.FixedChoice)
                    {
                        ControlRow row = this.GetControlFromTemplateTable(stFromJson.DataLabel);
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
                            this.CustomSelection = new CustomSelection(this.Controls);
                            return FileSelectionEnum.All;
                        }
                    }
                }

                // We have a valid custom selection from the Json, so let's use it.
                this.CustomSelection = customSelectionFromJson;

                // Set the FileSelectionEnum state
                if (this.CustomSelection.DetectionSelections.UseRecognition)
                {
                    // Recognition is always custom
                    return FileSelectionEnum.Custom;
                }
                else if (numberSearchTermsUsed > 1 && this.CustomSelection.TermCombiningOperator != CustomSelectionOperatorEnum.And)
                {
                    // the operator only matters if more than  one term being used
                    return FileSelectionEnum.Custom;
                }
                else if (relativePathIsUsed && numberSearchTermsUsed == 1)
                {
                    // As only the relative path is set, we must be using folders
                    return FileSelectionEnum.Folders;
                }
                else if (deleteFlagIsUsed && numberSearchTermsUsed == 1)
                {
                    // As only the DeleteFlag is set, we must be using MarkedForDeletion
                    return FileSelectionEnum.MarkedForDeletion;
                }
                else if (numberSearchTermsUsed > 0)
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
                this.CustomSelection = new CustomSelection(this.Controls);
                return FileSelectionEnum.All;
            }
        }
        #endregion

        #region Disposing
        protected override void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.FileTable?.Dispose();
                this.Markers?.Dispose();
                this.detectionDataTable?.Dispose();
                this.classificationsDataTable?.Dispose();
            }

            base.Dispose(disposing);
            this.disposed = true;
        }

        public void DisposeAsNeeded()
        {
            try
            {
                // Release the file table
                this.FileTable?.DisposeAsNeeded(this.onFileDataTableRowChanged);
                this.FileTable = null;
                this.Markers?.DisposeAsNeeded(null);
                this.Markers = null;
                this.Controls?.DisposeAsNeeded(null);

                // Release various data tables
                this.detectionDataTable?.Clear();
                this.detectionDataTable = null;
                this.classificationsDataTable?.Clear();
                this.classificationsDataTable = null;

                // Release the bound grid
                if (this.boundGrid != null)
                {
                    this.boundGrid.DataContext = null;
                    this.boundGrid.ItemsSource = null;
                    this.boundGrid = null;
                }

                // Release various dictionaries
                this.classificationCategoriesDictionary = null;
                this.detectionCategoriesDictionary = null;
            }
            catch
            {
                Debug.Print("Failed in FileDatabase:DisposeAsNeeded");
            }
        }
        #endregion
    }
}