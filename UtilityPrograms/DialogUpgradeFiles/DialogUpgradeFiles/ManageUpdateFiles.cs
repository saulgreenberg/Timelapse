using DialogUpgradeFiles.Enums;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace DialogUpgradeFiles
{
    public partial class DialogUpgradeFilesAndFolders : Window
    {

        #region Upgrade the files
        // The list of files to upgrade should only include .tdb and .ddb files that require updating
        private async Task<UpgradeResultsEnum> UpgradeFiles(List<string> files, string timelapseVersion)
        {
            this.ListBoxResultsStatus.Items.Clear();
            if (files.Count == 0)
            {
                // Feedback for this is provided in the invoking method
                return UpgradeResultsEnum.NoFilesFound;
            }

            // Get the common directory path between all files, which we will use to trim off the file path (for feedback)
            // Then:
            // - populate a dictionary for displaying all found files and their current process status
            // - display the initial list of files
            List<string> paths = new List<string>();
            foreach (string file in files)
            {
                paths.Add(Path.GetDirectoryName(file));
            }
            string commonPath = this.GetCommonPrefix(paths);

            foreach (string file in files.OrderBy(q => q).ToList())
            {
                this.ShortFileName = String.IsNullOrWhiteSpace(commonPath)
                    ? file
                    : file.Replace(commonPath, String.Empty).TrimStart(Path.DirectorySeparatorChar);
                DictFileUpdateStatus.Add(this.ShortFileName, "Waiting:          ");
            }
            this.RefreshFileUpdateStatusDisplay(DictFileUpdateStatus, 0);

            // Begin processing each file, where we try to update it.
            int i = 0;
            int failed = 0;
            int upgraded = 0;
            int cancelled = 0;
            foreach (string file in files.OrderBy(q => q).ToList())
            {
                string backupFilePath = String.Empty;

                // Feedback
                this.LineFeedback(String.Format("Processing {0} of {1} files", i + 1, files.Count));
                await Task.Delay(Constant.BusyState.SleepTime);

                // Extract the short file name
                this.ShortFileName = String.IsNullOrWhiteSpace(commonPath)
                        ? file
                        : file.Replace(commonPath, String.Empty).TrimStart(Path.DirectorySeparatorChar);
                string shortFilePathWithoutExtension = Path.Combine(Path.GetDirectoryName(this.ShortFileName), Path.GetFileNameWithoutExtension(ShortFileName));

                // Check if this file has been cancelled before continuing
                if (this.CancelUpgrade)
                {
                    this.DictFileUpdateStatus[this.ShortFileName] = "Cancelled:      ";
                    this.RefreshFileUpdateStatusDisplay(DictFileUpdateStatus, -1);
                    cancelled++;
                    continue;
                }

                // Status: Process the file...
                DictFileUpdateStatus[this.ShortFileName] = "Processing:     ";
                this.RefreshFileUpdateStatusDisplay(DictFileUpdateStatus, i);

                // Try to upgrade the file
                UpgradeResultsEnum result = UpgradeResultsEnum.Failed;
                try
                {
                    // Animation Feedback 
                    this.AnimateProgressTimer.Start();

                    // Copy it to the backups folder
                    backupFilePath = Util.FileBackup.TryCreateBackup(Path.GetDirectoryName(file), Path.GetFileName(file));

                    // Attempt the upgrade
                    result = String.IsNullOrEmpty(backupFilePath)
                        ? UpgradeResultsEnum.NoBackupMade
                        : await this.UDBUpgradeTemplatesInDatabaseFilesAsync(file, this.IsDeleteImageQualityRequested, timelapseVersion).ConfigureAwait(true);

                    this.AnimateProgressTimer.Stop();

                    // Feedback gased on result
                    this.ProgressCharacter = String.Empty;
                    switch (result)
                    {
                        case UpgradeResultsEnum.Upgraded:
                            this.DictFileUpdateStatus[this.ShortFileName] = "Upgraded";
                            upgraded++;
                            break;
                        case UpgradeResultsEnum.Failed:
                            this.DictFileUpdateStatus[this.ShortFileName] = "Failed:             ";
                            failed++;
                            break;
                        case UpgradeResultsEnum.NoBackupMade:
                            this.DictFileUpdateStatus[this.ShortFileName] = "Failed (could not make backup):";
                            break;
                        case UpgradeResultsEnum.FileNotFound:
                            this.DictFileUpdateStatus[this.ShortFileName] = "Failed (file not found):";
                            break;
                        case UpgradeResultsEnum.InvalidFile:
                            this.DictFileUpdateStatus[this.ShortFileName] = "Failed (invalid file):";
                            break;
                        case UpgradeResultsEnum.Cancelled:
                        default:
                            this.DictFileUpdateStatus[this.ShortFileName] = "Cancelled:      ";
                            failed++;
                            break;
                    }
                }
                catch
                {
                    this.DictFileUpdateStatus[this.ShortFileName] = "Failed:             ";
                    failed++;
                }

                // If we failed, restore the original file from the backup file
                if (result != UpgradeResultsEnum.Upgraded)
                {
                    Util.FileBackup.TryRestoreBackup(file, backupFilePath);
                }
                // Uncomment this to debug the dialog, where the original file is just copied back over the updated file
                //else
                //{
                //    Util.FileBackup.TryRestoreBackup(file, backupFilePath);
                //}
                this.RefreshFileUpdateStatusDisplay(DictFileUpdateStatus, i++);
                this.TitleMessage.Text = String.Format("Upgrading {0} files", files.Count - i);
            }

            // Done!
            this.LineFeedback(String.Format("Finished. {0}/{3} files upgraded successfully, {1} failed, {2} cancelled.", upgraded, failed, cancelled, files.Count));
            this.CancelUpgrade = false;
            return UpgradeResultsEnum.FilesFound;
        }
        #endregion

        #region Collect Files
        // Collect all the .ddb and .tdb files found in the filesFolder list
        private static async Task<List<string>> CollectFiles(string[] filesFolders)
        {
            List<string> foundFiles = new List<string>();
            List<string> foundNonUpdatedFiles = new List<string>();

            await Task.Run(() =>
            {
                foreach (string fileFolder in filesFolders)
                {
                    FileAttributes attr = File.GetAttributes(fileFolder);
                    if (attr.HasFlag(FileAttributes.Directory))
                    {
                        // Its a folder: Add all the ddb / tdb files in this folder and its sub-folders
                        Util.FilesFolders.GetAllDDBandTDBFilesInFoldersAndSubfolders(fileFolder, foundFiles);
                    }
                    else
                    {
                        if (Path.GetExtension(fileFolder) == ".tdb" || (Path.GetExtension(fileFolder) == ".ddb"))
                        {
                            foundFiles.Add(fileFolder);
                        }
                    }
                }

                foreach (string foundFile in foundFiles)
                {
                    Database.SQLiteWrapper SQLiteWrapper = new Database.SQLiteWrapper(foundFile);
                    if (SQLiteWrapper.PragmaGetQuickCheck() == false)
                    {
                        // Skip over bad files, so those aren't reported.
                        continue;
                    }
                    if (Path.GetExtension(foundFile) == ".tdb")
                    {
                        // Check to see if this file needs upgrading by seeing if the TemplateInfo table is presence (introduced in 2.3.0.0)
                        // However, we also need to check if the UtcOffsetLabel exists in it too, as this is a special case
                        // where an updated template was opened with a pre2.3 timelapse version, it could be corrupted by the addition of a UtcOffsetLabel
                        if (SQLiteWrapper.TableExists(Constant.DBTables.TemplateInfo))
                        {
                            string typeQuery = Sql.Select + Constant.Control.Type + Sql.From + Constant.DBTables.Template + Sql.Where + Constant.Control.Type + Sql.Equal + Sql.Quote(Constant.DatabaseColumn.UtcOffset);
                            DataTable table = SQLiteWrapper.GetDataTableFromSelect(typeQuery);
                            if (table.Rows.Count == 0)
                            {
                                // There is no UtcOffset in this table, so this is a valid 2.3+ file
                                continue;
                            }
                        }
                    }
                    else if (Path.GetExtension(foundFile) == ".ddb")
                    {
                        // Check to see if it needs upgrading by the presence of the VersionCompatability column and examining the version number
                        if (false == SQLiteWrapper.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.VersionCompatabily))
                        {
                            continue;
                        }
                        List<object> version = SQLiteWrapper.GetDistinctValuesInColumn(Constant.DBTables.ImageSet, Constant.DatabaseColumn.VersionCompatabily);
                        if (version.Count == 1 && Util.VersionChecks.IsVersion1GreaterOrEqualToVersion2((string)version[0], "2.3.0.0"))
                        {
                            // Special case: Upgraded file, but with a UTCOffset Column
                            if (false == SQLiteWrapper.SchemaIsColumnInTable(Constant.DBTables.FileData, Constant.DatabaseColumn.UtcOffset))
                            {
                                continue;
                            }
                        }
                    }
                    foundNonUpdatedFiles.Add(foundFile);
                }
            });
            return foundNonUpdatedFiles;
        }
        #endregion

        #region Helpers
        string GetCommonPrefix(string first, string second)
        {
            int prefixLength = 0;
            for (int i = 0; i < Math.Min(first.Length, second.Length); i++)
            {
                if (first[i] != second[i])
                    break;
                prefixLength++;
            }
            return first.Substring(0, prefixLength);
        }

        string GetCommonPrefix(IEnumerable<string> strings)
        {
            return strings.Count() <= 1
            ? String.Empty
            : strings.Aggregate(GetCommonPrefix);
        }

        private void SetPreviousFolderPath(string chosenFileOrFolder)
        {
            FileAttributes attr = File.GetAttributes(chosenFileOrFolder);
            this.previousFolderPath = attr.HasFlag(FileAttributes.Directory)
                ? chosenFileOrFolder
                : Path.GetDirectoryName(chosenFileOrFolder);
        }
        #endregion
    }
}
