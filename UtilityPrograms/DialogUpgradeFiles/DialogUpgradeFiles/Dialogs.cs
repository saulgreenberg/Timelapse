using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// ReSharper disable once CheckNamespace
namespace DialogUpgradeFiles.Dialog
{
    public static class Dialogs
    {
        public static bool TryGetFilesFromUserUsingOpenFileDialog(string title, string defaultFilePath, string filter, string defaultExtension, out string[] selectedFiles)
        {
            // Get the template file, which should be located where the images reside
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = title;
                openFileDialog.CheckFileExists = true;
                openFileDialog.CheckPathExists = true;
                openFileDialog.Multiselect = true;
                openFileDialog.AutoUpgradeEnabled = true; // Set filter for file extension and default file extension 
                openFileDialog.DefaultExt = defaultExtension;
                openFileDialog.Filter = filter;
                if (string.IsNullOrWhiteSpace(defaultFilePath) || !(File.Exists(defaultFilePath) || Directory.Exists(defaultFilePath)))
                {
                    openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
                else
                {

                    FileAttributes attr = File.GetAttributes(defaultFilePath);
                    if (attr.HasFlag(FileAttributes.Directory))
                    {
                        openFileDialog.InitialDirectory = defaultFilePath;
                        openFileDialog.FileName = string.Empty;
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

            using (CommonOpenFileDialog folderSelectionDialog = new CommonOpenFileDialog())
            {
                folderSelectionDialog.Title = title;
                folderSelectionDialog.DefaultDirectory = initialFolder;
                folderSelectionDialog.IsFolderPicker = true;
                folderSelectionDialog.EnsurePathExists = false;
                folderSelectionDialog.Multiselect = true;
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
    }
}
