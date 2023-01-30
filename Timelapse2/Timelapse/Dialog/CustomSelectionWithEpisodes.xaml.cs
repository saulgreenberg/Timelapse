
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.Enums;
using Timelapse.Recognition;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Dialog
{
    /// <summary>
    /// A dialog allowing a user to create a custom selection by setting conditions on data fields.
    /// </summary>
    public partial class CustomSelectionWithEpisodes
    {
        #region Private Variables
        private const int DefaultControlWidth = 200;

        private const int SelectColumn = 0;
        private const int LabelColumn = 1;
        private const int OperatorColumn = 2;
        private const int ValueColumn = 3;
        private const int SearchCriteriaColumn = 4;

        // Detections variables
        private bool dontInvoke;
        private bool dontCount;
        private bool dontUpdateRangeSlider;

        // Variables
        private readonly FileDatabase database;
        private readonly ImageRow currentImageRow;
        private readonly DataEntryControls dataEntryControls;
        private bool dontUpdate = true;

        // Remember note fields that contain Episode data
        string NoteDataLabelContainingEpisodeData;

        // UseTime Checkbox, funciton is to specify whether the select should use a pure time range instead of a pure date range
        private readonly CheckBox CheckBoxUseTime = new CheckBox()
        {
            Content = "Use time (hh:mm:ss) instead of date",
            FontWeight = FontWeights.Normal,
            FontStyle = FontStyles.Italic,
            VerticalAlignment = VerticalAlignment.Center,
            IsChecked = false,
            Width = Double.NaN,
            Margin = new Thickness
            {
                Left = 10
            },
            IsEnabled = true
        };

        // And/Or RadioButtons use to combine non-standard terms
        private readonly RadioButton RadioButtonTermCombiningAnd = new RadioButton()
        {
            Content = "And ",
            GroupName = "LogicalOperators",
            FontWeight = FontWeights.DemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            IsChecked = true,
            Width = Double.NaN,
            IsEnabled = false
        };
        private readonly RadioButton RadioButtonTermCombiningOr = new RadioButton()
        {
            Content = "Or ",
            GroupName = "LogicalOperators",
            VerticalAlignment = VerticalAlignment.Center,
            Width = Double.NaN,
            IsEnabled = false
        };

        // References to the various dateTime labels and controls set when they are created later,
        // so we can switch their attributes depending on the CheckBoxUseTime state
        private TextBlock dateTimeLabel1;
        private TextBlock dateTimeLabel2;
        private DateTimePicker dateTimeControl1;
        private DateTimePicker dateTimeControl2;

        // This timer is used to delay showing count information, which could be an expensive operation, as the user may be setting values quickly
        private readonly DispatcherTimer countTimer = new DispatcherTimer();

        private RecognitionSelections DetectionSelections { get; set; }
        #endregion

        #region Constructors and Loading
        public CustomSelectionWithEpisodes(FileDatabase database, DataEntryControls dataEntryControls, Window owner, RecognitionSelections detectionSelections, ImageRow currentImageRow)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(database, nameof(database));

            this.InitializeComponent();

            this.database = database;
            this.currentImageRow = currentImageRow;
            this.dataEntryControls = dataEntryControls;
            this.Owner = owner;
            this.countTimer.Interval = TimeSpan.FromMilliseconds(500);
            this.countTimer.Tick += this.CountTimer_Tick;

            // Detections-specific
            if (GlobalReferences.DetectionsExists)
            {
                this.DetectionSelections = detectionSelections;
            }
        }

        // When the window is loaded, construct all the SearchTerm controls 
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Used to track whether we are on the 1st or 2nd dateTime control
            bool firstDateTimeControlSeen = false;

            // Adds the callback to this checkbox
            this.CheckBoxUseTime.Checked += CheckBoxUseTime_CheckChanged;
            this.CheckBoxUseTime.Unchecked += CheckBoxUseTime_CheckChanged;

            // Adjust this dialog window position 
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            // Detections-specific
            this.dontCount = true;
            this.dontInvoke = true;

            // Set the state of the detections to the last used ones (or to its defaults)
            if (GlobalReferences.DetectionsExists)
            {
                this.DetectionGroupBox.Visibility = Visibility.Visible;
                this.Detections2Panel.Visibility = Visibility.Visible;
                this.UseDetectionsCheckbox.IsChecked = this.DetectionSelections.UseRecognition;

                // Set the spinner and sliders to the last used values
                this.DetectionConfidenceSpinnerLower.Value = this.DetectionSelections.ConfidenceThreshold1ForUI;
                this.DetectionConfidenceSpinnerHigher.Value = this.DetectionSelections.ConfidenceThreshold2ForUI;
                this.DetectionRangeSlider.LowerValue = this.DetectionSelections.ConfidenceThreshold1ForUI;
                this.DetectionRangeSlider.HigherValue = this.DetectionSelections.ConfidenceThreshold2ForUI;

                // Set the Rank by Confidence
                this.RankByConfidenceCheckbox.IsChecked = this.DetectionSelections.RankByConfidence;

                // Put Detection and Classification categories in the combo box as human-readable labels
                // Note that we add "All" to the Detections list as that is a 'bogus' Timelapse-internal category.
                List<string> labels = this.database.GetDetectionLabels();
                this.DetectionCategoryComboBox.Items.Add(Constant.RecognizerValues.AllDetectionLabel);
                foreach (string label in labels)
                {
                    this.DetectionCategoryComboBox.Items.Add(label);
                }

                if (GlobalReferences.UseClassifications)
                {
                    // Now add classifications
                    labels = this.database.GetClassificationLabels();
                    if (labels.Count > 0)
                    {
                        // Add a separator
                        ComboBoxItem separator = new ComboBoxItem
                        {
                            BorderBrush = Brushes.Black,
                            BorderThickness = new Thickness(0, 0, 0, 2),
                            Focusable = false,
                            IsEnabled = false
                        };
                        this.DetectionCategoryComboBox.Items.Add(separator);
                        foreach (string label in labels)
                        {
                            this.DetectionCategoryComboBox.Items.Add(label);
                        }
                    }
                }

                // Set the combobox selection to the last used one.
                string categoryLabel = String.Empty;
                if (this.DetectionSelections.RecognitionType == RecognitionType.Empty)
                {
                    // If we don't know the recognition type, default to All
                    this.DetectionCategoryComboBox.SelectedValue = Constant.RecognizerValues.AllDetectionLabel;
                }
                else if (this.DetectionSelections.RecognitionType == RecognitionType.Detection)
                {
                    categoryLabel = this.database.GetDetectionLabelFromCategory(this.DetectionSelections.DetectionCategory);
                    if (string.IsNullOrEmpty(this.DetectionSelections.DetectionCategory) || (this.DetectionSelections.AllDetections && !this.DetectionSelections.InterpretAllDetectionsAsEmpty))
                    {
                        // We need an 'All' detection category, which is the union of all categories (except empty).
                        // Because All is a bogus detection category (since its not part of the detection data), we have to set it explicitly
                        this.DetectionCategoryComboBox.SelectedValue = Constant.RecognizerValues.AllDetectionLabel;
                    }
                    else
                    {
                        this.DetectionCategoryComboBox.SelectedValue = categoryLabel;
                    }
                }
                else
                {
                    categoryLabel = this.database.GetClassificationLabelFromCategory(this.DetectionSelections.ClassificationCategory);
                    this.DetectionCategoryComboBox.SelectedValue = (categoryLabel.Length != 0)
                        ? categoryLabel
                        : this.DetectionCategoryComboBox.SelectedValue = Constant.RecognizerValues.AllDetectionLabel;
                }
                this.EnableDetectionControls((bool)this.UseDetectionsCheckbox.IsChecked);
            }
            else
            {
                this.DetectionGroupBox.Visibility = Visibility.Collapsed;
                this.Detections2Panel.Visibility = Visibility.Collapsed;
                this.DetectionSelections?.ClearAllDetectionsUses();
            }
            this.dontInvoke = false;
            this.dontCount = false;
            if (GlobalReferences.DetectionsExists)
            {
                this.SetDetectionCriteria();
                this.ShowMissingDetectionsCheckbox.IsChecked = this.database.CustomSelection.ShowMissingDetections;
                this.NoteDataLabelContainingEpisodeData = this.database.CustomSelection.EpisodeNoteField;
                if (this.database.CustomSelection.EpisodeShowAllIfAnyMatch && EpisodeFieldCheckFormat(this.currentImageRow, this.NoteDataLabelContainingEpisodeData))
                {
                    // Only check the checkbox if it was previously checked and the data field still contains valid Episode data
                    this.CheckboxShowAllEpisodeImages.IsChecked = this.database.CustomSelection.EpisodeShowAllIfAnyMatch;
                }
            }
            this.InitiateShowCountsOfMatchingFiles();
            this.DetectionCategoryComboBox.SelectionChanged += this.DetectionCategoryComboBox_SelectionChanged;

            // Selection-specific
            this.dontUpdate = true;

            // Configure the And vs Or conditional Radio Buttons
            if (this.database.CustomSelection.TermCombiningOperator == CustomSelectionOperatorEnum.And)
            {
                this.RadioButtonTermCombiningAnd.IsChecked = true;
                this.RadioButtonTermCombiningOr.IsChecked = false;
            }
            else
            {
                this.RadioButtonTermCombiningAnd.IsChecked = false;
                this.RadioButtonTermCombiningOr.IsChecked = true;
            }
            this.RadioButtonTermCombiningAnd.Checked += this.AndOrRadioButton_Checked;
            this.RadioButtonTermCombiningOr.Checked += this.AndOrRadioButton_Checked;

            // Create a new row for each search term. 
            // Each row specifies a particular control and how it can be searched
            // Note that the search terms are expected to be in a specific order i.e.
            // - the core standard controls defined by Timelapse
            // - the nonStandard controls defined by whoever customized the template 
            int gridRowIndex = 0;
            bool noSeparatorCreated = true;
            foreach (SearchTerm searchTerm in this.database.CustomSelection.SearchTerms)
            {
                // start at 1 as there is already a header row
                ++gridRowIndex;
                RowDefinition gridRow = new RowDefinition()
                {
                    Height = GridLength.Auto
                };
                this.SearchTerms.RowDefinitions.Add(gridRow);

                // USE Column: A checkbox to indicate whether the current search row should be used as part of the search
                Thickness thickness = new Thickness(5, 2, 5, 2);
                CheckBox useCurrentRow = new CheckBox()
                {
                    FontWeight = FontWeights.DemiBold,
                    Margin = thickness,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    IsChecked = searchTerm.UseForSearching
                };
                if (searchTerm.Label == Constant.DatabaseColumn.RelativePath && GlobalReferences.MainWindow.Arguments.ConstrainToRelativePath)
                {
                    useCurrentRow.IsChecked = true;
                    useCurrentRow.IsEnabled = false;
                }
                useCurrentRow.Checked += this.Select_CheckedOrUnchecked;
                useCurrentRow.Unchecked += this.Select_CheckedOrUnchecked;
                Grid.SetRow(useCurrentRow, gridRowIndex);
                Grid.SetColumn(useCurrentRow, CustomSelectionWithEpisodes.SelectColumn);
                this.SearchTerms.Children.Add(useCurrentRow);

                // LABEL column: The label associated with the control (Note: not the data label)
                TextBlock controlLabel = new TextBlock()
                {
                    FontWeight = searchTerm.UseForSearching ? FontWeights.DemiBold : FontWeights.Normal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5)
                };

                if (searchTerm.Label == Constant.DatabaseColumn.DateTime)
                {
                    // Change DateTime to Date
                    controlLabel.Text = Constant.ControlDeprecated.DateLabel;

                    // Remember the DateTime labels so we can switch their values when the CheckboxUseTime is checked/unchecked
                    if (dateTimeLabel1 == null)
                    {
                        // Must be the 1st one, as its unassigned
                        dateTimeLabel1 = controlLabel;
                    }
                    else
                    {
                        // Must be the 2nd one as the first one is unassigned
                        dateTimeLabel2 = controlLabel;
                    }
                }
                else if (searchTerm.Label == Constant.DatabaseColumn.RelativePath)
                {
                    // RelativePath label adds details
                    controlLabel.Inlines.Add(searchTerm.Label + " folder");
                    controlLabel.Inlines.Add(new Run(Environment.NewLine + "includes subfolders") { FontStyle = FontStyles.Italic, FontSize = 10 });
                }
                else
                {
                    // Just use the label's name
                    controlLabel.Text = searchTerm.Label;

                }
                Grid.SetRow(controlLabel, gridRowIndex);
                Grid.SetColumn(controlLabel, CustomSelectionWithEpisodes.LabelColumn);
                this.SearchTerms.Children.Add(controlLabel);

                // The operators allowed for each search term type
                string controlType = searchTerm.ControlType;
                string[] termOperators;
                if (controlType == Constant.Control.Counter ||
                    controlType == Constant.DatabaseColumn.DateTime ||
                    controlType == Constant.Control.FixedChoice)
                {
                    // No globs in Counters as that text field only allows numbers, we can't enter the special characters Glob required
                    // No globs in Dates the date entries are constrained by the date picker
                    // No globs in Fixed Choices as choice entries are constrained by menu selection
                    termOperators = new[]
                    {
                        Constant.SearchTermOperator.Equal,
                        Constant.SearchTermOperator.NotEqual,
                        Constant.SearchTermOperator.LessThan,
                        Constant.SearchTermOperator.GreaterThan,
                        Constant.SearchTermOperator.LessThanOrEqual,
                        Constant.SearchTermOperator.GreaterThanOrEqual
                    };
                }
                // Relative path only allows = (this will be converted later to a glob to get subfolders) 
                else if (controlType == Constant.DatabaseColumn.RelativePath)
                {
                    // Only equals (actually a glob including subfolders), as other options don't make sense for RelatvePath
                    termOperators = new[]
                    {
                        Constant.SearchTermOperator.Equal,
                    };
                }
                // Only equals and not equals (For relative path this will be converted later to a glob to get subfolders) 
                else if (controlType == Constant.DatabaseColumn.DeleteFlag ||
                         controlType == Constant.Control.Flag)
                {
                    // Only equals and not equals in Flags, as other options don't make sense for booleans
                    termOperators = new[]
                    {
                        Constant.SearchTermOperator.Equal,
                        Constant.SearchTermOperator.NotEqual
                    };
                }
                else
                {
                    termOperators = new[]
                    {
                        Constant.SearchTermOperator.Equal,
                        Constant.SearchTermOperator.NotEqual,
                        Constant.SearchTermOperator.LessThan,
                        Constant.SearchTermOperator.GreaterThan,
                        Constant.SearchTermOperator.LessThanOrEqual,
                        Constant.SearchTermOperator.GreaterThanOrEqual,
                        Constant.SearchTermOperator.Glob,
                        Constant.SearchTermOperator.NotGlob
                    };
                }

                // term operator combo box
                ComboBox operatorsComboBox = new ComboBox()
                {
                    FontWeight = FontWeights.Normal,
                    IsEnabled = searchTerm.UseForSearching,
                    ItemsSource = termOperators,
                    Margin = thickness,
                    Width = 70,
                    Height = 25,
                    SelectedValue = searchTerm.Operator
                };
                operatorsComboBox.SelectionChanged += this.Operator_SelectionChanged; // Create the callback that is invoked whenever the user changes the expresison
                Grid.SetRow(operatorsComboBox, gridRowIndex);
                Grid.SetColumn(operatorsComboBox, CustomSelectionWithEpisodes.OperatorColumn);
                this.SearchTerms.Children.Add(operatorsComboBox);

                // Value column: The value used for comparison in the search
                // Notes and Counters both uses a text field, so they can be constructed as a textbox
                // However, counter textboxes are modified to only allow integer input (both direct typing or pasting are checked)
                if (controlType == Constant.DatabaseColumn.DateTime)
                {
                    DateTime dateTime = this.database.CustomSelection.GetDateTimePLAINVERSION(gridRowIndex - 1);
                    // The DateTime Picker is set to show only the date portion
                    DateTimePicker dateValue = new DateTimePicker()
                    {
                        FontWeight = FontWeights.Normal,
                        Format = DateTimeFormat.Custom,
                        FormatString = Constant.Time.DateFormat,
                        IsEnabled = searchTerm.UseForSearching,
                        Width = DefaultControlWidth,
                        CultureInfo = CultureInfo.CreateSpecificCulture("en-US"),
                        Value = dateTime,
                        TimePickerVisibility = Visibility.Collapsed
                    };
                    // Remember the DateTime controls so we can switch whether they show Date or Time when the CheckboxUseTime is checked/unchecked
                    if (this.dateTimeControl1 == null)
                    {
                        // must be the first dateValue
                        this.dateTimeControl1 = dateValue;
                    }
                    else
                    {
                        // must be the 2nd dateValue
                        this.dateTimeControl2 = dateValue;
                    }
                    dateValue.ValueChanged += this.DateTime_SelectedDateChanged;
                    Grid.SetRow(dateValue, gridRowIndex);
                    Grid.SetColumn(dateValue, CustomSelectionWithEpisodes.ValueColumn);
                    this.SearchTerms.Children.Add(dateValue);
                }
                else if (controlType == Constant.DatabaseColumn.RelativePath)
                {
                    // Relative path uses a dropdown that shows existing folders
                    ComboBox comboBoxValue = new ComboBox()
                    {
                        FontWeight = FontWeights.Normal,
                        IsEnabled = searchTerm.UseForSearching,
                        Width = CustomSelectionWithEpisodes.DefaultControlWidth,
                        Height = 25,
                        Margin = thickness,

                    };
                    // Create the dropdown menu containing only folders with images in it
                    Arguments arguments = GlobalReferences.MainWindow.Arguments;
                    List<string> newFolderList;
                    if (false == arguments.ConstrainToRelativePath)
                    {
                        // We are not constrained to a particular relative path
                        newFolderList = this.database.GetFoldersFromRelativePaths();
                        comboBoxValue.ItemsSource = newFolderList;
                    }
                    else
                    {
                        // We are constrained to a particular relative path
                        // Generate a folder list that is just the relativePath and its sub-folders
                        newFolderList = new List<string>();
                        foreach (string folder in this.database.GetFoldersFromRelativePaths())
                        {
                            // Add the folder to the menu only if it isn't constrained by the relative path arguments
                            if (arguments.ConstrainToRelativePath && !(folder == arguments.RelativePath || folder.StartsWith(arguments.RelativePath + @"\")))
                            {
                                continue;
                            }
                            newFolderList.Add(folder);
                        }
                        comboBoxValue.ItemsSource = newFolderList;

                    }
                    // Set the relativepath item to the current relative path search term
                    if (newFolderList.Count > 0)
                    {
                        if (comboBoxValue.Items.Contains(searchTerm.DatabaseValue))
                        {
                            comboBoxValue.SelectedItem = searchTerm.DatabaseValue;
                        }
                        else
                        {
                            comboBoxValue.SelectedIndex = 0;
                        }
                        searchTerm.DatabaseValue = (string)comboBoxValue.SelectedValue;
                    }
                    comboBoxValue.SelectionChanged += this.FixedChoice_SelectionChanged;
                    Grid.SetRow(comboBoxValue, gridRowIndex);
                    Grid.SetColumn(comboBoxValue, CustomSelectionWithEpisodes.ValueColumn);
                    this.SearchTerms.Children.Add(comboBoxValue);
                }
                else if (controlType == Constant.DatabaseColumn.File ||
                         controlType == Constant.Control.Counter ||
                         controlType == Constant.Control.Note)
                {
                    AutocompleteTextBox textBoxValue = new AutocompleteTextBox()
                    {
                        FontWeight = FontWeights.Normal,
                        Autocompletions = null,
                        IsEnabled = searchTerm.UseForSearching,
                        Text = searchTerm.DatabaseValue,
                        Margin = thickness,
                        Width = CustomSelectionWithEpisodes.DefaultControlWidth,
                        Height = 22,
                        TextWrapping = TextWrapping.NoWrap,
                        VerticalAlignment = VerticalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center
                    };
                    if (controlType == Constant.Control.Note)
                    {
                        // Add existing autocompletions for this control
                        textBoxValue.Autocompletions = this.dataEntryControls.AutocompletionGetForNote(searchTerm.DataLabel);
                    }

                    // The following is specific only to Counters
                    if (controlType == Constant.Control.Counter)
                    {
                        textBoxValue.PreviewTextInput += this.Counter_PreviewTextInput;
                        DataObject.AddPastingHandler(textBoxValue, this.Counter_Paste);
                    }
                    textBoxValue.TextChanged += this.NoteOrCounter_TextChanged;

                    Grid.SetRow(textBoxValue, gridRowIndex);
                    Grid.SetColumn(textBoxValue, CustomSelectionWithEpisodes.ValueColumn);
                    this.SearchTerms.Children.Add(textBoxValue);
                }
                else if (controlType == Constant.Control.FixedChoice)
                {
                    // FixedChoice presents combo boxes, so they can be constructed the same way
                    ComboBox comboBoxValue = new ComboBox()
                    {
                        FontWeight = FontWeights.Normal,
                        IsEnabled = searchTerm.UseForSearching,
                        Width = CustomSelectionWithEpisodes.DefaultControlWidth,
                        Margin = thickness,

                        // Create the dropdown menu 
                        ItemsSource = searchTerm.List,
                        SelectedItem = searchTerm.DatabaseValue
                    };
                    comboBoxValue.SelectionChanged += this.FixedChoice_SelectionChanged;
                    Grid.SetRow(comboBoxValue, gridRowIndex);
                    Grid.SetColumn(comboBoxValue, CustomSelectionWithEpisodes.ValueColumn);
                    this.SearchTerms.Children.Add(comboBoxValue);
                }
                else if (controlType == Constant.DatabaseColumn.DeleteFlag ||
                         controlType == Constant.Control.Flag)
                {
                    // Flags present checkboxes
                    CheckBox flagCheckBox = new CheckBox()
                    {
                        FontWeight = FontWeights.Normal,
                        Margin = thickness,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        IsChecked = !String.Equals(searchTerm.DatabaseValue, Constant.BooleanValue.False, StringComparison.OrdinalIgnoreCase),
                        IsEnabled = searchTerm.UseForSearching
                    };
                    flagCheckBox.Checked += this.Flag_CheckedOrUnchecked;
                    flagCheckBox.Unchecked += this.Flag_CheckedOrUnchecked;

                    searchTerm.DatabaseValue = flagCheckBox.IsChecked.Value ? Constant.BooleanValue.True : Constant.BooleanValue.False;

                    Grid.SetRow(flagCheckBox, gridRowIndex);
                    Grid.SetColumn(flagCheckBox, CustomSelectionWithEpisodes.ValueColumn);
                    this.SearchTerms.Children.Add(flagCheckBox);
                }
                else
                {
                    throw new NotSupportedException(String.Format("Unhandled control type '{0}'.", controlType));
                }

                if (searchTerm.DataLabel == Constant.DatabaseColumn.DateTime && firstDateTimeControlSeen == false)
                {
                    // Display the CheckBoxUseTime control next to the DateTime expression
                    Grid.SetRow(CheckBoxUseTime, gridRowIndex);
                    Grid.SetColumn(CheckBoxUseTime, CustomSelectionWithEpisodes.SearchCriteriaColumn);
                    Grid.SetRowSpan(CheckBoxUseTime, 2);
                    this.SearchTerms.Children.Add(CheckBoxUseTime);
                    firstDateTimeControlSeen = true;
                }

                // Conditional And/Or column
                // If we are  on the first term after the non-standard controls
                // - create the and/or buttons in the last column, which lets a user determine how to combine the remaining terms
                if (noSeparatorCreated && false == (searchTerm.DataLabel == Constant.DatabaseColumn.File ||
                              searchTerm.DataLabel == Constant.DatabaseColumn.RelativePath || searchTerm.DataLabel == Constant.DatabaseColumn.DateTime ||
                              searchTerm.DataLabel == Constant.DatabaseColumn.DeleteFlag))
                {
                    StackPanel sp = CreateAndOrButtons();
                    Grid.SetRow(sp, gridRowIndex);
                    Grid.SetColumn(sp, CustomSelectionWithEpisodes.SearchCriteriaColumn);
                    Grid.SetRowSpan(sp, 2);
                    this.SearchTerms.Children.Add(sp);
                    noSeparatorCreated = false;
                }

                // If we are  on the first term in the standard controls
                // - create a description that tells the user how these will be combined
                if (gridRowIndex == 1 && noSeparatorCreated)
                {
                    StackPanel sp = CreateStandardControlDescription();
                    Grid.SetRow(sp, gridRowIndex);
                    Grid.SetColumn(sp, CustomSelectionWithEpisodes.SearchCriteriaColumn);
                    Grid.SetRowSpan(sp, 2);
                    this.SearchTerms.Children.Add(sp);
                }
            }
            this.dontUpdate = false;
            this.UpdateSearchDialogFeedback();

            // Load the available note fields in the Episode ComboBox
            // and set the CustomSelection to the current values
            this.NoteDataLabelContainingEpisodeData = String.Empty;
            foreach (ControlRow control in this.database.Controls)
            {
                if (control.Type == Constant.Control.Note && EpisodeFieldCheckFormat(this.currentImageRow, control.DataLabel))
                {
                    // We found a note data label whose value in the current image follows the expected Episode format.
                    // So save it
                    this.NoteDataLabelContainingEpisodeData = control.DataLabel;
                    break;
                }
            }

            // Set the UseTime state based on what was last recorded
            this.CheckBoxUseTime.IsChecked = this.database.CustomSelection.UseTimeInsteadOfDate;

            // Set the selected item to the Note field with episode data in it.
            this.database.CustomSelection.EpisodeNoteField = this.NoteDataLabelContainingEpisodeData;
            this.database.CustomSelection.EpisodeShowAllIfAnyMatch = this.CheckboxShowAllEpisodeImages.IsChecked == true;
        }
        #endregion

        #region CheckBoxUseTime Callbacks
        // Toggle whether we are using dates or times
        private void CheckBoxUseTime_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                // Remember the checkbox state
                this.database.CustomSelection.UseTimeInsteadOfDate = cb.IsChecked == true;

                // The DateTime label should reflect the state
                if (this.dateTimeLabel1 != null)
                {
                    this.dateTimeLabel1.Text = cb.IsChecked == true ? "Time" : "Date";
                }
                if (this.dateTimeLabel2 != null)
                {
                    this.dateTimeLabel2.Text = cb.IsChecked == true ? "Time" : "Date";
                }

                // The DateTime control should reflect the state by displaying Time or Date only input
                if (this.dateTimeControl1 != null)
                {
                    dateTimeControl1.TimePickerVisibility = cb.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                    dateTimeControl1.ShowDropDownButton = cb.IsChecked == false;
                    dateTimeControl1.FormatString = cb.IsChecked == true ? Constant.Time.TimeInputFormat : Constant.Time.DateFormat;
                }
                if (this.dateTimeControl2 != null)
                {
                    dateTimeControl2.TimePickerVisibility = cb.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                    dateTimeControl2.ShowDropDownButton = cb.IsChecked == false;
                    dateTimeControl2.FormatString = cb.IsChecked == true ? Constant.Time.TimeInputFormat : Constant.Time.DateFormat;
                }
                // Update the count. We could be a bit more efficient by checking to see if either of these controls have their 'Use'checkboxes unchecked,
                // but its not worth the bother.
                this.InitiateShowCountsOfMatchingFiles();
            }
        }
        #endregion

        #region Create Multiple Term column entries (And/Or buttons)
        // Create the AND/OR Radio buttons to set how non-standard controls are combined i.e., with AND or OR SQL queries
        // The actual radio buttons are defined above as instance variables
        private StackPanel CreateAndOrButtons()
        {
            // Separator
            Separator separator = new Separator()
            {
                Width = Double.NaN
            };

            // Haader text
            TextBlock tbHeader = new TextBlock()
            {
                Text = "Choose how terms are combined using either",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Normal,
            };

            // And control
            StackPanel spAnd = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                Width = Double.NaN
            };

            TextBlock tbAnd = new TextBlock()
            {
                Text = "to match all selected conditions",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Normal,
            };
            spAnd.Children.Add(this.RadioButtonTermCombiningAnd);
            spAnd.Children.Add(tbAnd);

            // Or control
            StackPanel spOr = new StackPanel()
            {
                Name = "TermCombiningOr",
                Orientation = Orientation.Horizontal,
                Width = Double.NaN,
            };

            TextBlock tbOr = new TextBlock()
            {
                Text = "to match at least one selected conditions",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Normal,
            };
            spOr.Children.Add(this.RadioButtonTermCombiningOr);
            spOr.Children.Add(tbOr);

            // Container for above
            StackPanel sp = new StackPanel()
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(10, 0, 0, 0),
                Width = Double.NaN
            };

            sp.Children.Add(separator);
            sp.Children.Add(tbHeader);
            sp.Children.Add(spAnd);
            sp.Children.Add(spOr);
            return sp;
        }

        // Create the AND/OR Radio buttons to set how non-standard controls are combined i.e., with AND or OR SQL queries
        // The actual radio buttons are defined above as instance variables
        private static StackPanel CreateStandardControlDescription()
        {
            // Separator
            Separator separator = new Separator()
            {
                Width = Double.NaN
            };

            // Haader text
            TextBlock tbHeader = new TextBlock()
            {
                Text = "These terms are combined using AND:" + Environment.NewLine + "returned files match all selected conditions.",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Normal,
            };

            // Container for above
            StackPanel sp = new StackPanel()
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(10, 0, 0, 0),
                Width = Double.NaN
            };

            sp.Children.Add(separator);
            sp.Children.Add(tbHeader);
            return sp;
        }
        #endregion

        #region Query formation callbacks
        // Radio buttons for determing if we use And or Or
        private void AndOrRadioButton_Checked(object sender, RoutedEventArgs args)
        {
            RadioButton radioButton = sender as RadioButton;
            this.database.CustomSelection.TermCombiningOperator = (radioButton == this.RadioButtonTermCombiningAnd) ? CustomSelectionOperatorEnum.And : CustomSelectionOperatorEnum.Or;
            this.UpdateSearchDialogFeedback();
        }

        // Select: When the use checks or unchecks the checkbox for a row
        // - activate or deactivate the search criteria for that row
        // - update the searchterms to reflect the new status 
        // - update the UI to activate or deactivate (or show or hide) its various search terms
        private void Select_CheckedOrUnchecked(object sender, RoutedEventArgs args)
        {
            CheckBox select = sender as CheckBox;
            int row = Grid.GetRow(select);  // And you have the row number...

            SearchTerm searchterms = this.database.CustomSelection.SearchTerms[row - 1];
            searchterms.UseForSearching = select.IsChecked.Value;

            TextBlock label = this.GetGridElement<TextBlock>(CustomSelectionWithEpisodes.LabelColumn, row);
            ComboBox expression = this.GetGridElement<ComboBox>(CustomSelectionWithEpisodes.OperatorColumn, row);
            UIElement value = this.GetGridElement<UIElement>(CustomSelectionWithEpisodes.ValueColumn, row);

            label.FontWeight = select.IsChecked.Value ? FontWeights.DemiBold : FontWeights.Normal;
            expression.IsEnabled = select.IsChecked.Value;
            value.IsEnabled = select.IsChecked.Value;

            this.UpdateSearchDialogFeedback();
        }

        // Operator: The user has selected a new expression
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void Operator_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            ComboBox comboBox = sender as ComboBox;
            int row = Grid.GetRow(comboBox);  // Get the row number...
            this.database.CustomSelection.SearchTerms[row - 1].Operator = comboBox.SelectedValue.ToString(); // Set the corresponding expression to the current selection
            this.UpdateSearchDialogFeedback();
        }

        // Value (Counters and Notes): The user has selected a new value
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void NoteOrCounter_TextChanged(object sender, TextChangedEventArgs args)
        {
            TextBox textBox = sender as TextBox;
            int row = Grid.GetRow(textBox);  // Get the row number...
            this.database.CustomSelection.SearchTerms[row - 1].DatabaseValue = textBox.Text;
            this.UpdateSearchDialogFeedback();
        }

        // Value (Counter) Helper function: textbox accept only typed numbers 
        private void Counter_PreviewTextInput(object sender, TextCompositionEventArgs args)
        {
            args.Handled = IsNumbersOnly(args.Text);
        }

        // Value (DateTime): we need to construct a string DateTime from it
        private void DateTime_SelectedDateChanged(object sender, RoutedPropertyChangedEventArgs<object> args)
        {
            DateTimePicker datePicker = sender as DateTimePicker;
            if (datePicker.Value.HasValue)
            {
                int row = Grid.GetRow(datePicker);
                // Set the DateTime from the updated value, regardless of whether the UseTime checkbox is checked.
                // This stores both the date and the time.
                // Later, the search itself will check whether the UseTime is true or false to determine whether it should parse out the date or time portion.
                this.database.CustomSelection.SetDateTime(row - 1, datePicker.Value.Value);
                this.UpdateSearchDialogFeedback();
            }
        }

        // Value (FixedChoice): The user has selected a new value 
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void FixedChoice_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            ComboBox comboBox = sender as ComboBox;
            int row = Grid.GetRow(comboBox);  // Get the row number...
            if (comboBox.SelectedValue == null)
            {
                return;
            }
            this.database.CustomSelection.SearchTerms[row - 1].DatabaseValue = comboBox.SelectedValue.ToString(); // Set the corresponding value to the current selection
            this.UpdateSearchDialogFeedback();
        }

        // Value (Flags): The user has checked or unchecked a new value 
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void Flag_CheckedOrUnchecked(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            int row = Grid.GetRow(checkBox);  // Get the row number...
            this.database.CustomSelection.SearchTerms[row - 1].DatabaseValue = checkBox.IsChecked.ToString().ToLower(); // Set the corresponding value to the current selection
            this.UpdateSearchDialogFeedback();
        }

        // When this button is pressed, all the search terms checkboxes are cleared, which is equivalent to showing all images
        private void ResetToAllImagesButton_Click(object sender, RoutedEventArgs e)
        {
            this.UseDetectionsCheckbox.IsChecked = false;
            for (int row = 1; row <= this.database.CustomSelection.SearchTerms.Count; row++)
            {
                CheckBox select = this.GetGridElement<CheckBox>(CustomSelectionWithEpisodes.SelectColumn, row);
                select.IsChecked = false;
            }
            this.ShowMissingDetectionsCheckbox.IsChecked = false;
        }
        #endregion

        #region Search Criteria feedback for each row
        // Updates the feedback and control enablement to reflect the contents of the search list,
        private void UpdateSearchDialogFeedback()
        {
            if (this.dontUpdate)
            {
                return;
            }
            // We go backwards, as we don't want to print the AND or OR on the last expression
            bool atLeastOneSearchTermIsSelected = false;
            int multipleNonStandardSelectionsMade = 0;
            for (int index = this.database.CustomSelection.SearchTerms.Count - 1; index >= 0; index--)
            {
                SearchTerm searchTerm = this.database.CustomSelection.SearchTerms[index];

                if (searchTerm.UseForSearching == false)
                {
                    // As this search term is not used for searching, we can skip it
                    continue;
                }

                if (false == (searchTerm.DataLabel == Constant.DatabaseColumn.File ||
                              searchTerm.DataLabel == Constant.DatabaseColumn.RelativePath || searchTerm.DataLabel == Constant.DatabaseColumn.DateTime ||
                              searchTerm.DataLabel == Constant.DatabaseColumn.DeleteFlag))
                {
                    // Count the number of multiple non-standard selection rows
                    multipleNonStandardSelectionsMade++;
                }
                atLeastOneSearchTermIsSelected = true;
            }

            // Show how many file will match the current search
            this.InitiateShowCountsOfMatchingFiles();

            // Enable  the reset button if at least one search term (including detections) is enabled
            this.ResetToAllImagesButton.IsEnabled = atLeastOneSearchTermIsSelected
                                                    || (bool)this.ShowMissingDetectionsCheckbox.IsChecked;

            // Enable the and/or radio buttons if more than one non-standard selection was made
            this.RadioButtonTermCombiningAnd.IsEnabled = multipleNonStandardSelectionsMade > 1;
            this.RadioButtonTermCombiningOr.IsEnabled = multipleNonStandardSelectionsMade > 1;
        }
        #endregion

        #region Helper functions
        // Get the corresponding grid element from a given a column, row, 
        private TElement GetGridElement<TElement>(int column, int row) where TElement : UIElement
        {
            return (TElement)this.SearchTerms.Children.Cast<UIElement>().First(control => Grid.GetRow(control) == row && Grid.GetColumn(control) == column);
        }

        // Value (Counter) Helper function:  textbox accept only pasted numbers 
        private void Counter_Paste(object sender, DataObjectPastingEventArgs args)
        {
            bool isText = args.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true);
            if (!isText)
            {
                args.CancelCommand();
            }

            string text = args.SourceDataObject.GetData(DataFormats.UnicodeText) as string;
            if (CustomSelectionWithEpisodes.IsNumbersOnly(text))
            {
                args.CancelCommand();
            }
        }

        // Value(Counter) Helper function: checks if the text contains only numbers
        private static bool IsNumbersOnly(string text)
        {
            Regex regex = new Regex("[^0-9.-]+"); // regex that matches allowed text
            return regex.IsMatch(text);
        }
        #endregion

        #region Detection-specific methods and callbacks
        private void UseDetections_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (this.dontInvoke)
            {
                return;
            }
            // Enable or disable the controls depending on the various checkbox states
            this.EnableDetectionControls((bool)this.UseDetectionsCheckbox.IsChecked);

            this.SetDetectionCriteria();
            this.InitiateShowCountsOfMatchingFiles();
        }

        private void SetDetectionCriteria()
        {
            SetDetectionCriteria(false);
        }

        private void SetDetectionCriteria(bool resetSlidersIfNeeded)
        {
            if (this.IsLoaded == false || this.dontInvoke)
            {
                return;
            }
            this.DetectionSelections.UseRecognition = this.UseDetectionsCheckbox.IsChecked == true;
            if (this.DetectionSelections.UseRecognition)
            {
                this.SetDetectionCriteriaForComboBox(resetSlidersIfNeeded);
                this.DetectionSelections.ConfidenceThreshold1ForUI = this.DetectionConfidenceSpinnerLower.Value == null ? 0 : Round2(this.DetectionConfidenceSpinnerLower.Value);
                this.DetectionSelections.ConfidenceThreshold2ForUI = this.DetectionConfidenceSpinnerHigher.Value == null ? 0 : Round2(this.DetectionConfidenceSpinnerHigher.Value);
            }

            // The BoundingBoxDisplayThreshold is the user-defined default set in preferences, while the BoundingBoxThresholdOveride is the threshold
            // determined in this select dialog. For example, if (say) the preference setting is .6 but the selection is at .4 confidence, then we should 
            // show bounding boxes when the confidence is .4 or more. 
            Tuple<double, double> confidenceBounds = this.DetectionSelections.ConfidenceThresholdForSelect;
            GlobalReferences.TimelapseState.BoundingBoxThresholdOveride = this.DetectionSelections.UseRecognition // && this.DetectionSelections.RecognitionType != RecognitionType.Classification
                ? confidenceBounds.Item1
                : 1;
            // Debug.Print(GlobalReferences.TimelapseState.BoundingBoxThresholdOveride.ToString());
            // Enable / alter looks and behavour of detecion UI to match whether detections should be used
            this.EnableDetectionControls((bool)this.UseDetectionsCheckbox.IsChecked);
        }

        private void ShowMissingDetectionsCheckbox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            this.database.CustomSelection.ShowMissingDetections = (bool)this.ShowMissingDetectionsCheckbox.IsChecked;
            this.SetDetectionCriteria();
            this.InitiateShowCountsOfMatchingFiles();
        }
        private void DetectionCategoryComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (this.IsLoaded == false)
            {
                return;
            }
            // Invoke this with a true argument, which forces the confidence values to be reset based upon the selection
            this.SetDetectionCriteria(true);
            this.InitiateShowCountsOfMatchingFiles();
        }

        private void SetDetectionCriteriaForComboBox(bool resetSlidersIfNeeded)
        {
            this.ignoreSpinnerUpdates = true; // as otherwise resetting sliders/spinners will reinvoke this

            // Set various flags and values depending on what was selected in the combo box
            if (resetSlidersIfNeeded)
            {
                // These reset settings universally apply regardless of the recognition type
                // The higher limit is always 1.0. Resetting the lower limit to its undefined state signals that default values should be looked up and used.
                this.DetectionRangeSlider.HigherValue = 1.0;
                this.DetectionSelections.CurrentDetectionThreshold = Constant.RecognizerValues.Undefined; // As its a new JSON, resetting sets it back to detections, so we can use the default value.
                this.DetectionSelections.CurrentClassificationThreshold = Constant.RecognizerValues.Undefined; // As its a new JSON, resetting sets it back to detections, so we can use the default value.
            }

            if ((string)this.DetectionCategoryComboBox.SelectedItem == Constant.RecognizerValues.NoDetectionLabel)
            {
                // EMPTY
                // Note that Empties are special cases of an All Detection (which is why its recognition type is still a Detection and both AllDetections and EmptyDetections are true).
                // What actually happens is that other code flips the confidence values in the query to 1 - the current higher and lowever slider settings (e.g., from 1-1 to 0-0). 
                this.DetectionSelections.RecognitionType = RecognitionType.Detection;
                this.DetectionSelections.InterpretAllDetectionsAsEmpty = true;
                this.DetectionSelections.AllDetections = true; // Empty detections signal that we need to flip the confidence to its inverse, which is then applied to AllDetections
                this.DetectionSelections.DetectionCategory = this.database.GetDetectionCategoryFromLabel(Constant.RecognizerValues.NoDetectionLabel);

                if (resetSlidersIfNeeded)
                {
                    // Default is ConservativeDetection Threshold - 1.0, i.e.,to only show images where the recognizer has not found any detections.
                    // As the EmptyDetections is true, other code will special case this by actually doing a query on 1 - these values
                    this.DetectionRangeSlider.HigherValue = 1.0;
                    this.DetectionRangeSlider.LowerValue = 1 - RecognitionSelections.ConservativeDetectionThreshold;
                }

                // Set the minium values for the slider and spinner, which is 0 fpr Empty detections
                this.DetectionRangeSlider.Minimum = 0;
                this.DetectionConfidenceSpinnerLower.Minimum = 0;
                this.DetectionConfidenceSpinnerHigher.Minimum = 0;
            }
            else
            {
                // A non-empty selection
                this.DetectionSelections.InterpretAllDetectionsAsEmpty = false;

                // Set the minium values for the slider and spinner
                // These are just a titch above 0, which means the results will only include items with a detection, but never include purely empty items (i.e., items with no detections)
                this.DetectionRangeSlider.Minimum = Constant.RecognizerValues.MinimumDetectionValue;
                this.DetectionConfidenceSpinnerLower.Minimum = Constant.RecognizerValues.MinimumDetectionValue;
                this.DetectionConfidenceSpinnerHigher.Minimum = Constant.RecognizerValues.MinimumDetectionValue;

                // Resetting the minimum doesn't necessarily change the value if its below the minimum
                if (this.DetectionConfidenceSpinnerLower.Value == null || this.DetectionConfidenceSpinnerLower.Value < Constant.RecognizerValues.MinimumDetectionValue)
                {
                    this.DetectionConfidenceSpinnerLower.Value = Constant.RecognizerValues.MinimumDetectionValue;
                }
                if (this.DetectionConfidenceSpinnerHigher.Value == null || this.DetectionConfidenceSpinnerHigher.Value < Constant.RecognizerValues.MinimumDetectionValue)
                {
                    this.DetectionConfidenceSpinnerHigher.Value = Constant.RecognizerValues.MinimumDetectionValue;
                }

                //this.DetectionSelections.ConfidenceThreshold1ForUI = this.DetectionConfidenceSpinnerLower.Value == null  ? Constant.RecognizerValues.MinimumDetectionValue : Round2(this.DetectionConfidenceSpinnerLower.Value);
                //this.DetectionSelections.ConfidenceThreshold2ForUI = this.DetectionConfidenceSpinnerHigher.Value == null ? Constant.RecognizerValues.MinimumDetectionValue : Round2(this.DetectionConfidenceSpinnerHigher.Value);

                if ((string)this.DetectionCategoryComboBox.SelectedItem == Constant.RecognizerValues.AllDetectionLabel)
                {
                    // ALL (which is a detection)
                    this.DetectionSelections.RecognitionType = RecognitionType.Detection;
                    this.DetectionSelections.AllDetections = true;
                    this.DetectionSelections.InterpretAllDetectionsAsEmpty = false;
                    if (resetSlidersIfNeeded)
                    {
                        this.DetectionRangeSlider.LowerValue = this.DetectionSelections.CurrentDetectionThreshold;
                    }
                }
                else
                {
                    // Either a Detection ((excluding All and Empty)) or a Classification type 
                    this.DetectionSelections.AllDetections = false;
                    this.DetectionSelections.InterpretAllDetectionsAsEmpty = false;
                    string detectionCategory = this.database.GetDetectionCategoryFromLabel((string)this.DetectionCategoryComboBox.SelectedItem);

                    if (String.IsNullOrWhiteSpace(detectionCategory))
                    {
                        // CLASSIFICATION
                        this.DetectionSelections.RecognitionType = RecognitionType.Classification;
                        this.DetectionSelections.ClassificationCategory = this.database.GetClassificationCategoryFromLabel((string)this.DetectionCategoryComboBox.SelectedItem);
                        if (resetSlidersIfNeeded)
                        {
                            this.DetectionRangeSlider.LowerValue = this.DetectionSelections.CurrentClassificationThreshold;
                        }
                    }
                    else
                    {
                        // DETECTION
                        this.DetectionSelections.RecognitionType = RecognitionType.Detection;
                        this.DetectionSelections.DetectionCategory = detectionCategory;
                        if (resetSlidersIfNeeded)
                        {
                            this.DetectionRangeSlider.LowerValue = this.DetectionSelections.CurrentDetectionThreshold;
                        }
                    }
                }
            }
            this.ignoreSpinnerUpdates = false;
        }

        // Note that for either of these, we avoid a race condition where each tries to update the other by
        // setting this.ignoreSpinnerUpdates to true, which will cancel the operation
        private bool ignoreSpinnerUpdates;
        private void DetectionConfidenceSpinnerLower_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (this.IsLoaded == false || this.ignoreSpinnerUpdates)
            {
                return;
            }

            // If the user has set the upper spinner to less than the lower spinner,
            // reset it to the value of the lower spinner. That is, don't allow the user to 
            // go below the lower spinner value.
            if (this.DetectionConfidenceSpinnerLower.Value > this.DetectionConfidenceSpinnerHigher.Value)
            {
                this.ignoreSpinnerUpdates = true;
                this.DetectionConfidenceSpinnerLower.Value = this.DetectionConfidenceSpinnerHigher.Value;
                this.ignoreSpinnerUpdates = false;
            }
            this.SetDetectionCriteria();

            if (this.dontUpdateRangeSlider == false)
            {
                this.DetectionRangeSlider.LowerValue = this.DetectionConfidenceSpinnerLower.Value ?? 0;
            }
            else
            {
                this.dontUpdateRangeSlider = false;
            }


            if (this.DetectionSelections.RecognitionType == RecognitionType.Detection)
            {
                this.DetectionSelections.CurrentDetectionThreshold = (double)this.DetectionConfidenceSpinnerLower.Value;
            }
            else if (this.DetectionSelections.RecognitionType == RecognitionType.Classification)
            {
                this.DetectionSelections.CurrentDetectionThreshold = (double)this.DetectionConfidenceSpinnerLower.Value;
            }
            this.InitiateShowCountsOfMatchingFiles();
        }

        private void DetectionConfidenceSpinnerHigher_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (this.IsLoaded == false || this.ignoreSpinnerUpdates)
            {
                return;
            }

            // If the user has set the upper spinner to less than the lower spinner,
            // reset it to the value of the lower spinner. That is, don't allow the user to 
            // go below the lower spinner value.
            if (this.DetectionConfidenceSpinnerHigher.Value < this.DetectionConfidenceSpinnerLower.Value)
            {
                this.ignoreSpinnerUpdates = true;
                this.DetectionConfidenceSpinnerHigher.Value = this.DetectionConfidenceSpinnerLower.Value;
                this.ignoreSpinnerUpdates = false;
            }
            this.SetDetectionCriteria();

            if (this.dontUpdateRangeSlider == false)
            {
                this.DetectionRangeSlider.HigherValue = this.DetectionConfidenceSpinnerHigher.Value ?? 0;
                this.dontUpdateRangeSlider = false;
            }
            else
            {
                this.dontUpdateRangeSlider = false;
            }
            this.InitiateShowCountsOfMatchingFiles();
        }

        // Detection range slider callback - Upper range
        // Note that this does not invoke this.SetDetectionCriteria(), as that is done as a side effect of invoking the spinner
        private void DetectionRangeSlider_HigherValueChanged(object sender, RoutedEventArgs e)
        {
            // Round up the value to the nearest 2 decimal places,
            // and update the spinner (also in two decimal places) only if the value differs
            // This stops the spinner from updated if values change in the 3rd decimal place and beyond
            double value = Round2(this.DetectionRangeSlider.HigherValue);
            if (value != Round2(this.DetectionConfidenceSpinnerHigher.Value))
            {
                this.dontUpdateRangeSlider = true;
                this.DetectionConfidenceSpinnerHigher.Value = value;
                this.dontUpdateRangeSlider = false;
            }
        }

        // Detection range slider callback - Lower range
        // Note that this does not invoke this.SetDetectionCriteria(), as that is done as a side effect of invoking the spinner
        private void DetectionRangeSlider_LowerValueChanged(object sender, RoutedEventArgs e)
        {
            // Round up the value to the nearest 2 decimal places,
            // and update the spinner (also in two decimal places) only if the value differs
            // This stops the spinner from updated if values change in the 3rd decimal place and beyond
            double value = Round2(this.DetectionRangeSlider.LowerValue);
            if (value != Round2(this.DetectionConfidenceSpinnerLower.Value))
            {
                this.dontUpdateRangeSlider = true;
                this.DetectionConfidenceSpinnerLower.Value = value;
                this.dontUpdateRangeSlider = false;
            }
        }

        // Enable or disable the controls depending on the parameter
        private void EnableDetectionControls(bool isEnabled)
        {
            // Various confidence controls are enabled only if useDetections is set and the rank by confidence is unchecked
            bool confidenceControlsEnabled = isEnabled && !this.DetectionSelections.RankByConfidence;
            this.DetectionConfidenceSpinnerLower.IsEnabled = confidenceControlsEnabled;
            this.DetectionConfidenceSpinnerHigher.IsEnabled = confidenceControlsEnabled;
            this.DetectionRangeSlider.IsEnabled = confidenceControlsEnabled;
            this.ConfidenceLabel.FontWeight = confidenceControlsEnabled ? FontWeights.Normal : FontWeights.Light;
            this.FromLabel.FontWeight = confidenceControlsEnabled ? FontWeights.Normal : FontWeights.Light;
            this.ToLabel.FontWeight = confidenceControlsEnabled ? FontWeights.Normal : FontWeights.Light;
            this.DetectionRangeSlider.RangeBackground = confidenceControlsEnabled ? Brushes.Gold : Brushes.LightGray;

            // The episode contorls are only enabled if detections is enabled
            this.CheckboxShowAllEpisodeImages.FontWeight = isEnabled ? FontWeights.Normal : FontWeights.Light;
            this.CheckboxShowAllEpisodeImages.IsEnabled = isEnabled;

            // There remainder depends upon the use detections isEnable state only
            this.DetectionCategoryComboBox.IsEnabled = isEnabled;
            this.CategoryLabel.FontWeight = isEnabled ? FontWeights.Normal : FontWeights.Light;
            this.RankByConfidenceCheckbox.IsEnabled = isEnabled;
            this.RankByConfidenceCheckbox.FontWeight = isEnabled ? FontWeights.Normal : FontWeights.Light;

            // CHECK THE ONES BELOW TO SEE IF THIS IS THE BEST WAY TO DO THESE
            this.SelectionGroupBox.IsEnabled = !this.database.CustomSelection.ShowMissingDetections;
            this.SelectionGroupBox.Background = this.database.CustomSelection.ShowMissingDetections ? Brushes.LightGray : Brushes.White;

            this.DetectionGroupBox.IsEnabled = !this.database.CustomSelection.ShowMissingDetections;
            this.DetectionGroupBox.Background = this.database.CustomSelection.ShowMissingDetections ? Brushes.LightGray : Brushes.White;

            if ((bool)this.ShowMissingDetectionsCheckbox.IsChecked || (bool)this.UseDetectionsCheckbox.IsChecked)
            {
                this.ResetToAllImagesButton.IsEnabled = true;
            }
        }

        private void RankByConfidence_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // Need to disable confidence sliders/spinners depending on the state of this checkbox and use detections
            // ALso need to restore state of this checkbox between repeated uses in Window_Loaded.
            this.DetectionSelections.RankByConfidence = this.RankByConfidenceCheckbox.IsChecked == true;
            this.InitiateShowCountsOfMatchingFiles();
            this.EnableDetectionControls((bool)this.UseDetectionsCheckbox.IsChecked);
        }
        #endregion

        #region Common to Selections and Detections
        private void CountTimer_Tick(object sender, EventArgs e)
        {
            this.countTimer.Stop();
            // This is set everytime a selection is made
            if (this.dontCount)
            {
                return;
            }
            int count = this.database.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.Custom);
            this.QueryMatches.Text = count > 0 ? count.ToString() : "0";
            this.OkButton.IsEnabled = count > 0; // Dusable OK button if there are no matches

            // Uncomment this to add feedback to the File count line desribing the kinds of files selected
            //if (this.UseDetectionsCheckbox.IsChecked == false)
            //{
            //    this.QueryFileMatchNote.Text = String.Empty;
            //}
            //else
            //{
            //    if ((string)this.DetectionCategoryComboBox.SelectedItem == Constant.RecognizerValues.NoDetectionLabel)
            //    {
            //        if (this.DetectionRangeSlider.LowerValue == 1)
            //        {
            //            // Must be 1:1
            //            this.QueryFileMatchNote.Text = "(files with no recognized entities)";
            //        }
            //        else if (this.DetectionRangeSlider.HigherValue == 1)
            //        {
            //            // Must be  n:1 wbere n < 1
            //            this.QueryFileMatchNote.Text = "(files with no recognized entities or lower-probability recognitions)";
            //        }
            //        else 
            //        {
            //            // Must be  n:m wbere n,m < 1
            //            this.QueryFileMatchNote.Text = "(files with lower-probability recognitions)";
            //        }
            //    }
            //    else
            //    {
            //        this.QueryFileMatchNote.Text = "(files with a recognized entity within the confidence range)";
            //    }
            //}
        }

        // Start the timer that will show how many files match the current selection
        private void InitiateShowCountsOfMatchingFiles()
        {
            this.countTimer.Stop();
            this.countTimer.Start();
        }

        // Apply the selection if the Ok button is clicked
        private void OkButton_Click(object sender, RoutedEventArgs args)
        {
            if (GlobalReferences.DetectionsExists)
            {
                this.SetDetectionCriteria();
            }
            this.DialogResult = true;
        }

        // Cancel - exit the dialog without doing anythikng.
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion

        #region Static helpers
        private static double Round2(double? value)
        {
            return value == null ? 0 : Math.Round((double)value, 2);
        }
        #endregion

        #region EpisodeStuff - Move this code into proper regions later
        private void CheckboxShowAllEpisodeImages_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (this.database == null)
            {
                this.CheckboxShowAllEpisodeImages.IsChecked = false;
                return;
            }

            if (true == this.CheckboxShowAllEpisodeImages.IsChecked)
            {
                if (string.IsNullOrEmpty(this.NoteDataLabelContainingEpisodeData))
                {
                    // No note fields contain the expected Episode data. Disable this operation and get the heck out of here.
                    Dialogs.CustomSelectEpisodeDataLabelProblem(this.Owner);
                    this.CheckboxShowAllEpisodeImages.IsChecked = false;
                    this.database.CustomSelection.EpisodeShowAllIfAnyMatch = false;
                    return;
                }

            }
            this.database.CustomSelection.EpisodeShowAllIfAnyMatch = true == this.CheckboxShowAllEpisodeImages.IsChecked;
            this.UpdateSearchDialogFeedback();
        }

        private static bool EpisodeFieldCheckFormat(ImageRow row, string dataLabel)
        {
            Regex rgx = new Regex(@"^[0-9]+:[0-9]+\|[0-9]+$");
            string value = row.GetValueDisplayString(dataLabel);
            return (null != value && rgx.IsMatch(value));
        }

        #endregion
    }
}
