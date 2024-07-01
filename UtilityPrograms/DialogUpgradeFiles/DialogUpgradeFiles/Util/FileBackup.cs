using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
// ReSharper disable UnusedMember.Global

namespace DialogUpgradeFiles.Util
{
    public static class FileBackup
    {
        #region Public Static Methods - Get Backup-related things
        private static IEnumerable<FileInfo> GetBackupFiles(DirectoryInfo backupFolder, string sourceFilePath, bool excludeCheckpointFiles)
        {
            string sourceFileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourceFilePath);
            string sourceFileExtension = Path.GetExtension(sourceFilePath);
            string searchPattern = sourceFileNameWithoutExtension + "*" + sourceFileExtension;
            try
            {
                IEnumerable<FileInfo> backupFiles = backupFolder.GetFiles(searchPattern);
                //Skip files that have the Constant.File.BackupCheckpointIndicator, as those are left for manual removal
                if (excludeCheckpointFiles)
                {
                    backupFiles = backupFiles.Where(x => x.Name.Contains(Constant.File.BackupCheckpointIndicator) == false);
                }
                return backupFiles;
            }
            catch
            {
                return null;
            }
        }

        public static DateTime GetMostRecentBackup(string sourceFilePath)
        {
            try
            {
                DirectoryInfo backupFolder = FileBackup.GetOrCreateBackupFolder(sourceFilePath);
                FileInfo mostRecentBackupFile = null;
                if (backupFolder != null)
                {
                    mostRecentBackupFile = FileBackup.GetBackupFiles(backupFolder, sourceFilePath, false).OrderByDescending(file => file.LastWriteTime).FirstOrDefault();
                }
                if (backupFolder != null && mostRecentBackupFile != null)
                {
                    return mostRecentBackupFile.LastWriteTime;
                }
                return DateTime.MinValue;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        public static DirectoryInfo GetOrCreateBackupFolder(string sourceFilePath)
        {
            string sourceFolderPath = Path.GetDirectoryName(sourceFilePath);
            if (sourceFolderPath == null) return null;
            DirectoryInfo backupFolder = new DirectoryInfo(Path.Combine(sourceFolderPath, Constant.File.BackupFolder));   // The Backup Folder 
            if (backupFolder.Exists == false)
            {
                try
                {
                    backupFolder.Create();
                }
                catch
                {
                    return null;
                }
            }
            return backupFolder;
        }
        #endregion

        #region Public Static Methods -TryCreateBackup, various versions

        // Full version: Copy to backup folder with augmented file name
        public static string TryCreateBackup(string folderPath, string sourceFileName)
        {
            bool createAlternateBackup = false;
            string sourceFilePath = Path.Combine(folderPath, sourceFileName);
            if (File.Exists(sourceFilePath) == false)
            {
                // nothing to do
                return string.Empty;
            }

            // create backup folder if needed
            DirectoryInfo backupFolder = FileBackup.GetOrCreateBackupFolder(sourceFilePath);
            if (backupFolder == null)
            {
                // Something went wrong...
                createAlternateBackup = true;
            }

            // create a timestamped copy of the file
            // file names can't contain colons so use non-standard format for timestamp with dashes for 24 hour-minute-second separation
            // If there is a specialBackup term, then we modify anadd it before the timestamp
            string sourceFileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourceFileName);
            string sourceFileExtension = Path.GetExtension(sourceFileName);
            // If we couldn't create the backup folder, then use the alternate form
            string destinationFileName = createAlternateBackup
                ? String.Concat(sourceFileNameWithoutExtension, sourceFileExtension == Constant.File.FileDatabaseFileExtension ? ".dbk" : ".tbk")
                : String.Concat(sourceFileNameWithoutExtension, Constant.File.BackupPre23Indicator, ".", DateTime.Now.ToString("yyyy-MM-dd.HH-mm-ss"), sourceFileExtension);
            string destinationFilePath = createAlternateBackup
                ? Path.Combine(folderPath, destinationFileName)
                : Path.Combine(backupFolder.FullName, destinationFileName);

            // if the path length is too long, use the alternate form
            if (IsCondition.IsPathLengthTooLong(destinationFilePath, FilePathTypeEnum.Pre23))
            {
                destinationFileName = String.Concat(sourceFileNameWithoutExtension, sourceFileExtension == Constant.File.FileDatabaseFileExtension ? ".dbk" : ".tbk");
                destinationFilePath = Path.Combine(folderPath, destinationFileName);
            }

            try
            {
                if (File.Exists(destinationFilePath))
                {
                    // Unlikely
                    File.Delete(destinationFilePath);
                }
                File.Copy(sourceFilePath, destinationFilePath, true);
            }
            catch
            {
                // Old code: We just don't create the backup now. While we previously threw an exception, we now test and warn the user earlier on in the code that a backup can't be made 
                // System.Diagnostics.Debug.Print("Did not back up" + destinationFilePath);
                // throw new PathTooLongException("Backup failure: Could not create backups as the file path is too long", e);
                return string.Empty;
            }
            return destinationFilePath;
        }

        public static void TryRestoreBackup(string originalFilePath, string backupFilePath)
        {
            if (File.Exists(originalFilePath))
            {
                File.Delete(originalFilePath);
            }
            System.IO.File.Move(backupFilePath, originalFilePath);
        }
        #endregion
    }
}
