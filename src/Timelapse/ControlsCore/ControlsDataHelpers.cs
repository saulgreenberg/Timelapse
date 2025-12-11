using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Timelapse.Constant;
using Timelapse.ControlsDataEntry;
using Timelapse.ControlsMetadata;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Util;
using TimelapseWpf.Toolkit;
using TimelapseWpf.Toolkit.Primitives;
using Application = System.Windows.Application;
using Control = Timelapse.Constant.Control;
using DataFormats = System.Windows.DataFormats;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace Timelapse.ControlsCore
{
    public static class ControlsDataHelpers
    {
        #region Generic KeyDown handlers

        // Handle enter characters to do a tab traversal in the textbox.
        // If it is a tab, a traversal is invoked and true is returned
        public static bool GenericControlHandleKeyDownForEnter(KeyEventArgs args)
        {
            // Interpret an enter key as a tab 
            if (TraverseOnEnterKeyPress(args.Key))
            {
                return true;
            }

            return false;
        }

        // Handle space and tab characters entered in the textbox. If the key 
        // - is a tab, a traversal is invoked and true is returned
        // -is not a space, true is returned
        // - is  a space and
        //    - if no text is selected within it, the space is ignored and false is returned
        //    - if text is selected, the selected text is deleted and true is returned
        public static bool TextBoxHandleKeyDownForSpace(TextBox textBox, KeyEventArgs args, bool flashIfNeeded)
        {
            // Interpret an enter key as a tab
            if (TraverseOnEnterKeyPress(args.Key))
            {
                return true;
            }

            args.Handled = args.Key == Key.Space;
            if (args.Handled)
            {
                if (textBox.SelectedText == string.Empty)
                {
                    if (flashIfNeeded)
                    {
                        FlashContentControl(textBox);
                    }

                    return false;
                }

                // Some characters are selected. In this case, space should be used to remove selected text i.args., similar to a delete keypress
                int a = textBox.SelectionLength;
                int lastPosition = textBox.SelectionStart;
                textBox.Text = textBox.Text.Remove(textBox.SelectionStart, a);
                textBox.SelectionStart = lastPosition;
            }

            return true;
        }
        #endregion

        #region AlphaNumeric handlers
        // When a space key down occurs in an alphanumeric control, this helper:
        // - if no text is selected within it, the space is ignored and the control flashes
        // - if text is selected, the selected text is deleted
        // The two versions are identical except for its type (yes, I know...I do have to refactor these)
        public static void AlphaNumericHandleKeyDownForSpace(DataEntryAlphaNumeric alphaNumeric, KeyEventArgs args)
        {
            TextBox textBox = alphaNumeric.ContentControl;
            // If we are in viewonly state, this ensures that the number textbox can't be edited.
            if (alphaNumeric.ContentReadOnly)
            {
                args.Handled = true;
                return;
            }

            if (false == TextBoxHandleKeyDownForSpace(textBox, args, false))
            {
                alphaNumeric.FlashContentControl(FlashEnum.UseErrorFlash);
            }
        }

        public static void AlphaNumericHandleKeyDownForSpace(MetadataDataEntryAlphaNumeric alphaNumeric, KeyEventArgs args)
        {
            if (alphaNumeric.ContentReadOnly)
            {
                args.Handled = true;
                return;
            }

            TextBox textBox = alphaNumeric.ContentControl;
            // If we are in viewonly state, this ensures that the number textbox can't be edited.
            if (false == TextBoxHandleKeyDownForSpace(textBox, args, false))
            {
                alphaNumeric.FlashContentControl();
            }
        }

        public static void AlphaNumericHandleAlphaNumericInputOnly(DataEntryAlphaNumeric alphaNumeric, TextCompositionEventArgs args)
        {
            if (alphaNumeric.ContentReadOnly)
            {
                args.Handled = true;
                return;
            }

            TextBox textBox = alphaNumeric.ContentControl;
            // If we are in viewonly state, this ensures that the number textbox can't be edited.
            if (false == TextBoxHandleAlphanumericInputOnly(textBox, args, false))
            {
                alphaNumeric.FlashContentControl(FlashEnum.UseErrorFlash);
            }
        }

        public static void AlphaNumericHandleAlphaNumericInputOnly(MetadataDataEntryAlphaNumeric alphaNumeric, TextCompositionEventArgs args)
        {
            if (alphaNumeric.ContentReadOnly)
            {
                args.Handled = true;
                return;
            }

            TextBox textBox = alphaNumeric.ContentControl;
            // If we are in viewonly state, this ensures that the number textbox can't be edited.
            if (false == TextBoxHandleAlphanumericInputOnly(textBox, args, false))
            {
                alphaNumeric.FlashContentControl();
            }
        }

        public static void AlphaNumericHandleAlphaNumericTextChange(Window window, DataEntryAlphaNumeric alphaNumeric, TextChangedEventArgs args)
        {
            if (alphaNumeric.ContentReadOnly)
            {
                args.Handled = true;
                return;
            }

            TextBox textBox = alphaNumeric.ContentControl;
            // If we are in viewonly state, this ensures that the number textbox can't be edited.
            TextBoxHandleAlphanumericTextChange(window, textBox, window != null);
        }

        public static void AlphaNumericHandleAlphaNumericTextChange(Window window, MetadataDataEntryAlphaNumeric alphaNumeric, TextChangedEventArgs args)
        {
            if (alphaNumeric.ContentReadOnly)
            {
                args.Handled = true;
                return;
            }

            TextBox textBox = alphaNumeric.ContentControl;
            // If we are in viewonly state, this ensures that the number textbox can't be edited.
            TextBoxHandleAlphanumericTextChange(window, textBox, window != null);
        }

        // Handle non-alphanumeric characters entered into the text box by ignoring them and, if flashControl, flashing the textbox
        public static bool TextBoxHandleAlphanumericInputOnly(TextBox textBox, TextCompositionEventArgs args, bool flashControl)
        {
            if (IsCondition.IsLineFeedCharacters(args.Text))
            {
                args.Handled = false;
                return true;
            }

            args.Handled = !IsCondition.IsAlphaNumeric(args.Text);
            if (args.Handled)
            {
                if (flashControl)
                {
                    FlashContentControl(textBox);
                }

                return false;
            }

            return true;
        }

        // Handle non-alphanumeric characters entered into the text box by ignoring them and, if flashControl, flashing the textbox
        public static bool TextBoxHandleAlphanumericWithGlobInputOnly(TextBox textBox, TextCompositionEventArgs args, bool flashControl)
        {
            if (IsCondition.IsLineFeedCharacters(args.Text))
            {
                args.Handled = false;
                return true;
            }

            args.Handled = !IsCondition.IsAlphaNumericIncludingGlobCharacters(args.Text);
            if (args.Handled)
            {
                if (flashControl)
                {
                    FlashContentControl(textBox);
                }

                return false;
            }

            return true;
        }

        // Handle non-alphanumeric strings entered into the text box by clearing the textbox and showing an error dialog
        public static bool TextBoxHandleAlphanumericTextChange(Window window, TextBox textBox, bool showDialog)
        {
            if (false == IsCondition.IsAlphaNumeric(textBox.Text))
            {
                if (showDialog)
                {
                    Dialogs.InvalidDataFieldInput(window, Control.AlphaNumeric, textBox.Text);
                }

                textBox.Text = string.Empty;
                return false;
            }

            return true;
        }

        public static void Paste_OnlyIfAlphaNumeric(object sender, DataObjectPastingEventArgs args, bool isGlobVersion)
        {
            string textAfterPasting = string.Empty;
            Window window = null;
            // Get the text to paste
            bool isText = args.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true);
            if (!isText)
            {
                // Nothing to paste, so abort
                args.CancelCommand();
            }

            if (args.SourceDataObject.GetData(DataFormats.UnicodeText) is not string textToPaste)
            {
                args.CancelCommand();
                return;
            }
            if (sender is TextBox textBox)
            {
                textAfterPasting = textBox.Text;
                int selectionLength = textBox.SelectionLength;
                if (selectionLength > 0)
                {
                    // As some text was selected, we have to delete that selection before inserting the pasted text
                    textAfterPasting = textAfterPasting.Remove(textBox.SelectionStart, selectionLength);
                }

                // Insert the pasted text if its valid
                textAfterPasting = textAfterPasting.Insert(textBox.SelectionStart, textToPaste);
                if (isGlobVersion && IsCondition.IsAlphaNumericIncludingGlobCharacters(textAfterPasting))
                {
                    return;
                }

                if (IsCondition.IsAlphaNumeric(textAfterPasting))
                {
                    return;
                }

                // If we got here, then the pasted text is not what we want
                window = textBox.FindParentOfType<Window>();
            }

            args.CancelCommand();

            // if we got here, the text after pasting is not an alphanumeric
            Application.Current.Dispatcher.BeginInvoke(new Action(() => { Dialogs.InvalidDataFieldInput(window, Control.AlphaNumeric, textAfterPasting); }));
        }

        // Handle non-alphanumeric strings entered into the text box by clearing the textbox and showing an error dialog
        public static bool TextBoxHandleAlphanumericWithGlobTextChangedAlphanumericWithGlobTextChanged(Window window, TextBox textBox, bool showDialog)
        {
            if (false == IsCondition.IsAlphaNumericIncludingGlobCharacters(textBox.Text))
            {
                if (showDialog)
                {
                    Dialogs.InvalidDataFieldInput(window, Control.AlphaNumeric + "Glob", textBox.Text);
                }

                textBox.Text = string.Empty;
                return false;
            }

            return true;
        }

        #endregion

        #region Number handlers

        // Handle non-integer characters entered into the text box by ignoring them and, if flashControl, flashing the textbox.
        // While it will work for the majority of inputs, non-legal text can still appear in the textbox e.g. such as -, -., and
        // other non-legal text due to backspacing (which is not handled here)
        public static bool TextBoxHandleIntegerInputOnly(TextBox textBox, TextCompositionEventArgs args, bool positiveNumbersOnly, bool flashControl)
        {
            if (IsCondition.IsLineFeedCharacters(args.Text))
            {
                args.Handled = false;
                return true;
            }

            if (positiveNumbersOnly)
            {
                args.Handled = !IsCondition.IsIntegerPositiveCharacters(args.Text);
            }
            else
            {
                args.Handled = args.Text != Environment.NewLine && !IsCondition.IsIntegerCharacters(args.Text);
            }

            if (args.Handled)
            {
                if (flashControl)
                {
                    FlashContentControl(textBox);
                }

                return false;
            }

            // We have a valid character. Create a preview of the text by inserting the character at the character position.
            // and make sure we have a valid decimal
            // Otherwise flash the control and ignore that character. 

            // If there is a selection, then delete the selection and insert the character there
            // Otherwise just insert the selection at the current caret
            string tmpString = textBox.SelectionLength > 0
                ? textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength).Insert(textBox.SelectionStart, args.Text)
                : textBox.Text.Insert(textBox.CaretIndex, args.Text);
            // If the preview is just ".", then transform it into 0. (so its a legal input)
            if (tmpString == "-")
            {
                // Allow - input. While its not a valid number, later error management will catch this
                // if the user does not continue editing it into a valid number
                return true;
            }
            // Now check if its a valid integer.
            // Note that if its a positive only character, the '-' sign will have already been tested for before this
            if (false == Int32.TryParse(tmpString, out _))
            {
                if (flashControl)
                {
                    FlashContentControl(textBox);
                }
                args.Handled = true;
            }
            return true;
        }

        // Handle non-decimal characters entered into the text box by ignoring them and, if flashControl, flashing the textbox.
        // While it will work for the majority of inputs, non-legal text can still appear in the textbox e.g. such as -, -., and
        // other non-legal text due to backspacing (which is not handled here)
        public static bool TextBoxHandleDecimalInputOnly(TextBox textBox, TextCompositionEventArgs args, bool positiveNumbersOnly, bool flashControl)
        {
            if (IsCondition.IsLineFeedCharacters(args.Text))
            { 
                args.Handled = false;
                return true;
            }

            if (positiveNumbersOnly)
            {
                args.Handled = !IsCondition.IsDecimalPositiveCharacters(args.Text.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                args.Handled = !IsCondition.IsDecimalCharacters(args.Text.ToString(CultureInfo.InvariantCulture));
            }

            if (args.Handled)
            {
                if (flashControl)
                {
                    FlashContentControl(textBox);
                }
                return false;
            }

            // We have a valid character. Create a preview of the text by inserting the character at the character position.
            // and make sure we have a valid decimal
            // Otherwise flash the control and ignore that character. 

            // If there is a selection, then delete the selection and insert the character there
            // Otherwise just insert the selection at the current caret
            string tmpString = textBox.SelectionLength > 0 
                ? textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength).Insert(textBox.SelectionStart, args.Text)
                : textBox.Text.Insert(textBox.CaretIndex, args.Text);
            // If the preview is just ".", then transform it into 0. (so its a legal input)
            if (tmpString == ".")
            {
                textBox.Text ="0.";
                textBox.CaretIndex = 2;
                args.Handled = true;
                return true;
            }
            if (tmpString == "-" || tmpString == "-.")
            {
                // Allow - or -. input. While its not a valid number, later error management will catch this
                // if the user does not continue editing it into a valid number
                return true;
            }
            // Now check if its a valid decimal.
            // Note that if its a positive only character, the '-' sign will have already been tested for before this
            if (false == Double.TryParse(tmpString, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                if (flashControl)
                {
                    FlashContentControl(textBox);
                }
                args.Handled = true;
            }
            return true;
        }

        // Handle erroneous integer strings entered into the text box by clearing the textbox and showing an error dialog
        public static bool TextBoxHandleIntegerTextChanged(Window window, TextBox textBox, bool positiveNumbersOnly, bool showDialog)
        {
            if (textBox.Text == string.Empty)
            {
                return true;
            }

            if (positiveNumbersOnly)
            {
                if (false == IsCondition.IsIntegerPositive(textBox.Text))
                {
                    if (showDialog)
                    {
                        Dialogs.InvalidDataFieldInput(window, Control.IntegerPositive, textBox.Text);
                    }

                    textBox.Text = ControlDefault.NumberDefaultValue;
                    return false;
                }
            }
            else if (false == IsCondition.IsInteger(textBox.Text))
            {
                if (showDialog)
                {
                    Dialogs.InvalidDataFieldInput(window, Control.IntegerAny, textBox.Text);
                }

                textBox.Text = ControlDefault.NumberDefaultValue;
                return false;
            }

            return true;
        }

        // Handle erroneous decimal strings entered into the text box by clearing the textbox and showing an error dialog
        public static bool TextBoxHandleDecimalTextChanged(Window window, TextBox textBox, bool positiveNumbersOnly, bool showDialog)
        {
            if (textBox.Text == string.Empty)
            {
                return true;
            }

            if (positiveNumbersOnly)
            {
                if (false == IsCondition.IsDecimalPositive(textBox.Text))
                {
                    if (showDialog)
                    {
                        Dialogs.InvalidDataFieldInput(window, Control.DecimalPositive, textBox.Text);
                    }

                    textBox.Text = ControlDefault.NumberDefaultValue;
                    return false;
                }
            }
            else if (false == IsCondition.IsDecimal(textBox.Text))
            {
                if (showDialog)
                {
                    Dialogs.InvalidDataFieldInput(window, Control.DecimalAny, textBox.Text);
                }

                textBox.Text = ControlDefault.NumberDefaultValue;
                return false;
            }

            return true;
        }


        public static void Paste_OnlyIfNumber(object sender, DataObjectPastingEventArgs args, NumberTypeEnum numberType)
        {
            string textAfterPasting = string.Empty;
            Window window = null;
            // Get the text to paste
            bool isText = args.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true);
            if (!isText)
            {
                // Nothing to paste, so abort
                args.CancelCommand();
                return;
            }

            if (args.SourceDataObject.GetData(DataFormats.UnicodeText) is not string textToPaste)
            {
                args.CancelCommand();
                return;
            }
            if ((numberType == NumberTypeEnum.IntegerAny || numberType == NumberTypeEnum.IntegerPositive || numberType == NumberTypeEnum.Counter) && sender is IntegerUpDown integerUpDown)
            {
                TextBox textBox = VisualChildren.GetVisualChild<TextBox>(integerUpDown, "PART_TextBox");
                if (null != textBox)
                {
                    textAfterPasting = textBox.Text;
                    int selectionLength = textBox.SelectionLength;
                    if (selectionLength > 0)
                    {
                        // As some text was selected, we have to delete that selection before inserting the pasted text
                        textAfterPasting = textAfterPasting.Remove(textBox.SelectionStart, selectionLength);
                    }

                    // Insert the pasted text if its valid
                    textAfterPasting = textAfterPasting.Insert(textBox.SelectionStart, textToPaste);
                    if ((numberType == NumberTypeEnum.IntegerAny && IsCondition.IsInteger(textAfterPasting)) ||
                        ((numberType == NumberTypeEnum.IntegerPositive || numberType == NumberTypeEnum.Counter) && IsCondition.IsIntegerPositive(textAfterPasting))
                       )
                    {
                        return;
                    }
                }

                // If we got here, then the pasted text is not what we want
                window = textBox.FindParentOfType<Window>();
            }
            else if ((numberType == NumberTypeEnum.DecimalAny || numberType == NumberTypeEnum.DecimalPositive) && sender is DoubleUpDown doubleUpDown)
            {
                TextBox textBox = VisualChildren.GetVisualChild<TextBox>(doubleUpDown, "PART_TextBox");
                if (null != textBox)
                {
                    textAfterPasting = textBox.Text;
                    int selectionLength = textBox.SelectionLength;
                    if (selectionLength > 0)
                    {
                        // As some text was selected, we have to delete that selection before inserting the pasted text
                        textAfterPasting = textAfterPasting.Remove(textBox.SelectionStart, selectionLength);
                    }

                    // Insert the pasted text if its valid
                    textAfterPasting = textAfterPasting.Insert(textBox.SelectionStart, textToPaste);
                    if ((numberType == NumberTypeEnum.DecimalAny && IsCondition.IsDecimal(textAfterPasting)) ||
                        (numberType == NumberTypeEnum.DecimalPositive && IsCondition.IsDecimalPositive(textAfterPasting))
                       )
                    {
                        return;
                    }
                }

                // If we got here, then the pasted text is not what we want
                window = textBox.FindParentOfType<Window>();
            }

            args.CancelCommand();

            string controlType = "Number";
            switch (numberType)
            {
                case NumberTypeEnum.Counter:
                    controlType = Control.Counter;
                    break;
                case NumberTypeEnum.IntegerAny:
                    controlType = Control.IntegerAny;
                    break;
                case NumberTypeEnum.IntegerPositive:
                    controlType = Control.IntegerPositive;
                    break;
                case NumberTypeEnum.DecimalAny:
                    controlType = Control.DecimalAny;
                    break;
                case NumberTypeEnum.DecimalPositive:
                    controlType = Control.DecimalPositive;
                    break;
            }

            // if we got here, the text after pasting is not an integer
            Application.Current.Dispatcher.BeginInvoke(new Action(() => { Dialogs.InvalidDataFieldInput(window, controlType, textAfterPasting); }));
        }

        #endregion

        #region FixedChoice-specific handlers
        // Handle erroneous fixed choice strings entered into the text box by clearing the textbox and showing an error dialog
        public static bool TextBoxHandleFixedChoiceTextChanged(Window window, TextBox textBox, Choices choices, bool showDialog)
        {
            // Empty choices are allowed (well, that does depend on the setting. Check it?
            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                return true;
            }

            // The value present in the textbox, if any, must also be present in the choice list
            // Parse the current comma-separated textBox items as a list
            string[] parsedText = textBox.Text.Split(',');
            foreach (string str in parsedText)
            {
                if (choices.ChoiceList.Contains(str) == false)
                {
                    if (showDialog)
                    {
                        Dialogs.InvalidDataFieldInput(window, Control.FixedChoice, textBox.Text);
                    }
                    textBox.Text = ControlDefault.FixedChoiceDefaultValue;
                    return false;
                }
            }
            return true;
        }
        #endregion

        #region MultiChoice-specific handlers
        // Handle erroneous multichoice strings entered into the text box by clearing the textbox and showing an error dialog
        public static bool TextBoxHandleMultiChoiceTextChanged(Window window, TextBox textBox, Choices choices, bool showDialog)
        {
            // The values present in the textbox must also be present in the choice list
            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                return true;
            }

            // Parse the current comma-separated textBox items as a list
            string[] parsedText = textBox.Text.Split(',');
            List<string> sortedList = [];
            foreach (string str in parsedText)
            {
                if (choices.ChoiceList.Contains(str) == false)
                {
                    if (showDialog)
                    {
                        Dialogs.InvalidDataFieldInput(window, Control.MultiChoice, textBox.Text);
                    }

                    textBox.Text = ControlDefault.MultiChoiceDefaultValue;
                    return false;
                }
                sortedList.Add(str);
            }
            sortedList.Sort();
            textBox.Text = string.Join(",",  sortedList).Trim(',');
            return true;
        }

        // Used when an item is selected from a Multichoice drop-down, where  it syncs the multichoice drop-down list and its textbox
        // as otherwise the two can differ
        public static void MultiChoice_ItemSelectionChanged(object sender, ItemSelectionChangedEventArgs e)
        {
            if (sender is WatermarkCheckComboBox checkComboBox == false)
            {
                TracePrint.NullException(nameof(checkComboBox));
                return;
            }

            if (checkComboBox.SelectedItemsOverride != null)
            {
                // Parse the current checkComboBox items as a text string to update the checkComboBox text as needed
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
        }

        // Used when an item is selected from a Multichoice drop-down, in order to sync the multichoice drop-down list and its textbox
        // as otherwise the two can differ
        public static void WatermarkCheckComboBox_DropDownOpened(object sender, RoutedEventArgs e)
        {
            if (sender is WatermarkCheckComboBox checkComboBox)
            {
                ObservableCollection<string> itemsList = new(checkComboBox.Text.Split(','));
                checkComboBox.SelectedItemsOverride = itemsList;
            }
        }

        // Used when an item is selected from a Multichoice drop-down, in order to sync the multichoice drop-down list and its textbox
        // as otherwise the two can differ
        public static void WatermarkCheckComboBox_DropDownClosed(object sender, RoutedEventArgs e)
        {
            if (sender is WatermarkCheckComboBox checkComboBox)
            {
                // As setting the override to null clears the content control text, we save and then restore the content control tex.
                string savedContent = checkComboBox.Text;
                checkComboBox.SelectedItemsOverride = null;
                checkComboBox.Text = savedContent.Trim(',');
            }
        }
        #endregion

        #region DateTime handlers
        // Handle erroneous decimal strings entered into the text box by clearing the textbox and showing an error dialog
        public static bool TextBoxHandleDateTimeTextChanged(Window window, TextBox textBox, DateTimeFormatEnum dateTimeFormat, bool showDialog)
        {
            if (dateTimeFormat == DateTimeFormatEnum.DateAndTime)
            {
                if (false == DateTimeHandler.TryParseDatabaseDateTime(textBox.Text, out DateTime _))
                {
                    if (showDialog)
                    {
                        Dialogs.InvalidDataFieldInput(window, Control.DateTime_, textBox.Text);
                    }
                    textBox.Text = DateTimeHandler.ToStringDatabaseDateTime(ControlDefault.DateTimeCustomDefaultValue);
                    return false;
                }
            }
            else if (dateTimeFormat == DateTimeFormatEnum.DateOnly)
            {
                if (false == DateTimeHandler.TryParseDatabaseDate(textBox.Text, out DateTime _))
                {
                    if (showDialog)
                    {
                        Dialogs.InvalidDataFieldInput(window, Control.Date_, textBox.Text);
                    }
                    textBox.Text = DateTimeHandler.ToStringDatabaseDate(ControlDefault.Date_DefaultValue);
                    return false;
                }
            }
            else if (dateTimeFormat == DateTimeFormatEnum.TimeOnly)
            {
                if (false == DateTimeHandler.TryParseDatabaseTime(textBox.Text, out DateTime _))
                {

                    if (showDialog)
                    {
                        Dialogs.InvalidDataFieldInput(window, Control.Time_, textBox.Text);
                    }
                    textBox.Text = DateTimeHandler.ToStringTime(ControlDefault.Time_DefaultValue);
                    return false;
                }
            }
            return true;
        }
        #endregion

        #region Flag handlers
        public static bool TextBoxHandleFlagPreviewInput(TextBox textBox, TextCompositionEventArgs args, out string newValue)
        {
            // Only allow t/f and translate to true/false
            // Otherwise flash the textbox
            newValue = string.Empty;
            if (args.Text == "t" || args.Text == "T")
            {
                newValue = BooleanValue.True;
            }
            else if (args.Text == "f" || args.Text == "F")
            {
                newValue = BooleanValue.False;
            }

            if (string.IsNullOrEmpty(newValue))
            {
                FlashContentControl(textBox);
                args.Handled = true;
                return false;
            }
            return true;
        }

        // Handle non-boolean strings entered into the text box by clearing the textbox and showing an error dialog
        public static bool TextBoxHandleFlagTextChange(Window window, TextBox textBox, bool showDialog)
        {
            if (false == IsCondition.IsBoolean(textBox.Text))
            {
                if (showDialog)
                {
                    Dialogs.InvalidDataFieldInput(window, Control.Flag, textBox.Text);
                }
                textBox.Text = ControlDefault.FlagValue;
                return false;
            }
            return true;
        }
        #endregion

        #region Focus handlers
        // Highlight control when it gets the focus (simulates aspects of tab control in Timelapse)
        public static void Control_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Control control)
            {
                control.BorderThickness = new(Control.BorderThicknessHighlight);
                control.BorderBrush = Control.BorderColorHighlight;
            }
            else
            {
                TracePrint.StackTraceToOutput("Unexpected null");
            }
        }

        // Remove the highlight by restoring the original border appearance
        public static void Control_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Control control)
            {
                control.BorderThickness = new(Control.BorderThicknessNormal);
                control.BorderBrush = Control.BorderColorNormal;
            }
            else
            {
                TracePrint.StackTraceToOutput("Unexpected null");
            }
        }
        #endregion

        #region Visual effects used by above
        // Flash the content area of the control
        public static void FlashContentControl(TextBox textBox)
        {
            ScrollViewer contentHost = (ScrollViewer)textBox?.Template.FindName("PART_ContentHost", textBox);
            if (contentHost != null)
            {
                contentHost.Background = new SolidColorBrush(Colors.White);
                contentHost.Background.BeginAnimation(SolidColorBrush.ColorProperty, GetColorAnimation());
            }
        }

        // This is a standard color animation scheme that can be accessed by the other controls

        private static ColorAnimation GetColorAnimation()
        {
            return new()
            {
                From = Colors.LightCoral,
                AutoReverse = false,
                Duration = new(TimeSpan.FromSeconds(.1)),
                EasingFunction = new ExponentialEase
                {
                    EasingMode = EasingMode.EaseIn
                },
            };
        }
        #endregion

        #region TraverseOnEnterKeyPress
        private static bool TraverseOnEnterKeyPress(Key key)
        {
            // If we are in viewonly state, this ensures that the number textbox can't be edited.textBox.IsReadOnly = GlobalReferences.TimelapseState.IsViewOnly;
            if (key == Key.Enter)
            {
                // MoveFocus takes a TraversalRequest as its argument.
                TraversalRequest request = new(FocusNavigationDirection.Next);

                // Gets the element with keyboard focus.

                // Change keyboard focus.
                if (Keyboard.FocusedElement is UIElement elementWithFocus)
                {
                    elementWithFocus.MoveFocus(request);
                }
                return true;
            }
            return false;
        }
        #endregion
    }
}
