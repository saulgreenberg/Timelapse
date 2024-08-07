using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;

namespace Timelapse.Util
{
    public static class CSVHelpers
    {
        //Helper methods for various CSVReader/Writers including Standard-specific CSV writers
        #region Read Csv helpers. 
        // Read in the CSV file. Return false if there is a problem in reading the CSV file or if the CSV file is empty
        public static bool TryReadingCSVFile(string filePath, out List<List<string>> parsedFile, List<string> importErrors)
        {
            parsedFile = CSVFileReadAndParseAsListOfRows(filePath);

            // Abort if the CSV file could not be read 
            if (parsedFile == null)
            {
                // Could not open the file
                importErrors.Add($"The file '{Path.GetFileName(filePath)}' could not be read. Things to check:");
                importErrors.Add("- Is the file is currently opened by another application?");
                importErrors.Add("- Do you have permission to read this file (especially network file systems, which sometimes limit access).");
                return false;
            }

            // Abort if The CSV file is empty or only contains a header row
            if (parsedFile.Count < 1)
            {
                importErrors.Add($"The file '{Path.GetFileName(filePath)}' appears to be empty.");
                return false;
            }

            if (parsedFile.Count < 2)
            {
                importErrors.Add($"The file '{Path.GetFileName(filePath)}' does not contain any data.");
                return false;
            }
            return true;
        }

        // Parse the rows in a CSV file and return it as a list of lines, each line being a list of values in a csv row
        public static List<List<string>> CSVFileReadAndParseAsListOfRows(string path)
        {
            try
            {
                List<List<string>> parsedRows = new List<List<string>>();
                using (TextFieldParser parser = new TextFieldParser(path))
                {
                    parser.Delimiters = new[] { "," };
                    while (true)
                    {
                        string[] parts = parser.ReadFields();
                        if (parts == null)
                        {
                            break;
                        }
                        List<string> rowFields = parts.ToList();
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

        // Get all the data rows from the CSV file. Each dictionary entry is a row with a list of matching  CSV column Headers and column value 
        public static List<Dictionary<string, string>> GetAllDataRows(List<string> dataLabelsFromCSV, List<List<string>> parsedFile)
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
                    //string valueToAdd = (i < parsedRow.Count) ? parsedRow[i] : string.Empty;
                    rowDictionary.Add(dataLabelsFromCSV[i], parsedRow[i]);
                }
                rowDictionaryList.Add(rowDictionary);
            }
            return rowDictionaryList;
        }
        #endregion

        #region Write CSV helpers
        // Given a string representing a comma-separated row of values, add a value to it.
        // If special characters are in the string,  escape the string as needed
        // if includeComma is true, prepend a comma to the value
        public static string CSVToCommaSeparatedValue(string value, bool includeComma)
        {
            if (value == null)
            {
                return includeComma ? "," : string.Empty;
            }

            char[] charArray = { '\"', ',', '\r', '\n' };
            if (value.IndexOfAny(charArray) > -1)
            {
                // commas, double quotation marks, line feeds (\x0A), and carriage returns (\x0D) require leading and ending double quotation marks be added
                // double quotation marks within the field also have to be escaped as double quotes
                return includeComma
                ? "," + "\"" + value.Replace("\"", "\"\"") + "\""
                : "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return includeComma
                ? "," + value
                : value;
        }

        // Convert a comma-separated string into a list of non-empty trimmed strings
        public static List<string> CommaSeparatedStringToList(string commaSeparatedList)
        {
            if (string.IsNullOrWhiteSpace(commaSeparatedList) || commaSeparatedList == "[]")
            {
                return null;
            }
            return commaSeparatedList.Split(',').Select(s => s.Trim()).Select(s => Regex.Replace(s, @"\s+", " ")).Where(s => !string.IsNullOrEmpty(s)).ToList();
        }
        #endregion
    }
}
