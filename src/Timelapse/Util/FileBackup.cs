using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using File = Timelapse.Constant.File;

namespace Timelapse.Util
{
    /// <summary>
    /// Make a time-stamped backup of the given file in the backup folder.
    /// At the same time, limit the number of backup files, where we prune older files with the same extension as needed. 
    /// </summary>
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
                    backupFiles = backupFiles.Where(x => x.Name.Contains(File.BackupCheckpointIndicator) == false);
                }
                return backupFiles;
            }
            catch (Exception ex)
            {
                AppLog.Warning("This is a non-critical warning, where creating a backup file was skipped. Failed to enumerate backup files. Possible causes: permissions error, or folder disappeared between creation and use.", ex);
                return null;
            }
        }

        public static DateTime GetMostRecentBackup(string sourceFilePath)
        {
            try
            {
                DirectoryInfo backupFolder = GetOrCreateBackupFolder(sourceFilePath);
                FileInfo mostRecentBackupFile = null;
                if (backupFolder != null)
                {
                    mostRecentBackupFile = GetBackupFiles(backupFolder, sourceFilePath, false)?.MaxBy(file => file.LastWriteTime);
                }   
                if (backupFolder != null && mostRecentBackupFile != null)
                {
                    return mostRecentBackupFile.LastWriteTime;
                }
                return DateTime.MinValue;
            }
            catch (Exception ex)
            {
                AppLog.Warning("This is a non-critical warning, where retrieving the most recent backup was skipped. Possible causes: permissions error or path too long.", ex);
                return DateTime.MinValue;
            }
        }

        public static DirectoryInfo GetOrCreateBackupFolder(string sourceFilePath)
        {
            if (IsCondition.IsPathLengthTooLong(sourceFilePath, FilePathTypeEnum.Backup))
            {
                AppLog.Warning("This is a non-critical warning, where creating a backup file was skipped. File path is approaching the critical max length, so the backup folder will not be created.");
                return null;
            }

            string sourceFolderPath = Path.GetDirectoryName(sourceFilePath);
            if (sourceFolderPath == null)
            {
                // If the path consists of a root directory, such as "c:\", null is returned.
                AppLog.Warning("This is a non-critical warning, where creating a backup file was skipped. Could not determine the source folder path — this should not happen, as it only occurs if the path is a root directory such as 'c:\\'.");
                return null;
            }

            DirectoryInfo backupFolder = new(Path.Combine(sourceFolderPath, File.BackupFolder));   // The Backup Folder 
            if (backupFolder.Exists == false)
            {
                try
                {
                    backupFolder.Create();
                }
                catch (Exception ex)
                {
                    AppLog.Warning("This is a non-critical warning, where creating a backup file was skipped. Failed to create backup folder. Possible cause: permissions error on the parent directory.", ex);
                    return null;
                }
            }
            return backupFolder;
        }
        #endregion

        #region Public Static Methods -TryCreateBackup, various versions

        // Copy to backup version with full path to source file
        public static bool TryCreateBackup(string sourceFilePath)
        {
            return TryCreateBackup(Path.GetDirectoryName(sourceFilePath), Path.GetFileName(sourceFilePath), false);
        }

        // Copy or move file to backup version with full path to source file
        // ReSharper disable once UnusedMember.Global
        public static bool TryCreateBackup(string sourceFilePath, bool moveInsteadOfCopy)
        {
            return TryCreateBackup(Path.GetDirectoryName(sourceFilePath), Path.GetFileName(sourceFilePath), moveInsteadOfCopy);
        }

        // Copy or move file to backup version with separated path/source file name
        public static bool TryCreateBackup(string folderPath, string sourceFileName)
        {
            return TryCreateBackup(folderPath, sourceFileName, false);
        }

        // Creates a standard backup file
        public static bool TryCreateBackup(string folderPath, string sourceFileName, bool moveInsteadOfCopy)
        {
            return TryCreateBackup(folderPath, sourceFileName, moveInsteadOfCopy, string.Empty);
        }

        // Full version: Copy or move file to backup version with separated path/source file name
        // If specialBackup is non-empty, it creates a special checkpointfile, usually to flag a non=-backwards compatable upgrade to the tdb and ddb files
        public static bool TryCreateBackup(string folderPath, string sourceFileName, bool moveInsteadOfCopy, string specialBackup)
        {
            string sourceFilePath = Path.Combine(folderPath, sourceFileName);
            if (System.IO.File.Exists(sourceFilePath) == false)
            {
                // nothing to do
                return false;
            }

            // create backup folder if needed
            DirectoryInfo backupFolder = GetOrCreateBackupFolder(sourceFilePath);
            if (backupFolder == null)
            {
                // Something went wrong...
                return false;
            }
            // create a timestamped copy of the file
            // file names can't contain colons so use non-standard format for timestamp with dashes for 24 hour-minute-second separation
            // If there is a specialBackup term, then we modify anadd it before the timestamp
            string sourceFileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourceFileName);
            string sourceFileExtension = Path.GetExtension(sourceFileName);
            specialBackup = specialBackup == string.Empty
                ? string.Empty
                : File.BackupCheckpointIndicator + specialBackup;
            string destinationFileName = String.Concat(sourceFileNameWithoutExtension, specialBackup, ".", DateTime.Now.ToString("yyyy-MM-dd.HH-mm-ss"), sourceFileExtension);
            string destinationFilePath = Path.Combine(backupFolder.FullName, destinationFileName);

            try
            {
                if (System.IO.File.Exists(destinationFilePath) && new FileInfo(destinationFilePath).Attributes.HasFlag(FileAttributes.ReadOnly))
                {
                    AppLog.Warning("This is a non-critical warning, where creating a backup file was skipped. The destination backup file is read-only and cannot be overwritten.");
                    return false;
                }
                if (moveInsteadOfCopy)
                {

                    FilesFolders.TryMoveFileIfExists(sourceFilePath, destinationFilePath);
                }
                else
                {
                    System.IO.File.Copy(sourceFilePath, destinationFilePath, true);
                }
            }
            catch (Exception ex)
            {
                // Old code: We just don't create the backup now. While we previously threw an exception, we now test and warn the user earlier on in the code that a backup can't be made
                AppLog.Warning("This is a non-critical warning, where creating a backup file was skipped. Failed to copy or move the source file to the backup destination. Possible cause: path too long, or permissions error.", ex);
                return false;
            }

            // age out older backup files (this skips the special checkpoint files)
            IEnumerable<FileInfo> backupFiles = GetBackupFiles(backupFolder, sourceFilePath, true).OrderByDescending(file => file.LastWriteTimeUtc);

            // ReSharper disable All
            // Resharper says this is heuristically unreachable, but that isn't true
            if (backupFiles == null)
            {
                // We can't delete older backups, but at least we were able to create a backup.
                return true;
            }
            // ReSharper restore All
            foreach (FileInfo file in backupFiles.Skip(File.NumberOfBackupFilesToKeep))
            {
                // Don't remove newer backups
                int days = (DateTime.Now - file.CreationTime).Days;
                if (days > 14)
                {
                    FilesFolders.TryDeleteFileIfExists(file.FullName);
                }
            }
            return true;
        }
        #endregion
    }
}