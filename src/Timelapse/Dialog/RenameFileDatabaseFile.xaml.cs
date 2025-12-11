using System.IO;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Util;
using File = Timelapse.Constant.File;

namespace Timelapse.Dialog
{
    public partial class RenameFileDatabaseFile
    {
        #region Public Properties and Private Variables
        public string NewFilename { get; private set; }
        private readonly string currentFileName;
        #endregion

        #region Constructor, Loaded
        public RenameFileDatabaseFile(string fileName, Window owner)
        {
            InitializeComponent();
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);

            currentFileName = fileName;
            Owner = owner;
            NewFilename = Path.GetFileNameWithoutExtension(fileName);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Message.BuildContentFromProperties();
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            runOriginalFileName.Text = currentFileName;
            txtboxNewFileName.Text = NewFilename;
            OkButton.IsEnabled = false;
            txtboxNewFileName.TextChanged += TxtboxNewFileName_TextChanged;
        }
        #endregion

        #region Callbacks
        private void TxtboxNewFileName_TextChanged(object sender, TextChangedEventArgs e)
        {
            NewFilename = txtboxNewFileName.Text + File.FileDatabaseFileExtension;
            OkButton.IsEnabled = !NewFilename.Equals(currentFileName); // Enable the button only if the two names differ
        }

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
