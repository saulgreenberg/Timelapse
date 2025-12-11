using System;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;
using Timelapse.Constant;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    public partial class AboutTimelapse
    {
        #region Public Properties
        public DateTime? MostRecentCheckForUpdate { get; private set; }
        #endregion

        #region Constructor and Loaded
        public AboutTimelapse(Window owner)
        {
            InitializeComponent();
            // Set up static reference resolver for FormattedMessageContent
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            Owner = owner;
            Message.BuildContentFromProperties();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            NavigateVersionUrl.NavigateUri = ExternalLinks.TimlapseVersionChangesLink;
            NavigateCreativeCommonLicense.NavigateUri = ExternalLinks.CreativeCommonsLicenseLink;
            NavigateAdditionalLicenseDetails.NavigateUri = ExternalLinks.AdditionalLicenseDetailsLink;

            Version curVersion = Assembly.GetExecutingAssembly().GetName().Version;
            Version.Text = curVersion + Constant.DatabaseValues.VersionPatchNumber;

            MostRecentCheckForUpdate = null;
        }
        #endregion

        #region Callbacks
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CheckForUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            VersionChecks updater = new(this, VersionUpdates.ApplicationName, VersionUpdates.LatestVersionFileNameXML);
            if (updater.TryCheckForNewVersionAndDisplayResultsAsNeeded(true))
            {
                // PERHAPS. This isn't quite right, as the most recent check for update data is (I think) set only if there is a new release
                // (as true is only returned by TryGetAndParseVersion when that happens, I think).
                // Maybe the most recent check date should be done anytime a check is done???
                MostRecentCheckForUpdate = DateTime.UtcNow;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            ProcessExecution.TryProcessStart(e.Uri);
            e.Handled = true;
        }
        #endregion
    }
}
