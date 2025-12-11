using System;
using System.Collections.Generic;
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
    // A FixedChoice comprises a stack panel containing
    // - a label containing the descriptive label)
    // - a combobox (containing the content) at the given width
    public class DataEntryChoice : DataEntryControl<WatermarkComboBox, Label>
    {
        private readonly ChoiceControlCore core;

        #region Public properties
        // Return the TopLeft corner of the content control as a point
        public override Point TopLeft => ContentControl.PointToScreen(new(0, 0));

        public override UIElement GetContentControl => ContentControl;

        public override bool IsContentControlEnabled => ContentControl.IsEnabled;

        /// <summary>Gets or sets the content of the choice.</summary>
        public override string Content => core.GetContent();
        #endregion

        #region Constructor
        public DataEntryChoice(ControlRow control, DataEntryControls styleProvider)
            : base(control, styleProvider, ControlContentStyleEnum.ChoiceComboBox, ControlLabelStyleEnum.DefaultLabel)
        {
            // Create core shared implementation
            core = new ChoiceControlCore(ContentControl);

            // Check the arguments for null
            ThrowIf.IsNullArgument(control, nameof(control));

            // The behaviour of the combo box
            ContentControl.Focusable = true;
            ContentControl.IsEditable = false;
            ContentControl.IsTextSearchEnabled = true;
            ContentControl.Watermark = Unicode.Ellipsis;

            if (GlobalReferences.TimelapseState.IsViewOnly)
            {
                ContentControl.IsReadOnly = true;
                ContentControl.IsHitTestVisible = false;
            }

            // Callback used to allow Enter to select the highlit item
            ContentControl.PreviewKeyDown += ContentCtl_PreviewKeyDown;

            // Add items to the combo box. If we have an  EmptyChoiceItem, then  add an 'empty string' to the end 
            ComboBoxItem cbi;
            Choices choices = Choices.ChoicesFromJson(control.List);
            foreach (string choice in choices.ChoiceList)
            {
                cbi = new()
                {
                    Content = choice
                };
                ContentControl.Items.Add(cbi);
            }
            if (choices.IncludeEmptyChoice)
            {
                // put empty choice / separator at the beginning of the control

                cbi = new()
                {
                    Content = string.Empty
                };
                ContentControl.Items.Insert(0, new Separator());
                ContentControl.Items.Insert(0, cbi);
            }
        }
        #endregion

        #region Event Handlers
        // Note: DataEntryControlContainer_PreviewKeyDown will handle all the basic shortcut keys eg, left,right,up,down etc

        // Users may want to use the text search facility on the combobox, where they type the first letter and then enter
        // For some reason, it wasn't working on pressing 'enter' so this handler does that.
        // Whenever a return or enter is detected on the combobox, it finds the highlight item (i.e., that is highlit from the text search)
        // and sets the combobox to that value.
        private void ContentCtl_PreviewKeyDown(object sender, KeyEventArgs keyEvent)
        {
            if (sender is not ComboBox comboBox)
            {
                return;
            }

            // Prevent Left/Right arrow keys from changing ComboBox selection
            // These keys are used for image navigation in TimelapseWindow
            // Allow Ctrl+Left/Right and Shift+Left/Right to pass through for other handlers
            if (IsCondition.IsKeyLeftRightArrow(keyEvent.Key) &&
                !(IsCondition.IsKeyControlDown() || IsCondition.IsKeyShiftDown()))
            {
                keyEvent.Handled = true;
                return;
            }

            // When dropdown is open and Tab/Shift+Tab is pressed, ignore it (no-op)
            if (keyEvent.Key is Key.Tab && comboBox.IsDropDownOpen)
            {
                // Mark as handled to prevent tab navigation through menu items
                keyEvent.Handled = true;
                return;
            }

            // We don't want to update the 
            if (IsCondition.IsKeyReturnOrEnter(keyEvent.Key))
            {
                Keyboard.Focus(ContentControl);
                GlobalReferences.MainWindow.TrySetKeyboardFocusToMarkableCanvas(false, keyEvent);
                keyEvent.Handled = true;
            }
           

            // Depending on the key event, this pass the key to the main window 
            base.Container_PreviewKeyDown(sender, keyEvent);
        }
        #endregion

        #region Setting Content and Tooltip
        // Set the Control's Content and Tooltip to the provided value
        public override void SetContentAndTooltip(string value)
        {
            // If the value is null, an ellipsis watermark appear in the checkbox
            // Used to signify the indeterminate state in no or multiple selections in the overview.
            if (value == null)
            {
                ContentControl.ForceWatermark = true;
                ContentControl.ToolTip = "Select an item to change the " + Label + " for all selected images";
                return;
            }
            ContentControl.ForceWatermark = false;
            ContentControl.Text = value;
            ContentControl.ForceWatermark = false;
            ContentControl.ToolTip = string.IsNullOrEmpty(value) ? "Blank entry" : value;
        }

        #endregion

        #region Hiding, visual Effects and Popup Previews

        // ReSharper disable once UnusedMember.Global
        public void HideItems(List<String> itemsToHide)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (ContentControl == null || ContentControl.Items == null || itemsToHide == null)
            {
                return;
            }
            foreach (ComboBoxItem cbi in ContentControl.Items)
            {
                if (itemsToHide.Contains((string)cbi.Content))
                {
                    cbi.Height = 0;
                }
            }
        }

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
            // Create the popup overlay if it hasn't already been created
            if (PopupPreview == null)
            {
                // We want to expose the arrow on the choice menu, so subtract its width and move the horizontal offset over
                double arrowWidth = 20;
                double width = ContentControl.Width - arrowWidth;
                double horizontalOffset = -arrowWidth / 2;

                // Padding is used to align the text so it begins at the same spot as the control's text
                Thickness padding = new(5.5, 6, 0, 0);

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
    }
}
