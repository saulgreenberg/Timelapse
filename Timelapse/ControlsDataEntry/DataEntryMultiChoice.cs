using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;
using Xceed.Wpf.Toolkit.Primitives;

namespace Timelapse.ControlsDataEntry
{
    // A MultiChoice comprises a stack panel containing
    // - a label containing the descriptive label) 
    // - a checkCombobox (containing the content) at the given width
    public class DataEntryMultiChoice : DataEntryControl<CheckComboBox, Label>
    {
        #region Public properties
        // Return the TopLeft corner of the content control as a point
        public override Point TopLeft => ContentControl.PointToScreen(new Point(0, 0));

        public override UIElement GetContentControl => ContentControl;

        public override bool IsContentControlEnabled => ContentControl.IsEnabled;

        /// <summary>Gets or sets the content of the choice.</summary>
        public override string Content => ContentControl.Text;

        public override bool ContentReadOnly
        {
            // A hack, as the CheckComboBox does not contain an IsReadOnly field
            get => ContentControl.IsEditable;
            set
            {
                if (GlobalReferences.TimelapseState.IsViewOnly)
                {
                    ContentControl.IsEnabled = true;
                    ContentControl.IsHitTestVisible = false;
                }
                else
                {
                    ContentControl.IsEditable = value;
                }
            }
        }
        #endregion

        #region Constructor
        public DataEntryMultiChoice(ControlRow control, DataEntryControls styleProvider)
            : base(control, styleProvider, ControlContentStyleEnum.MultiChoiceComboBox, ControlLabelStyleEnum.DefaultLabel)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(control, nameof(control));

            // The behaviour of the combo box
            ContentControl.Focusable = true;
            ContentControl.IsEditable = false;
            ContentControl.IsTextSearchEnabled = true;

            ContentControl.ItemSelectionChanged += ContentControl_ItemSelectionChanged;
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
        // We parse the text into an observable collection and set its selected items to that.
        // However, using an observable collection leads to issues when we clear the DataEntryPanel on closing an image set,
        // To fix that, we remove the observable collection when the drop down is closed.
        private void ContentControl_DropDownOpened(object sender, RoutedEventArgs e)
        {
            SetMenuToList(ContentControl.Text);
            //ObservableCollection<string> itemsList = new ObservableCollection<string>(this.ContentControl.Text.Split(',').ToList());
            //this.ContentControl.SelectedItemsOverride = itemsList;
        }

        private void ContentControl_DropDownClosed(object sender, RoutedEventArgs e)
        {
            // As setting the override to null clears the content control text, we save and then restore the content control tex.
            string savedContent = ContentControl.Text;
            ContentControl.SelectedItemsOverride = null;
            ContentControl.Text = savedContent;
            ContentControl.Focus();
        }
        #endregion

        #region Event Handlers: ItemSelectionChanged
        private void ContentControl_ItemSelectionChanged(object sender, ItemSelectionChangedEventArgs e)
        {
            // Invoked:
            // - every time a dropdown is open, for each checked item (yes, somewhat inefficient but...),
            // - when a dropdown checkbox is checked or unchecked, for that particular item
            if (sender is CheckComboBox checkComboBox && checkComboBox.SelectedItemsOverride != null)
            {
                // Parse the current checkComboBox items a text string to update the checkComboBox text as needed
                List<string> list = new List<string>();
                foreach (string item in checkComboBox.SelectedItemsOverride)
                {
                    if (item != Unicode.Ellipsis)
                    {
                        list.Add(item);
                    }
                }
                list.Sort();
                string newText = string.Join(",", list).Trim(',');
                if (checkComboBox.Text != newText)
                {
                    checkComboBox.Text = newText;
                }
            }
        }
        #endregion

        #region EventHandlers: PreviewKeyDow
        // Users may want to use the text search facility on the combobox, where they type the first letter and then enter
        // For some reason, it wasn't working on pressing 'enter' so this handler does that.
        // Whenever a return or enter is detected on the combobox, it finds the highlight item (i.e., that is highlit from the text search)
        // and sets the combobox to that value.
        private void ContentCtl_PreviewKeyDown(object sender, KeyEventArgs keyEvent)
        {
            if (keyEvent.Key == Key.Right || keyEvent.Key == Key.Left || keyEvent.Key == Key.PageUp || keyEvent.Key == Key.PageDown)
            {
                // the right/left arrow keys normally cycle through the menu items.
                // However, we want to retain the arrow keys - as well as the PageUp/Down keys - for cycling through the image.
                // So we mark the event as handled, and we cycle through the images anyways.
                // Note that redirecting the event to the main window, while prefered, won't work
                // as the main window ignores the arrow keys if the focus is set to a control.
                keyEvent.Handled = true;
                GlobalReferences.MainWindow.Handle_PreviewKeyDown(keyEvent, true);
            }
            else
            {
                if (!(sender is CheckComboBox checkComboBox))
                {
                    return;
                }
                if (keyEvent.Key == Key.Return || keyEvent.Key == Key.Enter)
                {
                    checkComboBox.IsDropDownOpen = false;
                }
            }
        }
        #endregion


        public void SetMenuToList(string commaSeparatedList)
        {
            ObservableCollection<string> itemsList = new ObservableCollection<string>(commaSeparatedList.Split(',').ToList());
            ContentControl.SelectedItemsOverride = itemsList;
        }

        #region Setting Content and Tooltip
        // Set the Control's Content and Tooltip to the provided value
        public override void SetContentAndTooltip(string value)
        {
            // If the value is null, an ellipsis will be drawn in the checkbox (see Checkbox style)
            // Used to signify the indeterminate state in no or multiple selections in the overview.
            if (value == null)
            {
                ContentControl.Text = Unicode.Ellipsis;
                ContentControl.ToolTip = "Select an item to change the " + Label + " for all selected images";
                return;
            }
            value = value.Trim();


            if (ContentControl.Text != value)
            {
                ContentControl.Text = value;

                ContentControl.ToolTip = string.IsNullOrEmpty(value) ? "Blank entry" : value;
            }
        }
        #endregion

        #region Visual Effects and Popup Previews

        // ReSharper disable once UnusedMember.Global
        //public void HideItems(List<String> itemsToHide)
        //{
        //    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        //    if (this.ContentControl == null || this.ContentControl.Items == null || itemsToHide == null)
        //    {
        //        return;
        //    }
        //    foreach (ComboBoxItem cbi in this.ContentControl.Items)
        //    {
        //        if (itemsToHide.Contains((string)cbi.Content))
        //        {
        //            cbi.Height = 0;
        //        }
        //    }
        //}

        // Flash the content area of the control
        public override void FlashContentControl()
        {
            Border contentHost = (Border)ContentControl.Template.FindName("PART_Border", ContentControl);
            if (contentHost != null)
            {
                TextBlock tb = VisualChildren.GetVisualChild<TextBlock>(contentHost);
                if (tb != null)
                {
                    tb.Background = new SolidColorBrush(Colors.White);
                    tb.Background.BeginAnimation(SolidColorBrush.ColorProperty, GetColorAnimation());
                }
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
                Thickness padding = new Thickness(5.5, 6.5, 0, 0);

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
