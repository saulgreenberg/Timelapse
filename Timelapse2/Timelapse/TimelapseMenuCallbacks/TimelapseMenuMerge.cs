using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Util;
using ToastNotifications.Position;
using MessageBox = System.Windows.MessageBox;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    public partial class TimelapseWindow
    {
        private readonly string templateTdbPath = @"C:\Users\saulg\Desktop\TestSets\MergeTest\TimelapseTemplate.tdb";
        //private readonly string sourceDdbPath = @"C:\Users\saulg\Desktop\TestSets\MergeTest\SubFolder\TimelapseData.ddb";


        // Create an empty Timelapse database based upon the template.
        // Abort if the template does not exist or cannot be opened, generating the various error messages as needed.
        private async void MenuItemCreateEmptyDatabase_Click(object sender, RoutedEventArgs e)
        {
        string destinationDdbPathToCreate = @"C:\Users\saulg\Desktop\TestSets\MergeTest\MasterDatabase.ddb";
        ErrorsAndWarnings errorMessages;

            if (File.Exists(this.templateTdbPath))
            {
                // The template exists, so try to create the empty ddb
                errorMessages = await MergeDatabasesNew.TryCreateEmptyDatabaseFromTemplateAsync(
                    this.templateTdbPath,
                    destinationDdbPathToCreate).ConfigureAwait(true);
            }
            else
            {
                // The template does not exist, so don't try to create an empty database
                // Also generate an error message
                errorMessages = new ErrorsAndWarnings();
                errorMessages.Errors.Add($"Database not created, as the template file {this.templateTdbPath} does not exist");
            }
            DisplayMergeResults(errorMessages);
        }

        private void MenuItemAddDatabase_Click(object sender, RoutedEventArgs e)
        {
            // Show an explanation dialog
            if (this.State.SuppressMergeASingleDatabasePrompt == false)
            {
                if (Dialogs.MenuFileMergeASingleDatabaseExplainedDialog(this) == false)
                {
                    return;
                }
            }

            // Get the 
            string sourceDdbPath = GetFolderToMerge(this, this.DataHandler.FileDatabase.FilePath);
            if (string.IsNullOrWhiteSpace(sourceDdbPath))
            {
                return;
            }

            // Destination database is the currently opened database. For convenicence, set an alias to its path and handle
            string destinationDdbPath = this.DataHandler.FileDatabase.FilePath;
            SQLiteWrapper destinationDdb = this.DataHandler.FileDatabase.Database;

            // a. Check if databases are valid
            ErrorsAndWarnings errorMessages = new ErrorsAndWarnings();
            DatabaseFileErrorsEnum destinationDdbErrorsEnum = FilesFolders.QuickCheckDatabaseFile(destinationDdbPath);
            if (destinationDdbErrorsEnum != DatabaseFileErrorsEnum.Ok)
            {
                // XXX WRITE A GENERIC WAY TO HANDLE DIFFERENT DatabaseFileErrorsEnum
                Dialogs.DatabaseFileAppearsCorruptDialog(this, sourceDdbPath);
                return;
            }

            // b. Check if templates are compatable
            // XXX BETTER TEMPLATE CHECK???
            if (MergeDatabasesNew.CheckIfDatabaseTemplatesAreMergeCompatable(sourceDdbPath, destinationDdbPath) == DatabaseFileErrorsEnum.Ok)
            {
                Debug.Print("Both databases templates appear compatable");
            }
            else
            {
                errorMessages.Errors.Add("Database templates are not compatable");
                DisplayMergeResults(errorMessages);
                return;
            }

            // SQLiteWrapper destinationDdb = new SQLiteWrapper(destinationDdbPath);

            // c. Determine the path prefix to add to the Relative Path i.e., the difference between the .tdb root folder and the path to the source ddb file
            // XXX CHECK FOR STRING.EMPTY IF IN C: ??
            string pathToRootFolder = Path.GetDirectoryName(destinationDdbPath);
            if (pathToRootFolder == null)
            {
                Debug.Print("Could not get directory name");
                return;
            }
            string relativePathDifference = FilesFolders.GetDifferenceBetweenPathAndSubPath(pathToRootFolder, sourceDdbPath);

            // d. Remove entries from the destination ddb that refer to the source ddb's folder
            //    First, delete the ones in the DataTable (automatically removes foreign key recognitions, if any)
            //    Second, delete the ones in the Markers table if any (using the deleted Ids returned by the previous operation)
            DataTable dtDeletedIds = MergeDatabasesNew.RemoveEntriesFromDdbContainingThisPathReturningDeletedIds(destinationDdb, relativePathDifference);
            if (dtDeletedIds.Rows.Count > 0)
            {
                // Use the Ids of the deleted entries to remove corresponding entries (if they exist) from the Markers table
                List<string> idClauses = new List<string>();
                foreach (DataRow row in dtDeletedIds.Rows)
                {
                    idClauses.Add(Constant.DatabaseColumn.ID + " = " + row[0]);
                }
                // XXX WHAT IF THERE ARE A HUGE NUMBER? WRAP IN BEGIN END?
                destinationDdb.Delete(Constant.DBTables.Markers, idClauses);
                Debug.Print($"Ddb deleted {idClauses.Count} entries in the folder {relativePathDifference}");
            }
            else
            {
                Debug.Print($"Ddb does not reference the folder: {relativePathDifference}. Nothing deleted");
            }

            // e. Do the merge
            DatabaseFileErrorsEnum errorsEnum = MergeDatabasesNew.MergeSourceIntoDestinationDdb(
                destinationDdb, sourceDdbPath, relativePathDifference);
        }

        // Get a database file from the user that is in a sub-folder of the destination path
        private static string GetFolderToMerge(TimelapseWindow timelapseWindow, string destinationDdbPath)
        {
            // Source database: get it from the user
            string sourceDdbPath = Dialogs.LocateFileUsingOpenFileDialog(Path.GetDirectoryName(destinationDdbPath),
                $"Locate a database ({Constant.File.FileDatabaseFileExtension}) file in a sub-folder to merge into this one...",
                $"Timelapse data ({Constant.File.FileDatabaseFileExtension}) files",
                Constant.File.FileDatabaseFileExtension);

            if (string.IsNullOrWhiteSpace(sourceDdbPath))
            {
                // User cancelled
                return string.Empty;
            }

            if (Path.GetDirectoryName(destinationDdbPath)?.Length >= Path.GetDirectoryName(sourceDdbPath)?.Length)
            {
                bool sameFile = destinationDdbPath.Equals(sourceDdbPath);
                string pathToShow = sameFile
                    ? sourceDdbPath
                    : Path.GetDirectoryName(sourceDdbPath);
                Dialogs.MergeSourceFileMustBeInASubfolder(timelapseWindow, pathToShow, sameFile);
                return string.Empty;
            }

            return sourceDdbPath;
        }

        static string GetRootFolder(string path)
        {
            while (true)
            {
                string temp = Path.GetDirectoryName(path);
                if (String.IsNullOrEmpty(temp))
                    break;
                path = temp;
            }
            return path;
        }

        // Display the status message concerning the merge
        private void DisplayMergeResults(ErrorsAndWarnings errorMessages)
        {
            // Generate a message
            string message = string.Empty;
            foreach (string error in errorMessages.Errors)
            {
                message += error + Environment.NewLine;
            }
            foreach (string warning in errorMessages.Warnings)
            {
                message += warning + Environment.NewLine;
            }
            foreach (string backupMessages in errorMessages.BackupMessages)
            {
                message += errorMessages.BackupMessages + Environment.NewLine;
            }
            message += errorMessages.BackupMade ? "backup made" : "no backup made";

            Debug.Print(message);
        }
    }
}
