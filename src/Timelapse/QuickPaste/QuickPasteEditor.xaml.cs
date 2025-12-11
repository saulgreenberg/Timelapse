using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Timelapse.Constant;
using Timelapse.ControlsCore;
using Timelapse.ControlsDataEntry;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Util;
using TimelapseWpf.Toolkit;
using TimelapseWpf.Toolkit.Primitives;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Control = Timelapse.Constant.Control;
using Style = System.Windows.Style;
using TextBox = System.Windows.Controls.TextBox;

namespace Timelapse.QuickPaste
{
    // Given a QuickPasteEntry (a name and a list of QuickPasteItems),
    // allow the user to edit it.
    // Currently, the only thing that is editable is its name and whether a particular item's data should be included when pasted
    public partial class QuickPasteEditor
    {
        #region Public Properties
        public QuickPasteEntry QuickPasteEntry { get; set; }
        #endregion

        #region Private Variables
        // Columns where fields will be placed in the grid
        private const int GridColumnUse = 1;
        private const int GridColumnLabel = 2;
        private const int GridColumnValue = 3;

        // The initial grid row. We start adding rows after this one.
        // the 1st two grid rows are already filled.
        private const int GridRowInitialRow = 1;

        // For simplicity, create a list with all 'Use' checkboxes in it so we can set or clear all of them
        readonly List<CheckBox> UseCheckboxes = [];

        // UI Constants
        private const double ValuesWidth = 160;
        private const double ValuesHeight = 25;
        private readonly FileDatabase fileDatabase;
        private readonly DataEntryControls dataEntryControls;
        #endregion

        #region Constructor, Loaded
        public QuickPasteEditor(QuickPasteEntry quickPasteEntry, FileDatabase fileDatabase, DataEntryControls dataEntryControls)
        {
            InitializeComponent();
            // Set up static reference resolver for FormattedMessageContent
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            QuickPasteEntry = quickPasteEntry;
            this.fileDatabase = fileDatabase;
            this.dataEntryControls = dataEntryControls;
        }

        // When the window is loaded
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Message.BuildContentFromProperties();
            // Adjust this dialog window position
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            // Display the title of the QuickPasteEntry
            QuickPasteTitle.Text = QuickPasteEntry.Title;
            QuickPasteTitle.TextChanged += QuickPasteTitle_TextChanged;

            // Build the grid rows, each displaying successive items in the QuickPasteItems list
            BuildRows();
        }
        #endregion

        #region Callbacks - UI 
        // Title: Update the QuickPasteEntry's title
        private void QuickPasteTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            QuickPasteEntry.Title = QuickPasteTitle.Text;
        }

        // Use: Invoked when the user clicks the checkbox to enable or disable the data row
        private void UseCurrentRow_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cbox == false)
            {
                TracePrint.NullException(nameof(cbox));
                return;
            }

            // Enable or disable the controls on that row to reflect whether the checkbox is checked or unchecked
            int row = Grid.GetRow(cbox);

            TextBlock label = GetGridElement<TextBlock>(GridColumnLabel, row);
            UIElement value = GetGridElement<UIElement>(GridColumnValue, row);
            if (cbox.IsChecked is true)
            {
                label.Foreground = Brushes.Black;
                if (cbox.IsChecked != null) value.IsEnabled = cbox.IsChecked.Value;
            }
            else
            {
                label.Foreground = Brushes.Gray;
                value.IsEnabled = cbox.IsChecked ?? false;
            }

            // Update the QuickPaste row data structure to reflect the current checkbox state
            QuickPasteItem quickPasteRow = (QuickPasteItem)cbox.Tag;
            quickPasteRow.Use = cbox.IsChecked == true;
            Note.Visibility = QuickPasteEntry.IsAtLeastOneItemPastable() ? Visibility.Collapsed : Visibility.Visible;
        }


        // Value changed: Notes.
        // - set its corresponding value in the quickPasteItem data structure
        // - update the UI to show the new value
        private void Note_TextChanged(object sender, TextChangedEventArgs args)
        {
            if (sender is TextBox textBox == false)
            {
                TracePrint.NullException(nameof(textBox));
                return;
            }
            QuickPasteItem quickPasteItem = (QuickPasteItem)textBox.Tag;
            quickPasteItem.Value = textBox.Text;
        }


        private void MultiLine_TextHasChanged(object sender, EventArgs e)
        {
            if(sender is MultiLineText multiLine == false)
            {
                TracePrint.NullException(nameof(multiLine));
                return;
            }
            QuickPasteItem quickPasteItem = (QuickPasteItem)multiLine.Tag;
            quickPasteItem.Value = multiLine.Text;
        }
        // Value changed: Counter or Integer. 
        private void CounterOrInteger_TextChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (sender is IntegerUpDown integerUpDown == false)
            {
                TracePrint.NullException(nameof(integerUpDown));
                return;
            }
            QuickPasteItem quickPasteItem = (QuickPasteItem)integerUpDown.Tag;
            quickPasteItem.Value = integerUpDown.Text;
        }

        // Value changed: Decimal. 
        private void Decimal_TextChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (sender is DoubleUpDown doubleUpDown == false)
            {
                TracePrint.NullException(nameof(doubleUpDown));
                return;
            }
            QuickPasteItem quickPasteItem = (QuickPasteItem)doubleUpDown.Tag;
            quickPasteItem.Value = doubleUpDown.Text;
        }

        // Value changed: FixedChoice.
        private void FixedChoice_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            if (sender is ComboBox comboBox == false)
            {
                TracePrint.NullException(nameof(comboBox));
                return;
            }
            QuickPasteItem quickPasteItem = (QuickPasteItem)comboBox.Tag;
            quickPasteItem.Value = comboBox.SelectedValue.ToString();
        }

        // Essentiallly the same as the one in ControlsDataHandlersCommon, except it also modifies the Quickpaste item value
        private void MultiChoice_ItemSelectionChanged(object sender, ItemSelectionChangedEventArgs e)
        {
            if(sender is WatermarkCheckComboBox checkComboBox == false)
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
                QuickPasteItem quickPasteItem = (QuickPasteItem)checkComboBox.Tag;
                quickPasteItem.Value = checkComboBox.Text;
            }
        }
        // Value changed: Flags
        private void Flag_CheckedOrUnchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox == false)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            QuickPasteItem quickPasteItem = (QuickPasteItem)checkBox.Tag;
            quickPasteItem.Value = checkBox.IsChecked.ToString();
        }

        private void WatermarkDateTimePicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (sender is WatermarkDateTimePicker dateTimePicker == false)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            QuickPasteItem quickPasteItem = (QuickPasteItem)dateTimePicker.Tag;
            quickPasteItem.Value = dateTimePicker.Text;
        }

        private void TimePicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (sender is TimePicker timePicker == false)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            QuickPasteItem quickPasteItem = (QuickPasteItem)timePicker.Tag;
            quickPasteItem.Value = timePicker.Text;
        }

        // Set or unset all the checkboxes to use
        private void SetUses_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                bool useState = button.Name == "UseAll";
                foreach (CheckBox cb in UseCheckboxes)
                {
                    cb.IsChecked = useState;
                }
            }
        }
        #endregion

        #region Callbacks - Dialog Buttons
        // Apply the selection if the Ok button is clicked
        private void OkButton_Click(object sender, RoutedEventArgs args)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs args)
        {
            DialogResult = false;
        }
        #endregion

        #region Private Methods - BuildRows
        // Build a row displaying each QuickPaste item
        private void BuildRows()
        {
            // We start after the GridRowInitialRow
            int gridRowIndex = GridRowInitialRow;

            foreach (QuickPasteItem quickPasteItem in QuickPasteEntry.Items)
            {
                ++gridRowIndex;
                RowDefinition gridRow = new()
                {
                    Height = GridLength.Auto,
                };
                QuickPasteGridRows.RowDefinitions.Add(gridRow);
                BuildRow(quickPasteItem, gridRowIndex);
            }
        }

        // Given a quickPasteItem (essential the information representing a single data control and its value),
        // - add a row to the grid with controls that display that information,
        // - add a checkbox that can be selected to indicate whether that information should be included in a paste operation
        private void BuildRow(QuickPasteItem quickPasteItem, int gridRowIndex)
        {
            // USE Column: A checkbox to indicate whether the current search row should be used as part of the search
            Thickness thickness = new(0, 2, 0, 2);
            CheckBox useCurrentRow = new()
            {
                Margin = thickness,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsChecked = quickPasteItem.Use,
                Tag = quickPasteItem
            };
            useCurrentRow.Checked += UseCurrentRow_CheckChanged;
            useCurrentRow.Unchecked += UseCurrentRow_CheckChanged;
            UseCheckboxes.Add(useCurrentRow);

            Grid.SetRow(useCurrentRow, gridRowIndex);
            Grid.SetColumn(useCurrentRow, GridColumnUse);
            QuickPasteGridRows.Children.Add(useCurrentRow);

            // LABEL column: The label associated with the control (Note: not the data label)
            TextBlock controlLabel = new()
            {
                Margin = new(5,6,5,6),
                Text = quickPasteItem.Label,
                Foreground = quickPasteItem.Use ? Brushes.Black : Brushes.Gray,
            };
            Grid.SetRow(controlLabel, gridRowIndex);
            Grid.SetColumn(controlLabel, GridColumnLabel);
            QuickPasteGridRows.Children.Add(controlLabel);

            // VALUES Columns below
            // Note/Alphanumeric Value column:
            // - presented as an editable AutocompleteTextBox field 
            if (quickPasteItem.ControlType == Control.Note ||
                quickPasteItem.ControlType == Control.AlphaNumeric)
            {
                // The controls above use a text field, so they can be constructed as a textbox
                ImprintAutoCompleteTextBox textBoxValue = new()
                {
                    Text = quickPasteItem.Value,
                    Height = ValuesHeight,
                    Width = ValuesWidth,
                    TextWrapping = TextWrapping.NoWrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Top,
                    Padding = new Thickness(0,2,0,0),
                    IsEnabled = quickPasteItem.Use,
                    Tag = quickPasteItem
                };

                if (quickPasteItem.ControlType == Control.Note)
                {
                    // Fill in the autocompletions for Note controls
                    textBoxValue.AddToAutocompletions(dataEntryControls.AutocompletionGetForNote(quickPasteItem.DataLabel));
                }
                else if (quickPasteItem.ControlType == Control.AlphaNumeric)
                {
                    // Fill in the autocompletions for AlphaNumeric controls
                    textBoxValue.AddToAutocompletions(dataEntryControls.AutocompletionGetForNote(quickPasteItem.DataLabel));
                    textBoxValue.PreviewKeyDown += ValidationCallbacks.PreviewKeyDown_TextBoxNoSpaces;
                    textBoxValue.PreviewTextInput += ValidationCallbacks.PreviewInput_AlphaNumericCharacterOnly;
                    textBoxValue.TextChanged += ValidationCallbacks.TextChanged_AlphaNumericTextOnly;
                    DataObject.AddPastingHandler(textBoxValue, ValidationCallbacks.Paste_OnlyIfAlphaNumeric);
                }
                textBoxValue.TextChanged += Note_TextChanged;
                textBoxValue.GotFocus += ControlsDataHelpers.Control_GotFocus;
                textBoxValue.LostFocus += ControlsDataHelpers.Control_LostFocus;
                Grid.SetRow(textBoxValue, gridRowIndex);
                Grid.SetColumn(textBoxValue, GridColumnValue);
                QuickPasteGridRows.Children.Add(textBoxValue);
            }

            // MultiLine Value column:
            // - presented as an editable AutocompleteTextBox field 
            else if (quickPasteItem.ControlType == Control.MultiLine)
            {
                // The control above use a text field, so they can be constructed as a textbox
                MultiLineText textBoxValue = new()
                {
                    Text = quickPasteItem.Value,
                    Height = ValuesHeight,
                    Width = ValuesWidth,
                    TextWrapping = TextWrapping.NoWrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    IsEnabled = quickPasteItem.Use,
                    Tag = quickPasteItem,
                    Style = (Style)dataEntryControls.FindResource("MultiLineTextBox"),
                };
                
                textBoxValue.TextChanged += MultiLine_TextHasChanged;
                Grid.SetRow(textBoxValue, gridRowIndex);
                Grid.SetColumn(textBoxValue, GridColumnValue);
                QuickPasteGridRows.Children.Add(textBoxValue);
            }

            // Counter/IntegerAny Value column
            // - presented as an editable IntegerUpDown field 
            else if (quickPasteItem.ControlType == Control.Counter ||
                     quickPasteItem.ControlType == Control.IntegerAny ||
                     quickPasteItem.ControlType == Control.IntegerPositive)
            {
                // The controls above use an integer field, so they can be constructed as a integerUpDown
                IntegerUpDown textBoxValue = new()
                {
                    Text = quickPasteItem.Value,
                    Height = ValuesHeight,
                    Width = ValuesWidth,
                    VerticalAlignment = VerticalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    IsEnabled = quickPasteItem.Use,
                    Tag = quickPasteItem,
                };
                if (quickPasteItem.ControlType == Control.Counter ||
                    quickPasteItem.ControlType == Control.IntegerPositive)
                {
                    // these controls should not allow negative values
                    textBoxValue.Minimum = 0;
                }
                textBoxValue.PreviewKeyDown += ValidationCallbacks.PreviewKeyDown_IntegerUpDownNoSpaces;
                if (quickPasteItem.ControlType == Control.IntegerAny)
                {
                    textBoxValue.PreviewTextInput += ValidationCallbacks.PreviewInput_IntegerCharacterOnly;
                    DataObject.AddPastingHandler(textBoxValue, ValidationCallbacks.Paste_OnlyIfIntegerAny);
                }
                else
                {
                    textBoxValue.PreviewTextInput += ValidationCallbacks.PreviewInput_IntegerPositiveCharacterOnly;
                    DataObject.AddPastingHandler(textBoxValue, ValidationCallbacks.Paste_OnlyIfIntegerPositive);
                }
                textBoxValue.ValueChanged += CounterOrInteger_TextChanged;
                textBoxValue.GotFocus += ControlsDataHelpers.Control_GotFocus;
                textBoxValue.LostFocus += ControlsDataHelpers.Control_LostFocus;
                Grid.SetRow(textBoxValue, gridRowIndex);
                Grid.SetColumn(textBoxValue, GridColumnValue);
                QuickPasteGridRows.Children.Add(textBoxValue);
            }

            // DecimalAny  DecimalPositive Value column
            // - presented as an editable IntegerUpDown field 
            else if (quickPasteItem.ControlType == Control.DecimalAny ||
                     quickPasteItem.ControlType == Control.DecimalPositive)
            {
                // The controls above use an integer field, so they can be constructed as a integerUpDown
                DoubleUpDown textBoxValue = new()
                {
                    Text = quickPasteItem.Value,
                    Height = ValuesHeight,
                    Width = ValuesWidth,
                    VerticalAlignment = VerticalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    FormatString = ControlDefault.DecimalFormatString,
                    CultureInfo = CultureInfo.InvariantCulture,
                    IsEnabled = quickPasteItem.Use,
                    Tag = quickPasteItem,
                };
                if (quickPasteItem.ControlType == Control.DecimalPositive)
                {
                    // these controls should not allow negative values
                    textBoxValue.Minimum = 0;
                }
                textBoxValue.PreviewKeyDown += ValidationCallbacks.PreviewKeyDown_DecimalUpDownNoSpaces;
                if (quickPasteItem.ControlType == Control.DecimalAny)
                {
                    textBoxValue.PreviewTextInput += ValidationCallbacks.PreviewInput_DecimalCharacterOnly;
                    DataObject.AddPastingHandler(textBoxValue, ValidationCallbacks.Paste_OnlyIfDecimalAny);
                }
                else
                {
                    textBoxValue.PreviewTextInput += ValidationCallbacks.PreviewInput_DecimalPositiveCharacterOnly;
                    DataObject.AddPastingHandler(textBoxValue, ValidationCallbacks.Paste_OnlyIfDecimalPositive);
                }
                textBoxValue.ValueChanged += Decimal_TextChanged;
                textBoxValue.GotFocus += ControlsDataHelpers.Control_GotFocus;
                textBoxValue.LostFocus += ControlsDataHelpers.Control_LostFocus;
                Grid.SetRow(textBoxValue, gridRowIndex);
                Grid.SetColumn(textBoxValue, GridColumnValue);
                QuickPasteGridRows.Children.Add(textBoxValue);
            }

            // Flags Value column
            // - presented as an editable Checkbox field 
            else if (quickPasteItem.ControlType == Control.Flag ||
                     quickPasteItem.ControlType == DatabaseColumn.DeleteFlag)
            {
                // Flags present checkable checkboxes
                CheckBox flagCheckBox = new()
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    IsChecked = !string.IsNullOrEmpty(quickPasteItem.Value) && !string.Equals(quickPasteItem.Value, BooleanValue.False, StringComparison.OrdinalIgnoreCase),
                    IsEnabled = quickPasteItem.Use,
                    Tag = quickPasteItem
                };
                flagCheckBox.Checked += Flag_CheckedOrUnchecked;
                flagCheckBox.Unchecked += Flag_CheckedOrUnchecked;
                Grid.SetRow(flagCheckBox, gridRowIndex);
                Grid.SetColumn(flagCheckBox, GridColumnValue);
                QuickPasteGridRows.Children.Add(flagCheckBox);
            }

            // FixedChoice Value column
            // - presented as an editable ComboBox field 
            else if (quickPasteItem.ControlType == Control.FixedChoice)
            {
                // Choices use choiceboxes
                ControlRow controlRow = fileDatabase.GetControlFromControls(quickPasteItem.DataLabel);
                ComboBox comboBoxValue = new()
                {
                    Height = ValuesHeight,
                    Width = ValuesWidth,
                    SelectedItem = quickPasteItem.Value,
                    IsEnabled = quickPasteItem.Use,
                    Tag = quickPasteItem
                };
                // Populate the combobox menu
                Choices.ChoicesFromJson(controlRow.List).SetComboBoxItems(comboBoxValue);
                comboBoxValue.SelectionChanged += FixedChoice_SelectionChanged;
                comboBoxValue.GotFocus += ControlsDataHelpers.Control_GotFocus;
                comboBoxValue.LostFocus += ControlsDataHelpers.Control_LostFocus;
                Grid.SetRow(comboBoxValue, gridRowIndex);
                Grid.SetColumn(comboBoxValue, GridColumnValue);
                QuickPasteGridRows.Children.Add(comboBoxValue);
            }

            // MultiChoice Value column
            // - presented as an editable checkComboBox field 
            else if (quickPasteItem.ControlType == Control.MultiChoice)
            {
                // Choices use choiceboxes
                ControlRow controlRow = fileDatabase.GetControlFromControls(quickPasteItem.DataLabel);
                WatermarkCheckComboBox checkComboBox = new()
                {
                    Height = ValuesHeight,
                    Width = ValuesWidth,
                    SelectedItem = quickPasteItem.Value,
                    IsEnabled = quickPasteItem.Use,
                    Tag = quickPasteItem
                };
                // Populate the combobox menu
                Choices choices = Choices.ChoicesFromJson(controlRow.List);
                foreach (string choice in choices.ChoiceList)
                {
                    if (string.IsNullOrWhiteSpace(choice))
                    {
                        continue;
                    }
                    checkComboBox.Items.Add(choice);
                }
                checkComboBox.Opened += ControlsDataHelpers.WatermarkCheckComboBox_DropDownOpened;
                checkComboBox.Closed += ControlsDataHelpers.WatermarkCheckComboBox_DropDownClosed;
                checkComboBox.GotFocus += ControlsDataHelpers.Control_GotFocus;
                checkComboBox.LostFocus += ControlsDataHelpers.Control_LostFocus;
                Grid.SetRow(checkComboBox, gridRowIndex);
                Grid.SetColumn(checkComboBox, GridColumnValue);
                QuickPasteGridRows.Children.Add(checkComboBox);
                checkComboBox.ItemSelectionChanged += MultiChoice_ItemSelectionChanged;
                checkComboBox.Text = quickPasteItem.Value;
            }

 
            // DateTime_ Value column
            // - presented as an editable datetime field 
            else if (quickPasteItem.ControlType == Control.DateTime_)
            {
                // DateTime_ present WatermarkDateTimePickers
                WatermarkDateTimePicker dateTimePicker = DateTimeHandler.TryParseDatabaseOrDisplayDateTime(quickPasteItem.Value, out DateTime dateTime)
                    ? CreateControls.CreateWatermarkDateTimePicker(String.Empty, DateTimeFormatEnum.DateAndTime, dateTime)
                    : CreateControls.CreateWatermarkDateTimePicker(String.Empty, DateTimeFormatEnum.DateAndTime, ControlDefault.DateTimeCustomDefaultValue);
                dateTimePicker.Tag = quickPasteItem;
                dateTimePicker.Width = ValuesWidth;
                dateTimePicker.ValueChanged += WatermarkDateTimePicker_ValueChanged;
                Grid.SetRow(dateTimePicker, gridRowIndex);
                Grid.SetColumn(dateTimePicker, GridColumnValue);
                QuickPasteGridRows.Children.Add(dateTimePicker);
            }

            // Date_ Value column
            // - presented as an editable datetime field 
            else if (quickPasteItem.ControlType == Control.Date_)
            {
                // Date_ present WatermarkDateTimePickers

                WatermarkDateTimePicker dateTimePicker = DateTimeHandler.TryParseDatabaseOrDisplayDate(quickPasteItem.Value, out DateTime date)
                    ? CreateControls.CreateWatermarkDateTimePicker(String.Empty, DateTimeFormatEnum.DateOnly, date)
                    : CreateControls.CreateWatermarkDateTimePicker(String.Empty, DateTimeFormatEnum.DateOnly, ControlDefault.Time_DefaultValue);

                dateTimePicker.Tag = quickPasteItem;
                dateTimePicker.Width = ValuesWidth;
                dateTimePicker.ValueChanged += WatermarkDateTimePicker_ValueChanged;
                Grid.SetRow(dateTimePicker, gridRowIndex);
                Grid.SetColumn(dateTimePicker, GridColumnValue);
                QuickPasteGridRows.Children.Add(dateTimePicker);
            }
            else if (quickPasteItem.ControlType == Control.Time_)
            {
                // Time_ presents TimePickers

                TimePicker timePicker = DateTimeHandler.TryParseDatabaseTime(quickPasteItem.Value, out DateTime time)
                    ? CreateControls.CreateWatermarkTimePicker(String.Empty, time)
                    : CreateControls.CreateWatermarkTimePicker(String.Empty, ControlDefault.Time_DefaultValue);

                timePicker.Tag = quickPasteItem;
                timePicker.Width = ValuesWidth;
                timePicker.ValueChanged += TimePicker_ValueChanged;
                Grid.SetRow(timePicker, gridRowIndex);
                Grid.SetColumn(timePicker, GridColumnValue);
                QuickPasteGridRows.Children.Add(timePicker);
            }
            else
            {
                // We should never get here
                throw new NotSupportedException(
                    $"Unhandled control type in QuickPasteEditor '{quickPasteItem.ControlType}'.");
            }
            Note.Visibility = QuickPasteEntry.IsAtLeastOneItemPastable() ? Visibility.Collapsed : Visibility.Visible;
        }

        #endregion

        #region Private  Helper functions
        // Get the corresponding grid element from a given column, row, 
        private TElement GetGridElement<TElement>(int column, int row) where TElement : UIElement
        {
            return (TElement)QuickPasteGridRows.Children.Cast<UIElement>().First(control => Grid.GetRow(control) == row && Grid.GetColumn(control) == column);
        }
        #endregion
    }
}