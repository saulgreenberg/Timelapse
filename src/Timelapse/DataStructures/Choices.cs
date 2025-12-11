using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Newtonsoft.Json;

namespace Timelapse.DataStructures
{
    // The ChoiceList class is used to maintain the Choice List data for FixedChoice controls.
    // It is also used to convert to and from a JSON string representation
    public class Choices
    {
        #region Private variables
        // ChoicesInternalPart isolates the Choice properties  we want to serialize and deserialize to a JSON file
        private ChoicesInternalPart ChoicesInternal;
        #endregion

        #region Public properties
        public bool IncludeEmptyChoice
        {
            get => ChoicesInternal.IncludeEmptyChoice;
            set => ChoicesInternal.IncludeEmptyChoice = value;
        }

        public List<string> ChoiceList
        {
            get => ChoicesInternal.ChoiceListNonEmpty;
            set => ChoicesInternal.ChoiceListNonEmpty = value;
        }

        // the list, including the optional empty item
        public List<string> GetAsListWithOptionalEmptyAsNewLine
        {
            get
            {
                if (IncludeEmptyChoice == false)
                {
                    // No empty item
                    return ChoiceList ??= [];
                }
                // Build and return the choice list with an empty item at the beginning
                List<string> choiceListWithEmpty = [..ChoiceList];
                choiceListWithEmpty.Insert(0, Environment.NewLine);
                return choiceListWithEmpty;
            }
        }

        public string GetAsJson =>
            ChoiceList.Count == 0
                ? string.Empty
                : JsonConvert.SerializeObject(ChoicesInternal);

        public string GetAsTextboxList => String.Join(Environment.NewLine, ChoiceList);

        #endregion

        #region Constructors
        // First form initializes the properties to the values provided as text,
        public Choices()
        {
            ChoicesInternal = new();
            ChoiceList = [];
            IncludeEmptyChoice = true;
        }

        // Second form initializes the properties to the values provided as text,
        // where each entry is separated by a line
        public Choices(string choiceText, bool includeEmptyChoice)
        {
            // If the list is empty, the includeEmptyChoice must be set to true
            // as otherwise there is no default value that can match
            if (false == includeEmptyChoice && string.IsNullOrWhiteSpace(choiceText))
            {
                includeEmptyChoice = true;
            }
            string[] NewLineDelimiter = [Environment.NewLine];
            choiceText = TrimLinesAndRemoveEmptyLines(choiceText);
            List<string> choiceList = string.IsNullOrWhiteSpace(choiceText)
                ? []
                : choiceText.Split(NewLineDelimiter, StringSplitOptions.None).ToList();
            ChoicesInternal = new();
            ChoiceList = choiceList;
            IncludeEmptyChoice = includeEmptyChoice;
        }
        #endregion

        #region Static Json Converters
        public static Choices ChoicesFromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new();
            }
            try
            {
                Choices choices = new();
                ChoicesInternalPart choicesInternal = JsonConvert.DeserializeObject<ChoicesInternalPart>(json);

                if (choicesInternal?.ChoiceListNonEmpty == null || choicesInternal.ChoiceListNonEmpty.Count == 0)
                {
                    // Just in case we can't deserialize the text or if it contains and empty list,
                    // this will always return a properly initialized empty choice structure
                    return choices;
                }
                choices.ChoicesInternal = choicesInternal;
                return choices;
            }
            catch
            {
                return new();
            }
        }
        #endregion

        #region Public Utilities
        // Given a combobox, set the contents of its items
        public void SetComboBoxItems(ComboBox comboBox)
        {
            if (comboBox == null)
            {
                // Just in case
                return;
            }

            comboBox.Items.Clear();
            if (IncludeEmptyChoice)
            {
                // Add an empty choice followed by a separator 
                comboBox.Items.Add(string.Empty);
                comboBox.Items.Add(new Separator());
            }
            foreach (string choice in ChoiceList)
            {
                // Add each non-empty string
                comboBox.Items.Add(choice);
            }

        }

        // Given a combobox, set the contents of its items
        // Unused, but keep for now in case we want it in the future
        //public void SetComboBoxItems(WatermarkCheckComboBox checkComboBox)
        //{
        //    if (checkComboBox == null)
        //    {
        //        // Just in case
        //        return;
        //    }

        //    checkComboBox.Items.Clear();
        //    if (IncludeEmptyChoice)
        //    {
        //        // Add an empty choice followed by a separator 
        //        checkComboBox.Items.Add(string.Empty);
        //        checkComboBox.Items.Add(new Separator());
        //    }
        //    foreach (string choice in ChoiceList)
        //    {
        //        // Add each non-empty string
        //        checkComboBox.Items.Add(choice);
        //    }
        //}

        public bool Contains(string itemToCheck)
        {
            if (string.IsNullOrEmpty(itemToCheck) && IncludeEmptyChoice)
            {
                return true;
            }
            return ChoiceList.Contains(itemToCheck);
        }
        #endregion

        #region Private static helpers

        // Transform a text list by
        // - trimming leading and trailing white space for each line,
        // - removing empty lines
        // - removing duplicate items
        private static string TrimLinesAndRemoveEmptyLines(string textlist)
        {
            string[] NewLineDelimiter = [Environment.NewLine];
            List<string> trimmedchoices = [];
            List<string> choices = [..textlist.Split(NewLineDelimiter, StringSplitOptions.RemoveEmptyEntries)];

            foreach (string choice in choices)
            {
                string trimmedchoice = choice.Trim();
                if (string.IsNullOrWhiteSpace(choice) == false && trimmedchoices.Contains(trimmedchoice) == false)
                {
                    trimmedchoices.Add(trimmedchoice);
                }
            }
            return string.Join(string.Join(string.Empty, NewLineDelimiter), trimmedchoices);
        }
        #endregion

        #region ChoicesInner class
        // The inner class is nested this way so that only this part of the 
        // Choices class will be serialized to/from a Json string
        private class ChoicesInternalPart
        {
            // When set to true, an empty item will be added to the FixedChoice or MultiChoice menu
            public bool IncludeEmptyChoice { get; set; }

            // the list excluding the empty item
            public List<string> ChoiceListNonEmpty { get; set; }
        }
        #endregion
    }
}
