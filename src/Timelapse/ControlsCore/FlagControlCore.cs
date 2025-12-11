using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.Util;

namespace Timelapse.ControlsCore
{
    /// <summary>
    /// Shared implementation for Flag (CheckBox) controls.
    /// Contains all logic common to both DataEntryFlag and MetadataDataEntryFlag.
    /// </summary>
    public class FlagControlCore(CheckBox control)
    {
        #region Content and ReadOnly
        /// <summary>
        /// Gets the content as a boolean string ("true" or "false")
        /// </summary>
        public string GetContent()
        {
            return control.IsChecked != null && (bool)control.IsChecked
                ? BooleanValue.True
                : BooleanValue.False;
        }

        /// <summary>
        /// CheckBox doesn't have an IsReadOnly property, so we track it manually
        /// </summary>
        public bool ContentReadOnly { get; set; }

        #endregion

        #region Keyboard Navigation
        // Intercept keyboard navigation for Flag controls, as checkbox handling of some keys
        // are not quite the same as other controls. In particular, 
        // - arrow keys without the control key down are ignores (otherwise they would act as tabs)
        // Other key presses are then passed on to DataEntryControlBase
        public void HandleNavigationKeys(KeyEventArgs keyEvent, bool isDataEntry)
        {
            if (keyEvent.Key is Key.Right or Key.Left or Key.Up or Key.Down)
            {
                // Ignore left/right/up/down arrow keys (which othewise tabs)
                // unless its a DataEntryControl with the control key is pressed, in which case its handled elsewhere
                keyEvent.Handled = true;

                // Control key press indicates a Shortcut key, handled by the main window 
                // Possible shortcut keys (delegated to main window):
                // - any Control key press could indicate a Shortcut key, and
                // - a few very specific keys that don't require a Control key press
                if (isDataEntry && 
                    (IsCondition.IsKeyControlDown() ||
                     IsCondition.IsKeyPageUpDown(keyEvent.Key)))
                {
                    GlobalReferences.MainWindow.Handle_PreviewKeyDown(keyEvent, true);
                }
            }
        }
        #endregion
    }
}
