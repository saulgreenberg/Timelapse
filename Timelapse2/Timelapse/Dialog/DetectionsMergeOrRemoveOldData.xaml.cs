using System.Windows;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for DetectionsMergeOrRemoveOldData.xaml
    /// </summary>
    public partial class DetectionsMergeOrRemoveOldData : Window
    {
        public bool IsMergeSelected = false;
        public DetectionsMergeOrRemoveOldData(Window owner)
        {
            InitializeComponent();
            this.Owner = owner;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
        }


        private void MergeButton_Click(object sender, RoutedEventArgs e)
        {
            this.IsMergeSelected = true;
            this.DialogResult = true;
        }

        private void ReplaceButton_Click(object sender, RoutedEventArgs e)
        {
            this.IsMergeSelected = false;
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.IsMergeSelected = false;
            this.DialogResult = false;
        }
    }
}
