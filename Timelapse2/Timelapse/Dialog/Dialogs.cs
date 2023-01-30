using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.Enums;
using Timelapse.Util;
using Clipboard = System.Windows.Clipboard;
using Cursor = System.Windows.Input.Cursor;
using Rectangle = System.Drawing.Rectangle;

namespace Timelapse.Dialog
{
    public static class Dialogs
    {
        #region Dialog Box Positioning and Fitting
        // Most (but not all) invocations of SetDefaultDialogPosition and TryFitDialogWndowInWorkingArea 
        // are done together, so collapse it into a single call
        public static void TryPositionAndFitDialogIntoWindow(Window window)
        {
            Dialogs.SetDefaultDialogPosition(window);
            Dialogs.TryFitDialogInWorkingArea(window);
        }


        // Position the dialog box within its owner's window
        public static void SetDefaultDialogPosition(Window window)
        {
            // Check the arguments for null 
            if (window == null)
            {
                // this should not happen
                TracePrint.StackTrace("Window's owner property is null. Is a set of it prior to calling ShowDialog() missing?", 1);
                // Treat it as a no-op
                return;
            }

            window.Left = window.Owner.Left + (window.Owner.Width - window.ActualWidth) / 2; // Center it horizontally
            window.Top = window.Owner.Top + 20; // Offset it from the windows'top by 20 pixels downwards
        }

        // Used to ensure that the window is positioned within the screen
        // Note that all uses of this method is by dialog box windows (which should be initialy positioned relative to the main timelapse window) by a call to SetDefaultDialogPosition), 
        // rather than the main timelapse window (whose position, size and layout  is managed by the TimelapseAvalonExtension methods). 
        // We could likely collapse the two, but its not worth the bother. 
        // This will sort of fail if a window's minimum size is larger than the available screen space. It should still show the window, but it may cut off the bottom of it.
        public static bool TryFitDialogInWorkingArea(Window window)
        {
            int minimumWidthOrHeightValue = 200;
            int makeItATouchSmaller = 10;
            if (window == null)
            {
                return false;
            }
            if (Double.IsNaN(window.Left))
            {
                window.Left = 0;
            }
            if (Double.IsNaN(window.Top))
            {
                window.Top = 0;
            }

            // Get DPI factor from the main window
            // Not sure how this would all work if multi-monitors had different dpis...
            double dpiWidthFactor = 1;
            double dpiHeightFactor = 1;
            Window mainWindow = System.Windows.Application.Current.MainWindow;
            PresentationSource presentationSource = PresentationSource.FromVisual(mainWindow);
            if (presentationSource != null)
            {
                CompositionTarget compositionTarget = presentationSource.CompositionTarget;
                Matrix m = compositionTarget.TransformToDevice;
                //Matrix m = PresentationSource.FromVisual(System.Windows.Application.Current.MainWindow).CompositionTarget.TransformToDevice;
                dpiWidthFactor = m.M11;
                dpiHeightFactor = m.M22;
            }

            // Get the monitor screen that this window appears to be on
            Screen screenInDpi = Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(window).Handle);

            // A user reported a bug where the window height was negative. Not sure what value we should
            // really be testing against... Maybe MinWidth? Anyways, this should at least catch the worst of it.
            if (window.Width * dpiWidthFactor <= minimumWidthOrHeightValue || window.Height * dpiHeightFactor <= minimumWidthOrHeightValue)
            {
                return false;
            }

            // Get a rectangle defining the current window, converted to dpi
            Rectangle windowPositionInDpi = new Rectangle(
                        (int)(window.Left * dpiWidthFactor),
                        (int)(window.Top * dpiHeightFactor),
                        (int)(window.Width * dpiWidthFactor),
                        (int)(window.Height * dpiHeightFactor));

            // Get a rectangle defining the working area for this window, which should be dpi
            // We will compare the two to see how the window fits into the working area.
            Rectangle workingAreaInDpi = Screen.GetWorkingArea(windowPositionInDpi);

            // If needed, adjust the window's height to be somewhat smaller than the screen's height 
            // Allow some space for the task bar at the screen's bottom and place
            // the window at the screen's top. Note that this won't cater for the situation when
            // the task bar is at the top of the screen, but so it goes.
            if (windowPositionInDpi.Height > screenInDpi.WorkingArea.Height)
            {
                double height = (screenInDpi.WorkingArea.Height - makeItATouchSmaller) / dpiHeightFactor;
                if (height < minimumWidthOrHeightValue || height < 0)
                {
                    return false;
                }
                window.Height = height;
                window.Top = 0;
            }

            bool windowFitsInWorkingArea = true;
            // move window up if it extends below the working area
            if (windowPositionInDpi.Bottom > workingAreaInDpi.Bottom)
            {
                int dpiToMoveUp = Convert.ToInt32(windowPositionInDpi.Bottom) - workingAreaInDpi.Bottom;
                if (dpiToMoveUp > windowPositionInDpi.Top)
                {
                    // window is too tall and has to shorten to fit screen
                    window.Top = 0;
                    double height = workingAreaInDpi.Bottom / dpiHeightFactor - makeItATouchSmaller;
                    if (height < minimumWidthOrHeightValue || height < 0)
                    {
                        return false;
                    }
                    window.Height = height;
                    windowFitsInWorkingArea = false;
                }
                else if (dpiToMoveUp > 0)
                {
                    // move window up
                    window.Top -= dpiToMoveUp / dpiHeightFactor;
                }
            }

            // move window left if it extends right of the working area
            if (windowPositionInDpi.Right > workingAreaInDpi.Right)
            {
                int dpiToMoveLeft = windowPositionInDpi.Right - workingAreaInDpi.Right;
                if (windowPositionInDpi.Left >= 0 && dpiToMoveLeft > windowPositionInDpi.Left)
                {
                    // window is too wide and has to narrow to fit screen
                    window.Left = 0;
                    // So we don't get massively sized windows in case its a large working area with multiple monitors
                    window.Width = Math.Min(workingAreaInDpi.Width, windowPositionInDpi.Width);
                    windowFitsInWorkingArea = false;
                }
                else if (dpiToMoveLeft > 0)
                {
                    // move window left
                    window.Left -= dpiToMoveLeft / dpiWidthFactor;
                }
            }
            return windowFitsInWorkingArea;
        }
        #endregion

        #region OpenFileDialog: Get file or folder
        /// <summary>
        /// Prompt the user for a file location via an an open file dialog. Set selectedFilePath.
        /// </summary>
        /// <returns>True if the user indicated one, else false. selectedFilePath contains the selected path, if any, otherwise null </returns>
        public static bool TryGetFileFromUserUsingOpenFileDialog(string title, string defaultFilePath, string filter, string defaultExtension, out string selectedFilePath)
        {
            // Get the template file, which should be located where the images reside
            using (OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Title = title,
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false,
                AutoUpgradeEnabled = true,

                // Set filter for file extension and default file extension 
                DefaultExt = defaultExtension,
                Filter = filter
            })
            {
                if (String.IsNullOrWhiteSpace(defaultFilePath))
                {
                    openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
                else
                {
                    openFileDialog.InitialDirectory = Path.GetDirectoryName(defaultFilePath);
                    openFileDialog.FileName = Path.GetFileName(defaultFilePath);
                }

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    selectedFilePath = openFileDialog.FileName;
                    return true;
                }
                selectedFilePath = null;
                return false;
            }
        }

        /// <summary>
        /// Prompt the user for a folder location via an an open file dialog. 
        /// </summary>
        /// <returns>The selected path, otherwise null </returns>
        public static bool TryGetFolderFromUserUsingOpenFileDialog(string title, string initialFolder, out string selectedFolderPath)
        {
            selectedFolderPath = String.Empty;
            using (CommonOpenFileDialog folderSelectionDialog = new CommonOpenFileDialog()
            {
                Title = title,
                DefaultDirectory = initialFolder,
                IsFolderPicker = true,
                EnsurePathExists = false,
                Multiselect = false

            })
            {
                folderSelectionDialog.InitialDirectory = folderSelectionDialog.DefaultDirectory;
                if (folderSelectionDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    selectedFolderPath = folderSelectionDialog.FileName;
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Folder dialog where the user can only select a sub-folder of the root folder path
        /// It returns the relative path to the selected folder
        /// If folderNameToLocate is not empty, it displays that a desired folder to select in the dialog title.
        /// </summary>
        /// <param name="initialFolder">The path to the root folder containing the template</param>
        /// <param name="folderNameToLocate">If folderNameToLocate is not empty, it displays that a desired folder to select.</param>
        /// <returns></returns>

        public static string LocateRelativePathUsingOpenFileDialog(string initialFolder, string folderNameToLocate)
        {
            if (initialFolder == null)
            {
                return String.Empty;
            }
            using (CommonOpenFileDialog folderSelectionDialog = new CommonOpenFileDialog()
            {
                Title = "Locate folder" + folderNameToLocate + "...",
                DefaultDirectory = initialFolder,
                IsFolderPicker = true,
                Multiselect = false
            })
            {
                folderSelectionDialog.InitialDirectory = folderSelectionDialog.DefaultDirectory;
                folderSelectionDialog.FolderChanging += FolderSelectionDialog_FolderChanging;
                if (folderSelectionDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    // Trim the root folder path from the folder name to produce a relative path. 
                    return (folderSelectionDialog.FileName.Length > initialFolder.Length) ? folderSelectionDialog.FileName.Substring(initialFolder.Length + 1) : String.Empty;
                }
                else
                {
                    return null;
                }
            }
        }

        // Limit the folder selection to only those that are sub-folders of the folder path
        private static void FolderSelectionDialog_FolderChanging(object sender, CommonFileDialogFolderChangeEventArgs e)
        {
            if (!(sender is CommonOpenFileDialog dialog))
            {
                return;
            }
            // require folders to be loaded be either the same folder as the .tdb and .ddb or subfolders of it
            if (e.Folder.StartsWith(dialog.DefaultDirectory, StringComparison.OrdinalIgnoreCase) == false)
            {
                e.Cancel = true;
            }
        }
        #endregion

        #region SaveFileDialog: Get file or folder
        /// <summary>
        /// Prompt the user for a file location via an an open file dialog. Set selectedFilePath.
        /// </summary>
        /// <returns>True if the user indicated one, else false. selectedFilePath contains the selected path, if any, otherwise null </returns>
        public static bool TryGetFileFromUserUsingSaveFileDialog(string title, string defaultFilePath, string filter, string defaultExtension, out string selectedFilePath)
        {
            // Get the template file, which should be located where the images reside
            using (SaveFileDialog saveFileDialog = new SaveFileDialog()
            {
                Title = title,
                CheckFileExists = false,
                CheckPathExists = true,
                AutoUpgradeEnabled = true,

                // Set filter for file extension and default file extension 
                DefaultExt = defaultExtension,
                Filter = filter
            })
            {
                if (String.IsNullOrWhiteSpace(defaultFilePath))
                {
                    saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
                else
                {
                    saveFileDialog.InitialDirectory = Path.GetDirectoryName(defaultFilePath);
                    saveFileDialog.FileName = Path.GetFileName(defaultFilePath);
                }

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    selectedFilePath = saveFileDialog.FileName;
                    return true;
                }
                selectedFilePath = null;
                return false;
            }
        }
        #endregion

        #region MessageBox: DragDrop Close template and open a new template
        // Confirm closing this template and creating a new one
        public static bool? CloseTemplateAndOpenNewTemplate(Window owner, string newTemplateFileName)
        {
            return new MessageBox("Close this template and open another one", owner, MessageBoxButton.OKCancel)
            {
                Message =
                    {
                        Icon = MessageBoxImage.Question,
                        What = $"Close the current template and open this one instead? {Environment.NewLine}   {newTemplateFileName} "
                    }
            }.ShowDialog();
        }
        #endregion

        #region MessageBox: Cannot read/write file
        public static void FileCantOpen(Window owner, string path, bool isFile)
        {
            string entity = isFile ? "file" : "folder";
            // Tell the user we could not read or write the file
            string title = "Could not open the " + entity;
            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OK)
            {
                Message =
                {
                    What = "The " + entity + " could not be opened:"
                           + Environment.NewLine
                           + path,
                    Reason = "There are many possible reasons, including:"
                             + Environment.NewLine
                             + "\u2022 the folder may not be accessible or may not exist " + Environment.NewLine
                             + "\u2022 you may not have permission to access the " + entity + Environment.NewLine
                             + "\u2022 another application may be using the " + entity,
                    Solution = "Check to see if: "
                               + Environment.NewLine
                               + "\u2022 the folder exists or if you can create it" + Environment.NewLine
                               + "\u2022 you can create a file in that folder"
                }
            };

            if (isFile)
            {
                messageBox.Message.Solution += Environment.NewLine
                + "\u2022 you can open and close that file with another application" + Environment.NewLine
                + "\u2022 another application is using that file";
            }
            messageBox.Message.Hint = "Try logging off and then back on, which may release the " + entity + " if another application is using it.";
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: Prompt to apply operation if partial selection.
        // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
        public static bool MaybePromptToApplyOperationOnSelectionDialog(Window owner, FileDatabase fileDatabase, bool promptState, string operationDescription, Action<bool> persistOptOut)
        {
            if (Dialogs.CheckIfPromptNeeded(promptState, fileDatabase, out int filesTotalCount, out int filesSelectedCount) == false)
            {
                // if showing all images, or if users had elected not to be warned, then no need for showing the warning message
                return true;
            }

            // Warn the user that the operation will only be applied to an image set.
            string title = "Apply " + operationDescription + " to this selection?";
            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    What = operationDescription + " will be applied only to a subset of your images."
                                                + Environment.NewLine
                                                + "Is this what you want?",
                    Reason = $"A 'selection' is active, where you are currently viewing {filesSelectedCount}/{filesTotalCount} total files.{Environment.NewLine}"
                             + "Only these selected images will be affected by this operation." + Environment.NewLine
                             + "Data for other unselected images will be unaffected.",
                    Solution = "Select "
                               + Environment.NewLine
                               + "\u2022 'Ok' for Timelapse to continue to " + operationDescription + " for these selected files" + Environment.NewLine
                               + "\u2022 'Cancel' to abort",
                    Hint = "This is not an error."
                           + Environment.NewLine
                           + "\u2022 We are just reminding you that you have an active selection that is displaying only a subset of your images." + Environment.NewLine
                           + "\u2022 You can apply this operation to that subset ." + Environment.NewLine
                           + "\u2022 However, if you did want to do this operaton for all images, choose the 'Select|All files' menu option.",
                    Icon = MessageBoxImage.Question
                },
                DontShowAgain =
                {
                    Visibility = Visibility.Visible
                }
            };

            bool proceedWithOperation = (bool)messageBox.ShowDialog();
            if (proceedWithOperation && messageBox.DontShowAgain.IsChecked.HasValue && persistOptOut != null)
            {
                persistOptOut(messageBox.DontShowAgain.IsChecked.Value);
            }
            return proceedWithOperation;
        }

        // Check if a prompt dialog is needed
        private static bool CheckIfPromptNeeded(bool promptState, FileDatabase fileDatabase, out int filesTotalCount, out int filesSelectedCount)
        {
            filesTotalCount = 0;
            filesSelectedCount = 0;
            if (fileDatabase == null)
            {
                // This should not happen. Maybe raise an exception?
                // In any case, don't show the prompt
                return false;
            }

            if (promptState)
            {
                // We don't show the prompt as the user has turned it off.
                return false;
            }
            // We want to show the prompt only if the promptState is true, and we are  viewing all images
            filesTotalCount = fileDatabase.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.All);
            filesSelectedCount = fileDatabase.FileTable.RowCount;
            return filesTotalCount != filesSelectedCount;
        }
        #endregion

        #region MessageBox: Overwrite Files?
        // Check if a prompt dialog is needed
        public static bool OverwriteExistingFiles(Window owner, int existingFilesCount)
        {
            // Warn the user that the operation will overwrite existing files.
            const string title = "Overwrite existing files?";
            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    What = $"Overwrite {existingFilesCount} files with the same name?",
                    Reason = $"The destination folder already has {existingFilesCount} files with the same name",
                    Solution = "Select "
                               + Environment.NewLine
                               + "\u2022 'Ok' for Timelapse to overwrite those files" + Environment.NewLine
                               + "\u2022 'Cancel' to abort",
                    Icon = MessageBoxImage.Question
                }
            };
            return messageBox.ShowDialog() == true;
        }
        #endregion

        #region MessageBox: File Exists
        public static void FileExistsDialog(Window owner, string filePath)
        {
            const string title = "The file already exists.";
            MessageBox messageBox = new MessageBox(title, owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Error,
                    Title = title,
                    Problem = "This file already exists, so nothing was done."
                              + Environment.NewLine
                              + "\u2022 " + filePath,
                    Solution = "Use a different file name."
                }
            };
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: Missing dependencies
        public static void DependencyFilesMissingDialog(string applicationName)
        {
            // can't use DialogMessageBox to show this message as that class requires the Timelapse window to be displayed.
            string messageTitle = $"{applicationName} needs to be in its original downloaded folder.";
            StringBuilder message = new StringBuilder("Problem:" + Environment.NewLine);
            message.AppendFormat("{0} won't run properly as it was not correctly installed.{1}{1}", applicationName, Environment.NewLine);
            message.AppendLine("Reason:");
            message.AppendFormat("When you downloaded {0}, it was in a folder with several other files and folders it needs. You probably dragged {0} out of that folder.{1}{1}", applicationName, Environment.NewLine);
            message.AppendLine("Solution:");
            message.AppendFormat("Move the {0} program back to its original folder, or download it again.{1}{1}", applicationName, Environment.NewLine);
            message.AppendLine("Hint:");
            message.AppendFormat("Create a shortcut if you want to access {0} outside its folder:{1}", applicationName, Environment.NewLine);
            message.AppendLine("1. From its original folder, right-click the Timelapse program icon.");
            message.AppendLine("2. Select 'Create Shortcut' from the menu.");
            message.Append("3. Drag the shortcut icon to the location of your choice.");
            System.Windows.MessageBox.Show(message.ToString(), messageTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        #endregion

        #region MessageBox: Path too long warnings
        // This version is for hard crashes. however, it may disappear from display too fast as the program will be shut down.
        public static void FilePathTooLongDialog(Window owner, UnhandledExceptionEventArgs e)
        {
            string title = "Your File Path Names are Too Long to Handle";
            MessageBox messageBox = new MessageBox(title, owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Error,
                    Title = title,
                    Problem = "Timelapse has to shut down as one or more of your file paths are too long.",
                    Solution = "\u2022 Shorten the path name by moving your image folder higher up the folder hierarchy, or" + Environment.NewLine + "\u2022 Use shorter folder or file names.",
                    Reason = "Windows cannot perform file operations if the folder path combined with the file name is more than " + Constant.File.MaxPathLength + " characters."
                             + "Timelapse will shut down until you fix this.",
                    Hint = "Files created in your " + Constant.File.BackupFolder + " folder must also be less than " + Constant.File.MaxPathLength + " characters."
                }
            };
            if (e != null)
            {
                Clipboard.SetText(e.ExceptionObject.ToString());
            }
            messageBox.ShowDialog();
        }

        public static bool FilePathDeletedFileTooLongDialog(Window owner)
        {
            const string title = "The files you want to delete won't be backed up.";
            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    Icon = MessageBoxImage.Warning,
                    Title = title,
                    Problem = title
                              + Environment.NewLine
                              + "As a precaution, Timelapse normally moves deleted files into the " +
                              Constant.File.DeletedFilesFolder + " folder." + Environment.NewLine
                              + "However, the new file paths are too long for Windows to handle.",
                    Reason = "Windows cannot perform file operations if the file path is more than " +
                             (Constant.File.MaxPathLength + 8) + " characters.",
                    Solution = "Click Okay to delete these files without backing them up, or Cancel to abort." +
                               Environment.NewLine
                               + "Alternately, shorten the path to your files, preferably well below the length limit:" +
                               Environment.NewLine
                               + "\u2022 move your image folder higher up the folder hierarchy, or" +
                               Environment.NewLine
                               + "\u2022 use shorter folder or file names."
                }
            };
            return messageBox.ShowDialog() == true;
        }

        // This version detects and displays warning messages.
        public static void FilePathTooLongDialog(Window owner, List<string> folders)
        {
            ThrowIf.IsNullArgument(folders, nameof(folders));

            const string title = "Some of your Image File Path Names Were Too Long";
            MessageBox messageBox = new MessageBox(title, owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Error,
                    Title = title,
                    Problem = "Timelapse skipped reading some of your images in the folders below, as their file paths were too long.",
                    Reason = "Windows cannot perform file operations if the folder path combined with the file name is more than " + Constant.File.MaxPathLength + " characters.",
                    Solution = "Try reloading this image set after shortening the file path:"
                               + Environment.NewLine
                               + "\u2022 shorten the path name by moving your image folder higher up the folder hierarchy, or" + Environment.NewLine + "\u2022 use shorter folder or file names.",
                    Hint = "Files created in your " + Constant.File.BackupFolder + " folder must also be less than " + Constant.File.MaxPathLength + " characters."
                }
            };

            if (folders.Count > 0)
            {
                messageBox.Message.Problem += "Those files are found in these folders:";
                foreach (string folder in folders)
                {
                    messageBox.Message.Problem += Environment.NewLine + "\u2022 " + folder;
                }
            }
            messageBox.ShowDialog();
        }

        // notify the user when the path is too long
        public static void TemplatePathTooLongDialog(Window owner, string templateDatabasePath)
        {
            new MessageBox("Timelapse could not open the template ", owner)
            {
                Message =
                {
                    Problem = "Timelapse could not open the template (.tdb) file as its name is too long:"
                              + Environment.NewLine
                              + "\u2022 " + templateDatabasePath,
                    Reason = "Windows imposes a file name length limit (including its folder path) of around " + Constant.File.MaxPathLength + " characters.",
                    Solution = "Shorten the path name, preferably well below the length limit:"
                               + Environment.NewLine
                               + "\u2022 move your image folder higher up the folder hierarchy, or" + Environment.NewLine
                               + "\u2022 use shorter folder or file names.",
                    Icon = MessageBoxImage.Error
                }
            }.ShowDialog();
        }

        // notify the user the template couldn't be loaded because its path is too long
        public static void DatabasePathTooLongDialog(Window owner, string databasePath)
        {
            new MessageBox("Timelapse could not load the database", owner)
            {
                Message =
                {
                    Problem = "Timelapse could not load the database (.ddb) file as its name is too long:"
                              + Environment.NewLine
                              + "\u2022 " + databasePath,
                    Reason = "Windows imposes a file name length limit (including its folder path) of around " + Constant.File.MaxPathLength + " characters.",
                    Solution = "Shorten the path name, preferably well below the length limit:" + Environment.NewLine
                        + "\u2022 move your image folder higher up the folder hierarchy, or" + Environment.NewLine
                        + "\u2022 use shorter folder or file names.",
                    Icon = MessageBoxImage.Error
                }
            }.ShowDialog();
        }

        // Warn the user if backups may not be made
        public static void BackupPathTooLongDialog(Window owner)
        {
            new MessageBox("Timelapse can't back up your files", owner)
            {
                Message =
                {
                    Problem = "Timelapse will continue, but without backing up your files."
                              + Environment.NewLine
                              + "The issue is that the backup file can't be created as its name is too long for Windows to handle.",
                    Reason = "Timelapse normally creates time-stamped backup files of your template, database, and csv files within a " + Constant.File.BackupFolder + " folder." + Environment.NewLine
                             + "However, Windows imposes a file name length limit (including its folder path) of around " + Constant.File.MaxPathLength + " characters.",
                    Solution = "Shorten the path name, preferably well below the length limit:"
                               + Environment.NewLine
                               + "\u2022 move your image folder higher up the folder hierarchy, or" + Environment.NewLine
                               + "\u2022 use shorter folder or file names.",
                    Hint = "You can still use Timelapse, but backup files will not be created.",
                    Icon = MessageBoxImage.Warning
                }
            }.ShowDialog();
        }

        // notify the user the template couldn't be loaded because its path is too long
        public static void DatabaseRenamedPathTooLongDialog(Window owner, string databasePath)
        {
            new MessageBox("Timelapse could not rename the database", owner)
            {
                Message =
                {
                    Problem = "Timelapse could not rename the database (.ddb) file as its name would be too long:"
                              + Environment.NewLine
                              + "\u2022 " + databasePath,
                    Reason = "Windows imposes a file name length limit (including its folder path) of around " + Constant.File.MaxPathLength + " characters.",
                    Solution = "Shorten the path name, preferably well below the length limit:"
                               + Environment.NewLine
                               + "\u2022 move your image folder higher up the folder hierarchy, or" + Environment.NewLine
                               + "\u2022 use shorter folder or file names.",
                    Icon = MessageBoxImage.Error
                }
            }.ShowDialog();
        }
        #endregion

        #region MessageBox: .tdb is in a root or system folder
        public static void TemplateInDisallowedFolder(Window owner, bool isDrive, string path)
        {
            const string title = "Your template file is in a problematic location";
            MessageBox messageBox = new MessageBox(title, owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Error,
                    Title = title,
                    Problem = "The location of your template is problematic. It should be in a normal folder." + Environment.NewLine,
                    Reason = "Timelapse expects templates and images to be in a normal folder." + Environment.NewLine,
                    Solution = "Create a new folder, and try moving your files to that folder."
                }
            };
            if (isDrive)
            {
                messageBox.Message.Problem +=
                    $"The issue is that your files are located in the top-level root drive '{path}' rather than a folder.{Environment.NewLine}"
                    + "Timelapse disallows this as the entire drive would be searched for images. ";
                messageBox.Message.Reason += "Timelapse cannot tell if this location is a massive drive containing all your files" + Environment.NewLine
                 + "(which would take ages to search and would retrieve every single image on it)," + Environment.NewLine
                 + "or, for example, an SD card that only contains your image set images.";
            }
            else
            {
                messageBox.Message.Problem += "The issue is that your files are located in a system or hidden folder:" + Environment.NewLine + "\u2022 " + path;
                messageBox.Message.Reason += "As system or hidden folders shouldn't normally contain user files, this could lead to future problems.";
                messageBox.Message.Solution += Environment.NewLine + "Or, you may be able to change the folder's attributes by selecting 'Properties' from" + Environment.NewLine
                + "that folder's context menu, and reviewing the 'Attributes' settings on the 'General' tab";
            }
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: template includes a control of an unknown type
        public static void TemplateIncludesControlOfUnknownType(Window owner, string unknownTypes)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            // notify the user the template couldn't be loaded rather than silently doing nothing
            new MessageBox("Your template file has an issue.", owner)
            {
                Message =
                {
                    Problem = "Your template has an issue",
                    Reason = "Your template contains data controls of unknown types."
                             + Environment.NewLine
                             + "\u2022 " + unknownTypes + Environment.NewLine
                             + "This could happen if you are trying to open a template with an old Timelapse version," + Environment.NewLine
                             + "as newer Timelapse versions may have new types of controls.",
                    Solution = "Download the latest verson of Timelapse and try reloading your files." + Environment.NewLine
                        + "You can also send an explanatory note to saul@ucalgary.ca." + Environment.NewLine
                        + "He will check those files to see if there is a fixable bug.",
                    Icon = MessageBoxImage.Error
                }
            }.ShowDialog();
        }
        #endregion

        #region MessageBox: Corrupted template
        public static void TemplateFileNotLoadedAsCorruptDialog(Window owner, string templateDatabasePath)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            // notify the user the template couldn't be loaded rather than silently doing nothing
            MessageBox messageBox = new MessageBox("Timelapse could not load the Template file.", owner)
            {
                Message =
                {
                    Problem = "Timelapse could not load the Template File :"
                    + Environment.NewLine
                    + "\u2022 " + templateDatabasePath,
                    Reason = $"The template ({Constant.File.TemplateDatabaseFileExtension}) file may be corrupted, unreadable, or otherwise invalid.",
                    Solution = "Try one or more of the following:"
                    + Environment.NewLine
                    + $"\u2022 recreate the template, or use another copy of it.{Environment.NewLine}"
                    + $"\u2022 check if there is a valid template file in your {Constant.File.BackupFolder} folder.{Environment.NewLine}"
                    + $"\u2022 email {Constant.ExternalLinks.EmailAddress} describing what happened, attaching a copy of your {Constant.File.TemplateDatabaseFileExtension} file.",
                    Result = "Timelapse did not affect any of your other files.",
                    Icon = MessageBoxImage.Error,
                    Hint = owner.Name.Equals("Timelapse") // Only displayed in Timelapse, not the template editor
                     ? $"See if you can open and examine the template file in the Timelapse Template Editor.{Environment.NewLine}"
                        + $"If you can't, and if you don't have a copy elsewhere, you will have to recreate it.{Environment.NewLine}"
                        + $"You can also send an explanatory note to saul@ucalgary.ca.{Environment.NewLine}"
                        + "He will check those files to see if there is a fixable bug."
                     : string.Empty
                }
            };
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: Corrupted .ddb file (no primary key)
        public static void DatabaseFileNotLoadedAsCorruptDialog(Window owner, string ddbDatabasePath, bool isEmpty)
        {
            // notify the user the database couldn't be loaded because there is a problem with it
            MessageBox messageBox = new MessageBox("Timelapse could not load your database file.", owner)
            {
                Message =
                {
                    Problem = "Timelapse could not load your .ddb database file:"
                        + Environment.NewLine
                        + "\u2022 " + ddbDatabasePath,
                    Reason = isEmpty
                         ? "Your database file is empty. Possible reasons include:" + Environment.NewLine
                        : "Your database is unreadable or corrupted. Possible reasons include:" + Environment.NewLine,
                    Solution = "\u2022 If you have not analyzed any images yet, delete the .ddb file and try again."
                        + Environment.NewLine
                        + "\u2022 Also, check for valid backups of your database in your " + Constant.File.BackupFolder + " folder that you can reuse.",
                    Hint = "If you are stuck: Send an explanatory note to saul@ucalgary.ca." + Environment.NewLine
                         + "He will check those files to see if there is a fixable bug.",
                 Icon = MessageBoxImage.Error
                 }
            };
            messageBox.Message.Reason += "\u2022 Timelapse was shut down (or crashed) in the midst of:" + Environment.NewLine
                + "    - loading your image set for the first time, or" + Environment.NewLine
                + "    - writing your data into the file, or" + Environment.NewLine
                + "\u2022 system, security or network  restrictions prohibited file reading and writing, or," + Environment.NewLine
                + "\u2022 some other unkown reason.";
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: Not a Timelapse File
        public static void FileNotATimelapseFile(Window owner, string templateDatabasePath)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            // notify the user the template couldn't be loaded rather than silently doing nothing
            new MessageBox("Could not load the Timelapse file.", owner)
            {
                Message =
                {
                    Problem = "The file does not appear to be a timelapse file:"
                              + Environment.NewLine
                              + "\u2022 " + templateDatabasePath,
                    Reason = "Timelapse files are either:" + Environment.NewLine
                                                           + $"\u2022 template files with a suffix {Constant.File.TemplateDatabaseFileExtension} {Environment.NewLine}"
                                                           + $"\u2022 data files with a suffix {Constant.File.FileDatabaseFileExtension}",
                    Solution = "Load only template and database files with those suffixes.",
                    Icon = MessageBoxImage.Error
                }
            }.ShowDialog();
        }
        #endregion

        #region MessageBox: Not a template
        // notify the user the template couldn't be loaded rather than silently doing nothing
        public static void TemplateFileNotATDB(Window owner, string templateDatabasePath)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            new MessageBox("Could not load the Timelapse Template file.", owner)
            {
                Message =
                 {
                     Problem = "The file does not appear to be a template file:"
                               + Environment.NewLine
                               + "\u2022 " + templateDatabasePath,
                     Reason = $"Template files are identifed by the suffix {Constant.File.TemplateDatabaseFileExtension} .",
                     Solution = $"Load a valid template file ending in {Constant.File.TemplateDatabaseFileExtension} .",
                     Icon = MessageBoxImage.Error
                 }
            }.ShowDialog();
        }
        #endregion

        #region MessageBox: Not a data file
        // notify the user the database couldn't be loaded rather than silently doing nothing
        public static void DatabaseFileNotADDB(Window owner, string databasePath)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            new MessageBox("Could not load the Timelapse Database file.", owner)
            {
                Message =
                 {
                     Problem = "The file does not appear to be a database file:"
                               + Environment.NewLine
                               + "\u2022 " + databasePath,
                     Reason = $"Database files are identifed by the suffix {Constant.File.FileDatabaseFileExtension} .",
                     Solution = $"Load a valid database file ending in {Constant.File.FileDatabaseFileExtension} .",
                     Icon = MessageBoxImage.Error
                 }
            }.ShowDialog();
        }
        #endregion

        #region MessageBox: DataEntryHandler Confirmations / Warnings for Propagate, Copy Forward, Propagate to here
        // Display a dialog box saying there is nothing to propagate. 
        public static void DataEntryNothingToPropagateDialog(Window owner)
        {
            new MessageBox("Nothing to Propagate to Here.", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Exclamation,
                    Reason = "All the earlier files have nothing in this field, so there are no values to propagate."
                }
            }.ShowDialog();
        }

        // Display a dialog box saying there is nothing to copy forward. 
        public static void DataEntryNothingToCopyForwardDialog(Window owner)
        {
            // Display a dialog box saying there is nothing to propagate. Note that this should never be displayed, as the menu shouldn't be highlit if there is nothing to propagate
            // But just in case...
            new MessageBox("Nothing to copy forward.", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Exclamation,
                    Reason = "As you are on the last file, there are no files after this."
                }
            }.ShowDialog();
        }

        /// <summary>
        /// Ask the user to confirm value propagation from the last value
        /// </summary>
        public static bool? DataEntryConfirmCopyForwardDialog(Window owner, string text, int imagesAffected, bool checkForZero)
        {
            text = string.IsNullOrEmpty(text) ? String.Empty : text.Trim();

            MessageBox messageBox = new MessageBox("Please confirm 'Copy Forward' for this field...", owner, MessageBoxButton.YesNo)
            {
                Message =
                {
                    Icon = MessageBoxImage.Question,
                    What = "Copy Forward is not undoable, and can overwrite existing values.",
                    Result = "If you select yes, this operation will:" + Environment.NewLine
                }
            };
            if (!checkForZero && string.IsNullOrEmpty(text))
            {
                messageBox.Message.Result += "\u2022 copy the (empty) value \u00AB" + text + "\u00BB in this field from here to the last file of your selected files.";
            }
            else
            {
                messageBox.Message.Result += "\u2022 copy the value \u00AB" + text + "\u00BB in this field from here to the last file of your selected files.";
            }
            messageBox.Message.Result += Environment.NewLine + "\u2022 over-write any existing data values in those fields"
                                      + Environment.NewLine + "\u2022 will affect " + imagesAffected + " files.";
            return messageBox.ShowDialog();
        }

        /// <summary>
        /// Ask the user to confirm value propagation to all selected files
        /// </summary>
        public static bool? DataEntryConfirmCopyCurrentValueToAllDialog(Window owner, String text, int filesAffected, bool checkForZero)
        {
            text = string.IsNullOrEmpty(text) ? String.Empty : text.Trim();

            MessageBox messageBox = new MessageBox("Please confirm 'Copy to All' for this field...", owner, MessageBoxButton.YesNo)
            {
                Message =
                {
                    Icon = MessageBoxImage.Question,
                    What = "Copy to All is not undoable, and can overwrite existing values.",
                    Result = "If you select yes, this operation will:" + Environment.NewLine
                }
            };
            messageBox.Message.Result += !checkForZero && string.IsNullOrEmpty(text)
                ? "\u2022 clear this field across all " + filesAffected + " of your selected files."
                : messageBox.Message.Result += "\u2022 set this field to \u00AB" + text + "\u00BB across all " + filesAffected + " of your selected files.";
            messageBox.Message.Result += Environment.NewLine + "\u2022 over-write any existing data values in those fields";
            return messageBox.ShowDialog();
        }

        /// <summary>
        /// Ask the user to confirm value propagation from the last value
        /// </summary>
        public static bool? DataEntryConfirmPropagateFromLastValueDialog(Window owner, String text, int imagesAffected)
        {
            text = string.IsNullOrEmpty(text) ? String.Empty : text.Trim();
            return new MessageBox("Please confirm 'Propagate to Here' for this field.", owner, MessageBoxButton.YesNo)
            {
                Message =
                {
                    Icon = MessageBoxImage.Question,
                    What = "Propagate to Here is not undoable, and can overwrite existing values.",
                    Reason = "\u2022 The last non-empty value \u00AB" + text + "\u00BB was seen " + imagesAffected + " files back."
                             + Environment.NewLine
                             + "\u2022 That field's value will be copied across all files between that file and this one of your selected files",
                    Result = "If you select yes: " + Environment.NewLine
                                                   + "\u2022 " + imagesAffected + " files will be affected."
                }
            }.ShowDialog();
        }
        #endregion

        #region MessageBox: MarkableCanvas Can't Open External PhotoViewer
        /// <summary>
        /// // Can't Open the External Photo Viewer. 
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="extension"></param>

        public static void MarkableCanvasCantOpenExternalPhotoViewerDialog(Window owner, string extension)
        {
            // Can't open the image file. Note that file must exist at this pint as we checked for that above.
            new MessageBox("Can't open a photo viewer.", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Error,
                    Reason = "You probably don't have a default program set up to display a photo viewer for " + extension + " files",
                    Solution = "Set up a photo viewer in your Windows Settings."
                               + Environment.NewLine
                               + "\u2022 go to 'Default apps', select 'Photo Viewer' and choose a desired photo viewer." + Environment.NewLine
                               + "\u2022 or right click on an " + extension + " file and set the default viewer that way"
                }
            }.ShowDialog();
        }
        #endregion

        #region MessageBox: No Updates Available
        public static void NoUpdatesAvailableDialog(Window owner, string applicationName, Version currentVersionNumber)
        {
            new MessageBox(String.Format("No updates to {0} are available.", applicationName), owner)
            {
                Message =
                {
                    Reason = $"You a running the latest version of {applicationName}, version: {currentVersionNumber}",
                    Icon = MessageBoxImage.Information
                }
            }.ShowDialog();
        }
        #endregion

        #region MessageBox: File Selection
        /// <summary>
        /// // No files were missing in the current selection
        /// </summary>
        public static void FileSelectionNoFilesAreMissingDialog(Window owner)
        {
            new MessageBox("No Files are Missing.", owner)
            {
                Message =
                {
                    Title = "No Files are Missing in the Current Selection.",
                    Icon = MessageBoxImage.Information,
                    What = "No files are missing in the current selection.",
                    Reason = "All files in the current selection were checked, and all are present. None were missing.",
                    Result = "No changes were made."
                }
            }.ShowDialog();
        }

        public static void FileSelectionResettngSelectionToAllFilesDialog(Window owner, FileSelectionEnum selection)
        {
            // These cases are reached when 
            // 1) datetime modifications result in no files matching a custom selection
            // 2) all files which match the selection get deleted
            MessageBox messageBox = new MessageBox("Resetting selection to All files (no files currently match the current selection)", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Information,
                    Result = "The 'All files' selection will be applied, where all files in your image set are displayed."
                }
            };

            switch (selection)
            {
                case FileSelectionEnum.Custom:
                    messageBox.Message.Problem = "No files currently match the custom selection so nothing can be shown.";
                    messageBox.Message.Reason = "No files match the criteria set in the current Custom selection.";
                    messageBox.Message.Hint = "Create a different custom selection and apply it view the matching files.";
                    break;
                case FileSelectionEnum.Folders:
                    messageBox.Message.Problem = "No files and/or image data were found for the selected folder.";
                    messageBox.Message.Reason = "Perhaps they were deleted during this session?";
                    messageBox.Message.Hint = "Try other folders or another selection. ";
                    break;
                case FileSelectionEnum.Missing:
                    // We should never invoke this, as its handled earlier.
                    messageBox.Message.Problem = "Missing files were previously selected. However, none of the files appear to be missing, so nothing can be shown.";
                    break;
                case FileSelectionEnum.MarkedForDeletion:
                    messageBox.Message.Problem = "Files marked for deletion were previously selected but no files are currently marked so nothing can be shown.";
                    messageBox.Message.Reason = "No files have their 'Delete?' field checked.";
                    messageBox.Message.Hint = "If you have files you think should be marked for deletion, check their 'Delete?' field and then reselect files marked for deletion.";
                    break;
                case FileSelectionEnum.All:
                default:
                    throw new NotSupportedException($"Unhandled selection {selection}.");
            }
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: MissingFilesNotFound / Missing Folders
        public static void MissingFileSearchNoMatchesFoundDialog(Window owner, string fileName)
        {
            string title = "Timelapse could not find any matches to " + fileName;
            new MessageBox(title, owner, MessageBoxButton.OK)
            {
                Message =
                {
                    What = "Timelapse tried to find the missing image with no success.",
                    Reason = "Timelapse searched the other folders in this image set, but could not find another file that: "
                             + Environment.NewLine
                             + " - was named " + fileName + ", and  " + Environment.NewLine
                             + " - was not already associated with another image entry.",
                    Hint = "If the original file was:"
                           + Environment.NewLine
                           + "\u2022 deleted, check your " + Constant.File.DeletedFilesFolder + " folder to see if its there." + Environment.NewLine
                           + "\u2022 moved outside of this image set, then you will have to find it and move it back in." + Environment.NewLine
                           + "\u2022 renamed, then you have to find it yourself and restore its original name." + Environment.NewLine + Environment.NewLine
                            + "Of course, you can just leave things as they are, or delete this image's data field if it has little value to you.",
                    Icon = MessageBoxImage.Question
                }
            }.ShowDialog();
        }

        public static void MissingFoldersInformationDialog(Window owner, int count)
        {
            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = null;

            string title = count + " of your folders could not be found";
            new MessageBox(title, owner, MessageBoxButton.OK)
            {
                Message =
                {
                    Problem = "Timelapse checked for the folders containing your image and video files, and noticed that " + count + " are missing.",
                    Reason = "These folders may have been moved, renamed, or deleted since Timelapse last recorded their location.",
                    Solution = "If you want to try to locate missing folders and files, select: "
                               + Environment.NewLine
                               + "\u2022 'Edit | Try to find missing folders...' to have Timelapse help locate those folders, or" + Environment.NewLine
                               + "\u2022 'Edit | Try to find this (and other) missing files...' to have Timelapse help locate one or more missing files in a particular folder.",
                    Hint = "Everything will still work as normal, except that a 'Missing file' image will be displayed instead of the actual image."
                           + Environment.NewLine
                           + "Searching for the missing folders is optional.",
                    Icon = MessageBoxImage.Exclamation
                }
            }.ShowDialog();
            Mouse.OverrideCursor = cursor;
        }
        #endregion

        #region MessageBox: ImageSetLoading
        /// <summary>
        /// No images were found in the root folder or subfolders, so there is nothing to do
        /// </summary>
        public static void ImageSetLoadingNoImagesOrVideosWereFoundDialog(Window owner, string selectedFolderPath)
        {
            new MessageBox("No images or videos were found", owner, MessageBoxButton.OK)
            {
                Message =
                {
                    Problem = "No images or videos were found in this folder or its subfolders:"
                              + Environment.NewLine
                              +  "\u2022 " + selectedFolderPath + Environment.NewLine,
                    Reason = "Neither the folder nor its sub-folders contain:"
                             + Environment.NewLine
                             + "\u2022 image files (ending in '.jpg') " + Environment.NewLine
                             + "\u2022 video files (ending in '.avi or .mp4')",
                    Solution = "Timelapse aborted the load operation." + Environment.NewLine,
                    Hint = "Locate your template in a folder containing (or whose subfolders contain) image or video files ." + Environment.NewLine,
                    Icon = MessageBoxImage.Exclamation
                }
            }.ShowDialog();
        }
        #endregion

        #region MessageBox: MenuFile
        public static void MenuFileRecognizersDataCouldNotBeReadDialog(Window owner)
        {
            new MessageBox("Recognition data not imported.", owner)
            {
                Message =
                {
                    Problem = "No recognition information was imported."
                              + Environment.NewLine
                              + "There were problems reading the recognition data in the json file.",
                    Reason = "Possible causes are:" + Environment.NewLine
                                                    + "\u2022 the file could not be opened, or" + Environment.NewLine
                                                    + "\u2022 the recognition data in the file is somehow corrupted",
                    Solution = "You may have to re-create the json file.",
                    Result = "Recognition information was not imported."
                }
            }.ShowDialog();
        }

        /// <summary>
        /// No matching folders in the DB and the recognizer file
        /// </summary>
        public static void MenuFileRecognitionDataNotImportedDialog(Window owner, string details)
        {
            new MessageBox("Recognition data not imported.", owner)
            {
                Message =
                {
                    Problem = "No recognition information was imported. The image file paths in the recognition file and the Timelapse"
                              + Environment.NewLine
                              + "database are all completely different. Thus no recognition information could be assigned to your images.",
                    Reason = "When the recognizer originally processed a folder (and its subfolders) containing your images,"
                             + Environment.NewLine
                             + "it recorded each image's location relative to that folder. If the subfolder structure differs from " + Environment.NewLine
                             + "that found in the Timelapse root folder, then the paths won't match." + Environment.NewLine
                             + "For example, if the recognizer was run on 'AllFolders/Camera1/' but your template and database is in 'Camera1/'," + Environment.NewLine
                             + "the folder paths won't match, since AllFolders/Camera1/ \u2260 Camera1/.",
                    Solution = "You may be able to repair the paths in the recognition file using a program provided by Microsoft:"
                               + Environment.NewLine
                               + "  http://aka.ms/cameratraps-detectormismatch",
                    Result = "Recognition information was not imported.",
                    Details = details
                }
            }.ShowDialog();
        }

        /// <summary>
        ///  Some folders missing - show which folder paths in the DB are not in the recognizer file
        /// </summary>
        public static void MenuFileRecognitionDataImportedOnlyForSomeFoldersDialog(Window owner, string details)
        {
            // Some folders missing - show which folder paths in the DB are not in the detector
            new MessageBox("Recognition data imported for only some of your folders.", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Information,
                    What = "The recognition file references images in only some of the sub-folders loaded in the Timelapse database file." + Environment.NewLine
                        + "We just want to bring it to your attention, in case this is not what you expected.",
                    Reason = "This normally happens if the imported file only includes data about a subset your folders.",
                    Hint = "You can check to see which images (if any) are missing recognition data by choosing" + Environment.NewLine
                        + "'Select|Custom Selection...' and checking the box titled 'Show all files with no recognition data'",
                    Details = details
                }
            }.ShowDialog();
        }

        /// <summary>
        /// Recognitions: successfully imported message
        /// </summary>
        public static void MenuFileRecognitionsSuccessfulyImportedDialog(Window owner, string details)
        {
            new MessageBox("Recognitions imported.", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Information,
                    Result = "Recognition data imported. You can select images matching particular recognitions by choosing 'Select|Custom Selection...'",
                    Hint = "You can also view which images (if any) are missing recognition data by choosing" + Environment.NewLine
                        + "'Select|Custom Selection...' and checking the box titled 'Show all files with no recognition data'",
                    Details = details
                }
            }.ShowDialog();
        }

        /// <summary>
        /// Recognitions: failed import message
        /// </summary>
        public static void MenuFileRecognitionsFailedImportedDialog(Window owner, RecognizerImportResultEnum importError)
        {
            new MessageBox("Could not import the recognition data.", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Information,
                    Reason = (RecognizerImportResultEnum.JsonFileCouldNotBeRead == importError)
                        ? "The Json recognition file could not be read"
                        : "There were problems trying to import the recogntion data. We are not sure why this happened.",
                    Result = "Recognition data was not imported"
                }
            }.ShowDialog();
        }

        /// <summary>
        /// Warn the user that there no existing files match the recognition data
        /// </summary>
        /// <returns>The selected path, otherwise null </returns>
        public static bool RecognizerNoMatchToExistingFiles(Window owner, string samplePath)
        {
            MessageBox messageBox = new MessageBox("None of the recognition file paths match your existing files", owner, MessageBoxButton.OKCancel)
            {
                Message =
                    {
                        What = "None of the recognition file paths match your existing files." + Environment.NewLine
                            +  "Is this intensional?",
                        Reason = "It's likely that there is a mismatch between the recognizer paths and your actual file paths." + Environment.NewLine
                            + "Somewhat less likely is that you haven't yet moved your images over.",
                        Solution = "You may want to do one of the following." + Environment.NewLine
                                                                              + "\u2022 'Okay' to import the recognitions anyways" + Environment.NewLine
                                                                              + "\u2022 'Cancel' to stop importing so you can check to see what is going on.",
                        Icon = MessageBoxImage.Question
                    }
            };
            if (String.IsNullOrWhiteSpace(samplePath))
            {
                messageBox.Message.Hint = "The problem is that the recognition file contains no files!" + Environment.NewLine
                + "You probably want to Cancel";
            }
            else
            {
                messageBox.Message.Hint = "An example file path found in the recogntion file is: " + Environment.NewLine
                + "\u2022 " + samplePath + Environment.NewLine
                + "Examine this to see if you think this file path should point to a different location.";
            }
            return true == messageBox.ShowDialog();
        }


        /// <summary>
        /// Export data for this image set as a.csv file, but confirm, as only a subset will be exported since a selection is active
        /// </summary>
        public static bool? MenuFileExportCSVOnSelectionDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Exporting to a .csv file on a selected view...", owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    What = "Only a subset of your data will be exported to the .csv file.",
                    Reason = "As your selection (in the Selection menu) is not set to view 'All', "
                             + "only data for these selected files will be exported. ",
                    Solution = "If you want to export just this subset, then " + Environment.NewLine
                        + "\u2022 click Okay" + Environment.NewLine + Environment.NewLine
                        + "If you want to export data for all your files, then " + Environment.NewLine
                        + "\u2022 click Cancel," + Environment.NewLine
                        + "\u2022 select 'All Files' in the Selection menu, " + Environment.NewLine
                        + "\u2022 retry exporting your data as a .csv file.",
                    Hint = "Select 'Don't show this message again' to hide this message. You can unhide it later via the Options|Show or hide... menu.",
                    Icon = MessageBoxImage.Warning
                },
                DontShowAgain =
                {
                    Visibility = Visibility.Visible
                }
            };

            bool? exportCsv = messageBox.ShowDialog();
            if (messageBox.DontShowAgain.IsChecked.HasValue)
            {
                GlobalReferences.TimelapseState.SuppressSelectedCsvExportPrompt = messageBox.DontShowAgain.IsChecked.Value;
            }
            return exportCsv;
        }

        /// <summary>
        /// Cant write the spreadsheet file
        /// </summary>
        public static void MenuFileCantWriteSpreadsheetFileDialog(Window owner, string csvFilePath, string exceptionName, string exceptionMessage)
        {
            new MessageBox("Can't write the spreadsheet file.", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Error,
                    Problem = "The following file can't be written: " + csvFilePath,
                    Reason = "You may already have it open in Excel or another application.",
                    Solution = "If the file is open in another application, close it and try again.",
                    Hint = $"{exceptionName}: {exceptionMessage}"
                }
            }.ShowDialog();
        }

        /// <summary>
        /// Cant open the file using Excel
        /// </summary>
        public static void MenuFileCantOpenExcelDialog(Window owner, string csvFilePath)
        {
            new MessageBox("Can't open Excel.", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Error,
                    Problem = "Excel could not be opened to display " + csvFilePath,
                    Solution = "Try again, or just manually start Excel and open the .csv file "
                }
            }.ShowDialog();
        }

        /// <summary>
        /// Give the user some feedback about the CSV export operation
        /// </summary>
        public static void MenuFileCSVDataExportedDialog(Window owner, string csvFileName)
        {
            // since the exported file isn't shown give the user some feedback about the export operation
            MessageBox csvExportInformation = new MessageBox("Data exported.", owner)
            {
                Message =
                {
                    What = "The selected files were exported to " + csvFileName,
                    Result =
                        $"This file is overwritten every time you export it (backups can be found in the {Constant.File.BackupFolder} folder).",
                    Hint = "\u2022 You can open this file with most spreadsheet programs, such as Excel." + Environment.NewLine
                        + "\u2022 If you make changes in the spreadsheet file, you will need to import it to see those changes." + Environment.NewLine
                        + "\u2022 You can change the Date and Time formats by selecting the Options|Preferences menu." + Environment.NewLine
                        + "Select 'Don't show this message again' to hide this message. You can unhide it later via the Options|Show or hide... menu.",
                    Icon = MessageBoxImage.Information
                },
                DontShowAgain =
                {
                    Visibility = Visibility.Visible
                }
            };

            bool? result = csvExportInformation.ShowDialog();
            if (result.HasValue && result.Value && csvExportInformation.DontShowAgain.IsChecked.HasValue)
            {
                GlobalReferences.TimelapseState.SuppressCsvExportDialog = csvExportInformation.DontShowAgain.IsChecked.Value;
            }
        }

        /// <summary>
        /// Tell the user how importing CSV files work. Give them the opportunity to abort.
        /// </summary>
        public static bool? MenuFileHowImportingCSVWorksDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("How importing .csv data works", owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    What = "Importing data from a .csv (comma separated value) file follows the rules below.",
                    Reason = "The first row in the CSV file must comprise column headers, where:" + Environment.NewLine
                        + "\u2022 'File' must be included." + Environment.NewLine
                        + "\u2022 'RelativePath' must be included if any of your images are in subfolders" + Environment.NewLine
                        + "\u2022 remaining headers should generally match your template's DataLabels" + Environment.NewLine
                        + "Headers can be a subset of your template's DataLabels." + Environment.NewLine + Environment.NewLine
                        + "Subsequent rows define the data for each file, where it must match the Header type:" + Environment.NewLine
                        + "\u2022 'File' data should match the name of the file you want to update." + Environment.NewLine
                        + "\u2022 'RelativePath' data should match the sub-folder path containing that file, if any" + Environment.NewLine
                        + "\u2022 'Counter' data must be blank, 0, or a positive integer. " + Environment.NewLine
                        + "\u2022 'DateTime', 'Date' and 'Time' data must follow the specific date/time formats (see File|Export data...). " + Environment.NewLine
                        + "\u2022 'Flag' and 'DeleteFlag' data must be 'true' or 'false'." + Environment.NewLine
                        + "\u2022 'FixedChoice' data should exactly match a corresponding list item defined in the template, or empty. " + Environment.NewLine
                        + "\u2022 'Folder' and 'ImageQuality' columns, if included, are skipped over.",
                    Result = "Database values will be updated only for matching RelativePath/File entries. Non-matching entries are ignored.",
                    Hint = "Warnings will be generated for non-matching CSV headers, which you can then fix." + Environment.NewLine 
                           +"Select 'Don't show this message again' to hide this message. You can unhide it later via the Options|Show or hide... menu.",
                    Icon = MessageBoxImage.Warning
                },
                DontShowAgain =
                {
                    Visibility = Visibility.Visible
                }
            };
            bool? result = messageBox.ShowDialog();
            if (messageBox.DontShowAgain.IsChecked.HasValue)
            {
                GlobalReferences.TimelapseState.SuppressCsvImportPrompt = messageBox.DontShowAgain.IsChecked.Value;
            }
            return result;
        }

        /// <summary>
        /// Can't import CSV File
        /// </summary>
        public static void MenuFileCantImportCSVFileDialog(Window owner, string csvFileName, List<string> resultAndImportErrors)
        {
            MessageBox messageBox = new MessageBox("Can't import the .csv file.", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Error,
                    Problem = $"The file {csvFileName} could not be read.",
                    Reason = "The .csv file is not compatible with the Timelapse template defining the current image set.",
                    Hint = "Timelapse checks the following when importing the .csv file:" + Environment.NewLine
                        + "\u2022 The first row is a header whose column names match the data labels in the .tdb template file" + Environment.NewLine
                        + "\u2022 Counter data values are numbers or blanks." + Environment.NewLine
                        + "\u2022 Flag and DeleteFlag values are either 'True' or 'False'." + Environment.NewLine
                        + "\u2022 Choice values are in that field's Choice list, defined in the template." + Environment.NewLine + Environment.NewLine

                        + "While Timelapse will do the best it can to update your fields: " + Environment.NewLine
                        + "\u2022 the csv row is skipped if its RelativePath/File location do not match a file in the Timelapse database ." + Environment.NewLine
                        + "\u2022 the csv row's Date/Time is updated only if it is in the expected format (see Timelapse Reference Guide).",
                    Result = "Importing of data from the CSV file was aborted. No changes were made."
                }
            };

            if (resultAndImportErrors != null)
            {
                messageBox.Message.Solution += "Change your .csv file to fix the errors below and try again.";
                foreach (string importError in resultAndImportErrors)
                {
                    string prefix = (importError[0] == '-') ? "   " : "\u2022 ";
                    messageBox.Message.Solution += Environment.NewLine + prefix + importError;
                }
            }
            messageBox.ShowDialog();
        }

        /// <summary>
        /// CSV file imported
        /// </summary>
        public static void MenuFileCSVFileImportedDialog(Window owner, string csvFileName, List<string> warnings)
        {
            MessageBox messageBox = new MessageBox("CSV file imported", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Information,
                    What = $"The file {csvFileName} was successfully imported.",
                    Hint = "\u2022 Check your data. If it is not what you expect, restore your data by using latest backup file in " + Constant.File.BackupFolder + "."
                }
            };
            if (warnings.Count != 0)
            {
                messageBox.Message.Result = "However, here are some warnings that you may want to check.";
                foreach (string warning in warnings)
                {
                    string prefix = (warning[0] == '-') ? "   " : "\u2022 ";
                    messageBox.Message.Result += Environment.NewLine + prefix + warning;
                }
            }
            messageBox.ShowDialog();

        }

        /// <summary>
        /// Can't import the .csv file
        /// </summary>
        public static void MenuFileCantImportCSVFileDialog(Window owner, string csvFileName, string exceptionMessage)
        {
            new MessageBox("Can't import the .csv file.", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Error,
                    Problem = String.Format("The file {0} could not be opened.", csvFileName),
                    Reason = "Most likely the file is open in another program. The technical reason is:" + Environment.NewLine
                        + exceptionMessage,
                    Solution = "If the file is open in another program, close it.",
                    Result = "Importing of data from the CSV file was aborted. No changes were made.",
                    Hint = "Is the file open in Excel?"
                }
            }.ShowDialog();
        }

        /// <summary>
        /// Can't export the currently displayed image as a file
        /// </summary>
        public static void MenuFileCantExportCurrentImageDialog(Window owner)
        {
            new MessageBox("Can't export this file!", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Error,
                    Problem = "Timelapse can't export a copy of the current image or video file.",
                    Reason = "It is likely a corrupted or missing file.",
                    Solution = "Make sure you have navigated to, and are displaying, a valid file before you try to export a copy of it."
                }
            }.ShowDialog();
        }

        /// <summary>
        /// Show a message that explains how merging databases works and its constraints. Give the user an opportunity to abort
        /// </summary>
        public static bool? MenuFileMergeDatabasesExplainedDialog(Window owner)
        {

            MessageBox messageBox = new MessageBox("Merge Databases.", owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                     Icon = MessageBoxImage.Question,
                     Title = "Merge Databases Explained.",
                     What = "Merging databases works as follows. Timelapse will:"
                          + Environment.NewLine
                          + "\u2022 ask you to locate a root folder containing a template (a.tdb file)," + Environment.NewLine
                          + $"\u2022 create a new database (.ddb) file in that folder, called {Constant.File.MergedFileName},{Environment.NewLine}"
                          + "\u2022 search for other database (.ddb) files in that folder's sub-folders, " + Environment.NewLine
                          + "\u2022 try to merge all data found in those found databases into the new database.",
                     Details = "\u2022 All databases must be based on the same template, otherwise the merge will fail." + Environment.NewLine
                         +"\u2022 Databases found in the Backup folders are ignored." + Environment.NewLine
                         + "\u2022 Detections and Classifications (if any) are merged; categories are taken from the first database found with detections." + Environment.NewLine
                         + "\u2022 The merged database is independent of the found databases: updates will not propagate between them." + Environment.NewLine
                         + "\u2022 The merged database is a normal Timelapse database, which you can open and use as expected.",
                     Hint = "Press Ok to continue with the merge, otherwise Cancel."
                },
                DontShowAgain =
                {
                    Visibility = Visibility.Visible
                }
            };
            messageBox.ShowDialog();

            if (messageBox.DontShowAgain.IsChecked.HasValue)
            {
                GlobalReferences.TimelapseState.SuppressMergeDatabasesPrompt = messageBox.DontShowAgain.IsChecked.Value;
            }
            return messageBox.DialogResult;
        }

        /// <summary>
        /// Merge databases: Show errors and/or warnings, if any.
        /// </summary>
        public static void MenuFileMergeDatabasesErrorsAndWarningsDialog(Window owner, ErrorsAndWarnings errorMessages)
        {
            if (errorMessages == null)
            {
                return;
            }
            MessageBox messageBox = new MessageBox("Merge Databases Results.", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Error
                }
            };

            if (errorMessages.Errors.Count != 0)
            {
                messageBox.Message.Title = "Merge Databases Failed.";
                messageBox.Message.What = "The merged database could not be created for the following reasons:";
            }
            else if (errorMessages.Warnings.Count != 0)
            {
                messageBox.Message.Title = "Merge Databases Left Out Some Files.";
                messageBox.Message.What = "The merged database left out some files for the following reasons:";
            }

            if (errorMessages.Errors.Count != 0)
            {
                messageBox.Message.What += $"{Environment.NewLine}{Environment.NewLine}Errors:";
                foreach (string error in errorMessages.Errors)
                {
                    messageBox.Message.What += $"{Environment.NewLine}\u2022 {error},";
                }
            }
            if (errorMessages.Warnings.Count != 0)
            {
                messageBox.Message.What += $"{Environment.NewLine}{Environment.NewLine}Warnings:";
            }
            foreach (string warning in errorMessages.Warnings)
            {
                messageBox.Message.What += $"{Environment.NewLine}\u2022 {warning},";
            }
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: MenuEdit
        /// <summary>
        /// Tell the user how duplicates work, including showing a problem statement if the sort order isn't optimal. Give them the opportunity to abort.
        /// THe various flags determine whether to show or hide the duplicate information, and how to deal with the DontShowAgain checkbox, as we always want
        /// the problem message to appear regardless of the state of DontShowAgain.
        /// </summary>
        public static bool? MenuEditHowDuplicatesWorkDialog(Window owner, bool sortTermsOKForDuplicateOrdering, bool showProblemDescriptionOnly)
        {
            MessageBox messageBox = new MessageBox("Duplicate this record - What it is for, and caveats", owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    Icon = MessageBoxImage.Information
                },
                DontShowAgain =
                {
                    Visibility = Visibility.Visible
                }
            };

            if (false == sortTermsOKForDuplicateOrdering)
            {
                messageBox.Message.Problem = "'Duplicate this record' works best with Sorting set to either 'Sort | By relative Path + DateTime (default)', or 'Sort |by DateTime'. Otherwise, duplicate records may not appear next to each other.";
                if (showProblemDescriptionOnly)
                {
                    messageBox.Message.Hint += "You may want to change your sort order before proceeding.";
                }
                else
                {
                    messageBox.Message.Problem += Environment.NewLine + "\u2022 You may want to change your sort order before proceeding.";
                }
            }

            if (showProblemDescriptionOnly == false)
            {
                messageBox.Message.What += "Duplicating a record will create a new copy of the current record populated with its default values." + Environment.NewLine
                    + "Duplicates provide you with the ability to have the same field describe multiple things in your image." + Environment.NewLine + Environment.NewLine
                    + "For example, let's say you have a Choice box called 'Species' used to identify animals in your image." + Environment.NewLine
                    + "If more than one animal is in the image, you can use the original image to record the first species (e.g., Deer)" + Environment.NewLine
                    + "and then use one (or more) dupicate records to record the other species that are present (e.g., Elk)" + Environment.NewLine + Environment.NewLine
                    + "If you export your data to a CSV file, each duplicates will appear in its own row ";

                messageBox.Message.Hint = "Duplicates come with several caveats." + Environment.NewLine
                    + "\u2022 Use 'Sort | Relative Path + Date Time (default)' to ensure that duplicates appear in sequence." + Environment.NewLine
                    + "\u2022 Duplicates can only be created in the main view, not in the overview." + Environment.NewLine
                    + "\u2022 Duplicates in the exported CSV file are identifiable as rows with the same relative path and file name.";
            }
            bool? result = messageBox.ShowDialog();
            if (messageBox.DontShowAgain.IsChecked.HasValue && showProblemDescriptionOnly == false)
            {
                GlobalReferences.TimelapseState.SuppressHowDuplicatesWork = messageBox.DontShowAgain.IsChecked.Value;
            }
            return result;
        }

        public static void MenuEditCouldNotImportQuickPasteEntriesDialog(Window owner)
        {
            new MessageBox("Could not import QuickPaste entries", owner)
            {
                Message =
                {
                    Problem = "Timelapse could not find any QuickPaste entries in the selected database",
                    Reason = "When an analyst creates QuickPaste entries, those entries are stored in the database file " + Environment.NewLine
                        + "associated with the image set being analyzed. Since none where found, " + Environment.NewLine
                        + "its likely that no one had created any quickpaste entries when analyzing that image set.",
                    Hint = "Perhaps they are in a different database?",
                    Icon = MessageBoxImage.Information
                }
            }.ShowDialog();
        }
        /// <summary>
        /// There are no displayable images, and thus no metadata to choose from
        /// </summary>
        public static void MenuEditPopulateDataFieldWithMetadataDialog(Window owner)
        {
            new MessageBox("Populate a data field with image metadata of your choosing.", owner)
            {
                Message =
                {
                    Problem = "Timelapse can't extract any metadata, as the currently displayed image or video is missing or corrupted." + Environment.NewLine,
                    Reason = "Timelapse tries to examines the currently displayed image or video for its metadata.",
                    Hint = "Navigate to a displayable image or video, and try again.",
                    Icon = MessageBoxImage.Error
                }
            }.ShowDialog();
        }

        public static void MenuEditRereadDateTimesFromMetadataDialog(Window owner)
        {
            new MessageBox("Re-read date and times from a metadata field of your choosing.", owner)
            {
                Message =
                {
                    Problem = "Timelapse can't extract any metadata, as the currently displayed image or video is missing or corrupted." + Environment.NewLine,
                    Reason = "Timelapse tries to examines the currently displayed image or video for its metadata.",
                    Hint = "Navigate to a displayable image or video, and try again.",
                    Icon = MessageBoxImage.Error
                }
            }.ShowDialog();
        }

        public static void MenuEditNoFilesMarkedForDeletionDialog(Window owner)
        {
            new MessageBox("No files are marked for deletion", owner)
            {
                Message =
                {
                    Problem = "You are trying to delete files marked for deletion, but no files have their 'Delete?' field checked.",
                    Hint = "If you have files that you think should be deleted, check their Delete? field.",
                    Icon = MessageBoxImage.Information
                }
            }.ShowDialog();
        }

        public static void MenuEditNoFoldersAreMissing(Window owner)
        {
            new MessageBox("No folders appear to be missing", owner)
            {
                Message =
                {
                    What = "You asked to to find any missing folders, but none appear to be missing.",
                    Hint = "You don't normally have to do this check yourself, as a check for missing folders is done automatically whenever you start Timelapse.",
                    Icon = MessageBoxImage.Information
                }
            }.ShowDialog();
        }
        #endregion

        #region MessageBox: MenuOptions
        public static void MenuOptionsCantPopulateDataFieldWithEpisodeAsNoFilesDialog(Window owner)
        {
            new MessageBox("Cannot populate a field with Episode data", owner)
            {
                Message =
                {
                    Problem = "Timelapse cannot currently populate any fields with Episode data." + Environment.NewLine,
                    Reason = "There are no files in the current selection.",
                    Hint = "Expand the current selection, or add some images or videos. Then try again.",
                    Icon = MessageBoxImage.Error
                }
            }.ShowDialog();
        }

        public static void MenuOptionsCantPopulateDataFieldWithEpisodeAsNoNoteFields(Window owner)
        {
            new MessageBox("Cannot populate a field with Episode data", owner)
            {
                Message =
                {
                    Problem = "Timelapse cannot currently populate any fields with Episode data." + Environment.NewLine,
                    Reason = "Episode data would be put in a Note field, but none of your fields are Notes.",
                    Hint = "Modify your template .tdb file to include a Note field using the Timelapse Template Editor." + Environment.NewLine,
                    Icon = MessageBoxImage.Error
                }
            }.ShowDialog();
        }

        public static bool MenuOptionsCantPopulateDataFieldWithEpisodeAsSortIsWrong(Window owner, bool searchTermsOk, bool sortTermsOk)
        {
            MessageBox messageBox = new MessageBox("You may not want to populate this field with Episode data", owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    Problem = "You may not want to populate this field with Episode data.",
                    Solution = "Select:" + Environment.NewLine
                                         + "- Okay to populate this field anyways, or " + Environment.NewLine
                                         + "- Cancel to abort populating this field with episode data",
                    Icon = MessageBoxImage.Warning
                }
            };

            if (!searchTermsOk)
            {
                if (!sortTermsOk)
                {
                    messageBox.Message.Reason += "1. ";
                }
                messageBox.Message.Reason += "Your current file selection includes search terms that may omit files in an Episode.";
                messageBox.Message.Hint += "Use the Select menu to select either:" + Environment.NewLine
                    + " - All files, or " + Environment.NewLine
                    + " - All files in a folder and its subfolders";
                if (!sortTermsOk)
                {
                    messageBox.Message.Reason += Environment.NewLine;
                    messageBox.Message.Hint += Environment.NewLine;
                }
            }

            if (sortTermsOk) return messageBox.ShowDialog() == true;
            if (!searchTermsOk)
            {
                messageBox.Message.Reason += "2. ";
            }
            messageBox.Message.Reason += "Your files should be sorted in ascending date order for this to make sense.";
            messageBox.Message.Hint += "Use the Sort menu to sort either by:" + Environment.NewLine
                + " - RelativePath then DateTime (both in ascending order), or " + Environment.NewLine
                + " - DateTime only  (in ascending order)";
            return messageBox.ShowDialog() == true;
        }

        public static void MenuOptionsCantPopulateDataFieldWithEpisodeAsSortIsWrongOriginal(Window owner, bool searchTermsOk, bool sortTermsOk)
        {
            MessageBox messageBox = new MessageBox("Cannot populate a field with Episode data", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Error,
                    Problem = "Timelapse cannot currently populate any fields with Episode data." + Environment.NewLine
                }
            };
            if (!searchTermsOk)
            {
                if (!sortTermsOk)
                {
                    messageBox.Message.Reason += "1. ";
                }
                messageBox.Message.Reason += "Your current file selection includes search terms that may omit files in an Episode.";
                messageBox.Message.Hint += "Use the Select menu to select either:" + Environment.NewLine
                                        + " - All files, or " + Environment.NewLine
                                        + " - All files in a folder and its subfolders";
                if (!sortTermsOk)
                {
                    messageBox.Message.Reason += Environment.NewLine;
                    messageBox.Message.Hint += Environment.NewLine;
                }
            }

            if (!sortTermsOk)
            {
                if (!searchTermsOk)
                {
                    messageBox.Message.Reason += "2. ";
                }
                messageBox.Message.Reason += "Your files must be sorted in ascending date order for this to make sense.";
                messageBox.Message.Hint += "Use the Sort menu to sort either by:" + Environment.NewLine
                                        + " - RelativePath then DateTime (both in ascending order), or " + Environment.NewLine
                                        + " - DateTime only  (in ascending order)";
            }
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: related to DateTime
        public static void DateTimeNewTimeShouldBeLaterThanEarlierTimeDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Your new time has to be later than the earliest time", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Exclamation,
                    Problem = "Your new time has to be later than the earliest time   ",
                    Reason = "Even the slowest clock gains some time.",
                    Solution = "The date/time was unchanged from where you last left it.",
                    Hint = "The image on the left shows the earliest time recorded for images in this filtered view  shown over the left image"
                }
            };
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: related to Arguments to start a particular template or to constrain to a particular relative path
        // Tell the user that Timelapse is currently restricted to the folder designated by a particulare relative path
        public static void ArgumentRelativePathDialog(Window owner, string folderName)
        {
            string title = "Timelapse is currently restricted to the folder: '" + folderName + "'";
            new MessageBox(title, owner, MessageBoxButton.OK)
            {
                Message =
                {
                    What = title + Environment.NewLine
                                 + "This means that:" + Environment.NewLine
                                 + "\u2022 you will only be able to view and analyze files in that folder and its subfolders" + Environment.NewLine
                                 + "\u2022 any reference by Timelapse to 'All files' means 'All files in the folder: " + folderName + "'" + Environment.NewLine
                                 + "\u2022 to avoid confusion, you will not be able to open a different image set in this session",
                    Reason = "Timelapse was started with the instruction to restrict itself to the folder: '" + folderName + "'" + Environment.NewLine
                             + "This is usually done to narrow analysis to a particular subset of files of interest",
                    Icon = MessageBoxImage.Information
                }
            }.ShowDialog();
        }

        // Tell the user that Timelapse could not open the template specified in the argument
        public static void ArgumentTemplatePathDialog(Window owner, string fileName, string relativePathArgument)
        {
            string title = "Timelapse could not open the template";
            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OK)
            {
                Message =
                {
                    Icon = MessageBoxImage.Information,
                    What = title + Environment.NewLine
                                 + "     '" + fileName + "'" + Environment.NewLine + Environment.NewLine
                                 + "Consequently," + Environment.NewLine
                                 + "\u2022 the instruction to use that template is ignored.",
                    Reason = "Timelapse was started with instructions to open the template indicated above" + Environment.NewLine
                }
            };

            if (!String.IsNullOrWhiteSpace(relativePathArgument))
            {
                messageBox.Message.What += Environment.NewLine + "\u2022 the additional instruction to limit analysis to the subfolder " + "'" + relativePathArgument + "'" + " is also ignored ";
            }
            if (!String.IsNullOrWhiteSpace(relativePathArgument))
            {
                messageBox.Message.Reason += "and to limit analysis to a particular subfolder." + Environment.NewLine;
            }
            messageBox.Message.Reason += "However, that template either does not exist or could not be accessed.";
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: related to selecting Episodes in the custom select
        // There are two version of this warning dialog
        // - If the flag is true, the text informs that Timelapse could not find a data field in the expected episode format
        // - If the flag is false, the text informs that the currently selected data field is not in the expected format
        public static void CustomSelectEpisodeDataLabelProblem(Window owner)
        {
            string title = "Timelapse cannot enable searches for Episode-related files";
            new MessageBox(title, owner, MessageBoxButton.OK)
            {
                Message =
                {
                    Icon = MessageBoxImage.Warning,
                    What = "For Timelapse to expand the search to include episodes, you must have a data field populated" + Environment.NewLine
                        + "with your files' episode data, where the episode data is in the expected format." + Environment.NewLine
                        + "None of your data fields, at least for the current file, includes the expected episode data.",
                    Reason = "When you choose this option, Timelapse searches for episodes having at least one file " + Environment.NewLine
                        + "matching your search criteria. If so, all files contained in those episodes are then displayed." + Environment.NewLine + Environment.NewLine
                        + "For this to work properly, one of your data fields must have been filled in using " + Environment.NewLine
                        + "Edit | Populate a field with episode data..., where:" + Environment.NewLine
                        + " \u2022 the episode format includes the episode sequence number as its prefix, e.g. 25:1|3," + Environment.NewLine
                        + " \u2022 all currently selected files were populated with Episode information at the same time.",
                    Hint = "See the Timelapse Reference Guide about how this operation works, what is required, and what it is for."
                }
            }.ShowDialog();
        }
        #endregion

        #region MessageBox: opening messages when Timelapse is started
        /// <summary>
        /// Give the user various opening mesages
        /// </summary>
        public static void OpeningMessage(Window owner)
        {
            MessageBox openingMessage = new MessageBox("Opening Message ...", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Information,
                    What = "This update ..." + Environment.NewLine
                                             + "stuff" + Environment.NewLine
                                             + "more stuff",
                    Reason = "A reason" + Environment.NewLine
                                        + "\u2022 Point, " + Environment.NewLine
                                        + "\u2022 Another point"
                },
                DontShowAgain =
                {
                    Visibility = Visibility.Visible
                }
            };
            bool? result = openingMessage.ShowDialog();
            if (result.HasValue && result.Value && openingMessage.DontShowAgain.IsChecked.HasValue)
            {
                GlobalReferences.TimelapseState.SuppressOpeningMessageDialog = openingMessage.DontShowAgain.IsChecked.Value;
            }
        }
        #endregion

        #region Dialogs dealing with Timelapse being opened in a viewonly state
        /// Give the user various opening mesages
        public static void OpeningMessageViewOnly(Window owner)
        {
            new MessageBox("You are using the Timelapse 'View only' version ...", owner)
            {
                Message =
                {
                    What = "You started the view-only version of Timelapse. " + Environment.NewLine
                        + "\u2022 You can open and view existing images, videos and any previously entered data." + Environment.NewLine
                        + "\u2022 You will not be able to edit or alter that data, or create a new image set." + Environment.NewLine,
                    Reason = "This Timelapse version is handy if you only want to view an image set and its data," + Environment.NewLine
                        + "as it ensures that no accidental changes to the data will be made.",
                    Hint = "If you want to edit data, then you should start the normal Timelapse program.",
                    Details = "Menu items that alter data are hidden from view, and data entry controls are disabled.",
                    Icon = MessageBoxImage.Information
                }
            }.ShowDialog();
        }

        public static void ViewOnlySoDatabaseCannotBeCreated(Window owner)
        {
            new MessageBox("You are using the Timelapse 'View only' version. ", owner)
            {
                Message =
                {
                    What = "Creating a new image set is not allowed when Timelapse is started as view-only. " + Environment.NewLine,
                    Reason = "You tried to open a template on an image set that has no data associated with it." + Environment.NewLine
                        + "Timelapse normally looks for images in this folder, and creates a database holding data for each image." + Environment.NewLine
                        + "In view-only mode, creating a new database is not permitted",
                    Hint = "If you want to load these images for the first time, then you should start the normal Timelapse program.",
                    Icon = MessageBoxImage.Warning
                }
            }.ShowDialog();
        }
        #endregion

        #region MessageBox: ddb file opened with an older version of Timelapse than recorded in it
        public static bool? DatabaseFileOpenedWithOlderVersionOfTimelapse(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = null;
            // notify the user the template couldn't be loaded rather than silently doing nothing
            MessageBox messageBox = new MessageBox("You are opening your image set with an older Timelapse version ", owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    What = "You are opening your image set with an older version of Timelapse." + Environment.NewLine
                        + "You previously used a later version of Timelapse to open this image set." + Environment.NewLine
                        + "This is just a warning, as its rarely a problem.",
                    Reason = "Its best to use the latest Timelapse version to minimize any incompatabilities with older versions.",
                    Solution = "Click:" + Environment.NewLine
                                + "\u2022 " + "Ok to keep going. It will likely work fine anyways." + Environment.NewLine
                                + "\u2022 " + "Cancel to abort. You can then download the latest version from the Timelapse web site.",
                    Icon = MessageBoxImage.Warning,
                    Hint = "Select 'Don't show this message again' to hide this warning. " + Environment.NewLine
                         + "You can unhide it later via the 'Options|Show or hide...' menu."
                },
                DontShowAgain =
                {
                    Visibility = Visibility.Visible
                }
            };
            bool? result = messageBox.ShowDialog();
            if (messageBox.DontShowAgain.IsChecked.HasValue)
            {
                GlobalReferences.TimelapseState.SuppressOpeningWithOlderTimelapseVersionDialog = messageBox.DontShowAgain.IsChecked.Value;
            }
            Mouse.OverrideCursor = cursor;
            return result;
        }
        #endregion

        #region DialogIsFileValid checks for valid database file and displays appropriate dialog if it isn't
        public static bool DialogIsFileValid(Window owner, string filePath)
        {
            switch (Util.FilesFolders.QuickCheckDatabaseFile(filePath))
            {
                case DatabaseFileErrorsEnum.Ok:
                    return true;
                case DatabaseFileErrorsEnum.OkButOpenedWithAnOlderTimelapseVersion:
                    if (GlobalReferences.MainWindow.State.SuppressOpeningWithOlderTimelapseVersionDialog == false)
                    {
                        return true == Dialogs.DatabaseFileOpenedWithOlderVersionOfTimelapse(owner);
                    }
                    return true;
                case DatabaseFileErrorsEnum.PreVersion2300:
                case DatabaseFileErrorsEnum.UTCOffsetTypeExistsInUpgradedVersion:
                    Mouse.OverrideCursor = null;
                    DialogUpgradeFiles.DialogUpgradeFilesAndFolders dialogUpdateFiles =
                        new DialogUpgradeFiles.DialogUpgradeFilesAndFolders(owner, Path.GetDirectoryName(filePath), VersionChecks.GetTimelapseCurrentVersionNumber().ToString());
                    dialogUpdateFiles.ShowDialog();
                    return false;
                case DatabaseFileErrorsEnum.PathTooLong:
                    Mouse.OverrideCursor = null;
                    Dialogs.TemplatePathTooLongDialog(owner, filePath);
                    return false;
                case DatabaseFileErrorsEnum.FileInRootDriveFolder:
                    Mouse.OverrideCursor = null;
                    Dialog.Dialogs.TemplateInDisallowedFolder(owner, true, Path.GetDirectoryName(filePath));
                    return false;
                case DatabaseFileErrorsEnum.FileInSystemOrHiddenFolder:
                    Mouse.OverrideCursor = null;
                    Dialog.Dialogs.TemplateInDisallowedFolder(owner, false, Path.GetDirectoryName(filePath));
                    return false;
                case DatabaseFileErrorsEnum.InvalidDatabase:
                    Mouse.OverrideCursor = null;
                    if (Path.GetExtension(filePath) == Constant.File.TemplateDatabaseFileExtension)
                    {
                        Dialogs.TemplateFileNotLoadedAsCorruptDialog(owner, filePath);
                    }
                    else
                    {
                        Dialogs.DatabaseFileNotLoadedAsCorruptDialog(owner, filePath, false);
                    }
                    return false;
                case DatabaseFileErrorsEnum.NotATimelapseFile:
                    Mouse.OverrideCursor = null;
                    Dialogs.FileNotATimelapseFile(owner, filePath);
                    return false;
                case DatabaseFileErrorsEnum.DoesNotExist:
                case DatabaseFileErrorsEnum.TemplateElementsDiffer:
                case DatabaseFileErrorsEnum.TemplateElementsSameButOrderDifferent:
                case DatabaseFileErrorsEnum.DetectionCategoriesDiffers:
                case DatabaseFileErrorsEnum.ClassificationDictionaryDiffers:
                default:
                    return true;
            }
        }
        #endregion

        #region Testing messages for development
        public static void RandomMessage(Window owner, string message)
        {
            // since the exported file isn't shown give the user some feedback about the export operation
            MessageBox openingMessage = new MessageBox("Timelapse message...", owner)
            {
                Message =
                {
                    What = message,
                    Icon = MessageBoxImage.Information
                },
                DontShowAgain =
                {
                    Visibility = Visibility.Visible
                }
            };

            bool? result = openingMessage.ShowDialog();
            if (result.HasValue && result.Value && openingMessage.DontShowAgain.IsChecked.HasValue)
            {
                GlobalReferences.TimelapseState.SuppressOpeningMessageDialog = openingMessage.DontShowAgain.IsChecked.Value;
            }
        }
        #endregion
    }
}
