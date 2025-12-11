using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.ControlsCore;
using Timelapse.DataStructures;
using Timelapse.Enums;
using TimelapseWpf.Toolkit;

namespace Timelapse.Util
{
    // Many callbacks are common between different controls of the same type. 
    // We centralize all that here to make managing them easier and to provide a unified control behaviour
    public static class ValidationCallbacks
    {
        #region Note Callbacks
        // Take action when a space character is detected
        public static void PreviewKeyDown_HandleKeyDownForEnter(object sender, KeyEventArgs e)
        {
            ControlsDataHelpers.GenericControlHandleKeyDownForEnter(e);
        }
        #endregion

        #region Text and Alphanumeric Callbacks 
        // Take action when a space character is detected
        public static void PreviewKeyDown_TextBoxNoSpaces(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                ControlsDataHelpers.TextBoxHandleKeyDownForSpace(textBox, e, true);
            }
        }

        // These callbacks accept only numbers, letters, - and _, with a Glob version adding * and ?
        public static void PreviewInput_AlphaNumericCharacterOnly(object sender, TextCompositionEventArgs args)
        {
            if (sender is TextBox textBox)
            {
                ControlsDataHelpers.TextBoxHandleAlphanumericInputOnly(textBox, args, true);
            }
        }

        public static void PreviewInput_AlphaNumericCharacterOnlyWithGlob(object sender, TextCompositionEventArgs args)
        {
            if (sender is TextBox textBox)
            {
                ControlsDataHelpers.TextBoxHandleAlphanumericWithGlobInputOnly(textBox, args, true);
            }
        }

        public static void TextChanged_AlphaNumericTextOnly(object sender, object _)
        {
            if (sender is TextBox textBox)
            {
                Window window = textBox.FindParentOfType<Window>();
                ControlsDataHelpers.TextBoxHandleAlphanumericTextChange(window, textBox, true);
            }
        }
        public static void TextChanged_AlphaNumericTextWithGlobCharactersOnly(object sender, TextChangedEventArgs args)
        {
            if (sender is TextBox textBox)
            {
                Window window = textBox.FindParentOfType<Window>();
                ControlsDataHelpers.TextBoxHandleAlphanumericWithGlobTextChangedAlphanumericWithGlobTextChanged(window, textBox, true);
            }
        }
        #endregion

        #region Integer callbacks
        // Accepts only number formats depending on the control type

        // These callbacks accept only numbers - depending on the kind of integer indicated
        public static void PreviewKeyDown_IntegerUpDownNoSpaces(object sender, KeyEventArgs e)
        {
            if (sender is IntegerUpDown integerUpDown)
            {
                TextBox textBox = VisualChildren.GetVisualChild<TextBox>(integerUpDown, "PART_TextBox");
                if (null != textBox)
                {
                    ControlsDataHelpers.TextBoxHandleKeyDownForSpace(textBox, e, true);
                }
            }
        }

        public static void PreviewInput_IntegerCharacterOnly(object sender, TextCompositionEventArgs args)
        {
            TextBox textBox = null;
            if (sender is IntegerUpDown integerUpDown)
            {
                textBox = VisualChildren.GetVisualChild<TextBox>(integerUpDown, "PART_TextBox");
            }
            else if (sender is TextBox textBoxTemp)
            {
                textBox = textBoxTemp;
            }
            if (null != textBox)
            {
                ControlsDataHelpers.TextBoxHandleIntegerInputOnly(textBox, args, false, true);
            }
        }


        // Also used by Counters
        public static void PreviewInput_IntegerPositiveCharacterOnly(object sender, TextCompositionEventArgs args)
        {
            TextBox textBox = null;
            if (sender is IntegerUpDown integerUpDown)
            {
                textBox = VisualChildren.GetVisualChild<TextBox>(integerUpDown, "PART_TextBox");
            }
            else if (sender is TextBox textBoxTemp)
            {
                textBox = textBoxTemp;
            }

            if (null != textBox)
            {
                ControlsDataHelpers.TextBoxHandleIntegerInputOnly(textBox, args, true, true);
            }
        }

        public static void PreviewKeyDown_DecimalUpDownNoSpaces(object sender, KeyEventArgs e)
        {
            if (sender is DoubleUpDown doubleUpDown)
            {
                TextBox textBox = VisualChildren.GetVisualChild<TextBox>(doubleUpDown, "PART_TextBox");
                if (null != textBox)
                {
                    ControlsDataHelpers.TextBoxHandleKeyDownForSpace(textBox, e, true);
                }
            }
        }

        public static void PreviewInput_DecimalCharacterOnly(object sender, TextCompositionEventArgs args)
        {
            TextBox textBox = null;
            if (sender is DoubleUpDown doubleUpDown)
            {
                textBox = VisualChildren.GetVisualChild<TextBox>(doubleUpDown, "PART_TextBox");
            }
            else if (sender is TextBox textBoxTemp)
            {
                textBox = textBoxTemp;
            }
            if (null != textBox)
            {
                ControlsDataHelpers.TextBoxHandleDecimalInputOnly(textBox, args, false, true);
            }
        }

        public static void PreviewInput_DecimalPositiveCharacterOnly(object sender, TextCompositionEventArgs args)
        {
            TextBox textBox = null;
            if (sender is DoubleUpDown doubleUpDown)
            {
                textBox = VisualChildren.GetVisualChild<TextBox>(doubleUpDown, "PART_TextBox");
            }
            else if (sender is TextBox textBoxTemp)
            {
                textBox = textBoxTemp;
            }
            if (null != textBox)
            {
                ControlsDataHelpers.TextBoxHandleDecimalInputOnly(textBox, args, true, true);
            }
        }

        public static void TextChanged_IntegerTextOnly(object sender, object _, bool positiveNumbersOnly)
        {
            if (sender is TextBox textBox)
            {
                Window window = textBox.FindParentOfType<Window>();
                ControlsDataHelpers.TextBoxHandleIntegerTextChanged(window, textBox, positiveNumbersOnly, true);
            }
        }

        public static void TextChanged_DecimalTextOnly(object sender, object _, bool positiveNumbersOnly)
        {
            if (sender is TextBox textBox)
            {
                Window window = textBox.FindParentOfType<Window>();
                ControlsDataHelpers.TextBoxHandleDecimalTextChanged(window, textBox, positiveNumbersOnly, true);
            }
        }
        #endregion

        #region Choices callbacks

        public static void TextChanged_FixedChoiceTextOnly(object sender, object _, Choices choiceList)
        {
            if (sender is TextBox textBox)
            {
                Window window = textBox.FindParentOfType<Window>();
                ControlsDataHelpers.TextBoxHandleFixedChoiceTextChanged(window, textBox, choiceList, true);
            }
        }

        public static void TextChanged_MultiChoiceTextOnly(object sender, object _, Choices choiceList)
        {
            if (sender is TextBox textBox)
            {
                Window window = textBox.FindParentOfType<Window>();
                ControlsDataHelpers.TextBoxHandleMultiChoiceTextChanged(window, textBox, choiceList, true);
            }
        }

        #endregion
        #region DateTime versions callbacks
        public static void TextChanged_DateTimeTextOnly(object sender, object _, DateTimeFormatEnum dateTimeFormat)
        {
            if (sender is TextBox textBox)
            {
                Window window = textBox.FindParentOfType<Window>();
                ControlsDataHelpers.TextBoxHandleDateTimeTextChanged(window, textBox, dateTimeFormat, true);
            }
        }
        #endregion

        #region Flag callbacks
        public static void TextChanged_BooleanTextOnly(object sender, object _)
        {
            if (sender is TextBox textBox)
            {
                Window window = textBox.FindParentOfType<Window>();
                ControlsDataHelpers.TextBoxHandleFlagTextChange(window, textBox, true);
            }
        }
        #endregion

        #region Paste Callbacks
        public static void Paste_OnlyIfAlphaNumeric(object sender, DataObjectPastingEventArgs args)
        {
            ControlsDataHelpers.Paste_OnlyIfAlphaNumeric(sender, args, false);
        }

        public static void Paste_OnlyIfCounter(object sender, DataObjectPastingEventArgs args)
        {
            ControlsDataHelpers.Paste_OnlyIfNumber(sender, args, NumberTypeEnum.Counter);
        }

        public static void Paste_OnlyIfIntegerAny(object sender, DataObjectPastingEventArgs args)
        {
            ControlsDataHelpers.Paste_OnlyIfNumber(sender, args, NumberTypeEnum.IntegerAny);
        }
        public static void Paste_OnlyIfIntegerPositive(object sender, DataObjectPastingEventArgs args)
        {
            ControlsDataHelpers.Paste_OnlyIfNumber(sender, args, NumberTypeEnum.IntegerPositive);
        }
        public static void Paste_OnlyIfDecimalAny(object sender, DataObjectPastingEventArgs args)
        {
            ControlsDataHelpers.Paste_OnlyIfNumber(sender, args, NumberTypeEnum.DecimalAny);
        }
        public static void Paste_OnlyIfDecimalPositive(object sender, DataObjectPastingEventArgs args)
        {
            ControlsDataHelpers.Paste_OnlyIfNumber(sender, args, NumberTypeEnum.DecimalPositive);
        }
        #endregion
    }
}
