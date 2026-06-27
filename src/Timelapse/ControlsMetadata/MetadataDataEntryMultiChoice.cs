using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.ControlsCore;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;
using TimelapseWpf.Toolkit;

namespace Timelapse.ControlsMetadata
{
    // MultiChoice: A list of one or more item chosen from a menu. Comprises:
    // - a label containing the descriptive label)
    // - a DoubleUpDownControl containing the content
    public class MetadataDataEntryMultiChoice : MetadataDataEntryControl<WatermarkCheckComboBox, Label>
    {
        private readonly MultiChoiceControlCore core;

        #region Public Properties
        public override UIElement GetContentControl => ContentControl;

        public override bool IsContentControlEnabled => ContentControl.IsEnabled;

        /// <summary>Gets the content of the note</summary>
        public override string Content => core.GetContent();

        public bool ContentChanged
        {
            get => core.ContentChanged;
            set => core.ContentChanged = value;
        }

        // WatermarkCheckComboBox uses IsEditable instead of IsReadOnly
        protected override bool GetContentControlReadOnly()
        {
            return core.ContentReadOnly;
        }

        protected override void SetContentControlReadOnly(bool isReadOnly)
        {
            if (GlobalReferences.TimelapseState.IsViewOnly)
            {
                ContentControl.IsEnabled = true;
            }
            ContentControl.IsEditable = isReadOnly;
            core.ContentReadOnly = isReadOnly;
        }
        #endregion

        #region Constructor
        public MetadataDataEntryMultiChoice(MetadataControlRow control, DataEntryControls styleProvider, string tooltip) :
            base(control, styleProvider, ControlContentStyleEnum.MultiChoiceComboBox, ControlLabelStyleEnum.DefaultLabel, tooltip)
        {
            // Create core shared implementation
            core = new MultiChoiceControlCore(ContentControl);
            // The behaviour of the combo box
            ContentControl.Focusable = true;
            //this.ContentControl.IsEditable = false;
            ContentControl.IsTextSearchEnabled = true;

            // Callback used to allow Enter to select the highlit item
            // this.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;

            // Note that empty choice specs in the Json are ignored, as deselecting
            // items is the same as an empty choice
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

            // Now configure the various elements
            ControlType = control.Type;
            ContentChanged = false;

            ContentControl.PreviewKeyDown += ContentCtl_PreviewKeyDown;
        }
        #endregion

        // Manages shortcut keys, and drop-down open/closing/navigation
        // Note that the dropdown selects the highlit item when a space is entered
        private void ContentCtl_PreviewKeyDown(object sender, KeyEventArgs keyEvent)
        {
            if (sender is not WatermarkCheckComboBox checkComboBox)
            {
                // Unlikely to happen
                return;
            }

            // Left/right: noop
            if (IsCondition.IsKeyLeftRightArrow(keyEvent.Key))
            {
                keyEvent.Handled = true;
                return;
            }

            // Up/Down arrow:
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
                //keyEvent.Handled = true;
            }

            if (keyEvent.Key == Key.Tab)
            {
                // Close the dropdown if its open, but don't handle the event
                DropdownSetStateIfNeeded(checkComboBox, DropDownState.Close);
            }
        }
        #region Setting Content and Tooltip
        public override void SetContentAndTooltip(string value)
        {
            // For some reason, the empty item was not setting the selected index to the item with the blank entry. 
            // This is needed to set it explicitly.
            if (string.IsNullOrEmpty(value))
            {
                ContentControl.SelectedValue = string.Empty;
            }
            else
            {
                value = value.Trim();
            }

            // Set the choice to the value provided  
            // If the value is empty, we just make it the same as the tooltip so something meaningful is displayed..
            ContentControl.Text = value;
            ContentControl.ToolTip = string.IsNullOrEmpty(value) ? "Blank entry" : value;
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
                    if (index > 0)
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