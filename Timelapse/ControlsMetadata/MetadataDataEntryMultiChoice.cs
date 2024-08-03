using System.Windows.Controls;
using System.Windows;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Xceed.Wpf.Toolkit;

namespace Timelapse.ControlsMetadata
{
    // MultiChoice: A list of one or more item chosen from a menu. Comprises:
    // - a label containing the descriptive label) 
    // - a DoubleUpDownControl containing the content 
    public class MetadataDataEntryMultiChoice : MetadataDataEntryControl<CheckComboBox, Label>
    {
        #region Public Properties

        public override UIElement GetContentControl => this.ContentControl;

        public override bool IsContentControlEnabled => this.ContentControl.IsEnabled;

        /// <summary>Gets  the content of the note</summary>
        public override string Content => this.ContentControl.Text;

        public bool ContentChanged { get; set; }

        public override bool ContentReadOnly
        {
            // A hack, as the CheckComboBox does not contain an IsReadOnly field
            get => this.ContentControl.IsEditable;
            set
            {
                if (GlobalReferences.TimelapseState.IsViewOnly)
                {
                    this.ContentControl.IsEditable = false;
                    this.ContentControl.IsHitTestVisible = false;
                }
                else
                {
                    this.ContentControl.IsEditable = value;
                }
            }
        }
        #endregion

        #region Constructor
        public MetadataDataEntryMultiChoice(MetadataControlRow control, DataEntryControls styleProvider, string tooltip) :
            base(control, styleProvider, ControlContentStyleEnum.MultiChoiceComboBox, ControlLabelStyleEnum.DefaultLabel, tooltip)
        {
            // The behaviour of the combo box
            this.ContentControl.Focusable = true;
            //this.ContentControl.IsEditable = false;
            this.ContentControl.IsTextSearchEnabled = true;

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
                this.ContentControl.Items.Add(choice);
            }
            this.ContentControl.SelectedItem = control.DefaultValue;

            // Now configure the various elements
            this.ControlType = control.Type;
            this.ContentChanged = false;
        }
        #endregion

        #region Setting Content and Tooltip
        public override void SetContentAndTooltip(string value)
        {
            // For some reason, the empty item was not setting the selected index to the item with the blank entry. 
            // This is needed to set it explicitly.
            if (string.IsNullOrEmpty(value))
            {
                this.ContentControl.SelectedValue = string.Empty;
            }
            else
            {
                value = value.Trim();
            }

            // Set the choice to the value provided  
            // If the value is empty, we just make it the same as the tooltip so something meaningful is displayed..
            this.ContentControl.Text = value;
            this.ContentControl.ToolTip = string.IsNullOrEmpty(value) ? "Blank entry" : value;
        }
        #endregion
    }
}