using System.Windows;
using System.Windows.Controls;
using TimelapseWpf.Toolkit;

namespace Timelapse.ControlsCore
{
    /// <summary>
    /// Shared implementation for Choice (ComboBox-based) controls.
    /// Contains all logic common to both DataEntryChoice and MetadataDataEntryFixedChoice.
    /// Works with ComboBox and WatermarkComboBox (which inherits from ComboBox).
    /// </summary>
    public class ChoiceControlCore
    {
        private readonly ComboBox control;

        // A value that isn't one of the menu's items is shown by selecting an extra item holding it, marked with this tag.
        // The item is hidden in the open menu, so it can't be chosen, and is removed when the value changes.
        private static readonly object NotInMenuTag = new();
        private ComboBoxItem notInMenuItem;

        public ChoiceControlCore(ComboBox control)
        {
            this.control = control;
            // The box's display field only exists once its template is applied, which may be after a value is first shown
            control.Loaded += (_, _) => UpdateDisplayStyle();
        }

        #region Values that aren't menu items
        // Selects the menu item with this value or, if the value isn't one of the menu's items, shows it in italics with an extra item.
        // An extra item already showing this value is kept, as replacing it would change the selection, which the control would then save or confirm again.
        // Returns true if a new extra item was needed.
        public bool SelectValue(string value)
        {
            if ((string)notInMenuItem?.Content != value)
            {
                RemoveNotInMenuItem();
            }
            bool wasShowingValueNotInMenu = notInMenuItem != null;

            // Setting ComboBox.Text on a non-editable ComboBox doesn't reliably select items by their content, especially for empty strings,
            // so the matching item is found and selected
            int matchingIndex = -1;
            for (int i = 0; i < control.Items.Count; i++)
            {
                if (control.Items[i] is ComboBoxItem cbi && (string)cbi.Content == value)
                {
                    matchingIndex = i;
                    break;
                }
            }

            if (matchingIndex >= 0)
            {
                control.SelectedIndex = matchingIndex;
            }
            else
            {
                // The box's text is the selected item's content, so the item has no search text of its own
                notInMenuItem = new ComboBoxItem { Content = value, Tag = NotInMenuTag, Height = 0 };
                control.Items.Add(notInMenuItem);
                control.SelectedIndex = control.Items.Count - 1;
            }
            UpdateDisplayStyle();
            return false == wasShowingValueNotInMenu && notInMenuItem != null;
        }

        public void RemoveNotInMenuItem()
        {
            if (notInMenuItem != null)
            {
                control.Items.Remove(notInMenuItem);
                notInMenuItem = null;
                UpdateDisplayStyle();
            }
        }

        // Whether this is the extra item that shows a value that isn't one of the menu's items
        public static bool IsNotInMenuItem(object item)
        {
            return item is ComboBoxItem { Tag: var tag } && ReferenceEquals(tag, NotInMenuTag);
        }

        public string Tooltip(string value)
        {
            return string.IsNullOrEmpty(value)
                ? "Blank entry"
                : notInMenuItem == null ? value : $"{value} (not one of the menu's items)";
        }

        private void UpdateDisplayStyle()
        {
            if (control is WatermarkComboBox { MainDisplayField: { } display })
            {
                display.FontStyle = notInMenuItem == null ? FontStyles.Normal : FontStyles.Italic;
            }
        }
        #endregion

        #region Content
        /// <summary>
        /// Gets the content as text
        /// </summary>
        public string GetContent()
        {
            return control.Text;
        }

        /// <summary>
        /// Tracks whether content has changed since last set
        /// </summary>
        public bool ContentChanged { get; set; }
        #endregion
    }
}
