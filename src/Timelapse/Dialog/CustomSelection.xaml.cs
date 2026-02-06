using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.ControlsCore;
using Timelapse.ControlsDataEntry;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.EventArguments;
using Timelapse.Recognition;
using Timelapse.SearchingAndSorting;
using Timelapse.Util;
using TimelapseWpf.Toolkit;
using TimelapseWpf.Toolkit.Primitives;
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

        // Variables passed into constructor
        private readonly FileDatabase Database;
        private readonly ImageRow CurrentImageRow;
        private readonly DataEntryControls DataEntryControls;
        private readonly Arguments Arguments;
        private bool dontUpdate = true;
        private bool RefreshRecognitionCountsRequired = true;

        // The RelativePath control is implemented as a combination DropDownButton with a TreeViewRelativePathMenu as its content
        private TreeViewWithRelativePaths treeViewWithRelativePaths;
        private DropDownButton RelativePathButton;

        // Remember note fields that contain Episode data
        private string NoteDataLabelContainingEpisodeData;

        // References to the various dateTime labels and controls set when they are created later,
        // so we can switch their attributes depending on the CheckBoxUseTime state
        private TextBlock dateTimeLabel1;
        private TextBlock dateTimeLabel2;
        private WatermarkDateTimePicker dateTimeControl1;
        private WatermarkDateTimePicker dateTimeControl2;

        // This timer is used to delay showing count information, which could be an expensive operation, as the user may be setting values quickly
        private readonly DispatcherTimer CountTimer = new();

        private RecognitionSelector RecognitionSelector;
        private RecognitionSelections RecognitionSelections { get; }
        #endregion

        #region Controls created ahead of time
        // UseTime Checkbox, funciton is to specify whether the select should use a pure time range instead of a pure date range
        private readonly CheckBox CheckBoxUseTime = new()
        {
            Content = "Use time (hh:mm:ss) instead of date",
            FontWeight = FontWeights.Normal,
            FontStyle = FontStyles.Italic,
            VerticalAlignment = VerticalAlignment.Center,
            IsChecked = false,
            Width = double.NaN,
            Margin = new()
            {
                Left = 10
            },
            IsEnabled = true
        };

        // And/Or RadioButtons use to combine non-standard terms
        private readonly RadioButton RadioButtonTermCombiningAnd = new()
        {
            Content = "And ",
            GroupName = "LogicalOperators",
            FontWeight = FontWeights.DemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            IsChecked = true,
            Width = Double.NaN,
            IsEnabled = false
        };
        private readonly RadioButton RadioButtonTermCombiningOr = new()
        {
            Content = "Or ",
            GroupName = "LogicalOperators",
            VerticalAlignment = VerticalAlignment.Center,
            Width = double.NaN,
            IsEnabled = false
        };
        #endregion

        #region Constructors and Loading
        public CustomSelectionWithEpisodes(Window owner, FileDatabase database, DataEntryControls dataEntryControls,
            ImageRow currentImageRow, RecognitionSelections recognitionSelections, Arguments arguments) : base(owner)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(database, nameof(database));

            InitializeComponent();

            // Set up static reference resolver for FormattedMessageContent
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);

            // Save the passed in parameters
            this.Owner = owner;
            this.Database = database;
            this.DataEntryControls = dataEntryControls;
            this.CurrentImageRow = currentImageRow;
            if (GlobalReferences.DetectionsExists)
            {
                this.RecognitionSelections = recognitionSelections; // Detections-specific
            }
            this.Arguments = arguments;

            // Set up the count timer
            CountTimer.Interval = TimeSpan.FromMilliseconds(250);
            CountTimer.Tick += CountTimer_Tick;
        }

        // When the window is loaded, construct all the SearchTerm controls
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await Window_LoadedAsync();
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task Window_LoadedAsync()
        {
            this.Message.BuildContentFromProperties();

            // Adjust this dialog window position 
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            // Set up a progress handler that will update the progress bar
            InitalizeProgressHandler(BusyCancelIndicator);

            // Used to track whether we are on the 1st or 2nd dateTime control
            bool firstDateTimeControlSeen = false;

            // Add callback to this checkbox
            CheckBoxUseTime.Checked += CheckBoxUseTime_CheckChanged;
            CheckBoxUseTime.Unchecked += CheckBoxUseTime_CheckChanged;
            // Detections-specific
            // Set the state of the detections to the last used ones (or to its defaults)

            if (GlobalReferences.DetectionsExists)
            {
                EnableRecognitionsCheckbox.IsEnabled = true;
                RecognitonsGroupBoxHeaderText.Text = "Select recognitions…";
                this.RecognitionsGroupBox.Visibility = Visibility.Visible;
                this.EnableRecognitionsCheckbox.IsChecked = this.RecognitionSelections.UseRecognition;
                this.RecognitonsGroupBoxHeaderText.FontWeight = FontWeights.Normal ;
                dontInvoke = false;
                this.EnableRecognitions_CheckedChanged(null, null);
                this.ShowEpisodeOptionsPanel.Margin = new Thickness(32, 10, 0, 0);
            }
            else
            {
                EnableRecognitionsCheckbox.IsEnabled = false;
                this.RecognitonsGroupBoxHeaderText.FontWeight = FontWeights.Light;
                RecognitonsGroupBoxHeaderText.Inlines.Clear();
                RecognitonsGroupBoxHeaderText.Inlines.Add(new Run
                {
                    Text = "Select recognitions… "
                });

                RecognitonsGroupBoxHeaderText.Inlines.Add(new Run
                {
                    FontWeight = FontWeights.Light,
                    FontStyle = FontStyles.Italic,
                    Text = "(disabled: no Recognitions available)"
                });
                RecognitionSelections?.ClearAllDetectionsUses();
                this.ShowEpisodeOptionsPanel.Margin = new Thickness(32, 0, 0, 0);
            }
            dontInvoke = false;
            dontCount = false;
            if (GlobalReferences.DetectionsExists)
            {
                SetDetectionCriteria();
                //ShowMissingDetectionsCheckbox.IsChecked = Database.CustomSelection.ShowMissingDetections;

            }

            // Episode-related:
            // Check if there is an episode data field and if so, enable the appropriate checkbox and its value
            // If that checkbox is checked, then all images in an episode will be included in the selection
            // if any image in that episode matches the other criteria
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
            TextBlock textBlock = new TextBlock();
            if (isEpisodeAvailable)
            {
                textBlock.Text = "Include all files in an episode when at least one file matches";
            }
            else
            {
                textBlock.Inlines.Add(new Run
                {
                    Text = "Include all files in an episode… "
                });
                textBlock.Inlines.Add(new Run
                {
                    FontWeight = FontWeights.Light,
                    FontStyle = FontStyles.Italic,
                    Text = "(disabled: no Note field contains Episode data)"
                });
            }
            CheckboxShowAllEpisodeImages.Content = textBlock;
            InitiateShowCountsOfMatchingFiles();

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
            int tabIndex = 2; // Start at 2 (0 = RecognitionsCheckbox, 1 = EpisodeCheckbox)
            int andOrTabIndex = -1; // Will be set when And/Or buttons are created
            foreach (SearchTerm searchTerm in Database.CustomSelection.SearchTerms)
            {
                // start at 1 as there is already a header row
                ++gridRowIndex;
                RowDefinition gridRow = new()
                {
                    Height = GridLength.Auto
                };
                SearchTerms.RowDefinitions.Add(gridRow);

                // USE Column: A checkbox to indicate whether the current search row should be used as part of the search
                Thickness thickness = new(5, 2, 5, 2);
                CheckBox useCurrentRow = new()
                {
                    FontWeight = FontWeights.DemiBold,
                    Margin = thickness,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    IsChecked = searchTerm.UseForSearching,
                    TabIndex = tabIndex++
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
                TextBlock controlLabel = new()
                {
                    FontWeight = searchTerm.UseForSearching ? FontWeights.DemiBold : FontWeights.Normal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new(5)
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

                // I prefer switch statements here for readability, but the IDE suggests a different syntax
#pragma warning disable IDE0066
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
                        termOperators =
                        [
                            SearchTermOperator.Equal,
                            SearchTermOperator.NotEqual,
                            SearchTermOperator.LessThan,
                            SearchTermOperator.GreaterThan,
                            SearchTermOperator.LessThanOrEqual,
                            SearchTermOperator.GreaterThanOrEqual
                        ];
                        break;
                    case Control.MultiChoice:
                        termOperators =
                        [
                            SearchTermOperator.Equal,
                            SearchTermOperator.NotEqual,
                            SearchTermOperator.Includes,
                            SearchTermOperator.Excludes
                        ];
                        break;
                    // Relative path only allows = (this will be converted later to a glob to get subfolders) 
                    case DatabaseColumn.RelativePath:
                        // Only equals (actually a glob including subfolders), as other options don't make sense for RelatvePath
                        termOperators =
                        [
                            SearchTermOperator.Equal
                        ];
                        break;
                    // Only equals and not equals (For relative path this will be converted later to a glob to get subfolders) 
                    case DatabaseColumn.DeleteFlag:
                    case Control.Flag:
                        // Only equals and not equals in Flags, as other options don't make sense for booleans
                        termOperators =
                        [
                            SearchTermOperator.Equal,
                            SearchTermOperator.NotEqual
                        ];
                        break;


                    default:
                        termOperators =
                        [
                            SearchTermOperator.Equal,
                            SearchTermOperator.NotEqual,
                            SearchTermOperator.LessThan,
                            SearchTermOperator.GreaterThan,
                            SearchTermOperator.LessThanOrEqual,
                            SearchTermOperator.GreaterThanOrEqual,
                            SearchTermOperator.Glob,
                            SearchTermOperator.NotGlob
                        ];
                        break;
                }
#pragma warning restore IDE0066

                // term operator combo box
                ComboBox operatorsComboBox = new()
                {
                    FontWeight = FontWeights.Normal,
                    IsEnabled = searchTerm.UseForSearching,
                    ItemsSource = termOperators,
                    Margin = thickness,
                    Width = 78,
                    Height = 25,
                    SelectedValue = searchTerm.Operator,
                    TabIndex = tabIndex++
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
                        this.RelativePathCreateControl(searchTerm, thickness, gridRowIndex, checkboxforUsingRelativePath, tabIndex++);
                        break;

                    // DateTime
                    case DatabaseColumn.DateTime:
                        DateTime dateTime = Database.CustomSelection.GetDateTimePLAINVERSION(gridRowIndex - 1);
                        // The DateTime Picker is set to show only the date portion
                        WatermarkDateTimePicker dateValue = new()
                        {
                            FontWeight = FontWeights.Normal,
                            Format = DateTimeFormat.Custom,
                            FormatString = Time.DateDisplayFormat,
                            IsEnabled = searchTerm.UseForSearching,
                            Width = DefaultControlWidth,
                            CultureInfo = CultureInfo.CreateSpecificCulture("en-US"),
                            Value = dateTime,
                            TimePickerVisibility = Visibility.Collapsed,
                            TabIndex = tabIndex++
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
                        dateValue.GotFocus += ControlsDataHelpers.Control_GotFocus;
                        dateValue.LostFocus += ControlsDataHelpers.Control_LostFocus;
                        Grid.SetRow(dateValue, gridRowIndex);
                        Grid.SetColumn(dateValue, ValueColumn);
                        SearchTerms.Children.Add(dateValue);
                        break;

                    // File, Note, Alphanumeric
                    case DatabaseColumn.File:
                    case Control.Note:
                    case Control.AlphaNumeric:
                        {
                            ImprintAutoCompleteTextBox textBoxValue = new()
                            {
                                FontWeight = FontWeights.Normal,
                                IsEnabled = searchTerm.UseForSearching,
                                Text = searchTerm.DatabaseValue,
                                Margin = thickness,
                                Width = DefaultControlWidth,
                                Height = 25,
                                Padding = new Thickness(0,1,0,0),
                                TextWrapping = TextWrapping.NoWrap,
                                VerticalAlignment = VerticalAlignment.Center,
                                VerticalContentAlignment = VerticalAlignment.Top,
                                TabIndex = tabIndex++
                            };
                            if (controlType == Control.Note ||
                                controlType == Control.AlphaNumeric)
                            {
                                // Add existing autocompletions for this control
                                textBoxValue.AddToAutocompletions(DataEntryControls.AutocompletionGetForNote(searchTerm.DataLabel)); 
                            }

                            if (controlType == Control.AlphaNumeric)
                            {
                                textBoxValue.PreviewKeyDown += ValidationCallbacks.PreviewKeyDown_TextBoxNoSpaces;
                                textBoxValue.PreviewTextInput += ValidationCallbacks.PreviewInput_AlphaNumericCharacterOnlyWithGlob;
                                textBoxValue.TextChanged += ValidationCallbacks.TextChanged_AlphaNumericTextWithGlobCharactersOnly;
                            }
                            textBoxValue.TextChanged += Note_TextChanged;
                            textBoxValue.GotFocus += ControlsDataHelpers.Control_GotFocus;
                            textBoxValue.LostFocus += ControlsDataHelpers.Control_LostFocus;
                            Grid.SetRow(textBoxValue, gridRowIndex);
                            Grid.SetColumn(textBoxValue, ValueColumn);
                            SearchTerms.Children.Add(textBoxValue);
                            break;
                        }

                    case Control.MultiLine:
                        {
                            MultiLineText multiLineValue = new()
                            {
                                FontWeight = FontWeights.Normal,
                                IsEnabled = searchTerm.UseForSearching,
                                Text = searchTerm.DatabaseValue,
                                // Content = searchTerm.DatabaseValue,
                                Margin = thickness,
                                Width = DefaultControlWidth,
                                Height = 25,
                                Padding = new Thickness(0, 2, 0, 0),
                                TextWrapping = TextWrapping.NoWrap,
                                VerticalAlignment = VerticalAlignment.Center,
                                VerticalContentAlignment = VerticalAlignment.Top,
                                HorizontalContentAlignment = HorizontalAlignment.Left,
                                Style = (Style)DataEntryControls.FindResource("MultiLineTextBox"),
                                IsTabStop = true,
                                TabIndex = tabIndex++
                            };
                            multiLineValue.TextChanged += MultiLineValue_TextHasChanged;
                            multiLineValue.PreviewKeyDown += MultiLineValue_PreviewKeyDown;
                            Grid.SetRow(multiLineValue, gridRowIndex);
                            Grid.SetColumn(multiLineValue, ValueColumn);
                            SearchTerms.Children.Add(multiLineValue);
                            break;
                        }

                    // Counter IntegerAny IntegerPositive
                    case Control.Counter:
                    case Control.IntegerAny:
                    case Control.IntegerPositive:
                        IntegerUpDown integerUpDownBoxValue = new()
                        {
                            FontWeight = FontWeights.Normal,
                            IsEnabled = searchTerm.UseForSearching,
                            Margin = thickness,
                            Width = DefaultControlWidth,
                            Height = 22,
                            VerticalAlignment = VerticalAlignment.Center,
                            VerticalContentAlignment = VerticalAlignment.Center,
                            Minimum = controlType == Control.IntegerAny ? Int32.MinValue : 0,
                            TabIndex = tabIndex++
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
                        integerUpDownBoxValue.GotFocus += ControlsDataHelpers.Control_GotFocus;
                        integerUpDownBoxValue.LostFocus += ControlsDataHelpers.Control_LostFocus;
                        Grid.SetRow(integerUpDownBoxValue, gridRowIndex);
                        Grid.SetColumn(integerUpDownBoxValue, ValueColumn);
                        SearchTerms.Children.Add(integerUpDownBoxValue);
                        break;

                    // DecimalAny DecimalPositive
                    case Control.DecimalAny:
                    case Control.DecimalPositive:
                        DoubleUpDown doubleUpDownBoxValue = new()
                        {
                            FontWeight = FontWeights.Normal,
                            IsEnabled = searchTerm.UseForSearching,
                            Text = searchTerm.DatabaseValue,
                            Margin = thickness,
                            Width = DefaultControlWidth,
                            Height = 22,
                            VerticalAlignment = VerticalAlignment.Center,
                            VerticalContentAlignment = VerticalAlignment.Center,
                            FormatString = ControlDefault.DecimalFormatString,
                            CultureInfo = CultureInfo.InvariantCulture,
                            Minimum = controlType == Control.DecimalAny ? Double.MinValue : 0,
                            TabIndex = tabIndex++
                        };

                        if (Double.TryParse(searchTerm.DatabaseValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
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
                        doubleUpDownBoxValue.GotFocus += ControlsDataHelpers.Control_GotFocus;
                        doubleUpDownBoxValue.LostFocus += ControlsDataHelpers.Control_LostFocus;
                        Grid.SetRow(doubleUpDownBoxValue, gridRowIndex);
                        Grid.SetColumn(doubleUpDownBoxValue, ValueColumn);
                        SearchTerms.Children.Add(doubleUpDownBoxValue);
                        break;

                    case Control.FixedChoice:
                        {
                            // FixedChoice presents combo boxes, so they can be constructed the same way
                            ComboBox comboBoxValue = new()
                            {
                                FontWeight = FontWeights.Normal,
                                IsEnabled = searchTerm.UseForSearching,
                                Width = DefaultControlWidth,
                                Margin = thickness,

                                // Create the dropdown menu
                                ItemsSource = searchTerm.List,
                                SelectedItem = searchTerm.DatabaseValue,
                                TabIndex = tabIndex++
                            };
                            comboBoxValue.SelectionChanged += FixedChoice_SelectionChanged;
                            comboBoxValue.GotFocus += ControlsDataHelpers.Control_GotFocus;
                            comboBoxValue.LostFocus += ControlsDataHelpers.Control_LostFocus;
                            Grid.SetRow(comboBoxValue, gridRowIndex);
                            Grid.SetColumn(comboBoxValue, ValueColumn);
                            SearchTerms.Children.Add(comboBoxValue);
                            break;
                        }
                    case Control.MultiChoice:
                        {
                            // MultiChoice presents checkCombo boxes, so they can be constructed the same way
                            // Remove the empty item from the list
                            List<string> newList = [.. searchTerm.List];
                            newList.Remove(string.Empty);
                            WatermarkCheckComboBox checkComboBoxValue = new()
                            {
                                FontWeight = FontWeights.Normal,
                                IsEnabled = searchTerm.UseForSearching,
                                Width = DefaultControlWidth,
                                Margin = thickness,
                                // Create the dropdown menu
                                ItemsSource = newList,
                                TabIndex = tabIndex++
                            };
                            // Populate the combobox menu
                            checkComboBoxValue.Opened += ControlsDataHelpers.WatermarkCheckComboBox_DropDownOpened;
                            checkComboBoxValue.Closed += ControlsDataHelpers.WatermarkCheckComboBox_DropDownClosed;
                            checkComboBoxValue.ItemSelectionChanged += WatermarkCheckComboBox_ItemSelectionChanged;
                            checkComboBoxValue.GotFocus += ControlsDataHelpers.Control_GotFocus;
                            checkComboBoxValue.LostFocus += ControlsDataHelpers.Control_LostFocus;
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
                            CheckBox flagCheckBox = new()
                            {
                                FontWeight = FontWeights.Normal,
                                Margin = thickness,
                                VerticalAlignment = VerticalAlignment.Center,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                IsChecked = !String.Equals(searchTerm.DatabaseValue, BooleanValue.False, StringComparison.OrdinalIgnoreCase),
                                IsEnabled = searchTerm.UseForSearching,
                                TabIndex = tabIndex++
                            };
                            flagCheckBox.Checked += Flag_CheckedOrUnchecked;
                            flagCheckBox.Unchecked += Flag_CheckedOrUnchecked;
                            flagCheckBox.GotFocus += ControlsDataHelpers.Control_GotFocus;
                            flagCheckBox.LostFocus += ControlsDataHelpers.Control_LostFocus;
                            searchTerm.DatabaseValue = flagCheckBox.IsChecked.Value ? BooleanValue.True : BooleanValue.False;
                            Grid.SetRow(flagCheckBox, gridRowIndex);
                            Grid.SetColumn(flagCheckBox, ValueColumn);
                            SearchTerms.Children.Add(flagCheckBox);
                            break;
                        }

                    case Control.DateTime_:
                        WatermarkDateTimePicker dateTimePicker = DateTimeHandler.TryParseDatabaseOrDisplayDateTime(searchTerm.DatabaseValue, out DateTime dateTimeCustom)
                            ? CreateControls.CreateWatermarkDateTimePicker(String.Empty, DateTimeFormatEnum.DateAndTime, dateTimeCustom)
                            : CreateControls.CreateWatermarkDateTimePicker(String.Empty, DateTimeFormatEnum.DateAndTime, ControlDefault.DateTimeCustomDefaultValue);
                        dateTimePicker.FontWeight = FontWeights.Normal;
                        dateTimePicker.Width = DefaultControlWidth;
                        dateTimePicker.IsEnabled = searchTerm.UseForSearching;
                        dateTimePicker.TabIndex = tabIndex++;
                        dateTimePicker.ValueChanged += DateTimeCustomPicker_ValueChanged;
                        Grid.SetRow(dateTimePicker, gridRowIndex);
                        Grid.SetColumn(dateTimePicker, ValueColumn);
                        SearchTerms.Children.Add(dateTimePicker);
                        break;

                    case Control.Date_:
                        WatermarkDateTimePicker datePicker = DateTimeHandler.TryParseDatabaseOrDisplayDate(searchTerm.DatabaseValue, out DateTime date)
                            ? CreateControls.CreateWatermarkDateTimePicker(String.Empty, DateTimeFormatEnum.DateOnly, date)
                            : CreateControls.CreateWatermarkDateTimePicker(String.Empty, DateTimeFormatEnum.DateOnly, ControlDefault.Date_DefaultValue);
                        datePicker.FontWeight = FontWeights.Normal;
                        datePicker.Width = DefaultControlWidth;
                        datePicker.IsEnabled = searchTerm.UseForSearching;
                        datePicker.TabIndex = tabIndex++;
                        datePicker.ValueChanged += DatePicker_ValueChanged;
                        Grid.SetRow(datePicker, gridRowIndex);
                        Grid.SetColumn(datePicker, ValueColumn);
                        SearchTerms.Children.Add(datePicker);
                        break;
                    case Control.Time_:
                        WatermarkTimePicker timePicker = DateTimeHandler.TryParseDatabaseTime(searchTerm.DatabaseValue, out DateTime time)
                            ? CreateControls.CreateWatermarkTimePicker(String.Empty, time)
                            : CreateControls.CreateWatermarkTimePicker(String.Empty, ControlDefault.Time_DefaultValue);
                        timePicker.FontWeight = FontWeights.Normal;
                        timePicker.Width = DefaultControlWidth;
                        timePicker.IsEnabled = searchTerm.UseForSearching;
                        timePicker.TabIndex = tabIndex++;
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
                    // Set TabIndex to be after the first Date value control
                    CheckBoxUseTime.TabIndex = tabIndex++;
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
                    // Save the tab index position where And/Or buttons will be
                    // But don't increment tabIndex yet - we'll set the And/Or button TabIndex values after all rows are processed
                    andOrTabIndex = tabIndex;
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

            // Set TabIndex for And/Or buttons after all rows are processed
            // They should come at the end of the tab order for the grid
            if (andOrTabIndex >= 0)
            {
                RadioButtonTermCombiningAnd.TabIndex = tabIndex++;
                RadioButtonTermCombiningOr.TabIndex = tabIndex;
            }

            dontUpdate = false;
            await UpdateSearchDialogFeedback(false);

            // Set the UseTime state based on what was last recorded
            CheckBoxUseTime.IsChecked = Database.CustomSelection.UseTimeInsteadOfDate;

            // Set the selected item to the Note field with episode data in it.
            Database.CustomSelection.EpisodeNoteField = NoteDataLabelContainingEpisodeData;
            Database.CustomSelection.EpisodeShowAllIfAnyMatch = CheckboxShowAllEpisodeImages.IsChecked == true;
        }
        #endregion

        #region RecognitionSelector - Create/Destroy
        // Create the RecognitionSelector control 
        private void CreateRecognitionSelectorControl()
        {
            if (this.RecognitionSelector != null)
            {
                // This shouldn't happen
                return;
            }

            // Create the RecognitionSelector
            this.RecognitionSelector = new(this, this.BusyCancelIndicator)
            {
                Margin = new(0, 10, 10, 10),
                Visibility = Visibility.Visible,
                Background = Brushes.Azure
            };

            // Adjust the Groupbox appearance to contain it
            this.RecognitionsGroupBox.BorderThickness = new(2);
            this.RecognitonsGroupBoxHeaderText.FontWeight = FontWeights.DemiBold;
            this.RecognitionsGroupBox.Content = this.RecognitionSelector;

            // Create an event handler to receive RecognitionSelector events
            this.RecognitionSelector.RecognitionSelectionEvent += RecognitionSelector_OnRecognitionSelectionEvent;
        }

        // Destroy the RecognitionSelector control
        private void DestroyRecognitionSelectorControl()
        {
            // Remove the RecognitionSelector event handler
            if (this.RecognitionSelector != null)
            {
                this.RecognitionSelector.RecognitionSelectionEvent -= RecognitionSelector_OnRecognitionSelectionEvent;
                this.RecognitionSelector = null;
            }
            this.RecognitionsGroupBox.BorderThickness = new(0);
            this.RecognitonsGroupBoxHeaderText.FontWeight = FontWeights.Normal;
            this.RecognitionsGroupBox.Content = null;
        }
        #endregion

        #region RecognitionSelector - EventHandler
        private void RecognitionSelector_OnRecognitionSelectionEvent(object sender, RecognitionSelectionChangedEventArgs e)
        {
            this.RefreshRecognitionCountsRequired = e.RefreshRecognitionCountsRequired;
            this.RecognitonsGroupBoxFeedback.Text = ComposeRecognitionsSelectionFeedback(e.DetectionCategoryLabel, e.ClassificationCategoryLabel);
            bool showAllEpisodeEnableState = false == e.DisableEpisodeAny || string.IsNullOrEmpty(Database.CustomSelection.EpisodeNoteField);

            this.ShowEpisodeOptionsPanel.IsEnabled = showAllEpisodeEnableState;
            this.CheckboxShowAllEpisodeImages.Foreground = showAllEpisodeEnableState
                ? Brushes.Black
                : Brushes.Gray;
            this.CountTimer.Start();
        }

        private static string ComposeRecognitionsSelectionFeedback(string detectionCategory, string classificationCategory)
        {
            if (string.IsNullOrEmpty(detectionCategory) && string.IsNullOrEmpty(classificationCategory))
            {
                return (" (No recognitions selected)");
            }

            if (false == string.IsNullOrEmpty(detectionCategory) && string.IsNullOrEmpty(classificationCategory))
            {
                return ($" ({detectionCategory})");
            }

            if (string.IsNullOrEmpty(detectionCategory) && false == string.IsNullOrEmpty(classificationCategory))
            {
                return ($" ({classificationCategory})");
            }

            return ($" ({detectionCategory}:{classificationCategory})");
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
            Separator separator = new()
            {
                Width = double.NaN
            };

            // Haader text
            TextBlock tbHeader = new()
            {
                Text = "Choose how terms below are combined:",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Normal,
            };

            // And control
            StackPanel spAnd = new()
            {
                Orientation = Orientation.Horizontal,
                Width = double.NaN
            };

            TextBlock tbAnd = new()
            {
                Text = "to match all selected conditions",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Normal,
            };
            spAnd.Children.Add(RadioButtonTermCombiningAnd);
            spAnd.Children.Add(tbAnd);

            // Or control
            StackPanel spOr = new()
            {
                Name = "TermCombiningOr",
                Orientation = Orientation.Horizontal,
                Width = Double.NaN,
            };

            TextBlock tbOr = new()
            {
                Text = "to match at least one selected conditions",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Normal,
            };
            spOr.Children.Add(RadioButtonTermCombiningOr);
            spOr.Children.Add(tbOr);

            // Container for above
            StackPanel sp = new()
            {
                Orientation = Orientation.Vertical,
                Margin = new(10, 0, 0, 0),
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
            Separator separator = new()
            {
                Width = double.NaN
            };

            // Haader text
            TextBlock tbHeader = new()
            {
                Text = "These terms are combined using AND:" + Environment.NewLine + "returned files match all selected conditions.",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Normal,
            };

            // Container for above
            StackPanel sp = new()
            {
                Orientation = Orientation.Vertical,
                Margin = new(10, 0, 0, 0),
                Width = double.NaN
            };

            sp.Children.Add(separator);
            sp.Children.Add(tbHeader);
            return sp;
        }
        #endregion

        #region Query formation callbacks
        // Radio buttons for determing if we use And or Or
        private async void AndOrRadioButton_Checked(object sender, RoutedEventArgs args)
        {
            try
            {
                await AndOrRadioButton_CheckedAsync(sender);
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task AndOrRadioButton_CheckedAsync(object sender)
        {
            RadioButton radioButton = sender as RadioButton;
            Database.CustomSelection.TermCombiningOperator = (radioButton == RadioButtonTermCombiningAnd) ? CustomSelectionOperatorEnum.And : CustomSelectionOperatorEnum.Or;
            await UpdateSearchDialogFeedback();
        }

        // Select: When the use checks or unchecks the checkbox for a row
        // - activate or deactivate the search criteria for that row
        // - update the searchterms to reflect the new status
        // - update the UI to activate or deactivate (or show or hide) its various search terms
        private async void Select_CheckedOrUnchecked(object sender, RoutedEventArgs args)
        {
            try
            {
                await Select_CheckedOrUncheckedAsync(sender);
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task Select_CheckedOrUncheckedAsync(object sender)
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

            await UpdateSearchDialogFeedback();
        }

        // Operator: The user has selected a new expression
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria
        private async void Operator_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            try
            {
                await Operator_SelectionChangedAsync(sender);
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task Operator_SelectionChangedAsync(object sender)
        {
            if (sender is ComboBox comboBox == false)
            {
                // This shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            int row = Grid.GetRow(comboBox);  // Get the row number...
            Database.CustomSelection.SearchTerms[row - 1].Operator = comboBox.SelectedValue.ToString(); // Set the corresponding expression to the current selection
            await UpdateSearchDialogFeedback();
        }

        // Value (Counters and Notes): The user has selected a new value
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria
        private async void Note_TextChanged(object sender, TextChangedEventArgs args)
        {
            try
            {
                await Note_TextChangedAsync(sender);
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task Note_TextChangedAsync(object sender)
        {
            if (sender is TextBox textBox == false)
            {
                // This shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            int row = Grid.GetRow(textBox);  // Get the row number...
            Database.CustomSelection.SearchTerms[row - 1].DatabaseValue = textBox.Text;
            await UpdateSearchDialogFeedback();
        }

        // Value (Counters and Notes): The user has selected a new value
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria

        private async void MultiLineValue_TextHasChanged(object sender, EventArgs e)
        {
            try
            {
                await MultiLineValue_TextHasChangedAsync(sender);
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task MultiLineValue_TextHasChangedAsync(object sender)
        {
            if (sender is MultiLineText textBox == false)
            {
                // This shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            int row = Grid.GetRow(textBox);  // Get the row number...
            Database.CustomSelection.SearchTerms[row - 1].DatabaseValue = textBox.Text;
            await UpdateSearchDialogFeedback();
        }

        // Handle Tab key in MultiLineText popup to properly continue tab navigation
        private void MultiLineValue_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not MultiLineText multiLineText)
            {
                return;
            }

            // Only handle Tab when the popup is open
            if (e.Key == Key.Tab && multiLineText.EditorPopup is { IsOpen: true })
            {
                // Commit and close the popup
                multiLineText.Commit();

                // Move focus to next/previous control in tab order
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var direction = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                        ? FocusNavigationDirection.Previous
                        : FocusNavigationDirection.Next;
                    multiLineText.MoveFocus(new TraversalRequest(direction));
                }), DispatcherPriority.Input);

                e.Handled = true;
            }
        }

        private async void Integer_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> args)
        {
            try
            {
                await Integer_ValueChangedAsync(sender);
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task Integer_ValueChangedAsync(object sender)
        {
            if (sender is IntegerUpDown textBox == false)
            {
                // This shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            int row = Grid.GetRow(textBox);  // Get the row number...
            Database.CustomSelection.SearchTerms[row - 1].DatabaseValue = textBox.Text;
            await UpdateSearchDialogFeedback();
        }

        private async void Decimal_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> args)
        {
            try
            {
                await Decimal_ValueChangedAsync(sender);
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task Decimal_ValueChangedAsync(object sender)
        {
            if (sender is DoubleUpDown textBox == false)
            {
                // This shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            int row = Grid.GetRow(textBox);  // Get the row number...
            Database.CustomSelection.SearchTerms[row - 1].DatabaseValue = textBox.Text;
            await UpdateSearchDialogFeedback();
        }

        // Value (DateTime): we need to construct a string DateTime from it
        private async void DateTime_SelectedDateChanged(object sender, RoutedPropertyChangedEventArgs<object> args)
        {
            try
            {
                await DateTime_SelectedDateChangedAsync(sender);
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task DateTime_SelectedDateChangedAsync(object sender)
        {
            if (sender is WatermarkDateTimePicker datePicker == false)
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
                await UpdateSearchDialogFeedback();
            }
        }

        // Value (FixedChoice): The user has selected a new value
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria
        private async void FixedChoice_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            try
            {
                await FixedChoice_SelectionChangedAsync(sender);
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task FixedChoice_SelectionChangedAsync(object sender)
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
            await UpdateSearchDialogFeedback();
        }

        // Value: (MultiChoice)
        private async void WatermarkCheckComboBox_ItemSelectionChanged(object sender, ItemSelectionChangedEventArgs e)
        {
            try
            {
                await WatermarkCheckComboBox_ItemSelectionChangedAsync(sender);
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task WatermarkCheckComboBox_ItemSelectionChangedAsync(object sender)
        {
            if (sender is WatermarkCheckComboBox checkComboBox == false)
            {
                TracePrint.NullException(nameof(checkComboBox));
                return;
            }

            if (checkComboBox.SelectedItemsOverride != null)
            {
                // Parse the current checkComboBox items a text string to update the checkComboBox text as needed
                List<string> list = [];
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
            await UpdateSearchDialogFeedback();

        }

        // Value (Flags): The user has checked or unchecked a new value
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria
        private async void Flag_CheckedOrUnchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                await Flag_CheckedOrUncheckedAsync(sender);
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task Flag_CheckedOrUncheckedAsync(object sender)
        {
            if (sender is CheckBox checkBox == false)
            {
                // This shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            int row = Grid.GetRow(checkBox);  // Get the row number...
            Database.CustomSelection.SearchTerms[row - 1].DatabaseValue = checkBox.IsChecked.ToString().ToLower(); // Set the corresponding value to the current selection
            await UpdateSearchDialogFeedback();
        }

        private async void DateTimeCustomPicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            try
            {
                await DateTimeCustomPicker_ValueChangedAsync(sender);
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task DateTimeCustomPicker_ValueChangedAsync(object sender)
        {
            if (sender is WatermarkDateTimePicker dateTimePicker == false)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            int row = Grid.GetRow(dateTimePicker);  // Get the row number...
            Database.CustomSelection.SearchTerms[row - 1].DatabaseValue = DateTimeHandler.DateTimeDisplayStringToDataBaseString(dateTimePicker.Text); // Set the corresponding value to the current selection
            await UpdateSearchDialogFeedback();
        }

        private async void DatePicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            try
            {
                await DatePicker_ValueChangedAsync(sender);
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task DatePicker_ValueChangedAsync(object sender)
        {
            if (sender is WatermarkDateTimePicker dateTimePicker == false)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            int row = Grid.GetRow(dateTimePicker);  // Get the row number...
            Database.CustomSelection.SearchTerms[row - 1].DatabaseValue = DateTimeHandler.DateDisplayStringToDataBaseString(dateTimePicker.Text); // Set the corresponding value to the current selection
            await UpdateSearchDialogFeedback();
        }

        private async void TimePicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            try
            {
                await TimePicker_ValueChangedAsync(sender);
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task TimePicker_ValueChangedAsync(object sender)
        {
            if (sender is WatermarkTimePicker timePicker == false)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            int row = Grid.GetRow(timePicker);  // Get the row number...
            Database.CustomSelection.SearchTerms[row - 1].DatabaseValue = timePicker.Text; // Set the corresponding value to the current selection
            await UpdateSearchDialogFeedback();
        }

        // The RelativePathControl SelectedItemChanged callback: This does the work when a relative path is selected
        private async void RelativePathControl_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            try
            {
                await RelativePathControl_SelectedItemChangedAsync(sender);
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task RelativePathControl_SelectedItemChangedAsync(object sender)
        {
            if (this.treeViewWithRelativePaths.DontInvoke)
            {
                return;
            }

            if (sender is not TreeViewWithRelativePaths treeView)
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
            await this.UpdateSearchDialogFeedback();
        }

        // When this button is pressed, all the search terms checkboxes are cleared, which is equivalent to showing all images
        private void ResetToAllImagesButton_Click(object sender, RoutedEventArgs e)
        {
            //EnableRecognitionsCheckbox.IsChecked = false;
            for (int row = 1; row <= Database.CustomSelection.SearchTerms.Count; row++)
            {
                CheckBox select = GetGridElement<CheckBox>(SelectColumn, row);
                select.IsChecked = false;
            }
            //ShowMissingDetectionsCheckbox.IsChecked = false;
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
        // TODO: This was originally an async method, but there is no await in it now.
        // Rather than making it syncronous, I just return Task.CompletedTask so I don't have to change
        // the return value or its callers.
        private Task UpdateSearchDialogFeedback(bool refreshRecognitionSelectorCount = true)
        {
            if (dontUpdate)
            {
                return Task.CompletedTask;
            }

            if (null != this.RecognitionSelector && refreshRecognitionSelectorCount)
            {
                this.RecognitionSelector.ClearCountsAndResetUI();
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
            ResetToAllImagesButton.IsEnabled = atLeastOneSearchTermIsSelected;

            // Enable the and/or radio buttons if more than one non-standard selection was made
            RadioButtonTermCombiningAnd.IsEnabled = multipleNonStandardSelectionsMade > 1;
            RadioButtonTermCombiningOr.IsEnabled = multipleNonStandardSelectionsMade > 1;
            return Task.CompletedTask;
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
        private void EnableRecognitions_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (dontInvoke)
            {
                return;
            }

            if (this.EnableRecognitionsCheckbox.IsChecked == true)
            {
                this.CreateRecognitionSelectorControl();
                RecognitionsGroupBox.Background = Brushes.Azure;
            }
            else
            {
                this.DestroyRecognitionSelectorControl();
                this.RecognitonsGroupBoxFeedback.Text = string.Empty;
                RecognitionsGroupBox.Background = Brushes.White;
            }
            // Enable or disable the controls depending on the various checkbox states

            SetDetectionCriteria();
            InitiateShowCountsOfMatchingFiles();
        }

        private void SetDetectionCriteria()
        {
            if (IsLoaded == false || dontInvoke)
            {
                return;
            }
            RecognitionSelections.UseRecognition = EnableRecognitionsCheckbox.IsChecked == true;

            // The BoundingBoxDisplayThreshold is the user-defined default set in preferences, while the BoundingBoxThresholdOveride is the threshold
            // determined in this select dialog. For example, if (say) the preference setting is .6 but the selection is at .4 confidence, then we should 
            // show bounding boxes when the confidence is .4 or more. On the other  hand, we don't want to show spurious detections when empty is selected,
            // so we set a minimum value.
            CustomSelection.SetDetectionRanges(RecognitionSelections);
        }
        #endregion

        #region Common to Selections and Detections
        private void CountTimer_Tick(object sender, EventArgs e)
        {
            CountTimer.Stop();
            // This is set everytime a selection is made
            if (dontCount)
            {
                return;
            }


            int count = Database.CountAllFilesMatchingSelectionCondition(FileSelectionEnum.Custom);
            MatchingFilesCount.Text = count > 0 ? count.ToString() : "0";
            this.MatchingFilesCountLabel.Text = count == 1
                ? " file matches your query"
                : " files match your query";

            OkButton.IsEnabled = count > 0; // Dusable OK button if there are no matches
            //if (this.Database.CustomSelection.ShowMissingDetections)
            //{
            //    System.Diagnostics.Debug.Print("Show missing detections:" + this.Database.CustomSelection.ShowMissingDetections.ToString());
            //}
            if (null != this.RecognitionSelector )
            {
                this.RecognitionSelector.UpdateDisplayOfTotalFileCounts(MatchingFilesCount.Text);
                if (this.RefreshRecognitionCountsRequired 
                    && false == this.Database.CustomSelection.ShowMissingDetections)
                {
                    // await this.RecognitionSelector.RecognitionsRefreshCounts();
                    this.RecognitionSelector.ClearCountsAndResetUI(true);
                }
            }

            this.RefreshRecognitionCountsRequired = true;
            // Uncomment this to add feedback to the File count line desribing the kinds of files selected
            //if (this.UseDetectionsCheckbox.IsChecked == false)
            //{
            //    this.QueryFileMatchNote.Text = string.Empty;
            //}
            //else
            //{
            //    if ((string)this.DetectionCategoryComboBox.SelectedItem == Constant.RecognizerValues.EmptyDetectionLabel)
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
            CountTimer.Stop();
            CountTimer.Start();
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

        #region EpisodeStuff - Move this code into proper regions later
        private async void CheckboxShowAllEpisodeImages_CheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                await CheckboxShowAllEpisodeImages_CheckedChangedAsync();
            }
            catch (Exception ex)
            {
                TracePrint.CatchException(ex.Message);
            }
        }

        private async Task CheckboxShowAllEpisodeImages_CheckedChangedAsync()
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
            Database.IndexCreateForEpisodeFieldIfNeeded(NoteDataLabelContainingEpisodeData);
            Database.CustomSelection.EpisodeShowAllIfAnyMatch = true == CheckboxShowAllEpisodeImages.IsChecked;
            await UpdateSearchDialogFeedback();
        }

        private static bool EpisodeFieldCheckFormat(ImageRow row, string dataLabel)
        {
            if (string.IsNullOrWhiteSpace(dataLabel))
            {
                return false;
            }
            string value = row.GetValueDisplayString(dataLabel);
            return null != value && Regex.IsMatch(value, RegExExpressions.NotEpisodeCharacters);
        }
        #endregion

        #region RelativePathControl methods
        private void RelativePathCreateControl(SearchTerm searchTerm, Thickness thickness, int gridRowIndex, CheckBox checkboxforUsingRelativePath, int controlTabIndex)
        {
            // Relative path uses a dropdown button that shows existing relative path folders as a treeview
            this.RelativePathButton = new()
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
                TabIndex = controlTabIndex
            };

            // Add the TreeViewWithRelativePaths control as the DropDown button's drop down.
            // This treeview is specialized to show relative paths
            this.treeViewWithRelativePaths = new()
            {
                DontInvoke = true,
            };
            this.RelativePathButton.DropDownContent = treeViewWithRelativePaths;

            // Populate the treeview. Enable it only if it has content
            this.RelativePathControlRepopulateIfNeeded();
            this.treeViewWithRelativePaths.FocusSelection = true;
            this.RelativePathButton.IsEnabled = this.treeViewWithRelativePaths.HasContent && checkboxforUsingRelativePath is { IsChecked: true };
            if (checkboxforUsingRelativePath != null)
            {
                checkboxforUsingRelativePath.IsEnabled = this.treeViewWithRelativePaths.HasContent;
            }
            this.treeViewWithRelativePaths.SelectedItemChanged += RelativePathControl_SelectedItemChanged;
            RelativePathButton.GotFocus += ControlsDataHelpers.Control_GotFocus;
            RelativePathButton.LostFocus += ControlsDataHelpers.Control_LostFocus;

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
                List<string> newFolderList = [];
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
