using System.Windows;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for TestingDialog.xaml
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public partial class TestSomeCodeDialog
    {

        #region Constructor and Initialization

        public TestSomeCodeDialog(Window owner) : base(owner)
        {
            InitializeComponent();
            Owner = owner;
            FormattedDialogHelper.SetupStaticReferenceResolver(TestMessage);
        }

        private void TestSomeCodeDialog_OnLoaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            this.TestMessage.BuildContentFromProperties();

            // Set up a progress handler that will update the progress bar
            InitalizeProgressHandler(BusyCancelIndicator);
        }
        #endregion
    }
}
