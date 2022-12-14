using System;
using System.Collections.Generic;
using System.Windows;
using MessageBox = Timelapse.Dialog.MessageBox;

namespace Timelapse.Editor.Dialog
{
    public static class EditorDialogs
    {
        /// <summary>
        /// The template file no longer exists
        /// </summary>
        public static void EditorTemplateFileNoLongerExistsDialog(Window owner, string templateFileName)
        {
            MessageBox messageBox = new MessageBox("The template file no longer exist", owner);
            messageBox.Message.Icon = MessageBoxImage.Warning;
            messageBox.Message.Problem = String.Format("The template file '{0}' no longer exists.", templateFileName);
            messageBox.ShowDialog();
        }

        /// <summary>
        /// One or more data labels were problematic
        /// </summary>
        public static void EditorDataLabelsProblematicDialog(Window owner, List<string> conversionErrors)
        {
            MessageBox messageBox = new MessageBox("One or more data labels were problematic", owner);
            messageBox.Message.Icon = MessageBoxImage.Warning;

            messageBox.Message.Problem = (conversionErrors == null) ? "Some" : conversionErrors.Count.ToString();
            messageBox.Message.Problem += " of your Data Labels were problematic." + Environment.NewLine + Environment.NewLine;
            messageBox.Message.Problem += "Data Labels:" + Environment.NewLine;
            messageBox.Message.Problem += "\u2022 must be unique," + Environment.NewLine;
            messageBox.Message.Problem += "\u2022 can only contain alphanumeric characters and '_'," + Environment.NewLine;
            messageBox.Message.Problem += "\u2022 cannot match particular reserved words.";
            messageBox.Message.Result = "We will automatically repair these Data Labels:";
            if ((conversionErrors != null))
            {
                foreach (string erroneousDatalabel in conversionErrors)
                {
                    messageBox.Message.Solution += Environment.NewLine + "\u2022 " + erroneousDatalabel;
                }

                messageBox.Message.Hint = "Check if these are the names you want. You can also rename these corrected Data Labels if you want";
            }
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Confirm Showing UTC Date/Time Settings
        /// </summary>
        public static bool? EditorConfirmShowingUTCDateTimeDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Confirm Showing UTC Date/Time Settings", owner, MessageBoxButton.OKCancel);
            messageBox.Message.Icon = MessageBoxImage.Information;

            messageBox.Message.What = "Timelapse presents Date and Time conventionally in its interface, and as separate columns in the exported spreadsheet." + Environment.NewLine;
            messageBox.Message.What += "As an option, Timelapse can also show Date and Time information that follows the Coordinated Universal Time (UTC) standard." + Environment.NewLine;
            messageBox.Message.What += "That standard records an absolute Date/Time, plus a separate Time Zone Offset to transform that to local time." + Environment.NewLine + Environment.NewLine;
            messageBox.Message.What += "In rare cases, you may want:" + Environment.NewLine +
                "\u2022 to include additional UTC datetime and offset columns in the exported spreadsheet, and" + Environment.NewLine +
                "\u2022 to have your analysts to see the UTC Time Zone information within Timelapse";
            messageBox.Message.Result = "If you select Okay:" + Environment.NewLine +
                          "\u2022 this Editor will display the UtcOffset control as an additonal row" + Environment.NewLine +
                          "\u2022 If the UtcOffset control is set to visible, Timelapse will also export two extra columns to the spreadsheet:" + Environment.NewLine +
                          "    \u25e6 the absolute UTC DateTime, and " + Environment.NewLine +
                          "    \u25e6 the UTC offset necessary to convert absolute UTC DateTime to local time.";
            messageBox.Message.Hint = "Avoid showing UTC Date/Time unless you really need it, as your users may find it confusing." + Environment.NewLine;
            return messageBox.ShowDialog();
        }

        /// <summary>
        /// 
        /// </summary>
        public static void EditorDataLabelIsAReservedWordDialog(Window owner, string data_label)
        {
            MessageBox messageBox = new MessageBox("'" + data_label + "' is not a valid data label.", owner);
            messageBox.Message.Icon = MessageBoxImage.Warning;
            messageBox.Message.Problem = "Data labels cannot match the reserved words.";
            messageBox.Message.Result = "We will add an '_' suffix to this Data Label to make it differ from the reserved word";
            messageBox.Message.Hint = "Avoid the reserved words listed below. Start your label with a letter. Then use any combination of letters, numbers, and '_'." + Environment.NewLine;
            foreach (string keyword in EditorConstant.ReservedSqlKeywords)
            {
                messageBox.Message.Hint += keyword + " ";
            }
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Data Labels cannot be empty
        /// </summary>
        public static void EditorDataLabelsCannotBeEmptyDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Data Labels cannot be empty", owner);
            messageBox.Message.Icon = MessageBoxImage.Warning;
            messageBox.Message.Problem = "Data Labels cannot be empty. They must begin with a letter, followed only by letters, numbers, and '_'.";
            messageBox.Message.Result = "We will automatically create a uniquely named Data Label for you.";
            messageBox.Message.Hint = "You can create your own name for this Data Label. Start your label with a letter. Then use any combination of letters, numbers, and '_'.";
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Data lable is not a valid data label
        /// </summary>
        public static void EditorDataLabelIsInvalidDialog(Window owner, string old_data_label, string new_data_label)
        {
            MessageBox messageBox = new MessageBox("'" + old_data_label + "' is not a valid data label.", owner);
            messageBox.Message.Icon = MessageBoxImage.Warning;
            messageBox.Message.Problem = "Data labels must begin with a letter, followed only by letters, numbers, and '_'.";
            messageBox.Message.Result = "We replaced all dissallowed characters with an 'X': " + new_data_label;
            messageBox.Message.Hint = "Start your label with a letter. Then use any combination of letters, numbers, and '_'.";
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Data Labels must be unique
        /// </summary>
        public static void EditorDataLabelsMustBeUniqueDialog(Window owner, string data_label)
        {
            MessageBox messageBox = new MessageBox("Data Labels must be unique.", owner);
            messageBox.Message.Icon = MessageBoxImage.Warning;
            messageBox.Message.Problem = "'" + data_label + "' is not a valid Data Label, as you have already used it in another row.";
            messageBox.Message.Result = "We will automatically create a unique Data Label for you by adding a number to its end.";
            messageBox.Message.Hint = "You can create your own unique name for this Data Label. Start your label with a letter. Then use any combination of letters, numbers, and '_'.";
            messageBox.ShowDialog();
        }

        /// <summary>
        /// Data label requirements: Data Labels can only contain letters, numbers and '_'
        /// </summary>
        public static void EditorDataLabelRequirementsDialog(Window owner)
        {
            MessageBox messageBox = new MessageBox("Data Labels can only contain letters, numbers and '_'.", owner);
            messageBox.Message.Icon = MessageBoxImage.Warning;
            messageBox.Message.Problem = "Data labels must begin with a letter, followed only by letters, numbers, and '_'.";
            messageBox.Message.Result = "We will automatically ignore other characters, including spaces";
            messageBox.Message.Hint = "Start your label with a letter. Then use any combination of letters, numbers, and '_'.";
            messageBox.ShowDialog();
        }

        /// <summary>
        /// DefaultChoiceValuesMustMatchChoiceLists
        /// </summary>
        public static void EditorDefaultChoicValuesMustMatchChoiceListsDialog(Window owner, string invalidDefaultValue)
        {
            MessageBox messageBox = new MessageBox("Choice default values must match an item in the Choice menu", owner);
            messageBox.Message.Icon = MessageBoxImage.Warning;
            messageBox.Message.Problem = String.Format("'{0}' is not allowed as a default value, as it is not one of your 'Define List' items.{1}Choice default values must be either empty or must match one of those items.", invalidDefaultValue, Environment.NewLine);
            messageBox.Message.Result = "The default value will be cleared.";
            messageBox.Message.Hint = "Copy an item from your 'Define List' and paste it into your default value field as needed.";
            messageBox.ShowDialog();
        }
    }
}
