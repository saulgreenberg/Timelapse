using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.DataTables;
using Timelapse.Util;
using File = System.IO.File;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for ExportAllSelectedFiles.xaml
    /// </summary>
    public partial class ExportAllSelectedFiles
    {
        #region Private Variables
        private readonly FileDatabase FileDatabase;
        #endregion

        #region Constructors / Loaded / Closing
        public ExportAllSelectedFiles(Window owner, FileDatabase fileDatabase) : base(owner)
        {
            InitializeComponent();
            FileDatabase = fileDatabase;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);

            // Set up a progress handler that will update the progress bar
            InitalizeProgressHandler(BusyCancelIndicator);

            FolderLocation.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            int count = FileDatabase.CountAllCurrentlySelectedFiles;
            if (count >= 100)
            {
                // Reality check...
                Message.Hint =
                    $"Do you really want to copy [b]{count}[/b] files? This seems like alot.{Environment.NewLine}" + Message.Hint;
            }
            Message.DialogTitle = $"Export (by copying) [b]{count}[/b] currently selected files";
            this.Message.BuildContentFromProperties();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Closing the window also indicates we should cancel any ongoing operations
            TokenSource.Cancel();
        }
        #endregion

        #region Private Methods - Button Callbacks
        private void ChooseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            // Set up a Folder Browser with some instructions
            if (Dialogs.TryGetFolderFromUserUsingOpenFileDialog("Locate folder to export your files...", FolderLocation.Text, out string folder))
            {
                // Display the folder
                FolderLocation.Text = folder;
            }
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            if (GetPathAndCreateItIfNeeded(out string path) == false)
            {
                System.Windows.MessageBox.Show(String.Format("Could not create the folder: {1}  {0}{1}Export aborted.", path, Environment.NewLine), "Export aborted.", MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
            }
            BusyCancelIndicator.IsBusy = true;
            string feedbackMessage = await CopyFiles(path).ConfigureAwait(true);
            BusyCancelIndicator.IsBusy = false;

            Grid1.Visibility = Visibility.Collapsed;
            ButtonPanel1.Visibility = Visibility.Collapsed;

            TextBlockFeedback.Text = feedbackMessage;
            TextBlockFeedback.Visibility = Visibility.Visible;
            ButtonPanel2.Visibility = Visibility.Visible;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
        #endregion

        #region Private Methods - Copy Files (async with progress) and Folder operations
        private async Task<string> CopyFiles(string path)
        {
            string fileNamePrefix;
            string sourceFile;
            string destFile;
            int totalFiles = FileDatabase.FileTable.RowCount;
            int copiedFiles = 0;
            int skippedFiles = 0;
            int existingFiles = 0;
            bool cancelled = false;
            bool renameFileWithPrefix = CBRename.IsChecked == true;

            // We first check all files to see if they exist in the destination.
            // Yes, this is a bit heavyweight. There is likely a more efficient way to do this.
            foreach (ImageRow ir in FileDatabase.FileTable)
            {
                fileNamePrefix = renameFileWithPrefix
                ? ir.RelativePath.Replace('\\', '.')
                : string.Empty;

                destFile = string.IsNullOrWhiteSpace(fileNamePrefix)
                ? Path.Combine(path, fileNamePrefix + ir.File)
                : Path.Combine(path, fileNamePrefix + '.' + ir.File);

                if (File.Exists(destFile))
                {
                    existingFiles++;
                }
            }
            if (existingFiles > 0)
            {
                if (Dialogs.OverwriteExistingFiles(this, existingFiles) != true)
                {
                    // copiedFiles = -2; // indicates the duplicate file condition
                    return "Export aborted to avoid overwriting files.";
                }
            }

            await Task.Run(() =>
            {
                if (!renameFileWithPrefix)
                {
                    // Since we are not renaming files, we have to check for duplicates.
                    // If even one duplicate exists, abort.
                    HashSet<string> testForDuplicates = [];
                    foreach (ImageRow ir in FileDatabase.FileTable)
                    {
                        if (testForDuplicates.Add(ir.File)) continue;
                        cancelled = true;
                        copiedFiles = -1; // indicates the duplicate file condition
                        return;
                    }
                }

                foreach (ImageRow ir in FileDatabase.FileTable)
                {
                    if (TokenSource.IsCancellationRequested)
                    {
                        cancelled = true;
                        return;
                    }

                    fileNamePrefix = renameFileWithPrefix
                    ? ir.RelativePath.Replace('\\', '.')
                    : string.Empty;

                    sourceFile = Path.Combine(FileDatabase.RootPathToImages, ir.RelativePath, ir.File);
                    destFile = string.IsNullOrWhiteSpace(fileNamePrefix)
                    ? Path.Combine(path, fileNamePrefix + ir.File)
                    : Path.Combine(path, fileNamePrefix + '.' + ir.File);

                    try
                    {
                        File.Copy(sourceFile, destFile, true);
                        copiedFiles++;
                    }
                    catch
                    {
                        skippedFiles++;
                    }

                    if (!ReadyToRefresh()) continue;

                    int percentDone = Convert.ToInt32((copiedFiles + skippedFiles) / Convert.ToDouble(totalFiles) * 100.0);
                    Progress.Report(new(percentDone,
                        $"Copying {copiedFiles} / {totalFiles} files", true, false));
                    Thread.Sleep(ThrottleValues.RenderingBackoffTime);
                }
            }).ConfigureAwait(true);

            if (cancelled)
            {
                if (copiedFiles >= 0)
                {
                    return $"Export cancelled after copying {copiedFiles} files";
                }
                if (copiedFiles == -1)
                {
                    return string.Format("Export aborted, as duplicate file names exist." + Environment.NewLine + "Try using the Rename option to guarantee unique file names.");
                }

            }
            if (skippedFiles == 0)
            {
                return $"Copied {copiedFiles} out of {totalFiles} files{Environment.NewLine}";
            }
            string feedbackMessage = $"Copied {copiedFiles} out of {totalFiles} files{Environment.NewLine}";
            feedbackMessage += (skippedFiles == 1)
                    ? $"Skipped {skippedFiles} file (perhaps it is missing?)"
                    : $"Skipped {skippedFiles} files (perhaps they are missing?)";
            return feedbackMessage;
        }

        private bool GetPathAndCreateItIfNeeded(out string path)
        {
            path = (CBPutInSubFolder.IsChecked == true)
                ? Path.Combine(FolderLocation.Text, TextBoxPutInSubFolder.Text)
                : FolderLocation.Text;

            if (Directory.Exists(path)) return true;

            try
            {
                Directory.CreateDirectory(path);
            }
            catch
            {
                return false;
            }
            return true;
        }
        #endregion
    }
}
