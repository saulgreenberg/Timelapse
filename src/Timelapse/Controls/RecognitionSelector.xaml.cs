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
using TimelapseWpf.Toolkit;
using Application = System.Windows.Application;
using Timelapse.EventArguments;
using Timelapse.Constant;
using Cursors = System.Windows.Input.Cursors;
using DataGrid = System.Windows.Controls.DataGrid;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using static System.FormattableString;

namespace Timelapse.Controls
{
    // TODO Conf levels not saved between sessions
    // TODO add AutoUpdate checkbox? for selection and slider only?
    // TODO Problems Saving/Restoring State?
    // TODO Implement Show only files the recognizer did not process
    // TODO Implement RankByConfidence_CheckedChanged(object sender, RoutedEventArgs e)
    // TODO Implement ShowMissingDetectionsCheckbox_CheckedChanged
    // TODO SQL select files
    // TODO Category numbers on import and merge

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

        private readonly int? NoValue = null;

        // Dictionaries that will eventually hold the Detection and Classification categories  
        private Dictionary<string, string> DetectionCategories;
        private Dictionary<string, string> ClassificationCategories;
        private Dictionary<string, string> ClassificationDescriptions;

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
        private CategoryCount savedSelectedCategoryCount;

        // To hold passed in constructor arguments, used to set the busy state and to use the progress indicator
        private readonly BusyableDialogWindow Owner;
        private readonly BusyCancelIndicator BusyCancelIndicator;
        #endregion

        #region Counting
        // An externally invoked method by the parent to refresh the counts
        private async Task RecognitionsRefreshCounts()
        {
            this.ClearCountsAndResetUI();

            // Counting can be long-running, so we want to make it a cancellable operation
            this.BusyCancelIndicator.IsBusy = true;
            this.RecognitionSelectionsSaveState();
            bool allCountsCompleted = await this.DoCountRecognitionsAsync(true, this.classificationsExist);
            if (false == allCountsCompleted)
            {
                this.ClearCountsAndResetUI();
            }
            else
            {
                this.BtnCountRecognitions.IsEnabled = false;
            }
            this.RecognitionSelectionsRestoreState();
            this.TryHighlightCurrentSelection();
            this.BusyCancelIndicator.IsBusy = false;
            this.onlyUpdateClassificationCount = false;
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
                    if (countDetections && (this.onlyUpdateClassificationCount == false || this.DetectionCountsCollectionHasCounts() == false))
                    {
                        string savedClassificationCategoryNumber = this.RecognitionSelections.ClassificationCategoryNumber;
                        this.RecognitionSelections.ClassificationCategoryNumber = string.Empty;
                        allCountsCompleted = DoCountDetections(lowerDetectionConf, higherDetectionConf);
                        this.RecognitionSelections.ClassificationCategoryNumber = savedClassificationCategoryNumber;
                        if (!allCountsCompleted)
                        {
                            return false;
                        }
                    }

                    if (countClassifications)
                    {
                        allCountsCompleted = DoCountClassifications(lowerClassificationConf, higherClassificationConf);
                        if (!allCountsCompleted)
                        {
                            return false;
                        }
                    }
                    return true;
                });

                return isCompletelyCompleted;
            }
            catch (TaskCanceledException)
            {
                // The task was cancelled. Restore everything
                // Note that we have to reset the token so we can cancel subsequent counts
                this.Owner.TokenReset();
                return false;
            }
        }

        private bool DoCountDetections(double lowerConfidenceValue, double higherConfidenceValue)
        {
            // Set the confidence bounds
            this.RecognitionSelections.DetectionConfidenceLowerForUI = lowerConfidenceValue;
            this.RecognitionSelections.DetectionConfidenceHigherForUI = higherConfidenceValue;

            // All category count 
            // Check if cancelled, and/or show progress
            this.ShowProgressOrAbortIfCancelled(true, Constant.RecognizerValues.AllDetectionLabel);

            // Set parameters to count the All state
            RecognitionSelections.AllDetections = true;
            RecognitionSelections.InterpretAllDetectionsAsEmpty = false;

            // Do the count and update the display
            int allFilesCount = Database.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.Custom);
            this.SetDetectionCountForCategory(Constant.RecognizerValues.AllDetectionLabel, allFilesCount);

            // Empty category count 
            // Check if cancelled, and/or show progress
            this.ShowProgressOrAbortIfCancelled(true, Constant.RecognizerValues.EmptyDetectionLabel);

            // Set parameters to count the Empty state  
            RecognitionSelections.InterpretAllDetectionsAsEmpty = true;
            int emptyFilesCount = Database.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.Custom);
            this.SetDetectionCountForCategory(Constant.RecognizerValues.EmptyDetectionLabel, emptyFilesCount);


            // Individual detection category count 
            RecognitionSelections.InterpretAllDetectionsAsEmpty = false;
            RecognitionSelections.AllDetections = false;
            foreach (KeyValuePair<string, string> kvp in DetectionCategories)
            {
                if (kvp.Key == "0") continue; // Skip empty

                // Check if cancelled, and/or show progress
                this.ShowProgressOrAbortIfCancelled(true, kvp.Value);

                // Set parameters to count this individual
                RecognitionSelections.DetectionCategoryNumber = kvp.Key;

                // Do the count and update the display
                int distinctCount = Database.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.Custom);
                string categoryName = kvp.Value;
                this.SetDetectionCountForCategory(categoryName, distinctCount);
            }

            return true;
        }
        private bool DoCountClassifications(double lowerConfidenceValue, double higherConfidenceValue)
        {
            // Abort if there are no classifications
            if (this.ClassificationCategories == null || this.ClassificationCategories.Count == 0 || this.Owner.Token.IsCancellationRequested)
            {
                return false;
            }

            // Set search criteria to classifications
            RecognitionSelections.InterpretAllDetectionsAsEmpty = false;
            RecognitionSelections.AllDetections = true;
            this.RecognitionSelections.ClassificationConfidenceLowerForUI = lowerConfidenceValue;
            this.RecognitionSelections.ClassificationConfidenceHigherForUI = higherConfidenceValue;

            // Initialize by clearing the various lists
            this.ClearClassificationCounts();

            // For each category, generate the count and update the appropriate lists
            foreach (KeyValuePair<string, string> kvp in this.ClassificationCategories)
            {
                this.ShowProgressOrAbortIfCancelled(false, kvp.Value);

                // Set parameters to count this classification
                string categoryNumber = kvp.Key;
                RecognitionSelections.ClassificationCategoryNumber = categoryNumber;
                string categoryName = kvp.Value;

                // Do the count and update the display
                int distinctCount = Database.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.Custom);
                this.SetClassificationCountForCategory(categoryName, distinctCount);

                // Sort the datagrid by its count
                if (this.DataGridClassifications.Columns.Count > 0)
                {
                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        SortDataGrid(this.DataGridClassifications, 0, ListSortDirection.Descending);
                        this.DataGridClassifications.ScrollIntoViewFirstRow();
                    });
                }
            }

            return true;
        }
        #endregion

        #region Button Callbacks - BtnCountRecognitions - OnClick
        private async void BtnCountRecognitions_OnClick(object sender, RoutedEventArgs e)
        {
            await this.RecognitionsRefreshCounts();
        }
        #endregion

        #region Checkbox Callbacks - RankByConfidence, ShowMissingDetections
        private void RankByConfidence_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton)
            {
                this.RecognitionSelections.RankByDetectionConfidence = RankByDetectionConfidenceCheckbox.IsChecked == true;
                this.RecognitionSelections.RankByClassificationConfidence = RankByClassificationConfidenceCheckbox.IsChecked == true;
            }

            bool enableState = false == this.RecognitionSelections.RankByDetectionConfidence &&
                               false == RecognitionSelections.RankByClassificationConfidence;
            {
                // Disable controls
                SlidersEnableState(enableState);
            }
            // The Empty category will only show Empty when a Ranking checkbox is checked
            this.SetEmptyDetectionCategoryLabel();

            // Reset rank by classification if needed
            this.EnableDisableRankByClassificationCheckbox(!string.IsNullOrEmpty(this.RecognitionSelections.ClassificationCategoryNumber));

            // Send a recognition selection event to the parent
            this.SendRecognitionSelectionEvent(true);
        }

        private void ShowMissingDetectionsCheckbox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            this.EnableOrDisableAllControls(ShowMissingDetectionsCheckbox.IsChecked == false, false, true);
            this.Database.CustomSelection.ShowMissingDetections = ShowMissingDetectionsCheckbox.IsChecked == true;
            // Send a recognition selection event to the parent
            this.SendRecognitionSelectionEvent(false);
        }

        // Disable the classification radio button if no classification is selected and switches Rank to None
        // as we shouldn't be sorting by classifications
        private void EnableDisableRankByClassificationCheckbox(bool isClassificationSelected)
        {
            this.RankByClassificationConfidenceCheckbox.IsEnabled = isClassificationSelected;
            if (false == isClassificationSelected && true == this.RankByClassificationConfidenceCheckbox.IsChecked)
            {
                this.RankByNoneCheckbox.IsChecked = true;
            }
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
        private void SliderDetectionConf_ValueChanged(object sender, RoutedEventArgs e)
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

            // The CountRecogntions button is enabled so that the user can recount recogntions
            this.BtnCountRecognitions.IsEnabled = true;

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
        private void SliderClassificationConf_ValueChanged(object sender, RoutedEventArgs e)
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
            this.BtnCountRecognitions.IsEnabled = true;

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

            // Send a recognition selection event to the parent
            this.SendRecognitionSelectionEvent(true);
        }
        #endregion

        #region DataGrid Callbacks - OnSelectionChanged 
        private void DataGridDetections_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.DoDataGridDetections_OnSelectionChanged(sender);
        }

        private void DoDataGridDetections_OnSelectionChanged(object sender)
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

                    // Unselect classifications when the Detection category is not All
                    this.ignoreSelection = true;
                    this.DataGridClassifications.SelectedItem = null;
                    this.EnableDisableRankByClassificationCheckbox(false);
                    this.ignoreSelection = false;

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
                string categoryNumber = GetCategoryNumberFromCategoryName(DetectionCategories, selectedCategory);
                if (categoryNumber != string.Empty)
                {
                    // Set it to the selected category
                    this.RecognitionSelections.DetectionCategoryNumber = categoryNumber;
                    if (selectedCategory != Constant.RecognizerValues.AllDetectionLabel)
                    {
                        // Unselect classifications when the Detection category is not All
                        this.ignoreSelection = true;
                        this.DataGridClassifications.SelectedItem = null;
                        this.EnableDisableRankByClassificationCheckbox(false);
                        this.ignoreSelection = false;
                    }

                    // Send event, but don't redo the count on recognitions
                    this.SendRecognitionSelectionEvent(false);
                }
            }
        }

        // The classification selection has changed
        // 
        private void DataGridClassifications_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.ignoreSelection)
            {
                return;
            }

            if (sender is DataGrid { SelectedItems: [CategoryCount categoryCount] })
            {
                // The user selected a category
                string categoryNumber = GetCategoryNumberFromCategoryName(this.ClassificationCategories, categoryCount.Category);

                if (categoryNumber != string.Empty)
                {
                    // Set the Classification Category to the selected entity
                    this.RecognitionSelections.ClassificationCategoryNumber = categoryNumber;
                    this.EnableDisableRankByClassificationCheckbox(true);

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
            RecognitionSelectionChangedEventArgs e = new(detectionCategoryLabel, classificationCategoryLabel, refreshRecognitionCountsRequired);
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

        #region --------------------------DONE------------------
        // DONE
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
        }
        #endregion

        #region OnLoaded
        private void RecognitionsSelector_OnLoaded(object sender, RoutedEventArgs e)
        {
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

            // Set the rank by confidence checkboxes to their initial value
            if (this.RecognitionSelections.RankByDetectionConfidence)
            {
                this.RankByDetectionConfidenceCheckbox.IsChecked = true;
            }
            else if (this.RecognitionSelections.RankByClassificationConfidence)
            {
                this.RankByClassificationConfidenceCheckbox.IsChecked = true;
            }
            else
            {
                this.RankByNoneCheckbox.IsChecked = true;
            }

            if (false == this.classificationsExist)
            {
                this.RankByClassificationConfidenceCheckbox.Visibility = Visibility.Collapsed;
                if (this.RankByClassificationConfidenceCheckbox.IsChecked == true)
                {
                    // We can't rank by classification if there are none!
                    this.RankByNoneCheckbox.IsChecked = true;
                }
            }

            // Set the show missing detections checkbox to its initial value
            this.ShowMissingDetectionsCheckbox.IsChecked = this.CustomSelection.ShowMissingDetections;

            this.TryHighlightCurrentSelection();
            this.SendRecognitionSelectionEvent(false);
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
            if (sender is DataGridRow row && this.ClassificationDescriptions.Count > 0)
            {
                if (row.Item is CategoryCount cc)
                {
                    if (false == string.IsNullOrWhiteSpace(cc.Category) ) 
                    {
                        string categoryNumber = GetCategoryNumberFromCategoryName(this.ClassificationCategories, cc.Category);
                        if (this.ClassificationDescriptions.TryGetValue(categoryNumber, out string description) && false == string.IsNullOrWhiteSpace(description))
                        {
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
                    }
                }
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
            this.RankByDetectionConfidenceCheckbox.IsEnabled = enableAllControls;
            this.RankByClassificationConfidenceCheckbox.IsEnabled = enableAllControls;
            this.RankByNoneCheckbox.IsEnabled = enableAllControls;
            this.ShowMissingDetectionsCheckbox.IsEnabled = enableShowMissingDetectionsCheckbox || enableAllControls;

            // Adjust all label colors i.e., disabled is gray, enabled is black
            Brush labelColor = enableAllControls ? Brushes.Black : Brushes.DarkGray;
            this.TBDetectionsLabel.Foreground = labelColor;
            this.TBClassificationsLabel.Foreground = labelColor;
            this.RankByDetectionConfidenceCheckbox.Foreground = labelColor;
            this.RankByClassificationConfidenceCheckbox.Foreground = labelColor;
            this.RankByNoneCheckbox.Foreground = labelColor;
            if (false == enableShowMissingDetectionsCheckbox)
            {
                this.ShowMissingDetectionsCheckbox.Foreground = labelColor;
            }
            this.TextBlockSortAllByLabel.Foreground = labelColor;
        }

        private void DetectionDataGridEnableState(bool enableState, bool updateCursorToMatchState)
        {
            if (updateCursorToMatchState)
            {
                Mouse.OverrideCursor = enableState ? null : Cursors.Wait;
            }
            this.DataGridDetections.IsEnabled = enableState;
        }

        private void ClassificationDataGridEnableState(bool enableState, bool updateCursorToMatchState)
        {
            if (updateCursorToMatchState)
            {
                Mouse.OverrideCursor = enableState ? null : Cursors.Wait;
            }
            this.DataGridClassifications.IsEnabled = enableState && this.ShowMissingDetectionsCheckbox.IsChecked == false;
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
        #endregion

        #region Display text feedback for Detection/Classification confidence range.
        // Note that we use Invariant to ensure that the decimal separator is always '.' regardless of culture
        private void DisplayDetectionConfidenceRange()
        {
            this.TBDetectionsCount.Text = Invariant($"({Math.Round(this.SliderDetectionConf.LowerValue, 2):f2} - {Math.Round(this.SliderDetectionConf.HigherValue, 2):f2})");
        }

        private void DisplayClassificationConfidenceRange()
        {
            this.TBClassificationsCount.Text = Invariant($"({Math.Round(this.SliderClassificationConf.LowerValue, 2):f2} - {Math.Round(this.SliderClassificationConf.HigherValue, 2):f2})");
        }
        #endregion

        #region Clear Counts
        public void ClearCountsAndResetUI()
        {
            if (this.DetectionCategories == null)
            {
                // We need to do this in case we haven't completely created the RecogntionsSelector
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
                // RecognitionSelections.DetectionCategoryNumber = kvp.Key;
                string categoryName = kvp.Value;
                this.SetDetectionCountForCategory(categoryName, NoValue);
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
            string savedCategoryNumber = RecognitionSelections.ClassificationCategoryNumber;
            foreach (KeyValuePair<string, string> kvp in ClassificationCategories)
            {
                RecognitionSelections.ClassificationCategoryNumber = kvp.Key;
                string categoryName = kvp.Value;
                this.SetClassificationCountForCategory(categoryName, NoValue);
            }

            RecognitionSelections.ClassificationCategoryNumber = savedCategoryNumber;
        }
        #endregion

        #region Set individual category detection or recognition counts and category labels
        // Given a category and a count, add or update it in the DetectionCountsCollection
        private void SetDetectionCountForCategory(string category, int? count)
        {
            var categoryCount = this.DetectionCountsCollection.FirstOrDefault(i => i.Category.StartsWith(category));
            if (null == categoryCount)

            {   // We need to add it to the DetectionsCountCollection
                Application.Current.Dispatcher.Invoke(delegate
                {
                    if (category.StartsWith(Constant.RecognizerValues.EmptyDetectionLabel))
                    {
                        double lowerValue = Math.Round(this.SliderDetectionConf.LowerValue, 2);
                        category = lowerValue == 0 && RecognitionSelections.AllDetections && RecognitionSelections.InterpretAllDetectionsAsEmpty
                        ? $"{Constant.RecognizerValues.EmptyDetectionLabel}"
                        : Invariant($"{Constant.RecognizerValues.EmptyDetectionLabel} (excludes detections {Constant.SearchTermOperator.GreaterThanOrEqual} {lowerValue})");
                    }
                    CategoryCount cc = new(category, count);
                    this.DetectionCountsCollection.Add(cc);
                    cc.NotifyPropertyChanged("Count");
                });
            }
            else
            {
                // Its already in the DetectionCountsCollection, so just update the count
                categoryCount.Count = count;
                categoryCount.NotifyPropertyChanged("Count");
            }
        }

        // Given a category and a count, add or update it in the DetectionCountsCollection
        private void SetClassificationCountForCategory(string category, int? count)
        {
            var categoryCount = this.ClassificationCountsCollection.FirstOrDefault(i => i.Category == category);
            if (null == categoryCount)
            {
                Application.Current.Dispatcher.Invoke(delegate
                {
                    CategoryCount cc = new(category, count);
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
        private void ShowProgressOrAbortIfCancelled(bool countingDetections, string entity)
        {
            if (this.Owner.Token.IsCancellationRequested)
            {
                throw new TaskCanceledException();
            }

            string what = countingDetections ? "detections" : "classifications";
            this.Owner.Progress.Report(new(0, $"Counting {what} ({entity}). Please wait", true, true));
            Thread.Sleep(ThrottleValues.RenderingBackoffTime);  // Allows the UI thread to update every now and then
        }
        #endregion

        #region Helpers
        // Given a detection or classification category dictionary and a category name,
        // - return its category number 
        private static string GetCategoryNumberFromCategoryName(Dictionary<string, string> categoryDictionary, string categoryName)
        {
            // Get the category number from its name
            foreach (KeyValuePair<string, string> kvp in categoryDictionary)
            {
                if (EqualityComparer<string>.Default.Equals(kvp.Value, categoryName))
                {
                    return kvp.Key;
                }
            }

            return string.Empty;
        }

        // Check if there are any missing counts in the collection
        private bool DetectionCountsCollectionHasCounts()
        {
            foreach (CategoryCount cc in this.DetectionCountsCollection)
            {
                if (cc.Count == null)
                {
                    return false;
                }
            }

            return true;
        }
        #endregion

        #region Class CategoryCount defines an element containing a detection category and its current count
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
    }
}
