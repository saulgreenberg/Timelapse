using System;
using System.Windows;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for RecognitionsAddSubfolderToFilePaths.xaml
    /// </summary>
    public partial class RecognitionsAddSubfolderToFilePaths
    {
        public bool AddSubFolderPrefix;
        private readonly string Prefix;

        public RecognitionsAddSubfolderToFilePaths(Window owner, string prefix)
        {
            Owner = owner;
            Prefix = prefix;
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            // this.Title = String.Format("Add the subfolder \"{0}\" to file paths in the Json?", this.Prefix);
            Message.What = $"Your recognition file is in the subfolder '{Prefix}'.{Environment.NewLine}"
                                + "The image paths it contains appear to be relative to this subfolder rather than " + Environment.NewLine
                                + "the root Timelapse folder.";
        }

        private void AddSubfolder_Click(object sender, RoutedEventArgs e)
        {
            AddSubFolderPrefix = true;
            DialogResult = true;
        }

        private void LeaveThingsAsTheyAre_Click(object sender, RoutedEventArgs e)
        {
            AddSubFolderPrefix = false;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
