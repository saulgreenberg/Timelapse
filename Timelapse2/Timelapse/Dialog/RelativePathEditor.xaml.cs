using System.Windows;
using Timelapse.Database;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for RelativePathEditor.xaml
    /// </summary>
    public partial class RelativePathEditor : Window
    {
        private readonly FileDatabase FileDatabase;
        public RelativePathEditor(Window owner, Timelapse.Database.FileDatabase fileDatabase)
        {
            InitializeComponent();
            this.Owner = owner;
            this.FileDatabase = fileDatabase;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            this.RelativePathControl.Initialize(this, this.FileDatabase);
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = this.RelativePathControl.WereEditsMade;
        }

        private void SortButton_Click(object sender, RoutedEventArgs e)
        {
            this.RelativePathControl.RebuildTreeAndNodes(true);
        }

        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            this.RelativePathControl.ExpandTreeView(true);
        }
        private void ContractAll_Click(object sender, RoutedEventArgs e)
        {
            this.RelativePathControl.ExpandTreeView(false);
        }

        // Used for debugging
        //private void RefreshCompletelyButton_Click(object sender, RoutedEventArgs e)
        //{
        //    this.RelativePathControl.Initialize(this, this.FileDatabase);
        //}

    }
}
