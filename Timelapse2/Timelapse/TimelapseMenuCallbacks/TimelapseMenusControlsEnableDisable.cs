using System;
using System.Windows;
using Timelapse.Controls;

namespace Timelapse
{
    // Enabling or Disabling Menus and Controls
    public partial class TimelapseWindow
    {
        #region Enable Or disable menus and controls
        private void EnableOrDisableMenusAndControls()
        {
            bool imageSetAvailable = this.IsFileDatabaseAvailable(); // A possible empty image set is loaded
            bool filesSelected = imageSetAvailable && this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles > 0; // A non-empty image set is loaded

            // Depending upon whether images exist in the data set,
            // enable / disable menus and menu items as needed

            // File menu
            this.MenuItemAddFilesToImageSet.IsEnabled = imageSetAvailable;
            this.MenuItemLoadFiles.IsEnabled = !imageSetAvailable;
            this.MenuItemRecentImageSets.IsEnabled = !imageSetAvailable;
            this.MenuItemExportThisImage.IsEnabled = filesSelected;
            this.MenuItemExportSelectedImages.IsEnabled = filesSelected;
            this.MenuItemExportAsCsvAndPreview.IsEnabled = filesSelected;
            this.MenuItemExportAsCsv.IsEnabled = filesSelected;
            this.MenuItemImportFromCsv.IsEnabled = filesSelected;
            this.MenuItemRenameFileDatabaseFile.IsEnabled = filesSelected;
            this.MenuFileCloseImageSet.IsEnabled = imageSetAvailable;
            this.MenuItemImportDetectionData.Visibility = Visibility.Visible;
            this.MenuItemImportDetectionData.IsEnabled = imageSetAvailable;

            // Edit menu
            this.MenuItemEdit.IsEnabled = filesSelected;
            this.MenuItemDeleteCurrentFile.IsEnabled = filesSelected;
            this.MenuItemRestoreDefaults.IsEnabled = filesSelected;
            this.MenuItemPopulateFieldFromMetadata.IsEnabled = filesSelected;
            this.MenuItemEpisodePopulateField.IsEnabled = filesSelected;

            // Options menu
            // always enable at top level when an image set exists so that image set advanced options are accessible
            this.MenuItemOptions.IsEnabled = true; // imageSetAvailable;
            this.MenuItemAudioFeedback.IsEnabled = filesSelected;
            this.MenuItemImageAdjuster.IsEnabled = filesSelected;
            this.MenuItemEpisodeOptions.IsEnabled = filesSelected;
            this.MenuItemEpisodeShowHide.IsEnabled = filesSelected;
            this.MenuItemMagnifyingGlass.IsEnabled = imageSetAvailable;
            this.MenuItemDisplayMagnifyingGlass.IsChecked = imageSetAvailable && this.State.MagnifyingGlassOffsetLensEnabled;
            this.MenuItemImageAdjuster.IsEnabled = filesSelected;
            this.MenuItemDialogsOnOrOff.IsEnabled = true;
            this.MenuItemPreferences.IsEnabled = true;

            // View menu
            this.MenuItemView.IsEnabled = filesSelected;

            // Select menu
            this.MenuItemSelect.IsEnabled = filesSelected;

            // Sort menu
            this.MenuItemSort.IsEnabled = filesSelected;

            // Recognitions menu
            this.MenuItemRecognitions.IsEnabled = filesSelected;
            this.MenuItemImportDetectionData.IsEnabled = filesSelected;

            // Windows menu is always enabled

            // Enablement state of the various other UI components.
            this.ControlsPanel.IsEnabled = filesSelected;  // If images don't exist, the user shouldn't be allowed to interact with the control tray
            this.FileNavigatorSlider.IsEnabled = filesSelected;
            this.MarkableCanvas.IsEnabled = filesSelected;
            this.MarkableCanvas.MagnifiersEnabled = filesSelected && this.State.MagnifyingGlassOffsetLensEnabled;

            if (filesSelected == false)
            {
                this.DuplicateIndicatorInMainWindow.Visibility = Visibility.Collapsed;
                this.FileShow(Constant.DatabaseValues.InvalidRow);
                this.StatusBar.SetMessage("Image set is empty.");
                this.StatusBar.SetCurrentFile(0);
                this.StatusBar.SetCount(0);
            }

            // If we are in viewonly mode, then hide these menu items (which allow editing operations)
            // Of course, I could have folded this in with the above but its just simpler to do it here.
            if (this.State.IsViewOnly)
            {
                this.MenuItemAddFilesToImageSet.Visibility = Visibility.Collapsed;
                this.MenuItemImportDetectionData.Visibility = Visibility.Collapsed;
                this.MenuItemImportFromCsv.Visibility = Visibility.Collapsed;
                this.MenuItemRenameFileDatabaseFile.Visibility = Visibility.Collapsed;
                this.MenuItemMergeDatabases.Visibility = Visibility.Collapsed;
                this.MenuItemImportDetectionData.Visibility = Visibility.Collapsed;
                this.MenuItemImportDetectionData.Visibility = Visibility.Collapsed;
                this.MenuItemDeleteCurrentFile.Visibility = Visibility.Collapsed;
                this.MenuItemRestoreDefaults.Visibility = Visibility.Collapsed;

                this.MenuItemShowQuickPasteWindow.Visibility = Visibility.Collapsed;
                this.MenuItemImportQuickPasteFromDB.Visibility = Visibility.Collapsed;
                this.MenuItemCopyPreviousValues.Visibility = Visibility.Collapsed;
                this.MenuItemRestoreDefaults.Visibility = Visibility.Collapsed;
                this.MenuItemPopulateFieldFromMetadata.Visibility = Visibility.Collapsed;
                this.MenuItemEpisodePopulateField.Visibility = Visibility.Collapsed;
                this.MenuItemDuplicateRecord.Visibility = Visibility.Collapsed;
                this.MenuItemDelete.Visibility = Visibility.Collapsed;
                this.MenuItemDateCorrection.Visibility = Visibility.Collapsed;
                this.MenuItemFindMissingImage.Visibility = Visibility.Collapsed;
                this.MenuItemFindMissingFolder.Visibility = Visibility.Collapsed;
                this.MenuItemOptionsDarkImages.Visibility = Visibility.Collapsed;

                this.MenuS1.Visibility = Visibility.Collapsed;
                this.MenuS2.Visibility = Visibility.Collapsed;
                this.MenuS3.Visibility = Visibility.Collapsed;
                this.MenuS4.Visibility = Visibility.Collapsed;
                this.MenuS5.Visibility = Visibility.Collapsed;
                this.MenuS6.Visibility = Visibility.Collapsed;

                this.CopyPreviousValuesButton.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region Enable or disable the various menu items that allow images to be manipulated
        private void EnableImageManipulationMenus(bool enable)
        {
            this.MenuItemZoomIn.IsEnabled = enable;
            this.MenuItemZoomOut.IsEnabled = enable;
            this.MenuItemViewDifferencesCycleThrough.IsEnabled = enable;
            this.MenuItemViewDifferencesCombined.IsEnabled = enable;
            this.MenuItemDisplayMagnifyingGlass.IsEnabled = enable;
            this.MenuItemMagnifyingGlassIncrease.IsEnabled = enable;
            this.MenuItemMagnifyingGlassDecrease.IsEnabled = enable;
            this.MenuItemBookmarkSavePanZoom.IsEnabled = enable;
            this.MenuItemBookmarkSetPanZoom.IsEnabled = enable;
            this.MenuItemBookmarkDefaultPanZoom.IsEnabled = enable;
        }
        #endregion
    }
}
