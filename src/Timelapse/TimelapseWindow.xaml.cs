using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.ControlsDataEntry;
using Timelapse.ControlsMetadata;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.EventArguments;
using Timelapse.Extensions;
using Timelapse.Images;
using Timelapse.QuickPaste;
using Timelapse.State;
using Timelapse.Util;
using TimelapseWpf.Toolkit;
using Arguments = Timelapse.DataStructures.Arguments;
using Control = Timelapse.Constant.Control;
using File = System.IO.File;

namespace Timelapse
{
    /// <summary>
    /// main window for Timelapse
    /// </summary>
    public partial class TimelapseWindow
    {
        #region Public Properties 
        public DataEntryHandler DataHandler { get; set; }
        public MetadataDataEntryHandler MetadataDataHandler { get; set; }
        public TimelapseState State { get; set; }                       // Status information concerning the state of the UI

        public TimelapseTemplateEditor.TemplateEditorWindow TimelapseTemplateEditor { get; set; }
        public string RootPathToImages
        {
            get
            {
                if (DataHandler == null)
                {
                    Debug.Print("Error in RootPathToImages - datahandler is null");
                    return string.Empty;
                }

                return DataHandler.FileDatabase.RootPathToImages;
            }
        }

        public string RootPathToDatabase
        {
            get
            {
                if (DataHandler == null)
                {
                    Debug.Print("Error in RootPathToDatabase - datahandler is null");
                    return string.Empty;
                }

                return DataHandler.FileDatabase.RootPathToDatabase;
            }
        }

        #endregion

        #region Private Variables
        private bool disposed;
        private List<MarkersForCounter> markersOnCurrentFile;   // Holds a list of all markers for each counter on the current file

        private CommonDatabase templateDatabase;                      // The database that holds the template
        private IInputElement lastControlWithFocus;              // The last control (data, copyprevious button, or FileNavigatorSlider) that had the focus, so we can reset it

        private List<QuickPasteEntry> quickPasteEntries;              // 0 or more custum paste entries that can be created or edited by the user
        private QuickPasteWindow quickPasteWindow;

        private ImageAdjuster ImageAdjuster;    // The image adjuster controls


        // Timer for periodically updating images as the ImageNavigator slider is being used
        private readonly DispatcherTimer timerFileNavigator;

        // Timer for fixing bug in MultilineText where a tab in the dropdown goes to the counter instead of the next control
        readonly DispatcherTimer timerRestoreFocusOnLastRememberedItem;

        // Timer used to AutoPlay images via MediaControl buttons
        private readonly DispatcherTimer FilePlayerTimer = new();
        private readonly DispatcherTimer DataGridSelectionsTimer = new();

        // Notifier: A modern notification system
        public ModernNotifier ToastNotifier;

        public ImageDogear ImageDogear; 

        // Record any command line arguments
        public Arguments Arguments { get; set; } = new(Environment.GetCommandLineArgs());
        #endregion

        #region Main
        public TimelapseWindow()
        {

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            InitializeComponent();

            // Clean up any leftover print preview files from previous sessions
            PrintFlowDocument.CleanupOldPreviewFiles();

            // Register MarkableCanvas callbacks
            MarkableCanvas.PreviewMouseDown += MarkableCanvas_PreviewMouseDown;
            MarkableCanvas.MouseEnter += MarkableCanvas_MouseEnter;
            MarkableCanvas.MarkerEvent += MarkableCanvas_RaiseMarkerEvent;
            MarkableCanvas.ThumbnailGrid.DoubleClick += ThumbnailGrid_DoubleClick;
            MarkableCanvas.ThumbnailGrid.SelectionChanged += ThumbanilGrid_SelectionChanged;
            MarkableCanvas.SwitchedToThumbnailGridViewEventAction += SwitchedToThumbnailGrid;
            MarkableCanvas.SwitchedToSingleImageViewEventAction += SwitchedToSingleImagesView;

            // Save/restore the focus whenever we leave / enter the control grid (which contains controls pluse the copy previous button, or the file navigator
            ControlGrid.MouseEnter += FocusRestoreOn_MouseEnter;
            ControlGrid.MouseLeave += FocusSaveOn_MouseLeave;

            // Set the window's title
            Title = Defaults.MainWindowBaseTitle;

            // Recall user's state from prior sessions
            State = new();
            State.ReadSettingsFromRegistry();
            Episodes.Episodes.TimeThreshold = State.EpisodeTimeThreshold; // so we don't have to pass it as a parameter
            MarkableCanvas.SetBookmark(State.BookmarkScale, State.BookmarkTranslation);

            // Populate the global references so we can access these from other objects without going thorugh the hassle of passing arguments around
            // Yup, poor practice but...
            GlobalReferences.MainWindow = this; // So other classes can access methods here
            GlobalReferences.BusyCancelIndicator = BusyCancelIndicator; // So other classes can access methods here
            GlobalReferences.CancelTokenSource = new();     // Set the CancellationToken/Source. Only set globally
            GlobalReferences.TimelapseState = State;

            // Populate the most recent image set list
            MenuItemRecentImageSets_RefreshItems();

            // Timer to force the image to update to the current slider position when the user pauses while dragging the  slider 
            timerFileNavigator = new()
            {
                Interval = State.Throttles.DesiredIntervalBetweenRenders
            };
            timerFileNavigator.Tick += TimerFileNavigator_Tick;

            // Timer to force the focus to the last saved focused control
            timerRestoreFocusOnLastRememberedItem = new()
            {
                Interval = TimeSpan.FromMilliseconds(0)
            };
            timerRestoreFocusOnLastRememberedItem.Tick += (_, _) =>
            {
                // Sets the focus on the last remembered focused element, if any
                FocusRestoreOn_MouseEnter(null, null);
                timerRestoreFocusOnLastRememberedItem.Stop();
            };

            // Callback to ensure Video AutoPlay stops when the user clicks on it
            FileNavigatorSlider.PreviewMouseDown += ContentControl_MouseDown;
            FileNavigatorSliderReset();

            // Timer activated / deactivated by Video Autoplay media control buttons
            FilePlayerTimer.Tick += FilePlayerTimer_Tick;

            DataGridSelectionsTimer.Tick += DataGridSelectionsTimer_Tick;
            DataGridSelectionsTimer.Interval = ThrottleValues.DataGridTimerInterval;

            // Get the window and its size from its previous location
            // SAULXX: Note that this is actually redundant, as if AvalonLayout_TryLoad succeeds it will do it again.
            // Maybe integrate this call with that?
            Top = State.TimelapseWindowPosition.Y;
            Left = State.TimelapseWindowPosition.X;
            Height = State.TimelapseWindowPosition.Height;
            Width = State.TimelapseWindowPosition.Width;

            // Mute the harmless 'System.Windows.Data Error: 4 : Cannot find source for binding with reference' (I think its from Avalon dock)
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Critical;
        }
        #endregion

        #region Window Loading, Closing
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Abort if some of the required dependencies are missing
            if (Dependencies.AreRequiredBinariesPresent(Assembly.GetExecutingAssembly(), out string missingAssemblies) == false)
            {
                Dialogs.DependencyFilesMissingDialog(missingAssemblies);
                Application.Current.Shutdown();
            }

            // Check for updates at least once a day
            if (DateTime.Now.Year != State.MostRecentCheckForUpdates.Year ||
            DateTime.Now.Month != State.MostRecentCheckForUpdates.Month ||
            DateTime.Now.Day != State.MostRecentCheckForUpdates.Day)
            {
                VersionChecks updater = new(this, VersionUpdates.ApplicationName, VersionUpdates.LatestVersionFileNameXML);
                updater.TryCheckForNewVersionAndDisplayResultsAsNeeded(false);
                State.MostRecentCheckForUpdates = DateTime.Now;
            }
            if (State.FirstTimeFileLoading)
            {
                // Load the previously saved layout. If there is none, TryLoad will default to a reasonable layout and window size/position.
                this.AvalonLayout_TryLoad(AvalonLayoutTags.LastUsed);
                //FolderMetadataPane.IsEnabled = false; // Needed as Try Load will set it to true if that was its last state
                State.FirstTimeFileLoading = false;
            }

            if (!SystemStatus.CheckAndGetLangaugeAndCulture(out _, out _, out string displayname))
            {
                HelpDocument.WarningRegionLanguage = displayname;
            }

            // By default, we now set everything to use the Invariant culture, although this 
            // isn't tested to ensure it works
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            // Add a context menu to the data controls that allows a user to restore default values to the current or selected file
            // I originally had this in the XAML, but for some reason it complains if put there.
            ContextMenu menuRestoreDefaults = new();
            MenuItem menuItemRestoreDefaults = new();

            menuItemRestoreDefaults.Click += MenuItemRestoreDefaultValues_Click;
            menuRestoreDefaults.Opened += MenuRestoreDefaults_Opened;
            menuRestoreDefaults.Items.Add(menuItemRestoreDefaults);
            DataEntryControls.ContextMenu = menuRestoreDefaults;
            DataEntryControlPanel.IsVisible = false;
            InstructionPane.IsActive = true;
            EnableOrDisableMenusAndControls();

            // Initialize the modern notification system
            ToastNotifier = new(this);

            // Depending on the arguments, we may open Timelapse in particular wasy, or initiate it with a supplied template and/or data file
            this.HandleArgumentsOnOpen();
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            FindBoxSetVisibility(false);
        }

        // On exiting, save various attributes so we can use recover them later
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            CloseTimelapseAndSaveState(true);
        }

        private void DeleteTheDeletedFilesFolderIfNeeded()
        {
            string deletedFolderPath = Path.Combine(DataHandler.FileDatabase.RootPathToImages, Constant.File.DeletedFilesFolder);
            string[] extensions = [Constant.File.JpgFileExtension, Constant.File.ASFFileExtension, Constant.File.AviFileExtension, Constant.File.MovFileExtension, Constant.File.Mp4FileExtension
            ];
            int howManyDeletedFiles = Directory.Exists(deletedFolderPath) 
                ? Directory.GetFiles(deletedFolderPath, "*.*", SearchOption.AllDirectories).Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                    .ToArray().Length
                :0;

            // If there are no files, there is nothing to delete
            if (howManyDeletedFiles <= 0)
            {
                return;
            }

            // We either have auto deletion, or ask the user. Check both cases.
            // If its auto deletion, then set the flag to delete
            bool deleteTheDeletedFolder = State.DeleteFolderManagement == DeleteFolderManagementEnum.AutoDeleteOnExit;

            // if its ask the user, then set the flag according to the response
            if (State.DeleteFolderManagement == DeleteFolderManagementEnum.AskToDeleteOnExit)
            {
                DeleteDeleteFolder deleteDeletedFolders = new(howManyDeletedFiles)
                {
                    Owner = this
                };
                deleteTheDeletedFolder = deleteDeletedFolders.ShowDialog() == true;
            }
            if (deleteTheDeletedFolder)
            {
                Directory.Delete(deletedFolderPath, true);
            }
        }
        #endregion

        #region Disposing
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                DataHandler?.Dispose();
                MetadataDataHandler?.Dispose();
            }
            disposed = true;
        }
        #endregion

        #region Exception Management
        // If we get an exception that wasn't handled, show a dialog asking the user to send the bug report to us.
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject.ToString()!.Contains("System.IO.PathTooLongException"))
            {
                Dialogs.FilePathTooLongDialog(this, e);
            }
            else
            {
                ExceptionShutdownDialog dialog = new(this, e);
                dialog.ShowDialog();
                // force a shutdown. While some bugs could be recoverable, its dangerous to keep things running. 
                Close();
                Application.Current.Shutdown();
            }
        }
        #endregion

        #region Control Callbacks
        /// <summary>
        /// Add user interface event handler callbacks for (possibly invisible) controls
        /// </summary>
        private void SetUserInterfaceCallbacks()
        {
            // Add data entry callbacks to all editable controls. When the user changes an image's attribute using a particular control,
            // the callback updates the matching field for that image in the database.
            foreach (KeyValuePair<string, DataEntryControl> pair in DataEntryControls.ControlsByDataLabelThatAreVisible)
            {
                string controlType = DataHandler.FileDatabase.FileTableColumnsByDataLabel[pair.Key].ControlType;
                switch (controlType)
                {
                    case Control.Counter:
                        DataEntryCounter counter = (DataEntryCounter)pair.Value;
                        counter.ContentControl.PreviewMouseDown += ContentControl_MouseDown;
                        counter.ContentControl.PreviewKeyDown += ContentCtl_PreviewKeyDown;
                        counter.Container.MouseEnter += CounterControl_MouseEnter;
                        counter.Container.MouseLeave += CounterControl_MouseLeave;
                        counter.LabelControl.Click += CounterControl_Click;
                        break;
                    case Control.IntegerAny:
                        DataEntryIntegerAny integerAny = (DataEntryIntegerAny)pair.Value;
                        integerAny.ContentControl.PreviewMouseDown += ContentControl_MouseDown;
                        integerAny.ContentControl.PreviewKeyDown += ContentCtl_PreviewKeyDown;
                        break;
                    case Control.IntegerPositive:
                        DataEntryIntegerPositive integerPositive = (DataEntryIntegerPositive)pair.Value;
                        integerPositive.ContentControl.PreviewMouseDown += ContentControl_MouseDown;
                        integerPositive.ContentControl.PreviewKeyDown += ContentCtl_PreviewKeyDown;
                        break;
                    case Control.DecimalAny:
                        DataEntryDecimalAny decimalAny = (DataEntryDecimalAny)pair.Value;
                        decimalAny.ContentControl.PreviewMouseDown += ContentControl_MouseDown;
                        decimalAny.ContentControl.PreviewKeyDown += ContentCtl_PreviewKeyDown;
                        break;
                    case Control.DecimalPositive:
                        DataEntryDecimalPositive decimalPositive = (DataEntryDecimalPositive)pair.Value;
                        decimalPositive.ContentControl.PreviewMouseDown += ContentControl_MouseDown;
                        decimalPositive.ContentControl.PreviewKeyDown += ContentCtl_PreviewKeyDown;
                        break;
                    case Control.Flag:
                    case DatabaseColumn.DeleteFlag:
                        DataEntryFlag flag = (DataEntryFlag)pair.Value;
                        flag.ContentControl.PreviewMouseDown += ContentControl_MouseDown;
                        flag.ContentControl.PreviewKeyDown += ContentCtl_PreviewKeyDown;
                        break;
                    case Control.FixedChoice:
                        DataEntryChoice choice = (DataEntryChoice)pair.Value;
                        choice.ContentControl.PreviewMouseDown += ContentControl_MouseDown;
                        choice.ContentControl.PreviewKeyDown += ContentCtl_PreviewKeyDown;
                        break;
                    case Control.MultiChoice:
                        DataEntryMultiChoice multichoice = (DataEntryMultiChoice)pair.Value;
                        multichoice.ContentControl.PreviewMouseDown += ContentControl_MouseDown;
                        multichoice.ContentControl.PreviewKeyDown += ContentCtl_PreviewKeyDown;
                        break;
                    case Control.Note:
                    case DatabaseColumn.File:
                    case DatabaseColumn.RelativePath:
                        DataEntryNote note = (DataEntryNote)pair.Value;
                        note.ContentControl.PreviewMouseDown += ContentControl_MouseDown;
                        note.ContentControl.PreviewKeyDown += ContentCtl_PreviewKeyDown;
                        break;
                    case Control.AlphaNumeric:
                        DataEntryAlphaNumeric alphaNumeric = (DataEntryAlphaNumeric)pair.Value;
                        alphaNumeric.ContentControl.PreviewMouseDown += ContentControl_MouseDown;
                        alphaNumeric.ContentControl.PreviewKeyDown += ContentCtl_PreviewKeyDown;
                        break;
                    case Control.MultiLine:
                        DataEntryMultiLine multiLine = (DataEntryMultiLine)pair.Value;
                        multiLine.ContentControl.PreviewMouseDown += ContentControl_MouseDown;
                        multiLine.ContentControl.PreviewKeyDown += ContentCtl_PreviewKeyDown;
                        break;
                    case DatabaseColumn.DateTime:
                        DataEntryDateTime dateTime = (DataEntryDateTime)pair.Value;
                        dateTime.ContentControl.PreviewMouseDown += ContentControl_MouseDown;
                        dateTime.ContentControl.PreviewKeyDown += ContentCtl_PreviewKeyDown;
                        break;
                    case Control.DateTime_:
                        DataEntryDateTimeCustom dateTimeCustom = (DataEntryDateTimeCustom)pair.Value;
                        dateTimeCustom.ContentControl.PreviewMouseDown += ContentControl_MouseDown;
                        dateTimeCustom.ContentControl.PreviewKeyDown += ContentCtl_PreviewKeyDown;
                        break;
                    case Control.Date_:
                        DataEntryDate date = (DataEntryDate)pair.Value;
                        date.ContentControl.PreviewMouseDown += ContentControl_MouseDown;
                        date.ContentControl.PreviewKeyDown += ContentCtl_PreviewKeyDown;
                        break;
                    case Control.Time_:
                        DataEntryTime time = (DataEntryTime)pair.Value;
                        time.ContentControl.PreviewMouseDown += ContentControl_MouseDown;
                        time.ContentControl.PreviewKeyDown += ContentCtl_PreviewKeyDown;
                        break;
                    default:
                        TracePrint.PrintMessage($"Unhandled control type '{controlType}' in SetUserInterfaceCallbacks.");
                        break;
                }
            }
        }

        // This preview callback is used by all controls on receipt of a mouse down, 
        // to ensure the FilePlayer is stopped when the user clicks into it
        private void ContentControl_MouseDown(object sender, RoutedEventArgs e)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going
        }


        /// <summary>
        /// This preview callback is used by all controls to reset the focus.
        /// Whenever the user hits enter over the control, set the focus back to the top-level
        /// </summary>
        /// <param name="sender">source of the event</param>
        /// <param name="eventArgs">event information</param>
        private void ContentCtl_PreviewKeyDown(object sender, KeyEventArgs eventArgs)
        {
            if (IsCondition.IsKeyReturnOrEnter(eventArgs.Key) && sender is IInputElement inputElement)
            {
                // If a return or enter key was pressed, remember the control that had the focus
                // This is because the return / enter will reset the focus to the markable canvas,
                // and if we do a tab from there it will return to this control 
                lastControlWithFocus = inputElement;
            }
            if (sender is IntegerUpDown counterOrInteger)
            {
                if (counterOrInteger.Value == int.MaxValue)
                {
                    counterOrInteger.Value = null;
                }
            }
            else if (sender is DoubleUpDown doubleUpDown)
            {
                // A hack to make the counter control ellipsis work - see SetBogusCounterContentAndTooltip()
                if (doubleUpDown.Value >= int.MaxValue)
                {
                    doubleUpDown.Value = null;
                }
            }
            else if (eventArgs.Key == Key.Tab && sender is MultiLineText multiline)
            {
                multiline.Focus();
                Keyboard.Focus(multiline);
                MoveFocusToNextOrPreviousControlOrCopyPreviousButton(Keyboard.Modifiers == ModifierKeys.Shift);
                timerRestoreFocusOnLastRememberedItem.Start();
            }

            else if (eventArgs.Key == Key.Tab && sender is WatermarkCheckComboBox checkComboBox)
            {
                checkComboBox.IsDropDownOpen = false;
                checkComboBox.Focus();
                Keyboard.Focus(checkComboBox);
                MoveFocusToNextOrPreviousControlOrCopyPreviousButton(Keyboard.Modifiers == ModifierKeys.Shift);
                timerRestoreFocusOnLastRememberedItem.Start();
            }

            // We need to allow returns from the MultiLineTex, otherwise the return resets the focus to the markable canvas.
            // Maybe modify this so it only does this if the return is in the Multiline Edit popup window?
            if (IsCondition.IsKeyReturnOrEnter(eventArgs.Key)) // && false == sender is MultiLineText)
            {
                TrySetKeyboardFocusToMarkableCanvas(false, eventArgs);
                eventArgs.Handled = true;
                FilePlayer_Stop(); // In case the FilePlayer is going
            }

            // The 'empty else' means don't check to see if a textbox or control has the focus, as we want to reset the focus elsewhere
        }

        /// <summary>Click callback: When the user selects a counter, refresh the markers, which will also readjust the colors and emphasis</summary>
        /// <param name="sender">the event source</param>
        /// <param name="e">event information</param>
        private void CounterControl_Click(object sender, RoutedEventArgs e)
        {
            MarkableCanvas_UpdateMarkers();
            FilePlayer_Stop(); // In case the FilePlayer is going
        }

        /// <summary>Highlight the markers associated with a counter when the mouse enters it</summary>
        private void CounterControl_MouseEnter(object sender, MouseEventArgs e)
        {
            Panel panel = (Panel)sender;
            State.MouseOverCounter = ((DataEntryCounter)panel.Tag).DataLabel;
            MarkableCanvas_UpdateMarkers();
        }

        /// <summary>Remove marker highlighting associated with a counter when the mouse leaves it</summary>
        private void CounterControl_MouseLeave(object sender, MouseEventArgs e)
        {
            // Recolor the marks
            State.MouseOverCounter = null;
            MarkableCanvas_UpdateMarkers();
        }

        // When the Control Grid size changes, reposition the CopyPrevious Button depending on the width/height ratio
        private void ControlGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Height + 212 > e.NewSize.Width) // We include 250, as otherwise it will bounce around as repositioning the button changes the size
            {
                // Place the button at the bottom right of the grid
                Grid.SetRow(CopyPreviousValuesButton, 1);
                Grid.SetColumn(CopyPreviousValuesButton, 0);
                CopyPreviousValuesButton.HorizontalAlignment = HorizontalAlignment.Stretch;
                CopyPreviousValuesButton.VerticalAlignment = VerticalAlignment.Top;
                CopyPreviousValuesButton.Margin = new(0, 5, 0, 5);
            }
            else
            {
                // Place the button at the right of the grid
                Grid.SetRow(CopyPreviousValuesButton, 0);
                Grid.SetColumn(CopyPreviousValuesButton, 1);
                CopyPreviousValuesButton.HorizontalAlignment = HorizontalAlignment.Right;
                CopyPreviousValuesButton.VerticalAlignment = VerticalAlignment.Stretch;
                CopyPreviousValuesButton.Margin = new(5, 0, 5, 0);
            }
        }
        #endregion

        #region Recent File Sets
        /// <summary>
        /// Update the list of recent databases displayed under Timelapse|File -> Recent Databases.
        /// </summary>
        public void MenuItemRecentImageSets_RefreshItems()
        {
            // I now just show the tdb file paths (but disabled) in the recent image sets list, even if the file is no longer there
            // For now, I have kept this code in case I want to revert to the prior behavior
            // remove image sets which are no longer present from the most recently used list
            // probably overkill to perform this check on every refresh rather than once at application launch, but it's not particularly expensive
            //List<string> invalidPaths = [];
            //foreach (string recentImageSetPath in State.MostRecentImageSets)
            //{
            //    if (File.Exists(recentImageSetPath) == false)
            //    {
            //        invalidPaths.Add(recentImageSetPath);
            //    }
            //}

            //foreach (string path in invalidPaths)
            //{
            //    bool result = State.MostRecentImageSets.TryRemove(path);
            //    if (!result)
            //    {
            //        TracePrint.PrintMessage(
            //            $"Removal of image set '{path}' no longer present on disk unexpectedly failed.");
            //    }
            //}

            // Enable the menu only when there are items in it and only if the load menu is also enabled (i.e., that we haven't loaded anything yet)
            MenuItemRecentImageSets.IsEnabled = State.RecentlyOpenedTemplateFiles.Count > 0 && MenuItemLoadFiles.IsEnabled;
            MenuItemRecentImageSets.Items.Clear();

            // add menu items most recently used image sets, where its enable state depends upon whether that the file indicated in the menu item exists
            int index = 1;
            foreach (string recentImageSetPath in State.RecentlyOpenedTemplateFiles)
            {
                // Create a menu item for each path
                MenuItem recentImageSetItem = new();
                recentImageSetItem.Click += MenuItemRecentImageSet_Click;
                recentImageSetItem.Header = $"_{index++} {recentImageSetPath}";
                recentImageSetItem.ToolTip = recentImageSetPath;
                recentImageSetItem.IsEnabled = File.Exists(recentImageSetPath);
                MenuItemRecentImageSets.Items.Add(recentImageSetItem);
            }
        }
        #endregion

        #region Folder Selection Dialogs
        // Open a dialog where the user selects one or more folders that contain the image set(s)
        private bool ShowFolderSelectionDialog(string rootFolderPath, out string selectedFolderPath)
        {
            using CommonOpenFileDialog folderSelectionDialog = new();
            folderSelectionDialog.Title = "Select a folder ...";
            folderSelectionDialog.DefaultDirectory = rootFolderPath;
            folderSelectionDialog.IsFolderPicker = true;
            folderSelectionDialog.Multiselect = false;
            folderSelectionDialog.InitialDirectory = folderSelectionDialog.DefaultDirectory;
            folderSelectionDialog.FolderChanging += FolderSelectionDialog_FolderChanging;
            if (folderSelectionDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                IEnumerable<string> folderPaths = folderSelectionDialog.FileNames;
                selectedFolderPath = folderPaths.First();
                return true;
            }
            selectedFolderPath = null;
            return false;
        }
        /// <summary>
        /// File menu helper function.
        /// Note that it accesses this.FullSubFolderPath
        /// </summary>
        private void FolderSelectionDialog_FolderChanging(object sender, CommonFileDialogFolderChangeEventArgs e)
        {
            // require folders to be loaded be either the same folder as the .tdb and .ddb or subfolders of it
            if (e.Folder.StartsWith(RootPathToImages, StringComparison.OrdinalIgnoreCase) == false)
            {
                e.Cancel = true;
            }
        }
        #endregion

        #region DataGridPane activation
        // Update the datagrid (including its binding) to show the currently selected images whenever it is made visible. 
        public void DataGridPane_IsActiveChanged(object sender, EventArgs e)
        {
            // Because switching to the data grid generates a scroll event, we need to ignore it as it will 
            // otherwise turn off the data grid timer
            DataGridPane_IsActiveChanged(false);
        }

        // Format the datetime column using the database format rather than the default datagrid format for date time.
        private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.PropertyType == typeof(DateTime) && e.Column is DataGridTextColumn column)
            {
                column.Binding.StringFormat = Time.DateTimeDatabaseFormat;
            }
        }

        public void DataGridPane_IsActiveChanged(bool forceUpdate)
        {
            // Don't update anything if we don't have any files to display
            if (DataHandler == null || DataHandler.FileDatabase == null)
            {
                DataGrid.ItemsSource = null;
                return;
            }

            if (forceUpdate || DataGridPane.IsActive || DataGridPane.IsFloating || DataGridPane.IsVisible)
            {
                DataHandler.FileDatabase.BindToDataGrid(DataGrid, null);
            }
            DataGridSelectionsTimer_Reset();
        }
        #endregion

        #region Single vs Multiple Image View
        // Check to see if we are displaying at least one image in an active image set pane (not in the overview)
        private bool IsDisplayingActiveSingleImageOrVideo()
        {
            return IsDisplayingSingleImage() && ImageSetPane.IsActive;
        }

        // Check to see if we are displaying at least one image (not in the overview), regardless of whether the ImageSetPane is active
        private bool IsDisplayingSingleImage()
        {
            // Always false If we are in the overiew
            if (MarkableCanvas.IsThumbnailGridVisible) return false;

            // True only if we are displaying at least one file in an image set
            return IsFileDatabaseAvailable() &&
                   DataHandler.FileDatabase.CountAllCurrentlySelectedFiles > 0;
        }

        private bool IsDisplayingMultipleImagesInOverview()
        {
            return MarkableCanvas.IsThumbnailGridVisible && ImageSetPane.IsActive;
        }

        private void SwitchedToThumbnailGrid()
        {
            FilePlayer_Stop();
            FilePlayer.SwitchFileMode(false);
            MetadataUI.IsActive = false;

            // Refresh the CopyPreviousButton and its Previews as needed
            CopyPreviousValuesSetEnableStatePreviewsAndGlowsAsNeeded();

            // Hide the episode text
            EpisodeText.Visibility = Visibility.Hidden;
        }

        private void SwitchedToSingleImagesView()
        {
            FilePlayer.SwitchFileMode(true);
            DataGridSelectionsTimer_Reset();
            MetadataUI.IsActive = true;

            // Refresh the CopyPreviousButton and its Previews as needed
            CopyPreviousValuesSetEnableStatePreviewsAndGlowsAsNeeded();

            if (DataHandler != null)
            {
                DisplayEpisodeTextInImageIfWarranted(DataHandler.ImageCache.CurrentRow);
                this.MarkableCanvas.RefreshBoundingBoxesIfNeeded();
            }
        }

        // If the DoubleClick on the ThumbnailGrid selected an image or video, display it.
        private void ThumbnailGrid_DoubleClick(object sender, ThumbnailGridEventArgs e)
        {
            if (e.ImageRow != null && DataHandler.ImageCache.Current != null)
            {
                // Switch to either the video or image view as needed
                if (DataHandler.ImageCache.Current.IsVideo && DataHandler.ImageCache.Current.IsDisplayable(RootPathToImages))
                {
                    MarkableCanvas.SwitchToVideoView();
                }
                else
                {
                    MarkableCanvas.SwitchToImageView();
                }
                FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
                FileShow(DataHandler.FileDatabase.GetFileOrNextFileIndex(e.ImageRow.ID));
                FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);
            }
        }
        #endregion

        #region DataGrid events
        // When a user selects a row in the datagrid, show its corresponding image.
        // Note that because multiple selections are enabled, we show the image of the row that received the mouse-up
        private void DataGrid_RowSelected(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row)
            {
                FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
                if (row.Item is not DataRowView rowView)
                {
                    // Shouldn't happen
                    TracePrint.NullException(nameof(rowView));
                    return;
                }
                long fileID = (long)rowView.Row.ItemArray[0]!;
                FileShow(DataHandler.FileDatabase.GetFileOrNextFileIndex(fileID));
                FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);

                // The datagrid isn't floating: Switch from the dataGridPane view to the ImagesetPane view
                if (!DataGridPane.IsFloating)
                {
                    ImageSetPane.IsActive = true;
                }
            }
        }

        // This event handler is invoked whenever the user does a selection in the overview.
        // It is used to refresh (and match) what rows are selected in the DataGrid. 
        // However, because user selections can change rapidly (e.g., by dragging within the overview), we throttle the refresh using a timer 
        private void ThumbanilGrid_SelectionChanged(object sender, ThumbnailGridEventArgs e)
        {
            DataGridSelectionsTimer_Reset();
        }

        // If the DataGrid is visible, refresh it so its selected rows match the selections in the Overview. 
        private void DataGridSelectionsTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (DataHandler.FileDatabase.CountAllCurrentlySelectedFiles == 0)
                {
                    DataGrid.UpdateLayout();

                    DataGridSelectionsTimer.Stop();
                    return;
                }
                //this.DataGrid.UpdateLayout(); // Doesn't seem to be needed, but just in case...
                List<Tuple<long, int>> IdRowIndex = [];
                if (IsDisplayingSingleImage())
                {
                    // Only the current row is  selected in the single images view, so just use that.
                    int currentRowIndex = DataHandler.ImageCache.CurrentRow;
                    IdRowIndex.Add(new(DataHandler.FileDatabase.FileTable[currentRowIndex].ID, currentRowIndex));
                }
                else
                {
                    // multiple selections are possible in the 
                    int count = DataHandler.FileDatabase.FileTable.RowCount;
                    foreach (int rowIndex in MarkableCanvas.ThumbnailGrid.GetSelected())
                    {
                        if (rowIndex >= count)
                        {
                            // We are out of bounds!
                            return;
                        }
                        IdRowIndex.Add(new(DataHandler.FileDatabase.FileTable[rowIndex].ID, rowIndex));
                    }
                }
                if (DataGrid.Items.Count > 0)
                {
                    DataGrid.SelectAndScrollIntoView(IdRowIndex);
                }

                //this.DataGrid.UpdateLayout(); // Doesn't seem to be needed, but just in case...
                DataGridSelectionsTimer_Reset();
            }
            catch
            {
                // The above can fail, for example, if an image is deleted while this is triggered...
                // If this happens, its no big deal as we can just reset the timer and it will try again later when
                // things have (hopefully) caught up...
                DataGridSelectionsTimer_Reset();
            }
        }

        // Reset the timer, where we start it up again if the datagrid pane is active, floating or visible
        private void DataGridSelectionsTimer_Reset()
        {
            DataGridSelectionsTimer.Stop();
            if (DataGridPane.IsActive || DataGridPane.IsFloating)
            {
                DataGridSelectionsTimer.Start();
            }
        }

        // When we scroll the datagrid, we want to stop the timer updating the selection, 
        // as otherwise it would jump to the selection position
        private void DataGridScrollBar_Scroll(object sender, ScrollChangedEventArgs e)
        {
            // Stop the timer only if we are actually scrolling, i.e., if the scrolbar thumb has changed positions
            if (e.VerticalChange != 0)
            {
                DataGridSelectionsTimer.Stop();
            }
        }
        #endregion

        #region Help Document - Drag Drop
        private void HelpDocument_PreviewDrag(object sender, DragEventArgs dragEvent)
        {
            DragDropFile.OnTemplateFilePreviewDrag(dragEvent);
        }

        private async void HelpDocument_Drop(object sender, DragEventArgs dropEvent)
        {
            if (DragDropFile.IsTemplateFileDragging(dropEvent, out string templateDatabasePath))
            {
                if (IsFileDatabaseAvailable())
                {
                    // This method is currently a placeholder until we need to do some updating
                    if (false == Dialogs.CloseTemplateAndOpenNewTemplate(this, templateDatabasePath))
                    {
                        // The user aborted
                        return;
                    }
                    MenuFileCloseImageSet_Click(null, null);
                }
                // If its a valid template, load the images. Otherwise, just display the appropriate error dialog
                if (Dialogs.DialogIsFileValid(this, templateDatabasePath))
                {
                    if (false == await DoLoadImages(templateDatabasePath))
                    {
                        StatusBar.SetMessage("Aborted. Images were not added to the image set.");
                    }
                    Mouse.OverrideCursor = null;
                }

                dropEvent.Handled = true;
            }
        }
        #endregion

        #region Utilities
        // Returns whether there is an open file database
        public bool IsFileDatabaseAvailable()
        {
            if (DataHandler == null ||
                DataHandler.FileDatabase == null)
            {
                return false;
            }
            return true;
        }

        // Returns the currently active counter control, otherwise null
        private DataEntryCounter FindSelectedCounter()
        {
            foreach (DataEntryControl control in DataEntryControls.Controls)
            {
                if (control is DataEntryCounter { IsSelected: true } counter)
                {
                    return counter;
                }
            }
            return null;
        }

        #endregion

    }
}
