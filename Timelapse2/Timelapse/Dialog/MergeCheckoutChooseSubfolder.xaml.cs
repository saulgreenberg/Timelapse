using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ToastNotifications.Position;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Get a folder that is a subfolder of the initial folder,
    /// This will be used (as the text messsage indicates) for a checked out database location
    /// </summary>
    public partial class MergeCheckoutChooseSubfolder : Window
    {
        // The full folder path to the selected folder
        public string FullFolderPath { get; private set; }

        // The relative sub folder path under the initial folder to the selected folder
        public string RelativeFolderPath { get; private set; }

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
            this.RelativeFolderPath = Dialogs.LocateRelativePathUsingOpenFileDialog(this.InitialFolder, String.Empty);
            if (this.RelativeFolderPath == null)
            {
                // User cancelled
                return;
            }

            if (string.IsNullOrWhiteSpace(this.RelativeFolderPath))
            {
                this.txtboxNewFolderName.Text = string.Empty;
                this.FullFolderPath = string.Empty;
            }
            else
            {
                this.txtboxNewFolderName.Text = this.RelativeFolderPath;
                this.FullFolderPath = Path.Combine(this.InitialFolder, this.RelativeFolderPath);
            }
            this.OkButton.IsEnabled = !string.IsNullOrWhiteSpace(this.RelativeFolderPath); // Enable the button only if a folder was specified
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
