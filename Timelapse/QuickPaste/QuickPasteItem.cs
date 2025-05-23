﻿namespace Timelapse.QuickPaste
{
    // QuickPasteItem: Captures the essence of a single data control and its value
    public class QuickPasteItem
    {
        #region Public Properties
        public string DataLabel { get; set; }       // identifies the control
        public string Label { get; set; }           // used to identify the control to the user
        public bool Use { get; set; }               // indicates whether the item should be used in a quickpaste (this can be set be the user) 
        public string Value { get; set; }           // the data can be pasted into a single data control 
        public string ControlType { get; set; }           // the type of data control 
        #endregion

        #region Constructors
        public QuickPasteItem() : this(string.Empty, string.Empty, string.Empty, false, string.Empty)
        {
        }
        public QuickPasteItem(string dataLabel, string label, string value, bool enabled, string controlType)
        {
            DataLabel = dataLabel;
            Label = label;
            Value = value;
            Use = enabled;
            ControlType = controlType;
        }
        #endregion
    }
}
