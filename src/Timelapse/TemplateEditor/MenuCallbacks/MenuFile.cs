using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Dialog;
using Timelapse.Util;
using TimelapseTemplateEditor.EditorCode;
using TimelapseTemplateEditor.Standards;
using File = Timelapse.Constant.File;
using FilePathTypeEnum = Timelapse.Enums.FilePathTypeEnum;

namespace TimelapseTemplateEditor
{
    public partial class TemplateEditorWindow
    {
        #region MenuTestSomeCode_Click
        private void MenuTestSomeCode_Click(object sender, RoutedEventArgs e)
        {
        }
        #endregion
        private void FileMenu_OnSubmenuOpened(object sender, RoutedEventArgs e)
        {
            //MenuSwitchToTimelapse.IsEnabled = this.templateDatabase == null;

            // repopulate the recently opened templates list
            CreateMenuItemsForMenuFileRecentTemplates(this.templateDatabase == null);
        }

        #region  MenuFileNewTemplate
        // Creates a new database file of a user chosen name in a user chosen location.
        private async void MenuFileNewTemplate_Click(object sender, RoutedEventArgs e)
        {
            await CreateNewTemplateFile();
            this.TemplateUI.RowControls.IsEnabled = true;
        }
        #endregion

        #region MenuFileNewFromStandardResourceFile
        // Create a template based on a standard by copying that standard's template from a resource file
        private void MenuFileNewFromStandardResourceFile_Click(object sender, RoutedEventArgs e)
        {
            string rtfPath = string.Empty;
            string templatePath = string.Empty;
            string title = string.Empty;
            string what = string.Empty;
            if (sender is MenuItem menuItem)
            {
                switch (menuItem.Tag)
                {
                    case EditorConstant.Standards.AlbertaMetadataStandards:
                        rtfPath = EditorConstant.Standards.AlbertaMetadataStandardsOverview;
                        templatePath = EditorConstant.Standards.AlbertaMetadataStandardsTemplate;
                        title = EditorConstant.Standards.AlbertaMetadataStandardsTitle;
                        what = "Alberta Metadata Standard";
                        break;
                    case EditorConstant.Standards.CamtrapDP:
                        rtfPath = EditorConstant.Standards.CamtrapDPOverview;
                        templatePath = EditorConstant.Standards.CamtrapDPTemplate;
                        title = EditorConstant.Standards.CamtrapDPTitle;
                        what = "CamtrapDP Standard";
                        break;
                    default:
                        return;
                }
            }

            what = $"Create a template based on the [e]{what}[/e].[br][b]Important[/b]: The image set's sub-folder structure must match the hierarchy described in this standard, as detailed below.";
            ShowRtfFile show = new(this, $"{title} and Timelapse",
                what,
                rtfPath);
            if (false == show.ShowDialog())
            {
                return;
            }
            CreateNewTemplateFileFromResource(templatePath);
            this.TemplateUI.RowControls.IsEnabled = true;
        }
        #endregion

        #region MenuFileNewFromStandard - Programatically, various standards
        // Create an alberta metadata standard template programmatically
        private async void MenuFileNewAlbertaMetadataStandardProgramatically_Click(object sender, RoutedEventArgs e)
        {
            string rtfPath = EditorConstant.Standards.AlbertaMetadataStandardsOverview;
            ShowRtfFile show = new(this, "Alberta Metadata Standard and Timelapse",
                "Important: The image set's sub-folder structure must match the hierarchy described in this standard",
                rtfPath);
            if (false == show.ShowDialog())
            {
                return;
            }
            await CreateNewTemplateFile();
            DoCreateMetadataStandardFields(AlbertaMetadataStandard.FolderMetadataRows, AlbertaMetadataStandard.ImageTemplateRows, AlbertaMetadataStandard.Aliases);

            // Set the standard being used, if any. This avoids excessive calls to the database
            templateDatabase.SetTemplateStandard(AlbertaMetadataStandard.Standard);
            this.standardType = AlbertaMetadataStandard.Standard;

            this.TemplateUI.RowControls.IsEnabled = true;
            Globals.TemplateDataGridControl.DoLayoutUpdated(true);
        }

        // Create a camtrapDP template programmatically
        private async void MenuFileNewCameratrapDPProgramatically_Click(object sender, RoutedEventArgs e)
        {
            ShowRtfFile show = new(this, "The CamtrapDP Standard",
                "Important: The image set's sub-folder structure must match the hierarchy described in this standard",
                "pack://application:,,,/TemplateEditor/Resources/CamtrapDPOverview.rtf");
            if (false == show.ShowDialog())
            {
                return;
            }
            await CreateNewTemplateFile();
            DoCreateMetadataStandardFields(CamtrapDPStandard.FolderMetadataRows, CamtrapDPStandard.ImageTemplateRows, CamtrapDPStandard.Aliases);

            // Set the standard being used, if any. This avoids excessive calls to the database
            templateDatabase.SetTemplateStandard(CamtrapDPStandard.Standard);
            this.standardType = CamtrapDPStandard.Standard;

            this.TemplateUI.RowControls.IsEnabled = false;
            Globals.TemplateDataGridControl.DoLayoutUpdated(true);
        }

        // For testing: Create a template containing all the different control types programmatically
        private async void MenuFileNewAllControlsStandard_Click(object sender, RoutedEventArgs e)
        {
            await CreateNewTemplateFile();
            this.DoCreateMetadataStandardFields(AllControlsStandard.FolderMetadataRows, AllControlsStandard.ImageTemplateRows, AllControlsStandard.Aliases);
            this.TemplateUI.RowControls.IsEnabled = true;
            Globals.TemplateDataGridControl.DoLayoutUpdated(true);
        }


        // Create a practice image set template programmatically
        private async void MenuFileNewPracticeImageSetStandard_Click(object sender, RoutedEventArgs e)
        {
            ShowRtfFile show = new(this, "The Practice Image Set Tutorial Standard",
                "Important: The image set's sub-folder structure must match the hierarchy described in this standard",
                "pack://application:,,,/TemplateEditor/Resources/PracticeImageSetTutorialStandardOverview.rtf");
            if (false == show.ShowDialog())
            {
                return;
            }
            await CreateNewTemplateFile();
            DoCreateMetadataStandardFields(PracticeImageSetMetadataExample.FolderMetadataRows, PracticeImageSetMetadataExample.ImageTemplateRows, PracticeImageSetMetadataExample.Aliases);
            this.TemplateUI.RowControls.IsEnabled = true;
            Globals.TemplateDataGridControl.DoLayoutUpdated(true);
        }
        #endregion

        #region MenuFileOpenTemplate
        private void MenuFileOpenTemplate_Click(object sender, RoutedEventArgs e)
        {
            DataGrid dataGrid = TemplateUI.TemplateDataGridControl.DataGridInstance;
            DataGridCommonCode.ApplyPendingEdits(dataGrid);

            // Note that if we try to open a file with a too long path, the open file dialog will just say that it doesn't exist (which is a bad error message, but nothing we can do about it)
            OpenFileDialog openFileDialog = new()
            {
                FileName = Path.GetFileNameWithoutExtension(File.DefaultTemplateDatabaseFileName), // Default file name without the extension
                DefaultExt = File.TemplateDatabaseFileExtension, // Default file extension
                Filter = "Database Files (" + File.TemplateDatabaseFileExtension + ")|*" + File.TemplateDatabaseFileExtension, // Filter files by extension 
                Title = "Select an Existing Template File to Open"
            };

            // Show open file dialog box
            bool? result = openFileDialog.ShowDialog();

            // Process open file dialog box results 
            if (result == true)
            {
                OpenTemplateFileInTemplateEditor(openFileDialog.FileName);
            }
        }

        public async void OpenTemplateFileInTemplateEditor(string fileName)
        {
            // If its not a valid template, display a dialog and abort
            if (false == Dialogs.DialogIsFileValid(this, fileName))
            {
                return;
            }

            if (IsCondition.IsPathLengthTooLong(fileName, FilePathTypeEnum.Backup))
            {
                Dialogs.BackupPathTooLongDialog(this);
            }

            await TemplateDoOpen(fileName);
        }
        #endregion

        #region  MenuItemRecentTemplate
        private async void MenuItemRecentTemplate_Click(object sender, RoutedEventArgs e)
        {
            string recentTemplatePath = (string)((MenuItem)sender).ToolTip;

            if (System.IO.File.Exists(recentTemplatePath) == false)
            {
                Dialogs.EditorTemplateFileNoLongerExistsDialog(this, Path.GetFileName(recentTemplatePath));
                return;
            }
            // If its not a valid template, display a dialog and abort
            if (false == Dialogs.DialogIsFileValid(this, recentTemplatePath))
            {
                return;
            }
            await TemplateDoOpen(recentTemplatePath);
        }
        #endregion

        #region MenuItemUpgradeTimelapseFiles
        private void MenuItemUpgradeTimelapseFiles_Click(object sender, RoutedEventArgs e)
        {
            DialogUpgradeFiles.DialogUpgradeFilesAndFolders dialogUpdateFiles = new(this, string.Empty, VersionChecks.GetTimelapseCurrentVersionNumber().ToString());
            dialogUpdateFiles.ShowDialog();
        }
        #endregion

        #region MenuSwitchToTimelapse / MenuFileClose / MenuFileExit
        private void MenuSwitchToTimelapse_Click(object sender, RoutedEventArgs e)
        {
            if (this.templateDatabase != null)
            {
                if (Dialogs.MenuFileSwitchBetweenTimelapseAndEditorWarningDialog(this, false) == false)
                {
                    return;
                }
            }
            // Note: We don't have to do any cleanup via TemplateDoClose as we can't have a template open if we are switching to Timelapse
            this.closedByMenu = true;
            this.Close();
            this.TimelapseWindow.Show();
            this.TimelapseWindow.Activate();
        }

        // Closes the template and clears various states to allow another template to be created or opened.
        private void MenuFileClose_Click(object sender, RoutedEventArgs e)
        {
            this.closedByMenu = true;
            TemplateDoClose();
        }

        /// <summary>
        /// Exits the application.
        /// </summary>
        private void MenuFileExit_Click(object sender, RoutedEventArgs e)
        {
            // Note that Window_Closing, which does some cleanup, will be invoked as a side effect
            this.closedByMenu = true;
            this.Close();
            this.TimelapseWindow.Close();
            Application.Current.Shutdown();
        }
        #endregion

        #region Menu Helpers
        private async void CreateNewTemplateFileFromResource(string standardResourceFileName)
        {
            // If this operation would over-write an existing tdb file, we would back that old file up
            // before creating a new one

            // Ask the user for the path and file name of the file to create
            // ConfigureFormatForDateTimeCustom save file dialog box
            SaveFileDialog newTemplateFilePathDialog = new()
            {
                FileName = Path.GetFileNameWithoutExtension(File.DefaultTemplateDatabaseFileName), // Default file name without the extension
                DefaultExt = File.TemplateDatabaseFileExtension, // Default file extension
                Filter = "Database Files (" + File.TemplateDatabaseFileExtension + ")|*" + File.TemplateDatabaseFileExtension, // Filter files by extension 
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
                templateFileName = Path.ChangeExtension(templateFileName, File.TemplateDatabaseFileExtension[1..]);

                // First, check the file path length and notify the user the template if the path would be too long 
                // Note: The SaveFileDialog doesn't do the right thing when the user specifies a really long file name / path (it just returns the DefaultTemplateDatabaseFileName without a path), 
                // so we test for that too as it also indicates a too longpath name
                if (IsCondition.IsPathLengthTooLong(templateFileName, FilePathTypeEnum.TDB) || templateFileName.Equals(Path.GetFileNameWithoutExtension(File.DefaultTemplateDatabaseFileName)))
                {
                    Dialogs.TemplatePathTooLongDialog(this, templateFileName);
                    return;
                }
                if (IsCondition.IsPathLengthTooLong(templateFileName, FilePathTypeEnum.Backup))
                {
                    Dialogs.BackupPathTooLongDialog(this);
                    return;
                }

                // Overwrite the file if it exists
                if (System.IO.File.Exists(templateFileName))
                {
                    if (IsCondition.IsPathLengthTooLong(templateFileName, FilePathTypeEnum.Backup))
                    {
                        Dialogs.BackupPathTooLongDialog(this);
                    }

                    FileBackup.TryCreateBackup(templateFileName);
                    try
                    {
                        System.IO.File.Delete(templateFileName);
                    }
                    catch
                    {
                        Dialogs.FileCannotBeOverwrittenDialog(this, templateFileName);
                        return;
                    }
                }

                if (false == Timelapse.Util.FilesFolders.CopyResourceToFile(standardResourceFileName, templateFileName))
                {
                    Dialogs.EditorTemplateCouldNotCreateStandardDialog(this);
                }
                // Open document 
                await TemplateDoOpen(templateFileName).ConfigureAwait(true);
                MenuFileClose.IsEnabled = true;
                MenuMetadata.IsEnabled = true;
            }
        }

        // Create a new template file containing only the standard controls
        private async Task CreateNewTemplateFile()
        {
            bool makeBackup = true;
            DataGrid dataGrid = TemplateUI.TemplateDataGridControl.DataGridInstance;
            DataGridCommonCode.ApplyPendingEdits(dataGrid);

            // Save file dialog box
            SaveFileDialog newTemplateFilePathDialog = new()
            {
                FileName = Path.GetFileNameWithoutExtension(File.DefaultTemplateDatabaseFileName), // Default file name without the extension
                DefaultExt = File.TemplateDatabaseFileExtension, // Default file extension
                Filter = "Database Files (" + File.TemplateDatabaseFileExtension + ")|*" + File.TemplateDatabaseFileExtension, // Filter files by extension 
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
                templateFileName = Path.ChangeExtension(templateFileName, File.TemplateDatabaseFileExtension[1..]);

                // Now try to create or open the template database
                // First, check the file path length and notify the user the template couldn't be loaded because its path is too long 
                // Note: The SaveFileDialog doesn't do the right thing when the user specifies a really long file name / path (it just returns the DefaultTemplateDatabaseFileName without a path), 
                // so we test for that too as it also indicates a too longpath name
                if (IsCondition.IsPathLengthTooLong(templateFileName, FilePathTypeEnum.TDB) || templateFileName.Equals(Path.GetFileNameWithoutExtension(File.DefaultTemplateDatabaseFileName)))
                {
                    Dialogs.TemplatePathTooLongDialog(this, templateFileName);
                    return;
                }
                if (IsCondition.IsPathLengthTooLong(templateFileName, FilePathTypeEnum.Backup))
                {
                    Dialogs.BackupPathTooLongDialog(this);
                    makeBackup = false;
                }

                // Overwrite the file if it exists
                if (System.IO.File.Exists(templateFileName) && makeBackup)
                {
                    FileBackup.TryCreateBackup(templateFileName);
                    try
                    {
                        System.IO.File.Delete(templateFileName);
                    }
                    catch
                    {
                        Dialogs.FileCannotBeOverwrittenDialog(this, templateFileName);
                        return;
                    }
                }

                // Open document 
                await TemplateInitializeFromDBFileAsync(templateFileName).ConfigureAwait(true);
                MenuFileClose.IsEnabled = true;
                MenuMetadata.IsEnabled = true;
            }
        }

        // Create a numbered label whose prefix is the label
        private static string CreateLabel(string label)
        {
            string temp = Regex.Replace(label, @"ID", "Id");
            temp = Regex.Replace(temp, @"([A-Z])", " $1");
            return $"{temp[0].ToString().ToUpper()}{temp[1..]}";
        }

        /// Update the list of recent databases (ensuring they still exist) displayed under File -> Recent Databases.
        private void CreateMenuItemsForMenuFileRecentTemplates(bool enable)
        {
            MenuFileRecentTemplates.IsEnabled = enable && State.RecentlyOpenedTemplateFiles.Count > 0;
            MenuFileRecentTemplates.Items.Clear();

            int index = 1;
            foreach (string recentTemplatePath in State.RecentlyOpenedTemplateFiles)
            {
                if (Path.GetExtension(recentTemplatePath) == File.TemplateDatabaseFileExtension)
                {
                    MenuItem recentImageSetItem = new();
                    recentImageSetItem.Click += MenuItemRecentTemplate_Click;
                    recentImageSetItem.Header = $"_{index} {recentTemplatePath}";
                    recentImageSetItem.ToolTip = recentTemplatePath;
                    recentImageSetItem.IsEnabled = System.IO.File.Exists(recentTemplatePath);
                    MenuFileRecentTemplates.Items.Add(recentImageSetItem);
                    ++index;
                }
            }
        }
        #endregion

    }
}
