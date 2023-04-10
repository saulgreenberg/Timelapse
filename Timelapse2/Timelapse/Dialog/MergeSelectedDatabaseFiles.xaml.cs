using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Util;
using ToastNotifications.Messages.Error;
using File = Timelapse.Constant.File;
using Path = System.IO.Path;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for MergeSelectedDatabaseFiles.xaml
    /// </summary>
    public partial class MergeSelectedDatabaseFiles
    {
        private readonly string destinationDdbPath;
        private readonly SQLiteWrapper destinationDdb;

        private readonly List<string> sourceDdbFilePaths;

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

        private readonly string rootFolderPath;
        //private readonly string rootFolderName;

        // Tracks whether any changes to the database was made
        private bool IsAnyDataUpdated;

        public ObservableCollection<SourceFileInfo> ObservableDdbFileList { get; set; }
        public string DatabaseToLoad { get; set; } = string.Empty;
        public bool FoundInvalidFiles { get; set; } = false;

        #region Constructor, Loaded, Closing
        public MergeSelectedDatabaseFiles(Window owner, string destinationDdbPath, SQLiteWrapper destinationDdb) :
            base(owner)
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
            //this.rootFolderName = this.rootFolderPath.Split(Path.DirectorySeparatorChar).Last();

            // Each object in this list represents a file and its selection state
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
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.DialogResult = this.Token.IsCancellationRequested || this.IsAnyDataUpdated;
        }

        #endregion

        #region MergeButton Callback
        private async void MergeButton_Click(object sender, RoutedEventArgs e)
        {
            TryGenerateErrorIfAnySourceDdbFilesAreProblematic();


            // Set up progress indicators
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
            this.TextBlockFinalMessage.Text = "The merge operation was XXX";
            this.LabelBanner.Content = "Merge Databases XXX.";

            List<SourceFileInfo> mergedSourceFileInfos = new List<SourceFileInfo>();
            List<SourceFileInfo> unmergedSourceFileInfos = new List<SourceFileInfo>();
            foreach (SourceFileInfo sourceFileInfo in sourceFileInfos)
            {
                if (HasError(sourceFileInfo.DatabaseFileErrorsEnum))
                {
                    unmergedSourceFileInfos.Add(sourceFileInfo);
                }
                else
                {
                    mergedSourceFileInfos.Add(sourceFileInfo);
                }
            }

            int totalFiles = sourceFileInfos.Count;
            int mergedFiles = mergedSourceFileInfos.Count;
            int unmergedFiles = unmergedSourceFileInfos.Count;

            this.TextBlockFinalMessage.Text = mergedFiles == 0
                ? $"No files were merged.{Environment.NewLine}"
                : $"{mergedFiles} / {totalFiles} files were merged:{Environment.NewLine}{GenerateMergeFeedbackMessage(mergedSourceFileInfos)}";
            if (unmergedFiles > 0)
            {
                this.TextBlockFinalMessage.Text += $"{Environment.NewLine}{unmergedFiles} / {totalFiles} files were not merged for the indicated reasons:{Environment.NewLine}{GenerateMergeFeedbackMessage(unmergedSourceFileInfos)}";
            }

            // Show the Done button and hide the others
            this.MergeButton.Visibility = Visibility.Collapsed;
            this.CancelButton.Visibility = Visibility.Collapsed;
            this.DoneButton.Visibility = Visibility.Visible;


            //bool merged = false;

            //if (errorMessages.Warnings.Count == 1 && errorMessages.Warnings[0] == "Merge cancelled.")
            //{
            //    this.LabelBanner.Content = "Merge Cancelled.";
            //    this.TextBlockFinalMessage.Text = "The merge operation was cancelled";
            //}
            //else if (errorMessages.Errors.Count != 0)
            //{
            //    this.LabelBanner.Content = "Merge Databases Failed.";
            //    this.TextBlockFinalMessage.Text = "The merged database could not be created for the following reasons:";
            //    foreach (string error in errorMessages.Errors)
            //    {
            //        this.TextBlockFinalMessage.Text += $"{Environment.NewLine}\u2022 {error},";
            //    }
            //}
            //else if (errorMessages.Warnings.Count != 0)
            //{
            //    this.LabelBanner.Content = "Merge Databases Left Out Some Files.";
            //    this.TextBlockFinalMessage.Text = "The merged database left out some files for the following reasons:";
            //    foreach (string warning in errorMessages.Warnings)
            //    {
            //        this.TextBlockFinalMessage.Text += $"{Environment.NewLine}\u2022 {warning}";
            //    }

            //    this.TextBlockFinalMessage.Text += Environment.NewLine + Environment.NewLine;
            //    if (errorMessages.MergedFiles.Count == 0)
            //    {
            //        this.TextBlockFinalMessage.Text += $"{Environment.NewLine}No files were left to merge";
            //        // already has this value: merged = false;
            //    }
            //    else
            //    {
            //        merged = true;
            //    }
            //}
            //else
            //{
            //    this.LabelBanner.Content = "Databases merged.";
            //    merged = true;
            //}

            //if (merged)
            //{
            //    this.TextBlockFinalMessage.Text += "These database files were merged:";
            //    foreach (string file in errorMessages.MergedFiles)
            //    {
            //        this.TextBlockFinalMessage.Text += $"{Environment.NewLine}\u2022 {file}";
            //    }

            //    //this.TextBlockFinalMessage.Text += String.Format("{0}{0}The merged database is located in:", Environment.NewLine);
            //    //this.TextBlockFinalMessage.Text += $"{Environment.NewLine}\u2022 {this.DestinationddbFilePath}";
            //    this.DoneWithLoadButton.Visibility = Visibility.Visible;
            //    this.IsAnyDataUpdated = true;
            //}

            //if (errorMessages.BackupMessages.Any())
            //{
            //    foreach (string file in errorMessages.BackupMessages)
            //    {
            //        this.TextBlockFinalMessage.Text += $"{Environment.NewLine}{Environment.NewLine}{file}";
            //    }
            //}

        }
        #endregion
        private static async Task<List<SourceFileInfo>> MergeDatabasesAsync(SQLiteWrapper destinationDdb, string destinationDdbPath, List<SourceFileInfo> selectedSourceDdbFiles, IProgress<ProgressBarArguments> progress, CancellationTokenSource cancelTokenSource)
        {
            int sourceDdbCount = selectedSourceDdbFiles.Count;
            int i = 0;
            return await Task.Run(() =>
            {
                foreach (SourceFileInfo sourceFileInfo in selectedSourceDdbFiles) 
                {
                    // Show progress and check for cancel
                    progress.Report(new ProgressBarArguments((int)(i++ / (double)sourceDdbCount * 100.0),
                        $"Merging {sourceFileInfo.ShortPathDisplayName}. Please wait...",
                        "Merging...",
                        true, false));
                    if (cancelTokenSource.IsCancellationRequested)
                    {
                        return selectedSourceDdbFiles;
                    }
                    Task.Delay(1000);

                    // A. Checks to see if we can merge the file

                    // Check if its a valid readable database ddb file
                    sourceFileInfo.DatabaseFileErrorsEnum = FilesFolders.QuickCheckDatabaseFile(sourceFileInfo.FullPath);
                    if (HasError(sourceFileInfo.DatabaseFileErrorsEnum))
                    {
                        // Skip the merge on this file if it is problematic
                        continue;
                    }
                    // Now check if the templates are compatable
                    SQLiteWrapper sourceDdb = new SQLiteWrapper(sourceFileInfo.FullPath);
                    sourceFileInfo.DatabaseFileErrorsEnum = MergeDatabasesNew.CheckIfDatabaseTemplatesAreMergeCompatable(sourceDdb, destinationDdb);
                    if (HasError(sourceFileInfo.DatabaseFileErrorsEnum))
                    {
                        // Skip the merge on this file if the templates are problematic
                        continue;
                    }

                    string relativePathDifference =
                        FilesFolders.GetDifferenceBetweenPathAndSubPath(destinationDdbPath, sourceFileInfo.FullPath);
                    sourceFileInfo.DatabaseFileErrorsEnum = MergeDatabasesNew.RemoveEntriesFromDestinationDdbMatchingPath(destinationDdb, sourceFileInfo.FullPath, relativePathDifference);
                    if (HasError(sourceFileInfo.DatabaseFileErrorsEnum))
                    {
                        // Skip the merge on this file if the templates are problematic
                        continue;
                    }
                    // e. Do the merge
                    sourceFileInfo.DatabaseFileErrorsEnum = MergeDatabasesNew.MergeSourceIntoDestinationDdb(
                        destinationDdb, sourceFileInfo.FullPath, relativePathDifference);
                }
                return selectedSourceDdbFiles;

            }).ConfigureAwait(true);
        }

        #region Callbacks: Buttons and Checkboxes

        private void Selector_CheckChanged(object sender, RoutedEventArgs e)
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
            this.DatabaseToLoad = this.destinationDdbPath;
            this.DialogResult = true;
        }

        #endregion

        #region Private: Utilities

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

            string relativePathAndFileName =
                ddbFilePath.Replace(tdbPathWithoutFileName.TrimEnd(Path.DirectorySeparatorChar) + "\\", "");
            return relativePathAndFileName;
        }

        #endregion

        #region Private: Error management, including error messages

        private static bool HasError(DatabaseFileErrorsEnum databaseFileErrorsEnum)
        {
            return databaseFileErrorsEnum != DatabaseFileErrorsEnum.Ok 
                   && databaseFileErrorsEnum != DatabaseFileErrorsEnum.OkButOpenedWithAnOlderTimelapseVersion;
        }
        
        private string GenerateMergeFeedbackMessage(List<SourceFileInfo> sourceFileInfos)
        {
            string message = string.Empty;
            foreach (SourceFileInfo sourceFileInfo in sourceFileInfos)
            {
                if (sourceFileInfo.DatabaseFileErrorsEnum == DatabaseFileErrorsEnum.Ok ||
                    sourceFileInfo.DatabaseFileErrorsEnum == DatabaseFileErrorsEnum.OkButOpenedWithAnOlderTimelapseVersion)
                {
                    message += $" \u2713 {sourceFileInfo.ShortPathDisplayName}{Environment.NewLine}";
                }
                else
                {
                    message += $" \u2717 {sourceFileInfo.ShortPathDisplayName}{Environment.NewLine}";
                    message +=
                        $"       \u2022 {GenerateMessageFromDatabaseFileErrorsEnum(sourceFileInfo.DatabaseFileErrorsEnum)}{Environment.NewLine}";
                }
            }
            return message;
        }

        private string GenerateMessageFromDatabaseFileErrorsEnum(DatabaseFileErrorsEnum databaseFileErrorsEnum)
        {
            switch (databaseFileErrorsEnum)
            {
                case DatabaseFileErrorsEnum.Ok:
                case DatabaseFileErrorsEnum.OkButOpenedWithAnOlderTimelapseVersion:
                    return "Merge succeeded";
                case DatabaseFileErrorsEnum.NotATimelapseFile:
                case DatabaseFileErrorsEnum.InvalidDatabase:
                    return "The file does not contain a valid .ddb database.";
                case DatabaseFileErrorsEnum.PreVersion2300:
                case DatabaseFileErrorsEnum.UTCOffsetTypeExistsInUpgradedVersion:
                    return "The database needs to be updated.";
                case DatabaseFileErrorsEnum.FileInSystemOrHiddenFolder:
                    return "The file cannot be located in a system or hidden folder.";
                case DatabaseFileErrorsEnum.FileInRootDriveFolder:
                    return "The file cannot be located in a top level root drive.";
                case DatabaseFileErrorsEnum.DoesNotExist:
                    return "The file does not exist.";
                case DatabaseFileErrorsEnum.PathTooLong:
                    return "The file path length exceeds the Windows maximum size.";
                // These results are used during merge testing for incompatabilities
                case DatabaseFileErrorsEnum.TemplateElementsDiffer:
                    return "The file's template is not compatable with the destination database (their data fields differ).";
                case DatabaseFileErrorsEnum.TemplateElementsSameButOrderDifferent:
                    return "The templates differ (data fields are the same but their order differs).";
                case DatabaseFileErrorsEnum.DetectionCategoriesDiffer:
                    return "Recognition data differs (detection categories differ).";
                case DatabaseFileErrorsEnum.ClassificationCategoriesDiffer:
                    return "Recognition data differs (classification categories differ).";
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
                LabelBanner.Content = "Warning: No database (.ddb) files are available to merge.";
                TextBlockFinalMessage.Text =
                    $"Potential database files must be located in sub-folders contained by this root folder:{Environment.NewLine}";
                TextBlockFinalMessage.Text += $"    \u2022 {rootFolderPath}{Environment.NewLine}{Environment.NewLine}";
                TextBlockFinalMessage.Text += "Timelapse searched those sub-folders, and no database files were found.";
                MergeButton.IsEnabled = false;
                return true;
            }
            return false;
        }



        // Check if the path of the potential source.ddb file is too long.
        // If so, display an error message and return true
        private bool TryGenerateErrorIfLongDdbFileNamesSelected()
        {
            List<string> filesWhosePathsAreTooLong = new List<string>();
            foreach (SourceFileInfo fileObject in this.ObservableDdbFileList)
            {
                if (fileObject.IsSelected && fileObject.PathTooLong)
                {
                    filesWhosePathsAreTooLong.Add(fileObject.ShortPathDisplayName);
                }
            }

            if (filesWhosePathsAreTooLong.Count == 0)
            {
                return false;
            }

            // We have some long files paths. Generate an error message.
            this.ListboxFileDatabases.Visibility = Visibility.Collapsed;
            ScrollerTextBlockFinalMessage.Visibility = Visibility.Visible;
            LabelBanner.Content = "Warning: Merge not done. Some of your database (.ddb) file paths are too long.";
            TextBlockFinalMessage.Text =
                $"Your selection includes files whose paths too long (this is a Windows restriction):{Environment.NewLine}";
            foreach (string shortenedPath in filesWhosePathsAreTooLong)
            {
                TextBlockFinalMessage.Text += $"    \u2022 {shortenedPath}{Environment.NewLine}";
            }

            TextBlockFinalMessage.Text +=
                $"{Environment.NewLine}Try again after shortening those file paths by:{Environment.NewLine}";
            TextBlockFinalMessage.Text +=
                $"    \u2022 moving your root folder higher up the folder hierarchy,{Environment.NewLine}"
                + $"    \u2022 shortening your folder names,{Environment.NewLine}"
                + $"    \u2022 removing unneeded sub-folders from the path.{Environment.NewLine}";
            TextBlockFinalMessage.Text +=
                $"Note: Path name changes may result in Timelapse not being able to locate your images.{Environment.NewLine}";
            TextBlockFinalMessage.Text +=
                "          If that happens, you can locate them using Timelapse's 'Edit|Try to find...' facility.";
            MergeButton.IsEnabled = false;
            return true;
        }

        private bool TryGenerateErrorIfAnySourceDdbFilesAreProblematic()
        {
            foreach (SourceFileInfo fileObject in this.ObservableDdbFileList)
            {
                if (fileObject.IsSelected)
                {
                    DatabaseFileErrorsEnum sourceDdbErrorsEnum = FilesFolders.QuickCheckDatabaseFile(fileObject.FullPath);
                    Debug.Print(fileObject.ShortPathDisplayName + ": " + sourceDdbErrorsEnum);
                }
            }
            return true;
        }
        #endregion
    }

    #region Class: SourceFileInfo
    public class SourceFileInfo
    {
        public bool IsSelected { get; set; }
        public string FullPath { get; set; }
        public string RelativePathIncludingFileName { get; set; }

        public DatabaseFileErrorsEnum DatabaseFileErrorsEnum { get; set; }

        private const int maxLength = 92;

        private const int prefixLength = 38;

        private const int suffixLength = 53;


        // Check for very long file names, and generate a shortened version of it
        public bool PathTooLong => IsCondition.IsPathLengthTooLong(this.FullPath, FilePathTypeEnum.DDB);
        public string ShortPathDisplayName =>
            RelativePathIncludingFileName.Length < maxLength
                ? this.RelativePathIncludingFileName
                : this.RelativePathIncludingFileName.Substring(0, prefixLength) + " \u2026 "
                + this.RelativePathIncludingFileName.Substring(this.RelativePathIncludingFileName.Length - suffixLength);
    }
    #endregion
}
