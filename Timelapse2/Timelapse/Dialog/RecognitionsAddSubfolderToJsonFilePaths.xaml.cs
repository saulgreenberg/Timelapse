using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Timelapse.Enums;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for RecognitionsAddSubfolderToFilePaths.xaml
    /// </summary>
    public partial class RecognitionsAddSubfolderToFilePaths : Window
    {
        public bool AddSubFolderPrefix = false;
        private string Prefix;

        public RecognitionsAddSubfolderToFilePaths(Window owner, string prefix)
        {
            this.Owner = owner;
            this.Prefix = prefix;
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            // this.Title = String.Format("Add the subfolder \"{0}\" to file paths in the Json?", this.Prefix);
            this.Message.What = String.Format("Your recognition file is in the subfolder '{0}'.{1}", this.Prefix, Environment.NewLine)
                + "The image paths it contains appear to be relative to this subfolder rather than " + Environment.NewLine
                + "the root Timelapse folder."; 
        }

        private void AddSubfolder_Click(object sender, RoutedEventArgs e)
        {
            AddSubFolderPrefix = true;
            this.DialogResult = true;
        }

        private void LeaveThingsAsTheyAre_Click(object sender, RoutedEventArgs e)
        {
            AddSubFolderPrefix = false;
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
