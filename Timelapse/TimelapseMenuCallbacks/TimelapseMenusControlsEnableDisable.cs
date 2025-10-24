using System.Windows;
using Timelapse.Constant;
using Timelapse.Controls;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    // Enabling or Disabling Menus and Controls
    public partial class TimelapseWindow
    {
        #region Enable Or disable menus and controls
        private void EnableOrDisableMenusAndControls()
        {
            bool imageSetAvailable = IsFileDatabaseAvailable(); // A possible empty image set is loaded
            bool filesSelected = imageSetAvailable && DataHandler.FileDatabase.CountAllCurrentlySelectedFiles > 0; // A non-empty image set is loaded
            bool metadataLevelsExists = imageSetAvailable && DataHandler.FileDatabase.MetadataInfo is { RowCount: > 0 };
            // Depending upon whether images exist in the data set,
            // enable / disable menus and menu items as needed

            // File menu
            MenuItemAddFilesToImageSet.IsEnabled = imageSetAvailable;
            MenuItemLoadFiles.IsEnabled = !imageSetAvailable;
            MenuItemRecentImageSets.IsEnabled = !imageSetAvailable;
            MenuItemExportData.IsEnabled = filesSelected;
            MenuItemExportThisImage.IsEnabled = filesSelected;
            MenuItemExportSelectedImages.IsEnabled = filesSelected;
            MenuItemExportAsCsvAndPreview.IsEnabled = filesSelected;
            MenuItemExportAsCsv.IsEnabled = filesSelected;
            MenuItemCreateEmptyDatabase.IsEnabled = !imageSetAvailable;
            MenuItemCheckInDatabases.IsEnabled = imageSetAvailable;
            MenuItemCheckOutDatabase.IsEnabled = imageSetAvailable;
            MenuItemImportFromCsv.IsEnabled = filesSelected; 
            MenuItemExportAllDataAsCSV.IsEnabled = filesSelected && metadataLevelsExists;

            MenuItemRenameFileDatabaseFile.IsEnabled = filesSelected;
            MenuItemTemplateEditor.IsEnabled = true;
            MenuFileCloseImageSet.IsEnabled = imageSetAvailable;
            MenuItemImportDetectionData.Visibility = Visibility.Visible;
            MenuItemImportDetectionData.IsEnabled = imageSetAvailable;
            MenuItemAnalyzeFolderStructure.IsEnabled = imageSetAvailable && metadataLevelsExists;

            // Edit menu
            MenuItemEdit.IsEnabled = filesSelected; 
            MenuItemDeleteCurrentFile.IsEnabled = filesSelected;
            MenuItemRestoreDefaults.IsEnabled = filesSelected;
            MenuItemPopulateFieldFromMetadata.IsEnabled = filesSelected;
            MenuItemPopulateDateTimeFieldFromMetadata.IsEnabled = filesSelected;
            MenuItemPopulateFieldWithGUID.IsEnabled = filesSelected;
            MenuItemPopulateEpisodeField.IsEnabled = filesSelected;
            MenuItemFolderEditor.IsEnabled = filesSelected;

            // Options menu
            // always enable at top level when an image set exists so that image set advanced options are accessible
            MenuItemOptions.IsEnabled = true; // imageSetAvailable;
            MenuItemImageAdjuster.IsEnabled = filesSelected;
            MenuItemEpisodeOptions.IsEnabled = filesSelected;
            MenuItemEpisodeShowHide.IsEnabled = filesSelected;
            MenuItemMagnifyingGlass.IsEnabled = imageSetAvailable;
            MenuItemDisplayMagnifyingGlass.IsChecked = imageSetAvailable && State.MagnifyingGlassOffsetLensEnabled;
            MenuItemImageAdjuster.IsEnabled = filesSelected;
            MenuItemDialogsOnOrOff.IsEnabled = true;
            MenuItemPreferences.IsEnabled = true;

            // View menu
            MenuItemView.IsEnabled = filesSelected;

            // Select menu
            MenuItemSelect.IsEnabled = filesSelected;

            // Sort menu
            MenuItemSort.IsEnabled = filesSelected;

            // Recognitions menu
            MenuItemRecognitions.IsEnabled = true;                 // Always visible
            MenuItemAddaxAIDownload.IsEnabled = true;
            MenuItemAddaxAICheckForUpdates.IsEnabled = true;
            MenuBoundingBoxSetOptions.IsEnabled = filesSelected;  // Hidden when no image set
            MenuItemImportDetectionData.IsEnabled = filesSelected;
            MenuItemPopulateWithDetectionCounts.IsEnabled = filesSelected;
 
            // CamtrapDP menu
            MenuItemCamtrapDP.IsEnabled = filesSelected;
            MenuItemCamtrapDP.Visibility = filesSelected && metadataLevelsExists && DataHandler.FileDatabase.MetadataTablesIsCamtrapDPStandard()
                ? Visibility.Visible
                : Visibility.Collapsed; // Only show the CamtrapDP menu if the database is CamtrapDP compliant
            MenuItem_ExportCamtrapDP.IsEnabled = filesSelected && metadataLevelsExists && DataHandler.FileDatabase.MetadataTablesIsCamtrapDPStandard();
            
            // Windows and Help menu are always enabled

            // Enablement state of the various othesr UI components.
            ControlsPanel.IsEnabled = filesSelected;  // If images don't exist, the user shouldn't be allowed to interact with the control tray
            FileNavigatorSlider.IsEnabled = filesSelected;
            GridFileNavigator.Visibility = imageSetAvailable ? Visibility.Visible : Visibility.Collapsed;
            MarkableCanvas.IsEnabled = filesSelected;
            MarkableCanvas.MagnifiersEnabled = filesSelected && State.MagnifyingGlassOffsetLensEnabled;

            if (filesSelected == false)
            {
                DuplicateIndicatorInMainWindow.Visibility = Visibility.Collapsed;
                FileShow(DatabaseValues.InvalidRow);
                StatusBar.SetMessage("Image set is empty.");
                StatusBar.SetCurrentFile(0);
                StatusBar.SetCount(0);
            }

            // If we are in viewonly mode, then hide these menu items (which allow editing operations)
            // Of course, I could have folded this in with the above but its just simpler to do it here.
            if (State.IsViewOnly)
            {
                MenuItemAddFilesToImageSet.Visibility = Visibility.Collapsed;
                MenuItemUpgradeTimelapseFiles.Visibility = Visibility.Collapsed;
                MenuItemImportDetectionData.Visibility = Visibility.Collapsed;
                MenuItemImportFromCsv.Visibility = Visibility.Collapsed;
                MenuItemRenameFileDatabaseFile.Visibility = Visibility.Collapsed;
                MenuItemImportDetectionData.Visibility = Visibility.Collapsed;
                MenuItemImportDetectionData.Visibility = Visibility.Collapsed;
                MenuItemDeleteCurrentFile.Visibility = Visibility.Collapsed;
                MenuItemRestoreDefaults.Visibility = Visibility.Collapsed;
                MenuItemCheckInDatabases.IsEnabled = false;

                MenuItemShowQuickPasteWindow.Visibility = Visibility.Collapsed;
                MenuItemImportQuickPasteFromDB.Visibility = Visibility.Collapsed;
                MenuItemCopyPreviousValues.Visibility = Visibility.Collapsed;
                MenuItemRestoreDefaults.Visibility = Visibility.Collapsed;

                MenuItemPopulateFieldFromMetadata.Visibility = Visibility.Collapsed;
                MenuItemPopulateEpisodeField.Visibility = Visibility.Collapsed;
                MenuItemPopulateFieldWithGUID.Visibility = Visibility.Collapsed;
                MenuItemPopulateDateTimeFieldFromMetadata.Visibility = Visibility.Collapsed;
                MenuItemPopulateWithDarkImages.Visibility = Visibility.Collapsed;

                MenuItemDuplicateRecordUsingDefaultValues.Visibility = Visibility.Collapsed;
                MenuItemDuplicateRecordUsingCurrentValues.Visibility = Visibility.Collapsed;
                MenuItemDelete.Visibility = Visibility.Collapsed;
                MenuItemDateCorrection.Visibility = Visibility.Collapsed;
                MenuItemFolderEditor.Visibility = Visibility.Collapsed;
                MenuItemFindMissingImage.Visibility = Visibility.Collapsed;
                MenuItemFindMissingFolder.Visibility = Visibility.Collapsed;

                MenuItemPopulateWithDetectionCounts.Visibility = Visibility.Collapsed;

                MenuS1.Visibility = Visibility.Collapsed;
                MenuS2.Visibility = Visibility.Collapsed;
                MenuS3.Visibility = Visibility.Collapsed;
                MenuS4.Visibility = Visibility.Collapsed;
                MenuS5.Visibility = Visibility.Collapsed;
                MenuS6.Visibility = Visibility.Collapsed;
                MenuS7.Visibility = Visibility.Collapsed;

                CopyPreviousValuesButton.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region Enable or disable the various menu items that allow images to be manipulated
        private void EnableImageManipulationMenus(bool enable)
        {
            MenuItemViewDifferencesCycleThrough.IsEnabled = enable;
            MenuItemViewDifferencesCombined.IsEnabled = enable;
            MenuItemDisplayMagnifyingGlass.IsEnabled = enable;
            MenuItemMagnifyingGlassIncrease.IsEnabled = enable;
            MenuItemMagnifyingGlassDecrease.IsEnabled = enable;
            MenuItemBookmarkSavePanZoom.IsEnabled = enable;
            MenuItemBookmarkSetPanZoom.IsEnabled = enable;
            MenuItemBookmarkDefaultPanZoom.IsEnabled = enable;
        }
        #endregion
    }
}
