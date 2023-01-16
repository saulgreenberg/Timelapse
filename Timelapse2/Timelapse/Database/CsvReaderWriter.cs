using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Timelapse.Controls;
using Timelapse.DataStructures;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse.Database
{
    /// <summary>
    /// Import and export .csv files.
    /// </summary>
    internal class CsvReaderWriter
    {
        #region Public Static Method - Export to CSV
        /// <summary>
        /// Export all the database data associated with the selected view to the .csv file indicated in the file path so that spreadsheet applications (like Excel) can display it.
        /// </summary>
        public static async Task<bool> ExportToCsv(FileDatabase database, string filePath, CSVDateTimeOptionsEnum csvDateTimeOptions, bool csvInsertSpaceBeforeDates, bool csvIncludeRootFolderColumn, string rootFolder)
        {
            // Set up a progress handler that will update the progress bar
            Progress<ProgressBarArguments> progressHandler = new Progress<ProgressBarArguments>(value =>
            {
                // Update the progress bar
                CsvReaderWriter.UpdateProgressBar(GlobalReferences.BusyCancelIndicator, value.PercentDone, value.Message, value.IsCancelEnabled, value.IsIndeterminate);
            });
            IProgress<ProgressBarArguments> progress = progressHandler;
            return await Task.Run(() =>
            {
                try
                {
                    progress.Report(new ProgressBarArguments(0, "Writing the CSV file. Please wait", false, true));
                    using (StreamWriter fileWriter = new StreamWriter(filePath, false))
                    {
                        // Write the header as defined by the data labels in the template file (skipping the ones we don't use)
                        // If the data label is an empty string, we use the label instead.
                        // The append sequence results in a trailing comma which is retained when writing the line.
                        StringBuilder header = new StringBuilder();
                        List<string> dataLabels = database.GetDataLabelsExceptIDInSpreadsheetOrder();
                        if (csvIncludeRootFolderColumn)
                        {
                            dataLabels.Insert(0, Constant.DatabaseColumn.RootFolder);
                        }
                        foreach (string dataLabel in dataLabels)
                        {
                            if (dataLabel == Constant.DatabaseColumn.DateTime && csvDateTimeOptions == CSVDateTimeOptionsEnum.DateAndTimeColumns)
                            {
                                header.Append(AddColumnValue(Constant.ControlDeprecated.DateLabel));
                                header.Append(AddColumnValue(Constant.ControlDeprecated.TimeLabel));
                            }
                            else
                            {
                                header.Append(AddColumnValue(dataLabel));
                            }
                        }
                        fileWriter.WriteLine(header.ToString());

                        // For each row in the data table, write out the columns in the same order as the 
                        // data labels in the template file (again, skipping the ones we don't use and special casing the date/time data)
                        int countAllCurrentlySelectedFiles = database.CountAllCurrentlySelectedFiles;
                        for (int row = 0; row < countAllCurrentlySelectedFiles; row++)
                        {
                            StringBuilder csvRow = new StringBuilder();
                            ImageRow image = database.FileTable[row];
                            foreach (string dataLabel in dataLabels)
                            {
                                if (dataLabel == Constant.DatabaseColumn.RootFolder)
                                {
                                    // Export the data as is
                                    csvRow.Append(AddColumnValue(rootFolder));
                                }
                                else if (dataLabel == Constant.DatabaseColumn.DateTime)
                                {
                                    if (csvDateTimeOptions == CSVDateTimeOptionsEnum.DateAndTimeColumns)
                                    {
                                        // Export both the separate Date and Time column data with or without a space as needed
                                        string prefix = csvInsertSpaceBeforeDates ? " " : String.Empty;
                                        // csvRow.Append(prefix + AddColumnValue(image.GetValueDatabaseString(dataLabel)));
                                        csvRow.Append(prefix + AddColumnValue(image.GetValueCSVDateString()));
                                        csvRow.Append(prefix + AddColumnValue(image.GetValueCSVTimeString()));
                                        continue;
                                    }
                                    else
                                    {
                                        // Export the single DateTime column data
                                        string prefix = csvInsertSpaceBeforeDates ? " " : String.Empty;
                                        if (csvDateTimeOptions == CSVDateTimeOptionsEnum.DateTimeColumnWithTSeparator)
                                        {
                                            // with the T separator
                                            csvRow.Append(prefix + AddColumnValue(image.GetValueCSVDateTimeWithTSeparatorString()));
                                        }
                                        else if (csvDateTimeOptions == CSVDateTimeOptionsEnum.DateTimeWithoutTSeparatorColumn)
                                        {
                                            // without the T separator
                                            csvRow.Append(prefix + AddColumnValue(image.GetValueCSVDateTimeWithoutTSeparatorString()));
                                        }
                                    }
                                }
                                else
                                {   // Export the data as is
                                    csvRow.Append(AddColumnValue(image.GetValueDatabaseString(dataLabel)));
                                }
                            }
                            fileWriter.WriteLine(csvRow.ToString());
                            if (row % 5000 == 0)
                            {
                                progress.Report(new ProgressBarArguments(Convert.ToInt32(((double)row) / countAllCurrentlySelectedFiles * 100.0), String.Format("Writing {0}/{1} file entries to CSV file. Please wait...", row, countAllCurrentlySelectedFiles), false, false));
                            }
                        }
                    }
                    return true;
                }
                catch
                {
                    return false;
                }
            }).ConfigureAwait(true);
        }
        #endregion

        #region Public Static Method - Import from CSV (async)
        // Try importing a CSV file, checking its headers and values against the template's DataLabels and data types.
        // Duplicates are handled.
        // Return a list of errors if needed.
        // However, error reporting is limited to only gross mismatches.
        // Note that:
        // - rows in the CSV file that are not in the .ddb file are ignored (not reported - maybe it should be?)
        // - rows in the .ddb file that are not in the CSV file are ignored
        // - if there are more duplicate rows for an image in the .csv file than there are in the .ddb file, those extra duplicates are ignored (not reported - maybe it should be?)
        // - if there are more duplicate rows for an image in the .ddb file than there are in the .csv file, those extra duplicates are ignored (not reported - maybe it should be?)
        public static async Task<Tuple<bool, List<string>>> TryImportFromCsv(string filePath, FileDatabase fileDatabase)
        {
            // Set up a progress handler that will update the progress bar
            Progress<ProgressBarArguments> progressHandler = new Progress<ProgressBarArguments>(value =>
            {
                // Update the progress bar
                CsvReaderWriter.UpdateProgressBar(GlobalReferences.BusyCancelIndicator, value.PercentDone, value.Message, value.IsCancelEnabled, value.IsIndeterminate);
            });
            IProgress<ProgressBarArguments> progress = progressHandler;

            List<string> importErrors = new List<string>();
            return await Task.Run(() =>
                {
                    const int bulkFilesToHandle = 2000;
                    int processedFilesCount = 0;
                    int totalFilesProcessed = 0;
                    int dateTimeErrors = 0;
                    progress.Report(new ProgressBarArguments(0, "Reading the CSV file. Please wait", false, true));

                    // PART 1. Read in the CSV file. Return false if there is a problem in reading the CSV file or if the CSV file is empty
                    if (false == TryReadingCSVFile(filePath, out List<List<string>> parsedFile, importErrors))
                    {
                        return new Tuple<bool, List<string>>(false, importErrors);
                    }

                    // Now that we have a parsed file, get its headers, which we will use as DataLabels
                    List<string> dataLabelsFromCSV = parsedFile[0].Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();

                    // Part 2. Abort if required CSV column are missing or there is a problem matching the CSV file headers against the DB headers.
                    if (false == VerifyCSVHeaders(fileDatabase, dataLabelsFromCSV, importErrors))
                    {
                        return new Tuple<bool, List<string>>(false, importErrors);
                    }

                    // Part 3: Create a List of all data rows, where each row is a dictionary containing the header and that row's valued for the header
                    List<Dictionary<string, string>> rowDictionaryList = GetAllDataRows(dataLabelsFromCSV, parsedFile);

                    // Part 4. For every row, validate each column's data against its type. Abort if the type does not match
                    if (false == VerifyDataInColumns(fileDatabase, dataLabelsFromCSV, rowDictionaryList, importErrors))
                    {
                        return new Tuple<bool, List<string>>(false, importErrors);
                    }

                    //
                    // Part 4. Check and manage duplicates
                    // 
                    // Get a list of duplicates in the database, i.e. rows with both the Same relativePath and File
                    List<string> databaseDuplicates = fileDatabase.GetDistinctRelativePathFileCombinationsDuplicates();

                    // Sort the rowDictionaryList so that duplicates in the CSV file (with the same relative path / File name) are in order, one after the other.
                    List<Dictionary<string, string>> sortedRowDictionaryList = rowDictionaryList.OrderBy(dict => dict["RelativePath"]).ThenBy(dict => dict["File"]).ToList();
                    int sortedRowDictionaryListCount = sortedRowDictionaryList.Count;
                    // Create the data structure for the query

                    List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();

                    // Handle duplicates and more
                    int nextRowIndex = 0;
                    string currentPath = String.Empty;   // the path of the current row 
                    string examinedPath = String.Empty;  // the path of a surrounding row currently being examined to see if its a duplicate
                    string duplicatePath = String.Empty; // a duplicate was identified, and this holds the duplicate path
                    List<Dictionary<string, string>> duplicatesDictionaryList = new List<Dictionary<string, string>>();

                    foreach (Dictionary<string, string> rowDict in sortedRowDictionaryList)
                    {
                        // For every row...
                        nextRowIndex++;
                        currentPath = Path.Combine(rowDict[Constant.DatabaseColumn.RelativePath], rowDict[Constant.DatabaseColumn.File]);

                        #region Handle duplicates
                        // Duplicates are special cases, where we have to update each set of duplicates separately as a chunk.
                        // To begin, check if its a duplicate, which occurs if the path (RelativePath/File) is identical

                        if (currentPath == duplicatePath)
                        {
                            // we are in the middle of a sequence, and this record has the same path as the previously identified duplicate.
                            // Thus the current record has to be a duplicate.
                            // Add it to the list.
                            duplicatesDictionaryList.Add(rowDict);

                            // A check if we are at the end of the CSV file - this catches the condition where the very last entry in the sorted csv file is a duplicate
                            if (nextRowIndex >= sortedRowDictionaryListCount)
                            {
                                string error = UpdateDuplicatesInDatabase(fileDatabase, duplicatesDictionaryList, Path.GetDirectoryName(duplicatePath), Path.GetFileName(duplicatePath));
                                if (false == string.IsNullOrEmpty(error))
                                {
                                    importErrors.Add(error);
                                }
                                duplicatesDictionaryList.Clear();
                            }
                            continue;
                        }
                        else
                        {
                            // Check if we are at the end of a duplicate sequence
                            if (duplicatesDictionaryList.Count > 0)
                            {
                                // This entry marks the end of a sequence as the paths aren't equal but we have duplicates. Process the prior sequence
                                string error = UpdateDuplicatesInDatabase(fileDatabase, duplicatesDictionaryList, Path.GetDirectoryName(duplicatePath), Path.GetFileName(duplicatePath));
                                if (false == string.IsNullOrEmpty(error))
                                {
                                    importErrors.Add(error);
                                }
                                duplicatesDictionaryList.Clear();
                            }

                            // We are either not in a sequence, or we completed the sequence. So we need to manage the current entry.
                            if (nextRowIndex < sortedRowDictionaryListCount)
                            {
                                // We aren't currently in a sequence. Determine if the current entry is a singleton or the first duplicate in a sequence by checking its path against the next record.
                                // If it is a duplicate, add it to the list.
                                Dictionary<string, string> nextRow = sortedRowDictionaryList[nextRowIndex];
                                examinedPath = Path.Combine(nextRow[Constant.DatabaseColumn.RelativePath], nextRow[Constant.DatabaseColumn.File]);
                                if (examinedPath == currentPath)
                                {
                                    // Yup, its the beginning of a sequence.
                                    duplicatePath = currentPath;
                                    duplicatesDictionaryList.Clear();
                                    duplicatesDictionaryList.Add(rowDict);
                                    continue;
                                }
                                else
                                {
                                    // It must be singleton
                                    duplicatePath = String.Empty;
                                    if (databaseDuplicates.Contains(currentPath))
                                    {
                                        // But, if the database contains a duplicate with the same relativePath/File, then we want to update just the first database duplicate, rather than update all those
                                        // database duplicates with the same value (if we let it fall thorugh)
                                        duplicatesDictionaryList.Add(rowDict);
                                        string error = UpdateDuplicatesInDatabase(fileDatabase, duplicatesDictionaryList, Path.GetDirectoryName(currentPath), Path.GetFileName(currentPath));
                                        if (false == string.IsNullOrEmpty(error))
                                        {
                                            importErrors.Add(error);
                                        }
                                        duplicatesDictionaryList.Clear();
                                        continue;
                                    }
                                }
                            }
                        }
                        #endregion Handle duplicates

                        #region Process each column in a row by its header type
                        // Process each non-duplicate row
                        // Note that we never update Path-related fields (File, RelativePath)
                        ColumnTuplesWithWhere imageToUpdate = new ColumnTuplesWithWhere();
                        CultureInfo provider = CultureInfo.InvariantCulture;
                        DateTime datePortion = DateTime.MinValue;
                        DateTime timePortion = DateTime.MinValue;
                        DateTime dateTime = DateTime.MinValue;
                        foreach (string header in rowDict.Keys)
                        {
                            string type = String.Empty;
                            ControlRow controlRow = null;

                            // For every column ...
                            if (header == Constant.ControlDeprecated.DateLabel || header == Constant.ControlDeprecated.TimeLabel || header == Constant.ControlDeprecated.Folder || header == Constant.ControlDeprecated.ImageQuality || header == Constant.DatabaseColumn.RootFolder)
                            {
                                // We have to treat the deprecated headers (Date, Time, Folder, ImageQuality) differently as they won't be in the template.
                                // Similarly, RootFolder is a special case.It is not in the template, but generated on CSV export on the fly.
                                type = header;
                            }
                            else
                            {
                                controlRow = fileDatabase.GetControlFromTemplateTable(header);
                                type = controlRow.Type;
                            }

                            // process each column but only if its of the specific type
                            if (IsStandardColumn(type))
                            {
                                imageToUpdate.Columns.Add(new ColumnTuple(header, rowDict[header]));
                            }
                            else
                            {
                                // Its not a standard control, so check if its a date/time/DateTime control and handle that as these are special cases
                                // Day: dd-MMM-yyyy 03-Jul-2017
                                // Time: HH:mm:ss 12:30:57
                                // DateTime:
                                // - yyyy-MM-ddTHH:mm:ss (includes T separator): 
                                // - yyyy-MM-dd HH:mm:ss (excludes T separator) 
                                if (type == Constant.DatabaseColumn.DateTime)
                                {
                                    string strDateTime = rowDict[header];
                                    if (DateTime.TryParseExact(strDateTime, Constant.Time.DateTimeCSVWithoutTSeparator, provider, DateTimeStyles.None, out dateTime))
                                    {
                                        // Standard DateTime
                                        // System.Diagnostics.Debug.Print("Standard: " + dateTime.ToString());
                                    }
                                    else if (DateTime.TryParseExact(strDateTime, Constant.Time.DateTimeCSVWithTSeparator, provider, DateTimeStyles.None, out dateTime))
                                    {
                                        // Standard DateTime wit T separator
                                        // System.Diagnostics.Debug.Print("StandardT: " + dateTime.ToString());
                                    }
                                }
                                else if (type == Constant.ControlDeprecated.DateLabel)
                                {
                                    // Date only
                                    string strDateTime = rowDict[header];
                                    if (DateTime.TryParseExact(strDateTime, Constant.Time.DateFormat, provider, DateTimeStyles.None, out DateTime tempDateTime))
                                    {
                                        datePortion = tempDateTime;
                                    }
                                }
                                else if (type == Constant.ControlDeprecated.TimeLabel)
                                {
                                    // Time only
                                    string strDateTime = rowDict[header];
                                    if (DateTime.TryParseExact(strDateTime, Constant.Time.TimeFormat, provider, DateTimeStyles.None, out DateTime tempDateTime))
                                    {
                                        //System.Diagnostics.Debug.Print("Time only: " + tempDateTime.ToString());
                                        timePortion = tempDateTime;
                                    }
                                }
                                else if (type == Constant.DatabaseColumn.RootFolder || type == Constant.ControlDeprecated.Folder || type == Constant.ControlDeprecated.ImageQuality)
                                {
                                    // Skip the Folder, RootFolder and ImageQuality columns,
                                    // as Folder / RootFolder data should not be updated, and ImageQuality is deprecated and thus ignored.
                                }
                            }
                        }
                        #endregion Process each column by its header type

                        // We've now looked at all the columns in a row, so continue processing that row as needed
                        totalFilesProcessed++;

                        // If Date and Time columns were used instead of DateTime, they have to be combined to get the DateTime
                        if (dateTime != DateTime.MinValue || (datePortion != DateTime.MinValue && timePortion != DateTime.MinValue))
                        {
                            // Update dateTime from the separate date and time fields were used, update dateTime from them
                            if (datePortion != DateTime.MinValue && timePortion != DateTime.MinValue)
                            {
                                // We have a valid separate date and time. Combine it.
                                dateTime = datePortion.Date + timePortion.TimeOfDay;
                            }
                            // We should now have a valid dateTime. Add it to the database. 
                            imageToUpdate.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.DateTime, dateTime));
                            // System.Diagnostics.Debug.Print("Wrote DateTime: " + dateTime.ToString());
                        }
                        else
                        {
                            dateTimeErrors++;
                            // importErrors.Add(String.Format("{0}: Could not extract datetime", currentPath));
                            // System.Diagnostics.Debug.Print("Could not extract datetime");
                        }
                        dateTime = DateTime.MinValue;
                        datePortion = DateTime.MinValue;
                        timePortion = DateTime.MinValue;

                        // NOTE: We currently do NOT report an error if there is a row in the csv file whose location does not match
                        // the location in the database. We could do this by performing a check before submitting a query, eg. something like:
                        //  Select Count (*) from DataTable where File='IMG_00197.JPG' or File='IMG_01406.JPG' or File='XX.JPG'
                        // where we would then compare the counts against the rows. However, this likely has a performance hit, and it doesn't 
                        // return the erroneous rows... So its not done yet.

                        // Add to the query only if there are columns to add!
                        if (imageToUpdate.Columns.Count > 0)
                        {
                            if (rowDict.ContainsKey(Constant.DatabaseColumn.RelativePath) && !String.IsNullOrWhiteSpace(rowDict[Constant.DatabaseColumn.RelativePath]))
                            {
                                imageToUpdate.SetWhere(rowDict[Constant.DatabaseColumn.RelativePath], rowDict[Constant.DatabaseColumn.File]);
                            }
                            else
                            {
                                imageToUpdate.SetWhere(rowDict[Constant.DatabaseColumn.File]);
                            }
                            imagesToUpdate.Add(imageToUpdate);
                        }

                        // Write current batch of updates to database. Note that we Update the database every number of rows as specified in bulkFilesToHandle.
                        // We should probably put in a cancellation CancelToken somewhere around here...
                        if (imagesToUpdate.Count >= bulkFilesToHandle)
                        {
                            processedFilesCount += bulkFilesToHandle;
                            progress.Report(new ProgressBarArguments(Convert.ToInt32(((double)processedFilesCount) / sortedRowDictionaryListCount * 100.0), String.Format("Processing {0}/{1} files. Please wait...", processedFilesCount, sortedRowDictionaryListCount), false, false));
                            fileDatabase.UpdateFiles(imagesToUpdate);
                            imagesToUpdate.Clear();
                        }
                    }
                    // perform any remaining updates
                    if (dateTimeErrors != 0)
                    {
                        // Need to check IF THIS WORKS FOR files with no date-time fields!
                        importErrors.Add(String.Format("The Date/Time was not be updated for {0} / {1} files. ", dateTimeErrors, totalFilesProcessed));
                        if (dataLabelsFromCSV.Contains(Constant.DatabaseColumn.DateTime) || (dataLabelsFromCSV.Contains(Constant.ControlDeprecated.DateLabel) && dataLabelsFromCSV.Contains(Constant.ControlDeprecated.TimeLabel)))
                        {
                            importErrors.Add("- some date / time values in the DateTime, Date or Time columns are in an unexpected format (see manual)");
                        }
                        else
                        {
                            importErrors.Add("- the CSV file is missing either a DateTime column or both Date and Time columns (this is ok if it was intended)");
                        }
                    }
                    fileDatabase.UpdateFiles(imagesToUpdate);
                    return new Tuple<bool, List<string>>(true, importErrors);
                }).ConfigureAwait(true);
        }

        #region Helpers for TryImportFromCsv. These just reduce the size of the method to make it easier to debug.
        // Read in the CSV file. Return false if there is a problem in reading the CSV file or if the CSV file is empty
        private static bool TryReadingCSVFile(string filePath, out List<List<string>> parsedFile, List<string> importErrors)
        {
            parsedFile = ReadAndParseCSVFile(filePath);

            // Abort if the CSV file could not be read 
            if (parsedFile == null)
            {
                // Could not open the file
                importErrors.Add(String.Format("The file '{0}' could not be read. Things to check:", Path.GetFileName(filePath)));
                importErrors.Add("- Is the file is currently opened by another application?");
                importErrors.Add("- Do you have permission to read this file (especially network file systems, which sometimes limit access).");
                return false;
            }

            // Abort if The CSV file is empty or only contains a header row
            if (parsedFile.Count < 1)
            {
                importErrors.Add(String.Format("The file '{0}' appears to be empty.", Path.GetFileName(filePath)));
                return false;
            }
            else if (parsedFile.Count < 2)
            {
                importErrors.Add(String.Format("The file '{0}' does not contain any data.", Path.GetFileName(filePath)));
                return false;
            }
            return true;
        }

        // Return false if required CSV column are missing or there is a problem matching the CSV file headers against the DB headers.
        private static bool VerifyCSVHeaders(FileDatabase fileDatabase, List<string> dataLabelsFromCSV, List<string> importErrors)
        {
            bool abort = false;
            // Get the dataLabels from the database and from the headers in the CSV files (and remove any empty trailing headers from the CSV file list)
            List<string> dataLabelsFromDB = fileDatabase.GetDataLabelsExceptIDInSpreadsheetOrder();
            // Because Date and Time (which are not controls) may appear instead of DateTime, we add them explicitly so they can pass this test
            // Similarly, we add (and we will skip over)
            // - Folder and ImageQuality (both deprecated columns that could exist in CSV files pre v2.3)
            // - RootFolder (which isn't in the Template)
            dataLabelsFromDB.Add(Constant.DatabaseColumn.RootFolder);
            dataLabelsFromDB.Add(Constant.ControlDeprecated.DateLabel);
            dataLabelsFromDB.Add(Constant.ControlDeprecated.TimeLabel);
            dataLabelsFromDB.Add(Constant.ControlDeprecated.Folder);
            dataLabelsFromDB.Add(Constant.ControlDeprecated.ImageQuality);

            List<string> dataLabelsInHeaderButNotFileDatabase = dataLabelsFromCSV.Except(dataLabelsFromDB).ToList();

            // Abort if the File and Relative Path columns are missing from the CSV file 
            // While the CSV data labels can be a subset of the DB data labels,
            // the File and Relative Path are a required CSV datalabel, as we can't match the DB data row without it.
            if (dataLabelsFromCSV.Contains(Constant.DatabaseColumn.File) == false || dataLabelsFromCSV.Contains(Constant.DatabaseColumn.RelativePath) == false)
            {
                importErrors.Add("CSV columns necessary to locate your image or video files are missing: ");
                if (dataLabelsFromCSV.Contains(Constant.DatabaseColumn.File) == false)
                {
                    importErrors.Add(String.Format("- the '{0}' column.", Constant.DatabaseColumn.File));
                }
                if (dataLabelsFromCSV.Contains(Constant.DatabaseColumn.RelativePath) == false)
                {
                    importErrors.Add(String.Format("- the '{0}' column (You still need it even if your files are all in your root folder).", Constant.DatabaseColumn.RelativePath));
                }
                abort = true;
            }

            // Abort if a column header in the CSV file does not exist in the template
            // NOTE: could do this as a warning rather than as an abort, but...
            if (dataLabelsInHeaderButNotFileDatabase.Count != 0)
            {
                importErrors.Add("These CSV column headings do not match any of the template'sDataLabels:");
                foreach (string dataLabel in dataLabelsInHeaderButNotFileDatabase)
                {
                    importErrors.Add(String.Format("- {0}", dataLabel));
                    abort = true;
                }
            }

            if (abort)
            {
                // We failed. abort.
                return false;
            }
            return true;
        }

        // Get all the data rows from the CSV file. Each dictionary entry is a row with a list of matching  CSV column Headers and column value 
        private static List<Dictionary<string, string>> GetAllDataRows(List<string> dataLabelsFromCSV, List<List<string>> parsedFile)
        {
            List<Dictionary<string, string>> rowDictionaryList = new List<Dictionary<string, string>>();

            // Part 3. Get all data rows, and validate each column's data against its type. Abort if the type does not match
            int rowNumber = 0;
            int numberOfHeaders = dataLabelsFromCSV.Count;
            foreach (List<string> parsedRow in parsedFile)
            {
                // For each data row
                rowNumber++;
                if (rowNumber == 1)
                {
                    // Skip the 1st header row
                    continue;
                }

                // for this row, create a dictionary of matching the CSV column Header and that column's value 
                Dictionary<string, string> rowDictionary = new Dictionary<string, string>();
                for (int i = 0; i < numberOfHeaders; i++)
                {
                    //string valueToAdd = (i < parsedRow.Count) ? parsedRow[i] : String.Empty;
                    rowDictionary.Add(dataLabelsFromCSV[i], parsedRow[i]);
                }
                rowDictionaryList.Add(rowDictionary);
            }
            return rowDictionaryList;
        }

        // Validate Data columns against data type. Return false if any of the types don't match
        private static bool VerifyDataInColumns(FileDatabase fileDatabase, List<string> dataLabelsFromCSV, List<Dictionary<string, string>> rowDictionaryList, List<string> importErrors)
        {
            bool abort = false;
            // For each column in the CSV file,
            // - get its type from the template
            // - for particular types, validate the data in the column against that type
            // Validation ignored for:
            // - Note, as it can hold any data
            // - File, RelativePath, as that data row would be ignored if it does not create a valid path
            // - Date/Time/DateTime as they are handled elsewhere 
            //   - Date, Time formats must match exactl
            int rowNumber = 0;
            bool errorInRow;
            int numberRowsWithErrors = 0;
            int maxRowsToReportWithErrors = 2;
            // For every row
            foreach (Dictionary<string, string> rowDict in rowDictionaryList)
            {
                rowNumber++;
                errorInRow = false;
                // Check for Date and Time columns, and deal with them separately
                // Get the header type

                // For every column
                foreach (string csvHeader in dataLabelsFromCSV)
                {
                    if (csvHeader == Constant.ControlDeprecated.DateLabel
                        || csvHeader == Constant.ControlDeprecated.TimeLabel
                        || csvHeader == Constant.DatabaseColumn.DateTime
                        || csvHeader == Constant.ControlDeprecated.Folder
                        || csvHeader == Constant.DatabaseColumn.RootFolder
                        || csvHeader == Constant.ControlDeprecated.ImageQuality)
                    {
                        // Date/Time/DateTime checking is handled elsewhere, while Folder, RootFolder and ImageQuality are skipped
                        continue;
                    }
                    ControlRow controlRow = fileDatabase.GetControlFromTemplateTable(csvHeader);
                    string controlRowType = controlRow.Type;

                    if (IsStandardColumn(controlRowType))
                    {
                        // Validate the data as needed for each of these columns in the row
                        switch (controlRowType)
                        {
                            case Constant.Control.Flag:
                            case Constant.DatabaseColumn.DeleteFlag:
                                if (!Boolean.TryParse(rowDict[csvHeader], out _))
                                {
                                    // Flag values must be true or false, but its not. So raise an error
                                    importErrors.Add(String.Format("- error in row {1} as {0} values must be true or false, but is '{2}'", csvHeader, rowNumber, rowDict[csvHeader]));
                                    abort = true;
                                }
                                break;
                            case Constant.Control.Counter:
                                if (!String.IsNullOrWhiteSpace(rowDict[csvHeader]) && !Int32.TryParse(rowDict[csvHeader], out _))
                                {
                                    // Counters must be integers / blanks 
                                    importErrors.Add(String.Format("- error in row {1} as {0} values must be blank or a number, but is '{2}'", csvHeader, rowNumber, rowDict[csvHeader]));
                                    abort = true;
                                }
                                break;
                            case Constant.Control.FixedChoice:
                                // We allow empty values, even though it may not be a list option
                                if (false == String.IsNullOrWhiteSpace(rowDict[csvHeader]) && Choices.ChoicesFromJson(controlRow.List).Contains(rowDict[csvHeader]) == false)
                                {
                                    // Fixed Choices must be in the Choice List
                                    importErrors.Add(String.Format("- error in row {1} as {0} values must be in the template's choice list, but '{2}' isn't in it.", csvHeader, rowNumber, rowDict[csvHeader]));
                                    abort = true;
                                }
                                break;
                            case Constant.Control.Note:
                            default:
                                // as these can be any string, they don't require checking
                                break;
                        }
                        if (!errorInRow && abort)
                        {
                            // If there is an error, only count one error per row.
                            numberRowsWithErrors++;
                            errorInRow = true;
                        }
                        if (numberRowsWithErrors > maxRowsToReportWithErrors)
                        {
                            importErrors.Add(String.Format("- Timelapse only reports data errors for a maximum of {0} rows. Use the information above to start fixing them.", maxRowsToReportWithErrors));
                            importErrors.Add("- Use the information above to check the data values in those columns for all rows.");
                            return false;
                        }
                    }
                }
            }
            return !abort;
        }

        private static bool IsStandardColumn(string controlRowType)
        {
            return controlRowType == Constant.DatabaseColumn.DeleteFlag ||
                    controlRowType == Constant.Control.Note ||
                    controlRowType == Constant.Control.Flag ||
                    controlRowType == Constant.Control.Counter ||
                    controlRowType == Constant.Control.FixedChoice;
        }
        #endregion
        // Given a list of duplicates and their common relative path, update the corresponding duplicates in the database
        // We do this by getting the IDs of duplicates in the database, where we update each database by ID to a duplicate.
        // If there is a mismatch in the number of duplicates in the database vs. in the CSV file, we just update whatever does match.
        private static string UpdateDuplicatesInDatabase(FileDatabase fileDatabase, List<Dictionary<string, string>> duplicatesDictionaryList, string relativePath, string file)
        {
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            string errorMessage = String.Empty;

            // Find THE IDs of ImageRows with those RelativePath / File values

            List<long> duplicateIDS = fileDatabase.SelectFilesByRelativePathAndFileName(relativePath, file);

            if (duplicateIDS.Count != duplicatesDictionaryList.Count)
            {
                string dbEntry = duplicateIDS.Count == 1 ? "entry" : "entries";
                string csvEntry = duplicatesDictionaryList.Count == 1 ? "entry" : "entries";
                errorMessage = String.Format("duplicate entry mismatch for {0}: {1} database {2} vs. {3} CSV {4}.", Path.Combine(relativePath, file), duplicateIDS.Count, dbEntry, duplicatesDictionaryList.Count, csvEntry);
            }

            int idIndex = 0;
            foreach (Dictionary<string, string> rowDict in duplicatesDictionaryList)
            {
                if (idIndex >= duplicateIDS.Count)
                {
                    break;
                }

                //NEW
                CultureInfo provider = CultureInfo.InvariantCulture;
                DateTime datePortion = DateTime.MinValue;
                DateTime timePortion = DateTime.MinValue;
                DateTime dateTime = DateTime.MinValue;
                //ENDNEW

                // Process each row
                ColumnTuplesWithWhere imageToUpdate = new ColumnTuplesWithWhere();
                foreach (string header in rowDict.Keys)
                {
                    if (header == Constant.DatabaseColumn.RootFolder || header == Constant.ControlDeprecated.Folder || header == Constant.ControlDeprecated.ImageQuality)
                    {
                        // Skip the Folder, RootFolder and ImageQuality columns,
                        // Folder / RootFolderdata should not be updated, and ImageQuality is deprecated and thus ignored.
                        continue;
                    }
                    if (header == Constant.ControlDeprecated.DateLabel
                     || header == Constant.ControlDeprecated.TimeLabel
                     || header == Constant.DatabaseColumn.DateTime)
                    {
                        //NEW
                        // check if its a date/ time / DateTime control and handle that as these are special cases
                        // Day: dd-MMM-yyyy 03-Jul-2017
                        // Time: HH:mm:ss 12:30:57
                        // DateTime:
                        // - yyyy-MM-ddTHH:mm:ss (includes T separator): 
                        // - yyyy-MM-dd HH:mm:ss (excludes T separator) 
                        if (header == Constant.DatabaseColumn.DateTime)
                        {
                            string strDateTime = rowDict[header];
                            if (DateTime.TryParseExact(strDateTime, Constant.Time.DateTimeCSVWithoutTSeparator, provider, DateTimeStyles.None, out dateTime))
                            {
                                // Standard DateTime
                                // System.Diagnostics.Debug.Print("Standard: " + dateTime.ToString());
                            }
                            else if (DateTime.TryParseExact(strDateTime, Constant.Time.DateTimeCSVWithTSeparator, provider, DateTimeStyles.None, out dateTime))
                            {
                                // Standard DateTime wit T separator
                                // System.Diagnostics.Debug.Print("StandardT: " + dateTime.ToString());
                            }
                        }
                        else if (header == Constant.ControlDeprecated.DateLabel)
                        {
                            // Date only
                            string strDateTime = rowDict[header];
                            if (DateTime.TryParseExact(strDateTime, Constant.Time.DateFormat, provider, DateTimeStyles.None, out DateTime tempDateTime))
                            {
                                datePortion = tempDateTime;
                            }
                        }
                        else if (header == Constant.ControlDeprecated.TimeLabel)
                        {
                            // Time only
                            string strDateTime = rowDict[header];
                            if (DateTime.TryParseExact(strDateTime, Constant.Time.TimeFormat, provider, DateTimeStyles.None, out DateTime tempDateTime))
                            {
                                //System.Diagnostics.Debug.Print("Time only: " + tempDateTime.ToString());
                                timePortion = tempDateTime;
                            }
                        }
                        //End NEW
                    }

                    ControlRow controlRow = fileDatabase.GetControlFromTemplateTable(header);
                    // process each column but only if its of the specific type
                    if (controlRow != null &&
                        (controlRow.Type == Constant.Control.Flag ||
                         controlRow.Type != Constant.DatabaseColumn.DeleteFlag ||
                         controlRow.Type == Constant.Control.Counter ||
                         controlRow.Type == Constant.Control.FixedChoice
                        ))
                    {
                        imageToUpdate.Columns.Add(new ColumnTuple(header, rowDict[header]));
                    }
                }

                // NEW
                // If Date and Time columns were used instead of DateTime, they have to be combined to get the DateTime
                if (dateTime != DateTime.MinValue || (datePortion != DateTime.MinValue && timePortion != DateTime.MinValue))
                {
                    // Update dateTime from the separate date and time fields were used, update dateTime from them
                    if (datePortion != DateTime.MinValue && timePortion != DateTime.MinValue)
                    {
                        // We have a valid separate date and time. Combine it.
                        dateTime = datePortion.Date + timePortion.TimeOfDay;
                    }
                    // We should now have a valid dateTime. Add it to the database. 
                    imageToUpdate.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.DateTime, dateTime));
                    // System.Diagnostics.Debug.Print("Wrote DateTime: " + dateTime.ToString());
                }

                // Add to the query only if there are columns to add!
                if (imageToUpdate.Columns.Count > 0)
                {
                    imageToUpdate.SetWhere(duplicateIDS[idIndex]);
                    imagesToUpdate.Add(imageToUpdate);
                }
                dateTime = DateTime.MinValue;
                datePortion = DateTime.MinValue;
                timePortion = DateTime.MinValue;
                //ENDNEW
                idIndex++;
            }
            if (imagesToUpdate.Count > 0)
            {
                fileDatabase.UpdateFiles(imagesToUpdate);
            }
            return errorMessage;
        }
        #endregion

        #region Private Method - Update Progress Bar
        private static void UpdateProgressBar(BusyCancelIndicator busyCancelIndicator, int percent, string message, bool isCancelEnabled, bool isIndeterminate)
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
                busyCancelIndicator.CancelButtonText = isCancelEnabled ? "Cancel" : "Processing CSV file...";
            });
        }
        #endregion

        #region Private Methods - Used by above
        // Given a string representing a comma-separated row of values, add a value to it.
        // If special characters are in the string,  escape the string as needed
        private static string AddColumnValue(string value)
        {
            if (value == null)
            {
                return ",";
            }
            if (value.IndexOfAny("\",\x0A\x0D".ToCharArray()) > -1)
            {
                // commas, double quotation marks, line feeds (\x0A), and carriage returns (\x0D) require leading and ending double quotation marks be added
                // double quotation marks within the field also have to be escaped as double quotes
                return "\"" + value.Replace("\"", "\"\"") + "\"" + ",";
            }

            return value + ",";
        }

        // Parse the rows in a CSV file and return it as a list   of lines, each line being a list of values
        private static List<List<string>> ReadAndParseCSVFile(string path)
        {
            try
            {
                List<List<string>> parsedRows = new List<List<string>>();
                using (TextFieldParser parser = new TextFieldParser(path))
                {
                    parser.Delimiters = new string[] { "," };
                    while (true)
                    {
                        string[] parts = parser.ReadFields();
                        if (parts == null)
                        {
                            break;
                        }
                        List<string> rowFields = parts.ToList<string>();
                        parsedRows.Add(rowFields);
                    }
                }
                return parsedRows;
            }
            catch
            {
                return null;
            }
        }
        #endregion
    }
}
