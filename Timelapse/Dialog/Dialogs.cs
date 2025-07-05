using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DialogUpgradeFiles;
using Microsoft.WindowsAPICodePack.Dialogs;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Util;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using Clipboard = System.Windows.Clipboard;
using Control = Timelapse.Constant.Control;
using Cursor = System.Windows.Input.Cursor;
using File = Timelapse.Constant.File;
using Rectangle = System.Drawing.Rectangle;
using UnhandledExceptionEventArgs = System.UnhandledExceptionEventArgs;
using WebBrowser = System.Windows.Controls.WebBrowser;

namespace Timelapse.Dialog
{
    public static class Dialogs
    {
        #region Dialog Box Positioning and Fitting

        // Most (but not all) invocations of SetDefaultDialogPosition and TryFitDialogWndowInWorkingArea 
        // are done together, so collapse it into a single call
        public static void TryPositionAndFitDialogIntoWindow(Window window)
        {
            SetDefaultDialogPosition(window);
            TryFitDialogInWorkingArea(window);
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
            Window mainWindow = Application.Current.MainWindow;
            if (mainWindow == null)
            {
                // This shouldn't happen
                TracePrint.NullException(nameof(mainWindow));
                return false;
            }

            PresentationSource presentationSource = PresentationSource.FromVisual(mainWindow);
            if (presentationSource != null)
            {
                CompositionTarget compositionTarget = presentationSource.CompositionTarget;
                if (compositionTarget == null)
                {
                    // This shouldn't happen
                    TracePrint.NullException(nameof(compositionTarget));
                    return false;
                }

                Matrix m = compositionTarget.TransformToDevice;
                //Matrix m = PresentationSource.FromVisual(System.Windows.Application.Current.MainWindow).CompositionTarget.TransformToDevice;
                dpiWidthFactor = m.M11;
                dpiHeightFactor = m.M22;
            }

            // Get the monitor screen that this window appears to be on
            Screen screenInDpi = Screen.FromHandle(new WindowInteropHelper(window).Handle);

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
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = title;
                openFileDialog.CheckFileExists = true;
                openFileDialog.CheckPathExists = true;
                openFileDialog.Multiselect = false;
                openFileDialog.AutoUpgradeEnabled = true; // Set filter for file extension and default file extension 
                openFileDialog.DefaultExt = defaultExtension;
                openFileDialog.Filter = filter;
                if (string.IsNullOrWhiteSpace(defaultFilePath))
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
            selectedFolderPath = string.Empty;
            using (CommonOpenFileDialog folderSelectionDialog = new CommonOpenFileDialog())
            {
                folderSelectionDialog.Title = title;
                folderSelectionDialog.DefaultDirectory = initialFolder;
                folderSelectionDialog.IsFolderPicker = true;
                folderSelectionDialog.EnsurePathExists = false;
                folderSelectionDialog.Multiselect = false;
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
        /// File dialog where the user can only select a file within a sub-folder of the root folder path
        /// It returns the path to the selected folder
        /// If fileNameToLocate is not empty, it displays that a desired folder to select in the dialog title.
        /// </summary>
        /// <param name="initialFolder">The path to the root folder containing the template</param>
        /// <param name="title">The title to be displayed in the dialog</param>
        /// <param name="extensionInUsersLanguage">The file type using the user's language, usually something like "Timelapse data (ddb) files"</param>
        /// <param name="extension">The file type, usually a .tdb or .ddb extension</param>
        /// <returns></returns>
        // ReSharper disable once UnusedMember.Global
        public static string LocateFileBelowInitialFolderUsingOpenFileDialog(string initialFolder, string title, string extensionInUsersLanguage, string extension)
        {
            if (initialFolder == null)
            {
                return string.Empty;
            }

            CommonFileDialogFilter filter = new CommonFileDialogFilter(extensionInUsersLanguage, extension)
            {
                ShowExtensions = true
            };

            using (CommonOpenFileDialog fileSelectionDialog = new CommonOpenFileDialog())
            {
                fileSelectionDialog.Title = title;
                fileSelectionDialog.DefaultDirectory = initialFolder;
                fileSelectionDialog.IsFolderPicker = false;
                fileSelectionDialog.Multiselect = false;
                fileSelectionDialog.DefaultExtension = extension;
                fileSelectionDialog.EnsureFileExists = true;
                fileSelectionDialog.EnsurePathExists = true;
                fileSelectionDialog.Filters.Add(filter);
                fileSelectionDialog.InitialDirectory = fileSelectionDialog.DefaultDirectory;
                fileSelectionDialog.FolderChanging += FolderSelectionDialog_FolderChanging;
                if (fileSelectionDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    // Trim the root folder path from the folder name to produce a relative path. 
                    return fileSelectionDialog.FileName;
                }

                return null;
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
                return string.Empty;
            }

            using (CommonOpenFileDialog folderSelectionDialog = new CommonOpenFileDialog())
            {
                folderSelectionDialog.Title = "Locate folder" + folderNameToLocate + "...";
                folderSelectionDialog.DefaultDirectory = initialFolder;
                folderSelectionDialog.IsFolderPicker = true;
                folderSelectionDialog.Multiselect = false;
                folderSelectionDialog.InitialDirectory = folderSelectionDialog.DefaultDirectory;
                folderSelectionDialog.FolderChanging += FolderSelectionDialog_FolderChanging;
                if (folderSelectionDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    // Trim the root folder path from the folder name to produce a relative path. 
                    return (folderSelectionDialog.FileName.Length > initialFolder.Length) ? folderSelectionDialog.FileName.Substring(initialFolder.Length + 1) : string.Empty;
                }

                return null;
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
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Title = title;
                saveFileDialog.CheckFileExists = false;
                saveFileDialog.CheckPathExists = true;
                saveFileDialog.AutoUpgradeEnabled = true; // Set filter for file extension and default file extension 
                saveFileDialog.DefaultExt = defaultExtension;
                saveFileDialog.Filter = filter;
                if (string.IsNullOrWhiteSpace(defaultFilePath))
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
                                               + "\u2022 another application is using that file (e.g., Excel)";
            }

            messageBox.Message.Hint = "Try logging off and then back on, which may release the " + entity + " if another application is using it.";
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.ShowDialog();
        }

        #endregion

        #region MessageBox: Prompt to apply operation if partial selection.

        // Warn the user that they are currently in a selection displaying only a subset of files, and make sure they want to continue.
        public static bool MaybePromptToApplyOperationOnSelectionDialog(Window owner, FileDatabase fileDatabase, bool promptState, string operationDescription,
            Action<bool> persistOptOut)
        {
            if (CheckIfPromptNeeded(promptState, fileDatabase, out int filesTotalCount, out int filesSelectedCount) == false)
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

            bool proceedWithOperation = messageBox.ShowDialog() == true;
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

        #region MessageBox: Overwrite Files?

        // Check if a prompt dialog is needed
        public static bool OverwriteListOfExistingFiles(Window owner, List<string> files)
        {
            // Warn the user that the operation will overwrite existing files
            string s = files.Count == 1 ? "" : "s";
            string title = $"Overwrite file{s}?";
            string fileList = string.Empty;
            foreach (string file in files)
            {
                fileList += $"{Environment.NewLine}\u2022 {file}";
            }

            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OKCancel)
            {


                Message =
                {
                    What = $"Overwrite {files.Count} file{s} with the same name?",
                    Reason = $"The destination folder already has {files.Count} file{s} with the same name:{fileList}",
                    Solution = $"Select {Environment.NewLine}"
                               + "\u2022 'Ok' for Timelapse to overwrite those files" + Environment.NewLine
                               + "\u2022 'Cancel' to abort",
                    Icon = MessageBoxImage.Question
                }
            };
            return messageBox.ShowDialog() == true;
        }

        #endregion

        #region MessageBox: FilesCannotBeModified

        public static bool FilesCannotBeModified(Window owner, List<string> files)
        {
            // Warn the user that these files cannot be modified
            string s = files.Count == 1 ? "" : "s";
            string title = $"File{s} cannot be modified";
            string fileList = string.Empty;
            foreach (string file in files)
            {
                fileList += $"\u2022 {file}{Environment.NewLine}";
            }

            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OK)
            {
                Message =
                {
                    Problem = "Some files you are trying to write already exist and cannot be modified",
                    Reason =
                        $"The destination folder already has {files.Count} file{s} with the same name,{Environment.NewLine}and whose permissions disallow modification:{Environment.NewLine}{fileList}",
                    Solution = "Try changing the security setting on the file by" + Environment.NewLine
                                                                                  + "\u2022 clicking its 'Property' setting" + Environment.NewLine
                                                                                  + "\u2022 go to the  'Attributes' row and unclick the 'Read only' checkbox.",
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

        #region MessagBox: Original file cannot be overwritten

        public static void FileCannotBeOverwrittenDialog(Window owner, string filePath)
        {
            const string title = "The file cannot be over-written";
            MessageBox messageBox = new MessageBox(title, owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Error,
                    Title = title,
                    Problem = "The original file cannot be overwritten, so nothing was done."
                              + Environment.NewLine
                              + "\u2022 " + filePath,
                    Reason = "Overwriting a file requires deleting the original file and then creating the new file."
                             + Environment.NewLine
                             + "However, if another application is using the original file, it may block deletion.",
                    Solution = "Check if another application is using that file. If so, close that application and retry."
                }
            };
            messageBox.ShowDialog();
        }

        #endregion

        #region MessageBox: Missing dependencies

        public static void DependencyFilesMissingDialog(string applicationName, string missingAssemblies)
        {
            // can't use DialogMessageBox to show this message as that class requires the Timelapse window to be displayed.
            string messageTitle = $"{applicationName} needs to be in its original downloaded folder.";
            StringBuilder message = new StringBuilder("Problem:" + Environment.NewLine);
            message.AppendFormat("{0} won't run properly as it was not correctly installed.{1}{1}", applicationName, Environment.NewLine);
            message.AppendLine("Reason:");
            message.AppendFormat("When you downloaded {0}, it was in a folder with several other files and folders it needs. You probably dragged {0} out of that folder.{1}{1}",
                applicationName, Environment.NewLine);
            message.AppendLine("Solution:");
            message.AppendFormat("Move the {0} program back to its original folder, or download it again.{1}{1}", applicationName, Environment.NewLine);
            message.AppendLine("Hint:");
            message.AppendFormat("Create a shortcut if you want to access {0} outside its folder:{1}", applicationName, Environment.NewLine);
            message.AppendLine("1. From its original folder, right-click the Timelapse program icon.");
            message.AppendLine("2. Select 'Create Shortcut' from the menu.");
            message.AppendFormat("3. Drag the shortcut icon to the location of your choice.{0}{0}", Environment.NewLine);
            message.AppendLine("Details:");
            message.AppendLine("The following assemblies were not found:");
            message.AppendFormat("- {0}", missingAssemblies);
            System.Windows.MessageBox.Show(message.ToString(), messageTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        #endregion

        #region MessageBox: Path too long warnings - several versions


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
                    Solution = "\u2022 Shorten the path name by moving your image folder higher up the folder hierarchy, or" + Environment.NewLine +
                               "\u2022 Use shorter folder or file names."
                               + Environment.NewLine + "\u2022 If you do move them and Timelapse cannot find those folders, "
                               + Environment.NewLine + "     use Edit|Try to find any missing folders... to locate them.",
                    Reason = "Windows cannot perform file operations if the folder path combined with the file name is more than " + File.MaxPathLength + " characters."
                             + "Timelapse will shut down until you fix this.",
                    Hint = "Files created in your " + File.BackupFolder + " folder must also be less than " + File.MaxPathLength + " characters."
                }
            };
            if (e != null)
            {
                Clipboard.SetText(e.ExceptionObject.ToString());
            }

            messageBox.ShowDialog();
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
                    Reason = "Windows cannot perform file operations if the folder path combined with the file name is more than " + File.MaxPathLength + " characters.",
                    Solution = "Try reloading this image set after shortening the file path:"
                               + Environment.NewLine
                               + "\u2022 shorten the path name by moving your image folder higher up the folder hierarchy, or" + Environment.NewLine +
                               "\u2022 use shorter folder or file names.",
                    Hint = "Files created in your " + File.BackupFolder + " folder must also be less than " + File.MaxPathLength + " characters."
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
                              File.DeletedFilesFolder + " folder." + Environment.NewLine
                              + "However, the new file paths are too long for Windows to handle.",
                    Reason = "Windows cannot perform file operations if the file path is more than " +
                             (File.MaxPathLength + 8) + " characters.",
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
                    Reason = "Windows imposes a file name length limit (including its folder path) of around " + File.MaxPathLength + " characters.",
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
                    Reason = "Windows imposes a file name length limit (including its folder path) of around " + File.MaxPathLength + " characters.",
                    Solution = "Shorten the path name, preferably well below the length limit:" + Environment.NewLine
                                                                                                + "\u2022 move your image folder higher up the folder hierarchy, or" +
                                                                                                Environment.NewLine
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
                    Reason = "Timelapse normally creates time-stamped backup files of your template, database, and csv files within a " + File.BackupFolder + " folder." +
                             Environment.NewLine
                             + "However, Windows imposes a file name length limit (including its folder path) of around " + File.MaxPathLength + " characters.",
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
                    Reason = "Windows imposes a file name length limit (including its folder path) of around " + File.MaxPathLength + " characters.",
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
                                                                                                       + "You can also send an explanatory note to saul@ucalgary.ca." +
                                                                                                       Environment.NewLine
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
                    Reason = $"The template ({File.TemplateDatabaseFileExtension}) file may be corrupted, unreadable, or otherwise invalid.",
                    Solution = "Try one or more of the following:"
                               + Environment.NewLine
                               + $"\u2022 recreate the template, or use another copy of it.{Environment.NewLine}"
                               + $"\u2022 check if there is a valid template file in your {File.BackupFolder} folder.{Environment.NewLine}"
                               + $"\u2022 email {ExternalLinks.EmailAddress} describing what happened, attaching a copy of your {File.TemplateDatabaseFileExtension} file.",
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
                               + "\u2022 Also, check for valid backups of your database in your " + File.BackupFolder + " folder that you can reuse.",
                    Hint = "If you are stuck: Send an explanatory note to saul@ucalgary.ca." + Environment.NewLine
                                                                                             + "He will check those files to see if there is a fixable bug.",
                    Icon = MessageBoxImage.Error
                }
            };
            messageBox.Message.Reason += "\u2022 Timelapse was shut down (or crashed) in the midst of:" + Environment.NewLine
                                                                                                        + "    - loading your image set for the first time, or" +
                                                                                                        Environment.NewLine
                                                                                                        + "    - writing your data into the file, or" + Environment.NewLine
                                                                                                        + "\u2022 system, security or network  restrictions prohibited file reading and writing, or," +
                                                                                                        Environment.NewLine
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
                                                           + $"\u2022 template files with a suffix {File.TemplateDatabaseFileExtension} {Environment.NewLine}"
                                                           + $"\u2022 data files with a suffix {File.FileDatabaseFileExtension}",
                    Solution = "Load only template and database files with those suffixes.",
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
        /// Tell the user Timelapse can't copy to all a null value
        /// </summary>
        public static void DataEntryCantCopyAsNullDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Can't do this operation", owner, MessageBoxButton.YesNo)
            {
                Message =
                {
                    Icon = MessageBoxImage.Error,
                    What = "Timelapse cannot do this operation.",
                    Reason = "The control has, for some reason, a 'null' value that can't be copied.",
                    Solution = $"Try one of the following:{Environment.NewLine}\u2022 Retype the value in this control with an appropriate value (including empty)"  +
                            $"{Environment.NewLine}• Navigate to a different image and then back to this one and try to do this again.",
                    Result = "Nothing has been changed."
                }
            };
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Ask the user to confirm value propagation from the last value
        /// </summary>
        public static bool? DataEntryConfirmCopyForwardDialog(Window owner, string text, int imagesAffected, bool checkForZero)
        {
            text = string.IsNullOrEmpty(text) ? string.Empty : text.Trim();

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
            text = string.IsNullOrEmpty(text) ? string.Empty : text.Trim();

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

        public static bool DataEntryConfirmPropagateFromLastValueDialog(Window owner, String text, int imagesAffected)
        {
            text = string.IsNullOrEmpty(text) ? string.Empty : text.Trim();
            MessageBox messageBox = new MessageBox("Please confirm 'Propagate to Here' for this field.", owner, MessageBoxButton.YesNo)
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
                },
                DontShowAgain =
                {
                    Visibility = Visibility.Visible
                }
            };

            bool proceedWithOperation = messageBox.ShowDialog() == true;
            if (proceedWithOperation && messageBox.DontShowAgain.IsChecked.HasValue)
            {
                GlobalReferences.TimelapseState.SuppressPropagateFromLastNonEmptyValuePrompt = messageBox.DontShowAgain.IsChecked.Value;
            }

            return proceedWithOperation;
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

        #region MessageBox: No Updates Available / Timeout

        public static void NoUpdatesAvailableDialog(Window owner, string applicationName, Version currentVersionNumber)
        {
            new MessageBox($"No updates to {applicationName} are available.", owner)
            {
                Message =
                {
                    Reason = $"You a running the latest version of {applicationName}, version: {currentVersionNumber}",
                    Icon = MessageBoxImage.Information
                }
            }.ShowDialog();
        }

        public static void CheckUpdatesTimedOutDialog(Window owner)
        {
            new MessageBox("Could not check for newer versions.", owner)
            {
                Message =
                {
                    What = "Could not check to see if a newer Timelapse version is available.",
                    Reason = "The request timed out. Either the network is slow or the server is down.",
                    Hint = "Try again later.",
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
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "No Files are Missing.";
            new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
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
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = "Resetting selection to All files (no files currently match the current selection)";
            MessageBox messageBox = new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
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
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = "Timelapse could not find any matches to " + fileName;
            new MessageBox(title, owner, MessageBoxButton.OK)
            {
                Message =
                {
                    Title = title,
                    What = "Timelapse tried to find the missing image with no success.",
                    Reason = "Timelapse searched the other folders in this image set, but could not find another file that: "
                             + Environment.NewLine
                             + " - was named " + fileName + ", and  " + Environment.NewLine
                             + " - was not already associated with another image entry.",
                    Hint = "If the original file was:"
                           + Environment.NewLine
                           + "\u2022 deleted, check your " + File.DeletedFilesFolder + " folder to see if its there." + Environment.NewLine
                           + "\u2022 moved outside of this image set, then you will have to find it and move it back in." + Environment.NewLine
                           + "\u2022 renamed, then you have to find it yourself and restore its original name." + Environment.NewLine + Environment.NewLine
                           + "Of course, you can just leave things as they are, or delete this image's data field if it has little value to you.",
                    Icon = MessageBoxImage.Question
                }
            }.ShowDialog();
        }

        public static void MissingFoldersInformationDialog(Window owner, int count)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = null;

            string title = count + " of your folders could not be found";
            new MessageBox(title, owner, MessageBoxButton.OK)
            {
                Message =
                {
                    Title = title,
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
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "No images or videos were found.";
            new MessageBox(title, owner, MessageBoxButton.OK)
            {
                Message =
                {
                    Title = title,
                    Problem = "No images or videos were found in this folder or its subfolders:"
                              + Environment.NewLine
                              + "\u2022 " + selectedFolderPath + Environment.NewLine,
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

        #region MenuFile CSV Export
        /// <summary>
        /// Export data for this image set as a.csv file, but confirm, as only a subset will be exported since a selection is active
        /// </summary>
        public static bool? MenuFileExportCSVOnSelectionDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Exporting to a .csv file on a selected view...";
            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    Title = title,
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
        /// Export data for this image set as a.csv file, but confirm, as only a subset will be exported since a selection is active
        /// </summary>
        public static bool? MenuFileExportFailedForUnknownReasonDialog(Window owner, string fileName)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Exporting failed for an unkown reason...";
            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    Title = title,
                    What = $"Exporting your data to the file below failed for an unknown reason{Environment.NewLine}" +
                           $"{fileName}",
                    Hint = "If there is no obvious reason for this failure, email the Timelapse developer saul@ucalgary.ca",
                    Icon = MessageBoxImage.Exclamation
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
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Can't write the spreadsheet file.";
            new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
                    Icon = MessageBoxImage.Error,
                    Problem = "The following file can't be written: " + csvFilePath,
                    Reason = "You may already have it open in Excel or another application.",
                    Solution = "If the file is open in another application, close it and try again.",
                    Hint = $"{exceptionName}: {exceptionMessage}"
                }
            }.ShowDialog();
        }

        /// <summary>
        /// Export data for this image set as a.csv file, but confirm, as only a subset will be exported since a selection is active
        /// </summary>
        public static void MenuFileExportRequiresAllFilesSelected(Window owner, string whereToExport)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = $"Can't do the {whereToExport} export, as only a subset of your files are selected ";
            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OK)
            {
                Message =
                {
                    Title = title,
                    What = $"Exporting {whereToExport} requires that you have all your files selected,{Environment.NewLine}" +
                           "but you are only viewing a subset of them",
                    Solution = $"Try exporting again by selecting all your files via:{Environment.NewLine}" +
                               " • Select | All files",
                    Icon = MessageBoxImage.Exclamation
                },
            };

            messageBox.ShowDialog();
        }

        #region MessageBox: CamtrapDP-specific exporting

        // Confirm closing this template and creating a new one
        public static bool? ExportToCamtrapDPExplanation(Window owner)
        {
            return new MessageBox("Export all data as CamtrapDP files", owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    Icon = MessageBoxImage.Question,
                    What = $"To export all your data as CamtrapDP files, you will be asked to select a folder.{Environment.NewLine}" +
                           $"Timelapse will then create a new folder within that called '{File.CamtrapDPExportFolder}',{Environment.NewLine}" +
                           $"and will export four files as required by the CamtrapDP standard into that folder.{Environment.NewLine}" +

                           $" • {File.CamtrapDPDataPackageJsonFilename}{Environment.NewLine}" +
                           $" • {File.CamtrapDPDeploymentCSVFilename}{Environment.NewLine}" +
                           $" • {File.CamtrapDPMediaCSVFilename}{Environment.NewLine}" +
                           $" • {File.CamtrapDPObservationsCSVFilename}",
                    Result = $"You should be able to upload these files to a CamptrapDP-compatable data repository,{Environment.NewLine}" +
                            "as Timelapse configures each one to conform to the CamtrapDP standard.",
                    Hint = $"CamptrapDP requires filled in values for various fields. Timelapse will warn you if some are missing.{Environment.NewLine}" +
                           $" • see CamtrapDP specifications: see https://camtrap-dp.tdwg.org/{Environment.NewLine}{Environment.NewLine}" +
                           $"However, we do recommend validating your files against a proper CamtrapDP validator before uploading.{Environment.NewLine}" +
                           " • see CamtrapDP validation: see https://camtrap-dp.tdwg.org/#validation ",
                }
            }.ShowDialog();
        }
        #endregion

        #region MessageBox: AllDataExportedToCSV

        // A message saying that the folder data was exported to various CSV files
        public static void AllDataExportedToCSV(Window owner, string folderPath, List<string> files, bool imageDataIncluded)
        {
            // Warn the user that these files cannot be modified
            string title = "The following CSV files were written";
            string fileList = string.Empty;
            foreach (string file in files)
            {
                fileList += $"{Environment.NewLine}\u2022 {file}";
            }

            string reason = imageDataIncluded
                ? $"• Image data was exported to {File.CSVImageDataFileName}.{Environment.NewLine}" +
                  $"  (data in that file can be altered and imported back into Timelapse).{Environment.NewLine}"
                : string.Empty;
            reason += "• Each folder data level was exported to a file whose name is the same as the folder data level.";
            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OK)
            {
                Message =
                {
                    What = $"All your  data was exported to these files:{fileList}{Environment.NewLine}in the folder: {folderPath}",
                    Reason = reason,
                    Icon = MessageBoxImage.Information
                }
            };
            messageBox.ShowDialog();
        }
        #endregion


        /// <summary>
        /// Cant open the file using Excel
        /// </summary>
        public static void MenuFileCantOpenExcelDialog(Window owner, string csvFilePath)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Can't open Excel.";
            new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
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
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Data exported.";
            // since the exported file isn't shown give the user some feedback about the export operation
            MessageBox csvExportInformation = new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
                    What = "The selected files were exported to " + csvFileName,
                    Result =
                        $"This file is overwritten every time you export it (backups can be found in the {File.BackupFolder} folder).",
                    Hint = "\u2022 You can open this file with most spreadsheet programs, such as Excel." + Environment.NewLine
                                                                                                          + "\u2022 If you make changes in the spreadsheet file, you will need to import it to see those changes." +
                                                                                                          Environment.NewLine
                                                                                                          + "\u2022 You can change the Date and Time formats by selecting the Options|Preferences menu." +
                                                                                                          Environment.NewLine
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
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "How importing .csv data works";
            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    Title = title,
                    What = "Importing data from a .csv (comma separated value) file follows the rules below.",
                    Reason = "The first row in the CSV file must comprise column headers, where:" + Environment.NewLine
                                                                                                  + "\u2022 'File' must be included." + Environment.NewLine
                                                                                                  + "\u2022 'RelativePath' must be included if any of your images are in subfolders" +
                                                                                                  Environment.NewLine
                                                                                                  + "\u2022 remaining headers should generally match your template's DataLabels" +
                                                                                                  Environment.NewLine
                                                                                                  + "Headers can be a subset of your template's DataLabels." + Environment.NewLine +
                                                                                                  Environment.NewLine
                                                                                                  + "Subsequent rows define the data for each file, where it must match the Header type:" +
                                                                                                  Environment.NewLine
                                                                                                  + "\u2022 'File' data should match the name of the file you want to update." +
                                                                                                  Environment.NewLine
                                                                                                  + "\u2022 'RelativePath' data should match the sub-folder path containing that file, if any" +
                                                                                                  Environment.NewLine
                                                                                                  + "\u2022 'Counter' data must be blank, 0, or a positive integer. " +
                                                                                                  Environment.NewLine
                                                                                                  + "\u2022 'DateTime', 'Date' and 'Time' data must follow the specific date/time formats (see File|Export data...). " +
                                                                                                  Environment.NewLine
                                                                                                  + "\u2022 'Flag' and 'DeleteFlag' data must be 'true' or 'false'." +
                                                                                                  Environment.NewLine
                                                                                                  + "\u2022 'FixedChoice' data should exactly match a corresponding list item defined in the template, or empty. " +
                                                                                                  Environment.NewLine
                                                                                                  + "\u2022 'Folder' and 'ImageQuality' columns, if included, are skipped over.",
                    Result = "Database values will be updated only for matching RelativePath/File entries. Non-matching entries are ignored.",
                    Hint = "Warnings will be generated for non-matching CSV headers, which you can then fix." + Environment.NewLine
                                                                                                              + "Select 'Don't show this message again' to hide this message. You can unhide it later via the Options|Show or hide... menu.",
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
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Can't import the .csv file.";
            MessageBox messageBox = new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
                    Icon = MessageBoxImage.Error,
                    Problem = $"The file {csvFileName} could not be read.",
                    Reason = "The .csv file is not compatible with the Timelapse template defining the current image set.",
                    Hint = "Timelapse checks the following when importing the .csv file:" + Environment.NewLine
                                                                                          + "\u2022 The first row is a header whose column names match the data labels in the .tdb template file" +
                                                                                          Environment.NewLine
                                                                                          + "\u2022 Counter data values are numbers or blanks." + Environment.NewLine
                                                                                          + "\u2022 Flag and DeleteFlag values are either 'True' or 'False'." + Environment.NewLine
                                                                                          + "\u2022 Choice values are in that field's Choice list, defined in the template." +
                                                                                          Environment.NewLine + Environment.NewLine

                                                                                          + "While Timelapse will do the best it can to update your fields: " + Environment.NewLine
                                                                                          + "\u2022 the csv row is skipped if its RelativePath/File location do not match a file in the Timelapse database ." +
                                                                                          Environment.NewLine
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
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "CSV file imported";
            MessageBox messageBox = new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
                    Icon = MessageBoxImage.Information,
                    What = $"The file {csvFileName} was successfully imported.",
                    Hint = "\u2022 Check your data. If it is not what you expect, restore your data by using latest backup file in " + File.BackupFolder + "."
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
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Can't import the .csv file.";
            new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
                    Icon = MessageBoxImage.Error,
                    Problem = $"The file {csvFileName} could not be opened.",
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
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Can't export this file!";
            new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
                    Icon = MessageBoxImage.Error,
                    Problem = "Timelapse can't export a copy of the current image or video file.",
                    Reason = "It is likely a corrupted or missing file.",
                    Solution = "Make sure you have navigated to, and are displaying, a valid file before you try to export a copy of it."
                }
            }.ShowDialog();
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
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Duplicate this record - What it is for, and caveats";
            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OKCancel)
            {
                Title = title,
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
                messageBox.Message.Problem =
                    "'Duplicate this record' works best with Sorting set to either 'Sort | By relative Path + DateTime (default)', or 'Sort |by DateTime'. Otherwise, duplicate records may not appear next to each other.";
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
                                                                                  + "\u2022 Use 'Sort | Relative Path + Date Time (default)' to ensure that duplicates appear in sequence." +
                                                                                  Environment.NewLine
                                                                                  + "\u2022 Duplicates can only be created in the main view when displaying a single image, not in the overview." +
                                                                                  Environment.NewLine
                                                                                  + "\u2022 Duplicates in the exported CSV file are identifiable as rows with the same relative path and file name.";
            }

            bool? result = messageBox.ShowDialog();
            if (messageBox.DontShowAgain.IsChecked.HasValue && showProblemDescriptionOnly == false)
            {
                GlobalReferences.TimelapseState.SuppressHowDuplicatesWork = messageBox.DontShowAgain.IsChecked.Value;
            }

            return result;
        }

        public static void MenuEditDuplicatesPleaseWait(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Please wait a bit before trying multiple duplications";
            new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
                    Problem = "Duplication needs to wait until the previous duplication is completed",
                    Reason = "When you duplicate a file, Timelapse updates the database, which takes a bit of time." + Environment.NewLine
                        + "Wait until the previous duplication finishes  before duplicating again.",
                    Hint = "The cursor will change to a normal cursor when the previous duplication is done.",
                    Icon = MessageBoxImage.Information
                }
            }.ShowDialog();
        }

        public static void MenuEditCouldNotImportQuickPasteEntriesDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Could not import QuickPaste entries";
            new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
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
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Populate a data field with image metadata of your choosing.";
            new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
                    Problem = "Timelapse can't extract any metadata, as the currently displayed image or video is missing or corrupted." + Environment.NewLine,
                    Reason = "Timelapse tries to examines the currently displayed image or video for its metadata.",
                    Hint = "Navigate to a displayable image or video, and try again.",
                    Icon = MessageBoxImage.Error
                }
            }.ShowDialog();
        }

        public static void MenuEditRereadDateTimesFromMetadataDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Re-read date and times from a metadata field of your choosing.";
            new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
                    Problem = "Timelapse can't extract any metadata, as the currently displayed image or video is missing or corrupted." + Environment.NewLine,
                    Reason = "Timelapse tries to examines the currently displayed image or video for its metadata.",
                    Hint = "Navigate to a displayable image or video, and try again.",
                    Icon = MessageBoxImage.Error
                }
            }.ShowDialog();
        }

        public static void MenuEditNoFilesMarkedForDeletionDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "No files are marked for deletion";
            new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
                    Problem = "You are trying to delete files marked for deletion, but no files have their 'Delete?' field checked.",
                    Hint = "If you have files that you think should be deleted, check their Delete? field.",
                    Icon = MessageBoxImage.Information
                }
            }.ShowDialog();
        }

        public static void MenuEditNoFoldersAreMissing(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "No folders appear to be missing";
            new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
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
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Cannot populate a field with Episode data";
            new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
                    Problem = "Timelapse cannot currently populate any fields with Episode data." + Environment.NewLine,
                    Reason = "There are no files in the current selection.",
                    Hint = "Expand the current selection, or add some images or videos. Then try again.",
                    Icon = MessageBoxImage.Error
                }
            }.ShowDialog();
        }

        public static void MenuOptionsCantPopulateDataFieldWithEpisodeAsNoNoteFields(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Cannot populate a field with Episode data";
            new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
                    Problem = "Timelapse cannot currently populate any fields with Episode data." + Environment.NewLine,
                    Reason = "Episode data would be put in a Note field, but none of your fields are Notes.",
                    Hint = "Modify your template .tdb file to include a Note field using the Timelapse Template Editor." + Environment.NewLine,
                    Icon = MessageBoxImage.Error
                }
            }.ShowDialog();
        }

        public static bool MenuOptionsCantPopulateDataFieldWithEpisodeAsSortIsWrong(Window owner, bool searchTermsOk, bool sortTermsOk)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "You may not want to populate this field with Episode data";
            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    Title = title,
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

        #endregion

        #region MessageBox: related to DateTime

        public static void DateTimeNewTimeShouldBeLaterThanEarlierTimeDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Your new time has to be later than the earliest time";
            MessageBox messageBox = new MessageBox(title, owner)
            {
                Title = title,
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
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = "Timelapse is currently restricted to the folder: '" + folderName + "'";
            new MessageBox(title, owner, MessageBoxButton.OK)
            {
                Message =
                {
                    Title = title,
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
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = "Timelapse could not open the template";
            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OK)
            {
                Message =
                {
                    Title = title,
                    Icon = MessageBoxImage.Information,
                    What = title + Environment.NewLine
                                 + "     '" + fileName + "'" + Environment.NewLine + Environment.NewLine
                                 + "Consequently," + Environment.NewLine
                                 + "\u2022 the instruction to use that template is ignored.",
                    Reason = "Timelapse was started with instructions to open the template indicated above" + Environment.NewLine
                }
            };

            if (!string.IsNullOrWhiteSpace(relativePathArgument))
            {
                messageBox.Message.What += Environment.NewLine + "\u2022 the additional instruction to limit analysis to the subfolder " + "'" + relativePathArgument + "'" +
                                           " is also ignored ";
            }

            if (!string.IsNullOrWhiteSpace(relativePathArgument))
            {
                messageBox.Message.Reason += "and to limit analysis to a particular subfolder." + Environment.NewLine;
            }

            messageBox.Message.Reason += "However, that template either does not exist or could not be accessed.";
            messageBox.ShowDialog();
        }

        // Tell the user that Timelapse could not open the template specified in the argument
        public static bool ArgumentRelativPathWithNoTemplatePathDialog(Window owner, string relativePathArgument)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = "A relative path argument was specified without a template argument";
            return new MessageBox(title, owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    Title = title,
                    Icon = MessageBoxImage.Information,
                    What = title + $"{Environment.NewLine} • -relativepath {relativePathArgument}",
                    Reason = $"Timelapse was started with an argument to limit its analysis to images in a particular folder (the relative path).{Environment.NewLine}" +
                             "However, that only works with the template is specified as an argument as well.",
                    Solution = "Click Ok to open Timelapse (ignoring that constraint), or Cancel to exit Timelapse."
                }
            }.ShowDialog() == true;
        }

        #endregion

        #region MessageBox: related to selecting Episodes in the custom select

        // There are two version of this warning dialog
        // - If the flag is true, the text informs that Timelapse could not find a data field in the expected episode format
        // - If the flag is false, the text informs that the currently selected data field is not in the expected format
        public static void CustomSelectEpisodeDataLabelProblem(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Timelapse cannot enable searches for Episode-related files";
            new MessageBox(title, owner, MessageBoxButton.OK)
            {
                Message =
                {
                    Title = title,
                    Icon = MessageBoxImage.Warning,
                    What = "For Timelapse to expand the search to include episodes, you must have a data field populated" + Environment.NewLine
                        + "with your files' episode data, where the episode data is in the expected format." + Environment.NewLine
                        + "None of your data fields, at least for the current file, includes the expected episode data.",
                    Reason = "When you choose this option, Timelapse searches for episodes having at least one file " + Environment.NewLine
                                                                                                                      + "matching your search criteria. If so, all files contained in those episodes are then displayed." +
                                                                                                                      Environment.NewLine + Environment.NewLine
                                                                                                                      + "For this to work properly, one of your data fields must have been filled in using " +
                                                                                                                      Environment.NewLine
                                                                                                                      + "Edit | Populate a field with episode data..., where:" +
                                                                                                                      Environment.NewLine
                                                                                                                      + " \u2022 the episode format includes the episode sequence number as its prefix, e.g. 25:1|3," +
                                                                                                                      Environment.NewLine
                                                                                                                      + " \u2022 all currently selected files were populated with Episode information at the same time.",
                    Hint = "See the Timelapse Reference Guide about how this operation works, what is required, and what it is for."
                }
            }.ShowDialog();
        }

        #endregion

        #region MessageBox: opening messages when Timelapse is started

        /// <summary>
        /// Give the user various opening messages
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public static void OpeningMessage(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Opening Message ...";
            MessageBox openingMessage = new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
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

        #region MessageBox: Opening in a viewonly state

        /// Give the user various opening mesages
        public static void OpeningMessageViewOnly(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "You are using the Timelapse 'View only' version ...";
            new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
                    What = "You started the view-only version of Timelapse. " + Environment.NewLine
                                                                              + "\u2022 You can open and view existing images, videos and any previously entered data." +
                                                                              Environment.NewLine
                                                                              + "\u2022 You will not be able to edit or alter that data, or create a new image set." +
                                                                              Environment.NewLine,
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
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "You are using the Timelapse 'View only' version. ";
            new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
                    What = "Creating a new image set is not allowed when Timelapse is started as view-only. " + Environment.NewLine,
                    Reason = "You tried to open a template on an image set that has no data associated with it." + Environment.NewLine
                                                                                                                 + "Timelapse normally looks for images in this folder, and creates a database holding data for each image." +
                                                                                                                 Environment.NewLine
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
            const string title = "You are opening your image set with an older Timelapse version";
            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = null;
            // notify the user the template couldn't be loaded rather than silently doing nothing
            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    Title = title,
                    What = "You are opening your image set with an older version of Timelapse." + Environment.NewLine
                                                                                                + "You previously used a later version of Timelapse to open this image set." +
                                                                                                Environment.NewLine
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

        #region MessageBox: ddb file opened with an older version of Timelapse than recorded in it

        public static void DatabaseFileOpenedWithIncompatibleVersionOfTimelapse(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "You are using an old incompatible version of Timelapse";
            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = null;
            // notify the user the template couldn't be loaded rather than silently doing nothing
            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OK)
            {
                Message =
                {
                    Title = title,
                    What = $"You are using an old incompatible version of the Timelapse program to open this image set.{Environment.NewLine}"
                           + "To open this image set, you must update Timelapse to the latest version.",
                    Reason =  $"This image set was previously opened by a later version of Timelapse, which updated{Environment.NewLine}" +
                              "your files in a way that is no longer compatible with the version of Timelapse you are using.",

                    Solution = $"Go to the Timelapse web site, download the new version, and try again.{Environment.NewLine}"
                                        + "\u2022 https://timelapse.ucalgary.ca",
                    Icon = MessageBoxImage.Error,
                    Hint = "Its always best to use the latest Timelapse version to minimize any incompatabilities."
                },
            };
            messageBox.ShowDialog();
            Mouse.OverrideCursor = cursor;
        }

        public static void DatabaseFileBeingMergedIsIncompatibleWithParent(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "You are trying to merge an old incompatible database.";
            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = null;
            // notify the user the template couldn't be loaded rather than silently doing nothing
            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OK)
            {
                Message =
                {
                    Title = title,
                    What = $"You are trying to merge an incompatible database into the parent database.{Environment.NewLine}"
                           + "To merge this database, you must open it within Timelapse, which will update it to the latest version.",
                    Reason =  $"The database you are trying to merge was created with an earlier version of Timelapse.{Environment.NewLine}" +
                              "Its internals are not compatible with the latest database structure.",

                    Solution = $"Use Timelapse to open the template/database located in the folder you are trying to merge.{Environment.NewLine}"
                               + "Timelapse will then update the database. Then try to merge again",
                    Icon = MessageBoxImage.Error,
                    Hint = "Its always best to use the latest Timelapse version on all databases to minimize any incompatabilities."
                },
            };
            messageBox.ShowDialog();
            Mouse.OverrideCursor = cursor;
        }

        #endregion

        #region DialogIsFileValid checks for valid database file and displays appropriate dialog if it isn't

        public static bool DialogIsFileValid(Window owner, string filePath)
        {
            switch (FilesFolders.QuickCheckDatabaseFile(filePath))
            {
                case DatabaseFileErrorsEnum.Ok:
                    return true;
                case DatabaseFileErrorsEnum.OkButOpenedWithAnOlderTimelapseVersion:
                    if (GlobalReferences.MainWindow.State.SuppressOpeningWithOlderTimelapseVersionDialog == false)
                    {
                        return true == DatabaseFileOpenedWithOlderVersionOfTimelapse(owner);
                    }

                    return true;
                case DatabaseFileErrorsEnum.PreVersion2300:
                case DatabaseFileErrorsEnum.UTCOffsetTypeExistsInUpgradedVersion:
                    Mouse.OverrideCursor = null;
                    DialogUpgradeFilesAndFolders dialogUpdateFiles =
                        new DialogUpgradeFilesAndFolders(owner, Path.GetDirectoryName(filePath), VersionChecks.GetTimelapseCurrentVersionNumber().ToString());
                    dialogUpdateFiles.ShowDialog();
                    return false;
                case DatabaseFileErrorsEnum.IncompatibleVersion:
                    Mouse.OverrideCursor = null;
                    DatabaseFileOpenedWithIncompatibleVersionOfTimelapse(owner);
                    return false;
                case DatabaseFileErrorsEnum.PathTooLong:
                    Mouse.OverrideCursor = null;
                    TemplatePathTooLongDialog(owner, filePath);
                    return false;
                case DatabaseFileErrorsEnum.FileInRootDriveFolder:
                    Mouse.OverrideCursor = null;
                    TemplateInDisallowedFolder(owner, true, Path.GetDirectoryName(filePath));
                    return false;
                case DatabaseFileErrorsEnum.FileInSystemOrHiddenFolder:
                    Mouse.OverrideCursor = null;
                    TemplateInDisallowedFolder(owner, false, Path.GetDirectoryName(filePath));
                    return false;
                case DatabaseFileErrorsEnum.InvalidDatabase:
                    Mouse.OverrideCursor = null;
                    if (Path.GetExtension(filePath) == File.TemplateDatabaseFileExtension)
                    {
                        TemplateFileNotLoadedAsCorruptDialog(owner, filePath);
                    }
                    else
                    {
                        DatabaseFileNotLoadedAsCorruptDialog(owner, filePath, false);
                    }

                    return false;
                case DatabaseFileErrorsEnum.NotATimelapseFile:
                    Mouse.OverrideCursor = null;
                    FileNotATimelapseFile(owner, filePath);
                    return false;
                case DatabaseFileErrorsEnum.DoesNotExist:
                case DatabaseFileErrorsEnum.TemplateElementsDiffer:
                case DatabaseFileErrorsEnum.TemplateElementsSameButOrderDifferent:
                case DatabaseFileErrorsEnum.DetectionCategoriesIncompatible:
                case DatabaseFileErrorsEnum.ClassificationCategoriesIncompatible:
                default:
                    return true;
            }
        }

        #endregion

        #region Testing messages for development

        // ReSharper disable once UnusedMember.Global
        public static void RandomMessage(Window owner, string message)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Timelapse message...";
            // since the exported file isn't shown give the user some feedback about the export operation
            MessageBox openingMessage = new MessageBox(title, owner)
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

        #region Merging errors

        // Invalid file
        public static void MergeErrorDatabaseFileAppearsCorruptDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = $"Your database {File.FileDatabaseFileExtension} file  is likely corrupted.";
            // notify the user the database appears corrupt
            new MessageBox(title, owner)
            {
                Message =
                {
                    Problem = $"The selected {File.FileDatabaseFileExtension} file does not contain a valid  database.",
                    Reason = "Your  database file is likely corrupted, which means that Timelapse cannot use it.",
                    Solution = $"If you could open this database file previously, a working backup may be available.{Environment.NewLine}"
                               + $"\u2022 Check your {File.BackupFolder} folder to see if there is a backup that works.{Environment.NewLine}"
                               + $"\u2022 If so, copy it to the problem file's location, and try to open it.{Environment.NewLine}"
                               + "\u2022 If not, you may have to delete your database file and then recreate it.",
                    Hint = $"If you are stuck, send an explanatory note to saul@ucalgary.ca.{Environment.NewLine}"
                           + "He will check those files to see if they can be repaired."
                }
            }.ShowDialog();
        }

        // DDb file exists
        public static bool? MergeWarningCreateEmptyDdbFileExists(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = "Do you really want to create an empty database in this folder?";
            // notify the user the database appears corrupt
            return new MessageBox(title, owner, MessageBoxButton.YesNo)
            {
                Message =
                {
                    Icon = MessageBoxImage.Question,
                    Problem = $"A database {File.FileDatabaseFileExtension} file already exists in that folder.",
                    Reason = $"While you can have multiple databases in a folder, it can lead to confusion {Environment.NewLine}"
                             + "as to which one to use and which one has the most up to date data.",
                    Solution = $"You can continue to create the empty database. However, you may want to {Environment.NewLine}"
                               + $"\u2022 reconsider, revisit and/or clean up that folder before doing so, or{Environment.NewLine}"
                               + "\u2022 create a folder above this one, and use that as the root folder instead.",
                    Hint = $"This is just a warning, as having multiple databases in a folder goes against best practices.{Environment.NewLine}"
                           + "You may want to read about 'Merging files' in the Timelapse Reference Guide."
                }
            }.ShowDialog();
        }

        // DDb file exists
        public static bool? MergeWarningCheckOutDdbFileExists(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = "Do you really want to check out a database into this folder?";
            // notify the user the database appears corrupt
            return new MessageBox(title, owner, MessageBoxButton.YesNo)
            {
                Message =
                {
                    Icon = MessageBoxImage.Question,
                    Problem = $"A database {File.FileDatabaseFileExtension} file already exists in that folder.",
                    Reason = $"While you can have multiple databases in a folder, it can lead to confusion {Environment.NewLine}"
                             + "as to which one to use and which one has the most up to date data.",
                    Solution = $"You can continue to check out a database. However, you may want to {Environment.NewLine}"
                               + " reconsider, revisit and/or clean up that folder before doing so.",
                    Hint = $"This is just a warning, as having multiple databases in a folder goes against best practices.{Environment.NewLine}"
                           + "You may want to read 'Merging files' in the Timelapse Reference Guide."
                }
            }.ShowDialog();
        }

        // Old .ddb version
        public static void MergeErrorDatabaseFileNeedsToBeUpdatedDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = $"Your selected database {File.FileDatabaseFileExtension} needs to be updated.";
            new MessageBox(title, owner)
            {
                Message =
                {
                    Problem = $"Your database {File.FileDatabaseFileExtension} needs to be updated.",
                    Reason = $"Your  database file was created with an old version of Timelapse{Environment.NewLine}"
                             + "and cannot be opened with the current Timelapse version",
                    Solution = $"Upgrade this (and other) files as follows.{Environment.NewLine}"
                               + $"1. Select 'File|Upgrade Timelapse files to the latest version...' from the Timelapse menu.{Environment.NewLine}"
                               + "2. Follow the instructions in that dialog to select and upgrade one or more files.",
                    Hint = $"Opening an upgraded file with an old version of Timelapse can cause Timelapseto crash,{Environment.NewLine}"
                           + $"which may corrupt your database {File.FileDatabaseFileExtension} file.{Environment.NewLine}"
                           + "If that happens, try upgrading it again in the latest version of Timelapse."
                }
            }.ShowDialog();
        }

        // File in a non-permitted place
        public static void TemplateInDisallowedFolder(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Your selected file is in a problematic location";
            MessageBox messageBox = new MessageBox(title, owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Error,
                    Title = title,
                    Problem = $"Your file cannot be located in:{Environment.NewLine}"
                              + $"\u2022 system folders{Environment.NewLine}"
                              + $"\u2022 hidden folders{Environment.NewLine}"
                              + "\u2022 top-level root drives (e.g., C:, D:, etc)",

                    Reason = $"Timelapse expects your file to be in a normal folder for various reasons.{Environment.NewLine}"
                             + $"\u2022 System and hidden folders should not contain user files. {Environment.NewLine}"
                             + "\u2022 If in a root drive, Timelapse search facilities would search everything would could take ages.",

                    Solution = $"\u2022 Move your files to a new or different folder.{Environment.NewLine}"
                               + "\u2022 Change the folder's attributes by selecting 'Properties' from" + Environment.NewLine
                               + "     that folder's context menu, and reviewing the 'Attributes' settings on the 'General' tab",
                }
            };
            messageBox.ShowDialog();
        }

        // File path is too long
        public static void MergeErrorFilePathTooLongDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Your file's path name is too long";
            MessageBox messageBox = new MessageBox(title, owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Error,
                    Title = title,
                    Problem = "Your file's path name is too long.",
                    Reason = $"Windows cannot perform file operations if the folder path combined with the file name{Environment.NewLine}"
                             + "is more than " + File.MaxPathLength + " characters.",
                    Solution = "\u2022 Shorten the path name by moving your image folder higher up the folder hierarchy, or" + Environment.NewLine +
                               "\u2022 Use shorter folder or file names."
                               + Environment.NewLine + "\u2022 If you do move or rename them and Timelapse cannot find those folders or files, "
                               + Environment.NewLine + "     use the 'Edit|Try to find...' functions to locate them.",
                    Hint = "Files created in your " + File.BackupFolder + " folder must also be less than " + File.MaxPathLength + " characters."
                }
            };
            messageBox.ShowDialog();
        }

        // Incompatible Template
        public static void MergeErrorTemplateFilesNotCompatableDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Incompatible templates ";
            MessageBox messageBox = new MessageBox(title, owner)
            {
                Title = title,
                Message =
                {
                    Problem = $"Timelapse could not merge this database back into the parent database{Environment.NewLine}"
                              + "because their templates differ.",
                    Reason = "The data field definitions used to enter image-level data differ.",
                    Solution = "Try the following steps."
                               + Environment.NewLine
                               + $"1. Update the selected database's template ({File.TemplateDatabaseFileExtension}) file to match the destination database's template.{Environment.NewLine}"
                               + $"2. Using folder levels? The child template should not include the extraneous folder levels.{Environment.NewLine}     If it does, this can be difficult to repair.{Environment.NewLine}"
                               + $"3. Reopen the selected database in Timelapse to complete the update.{Environment.NewLine}"
                               + "4. Try to merge the file again.",
                    Icon = MessageBoxImage.Error,
                    Hint =
                        $"The best way to create compatable child template ({File.TemplateDatabaseFileExtension}) and data ({File.FileDatabaseFileExtension}) files is by checking out{Environment.NewLine}" +
                        $"the child via the File|Merge option.{Environment.NewLine}Then use that template to either load the checked out data file, or to create a new data file."
                }
            };
            messageBox.ShowDialog();
        }

        public static void MergeErrorTemplateFilesLevelsNotCompatableDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Incompatible templates ";
            MessageBox messageBox = new MessageBox(title, owner)
            {
                Title = title,
                Message =
                {
                    Problem = $"Timelapse could not merge this database back into the parent database{Environment.NewLine}"
                              + "because their templates differ.",
                    Reason = "The folder level definitions differ.",
                    Solution = $"Compare the folder levels between the templates used by the parent and child.{Environment.NewLine}" +
                               "Their folder levels should be the same, and each level should include the same controls.",
                    Icon = MessageBoxImage.Error,
                    Hint =
                        $"The best way to create compatable child template ({File.TemplateDatabaseFileExtension}) and data ({File.FileDatabaseFileExtension}) files is by checking out{Environment.NewLine}" +
                        $"the child via the File|Merge option.{Environment.NewLine}Then use that template to either load the checked out data file, or to create a new data file."
                }
            };
            messageBox.ShowDialog();
        }

        // File does not exist
        public static void MergeErrorFileDoesNotExist(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Cannot merge the databases";
            MessageBox messageBox = new MessageBox(title, owner)
            {
                Message =
                {
                    Problem = "The selected file no longer exists so it can't be merged",
                    Reason = $"Timelapse could not determine why the selected file no longer exists.{Environment.NewLine}"
                             + "Was it was deleted, moved or renamed before completing the merge operation?",
                    Solution = $"Try relocating the file and moving it into the correct folder, {Environment.NewLine}"
                               + "or, run the merge operation again to see what files are available for merging.",
                    Icon = MessageBoxImage.Error,
                }
            };
            messageBox.ShowDialog();
        }

        // Recognizer categories differ
        public static void MergeErrorRecognitionCategoriesIncompatible(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "The recognition categories between your files are incompatible";
            new MessageBox(title, owner)
            {
                Message =
                {
                    Problem = $"The detection and/or classification categories used for image recognition{Environment.NewLine}"
                              + "are incompatible between the selected files and the destination file.",
                    Reason = "As Timelapse was unable to combine the categories, it stopped the merge as otherwise recognitions would be inconsistent in the merged files.",
                    Solution = $"Possibities include:{Environment.NewLine}"
                               + $"\u2022 Revisit how the imported recognition json files were created.{Environment.NewLine}"
                               + $"\u2022 Redo image recognition for those files, being careful to use the same recognizer and (optional) classification model.{Environment.NewLine}"
                               + "\u2022 If all else fails, delete the recognitions from the selected file."
                }
            }.ShowDialog();
        }

        #endregion

        #region Moving and Creating folder errors (used by the RelativePathEditor)

        public static void RenameRelativePathError(Window owner, MoveFolderResultEnum result, string sourceFolderPath, string destinationFolderPath)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = $"Could not rename the folder {sourceFolderPath}";

            string reason;
            switch (result)
            {
                case MoveFolderResultEnum.Success:
                    // This should not have been passed in
                    return;
                case MoveFolderResultEnum.FailAsSourceFolderDoesNotExist:
                    reason = $"The source folder '{sourceFolderPath}' does not exist." + Environment.NewLine +
                             "Because of that, Timelapse could not rename the actual folder.";
                    break;
                case MoveFolderResultEnum.FailAsDestinationFolderExists:
                    reason = $"The destination folder '{destinationFolderPath}' already exists as a Windows subfolder." + Environment.NewLine +
                             "Windows does not allow a folder to be renamed if a folder with the desired name already exists.";
                    break;
                case MoveFolderResultEnum.FailDueToSystemMoveException:
                default:
                    reason = "Windows tried to rename your folder, but for some reason couldn't do it.";
                    break;
            }

            new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
                    What = $"Timelapse could not rename {sourceFolderPath} tos {destinationFolderPath}",
                    Reason = reason,
                    Icon = MessageBoxImage.Error
                }
            }.ShowDialog();
        }

        public static void RenameRelativePathError(Window owner, CreateSubfolderResultEnum result, string sourceFolderPath, string destinationName)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = $"Could not create the folder {destinationName}";

            string reason;
            switch (result)
            {
                case CreateSubfolderResultEnum.Success:
                    // This should not have been passed in
                    return;
                case CreateSubfolderResultEnum.FailAsSourceFolderDoesNotExist:
                    reason = $"The folder '{sourceFolderPath}' does not exist as a Windows folder." + Environment.NewLine +
                             $"Because of that, Timelapse could not create the subfolder '{destinationName}' within it.";
                    break;
                case CreateSubfolderResultEnum.FailAsDestinationFolderExists:
                    reason = $"The destination subfolder '{destinationName}' already exists as a Windows subfolder in '{sourceFolderPath}'." + Environment.NewLine +
                             "Because of that, Timelapse did not have to create the subfolder.";
                    break;
                case CreateSubfolderResultEnum.FailDueToSystemCreateException:
                default:
                    reason = "Windows tried to create the subfolder '{destinationName}' in '{sourceFolderPath}'," + Environment.NewLine +
                             " but for some reason couldn't do it.";
                    break;
            }

            new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
                    What = $"Timelapse could not create the folder {destinationName} in {sourceFolderPath}",
                    Reason = reason,
                    Icon = MessageBoxImage.Error
                }
            }.ShowDialog();
        }

        #endregion

        #region Invalid data field input
        public static void InvalidDataFieldInput(Window owner, string dataFieldType, string invalidContent)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string expectedInput = string.Empty;
            string example = string.Empty;
            string expectedType = string.Empty;
            string title = "Your entered data is not ";
            switch (dataFieldType)
            {
                case Control.AlphaNumeric:
                    expectedInput = "alphanumeric text";
                    example = $"{Environment.NewLine}• allowed characters: (A-Z, a-z, 0-9, _, -)" +
                              $"{Environment.NewLine}•  e.g., Station_1";
                    expectedType = "alphanumeric";
                    title += "alphanumeric text";
                    break;
                case Control.AlphaNumeric + "Glob":
                    expectedInput = "alphanumeric text plus glob characters";
                    example = $"{Environment.NewLine}• allowed characters: (A-Z, a-z, 0-9, _, -,?, *)" +
                              " e.g., Station* to find text with the prefix Station";
                    expectedType = "alphanumeric with glob characters";
                    title += "alphanumeric text plus glob chacters";
                    break;
                case Control.IntegerAny:
                    expectedInput = "an integer";
                    expectedType = "integer";
                    example = $"{Environment.NewLine}• e.g., -5";
                    title += "a valid integer";
                    break;
                case Control.Counter:
                    expectedInput = "a positive integer";
                    expectedType = "counter";
                    example = $"{Environment.NewLine}• e.g., 5";
                    title += "a valid positive integer";
                    break;
                case Control.IntegerPositive:
                    expectedInput = "a positive integer";
                    expectedType = "positive integer";
                    example = $"{Environment.NewLine}• e.g., 5";
                    title += "a valid positive integer";
                    break;
                case Control.FixedChoice:
                    expectedInput = "an item in this control's fixed choice menu, or blank";
                    expectedType = "an item in this control's fixed choice menu, or blank";
                    example = $"{Environment.NewLine}• e.g., c if your menu list contained a,b,c ";
                    title += "a valid choice menu item";
                    break;
                case Control.MultiChoice:
                    expectedInput = "a comma-separated list of items, each in your list menu";
                    expectedType = "comma-separated list";
                    example = $"{Environment.NewLine}• e.g., a,c if your multichoice list contained a,b,c ";
                    title += "a valid list of choice menu items";
                    break;
                case Control.DecimalAny:
                    expectedInput = "a decimal number";
                    example = $"{Environment.NewLine}•  e.g., -3.45";
                    expectedType = "decimal";
                    title += "a valid decimal number";
                    break;
                case Control.DecimalPositive:
                    expectedInput = "a positive decimal number";
                    example = $"{Environment.NewLine}• e.g., 3.45";
                    expectedType = "positive decimal";
                    title += "a valid positive decimal number";
                    break;
                case Control.DateTime_:
                    expectedInput = "in date/time format";
                    example = $"{Environment.NewLine}• format is yyyy-mm-dd hh:mm:ss" +
                              $"{Environment.NewLine}• e.g., 2024-12-24 13:05:00 for Dec. 24, 2024, 1:05 pm";
                    expectedType = "date/time";
                    title += "in date/time format";
                    break;
                case Control.Date_:
                    expectedInput = "in date format";
                    example = $"{Environment.NewLine}• format is yyyy-mm-dd" +
                              $"{Environment.NewLine}• e.g., 2024-12-24 ";
                    expectedType = "date";
                    title += "in date format";
                    break;
                case Control.Time_:
                    expectedInput = "in time format ";
                    example = $"{Environment.NewLine}• format is hh:mm:ss" +
                              $"{Environment.NewLine}• e.g., 13:05:00 for 1:05 pm";
                    expectedType = "time";
                    title += "in time format";
                    break;
                case Control.Flag:
                    expectedInput = "a true or false value";
                    title += "a valid true or false value";
                    break;
            }

            string what = invalidContent.Length < 30

                ? $"Your entered data would be: {invalidContent}{Environment.NewLine}This does not match what a {expectedType} data field expects."
                : "Your entered data does not match what this data field expects.";

            new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
                    What = what,
                    Reason = $"The contents of this data field must be {expectedInput}{example}",
                    Result = "Your data field's contents may be reset.",
                    Hint = "Check your data field's contents, then enter text that matches what the data field expects.",
                    Icon = MessageBoxImage.Error
                }
            }.ShowDialog();
        }

        #endregion

        #region FolderEditor warning as using folder levels

        public static bool? FolderEditorMetadataWarning(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "The folder editor does not work well with folder levels and folder metadata...";
            return new MessageBox(title, owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    Title = title,
                    Problem = "Your image set uses folder levels and folder metadata. " + Environment.NewLine
                                                                                        + "If you rename, move or delete existing folders that have metadata associated with it, " +
                                                                                        Environment.NewLine
                                                                                        + "the folder editor will not update its folder level locations." + Environment.NewLine
                                                                                        + "This means that previously entered metadata may no longer be associated with the edited folder.",
                    Solution = "For now, we recommend against moving or renaming existing folders." + Environment.NewLine
                                                                                                    + " This will be fixed in the next version of Timelapse.",
                    Hint = "You can still use the folder editor to review your folders and/or to create new folders",
                    Icon = MessageBoxImage.Information
                }
            }.ShowDialog();
        }

        #endregion

        #region MessageBox: MenuRecognition - ImportRecognizer File Feeldback
        public static void MenuFileRecognizersDataCouldNotBeReadDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Recognition data not imported.";
            new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
                    Problem = "No recognition information was imported."
                              + Environment.NewLine
                              + "There were problems reading the recognition data.",
                    Reason = $"Possible causes are:{Environment.NewLine}" +
                             $" \u2022 the file could not be opened, or{Environment.NewLine}" +
                              " \u2022 the recognition data in the file is somehow corrupted",
                    Solution = "You may have to re-create the json file.",
                    Result = "Recognition information was not imported, and nothing was changed."
                }
            }.ShowDialog();
        }

        /// <summary>
        /// No matching folders in the DB and the recognizer file
        /// </summary>
        public static void MenuFileRecognitionDataNotImportedDialog(Window owner, string details)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Recognition data not imported.";
            new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
                    Problem = $"No recognition information was imported. The image file paths in the recognition file and the Timelapse{Environment.NewLine}"
                              + "database are all completely different. Thus no recognition information could be assigned to your images.",
                    Reason = $"When the recognizer originally processed a folder (and its subfolders) containing your images,{Environment.NewLine}" +
                             $"it recorded each image's location relative to that folder. If the subfolder structure differs from{Environment.NewLine}" +
                             $"that found in the Timelapse root folder, then the paths won't match.{Environment.NewLine}" +
                             $"For example, if the recognizer was run on 'AllFolders/Camera1/' but your template and database is in 'Camera1/',{Environment.NewLine}" +
                             "the folder paths won't match, since AllFolders/Camera1/ \u2260 Camera1/.",
                    Solution = $"1. Easiest: Rerun the recognizer on the proper folder. See the Timelapse Recognition Guide to better understand what happens.{Environment.NewLine}" +
                               $"2. You may be able to repair the paths in the recognition file using this program:{Environment.NewLine}" +
                                "  https://lila.science/cameratraps-detectormismatch",
                    Result = "Recognition information was not imported, and nothing was changed.",
                    Details = details
                }
            }.ShowDialog();
        }

        /// <summary>
        ///  Some folders missing - show which folder paths in the DB are not in the recognizer file
        /// </summary>
        public static void MenuFileRecognitionDataImportedOnlyForSomeFoldersDialog(Window owner, string jsonFilePath, string details)
        {
            // Some folders missing - show which folder paths in the DB are not in the detector
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = "Recognition data imported";
            bool isRootFolder = string.IsNullOrWhiteSpace(jsonFilePath);
            string what = isRootFolder
                ? "Recognition data imported for images located in your image set."
                : $"Recognition data imported for images located in a particular sub-folder.{Environment.NewLine}•  {jsonFilePath}";

            new MessageBox(title, owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Information,
                    Title = title,
                    What = what,

                    Hint =  $"1. When you run the recognizer on a folder or sub-folder, recognitions are constrained{Environment.NewLine}" +
                            $"   to images located within the selected folder and its sub-folders.{Environment.NewLine}{Environment.NewLine}" +
                            $"2. If you choose 'Select|Custom Selection...', you can:{Environment.NewLine}" +
                            $"   • select images matching particular recognitions,{Environment.NewLine}" +
                            "   • click 'Show all files with no recognition data' to list images missing recognition data.",
                    Details = $"This list indicates which sub-folders that had recognitions.{Environment.NewLine}{details}"
                }
            }.ShowDialog();
        }

        /// <summary>
        /// Recognitions: successfully imported message
        /// </summary>
        public static void MenuFileRecognitionsSuccessfulyImportedDialog(Window owner, string details, string summaryReport)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Recognitions imported.";
            MessageBox message = new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
                    Icon = MessageBoxImage.Information,
                    Result = "Recognition data imported for your image set.",
                    Hint = $"If you choose 'Select|Custom Selection...', you can:{Environment.NewLine}" +
                           $"• select images matching particular recognitions,{Environment.NewLine}" +
                           "• click 'Show all files with no recognition data' to list images missing recognition data.",
                    Details = details
                }
            };
            // The Extra button is normally hidden. We reveal it and use it to invoke a dialog box to show the summary report (extracted from the recognizer file), if present
            if (false == string.IsNullOrWhiteSpace(summaryReport))
            {
                message.ExtraButton.Visibility = Visibility.Visible;
                message.ExtraButton.Content = "Show the recognizer's summary report";
                message.ExtraButton.Tag = summaryReport;
                message.ExtraButton.Click += ExtraButton_Click;
            }
            message.ShowDialog();
        }

        // Event handler: invoke a dialog box to show the summary report (held as a string in the tag), if present
        private static void ExtraButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button extraButton == false) return;
            if (extraButton.Tag is String contentString == false) return;
            if (string.IsNullOrWhiteSpace(contentString)) return;
                Window window = new Window
                {
                    Title = "Recognizer's summary report",
                    ShowInTaskbar = false,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Width = 400,
                    Height = 500,
                    Owner = extraButton.FindParentOfType<Window>()
                };

                Dialogs.TryPositionAndFitDialogIntoWindow(window);
                Grid grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(60) });

                Button button = new Button()
                {
                    Content = "Okay",
                    Margin = new Thickness(10)
                };
                button.Click += (s, args) =>
                {
                    window.Close();
                };
                Grid.SetRow(button, 1);

                WebBrowser wb = new WebBrowser()
                {
                    Width = Double.NaN,
                    Height = Double.NaN,
                };

                wb.NavigateToString(contentString);
                Grid.SetRow(wb, 0);
                window.Content = grid;
                grid.Children.Add(wb);
                grid.Children.Add(button);

                window.ShowDialog();
        }

        /// <summary>
        /// Recognitions: failed import message
        /// </summary>
        public static void MenuFileRecognitionsFailedImportedDialog(Window owner, RecognizerImportResultEnum importError)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Could not import the recognition data.";
            new MessageBox(title, owner)
            {
                Message =
                {
                    Title = title,
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
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Your recognition image file paths don't match your actual files";
            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    Title = title,
                    What = $"Do you know that your recognition image file paths don't match your existing files?{Environment.NewLine}" +
                           "This could be a problem unless its intentional.",
                    Reason = $"There are two likely reasons for this mismatch.{Environment.NewLine}" +
                             $"1. This is an unintentional error, possibly due to the location of your recognizer file.{Environment.NewLine}" +
                             $"   You should correct this.{Environment.NewLine}" +
                              "2. This is not an error, as you intentionally located your images elsewhere and will resolve this later.",
                    Solution = $"Depending on the reason, you may want to:{Environment.NewLine}" +
                               $"\u2022 'Cancel' to stop importing so you can check to see what is going on (see hint below).{Environment.NewLine}" +
                               "\u2022 'Okay' to import the recognitions anyways (if you know how to resolve this)",
                    Icon = MessageBoxImage.Question,
                    Hint = string.IsNullOrWhiteSpace(samplePath)
                        ? $"The problem is that the recognition file contains no files!{Environment.NewLine}You probably want to Cancel."
                        : $"An example file path found in the recogntion file is:{Environment.NewLine}" +
                          $"\u2022 {samplePath}{Environment.NewLine}" +
                          "Examine this to see if this file path matches an actual image file's location."
                }
            };
            return true == messageBox.ShowDialog();
        }

        #endregion

        #region Shortcut management
        // Using shortcut
        public static void ShortcutDetectedDialog(Window owner, string path)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = "A shortcut to a folder containing your images was detected.";
            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = null;
            MessageBox messageBox = new MessageBox(title, owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Information,
                    What = $"Timelapse detected a shortcut in your root folder, where it points to:{Environment.NewLine} • {path}",
                    Result = "Timelapse will search for images in the shortcut's destination folder.",
                    Reason = $"Timelapse normally searches for images in your root folder.{Environment.NewLine}"
                             + $"By including a shortcut, you can locate your images outside of the root folder instead of within it, e.g.,{Environment.NewLine}"
                             + $" • elsewhere on the local disk,{Environment.NewLine} • on a network drive, or{Environment.NewLine} • in the cloud.{Environment.NewLine}"
                             + "Your Timelapse .tdb, .ddb and Backup files will still be stored in the root folder you selected.",
                    Hint = "If you did want Timelapse to use images located in your root folder, remove the shortcut."
                },
                DontShowAgain =
                {
                    Visibility = Visibility.Visible
                }
            };
            messageBox.ShowDialog();
            if (messageBox.DontShowAgain.IsChecked.HasValue)
            {
                GlobalReferences.TimelapseState.SuppressShortcutDetectedPrompt = messageBox.DontShowAgain.IsChecked.Value;
            }
            Mouse.OverrideCursor = cursor;
        }

        public static void ShortcutMultipleShortcutsDetectedDialog(Window owner, List<string> paths)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));

            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = null;

            string title = "Multiple shortcuts to folders where detected.";
            string pathList = string.Empty;

            foreach (string path in paths)
            {
                pathList += $"{Environment.NewLine} • {path}";
            }

            MessageBox messageBox = new MessageBox(title, owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Error,
                    Problem =
                        $"Timelapse detected multiple shortcuts in your root folder,{Environment.NewLine}each pointing to a folder that could contain your images:{pathList}" +
                        $"{Environment.NewLine}However, Timelapse does not know which shortcut to use.",
                    Result = "Timelapse will abort this operation.",
                    Reason = $"A shortcut allows you to locate your images outside of the root folder instead of within it, e.g., {Environment.NewLine}"
                             + $" • elsewhere on the local disk,{Environment.NewLine} • on a network drive, or{Environment.NewLine} • in the cloud.{Environment.NewLine}"
                             + "Because several shortcuts were found, Timelapse does not know which shortcut's folder it should use.",
                    Solution = $" If you want Timelapse to use:{Environment.NewLine}" +
                               $" • a particular shortcut, remove the other shortcuts from the root folder{Environment.NewLine}" +
                               " • images located in your root folder, remove all shortcuts."
                },
            };
            messageBox.ShowDialog();
            Mouse.OverrideCursor = cursor;
        }
        #endregion

        #region AddaxAI-related dialogs
        public static void AddaxAICouldNotBeStarted(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "AddaxAI could not be started";

            new MessageBox(title, owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    Icon = MessageBoxImage.Information,
                    What = $"AddaxAI could not be started, but we don't know why.{Environment.NewLine}" +
                           $"\u2022 You can try to re-download and install AddaxAI again, or{Environment.NewLine}" +
                           "\u2022 Check for the access and execution permissions for AddaxAI (perhaps your computer setup has restrictions?)."
                }
            }.ShowDialog();
        }
        public static bool? AddaxAIAlreadyDownloaded(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "A version of AddaxAI is already installed ";

            return new MessageBox(title, owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    Icon = MessageBoxImage.Information,
                    What = $"A version of AddaxAI is already installed.{Environment.NewLine}" +
                           $"As installation takes about 10-15 minutes, you may want to avoid reinstalling AddaxAI unless:{Environment.NewLine}" +
                           $"\u2022 you know that a new version is available, or{Environment.NewLine}" +
                           $"\u2022 you are having issues with AddaxAI, and want to reinstall it,{Environment.NewLine}" +
                           "\u2022 you are unsure if this is the latest and greatest AddaxAI version.",
                    Hint = $"Select:{Environment.NewLine}" +
                           $"• 'Okay' to continue with the AddaxAI installation, or{Environment.NewLine}" +
                           "• 'Cancel' to return to Timelapse without re-installing AddaxAI."
                }
            }.ShowDialog();
        }

        public static bool? AddaxAINotInstalled(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "AddaxAI doesn't appear to be installed.";

            return new MessageBox(title, owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    Icon = MessageBoxImage.Information,
                    What = $"The AddaxAI image recognizer does not appear to be installed.{Environment.NewLine}" +
                           $"But if you want to try to run the uninstaller anyways, press Ok.{Environment.NewLine}",
                }
            }.ShowDialog();
        }

        public static bool? AddaxAIInstallationInformaton(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Install AddaxAI (from web site) - Overview ";

            return new MessageBox(title, owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    Icon = MessageBoxImage.Information,
                    What = $"An overview of the AddaxAI installation process.{Environment.NewLine}" +
                           $"\u2022 The AddaxAI Windows Installation web page will appear.{Environment.NewLine}" +
                           $"\u2022 Click its 'Download...' button, and then open the just-downloaded 'Install.bat' file in your Downloads folder.{Environment.NewLine}" +
                           $"\u2022 If a 'Windows protected your PC' dialog appears, select 'More Info' and 'Run anyway'.{Environment.NewLine}" +
                           $"\u2022 A window appears. Follow the prompts, if any. {Environment.NewLine}" +
                           $"\u2022 Installation takes ~5-15 minutes. {Environment.NewLine}" +
                           "\u2022 Lots of technical feedback will be displayed as the large Python Anaconda Data Science Platform is loaded.",
                    Hint = $"It takes time as AddaxAI relies on Python Anaconda Data Science Platform, which is a big.{Environment.NewLine}" +
                           "Try to avoid interrupting the installation, as cleaning up an aborted installation can be messy."
                }
            }.ShowDialog();
        }

        public static bool? AddaxAIApplicationInstructions(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "AddaxAI is starting up (about 5 - 20 seconds)...";

            return new MessageBox(title, owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Information,
                    What = $"The AddaxAI application should start up shortly. When it does:{Environment.NewLine}" +
                           $" 1. Select the model you want to use{Environment.NewLine}" +
                           $"    (the default model shows detections with broad animal/person/vehicle classifications){Environment.NewLine}" +
                           $" 2. Click 'Start processing', which will start processing your images.{Environment.NewLine}" +
                            " 3. You will be notified when your image recognition is completed.",
                    Hint = $"Be patient. Image recognition takes time.{Environment.NewLine}" +
                           $" • you may want to run this overnight if you have (say) tens or hundreds of thousands of images.{Environment.NewLine}" +
                           " • you can continue with your other work (including Timelapse work) while AddaxAI is running.",
                }
            }.ShowDialog();
        }

        #endregion

        #region CamtrapDP Dialogs
        public static bool? CamtrapDPDataPackageMissingRequiredFields(Window owner, List<string> missingMessages)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "CamtrapDP: Required fields are missing";
            string missingMessage = string.Empty;
            foreach (string str in missingMessages)
            {
                missingMessage += $"{Environment.NewLine}{str}";
            }

            return new MessageBox(title, owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Warning,
                    What = "CamtrapDP specifies various required fields, which don't appear to be filled in." +
                           missingMessage,
                    Result = "Other systems that expect these required fields may complain or fail.",
                    Hint = $"This is just a warning, as your data was still exported.{Environment.NewLine}" +
                           "You may want to go back and fill in those missing fields.",
                }
            }.ShowDialog();
        }

        public static bool? CamtrapDPSpatialCoverageInstructions(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "How to use spatial coverage";

            return new MessageBox(title, owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Information,
                    What = $"Spatial coverage, specified as GeoJson data, can be defined in a few ways.{Environment.NewLine}" +
                           $"1. From lat/long. Click to:{Environment.NewLine}" +
                           $"   • calculate a bounding box surrounding your deployments' latitude/longitude coordinates.{Environment.NewLine}" +
                           $"2. Via Geojson.io. Click to:{Environment.NewLine}" +
                           $"   • view and/or edit your current spatial coverage in a browser-based map;{Environment.NewLine}" +
                           $"   • then copy/paste the generated geojson text into the Timelapse spatial field.{Environment.NewLine}" +
                           $"3. From some other source (e.g., GIS package). {Environment.NewLine}" +
                           "   • paste geojson into the spatial coverage field",

                    Hint = $"The easiest approach is to:{Environment.NewLine}" +
                           $"   • click 'From lat/long'{Environment.NewLine}" +
                           $"   • click 'Via GeoJson.IO' to view the results{Environment.NewLine}"
                }
            }.ShowDialog();
        }
        #endregion
    }

}

