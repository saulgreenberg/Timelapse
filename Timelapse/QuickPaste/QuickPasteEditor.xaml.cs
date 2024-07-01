using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Timelapse.Controls;
using Timelapse.ControlsDataCommon;
using Timelapse.ControlsDataEntry;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
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

        #region Privaate Variables
        // Columns where fields will be placed in the grid
        private const int GridColumnUse = 1;
        private const int GridColumnLabel = 2;
        private const int GridColumnValue = 3;

        // The initial grid row. We start adding rows after this one.
        // the 1st two grid rows are already filled.
        private const int GridRowInitialRow = 1;

        // For simplicity, create a list with all 'Use' checkboxes in it so we can set or clear all of them
        readonly List<CheckBox> UseCheckboxes = new List<CheckBox>();

        // UI Constants
        private const double ValuesWidth = 160;
        private const double ValuesHeight = 22;
        private readonly FileDatabase fileDatabase;
        private readonly DataEntryControls dataEntryControls;
        #endregion

        #region Constructor, Loaded
        public QuickPasteEditor(QuickPasteEntry quickPasteEntry, FileDatabase fileDatabase, DataEntryControls dataEntryControls)
        {
            this.InitializeComponent();
            this.QuickPasteEntry = quickPasteEntry;
            this.fileDatabase = fileDatabase;
            this.dataEntryControls = dataEntryControls;
        }

        // When the window is loaded
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Adjust this dialog window position 
            Dialogs.TryPositionAndFitDialogIntoWindow(this);

            // Display the title of the QuickPasteEntry
            this.QuickPasteTitle.Text = this.QuickPasteEntry.Title;
            this.QuickPasteTitle.TextChanged += this.QuickPasteTitle_TextChanged;

            // Build the grid rows, each displaying successive items in the QuickPasteItems list
            this.BuildRows();
        }
        #endregion

        #region Callbacks - UI 
        // Title: Update the QuickPasteEntry's title
        private void QuickPasteTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.QuickPasteEntry.Title = this.QuickPasteTitle.Text;
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

            TextBlock label = this.GetGridElement<TextBlock>(GridColumnLabel, row);
            UIElement value = this.GetGridElement<UIElement>(GridColumnValue, row);
            if (cbox.IsChecked != null && cbox.IsChecked == true)
            {
                label.Foreground = Brushes.Black;
                value.IsEnabled = cbox.IsChecked.Value;
            }
            else
            {
                label.Foreground = Brushes.Gray;
                value.IsEnabled = cbox.IsChecked ?? false;
            }

            // Update the QuickPaste row data structure to reflect the current checkbox state
            QuickPasteItem quickPasteRow = (QuickPasteItem)cbox.Tag;
            quickPasteRow.Use = cbox.IsChecked == true;
            this.Note.Visibility = this.QuickPasteEntry.IsAtLeastOneItemPastable() ? Visibility.Collapsed : Visibility.Visible;
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
            if(sender is MultiLineTextEditor multiLine == false)
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
        private void MultiChoice_ItemSelectionChanged(object sender, Xceed.Wpf.Toolkit.Primitives.ItemSelectionChangedEventArgs e)
        {
            if(sender is CheckComboBox checkComboBox == false)
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

        private void DateTimePicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (sender is DateTimePicker dateTimePicker == false)
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
                foreach (CheckBox cb in this.UseCheckboxes)
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
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs args)
        {
            this.DialogResult = false;
        }
        #endregion

        #region Private Methods - BuildRows
        // Build a row displaying each QuickPaste item
        private void BuildRows()
        {
            // We start after the GridRowInitialRow
            int gridRowIndex = GridRowInitialRow;

            foreach (QuickPasteItem quickPasteItem in this.QuickPasteEntry.Items)
            {
                ++gridRowIndex;
                RowDefinition gridRow = new RowDefinition()
                {
                    Height = GridLength.Auto
                };
                this.QuickPasteGridRows.RowDefinitions.Add(gridRow);
                this.BuildRow(quickPasteItem, gridRowIndex);
            }
        }

        // Given a quickPasteItem (essential the information representing a single data control and its value),
        // - add a row to the grid with controls that display that information,
        // - add a checkbox that can be selected to indicate whether that information should be included in a paste operation
        private void BuildRow(QuickPasteItem quickPasteItem, int gridRowIndex)
        {
            // USE Column: A checkbox to indicate whether the current search row should be used as part of the search
            Thickness thickness = new Thickness(0, 2, 0, 2);
            CheckBox useCurrentRow = new CheckBox()
            {
                Margin = thickness,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsChecked = quickPasteItem.Use,
                Tag = quickPasteItem
            };
            useCurrentRow.Checked += this.UseCurrentRow_CheckChanged;
            useCurrentRow.Unchecked += this.UseCurrentRow_CheckChanged;
            this.UseCheckboxes.Add(useCurrentRow);

            Grid.SetRow(useCurrentRow, gridRowIndex);
            Grid.SetColumn(useCurrentRow, GridColumnUse);
            this.QuickPasteGridRows.Children.Add(useCurrentRow);

            // LABEL column: The label associated with the control (Note: not the data label)
            TextBlock controlLabel = new TextBlock()
            {
                Margin = new Thickness(5),
                Text = quickPasteItem.Label,
                Foreground = quickPasteItem.Use ? Brushes.Black : Brushes.Gray,
            };
            Grid.SetRow(controlLabel, gridRowIndex);
            Grid.SetColumn(controlLabel, GridColumnLabel);
            this.QuickPasteGridRows.Children.Add(controlLabel);

            // VALUES Columns below
            // Note/Alphanumeric Value column:
            // - presented as an editable AutocompleteTextBox field 
            if (quickPasteItem.ControlType == Constant.Control.Note ||
                quickPasteItem.ControlType == Constant.Control.AlphaNumeric)
            {
                // The controls above use a text field, so they can be constructed as a textbox
                AutocompleteTextBox textBoxValue = new AutocompleteTextBox()
                {
                    Autocompletions = null,
                    Text = quickPasteItem.Value,
                    Height = ValuesHeight,
                    Width = ValuesWidth,
                    TextWrapping = TextWrapping.NoWrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    IsEnabled = quickPasteItem.Use,
                    Tag = quickPasteItem
                };

                if (quickPasteItem.ControlType == Constant.Control.Note)
                {
                    textBoxValue.Autocompletions = this.dataEntryControls.AutocompletionGetForNote(quickPasteItem.DataLabel);
                }
                else if (quickPasteItem.ControlType == Constant.Control.AlphaNumeric)
                {
                    textBoxValue.Autocompletions = this.dataEntryControls.AutocompletionGetForNote(quickPasteItem.DataLabel);
                    textBoxValue.PreviewKeyDown += Util.ValidationCallbacks.PreviewKeyDown_TextBoxNoSpaces;
                    textBoxValue.PreviewTextInput += Util.ValidationCallbacks.PreviewInput_AlphaNumericCharacterOnly;
                    textBoxValue.TextChanged += Util.ValidationCallbacks.TextChanged_AlphaNumericTextOnly;
                    DataObject.AddPastingHandler(textBoxValue, Timelapse.Util.ValidationCallbacks.Paste_OnlyIfAlphaNumeric);
                }
                textBoxValue.TextChanged += this.Note_TextChanged;
                textBoxValue.GotFocus += ControlsDataHelpersCommon.Control_GotFocus;
                textBoxValue.LostFocus += ControlsDataHelpersCommon.Control_LostFocus;
                Grid.SetRow(textBoxValue, gridRowIndex);
                Grid.SetColumn(textBoxValue, GridColumnValue);
                this.QuickPasteGridRows.Children.Add(textBoxValue);
            }

            // MultiLine Value column:
            // - presented as an editable AutocompleteTextBox field 
            else if (quickPasteItem.ControlType == Constant.Control.MultiLine)
            {
                // The control above use a text field, so they can be constructed as a textbox
                MultiLineTextEditor textBoxValue = new MultiLineTextEditor()
                {
                    Text = quickPasteItem.Value,
                    Height = ValuesHeight,
                    Width = ValuesWidth,
                    TextWrapping = TextWrapping.NoWrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    IsEnabled = quickPasteItem.Use,
                    Tag = quickPasteItem,
                    Style = (Style)this.dataEntryControls.FindResource("MultiLineBox"),
                };
                textBoxValue.TextHasChanged += MultiLine_TextHasChanged;
                Grid.SetRow(textBoxValue, gridRowIndex);
                Grid.SetColumn(textBoxValue, GridColumnValue);
                this.QuickPasteGridRows.Children.Add(textBoxValue);
            }

            // Counter/IntegerAny Value column
            // - presented as an editable IntegerUpDown field 
            else if (quickPasteItem.ControlType == Constant.Control.Counter ||
                     quickPasteItem.ControlType == Constant.Control.IntegerAny ||
                     quickPasteItem.ControlType == Constant.Control.IntegerPositive)
            {
                // The controls above use an integer field, so they can be constructed as a integerUpDown
                IntegerUpDown textBoxValue = new IntegerUpDown()
                {
                    Text = quickPasteItem.Value,
                    Height = ValuesHeight,
                    Width = ValuesWidth,
                    VerticalAlignment = VerticalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    IsEnabled = quickPasteItem.Use,
                    Tag = quickPasteItem,
                };
                if (quickPasteItem.ControlType == Constant.Control.Counter ||
                    quickPasteItem.ControlType == Constant.Control.IntegerPositive)
                {
                    // these controls should not allow negative values
                    textBoxValue.Minimum = 0;
                }
                textBoxValue.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_IntegerUpDownNoSpaces;
                if (quickPasteItem.ControlType == Constant.Control.IntegerAny)
                {
                    textBoxValue.PreviewTextInput += Timelapse.Util.ValidationCallbacks.PreviewInput_IntegerCharacterOnly;
                    DataObject.AddPastingHandler(textBoxValue, Timelapse.Util.ValidationCallbacks.Paste_OnlyIfIntegerAny);
                }
                else
                {
                    textBoxValue.PreviewTextInput += Timelapse.Util.ValidationCallbacks.PreviewInput_IntegerPositiveCharacterOnly;
                    DataObject.AddPastingHandler(textBoxValue, Timelapse.Util.ValidationCallbacks.Paste_OnlyIfIntegerPositive);
                }
                textBoxValue.ValueChanged += this.CounterOrInteger_TextChanged;
                textBoxValue.GotFocus += ControlsDataHelpersCommon.Control_GotFocus;
                textBoxValue.LostFocus += ControlsDataHelpersCommon.Control_LostFocus;
                Grid.SetRow(textBoxValue, gridRowIndex);
                Grid.SetColumn(textBoxValue, GridColumnValue);
                this.QuickPasteGridRows.Children.Add(textBoxValue);
            }

            // DecimalAny  DecimalPositive Value column
            // - presented as an editable IntegerUpDown field 
            else if (quickPasteItem.ControlType == Constant.Control.DecimalAny ||
                     quickPasteItem.ControlType == Constant.Control.DecimalPositive)
            {
                // The controls above use an integer field, so they can be constructed as a integerUpDown
                DoubleUpDown textBoxValue = new DoubleUpDown()
                {
                    Text = quickPasteItem.Value,
                    Height = ValuesHeight,
                    Width = ValuesWidth,
                    VerticalAlignment = VerticalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    IsEnabled = quickPasteItem.Use,
                    Tag = quickPasteItem,
                };
                if (quickPasteItem.ControlType == Constant.Control.DecimalPositive)
                {
                    // these controls should not allow negative values
                    textBoxValue.Minimum = 0;
                }
                textBoxValue.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_DecimalUpDownNoSpaces;
                if (quickPasteItem.ControlType == Constant.Control.DecimalAny)
                {
                    textBoxValue.PreviewTextInput += Timelapse.Util.ValidationCallbacks.PreviewInput_DecimalCharacterOnly;
                    DataObject.AddPastingHandler(textBoxValue, Timelapse.Util.ValidationCallbacks.Paste_OnlyIfDecimalAny);
                }
                else
                {
                    textBoxValue.PreviewTextInput += Timelapse.Util.ValidationCallbacks.PreviewInput_DecimalPositiveCharacterOnly;
                    DataObject.AddPastingHandler(textBoxValue, Timelapse.Util.ValidationCallbacks.Paste_OnlyIfDecimalPositive);
                }
                textBoxValue.ValueChanged += this.Decimal_TextChanged;
                textBoxValue.GotFocus += ControlsDataHelpersCommon.Control_GotFocus;
                textBoxValue.LostFocus += ControlsDataHelpersCommon.Control_LostFocus;
                Grid.SetRow(textBoxValue, gridRowIndex);
                Grid.SetColumn(textBoxValue, GridColumnValue);
                this.QuickPasteGridRows.Children.Add(textBoxValue);
            }

            // Flags Value column
            // - presented as an editable Checkbox field 
            else if (quickPasteItem.ControlType == Constant.Control.Flag)
            {
                // Flags present checkable checkboxes
                CheckBox flagCheckBox = new CheckBox()
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    IsChecked = !string.IsNullOrEmpty(quickPasteItem.Value) && !string.Equals(quickPasteItem.Value, Constant.BooleanValue.False, StringComparison.OrdinalIgnoreCase),
                    IsEnabled = quickPasteItem.Use,
                    Tag = quickPasteItem
                };
                flagCheckBox.Checked += this.Flag_CheckedOrUnchecked;
                flagCheckBox.Unchecked += this.Flag_CheckedOrUnchecked;
                Grid.SetRow(flagCheckBox, gridRowIndex);
                Grid.SetColumn(flagCheckBox, GridColumnValue);
                this.QuickPasteGridRows.Children.Add(flagCheckBox);
            }

            // FixedChoice Value column
            // - presented as an editable ComboBox field 
            else if (quickPasteItem.ControlType == Constant.Control.FixedChoice)
            {
                // Choices use choiceboxes
                ControlRow controlRow = this.fileDatabase.GetControlFromControls(quickPasteItem.DataLabel);
                ComboBox comboBoxValue = new ComboBox()
                {
                    Height = ValuesHeight,
                    Width = ValuesWidth,
                    SelectedItem = quickPasteItem.Value,
                    IsEnabled = quickPasteItem.Use,
                    Tag = quickPasteItem
                };
                // Populate the combobox menu
                Choices.ChoicesFromJson(controlRow.List).SetComboBoxItems(comboBoxValue);
                comboBoxValue.SelectionChanged += this.FixedChoice_SelectionChanged;
                comboBoxValue.GotFocus += ControlsDataHelpersCommon.Control_GotFocus;
                comboBoxValue.LostFocus += ControlsDataHelpersCommon.Control_LostFocus;
                Grid.SetRow(comboBoxValue, gridRowIndex);
                Grid.SetColumn(comboBoxValue, GridColumnValue);
                this.QuickPasteGridRows.Children.Add(comboBoxValue);
            }

            // MultiChoice Value column
            // - presented as an editable checkComboBox field 
            else if (quickPasteItem.ControlType == Constant.Control.MultiChoice)
            {
                // Choices use choiceboxes
                ControlRow controlRow = this.fileDatabase.GetControlFromControls(quickPasteItem.DataLabel);
                CheckComboBox checkComboBox = new CheckComboBox()
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
                checkComboBox.Opened += ControlsDataHelpersCommon.CheckComboBox_DropDownOpened;
                checkComboBox.Closed += ControlsDataHelpersCommon.CheckComboBox_DropDownClosed;
                checkComboBox.GotFocus += ControlsDataHelpersCommon.Control_GotFocus;
                checkComboBox.LostFocus += ControlsDataHelpersCommon.Control_LostFocus;
                Grid.SetRow(checkComboBox, gridRowIndex);
                Grid.SetColumn(checkComboBox, GridColumnValue);
                this.QuickPasteGridRows.Children.Add(checkComboBox);
                checkComboBox.ItemSelectionChanged += MultiChoice_ItemSelectionChanged;
                checkComboBox.Text = quickPasteItem.Value;
            }

 
            // DateTime_ Value column
            // - presented as an editable datetime field 
            else if (quickPasteItem.ControlType == Constant.Control.DateTime_)
            {
                // DateTime_ present DateTimePickers
                DateTimePicker dateTimePicker = DateTimeHandler.TryParseDisplayDateTime(quickPasteItem.Value, out DateTime dateTime)
                    ? ControlsDataCommon.CreateControls.CreateDateTimePicker(String.Empty, DateTimeFormatEnum.DateAndTime, dateTime)
                    : ControlsDataCommon.CreateControls.CreateDateTimePicker(String.Empty, DateTimeFormatEnum.DateAndTime, Constant.ControlDefault.DateTimeCustomDefaultValue);
                dateTimePicker.Tag = quickPasteItem;
                dateTimePicker.Width = ValuesWidth;
                dateTimePicker.ValueChanged += DateTimePicker_ValueChanged;
                Grid.SetRow(dateTimePicker, gridRowIndex);
                Grid.SetColumn(dateTimePicker, GridColumnValue);
                this.QuickPasteGridRows.Children.Add(dateTimePicker);
            }

            // Date_ Value column
            // - presented as an editable datetime field 
            else if (quickPasteItem.ControlType == Constant.Control.Date_)
            {
                // Date_ present DateTimePickers

                DateTimePicker dateTimePicker = DateTimeHandler.TryParseDisplayDate(quickPasteItem.Value, out DateTime date)
                    ? ControlsDataCommon.CreateControls.CreateDateTimePicker(String.Empty, DateTimeFormatEnum.DateOnly, date)
                    : ControlsDataCommon.CreateControls.CreateDateTimePicker(String.Empty, DateTimeFormatEnum.DateOnly, Constant.ControlDefault.Time_DefaultValue);

                dateTimePicker.Tag = quickPasteItem;
                dateTimePicker.Width = ValuesWidth;
                dateTimePicker.ValueChanged += DateTimePicker_ValueChanged;
                Grid.SetRow(dateTimePicker, gridRowIndex);
                Grid.SetColumn(dateTimePicker, GridColumnValue);
                this.QuickPasteGridRows.Children.Add(dateTimePicker);
            }
            else if (quickPasteItem.ControlType == Constant.Control.Time_)
            {
                // Time_ presents TimePickers

                TimePicker timePicker = DateTimeHandler.TryParseDatabaseTime(quickPasteItem.Value, out DateTime time)
                    ? ControlsDataCommon.CreateControls.CreateTimePicker(String.Empty, time)
                    : ControlsDataCommon.CreateControls.CreateTimePicker(String.Empty, Constant.ControlDefault.Time_DefaultValue);

                timePicker.Tag = quickPasteItem;
                timePicker.Width = ValuesWidth;
                timePicker.ValueChanged += TimePicker_ValueChanged;
                Grid.SetRow(timePicker, gridRowIndex);
                Grid.SetColumn(timePicker, GridColumnValue);
                this.QuickPasteGridRows.Children.Add(timePicker);
            }
            else
            {
                // We should never get here
                throw new NotSupportedException(
                    $"Unhandled control type in QuickPasteEditor '{quickPasteItem.ControlType}'.");
            }
            this.Note.Visibility = this.QuickPasteEntry.IsAtLeastOneItemPastable() ? Visibility.Collapsed : Visibility.Visible;
        }

        #endregion

        #region Private  Helper functions
        // Get the corresponding grid element from a given column, row, 
        private TElement GetGridElement<TElement>(int column, int row) where TElement : UIElement
        {
            return (TElement)this.QuickPasteGridRows.Children.Cast<UIElement>().First(control => Grid.GetRow(control) == row && Grid.GetColumn(control) == column);
        }
        #endregion
    }
}