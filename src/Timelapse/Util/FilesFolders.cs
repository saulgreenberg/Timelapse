using Microsoft.WindowsAPICodePack.Shell;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Resources;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Extensions;
using TimelapseTemplateEditor;
using File = System.IO.File;

namespace Timelapse.Util
{
    /// <summary>
    /// Static convenience methods to Get information about files, folders and paths
    /// </summary>
    public static class FilesFolders
    {
        #region Try versions for file utilities

        // Try to delete the indicated file
        public static bool TryDeleteFileIfExists(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception exception)
                {
                    TracePrint.PrintMessage("Could not delete " + filePath + Environment.NewLine + exception.Message + ": " + exception);
                    return false;
                }
                return true;
            }

            return false;
        }

        public static bool TryMoveFileIfExists(string sourceFilePath, string destinationFilePath)
        {
            if (File.Exists(sourceFilePath))
            {
                try
                {
                    File.Move(sourceFilePath, destinationFilePath);
                }
                catch (Exception exception)
                {
                    TracePrint.PrintMessage("Could not move " + sourceFilePath + " to " + destinationFilePath + Environment.NewLine + exception.Message + ": " + exception);
                    return false;
                }
                return true;
            }

            return false;
        }

        public static MoveFolderResultEnum TryMoveFolderIfExists(string sourceFolderPath, string destinationFolderPath)
        {
            try
            {
                if (Directory.Exists(sourceFolderPath))
                {
                    // Ensure the destination directory doesn't already exist
                    if (Directory.Exists(destinationFolderPath) == false)
                    {
                        // Perform the move
                        if (destinationFolderPath != null)
                        {
                            Directory.Move(sourceFolderPath, destinationFolderPath);
                        }
                    }
                    else
                    {
                        TracePrint.PrintMessage($"Move not done: destination folder '{destinationFolderPath} already exists.");
                        return MoveFolderResultEnum.FailAsDestinationFolderExists;
                    }
                    return MoveFolderResultEnum.Success;
                }

                TracePrint.PrintMessage($"Move not done: the source folder '{sourceFolderPath} does not exists.");
                return MoveFolderResultEnum.FailAsSourceFolderDoesNotExist;
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage(
                    $"Move not done of {sourceFolderPath} to {destinationFolderPath} as an exception was raised{Environment.NewLine}{exception.Message}: {exception}");
                return MoveFolderResultEnum.FailDueToSystemMoveException;
            }
        }

        // Try to create a subfolder iwth the given name in the indicated folder
        public static CreateSubfolderResultEnum TryCreateSubfolderInFolder(string sourceFolderPath, string destinationSubfolderName)
        {
            try
            {
                string destinationPath = Path.Combine(sourceFolderPath, destinationSubfolderName);

                if (Directory.Exists(sourceFolderPath))
                {
                    // Ensure the destination directory doesn't already exist
                    if (Directory.Exists(destinationPath) == false)
                    {
                        // Create the destination subFolder
                        Directory.CreateDirectory(destinationPath);
                        return CreateSubfolderResultEnum.Success;
                    }
                    TracePrint.PrintMessage($"Subfolder '{destinationSubfolderName}' not created: '{destinationPath}' already exists.");
                    return CreateSubfolderResultEnum.FailAsDestinationFolderExists;
                }
                TracePrint.PrintMessage($"Subfolder '{destinationSubfolderName}' not created: the parent folder '{sourceFolderPath}' does not exist.");
                return CreateSubfolderResultEnum.FailAsSourceFolderDoesNotExist;
            }
            catch (Exception exception)
            {
                TracePrint.PrintMessage(
                    $"Subfolder '{destinationSubfolderName}' was not created in '{sourceFolderPath}'  as an exception was raised{Environment.NewLine}{exception.Message}: {exception}");
                return CreateSubfolderResultEnum.FailDueToSystemCreateException;
            }
        }

        #endregion

        #region Public static methods - Check if the database is valid, and return error status reports if it isn't
        // Only invoke this on a .tdb or .ddb file
        public static DatabaseFileErrorsEnum QuickCheckDatabaseFile(string filePath)
        {
            // Check the file path length and notify the user the template couldn't be loaded because its path is too long 
            if (IsCondition.IsPathLengthTooLong(filePath, FilePathTypeEnum.TDB))
            {
                return DatabaseFileErrorsEnum.PathTooLong;
            }

            // Test: Does file exists
            if (false == File.Exists(filePath))
            {
                return DatabaseFileErrorsEnum.DoesNotExist;
            }

            // Test: Check for invalid file locations
            // Disallowed are Drive letter roots and System/Hidden folders
            string extension = Path.GetExtension(filePath);
            if (IsFolderPathADriveLetter(Path.GetDirectoryName(filePath)))
            {
                return DatabaseFileErrorsEnum.FileInRootDriveFolder;
            }
            if (IsFolderSystemOrHidden(Path.GetDirectoryName(filePath)))
            {
                return DatabaseFileErrorsEnum.FileInSystemOrHiddenFolder;
            }

            // Test: Is it a .ddb or .tdb file
            if (Path.GetExtension(filePath) != Constant.File.FileDatabaseFileExtension && Path.GetExtension(filePath) != Constant.File.TemplateDatabaseFileExtension)
            {
                return DatabaseFileErrorsEnum.NotATimelapseFile;
            }

            // Database integrity tests
            // Create the database wrapper. Note that since we know the file exists, the wrapper won't try to create it (which it does if it doesn't exist).
            SQLiteWrapper db = new(filePath);
            if (extension == Constant.File.TemplateDatabaseFileExtension)
            {
                // Template tests for .tdb files
                // Template Test: at least one row exists with the type file (to ensure its not an empty table)
                if (0 == db.ScalarGetScalarFromSelectAsInt(Sql.SelectCountStarFrom + DBTables.Template + Sql.Where + Sql.TypeEquals + Sql.Quote(DatabaseColumn.File)))
                {
                    return DatabaseFileErrorsEnum.InvalidDatabase;
                }

                bool existsTemplateTable = db.TableExists(DBTables.Template);
                if (false == existsTemplateTable)
                {
                    // Basic test failed: Template table does not exist
                    return DatabaseFileErrorsEnum.InvalidDatabase;
                }

                // Various version checks
                bool existsTemplateInfoTable = db.TableExists(DBTables.TemplateInfo);

                // Test: There is no TemplateIno)
                if (false == existsTemplateInfoTable)
                {
                    // High probability that its a good db, but pre version 2.3.0.0 as the TemplateInfo table does not exist
                    return DatabaseFileErrorsEnum.PreVersion2300;
                }

                // Test: Check if its a template that was opened with a pre2.3 version of Timelapse, which would re-insert the UTCOffset type...
                // If so, this would have to be fixed.
                string typeQuery = Sql.Select + Control.Type + Sql.From + DBTables.Template + Sql.Where + Control.Type + Sql.Equal + Sql.Quote(ControlDeprecated.UtcOffsetLabel);
                DataTable table = db.GetDataTableFromSelect(typeQuery);
                if (table.Rows.Count > 0)
                {
                    return DatabaseFileErrorsEnum.UTCOffsetTypeExistsInUpgradedVersion;
                }

                // Check if the template is being open with an earlier version of Timelapse than what it was previously saved with, but it should be post 2.3.3.0
                string templateVersion = TemplateEditorWindow.TemplateGetVersionCompatability(db);
                string timelapseExecutableCurrentVersionNumber = VersionChecks.GetTimelapseCurrentVersionNumber().ToString();
                if (VersionChecks.IsVersion1GreaterThanVersion2(templateVersion, timelapseExecutableCurrentVersionNumber))
                {
                    return DatabaseFileErrorsEnum.OkButOpenedWithAnOlderTimelapseVersion;
                }

                // High probability that its a good db
                return DatabaseFileErrorsEnum.Ok;
            }
            else
            {
                // Test Data .ddb file 
                // Data Test: basic test to make sure it seems to be a valid ddb
                if (false == db.TableExists(DBTables.FileData))
                {
                    return DatabaseFileErrorsEnum.InvalidDatabase;
                }

                // Test: if its missing the VersionCompatability column, its pre 2.3.0.0
                if (false == db.SchemaIsColumnInTable(DBTables.ImageSet, DatabaseColumn.VersionCompatibility))
                {
                    return DatabaseFileErrorsEnum.PreVersion2300;
                }

                // Test: Get the versionCompatability value and test if the executable version number is:
                // - earlier than 2.3.0.0 (requires a special database update)
                // - 2.3.0.0 but earlier than the BackwardsCompatibility version (which requires downloading the latest version of Timelapse)
                // - At or later than BackwardsCompatibility, but less than the database version (which is likely ok, but generates a warning)
                string versionQuery = Sql.Select + DatabaseColumn.VersionCompatibility + Sql.From + DBTables.ImageSet + Sql.Where + DatabaseColumn.ID + Sql.Equal + Sql.Quote(DatabaseValues.ImageSetRowID.ToString());
                DataTable table = db.GetDataTableFromSelect(versionQuery);
                if (table.Rows.Count > 0)
                {
                    string databaseCompatibilityVersion = (string)table.Rows[0][DatabaseColumn.VersionCompatibility];
                    if (VersionChecks.IsVersion1GreaterOrEqualToVersion2(databaseCompatibilityVersion, DatabaseValues.VersionNumberMinimum))
                    {
                        // While the database looks like it was last updated after 2.3.0.0, we still have to handle this special case
                        // But - Special case as UTCOffset column could be added if the DB was opened with a pre2.3 version of Timelapse.
                        if (db.SchemaIsColumnInTable(DBTables.FileData, ControlDeprecated.UtcOffsetLabel))
                        {
                            return DatabaseFileErrorsEnum.PreVersion2300;
                        }

                        // Get the version number of the running timelapse application
                        string timelapseExecutableCurrentVersionNumber = VersionChecks.GetTimelapseCurrentVersionNumber().ToString();

                        // Test: Get the BackwardsCompatibility value and test if the current version is compatible with it
                        string backwardsCompatibilityQuery = Sql.Select + DatabaseColumn.BackwardsCompatibility + Sql.From + DBTables.ImageSet + Sql.Where + DatabaseColumn.ID + Sql.Equal + Sql.Quote(DatabaseValues.ImageSetRowID.ToString());
                        if (db.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.BackwardsCompatibility))
                        {
                            table = db.GetDataTableFromSelect(backwardsCompatibilityQuery);
                            if (table.Rows.Count > 0)
                            {
                                string databaseBackwardsCompatibilityVersion = (string)table.Rows[0][DatabaseColumn.BackwardsCompatibility];
                                // Now just check to see if we are opening the .ddb file with an older version of Timelapse that last opened it...
                                if (false == VersionChecks.IsVersion1GreaterOrEqualToVersion2(timelapseExecutableCurrentVersionNumber, databaseBackwardsCompatibilityVersion))
                                {
                                    return DatabaseFileErrorsEnum.IncompatibleVersion;
                                }
                            }
                        }

                        // Check if the database is being open with an earlier version of Timelapse than what it was previously saved with, but it should be post 2.3.3.0
                        if (VersionChecks.IsVersion1GreaterThanVersion2(databaseCompatibilityVersion, timelapseExecutableCurrentVersionNumber))
                        {
                            return DatabaseFileErrorsEnum.OkButOpenedWithAnOlderTimelapseVersion;

                        }
                        return DatabaseFileErrorsEnum.Ok;

                    }
                    // The database is pre2.3.0.0
                    return DatabaseFileErrorsEnum.PreVersion2300;
                }
                // No version compatibility value, so it must be pre2300
                return DatabaseFileErrorsEnum.PreVersion2300;
            }
            // It should never get here
        }

        // Check if the sourceDb version is compatible (i.e., equal or greater than the destinationDb backwards compatibility version
        public static DatabaseFileErrorsEnum IsDatabaseVersionMergeCompatabileWithTimelapseVersion(SQLiteWrapper sourceDb, SQLiteWrapper destinationDb)
        {
            // Test: Get the BackwardsCompatibility value from the destinationDb and test if the sourceDb version is compatible with it
            string backwardsCompatibilityQuery = $"{Sql.Select} {DatabaseColumn.BackwardsCompatibility} {Sql.From} {DBTables.ImageSet} {Sql.Where} {DatabaseColumn.ID} {Sql.Equal} {Sql.Quote(DatabaseValues.ImageSetRowID.ToString())} ";
            string versionCompatibilityQuery = $"{Sql.Select} {DatabaseColumn.VersionCompatibility} {Sql.From} {DBTables.ImageSet} {Sql.Where} {DatabaseColumn.ID} {Sql.Equal} {Sql.Quote(DatabaseValues.ImageSetRowID.ToString())} ";
            string sourceDbVersion;
            string destinationDbBackwardsCompatibilityVersion;
            DataTable dataTable;

            // Get the source DB version number
            if (sourceDb.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.VersionCompatibility))
            {
                dataTable = sourceDb.GetDataTableFromSelect(versionCompatibilityQuery);
                if (dataTable.Rows.Count > 0)
                {
                    // Get the backwards compatibility version from the sourceDb
                    sourceDbVersion = (string)dataTable.Rows[0][DatabaseColumn.VersionCompatibility];
                }
                else
                {
                    // We should always be able to get an image set row.
                    return DatabaseFileErrorsEnum.IncompatibleVersionForMerging;
                }
            }
            else
            {
                // If we can't get this column, it must mean its an early version
                return DatabaseFileErrorsEnum.IncompatibleVersionForMerging;
            }

            // Get the destinationDB Backwards compatibility version number
            if (destinationDb.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.BackwardsCompatibility))
            {
                dataTable = destinationDb.GetDataTableFromSelect(backwardsCompatibilityQuery);
                if (dataTable.Rows.Count > 0)
                {
                    // Get the backwards compatibility version from the destinationDb
                    destinationDbBackwardsCompatibilityVersion = (string)dataTable.Rows[0][DatabaseColumn.BackwardsCompatibility];
                }
                else
                {
                    // We should always be able to get an image set row.
                    return DatabaseFileErrorsEnum.InvalidDatabase;
                }
            }
            else
            {
                // If we can't get this column, it must mean its an early version
                return DatabaseFileErrorsEnum.IncompatibleVersion;
            }

            // Check if the sourceDb version is at least as new as the destinationDb backwards compatibility version
            if (false == VersionChecks.IsVersion1GreaterOrEqualToVersion2(sourceDbVersion, destinationDbBackwardsCompatibilityVersion))
            {
                return DatabaseFileErrorsEnum.IncompatibleVersionForMerging;
            }
            return DatabaseFileErrorsEnum.Ok;
        }
        #endregion

        #region Public Static Methods - GetAllFiles
        // Populate fileInfoList  with the .jpg, .avi, and .mp4 files found in the rootFolderPath and its sub-folders
        // rootFolderPath is the complete path to the root folder</param>
        // fileInfoList are found files are added to this list</param>        
        public static void GetAllImageAndVideoFilesInFolderAndSubfolders(string rootFolderPath, List<FileInfo> fileInfoList, List<string> foldersWithImagesOrVideos)
        {
            GetAllImageAndVideoFilesInFolderAndSubfolders(rootFolderPath, fileInfoList, foldersWithImagesOrVideos, 0);
        }

        /// <summary>
        /// Populate folderPaths with all the folders and subfolders (from the root folder) that contains at least one video or image file
        /// If prefixPath is provided, it is stripped from the beginning of the matching folder paths, otherwise the full path is returned
        /// </summary>
        /// <param name="folderRoot"></param>
        /// <param name="folderPaths"></param>
        /// <param name="prefixPath"></param>
        public static void GetAllFoldersContainingAnImageOrVideo(string folderRoot, List<string> folderPaths, string prefixPath)
        {
            // Check the arguments for null 
            if (folderPaths == null || folderRoot == null)
            {
                // this should not happen
                TracePrint.StackTrace(1);
                // throw new ArgumentNullException(nameof(folderPaths));
                // Not sure what happens if we have a null folderPaths, but we may as well try it.
                return;
            }

            if (!Directory.Exists(folderRoot))
            {
                return;
            }
            // Add a folder only if it contains one of the desired extensions
            if (CheckFolderForAtLeastOneImageOrVideoFiles(folderRoot))
            {
                if (string.IsNullOrEmpty(prefixPath) == false)
                {
                    int index = folderRoot.Length > prefixPath.Length + 1 ? prefixPath.Length + 1 : prefixPath.Length;
                    folderPaths.Add(folderRoot[index..]);
                }
                else
                {
                    folderPaths.Add(folderRoot);
                }
            }

            DirectoryInfo[] subDirs;
            // Recursively descend subfolders, collecting directory info on the way
            // Note that while folders without images are also collected, these will eventually be skipped when it is later scanned for images to load
            try
            {
                DirectoryInfo dirInfo = new(folderRoot);
                subDirs = dirInfo.GetDirectories();
            }
            catch
            {
                // It may fail if there is a permissions issue
                return;
            }
            foreach (DirectoryInfo subDir in subDirs)
            {
                // Skip the following folders
                if (subDir.Name == Constant.File.BackupFolder || subDir.Name == Constant.File.DeletedFilesFolder || subDir.Name == Constant.File.VideoThumbnailFolderName)
                {
                    continue;
                }
                GetAllFoldersContainingAnImageOrVideo(subDir.FullName, folderPaths, prefixPath);
            }
        }

        /// <summary>
        /// Populate foundFiles with files matching the patternfound by recursively descending the startFolder path.
        /// </summary>
        public static List<string> GetAllFilesInFoldersAndSubfoldersMatchingPattern(string startFolder, string pattern, bool ignoreBackupFolder, bool ignoreDeletedFolder, List<string> foundFiles)
        {
            if (startFolder == null)
            {
                // This should not happen, but just in case
                return null;
            }
            foundFiles ??= [];
            try
            {
                string foldername = startFolder.Split(Path.DirectorySeparatorChar).Last();
                if ((ignoreBackupFolder && foldername == Constant.File.BackupFolder) || (ignoreDeletedFolder && foldername == Constant.File.DeletedFilesFolder))
                {

                }
                else
                {
                    foundFiles.AddRange(Directory.GetFiles(startFolder, pattern, SearchOption.TopDirectoryOnly));
                    foreach (string directory in Directory.GetDirectories(startFolder))
                    {
                        GetAllFilesInFoldersAndSubfoldersMatchingPattern(directory, pattern, true, true, foundFiles);
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
            return foundFiles;
        }

        /// <summary>
        /// Populate foundFiles with files matching the patternfound by recursively descending the folder path.
        /// </summary>
        public static List<string> GetAllImageAndVideoFilesInASingleFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || false == Directory.Exists(folder))
            {
                // This should not happen, but just in case
                return null;
            }

            try
            {
                List<string> foundFiles = [.. Directory.GetFiles(folder)];
                return FilesRemoveAllButImagesAndVideos(foundFiles);
            }
            catch (Exception)
            {
                return null;
            }
        }
        #endregion

        #region public static Method - GetFolders

        ///
        // Async wrapper around GetAllFoldersExceptBackupAndDeletedFolders
        public static async Task<List<string>> AsyncGetAllFoldersExceptBackupAndDeletedFolders(string rootFolderPath, string rootFolderPrefix)
        {
            // Get all the physcial folders under the root folder (excepting backups and deleted folders)
            return await Task.Run(() => GetAllFoldersExceptBackupAndDeletedFolders(rootFolderPath, [], rootFolderPrefix));
        }

        /// <summary>
        /// Populate folderPaths with all the folders and subfolders (from the root folder) excepting the Backup and Deleted folders
        /// </summary>
        /// <param name="folderRoot"></param>
        /// <param name="folderPaths"></param>
        /// <param name="prefixPath"></param>
        public static List<string> GetAllFoldersExceptBackupAndDeletedFolders(string folderRoot, List<string> folderPaths, string prefixPath)
        {
            // Check the arguments for null 
            if (folderPaths == null || folderRoot == null)
            {
                // this should not happen
                TracePrint.StackTrace(1);
                // throw new ArgumentNullException(nameof(folderPaths));
                // Not sure what happens if we have a null folderPaths, but we may as well try it.
                return folderPaths;
            }

            if (!Directory.Exists(folderRoot))
            {
                return folderPaths;
            }

            if (string.IsNullOrEmpty(prefixPath) == false)
            {
                int index = folderRoot.Length > prefixPath.Length + 1 ? prefixPath.Length + 1 : prefixPath.Length;
                string newPath = folderRoot[index..];
                if (false == string.IsNullOrWhiteSpace(newPath))
                {
                    folderPaths.Add(newPath);
                }
            }
            else
            {
                folderPaths.Add(folderRoot);
            }

            DirectoryInfo[] subDirs;
            // Recursively descend subfolders, collecting directory info on the way
            // Note that while folders without images are also collected, these will eventually be skipped when it is later scanned for images to load
            try
            {
                DirectoryInfo dirInfo = new(folderRoot);
                subDirs = dirInfo.GetDirectories();
            }
            catch
            {
                // It may fail if there is a permissions issue
                return folderPaths;
            }
            foreach (DirectoryInfo subDir in subDirs)
            {
                // Skip the following folders
                if (subDir.Name == Constant.File.BackupFolder || subDir.Name == Constant.File.DeletedFilesFolder || subDir.Name == Constant.File.VideoThumbnailFolderName)
                {
                    continue;
                }
                return GetAllFoldersExceptBackupAndDeletedFolders(subDir.FullName, folderPaths, prefixPath);
            }
            return folderPaths;
        }
        #endregion

        #region Public Static Methods - Get Missing Folders
        // For each missingFolderPath, gets its folder name and search for its first counterpart in the subdirectory under rootPath.
        // Returns a dictionary where 
        // - key is each missing relativePath, 
        // - value is the possible found relativePath, or string.Empty if there is no match
        public static Dictionary<string, List<string>> TryGetMissingFolders(string rootPath, List<string> missingFolderPaths)
        {
            if (missingFolderPaths == null)
            {
                return null;
            }
            List<string> allFolderPaths = [];
            GetAllFoldersContainingAnImageOrVideo(rootPath, allFolderPaths, rootPath);
            Dictionary<string, List<string>> matchingFolders = [];
            foreach (string missingFolderPath in missingFolderPaths)
            {
                string missingFolderName = Path.GetFileName(missingFolderPath);
                List<string> matches = [];
                foreach (string oneFolderPath in allFolderPaths)
                {
                    string allRelativePathName = Path.GetFileName(oneFolderPath);
                    if (String.Equals(missingFolderName, allRelativePathName))
                    {
                        matches.Add(oneFolderPath);
                    }
                }
                matchingFolders.Add(missingFolderPath, matches);
            }
            return matchingFolders;
        }

        public static Dictionary<string, List<string>> TryGetMissingFoldersStringent(string rootPath, List<string> missingFolderPaths, FileDatabase fileDatabase)
        {
            if (missingFolderPaths == null || fileDatabase == null)
            {
                return null;
            }

            List<string> allFolderPaths = [];
            GetAllFoldersContainingAnImageOrVideo(rootPath, allFolderPaths, rootPath);
            Dictionary<string, List<string>> matchingFolders = [];
            foreach (string missingFolderPath in missingFolderPaths)
            {
                // Count the number of entries in the database matching this relative path
                int countInDatabase = fileDatabase.CountAllFilesMatchingRelativePath(missingFolderPath);

                string missingFolderName = Path.GetFileName(missingFolderPath);
                List<string> matches = [];
                foreach (string oneFolderPath in allFolderPaths)
                {
                    string allRelativePathName = Path.GetFileName(oneFolderPath);
                    if (String.Equals(missingFolderName, allRelativePathName))
                    {
                        string[] extensions =
                        [
                            Constant.File.JpgFileExtension,
                            Constant.File.AviFileExtension,
                            Constant.File.ASFFileExtension,
                            Constant.File.Mp4FileExtension,
                            Constant.File.MovFileExtension
                        ];
                        List<string> filesInFolder =
                            [.. Directory.EnumerateFiles(Path.Combine(rootPath, oneFolderPath))];
                        int fileCount = 0;
                        foreach (string extension in extensions)
                        {
                            fileCount += filesInFolder.Count(f =>
                                f.EndsWith(extension, StringComparison.CurrentCultureIgnoreCase));
                        }
                        // Only add the folder if it has the same number of images/videos in it.
                        if (fileCount == countInDatabase)
                        {
                            matches.Add(oneFolderPath);
                        }
                        //Debug.Print(countInDatabase.ToString() + " | " + fileCount + " | " + missingFolderName + " | " + oneFolderPath);
                    }
                }
                matchingFolders.Add(missingFolderPath, matches);
            }
            return matchingFolders;
        }
        #endregion

        #region Public Static Methods - Search Folders
        /// <summary>
        /// Search for and return the relative path to all folders under the root folder that have a file with the same name as the fileName.
        /// </summary>
        /// <param name="rootFolder">the path to the root folder containing the template</param>
        /// <param name="fileName">the name of the file</param>
        /// <returns>List-Tuple-string,string- a list of tuples, each tuple comprising the RelativePath as Item1, and the File's name as Item2</returns>
        public static List<Tuple<string, string>> SearchForFoldersContainingFileName(string rootFolder, string fileName)
        {
            List<string> foundFiles = [];
            GetAllFilesInFoldersAndSubfoldersMatchingPattern(rootFolder, fileName, true, true, foundFiles);
            // strip off the root folder, leaving just the relative path/filename portion

            List<Tuple<string, string>> relativePathFileNameList = [];
            foreach (string foundFile in foundFiles)
            {
                Tuple<string, string, string> tuple = SplitFullPath(rootFolder, foundFile);
                if (null == tuple)
                {
                    continue;
                }
                relativePathFileNameList.Add(new(tuple.Item2, tuple.Item3));
            }
            return relativePathFileNameList;
        }
        #endregion

        #region Public Static Methods - Split Full Path
        // Given a root path (e.g., C:/user/timelapseStuff) and a full path e.g., C:/user/timelapseStuff/Sites/Camera1/img1.jpg), return
        // - a tuple as the root path, the relativePath, and the filename. e.g.,  C:/user/timelapseStuff, Sites/Camera1, img1.jpg)
        // - null if the arguments are null, or if the full path is outside of the root path (i.e., where the full path does not start with the root Path)
        public static Tuple<string, string, string> SplitFullPath(string rootPath, string fullPath)
        {
            if (fullPath == null || rootPath == null)
            {
                return null;
            }

            string directoryName = Path.GetDirectoryName(fullPath);
            if (directoryName == null)
            {
                // Should not happen, as only occurs when a path is in the root drive.
                // Shouldn't normally happen, i.e., Only happens if its a drive e.g., C:
                // NOt sure if this workaround works
                TracePrint.NullException(nameof(directoryName));
                directoryName = Path.GetPathRoot(fullPath);
            }

            if (false == directoryName!.StartsWith(rootPath))
            {
                return null;
            }
            string fileName = Path.GetFileName(fullPath);
            directoryName = directoryName.TrimEnd('\\');
            string relativePath = rootPath.Equals(directoryName) ? string.Empty : directoryName[(rootPath.Length + 1)..];
            return new(rootPath, relativePath, fileName);
        }
        #endregion

        #region Public Static Methods - Split Relative Path
        // ReSharper disable once UnusedMember.Global
        public static List<string> SplitRelativePath(string relativePath)
        {
            return [.. relativePath.Split('\\')];
        }

        public static List<string> SplitAsCascadingRelativePath(string relativePath)
        {
            if (relativePath == null)
            {
                return [];
            }
            string[] pathParts = relativePath.Split('\\');
            List<string> cascadingPathParts = [];
            string previousItem = string.Empty;
            foreach (string pathPart in pathParts)
            {
                string currentItem = string.IsNullOrWhiteSpace(previousItem)
                    ? pathPart
                    : $"{previousItem}\\{pathPart}";
                cascadingPathParts.Add(currentItem);
                previousItem = currentItem;
            }

            return cascadingPathParts;
        }

        #endregion

        #region Public Static Methods - TruncateFileNameForDisplay

        // Truncate a file name for display purposes to fit within the indicated length, adding ellipses as needed, preferably in the path section
        // An example returned value might be: C:\Users\Owner\Deskto…\Test sets\MergeLarge\foo\TimelapseData.ddb
        public static string TruncateFileNameForDisplay(string fileName, string path, int length)
        {
            if (fileName == null) return string.Empty;
            string newFile = Path.Combine(path, fileName);
            if (newFile.Length <= length)
            {
                // Its less than the length, so return it unchanged
                return newFile;
            }

            int fileNameLength = fileName.Length;
            if (fileNameLength > length)
            {
                // /The file name itself is longer than the length, so truncate it
                return $"…{fileName.Substring(fileName.Length - length - 1)}";
            }
            // The file name length is less than the length, so truncate the path
            int desiredPathLength = length - fileNameLength - 2; // -1 for the path separator and ellipsis

            // Get the front half and the back half of the path, and stitch them together with an ellipsis in between
            string frontPath = path.Substring(0, desiredPathLength/2);
            string backPath = path.Substring(path.Length - desiredPathLength/2);
            string pathAfterTruncation = $"{frontPath}…{backPath}";
            return Path.Combine(pathAfterTruncation, fileName);

        }
        #endregion

        #region Public Static Methods - Get Path Parts
        // Given a relative path return the subfolder path after the first folder e.g., e.g. a\b\c returns b\c
        // If its a root folder, return "" as there are not folders after that e.g., a returns ""
        public static string GetRelativePathSubFolder(string path)
        {
            int indx = path.NthIndexOf(Path.DirectorySeparatorChar.ToString(), 1);
            if (indx == -1 || indx == path.Length)
            {
                return string.Empty;
            }

            return path[(indx + 1)..];
        }

        // Given a relative path return the first folder in the path e.g. e.g. a\b\c, returns a
        // If its a root folder, then just return that e.g., a returns a
        public static string GetRelativePathRootFolder(string path)
        {
            int indx = path.NthIndexOf(Path.DirectorySeparatorChar, 1);
            return indx == -1
                ? path // the path is just the root folder
                : path[..indx]; // trim off everything from the first path separator onwards
        }
        #endregion

        #region Public Static Methods - Find the difference between two paths
        // Find the difference between two paths (ignoring the file name, if any) and return it
        // For example, given:
        // path1 =    "C:\\Users\\Owner\\Desktop\\Test sets\\MergeLarge\\foo\\TimelapseData.ddb"
        // path2 =    "C:\\Users\\Owner\\Desktop\\Test sets\\MergeLarge" 
        // return     "foo"
        public static string GetDifferenceBetweenPathAndSubPath(string path1, string path2)
        {
            if (path1 == null || path2 == null)
            {
                return string.Empty;
            }

            // If its a file, strip the file name off the path
            path1 = File.GetAttributes(path1).HasFlag(FileAttributes.Directory)
                ? path1
                : Path.GetDirectoryName(path1);
            // If its a file, strip the file name off the path
            path2 = File.GetAttributes(path2).HasFlag(FileAttributes.Directory)
                ? path2
                : Path.GetDirectoryName(path2);


            if (String.CompareOrdinal(path1, path2) == 0)
            {
                // both paths are identical 
                return string.Empty;
            }


            return path1?.Length > path2?.Length
            ? path1.Replace(path2 + "\\", "")
            : path2?.Replace(path1 + "\\", "");

        }
        #endregion

        #region Public Static Methods - Various forms to get the full path of a file
        public static string GetFullPath(FileDatabase fileDatabase, ImageRow imageRow)
        {
            if (fileDatabase == null || imageRow == null)
            {
                return string.Empty;
            }
            return Path.Combine(fileDatabase.RootPathToImages, imageRow.RelativePath, imageRow.File);
        }

        public static string GetFullPath(string rootPath, ImageRow imageRow)
        {
            if (imageRow == null)
            {
                return string.Empty;
            }
            return Path.Combine(rootPath, imageRow.RelativePath, imageRow.File);
        }

        public static string GetFullPath(string rootPath, string relativePath, string fileName)
        {
            return Path.Combine(rootPath, relativePath, fileName);
        }
        #endregion

        #region  Public Static Methods - File/Folder tests
        /// <summary>
        /// // return true iff the file path ends with .jpg
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static FileExtensionEnum GetFileTypeByItsExtension(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return FileExtensionEnum.IsNotImageOrVideo;
            }
            if (path.EndsWith(Constant.File.JpgFileExtension, StringComparison.InvariantCultureIgnoreCase))
            {
                return FileExtensionEnum.IsImage;
            }
            if (path.EndsWith(Constant.File.AviFileExtension, StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(Constant.File.Mp4FileExtension, StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(Constant.File.MovFileExtension, StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(Constant.File.ASFFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return FileExtensionEnum.IsVideo;
            }
            return FileExtensionEnum.IsNotImageOrVideo;
        }

        // Return true if any of the files in the fileinfo list includes at least  image or video
        public static bool CheckFolderForAtLeastOneImageOrVideoFiles(string folderPath)
        {
            DirectoryInfo directoryInfo;
            try
            {
                directoryInfo = new(folderPath);
            }
            catch
            {
                // The call may fail if the OS denies access because of an I/O error or a specific type of security error
                return false;
            }

            foreach (string extension in new List<string> { Constant.File.JpgFileExtension, Constant.File.AviFileExtension, Constant.File.Mp4FileExtension, Constant.File.ASFFileExtension, Constant.File.MovFileExtension })
            {
                List<FileInfo> fileInfoList = [];
                try
                {
                    fileInfoList.AddRange(directoryInfo.GetFiles("*" + extension));
                }
                catch
                {
                    // The call may fail if the OS denies access because of an I/O error or a specifi type of security error
                    continue;
                }
                FilesRemoveAllButImagesAndVideos(fileInfoList);
                if (fileInfoList.Any(x => x.Name.EndsWith(extension, StringComparison.InvariantCultureIgnoreCase)))
                {
                    return true;
                }
            }
            return false;
        }

        // Unused but keep for now in case it becomes useful at some point
        // Return true if a file with the given extension exists in the provided folder path
        //public static int CountFilesInFolderWithExtension(string folderPath, string extension)
        //{
        //    return Directory.GetFiles(folderPath, "*" + extension).Length;
        //}
        #endregion

        #region Public Static Methods - Video files

        // Returns
        // - the actual video duration in seconds
        // Error cases:
        // - returns null if its not a video
        // - returns -1 if the file is missing
        // - retunrs 0 if it can't get the duration
        // 
        public static float? GetVideoDuration(string filePath)
        {
            // Make sure the file is a video file and that it actually exists
            if (false == Util.IsCondition.IsVideoExtension(filePath))
            {
                return null;
            }

            if (false == File.Exists(filePath))
            {
                return -1;
            }

            using ShellObject shell = ShellObject.FromParsingName(filePath);
            // alternatively: shell.Properties.GetProperty("System.Media.Duration");
            ShellProperty<ulong?> prop = shell?.Properties?.System?.Media.Duration;
            // Duration will be formatted as 00:44:08
            if (prop == null)
            {
                return 0;
            }
            string durationAsString = prop.FormatForDisplay(PropertyDescriptionFormatOptions.None);
            if (TimeSpan.TryParse(durationAsString, out TimeSpan duration))
            {
                return (float?)duration.TotalSeconds;
            }
            return 0;
        }
        #endregion

        #region Identify System folders, including the recycle bin
        public static bool IsFolderSystemOrHidden(string folderPath)
        {
            return IsFolderSystemOrHidden(new DirectoryInfo(folderPath).Attributes);
        }

        public static bool IsFolderSystemOrHidden(FileAttributes attributes)
        {

            return ((attributes & FileAttributes.System) == FileAttributes.System) ||
                   ((attributes & FileAttributes.Hidden) == FileAttributes.Hidden);
        }

        public static bool IsFolderPathADriveLetter(string path)
        {
            return Path.GetPathRoot(path) == path;
        }
        #endregion

        #region Generate file names
        // Generate a unique name if the file name already exists,
        // where it appends it with a number e.g., filename_1.extension
        // return true if the fileNname was changed
        public static bool GenerateFileNameIfNeeded(string path, string fileName, out string newFileName)
        {
            newFileName = fileName;
            string baseFileName = Path.GetFileNameWithoutExtension(newFileName);
            string extension = Path.GetExtension(fileName);
            string completeFilePath = Path.Combine(path, newFileName);
            int index = 0;
            while (File.Exists(completeFilePath) || Directory.Exists(completeFilePath))
            {
                // A file or folder with that name already exists, so generate a new file or folder name
                newFileName = $"{baseFileName}_{++index}{extension}";
                completeFilePath = Path.Combine(path, newFileName);
            }
            return index > 0;
        }
        #endregion

        #region Copy stream to file
        // Copy the file provided in the resource path to the file path
        // Checks to that file path (e.g., if the folder exists, there's a file already there, etc.)
        // should be done before invoking this
        // Return false if it can't be done
        // For example, the resource path could a file saved as a resource, e.g. something like: "pack://application:,,,/Resources/AlbertaMetadataStandardsOverview.rtf"
        public static bool CopyResourceToFile(string resourcePackPath, string filePath)
        {
            try
            {
                StreamResourceInfo standard = Application.GetResourceStream(new(resourcePackPath));
                using FileStream file = File.Create(filePath);
                if (standard?.Stream == null)
                {
                    return false;
                }

                byte[] buffer = new byte[8 * 1024];
                int len;
                while ((len = standard.Stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    file.Write(buffer, 0, len);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion

        #region Private (internal) methods
        // Remove, any files that 
        // - don't exactly match the desired image or video extension, 
        // - have a MacOSX hidden file prefix
        // When files from a MacOSX system are copied to Windows, it may produce 'hidden' files mirroring the valid files. 
        // These are prefixed by '._' and are not actually a valid image or video
        private static void FilesRemoveAllButImagesAndVideos(List<FileInfo> fileInfoList)
        {
            fileInfoList.RemoveAll(x => !(x.Name.EndsWith(Constant.File.JpgFileExtension, StringComparison.InvariantCultureIgnoreCase)
                                   || x.Name.EndsWith(Constant.File.AviFileExtension, StringComparison.InvariantCultureIgnoreCase)
                                   || x.Name.EndsWith(Constant.File.Mp4FileExtension, StringComparison.InvariantCultureIgnoreCase)
                                   || x.Name.EndsWith(Constant.File.MovFileExtension, StringComparison.InvariantCultureIgnoreCase)
                                   || x.Name.EndsWith(Constant.File.ASFFileExtension, StringComparison.InvariantCultureIgnoreCase))
                                   || x.Name.StartsWith(Constant.File.MacOSXHiddenFilePrefix, StringComparison.Ordinal));
        }

        private static List<string> FilesRemoveAllButImagesAndVideos(List<string> fileList)
        {
            fileList.RemoveAll(x => !(x.EndsWith(Constant.File.JpgFileExtension, StringComparison.InvariantCultureIgnoreCase)
                                          || x.EndsWith(Constant.File.AviFileExtension, StringComparison.InvariantCultureIgnoreCase)
                                          || x.EndsWith(Constant.File.Mp4FileExtension, StringComparison.InvariantCultureIgnoreCase)
                                          || x.EndsWith(Constant.File.MovFileExtension, StringComparison.InvariantCultureIgnoreCase)
                                          || x.EndsWith(Constant.File.ASFFileExtension, StringComparison.InvariantCultureIgnoreCase))
                                          || x.StartsWith(Constant.File.MacOSXHiddenFilePrefix, StringComparison.Ordinal));
            return fileList;
        }

        private static void GetAllImageAndVideoFilesInFolderAndSubfolders(string rootFolderPath, List<FileInfo> fileInfoList, List<string> foldersWithImagesOrVideos, int recursionLevel)
        {
            bool folderAddedToFolderList = false;
            // Check the arguments for null 
            if (fileInfoList == null)
            {
                // this should not happen
                TracePrint.StackTrace(1);
                // Not show what happens if we return with a null fileList, but its worth a shot
                // throw new ArgumentNullException(nameof(control));
                return;
            }

            int nextRecursionLevel = recursionLevel + 1;
            if (!Directory.Exists(rootFolderPath))
            {
                return;
            }

            DirectoryInfo directoryInfo = new(rootFolderPath);
            // If its a system or hidden folder, skip it. (drive letters are system folders!)
            if (IsFolderSystemOrHidden(directoryInfo.Attributes))
            {
                return;
            }
            foreach (string extension in new List<string> { Constant.File.JpgFileExtension, Constant.File.AviFileExtension, Constant.File.Mp4FileExtension, Constant.File.ASFFileExtension, Constant.File.MovFileExtension })
            {
                // GetFiles has a 'bug', where it can match an extension even if there are more letters after the extension. 
                // That is, if we are looking for *.jpg, it will not only return *.jpg files, but files such as *.jpgXXX
                FileInfo[] fileInfo = directoryInfo.GetFiles("*" + extension);
                if (fileInfo.Length > 0)
                {
                    if (false == folderAddedToFolderList)
                    {
                        // Since it has a video or image in it, add it to the folders list (but only once)
                        // TODO: Not perfect, as it may include folders with jpgxxx etc, but should be ok.
                        foldersWithImagesOrVideos.Add(rootFolderPath);
                        folderAddedToFolderList = true;
                    }
                    fileInfoList.AddRange(fileInfo);
                }
                //fileInfoList.AddRange(directoryInfo.GetFiles("*" + extension));
            }

            // Recursively descend subfolders
            DirectoryInfo[] subDirs;
            try
            {
                DirectoryInfo dirInfo = new(rootFolderPath);
                subDirs = dirInfo.GetDirectories();
            }
            catch
            {
                // It may fail if there is a permissions issue.
                return;
            }
            foreach (DirectoryInfo subDir in subDirs)
            {
                // Skip the following folders
                if (subDir.Name == Constant.File.BackupFolder || subDir.Name == Constant.File.DeletedFilesFolder
                                                              || subDir.Name == Constant.File.VideoThumbnailFolderName
                                                              || subDir.Name == Constant.File.NetworkRecycleBin)
                {
                    continue;
                }
                GetAllImageAndVideoFilesInFolderAndSubfolders(subDir.FullName, fileInfoList, foldersWithImagesOrVideos, nextRecursionLevel);
            }

            if (recursionLevel == 0)
            {
                // After all recursion is complete, do the following (but only on the initial recursion level)
                // Because of that bug, we need to check for, and remove, any files that don't exactly match the desired extension
                // At the same time, we also remove MacOSX hidden files, if any
                FilesRemoveAllButImagesAndVideos(fileInfoList);
                if (fileInfoList.Count != 0)
                {
                    // ReSharper disable once RedundantAssignment
                    fileInfoList = [.. fileInfoList.OrderBy(file => file.FullName)];
                }
            }
        }
        #endregion
    }
}
