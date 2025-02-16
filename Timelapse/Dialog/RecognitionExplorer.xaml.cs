using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using Timelapse.DataStructures;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.SearchingAndSorting;
using Timelapse.Recognition;
using System.Windows.Media;
using Timelapse.Extensions;
using Application = System.Windows.Application;

namespace Timelapse.Dialog
{
    /// <summary>
    /// TODO: Would be nice to have a click to drag...
    /// TODO: Maybe IsSnapToTickEnabled="True" with 0.01 and no ticks?
    /// TODO: Make it actually select when select an item
    /// </summary>
    public partial class RecognitionExplorer
    {
        #region Properties and Variables
        // Accessing external parameters
        private readonly FileDatabase Database;
        private readonly CustomSelection CustomSelection;
        private readonly RecognitionSelections DetectionSelections;

        // Dictionaries that will eventually hold the Detection and Classification categories  
        private Dictionary<string, string> DetectionCategories;
        private Dictionary<string, string> ClassificationCategories;

        // These collections are used to store the various Count results as they are updated.
        // They serve as ItemsSource to the corresponding DataGrid and ListBox controls 
        public ObservableCollection<CategoryCount> DetectionCountsCollection { get; set; } = new ObservableCollection<CategoryCount>();
        public ObservableCollection<CategoryCount> ClassificationCountsCollection { get; set; } = new ObservableCollection<CategoryCount>();
        public List<string> ClassificationEmptyCountsList { get; set; } = new List<string>();

        // Original selection parameters so we can restore them as needed on exit
        private bool UseRecognitions;
        private string DetectionCategory;
        private string ClassificationCategory;
        private bool AllDetections;
        private bool InterpretAllDetectionsAsEmpty;
        private double ConfidenceThreshold1ForUI;
        private double ConfidenceThreshold2ForUI;
        private RecognitionType RecognitionType;
        private bool RankByConfidence;
        private bool ShowMissingDetections;
        private int RandomSample;
        private bool EpisodeShowAllIfAnyMatch;

        // The currently selected recognition, if any. This will be used to replace the original selection parameters
        private readonly CurrentSelection CurrentlySelectedRecognition = new CurrentSelection();
        private readonly string AllCategoryNumber = "-1";
        private CancellationTokenSource TokenSource = new CancellationTokenSource();
        #endregion

        #region Constructor / Loaded
        public RecognitionExplorer(Window owner, FileDatabase database)
        {
            InitializeComponent();
            this.Owner = owner;
            this.Database = database;
            this.CustomSelection = database?.CustomSelection;
            this.DetectionSelections = database?.CustomSelection?.DetectionSelections;
        }

        private async void RecognitionsExplorer_OnLoaded(object sender, RoutedEventArgs e)
        {
            // Adjust this dialog window position 
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            // Alter the default look of  Datagrid
            SetDataGridCategoryCountLook(this.DataGridDetections);
            SetDataGridCategoryCountLook(this.DataGridClassifications);

            //Abort if there is nothing to show
            if (this.Database == null)
            {
                this.TBMessage.Text = "No image set loaded";
                this.DisableAllControls();
                return;
            }
            if (GlobalReferences.DetectionsExists == false)
            {
                this.TBMessage.Text = "No recognitions are present in this image set.";
                this.DisableAllControls();
                return;
            }

            // If we make it here, recognitions are turned on for this image set

            // Ensure the detection categories are populated
            // - abort if there are none (likely a problem with the json file?)
            this.Database.CreateDetectionCategoriesDictionaryIfNeeded();
            this.DetectionCategories = Database.detectionCategoriesDictionary;
            if (null == this.DetectionCategories || this.DetectionCategories.Count == 0)
            {
                this.TBMessage.Text = "Your recognitions are missing the detection categories";
                this.DisableAllControls();
                return;
            }

            //
            // At this point we should at least have some detections to try to count, so lets get going.
            // 

            // Save the original recognition parameters, so we can restore them later
            // Then initialize parameters to use recognitions
            this.ParametersSaveOriginalRecognitions();

            // Detection-related initialization for the first query:
            // - Initialize the Detection confidence range and SliderDetectionConf value
            // - Display the current Confidence range in the detections title
            SliderDetectionConf.Value = this.UseRecognitions && this.RecognitionType == RecognitionType.Detection
                ? Math.Round(DetectionSelections.ConfidenceThreshold1ForUI, 2)
                : Math.Round(Database.GetTypicalDetectionThreshold(), 2);
            DetectionSelections.ConfidenceThreshold2ForUI = 1;
            // SliderDetectionConf.Value = Math.Round(DetectionSelections.ConfidenceThreshold1ForUI, 2);
            this.DisplayDetectionConfidenceRange(Math.Round(SliderDetectionConf.Value, 2));

            // Clear the counts for each detection category held in the DetectionCounts
            // This will also create and entry for each detection category if it doesn't already exist.
            this.ClearCountsInDetectionCountsCollection();

            // Classifications
            // - Display the current Confidence range in the classifications title
            // - Retrieve the classification categories
            // - Generate a count for each category, splitting them into two lists
            SliderClassificationConf.Value = this.UseRecognitions && this.RecognitionType == RecognitionType.Classification
                ? Math.Round(this.ConfidenceThreshold1ForUI, 2)
                : Math.Round(Database.GetTypicalClassificationThreshold(), 2);
            
            DisplayClassificationConfidenceRange(Math.Round(SliderClassificationConf.Value, 2));
            this.Database.CreateClassificationCategoriesDictionaryIfNeeded();
            this.ClassificationCategories = this.Database.classificationCategoriesDictionary;

            this.ParametersIntializeAsDetections();

            // Count and display the total number of files in the current selection with recognitions turned off
            int totalFiles = DoCountFilesInCurrentSelectionWithRecognitionsOff(); // database.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.Custom);
            this.TBTotalFiles.Text = $"{totalFiles}";

            this.DisplayCurrentSelection();

            // Get the recognition counts for both detections and (if they are present) classifications
            RecognitionTypeEnum recognitionTypeEnum = RecognitionTypeEnum.DetectionsAndClassifications;
            if (this.ClassificationCategories == null || this.ClassificationCategories.Count == 0)
            {
                // As there are no classifications in this image set,
                // - disable the classificatin controls
                // - set things to only update the detection counts
                this.DisableClassificationControls();
                recognitionTypeEnum = RecognitionTypeEnum.Detections;
            }
            else
            {
                // We have classifications, so clear the counts
                this.ClearCountsInClassificationCountsCollection();
            }
            // Generate the counts
            await this.DoCountRecognitionsAsync(new CancellationTokenSource(), recognitionTypeEnum);
        }
        #endregion

        #region Counting
        private int DoCountFilesInCurrentSelectionWithRecognitionsOff()
        {
            // Get the total number of files in the current selection without recognitions
            DetectionSelections.UseRecognition = false;
            int totalFiles = Database.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.Custom);
            DetectionSelections.UseRecognition = true;
            return totalFiles;
        }

        public async Task DoCountRecognitionsAsync(CancellationTokenSource cancellationTokenSource, RecognitionTypeEnum recognitionType)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            double lowerDetectionConfidenceValue = Math.Round(this.SliderDetectionConf.Value, 2);
            double lowerClassificationConfidenceValue = Math.Round(this.SliderClassificationConf.Value, 2);
            await Task.Run(() =>
            {
                bool success = false;
                if (recognitionType == RecognitionTypeEnum.Detections || recognitionType == RecognitionTypeEnum.DetectionsAndClassifications)
                {

                    success = DoCountDetections(cancellationTokenSource, lowerDetectionConfidenceValue);
                    if (false == success)
                    {
                        return;
                    }
                }

                if (recognitionType == RecognitionTypeEnum.Classifications || recognitionType == RecognitionTypeEnum.DetectionsAndClassifications)
                {

                    success = DoCountClassifications(cancellationTokenSource, lowerClassificationConfidenceValue);
                }

            }, cancellationTokenSource.Token);
            Mouse.OverrideCursor = null;
        }

        private bool DoCountDetections(CancellationTokenSource cancellationTokenSource, double lowerConfidenceValue)
        {
            if (cancellationTokenSource.Token.IsCancellationRequested)
            {
                return false;
            }

            // All category count 
            DetectionSelections.RecognitionType = RecognitionType.Detection;
            DetectionSelections.AllDetections = true;
            DetectionSelections.InterpretAllDetectionsAsEmpty = false;
            this.DetectionSelections.ConfidenceThreshold1ForUI = lowerConfidenceValue;
            this.DetectionSelections.ConfidenceThreshold2ForUI = 1;

            int allFilesCount = Database.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.Custom);
            if (cancellationTokenSource.Token.IsCancellationRequested)
            {
                // Don't bother showing the results if its been cancelled
                return false;
            }
            this.SetDetectionCountForCategory("All", allFilesCount);
            DetectionSelections.AllDetections = false;
            DetectionSelections.InterpretAllDetectionsAsEmpty = false;
            DetectionSelections.UseRecognition = true;

            if (cancellationTokenSource.Token.IsCancellationRequested)
            {
                return false;
            }

            // Empty category count  
            // TODO: EMPTY NUMBERS DONT MAKE SENSE, OR DO THEY?
            DetectionSelections.InterpretAllDetectionsAsEmpty = true;
            int emptyFilesCount = Database.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.Custom);
            if (cancellationTokenSource.Token.IsCancellationRequested)
            {
                // Don't bother showing the results if its been cancelled
                return false;
            }

            this.SetDetectionCountForCategory("Empty", emptyFilesCount);
            DetectionSelections.InterpretAllDetectionsAsEmpty = false;

            // Individual category counts
            foreach (KeyValuePair<string, string> kvp in DetectionCategories)
            {
                if (cancellationTokenSource.Token.IsCancellationRequested)
                {
                    return false;
                }
                //cancellationTokenSource.Token.ThrowIfCancellationRequested();
                if (kvp.Key == "0") continue; // Skip empty
                DetectionSelections.DetectionCategory = kvp.Key;
                int distinctCount = Database.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.Custom);
                if (cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Don't bother showing the results if its been cancelled
                    return false;
                }
                string categoryName = kvp.Value;
                this.SetDetectionCountForCategory(categoryName, distinctCount);
            }

            return true;
        }
        private bool DoCountClassifications(CancellationTokenSource cancellationTokenSource, double lowerConfidenceValue)
        {
            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                this.DataGridClassifications.IsEnabled = false;
                this.LBEmptyClassifications.IsEnabled = false;
            });

            // Abort if there are no classifications
            if (this.ClassificationCategories == null || this.ClassificationCategories.Count == 0 || cancellationTokenSource.Token.IsCancellationRequested)
            {
                Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    this.ClassificationControlsEnableState(true, true);
                });
                return false;
            }

            this.ClearCountsInClassificationCountsCollection();

            // Set search criteria to classifications
            DetectionSelections.InterpretAllDetectionsAsEmpty = false;
            DetectionSelections.RecognitionType = RecognitionType.Classification;
            this.DetectionSelections.ConfidenceThreshold1ForUI = lowerConfidenceValue;

            // Store the counts
            Dictionary<string, int> resultValidClassifications = new Dictionary<string, int>();
            List<string> resultNoClassificationsFound = new List<string>();
            foreach (KeyValuePair<string, string> kvp in this.ClassificationCategories)
            {
                if (cancellationTokenSource.Token.IsCancellationRequested)
                {
                    Application.Current.Dispatcher.Invoke((Action)delegate
                    {
                        this.ClassificationControlsEnableState(true, true);
                    });
                    return false;
                }

                string categoryNumber = kvp.Key;
                DetectionSelections.ClassificationCategory = categoryNumber;
                string categoryName = kvp.Value;
                int distinctCount = Database.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.Custom);
                resultValidClassifications.Add(categoryName, distinctCount);
                if (distinctCount == 0)
                {
                    resultNoClassificationsFound.Add(categoryName);
                }
            }

            // Set the counts for each classification category
            foreach (KeyValuePair<string, int> kvp in resultValidClassifications)
            {
                this.SetClassificationCountForCategory(kvp.Key, kvp.Value);
            }

            // Update the empty classifications
            ClassificationEmptyCountsList.Clear();
            foreach (string category in resultNoClassificationsFound)
            {
                this.ClassificationEmptyCountsList.Add(category);
            }

            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                SortDataGrid(this.DataGridClassifications, 0, ListSortDirection.Descending);
                LBEmptyClassifications.ItemsSource = null;
                LBEmptyClassifications.ItemsSource = ClassificationEmptyCountsList;
                this.ClassificationControlsEnableState(true, true);
            });
            return true;
        }
        #endregion

        #region UI Display Updates
        private void DisplayCurrentSelection()
        {
            if (this.CurrentlySelectedRecognition.IsRecognitionSelected == false)
            {
                this.TBSelectionFeedback.Text = "No recognition selected.";
                this.OkButton.IsEnabled = false;
                return;
            }
            this.OkButton.IsEnabled = true;

            // Category name (1st letter in caps)
            string msg = CurrentlySelectedRecognition.CategoryName.Length > 1
                ? CurrentlySelectedRecognition.CategoryName[0].ToString().ToUpper() + CurrentlySelectedRecognition.CategoryName.Substring(1)
                : CurrentlySelectedRecognition.CategoryName;
            if (CurrentlySelectedRecognition.RecognitionType == RecognitionTypeEnum.Detections)
            {
                msg += $" detections ≥ {this.SliderDetectionConf.Value:f2}";
            }
            else
            {
                msg += $" classifications ≥ {this.SliderClassificationConf.Value:f2}";
            }
            this.TBSelectionFeedback.Text = msg;
        }

        // Set the look of  Datagrid holding the CategoryCount values
        private static void SetDataGridCategoryCountLook(DataGrid dataGrid)
        {
            if (dataGrid.Columns.Count <= 1)
            {
                return;
            }
            // Category column: Try to size to just fit the widest category content
            dataGrid.Columns[0].Width = new DataGridLength(1, DataGridLengthUnitType.Auto);

            // Count column: Fill the remaining space, if any
            dataGrid.Columns[1].Width = new DataGridLength(1, DataGridLengthUnitType.Star);
        }

        private void ClearSelectionsAndScrollToTop(DataGrid dataGrid)
        {
            this.DataGridDetections.UnselectAllCells();
            this.DataGridClassifications.UnselectAllCells();
            if (dataGrid == this.DataGridDetections)
            {
                this.DataGridDetections.ScrollIntoViewFirstRow();
            }
            else
            {
                this.DataGridClassifications.ScrollIntoViewFirstRow();
                this.EmptyClassificationsScrollViewer.ScrollToTop();
            }
        }
        private void DisplayDetectionConfidenceRange(double lowerConfidence)
        {
            this.TBDetectionsCount.Text = $"({lowerConfidence:f2} - {this.DetectionSelections.ConfidenceThreshold2ForUI:f2})";
        }

        private void DisplayClassificationConfidenceRange(double lowerConfidence)
        {
            this.TBClassificationsCount.Text = $"({lowerConfidence:f2} - {this.DetectionSelections.ConfidenceThreshold2ForUI:f2})";
            this.TBBelowClassificationValue.Text = $" (below {lowerConfidence:f2} or absent)";
        }

        // Disable the various controls, usually because there is nothing to show
        private void DisableAllControls()
        {
            this.TBTotalFiles.Text = string.Empty;
            this.DataGridDetections.IsEnabled = false;
            this.SliderDetectionConf.IsEnabled = false;
            this.TBDetectionsLabel.Foreground = Brushes.DarkGray;
            this.DisableClassificationControls();
        }

        private void DisableClassificationControls()
        {
            this.TBClassificationsLabel.Foreground = Brushes.DarkGray;
            this.TBBelowClassificationsLabel.Foreground = Brushes.DarkGray;
            this.SliderClassificationConf.IsEnabled = false;
            this.ClassificationControlsEnableState(false, false);
        }


        private void DetectionControlsEnableState(bool enableState, bool updateCursorToMatchState)
        {
            if (updateCursorToMatchState)
            {
                Mouse.OverrideCursor = enableState ? null : Cursors.Wait;
            }
            this.DataGridDetections.IsEnabled = enableState;
        }

        private void ClassificationControlsEnableState(bool enableState, bool updateCursorToMatchState)
        {
            if (updateCursorToMatchState)
            {
                Mouse.OverrideCursor = enableState ? null : Cursors.Wait;
            }
            this.DataGridClassifications.IsEnabled = enableState;
            this.LBEmptyClassifications.IsEnabled = enableState; //enableState;
        }

        // Sort the given data grid by the first column (i.e., the Count) in ascending order
        public static void SortDataGrid(DataGrid dataGrid, int columnIndex = 0, ListSortDirection sortDirection = ListSortDirection.Ascending)
        {
            var column = dataGrid.Columns[columnIndex];

            // Clear current sort descriptions
            dataGrid.Items.SortDescriptions.Clear();

            // Add the new sort description
            dataGrid.Items.SortDescriptions.Add(new SortDescription(column.SortMemberPath, sortDirection));

            // Apply sort
            foreach (var col in dataGrid.Columns)
            {
                col.SortDirection = null;
            }
            column.SortDirection = sortDirection;

            // Refresh items to display sort
            dataGrid.Items.Refresh();
        }
        #endregion

        #region Save/Restore/Initialize original selection parameters

        // Save the original selection parameters.
        // As we will alter some of these, we will restore them later back to its original values
        private void ParametersSaveOriginalRecognitions()
        {
            this.UseRecognitions = this.DetectionSelections.UseRecognition;
            this.DetectionCategory = DetectionSelections.DetectionCategory;
            this.ClassificationCategory = DetectionSelections.ClassificationCategory;
            this.AllDetections = DetectionSelections.AllDetections;
            this.InterpretAllDetectionsAsEmpty = DetectionSelections.InterpretAllDetectionsAsEmpty;
            this.ConfidenceThreshold1ForUI = DetectionSelections.ConfidenceThreshold1ForUI;
            this.ConfidenceThreshold2ForUI = DetectionSelections.ConfidenceThreshold2ForUI;
            this.RecognitionType = DetectionSelections.RecognitionType;
            this.RankByConfidence = DetectionSelections.RankByConfidence;
            this.ShowMissingDetections = CustomSelection.ShowMissingDetections;
            this.RandomSample = CustomSelection.RandomSample;
            this.EpisodeShowAllIfAnyMatch = CustomSelection.EpisodeShowAllIfAnyMatch;
        }

        private void ParametersRestoreOriginalRecognitions()
        {
            if (null != DetectionSelections)
            {
                // Restore original selection parameters as we may have alter some of these
                this.DetectionSelections.UseRecognition = this.UseRecognitions;
                this.DetectionSelections.DetectionCategory = this.DetectionCategory;
                this.DetectionSelections.AllDetections = this.AllDetections;
                this.DetectionSelections.InterpretAllDetectionsAsEmpty = this.InterpretAllDetectionsAsEmpty;
                this.DetectionSelections.ConfidenceThreshold1ForUI = this.ConfidenceThreshold1ForUI;
                this.DetectionSelections.ConfidenceThreshold2ForUI = this.ConfidenceThreshold2ForUI;
                this.DetectionSelections.RecognitionType = this.RecognitionType;
                this.DetectionSelections.ClassificationCategory = this.ClassificationCategory;
                this.DetectionSelections.RankByConfidence = this.RankByConfidence;
            }
            if (null != CustomSelection)
            {
                this.CustomSelection.ShowMissingDetections = this.ShowMissingDetections;
                this.CustomSelection.RandomSample = this.RandomSample;
                this.CustomSelection.EpisodeShowAllIfAnyMatch = this.EpisodeShowAllIfAnyMatch;
            }
        }

        private void ParametersIntializeAsDetections()
        {
            // Initialize parameters
            DetectionSelections.AllDetections = false;
            DetectionSelections.RecognitionType = RecognitionType.Detection;

            DetectionSelections.RankByConfidence = false;
            CustomSelection.ShowMissingDetections = false;
            CustomSelection.EpisodeShowAllIfAnyMatch = false;
        }

        #endregion

        #region UI callbacks - OnSelectionChanged
        private void DataGridDetections_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid dataGrid)
            {
                if (dataGrid.SelectedItems.Count == 1 && dataGrid.SelectedItems[0] is CategoryCount categoryCount)
                {
                    // The user selected All. By convention (and only used in this class), All is mapped to the category string in AllCategoryNumber (-1)
                    if (categoryCount.Category == Constant.RecognizerValues.AllDetectionLabel)
                    {
                        this.CurrentlySelectedRecognition.IsRecognitionSelected = true;
                        this.CurrentlySelectedRecognition.RecognitionType = RecognitionTypeEnum.Detections;
                        this.CurrentlySelectedRecognition.CategoryName = categoryCount.Category;
                        this.CurrentlySelectedRecognition.CategoryNumber = this.AllCategoryNumber;
                        this.DisplayCurrentSelection();
                        return;
                    }

                    // The user selected a category (which could include empty)
                    string categoryNumber = string.Empty;

                    // Get the category number from its name
                    foreach (KeyValuePair<string, string> kvp in this.DetectionCategories)
                    {
                        if (EqualityComparer<string>.Default.Equals(kvp.Value, categoryCount.Category))
                        {
                            categoryNumber = kvp.Key;
                            break;
                        }
                    }
                    if (categoryNumber != string.Empty)
                    {
                        // Set it to the selected category
                        this.CurrentlySelectedRecognition.IsRecognitionSelected = true;
                        this.CurrentlySelectedRecognition.RecognitionType = RecognitionTypeEnum.Detections;
                        this.CurrentlySelectedRecognition.CategoryName = categoryCount.Category;
                        this.CurrentlySelectedRecognition.CategoryNumber = categoryNumber;
                        this.DisplayCurrentSelection();
                        return;
                    }
                }
                // Something went wrong. Feedback???
                this.CurrentlySelectedRecognition.IsRecognitionSelected = false;
                this.DisplayCurrentSelection();
            }
        }

        private void DataGridClassifications_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid dataGrid)
            {
                if (dataGrid.SelectedItems.Count == 1 && dataGrid.SelectedItems[0] is CategoryCount categoryCount)
                {
                    // The user selected a category (which could include empty)
                    string categoryNumber = string.Empty;

                    // Get the category number from its name
                    foreach (KeyValuePair<string, string> kvp in this.ClassificationCategories)
                    {
                        if (EqualityComparer<string>.Default.Equals(kvp.Value, categoryCount.Category))
                        {
                            categoryNumber = kvp.Key;
                            break;
                        }
                    }
                    if (categoryNumber != string.Empty)
                    {
                        // Set it to the selected category
                        this.CurrentlySelectedRecognition.IsRecognitionSelected = true;
                        this.CurrentlySelectedRecognition.RecognitionType = RecognitionTypeEnum.Classifications;
                        this.CurrentlySelectedRecognition.CategoryName = categoryCount.Category;
                        this.CurrentlySelectedRecognition.CategoryNumber = categoryNumber;
                        this.DisplayCurrentSelection();
                        return;
                    }
                }
                // Something went wrong. Feedback???
                this.CurrentlySelectedRecognition.IsRecognitionSelected = false;
                this.DisplayCurrentSelection();
            }
        }
        #endregion

        #region Slider - On DragDelta, OnDragCompleted
        // When the detection drag is in progress
        // - disable the detection controls
        // - display the updated slider value
        private bool isDetectionSliderDragging;
        private void SliderDetectionConf_OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            if (!(sender is Slider slider) || null == this.DetectionCategories)
            {
                return;
            }

            if (false == isDetectionSliderDragging)
            {
                // disable the detection controls when dragging begins and set values to -1
                this.DetectionControlsEnableState(false, true);
            }
            isDetectionSliderDragging = true;
            this.DisplayDetectionConfidenceRange(Math.Round(slider.Value, 2));
            this.ClearSelectionsAndScrollToTop(this.DataGridDetections);
            this.CurrentlySelectedRecognition.IsRecognitionSelected = false;
        }

        // When the detection drag is complete
        // - enable the detection controls
        // - update the detection count to the new confidence value, but only if it differs
        private async void SliderDetectionConf_OnDragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (!(sender is Slider slider) || null == this.DetectionCategories)
            {
                return;
            }

            bool noChange = Math.Abs(Math.Round(slider.Value, 2) - this.DetectionSelections.ConfidenceThreshold1ForUI) < .01;
            isDetectionSliderDragging = false;
            this.DetectionControlsEnableState(true, true);

            if (!noChange)
            {
                // Set and display the new confidence thresholds
                this.DetectionSelections.ConfidenceThreshold1ForUI = Math.Round(slider.Value, 2);
                this.DisplayDetectionConfidenceRange(Math.Round(this.DetectionSelections.ConfidenceThreshold1ForUI, 2));

                // Clear the current counts and start counting the new ones
                this.ClearCountsInDetectionCountsCollection();

                // Recount classifications. 
                // Note that we don't clear counts as we are rebuilding the entire list
                // Cancel the last operation, then reset the token for the next operation
                this.TokenSource.Cancel();
                this.TokenSource = new CancellationTokenSource();
                await this.DoCountRecognitionsAsync(new CancellationTokenSource(), RecognitionTypeEnum.Detections);
            }
        }
        // Classification slider -
        // - only drags are allowed (no click to increment or keyboard shortcuts)
        // - disable Classification controls when dragging to provide feedback
        // - only start counting after a drag is completed 
        private bool isClassificationSliderDragging;

        // When the classification drag is in progress
        // - disable the classification controls
        // - display the updated slider value
        private void SliderClassificationConf_OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            if (!(sender is Slider slider) || null == this.ClassificationCategories)
            {
                return;
            }

            if (false == isClassificationSliderDragging)
            {
                // disable the classification controls when dragging begins
                this.ClassificationControlsEnableState(false, true);
            }
            isClassificationSliderDragging = true;
            this.DisplayClassificationConfidenceRange(Math.Round(slider.Value, 2));
            this.ClearSelectionsAndScrollToTop(this.DataGridClassifications);
        }

        // When the classification drag is complete
        // - enable the classification controls
        // - update the classification count to the new confidence value, but only if it differs
        private async void SliderClassificationConf_OnDragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (!(sender is Slider slider) || null == this.ClassificationCategories)
            {
                return;
            }

            bool noChange = Math.Abs(Math.Round(slider.Value, 2) - this.DetectionSelections.ConfidenceThreshold1ForUI) < .01;
            isClassificationSliderDragging = false;
            this.ClassificationControlsEnableState(true, true);

            if (!noChange)
            {
                // Set and display the new confidence thresholds
                this.DetectionSelections.ConfidenceThreshold1ForUI = Math.Round(this.SliderClassificationConf.Value, 2);
                this.DetectionSelections.ConfidenceThreshold2ForUI = 1;

                // Recount classifications. 
                // Note that we don't clear counts as we are rebuilding the entire list
                this.ClearCountsInClassificationCountsCollection();
                // Cancel the last operation, then reset the token for the next operation
                this.TokenSource.Cancel();
                this.TokenSource = new CancellationTokenSource();
                await this.DoCountRecognitionsAsync(new CancellationTokenSource(), RecognitionTypeEnum.Classifications);
            }
        }
        #endregion

        #region UI Callbacks - Button clicks
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.TokenSource.Cancel();
            this.ParametersRestoreOriginalRecognitions();

            if (this.CurrentlySelectedRecognition.IsRecognitionSelected)
            {
                // A recognition is selected, so ensure that UseRecognition is on
                this.DetectionSelections.UseRecognition = true;

                if (this.CurrentlySelectedRecognition.RecognitionType == RecognitionTypeEnum.Detections)
                {
                    // Detections
                    if (this.CurrentlySelectedRecognition.CategoryNumber == this.AllCategoryNumber)
                    {
                        // All
                        DetectionSelections.AllDetections = true;
                        DetectionSelections.InterpretAllDetectionsAsEmpty = false;
                        DetectionSelections.RecognitionType = RecognitionType.Detection;
                        this.DetectionSelections.DetectionCategory = string.Empty;
                        this.DetectionSelections.ClassificationCategory = string.Empty;
                        this.DetectionSelections.ConfidenceThreshold1ForUI = this.SliderDetectionConf.Value;
                        this.DetectionSelections.ConfidenceThreshold2ForUI = 1;
                        //this.DetectionSelections.RankByConfidence = false;
                        this.CustomSelection.ShowMissingDetections = false;
                        this.DialogResult = true;
                        return;
                    }

                    if (this.CurrentlySelectedRecognition.CategoryNumber == "0")
                    {
                        // Empty
                        DetectionSelections.AllDetections = true;
                        DetectionSelections.InterpretAllDetectionsAsEmpty = true;
                        DetectionSelections.RecognitionType = RecognitionType.Detection;
                        this.DetectionSelections.DetectionCategory = this.CurrentlySelectedRecognition.CategoryNumber;
                        this.DetectionSelections.ClassificationCategory = string.Empty;
                        this.DetectionSelections.ConfidenceThreshold1ForUI = this.SliderDetectionConf.Value;
                        this.DetectionSelections.ConfidenceThreshold2ForUI = 1;
                        //this.DetectionSelections.RankByConfidence = false;
                        this.CustomSelection.ShowMissingDetections = false;
                        this.DialogResult = true;
                        return;
                    }

                    // TODO: Need to treat Empty as special case 
                    DetectionSelections.AllDetections = false;
                    DetectionSelections.InterpretAllDetectionsAsEmpty = false;

                    DetectionSelections.RecognitionType = RecognitionType.Detection;
                    this.DetectionSelections.DetectionCategory = this.CurrentlySelectedRecognition.CategoryNumber;
                    this.DetectionSelections.ClassificationCategory = string.Empty;
                    this.DetectionSelections.ConfidenceThreshold1ForUI = this.SliderDetectionConf.Value;
                    this.DetectionSelections.ConfidenceThreshold2ForUI = 1;
                    //this.DetectionSelections.RankByConfidence = false;
                    this.CustomSelection.ShowMissingDetections = false;
                    //this.CustomSelection.EpisodeShowAllIfAnyMatch = false;
                    this.DialogResult = true;
                    return;
                }

                if (this.CurrentlySelectedRecognition.RecognitionType == RecognitionTypeEnum.Classifications)
                {
                    // Classification
                    DetectionSelections.AllDetections = false;
                    DetectionSelections.InterpretAllDetectionsAsEmpty = false;

                    DetectionSelections.RecognitionType = RecognitionType.Classification;
                    this.DetectionSelections.DetectionCategory = string.Empty;
                    this.DetectionSelections.ClassificationCategory = this.CurrentlySelectedRecognition.CategoryNumber;
                    this.DetectionSelections.ConfidenceThreshold1ForUI = this.SliderClassificationConf.Value;
                    this.DetectionSelections.ConfidenceThreshold2ForUI = 1;
                    //this.DetectionSelections.RankByConfidence = false;
                    this.CustomSelection.ShowMissingDetections = false;
                    //this.CustomSelection.EpisodeShowAllIfAnyMatch = false;
                    this.DialogResult = true;
                    return;
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.TokenSource.Cancel();
            this.ParametersRestoreOriginalRecognitions();
            this.DialogResult = false;
        }
        #endregion

        #region Setting and Clearing DetectionCountsCollection
        // Given a category and a count, add or update it in the DetectionCountsCollection
        private void SetDetectionCountForCategory(string category, int count)
        {
            var categoryCount = this.DetectionCountsCollection.FirstOrDefault(i => i.Category == category);
            if (null == categoryCount)
            {
                Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    CategoryCount cc = new CategoryCount(category, count);
                    this.DetectionCountsCollection.Add(cc);
                    cc.NotifyPropertyChanged("Count");
                });
            }
            else
            {
                categoryCount.Count = count;
                categoryCount.NotifyPropertyChanged("Count");
            }
        }

        // Clear the counts for each detection category held in the DetectionCounts
        // This will also create and entry for each deteccton category if it doesn't already exist.
        private void ClearCountsInDetectionCountsCollection()
        {
            // Clear the All and Empty categories 
            this.SetDetectionCountForCategory("All", -1);
            this.SetDetectionCountForCategory("Empty", -1);

            // Detections: clear the count in Category 
            foreach (KeyValuePair<string, string> kvp in DetectionCategories)
            {
                if (kvp.Key == "0") continue; // Skip empty
                DetectionSelections.DetectionCategory = kvp.Key;
                string categoryName = kvp.Value;
                this.SetDetectionCountForCategory(categoryName, -1);
            }
        }
        #endregion

        #region Setting and ClearingClassificationCountsCollection
        // Given a category and a count, add or update it in the DetectionCountsCollection
        private void SetClassificationCountForCategory(string category, int count)
        {
            var categoryCount = this.ClassificationCountsCollection.FirstOrDefault(i => i.Category == category);
            if (null == categoryCount)
            {
                Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    CategoryCount cc = new CategoryCount(category, count);
                    this.ClassificationCountsCollection.Add(cc);
                    cc.NotifyPropertyChanged("Count");
                });
            }
            else
            {
                categoryCount.Count = count;
                categoryCount.NotifyPropertyChanged("Count");
            }
        }

        // Clear the counts for each classification category held in theclassificationCounts
        // This will also create and entry for each classification category if it doesn't already exist.
        private void ClearCountsInClassificationCountsCollection()
        {
            // Classification: clear the count in Category 
            foreach (KeyValuePair<string, string> kvp in ClassificationCategories)
            {
                DetectionSelections.ClassificationCategory = kvp.Key;
                string categoryName = kvp.Value;
                this.SetClassificationCountForCategory(categoryName, -1);
            }
        }
        #endregion

        #region Class CategoryCount defines an element containing a detection category and its current count
        public class CategoryCount : INotifyPropertyChanged
        {

            public int Count { get; set; }
            public string Category { get; set; }
            public CategoryCount(string category, int count)
            {
                this.Category = category;
                this.Count = count;
            }

            public event PropertyChangedEventHandler PropertyChanged;
            public void NotifyPropertyChanged(string propName)
            {
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            }
        }
        #endregion

        #region Class CurrentSelection
        public class CurrentSelection
        {
            public bool IsRecognitionSelected { get; set; } = false;
            public RecognitionTypeEnum RecognitionType { get; set; }
            // Reminder: A Category of "0" is empty; and "-1" is All
            public string CategoryName { get; set; }
            public string CategoryNumber { get; set; }

            public CurrentSelection()
            {
                this.IsRecognitionSelected = false;
                this.RecognitionType = RecognitionTypeEnum.None;
                this.CategoryName = string.Empty;
                this.CategoryNumber = string.Empty;
            }
        }
        #endregion

        #region Enum RecognitionTypeEnum
        public enum RecognitionTypeEnum
        {
            Detections,
            Classifications,
            DetectionsAndClassifications,
            None
        }
        #endregion
    }
}
