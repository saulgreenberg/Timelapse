using System.Windows;
using System.Windows.Controls;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Lets the user Hide or Show various informational dialog boxes.
    /// </summary>
    public partial class DialogsHideOrShow
    {
        #region Private Variabes
        private readonly TimelapseState state;
        #endregion

        #region Constructor, Loaded
        public DialogsHideOrShow(TimelapseState state, Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;
            this.state = state;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Adjust this dialog window position 
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            this.SuppressAmbiguousDatesDialog.IsChecked = this.state.SuppressAmbiguousDatesDialog;
            this.SuppressCsvExportDialog.IsChecked = this.state.SuppressCsvExportDialog;
            this.SuppressCsvExportDialog.IsChecked = this.state.SuppressCsvExportDialog;
            this.SuppressCsvImportPrompt.IsChecked = this.state.SuppressCsvImportPrompt;
            this.SuppressHowDuplicatesWorkPrompt.IsChecked = this.state.SuppressHowDuplicatesWork;
            this.SuppressImportantMessagePrompt.IsChecked = this.state.SuppressOpeningMessageDialog;
            this.SuppressMergeDatabasesPrompt.IsChecked = this.state.SuppressMergeDatabasesPrompt;
            this.SuppressSelectedAmbiguousDatesPrompt.IsChecked = this.state.SuppressSelectedAmbiguousDatesPrompt;
            this.SuppressSelectedCsvExportPrompt.IsChecked = this.state.SuppressSelectedCsvExportPrompt;
            this.SuppressSelectedDarkThresholdPrompt.IsChecked = this.state.SuppressSelectedDarkThresholdPrompt;
            this.SuppressSelectedDateTimeFixedCorrectionPrompt.IsChecked = this.state.SuppressSelectedDateTimeFixedCorrectionPrompt;
            this.SuppressSelectedDateTimeLinearCorrectionPrompt.IsChecked = this.state.SuppressSelectedDateTimeLinearCorrectionPrompt;
            this.SuppressSelectedDaylightSavingsCorrectionPrompt.IsChecked = this.state.SuppressSelectedDaylightSavingsCorrectionPrompt;
            this.SuppressSelectedPopulateFieldFromMetadataPrompt.IsChecked = this.state.SuppressSelectedPopulateFieldFromMetadataPrompt;
            this.SuppressSelectedRereadDatesFromFilesPrompt.IsChecked = this.state.SuppressSelectedRereadDatesFromFilesPrompt;
            this.SuppressWarningToUpdateDBFilesToSQL.IsChecked = this.state.SuppressWarningToUpdateDBFilesToSQLPrompt;
            this.SuppressOpeningWithOlderTimelapseVersionDialog.IsChecked = this.state.SuppressOpeningWithOlderTimelapseVersionDialog;
        }
        #endregion

        #region Callbacks - Suppress Checkboxes
        private void SuppressAmbiguousDatesDialog_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressAmbiguousDatesDialog = (cb.IsChecked == true);
        }

        private void SuppressCsvExportDialog_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressCsvExportDialog = cb.IsChecked == true;
        }

        private void SuppressCsvImportPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressCsvImportPrompt = cb.IsChecked == true;
        }

        private void SuppressHowDuplicatesWorkPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressHowDuplicatesWork = cb.IsChecked == true;
        }
        private void SuppressSelectedAmbiguousDatesPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressSelectedAmbiguousDatesPrompt = cb.IsChecked == true;
        }
        private void SuppressImportantMessagePrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressOpeningMessageDialog = cb.IsChecked == true;
        }

        private void SuppressMergeDatabasesPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressMergeDatabasesPrompt = cb.IsChecked == true;
        }

        private void SuppressSelectedCsvExportPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressSelectedCsvExportPrompt = cb.IsChecked == true;
        }

        private void SuppressSelectedDarkThresholdPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressSelectedDarkThresholdPrompt = cb.IsChecked == true;
        }

        private void SuppressSelectedDateTimeFixedCorrectionPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressSelectedDateTimeFixedCorrectionPrompt = cb.IsChecked == true;
        }

        private void SuppressSelectedDateTimeLinearCorrectionPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressSelectedDateTimeLinearCorrectionPrompt = cb.IsChecked == true;
        }

        private void SuppressSelectedDaylightSavingsCorrectionPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressSelectedDaylightSavingsCorrectionPrompt = cb.IsChecked == true;
        }

        private void SuppressSelectedPopulateFieldFromMetadataPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressSelectedPopulateFieldFromMetadataPrompt = cb.IsChecked == true;
        }

        private void SuppressSelectedRereadDatesFromFilesPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressSelectedRereadDatesFromFilesPrompt = cb.IsChecked == true;
        }

        private void SuppressWarningToUpdateDBFilesToSQL_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressWarningToUpdateDBFilesToSQLPrompt = cb.IsChecked == true;
        }

        private void SuppressOpeningWithOlderTimelapseVersionDialogL_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            this.state.SuppressOpeningWithOlderTimelapseVersionDialog = cb.IsChecked == true;
        }
        #endregion

        #region Callback - Dialog Buttons
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        #endregion


    }
}
