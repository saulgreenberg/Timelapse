using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.ControlsDataCommon;
using Timelapse.ControlsDataEntry;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Recognition;
using Timelapse.SearchingAndSorting;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;
using Xceed.Wpf.Toolkit.Primitives;
using Arguments = Timelapse.DataStructures.Arguments;
using Control = Timelapse.Constant.Control;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace Timelapse.Dialog
{
    /// <summary>
    /// A dialog allowing a user to create a custom selection by setting conditions on data fields.
    /// </summary>
    public partial class CustomSelectionWithEpisodes
    {
        public FileSelectionEnum FileSelection;

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

        // Variables passed into constructor
        private readonly FileDatabase Database;
        private readonly ImageRow CurrentImageRow;
        private readonly DataEntryControls DataEntryControls;
        private readonly Arguments Arguments;
        private bool dontUpdate = true;

        // The RelativePath control is implemented as a combination DropDownButton with a TreeViewRelativePathMenu as its content
        private TreeViewWithRelativePaths treeViewWithRelativePaths;
        private DropDownButton RelativePathButton;

        // Remember note fields that contain Episode data
        private string NoteDataLabelContainingEpisodeData;

        // References to the various dateTime labels and controls set when they are created later,
        // so we can switch their attributes depending on the CheckBoxUseTime state
        private TextBlock dateTimeLabel1;
        private TextBlock dateTimeLabel2;
        private DateTimePicker dateTimeControl1;
        private DateTimePicker dateTimeControl2;

        // This timer is used to delay showing count information, which could be an expensive operation, as the user may be setting values quickly
        private readonly DispatcherTimer countTimer = new DispatcherTimer();

        private RecognitionSelections DetectionSelections { get; }
        #endregion

        #region Controls created ahead of time
        // UseTime Checkbox, funciton is to specify whether the select should use a pure time range instead of a pure date range
        private readonly CheckBox CheckBoxUseTime = new CheckBox
        {
            Content = "Use time (hh:mm:ss) instead of date",
            FontWeight = FontWeights.Normal,
            FontStyle = FontStyles.Italic,
            VerticalAlignment = VerticalAlignment.Center,
            IsChecked = false,
            Width = double.NaN,
            Margin = new Thickness
            {
                Left = 10
            },
            IsEnabled = true
        };

        // And/Or RadioButtons use to combine non-standard terms
        private readonly RadioButton RadioButtonTermCombiningAnd = new RadioButton
        {
            Content = "And ",
            GroupName = "LogicalOperators",
            FontWeight = FontWeights.DemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            IsChecked = true,
            Width = Double.NaN,
            IsEnabled = false
        };
        private readonly RadioButton RadioButtonTermCombiningOr = new RadioButton
        {
            Content = "Or ",
            GroupName = "LogicalOperators",
            VerticalAlignment = VerticalAlignment.Center,
            Width = double.NaN,
            IsEnabled = false
        };
        #endregion

        #region Constructors and Loading
        public CustomSelectionWithEpisodes(Window owner, FileDatabase database, DataEntryControls dataEntryControls, ImageRow currentImageRow, RecognitionSelections detectionSelections, Arguments arguments)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(database, nameof(database));

            InitializeComponent();

            // Save the passed in parameters
            this.Owner = owner;
            this.Database = database;
            this.DataEntryControls = dataEntryControls;
            this.CurrentImageRow = currentImageRow;
            if (GlobalReferences.DetectionsExists)
            {
                this.DetectionSelections = detectionSelections; // Detections-specific
            }
            this.Arguments = arguments;

            // Set up the count timer
            countTimer.Interval = TimeSpan.FromMilliseconds(500);
            countTimer.Tick += CountTimer_Tick;
        }

        // When the window is loaded, construct all the SearchTerm controls 
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Used to track whether we are on the 1st or 2nd dateTime control
            bool firstDateTimeControlSeen = false;
            // Adds the callback to this checkbox
            CheckBoxUseTime.Checked += CheckBoxUseTime_CheckChanged;
            CheckBoxUseTime.Unchecked += CheckBoxUseTime_CheckChanged;

            // Adjust this dialog window position 
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            // Detections-specific
            dontCount = true;
            dontInvoke = true;

            // Set the state of the detections to the last used ones (or to its defaults)
            if (GlobalReferences.DetectionsExists)
            {
                this.ButtonRecognitionExplorer.Visibility = Visibility.Visible;
                RecognitionsGroupBox.Visibility = Visibility.Visible;
                Recognitions2Panel.Visibility = Visibility.Visible;
                EnableRecognitionsCheckbox.IsChecked = DetectionSelections.UseRecognition;

                // Set the spinner and sliders to the last used values
                DetectionConfidenceSpinnerLower.Value = DetectionSelections.ConfidenceThreshold1ForUI;
                DetectionConfidenceSpinnerHigher.Value = DetectionSelections.ConfidenceThreshold2ForUI;
                DetectionRangeSlider.LowerValue = DetectionSelections.ConfidenceThreshold1ForUI;
                DetectionRangeSlider.HigherValue = DetectionSelections.ConfidenceThreshold2ForUI;

                // Set the Rank by Confidence
                RankByDetectionConfidenceCheckbox.IsChecked = DetectionSelections.RankByConfidence;

                // Put Detection and Classification categories in the combo box as human-readable labels
                // Note that we add "All" to the Detections list as that is a 'bogus' Timelapse-internal category.
                List<string> labels = Database.GetDetectionLabels();
                DetectionCategoryComboBox.Items.Add(RecognizerValues.AllDetectionLabel);
                foreach (string label in labels)
                {
                    DetectionCategoryComboBox.Items.Add(label);
                }

                if (GlobalReferences.UseClassifications)
                {
                    // Now add classifications
                    labels = Database.GetClassificationLabels();
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
                        DetectionCategoryComboBox.Items.Add(separator);
                        foreach (string label in labels)
                        {
                            DetectionCategoryComboBox.Items.Add(label);
                        }
                    }
                }

                // Set the combobox selection to the last used one.
                string categoryLabel;
                if (DetectionSelections.RecognitionType == RecognitionType.Empty)
                {
                    // If we don't know the recognition type, default to All
                    DetectionCategoryComboBox.SelectedValue = RecognizerValues.AllDetectionLabel;
                    RankByDetectionConfidenceCheckbox.Content = "by detection confidence";
                }
                else if (DetectionSelections.RecognitionType == RecognitionType.Detection)
                {
                    categoryLabel = Database.GetDetectionLabelFromCategory(DetectionSelections.DetectionCategory);
                    if (string.IsNullOrEmpty(DetectionSelections.DetectionCategory) || (DetectionSelections.AllDetections && !DetectionSelections.InterpretAllDetectionsAsEmpty))
                    {
                        // We need an 'All' detection category, which is the union of all categories (except empty).
                        // Because All is a bogus detection category (since its not part of the detection data), we have to set it explicitly
                        DetectionCategoryComboBox.SelectedValue = RecognizerValues.AllDetectionLabel;
                    }
                    else
                    {
                        DetectionCategoryComboBox.SelectedValue = categoryLabel;
                    }
                    RankByDetectionConfidenceCheckbox.Content = "by detection confidence";
                }
                else
                {
                    categoryLabel = Database.GetClassificationLabelFromCategory(DetectionSelections.ClassificationCategory);
                    DetectionCategoryComboBox.SelectedValue = (categoryLabel.Length != 0)
                        ? categoryLabel
                        : DetectionCategoryComboBox.SelectedValue = RecognizerValues.AllDetectionLabel;
                    RankByDetectionConfidenceCheckbox.Content = "by classification confidence";
                }
                EnableDetectionControls((bool)EnableRecognitionsCheckbox.IsChecked);
            }
            else
            {
                RecognitionsGroupBox.Visibility = Visibility.Collapsed;
                Recognitions2Panel.Visibility = Visibility.Collapsed;
                DetectionSelections?.ClearAllDetectionsUses();
            }
            dontInvoke = false;
            dontCount = false;
            if (GlobalReferences.DetectionsExists)
            {
                SetDetectionCriteria();
                ShowMissingDetectionsCheckbox.IsChecked = Database.CustomSelection.ShowMissingDetections;
  
            }

            // Episode-related:
            // Check if there is an episode data field and if so, enable the appropriate checkbox and its value
            // TODO Cleanup up the episode stuff 
            // TODO: Why are we bothering with the Database.CustomSelection.EpisodeNoteField if we recreate its value each time?
            // TODO: But if we use it, then we have to consider whether it actually holds a valid episode... or whether that field even exists anymore (e.g., due to a change in the template)
            bool isEpisodeAvailable = false;
            
            NoteDataLabelContainingEpisodeData = string.Empty;
            foreach (ControlRow control in Database.Controls)
            {
                if (control.Type == Control.Note && EpisodeFieldCheckFormat(CurrentImageRow, control.DataLabel))
                {
                    // We found a note data label whose value in the current image follows the expected Episode format.
                    // So save it
                    NoteDataLabelContainingEpisodeData = control.DataLabel;
                    Database.CustomSelection.EpisodeNoteField = control.DataLabel;
                    isEpisodeAvailable = true;
                    break;
                }
            }
            if (Database.CustomSelection.EpisodeShowAllIfAnyMatch && isEpisodeAvailable)
            {
                // Only check the checkbox if it was previously checked and the data field still contains valid Episode data
                CheckboxShowAllEpisodeImages.IsChecked = Database.CustomSelection.EpisodeShowAllIfAnyMatch;
            }
            // The episode controls are only enabled if detections is enabled
            CheckboxShowAllEpisodeImages.FontWeight = isEpisodeAvailable ? FontWeights.Normal : FontWeights.Light;
            CheckboxShowAllEpisodeImages.IsEnabled = isEpisodeAvailable;

            InitiateShowCountsOfMatchingFiles();
            DetectionCategoryComboBox.SelectionChanged += DetectionCategoryComboBox_SelectionChanged;

            // Selection-specific
            dontUpdate = true;

            // ConfigureFormatForDateTimeCustom the And vs Or conditional Radio Buttons
            if (Database.CustomSelection.TermCombiningOperator == CustomSelectionOperatorEnum.And)
            {
                RadioButtonTermCombiningAnd.IsChecked = true;
                RadioButtonTermCombiningOr.IsChecked = false;
            }
            else
            {
                RadioButtonTermCombiningAnd.IsChecked = false;
                RadioButtonTermCombiningOr.IsChecked = true;
            }
            RadioButtonTermCombiningAnd.Checked += AndOrRadioButton_Checked;
            RadioButtonTermCombiningOr.Checked += AndOrRadioButton_Checked;

            // Create a new row for each search term. 
            // Each row specifies a particular control and how it can be searched
            // Note that the search terms are expected to be in a specific order i.e.
            // - the core standard controls defined by Timelapse
            // - the nonStandard controls defined by whoever customized the template 
            int gridRowIndex = 0;
            bool noSeparatorCreated = true;
            foreach (SearchTerm searchTerm in Database.CustomSelection.SearchTerms)
            {
                // start at 1 as there is already a header row
                ++gridRowIndex;
                RowDefinition gridRow = new RowDefinition
                {
                    Height = GridLength.Auto
                };
                SearchTerms.RowDefinitions.Add(gridRow);

                // USE Column: A checkbox to indicate whether the current search row should be used as part of the search
                Thickness thickness = new Thickness(5, 2, 5, 2);
                CheckBox useCurrentRow = new CheckBox
                {
                    FontWeight = FontWeights.DemiBold,
                    Margin = thickness,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    IsChecked = searchTerm.UseForSearching
                };

                // As we want to disabled the relative path Use checkbox if there are no folders,
                // we need to remember it so we can do that after we create the relativePathControl
                CheckBox checkboxforUsingRelativePath = null;
                if (searchTerm.Label == DatabaseColumn.RelativePath)
                {
                    checkboxforUsingRelativePath = useCurrentRow;

                    if (this.Arguments.ConstrainToRelativePath)
                    {
                        // TODO This is wrong as we want to be able to choose subfolders udner the constrained relative path
                        // I think its corrected in the menu, but need to test this.
                        useCurrentRow.IsChecked = true;
                        useCurrentRow.IsEnabled = false;
                    }
                }

                useCurrentRow.Checked += Select_CheckedOrUnchecked;
                useCurrentRow.Unchecked += Select_CheckedOrUnchecked;
                Grid.SetRow(useCurrentRow, gridRowIndex);
                Grid.SetColumn(useCurrentRow, SelectColumn);
                SearchTerms.Children.Add(useCurrentRow);

                // LABEL column: The label associated with the control (Note: not the data label)
                TextBlock controlLabel = new TextBlock
                {
                    FontWeight = searchTerm.UseForSearching ? FontWeights.DemiBold : FontWeights.Normal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5)
                };

                switch (searchTerm.Label)
                {
                    case DatabaseColumn.DateTime:
                        {
                            // Change DateTime to Date
                            controlLabel.Text = ControlDeprecated.DateLabel;

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

                            break;
                        }
                    case DatabaseColumn.RelativePath:
                        // RelativePath label adds details
                        controlLabel.Inlines.Add(searchTerm.Label + " folder");
                        controlLabel.Inlines.Add(new Run(Environment.NewLine + "includes subfolders") { FontStyle = FontStyles.Italic, FontSize = 10 });
                        break;
                    default:
                        // Just use the label's name
                        controlLabel.Text = searchTerm.Label;
                        break;
                }
                Grid.SetRow(controlLabel, gridRowIndex);
                Grid.SetColumn(controlLabel, LabelColumn);
                SearchTerms.Children.Add(controlLabel);

                // The operators allowed for each search term type
                string controlType = searchTerm.ControlType;
                string[] termOperators;
                switch (controlType)
                {
                    case Control.Counter:
                    case Control.IntegerAny:
                    case Control.IntegerPositive:
                    case Control.DecimalAny:
                    case Control.DecimalPositive:
                    case DatabaseColumn.DateTime:
                    case Control.FixedChoice:
                    case Control.DateTime_:
                    case Control.Date_:
                    case Control.Time_:
                        // No globs in Counters or Integers as that text field only allows numbers, we can't enter the special characters Glob required
                        // No globs in Dates the date entries are constrained by the date picker
                        // No globs in Fixed Choices as choice entries are constrained by menu selection
                        termOperators = new[]
                        {
                            SearchTermOperator.Equal,
                            SearchTermOperator.NotEqual,
                            SearchTermOperator.LessThan,
                            SearchTermOperator.GreaterThan,
                            SearchTermOperator.LessThanOrEqual,
                            SearchTermOperator.GreaterThanOrEqual
                        };
                        break;
                    case Control.MultiChoice:
                        termOperators = new[]
                        {
                            SearchTermOperator.Equal,
                            SearchTermOperator.NotEqual,
                            SearchTermOperator.Includes,
                            SearchTermOperator.Excludes
                        };
                        break;
                    // Relative path only allows = (this will be converted later to a glob to get subfolders) 
                    case DatabaseColumn.RelativePath:
                        // Only equals (actually a glob including subfolders), as other options don't make sense for RelatvePath
                        termOperators = new[]
                        {
                            SearchTermOperator.Equal,
                        };
                        break;
                    // Only equals and not equals (For relative path this will be converted later to a glob to get subfolders) 
                    case DatabaseColumn.DeleteFlag:
                    case Control.Flag:
                        // Only equals and not equals in Flags, as other options don't make sense for booleans
                        termOperators = new[]
                        {
                            SearchTermOperator.Equal,
                            SearchTermOperator.NotEqual
                        };
                        break;


                    default:
                        termOperators = new[]
                        {
                            SearchTermOperator.Equal,
                            SearchTermOperator.NotEqual,
                            SearchTermOperator.LessThan,
                            SearchTermOperator.GreaterThan,
                            SearchTermOperator.LessThanOrEqual,
                            SearchTermOperator.GreaterThanOrEqual,
                            SearchTermOperator.Glob,
                            SearchTermOperator.NotGlob
                        };
                        break;
                }

                // term operator combo box
                ComboBox operatorsComboBox = new ComboBox
                {
                    FontWeight = FontWeights.Normal,
                    IsEnabled = searchTerm.UseForSearching,
                    ItemsSource = termOperators,
                    Margin = thickness,
                    Width = 78,
                    Height = 25,
                    SelectedValue = searchTerm.Operator
                };
                operatorsComboBox.SelectionChanged += Operator_SelectionChanged; // Create the callback that is invoked whenever the user changes the expresison
                Grid.SetRow(operatorsComboBox, gridRowIndex);
                Grid.SetColumn(operatorsComboBox, OperatorColumn);
                SearchTerms.Children.Add(operatorsComboBox);

                switch (controlType)
                {
                    // Value column: The value used for comparison in the search
                    // Notes and Counters both uses a text field, so they can be constructed as a textbox
                    // However, counter textboxes are modified to only allow integer input (both direct typing or pasting are checked)

                    // RelativePath
                    case DatabaseColumn.RelativePath:
                        // Relative path uses a dropdown to show existing folders within a specialized treeview control
                        // As the RelativePath control is somewhat complex to set up, we create it in its on method
                        this.RelativePathCreateControl(searchTerm, thickness, gridRowIndex, checkboxforUsingRelativePath);
                        break;

                    // DateTime
                    case DatabaseColumn.DateTime:
                        DateTime dateTime = Database.CustomSelection.GetDateTimePLAINVERSION(gridRowIndex - 1);
                        // The DateTime Picker is set to show only the date portion
                        DateTimePicker dateValue = new DateTimePicker
                        {
                            FontWeight = FontWeights.Normal,
                            Format = DateTimeFormat.Custom,
                            FormatString = Time.DateDisplayFormat,
                            IsEnabled = searchTerm.UseForSearching,
                            Width = DefaultControlWidth,
                            CultureInfo = CultureInfo.CreateSpecificCulture("en-US"),
                            Value = dateTime,
                            TimePickerVisibility = Visibility.Collapsed
                        };
                        // Remember the DateTime controls so we can switch whether they show Date or Time when the CheckboxUseTime is checked/unchecked
                        if (dateTimeControl1 == null)
                        {
                            // must be the first dateValue
                            dateTimeControl1 = dateValue;
                        }
                        else
                        {
                            // must be the 2nd dateValue
                            dateTimeControl2 = dateValue;
                        }

                        dateValue.ValueChanged += DateTime_SelectedDateChanged;
                        dateValue.GotFocus += ControlsDataHelpersCommon.Control_GotFocus;
                        dateValue.LostFocus += ControlsDataHelpersCommon.Control_LostFocus;
                        Grid.SetRow(dateValue, gridRowIndex);
                        Grid.SetColumn(dateValue, ValueColumn);
                        SearchTerms.Children.Add(dateValue);
                        break;

                    // File, Note, Alphanumeric
                    case DatabaseColumn.File:
                    case Control.Note:
                    case Control.AlphaNumeric:
                        {
                            AutocompleteTextBox textBoxValue = new AutocompleteTextBox
                            {
                                FontWeight = FontWeights.Normal,
                                Autocompletions = null,
                                IsEnabled = searchTerm.UseForSearching,
                                Text = searchTerm.DatabaseValue,
                                Margin = thickness,
                                Width = DefaultControlWidth,
                                Height = 22,
                                TextWrapping = TextWrapping.NoWrap,
                                VerticalAlignment = VerticalAlignment.Center,
                                VerticalContentAlignment = VerticalAlignment.Center
                            };
                            if (controlType == Control.Note ||
                                controlType == Control.AlphaNumeric)
                            {
                                // Add existing autocompletions for this control
                                textBoxValue.Autocompletions = DataEntryControls.AutocompletionGetForNote(searchTerm.DataLabel);
                            }

                            if (controlType == Control.AlphaNumeric)
                            {
                                textBoxValue.PreviewKeyDown += ValidationCallbacks.PreviewKeyDown_TextBoxNoSpaces;
                                textBoxValue.PreviewTextInput += ValidationCallbacks.PreviewInput_AlphaNumericCharacterOnlyWithGlob;
                                textBoxValue.TextChanged += ValidationCallbacks.TextChanged_AlphaNumericTextWithGlobCharactersOnly;
                            }
                            textBoxValue.TextChanged += Note_TextChanged;
                            textBoxValue.GotFocus += ControlsDataHelpersCommon.Control_GotFocus;
                            textBoxValue.LostFocus += ControlsDataHelpersCommon.Control_LostFocus;
                            Grid.SetRow(textBoxValue, gridRowIndex);
                            Grid.SetColumn(textBoxValue, ValueColumn);
                            SearchTerms.Children.Add(textBoxValue);
                            break;
                        }

                    case Control.MultiLine:
                        {
                            MultiLineTextEditor multiLineValue = new MultiLineTextEditor
                            {
                                FontWeight = FontWeights.Normal,
                                IsEnabled = searchTerm.UseForSearching,
                                Text = searchTerm.DatabaseValue,
                                Content = searchTerm.DatabaseValue,
                                Margin = thickness,
                                Width = DefaultControlWidth,
                                Height = 22,
                                TextWrapping = TextWrapping.NoWrap,
                                VerticalAlignment = VerticalAlignment.Center,
                                VerticalContentAlignment = VerticalAlignment.Top,
                                HorizontalContentAlignment = HorizontalAlignment.Left,
                                Style = (Style)DataEntryControls.FindResource("MultiLineBox"),
                            };
                            multiLineValue.TextHasChanged += MultiLineValue_TextHasChanged;
                            Grid.SetRow(multiLineValue, gridRowIndex);
                            Grid.SetColumn(multiLineValue, ValueColumn);
                            SearchTerms.Children.Add(multiLineValue);
                            break;
                        }

                    // Counter IntegerAny IntegerPositive
                    case Control.Counter:
                    case Control.IntegerAny:
                    case Control.IntegerPositive:
                        IntegerUpDown integerUpDownBoxValue = new IntegerUpDown
                        {
                            FontWeight = FontWeights.Normal,
                            IsEnabled = searchTerm.UseForSearching,
                            Margin = thickness,
                            Width = DefaultControlWidth,
                            Height = 22,
                            VerticalAlignment = VerticalAlignment.Center,
                            VerticalContentAlignment = VerticalAlignment.Center,
                            Minimum = controlType == Control.IntegerAny ? Int32.MinValue : 0
                        };
                        if (Int32.TryParse(searchTerm.DatabaseValue, out int intValue))
                        {
                            integerUpDownBoxValue.Text = searchTerm.DatabaseValue;
                            integerUpDownBoxValue.Value = intValue;
                        }
                        else
                        {
                            integerUpDownBoxValue.Text = "0";
                            integerUpDownBoxValue.Value = 0;
                        }

                        if (controlType == Control.IntegerAny)
                        {
                            integerUpDownBoxValue.PreviewTextInput += ValidationCallbacks.PreviewInput_IntegerCharacterOnly;
                            DataObject.AddPastingHandler(integerUpDownBoxValue, ValidationCallbacks.Paste_OnlyIfIntegerAny);
                        }
                        else
                        {
                            integerUpDownBoxValue.PreviewTextInput += ValidationCallbacks.PreviewInput_IntegerPositiveCharacterOnly;
                            DataObject.AddPastingHandler(integerUpDownBoxValue, ValidationCallbacks.Paste_OnlyIfIntegerPositive);
                        }
                        integerUpDownBoxValue.PreviewKeyDown += ValidationCallbacks.PreviewKeyDown_IntegerUpDownNoSpaces;
                        integerUpDownBoxValue.ValueChanged += Integer_ValueChanged;
                        integerUpDownBoxValue.GotFocus += ControlsDataHelpersCommon.Control_GotFocus;
                        integerUpDownBoxValue.LostFocus += ControlsDataHelpersCommon.Control_LostFocus;
                        Grid.SetRow(integerUpDownBoxValue, gridRowIndex);
                        Grid.SetColumn(integerUpDownBoxValue, ValueColumn);
                        SearchTerms.Children.Add(integerUpDownBoxValue);
                        break;

                    // DecimalAny DecimalPositive
                    case Control.DecimalAny:
                    case Control.DecimalPositive:
                        DoubleUpDown doubleUpDownBoxValue = new DoubleUpDown
                        {
                            FontWeight = FontWeights.Normal,
                            IsEnabled = searchTerm.UseForSearching,
                            Text = searchTerm.DatabaseValue,
                            Margin = thickness,
                            Width = DefaultControlWidth,
                            Height = 22,
                            VerticalAlignment = VerticalAlignment.Center,
                            VerticalContentAlignment = VerticalAlignment.Center,
                            Minimum = controlType == Control.DecimalAny ? Double.MinValue : 0
                        };

                        if (Double.TryParse(searchTerm.DatabaseValue, out double doubleValue))
                        {
                            doubleUpDownBoxValue.Text = searchTerm.DatabaseValue;
                            doubleUpDownBoxValue.Value = doubleValue;
                        }
                        else
                        {
                            doubleUpDownBoxValue.Text = "0";
                            doubleUpDownBoxValue.Value = 0;
                        }

                        if (controlType == Control.DecimalPositive)
                        {
                            doubleUpDownBoxValue.PreviewTextInput += ValidationCallbacks.PreviewInput_DecimalPositiveCharacterOnly;
                            DataObject.AddPastingHandler(doubleUpDownBoxValue, ValidationCallbacks.Paste_OnlyIfDecimalPositive);
                        }
                        else
                        {
                            doubleUpDownBoxValue.PreviewTextInput += ValidationCallbacks.PreviewInput_DecimalCharacterOnly;
                            DataObject.AddPastingHandler(doubleUpDownBoxValue, ValidationCallbacks.Paste_OnlyIfDecimalAny);
                        }
                        doubleUpDownBoxValue.PreviewKeyDown += ValidationCallbacks.PreviewKeyDown_DecimalUpDownNoSpaces;
                        doubleUpDownBoxValue.ValueChanged += Decimal_ValueChanged;
                        doubleUpDownBoxValue.GotFocus += ControlsDataHelpersCommon.Control_GotFocus;
                        doubleUpDownBoxValue.LostFocus += ControlsDataHelpersCommon.Control_LostFocus;
                        Grid.SetRow(doubleUpDownBoxValue, gridRowIndex);
                        Grid.SetColumn(doubleUpDownBoxValue, ValueColumn);
                        SearchTerms.Children.Add(doubleUpDownBoxValue);
                        break;

                    case Control.FixedChoice:
                        {
                            // FixedChoice presents combo boxes, so they can be constructed the same way
                            ComboBox comboBoxValue = new ComboBox
                            {
                                FontWeight = FontWeights.Normal,
                                IsEnabled = searchTerm.UseForSearching,
                                Width = DefaultControlWidth,
                                Margin = thickness,

                                // Create the dropdown menu 
                                ItemsSource = searchTerm.List,
                                SelectedItem = searchTerm.DatabaseValue
                            };
                            comboBoxValue.SelectionChanged += FixedChoice_SelectionChanged;
                            comboBoxValue.GotFocus += ControlsDataHelpersCommon.Control_GotFocus;
                            comboBoxValue.LostFocus += ControlsDataHelpersCommon.Control_LostFocus;
                            Grid.SetRow(comboBoxValue, gridRowIndex);
                            Grid.SetColumn(comboBoxValue, ValueColumn);
                            SearchTerms.Children.Add(comboBoxValue);
                            break;
                        }
                    case Control.MultiChoice:
                        {
                            // MultiChoice presents checkCombo boxes, so they can be constructed the same way
                            // Remove the empty item from the list
                            List<string> newList = new List<string>(searchTerm.List);
                            newList.Remove(string.Empty);
                            CheckComboBox checkComboBoxValue = new CheckComboBox
                            {
                                FontWeight = FontWeights.Normal,
                                IsEnabled = searchTerm.UseForSearching,
                                Width = DefaultControlWidth,
                                Margin = thickness,
                                // Create the dropdown menu 
                                ItemsSource = newList,
                            };
                            // Populate the combobox menu
                            checkComboBoxValue.Opened += ControlsDataHelpersCommon.CheckComboBox_DropDownOpened;
                            checkComboBoxValue.Closed += ControlsDataHelpersCommon.CheckComboBox_DropDownClosed;
                            checkComboBoxValue.ItemSelectionChanged += CheckComboBox_ItemSelectionChanged;
                            checkComboBoxValue.GotFocus += ControlsDataHelpersCommon.Control_GotFocus;
                            checkComboBoxValue.LostFocus += ControlsDataHelpersCommon.Control_LostFocus;
                            checkComboBoxValue.Text = searchTerm.DatabaseValue;
                            Grid.SetRow(checkComboBoxValue, gridRowIndex);
                            Grid.SetColumn(checkComboBoxValue, ValueColumn);
                            SearchTerms.Children.Add(checkComboBoxValue);
                            break;
                        }
                    case DatabaseColumn.DeleteFlag:
                    case Control.Flag:
                        {
                            // Flags present checkboxes
                            CheckBox flagCheckBox = new CheckBox
                            {
                                FontWeight = FontWeights.Normal,
                                Margin = thickness,
                                VerticalAlignment = VerticalAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                IsChecked = !String.Equals(searchTerm.DatabaseValue, BooleanValue.False, StringComparison.OrdinalIgnoreCase),
                                IsEnabled = searchTerm.UseForSearching
                            };
                            flagCheckBox.Checked += Flag_CheckedOrUnchecked;
                            flagCheckBox.Unchecked += Flag_CheckedOrUnchecked;
                            flagCheckBox.GotFocus += ControlsDataHelpersCommon.Control_GotFocus;
                            flagCheckBox.LostFocus += ControlsDataHelpersCommon.Control_LostFocus;
                            searchTerm.DatabaseValue = flagCheckBox.IsChecked.Value ? BooleanValue.True : BooleanValue.False;
                            Grid.SetRow(flagCheckBox, gridRowIndex);
                            Grid.SetColumn(flagCheckBox, ValueColumn);
                            SearchTerms.Children.Add(flagCheckBox);
                            break;
                        }

                    case Control.DateTime_:
                        DateTimePicker dateTimePicker = DateTimeHandler.TryParseDisplayDateTime(searchTerm.DatabaseValue, out DateTime dateTimeCustom)
                            ? CreateControls.CreateDateTimePicker(String.Empty, DateTimeFormatEnum.DateAndTime, dateTimeCustom)
                            : CreateControls.CreateDateTimePicker(String.Empty, DateTimeFormatEnum.DateAndTime, ControlDefault.DateTimeCustomDefaultValue);
                        dateTimePicker.FontWeight = FontWeights.Normal;
                        dateTimePicker.Width = DefaultControlWidth;
                        dateTimePicker.ValueChanged += DateTimeCustomPicker_ValueChanged;
                        Grid.SetRow(dateTimePicker, gridRowIndex);
                        Grid.SetColumn(dateTimePicker, ValueColumn);
                        SearchTerms.Children.Add(dateTimePicker);
                        break;

                    case Control.Date_:
                        DateTimePicker datePicker = DateTimeHandler.TryParseDisplayDate(searchTerm.DatabaseValue, out DateTime date)
                            ? CreateControls.CreateDateTimePicker(String.Empty, DateTimeFormatEnum.DateOnly, date)
                            : CreateControls.CreateDateTimePicker(String.Empty, DateTimeFormatEnum.DateOnly, ControlDefault.Date_DefaultValue);
                        datePicker.FontWeight = FontWeights.Normal;
                        datePicker.Width = DefaultControlWidth;
                        datePicker.ValueChanged += DatePicker_ValueChanged;
                        Grid.SetRow(datePicker, gridRowIndex);
                        Grid.SetColumn(datePicker, ValueColumn);
                        SearchTerms.Children.Add(datePicker);
                        break;
                    case Control.Time_:
                        TimePicker timePicker = DateTimeHandler.TryParseDatabaseTime(searchTerm.DatabaseValue, out DateTime time)
                            ? CreateControls.CreateTimePicker(String.Empty, time)
                            : CreateControls.CreateTimePicker(String.Empty, ControlDefault.Time_DefaultValue);
                        timePicker.FontWeight = FontWeights.Normal;
                        timePicker.Width = DefaultControlWidth;
                        timePicker.ValueChanged += TimePicker_ValueChanged;
                        Grid.SetRow(timePicker, gridRowIndex);
                        Grid.SetColumn(timePicker, ValueColumn);
                        SearchTerms.Children.Add(timePicker);
                        break;
                    default:
                        throw new NotSupportedException($"Unhandled control type '{controlType}'.");
                }

                if (searchTerm.DataLabel == DatabaseColumn.DateTime && firstDateTimeControlSeen == false)
                {
                    // Display the CheckBoxUseTime control next to the DateTime expression
                    Grid.SetRow(CheckBoxUseTime, gridRowIndex);
                    Grid.SetColumn(CheckBoxUseTime, SearchCriteriaColumn);
                    Grid.SetRowSpan(CheckBoxUseTime, 2);
                    SearchTerms.Children.Add(CheckBoxUseTime);
                    firstDateTimeControlSeen = true;
                }

                // Conditional And/Or column
                // If we are  on the first term after the non-standard controls
                // - create the and/or buttons in the last column, which lets a user determine how to combine the remaining terms
                if (noSeparatorCreated && false == (searchTerm.DataLabel == DatabaseColumn.File ||
                              searchTerm.DataLabel == DatabaseColumn.RelativePath || searchTerm.DataLabel == DatabaseColumn.DateTime ||
                              searchTerm.DataLabel == DatabaseColumn.DeleteFlag))
                {
                    StackPanel sp = CreateAndOrButtons();
                    Grid.SetRow(sp, gridRowIndex);
                    Grid.SetColumn(sp, SearchCriteriaColumn);
                    Grid.SetRowSpan(sp, 2);
                    SearchTerms.Children.Add(sp);
                    noSeparatorCreated = false;
                }

                // If we are  on the first term in the standard controls
                // - create a description that tells the user how these will be combined
                if (gridRowIndex == 1 && noSeparatorCreated)
                {
                    StackPanel sp = CreateStandardControlDescription();
                    Grid.SetRow(sp, gridRowIndex);
                    Grid.SetColumn(sp, SearchCriteriaColumn);
                    Grid.SetRowSpan(sp, 2);
                    SearchTerms.Children.Add(sp);
                }
            }
            dontUpdate = false;
            UpdateSearchDialogFeedback();

 
            // Set the UseTime state based on what was last recorded
            CheckBoxUseTime.IsChecked = Database.CustomSelection.UseTimeInsteadOfDate;

            // Set the selected item to the Note field with episode data in it.
            Database.CustomSelection.EpisodeNoteField = NoteDataLabelContainingEpisodeData;
            Database.CustomSelection.EpisodeShowAllIfAnyMatch = CheckboxShowAllEpisodeImages.IsChecked == true;
        }
        #endregion

        #region CheckBoxUseTime Callbacks
        // Toggle whether we are using dates or times
        private void CheckBoxUseTime_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                // Remember the checkbox state
                Database.CustomSelection.UseTimeInsteadOfDate = cb.IsChecked == true;

                // The DateTime label should reflect the state
                if (dateTimeLabel1 != null)
                {
                    dateTimeLabel1.Text = cb.IsChecked == true ? "Time" : "Date";
                }
                if (dateTimeLabel2 != null)
                {
                    dateTimeLabel2.Text = cb.IsChecked == true ? "Time" : "Date";
                }

                // The DateTime control should reflect the state by displaying Time or Date only input
                if (dateTimeControl1 != null)
                {
                    dateTimeControl1.TimePickerVisibility = cb.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                    dateTimeControl1.ShowDropDownButton = cb.IsChecked == false;
                    dateTimeControl1.FormatString = cb.IsChecked == true ? Time.TimeInputFormat : Time.DateDisplayFormat;
                }
                if (dateTimeControl2 != null)
                {
                    dateTimeControl2.TimePickerVisibility = cb.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                    dateTimeControl2.ShowDropDownButton = cb.IsChecked == false;
                    dateTimeControl2.FormatString = cb.IsChecked == true ? Time.TimeInputFormat : Time.DateDisplayFormat;
                }
                // Update the count. We could be a bit more efficient by checking to see if either of these controls have their 'Use'checkboxes unchecked,
                // but its not worth the bother.
                InitiateShowCountsOfMatchingFiles();
            }
        }
        #endregion

        #region Create Multiple Term column entries (And/Or buttons)
        // Create the AND/OR Radio buttons to set how non-standard controls are combined i.e., with AND or OR SQL queries
        // The actual radio buttons are defined above as instance variables
        private StackPanel CreateAndOrButtons()
        {
            // Separator
            Separator separator = new Separator
            {
                Width = double.NaN
            };

            // Haader text
            TextBlock tbHeader = new TextBlock
            {
                Text = "Choose how terms are combined using either",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Normal,
            };

            // And control
            StackPanel spAnd = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Width = double.NaN
            };

            TextBlock tbAnd = new TextBlock
            {
                Text = "to match all selected conditions",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Normal,
            };
            spAnd.Children.Add(RadioButtonTermCombiningAnd);
            spAnd.Children.Add(tbAnd);

            // Or control
            StackPanel spOr = new StackPanel
            {
                Name = "TermCombiningOr",
                Orientation = Orientation.Horizontal,
                Width = Double.NaN,
            };

            TextBlock tbOr = new TextBlock
            {
                Text = "to match at least one selected conditions",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Normal,
            };
            spOr.Children.Add(RadioButtonTermCombiningOr);
            spOr.Children.Add(tbOr);

            // Container for above
            StackPanel sp = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(10, 0, 0, 0),
                Width = double.NaN
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
            Separator separator = new Separator
            {
                Width = double.NaN
            };

            // Haader text
            TextBlock tbHeader = new TextBlock
            {
                Text = "These terms are combined using AND:" + Environment.NewLine + "returned files match all selected conditions.",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Normal,
            };

            // Container for above
            StackPanel sp = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(10, 0, 0, 0),
                Width = double.NaN
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
            Database.CustomSelection.TermCombiningOperator = (radioButton == RadioButtonTermCombiningAnd) ? CustomSelectionOperatorEnum.And : CustomSelectionOperatorEnum.Or;
            UpdateSearchDialogFeedback();
        }

        // Select: When the use checks or unchecks the checkbox for a row
        // - activate or deactivate the search criteria for that row
        // - update the searchterms to reflect the new status 
        // - update the UI to activate or deactivate (or show or hide) its various search terms
        private void Select_CheckedOrUnchecked(object sender, RoutedEventArgs args)
        {
            if (sender is CheckBox select == false)
            {
                // This shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }

            int row = Grid.GetRow(select);  // And you have the row number...

            SearchTerm searchterms = Database.CustomSelection.SearchTerms[row - 1];
            searchterms.UseForSearching = select.IsChecked == true;

            TextBlock label = GetGridElement<TextBlock>(LabelColumn, row);
            ComboBox expression = GetGridElement<ComboBox>(OperatorColumn, row);
            UIElement value = GetGridElement<UIElement>(ValueColumn, row);

            label.FontWeight = select.IsChecked == true ? FontWeights.DemiBold : FontWeights.Normal;
            expression.IsEnabled = select.IsChecked == true;
            value.IsEnabled = select.IsChecked == true;

            UpdateSearchDialogFeedback();
        }

        // Operator: The user has selected a new expression
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void Operator_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            if (sender is ComboBox comboBox == false)
            {
                // This shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            int row = Grid.GetRow(comboBox);  // Get the row number...
            Database.CustomSelection.SearchTerms[row - 1].Operator = comboBox.SelectedValue.ToString(); // Set the corresponding expression to the current selection
            UpdateSearchDialogFeedback();
        }

        // Value (Counters and Notes): The user has selected a new value
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void Note_TextChanged(object sender, TextChangedEventArgs args)
        {
            if (sender is TextBox textBox == false)
            {
                // This shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            int row = Grid.GetRow(textBox);  // Get the row number...
            Database.CustomSelection.SearchTerms[row - 1].DatabaseValue = textBox.Text;
            UpdateSearchDialogFeedback();
        }

        // Value (Counters and Notes): The user has selected a new value
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 

        private void MultiLineValue_TextHasChanged(object sender, EventArgs e)
        {
            if (sender is MultiLineTextEditor textBox == false)
            {
                // This shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            int row = Grid.GetRow(textBox);  // Get the row number...
            Database.CustomSelection.SearchTerms[row - 1].DatabaseValue = textBox.Text;
            UpdateSearchDialogFeedback();
        }

        private void Integer_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> args)
        {
            if (sender is IntegerUpDown textBox == false)
            {
                // This shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            int row = Grid.GetRow(textBox);  // Get the row number...
            Database.CustomSelection.SearchTerms[row - 1].DatabaseValue = textBox.Text;
            UpdateSearchDialogFeedback();
        }

        private void Decimal_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> args)
        {
            if (sender is DoubleUpDown textBox == false)
            {
                // This shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            int row = Grid.GetRow(textBox);  // Get the row number...
            Database.CustomSelection.SearchTerms[row - 1].DatabaseValue = textBox.Text;
            UpdateSearchDialogFeedback();
        }

        // Value (DateTime): we need to construct a string DateTime from it
        private void DateTime_SelectedDateChanged(object sender, RoutedPropertyChangedEventArgs<object> args)
        {
            if (sender is DateTimePicker datePicker == false)
            {
                TracePrint.NullException(nameof(sender));
                return;
            }
            if (datePicker.Value.HasValue)
            {
                int row = Grid.GetRow(datePicker);
                // Set the DateTime from the updated value, regardless of whether the UseTime checkbox is checked.
                // This stores both the date and the time.
                // Later, the search itself will check whether the UseTime is true or false to determine whether it should parse out the date or time portion.
                Database.CustomSelection.SetDateTime(row - 1, datePicker.Value.Value);
                UpdateSearchDialogFeedback();
            }
        }

        // Value (FixedChoice): The user has selected a new value 
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void FixedChoice_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            if (sender is ComboBox comboBox == false)
            {
                // This shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }

            int row = Grid.GetRow(comboBox);  // Get the row number...
            if (comboBox.SelectedValue == null)
            {
                return;
            }
            Database.CustomSelection.SearchTerms[row - 1].DatabaseValue = comboBox.SelectedValue.ToString(); // Set the corresponding value to the current selection
            UpdateSearchDialogFeedback();
        }

        // Value: (MultiChoice)
        private void CheckComboBox_ItemSelectionChanged(object sender, ItemSelectionChangedEventArgs e)
        {
            if (sender is CheckComboBox checkComboBox == false)
            {
                TracePrint.NullException(nameof(checkComboBox));
                return;
            }

            if (checkComboBox.SelectedItemsOverride != null)
            {
                // Parse the current checkComboBox items a text string to update the checkComboBox text as needed
                List<string> list = new List<string>();
                foreach (string item in checkComboBox.SelectedItemsOverride)
                {
                    list.Add(item);
                }
                list.Sort();
                string newText = string.Join(",", list).Trim(',');
                if (checkComboBox.Text != newText)
                {
                    checkComboBox.Text = newText;
                }
            }
            int row = Grid.GetRow(checkComboBox);  // Get the row number...
            Database.CustomSelection.SearchTerms[row - 1].DatabaseValue = checkComboBox.Text; // Set the corresponding value to the current selection
            UpdateSearchDialogFeedback();

        }

        // Value (Flags): The user has checked or unchecked a new value 
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void Flag_CheckedOrUnchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox == false)
            {
                // This shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            int row = Grid.GetRow(checkBox);  // Get the row number...
            Database.CustomSelection.SearchTerms[row - 1].DatabaseValue = checkBox.IsChecked.ToString().ToLower(); // Set the corresponding value to the current selection
            UpdateSearchDialogFeedback();
        }

        private void DateTimeCustomPicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (sender is DateTimePicker dateTimePicker == false)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            int row = Grid.GetRow(dateTimePicker);  // Get the row number...
            Database.CustomSelection.SearchTerms[row - 1].DatabaseValue = DateTimeHandler.DateTimeDisplayStringToDataBaseString(dateTimePicker.Text); // Set the corresponding value to the current selection
            UpdateSearchDialogFeedback();
        }

        private void DatePicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (sender is DateTimePicker dateTimePicker == false)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            int row = Grid.GetRow(dateTimePicker);  // Get the row number...
            Database.CustomSelection.SearchTerms[row - 1].DatabaseValue = DateTimeHandler.DateDisplayStringToDataBaseString(dateTimePicker.Text); // Set the corresponding value to the current selection
            UpdateSearchDialogFeedback();
        }

        private void TimePicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (sender is TimePicker timePicker == false)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            int row = Grid.GetRow(timePicker);  // Get the row number...
            Database.CustomSelection.SearchTerms[row - 1].DatabaseValue = timePicker.Text; // Set the corresponding value to the current selection
            UpdateSearchDialogFeedback();
        }

        // The RelativePathControl SelectedItemChanged callback: This does the work when a relative path is selected
        private void RelativePathControl_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (this.treeViewWithRelativePaths.DontInvoke)
            {
                return;
            }
            if (!(sender is TreeViewWithRelativePaths treeView))
            {
                return;
            }


            if (treeView.SelectedValue == null)
            {
                return;
            }

            treeView.FocusSelection = true;
            this.RelativePathButton.Content = treeView.SelectedPath;
            int row = Grid.GetRow(this.RelativePathButton);  // Get the row number... should always be a valid int
            this.Database.CustomSelection.SearchTerms[row - 1].DatabaseValue = treeView.SelectedPath; // Set the corresponding value to the current selection
            this.RelativePathButton.IsOpen = false;
            this.UpdateSearchDialogFeedback();
        }

        // When this button is pressed, all the search terms checkboxes are cleared, which is equivalent to showing all images
        private void ResetToAllImagesButton_Click(object sender, RoutedEventArgs e)
        {
            EnableRecognitionsCheckbox.IsChecked = false;
            for (int row = 1; row <= Database.CustomSelection.SearchTerms.Count; row++)
            {
                CheckBox select = GetGridElement<CheckBox>(SelectColumn, row);
                select.IsChecked = false;
            }
            ShowMissingDetectionsCheckbox.IsChecked = false;
        }

        private FileSelectionEnum ChangeSelectionStateIfNeeded()
        {
            if (EnableRecognitionsCheckbox.IsChecked == true)
            {
                // We have at least one non-relativePath checkmark, so don't change anything
                // i.e., this leave it as a custom selection
                return FileSelectionEnum.Custom;
            }

            int checkedSelectionsCount = 0;
            bool relativePathChecked = false;
            for (int index = Database.CustomSelection.SearchTerms.Count - 1; index >= 0; index--)
            {
                SearchTerm searchTerm = Database.CustomSelection.SearchTerms[index];

                if (searchTerm.UseForSearching)
                {
                    checkedSelectionsCount++;
                    if (searchTerm.DataLabel != DatabaseColumn.RelativePath)
                    {
                        // We have at least one non-relativePath checkmark, so don't change anything
                        // i.e., this leave it as a custom selection
                        return FileSelectionEnum.Custom;
                    }
                    relativePathChecked = true;
                }
            }

            if (checkedSelectionsCount == 0)
            {
                // As nothing is selected, this is the same as FileSelection All, so set that
                return FileSelectionEnum.All;
            }

            if (checkedSelectionsCount == 1 && relativePathChecked)
            {
                // As only relative paths are selected, this is the same as FileSelection Folders, so set that
                return FileSelectionEnum.Folders;
            }
            // We shouldn't arrive here...
            return FileSelectionEnum.Custom;
        }
        #endregion

        #region Search Criteria feedback for each row
        // Updates the feedback and control enablement to reflect the contents of the search list,
        private void UpdateSearchDialogFeedback()
        {
            if (dontUpdate)
            {
                return;
            }
            // We go backwards, as we don't want to print the AND or OR on the last expression
            bool atLeastOneSearchTermIsSelected = false;
            int multipleNonStandardSelectionsMade = 0;
            for (int index = Database.CustomSelection.SearchTerms.Count - 1; index >= 0; index--)
            {
                SearchTerm searchTerm = Database.CustomSelection.SearchTerms[index];

                if (searchTerm.UseForSearching == false)
                {
                    // As this search term is not used for searching, we can skip it
                    continue;
                }

                if (false == (searchTerm.DataLabel == DatabaseColumn.File ||
                              searchTerm.DataLabel == DatabaseColumn.RelativePath ||
                              searchTerm.DataLabel == DatabaseColumn.DateTime ||
                              searchTerm.DataLabel == DatabaseColumn.DeleteFlag))
                {
                    // Count the number of multiple non-standard selection rows
                    multipleNonStandardSelectionsMade++;
                }
                atLeastOneSearchTermIsSelected = true;
            }

            // Show how many file will match the current search
            InitiateShowCountsOfMatchingFiles();

            // Enable  the reset button if at least one search term (including detections) is enabled
            ResetToAllImagesButton.IsEnabled = atLeastOneSearchTermIsSelected
                                                    || ShowMissingDetectionsCheckbox.IsChecked == true;

            // Enable the and/or radio buttons if more than one non-standard selection was made
            RadioButtonTermCombiningAnd.IsEnabled = multipleNonStandardSelectionsMade > 1;
            RadioButtonTermCombiningOr.IsEnabled = multipleNonStandardSelectionsMade > 1;
        }
        #endregion

        #region Helper functions
        // Get the corresponding grid element from a given a column, row, 
        private TElement GetGridElement<TElement>(int column, int row) where TElement : UIElement
        {
            return (TElement)SearchTerms.Children.Cast<UIElement>().First(control => Grid.GetRow(control) == row && Grid.GetColumn(control) == column);
        }
        #endregion

        #region Detection-specific methods and callbacks

        private void ButtonRecognitionExplorer_OnClick(object sender, RoutedEventArgs e)
        {
            RecognitionExplorer dialog = new RecognitionExplorer(this, this.Database);
            if (dialog.ShowDialog() == true)
            {
                // update the current detection settings
                this.EnableRecognitionsCheckbox.IsChecked = true;
                DetectionRangeSlider.LowerValue = DetectionSelections.ConfidenceThreshold1ForUI;
                DetectionRangeSlider.HigherValue = DetectionSelections.ConfidenceThreshold2ForUI;
                DetectionConfidenceSpinnerLower.Value = DetectionSelections.ConfidenceThreshold1ForUI;
                DetectionConfidenceSpinnerHigher.Value = DetectionSelections.ConfidenceThreshold2ForUI;

            }
        }
        private void UseDetections_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (dontInvoke)
            {
                return;
            }
            // Enable or disable the controls depending on the various checkbox states
            EnableDetectionControls(EnableRecognitionsCheckbox.IsChecked == true);

            SetDetectionCriteria();
            InitiateShowCountsOfMatchingFiles();
        }

        private void SetDetectionCriteria(bool resetSlidersIfNeeded = false)
        {
            if (IsLoaded == false || dontInvoke)
            {
                return;
            }
            DetectionSelections.UseRecognition = EnableRecognitionsCheckbox.IsChecked == true;
            if (DetectionSelections.UseRecognition)
            {
                SetDetectionCriteriaForComboBox(resetSlidersIfNeeded);
                DetectionSelections.ConfidenceThreshold1ForUI = DetectionConfidenceSpinnerLower.Value == null ? 0 : Round2(DetectionConfidenceSpinnerLower.Value);
                DetectionSelections.ConfidenceThreshold2ForUI = DetectionConfidenceSpinnerHigher.Value == null ? 0 : Round2(DetectionConfidenceSpinnerHigher.Value);
            }

            // The BoundingBoxDisplayThreshold is the user-defined default set in preferences, while the BoundingBoxThresholdOveride is the threshold
            // determined in this select dialog. For example, if (say) the preference setting is .6 but the selection is at .4 confidence, then we should 
            // show bounding boxes when the confidence is .4 or more. On the other  hand, we don't want to show spurious detections when empty is selected,
            // so we set a minimum value.
            CustomSelection.SetDetectionRanges(DetectionSelections);

            // Enable / alter looks and behavour of detecion UI to match whether detections should be used
            EnableDetectionControls(EnableRecognitionsCheckbox.IsChecked == true);
        }

        private void ShowMissingDetectionsCheckbox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            Database.CustomSelection.ShowMissingDetections = ShowMissingDetectionsCheckbox.IsChecked == true;
            SetDetectionCriteria();
            InitiateShowCountsOfMatchingFiles();
        }
        private void DetectionCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded == false)
            {
                return;
            }
            // Invoke this with a true argument, which forces the confidence values to be reset based upon the selection
            SetDetectionCriteria(true);
            InitiateShowCountsOfMatchingFiles();
        }

        private void SetDetectionCriteriaForComboBox(bool resetSlidersIfNeeded)
        {
            ignoreSpinnerUpdates = true; // as otherwise resetting sliders/spinners will reinvoke this

            // Set various flags and values depending on what was selected in the combo box
            if (resetSlidersIfNeeded)
            {
                // These reset settings universally apply regardless of the recognition type
                // The higher limit is always 1.0. Resetting the lower limit to its undefined state signals that default values should be looked up and used.
                DetectionRangeSlider.HigherValue = 1.0;
                DetectionSelections.CurrentDetectionThreshold = RecognizerValues.Undefined; // As its a new JSON, resetting sets it back to detections, so we can use the default value.
                DetectionSelections.CurrentClassificationThreshold = RecognizerValues.Undefined; // As its a new JSON, resetting sets it back to detections, so we can use the default value.
            }

            if ((string)DetectionCategoryComboBox.SelectedItem == RecognizerValues.NoDetectionLabel)
            {
                // EMPTY
                // Note that Empties are special cases of an All Detection (which is why its recognition type is still a Detection and both AllDetections and EmptyDetections are true).
                // What actually happens is that other code flips the confidence values in the query to 1 - the current higher and lowever slider settings (e.g., from 1-1 to 0-0). 
                DetectionSelections.RecognitionType = RecognitionType.Detection;
                DetectionSelections.InterpretAllDetectionsAsEmpty = true;
                DetectionSelections.AllDetections = true; // Empty detections signal that we need to flip the confidence to its inverse, which is then applied to AllDetections
                DetectionSelections.DetectionCategory = Database.GetDetectionCategoryFromLabel(RecognizerValues.NoDetectionLabel);

                if (resetSlidersIfNeeded)
                {
                    // Default is ConservativeDetection Threshold - 1.0, i.e.,to only show images where the recognizer has not found any detections.
                    // As the EmptyDetections is true, other code will special case this by actually doing a query on 1 - these values
                    DetectionRangeSlider.HigherValue = 1.0;
                    DetectionRangeSlider.LowerValue = 1 - RecognitionSelections.ConservativeDetectionThreshold;
                }

                // Set the minium values for the slider and spinner, which is 0 fpr Empty detections
                DetectionRangeSlider.Minimum = 0;
                DetectionConfidenceSpinnerLower.Minimum = 0;
                DetectionConfidenceSpinnerHigher.Minimum = 0;
                RankByDetectionConfidenceCheckbox.Content = "by detection confidence";
            }
            else
            {
                // A non-empty selection
                DetectionSelections.InterpretAllDetectionsAsEmpty = false;

                // Set the minium values for the slider and spinner
                // These are just a titch above 0, which means the results will only include items with a detection, but never include purely empty items (i.e., items with no detections)
                DetectionRangeSlider.Minimum = RecognizerValues.MinimumDetectionValue;
                DetectionConfidenceSpinnerLower.Minimum = RecognizerValues.MinimumDetectionValue;
                DetectionConfidenceSpinnerHigher.Minimum = RecognizerValues.MinimumDetectionValue;

                // Resetting the minimum doesn't necessarily change the value if its below the minimum
                if (DetectionConfidenceSpinnerLower.Value == null || DetectionConfidenceSpinnerLower.Value < RecognizerValues.MinimumDetectionValue)
                {
                    DetectionConfidenceSpinnerLower.Value = RecognizerValues.MinimumDetectionValue;
                }
                if (DetectionConfidenceSpinnerHigher.Value == null || DetectionConfidenceSpinnerHigher.Value < RecognizerValues.MinimumDetectionValue)
                {
                    DetectionConfidenceSpinnerHigher.Value = RecognizerValues.MinimumDetectionValue;
                }

                //this.DetectionSelections.ConfidenceThreshold1ForUI = this.DetectionConfidenceSpinnerLower.Value == null  ? Constant.RecognizerValues.MinimumDetectionValue : Round2(this.DetectionConfidenceSpinnerLower.Value);
                //this.DetectionSelections.ConfidenceThreshold2ForUI = this.DetectionConfidenceSpinnerHigher.Value == null ? Constant.RecognizerValues.MinimumDetectionValue : Round2(this.DetectionConfidenceSpinnerHigher.Value);

                if ((string)DetectionCategoryComboBox.SelectedItem == RecognizerValues.AllDetectionLabel)
                {
                    // ALL (which is a detection)
                    DetectionSelections.RecognitionType = RecognitionType.Detection;
                    DetectionSelections.AllDetections = true;
                    DetectionSelections.InterpretAllDetectionsAsEmpty = false;
                    if (resetSlidersIfNeeded)
                    {
                        DetectionRangeSlider.LowerValue = DetectionSelections.CurrentDetectionThreshold;
                    }
                    RankByDetectionConfidenceCheckbox.Content = "by detection confidence";
                }
                else
                {
                    // Either a Detection (excluding All and Empty) or a Classification type 
                    DetectionSelections.AllDetections = false;
                    DetectionSelections.InterpretAllDetectionsAsEmpty = false;
                    string detectionCategory = Database.GetDetectionCategoryFromLabel((string)DetectionCategoryComboBox.SelectedItem);

                    if (string.IsNullOrWhiteSpace(detectionCategory))
                    {
                        // CLASSIFICATION
                        DetectionSelections.RecognitionType = RecognitionType.Classification;
                        DetectionSelections.ClassificationCategory = Database.GetClassificationCategoryFromLabel((string)DetectionCategoryComboBox.SelectedItem);
                        if (resetSlidersIfNeeded)
                        {
                            DetectionRangeSlider.LowerValue = DetectionSelections.CurrentClassificationThreshold;
                        }
                        RankByDetectionConfidenceCheckbox.Content = "by classification confidence";
                    }
                    else
                    {
                        // DETECTION
                        DetectionSelections.RecognitionType = RecognitionType.Detection;
                        DetectionSelections.DetectionCategory = detectionCategory;
                        if (resetSlidersIfNeeded)
                        {
                            DetectionRangeSlider.LowerValue = DetectionSelections.CurrentDetectionThreshold;
                        }
                        RankByDetectionConfidenceCheckbox.Content = "by detection confidence";
                    }
                }
            }
            ignoreSpinnerUpdates = false;
        }

        // Note that for either of these, we avoid a race condition where each tries to update the other by
        // setting this.ignoreSpinnerUpdates to true, which will cancel the operation
        private bool ignoreSpinnerUpdates;
        private void DetectionConfidenceSpinnerLower_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (IsLoaded == false || ignoreSpinnerUpdates)
            {
                return;
            }

            // If the user has set the upper spinner to less than the lower spinner,
            // reset it to the value of the lower spinner. That is, don't allow the user to 
            // go below the lower spinner value.
            if (DetectionConfidenceSpinnerLower.Value > DetectionConfidenceSpinnerHigher.Value)
            {
                ignoreSpinnerUpdates = true;
                DetectionConfidenceSpinnerLower.Value = DetectionConfidenceSpinnerHigher.Value;
                ignoreSpinnerUpdates = false;
            }
            SetDetectionCriteria();

            if (dontUpdateRangeSlider == false)
            {
                DetectionRangeSlider.LowerValue = DetectionConfidenceSpinnerLower.Value ?? 0;
            }
            else
            {
                dontUpdateRangeSlider = false;
            }


            if (DetectionSelections.RecognitionType == RecognitionType.Detection)
            {
                if (DetectionConfidenceSpinnerLower.Value != null)
                {
                    DetectionSelections.CurrentDetectionThreshold = (double)DetectionConfidenceSpinnerLower.Value;
                }
                else
                {
                    // Shouldn't happen
                    TracePrint.NullException(nameof(DetectionConfidenceSpinnerLower.Value));
                }
            }
            else if (DetectionSelections.RecognitionType == RecognitionType.Classification)
            {
                if (DetectionConfidenceSpinnerLower.Value != null)
                {
                    DetectionSelections.CurrentDetectionThreshold = (double)DetectionConfidenceSpinnerLower.Value;
                }
                else
                {
                    // Shouldn't happen
                    TracePrint.NullException(nameof(DetectionConfidenceSpinnerLower.Value));
                }
            }
            InitiateShowCountsOfMatchingFiles();
        }

        private void DetectionConfidenceSpinnerHigher_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (IsLoaded == false || ignoreSpinnerUpdates)
            {
                return;
            }

            // If the user has set the upper spinner to less than the lower spinner,
            // reset it to the value of the lower spinner. That is, don't allow the user to 
            // go below the lower spinner value.
            if (DetectionConfidenceSpinnerHigher.Value < DetectionConfidenceSpinnerLower.Value)
            {
                ignoreSpinnerUpdates = true;
                DetectionConfidenceSpinnerHigher.Value = DetectionConfidenceSpinnerLower.Value;
                ignoreSpinnerUpdates = false;
            }
            SetDetectionCriteria();

            if (dontUpdateRangeSlider == false)
            {
                DetectionRangeSlider.HigherValue = DetectionConfidenceSpinnerHigher.Value ?? 0;
                dontUpdateRangeSlider = false;
            }
            else
            {
                dontUpdateRangeSlider = false;
            }
            InitiateShowCountsOfMatchingFiles();
        }

        // Detection range slider callback - Upper range
        // Note that this does not invoke this.SetDetectionCriteria(), as that is done as a side effect of invoking the spinner
        private void DetectionRangeSlider_HigherValueChanged(object sender, RoutedEventArgs e)
        {
            if (IsLoaded == false || ignoreSpinnerUpdates)
            {
                return;
            }
            // Round up the value to the nearest 2 decimal places,
            // and update the spinner (also in two decimal places) only if the value differs
            // This stops the spinner from updated if values change in the 3rd decimal place and beyond
            double value = Round2(DetectionRangeSlider.HigherValue);
            if (Math.Abs(value - Round2(DetectionConfidenceSpinnerHigher.Value)) > .0001)
            {
                dontUpdateRangeSlider = true;
                DetectionConfidenceSpinnerHigher.Value = value;
                dontUpdateRangeSlider = false;
            }
        }

        // Detection range slider callback - Lower range
        // Note that this does not invoke this.SetDetectionCriteria(), as that is done as a side effect of invoking the spinner
        private void DetectionRangeSlider_LowerValueChanged(object sender, RoutedEventArgs e)
        {
            // Round up the value to the nearest 2 decimal places,
            // and update the spinner (also in two decimal places) only if the value differs
            // This stops the spinner from updated if values change in the 3rd decimal place and beyond
            double value = Round2(DetectionRangeSlider.LowerValue);
            if (Math.Abs(value - Round2(DetectionConfidenceSpinnerLower.Value)) > .0001)
            {
                dontUpdateRangeSlider = true;
                DetectionConfidenceSpinnerLower.Value = value;
                dontUpdateRangeSlider = false;
            }
        }

        // Enable or disable the controls depending on the parameter
        private void EnableDetectionControls(bool isEnabled)
        {
            // Various confidence controls are enabled only if useDetections is set and the rank by confidence is unchecked
            bool confidenceControlsEnabled = isEnabled && !DetectionSelections.RankByConfidence;
            DetectionConfidenceSpinnerLower.IsEnabled = confidenceControlsEnabled;
            DetectionConfidenceSpinnerHigher.IsEnabled = confidenceControlsEnabled;
            DetectionRangeSlider.IsEnabled = confidenceControlsEnabled;
            DetectionConfidenceLabel.FontWeight = confidenceControlsEnabled ? FontWeights.Normal : FontWeights.Light;
            //FromLabel.FontWeight = confidenceControlsEnabled ? FontWeights.Normal : FontWeights.Light;
            //ToLabel.FontWeight = confidenceControlsEnabled ? FontWeights.Normal : FontWeights.Light;
            DetectionRangeSlider.RangeBackground = confidenceControlsEnabled ? Brushes.Gold : Brushes.LightGray;


            // There remainder depends upon the use detections isEnable state only
            DetectionCategoryComboBox.IsEnabled = isEnabled;
            DetectionCategoryLabel.FontWeight = isEnabled ? FontWeights.Normal : FontWeights.Light;
            RankByDetectionConfidenceCheckbox.IsEnabled = isEnabled;
            RankByDetectionConfidenceCheckbox.FontWeight = isEnabled ? FontWeights.Normal : FontWeights.Light;

            // CHECK THE ONES BELOW TO SEE IF THIS IS THE BEST WAY TO DO THESE
            SelectionGroupBox.IsEnabled = !Database.CustomSelection.ShowMissingDetections;
            SelectionGroupBox.Background = Database.CustomSelection.ShowMissingDetections ? Brushes.LightGray : Brushes.White;

            RecognitionsGroupBox.IsEnabled = !Database.CustomSelection.ShowMissingDetections;
            RecognitionsGroupBox.Background = Database.CustomSelection.ShowMissingDetections ? Brushes.LightGray : Brushes.White;

            if (ShowMissingDetectionsCheckbox.IsChecked == true || EnableRecognitionsCheckbox.IsChecked == true)
            {
                ResetToAllImagesButton.IsEnabled = true;
            }
        }

        private void RankByConfidence_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // Need to disable confidence sliders/spinners depending on the state of this checkbox and use detections
            // ALso need to restore state of this checkbox between repeated uses in Window_Loaded.
            DetectionSelections.RankByConfidence = RankByDetectionConfidenceCheckbox.IsChecked == true;
            InitiateShowCountsOfMatchingFiles();
            EnableDetectionControls(EnableRecognitionsCheckbox.IsChecked == true);
        }
        #endregion

        #region Common to Selections and Detections
        private void CountTimer_Tick(object sender, EventArgs e)
        {
            countTimer.Stop();
            // This is set everytime a selection is made
            if (dontCount)
            {
                return;
            }
            int count = Database.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.Custom);
            QueryMatches.Text = count > 0 ? count.ToString() : "0";
            OkButton.IsEnabled = count > 0; // Dusable OK button if there are no matches

            // Uncomment this to add feedback to the File count line desribing the kinds of files selected
            //if (this.UseDetectionsCheckbox.IsChecked == false)
            //{
            //    this.QueryFileMatchNote.Text = string.Empty;
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
            countTimer.Stop();
            countTimer.Start();
        }

        // Apply the selection if the Ok button is clicked
        private void OkButton_Click(object sender, RoutedEventArgs args)
        {
            if (GlobalReferences.DetectionsExists)
            {
                SetDetectionCriteria();
            }

            this.FileSelection = ChangeSelectionStateIfNeeded();
            this.Database.FileSelectionEnum = this.FileSelection;
            DialogResult = true;
        }

        // Cancel - exit the dialog without doing anythikng.
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
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
            if (Database == null)
            {
                CheckboxShowAllEpisodeImages.IsChecked = false;
                return;
            }

            if (true == CheckboxShowAllEpisodeImages.IsChecked)
            {
                if (string.IsNullOrEmpty(NoteDataLabelContainingEpisodeData))
                {
                    // No note fields contain the expected Episode data. Disable this operation and get the heck out of here.
                    Dialogs.CustomSelectEpisodeDataLabelProblem(Owner);
                    CheckboxShowAllEpisodeImages.IsChecked = false;
                    Database.CustomSelection.EpisodeShowAllIfAnyMatch = false;
                    return;
                }

            }
            Database.CustomSelection.EpisodeShowAllIfAnyMatch = true == CheckboxShowAllEpisodeImages.IsChecked;
            UpdateSearchDialogFeedback();
        }

        private static bool EpisodeFieldCheckFormat(ImageRow row, string dataLabel)
        {
            if (string.IsNullOrWhiteSpace(dataLabel))
            {
                return false;
            }
            string value = row.GetValueDisplayString(dataLabel);
            return (null != value && Regex.IsMatch(value, RegExExpressions.NotEpisodeCharacters));
        }
        #endregion

        #region RelativePathControl methods
        private void RelativePathCreateControl(SearchTerm searchTerm, Thickness thickness, int gridRowIndex, CheckBox checkboxforUsingRelativePath)
        {
            // Relative path uses a dropdown button that shows existing relative path folders as a treeview
            this.RelativePathButton = new DropDownButton
            {
                FontWeight = FontWeights.Normal,
                IsEnabled = searchTerm.UseForSearching,
                Width = DefaultControlWidth,
                Height = 25,
                Margin = thickness,
                HorizontalAlignment = HorizontalAlignment.Left,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Brushes.White,
                MaxDropDownHeight = 350,
            };

            // Add the TreeViewWithRelativePaths control as the DropDown button's drop down.
            // This treeview is specialized to show relative paths
            this.treeViewWithRelativePaths = new TreeViewWithRelativePaths
            {
                DontInvoke = true,
            };
            this.RelativePathButton.DropDownContent = treeViewWithRelativePaths;

            // Populate the treeview. Enable it only if it has content
            this.RelativePathControlRepopulateIfNeeded();
            this.treeViewWithRelativePaths.FocusSelection = true;
            this.RelativePathButton.IsEnabled = this.treeViewWithRelativePaths.HasContent && (null != checkboxforUsingRelativePath && checkboxforUsingRelativePath.IsChecked == true);
            if (checkboxforUsingRelativePath != null)
            {
                checkboxforUsingRelativePath.IsEnabled = this.treeViewWithRelativePaths.HasContent;
            }
            this.treeViewWithRelativePaths.SelectedItemChanged += RelativePathControl_SelectedItemChanged;
            RelativePathButton.GotFocus += ControlsDataHelpersCommon.Control_GotFocus;
            RelativePathButton.LostFocus += ControlsDataHelpersCommon.Control_LostFocus;

            Grid.SetRow(RelativePathButton, gridRowIndex);
            Grid.SetColumn(RelativePathButton, ValueColumn);
            SearchTerms.Children.Add(RelativePathButton);
            this.treeViewWithRelativePaths.DontInvoke = false;
        }

        private void RelativePathControlSetSearchTerm()
        {
            SearchTerm relativePathSearchTerm = this.Database?.CustomSelection?.SearchTerms.FirstOrDefault(term => term.DataLabel == DatabaseColumn.RelativePath);

            if (string.IsNullOrEmpty(relativePathSearchTerm?.DatabaseValue))
            {
                // Nothing relevant found so just collapse everything
                this.treeViewWithRelativePaths.SelectedPath = string.Empty;
                this.treeViewWithRelativePaths.FocusSelection = false;
                this.treeViewWithRelativePaths.UnselectAll();
                this.treeViewWithRelativePaths.CollapseAll();
                return;
            }

            if (false == relativePathSearchTerm.UseForSearching || this.Database.FileSelectionEnum != FileSelectionEnum.Folders)
            {
                // Expand the search term, but we don't want it focused
                this.treeViewWithRelativePaths.FocusSelection = false;
                this.treeViewWithRelativePaths.SelectedPath = relativePathSearchTerm.DatabaseValue;
                return;
            }
            this.treeViewWithRelativePaths.FocusSelection = true;
            this.treeViewWithRelativePaths.SelectedPath = relativePathSearchTerm.DatabaseValue;
        }

        private void RelativePathControlRepopulateIfNeeded()
        {
            this.RelativePathControlSetSearchTerm();
            this.RelativePathButton.Content = this.treeViewWithRelativePaths.SelectedPath;

            // Repopulate the treeview if needed.
            if (false == this.treeViewWithRelativePaths.HasContent)
            {
                this.RelativePathControlResetFolderList();
            }
        }

        // Populate the control. Get the folders from the database, and create a menu item representing it
        private void RelativePathControlResetFolderList()
        {
            // Get the folders from the database
            // PERFORMANCE. This can introduce a delay when there are a large number of files.
            // To remedy this, it is only invoked when the user loads images for the first time or when new images are added. 
            List<string> folderList = this.Database.GetFoldersFromRelativePaths();//this.DataHandler.FileDatabase.GetDistinctValuesInColumn(Constant.DBTables.FileData, Constant.DatabaseColumn.RelativePath);

            if (this.Arguments.ConstrainToRelativePath)
            {
                // Special case.
                // If we are constrained to the relative path, create a new list that removes folders outside that relative path
                List<string> newFolderList = new List<string>();
                foreach (string relativePath in folderList)
                {
                    if (false == string.IsNullOrEmpty(relativePath) &&
                        (relativePath == this.Arguments.RelativePath || relativePath.StartsWith(this.Arguments.RelativePath + @"\")))
                    {
                        // An empty header is actually the root folder, which we don't need
                        // We also don't want any relative paths outside the desired one
                        // Add the folder to the menu only if it isn't constrained by the relative path arguments
                        newFolderList.Add(relativePath);
                    }
                }
                folderList = newFolderList;
            }

            this.treeViewWithRelativePaths.DontInvoke = true;
            List<Item> items = this.treeViewWithRelativePaths.SetTreeViewContentsToRelativePathList(folderList);
            // Because we are not doing the treeview in xaml, we have to set the ItemsSource here
            this.treeViewWithRelativePaths.ItemsSource = items;
            this.treeViewWithRelativePaths.DontInvoke = false;
        }

        #endregion

    }
}
