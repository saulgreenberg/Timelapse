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
    /// Interaction logic for DetectionsAddSubfolderToJsonFilePaths.xaml
    /// </summary>
    public partial class DetectionsAddSubfolderToJsonFilePaths : Window
    {
        public bool AddSubFolderPrefix = false;
        private string Prefix;
        private string SampleFilePath = String.Empty;
        public DetectionsAddSubfolderToJsonFilePaths(Window owner, string prefix, string sampleFilePath)
        {
            this.Owner = owner;
            this.Prefix = prefix;
            this.SampleFilePath = sampleFilePath;
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            this.Title = String.Format("Add the subfolder \"{0}\" to file paths in the Json?", this.Prefix);
            this.Message.What = String.Format("Your json file is in the subfolder \"{0}\".{1}{1}", this.Prefix, Environment.NewLine) + this.Message.What;
            if (false == String.IsNullOrEmpty(SampleFilePath))
            {
                string addText = String.Format("As an example, the first file path found in the json is: {0} - {1}{0}", Environment.NewLine, SampleFilePath);
                if (SampleFilePath.StartsWith(this.Prefix))
                {
                    addText += String.Format("Since the path includes the subfolder name, the recognizer was probably run within the root folder.{0}", Environment.NewLine) ;
                }
                this.Message.Hint = addText + Environment.NewLine + this.Message.Hint;
            }
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
