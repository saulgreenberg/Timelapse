using System.Windows;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for TestingDialog.xaml
    /// </summary>
    public partial class TestSomeCodeDialog
    {
        public TestSomeCodeDialog(Window owner)
        {
            InitializeComponent();
            this.Owner = owner;
        }

        private void ButtonDoSomething_Click(object sender, RoutedEventArgs e)
        {
            this.ListData.Items.Insert(0, "Data");
            this.StatusFeedback("Data entered");
            // DialogUpgradeFiles.DialogUpgradeFilesAndFolders dialogUpdateFiles = new DialogUpgradeFiles.DialogUpgradeFilesAndFolders(@"C:\Users\Owner\Desktop\Test sets\test-0004-imagesInRootFolder", VersionChecks.GetTimelapseCurrentVersionNumber().ToString());
            //dialogUpdateFiles.ShowDialog();
        }

        private void StatusFeedback(string message)
        {
            this.ListFeedback.Items.Insert(0, message);
        }

        private void ButtonShowStatus_Click(object sender, RoutedEventArgs e)
        {
            StatusFeedback("Latest status");
        }
    }
}
