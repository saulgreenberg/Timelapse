using System;
using System.Windows;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for DeleteDeleteFolder.xaml
    /// </summary>
    public partial class DeleteDeleteFolder : Window
    {
        #region Private Variables
        private readonly int howManyDeleteFiles;
        #endregion

        #region Constructor, Loaded
        public DeleteDeleteFolder(int howManyDeleteFiles)
        {
            this.InitializeComponent();

            // If there are no files, just abort
            this.howManyDeleteFiles = howManyDeleteFiles;
        }

        // Adjust this dialog window position 
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            this.Message.What = String.Format("Your 'DeletedFiles' sub-folder contains backups of {0} 'deleted' image or video files.", this.howManyDeleteFiles);
        }
        #endregion

        #region Callbacks - Dialog Buttons
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion
    }
}
