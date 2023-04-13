using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Util;
using Path = System.IO.Path;

namespace Timelapse.Dialog
{
    /// <summary>
    /// MergeSelectedDatabaseFiles
    /// - Raises a dialog box showing all databases under the root folder but not in it,
    ///   where the user can select some of them to merge into the currently opened (destination) database 
    /// </summary>
    public partial class MergeSelectedDatabaseFiles
    {
        // Tracks the found ddb files and their selection state
        public ObservableCollection<SourceFileInfo> ObservableDdbFileList { get; }

        // Returns the currently selected files and info about them
        private List<SourceFileInfo> selectedDdbFiles
        {
            get
            {
                List<SourceFileInfo> _selectedDdbFile = new List<SourceFileInfo>();
                foreach (SourceFileInfo ddbObject in this.ObservableDdbFileList)
                {
                    if (ddbObject.IsSelected)
                    {
                        _selectedDdbFile.Add(ddbObject);
                    }
                }

                return _selectedDdbFile;
            }
        }

        private readonly string destinationDdbPath;
        private readonly SQLiteWrapper destinationDdb;
        public List<string> sourceDdbFilePaths;
        private readonly string rootFolderPath;
        private bool IsAnyDataUpdated;

        #region Constructor, Loaded, Closing
        public MergeSelectedDatabaseFiles(Window owner, string destinationDdbPath, SQLiteWrapper destinationDdb) : base(owner)
        {
            InitializeComponent();

            this.destinationDdbPath = destinationDdbPath;
            this.destinationDdb = destinationDdb;

            this.rootFolderPath = Path.GetDirectoryName(this.destinationDdbPath);
            if (this.rootFolderPath == null)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(rootFolderPath));
                return;
            }

            // Each object in this list represents a file, its selection state, and other related info about the fil
            this.ObservableDdbFileList = new ObservableCollection<SourceFileInfo>();

            // Get all the ddb files contained by subfolders under the rootFolder, excluding those directly in the root folder
            this.sourceDdbFilePaths = FilesFolders.GetAllFilesInFoldersAndSubfoldersMatchingPattern(this.rootFolderPath,
                "*" + Constant.File.FileDatabaseFileExtension, true, true, null);
            this.sourceDdbFilePaths.RemoveAll(Item => Path.GetDirectoryName(Item) == this.rootFolderPath);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Abort if no candidate source ddb files were found
            if (TryGenerateErrorIfNoDdbSourceFilesExists())
            {
                return;
            }

            // Set up a progress handler that will update the progress bar
            InitalizeProgressHandler(BusyCancelIndicator);

            // We have at least one or more valid .ddb files. Load them up into the list
            foreach (string ddbFile in sourceDdbFilePaths)
            {
                ObservableDdbFileList.Add(new SourceFileInfo
                {
                    IsSelected = false,
                    FullPath = ddbFile,
                    RelativePathIncludingFileName = GetRelativePathAndFileName(destinationDdbPath, ddbFile)
                });
            }
            DataContext = this;
            this.EnableOrDisableMergeButton();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.DialogResult = this.Token.IsCancellationRequested || this.IsAnyDataUpdated;
        }

        #endregion

        #region MergeButton Callback
        // Start the merge process
        private async void MergeButton_Click(object sender, RoutedEventArgs e)
        {
            // Before doing the merge, check if one or more databases are nested in a common folder/subfolder.
            // If so, warn the user and give them the option to abort.
            string warningMessage = GenerateTextMessageIfDatabasesAreInNestedFolders();
            
            if (false == string.IsNullOrWhiteSpace(warningMessage))
            {
                MergingDatabaseWarningAsDuplicateEntriesPossible messageBox =
                    new MergingDatabaseWarningAsDuplicateEntriesPossible(this, warningMessage);
                if (false == messageBox.ShowDialog())
                {
                    // The user has decided not to merge the databases
                    return;
                }
            }

            // Start the merge process by first setting up progress indicators
            Mouse.OverrideCursor = Cursors.Wait;
            this.BusyCancelIndicator.EnableForMerging(true);

            // Try to merge the selected databases into destination ddb file.
            // .ddb files found in a Backup folder are ignored
            List<SourceFileInfo> sourceFileInfos = await MergeDatabasesAsync(
                this.destinationDdb, this.destinationDdbPath,
                this.selectedDdbFiles,
                this.Progress, GlobalReferences.CancelTokenSource).ConfigureAwait(true);

            // Turn off progress indicators
            this.BusyCancelIndicator.EnableForSelection(false);
            this.BusyCancelIndicator.Reset();
            Mouse.OverrideCursor = null;

            // Show the result;
            this.ListboxFileDatabases.Visibility = Visibility.Collapsed;
            this.ScrollerTextBlockFinalMessage.Visibility = Visibility.Visible;
            this.ButtonSelectAll.Visibility = Visibility.Collapsed;
            this.ButtonSelectNone.Visibility = Visibility.Collapsed;
            this.LabelBanner.Content = "Merge results";

            // As part of the above merge attempt, each SourceFileInfo records if the merge
            // was successful, or if the merge failed as well as why it failed.
            List<SourceFileInfo> mergedSourceFileInfos = new List<SourceFileInfo>();
            List<SourceFileInfo> unmergedSourceFileInfos = new List<SourceFileInfo>();
            foreach (SourceFileInfo sourceFileInfo in sourceFileInfos)
            {
                if (HasError(sourceFileInfo.DatabaseFileError))
                {
                    unmergedSourceFileInfos.Add(sourceFileInfo);
                }
                else
                {
                    mergedSourceFileInfos.Add(sourceFileInfo);
                }
                this.IsAnyDataUpdated = mergedSourceFileInfos.Any();
            }

            int totalFiles = sourceFileInfos.Count;
            int mergedFiles = mergedSourceFileInfos.Count;
            int unmergedFiles = unmergedSourceFileInfos.Count;

            this.TextBlockFinalMessage.Text = mergedFiles == 0
                ? $"No files were merged.{Environment.NewLine}"
                : $"{mergedFiles} / {totalFiles} files were merged:{Environment.NewLine}{GenerateMergeFeedbackMessage(mergedSourceFileInfos)}";
            if (unmergedFiles > 0)
            {
                this.TextBlockFinalMessage.Text +=
                    $"{Environment.NewLine}{unmergedFiles} / {totalFiles} files were not merged for the indicated reasons:{Environment.NewLine}{GenerateMergeFeedbackMessage(unmergedSourceFileInfos)}";
            }

            // Show the Done button and hide the other buttons
            this.MergeButton.Visibility = Visibility.Collapsed;
            this.CancelButton.Visibility = Visibility.Collapsed;
            this.DoneButton.Visibility = Visibility.Visible;
        }
        #endregion

        // Actually do the merge of each of the selected files
        // Update whether it succeeded or failed in the sourceFileInfo.DatabaseFileError structure
        private static async Task<List<SourceFileInfo>> MergeDatabasesAsync(SQLiteWrapper destinationDdb,
            string destinationDdbPath, List<SourceFileInfo> selectedSourceDdbFiles,
            IProgress<ProgressBarArguments> progress, CancellationTokenSource cancelTokenSource)
        {
            int sourceDdbCount = selectedSourceDdbFiles.Count;
            int i = 0;
            bool cancelled = false;
            return await Task.Run(() =>
            {
                foreach (SourceFileInfo sourceFileInfo in selectedSourceDdbFiles)
                {
                    if (cancelled)
                    {
                        sourceFileInfo.DatabaseFileError = DatabaseFileErrorsEnum.Cancelled;
                    }

                    // Show progress and check for cancel
                    progress.Report(new ProgressBarArguments((int)(i++ / (double)sourceDdbCount * 100.0),
                        $"Merging {sourceFileInfo.ShortPathDisplayName}. Please wait...",
                        "Merging...",
                        true, false));
                    if (cancelTokenSource.IsCancellationRequested)
                    {
                        cancelled = true;
                        sourceFileInfo.DatabaseFileError = DatabaseFileErrorsEnum.Cancelled;
                        continue;
                    } 

                    // A. Checks to see if we can merge the file

                    // Check if its a valid readable database ddb file
                    sourceFileInfo.DatabaseFileError =
                        FilesFolders.QuickCheckDatabaseFile(sourceFileInfo.FullPath);
                    if (HasError(sourceFileInfo.DatabaseFileError))
                    {
                        // Skip the merge on this file if it is problematic
                        continue;
                    }

                    // Now check if the templates are compatable
                    SQLiteWrapper sourceDdb = new SQLiteWrapper(sourceFileInfo.FullPath);
                    sourceFileInfo.DatabaseFileError =
                        MergeDatabases.CheckIfDatabaseTemplatesAreMergeCompatable(sourceDdb, destinationDdb);
                    if (HasError(sourceFileInfo.DatabaseFileError))
                    {
                        // Skip the merge on this file if the templates are problematic
                        continue;
                    }

                    string relativePathDifference =
                        FilesFolders.GetDifferenceBetweenPathAndSubPath(destinationDdbPath, sourceFileInfo.FullPath);
                    sourceFileInfo.DatabaseFileError =
                        MergeDatabases.RemoveEntriesFromDestinationDdbMatchingPath(destinationDdb,
                            sourceFileInfo.FullPath, relativePathDifference);
                    if (HasError(sourceFileInfo.DatabaseFileError))
                    {
                        // Skip the merge on this file if the templates are problematic
                        continue;
                    }

                    // e. Do the merge
                    sourceFileInfo.DatabaseFileError = MergeDatabases.MergeSourceIntoDestinationDdb(
                        destinationDdb, sourceFileInfo.FullPath, relativePathDifference);
                }
                return selectedSourceDdbFiles;

            }).ConfigureAwait(true);
        }

        #region Callbacks: Buttons and Checkboxes
        private void Selector_CheckChanged(object sender, RoutedEventArgs e)
        {
            EnableOrDisableMergeButton();
        }

        private void ButtonSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (SourceFileInfo ddbObject in this.ObservableDdbFileList)
            {
                ddbObject.IsSelected = true;
            }
            this.ListboxFileDatabases.Items.Refresh();
            this.EnableOrDisableMergeButton();
        }

        private void ButtonSelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (SourceFileInfo ddbObject in this.ObservableDdbFileList)
            {
                ddbObject.IsSelected = false;
            }
            this.ListboxFileDatabases.Items.Refresh();
            this.EnableOrDisableMergeButton();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void DoneWithLoadButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
        #endregion

        #region Private: Error management, including error messages
        // Return true iff databaseFileError indicates the presence of an error
        private static bool HasError(DatabaseFileErrorsEnum databaseFileError)
        {
            return databaseFileError != DatabaseFileErrorsEnum.Ok
                   && databaseFileError != DatabaseFileErrorsEnum.OkButOpenedWithAnOlderTimelapseVersion;
        }

        // Generate a displayable message listing the merge results,
        // separated into successfuly merged files vs failed merges and the reason why it failed
        private string GenerateMergeFeedbackMessage(List<SourceFileInfo> sourceFileInfos)
        {
            string message = string.Empty;
            foreach (SourceFileInfo sourceFileInfo in sourceFileInfos)
            {
                if (sourceFileInfo.DatabaseFileError == DatabaseFileErrorsEnum.Ok ||
                    sourceFileInfo.DatabaseFileError ==
                    DatabaseFileErrorsEnum.OkButOpenedWithAnOlderTimelapseVersion)
                {
                    message += $" \u2713 {sourceFileInfo.ShortPathDisplayName}{Environment.NewLine}";
                }
                else
                {
                    message += $" \u2717 {sourceFileInfo.ShortPathDisplayName}{Environment.NewLine}";
                    message +=
                        $"       \u2022 {GenerateMessageFromDatabaseFileErrorsEnum(sourceFileInfo.DatabaseFileError)}{Environment.NewLine}";
                }
            }
            return message;
        }

        // See above: this generates the actual text for each databaseFileError type
        private string GenerateMessageFromDatabaseFileErrorsEnum(DatabaseFileErrorsEnum databaseFileError)
        {
            switch (databaseFileError)
            {
                case DatabaseFileErrorsEnum.Ok:
                case DatabaseFileErrorsEnum.OkButOpenedWithAnOlderTimelapseVersion:
                    return "Merge succeeded";
                case DatabaseFileErrorsEnum.NotATimelapseFile:
                case DatabaseFileErrorsEnum.InvalidDatabase:
                    return "The file does not contain a valid .ddb database.";
                case DatabaseFileErrorsEnum.PreVersion2300:
                case DatabaseFileErrorsEnum.UTCOffsetTypeExistsInUpgradedVersion:
                    return "The file needs to be updated. Select File|Upgrade Timelapse files... to upgrade it.";
                case DatabaseFileErrorsEnum.FileInSystemOrHiddenFolder:
                    return "The file cannot be located in a system or hidden folder. Move it elsewhere.";
                case DatabaseFileErrorsEnum.FileInRootDriveFolder:
                    return "The file cannot be located in a top level root drive. Move it elsewhere.";
                case DatabaseFileErrorsEnum.Cancelled:
                    return "The merging operation was cancelled by the user.";
                case DatabaseFileErrorsEnum.DoesNotExist:
                    return "The file does not exist.";
                case DatabaseFileErrorsEnum.PathTooLong:
                    return "The file path length exceeds the Windows maximum size. Shorten the path name.";
                // These results are used during merge testing for incompatabilities
                case DatabaseFileErrorsEnum.TemplateElementsDiffer:
                    return "The file's template is not compatable with the destination database (their data fields differ).";
                case DatabaseFileErrorsEnum.TemplateElementsSameButOrderDifferent:
                    return "The templates differ (data fields are the same but their order differs).";
                case DatabaseFileErrorsEnum.DetectionCategoriesDiffer:
                    return "The file's recognition data has different detection categories.";
                case DatabaseFileErrorsEnum.ClassificationCategoriesDiffer:
                    return "The file's recognition data has different classification categories.";
                default:
                    return "Unknown error";
            }
        }

        // Check if there are any candidate sourceDdb files.
        // If not, display an error message and return true
        private bool TryGenerateErrorIfNoDdbSourceFilesExists()
        {
            // Check if any candidate source ddb files were found
            if (sourceDdbFilePaths == null || this.sourceDdbFilePaths.Count == 0)
            {
                // Show error message
                ListboxFileDatabases.Visibility = Visibility.Collapsed;
                ScrollerTextBlockFinalMessage.Visibility = Visibility.Visible;
                this.ButtonSelectAll.Visibility = Visibility.Collapsed;
                this.ButtonSelectNone.Visibility = Visibility.Collapsed;
                LabelBanner.Content = "Warning: No database (.ddb) files are available to merge.";
                TextBlockFinalMessage.Text =
                    $"Potential database files must be located in sub-folders contained within this root folder:{Environment.NewLine}";
                TextBlockFinalMessage.Text += $"    \u2022 {rootFolderPath}{Environment.NewLine}{Environment.NewLine}";
                TextBlockFinalMessage.Text += "Timelapse searched those sub-folders, and no database files were found.";
                MergeButton.IsEnabled = false;
                return true;
            }

            return false;
        }

        // Generate a displayable text list showing which of the selected databases, if any, are located in nested folders.
        // This is done because nested folders will likely have duplicate entires.
        // This can be used as a test method: If the returned message is empty, then no folders are nested.
        private string GenerateTextMessageIfDatabasesAreInNestedFolders()
        {
            string matchingMessage = String.Empty;
            // Collect the relative paths of the selected Ddbs
            StringComparer comparer = StringComparer.OrdinalIgnoreCase;
            var descendingComparer = Comparer<string>.Create((x, y) => comparer.Compare(y, x));

            SortedList<string, string> relativePaths = new SortedList<string, string>(descendingComparer);
            foreach (SourceFileInfo fileObject in this.ObservableDdbFileList)
            {
                if (fileObject.IsSelected)
                {
                    relativePaths.Add(fileObject.RelativePathIncludingFileName,
                        Path.GetDirectoryName(fileObject.RelativePathIncludingFileName));
                }
            }

            bool addLine = false;
            // Iterate through the list
            for (int i = 0; i < relativePaths.Count; i++)
            {
                KeyValuePair<string, string> relativePath = relativePaths.ElementAt(i);

                List<KeyValuePair<string, string>> matches = relativePaths
                    .Where(kvp => kvp.Value.StartsWith(relativePath.Value + "\\"))
                    .ToList();
                int matchCount = matches.Count;
                if (matchCount > 0)
                {
                    if (addLine)
                    {
                        matchingMessage += Environment.NewLine + Environment.NewLine;
                    }
                    addLine = true;
                    matchingMessage += $"'{relativePath.Value}':";
                    matchingMessage += $"{Environment.NewLine} - {relativePath.Key}";
                    foreach (KeyValuePair<string, string> match in matches)
                    {
                        matchingMessage += $"{Environment.NewLine} - {match.Key}";
                    }
                }

                i += matchCount;
            }
            return matchingMessage;
        }
        #endregion
        
        #region Utilities
        // Enable or Disable the merge button, depending upon whether any databases are selected
        private void EnableOrDisableMergeButton()
        {
            foreach (SourceFileInfo ddbObject in ObservableDdbFileList)
            {
                if (ddbObject.IsSelected)
                {
                    this.MergeButton.IsEnabled = true;
                    return;
                }
            }
            this.MergeButton.IsEnabled = false;
        }
        
        // Return the relative path (including file name) to the ddb file. 
        // For example if the template is in C:\foo\template.tdb and the ddb file is in C:\foo\bar\data.ddb,
        // it will return bar\data.ddb
        private static string GetRelativePathAndFileName(string tdbPath, string ddbFilePath)
        {
            string tdbPathWithoutFileName = Path.GetDirectoryName(tdbPath);
            if (tdbPathWithoutFileName == null)
            {
                // Shouldn't happen, i.e., Only occurs if its the drive e.g., C://
                // Not sure if setting the tdbPath to the drive letter will work, though.
                TracePrint.NullException(nameof(tdbPathWithoutFileName));
                tdbPathWithoutFileName = Path.GetPathRoot(tdbPath);
            }
            return ddbFilePath.Replace(tdbPathWithoutFileName.TrimEnd(Path.DirectorySeparatorChar) + "\\", "");
        }
        #endregion

        #region Class: SourceFileInfo
        // Information about each DDB Source File is kept here, including whether it selected
        public class SourceFileInfo
        {
            public bool IsSelected { get; set; }
            public string FullPath { get; set; }
            public string RelativePathIncludingFileName { get; set; }

            public DatabaseFileErrorsEnum DatabaseFileError { get; set; }

            private const int maxLength = 92;
            private const int prefixLength = 38;
            private const int suffixLength = 53;

            // Check for very long file names
            public bool PathTooLong => IsCondition.IsPathLengthTooLong(this.FullPath, FilePathTypeEnum.DDB);

            // Generate a shortened version of the file name to display in the available space
            public string ShortPathDisplayName =>
                RelativePathIncludingFileName.Length < maxLength
                    ? this.RelativePathIncludingFileName
                    : this.RelativePathIncludingFileName.Substring(0, prefixLength) + " \u2026 "
                    + this.RelativePathIncludingFileName.Substring(this.RelativePathIncludingFileName.Length -
                                                                   suffixLength);
        }

        #endregion
    }
}
