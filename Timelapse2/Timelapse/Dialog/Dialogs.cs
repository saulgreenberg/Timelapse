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
                TracePrint.PrintStackTrace("Window's owner property is null. Is a set of it prior to calling ShowDialog() missing?", 1);
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
            Screen screenInDpi = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(window).Handle);

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

        /// <summary>
        /// Confirm closing this template and creating a new one
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="templateFileName"></param>
        public static bool? CloseTemplateAndOpenNewTemplate(Window owner, string newTemplateFileName)
        {
            MessageBox messageBox = new MessageBox("Close this template and open another one", owner, MessageBoxButton.OKCancel);
            messageBox.Message.Icon = MessageBoxImage.Question;
            messageBox.Message.What = String.Format("Close the current template and open this one instead? {0}   {1} ", Environment.NewLine, newTemplateFileName);
            return messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: Cannot read/write file
        public static void FileCantOpen(Window owner, string path, bool isFile)
        {
            string entity = isFile ? "file" : "folder";
            // Tell the user we could not read or write the file
            string title = "Could not open the " + entity;
            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OK);

            messageBox.Message.What = "The " + entity + " could not be opened:" + Environment.NewLine;
            messageBox.Message.What += path;

            messageBox.Message.Reason = "There are many possible reasons, including:" + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 the folder may not be accessible or may not exist " + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 you may not have permission to access the " + entity + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 another application may be using the " + entity;

            messageBox.Message.Solution = "Check to see if: " + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 the folder exists or if you can create it" + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 you can create a file in that folder";
            if (isFile)
            {
                messageBox.Message.Solution += Environment.NewLine;
                messageBox.Message.Solution += "\u2022 you can open and close that file with another application" + Environment.NewLine;
                messageBox.Message.Solution += "\u2022 another application is using that file";
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
            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OKCancel);

            messageBox.Message.What = operationDescription + " will be applied only to a subset of your images." + Environment.NewLine;
            messageBox.Message.What += "Is this what you want?";

            messageBox.Message.Reason = String.Format("A 'selection' is active, where you are currently viewing {0}/{1} total files.{2}", filesSelectedCount, filesTotalCount, Environment.NewLine);
            messageBox.Message.Reason += "Only these selected images will be affected by this operation." + Environment.NewLine;
            messageBox.Message.Reason += "Data for other unselected images will be unaffected.";

            messageBox.Message.Solution = "Select " + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 'Ok' for Timelapse to continue to " + operationDescription + " for these selected files" + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 'Cancel' to abort";

            messageBox.Message.Hint = "This is not an error." + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 We are just reminding you that you have an active selection that is displaying only a subset of your images." + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 You can apply this operation to that subset ." + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 However, if you did want to do this operaton for all images, choose the 'Select|All files' menu option.";

            messageBox.Message.Icon = MessageBoxImage.Question;
            messageBox.DontShowAgain.Visibility = Visibility.Visible;

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
            string title = "Overwrite existing files?";
            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OKCancel);

            messageBox.Message.What = String.Format("Overwrite {0} files with the same name?", existingFilesCount);

            messageBox.Message.Reason = String.Format("The destination folder already has {0} files with the same name", existingFilesCount);

            messageBox.Message.Solution = "Select " + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 'Ok' for Timelapse to overwrite those files" + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 'Cancel' to abort";

            messageBox.Message.Icon = MessageBoxImage.Question;

            return (bool)messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: Missing dependencies
        public static void DependencyFilesMissingDialog(string applicationName)
        {
            // can't use DialogMessageBox to show this message as that class requires the Timelapse window to be displayed.
            string messageTitle = String.Format("{0} needs to be in its original downloaded folder.", applicationName);
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
            MessageBox messageBox = new MessageBox(title, owner);
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.Message.Title = title;
            messageBox.Message.Problem = "Timelapse has to shut down as one or more of your file paths are too long.";
            messageBox.Message.Solution = "\u2022 Shorten the path name by moving your image folder higher up the folder hierarchy, or" + Environment.NewLine + "\u2022 Use shorter folder or file names.";
            messageBox.Message.Reason = "Windows cannot perform file operations if the folder path combined with the file name is more than " + Constant.File.MaxPathLength.ToString() + " characters.";
            messageBox.Message.Result = "Timelapse will shut down until you fix this.";
            messageBox.Message.Hint = "Files created in your " + Constant.File.BackupFolder + " folder must also be less than " + Constant.File.MaxPathLength.ToString() + " characters.";
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

            string title = "Some of your Image File Path Names Were Too Long";
            MessageBox messageBox = new MessageBox(title, owner);
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.Message.Title = title;
            messageBox.Message.Problem = "Timelapse skipped reading some of your images in the folders below, as their file paths were too long.";
            if (folders.Count > 0)
            {
                messageBox.Message.Problem += "Those files are found in these folders:";
                foreach (string folder in folders)
                {
                    messageBox.Message.Problem += Environment.NewLine + "\u2022 " + folder;
                }
            }
            messageBox.Message.Reason = "Windows cannot perform file operations if the folder path combined with the file name is more than " + Constant.File.MaxPathLength.ToString() + " characters.";
            messageBox.Message.Solution = "Try reloading this image set after shortening the file path:" + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 shorten the path name by moving your image folder higher up the folder hierarchy, or" + Environment.NewLine + "\u2022 use shorter folder or file names.";

            messageBox.Message.Hint = "Files created in your " + Constant.File.BackupFolder + " folder must also be less than " + Constant.File.MaxPathLength.ToString() + " characters.";
            messageBox.ShowDialog();
        }

        // notify the user when the path is too long
        public static void TemplatePathTooLongDialog(Window owner, string templateDatabasePath)
        {
            MessageBox messageBox = new MessageBox("Timelapse could not open the template ", owner);
            messageBox.Message.Problem = "Timelapse could not open the Template File as its name is too long:" + Environment.NewLine;
            messageBox.Message.Problem += "\u2022 " + templateDatabasePath;
            messageBox.Message.Reason = "Windows cannot perform file operations if the folder path combined with the file name is more than " + Constant.File.MaxPathLength.ToString() + " characters.";
            messageBox.Message.Solution = "\u2022 Shorten the path name by moving your image folder higher up the folder hierarchy, or" + Environment.NewLine + "\u2022 Use shorter folder or file names.";
            messageBox.Message.Hint = "Files created in your " + Constant.File.BackupFolder + " folder must also be less than " + Constant.File.MaxPathLength.ToString() + " characters.";
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.ShowDialog();
        }

        // notify the user the template couldn't be loaded because its path is too long
        public static void DatabasePathTooLongDialog(Window owner, string databasePath)
        {
            MessageBox messageBox = new MessageBox("Timelapse could not load the database ", owner);
            messageBox.Message.Problem = "Timelapse could not load the Template File as its name is too long:" + Environment.NewLine;
            messageBox.Message.Problem += "\u2022 " + databasePath;
            messageBox.Message.Reason = "Windows cannot perform file operations if the folder path combined with the file name is more than " + Constant.File.MaxPathLength.ToString() + " characters.";
            messageBox.Message.Solution = "\u2022 Shorten the path name by moving your image folder higher up the folder hierarchy, or" + Environment.NewLine + "\u2022 Use shorter folder or file names.";
            messageBox.Message.Hint = "Files created in your " + Constant.File.BackupFolder + " folder must also be less than " + Constant.File.MaxPathLength.ToString() + " characters.";
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.ShowDialog();
        }

        // Warn the user if backups may not be made
        public static void BackupPathTooLongDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Timelapse may not be able to backup your files", owner);
            messageBox.Message.Problem = "Timelapse may not be able to backup your files as your file names are very long.";

            messageBox.Message.Reason = "Timelapse normally creates backups of your template, database, and csv files in the " + Constant.File.BackupFolder + " folder." + Environment.NewLine;
            messageBox.Message.Reason += "However, Windows cannot create those files if the " + Constant.File.BackupFolder + " folder path combined with the file name is more than " + Constant.File.MaxPathLength.ToString() + " characters.";

            messageBox.Message.Solution = "\u2022 Shorten the path name by moving your image folder higher up the folder hierarchy, or" + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 Use shorter folder or file names.";
            messageBox.Message.Hint = "You can still use Timelapse, but backup files may not be created.";
            messageBox.Message.Icon = MessageBoxImage.Warning;
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: .tdb is in a root or system folder
        public static void TemplateInDisallowedFolder(Window owner, bool isDrive, string path)
        {
            string title = "Your template file is in a problematic location";
            MessageBox messageBox = new MessageBox(title, owner);
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.Message.Title = title;
            messageBox.Message.Problem = "The location of your template is problematic. It should be in a normal folder." + Environment.NewLine;
            messageBox.Message.Reason = "Timelapse expects templates and images to be in a normal folder." + Environment.NewLine;
            messageBox.Message.Solution = "Create a new folder, and try moving your files to that folder.";
            if (isDrive)
            {
                messageBox.Message.Problem += String.Format("The issue is that your files are located in the top-level root drive '{0}' rather than a folder.{1}", path, Environment.NewLine);
                messageBox.Message.Problem += "Timelapse disallows this as the entire drive would be searched for images. ";
                messageBox.Message.Reason += "Timelapse cannot tell if this location is a massive drive containing all your files" + Environment.NewLine;
                messageBox.Message.Reason += "(which would take ages to search and would retrieve every single image on it)," + Environment.NewLine;
                messageBox.Message.Reason += "or, for example, an SD card that only contains your image set images.";
            }
            else
            {
                messageBox.Message.Problem += "The issue is that your files are located in a system or hidden folder:" + Environment.NewLine + "\u2022 " + path;
                messageBox.Message.Reason += "As system or hidden folders shouldn't normally contain user files, this could lead to future problems.";
                messageBox.Message.Solution += Environment.NewLine + "Or, you may be able to change the folder's attributes by selecting 'Properties' from";
                messageBox.Message.Solution += Environment.NewLine + "that folder's context menu, and reviewing the 'Attributes' settings on the 'General' tab";
            }
            messageBox.ShowDialog();
        }
        #endregion

        #region: MessageBox: template includes a control of an unknown type
        public static void TemplateIncludesControlOfUnknownType(Window owner, string unknownTypes)
        {
            Util.ThrowIf.IsNullArgument(owner, nameof(owner));
            // notify the user the template couldn't be loaded rather than silently doing nothing
            MessageBox messageBox = new MessageBox("Your template file has an issue.", owner);
            messageBox.Message.Problem = "Your template has an issue";
            messageBox.Message.Reason = "Your template contains data controls of unknown types." + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 " + unknownTypes + Environment.NewLine;
            messageBox.Message.Reason += "This could happen if you are trying to open a template with an old Timelapse version," + Environment.NewLine;
            messageBox.Message.Reason += "as newer Timelapse versions may have new types of controls.";

            messageBox.Message.Solution = "Download the latest verson of Timelapse and try reloading your files." + Environment.NewLine;
            messageBox.Message.Solution += "You can also send an explanatory note to saul@ucalgary.ca." + Environment.NewLine;
            messageBox.Message.Solution += "He will check those files to see if there is a fixable bug.";

            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: Corrupted template
        public static void TemplateFileNotLoadedAsCorruptDialog(Window owner, string templateDatabasePath)
        {
            Util.ThrowIf.IsNullArgument(owner, nameof(owner));
            // notify the user the template couldn't be loaded rather than silently doing nothing
            MessageBox messageBox = new MessageBox("Timelapse could not load the Template file.", owner);
            messageBox.Message.Problem = "Timelapse could not load the Template File :" + Environment.NewLine;
            messageBox.Message.Problem += "\u2022 " + templateDatabasePath;
            messageBox.Message.Reason = String.Format("The template ({0}) file may be corrupted, unreadable, or otherwise invalid.", Constant.File.TemplateDatabaseFileExtension);
            messageBox.Message.Solution = "Try one or more of the following:" + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 recreate the template, or use another copy of it." + Environment.NewLine;
            messageBox.Message.Solution += String.Format("\u2022 check if there is a valid template file in your {0} folder.", Constant.File.BackupFolder) + Environment.NewLine;
            messageBox.Message.Solution += String.Format("\u2022 email {0} describing what happened, attaching a copy of your {1} file.", Constant.ExternalLinks.EmailAddress, Constant.File.TemplateDatabaseFileExtension);

            messageBox.Message.Result = "Timelapse did not affect any of your other files.";
            if (owner.Name.Equals("Timelapse"))
            {
                // Only displayed in Timelapse, not the template editor
                messageBox.Message.Hint = "See if you can open and examine the template file in the Timelapse Template Editor." + Environment.NewLine;
                messageBox.Message.Hint += "If you can't, and if you don't have a copy elsewhere, you will have to recreate it." + Environment.NewLine;
                messageBox.Message.Hint += "You can also send an explanatory note to saul@ucalgary.ca." + Environment.NewLine;
                messageBox.Message.Hint += "He will check those files to see if there is a fixable bug.";
            }
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: Corrupted .ddb file (no primary key)
        public static void DatabaseFileNotLoadedAsCorruptDialog(Window owner, string ddbDatabasePath, bool isEmpty)
        {
            // notify the user the database couldn't be loaded because there is a problem with it
            MessageBox messageBox = new MessageBox("Timelapse could not load your database file.", owner);
            messageBox.Message.Problem = "Timelapse could not load your .ddb database file:" + Environment.NewLine;
            messageBox.Message.Problem += "\u2022 " + ddbDatabasePath;
            if (isEmpty)
            {
                messageBox.Message.Reason = "Your database file is empty. Possible reasons include:" + Environment.NewLine;
            }
            else
            {
                messageBox.Message.Reason = "Your database is unreadable or corrupted. Possible reasons include:" + Environment.NewLine;
            }
            messageBox.Message.Reason += "\u2022 Timelapse was shut down (or crashed) in the midst of:" + Environment.NewLine;
            messageBox.Message.Reason += "    - loading your image set for the first time, or" + Environment.NewLine;
            messageBox.Message.Reason += "    - writing your data into the file, or" + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 system, security or network  restrictions prohibited file reading and writing, or," + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 some other unkown reason.";
            messageBox.Message.Solution = "\u2022 If you have not analyzed any images yet, delete the .ddb file and try again." + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 Also, check for valid backups of your database in your " + Constant.File.BackupFolder + " folder that you can reuse.";
            messageBox.Message.Hint = "If you are stuck: Send an explanatory note to saul@ucalgary.ca." + Environment.NewLine;
            messageBox.Message.Hint += "He will check those files to see if there is a fixable bug.";
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: Not a Timelapse File
        public static void FileNotATimelapseFile(Window owner, string templateDatabasePath)
        {
            Util.ThrowIf.IsNullArgument(owner, nameof(owner));
            // notify the user the template couldn't be loaded rather than silently doing nothing
            MessageBox messageBox = new MessageBox("Could not load the Timelapse file.", owner);
            messageBox.Message.Problem = "The file does not appear to be a timelapse file:" + Environment.NewLine;
            messageBox.Message.Problem += "\u2022 " + templateDatabasePath;
            messageBox.Message.Reason = "Timelapse files are either:" + Environment.NewLine;
            messageBox.Message.Reason += String.Format("\u2022 template files with a suffix {0} {1}", Constant.File.TemplateDatabaseFileExtension, Environment.NewLine);
            messageBox.Message.Reason += String.Format("\u2022 data files with a suffix {0}", Constant.File.FileDatabaseFileExtension);
            messageBox.Message.Solution = String.Format("Load only template and database files with those suffixes.");
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.ShowDialog();
        }
        #endregion
        #region MessageBox: Not a template
        public static void TemplateFileNotATDB(Window owner, string templateDatabasePath)
        {
            Util.ThrowIf.IsNullArgument(owner, nameof(owner));
            // notify the user the template couldn't be loaded rather than silently doing nothing
            MessageBox messageBox = new MessageBox("Could not load the Timelapse Template file.", owner);
            messageBox.Message.Problem = "The file does not appear to be a template file:" + Environment.NewLine;
            messageBox.Message.Problem += "\u2022 " + templateDatabasePath;
            messageBox.Message.Reason = String.Format("Template files are identifed by the suffix {0} .", Constant.File.TemplateDatabaseFileExtension);
            messageBox.Message.Solution = String.Format("Load a valid template file ending in {0} .", Constant.File.TemplateDatabaseFileExtension);
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: Not a data file
        public static void DatabaseFileNotADDB(Window owner, string databasePath)
        {
            Util.ThrowIf.IsNullArgument(owner, nameof(owner));
            // notify the user the template couldn't be loaded rather than silently doing nothing
            MessageBox messageBox = new MessageBox("Could not load the Timelapse Database file.", owner);
            messageBox.Message.Problem = "The file does not appear to be a database file:" + Environment.NewLine;
            messageBox.Message.Problem += "\u2022 " + databasePath;
            messageBox.Message.Reason = String.Format("Database files are identifed by the suffix {0} .", Constant.File.FileDatabaseFileExtension);
            messageBox.Message.Solution = String.Format("Load a valid database file ending in {0} .", Constant.File.FileDatabaseFileExtension);
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: DataEntryHandler Confirmations / Warnings for Propagate, Copy Forward, Propagate to here
        /// <summary>
        /// Display a dialog box saying there is nothing to propagate. 
        /// </summary>
        public static void DataEntryNothingToPropagateDialog(Window owner)
        {

            MessageBox messageBox = new MessageBox("Nothing to Propagate to Here.", owner);
            messageBox.Message.Icon = MessageBoxImage.Exclamation;
            messageBox.Message.Reason = "All the earlier files have nothing in this field, so there are no values to propagate.";
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Display a dialog box saying there is nothing to copy forward. 
        /// </summary>
        public static void DataEntryNothingToCopyForwardDialog(Window owner)
        {
            // Display a dialog box saying there is nothing to propagate. Note that this should never be displayed, as the menu shouldn't be highlit if there is nothing to propagate
            // But just in case...
            MessageBox messageBox = new MessageBox("Nothing to copy forward.", owner);
            messageBox.Message.Icon = MessageBoxImage.Exclamation;
            messageBox.Message.Reason = "As you are on the last file, there are no files after this.";
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Ask the user to confirm value propagation from the last value
        /// </summary>
        public static bool? DataEntryConfirmCopyForwardDialog(Window owner, string text, int imagesAffected, bool checkForZero)
        {
            text = String.IsNullOrEmpty(text) ? String.Empty : text.Trim();

            MessageBox messageBox = new MessageBox("Please confirm 'Copy Forward' for this field...", owner, MessageBoxButton.YesNo);
            messageBox.Message.Icon = MessageBoxImage.Question;
            messageBox.Message.What = "Copy Forward is not undoable, and can overwrite existing values.";
            messageBox.Message.Result = "If you select yes, this operation will:" + Environment.NewLine;
            if (!checkForZero && String.IsNullOrEmpty(text))
            {
                messageBox.Message.Result += "\u2022 copy the (empty) value \u00AB" + text + "\u00BB in this field from here to the last file of your selected files.";
            }
            else
            {
                messageBox.Message.Result += "\u2022 copy the value \u00AB" + text + "\u00BB in this field from here to the last file of your selected files.";
            }
            messageBox.Message.Result += Environment.NewLine + "\u2022 over-write any existing data values in those fields";
            messageBox.Message.Result += Environment.NewLine + "\u2022 will affect " + imagesAffected.ToString() + " files.";
            return messageBox.ShowDialog();
        }

        /// <summary>
        /// Ask the user to confirm value propagation to all selected files
        /// </summary>
        public static bool? DataEntryConfirmCopyCurrentValueToAllDialog(Window owner, String text, int filesAffected, bool checkForZero)
        {
            text = String.IsNullOrEmpty(text) ? String.Empty : text.Trim();

            MessageBox messageBox = new MessageBox("Please confirm 'Copy to All' for this field...", owner, MessageBoxButton.YesNo);
            messageBox.Message.Icon = MessageBoxImage.Question;
            messageBox.Message.What = "Copy to All is not undoable, and can overwrite existing values.";
            messageBox.Message.Result = "If you select yes, this operation will:" + Environment.NewLine;
            if (!checkForZero && String.IsNullOrEmpty(text))
            {
                messageBox.Message.Result += "\u2022 clear this field across all " + filesAffected.ToString() + " of your selected files.";
            }
            else
            {
                messageBox.Message.Result += "\u2022 set this field to \u00AB" + text + "\u00BB across all " + filesAffected.ToString() + " of your selected files.";
            }
            messageBox.Message.Result += Environment.NewLine + "\u2022 over-write any existing data values in those fields";
            return messageBox.ShowDialog();
        }

        /// <summary>
        /// Ask the user to confirm value propagation from the last value
        /// </summary>
        public static bool? DataEntryConfirmPropagateFromLastValueDialog(Window owner, String text, int imagesAffected)
        {
            text = String.IsNullOrEmpty(text) ? String.Empty : text.Trim();
            MessageBox messageBox = new MessageBox("Please confirm 'Propagate to Here' for this field.", owner, MessageBoxButton.YesNo);
            messageBox.Message.Icon = MessageBoxImage.Question;
            messageBox.Message.What = "Propagate to Here is not undoable, and can overwrite existing values.";
            messageBox.Message.Reason = "\u2022 The last non-empty value \u00AB" + text + "\u00BB was seen " + imagesAffected.ToString() + " files back." + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 That field's value will be copied across all files between that file and this one of your selected files";
            messageBox.Message.Result = "If you select yes: " + Environment.NewLine;
            messageBox.Message.Result = "\u2022 " + imagesAffected.ToString() + " files will be affected.";
            return messageBox.ShowDialog();
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
            MessageBox messageBox = new MessageBox("Can't open a photo viewer.", owner);
            messageBox.Message.Icon = System.Windows.MessageBoxImage.Error;
            messageBox.Message.Reason = "You probably don't have a default program set up to display a photo viewer for " + extension + " files";
            messageBox.Message.Solution = "Set up a photo viewer in your Windows Settings." + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 go to 'Default apps', select 'Photo Viewer' and choose a desired photo viewer." + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 or right click on an " + extension + " file and set the default viewer that way";
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: Show Exception Reporting  
        // REPLACED BY ExceptionShutdownDialog  - DELETE after we are sure that other method works 
        /// <summary>
        /// Display a dialog showing unhandled exceptions. The dialog text is also placed in the clipboard so that the user can paste it into their email
        /// </summary>
        /// <param name="programName">The name of the program that generated the exception</param>
        /// <param name="e">the exception</param>
        /// <param name="owner">A window where the message will be positioned within it</param>
        //public static void ShowExceptionReportingDialog(string programName, UnhandledExceptionEventArgs e, Window owner)
        //{
        //    // Check the arguments for null 
        //    ThrowIf.IsNullArgument(e, nameof(e));

        //    // once .NET 4.5+ is used it's meaningful to also report the .NET release version
        //    // See https://msdn.microsoft.com/en-us/library/hh925568.aspx.
        //    string title = programName + " needs to close. Please report this error.";
        //    MessageBox exitNotification = new MessageBox(title, owner);
        //    exitNotification.Message.Icon = MessageBoxImage.Error;
        //    exitNotification.Message.Title = title;
        //    exitNotification.Message.Problem = programName + " encountered a problem, likely due to a bug. If you let us know, we will try and fix it. ";
        //    exitNotification.Message.What = "Please help us fix it! You should be able to paste the entire content of the Reason section below into an email to saul@ucalgary.ca , along with a description of what you were doing at the time.  To quickly copy the text, click on the 'Reason' details, hit ctrl+a to select all of it, ctrl+c to copy, and then email all that.";
        //    exitNotification.Message.Reason = String.Format("{0}, {1}, .NET runtime {2}{3}", typeof(TimelapseWindow).Assembly.GetName(), Environment.OSVersion, Environment.Version, Environment.NewLine);
        //    if (e.ExceptionObject != null)
        //    {
        //        exitNotification.Message.Reason += e.ExceptionObject.ToString();
        //    }
        //    exitNotification.Message.Result = String.Format("The data file is likely OK.  If it's not you can restore from the {0} folder.", Constant.File.BackupFolder);
        //    exitNotification.Message.Hint = "\u2022 If you do the same thing this'll probably happen again.  If so, that's helpful to know as well." + Environment.NewLine;

        //    // Modify text for custom exceptions
        //    Exception custom_excepton = (Exception)e.ExceptionObject;
        //    switch (custom_excepton.Message)
        //    {
        //        case Constant.ExceptionTypes.TemplateReadWriteException:
        //            exitNotification.Message.Problem =
        //                programName + "  could not read data from the template (.tdb) file. This could be because: " + Environment.NewLine +
        //                "\u2022 the .tdb file is corrupt, or" + Environment.NewLine +
        //                "\u2022 your system is somehow blocking Timelapse from manipulating that file (e.g., Citrix security will do that)" + Environment.NewLine +
        //                "If you let us know, we will try and fix it. ";
        //            break;
        //        default:
        //            exitNotification.Message.Problem = programName + " encountered a problem, likely due to a bug. If you let us know, we will try and fix it. ";
        //            break;
        //    }
        //    Clipboard.SetText(exitNotification.Message.Reason);
        //    exitNotification.ShowDialog();
        //}
        #endregion

        #region MessageBox: No Updates Available
        public static void NoUpdatesAvailableDialog(Window owner, string applicationName, Version currentVersionNumber)
        {
            MessageBox messageBox = new MessageBox(String.Format("No updates to {0} are available.", applicationName), owner);
            messageBox.Message.Reason = String.Format("You a running the latest version of {0}, version: {1}", applicationName, currentVersionNumber);
            messageBox.Message.Icon = MessageBoxImage.Information;
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: File Selection
        /// <summary>
        /// // No files were missing in the current selection
        /// </summary>
        public static void FileSelectionNoFilesAreMissingDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("No Files are Missing.", owner);
            messageBox.Message.Title = "No Files are Missing in the Current Selection.";
            messageBox.Message.Icon = MessageBoxImage.Information;
            messageBox.Message.What = "No files are missing in the current selection.";
            messageBox.Message.Reason = "All files in the current selection were checked, and all are present. None were missing.";
            messageBox.Message.Result = "No changes were made.";
            messageBox.ShowDialog();
        }

        public static void FileSelectionResettngSelectionToAllFilesDialog(Window owner, FileSelectionEnum selection)
        {
            // These cases are reached when 
            // 1) datetime modifications result in no files matching a custom selection
            // 2) all files which match the selection get deleted
            MessageBox messageBox = new MessageBox("Resetting selection to All files (no files currently match the current selection)", owner);
            messageBox.Message.Icon = MessageBoxImage.Information;
            messageBox.Message.Result = "The 'All files' selection will be applied, where all files in your image set are displayed.";

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
                default:
                    throw new NotSupportedException(String.Format("Unhandled selection {0}.", selection));
            }
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: MissingFilesNotFound / Missing Folders
        public static void MissingFileSearchNoMatchesFoundDialog(Window owner, string fileName)
        {
            string title = "Timelapse could not find any matches to " + fileName;
            Dialog.MessageBox messageBox = new Dialog.MessageBox(title, owner, MessageBoxButton.OK);

            messageBox.Message.What = "Timelapse tried to find the missing image with no success.";

            messageBox.Message.Reason = "Timelapse searched the other folders in this image set, but could not find another file that: " + Environment.NewLine;
            messageBox.Message.Reason += " - was named " + fileName + ", and  " + Environment.NewLine;
            messageBox.Message.Reason += " - was not already associated with another image entry.";

            messageBox.Message.Hint = "If the original file was:" + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 deleted, check your " + Constant.File.DeletedFilesFolder + " folder to see if its there." + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 moved outside of this image set, then you will have to find it and move it back in." + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 renamed, then you have to find it yourself and restore its original name." + Environment.NewLine + Environment.NewLine;
            messageBox.Message.Hint += "Of course, you can just leave things as they are, or delete this image's data field if it has little value to you.";

            messageBox.Message.Icon = MessageBoxImage.Question;
            messageBox.ShowDialog();
        }

        public static void MissingFoldersInformationDialog(Window owner, int count)
        {
            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = null;

            string title = count.ToString() + " of your folders could not be found";
            Dialog.MessageBox messageBox = new Dialog.MessageBox(title, owner, MessageBoxButton.OK);

            messageBox.Message.Problem = "Timelapse checked for the folders containing your image and video files, and noticed that " + count.ToString() + " are missing.";

            messageBox.Message.Reason = "These folders may have been moved, renamed, or deleted since Timelapse last recorded their location.";

            messageBox.Message.Solution = "If you want to try to locate missing folders and files, select: " + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 'Edit | Try to find missing folders...' to have Timelapse help locate those folders, or" + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 'Edit | Try to find this (and other) missing files...' to have Timelapse help locate one or more missing files in a particular folder.";

            messageBox.Message.Hint = "Everything will still work as normal, except that a 'Missing file' image will be displayed instead of the actual image." + Environment.NewLine;

            messageBox.Message.Hint += "Searching for the missing folders is optional.";

            messageBox.Message.Icon = MessageBoxImage.Exclamation;
            messageBox.ShowDialog();
            Mouse.OverrideCursor = cursor;
        }
        #endregion

        #region MessageBox: ImageSetLoading
        /// <summary>
        /// If there are multiple missing folders, it will generate multiple dialog boxes. Thus we explain what is going on.
        /// </summary>
        /// DEPRACATED - CAN DELETE
        //public static bool? ImageSetLoadingMultipleImageFoldersNotFoundDialog(Window owner, List<string> missingRelativePaths)
        //{

        //    if (missingRelativePaths == null)
        //    {
        //        // this should never happen
        //        missingRelativePaths = new List<string>();
        //    }
        //    MessageBox messageBox = new MessageBox("Multiple image folders cannot be found. Locate them?", owner, MessageBoxButton.OKCancel);
        //    messageBox.Message.Problem = "Timelapse could not locate the following image folders" + Environment.NewLine;
        //    foreach (string relativePath in missingRelativePaths)
        //    {
        //        messageBox.Message.Problem += "\u2022 " + relativePath + Environment.NewLine;
        //    }
        //    messageBox.Message.Solution = "OK raises one or more dialog boxes asking you to locate a particular missing folder." + Environment.NewLine;
        //    messageBox.Message.Solution += "Cancel will still display the image's data, along with a 'missing' image placeholder";
        //    messageBox.Message.Icon = MessageBoxImage.Question;
        //    return messageBox.ShowDialog();
        //}

        /// <summary>
        /// No images were found in the root folder or subfolders, so there is nothing to do
        /// </summary>
        public static void ImageSetLoadingNoImagesOrVideosWereFoundDialog(Window owner, string selectedFolderPath)
        {
            MessageBox messageBox = new MessageBox("No images or videos were found", owner, MessageBoxButton.OK);
            messageBox.Message.Problem = "No images or videos were found in this folder or its subfolders:" + Environment.NewLine;
            messageBox.Message.Problem += "\u2022 " + selectedFolderPath + Environment.NewLine;
            messageBox.Message.Reason = "Neither the folder nor its sub-folders contain:" + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 image files (ending in '.jpg') " + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 video files (ending in '.avi or .mp4')";
            messageBox.Message.Solution = "Timelapse aborted the load operation." + Environment.NewLine;
            messageBox.Message.Hint = "Locate your template in a folder containing (or whose subfolders contain) image or video files ." + Environment.NewLine;
            messageBox.Message.Icon = MessageBoxImage.Exclamation;
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: MenuFile
        /// <summary>
        /// No matching folders in the DB and the detector
        /// </summary>
        public static void MenuFileRecognitionDataNotImportedDialog(Window owner, string details)
        {
            MessageBox messageBox = new MessageBox("Recognition data not imported.", owner);
            messageBox.Message.Problem = "No recognition information was imported, as none of its image folder paths were found in your Database file." + Environment.NewLine;
            messageBox.Message.Problem += "Thus no recognition information could be assigned to your images.";
            messageBox.Message.Reason = "The recognizer may have been run on a folder containing various image sets, each in a sub-folder. " + Environment.NewLine;
            messageBox.Message.Reason += "For example, if the recognizer was run on 'AllFolders/Camera1/' but your template and database is in 'Camera1/'," + Environment.NewLine;
            messageBox.Message.Reason += "the folder paths won't match, since AllFolders/Camera1/ \u2260 Camera1/.";
            messageBox.Message.Solution = "Microsoft provides a program to extract a subset of recognitions in the Recognition file" + Environment.NewLine;
            messageBox.Message.Solution += "that you can use to extract recognitions matching your sub-folder: " + Environment.NewLine;
            messageBox.Message.Solution += "  http://aka.ms/cameratraps-detectormismatch";
            messageBox.Message.Result = "Recognition information was not imported.";
            messageBox.Message.Details = details;
            messageBox.ShowDialog();
        }

        /// <summary>
        ///  Some folders missing - show which folder paths in the DB are not in the detector
        /// </summary>
        public static void MenuFileRecognitionDataImportedOnlyForSomeFoldersDialog(Window owner, string details)
        {
            // Some folders missing - show which folder paths in the DB are not in the detector
            MessageBox messageBox = new MessageBox("Recognition data imported for only some of your folders.", owner);
            messageBox.Message.Icon = MessageBoxImage.Information;
            messageBox.Message.Problem = "Some of the sub-folders in your image set's Database file have no corresponding entries in the Recognition file." + Environment.NewLine;
            messageBox.Message.Problem += "While not an error, we just wanted to bring it to your attention.";
            messageBox.Message.Reason = "This could happen if you have added, moved, or renamed the folders since supplying the originals to the recognizer:" + Environment.NewLine;
            messageBox.Message.Result = "Recognition data will still be imported for the other folders.";
            messageBox.Message.Hint = "You can also view which images are missing recognition data by choosing" + Environment.NewLine;
            messageBox.Message.Hint += "'Select|Custom Selection...' and checking the box titled 'Show all files with no recognition data'";
            messageBox.Message.Details = details;
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Detections successfully imported message
        /// </summary>
        public static void MenuFileDetectionsSuccessfulyImportedDialog(Window owner, string details)
        {
            MessageBox messageBox = new MessageBox("Recognitions imported.", owner);
            messageBox.Message.Icon = MessageBoxImage.Information;
            messageBox.Message.Result = "Recognition data imported. You can select images matching particular recognitions by choosing 'Select|Custom Selection...'";
            messageBox.Message.Hint = "You can also view which images (if any) are missing recognition data by choosing" + Environment.NewLine;
            messageBox.Message.Hint += "'Select|Custom Selection...' and checking the box titled 'Show all files with no recognition data'";
            messageBox.Message.Details = details;
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Export data for this image set as a.csv file, but confirm, as only a subset will be exported since a selection is active
        /// </summary>
        public static bool? MenuFileExportCSVOnSelectionDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Exporting to a .csv file on a selected view...", owner, MessageBoxButton.OKCancel);
            messageBox.Message.What = "Only a subset of your data will be exported to the .csv file.";
            messageBox.Message.Reason = "As your selection (in the Selection menu) is not set to view 'All', ";
            messageBox.Message.Reason += "only data for these selected files will be exported. ";
            messageBox.Message.Solution = "If you want to export just this subset, then " + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 click Okay" + Environment.NewLine + Environment.NewLine;
            messageBox.Message.Solution += "If you want to export data for all your files, then " + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 click Cancel," + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 select 'All Files' in the Selection menu, " + Environment.NewLine;
            messageBox.Message.Solution += "\u2022 retry exporting your data as a .csv file.";
            messageBox.Message.Hint = "If you select 'Don't show this message this dialog can be turned back on via the Options |Show or hide... menu.";
            messageBox.Message.Icon = MessageBoxImage.Warning;
            messageBox.DontShowAgain.Visibility = Visibility.Visible;

            bool? exportCsv = messageBox.ShowDialog();
            if (messageBox.DontShowAgain.IsChecked.HasValue)
            {
                Util.GlobalReferences.TimelapseState.SuppressSelectedCsvExportPrompt = messageBox.DontShowAgain.IsChecked.Value;
            }
            return exportCsv;
        }

        /// <summary>
        /// Cant write the spreadsheet file
        /// </summary>
        public static void MenuFileCantWriteSpreadsheetFileDialog(Window owner, string csvFilePath, string exceptionName, string exceptionMessage)
        {
            MessageBox messageBox = new MessageBox("Can't write the spreadsheet file.", owner);
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.Message.Problem = "The following file can't be written: " + csvFilePath;
            messageBox.Message.Reason = "You may already have it open in Excel or another application.";
            messageBox.Message.Solution = "If the file is open in another application, close it and try again.";
            messageBox.Message.Hint = String.Format("{0}: {1}", exceptionName, exceptionMessage);
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Cant open the file using Excel
        /// </summary>
        public static void MenuFileCantOpenExcelDialog(Window owner, string csvFilePath)
        {
            MessageBox messageBox = new MessageBox("Can't open Excel.", owner);
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.Message.Problem = "Excel could not be opened to display " + csvFilePath;
            messageBox.Message.Solution = "Try again, or just manually start Excel and open the .csv file ";
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Give the user some feedback about the CSV export operation
        /// </summary>
        public static void MenuFileCSVDataExportedDialog(Window owner, string csvFileName)
        {
            // since the exported file isn't shown give the user some feedback about the export operation
            MessageBox csvExportInformation = new MessageBox("Data exported.", owner);
            csvExportInformation.Message.What = "The selected files were exported to " + csvFileName;
            csvExportInformation.Message.Result = String.Format("This file is overwritten every time you export it (backups can be found in the {0} folder).", Constant.File.BackupFolder);
            csvExportInformation.Message.Hint = "\u2022 You can open this file with most spreadsheet programs, such as Excel." + Environment.NewLine;
            csvExportInformation.Message.Hint += "\u2022 If you make changes in the spreadsheet file, you will need to import it to see those changes." + Environment.NewLine;
            csvExportInformation.Message.Hint += "\u2022 You can change the Date and Time formats by selecting the Options | Preferences menu." + Environment.NewLine;
            csvExportInformation.Message.Hint += "\u2022 If you select 'Don't show this message again', this dialog can be turned back on through the Options | Show or hide... menu.";
            csvExportInformation.Message.Icon = MessageBoxImage.Information;
            csvExportInformation.DontShowAgain.Visibility = Visibility.Visible;

            bool? result = csvExportInformation.ShowDialog();
            if (result.HasValue && result.Value && csvExportInformation.DontShowAgain.IsChecked.HasValue)
            {
                Util.GlobalReferences.TimelapseState.SuppressCsvExportDialog = csvExportInformation.DontShowAgain.IsChecked.Value;
            }
        }

        /// <summary>
        /// Tell the user how importing CSV files work. Give them the opportunity to abort.
        /// </summary>
        public static bool? MenuFileHowImportingCSVWorksDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("How importing .csv data works", owner, MessageBoxButton.OKCancel);
            messageBox.Message.What = "Importing data from a .csv (comma separated value) file follows the rules below.";
            messageBox.Message.Reason = "The first row in the CSV file must comprise column headers, where:" + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 'File' must be included." + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 'RelativePath' must be included if any of your images are in subfolders" + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 remaining headers should generally match your template's DataLabels" + Environment.NewLine;
            messageBox.Message.Reason += "Headers can be a subset of your template's DataLabels." + Environment.NewLine + Environment.NewLine;
            messageBox.Message.Reason += "Subsequent rows define the data for each file, where it must match the Header type:" + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 'File' data should match the name of the file you want to update." + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 'RelativePath' data should match the sub-folder path containing that file, if any" + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 'Counter' data must be blank, 0, or a positive integer. " + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 'DateTime', 'Date' and 'Time' data must follow the specific date/time formats (see File|Export data...). " + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 'Flag' and 'DeleteFlag' data must be 'true' or 'false'." + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 'FixedChoice' data should exactly match a corresponding list item defined in the template, or empty. " + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 'Folder' and 'ImageQuality' columns, if included, are skipped over.";
            messageBox.Message.Result = "Database values will be updated only for matching RelativePath/File entries. Non-matching entries are ignored.";
            messageBox.Message.Hint = "Warnings will be generated for non-matching CSV headers, which you can then fix." + Environment.NewLine;
            messageBox.Message.Hint += "If you select 'Don't show this message again', this dialog can be turned back on through the Options | Show or hide... menu.";
            messageBox.Message.Icon = MessageBoxImage.Warning;
            messageBox.DontShowAgain.Visibility = Visibility.Visible;

            bool? result = messageBox.ShowDialog();
            if (messageBox.DontShowAgain.IsChecked.HasValue)
            {
                Util.GlobalReferences.TimelapseState.SuppressCsvImportPrompt = messageBox.DontShowAgain.IsChecked.Value;
            }
            return result;
        }

        /// <summary>
        /// Can't import CSV File
        /// </summary>
        public static void MenuFileCantImportCSVFileDialog(Window owner, string csvFileName, List<string> resultAndImportErrors)
        {
            MessageBox messageBox = new MessageBox("Can't import the .csv file.", owner);
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.Message.Problem = String.Format("The file {0} could not be read.", csvFileName);
            messageBox.Message.Reason = "The .csv file is not compatible with the Timelapse template defining the current image set.";
            if (resultAndImportErrors != null)
            {
                messageBox.Message.Solution += "Change your .csv file to fix the errors below and try again.";
                string prefix;
                foreach (string importError in resultAndImportErrors)
                {
                    prefix = (importError[0] == '-') ? "   " : "\u2022 ";
                    messageBox.Message.Solution += Environment.NewLine + prefix + importError;
                }
            }
            messageBox.Message.Hint = "Timelapse checks the following when importing the .csv file:" + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 The first row is a header whose column names match the data labels in the .tdb template file" + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 Counter data values are numbers or blanks." + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 Flag and DeleteFlag values are either 'True' or 'False'." + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 Choice values are in that field's Choice list, defined in the template." + Environment.NewLine + Environment.NewLine;

            messageBox.Message.Hint += "While Timelapse will do the best it can to update your fields: " + Environment.NewLine; ;
            messageBox.Message.Hint += "\u2022 the csv row is skipped if its RelativePath/File location do not match a file in the Timelapse database ." + Environment.NewLine;
            messageBox.Message.Hint += "\u2022 the csv row's Date/Time is updated only if it is in the expected format (see Timelapse Reference Guide).";

            messageBox.Message.Result = "Importing of data from the CSV file was aborted. No changes were made.";

            messageBox.ShowDialog();
        }

        /// <summary>
        /// CSV file imported
        /// </summary>
        public static void MenuFileCSVFileImportedDialog(Window owner, string csvFileName, List<string> warnings)
        {
            MessageBox messageBox = new MessageBox("CSV file imported", owner);
            messageBox.Message.Icon = MessageBoxImage.Information;
            messageBox.Message.What = String.Format("The file {0} was successfully imported.", csvFileName);
            if (warnings.Count != 0)
            {
                messageBox.Message.Result = "However, here are some warnings that you may want to check.";
                string prefix;
                foreach (string warning in warnings)
                {
                    prefix = (warning[0] == '-') ? "   " : "\u2022 ";
                    messageBox.Message.Result += Environment.NewLine + prefix + warning;
                }
            }
            messageBox.Message.Hint = "\u2022 Check your data. If it is not what you expect, restore your data by using latest backup file in " + Constant.File.BackupFolder + ".";
            messageBox.ShowDialog();

        }

        /// <summary>
        /// Can't import the .csv file
        /// </summary>
        public static void MenuFileCantImportCSVFileDialog(Window owner, string csvFileName, string exceptionMessage)
        {
            MessageBox messageBox = new MessageBox("Can't import the .csv file.", owner);
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.Message.Problem = String.Format("The file {0} could not be opened.", csvFileName);
            messageBox.Message.Reason = "Most likely the file is open in another program. The technical reason is:" + Environment.NewLine;
            messageBox.Message.Reason += exceptionMessage;
            messageBox.Message.Solution = "If the file is open in another program, close it.";
            messageBox.Message.Result = "Importing of data from the CSV file was aborted. No changes were made.";
            messageBox.Message.Hint = "Is the file open in Excel?";
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Can't export the currently displayed image as a file
        /// </summary>
        public static void MenuFileCantExportCurrentImageDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Can't export this file!", owner);
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.Message.Problem = "Timelapse can't export a copy of the current image or video file.";
            messageBox.Message.Reason = "It is likely a corrupted or missing file.";
            messageBox.Message.Solution = "Make sure you have navigated to, and are displaying, a valid file before you try to export a copy of it.";
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Show a message that explains how merging databases works and its constraints. Give the user an opportunity to abort
        /// </summary>
        public static bool? MenuFileMergeDatabasesExplainedDialog(Window owner)
        {

            MessageBox messageBox = new MessageBox("Merge Databases.", owner, MessageBoxButton.OKCancel);
            messageBox.Message.Icon = MessageBoxImage.Question;
            messageBox.Message.Title = "Merge Databases Explained.";
            messageBox.Message.What = "Merging databases works as follows. Timelapse will:" + Environment.NewLine;
            messageBox.Message.What += "\u2022 ask you to locate a root folder containing a template (a.tdb file)," + Environment.NewLine;
            messageBox.Message.What += String.Format("\u2022 create a new database (.ddb) file in that folder, called {0},{1}", Constant.File.MergedFileName, Environment.NewLine);
            messageBox.Message.What += "\u2022 search for other database (.ddb) files in that folder's sub-folders, " + Environment.NewLine;
            messageBox.Message.What += "\u2022 try to merge all data found in those found databases into the new database.";
            messageBox.Message.Details = "\u2022 All databases must be based on the same template, otherwise the merge will fail." + Environment.NewLine;
            messageBox.Message.Details += "\u2022 Databases found in the Backup folders are ignored." + Environment.NewLine;
            messageBox.Message.Details += "\u2022 Detections and Classifications (if any) are merged; categories are taken from the first database found with detections." + Environment.NewLine;
            messageBox.Message.Details += "\u2022 The merged database is independent of the found databases: updates will not propagate between them." + Environment.NewLine;
            messageBox.Message.Details += "\u2022 The merged database is a normal Timelapse database, which you can open and use as expected.";
            messageBox.Message.Hint = "Press Ok to continue with the merge, otherwise Cancel.";
            messageBox.DontShowAgain.Visibility = Visibility.Visible;
            messageBox.ShowDialog();

            if (messageBox.DontShowAgain.IsChecked.HasValue)
            {
                Util.GlobalReferences.TimelapseState.SuppressMergeDatabasesPrompt = messageBox.DontShowAgain.IsChecked.Value;
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
            MessageBox messageBox = new MessageBox("Merge Databases Results.", owner);
            messageBox.Message.Icon = MessageBoxImage.Error;
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
                messageBox.Message.What += String.Format("{0}{0}Errors:", Environment.NewLine);
                foreach (string error in errorMessages.Errors)
                {
                    messageBox.Message.What += String.Format("{0}\u2022 {1},", Environment.NewLine, error);
                }
            }
            if (errorMessages.Warnings.Count != 0)
            {
                messageBox.Message.What += String.Format("{0}{0}Warnings:", Environment.NewLine);
            }
            foreach (string warning in errorMessages.Warnings)
            {
                messageBox.Message.What += String.Format("{0}\u2022 {1},", Environment.NewLine, warning);
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
            MessageBox messageBox = new MessageBox("Duplicate this record - What it is for, and caveats", owner, MessageBoxButton.OKCancel);
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
                messageBox.Message.What += "Duplicating a record will create a new copy of the current record populated with its default values." + Environment.NewLine;
                messageBox.Message.What += "Duplicates provide you with the ability to have the same field describe multiple things in your image." + Environment.NewLine + Environment.NewLine;
                messageBox.Message.What += "For example, let's say you have a Choice box called 'Species' used to identify animals in your image." + Environment.NewLine;
                messageBox.Message.What += "If more than one animal is in the image, you can use the original image to record the first species (e.g., Deer)" + Environment.NewLine;
                messageBox.Message.What += "and then use one (or more) dupicate records to record the other species that are present (e.g., Elk)" + Environment.NewLine + Environment.NewLine;
                messageBox.Message.What += "If you export your data to a CSV file, each duplicates will appear in its own row ";

                messageBox.Message.Hint = "Duplicates come with several caveats." + Environment.NewLine;
                messageBox.Message.Hint += "\u2022 Use 'Sort | Relative Path + Date Time (default)' to ensure that duplicates appear in sequence." + Environment.NewLine;
                messageBox.Message.Hint += "\u2022 Duplicates can only be created in the main view, not in the overview." + Environment.NewLine;
                messageBox.Message.Hint += "\u2022 Duplicates in the exported CSV file are identifiable as rows with the same relative path and file name.";

                messageBox.Message.Icon = MessageBoxImage.Information;
                messageBox.DontShowAgain.Visibility = Visibility.Visible;
            }
            bool? result = messageBox.ShowDialog();
            if (messageBox.DontShowAgain.IsChecked.HasValue && showProblemDescriptionOnly == false)
            {
                Util.GlobalReferences.TimelapseState.SuppressHowDuplicatesWork = messageBox.DontShowAgain.IsChecked.Value;
            }
            return result;
        }

        public static void MenuEditCouldNotImportQuickPasteEntriesDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Could not import QuickPaste entries", owner);
            messageBox.Message.Problem = "Timelapse could not find any QuickPaste entries in the selected database";
            messageBox.Message.Reason = "When an analyst creates QuickPaste entries, those entries are stored in the database file " + Environment.NewLine;
            messageBox.Message.Reason += "associated with the image set being analyzed. Since none where found, " + Environment.NewLine;
            messageBox.Message.Reason += "its likely that no one had created any quickpaste entries when analyzing that image set.";
            messageBox.Message.Hint = "Perhaps they are in a different database?";
            messageBox.Message.Icon = MessageBoxImage.Information;
            messageBox.ShowDialog();
        }
        /// <summary>
        /// There are no displayable images, and thus no metadata to choose from
        /// </summary>
        public static void MenuEditPopulateDataFieldWithMetadataDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Populate a data field with image metadata of your choosing.", owner);
            messageBox.Message.Problem = "Timelapse can't extract any metadata, as the currently displayed image or video is missing or corrupted." + Environment.NewLine;
            messageBox.Message.Reason = "Timelapse tries to examines the currently displayed image or video for its metadata.";
            messageBox.Message.Hint = "Navigate to a displayable image or video, and try again.";
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.ShowDialog();
        }

        public static void MenuEditRereadDateTimesFromMetadataDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Re-read date and times from a metadata field of your choosing.", owner);
            messageBox.Message.Problem = "Timelapse can't extract any metadata, as the currently displayed image or video is missing or corrupted." + Environment.NewLine;
            messageBox.Message.Reason = "Timelapse tries to examines the currently displayed image or video for its metadata.";
            messageBox.Message.Hint = "Navigate to a displayable image or video, and try again.";
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.ShowDialog();
        }

        public static void MenuEditNoFilesMarkedForDeletionDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("No files are marked for deletion", owner);
            messageBox.Message.Problem = "You are trying to delete files marked for deletion, but no files have their 'Delete?' field checked.";
            messageBox.Message.Hint = "If you have files that you think should be deleted, check their Delete? field.";
            messageBox.Message.Icon = MessageBoxImage.Information;
            messageBox.ShowDialog();
        }

        public static void MenuEditNoFoldersAreMissing(Window owner)
        {
            MessageBox messageBox = new MessageBox("No folders appear to be missing", owner);
            messageBox.Message.What = "You asked to to find any missing folders, but none appear to be missing.";
            messageBox.Message.Hint = "You don't normally have to do this check yourself, as a check for missing folders is done automatically whenever you start Timelapse.";
            messageBox.Message.Icon = MessageBoxImage.Information;
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: MenuOptions
        public static void MenuOptionsCantPopulateDataFieldWithEpisodeAsNoFilesDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Cannot populate a field with Episode data", owner);
            messageBox.Message.Problem = "Timelapse cannot currently populate any fields with Episode data." + Environment.NewLine;
            messageBox.Message.Reason = "There are no files in the current selection.";
            messageBox.Message.Hint = "Expand the current selection, or add some images or videos. Then try again.";
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.ShowDialog();
        }

        public static void MenuOptionsCantPopulateDataFieldWithEpisodeAsNoNoteFields(Window owner)
        {
            MessageBox messageBox = new MessageBox("Cannot populate a field with Episode data", owner);
            messageBox.Message.Problem = "Timelapse cannot currently populate any fields with Episode data." + Environment.NewLine;
            messageBox.Message.Reason = "Episode data would be put in a Note field, but none of your fields are Notes.";
            messageBox.Message.Hint = "Modify your template .tdb file to include a Note field using the Timelapse Template Editor." + Environment.NewLine;
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.ShowDialog();
        }

        public static bool MenuOptionsCantPopulateDataFieldWithEpisodeAsSortIsWrong(Window owner, bool searchTermsOk, bool sortTermsOk)
        {
            MessageBox messageBox = new MessageBox("You may not want to populate this field with Episode data", owner, MessageBoxButton.OKCancel);
            messageBox.Message.Problem = "You may not want to populate this field with Episode data.";
            if (!searchTermsOk)
            {
                if (!sortTermsOk)
                {
                    messageBox.Message.Reason += "1. ";
                }
                messageBox.Message.Reason += "Your current file selection includes search terms that may omit files in an Episode.";
                messageBox.Message.Hint += "Use the Select menu to select either:" + Environment.NewLine;
                messageBox.Message.Hint += " - All files, or " + Environment.NewLine;
                messageBox.Message.Hint += " - All files in a folder and its subfolders";
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
                messageBox.Message.Reason += "Your files should be sorted in ascending date order for this to make sense.";
                messageBox.Message.Hint += "Use the Sort menu to sort either by:" + Environment.NewLine;
                messageBox.Message.Hint += " - RelativePath then DateTime (both in ascending order), or " + Environment.NewLine;
                messageBox.Message.Hint += " - DateTime only  (in ascending order)";
            }

            messageBox.Message.Solution = "Select:" + Environment.NewLine;
            messageBox.Message.Solution += "- Okay to populate this field anyways, or " + Environment.NewLine;
            messageBox.Message.Solution += "- Cancel to abort populating this field with episode data";
            messageBox.Message.Icon = MessageBoxImage.Warning;
            return messageBox.ShowDialog() == true;
        }

        public static void MenuOptionsCantPopulateDataFieldWithEpisodeAsSortIsWrongOriginal(Window owner, bool searchTermsOk, bool sortTermsOk)
        {
            MessageBox messageBox = new MessageBox("Cannot populate a field with Episode data", owner);
            messageBox.Message.Problem = "Timelapse cannot currently populate any fields with Episode data." + Environment.NewLine;
            if (!searchTermsOk)
            {
                if (!sortTermsOk)
                {
                    messageBox.Message.Reason += "1. ";
                }
                messageBox.Message.Reason += "Your current file selection includes search terms that may omit files in an Episode.";
                messageBox.Message.Hint += "Use the Select menu to select either:" + Environment.NewLine;
                messageBox.Message.Hint += " - All files, or " + Environment.NewLine;
                messageBox.Message.Hint += " - All files in a folder and its subfolders";
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
                messageBox.Message.Hint += "Use the Sort menu to sort either by:" + Environment.NewLine;
                messageBox.Message.Hint += " - RelativePath then DateTime (both in ascending order), or " + Environment.NewLine;
                messageBox.Message.Hint += " - DateTime only  (in ascending order)";
            }
            messageBox.Message.Icon = MessageBoxImage.Error;
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: related to DateTime
        public static void DateTimeNewTimeShouldBeLaterThanEarlierTimeDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Your new time has to be later than the earliest time", owner);
            messageBox.Message.Icon = MessageBoxImage.Exclamation;
            messageBox.Message.Problem = "Your new time has to be later than the earliest time   ";
            messageBox.Message.Reason = "Even the slowest clock gains some time.";
            messageBox.Message.Solution = "The date/time was unchanged from where you last left it.";
            messageBox.Message.Hint = "The image on the left shows the earliest time recorded for images in this filtered view  shown over the left image";
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: related to Arguments to start a particular template or to constrain to a particular relative path
        // Tell the user that Timelapse is currently restricted to the folder designated by a particulare relative path
        public static void ArgumentRelativePathDialog(Window owner, string folderName)
        {
            string title = "Timelapse is currently restricted to the folder: '" + folderName + "'";
            Dialog.MessageBox messageBox = new Dialog.MessageBox(title, owner, MessageBoxButton.OK);

            messageBox.Message.What = title + Environment.NewLine;
            messageBox.Message.What += "This means that:" + Environment.NewLine;
            messageBox.Message.What += "\u2022 you will only be able to view and analyze files in that folder and its subfolders" + Environment.NewLine;
            messageBox.Message.What += "\u2022 any reference by Timelapse to 'All files' means 'All files in the folder: " + folderName + "'" + Environment.NewLine; ;
            messageBox.Message.What += "\u2022 to avoid confusion, you will not be able to open a different image set in this session";

            messageBox.Message.Reason = "Timelapse was started with the instruction to restrict itself to the folder: '" + folderName + "'" + Environment.NewLine;
            messageBox.Message.Reason += "This is usually done to narrow analysis to a particular subset of files of interest";

            messageBox.Message.Icon = MessageBoxImage.Information;
            messageBox.ShowDialog();
        }

        // Tell the user that Timelapse could not open the template specified in the argument
        public static void ArgumentTemplatePathDialog(Window owner, string fileName, string relativePathArgument)
        {
            string title = "Timelapse could not open the template";
            Dialog.MessageBox messageBox = new Dialog.MessageBox(title, owner, MessageBoxButton.OK);

            messageBox.Message.What = title + Environment.NewLine;
            messageBox.Message.What += "     '" + fileName + "'" + Environment.NewLine + Environment.NewLine;
            messageBox.Message.What += "Consequently," + Environment.NewLine;
            messageBox.Message.What += "\u2022 the instruction to use that template is ignored.";
            if (!String.IsNullOrWhiteSpace(relativePathArgument))
            {
                messageBox.Message.What += Environment.NewLine + "\u2022 the additional instruction to limit analysis to the subfolder " + "'" + relativePathArgument + "'" + " is also ignored ";
            }
            messageBox.Message.Reason = "Timelapse was started with instructions to open the template indicated above" + Environment.NewLine;
            if (!String.IsNullOrWhiteSpace(relativePathArgument))
            {
                messageBox.Message.Reason += "and to limit analysis to a particular subfolder." + Environment.NewLine; ;
            }
            messageBox.Message.Reason += "However, that template either does not exist or could not be accessed.";

            messageBox.Message.Icon = MessageBoxImage.Information;
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

            Dialog.MessageBox messageBox = new Dialog.MessageBox(title, owner, MessageBoxButton.OK);
            messageBox.Message.What = "For Timelapse to expand the search to include episodes, you must have a data field populated" + Environment.NewLine
                                        + "with your files' episode data, where the episode data is in the expected format." + Environment.NewLine
                                        + "None of your data fields, at least for the current file, includes the expected episode data.";

            messageBox.Message.Reason += "When you choose this option, Timelapse searches for episodes having at least one file " + Environment.NewLine;
            messageBox.Message.Reason += "matching your search criteria. If so, all files contained in those episodes are then displayed." + Environment.NewLine + Environment.NewLine;
            messageBox.Message.Reason += "For this to work properly, one of your data fields must have been filled in using " + Environment.NewLine;
            messageBox.Message.Reason += "Edit | Populate a field with episode data..., where:" + Environment.NewLine;
            messageBox.Message.Reason += " \u2022 the episode format includes the episode sequence number as its prefix, e.g. 25:1|3," + Environment.NewLine;
            messageBox.Message.Reason += " \u2022 all currently selected files were populated with Episode information at the same time.";

            messageBox.Message.Hint = "See the Timelapse Reference Guide about how this operation works, what is required, and what it is for.";

            messageBox.Message.Icon = MessageBoxImage.Warning;
            messageBox.ShowDialog();
        }
        #endregion

        #region MessageBox: opening messages when Timelapse is started
        /// <summary>
        /// Give the user various opening mesages
        /// </summary>
        public static void OpeningMessage(Window owner)
        {
            MessageBox openingMessage = new MessageBox("Opening Message ...", owner);
            openingMessage.Message.What = "This update ..." + Environment.NewLine;
            openingMessage.Message.What += "stuff" + Environment.NewLine;
            openingMessage.Message.What += "more stuff";

            openingMessage.Message.Reason = "A reason" + Environment.NewLine;
            openingMessage.Message.Reason += "\u2022 Point, " + Environment.NewLine;
            openingMessage.Message.Reason += "\u2022 Another point";

            openingMessage.Message.Icon = MessageBoxImage.Information;
            openingMessage.DontShowAgain.Visibility = Visibility.Visible;

            bool? result = openingMessage.ShowDialog();
            if (result.HasValue && result.Value && openingMessage.DontShowAgain.IsChecked.HasValue)
            {
                Util.GlobalReferences.TimelapseState.SuppressOpeningMessageDialog = openingMessage.DontShowAgain.IsChecked.Value;
            }
        }
        #endregion

        #region Dialogs dealing with Timelapse being opened in a viewonly state
        /// Give the user various opening mesages
        public static void OpeningMessageViewOnly(Window owner)
        {
            MessageBox openingMessage = new MessageBox("You are using the Timelapse 'View only' version ...", owner);
            openingMessage.Message.What = "You started the view-only version of Timelapse. " + Environment.NewLine;
            openingMessage.Message.What += "\u2022 You can open and view existing images, videos and any previously entered data." + Environment.NewLine;
            openingMessage.Message.What += "\u2022 You will not be able to edit or alter that data, or create a new image set." + Environment.NewLine;
            openingMessage.Message.Reason = "This Timelapse version is handy if you only want to view an image set and its data," + Environment.NewLine;
            openingMessage.Message.Reason += "as it ensures that no accidental changes to the data will be made.";
            openingMessage.Message.Hint = "If you want to edit data, then you should start the normal Timelapse program.";
            openingMessage.Message.Details += "Menu items that alter data are hidden from view, and data entry controls are disabled.";
            openingMessage.Message.Icon = MessageBoxImage.Information;
            openingMessage.ShowDialog();
        }

        public static void ViewOnlySoDatabaseCannotBeCreated(Window owner)
        {
            MessageBox openingMessage = new MessageBox("You are using the Timelapse 'View only' version. ", owner);
            openingMessage.Message.What = "Creating a new image set is not allowed when Timelapse is started as view-only. " + Environment.NewLine;
            openingMessage.Message.Reason = "You tried to open a template on an image set that has no data associated with it." + Environment.NewLine;
            openingMessage.Message.Reason += "Timelapse normally looks for images in this folder, and creates a database holding data for each image." + Environment.NewLine; ;
            openingMessage.Message.Reason += "In view-only mode, creating a new database is not permitted";
            openingMessage.Message.Hint = "If you want to load these images for the first time, then you should start the normal Timelapse program.";
            openingMessage.Message.Icon = MessageBoxImage.Warning;
            openingMessage.ShowDialog();
        }
        #endregion

        #region DialogIsFileValid checks for valid database file and displays appropriate dialog if it isn't
        public static bool DialogIsFileValid(Window owner, string filePath)
        {
            switch (Util.FilesFolders.QuickCheckDatabaseFile(filePath))
            {
                case DatabaseFileErrorsEnum.Ok:
                    return true;
                case DatabaseFileErrorsEnum.PreVersion2300:
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
                default:
                    return true;
            }
        }
        #endregion

        #region Testing messages for development
        public static void RandomMessage(Window owner, string message)
        {
            // since the exported file isn't shown give the user some feedback about the export operation
            MessageBox openingMessage = new MessageBox("Timelapse message...", owner);
            openingMessage.Message.What = message;
            openingMessage.Message.Icon = MessageBoxImage.Information;
            openingMessage.DontShowAgain.Visibility = Visibility.Visible;

            bool? result = openingMessage.ShowDialog();
            if (result.HasValue && result.Value && openingMessage.DontShowAgain.IsChecked.HasValue)
            {
                Util.GlobalReferences.TimelapseState.SuppressOpeningMessageDialog = openingMessage.DontShowAgain.IsChecked.Value;
            }
        }
        #endregion
    }
}
