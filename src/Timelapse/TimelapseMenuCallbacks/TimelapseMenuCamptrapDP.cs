using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Standards;
using File = Timelapse.Constant.File;
using Path = System.IO.Path;

namespace Timelapse
{
    public partial class TimelapseWindow
    {
        #region CamtrapDP sub-menu opening
        private void CamtrapDP_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going

        }

         private async void MenuItem_CamtrapDPPopulateFields_Click(object sender, RoutedEventArgs e)
        {
            FileDatabase fileDatabase = GlobalReferences.MainWindow.DataHandler.FileDatabase;
            PopulateCamtrapDataFields dialog = new(this, fileDatabase);
            if (ShowDialogAndCheckIfChangesWereMade(dialog))
            {
                await FilesSelectAndShowAsync().ConfigureAwait(true);
            }
        }

        #endregion

        #region Export Camtrap files

        private async void MenuItem_ExportCamtrapDP_Click(object sender, RoutedEventArgs e)
        {
            if (DataHandler?.FileDatabase == null || false == DataHandler.FileDatabase.MetadataTablesIsCamtrapDPStandard()) return;

            // Check to see if the data package exists
            bool missingDataPackage = false == CamtrapDPExportFiles.CamtrapDPDataPackageExists(DataHandler.FileDatabase);

            List<string> missingDeploymentsList = [];
            bool missingAllDeployments = false == CamtrapDPExportFiles.CamtrapDPAllDeploymentLevelsExists(DataHandler.FileDatabase, missingDeploymentsList);

            if (missingDataPackage || missingAllDeployments || missingDeploymentsList.Count > 0)
            {
                if (false == Dialogs.CamtrapDPDataPackageOrDeploymentNotFilledIn(this, missingDataPackage, missingAllDeployments, missingDeploymentsList))
                {
                    return;
                }
            }

            // We want to show the prompt only if the promptState is true, and we are  viewing all images
            int filesTotalCount = DataHandler.FileDatabase.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.All);
            int filesSelectedCount = DataHandler.FileDatabase.FileTable.RowCount;
            if (filesTotalCount != filesSelectedCount)
            {
                Dialogs.MenuFileExportRequiresAllFilesSelected(this, $"as {Constant.Standards.CamtrapDPStandard}");
                StatusBar.SetMessage("Export cancelled.");
                return;
            }

            if (false == Dialogs.ExportToCamtrapDPExplanation(this))
            {
                StatusBar.SetMessage("Export cancelled.");
                return;
            }
            // Get a folder path from the user
            if (false == Dialogs.TryGetFolderFromUserUsingOpenFileDialog(
                    $"Select a folder to contain the {File.CamtrapDPExportFolder} folder and CamtrapDP files",
                    DataHandler.FileDatabase.RootPathToDatabase, out string camTrapDPFolder))
            {
                StatusBar.SetMessage("Export cancelled.");
                return;
            }

            // Create a CamtrapDP export folder (if needed)
            camTrapDPFolder = Path.Combine(camTrapDPFolder, File.CamtrapDPExportFolder);
            if (false == Directory.Exists(camTrapDPFolder))
            {
                Directory.CreateDirectory(camTrapDPFolder);
            }

            // Write all the data into the expected camtrapDP files
            // where those files convert the Timelapse data into the exact CamtrapDP specs
            // Export the data package
            string dataPackageFilePath = Path.Combine(camTrapDPFolder, File.CamtrapDPDataPackageJsonFilename);
            List<string> datapackageMessages = await CamtrapDPExportFiles.ExportCamtrapDPDataPackageToJsonFile(GlobalReferences.MainWindow.DataHandler.FileDatabase, dataPackageFilePath);
            if (null == datapackageMessages)
            {
                // Something went wrong.
                Dialogs.MenuFileExportFailedForUnknownReasonDialog(GlobalReferences.MainWindow, dataPackageFilePath);
                StatusBar.SetMessage("Export cancelled.");
                return;
            }
            if (datapackageMessages.Count > 0)
            {
                datapackageMessages.Insert(0, $"{Environment.NewLine}{Environment.NewLine}In [b]{File.CamtrapDPDataPackageJsonFilename}[/b]:");
            }

            // Export the deployment csv file
            string deploymentFilePath = Path.Combine(camTrapDPFolder, File.CamtrapDPDeploymentCSVFilename);

            List<string> deploymentMessages = await CamtrapDPExportFiles.ExportCamtrapDPDeploymentToCsv(DataHandler.FileDatabase, deploymentFilePath);
            if (null == deploymentMessages)
            {
                Dialogs.MenuFileExportFailedForUnknownReasonDialog(GlobalReferences.MainWindow, deploymentFilePath);
                StatusBar.SetMessage("Export cancelled.");
                return;
            }

            if (deploymentMessages.Count > 0)
            {
                deploymentMessages.Insert(0, $"{Environment.NewLine}{Environment.NewLine}In [b]{File.CamtrapDPDeploymentCSVFilename}[/b]:");
            }

            // Export the media and observations csv file
            // This has a progress indicator as it could be a big file
            string mediaFilePath = Path.Combine(camTrapDPFolder, File.CamtrapDPMediaCSVFilename);
            string observationsFilePath = Path.Combine(camTrapDPFolder, File.CamtrapDPObservationsCSVFilename);

            BusyCancelIndicator.IsBusy = true;
            List<string> mediaObservationsMessages = await CamtrapDPExportFiles.ExportCamtrapDPMediaObservationsToCsv(DataHandler.FileDatabase, DataEntryControls, mediaFilePath, observationsFilePath);
            BusyCancelIndicator.IsBusy = false;
            if (null == mediaObservationsMessages)
            {
                Dialogs.MenuFileExportFailedForUnknownReasonDialog(GlobalReferences.MainWindow, $"{Environment.NewLine}{Environment.NewLine}{mediaFilePath} and {observationsFilePath}:");
                StatusBar.SetMessage("Export cancelled.");
                return;
            }
            if (mediaObservationsMessages.Count > 0)
            {
                mediaObservationsMessages.Insert(0, $"{Environment.NewLine}{Environment.NewLine}In [b]{File.CamtrapDPMediaCSVFilename}[/b] and [b]{File.CamtrapDPObservationsCSVFilename}[/b]:");
            }

            // ReSharper disable once AssignNullToNotNullAttribute
            List<string> allMessages = [.. datapackageMessages, .. deploymentMessages, .. mediaObservationsMessages];
            if (allMessages.Count > 0)
            {
                Dialogs.CamtrapDPDataPackageMissingRequiredFields(GlobalReferences.MainWindow, allMessages);
                StatusBar.SetMessage("Export completed, with warnings.");
            }
            else
            {
                Dialogs.AllDataExportedToCSV(this, camTrapDPFolder,
                [
                    File.CamtrapDPDataPackageJsonFilename, File.CamtrapDPDeploymentCSVFilename, File.CamtrapDPMediaCSVFilename,
                    File.CamtrapDPObservationsCSVFilename
                ], false);
                StatusBar.SetMessage("Export completed.");
            }
        }
        #endregion
    }
}
