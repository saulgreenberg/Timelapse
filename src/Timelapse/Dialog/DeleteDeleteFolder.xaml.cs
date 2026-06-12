using System.IO;
using System.Linq;
using System.Windows;
using Timelapse.Util;
using TimelapseWpf.Toolkit;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for DeleteDeleteFolder.xaml
    /// </summary>
    public partial class DeleteDeleteFolder
    {
        #region Private Variables
        private readonly string DeletedFolderPath;
        #endregion

        #region Constructor, Loaded
        public DeleteDeleteFolder(string deletedFolderPath)
        {
            InitializeComponent();
            this.DeletedFolderPath = deletedFolderPath; 
        }

        // Adjust this dialog window position
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            this.Message.BuildContentFromProperties();
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
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

        private void CountButton_Click(object sender, RoutedEventArgs e)
        {
            int count = Directory.EnumerateFiles(DeletedFolderPath, "*", SearchOption.AllDirectories).Count();
            string article = count == 1 ? "is" : "are";
            string suffix = count == 1 ? "" : "s";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = this,
                DialogTitle = "Deleted Folder File Count",
                Icon = DialogIconType.Information,
                What = $"There {article} {count} file{suffix} in the DeletedFiles folder."
            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
            //MessageBox.Show(this, $"There {article} {count} file{suffix} in the DeletedFiles folder.", "Deleted Folder File Count", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
