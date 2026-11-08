using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Timelapse.Constant;
using Timelapse.ControlsCore;
using Timelapse.ControlsDataEntry;
using Timelapse.ControlsMetadata;
using Timelapse.Enums;
using Timelapse.SearchingAndSorting;
using Timelapse.Util;
using TimelapseWpf.Toolkit;
using Control = Timelapse.Constant.Control;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace Timelapse.Controls
{
    /// <summary>
    /// Lets the user specify search terms for the folder metadata, level by level, along with how they are combined.
    /// It appears in the custom selection dialog when the user expands its Select folder metadata area.
    /// Its rows are like the dialog's other search term rows, but are in their own control as those are tied to the positions of the data field search terms.
    /// </summary>
    public class FolderMetadataSelector : UserControl
    {
        private const int DefaultControlWidth = 200;
        private const int SelectColumn = 0;
        private const int LabelColumn = 1;
        private const int OperatorColumn = 2;
        private const int ValueColumn = 3;

        // The same wording as the and/or buttons in the rest of the dialog
        private const string MatchAll = "And to match all selected conditions";
        private const string MatchAtLeastOne = "Or to match at least one selected condition";

        private readonly FolderMetadataSelection selection;
        private readonly DataEntryControls dataEntryControls;
        private readonly Grid termsGrid = new();

        // Raised whenever the user changes anything that could change the query
        public event EventHandler SelectionChanged;

        public FolderMetadataSelector(FolderMetadataSelection selection, DataEntryControls dataEntryControls)
        {
            this.selection = selection;
            this.dataEntryControls = dataEntryControls;

            StackPanel panel = new();
            panel.Children.Add(CreateBetaTestingNote());
            panel.Children.Add(CreateCombiningOperatorButtons());

            foreach (GridLength width in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto, new(1, GridUnitType.Star) })
            {
                termsGrid.ColumnDefinitions.Add(new() { Width = width });
            }
            AddColumnHeaders();
            AddTopLevelExplanation();
            foreach (FolderLevelSearchTerms level in selection.Levels)
            {
                AddLevel(level);
            }
            panel.Children.Add(termsGrid);
            Content = panel;
        }

        // A description of the terms in use, e.g., for showing as feedback. It is empty if there are none.
        public static string DescribeTermsInUse(FolderMetadataSelection selection)
        {
            return string.Join("; ", selection.Levels
                .SelectMany(level => level.SearchTerms)
                .Where(term => term.UseForSearching)
                .Select(term => $"{term.Label} {term.Operator.Trim()} {(string.IsNullOrEmpty(term.DatabaseValue) ? "(empty)" : term.DatabaseValue)}"));
        }

        #region Layout
        private static TextBlock CreateBetaTestingNote()
        {
            TextBlock note = new()
            {
                Foreground = Brushes.Red,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new(5, 0, 5, 0)
            };
            Hyperlink email = new(new Run(ExternalLinks.EmailAddress)) { NavigateUri = new(ExternalLinks.EmailAddressAsMailTo) };
            email.RequestNavigate += (_, e) =>
            {
                ProcessExecution.TryProcessStart(e.Uri);
                e.Handled = true;
            };
            note.Inlines.Add("Note: This is a beta preview for testing. Email ");
            note.Inlines.Add(email);
            note.Inlines.Add(" with your experiences.");
            return note;
        }

        private StackPanel CreateCombiningOperatorButtons()
        {
            StackPanel panel = new() { Orientation = Orientation.Horizontal, Margin = new(5) };
            panel.Children.Add(new TextBlock { Text = "Combine these terms using", VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(CreateOperatorButton(MatchAll, CustomSelectionOperatorEnum.And));
            panel.Children.Add(CreateOperatorButton(MatchAtLeastOne, CustomSelectionOperatorEnum.Or));
            return panel;
        }

        private RadioButton CreateOperatorButton(string content, CustomSelectionOperatorEnum combiningOperator)
        {
            RadioButton button = new()
            {
                Content = content,
                GroupName = "FolderMetadataCombine",
                IsChecked = combiningOperator == selection.CombiningOperator,
                Margin = new(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            button.Checked += (_, _) =>
            {
                selection.CombiningOperator = combiningOperator;
                OnSelectionChanged();
            };
            return button;
        }

        private void AddColumnHeaders()
        {
            int row = AddRow();
            AddToGrid(new TextBlock { Text = "Select", FontWeight = FontWeights.Bold, Padding = new(10, 0, 5, 0) }, row, SelectColumn);
            AddToGrid(new TextBlock { Text = "Label", FontWeight = FontWeights.Bold, Padding = new(5, 0, 5, 0) }, row, LabelColumn);
            AddToGrid(new TextBlock { Text = "Expression", FontWeight = FontWeights.Bold, Padding = new(5, 0, 5, 0) }, row, OperatorColumn);
            AddToGrid(new TextBlock { Text = "Value", FontWeight = FontWeights.Bold, Padding = new(5, 0, 5, 0) }, row, ValueColumn);
        }

        // The top level's name appears in the same style as the other levels' headings, followed by why it has no terms
        private void AddTopLevelExplanation()
        {
            if (selection.TopLevelName == null)
            {
                return;
            }
            TextBlock explanation = new()
            {
                TextWrapping = TextWrapping.Wrap,
                Margin = new(5, 8, 5, 2)
            };
            explanation.Inlines.Add(new Run(selection.TopLevelName) { FontWeight = FontWeights.Bold, FontStyle = FontStyles.Italic });
            explanation.Inlines.Add(new Run(" Top-level fields are not shown, as they are common to all images."));
            Grid.SetColumnSpan(explanation, termsGrid.ColumnDefinitions.Count);
            AddToGrid(explanation, AddRow(), SelectColumn);
        }

        private void AddLevel(FolderLevelSearchTerms level)
        {
            TextBlock heading = new()
            {
                Text = MetadataUI.CreateTemporaryAliasIfNeeded(level.Level, level.Alias),
                FontWeight = FontWeights.Bold,
                FontStyle = FontStyles.Italic,
                Margin = new(5, 8, 5, 2)
            };
            if (false == level.HasMetadata)
            {
                heading.Inlines.Add(new Run(" (no metadata has been created)") { FontWeight = FontWeights.Normal });
                heading.Foreground = Brushes.Gray;
            }
            Grid.SetColumnSpan(heading, termsGrid.ColumnDefinitions.Count);
            AddToGrid(heading, AddRow(), SelectColumn);

            foreach (SearchTerm term in level.SearchTerms)
            {
                AddTerm(level, term);
            }
        }

        private void AddTerm(FolderLevelSearchTerms level, SearchTerm term)
        {
            int row = AddRow();
            Thickness thickness = new(5, 2, 5, 2);

            CheckBox select = new()
            {
                IsChecked = term.UseForSearching,
                IsEnabled = level.HasMetadata,
                Margin = thickness,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            AddToGrid(select, row, SelectColumn);

            TextBlock label = new()
            {
                Text = term.Label,
                FontWeight = term.UseForSearching ? FontWeights.DemiBold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new(5)
            };
            if (false == level.HasMetadata)
            {
                label.Foreground = Brushes.Gray;
            }
            AddToGrid(label, row, LabelColumn);

            ComboBox operators = new()
            {
                IsEnabled = term.UseForSearching,
                ItemsSource = OperatorsFor(term.ControlType),
                Margin = thickness,
                Width = 78,
                Height = 25,
                SelectedValue = term.Operator
            };
            operators.SelectionChanged += (_, _) =>
            {
                term.Operator = operators.SelectedValue.ToString();
                OnSelectionChanged();
            };
            AddToGrid(operators, row, OperatorColumn);

            FrameworkElement value = CreateValueControl(term, thickness);
            value.IsEnabled = term.UseForSearching;
            AddToGrid(value, row, ValueColumn);

            RoutedEventHandler selectChanged = (_, _) =>
            {
                term.UseForSearching = true == select.IsChecked;
                label.FontWeight = term.UseForSearching ? FontWeights.DemiBold : FontWeights.Normal;
                operators.IsEnabled = term.UseForSearching;
                value.IsEnabled = term.UseForSearching;
                OnSelectionChanged();
            };
            select.Checked += selectChanged;
            select.Unchecked += selectChanged;
        }

        private int AddRow()
        {
            termsGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });
            return termsGrid.RowDefinitions.Count - 1;
        }

        private void AddToGrid(UIElement element, int row, int column)
        {
            Grid.SetRow(element, row);
            Grid.SetColumn(element, column);
            termsGrid.Children.Add(element);
        }
        #endregion

        #region The expressions and values for each type of field, the same as for the other search terms
        private static string[] OperatorsFor(string controlType)
        {
            return controlType switch
            {
                Control.Counter or Control.IntegerAny or Control.IntegerPositive or Control.DecimalAny or Control.DecimalPositive
                    or Control.FixedChoice or Control.DateTime_ or Control.Date_ or Control.Time_ =>
                [
                    SearchTermOperator.Equal, SearchTermOperator.NotEqual, SearchTermOperator.LessThan, SearchTermOperator.GreaterThan,
                    SearchTermOperator.LessThanOrEqual, SearchTermOperator.GreaterThanOrEqual
                ],
                Control.MultiChoice => [SearchTermOperator.Equal, SearchTermOperator.NotEqual, SearchTermOperator.Includes, SearchTermOperator.Excludes],
                Control.Flag => [SearchTermOperator.Equal, SearchTermOperator.NotEqual],
                _ =>
                [
                    SearchTermOperator.Equal, SearchTermOperator.NotEqual, SearchTermOperator.LessThan, SearchTermOperator.GreaterThan,
                    SearchTermOperator.LessThanOrEqual, SearchTermOperator.GreaterThanOrEqual, SearchTermOperator.Glob, SearchTermOperator.NotGlob
                ]
            };
        }

        private FrameworkElement CreateValueControl(SearchTerm term, Thickness thickness)
        {
            return term.ControlType switch
            {
                Control.MultiLine => CreateMultiLineValue(term, thickness),
                Control.Counter or Control.IntegerAny or Control.IntegerPositive => CreateIntegerValue(term, thickness),
                Control.DecimalAny or Control.DecimalPositive => CreateDecimalValue(term, thickness),
                Control.FixedChoice => CreateFixedChoiceValue(term, thickness),
                Control.MultiChoice => CreateMultiChoiceValue(term, thickness),
                Control.Flag => CreateFlagValue(term, thickness),
                Control.DateTime_ => CreateDateTimeValue(term),
                Control.Date_ => CreateDateValue(term),
                Control.Time_ => CreateTimeValue(term),
                _ => CreateTextValue(term, thickness)
            };
        }

        private ImprintAutoCompleteTextBox CreateTextValue(SearchTerm term, Thickness thickness)
        {
            ImprintAutoCompleteTextBox textBox = new()
            {
                Text = term.DatabaseValue,
                Margin = thickness,
                Width = DefaultControlWidth,
                Height = 25,
                Padding = new(0, 1, 0, 0),
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Top
            };
            if (term.ControlType == Control.AlphaNumeric)
            {
                textBox.PreviewKeyDown += ValidationCallbacks.PreviewKeyDown_TextBoxNoSpaces;
                textBox.PreviewTextInput += ValidationCallbacks.PreviewInput_AlphaNumericCharacterOnlyWithGlob;
                textBox.TextChanged += ValidationCallbacks.TextChanged_AlphaNumericTextWithGlobCharactersOnly;
            }
            textBox.TextChanged += (_, _) => SetValue(term, textBox.Text);
            AddFocusFeedback(textBox);
            return textBox;
        }

        private MultiLineText CreateMultiLineValue(SearchTerm term, Thickness thickness)
        {
            MultiLineText multiLineText = new()
            {
                Text = term.DatabaseValue,
                Margin = thickness,
                Width = DefaultControlWidth,
                Height = 25,
                Padding = new(0, 2, 0, 0),
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Top,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Style = (Style)dataEntryControls.FindResource("MultiLineTextBox"),
                IsTabStop = true
            };
            multiLineText.TextChanged += (_, _) => SetValue(term, multiLineText.Text);
            multiLineText.PreviewKeyDown += (_, e) =>
            {
                // The Tab key should commit the text and move on from the popup editor
                if (e.Key == Key.Tab && multiLineText.EditorPopup is { IsOpen: true })
                {
                    multiLineText.Commit();
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        FocusNavigationDirection direction = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                            ? FocusNavigationDirection.Previous
                            : FocusNavigationDirection.Next;
                        multiLineText.MoveFocus(new(direction));
                    }), DispatcherPriority.Input);
                    e.Handled = true;
                }
            };
            return multiLineText;
        }

        private IntegerUpDown CreateIntegerValue(SearchTerm term, Thickness thickness)
        {
            IntegerUpDown upDown = new()
            {
                Margin = thickness,
                Width = DefaultControlWidth,
                Height = 22,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Minimum = term.ControlType == Control.IntegerAny ? int.MinValue : 0
            };
            bool isValid = int.TryParse(term.DatabaseValue, out int value);
            upDown.Text = isValid ? term.DatabaseValue : "0";
            upDown.Value = isValid ? value : 0;
            if (term.ControlType == Control.IntegerAny)
            {
                upDown.PreviewTextInput += ValidationCallbacks.PreviewInput_IntegerCharacterOnly;
                DataObject.AddPastingHandler(upDown, ValidationCallbacks.Paste_OnlyIfIntegerAny);
            }
            else
            {
                upDown.PreviewTextInput += ValidationCallbacks.PreviewInput_IntegerPositiveCharacterOnly;
                DataObject.AddPastingHandler(upDown, ValidationCallbacks.Paste_OnlyIfIntegerPositive);
            }
            upDown.PreviewKeyDown += ValidationCallbacks.PreviewKeyDown_IntegerUpDownNoSpaces;
            upDown.ValueChanged += (_, _) => SetValue(term, upDown.Text);
            AddFocusFeedback(upDown);
            return upDown;
        }

        private DoubleUpDown CreateDecimalValue(SearchTerm term, Thickness thickness)
        {
            DoubleUpDown upDown = new()
            {
                Margin = thickness,
                Width = DefaultControlWidth,
                Height = 22,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                FormatString = ControlDefault.DecimalFormatString,
                CultureInfo = CultureInfo.InvariantCulture,
                Minimum = term.ControlType == Control.DecimalAny ? double.MinValue : 0
            };
            bool isValid = double.TryParse(term.DatabaseValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double value);
            upDown.Text = isValid ? term.DatabaseValue : "0";
            upDown.Value = isValid ? value : 0;
            if (term.ControlType == Control.DecimalPositive)
            {
                upDown.PreviewTextInput += ValidationCallbacks.PreviewInput_DecimalPositiveCharacterOnly;
                DataObject.AddPastingHandler(upDown, ValidationCallbacks.Paste_OnlyIfDecimalPositive);
            }
            else
            {
                upDown.PreviewTextInput += ValidationCallbacks.PreviewInput_DecimalCharacterOnly;
                DataObject.AddPastingHandler(upDown, ValidationCallbacks.Paste_OnlyIfDecimalAny);
            }
            upDown.PreviewKeyDown += ValidationCallbacks.PreviewKeyDown_DecimalUpDownNoSpaces;
            upDown.ValueChanged += (_, _) => SetValue(term, upDown.Text);
            AddFocusFeedback(upDown);
            return upDown;
        }

        private ComboBox CreateFixedChoiceValue(SearchTerm term, Thickness thickness)
        {
            ComboBox comboBox = new()
            {
                Width = DefaultControlWidth,
                Margin = thickness,
                ItemsSource = term.List,
                SelectedItem = term.DatabaseValue
            };
            comboBox.SelectionChanged += (_, _) =>
            {
                if (comboBox.SelectedValue != null)
                {
                    SetValue(term, comboBox.SelectedValue.ToString());
                }
            };
            AddFocusFeedback(comboBox);
            return comboBox;
        }

        private WatermarkCheckComboBox CreateMultiChoiceValue(SearchTerm term, Thickness thickness)
        {
            WatermarkCheckComboBox checkComboBox = new()
            {
                Width = DefaultControlWidth,
                Margin = thickness,
                ItemsSource = term.List.Where(item => item != string.Empty).ToList()
            };
            checkComboBox.Opened += ControlsDataHelpers.WatermarkCheckComboBox_DropDownOpened;
            checkComboBox.Closed += ControlsDataHelpers.WatermarkCheckComboBox_DropDownClosed;
            checkComboBox.ItemSelectionChanged += (_, _) =>
            {
                if (checkComboBox.SelectedItemsOverride != null)
                {
                    List<string> items = [.. checkComboBox.SelectedItemsOverride.Cast<string>()];
                    items.Sort();
                    string newText = string.Join(",", items).Trim(',');
                    if (checkComboBox.Text != newText)
                    {
                        checkComboBox.Text = newText;
                    }
                }
                SetValue(term, checkComboBox.Text);
            };
            AddFocusFeedback(checkComboBox);
            checkComboBox.Text = term.DatabaseValue;
            return checkComboBox;
        }

        private CheckBox CreateFlagValue(SearchTerm term, Thickness thickness)
        {
            CheckBox checkBox = new()
            {
                Margin = thickness,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                IsChecked = !string.Equals(term.DatabaseValue, BooleanValue.False, StringComparison.OrdinalIgnoreCase)
            };
            term.DatabaseValue = true == checkBox.IsChecked ? BooleanValue.True : BooleanValue.False;
            RoutedEventHandler changed = (_, _) => SetValue(term, checkBox.IsChecked.ToString().ToLower());
            checkBox.Checked += changed;
            checkBox.Unchecked += changed;
            AddFocusFeedback(checkBox);
            return checkBox;
        }

        // The date and time pickers always show a value. So if the field has no value, such as an empty default, set
        // the term to what is displayed, as otherwise the term would be searching for something other than what the user sees.
        private WatermarkDateTimePicker CreateDateTimeValue(SearchTerm term)
        {
            if (false == DateTimeHandler.TryParseDatabaseOrDisplayDateTime(term.DatabaseValue, out DateTime value))
            {
                value = ControlDefault.DateTimeCustomDefaultValue;
                term.DatabaseValue = DateTimeHandler.ToStringDatabaseDateTime(value);
            }
            WatermarkDateTimePicker picker = CreateControls.CreateWatermarkDateTimePicker(string.Empty, DateTimeFormatEnum.DateAndTime, value);
            picker.Width = DefaultControlWidth;
            picker.ValueChanged += (_, _) => SetValue(term, DateTimeHandler.DateTimeDisplayStringToDataBaseString(picker.Text));
            return picker;
        }

        private WatermarkDateTimePicker CreateDateValue(SearchTerm term)
        {
            if (false == DateTimeHandler.TryParseDatabaseOrDisplayDate(term.DatabaseValue, out DateTime value))
            {
                value = ControlDefault.Date_DefaultValue;
                term.DatabaseValue = DateTimeHandler.ToStringDatabaseDate(value);
            }
            WatermarkDateTimePicker picker = CreateControls.CreateWatermarkDateTimePicker(string.Empty, DateTimeFormatEnum.DateOnly, value);
            picker.Width = DefaultControlWidth;
            picker.ValueChanged += (_, _) => SetValue(term, DateTimeHandler.DateDisplayStringToDataBaseString(picker.Text));
            return picker;
        }

        private WatermarkTimePicker CreateTimeValue(SearchTerm term)
        {
            if (false == DateTimeHandler.TryParseDatabaseTime(term.DatabaseValue, out DateTime value))
            {
                value = ControlDefault.Time_DefaultValue;
                term.DatabaseValue = DateTimeHandler.ToStringTime(value);
            }
            WatermarkTimePicker picker = CreateControls.CreateWatermarkTimePicker(string.Empty, value);
            picker.Width = DefaultControlWidth;
            picker.ValueChanged += (_, _) => SetValue(term, picker.Text);
            return picker;
        }

        private static void AddFocusFeedback(UIElement element)
        {
            element.GotFocus += ControlsDataHelpers.Control_GotFocus;
            element.LostFocus += ControlsDataHelpers.Control_LostFocus;
        }

        private void SetValue(SearchTerm term, string value)
        {
            term.DatabaseValue = value;
            OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
        #endregion
    }
}
