using System.Collections.Generic;

namespace Timelapse.QuickPaste
{
    // QuickPasteEntry Data Structure: collects all the data controls and their values as a single potential quickpaste entry
    public class QuickPasteEntry
    {
        #region Public Properties
        public string Title { get; set; } = string.Empty; // a user- or system-supplied Title that will be displayed to identify the QuickPaste Entry
        public List<QuickPasteItem> Items { get; set; } = []; // a list of QuickPasteItems, each identifying a potential pastable control
        #endregion

        #region Public Methods
        // A test to see if at lease one item is marked as 'Use', i.e., if at least one item is pastable
        public bool IsAtLeastOneItemPastable()
        {
            foreach (QuickPasteItem item in Items)
            {
                if (item.Use)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion
    }
}
