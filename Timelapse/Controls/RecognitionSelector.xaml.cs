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
using System.Windows.Input;
using Timelapse.Database;
using Timelapse.Enums;
using Timelapse.SearchingAndSorting;
using Timelapse.Recognition;
using System.Windows.Media;
using Timelapse.Extensions;
using Xceed.Wpf.Toolkit;
using Application = System.Windows.Application;
using Timelapse.EventArguments;


namespace Timelapse.Controls
{
    //
    // TODO If a setting is changed in Custom Selection, we need to reset everything. Maybe a method to invoke that?
    // TODO Sort by recognition or classification confidence as radio button
    // TODO Sort by recognition or classification confidence: wire it up
    // TODO SendRecognitionSelectionEvent() - likely easier by setting variables vs calculating it all again
    // TODO Unselect classifications i.e. to select only a detection
    // TODO Do we need file counts here (at far right)?
    // TODO Show only files the recognizer did not process
    // TODO Do classification numbers constrained by the detection confidence
    // TODO Raise Selection Events so they can be seen by CustomSelection 
    // TODO CustomSelection turn of timer count for total files when counts are being done (maybe? unsure if I have to) 
    // TODO RankByConfidence_CheckedChanged(object sender, RoutedEventArgs e)
    // TODO ShowMissingDetectionsCheckbox_CheckedChanged

    /// <summary>
    /// Control for displaying and selecting detections and classification recognitions
    /// It display Detections categories along with a count.
    /// When a detection category is selected, its displays the classification categories along with a count for that selected detection.
    /// Auxiliary controls are included to let a user sort by either detection or classification confidence.
    /// The general way it works is that
    /// - the current selection parameters are copied saved
    /// - queries are done on by altering the copied parameters to generate and display the categories and count
    /// - the current selection parameters are restored
    /// - actually selecting categories and classifications (and/or associated recognition controls) updates the current selection parameters 
    /// </summary>
    public partial class RecognitionSelector
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
        public RecognitionSelector()
        {
            InitializeComponent();
            // So we can access the database and the various custom selection parameters
            this.Database = GlobalReferences.MainWindow.DataHandler?.FileDatabase;
            this.CustomSelection = Database?.CustomSelection;
            this.DetectionSelections = Database?.CustomSelection?.DetectionSelections;
        }

        private async void RecognitionsSelector_OnLoaded(object sender, RoutedEventArgs e)
        {
            // Alter the default look of  Datagrid
            SetDataGridCategoryCountLook(this.DataGridDetections);
            SetDataGridCategoryCountLook(this.DataGridClassifications);

            //Abort if there is nothing to show
            if (this.Database == null)
            {
                this.DisableAllControls();
                return;
            }
            if (GlobalReferences.DetectionsExists == false)
            {
                this.DisableAllControls();
                return;
            }

            // If we make it here, recognitions are available for this image set

            // Ensure the detection categories are populated
            // - abort if there are none (likely a problem with the json file?)
            this.Database.CreateDetectionCategoriesDictionaryIfNeeded();
            this.DetectionCategories = Database.detectionCategoriesDictionary;
            if (null == this.DetectionCategories || this.DetectionCategories.Count == 0)
            {
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
            SliderDetectionConf.LowerValue = this.UseRecognitions && this.RecognitionType == RecognitionType.Detection
                ? Math.Round(DetectionSelections.ConfidenceDetectionThresholdLowerForUI, 2)
                : Math.Round(Database.GetTypicalDetectionThreshold(), 2);

            SliderDetectionConf.HigherValue = this.UseRecognitions && this.RecognitionType == RecognitionType.Detection
                ? Math.Round(this.ConfidenceThreshold2ForUI, 2)
                : 1;

            this.DetectionSelections.ConfidenceDetectionThresholdUpperForUI = SliderDetectionConf.HigherValue;
            this.DisplayDetectionConfidenceRange(Math.Round(SliderDetectionConf.LowerValue, 2));

            // Clear the counts for each detection category held in the DetectionCounts
            // This will also create and entry for each detection category if it doesn't already exist.
            this.ClearCountsInDetectionCountsCollection();

            // Classifications
            // - Display the current Confidence range in the classifications title
            // - Retrieve the classification categories
            // - Generate a count for each category, splitting them into two lists
            SliderClassificationConf.LowerValue = this.UseRecognitions && this.RecognitionType == RecognitionType.Classification
                ? Math.Round(this.ConfidenceThreshold1ForUI, 2)
                : Math.Round(Database.GetTypicalClassificationThreshold(), 2);
            SliderClassificationConf.HigherValue = this.UseRecognitions && this.RecognitionType == RecognitionType.Classification
                ? Math.Round(this.ConfidenceThreshold2ForUI, 2)
                : 1;
            DisplayClassificationConfidenceRange(Math.Round(SliderClassificationConf.LowerValue, 2), Math.Round(SliderClassificationConf.HigherValue, 2));
            this.Database.CreateClassificationCategoriesDictionaryIfNeeded();
            this.ClassificationCategories = this.Database.classificationCategoriesDictionary;

            this.ParametersIntializeAsDetections();

            // Count and display the total number of files in the current selection with recognitions turned off
            int totalFiles = DoCountFilesInCurrentSelectionWithRecognitionsOff(); // database.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.Custom);
            //this.TBTotalFiles.Text = $"{totalFiles}";

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

            // Generate the counts, while keeping the current selection (if any) highlit
            this.TryHighlightSelection();
            await this.DoCountRecognitionsAsync(new CancellationTokenSource(), recognitionTypeEnum);
            this.TryHighlightSelection();
        }
        #endregion

        #region Highlight selection
        // Try to highlight the currently selected item, if possible
        private void TryHighlightSelection()
        {
            if (this.UseRecognitions == false)
            {
                return;
            }

            DataGrid dataGrid = null;
            ObservableCollection<CategoryCount> countsCollection = null;
            Dictionary<string, string> categoryDictionary = null;
            string selectedCategoryNumber = string.Empty;

            // Initialize the above depending upon whether its a detection or classification
            if (this.RecognitionType == RecognitionType.Detection && null != this.DetectionCategories)
            {
                // Detection
                dataGrid = this.DataGridDetections;
                countsCollection = this.DetectionCountsCollection;
                categoryDictionary = this.DetectionCategories;
                selectedCategoryNumber = this.AllDetections ? string.Empty : this.DetectionCategory;
            }
            else if (this.RecognitionType == RecognitionType.Classification && null != this.ClassificationCategories)
            {
                // Classification
                dataGrid = this.DataGridClassifications;
                countsCollection = this.ClassificationCountsCollection;
                categoryDictionary = this.ClassificationCategories;
                selectedCategoryNumber = this.ClassificationCategory;
            }

            if (null == dataGrid || countsCollection == null || categoryDictionary == null)
            {
                // Abort if we can't do anything
                return;
            }

            // Get the actual category name from its category number
            categoryDictionary.TryGetValue(selectedCategoryNumber, out string selectedCategoryName);
            if (selectedCategoryName == null)
            {
                // Special case to interpret all and empty categories 
                if (this.AllDetections && this.InterpretAllDetectionsAsEmpty)
                {
                    selectedCategoryName = Constant.RecognizerValues.NoDetectionLabel;

                }
                else if (this.AllDetections && false == this.InterpretAllDetectionsAsEmpty)
                {
                    selectedCategoryName = Constant.RecognizerValues.AllDetectionLabel;
                }
                else
                {
                    return;
                }
            }

            // Get the category count item matching the category name
            CategoryCount categoryCount = countsCollection.FirstOrDefault(x => x.Category == selectedCategoryName);
            if (categoryCount == null)
            {
                return;
            }

            // Finally, select that item in the data grid, making sure its visible and highlit
            dataGrid.SelectedItem = categoryCount;
            dataGrid.ScrollIntoView(categoryCount);
            dataGrid.Focus();
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
            double lowerDetectionConf = Math.Round(this.SliderDetectionConf.LowerValue, 2);
            double higherDetectionConf = Math.Round(this.SliderDetectionConf.HigherValue, 2);
            double lowerClassificationConf = Math.Round(this.SliderClassificationConf.LowerValue, 2);
            double higherClassificationConf = Math.Round(this.SliderClassificationConf.HigherValue, 2);

            await Task.Run(() =>
            {
                bool success = false;
                if (recognitionType == RecognitionTypeEnum.Detections || recognitionType == RecognitionTypeEnum.DetectionsAndClassifications)
                {

                    success = DoCountDetections(cancellationTokenSource, lowerDetectionConf, higherDetectionConf);
                    if (false == success)
                    {
                        return;
                    }
                }

                if (recognitionType == RecognitionTypeEnum.Classifications || recognitionType == RecognitionTypeEnum.DetectionsAndClassifications)
                {

                    success = DoCountClassifications(cancellationTokenSource, lowerClassificationConf, higherClassificationConf);
                }
                // XXXX ADDED TEST THIS SEEMS TO APPROX DO THE TRICK BUT NEED TO CHECK
                // XXXX LIKELY DOESN"T DO THE SELECTION PROPERLY
                // NOTE SQL ERROR ON CLASSIFICATIONS
                // this.ParametersRestoreOriginalRecognitions();
            }, cancellationTokenSource.Token);
            Mouse.OverrideCursor = null;
        }

        private bool DoCountDetections(CancellationTokenSource cancellationTokenSource, double lowerConfidenceValue, double higherConfidenceValue)
        {
            if (cancellationTokenSource.Token.IsCancellationRequested)
            {
                return false;
            }

            // All category count 
            DetectionSelections.RecognitionType = RecognitionType.Detection;
            DetectionSelections.AllDetections = true;
            DetectionSelections.InterpretAllDetectionsAsEmpty = false;
            this.DetectionSelections.ConfidenceDetectionThresholdLowerForUI = lowerConfidenceValue;
            this.DetectionSelections.ConfidenceDetectionThresholdUpperForUI = higherConfidenceValue;

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
        private bool DoCountClassifications(CancellationTokenSource cancellationTokenSource, double lowerConfidenceValue, double higherConfidenceValue)
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

            // Set search criteria to classifications
            DetectionSelections.InterpretAllDetectionsAsEmpty = false;
            DetectionSelections.RecognitionType = RecognitionType.Classification;
            this.DetectionSelections.ConfidenceDetectionThresholdLowerForUI = lowerConfidenceValue;
            this.DetectionSelections.ConfidenceDetectionThresholdUpperForUI = higherConfidenceValue;

            // Initialize by clearing the various lists
            this.ClearCountsInClassificationCountsCollection();
            this.ClearClassificationEmptyCountsListAndUpdateListBox();

            // For each category, generate the count and update the appropriate lists
            foreach (KeyValuePair<string, string> kvp in this.ClassificationCategories)
            {
                // If cancelled, enable
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

                // Get the count for the current classification category
                int distinctCount = Database.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.Custom);

                // Add the count to each category, where counts will appear incrementally on the DataGrid (if non-0) in sorted order
                // (0 entries are collapsed in the style)

                this.SetClassificationCountForCategory(categoryName, distinctCount);
                if (this.DataGridClassifications.Columns.Count > 0)
                {
                    Application.Current.Dispatcher.Invoke((Action)delegate
                    {

                        SortDataGrid(this.DataGridClassifications, 0, ListSortDirection.Descending);
                    });
                }

                if (distinctCount == 0)
                {
                    // 0 count: Add the category to the Listbox
                    this.AddClassificationEmptyCountsListAndUpdateListBox(categoryName);
                }
            }

            // Done! Enable the controls 
            Application.Current.Dispatcher.Invoke((Action)delegate
            {
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
                //this.TBSelectionFeedback.Text = "No recognition selected.";
                //this.OkButton.IsEnabled = false;
                return;
            }
            //this.OkButton.IsEnabled = true;

            // Category name (1st letter in caps)
            string msg = CurrentlySelectedRecognition.CategoryName.Length > 1
                ? CurrentlySelectedRecognition.CategoryName[0].ToString().ToUpper() + CurrentlySelectedRecognition.CategoryName.Substring(1)
                : CurrentlySelectedRecognition.CategoryName;
            if (CurrentlySelectedRecognition.RecognitionType == RecognitionTypeEnum.Detections)
            {
                if (Math.Abs(this.SliderDetectionConf.HigherValue - 1) < .009)
                {
                    msg += $" detections ≥ {this.SliderDetectionConf.LowerValue:f2}";
                }
                else
                {
                    msg += $" detections: {this.SliderDetectionConf.LowerValue:f2}\u2194{this.SliderDetectionConf.HigherValue:f2}";
                }
            }
            else
            {
                if (Math.Abs(this.SliderClassificationConf.HigherValue - 1) < .009)
                {
                    msg += $" classifications ≥ {this.SliderClassificationConf.LowerValue:f2}";
                }
                else
                {
                    msg += $" classifications: {this.SliderClassificationConf.LowerValue:f2}\u2194{this.SliderClassificationConf.HigherValue:f2}";
                }
            }
            //this.TBSelectionFeedback.Text = msg;
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
            this.TBDetectionsCount.Text = $"({lowerConfidence:f2} - {this.DetectionSelections.ConfidenceDetectionThresholdUpperForUI:f2})";
        }

        private void DisplayDetectionConfidenceRange(double lowerConfidence, double higherConfidence)
        {
            this.TBDetectionsCount.Text = $"({lowerConfidence:f2} - {higherConfidence:f2})";
        }

        private void DisplayClassificationConfidenceRange(double lowerConfidence, double higherConfidence)
        {
            this.TBClassificationsCount.Text = $"({lowerConfidence:f2} - {higherConfidence:f2})";
            //this.TBBelowClassificationValue.Text = $"(with 0 counts in the above range or absent)";
        }

        // Disable the various controls, usually because there is nothing to show
        private void DisableAllControls()
        {
            //this.TBTotalFiles.Text = string.Empty;
            this.DataGridDetections.IsEnabled = false;
            this.SliderDetectionConf.IsEnabled = false;
            this.TBDetectionsLabel.Foreground = Brushes.DarkGray;
            this.DisableClassificationControls();
        }

        private void DisableClassificationControls()
        {
            this.TBClassificationsLabel.Foreground = Brushes.DarkGray;
            //this.TBBelowClassificationsLabel.Foreground = Brushes.DarkGray;
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
            this.ConfidenceThreshold1ForUI = DetectionSelections.ConfidenceDetectionThresholdLowerForUI;
            this.ConfidenceThreshold2ForUI = DetectionSelections.ConfidenceDetectionThresholdUpperForUI;
            this.RecognitionType = DetectionSelections.RecognitionType;
            this.RankByConfidence = DetectionSelections.RankByDetectionConfidence;
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
                this.DetectionSelections.ConfidenceDetectionThresholdLowerForUI = this.ConfidenceThreshold1ForUI;
                this.DetectionSelections.ConfidenceDetectionThresholdUpperForUI = this.ConfidenceThreshold2ForUI;
                this.DetectionSelections.RecognitionType = this.RecognitionType;
                this.DetectionSelections.ClassificationCategory = this.ClassificationCategory;
                this.DetectionSelections.RankByDetectionConfidence = this.RankByConfidence;
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

            DetectionSelections.RankByDetectionConfidence = false;
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

                        SendRecognitionSelectionEvent();
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
                        SendRecognitionSelectionEvent();
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
                        SendRecognitionSelectionEvent();

                        return;
                    }
                }
                // Something went wrong. Feedback???
                this.CurrentlySelectedRecognition.IsRecognitionSelected = false;
                this.DisplayCurrentSelection();
            }
        }
        #endregion

        #region Slider Detection Confidence 
        // When the detection drag is in progress
        // - disable the detection controls
        // - display the updated slider value
        private bool isDetectionSliderMouseDown;
        private bool isDetectionValueChanged = false;

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
            // Abort if we can't do anything
            if (!(sender is RangeSlider slider) || null == this.DetectionCategories)
            {
                return;
            }

            // Abort if nothing has changed  in the current slider interaction, i.e., the value has not been changed previously
            // and there are no changes between the current slider values vs the current confidence values
            if (isDetectionValueChanged == false &&
                Math.Abs(Math.Round(slider.LowerValue, 2) - this.DetectionSelections.ConfidenceDetectionThresholdLowerForUI) < .01 &&
                Math.Abs(Math.Round(slider.HigherValue, 2) - this.DetectionSelections.ConfidenceDetectionThresholdUpperForUI) < .01)
            {
                return;
            }

            if (isDetectionSliderMouseDown)
            {
                // The user is likely in the midst of adjusting the slider values e.g., by dragging.
                // We only want to update the display as described below, but not actually do any counting
                // as that is an expensive operation.

                // Disable the detection datagrid including clearing the current selection and recognition
                this.DetectionControlsEnableState(false, true);
                this.ClearSelectionsAndScrollToTop(this.DataGridDetections);
                this.CurrentlySelectedRecognition.IsRecognitionSelected = false;

                // Show the current slider values 
                this.DisplayDetectionConfidenceRange(Math.Round(slider.LowerValue, 2), Math.Round(slider.HigherValue, 2));

                // Clear the current counts
                this.ClearCountsInDetectionCountsCollection();

                this.isDetectionValueChanged = true;
                return;
            }

            // The user has finished updating the sliders, so we want to both update the display 
            // and counts

            // Enable the detection datagrid 
            this.DetectionControlsEnableState(true, true);

            // Set and display the new confidence thresholds
            double lowerConf = Math.Round(slider.LowerValue, 2);
            double higherConf = Math.Round(slider.HigherValue, 2);
            this.DetectionSelections.ConfidenceDetectionThresholdLowerForUI = lowerConf;
            this.DetectionSelections.ConfidenceDetectionThresholdUpperForUI = higherConf;
            this.DisplayDetectionConfidenceRange(lowerConf, higherConf);

            // Clear the current counts and start counting the new ones
            this.ClearCountsInDetectionCountsCollection();
            this.isDetectionValueChanged = false;

            // Recount classifications. 
            // Note that we don't clear counts as we are rebuilding the entire list
            // Cancel the last operation, then reset the token for the next operation
            this.TokenSource.Cancel();
            this.TokenSource = new CancellationTokenSource();
            await this.DoCountRecognitionsAsync(new CancellationTokenSource(), RecognitionTypeEnum.Detections);
        }
        #endregion

        #region Slider Classification Confidence Callbacks
        // Classification slider -
        // When the classification drag is in progress
        // - disable the classification controls
        // - display the updated slider value
        private bool isClassificationSliderMouseDown;
        private bool isClassificationValueChanged = false;
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
        }
        private async void SliderClassificationConf_ValueChanged(object sender, RoutedEventArgs e)
        {
            // Abort if we can't do anything
            if (!(sender is RangeSlider slider) || null == this.DetectionCategories)
            {
                return;
            }

            // Abort if nothing has changed  in the current slider interaction, i.e., the value has not been changed previously
            // and there are no changes between the current slider values vs the current confidence values
            if (isClassificationValueChanged == false &&
                Math.Abs(Math.Round(slider.LowerValue, 2) - this.DetectionSelections.ConfidenceDetectionThresholdLowerForUI) < .01 &&
                Math.Abs(Math.Round(slider.HigherValue, 2) - this.DetectionSelections.ConfidenceDetectionThresholdUpperForUI) < .01)
            {
                return;
            }

            if (isClassificationSliderMouseDown)
            {
                // The user is likely in the midst of adjusting the slider values e.g., by dragging.
                // We only want to update the display as described below, but not actually do any counting
                // as that is an expensive operation.

                // Disable the detection datagrid including clearing the current selection and recognition
                this.ClassificationControlsEnableState(false, true);
                this.ClearSelectionsAndScrollToTop(this.DataGridClassifications);
                this.CurrentlySelectedRecognition.IsRecognitionSelected = false;

                // Show the current slider values 
                this.DisplayClassificationConfidenceRange(Math.Round(slider.LowerValue, 2), Math.Round(slider.HigherValue, 2));

                // Clear the current counts
                this.ClearCountsInClassificationCountsCollection();
                this.ClearClassificationEmptyCountsListAndUpdateListBox();
                this.isClassificationValueChanged = true;
                return;
            }

            // The user has finished updating the sliders, so we want to both update the display 
            // and counts

            // Enable the detection datagrid 
            this.ClassificationControlsEnableState(true, true);

            // Set and display the new confidence thresholds
            double lowerConf = Math.Round(slider.LowerValue, 2);
            double higherConf = Math.Round(slider.HigherValue, 2);
            this.DetectionSelections.ConfidenceDetectionThresholdLowerForUI = lowerConf;
            this.DetectionSelections.ConfidenceDetectionThresholdUpperForUI = higherConf;
            this.DisplayClassificationConfidenceRange(lowerConf, higherConf);

            // Clear the current counts and start counting the new ones
            this.ClearCountsInClassificationCountsCollection();
            this.ClearClassificationEmptyCountsListAndUpdateListBox();
            this.isClassificationValueChanged = false;

            // Recount classifications. 
            // Note that we don't clear counts as we are rebuilding the entire list
            // Cancel the last operation, then reset the token for the next operation
            this.TokenSource.Cancel();
            this.TokenSource = new CancellationTokenSource();
            await this.DoCountRecognitionsAsync(new CancellationTokenSource(), RecognitionTypeEnum.Classifications);
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
                        // Detections: All
                        DetectionSelections.AllDetections = true;
                        DetectionSelections.InterpretAllDetectionsAsEmpty = false;
                        DetectionSelections.RecognitionType = RecognitionType.Detection;
                        this.DetectionSelections.ConfidenceDetectionThresholdLowerForUI = this.SliderDetectionConf.LowerValue;
                        this.DetectionSelections.ConfidenceDetectionThresholdUpperForUI = this.SliderDetectionConf.HigherValue;
                        this.CustomSelection.ShowMissingDetections = false;
                        //this.DialogResult = true;
                        return;
                    }

                    if (this.CurrentlySelectedRecognition.CategoryNumber == "0")
                    {
                        // Detections: Empty
                        DetectionSelections.AllDetections = true;
                        DetectionSelections.InterpretAllDetectionsAsEmpty = true;
                        DetectionSelections.RecognitionType = RecognitionType.Detection;
                        this.DetectionSelections.DetectionCategory = this.CurrentlySelectedRecognition.CategoryNumber;
                        this.DetectionSelections.ConfidenceDetectionThresholdLowerForUI = this.SliderDetectionConf.LowerValue;
                        this.DetectionSelections.ConfidenceDetectionThresholdUpperForUI = this.SliderDetectionConf.HigherValue;
                        this.CustomSelection.ShowMissingDetections = false;
                        //this.DialogResult = true;
                        return;
                    }

                    // Detections: A non-Empty, non-All category
                    DetectionSelections.AllDetections = false;
                    DetectionSelections.InterpretAllDetectionsAsEmpty = false;
                    DetectionSelections.RecognitionType = RecognitionType.Detection;
                    this.DetectionSelections.DetectionCategory = this.CurrentlySelectedRecognition.CategoryNumber;
                    this.DetectionSelections.ConfidenceDetectionThresholdLowerForUI = this.SliderDetectionConf.LowerValue;
                    this.DetectionSelections.ConfidenceDetectionThresholdUpperForUI = this.SliderDetectionConf.HigherValue;
                    this.CustomSelection.ShowMissingDetections = false;
                    return;
                }

                if (this.CurrentlySelectedRecognition.RecognitionType == RecognitionTypeEnum.Classifications)
                {
                    // Classification
                    DetectionSelections.AllDetections = false;
                    DetectionSelections.InterpretAllDetectionsAsEmpty = false;

                    DetectionSelections.RecognitionType = RecognitionType.Classification;
                    this.DetectionSelections.ClassificationCategory = this.CurrentlySelectedRecognition.CategoryNumber;
                    this.DetectionSelections.ConfidenceDetectionThresholdLowerForUI = this.SliderClassificationConf.LowerValue;
                    this.DetectionSelections.ConfidenceDetectionThresholdUpperForUI = this.SliderClassificationConf.HigherValue;
                    this.CustomSelection.ShowMissingDetections = false;
                    //this.DialogResult = true;
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.TokenSource.Cancel();
            this.ParametersRestoreOriginalRecognitions();
            //this.DialogResult = false;
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
            if (ClassificationCategories == null)
            {
                return;
            }
            // Classification: clear the count in Category 
            foreach (KeyValuePair<string, string> kvp in ClassificationCategories)
            {
                DetectionSelections.ClassificationCategory = kvp.Key;
                string categoryName = kvp.Value;
                this.SetClassificationCountForCategory(categoryName, 0);
            }
        }

        private void ClearClassificationEmptyCountsListAndUpdateListBox()
        {
            if (ClassificationEmptyCountsList == null)
            {
                return;
            }
            ClassificationEmptyCountsList.Clear();

            // There's probably a more efficient way to do this, but its works 
            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                LBEmptyClassifications.ItemsSource = null;
                LBEmptyClassifications.ItemsSource = ClassificationEmptyCountsList;
                this.DropDownEmptyLabel.Text = $"({ClassificationEmptyCountsList.Count} items)";
            });
        }
        private void AddClassificationEmptyCountsListAndUpdateListBox(string categoryName)
        {
            if (this.ClassificationEmptyCountsList == null)
            {
                return;
            }
            this.ClassificationEmptyCountsList.Add(categoryName);

            // There's probably a more elegant way to do this via Notifies, but its works 
            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                this.LBEmptyClassifications.ItemsSource = null;
                this.LBEmptyClassifications.ItemsSource = ClassificationEmptyCountsList;
                this.DropDownEmptyLabel.Text = $"({ClassificationEmptyCountsList.Count} items)";
            });
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

        #region Custom Selection Event
        public event EventHandler<RecognitionSelectionChangedEventArgs> RecognitionSelectionEvent;

        private void SendRecognitionSelectionEvent()
        {
            string detectionCategory = string.Empty;
            string detectionNumber = string.Empty;
            string classificationCategory = string.Empty;
            string classificationNumber = string.Empty;

            // Get the current Detection selection, if any
            if (this.DataGridDetections.SelectedItems.Count == 1 && this.DataGridDetections.SelectedItems[0] is CategoryCount categoryCountDetections)
            {
                if (categoryCountDetections.Category == Constant.RecognizerValues.AllDetectionLabel)
                {
                    detectionCategory = categoryCountDetections.Category;
                    detectionNumber = this.AllCategoryNumber;
                }
                else
                {
                    // Get the category number from its name
                    string categoryNumber = string.Empty;
                    foreach (KeyValuePair<string, string> kvp in this.DetectionCategories)
                    {
                        if (EqualityComparer<string>.Default.Equals(kvp.Value, categoryCountDetections.Category))
                        {
                            categoryNumber = kvp.Key;
                            break;
                        }
                    }

                    if (categoryNumber != string.Empty)
                    {
                        detectionCategory = categoryCountDetections.Category;
                        detectionNumber = categoryNumber;
                    }
                }
            }

            // Get the current Classification selection, if any
            if (DataGridClassifications.SelectedItems.Count == 1 && DataGridClassifications.SelectedItems[0] is CategoryCount categoryCountClassifications)
            {
                // The user selected a category (which could include empty)
                string categoryNumber = string.Empty;

                // Get the category number from its name
                foreach (KeyValuePair<string, string> kvp in this.ClassificationCategories)
                {
                    if (EqualityComparer<string>.Default.Equals(kvp.Value, categoryCountClassifications.Category))
                    {
                        categoryNumber = kvp.Key;
                        break;
                    }
                }

                if (categoryNumber != string.Empty)
                {
                    classificationCategory = categoryCountClassifications.Category;
                    classificationNumber = categoryNumber;
                }
            }

            // Send the event
            // Note that this could include
            // - detections only
            // - detections and classifications
            // - no selections
            // It should not include classifications only
            RecognitionSelectionChangedEventArgs e = new RecognitionSelectionChangedEventArgs(
                detectionCategory,
                detectionNumber,
                classificationCategory,
                classificationNumber);
            RecognitionSelectionEvent?.Invoke(this, e);
        }
        #endregion

        private void RankByConfidence_CheckedChanged(object sender, RoutedEventArgs e)
        {

        }

        private void ShowMissingDetectionsCheckbox_CheckedChanged(object sender, RoutedEventArgs e)
        {

        }


    }
}
