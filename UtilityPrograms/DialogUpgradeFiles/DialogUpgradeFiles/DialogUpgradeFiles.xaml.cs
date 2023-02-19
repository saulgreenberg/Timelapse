using DialogUpgradeFiles.Database;
using DialogUpgradeFiles.Dialog;
using DialogUpgradeFiles.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using DragEventArgs = System.Windows.DragEventArgs;
// ReSharper disable HeuristicUnreachableCode

namespace DialogUpgradeFiles
{
    public partial class DialogUpgradeFilesAndFolders
    {
        #region Properties
        // These are set with parameters when this object is created
        public readonly string FolderPath;    // The initial folder path used for searching for files
        public readonly string TimelapseVersion; // The current Timelapse version used 
        public readonly bool IsInvokedAsGeneralUpdateFacility; // Set to true if the Folder path is initially passed as non-empty string

        // Indicates whether the upgrade was cancelled mid-stream
        public bool CancelUpgrade { get; set; }

        // Set to true if we should delete the imageQuality column
        public bool IsDeleteImageQualityRequested { get; set; } = true;

        // Used to display updating status information per file 
        public Dictionary<string, string> DictFileUpdateStatus { get; set; } = new Dictionary<string, string>();
        public string ShortFileName { get; set; } = string.Empty;

        // Used to animate a character-based spinner
        public DispatcherTimer AnimateProgressTimer { get; set; } = new DispatcherTimer();
        public string ProgressCharacter { get; set; } = string.Empty;     // a character that is updated to represent a spinner that shows activity
        #endregion

        #region Private variables
        private TemplateDatabase templateDatabase; // The database that holds the template
        private string previousFolderPath = string.Empty;   // so that OpenFile/Folder dialogs will begin at the last opened path
        #endregion

        #region Initialization and opening
        public DialogUpgradeFilesAndFolders(Window owner, string folderPath, string timelapseVersion)
        {
            InitializeComponent();
            this.Owner = owner;
            this.FolderPath = folderPath;
            this.TimelapseVersion = timelapseVersion;

            // If the folder path is not supplied, then this is invoked as a general update facility
            // Otherwise it is invoked on a specific file path
            this.IsInvokedAsGeneralUpdateFacility = string.IsNullOrWhiteSpace(this.FolderPath);

            this.RunFolderName.Text = this.FolderPath;

            // Create a timer that will be used to show a character-based spinner
            AnimateProgressTimer.Interval = TimeSpan.FromMilliseconds(100);
            AnimateProgressTimer.Tick += AnimateProgressTimer_Tick;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Position the dialog a titch below the parent
            if (this.Owner != null)
            {
                this.Left = this.Owner.Left + 20;
                this.Top = this.Owner.Top + 20;
            }

            if (this.IsInvokedAsGeneralUpdateFacility)
            {
                // By default, the interface is configured for invoking it as a
                // folder-specific facility. So we have to switch things around
                this.Title = "Upgrade your Timelapse files";
                this.TitleMessage.Text = this.Title;

                ListBoxResultsStatus.Items.Add("Drag and drop files and folders onto this box to upgrade them, or use the buttons below.");
                ListBoxResultsStatus.Items.Add("Any contained .tdb and .ddb files will be automatically selected and upgraded.");
                ListBoxResultsStatus.Items.Add("If issues arise, contact Saul Greenberg at saul@ucalgary.ca so he can investigate.");
            }
            // Display the appropriate instructions
            this.InstructionsForUpdateAnyFolderOrFile.Visibility = this.IsInvokedAsGeneralUpdateFacility
                ? Visibility.Visible : Visibility.Collapsed;
            this.InstructionsForUpdateAFolder.Visibility = this.IsInvokedAsGeneralUpdateFacility
                ? Visibility.Collapsed : Visibility.Visible;

            // The initial visibility of certain buttons, some which depend upon the IsUpdatingAFolderOnly state
            this.ButtonDone.Visibility = Visibility.Visible;
            this.ButtonCancelUpgrades.Visibility = Visibility.Collapsed;

            this.ButtonStartUpgrade.Visibility = this.IsInvokedAsGeneralUpdateFacility
                ? Visibility.Collapsed : Visibility.Visible;
            this.ButtonUpgradeSelectAFile.Visibility = this.IsInvokedAsGeneralUpdateFacility
                ? Visibility.Visible : Visibility.Collapsed;
            this.ButtonUpgradeSelectAFolder.Visibility = this.IsInvokedAsGeneralUpdateFacility
                ? Visibility.Visible : Visibility.Collapsed;
            this.LabelDragDrop.Visibility = this.IsInvokedAsGeneralUpdateFacility
                ? Visibility.Visible : Visibility.Collapsed;

            this.RadioButtonConvertToFlag1.Checked += RadioButtonSetImageQualityRequest_CheckedChanged;
            //this.RadioButtonConvertToFlag1.Unchecked += RadioButtonSetImageQualityRequest_CheckedChanged;
            this.RadioButtonConvertToFlag2.Checked += RadioButtonSetImageQualityRequest_CheckedChanged;
            //this.RadioButtonConvertToFlag2.Unchecked += RadioButtonSetImageQualityRequest_CheckedChanged;
            this.RadioButtonDeleteImageQuality1.Checked += RadioButtonSetImageQualityRequest_CheckedChanged;
            //this.RadioButtonDeleteImageQuality1.Unchecked += RadioButtonSetImageQualityRequest_CheckedChanged;
            this.RadioButtonDeleteImageQuality2.Checked += RadioButtonSetImageQualityRequest_CheckedChanged;
            //this.RadioButtonDeleteImageQuality2.Unchecked += RadioButtonSetImageQualityRequest_CheckedChanged;
            this.IsDeleteImageQualityRequested = this.IsInvokedAsGeneralUpdateFacility
                 ? RadioButtonDeleteImageQuality1.IsChecked == true
                 : RadioButtonDeleteImageQuality2.IsChecked == true;
        }
        #endregion

        #region Button, RadioButton, Drag and Drop callbacks
        // The radio button state either deletes the ImageQuality field (default) or replaces it with a Dark flag
        private void RadioButtonSetImageQualityRequest_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!(sender is RadioButton rb))
            {
                return;
            }
            if (rb == RadioButtonDeleteImageQuality1 || rb == RadioButtonConvertToFlag1)
            {
                this.IsDeleteImageQualityRequested = RadioButtonDeleteImageQuality1.IsChecked == true;
                this.RadioButtonDeleteImageQuality3.IsChecked = RadioButtonDeleteImageQuality1.IsChecked;
                this.RadioButtonConvertToFlag3.IsChecked = RadioButtonConvertToFlag1.IsChecked;
            }
            else if (rb == RadioButtonDeleteImageQuality2 || rb == RadioButtonConvertToFlag2)
            {
                this.IsDeleteImageQualityRequested = RadioButtonDeleteImageQuality2.IsChecked == true;
                this.RadioButtonDeleteImageQuality3.IsChecked = RadioButtonDeleteImageQuality2.IsChecked;
                this.RadioButtonConvertToFlag3.IsChecked = RadioButtonConvertToFlag2.IsChecked;
            }
            else if (rb == RadioButtonDeleteImageQuality3 || rb == RadioButtonConvertToFlag3)
            {
                this.IsDeleteImageQualityRequested = RadioButtonDeleteImageQuality3.IsChecked == true;
            }

            //this.IsDeleteImageQualityRequested = this.IsInvokedAsGeneralUpdateFacility
            //     ? RadioButtonDeleteImageQuality1.IsChecked == true
            //     : RadioButtonDeleteImageQuality2.IsChecked == true;
        }

        // Select files and upgrade them
        private async void ButtonUpgradeSelectedFiles_Click(object sender, RoutedEventArgs e)
        {
            if (Dialogs.TryGetFilesFromUserUsingOpenFileDialog("Choose one or more .tdb or .ddb files", this.previousFolderPath, ".ddb or .tdb files|*.*db", "", out string[] selectedFiles))
            {
                await this.BeginUpgrading(selectedFiles);
                this.SetPreviousFolderPath(selectedFiles[0]);
            }
        }

        // Select a folder and upgrade it
        private async void ButtonUpgradeSelectAFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Dialogs.TryGetFoldersFromUserUsingOpenFileDialog("Choose one or more folders", this.previousFolderPath, out string[] selectedPaths))
            {
                await this.BeginUpgrading(selectedPaths);
                this.SetPreviousFolderPath(selectedPaths[0]);
            }
        }

        // Start Upgrade with the provided folder path
        private async void ButtonStartUpgrade_Click(object sender, RoutedEventArgs e)
        {
            // There should be a folder path in this.FolderPath,
            // as otherwise this button (and thus its callback) isn't selectable
            string[] selectedPaths = new string[] { this.FolderPath };
            await this.BeginUpgrading(selectedPaths);
            this.SetPreviousFolderPath(this.FolderPath);
        }

        // Objects were dragged and dropped. Upgrade valid files
        private async void GridUpgradeFiles_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] selectedPaths = (string[])e.Data.GetData(DataFormats.FileDrop);
                await this.BeginUpgrading(selectedPaths);
                if (selectedPaths != null)
                {
                    // It should never be null
                    this.SetPreviousFolderPath(selectedPaths[0]);
                }
            }
        }

        // Cancel the current upgrade opeation
        private void ButtonCancelUpgrades_Click(object sender, RoutedEventArgs e)
        {
            this.CancelUpgrade = true;
        }

        // Done - return dialog result is based on whether things were cancelled.
        private void ButtonDone_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = !this.CancelUpgrade;
        }
        #endregion

        #region BeginUpgrading and Helpers
        private async Task BeginUpgrading(string[] selectedPaths)
        {
            this.CancelUpgrade = false;
            this.TitleMessage.Text = "Searching for .ddb and .tdb files that require updating...";

            // Collapse the instructions
            this.InstructionsForUpdateAnyFolderOrFile.Visibility = Visibility.Collapsed;
            this.InstructionsForUpdateAFolder.Visibility = Visibility.Collapsed;

            // The two feedback area should be visible and prepared
            this.FeedbackArea.Visibility = Visibility.Visible;
            this.ListBoxResultsStatus.Visibility = Visibility.Visible;
            this.ListBoxResultsStatus.Items.Clear();

            // The particular button visibility depends upon whether its being updated or not
            this.ButtonUpgradeSelectAFile.Visibility = Visibility.Collapsed;
            this.ButtonUpgradeSelectAFolder.Visibility = Visibility.Collapsed;
            this.ButtonStartUpgrade.Visibility = Visibility.Collapsed;
            this.LabelDragDrop.Visibility = Visibility.Collapsed;
            this.ButtonDone.Visibility = Visibility.Collapsed;
            this.ButtonDone.Content = "Done";
            this.ButtonCancelUpgrades.Visibility = Visibility.Visible;

            // ReSharper disable once UnusedVariable
            List<string> pathsTooLong = new List<string>();
            Dictionary<string, UpgradeResultsEnum> filePathsRequiringUpdating = await CollectFiles(selectedPaths);

            if (filePathsRequiringUpdating.Count == 0)
            {
                // No filepaths require updating, or appear to have issues 
                if (this.IsInvokedAsGeneralUpdateFacility)
                {
                    this.LineFeedback("No .ddb or .tdb files found");
                    this.TitleMessage.Text = "You can upgrade more Timelapse files if desired";
                }
                else
                {
                    this.TitleMessage.Text = "No .ddb or .tdb files were found that require updating.";
                }
            }
            else
            {
                // Begin updating
                // Prepare the feedback area
                Mouse.OverrideCursor = Cursors.Wait;
                this.TitleMessage.Text = $"Upgrading {filePathsRequiringUpdating.Count} files";
                this.DictFileUpdateStatus.Clear();

                // Update the files requiring upgrading
                UpgradeResultsEnum updateResultsEnum = await this.UpgradeFiles(filePathsRequiringUpdating, this.TimelapseVersion);

                // Depending on the result, display the appropriate status message
                if (updateResultsEnum == UpgradeResultsEnum.Cancelled)
                {
                    this.TitleMessage.Text = "Cancelled. Check below to see which files were upgraded, if any.";
                }
                else if (updateResultsEnum == UpgradeResultsEnum.NoFilesFound)
                {
                    // This shouldn't happen, but...
                    this.TitleMessage.Text = "No.ddb or .tdb files were found that require upgrading.";
                    this.ListBoxResultsStatus.Items.Add("No .ddb or .tdb files were found that required upgrading");
                }
                else if (updateResultsEnum == UpgradeResultsEnum.NoFilesUpdated)
                {
                    // This shouldn't happen, but...
                    this.TitleMessage.Text = "No.ddb or .tdb files were upgraded.";
                }
                else // Upgrading was completed
                {
                    this.TitleMessage.Text = this.IsInvokedAsGeneralUpdateFacility
                        ? "You can upgrade more Timelapse files if desired."
                        : "Close this, then try loading your image set again.";
                }
            }
            // These buttons are made visible if its a general update facility
            this.ButtonUpgradeSelectAFile.Visibility = this.IsInvokedAsGeneralUpdateFacility ? Visibility.Visible : Visibility.Collapsed;
            this.ButtonUpgradeSelectAFolder.Visibility = this.IsInvokedAsGeneralUpdateFacility ? Visibility.Visible : Visibility.Collapsed;
            this.LabelDragDrop.Visibility = this.IsInvokedAsGeneralUpdateFacility ? Visibility.Visible : Visibility.Collapsed;

            if (this.IsInvokedAsGeneralUpdateFacility)
            {
                this.RadioButtonPanel3.Visibility = Visibility.Visible;
                this.RadioButtonConvertToFlag3.Checked += RadioButtonSetImageQualityRequest_CheckedChanged;
                this.RadioButtonDeleteImageQuality3.Checked += RadioButtonSetImageQualityRequest_CheckedChanged;
            }

            // Replace the cancel button for a done button
            this.ButtonCancelUpgrades.Visibility = Visibility.Collapsed;
            this.ButtonDone.Visibility = Visibility.Visible;
            Mouse.OverrideCursor = null;
        }

        #endregion

        //#region Utilities
        //static List<string> CheckPathLengthsForBackups(List<string> filepaths)
        //{
        //    List<string> longPaths = new List<string>();
        //    string sampleDateTime = DateTime.Now.ToString("yyyy-MM-dd.HH-mm-ss");
        //    foreach (string p in filepaths)
        //    {
        //        string sourceFileName = Path.GetFileName(p);
        //        string sourceFileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourceFileName);
        //        string sourceFileExtension = Path.GetExtension(sourceFileName);
        //        string destinationFileName = String.Concat(sourceFileNameWithoutExtension, Constant.File.BackupPre23Indicator, ".", sampleDateTime, sourceFileExtension);
        //        string backupFolder = Path.Combine(Path.GetDirectoryName(p), Constant.File.BackupFolder);
        //        string destinationFilePath = Path.Combine(backupFolder, destinationFileName);
        //        System.Diagnostics.Debug.Print(destinationFilePath);
        //        if (IsCondition.IsPathLengthTooLong(destinationFilePath, FilePathTypeEnum.Pre23))
        //        {
        //            longPaths.Add(destinationFilePath);
        //            System.Diagnostics.Debug.Print(destinationFilePath.Length + "|" + destinationFilePath);
        //        }
        //    }
        //    return longPaths;
        //}
        //#endregion

        #region AnimateProgressTimer feedback
        private void AnimateProgressTimer_Tick(object sender, EventArgs e)
        {
            switch (this.ProgressCharacter)
            {
                case "\\":
                    ProgressCharacter = "|";
                    break;
                case "|":
                    ProgressCharacter = "/";
                    break;
                case "/":
                    ProgressCharacter = "-";
                    break;
                default:
                    ProgressCharacter = "\\";
                    break;
            }
            DictFileUpdateStatus[this.ShortFileName] = "Processing: " + ProgressCharacter + "   ";
            this.RefreshFileUpdateStatusDisplay(DictFileUpdateStatus, -1);
        }
        #endregion

        #region Provide Feedback about the updates
        private void RefreshFileUpdateStatusDisplay(Dictionary<string, string> dictFileUpdateStatus, int index)
        {
            int shortLength = 60;
            this.ListBoxResultsStatus.Items.Clear();
            foreach (KeyValuePair<string, string> kvp in dictFileUpdateStatus)
            {
                string shortenedKey = kvp.Key.Length <= shortLength
                    ? kvp.Key
                    : "..." + kvp.Key.Substring(Math.Max(0, kvp.Key.Length - shortLength));
                this.ListBoxResultsStatus.Items.Add($"{kvp.Value,-40} {shortenedKey}");
            }
            if (index >= 0)
            {
                this.ListBoxResultsStatus.SelectedIndex = index;
                this.ListBoxResultsStatus.ScrollIntoView(this.ListBoxResultsStatus.SelectedItem);
            }
        }

#pragma warning disable CA1822
        public void DebugFeedback(bool success, string message)
#pragma warning restore CA1822
        {
            bool trace = false; // Change to true to show the feedback
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (trace)
            {
                // ReSharper disable once RedundantAssignment
                message = success ? "OK " + message : "XX " + message;
                System.Diagnostics.Debug.Print(message);
            }
        }

#pragma warning disable CA1822
        public void DebugFeedback(string message)
#pragma warning restore CA1822
        {
            bool trace = false; // Change to true to show the feedback
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (trace)
            {
                System.Diagnostics.Debug.Print(message);
            }
        }

        public void LineFeedback(string message)
        {
            this.ParagraphFeedback.Inlines.Clear();
            this.ParagraphFeedback.Inlines.Add(message);
        }
        #endregion
    }
}
