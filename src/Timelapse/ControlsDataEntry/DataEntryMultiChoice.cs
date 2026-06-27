using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Constant;
using Timelapse.ControlsCore;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;
using TimelapseWpf.Toolkit;
using Point = System.Windows.Point;

namespace Timelapse.ControlsDataEntry
{
    // A MultiChoice comprises a stack panel containing
    // - a label containing the descriptive label)
    // - a checkCombobox (containing the content) at the given width
    public class DataEntryMultiChoice : DataEntryControl<WatermarkCheckComboBox, Label>
    {
        private readonly MultiChoiceControlCore core;

        #region Public properties
        // Return the TopLeft corner of the content control as a point
        public override Point TopLeft => ContentControl.PointToScreen(new(0, 0));

        public override UIElement GetContentControl => ContentControl;

        public override bool IsContentControlEnabled => ContentControl.IsEnabled;

        /// <summary>Gets or sets the content of the choice.</summary>
        public override string Content => core.GetContent();

        public bool IgnoreSelectionChanged;

        // WatermarkCheckComboBox doesn't have IsReadOnly property, it uses IsEditable instead
        protected override bool GetContentControlReadOnly()
        {
            return core.ContentReadOnly;
        }

        protected override void SetContentControlReadOnly(bool isReadOnly)
        {
            if (GlobalReferences.TimelapseState.IsViewOnly)
            {
                ContentControl.IsEnabled = true;  // Special handling for MultiChoice
            }
            ContentControl.IsEditable = isReadOnly;
            core.ContentReadOnly = isReadOnly;
        }

        #endregion

        #region Constructor
        public DataEntryMultiChoice(ControlRow control, DataEntryControls styleProvider)
            : base(control, styleProvider, ControlContentStyleEnum.MultiChoiceComboBox, ControlLabelStyleEnum.DefaultLabel)
        {
            // Create core shared implementation
            core = new MultiChoiceControlCore(ContentControl);

            // Check the arguments for null
            ThrowIf.IsNullArgument(control, nameof(control));

            // The behaviour of the combo box
            ContentControl.Focusable = true;
            ContentControl.IsEditable = false;
            ContentControl.IsTextSearchEnabled = true;
            ContentControl.Watermark = Unicode.Ellipsis;

            ContentControl.PreviewKeyDown += ContentCtl_PreviewKeyDown;
            ContentControl.Opened += ContentControl_DropDownOpened;
            ContentControl.Closed += ContentControl_DropDownClosed;
            ContentControl.MouseUp += ContentControl_MouseUp;

            // Add items to the combo box. If we have an  EmptyChoiceItem, then  add an 'empty string' to the end 
            Choices choices = Choices.ChoicesFromJson(control.List);
            foreach (string choice in choices.ChoiceList)
            {
                if (string.IsNullOrWhiteSpace(choice))
                {
                    continue;
                }
                ContentControl.Items.Add(choice);
            }
            ContentControl.SelectedItem = control.DefaultValue;
        }

        private void ContentControl_MouseUp(object sender, MouseButtonEventArgs e)
        {
            ContentControl.Focus();
        }
        #endregion

        #region Event handlers: DropDown Open/Closed
        // In order to get the checkComboBox dropdown checkmarks to match its text,
        // We parse the text into an ObservableCollection and set its selected items to that.
        // ObservableCollection is required for the control to properly notify when items are checked/unchecked.
        private void ContentControl_DropDownOpened(object sender, RoutedEventArgs e)
        {
            IgnoreSelectionChanged = true;
            IntializeMenuFromCommaSeparatedList(ContentControl.Text);
            ObservableCollection<string> itemsList = new(this.ContentControl.Text.Split(','));
            this.ContentControl.SelectedItemsOverride = itemsList;
            IgnoreSelectionChanged = false;
        }

        private void ContentControl_DropDownClosed(object sender, RoutedEventArgs e)
        {
            // Clean up ObservableCollection immediately to prevent serialization errors during control cleanup
            // Setting SelectedItemsOverride to null clears the text, so save and restore it
            IgnoreSelectionChanged = true;
            string savedContent = ContentControl.Text;
            ContentControl.SelectedItemsOverride = null;
            ContentControl.Text = savedContent.Trim(',');
            IgnoreSelectionChanged = false;
            ContentControl.Focus();
        }
        #endregion

        #region EventHandlers: PreviewKeyDown
        protected override bool HandleKeyboardNavigationInBase()
        {
            return false; // We handle our own keyboard navigation
        }

        // Manages shortcut keys, and drop-down open/closing/navigation
        // Note that the dropdown selects the highlit item when a space is entered
        private void ContentCtl_PreviewKeyDown(object sender, KeyEventArgs keyEvent)
        {
            if (sender is not WatermarkCheckComboBox checkComboBox)
            {
                // Unlikely to happen
                return;
            }

            // Possible shortcut keys (delegated to main window):
            // - any Control key press could indicate a Shortcut key, and
            // - a few very specific keys that don't require a Control key press
            if (IsCondition.IsKeyControlDown() ||
                IsCondition.IsKeyPageUpDown(keyEvent.Key))
            {
                DropdownSetStateIfNeeded(checkComboBox, DropDownState.Close);
                DelegateKeyEventToMainWindow(keyEvent, true);
                return;
            }

            // Left/right without ctl: noop
            // Reminder: ctl-left/right handled before this
            if (IsCondition.IsKeyLeftRightArrow(keyEvent.Key))
            {
                keyEvent.Handled = true;
                return;
            }

            // Up/Down arrow without ctl:
            // Reminder: ctl-up/down handled previously)
            if (IsCondition.IsKeyUpDownArrow(keyEvent.Key))
            {
                // Down: open menu if closed
                // Up closes menu if at first item
                if (!checkComboBox.IsDropDownOpen)
                {
                    // Dropdown: not opened.
                    //  Down: force open menu if closed then navigate to first item
                    //  Up: noop
                    if (keyEvent.Key is Key.Down)
                    {
                        // Down
                        keyEvent.Handled = true;
                        DropdownSetStateIfNeeded(checkComboBox, DropDownState.Open);
                    }
                    else
                    {
                        // Up
                        keyEvent.Handled = true;
                    }
                    return;
                }

                // Dropdown: opened and on first item
                if (keyEvent.Key is Key.Up && IsFirstItemFocused(checkComboBox))
                {
                    // when Up is pressed at the first item, close the dropdown if its open 
                    keyEvent.Handled = true;
                    DropdownSetStateIfNeeded(checkComboBox, DropDownState.Close);
                    return;
                }
            }

            // Enter/Return closes the dropdown
            if (IsCondition.IsKeyReturnOrEnter(keyEvent.Key))
            {
                // Close the dropdown if its open
                DropdownSetStateIfNeeded(checkComboBox, DropDownState.Close);
                keyEvent.Handled = true;
            }
        }
        #endregion

        #region Public methods
        public void IntializeMenuFromCommaSeparatedList(string commaSeparatedList)
        {
            ObservableCollection<string> itemsList = new(commaSeparatedList.Split(','));
            ContentControl.SelectedItemsOverride = itemsList;
        }
        #endregion

        #region Setting Content and Tooltip
        // Set the Control's Content and Tooltip to the provided value
        public override void SetContentAndTooltip(string value)
        {
            // If the value is null, an ellipsis will be drawn in the checkbox (see Checkbox style)
            // Used to signify the indeterminate state in no or multiple selections in the overview.
            if (value == null)
            {
                ContentControl.ForceWatermark = true;
                ContentControl.ToolTip = "Select an item to change the " + Label + " for all selected images";
                return;
            }
            ContentControl.ForceWatermark = false;
            if (ContentControl.Text != value)
            {
                ContentControl.Text = value;

                ContentControl.ToolTip = string.IsNullOrEmpty(value) ? "Blank entry" : value;
            }
        }
        #endregion

        #region Visual Effects and Popup Previews
        // Flash the content area of the control
        public override void FlashContentControl(FlashEnum flashEnum)
        {
            if (ContentControl?.MainDisplayField is { } primaryDisplay)
            {
                primaryDisplay.Background = new SolidColorBrush(Colors.White);
                primaryDisplay.Background.BeginAnimation(SolidColorBrush.ColorProperty,
                    flashEnum == FlashEnum.UsePasteFlash
                        ? GetColorAnimationForPasting()
                        : GetColorAnimationWarning());
            }
        }

        public override void ShowPreviewControlValue(string value)
        {
            // Create the popup overlay
            if (PopupPreview == null)
            {
                // We want to expose the arrow on the choice menu, so subtract its width and move the horizontal offset over
                double arrowWidth = 20;
                double width = ContentControl.Width - arrowWidth;
                double horizontalOffset = -arrowWidth / 2;

                // Padding is used to align the text so it begins at the same spot as the control's text
                Thickness padding = new(5.5, 5, 0, 0);

                PopupPreview = CreatePopupPreview(ContentControl, padding, width, horizontalOffset);
            }
            // Show the popup
            ShowPopupPreview(value);
        }

        public override void HidePreviewControlValue()
        {
            HidePopupPreview();
        }

        public override void FlashPreviewControlValue()
        {
            FlashPopupPreview();
        }
        #endregion

        #region Helpers

        // Set the dropdown to the desired state if needed
        private static void DropdownSetStateIfNeeded(WatermarkCheckComboBox checkComboBox, DropDownState dropDownState)
        {
            // Open the dropdown if its closed
            if (dropDownState is DropDownState.Open && false == checkComboBox.IsDropDownOpen)
            {
                checkComboBox.IsDropDownOpen = true;
                return;
            }

            // Close the dropdown if its open
            if (dropDownState is DropDownState.Close && checkComboBox.IsDropDownOpen)
            {
                checkComboBox.IsDropDownOpen = false;
            }
        }

        private bool IsFirstItemFocused(WatermarkCheckComboBox checkComboBox)
        {
            // Try to find the popup and the items within it
            var popup = checkComboBox.Template?.FindName("PART_Popup", checkComboBox) as System.Windows.Controls.Primitives.Popup;
            if (popup?.Child == null)
            {
                return false;
            }

            // Find the ItemsPresenter within the popup
            var itemsPresenter = VisualChildren.GetVisualChild<ItemsPresenter>(popup.Child);
            if (itemsPresenter == null)
            {
                return false;
            }

            // Get the focused element
            var focusedElement = Keyboard.FocusedElement as DependencyObject;
            if (focusedElement == null)
            {
                return false;
            }

            // Walk up the visual tree to find the container (WatermarkCheckComboBoxItem or similar)
            var currentElement = focusedElement;
            while (currentElement != null)
            {
                // Check if we've reached the items presenter
                if (currentElement == itemsPresenter)
                {
                    break;
                }

                // Try to find the index of this element if it's an item container
                var parent = VisualTreeHelper.GetParent(currentElement);
                if (parent is Panel panel)
                {
                    // Check if this element is the first child
                    int index = panel.Children.IndexOf(currentElement as UIElement);
                    if (index == 0)
                    {
                        return true;
                    }
                    else if (index > 0)
                    {
                        return false;
                    }
                }

                currentElement = parent;
            }

            return false;
        }
        #endregion

        #region Enums
        protected enum DropDownState
        {
            Open,
            Close
        }
        #endregion
    }
}
