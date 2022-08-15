using DialogUpgradeFiles;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions; // For debugging
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.Dialog;
using Timelapse.Editor.Dialog;
using Timelapse.Editor.Util;
using Timelapse.Util;

namespace Timelapse.Editor
{
    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1126:PrefixCallsCorrectly", Justification = "Reviewed.")]
    public partial class EditorWindow : Window
    {
        // state tracking
        private readonly EditorControls controls;
        private bool dataGridBeingUpdatedByCode;
        private readonly EditorUserRegistrySettings userSettings;

        // These variables support the drag/drop of controls
        private readonly UIElement dummyMouseDragSource;
        private bool isMouseDown;
        private bool isMouseDragging;
        private Point mouseDownStartPosition;
        private UIElement realMouseDragSource;

        // database where the template is stored
        private TemplateDatabase templateDatabase;

        public TimelapseState State { get; set; }                       // Status information concerning the state of the UI

        #region Initialization, Window Loading, Closing and Crashing
        /// <summary>
        /// Starts the UI.
        /// </summary>
        public EditorWindow()
        {
            AppDomain.CurrentDomain.UnhandledException += this.OnUnhandledException;
            this.InitializeComponent();
            this.Title = EditorConstant.MainWindowBaseTitle;
            //Dialogs.TryFitDialogInWorkingArea(this);

            // Abort if some of the required dependencies are missing
            if (Dependencies.AreRequiredBinariesPresent(EditorConstant.ApplicationName, Assembly.GetExecutingAssembly()) == false)
            {
                Dialogs.DependencyFilesMissingDialog(EditorConstant.ApplicationName);
                Application.Current.Shutdown();
            }

            this.controls = new EditorControls();
            this.dummyMouseDragSource = new UIElement();
            this.dataGridBeingUpdatedByCode = false;

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
                    this.userSettings.SuppressWarningToUpdateDBFilesToSQLPrompt = warning.DontShowAgain == true;
                }
            }
        }

        // Invoke the Update Timelapse files program
        private void MenuItemUpgradeTimelapseFiles_Click(object sender, RoutedEventArgs e)
        {
            DialogUpgradeFilesAndFolders dialogUpdateFiles = new DialogUpgradeFiles.DialogUpgradeFilesAndFolders(this, String.Empty, VersionChecks.GetTimelapseCurrentVersionNumber().ToString());
            dialogUpdateFiles.ShowDialog();
        }


        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // apply any pending edits
            this.ApplyPendingEdits();

            // persist state to registry
            this.userSettings.WriteToRegistry();
        }

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
            this.AddCounterButton.IsEnabled = templateIsLoaded;
            this.AddFixedChoiceButton.IsEnabled = templateIsLoaded;
            this.AddNoteButton.IsEnabled = templateIsLoaded;
            this.AddFlagButton.IsEnabled = templateIsLoaded;

            // Include the database file name in the window title if it is set
            this.Title = EditorConstant.MainWindowBaseTitle;
            this.Title += templateIsLoaded ? " (" + Path.GetFileName(filePath) + ")" : String.Empty;

            // Switch to the appropriate tab
            this.TemplatePane.IsActive = templateIsLoaded;
            this.InstructionPane.IsActive = !templateIsLoaded;
        }
        #endregion

        #region File Menu Callbacks
        /// <summary>
        /// Creates a new database file of a user chosen name in a user chosen location.
        /// </summary>
        private async void MenuFileNewTemplate_Click(object sender, RoutedEventArgs e)
        {
            this.ApplyPendingEdits();

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
            Nullable<bool> result = newTemplateFilePathDialog.ShowDialog();

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
                if (IsCondition.IsPathLengthTooLong(templateFileName) || templateFileName.Equals(Path.GetFileNameWithoutExtension(Constant.File.DefaultTemplateDatabaseFileName)))
                {
                    Dialogs.TemplatePathTooLongDialog(this, templateFileName);
                    return;
                }

                // Overwrite the file if it exists
                if (File.Exists(templateFileName))
                {
                    FileBackup.TryCreateBackup(templateFileName);
                    File.Delete(templateFileName);
                }

                // Open document 
                await this.InitializeDataGridAsync(templateFileName).ConfigureAwait(true);
                this.HelpMessageInitial.Visibility = Visibility.Collapsed;
                this.MenuFileClose.IsEnabled = true;
            }
        }

        #region Open Templates
        /// <summary>
        /// Opens an existing database file.
        /// </summary>
        private async void MenuFileOpenTemplate_Click(object sender, RoutedEventArgs e)
        {
            this.ApplyPendingEdits();

            // Note that if we try to open a file with a too long path, the open file dialog will just say that it doesn't exist (which is a bad error message, but nothing we can do about it)
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                FileName = Path.GetFileNameWithoutExtension(Constant.File.DefaultTemplateDatabaseFileName), // Default file name without the extension
                DefaultExt = Constant.File.TemplateDatabaseFileExtension, // Default file extension
                Filter = "Database Files (" + Constant.File.TemplateDatabaseFileExtension + ")|*" + Constant.File.TemplateDatabaseFileExtension, // Filter files by extension 
                Title = "Select an Existing Template File to Open"
            };

            // Show open file dialog box
            Nullable<bool> result = openFileDialog.ShowDialog();

            // Process open file dialog box results 
            if (result == true)
            {
                // If its not a valid template, display a dialog and abort
                if (false == Dialogs.DialogIsFileValid(this, openFileDialog.FileName))
                {
                    return;
                }
                await this.DoOpenTemplate(openFileDialog.FileName);
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
            await DoOpenTemplate(recentTemplatePath);
        }

        private async Task DoOpenTemplate(string templateFilePath)
        {
            // This likely isn't needed as the OpenFileDialog won't let us do that anyways. But just in case...
            if (IsCondition.IsPathLengthTooLong(templateFilePath))
            {
                Dialogs.TemplatePathTooLongDialog(this, templateFilePath);
                return;
            }

            if (false == await this.InitializeDataGridAsync(templateFilePath).ConfigureAwait(true))
            {
                Mouse.OverrideCursor = null;
                Dialogs.TemplateFileNotLoadedAsCorruptDialog(this, templateFilePath);
                return;
            }

            // Update the verson number in the templateInfo table to the current version
            if (this.templateDatabase != null)
            {
                this.templateDatabase.UpdateVersionNumber(VersionChecks.GetTimelapseCurrentVersionNumber().ToString());
            }
            this.HelpMessageInitial.Visibility = Visibility.Collapsed;
            this.MenuFileClose.IsEnabled = true;
        }
        #endregion

        #region Close Templates
        /// <summary>
        /// Closes the template and clears various states to allow another template to be created or opened.
        /// </summary>
        private void MenuFileClose_Click(object sender, RoutedEventArgs e)
        {
            this.DoCloseTemplate();
        }

        private void DoCloseTemplate()
        {
            this.ApplyPendingEdits();

            // Close the DB file 
            this.templateDatabase = null;
            this.TemplateDataGrid.ItemsSource = null;

            // Update the user interface specified by the contents of the table
            this.ControlsPanel.Children.Clear();
            this.SpreadsheetPreview.Columns.Clear();

            // Enable/disable the various menus as needed.
            this.ResetUIElements(false, String.Empty);
        }
        #endregion

        /// <summary>
        /// Exits the application.
        /// </summary>
        private void MenuFileExit_Click(object sender, RoutedEventArgs e)
        {
            // Note that Window_Closing, which does some cleanup, will be invoked as a side effect
            Application.Current.Shutdown();
        }
        #endregion

        #region View Menu Callbacks
        /// <summary>
        /// Depending on the menu's checkbox state, show all columns or hide selected columns
        /// </summary>
        private void MenuViewShowAllColumns_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                Visibility visibility = mi.IsChecked ? Visibility.Visible : Visibility.Collapsed;
                foreach (DataGridColumn column in this.TemplateDataGrid.Columns)
                {
                    if (column.Header.Equals(EditorConstant.ColumnHeader.ID) ||
                        column.Header.Equals(EditorConstant.ColumnHeader.ControlOrder) ||
                        column.Header.Equals(EditorConstant.ColumnHeader.SpreadsheetOrder))
                    {
                        column.Visibility = visibility;
                    }
                }
            }
        }

        /// <summary>
        /// Show the dialog that allows a user to inspect image metadata
        /// </summary>
        private void MenuItemInspectImageMetadata_Click(object sender, RoutedEventArgs e)
        {
            InspectMetadata inspectMetadata = new InspectMetadata(this);
            inspectMetadata.ShowDialog();
            this.State.ExifToolManager.Stop();
        }
        #endregion

        #region Other menu related items
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
                if (File.Exists(recentTemplatePath) && Path.GetExtension(recentTemplatePath) == Constant.File.TemplateDatabaseFileExtension)
                {
                    MenuItem recentImageSetItem = new MenuItem();
                    recentImageSetItem.Click += this.MenuItemRecentTemplate_Click;
                    recentImageSetItem.Header = String.Format("_{0} {1}", index, recentTemplatePath);
                    recentImageSetItem.ToolTip = recentTemplatePath;
                    this.MenuFileRecentTemplates.Items.Add(recentImageSetItem);
                    ++index;
                }
            }
        }
        #endregion

        #region DataGrid and New Database Initialization
        /// <summary>
        /// Given a database file path,create a new DB file if one does not exist, or load a DB file if there is one.
        /// After a DB file is loaded, the table is extracted and loaded a DataTable for binding to the DataGrid.
        /// Some listeners are added to the DataTable, and the DataTable is bound. The add row buttons are enabled.
        /// </summary>
        /// <param name="templateDatabaseFilePath">The path of the DB file created or loaded</param>
        private async Task<bool> InitializeDataGridAsync(string templateDatabaseFilePath)
        {
            // Create a new DB file if one does not exist, or load a DB file if there is one.
            this.templateDatabase = await TemplateDatabase.CreateOrOpenAsync(templateDatabaseFilePath).ConfigureAwait(true);
            if (this.templateDatabase == null)
            {
                return false;
            }
            // Map the data table to the data grid, and create a callback executed whenever the datatable row changes
            this.templateDatabase.BindToEditorDataGrid(this.TemplateDataGrid, this.TemplateDataTable_RowChanged);

            // Update the user interface specified by the contents of the table
            this.controls.Generate(this.ControlsPanel, this.templateDatabase.Controls);
            this.GenerateSpreadsheet();

            // Enable/disable the various UI elements as needed.
            this.userSettings.MostRecentTemplates.SetMostRecent(templateDatabaseFilePath);
            this.ResetUIElements(true, this.templateDatabase.FilePath);
            return true;
        }
        #endregion DataGrid and New Database Initialization

        #region Data Changed Listeners and Methods
        /// <summary>
        /// Updates a given control in the database with the current state of the DataGrid. 
        /// </summary>
        private void SyncControlToDatabase(ControlRow control)
        {
            this.dataGridBeingUpdatedByCode = true;

            this.templateDatabase.SyncControlToDatabase(control);
            this.controls.Generate(this.ControlsPanel, this.templateDatabase.Controls);
            this.GenerateSpreadsheet();

            this.dataGridBeingUpdatedByCode = false;
        }

        /// <summary>
        /// Listener: Whenever a row changes, save that row to the database, which also updates the grid colors.
        /// Note that bulk changes due to code update defers this, so that updates can be done collectively and more efficiently later
        /// </summary>
        private void TemplateDataTable_RowChanged(object sender, DataRowChangeEventArgs e)
        {
            // Utilities.PrintMethodName();
            if (this.dataGridBeingUpdatedByCode == false)
            {
                this.SyncControlToDatabase(new ControlRow(e.Row));
            }
        }
        #endregion Data Changed Listeners and Methods

        #region Datagrid Row Modifiers listeners and methods
        /// <summary>
        /// Logic to enable/disable editing buttons depending on there being a row selection
        /// Also sets the text for the remove row button.
        /// </summary>
        private void TemplateDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.TemplateDataGrid.SelectedItem is DataRowView selectedRowView)
            {
                ControlRow control = new ControlRow(selectedRowView.Row);
                this.RemoveControlButton.IsEnabled = !Constant.Control.StandardTypes.Contains(control.Type);
            }
            else
            {
                this.RemoveControlButton.IsEnabled = false;
            }
        }

        /// <summary>
        /// Adds a row to the table. The row type is decided by the button tags.
        /// Default values are set for the added row, differing depending on type.
        /// </summary>
        private void AddControlButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            string controlType = button.Tag.ToString();

            // Commit any edits that are in progress
            this.ApplyPendingEdits();

            this.dataGridBeingUpdatedByCode = true;

            this.templateDatabase.AddUserDefinedControl(controlType);
            this.TemplateDataGrid.DataContext = this.templateDatabase.Controls;
            this.TemplateDataGrid.ScrollIntoView(this.TemplateDataGrid.Items[this.TemplateDataGrid.Items.Count - 1]);

            this.GenerateSpreadsheet();
            this.OnControlOrderChanged();

            this.dataGridBeingUpdatedByCode = false;
        }

        /// <summary>
        /// Removes a row from the table and shifts up the ids on the remaining rows.
        /// The required rows are unable to be deleted.
        /// </summary>
        private void RemoveControlButton_Click(object sender, RoutedEventArgs e)
        {
            // Commit any edits that are in progress. 
            // Likely not needed as this row will be removed anyways, but just in case.
            this.ApplyPendingEdits();

            if (this.TemplateDataGrid.SelectedItem is DataRowView selectedRowView && selectedRowView.Row != null)
            {
                ControlRow control = new ControlRow(selectedRowView.Row);
                if (EditorControls.IsStandardControlType(control.Type))
                {
                    // standard controls cannot be removed
                    return;
                }
                this.dataGridBeingUpdatedByCode = true;
                // remove the control, then update the view so it reflects the current values in the database
                this.templateDatabase.RemoveUserDefinedControl(new ControlRow(selectedRowView.Row));
                this.controls.Generate(this.ControlsPanel, this.templateDatabase.Controls);
                this.GenerateSpreadsheet();
                this.dataGridBeingUpdatedByCode = false;
            }
        }

        // Apply and commit any pending edits that may be pending 
        // (e.g., invoked in cases where the enter key was not pressed)
        private void ApplyPendingEdits()
        {
            this.dataGridBeingUpdatedByCode = false;
            this.TemplateDataGrid.CommitEdit();
        }
        #endregion Datagrid Row Modifyiers listeners and methods

        #region Choice List Box Handlers
        // When the  choice list button is clicked, raise a dialog box that lets the user edit the list of choices
        private void ChoiceListButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;

            // The button tag holds the Control Order of the row the button is in, not the ID.
            // So we have to search through the rows to find the one with the correct control order
            // and retrieve / set the ItemList menu in that row.
            ControlRow choiceControl = this.templateDatabase.Controls.FirstOrDefault(control => control.ControlOrder.ToString().Equals(button.Tag.ToString()));
            if (choiceControl == null)
            {
                TracePrint.PrintMessage(String.Format("Control named {0} not found.", button.Tag));
                return;
            }

            Choices choices = Choices.ChoicesFromJson(choiceControl.List);
            Dialog.EditChoiceList choiceListDialog = new Dialog.EditChoiceList(button, choices, this);
            bool? result = choiceListDialog.ShowDialog();
            if (result == true)
            {
                // Ensure that choices disallowing empties have an appropriate default value
                if (false == choiceListDialog.Choices.IncludeEmptyChoice && (string.IsNullOrEmpty(choiceControl.DefaultValue) || false == choiceListDialog.Choices.Contains(choiceControl.DefaultValue)))
                {
                    EditorDialogs.EditorDefaultChoiceValuesMustMatchNonEmptyChoiceListsDialog(this, choiceControl.DefaultValue);
                    choiceControl.DefaultValue = choiceListDialog.Choices.ChoiceList[0];
                }
                // Ensure that non-empty default values matches an entry on the edited choice menu
                else if (!string.IsNullOrEmpty(choiceControl.DefaultValue) && choiceListDialog.Choices.Contains(choiceControl.DefaultValue) == false)
                {
                    EditorDialogs.EditorDefaultChoiceValuesMustMatchChoiceListsDialog(this, choiceControl.DefaultValue);
                    choiceControl.DefaultValue = String.Empty;
                }
                choiceControl.List = choiceListDialog.Choices.GetAsJson;
                this.SyncControlToDatabase(choiceControl);
            }
        }
        #endregion

        #region Cell Editing / Coloring Listeners and Methods
        // Cell editing: Preview character by character entry to disallow spaces in particular fields (DataLabels, Width, Counters
        private void TemplateDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((this.TryGetCurrentCell(out DataGridCell currentCell, out DataGridRow currentRow) == false) || currentCell.Background.Equals(EditorConstant.NotEditableCellColor))
            {
                e.Handled = true;
                return;
            }

            switch ((string)this.TemplateDataGrid.CurrentColumn.Header)
            {
                case EditorConstant.ColumnHeader.DataLabel:
                    // Datalabels should not accept spaces - display a warning if needed
                    if (e.Key == Key.Space)
                    {
                        EditorDialogs.EditorDataLabelRequirementsDialog(this);
                        e.Handled = true;
                    }
                    // If its a tab, commit the edit before going to the next cell
                    if (e.Key == Key.Tab)
                    {
                        this.ApplyPendingEdits();
                    }
                    break;
                case EditorConstant.ColumnHeader.Width:
                    // Width should  not accept spaces 
                    e.Handled = e.Key == Key.Space;
                    break;
                case EditorConstant.ColumnHeader.DefaultValue:
                    // Default value for Counters should not accept spaces 
                    ControlRow control = new ControlRow((currentRow.Item as DataRowView).Row);
                    if (control.Type == Constant.Control.Counter)
                    {
                        e.Handled = e.Key == Key.Space;
                    }
                    break;
                default:
                    break;
            }
        }

        // Cell editing: Preview final string entry
        // Accept only numbers in counters and widths, t and f in flags (translated to true and false), and alphanumeric or _ in datalabels
        private void TemplateDataGrid_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if ((this.TryGetCurrentCell(out DataGridCell currentCell, out DataGridRow currentRow) == false) || currentCell.Background.Equals(EditorConstant.NotEditableCellColor))
            {
                e.Handled = true;
                return;
            }

            switch ((string)this.TemplateDataGrid.CurrentColumn.Header)
            {
                // EditorConstant.Control.ControlOrder is not editable
                case EditorConstant.ColumnHeader.DataLabel:
                    // Only allow alphanumeric and '_' in data labels
                    if ((!IsCondition.IsLetterOrDigit(e.Text)) && !e.Text.Equals("_"))
                    {
                        EditorDialogs.EditorDataLabelRequirementsDialog(this);
                        e.Handled = true;
                    }
                    break;

                case EditorConstant.ColumnHeader.DefaultValue:
                    // Restrict certain default values for counters, flags (and perhaps fixed choices in the future)
                    ControlRow control = new ControlRow((currentRow.Item as DataRowView).Row);
                    switch (control.Type)
                    {
                        case Constant.Control.Counter:
                            // Only allow numbers in counters 
                            e.Handled = !IsCondition.IsDigits(e.Text);
                            break;
                        case Constant.Control.Flag:
                            // Only allow t/f and translate to true/false
                            if (e.Text == "t" || e.Text == "T")
                            {
                                control.DefaultValue = Constant.BooleanValue.True;
                                this.SyncControlToDatabase(control);
                            }
                            else if (e.Text == "f" || e.Text == "F")
                            {
                                control.DefaultValue = Constant.BooleanValue.False;
                                this.SyncControlToDatabase(control);
                            }
                            e.Handled = true;
                            break;
                        case Constant.Control.FixedChoice:
                            // The default value should be constrained to one of the choices, but that introduces a chicken and egg problem
                            // So lets just ignore it for now.
                            break;
                        case Constant.Control.Note:
                        default:
                            // no restrictions on Notes 
                            break;
                    }
                    break;
                case EditorConstant.ColumnHeader.Width:
                    // Only allow digits in widths as they must be parseable as integers
                    e.Handled = !IsCondition.IsDigits(e.Text);
                    break;
                default:
                    // no restrictions on any of the other editable coumns
                    break;
            }
        }

        /// <summary>
        /// Before cell editing begins on a cell click, the cell is disabled if it is grey (meaning cannot be edited).
        /// Another method re-enables the cell immediately afterwards.
        /// The reason for this implementation is because disabled cells cannot be single clicked, which is needed for row actions.
        /// </summary>
        private void TemplateDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (this.TryGetCurrentCell(out DataGridCell currentCell, out _) == false)
            {
                return;
            }

            if (currentCell.Background.Equals(EditorConstant.NotEditableCellColor))
            {
                currentCell.IsEnabled = false;
                this.TemplateDataGrid.CancelEdit();
            }
        }

        /// <summary>
        /// After cell editing ends (prematurely or no), re-enable disabled cells.
        /// See TemplateDataGrid_BeginningEdit for full explanation.
        /// </summary>
        private void TemplateDataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            if (this.TryGetCurrentCell(out DataGridCell currentCell, out _) == false)
            {
                return;
            }
            if (currentCell.Background.Equals(EditorConstant.NotEditableCellColor))
            {
                currentCell.IsEnabled = true;
            }
        }

        private bool manualCommitEdit;
        // After editing is complete, validate the data labels, default values, and widths as needed.
        // Also commit the edit, which will raise the RowChanged event
        private void TemplateDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Stop re-entering, which can occur after we manually perform a CommitEdit (see below) 
            if (this.manualCommitEdit == true)
            {
                this.manualCommitEdit = false;
                return;
            }

            DataGridRow editedRow = e.Row;
            if (editedRow == null)
            {
                return;
            }
            DataGridColumn editedColumn = e.Column;
            if (editedColumn == null)
            {
                return;
            }

            switch ((string)editedColumn.Header)
            {
                case EditorConstant.ColumnHeader.DataLabel:
                    this.ValidateDataLabel(e);
                    break;
                case EditorConstant.ColumnHeader.DefaultValue:
                    this.ValidateDefaults(e, editedRow);
                    break;
                case Constant.Control.Label:
                    this.ValidateLabels(e, editedRow);
                    break;
                case EditorConstant.ColumnHeader.Width:
                    ValidateWidths(e, editedRow);
                    break;
                default:
                    // no restrictions on any of the other editable columns
                    break;
            }

            // While hitting return after editing (say) a note will raise a RowChanged event, 
            // clicking out of that cell does not raise a RowChangedEvent, even though the cell has been edited. 
            // Thus we manually commit the edit. 
            this.dataGridBeingUpdatedByCode = false;
            this.manualCommitEdit = true;
            this.TemplateDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        /// <summary>
        /// Updates colors when rows are added, moved, or deleted.
        /// </summary>
        private void TemplateDataGrid_LayoutUpdated(object sender, EventArgs e)
        {
            // Greys out cells and updates the visibility of particular rows as defined by logic. 
            // This is to  show the user uneditable cells. Color is also used by code to check whether a cell can be edited.
            // This method should be called after row are added/moved/deleted to update the colors. 
            // This also disables checkboxes that cannot be edited. Disabling checkboxes does not effect row interactions.
            // Finally, it collapses or shows various date-associated rows.
            for (int rowIndex = 0; rowIndex < this.TemplateDataGrid.Items.Count; rowIndex++)
            {
                // In order for ItemContainerGenerator to work, we need to set the TemplateGrid in the XAML to VirtualizingStackPanel.IsVirtualizing="False"
                // Alternately, we could just do the following, which may be more efficient for large grids (which we normally don't have)
                // this.TemplateDataGrid.UpdateLayout();
                // this.TemplateDataGrid.ScrollIntoView(rowIndex + 1);
                DataGridRow row = (DataGridRow)this.TemplateDataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex);
                if (row == null)
                {
                    return;
                }

                // grid cells are editable by default
                // disable cells which should not be editable
                DataGridCellsPresenter presenter = VisualChildren.GetVisualChild<DataGridCellsPresenter>(row);
                for (int column = 0; column < this.TemplateDataGrid.Columns.Count; column++)
                {
                    DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(column);
                    if (cell == null)
                    {
                        // cell will be null for columns with Visibility = Hidden
                        continue;
                    }

                    ControlRow control = new ControlRow(((DataRowView)this.TemplateDataGrid.Items[rowIndex]).Row);
                    string controlType = control.Type;

                    // These columns should always be editable
                    // Note that Width is normally editable unless it is a Flag (as the checkbox is set to the optimal width)
                    string columnHeader = (string)this.TemplateDataGrid.Columns[column].Header;
                    if ((columnHeader == Constant.Control.Label) ||
                        (columnHeader == Constant.Control.Tooltip) ||
                        (columnHeader == Constant.Control.Visible) ||
                        (columnHeader == EditorConstant.ColumnHeader.Width && (control.Type != Constant.DatabaseColumn.DeleteFlag && control.Type != Constant.Control.Flag)))
                    {
                        cell.SetValue(DataGridCell.IsTabStopProperty, true); // Allow tabbing in non-editable fields
                        continue;
                    }

                    // The following attributes should NOT be editable.
                    ContentPresenter cellContent = cell.Content as ContentPresenter;
                    string sortMemberPath = this.TemplateDataGrid.Columns[column].SortMemberPath;
                    if (String.Equals(sortMemberPath, Constant.DatabaseColumn.ID, StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(sortMemberPath, Constant.Control.ControlOrder, StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(sortMemberPath, Constant.Control.SpreadsheetOrder, StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(sortMemberPath, Constant.Control.Type, StringComparison.OrdinalIgnoreCase) ||
                        (controlType == Constant.DatabaseColumn.DateTime) ||
                        (controlType == Constant.DatabaseColumn.DeleteFlag) ||
                        (controlType == Constant.DatabaseColumn.File) ||
                        (controlType == Constant.DatabaseColumn.RelativePath) ||
                        ((controlType == Constant.Control.Flag) && (columnHeader == EditorConstant.ColumnHeader.Width)) ||
                        ((controlType == Constant.Control.Counter) && (columnHeader == Constant.Control.List)) ||
                        ((controlType == Constant.Control.Flag) && (columnHeader == Constant.Control.List)) ||
                        ((controlType == Constant.Control.Note) && (columnHeader == Constant.Control.List)))
                    {
                        cell.Background = EditorConstant.NotEditableCellColor;
                        cell.Foreground = Brushes.Gray;
                        cell.SetValue(DataGridCell.IsTabStopProperty, false);  // Disallow tabbing in non-editable fields

                        // if cell has a checkbox, also disable it.
                        if (cellContent != null)
                        {
                            if (cellContent.ContentTemplate.FindName("CheckBox", cellContent) is CheckBox checkbox)
                            {
                                checkbox.IsEnabled = false;
                            }
                        }
                    }
                    else
                    {
                        cell.ClearValue(DataGridCell.BackgroundProperty); // otherwise when scrolling cells offscreen get colored randomly
                        cell.SetValue(DataGridCell.IsTabStopProperty, true);
                        // if cell has a checkbox, enable it.
                        if (cellContent != null)
                        {
                            if (cellContent.ContentTemplate.FindName("CheckBox", cellContent) is CheckBox checkbox)
                            {
                                checkbox.IsEnabled = true;
                            }
                        }
                    }
                }
            }
        }
        #endregion Cell Editing / Coloring Listeners and Methods

        #region Retrieving cells from the datagrid
        // If we can, return the curentCell and the current Row
        private bool TryGetCurrentCell(out DataGridCell currentCell, out DataGridRow currentRow)
        {
            if ((this.TemplateDataGrid.SelectedIndex == -1) || (this.TemplateDataGrid.CurrentColumn == null))
            {
                currentCell = null;
                currentRow = null;
                return false;
            }

            currentRow = (DataGridRow)this.TemplateDataGrid.ItemContainerGenerator.ContainerFromIndex(this.TemplateDataGrid.SelectedIndex);
            DataGridCellsPresenter presenter = VisualChildren.GetVisualChild<DataGridCellsPresenter>(currentRow);
            currentCell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(this.TemplateDataGrid.CurrentColumn.DisplayIndex);
            return currentCell != null;
        }
        #endregion

        #region Validation of Cell contents
        // Validate the data label to correct for empty, duplicate, or non-legal naming
        private void ValidateDataLabel(DataGridCellEditEndingEventArgs e)
        {
            // Check to see if the data label entered is a reserved word or if its a non-unique label
            TextBox textBox = e.EditingElement as TextBox;
            string dataLabel = textBox.Text;

            // Check to see if the data label is empty. If it is, generate a unique data label and warn the user
            if (String.IsNullOrWhiteSpace(dataLabel))
            {
                EditorDialogs.EditorDataLabelsCannotBeEmptyDialog(this);
                textBox.Text = this.templateDatabase.GetNextUniqueDataLabel("DataLabel");
            }

            // Check to see if the data label is unique. If not, generate a unique data label and warn the user
            for (int row = 0; row < this.templateDatabase.Controls.RowCount; row++)
            {
                ControlRow control = this.templateDatabase.Controls[row];
                if (dataLabel.Equals(control.DataLabel))
                {
                    if (this.TemplateDataGrid.SelectedIndex == row)
                    {
                        continue; // Its the same row, so its the same key, so skip it
                    }
                    EditorDialogs.EditorDataLabelsMustBeUniqueDialog(this, textBox.Text);
                    textBox.Text = this.templateDatabase.GetNextUniqueDataLabel(dataLabel);
                    break;
                }
            }

            // Check to see if the label (if its not empty, which it shouldn't be) has any illegal characters.
            // Note that most of this is redundant, as we have already checked for illegal characters as they are typed. However,
            // we have not checked to see if the first letter is alphabetic.
            if (dataLabel.Length > 0)
            {
                Regex alphanumdash = new Regex("^[a-zA-Z0-9_]*$");
                Regex alpha = new Regex("^[a-zA-Z]*$");

                string firstCharacter = dataLabel[0].ToString();

                if (!(alpha.IsMatch(firstCharacter) && alphanumdash.IsMatch(dataLabel)))
                {
                    string replacementDataLabel = dataLabel;

                    if (!alpha.IsMatch(firstCharacter))
                    {
                        replacementDataLabel = "X" + replacementDataLabel.Substring(1);
                    }
                    replacementDataLabel = Regex.Replace(replacementDataLabel, @"[^A-Za-z0-9_]+", "X");

                    EditorDialogs.EditorDataLabelIsInvalidDialog(this, textBox.Text, replacementDataLabel);
                    textBox.Text = replacementDataLabel;
                }
            }

            // Check to see if its a reserved word
            foreach (string sqlKeyword in EditorConstant.ReservedSqlKeywords)
            {
                if (String.Equals(sqlKeyword, dataLabel, StringComparison.OrdinalIgnoreCase))
                {
                    EditorDialogs.EditorDataLabelIsAReservedWordDialog(this, textBox.Text);
                    textBox.Text += "_";
                    break;
                }
            }
        }

        // Check to see if the current label is a duplicate of another label
        private void ValidateLabels(DataGridCellEditEndingEventArgs e, DataGridRow currentRow)
        {
            bool editorDialogAlreadyShown = false;

            // ControlRow currentControl = new ControlRow((currentRow.Item as DataRowView).Row);
            if (e.EditingElement is TextBox textBox)
            {
                string label = textBox.Text;
                // Check to see if the data label is empty. If it is, generate a unique data label and warn the user
                if (String.IsNullOrWhiteSpace(label))
                {
                    EditorDialogs.EditorLabelsCannotBeEmptyDialog(this);
                    if (currentRow != null)
                    {
                        DataGridCellsPresenter presenter = VisualChildren.GetVisualChild<DataGridCellsPresenter>(currentRow);

                        // try to get the cell but it may possibly be virtualized
                        DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(6);
                        string s = (cell == null)
                            ? "Label"
                            : ((TextBlock)cell.Content).Text;
                        textBox.Text = s; //this.templateDatabase.GetNextUniqueLabel(s);
                        label = s;
                        editorDialogAlreadyShown = true;
                    }
                }

                // Check to see if the data label is unique. If not, generate a unique data label and warn the user
                for (int row = 0; row < this.templateDatabase.Controls.RowCount; row++)
                {
                    ControlRow control = this.templateDatabase.Controls[row];
                    if (label.Equals(control.Label))
                    {
                        if (this.TemplateDataGrid.SelectedIndex == row)
                        {
                            continue; // Its the same row, so its the same key, so skip it
                        }
                        if (false == editorDialogAlreadyShown)
                        {
                            EditorDialogs.EditorLabelsMustBeUniqueDialog(this, label);
                        }
                        textBox.Text = this.templateDatabase.GetNextUniqueLabel(label);
                        break;
                    }
                }
            }
        }

        // Validation of Defaults: Particular defaults (Flags) cannot be empty 
        // There is no need to check other validations here (e.g., unallowed characters) as these will have been caught previously 
        private void ValidateDefaults(DataGridCellEditEndingEventArgs e, DataGridRow currentRow)
        {
            ControlRow control = new ControlRow((currentRow.Item as DataRowView).Row);
            TextBox textBox = e.EditingElement as TextBox;
            switch (control.Type)
            {
                case Constant.Control.Flag:
                    if (String.IsNullOrWhiteSpace(textBox.Text))
                    {
                        textBox.Text = Constant.ControlDefault.FlagValue;
                    }
                    break;
                case Constant.Control.FixedChoice:
                    // Check to see if the value matches one of the items on the menu
                    Choices choices = Choices.ChoicesFromJson(control.List);
                    if (choices.IncludeEmptyChoice == false && String.IsNullOrWhiteSpace(textBox.Text))
                    {
                        // Can't have an empty default if the IncludeEmptyChoice is unselecteds
                        EditorDialogs.EditorDefaultChoiceValuesMustMatchNonEmptyChoiceListsDialog(this, textBox.Text);
                        if (choices.ChoiceList.Count > 0)
                        {
                            // Set it to the first item
                            textBox.Text = choices.ChoiceList[0];
                        }
                        // Note: Undefined if we have an empty choice list with IncludeEmptyChoice of false!
                    }
                    else if (String.IsNullOrWhiteSpace(textBox.Text) == false)
                    {
                        List<string> choicesList = choices.ChoiceList;
                        if (choicesList.Contains(textBox.Text) == false)
                        {
                            EditorDialogs.EditorDefaultChoiceValuesMustMatchChoiceListsDialog(this, textBox.Text);
                            if (choices.IncludeEmptyChoice)
                            {
                                textBox.Text = String.Empty;
                            }
                            else
                            {
                                if (choices.ChoiceList.Count > 0)
                                {
                                    // Set it to the first item
                                    textBox.Text = choices.ChoiceList[0];
                                }
                                // Note: Undefined if we have an empty choice list with IncludeEmptyChoice of false!
                            }
                        }
                    }
                    break;
                case Constant.Control.Counter:
                case Constant.Control.Note:
                default:
                    // empty fields are allowed in these control types
                    break;
            }
        }

        // Validation of Widths: if a control's width is empty, reset it to its corresponding default width
        private static void ValidateWidths(DataGridCellEditEndingEventArgs e, DataGridRow currentRow)
        {
            TextBox textBox = e.EditingElement as TextBox;
            if (!String.IsNullOrWhiteSpace(textBox.Text))
            {
                return;
            }

            ControlRow control = new ControlRow((currentRow.Item as DataRowView).Row);
            switch (control.Type)
            {
                case Constant.DatabaseColumn.File:
                    textBox.Text = Constant.ControlDefault.FileWidth.ToString();
                    break;
                case Constant.DatabaseColumn.DateTime:
                    textBox.Text = Constant.ControlDefault.DateTimeWidth.ToString();
                    break;
                case Constant.DatabaseColumn.RelativePath:
                    textBox.Text = Constant.ControlDefault.RelativePathWidth.ToString();
                    break;
                case Constant.DatabaseColumn.DeleteFlag:
                case Constant.Control.Flag:
                    textBox.Text = Constant.ControlDefault.FlagWidth.ToString();
                    break;
                case Constant.Control.FixedChoice:
                    textBox.Text = Constant.ControlDefault.FixedChoiceWidth.ToString();
                    break;
                case Constant.Control.Counter:
                    textBox.Text = Constant.ControlDefault.CounterWidth.ToString();
                    break;
                case Constant.Control.Note:
                    textBox.Text = Constant.ControlDefault.NoteWidth.ToString();
                    break;
                default:
                    Constant.ControlDefault.NoteWidth.ToString();
                    break;
            }
        }
        #endregion

        #region Spreadsheet Appearance
        // Generate the spreadsheet, adjusting the DateTime and UTCOffset visibility as needed
        private void GenerateSpreadsheet()
        {
            List<ControlRow> controlsInSpreadsheetOrder = this.templateDatabase.Controls.OrderBy(control => control.SpreadsheetOrder).ToList();
            this.SpreadsheetPreview.Columns.Clear();

            // Find the DateTime Control 
            ControlRow dateTimeControl = null;
            foreach (ControlRow control in controlsInSpreadsheetOrder)
            {
                if (control.Type == Constant.DatabaseColumn.DateTime)
                {
                    dateTimeControl = control;
                    break;
                }
            }

            // Now generate the spreadsheet columns as needed. 
            foreach (ControlRow control in controlsInSpreadsheetOrder)
            {
                DataGridTextColumn column = new DataGridTextColumn();
                string dataLabel = control.DataLabel;
                if (String.IsNullOrEmpty(dataLabel))
                {
                    TracePrint.PrintMessage("GenerateSpreadsheet: Database constructors should guarantee data labels are not null.");
                }
                else
                {
                    column.Header = dataLabel;
                    this.SpreadsheetPreview.Columns.Add(column);
                }
            }
        }

        // When the spreadsheet order is changd, update the database
        private void OnSpreadsheetOrderChanged(object sender, DataGridColumnEventArgs e)
        {
            DataGrid dataGrid = (DataGrid)sender;
            Dictionary<string, long> spreadsheetOrderByDataLabel = new Dictionary<string, long>();
            for (int control = 0; control < dataGrid.Columns.Count; control++)
            {
                string dataLabelFromColumnHeader = dataGrid.Columns[control].Header.ToString();
                long newSpreadsheetOrder = dataGrid.Columns[control].DisplayIndex + 1;
                spreadsheetOrderByDataLabel.Add(dataLabelFromColumnHeader, newSpreadsheetOrder);
            }
            this.dataGridBeingUpdatedByCode = true;
            this.templateDatabase.UpdateDisplayOrder(Constant.Control.SpreadsheetOrder, spreadsheetOrderByDataLabel);
            this.dataGridBeingUpdatedByCode = false;
        }
        #endregion

        #region Dragging and Dropping of Controls to Reorder them
        private void ControlsPanel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source != this.ControlsPanel)
            {
                this.isMouseDown = true;
                this.mouseDownStartPosition = e.GetPosition(this.ControlsPanel);
            }
        }

        private void ControlsPanel_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                this.isMouseDown = false;
                this.isMouseDragging = false;
                if (!(this.realMouseDragSource == null))
                {
                    this.realMouseDragSource.ReleaseMouseCapture();
                }
            }
            catch
            {
            }
        }

        private void ControlsPanel_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (this.isMouseDown)
            {
                Point currentMousePosition = e.GetPosition(this.ControlsPanel);
                if ((this.isMouseDragging == false) &&
                    ((Math.Abs(currentMousePosition.X - this.mouseDownStartPosition.X) > SystemParameters.MinimumHorizontalDragDistance) ||
                     (Math.Abs(currentMousePosition.Y - this.mouseDownStartPosition.Y) > SystemParameters.MinimumVerticalDragDistance)))
                {
                    this.isMouseDragging = true;
                    this.realMouseDragSource = e.Source as UIElement;
                    this.realMouseDragSource.CaptureMouse();
                    DragDrop.DoDragDrop(this.dummyMouseDragSource, new DataObject("UIElement", e.Source, true), DragDropEffects.Move);
                }
            }
        }

        private void ControlsPanel_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("UIElement"))
            {
                e.Effects = DragDropEffects.Move;
            }
        }

        private void ControlsPanel_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("UIElement"))
            {
                UIElement dropTarget = e.Source as UIElement;
                int control = 0;
                int dropTargetIndex = -1;
                foreach (UIElement element in this.ControlsPanel.Children)
                {
                    if (element.Equals(dropTarget))
                    {
                        dropTargetIndex = control;
                        break;
                    }
                    else
                    {
                        // Check if its a stack panel, and if so check to see if its children are the drop target
                        if (element is StackPanel stackPanel)
                        {
                            // Check the children...
                            foreach (UIElement subelement in stackPanel.Children)
                            {
                                if (subelement.Equals(dropTarget))
                                {
                                    dropTargetIndex = control;
                                    break;
                                }
                            }
                        }
                    }
                    control++;
                }
                if (dropTargetIndex != -1)
                {
                    if (!(this.realMouseDragSource is StackPanel))
                    {
                        StackPanel parent = FindVisualParent<StackPanel>(this.realMouseDragSource);
                        this.realMouseDragSource = parent;
                    }
                    this.ControlsPanel.Children.Remove(this.realMouseDragSource);
                    this.ControlsPanel.Children.Insert(dropTargetIndex, this.realMouseDragSource);
                    this.OnControlOrderChanged();
                }

                this.isMouseDown = false;
                this.isMouseDragging = false;
                this.realMouseDragSource.ReleaseMouseCapture();
            }
        }

        private static T FindVisualParent<T>(UIElement element) where T : UIElement
        {
            UIElement parent = element;
            while (parent != null)
            {
                if (parent is T correctlyTyped)
                {
                    return correctlyTyped;
                }
                parent = VisualTreeHelper.GetParent(parent) as UIElement;
            }
            return null;
        }

        private void OnControlOrderChanged()
        {
            Dictionary<string, long> newControlOrderByDataLabel = new Dictionary<string, long>();
            long controlOrder = 1;
            foreach (UIElement element in this.ControlsPanel.Children)
            {
                if (!(element is StackPanel stackPanel))
                {
                    continue;
                }
                newControlOrderByDataLabel.Add((string)stackPanel.Tag, controlOrder);
                controlOrder++;
            }
            this.dataGridBeingUpdatedByCode = true;
            this.templateDatabase.UpdateDisplayOrder(Constant.Control.ControlOrder, newControlOrderByDataLabel);
            this.dataGridBeingUpdatedByCode = false;
            this.controls.Generate(this.ControlsPanel, this.templateDatabase.Controls); // Ensures that the controls panel updates itself
        }
        #endregion

        #region Template Drag and Drop
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
                    this.DoCloseTemplate();
                }
                await this.DoOpenTemplate(templateDatabaseFilePath);
            }
        }

        private void HelpDocument_PreviewDrag(object sender, DragEventArgs dragEvent)
        {
            DragDropFile.OnTemplateFilePreviewDrag(dragEvent);
        }
        #endregion
    }
}
