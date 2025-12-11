using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.ControlsCore;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;
using TimelapseWpf.Toolkit;

namespace Timelapse.ControlsMetadata
{
    // FixedChoice: Any single item chosen from a menu. Comprises:
    // - a label containing the descriptive label)
    // - a combobox containing the drop down menu and content
    public class MetadataDataEntryFixedChoice : MetadataDataEntryControl<WatermarkComboBox, Label>
    {
        private readonly ChoiceControlCore core;

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
        #endregion

        #region Constructor
        public MetadataDataEntryFixedChoice(MetadataControlRow control, DataEntryControls styleProvider, string tooltip) :
            base(control, styleProvider, ControlContentStyleEnum.ChoiceComboBox, ControlLabelStyleEnum.DefaultLabel, tooltip)
        {
            // Create core shared implementation
            core = new ChoiceControlCore(ContentControl);
            // The behaviour of the combo box
            ContentControl.Focusable = true;
            ContentControl.IsEditable = false;
            ContentControl.IsTextSearchEnabled = true;

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
            ContentControl.SelectedIndex = 0;

            // Now configure the various elements
            ControlType = control.Type;
            ContentChanged = false;
        }
        #endregion

        #region Event Handlers
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

            if (IsCondition.IsKeyLeftRightArrow(keyEvent.Key))
            {
                // ignore left/right arrow, as it otherwise cycles through items
                // which can be done accidentally
                keyEvent.Handled = true;
                return;
            }
            if (IsCondition.IsKeyReturnOrEnter(keyEvent.Key))
            {
                for (int i = 0; i < comboBox.Items.Count; i++)
                {
                    ComboBoxItem comboBoxItem = (ComboBoxItem)comboBox.ItemContainerGenerator.ContainerFromIndex(i);
                    if (comboBoxItem is { IsHighlighted: true })
                    {
                        comboBox.SelectedIndex = i;
                    }
                }
            }
        }
        #endregion

        #region Setting Content and Tooltip
        public override void SetContentAndTooltip(string value)
        {
            if (value == null)
            {
                return;
            }

            // For some reason, the empty item was not setting the selected index to the item with the blank entry. 
            // This is needed to set it explicitly.
            if (string.IsNullOrEmpty(value))
            {
                ContentControl.SelectedIndex = 0;
            }

            // Set the note to the value provided  
            // If the value is empty, we just make it the same as the tooltip so something meaningful is displayed..
            ContentControl.Text = value;
            ContentControl.ToolTip = string.IsNullOrEmpty(value) ? "Blank entry" : value;
        }
        #endregion
    }
}