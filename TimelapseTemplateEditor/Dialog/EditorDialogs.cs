﻿using System;
using System.Windows;
using System.Windows.Input;
using TimelapseTemplateEditor.EditorCode;
using MessageBox = Timelapse.Dialog.MessageBox;

namespace TimelapseTemplateEditor.Dialog
{
    public static class EditorDialogs
    {
        /// <summary>
        /// The template file no longer exists
        /// </summary>
        public static void EditorTemplateFileNoLongerExistsDialog(Window owner, string templateFileName)
        {
            MessageBox messageBox = new MessageBox("The template file no longer exist", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Warning,
                    Problem = $"The template file '{templateFileName}' no longer exists."
                }
            };
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Could not create a template based on the style
        /// </summary>
        public static void EditorTemplateCouldNotCreateStandardDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Could not create a template based on the standard", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Error,
                    Problem = "A template based on the standard could not be created",
                    Reason = "We are not sure what happened or what to do next.",
                    Hint = "Contact the Timelapse creator if you need help resolving this"
                }
            };
            messageBox.ShowDialog();
        }

        public static bool? TypeChangeInformationDialog(Window owner, string from, string to)
        {
            MessageBox messageBox = new MessageBox("Changing a data field's type", owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    Icon = MessageBoxImage.Warning,
                    What = $"You are changing the data field's type from '{from}' to '{to}.'{Environment.NewLine}" +
                           $"This may have consequences when loading a Timelapse Data (.ddb) file previously opened with the old data type.{Environment.NewLine}" +
                           $"1. Two equivalent types. You can convert back and forth between them, e.g., {Environment.NewLine}" +
                           $"   ‣ Note\u27F7MultiLine, as both contain plain text.{Environment.NewLine}" +
                           $"2. From a specialized type to a more general type. This is a one-way  operation. Reversing it falls under 3 below, e.g., {Environment.NewLine}" +
                           $"   ‣ PositiveDecimal→Decimal (decimals can contain positive decimals).{Environment.NewLine}" +
                           $"   ‣ Decimal→Note (notes can contain decimals as text).{Environment.NewLine}" +
                           $"3. Unsafe: From a general type to a specialized type. Timelapse will not allow the update as its data {Environment.NewLine}" +
                           $"   will likely not match what the specialized type expects, e.g., {Environment.NewLine}" +
                           $"   ‣ Note→Decimal (Note data may contain non-numeric characters).{Environment.NewLine}" +
                           $"   ‣ Decimal→Date (Decimal data if very different from Date data).{Environment.NewLine}" +
                           $"   ‣ Decimal→PositiveDecimal (Decimal data may be negative).{Environment.NewLine}{Environment.NewLine}" +
                           "The drop-down menu only lists the first two Safe type changes.", 

                    Result = $" • The data field's control will reflect the new type.{Environment.NewLine}" +
                             $" • The data field's default value may be adjusted if it doesn't match the new type.{Environment.NewLine}" +
                             " • When you load an existing data (.ddb) file with this revised template, Timelapse will check to see if the change is allowed.",

                    Hint = $"Hold the <Shift> key while opening the menu to select from all types, including unsafe ones.{Environment.NewLine}" +
                            $"If you plan to open an existing data file, consider the consequences (if any) on previously entered data, e.g., {Environment.NewLine}" +
                            $" • Note→MultiLine is ok: the control will now let you enter longer text.{Environment.NewLine}" +
                            $" • FixedChoice→MultiChoice is ok: the control will now let you do multi-selections.{Environment.NewLine}" +
                            $" • PositiveDecimal→Decimal is ok: the control will now let you enter negative numbers.{Environment.NewLine}" +
                            $" • Date→Text field is mostly ok, but previously entered Date data will become plain text.{Environment.NewLine}" +
                            $" • Note→Decimal is disallowed by Timelapse: previously entered Note data may be non-numeric.{Environment.NewLine}" +
                            " • Date→Decimal is disallowed by Timelapse: previously entered Date data will never by a decimal."
                }
            };
            return messageBox.ShowDialog();
        }

        public static void EditorDataLabelIsAReservedWordDialog(Window owner, string data_label)
        {
            MessageBox messageBox = new MessageBox("'" + data_label + "' is not a valid data label.", owner)
            {
                Message =
                 {
                     Icon = MessageBoxImage.Warning,
                     Problem = "Data labels cannot match the reserved words.",
                     Result = "We will add an '_' suffix to this Data Label to make it differ from the reserved word",
                     Hint = $"Avoid the reserved words listed below.{Environment.NewLine}Start your label with a letter. Then use any combination of letters, numbers, and '_'." + Environment.NewLine
                 }
            };
            foreach (string keyword in EditorConstant.ReservedSqlKeywords)
            {
                messageBox.Message.Hint += keyword + " ";
            }
            messageBox.ShowDialog();
        }

        public static void EditorDateAndTimeLabelAreReservedWordsDialog(Window owner, string data_label, bool isDate)
        {
            string offendingType = isDate ? "Date" : "Time";
            MessageBox messageBox = new MessageBox("'" + data_label + "' is not a valid data label.", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Warning,
                    Problem = $"{offendingType} cannot be used as a Data label.",
                    Reason = $"{offendingType} is already used internally by Timelapse to handle the image's date / time tag.",
                    Result = $"We will add an '_' suffix to {offendingType} make it differ",
                    Hint = $"Avoid using {offendingType}.{Environment.NewLine}Data labels should start with a letter. Then use any combination of letters, numbers, and '_'." + Environment.NewLine
                }
            };
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Data Labels cannot be empty
        /// </summary>
        public static void EditorDataLabelsCannotBeEmptyDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Data Labels cannot be empty", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Warning,
                    Problem = "Data Labels cannot be empty. They must begin with a letter, followed only by letters, numbers, and '_'.",
                    Result = "We will automatically create a uniquely named Data Label for you.",
                    Hint = "You can create your own name for this Data Label. Start your label with a letter. Then use any combination of letters, numbers, and '_'."
                }
            };
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Data label is not a valid data label
        /// </summary>
        public static void EditorDataLabelIsInvalidDialog(Window owner, string old_data_label, string new_data_label)
        {
            MessageBox messageBox = new MessageBox("'" + old_data_label + "' is not a valid data label.", owner)
            {
                Message =
                    {
                        Icon = MessageBoxImage.Warning,
                        Problem = "Data labels must begin with a letter, followed only by letters, numbers, and '_'.",
                        Result = "We replaced all dissallowed characters with an 'X': " + new_data_label,
                        Hint = "Start your label with a letter. Then use any combination of letters, numbers, and '_'."
                    }
            };
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Data Labels must be unique
        /// </summary>
        public static void EditorDataLabelsMustBeUniqueDialog(Window owner, string data_label)
        {
            MessageBox messageBox = new MessageBox("Data Labels must be unique.", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Warning,
                    Problem = "'" + data_label + "' is not a valid Data Label, as you have already used it in another row.",
                    Result = "We will automatically create a unique Data Label for you by adding a number to its end.",
                    Hint = "You can create your own unique name for this Data Label. Start your label with a letter. Then use any combination of letters, numbers, and '_'."
                }
            };
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Data label requirements: Data Labels can only contain letters, numbers and '_'
        /// </summary>
        public static void EditorDataLabelRequirementsDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Data Labels can only contain letters, numbers and '_'.", owner)
            {
                Message =
                    {
                        Icon = MessageBoxImage.Warning,
                        Problem = "Data labels must begin with a letter, followed only by letters, numbers, and '_'.",
                        Result = "We will automatically ignore other characters, including spaces",
                        Hint = "Start your label with a letter. Then use any combination of letters, numbers, and '_'."
                    }
            };
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Labels must be unique
        /// </summary>
        public static void EditorLabelsMustBeUniqueDialog(Window owner, string label)
        {
            MessageBox messageBox = new MessageBox("Labels must be unique.", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Warning,
                    Problem = "'" + label + "' is not a valid Label, as you have already used it in another row.",
                    Result = "We will automatically create a unique Label for you by adding a number to its end.",
                    Hint = "You can overwrite this label with your own choice of a unique label name."
                }
            };
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Data Labels cannot be empty
        /// </summary>
        public static void EditorLabelsCannotBeEmptyDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Labels cannot be empty", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Warning,
                    Problem = "Labels cannot be empty. They identify what each data field represents to the Timelapse user.",
                    Result = "We will automatically create a uniquely named label for you.",
                    Hint = "Rename this to something meaningful. It only has to be different from the other labels."
                }
            };
            messageBox.ShowDialog();
        }
        /// <summary>
        /// DefaultChoiceValuesMustMatchChoiceLists
        /// </summary>
        public static void EditorDefaultChoiceValuesMustMatchChoiceListsDialog(Window owner, string invalidDefaultValue)
        {
            MessageBox messageBox = new MessageBox("Choice default values must match an item in the Choice menu", owner)
            {
                Message =
                    {
                        Icon = MessageBoxImage.Warning,
                        Problem =
                            $"'{invalidDefaultValue}' is not allowed as a default value, as it is not one of your 'Define List' items.{Environment.NewLine}Choice default values must be either empty or must match one of those items.",
                        Result = "The default value will be cleared.",
                        Hint = "Copy an item from your 'Define List' and paste it into your default value field as needed."
                    }
            };
            messageBox.ShowDialog();
        }

        /// <summary>
        /// EditorDefaultChoiceValuesMustMatchNonEmptyChoiceLists
        /// </summary>
        public static void EditorDefaultChoiceValuesMustMatchNonEmptyChoiceListsDialog(Window owner, string invalidDefaultValue)
        {
            MessageBox messageBox = new MessageBox("Choice default values must match an item in the Choice menu", owner)
            {
                Message =
                    {
                        Icon = MessageBoxImage.Warning,
                        Problem = string.IsNullOrEmpty(invalidDefaultValue)
                            ? $"An empty value is not allowed as a default value, as you have 'Include an empty item' unselected in your 'Define List' dialog.{Environment.NewLine}Choice default values must match one of your allowed items."
                            : $"'{invalidDefaultValue}' is not allowed as a default value, as it is not one of your 'Define List' items.{Environment.NewLine}Choice default values must match one of those items.{Environment.NewLine}An empty value is not allowed as a default value, as you have 'Include an empty item' unselected in your 'Define List' dialog.",
                        Result = "The default value was set to the first item on your 'Define List' items.",
                        Hint = "Change the default value if desired by copying an item from your 'Define List' and pasting it into your default value field as needed."
                    }
            };
            messageBox.ShowDialog();
        }

        #region MessageBox: Template: Changing controls can violate the current standard

        // Confirm closing this template and creating a new one
        private static bool dontShowChangesToStandardWarningDialog;
        public static bool? ChangesToStandardWarning(Window owner, string changeType, string standardType)
        {
            if (dontShowChangesToStandardWarningDialog)
            {
                return true;
            }
            string title = $"{changeType} may compromise the {standardType} standard";
            MessageBox messageBox = new MessageBox(title, owner, MessageBoxButton.OKCancel)
            {
                Message =
                {
                    Icon = MessageBoxImage.Information,
                    What = $"{title}.{Environment.NewLine}"
                           + $"This may cause problems if other software you use expects a strict {standardType} standard.",
                    Result = $"Select:{Environment.NewLine}"
                             + $"\u2022 Okay to keep {changeType.ToLower()} anyways,{Environment.NewLine}"
                             + "\u2022 Cancel to abort.",
                    Reason = $"The {standardType} defines what levels and fields are needed and how they are named.{Environment.NewLine}" +
                             "Changes to levels or fields can (perhaps) affect how other software uses your data.",
                    Hint = $"This is just a warning, as it really depends upon what you plan to do with your data.{Environment.NewLine}"
                          + "Ignore this if you know what you are doing."
                },
                DontShowAgain =
                {
                    Visibility = Visibility.Visible
                }
            };
            bool? result = messageBox.ShowDialog();
            if (messageBox.DontShowAgain.IsChecked.HasValue)
            {
                dontShowChangesToStandardWarningDialog = messageBox.DontShowAgain.IsChecked.Value;
            }

            return result;
        }

        #endregion
        #region MessageBox: ddb file opened with an older version of Timelapse than recorded in it
        public static bool? EditorDatabaseFileOpenedWithOlderVersionOfTimelapse(Window owner, EditorUserRegistrySettings userSettings)
        {
            Cursor cursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = null;
            // notify the user the template couldn't be loaded rather than silently doing nothing
            MessageBox messageBox = new MessageBox("You are opening your template with an older Timelapse Editor version ", owner, MessageBoxButton.OKCancel)
            {
                Message =
                    {
                        What = "You are opening your template with an older version of the Timelapse Editor." + Environment.NewLine
                            + "You previously used a later version of the Timelapse Editor to open this template." + Environment.NewLine
                            + "This is just a warning, as its rarely a problem.",
                        Reason = "Its best to use the latest Timelapse versions to minimize possible incompatabilities with older versions.",
                        Solution = "Click:" + Environment.NewLine
                                            + "\u2022 " + "Ok to keep going. It will likely work fine anyways." + Environment.NewLine
                                            + "\u2022 " + "Cancel to abort. You can then download the latest version from the Timelapse web site.",
                        Icon = MessageBoxImage.Warning,
                        Hint = "Select 'Don't show this message again' to hide this warning." + Environment.NewLine
                            + "You can unhide it using 'Options|Show or hide...' in the main Timelapse  program."
                    },
                DontShowAgain =
                    {
                        Visibility = Visibility.Visible
                    }
            };
            bool? result = messageBox.ShowDialog();
            if (messageBox.DontShowAgain.IsChecked.HasValue)
            {
                userSettings.SuppressOpeningWithOlderTimelapseVersionDialog = messageBox.DontShowAgain.IsChecked.Value;
            }
            Mouse.OverrideCursor = cursor;
            return result;
        }
        #endregion

        #region MessageBox: DeleteFolderLevelWarning
        public static bool? EditorDeleteFolderLevelWarning(Window owner, string levelName)
        {
            // warn the user about consequences of deleting a level
            MessageBox messageBox = new MessageBox($"Delete '{levelName}' folder level definition?", owner, MessageBoxButton.OKCancel)
            {
                Message =
                    {
                        What = $"You are about to delete the '{levelName}' folder definition and all the controls within it (if any)." + Environment.NewLine
                                + "Just checking to make sure you really want to do this.",
                        Solution = "\u2022 Ok deletes this level," + Environment.NewLine 
                                 + "\u2022 Cancel aborts deletion.",
                        Icon = MessageBoxImage.Warning,
                    },
            };
            return messageBox.ShowDialog();
        }
        #endregion
    }
}
