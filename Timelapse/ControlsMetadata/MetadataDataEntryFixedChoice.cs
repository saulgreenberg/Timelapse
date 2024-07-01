using System.Windows.Controls;
using System.Windows;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using System.Windows.Input;
namespace Timelapse.ControlsMetadata
{
    // FixedChoice: Any single item chosen from a menu. Comprises:
    // - a label containing the descriptive label) 
    // - a combobox containing the drop down menu and content 
    public class MetadataDataEntryFixedChoice : MetadataDataEntryControl<ComboBox, Label>
    {
        #region Public Properties

        public override UIElement GetContentControl => this.ContentControl;

        public override bool IsContentControlEnabled => this.ContentControl.IsEnabled;

        /// <summary>Gets  the content of the note</summary>
        public override string Content => this.ContentControl.Text;

        public bool ContentChanged { get; set; }

        public override bool ContentReadOnly
        {
            get => this.ContentControl.IsReadOnly;
            set
            {
                if (GlobalReferences.TimelapseState.IsViewOnly)
                {
                    this.ContentControl.IsReadOnly = true;
                    this.ContentControl.IsHitTestVisible = false;
                }
                else
                {
                    this.ContentControl.IsReadOnly = value;
                }
            }
        }
        #endregion

        #region Constructor
        public MetadataDataEntryFixedChoice(MetadataControlRow control, DataEntryControls styleProvider, string tooltip) :
            base(control, styleProvider, ControlContentStyleEnum.ChoiceComboBox, ControlLabelStyleEnum.DefaultLabel, tooltip)
        {
            // The behaviour of the combo box
            this.ContentControl.Focusable = true;
            this.ContentControl.IsEditable = false;
            this.ContentControl.IsTextSearchEnabled = true;

            // Callback used to allow Enter to select the highlit item
            this.ContentControl.PreviewKeyDown += this.ContentCtl_PreviewKeyDown;

            // Add items to the combo box. If we have an  EmptyChoiceItem, then  add an 'empty string' to the end 
            ComboBoxItem cbi;
            Choices choices = Choices.ChoicesFromJson(control.List);
            foreach (string choice in choices.ChoiceList)
            {
                cbi = new ComboBoxItem()
                {
                    Content = choice
                };
                this.ContentControl.Items.Add(cbi);
            }
            if (choices.IncludeEmptyChoice)
            {
                // put empty choice / separator at the beginning of the control

                cbi = new ComboBoxItem()
                {
                    Content = string.Empty
                };
                this.ContentControl.Items.Insert(0, new Separator());
                this.ContentControl.Items.Insert(0, cbi);
            }

            // We include an invisible ellipsis menu item. This allows us to display an ellipsis in the combo box text field
            // when multiple images with different values are selected
            //cbi = new ComboBoxItem()
            //{
            //    Content = Constant.Unicode.Ellipsis
            //};
            //this.ContentControl.Items.Insert(0, cbi);
            //((ComboBoxItem)this.ContentControl.Items[0]).Visibility = Visibility.Collapsed;
            this.ContentControl.SelectedIndex = 0;


            // Now configure the various elements
            this.Tooltip = tooltip;
            this.ControlType = control.Type;
            this.ContentChanged = false;
        }
        #endregion

        #region Event Handlers
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
                ComboBox comboBox = sender as ComboBox;
                if (comboBox == null)
                {
                    return;
                }
                if (keyEvent.Key == Key.Return || keyEvent.Key == Key.Enter)
                {
                    for (int i = 0; i < comboBox.Items.Count; i++)
                    {
                        ComboBoxItem comboBoxItem = (ComboBoxItem)comboBox.ItemContainerGenerator.ContainerFromIndex(i);
                        if (comboBoxItem != null && comboBoxItem.IsHighlighted)
                        {
                            comboBox.SelectedValue = comboBoxItem.Content.ToString();
                        }
                    }
                }
                else if (keyEvent.Key == Key.Up || keyEvent.Key == Key.Down || keyEvent.Key == Key.Home)
                {
                    // Because we have inserted an invisible ellipses into the list, we have to skip over it when a 
                    // user navigates the combobox with the keyboard using the arrow keys
                    if (keyEvent.Key == Key.Up && (comboBox.SelectedIndex == 0 || comboBox.SelectedIndex == -1))
                    {
                        // If the user tries to navigate beyound the beginning of the list, keep it on the first valid item
                        if (comboBox.SelectedIndex == -1)
                        {
                            comboBox.SelectedIndex = 0;
                        }

                        keyEvent.Handled = true;
                    }
                    else if (keyEvent.Key == Key.Down && (comboBox.SelectedIndex == comboBox.Items.Count - 1 ||
                                                          comboBox.SelectedIndex == -1))
                    {
                        // If the user tries to navigate beyond the end of the list, keep it on the last valid item
                        // But the -1 should only be triggered to go back to the beginning
                        if (comboBox.SelectedIndex == -1)
                        {
                            comboBox.SelectedIndex = 0;
                        }

                        keyEvent.Handled = true;
                    }
                    else if (keyEvent.Key == Key.Home)
                    {
                        // Key.Home - go to the first item.
                        comboBox.SelectedIndex = 0;
                        keyEvent.Handled = true;
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
                this.ContentControl.SelectedIndex = 0;
            }

            // Set the note to the value provided  
            // If the value is empty, we just make it the same as the tooltip so something meaningful is displayed..
            this.ContentControl.Text = value;
            this.ContentControl.ToolTip = string.IsNullOrEmpty(value) ? "Blank entry" : value;

            //// Set the note to the value provided  
            //// If the value is empty, we just make it the same as the tooltip so something meaningful is displayed..
            //this.ContentChanged = this.ContentControl.Text != value;
            //this.ContentControl.Text = value;
            //this.ContentControl.ToolTip = string.IsNullOrEmpty(value) ? this.LabelControl.ToolTip : value;
        }
        #endregion
    }
}