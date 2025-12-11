using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.ControlsDataEntry;
using Timelapse.ControlsMetadata;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Util;

namespace Timelapse.Database
{
    /// <summary>
    /// Import and export .csv files.
    /// </summary>
    // TODO: Region format issue e.g. English(Finland). The csv will still have decimals with a dot, thus Excel will not open the csv correctly, as it expects a comma as the decimal separator. 
    internal class CsvReaderWriter
    {
        #region Public Static Method - Export to CSV
        // Export all the database data associated with the selected view to the .csv file indicated in the file path so that spreadsheet applications (like Excel) can display it.
        public static async Task<bool> ExportToCsv(FileDatabase database, DataEntryControls controls, string filePath, CSVDateTimeOptionsEnum csvDateTimeOptions,
            bool csvInsertSpaceBeforeDates, bool csvIncludeRootFolderColumn, string rootFolder)
        {
            // Set up a progress handler that will update the progress bar
            Progress<ProgressBarArguments> progressHandler = new(value =>
            {
                // Update the progress bar
                UpdateProgressBar(GlobalReferences.BusyCancelIndicator, value.PercentDone, value.Message, value.IsCancelEnabled, value.IsIndeterminate);
            });
            IProgress<ProgressBarArguments> progress = progressHandler;
            return await Task.Run(() =>
            {
                try
                {
                    progress.Report(new(0, "Writing the CSV file. Please wait", false, true));
                    using StreamWriter fileWriter = new(filePath, false);
                    // Get all data labels except those excluded from export (via a false ExportToCSV field)
                    List<string> dataLabelsToExport =
                        database.GetDataLabelsExceptIDInSpreadsheetOrderFromControls().Except(database.GetDataLabelsToExcludeFromExport()).ToList();

                    // Write the header as defined by the data labels in the template file (skipping the ones we don't use)
                    // If the data label is an empty string, we use the label instead.
                    // The append sequence results in a leading comma except for the first column.
                    StringBuilder header = new();
                    bool includeComma = false;

                    // Add each level's name as a column at the beginning of the table
                    foreach (MetadataInfoRow infoRow in database.MetadataInfo)
                    {
                        string alias = MetadataUI.CreateTemporaryAliasIfNeeded(infoRow.Level, infoRow.Alias);
                        header.Append(CSVHelpers.CSVToCommaSeparatedValue(alias, includeComma));
                        includeComma = true;
                    }

                    if (csvIncludeRootFolderColumn)
                    {
                        dataLabelsToExport.Insert(0, DatabaseColumn.RootFolder);
                    }

                    foreach (string dataLabel in dataLabelsToExport)
                    {
                        if (dataLabel == DatabaseColumn.DateTime && csvDateTimeOptions == CSVDateTimeOptionsEnum.DateAndTimeColumns)
                        {
                            header.Append(CSVHelpers.CSVToCommaSeparatedValue(ControlDeprecated.DateLabel, includeComma));
                            header.Append(CSVHelpers.CSVToCommaSeparatedValue(ControlDeprecated.TimeLabel, true));
                        }
                        else
                        {
                            header.Append(CSVHelpers.CSVToCommaSeparatedValue(dataLabel, includeComma));
                        }
                        includeComma = true;
                    }

                    fileWriter.WriteLine(header.ToString());

                    // For each row in the data table, write out the columns in the same order as the 
                    // data labels in the template file (again, skipping the ones we don't use and special casing the date/time data)
                    int countAllCurrentlySelectedFiles = database.CountAllCurrentlySelectedFiles;

                    for (int row = 0; row < countAllCurrentlySelectedFiles; row++)
                    {
                        includeComma = false;
                        StringBuilder csvRow = new();
                        ImageRow image = database.FileTable[row];
                        if (true)
                        {
                            foreach (MetadataInfoRow infoRow in database.MetadataInfo)
                            {
                                int level = infoRow.Level;
                                List<string> cascadingRelativePaths = FilesFolders.SplitAsCascadingRelativePath(image.RelativePath);
                                if (level == 1)
                                {
                                    csvRow.Append(CSVHelpers.CSVToCommaSeparatedValue(string.Empty, includeComma));
                                    includeComma = true;
                                }
                                else
                                {
                                    if (level - 2 <= cascadingRelativePaths.Count - 1)
                                    {
                                        csvRow.Append(CSVHelpers.CSVToCommaSeparatedValue(cascadingRelativePaths[level - 2], includeComma));
                                        includeComma = true;
                                    }
                                }
                            }
                        }

                        foreach (string dataLabel in dataLabelsToExport)
                        {

                            // Check for these standard controls, as represented by a fixed data label
                            if (dataLabel == DatabaseColumn.RootFolder)
                            {
                                // Export the data as is
                                csvRow.Append(CSVHelpers.CSVToCommaSeparatedValue(rootFolder, includeComma));
                                includeComma = true;
                            }
                            else
                            {
                                DataEntryControl control = controls.ControlsByDataLabelForExport[dataLabel];

                                if (dataLabel == DatabaseColumn.DateTime)
                                {
                                    if (csvDateTimeOptions == CSVDateTimeOptionsEnum.DateAndTimeColumns)
                                    {
                                        // Export both the separate Date and Time column data with or without a space as needed
                                        csvRow.Append(CSVHelpers.CSVToCommaSeparatedValue(image.GetValueCSVDateString(csvInsertSpaceBeforeDates), includeComma));
                                        includeComma = true;
                                        csvRow.Append(CSVHelpers.CSVToCommaSeparatedValue(image.GetValueCSVTimeString(csvInsertSpaceBeforeDates), true));
                                    }
                                    else
                                    {
                                        // Export the single DateTime column data
                                        if (csvDateTimeOptions == CSVDateTimeOptionsEnum.DateTimeColumnWithTSeparator)
                                        {
                                            // with the T separator
                                            csvRow.Append(CSVHelpers.CSVToCommaSeparatedValue(image.GetValueCSVDateTimeWithTSeparatorString(csvInsertSpaceBeforeDates), includeComma));
                                            includeComma = true;
                                        }
                                        else if (csvDateTimeOptions == CSVDateTimeOptionsEnum.DateTimeWithoutTSeparatorColumn)
                                        {
                                            // without the T separator
                                            csvRow.Append(CSVHelpers.CSVToCommaSeparatedValue(image.GetValueCSVDateTimeWithoutTSeparatorString(csvInsertSpaceBeforeDates), includeComma));
                                            includeComma = true;
                                        }
                                    }
                                }

                                // Now check for these custom controls, as represented by its data type
                                else if (control is DataEntryDateTimeCustom)
                                    // Export the  DateTime_ column as determined by the options
                                {
                                    if (DateTime.TryParse(image.GetValueDatabaseString(dataLabel), out DateTime dateTime))
                                    {
                                        if (csvDateTimeOptions == CSVDateTimeOptionsEnum.DateTimeColumnWithTSeparator)
                                        {
                                            // Export both the separate Date and Time column data with or without a space as needed

                                            // with the T separator
                                            csvRow.Append(CSVHelpers.CSVToCommaSeparatedValue(DateTimeHandler.ToStringCSVDateTimeWithTSeparator(dateTime, csvInsertSpaceBeforeDates), includeComma));
                                            includeComma = true;
                                        }
                                        else if (csvDateTimeOptions == CSVDateTimeOptionsEnum.DateTimeWithoutTSeparatorColumn)
                                        {
                                            // without the T separator
                                            csvRow.Append(CSVHelpers.CSVToCommaSeparatedValue(DateTimeHandler.ToStringCSVDateTimeWithoutTSeparator(dateTime, csvInsertSpaceBeforeDates), includeComma));
                                            includeComma = true;
                                        }
                                        else if (csvDateTimeOptions == CSVDateTimeOptionsEnum.DateAndTimeColumns)
                                        {
                                            // dd-MMM-yyyy HH:mm:ss
                                            csvRow.Append(CSVHelpers.CSVToCommaSeparatedValue(DateTimeHandler.ToStringDisplayDateTime(dateTime, csvInsertSpaceBeforeDates), includeComma));
                                            includeComma = true;
                                        }
                                    }
                                    else
                                    {
                                        TracePrint.PrintMessage($"DateTime_ in CSV export is not parsable for {dataLabel}: {image.GetValueDatabaseString(dataLabel)}");
                                        csvRow.Append(CSVHelpers.CSVToCommaSeparatedValue(string.Empty, includeComma));
                                        includeComma = true;
                                    }
                                }

                                // Now check for these custom controls, as represented by its data type
                                else if (control is DataEntryDate)
                                    // Export the  Date_ column as determined by the options
                                {
                                    if (DateTime.TryParse(image.GetValueDatabaseString(dataLabel), out DateTime dateTime))
                                    {
                                        if (csvDateTimeOptions == CSVDateTimeOptionsEnum.DateAndTimeColumns)
                                        {
                                            // dd-MMM-yyyy HH:mm:ss
                                            csvRow.Append(CSVHelpers.CSVToCommaSeparatedValue(DateTimeHandler.ToStringDisplayDatePortion(dateTime, csvInsertSpaceBeforeDates), includeComma));
                                            includeComma = true;
                                        }
                                        else
                                        {
                                            csvRow.Append(CSVHelpers.CSVToCommaSeparatedValue(DateTimeHandler.ToStringDatabaseDate(dateTime, csvInsertSpaceBeforeDates), includeComma));
                                            includeComma = true;
                                        }
                                    }
                                    else
                                    {
                                        TracePrint.PrintMessage($"Date_ in CSV export is not parsable for {dataLabel}: {image.GetValueDatabaseString(dataLabel)}");
                                        csvRow.Append(CSVHelpers.CSVToCommaSeparatedValue(string.Empty, includeComma));
                                        includeComma = true;
                                    }
                                }

                                else if (control is DataEntryTime)
                                    // Export the  Time_ column as determined by the options
                                {
                                    if (DateTime.TryParse(image.GetValueDatabaseString(dataLabel), out DateTime dateTime))
                                    {
                                        csvRow.Append(CSVHelpers.CSVToCommaSeparatedValue(DateTimeHandler.ToStringTime(dateTime, csvInsertSpaceBeforeDates), includeComma));
                                        includeComma = true;
                                    }
                                    else
                                    {
                                        TracePrint.PrintMessage($"Time_ in CSV export is not parsable for {dataLabel}: {image.GetValueDatabaseString(dataLabel)}");
                                        csvRow.Append(CSVHelpers.CSVToCommaSeparatedValue(string.Empty, includeComma));
                                        includeComma = true;
                                    }
                                }

                                else
                                {
                                    // Export the data as is
                                    csvRow.Append(CSVHelpers.CSVToCommaSeparatedValue(image.GetValueDatabaseString(dataLabel), includeComma));
                                    includeComma = true;
                                }
                            }
                        }

                        fileWriter.WriteLine(csvRow.ToString());
                        if (row % 5000 == 0)
                        {
                            progress.Report(new(Convert.ToInt32(((double)row) / countAllCurrentlySelectedFiles * 100.0),
                                $"Writing {row}/{countAllCurrentlySelectedFiles} file entries to CSV file. Please wait...", false, false));
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

        #region Public Static Method - Export Metadata to CSV
        // Export all the database data associated with the selected view to the .csv file indicated in the file path so that spreadsheet applications (like Excel) can display it.
        public static async Task<bool> ExportMetadataToCsv(FileDatabase database, string folderPath, CSVDateTimeOptionsEnum csvDateTimeOptions, bool csvInsertSpaceBeforeDates)
        {
            // Set up a progress handler that will update the progress bar
            Progress<ProgressBarArguments> progressHandler = new(value =>
            {
                // Update the progress bar
                UpdateProgressBar(GlobalReferences.BusyCancelIndicator, value.PercentDone, value.Message, value.IsCancelEnabled, value.IsIndeterminate);
            });
            IProgress<ProgressBarArguments> progress = progressHandler;
            return await Task.Run(() =>
            {
                try
                {
                    progress.Report(new(0, "Writing the CSV file. Please wait", false, true));

                    // For every level
                    foreach (MetadataInfoRow infoRow in database.MetadataInfo)
                    {
                        string alias = MetadataUI.CreateTemporaryAliasIfNeeded(infoRow.Level, infoRow.Alias);
                        int level = infoRow.Level;
                        string filePath = Path.Combine(folderPath, alias + ".csv");

                        // Get the rows for this level
                        DataTableBackedList<MetadataRow> rows = false == database.MetadataTablesByLevel.TryGetValue(level, out var value)
                            ? null
                            : value;

                        // Get the data labels in spreadsheet order
                        Dictionary<string, string> dataLabelsAndTypesInSpreadsheetOrder = database.MetadataGetDataLabelsInSpreadsheetOrderForExport(level);

                        // Write data as CSV rows to the indicated file
                        using (StreamWriter fileWriter = new(filePath, false))
                        {
                            // Write the header as defined by the data labels in the template file.
                            // If the data label is an empty string, we use the label instead.
                            // The append sequence results in a leading comma except for the first column
                            StringBuilder header = new();

                            // Insert level columns at the beginning
                            bool firstColumnWritten = false;
                            bool includeComma;
                            for (int i = 0; i < level; i++)
                            {
                                string tempAlias = MetadataUI.CreateTemporaryAliasIfNeeded(i, database.MetadataInfo[i].Alias);
                                includeComma = i > 0;
                                firstColumnWritten = true;
                                header.Append(CSVHelpers.CSVToCommaSeparatedValue(tempAlias, includeComma));
                            }

                            // At this point, we should have at least one column
                            // Now write the headers
                            includeComma = firstColumnWritten;
                            foreach (KeyValuePair<string, string> dataLabelAndType in dataLabelsAndTypesInSpreadsheetOrder)
                            {
                                header.Append(CSVHelpers.CSVToCommaSeparatedValue(dataLabelAndType.Key, includeComma));
                                includeComma = true;
                            }
                            fileWriter.WriteLine(header.ToString());

                            // If there are no data rows, we are done.
                            if (null == rows || rows.RowCount == 0)
                            {
                                // No data for this level, so skip it
                                continue;
                            }
                            // Write each row as a line
                            foreach (MetadataRow row in rows)
                            {
                                includeComma = false;
                                StringBuilder rowBuilder = new();
                                List<string> cascadingPaths = FilesFolders.SplitAsCascadingRelativePath(row[DatabaseColumn.FolderDataPath]);
                                if (level > 1)
                                {
                                    // corrects the above function, which returns a blank if the path is blank, but no blank if an actual path is provided
                                    cascadingPaths.Insert(0, "");
                                }

                                foreach (string path in cascadingPaths)
                                {
                                    rowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(path, includeComma));
                                    includeComma = true;
                                }

                                foreach (KeyValuePair<string, string> dataLabelAndType in dataLabelsAndTypesInSpreadsheetOrder)
                                {
                                    if (false == dataLabelsAndTypesInSpreadsheetOrder.ContainsKey(dataLabelAndType.Key))
                                    {
                                        // Skip a column as it is flagged as not for export
                                        continue;
                                    }

                                    switch (dataLabelAndType.Value)
                                    {
                                        case Control.DateTime_:
                                            // Export the DateTime_ column as determined by the options
                                            if (DateTime.TryParse(row[dataLabelAndType.Key], out DateTime dateTime))
                                            {
                                                if (csvDateTimeOptions == CSVDateTimeOptionsEnum.DateTimeColumnWithTSeparator)
                                                {
                                                    // with the T separator
                                                    rowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(DateTimeHandler.ToStringCSVDateTimeWithTSeparator(dateTime, csvInsertSpaceBeforeDates), includeComma));
                                                    includeComma = true;
                                                }
                                                else if (csvDateTimeOptions == CSVDateTimeOptionsEnum.DateTimeWithoutTSeparatorColumn)
                                                {
                                                    // without the T separator
                                                    rowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(DateTimeHandler.ToStringCSVDateTimeWithoutTSeparator(dateTime, csvInsertSpaceBeforeDates), includeComma));
                                                    includeComma = true;
                                                }
                                                else if (csvDateTimeOptions == CSVDateTimeOptionsEnum.DateAndTimeColumns)
                                                {
                                                    // dd-MMM-yyyy HH:mm:ss
                                                    rowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(DateTimeHandler.ToStringDisplayDateTime(dateTime, csvInsertSpaceBeforeDates), includeComma));
                                                    includeComma = true;
                                                }
                                            }
                                            else
                                            {
                                                rowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(row[dataLabelAndType.Key], includeComma));
                                                includeComma = true;
                                            }

                                            break;
                                        case Control.Date_:
                                            // Export the  Date_ column as determined by the options
                                            if (DateTime.TryParse(row[dataLabelAndType.Key], out DateTime date))
                                            {
                                                if (csvDateTimeOptions == CSVDateTimeOptionsEnum.DateAndTimeColumns)
                                                {
                                                    rowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(DateTimeHandler.ToStringDisplayDatePortion(date, csvInsertSpaceBeforeDates), includeComma));
                                                    includeComma = true;
                                                }
                                                else
                                                {
                                                    rowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(DateTimeHandler.ToStringDatabaseDate(date, csvInsertSpaceBeforeDates), includeComma));
                                                    includeComma = true;
                                                }
                                            }
                                            else
                                            {
                                                rowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(row[dataLabelAndType.Key], includeComma));
                                                includeComma = true;
                                            }

                                            break;
                                        case Control.Time_:
                                            // Export the  Time_ column
                                            string prefix = csvInsertSpaceBeforeDates ? " " : string.Empty;
                                            rowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(prefix + row[dataLabelAndType.Key], includeComma));
                                            includeComma = true;
                                            break;
                                        default:
                                            rowBuilder.Append(CSVHelpers.CSVToCommaSeparatedValue(row[dataLabelAndType.Key], includeComma));
                                            includeComma = true;
                                            break;
                                    }
                                }

                                fileWriter.WriteLine(rowBuilder.ToString());
                            }
                        }

                        progress.Report(new(Convert.ToInt32((double)level / rows.RowCount * 100.0),
                            $"Writing {filePath}.csv file. Please wait...", false, false));
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
            Progress<ProgressBarArguments> progressHandler = new(value =>
            {
                // Update the progress bar
                UpdateProgressBar(GlobalReferences.BusyCancelIndicator, value.PercentDone, value.Message, value.IsCancelEnabled, value.IsIndeterminate);
            });
            IProgress<ProgressBarArguments> progress = progressHandler;

            List<string> importErrors = [];
            return await Task.Run(() =>
            {
                const int bulkFilesToHandle = 2000;
                int processedFilesCount = 0;
                int totalFilesProcessed = 0;
                int dateTimeErrors = 0;
                progress.Report(new(0, "Reading the CSV file. Please wait", false, true));

                // PART 1. Read in the CSV file. Return false if there is a problem in reading the CSV file or if the CSV file is empty
                if (false == CSVHelpers.TryReadingCSVFile(filePath, out List<List<string>> parsedFile, importErrors))
                {
                    return new(false, importErrors);
                }

                // Trim the metadata column from the parsed file, as they are not relevant when importing the data
                parsedFile = CSVHelpers.TrimMetadataColumnsIfNeeded(parsedFile, fileDatabase.MetadataInfo);

                // Now that we have a parsed file, get its headers, which we will use as DataLabels
                List<string> dataLabelsFromCSV = parsedFile[0].Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();

                // Part 2. Abort if required CSV column are missing or there is a problem matching the CSV file headers against the DB headers.
                if (false == VerifyCSVHeaders(fileDatabase, dataLabelsFromCSV, importErrors))
                {
                    return new(false, importErrors);
                }

                // Part 3: Create a List of all data rows, where each row is a dictionary containing the header and that row's valued for the header
                List<Dictionary<string, string>> rowDictionaryList = CSVHelpers.GetAllDataRows(dataLabelsFromCSV, parsedFile);

                // Part 4. For every row, validate each column's data against its type. Abort if the type does not match
                if (false == VerifyDataInColumns(fileDatabase, dataLabelsFromCSV, rowDictionaryList, importErrors))
                {
                    return new(false, importErrors);
                }

                //
                // Part 5. Check and manage duplicates
                // 
                // Get a list of duplicates in the database, i.e. rows with both the Same relativePath and File
                List<string> databaseDuplicates = fileDatabase.GetDistinctRelativePathFileCombinationsDuplicates();

                // Sort the rowDictionaryList so that duplicates in the CSV file (with the same relative path / File name) are in order, one after the other.
                List<Dictionary<string, string>> sortedRowDictionaryList = rowDictionaryList.OrderBy(dict => dict["RelativePath"]).ThenBy(dict => dict["File"]).ToList();
                int sortedRowDictionaryListCount = sortedRowDictionaryList.Count;
                // Create the data structure for the query

                List<ColumnTuplesWithWhere> imagesToUpdate = [];

                // Handle duplicates and more
                int nextRowIndex = 0;
                string duplicatePath = string.Empty; // a duplicate was identified, and this holds the duplicate path
                List<Dictionary<string, string>> duplicatesDictionaryList = [];

                CultureInfo provider = CultureInfo.InvariantCulture;
                foreach (Dictionary<string, string> rowDict in sortedRowDictionaryList)
                {
                    // For every row...
                    nextRowIndex++;
                    string currentPath = Path.Combine(rowDict[DatabaseColumn.RelativePath], rowDict[DatabaseColumn.File]); // the path of the current row 

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
                            string error = UpdateDuplicatesInDatabase(fileDatabase, duplicatesDictionaryList, Path.GetDirectoryName(duplicatePath),
                                Path.GetFileName(duplicatePath));
                            if (false == string.IsNullOrEmpty(error))
                            {
                                importErrors.Add(error);
                            }

                            duplicatesDictionaryList.Clear();
                        }
                        continue;
                    }

                    // Check if we are at the end of a duplicate sequence
                    if (duplicatesDictionaryList.Count > 0)
                    {
                        // This entry marks the end of a sequence as the paths aren't equal but we have duplicates. Process the prior sequence
                        string error = UpdateDuplicatesInDatabase(fileDatabase, duplicatesDictionaryList, Path.GetDirectoryName(duplicatePath),
                            Path.GetFileName(duplicatePath));
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
                        string examinedPath =
                            Path.Combine(nextRow[DatabaseColumn.RelativePath],
                                nextRow[DatabaseColumn.File]); // the path of a surrounding row currently being examined to see if its a duplicate
                        if (examinedPath == currentPath)
                        {
                            // Yup, its the beginning of a sequence.
                            duplicatePath = currentPath;
                            duplicatesDictionaryList.Clear();
                            duplicatesDictionaryList.Add(rowDict);
                            continue;
                        }

                        // It must be singleton
                        duplicatePath = string.Empty;
                        if (databaseDuplicates.Contains(currentPath))
                        {
                            // But, if the database contains a duplicate with the same relativePath/File, then we want to update just the first database duplicate, rather than update all those
                            // database duplicates with the same value (if we let it fall thorugh)
                            duplicatesDictionaryList.Add(rowDict);
                            string error = UpdateDuplicatesInDatabase(fileDatabase, duplicatesDictionaryList, Path.GetDirectoryName(currentPath),
                                Path.GetFileName(currentPath));
                            if (false == string.IsNullOrEmpty(error))
                            {
                                importErrors.Add(error);
                            }
                            duplicatesDictionaryList.Clear();
                            continue;
                        }
                    }
                    #endregion Handle duplicates

                    #region Process each column in a row by its header type
                    // Process each non-duplicate row
                    // Note that we never update Path-related fields (File, RelativePath)
                    ColumnTuplesWithWhere imageToUpdate = new();
                    DateTime datePortion = DateTime.MinValue;
                    DateTime timePortion = DateTime.MinValue;
                    DateTime dateTime = DateTime.MinValue;
                    foreach (string header in rowDict.Keys)
                    {
                        string type;
                        // For every column ...
                        if (header == ControlDeprecated.DateLabel || header == ControlDeprecated.TimeLabel || header == ControlDeprecated.Folder ||
                            header == ControlDeprecated.ImageQuality || header == DatabaseColumn.RootFolder)
                        {
                            // We have to treat the deprecated headers (Date, Time, Folder, ImageQuality) differently as they won't be in the template.
                            // Similarly, RootFolder is a special case.It is not in the template, but generated on CSV export on the fly.
                            type = header;
                        }
                        else
                        {
                            ControlRow controlRow = fileDatabase.GetControlFromControls(header);
                            type = controlRow.Type;
                        }

                        // process each column but only if its of the specific type
                        if (IsCondition.IsControlType_AnyNonRequired(type))
                        {
                            if (type == Control.DateTime_)
                            {
                                // Translate the various datetime formats into the database format
                                string strDateTime = rowDict[header];
                                if (DateTime.TryParseExact(strDateTime, Time.DateTimeCSVWithoutTSeparator, provider, DateTimeStyles.None, out DateTime dateTimeCustom))
                                {
                                    imageToUpdate.Columns.Add(new(header, dateTimeCustom));
                                }
                                else if (DateTime.TryParseExact(strDateTime, Time.DateTimeCSVWithTSeparator, provider, DateTimeStyles.None, out dateTimeCustom))
                                {
                                    imageToUpdate.Columns.Add(new(header, dateTimeCustom));
                                }
                                else if (DateTime.TryParseExact(strDateTime, Time.DateTimeDisplayFormat, provider, DateTimeStyles.None, out dateTimeCustom))
                                {
                                    imageToUpdate.Columns.Add(new(header, dateTimeCustom));
                                }

                                else
                                {
                                    // Shouldnt happen as error checking of date format was done before this.
                                    imageToUpdate.Columns.Add(new(header, ControlDefault.DateTimeCustomDefaultValue));
                                }
                            }
                            else
                            {
                                imageToUpdate.Columns.Add(new(header, rowDict[header]));
                            }

                        }
                        else
                        {
                            // Its not a standard control, so check if its a date/time/DateTime control and handle that as these are special cases
                            // Day: dd-MMM-yyyy 03-Jul-2017 or d-MMM-yyyy 3-Jul-2017
                            // Time: HH:mm:ss 12:30:57 or H:mm:ss 2:23:33
                            // DateTime:
                            // - yyyy-MM-ddTHH:mm:ss (includes T separator): 
                            // - yyyy-MM-dd HH:mm:ss (excludes T separator) 
                            if (type == DatabaseColumn.DateTime)
                            {
                                string strDateTime = rowDict[header];
                                if (DateTime.TryParseExact(strDateTime, Time.DateTimeCSVWithoutTSeparator, provider, DateTimeStyles.None, out dateTime))
                                {
                                    // Standard DateTime
                                    // Debug.Print("Standard: " + dateTime.ToString());
                                }
                                else if (DateTime.TryParseExact(strDateTime, Time.DateTimeCSVWithTSeparator, provider, DateTimeStyles.None, out dateTime))
                                {
                                    // Standard DateTime wit T separator
                                    // Debug.Print("StandardT: " + dateTime.ToString());
                                }
                            }
                            else if (type == ControlDeprecated.DateLabel)
                            {
                                // Date only
                                string strDateTime = rowDict[header];
                                if (DateTime.TryParseExact(strDateTime, Time.DateDisplayFormats, provider, DateTimeStyles.None, out DateTime tempDateTime))
                                {
                                    datePortion = tempDateTime;
                                }
                            }
                            else if (type == ControlDeprecated.TimeLabel)
                            {
                                // Time only
                                string strDateTime = rowDict[header];
                                if (DateTime.TryParseExact(strDateTime, Time.TimeFormats, provider, DateTimeStyles.None, out DateTime tempDateTime))
                                {
                                    //Debug.Print("Time only: " + tempDateTime.ToString());
                                    timePortion = tempDateTime;
                                }
                            }
                            else if (type == DatabaseColumn.RootFolder || type == ControlDeprecated.Folder || type == ControlDeprecated.ImageQuality)
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
                    if (dateTime != DateTime.MinValue || datePortion != DateTime.MinValue)
                    {
                        // Check if we need to update dateTime from the separate date and time fields were used, update dateTime from them
                        if (datePortion != DateTime.MinValue && timePortion != DateTime.MinValue)
                        {
                            // We have a valid separate date and time. Combine it.
                            dateTime = datePortion.Date + timePortion.TimeOfDay;
                        }

                        // We should now have a valid dateTime. Add it to the database. 
                        imageToUpdate.Columns.Add(new(DatabaseColumn.DateTime, dateTime));
                        // Debug.Print("Wrote DateTime: " + dateTime.ToString());
                    }
                    else if (dateTime == DateTime.MinValue && datePortion == DateTime.MinValue)
                    {
                        dateTimeErrors++;
                        // importErrors.Add(String.Format("{0}: Could not extract datetime", currentPath));
                        // Debug.Print("Could not extract datetime");
                    }

                    // ReSharper disable once RedundantAssignment
                    dateTime = DateTime.MinValue;
                    // ReSharper disable once RedundantAssignment
                    datePortion = DateTime.MinValue;
                    // ReSharper disable once RedundantAssignment
                    timePortion = DateTime.MinValue;

                    // NOTE: We currently do NOT report an error if there is a row in the csv file whose location does not match
                    // the location in the database. We could do this by performing a check before submitting a query, eg. something like:
                    //  Select Count (*) from DataTable where File='IMG_00197.JPG' or File='IMG_01406.JPG' or File='XX.JPG'
                    // where we would then compare the counts against the rows. However, this likely has a performance hit, and it doesn't 
                    // return the erroneous rows... So its not done yet.

                    // Add to the query only if there are columns to add!
                    if (imageToUpdate.Columns.Count > 0)
                    {
                        if (rowDict.ContainsKey(DatabaseColumn.RelativePath) && !string.IsNullOrWhiteSpace(rowDict[DatabaseColumn.RelativePath]))
                        {
                            imageToUpdate.SetWhere(rowDict[DatabaseColumn.RelativePath], rowDict[DatabaseColumn.File]);
                        }
                        else
                        {
                            imageToUpdate.SetWhere(rowDict[DatabaseColumn.File]);
                        }

                        imagesToUpdate.Add(imageToUpdate);
                    }

                    // Write current batch of updates to database. Note that we Update the database every number of rows as specified in bulkFilesToHandle.
                    // We should probably put in a cancellation CancelToken somewhere around here...
                    if (imagesToUpdate.Count >= bulkFilesToHandle)
                    {
                        processedFilesCount += bulkFilesToHandle;
                        progress.Report(new(Convert.ToInt32(((double)processedFilesCount) / sortedRowDictionaryListCount * 100.0),
                            $"Processing {processedFilesCount}/{sortedRowDictionaryListCount} files. Please wait...", false, false));
                        fileDatabase.UpdateFiles(imagesToUpdate);
                        imagesToUpdate.Clear();
                    }
                }

                // perform any remaining updates
                if (dateTimeErrors != 0)
                {
                    // Need to check IF THIS WORKS FOR files with no date-time fields!
                    importErrors.Add($"The Date/Time was not updated for {dateTimeErrors} / {totalFilesProcessed} files. ");
                    if (dataLabelsFromCSV.Contains(DatabaseColumn.DateTime) || (dataLabelsFromCSV.Contains(ControlDeprecated.DateLabel) &&
                                                                                dataLabelsFromCSV.Contains(ControlDeprecated.TimeLabel)))
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

        // Return false if required CSV column are missing or there is a problem matching the CSV file headers against the DB headers.
        private static bool VerifyCSVHeaders(FileDatabase fileDatabase, List<string> dataLabelsFromCSV, List<string> importErrors)
        {
            bool abort = false;
            // Get the dataLabels from the database and from the headers in the CSV files (and remove any empty trailing headers from the CSV file list)
            List<string> dataLabelsFromDB = fileDatabase.GetDataLabelsExceptIDInSpreadsheetOrderFromControls();
            // Because Date and Time (which are not controls) may appear instead of DateTime, we add them explicitly so they can pass this test
            // Similarly, we add (and we will skip over)
            // - Folder and ImageQuality (both deprecated columns that could exist in CSV files pre v2.3)
            // - RootFolder (which isn't in the Template)
            dataLabelsFromDB.Add(DatabaseColumn.RootFolder);
            dataLabelsFromDB.Add(ControlDeprecated.DateLabel);
            dataLabelsFromDB.Add(ControlDeprecated.TimeLabel);
            dataLabelsFromDB.Add(ControlDeprecated.Folder);
            dataLabelsFromDB.Add(ControlDeprecated.ImageQuality);

            // Get the data labels from the csv file
            List<string> dataLabelsInHeaderButNotFileDatabase = dataLabelsFromCSV.Except(dataLabelsFromDB).ToList();

            // Abort if the File and Relative Path columns are missing from the CSV file 
            // While the CSV data labels can be a subset of the DB data labels,
            // the File and Relative Path are a required CSV datalabel, as we can't match the DB data row without it.
            if (dataLabelsFromCSV.Contains(DatabaseColumn.File) == false || dataLabelsFromCSV.Contains(DatabaseColumn.RelativePath) == false)
            {
                importErrors.Add("CSV columns necessary to locate your image or video files are missing: ");
                if (dataLabelsFromCSV.Contains(DatabaseColumn.File) == false)
                {
                    importErrors.Add($"- the '{DatabaseColumn.File}' column.");
                }

                if (dataLabelsFromCSV.Contains(DatabaseColumn.RelativePath) == false)
                {
                    importErrors.Add(
                        $"- the '{DatabaseColumn.RelativePath}' column (You still need it even if your files are all in your root folder).");
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
                    importErrors.Add($"- {dataLabel}");
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


        // Validate Data columns against data type. Return false if any of the types don't match
        private static bool VerifyDataInColumns(FileDatabase fileDatabase, List<string> dataLabelsFromCSV, List<Dictionary<string, string>> rowDictionaryList,
            List<string> importErrors)
        {
            bool abort = false;
            // For each column in the CSV file,
            // - get its type from the template
            // - for particular types, validate the data in the column against that type
            // Validation ignored for:
            // - Note, as it can hold any data
            // - File, RelativePath, as that data row would be ignored if it does not create a valid path
            // - Date/Time/DateTime as they are handled elsewhere 
            //  - Date, Time formats must match exactl
            int rowNumber = 0;
            int numberRowsWithErrors = 0;
            int maxRowsToReportWithErrors = 2;
            // For every row
            foreach (Dictionary<string, string> rowDict in rowDictionaryList)
            {
                rowNumber++;
                bool errorInRow = false;
                // Check for Date and Time columns, and deal with them separately
                // Get the header type

                // For every column
                foreach (string csvHeader in dataLabelsFromCSV)
                {
                    if (csvHeader == ControlDeprecated.DateLabel
                        || csvHeader == ControlDeprecated.TimeLabel
                        || csvHeader == DatabaseColumn.DateTime
                        || csvHeader == ControlDeprecated.Folder
                        || csvHeader == DatabaseColumn.RootFolder
                        || csvHeader == ControlDeprecated.ImageQuality)
                    {
                        // Date/Time/DateTime checking is handled elsewhere, while Folder, RootFolder and ImageQuality are skipped
                        continue;
                    }

                    ControlRow controlRow = fileDatabase.GetControlFromControls(csvHeader);
                    string controlRowType = controlRow.Type;
                    CultureInfo provider = CultureInfo.InvariantCulture;
                    if (IsCondition.IsControlType_AnyNonRequired(controlRowType))
                    {
                        string content = rowDict[csvHeader];
                        // Validate the data as needed for each of these columns in the row
                        switch (controlRowType)
                        {
                            case Control.Flag:
                            case DatabaseColumn.DeleteFlag:
                                if (!Boolean.TryParse(content, out _))
                                {
                                    // Flag values must be true or false, but its not. So raise an error
                                    importErrors.Add(String.Format("- error in row {1} as {0} values must be true or false, but is '{2}'", csvHeader, rowNumber, content));
                                    abort = true;
                                }

                                break;
                            case Control.Counter:
                            case Control.IntegerAny:
                                if (!string.IsNullOrWhiteSpace(content) && !Int32.TryParse(content, out _))
                                {
                                    // Counters must be integers / blanks 
                                    importErrors.Add(String.Format("- error in row {1} as {0} values must be blank or an integer, but is '{2}'", csvHeader, rowNumber, content));
                                    abort = true;
                                }

                                break;
                            case Control.IntegerPositive:
                                if (!string.IsNullOrWhiteSpace(content) && !(Int32.TryParse(content, out int parsedResult) && parsedResult >= 0))
                                {
                                    // Counters must be positive integers / blanks 
                                    importErrors.Add(String.Format("- error in row {1} as {0} values must be blank or a positive integer, but is '{2}'", csvHeader, rowNumber,
                                        content));
                                    abort = true;
                                }

                                break;

                            case Control.DecimalAny:
                                if (!string.IsNullOrWhiteSpace(content) && !Double.TryParse(content, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                                {
                                    // Counters must be decimals / blanks 
                                    importErrors.Add(String.Format("- error in row {1} as {0} values must be blank or a decimal, but is '{2}'", csvHeader, rowNumber, content));
                                    abort = true;
                                }

                                break;
                            case Control.DecimalPositive:
                                if (!string.IsNullOrWhiteSpace(content) && !(Double.TryParse(content, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedDoublePositiveResult) && parsedDoublePositiveResult >= 0))
                                {
                                    // Counters must be positive integers / blanks 
                                    importErrors.Add(String.Format("- error in row {1} as {0} values must be blank or a positive decimal, but is '{2}'", csvHeader, rowNumber,
                                        content));
                                    abort = true;
                                }

                                break;

                            case Control.AlphaNumeric:
                                // Succeeds if its empty or only alphanumeric characters are present
                                if (false == (string.IsNullOrWhiteSpace(content) || IsCondition.IsAlphaNumeric(content)))
                                {
                                    // Alphanumeric must be letters, numbers and/or -_ 
                                    importErrors.Add(String.Format("- error in row {1} as {0} values must contain only letters, numbers, dashes and underscore, but is '{2}'",
                                        csvHeader, rowNumber, content));
                                    abort = true;
                                }

                                break;
                            case Control.FixedChoice:
                                // We allow empty values, even though it may not be a list option
                                if (false == string.IsNullOrWhiteSpace(content) && Choices.ChoicesFromJson(controlRow.List).Contains(content) == false)
                                {
                                    // Fixed Choices must be in the Choice List
                                    importErrors.Add(String.Format("- error in row {1} as {0} values must be in the template's choice list, but '{2}' isn't in it.", csvHeader,
                                        rowNumber, content));
                                    abort = true;
                                }

                                break;
                            case Control.MultiChoice:
                                // Only valid choices are permitted. Slso allow empty values.
                                if (false == string.IsNullOrWhiteSpace(content))
                                {
                                    Choices choices = Choices.ChoicesFromJson(controlRow.List);
                                    List<string> contentList = content.Split(',').ToList();
                                    //contentList.RemoveAt(0);
                                    foreach (string item in contentList)
                                    {
                                        if (choices.Contains(item) == false)
                                        {
                                            // Multi Choices must be in the Choice List
                                            importErrors.Add(String.Format("- error in row {1} as {0} values must be in the template's choice list, but '{2}' isn't in it.",
                                                csvHeader, rowNumber, content));
                                            abort = true;
                                        }
                                    }
                                }

                                break;
                            case Control.DateTime_:
                                if (false == DateTime.TryParseExact(content, Time.DateTimeCSVWithoutTSeparator, provider, DateTimeStyles.None, out DateTime _) &&
                                    false == DateTime.TryParseExact(content, Time.DateTimeCSVWithTSeparator, provider, DateTimeStyles.None, out _) &&
                                    false == DateTime.TryParseExact(content, Time.DateTimeDisplayFormat, provider, DateTimeStyles.None, out _))
                                {
                                    // Multi Choices must be in the Choice List
                                    importErrors.Add(String.Format("- error in row {1} as {0} values must be in one of the expected data formats, but '{2}' isn't.", csvHeader,
                                        rowNumber, content));
                                    abort = true;
                                }

                                break;
                            case Control.Date_:
                                if (false == DateTime.TryParseExact(content, Time.DateDisplayFormat, provider, DateTimeStyles.None, out DateTime _) &&
                                    false == DateTime.TryParseExact(content, Time.DateDatabaseFormat, provider, DateTimeStyles.None, out DateTime _))
                                {
                                    // Date must be in the display or databae format
                                    importErrors.Add(String.Format("- error in row {1} as {0} values must be in one of the expected date formats, but '{2}' isn't.", csvHeader,
                                        rowNumber, content));
                                    abort = true;
                                }

                                break;
                            case Control.Time_:
                                if (false == DateTime.TryParseExact(content, Time.TimeFormat, provider, DateTimeStyles.None, out DateTime _))
                                {
                                    // Date must be in the display or databae format
                                    importErrors.Add(String.Format("- error in row {1} as {0} values must be in the expected time format, but '{2}' isn't.", csvHeader, rowNumber,
                                        content));
                                    abort = true;
                                }

                                break;
                                // case Constant.Control.Note:
                                // case Constant.Control.MultiLine:
                                // default:
                                // as these can be any string, they don't require checking
                                // break;
                        }

                        if (!errorInRow && abort)
                        {
                            // If there is an error, only count one error per row.
                            numberRowsWithErrors++;
                            errorInRow = true;
                        }

                        if (numberRowsWithErrors > maxRowsToReportWithErrors)
                        {
                            importErrors.Add(
                                $"- Timelapse only reports data errors for a maximum of {maxRowsToReportWithErrors} rows. Use the information above to start fixing them.");
                            importErrors.Add("- Use the information above to check the data values in those columns for all rows.");
                            return false;
                        }
                    }
                }
            }

            return !abort;
        }

        #endregion

        // Given a list of duplicates and their common relative path, update the corresponding duplicates in the database
        // We do this by getting the IDs of duplicates in the database, where we update each database by ID to a duplicate.
        // If there is a mismatch in the number of duplicates in the database vs. in the CSV file, we just update whatever does match.
        private static string UpdateDuplicatesInDatabase(FileDatabase fileDatabase, List<Dictionary<string, string>> duplicatesDictionaryList, string relativePath, string file)
        {
            List<ColumnTuplesWithWhere> imagesToUpdate = [];
            string errorMessage = string.Empty;

            // Find THE IDs of ImageRows with those RelativePath / File values

            List<long> duplicateIDS = fileDatabase.SelectFilesByRelativePathAndFileName(relativePath, file);

            if (duplicateIDS.Count != duplicatesDictionaryList.Count)
            {
                string dbEntry = duplicateIDS.Count == 1 ? "entry" : "entries";
                string csvEntry = duplicatesDictionaryList.Count == 1 ? "entry" : "entries";
                errorMessage =
                    $"duplicate entry mismatch for {Path.Combine(relativePath, file)}: {duplicateIDS.Count} database {dbEntry} vs. {duplicatesDictionaryList.Count} CSV {csvEntry}.";
            }

            int idIndex = 0;
            foreach (Dictionary<string, string> rowDict in duplicatesDictionaryList)
            {
                if (idIndex >= duplicateIDS.Count)
                {
                    break;
                }

                CultureInfo provider = CultureInfo.InvariantCulture;
                DateTime datePortion = DateTime.MinValue;
                DateTime timePortion = DateTime.MinValue;
                DateTime dateTime = DateTime.MinValue;

                // Process each row
                ColumnTuplesWithWhere imageToUpdate = new();
                foreach (string header in rowDict.Keys)
                {
                    if (header == DatabaseColumn.RootFolder || header == ControlDeprecated.Folder || header == ControlDeprecated.ImageQuality)
                    {
                        // Skip the Folder, RootFolder and ImageQuality columns,
                        // Folder / RootFolderdata should not be updated, and ImageQuality is deprecated and thus ignored.
                        continue;
                    }

                    if (header == ControlDeprecated.DateLabel
                        || header == ControlDeprecated.TimeLabel
                        || header == DatabaseColumn.DateTime)
                    {
                        //NEW
                        // check if its a date/ time / DateTime control and handle that as these are special cases
                        // Day: dd-MMM-yyyy 03-Jul-2017
                        // Time: HH:mm:ss 12:30:57
                        // DateTime:
                        // - yyyy-MM-ddTHH:mm:ss (includes T separator): 
                        // - yyyy-MM-dd HH:mm:ss (excludes T separator) 
                        if (header == DatabaseColumn.DateTime)
                        {
                            string strDateTime = rowDict[header];
                            if (DateTime.TryParseExact(strDateTime, Time.DateTimeCSVWithoutTSeparator, provider, DateTimeStyles.None, out dateTime))
                            {
                                // Standard DateTime
                                // Debug.Print("Standard: " + dateTime.ToString());
                            }
                            else if (DateTime.TryParseExact(strDateTime, Time.DateTimeCSVWithTSeparator, provider, DateTimeStyles.None, out dateTime))
                            {
                                // Standard DateTime wit T separator
                                // Debug.Print("StandardT: " + dateTime.ToString());
                            }
                        }
                        else if (header == ControlDeprecated.DateLabel)
                        {
                            // Date only
                            string strDateTime = rowDict[header];
                            if (DateTime.TryParseExact(strDateTime, Time.DateDisplayFormat, provider, DateTimeStyles.None, out DateTime tempDateTime))
                            {
                                datePortion = tempDateTime;
                            }
                        }
                        else if (header == ControlDeprecated.TimeLabel)
                        {
                            // Time only
                            string strDateTime = rowDict[header];
                            if (DateTime.TryParseExact(strDateTime, Time.TimeFormat, provider, DateTimeStyles.None, out DateTime tempDateTime))
                            {
                                //Debug.Print("Time only: " + tempDateTime.ToString());
                                timePortion = tempDateTime;
                            }
                        }
                        //End NEW
                    }

                    ControlRow controlRow = fileDatabase.GetControlFromControls(header);
                    // process each column but only if its of the specific type
                    if (controlRow != null &&
                        (controlRow.Type == Control.Flag ||
                         controlRow.Type != DatabaseColumn.DeleteFlag ||
                         controlRow.Type == Control.Counter ||
                         controlRow.Type == Control.IntegerAny ||
                         controlRow.Type == Control.IntegerPositive ||
                         controlRow.Type == Control.DecimalAny ||
                         controlRow.Type == Control.DecimalPositive ||
                         controlRow.Type == Control.FixedChoice ||
                         controlRow.Type == Control.MultiLine
                        ))
                    {
                        imageToUpdate.Columns.Add(new(header, rowDict[header]));
                    }
                }

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
                    imageToUpdate.Columns.Add(new(DatabaseColumn.DateTime, dateTime));
                    // Debug.Print("Wrote DateTime: " + dateTime.ToString());
                }

                // Add to the query only if there are columns to add!
                if (imageToUpdate.Columns.Count > 0)
                {
                    imageToUpdate.SetWhere(duplicateIDS[idIndex]);
                    imagesToUpdate.Add(imageToUpdate);
                }

                idIndex++;
            }

            if (imagesToUpdate.Count > 0)
            {
                fileDatabase.UpdateFiles(imagesToUpdate);
            }

            return errorMessage;
        }

        #endregion

        #region Public Method - Update Progress Bar

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
                busyCancelIndicator.CancelButtonText = isCancelEnabled ? "Cancel" : "Processing CSV file...";
            });
        }

        #endregion
    }
}
