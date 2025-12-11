using System.Windows;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for TestingDialog.xaml
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public partial class TestSomeCodeDialog
    {
        public TestSomeCodeDialog(Window owner)
        {
            InitializeComponent();
            Owner = owner;
        }

        private void TestSomeCodeDialog_OnLoaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            TimelapseWindow timelapseWindow = this.Owner as TimelapseWindow;


            if (timelapseWindow?.DataHandler?.FileDatabase?.Database != null && timelapseWindow.DataHandler.FileDatabase.DetectionsExists())
            {
            }
        }
    }
}
