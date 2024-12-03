using System.Collections.Generic;
using System.Windows;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for TestingDialog.xaml
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public partial class TestSomeCodeDialog
    {
        public TestSomeCodeDialog(Window owner, List<string> problems)
        {
            InitializeComponent();
            Owner = owner;
            StatusFeedback(problems);
        }

        private void TestSomeCodeDialog_OnLoaded(object sender, RoutedEventArgs e)
        {
           
        }

        private void ButtonDoSomething_Click(object sender, RoutedEventArgs e)
        {
            //this.ListData.Items.Insert(0, "Data");
            //this.StatusFeedback("Data entered");
            // DialogUpgradeFiles.DialogUpgradeFilesAndFolders dialogUpdateFiles = new DialogUpgradeFiles.DialogUpgradeFilesAndFolders(@"C:\Users\Owner\Desktop\Test sets\test-0004-imagesInRootFolder", VersionChecks.GetTimelapseCurrentVersionNumber().ToString());
            //dialogUpdateFiles.ShowDialog();
        }

        private void StatusFeedback(string message)
        {
            ListData.Items.Insert(0, message);
        }

        public void StatusFeedback(List<string> messages)
        {
            foreach (string message in messages)
            {
                ListData.Items.Add(message);
            }
        }

        private void ButtonShowStatus_Click(object sender, RoutedEventArgs e)
        {
            StatusFeedback("Latest status");
        }
    }
}
