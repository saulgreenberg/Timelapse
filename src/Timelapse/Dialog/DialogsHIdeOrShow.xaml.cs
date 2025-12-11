using System.Windows;
using System.Windows.Controls;
using Timelapse.State;
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
            InitializeComponent();
            Owner = owner;
            this.state = state;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            this.Message.BuildContentFromProperties();
            // Adjust this dialog window position
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            SuppressAmbiguousDatesDialog.IsChecked = state.SuppressAmbiguousDatesDialog;
            SuppressCsvExportDialog.IsChecked = state.SuppressCsvExportDialog;
            SuppressCsvExportDialog.IsChecked = state.SuppressCsvExportDialog;
            SuppressCsvImportPrompt.IsChecked = state.SuppressCsvImportPrompt;
            SuppressHowDuplicatesWorkPrompt.IsChecked = state.SuppressHowDuplicatesWork;
            SuppressImportantMessagePrompt.IsChecked = state.SuppressOpeningMessageDialog;
            SuppressSelectedAmbiguousDatesPrompt.IsChecked = state.SuppressSelectedAmbiguousDatesPrompt;
            SuppressSelectedCsvExportPrompt.IsChecked = state.SuppressSelectedCsvExportPrompt;
            SuppressSelectedDarkThresholdPrompt.IsChecked = state.SuppressSelectedDarkThresholdPrompt;
            SuppressSelectedDateTimeFixedCorrectionPrompt.IsChecked = state.SuppressSelectedDateTimeFixedCorrectionPrompt;
            SuppressSelectedDateTimeLinearCorrectionPrompt.IsChecked = state.SuppressSelectedDateTimeLinearCorrectionPrompt;
            SuppressSelectedDaylightSavingsCorrectionPrompt.IsChecked = state.SuppressSelectedDaylightSavingsCorrectionPrompt;
            SuppressSelectedPopulateFieldFromMetadataPrompt.IsChecked = state.SuppressSelectedPopulateFieldFromMetadataPrompt;
            SuppressSelectedRereadDatesFromFilesPrompt.IsChecked = state.SuppressSelectedRereadDatesFromFilesPrompt;
            SuppressShortcutDetectedPrompt.IsChecked = state.SuppressShortcutDetectedPrompt;
           
        // this.SuppressWarningToUpdateDBFilesToSQL.IsChecked = this.state.SuppressWarningToUpdateDBFilesToSQLPrompt;
        SuppressOpeningWithOlderTimelapseVersionDialog.IsChecked = state.SuppressOpeningWithOlderTimelapseVersionDialog;
            SuppressPropagateFromLastNonEmptyValuePrompt.IsChecked = state.SuppressPropagateFromLastNonEmptyValuePrompt;
        }
        #endregion

        #region Callbacks - Suppress Checkboxes
        private void SuppressAmbiguousDatesDialog_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            state.SuppressAmbiguousDatesDialog = (cb.IsChecked == true);
        }

        private void SuppressCsvExportDialog_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            state.SuppressCsvExportDialog = cb.IsChecked == true;
        }

        private void SuppressCsvImportPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            state.SuppressCsvImportPrompt = cb.IsChecked == true;
        }

        private void SuppressHowDuplicatesWorkPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            state.SuppressHowDuplicatesWork = cb.IsChecked == true;
        }
        private void SuppressSelectedAmbiguousDatesPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            state.SuppressSelectedAmbiguousDatesPrompt = cb.IsChecked == true;
        }
        private void SuppressImportantMessagePrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            state.SuppressOpeningMessageDialog = cb.IsChecked == true;
        }

        private void SuppressSelectedCsvExportPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            state.SuppressSelectedCsvExportPrompt = cb.IsChecked == true;
        }

        private void SuppressSelectedDarkThresholdPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            state.SuppressSelectedDarkThresholdPrompt = cb.IsChecked == true;
        }

        private void SuppressSelectedDateTimeFixedCorrectionPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            state.SuppressSelectedDateTimeFixedCorrectionPrompt = cb.IsChecked == true;
        }

        private void SuppressSelectedDateTimeLinearCorrectionPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            state.SuppressSelectedDateTimeLinearCorrectionPrompt = cb.IsChecked == true;
        }

        private void SuppressSelectedDaylightSavingsCorrectionPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            state.SuppressSelectedDaylightSavingsCorrectionPrompt = cb.IsChecked == true;
        }

        private void SuppressSelectedPopulateFieldFromMetadataPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            state.SuppressSelectedPopulateFieldFromMetadataPrompt = cb.IsChecked == true;
        }

        private void SuppressSelectedRereadDatesFromFilesPrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            state.SuppressSelectedRereadDatesFromFilesPrompt = cb.IsChecked == true;
        }

        private void SuppressShortcutDetectedPrompt_Click(object sender, RoutedEventArgs _)
        {
            CheckBox cb = (CheckBox)sender;
            state.SuppressShortcutDetectedPrompt = cb.IsChecked == true;
        }

        // Unused for now, but keep around in case we need something like this in the future
        // ReSharper disable once UnusedMember.Local
        private void SuppressWarningToUpdateDBFilesToSQL_Click(object sender, RoutedEventArgs _)
        {
            CheckBox cb = (CheckBox)sender;
            state.SuppressWarningToUpdateDBFilesToSQLPrompt = cb.IsChecked == true;
        }

        private void SuppressOpeningWithOlderTimelapseVersionDialog_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            state.SuppressOpeningWithOlderTimelapseVersionDialog = cb.IsChecked == true;
        }
        #endregion


        private void SuppressPropagateFromLastNonEmptyValuePrompt_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            state.SuppressPropagateFromLastNonEmptyValuePrompt = cb.IsChecked == true;
        }

        #region Callback - Dialog Buttons
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        #endregion

    }
}
