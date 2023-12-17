using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Util;

namespace Timelapse.Database
{
    // Timelapse Template Database.
    // A high-level discussion of the logic is below.
    // Both the .tdb and .ddb file contain (or create) templates as tables. Thus the TemplateDatabase defines and manages what is common to both. 
    // This is why FileDatabase inherits a TemplateDatabase

    // Templates exist in both the .tdb and .ddb file. The .tdb template is (more or less) the 'master', while
    // the one in the .ddb is there both for convenience and to check for differences in case the template has been updated and
    // differs from what is stored in the .ddb database (which would result in a dialog where the user would accept some of the changes).
    // Timelapse maintains a 'Control' data structure (a DataTableBackedList) mirroring the template contents, which is also bound
    // to a datagrid so that a user can see its contents.
    // When a template is modified, Timelapse updates the template data table, and then updates the Control data structure to reflect that table's contents.

    // When a .tdb is initially created, 
    // - an empty template table is created
    // - the standard control roles are added

    // - When a new ddb database is created, an empty template is first created, and then loaded with the contents of the template table from the .tdb file.
    // - When an existing ddb database is opened, its template is checked against the template found in the tdb database
    public class TemplateDatabase : IDisposable
    {
        #region Public / Protected Properties

        // Controls reflect the contents of the template table
        public DataTableBackedList<ControlRow> Controls { get; private set; }

        /// <summary>Get the file name of the image database on disk.</summary>
        public string FilePath { get; }

        // The database holding the template  (which could be a tdb or ddb file)
        public SQLiteWrapper Database { get; set; }

        #endregion

        #region Private Variables

        private bool disposed;
        private DataGrid editorDataGrid;
        public DateTime mostRecentBackup;
        private DataRowChangeEventHandler onTemplateTableRowChanged;

        #endregion

        #region Constructors

        // This is normally invoked as follows.
        // A bit confusing unless you know that the TemplateDatabase is used to open and get a reference to both a .tdb or a .ddb database. 
        // Its used for both, as both require us to get and store the common Controls (defined in the Template table) as a datatableBackedList data structure.
        // 1. When a template .tdb is opened (either from the Editor or Timelapse)
        // 2. When a .ddb file is opened
        //    a) initially as a check to see if .ddb template needs updating i.e., to compare templates. The database is then closed
        //    b) later to actually open the .ddb (which as a side effect loads all the required template info)
        protected TemplateDatabase(string filePath)
        {
            this.disposed = false;
            this.mostRecentBackup = FileBackup.GetMostRecentBackup(filePath);

            // open or create database
            this.Database = new SQLiteWrapper(filePath);
            this.FilePath = filePath;
        }

        #endregion

        #region Public Async Tasks - TryCreateOrOpen, Private DoCreateOpen
        // Try to create or open a template database from the provided tdb file path
        // This is only invoked when either loading images, or when attempting a merge
        public static async Task<Tuple<bool, TemplateDatabase>> TryCreateOrOpenAsync(string filePath)
        {
            // Follow the MSDN design pattern for returning an IDisposable: https://www.codeproject.com/Questions/385273/Returning-a-Disposable-Object-from-a-Method
            TemplateDatabase disposableTemplateDB = null;
            try
            {
                disposableTemplateDB = await DoCreateOrOpenAsync(filePath).ConfigureAwait(true);
                TemplateDatabase returnableTemplateDB = disposableTemplateDB;
                // the returnableTemplateDB will be null if its not a valid template, e.g., if no TemplateTable exists in it
                bool successOrFail = returnableTemplateDB != null;
                return new Tuple<bool, TemplateDatabase>(successOrFail, returnableTemplateDB);
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage($"Failure in TryCreateOpen. {exception}");
                return new Tuple<bool, TemplateDatabase>(false, null);
            }
            finally
            {
                disposableTemplateDB?.Dispose();
            }
        }

        // Create or open a template database from the provided tdb file path
        // This is only invoked for .tdb files by the above method. It:
        // - creates a new database if the file doesn't exist,
        // - opens an existing database if the file exists and is valid.
        // It is invoked by only the prior method, or from the Editor
        private static async Task<TemplateDatabase> DoCreateOrOpenAsync(string filePath)
        {
            // check if its a new or existing database file before instantiating the database as SQL wrapper
            bool newDatabase = !File.Exists(filePath);

            TemplateDatabase templateDatabase = new TemplateDatabase(filePath);
            if (newDatabase)
            {
                // Its a new database file. Initialize it
                await templateDatabase.OnDatabaseCreatedAsync(null).ConfigureAwait(true);
            }
            else
            {
                // Its an existing database file.  
                // Check if its valid.
                //   We do this by checking both the database integrity and if it has a TemplateTable.
                //   While a minimal check, it suffices in most cases.
                //   Note that the check may be redundant as one of the calling methods may have already done it, but since its fast to do we don't bother factoring it out. 
                if (templateDatabase.Database.PragmaGetQuickCheck() == false || templateDatabase.TableExists(Constant.DBTables.Template) == false)
                {
                    templateDatabase.Dispose();
                    return null;
                }

                // if it's an existing database check if it needs updating to current structure and load data tables
                await templateDatabase.OnExistingDatabaseOpenedAsync(null, null).ConfigureAwait(true);
            }
            return templateDatabase;
        }
        #endregion

        #region OnDatabaseCreated, UpgradeDatabasesAndCompareTemplates, OnExistingDatabaseOpened
        
        // This is called when a new database file (which could be a tdb or ddb) is created. 
        protected virtual async Task OnDatabaseCreatedAsync(TemplateDatabase existingTemplateTable)
        {
            // create the template table
            await Task.Run(() =>
            {
                // Passing an existing template table normally occurs when a ddb file is created,
                // where we want to clone the exiting template table from the tdb file.
                if (existingTemplateTable != null)
                {
                    // Create and empty template table with the template schema
                    // and populate it from the existing template table
                    this.CreateEmptyTemplateTable(this.Database);
                    this.RepopulateTemplateTableWithControls(existingTemplateTable.Controls);
                    return;
                }

                // WE ARE IN A TDB FILE as  no existing template table was passed.
                // This means we need to create the template table in a tdb database
                // and populate it with the standard contorols.

                // 1. Create a template table and populate it with the standard controls
                this.CreateEmptyTemplateTable(this.Database);
                this.PopulateTemplateTableWithStandardControls(this.Database);

                // 2. Populate the in-memory version of the template table
                this.LoadControlsFromTemplateDBSortedByControlOrder();

                // 3. Add and populate the TemplateInfo table
                this.CreateAndPopulateTemplateInfoTable(this.Database);

            }).ConfigureAwait(true);
        }

        protected virtual async Task OnExistingDatabaseOpenedAsync(TemplateDatabase other, TemplateSyncResults templateSyncResults)
        {
            await Task.Run(this.LoadControlsFromTemplateDBSortedByControlOrder).ConfigureAwait(true);
        }

        protected virtual async Task UpgradeDatabasesAndCompareTemplatesAsync(TemplateDatabase other, TemplateSyncResults templateSyncResults)
        {
            await Task.Run(this.LoadControlsFromTemplateDBSortedByControlOrder).ConfigureAwait(true);
        }

      

        #endregion

        #region Public Methods - Boolean tests - Exists tables, Is database valid
        public bool TableExists(string dataTable)
        {
            return this.Database.TableExists(dataTable);
        }

        // Check if the database is valid. 
        // ReSharper disable once UnusedMember.Global
        public bool IsDatabaseFileValid(string filePath, string tableNameToCheck)
        {
            // check if a database file exists, and if so that it is not corrupt
            if (!File.Exists(filePath))
            {
                return false;
            }

            // The database file exists. However, we still need to check if its valid. 
            using (TemplateDatabase database = new TemplateDatabase(filePath))
            {
                if (database.Database == null)
                {
                    return false;
                }

                // We do this by checking the database integrity (which may raise an internal exception) and if that is ok, by checking if it has a TemplateTable. 
                if (this.Database.PragmaGetQuickCheck() == false || this.TableExists(tableNameToCheck) == false)
                {
                    return false;
                }
                return true;
            }
        }

        // ReSharper disable once UnusedMember.Global
        public bool IsControlCopyable(string dataLabel)
        {
            long id = this.GetControlIDFromTemplateTable(dataLabel);
            ControlRow control = this.Controls.Find(id);
            return control.Copyable;
        }

        // Return string.Empty only if each control is of a known type,
        // otherwise return the unknown type
        public string AreControlsOfKnownTypes()
        {
            List<string> unknownTypes = new List<string>();
            foreach (ControlRow control in this.Controls)
            {
                string controlType = control.Type;
                switch (controlType)
                {
                    case Constant.DatabaseColumn.File:
                    case Constant.DatabaseColumn.RelativePath:
                    case Constant.DatabaseColumn.DateTime:
                    case Constant.DatabaseColumn.DeleteFlag:
                    case Constant.Control.Counter:
                    case Constant.Control.Flag:
                    case Constant.Control.FixedChoice:
                    case Constant.Control.Note:
                        continue;
                    default:
                        if (false == unknownTypes.Contains(controlType))
                        {
                            unknownTypes.Add(controlType);
                        }
                        break;
                }
            }
            return string.Join(", ", unknownTypes);
        }
        #endregion

        #region Public methods - Update version in info table
        public void UpdateVersionNumber(string versionNumber)
        {
            this.Database.SetColumnToACommonValue(Constant.DBTables.TemplateInfo, Constant.DatabaseColumn.VersionCompatabily, versionNumber);
        }
        #endregion

        #region Public methods - Add user defined control
        public ControlRow AddUserDefinedControl(string controlType)
        {
            this.CreateBackupIfNeeded();

            // create the row for the new control in the data table
            ControlRow newControl = this.Controls.NewRow();
            string dataLabelPrefix;
            switch (controlType)
            {
                case Constant.Control.Counter:
                    dataLabelPrefix = Constant.Control.Counter;
                    newControl.DefaultValue = Constant.ControlDefault.CounterValue;
                    newControl.Type = Constant.Control.Counter;
                    newControl.Width = Constant.ControlDefault.CounterWidth;
                    newControl.Copyable = false;
                    newControl.Visible = true;
                    newControl.Tooltip = Constant.ControlDefault.CounterTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabel(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                case Constant.Control.Note:
                    dataLabelPrefix = Constant.Control.Note;
                    newControl.DefaultValue = Constant.ControlDefault.Value;
                    newControl.Type = Constant.Control.Note;
                    newControl.Width = Constant.ControlDefault.NoteWidth;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.Tooltip = Constant.ControlDefault.NoteTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabel(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                case Constant.Control.FixedChoice:
                    dataLabelPrefix = Constant.Control.Choice;
                    newControl.DefaultValue = Constant.ControlDefault.Value;
                    newControl.Type = Constant.Control.FixedChoice;
                    newControl.Width = Constant.ControlDefault.FixedChoiceWidth;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.Tooltip = Constant.ControlDefault.FixedChoiceTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabel(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                case Constant.Control.Flag:
                    dataLabelPrefix = Constant.Control.Flag;
                    newControl.DefaultValue = Constant.ControlDefault.FlagValue;
                    newControl.Type = Constant.Control.Flag;
                    newControl.Width = Constant.ControlDefault.FlagWidth;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.Tooltip = Constant.ControlDefault.FlagTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabel(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                default:
                    throw new NotSupportedException($"Unhandled control type {controlType}.");
            }
            newControl.ControlOrder = this.GetOrderForNewControl();
            newControl.SpreadsheetOrder = newControl.ControlOrder;

            // add the new control to the database
            List<List<ColumnTuple>> controlInsertWrapper = new List<List<ColumnTuple>>() { newControl.CreateColumnTuplesWithWhereByID().Columns };
            this.Database.Insert(Constant.DBTables.Template, controlInsertWrapper);

            // update the in memory table to reflect current database content
            // could just add the new row to the table but this is done in case a bug results in the insert lacking perfect fidelity
            this.LoadControlsFromTemplateDBSortedByControlOrder();
            return this.Controls[this.Controls.RowCount - 1];
        }
        #endregion

        #region Public Methods - Get Controls, DataLabels, TypedDataLabel
        public List<string> GetDataLabelsExceptIDInSpreadsheetOrder()
        {
            // Utilities.PrintMethodName();
            List<string> dataLabels = new List<string>();
            IEnumerable<ControlRow> controlsInSpreadsheetOrder = this.Controls.OrderBy(control => control.SpreadsheetOrder);
            foreach (ControlRow control in controlsInSpreadsheetOrder)
            {
                string dataLabel = control.DataLabel;
                if (string.IsNullOrEmpty(dataLabel))
                {
                    dataLabel = control.DataLabel;
                }
                Debug.Assert(string.IsNullOrWhiteSpace(dataLabel) == false,
                    $"Encountered empty data label and label at ID {control.ID} in template table.");

                // get a list of datalabels so we can add columns in the order that matches the current template table order
                if (Constant.DatabaseColumn.ID != dataLabel)
                {
                    dataLabels.Add(dataLabel);
                }
            }
            return dataLabels;
        }

        public Dictionary<string, string> GetTypedDataLabelsExceptIDInSpreadsheetOrder()
        {
            // Utilities.PrintMethodName();
            Dictionary<string, string> typedDataLabels = new Dictionary<string, string>();
            IEnumerable<ControlRow> controlsInSpreadsheetOrder = this.Controls.OrderBy(control => control.SpreadsheetOrder);
            foreach (ControlRow control in controlsInSpreadsheetOrder)
            {
                string dataLabel = control.DataLabel;
                if (string.IsNullOrEmpty(dataLabel))
                {
                    dataLabel = control.Label;
                }
                Debug.Assert(string.IsNullOrWhiteSpace(dataLabel) == false,
                    $"Encountered empty data label and label at ID {control.ID} in template table.");

                // get a list of datalabels so we can add columns in the order that matches the current template table order
                if (Constant.DatabaseColumn.ID != dataLabel)
                {
                    typedDataLabels.Add(dataLabel, control.Type);
                }
            }
            return typedDataLabels;
        }
        #endregion

        #region Public Methods - RemoveuserDefinedControl
        public void RemoveUserDefinedControl(ControlRow controlToRemove)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(controlToRemove, nameof(controlToRemove));

            this.CreateBackupIfNeeded();

            // For backwards compatability: MarkForDeletion DataLabel is of the type DeleteFlag,
            // which is a standard control. So we coerce it into thinking its a different type.
            string controlType = controlToRemove.DataLabel == Constant.ControlDeprecated.MarkForDeletion
                ? Constant.ControlDeprecated.MarkForDeletion
                : controlToRemove.Type;
            if (Constant.Control.StandardTypes.Contains(controlType))
            {
                throw new NotSupportedException($"Standard control of type {controlType} cannot be removed.");
            }

            // capture state
            long removedControlOrder = controlToRemove.ControlOrder;
            long removedSpreadsheetOrder = controlToRemove.SpreadsheetOrder;

            // drop the control from the database and data table
            string where = Constant.DatabaseColumn.ID + " = " + controlToRemove.ID;
            this.Database.DeleteRows(Constant.DBTables.Template, where);
            this.LoadControlsFromTemplateDBSortedByControlOrder();

            // regenerate counter and spreadsheet orders; if they're greater than the one removed, decrement
            List<ColumnTuplesWithWhere> controlUpdates = new List<ColumnTuplesWithWhere>();
            foreach (ControlRow control in this.Controls)
            {
                long controlOrder = control.ControlOrder;
                long spreadsheetOrder = control.SpreadsheetOrder;

                if (controlOrder >= removedControlOrder)
                {
                    List<ColumnTuple> controlUpdate = new List<ColumnTuple>
                    {
                        new ColumnTuple(Constant.Control.ControlOrder, controlOrder - 1)
                    };
                    control.ControlOrder = controlOrder - 1;
                    controlUpdates.Add(new ColumnTuplesWithWhere(controlUpdate, control.ID));
                }

                if (spreadsheetOrder >= removedSpreadsheetOrder)
                {
                    List<ColumnTuple> controlUpdate = new List<ColumnTuple>
                    {
                        new ColumnTuple(Constant.Control.SpreadsheetOrder, spreadsheetOrder - 1)
                    };
                    control.SpreadsheetOrder = spreadsheetOrder - 1;
                    controlUpdates.Add(new ColumnTuplesWithWhere(controlUpdate, control.ID));
                }
            }
            this.Database.Update(Constant.DBTables.Template, controlUpdates);

            // update the in memory table to reflect current database content
            // should not be necessary but this is done to mitigate divergence in case a bug results in the delete lacking perfect fidelity
            this.LoadControlsFromTemplateDBSortedByControlOrder();
        }
        #endregion

        #region Public /Private Methods Sync - ControlToDatabase, TemplateTableCOntrolAndSpreadsheetOrderToDatabase
        // The various forms of syncing essentially update the database with one or more controls (usually because a control has changed)
        // and then reloads the control data structure (a datatablebacked list) from the database
        public void SyncControlToDatabase(ControlRow control)
        {
            // This form sync's by the control's ID
            SyncControlToDatabase(control, string.Empty);
        }

        public void SyncControlToDatabase(ControlRow control, string dataLabel)
        {
            // This generic form sync's by ID, or by a non-empty datalabel 
            // Check the arguments for null 
            ThrowIf.IsNullArgument(control, nameof(control));

            // Create the where condition with the ID, but if the dataLabel is not empty, use the dataLabel as the where condition
            ColumnTuplesWithWhere ctw = dataLabel == string.Empty
                ? control.CreateColumnTuplesWithWhereByID()
                : new ColumnTuplesWithWhere(control.CreateColumnTuplesWithWhereByID().Columns, new ColumnTuple(Constant.Control.DataLabel, dataLabel));
            this.Database.Update(Constant.DBTables.Template, ctw);

            // it's possible the passed data row isn't attached to TemplateTable, so refresh the table just in case
            this.LoadControlsFromTemplateDBSortedByControlOrder();
        }

        // Update all ControlOrder and SpreadsheetOrder column entries in the template database to match their in-memory counterparts
        public void SyncTemplateTableControlAndSpreadsheetOrderToDatabase()
        {
            // Utilities.PrintMethodName();
            List<ColumnTuplesWithWhere> columnsTuplesWithWhereList = new List<ColumnTuplesWithWhere>();    // holds columns which have changed for the current control
            foreach (ControlRow control in this.Controls)
            {
                // Update each row's Control and Spreadsheet order values
                List<ColumnTuple> columnTupleList = new List<ColumnTuple>();
                ColumnTuplesWithWhere columnTupleWithWhere = new ColumnTuplesWithWhere(columnTupleList, control.ID);
                columnTupleList.Add(new ColumnTuple(Constant.Control.ControlOrder, control.ControlOrder));
                columnTupleList.Add(new ColumnTuple(Constant.Control.SpreadsheetOrder, control.SpreadsheetOrder));
                columnsTuplesWithWhereList.Add(columnTupleWithWhere);
            }
            this.Database.Update(Constant.DBTables.Template, columnsTuplesWithWhereList);
            // update the in memory table to reflect current database content
            // could just use the new table but this is done in case a bug results in the insert lacking perfect fidelity
            this.LoadControlsFromTemplateDBSortedByControlOrder();
        }

        // Update the entire template database to match the in-memory template
        // Note that this version does this by recreating the entire table: 
        // We could likely be far more efficient by only updateding those entries that differ from the current entries.
        private void RepopulateTemplateTableWithControls(DataTableBackedList<ControlRow> newTable)
        {
            // Utilities.PrintMethodName("Called with arguments");
            // clear the existing table in the database 
            this.Database.DeleteRows(Constant.DBTables.Template, null);

            // Create new rows in the database to match the in-memory verson
            List<List<ColumnTuple>> newTableTuples = new List<List<ColumnTuple>>();
            foreach (ControlRow control in newTable)
            {
                newTableTuples.Add(control.CreateColumnTuplesWithWhereByID().Columns);
            }
            this.Database.Insert(Constant.DBTables.Template, newTableTuples);

            // update the in memory table to reflect current database content
            // could just use the new table but this is done in case a bug results in the insert lacking perfect fidelity
            this.LoadControlsFromTemplateDBSortedByControlOrder();
        }
        #endregion

        #region Public Methods - Misc: BindToEditorDataGrid, CreateBackupIfNeeded, Update DisplayOrder
        public void CreateBackupIfNeeded()
        {
            if (DateTime.Now - this.mostRecentBackup < Constant.File.BackupInterval)
            {
                // not due for a new backup yet
                return;
            }
            FileBackup.TryCreateBackup(this.FilePath);
            this.mostRecentBackup = DateTime.Now;
        }

        public void BindToEditorDataGrid(DataGrid dataGrid, DataRowChangeEventHandler onRowChanged)
        {
            this.editorDataGrid = dataGrid;
            this.onTemplateTableRowChanged = onRowChanged;
            this.LoadControlsFromTemplateDBSortedByControlOrder();
        }

        public void UpdateDisplayOrder(string orderColumnName, Dictionary<string, long> newOrderByDataLabel)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(newOrderByDataLabel, nameof(newOrderByDataLabel));

            // Utilities.PrintMethodName();

            // argument validation. Only ControlOrder and SpreadsheetOrder are orderable columns
            if (orderColumnName != Constant.Control.ControlOrder && orderColumnName != Constant.Control.SpreadsheetOrder)
            {
                throw new ArgumentOutOfRangeException(nameof(orderColumnName),
                    $"column '{orderColumnName}' is not a control order column.  Only '{Constant.Control.ControlOrder}' and '{Constant.Control.SpreadsheetOrder}' are order columns.");
            }

            List<long> uniqueOrderValues = newOrderByDataLabel.Values.Distinct().ToList();
            if (uniqueOrderValues.Count != newOrderByDataLabel.Count)
            {
                throw new ArgumentException(
                    $"newOrderByDataLabel: Each control must have a unique value for its order.  {newOrderByDataLabel.Count - uniqueOrderValues.Count} duplicate values were passed for '{orderColumnName}'.", nameof(newOrderByDataLabel));
            }

            uniqueOrderValues.Sort();
            int uniqueOrderValuesCount = uniqueOrderValues.Count;
            for (int control = 0; control < uniqueOrderValuesCount; ++control)
            {
                int expectedOrder = control + 1;
                if (uniqueOrderValues[control] != expectedOrder)
                {
                    throw new ArgumentOutOfRangeException(nameof(newOrderByDataLabel),
                        $"Control order must be a ones based count.  An order of {uniqueOrderValues[0]} was passed instead of the expected order {expectedOrder} for '{orderColumnName}'.");
                }
            }

            long lastItem = this.Controls.Count();

            // update in memory table with new order
            foreach (ControlRow control in this.Controls)
            {
                // Redundant check for null, as for some reason the CA1062 warning was still showing up
                ThrowIf.IsNullArgument(newOrderByDataLabel, nameof(newOrderByDataLabel));

                string dataLabel = control.DataLabel;
                // Because we don't show all controls, we skip the ones that are missing.
                if (newOrderByDataLabel.ContainsKey(dataLabel) == false)
                {
                    control.SpreadsheetOrder = lastItem--;
                    continue;
                }
                long newOrder = newOrderByDataLabel[dataLabel];
                switch (orderColumnName)
                {
                    case Constant.Control.ControlOrder:
                        control.ControlOrder = newOrder;
                        break;
                    case Constant.Control.SpreadsheetOrder:
                        control.SpreadsheetOrder = newOrder;
                        break;
                }
            }
            // sync new order to database
            this.SyncTemplateTableControlAndSpreadsheetOrderToDatabase();
        }
        #endregion

        #region Public Methods - Get ControlFromTemplate, NextUniqueDataLabel, NextDataLabel
        /// <summary>Given a data label, get the corresponding data entry control</summary>
        public ControlRow GetControlFromTemplateTable(string dataLabel)
        {
            if (dataLabel == null)
            {
                return null;
            }
            foreach (ControlRow control in this.Controls)
            {
                if (dataLabel.Equals(control.DataLabel))
                {
                    return control;
                }
            }
            return null;
        }


        public string GetNextUniqueDataLabel(string dataLabelPrefix)
        {
            // get all existing data labels, as we have to ensure that a new data label doesn't have the same name as an existing one
            List<string> dataLabels = new List<string>();
            List<string> labels = new List<string>();
            foreach (ControlRow control in this.Controls)
            {
                dataLabels.Add(control.DataLabel);
                labels.Add(control.Label);
            }

            // If the data label name and/or the label exists, keep incrementing the count that is appended to the end
            // of the field type until it forms a unique data label name
            int dataLabelUniqueIdentifier = 0;
            string nextDataLabel = dataLabelPrefix + dataLabelUniqueIdentifier;
            while (dataLabels.Contains(nextDataLabel) || labels.Contains(nextDataLabel))
            {
                ++dataLabelUniqueIdentifier;
                nextDataLabel = dataLabelPrefix + dataLabelUniqueIdentifier;
            }

            return nextDataLabel;
        }

        public string GetNextUniqueLabel(string labelPrefix)
        {
            // get all existing labels, as we have to ensure that a new label doesn't have the same name as an existing one
            List<string> labels = new List<string>();
            foreach (ControlRow control in this.Controls)
            {
                labels.Add(control.Label);
            }

            // If the  label name exists, keep incrementing the count that is appended to the end
            // of the field type until it forms a unique data label name
            int labelUniqueIdentifier = 0;
            string nextLabel = labelPrefix + labelUniqueIdentifier;
            while (labels.Contains(nextLabel))
            {
                ++labelUniqueIdentifier;
                nextLabel = labelPrefix + labelUniqueIdentifier;
            }

            return nextLabel;
        }
        #endregion

        #region Private helpers: Create and populate various template tables

        // Create an empty template table in the database based on the template schema
        private void CreateEmptyTemplateTable(SQLiteWrapper database)
        {
            List<SchemaColumnDefinition> templateTableColumns = new List<SchemaColumnDefinition>
            {
                new SchemaColumnDefinition(Constant.DatabaseColumn.ID, Sql.CreationStringPrimaryKey),
                new SchemaColumnDefinition(Constant.Control.ControlOrder, Sql.IntegerType),
                new SchemaColumnDefinition(Constant.Control.SpreadsheetOrder, Sql.IntegerType),
                new SchemaColumnDefinition(Constant.Control.Type, Sql.Text),
                new SchemaColumnDefinition(Constant.Control.DefaultValue, Sql.Text),
                new SchemaColumnDefinition(Constant.Control.Label, Sql.Text),
                new SchemaColumnDefinition(Constant.Control.DataLabel, Sql.Text),
                new SchemaColumnDefinition(Constant.Control.Tooltip, Sql.Text),
                new SchemaColumnDefinition(Constant.Control.TextBoxWidth, Sql.Text),
                new SchemaColumnDefinition(Constant.Control.Copyable, Sql.Text),
                new SchemaColumnDefinition(Constant.Control.Visible, Sql.Text),
                new SchemaColumnDefinition(Constant.Control.List, Sql.Text)
                };
            database.CreateTable(Constant.DBTables.Template, templateTableColumns);
        }

        // Create and populate a TemplateInfo table in the database using the schema below
        private void CreateAndPopulateTemplateInfoTable(SQLiteWrapper database)
        {
            // Add a TemplateInfo table only to the .tdb file
            List<SchemaColumnDefinition> templateInfoColumns = new List<SchemaColumnDefinition>
            {
                new SchemaColumnDefinition(Constant.DatabaseColumn.VersionCompatabily, Sql.Text, Constant.DatabaseValues.VersionNumberMinimum)
            };
            this.Database.CreateTable(Constant.DBTables.TemplateInfo, templateInfoColumns);

            // Add the version number of the current Timelapse program to the templateinfo table 
            List<List<ColumnTuple>> templateContents = new List<List<ColumnTuple>>();
            List<ColumnTuple> version = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.DatabaseColumn.VersionCompatabily, VersionChecks.GetTimelapseCurrentVersionNumber().ToString())
            };
            templateContents.Add(version);
            database.Insert(Constant.DBTables.TemplateInfo, templateContents);
        }

        private void PopulateTemplateTableWithStandardControls(SQLiteWrapper database)
        {
            // Add standard controls to template table
            List<List<ColumnTuple>> standardControls = new List<List<ColumnTuple>>();
            long controlOrder = 0; // The control order, a one based count incremented for every new entry
            long spreadsheetOrder = 0; // The spreadsheet order, a one based count incremented for every new entry

            // file
            standardControls.Add(CreateFileTuples(controlOrder, spreadsheetOrder, true));

            // relative path
            standardControls.Add(CreateRelativePathTuples(++controlOrder, ++spreadsheetOrder, true));

            // datetime
            standardControls.Add(CreateDateTimeTuples(++controlOrder, ++spreadsheetOrder, true));

            // delete flag
            standardControls.Add(CreateDeleteFlagTuples(++controlOrder, ++spreadsheetOrder, true));

            // insert standard controls into the template table
            database.Insert(Constant.DBTables.Template, standardControls);
        }
        #endregion

        #region Private Methods - Get OrderForNewControl, ControlID, Controls in sorted order
        private long GetOrderForNewControl()
        {
            return this.Controls.RowCount + 1;
        }

        /// <summary>Given a data label, get the id of the corresponding data entry control</summary>
        protected long GetControlIDFromTemplateTable(string dataLabel)
        {
            ControlRow control = this.GetControlFromTemplateTable(dataLabel);
            if (control == null)
            {
                return -1;
            }
            return control.ID;
        }

        // Re-populate the controls (a DataTableBackedList) from the database
        // Bind the controls to the data grid so they appear as needed
        private void LoadControlsFromTemplateDBSortedByControlOrder()
        {
            // Utilities.PrintMethodName();
            DataTable templateTable = this.Database.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.Template + Sql.OrderBy + Constant.Control.ControlOrder);
            this.Controls = new DataTableBackedList<ControlRow>(templateTable, row => new ControlRow(row));
            this.Controls.BindDataGrid(this.editorDataGrid, this.onTemplateTableRowChanged);
        }
        #endregion

        #region Private static Methods: Create tuples defining the standard controls  (File, RelativePath, DateTime, DeleteFlag)
        private static List<ColumnTuple> CreateFileTuples(long controlOrder, long spreadsheetOrder, bool visible)
        {
            List<ColumnTuple> file = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.Control.ControlOrder, controlOrder),
                new ColumnTuple(Constant.Control.SpreadsheetOrder, spreadsheetOrder),
                new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.File),
                new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.Value),
                new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.File),
                new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.File),
                new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.FileTooltip),
                new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.FileWidth),
                new ColumnTuple(Constant.Control.Copyable, false),
                new ColumnTuple(Constant.Control.Visible, true),
                new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value)
            };
            return file;
        }

        // Defines a RelativePath control. The definition is used by its caller to insert a RelativePath control into the template for backwards compatability. 
        private static List<ColumnTuple> CreateRelativePathTuples(long controlOrder, long spreadsheetOrder, bool visible)
        {
            List<ColumnTuple> relativePath = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.Control.ControlOrder, controlOrder),
                new ColumnTuple(Constant.Control.SpreadsheetOrder, spreadsheetOrder),
                new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.RelativePath),
                new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.Value),
                new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.RelativePath),
                new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.RelativePath),
                new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.RelativePathTooltip),
                new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.RelativePathWidth),
                new ColumnTuple(Constant.Control.Copyable, false),
                new ColumnTuple(Constant.Control.Visible, visible),
                new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value)
            };
            return relativePath;
        }

        private static List<ColumnTuple> CreateDateTimeTuples(long controlOrder, long spreadsheetOrder, bool visible)
        {
            List<ColumnTuple> dateTime = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.Control.ControlOrder, controlOrder),
                new ColumnTuple(Constant.Control.SpreadsheetOrder, spreadsheetOrder),
                new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.DateTime),
                new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.DateTimeDefaultValue),
                new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.DateTime),
                new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.DateTime),
                new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.DateTimeTooltip),
                new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.DateTimeWidth),
                new ColumnTuple(Constant.Control.Copyable, false),
                new ColumnTuple(Constant.Control.Visible, visible),
                new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value)
            };
            return dateTime;
        }


        // Defines a DeleteFlag control. The definition is used by its caller to insert a DeleteFlag control into the template for backwards compatability. 
        private static List<ColumnTuple> CreateDeleteFlagTuples(long controlOrder, long spreadsheetOrder, bool visible)
        {
            List<ColumnTuple> deleteFlag = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.Control.ControlOrder, controlOrder),
                new ColumnTuple(Constant.Control.SpreadsheetOrder, spreadsheetOrder),
                new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.DeleteFlag),
                new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.FlagValue),
                new ColumnTuple(Constant.Control.Label, Constant.ControlDefault.DeleteFlagLabel),
                new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.DeleteFlag),
                new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.DeleteFlagTooltip),
                new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.FlagWidth),
                new ColumnTuple(Constant.Control.Copyable, false),
                new ColumnTuple(Constant.Control.Visible, visible),
                new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value)
            };
            return deleteFlag;
        }
        #endregion

        #region Disposing
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.Controls?.Dispose();
            }

            this.disposed = true;
        }
        #endregion
    }
}
