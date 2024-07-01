using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Controls;
using Timelapse.DebuggingSupport;
using Timelapse.SearchingAndSorting;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    // Sort Menu Callbacks
    public partial class TimelapseWindow
    {
        #region Sort sub-menu opening
        private void Sort_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            this.FilePlayer_Stop(); // In case the FilePlayer is going
        }
        #endregion

        #region Sort callbacks
        // Handle the standard menu sorting items
        private async void MenuItemSort_Click(object sender, RoutedEventArgs e)
        {

            // While this should never happen, don't do anything if we don's have any data
            if (this.DataHandler == null || this.DataHandler.FileDatabase == null)
            {
                return;
            }

            MenuItem mi = (MenuItem)sender;
            SortTerm sortTerm1 = new SortTerm();
            SortTerm sortTerm2 = new SortTerm();
            switch (mi.Name)
            {
                case "MenuItemSortByRelativPathDateTime":
                    // The default
                    sortTerm1.DataLabel = Constant.DatabaseColumn.RelativePath;
                    sortTerm1.DisplayLabel = Constant.DatabaseColumn.RelativePath;
                    sortTerm1.ControlType = Constant.DatabaseColumn.RelativePath;
                    sortTerm1.IsAscending = Constant.BooleanValue.True;
                    sortTerm2.DataLabel = Constant.DatabaseColumn.DateTime;

                    sortTerm2.DisplayLabel = Constant.DatabaseColumn.DateTime;
                    sortTerm2.ControlType = Constant.DatabaseColumn.DateTime;
                    sortTerm2.IsAscending = Constant.BooleanValue.True;
                    break;
                case "MenuItemSortByDateTime":
                    sortTerm1.DataLabel = Constant.DatabaseColumn.DateTime;
                    sortTerm1.DisplayLabel = Constant.DatabaseColumn.DateTime;
                    sortTerm1.ControlType = Constant.DatabaseColumn.DateTime;
                    sortTerm1.IsAscending = Constant.BooleanValue.True;
                    break;
                case "MenuItemSortByFileName":
                    sortTerm1.DataLabel = Constant.DatabaseColumn.File;
                    sortTerm1.DisplayLabel = Constant.DatabaseColumn.File;
                    sortTerm1.ControlType = Constant.DatabaseColumn.File;
                    sortTerm1.IsAscending = Constant.BooleanValue.True;
                    break;
                case "MenuItemSortById":
                    sortTerm1.DataLabel = Constant.DatabaseColumn.ID;
                    sortTerm1.DisplayLabel = Constant.DatabaseColumn.ID;
                    sortTerm1.ControlType = Constant.DatabaseColumn.ID;
                    sortTerm1.IsAscending = Constant.BooleanValue.True;
                    break;
            }
            // Record the sort terms in the image set
            this.DataHandler.FileDatabase.ImageSet.SetSortTerms(sortTerm1, sortTerm2);

            // Do the sort, showing feedback in the status bar and by checking the appropriate menu item
            await this.DoSortAndShowSortFeedbackAsync(true).ConfigureAwait(true);
        }

        // Custom Sort: raises a dialog letting the user specify their sort criteria
        private async void MenuItemSortCustom_Click(object sender, RoutedEventArgs e)
        {
            // Raise a dialog where user can specify the sorting criteria
            Dialog.CustomSort customSort = new Dialog.CustomSort(this.DataHandler.FileDatabase)
            {
                Owner = this
            };
            if (customSort.ShowDialog() == true)
            {
                this.DataHandler?.FileDatabase?.ImageSet.SetSortTerms(customSort.SortTerm1, customSort.SortTerm2);
                await this.DoSortAndShowSortFeedbackAsync(true).ConfigureAwait(true);
            }
            else
            {
                // Ensure the checkmark appears next to the correct menu item 
                this.ShowSortFeedback(true);
            }
        }

        // Refresh the sort: based on the current sort criteria. 
        // Useful when, for example, the user has sorted a view, but then changed some data values where items are no longer sorted correctly.
        private async void MenuItemSortResort_Click(object sender, RoutedEventArgs e)
        {
            await this.DoSortAndShowSortFeedbackAsync(false).ConfigureAwait(true);
        }
        #endregion

        #region Helper functions
        // Do the sort and show feedback to the user. 
        // Only invoked by the above menu functions 
        private async Task DoSortAndShowSortFeedbackAsync(bool updateMenuChecks)
        {
            if (this.DataHandler.ImageCache.Current == null)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(this.DataHandler.ImageCache.Current));
                return;
            }
            // Sync the current sort settings into the actual database. While this is done
            // on closing Timelapse, this will save it on the odd chance that Timelapse crashes before it exits.
            this.DataHandler.FileDatabase.UpdateSyncImageSetToDatabase(); // SAULXXX CHECK IF THIS IS NEEDED

            this.BusyCancelIndicator.IsBusy = true;
            // Reselect the images, which re-sorts them to the current sort criteria. 
            await this.FilesSelectAndShowAsync(this.DataHandler.ImageCache.Current.ID, this.DataHandler.FileDatabase.FileSelectionEnum).ConfigureAwait(true);
            this.BusyCancelIndicator.IsBusy = false;

            // sets up various status indicators in the UI
            this.ShowSortFeedback(updateMenuChecks);
        }

        // Show feedback in the UI based on the sort selection
        // Record the current sort state
        // Note: invoked by the above menu functions AND the OnFolderLoadingComplete method
        // SAULXXX WE MAY WANT TO MOVE THIS ELSEWHERE
        private string ShowSortFeedback(bool updateMenuChecks)
        {
            bool defaultSort = false;

            // Get the two sort terms
            SortTerm[] sortTerm = new SortTerm[2];
            for (int i = 0; i <= 1; i++)
            {
                sortTerm[i] = this.DataHandler.FileDatabase.ImageSet.GetSortTerm(i);
            }

            // If instructed to do so, Reset menu item checkboxes based on the current sort terms.
            if (updateMenuChecks == false)
            {
                return string.Empty;
            }

            this.MenuItemSortByRelativPathDateTime.IsChecked = false;
            this.MenuItemSortByDateTime.IsChecked = false;
            this.MenuItemSortByFileName.IsChecked = false;
            this.MenuItemSortById.IsChecked = false;
            this.MenuItemSortCustom.IsChecked = false;
            

            // Determine which selection best fits the sort terms (e.g., a custom selection on just ID will be ID rather than Custom)
            if (sortTerm[0].DataLabel == Constant.DatabaseColumn.DateTime && sortTerm[0].IsAscending == Constant.BooleanValue.True && string.IsNullOrEmpty(sortTerm[1].DataLabel))
            {
                this.MenuItemSortByDateTime.IsChecked = true;
            }
            else if (sortTerm[0].DataLabel == Constant.DatabaseColumn.RelativePath && sortTerm[0].IsAscending == Constant.BooleanValue.True && sortTerm[1].DataLabel == Constant.DatabaseColumn.DateTime && sortTerm[1].IsAscending == Constant.BooleanValue.True)
            {
                defaultSort = true;
                this.MenuItemSortByRelativPathDateTime.IsChecked = true;
            }
            else if (sortTerm[0].DataLabel == Constant.DatabaseColumn.ID && sortTerm[0].IsAscending == Constant.BooleanValue.True && string.IsNullOrEmpty(sortTerm[1].DataLabel))
            {
                this.MenuItemSortById.IsChecked = true;
            }
            else if (sortTerm[0].DataLabel == Constant.DatabaseColumn.File && sortTerm[0].IsAscending == Constant.BooleanValue.True && string.IsNullOrEmpty(sortTerm[1].DataLabel))
            {
                this.MenuItemSortByFileName.IsChecked = true;
            }
            else
            {
                this.MenuItemSortCustom.IsChecked = true;
            }
            // Provide feedback in the status bar of what sort terms are being used
            string sortFeedback = this.StatusBar.SetSort(sortTerm[0].DataLabel, sortTerm[0].IsAscending == Constant.BooleanValue.True, sortTerm[1].DataLabel, sortTerm[1].IsAscending == Constant.BooleanValue.True);
            // If its the default sort,then return nothing (which means no notification toast will be displayed)
            return defaultSort
                ? string.Empty
                : sortFeedback;
        }
        #endregion
    }
}
