using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Timelapse.Database;
using Timelapse.Dialog;
using Timelapse.Util;
namespace Timelapse
{
    /// <summary>
    /// Methods to check for various missing folders and to ask the user to correct them if they are missing.
    /// These checks are requested during image set loading (see TimelapseImageSetLoading)
    /// </summary>
    public partial class TimelapseWindow
    {
        #region Private Methods - CheckAndCorrectRootFolder
        // Get the root folder name from the database, and check to see if its the same as the actual root folder.
        // If not, ask the user if he/she wants to update the database.
        private void CheckAndCorrectRootFolder(FileDatabase fileDatabase)
        {
            // Check the arguments for null 
            if (fileDatabase == null)
            {
                // this should not happen
                // System.Diagnostics.Debug.Print("The fielDatabase was null and it shouldn't be");
                TracePrint.StackTrace(1);
                // No-op
                return;
            }
            string rootFolder = fileDatabase.ImageSet.RootFolder;

            // retrieve and compare the db and actual root folder path names. While there really should be only one entry in the allRootFolderPaths,
            // we still do a check in case there is more than one. If even one entry doesn't match, we use that entry to ask the user if he/she
            // wants to update the root folder to match the actual location of the root folder containing the template, data and image files.
            string actualRootFolderName = fileDatabase.FolderPath.Split(Path.DirectorySeparatorChar).Last();
            if (false == rootFolder.Equals(actualRootFolderName))
            {
                // There is a mismatch between the actual root folder and the stored root folder
                // Consequently, ask the user if he/she wants to update the db entry 
                Dialog.UpdateRootFolder renameRootFolderDialog = new Dialog.UpdateRootFolder(this, rootFolder, actualRootFolderName);
                bool? result = renameRootFolderDialog.ShowDialog();
                if (result == true)
                {
                    fileDatabase.ImageSet.RootFolder = actualRootFolderName;
                    fileDatabase.UpdateSyncImageSetToDatabase();
                }
                return;
            }
        }
        #endregion

        #region Private Static Check or Correct for Missing Folders including dialog
        /// <summary>
        /// If there are missing folders, search for possible matches and raise a dialog box asking the user to locate them
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="fileDatabase"></param>
        /// <param name="missingFolders"></param>
        /// <returns>whether any folder are actually missing </returns>
        private static bool? CorrectForMissingFolders(Window owner, FileDatabase fileDatabase, List<string> missingRelativePaths)
        {
            // Abort if the arguments for null 
            if (null == fileDatabase) return null;
            if (null == missingRelativePaths) return null;

            // We know that at least one or more folders are missing.
            // For each missing folder path, try to find all folders with the same name under the root folder.
            Dictionary<string, List<string>> matchingFolderNames = Util.FilesFolders.TryGetMissingFolders(fileDatabase.FolderPath, missingRelativePaths);

            // We want to show the normal cursor when we display dialog boxes, so save the current cursor so we can store it.
            Cursor cursor = Mouse.OverrideCursor;

            if (matchingFolderNames != null)
            {
                Mouse.OverrideCursor = null;
                // Present a dialog box that asks the user to locate the missing folders. It will show possible locations for each folder (if any).
                // The user can then confirm correct locations, manually set the locaton of those folders, or cancel altogether.
                MissingFoldersLocateAllFolders dialog = new MissingFoldersLocateAllFolders(owner, fileDatabase.FolderPath, matchingFolderNames);
                bool? result = dialog.ShowDialog();

                if (result == true)
                {
                    // Get the updated folder locations and update the database
                    Dictionary<string, string>  finalFileLocations = dialog.FinalFolderLocations;
                    foreach (string key in finalFileLocations.Keys)
                    {
                        ColumnTuple columnToUpdate = new ColumnTuple(Constant.DatabaseColumn.RelativePath, finalFileLocations[key]);
                        ColumnTuplesWithWhere columnToUpdateWithWhere = new ColumnTuplesWithWhere(columnToUpdate, key);
                        fileDatabase.UpdateFiles(columnToUpdateWithWhere);
                    }
                    Mouse.OverrideCursor = cursor;
                    return true;
                }
            }
            Mouse.OverrideCursor = cursor;
            return null; // Operaton aborted
        }

        /// <summary>
        /// A convenience wrapper function for checking for missing folders, and correcting them if they are missing
        /// </summary>
        /// returns 
        /// - true if folders were corrected, 
        /// - false if no folders are missing, 
        ///  - null if the operation was aborted for some reason, or the folders were missing but not updated..
        private static bool? GetAndCorrectForMissingFolders(Window owner, FileDatabase fileDatabase)
        {
            if (fileDatabase == null) return null;
            List<string> missingRelativePaths = GetMissingFolders(fileDatabase);
            return (missingRelativePaths.Count == 0) ? false : CorrectForMissingFolders(owner, fileDatabase, missingRelativePaths);
        }


        /// <summary>
        /// Returns a (possible empty) list of missing folders. This is done by by getting all relative paths and seeing if each folder actually exists.
        /// </summary>
        private static List<string> GetMissingFolders(FileDatabase fileDatabase)
        {
            List<object> allRelativePaths = fileDatabase.GetDistinctValuesInColumn(Constant.DBTables.FileData, Constant.DatabaseColumn.RelativePath);
            List<string> missingRelativePaths = new List<string>();
            foreach (string relativePath in allRelativePaths)
            {
                string path = Path.Combine(fileDatabase.FolderPath, relativePath);
                if (!Directory.Exists(path))
                {
                    missingRelativePaths.Add(relativePath);
                }
            }
            return missingRelativePaths;
        }
        #endregion
    }
}
