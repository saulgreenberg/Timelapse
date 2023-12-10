using System;
using System.Reflection;
using System.Windows;
using Constant = Timelapse.Constant ;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.Dialog;
using Timelapse.State;
using Timelapse.Util;
using TimelapseTemplateEditor.EditorCode;
using System.IO;

namespace TimelapseTemplateEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class TemplateEditorWindow 
    {
        #region variables
        // Database where the template is stored
        public TemplateDatabase templateDatabase;
        
        private TimelapseState State { get;}           // Stores the UI state 
        private readonly EditorUserRegistrySettings userSettings;

        public bool dataGridBeingUpdatedByCode;             // Used to indicate when the data grid is being updated
        public MouseState mouseState = new MouseState();    // Tracks mouse state for drag/drop of controls in the preview control panel
        #endregion

        #region Initialization, Window Loading, Closing 
        public TemplateEditorWindow()
        {
            AppDomain.CurrentDomain.UnhandledException += this.OnUnhandledException;
            EditorConstant.templateEditorWindow = this;
            Globals.Root = this;

            this.InitializeComponent();
            this.Title = EditorConstant.MainWindowBaseTitle;
            //Dialogs.TryFitDialogInWorkingArea(this);

            // Abort if some of the required dependencies are missing
            if (Dependencies.AreRequiredBinariesPresent(EditorConstant.ApplicationName, Assembly.GetExecutingAssembly(), out string missingAssemblies) == false)
            {
                Dialogs.DependencyFilesMissingDialog(EditorConstant.ApplicationName, missingAssemblies);
                Application.Current.Shutdown();
            }

            // Have the grid hide the ID and Order columns
            this.MenuViewShowAllColumns_Click(this.MenuViewShowAllColumns, null);

            // Recall state from prior sessions
            this.userSettings = new EditorUserRegistrySettings();

            // Populate the most recent databases list
            this.MenuFileRecentTemplates_Refresh(true);

            this.State = new TimelapseState();
            GlobalReferences.TimelapseState = this.State;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Check for updates only once on every calendar day
            if (DateTime.Now.Year != this.userSettings.MostRecentCheckForUpdates.Year ||
                DateTime.Now.Month != this.userSettings.MostRecentCheckForUpdates.Month ||
                DateTime.Now.Day != this.userSettings.MostRecentCheckForUpdates.Day)
            {
                VersionChecks updater = new VersionChecks(this, Constant.VersionUpdates.ApplicationName, Constant.VersionUpdates.LatestVersionFileNameXML);
                updater.TryCheckForNewVersionAndDisplayResultsAsNeeded(false);
                this.userSettings.MostRecentCheckForUpdates = DateTime.Now;
            }

            if (this.userSettings.SuppressWarningToUpdateDBFilesToSQLPrompt == false)
            {
                WarningToUpdateDBFilesToSQL warning = new WarningToUpdateDBFilesToSQL(this);
                bool? result = warning.ShowDialog();
                if (result.HasValue && result.Value)
                {
                    this.userSettings.SuppressWarningToUpdateDBFilesToSQLPrompt = warning.DontShowAgain;
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // apply any pending edits
            this.TemplateDoApplyPendingEdits();

            // persist state to registry
            this.userSettings.WriteToRegistry();
        }
        #endregion

        #region Crash Exception Management
        // If we get an exception that wasn't handled, show a dialog asking the user to send the bug report to us.
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ExceptionShutdownDialog dialog = new ExceptionShutdownDialog(this, "Timelapse Editor", e);
            dialog.ShowDialog();
            // force a shutdown. While some bugs could be recoverable, its dangerous to keep things running. 
            this.Close();
            Application.Current.Shutdown();
        }
        #endregion

        #region Reinitializing and Ending
        private void ResetUIElements(bool templateIsLoaded, string filePath)
        {
            // Enable/disable the various menus as needed. This includes updating the recent templates list. 
            this.MenuFileNewTemplate.IsEnabled = !templateIsLoaded;
            this.MenuFileOpenTemplate.IsEnabled = !templateIsLoaded;

            this.MenuFileClose.IsEnabled = templateIsLoaded;
            this.MenuView.IsEnabled = templateIsLoaded;

            // repopulate the most recent databases list
            this.MenuFileRecentTemplates_Refresh(!templateIsLoaded);

            // Enable/disable  all the buttons that allow rows to be added
            this.TemplateUI.RowControls.AddCounterButton.IsEnabled = templateIsLoaded;
            this.TemplateUI.RowControls.AddFixedChoiceButton.IsEnabled = templateIsLoaded;
            this.TemplateUI.RowControls.AddNoteButton.IsEnabled = templateIsLoaded;
            this.TemplateUI.RowControls.AddFlagButton.IsEnabled = templateIsLoaded;

            // Include the database file name in the window title if it is set
            this.Title = EditorConstant.MainWindowBaseTitle;
            this.Title += templateIsLoaded ? " (" + Path.GetFileName(filePath) + ")" : string.Empty;

            // Switch to the appropriate tab
            this.TemplatePane.IsActive = templateIsLoaded;
            this.InstructionPane.IsActive = !templateIsLoaded;
        }
        #endregion

        #region Drag and Drop tdb files to open them
        // Dragging and dropping a .tdb file on the help window will open that file 
        private async void HelpDocument_Drop(object sender, DragEventArgs dropEvent)
        {
            if (DragDropFile.IsTemplateFileDragging(dropEvent, out string templateDatabaseFilePath))
            {
                // If its not a valid template, display a dialog and abort
                if (false == Dialogs.DialogIsFileValid(this, templateDatabaseFilePath))
                {
                    return;
                }
                if (this.templateDatabase != null)
                {

                    // This method is currently a placeholder until we need to do some updating
                    if (false == Dialogs.CloseTemplateAndOpenNewTemplate(this, templateDatabaseFilePath))
                    {
                        // The user aborted
                        return;
                    }
                    this.TemplateDoClose();
                }
                await this.TemplateDoOpen(templateDatabaseFilePath);
            }
        }

        private void HelpDocument_PreviewDrag(object sender, DragEventArgs dragEvent)
        {
            DragDropFile.OnTemplateFilePreviewDrag(dragEvent);
        }
        #endregion
    }
}
