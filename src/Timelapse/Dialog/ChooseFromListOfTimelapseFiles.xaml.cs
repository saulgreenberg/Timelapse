using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// If a user tries to open a Timelapse template (.tdb) , and there is more than one data file in the folder containing the file they are trying to open,
    /// OR If a user tries to open a Timelapse data (.ddb) , and there is more than one template file in the folder containing the file they are trying to open,
    /// this dialog asks the user to choose the one they want.
    /// The dialog's text is adjusted depending on whether a template or data file is being chosen.
    /// </summary>
    public partial class ChooseFromListOfTimelapseFiles
    {
        #region Public Properties
        // This will contain the file selected by the user
        public string SelectedFile { get; set; }
        private string FolderPath { get; }
        #endregion

        #region Constructor and Loaded
        public ChooseFromListOfTimelapseFiles(Window owner, string[] timelapseFilePaths, string filePathThatWeHave)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(timelapseFilePaths, nameof(timelapseFilePaths));
            bool isChoosingData = Path.GetExtension(filePathThatWeHave) == Constant.File.FileDatabaseFileExtension;
            string shortfilePath = Path.GetFileName(filePathThatWeHave);
            string fileType = isChoosingData ? "template" : "data";
            string otherFile = isChoosingData ? "data" : "template";
            string extension = isChoosingData ? ".tdb" : ".ddb";

            InitializeComponent();
            // Set up static reference resolver for FormattedMessageContent
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            Owner = owner;
            this.FolderPath = Path.GetDirectoryName(filePathThatWeHave);
            Message.DialogTitle = $"Choose a Timelapse {fileType} ([i]{extension}[/i]) file";
            Message.Problem = $"Multiple Timelapse {fileType} files ([i]{extension}[/i]) exist in the same folder as your chosen #DarkSlateGray[{shortfilePath}] {otherFile} file.";
            Message.Solution = $"You need to choose which of the Timelapse {fileType} files below you want to use.";
            Message.Result = isChoosingData 
                ? "Timelapse will open your data file with the chosen template"
                : "Timelapse will read existing data from the chosen data file, and will save any data you enter into that file.";
            Message.Hint = isChoosingData 
                ? "A Timelapse template file defines the data fields for your image set, including the fields you see and the way they are displayed. "
                : "A Timelapse data file stores the information you (or someone else) had previously entered for this image set. ";
            Message.Hint += "It is important that you choose the right one.";
            Message.BuildContentFromProperties();
            SelectedFile = string.Empty;

            // timelapseFilePaths contains an array of files. We add each to the listbox.
            // by default, the first item in the listbox is shown selected.
            int defaultFileIndex = 0;
            string fileDatabaseNameWithoutExtension = Path.GetFileNameWithoutExtension(filePathThatWeHave);
            for (int index = 0; index < timelapseFilePaths.Length; ++index)
            {
                string fileName = Path.GetFileName(timelapseFilePaths[index]);
                string otherFileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                if (string.Equals(otherFileNameWithoutExtension, fileDatabaseNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
                {
                    defaultFileIndex = index;
                }
                TimelapseFiles.Items.Add(fileName);
            }
            TimelapseFiles.SelectedIndex = defaultFileIndex;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            // marking the OK button IsDefault to associate it with dialog completion also gives it initial focus
            // It's more helpful to put focus on the database list as this saves having to tab to the list as a first step.
            TimelapseFiles.Focus();
        }
        #endregion

        #region Callbacks
        private void FileDatabases_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TimelapseFiles.SelectedIndex != -1)
            {
                OkButton_Click(sender, e);
            }
        }

        private void FileDatabases_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            OkButton.IsEnabled = TimelapseFiles.SelectedIndex != -1;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedFile = Path.Combine(this.FolderPath, TimelapseFiles.SelectedItem.ToString()!); // The selected file
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
        #endregion
    }
}
