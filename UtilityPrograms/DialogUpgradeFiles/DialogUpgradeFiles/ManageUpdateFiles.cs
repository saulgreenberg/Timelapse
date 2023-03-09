using DialogUpgradeFiles.Enums;
using DialogUpgradeFiles.Util;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DialogUpgradeFiles
{
    public partial class DialogUpgradeFilesAndFolders
    {

        #region Upgrade the files
        // The list of filesDictionary to upgrade should only include .tdb and .ddb filesDictionary that require updating
        private async Task<UpgradeResultsEnum> UpgradeFiles(Dictionary<string, UpgradeResultsEnum> filesDictionary, string timelapseVersion)
        {
            this.ListBoxResultsStatus.Items.Clear();
            if (filesDictionary.Count == 0)
            {
                // Feedback for this is provided in the invoking method
                return UpgradeResultsEnum.NoFilesFound;
            }

            // Get the common directory path between all filesDictionary, which we will use to trim off the file path (for feedback)
            // Then:
            // - populate a dictionary for displaying all found filesDictionary and their current process status
            // - display the initial list of filesDictionary
            List<string> paths = new List<string>();
            foreach (string file in filesDictionary.Keys)
            {
                paths.Add(Path.GetDirectoryName(file));
            }
            string commonPath = this.GetCommonPrefix(paths);

            foreach (string file in filesDictionary.Keys.OrderBy(q => q).ToList())
            {
                this.ShortFileName = string.IsNullOrWhiteSpace(commonPath)
                    ? file
                    : file.Replace(commonPath, string.Empty).TrimStart(Path.DirectorySeparatorChar);
                DictFileUpdateStatus.Add(this.ShortFileName, "Waiting:          ");
            }
            this.RefreshFileUpdateStatusDisplay(DictFileUpdateStatus, 0);

            // Begin processing each file, where we try to update it.
            int i = 0;
            int failed = 0;
            int upgraded = 0;
            int cancelled = 0;
            bool pathTooLongFilesDetected = false;
            foreach (KeyValuePair<string, UpgradeResultsEnum> filekvp in filesDictionary.OrderBy(q => q.Key))
            {
                string file = filekvp.Key;
                string backupFilePath = string.Empty;

                // Feedback
                this.LineFeedback($"Processing {i + 1} of {filesDictionary.Count} files");
                await Task.Delay(Constant.BusyState.SleepTime);

                // Extract the short file name
                this.ShortFileName = string.IsNullOrWhiteSpace(commonPath)
                        ? file
                        : file.Replace(commonPath, string.Empty).TrimStart(Path.DirectorySeparatorChar);
                // ReSharper disable once UnusedVariable
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

                    //if (IsCondition.IsPathLengthTooLong(file, FilePathTypeEnum.DDB))
                    if (filekvp.Value == UpgradeResultsEnum.PathTooLong)
                    {
                        // Don't bother trying to upgrade it as the path of the tdb or ddb file is too long.
                        result = UpgradeResultsEnum.PathTooLong;
                        pathTooLongFilesDetected = true;
                    }
                    else if (filekvp.Value == UpgradeResultsEnum.InvalidFile)
                    {
                        // Don't bother trying to upgrade it as the path of the tdb or ddb file is too long.
                        result = UpgradeResultsEnum.InvalidFile;
                    }
                    else
                    {
                        // Copy it to the backups folder
                        backupFilePath = Util.FileBackup.TryCreateBackup(Path.GetDirectoryName(file), Path.GetFileName(file));

                        // Attempt the upgrade
                        result = await this.UDBUpgradeTemplatesInDatabaseFilesAsync(file, this.IsDeleteImageQualityRequested, timelapseVersion).ConfigureAwait(true);
                        if (result == UpgradeResultsEnum.Upgraded && backupFilePath.EndsWith("bk"))
                            result = UpgradeResultsEnum.AlternateBackupMade;
                    }
                    this.AnimateProgressTimer.Stop();

                    // Feedback based on result
                    this.ProgressCharacter = string.Empty;
                    switch (result)
                    {
                        case UpgradeResultsEnum.Upgraded:
                            this.DictFileUpdateStatus[this.ShortFileName] = "Upgraded";
                            upgraded++;
                            break;
                        case UpgradeResultsEnum.AlternateBackupMade:
                            this.DictFileUpdateStatus[this.ShortFileName] = "Upgraded";
                            upgraded++;
                            break;
                        case UpgradeResultsEnum.PathTooLong:
                            this.DictFileUpdateStatus[this.ShortFileName] = "Failed (path too long)";
                            failed++;
                            break;
                        case UpgradeResultsEnum.Failed:
                            this.DictFileUpdateStatus[this.ShortFileName] = "Failed";
                            failed++;
                            break;
                        case UpgradeResultsEnum.NoBackupMade:
                            this.DictFileUpdateStatus[this.ShortFileName] = "Failed (could not make backup)";
                            break;
                        case UpgradeResultsEnum.FileNotFound:
                            this.DictFileUpdateStatus[this.ShortFileName] = "Failed (file not found)";
                            break;
                        case UpgradeResultsEnum.InvalidFile:
                            this.DictFileUpdateStatus[this.ShortFileName] = "Failed (invalid file)";
                            break;
                        case UpgradeResultsEnum.Cancelled:
                        default:
                            this.DictFileUpdateStatus[this.ShortFileName] = "Cancelled";
                            failed++;
                            break;
                    }
                }
                catch
                {
                    this.DictFileUpdateStatus[this.ShortFileName] = "Failed";
                    failed++;
                }

                // If we failed, restore the original file from the backup file
                // unless no backup was made due to the path being too long
                if (result != UpgradeResultsEnum.Upgraded && result != UpgradeResultsEnum.PathTooLong && result != UpgradeResultsEnum.AlternateBackupMade && backupFilePath != string.Empty && File.Exists(backupFilePath))
                {
                    Util.FileBackup.TryRestoreBackup(file, backupFilePath);
                }
                // Uncomment this to debug the dialog, where the original file is just copied back over the updated file
                //else
                //{
                //    Util.FileBackup.TryRestoreBackup(file, backupFilePath);
                //}
                this.RefreshFileUpdateStatusDisplay(DictFileUpdateStatus, i++);
                this.TitleMessage.Text = $"Upgrading {filesDictionary.Count - i} files";
            }

            // Done!
            string finishedMessage = String.Format("Finished. {0}/{3} files upgraded successfully, {1} failed, {2} cancelled.", upgraded, failed, cancelled, filesDictionary.Count);
            if (pathTooLongFilesDetected)
            {
                finishedMessage += Environment.NewLine + Environment.NewLine + "Some upgrades failed as their file path length are near Windows' allowed maximum." + Environment.NewLine;
                finishedMessage += "\u2022 shorten the path by moving your image folder higher up the folder hierarchy";
            }
            this.LineFeedback(finishedMessage);
            this.CancelUpgrade = false;
            return UpgradeResultsEnum.FilesFound;
        }
        #endregion

        #region Collect Files
        // Collect all the .ddb and .tdb filesDictionary found in the filesFolder list
        private static async Task<Dictionary<string, UpgradeResultsEnum>> CollectFiles(string[] filesFolders)
        {
            Dictionary<string, UpgradeResultsEnum> foundFilesDictionary = new Dictionary<string, UpgradeResultsEnum>();
            List<string> pathsTooLongList = new List<string>();
            List<string> foundFilesList = new List<string>();
            // ReSharper disable once UnusedVariable
            List<string> foundNonUpdatedFilesList = new List<string>();
            await Task.Run(() =>
            {
                foreach (string fileFolder in filesFolders)
                {
                    try
                    {
                        FileAttributes attr = File.GetAttributes(fileFolder);
                        if (attr.HasFlag(FileAttributes.Directory))
                        {
                            // Its a folder: Add all the ddb / tdb filesDictionary in this folder and its sub-folders
                            Util.FilesFolders.GetAllDDBandTDBFilesInFoldersAndSubfolders(fileFolder, foundFilesList);
                        }
                        else
                        {
                            if (Path.GetExtension(fileFolder) == ".tdb" || (Path.GetExtension(fileFolder) == ".ddb"))
                            {
                                foundFilesList.Add(fileFolder);
                            }
                        }
                    }
                    catch (System.IO.PathTooLongException)
                    {
                        pathsTooLongList.Add(fileFolder);
                        // ReSharper disable once RedundantJumpStatement
                        continue;
                    }
                    catch
                    {
                        pathsTooLongList.Add(fileFolder);
                    }
                }

                foreach (string pathTooLong in pathsTooLongList)
                {
                    // Add the ones that drew an exception to the dictionary with a tag that says its too long
                    // Note that this may include folder paths, but I am unsure if that is actually the case
                    foundFilesDictionary.Add(pathTooLong, UpgradeResultsEnum.PathTooLong);
                }

                foreach (string foundFile in foundFilesList)
                {

                    if (IsCondition.IsPathLengthTooLong(foundFile, FilePathTypeEnum.DDB))
                    {
                        // Skip over filesDictionary that are too long, so those aren't reported.
                        if (false == foundFilesDictionary.ContainsKey(foundFile))
                        {
                            foundFilesDictionary.Add(foundFile, UpgradeResultsEnum.PathTooLong);
                        }
                        continue;
                    }

                    // At this point we should only have valid ddbs an tdbs
                    Database.SQLiteWrapper SQLiteWrapper = new Database.SQLiteWrapper(foundFile);
                    if (SQLiteWrapper.PragmaGetQuickCheck() == false)
                    {
                        // Tag bad filesDictionary, so those aren't reported.
                        foundFilesDictionary.Add(foundFile, UpgradeResultsEnum.InvalidFile);
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
                                // Valid tdb, but no UtcOffset, so this is already an updated 2.3+ file
                                // Thus there is no reason to add it anywhere
                                continue;
                            }
                        }
                    }
                    else if (Path.GetExtension(foundFile) == ".ddb")
                    {
                        // Check to see if it needs upgrading by the presence of the VersionCompatability column and examining the version number
                        if (false == SQLiteWrapper.SchemaIsColumnInTable(Constant.DBTables.ImageSet, Constant.DatabaseColumn.VersionCompatabily))
                        {
                            // Tag it for an upgrade it as its missing a version compatability file
                            foundFilesDictionary.Add(foundFile, UpgradeResultsEnum.Pre23);
                            continue;
                        }
                        List<object> version = SQLiteWrapper.GetDistinctValuesInColumn(Constant.DBTables.ImageSet, Constant.DatabaseColumn.VersionCompatabily);
                        if (version.Count == 1 && Util.VersionChecks.IsVersion1GreaterOrEqualToVersion2((string)version[0], "2.3.0.0"))
                        {
                            // Special case: Upgraded file, but with a UTCOffset Column so we still have to upgrade it.
                            if (false == SQLiteWrapper.SchemaIsColumnInTable(Constant.DBTables.FileData, Constant.DatabaseColumn.UtcOffset))
                            {
                                // foundFilesDictionary.Add(foundFile, UpgradeResultsEnum.Pre23);
                                // Skip this file, as it seems good
                                continue;
                            }
                        }
                        // else will fall through, which means it will be upgraded since the version number is less than 2.3.0.0
                    }
                    foundFilesDictionary.Add(foundFile, UpgradeResultsEnum.Pre23);
                }
            });
            return foundFilesDictionary;
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
            // ReSharper disable once PossibleMultipleEnumeration
            return strings.Count() <= 1
            ? string.Empty
            // ReSharper disable once PossibleMultipleEnumeration
            : strings.Aggregate(GetCommonPrefix);
        }

        private void SetPreviousFolderPath(string chosenFileOrFolder)
        {
            // Its in a try/catch as GetAttributes fails on long paths 
            try
            {
                FileAttributes attr = File.GetAttributes(chosenFileOrFolder);
                this.previousFolderPath = attr.HasFlag(FileAttributes.Directory)
                    ? chosenFileOrFolder
                    : Path.GetDirectoryName(chosenFileOrFolder);
            }
            catch
            {
                Debug.Print("In SetPreviousFolderPath Catch");
            }
        }
        #endregion
    }
}
