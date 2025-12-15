using System.Windows;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for MergeDatabaseWarningAsDuplicateEntriesPossible.xaml
    /// </summary>
    public partial class MergeDatabaseWarningAsDuplicateEntriesPossible
    {
        public MergeDatabaseWarningAsDuplicateEntriesPossible(Window owner, string details) 
        {
            InitializeComponent();
            InitializeComponent();
            Owner = owner;
            TBDetails.Text += details;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            this.Message.BuildContentFromProperties();
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
        }

        private void DoMergeButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
