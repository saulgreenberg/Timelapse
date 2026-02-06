using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.EventArguments;
using Timelapse.Extensions;
using Timelapse.Recognition;
using Timelapse.SearchingAndSorting;
using TimelapseWpf.Toolkit;
using static System.FormattableString;
using Application = System.Windows.Application;
using Cursors = System.Windows.Input.Cursors;
using DataGrid = System.Windows.Controls.DataGrid;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Timelapse.Controls
{
    /// <summary>
    /// Control for displaying and selecting detections and classification recognitions. It:
    /// - display Detections and classification categories (if any) along with a count.
    /// - generates an event that is seen by its parent (CustomSelectionWithEpisodes) which in turn invokes the Refresh Counts event
    /// - contains auxiliary controls are included to let a user sort by either detection or classification confidence.
    /// The general way RefreshCounts works is that
    /// - the current RecognitionSelection parameters are copied saved
    /// - queries are done on by altering the original RecognitionSelection parameters to generate and display the categories and count for the given confidence boudns
    /// - the RecognitionSelection parameters are restored
    /// - actually selecting categories and classifications (and/or associated recognition controls) updates the current selection parameters 
    /// </summary>
    public partial class RecognitionSelector
    {
        #region Properties and Variables

        // Accessing external parameters
        private readonly FileDatabase Database;
        private readonly CustomSelection CustomSelection;
        private readonly RecognitionSelections RecognitionSelections;

        private static readonly int? NoValue = null;

        // Dictionaries that will eventually hold the Detection and Classification categories
        private Dictionary<string, string> DetectionCategories;
        private Dictionary<string, string> ClassificationCategories;
        private Dictionary<string, string> ClassificationDescriptions;

        // Reverse lookup dictionaries (category name -> category number) for O(1) lookups
        private Dictionary<string, string> DetectionCategoryNameToNumber;
        private Dictionary<string, string> ClassificationCategoryNameToNumber;

        // These collections are used to store the various Count results as they are updated.
        // They serve as ItemsSource to the corresponding DataGrid and ListBox controls 
        public ObservableCollection<CategoryCount> DetectionCountsCollection { get; set; } = [];
        public ObservableCollection<CategoryCount> ClassificationCountsCollection { get; set; } = [];

        // Original selection parameters so we can restore them as needed on exit
        private RecognitionSelections SavedRecognitionSelections;
        private bool ShowMissingDetections;
        private int RandomSample;
        private bool EpisodeShowAllIfAnyMatch;

        // State variables
        private bool classificationsExist;
        private bool ignoreSelection;
        private bool ignoreSliderUpdate;
        private bool sliderConfidenceInitialMovement;
        private bool onlyUpdateClassificationCount;
        // Concurrency guards for overlapping count operations and control unload.
        // - isCountingInProgress: true while RecognitionsRefreshCounts is executing
        // - isRecountPending: set when a new count is requested while one is already running;
        //   causes the in-progress count to abort early and restart with the latest settings
        // - isUnloaded: set when the control is unloaded; causes any in-progress count to abort
        //   and prevents restarts; temp table cleanup is deferred to the finally block
        private bool isCountingInProgress;
        private bool isRecountPending;
        private bool isUnloaded;
        private CategoryCount savedSelectedCategoryCount;


        // To hold passed in constructor arguments, used to set the busy state and to use the progress indicator
        private readonly BusyableDialogWindow Owner;
        private readonly BusyCancelIndicator BusyCancelIndicator;

        #endregion

        #region Constructor

        public RecognitionSelector(BusyableDialogWindow owner, BusyCancelIndicator busyCancelIndicator)
        {
            InitializeComponent();
            // So we can access the database and the various custom selection parameters
            this.Database = GlobalReferences.MainWindow.DataHandler?.FileDatabase;
            this.CustomSelection = Database?.CustomSelection;
            this.RecognitionSelections = Database?.CustomSelection?.RecognitionSelections;
            this.Owner = owner;
            this.BusyCancelIndicator = busyCancelIndicator;
            this.BusyCancelIndicator.Busy.DisplayAfter = TimeSpan.FromMilliseconds(3000);
        }

        #endregion

        #region OnLoaded / Unlaoded

        private void RecognitionsSelector_OnLoaded(object sender, RoutedEventArgs e)
        {
            DropSessionTempTables();

            // Set the look of the two Datagrids
            SetDataGridCategoryCountLook(this.DataGridDetections);
            SetDataGridCategoryCountLook(this.DataGridClassifications);

            // Abort if there is nothing to show
            if (this.Database == null)
            {
                this.EnableOrDisableAllControls(false, false);
                return;
            }

            // If we make it here, recognitions are available for this image set

            // Populate the detection categories
            this.Database.CreateDetectionCategoriesDictionaryIfNeeded();
            this.DetectionCategories = Database.detectionCategoriesDictionary;
            this.DetectionCategoryNameToNumber = this.DetectionCategories?.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
            if (null == this.DetectionCategories || this.DetectionCategories.Count == 0)
            {
                // Shouldn't happen: there are no detection categories! (likely a problem with the json file?)
                this.EnableOrDisableAllControls(false, false);
                return;
            }

            // Set the detection sliders, etc to the appropriate values
            this.SetDetectionControlsToInitialValues();

            // At this point we should at least have some detections.
            // So now we need to handle classifications, if any
            // Try to Populate the classification categories
            this.Database.CreateClassificationCategoriesDictionaryIfNeeded();
            this.ClassificationCategories = this.Database.classificationCategoriesDictionary;
            this.ClassificationCategoryNameToNumber = this.ClassificationCategories?.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
            this.ClassificationDescriptions = this.Database.classificationDescriptionsDictionary;
            if (this.ClassificationCategories == null || this.ClassificationCategories.Count == 0)
            {
                // No classifications for this image set
                this.classificationsExist = false;
                this.ClassificationControlsCollapse();
            }
            else
            {
                // Set the classification sliders to the appropriate values, initialize classifications, etc
                this.classificationsExist = true;
                this.SetClassificationControlsToInitialValues();
            }

            // Set Ranking to saved setting. Note that ranking is done by sorting on detection confidence
            this.CBRankRecognitions.IsChecked = this.RecognitionSelections.RankByDetectionConfidence || this.RecognitionSelections.RankByClassificationConfidence ;

            // Set event handlers for the rank by confidence checkboxes after setting their initial states, to avoid triggering events during initialization
            this.CBRankRecognitions.Checked += CBRankRecognitions_CheckChanged;
            this.CBRankRecognitions.Unchecked += CBRankRecognitions_CheckChanged;

            // Set the show missing detections checkbox to its initial value
            this.ShowMissingDetectionsCheckbox.IsChecked = this.CustomSelection.ShowMissingDetections;

            CBAutoCount.IsChecked = GlobalReferences.TimelapseState.AutoUpdateRecognitionCounts;
            CBAutoCount.Checked += CBAutoCount_CheckChanged;
            CBAutoCount.Unchecked += CBAutoCount_CheckChanged;

            //this method also sends a recognition selection event to the parent, which will trigger various updates
            CBRankRecognitions_CheckChanged(null, null);
        }

        private void RecognitionSelector_OnUnloaded(object sender, RoutedEventArgs e)
        {
            isUnloaded = true;
            if (false == isCountingInProgress)
            {
                // Safe to drop immediately — no background work is using the tables
                DropSessionTempTables();
            }
            // RecognitionsRefreshCounts will also try to drop them in its finally block
        }

        private void DropSessionTempTables()
        {
            string query = SqlForCounting.DoDropTemporaryTablesForSession();
            Database.Database.ExecuteNonQuery(query, 3000);
        }
        #endregion

        #region Counting

        // Refresh recognition counts. Handles overlapping invocations and control unload:
        // - If a count is already running, the new request is queued via isRecountPending.
        //   The in-progress count will abort early (at the next ShowProgressOrAbortIfCancelled checkpoint)
        //   and restart with the latest settings.
        // - If the control is unloaded during counting, the count aborts and temp tables
        //   are cleaned up in the finally block (since OnUnloaded deferred the cleanup).
        private async Task RecognitionsRefreshCounts()
        {
            if (CustomSelection.ShowMissingDetections) return;
            if (isCountingInProgress)
            {
                // A count is already running — flag it so the current run aborts and restarts
                isRecountPending = true;
                return;
            }

            isCountingInProgress = true;
            try
            {
                do
                {
                    isRecountPending = false;

                    // Counting can be long-running, so we want to make it a cancellable operation
                    this.BusyCancelIndicator.IsBusy = true;
                    this.BtnCountRecognitions.Content = "Counting...";
                    this.RecognitionSelectionsSaveState();
                    bool allCountsCompleted = await this.DoCountRecognitionsAsync(true, this.classificationsExist);
                    if (false == allCountsCompleted)
                    {
                        this.ClearCountsAndResetUI();
                    }
                    else
                    {
                        if (CustomSelection.EpisodeShowAllIfAnyMatch)
                        {
                            this.BtnCountRecognitions.IsEnabled = GlobalReferences.TimelapseState.AutoUpdateRecognitionCounts;
                        }
                    }

                    this.RecognitionSelectionsRestoreState();
                    this.TryHighlightCurrentSelection();
                    this.BusyCancelIndicator.IsBusy = false;
                    this.BtnCountRecognitions.Content = "Update counts";
                    this.onlyUpdateClassificationCount = false;

                    // Restart if a new count was requested during this run,
                    // but not if the control has been unloaded
                } while (isRecountPending && false == isUnloaded);
            }
            finally
            {
                isCountingInProgress = false;

                // If the control was unloaded while we were counting, OnUnloaded deferred
                // the temp table cleanup to us since the background thread was still using them
                if (isUnloaded)
                {
                    DropSessionTempTables();
                }
            }
        }

        private async Task AutocountRecognitions()
        {
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                await RecognitionsRefreshCounts();
            });
        }

        private async Task<bool> DoCountRecognitionsAsync(bool countDetections, bool countClassifications)
        {
            double lowerDetectionConf = Math.Round(this.SliderDetectionConf.LowerValue, 2);
            double higherDetectionConf = Math.Round(this.SliderDetectionConf.HigherValue, 2);
            double lowerClassificationConf = Math.Round(this.SliderClassificationConf.LowerValue, 2);
            double higherClassificationConf = Math.Round(this.SliderClassificationConf.HigherValue, 2);

            try
            {
                bool isCompletelyCompleted = await Task.Run(() =>
                {
                    bool allCountsCompleted;

                    if (this.RecognitionSelections.RankByDetectionConfidence || this.RecognitionSelections.RankByClassificationConfidence)
                    {
                        // These settings should couont all recognized files,
                        // i.e., ignore confidence thresholds, as the user is likely trying to see all recognitions sorted by confidence 
                        lowerDetectionConf = 0;
                        higherDetectionConf = 1;
                        lowerClassificationConf = 0;
                        higherClassificationConf = 1;
                    }

                    if (countDetections && (this.onlyUpdateClassificationCount == false || this.DetectionCountsCollectionHasCounts() == false))
                    {
                        string savedClassificationCategoryNumber = this.RecognitionSelections.ClassificationCategoryNumber;
                        this.RecognitionSelections.ClassificationCategoryNumber = string.Empty;
                        // We have two methods to count detections, each using a different Sql form.
                        // Which is used depends on whether 'Include all files in an episode' is selected
                        allCountsCompleted = CustomSelection.EpisodeShowAllIfAnyMatch
                            ? DoCountDetectionsIfEpisodesShowAll(lowerDetectionConf, higherDetectionConf)
                            : DoCountDetections(lowerDetectionConf, higherDetectionConf);

                        this.RecognitionSelections.ClassificationCategoryNumber = savedClassificationCategoryNumber;
                        if (!allCountsCompleted)
                        {
                            return false;
                        }
                        ReplaceBlankCountsWithZero(this.DetectionCountsCollection);
                    }

                    if (countClassifications)
                    {
                        // We have two methods to count classifications, each using a different Sql form.
                        // Which is used depends on whether 'Include all files in an episode' is selected
                        allCountsCompleted = CustomSelection.EpisodeShowAllIfAnyMatch
                            ? DoCountClassificationsIfEpisodesShowAll(lowerDetectionConf, higherDetectionConf, lowerClassificationConf, higherClassificationConf)
                            : DoCountClassifications(lowerDetectionConf, higherDetectionConf, lowerClassificationConf, higherClassificationConf);

                        if (!allCountsCompleted)
                        {
                            return false;
                        }
                        ReplaceBlankCountsWithZero(this.ClassificationCountsCollection);
                    }

                    return true;
                });

                return isCompletelyCompleted;
            }
            catch (TaskCanceledException)
            {
                // Only reset the token for user-initiated cancellations, not for recount-pending aborts or unload
                if (false == this.isRecountPending && false == this.isUnloaded)
                {
                    this.Owner.TokenReset();
                }
                return false;
            }
        }

        private bool DoCountDetections(double lowerConfidenceValue, double higherConfidenceValue)
        {
            // All category count
            // Check if cancelled, and/or show progress
            this.ShowProgressOrAbortIfCancelled(true, Constant.RecognizerValues.AllDetectionLabel);
            string where = CustomSelection.GetFilesWhere(true, false);
            if (this.RecognitionSelections.RankByDetectionConfidence)
            {
                lowerConfidenceValue = 0;
                higherConfidenceValue = 1;
            }
            string query = SqlForCounting.GetQueryDetectionCounts(DetectionCategories, where, lowerConfidenceValue, higherConfidenceValue);
            //System.Diagnostics.Debug.Print(query);
            // Stopwatch sw = new Stopwatch();
            //sw.Start();
            DataTable dataTable = Database.Database.GetDataTableFromSelect(query);
            //sw.Stop();
            //Debug.Print($"DetectionsNoEpisodes: {sw.ElapsedMilliseconds.ToString()}");

            // Parse the results and update the display
            if (dataTable.Rows.Count != 1)
            {
                return false;
            }
            DataRow row = dataTable.Rows[0];
            foreach (DataColumn col in dataTable.Columns)
            {
                this.SetDetectionCountForCategory(col.ColumnName,
                    Int32.TryParse(row[col].ToString(), out int countResult)
                        ? countResult
                        : 0);
            }
            return true;

        }

        private bool isSessionTmpTablesCreated;
        private bool isPerWhereTmpTablesCreated;
        private string lastWhereStatement;
        private double lastLowerDetectionConfidence = -1;
        private double lastUpperDetectionConfidence = -1;
        private bool DoCountDetectionsIfEpisodesShowAll(double lowerDetectionConfidence, double upperDetectionConfidence)
        {
            //long totalTime = 0;
            //Stopwatch sw = new Stopwatch();
            string episodeNoteField = CustomSelection.EpisodeNoteField;
            // All category count 
            // Check if cancelled, and/or show progress


            if (false == isSessionTmpTablesCreated)
            {
                // These re-usable temporary tables must be created once in the CustomSelection session before EpisodeAny counts
                // can be used. As they are expensive to create, we create them once and reuse them for all counts
                // They should not be reused between sessions, as the database may change.
                this.ShowProgressOrAbortIfCancelled(true, "preparations (initial database tables and indexes");

                string query1 = SqlForCounting.CreateTempTableAndIndexForEpisodePrefixCounts(episodeNoteField);
                //sw.Start();
                Database.Database.ExecuteNonQuery(query1);
                //sw.Stop();
                //totalTime += sw.ElapsedMilliseconds;
                // Debug.Print($"tempTable 1 created: {sw.ElapsedMilliseconds}");
                //Debug.Print($"tempTable 1 created");

                string query2 = SqlForCounting.CreateTempTableAndIndexForEpisodePrefixMap(episodeNoteField);
                //sw.Reset();
                //sw.Start();
                Database.Database.ExecuteNonQuery(query2);
                //sw.Stop();
                //totalTime += sw.ElapsedMilliseconds;
                //Debug.Print($"tempTable 2 created: {sw.ElapsedMilliseconds}");
                //Debug.Print($"tempTable 2 created");
                isSessionTmpTablesCreated = true;

            }

            string where = CustomSelection.GetFilesWhere(true, false);
            bool isThresholdChanged = Math.Abs(lowerDetectionConfidence - lastLowerDetectionConfidence) > .000001 ||
                                      Math.Abs(upperDetectionConfidence - lastUpperDetectionConfidence) > .000001;
            bool isWhereChanged = lastWhereStatement == null || lastWhereStatement != where;

            if (false == isPerWhereTmpTablesCreated || isWhereChanged)
            {
                // Rebuild the filtered image IDs temp table when the where statement changes
                this.ShowProgressOrAbortIfCancelled(true, ": examining conditions");
                string query3 = SqlForCounting.CreateTempTableAndIndexForFilteredImageIds(where);
                //sw.Reset();
                //sw.Start();
                Database.Database.ExecuteNonQuery(query3);
                //sw.Stop();
                //totalTime += sw.ElapsedMilliseconds;
                //Debug.Print($"tempTable 3 created: {sw.ElapsedMilliseconds}");
                //Debug.Print($"tempTable 3 created");
            }

            if (false == isPerWhereTmpTablesCreated || isWhereChanged || isThresholdChanged)
            {
                // Do query4 if only tmptables don't exist or if the thresholds or where have changed
                //   -- ============================================================================
                // -- These tmpTables should be rebuilt when the user's PER-WHERE + PER-THRESHOLD SETUP changes;
                // -- We save the last where statement for comparision and only rebuild if it changes, as building these tables is expensive
                // -- ============================================================================
                this.ShowProgressOrAbortIfCancelled(true, ": examining conditions");
                string query4 = SqlForCounting.CreateTempTableAndIndexForEpisodeDetectionFlags(lowerDetectionConfidence, upperDetectionConfidence);
                //sw.Reset();
                //sw.Start();
                Database.Database.ExecuteNonQuery(query4);
                //sw.Stop();
                //totalTime += sw.ElapsedMilliseconds;
                //Debug.Print($"tempTable 4 created: {sw.ElapsedMilliseconds}");
                //Debug.Print($"tempTable 4 created");
                isPerWhereTmpTablesCreated = true;
            }
            lastWhereStatement = where;
            lastLowerDetectionConfidence = lowerDetectionConfidence;
            lastUpperDetectionConfidence = upperDetectionConfidence;
            this.ShowProgressOrAbortIfCancelled(true, $"{Constant.RecognizerValues.EmptyDetectionLabel} and {Constant.RecognizerValues.AllDetectionLabel}");

            // Do the All and Empty count and update the display
            string query5 = SqlForCounting.GetQueryDetectionCountsWithEpisodesAnyForAllAndEmpty();
            //sw.Reset();
            //sw.Start();
            DataTable dataTableAllAndEmpty = Database.Database.GetDataTableFromSelect(query5);
            //sw.Stop();
            //totalTime += sw.ElapsedMilliseconds;
            //Debug.Print($"Detections All/Empty selected: {sw.ElapsedMilliseconds}");
            //int allFilesCount = Convert.ToInt32(dataTableAllAndEmpty.Rows[0]["CountAll"]);

            int allFilesCount = Int32.TryParse(dataTableAllAndEmpty.Rows[0]["CountAll"].ToString(), out int countAllResult)
                ? countAllResult
                : 0;
            this.SetDetectionCountForCategory(Constant.RecognizerValues.AllDetectionLabel, allFilesCount);

            // int emptyFilesCount = Convert.ToInt32(dataTableAllAndEmpty.Rows[0]["CountEmpty"]);
            int emptyFilesCount = Int32.TryParse(dataTableAllAndEmpty.Rows[0]["CountEmpty"].ToString(), out int countEmptyResult)
                ? countEmptyResult
                : 0;
            this.SetDetectionCountForCategory(Constant.RecognizerValues.EmptyDetectionLabel, emptyFilesCount);

            // Do other detection category counts and update the display
            this.ShowProgressOrAbortIfCancelled(true, "remaining detection categories");
            string query6 = SqlForCounting.GetQueryDetectionCountsWithEpisodesAnyForDetectionCategories(lowerDetectionConfidence, upperDetectionConfidence);
            //sw.Reset();
            //sw.Start();
            DataTable dataTableOtherDetectionCategories = Database.Database.GetDataTableFromSelect(query6);
            //sw.Stop();
            //totalTime += sw.ElapsedMilliseconds;
            //Debug.Print($"Detections OtherCategories selected: {sw.ElapsedMilliseconds}");
            foreach (DataRow row in dataTableOtherDetectionCategories.Rows)
            {
                string detCat = row["DetectionCategory"].ToString();
                if (detCat == null || Convert.ToInt32(detCat) == 0) continue; // Skip empty

                if (this.DetectionCategories.TryGetValue(detCat, out string detectionName))
                {
                    this.SetDetectionCountForCategory(detectionName, Convert.ToInt32(row["ImageCount"]));
                }
            }
            //Debug.Print($"Detections Total time: {totalTime}");
            return true;
        }

        private bool DoCountClassifications(
            double lowerDetectionConf, double higherDetectionConf,
            double lowerClassificationConf, double higherClassificationConf)
        {

            // Abort if there are no classifications
            if (this.ClassificationCategories == null || this.ClassificationCategories.Count == 0 || this.Owner.Token.IsCancellationRequested)
            {
                return false;
            }
            // Initialize by clearing the various lists
            this.ClearClassificationCounts();
            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            Database.Database.IndexCreateIfNotExists("IndexDetConfCls", "Detections", "Id, conf, classification_conf, classification");

            string where = CustomSelection.GetFilesWhere(true, false);
            string query = SqlForCounting.GetQueryClassificationCounts(where, lowerDetectionConf, higherDetectionConf, lowerClassificationConf, higherClassificationConf);
            DataTable dataTable = Database.Database.GetDataTableFromSelect(query);
            //sw.Stop();
            //Debug.Print($"NewClassifications: {sw.ElapsedMilliseconds.ToString()}");
            this.ShowProgressOrAbortIfCancelled(false, "Doing classifications");
            foreach (DataRow row in dataTable.Rows)
            {
                string value = row[0].ToString();
                if (value is not null && this.ClassificationCategories.TryGetValue(value, out string classificationName))
                {
                    this.SetClassificationCountForCategory(classificationName, Convert.ToInt32(row[1]));
                }
            }

            // Sort the datagrid by its count
            SortAndScrollClassificationGrid();
            return true;
        }

        private bool DoCountClassificationsIfEpisodesShowAll(double lowerDetectionConf, double higherDetectionConf, double lowerClassificationConf, double higherClassificationConf)
        {
            // Abort if there are no classifications
            if (this.ClassificationCategories == null || this.ClassificationCategories.Count == 0 || this.Owner.Token.IsCancellationRequested)
            {
                return false;
            }
            this.ShowProgressOrAbortIfCancelled(false, "classifications");
            // Initialize by clearing the various lists
            this.ClearClassificationCounts();

            // For each category, generate the count and update the appropriate lists
            string query7 = SqlForCounting.GetQueryClassificationCountsWithEpisodesAny(
                lowerDetectionConf, higherDetectionConf,
                lowerClassificationConf, higherClassificationConf);
            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            DataTable dataTable = Database.Database.GetDataTableFromSelect(query7);
            //sw.Stop();
            //Debug.Print($"ClassificationsEpisodesAny: {sw.ElapsedMilliseconds.ToString()}");
            this.ShowProgressOrAbortIfCancelled(false, "classification categories");
            foreach (DataRow row in dataTable.Rows)
            {
                string value = row["ClassificationCategory"].ToString();
                if (value is not null && this.ClassificationCategories.TryGetValue(value, out string classificationName))
                {
                    this.SetClassificationCountForCategory(classificationName, Convert.ToInt32(row["ImageCount"]));
                }
            }

            // Sort the datagrid by its count
            SortAndScrollClassificationGrid();

            return true;
        }


        #endregion

        #region Button Callbacks - BtnCountRecognitions - OnClick

        private async void BtnCountRecognitions_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await this.RecognitionsRefreshCounts();
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        #endregion

        #region Checkbox Callbacks - AutoCount, RankByConfidence, ShowMissingDetections

        private void CBAutoCount_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                GlobalReferences.TimelapseState.AutoUpdateRecognitionCounts = cb.IsChecked == true;
                if (cb.IsChecked == true)
                {
                    Task.Run(async () => await this.AutocountRecognitions());
                }
            }
        }

        // 
        private void CBRankRecognitions_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                bool rankingEnabled = cb.IsChecked == true;
                this.RecognitionSelections.RankByDetectionConfidence = rankingEnabled;

                // As turned off for now
                this.RecognitionSelections.RankByClassificationConfidence = false;
                
                // Disable sliders if ranking is enabled, as confidence thresholds don't apply when ranking by confidence, and to avoid confusion
                SlidersEnableState(rankingEnabled == false);

                if (rankingEnabled)
                {
                    // The Empty category will show Empty when a Ranking checkbox is checked
                    this.SetEmptyDetectionCategoryLabel();
                }

                // Send a recognition selection event to the parent
                this.SendRecognitionSelectionEvent(true);
            }
        }

        private void ShowMissingDetectionsCheckbox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            this.EnableOrDisableAllControls(ShowMissingDetectionsCheckbox.IsChecked == false, false, true);
            this.Database.CustomSelection.ShowMissingDetections = ShowMissingDetectionsCheckbox.IsChecked == true;

            // Send a recognition selection event to the parent
            this.SendRecognitionSelectionEvent(false);
        }

        // Unselect classifications in the datagrid and disable the rank-by-classification checkbox
        private void UnselectClassifications()
        {
            this.ignoreSelection = true;
            this.DataGridClassifications.SelectedItem = null;
            this.ignoreSelection = false;
        }
        #endregion

        #region Slider: Detection Confidence Callbacks

        // When the detection drag is in progress
        // - disable the detection controls
        // - display the updated slider value
        private bool isDetectionSliderMouseDown;
        private bool isDetectionValueChanged;

        // We only want to update counts after a slider action is completed, while at the same time display 
        // the current slider range. To make this work,
        // - we update the slider values whenever there is a change from the previous values.
        // - we only trigger counting on a mouse up event
        private void SliderDetectionConf_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.isDetectionSliderMouseDown = false;
            SliderDetectionConf_ValueChanged(this.SliderDetectionConf, null);
        }

        private void SliderDetectionConf_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.isDetectionSliderMouseDown = true;
        }

        private void SliderClassificationConf_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.IsRepeat)
            {
                e.Handled = true;
            }
        }

        private async void SliderDetectionConf_ValueChanged(object sender, RoutedEventArgs e)
        {
            // Abort if we can't or shouldn't do anything
            if (sender is not RangeSlider slider || null == this.DetectionCategories || ignoreSliderUpdate)
            {
                return;
            }

            // Abort if nothing has changed  in the current slider interaction, i.e., the value has not been changed previously
            // and there are no changes between the current slider values vs the current confidence values
            if (isDetectionValueChanged == false &&
                Math.Abs(Math.Round(slider.LowerValue, 2) - this.RecognitionSelections.DetectionConfidenceLowerForUI) < .01 &&
                Math.Abs(Math.Round(slider.HigherValue, 2) - this.RecognitionSelections.DetectionConfidenceHigherForUI) < .01)
            {
                return;
            }

            if (isDetectionSliderMouseDown)
            {
                // The user is likely in the midst of adjusting the slider values e.g., by dragging.
                // We only want to update the display as described below, but not actually do any counting
                // as that is an expensive operation.

                if (this.sliderConfidenceInitialMovement == false)
                {
                    // We only need to do this the first time things are being moved

                    // Clearing the current selection and recognition
                    this.sliderConfidenceInitialMovement = true;

                    // As the user is scrolling, indicate this by clearing the current counts (ie., to NoValue)
                    this.ClearCountsAndResetUI();

                    // Disable the detection datagrid 
                    this.DetectionDataGridEnableState(false, true);
                    this.ClassificationDataGridEnableState(false, true);
                    this.isDetectionValueChanged = true;
                }

                // Show the current slider values 
                this.DisplayDetectionConfidenceRange();
                this.onlyUpdateClassificationCount = false;
                return;
            }

            // The user has finished updating the sliders, so we want to both update the display 
            // and counts
            this.sliderConfidenceInitialMovement = false;
            this.onlyUpdateClassificationCount = false;

            // Enable the detection datagrid 
            this.DetectionDataGridEnableState(true, true);
            this.ClassificationDataGridEnableState(true, true);



            // Set and display the new confidence thresholds
            double lowerConf = Math.Round(slider.LowerValue, 2);
            double higherConf = Math.Round(slider.HigherValue, 2);
            this.RecognitionSelections.DetectionConfidenceLowerForUI = lowerConf;
            this.RecognitionSelections.DetectionConfidenceHigherForUI = higherConf;
            this.DisplayDetectionConfidenceRange();
            this.SetEmptyDetectionCategoryLabel();

            // Clear the current counts 
            this.ClearCountsAndResetUI();
            this.isDetectionValueChanged = false;

            // The CountRecognitions button is enabled so that the user can recount recogntions
            this.BtnCountRecognitions.IsEnabled = true;

            // Send a recognition selection event to the parent
            this.SendRecognitionSelectionEvent(true);
        }

        #endregion

        #region Slider: Classification Confidence Callbacks

        // Classification slider -
        // When the classification drag is in progress
        // - disable the classification controls
        // - display the updated slider value
        private bool isClassificationSliderMouseDown;

        private bool isClassificationValueChanged;

        // We only want to update counts after a slider action is completed, while at the same time display 
        // the current slider range. To make this work,
        // - we update the slider values whenever there is a change from the previous values.
        // - we only trigger counting on a mouse up event
        private void SliderClassificationConf_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.isClassificationSliderMouseDown = false;
            this.SliderClassificationConf_ValueChanged(this.SliderClassificationConf, null);
        }

        private void SliderClassificationConf_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.isClassificationSliderMouseDown = true;
            this.savedSelectedCategoryCount = (CategoryCount)DataGridClassifications?.SelectedItem;
        }

        private async void SliderClassificationConf_ValueChanged(object sender, RoutedEventArgs e)
        {
            // Abort if we can't or shouldn't do anything
            if (sender is not RangeSlider slider || null == this.DetectionCategories || ignoreSliderUpdate)
            {
                return;
            }

            // Abort if nothing has changed  in the current slider interaction, i.e., the value has not been changed previously
            // and there are no changes between the current slider values vs the current confidence values
            if (isClassificationValueChanged == false &&
                Math.Abs(Math.Round(slider.LowerValue, 2) - this.RecognitionSelections.ClassificationConfidenceLowerForUI) < .01 &&
                Math.Abs(Math.Round(slider.HigherValue, 2) - this.RecognitionSelections.ClassificationConfidenceHigherForUI) < .01)
            {
                return;
            }

            if (isClassificationSliderMouseDown)
            {
                // The user is likely in the midst of adjusting the slider values e.g., by dragging.
                // We only want to update the display as described below, but not actually do any counting
                // as that is an expensive operation.

                // As the user is in the midst of scrolling, provide feedback by
                // disabling the detection datagrid and clearing the current classification selection and recognition
                this.ClassificationDataGridEnableState(false, true);

                // Show the current slider values 
                this.DisplayClassificationConfidenceRange();

                // Clear the current counts
                this.onlyUpdateClassificationCount = true;
                this.ClearClassificationCounts();
                this.isClassificationValueChanged = true;
                return;
            }

            // The user has finished updating the sliders, so we want to both update the display and counts

            // Enable the classification datagrid 
            this.ClassificationDataGridEnableState(true, true);

            // The CountRecogntions button is enabled so that the user can recount recogntions


            // Set and display the new confidence thresholds
            double lowerConf = Math.Round(slider.LowerValue, 2);
            double higherConf = Math.Round(slider.HigherValue, 2);
            this.RecognitionSelections.ClassificationConfidenceLowerForUI = lowerConf;
            this.RecognitionSelections.ClassificationConfidenceHigherForUI = higherConf;
            this.DisplayClassificationConfidenceRange();
            this.DataGridClassifications.SelectedItem = this.savedSelectedCategoryCount;
            this.TryHighlightCurrentSelection();
            this.isClassificationValueChanged = false;
            this.onlyUpdateClassificationCount = true;

            this.BtnCountRecognitions.IsEnabled = true;

            // Send a recognition selection event to the parent
            this.SendRecognitionSelectionEvent(true);
        }

        #endregion

        #region DataGrid Callbacks - OnSelectionChanged

        private void DataGridDetections_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.ignoreSelection)
            {
                return;
            }

            this.RecognitionSelections.ClassificationCategoryNumber = string.Empty;
            if (sender is DataGrid { SelectedItems: [CategoryCount categoryCount] })
            {

                // Alter the RecognitionSelection parameters so that the parent can redo the count on it
                // All special case: By convention, All is mapped to the category string in AllCategoryNumber (NoValue)
                if (categoryCount.Category == Constant.RecognizerValues.AllDetectionLabel)
                {
                    // Set to the currently selected item
                    this.RecognitionSelections.DetectionCategoryNumber = Constant.RecognizerValues.AllDetectionCategoryNumber;
                    this.RecognitionSelections.AllDetections = true;
                    this.RecognitionSelections.InterpretAllDetectionsAsEmpty = false;

                    // Update the datagrid

                    this.UnselectClassifications();

                    this.SendRecognitionSelectionEvent(false);
                    return;
                }

                // The user selected a category (which could include empty)
                string selectedCategory = categoryCount.Category.StartsWith(Constant.RecognizerValues.EmptyDetectionLabel)
                    ? Constant.RecognizerValues.EmptyDetectionLabel
                    : categoryCount.Category;

                // Change the recognitionSelection attributes to match Empty
                if (selectedCategory == Constant.RecognizerValues.EmptyDetectionLabel)
                {
                    this.RecognitionSelections.AllDetections = true;
                    this.RecognitionSelections.InterpretAllDetectionsAsEmpty = true;
                }
                else
                {
                    this.RecognitionSelections.AllDetections = false;
                    this.RecognitionSelections.InterpretAllDetectionsAsEmpty = false;
                }

                // Get the category number from its name
                if (this.DetectionCategoryNameToNumber.TryGetValue(selectedCategory, out string categoryNumber))
                {
                    // Set it to the selected category
                    this.RecognitionSelections.DetectionCategoryNumber = categoryNumber;
                    if (selectedCategory != Constant.RecognizerValues.AllDetectionLabel)
                    {
                        // Unselect classifications when the Detection category is not All
                        this.ignoreSelection = true;
                        this.DataGridClassifications.SelectedItem = null;
                        //this.EnableDisableRankByClassificationCheckbox(false);
                        this.ignoreSelection = false;
                    }

                    // Send event, but don't redo the count on recognitions
                    this.SendRecognitionSelectionEvent(false);
                }
            }
        }

        // The classification selection has changed
        private void DataGridClassifications_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.ignoreSelection)
            {
                return;
            }

            if (sender is DataGrid { SelectedItems: [CategoryCount categoryCount] })
            {
                // The user selected a category
                if (this.ClassificationCategoryNameToNumber.TryGetValue(categoryCount.Category, out string categoryNumber))
                {
                    // Set the Classification Category to the selected entity
                    this.RecognitionSelections.ClassificationCategoryNumber = categoryNumber;
                    // this.EnableDisableRankByClassificationCheckbox(true);

                    // Because we are selecting a classification, we should ensure that the Detections Category is set to All
                    this.RecognitionSelections.AllDetections = true;
                    this.RecognitionSelections.InterpretAllDetectionsAsEmpty = false;
                    this.RecognitionSelections.DetectionCategoryNumber = Constant.RecognizerValues.AllDetectionCategoryNumber;

                    CategoryCount allCategoryCount = this.DetectionCountsCollection.FirstOrDefault(i => i.Category.StartsWith(Constant.RecognizerValues.AllDetectionLabel));
                    if (allCategoryCount != null)
                    {
                        // Set the selected item to All
                        // We then invoke the DataGridDetections_OnSelectionChanged to set the detection appropriately,
                        // and to trigger SendRecognitionSelectionEvent();
                        this.ignoreSelection = true;
                        this.DataGridDetections.SelectedItem = allCategoryCount;
                        this.ignoreSelection = false;
                    }

                    this.SendRecognitionSelectionEvent(false);
                }
            }
        }

        #endregion

        #region Highlight selection

        // Try to highlight the currently selected item, if possible
        // Used only by OnLoaded to show the initial selections.
        private void TryHighlightCurrentSelection()
        {
            if (string.IsNullOrEmpty(this.RecognitionSelections.DetectionCategoryNumber) && string.IsNullOrEmpty(this.RecognitionSelections.ClassificationCategoryNumber))
            {
                // Nothing was selected
                this.ignoreSelection = true;
                this.DataGridDetections.SelectedItem = null;
                this.DataGridClassifications.SelectedItem = null;
                this.ignoreSelection = false;
                return;
            }

            if (false == string.IsNullOrEmpty(this.RecognitionSelections.ClassificationCategoryNumber))
            {
                // if we have a category, this ensures that the detection is set to All
                this.RecognitionSelections.DetectionCategoryNumber = Constant.RecognizerValues.AllDetectionCategoryNumber;
            }

            if (false == string.IsNullOrEmpty(this.RecognitionSelections.DetectionCategoryNumber))
            {
                // We have a selected detection. Get its name 
                string selectedDetectionCategoryName;
                if (this.RecognitionSelections.DetectionCategoryNumber == Constant.RecognizerValues.AllDetectionCategoryNumber)
                {
                    selectedDetectionCategoryName = Constant.RecognizerValues.AllDetectionLabel;
                }
                else if (this.RecognitionSelections.DetectionCategoryNumber == Constant.RecognizerValues.EmptyDetectionCategoryNumber)
                {
                    selectedDetectionCategoryName = Constant.RecognizerValues.EmptyDetectionLabel;
                }
                else
                {
                    this.DetectionCategories.TryGetValue(this.RecognitionSelections.DetectionCategoryNumber, out selectedDetectionCategoryName);
                }

                if (selectedDetectionCategoryName == null)
                {
                    // Special case to interpret all and empty categories 
                    if (this.RecognitionSelections.AllDetections && this.RecognitionSelections.InterpretAllDetectionsAsEmpty)
                    {
                        selectedDetectionCategoryName = Constant.RecognizerValues.EmptyDetectionLabel;
                    }
                    else if (this.RecognitionSelections.AllDetections && false == this.RecognitionSelections.InterpretAllDetectionsAsEmpty)
                    {
                        selectedDetectionCategoryName = Constant.RecognizerValues.AllDetectionLabel;
                    }
                    else
                    {
                        // Something went wrong
                        return;
                    }
                }

                // Get the category count item matching the category name
                CategoryCount detectionCategoryCount = this.DetectionCountsCollection.FirstOrDefault(x => x.Category == selectedDetectionCategoryName);
                if (detectionCategoryCount == null)
                {
                    detectionCategoryCount = this.DetectionCountsCollection.FirstOrDefault(x => x.Category.StartsWith(Constant.RecognizerValues.EmptyDetectionLabel));
                    if (detectionCategoryCount == null)
                    {
                        // Something went wrong
                        return;
                    }
                }

                // Finally, select that item in the data grid, making sure its visible and highlit
                this.ignoreSelection = true;
                this.DataGridDetections.SelectedItem = detectionCategoryCount;
                if (string.IsNullOrEmpty(this.RecognitionSelections.ClassificationCategoryNumber))
                {
                    this.DataGridClassifications.SelectedItem = null;
                }

                this.ignoreSelection = false;
                this.DataGridDetections.ScrollIntoView(detectionCategoryCount);
                this.DataGridDetections.Focus();
            }

            // If we have a classification selected, highlight it
            if (null != this.ClassificationCategories && false == string.IsNullOrEmpty(this.RecognitionSelections.ClassificationCategoryNumber))
            {
                // Classification
                this.ClassificationCategories.TryGetValue(this.RecognitionSelections.ClassificationCategoryNumber, out string selectedClassificationCategoryName);
                if (selectedClassificationCategoryName == null)
                {
                    return;
                }

                CategoryCount classificationCategoryCount = this.ClassificationCountsCollection.FirstOrDefault(x => x.Category == selectedClassificationCategoryName);
                if (classificationCategoryCount == null)
                {
                    // Something went wrong
                    return;
                }

                // Finally, select that item in the data grid, making sure its visible and highlit
                this.ignoreSelection = true;
                this.DataGridClassifications.SelectedItem = classificationCategoryCount;
                this.ignoreSelection = false;
                this.DataGridClassifications.ScrollIntoView(classificationCategoryCount);
                this.DataGridClassifications.Focus();
            }
            else
            {
                this.DataGridClassifications.SelectedItem = null;
                this.DataGridClassifications.ScrollIntoViewFirstRow();
            }
        }

        #endregion

        #region Custom Selection Event

        public event EventHandler<RecognitionSelectionChangedEventArgs> RecognitionSelectionEvent;

        private void SendRecognitionSelectionEvent(bool refreshRecognitionCountsRequired)
        {
            string detectionCategoryLabel = string.Empty;
            string classificationCategoryLabel = string.Empty;

            // Get the current Detection selection, if any
            if (this.DataGridDetections.SelectedItems is [CategoryCount categoryCountDetections])
            {
                detectionCategoryLabel = categoryCountDetections.Category.StartsWith(Constant.RecognizerValues.EmptyDetectionLabel)
                    ? Constant.RecognizerValues.EmptyDetectionLabel
                    : categoryCountDetections.Category;
            }

            // Get the current Classification selection, if any
            if (DataGridClassifications.SelectedItems is [CategoryCount categoryCountClassifications])
            {
                classificationCategoryLabel = categoryCountClassifications.Category;
            }

            // Compose the argument and send the event
            RecognitionSelectionChangedEventArgs e = new(detectionCategoryLabel, classificationCategoryLabel, refreshRecognitionCountsRequired,
                // ShowMissingDetectionsCheckbox.IsChecked == true
                this.CustomSelection.ShowMissingDetections
                || this.RecognitionSelections.RankByClassificationConfidence
                || this.RecognitionSelections.RankByDetectionConfidence);
            RecognitionSelectionEvent?.Invoke(this, e);
        }

        #endregion

        #region Public methods, invoked by Parent

        public void UpdateDisplayOfTotalFileCounts(string count)
        {
            this.MatchingFilesCountLabel.Text = (count == "1")
                ? " file matches your query"
                : " files match your query";
            this.MatchingFilesCount.Text = count;
        }

        #endregion

        #region Save/Restore RecognitionSelections Parameters

        // Save the original selection parameters.
        // As we will alter some of these, we will restore them later back to its original values
        private void RecognitionSelectionsSaveState()
        {
            this.SavedRecognitionSelections = new()
            {
                UseRecognition = this.RecognitionSelections.UseRecognition,
                DetectionCategoryNumber = RecognitionSelections.DetectionCategoryNumber,
                ClassificationCategoryNumber = RecognitionSelections.ClassificationCategoryNumber,
                AllDetections = RecognitionSelections.AllDetections,
                InterpretAllDetectionsAsEmpty = RecognitionSelections.InterpretAllDetectionsAsEmpty,
                DetectionConfidenceLowerForUI = RecognitionSelections.DetectionConfidenceLowerForUI,
                DetectionConfidenceHigherForUI = RecognitionSelections.DetectionConfidenceHigherForUI,
                ClassificationConfidenceLowerForUI = RecognitionSelections.ClassificationConfidenceLowerForUI,
                ClassificationConfidenceHigherForUI = RecognitionSelections.ClassificationConfidenceHigherForUI,
                RankByDetectionConfidence = RecognitionSelections.RankByDetectionConfidence,
            };

            this.ShowMissingDetections = CustomSelection.ShowMissingDetections;
            this.RandomSample = CustomSelection.RandomSample;
            this.EpisodeShowAllIfAnyMatch = CustomSelection.EpisodeShowAllIfAnyMatch;
        }

        private void RecognitionSelectionsRestoreState()
        {
            if (null != RecognitionSelections)
            {
                // Restore original selection parameters as we may have alter some of these
                this.RecognitionSelections.UseRecognition = this.SavedRecognitionSelections.UseRecognition;
                this.RecognitionSelections.DetectionCategoryNumber = this.SavedRecognitionSelections.DetectionCategoryNumber;
                this.RecognitionSelections.AllDetections = this.SavedRecognitionSelections.AllDetections;
                this.RecognitionSelections.InterpretAllDetectionsAsEmpty = this.SavedRecognitionSelections.InterpretAllDetectionsAsEmpty;
                this.RecognitionSelections.DetectionConfidenceLowerForUI = this.SavedRecognitionSelections.DetectionConfidenceLowerForUI;
                this.RecognitionSelections.DetectionConfidenceHigherForUI = this.SavedRecognitionSelections.DetectionConfidenceHigherForUI;
                this.RecognitionSelections.ClassificationConfidenceLowerForUI = this.SavedRecognitionSelections.ClassificationConfidenceLowerForUI;
                this.RecognitionSelections.ClassificationConfidenceHigherForUI = this.SavedRecognitionSelections.ClassificationConfidenceHigherForUI;
                this.RecognitionSelections.ClassificationCategoryNumber = this.SavedRecognitionSelections.ClassificationCategoryNumber;
                this.RecognitionSelections.RankByDetectionConfidence = this.SavedRecognitionSelections.RankByDetectionConfidence;
            }

            if (null == CustomSelection)
            {
                return;
            }

            this.CustomSelection.ShowMissingDetections = this.ShowMissingDetections;
            this.CustomSelection.RandomSample = this.RandomSample;
            this.CustomSelection.EpisodeShowAllIfAnyMatch = this.EpisodeShowAllIfAnyMatch;
        }

        #endregion

        #region Set detection and classification controls (sliders, labels, counts) to initial values

        // Detection-related initialization to saved (original) parameter values
        // - only invoked in OnLoaded
        private void SetDetectionControlsToInitialValues()
        {
            // Set the current Confidence range in the detection sliders
            this.ignoreSliderUpdate = true;
            this.SliderDetectionConf.LowerValue = Math.Round(this.RecognitionSelections.DetectionConfidenceLowerForUI, 2);
            this.SliderDetectionConf.HigherValue = Math.Round(this.RecognitionSelections.DetectionConfidenceHigherForUI, 2);
            this.ignoreSliderUpdate = false;

            // Display the current Confidence range in the detections title
            this.DisplayDetectionConfidenceRange();

            // Clear the counts for each detection category held in the DetectionCounts
            // This will also create an entry for each detection category as they don't already exist.
            this.ClearDetectionCounts();
        }

        // Classification-related initialization to saved (original) parameter values
        // - only invoked in OnLoaded
        private void SetClassificationControlsToInitialValues()
        {
            if (false == this.classificationsExist)
            {
                return;
            }

            // Set the current Confidence range in the classification sliders
            this.ignoreSliderUpdate = true;
            this.SliderClassificationConf.LowerValue = Math.Round(this.RecognitionSelections.ClassificationConfidenceLowerForUI, 2);
            this.SliderClassificationConf.HigherValue = Math.Round(this.RecognitionSelections.ClassificationConfidenceHigherForUI, 2);
            this.ignoreSliderUpdate = false;

            // Display the current Confidence range in the classifications title
            this.DisplayClassificationConfidenceRange();

            // Clear the counts for each classification category held in the DetectionCounts
            // This will also create an entry for each classification category as they don't already exist.
            // Initialize classifications, where each has an empty count
            this.ClearAllClassifications();
        }

        #endregion

        #region Classification tooltip

        // Show the classification tooltip (but only if a corresponding description exists) when the mouse enters a row
        private void DataGridClassificationsRow_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is not DataGridRow row || this.ClassificationDescriptions.Count == 0) return;
            if (row.Item is not CategoryCount cc || string.IsNullOrWhiteSpace(cc.Category)) return;

            if (false == this.ClassificationCategoryNameToNumber.TryGetValue(cc.Category, out string categoryNumber)) return;
            if (false == this.ClassificationDescriptions.TryGetValue(categoryNumber, out string description) || string.IsNullOrWhiteSpace(description)) return;

            try
            {
                // The description is expected to be in the form of "GUID;term;term;term;term;term;commonName", where term can be empty (but ';' still present)
                string descriptionWithoutGuid = description[(description.IndexOf(';') + 1)..];
                string descriptionWithoutCommonName = descriptionWithoutGuid.Remove(descriptionWithoutGuid.LastIndexOf(';')).TrimEnd(';');

                // Ignore empty tooltips
                row.ToolTip = string.IsNullOrEmpty(descriptionWithoutCommonName)
                    ? cc.Category
                    : descriptionWithoutCommonName;
            }
            catch
            {
                // The description doesn't conform to the expected format
            }
        }

        #endregion

        #region Enable/Disable controls

        // Disable all the recognition controls, usually because there is nothing to show
        // This is likely redundant, as the recognitions selector should NOT be created if there is nothing to show.
        private void EnableOrDisableAllControls(bool enableAllControls, bool updateCursorToMatchState, bool enableShowMissingDetectionsCheckbox = false)
        {
            // Enable/disable the detection datagrid and detection slider
            this.DetectionDataGridEnableState(enableAllControls, updateCursorToMatchState);
            this.SliderDetectionConf.IsEnabled = enableAllControls;

            // Enable/disable the classification controls
            this.ClassificationDataGridEnableState(enableAllControls, updateCursorToMatchState);

            // Enable/disable confidence sliders
            this.SlidersEnableState(enableAllControls);

            // Enable/disable the buttons and checkbox 
            this.BtnCountRecognitions.IsEnabled = enableAllControls;
            this.CBRankRecognitions.IsEnabled = enableAllControls;
            this.CBAutoCount.IsEnabled = enableAllControls;

            this.ShowMissingDetectionsCheckbox.IsEnabled = enableShowMissingDetectionsCheckbox || enableAllControls;

            // Adjust all label colors i.e., disabled is gray, enabled is black
            Brush labelColor = enableAllControls ? Brushes.Black : Brushes.DarkGray;
            this.TBDetectionsLabel.Foreground = labelColor;
            this.TBClassificationsLabel.Foreground = labelColor;
            //this.CBRankByDetectionConfidence.Foreground = labelColor;
            //this.CBRankByClassificationConfidence.Foreground = labelColor;
            if (false == enableShowMissingDetectionsCheckbox)
            {
                this.ShowMissingDetectionsCheckbox.Foreground = labelColor;
            }

        }

        private void DataGridEnableState(DataGrid dataGrid, bool enableState, bool mouseState, bool updateCursorToMatchState)
        {
            if (updateCursorToMatchState)
            {
                Mouse.OverrideCursor = mouseState ? null : Cursors.Wait;
            }

            dataGrid.IsEnabled = enableState;
        }

        private void DetectionDataGridEnableState(bool enableState, bool updateCursorToMatchState)
        {
            DataGridEnableState(this.DataGridDetections, enableState, enableState, updateCursorToMatchState);
        }

        private void ClassificationDataGridEnableState(bool enableState, bool updateCursorToMatchState)
        {
            DataGridEnableState(this.DataGridClassifications, enableState && this.ShowMissingDetectionsCheckbox.IsChecked == false, enableState, updateCursorToMatchState);
        }


        // Collapse the classification controls (because no classifications are present)
        // - only be invoked via the OnLoaded event
        private void ClassificationControlsCollapse()
        {
            this.GridClassifications.Visibility = Visibility.Collapsed;
            this.ClassificationColumnWidth.Width = new(0);
        }

        private void SlidersEnableState(bool enableState)
        {
            // Show/Hide the detection and classification slider
            this.SliderDetectionConf.IsEnabled = enableState;
            this.TBDetectionsCount.Foreground = enableState ? Brushes.Black : Brushes.Azure;
            this.SliderDetectionConf.RangeBackground = enableState ? SystemColors.HighlightBrush : Brushes.DarkGray;

            this.SliderClassificationConf.IsEnabled = enableState;
            this.TBClassificationsCount.Foreground = enableState ? Brushes.Black : Brushes.Azure;
            this.SliderClassificationConf.RangeBackground = enableState ? Brushes.SaddleBrown : Brushes.DarkGray;
        }

        #endregion

        #region Datagrid: Set its look or clear its selections, or sort it by count

        // Set the look of  Datagrid holding the CategoryCount values
        private static void SetDataGridCategoryCountLook(DataGrid dataGrid)
        {
            if (dataGrid.Columns.Count <= 1)
            {
                return;
            }

            // Count column: Fill the remaining space, if any
            dataGrid.Columns[0].Width = new(1, DataGridLengthUnitType.Auto);
            dataGrid.Columns[0].CanUserSort = true;

            // Category column: Try to size to just fit the widest category content
            dataGrid.Columns[1].Width = new(1, DataGridLengthUnitType.Star);
            dataGrid.Columns[1].CanUserSort = true;
        }

        // Sort the given data grid by the indicated column (Count is 0, Category name is 1) in the appropriate order
        public static void SortDataGrid(DataGrid dataGrid, int columnIndex = 0, ListSortDirection sortDirection = ListSortDirection.Ascending)
        {
            var column = dataGrid.Columns[columnIndex];

            // Clear current sort descriptions
            dataGrid.Items.SortDescriptions.Clear();

            // Add the new sort description
            dataGrid.Items.SortDescriptions.Add(new(column.SortMemberPath, sortDirection));

            // Apply sort
            foreach (var col in dataGrid.Columns)
            {
                col.SortDirection = null;
            }

            column.SortDirection = sortDirection;

            // Refresh items to display sort
            dataGrid.Items.Refresh();
        }

        private void SortAndScrollClassificationGrid()
        {
            if (this.DataGridClassifications.Columns.Count > 0)
            {
                Application.Current.Dispatcher.Invoke(delegate
                {
                    SortDataGrid(this.DataGridClassifications, 0, ListSortDirection.Descending);
                    this.DataGridClassifications.ScrollIntoViewFirstRow();
                });
            }
        }

        #endregion

        #region Display text feedback for Detection/Classification confidence range.

        // Note that we use Invariant to ensure that the decimal separator is always '.' regardless of culture
        private void DisplayDetectionConfidenceRange()
        {
            this.TBDetectionsCount.Text = Invariant($"({Math.Round(this.SliderDetectionConf.LowerValue, 2):f2} - {Math.Round(this.SliderDetectionConf.HigherValue, 2):f2})");
        }

        private void DisplayClassificationConfidenceRange()
        {
            this.TBClassificationsCount.Text =
                Invariant($"({Math.Round(this.SliderClassificationConf.LowerValue, 2):f2} - {Math.Round(this.SliderClassificationConf.HigherValue, 2):f2})");
        }

        #endregion

        #region Clear the counts

        public void ClearCountsAndResetUI(bool doCountIfNeeded = false)
        {
            if (this.DetectionCategories == null)
            {
                // We need to do this in case we haven't completely created the RecognitionsSelector
                return;
            }

            // Clear the counts
            if (this.onlyUpdateClassificationCount == false)
            {
                this.ClearDetectionCounts();
            }

            this.ClearAllClassifications();

            // Highlight the current selection, if any
            this.TryHighlightCurrentSelection();

            // The button is enabled so that the user can count recogntions

            this.BtnCountRecognitions.IsEnabled = true;

            if (GlobalReferences.TimelapseState.AutoUpdateRecognitionCounts && doCountIfNeeded)
            {
                //Debug.Print("======== autoCounting");
                Task.Run(async () => await this.AutocountRecognitions());
            }
        }

        // Clear the counts for each detection category held in the DetectionCounts
        // This will also create and entry for each deteccton category if it doesn't already exist.
        private void ClearDetectionCounts()
        {
            // Clear the All and Empty categories 
            this.SetDetectionCountForCategory(Constant.RecognizerValues.AllDetectionLabel, NoValue);
            this.SetDetectionCountForCategory(Constant.RecognizerValues.EmptyDetectionLabel, NoValue);

            // Detections: clear the count in Category 
            foreach (KeyValuePair<string, string> kvp in DetectionCategories)
            {
                if (kvp.Key == "0") continue; // Skip empty
                this.SetDetectionCountForCategory(kvp.Value, NoValue);
            }
        }

        // Clear all counts associated with classifications.
        // This will also:
        // - clear the the EmptyClassifications and disable it
        // - sort the datagrid by its classifications (as the counts will all be the same)
        private void ClearAllClassifications()
        {
            // Abort if there are no classifications
            if (this.ClassificationCategories?.Count == 0)
            {
                return;
            }

            // Initialize by clearing the various lists
            // Note that the first method will also initialize the collection if needed
            this.ClearClassificationCounts();

            // Enable the controls  as needed, and sort the classifications by the classifications column
            Application.Current.Dispatcher.Invoke(delegate
            {
                this.ClassificationDataGridEnableState(true, true);
                if (this.DataGridClassifications.Columns.Count > 1)
                {
                    // Defaults to ListSortDirection.Ascending
                    SortDataGrid(this.DataGridClassifications, 1);
                }
            });
        }

        // Clear the counts for each classification category held in theclassificationCounts
        // This will also create and entry for each classification category if it doesn't already exist.
        private void ClearClassificationCounts()
        {
            if (ClassificationCategories == null)
            {
                return;
            }

            // Classification: clear the count in Category
            foreach (KeyValuePair<string, string> kvp in ClassificationCategories)
            {
                this.SetClassificationCountForCategory(kvp.Value, NoValue);
            }
        }

        #endregion

        #region Set / Get individual category detection or recognition counts and category labels

        // Given a category and a count, add or update it in the DetectionCountsCollection
        private void SetDetectionCountForCategory(string category, int? count)
        {
            SetCountForCategory(this.DetectionCountsCollection, category, count,
                matchPredicate: i => i.Category.StartsWith(category),
                transformCategory: cat =>
                {
                    if (cat.StartsWith(Constant.RecognizerValues.EmptyDetectionLabel))
                    {
                        double lowerValue = Math.Round(this.SliderDetectionConf.LowerValue, 2);
                        return lowerValue == 0 && RecognitionSelections.AllDetections && RecognitionSelections.InterpretAllDetectionsAsEmpty
                            ? $"{Constant.RecognizerValues.EmptyDetectionLabel}"
                            : Invariant($"{Constant.RecognizerValues.EmptyDetectionLabel} (excludes detections {Constant.SearchTermOperator.GreaterThanOrEqual} {lowerValue})");
                    }
                    return cat;
                });
        }

        private void SetClassificationCountForCategory(string category, int? count)
        {
            SetCountForCategory(this.ClassificationCountsCollection, category, count,
                matchPredicate: i => i.Category == category);
        }

        // Given a category and a count, add or update it in the specified collection
        private static void SetCountForCategory(ObservableCollection<CategoryCount> collection, string category, int? count,
            Func<CategoryCount, bool> matchPredicate, Func<string, string> transformCategory = null)
        {
            var categoryCount = collection.FirstOrDefault(matchPredicate);
            if (categoryCount == null)
            {
                Application.Current.Dispatcher.Invoke(delegate
                {
                    string finalCategory = transformCategory != null ? transformCategory(category) : category;
                    CategoryCount cc = new(finalCategory, count);
                    collection.Add(cc);
                    cc.NotifyPropertyChanged("Count");
                });
            }
            else
            {
                categoryCount.Count = count;
                categoryCount.NotifyPropertyChanged("Count");
            }
        }

        private static void ReplaceBlankCountsWithZero(ObservableCollection<CategoryCount> collection)
        {
            if (collection is null) return;
            foreach (CategoryCount cc in collection)
            {
                if (cc.Count == null)
                {
                    cc.Count = 0;
                    cc.NotifyPropertyChanged("Count");
                }
            }
        }

        private void SetEmptyDetectionCategoryLabel()
        {
            var categoryCount = this.DetectionCountsCollection.FirstOrDefault(i => i.Category.StartsWith(Constant.RecognizerValues.EmptyDetectionLabel));
            if (null == categoryCount)
            {
                // Hmmm. We should always have an Empty category
                return;
            }

            // Modify the Empty label in the DetectionsCountCollection.
            // Uses Invariant to ensure that the decimal separator is always '.' regardless of culture
            Application.Current.Dispatcher.Invoke(delegate
            {
                double lowerValue = Math.Round(this.SliderDetectionConf.LowerValue, 2);
                categoryCount.Category = lowerValue == 0 || (RecognitionSelections.RankByDetectionConfidence || RecognitionSelections.RankByClassificationConfidence)
                    ? $"{Constant.RecognizerValues.EmptyDetectionLabel}"
                    : Invariant($"{Constant.RecognizerValues.EmptyDetectionLabel} (excludes detections {Constant.SearchTermOperator.GreaterThanOrEqual} {lowerValue})");

                categoryCount.NotifyPropertyChanged("Category");
            });
        }

        #endregion

        #region Show progress during counting, or Abort operation if cancelled

        // Called periodically during counting to update progress and check for abort conditions:
        // - User clicked cancel (Owner.Token)
        // - A newer count was requested (isRecountPending)
        // - The control was unloaded (isUnloaded)
        private void ShowProgressOrAbortIfCancelled(bool countingDetections, string entity)
        {
            if (this.Owner.Token.IsCancellationRequested || this.isRecountPending || this.isUnloaded)
            {
                string reason = this.Owner.Token.IsCancellationRequested
                    ? "cancelled by user"
                    : this.isRecountPending
                        ? "a new count is pending"
                        : "Custom Select dialog was closed";
                TracePrint.PrintMessageOnly($"Counting recognitions task cancelled as {reason}");
                return;
                //throw new TaskCanceledException();
            }

            string what = countingDetections ? "detections" : "classifications";
            this.Owner.Progress.Report(new(0, $"Counting {what} ({entity}). Please wait", true, true));
            Thread.Sleep(ThrottleValues.RenderingBackoffTime); // Allows the UI thread to update every now and then
        }

        #endregion

        #region Helpers

        // Check if there are any missing counts in the collection
        private bool DetectionCountsCollectionHasCounts()
        {
            return this.DetectionCountsCollection.All(cc => cc.Count != null);
        }

        #endregion

        #region Class: CategoryCount defines an element containing a detection category and its current count

        public class CategoryCount(string category, int? count) : INotifyPropertyChanged
        {
            public int? Count { get; set; } = count;
            public string Category { get; set; } = category;

            public event PropertyChangedEventHandler PropertyChanged;

            public void NotifyPropertyChanged(string propName)
            {
                this.PropertyChanged?.Invoke(this, new(propName));
            }
        }

        #endregion

        #region Class: SqlForCounting 
        // contains the SQL query strings used for counting detections and classifications, as well as for pre-computing episode prefix counts and maps to optimize episode-based expansion
        // These queries are somewhat complex. I used ClaudeCode to help write, optimize, and test them. I had previously created much simpler versions, but they ran significantly slower.
        private static class SqlForCounting
        {
            #region
            // static strings used by the SQL queries below

            // temporary table and index names for the episode-based expansion optimization,
            // which are created and dropped in the Session setup and teardown queries, respectively
            private static readonly string tmpTableEpisodePrefixCounts = "tmpTableEpisodePrefixCounts";
            private static readonly string tmpIndexEpisodePrefixCounts = "tmpIndexEpisodePrefixCounts";
            private static readonly string tmpTableImageEpisodePrefixMap = "tmpTableImageEpisodePrefixMap";
            private static readonly string tmpIndexImageEpisodePrefixMap = "tmpIndexImageEpisodePrefixMap";
            private static readonly string tmpTableFilteredImageIds = "tmpTableFilteredImageIds";
            private static readonly string tmpIndexFilteredImageIds = "tmpIndexFilteredImageIds";
            private static readonly string tmpTableEpisodeDetectionFlags = "tmpTableEpisodeDetectionFlags";
            private static readonly string tmpIndexEpisodeDetectionFlags_Prefix = "tmpIndexEpisodeDetectionFlags_Prefix";
            private static readonly string lf = Environment.NewLine;

            // Database column names used in the queries below
            private static readonly string detConf = $"{DBTables.Detections}.{DetectionColumns.Conf}";
            private static readonly string detId = $"{DBTables.Detections}.{DetectionColumns.ImageID}";
            private static readonly string dtId = $"{DBTables.FileData}.{DatabaseColumn.ID}";
            const string detClassification = $"{DBTables.Detections}.{DetectionColumns.Classification}";
            const string detClassificationConf = $"{DBTables.Detections}.{DetectionColumns.ClassificationConf}";

            // Sql fragments
            private static readonly string maxCaseWhenDetectionsDot = $"{Sql.Max} ( {Sql.Case} {Sql.When} {DBTables.Detections}.";
            private static readonly string thenOneElse0EndAs = $"{Sql.Then} 1 {Sql.Else} 0 {Sql.End} ) {Sql.As}";

            // Database terms - set here for convenience and consistency
            private static readonly string per_image = "per_image";
            private static readonly string has_any = "has_any";
            private static readonly string ImageId = "ImageId";
            private static readonly string max_conf = "max_conf";
            private static readonly string ImageCount = "ImageCount";
            private static readonly string EpisodePrefix = "EpisodePrefix";
            private static readonly string HasHighConfDetection = "HasHighConfDetection";
            private static readonly string HasLowConfOnly = "HasLowConfOnly";
            private static readonly string PerImageFlags = "PerImageFlags";
            #endregion

            #region Sql statements for Detection counts without episode-based expansion
            //  -- ============================================================================
            //  -- QUERY: Detection counts without episode-based expansion
            //  --    Returns the count of matching images that contain at least one filtered image with matching detections.
            //  --    This is used when EpisodeAny is not selected, so we don't need to do any episode-based expansion.
            //  -- ============================================================================
            public static string GetQueryDetectionCounts(Dictionary<string, string> detectionCategories, string where, double lowerConfidenceValue, double higherConfidenceValue)
            {
                // WITH per_image AS (
                //    SELECT
                //      Detections.Id,
                //      MAX(Detections.conf) AS max_conf,
                //      MAX(CASE WHEN Detections.conf BETWEEN 0.2 AND 1
                //          THEN 1 ELSE 0 END) AS has_any,
                //      MAX(CASE WHEN Detections.category = 1 AND Detections.conf BETWEEN 0.2 AND 1
                //          THEN 1 ELSE 0 END) AS 'has_cat1',
                //      MAX(CASE WHEN Detections.category = 2 AND Detections.conf BETWEEN 0.2 AND 1
                //          THEN 1 ELSE 0 END) AS 'has_cat2',
                //      MAX(CASE WHEN Detections.category = 3 AND Detections.conf BETWEEN 0.2 AND 1
                //          THEN 1 ELSE 0 END) AS 'has_cat3'
                //    FROM Detections
                //    INNER JOIN DataTable ON DataTable.Id = Detections.Id

                //    WHERE DataTable.Wildlife = 'false' COLLATE NOCASE
                //    GROUP BY Detections.Id
                //  )
                //  SELECT
                //    SUM(has_any)   AS All,
                //    SUM(has_cat1)  AS cat1,
                //    SUM(has_cat2)  AS cat2,
                //    SUM(has_cat3)  AS cat3,
                //    SUM(CASE WHEN max_conf < 0.19990000000000002
                //        THEN 1 ELSE 0 END) AS Empty
                //  FROM per_image

                string betweenConf = $"{Sql.Between} {lowerConfidenceValue} {Sql.And} {higherConfidenceValue}";
                string count_all = Sql.Quote("All");
                string count_empty = Sql.Quote("Empty");
                const string has_prefix = "has_";

                string query = $"{Sql.With} {per_image} {Sql.As} ( {lf}"
                             + $"  {Sql.Select} {lf}"
                             + $"    {detId} {Sql.Comma} {lf}"
                             + $"    {Sql.Max} ( {detConf} ) {Sql.As} {max_conf} {Sql.Comma} {lf}"
                             + $"    {maxCaseWhenDetectionsDot}{DetectionColumns.Conf} {betweenConf} {lf}"
                             + $"       {thenOneElse0EndAs} {has_any} {Sql.Comma} {lf}";

                int categoryCount = detectionCategories.Count;
                int categoriesProcessed = 0;
                foreach (KeyValuePair<string, string> kvp in detectionCategories)
                {
                    categoriesProcessed++;
                    if (kvp.Key == "0") continue; // Skip empty
                    string comma = categoriesProcessed == categoryCount ? string.Empty : Sql.Comma; // omits the last comma
                    query += $"    {maxCaseWhenDetectionsDot}{DetectionColumns.Category} {Sql.Equal} {kvp.Key} {Sql.And} {DetectionColumns.Conf} {betweenConf} {lf}"
                           + $"       {thenOneElse0EndAs} \"{has_prefix + kvp.Value}\" {comma} {lf}";
                }
                query += $"  {Sql.From} {DBTables.Detections} {lf}"
                       + $"  {Sql.InnerJoin} {DBTables.FileData} {Sql.On} {dtId} = {detId} {lf}"
                       + $"  {where} {lf}"
                       + $"  {Sql.GroupBy} {detId} {lf}"
                       + $") {lf}"
                       + $"  {Sql.Select} {lf}"
                       + $"    {Sql.Sum} ( {has_any} ) {Sql.As} {count_all} {Sql.Comma} {lf}";
                foreach (KeyValuePair<string, string> kvp in detectionCategories)
                {
                    if (kvp.Key == "0") continue; // Skip empty
                    query += $"    {Sql.Sum} ( \"{has_prefix + kvp.Value}\" ) {Sql.As} \"{kvp.Value}\" {Sql.Comma} {lf}";
                }

                query += $"    {Sql.Sum} ( {Sql.CaseWhen} {max_conf} {Sql.LessThan} {Math.Min(lowerConfidenceValue, higherConfidenceValue) - .00001} {lf}"
                       + $"       {thenOneElse0EndAs} {count_empty}"
                       + $"  {Sql.From} {per_image}";
                return query;
            }
            #endregion

            #region Sql statements for Classification counts without episode-based expansion
            //  -- ============================================================================
            //  -- QUERY: Classification counts without episode-based expansion
            //  --    Returns the counts of matching images that contain at least one filtered image with matching classifications.
            //  --    This is used when EpisodeAny is not selected, so we don't need to do any episode-based expansion.
            //  -- ============================================================================
            public static string GetQueryClassificationCounts(string where, double lowerDetectionConf, double higherDetectionConf, double lowerClassificationConf, double higherClassificationConf)
            {
                //   SELECT  
                //    Detections.classification , 
                //     Count  (  DISTINCT  DataTable.Id )  AS  file_count 
                //   FROM  Detections 
                //      INNER JOIN  DataTable  ON  DataTable.Id  =  Detections.Id 
                //      WHERE (DataTable.img_wildlife='false' COLLATE NOCASE ) 
                //      AND      Detections.conf  BETWEEN  0.2  AND  1
                //      AND  Detections.classification_conf  BETWEEN  0.5  AND  1
                //   GROUP BY  Detections.classificaton

                string query = $"{Sql.Select} {lf}"
                               + $"    {detClassification} , {lf}"
                               + $"    {Sql.Count} ( {Sql.Distinct} {dtId} ) {Sql.As} file_count {lf}"
                               + $"  {Sql.From} {DBTables.Detections} {lf}"
                               + $"     {Sql.InnerJoin} {DBTables.FileData} {Sql.On} {dtId} {Sql.Equal} {detId} {lf}";
                query += (false == string.IsNullOrWhiteSpace(where))
                               ? $"     {where} {lf}     {Sql.And}"
                               : $"     {Sql.Where} ";
                query += $"     {detConf} {Sql.Between} {lowerDetectionConf} {Sql.And} {higherDetectionConf}{lf}"
                               + $"     {Sql.And} {detClassificationConf} {Sql.Between} {lowerClassificationConf} {Sql.And} {higherClassificationConf}{lf}"
                               + $"  {Sql.GroupBy} {detClassification}";
                return query;
            }
            #endregion

            #region Sql statements for Session setup
            //  -- ============================================================================
            //  -- SESSION SETUP (run once when the Custom selection session begins or when EpisodeAny checkbox is first selected;
            //     does not depend on threshold or WHERE clause
            //  -- ============================================================================      

            // Pre-compute episode prefix ep_prefix -> image count (eliminates all future DataTable re-scans)
            //  -- 1. Episode prefix image counts
            //  --    Pre-computes how many images belong to each episode prefix.
            //  --    This eliminates the expensive full-table rescan that the original queries
            //  --    use to "expand" a matching episode prefix into a COUNT(*) of its images.
            // tmpTableName: tmpTableEpisodePrefixCounts
            // tmpIndex:     tmpIndexEpisodePrefixCounts
            // episodeColumn: depends on which column contains the episode data
            internal static string CreateTempTableAndIndexForEpisodePrefixCounts(string episodeColumn)
            {
                return CreateTempTableAndIndexForEpisodePrefixCounts(tmpTableEpisodePrefixCounts, tmpIndexEpisodePrefixCounts, episodeColumn);
            }

            private static string CreateTempTableAndIndexForEpisodePrefixCounts(string tmpTableName, string tmpIndex, string episodeColumn)
            {
                //  CREATE TEMP TABLE tmpTableEpisodePrefixCounts AS
                //      SELECT
                //          SUBSTR(Episode, 1, INSTR(Episode, ':') - 1) AS EpisodePrefix,
                //          COUNT(*) AS ImageCount
                //      FROM DataTable
                //      GROUP BY SUBSTR(Episode, 1, INSTR(Episode, ':') - 1);
                //  CREATE UNIQUE INDEX tmpIndexEpisodePrefixCounts_Prefix
                //      ON tmpTableEpisodePrefixCounts (EpisodePrefix);
                string query = $"{Sql.DropTableIfExists} {tmpTableName} {Sql.Semicolon} {lf}"
                               + $"{Sql.CreateTable} {tmpTableName} {Sql.As} {lf}"
                               + $"  {Sql.Select} {Sql.Substr} ( {episodeColumn}, 1, {Sql.Instr} ( {episodeColumn}, ':') - 1) {Sql.As} {EpisodePrefix}, {lf}"
                               + $"    {Sql.CountStar} {Sql.As} {ImageCount} {lf}"
                               + $"{Sql.From} {DBTables.FileData} {Sql.GroupBy} 1; {lf}"
                               + $"{Sql.CreateUniqueIndex} {tmpIndex} {Sql.On} {tmpTableName} ({EpisodePrefix}); {lf}";
                return query;
            }

            // 2. Image-to-episode-prefix mapping
            // --    Pre-computes the episode prefix for every image, so that SUBSTR/INSTR
            // --    never needs to be re-evaluated in any subsequent query.
            // tmpTableName: tmpTableImageEpisodePrefixMap
            // tmpIndex:     tmpIndexImageEpisodePrefixMap
            // episodeColumn: depends on which column contains the episode data

            internal static string CreateTempTableAndIndexForEpisodePrefixMap(string episodeColumn)
            {
                return CreateTempTableAndIndexForEpisodePrefixMap(tmpTableImageEpisodePrefixMap, tmpIndexImageEpisodePrefixMap, episodeColumn);
            }
            private static string CreateTempTableAndIndexForEpisodePrefixMap(string tmpTableName, string tmpIndex, string episodeColumn)
            {
                //  CREATE TEMP TABLE tmpTableImageEpisodePrefixMap AS
                //     SELECT
                //         Id AS ImageId,
                //         SUBSTR(Episode, 1, INSTR(Episode, ':') - 1) AS EpisodePrefix
                //     FROM DataTable;
                // CREATE UNIQUE INDEX tmpIndexImageEpisodePrefixMap_ImageId
                //     ON tmpTableImageEpisodePrefixMap (ImageId);

                string query = $"{Sql.DropTableIfExists} {tmpTableName} {Sql.Semicolon} {lf}"
                               + $"{Sql.CreateTable} {tmpTableName} {Sql.As} {lf}"
                               + $"  {Sql.Select} {lf}"
                               + $"    {DatabaseColumn.ID} {Sql.As} {ImageId}, {lf}"
                               + $"    {Sql.Substr} ( {episodeColumn}, 1, {Sql.Instr} ( {episodeColumn}, ':') - 1) {Sql.As} {EpisodePrefix} {lf}"
                               + $"  {Sql.From} {DBTables.FileData}; {lf}"
                               + $"{Sql.CreateUniqueIndex} {tmpIndex} ON {tmpTableName} ({ImageId}); {lf}";
                return query;
            }

            #endregion

            #region Sql statements for per where setup
            //  -- ============================================================================
            //  -- PER-WHERE SETUP (rebuild when the user's selection/filter criteria change)
            //  -- ============================================================================

            // -- 3. Filtered image IDs
            //  --    Materializes the set of image IDs that match the current user filter.
            //  --    Used by Query3 categories, Query4, and the episode flags build below.
            //  --    Replace the WHERE clause with the user's current selection criteria.
            // tmpTableName: tmpTableFilteredImageIds
            // tmpIndex:     tmpIndexFilteredImageIds
            internal static string CreateTempTableAndIndexForFilteredImageIds(string whereStatement)
            {
                return CreateTempTableAndIndexForFilteredImageIds(tmpTableFilteredImageIds, tmpIndexFilteredImageIds, whereStatement);
            }
            private static string CreateTempTableAndIndexForFilteredImageIds(string tmpTableName, string tmpIndex, string whereStatement)
            {
                //  CREATE TEMP TABLE tmpTableFilteredImageIds AS
                //      SELECT DataTable.Id AS ImageId
                //      FROM DataTable
                //      WHERE DataTable.Wildlife = 'false' COLLATE NOCASE;
                //      -- e.g. AND DataTable.Sp1_Count > 1 AND DataTable.PlacardPhoto = 'true'
                //  CREATE UNIQUE INDEX tmpIndexFilteredImageIds_ImageId
                //      ON tmpTableFilteredImageIds (ImageId);

                string query = $"{Sql.DropTableIfExists} {tmpTableName} {Sql.Semicolon} {lf}"
                               + $"{Sql.CreateTable} {tmpTableName} {Sql.As} {lf}"
                               + $"  {Sql.Select} {dtId} {Sql.As} {ImageId} {lf}"
                               + $"  {Sql.From} {DBTables.FileData} {lf}"
                               + $"  {whereStatement}; {lf}"
                               + $"{Sql.CreateUniqueIndex} {tmpIndex} ON {tmpTableName} ({ImageId}); {lf}";
                return query;
            }

            #endregion

            #region Sql statements for per-where and per-threshold setup
            // -- ============================================================================
            // -- PER-WHERE + PER-THRESHOLD SETUP (rebuild when WHERE or detection confidence
            // -- threshold changes; only needed if Episodes any for detections query will be executed)
            // Note: it depends on the WHERE indirectly, because it joins against tmpTableFilteredImageIds:              
            // INNER JOIN tmpTableFilteredImageIds  
            // That join restricts which images (and therefore which episode prefixes) are considered. When the WHERE changes, tmpTableFilteredImageIds is 
            // rebuilt with a different set of IDs, which changes which detections participate in the GROUP BY, which changes the resulting flags.
            // -- ============================================================================

            // -- 4. Episode detection flags
            // --    For each episode prefix that has at least one filtered image with a
            // --    detection, determines:
            // --      HasHighConfDetection = 1 if ANY image in the episode has a detection
            // --                               with conf within the threshold range
            // --      HasLowConfOnly       = 1 if ANY image in the episode has detections
            // --                               but NONE of them reach the threshold (Empty)
            // --
            // --    Uses a two-level GROUP BY:
            // --      Inner: per image  — does this image have any high-conf detection?
            // --      Outer: per episode prefix — roll up to episode level
            // --    tmpTableName = tmpTableEpisodeDetectionFlags
            // --    tmpIndex = tmpIndexEpisodeDetectionFlags_Prefix
            // --    tmpTableImageEpisodePrefixMap = tmpTableImageEpisodePrefixMap
            // --    tmpTableFilteredImageIds = tmpTableFilteredImageIds
            // --    @lowerDetectionConfidence and @higherDetectionConfidence are the user's detection confidence thresholds.

            internal static string CreateTempTableAndIndexForEpisodeDetectionFlags(double lowerDetectionConfidence, double higherDetectionConfidence)
            {
                return CreateTempTableAndIndexForEpisodeDetectionFlags(
                    tmpTableEpisodeDetectionFlags, tmpIndexEpisodeDetectionFlags_Prefix,
                    tmpTableImageEpisodePrefixMap, tmpTableFilteredImageIds,
                    lowerDetectionConfidence, higherDetectionConfidence);
            }

            private static string CreateTempTableAndIndexForEpisodeDetectionFlags(
                string tmpTableName1, string tmpIndex1,
                string tmpTableName2, string tmpTableName3,
                double lowerDetectionConfidence, double higherDetectionConfidence)
            {
                // CREATE TEMP TABLE tmpTableEpisodeDetectionFlags AS
                //     SELECT
                //         PerImageFlags.EpisodePrefix,
                //         MAX(PerImageFlags.HasHighConfDetection) AS HasHighConfDetection,
                //         MAX(CASE WHEN PerImageFlags.HasHighConfDetection = 0
                //                  THEN 1 ELSE 0 END)              AS HasLowConfOnly
                //     FROM (
                //         SELECT
                //             tmpTableImageEpisodePrefixMap.EpisodePrefix,
                //             MAX(CASE WHEN Detections.conf BETWEEN @lowerDetectionConfidence AND @higherDetectionConfidence
                //                      THEN 1 ELSE 0 END) AS HasHighConfDetection
                //         FROM Detections
                //         INNER JOIN tmpTableFilteredImageIds
                //             ON tmpTableFilteredImageIds.ImageId = Detections.Id
                //         INNER JOIN tmpTableImageEpisodePrefixMap
                //             ON tmpTableImageEpisodePrefixMap.ImageId = Detections.Id
                //         GROUP BY Detections.Id
                //     ) AS PerImageFlags
                //     GROUP BY PerImageFlags.EpisodePrefix;
                // CREATE UNIQUE INDEX tmpIndexEpisodeDetectionFlags_Prefix
                //     ON tmpTableEpisodeDetectionFlags (EpisodePrefix);


                string query = $"{Sql.DropTableIfExists} {tmpTableName1} {Sql.Semicolon} {lf}"
                               + $"{Sql.CreateTable} {tmpTableName1} {Sql.As} {lf}"
                               + $"   {Sql.Select} {lf}"
                               + $"    {PerImageFlags}.{EpisodePrefix}, {lf}"
                               + $"    {Sql.Max} ({PerImageFlags}.{HasHighConfDetection}) AS {HasHighConfDetection},{lf}"
                               + $"    {Sql.Max} ({Sql.CaseWhen} {PerImageFlags}.{HasHighConfDetection} = 0 {lf}"
                               + $"            {thenOneElse0EndAs} {HasLowConfOnly} {lf}"
                               + $"   {Sql.From} ({lf}"
                               + $"     {Sql.Select} {lf}"
                               + $"       {tmpTableName2}.{EpisodePrefix}, {lf}"
                               + $"       {Sql.Max} ({Sql.CaseWhen} {detConf} {Sql.Between} {lowerDetectionConfidence} {Sql.And} {higherDetectionConfidence} {lf}"
                               + $"            {thenOneElse0EndAs} {HasHighConfDetection} {lf}"
                               + $"     {Sql.From} {DBTables.Detections} {lf}"
                               + $"     {Sql.InnerJoin} {tmpTableName3} {lf}"
                               + $"         {Sql.On} {tmpTableName3}.{ImageId} = {DBTables.Detections}.{DatabaseColumn.ID} {lf}"
                               + $"     {Sql.InnerJoin} {tmpTableName2} {lf}"
                               + $"         {Sql.On} {tmpTableName2}.{ImageId} = {DBTables.Detections}.{DatabaseColumn.ID} {lf}"
                               + $"     {Sql.GroupBy} {DBTables.Detections}.{DatabaseColumn.ID} {lf}"
                               + $"   ) {Sql.As} {PerImageFlags}{lf}"
                               + $"   {Sql.GroupBy} {PerImageFlags}.{EpisodePrefix}; {lf}{lf}"
                               + $"{Sql.CreateUniqueIndex} {tmpIndex1} {Sql.On} {tmpTableName1} ({EpisodePrefix}); {lf}";
                return query;
            }

            #endregion

            #region Two Sql statements for Detection counts with episode-based expansion
            //  -- ============================================================================
            //  -- QUERY 3: Detection counts with episode-based expansion
            //  --    Returns the count of ALL images (not just matching ones) in episodes
            //  --    that contain at least one filtered image with matching detections.
            //  -- ============================================================================

            //  -- Query3 part A: "All" and "Empty" counts in a single query
            //  --    All   = total images in episodes with at least one high-conf detection
            //  --    Empty = total images in episodes where some filtered image has
            //  --            detections but none reached the confidence threshold
            internal static string GetQueryDetectionCountsWithEpisodesAnyForAllAndEmpty()
            {
                return GetQueryDetectionCountsWithEpisodesAnyForAllAndEmpty(tmpTableEpisodeDetectionFlags, tmpTableEpisodePrefixCounts);
            }

            private static string GetQueryDetectionCountsWithEpisodesAnyForAllAndEmpty(string tmpTable1, string tmpTable2)
            {
                // SELECT
                //     SUM(CASE WHEN tmpTableEpisodeDetectionFlags.HasHighConfDetection = 1
                //              THEN tmpTableEpisodePrefixCounts.ImageCount
                //              ELSE 0 END) AS CountAll,
                //     SUM(CASE WHEN tmpTableEpisodeDetectionFlags.HasLowConfOnly = 1
                //              THEN tmpTableEpisodePrefixCounts.ImageCount
                //              ELSE 0 END) AS CountEmpty
                // FROM tmpTableEpisodePrefixCounts
                // INNER JOIN tmpTableEpisodeDetectionFlags
                //     ON tmpTableEpisodeDetectionFlags.EpisodePrefix
                //      = tmpTableEpisodePrefixCounts.EpisodePrefix;

                string SumCaseWhen = $"{Sql.Sum} ( {Sql.CaseWhen}";
                string CountAll = "CountAll";
                string CountEmpty = "CountEmpty";

                string query = $"{Sql.Select} {lf}"
                               + $"  {SumCaseWhen} {tmpTable1}.{HasHighConfDetection} = 1 {lf}"
                               + $"     {Sql.Then} {tmpTable2}.{ImageCount}{lf}"
                               + $"     {Sql.Else} 0 {Sql.End} ) {Sql.As} {CountAll}, {lf}"
                               + $"  {SumCaseWhen} {tmpTable1}.{HasLowConfOnly} = 1 {lf}"
                               + $"     {Sql.Then} {tmpTable2}.{ImageCount}{lf}"
                               + $"     {Sql.Else} 0 {Sql.End} ) {Sql.As} {CountEmpty} {lf}"
                               + $"{Sql.From} {tmpTable2}{lf}"
                               + $"{Sql.InnerJoin} {tmpTable1}{lf}"
                               + $"  {Sql.On} {tmpTable1}.{EpisodePrefix} {lf}"
                               + $"    = {tmpTable2}.{EpisodePrefix}; {lf}";
                return query;
            }

            //  -- Query3 part B: Per-detection-category counts in a single query
            //  --    Returns one row per detection category (e.g. animal, person, vehicle)
            //  --    with the total images in episodes that contain at least one filtered
            //  --    image with a high-conf detection of that category.
            //  --    @DetConfMin and @DetConfMax are the detection confidence thresholds.

            internal static string GetQueryDetectionCountsWithEpisodesAnyForDetectionCategories(
                 double lowerDetectionConfidence, double higherDetectionConfidence)
            {
                return GetQueryDetectionCountsWithEpisodesAnyForDetectionCategories(
                    tmpTableEpisodePrefixCounts,
                    tmpTableImageEpisodePrefixMap, tmpTableFilteredImageIds,
                    lowerDetectionConfidence, higherDetectionConfidence);
            }

            private static string GetQueryDetectionCountsWithEpisodesAnyForDetectionCategories(
                string tmpTable2,
                string tmpTable3, string tmpTable4,
                double lowerDetectionConfidence, double higherDetectionConfidence)
            {
                //  SELECT
                //      DistinctCategoryEpisodes.DetectionCategory,
                //      SUM(tmpTableEpisodePrefixCounts.ImageCount) AS ImageCount
                //  FROM (
                //      SELECT DISTINCT
                //          Detections.category     AS DetectionCategory,
                //          tmpTableImageEpisodePrefixMap.EpisodePrefix
                //      FROM Detections
                //      INNER JOIN tmpTableFilteredImageIds
                //          ON tmpTableFilteredImageIds.ImageId = Detections.Id
                //      INNER JOIN tmpTableImageEpisodePrefixMap
                //          ON tmpTableImageEpisodePrefixMap.ImageId = Detections.Id
                //      WHERE Detections.conf BETWEEN lowerDetectionConfidence AND higherDetectionConfidence
                //  ) AS DistinctCategoryEpisodes

                //  INNER JOIN tmpTableEpisodePrefixCounts
                //      ON tmpTableEpisodePrefixCounts.EpisodePrefix
                //       = DistinctCategoryEpisodes.EpisodePrefix
                //  GROUP BY DistinctCategoryEpisodes.DetectionCategory;
                string DistinctCategoryEpisodes = "DistinctCategoryEpisodes";
                string DetectionCategory = "DetectionCategory";

                string query = $"{Sql.Select} {lf}"
                               + $"  {DistinctCategoryEpisodes}.{DetectionCategory}, {lf}"
                               + $"  {Sql.Sum} ( {tmpTable2}.{ImageCount} ) {Sql.As} {ImageCount} {lf}"
                               + $"{Sql.From} ( {lf}"
                               + $"    {Sql.SelectDistinct} {lf}"
                               + $"      {DBTables.Detections}.category  {Sql.As} {DetectionCategory},{lf}"
                               + $"      {tmpTable3}.{EpisodePrefix}{lf}"
                               + $"    {Sql.From} {DBTables.Detections} {lf}"
                               + $"    {Sql.InnerJoin} {tmpTable4}{lf}"
                               + $"        {Sql.On} {tmpTable4}.{ImageId} = {detId}  {lf}"
                               + $"    {Sql.InnerJoin} {tmpTable3}{lf}"
                               + $"        {Sql.On} {tmpTable3}.{ImageId} = {detId}  {lf}"

                               + $"     {Sql.Where} {detConf} {Sql.Between} {lowerDetectionConfidence} {Sql.And} {higherDetectionConfidence} {lf}"
                               + $") {Sql.As} {DistinctCategoryEpisodes} {lf}"
                               + $"{Sql.InnerJoin} {tmpTable2}{lf}"
                               + $"        {Sql.On} {tmpTable2}.{EpisodePrefix} {lf}"
                               + $"         = {DistinctCategoryEpisodes}.{EpisodePrefix}  {lf}"

                               + $"{Sql.GroupBy} {DistinctCategoryEpisodes}.{DetectionCategory};{lf}"
                    ;
                return query;
            }

            #endregion

            #region Sql statement for Classification counts with episode-based expansion

            // -- ============================================================================
            // -- QUERY 4: Classification counts with episode-based expansion
            // --    Returns one row per classification category with the total images in
            // --    episodes that contain at least one filtered image with a high-conf
            // --    detection AND a high-conf classification of that category.
            // --    Does NOT require tmpTableEpisodeDetectionFlags.
            // --    @DetConfMin / @DetConfMax = detection confidence thresholds
            // --    @ClsConfMin / @ClsConfMax = classification confidence thresholds
            // -- ============================================================================
            internal static string GetQueryClassificationCountsWithEpisodesAny(
                double lowerDetectionConfidence, double higherDetectionConfidence,
                double lowerClassificationConfidence, double higherClassificationConfidence)
            {
                return GetQueryClassificationCountsWithEpisodesAny(
                    tmpTableEpisodePrefixCounts,
                    tmpTableImageEpisodePrefixMap, tmpTableFilteredImageIds,
                    lowerDetectionConfidence, higherDetectionConfidence,
                    lowerClassificationConfidence, higherClassificationConfidence);
            }

            private static string GetQueryClassificationCountsWithEpisodesAny(
                string tmpTable2,
                string tmpTable3, string tmpTable4,
                double lowerDetectionConfidence, double higherDetectionConfidence,
                double lowerClassificationConfidence, double higherClassificationConfidence)
            {
                // SELECT
                //     DistinctClassificationEpisodes.ClassificationCategory,
                //     SUM(tmpTableEpisodePrefixCounts.ImageCount) AS ImageCount
                // FROM (
                //     SELECT DISTINCT
                //         Detections.classification AS ClassificationCategory,
                //         tmpTableImageEpisodePrefixMap.EpisodePrefix
                //     FROM Detections
                //     INNER JOIN tmpTableFilteredImageIds
                //         ON tmpTableFilteredImageIds.ImageId = Detections.Id
                //     INNER JOIN tmpTableImageEpisodePrefixMap
                //         ON tmpTableImageEpisodePrefixMap.ImageId = Detections.Id
                //     WHERE Detections.conf BETWEEN lowerDetectionConfidence AND higherDetectionConfidence
                //       AND Detections.classification_conf BETWEEN lowerClassificationConfidence AND higherClassificationConfidence

                // ) AS DistinctClassificationEpisodes
                // INNER JOIN tmpTableEpisodePrefixCounts
                //     ON tmpTableEpisodePrefixCounts.EpisodePrefix
                //      = DistinctClassificationEpisodes.EpisodePrefix
                // GROUP BY DistinctClassificationEpisodes.ClassificationCategory;
                string ClassificationCategory = "ClassificationCategory";
                string DistinctClassificationEpisodes = "DistinctClassificationEpisodes";

                string query = $"{Sql.Select} {lf}"
                             + $"  {DistinctClassificationEpisodes}.{ClassificationCategory}, {lf}"
                             + $"  {Sql.Sum} ( {tmpTable2}.{ImageCount} ) {Sql.As} {ImageCount} {lf}"
                             + $"{Sql.From} ( {lf}"
                             + $"    {Sql.SelectDistinct} {lf}"
                             + $"      {DBTables.Detections}.classification  {Sql.As} {ClassificationCategory},{lf}"
                             + $"      {tmpTable3}.{EpisodePrefix}{lf}"
                             + $"    {Sql.From} {DBTables.Detections} {lf}"

                             + $"    {Sql.InnerJoin} {tmpTable4}{lf}"
                             + $"        {Sql.On} {tmpTable4}.{ImageId} = {detId}  {lf}"
                             + $"    {Sql.InnerJoin} {tmpTable3}{lf}"
                             + $"        {Sql.On} {tmpTable3}.{ImageId} = {detId}  {lf}"
                             + $"     {Sql.Where} {detConf} {Sql.Between} {lowerDetectionConfidence} {Sql.And} {higherDetectionConfidence} {lf}"
                             + $"        {Sql.And} {DBTables.Detections}.{DetectionColumns.ClassificationConf} {Sql.Between} {lowerClassificationConfidence} {Sql.And} {higherClassificationConfidence}{lf}"
                             + $") {Sql.As} {DistinctClassificationEpisodes} {lf}"
                             + $"{Sql.InnerJoin} {tmpTable2}{lf}"
                             + $"        {Sql.On} {tmpTable2}.{EpisodePrefix} {lf}"
                             + $"         = {DistinctClassificationEpisodes}.{EpisodePrefix}  {lf}"
                             + $"{Sql.GroupBy} {DistinctClassificationEpisodes}.{ClassificationCategory};{lf}"
                    ;
                return query;
            }

            #endregion

            #region Sql statement for Cleaning up temporary tables
            //   -- ============================================================================
            // -- CLEANUP (run when the session ends)
            // -- ============================================================================
            internal static string DoDropTemporaryTablesForSession()
            {
                return DoDropTemporaryTablesForSession(
                    tmpTableEpisodeDetectionFlags, tmpTableEpisodePrefixCounts,
                    tmpTableImageEpisodePrefixMap, tmpTableFilteredImageIds);
            }

            private static string DoDropTemporaryTablesForSession(
                string tmpTable1, string tmpTable2,
                string tmpTable3, string tmpTable4)
            {
                string query = $"{Sql.DropTableIfExists} {tmpTable1}; {lf}"
                               + $"{Sql.DropTableIfExists} {tmpTable2}; {lf}"
                               + $"{Sql.DropTableIfExists} {tmpTable3}; {lf}"
                               + $"{Sql.DropTableIfExists} {tmpTable4}; {lf}";
                return query;
            }

            #endregion
        }
        #endregion

    }
}
