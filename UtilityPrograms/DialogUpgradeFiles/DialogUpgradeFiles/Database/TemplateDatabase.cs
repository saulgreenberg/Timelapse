using DialogUpgradeFiles.Util;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DialogUpgradeFiles.Database
{
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
        //ZZ private DataGrid editorDataGrid;
        //ZZ public DateTime mostRecentBackup = DateTime.MinValue;
        //ZZprivate DataRowChangeEventHandler onTemplateTableRowChanged;
        #endregion

        #region Constructors
        protected TemplateDatabase(string filePath)
        {
            this.disposed = false;
            //this.mostRecentBackup = FileBackup.GetMostRecentBackup(filePath);

            // open or create database
            this.Database = new SQLiteWrapper(filePath);
            this.FilePath = filePath;
        }
        #endregion

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
            catch
            {
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

        public static async Task<TemplateDatabase> CreateOrOpenAsync(string filePath)
        {
            // check for an existing database before instantiating the database as SQL wrapper instantiation creates the database file
            bool populateDatabase = !File.Exists(filePath);

            TemplateDatabase templateDatabase = new TemplateDatabase(filePath);
            if (populateDatabase)
            {
                // initialize the database if it's newly created
                //await templatedatabase.ondatabasecreatedasync(null).configureawait(true);
            }
            else
            {
                //the database file exists. however, we still need to check if its valid.
                //we do this by checking the database integrity(which may raise an internal exception) and if that is ok, by checking if it has a templatetable.
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

        // Note the extra call in here, where we try to guarantee that the DateTime Defaults are in the new format.
        protected virtual async Task OnExistingDatabaseOpenedAsync(TemplateDatabase other, TemplateSyncResults templateSyncResults)
        {
            await Task.Run(() =>
            {
                this.GetControlsSortedByControlOrder();
                this.EnsureDataLabelsAndLabelsNotEmpty();
                this.EnsureUtcOffsetControlNotVisible();
                this.EnsureDateTimeDefaultInNewFormat();
                this.EnsureCurrentSchema();
            }).ConfigureAwait(true);
        }

        #region Private Methods - Ensure: Current Schema,  DataLabelsAndLabelsNotEmpty
        // Do various checks and corrections to the Template DB to maintain backwards compatability. 
        private void EnsureCurrentSchema()
        {
            // Add a RelativePath control to pre v2.1 databases if one hasn't already been inserted
            long relativePathID = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.RelativePath);
            if (relativePathID == -1)
            {
                // insert a relative path control, where its ID will be created as the next highest ID
                long order = this.GetOrderForNewControl();
                List<ColumnTuple> relativePathControl = GetRelativePathTuples(order, order, true);
                this.Database.Insert(Constant.DBTables.Template, new List<List<ColumnTuple>>() { relativePathControl });

                // move the relative path control to ID and order 2 for consistency with newly created templates
                this.SetControlID(Constant.DatabaseColumn.RelativePath, Constant.DatabaseValues.RelativePathPosition);
                this.SetControlOrders(Constant.DatabaseColumn.RelativePath, Constant.DatabaseValues.RelativePathPosition);
            }

            // add DateTime and UtcOffset controls to pre v2.1.0.5 databases if they haven't already been inserted
            long dateTimeID = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.DateTime);
            if (dateTimeID == -1)
            {
                ControlRow date = this.GetControlFromTemplateTable(Constant.DatabaseColumn.Date);
                ControlRow time = this.GetControlFromTemplateTable(Constant.DatabaseColumn.Time);

                // insert a date time control, where its ID will be created as the next highest ID
                // if either the date or time was visible make the date time visible
                bool dateTimeVisible = date.Visible || time.Visible;
                long order = this.GetOrderForNewControl();
                List<ColumnTuple> dateTimeControl = GetDateTimeTuples(order, order, dateTimeVisible);
                this.Database.Insert(Constant.DBTables.Template, new List<List<ColumnTuple>>() { dateTimeControl });

                // make date and time controls invisible as they're replaced by the date time control
                if (date.Visible)
                {
                    date.Visible = false;
                    this.SyncControlToDatabase(date);
                }
                if (time.Visible)
                {
                    time.Visible = false;
                    this.SyncControlToDatabase(time);
                }

                // move the date time control to ID and order 2 for consistency with newly created templates
                this.SetControlID(Constant.DatabaseColumn.DateTime, Constant.DatabaseValues.DateTimePosition);
                this.SetControlOrders(Constant.DatabaseColumn.DateTime, Constant.DatabaseValues.DateTimePosition);
            }

            long utcOffsetID = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.UtcOffset);
            if (utcOffsetID == -1)
            {
                // insert a relative path control, where its ID will be created as the next highest ID
                long order = this.GetOrderForNewControl();
                List<ColumnTuple> utcOffsetControl = GetUtcOffsetTuples(order, order, false);
                this.Database.Insert(Constant.DBTables.Template, new List<List<ColumnTuple>>() { utcOffsetControl });

                // move the relative path control to ID and order 2 for consistency with newly created templates
                this.SetControlID(Constant.DatabaseColumn.UtcOffset, Constant.DatabaseValues.UtcOffsetPosition);
                this.SetControlOrders(Constant.DatabaseColumn.UtcOffset, Constant.DatabaseValues.UtcOffsetPosition);
            }

            // Bug fix: 
            // Check to ensure that the image quality choice list in the template matches the expected default value,
            // A previously introduced bug had added spaces before several items in the list. This fixes that.
            // Note that this updates the template table in both the .tdb and .ddb file
            ControlRow imageQualityControlRow = this.GetControlFromTemplateTable(Constant.DatabaseColumn.ImageQuality);
            if (imageQualityControlRow != null && imageQualityControlRow.List != Constant.ImageQuality.ListOfValues)
            {
                imageQualityControlRow.List = Constant.ImageQuality.ListOfValues;
                this.SyncControlToDatabase(imageQualityControlRow);
            }

            // Backwards compatability: ensure a DeleteFlag control exists, replacing the MarkForDeletion data label used in pre 2.1.0.4 templates if necessary
            ControlRow markForDeletion = this.GetControlFromTemplateTable(Constant.ControlsDeprecated.MarkForDeletion);
            if (markForDeletion != null)
            {
                List<ColumnTuple> deleteFlagControl = GetDeleteFlagTuples(markForDeletion.ControlOrder, markForDeletion.SpreadsheetOrder, markForDeletion.Visible);
                this.Database.Upgrade(Constant.DBTables.Template, new ColumnTuplesWithWhere(deleteFlagControl, markForDeletion.ID));
                this.GetControlsSortedByControlOrder();
            }
            else if (this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.DeleteFlag) < 0)
            {
                // insert a DeleteFlag control, where its ID will be created as the next highest ID
                long order = this.GetOrderForNewControl();
                List<ColumnTuple> deleteFlagControl = GetDeleteFlagTuples(order, order, true);
                this.Database.Insert(Constant.DBTables.Template, new List<List<ColumnTuple>>() { deleteFlagControl });
                this.GetControlsSortedByControlOrder();
            }
        }

        // We no longer want to show the UtcOffset control, but templates may have set it to visible,
        // So this should over-ride and rewrite that.
        private void EnsureUtcOffsetControlNotVisible()
        {
            try
            {
                foreach (ControlRow control in this.Controls)
                {
                    if (control.DataLabel == Constant.DatabaseColumn.UtcOffset)
                    {
                        if (control.Visible == false)
                        {
                            return;
                        }
                        ColumnTuplesWithWhere columnsToUpgrade = new ColumnTuplesWithWhere();    // holds columns which have changed for the current control
                        control.Visible = false;
                        columnsToUpgrade.Columns.Add(new ColumnTuple(Constant.Control.Label, control.Visible));
                        columnsToUpgrade.SetWhere(control.ID);
                        this.Database.Upgrade(Constant.DBTables.Template, columnsToUpgrade);
                        return;
                    }
                }
            }
            catch
            {
                // Throw a custom exception so we can give a more informative fatal error message.
                // While this method does not normally fail, one user did report it crashing here due to his Citrix system
                // limiting how the template file is manipulated. The actual failure happens before this, but this
                // is where it is caught.
                Exception custom_e = new Exception(Constant.ExceptionTypes.TemplateReadWriteException, null);
                throw custom_e;
            }
        }

        // In the upgrade to 2.2.5.0, the format for the DefaultValue for DateTime was changed.
        // This upgrades the default in the template to the new format.
        private void EnsureDateTimeDefaultInNewFormat()
        {
            try
            {
                foreach (ControlRow control in this.Controls)
                {
                    if (control.DataLabel == Constant.DatabaseColumn.DateTime)
                    {
                        string defaultDateTime = Util.DateTimeHandler.ToStringDatabaseDateTime(Constant.ControlDefault.DateTimeValue);
                        if (control.DefaultValue == defaultDateTime)
                        {
                            return;
                        }
                        ColumnTuplesWithWhere columnsToUpgrade = new ColumnTuplesWithWhere();    // holds columns which have changed for the current control
                        control.DefaultValue = defaultDateTime;
                        columnsToUpgrade.Columns.Add(new ColumnTuple(Constant.Control.DefaultValue, control.DefaultValue));
                        columnsToUpgrade.SetWhere(control.ID);
                        this.Database.Upgrade(Constant.DBTables.Template, columnsToUpgrade);
                        return;
                    }
                }
            }
            catch
            {
                // Throw a custom exception so we can give a more informative fatal error message.
                // This method is not expected to fail, but...
                Exception custom_e = new Exception(Constant.ExceptionTypes.TemplateReadWriteException, null);
                throw custom_e;
            }
        }
        /// <summary>
        /// Supply default values for any empty labels or data labels are non-empty, updating both TemplateTable and the database as needed
        /// </summary>
        private void EnsureDataLabelsAndLabelsNotEmpty()
        {
            // All the code below goes through the template table to see if there are any non-empty labels / data labels,
            // and if so, updates them to a reasonable value. If both are empty, it keeps track of its type and creates
            // a label called (say) Counter3 for the third counter that has no label. If there is no DataLabel value, it
            // makes it the same as the label. Ultimately, it guarantees that there will always be a (hopefully unique)
            // data label and label name. 
            // As well, the contents of the template table are loaded into memory.
            try
            {
                foreach (ControlRow control in this.Controls)
                {
                    // Check if various values are empty, and if so update the row and fill the dataline with appropriate defaults
                    ColumnTuplesWithWhere columnsToUpgrade = new ColumnTuplesWithWhere();    // holds columns which have changed for the current control
                    bool noDataLabel = String.IsNullOrWhiteSpace(control.DataLabel);
                    bool noLabel = String.IsNullOrWhiteSpace(control.Label);
                    if (noDataLabel && noLabel)
                    {
                        string dataLabel = this.GetNextUniqueDataLabel(control.Type);
                        columnsToUpgrade.Columns.Add(new ColumnTuple(Constant.Control.Label, dataLabel));
                        columnsToUpgrade.Columns.Add(new ColumnTuple(Constant.Control.DataLabel, dataLabel));
                        control.Label = dataLabel;
                        control.DataLabel = dataLabel;
                    }
                    else if (noLabel)
                    {
                        string label = control.DataLabel;
                        foreach (ControlRow tmpcontrol in this.Controls)
                        {
                            if (tmpcontrol.Label == label)
                            {
                                // check if a label of the same name already exists
                                label = this.GetNextUniqueDataLabel(tmpcontrol.Type);
                                break;
                            }
                        }
                        columnsToUpgrade.Columns.Add(new ColumnTuple(Constant.Control.Label, label));
                        control.Label = label;
                    }
                    else if (noDataLabel)
                    {
                        // No data label but a label, so use the label's value as the data label
                        columnsToUpgrade.Columns.Add(new ColumnTuple(Constant.Control.DataLabel, control.Label));
                        control.DataLabel = control.Label;
                    }

                    // Now add the new values to the database
                    if (columnsToUpgrade.Columns.Count > 0)
                    {
                        columnsToUpgrade.SetWhere(control.ID);
                        this.Database.Upgrade(Constant.DBTables.Template, columnsToUpgrade);
                    }
                }
            }
            catch
            {
                // Throw a custom exception so we can give a more informative fatal error message.
                // While this method does not normally fail, one user did report it crashing here due to his Citrix system
                // limiting how the template file is manipulated. The actual failure happens before this, but this
                // is where it is caught.
                Exception custom_e = new Exception(Constant.ExceptionTypes.TemplateReadWriteException, null);
                throw custom_e;
            }
        }

        // Make sure the default value is one allowed in the choice list. 

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

        public bool IsControlCopyable(string dataLabel)
        {
            long id = this.GetControlIDFromTemplateTable(dataLabel);
            ControlRow control = this.Controls.Find(id);
            return control.Copyable;
        }
        #endregion

        #region Private Methods - Set ControlOrder, ControlID
        /// <summary>
        /// Set the order of the specified control to the specified value, shifting other controls' orders as needed.
        /// </summary>
        private void SetControlOrders(string dataLabel, int order)
        {
            if ((order < 1) || (order > this.Controls.RowCount))
            {
                throw new ArgumentOutOfRangeException(nameof(order), "Control and spreadsheet orders must be contiguous ones based values.");
            }

            Dictionary<string, long> newControlOrderByDataLabel = new Dictionary<string, long>();
            Dictionary<string, long> newSpreadsheetOrderByDataLabel = new Dictionary<string, long>();
            foreach (ControlRow control in this.Controls)
            {
                if (control.DataLabel == dataLabel)
                {
                    newControlOrderByDataLabel.Add(dataLabel, order);
                    newSpreadsheetOrderByDataLabel.Add(dataLabel, order);
                }
                else
                {
                    long currentControlOrder = control.ControlOrder;
                    if (currentControlOrder >= order)
                    {
                        ++currentControlOrder;
                    }
                    newControlOrderByDataLabel.Add(control.DataLabel, currentControlOrder);

                    long currentSpreadsheetOrder = control.SpreadsheetOrder;
                    if (currentSpreadsheetOrder >= order)
                    {
                        ++currentSpreadsheetOrder;
                    }
                    newSpreadsheetOrderByDataLabel.Add(control.DataLabel, currentSpreadsheetOrder);
                }
            }

            this.UpgradeDisplayOrder(Constant.Control.ControlOrder, newControlOrderByDataLabel);
            this.UpgradeDisplayOrder(Constant.Control.SpreadsheetOrder, newSpreadsheetOrderByDataLabel);
            this.GetControlsSortedByControlOrder();
        }

        /// <summary>
        /// Set the ID of the specified control to the specified value, shifting other controls' IDs as needed.
        /// </summary>
        private void SetControlID(string dataLabel, int newID)
        {
            // Utilities.PrintMethodName();
            // nothing to do
            long currentID = this.GetControlIDFromTemplateTable(dataLabel);
            if (currentID == newID)
            {
                return;
            }

            // move other controls out of the way if the requested ID is in use
            ControlRow conflictingControl = this.Controls.Find(newID);
            List<string> queries = new List<string>();
            if (conflictingControl != null)
            {
                // First update: because any changed IDs have to be unique, first move them beyond the current ID range
                long maximumID = 0;
                foreach (ControlRow control in this.Controls)
                {
                    if (maximumID < control.ID)
                    {
                        maximumID = control.ID;
                    }
                }
                Debug.Assert((maximumID > 0) && (maximumID <= Int64.MaxValue),
                    $"Maximum ID found is {maximumID}, which is out of range.");
                string jumpAmount = maximumID.ToString();

                string increaseIDs = Sql.Update + Constant.DBTables.Template;
                increaseIDs += Sql.Set + Constant.DatabaseColumn.ID + " = " + Constant.DatabaseColumn.ID + " + 1 + " + jumpAmount;
                increaseIDs += Sql.Where + Constant.DatabaseColumn.ID + " >= " + newID;
                queries.Add(increaseIDs);

                // Second update: decrease IDs above newID to be one more than their original value
                // This leaves everything in sequence except for an open spot at newID.
                string reduceIDs = Sql.Update + Constant.DBTables.Template;
                reduceIDs += Sql.Set + Constant.DatabaseColumn.ID + " = " + Constant.DatabaseColumn.ID + " - " + jumpAmount;
                reduceIDs += Sql.Where + Constant.DatabaseColumn.ID + " >= " + newID;
                queries.Add(reduceIDs);
            }

            // 3rd update: change the target ID to the desired ID
            // this.CreateBackupIfNeeded();

            string setControlID = Sql.Update + Constant.DBTables.Template;
            setControlID += Sql.Set + Constant.DatabaseColumn.ID + " = " + newID;
            setControlID += Sql.Where + Constant.Control.DataLabel + " = '" + dataLabel + "'";
            queries.Add(setControlID);
            this.Database.ExecuteNonQueryWrappedInBeginEnd(queries);

            this.GetControlsSortedByControlOrder();
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
                new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.DateTimeValue.UtcDateTime),
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

        private static List<ColumnTuple> GetUtcOffsetTuples(long controlOrder, long spreadsheetOrder, bool visible)
        {
            List<ColumnTuple> utcOffset = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.Control.ControlOrder, controlOrder),
                new ColumnTuple(Constant.Control.SpreadsheetOrder, spreadsheetOrder),
                new ColumnTuple(Constant.Control.Type, Constant.DatabaseColumn.UtcOffset),
                new ColumnTuple(Constant.Control.DefaultValue, Constant.ControlDefault.DateTimeValue.Offset),
                new ColumnTuple(Constant.Control.Label, Constant.DatabaseColumn.UtcOffset),
                new ColumnTuple(Constant.Control.DataLabel, Constant.DatabaseColumn.UtcOffset),
                new ColumnTuple(Constant.Control.Tooltip, Constant.ControlDefault.UtcOffsetTooltip),
                new ColumnTuple(Constant.Control.TextBoxWidth, Constant.ControlDefault.UtcOffsetWidth),
                new ColumnTuple(Constant.Control.Copyable, false),
                new ColumnTuple(Constant.Control.Visible, visible),
                new ColumnTuple(Constant.Control.List, Constant.ControlDefault.Value)
            };
            return utcOffset;
        }

        public void GetControlsSortedByControlOrder()
        {
            // Utilities.PrintMethodName();
            DataTable templateTable = this.Database.GetDataTableFromSelect(Sql.SelectStarFrom + Constant.DBTables.Template + Sql.OrderBy + Constant.Control.ControlOrder);
            this.Controls = new DataTableBackedList<ControlRow>(templateTable, row => new ControlRow(row));
            this.Controls.BindDataGrid(null, null);
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
            this.Database.Upgrade(Constant.DBTables.Template, ctw);

            // it's possible the passed data row isn't attached to TemplateTable, so refresh the table just in case
            this.GetControlsSortedByControlOrder();
        }


        // Upgrade all ControlOrder and SpreadsheetOrder column entries in the template database to match their in-memory counterparts
        public void SyncTemplateTableControlAndSpreadsheetOrderToDatabase()
        {
            // Utilities.PrintMethodName();
            List<ColumnTuplesWithWhere> columnsTuplesWithWhereList = new List<ColumnTuplesWithWhere>();    // holds columns which have changed for the current control
            foreach (ControlRow control in this.Controls)
            {
                // Upgrade each row's Control and Spreadsheet order values
                List<ColumnTuple> columnTupleList = new List<ColumnTuple>();
                ColumnTuplesWithWhere columnTupleWithWhere = new ColumnTuplesWithWhere(columnTupleList, control.ID);
                columnTupleList.Add(new ColumnTuple(Constant.Control.ControlOrder, control.ControlOrder));
                columnTupleList.Add(new ColumnTuple(Constant.Control.SpreadsheetOrder, control.SpreadsheetOrder));
                columnsTuplesWithWhereList.Add(columnTupleWithWhere);
            }
            this.Database.Upgrade(Constant.DBTables.Template, columnsTuplesWithWhereList);
            // update the in memory table to reflect current database content
            // could just use the new table but this is done in case a bug results in the insert lacking perfect fidelity
            this.GetControlsSortedByControlOrder();
        }

        // Upgrade the entire template database to match the in-memory template
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

        #region Public Methods - Misc: BindToEditorDataGrid, CreateBackupIfNeeded, Upgrade DisplayOrder

        //public void BindToEditorDataGrid(DataGrid dataGrid, DataRowChangeEventHandler onRowChanged)
        //{
        //    this.editorDataGrid = dataGrid;
        //    this.onTemplateTableRowChanged = onRowChanged;
        //    this.GetControlsSortedByControlOrder();
        //}

        //protected void CreateBackupIfNeeded()
        //{
        //    return;
        //    if (DateTime.Now - this.mostRecentBackup < Constant.File.BackupInterval)
        //    {
        //        // not due for a new backup yet
        //        return;
        //    }
        //    FileBackup.TryCreateBackup(this.FilePath);
        //    this.mostRecentBackup = DateTime.Now;
        //}

        public void UpgradeDisplayOrder(string orderColumnName, Dictionary<string, long> newOrderByDataLabel)
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
                        $"Control order must be a one's based count.  An order of {uniqueOrderValues[0]} was passed instead of the expected order {expectedOrder} for '{orderColumnName}'.");
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

        #region Public Methods - RemoveuserDefinedControl
        public void UpgradeTemplateRemoveControl(ControlRow controlToRemove)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(controlToRemove, nameof(controlToRemove));

            // capture state
            long removedControlOrder = controlToRemove.ControlOrder;
            long removedSpreadsheetOrder = controlToRemove.SpreadsheetOrder;

            // drop the control from the database and data table
            string where = Constant.DatabaseColumn.ID + " = " + controlToRemove.ID;
            this.Database.DeleteRows(Constant.DBTables.Template, where);
            this.GetControlsSortedByControlOrder();

            // regenerate counter and spreadsheet orders; if they're greater than the one removed, decrement
            List<ColumnTuplesWithWhere> controlUpgrades = new List<ColumnTuplesWithWhere>();
            foreach (ControlRow control in this.Controls)
            {
                long controlOrder = control.ControlOrder;
                long spreadsheetOrder = control.SpreadsheetOrder;

                if (controlOrder >= removedControlOrder)
                {
                    List<ColumnTuple> controlUpgrade = new List<ColumnTuple>
                    {
                        new ColumnTuple(Constant.Control.ControlOrder, controlOrder - 1)
                    };
                    control.ControlOrder = controlOrder - 1;
                    controlUpgrades.Add(new ColumnTuplesWithWhere(controlUpgrade, control.ID));
                }

                if (spreadsheetOrder >= removedSpreadsheetOrder)
                {
                    List<ColumnTuple> controlUpgrade = new List<ColumnTuple>
                    {
                        new ColumnTuple(Constant.Control.SpreadsheetOrder, spreadsheetOrder - 1)
                    };
                    control.SpreadsheetOrder = spreadsheetOrder - 1;
                    controlUpgrades.Add(new ColumnTuplesWithWhere(controlUpgrade, control.ID));
                }
            }
            this.Database.Upgrade(Constant.DBTables.Template, controlUpgrades);

            // update the in memory table to reflect current database content
            // should not be necessary but this is done to mitigate divergence in case a bug results in the delete lacking perfect fidelity
            this.GetControlsSortedByControlOrder();
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
