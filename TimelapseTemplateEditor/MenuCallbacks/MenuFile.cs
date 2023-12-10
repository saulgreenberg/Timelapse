using System.IO;
using System.Windows;
using System.Windows.Controls;
using DialogUpgradeFiles;
using Microsoft.Win32;
using Timelapse.Database;
using Timelapse.Dialog;
using Constant = Timelapse.Constant;
using Enums=Timelapse.Enums;
using Timelapse.Util;
using TimelapseTemplateEditor.Dialog;

namespace TimelapseTemplateEditor
{
    public partial class TemplateEditorWindow 
    {

        // Creates a new database file of a user chosen name in a user chosen location.
        private async void MenuFileNewTemplate_Click(object sender, RoutedEventArgs e)
        {
            bool makeBackup = true;
            this.TemplateDoApplyPendingEdits();

            // Configure save file dialog box
            SaveFileDialog newTemplateFilePathDialog = new SaveFileDialog
            {
                FileName = Path.GetFileNameWithoutExtension(Constant.File.DefaultTemplateDatabaseFileName), // Default file name without the extension
                DefaultExt = Constant.File.TemplateDatabaseFileExtension, // Default file extension
                Filter = "Database Files (" + Constant.File.TemplateDatabaseFileExtension + ")|*" + Constant.File.TemplateDatabaseFileExtension, // Filter files by extension 
                AddExtension = true,
                Title = "Select Location to Save New Template File"
            };

            // Show save file dialog box
            bool? result = newTemplateFilePathDialog.ShowDialog();

            // Process save file dialog box results 
            if (result == true)
            {
                string templateFileName = newTemplateFilePathDialog.FileName;

                // Ensure that the filename has a .tdb extension by replacing whatever extension is there with the desired extension.
                templateFileName = Path.ChangeExtension(templateFileName, Constant.File.TemplateDatabaseFileExtension.Substring(1));

                // Now try to create or open the template database
                // First, check the file path length and notify the user the template couldn't be loaded because its path is too long 
                // Note: The SaveFileDialog doesn't do the right thing when the user specifies a really long file name / path (it just returns the DefaultTemplateDatabaseFileName without a path), 
                // so we test for that too as it also indicates a too longpath name
                if (IsCondition.IsPathLengthTooLong(templateFileName, Timelapse.Enums.FilePathTypeEnum.TDB) || templateFileName.Equals(Path.GetFileNameWithoutExtension(Constant.File.DefaultTemplateDatabaseFileName)))
                {
                    Dialogs.TemplatePathTooLongDialog(this, templateFileName);
                    return;
                }
                if (IsCondition.IsPathLengthTooLong(templateFileName, Timelapse.Enums.FilePathTypeEnum.Backup))
                {
                    Dialogs.BackupPathTooLongDialog(this);
                    makeBackup = false;
                }

                // Overwrite the file if it exists
                if (File.Exists(templateFileName) && makeBackup)
                {
                    FileBackup.TryCreateBackup(templateFileName);
                    File.Delete(templateFileName);
                }

                // Open document 
                await this.TemplateInitializeFromDBFileAsync(templateFileName).ConfigureAwait(true);
                this.TemplateUI.HelpMessageInitial.Visibility = Visibility.Collapsed;
                this.MenuFileClose.IsEnabled = true;
            }
        }

        // Open an existing database file.
        private async void MenuFileOpenTemplate_Click(object sender, RoutedEventArgs e)
        {
            this.TemplateDoApplyPendingEdits();

            // Note that if we try to open a file with a too long path, the open file dialog will just say that it doesn't exist (which is a bad error message, but nothing we can do about it)
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                FileName = Path.GetFileNameWithoutExtension(Constant.File.DefaultTemplateDatabaseFileName), // Default file name without the extension
                DefaultExt = Constant.File.TemplateDatabaseFileExtension, // Default file extension
                Filter = "Database Files (" + Constant.File.TemplateDatabaseFileExtension + ")|*" + Constant.File.TemplateDatabaseFileExtension, // Filter files by extension 
                Title = "Select an Existing Template File to Open"
            };

            // Show open file dialog box
            bool? result = openFileDialog.ShowDialog();

            // Process open file dialog box results 
            if (result == true)
            {
                // If its not a valid template, display a dialog and abort
                if (false == Dialogs.DialogIsFileValid(this, openFileDialog.FileName))
                {
                    return;
                }

                if (IsCondition.IsPathLengthTooLong(openFileDialog.FileName, Enums.FilePathTypeEnum.Backup))
                {
                    Dialogs.BackupPathTooLongDialog(this);
                }

                // But check to see if we are opening it up with an older version of TImelapse vs. the previous version that opened it
                // If so, generate a warning
                if (this.userSettings.SuppressOpeningWithOlderTimelapseVersionDialog == false)
                {
                    SQLiteWrapper db = new SQLiteWrapper(openFileDialog.FileName);
                    if (db.TableExists(Constant.DBTables.TemplateInfo))
                    {
                        string thisVersion = TemplateGetVersionCompatability(db);
                        string timelapseCurrentVersionNumber = VersionChecks.GetTimelapseCurrentVersionNumber().ToString();
                        if (VersionChecks.IsVersion1GreaterThanVersion2(thisVersion, timelapseCurrentVersionNumber))
                        {
                            if (true != EditorDialogs.EditorDatabaseFileOpenedWithOlderVersionOfTimelapse(this, this.userSettings))
                            {
                                // The user aborted loading the template
                                return;
                            }
                        }
                    }
                }
                await this.TemplateDoOpen(openFileDialog.FileName);
            }
        }

        // Open a recently used template
        private async void MenuItemRecentTemplate_Click(object sender, RoutedEventArgs e)
        {
            string recentTemplatePath = (string)((MenuItem)sender).ToolTip;

            if (File.Exists(recentTemplatePath) == false)
            {
                EditorDialogs.EditorTemplateFileNoLongerExistsDialog(this, Path.GetFileName(recentTemplatePath));
                return;
            }
            // If its not a valid template, display a dialog and abort
            if (false == Dialogs.DialogIsFileValid(this, recentTemplatePath))
            {
                return;
            }
            await TemplateDoOpen(recentTemplatePath);
        }

        private void MenuItemUpgradeTimelapseFiles_Click(object sender, RoutedEventArgs e)
        {
            DialogUpgradeFilesAndFolders dialogUpdateFiles = new DialogUpgradeFilesAndFolders(this, string.Empty, VersionChecks.GetTimelapseCurrentVersionNumber().ToString());
            dialogUpdateFiles.ShowDialog();
        }

        // Closes the template and clears various states to allow another template to be created or opened.
        private void MenuFileClose_Click(object sender, RoutedEventArgs e)
        {
            this.TemplateDoClose();
        }

        /// <summary>
        /// Exits the application.
        /// </summary>
        private void MenuFileExit_Click(object sender, RoutedEventArgs e)
        {
            // Note that Window_Closing, which does some cleanup, will be invoked as a side effect
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Update the list of recent databases (ensuring they still exist) displayed under File -> Recent Databases.
        /// </summary>
        private void MenuFileRecentTemplates_Refresh(bool enable)
        {
            this.MenuFileRecentTemplates.IsEnabled = enable && this.userSettings.MostRecentTemplates.Count > 0;
            this.MenuFileRecentTemplates.Items.Clear();

            int index = 1;
            foreach (string recentTemplatePath in this.userSettings.MostRecentTemplates)
            {
                if (File.Exists(recentTemplatePath) && Path.GetExtension(recentTemplatePath) == Timelapse.Constant.File.TemplateDatabaseFileExtension)
                {
                    MenuItem recentImageSetItem = new MenuItem();
                    recentImageSetItem.Click += this.MenuItemRecentTemplate_Click;
                    recentImageSetItem.Header = $"_{index} {recentTemplatePath}";
                    recentImageSetItem.ToolTip = recentTemplatePath;
                    this.MenuFileRecentTemplates.Items.Add(recentImageSetItem);
                    ++index;
                }
            }
        }
    }
}
