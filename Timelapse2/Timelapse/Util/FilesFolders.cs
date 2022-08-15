using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Timelapse.Database;
using Timelapse.Enums;

namespace Timelapse.Util
{
    /// <summary>
    /// Static convenience methods to Get information about files, folders and paths
    /// </summary>
    public static class FilesFolders
    {

        #region Public static methods - Check if the database is valid, and return error status reports if it isn't
        // Only invoke this on a .tdb or .ddb file
        public static DatabaseFileErrorsEnum QuickCheckDatabaseFile(string filePath)
        {
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

            // Check the file path length and notify the user the template couldn't be loaded because its path is too long 
            if (IsCondition.IsPathLengthTooLong(filePath))
            {
                return DatabaseFileErrorsEnum.PathTooLong;
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
                        return DatabaseFileErrorsEnum.Ok;
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
        /// <summary>
        /// Populate fileInfoList  with the .jpg, .avi, and .mp4 files found in the rootFolderPath and its sub-folders
        /// </summary>
        /// <param name="rootFolderPath">The complete path to the root folder</param>
        /// <param name="fileInfoList">found files are added to this list</param>        
        public static void GetAllImageAndVideoFilesInFolderAndSubfolders(string rootFolderPath, List<FileInfo> fileInfoList)
        {
            GetAllImageAndVideoFilesInFolderAndSubfolders(rootFolderPath, fileInfoList, 0);
        }

        /// <summary>
        /// Populate folderPaths with all the folders and subfolders (from the root folder) that contains at least one video or image file
        /// If prefixPath is provided, it is stripped from the beginning of the matching folder paths, otherwise the full path is returned
        /// </summary>
        /// <param name="folderRoot"></param>
        /// <param name="folderPaths"></param>
        public static void GetAllFoldersContainingAnImageOrVideo(string folderRoot, List<string> folderPaths, string prefixPath)
        {
            // Check the arguments for null 
            if (folderPaths == null || folderRoot == null)
            {
                // this should not happen
                TracePrint.PrintStackTrace(1);
                // throw new ArgumentNullException(nameof(folderPaths));
                // Not sure what happens if we have a null folderPaths, but we may as well try it.
                return;
            }

            if (!Directory.Exists(folderRoot))
            {
                return;
            }
            // Add a folder only if it contains one of the desired extensions
            if (CheckFolderForAtLeastOneImageOrVideoFiles(folderRoot) == true)
            {
                if (String.IsNullOrEmpty(prefixPath) == false)
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
                    foundFiles.AddRange(System.IO.Directory.GetFiles(startFolder, pattern, SearchOption.TopDirectoryOnly));
                    foreach (string directory in Directory.GetDirectories(startFolder))
                    {
                        GetAllFilesInFoldersAndSubfoldersMatchingPattern(directory, pattern, true, true, foundFiles);
                    }
                }
            }
            catch (System.Exception)
            {
                return null;
            }
            return foundFiles;
        }
        #endregion

        #region Public Static Methods - Get Missing Folders
        // For each missingFolderPath, gets its folder name and search for its first counterpart in the subdirectory under rootPath.
        // Returns a dictionary where 
        // - key is each missing relativePath, 
        // - value is the possible found relativePath, or String.Empty if there is no match
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
                        continue;
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
        /// <returns>List<Tuple<string,string>>a list of tuples, each tuple comprising the RelativePath as Item1, and the File's name as Item2</returns>
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
        // Given a root path (e.g., C:/user/timelapseStuff) and a full path e.g., C:/user/timelapseStuff/Sites/Camera1/img1.jpg)
        // return a tuple as the root path, the relativePath, and the filename. e.g.,  C:/user/timelapseStuff, Sites/Camera1, img1.jpg)
        public static Tuple<string, string, string> SplitFullPath(string rootPath, string fullPath)
        {
            if (fullPath == null || rootPath == null)
            {
                return null;
            }
            string fileName = Path.GetFileName(fullPath);
            string directoryName = Path.GetDirectoryName(fullPath).TrimEnd('\\');

            //string relativePath = fullPath.Substring(rootPath.Length + 1, fullPath.Length - fileName.Length - rootPath.Length - 1);
            string relativePath = rootPath.Equals(directoryName) ? String.Empty : directoryName.Substring(rootPath.Length + 1);
            //string relativePath = directoryName.Substring(rootPath.Length + 1);
            return new Tuple<string, string, string>(rootPath, relativePath, fileName);
        }
        #endregion

        #region Public Static Methods - Various forms to get the full path of a file
        public static string GetFullPath(FileDatabase fileDatabase, ImageRow imageRow)
        {
            if (fileDatabase == null || imageRow == null)
            {
                return String.Empty;
            }
            return Path.Combine(fileDatabase.FolderPath, imageRow.RelativePath, imageRow.File);
        }

        public static string GetFullPath(string rootPath, ImageRow imageRow)
        {
            if (imageRow == null)
            {
                return String.Empty;
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
            if (String.IsNullOrEmpty(path))
            {
                return FileExtensionEnum.IsNotImageOrVideo;
            }
            if (path.EndsWith(Constant.File.JpgFileExtension, StringComparison.InvariantCultureIgnoreCase))
            {
                return FileExtensionEnum.IsImage;
            }
            if (path.EndsWith(Constant.File.AviFileExtension, StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(Constant.File.Mp4FileExtension, StringComparison.OrdinalIgnoreCase) ||
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

            foreach (string extension in new List<string>() { Constant.File.JpgFileExtension, Constant.File.AviFileExtension, Constant.File.Mp4FileExtension, Constant.File.ASFFileExtension })
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
                if (fileInfoList.Any(x => x.Name.EndsWith(extension, StringComparison.InvariantCultureIgnoreCase) == true))
                {
                    return true;
                }
            }
            return false;
        }

        // Return a FileInfo List of files in the folderPath that are images or videos
        public static List<FileInfo> GetAllImageOrVideoFilesFromFolder(string folderPath)
        {
            DirectoryInfo directoryInfo;
            List<FileInfo> fileInfoList = new List<FileInfo>();
            try
            {
                directoryInfo = new DirectoryInfo(folderPath);
            }
            catch
            {
                // The call may fail if the OS denies access because of an I/O error or a specific type of security error
                return fileInfoList;
            }

            foreach (string extension in new List<string>() { Constant.File.JpgFileExtension, Constant.File.AviFileExtension, Constant.File.Mp4FileExtension, Constant.File.ASFFileExtension })
            {
                try
                {
                    fileInfoList.AddRange(directoryInfo.GetFiles("*" + extension));
                }
                catch
                {
                    // The call may fail if the OS denies access because of an I/O error or a specific type of security error
                    continue;
                }
            }
            FilesRemoveAllButImagesAndVideos(fileInfoList);
            return fileInfoList;
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
            fileInfoList.RemoveAll(x => !(x.Name.EndsWith(Constant.File.JpgFileExtension, StringComparison.InvariantCultureIgnoreCase) == true
                                   || x.Name.EndsWith(Constant.File.AviFileExtension, StringComparison.InvariantCultureIgnoreCase) == true
                                   || x.Name.EndsWith(Constant.File.Mp4FileExtension, StringComparison.InvariantCultureIgnoreCase) == true
                                   || x.Name.EndsWith(Constant.File.ASFFileExtension, StringComparison.InvariantCultureIgnoreCase) == true)
                                   || x.Name.IndexOf(Constant.File.MacOSXHiddenFilePrefix) == 0);
        }

        private static void GetAllImageAndVideoFilesInFolderAndSubfolders(string rootFolderPath, List<FileInfo> fileInfoList, int recursionLevel)
        {
            // Check the arguments for null 
            if (fileInfoList == null)
            {
                // this should not happen
                TracePrint.PrintStackTrace(1);
                // Not show what happens if we return with a null fileInfoList, but its worth a shot
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
            foreach (string extension in new List<string>() { Constant.File.JpgFileExtension, Constant.File.AviFileExtension, Constant.File.Mp4FileExtension, Constant.File.ASFFileExtension })
            {
                // GetFiles has a 'bug', where it can match an extension even if there are more letters after the extension. 
                // That is, if we are looking for *.jpg, it will not only return *.jpg files, but files such as *.jpgXXX
                fileInfoList.AddRange(directoryInfo.GetFiles("*" + extension));
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
                GetAllImageAndVideoFilesInFolderAndSubfolders(subDir.FullName, fileInfoList, nextRecursionLevel);
            }

            if (recursionLevel == 0)
            {
                // After all recursion is complete, do the following (but only on the initial recursion level)
                // Because of that bug, we need to check for, and remove, any files that don't exactly match the desired extension
                // At the same time, we also remove MacOSX hidden files, if any
                FilesRemoveAllButImagesAndVideos(fileInfoList);
                if (fileInfoList.Count != 0)
                {
                    fileInfoList = fileInfoList.OrderBy(file => file.FullName).ToList();
                }
            }
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
    }
}
