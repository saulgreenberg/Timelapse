using System;
using System.IO;
using System.Windows;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// When there is more than one .ddb file in the folder containing the template, this dialog asks the user to choose the one they want.
    /// </summary>
    public partial class ChooseFileDatabaseFile : Window
    {
        #region Public Properties
        // This will contain the file selected by the user
        public string SelectedFile { get; set; }
        #endregion

        #region Constructor and Loaded
        public ChooseFileDatabaseFile(string[] fileDatabasePaths, string templateDatabasePath, Window owner)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(fileDatabasePaths, nameof(fileDatabasePaths));

            this.InitializeComponent();
            this.Owner = owner;
            this.SelectedFile = String.Empty;

            // file_names contains an array of .ddb files. We add each to the listbox.
            // by default, the first item in the listbox is shown selected.
            int defaultDatabaseIndex = 0;
            string templateDatabaseNameWithoutExtension = Path.GetFileNameWithoutExtension(templateDatabasePath);
            for (int index = 0; index < fileDatabasePaths.Length; ++index)
            {
                string databaseName = Path.GetFileName(fileDatabasePaths[index]);
                string databaseNameWithoutExtension = Path.GetFileNameWithoutExtension(databaseName);
                if (String.Equals(databaseNameWithoutExtension, templateDatabaseNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
                {
                    defaultDatabaseIndex = index;
                }
                this.FileDatabases.Items.Add(databaseName);
            }
            this.FileDatabases.SelectedIndex = defaultDatabaseIndex;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            // marking the OK button IsDefault to associate it with dialog completion also gives it initial focus
            // It's more helpful to put focus on the database list as this saves having to tab to the list as a first step.
            this.FileDatabases.Focus();
        }
        #endregion

        #region Callbacks
        private void FileDatabases_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (this.FileDatabases.SelectedIndex != -1)
            {
                this.OkButton_Click(sender, e);
            }
        }

        private void FileDatabases_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            this.OkButton.IsEnabled = this.FileDatabases.SelectedIndex != -1;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.SelectedFile = this.FileDatabases.SelectedItem.ToString(); // The selected file
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion
    }
}
