using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Timelapse.Dialog
{
    public partial class RenameFileDatabaseFile : Window
    {
        #region Public Properties and Private Variables
        public string NewFilename { get; private set; }
        private readonly string currentFileName;
        #endregion

        #region Constructor, Loaded
        public RenameFileDatabaseFile(string fileName, Window owner)
        {
            this.InitializeComponent();

            this.currentFileName = fileName;
            this.Owner = owner;
            this.NewFilename = Path.GetFileNameWithoutExtension(fileName);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            this.runOriginalFileName.Text = this.currentFileName;
            this.txtboxNewFileName.Text = this.NewFilename;
            this.OkButton.IsEnabled = false;
            this.txtboxNewFileName.TextChanged += this.TxtboxNewFileName_TextChanged;
        }
        #endregion

        #region Callbacks
        private void TxtboxNewFileName_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.NewFilename = this.txtboxNewFileName.Text + Constant.File.FileDatabaseFileExtension;
            this.OkButton.IsEnabled = !this.NewFilename.Equals(this.currentFileName); // Enable the button only if the two names differ
        }

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
