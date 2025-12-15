using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Interaction logic for RelativePathEditor.xaml
    /// </summary>
    public partial class RelativePathEditor 
    {
        private readonly FileDatabase FileDatabase;
        public RelativePathEditor(Window owner, FileDatabase fileDatabase) : base(owner)
        {
            InitializeComponent();
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            Owner = owner;
            FileDatabase = fileDatabase;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await Window_LoadedAsync();
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task Window_LoadedAsync()
        {
            this.Message.BuildContentFromProperties();
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            // Set up a progress handler for long-running atomic operation
            InitalizeProgressHandler(BusyCancelIndicator);
            Mouse.OverrideCursor = Cursors.Wait;
            BusyCancelIndicator.Reset(true);

            bool result = await RelativePathControl.AsyncInitialize(this, FileDatabase, ProgressHandler, GlobalReferences.CancelTokenSource);

            BusyCancelIndicator.Reset(false);
            Mouse.OverrideCursor = null;
            if (result == false)
            {
                // Abort this, likely due to a user's progress cancel event
                DialogResult = false;
            }
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = RelativePathControl.WereEditsMade;
        }

        private void SortButton_Click(object sender, RoutedEventArgs e)
        {
            RelativePathControl.RebuildTreeAndNodes(true);
        }

        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            RelativePathControl.ExpandTreeView(true);
        }
        private void ContractAll_Click(object sender, RoutedEventArgs e)
        {
            RelativePathControl.ExpandTreeView(false);
        }

        // Used for debugging
        //private void RefreshCompletelyButton_Click(object sender, RoutedEventArgs e)
        //{
        //    this.RelativePathControl.Initialize(this, this.FileDatabase);
        //}

    }
}
