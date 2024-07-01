using System;
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

        public static void EditorDefaultMultiChoiceValuesMustMatchChoiceListsDialog(Window owner, string invalidDefaultValue)
        {
            MessageBox messageBox = new MessageBox("MultiChoice default values must match an item in the Choice menu", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Warning,
                    Problem =
                        $"'{invalidDefaultValue}' is not allowed as a default value, as it is not a combination of one or more of your 'Define List' items.{Environment.NewLine}" +
                        "Choice default values must be either empty or a comma-separated list matching one or more of those items.",
                    Result = "The default value will be cleared.",
                    Hint = "Example: If your DefineList contains Aa Bb Cc, a default value specifying Aa and Cc would be Aa,Cc."
                }
            };
            messageBox.ShowDialog();
        }

        public static void EditorDefaultDateTimeCustomValuesIncorrectDialog(Window owner, string invalidDefaultValue)
        {
            MessageBox messageBox = new MessageBox("The entered Date and/or Time value did not match the expected format", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Warning,
                    Problem =
                        $"The value you entered was not allowed as a default value, as it was not in the expected date format.{Environment.NewLine}" +
                        $"Depending on which date/time control you are using, default formats are:{Environment.NewLine}" +
                        $"• Date: YYYY-MM-DD{Environment.NewLine}" +
                        $"• Time: HH:MM:SS in 24 hour time{Environment.NewLine}" +
                        "• Date and Time: YYYY-MM-DD HH:MM:SS in 24 hour time",
                    Result = "A standard default value was substituted  instead.",
                    Hint = $"Example: for Dec 24, 2024 at 3:25 am, you would enter:{Environment.NewLine}" +
                           $"• Date: 2024-12-24{Environment.NewLine}" +
                           $"• Time: 15:25:00{Environment.NewLine}" +
                           "• Date and Time: 2024-12-24 15:25:00"
                }
            };
            messageBox.ShowDialog();
        }

        public static void EditorDefaultTimeCustomValuesIncorrectDialog(Window owner, string invalidDefaultValue)
        {
            MessageBox messageBox = new MessageBox("The entered Time value did not match the expected format", owner)
            {
                Message =
                {
                    Icon = MessageBoxImage.Warning,
                    Problem =
                        $"The value you entered was not allowed as a default value, as it was not in the expected time format.{Environment.NewLine}" +
                        $"Default format is:{Environment.NewLine}" +
                        "• Time: HH:MM:SS in 24 hour time",
                    Result = "A standard default value was substituted  instead.",
                    Hint = "Example: you would enter 3:25 am as 15:25:00"
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
