using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Speech.Synthesis;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.EventArguments;
using Timelapse.Images;
using Timelapse.QuickPaste;
using Timelapse.Util;
using ToastNotifications;
using ToastNotifications.Lifetime;
using ToastNotifications.Position;
using Xceed.Wpf.Toolkit;

namespace Timelapse
{
    /// <summary>
    /// main window for Timelapse
    /// </summary>
    public partial class TimelapseWindow
    {
        #region Public Properties 
        public DataEntryHandler DataHandler { get; set; }
        public TimelapseState State { get; set; }                       // Status information concerning the state of the UI

        public string FolderPath
        {
            get
            {
                if (this.DataHandler == null)
                {
                    System.Diagnostics.Debug.Print("Weird error in FolderPath - datahandler is null");
                    return String.Empty;
                }
                else
                {
                    return this.DataHandler.FileDatabase.FolderPath;
                }
            }
        }

        #endregion

        #region Private Variables
        private bool disposed;
        private List<MarkersForCounter> markersOnCurrentFile;   // Holds a list of all markers for each counter on the current file

        private readonly SpeechSynthesizer speechSynthesizer;                    // Enables speech feedback

        private TemplateDatabase templateDatabase;                      // The database that holds the template
        private IInputElement lastControlWithFocus;              // The last control (data, copyprevious button, or FileNavigatorSlider) that had the focus, so we can reset it

        private List<QuickPasteEntry> quickPasteEntries;              // 0 or more custum paste entries that can be created or edited by the user
        private QuickPasteWindow quickPasteWindow;

        private ImageAdjuster ImageAdjuster;    // The image adjuster controls

        // Timer for periodically updating images as the ImageNavigator slider is being used
        private readonly DispatcherTimer timerFileNavigator;

        // Timer used to AutoPlay images via MediaControl buttons
        private readonly DispatcherTimer FilePlayerTimer = new DispatcherTimer { };
        private readonly DispatcherTimer DataGridSelectionsTimer = new DispatcherTimer { };

        // Notifier: A toast that we can use anywher
        private Notifier ToastNotifier;
        // Record any command line arguments
        public DataStructures.Arguments Arguments { get; set; } = new DataStructures.Arguments(Environment.GetCommandLineArgs());
        #endregion

        #region Main
        public TimelapseWindow()
        {

            AppDomain.CurrentDomain.UnhandledException += this.OnUnhandledException;
            this.InitializeComponent();

            // Get the command line arguments, if any
            // this.Arguments = new DataStructures.Arguments(Environment.GetCommandLineArgs());

            // Register MarkableCanvas callbacks
            this.MarkableCanvas.PreviewMouseDown += new MouseButtonEventHandler(this.MarkableCanvas_PreviewMouseDown);
            this.MarkableCanvas.MouseEnter += new MouseEventHandler(this.MarkableCanvas_MouseEnter);
            this.MarkableCanvas.MarkerEvent += new EventHandler<MarkerEventArgs>(this.MarkableCanvas_RaiseMarkerEvent);
            this.MarkableCanvas.ThumbnailGrid.DoubleClick += this.ThumbnailGrid_DoubleClick;
            this.MarkableCanvas.ThumbnailGrid.SelectionChanged += this.ThumbanilGrid_SelectionChanged;
            this.MarkableCanvas.SwitchedToThumbnailGridViewEventAction += this.SwitchedToThumbnailGrid;
            this.MarkableCanvas.SwitchedToSingleImageViewEventAction += this.SwitchedToSingleImagesView;

            // Save/restore the focus whenever we leave / enter the control grid (which contains controls pluse the copy previous button, or the file navigator
            this.ControlGrid.MouseEnter += this.FocusRestoreOn_MouseEnter;
            this.ControlGrid.MouseLeave += this.FocusSaveOn_MouseLeave;

            // Set the window's title
            this.Title = Constant.Defaults.MainWindowBaseTitle;

            // Create the speech synthesiser
            this.speechSynthesizer = new SpeechSynthesizer();

            // Recall user's state from prior sessions
            this.State = new TimelapseState();
            this.State.ReadSettingsFromRegistry();
            Episodes.TimeThreshold = this.State.EpisodeTimeThreshold; // so we don't have to pass it as a parameter
            this.MarkableCanvas.SetBookmark(this.State.BookmarkScale, this.State.BookmarkTranslation);
            this.MenuItemAudioFeedback.IsChecked = this.State.AudioFeedback;




            // Populate the global references so we can access these from other objects without going thorugh the hassle of passing arguments around
            // Yup, poor practice but...
            GlobalReferences.MainWindow = this; // So other classes can access methods here
            GlobalReferences.BusyCancelIndicator = this.BusyCancelIndicator; // So other classes can access methods here
            GlobalReferences.CancelTokenSource = new CancellationTokenSource();     // Set the CancellationToken/Source. Only set globally
            GlobalReferences.TimelapseState = this.State;

            // Populate the most recent image set list
            this.RecentFileSets_Refresh();

            // Timer to force the image to update to the current slider position when the user pauses while dragging the  slider 
            this.timerFileNavigator = new DispatcherTimer()
            {
                Interval = this.State.Throttles.DesiredIntervalBetweenRenders
            };
            this.timerFileNavigator.Tick += this.TimerFileNavigator_Tick;

            // Callback to ensure Video AutoPlay stops when the user clicks on it
            this.FileNavigatorSlider.PreviewMouseDown += this.ContentControl_MouseDown;
            this.FileNavigatorSliderReset();

            // Timer activated / deactivated by Video Autoplay media control buttons
            this.FilePlayerTimer.Tick += this.FilePlayerTimer_Tick;

            this.DataGridSelectionsTimer.Tick += this.DataGridSelectionsTimer_Tick;
            this.DataGridSelectionsTimer.Interval = Constant.ThrottleValues.DataGridTimerInterval;

            // Get the window and its size from its previous location
            // SAULXX: Note that this is actually redundant, as if AvalonLayout_TryLoad succeeds it will do it again.
            // Maybe integrate this call with that?
            this.Top = this.State.TimelapseWindowPosition.Y;
            this.Left = this.State.TimelapseWindowPosition.X;
            this.Height = this.State.TimelapseWindowPosition.Height;
            this.Width = this.State.TimelapseWindowPosition.Width;

            // Mute the harmless 'System.Windows.Data Error: 4 : Cannot find source for binding with reference' (I think its from Avalon dock)
            System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Critical;
        }
        #endregion

        #region Window Loading, Closing
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Abort if some of the required dependencies are missing
            if (Dependencies.AreRequiredBinariesPresent(Constant.VersionUpdates.ApplicationName, Assembly.GetExecutingAssembly()) == false)
            {
                Dialogs.DependencyFilesMissingDialog(Constant.VersionUpdates.ApplicationName);
                Application.Current.Shutdown();
            }

            // Check for updates at least once a day
            if (DateTime.Now.Year != this.State.MostRecentCheckForUpdates.Year ||
            DateTime.Now.Month != this.State.MostRecentCheckForUpdates.Month ||
            DateTime.Now.Day != this.State.MostRecentCheckForUpdates.Day)
            {
                VersionChecks updater = new VersionChecks(this, Constant.VersionUpdates.ApplicationName, Constant.VersionUpdates.LatestVersionFileNameXML);
                updater.TryCheckForNewVersionAndDisplayResultsAsNeeded(false);
                this.State.MostRecentCheckForUpdates = DateTime.Now;
            }
            if (this.State.FirstTimeFileLoading)
            {
                // Load the previously saved layout. If there is none, TryLoad will default to a reasonable layout and window size/position.
                this.AvalonLayout_TryLoad(Constant.AvalonLayoutTags.LastUsed);
                this.State.FirstTimeFileLoading = false;
            }

            if (!SystemStatus.CheckAndGetLangaugeAndCulture(out _, out _, out string displayname))
            {
                this.HelpDocument.WarningRegionLanguage = displayname;
            }

            // By default, we now set everything to use the Invariant culture, although this 
            // isn't tested to ensure it works
            Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

            // Add a context menu to the data controls that allows a user to restore default values to the current or selected file
            // I originally had this in the XAML, but for some reason it complains if put there.
            ContextMenu menuRestoreDefaults = new ContextMenu();
            MenuItem menuItemRestoreDefaults = new MenuItem();

            menuItemRestoreDefaults.Click += MenuItemRestoreDefaultValues_Click;
            menuRestoreDefaults.Opened += this.MenuRestoreDefaults_Opened;
            menuRestoreDefaults.Items.Add(menuItemRestoreDefaults);
            this.DataEntryControls.ContextMenu = menuRestoreDefaults;
            this.DataEntryControlPanel.IsVisible = false;
            this.InstructionPane.IsActive = true;

            // If Timelapse was started with a -viewonly argument, set it up to initially be in viewonly mode
            if (this.Arguments.IsViewOnly)
            {
                this.State.IsViewOnly = true;
                // Disable the data entry panel, the copy previous values button, the data entry panels' context menu etc.
                // Individual controls are disabled in the DataEntryX panel
                this.DataEntryControls.ContextMenu = null;
                this.CopyPreviousValuesButton.Visibility = Visibility.Collapsed;
                this.EnableOrDisableMenusAndControls();
            }

            if (false == string.IsNullOrEmpty(this.Arguments.Template))
            {
                // If its not a valid template, display a dialog and abort
                if (false == Dialogs.DialogIsFileValid(this, this.Arguments.Template))
                {
                    return;
                }
                if (File.Exists(this.Arguments.Template))
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    this.StatusBar.SetMessage("Loading images, please wait...");
                    Tuple<bool,string> results = await this.TryOpenTemplateAndBeginLoadFoldersAsync(this.Arguments.Template).ConfigureAwait(true);
                    if (results.Item1 == false)
                    { 
                       // SAULXXX SHOULD BAIL HERE AS IT FAILED OPENING THE TEMPLATE AND/OR DATABASE
                    }
                    if (false == string.IsNullOrEmpty(this.Arguments.RelativePath))
                    {
                        // Set and only use the relative path as a search term
                        this.DataHandler.FileDatabase.CustomSelection.ClearCustomSearchUses();
                        this.DataHandler.FileDatabase.CustomSelection.SetAndUseRelativePathSearchTerm(this.Arguments.RelativePath);
                        if (null == this.DataHandler?.ImageCache?.Current)
                        {
                            await this.FilesSelectAndShowAsync(FileSelectionEnum.Folders).ConfigureAwait(true);  // Go to the first result (i.e., index 0) in the given selection set
                        }
                        else
                        {
                            await this.FilesSelectAndShowAsync(this.DataHandler.ImageCache.Current.ID, FileSelectionEnum.Folders).ConfigureAwait(true);  // Go to the first result (i.e., index 0) in the given selection set
                        }
                    }
                    this.StatusBar.SetMessage("Image set is now loaded.");
                    Mouse.OverrideCursor = null;

                    if (this.Arguments.ConstrainToRelativePath)
                    {
                        // Tell user that its a constrained relative path,
                        // Also, set the File menus so that users cannot close and reopen a new image set
                        // This is to avoid confusion as to how the user may mis-interpret the argument state given another image set
                        Dialogs.ArgumentRelativePathDialog(this, this.Arguments.RelativePath);
                        this.MenuItemExit.Header = "Close image set and exit Timelapse";
                        this.MenuFileCloseImageSet.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    // We can't open the template. Show a message and ignore the arguments (by clearing them)
                    Dialogs.ArgumentTemplatePathDialog(this, this.Arguments.Template, this.Arguments.RelativePath);
                    this.Arguments = new DataStructures.Arguments(null);
                }
            }

            if (this.State.IsViewOnly)
            {
                Dialog.Dialogs.OpeningMessageViewOnly(this);
            }

            if (this.State.SuppressWarningToUpdateDBFilesToSQLPrompt == false)
            {
                WarningToUpdateDBFilesToSQL warning = new WarningToUpdateDBFilesToSQL(this);
                bool? result = warning.ShowDialog();
                if (result.HasValue && result.Value && warning.CheckBoxDontShowAgain.IsChecked.HasValue)
                {
                    GlobalReferences.TimelapseState.SuppressWarningToUpdateDBFilesToSQLPrompt = warning.CheckBoxDontShowAgain.IsChecked == true;
                }
            }

            // Initialize the Toast notifier, where we set its position, width, etc.
            this.ToastNotifier = new Notifier(cfg =>
            {
                cfg.PositionProvider = new WindowPositionProvider(
                    parentWindow: Application.Current.MainWindow,
                    corner: Corner.BottomLeft,
                    offsetX: 3,
                    offsetY: 25);
                cfg.LifetimeSupervisor = new TimeAndCountBasedLifetimeSupervisor(
                    notificationLifetime: TimeSpan.FromSeconds(6),
                    maximumNotificationCount: MaximumNotificationCount.FromCount(5));
                cfg.DisplayOptions.Width = 300;
                cfg.Dispatcher = Application.Current.Dispatcher;

            });
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            this.FindBoxSetVisibility(false);
        }

        // On exiting, save various attributes so we can use recover them later
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            this.CloseTimelapseAndSaveState(true);
        }

        private void DeleteTheDeletedFilesFolderIfNeeded()
        {
            string deletedFolderPath = Path.Combine(this.DataHandler.FileDatabase.FolderPath, Constant.File.DeletedFilesFolder);
            int howManyDeletedFiles = Directory.Exists(deletedFolderPath) ? Directory.GetFiles(deletedFolderPath).Length : 0;

            // If there are no files, there is nothing to delete
            if (howManyDeletedFiles <= 0)
            {
                return;
            }

            // We either have auto deletion, or ask the user. Check both cases.
            // If its auto deletion, then set the flag to delete
            bool deleteTheDeletedFolder = this.State.DeleteFolderManagement == DeleteFolderManagementEnum.AutoDeleteOnExit;

            // if its ask the user, then set the flag according to the response
            if (this.State.DeleteFolderManagement == DeleteFolderManagementEnum.AskToDeleteOnExit)
            {
                Dialog.DeleteDeleteFolder deleteDeletedFolders = new Dialog.DeleteDeleteFolder(howManyDeletedFiles)
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
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                if (this.DataHandler != null)
                {
                    this.DataHandler.Dispose();
                }
                if (this.speechSynthesizer != null)
                {
                    this.speechSynthesizer.Dispose();
                }
            }
            this.disposed = true;
        }
        #endregion

        #region Exception Management
        // If we get an exception that wasn't handled, show a dialog asking the user to send the bug report to us.
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject.ToString().Contains("System.IO.PathTooLongException"))
            {
                Dialogs.FilePathTooLongDialog(this, e);
            }
            else
            {
                ExceptionShutdownDialog dialog = new ExceptionShutdownDialog(this, "Timelapse", e);
                dialog.ShowDialog();
                // force a shutdown. While some bugs could be recoverable, its dangerous to keep things running. 
                this.Close();
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
            foreach (KeyValuePair<string, DataEntryControl> pair in this.DataEntryControls.ControlsByDataLabel)
            {
                string controlType = this.DataHandler.FileDatabase.FileTableColumnsByDataLabel[pair.Key].ControlType;
                switch (controlType)
                {
                    case Constant.Control.Counter:
                        DataEntryCounter counter = (DataEntryCounter)pair.Value;
                        counter.ContentControl.PreviewMouseDown += this.ContentControl_MouseDown;
                        counter.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;
                        counter.Container.MouseEnter += this.CounterControl_MouseEnter;
                        counter.Container.MouseLeave += this.CounterControl_MouseLeave;
                        counter.LabelControl.Click += this.CounterControl_Click;
                        break;
                    case Constant.Control.Flag:
                    case Constant.DatabaseColumn.DeleteFlag:
                        DataEntryFlag flag = (DataEntryFlag)pair.Value;
                        flag.ContentControl.PreviewMouseDown += this.ContentControl_MouseDown;
                        flag.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;
                        break;
                    case Constant.Control.FixedChoice:
                        DataEntryChoice choice = (DataEntryChoice)pair.Value;
                        choice.ContentControl.PreviewMouseDown += this.ContentControl_MouseDown;
                        choice.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;
                        break;
                    case Constant.Control.Note:
                    case Constant.DatabaseColumn.File:
                    case Constant.DatabaseColumn.RelativePath:
                        DataEntryNote note = (DataEntryNote)pair.Value;
                        note.ContentControl.PreviewMouseDown += this.ContentControl_MouseDown;
                        note.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;
                        break;
                    case Constant.DatabaseColumn.DateTime:
                        DataEntryDateTime dateTime = (DataEntryDateTime)pair.Value;
                        dateTime.ContentControl.PreviewMouseDown += this.ContentControl_MouseDown;
                        dateTime.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;
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
            this.FilePlayer_Stop(); // In case the FilePlayer is going
        }

        /// <summary>
        /// This preview callback is used by all controls to reset the focus.
        /// Whenever the user hits enter over the control, set the focus back to the top-level
        /// </summary>
        /// <param name="sender">source of the event</param>
        /// <param name="eventArgs">event information</param>
        private void ContentCtl_PreviewKeyDown(object sender, KeyEventArgs eventArgs)
        {
            if (sender is IntegerUpDown counter)
            {
                // A hack to make the counter control work - see DateEntryCounter.cs
                if (counter.Value == int.MaxValue)
                {
                    counter.Value = null;
                }
            }

            if (eventArgs.Key == Key.Enter)
            {
                this.TrySetKeyboardFocusToMarkableCanvas(false, eventArgs);
                eventArgs.Handled = true;
                this.FilePlayer_Stop(); // In case the FilePlayer is going
            }
            // The 'empty else' means don't check to see if a textbox or control has the focus, as we want to reset the focus elsewhere
        }

        /// <summary>Preview callback for counters, to ensure that we only accept numbers</summary>
        /// <param name="sender">the event source</param>
        /// <param name="e">event information</param>
        private void CounterCtl_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsCondition.IsDigits(e.Text) && !String.IsNullOrWhiteSpace(e.Text);
            this.OnPreviewTextInput(e);
            this.FilePlayer_Stop(); // In case the FilePlayer is going
        }

        /// <summary>Click callback: When the user selects a counter, refresh the markers, which will also readjust the colors and emphasis</summary>
        /// <param name="sender">the event source</param>
        /// <param name="e">event information</param>
        private void CounterControl_Click(object sender, RoutedEventArgs e)
        {
            this.MarkableCanvas_UpdateMarkers();
            this.FilePlayer_Stop(); // In case the FilePlayer is going
        }

        /// <summary>Highlight the markers associated with a counter when the mouse enters it</summary>
        private void CounterControl_MouseEnter(object sender, MouseEventArgs e)
        {
            Panel panel = (Panel)sender;
            this.State.MouseOverCounter = ((DataEntryCounter)panel.Tag).DataLabel;
            this.MarkableCanvas_UpdateMarkers();
        }

        /// <summary>Remove marker highlighting associated with a counter when the mouse leaves it</summary>
        private void CounterControl_MouseLeave(object sender, MouseEventArgs e)
        {
            // Recolor the marks
            this.State.MouseOverCounter = null;
            this.MarkableCanvas_UpdateMarkers();
        }

        // When the Control Grid size changes, reposition the CopyPrevious Button depending on the width/height ratio
        private void ControlGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Height + 212 > e.NewSize.Width) // We include 250, as otherwise it will bounce around as repositioning the button changes the size
            {
                // Place the button at the bottom right of the grid
                Grid.SetRow(this.CopyPreviousValuesButton, 1);
                Grid.SetColumn(this.CopyPreviousValuesButton, 0);
                this.CopyPreviousValuesButton.HorizontalAlignment = HorizontalAlignment.Stretch;
                this.CopyPreviousValuesButton.VerticalAlignment = VerticalAlignment.Top;
                this.CopyPreviousValuesButton.Margin = new Thickness(0, 5, 0, 5);
            }
            else
            {
                // Place the button at the right of the grid
                Grid.SetRow(this.CopyPreviousValuesButton, 0);
                Grid.SetColumn(this.CopyPreviousValuesButton, 1);
                this.CopyPreviousValuesButton.HorizontalAlignment = HorizontalAlignment.Right;
                this.CopyPreviousValuesButton.VerticalAlignment = VerticalAlignment.Stretch;
                this.CopyPreviousValuesButton.Margin = new Thickness(5, 0, 5, 0);
            }
        }
        #endregion

        #region Recent File Sets
        /// <summary>
        /// Update the list of recent databases displayed under File -> Recent Databases.
        /// </summary>
        public void RecentFileSets_Refresh()
        {
            // remove image sets which are no longer present from the most recently used list
            // probably overkill to perform this check on every refresh rather than once at application launch, but it's not particularly expensive
            List<string> invalidPaths = new List<string>();
            foreach (string recentImageSetPath in this.State.MostRecentImageSets)
            {
                if (File.Exists(recentImageSetPath) == false)
                {
                    invalidPaths.Add(recentImageSetPath);
                }
            }

            foreach (string path in invalidPaths)
            {
                bool result = this.State.MostRecentImageSets.TryRemove(path);
                if (!result)
                {
                    TracePrint.PrintMessage(String.Format("Removal of image set '{0}' no longer present on disk unexpectedly failed.", path));
                }
            }

            // Enable the menu only when there are items in it and only if the load menu is also enabled (i.e., that we haven't loaded anything yet)
            this.MenuItemRecentImageSets.IsEnabled = this.State.MostRecentImageSets.Count > 0 && this.MenuItemLoadFiles.IsEnabled;
            this.MenuItemRecentImageSets.Items.Clear();

            // add menu items most recently used image sets
            int index = 1;
            foreach (string recentImageSetPath in this.State.MostRecentImageSets)
            {
                // Create a menu item for each path
                MenuItem recentImageSetItem = new MenuItem();
                recentImageSetItem.Click += this.MenuItemRecentImageSet_Click;
                recentImageSetItem.Header = String.Format("_{0} {1}", index++, recentImageSetPath);
                recentImageSetItem.ToolTip = recentImageSetPath;
                this.MenuItemRecentImageSets.Items.Add(recentImageSetItem);
            }
        }
        #endregion

        #region Folder Selection Dialogs
        // Open a dialog where the user selects one or more folders that contain the image set(s)
        private bool ShowFolderSelectionDialog(string rootFolderPath, out string selectedFolderPath)
        {
            using (CommonOpenFileDialog folderSelectionDialog = new CommonOpenFileDialog()
            {
                Title = "Select a folder ...",
                DefaultDirectory = rootFolderPath,
                IsFolderPicker = true,
                Multiselect = false
            })
            {
                folderSelectionDialog.InitialDirectory = folderSelectionDialog.DefaultDirectory;
                folderSelectionDialog.FolderChanging += this.FolderSelectionDialog_FolderChanging;
                if (folderSelectionDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    IEnumerable<string> folderPaths = folderSelectionDialog.FileNames;
                    selectedFolderPath = folderPaths.First();
                    return true;
                }
                selectedFolderPath = null;
                return false;
            }
        }
        /// <summary>
        /// File menu helper function.
        /// Note that it accesses this.FolderPath
        /// </summary>
        private void FolderSelectionDialog_FolderChanging(object sender, CommonFileDialogFolderChangeEventArgs e)
        {
            // require folders to be loaded be either the same folder as the .tdb and .ddb or subfolders of it
            if (e.Folder.StartsWith(this.FolderPath, StringComparison.OrdinalIgnoreCase) == false)
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
            this.DataGridPane_IsActiveChanged(false);
        }
        public void DataGridPane_IsActiveChanged(bool forceUpdate)
        {
            // Don't update anything if we don't have any files to display
            if (this.DataHandler == null || this.DataHandler.FileDatabase == null)
            {
                this.DataGrid.ItemsSource = null;
                return;
            }

            if (forceUpdate || this.DataGridPane.IsActive || this.DataGridPane.IsFloating || this.DataGridPane.IsVisible)
            {
                this.DataHandler.FileDatabase.BindToDataGrid(this.DataGrid, null);
            }
            this.DataGridSelectionsTimer_Reset();
        }
        #endregion

        #region Single vs Multiple Image View
        // Check to see if we are displaying at least one image in an active image set pane (not in the overview)
        private bool IsDisplayingActiveSingleImage()
        {
            return this.IsDisplayingSingleImage() && this.ImageSetPane.IsActive;
        }

        // Check to see if we are displaying at least one image (not in the overview), regardless of whether the ImageSetPane is active
        private bool IsDisplayingSingleImage()
        {
            // Always false If we are in the overiew
            if (this.MarkableCanvas.IsThumbnailGridVisible) return false;

            // True only if we are displaying at least one file in an image set
            return this.IsFileDatabaseAvailable() &&
                   this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles > 0;
        }

        private bool IsDisplayingMultipleImagesInOverview()
        {
            return this.MarkableCanvas.IsThumbnailGridVisible && this.ImageSetPane.IsActive;
        }

        private void SwitchedToThumbnailGrid()
        {
            this.FilePlayer_Stop();
            this.FilePlayer.SwitchFileMode(false);

            // Refresh the CopyPreviousButton and its Previews as needed
            this.CopyPreviousValuesSetEnableStatePreviewsAndGlowsAsNeeded();

            // Hide the episode text
            this.EpisodeText.Visibility = Visibility.Hidden;
        }

        private void SwitchedToSingleImagesView()
        {
            this.FilePlayer.SwitchFileMode(true);
            this.DataGridSelectionsTimer_Reset();

            // Refresh the CopyPreviousButton and its Previews as needed
            this.CopyPreviousValuesSetEnableStatePreviewsAndGlowsAsNeeded();

            if (this.DataHandler != null)
            {
                this.DisplayEpisodeTextInImageIfWarranted(this.DataHandler.ImageCache.CurrentRow);
            }
        }

        // If the DoubleClick on the ThumbnailGrid selected an image or video, display it.
        private void ThumbnailGrid_DoubleClick(object sender, ThumbnailGridEventArgs e)
        {
            if (e.ImageRow != null)
            {
                // Switch to either the video or image view as needed
                if (this.DataHandler.ImageCache.Current.IsVideo && this.DataHandler.ImageCache.Current.IsDisplayable(this.FolderPath))
                {
                    this.MarkableCanvas.SwitchToVideoView();
                }
                else
                {
                    this.MarkableCanvas.SwitchToImageView();
                }
                this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
                this.FileShow(this.DataHandler.FileDatabase.GetFileOrNextFileIndex(e.ImageRow.ID));
                this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);
            }
        }
        #endregion

        #region DataGrid events
        // When a user selects a row in the datagrid, show its corresponding image.
        // Note that because multiple selections are enabled, we show the image of the row that received the mouse-up
        private void DataGrid_RowSelected(object sender, MouseButtonEventArgs e)
        {
            if (sender != null)
            {
                if (sender is DataGridRow row)
                {
                    this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
                    DataRowView rowView = row.Item as DataRowView;
                    long fileID = (long)rowView.Row.ItemArray[0];
                    this.FileShow(this.DataHandler.FileDatabase.GetFileOrNextFileIndex(fileID));
                    this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);

                    // The datagrid isn't floating: Switch from the dataGridPane view to the ImagesetPane view
                    if (!this.DataGridPane.IsFloating)
                    {
                        this.ImageSetPane.IsActive = true;
                    }
                }
            }
        }

        // This event handler is invoked whenever the user does a selection in the overview.
        // It is used to refresh (and match) what rows are selected in the DataGrid. 
        // However, because user selections can change rapidly (e.g., by dragging within the overview), we throttle the refresh using a timer 
        private void ThumbanilGrid_SelectionChanged(object sender, ThumbnailGridEventArgs e)
        {
            this.DataGridSelectionsTimer_Reset();
        }

        // If the DataGrid is visible, refresh it so its selected rows match the selections in the Overview. 
        private void DataGridSelectionsTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles == 0)
                {
                    this.DataGrid.UpdateLayout();

                    this.DataGridSelectionsTimer.Stop();
                    return;
                }
                //this.DataGrid.UpdateLayout(); // Doesn't seem to be needed, but just in case...
                List<Tuple<long, int>> IdRowIndex = new List<Tuple<long, int>>();
                if (this.IsDisplayingSingleImage())
                {
                    // Only the current row is  selected in the single images view, so just use that.
                    int currentRowIndex = this.DataHandler.ImageCache.CurrentRow;
                    IdRowIndex.Add(new Tuple<long, int>(this.DataHandler.FileDatabase.FileTable[currentRowIndex].ID, currentRowIndex));
                }
                else
                {
                    // multiple selections are possible in the 
                    foreach (int rowIndex in this.MarkableCanvas.ThumbnailGrid.GetSelected())
                    {
                        IdRowIndex.Add(new Tuple<long, int>(this.DataHandler.FileDatabase.FileTable[rowIndex].ID, rowIndex));
                    }
                }
                if (this.DataGrid.Items.Count > 0)
                {
                    this.DataGrid.SelectAndScrollIntoView(IdRowIndex);
                }

                //this.DataGrid.UpdateLayout(); // Doesn't seem to be needed, but just in case...
                this.DataGridSelectionsTimer_Reset();
            }
            catch
            {
                // The above can fail, for example, if an image is deleted while this is triggered...
                // If this happens, its no big deal as we can just reset the timer and it will try again later when
                // things have (hopefully) caught up...
                this.DataGridSelectionsTimer_Reset();
            }
        }

        // Reset the timer, where we start it up again if the datagrid pane is active, floating or visible
        private void DataGridSelectionsTimer_Reset()
        {
            this.DataGridSelectionsTimer.Stop();
            if (this.DataGridPane.IsActive || this.DataGridPane.IsFloating)
            {
                this.DataGridSelectionsTimer.Start();
            }
        }

        // When we scroll the datagrid, we want to stop the timer updating the selection, 
        // as otherwise it would jump to the selection position
        private void DataGridScrollBar_Scroll(object sender, ScrollChangedEventArgs e)
        {
            // Stop the timer only if we are actually scrolling, i.e., if the scrolbar thumb has changed positions
            if (e.VerticalChange != 0)
            {
                this.DataGridSelectionsTimer.Stop();
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
                if (this.IsFileDatabaseAvailable())
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
                    if (false == await this.DoLoadImages(templateDatabasePath))
                    {
                        this.StatusBar.SetMessage("Aborted. Images were not added to the image set.");
                    }
                    Mouse.OverrideCursor = null;
                }
                dropEvent.Handled = true;
            }
        }
        #endregion

        #region Utilities
        // Returns whether there is an open file database
        private bool IsFileDatabaseAvailable()
        {
            if (this.DataHandler == null ||
                this.DataHandler.FileDatabase == null)
            {
                return false;
            }
            return true;
        }

        // Returns the currently active counter control, otherwise null
        private DataEntryCounter FindSelectedCounter()
        {
            foreach (DataEntryControl control in this.DataEntryControls.Controls)
            {
                if (control is DataEntryCounter counter)
                {
                    if (counter.IsSelected)
                    {
                        return counter;
                    }
                }
            }
            return null;
        }

        // Say the given text
        public void Speak(string text)
        {
            if (this.State.AudioFeedback)
            {
                this.speechSynthesizer.SpeakAsyncCancelAll();
                this.speechSynthesizer.SpeakAsync(text);
            }
        }
        #endregion

    }
}
