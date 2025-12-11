using System.Windows;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for DeleteDeleteFolder.xaml
    /// </summary>
    public partial class DeleteDeleteFolder
    {
        #region Private Variables
        private readonly int howManyDeleteFiles;
        #endregion

        #region Constructor, Loaded
        public DeleteDeleteFolder(int howManyDeleteFiles)
        {
            InitializeComponent();

            // If there are no files, just abort
            this.howManyDeleteFiles = howManyDeleteFiles;
        }

        // Adjust this dialog window position
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            this.Message.BuildContentFromProperties();
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            Message.What =
                $"Your 'DeletedFiles' sub-folder contains backups of {howManyDeleteFiles} 'deleted' image or video files.";
        }
        #endregion

        #region Callbacks - Dialog Buttons
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
        #endregion
    }
}
