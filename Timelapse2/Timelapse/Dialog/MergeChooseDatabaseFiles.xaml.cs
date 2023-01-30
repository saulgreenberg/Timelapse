using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.Enums;
using Timelapse.Util;
using File = Timelapse.Constant.File;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for MergeChooseDatabaseFiles.xaml
    /// </summary>
    public partial class MergeChooseDatabaseFiles
    {
        private readonly List<string> SourceddbFilePaths;
        private readonly string TemplatetdbFilePath;
        private readonly string RootFolderPath;
        private readonly string RootFolderName;
        private readonly string DestinationddbFilePath;
        private readonly string DestinationddbFileName;

        // Tracks whether any changes to the database was made
        private bool IsAnyDataUpdated;

        public ObservableCollection<ddbFileClass> ObservableddbFileList { get; set; }
        public string DatabaseToLoad { get; set; } = String.Empty;
        public bool FoundInvalidFiles { get; set; } = false;
        public MergeChooseDatabaseFiles(Window owner, string templatetdbFilePath) : base(owner)
        {
            InitializeComponent();

            this.TemplatetdbFilePath = templatetdbFilePath;
            this.ObservableddbFileList = new ObservableCollection<ddbFileClass>();
            this.RootFolderPath = Path.GetDirectoryName(templatetdbFilePath);
            this.RootFolderName = this.RootFolderPath.Split(Path.DirectorySeparatorChar).Last();
            this.DestinationddbFileName = Constant.File.MergedFileName;
            this.DestinationddbFilePath = Path.Combine(this.RootFolderPath, this.DestinationddbFileName);
            this.SourceddbFilePaths = FilesFolders.GetAllFilesInFoldersAndSubfoldersMatchingPattern(this.RootFolderPath, "*" + Constant.File.FileDatabaseFileExtension, true, true, null);

            // If the merged database file name is included in the source list (e.g., if it was previously created),
            // remove it from the list as a possible source file. Note that, if we can do any merging, we will be over-writing that file.
            this.SourceddbFilePaths.RemoveAll(Item => Item == this.DestinationddbFilePath);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (SourceddbFilePaths == null || SourceddbFilePaths.Count == 0)
            {
                // Show error message
                ListboxFileDatabases.Visibility = Visibility.Collapsed;
                ScrollerTextBlockFinalMessage.Visibility = Visibility.Visible;
                LabelBanner.Content = "Warning: Merging cannot be done.";
                TextBlockFinalMessage.Text = String.Format("Timelapse searches for database (.ddb) files in:{0}", Environment.NewLine);
                TextBlockFinalMessage.Text += String.Format(" \u2022 the folder containing the template ({0}),{1}", RootFolderPath, Environment.NewLine);
                TextBlockFinalMessage.Text += String.Format(" \u2022 its sub-folders.{0}{0}", Environment.NewLine);
                TextBlockFinalMessage.Text += "No database (.ddb) files were found, so there is nothing to merge.";
                MergeButton.IsEnabled = false;
                return;
            }

            if (IsCondition.IsPathLengthTooLong(DestinationddbFilePath, FilePathTypeEnum.DDB))
            {
                string dir = Path.GetDirectoryName(DestinationddbFilePath);
                dir = Path.GetDirectoryName(DestinationddbFilePath).Length < 41
                    ? dir
                    : Path.GetDirectoryName(DestinationddbFilePath).Substring(0, 40);
                string shortenedPath = "  - " + dir + "......." + Path.GetFileName(DestinationddbFilePath);
                // The path of the TimelapseData-merged.ddb file is too long
                ScrollerTextBlockFinalMessage.Visibility = Visibility.Visible;
                LabelBanner.Content = "Warning: Merging cannot be done.";
                TextBlockFinalMessage.Text = String.Format("The path to the merged database (.ddb) file is too long: {0}{1}{0}", Environment.NewLine, shortenedPath);
                TextBlockFinalMessage.Text += "Windows cannot perform file operations if the file path is more than " + File.MaxPathLength + " characters." + Environment.NewLine + Environment.NewLine;
                TextBlockFinalMessage.Text += "Try again after shortening the file path:" + Environment.NewLine;
                TextBlockFinalMessage.Text += "\u2022 shorten the path name by moving your image folder higher up the folder hierarchy, or" + Environment.NewLine + "\u2022 use shorter folder or file names.";
                MergeButton.IsEnabled = false;
                return;
            }

            // Set up a progress handler that will update the progress bar
            InitalizeProgressHandler(BusyCancelIndicator);

            // We have at least one or more valid .ddb files. Load them up into the list
            foreach (string ddbFile in SourceddbFilePaths)
            {
                ObservableddbFileList.Add(new ddbFileClass
                {
                    IsSelected = true,
                    FullPath = ddbFile,
                    RelativePathIncludingFileName = GetRelativePathAndFileName(TemplatetdbFilePath, ddbFile)
                });
            }
            DataContext = this;
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.DialogResult = this.Token.IsCancellationRequested || this.IsAnyDataUpdated;
        }

        private void Selector_CheckChanged(object sender, RoutedEventArgs e)
        {
            foreach (ddbFileClass ddbObject in ObservableddbFileList)
            {
                if (ddbObject.IsSelected)
                {
                    this.MergeButton.IsEnabled = true;
                    return;
                }
            }
            this.MergeButton.IsEnabled = false;
        }
        private async void MergeButton_Click(object sender, RoutedEventArgs e)
        {
            List<string> sourceddbFilePaths = new List<string>();
            foreach (ddbFileClass fileObject in ObservableddbFileList)
            {

                if (fileObject.IsSelected)
                {
                    sourceddbFilePaths.Add(fileObject.FullPath);
                }
            }
            // Set up progress indicators
            Mouse.OverrideCursor = Cursors.Wait;
            this.BusyCancelIndicator.EnableForMerging(true);

            // Merge the found databases into a new (or replaced) TimelapseData_merged.ddb file located in the same folder as the template.
            // Note: .ddb files found in a Backup folder will be ignored
            ErrorsAndWarnings errorMessages = await MergeDatabases.TryMergeDatabasesAsync(
                this.TemplatetdbFilePath,
                sourceddbFilePaths,
                this.RootFolderPath,
                this.RootFolderName,
                this.DestinationddbFilePath,
                this.DestinationddbFileName,
                this.Progress, GlobalReferences.CancelTokenSource).ConfigureAwait(true);

            // Turn off progress indicators
            this.BusyCancelIndicator.EnableForSelection(false);
            this.BusyCancelIndicator.Reset();
            Mouse.OverrideCursor = null;

            this.ListboxFileDatabases.Visibility = Visibility.Collapsed;
            this.ScrollerTextBlockFinalMessage.Visibility = Visibility.Visible;
            bool merged = false;

            if (errorMessages.Warnings.Count == 1 && errorMessages.Warnings[0] == "Merge cancelled.")
            {
                this.LabelBanner.Content = "Merge Cancelled.";
                this.TextBlockFinalMessage.Text = "The merge operation was cancelled";
            }
            else if (errorMessages.Errors.Count != 0)
            {
                this.LabelBanner.Content = "Merge Databases Failed.";
                this.TextBlockFinalMessage.Text = "The merged database could not be created for the following reasons:";
                foreach (string error in errorMessages.Errors)
                {
                    this.TextBlockFinalMessage.Text += String.Format("{0}\u2022 {1},", Environment.NewLine, error);
                }
            }
            else if (errorMessages.Warnings.Count != 0)
            {

                this.LabelBanner.Content = "Merge Databases Left Out Some Files.";
                this.TextBlockFinalMessage.Text = "The merged database left out some files for the following reasons:";
                foreach (string warning in errorMessages.Warnings)
                {
                    this.TextBlockFinalMessage.Text += String.Format("{0}\u2022 {1}", Environment.NewLine, warning);
                }
                this.TextBlockFinalMessage.Text += Environment.NewLine + Environment.NewLine;
                if (errorMessages.MergedFiles.Count == 0)
                {
                    this.TextBlockFinalMessage.Text += String.Format("{0}{1}", Environment.NewLine, "No files were left to merge");
                    merged = false;
                }
                else
                {
                    merged = true;
                }
            }
            else
            {
                this.LabelBanner.Content = "Databases merged.";
                merged = true;
            }
            if (merged)
            {
                this.TextBlockFinalMessage.Text += "These database files were merged:";
                foreach (string file in errorMessages.MergedFiles)
                {
                    this.TextBlockFinalMessage.Text += String.Format("{0}\u2022 {1}", Environment.NewLine, file);
                }
                this.TextBlockFinalMessage.Text += String.Format("{0}{0}The merged database is located in:", Environment.NewLine);
                this.TextBlockFinalMessage.Text += String.Format("{0}\u2022 {1}", Environment.NewLine, this.DestinationddbFilePath);
                this.DoneWithLoadButton.Visibility = Visibility.Visible;
                this.IsAnyDataUpdated = true;
            }

            if (errorMessages.BackupMessages.Any())
            {
                foreach (string file in errorMessages.BackupMessages)
                {
                    this.TextBlockFinalMessage.Text += String.Format("{0}{0}{1}", Environment.NewLine, file);
                }
            }

            this.MergeButton.Visibility = Visibility.Collapsed;
            this.CancelButton.Visibility = Visibility.Collapsed;
            this.DoneButton.Visibility = Visibility.Visible;
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
            this.DatabaseToLoad = this.TemplatetdbFilePath;
            this.DialogResult = true;
        }
        #region Private methods
        // Return the relative path (including file name) to the ddb file. 
        //For example if the template is in C:\foo\template.tdb and the ddb file is in C:\foo\bar\data.ddb,
        // it will return bar\data.ddb
        private static string GetRelativePathAndFileName(string tdbPath, string ddbFilePath)
        {
            string tdbPathWithoutFileName = Path.GetDirectoryName(tdbPath);
            string relativePathAndFileName = ddbFilePath.Replace(tdbPathWithoutFileName.TrimEnd(Path.DirectorySeparatorChar) + "\\", "");
            return relativePathAndFileName;
        }
        #endregion

    }
    public class ddbFileClass
    {
        public bool IsSelected { get; set; }
        public string FullPath { get; set; }
        public string RelativePathIncludingFileName { get; set; }
    }
}
