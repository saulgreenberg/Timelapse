using System.Windows;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for MergingDatabaseWarningAsDuplicateEntriesPossible.xaml
    /// </summary>
    public partial class MergingDatabaseWarningAsDuplicateEntriesPossible
    {
        public MergingDatabaseWarningAsDuplicateEntriesPossible(Window owner, string details) 
        {
            InitializeComponent();
            InitializeComponent();
            Owner = owner;
            TBDetails.Text += details;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
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
