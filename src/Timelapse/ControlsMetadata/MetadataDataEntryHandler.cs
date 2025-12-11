using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Util;
using TimelapseWpf.Toolkit;
using TimelapseWpf.Toolkit.Primitives;
using Control = Timelapse.Constant.Control;

namespace Timelapse.ControlsMetadata
{
    public class MetadataDataEntryHandler(FileDatabase fileDatabase) : IDisposable
    {
        #region Public Properties and Private variables
        public FileDatabase FileDatabase { get; } = fileDatabase; // We need a reference to the database if we are going to update it.
        public bool IsProgrammaticControlUpdate { get; set; } = false;

        // Index location of these menu items in the context menu
        private const int CopyToClipboardIndex = 0;
        private const int PasteFromClipboardIndex = 1;
        private bool disposed;
        #endregion

        #region Loading

        //this.ImageCache = new ImageCache(fileDatabase);

        #endregion

        #region Configuration, including Callback Configuration

        /// <summary>
        /// Add data event handler callbacks for (possibly invisible) controls
        /// </summary>
        public void SetDataEntryCallbacks(Dictionary<string, MetadataDataEntryControl> controlsByDataLabel)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(controlsByDataLabel, nameof(controlsByDataLabel));

            // Add data entry callbacks to all editable controls. When the user changes a file's attribute using a particular control,
            // the callback updates the matching field for that file in the database.
            foreach (KeyValuePair<string, MetadataDataEntryControl> pair in controlsByDataLabel)
            {
                string controlType = pair.Value.ControlType;
                switch (controlType)
                {
                    case Control.Note:
                        MetadataDataEntryNote note = (MetadataDataEntryNote)pair.Value;
                        note.ContentControl.LostKeyboardFocus += NoteControl_LostKeyboardFocus;
                        SetContextMenuCallbacks(note);
                        break;

                    case Control.MultiLine:
                        MetadataDataEntryMultiLine multiLine = (MetadataDataEntryMultiLine)pair.Value;
                        multiLine.ContentControl.TextChanged += MultiLineControl_TextHasChanged;
                        SetContextMenuCallbacks(multiLine);
                        break;

                    case Control.AlphaNumeric:
                        MetadataDataEntryAlphaNumeric alphaNumeric = (MetadataDataEntryAlphaNumeric)pair.Value;
                        alphaNumeric.ContentControl.LostKeyboardFocus += AlphaNumericControl_LostKeyboardFocus;
                        SetContextMenuCallbacks(alphaNumeric);
                        break;

                    case Control.IntegerAny:
                        MetadataDataEntryIntegerAny integerAny = (MetadataDataEntryIntegerAny)pair.Value;
                        integerAny.ContentControl.LostKeyboardFocus += IntegerAnyControl_LostKeyboardFocus;
                        SetContextMenuCallbacks(integerAny);
                        break;

                    case Control.IntegerPositive:
                        MetadataDataEntryIntegerPositive integerPositive = (MetadataDataEntryIntegerPositive)pair.Value;
                        integerPositive.ContentControl.LostKeyboardFocus += IntegerPositiveControl_LostKeyboardFocus;
                        SetContextMenuCallbacks(integerPositive);
                        break;

                    case Control.DecimalAny:
                        MetadataDataEntryDecimalAny decimalAny = (MetadataDataEntryDecimalAny)pair.Value;
                        decimalAny.ContentControl.LostKeyboardFocus += DecimalAnyControl_LostKeyboardFocus;
                        SetContextMenuCallbacks(decimalAny);
                        break;

                    case Control.DecimalPositive:
                        MetadataDataEntryDecimalPositive decimalPositive = (MetadataDataEntryDecimalPositive)pair.Value;
                        decimalPositive.ContentControl.LostKeyboardFocus += DecimalPositiveControl_LostKeyboardFocus;
                        SetContextMenuCallbacks(decimalPositive);
                        break;

                    case Control.FixedChoice:
                        MetadataDataEntryFixedChoice choice = (MetadataDataEntryFixedChoice)pair.Value;
                        choice.ContentControl.SelectionChanged += FixedChoiceControl_SelectionChanged;
                        SetContextMenuCallbacks(choice);
                        break;

                    case Control.MultiChoice:
                        MetadataDataEntryMultiChoice multiChoice = (MetadataDataEntryMultiChoice)pair.Value;
                        multiChoice.ContentControl.ItemSelectionChanged += MultiChoice_ItemSelectionChanged;
                        SetContextMenuCallbacks(multiChoice);
                        break;

                    case Control.DateTime_:
                        MetadataDataEntryDateTimeCustom dateTimeCustom = (MetadataDataEntryDateTimeCustom)pair.Value;
                        dateTimeCustom.ContentControl.ValueChanged += DateTimeCustom_ValueChanged;
                        SetContextMenuCallbacks(dateTimeCustom);
                        break;

                    case Control.Date_:
                        MetadataDataEntryDate date = (MetadataDataEntryDate)pair.Value;
                        date.ContentControl.ValueChanged += Date_ValueChanged;
                        SetContextMenuCallbacks(date);
                        break;

                    case Control.Time_:
                        MetadataDataEntryTime time = (MetadataDataEntryTime)pair.Value;
                        time.ContentControl.ValueChanged += Time_ValueChanged;
                        SetContextMenuCallbacks(time);
                        break;

                    case Control.Flag:
                        MetadataDataEntryFlag flag = (MetadataDataEntryFlag)pair.Value;
                        flag.ContentControl.Checked += FlagControl_CheckedChanged;
                        flag.ContentControl.Unchecked += FlagControl_CheckedChanged;
                        SetContextMenuCallbacks(flag);
                        break;
                }
            }
        }
        #endregion

        #region SetContextMenuCallbacks
        // Create the Context menu, including settings its callbakcs
        private void SetContextMenuCallbacks(MetadataDataEntryControl control)
        {
            if (GlobalReferences.TimelapseState.IsViewOnly)
            {
                // In view-only mode, we don't create these menus as they allow editing
                return;
            }

            // Start with an empty clipboard
            // Its in a try / catch loop as a known issue is that it may sometimes fail due
            // to another process briefly having the clipboard
            try
            {
                Clipboard.Clear();
            }
            catch
            {
                // Debug.Print("Ignorable error in Clipboard.SetText (see MetadataDataEntryHandler:SetContextMenuCallbacks in ");
            }

            MenuItem menuItemCopy = new()
            {
                IsCheckable = false,
                Header = "Copy",
                ToolTip = "Copy will copy this field's entire content to the clipboard",
                Tag = control
            };
            menuItemCopy.Click += MenuItemCopyToClipboard_Click;

            MenuItem menuItemPaste = new()
            {
                IsCheckable = false,
                Header = "Paste",
                ToolTip = "Paste will replace this field's content with the clipboard's content",
                Tag = control
            };
            menuItemPaste.Click += MenuItemPasteFromClipboard_Click;

            // MetadataDataEntrHandler.PropagateFromLastValueIndex and CopyForwardIndex must be kept in sync with the add order here
            // Add the context menu to the control
            ContextMenu menu = new();
            menu.Items.Add(menuItemCopy);
            menu.Items.Add(menuItemPaste);
            control.Container.ContextMenu = menu;
            control.Container.PreviewMouseRightButtonDown += Container_PreviewMouseRightButtonDown;

            if (control is MetadataDataEntryAlphaNumeric alphaNumeric)
            {
                alphaNumeric.ContentControl.ContextMenu = menu;
            }
            else if (control is MetadataDataEntryNote note)
            {
                note.ContentControl.ContextMenu = menu;
            }
            else if (control is MetadataDataEntryMultiLine multiLine)
            {
                multiLine.ContentControl.ContextMenu = menu;
            }
            else if (control is MetadataDataEntryIntegerPositive integerPositive)
            {
                integerPositive.ContentControl.ContextMenu = menu;
            }
            else if (control is MetadataDataEntryIntegerAny integerAny)
            {
                integerAny.ContentControl.ContextMenu = menu;
            }
            else if (control is MetadataDataEntryDecimalPositive decimalPositive)
            {
                decimalPositive.ContentControl.ContextMenu = menu;
            }
            else if (control is MetadataDataEntryDecimalAny decimalAny)
            {
                decimalAny.ContentControl.ContextMenu = menu;
            }
            else if (control is MetadataDataEntryFixedChoice fixedChoice)
            {
                fixedChoice.ContentControl.ContextMenu = menu;
            }
            else if (control is MetadataDataEntryMultiChoice multiChoice)
            {
                multiChoice.ContentControl.ContextMenu = menu;
            }
            else if (control is MetadataDataEntryDateTimeCustom dateTimeCustom)
            {
                dateTimeCustom.ContentControl.ContextMenu = menu;
                // We also need to set the dateTimePicker's TextBox as otherwise the standard copy/paste will be displayed
                TextBox tb = VisualChildren.GetVisualChild<TextBox>(dateTimeCustom.ContentControl);
                if (tb != null)
                {
                    tb.ContextMenu = menu;
                }
            }
            else if (control is MetadataDataEntryDate date)
            {
                date.ContentControl.ContextMenu = menu;
                // We also need to set the dateTimePicker's TextBox as otherwise the standard copy/paste will be displayed
                TextBox tb = VisualChildren.GetVisualChild<TextBox>(date.ContentControl);
                if (tb != null)
                {
                    tb.ContextMenu = menu;
                }
            }
            else if (control is MetadataDataEntryTime time)
            {
                time.ContentControl.ContextMenu = menu;
                // We also need to set the dateTimePicker's TextBox as otherwise the standard copy/paste will be displayed
                TextBox tb = VisualChildren.GetVisualChild<TextBox>(time.ContentControl);
                if (tb != null)
                {
                    tb.ContextMenu = menu;
                }
            }
            else if (control is MetadataDataEntryFlag flag)
            {
                flag.ContentControl.ContextMenu = menu;
            }

            else
            {
                throw new NotSupportedException($"Unhandled control type {control.GetType().Name}.");
            }
        }
        #endregion

        #region Context menu event handlers

        // Copy the  value of the current control to the clipboard
        protected virtual void MenuItemCopyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(sender, nameof(sender));

            // Get the chosen data entry control
            MetadataDataEntryControl control = (MetadataDataEntryControl)((MenuItem)sender).Tag;
            if (control == null)
            {
                return;
            }

            // Its in a try / catch as one user reported an unusual error: OpenClipboardFailed
            try
            {
                Clipboard.SetText(control.Content);
            }
            catch
            {
                Debug.Print("Error in setting text in clipboard (see MenuItemCopyToClipboard_Click in MetadataDataEntryHandler");
            }
        }

        // Paste the contents of the clipboard into the current or selected controls
        // Note that we don't do any checks against the control's type, as that would be handled by the menu enablement
        protected virtual void MenuItemPasteFromClipboard_Click(object sender, RoutedEventArgs e)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(sender, nameof(sender));

            // Get the chosen data entry control
            MetadataDataEntryControl control = (MetadataDataEntryControl)((MenuItem)sender).Tag;
            if (control == null)
            {
                return;
            }
            string newContent = Clipboard.GetText().Trim();
            control.SetContentAndTooltip(newContent);
            UpdateMetadataTableAndMetadataDatabase(control);
        }

        // Enable or disable particular context menu items
        protected virtual void Container_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(sender, nameof(sender));

            StackPanel stackPanel = (StackPanel)sender;
            MetadataDataEntryControl control = (MetadataDataEntryControl)stackPanel.Tag;

            if (stackPanel.ContextMenu == null)
            {
                TracePrint.NullException(nameof(stackPanel));
                return;
            }
            MenuItem menuItemCopyToClipboard = (MenuItem)stackPanel.ContextMenu.Items[CopyToClipboardIndex];
            MenuItem menuItemPasteFromClipboard = (MenuItem)stackPanel.ContextMenu.Items[PasteFromClipboardIndex];

            // Behaviour: 
            // - if the ThumbnailInCell is visible, disable Copy to all / Copy forward / Propagate if a single item isn't selected
            // - otherwise enable the menu item only if the resulting action is coherent
            // bool enabledIsPossible = this.ThumbnailGrid.IsVisible == false || this.ThumbnailGrid.SelectedCount() == 1;

            // Enable Copy menu if
            // - its not empty / white space and not in the overview with different contents (i.e., ellipsis is showing)
            if (null != menuItemCopyToClipboard)
            {
                menuItemCopyToClipboard.IsEnabled = !(string.IsNullOrWhiteSpace(control.Content) || control.Content == Unicode.Ellipsis);
            }

            // Enable Paste menu only if
            // - the clipboard is not empty or white space, 
            // - the string matches the contents expected by the control's type
            // - we are not in the overview with different contents selected (i.e., ellipsis is showing)
            // Its in a try / catch as one user reported an unusual error: OpenClipboardFailed
            string clipboardText;
            try
            {
                clipboardText = Clipboard.GetText().Trim();
            }
            catch
            {
                clipboardText = string.Empty;
                Debug.Print("Error in setting text in clipboard (see Container_PreviewMouseRightButtonDown in MetadataDataEntryHandler");
            }
            if (string.IsNullOrEmpty(clipboardText) && null != menuItemPasteFromClipboard)
            {
                menuItemPasteFromClipboard.IsEnabled = false;
            }
            else
            {
                if (control is MetadataDataEntryAlphaNumeric)
                {
                    // Only alphanumeric characters are valid
                    menuItemPasteFromClipboard!.IsEnabled = IsCondition.IsAlphaNumeric(clipboardText);
                }

                else if (control is MetadataDataEntryNote || control is MetadataDataEntryMultiLine)
                {
                    // Any string is valid
                    menuItemPasteFromClipboard!.IsEnabled = true;
                }
                else if (control is MetadataDataEntryFlag)
                {
                    // Only true / false is valid
                    menuItemPasteFromClipboard!.IsEnabled = (clipboardText == "true" || clipboardText == "false");
                }
                else if (control is MetadataDataEntryIntegerAny)
                {
                    // Only an integer is valid
                    menuItemPasteFromClipboard!.IsEnabled = Int32.TryParse(clipboardText, out _);
                    clipboardText = clipboardText.Trim('0');
                    if (string.IsNullOrEmpty(clipboardText))
                    {
                        clipboardText = "0";
                    }
                }
                else if (control is MetadataDataEntryIntegerPositive)
                {
                    // Only a positive integer is valid
                    menuItemPasteFromClipboard!.IsEnabled = Int32.TryParse(clipboardText, out int x) && x >= 0;
                    clipboardText = clipboardText.Trim('0');
                    if (string.IsNullOrEmpty(clipboardText))
                    {
                        clipboardText = "0";
                    }
                }

                else if (control is MetadataDataEntryDecimalAny)
                {
                    // Only a real is valid
                    menuItemPasteFromClipboard!.IsEnabled = Double.TryParse(clipboardText, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                    clipboardText = clipboardText.Trim('0');
                    if (string.IsNullOrEmpty(clipboardText) || clipboardText == ".")
                    {
                        clipboardText = "0";
                    }
                }
                else if (control is MetadataDataEntryDecimalPositive)
                {
                    // Only a positive real is valid
                    menuItemPasteFromClipboard!.IsEnabled = Double.TryParse(clipboardText, NumberStyles.Float, CultureInfo.InvariantCulture, out double x) && x >= 0;
                    clipboardText = clipboardText.Trim('0');
                    if (string.IsNullOrEmpty(clipboardText) || clipboardText == ".")
                    {
                        clipboardText = "0";
                    }
                }

                else if (control is MetadataDataEntryFixedChoice fixedChoice)
                {
                    // Only a value present as a menu choice is valid 
                    menuItemPasteFromClipboard!.IsEnabled = false;
                    ComboBox comboBox = fixedChoice.ContentControl;
                    foreach (Object t in comboBox.Items)
                    {
                        // This check skips over the Separator
                        if (t is ComboBoxItem cbi)
                        {
                            if (clipboardText == ((string)cbi.Content).Trim())
                            {
                                // We found a matching value, so pasting is possible
                                menuItemPasteFromClipboard.IsEnabled = true;
                                break;
                            }
                        }
                    }
                }
                else if (control is MetadataDataEntryMultiChoice multiChoice)
                {
                    // Only a value present as a menu choice is valid 
                    menuItemPasteFromClipboard!.IsEnabled = false;
                    WatermarkCheckComboBox comboBox = multiChoice.ContentControl;
                    foreach (Object t in comboBox.Items)
                    {
                        // This check skips over the Separator

                        if (clipboardText == t.ToString() )//((string)t).Trim())
                        {
                            // We found a matching value, so pasting is possible
                            menuItemPasteFromClipboard.IsEnabled = true;
                            break;
                        }
                    }
                }
                else if (control is MetadataDataEntryDateTimeCustom || control is MetadataDataEntryDate || control is MetadataDataEntryTime)
                {
                    menuItemPasteFromClipboard!.IsEnabled = false;
                    // This doesn't work as we have to set the value, not the text (I think)
                    //menuItemPasteFromClipboard.IsEnabled = DateTime.TryParse(clipboardText, out DateTime dateTime);
                    //if (menuItemPasteFromClipboard.IsEnabled)
                    //{
                    //    clipboardText = DateTimeHandler.ToStringDatabaseDateTime(dateTime);
                    //}
                }

                // Alter the paste header to show the text that will be pasted e.g Paste 'Lion'
                if (menuItemPasteFromClipboard!.IsEnabled)
                {
                    menuItemPasteFromClipboard.Header = "Paste '" + (clipboardText.Length > 20 ? clipboardText[..20] + Unicode.Ellipsis : clipboardText) + "'";
                }
                else
                {
                    // Since there is nothing in the clipboard, just show 'Paste'
                    menuItemPasteFromClipboard.Header = "Paste";
                }

                // Alter the copy header to show the text that will be copied, i.e. Copy 'Lion'
                if (menuItemCopyToClipboard!.IsEnabled)
                {
                    string content = control.Content!.Trim();
                    menuItemCopyToClipboard.Header = "Copy '" + (content.Length > 20 ? content[..20] + Unicode.Ellipsis : content) + "'";
                }
                else
                {
                    // Since there an empty string to Copy, just show 'Copy'
                    menuItemCopyToClipboard.Header = "Copy";
                }
                Clipboard.SetText(clipboardText);
            }
        }
        #endregion

        #region Event handlers - Content Selections and Changes
        // Note: Update the structure/database with the new value (if its different)
        private void NoteControl_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                MetadataDataEntryNote control = (MetadataDataEntryNote)textBox.Tag;
                control.SetContentAndTooltip(control.ContentControl.Text);
                UpdateMetadataTableAndMetadataDatabase(control);
            }
        }

        // MultiLine:  Update the structure/database with the new value (if its different) 
        private void MultiLineControl_TextHasChanged(object sender, EventArgs e)
        {
            if (sender is MultiLineText editor)
            {
                MetadataDataEntryMultiLine control = (MetadataDataEntryMultiLine)editor.Tag;
                control.SetContentAndTooltip(control.ContentControl.Text);
                UpdateMetadataTableAndMetadataDatabase(control);
            }
        }
        
        // Note: Update the structure/database with the new value (if its different)
        private void AlphaNumericControl_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // Check if there are any non-valid letters in the textBox
                MetadataDataEntryAlphaNumeric control = (MetadataDataEntryAlphaNumeric)textBox.Tag;
                control.SetContentAndTooltip(control.ContentControl.Text);
                UpdateMetadataTableAndMetadataDatabase(control);
            }
        }

        private void IntegerAnyControl_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is IntegerUpDown integerUpDown)
            {
                // Check if there are any non-valid letters in the textBox
                MetadataDataEntryIntegerAny control = (MetadataDataEntryIntegerAny)integerUpDown.Tag;
                control.SetContentAndTooltip (string.IsNullOrWhiteSpace(control.ContentControl.Text) ? "0" : control.ContentControl.Text);
                UpdateMetadataTableAndMetadataDatabase(control);
            }
        }

        private void IntegerPositiveControl_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is IntegerUpDown integerUpDown)
            {
                // Check if there are any non-valid letters in the textBox
                MetadataDataEntryIntegerPositive control = (MetadataDataEntryIntegerPositive)integerUpDown.Tag;
                control.SetContentAndTooltip(string.IsNullOrWhiteSpace(control.ContentControl.Text) ? "0" : control.ContentControl.Text);
                UpdateMetadataTableAndMetadataDatabase(control);
            }
        }

        private void DecimalAnyControl_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is DoubleUpDown doubleUpDown)
            {
                // Check if there are any non-valid letters in the textBox
                MetadataDataEntryDecimalAny control = (MetadataDataEntryDecimalAny)doubleUpDown.Tag;
                control.SetContentAndTooltip(string.IsNullOrWhiteSpace(control.ContentControl.Text) ? "0" : control.ContentControl.Text);
                UpdateMetadataTableAndMetadataDatabase(control);
            }
        }

        private void DecimalPositiveControl_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is DoubleUpDown doubleUpDown)
            {
                // Check if there are any non-valid letters in the textBox
                MetadataDataEntryDecimalPositive control = (MetadataDataEntryDecimalPositive)doubleUpDown.Tag;
                control.SetContentAndTooltip(string.IsNullOrWhiteSpace(control.ContentControl.Text) ? "0" : control.ContentControl.Text);
                UpdateMetadataTableAndMetadataDatabase(control);
            }
        }

        // When a choice changes, update the particular choice field(s) in the database
        private void FixedChoiceControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox { SelectedItem: ComboBoxItem cbi } comboBox)
            {
                // Get the control, but because it doesn't contain the select value yet, we need to put it in explicitly.
                MetadataDataEntryFixedChoice control = (MetadataDataEntryFixedChoice)comboBox.Tag;
                control.SetContentAndTooltip((string)cbi.Content);
                UpdateMetadataTableAndMetadataDatabase(control);
            }
        }

        // When a multiChoice changes, update the particular choice field(s) in the database
        private void MultiChoice_ItemSelectionChanged(object sender, ItemSelectionChangedEventArgs e)
        {
            if (sender is WatermarkCheckComboBox checkComboBox)
            {
                // Get the control, but because it doesn't contain the select value yet, we need to put it in explicitly.
                MetadataDataEntryMultiChoice control = (MetadataDataEntryMultiChoice)checkComboBox.Tag;

                // Sort the checkComboBox Text:
                // - by collecting and sorting the current checkComboBox selected items into a comma-delimited string
                List<string> list = [];
                foreach (string item in checkComboBox.SelectedItems)
                {
                    list.Add(item);
                }
                list.Sort();
                string newText = string.Join(",", list).Trim(',');
                if (checkComboBox.Text != newText)
                {
                    checkComboBox.Text = newText;
                }

                // Not sure why a comma is being inserted in the beginning of the text, but this fixes it.
                control.SetContentAndTooltip(checkComboBox.Text.TrimStart(','));
                UpdateMetadataTableAndMetadataDatabase(control);
            }
        }

        private void DateTimeCustom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (sender is WatermarkDateTimePicker dateTimePicker)
            {
                // Get the control, but because it doesn't contain the select value yet, we need to put it in explicitly.
                MetadataDataEntryDateTimeCustom control = (MetadataDataEntryDateTimeCustom)dateTimePicker.Tag;
                control.SetContentAndTooltip(dateTimePicker.Value);
                UpdateMetadataTableAndMetadataDatabase(control);
            }
        }

        private void Date_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (sender is WatermarkDateTimePicker dateTimePicker)
            {
                // Get the control, but because it doesn't contain the select value yet, we need to put it in explicitly.
                MetadataDataEntryDate control = (MetadataDataEntryDate)dateTimePicker.Tag;
                control.SetContentAndTooltip(dateTimePicker.Value);
                UpdateMetadataTableAndMetadataDatabase(control);
            }
        }

        private void Time_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (sender is TimePicker timePicker)
            {
                // Get the control, but because it doesn't contain the select value yet, we need to put it in explicitly.
                MetadataDataEntryTime control = (MetadataDataEntryTime)timePicker.Tag;
                control.SetContentAndTooltip(timePicker.Text);
                UpdateMetadataTableAndMetadataDatabase(control);
            }
        }

        //  Flag: Update the structure/database with the new value (if its different)
        private void FlagControl_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (IsProgrammaticControlUpdate)
            {
                return;
            }
            if (sender is CheckBox checkBox)
            {
                MetadataDataEntryControl control = (MetadataDataEntryControl)checkBox.Tag;
                control.SetContentAndTooltip(checkBox.IsChecked == true ? BooleanValue.True : BooleanValue.False);
                UpdateMetadataTableAndMetadataDatabase(control);
            }
        }

        #endregion

        #region Update Rows
        // Update either the current row or the selected rows in the database, 
        // depending upon whether we are in the single image or  theThumbnailGrid view respectively.
        public void UpdateMetadataTableAndMetadataDatabase(MetadataDataEntryControl control)
        {
            // Now, update the similar entry in the MetadataTable structure
            DataTableBackedList<MetadataRow> rows = FileDatabase.MetadataTablesByLevel[control.ParentPanel.Level];
            foreach (MetadataRow row in rows)
            {
                if (row[DatabaseColumn.FolderDataPath] == control.ParentPanel.SubPath)
                {
                    if (row[control.DataLabel] == control.Content)
                    {
                        // old and new values are the same, so abort.
                        return;
                    }
                    row[control.DataLabel] = control.Content;
                }
            }


            // First, update the appropriate tablel in the database
            // where we specify the row having the matching RelativePathToTheCurrentFolder, and indicate the datalabel and its new content
            string tableName = FileDatabase.MetadataComposeTableNameFromLevel(control.ParentPanel.Level);
            ColumnTuplesWithWhere columnToUpdate = new();
            if (control is MetadataDataEntryDateTimeCustom dateTimeCustom)
            {
                if (dateTimeCustom.ContentControl.Value != null)
                {
                    DateTime dateTime = (DateTime)dateTimeCustom.ContentControl.Value;
                    columnToUpdate.Columns.Add(new(control.DataLabel, DateTimeHandler.ToStringDatabaseDateTime(dateTime))); // Populate the datetime data using the database datetime format
                }
            }
            else if (control is MetadataDataEntryDate date)
            {
                if (date.ContentControl.Value != null)
                {
                    DateTime dateTime = (DateTime)date.ContentControl.Value;
                    columnToUpdate.Columns.Add(new(control.DataLabel, DateTimeHandler.ToStringDatabaseDate(dateTime))); // Populate the datetime data using the database datetime format
                }
            }
            else
            {
                columnToUpdate.Columns.Add(new(control.DataLabel, control.Content)); // Populate the data 
            }

            //columnToUpdate.SetWhere(new ColumnTuple(Constant.DatabaseColumn.FolderDataPath, control.ParentPanel.RelativePathToCurrentFolder));
            columnToUpdate.SetWhere(new ColumnTuple(DatabaseColumn.FolderDataPath, control.ParentPanel.SubPath));


            FileDatabase.Database.Update(tableName, columnToUpdate);
        }

        #endregion

        #region Utilities
        public static bool TryFindFocusedControl(IInputElement focusedElement, out MetadataDataEntryControl focusedControl)
        {
            if (focusedElement is FrameworkElement focusedFrameworkElement)
            {
                focusedControl = (MetadataDataEntryControl)focusedFrameworkElement.Tag;
                if (focusedControl != null)
                {
                    return true;
                }

                // for complex controls which dynamic generate child controls, such as date time pickers, the tag of the focused element can't be set
                // so try to locate a parent of the focused element with a tag indicating the control
                FrameworkElement parent = null;
                if (focusedFrameworkElement.Parent != null && focusedFrameworkElement.TemplatedParent is FrameworkElement)
                {
                    parent = (FrameworkElement)focusedFrameworkElement.Parent;
                }
                else if (focusedFrameworkElement.TemplatedParent is FrameworkElement element)
                {
                    parent = element;
                }

                if (parent != null)
                {
                    return TryFindFocusedControl(parent, out focusedControl);
                }
            }
            focusedControl = null;
            return false;
        }
        #endregion

        #region Disposing

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                FileDatabase?.Dispose();
            }
            disposed = true;
        }
        #endregion
    }
}
