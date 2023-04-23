using System;
using System.IO;
using System.Windows;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Get a folder that is a subfolder of the initial folder,
    /// This will be used (as the text messsage indicates) for a checked out database location
    /// </summary>
    public partial class MergeCheckoutChooseSubfolder
    {
        // The full folder path to the selected folder
        public string FullSubFolderPath { get; private set; }

        // The relative sub folder path under the initial folder to the selected folder
        public string RelativeSubFolderPath { get; private set; }

        private readonly string InitialFolder;

        #region Constructor/Loading
        public MergeCheckoutChooseSubfolder(Window owner, string initialFolder)
        {
            InitializeComponent();
            this.Owner = owner;
            this.InitialFolder = initialFolder;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            this.OkButton.IsEnabled = false;
        }
        #endregion

        #region Callbacks
        private void ButtonChooseFolder_OnClick(object sender, RoutedEventArgs e)
        {
            this.RelativeSubFolderPath = Dialogs.LocateRelativePathUsingOpenFileDialog(this.InitialFolder, String.Empty);
            if (this.RelativeSubFolderPath == null)
            {
                // User cancelled
                return;
            }

            if (string.IsNullOrWhiteSpace(this.RelativeSubFolderPath))
            {
                this.txtboxNewFolderName.Text = string.Empty;
                this.FullSubFolderPath = string.Empty;
            }
            else
            {
                this.txtboxNewFolderName.Text = this.RelativeSubFolderPath;
                this.FullSubFolderPath = Path.Combine(this.InitialFolder, this.RelativeSubFolderPath);
            }
            this.OkButton.IsEnabled = !string.IsNullOrWhiteSpace(this.RelativeSubFolderPath); // Enable the button only if a folder was specified
        }
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            if (Directory.GetFiles(Path.GetDirectoryName(this.FullSubFolderPath), "*" + Constant.File.FileDatabaseFileExtension).Length > 0)
            {
                if (false == Dialogs.MergeWarningCheckOutDdbFileExists(this))
                {
                    return;
                }
            }
            this.DialogResult = true;
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
