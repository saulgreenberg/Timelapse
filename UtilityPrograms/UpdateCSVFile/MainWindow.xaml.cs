﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;

namespace UpdateCSVFile
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public partial class MainWindow
    {
        readonly string jsonHeaderTranslationsFileName = "headerTranslations.json";
        readonly string outSuffix = "_updated";
        FileStream instream;
        StreamReader csvReader;
        StreamWriter outstream;

        // for each existing column in the CSV file, the key value pair defines the old header , new header names
        // this dictionary will be created from the headerTranslations.json file
        Dictionary<string, string> HeaderUpdateDictionary;
        private string rootPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        public MainWindow()
        {
            InitializeComponent();
        }

        // Try to convert the csv file
        public bool TryConvertCsv(string csvOriginalFilePath, string csvTranslatedFilePath)
        {
            HeaderUpdateDictionary = new Dictionary<string, string>();
            // Open the various streams, which also populates HeaderUpdateDictionary
            if (OpenStreamsAndLoadHeaderUpdates(csvOriginalFilePath, csvTranslatedFilePath, this.jsonHeaderTranslationsFileName) == false)
            {
                this.CloseStreams();
                return false;
            }

            // Update the .csv file headers 
            List<string> ListOriginalHeadersInCSVFile = ReadAndParseLine(csvReader);
            List<string> ListUpdatedHeaders = new List<string>();
            foreach (string header in ListOriginalHeadersInCSVFile)
            {
                ListUpdatedHeaders.Add(HeaderUpdateDictionary.TryGetValue(header, out string key)
                    ? HeaderUpdateDictionary[key]
                    : header);
            }

            List<string> ListFinalHeaders = ListUpdatedHeaders.Select(item => (string)item.Clone()).ToList();
            if (this.CreateRelativePathCheckBox.IsChecked == true && ListUpdatedHeaders.Contains(Constant.DatabaseColumn.RelativePath) == false)
            {
                // If the CreateRelativePath checkbox is checked, add the RelativePath header if needed
                ListFinalHeaders.Add(Constant.DatabaseColumn.RelativePath);
            }
            // Write the headers to the CSV file
            WriteListAsCommaSeparatedLine(outstream, ListFinalHeaders);

            // Repopulate each row, adjusting the file name and relative path as required
            int rowNumber = 0;
            for (List<string> row = ReadAndParseLine(csvReader); row != null; row = ReadAndParseLine(csvReader))
            {
                // For each row
                rowNumber++;
                if (row.Count == ListUpdatedHeaders.Count - 1)
                {
                    // .csv files are ambiguous in the sense a trailing comma may or may not be present at the end of the line
                    // if the final field has a value this case isn't a concern, but if the final field has no value then there's
                    // no way for the parser to know the exact number of fields in the line
                    row.Add(string.Empty);
                }

                // For a single row, create a dictionary matching the CSV column Header and that row's recorded value for that column
                // Project-specific DataLabel fields and values
                string[] rowArray = row.ToArray();
                string[] headerArray = ListUpdatedHeaders.ToArray();
                Dictionary<string, string> rowDictionary = new Dictionary<string, string>();
                int trimAmount = Convert.ToInt32(TrimSlider.Value);
                for (int j = 0; j < headerArray.Length; j++)
                {
                    // for each header
                    if (headerArray.Length != rowArray.Length)
                    {
                        this.FeedbackText.Text +=
                            $"Expected {ListUpdatedHeaders.Count} fields in line {row.Count} but found {rowArray.Length}.{Environment.NewLine}";
                        this.FeedbackText.Text +=
                            $"Could not update the data correctly due to the above reasons.{Environment.NewLine}";
                        this.CloseStreams();
                        return false;
                    }

                    if (headerArray[j] == Constant.DatabaseColumn.File)
                    {
                        string newFileName = rowArray[j];
                        // Trim the file nameif needed
                        if (this.TrimFileCheckBox.IsChecked == true && trimAmount > 0)
                        {
                            if (newFileName.Length >= trimAmount)
                            {
                                newFileName = newFileName.Remove(0, trimAmount);
                            }
                        }

                        // Add or update the relativePath to the new path if needed
                        // We do this within the File key as we begin with the appropriate (trimmed?) file name
                        if (this.CreateRelativePathCheckBox.IsChecked == true)
                        {
                            string relativePath = Path.GetDirectoryName(newFileName);
                            // The new file name is extracted from the path
                            newFileName = Path.GetFileName(newFileName);
                            if (rowDictionary.ContainsKey(Constant.DatabaseColumn.RelativePath))
                            {
                                // Update RelativePath to its new value if the RelativePath key already exists
                                rowDictionary[Constant.DatabaseColumn.RelativePath] = relativePath;
                            }
                            else
                            {
                                // Add  RelativePath and its new value if the RelativePath key does not exist
                                rowDictionary.Add(Constant.DatabaseColumn.RelativePath, relativePath);
                            }
                        }
                        // Add the File and its new adjusted value 
                        rowDictionary.Add(headerArray[j], newFileName);
                    }
                    else if (headerArray[j] == Constant.DatabaseColumn.RelativePath)
                    {
                        if (this.CreateRelativePathCheckBox.IsChecked == false)
                        {
                            // Add the existing RelativePath value from the CSV file
                            // Note that we ignore the RelativePath if the CreateRelativePath checkbox is checked,
                            // as the new value will have been added above
                            rowDictionary.Add(headerArray[j], rowArray[j]);
                        }
                    }
                    else
                    {
                        // Add the unalterd key/value pair
                        rowDictionary.Add(headerArray[j], rowArray[j]);
                    }
                }
                WriteListAsCommaSeparatedLine(outstream, ListFinalHeaders, rowDictionary);
            }
            this.FeedbackText.Text += $"Wrote {rowNumber} data rows.{Environment.NewLine}";
            this.CloseStreams();
            return true;
        }

        #region Callbacks
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            this.FeedbackText.Text = string.Empty;

            if (TryGetFileFromUser(
                                 "Select a .csv file to update",
                                 rootPath,
                                 String.Format("Comma separated value files (*{0})|*{0}", ".csv"),
                                 ".csv",
                                 out string csvOriginalFilePath) == false)
            {
                return;
            }

            // Set the root path so the next time we try to get a csv file it will go to the same place
            this.rootPath = Path.GetDirectoryName(csvOriginalFilePath) ?? string.Empty;

            // Set the output file name
            string csvTranslatedFileName = Path.GetFileNameWithoutExtension(csvOriginalFilePath) + outSuffix + Path.GetExtension(csvOriginalFilePath);
            string csvTranslatedFilePath = Path.Combine(rootPath, csvTranslatedFileName);

            bool result = TryConvertCsv(csvOriginalFilePath, csvTranslatedFilePath);
            if (result == false)
            {
                this.FeedbackText.Text += String.Format("{0}Aborted due to errors.{0}.", Environment.NewLine);
            }
            else
            {
                this.FeedbackText.Text += String.Format("{0}Success. The updated CSV file is {1}.{0} ", Environment.NewLine, csvTranslatedFileName);
            }
        }

        private void TrimFile_CheckedChanged(object sender, RoutedEventArgs e)
        {
            TrimSlider.IsEnabled = TrimFileCheckBox.IsChecked == true;
        }
        #endregion

        #region Utilities
        // Load the header translation dictionary from the file specified by path
        private bool LoadJSon(string path)
        {
            try
            {
                string jsonString = File.ReadAllText(path);
                this.HeaderUpdateDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);
            }
            catch
            {
                return false;
            }
            return true;
        }

        private static List<string> ReadAndParseLine(StreamReader csvReader)
        {
            string unparsedLine = csvReader.ReadLine();
            if (unparsedLine == null)
            {
                return null;
            }

            List<string> parsedLine = new List<string>();
            bool isFieldEscaped = false;
            int fieldStart = 0;
            bool inField = false;
            for (int index = 0; index < unparsedLine.Length; ++index)
            {
                char currentCharacter = unparsedLine[index];
                if (inField == false)
                {
                    if (currentCharacter == '\"')
                    {
                        // start of escaped field
                        isFieldEscaped = true;
                        fieldStart = index + 1;
                    }
                    else if (currentCharacter == ',')
                    {
                        // empty field
                        // promote null values to empty values to prevent the presence of SQNull objects in data tables
                        // much Timelapse code assumes data table fields can be blindly cast to string and breaks once the data table has been
                        // refreshed after null values are inserted
                        parsedLine.Add(string.Empty);
                        continue;
                    }
                    else
                    {
                        // start of unescaped field
                        fieldStart = index;
                    }

                    inField = true;
                }
                else
                {
                    if (currentCharacter == ',' && isFieldEscaped == false)
                    {
                        // end of unescaped field
                        inField = false;
                        string field = unparsedLine.Substring(fieldStart, index - fieldStart);
                        parsedLine.Add(field.Trim());
                    }
                    else if (currentCharacter == '\"' && isFieldEscaped)
                    {
                        // escaped character encountered; check for end of escaped field
                        int nextIndex = index + 1;
                        if (nextIndex < unparsedLine.Length)
                        {
                            if (unparsedLine[nextIndex] == ',')
                            {
                                // end of escaped field
                                // note: Whilst this implementation supports escaping of carriage returns and line feeds on export it does not support them on
                                // import.  This is common in .csv parsers and can be addressed if needed by appending the next line to unparsedLine and 
                                // continuing parsing rather than terminating the field.
                                inField = false;
                                isFieldEscaped = false;
                                string field = unparsedLine.Substring(fieldStart, index - fieldStart);
                                field = field.Replace("\"\"", "\"");
                                parsedLine.Add(field.Trim());
                                ++index;
                            }
                            else if (unparsedLine[nextIndex] == '"')
                            {
                                // escaped double quotation mark
                                // just move next to skip over the second quotation mark as replacement back to one quotation mark is done in field extraction
                                ++index;
                            }
                        }
                    }
                }
            }

            // if the last character is a non-comma add the final (non-empty) field
            // final empty fields are ambiguous at this level and therefore handled by the caller
            if (inField)
            {
                string field = unparsedLine.Substring(fieldStart, unparsedLine.Length - fieldStart);
                if (isFieldEscaped)
                {
                    field = field.Replace("\"\"", "\"");
                }
                parsedLine.Add(field.Trim());
            }

            return parsedLine;
        }

        // get a file from the user
        public static bool TryGetFileFromUser(string title, string defaultFilePath, string filter, string defaultExtension, out string selectedFilePath)
        {
            // Get the template file, which should be located where the images reside
            using (OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Title = title,
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false,
                AutoUpgradeEnabled = true,

                // Set filter for file extension and default file extension 
                DefaultExt = defaultExtension,
                Filter = filter
            })
            {
                if (string.IsNullOrWhiteSpace(defaultFilePath))
                {
                    openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
                else
                {
                    openFileDialog.InitialDirectory = string.Empty; // Path.GetDirectoryName(defaultFilePath);
                    openFileDialog.FileName = string.Empty; // Path.GetFileName(defaultFilePath);
                }

                if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    selectedFilePath = openFileDialog.FileName;
                    return true;
                }
                selectedFilePath = null;
                return false;
            }
        }
        #endregion

        #region Open and close streams / Write headers and values to the CSV file
        private bool OpenStreamsAndLoadHeaderUpdates(string csvOriginalFilePath, string csvTranslatedFilePath, string jsonHeaderTranslationsFileNameLocal)
        {
            try
            {
                this.instream = new FileStream(csvOriginalFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch
            {
                this.FeedbackText.Text += String.Format("Could not read the file: {0}.{1}Perhaps its open in another application?{1}", csvOriginalFilePath, Environment.NewLine);
                return false;
            }
            try
            {
                this.outstream = new StreamWriter(csvTranslatedFilePath);
            }
            catch
            {
                this.FeedbackText.Text += String.Format("Could not write the file: {0}.{1}Perhaps its open in another application?{1}", csvTranslatedFilePath, Environment.NewLine);
                return false;
            }
            try
            {
                this.csvReader = new StreamReader(instream);
            }
            catch
            {
                this.FeedbackText.Text += String.Format("Could not read the file: {0}.{1}Perhaps its open in another application?{1}", csvOriginalFilePath, Environment.NewLine);
                return false;
            }

            // Load the dictionary from the json file 
            string jsonFilePath = Path.Combine(Path.GetDirectoryName(csvOriginalFilePath) ?? string.Empty, jsonHeaderTranslationsFileNameLocal);
            if (File.Exists(jsonFilePath) == false)
            {
                this.FeedbackText.Text +=
                    $"Warning: As the {jsonHeaderTranslationsFileNameLocal} file is not present, we assume that no headers need translating.{Environment.NewLine}";
            }
            else if (this.LoadJSon(jsonFilePath) == false)
            {
                this.FeedbackText.Text +=
                    $"Could not read the file: {csvOriginalFilePath}.{Environment.NewLine}Perhaps its open in another application?{Environment.NewLine}";
                return false;
            }
            return true;
        }

        private void CloseStreams()
        {
            outstream?.Close();
            instream?.Close();
            csvReader?.Close();
        }

        // Write the headers
        private static void WriteListAsCommaSeparatedLine(StreamWriter thisOutstream, List<string> elements)
        {
            int last = elements.Count - 1;
            int i = 0;
            string line = string.Empty;
            foreach (string element in elements)
            {
                line += element;
                line += (i != last) ? "," : Environment.NewLine;
                i++;
            }
            thisOutstream.Write(line);
        }

        // Write the values
        private static void WriteListAsCommaSeparatedLine(StreamWriter thisOutstream, List<string> headers, Dictionary<string, string> valuesDictionary)
        {
            int last = valuesDictionary.Count - 1;
            int i = 0;
            string line = string.Empty;
            //foreach (string key in rowDictionary.Keys)
            foreach (string header in headers)
            {
                // Quote all text. But if its already quoted, trim off quotes first
                line += "\"" + valuesDictionary[header].Trim('"') + "\"";
                line += (i != last) ? "," : Environment.NewLine;
                i++;
            }
            thisOutstream.Write(line);
        }
        #endregion
    }
}
