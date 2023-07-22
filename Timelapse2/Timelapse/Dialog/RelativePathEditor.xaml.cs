using System.Windows;
using Timelapse.Database;
using System.Windows.Input;
using Timelapse.DataStructures;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for RelativePathEditor.xaml
    /// </summary>
    public partial class RelativePathEditor 
    {
        private readonly FileDatabase FileDatabase;
        public RelativePathEditor(Window owner, Timelapse.Database.FileDatabase fileDatabase) : base(owner)
        {
            InitializeComponent();
            this.Owner = owner;
            this.FileDatabase = fileDatabase;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            
            // Set up a progress handler for long-running atomic operation
            this.InitalizeProgressHandler(this.BusyCancelIndicator);
            Mouse.OverrideCursor = Cursors.Wait;
            this.BusyCancelIndicator.Reset(true);

            bool result = await this.RelativePathControl.AsyncInitialize(this, this.FileDatabase, this.ProgressHandler, GlobalReferences.CancelTokenSource);

            this.BusyCancelIndicator.Reset(false);
            Mouse.OverrideCursor = null;
            if (result == false)
            {
                // Abort this, likely due to a user's progress cancel event
                this.DialogResult = false;
            }
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
