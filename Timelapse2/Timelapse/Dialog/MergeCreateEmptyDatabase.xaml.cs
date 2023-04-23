using System;
using System.IO;
using System.Windows;
using Path = System.IO.Path;


namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for MergeCreateEmptyDatabase.xaml
    /// </summary>
    public partial class MergeCreateEmptyDatabase
    {
        // The path to the selected
        public string TemplateFilePath { get; private set; }

        private readonly string InitialFolder;

        #region Constructor/Loading
        public MergeCreateEmptyDatabase(Window owner, string initialFolder)
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
        private void ButtonChooseTemplate_OnClick(object sender, RoutedEventArgs e)
        {
            if (Dialogs.TryGetFileFromUserUsingOpenFileDialog(
                    "Select a TimelapseTemplate.tdb file, which should be located in the root folder",
                    this.InitialFolder,
                    String.Format("Template files (*{0})|*{0}", Constant.File.TemplateDatabaseFileExtension),
                    Constant.File.TemplateDatabaseFileExtension,
                    out string templateFilePath) == false)
            {
                // User cancelled
                return;
            }

            // Set and display the chosen template file
            this.TemplateFilePath = templateFilePath;
            this.txtboxTemplateFileName.Text = string.IsNullOrWhiteSpace(this.TemplateFilePath) ? string.Empty : Path.GetFileNameWithoutExtension(this.TemplateFilePath);
            this.OkButton.IsEnabled = !string.IsNullOrWhiteSpace(this.TemplateFilePath); // Enable the button only if a folder was specified

        }
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            if (Directory.GetFiles(Path.GetDirectoryName(this.TemplateFilePath), "*" + Constant.File.FileDatabaseFileExtension).Length > 0)
            {
                if (false == Dialogs.MergeWarningCreateEmptyDdbFileExists(this))
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
    }
}
