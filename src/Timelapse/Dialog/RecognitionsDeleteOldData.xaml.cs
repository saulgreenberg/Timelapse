using System;
using System.Windows;
using Timelapse.Enums;
using Timelapse.Util;

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
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            Owner = owner;
            ImportError = importError;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string errorType = RecognizerImportResultEnum.IncompatibleDetectionCategories == ImportError ? "detection" : "classification";

            Message.Reason = "Conflicts exist between the old versus new " + errorType + " categories." + Environment.NewLine;
            Message.Reason += "Consequently, Timelapse can't merge the new recognition data into the existing recognition data.";
            Message.Solution = "To solve this, completely replace the old recognition data with the new recognition data." + Environment.NewLine;
            Message.Solution += "[li] [e]Completely replace the recognition data[/e] does this" + Environment.NewLine;
            Message.Solution += "[li] [e]Cancel[/e] aborts importing the recognition data";
            this.Message.BuildContentFromProperties();
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
        }

        private void ReplaceButton_Click(object sender, RoutedEventArgs e)
        {
            IsDeleteAllSelected = false;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsDeleteAllSelected = false;
            DialogResult = false;
        }
    }
}
