using System;
using System.Windows;
using Timelapse.Enums;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for RecognitionsDeleteOldData.xaml
    /// </summary>
    public partial class RecognitionsDeleteOldData
    {
        public bool IsDeleteAllSelected;
        private readonly RecognizerImportResultEnum ImportError;
        public RecognitionsDeleteOldData(Window owner, RecognizerImportResultEnum importError)
        {
            InitializeComponent();
            this.Owner = owner;
            ImportError = importError;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string errorType = RecognizerImportResultEnum.IncompatableDetectionCategories == ImportError ? "detection" : "classification";

            this.Message.Reason = "Conflicts exist between the old versus new " + errorType + " categories." + Environment.NewLine;
            this.Message.Reason += "Consequently, Timelapse can't merge the new recognition data into the existing recognition data.";
            this.Message.Solution = "To solve this, completely replace the old recognition data with the new recognition data." + Environment.NewLine;
            this.Message.Solution += "\u2022 'Completely replace the recognition data' does this" + Environment.NewLine;
            this.Message.Solution += "\u2022 'Cancel' aborts importing the recognition data";
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
        }

        private void ReplaceButton_Click(object sender, RoutedEventArgs e)
        {
            this.IsDeleteAllSelected = false;
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.IsDeleteAllSelected = false;
            this.DialogResult = false;
        }
    }
}
