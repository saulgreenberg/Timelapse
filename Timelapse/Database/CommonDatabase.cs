using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using Timelapse.Constant;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Util;
using ColumnTuple = Timelapse.DataStructures.ColumnTuple;
using ColumnTuplesWithWhere = Timelapse.DataStructures.ColumnTuplesWithWhere;
using File = System.IO.File;

// ReSharper disable LocalizableElement

namespace Timelapse.Database
{
    #region Description
    // Timelapse Common Database.
    // A high-level discussion of the logic is below.
    // Both the .tdb and .ddb file have a few common functions, and both create or contain common tables.
    // Thus the CommonDatabase defines and manages what is common to both databases. 
    // This is why FileDatabase inherits from CommonDatabase
    // Currently, the items special to Template databases are included, but that may change

    // Templates and MetadataTemplates tables exist in both the .tdb and .ddb file.
    // Templates: define the data field controls for images
    // MetadataTemplates: defines the data fields controls for a folder metadata
    // The .tdb contains (more or less) the 'master' version of these tables, while
    // the corresponding versions in the .ddb is included both for retrieval convenience and to check for differences between the two
    // in case the template tdb has been updated and differs from what is stored in the .ddb database
    // (detected differences eventually result in a dialog where the user would review some of the changes).

    // Timelapse maintains a 'Control' and 'MetadataControl' data structure (a DataTableBackedList) mirroring their corresponing table contents,
    // These will be bound to  datagrids as needed so that a user can see its contents.
    // When either a control or metadatacontrol is modified, Timelapse updates both the corresponding data table
    // and the control/metadataControl data structure to reflect those changes.

    // When a .tdb is initially created, 
    // - an empty template and metadataTemplate table is created
    // - the standard control rows are added

    // When a new ddb database is created,
    // - an empty template table is first created, and then loaded with the contents of the template table from the .tdb file.
    // - an empty metadataTemplate table is also created
    // When an existing ddb database is opened,
    // - its template and metadata template is checked against the corresponding tables  found in the tdb database
    #endregion

    public class CommonDatabase : IDisposable
    {
        #region Public / Protected Properties

        // Controls reflect the contents of the template table
        public DataTableBackedList<ControlRow> Controls { get; private set; }

        // MetadataControls reflect the contents of the MetadataTemplate table
        public DataTableBackedList<MetadataControlRow> MetadataControlsAll { get; private set; }
        public Dictionary<int, DataTableBackedList<MetadataControlRow>> MetadataControlsByLevel { get; private set; }
        public DataTableBackedList<MetadataInfoRow> MetadataInfo { get; private set; }

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
        // A bit confusing unless you know that the CommonDatabase is used to open and get a reference to both a .tdb or a .ddb database file. 
        // Both files contains tables that require us to get and store the common Controls (defined in the Template table)
        // and MetadataControls (defined in the MetadataTemplate table), each as datatableBackedList data structures.
        // 1. When a template .tdb is opened (either from the Editor or Timelapse)
        // 2. When a .ddb file is opened
        //    a) initially as a check to see if .ddb template and metadataTemplate needs updating i.e., to compare templates. The database is then closed
        //    b) later to actually open the .ddb (which as a side effect loads all the required template info)
        public CommonDatabase(string filePath)
        {
            this.disposed = false;
            this.mostRecentBackup = FileBackup.GetMostRecentBackup(filePath);

            // open or create database
            this.Database = new SQLiteWrapper(filePath);
            this.FilePath = filePath;
        }

        #endregion

        #region Async Tasks - TryCreateOrOpen, Private DoCreateOpen, OnDatabaseCreated
        // Try to create or open a database from the provided file path
        // This is only invoked when either loading images, or when attempting a merge
        public static async Task<Tuple<bool, CommonDatabase>> TryCreateOrOpenAsync(string filePath)
        {
            // Follow the MSDN design pattern for returning an IDisposable: https://www.codeproject.com/Questions/385273/Returning-a-Disposable-Object-from-a-Method
            CommonDatabase disposableCommonDatabase = null;
            try
            {
                disposableCommonDatabase = await DoCreateOrOpenAsync(filePath).ConfigureAwait(true);
                CommonDatabase returnableCommonDatabase = disposableCommonDatabase;

                // the returnableCommonDatabase will be null if it does not appear to be valid
                bool successOrFail = returnableCommonDatabase != null;
                return new Tuple<bool, CommonDatabase>(successOrFail, returnableCommonDatabase);
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage($"Failure in TryCreateOpen. {exception}");
                return new Tuple<bool, CommonDatabase>(false, null);
            }
            finally
            {
                disposableCommonDatabase?.Dispose();
            }
        }

        // Create or open a template database from the provided tdb file path
        // This is only invoked for .tdb files by the above method. It:
        // - creates a new database if the file doesn't exist,
        // - opens an existing database if the file exists and is valid.
        // It is invoked by only the prior method, or from the Editor
        private static async Task<CommonDatabase> DoCreateOrOpenAsync(string filePath)
        {
            // check if its a new or existing database file before instantiating the database as SQL wrapper
            bool newDatabase = !File.Exists(filePath);

            CommonDatabase commonDatabase = new CommonDatabase(filePath);
            if (newDatabase)
            {
                // Its a new database file. Initialize it
                await commonDatabase.OnDatabaseCreatedAsync(null).ConfigureAwait(true);
            }
            else
            {
                // Its an existing database file.  
                // Check if its valid.
                //   We do this by checking both the database integrity and if it has a TemplateTable.
                //   While a minimal check, it suffices in most cases.
                //   Note that the check may be redundant as one of the calling methods may have already done it, but since its fast to do we don't bother factoring it out. 
                if (commonDatabase.Database.PragmaGetQuickCheck() == false || commonDatabase.DoesTableExist(Constant.DBTables.Template) == false)
                {
                    commonDatabase.Dispose();
                    return null;
                }
                // Backwards compatability: If the ExportToCSV column isn't in the template, it means we are opening up 
                // an old version of the template. Update the table by adding a new ExportToCSV column filled with the appropriate default
                // Note that we don't have to do this for the metadataTemplate as it follows the new versions' spec
                CommonDatabase.AddExportToCSVColumnIfNeeded(commonDatabase.Database);

                // Load (or reload) the Controls data structure from the template table in the database
                await commonDatabase.LoadControlsFromTemplateDBSortedByControlOrderAsync();

                // Create the Metadata tables in the file if they don't exists (i.e., upgrade those files)
                if (false == commonDatabase.Database.TableExists(Constant.DBTables.MetadataInfo))
                {
                    CommonDatabase.CreateEmptyMetadataInfoTable(commonDatabase.Database);
                }
                if (false == commonDatabase.Database.TableExists(Constant.DBTables.MetadataTemplate))
                {
                    CommonDatabase.CreateEmptyMetadataTemplateTable(commonDatabase.Database);
                }
                await commonDatabase.LoadMetadataControlsAndInfoFromTemplateTDBSortedByControlOrderAsync();
            }
            return commonDatabase;
        }

        // Called when a new database file (which could be a tdb or ddb) is created. 
        // Essentially it creates and populates the template table 
        protected virtual async Task OnDatabaseCreatedAsync(CommonDatabase existingTemplateDatabase)
        {
            // create the template and metadataTemplate table
            await Task.Run(() =>
            {
                // WE ARE IN A DDB FILE as a template table was passed.
                // Passing an existing template table normally occurs when a ddb file is created,
                // where we want to clone the exiting template table from the tdb file.
                if (existingTemplateDatabase != null)
                {
                    // Create empty template and metadata table with the appropriate schema
                    CommonDatabase.CreateEmptyTemplateTable(this.Database);
                    CommonDatabase.CreateEmptyMetadataTemplateTable(this.Database);
                    CommonDatabase.CreateEmptyMetadataInfoTable(this.Database);

                    // Populate the template table from their existing template structures
                    this.SyncControlsToEmptyDatabase(existingTemplateDatabase.Controls);

                    // Populate the Metadata Controls and Info tables from the existing structures
                    // If those structures don't exist, nothinig will be done.
                    this.SyncMetadataControlsToEmptyDatabase(existingTemplateDatabase.MetadataControlsAll);
                    this.SyncMetadataInfoToEmptyDatabase(existingTemplateDatabase.MetadataInfo);
                    this.LoadMetadataControlsAndInfoFromTemplateDBSortedByControlOrder();
                    return;
                }

                // WE ARE IN A TDB FILE as no existing template table was passed.
                // This means we need to create the template table in a tdb database
                // and populate it with the standard contorols.

                // 1. Create a template and metadata table 
                CommonDatabase.CreateEmptyTemplateTable(this.Database);
                CommonDatabase.CreateEmptyMetadataTemplateTable(this.Database);
                CommonDatabase.CreateEmptyMetadataInfoTable(this.Database);

                // 2. Populate the template with the standard controls
                //    (there are no standard controls for metadata)
                CommonDatabase.PopulateTemplateTableWithStandardControls(this.Database);

                // 3. Populate the in-memory version of the template and metadataTemplate table
                this.LoadControlsFromTemplateDBSortedByControlOrder();
                this.LoadMetadataControlsAndInfoFromTemplateDBSortedByControlOrder();

                // 4. Add and populate the TemplateInfo table
                CommonDatabase.CreateAndPopulateTemplateInfoTable(this.Database);
            }).ConfigureAwait(true);
        }
        #endregion

        #region Public Methods - Boolean tests - DoesTableExist, IsControlCopyable, AreControlsOfKnownTypes
        public bool DoesTableExist(string dataTable)
        {
            return this.Database.TableExists(dataTable);
        }

        public bool IsControlCopyable(string dataLabel)
        {
            long id = this.GetControlIDFromControls(dataLabel);
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
                    case Constant.Control.Note:
                    case Constant.Control.MultiLine:
                    case Constant.Control.AlphaNumeric:
                    case Constant.Control.Counter:
                    case Constant.Control.IntegerAny:
                    case Constant.Control.IntegerPositive:
                    case Constant.Control.DecimalAny:
                    case Constant.Control.DecimalPositive:
                    case Constant.Control.FixedChoice:
                    case Constant.Control.MultiChoice:
                    case Constant.Control.Flag:
                    case Constant.Control.DateTime_:
                    case Constant.Control.Date_:
                    case Constant.Control.Time_:
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

        #region Controls - LoadControlsFromTemplateDBSortedByControlOrder
        // Re-populate the controls (a DataTableBackedList) from the database
        // Bind the controls to the data grid so they appear as needed
        // ASYNC and NON-ASYNC versions
        public virtual async Task LoadControlsFromTemplateDBSortedByControlOrderAsync()
        {
            await Task.Run(this.LoadControlsFromTemplateDBSortedByControlOrder).ConfigureAwait(true);
        }

        public void LoadControlsFromTemplateDBSortedByControlOrder()
        {
            // Utilities.PrintMethodName();
            DataTable templateTable = this.Database.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.Template + Sql.OrderBy + Constant.Control.ControlOrder);
            this.Controls = new DataTableBackedList<ControlRow>(templateTable, row => new ControlRow(row));
            this.Controls.BindDataGrid(this.editorDataGrid, this.onTemplateTableRowChanged);
        }
        #endregion

        #region Controls - Add a user defined control.
        public ControlRow AddControlToDataTableAndDatabase(string controlType)
        {
            this.CreateBackupIfNeeded();

            // create the row for the new control in the data table
            ControlRow newControl = this.Controls.NewRow();
            string dataLabelPrefix;
            switch (controlType)
            {
                case Constant.Control.Counter:
                    dataLabelPrefix = Constant.Control.Counter;
                    newControl.DefaultValue = Constant.ControlDefault.NumberDefaultValue;
                    newControl.Type = Constant.Control.Counter;
                    newControl.Width = Constant.ControlDefault.CounterWidth;
                    newControl.Copyable = false;
                    newControl.Visible = true;
                    newControl.ExportToCSV = true;
                    newControl.Tooltip = Constant.ControlDefault.CounterTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabelInControls(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                case Constant.Control.IntegerAny:
                case Constant.Control.IntegerPositive:
                    if (controlType == Constant.Control.IntegerAny)
                    {
                        dataLabelPrefix = Constant.Control.LabelIntegerAny;
                        newControl.Type =  Constant.Control.IntegerAny;
                        newControl.Tooltip = Constant.ControlDefault.IntegerAnyTooltip;
                    }
                    else
                    {
                        dataLabelPrefix = Constant.Control.IntegerPositive;
                        newControl.Type = Constant.Control.IntegerPositive;
                        newControl.Tooltip = Constant.ControlDefault.IntegerPositiveTooltip;
                    }
                    newControl.DefaultValue = Constant.ControlDefault.NumberDefaultValue;
                    newControl.Width = Constant.ControlDefault.NumberWidth;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.ExportToCSV = true;
                    
                    newControl.DataLabel = this.GetNextUniqueDataLabelInControls(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;

                case Constant.Control.DecimalAny:
                case Constant.Control.DecimalPositive:
                    if (controlType == Constant.Control.DecimalAny)
                    {
                        dataLabelPrefix = Constant.Control.LabelDecimalAny;
                        newControl.Type = Constant.Control.DecimalAny;
                        newControl.Tooltip = Constant.ControlDefault.DecimalAnyTooltip;
                    }
                    else
                    {
                        dataLabelPrefix = Constant.Control.DecimalPositive;
                        newControl.Type = Constant.Control.DecimalPositive;
                        newControl.Tooltip = Constant.ControlDefault.DecimalPositiveTooltip;
                    }
                    newControl.DefaultValue = Constant.ControlDefault.NumberDefaultValue;
                    newControl.Width = Constant.ControlDefault.NumberWidth;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.ExportToCSV = true;

                    newControl.DataLabel = this.GetNextUniqueDataLabelInControls(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;

                case Constant.Control.Note:
                    dataLabelPrefix = Constant.Control.Note;
                    newControl.DefaultValue = Constant.ControlDefault.NoteDefaultValue;
                    newControl.Type = Constant.Control.Note;
                    newControl.Width = Constant.ControlDefault.NoteDefaultWidth;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.ExportToCSV = true;
                    newControl.Tooltip = Constant.ControlDefault.NoteTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabelInControls(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;

                case Constant.Control.MultiLine:
                    dataLabelPrefix = Constant.Control.MultiLine;
                    newControl.DefaultValue = Constant.ControlDefault.MultiLineDefaultValue;
                    newControl.Type = Constant.Control.MultiLine;
                    newControl.Width = Constant.ControlDefault.MultiLineWidth;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.ExportToCSV = true;
                    newControl.Tooltip = Constant.ControlDefault.MultiLineTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabelInControls(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;

                case Constant.Control.AlphaNumeric:
                    dataLabelPrefix = Constant.Control.AlphaNumeric;
                    newControl.DefaultValue = Constant.ControlDefault.AlphaNumericDefaultValue;
                    newControl.Type = Constant.Control.AlphaNumeric;
                    newControl.Width = Constant.ControlDefault.NoteDefaultWidth;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.ExportToCSV = true;
                    newControl.Tooltip = Constant.ControlDefault.AlphaNumericTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabelInControls(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;

                case Constant.Control.FixedChoice:
                    dataLabelPrefix = Constant.Control.Choice;
                    newControl.DefaultValue = Constant.ControlDefault.FixedChoiceDefaultValue;
                    newControl.Type = Constant.Control.FixedChoice;
                    newControl.Width = Constant.ControlDefault.FixedChoiceDefaultWidth;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.ExportToCSV = true;
                    newControl.Tooltip = Constant.ControlDefault.FixedChoiceTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabelInControls(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                case Constant.Control.MultiChoice:
                    dataLabelPrefix = Constant.Control.MultiChoice;
                    newControl.DefaultValue = Constant.ControlDefault.MultiChoiceDefaultValue;
                    newControl.Type = Constant.Control.MultiChoice;
                    newControl.Width = Constant.ControlDefault.FixedChoiceDefaultWidth;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.ExportToCSV = true;
                    newControl.Tooltip = Constant.ControlDefault.MultiChoiceTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabelInControls(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                case Constant.Control.Flag:
                    dataLabelPrefix = Constant.Control.Flag;
                    newControl.DefaultValue = Constant.ControlDefault.FlagValue;
                    newControl.Type = Constant.Control.Flag;
                    newControl.Width = Constant.ControlDefault.FlagWidth;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.ExportToCSV = true;
                    newControl.Tooltip = Constant.ControlDefault.FlagTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabelInControls(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                case Constant.Control.DateTime_:
                    dataLabelPrefix = Constant.Control.DateTime_;
                    newControl.DefaultValue = DateTimeHandler.ToStringDatabaseDateTime(Constant.ControlDefault.DateTimeCustomDefaultValue);
                    newControl.Type = Constant.Control.DateTime_;
                    newControl.Width = Constant.ControlDefault.DateTimeCustomDefaultWidth;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.ExportToCSV = true;
                    newControl.Tooltip = Constant.ControlDefault.DateTimeCustomTooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabelInControls(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                case Constant.Control.Date_:
                    dataLabelPrefix = Constant.Control.Date_;
                    newControl.DefaultValue = DateTimeHandler.ToStringDatabaseDate(Constant.ControlDefault.Date_DefaultValue);
                    newControl.Type = Constant.Control.Date_;
                    newControl.Width = Constant.ControlDefault.Date_DefaultWidth;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.ExportToCSV = true;
                    newControl.Tooltip = Constant.ControlDefault.Date_Tooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabelInControls(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                case Constant.Control.Time_:
                    dataLabelPrefix = Constant.Control.Time_;
                    newControl.DefaultValue = DateTimeHandler.ToStringTime(Constant.ControlDefault.Time_DefaultValue);
                    newControl.Type = Constant.Control.Time_;
                    newControl.Width = Constant.ControlDefault.Time_Width;
                    newControl.Copyable = true;
                    newControl.Visible = true;
                    newControl.ExportToCSV = true;
                    newControl.Tooltip = Constant.ControlDefault.Time_Tooltip;
                    newControl.DataLabel = this.GetNextUniqueDataLabelInControls(dataLabelPrefix);
                    newControl.Label = newControl.DataLabel;
                    break;
                default:
                    throw new NotSupportedException($"Unhandled control type {controlType}.");
            }

            // Set the order to the last one
            newControl.ControlOrder = this.Controls.RowCount + 1;
            newControl.SpreadsheetOrder = newControl.ControlOrder;

            // add the new control to the database
            List<List<ColumnTuple>> controlInsertWrapper = new List<List<ColumnTuple>>() { newControl.CreateColumnTuplesWithWhereByID().Columns };
            this.Database.Insert(Constant.DBTables.Template, controlInsertWrapper);

            // update the in memory table to reflect current database content
            // could just add the new row to the table but this is simpler
            this.LoadControlsFromTemplateDBSortedByControlOrder();
            return this.Controls[this.Controls.RowCount - 1];
        }
        #endregion

        #region Controls - Remove a user defined Control
        public void RemoveControlFromDataTableAndDatabase(ControlRow controlToRemove)
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

        #region Controls - GetControlFromControls, GetControlIDFromControls
        // Get the  data entry control matching the data label
        // Since data labels are unique, there could ever be only 0 or 1 match
        public ControlRow GetControlFromControls(string dataLabel)
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

        // Given a data label, get the id of the corresponding data entry control
        protected long GetControlIDFromControls(string dataLabel)
        {
            ControlRow control = this.GetControlFromControls(dataLabel);
            if (control == null)
            {
                return -1;
            }
            return control.ID;
        }
        #endregion

        #region Controls - Get NextUniqueDataLabel, NextDataLabel
        // Given a data label prefix, return it where it is appended with an integer (starting at 0) that makes it unique from others in the Controls data structure
        // e.g., 1st Counter becomes Counter0, 2nd Counter is Counter1 etc.
        public string GetNextUniqueDataLabelInControls(string dataLabelPrefix)
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

        // Given a label prefix, return it where it is appended with an integer (starting at 0) that makes it unique from others in the Controls data structure
        // e.g., 1st Counter becomes Counter0, 2nd Counter is Counter1 etc.
        public string GetNextUniqueLabelInControls(string labelPrefix)
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

        #region Controls - Get DataLabels, TypedDataLabel except id controls
        public List<string> GetDataLabelsFromControlsByIDCreationOrder()
        {
            List<string> dataLabels = new List<string>();
            IEnumerable<ControlRow> controls = this.Controls.OrderBy(control => control.ID);
            foreach (ControlRow control in controls)
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

        public List<string> GetDataLabelsExceptIDInSpreadsheetOrderFromControls()
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

        public List<string> GetDataLabelsToExcludeFromExport()
        {
            List<string> dataLabels = new List<string>();
            IEnumerable<ControlRow> controlsInSpreadsheetOrder = this.Controls.OrderBy(control => control.SpreadsheetOrder);
            foreach (ControlRow control in controlsInSpreadsheetOrder)
            {
                if (control.ExportToCSV)
                {
                    continue;
                }
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

        public Dictionary<string, string> GetTypedDataLabelsExceptIDInSpreadsheetOrderFromControls()
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

        #region Controls - Sync To Database
        // The various forms of syncing essentially update the database with one or more controls (usually because a control has changed)
        // and then reloads the control data structure (a datatablebacked list) from the database
        // Controls are sorted by control order
        public void SyncControlToDatabase(ControlRow control)
        {
            // This form sync's by the control's ID
            SyncControlToDatabase(control, string.Empty);
        }

        private void SyncControlToDatabase(ControlRow control, string dataLabel)
        {
            // This generic form sync's by ID, or by a non-empty datalabel 
            // Check the arguments for null 
            ThrowIf.IsNullArgument(control, nameof(control));

            // Create the where condition with the ID, but if the dataLabel is not empty, use the dataLabel as the where condition
            ColumnTuplesWithWhere ctw = dataLabel == string.Empty
                ? control.CreateColumnTuplesWithWhereByID()
                : new ColumnTuplesWithWhere(control.CreateColumnTuplesWithWhereByID().Columns, new ColumnTuple(Constant.Control.DataLabel, dataLabel));
            this.Database.Update(Constant.DBTables.Template, ctw);
            this.LoadControlsFromTemplateDBSortedByControlOrder();
        }

        // Update all ControlOrder and SpreadsheetOrder column entries in the template database to match their in-memory counterparts.
        // Note that this only updates those entries. If other control entries exist in the database table, they will be unaffected.  
        private void SyncControlsToDatabase()
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

            // Update the in memory table to reflect current database content
            // Perhaps not needed as the database was generated from the table, but guarantees resorts if control order has changed
            this.LoadControlsFromTemplateDBSortedByControlOrder();
        }

        // Populate the (empty) template database with its in-memory version
        // Note that this version does this by recreating the entire table: 
        // We could likely be more efficient by only updating those entries that differ from the current entries, but its not worth the bother.
        private void SyncControlsToEmptyDatabase(DataTableBackedList<ControlRow> templateControlRows)
        {
            // Create a list matching the in-memory verson and insert it into the database
            List<List<ColumnTuple>> newTableTuples = new List<List<ColumnTuple>>();
            foreach (ControlRow control in templateControlRows)
            {
                newTableTuples.Add(control.CreateColumnTuplesWithWhereByID().Columns);
            }
            this.Database.Insert(Constant.DBTables.Template, newTableTuples);

            // Update the in memory table to reflect current database content
            // Perhaps not needed as the database was generated from the table, but guarantees resorts if control order has changed
            this.LoadControlsFromTemplateDBSortedByControlOrder();
        }

        // Populate the (empty) template database with its in-memory version
        // Note that this version does this by recreating the entire table: 
        // We could likely be more efficient by only updating those entries that differ from the current entries, but its not worth the bother.
        public void SyncMetadataControlsToEmptyDatabase(DataTableBackedList<MetadataControlRow> metadataControlRows)
        {
            if (metadataControlRows == null)
            {
                return;
            }
            // Create a list matching the in-memory verson and insert it into the database
            List<List<ColumnTuple>> newTableTuples = new List<List<ColumnTuple>>();
            foreach (MetadataControlRow control in metadataControlRows)
            {
                newTableTuples.Add(control.CreateColumnTuplesWithWhereByID().Columns);
            }
            this.Database.Insert(Constant.DBTables.MetadataTemplate, newTableTuples);

            // Update the in memory metadataControlsAll and MetadataControlsByLevel structures to reflect the just syncronized database content
            this.LoadMetadataControlsAndInfoFromTemplateDBSortedByControlOrder();
        }

        // Populate the (empty) template database with its in-memory version
        // Note that this version does this by recreating the entire table: 
        // We could likely be more efficient by only updating those entries that differ from the current entries, but its not worth the bother.
        public void SyncMetadataInfoToEmptyDatabase(DataTableBackedList<MetadataInfoRow> metadataInfoRows)
        {
            if (metadataInfoRows == null)
            {
                return;
            }
            // Create a list matching the in-memory verson and insert it into the database
            List<List<ColumnTuple>> newTableTuples = new List<List<ColumnTuple>>();
            foreach (MetadataInfoRow control in metadataInfoRows)
            {
                newTableTuples.Add(control.CreateColumnTuplesWithWhereByID().Columns);
            }
            this.Database.Insert(Constant.DBTables.MetadataInfo, newTableTuples);

            // Update the in memory MetadataControlsByLevel structures to reflect the just syncronized database content
            this.TryLoadMetadataInfoFromTemplateDB();
        }
        #endregion

        #region Controls (Editor only) - UpdateControlDisplayOrder, BindToEditorDataGrid
        // Update controls with the new order 
        // Order can be either the control order or the spreadsheet order. The Dictionary holds the new order 
        public void UpdateControlDisplayOrder(string orderColumnName, Dictionary<string, long> newOrderByDataLabel)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(newOrderByDataLabel, nameof(newOrderByDataLabel));

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
                if (newOrderByDataLabel.TryGetValue(dataLabel, out var newOrder) == false)
                {
                    control.SpreadsheetOrder = lastItem--;
                    continue;
                }

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
            // sync the newly ordered controls to the database,, which also reloads the controls into the controls data structure
            this.SyncControlsToDatabase();
        }

        public void BindToEditorDataGrid(DataGrid dataGrid, DataRowChangeEventHandler onRowChanged)
        {
            this.editorDataGrid = dataGrid;
            this.onTemplateTableRowChanged = onRowChanged;
            this.LoadControlsFromTemplateDBSortedByControlOrder();
        }

        #endregion

        #region Metadata Controls - Create empty metadata tables and/or data structure
        // Create, as needed, a MetadataTemplateTable and MetadataInfo table in the database and a metadata data structure.
        // Create both as needed
        public void CreateEmptyMetadataTablesIfNeeded()
        {
            //  MetadataInfo table
            if (false == this.Database.TableExists(Constant.DBTables.MetadataInfo))
            {
                CommonDatabase.CreateEmptyMetadataInfoTable(this.Database);
            }
            
            // MetadataTemplateTable
            if (false == this.Database.TableExists(Constant.DBTables.MetadataTemplate))
            {
                CommonDatabase.CreateEmptyMetadataTemplateTable(this.Database);
            }

            // Metadata data structure
            if (null == this.MetadataControlsAll)
            {
                this.LoadMetadataControlsAndInfoFromTemplateDBSortedByControlOrder();
            }

        }

        // Create an empty Metadata template table in the database based on the Metadata template schema
        public static void CreateEmptyMetadataTemplateTable(SQLiteWrapper database)
        {
            List<SchemaColumnDefinition> templateTableColumns = GetCommonSchema();
            templateTableColumns.Insert(3, new SchemaColumnDefinition(Constant.Control.Level, Sql.IntegerType));
            database.CreateTable(Constant.DBTables.MetadataTemplate, templateTableColumns);
        }


        // Create an empty MetadataInfo table in the database
        public static void CreateEmptyMetadataInfoTable(SQLiteWrapper database)
        {
            List<SchemaColumnDefinition> metadataAliasTableColumns = new List<SchemaColumnDefinition>
            {
                new SchemaColumnDefinition(Constant.DatabaseColumn.ID, Sql.CreationStringPrimaryKey),
                new SchemaColumnDefinition(Constant.Control.Level, Sql.IntegerType),
                new SchemaColumnDefinition(Constant.Control.Guid, Sql.Text),
                new SchemaColumnDefinition(Constant.Control.Alias, Sql.Text)
            };
            database.CreateTable(Constant.DBTables.MetadataInfo, metadataAliasTableColumns);
        }

        #endregion

        #region MetadataControls - LoadMetadataControlsAndInfoFromTemplateDBSortedByControlOrder
        // Re-populate the controls (a DataTableBackedList) from the database
        // Bind the controls to the data grid so they appear as needed
        // ASYNC and NON-ASYNC versions
        public virtual async Task<bool> LoadMetadataControlsAndInfoFromTemplateTDBSortedByControlOrderAsync()
        {
            return await Task.Run(this.LoadMetadataControlsAndInfoFromTemplateDBSortedByControlOrder).ConfigureAwait(true);
        }

        // Note that this currently completely replaces everything, rather than selectively.
        // Hoever, the Metadata table is relatively small, so the performance hit isn't worth the added code complexity to be efficient
        public bool LoadMetadataControlsAndInfoFromTemplateDBSortedByControlOrder()
        {
            this.MetadataInfo = null;
            this.MetadataControlsByLevel = null;
            this.MetadataControlsAll = null;

            // 1. Get the MetadataInfo
            if (false == TryLoadMetadataInfoFromTemplateDB())
            {
                // If there is no MetadataInfo, there shouldn't be a MetadataTemplate either
                return false;
            }

            // 2. Load the rows from the MetadataTable and load it into the corresponding structures
            return TryLoadMetadataControlFromTemplateDBSortedByLevel();
            //if (false == this.Database.TableExists(Constant.DBTables.MetadataTemplate))
            //{
            //    return;
            //}
            //// 2a Retrieve the complete metadata template table contents and load it into the metadataControlsAll structure
            //DataTable metadataTemplateTable = this.Database.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.MetadataTemplate + Sql.OrderBy + Constant.Control.ControlOrder);
            //this.MetadataControlsAll = new DataTableBackedList<MetadataControlRow>(metadataTemplateTable, row => new MetadataControlRow(row));

            //// 2b. Now get each level from the table and load it into the MetadataControlsByLevel dictionary 
            //this.MetadataControlsByLevel = CreateMetadataControlsByLevelFromMetadataControlsAll(this.Database, this.MetadataControlsAll);
        }

        public bool TryLoadMetadataInfoFromTemplateDB()
        {
            // 1. Get the MetadataInfo
            if (false == this.Database.TableExists(Constant.DBTables.MetadataInfo))
            {
                // If there is no MetadataInfo, we need to create an empty one
                return false;
            }
            // Retrieve the complete metadata template table contents and load it into the metadataControlsAll structure
            DataTable metadataInfoTable = this.Database.GetDataTableFromSelect($"{Sql.SelectStarFrom} {Constant.DBTables.MetadataInfo} {Sql.OrderBy} {Constant.Control.Level}");
            this.MetadataInfo = new DataTableBackedList<MetadataInfoRow>(metadataInfoTable, row => new MetadataInfoRow(row));
            return true;
        }

        public bool TryLoadMetadataControlFromTemplateDBSortedByLevel()
        {
            if (false == this.Database.TableExists(Constant.DBTables.MetadataTemplate))
            {
                return false;
            }
            // 2a Retrieve the complete metadata template table contents and load it into the metadataControlsAll structure
            DataTable metadataTemplateTable = this.Database.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.MetadataTemplate + Sql.OrderBy + Constant.Control.Level);
            this.MetadataControlsAll = new DataTableBackedList<MetadataControlRow>(metadataTemplateTable, row => new MetadataControlRow(row));

            // 2b. Now get each level from the table and load it into the MetadataControlsByLevel dictionary 
            this.MetadataControlsByLevel = CreateMetadataControlsByLevelFromMetadataControlsAll(this.Database, this.MetadataControlsAll);
            return true;
        }

        private static Dictionary<int, DataTableBackedList<MetadataControlRow>> CreateMetadataControlsByLevelFromMetadataControlsAll(SQLiteWrapper database, DataTableBackedList<MetadataControlRow> metadataControlsAll)
        {
            // Create an empty metadataControlsByLevel structure, with a row for each level regardless of whether it contains a control
            Dictionary<int, DataTableBackedList<MetadataControlRow>> metadataControlsByLevel = new Dictionary<int, DataTableBackedList<MetadataControlRow>>();
            if (null == metadataControlsAll)
            {
                return metadataControlsByLevel;
            }
            // Get the levels 
            List<int> levels = metadataControlsAll.AsEnumerable().Select(s => s.Level).Distinct().ToList();

            // Create a new dictionary of metadatacontrols for each level, perhaps repacing the old one
            foreach (int level in levels)
            {
                DataTable metadataTemplateTableByLevel = database.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.MetadataTemplate
                                                                                                                 + Sql.Where + Constant.Control.Level + Sql.Equal + Sql.Quote(level.ToString())
                                                                                                                 + Sql.OrderBy + Constant.Control.ControlOrder);
                metadataControlsByLevel.Add(
                    level,
                    new DataTableBackedList<MetadataControlRow>(metadataTemplateTableByLevel, row => new MetadataControlRow(row)));
            }
            return metadataControlsByLevel;
        }
        #endregion

        #region MetadataControls - Add a user defined control.
        public UpdateStateEnum AddMetadataControlToDataTableAndDatabase(int level, string controlType)
        {
            this.CreateBackupIfNeeded();

            // Create an empty metadata data structure and/or MetadataTable if they are missing. 
            this.CreateEmptyMetadataTablesIfNeeded();

            // create the row for the new control in the data table
            MetadataControlRow newControl = this.MetadataControlsAll.NewRow();
            string dataLabelPrefix;
            switch (controlType)
            {
                // Number controls
                case Constant.Control.Counter:
                    dataLabelPrefix = Constant.Control.Counter;
                    newControl.DefaultValue = Constant.ControlDefault.NumberDefaultValue;
                    newControl.Type = Constant.Control.Counter;
                    newControl.Tooltip = Constant.ControlDefault.CounterTooltip;
                    break;

                case Constant.Control.IntegerAny:
                    dataLabelPrefix = Constant.Control.LabelIntegerAny;
                    newControl.DefaultValue = Constant.ControlDefault.NumberDefaultValue;
                    newControl.Type = controlType;
                    newControl.Tooltip = Constant.ControlDefault.IntegerAnyTooltip;
                    break;

                case Constant.Control.IntegerPositive:
                    dataLabelPrefix = Constant.Control.IntegerPositive;
                    newControl.DefaultValue = Constant.ControlDefault.NumberDefaultValue;
                    newControl.Type = controlType;
                    newControl.Tooltip = Constant.ControlDefault.IntegerPositiveTooltip;
                    break;

                case Constant.Control.DecimalAny:
                    dataLabelPrefix = Constant.Control.LabelDecimalAny;
                    newControl.DefaultValue = Constant.ControlDefault.NumberDefaultValue;
                    newControl.Type = controlType;
                    newControl.Tooltip = Constant.ControlDefault.DecimalAnyTooltip;
                    break;

                case Constant.Control.DecimalPositive:
                    dataLabelPrefix = Constant.Control.DecimalPositive;
                    newControl.DefaultValue = Constant.ControlDefault.NumberDefaultValue;
                    newControl.Type = controlType;
                    newControl.Tooltip = Constant.ControlDefault.DecimalPositiveTooltip;
                    break;

                // Text controls
                case Constant.Control.Note:
                    dataLabelPrefix = Constant.Control.Note;
                    newControl.DefaultValue = Constant.ControlDefault.NoteDefaultValue;
                    newControl.Type = Constant.Control.Note;
                    newControl.Tooltip = Constant.ControlDefault.NoteTooltip;
                    break;

                // Text controls
                case Constant.Control.AlphaNumeric:
                    dataLabelPrefix = Constant.Control.AlphaNumeric;
                    newControl.DefaultValue = Constant.ControlDefault.AlphaNumericDefaultValue;
                    newControl.Type = Constant.Control.AlphaNumeric;
                    newControl.Tooltip = Constant.ControlDefault.AlphaNumericTooltip;
                    break;

                case Constant.Control.MultiLine:
                    dataLabelPrefix = Constant.Control.MultiLine;
                    newControl.DefaultValue = Constant.ControlDefault.MultiLineDefaultValue;
                    newControl.Type = Constant.Control.MultiLine;
                    newControl.Tooltip = Constant.ControlDefault.MultiLineTooltip;
                    break;

                // Other controls
                case Constant.Control.FixedChoice:
                    dataLabelPrefix = Constant.Control.Choice;
                    newControl.DefaultValue = Constant.ControlDefault.FixedChoiceDefaultValue;
                    newControl.Type = Constant.Control.FixedChoice;
                    newControl.Tooltip = Constant.ControlDefault.FixedChoiceTooltip;
                    break;

                case Constant.Control.MultiChoice:
                    dataLabelPrefix = Constant.Control.MultiChoice;
                    newControl.DefaultValue = Constant.ControlDefault.MultiChoiceDefaultValue;
                    newControl.Type = Constant.Control.MultiChoice;
                    newControl.Tooltip = Constant.ControlDefault.MultiChoiceTooltip;
                    break;

                case Constant.Control.Flag:
                    dataLabelPrefix = Constant.Control.Flag;
                    newControl.DefaultValue = Constant.ControlDefault.FlagValue;
                    newControl.Type = Constant.Control.Flag;
                    newControl.Tooltip = Constant.ControlDefault.FlagTooltip;
                    break;
 
                case Constant.Control.DateTime_:
                    dataLabelPrefix = Constant.ControlDefault.DateTimeCustomLabel; // We use a simple DateTime label as its cleaner
                    newControl.DefaultValue = DateTimeHandler.ToStringDatabaseDateTime(Constant.ControlDefault.DateTimeCustomDefaultValue);
                    newControl.Type = Constant.Control.DateTime_;
                    newControl.Tooltip = Constant.ControlDefault.DateTimeCustomTooltip;
                    break;

                case Constant.Control.Date_:
                    dataLabelPrefix = Constant.ControlDefault.Date_Label; // We use a Date_ label 
                    newControl.DefaultValue = DateTimeHandler.ToStringDatabaseDate(Constant.ControlDefault.Date_DefaultValue);
                    newControl.Type = Constant.Control.Date_;
                    newControl.Tooltip = Constant.ControlDefault.Date_Tooltip;
                    break;

                case Constant.Control.Time_:
                    dataLabelPrefix = Constant.ControlDefault.Time_Label; // We use a Time_ label
                    newControl.DefaultValue = DateTimeHandler.ToStringTime(Constant.ControlDefault.Time_DefaultValue);
                    newControl.Type = Constant.Control.Time_;
                    newControl.Tooltip = Constant.ControlDefault.Time_Tooltip;
                    break;
                default:
                    throw new NotSupportedException($"Unhandled control type {controlType}.");
            }

            // These other settings are common to all controls
            newControl.DataLabel = this.GetNextUniqueDataLabelInMetadataControls(level, dataLabelPrefix);
            newControl.Label = newControl.DataLabel;
            newControl.Visible = true;
            newControl.ExportToCSV = true;
            newControl.Level = level;

            // Set the order to the last one at the current level, as controls are always added to the end
            newControl.ControlOrder = this.MetadataControlsByLevel.TryGetValue(level, out var value)
                ? value.RowCount + 1
                : 1;
            newControl.SpreadsheetOrder = newControl.ControlOrder;

            // A. Add the new control to the database
            List<List<ColumnTuple>> controlInsertWrapper = new List<List<ColumnTuple>>() { newControl.CreateColumnTuplesWithWhereByID().Columns };
            this.Database.Insert(Constant.DBTables.MetadataTemplate, controlInsertWrapper);

            // B. Update the in memory table to reflect current database content
            // First, get the row that was just added to the Database as a datatable. This ensures its in the correct format
            DataTable justInsertedRow = this.Database.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.MetadataTemplate + Sql.Where
                                                                                                + Constant.Control.Level + Sql.Equal + Sql.Quote(level.ToString()) + Sql.And + Constant.Control.DataLabel + Sql.Equal + Sql.Quote(newControl.DataLabel));
            // Second, Update the data structures to reflect the newly inserted row.
            if (justInsertedRow.Rows.Count > 0)
            {
                this.MetadataControlsAll.AddRow(justInsertedRow.Rows[0]);
                if (this.MetadataControlsByLevel.ContainsKey(level))
                {
                    // Since we already have a table at that level, we just add the row
                    this.MetadataControlsByLevel[level].AddRow(justInsertedRow.Rows[0]);
                    return UpdateStateEnum.Modified;
                }

                // We don't have a table at that level, so we have to add the table with the row
                this.MetadataControlsByLevel.Add(level, new DataTableBackedList<MetadataControlRow>(justInsertedRow, row => new MetadataControlRow(row)));
                return UpdateStateEnum.Created;
            }
            return UpdateStateEnum.Failed;
        }
        #endregion

        #region MetadataControls - Remove a user defined Control from the database
        public async Task RemoveMetadataControlFromDataTableAndDatabase(int level, ControlRow controlToRemove)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(controlToRemove, nameof(controlToRemove));
            this.CreateBackupIfNeeded();

            // Capture state
            long removedControlOrder = controlToRemove.ControlOrder;
            long removedSpreadsheetOrder = controlToRemove.SpreadsheetOrder;

            // Part 1. Remove the control from the database
            //         and update the data table and data structures
            string where = Constant.DatabaseColumn.ID + Sql.Equal + controlToRemove.ID 
                           + Sql.And + Constant.Control.Level + Sql.Equal + Sql.Quote(level.ToString());
            this.Database.DeleteRows(Constant.DBTables.MetadataTemplate, where);
            await this.LoadMetadataControlsAndInfoFromTemplateTDBSortedByControlOrderAsync();

            // Part 2. Reread counter and spreadsheet orders; if they're greater than the one removed, decrement
            //         then update the data table and data structures
            List<ColumnTuplesWithWhere> controlUpdates = new List<ColumnTuplesWithWhere>();
            foreach (MetadataControlRow control in this.MetadataControlsAll)
            {
                if (control.Level != level)
                {
                    // Its a different level, so this control should not be altered
                    continue;
                }

                // This control is for this level 
                long controlOrder = control.ControlOrder;
                long spreadsheetOrder = control.SpreadsheetOrder;
                if (controlOrder >= removedControlOrder)
                {
                    // The ordering of this control should be changed, so do so
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
            this.Database.Update(Constant.DBTables.MetadataTemplate, controlUpdates);

            // update the in memory table to reflect current database content
            await this.LoadMetadataControlsAndInfoFromTemplateTDBSortedByControlOrderAsync();
        }
        #endregion

        #region MetadataControls: Edit levels: Delete a level from the database, or Move a level back/forward in the database
        // Delete the level from the two tables.
        public void MetadataDeleteLevelFromDatabase(int level)
        {

            // Assumes the tables exist, as does the level before this is invoked
            // Note that because we reset the IDs back to 1 every time we go through the loop, we always want to delete Level 1
            List<string> whereClause = new List<string> {
                $"{Constant.Control.Level} {Sql.Equal} {level}"
            };
            this.Database.Delete(Constant.DBTables.MetadataInfo, whereClause);
            this.Database.Delete(Constant.DBTables.MetadataTemplate, whereClause);

            // Update the other levels and IDs accordingly
            
            List<string> queries = new List<string>()
            {
                $"{Sql.Update} {Constant.DBTables.MetadataInfo} {Sql.Set} {Constant.Control.Level} {Sql.Equal} {Constant.Control.Level} - 1 {Sql.Where} {Constant.Control.Level} {Sql.GreaterThan} {level}",
                $"{Sql.Update} {Constant.DBTables.MetadataInfo} {Sql.Set} {Constant.DatabaseColumn.ID} {Sql.Equal} {Constant.DatabaseColumn.ID} - 1 {Sql.Where} {Constant.Control.Level} {Sql.GreaterThanEqual} {level}",
                $"{Sql.Update} {Constant.DBTables.MetadataTemplate} {Sql.Set} {Constant.Control.Level} {Sql.Equal} {Constant.Control.Level} - 1 {Sql.Where} {Constant.Control.Level} {Sql.GreaterThan} {level}"
            };
            this.Database.ExecuteNonQueryWrappedInBeginEnd(queries);
        }

        // Move the given level forward or backwards in the table
        public void MetadataMoveLevelForwardsOrBackwardsInDatabase(int level, int maxLevel, bool backwards)
        {
            
            // Should only be invoked if there are rows in the MetadataInfo table.
            // Assumes the tables exist, as does the level before this is invoked
            // Move the level bakcwards
            if ( (level == 1 && backwards) || (level == maxLevel && !backwards) )
            {
                // Its already at the beginning or the end
                return;
            }

            // We use this for temporarily changing levels and IDs to a value not held by another level or ID
            int tempLevel = level + maxLevel + 1; 
            // A factor based on whether we are moving forwards or backwards in the levels
            int correction = backwards ? -1 : 1;
            
            List<string> queries = new List<string>
            {
                // Adjust the level values in the MetadataInfo table
                // Change the current level's value to a unique value
                $"{Sql.Update} {Constant.DBTables.MetadataInfo} {Sql.Set} {Constant.Control.Level} {Sql.Equal} {tempLevel}  {Sql.Where} {Constant.Control.Level} {Sql.Equal} {level}",
                // Change the previous or next level's value (depending on the flag) to the current value
                $"{Sql.Update} {Constant.DBTables.MetadataInfo} {Sql.Set} {Constant.Control.Level} {Sql.Equal} {level}  {Sql.Where} {Constant.Control.Level} {Sql.Equal} {level} + {correction}",
                // Change the original level's value to the previous /next position depending on the flag
                $"{Sql.Update} {Constant.DBTables.MetadataInfo} {Sql.Set} {Constant.Control.Level} {Sql.Equal} {level + correction}  {Sql.Where} {Constant.Control.Level} {Sql.Equal} {tempLevel}",

                // Now do the same with the IDs

                // Change the current level's UD to a unique value
                $"{Sql.Update} {Constant.DBTables.MetadataInfo} {Sql.Set} {Constant.DatabaseColumn.ID} {Sql.Equal} {tempLevel}  {Sql.Where} {Constant.DatabaseColumn.ID} {Sql.Equal} {level}",
                // Change the previous or next IDs's value (depending on the flag) to the current value
                $"{Sql.Update} {Constant.DBTables.MetadataInfo} {Sql.Set} {Constant.DatabaseColumn.ID} {Sql.Equal} {level}  {Sql.Where} {Constant.DatabaseColumn.ID} {Sql.Equal} {level} + {correction}",
                // Change the original ID's value to the previous /next position depending on the flag
                $"{Sql.Update} {Constant.DBTables.MetadataInfo} {Sql.Set} {Constant.DatabaseColumn.ID} {Sql.Equal} {level + correction}  {Sql.Where} {Constant.DatabaseColumn.ID} {Sql.Equal} {tempLevel}",
               
                // Adjust the level values in the MetadataTemplate table

                // Change the current level's value to to a unique value
                $"{Sql.Update} {Constant.DBTables.MetadataTemplate} {Sql.Set} {Constant.Control.Level} {Sql.Equal} {tempLevel}  {Sql.Where} {Constant.Control.Level} {Sql.Equal} {level}",
                // Change the previous/next level's value to the current one
                $"{Sql.Update} {Constant.DBTables.MetadataTemplate} {Sql.Set} {Constant.Control.Level} {Sql.Equal} {level}  {Sql.Where} {Constant.Control.Level} {Sql.Equal} {level} + {correction}",
                // Change the original levels value to the previous/next position
                $"{Sql.Update} {Constant.DBTables.MetadataTemplate} {Sql.Set} {Constant.Control.Level} {Sql.Equal} {level + correction}  {Sql.Where} {Constant.Control.Level} {Sql.Equal} {tempLevel}"
            };

            this.Database.ExecuteNonQueryWrappedInBeginEnd(queries);
        }
        #endregion

        #region MetadataControls - Update Display order
        public void UpdateMetadataControlDisplayOrder(int level, string orderColumnName, Dictionary<string, long> newOrderByDataLabel)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(newOrderByDataLabel, nameof(newOrderByDataLabel));

            if (false == this.MetadataControlsByLevel.ContainsKey(level))
            {
                // This shouldn't happen
                TracePrint.StackTrace($"No such level in MetadataControlsByLevel: {level}");
            }

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

            long lastItem = this.MetadataControlsByLevel[level].Count();

            // update in memory table with new order
            foreach (MetadataControlRow metadataControl in this.MetadataControlsAll)
            {
                // Redundant check for null, as for some reason the CA1062 warning was still showing up
                ThrowIf.IsNullArgument(newOrderByDataLabel, nameof(newOrderByDataLabel));

                if (metadataControl.Level != level)
                {
                    // The control is not in the chosen level
                    continue;
                }
                
                string dataLabel = metadataControl.DataLabel;
                // Because we don't show all controls, we skip the ones that are missing.
                if (newOrderByDataLabel.TryGetValue(dataLabel, out var newOrder) == false)
                {
                    metadataControl.SpreadsheetOrder = lastItem--;
                    continue;
                }

                switch (orderColumnName)
                {
                    case Constant.Control.ControlOrder:
                        metadataControl.ControlOrder = newOrder;
                        break;
                    case Constant.Control.SpreadsheetOrder:
                        metadataControl.SpreadsheetOrder = newOrder;
                        break;
                }
            }
            // sync the newly ordered controls to the database,, which also reloads the controls into the controls data structure
            this.SyncMetadataControlsToDatabase(level);
        }
        #endregion

        #region MetadataControls - Sync single or multiple MetadataControlRow to the database
        // Update a given control in the database
        public void SyncMetadataControlsToDatabase(MetadataControlRow control)
        {
            // This form sync's a given control
            // Check the arguments for null 
            ThrowIf.IsNullArgument(control, nameof(control));

            // Create the where condition with the ID, but if the dataLabel is not empty, use the dataLabel as the where condition
            ColumnTuplesWithWhere ctw = control.CreateColumnTuplesWithWhereByID();
            this.Database.Update(Constant.DBTables.MetadataTemplate, ctw);
        }

        // Update all ControlOrder and SpreadsheetOrder column entries for the given level in the metadatatemplate database to match their in-memory counterparts.
        // Note that this only updates those entries. If other control entries exist in the database table, they will be unaffected.  
        private void SyncMetadataControlsToDatabase(int level)
        {
            // Utilities.PrintMethodName();
            List<ColumnTuplesWithWhere> columnsTuplesWithWhereList = new List<ColumnTuplesWithWhere>();    // holds columns which have changed for the current control
            foreach (MetadataControlRow control in this.MetadataControlsAll)
            {
                if (control.Level != level)
                {
                    // Skip controls that are not in the current level
                    continue;
                }
                // Update each row's Control and Spreadsheet order values
                List<ColumnTuple> columnTupleList = new List<ColumnTuple>();
                ColumnTuplesWithWhere columnTupleWithWhere = new ColumnTuplesWithWhere(columnTupleList, control.ID);
                columnTupleList.Add(new ColumnTuple(Constant.Control.ControlOrder, control.ControlOrder));
                columnTupleList.Add(new ColumnTuple(Constant.Control.SpreadsheetOrder, control.SpreadsheetOrder));
                columnsTuplesWithWhereList.Add(columnTupleWithWhere);
            }
            this.Database.Update(Constant.DBTables.MetadataTemplate, columnsTuplesWithWhereList);

            // Update the in memory table to reflect current database content
            // Perhaps not needed as the database was generated from the table, but guarantees resorts if control order has changed
            this.LoadMetadataControlsAndInfoFromTemplateDBSortedByControlOrder();
        }
        #endregion

        #region MetadataControls - Get NextMetadataUniqueDataLabel, NextDataLabel
        // Given a data label prefix, return it where it is appended with an integer (starting at 0) that makes it unique from others in the Controls data structure
        // e.g., 1st Counter becomes Counter0, 2nd Counter is Counter1 etc.
        public string GetNextUniqueDataLabelInMetadataControls(int level, string dataLabelPrefix)
        {
            // get all existing data labels, as we have to ensure that a new data label doesn't have the same name as an existing one
            List<string> dataLabels = new List<string>();
            List<string> labels = new List<string>();
            if (false == this.MetadataControlsByLevel.TryGetValue(level, out var value))
            {
                return dataLabelPrefix + "0";
            }
            foreach (MetadataControlRow control in value)
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

        // Given a label prefix, return it where it is appended with an integer (starting at 0) that makes it unique from others in the Controls data structure
        // e.g., 1st Counter becomes Counter0, 2nd Counter is Counter1 etc.
        public string GetNextUniqueLabelInMetadataControls(int level, string labelPrefix)
        {
            // get all existing labels, as we have to ensure that a new label doesn't have the same name as an existing one
            List<string> labels = new List<string>();
            foreach (MetadataControlRow control in this.MetadataControlsByLevel[level])
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

        #region MetadataControls - GetTypedDataLabelsExceptIDInSpreadsheetOrderFromMetadataControl
        public Dictionary<string, string> GetTypedDataLabelsExceptIDInSpreadsheetOrderFromMetadataControls(int level)
        {
            // Utilities.PrintMethodName();
            Dictionary<string, string> typedDataLabels = new Dictionary<string, string>();
            if (false == this.MetadataControlsByLevel.TryGetValue(level, out var value))
            {
                // Nothing here, so return an empty list
                return typedDataLabels;
            }
            IEnumerable<MetadataControlRow> controlsInSpreadsheetOrder = value.OrderBy(control => control.SpreadsheetOrder);
            foreach (MetadataControlRow control in controlsInSpreadsheetOrder)
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

        #region Controls - GetControlFromMetdataControls
        // Get the  data entry control matching the data label
        // Since data labels are unique, there could ever be only 0 or 1 match
        public MetadataControlRow GetControlFromMetadataControls(string dataLabel, int level)
        {
            if (dataLabel == null)
            {
                return null;
            }
            foreach (MetadataControlRow control in this.MetadataControlsByLevel[level])
            {
                if (dataLabel.Equals(control.DataLabel))
                {
                    return control;
                }
            }
            return null;
        }

        #endregion

        #region Info table - Create, Populate info table, and Update version info
        public void UpdateVersionNumber(string versionNumber)
        {
            this.Database.SetColumnToACommonValue(Constant.DBTables.TemplateInfo, Constant.DatabaseColumn.VersionCompatabily, versionNumber);
        }

        public void UpdateStandard(string standard)
        {
            this.Database.SetColumnToACommonValue(Constant.DBTables.TemplateInfo, Constant.DatabaseColumn.Standard, standard);
        }

        // Create and populate a TemplateInfo table in the database using the schema below
        private static void CreateAndPopulateTemplateInfoTable(SQLiteWrapper database)
        {
            // Add a TemplateInfo table only to the .tdb file
            List<SchemaColumnDefinition> templateInfoColumns = new List<SchemaColumnDefinition>
            {
                new SchemaColumnDefinition(Constant.DatabaseColumn.VersionCompatabily, Sql.Text, Constant.DatabaseValues.VersionNumberMinimum),
                new SchemaColumnDefinition(Constant.DatabaseColumn.Standard, Sql.Text, string.Empty)
            };
            database.CreateTable(Constant.DBTables.TemplateInfo, templateInfoColumns);

            // Add the version number of the current Timelapse program to the templateinfo table 
            List<List<ColumnTuple>> templateContents = new List<List<ColumnTuple>>();
            List<ColumnTuple> version = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.DatabaseColumn.VersionCompatabily, VersionChecks.GetTimelapseCurrentVersionNumber().ToString())
            };
            templateContents.Add(version);
            database.Insert(Constant.DBTables.TemplateInfo, templateContents);
        }

        // Return the current standard (if any) stored in the TemplateInfo table 
        public string TemplateGetStandard()
        {
            if (this.Database.TableExists(DBTables.TemplateInfo))
            {
                DataTable table = this.Database.GetDataTableFromSelect(Sql.Select + DatabaseColumn.Standard + Sql.From + DBTables.TemplateInfo);
                if (table.Rows.Count > 0)
                {
                    return (string)table.Rows[0][DatabaseColumn.Standard];
                }
            }

            return string.Empty;
        }
        #endregion

        #region MetadataInfoTable - UpsertMetadataInfoTableRow
        // Update or insert a metadata info row into the MetadataInfo Table
        public void UpsertMetadataInfoTableRow(int level, string guid = null, string alias = null)
        {
            // ID and Level are (or should be) the same. We do this so we can use a databacked list
            ColumnTuple primaryKeyTuple = new ColumnTuple(Constant.DatabaseColumn.ID, level);
            List<ColumnTuple> columnTuples = new List<ColumnTuple> {

                new ColumnTuple(Constant.Control.Level, level)
            };

            // Only add values that are actually present
            if (null != alias)
            {
                columnTuples.Add(new ColumnTuple(Constant.Control.Alias, alias));
            }

            if (null != guid)
            {
                columnTuples.Add(new ColumnTuple(Constant.Control.Guid, guid));
            }
            this.Database.UpsertRow(Constant.DBTables.MetadataInfo, primaryKeyTuple, columnTuples);
        }
        #endregion

        #region MetadataInfoTable - Various Gets
        // Get a list containing all level values in the MetadataInfo table
        // If there aren't any or if there is no MetadataInfo table, return an empty list
        public List<int> GetMetadataInfoTableLevels()
        {
            List<int> levels = new List<int>();
            if (false == this.DoesTableExist(Constant.DBTables.MetadataInfo))
            {
                return levels;
            }

            DataTable dt = this.Database.GetDataTableFromSelect(Sql.Select + Constant.Control.Level + Sql.From + Constant.DBTables.MetadataInfo);
            foreach (DataRow row in dt.Rows)
            {
                int level = Int32.Parse(row[Constant.Control.Level].ToString());
                levels.Add(level);
            }
            return levels;
        }

        // Get the maximimum level value in the MetadataInfo table
        // If there are none, return 0
        public int GetMetadataInfoTableMaxLevel()
        {
            List<int> levels = GetMetadataInfoTableLevels();
            return (levels.Count == 0)
                ? 0
                : levels.Max();
        }

        public DataTable GetMetadataInfoTableRow(int level)
        {
            string query = Sql.SelectStarFrom + Constant.DBTables.MetadataInfo + Sql.Where + Constant.Control.Level + Sql.Equal + level.ToString();
            return this.Database.GetDataTableFromSelect(query);
        }
        #endregion

        #region Misc: CreateBackupIfNeeded
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
        #endregion

        #region Private static methods: Create and populate various tables

        // Create an empty template table in the database based on the template schema
        private static void CreateEmptyTemplateTable(SQLiteWrapper database)
        {
            List<SchemaColumnDefinition> templateTableColumns = GetCommonSchema();
            templateTableColumns.Add(new SchemaColumnDefinition(Constant.Control.TextBoxWidth, Sql.Text));
            templateTableColumns.Add(new SchemaColumnDefinition(Constant.Control.Copyable, Sql.Text));
            database.CreateTable(Constant.DBTables.Template, templateTableColumns);
        }

        private static List<SchemaColumnDefinition> GetCommonSchema()
        {
            return new List<SchemaColumnDefinition>
            {
                new SchemaColumnDefinition(Constant.DatabaseColumn.ID, Sql.CreationStringPrimaryKey),
                new SchemaColumnDefinition(Constant.Control.ControlOrder, Sql.IntegerType),
                new SchemaColumnDefinition(Constant.Control.SpreadsheetOrder, Sql.IntegerType),
                new SchemaColumnDefinition(Constant.Control.Type, Sql.Text),
                new SchemaColumnDefinition(Constant.Control.DefaultValue, Sql.Text),
                new SchemaColumnDefinition(Constant.Control.Label, Sql.Text),
                new SchemaColumnDefinition(Constant.Control.DataLabel, Sql.Text),
                new SchemaColumnDefinition(Constant.Control.Tooltip, Sql.Text),
                // new SchemaColumnDefinition(Constant.Control.TextBoxWidth, Sql.Text),
                // new SchemaColumnDefinition(Constant.Control.Copyable, Sql.Text),
                new SchemaColumnDefinition(Constant.Control.Visible, Sql.Text),
                new SchemaColumnDefinition(Constant.Control.List, Sql.Text),
                new SchemaColumnDefinition(Constant.Control.ExportToCSV, Sql.Text, true)
            };
        }

        private static void PopulateTemplateTableWithStandardControls(SQLiteWrapper database)
        {
            // Add standard controls to template table
            List<List<ColumnTuple>> standardControls = new List<List<ColumnTuple>>();
            long controlOrder = 0; // The control order, a one based count incremented for every new entry
            long spreadsheetOrder = 0; // The spreadsheet order, a one based count incremented for every new entry

            // file
            standardControls.Add(CreateFileTuples(controlOrder, ++spreadsheetOrder, true));

            // relative path
            standardControls.Add(CreateRelativePathTuples(++controlOrder, ++spreadsheetOrder, true));

            // datetime
            standardControls.Add(CreateDateTimeTuples(++controlOrder, ++spreadsheetOrder, true));

            // delete flag
            standardControls.Add(CreateDeleteFlagTuples(++controlOrder, ++spreadsheetOrder, true));

            // insert standard controls into the template table
            database.Insert(Constant.DBTables.Template, standardControls);
        }

        protected static void AddExportToCSVColumnIfNeeded(SQLiteWrapper database)
        {
            // Backwards compatability: If the ExportToCSV column isn't in the template, it means we are opening up 
            // an old version of the template. Update the table by adding a new ExportToCSV column filled with the appropriate default
            // Note that the DeleteFlag export is set to false, while all theothers are true.
            if (false == database.SchemaIsColumnInTable(Constant.DBTables.Template, Constant.Control.ExportToCSV))
            {
                SchemaColumnDefinition scd = new SchemaColumnDefinition(Constant.Control.ExportToCSV, Constant.Control.Flag, Constant.BooleanValue.True);
                database.SchemaAddColumnToEndOfTable(Constant.DBTables.Template, scd);
                ColumnTuplesWithWhere ctww = new ColumnTuplesWithWhere();

                ctww.Columns.Add(new ColumnTuple(Constant.Control.ExportToCSV, Constant.BooleanValue.False));
                ctww.SetWhere(new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.DeleteFlag));
                database.Update(Constant.DBTables.Template, ctww);
            }
        }

        protected static void AddStandardToTemplateInfoColumnIfNeeded(SQLiteWrapper database)
        {
            // Backwards compatability: If the Standards column isn't in the template info, it means we are opening up 
            // an old version of the template. Update the table by adding a new Standards column filled with an empty value
            // Note that the DeleteFlag export is set to false, while all theothers are true.
            if (false == database.SchemaIsColumnInTable(Constant.DBTables.TemplateInfo, Constant.DatabaseColumn.Standard))
            {
                SchemaColumnDefinition scd = new SchemaColumnDefinition(Constant.DatabaseColumn.Standard, Sql.Text, string.Empty);
                database.SchemaAddColumnToEndOfTable(Constant.DBTables.TemplateInfo, scd);

                ColumnTuplesWithWhere ctww = new ColumnTuplesWithWhere();
                ctww.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.Standard, string.Empty));
                database.Update(Constant.DBTables.TemplateInfo, ctww);
            }
        }

        protected static void AddStandardToImageSetColumnIfNeeded(SQLiteWrapper database)
        {
            // Backwards compatability: If the Standards column isn't in the image set info, it means we are opening up 
            // an old version of the template. Update the table by adding a new Standards column filled with an empty value
            // Note that the DeleteFlag export is set to false, while all theothers are true.
            if (false == database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.Standard))
            {
                SchemaColumnDefinition scd = new SchemaColumnDefinition(Constant.DatabaseColumn.Standard, Sql.Text, string.Empty);
                database.SchemaAddColumnToEndOfTable(Constant.DBTables.ImageSet, scd);
                
                ColumnTuplesWithWhere ctww = new ColumnTuplesWithWhere();
                ctww.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.Standard, string.Empty));
                database.Update(Constant.DBTables.ImageSet, ctww);
            }
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
                new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.ControlDefaultTextValue),
                new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.File),
                new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.File),
                new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.FileTooltip),
                new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.FileWidth),
                new ColumnTuple(Constant.Control.Copyable, false),
                new ColumnTuple(Constant.Control.Visible, visible),
                new ColumnTuple(Constant.Control.List, Constant.ControlDefault.ControlDefaultTextValue),
                new ColumnTuple(Constant.Control.ExportToCSV, true),
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
                new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.ControlDefaultTextValue),
                new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.RelativePath),
                new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.RelativePath),
                new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.RelativePathTooltip),
                new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.RelativePathWidth),
                new ColumnTuple(Constant.Control.Copyable, false),
                new ColumnTuple(Constant.Control.Visible, visible),
                new ColumnTuple(Constant.Control.List, Constant.ControlDefault.ControlDefaultTextValue),
                new ColumnTuple(Constant.Control.ExportToCSV, true),
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
                new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.DateTimeDefaultWidth),
                new ColumnTuple(Constant.Control.Copyable, false),
                new ColumnTuple(Constant.Control.Visible, visible),
                new ColumnTuple(Constant.Control.List, Constant.ControlDefault.ControlDefaultTextValue),
                new ColumnTuple(Constant.Control.ExportToCSV, true),
            };
            return dateTime;
        }


        // Defines a DeleteFlag control. The definition is used by its caller to insert a DeleteFlag control into the template for backwards compatability.
        // Note that, unlike the others, the default is NOT to export it to a CSV file.
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
                new ColumnTuple(Constant.Control.List, Constant.ControlDefault.ControlDefaultTextValue),
                new ColumnTuple(Constant.Control.ExportToCSV, false),
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
                // TODO: Is this needed?
                // this.MetadataControls?.Dispose();
            }

            this.disposed = true;
        }
        #endregion
    }
}
