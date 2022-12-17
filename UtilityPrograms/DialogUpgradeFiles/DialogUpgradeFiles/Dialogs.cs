using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace DialogUpgradeFiles.Dialog
{
    public static class Dialogs
    {
        public static bool TryGetFilesFromUserUsingOpenFileDialog(string title, string defaultFilePath, string filter, string defaultExtension, out string[] selectedFiles)
        {
            // Get the template file, which should be located where the images reside
            using (OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Title = title,
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = true,
                AutoUpgradeEnabled = true,

                // Set filter for file extension and default file extension 
                DefaultExt = defaultExtension,
                Filter = filter
            })
            {
                if (String.IsNullOrWhiteSpace(defaultFilePath) || !(File.Exists(defaultFilePath) || Directory.Exists(defaultFilePath)))
                {
                    openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
                else
                {

                    FileAttributes attr = File.GetAttributes(defaultFilePath);
                    if (attr.HasFlag(FileAttributes.Directory))
                    {
                        openFileDialog.InitialDirectory = defaultFilePath;
                        openFileDialog.FileName = String.Empty;
                    }
                    else
                    {
                        openFileDialog.InitialDirectory = Path.GetDirectoryName(defaultFilePath);
                        openFileDialog.FileName = Path.GetFileName(defaultFilePath);
                    }

                }

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    selectedFiles = openFileDialog.FileNames;
                    return true;
                }
                selectedFiles = null;
                return false;
            }
        }

        public static bool TryGetFoldersFromUserUsingOpenFileDialog(string title, string initialFolder, out string[] selectedFolderPath)
        {

            using (CommonOpenFileDialog folderSelectionDialog = new CommonOpenFileDialog()
            {
                Title = title,
                DefaultDirectory = initialFolder,
                IsFolderPicker = true,
                EnsurePathExists = false,
                Multiselect = true
            })
            {
                folderSelectionDialog.InitialDirectory = folderSelectionDialog.DefaultDirectory;
                if (folderSelectionDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    selectedFolderPath = folderSelectionDialog.FileNames.ToArray();
                    return true;
                }
                selectedFolderPath = new string[] { };
                return false;
            }
        }
    
        public static void BackupPathsAreTooLong(Window owner, int count)
        {
            Timelapse.Dialog.MessageBox mbox = new Timelapse.Dialog.MessageBox("Some backup files will be in a different place ", owner, MessageBoxButton.OK);
            string pathPlural = count == 1 ? "one " : count.ToString() + " of your files .)";
            mbox.Message.What = "While updating some of your files, Timelapse could not create backups for " + pathPlural + " of your files in the Backups folder. " + Environment.NewLine +
                "Instead, those backups will be created in the same folder as the original file and its suffix changed:" + Environment.NewLine +
                " - the .tdb backup suffix is .tbk" + Environment.NewLine +
                " - the .ddb backup suffix is .dbk";
            mbox.Message.Reason = "Your backup file paths would be too long. Windows imposes a 260 character length restriction of file path";
            mbox.Message.Hint = "If updating issues occur, you can restore the original file by changing its suffix." + Environment.NewLine +
                "If everything goes well, you can delete the tbk and dbk files as desired.";
            mbox.ShowDialog();

            //string message = "Cannot create a normal backup for " + pathPlural + "too long." + Environment.NewLine +
            //    "Instead, the backup will be created in the original folder by replacing its suffix:" + Environment.NewLine +
            //    " - .tdb backup suffix is .tbk" + Environment.NewLine +
            //    " - .ddb backup suffix is .dbk" + Environment.NewLine + Environment.NewLine +
            //    "If issues occur, you can retrieve the original by changing its suffix back.";
            //MessageBox.Show(owner, message, "Cannot create normal backup files", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
