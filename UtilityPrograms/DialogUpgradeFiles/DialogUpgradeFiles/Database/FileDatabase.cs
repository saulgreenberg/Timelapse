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
    public class FileDatabase : TemplateDatabase
    {
        #region Private variables
        private bool disposed;
        #endregion

        #region Properties 
        public FileTable FileTable { get; private set; }
        public string FileName { get; private set; }
        public string FolderPath { get; private set; }
        public Dictionary<string, string> DataLabelFromStandardControlType { get; private set; }
        public Dictionary<string, FileTableColumn> FileTableColumnsByDataLabel { get; private set; }
        public ImageSetRow ImageSet { get; private set; }
        public DataTableBackedList<MarkerRow> Markers { get; private set; }

        public int CountAllCurrentlySelectedFiles => this.FileTable?.RowCount ?? 0;

        #endregion

        #region Create or Open the Database
        public FileDatabase(string filePath)
            : base(filePath)
        {
            this.DataLabelFromStandardControlType = new Dictionary<string, string>();
            this.disposed = false;
            this.FolderPath = Path.GetDirectoryName(filePath);
            this.FileName = Path.GetFileName(filePath);
            this.FileTableColumnsByDataLabel = new Dictionary<string, FileTableColumn>();
        }

        private static SchemaColumnDefinition CreateFileDataColumnDefinition(ControlRow control)
        {
            if (control.DataLabel == Constant.DatabaseColumn.DateTime)
            {
                return new SchemaColumnDefinition(control.DataLabel, "DATETIME", DateTimeHandler.ToStringDatabaseDateTime(Constant.ControlDefault.DateTimeValue));
            }
            if (control.DataLabel == Constant.DatabaseColumn.UtcOffset)
            {
                // UTC offsets are typically represented as TimeSpans but the least awkward way to store them in SQLite is as a real column containing the offset in
                // hours.  This is because SQLite
                // - handles TIME columns as DateTime rather than TimeSpan, requiring the associated DataTable column also be of type DateTime
                // - doesn't support negative values in time formats, requiring offsets for time zones west of Greenwich be represented as positive values
                // - imposes an upper bound of 24 hours on time formats, meaning the 26 hour range of UTC offsets (UTC-12 to UTC+14) cannot be accomodated
                // - lacks support for DateTimeOffset, so whilst offset information can be written to the database it cannot be read from the database as .NET
                //   supports only DateTimes whose offset matches the current system time zone
                // Storing offsets as ticks, milliseconds, seconds, minutes, or days offers equivalent functionality.  Potential for rounding error in roundtrip 
                // calculations on offsets is similar to hours for all formats other than an INTEGER (long) column containing ticks.  Ticks are a common 
                // implementation choice but testing shows no roundoff errors at single tick precision (100 nanoseconds) when using hours.  Even with TimeSpans 
                // near the upper bound of 256M hours, well beyond the plausible range of time zone calculations.  So there does not appear to be any reason to 
                // avoid using hours for readability when working with the database directly.
                return new SchemaColumnDefinition(control.DataLabel, "REAL", DateTimeHandler.ToStringDatabaseUtcOffset(Constant.ControlDefault.DateTimeValue.Offset));
            }
            if (String.IsNullOrWhiteSpace(control.DefaultValue))
            {
                return new SchemaColumnDefinition(control.DataLabel, Sql.Text, String.Empty);
            }
            return new SchemaColumnDefinition(control.DataLabel, Sql.Text, control.DefaultValue);
        }

        #endregion

        #region Upgrade the database as needed from older to newer formats to preserve backwards compatability 
        public async Task UDBUpgradeDatabasesForBackwardsCompatabilityAsync(string timelapseVersion)
        {
            // Note that we avoid Selecting * from the DataTable, as that could be an expensive operation
            // Instead, we operate directly on the database. There is only one exception (updating DateTime),
            // as we have to regenerate all the column's values
            // TODO XXXX: REPLACE CODE BELOW WITH new existing function this.TryGetImageSetVersionNumber(out string imageSetVersionNumber, false)), AS IT DOES EVERYTHING
            // Get the image set. We will be checking some of its values as we go along
            this.ImageSetLoadFromDatabase();
            await Task.Delay(Constant.BusyState.SleepTime);
            // Some comparisons are triggered by comparing the version number stored in the DB with 
            // particular version numbers where known changes occured 
            // Note: if we can't retrieve the version number from the image set, then set it to a very low version number to guarantee all checks will be made
            string lowestVersionNumber = "1.0.0.0";
            bool versionCompatabilityColumnExists = this.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.VersionCompatabily);
            string imageSetVersionNumber = versionCompatabilityColumnExists ? this.ImageSet.VersionCompatability
                : lowestVersionNumber;

            // Step 1. Check the FileTable for missing columns
            // RelativePath column (if missing) needs to be added 
            if (this.Database.SchemaIsColumnInTable(Constant.DBTables.FileData, Constant.DatabaseColumn.RelativePath) == false)
            {
                long relativePathID = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.RelativePath);
                ControlRow relativePathControl = this.Controls.Find(relativePathID);
                SchemaColumnDefinition columnDefinition = CreateFileDataColumnDefinition(relativePathControl);
                this.Database.SchemaAddColumnToTable(Constant.DBTables.FileData, Constant.DatabaseValues.RelativePathPosition, columnDefinition);
                await Task.Delay(Constant.BusyState.SleepTime);
            }


            // DateTime column (if missing) needs to be added 
            bool dateTimeColumnWasInDDB = this.Database.SchemaIsColumnInTable(Constant.DBTables.FileData, Constant.DatabaseColumn.DateTime);
            if (dateTimeColumnWasInDDB == false)
            {
                long dateTimeID = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.DateTime);
                ControlRow dateTimeControl = this.Controls.Find(dateTimeID);
                SchemaColumnDefinition columnDefinition = CreateFileDataColumnDefinition(dateTimeControl);
                this.Database.SchemaAddColumnToTable(Constant.DBTables.FileData, Constant.DatabaseValues.DateTimePosition, columnDefinition);
                await Task.Delay(Constant.BusyState.SleepTime);
            }

            // UTCOffset column (if missing) needs to be added 
            if (this.Database.SchemaIsColumnInTable(Constant.DBTables.FileData, Constant.DatabaseColumn.UtcOffset) == false)
            {
                long utcOffsetID = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.UtcOffset);
                ControlRow utcOffsetControl = this.Controls.Find(utcOffsetID);
                // ZZZ If utcOffsetControl isn't there, what to do!

                // There is no existing utcOffset controls in old database that had only Date and Time columns
                // So we have to create it from scratch.
                SchemaColumnDefinition columnDefinition = utcOffsetControl == null 
                    ? new SchemaColumnDefinition(Constant.DatabaseColumn.UtcOffset, Sql.Text, "0.00") 
                    : CreateFileDataColumnDefinition(utcOffsetControl);
                this.Database.SchemaAddColumnToTable(Constant.DBTables.FileData, Constant.DatabaseValues.UtcOffsetPosition, columnDefinition);
                await Task.Delay(Constant.BusyState.SleepTime);
            }

            // Remove MarkForDeletion column and add DeleteFlag column(if needed)
            bool hasMarkForDeletion = this.Database.SchemaIsColumnInTable(Constant.DBTables.FileData, Constant.ControlsDeprecated.MarkForDeletion);
            bool hasDeleteFlag = this.Database.SchemaIsColumnInTable(Constant.DBTables.FileData, Constant.DatabaseColumn.DeleteFlag);
            if (hasMarkForDeletion && (hasDeleteFlag == false))
            {
                // migrate any existing MarkForDeletion column to DeleteFlag
                // this is likely the most typical case
                this.Database.SchemaRenameColumn(Constant.DBTables.FileData, Constant.ControlsDeprecated.MarkForDeletion, Constant.DatabaseColumn.DeleteFlag);
            }
            else if (hasMarkForDeletion && hasDeleteFlag)
            {
                // if both MarkForDeletion and DeleteFlag are present drop MarkForDeletion
                // this is not expected to occur
                this.Database.SchemaDeleteColumn(Constant.DBTables.FileData, Constant.ControlsDeprecated.MarkForDeletion);
            }
            else if (hasDeleteFlag == false)
            {
                // if there's neither a MarkForDeletion or DeleteFlag column add DeleteFlag
                long id = this.GetControlIDFromTemplateTable(Constant.DatabaseColumn.DeleteFlag);
                ControlRow control = this.Controls.Find(id);
                SchemaColumnDefinition columnDefinition = CreateFileDataColumnDefinition(control);
                this.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.FileData, columnDefinition);
            }
            await Task.Delay(Constant.BusyState.SleepTime);

            // STEP 2. Check the ImageTable for missing columns
            // Make sure that all the string data in the datatable has white space trimmed from its beginning and end
            // This is needed as the custom selection doesn't work well in testing comparisons if there is leading or trailing white space in it
            // Newer versions of Timelapse  trim the data as it is entered, but older versions did not, so this is to make it backwards-compatable.
            // The WhiteSpaceExists column in the ImageSet Table did not exist before this version, so we add it to the table if needed. If it exists, then 
            // we know the data has been trimmed and we don't have to do it again as the newer versions take care of trimmingon the fly.
            bool whiteSpaceColumnExists = this.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.WhiteSpaceTrimmed);
            if (!whiteSpaceColumnExists)
            {
                // create the whitespace column
                this.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.ImageSet, new SchemaColumnDefinition(Constant.DatabaseColumn.WhiteSpaceTrimmed, Sql.Text, Constant.BooleanValue.False));

                // trim whitespace from the data table
                this.Database.TrimWhitespace(Constant.DBTables.FileData, this.GetDataLabelsExceptIDInSpreadsheetOrder());

                // mark image set as whitespace trimmed
                // This still has to be synchronized, which will occur after we prepare all missing columns
                this.ImageSetLoadFromDatabase();
                this.ImageSet.WhitespaceTrimmed = true;
                await Task.Delay(Constant.BusyState.SleepTime);
            }

            // Null test check against the version number
            // Versions prior to 2.2.2.4 may have set nulls as default values, which don't interact well with some aspects of Timelapse. 
            // Repair by turning all nulls in FileTable, if any, into empty strings
            // SAULXX Note that we could likely remove the WhiteSpaceTrimmed column and use the version number instead but we need to check if that is backwards compatable before doing so.
            string firstVersionWithNullCheck = "2.2.2.4";
            if (VersionChecks.IsVersion1GreaterThanVersion2(firstVersionWithNullCheck, imageSetVersionNumber))
            {
                this.Database.ChangeNullToEmptyString(Constant.DBTables.FileData, this.GetDataLabelsExceptIDInSpreadsheetOrder());
                await Task.Delay(Constant.BusyState.SleepTime);
            }

            // Upgrades the UTCOffset format. The issue is that the offset could have been written in the form +3,00 instead of +3.00 (i.e. with a comma)
            // depending on the computer's culture. 
            string firstVersionWithUTCOffsetCheck = "2.2.3.8";
            if (VersionChecks.IsVersion1GreaterThanVersion2(firstVersionWithUTCOffsetCheck, imageSetVersionNumber))
            {
                string utcColumnName = Constant.DatabaseColumn.UtcOffset;
                // FORM:  UPDATE DataTable SET UtcOffset =  REPLACE  ( UtcOffset, ',', '.' )  WHERE  INSTR  ( UtcOffset, ',' )  > 0
                this.Database.ExecuteNonQuery(Sql.Update + Constant.DBTables.FileData + Sql.Set + utcColumnName + Sql.Equal +
                    Sql.Replace + Sql.OpenParenthesis + utcColumnName + Sql.Comma + Sql.Quote(",") + Sql.Comma + Sql.Quote(".") + Sql.CloseParenthesis +
                    Sql.Where + Sql.Instr + Sql.OpenParenthesis + utcColumnName + Sql.Comma + Sql.Quote(",") + Sql.CloseParenthesis + Sql.GreaterThan + "0");
                await Task.Delay(Constant.BusyState.SleepTime);
            }

            // As if Version 2.2.5.0...
            // For non-zero offsets, correct DateTime to local time and set its UtcOffset to 0
            // This essentially will convert all dates to local time, which makes life way easier.
            // string firstVersionWithUTCSetToZero = "2.2.4.4";
            // This operation takes about 450ms to convert 112K rows when all rows have non-zero UtcOffset
            // However, if only a few rows have a non-zero UtcOffset, it is very fast (i.. ~ 37ms for 112K rows)
            // So we do this check every time we open a database file, just in case that db file had been opened by 
            // a version ofTimelapse earlier than 2.2.4.4, which could write the times back in non-zero UTC times.
            // The expression:
            // Upgrade DataTable Set
            //        datetime = DateTime(datetime, UtcOffset || ' hours'),
            //        UtcOffset = '0.0'
            //    WHERE UtcOffset<> '0.0'

            this.Database.ExecuteNonQuery(Sql.Update + Constant.DBTables.FileData + Sql.Set
                + Constant.DatabaseColumn.DateTime + Sql.Equal
                + Sql.Strftime + Sql.OpenParenthesis + Sql.Quote(Constant.Time.DateTimeSQLFormatForWritingTimelapseDB) + Sql.Comma
                + Sql.DateTimeFunction + Sql.OpenParenthesis + Constant.DatabaseColumn.DateTime + Sql.Comma + Constant.DatabaseColumn.UtcOffset + Sql.Concatenate + Sql.HoursQuoted + Sql.CloseParenthesis
                + Sql.CloseParenthesis
                + Sql.Comma
                + Constant.DatabaseColumn.UtcOffset + Sql.Equal + Sql.Quote("0.0")
                + Sql.Where + Constant.DatabaseColumn.UtcOffset + Sql.NotEqual + Sql.Quote("0.0"));
            await Task.Delay(Constant.BusyState.SleepTime);
            // We don't have to reset the TimeZone column in the ImageSet. It is ignored in this version, but still useful if opened in prior versions (I think)
            // So this code is commented out.
            // this.Database.ExecuteNonQuery(
            //    Sql.Upgrade + Constant.DBTables.ImageSet + Sql.Set 
            //    + Constant.DatabaseColumn.TimeZone + Sql.Equal + Sql.Quote(Constant.Time.NeutralTimeZone) 
            //    + Sql.Where + Constant.DatabaseColumn.TimeZone + Sql.NotEqual + Sql.Quote(Constant.Time.NeutralTimeZone));

            // STEP 3. Check both templates and update if needed (including values)

            // Version Compatability Column: If the imageSetVersion is set to the lowest version number, then the column containing the VersionCompatabily does not exist in the image set table. 
            // Add it and update the entry to contain the version of Timelapse currently being used to open this database
            // Note that we do this after the version compatability tests as otherwise we would just get the current version number
            if (versionCompatabilityColumnExists == false)
            {
                // Create the versioncompatability column and update the image set. Syncronization happens later
                this.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.ImageSet, new SchemaColumnDefinition(Constant.DatabaseColumn.VersionCompatabily, Sql.Text, "2.3.0.0"));
                await Task.Delay(Constant.BusyState.SleepTime);
            }
            this.Database.Upgrade(Constant.DBTables.ImageSet, new ColumnTuple(Constant.DatabaseColumn.VersionCompatabily, timelapseVersion));

            // Sort Criteria Column: Make sure that the column containing the SortCriteria exists in the image set table. 
            // If not, add it and set it to the default
            bool sortCriteriaColumnExists = this.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.SortTerms);
            if (!sortCriteriaColumnExists)
            {
                // create the sortCriteria column and update the image set. Syncronization happens later
                this.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.ImageSet, new SchemaColumnDefinition(Constant.DatabaseColumn.SortTerms, Sql.Text, Constant.DatabaseValues.DefaultSortTerms));
                await Task.Delay(Constant.BusyState.SleepTime);
            }

            // SelectedFolder Column: Make sure that the column containing the SelectedFolder exists in the image set table. 
            // If not, add it and set it to the default
            string firstVersionWithSelectedFilesColumns = "2.2.2.6";
            if (VersionChecks.IsVersion1GreaterOrEqualToVersion2(firstVersionWithSelectedFilesColumns, imageSetVersionNumber))
            {
                // Because we may be running this several times on the same version, we should still check to see if the column exists before adding it
                bool selectedFolderColumnExists = this.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.SelectedFolder);
                if (!selectedFolderColumnExists)
                {
                    // create the sortCriteria column and update the image set. Syncronization happens later
                    this.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.ImageSet, new SchemaColumnDefinition(Constant.DatabaseColumn.SelectedFolder, Sql.Text, String.Empty));
                    this.ImageSetLoadFromDatabase();
                    await Task.Delay(Constant.BusyState.SleepTime);
                }
            }
            // Make sure that the column containing the QuickPasteXML exists in the image set table. 
            // If not, add it and set it to the default
            bool quickPasteXMLColumnExists = this.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.QuickPasteXML);
            if (!quickPasteXMLColumnExists)
            {
                // create the QuickPaste column and update the image set. Syncronization happens later
                this.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.ImageSet, new SchemaColumnDefinition(Constant.DatabaseColumn.QuickPasteXML, Sql.Text, Constant.DatabaseValues.DefaultQuickPasteXML));
                await Task.Delay(Constant.BusyState.SleepTime);
            }

            // Timezone column (if missing) needs to be added to the Imageset Table
            bool timeZoneColumnExists = this.Database.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.TimeZone);

            if (!timeZoneColumnExists)
            {
                // create default time zone entry and refresh the image set.
                this.Database.SchemaAddColumnToEndOfTable(Constant.DBTables.ImageSet, new SchemaColumnDefinition(Constant.DatabaseColumn.TimeZone, Sql.Text));
                this.Database.SetColumnToACommonValue(Constant.DBTables.ImageSet, Constant.DatabaseColumn.TimeZone, Constant.Time.NeutralTimeZone);
                this.ImageSetLoadFromDatabase();
                await Task.Delay(Constant.BusyState.SleepTime);
            }

            // HANDLE REALLY OLD DDB FILE FORMATS missing TimeZoneColumns, which also date back to
            // DDBs that only have a Date Time column if no offset
            //// Populate DateTime column if the column has just been 
            if (!timeZoneColumnExists || !dateTimeColumnWasInDDB)
            {
                // PERFORMANCE, BUT RARE: We invoke this to update various date/time values on all rows based on existing values. However, its rarely called
                // PROGRESSBAR - Add to all calls to SelectFiles, perhaps after a .5 second delay
                // we  have to select all rows. However, this operation would only ever happen once, and only on legacy .ddb files
                string query = "Select * from DataTable";
                DataTable datatable = this.Database.GetDataTableFromSelect(query);
                List<ColumnTuplesWithWhere> listOfColumnTuplesWithWhere = new List<ColumnTuplesWithWhere>();
                for (int i = 0; i < datatable.Rows.Count; i++)
                {
                    DataRow row = datatable.Rows[i];
                    string date = (string)row[Constant.DatabaseColumn.Date];
                    string time = (string)row[Constant.DatabaseColumn.Time];
                    long id = (long)row[Constant.DatabaseColumn.ID];
                    bool result = DateTimeHandler.TryParseLegacyDateTime(date, time, DateTimeHandler.GetNeutralTimeZone(), out DateTimeOffset dateTime);
                    if (result)
                    {
                        ColumnTuplesWithWhere columnsToUpgrade = new ColumnTuplesWithWhere();    // holds columns which have changed for the current contro
                        columnsToUpgrade.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.DateTime, dateTime.LocalDateTime));
                        columnsToUpgrade.SetWhere(id);
                        listOfColumnTuplesWithWhere.Add(columnsToUpgrade);
                        //System.Diagnostics.Debug.Print(dateTime.ToString());
                    }
                    //else
                    //{
                    //    System.Diagnostics.Debug.Print("Could not get date/time from " + date + " " + time);
                    //}
                }
                if (listOfColumnTuplesWithWhere.Count > 0)
                {
                    this.Database.Upgrade(Constant.DBTables.FileData, listOfColumnTuplesWithWhere);
                    await Task.Delay(Constant.BusyState.SleepTime);
                }
            }
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
        #endregion

        #region Public Methods - Get Controls, DataLabels, TypedDataLabel
        public List<string> GetDataLabelsExceptIDInSpreadsheetOrder()
        {
            // Utilities.PrintMethodName();
            List<string> dataLabels = new List<string>();
            if (this.Controls == null)
            {
                return dataLabels;
            }
            IEnumerable<ControlRow> controlsInSpreadsheetOrder = this.Controls.OrderBy(control => control.SpreadsheetOrder);
            foreach (ControlRow control in controlsInSpreadsheetOrder)
            {
                string dataLabel = control.DataLabel;
                if (string.IsNullOrEmpty(dataLabel))
                {
                    dataLabel = control.DataLabel;
                }
                Debug.Assert(String.IsNullOrWhiteSpace(dataLabel) == false,
                    $"Encountered empty data label and label at ID {control.ID} in template table.");

                // get a list of datalabels so we can add columns in the order that matches the current template table order
                if (Constant.DatabaseColumn.ID != dataLabel)
                {
                    dataLabels.Add(dataLabel);
                }
            }
            return dataLabels;
        }
        #endregion

        #region Upgrade ImageQuality ValuesTo Dark Values
        public void UpgradeImageQualityValuesToDarkValues()
        {
            Dictionary<string, string> original_replacementPair = new Dictionary<string, string>
            {
                { "Ok", "false" },
                { "Dark", "true" }
            };
            this.Database.UpgradeParticularColumnValuesWithNewValues(Constant.DBTables.FileData, Constant.DatabaseColumn.ImageQuality, original_replacementPair);
        }
        #endregion

        #region Upgrade Detection Conf and bounding box and classification conf From Commas To Decimals  If Needed
        public void UpgradeDetectionConfFromCommasToDecimalsIfNeeded()
        {
            // no detection table to upgrade to upgrade
            if (false == this.Database.TableExistsAndNotEmpty(Constant.DBTables.Detections))
            {
                return;
            }
            // Replace commas as needed in detections.Conf
            // Form: Update Detections Set Conf = CAST(replace(Conf, ',', '.') AS REAL) WHERE Conf LIKE '%,%'
            string query = Sql.Update + Constant.DBTables.Detections + Sql.Set + Constant.DetectionColumns.Conf + Sql.Equal;
            query += Sql.Cast + Sql.OpenParenthesis + Sql.Replace + Sql.OpenParenthesis + Constant.DetectionColumns.Conf + Sql.Comma;
            query += Sql.Quote(",") + Sql.Comma + Sql.Quote(".") + Sql.CloseParenthesis + Sql.As + Sql.Real;
            query += Sql.CloseParenthesis + Sql.Where + Constant.DetectionColumns.Conf + Sql.Like + Sql.Quote("%,%");
            this.Database.ExecuteNonQuery(query);

            // Replace comas in the detection bounding box table as needed
            DataTable datatable = this.Database.GetDataTableFromSelect(Sql.Select + Constant.DetectionColumns.DetectionID + Sql.Comma + Constant.DetectionColumns.BBox + Sql.From + Constant.DBTables.Detections);
            char comma = ',';
            char period = '.';
            List<string> queries = new List<string>();
            foreach (DataRow row in datatable.Rows)
            {
                string bboxString = (string)row[1];
                if (string.IsNullOrEmpty(bboxString))
                {
                    continue;
                }
                int freq = bboxString.Count(f => (f == comma));
                if (freq == 0 || freq == 3)
                {
                    continue;
                }
                string[] coords = bboxString.Split(' ');
                if (coords.Length != 4)
                {
                    Debug.Print("In UpgradeDetectionConfFromCommasToDecimalsIfNeeded - the bbox coordinates are wrong", coords.Length);
                    continue;
                }
                // We must have a bounding box string with commas as decimal separators
                // Reconstruct it with decimal separators and in the expected bounding box format 
                string newBboxAsString = String.Empty;
                long id = (long)row[0];
                for (int i = 0; i < coords.Length; i++)
                {
                    string coord = coords[i].TrimEnd(comma);
                    coord = coord.Replace(comma, period);
                    newBboxAsString += coord;
                    if (i < coords.Length - 1)
                    {
                        newBboxAsString += ", ";
                    }
                }
                query = Sql.Update + Constant.DBTables.Detections + Sql.Set + Constant.DetectionColumns.BBox + Sql.Equal + Sql.Quote(newBboxAsString) + Sql.Where + Constant.DetectionColumns.DetectionID + Sql.Equal + id;
                queries.Add(query);
            }
            if (queries.Count > 0)
            {
                this.Database.ExecuteNonQueryWrappedInBeginEnd(queries);
            }

            // Replace commas as needed in Classifications.Conf
            // Form: Update Classifications Set Conf = CAST(replace(Conf, ',', '.') AS REAL) WHERE Conf LIKE '%,%'
            query = Sql.Update + Constant.DBTables.Classifications + Sql.Set + Constant.DetectionColumns.Conf + Sql.Equal;
            query += Sql.Cast + Sql.OpenParenthesis + Sql.Replace + Sql.OpenParenthesis + Constant.DetectionColumns.Conf + Sql.Comma;
            query += Sql.Quote(",") + Sql.Comma + Sql.Quote(".") + Sql.CloseParenthesis + Sql.As + Sql.Real;
            query += Sql.CloseParenthesis + Sql.Where + Constant.DetectionColumns.Conf + Sql.Like + Sql.Quote("%,%");
            this.Database.ExecuteNonQuery(query);

        }
        #endregion

        #region Markers
        // Get all markers from the Markers table and load it into the data table
        public void MarkersLoadRowsFromDatabase()
        {
            string markersQuery = Sql.SelectStarFrom + Constant.DBTables.Markers;
            this.Markers = new DataTableBackedList<MarkerRow>(this.Database.GetDataTableFromSelect(markersQuery), row => new MarkerRow(row));
        }
        #endregion

        #region Quickpaste retrieval
        public static string TryGetQuickPasteXMLFromDatabase(string filePath)
        {
            // Open the database if it exists
            SQLiteWrapper sqliteWrapper = new SQLiteWrapper(filePath);
            if (sqliteWrapper.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.QuickPasteXML) == false)
            {
                // The column isn't in the table, so give up
                return String.Empty;
            }

            List<object> listOfObjects = sqliteWrapper.GetDistinctValuesInColumn(Constant.DBTables.ImageSet, Constant.DatabaseColumn.QuickPasteXML);
            if (listOfObjects.Count == 1)
            {
                return (string)listOfObjects[0];
            }
            return String.Empty;
        }
        #endregion

        #region Find: By Row Index
        // Check if index is within the file row range
        public bool IsFileRowInRange(int imageRowIndex)
        {
            return (imageRowIndex >= 0) && (imageRowIndex < this.CountAllCurrentlySelectedFiles);
        }
        #endregion

        #region Index creation and dropping
        public void IndexCreateForDetectionsAndClassificationsIfNotExists()
        {
            this.Database.IndexCreateIfNotExists(Constant.DatabaseValues.IndexID, Constant.DBTables.Detections, Constant.DatabaseColumn.ID);
            this.Database.IndexCreateIfNotExists(Constant.DatabaseValues.IndexDetectionID, Constant.DBTables.Classifications, Constant.DetectionColumns.DetectionID);
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
            }

            base.Dispose(disposing);
            this.disposed = true;
        }
        #endregion
    }
}