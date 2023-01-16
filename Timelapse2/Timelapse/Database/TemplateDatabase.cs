using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using Timelapse.Util;

namespace Timelapse.Database
{
    /// <summary>
    /// Timelapse Template Database.
    /// </summary>
    public class TemplateDatabase : IDisposable
    {
        #region Public / Protected Properties
        public DataTableBackedList<ControlRow> Controls { get; private set; }

        /// <summary>Gets the file name of the image database on disk.</summary>
        public string FilePath { get; private set; }

        public SQLiteWrapper Database { get; set; }
        #endregion

        #region Private Variables
        private bool disposed;
        private DataGrid editorDataGrid;
        public DateTime mostRecentBackup = DateTime.MinValue;
        private DataRowChangeEventHandler onTemplateTableRowChanged;
        #endregion

        #region Constructors
        protected TemplateDatabase(string filePath)
        {
            this.disposed = false;
            this.mostRecentBackup = FileBackup.GetMostRecentBackup(filePath);

            // open or create database
            this.Database = new SQLiteWrapper(filePath);
            this.FilePath = filePath;
        }
        #endregion

        #region Public / Protected Async Tasks - TryCreateOrOpen, OnDatabaseCreated, UpgradeDatabasesAndCompareTemplates, OnExistingDatabaseOpened
        public static async Task<TemplateDatabase> CreateOrOpenAsync(string filePath)
        {
            // check for an existing database before instantiating the databse as SQL wrapper instantiation creates the database file
            bool populateDatabase = !File.Exists(filePath);

            TemplateDatabase templateDatabase = new TemplateDatabase(filePath);
            if (populateDatabase)
            {
                // initialize the database if it's newly created
                await templateDatabase.OnDatabaseCreatedAsync(null).ConfigureAwait(true);
            }
            else
            {
                // The database file exists. However, we still need to check if its valid. 
                // We do this by checking the database integrity (which may raise an internal exception) and if that is ok, by checking if it has a TemplateTable. 
                if (templateDatabase.Database.PragmaGetQuickCheck() == false || templateDatabase.TableExists(Constant.DBTables.Template) == false)
                {
                    if (templateDatabase != null)
                    {
                        templateDatabase.Dispose();
                    }
                    return null;
                }

                // if it's an existing database check if it needs updating to current structure and load data tables
                await templateDatabase.OnExistingDatabaseOpenedAsync(null, null).ConfigureAwait(true);
            }
            return templateDatabase;
        }

        public static async Task<Tuple<bool, TemplateDatabase>> TryCreateOrOpenAsync(string filePath)
        {
            // Follow the MSDN design pattern for returning an IDisposable: https://www.codeproject.com/Questions/385273/Returning-a-Disposable-Object-from-a-Method
            TemplateDatabase disposableTemplateDB = null;
            try
            {
                disposableTemplateDB = await CreateOrOpenAsync(filePath).ConfigureAwait(true);
                TemplateDatabase returnableTemplateDB = disposableTemplateDB;
                // the returnableTemplateDB will be null if its not a valid template, e.g., if no TemplateTable exists in it
                bool successOrFail = returnableTemplateDB != null;
                return new Tuple<bool, TemplateDatabase>(successOrFail, returnableTemplateDB);
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage(String.Format("Failure in TryCreateOpen. {0}", exception.ToString()));
                return new Tuple<bool, TemplateDatabase>(false, null);
            }
            finally
            {
                if (disposableTemplateDB != null)
                {
                    disposableTemplateDB.Dispose();
                }
            }
        }

        protected virtual async Task OnDatabaseCreatedAsync(TemplateDatabase other)
        {
            // create the template table
            await Task.Run(() =>
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
                this.Database.CreateTable(Constant.DBTables.Template, templateTableColumns);



                // if an existing table was passed, this must be a ddb file. So clone its contents into this database
                if (other != null)
                {
                    this.SyncTemplateTableToDatabase(other.Controls);
                    return;
                }

                // no existing table to clone...
                // If other is null, then we are creating the template
                // Otherwise, we are adding a template to the .ddb file
                // In this case, we check to see which it is, as we add a TemplateInfo table only to the .tdb file
                if (other == null)
                {
                    List<SchemaColumnDefinition> templateInfoColumns = new List<SchemaColumnDefinition>
                    {
                        new SchemaColumnDefinition(Constant.DatabaseColumn.VersionCompatabily, Sql.Text, Constant.DatabaseValues.VersionNumberMinimum)
                    };
                    this.Database.CreateTable(Constant.DBTables.TemplateInfo, templateInfoColumns);

                    // so add the version number to the templateinfo table
                    List<List<ColumnTuple>> templateContents = new List<List<ColumnTuple>>();
                    // Get the version of the current Timelapse program

                    List<ColumnTuple> version = new List<ColumnTuple>
                    {
                        new ColumnTuple(Constant.DatabaseColumn.VersionCompatabily, VersionChecks.GetTimelapseCurrentVersionNumber().ToString())
                    };
                    templateContents.Add(version);
                    this.Database.Insert(Constant.DBTables.TemplateInfo, templateContents);
                }


                // Add standard controls to template table
                List<List<ColumnTuple>> standardControls = new List<List<ColumnTuple>>();
                long controlOrder = 0; // The control order, a one based count incremented for every new entry
                long spreadsheetOrder = 0; // The spreadsheet order, a one based count incremented for every new entry

                // file
                List<ColumnTuple> file = new List<ColumnTuple>
                {
                    new ColumnTuple(Constant.Control.ControlOrder, ++controlOrder),
                    new ColumnTuple(Constant.Control.SpreadsheetOrder, ++spreadsheetOrder),
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
                standardControls.Add(file);

                // relative path
                standardControls.Add(GetRelativePathTuples(++controlOrder, ++spreadsheetOrder, true));

                // datetime
                standardControls.Add(GetDateTimeTuples(++controlOrder, ++spreadsheetOrder, true));

                // delete flag
                standardControls.Add(GetDeleteFlagTuples(++controlOrder, ++spreadsheetOrder, true));

                // insert standard controls into the template table
                this.Database.Insert(Constant.DBTables.Template, standardControls);

                // populate the in memory version of the template table
                this.GetControlsSortedByControlOrder();
            }).ConfigureAwait(true);
        }

        protected virtual async Task UpgradeDatabasesAndCompareTemplatesAsync(TemplateDatabase other, TemplateSyncResults templateSyncResults)
        {
            await Task.Run(() =>
            {
                this.GetControlsSortedByControlOrder();
                // If there are things to do, add them here.
                // See pre-2.2.2.5 version for example code
            }).ConfigureAwait(true);
        }

        protected virtual async Task OnExistingDatabaseOpenedAsync(TemplateDatabase other, TemplateSyncResults templateSyncResults)
        {
            await Task.Run(this.GetControlsSortedByControlOrder).ConfigureAwait(true);
        }
        #endregion

        #region Public Methods - Boolean tests - Exists tables, Is database valid
        public bool TableExists(string dataTable)
        {
            return this.Database.TableExists(dataTable);
        }

        // Check if the database table specified in the path has a detections table
        public static bool TableExists(string dataTable, string dbPath)
        {
            // Note that no error checking is done - I assume, perhaps unwisely, that the file is a valid database
            // On tedting, it does return 'false' on an invalid ddb file, so I suppose that's ok.
            SQLiteWrapper db = new SQLiteWrapper(dbPath);
            return db.TableExists(dataTable);
        }

        // Check if the database is valid. 
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
                if (database?.Database == null)
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

        public bool IsControlCopyable(string dataLabel)
        {
            long id = this.GetControlIDFromTemplateTable(dataLabel);
            ControlRow control = this.Controls.Find(id);
            return control.Copyable;
        }

        // Return String.Empty only if each control is of a known type,
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
                    throw new NotSupportedException(String.Format("Unhandled control type {0}.", controlType));
            }
            newControl.ControlOrder = this.GetOrderForNewControl();
            newControl.SpreadsheetOrder = newControl.ControlOrder;

            // add the new control to the database
            List<List<ColumnTuple>> controlInsertWrapper = new List<List<ColumnTuple>>() { newControl.CreateColumnTuplesWithWhereByID().Columns };
            this.Database.Insert(Constant.DBTables.Template, controlInsertWrapper);

            // update the in memory table to reflect current database content
            // could just add the new row to the table but this is done in case a bug results in the insert lacking perfect fidelity
            this.GetControlsSortedByControlOrder();
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
                Debug.Assert(String.IsNullOrWhiteSpace(dataLabel) == false, String.Format("Encountered empty data label and label at ID {0} in template table.", control.ID));

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
                Debug.Assert(String.IsNullOrWhiteSpace(dataLabel) == false, String.Format("Encountered empty data label and label at ID {0} in template table.", control.ID));

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
            this.GetControlsSortedByControlOrder();

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
            this.GetControlsSortedByControlOrder();
        }
        #endregion

        #region Public /Private Methods Sync - ControlToDatabase, TemplateTableCOntrolAndSpreadsheetOrderToDatabase
        public void SyncControlToDatabase(ControlRow control)
        {
            // This form sync's by the ID
            SyncControlToDatabase(control, String.Empty);
        }

        public void SyncControlToDatabase(ControlRow control, string dataLabel)
        {
            // This generic form sync's by ID, or by a non-empty datalabel 
            // Check the arguments for null 
            ThrowIf.IsNullArgument(control, nameof(control));

            // this.CreateBackupIfNeeded();

            // Create the where condition with the ID, but if the dataLabel is not empty, use the dataLabel as the where condition
            ColumnTuplesWithWhere ctw = dataLabel == String.Empty
                ? control.CreateColumnTuplesWithWhereByID()
                : new ColumnTuplesWithWhere(control.CreateColumnTuplesWithWhereByID().Columns, new ColumnTuple(Constant.Control.DataLabel, dataLabel));
            this.Database.Update(Constant.DBTables.Template, ctw);

            // it's possible the passed data row isn't attached to TemplateTable, so refresh the table just in case
            this.GetControlsSortedByControlOrder();
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
            this.GetControlsSortedByControlOrder();
        }

        // Update the entire template database to match the in-memory template
        // Note that this version does this by recreating the entire table: 
        // We could likely be far more efficient by only updateding those entries that differ from the current entries.
        private void SyncTemplateTableToDatabase(DataTableBackedList<ControlRow> newTable)
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
            this.GetControlsSortedByControlOrder();
        }
        #endregion

        #region Public Methods - Misc: BindToEditorDataGrid, CreateBackupIfNeeded, Update DisplayOrder

        public void BindToEditorDataGrid(DataGrid dataGrid, DataRowChangeEventHandler onRowChanged)
        {
            this.editorDataGrid = dataGrid;
            this.onTemplateTableRowChanged = onRowChanged;
            this.GetControlsSortedByControlOrder();
        }


        protected void CreateBackupIfNeeded()
        {
            if (DateTime.Now - this.mostRecentBackup < Constant.File.BackupInterval)
            {
                // not due for a new backup yet
                return;
            }
            FileBackup.TryCreateBackup(this.FilePath);
            this.mostRecentBackup = DateTime.Now;
        }

        public void UpdateDisplayOrder(string orderColumnName, Dictionary<string, long> newOrderByDataLabel)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(newOrderByDataLabel, nameof(newOrderByDataLabel));

            // Utilities.PrintMethodName();

            // argument validation. Only ControlOrder and SpreadsheetOrder are orderable columns
            if (orderColumnName != Constant.Control.ControlOrder && orderColumnName != Constant.Control.SpreadsheetOrder)
            {
                throw new ArgumentOutOfRangeException(nameof(orderColumnName), String.Format("column '{0}' is not a control order column.  Only '{1}' and '{2}' are order columns.", orderColumnName, Constant.Control.ControlOrder, Constant.Control.SpreadsheetOrder));
            }

            List<long> uniqueOrderValues = newOrderByDataLabel.Values.Distinct().ToList();
            if (uniqueOrderValues.Count != newOrderByDataLabel.Count)
            {
                throw new ArgumentException(String.Format("newOrderByDataLabel: Each control must have a unique value for its order.  {0} duplicate values were passed for '{1}'.", newOrderByDataLabel.Count - uniqueOrderValues.Count, orderColumnName), nameof(newOrderByDataLabel));
            }

            uniqueOrderValues.Sort();
            int uniqueOrderValuesCount = uniqueOrderValues.Count;
            for (int control = 0; control < uniqueOrderValuesCount; ++control)
            {
                int expectedOrder = control + 1;
                if (uniqueOrderValues[control] != expectedOrder)
                {
                    throw new ArgumentOutOfRangeException(nameof(newOrderByDataLabel), String.Format("Control order must be a ones based count.  An order of {0} was passed instead of the expected order {1} for '{2}'.", uniqueOrderValues[0], expectedOrder, orderColumnName));
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
                    default:
                        // Ignore unhandled columns, as these are the ones that are not visible   
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
            string nextDataLabel = dataLabelPrefix + dataLabelUniqueIdentifier.ToString();
            while (dataLabels.Contains(nextDataLabel) || labels.Contains(nextDataLabel))
            {
                ++dataLabelUniqueIdentifier;
                nextDataLabel = dataLabelPrefix + dataLabelUniqueIdentifier.ToString();
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
            string nextLabel = labelPrefix + labelUniqueIdentifier.ToString();
            while (labels.Contains(nextLabel))
            {
                ++labelUniqueIdentifier;
                nextLabel = labelPrefix + labelUniqueIdentifier.ToString();
            }

            return nextLabel;
        }
        #endregion

        #region Private Methods - Get OrderForNewControl, ControlID, DatTime RelativePath, DeleteFlag, UTCOffset, Controls in sorted order
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

        private static List<ColumnTuple> GetDateTimeTuples(long controlOrder, long spreadsheetOrder, bool visible)
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

        // Defines a RelativePath control. The definition is used by its caller to insert a RelativePath control into the template for backwards compatability. 
        private static List<ColumnTuple> GetRelativePathTuples(long controlOrder, long spreadsheetOrder, bool visible)
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

        // Defines a DeleteFlag control. The definition is used by its caller to insert a DeleteFlag control into the template for backwards compatability. 
        private static List<ColumnTuple> GetDeleteFlagTuples(long controlOrder, long spreadsheetOrder, bool visible)
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

        private void GetControlsSortedByControlOrder()
        {
            // Utilities.PrintMethodName();
            DataTable templateTable = this.Database.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.Template + Sql.OrderBy + Constant.Control.ControlOrder);
            this.Controls = new DataTableBackedList<ControlRow>(templateTable, (DataRow row) => new ControlRow(row));
            this.Controls.BindDataGrid(this.editorDataGrid, this.onTemplateTableRowChanged);
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
                if (this.Controls != null)
                {
                    this.Controls.Dispose();
                }
            }

            this.disposed = true;
        }
        #endregion
    }
}
