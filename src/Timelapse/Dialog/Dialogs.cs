using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms; // Still needed for Screen API (multi-monitor support)
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Util;
using TimelapseTemplateEditor;
using TimelapseWpf.Toolkit;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using Clipboard = System.Windows.Clipboard;
using Control = Timelapse.Constant.Control;
using Cursor = System.Windows.Input.Cursor;
using File = Timelapse.Constant.File;
// Use WPF native file dialogs instead of Windows Forms
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Rectangle = System.Drawing.Rectangle;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using UnhandledExceptionEventArgs = System.UnhandledExceptionEventArgs;
using WebBrowser = System.Windows.Controls.WebBrowser;

namespace Timelapse.Dialog
{
    public static class Dialogs
    {
        #region Common test strings
        private static readonly string backupsLinkAndDirections =
            $"See the [link:{Constant.ExternalLinks.TimelapseFAQPage}|Timelapse Web site: FAQ page (Timelapse Crashes and Backups)] for how to do this.";

        private static readonly string dontShowMessageAgainInstructions =
            "Click [i]Don't show this message again[/i] to hide this message. Select [b]Options|Show or hide...[/b] to unhide it.";

        private static readonly string emailSaulForHelp =
            $"email [link: mailto:{ExternalLinks.EmailAddress}|{ExternalLinks.EmailAddress}] and explain what happened";

        private static readonly string downloadTimelapseInstructions =
            $"Download the latest version from the [link:{Constant.ExternalLinks.TimelapseDownloadPage}|Timelapse Downloads page]";

        private static readonly string mergingFilesInstructions =
            $"Consider reading about [b]Merging files[/b] in the [link:{Constant.ExternalLinks.TimelapseGuideReference}|Timelapse Reference Guide]";
        #endregion

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
            Rectangle windowPositionInDpi = new(
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
        // Prompt the user for a file location via an an open file dialog. Set selectedFilePath.
        // <returns>True if the user indicated one, else false. selectedFilePath contains the selected path, if any, otherwise null </returns>
        public static bool TryGetFileFromUserUsingOpenFileDialog(string title, string defaultFilePath, string filter, string defaultExtension, out string selectedFilePath, Window owner = null)
        {
            // Get the template file, which should be located where the images reside
            // Note: Microsoft.Win32 dialogs don't implement IDisposable, so no 'using' statement needed
            OpenFileDialog openFileDialog = new()
            {
                Title = title,
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false,
                // Note: AutoUpgradeEnabled removed - Microsoft.Win32 dialogs use modern Vista+ style by default
                DefaultExt = defaultExtension,
                Filter = filter
            };

            // If (say) you had previously opened a file in a drive (say F:) and that drive was later removed,
            // Windows would crash when trying to open the dialog. So we check that the drive exists.
            // If it doesn't, we default to My Documents.Oddly, windows doesn't do this drive check itself, while
            // it does the check for folders
            if (string.IsNullOrWhiteSpace(defaultFilePath) || false == Path.Exists(Path.GetPathRoot(defaultFilePath)))
            {
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            else
            {
                openFileDialog.InitialDirectory = Path.GetDirectoryName(defaultFilePath) ?? string.Empty;
                openFileDialog.FileName = Path.GetFileName(defaultFilePath);
            }

            // ShowDialog with optional owner parameter for proper modal behavior
            bool? result = owner != null ? openFileDialog.ShowDialog(owner) : openFileDialog.ShowDialog();
            if (result == true) // WPF dialogs return bool? instead of DialogResult
            {
                selectedFilePath = openFileDialog.FileName;
                return true;
            }

            selectedFilePath = null;
            return false;
        }

        /// <summary>
        /// Prompt the user for a folder location via an an open file dialog. 
        /// </summary>
        /// <returns>The selected path, otherwise null </returns>
        public static bool TryGetFolderFromUserUsingOpenFileDialog(string title, string initialFolder, out string selectedFolderPath)
        {
            selectedFolderPath = string.Empty;
            using CommonOpenFileDialog folderSelectionDialog = new();
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

            CommonFileDialogFilter filter = new(extensionInUsersLanguage, extension)
            {
                ShowExtensions = true
            };

            using CommonOpenFileDialog fileSelectionDialog = new();
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

            using CommonOpenFileDialog folderSelectionDialog = new();
            folderSelectionDialog.Title = "Locate folder" + folderNameToLocate + "...";
            folderSelectionDialog.DefaultDirectory = initialFolder;
            folderSelectionDialog.IsFolderPicker = true;
            folderSelectionDialog.Multiselect = false;
            folderSelectionDialog.InitialDirectory = folderSelectionDialog.DefaultDirectory;
            folderSelectionDialog.FolderChanging += FolderSelectionDialog_FolderChanging;
            if (folderSelectionDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                // Trim the root folder path from the folder name to produce a relative path. 
                return (folderSelectionDialog.FileName.Length > initialFolder.Length) ? folderSelectionDialog.FileName[(initialFolder.Length + 1)..] : string.Empty;
            }

            return null;
        }

        // Limit the folder selection to only those that are sub-folders of the folder path
        private static void FolderSelectionDialog_FolderChanging(object sender, CommonFileDialogFolderChangeEventArgs e)
        {
            if (sender is not CommonOpenFileDialog dialog)
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

        // Prompt the user for a file location via an an save file dialog. Set selectedFilePath.
        public static bool TryGetFileFromUserUsingSaveFileDialog(string title, string defaultFilePath, string filter, string defaultExtension, out string selectedFilePath, Window owner = null, bool overwritePrompt = true)
        {
            // Get the template file, which should be located where the images reside
            // Note: Microsoft.Win32 dialogs don't implement IDisposable, so no 'using' statement needed
            SaveFileDialog saveFileDialog = new()
            {
                Title = title,
                CheckFileExists = false,
                CheckPathExists = true,
                OverwritePrompt = overwritePrompt,
                // Note: AutoUpgradeEnabled removed - Microsoft.Win32 dialogs use modern Vista+ style by default
                DefaultExt = defaultExtension,
                Filter = filter
            };
            if (string.IsNullOrWhiteSpace(defaultFilePath))
            {
                saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            else
            {
                saveFileDialog.InitialDirectory = Path.GetDirectoryName(defaultFilePath) ?? string.Empty;
                saveFileDialog.FileName = Path.GetFileName(defaultFilePath);
            }

            // ShowDialog with optional owner parameter for proper modal behavior
            bool? result = owner != null ? saveFileDialog.ShowDialog(owner) : saveFileDialog.ShowDialog();
            if (result == true) // WPF dialogs return bool? instead of DialogResult
            {
                selectedFilePath = saveFileDialog.FileName;
                return true;
            }

            selectedFilePath = null;
            return false;
        }

        #endregion

        #region DragDrop Close template and open a new template

        // Confirm closing this template and creating a new one
        public static bool? CloseTemplateAndOpenNewTemplate(Window owner, string newTemplateFileName)
        {
            var dialog = new FormattedDialog()
            {
                Owner = owner,
                DialogTitle = "Close this template and open another one",
                What = $"Close the current template and open this one instead?[br]" +
                       $"[li] #DarkSlateGray[[e]{newTemplateFileName}[/e]]",
                Icon = DialogIconType.Question
            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            return dialog.BuildAndShowDialog();
        }

        #endregion

        #region Cannot read/write file

        public static void FileCantOpen(Window owner, string path, bool isFile)
        {
            string entity = isFile ? "file" : "folder";
            // Tell the user we could not read or write the file
            string title = "Could not open the " + entity;

            string solution = $"Check to see if:[br 2]" +
                              $"[li] the folder exists or if you can create it" +
                              "[li] you can create a file in that folder";

            if (isFile)
            {
                solution += $"[li] another application is using that file (e.g., Excel)";
            }

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                // Height = 430,
                What = $"This {entity} could not be opened:[br 2]" +
                       $"[li] #DarkSlateGray[[e]{path}[/e]]",
                Reason = $"There are many possible reasons, including:[br 2]" +
                         $"[li] the folder may not be accessible or may not exist," +
                         $"[li] you may not have permission to access the {entity}," +
                         $"[li] another application may be using the {entity}.",
                Solution = solution,
                Hint = $"Try logging off and then back on. This may release the {entity} if another application is using it.",
                Icon = DialogIconType.Error
            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }

        #endregion

        #region Prompt to apply operation if partial selection.

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
            string title = $"Apply '{operationDescription}' to this selection?";
            var dialog = new FormattedDialog()
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Question,
                What = $"[e]{operationDescription}[/e] will be applied only to a subset of your images." +
                        $"[br]Is this what you want?",
                Reason = $"A [i]file selection[/i] is active, where you are currently viewing {filesSelectedCount}/{filesTotalCount} of your total files.[br 2]"
                         + $"[li] Only these selected images will be affected by this operation."
                         + "[li] Data for other unselected images will be unaffected.",
                Solution = "Select:[br 2]"
                           + $"[li] [b]Ok[/b] to continue to [i]{operationDescription}[/i] for these selected files"
                           + "[li] [b]Cancel[/b] to abort",
                Hint = $"This is not an error. "
                       + $"We are just reminding you that you have an active selection that is displaying only a subset of your images.[br 2]"
                       + $"[li] You can apply this operation to only this subset. [br 18]"
                       + $"{dontShowMessageAgainInstructions}",

                DontShowAgain =
                {
                    Visibility = Visibility.Visible
                },
            };

            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            bool proceedWithOperation = dialog.BuildAndShowDialog() == true;
            if (proceedWithOperation && dialog.DontShowAgain.IsChecked.HasValue && persistOptOut != null)
            {
                persistOptOut(dialog.DontShowAgain.IsChecked.Value);
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

        #region Overwrite Files?

        // Check if a prompt dialog is needed
        public static bool OverwriteExistingFiles(Window owner, int existingFilesCount)
        {
            // Warn the user that the operation will overwrite existing files.
            const string title = "Overwrite existing files?";
            var dialog = new FormattedDialog()
            {
                Owner = owner,
                DialogTitle = title,
                // Height = 320,
                What = $"Overwrite {existingFilesCount} files with the same name?",
                Reason = $"The destination folder already has {existingFilesCount} files with the same name",
                Solution = $"Select:[br 2]" +
                           "[li] [b]Ok[/b] for Timelapse to overwrite those files" + Environment.NewLine +
                           "[li] [b]Cancel[/b] to abort",
                Icon = DialogIconType.Question
            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            return dialog.BuildAndShowDialog() == true;
        }

        // Check if a prompt dialog is needed
        public static bool OverwriteListOfExistingFiles(Window owner, List<string> files)
        {
            // Warn the user that the operation will overwrite existing files
            string s = files.Count == 1 ? "" : "s";
            string title = $"Overwrite file{s}?";
            string fileList = $"Files with the same name:[br 2]";
            foreach (string file in files)
            {
                fileList += $"[li] {file}";
            }

            var dialog = new FormattedDialog()
            {
                Owner = owner,
                DialogTitle = title,
                Width = 500,
                What = $"Overwrite {files.Count} file{s} with the same name?",
                Reason = $"The destination folder already has {files.Count} file{s} with the same name.[br 2]" +
                         $"[li] Click [b]Details[/b] below to see a list of them.",
                Solution = $"Select: [br 2]" +
                           "[li] [b]Ok[/b] for Timelapse to overwrite those files" + Environment.NewLine +
                           "[li] [b]Cancel[/b] to abort",
                Details = fileList,
                Icon = DialogIconType.Question
            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            return dialog.BuildAndShowDialog() == true;
        }

        #endregion

        #region FilesCannotBeModified

        public static bool FilesCannotBeModified(Window owner, List<string> files)
        {
            // Warn the user that these files cannot be modified
            string s = files.Count == 1 ? "" : "s";
            string title = $"File{s} cannot be modified";

            string fileList = $"Files that cannot be modified:[br 2]";
            foreach (string file in files)
            {
                fileList += $"[li] {file}";
            }

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                // Height = 400,
                Problem = "Some files you are trying to write already exist and cannot be modified",
                Reason = $"The destination folder already has {files.Count} file{s} with the same name, and whose permissions disallow modification." +
                         $"[br 2] [li] Click [b]Details[/b] below to see a list of them.",
                Solution = $"Using the Window's [i]File Explorer[/i], try changing the security setting on the file.[br 2]" +
                           $"[ni] Click its [i]Property[/i] setting," +
                           $"[ni] Go to the [i]Attributes[/i]' row," +
                            "[ni] Unclick the [i]Read only[/i] checkbox.",
                Icon = DialogIconType.Question,
                Details = fileList
            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            return dialog.BuildAndShowDialog() == true;
        }

        #endregion

        #region File Exists

        public static void FileExistsDialog(Window owner, string filePath)
        {
            const string title = "The file already exists.";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                // Height = 270,
                Problem = $"This file already exists, so nothing was done.[br 2]" +
                          $"[li] #DarkSlateGray[[e]{filePath}[/e]]",
                Solution = "Use a different file name.",
                Icon = DialogIconType.Error
            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }
        #endregion

        #region Original file cannot be overwritten

        public static void FileCannotBeOverwrittenDialog(Window owner, string filePath)
        {
            const string title = "The file cannot be over-written";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Error,
                // Height = 320,
                Problem = $"As the original file (below) cannot be overwritten, nothing was done.[br 2]" +
                          $"[li] #DarkSlateGray[[e]{filePath}[/e]]",
                Reason = $"Overwriting a file requires deleting the original file and then creating the new file.[br]" +
                         $"However, if another application is using the original file, it may block deletion.",
                Solution = "Check if another application is using that file. If so, close that application and retry.",

            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }

        #endregion

        #region System.Windows.MessageBox: Missing dependencies

        public static void DependencyFilesMissingDialog(string missingAssemblies)
        {
            // can't use DialogMessageBox to show this message as that class requires the Timelapse window to be displayed.
            string messageTitle = $"Timelapse needs to be in its original downloaded folder.";
            StringBuilder message = new("Problem:" + Environment.NewLine);
            message.AppendFormat("Timelapse won't run properly as it was not correctly installed.{0}{0}", Environment.NewLine);
            message.AppendLine("Reason:");
            message.AppendFormat("When you downloaded Timelapse, it was in a folder with several other files and folders it needs. You probably dragged Timelapse out of that folder.{0}",
                Environment.NewLine);
            message.AppendLine("Solution:");
            message.AppendFormat("Move the Timelapse program back to its original folder, or download it again.{0}{0}", Environment.NewLine);
            message.AppendLine("Hint:");
            message.AppendFormat("Create a shortcut if you want to access Timelapse outside its folder:{0}", Environment.NewLine);
            message.AppendLine("1. From its original folder, right-click the Timelapse program icon.");
            message.AppendLine("2. Select 'Create Shortcut' from the menu.");
            message.AppendFormat("3. Drag the shortcut icon to the location of your choice.{0}{0}", Environment.NewLine);
            message.AppendLine("Details:");
            message.AppendLine("The following assemblies were not found:");
            message.AppendFormat("- {0}", missingAssemblies);
            System.Windows.MessageBox.Show(message.ToString(), messageTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        #endregion

        #region Path too long warnings - several versions
        // This version is for hard crashes. however, it may disappear from display too fast as the program will be shut down.
        public static void FilePathTooLongDialog(Window owner, UnhandledExceptionEventArgs e)
        {
            string title = "Your File Path Names are Too Long to Handle";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Error,
                DialogTitle = title,
                // Height = 360,

                Problem = "Timelapse has to shut down as one or more of your file paths are too long.",
                Solution = $"[li] Shorten the path name by moving your image folder higher up the folder hierarchy, or" +
                               "[li] Use shorter folder or file names.",
                Reason = "Windows cannot perform file operations if the folder path combined with the file name is more than " + File.MaxPathLength + " characters."
                             + "Timelapse will shut down until you fix this.",
                Hint = "File paths to files created in your " + File.BackupFolder + " folder must also be less than " + File.MaxPathLength + " characters."
            };
            if (e?.ExceptionObject != null)
            {
                Clipboard.SetText(e.ExceptionObject.ToString() ?? string.Empty);
            }
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }

        // This version detects and displays warning messages.
        public static void FilePathTooLongDialog(Window owner, List<string> folders)
        {
            ThrowIf.IsNullArgument(folders, nameof(folders));

            const string title = "Some of your image file paths are too long";

            string problem = "Timelapse skipped reading some of your images in the folders below, as their file paths are too long.";
            string fileList = "These are the long file paths:[br 2]";
            if (folders.Count > 0)
            {
                foreach (string folder in folders)
                {
                    fileList += $"[li] {folder}";
                }
            }

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Error,
                // Height = 440,
                Problem = problem,
                Reason = $"Windows cannot perform file operations if the folder path combined with the file name is more than {File.MaxPathLength} characters.",
                Solution = $"Try reloading this image set after shortening the file path. Here are two strategies:[br 2]" +
                           $"[ni] Shorten the path name by moving your image folder up the folder hierarchy." +
                           $"[ni] Use shorter folder or file names.",
                Hint = $"Files created in your [e]{File.BackupFolder}[/e] folder must also be less than {File.MaxPathLength} characters.",

                Details = fileList,
            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }

        public static bool FilePathDeletedFileTooLongDialog(Window owner)
        {
            const string title = "The files you want to delete won't be backed up";
            var dialog = new FormattedDialog()
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Warning,
                // Height = 380,
                Problem = $"As a precaution, Timelapse normally backs up deleted files by moving them into the [e]{File.DeletedFilesFolder}[/e] folder. " +
                          $"However, the new file paths are too long for Windows to do this.",
                Reason = $"Windows cannot perform file operations if the file path is more than {File.MaxPathLength + 8} characters.",
                Solution = $"[li] [b]Okay[/b] deletes these files without backing them up, or" +
                           $"[li] [b]Cancel[/b] aborts this operation.",
                Hint = $"Alternately, shorten the path to your files. Here are two strategies:[br 2]" +
                           $"[ni] Move your image folder higher up the folder hierarchy, or" +
                           $"[ni] Use shorter folder or file names.",

            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            return dialog.BuildAndShowDialog() == true;
        }


        // notify the user when the path is too long
        public static void TemplatePathTooLongDialog(Window owner, string templateDatabasePath)
        {
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = "Timelapse could not open the template",
                Icon = DialogIconType.Error,
                // Height = 345,
                Problem = $"Timelapse could not open this template ([i].tdb[/i]) file as its name is too long:[br 2]" +
                          $"[li] #DarkSlateGray[[e]{templateDatabasePath}[/e]]",
                Reason = $"Windows imposes a file name length limit (including its folder path) of around {File.MaxPathLength} characters.",
                Solution = $"Shorten the path name. Here are two strategies:[br 2]" +
                           $"[ni] Move your image folder higher up the folder hierarchy, or" +
                           $"[ni] Use shorter folder or file names.",
            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }

        // notify the user the template couldn't be loaded because its path is too long
        public static void DatabasePathTooLongDialog(Window owner, string databasePath)
        {
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = "Timelapse could not load the database",
                Icon = DialogIconType.Error,
                // Height = 345,
                Problem = $"Timelapse could not load the database (.ddb) file as its name is too long:[br 2]" +
                          $"[li] #DarkSlateGray[[e]{databasePath}[/e]]",
                Reason = $"Windows imposes a file name length limit (including its folder path) of around {File.MaxPathLength} characters.",
                Solution = $"Shorten the path name. Here are two strategies:[br 2]" +
                           $"[ni] Move your image folder higher up the folder hierarchy, or" +
                           $"[ni] Use shorter folder or file names.",

            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }

        // Warn the user if backups may not be made
        public static void BackupPathTooLongDialog(Window owner)
        {
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = "Timelapse can't back up your files",
                Icon = DialogIconType.Warning,
                // Height = 425,
                Problem = $"Timelapse will continue, but without backing up your files. " +
                          $"The issue is that the backup file can't be created as its name is too long for Windows to handle.",
                Reason = $"Timelapse normally creates time-stamped backup files of your template, database, and [i].csv[/i] files within a [e]{File.BackupFolder}[/e] folder. [br 18]" +
                         $"However, Windows imposes a file name length limit (including its folder path) of around {File.MaxPathLength} characters.",
                Solution = $"Shorten the path name. Here are two strategies:[br 2]" +
                           $"[ni] Move your image folder higher up the folder hierarchy, or" +
                           $"[ni] Use shorter folder or file names.",
                Hint = "You can still use Timelapse, but backup files will not be created.",

            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }

        // notify the user the template couldn't be loaded because its path is too long
        public static void DatabaseRenamedPathTooLongDialog(Window owner, string databasePath)
        {
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = "Timelapse could not rename the database",
                // Height = 345,
                Icon = DialogIconType.Error,
                Problem = $"Timelapse could not rename the database (.ddb) file as its name would be too long:[br 2]" +
                          $"[li] #DarkSlateGray[[e]{databasePath}[/e]]",
                Reason = $"Windows imposes a file name length limit (including its folder path) of around {File.MaxPathLength} characters.",
                Solution = $"Shorten the path name. Here are two strategies:[br 2]" +
                           $"[ni] Move your image folder higher up the folder hierarchy, or" +
                           $"[ni] Use shorter folder or file names.",

            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }

        #endregion

        #region .tdb is in a root or system folder

        public static void TemplateInDisallowedFolder(Window owner, bool isDrive, string path)
        {
            const string title = "Your template file is in a problematic location";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Error,
                Problem = $"The location of your template is problematic. It should be in a normal folder.",
                Solution = "Create a new folder, and try moving your files to that folder."

            };
            if (isDrive)
            {
                dialog.Problem +=
                    $"[br 2][li] The issue is that your files are located in the top-level root drive [e]{path}[/e] rather than a folder."
                    + "[li] Timelapse disallows this as the entire drive would have to be searched for images. ";
                dialog.Reason = $"Timelapse cannot tell if this top-level root drive is:[br 2]" +
                                 $"[li] a massive drive containing all your files (which would take ages to search and retrieve every single image on it), or" +
                                  "[li] an [i]SD card[/i] that only contains your image set images.";
            }
            else
            {
                dialog.Problem += $"[br 2]The issue is that your files are located in a system or hidden folder:[br 2]" +
                                  $"[li] #DarkSlateGray[[e]{path}[/e]]";
                dialog.Reason = "Timelapse expects templates and images to be in a normal folder.[br 2]" +
                                 "[li] As system or hidden folders shouldn't normally contain user files, this could lead to future problems.";
                dialog.Solution += $"[br 4]Or, you may be able to change the folder's attributes by selecting [i]Properties[/i] from "
                                   + "that folder's context menu, and reviewing the [i]Attributes[/i] settings on the [i]General[/i] tab";
            }

            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }


        #endregion

        #region template includes a control of an unknown type

        public static void TemplateIncludesControlOfUnknownType(Window owner, string unknownTypes)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            // notify the user the template couldn't be loaded rather than silently doing nothing
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = "Your template file has an issue.",
                Icon = DialogIconType.Error,
                // Height = 380,
                Problem = "Your template has an issue",
                Reason = $"Your template contains data controls of unknown types.[br 2]" +
                         $"[li] #DarkSlateGray[[e]{unknownTypes}[/e]][br 4]" +
                         $"This could happen if you are trying to open a template with an old version of Timelapse, as newer Timelapse versions may have new types of controls.",
                Solution = $"[ni] {downloadTimelapseInstructions}. Then try reloading your files." +
                            $"[ni] If the error persists, {emailSaulForHelp}. He will check those files to see if there is a fixable bug.",

            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }

        #endregion

        #region Corrupted template
        public static void TemplateFileNotLoadedAsCorruptDialog(Window owner, string templateDatabasePath)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = "Timelapse could not load the Template file.";
            // notify the user the template couldn't be loaded rather than silently doing nothing
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Error,
                // Height = 380,
                Problem = $"Timelapse could not load the Template File:[br 2]"
                              + $"[li] [li] #DarkSlateGray[[e]{templateDatabasePath}[/e]]",
                Reason = $"That template may be corrupted, unreadable, or otherwise invalid.",
                Solution = $"Try one or more of the following:[br 2]"
                               + $"[ni] Recreate the template, or use another copy of it."
                               + $"[ni] Check if there is a valid template file in your [e]{File.BackupFolder}[/e] folder. " +
                               $"{backupsLinkAndDirections}"
                               + $"[ni] {emailSaulForHelp}" +
                               $"He will likely ask you for a copy of your [i]{File.TemplateDatabaseFileExtension}[/i] file.",
                Result = "Timelapse did not affect any of your other files.",

            };

            if (owner.Name.Equals("Timelapse")) // Only displayed in Timelapse, not the template editor
            {
                dialog.Hint = $"See if you can open and examine the template file in the Timelapse Template Editor.[br 18]"
                              + $"If you can't, and if you don't have a copy elsewhere, you will likely have to recreate it.";
            }
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }

        #endregion

        #region Corrupted .ddb file (no primary key)

        public static void DatabaseFileNotLoadedAsCorruptDialog(Window owner, string ddbDatabasePath, bool isEmpty)
        {
            // notify the user the database couldn't be loaded because there is a problem with it
            string title = "Timelapse could not load your database file.";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Error,
                // Height = 440,
                Problem = "Timelapse could not load your database [i].ddb[/i] file:[br 2]"
                              + $"[li] #DarkSlateGray[[e]{ddbDatabasePath}[/e]]",
                Reason = isEmpty
                        ? "Your database file is empty. Possible reasons include: [br 2]"
                        : "Your database is unreadable or corrupted. Possible reasons include: [br 2]",
                Solution = $"[li] If you have not analyzed any images yet, delete the [i].ddb[/i] file and try again."
                               + $"[li] Also, check for valid backups of your database in your [e]{File.BackupFolder}[/e] folder that you can reuse. ",
                Hint = $"If you are stuck, {emailSaulForHelp}.",
            };
            dialog.Reason += $"[ni] Timelapse was shut down (or crashed) in the midst of:"
                              + $"[li 2] loading your image set for the first time, or"
                              + $"[li 2] writing your data into the file, or"
                              + $"[ni] System, security or network  restrictions prohibited file reading and writing, or"
                              + "[ni] Some other unknown reason.";

            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }

        #endregion

        #region Not a Timelapse File

        public static void FileNotATimelapseFile(Window owner, string templateDatabasePath)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            // notify the user the template couldn't be loaded rather than silently doing nothing
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = "Could not load the Timelapse file.",
                // Height = 335,
                Icon = DialogIconType.Error,
                Problem = $"The file does not appear to be a timelapse file:[br 2]" +
                          $"[li] #DarkSlateGray[[e]{templateDatabasePath}[/e]]",
                Reason = $"Timelapse files are either:[br 2]" +
                         $"[li] template files with a suffix [i]{File.TemplateDatabaseFileExtension}[/i]" +
                         $"[li] data files with a suffix [i]{File.FileDatabaseFileExtension}[/i]",
                Solution = "Load only template and database files with those suffixes.",

            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }

        #endregion

        #region DataEntryHandler Confirmations / Warnings for Propagate, Copy Forward, Propagate to here

        // Display a dialog box saying there is nothing to propagate. 
        public static void DataEntryNothingToPropagateDialog(Window owner)
        {
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = "Nothing to propagate",
                // Height = 235,
                Problem = "There is nothing to propagate.",
                Reason = "All the files before this have nothing in this field, so there are no values to propagate.",
                Icon = DialogIconType.Warning
            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }

        // Display a dialog box saying there is nothing to copy forward. 
        public static void DataEntryNothingToCopyForwardDialog(Window owner)
        {
            // Display a dialog box saying there is nothing to propagate. Note that this should never be displayed, as the menu shouldn't be highlit if there is nothing to propagate
            // But just in case...
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = "Nothing to copy forward",
                // Height = 235,
                Problem = "Nothing to copy forward, as there are no files after this one.",
                Reason = "You are on the last file.",
                Icon = DialogIconType.Warning
            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }

        /// <summary>
        /// Tell the user Timelapse can't copy to all a null value
        /// </summary>
        public static void DataEntryCantCopyAsNullDialog(Window owner)
        {
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = "Can't do this operation",
                Icon = DialogIconType.Warning,
                // Height = 350,
                What = "Timelapse cannot do this operation.",
                Reason = "The control has, for some reason, a [i]null[/i] value that can't be copied.",
                Solution = $"Try one of the following:[br 2]" +
                           $"[li] Retype the value in this control with an appropriate value (including empty)" +
                           $"[li] Navigate to a different image and then back to this one and try to do this again.",
                Result = "Nothing has been changed.",

            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }

        /// <summary>
        /// Ask the user to confirm value propagation from the last value
        /// </summary>
        public static bool? DataEntryConfirmCopyForwardDialog(Window owner, string text, int imagesAffected, bool checkForZero)
        {
            text = string.IsNullOrEmpty(text) ? string.Empty : text.Trim();
            string title = "Confirm «Copy Forward» for this field";
            var dialog = new FormattedDialog(MessageBoxButtonType.YesNo)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Question,
                // Height = 290,
                // Width = 630,
                What = "[i]Copy Forward[/i] is not undoable, and can overwrite existing values.",
            };
            string emptyText = !checkForZero && string.IsNullOrEmpty(text)
                ? "(empty)"
                : string.Empty;
            dialog.Result += $"This operation will copy the current {emptyText} value \u00AB[b]{text}[/b]\u00BB from here to the last file of your selected files. {imagesAffected} files will be affected.";

            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            return dialog.BuildAndShowDialog();
        }

        /// <summary>
        /// Ask the user to confirm value propagation to all selected files
        /// </summary>
        public static bool? DataEntryConfirmCopyCurrentValueToAllDialog(Window owner, String text, int filesAffected, bool checkForZero)
        {
            text = string.IsNullOrEmpty(text) ? string.Empty : text.Trim();
            string title = "Confirm «Copy to All» for this field";

            var dialog = new FormattedDialog(MessageBoxButtonType.YesNo)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Question,
                // Height = 290,
                What = "[i]Copy to All[/i] is not undoable, and can overwrite existing values.",
            };
            dialog.Result += !checkForZero && string.IsNullOrEmpty(text)
                ? $"This operation will clear this field across all {filesAffected} of your selected files."
                : $"This operation will set this field to \u00AB[b]{text}[/b]\u00BB across all selected files. {filesAffected} files will be affected.";

            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            return dialog.BuildAndShowDialog();
        }

        public static bool DataEntryConfirmPropagateFromLastValueDialog(Window owner, string text, int imagesAffected)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Confirm «Propagate to Here» for this field";
            text = string.IsNullOrEmpty(text) ? string.Empty : text.Trim();
            string plural = imagesAffected > 1 ? "s" : string.Empty;

            var dialog = new FormattedDialog(MessageBoxButtonType.YesNo)
            {
                Owner = owner,
                Icon = DialogIconType.Question,
                DialogTitle = title,
                // Height = 315,
                What = "[i]Propagate to Here[/i] is not undoable, and can overwrite existing values.",
                Reason = $"[li] The last non-empty value «[b]{text}[/b]» was seen {imagesAffected} file{plural} back."
                         + $"[li] That value will be used to update the selected field for those files between that last file and the current one. {imagesAffected} file{plural} will be affected.",
                DontShowAgain =
                {
                    Visibility = Visibility.Visible
                },
            };

            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            bool proceedWithOperation = dialog.BuildAndShowDialog() == true;
            if (proceedWithOperation && dialog.DontShowAgain.IsChecked.HasValue)
            {
                GlobalReferences.TimelapseState.SuppressPropagateFromLastNonEmptyValuePrompt = dialog.DontShowAgain.IsChecked.Value;
            }

            return proceedWithOperation;
        }
        #endregion

        #region MarkableCanvas Can't Open External PhotoViewer

        /// <summary>
        /// // Can't Open the External Photo Viewer. 
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="extension"></param>

        public static void MarkableCanvasCantOpenExternalPhotoViewerDialog(Window owner, string extension)
        {
            // Can't open the image file. Note that file must exist at this pint as we checked for that above.
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = "Can't open a photo viewer",
                // Height = 305,
                Reason = $"You probably don't have a default program set up to display a photo viewer for [i]{extension}[/i] files",
                Solution = $"Set up a photo viewer in your Windows Settings. Here are two ways to do this.[br 2]" +
                           $"[ni] Go to [i]Default apps[/i], select [i]Photo Viewer[/i] and then choose a desired photo viewer." +
                           $"[ni] Or, right-click on an [i]{extension}[/i] file and set the default viewer using [i]Open with...[/i]",
                Icon = DialogIconType.Error
            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }

        #endregion

        #region No Updates Available / Timeout

        public static void NoUpdatesAvailableDialog(Window owner, string applicationName, Version currentVersionNumber)
        {
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = $"No updates to {applicationName} are available.",
                Icon = DialogIconType.Information,
                // Height = 230,
                Reason = $"You are running the latest version:[br 2]" +
                         $"[li] #DarkSlateGray[[b]{applicationName} v{currentVersionNumber}[/b]]",
            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }

        public static void CheckUpdatesTimedOutDialog(Window owner)
        {
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = "Timelapse could not check for newer versions.",
                Icon = DialogIconType.Information,
                // Height = 285,
                What = "Timelapse could not check to see if a newer version is available.",
                Reason = "The request timed out. Either the network is slow or the server is down.",
                Hint = "Try again later.",
            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
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
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Information,
                // Height = 285,
                What = "No files are missing in the current selection.",
                Reason = "All files in the current selection were checked, and all are present.",
                Result = "No changes were made.",
            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }

        public static void FileSelectionResettngSelectionToAllFilesDialog(Window owner, FileSelectionEnum selection)
        {
            // These cases are reached when 
            // 1) datetime modifications result in no files matching a custom selection
            // 2) all files which match the selection get deleted
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = "Resetting selection to <All files> as no files match the current selection";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Information,
                DialogTitle = title,
                // Height = 335,
                Result = "The [b]All files[/b] selection will be applied, where all files in your image set are displayed."
            };

            switch (selection)
            {
                case FileSelectionEnum.Custom:
                    dialog.Problem = "No files currently match the custom selection, so nothing can be shown.";
                    dialog.Reason = "No files match the criteria set in the current Custom selection.";
                    dialog.Hint = "Create a different custom selection and apply it view the matching files.";
                    break;
                case FileSelectionEnum.Folders:
                    dialog.Problem = "No files and/or image data were found for the selected folder, so nothing can be shown.";
                    dialog.Reason = "Perhaps they were deleted during this session?";
                    dialog.Hint = "Try other folders or another selection. ";
                    break;
                case FileSelectionEnum.Missing:
                    // We should never invoke this, as its handled earlier.
                    dialog.Problem = "Missing files were previously selected. However, none of the files appear to be missing, so nothing can be shown.";
                    break;
                case FileSelectionEnum.MarkedForDeletion:
                    dialog.Problem = "Files marked for deletion were previously selected, but no files are currently marked so nothing can be shown.";
                    dialog.Reason = "No files have their [b]Delete?[/b] field checked.";
                    dialog.Hint = $"If you have files you think should be marked for deletion, check their [b]Delete?[/b] field and then reselect files marked for deletion.";
                    break;
                case FileSelectionEnum.All:
                default:
                    throw new NotSupportedException($"Unhandled selection {selection}.");
            }

            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }

        #endregion

        #region MissingFilesNotFound / Missing Folders

        public static void MissingFileSearchNoMatchesFoundDialog(Window owner, string fileName)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = $"Timelapse could not find any matches to {fileName}";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                // Height = 430,
                What = "Timelapse tried to find the missing image with no success.",
                Reason = $"Timelapse searched the other folders in this image set, but could not find another file that:[br 2]" +
                         $"[li] was named  #DarkSlateGray[[e]{fileName}[/e]], and" +
                         $"[li] was not already associated with an existing entry.",
                Hint = $"If the original file was[br 2]" +
                       $"[li] [b]deleted[/b]: check your [e]{File.DeletedFilesFolder}[/e] folder to see if its there." +
                       $"[li] [b]moved[/b] outside of this image set: try to find it and move it back in." +
                       $"[li] [b]renamed[/b]: you have to find it yourself and restore its original name.[br 12]" +
                       $"Of course, you can just leave things as they are, or delete this image's data field if it has little value to you.",
                Icon = DialogIconType.Question
            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }

        public static void MissingFoldersInformationDialog(Window owner, int count)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = null;

            string title = $"{count} of your folders could not be found";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Problem = $"Timelapse checked for the folders containing your image and video files, and noticed that {count} are missing.",
                Reason = "These folders may have been moved, renamed, or deleted since Timelapse last recorded their location.",
                Solution = $"If you want to try to locate missing folders and files, select:" +
                $"[li] [e]Edit | Try to find missing folders...[/e] to have Timelapse help locate those folders, or" +
                $"[li] [e]Edit | Try to find this (and other) missing files...[/e] to have Timelapse help locate one or more missing files in a particular folder.",
                Hint = $"Everything will still work as normal, except that a [i]Missing file[/i] placeholder image will be displayed instead of the actual image.[br 18]" +
                       $"Searching for the missing folders is optional.",
                Icon = DialogIconType.Warning
            };
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
            Mouse.OverrideCursor = cursor;
        }

        #endregion

        #region ImageSetLoading

        /// <summary>
        /// No images were found in the root folder or subfolders, so there is nothing to do
        /// </summary>
        public static void ImageSetLoadingNoImagesOrVideosWereFoundDialog(Window owner, string selectedFolderPath)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "No images or videos were found.";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Warning,
                Problem = $"No images or videos were found in this folder or its subfolders:[br 2]" +
                          $"[li] #DarkSlateGray[[e]{selectedFolderPath}[/e]]",
                Reason = $"Neither the folder nor its sub-folders contain:[br 2]" +
                         $"[li] image ([i].jpg[/i]) files " +
                         $"[li] video ([i].avi[/i] or [i].mp4[/i]) files",
                Solution = $"Timelapse aborted the load operation.",
                Hint = $"Locate your template in a folder containing (or whose subfolders contain) image or video files.",
            };
            dialog.BuildAndShowDialog();
        }

        #endregion

        #region FormattedDialog MenuFile CSV Export

        /// <summary>
        /// Export data for this image set as a.csv file, but confirm, as only a subset will be exported since a selection is active
        /// </summary>
        public static bool? MenuFileExportCSVOnSelectionDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Exporting to a .csv file on a selected view";
            var dialog = new FormattedDialog()
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Warning,
                // Height = 420,
                What = "Only a subset of your data will be exported to the .csv file.",
                Reason = "As your selection (in the [i]Select[/i] menu) is not set to view [i]All files[/i], "
                             + "only data for these selected files will be exported. ",
                Solution = "Click:" +
                           "[li] [b]Okay[/b] to export just this subset. "
                           + "[li] [b]Cancel[/b] to export data for all your files instead, after which you should:"
                               + $"[ni 2] Select [b]Select | All Files[/b]"
                               + "[ni 2] Retry exporting your data as a [i].csv[/i] file.",
                Hint = $"{dontShowMessageAgainInstructions}",
                DontShowAgain =
                {
                    Visibility = Visibility.Visible
                }
            };
            bool? exportCsv = dialog.BuildAndShowDialog();

            if (dialog.DontShowAgain.IsChecked.HasValue)
            {
                GlobalReferences.TimelapseState.SuppressSelectedCsvExportPrompt = dialog.DontShowAgain.IsChecked.Value;
            }
            return exportCsv;
        }

        /// <summary>
        /// Export data for this image set as a.csv file, but confirm, as only a subset will be exported since a selection is active
        /// </summary>
        public static void MenuFileExportFailedForUnknownReasonDialog(Window owner, string fileName)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Exporting failed for an unkown reason...";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Warning,
                // Height = 280,
                Title = title,
                What = $"Exporting your data to the file below failed for an unknown reason[br 2]" +
                           $"[li]  #DarkSlateGray[[e]{fileName}[/e]]",
                Hint = $"If you continue to have this issue, {emailSaulForHelp}.",
            };

            dialog.BuildAndShowDialog();
        }

        /// <summary>
        /// Cant write the spreadsheet file
        /// </summary>
        public static void MenuFileCantWriteSpreadsheetFileDialog(Window owner, string csvFilePath, string exceptionName, string exceptionMessage)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Can't write the spreadsheet file.";

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Error,
                // Height = 340,
                Title = title,
                Problem = $"The following file can't be written:[br 2]" +
                          $"[li] #DarkSlateGray[[e]{csvFilePath}[/e]]",
                Reason = "You may already have it open in Excel or another application.",
                Solution = "If the file is open in another application, close it and try again.",
                Hint = $"{exceptionName}: {exceptionMessage}"
            };
            dialog.BuildAndShowDialog();
        }

        /// <summary>
        /// Export data for this image set as a.csv file, but confirm, as only a subset will be exported since a selection is active
        /// </summary>
        public static void MenuFileExportRequiresAllFilesSelected(Window owner, string whereToExport)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = $"Can't do the {whereToExport} export, as only a subset of your files are selected ";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Warning,
                // Height = 280,
                Title = title,
                What = $"Exporting [e]{whereToExport}[/e] requires that you have all your files selected, " +
                        "but you are only viewing a subset of them",
                Solution = $"Do [e]Select | All files[/e] and then try exporting again.",
            };

            dialog.BuildAndShowDialog();
        }
        #endregion

        #region CamtrapDP-specific exporting

        // Confirm closing this template and creating a new one
        public static bool? ExportToCamtrapDPExplanation(Window owner)
        {
            var dialog = new FormattedDialog()
            {
                Owner = owner,
                DialogTitle = "Export all data as CamtrapDP files",
                Icon = DialogIconType.Question,
                // Height = 480,
                What = $"To export all your data as CamtrapDP files, you will be asked to select a folder. " +
                       $"Timelapse will then create a new folder within that called [i]{File.CamtrapDPExportFolder}[/i], " +
                       $"and will export four files as required by the CamtrapDP standard into that folder.[br 2]" +
                       $"[li] #DarkSlateGray[[e]{File.CamtrapDPDataPackageJsonFilename}[/e]]" +
                       $"[li] #DarkSlateGray[[e]{File.CamtrapDPDeploymentCSVFilename}[/e]]" +
                       $"[li] #DarkSlateGray[[e]{File.CamtrapDPMediaCSVFilename}[/e]]" +
                       $"[li] #DarkSlateGray[[e]{File.CamtrapDPObservationsCSVFilename}[/e]]",
                Result = $"You should be able to upload these files to a CamptrapDP-compatable data repository, " +
                         $"as Timelapse configures each one to conform to the CamtrapDP standard.",
                Hint = $"CamptrapDP requires filled in values for various fields. " +
                       $"Timelapse will warn you if some are missing.[br 14]" +
                       $"See CamtrapDP web site for:" +
                       $"[li] [link:{ExternalLinks.CamtrapWebSite}|CamtrapDP specifications] detailing the CamtrapDP standard" +
                       $"[li] [link:{ExternalLinks.CamtrapWebSiteValidation}|CamtrapDP validation page] for testing and validating your files before uploading.",

            };
            return dialog.BuildAndShowDialog();
        }
        #endregion

        #region DataExported/Imported To CSV

        // A message saying that the folder data was exported to various CSV files
        public static void AllDataExportedToCSV(Window owner, string folderPath, List<string> files, bool imageDataIncluded)
        {
            // Warn the user that these files cannot be modified
            string title = "The following CSV files were written";
            string fileList = string.Empty;
            foreach (string file in files)
            {
                fileList += $"[li] #DarkSlateGray[[e]{file}[/e]]";
            }

            string result = imageDataIncluded
                ? $"[li] Image data was exported to #DarkSlateGray[[e]{File.CSVImageDataFileName}[/e]] " +
                  $"(data in that file can be altered and imported back into Timelapse)."
                : string.Empty;

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Information,
                // Height = 355,
                What = $"All data was exported to files located under this folder:[br 1]#DarkSlateGray[[b]{folderPath}[/b]][br 1]" +
                       $"{fileList}",
                Result = $"{result}[li] Each folder data level was exported to a file whose name is the same as the folder data level."
            };
            dialog.BuildAndShowDialog();
        }

        // Cant open the file using Excel
        public static void MenuFileCantOpenExcelDialog(Window owner, string csvFilePath)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Can't open Excel.";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Error,
                // Height = 265,
                Problem = $"Excel could not be opened to display this file:[br 2]" +
                          $"[li] #DarkSlateGray[[e]{csvFilePath}[/e]]",
                Solution = "You can manually start Excel and then open the [i].csv[/i] file via Excel.",

            };
            dialog.BuildAndShowDialog();
        }

        /// <summary>
        /// Give the user some feedback about the CSV export operation
        /// </summary>
        public static void MenuFileCSVDataExportedDialog(Window owner, string csvFileName)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Data exported.";
            // since the exported file isn't shown give the user some feedback about the export operation
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Information,
                // Height = 395,
                // Width = 630,
                Title = title,
                What = "The selected data was exported to [br 2]"
                           + $"[li] #DarkSlateGray[[e]{csvFileName}[/e]]",
                Result =
                        $"This file is overwritten every time you export it. Backups can be found in the [e]{File.BackupFolder}[/e] folder.",
                Hint = $"[li] You can open this file with most spreadsheet programs, such as Excel." +
                           $"[li] If you make changes in the spreadsheet file, you will need to import it to see those changes." +
                           $"[li] If needed, select [e]Options|Preferences[/e] to change the [i]Date[/i] and [i]Time[/i] formats.[br 12]" +
                           $"{dontShowMessageAgainInstructions}",
                DontShowAgain =
                {
                    Visibility = Visibility.Visible
                }
            };

            bool? result = dialog.BuildAndShowDialog();
            if (result.HasValue && result.Value && dialog.DontShowAgain.IsChecked.HasValue)
            {
                GlobalReferences.TimelapseState.SuppressCsvExportDialog = dialog.DontShowAgain.IsChecked.Value;
            }
        }

        /// <summary>
        /// Tell the user how importing CSV files work. Give them the opportunity to abort.
        /// </summary>
        public static bool? MenuFileHowImportingCSVWorksDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "How importing .csv data works";
            var dialog = new FormattedDialog()
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Warning,
                // Height = 580,
                // Width = 640,
                Title = title,
                What = "Importing data from a [i].csv[/i] (comma separated value) file follows the rules below.",
                Reason = $"The first row in the [i].csv[/i] file must comprise column headers, where:[br 2]"
                          + $"[li] [b]File[/b] must be included."
                          + $"[li] [b]RelativePath[/b] must be included if any of your images are in subfolders."
                          + $"[li] [b]Remaining headers[/b] should generally match your template's DataLabels[br 14]"
                          + $"Headers can be a subset of your template's DataLabels.[br 4]"
                          + $"Subsequent rows define the data for each file, where it must match the Header type:[br 2]"
                          + $"[li] [b]File[/b] data should match the name of the file you want to update." +
                          Environment.NewLine
                          + $"[li] [b]RelativePath[/b] data should match the sub-folder path containing that file, if any."
                          + $"[li] [b]Counter[/b] data must be blank, 0, or a positive integer."
                          + $"[li] [b]DateTime[/b], [b]Date[/b] and [b]Time[/b] data must follow the specific date/time formats (see [e]File|Export data[/e])."
                          + $"[li] [b]Flag[/b] and [b]DeleteFlag[/b] data must be [i]true[/i] or [i]false[/i]."
                          + $"[li] [b]FixedChoice[/b] data should exactly match a corresponding list item defined in the template, or empty."
                          + $"[li] [b]Folder[/b] and [b]ImageQuality[/b] columns, if included, are skipped over.",
                Result = "Database values will be updated only for matching RelativePath/File entries. Non-matching entries are ignored.",
                Hint = $"Warnings will be generated for non-matching CSV headers, which you can then fix.[br 18]"
                       + $"{dontShowMessageAgainInstructions}",
                DontShowAgain =
                {
                    Visibility = Visibility.Visible
                }
            };
            bool? result = dialog.BuildAndShowDialog();
            if (dialog.DontShowAgain.IsChecked.HasValue)
            {
                GlobalReferences.TimelapseState.SuppressCsvImportPrompt = dialog.DontShowAgain.IsChecked.Value;
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

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Error,
                // Height = 580,
                // Width = 640,
                Title = title,
                Problem = $"The file #DarkSlateGray[[e]{csvFileName}[/e]] could not be read.",
                Reason = "The [i].csv[/i] file is not compatible with the Timelapse template defining the current image set.",
                Solution = "Change your .csv file to fix the errors listed in the [b]Details[/b] below and try again.[br 2]",
                Hint = "Timelapse checks the following when importing the .csv file:[br 2]"
                          + $"[li] The first row is a header whose column names match the data labels in the .tdb template file"
                          + $"[li] [b]Counter[/b] data values are numbers or blanks."
                          + $"[li] [b]Flag[/b] and [b]DeleteFlag[/b] values are either [i]True[/i] or [i]False[/i]."
                          + $"[li] [b]Choice[/b] values are in that field's [i]Choice[/i] list, defined in the template.[br 14]"

                          + "While Timelapse will do the best it can to update your fields:[br 2]"
                          + $"[li] the [i]csv[/i] row is skipped if its [b]RelativePath/File[/b] location do not match a file in the Timelapse database."
                          + $"[li] the [i]csv[/i] row's [b]Date/Time[/b] is updated only if it is in the expected format (see [link:{Constant.ExternalLinks.TimelapseGuideReference}|Timelapse Reference Guide]).",
                Result = "Importing of data from the CSV file was aborted. No changes were made."
            };

            if (resultAndImportErrors != null)
            {
                dialog.Details = "These errors were recorded: [br 2]";
                foreach (string importError in resultAndImportErrors)
                {
                    string prefix = (importError[0] == '-') ? "   " : "[li] ";
                    dialog.Details += prefix + importError;
                }
            }

            dialog.BuildAndShowDialog();
        }

        /// <summary>
        /// CSV file imported
        /// </summary>
        public static void MenuFileCSVFileImportedDialog(Window owner, string csvFileName, List<string> warnings)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "CSV file imported";

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Information,
                // Height = 360,
                What = $"The file #DarkSlateGray[[e]{csvFileName}[/e]] was successfully imported.",
                Hint = $"Check your data. If it is not what you expect, restore your data by using latest backup file in [e]{File.BackupFolder}[/e]. {backupsLinkAndDirections}"
            };
            if (warnings.Count != 0)
            {
                dialog.Result = "Several warnings were generated. Expand the [b]Details[/b] section to check them.";
                dialog.Details = "These warnings were generated.[br 2]";
                foreach (string warning in warnings)
                {
                    string prefix = (warning[0] == '-') ? "   " : "[li] ";
                    dialog.Details += Environment.NewLine + prefix + warning;
                }
            }

            dialog.BuildAndShowDialog();

        }

        /// <summary>
        /// Can't import the .csv file
        /// </summary>
        public static void MenuFileCantImportCSVFileDialog(Window owner, string csvFileName, string exceptionMessage)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Can't import the .csv file.";

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Error,
                // Height = 370,
                Problem = $"The file #DarkSlateGray[[e]{csvFileName}[/e]] could not be opened.",
                Reason = "The file is likely already open in another program. The [b]Details[/b] section below provides a technical explanation",
                Solution = "If the file is open in another program, close it.",
                Result = "Importing of data from the CSV file was aborted. No changes were made.",
                Hint = "Is the file open in Excel?",
                Details = "The technical reason for failing to import data is listed below."
                          + $"[li] {exceptionMessage}"
            };
            dialog.BuildAndShowDialog();
        }

        /// <summary>
        /// Can't export the currently displayed image as a file
        /// </summary>
        public static void MenuFileCantExportCurrentImageDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Can't export this file!";

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Error,
                // Height = 300,
                Problem = "Timelapse can't export a copy of the current image or video file.",
                Reason = "It is likely a corrupted or missing file.",
                Solution = "Make sure you have navigated to, and are displaying, a valid file before you try to export a copy of it."
            };
            dialog.BuildAndShowDialog();
        }

        #endregion

        #region MenuEdit

        /// <summary>
        /// Tell the user how duplicates work, including showing a problem statement if the sort order isn't optimal. Give them the opportunity to abort.
        /// THe various flags determine whether to show or hide the duplicate information, and how to deal with the DontShowAgain checkbox, as we always want
        /// the problem message to appear regardless of the state of DontShowAgain.
        /// </summary>
        /// TODO: The text and fromatting can be cleaned up a bit, but not crucial
        public static bool? MenuEditHowDuplicatesWorkDialog(Window owner, bool sortTermsOKForDuplicateOrdering, bool showProblemDescriptionOnly)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Duplicate this record - What it is for, and caveats";

            var dialog = new FormattedDialog()
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Information,
                // Height = 355,
                // Width = 640,
                DontShowAgain =
                {
                    Visibility = Visibility.Visible
                }
            };

            if (false == sortTermsOKForDuplicateOrdering)
            {
                dialog.What =
                    "[b]Duplicate this record[/b] works best with [e]Sorting[/e] set to either [e]Sort | By relative Path + DateTime[/e], or [e]Sort |by DateTime[/e]. Otherwise, duplicate records may not appear next to each other.";
                if (showProblemDescriptionOnly)
                {
                    dialog.Hint += "You may want to change your sort order before proceeding.";
                }
                else
                {
                    dialog.What += "[br 2]* You may want to change your sort order before proceeding.";
                }
            }

            if (showProblemDescriptionOnly == false)
            {
                dialog.What += $"[li] [e]Duplicating a record[/e] creates a new copy of the current record."
                               + $"[li] Using duplicates, you can have the same field describe multiple things in your image.[br 14]"
                               + $"For example, let's say you have a [b]Choice[/b] box called [i]Species[/i] used to identify animals in your image. "
                               + $"If more than one animal is in the image, you can use the original image to record the first species (e.g., Deer) "
                               + $"and then use one (or more) dupicate records to record the other species that are present (e.g., Elk)";
                dialog.Result = $"[li]A duplicate record will be created linked to the image or video file. The file itself is not duplicated." +
                                $"[li]If you export your data to a CSV file, each duplicates will appear in its own row ";

                dialog.Hint = $"Duplicates come with several caveats.[br 2]"
                              + $"[li] Use [b]Sort | Relative Path + Date Time (default)[/b] to ensure that duplicates appear in sequence."
                              + $"[li] Duplicate creation is disabled in the overview."
                              + $"[li] Duplicates in the exported CSV file are identifiable as rows with the same relative path and file name."
                              + $"[li] [e]Deleting duplicates[/e]: Duplicate records are associated with the same single image or video file. "
                              + $"Consequently, if you delete the image or video file associated with a duplicate record, other duplicates to that image or video will no longer be able to display that file.[br 18]"
                              + $"{dontShowMessageAgainInstructions}";
            }

            bool? result = dialog.BuildAndShowDialog();
            if (dialog.DontShowAgain.IsChecked.HasValue && showProblemDescriptionOnly == false)
            {
                GlobalReferences.TimelapseState.SuppressHowDuplicatesWork = dialog.DontShowAgain.IsChecked.Value;
            }

            return result;
        }

        public static void MenuEditDuplicatesPleaseWait(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Please wait a bit before trying multiple duplications";

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Information,
                // Height = 305,
                Problem = "Duplication needs to wait until the previous duplication is completed",
                Reason = $"[li] When you duplicate a file, Timelapse updates the database, which takes a bit of time." +
                         "[li] Wait until the previous duplication finishes  before duplicating again.",
                Hint = "The cursor will change to a normal cursor when the previous duplication is done.",
            };
            dialog.BuildAndShowDialog();
        }
        #endregion

        #region FormattedDialog VideoFrames
        public static bool? MenuEditExtractVideoFrameSortOrderWarning(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Extract video frame - the sort order is not ideal";

            var dialog = new FormattedDialog()
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Information,
                // Height = 335,
                Problem =
                       $"[e]Extract video frame and create record[/e] works best with [i]Sorting[/i] set to either:[br 2]" +
                        $"[li] [b]Sort | By relative Path + DateTime (default)[/b], or" +
                        $"[li] [b]Sort | By DateTime[/b].",
                Result = "If you continue, the video record and the extracted frame record may not appear in sequence.",
                Hint = "This is not an error. Still, consider changing your sort order before proceeding."
            };
            return dialog.BuildAndShowDialog();
        }

        public static void MenuEditExtractVideoFrameProblem(Window owner, string reason)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Could not extract the video frame";

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Error,
                // Height = 335,
                Problem = "Timelapse can't extract a video frame from the currently displayed video.",
                Reason = reason,
            };
            dialog.BuildAndShowDialog();
        }
        #endregion

        #region FormattedDialog for Quickpaste
        public static void MenuEditCouldNotImportQuickPasteEntriesDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Could not import QuickPaste entries";

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Information,
                // Height = 335,
                Problem = "Timelapse could not find any [i]QuickPaste[/i] entries in the selected database",
                Reason = "When an analyst creates [i]QuickPaste[/i] entries, those entries are stored in the database file "
                         + "associated with the image set being analyzed.[br 18]" +
                         "Since none were found, it is likely that no one had created any [i]QuickPaste[/i] entries when analyzing that image set.",
                Hint = "Perhaps the [i]QuickPaste[/i] entries you want are stored in a different database?",
            };
            dialog.BuildAndShowDialog();
        }
        #endregion

        #region FormattedDialog Populate DataFields
        /// <summary>
        /// There are no displayable images, and thus no metadata to choose from
        /// </summary>
        public static void MenuEditPopulateDataFieldWithMetadataDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Timelapse could not extract metadata";

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Error,
                // Height = 335,
                Problem = $"Timelapse can't extract any metadata, as the currently displayed image or video is missing or corrupted.",
                Reason = "Timelapse tries to examines the currently displayed image or video for its metadata.",
                Hint = "Navigate to a displayable image or video, and try again.",

            };
            dialog.BuildAndShowDialog();
        }

        public static void MenuEditRereadDateTimesFromMetadataDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Timelapse could not re-read date metadata";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Error,
                // Height = 335,
                Problem = $"Timelapse can't extract any date metadata, as the currently displayed image or video is missing or corrupted.",
                Reason = "Timelapse tries to examines the currently displayed image or video for its date metadata.",
                Hint = "Navigate to a displayable image or video, and try again.",
            };
            dialog.BuildAndShowDialog();
        }

        #endregion

        #region FormattedDialog Deletions and Missing
        public static void MenuEditNoFilesMarkedForDeletionDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "No files are marked for deletion";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Information,
                // Height = 270,
                Problem = "You are trying to delete files marked for deletion, but no files have their [b]Delete?[/b] field checked.",
                Hint = "If you have files that you think should be deleted, check-mark their [b]Delete?[/b] field.",
            };
            dialog.BuildAndShowDialog();
        }

        public static void MenuEditNoFoldersAreMissing(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "No folders appear to be missing";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Information,
                // Height = 270,
                What = "You asked to to find any missing folders, but none appear to be missing.",
                Hint = "You don't normally have to do this check yourself, as a check for missing folders is done automatically whenever you start Timelapse.",
            };
            dialog.BuildAndShowDialog();
        }

        #endregion

        #region MenuOptions
        public static void MenuOptionsCantPopulateDataFieldWithEpisodeAsNoFilesDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Cannot populate a field with Episode data";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Error,
                DialogTitle = title,
                // Height = 285,
                Problem = $"Timelapse cannot currently populate any fields with [i]Episode[/i] data.",
                Reason = "There are no files in the current selection.",
                Hint = "Expand the current selection, or add some images or videos. Then try again."
            };
            dialog.BuildAndShowDialog();
        }

        public static void MenuOptionsCantPopulateDataFieldWithEpisodeAsNoNoteFields(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Cannot populate a field with Episode data";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Error,
                DialogTitle = title,
                // Height = 305,
                Problem = $"Timelapse cannot currently populate any fields with Episode data.",
                Reason = "Episode data would be put in a [i]Note[/i] field, but none of your fields are [i]Notes[/i].",
                Hint = $"Using the Timelapse Template Editor, modify your template [i].tdb[/i] file to include a [i]Note[/i] field."
            };
            dialog.BuildAndShowDialog();
        }

        public static bool MenuOptionsCantPopulateDataFieldWithEpisodeAsSortIsWrong(Window owner, bool searchTermsOk, bool sortTermsOk)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "You may not want to populate this field with Episode data";
            var dialog = new FormattedDialog()
            {
                Owner = owner,
                Icon = DialogIconType.Warning,
                DialogTitle = title,
                Problem = "You may not want to populate this field with [i]Episode[/i] data.",
                Solution = $"Select:[br 2]" +
                           $"[li] [b]Okay[/b] to populate this field anyways, or" +
                           $"[li] [b]Cancel[/b] to abort populating this field with [i]Episode[/i] data"
            };


            string reason = "";
            string hint = "";

            if (!searchTermsOk)
            {
                if (!sortTermsOk)
                {
                    reason += "1. ";
                }

                reason += "Your current file selection includes search terms that may omit files in an Episode.";
                hint += $"Use the [i]Select[/i] menu to select either:[br 2]" +
                        $"[li] [b]All file[/b], or" +
                        $"[li] [b]All files in a folder and its subfolders[/b]";
                if (!sortTermsOk)
                {
                    reason += Environment.NewLine;
                    hint += Environment.NewLine;
                }
            }

            if (!searchTermsOk && !sortTermsOk)
            {
                hint += "[br 6]";
                dialog.Height += 60;
            }

            if (sortTermsOk)
            {
                dialog.Reason = reason;
                dialog.Hint = hint;
                return dialog.BuildAndShowDialog() == true;
            }

            if (!searchTermsOk)
            {
                reason += "2. ";
            }

            reason += "Your files should be sorted in ascending date order for this to make sense.";
            hint += $"Use the [i]Sort[/i] menu to sort either by:[br 2]" +
                    $"[li] [b]RelativePath[/b] then [b]DateTime[/b] (both in ascending order), or" +
                    $"[li] [b]DateTime only[/b]  (in ascending order)";

            dialog.Reason = reason;
            dialog.Hint = hint;
            return dialog.BuildAndShowDialog() == true;
        }

        #endregion

        #region related to DateTime

        public static void DateTimeNewTimeShouldBeLaterThanEarlierTimeDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Your new time has to be later than the earliest time";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Warning,
                DialogTitle = title,
                // Height = 340,
                Problem = "Your new time has to be later than the earliest time   ",
                Reason = "Even the slowest clock gains some time.",
                Solution = "The date/time was unchanged from where you last left it.",
                Hint = "The image on the left shows the earliest time recorded for images in this filtered view  shown over the left image"
            };
            dialog.BuildAndShowDialog();
        }

        #endregion

        #region MessageBox: related to Arguments to start a particular template or to constrain to a particular relative path

        // Tell the user that Timelapse is currently restricted to the folder designated by a particulare relative path
        public static void ArgumentRelativePathDialog(Window owner, string folderName)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = $"Timelapse is currently restricted to the folder #DarkSlateGray[[e]{folderName}[/e]][br 12]";
            //string title = $"Timelapse is currently restricted to the folder folderName";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Information,
                DialogTitle = title,
                // Height = 350,
                What = $"{title}[br 4]This means that:[br 2]" +
                       $"[li] you will only be able to view and analyze files in that folder and its subfolders" +
                       $"[li] any reference by Timelapse to [b]All files[/b] means '[b]All files in the folder[/b] #DarkSlateGray[[e]{folderName}[/e]]'" +
                       $"[li] to avoid confusion, you will not be able to open a different image set in this session",
                Reason = $"Timelapse was started with the instruction to restrict itself to that folder. " +
                         $"This is usually done to narrow analysis to a particular subset of files of interest"
            };
            dialog.BuildAndShowDialog();
        }

        public static void ArgumentDataFileButNoTemplateDialog(Window owner, string ddbfile, string folder)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = "Timelapse cannot open your data [i].ddb[/i] file as the template ([i].tdb[/i]) file is missing";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Information,
                DialogTitle = title,
                What = $"{title}[br 2]",
                Reason = $"You selected #DarkSlateGray[{ddbfile}] in the folder:" +
                         $"[li] #DarkSlateGray[{folder}]." +
                         $"[br]However, no accompanying template [i].tdb[/i] file was found in that folder. Templates are required, as they define the data fields you would fill in.",
                Solution = "[li] Use the [b]TemplateEditor[/b] to create a template in that folder, or" +
                           $"[li] copy an existing template [i].tdb[/i] file into that folder."
            };
            dialog.BuildAndShowDialog();
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
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Warning,
                DialogTitle = title,
                // Height = 425,
                // Width = 640,
                What = $"For Timelapse to expand the search to include episodes, you must have a data field populated with your files' [i]Episode[/i] data, " +
                       $"where the [i]Episode[/i] data is in the expected format.[br 18]" +
                       $"None of your data fields, at least for the current file, includes the expected [i]Episode[/i] data.",
                Reason = $"When you choose this option, Timelapse searches for episodes having at least one file matching your search criteria. " +
                         $"If so, all files contained in those episodes are then displayed.[br 18]" +
                         $"For this to work properly, one of your data fields must have been filled in using [br 2]" +
                         $"[li] [b]Edit | Populate a field with episode data[/b], where:" +
                         $"[li 2] the episode format includes the episode sequence number as its prefix, e.g. [i]25:1|3[/i]," +
                         $"[li 2] all currently selected files were populated with [i]Episode[/i] information at the same time.",
                Hint = $"See the [link:{Constant.ExternalLinks.TimelapseGuideReference}|Timelapse Reference Guide] about how this operation works, what is required, and what it is for."
            };
            dialog.BuildAndShowDialog();
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

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Information,
                DialogTitle = title,
                // Height = 425,
                What = "This update ... [br 2]"
                             + $"[li] stuff"
                             + "[li] more stuff",
                Reason = "The reason is  [br 2]"
                            + "[ni] Point, " + Environment.NewLine
                            + "[ni] Another point",
                Hint = $"{dontShowMessageAgainInstructions}",
                DontShowAgain =
                {
                    Visibility = Visibility.Visible
                }
            };
            bool? result = dialog.BuildAndShowDialog();
            if (result.HasValue && result.Value && dialog.DontShowAgain.IsChecked.HasValue)
            {
                GlobalReferences.TimelapseState.SuppressOpeningMessageDialog = dialog.DontShowAgain.IsChecked.Value;
            }
        }

        #endregion

        #region Opening in a viewonly state

        /// Give the user various opening mesages
        public static void OpeningMessageViewOnly(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "You are using the Timelapse 'View only' version ...";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Information,
                // Height = 360,
                What = $"You started the [i]view-only[/i] version of Timelapse.[br 18]" +
                       $"[li] You can open and view existing images, videos and any previously entered data." +
                       $"[li] You will not be able to edit or alter that data, or create a new image set." +
                       $"[li] Menu items that alter data are hidden from view, and data entry controls are disabled.",
                Reason = "This Timelapse version is handy if you only want to view an image set and its data, as it ensures that no accidental changes to the data will be made.",
                Hint = "If you want to edit data, then you should start the normal Timelapse program.",
            };
            dialog.BuildAndShowDialog();
        }

        public static void ViewOnlySoDatabaseCannotBeCreated(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "You are using the Timelapse 'View only' version. ";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Warning,
                // Height = 340,
                What = $"Creating a new image set is not allowed when Timelapse is started as [i]view-only[/i].",
                Reason = $"You tried to open a template on an image set that has no data associated with it. " +
                         $"Timelapse normally looks for images in this folder, and creates a database holding data for each image. " +
                         $"In view-only mode, creating a new database is not permitted",
                Hint = "If you want to load these images for the first time, then you should start the normal Timelapse program.",

            };
            dialog.BuildAndShowDialog();
        }

        #endregion

        #region MessageBox: ddb or tdb file opened with an older version of Timelapse than recorded in it

        public static bool? TimelapseFilesOpenedWithOlderVersionOfTimelapse(Window owner, string filePath)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "You are opening your Timelapse files with an older Timelapse version";
            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = null;
            string fileType = string.Equals(Path.GetExtension(filePath).ToLower(), ".ddb", StringComparison.Ordinal) ? "database [i].ddb[/i]" : "template [i].tdb[/i]";
            var dialog = new FormattedDialog()
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Warning,
                Title = title,
                What = $"You are opening your Timelapse {fileType} with an older version of Timelapse.[br 18]"
                            + $"You previously saved this file with a later version of Timelapse. "
                            + "This is just a warning, as its rarely a problem.",
                Reason = "Using the newest Timelapse version can minimize incompatabilities (if any) that could occur due to changes in the way data is saved.",
                Solution = $"Click:[br 2]"
                           + "[li] [b]Ok[/b] to keep going. It will likely work."
                           + $"[li] [b]Cancel[/b] to abort. {downloadTimelapseInstructions}.",
                Hint = $"{dontShowMessageAgainInstructions}",
                DontShowAgain =
                {
                    Visibility = Visibility.Visible
                },
            };
            bool? result = dialog.BuildAndShowDialog();
            if (dialog.DontShowAgain.IsChecked.HasValue)
            {
                GlobalReferences.TimelapseState.SuppressOpeningWithOlderTimelapseVersionDialog = dialog.DontShowAgain.IsChecked.Value;
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
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Error,
                DialogTitle = title,
                // Height = 350,
                // Width = 640,
                What = $"You are using an old incompatible version of the Timelapse program to open this image set.[br 18]" +
                       $"To open this image set, you must update Timelapse to the latest version.",
                Reason = $"This image set was previously opened by a later version of Timelapse, " +
                         $"which updated your files in a way that is no longer compatible with the version of Timelapse you are using.",
                Solution = $"{downloadTimelapseInstructions}. Then try again.",
                Hint = "Its always best to use the latest Timelapse version to minimize any incompatabilities."
            };
            dialog.BuildAndShowDialog();
            Mouse.OverrideCursor = cursor;
        }

        public static void DatabaseFileBeingMergedIsIncompatibleWithParent(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "You are trying to merge an old incompatible database.";
            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = null;
            // notify the user the template couldn't be loaded rather than silently doing nothing
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Error,
                DialogTitle = title,
                // Height = 425,
                // Width = 640,
                What = $"You are trying to merge an incompatible database into the parent database.[br 18]" +
                       $"To merge this database, you must open it within Timelapse, which will update it to the latest version.",
                Reason = $"The database you are trying to merge was created with an earlier version of Timelapse. " +
                         $"Its internals are not compatible with the latest database structure.",
                Solution = $"[ni] Use Timelapse to open the template and/or database located in the folder you are trying to merge." +
                           $"[ni] Timelapse will then update the database." +
                           $"[ni] Then try to merge again",
                Hint = "Its always best to use the latest Timelapse version to minimize any incompatabilities."
            };
            dialog.BuildAndShowDialog();
            Mouse.OverrideCursor = cursor;
        }

        #endregion

        #region DialogIsFileValid checks for valid database file and displays appropriate dialog if it isn't

        public static bool DialogIsFileValid(System.Windows.Window owner, string filePath)
        {
            switch (FilesFolders.QuickCheckDatabaseFile(filePath))
            {
                case DatabaseFileErrorsEnum.Ok:
                    return true;
                case DatabaseFileErrorsEnum.OkButOpenedWithAnOlderTimelapseVersion:
                    if (GlobalReferences.MainWindow.State.SuppressOpeningWithOlderTimelapseVersionDialog == false)
                    {
                        if (GlobalReferences.TimelapseState.AlreadyWarnedAboutOpenWithOlderVersionOfTimelapse == false)
                        {
                            GlobalReferences.TimelapseState.AlreadyWarnedAboutOpenWithOlderVersionOfTimelapse = true;
                            return true == Dialogs.TimelapseFilesOpenedWithOlderVersionOfTimelapse(owner, filePath);
                        }
                    }
                    return true;
                case DatabaseFileErrorsEnum.PreVersion2300:
                case DatabaseFileErrorsEnum.UTCOffsetTypeExistsInUpgradedVersion:
                    Mouse.OverrideCursor = null;
                    DialogUpgradeFiles.DialogUpgradeFilesAndFolders dialogUpdateFiles =
                        new(owner, Path.GetDirectoryName(filePath), VersionChecks.GetTimelapseCurrentVersionNumber().ToString());
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

        #region Merging errors

        // Invalid file
        public static void MergeErrorDatabaseFileAppearsCorruptDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = $"Your database {File.FileDatabaseFileExtension} file  is likely corrupted.";
            // notify the user the database appears corrupt
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Error,
                DialogTitle = title,
                // Height = 385,
                Problem = $"The selected #DarkSlateGray[[e]{File.FileDatabaseFileExtension}[/e]] file does not contain a valid  database.",
                Reason = "Your  database file is likely corrupted, which means that Timelapse cannot use it.",
                Solution = $"If you could open this database file previously, a working backup may be available.[br 2]" +
                           $"[li] Check your [b]{File.BackupFolder}[/b] folder to see if there is a backup that works. {backupsLinkAndDirections}" +
                           $"[li] If a useful backup is not available, you may have to delete your database file and then recreate it.",
                Hint = $"If you are stuck, {emailSaulForHelp}. He will check those files to see if they can be repaired."
            };
            dialog.BuildAndShowDialog();
        }

        // DDb file exists
        public static bool? MergeWarningCreateEmptyDdbFileExists(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = "Do you really want to create an empty database in this folder?";
            // notify the user the database appears corrupt
            var dialog = new FormattedDialog(MessageBoxButtonType.YesNo)
            {
                Owner = owner,
                Icon = DialogIconType.Question,
                DialogTitle = title,
                // Height = 395,
                Problem = $"A database [i]{File.FileDatabaseFileExtension}[/i] file already exists in that folder.",
                Reason = $"While you can have multiple databases in a folder, it can lead to confusion as to which one to use and which one has the most up to date data.",
                Solution = $"You can continue to create the empty database. However, you may want to [br 2]" +
                           $"[li] reconsider, revisit and/or clean up that folder before doing so, or" +
                           $"[li] create a folder above this one, and use that as the root folder instead.",
                Hint = $"[li] This is just a warning. Having multiple databases in a folder works, but goes against best practices." +
                       $"[li] {mergingFilesInstructions}."
            };
            return dialog.BuildAndShowDialog();
        }

        // DDb file exists
        public static bool? MergeWarningCheckOutDdbFileExists(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = "Do you really want to check out a database into this folder?";
            // notify the user the database appears corrupt
            var dialog = new FormattedDialog(MessageBoxButtonType.YesNo)
            {
                Owner = owner,
                Icon = DialogIconType.Question,
                DialogTitle = title,
                // Height = 375,
                Problem = $"A database [i]{File.FileDatabaseFileExtension}[/i] file already exists in that folder.",
                Reason = $"While you can have multiple databases in a folder, it can lead to confusion as to which one to use and which one has the most up to date data.",
                Solution = $"You can continue to check out a database. However, you may want to [br 2]" +
                           $"[li] reconsider, revisit and/or clean up that folder before doing so.",
                Hint = $"[li] This is just a warning, as having multiple databases in a folder goes against best practices." +
                       $"[li] {mergingFilesInstructions}."
            };
            return dialog.BuildAndShowDialog();
        }

        // Old .ddb version
        public static void MergeErrorDatabaseFileNeedsToBeUpdatedDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = $"Your selected database {File.FileDatabaseFileExtension} needs to be updated.";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Warning,
                DialogTitle = title,
                // Height = 405,
                Problem = $"Your database [i]{File.FileDatabaseFileExtension}[/i] needs to be updated.",
                Reason = $"Your  database file was created with an old version of Timelapse and cannot be opened with the current Timelapse version",
                Solution = $"Upgrade this (and other) files as follows.[br 2]" +
                           $"[ni] Select [b]File|Upgrade Timelapse files to the latest version[/b] from the Timelapse menu." +
                           $"[ni] Follow the instructions in that dialog to select and upgrade one or more files.",
                Hint = $"Opening an upgraded file with an old version of Timelapse can cause Timelapseto crash, " +
                       $"which may corrupt your database [i]{File.FileDatabaseFileExtension}[/i] file.[br 18]" +
                       $"If that happens, try upgrading it again in the latest version of Timelapse."
            };
            dialog.BuildAndShowDialog();
        }

        // File in a non-permitted place
        public static void TemplateInDisallowedFolder(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Your selected file is in a problematic location";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Error,
                DialogTitle = title,
                // Height = 415,
                Problem = $"Your file cannot be located in:[br 2]" +
                          $"[li] system folders" +
                          $"[li] hidden folders" +
                          $"[li] top-level root drives (e.g., C:, D:, etc)",
                Reason = $"Timelapse expects your file to be in a normal folder for various reasons.[br 2]" +
                         $"[li] System and hidden folders should not contain user files." +
                         $"[li] If in a root drive, Timelapse search facilities would search everything would could take ages.",
                Solution = $"[li] Move your files to a new or different folder." +
                           $"[li] If that folder should be a normal folder, change its attributes via the Windows File Explorer by selecting [i]Properties[/i] from that folder's context menu, and reviewing the [i]Attributes[/i] settings on the [i]General[/i] tab"
            };
            dialog.BuildAndShowDialog();
        }

        // File path is too long
        public static void MergeErrorFilePathTooLongDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Your file's path name is too long";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Error,
                DialogTitle = title,
                // Height = 385,
                Problem = "Your file's path name is too long.",
                Reason = $"Windows cannot perform file operations if the folder path combined with the file name is more than {File.MaxPathLength} characters.",
                Solution = $"[li] Shorten the path name by moving your image folder higher up the folder hierarchy, or" +
                           $"[li] Use shorter folder or file names.[br 6]" +
                           $"If you do move or rename them and Timelapse cannot find those folders or files, use the [b]Edit|Try to find...[/b] functions to locate them.",
                Hint = $"Files created in your [b]{File.BackupFolder}[/b] folder must also be less than {File.MaxPathLength} characters."
            };
            dialog.BuildAndShowDialog();
        }

        // Incompatible Template
        public static void MergeErrorTemplateFilesNotCompatableDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Incompatible templates ";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Error,
                DialogTitle = title,
                // Height = 475,
                // Width = 640,
                Problem = $"Timelapse could not merge this database back into the parent database because their templates differ.",
                Reason = "The data field definitions used to enter image-level data differ.",
                Solution = $"Try the following steps.[br 2]" +
                           $"[ni] Update the selected database's template [i]{File.TemplateDatabaseFileExtension}[/i] file to match the destination database's template." +
                           $"[ni] If you are using folder levels, the child template should not include the extraneous folder levels. " +
                           $"If it does, this can be difficult to repair." +
                           $"[ni] Reopen the selected database in Timelapse to complete the update." +
                           $"[ni] Try to merge the file again.",
                Hint = $"[li] The best way to create compatable child template [i]{File.TemplateDatabaseFileExtension}[/i] and data [i]{File.FileDatabaseFileExtension}[/i] " +
                       $"files is by checking out the child via the [b]File|Merge[/b] option." +
                       $"[li] Then use that template to either load the checked out data file, or to create a new data file." +
                       $"[li] {mergingFilesInstructions}"
            };
            dialog.BuildAndShowDialog();
        }

        public static void MergeErrorTemplateFilesLevelsNotCompatableDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Incompatible templates ";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Error,
                DialogTitle = title,
                Problem = $"Timelapse could not merge this database back into the parent database because their templates differ.",
                Reason = "The folder level definitions differ.",
                Solution = $"Compare the folder levels between the templates used by the parent and child. " +
                           $"Their folder levels should be the same, and each level should include the same controls.",
                Hint = $"[li] The best way to create compatable child template [i]{File.TemplateDatabaseFileExtension}[/i] and data [i]{File.FileDatabaseFileExtension}[/i] " +
                       $"files is by checking out the child via the [b]File|Merge[/b] option." +
                       $"[li] Then use that template to either load the checked out data file, or to create a new data file." +
                       $"[li] {mergingFilesInstructions}"
            };
            dialog.BuildAndShowDialog();
        }

        // File does not exist
        public static void MergeErrorFileDoesNotExist(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Cannot merge the databases";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Error,
                DialogTitle = title,
                // Height = 320,
                Problem = "The selected file no longer exists so it can't be merged",
                Reason = $"Timelapse could not determine why the selected file no longer exists. " +
                         $"Was it was deleted, moved or renamed before completing the merge operation?",
                Solution = $"[li] Try relocating the file and moving it into the correct folder, or" +
                           $"[li] Run the merge operation again to see what files are available for merging." +
                           $"[li] {mergingFilesInstructions}"
            };
            dialog.BuildAndShowDialog();
        }

        // Recognizer categories differ
        public static void MergeErrorRecognitionCategoriesIncompatible(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "The recognition categories between your files are incompatible";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Error,
                DialogTitle = title,
                // Height = 380,
                Problem = $"The detection and/or classification categories used for image recognition are incompatible " +
                          $"between the selected files and the destination file.",
                Reason = "As Timelapse was unable to combine the categories, it stopped the merge as otherwise recognitions would be inconsistent in the merged files.",
                Solution = $"Possible solutions include:[br 2]" +
                           $"[li] Revisit how the imported recognition [i].json[/i] file was created." +
                           $"[li] Redo image recognition for those files, being careful to use the same recognizer and (optional) classification model."
            };
            dialog.BuildAndShowDialog();
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
                    reason = $"The source folder [i]{sourceFolderPath}[/i] does not exist. Because of that, Timelapse could not rename the actual folder.";
                    break;
                case MoveFolderResultEnum.FailAsDestinationFolderExists:
                    reason = $"The destination folder [i]{destinationFolderPath}[/i] already exists as a Windows subfolder. " +
                             $"Windows does not allow a folder to be renamed if a folder with the desired name already exists.";
                    break;
                case MoveFolderResultEnum.FailDueToSystemMoveException:
                default:
                    reason = "Windows tried to rename your folder, but for some reason couldn't do it.";
                    break;
            }

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Error,
                DialogTitle = title,
                // Height = 275,
                What = $"Timelapse could not rename [i]{sourceFolderPath}[/i] to [i]{destinationFolderPath}[/i]",
                Reason = reason
            };
            dialog.BuildAndShowDialog();
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
                    reason = $"The folder [i]{sourceFolderPath}[/i]' does not exist as a Windows folder. " +
                             $"Because of that, Timelapse could not create the subfolder [i]{destinationName}[/i] within it.";
                    break;
                case CreateSubfolderResultEnum.FailAsDestinationFolderExists:
                    reason = $"The destination subfolder [i]{destinationName}[/i] already exists as a Windows subfolder in [i]{sourceFolderPath}[/i]. " +
                             $"Because of that, Timelapse did not have to create the subfolder.";
                    break;
                case CreateSubfolderResultEnum.FailDueToSystemCreateException:
                default:
                    reason = $"Windows tried to create the subfolder [i]{destinationName}[/i] in [i]{sourceFolderPath}[/i], but for some reason couldn't do it.";
                    break;
            }

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Error,
                DialogTitle = title,
                // Height = 275,
                What = $"Timelapse could not create the folder [i]{destinationName}[/i] in [i]{sourceFolderPath}[/i]",
                Reason = reason
            };
            dialog.BuildAndShowDialog();
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
                    example = $"[li] allowed characters: (A-Z, a-z, 0-9, _, -)" +
                              $"[li]  e.g., Station_1";
                    expectedType = "alphanumeric";
                    title += "alphanumeric text";
                    break;
                case Control.AlphaNumeric + "Glob":
                    expectedInput = "alphanumeric text plus glob characters";
                    example = $"[li] allowed characters: (A-Z, a-z, 0-9, _, -,?, *)" +
                              $"[li] e.g., Station* to find text with the prefix Station";
                    expectedType = "alphanumeric with glob characters";
                    title += "alphanumeric text plus glob chacters";
                    break;
                case Control.IntegerAny:
                    expectedInput = "an integer";
                    expectedType = "integer";
                    example = $"[li] e.g., -5";
                    title += "a valid integer";
                    break;
                case Control.Counter:
                    expectedInput = "a positive integer";
                    expectedType = "counter";
                    example = $"[li] e.g., 5";
                    title += "a valid positive integer";
                    break;
                case Control.IntegerPositive:
                    expectedInput = "a positive integer";
                    expectedType = "positive integer";
                    example = $"[li] e.g., 5";
                    title += "a valid positive integer";
                    break;
                case Control.FixedChoice:
                    expectedInput = "an item in this control's fixed choice menu, or blank";
                    expectedType = "an item in this control's fixed choice menu, or blank";
                    example = $"[li] e.g., c if your menu list contained a,b,c ";
                    title += "a valid choice menu item";
                    break;
                case Control.MultiChoice:
                    expectedInput = "a comma-separated list of items, each in your list menu";
                    expectedType = "comma-separated list";
                    example = $"[li] e.g., a,c if your multichoice list contained a,b,c ";
                    title += "a valid list of choice menu items";
                    break;
                case Control.DecimalAny:
                    expectedInput = "a decimal number";
                    example = $"[li]  e.g., -3.45";
                    expectedType = "decimal";
                    title += "a valid decimal number";
                    break;
                case Control.DecimalPositive:
                    expectedInput = "a positive decimal number";
                    example = $"[li] e.g., 3.45";
                    expectedType = "positive decimal";
                    title += "a valid positive decimal number";
                    break;
                case Control.DateTime_:
                    expectedInput = "in date/time format";
                    example = $"[li] format is yyyy-mm-dd hh:mm:ss" +
                              $"[li] e.g., 2024-12-24 13:05:00 for Dec. 24, 2024, 1:05 pm";
                    expectedType = "date/time";
                    title += "in date/time format";
                    break;
                case Control.Date_:
                    expectedInput = "in date format";
                    example = $"[li] format is yyyy-mm-dd" +
                              $"[li] e.g., 2024-12-24 ";
                    expectedType = "date";
                    title += "in date format";
                    break;
                case Control.Time_:
                    expectedInput = "in time format ";
                    example = $"[li] format is hh:mm:ss" +
                              $"[li] e.g., 13:05:00 for 1:05 pm";
                    expectedType = "time";
                    title += "in time format";
                    break;
                case Control.Flag:
                    expectedInput = "a true or false value";
                    title += "a valid true or false value";
                    break;
            }

            string what = invalidContent.Length < 30

                ? $"Your entered data is: [e]{invalidContent}[/e].[br 18]" +
                  $"This does not match what a [b]{expectedType}[/b] data field expects."
                : "Your entered data does not match what this data field expects.";

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Error,
                DialogTitle = title,
                // Height = 395,
                What = what,
                Reason = $"The contents of this data field must be [b]{expectedInput}[/b][br 2]{example}",
                Result = "Your data field's contents may be reset.",
                Hint = "Check your data field's contents, then enter text that matches what the data field expects."
            };
            dialog.BuildAndShowDialog();
        }

        #endregion

        #region FormattedDialog FolderEditor warning as using folder levels

        public static bool? FolderEditorMetadataWarning(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "The folder editor does not work well with folder levels and folder metadata";
            var dialog = new FormattedDialog()
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Warning,
                // Height = 415,
                Problem = $"Your image set uses folder levels and folder metadata.[br 2]" +
                          $"[li] If you rename, move or delete existing folders that have metadata associated with it, " +
                          $"the folder editor will not update its folder level locations." +
                          $"[li] This means that previously entered metadata may no longer be associated with the edited folder.",
                Solution = $"You can still use the folder editor to review your folders and/or to create new folders. However, we recommend against moving or renaming existing folders.[br 4]" +
                           $"Select:[br 2]" +
                           $"[li] [b]Ok[/b] to start the folder editor anyways," +
                           $"[li] [b]Cancel[/b] to abort.",
            };
            return dialog.BuildAndShowDialog();
        }

        #endregion

        #region MessageBox: MenuRecognition - ImportRecognizer File Feeldback
        public static void MenuFileRecognizersDataCouldNotBeReadDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Recognition data not imported.";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Problem = $"No recognition information was imported. There were problems reading the recognition data.",
                Reason = $"Possible causes are:[br 2]" +
                $"[li] the file could not be opened, or" +
                $"[li] the recognition data in the file is somehow corrupted",
                Solution = "You may have to re-create the recognition [i].json[/i] file.",
                Result = "Recognition information was not imported, and nothing was changed.",
                Icon = DialogIconType.Error
            };
            dialog.BuildAndShowDialog();
        }

        /// <summary>
        /// No matching folders in the DB and the recognizer file
        /// </summary>
        public static void MenuFileRecognitionDataNotImportedDialog(Window owner, string details)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Recognition data not imported.";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Warning,
                DialogTitle = title,
                // Height = 530,
                // Width = 640,
                Problem = $"No recognition information was imported.[br 18]" +
                          $"The image file paths in the recognition file and the Timelapse database are all completely different. " +
                          $"Thus no recognition information could be assigned to your images.",
                Reason = $"When the recognizer originally processed a folder (and its subfolders) containing your images, " +
                         $"it recorded each image's location relative to that folder. " +
                         $"If the subfolder structure differs from that found in the Timelapse root folder, then the paths won't match.[br 18]" +
                         $"For example, if the recognizer was run on [i]AllFolders/Camera1/[/i] but your template and database is in [i]Camera1/[/i]," +
                         $"the folder paths won't match, since [i]AllFolders/Camera1/[/i] \u2260 [i]Camera1/[/i].",
                Solution = $"[ni] Easiest: Rerun the recognizer on the proper folder. " +
                           $"See the [link:{Constant.ExternalLinks.TimelapseGuideImageRecognition}|Timelapse Recognition Guide] to better understand what happens." +
                           $"[ni] You may be able to repair the paths in the recognition file using this program: [link:https://lila.science/cameratraps-detectormismatch|MegaDetector Output Manager App] available from Lila Science.",
                Result = "Recognition information was not imported, and nothing was changed.",
                Details = details
            };
            dialog.BuildAndShowDialog();
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
                : $"Recognition data imported for images located in a particular sub-folder:[br 2]" +
                  $"[li] [i]{jsonFilePath}[/i]";

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Information,
                DialogTitle = title,
                What = what,
                // Height = 395,
                Hint = $"[ni] When you run the recognizer on a folder or sub-folder, recognitions are constrained " +
                       $"to images located within the selected folder and its sub-folders." +
                       $"[ni] If you choose [b]Select|Custom Selection[/b], you can:" +
                       $"[li 2] select images matching particular recognitions," +
                       $"[li 2] click [i]Show all files with no recognition data[/i] to list images missing recognition data.",
                Details = $"This list indicates which sub-folders had recognitions.[br 2]{details}"
            };
            dialog.BuildAndShowDialog();
        }

        /// <summary>
        /// Recognitions: successfully imported message
        /// </summary>
        public static void MenuFileRecognitionsSuccessfulyImportedDialog(Window owner, string details, string summaryReport)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Recognitions imported.";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Information,
                DialogTitle = title,
                Result = "Recognition data imported for your image set.",
                Hint = $"If you choose [b]Select|Custom Selection[/b], you can now:[br 2]" +
                           $"[li] select images matching particular detections and classifications at various confidence levels," +
                            "[li] find images that have not been processed by the recognizer by clicking [b]show all files with no recognition data[/b].",
                Details = details
            };
            // The Extra button is normally hidden. We reveal it and use it to invoke a dialog box to show the summary report (extracted from the recognizer file), if present
            if (false == string.IsNullOrWhiteSpace(summaryReport))
            {
                dialog.ExtraButton.Visibility = Visibility.Visible;
                dialog.ExtraButton.Content = "Show the recognizer's summary report";
                dialog.ExtraButton.Tag = summaryReport;
                dialog.ExtraButton.Click += ExtraButton_Click;
            }
            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowDialog();
        }


        /// <summary>
        /// Recognitions: warning that the recognition file may have been created by running AddaxAI externally
        /// </summary>
        public static bool? MenuFileRecognitionsLikelyFromExternallyRunAddaxAIDialog(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Was this recognition file created by invoking [i]AddaxAI[/i] within Timelapse?";
            var dialog = new FormattedDialog()
            {
                Owner = owner,
                Icon = DialogIconType.Warning,
                DialogTitle = title,
                Problem = $"This recognition file is only partially compatible with Timelapse",
                Reason = $"Depending on how it is invoked, [i]AddaxAI[/i] creates different versions of its recognition file. " +
                         $"[br 6][ni][b]Compatible version[/b]: [i]AddaxAI[/i] should be invoked withing Timelapse via the [b]Recognition|Addax Image Recognizer|Run AddaxAI Image Recognizer[/b] menu. " +
                         $"[li 2]This normally produces  a file called [i]timelapse_recognition_file.json[/i], which conforms to what Timelapse expects." +
                         $"[br 6][ni][b]Incompatible version[/b]: [i]AddaxAI[/i] was invoked externally (e.g., by directly opening it). " +
                         $"[li 2]This normally produces a file called [i]image_recognition_file.json[/i]. While Timelapse can import that, there are differences that can mess up your recognitions." +
                         $"[br 6]In this case, Timelapse noticed that you are trying to open a recognition file named [i]image_recognition_file.json[/i]",
                Solution = $"[ni] Re-run [i]AddaxAI[/i] by selecting [b]Recognition|Addax Image Recognizer|Run AddaxAI Image Recognizer[/b] from the Timelapse menu." +
                           $"[ni] Alternately, and only if you did intend to run [i]AddaxAI[/i] externally and know what you are doing, you can still import it. However, be aware that:" +
                           $"[li 2] The folder paths in the recognition file may not match your image set's folder structure." +
                           $"[li 2] The recognition categories may differ from those previously used in your image set." +
                           $"[li 2] The classification categories may be duplicated on the detection categories." +
                           $"[br 10]Select:" +
                           $"[li] [b]Ok[/b] to import the recognition file anyways," +
                           $"[li] [b]Cancel[/b] to abort.",
            };
            return dialog.BuildAndShowDialog();
        }
        // Event handler: invoke a dialog box to show the summary report (held as a string in the tag), if present
        private static void ExtraButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button extraButton == false) return;
            if (extraButton.Tag is String contentString == false) return;
            if (string.IsNullOrWhiteSpace(contentString)) return;
            Window window = new()
            {
                Title = "Recognizer's summary report",
                ShowInTaskbar = false,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                // Width = 400,
                // Height = 500,
                FontFamily = new("Segoe UI"),
                Owner = extraButton.FindParentOfType<Window>()
            };

            Dialogs.TryPositionAndFitDialogIntoWindow(window);
            Grid grid = new();
            grid.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new() { Height = new(60) });

            Button button = new()
            {
                Content = "Okay",
                Margin = new(10)
            };
            button.Click += (_, _) =>
            {
                window.Close();
            };
            Grid.SetRow(button, 1);

            WebBrowser wb = new()
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
            const string title = "Could not import the recognition data";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Information,
                DialogTitle = title,
                // Height = 255,
                Reason = (RecognizerImportResultEnum.JsonFileCouldNotBeRead == importError)
                    ? "The Json recognition file could not be read."
                    : "There were problems trying to import the recogntion data. We are not sure why this happened.",
                Result = "Recognition data was not imported"
            };
            dialog.BuildAndShowDialog();
        }

        /// <summary>
        /// Warn the user that there no existing files match the recognition data
        /// </summary>
        /// <returns>The selected path, otherwise null </returns>
        public static bool RecognizerNoMatchToExistingFiles(Window owner, string samplePath)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Your recognition image file paths don't match your actual files";
            var dialog = new FormattedDialog()
            {
                Owner = owner,
                Icon = DialogIconType.Question,
                DialogTitle = title,
                // Height = 465,
                // Width = 640,
                What = $"Do you know that your recognition image file paths don't match your existing files? This could be a problem unless its intentional.",
                Reason = $"There are two likely reasons for this mismatch.[br 2]" +
                         $"[ni] This is an unintentional error, possibly due to the location of your recognizer file. You should correct this." +
                         $"[ni] This is not an error, as you intentionally located your images elsewhere and will resolve this later.",
                Solution = $"Depending on the reason, you may want to:[br 2]" +
                           $"[li] [b]Cancel[/b] to stop importing so you can check to see what is going on (see hint below)." +
                           $"[li] [b]Okay[/b] to import the recognitions anyways (if you know how to resolve this)",
                Hint = string.IsNullOrWhiteSpace(samplePath)
                    ? $"The problem is that the recognition file contains no files! You probably want to Cancel."
                    : $"An example file path found in the recogntion file is:[br 2]" +
                      $"[li] #DarkSlateGray[[e]{samplePath}[/e]] " +
                      $"[li] Examine that path to see if this file path matches an actual image file's location."
            };
            return true == dialog.BuildAndShowDialog();
        }

        #endregion

        #region MessageBox: Shortcut management
        // Using shortcut
        public static void ShortcutDetectedDialog(Window owner, string path)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            string title = "A shortcut to a folder containing your images was detected.";
            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = null;

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Information,
                DialogTitle = title,
                // Height = 445,
                // Width = 640,
                What = $"Timelapse detected and will use a shortcut in your root folder. That shortcut indicates that your images are located there: [br 2]" +
                           $"[li] #DarkSlateGray[[e]{path}[/e]]",
                Result = $"[li] Timelapse will search for images in the shortcut's destination folder."
                         + "[li] Your Timelapse [i].tdb[/i], [i].ddb[/i] and [i]Backup[/i] files will still be stored in the root folder you selected.",
                Reason = $"By including this shortcut in your root folder, you can locate your images outside of the root folder instead of within it, e.g.,[br 2]"
                             + $"[li] elsewhere on the local disk,"
                             + $"[li] on a network drive, or"
                             + $"[li in the cloud.",

                Hint = "If you actually wanted Timelapse to use images located in your root folder, remove the shortcut. [br 18]"
                + $"{dontShowMessageAgainInstructions}",
                DontShowAgain =
                {
                    Visibility = Visibility.Visible
                },
            };
            dialog.BuildAndShowDialog();
            if (dialog.DontShowAgain.IsChecked.HasValue)
            {
                GlobalReferences.TimelapseState.SuppressShortcutDetectedPrompt = dialog.DontShowAgain.IsChecked.Value;
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
                pathList += $" • {path}";
            }

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Error,
                DialogTitle = title,
                // Height = 525,
                // Width = 650,
                Problem = $"Timelapse detected multiple shortcuts in your root folder (see [b]Details[/b] below), " +
                          $"each pointing to a folder that could contain your images.[br 18]" +
                          $"As each shortcut points to a folder that could contain your images, Timelapse does not know which one to use.",
                Result = "Timelapse will abort this operation.",
                Reason = $"A shortcut allows you to locate your images outside of the root folder instead of within it, e.g., [br 2]" +
                         $"[li] elsewhere on the local disk," +
                         $"[li] on a network drive, or" +
                         $"[li] in the cloud." +
                         $"As several shortcuts were found, Timelapse does not know which shortcut's folder it should use.",
                Solution = $" If you want Timelapse to use:[br 2]" +
                           $"[li] a particular shortcut, then remove the other shortcuts from the root folder" +
                           $"[li] images located in your root folder, then remove all shortcuts.",
                Details = $"Shortcuts found include:[br 2]{pathList}"
            };
            dialog.BuildAndShowDialog();
            Mouse.OverrideCursor = cursor;
        }
        #endregion

        #region FormattedDialog AddaxAI-related 
        public static void AddaxAICouldNotBeStarted(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "AddaxAI could not be started";
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Information,
                DialogTitle = title,
                // Height = 300,
                What = $"[i]AddaxAI[/i] could not be started, but we don't know why.",
                Hint = $"[li] Try to re-download and install [i]AddaxAI[/i] again, or" +
                       "[li] Check the access and execution permissions for [i]AddaxAI[/i] (perhaps your computer setup has restrictions?).",
            };
            dialog.BuildAndShowDialog();
        }

        public static bool? AddaxAIAlreadyDownloaded(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "A version of AddaxAI is already installed ";
            var dialog = new FormattedDialog()
            {
                Owner = owner,
                Icon = DialogIconType.Information,
                DialogTitle = title,
                // Height = 360,
                What = $"A version of [i]AddaxAI[/i] is already installed.[br]" +
                       $"As installation takes about 10-15 minutes, you may want to avoid reinstalling [i]AddaxAI[/i] unless:[br 2]" +
                       $"[li] you know that a new version is available, or" +
                       $"[li] you are having issues with [i]AddaxAI[/i], and want to reinstall it," +
                       "[li] you are unsure if this is the latest and greatest [i]AddaxAI[/i] version.",
                Hint = $"Select:[br 2]" +
                       $"[li] [b]Okay[/b] to continue with the [i]AddaxAI[/i] installation, or" +
                       "[li] [b]Cancel[/b] to abort [i]AddaxAI[/i] installation."
            };
            return dialog.BuildAndShowDialog();
        }

        public static bool? AddaxAINotInstalled(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "AddaxAI doesn't appear to be installed.";

            var dialog = new FormattedDialog()
            {
                Owner = owner,
                Icon = DialogIconType.Information,
                DialogTitle = title,
                // Height = 200,
                // Width = 500,
                What = $"The [i]AddaxAI[/i] image recognizer does not appear to be installed. ",
                Hint = $"Select:[br 2]" +
                       $"[li] [b]Okay[/b] if you want to try to run the uninstaller anyways," +
                       "[li] [b]Cancel[/b] to abort the [i]AddaxAI[/i] uninstall."
            };
            return dialog.BuildAndShowDialog();
        }

        public static bool? AddaxAIInstallationInformaton(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "Install AddaxAI (from web site) - Overview";

            var dialog = new FormattedDialog()
            {
                Owner = owner,
                Icon = DialogIconType.Information,
                DialogTitle = title,
                // Height = 460,
                What = $"When you install [i]AddaxAI[/i]:[br 2]" +
                       $"[ni] The [i]AddaxAI[/i] [b]Windows Installation web page[/b] will appear." +
                       $"[ni] Follow the instructions on that page (it should be easy)." +
                       $"[ni] If a [e]Windows protected your PC[/e] dialog appears, select [b]More Info[/b] and [b]Run anyway[/b].",
                Result = $"Installation takes ~5-15 minutes. " +
                       "Lots of technical feedback will be displayed as various required packages are loaded.",
                Hint = $"Be patient! Try to avoid interrupting the installation, as cleaning up an aborted installation can be messy."
            };
            return dialog.BuildAndShowDialog();
        }

        public static void AddaxAIApplicationInstructions(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "AddaxAI is starting up (about 5 - 20 seconds)...";

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Information,
                DialogTitle = title,
                // Height = 460,
                What = $"The [i]AddaxAI[/i] application should start up shortly. When it does:[br 2]" +
                       $"[ni] Select the model you want to use." +
                       $"[li 2] [b]None[/b] shows detections, and only classifies entities as an animal, person, or vehicle," +
                       $"[li 2] [b]Geographic models[/b] classify particular species found in a geographic region," +
                       $"[li 2] [b]Global - SpeciesNet - Google[/b]  is a more general species classifier[br 2]" +
                       $"[ni] Click [b]Start processing[/b] to recognize your images.",
                Result = "A #DarkSlateGray[[e]timelapse_recognition_file.json[/e]] file will be created in your chosen folder. Use the [b]Recognition[/b] menu to load it into Timelapse.",
                Hint = $"Be patient. Image recognition takes time![br]" +
                          $"[li] You may want to run this overnight if you have (say) tens or hundreds of thousands of images." +
                          "[li] You can continue with your other work (including Timelapse work) while AddaxAI is running."
            };
            dialog.BuildAndShowDialog();
        }
        #endregion

        #region FormattedDialog CamtrapDP Dialogs
        public static bool? CamtrapDPDataPackageOrDeploymentNotFilledIn(Window owner, bool missingDataPackage, bool missingAllDeployments, List<string> missingDeploymentsList)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            int missingDeploymentCount = missingDeploymentsList?.Count ?? 0;
            bool missingDeployments = missingDeploymentCount > 0;
            bool missingBoth = missingDataPackage && missingDeployments;
            string title = string.Empty;

            string details = string.Empty;
            string reason = "Exporting CamtrapDP requires the following:";
            string datapackageReason = missingDataPackage
                ? $"[li] [e]data package[/e] containing metadata about this project, exported as a [i]datapackage.json[/i] file"
                : string.Empty;
            string deploymentReason = missingDeploymentCount > 0
                ? $"[li] [e]deployment[/e] entries describing each camera trap placement, exported as rows in a [i]deployments.csv[/i] file (see [b]Details[/b] for a list)"
                : string.Empty;
            reason += datapackageReason;
            reason += deploymentReason;

            string solution = "Select [li] [b]Cancel[/b] to go back and fill in the missing information [i](recommended)[/i]." +
                              "[li] [b]Ok[/b] to export your files, even though they would be incomplete.";

            string hintInformation = string.Empty;

            string deploymentDetails = missingAllDeployments
                ? "all deployments"
                : $"{missingDeploymentCount} of your deployments";
            if (missingBoth)
            {
                title += $"CamtrapDP: Your data package and {deploymentDetails} need to be filled in.";
                reason += $"[br] However, neither your data package nor ";
                reason += missingAllDeployments
                    ? "any deployments"
                    : $"{deploymentDetails}";
                reason += " have been created and filled in.";
                hintInformation += "[e]data package[/e] and [e]deployment[/e]";

            }
            else if (missingDataPackage)
            {
                title += "CamtrapDP: Your data package needs to be filled in.";
                reason += $"[br] However, your data package has not yet been created and filled in. ";
                hintInformation += "[e]data package[/e]";
            }
            else if (missingDeployments)
            {
                title += $"CamtrapDP: {deploymentDetails} need to be filled in.";
                reason += $"[br] However, ";
                reason += missingAllDeployments
                    ? "none of your deployments"
                    : $"{deploymentDetails} ";
                reason += " have been created and filled in.";
                hintInformation += "[e]deployment[/e]";
            }

            if (missingDeployments && missingDeploymentsList != null)
            {

                details += "These deployments are missing:[br 2]";
                foreach (string str in missingDeploymentsList)
                {
                    details += $"[li] {str}";
                }
            }

            string hint = $"You can create, display and fill in CamtrapDP {hintInformation} information via the [e]Folder data[/e] tab. ";

            var dialog = new FormattedDialog()
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Warning,
                Problem = title,
                Reason = reason,
                Result = "If you select [b]Ok[/b], the exported files will be incomplete. Further warnings will likely be generated.",
                Solution = solution,
                Hint = hint,
                Details = details,
            };
            return dialog.BuildAndShowDialog();
        }


        public static bool? CamtrapDPDataPackageMissingRequiredFields(Window owner, List<string> missingMessages)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "CamtrapDP: Required fields are missing";
            string missingMessage = $"These fields are missing:[br 2]";
            foreach (string str in missingMessages)
            {
                missingMessage += $"{str}";
            }

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Warning,
                // Height = 370,
                What = "CamtrapDP specifies various required fields, which don't appear to be filled in. See [b]Details[/b] below for a list.",
                Result = "Other CamtrapDP systems that expect these required fields may complain or fail.",
                Hint = $"This is just a warning, as your data was still exported. " +
                       "You may want to go back and fill in those missing fields.",
                Details = missingMessage,
            };
            return dialog.BuildAndShowDialog();
        }

        public static bool? CamtrapDPSpatialCoverageInstructions(Window owner)
        {
            ThrowIf.IsNullArgument(owner, nameof(owner));
            const string title = "How to use spatial coverage";

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = title,
                // Height = 420,
                What = $"Spatial coverage, specified as GeoJson data, can be defined in a few ways.[br 6]" +
                       $"[ni] From lat/long: " +
                       $"[li 2] click to calculate a bounding box surrounding your deployments' latitude/longitude coordinates.[br 6]" +
                       $"[ni] Via Geojson.io." +
                       $"[li 2] click to view and/or edit your current spatial coverage in a browser-based map," +
                       $"[li 2] then copy/paste the generated geojson text into the Timelapse spatial field.[br 6]" +
                       $"[ni] From some other source (e.g., GIS package)." +
                       $"[li 2] paste geojson into the spatial coverage field",
                Hint = $"The easiest approach is to:[br 2]" +
                       $"[li] click [b]From lat/long[/b]" +
                       $"[li] click [b]Via GeoJson.IO[/b] to view the results",
                Icon = DialogIconType.Information
            };
            return dialog.BuildAndShowDialog();
        }

        //public static bool? CamtrapDPPopulateFields(Window owner)
        //{
        //    ThrowIf.IsNullArgument(owner, nameof(owner));
        //    const string title = "Populate CamtrapDP fields";

        //    var dialog = new FormattedDialog(MessageBoxButtonType.OK)
        //    {
        //        Owner = owner,
        //        DialogTitle = title,
        //        What = $"Populate the following CamtrapDP fields[br 6]" +
        //               $"[ni] [b]eventStart[/b]",
        //        Icon = DialogIconType.Question,
        //    };
        //    return dialog.BuildAndShowDialog();
        //}
        #endregion

        #region TreeViewWithRelativePaths: No files in selected folder
        public static void NoImageDataAssociatedWithFiles(Window owner, string title, string path)
        {
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Warning,
                DialogTitle = title,
                Reason = $"While the folder {path} exists, no image data is associated with any files in it.",
                Hint = "Perhaps you removed these files and its data during this session?"
            };
            dialog.BuildAndShowDialog();
        }
        #endregion

        #region Generic message
        public static void DisplayError(Window owner, string title, string reason)
        {
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Error,
                DialogTitle = title,
                Reason = reason,
            };
            dialog.BuildAndShowDialog();
        }
        #endregion

        #region SelectNewNameForRenamedFields
        public static void SelectNewNameForRenamedFields(Window owner, List<string> problemDataLabels)
        {
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                Icon = DialogIconType.Error,
                DialogTitle = "Select the new name for your 'Renamed' fields ",
                Problem = "You indicated that the following fields should be renamed, but did not provide the new name[br]" +
                           string.Join<string>(", ", problemDataLabels),
                Solution = "For each [e]Rename[/e] action, either" +
                           "[li]use the drop down menu to provide the new name, or" +
                           "[li]set the [e]Update Action[/e] back to [e]Delete[/e]."
            };
            dialog.BuildAndShowDialog();
        }
        #endregion

        #region ShowKeyboardShortcutsForTimelapse
        public static void ShowKeyboardShortcutsForTimelapse(Window owner)
        {
            string keyboardShortcutsContent =
                "[b][f 5]Timelapse Keyboard Shortcuts[/f][/b]" +
                "[br][b]If a data field has the focus[/b] ([b]#Purple[thick purple border][/b]):" +
                "[li] the [b]〔Ctl〕 key[/b] must also be pressed. If not, the key is interpretted by the data field." +
                "[br][b]Icons:[/b]" +
                "[ni]≡ shortcut same as selecting the indicated menu item." +
                "[ni]🖯 shortcut same as selecting the indicated button." +

                "[br 12][b][f 5]Tab and Enter[/f][/b]" +
                "[li]〔Tab〕/ 〔Shift〕〔Tab〕 " +
                "[li 2][e] If a data field has the input focus[/e]: go to the next / previous data field." +
                "[li 2][e] If main image area has the focus[/e]: go to the last used data field." +
                "[li]〔Enter〕" +
                "[li 2][e] If an image data field is focused[/e]: go to image area." +
                "[li 2][e] If a folder Data field is focused[/e]: go to next/previous control." +
                "[li 2][e] Main image area is focused[/e]: return to the last used data field." +

                "[br 12][b][f 5]Edit menu: [/f][/b]" +
                "[li]〔Ctl〕[b]F[/b] ≡[e]Find file[/e]" +
                "[li][b]Q[/b] ≡[e]Show Quickpaste window[/e]" +
                "[li][b]C[/b] ≡🖯[e]Copy previous values[/e] menu and button" +
                "[br][i]Duplicate records:[/i]" +
                "[li]〔Ctl〕[b]D[/b] ≡[e]Duplicate this record (use current values)[/e]" +
                "[li]〔Ctl〕〔Shift〕[b]D[/b] ≡[e]Duplicate this record (use default values)[/e]" +

                "[br 12][b][f 5]Options menu: [/f][/b]" +
                "[br][i]Magnification (ignored when a data field has the focus)[/i]" +
                "[li][b]M[/b] ≡[e]Magnifier|Display magnifying glass [/e]." +
                "[li][b]U[/b] ≡[e]Magnifier|Increase magnification [/e]." +
                "[li][b]D[/b] ≡[e]Magnifier|Decrease magnification[/e]." +

                "[br 8][b][f 5]View menu: [/f][/b]" +

                "[br][i]File navigation[/i]" +
                "[li][b]⬅[/b] ≡[e]View previous file[/e]" +
                "[li][b]⮕[/b] ≡[e]View next file[/e]" +

                "[br][i]Episode navigation[/i]" +
                "[li][b]E[/b] ≡[e]Show episode information[/e]." +
                "[li]〔Ctl〕[b]⬅[/b] ≡[e]View previous episode[/e]" +
                "[li]〔Ctl〕[b]⮕[/b] ≡[e]View next episode[/e]" +

                "[br][i]Thumbnail navigation in overview[/i]" +
                "[li][b]⬆[/b] ≡[e]Previous row[/e] (of thumbnails in overview)" +
                "[li]〔Shift〕[b]⬇[/b] ≡[e]Next row[/e] (of thumbnails in overview)" +
                "[li][b]⬆[/b] ≡[e]Previous page[/e] (of thumbnails in overview)" +
                "[li]〔Shift〕[b]⬆[/b] ≡[e]Next page[/e] (of thumbnails in overview)" +

                "[br][i]Zooming[/i]" +
                "[li][b]<[/b] ≡[e]Zoom in[/e] (ignored when data field has the focus)" +
                "[li][b]>[/b] ≡[e]Zoom out[/e] (ignored when data field has the focus)" +
                "[li][b]-[/b] ≡[e]Zoom out all the way[/e]" +
                "[li][b]+[/b] ≡[e]Zoom to bookmarked region[/e]" +
                "[li][b]B[/b] ≡[e]Bookmark current zoom region[/e]" +

                "[br][i]Dogear navigation[/i]" +
                "[li]〔Shift〕[b]K[/b] ≡[e]Dog-ear the current image.[/e]" +
                "[li][b]K[/b] ≡[e]Switch between the dog-eared and last seen image.[/e]" +

                "[br][i]Image differencing[/i]" +
                "[li][b]⬆[/b] ≡[e]Cycle through image differences[/e] (Single image view only)" +
                "[li][b]⬇[/b] ≡[e]View combined image differences[/e] (Single image view only)" +

                "[br 8] [b][f 5]Select menu:[/f][/b]" +
                "[li]〔Ctl〕[b]S[/b] ≡[e]Custom Select[/e]" +
                "[li][b] F5[/b] ≡[e]Refresh the current selection[/e]" +

                "[br 8] [b][f 5]Quickpaste window (if opened):[/f][/b] " +
                "[li]〔Ctl〕[b] 1,2...[/b] 🖯 invokes associated [e]Quickpaste[/e] button" +
                "[li]〔Ctl〕〔Shift〕[b]1,2...[/b] 🖯 invokes associated [e]Quickpaste[/e] button" +

                "[br 8] [f 5][b]View Images tab:[/b][/f]" +
                "[li][b] H[/b] Temporarily hide detection bounding boxes, if any" +
                "[li][b] P[/b] Temporarily show a popup displaying surrounding images in this episode, even those not in the current selection" +

                "[br 8][f 5][b]Video Player:[/b][/f]" +
                "[li 2]〔[b]Space[/b]〕🖯 Play / Pause toggle button" +
                "[li 2][b]R[/b] 🖯 [e]Best Recognition button[/e]]";

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                FontSize = 12,
                Owner = owner,
                Icon = DialogIconType.Information,
                DialogTitle = "Keyboard shortcuts for Timelapse",
                What = keyboardShortcutsContent,
                NoHeadings = true,
                ExtraButton =
                {
                    // Configure ExtraButton for Print functionality
                    Content = "Print...",
                    Margin = new Thickness(10, 0, 5, 0),
                    Visibility = Visibility.Visible
                }
            };

            dialog.ExtraButton.Click += (_, _) =>
            {
                PrintKeyboardShortcuts(owner, keyboardShortcutsContent, false);
            };

            // Add Print Preview button using the NoButton
            dialog.NoButton.Content = "Print Preview...";
            dialog.NoButton.Padding = new Thickness(10, 0, 10, 0);
            dialog.NoButton.Width = Double.NaN;
            dialog.NoButton.Visibility = Visibility.Visible;
            dialog.NoButton.Click += (_, _) =>
            {
                PrintKeyboardShortcuts(owner, keyboardShortcutsContent, true);
            };

            FormattedDialogHelper.SetupStaticReferenceResolver(dialog);
            dialog.BuildAndShowNonModalDialog();
        }

        // Prints or shows print preview for keyboard shortcuts
        // <param name="showPreview">If true, shows preview window; if false, shows print dialog directly</param>
        private static void PrintKeyboardShortcuts(Window owner, string formattedContent, bool showPreview)
        {
            try
            {
                // Create FlowDocument from the formatted content using the general-purpose utility
                FlowDocument document = PrintFlowDocument.CreateDocumentFromFormattedText(
                    formattedContent,
                    title: "");

                if (showPreview)
                {
                    // Show print preview using the PrintFlowDocument utility
                    PrintFlowDocument.PrintWithPreview(document, "", owner);
                }
                else
                {
                    // Show standard print dialog
                    PrintFlowDocument.Print(document, "", owner);
                }
            }
            catch (Exception ex)
            {
                string operation = showPreview ? "show print preview" : "print";
                System.Windows.MessageBox.Show(
                    $"Failed to {operation}:{Environment.NewLine}{ex.Message}",
                    showPreview ? "Print Preview Error" : "Print Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        #endregion

        #region Template Editor Dialogs: File issues
        public static void EditorTemplateFileNoLongerExistsDialog(Window owner, string templateFileName)
        {
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = "The template file no longer exist",
                Problem = $"The template file #DarkSlateGray[[e]{templateFileName}[/e]] no longer exists.",
                Icon = DialogIconType.Warning
            };
            dialog.BuildAndShowDialog();
        }
        #endregion

        #region Template Editor Dialogs: Standards issues
        /// <summary>
        /// Could not create a template based on the style
        /// </summary>
        public static void EditorTemplateCouldNotCreateStandardDialog(Window owner)
        {
            var dialog = new FormattedDialog()
            {
                Owner = owner,
                DialogTitle = "Could not create a template based on the standard",
                Problem = "A template based on the standard could not be created.",
                Reason = "We are not sure what happened or what to do next.",
                Hint = emailSaulForHelp,
                Icon = DialogIconType.Error
            };
            dialog.BuildAndShowDialog();
        }
        #endregion 

        #region Template Editor Dialogs: Data types, labels, resevered words etc issues
        public static bool? TypeChangeInformationDialog(Window owner, string from, string to)
        {

            var dialog = new FormattedDialog()
            {
                Owner = owner,
                DialogTitle = "Changing a data field's type",
                Icon = DialogIconType.Warning,
                What = $"You are changing the data field's type from [b]{from}[/b] to [b]{to}[/b].[br 6]" +
                       $"This may have consequences when loading a Timelapse Data [i](.ddb)[/i] file that was previously opened with the old data type.[br 6]" +
                       $"[ni] [e]Two equivalent types.[/e] This is safe. You can convert back and forth between them, e.g., " +
                       $"[li 2] [b]Note\u27F7MultiLine[/b], as both contain plain text.[br 6]" +
                       $"[ni] [e]From a specialized type to a more general type.[/e] This is a one-way  operation. Reversing it falls under 3 below, e.g.," +
                       $"[li 2] [b]PositiveDecimal→Decimal[/b] (decimals can contain positive decimals)." +
                       $"[li 2] [b]Decimal→Note[/b] (notes can contain decimals as text).[br 6]" +
                       $"[ni] [e]Unsafe From a general type to a specialized type[/e]: Timelapse will not allow the update as its data" +
                       $" will likely not match what the specialized type expects, e.g., " +
                       $"[li 2] [b]Note→Decimal[/b] (Note data may contain non-numeric characters)." +
                       $"[li 2] [b]Decimal→Date[/b] (Decimal data if very different from Date data)." +
                       $"[li 2] [b]Decimal→PositiveDecimal[/b] (Decimal data may be negative).[br 6]" +
                       "The drop-down menu only lists the first two Safe type changes.",

                Result = $"[li] The data field's control will reflect the new type.{Environment.NewLine}" +
                         $"[li] The data field's default value may be adjusted if it doesn't match the new type." +
                         "[li] When you load an existing data (.ddb) file with this revised template, Timelapse will check to see if the change is allowed.",

                Hint = $"Hold the [e]<Shift>[/e] key while opening the menu to select from all types, including unsafe ones.{Environment.NewLine}" +
                       $"If you plan to open an existing data file, consider the consequences (if any) on previously entered data, e.g., " +
                       $"[li] [b]Note→MultiLine[/b] is ok: the control will now let you enter longer text." +
                       $"[li] [b]FixedChoice→MultiChoice[/b] is ok: the control will now let you do multi-selections." +
                       $"[li] [b]PositiveDecimal→Decimal[/b] is ok: the control will now let you enter negative numbers." +
                       $"[li] [b]Date→Text[/b] field is allowed with consequences, as previously entered [b]Date[/b] data will become plain text." +
                       $"[li] [b]Note→Decimal[/b] is disallowed by Timelapse: previously entered [b]Note[/b] data may be non-numeric." +
                       "[li] [b]Date→Decimal[/b] is disallowed by Timelapse: previously entered [b]Date[/b] data will never be a decimal.",
            };
            return dialog.BuildAndShowDialog();
        }

        public static void UnknownErrorDialog(Window owner)
        {
            var dialog = new FormattedDialog()
            {
                Owner = owner,
                DialogTitle = "Unknown error",
                Icon = DialogIconType.Warning,
                What = $"We aren't sure what went wrong, but just try redoing what you did before.",
                Solution = "[ni] Try again - things often work the second time around." +
                           $"[ni] If not, {emailSaulForHelp} and tell him what you were trying to do.",
            };
            dialog.BuildAndShowDialog();
        }

        public static void EditorDataLabelIsAReservedWordDialog(Window owner, string data_label)
        {
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = "'" + data_label + "' is not a valid data label.",
                Icon = DialogIconType.Warning,
                Problem = "Data labels cannot match the reserved words.",
                Result = "We will add an [b]_[/b] suffix to this data label to make it differ from the reserved word",
                Hint = $"[li] Avoid the reserved words listed in the [b]Details[/b] section below." +
                $"[li] Start your label with a letter. Then use any combination of letters, numbers, and [b]_[/b].",
                Details = "[b]Reserved words:[/b] [br 4]"
            };

            foreach (string keyword in EditorConstant.ReservedSqlKeywords)
            {
                dialog.Details += keyword + " ";
            }
            dialog.BuildAndShowDialog();

        }

        public static void EditorDateAndTimeLabelAreReservedWordsDialog(Window owner, string data_label, bool isDate)
        {
            string offendingType = isDate ? "Date" : "Time";

            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = "'" + data_label + "' is not a valid data label.",
                Icon = DialogIconType.Warning,
                Problem = $"[b]{offendingType}[/b] cannot be used as a data label.",
                Reason = $"[b]{offendingType}[/b] is already used internally by Timelapse to handle the image's date / time tag.",
                Result = $"We will add an [b]_[/b] suffix to {offendingType} to make it differ",
                Hint = $"Avoid using [b]{offendingType}[/b]. Data labels should start with a letter. Then use any combination of letters, numbers, and [b]_[/b]."
            };
            dialog.BuildAndShowDialog();
        }

        /// <summary>
        /// Data Labels cannot be empty
        /// </summary>
        public static void EditorDataLabelsCannotBeEmptyDialog(Window owner)
        {
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = "Data Labels cannot be empty",
                Icon = DialogIconType.Warning,
                Problem = "Data labels cannot be empty. They must begin with a letter, followed only by letters, numbers, and [b]_[/b].",
                Result = "We will automatically create a uniquely named data label for you.",
                Hint = "You can create your own name for this data label. Start your label with a letter. Then use any combination of letters, numbers, and [b]_[/b]."
            };
            dialog.BuildAndShowDialog();
        }

        /// <summary>
        /// Data label is not a valid data label
        /// </summary>
        public static void EditorDataLabelIsInvalidDialog(Window owner, string old_data_label, string new_data_label)
        {
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = $"'{old_data_label}' is not a valid data label.",
                Icon = DialogIconType.Warning,
                Problem = "Data labels must begin with a letter, followed only by letters, numbers, and _.",
                Result = $"We replaced all dissallowed characters with an [b]X[/b]: [i]{new_data_label}[/i]",
                Hint = "Start your data label with a letter. Then use any combination of letters, numbers, and _."
            };
            dialog.BuildAndShowDialog();
        }

        /// <summary>
        /// Data Labels must be unique
        /// </summary>
        public static void EditorDataLabelsMustBeUniqueDialog(Window owner, string data_label)
        {
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = "Data Labels must be unique.",
                Icon = DialogIconType.Warning,
                Problem = $"[e]{data_label}[/e] is not a valid data label, as you have already used it in another row.",
                Result = "We will automatically create a unique data label for you by adding a number to its end.",
                Hint = "You can create your own unique name for this data label. Start your label with a letter. Then use any combination of letters, numbers, and _."

            };
            dialog.BuildAndShowDialog();
        }

        /// <summary>
        /// Data label requirements: Data Labels can only contain letters, numbers and '_'
        /// </summary>
        public static void EditorDataLabelRequirementsDialog(Window owner)
        {
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = "Data labels can only contain letters, numbers and _.",
                Icon = DialogIconType.Warning,
                Problem = "Data labels must begin with a letter, followed only by letters, numbers, and _.",
                Result = "We will automatically ignore other characters, including spaces",
                Hint = "Start your label with a letter. Then use any combination of letters, numbers, and _."

            };
            dialog.BuildAndShowDialog();
        }

        /// <summary>
        /// Labels must be unique
        /// </summary>
        public static void EditorLabelsMustBeUniqueDialog(Window owner, string label)
        {
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = "Labels must be unique.",
                Icon = DialogIconType.Warning,
                Problem = $"[b]{label}[/b] is not a recommended label, as you have already used it in another row.",
                Result = "We will automatically create a unique label for you by adding a number to its end.",
                Hint = "You can overwrite this label with your own choice of a unique label name."
            };
            dialog.BuildAndShowDialog();
        }

        /// <summary>
        /// Data Labels cannot be empty
        /// </summary>
        public static void EditorLabelsCannotBeEmptyDialog(Window owner)
        {
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = "Labels cannot be empty.",
                Icon = DialogIconType.Warning,
                Problem = "Labels cannot be empty. They identify what each data field represents to the Timelapse user.",
                Result = "We will automatically create a uniquely named label for you.",
                Hint = "Rename this to something meaningful. It only has to be different from the other labels."
            };
            dialog.BuildAndShowDialog();
        }
        /// <summary>
        /// DefaultChoiceValuesMustMatchChoiceLists
        /// </summary>
        public static void EditorDefaultChoiceValuesMustMatchChoiceListsDialog(Window owner, string invalidDefaultValue)
        {
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = "Choice default values must match an item in the Choice menu",
                Icon = DialogIconType.Warning,
                Problem =
                    $"[b]{invalidDefaultValue}[/b] is not allowed as a default value, as it is not one of your [b]Define List[/b] items.[br 4]" +
                    $"[li] Choice default values must be either empty or must match one of those items.",
                Result = "The default value will be cleared.",
                Hint = "Copy an item from your [b]Define List[/b] and paste it into your default value field as needed."
            };
            dialog.BuildAndShowDialog();
        }

        /// <summary>
        /// EditorDefaultChoiceValuesMustMatchNonEmptyChoiceLists
        /// </summary>
        public static void EditorDefaultChoiceValuesMustMatchNonEmptyChoiceListsDialog(Window owner, string invalidDefaultValue)
        {
            var dialog = new FormattedDialog(MessageBoxButtonType.OK)
            {
                Owner = owner,
                DialogTitle = "Choice default values must match an item in the Choice menu",
                Icon = DialogIconType.Warning,
                Problem = string.IsNullOrEmpty(invalidDefaultValue)
                    ? $"An empty value is not allowed as a default value, as you have [b]Include an empty item[/b] unselected in your [b]Define List[/b] dialog.[br 4]" +
                      $"[li] Choice default values must match one of your allowed items."
                    : $"[b]{invalidDefaultValue}[/b] is not allowed as a default value, as it is not one of your [b]Define List[/b] items.[br 4]" +
                      $" [li]Choice default values must match one of those items." +
                      $"[li] An empty value is not allowed as a default value, as you have [b]Include an empty item[/b] unselected in your [b]Define List[/b] dialog.",
                Result = "The default value was set to the first item on your [b]Define List[/b] items.",
                Hint = "Change the default value if desired by copying an item from your [b]Define List[/b] and pasting it into your default value field as needed."
            };
            dialog.BuildAndShowDialog();
        }
        #endregion

        #region Switching between Template Editor and Data Editor Dialogs
        public static bool? MenuFileSwitchBetweenTimelapseAndEditorWarningDialog(Window owner, bool toTemplateEditor)
        {
            string whatToClose = toTemplateEditor ? "image set" : "template";
            string whatToOpen = toTemplateEditor ? "the Template Editor" : "Timelapse";
            string toTemplateTitle = $"Your {whatToClose} will be closed before switching";
            string toTimelapseTitle = $"Your {whatToClose} will be closed before switching";
            var dialog = new FormattedDialog()
            {
                Owner = owner,
                DialogTitle = toTemplateEditor
                    ? toTemplateTitle
                    : toTimelapseTitle,
                Icon = DialogIconType.Question,
                What = toTemplateEditor
                    ? $"{toTemplateTitle} to the {whatToOpen}."
                    : $"{toTimelapseTitle} to {whatToOpen}.",
                Reason = toTemplateEditor
                    ? $"An image set is currently loaded into Timelapse. To switch to {whatToOpen}, Timelapse needs to close it first."
                    : $"A template is currently loaded into in the Template Editor. To switch to {whatToOpen}, the editor needs to close it first.",
                Solution = "Select:[br 4]"
                         + $"[li] [b]Okay[/b] to close your {whatToClose} and switch to {whatToOpen}."
                         + "[li]  [b]Cancel[/b] to abort.",
                Result = $"The {whatToClose} will be closed and then {whatToOpen} will appear.",
                Hint = $"Switching is safe. You can always switch back later and reopen the {whatToClose}."
            };
            return dialog.BuildAndShowDialog();
        }
        #endregion

        #region Template Editor Dialogs: Changing controls can violate the current standard

        // Confirm closing this template and creating a new one
        private static bool dontShowChangesToStandardWarningDialog;
        public static bool? ChangesToStandardWarning(Window owner, string changeType, string standardType)
        {
            if (dontShowChangesToStandardWarningDialog)
            {
                return true;
            }

            string title = $"{changeType} may compromise the {standardType} standard.";
            string formattedTitle = $"[b]{changeType}[/b] may compromise the [i]{standardType}[/i] standard";

            var dialog = new FormattedDialog()
            {
                Owner = owner,
                DialogTitle = title,
                Icon = DialogIconType.Warning,
                What = $"{formattedTitle}.[br 4]"
                       + $"This may cause problems if other software you use expects a strict [i]{standardType}[/i] standard.",
                Result = $"Select:[br 4]"
                         + $"[li] [b]Okay[/b] to keep [b]{changeType.ToLower()}[/b] anyways,"
                         + "[li]  [b]Cancel[/b] to abort.",
                Reason = $"The [i]{standardType}[/i] defines what levels and fields are needed and how they are named. " +
                         "Changes to levels or fields can (perhaps) affect how other software uses your data.",
                Hint = "This is just a warning, as it really depends upon what you plan to do with your data. "
                       + "Ignore this if you know what you are doing.",
                DontShowAgain =
                {
                    Visibility = Visibility.Visible
                }
            };

            bool? result = dialog.BuildAndShowDialog();
            if (dialog.DontShowAgain.IsChecked.HasValue)
            {
                dontShowChangesToStandardWarningDialog = dialog.DontShowAgain.IsChecked.Value;
            }

            return result;
        }

        #endregion

        #region Template Editor Dialogs: DeleteFolderLevelWarning
        public static bool? EditorDeleteFolderLevelWarning(Window owner, string levelName)
        {
            // warn the user about consequences of deleting a level
            var dialog = new FormattedDialog()
            {
                Owner = owner,
                DialogTitle = $"Delete '{levelName}' folder level definition?",
                Icon = DialogIconType.Warning,
                What = $"You are about to delete the [b]{levelName}[/b] folder definition and all the controls within it (if any). [br 6]"
                    + "Just checking to make sure you really want to do this.",
                Solution = "Select:"
                    + "[li] [b]Ok[/b] to delete this level,"
                    + "[li] [b]Cancel[/b] to abort deletion.",
            };
            return dialog.BuildAndShowDialog();
        }
        #endregion
    }

}

