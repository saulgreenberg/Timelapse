using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.Dialog;
using Timelapse.State;
using Timelapse.Util;
using TimelapseTemplateEditor.EditorCode;
using Xceed.Wpf.AvalonDock.Layout;

namespace TimelapseTemplateEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class TemplateEditorWindow
    {
        #region variables
        // Database where the template is stored
        public CommonDatabase templateDatabase;

        private TimelapseState State { get; }           // Stores the UI state 
        private readonly EditorUserRegistrySettings userSettings;

        public bool dataGridBeingUpdatedByCode;             // Used to indicate when the data grid is being updated
        public MouseState mouseState = new MouseState();    // Tracks mouse state for drag/drop of controls in the preview control panel
        private bool setInitialFolderLevelTabSelection = true;         // Whether we should set the initial metadata tab
        #endregion

        #region Initialization, Window Loading, Closing 
        public TemplateEditorWindow()
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            EditorConstant.templateEditorWindow = this;
            Globals.Root = this;

            InitializeComponent();
            Title = EditorConstant.MainWindowBaseTitle;
            //Dialogs.TryFitDialogInWorkingArea(this);

            // Abort if some of the required dependencies are missing
            if (Dependencies.AreRequiredBinariesPresent(EditorConstant.ApplicationName, Assembly.GetExecutingAssembly(), out string missingAssemblies) == false)
            {
                Dialogs.DependencyFilesMissingDialog(EditorConstant.ApplicationName, missingAssemblies);
                Application.Current.Shutdown();
            }

            // Have the grid hide the ID and Order columns
            MenuViewShowAllColumns_Click(MenuViewShowAllColumns, null);

            // Recall state from prior sessions
            userSettings = new EditorUserRegistrySettings();

            // Populate the most recent databases list
            this.CreateMenuItemsForMenuFileRecentTemplates(true);

            State = new TimelapseState();
            GlobalReferences.TimelapseState = State;


            // Get the window and its size from its previous location
            // Maybe integrate this call with that?
            this.Top = userSettings.EditorWindowPosition.Top; 
            this.Left = userSettings.EditorWindowPosition.Left; 
            this.Height = userSettings.EditorWindowPosition.Height;
            this.Width = userSettings.EditorWindowPosition.Width;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Check for updates only once on every calendar day
            if (DateTime.Now.Year != userSettings.MostRecentCheckForUpdates.Year ||
                DateTime.Now.Month != userSettings.MostRecentCheckForUpdates.Month ||
                DateTime.Now.Day != userSettings.MostRecentCheckForUpdates.Day)
            {
                VersionChecks updater = new VersionChecks(this, VersionUpdates.ApplicationName, VersionUpdates.LatestVersionFileNameXML);
                updater.TryCheckForNewVersionAndDisplayResultsAsNeeded(false);
                userSettings.MostRecentCheckForUpdates = DateTime.Now;
            }

            //if (userSettings.SuppressWarningToUpdateDBFilesToSQLPrompt == false)
            //{
            //    WarningToUpdateDBFilesToSQL warning = new WarningToUpdateDBFilesToSQL(this);
            //    bool? result = warning.ShowDialog();
            //    if (result.HasValue && result.Value)
            //    {
            //        userSettings.SuppressWarningToUpdateDBFilesToSQLPrompt = warning.DontShowAgain;
            //    }
            //}
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // apply any pending edits
            DataGrid dataGrid = TemplateUI.TemplateDataGridControl.DataGridInstance;
            DataGridCommonCode.ApplyPendingEdits(dataGrid);

            // persist user specific state to the registry
            if (this.Top > -10 && this.Left > -10)
            {
                this.userSettings.EditorWindowPosition = new Rect(new Point(this.Left, this.Top), new Size(this.Width, this.Height));
            }

            // persist state to registry
            userSettings.WriteToRegistry();
        }
        #endregion

        #region Crash Exception Management
        // If we get an exception that wasn't handled, show a dialog asking the user to send the bug report to us.
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ExceptionShutdownDialog dialog = new ExceptionShutdownDialog(this, "Timelapse Editor", e);
            dialog.ShowDialog();
            // force a shutdown. While some bugs could be recoverable, its dangerous to keep things running. 
            Close();
            Application.Current.Shutdown();
        }
        #endregion

        #region Reinitializing and Ending
        private void ResetUIElements(bool templateIsLoaded, string filePath)
        {
            // Enable/disable the various menus as needed. This includes updating the recent templates list. 
            TemplatePane.IsEnabled = templateIsLoaded;
            MetadataPane.IsEnabled = templateIsLoaded;
            MenuFileNewTemplate.IsEnabled = !templateIsLoaded;
            MenuFileNewTemplateFromStandard.IsEnabled = !templateIsLoaded;
            MenuFileOpenTemplate.IsEnabled = !templateIsLoaded;

            MenuFileClose.IsEnabled = templateIsLoaded;
            MenuView.IsEnabled = templateIsLoaded;

            // repopulate the most recent databases list
            CreateMenuItemsForMenuFileRecentTemplates(!templateIsLoaded);

            // Enable/disable  all the buttons that allow rows to be added
            TemplateUI.RowControls.EditRowDockPanel.IsEnabled = templateIsLoaded;

            // Include the database file name in the window title if it is set
            Title = EditorConstant.MainWindowBaseTitle;
            Title += templateIsLoaded ? " (" + Path.GetFileName(filePath) + ")" : string.Empty;

            // Switch to the appropriate tab
            this.setInitialFolderLevelTabSelection = true;
            TemplatePane.IsActive = templateIsLoaded;
            InstructionPane.IsActive = !templateIsLoaded;
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
                if (templateDatabase != null)
                {

                    // This method is currently a placeholder until we need to do some updating
                    if (false == Dialogs.CloseTemplateAndOpenNewTemplate(this, templateDatabaseFilePath))
                    {
                        // The user aborted
                        return;
                    }
                    TemplateDoClose();
                }
                await TemplateDoOpen(templateDatabaseFilePath);
            }
        }

        private void HelpDocument_PreviewDrag(object sender, DragEventArgs dragEvent)
        {
            DragDropFile.OnTemplateFilePreviewDrag(dragEvent);
        }
        #endregion

        #region Initial tab selection
        // When a user clicks on the folder level tab for the first time after opening a template,
        // It will display the 1st folder level tab (if there is one) instead of the default Folder Data Instructions tab
        private void MetadataPane_IsSelectedChanged(object sender, EventArgs e)
        {
            if (sender is LayoutDocument layoutDocument && layoutDocument.IsActive)
            {
                if (setInitialFolderLevelTabSelection && MetadataUI.MetadataTabs.Items.Count > 1)
                {
                   MetadataUI.MetadataTabs.SelectedIndex = 1; 
                   setInitialFolderLevelTabSelection = false;
                }
            }
        }
        #endregion
    }
}
