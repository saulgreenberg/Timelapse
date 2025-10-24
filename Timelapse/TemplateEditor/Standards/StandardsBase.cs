using System.Collections.Generic;

namespace TimelapseTemplateEditor.Standards
{
    public static class StandardsBase
    {
        #region Method: Create the ChoiceList box
        public static string CreateChoiceList(bool includeEmptyChoice, List<string> choices)
        {
            string choiceList = "{" + $"\"IncludeEmptyChoice\":{includeEmptyChoice.ToString().ToLower()} ,\"ChoiceListNonEmpty\":[";
            foreach (string choice in choices)
            {
                choiceList += $"\"{choice}\", ";
            }
            choiceList += "]}";
            return choiceList;
        }
        #endregion
    }

    #region Class: The StandardsRow
    public class StandardsRow
    {
        public string Type { get; set; }
        public int Level { get; set; }
        public string DefaultValue { get; set; }
        public string Label { get; set; }
        public string DataLabel { get; set; }
        public string Tooltip { get; set; }
        public string Choice { get; set; }
        public bool Copyable { get; set; }
        public bool Visible { get; set; }

        public StandardsRow(string type, int level, string defaultValue, string label, string dataLabel, string tooltip, string choice, bool copyable = true, bool visible = true)
        {
            this.SetAllValues(type, level, defaultValue, label, dataLabel, tooltip, choice, copyable, visible);
        }

        private void SetAllValues(string type, int level, string defaultValue, string label, string dataLabel, string tooltip, string choice, bool copyable, bool visible)
        {
            Type = type;
            Level = level;
            DefaultValue = defaultValue;
            Label = label;
            DataLabel = dataLabel;
            Tooltip = tooltip;
            Choice = choice;
            Copyable = copyable;
            Visible = visible;
        }
    }
    #endregion
}
