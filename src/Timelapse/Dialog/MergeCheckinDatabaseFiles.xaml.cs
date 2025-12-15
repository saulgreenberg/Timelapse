using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Timelapse.Constant;
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
    /// MergeCheckinDatabaseFiles
    /// - Raises a dialog box showing all databases under the root folder but not in it,
    ///   where the user can select some of them to merge into the currently opened (destination) database 
    /// </summary>
    public partial class MergeCheckinDatabaseFiles
    {
        #region Variables
        // Tracks the found ddb files and their selection state
        public ObservableCollection<SourceFileInfo> ObservableDdbFileList { get; }

        // Returns the currently selected files and info about them
        private List<SourceFileInfo> SelectedDdbFiles
        {
            get
            {
                List<SourceFileInfo> _selectedDdbFile = [];
                foreach (SourceFileInfo ddbObject in ObservableDdbFileList)
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
        private readonly FileDatabase fileDatabase;
        public List<string> sourceDdbFilePaths;
        private bool IsAnyDataUpdated;
        #endregion

        #region Constructor, Loaded, Closing
        public MergeCheckinDatabaseFiles(TimelapseWindow owner, string destinationDdbPath, SQLiteWrapper destinationDdb, FileDatabase fileDatabase) : base(owner)
        {
            InitializeComponent();
            Owner = owner;
            this.destinationDdbPath = destinationDdbPath;
            this.destinationDdb = destinationDdb;
            this.fileDatabase = fileDatabase;

            string rootFolderPath = Path.GetDirectoryName(this.destinationDdbPath);
            if (rootFolderPath == null)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(rootFolderPath));
                return;
            }

            // Each object in this list represents a file, its selection state, and other related info about the fil
            ObservableDdbFileList = [];

            // Get all the ddb files contained by subfolders under the rootFolder, excluding those directly in the root folder
            sourceDdbFilePaths = FilesFolders.GetAllFilesInFoldersAndSubfoldersMatchingPattern(rootFolderPath,
                "*" + File.FileDatabaseFileExtension, true, true, null);
            sourceDdbFilePaths.RemoveAll(Item => Path.GetDirectoryName(Item) == rootFolderPath);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            this.Message.BuildContentFromProperties();
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

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
                ObservableDdbFileList.Add(new()
                {
                    IsSelected = false,
                    FullPath = ddbFile,
                    RelativePathIncludingFileName = GetRelativePathAndFileName(destinationDdbPath, ddbFile)
                });
            }
            DataContext = this;
            EnableOrDisableCheckInButton();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            DialogResult = Token.IsCancellationRequested || IsAnyDataUpdated;
        }

        #endregion

        #region Do the Merge
        // Actually do the merge of each of the selected files
        // Update whether it succeeded or failed in the sourceFileInfo.DatabaseFileError structure
        private static async Task<List<SourceFileInfo>> MergeDatabasesAsync(SQLiteWrapper destinationDdb,
            string destinationDdbPath, List<SourceFileInfo> selectedSourceDdbFiles, FileDatabase fileDatabase,
            IProgress<ProgressBarArguments> progress, CancellationTokenSource cancelTokenSource)
        {
            int sourceDdbCount = selectedSourceDdbFiles.Count;
            int i = 0;
            bool cancelled = false;
            return await Task.Run(() =>
            {
                bool atLeastOneMergeSucceeded = false;
                foreach (SourceFileInfo sourceFileInfo in selectedSourceDdbFiles)
                {
                    if (cancelled)
                    {
                        sourceFileInfo.DatabaseFileError = DatabaseFileErrorsEnum.Cancelled;
                    }

                    // Show progress and check for cancel
                    progress.Report(new((int)(i++ / (double)sourceDdbCount * 100.0),
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
                    // TODO: MAYBE return a more specific error message if the error is in the metadata?
                    SQLiteWrapper sourceDdb = new(sourceFileInfo.FullPath);

                    // TODO: Check if the sourceDdb needs upgrading!!!
                    int levelsToIgnore = FilesFolders.GetDifferenceBetweenPathAndSubPath(destinationDdbPath, sourceFileInfo.FullPath).Split(Path.DirectorySeparatorChar).Length;
                    sourceFileInfo.DatabaseFileError =
                        MergeDatabases.CheckIfDatabaseTemplatesAreMergeCompatable(sourceDdb, destinationDdb, levelsToIgnore);
                    if (HasError(sourceFileInfo.DatabaseFileError))
                    {
                        // Skip the merge on this file if the templates are problematic
                        continue;
                    }

                    // TODO: The next few steps can be problematic if the Merge fails, as we just deleted all these entries!!!
                    // TODO: TEST THIS. Maybe we can do this AFTER the query has been generated in the MergeSourceIntoDestinationDdb?

                    string relativePathDifference =
                        FilesFolders.GetDifferenceBetweenPathAndSubPath(destinationDdbPath, sourceFileInfo.FullPath);

                    sourceFileInfo.DatabaseFileError =
                        MergeDatabases.RemoveEntriesFromDestinationDdbMatchingPath(destinationDdb, relativePathDifference);
                    if (HasError(sourceFileInfo.DatabaseFileError))
                    {
                        // Skip the merge on this file if the templates are problematic
                        continue;
                    }

                    // Remove the metadata entries from the destination ddb.
                    sourceFileInfo.DatabaseFileError =
                        MergeDatabases.RemoveMetadataEntriesFromDestinationDdbMatchingPath(destinationDdb, relativePathDifference, levelsToIgnore);
                    if (HasError(sourceFileInfo.DatabaseFileError))
                    {
                        // Skip the merge on this file if the templates are problematic
                        continue;
                    }

                    // Try updating the IDs of the child if the IDs are really large
                    // IDs can be up to Long.MaxValue, we really don't want to get close to that so we try to restrict it to around Int32.MaxValue
                    // This tries to mitigate the issue of multiple merges producing very large IDs that are close to the maximum supported by SQLite.
                    if (true)// && srcIDDmaxValue > Int32.MaxValue)
                    {
                        progress.Report(new((int)(i++ / (double)sourceDdbCount * 100.0),
                            $"Doing child database maintenance before merging. Please wait...",
                            "Doing database maintenance...",
                            false, true));
                        FileDatabase sourceFileDatabase = new(sourceFileInfo.FullPath, false);
                        sourceFileDatabase.ResetIDsAndVacuum();
                        progress.Report(new((int)(i++ / (double)sourceDdbCount * 100.0),
                            $"Merging {sourceFileInfo.ShortPathDisplayName}. Please wait...",
                            "Merging...",
                            true, false));
                        if (cancelTokenSource.IsCancellationRequested)
                        {
                            cancelled = true;
                            sourceFileInfo.DatabaseFileError = DatabaseFileErrorsEnum.Cancelled;
                            continue;
                        }
                    }

                    // g. Do the merge
                    sourceFileInfo.DatabaseFileError = MergeDatabases.MergeSourceIntoDestinationDdb(destinationDdb, sourceFileInfo.FullPath, relativePathDifference, levelsToIgnore);

                    // h. Update the detection and classification category tables
                    // The above may have altered the two category dictionaries, so lets update them
                    fileDatabase.detectionCategoriesDictionary = null;
                    fileDatabase.CreateDetectionCategoriesDictionaryIfNeeded();
                    fileDatabase.classificationCategoriesDictionary = null;
                    fileDatabase.CreateClassificationCategoriesDictionaryIfNeeded();
                    fileDatabase.classificationDescriptionsDictionary = null;
                    fileDatabase.CreateClassificationDescriptionsDictionaryIfNeeded();


                    atLeastOneMergeSucceeded = true;
                }
                // Rebuild the dataTable ID and the detection detection ID to start at 1
                // then Vacuum it to reclaim space if needed
                // Try updating the IDs of the File databases if the IDs are really large.
                // IDs can be up to Long.MaxValue, we really don't want to get close to that so we try to restrict it to around Int32.MaxValue
                // This tries to mitigate the issue of multiple merges producing very large IDs that are close to the maximum supported by SQLite.

                if (fileDatabase != null && atLeastOneMergeSucceeded)
                {
                    if (true)// && destIDmaxValue > Int32.MaxValue)
                    {
                        progress.Report(new((int)(i++ / (double)sourceDdbCount * 100.0),
                            $"Doing database maintenance after the merge. Please wait...",
                            "Doing database maintenance...",
                            false, true));
                        fileDatabase.ResetIDsAndVacuum();
                    }
                }

                return selectedSourceDdbFiles;

            }).ConfigureAwait(true);
        }
        #endregion

        #region CheckInButton Callback
        // Start the merge process
        private async void CheckInButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await CheckInButton_ClickAsync();
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task CheckInButton_ClickAsync()
        {
            // Before doing the merge, check if one or more databases are nested in a common folder/subfolder.
            // If so, warn the user and give them the option to abort.
            string warningMessage = GenerateTextMessageIfDatabasesAreInNestedFolders();

            if (false == string.IsNullOrWhiteSpace(warningMessage))
            {
                MergeDatabaseWarningAsDuplicateEntriesPossible messageBox = new(this, warningMessage);
                if (false == messageBox.ShowDialog())
                {
                    // The user has decided not to merge the databases
                    return;
                }
            }

            // Start the merge process by first setting up progress indicators
            Mouse.OverrideCursor = Cursors.Wait;
            BusyCancelIndicator.EnableForMerging(true);

            // Create a backup if needed before attempting doing the merge;
            fileDatabase.CreateBackupIfNeeded();

            // Try to merge the selected databases into destination ddb file.
            // .ddb files found in a Backup folder are ignored
            List<SourceFileInfo> sourceFileInfos = await MergeDatabasesAsync(
                destinationDdb, destinationDdbPath,
                SelectedDdbFiles, fileDatabase,
                Progress, GlobalReferences.CancelTokenSource).ConfigureAwait(true);

            //// Update the detection and classification category tables
            //// The above may have altered the two category dictionaries, so lets update them
            //this.fileDatabase.detectionCategoriesDictionary = null;
            //this.fileDatabase.CreateDetectionCategoriesDictionaryIfNeeded();
            //this.fileDatabase.classificationCategoriesDictionary = null;
            //this.fileDatabase.CreateClassificationCategoriesDictionaryIfNeeded();

            // Turn off progress indicators
            BusyCancelIndicator.EnableForSelection(false);
            BusyCancelIndicator.Reset(false);
            Mouse.OverrideCursor = null;

            // Show the result;
            ListboxFileDatabases.Visibility = Visibility.Collapsed;
            FinalMessageScrollViewer.Visibility = Visibility.Visible;
            ButtonSelectAll.Visibility = Visibility.Collapsed;
            ButtonSelectNone.Visibility = Visibility.Collapsed;
            ResultsBanner.Text = "Results";

            // As part of the above merge attempt, each SourceFileInfo records if the merge
            // was successful, or if the merge failed as well as why it failed.
            List<SourceFileInfo> mergedSourceFileInfos = [];
            List<SourceFileInfo> unmergedSourceFileInfos = [];
            foreach (SourceFileInfo sourceFileInfo in sourceFileInfos)
            {
                if (HasError(sourceFileInfo.DatabaseFileError))
                {
                    unmergedSourceFileInfos.Add(sourceFileInfo);
                }
                else
                {
                    mergedSourceFileInfos.Add(sourceFileInfo);
                    fileDatabase.ImageSet.Log +=
                        $"{Environment.NewLine}{DateTime.Now.ToShortDateString()} {DateTime.Now.ToShortTimeString()}: Checked in:     {sourceFileInfo.RelativePathIncludingFileName}";
                }
                IsAnyDataUpdated = mergedSourceFileInfos.Count != 0;
            }

            int totalFiles = sourceFileInfos.Count;
            int mergedFiles = mergedSourceFileInfos.Count;
            int unmergedFiles = unmergedSourceFileInfos.Count;

            FlowDocument.FontFamily = new("SeguiUI");
            FlowDocument.FontSize = 12;

            Paragraph p1 = new()
            {
                Margin = new(0)
            };
            if (mergedFiles != 0)
            {
                p1.Inlines.Add(new Run
                {
                    FontWeight = FontWeights.Bold,
                    Text = $"{mergedFiles} / {totalFiles} files were merged:"
                });
                FlowDocument.Blocks.Add(p1);
                FlowDocument.Blocks.Add(GenerateMergeFeedbackMessage(mergedSourceFileInfos));
            }

            if (unmergedFiles > 0)
            {
                Paragraph p2 = new()
                {
                    Margin = new(0)
                };
                p2.Inlines.Add(new Run
                {
                    FontWeight = FontWeights.Bold,
                    Text = $"{unmergedFiles} / {totalFiles} files were not merged for the indicated reasons:"
                });
                FlowDocument.Blocks.Add(p2);
                FlowDocument.Blocks.Add(GenerateMergeFeedbackMessage(unmergedSourceFileInfos));
            }

            // Show the Done button and hide the other buttons
            CheckInButton.Visibility = Visibility.Collapsed;
            CancelButton.Visibility = Visibility.Collapsed;
            DoneButton.Visibility = Visibility.Visible;
        }
        #endregion

        #region Other Callbacks: Buttons and Checkboxes
        private void Selector_CheckChanged(object sender, RoutedEventArgs e)
        {
            EnableOrDisableCheckInButton();
        }

        private void ButtonSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (SourceFileInfo ddbObject in ObservableDdbFileList)
            {
                ddbObject.IsSelected = true;
            }
            ListboxFileDatabases.Items.Refresh();
            EnableOrDisableCheckInButton();
        }

        private void ButtonSelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (SourceFileInfo ddbObject in ObservableDdbFileList)
            {
                ddbObject.IsSelected = false;
            }
            ListboxFileDatabases.Items.Refresh();
            EnableOrDisableCheckInButton();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
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
        private Paragraph GenerateMergeFeedbackMessage(List<SourceFileInfo> sourceFileInfos)
        {
            Paragraph p1 = new()
            {
                Margin = new(0)
            };
            foreach (SourceFileInfo sourceFileInfo in sourceFileInfos)
            {
                if (sourceFileInfo.DatabaseFileError == DatabaseFileErrorsEnum.Ok ||
                    sourceFileInfo.DatabaseFileError == DatabaseFileErrorsEnum.OkButOpenedWithAnOlderTimelapseVersion ||
                    sourceFileInfo.DatabaseFileError == DatabaseFileErrorsEnum.TemplateElementsSameButOrderDifferent)
                {
                    p1.Inlines.Add($"   \u2713 {sourceFileInfo.ShortPathDisplayName}{Environment.NewLine}");
                }
                else
                {
                    p1.Inlines.Add($"   \u2717 {sourceFileInfo.ShortPathDisplayName}{Environment.NewLine}");
                    Tuple<string, Button> tuple = GenerateMessageFromDatabaseFileErrorsEnum(sourceFileInfo.DatabaseFileError);
                    p1.Inlines.Add($"         \u2022 {tuple.Item1}");
                    if (tuple.Item2 != null)
                    {
                        p1.Inlines.Add(tuple.Item2);
                    }
                    p1.Inlines.Add(Environment.NewLine);
                }
            }
            return p1;
        }

        // See above: this generates the actual text for each databaseFileError type
        private Tuple<string, Button> GenerateMessageFromDatabaseFileErrorsEnum(DatabaseFileErrorsEnum databaseFileError)
        {
            Button b1 = new()
            {
                BorderThickness = new(0),
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new(-1),
                Margin = new(4, 0, 4, 0),
                Content = " Click to explain "
            };

            switch (databaseFileError)
            {
                // All is good
                case DatabaseFileErrorsEnum.Ok:
                case DatabaseFileErrorsEnum.OkButOpenedWithAnOlderTimelapseVersion:
                case DatabaseFileErrorsEnum.TemplateElementsSameButOrderDifferent:
                    return new("Merge succeeded", null);

                // Cancelled
                case DatabaseFileErrorsEnum.Cancelled:
                    return new("The merging operation was cancelled by the user before this file was processed.", null);

                // Invalid file
                case DatabaseFileErrorsEnum.NotATimelapseFile: // This shouldn't be invoked, as we only see it if a file does not end with .ddb or .tdb. Still...
                case DatabaseFileErrorsEnum.InvalidDatabase:
                    b1.Tag = DatabaseFileErrorsEnum.InvalidDatabase;
                    b1.Click += InvokeErrorExplanation_Click;
                    return new("The file does not contain a valid .ddb database.", b1);

                // Old .ddb version
                case DatabaseFileErrorsEnum.PreVersion2300:
                case DatabaseFileErrorsEnum.UTCOffsetTypeExistsInUpgradedVersion:
                    b1.Tag = DatabaseFileErrorsEnum.UTCOffsetTypeExistsInUpgradedVersion;
                    b1.Click += InvokeErrorExplanation_Click;
                    return new("The file needs to be updated.", b1);

                // File in a non-permitted place
                case DatabaseFileErrorsEnum.FileInSystemOrHiddenFolder:
                case DatabaseFileErrorsEnum.FileInRootDriveFolder:
                    b1.Tag = DatabaseFileErrorsEnum.FileInSystemOrHiddenFolder;
                    b1.Click += InvokeErrorExplanation_Click;
                    return new("The file cannot be located in a system or hidden folder or in a top-level root folder.", b1);

                // File path is too long
                case DatabaseFileErrorsEnum.PathTooLong:
                    b1.Tag = DatabaseFileErrorsEnum.PathTooLong;
                    b1.Click += InvokeErrorExplanation_Click;
                    return new("The file path length exceeds the Windows maximum size.", b1);

                // Incompatible Template
                case DatabaseFileErrorsEnum.TemplateElementsDiffer:
                    b1.Tag = DatabaseFileErrorsEnum.TemplateElementsDiffer;
                    b1.Click += InvokeErrorExplanation_Click;
                    return new("The file's template is incompatible due to image control differences.", b1);

                // File does not exist
                case DatabaseFileErrorsEnum.DoesNotExist:
                    b1.Tag = DatabaseFileErrorsEnum.DoesNotExist;
                    b1.Click += InvokeErrorExplanation_Click;
                    return new("The file does not exist.", b1);


                // Recognizer categories differ
                case DatabaseFileErrorsEnum.DetectionCategoriesIncompatible:
                case DatabaseFileErrorsEnum.ClassificationCategoriesIncompatible:
                    b1.Tag = DatabaseFileErrorsEnum.DetectionCategoriesIncompatible;
                    b1.Click += InvokeErrorExplanation_Click;
                    return new("The file's recognition data has different detection and/or classification categories.", b1);
                // Problem with Metadata levels

                case DatabaseFileErrorsEnum.MetadataLevelsDiffer:
                    b1.Tag = DatabaseFileErrorsEnum.MetadataLevelsDiffer;
                    b1.Click += InvokeErrorExplanation_Click;
                    return new("The file's template is incompatible due to folder-level differences.", b1);

                case DatabaseFileErrorsEnum.IncompatibleVersion:
                    b1.Tag = DatabaseFileErrorsEnum.IncompatibleVersion;
                    b1.Click += InvokeErrorExplanation_Click;
                    return new("The file's data is incompatible with the version of Timelapse you are using.", b1);

                case DatabaseFileErrorsEnum.IncompatibleVersionForMerging:
                    b1.Tag = DatabaseFileErrorsEnum.IncompatibleVersionForMerging;
                    b1.Click += InvokeErrorExplanation_Click;
                    return new("The file you are trying to merge is incompatible with the version of the master database.", b1);

                default:
                    return new("Unknown error", null);
            }
        }

        private void InvokeErrorExplanation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b)
            {
                switch ((DatabaseFileErrorsEnum)b.Tag)
                {
                    // Invalid file
                    case DatabaseFileErrorsEnum.NotATimelapseFile: // This shouldn't be invoked, as we only see it if a file does not end with .ddb or .tdb. Still...
                    case DatabaseFileErrorsEnum.InvalidDatabase:
                        Dialogs.MergeErrorDatabaseFileAppearsCorruptDialog(this);
                        break;

                    // Old .ddb version
                    case DatabaseFileErrorsEnum.PreVersion2300:
                    case DatabaseFileErrorsEnum.UTCOffsetTypeExistsInUpgradedVersion:
                        Dialogs.MergeErrorDatabaseFileNeedsToBeUpdatedDialog(this);
                        break;

                    // Incomatible .ddb version
                    case DatabaseFileErrorsEnum.IncompatibleVersion:
                        Dialogs.DatabaseFileOpenedWithIncompatibleVersionOfTimelapse(this);
                        break;

                    case DatabaseFileErrorsEnum.IncompatibleVersionForMerging:
                        Dialogs.DatabaseFileBeingMergedIsIncompatibleWithParent(this);
                        break;

                    // File in a non-permitted place
                    case DatabaseFileErrorsEnum.FileInSystemOrHiddenFolder:
                    case DatabaseFileErrorsEnum.FileInRootDriveFolder:
                        Dialogs.TemplateInDisallowedFolder(this);
                        break;

                    // File path is too long
                    case DatabaseFileErrorsEnum.PathTooLong:
                        Dialogs.MergeErrorFilePathTooLongDialog(this);
                        break;

                    // Incompatible Template
                    case DatabaseFileErrorsEnum.TemplateElementsDiffer:
                        Dialogs.MergeErrorTemplateFilesNotCompatableDialog(this);
                        break;

                    // Incompatible Template
                    case DatabaseFileErrorsEnum.MetadataLevelsDiffer:
                        Dialogs.MergeErrorTemplateFilesLevelsNotCompatableDialog(this);
                        break;

                    // File does not exist
                    case DatabaseFileErrorsEnum.DoesNotExist:
                        Dialogs.MergeErrorFileDoesNotExist(this);
                        break;

                    // Recognizer categories differ
                    case DatabaseFileErrorsEnum.DetectionCategoriesIncompatible:
                    case DatabaseFileErrorsEnum.ClassificationCategoriesIncompatible:
                        Dialogs.MergeErrorRecognitionCategoriesIncompatible(this);
                        break;
                }
            }
        }

        // Check if there are any candidate sourceDdb files.
        // If not, display an error message and return true
        private bool TryGenerateErrorIfNoDdbSourceFilesExists()
        {
            // Check if any candidate source ddb files were found
            if (sourceDdbFilePaths == null || sourceDdbFilePaths.Count == 0)
            {
                // Show error message
                ListboxFileDatabases.Visibility = Visibility.Collapsed;
                FinalMessageScrollViewer.Visibility = Visibility.Visible;
                ButtonSelectAll.Visibility = Visibility.Collapsed;
                ButtonSelectNone.Visibility = Visibility.Collapsed;
                ResultsBanner.Text = "Warning: No database (.ddb) files are available to merge.";
                CheckInButton.IsEnabled = false;
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

            SortedList<string, string> relativePaths = new(descendingComparer);
            foreach (SourceFileInfo fileObject in ObservableDdbFileList)
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

                List<KeyValuePair<string, string>> matches = [.. relativePaths.Where(kvp => kvp.Value.StartsWith(relativePath.Value + "\\"))];
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
        private void EnableOrDisableCheckInButton()
        {
            foreach (SourceFileInfo ddbObject in ObservableDdbFileList)
            {
                if (ddbObject.IsSelected)
                {
                    CheckInButton.IsEnabled = true;
                    return;
                }
            }
            CheckInButton.IsEnabled = false;
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
            return ddbFilePath.Replace(tdbPathWithoutFileName!.TrimEnd(Path.DirectorySeparatorChar) + "\\", "");
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

            // Generate a shortened version of the file name to display in the available space
            public string ShortPathDisplayName =>
                RelativePathIncludingFileName.Length < maxLength
                    ? RelativePathIncludingFileName
                    : string.Concat(RelativePathIncludingFileName.AsSpan(0, prefixLength), " \u2026 ", RelativePathIncludingFileName.AsSpan()[^suffixLength..]);
        }

        #endregion
    }
}
