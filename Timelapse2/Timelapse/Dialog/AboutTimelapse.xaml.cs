using System;
using System.Windows;
using System.Windows.Navigation;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    public partial class AboutTimelapse : Window
    {
        #region Public Properties
        public Nullable<DateTime> MostRecentCheckForUpdate { get; private set; }
        #endregion

        #region Constructor and Loaded
        public AboutTimelapse(Window owner)
        {
            this.InitializeComponent();
            this.Owner = owner;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            this.NavigateVersionUrl.NavigateUri = Constant.ExternalLinks.TimlapseVersionChangesLink;
            this.NavigateCreativeCommonLicense.NavigateUri = Constant.ExternalLinks.CreativeCommonsLicenseLink;
            this.NavigateAdditionalLicenseDetails.NavigateUri = Constant.ExternalLinks.AdditionalLicenseDetailsLink;

            Version curVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            this.Version.Text = curVersion.ToString();

            this.MostRecentCheckForUpdate = null;
        }
        #endregion

        #region Callbakcs
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void CheckForUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            VersionChecks updater = new VersionChecks(this, Constant.VersionUpdates.ApplicationName, Constant.VersionUpdates.LatestVersionFileNameXML);
            if (updater.TryCheckForNewVersionAndDisplayResultsAsNeeded(true))
            {
                // PERHAPS. This isn't quite right, as the most recent check for update data is (I think) set only if there is a new release
                // (as true is only returned by TryGetAndParseVersion when that happens, I think).
                // Maybe the most recent check date should be done anytime a check is done???
                this.MostRecentCheckForUpdate = DateTime.UtcNow;
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
