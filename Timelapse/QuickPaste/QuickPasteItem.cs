namespace Timelapse.QuickPaste
{
    // QuickPasteItem: Captures the essence of a single data control and its value
    public class QuickPasteItem(string dataLabel, string label, string value, bool enabled, string controlType)
    {
        #region Public Properties
        public string DataLabel { get; set; } = dataLabel; // identifies the control
        public string Label { get; set; } = label; // used to identify the control to the user
        public bool Use { get; set; } = enabled; // indicates whether the item should be used in a quickpaste (this can be set be the user) 
        public string Value { get; set; } = value; // the data can be pasted into a single data control 
        public string ControlType { get; set; } = controlType; // the type of data control 
        #endregion

        #region Constructors
        public QuickPasteItem() : this(string.Empty, string.Empty, string.Empty, false, string.Empty)
        {
        }

        #endregion
    }
}
