using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Timelapse.Database
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

        // Copy to backup version with with full path to source file
        public static bool TryCreateBackup(string sourceFilePath)
        {
            return FileBackup.TryCreateBackup(Path.GetDirectoryName(sourceFilePath), Path.GetFileName(sourceFilePath), false);
        }

        // Copy or move file to backup version with full path to source file
        public static bool TryCreateBackup(string sourceFilePath, bool moveInsteadOfCopy)
        {
            return FileBackup.TryCreateBackup(Path.GetDirectoryName(sourceFilePath), Path.GetFileName(sourceFilePath), moveInsteadOfCopy);
        }

        // Copy or move file to backup version with separated path/source file name
        public static bool TryCreateBackup(string folderPath, string sourceFileName)
        {
            return FileBackup.TryCreateBackup(folderPath, sourceFileName, false);
        }

        // Creates a standard backup file
        public static bool TryCreateBackup(string folderPath, string sourceFileName, bool moveInsteadOfCopy)
        {
            return TryCreateBackup(folderPath, sourceFileName, moveInsteadOfCopy, String.Empty);
        }

        // Full version: Copy or move file to backup version with separated path/source file name
        // If specialBackup is non-empty, it creates a special checkpointfile, usually to flag a non=-backwards compatable upgrade to the tdb and ddb files
        public static bool TryCreateBackup(string folderPath, string sourceFileName, bool moveInsteadOfCopy, string specialBackup)
        {
            string sourceFilePath = Path.Combine(folderPath, sourceFileName);
            if (File.Exists(sourceFilePath) == false)
            {
                // nothing to do
                return false;
            }

            // create backup folder if needed
            DirectoryInfo backupFolder = FileBackup.GetOrCreateBackupFolder(sourceFilePath);
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
            specialBackup = specialBackup == String.Empty
                ? String.Empty
                : Constant.File.BackupCheckpointIndicator + specialBackup;
            string destinationFileName = String.Concat(sourceFileNameWithoutExtension, specialBackup, ".", DateTime.Now.ToString("yyyy-MM-dd.HH-mm-ss"), sourceFileExtension);
            string destinationFilePath = Path.Combine(backupFolder.FullName, destinationFileName);

            try
            {
                if (File.Exists(destinationFilePath) && new System.IO.FileInfo(destinationFilePath).Attributes.HasFlag(System.IO.FileAttributes.ReadOnly))
                {
                    // Can't overwrite it...
                    return false;
                }
                if (moveInsteadOfCopy)
                {

                    File.Move(sourceFilePath, destinationFilePath);
                }
                else
                {
                    File.Copy(sourceFilePath, destinationFilePath, true);
                }
            }
            catch
            {
                // Old code: We just don't create the backup now. While we previously threw an exception, we now test and warn the user earlier on in the code that a backup can't be made 
                // System.Diagnostics.Debug.Print("Did not back up" + destinationFilePath);
                // throw new PathTooLongException("Backup failure: Could not create backups as the file path is too long", e);
                return false;
            }

            // age out older backup files (this skips the special checkpoint files)
            IEnumerable<FileInfo> backupFiles = FileBackup.GetBackupFiles(backupFolder, sourceFilePath, true).OrderByDescending(file => file.LastWriteTimeUtc);
            if (backupFiles == null)
            {
                // We can't delete older backups, but at least we were able to create a backup.
                return true;
            }
            foreach (FileInfo file in backupFiles.Skip(Constant.File.NumberOfBackupFilesToKeep))
            {
                File.Delete(file.FullName);
            }
            return true;
        }
        #endregion
    }
}