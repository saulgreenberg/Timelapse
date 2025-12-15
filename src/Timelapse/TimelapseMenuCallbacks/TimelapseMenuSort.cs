using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;
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
            FilePlayer_Stop(); // In case the FilePlayer is going
        }
        #endregion

        #region Sort callbacks
        // Handle the standard menu sorting items
        private async void MenuItemSort_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await MenuItemSort_ClickAsync(sender);
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task MenuItemSort_ClickAsync(object sender)
        {

            // While this should never happen, don't do anything if we don's have any data
            if (DataHandler == null || DataHandler.FileDatabase == null)
            {
                return;
            }

            MenuItem mi = (MenuItem)sender;
            SortTerm sortTerm1 = new();
            SortTerm sortTerm2 = new();
            switch (mi.Name)
            {
                case "MenuItemSortByRelativPathDateTime":
                    // The default
                    sortTerm1.DataLabel = DatabaseColumn.RelativePath;
                    sortTerm1.DisplayLabel = DatabaseColumn.RelativePath;
                    sortTerm1.ControlType = DatabaseColumn.RelativePath;
                    sortTerm1.IsAscending = BooleanValue.True;
                    sortTerm2.DataLabel = DatabaseColumn.DateTime;

                    sortTerm2.DisplayLabel = DatabaseColumn.DateTime;
                    sortTerm2.ControlType = DatabaseColumn.DateTime;
                    sortTerm2.IsAscending = BooleanValue.True;
                    break;
                case "MenuItemSortByDateTime":
                    sortTerm1.DataLabel = DatabaseColumn.DateTime;
                    sortTerm1.DisplayLabel = DatabaseColumn.DateTime;
                    sortTerm1.ControlType = DatabaseColumn.DateTime;
                    sortTerm1.IsAscending = BooleanValue.True;
                    break;
                case "MenuItemSortByFileName":
                    sortTerm1.DataLabel = DatabaseColumn.File;
                    sortTerm1.DisplayLabel = DatabaseColumn.File;
                    sortTerm1.ControlType = DatabaseColumn.File;
                    sortTerm1.IsAscending = BooleanValue.True;
                    break;
                case "MenuItemSortById":
                    sortTerm1.DataLabel = DatabaseColumn.ID;
                    sortTerm1.DisplayLabel = DatabaseColumn.ID;
                    sortTerm1.ControlType = DatabaseColumn.ID;
                    sortTerm1.IsAscending = BooleanValue.True;
                    break;
            }
            // Record the sort terms in the image set
            DataHandler.FileDatabase.ImageSet.SetSortTerms(sortTerm1, sortTerm2);

            // Do the sort, showing feedback in the status bar and by checking the appropriate menu item
            await DoSortAndShowSortFeedbackAsync(true).ConfigureAwait(true);
        }

        // Custom Sort: raises a dialog letting the user specify their sort criteria
        private async void MenuItemSortCustom_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await MenuItemSortCustom_ClickAsync();
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task MenuItemSortCustom_ClickAsync()
        {
            // Raise a dialog where user can specify the sorting criteria
            CustomSort customSort = new(DataHandler.FileDatabase)
            {
                Owner = this
            };
            if (customSort.ShowDialog() == true)
            {
                DataHandler?.FileDatabase?.ImageSet.SetSortTerms(customSort.SortTerm1, customSort.SortTerm2);
                await DoSortAndShowSortFeedbackAsync(true).ConfigureAwait(true);
            }
            else
            {
                // Ensure the checkmark appears next to the correct menu item
                ShowSortFeedback(true);
            }
        }

        // Refresh the sort: based on the current sort criteria.
        // Useful when, for example, the user has sorted a view, but then changed some data values where items are no longer sorted correctly.
        private async void MenuItemSortResort_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await MenuItemSortResort_ClickAsync();
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task MenuItemSortResort_ClickAsync()
        {
            await DoSortAndShowSortFeedbackAsync(false).ConfigureAwait(true);
        }
        #endregion

        #region Helper functions
        // Do the sort and show feedback to the user. 
        // Only invoked by the above menu functions 
        private async Task DoSortAndShowSortFeedbackAsync(bool updateMenuChecks)
        {
            if (DataHandler.ImageCache.Current == null)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(DataHandler.ImageCache.Current));
                return;
            }
            // Sync the current sort settings into the actual database. While this is done
            // on closing Timelapse, this will save it on the odd chance that Timelapse crashes before it exits.
            DataHandler.FileDatabase.UpdateSyncImageSetToDatabase(); // SAULXXX CHECK IF THIS IS NEEDED

            BusyCancelIndicator.IsBusy = true;
            // Reselect the images, which re-sorts them to the current sort criteria. 
            await FilesSelectAndShowAsync(DataHandler.ImageCache.Current.ID, DataHandler.FileDatabase.FileSelectionEnum).ConfigureAwait(true);
            BusyCancelIndicator.IsBusy = false;

            // sets up various status indicators in the UI
            ShowSortFeedback(updateMenuChecks);
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
                sortTerm[i] = DataHandler.FileDatabase.ImageSet.GetSortTerm(i);
            }

            // If instructed to do so, Reset menu item checkboxes based on the current sort terms.
            if (updateMenuChecks == false)
            {
                return string.Empty;
            }

            MenuItemSortByRelativPathDateTime.IsChecked = false;
            MenuItemSortByDateTime.IsChecked = false;
            MenuItemSortByFileName.IsChecked = false;
            MenuItemSortById.IsChecked = false;
            MenuItemSortCustom.IsChecked = false;
            

            // Determine which selection best fits the sort terms (e.g., a custom selection on just ID will be ID rather than Custom)
            if (sortTerm[0].DataLabel == DatabaseColumn.DateTime && sortTerm[0].IsAscending == BooleanValue.True && string.IsNullOrEmpty(sortTerm[1].DataLabel))
            {
                MenuItemSortByDateTime.IsChecked = true;
            }
            else if (sortTerm[0].DataLabel == DatabaseColumn.RelativePath && sortTerm[0].IsAscending == BooleanValue.True && sortTerm[1].DataLabel == DatabaseColumn.DateTime && sortTerm[1].IsAscending == BooleanValue.True)
            {
                defaultSort = true;
                MenuItemSortByRelativPathDateTime.IsChecked = true;
            }
            else if (sortTerm[0].DataLabel == DatabaseColumn.ID && sortTerm[0].IsAscending == BooleanValue.True && string.IsNullOrEmpty(sortTerm[1].DataLabel))
            {
                MenuItemSortById.IsChecked = true;
            }
            else if (sortTerm[0].DataLabel == DatabaseColumn.File && sortTerm[0].IsAscending == BooleanValue.True && string.IsNullOrEmpty(sortTerm[1].DataLabel))
            {
                MenuItemSortByFileName.IsChecked = true;
            }
            else
            {
                MenuItemSortCustom.IsChecked = true;
            }
            // Provide feedback in the status bar of what sort terms are being used
            string sortFeedback = StatusBar.SetSort(sortTerm[0].DataLabel, sortTerm[0].IsAscending == BooleanValue.True, sortTerm[1].DataLabel, sortTerm[1].IsAscending == BooleanValue.True);
            // If its the default sort,then return nothing (which means no notification toast will be displayed)
            return defaultSort
                ? string.Empty
                : sortFeedback;
        }
        #endregion
    }
}
