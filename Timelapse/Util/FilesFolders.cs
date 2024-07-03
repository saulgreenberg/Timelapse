using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Resources;
using Timelapse.Database;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;

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
            else
            {
                return false;
            }
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
            else
            {
                return false;
            }
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
                        Directory.Move(sourceFolderPath, destinationFolderPath);
                    }
                    else
                    {
                        TracePrint.PrintMessage($"Move not done: destination folder '{destinationFolderPath} already exists.");
                        return MoveFolderResultEnum.FailAsDestinationFolderExists;
                    }
                    return MoveFolderResultEnum.Success;
                }
                else
                {
                    TracePrint.PrintMessage($"Move not done: the source folder '{sourceFolderPath} does not exists.");
                    return MoveFolderResultEnum.FailAsSourceFolderDoesNotExist;
                }
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
            if (FilesFolders.IsFolderPathADriveLetter(Path.GetDirectoryName(filePath)))
            {
                return DatabaseFileErrorsEnum.FileInRootDriveFolder;
            }
            if (FilesFolders.IsFolderSystemOrHidden(Path.GetDirectoryName(filePath)))
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
            SQLiteWrapper db = new SQLiteWrapper(filePath);
            if (extension == Constant.File.TemplateDatabaseFileExtension)
            {
                // Template tests for .tdb files
                // Template Test: at least one row exists with the type file (to ensure its not an empty table)
                if (0 == db.ScalarGetCountFromSelect(Sql.SelectCountStarFrom + Constant.DBTables.Template + Sql.Where + Sql.TypeEquals + Sql.Quote(Constant.DatabaseColumn.File)))
                {
                    return DatabaseFileErrorsEnum.InvalidDatabase;
                }

                // Template Test: TemplateInfo exists (and from prior tests at least one row with the type file)
                if (db.TableExists(Constant.DBTables.TemplateInfo))
                {
                    // Check if its a template that was opened with a pre2.3 version of Timelapse, which would re-insert the UTCOffset type...
                    // If so, this would have to be fixed.
                    string typeQuery = Sql.Select + Constant.Control.Type + Sql.From + Constant.DBTables.Template + Sql.Where + Constant.Control.Type + Sql.Equal + Sql.Quote(Constant.ControlDeprecated.UtcOffsetLabel);
                    DataTable table = db.GetDataTableFromSelect(typeQuery);
                    if (table.Rows.Count > 0)
                    {
                        return DatabaseFileErrorsEnum.UTCOffsetTypeExistsInUpgradedVersion;
                    }

                    // High probability that its a good db
                    return DatabaseFileErrorsEnum.Ok;
                }

                // Test: Template exists (only) (and from prior tests no TemplateIno)
                if (db.TableExists(Constant.DBTables.Template))
                {
                    // High probability that its a good db, but pre version 2.3.0.0 as the TemplateInfo table does not exist
                    return DatabaseFileErrorsEnum.PreVersion2300;
                }
            }
            else
            {
                // Test Data .ddb file 
                // Data Test: basic test to make sure it seems to be a valid ddb
                if (false == db.TableExists(Constant.DBTables.FileData))
                {
                    return DatabaseFileErrorsEnum.InvalidDatabase;
                }

                // Test: if its missing the VersionCompatability column, its pre 2.3.0.0
                if (false == db.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.VersionCompatabily))
                {
                    return DatabaseFileErrorsEnum.PreVersion2300;
                }

                // Test: Get the versionCompatability value and test if its 2.3.0.0 or later
                string versionQuery = Sql.Select + Constant.DatabaseColumn.VersionCompatabily + Sql.From + Constant.DBTables.ImageSet + Sql.Where + Constant.DatabaseColumn.ID + Sql.Equal + Sql.Quote(Constant.DatabaseValues.ImageSetRowID.ToString());
                DataTable table = db.GetDataTableFromSelect(versionQuery);
                if (table.Rows.Count > 0)
                {
                    string thisVersion = (string)table.Rows[0][Constant.DatabaseColumn.VersionCompatabily];
                    if (VersionChecks.IsVersion1GreaterOrEqualToVersion2(thisVersion, Constant.DatabaseValues.VersionNumberMinimum))
                    {
                        // But - Special case as UTCOffset column could be added if the DB was opened with a pre2.3 version of Timelapse.
                        if (db.SchemaIsColumnInTable(Constant.DBTables.FileData, Constant.ControlDeprecated.UtcOffsetLabel))
                        {
                            return DatabaseFileErrorsEnum.PreVersion2300;
                        }

                        // Now just check to see if we are opening the .ddb file with an older version of Timelapse that last opened it...
                        string timelapseCurrentVersionNumber = VersionChecks.GetTimelapseCurrentVersionNumber().ToString();
                        if (VersionChecks.IsVersion1GreaterOrEqualToVersion2(timelapseCurrentVersionNumber, thisVersion))
                        {
                            return DatabaseFileErrorsEnum.Ok;
                        }
                        else
                        {
                            return DatabaseFileErrorsEnum.OkButOpenedWithAnOlderTimelapseVersion;
                        }
                    }
                    else
                    {
                        return DatabaseFileErrorsEnum.PreVersion2300;
                    }
                }
            }

            // It should never get here
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
                    folderPaths.Add(folderRoot.Substring(index));
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
                DirectoryInfo dirInfo = new DirectoryInfo(folderRoot);
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
            if (foundFiles == null)
            {
                // This should not be required, as it should have been initialized before this call
                foundFiles = new List<string>();
            }
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
                List<string> foundFiles = Directory.GetFiles(folder).ToList();
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
            return await Task.Run(() => FilesFolders.GetAllFoldersExceptBackupAndDeletedFolders(rootFolderPath, new List<string>(), rootFolderPrefix));
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
                string newPath = folderRoot.Substring(index);
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
                DirectoryInfo dirInfo = new DirectoryInfo(folderRoot);
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
            List<string> allFolderPaths = new List<string>();
            Util.FilesFolders.GetAllFoldersContainingAnImageOrVideo(rootPath, allFolderPaths, rootPath);
            Dictionary<string, List<string>> matchingFolders = new Dictionary<string, List<string>>();
            foreach (string missingFolderPath in missingFolderPaths)
            {
                string missingFolderName = Path.GetFileName(missingFolderPath);
                List<string> matches = new List<string>();
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

            List<string> allFolderPaths = new List<string>();
            Util.FilesFolders.GetAllFoldersContainingAnImageOrVideo(rootPath, allFolderPaths, rootPath);
            Dictionary<string, List<string>> matchingFolders = new Dictionary<string, List<string>>();
            foreach (string missingFolderPath in missingFolderPaths)
            {
                // Count the number of entries in the database matching this relative path
                int countInDatabase = fileDatabase.CountAllFilesMatchingRelativePath(missingFolderPath);

                string missingFolderName = Path.GetFileName(missingFolderPath);
                List<string> matches = new List<string>();
                foreach (string oneFolderPath in allFolderPaths)
                {
                    string allRelativePathName = Path.GetFileName(oneFolderPath);
                    if (String.Equals(missingFolderName, allRelativePathName))
                    {
                        string[] extensions =
                        {
                            Constant.File.JpgFileExtension,
                            Constant.File.AviFileExtension,
                            Constant.File.ASFFileExtension,
                            Constant.File.Mp4FileExtension,
                            Constant.File.MovFileExtension,
                        };
                        List<string> filesInFolder =
                            Directory.EnumerateFiles(Path.Combine(rootPath, oneFolderPath)).ToList();
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
            List<string> foundFiles = new List<string>();
            GetAllFilesInFoldersAndSubfoldersMatchingPattern(rootFolder, fileName, true, true, foundFiles);
            // strip off the root folder, leaving just the relative path/filename portion

            List<Tuple<string, string>> relativePathFileNameList = new List<Tuple<string, string>>();
            foreach (string foundFile in foundFiles)
            {
                Tuple<string, string, string> tuple = SplitFullPath(rootFolder, foundFile);
                if (null == tuple)
                {
                    continue;
                }
                relativePathFileNameList.Add(new Tuple<string, string>(tuple.Item2, tuple.Item3));
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

            if (false == directoryName.StartsWith(rootPath))
            {
                return null;
            }
            string fileName = Path.GetFileName(fullPath);
            directoryName = directoryName.TrimEnd('\\');
            string relativePath = rootPath.Equals(directoryName) ? string.Empty : directoryName.Substring(rootPath.Length + 1);
            return new Tuple<string, string, string>(rootPath, relativePath, fileName);
        }
        #endregion

        #region Public Static Methods - Split Relative Path
        public static List<string> SplitRelativePath(string relativePath)
        {
            return relativePath.Split('\\').ToList();
        }

        public static List<string> SplitAsCascadingRelativePath(string relativePath)
        {
            if (relativePath == null)
            {
                return new List<string>();
            }
            string[] pathParts = relativePath.Split('\\');
            List<string> cascadingPathParts = new List<string>();
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
            return Path.Combine(fileDatabase.FolderPath, imageRow.RelativePath, imageRow.File);
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
                directoryInfo = new DirectoryInfo(folderPath);
            }
            catch
            {
                // The call may fail if the OS denies access because of an I/O error or a specific type of security error
                return false;
            }

            foreach (string extension in new List<string>() { Constant.File.JpgFileExtension, Constant.File.AviFileExtension, Constant.File.Mp4FileExtension, Constant.File.ASFFileExtension, Constant.File.MovFileExtension })
            {
                List<FileInfo> fileInfoList = new List<FileInfo>();
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

        // Return true if a file with the given extension exists in the provided folder path
        public static int CountFilesInFolderWithExtension(string folderPath, string extension)
        {
            return Directory.GetFiles(folderPath, "*" + extension).Length;
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
                StreamResourceInfo standard = System.Windows.Application.GetResourceStream(new Uri(resourcePackPath));
                using (Stream file = System.IO.File.Create(filePath))
                {
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
                }
                return true;
            }
            catch (Exception )
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
                                   || x.Name.IndexOf(Constant.File.MacOSXHiddenFilePrefix, StringComparison.Ordinal) == 0);
        }

        private static List<string> FilesRemoveAllButImagesAndVideos(List<string> fileList)
        {
            fileList.RemoveAll(x => !(x.EndsWith(Constant.File.JpgFileExtension, StringComparison.InvariantCultureIgnoreCase)
                                          || x.EndsWith(Constant.File.AviFileExtension, StringComparison.InvariantCultureIgnoreCase)
                                          || x.EndsWith(Constant.File.Mp4FileExtension, StringComparison.InvariantCultureIgnoreCase)
                                          || x.EndsWith(Constant.File.MovFileExtension, StringComparison.InvariantCultureIgnoreCase)
                                          || x.EndsWith(Constant.File.ASFFileExtension, StringComparison.InvariantCultureIgnoreCase))
                                          || x.IndexOf(Constant.File.MacOSXHiddenFilePrefix, StringComparison.Ordinal) == 0);
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

            DirectoryInfo directoryInfo = new DirectoryInfo(rootFolderPath);
            // If its a system or hidden folder, skip it. (drive letters are system folders!)
            if (IsFolderSystemOrHidden(directoryInfo.Attributes))
            {
                return;
            }
            foreach (string extension in new List<string>() { Constant.File.JpgFileExtension, Constant.File.AviFileExtension, Constant.File.Mp4FileExtension, Constant.File.ASFFileExtension, Constant.File.MovFileExtension })
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
                DirectoryInfo dirInfo = new DirectoryInfo(rootFolderPath);
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
                    fileInfoList = fileInfoList.OrderBy(file => file.FullName).ToList();
                }
            }
        }
        #endregion
    }
}
