using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Controls;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using TimelapseTemplateEditor.EditorCode;
using Xceed.Wpf.Toolkit;

namespace TimelapseTemplateEditor.Controls
{
    // The code below is primarily to allow  a user to to re-order  controls 
    // by dragging and dropping the chosen control's label as displayed in the preview panel 
    public partial class TemplateDataEntryPanelPreview
    {
        public TemplateDataEntryPanelPreview()
        {
            InitializeComponent();
            Globals.Root.dataGridBeingUpdatedByCode = false;
        }

        #region Public: Generate the controls to show in the preview panel
        public void GeneratePreviewControls(WrapPanel parent, DataTableBackedList<ControlRow> templateTable)
        {
            // used for styling all content and label controls except ComboBoxes since the combo box style is commented out in DataEntryControls.xaml
            // and defined instead in MainWindow.xaml as an exception workaround
            DataEntryControls styleProvider = new DataEntryControls();

            parent.Children.Clear();
            foreach (ControlRow control in templateTable)
            {
                // instantiate control UX objects
                StackPanel stackPanel;
                switch (control.Type)
                {
                    case Timelapse.Constant.Control.Note:
                    case Timelapse.Constant.DatabaseColumn.File:
                    case Timelapse.Constant.DatabaseColumn.RelativePath:
                        Label noteLabel = CreateLabel(styleProvider, control);
                        TextBox noteContent = CreateTextBox(styleProvider, control);
                        stackPanel = CreateStackPanel(styleProvider, noteLabel, noteContent);
                        break;
                    case Timelapse.Constant.Control.Counter:
                        RadioButton counterLabel = CreateCounterLabelButton(styleProvider, control);
                        IntegerUpDown counterContent = CreateIntegerUpDown(styleProvider, control);
                        stackPanel = CreateStackPanel(styleProvider, counterLabel, counterContent);
                        counterLabel.IsTabStop = false;
                        counterContent.GotFocus += this.Control_GotFocus;
                        counterContent.LostFocus += this.Control_LostFocus;
                        break;
                    case Timelapse.Constant.Control.Flag:
                    case Timelapse.Constant.DatabaseColumn.DeleteFlag:
                        Label flagLabel = CreateLabel(styleProvider, control);
                        CheckBox flagContent = this.CreateFlag(styleProvider, control);
                        flagContent.IsChecked = String.Equals(control.DefaultValue, Timelapse.Constant.BooleanValue.True, StringComparison.OrdinalIgnoreCase);
                        stackPanel = CreateStackPanel(styleProvider, flagLabel, flagContent);
                        break;
                    case Timelapse.Constant.Control.FixedChoice:
                        Label choiceLabel = CreateLabel(styleProvider, control);
                        ComboBox choiceContent = CreateComboBox(styleProvider, control);
                        stackPanel = CreateStackPanel(styleProvider, choiceLabel, choiceContent);
                        break;
                    case Timelapse.Constant.DatabaseColumn.DateTime:
                        Label dateTimeLabel = CreateLabel(styleProvider, control);
                        DateTimePicker dateTimeContent = this.CreateDateTimePicker(control);
                        stackPanel = CreateStackPanel(styleProvider, dateTimeLabel, dateTimeContent);
                        break;
                    default:
                        throw new NotSupportedException($"Unhandled control type {control.Type}.");
                }

                stackPanel.Tag = control.DataLabel;
                if (control.Visible == false)
                {
                    stackPanel.Visibility = Visibility.Collapsed;
                }

                // add control to wrap panel
                parent.Children.Add(stackPanel);
            }
        }
        #endregion

        #region Public static: Check control type
        // Standard controls are the ones required by Timelapse, eg. File, RelativePath, DateTime, DeleteFlag
        public static bool IsStandardControlType(string controlType)
        {
            return Timelapse.Constant.Control.StandardTypes.Contains(controlType);
        }
        #endregion

        #region Private Static: Create individual preview control types
        // Returns a stack panel containing two controls
        // The stack panel ensures that controls are layed out as a single unit with certain spatial characteristcs 
        // i.e.,  a given height, right margin, where contents will not be broken durring (say) panel wrapping
        private static StackPanel CreateStackPanel(DataEntryControls styleProvider, Control label, Control content)
        {
            StackPanel stackPanel = new StackPanel();
            stackPanel.Children.Add(label);
            stackPanel.Children.Add(content);

            Style style = styleProvider.FindResource(Timelapse.Constant.ControlStyle.ContainerStyle) as Style;
            stackPanel.Style = style;
            return stackPanel;
        }


        // Create Label
        private static Label CreateLabel(DataEntryControls styleProvider, ControlRow control)
        {
            Label label = new Label()
            {
                Content = control.Label,
                ToolTip = control.Tooltip,
                Style = styleProvider.FindResource(ControlLabelStyleEnum.DefaultLabel.ToString()) as Style
            };
            return label;
        }

        // Create TextBox
        private static TextBox CreateTextBox(DataEntryControls styleProvider, ControlRow control)
        {
            TextBox textBox = new TextBox()
            {
                Text = control.DefaultValue,
                ToolTip = control.Tooltip,
                Width = control.Width,
                Style = styleProvider.FindResource(ControlContentStyleEnum.NoteTextBox.ToString()) as Style
            };
            return textBox;
        }

        // Create IntegerUpDown
        private static IntegerUpDown CreateIntegerUpDown(DataEntryControls styleProvider, ControlRow control)
        {
            IntegerUpDown integerUpDown = new IntegerUpDown()
            {
                Text = control.DefaultValue,
                ToolTip = control.Tooltip,
                Minimum = 0,
                Width = control.Width + 18, // accounts for the width of the spinner
                DisplayDefaultValueOnEmptyText = true,
                DefaultValue = null,
                UpdateValueOnEnterKey = true,
                Style = styleProvider.FindResource(ControlContentStyleEnum.CounterTextBox.ToString()) as Style
            };
            return integerUpDown;
        }

        // Create CounterLabel Button
        private static RadioButton CreateCounterLabelButton(DataEntryControls styleProvider, ControlRow control)
        {
            RadioButton radioButton = new RadioButton()
            {
                GroupName = "DataEntryCounter",
                Content = control.Label,
                ToolTip = control.Tooltip,
                Style = styleProvider.FindResource(ControlLabelStyleEnum.CounterButton.ToString()) as Style
            };
            return radioButton;
        }

        // Create ComboBox
        private static ComboBox CreateComboBox(DataEntryControls styleProvider, ControlRow control)
        {
            ComboBox comboBox = new ComboBox()
            {
                ToolTip = control.Tooltip,
                Width = control.Width,
                Style = styleProvider.FindResource(ControlContentStyleEnum.ChoiceComboBox.ToString()) as Style
            };

            // Add items to the combo box
            Choices choices = Choices.ChoicesFromJson(control.List);
            if (choices.IncludeEmptyChoice)
            {
                comboBox.Items.Add(string.Empty);
                comboBox.Items.Add(new Separator());
            }
            foreach (string choice in choices.ChoiceList)
            {
                comboBox.Items.Add(choice);
            }

            // The displayed item shoudl be the control's currentdefault value
            comboBox.SelectedItem = control.DefaultValue;
            return comboBox;
        }
        #endregion

        #region Private Not Static: Create individual preview control types
        // Create DateTimePicker
        private DateTimePicker CreateDateTimePicker(ControlRow control)
        {
            DateTimePicker dateTimePicker = new DateTimePicker()
            {
                ToolTip = control.Tooltip,
                Width = control.Width,
                CultureInfo = System.Globalization.CultureInfo.CreateSpecificCulture("en-US")
            };
            DataEntryHandler.Configure(dateTimePicker, Timelapse.Constant.ControlDefault.DateTimeDefaultValue);
            dateTimePicker.GotFocus += this.Control_GotFocus;
            dateTimePicker.LostFocus += this.Control_LostFocus;
            return dateTimePicker;
        }

        // Create Flag
        private CheckBox CreateFlag(DataEntryControls styleProvider, ControlRow control)
        {
            CheckBox checkBox = new CheckBox()
            {
                Visibility = Visibility.Visible,
                ToolTip = control.Tooltip,
                Style = styleProvider.FindResource(ControlContentStyleEnum.FlagCheckBox.ToString()) as Style
            };
            checkBox.GotFocus += this.Control_GotFocus;
            checkBox.LostFocus += this.Control_LostFocus;
            return checkBox;
        }
        #endregion

        #region Private Callbacks for above not-static control types
        // Highlight control when it gets the focus (simulates aspects of tab control in Timelapse)
        private void Control_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                control.BorderThickness = new Thickness(Timelapse.Constant.Control.BorderThicknessHighlight);
                control.BorderBrush = Timelapse.Constant.Control.BorderColorHighlight;
            }
            else
            {
                TracePrint.StackTraceToOutput("Unexpected null");
            }
        }

        // Remove the highlight by restoring the original border appearance
        private void Control_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                control.BorderThickness = new Thickness(Timelapse.Constant.Control.BorderThicknessNormal);
                control.BorderBrush = Timelapse.Constant.Control.BorderColorNormal;
            }
            else
            {
                TracePrint.StackTraceToOutput("Unexpected null");
            }
        }
        #endregion

        #region Private Dragging and Dropping of Controls to Reorder them
        private void ControlsPanel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Equals(e.Source, this.ControlsPanel)) return;
            this.MouseDownWrapper(e);
        }

        private void ControlsPanel_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (Globals.MouseState.isMouseDown)
            {
                Point currentMousePosition = e.GetPosition(this.ControlsPanel);
                if ((Globals.MouseState.isMouseDragging == false) &&
                    ((Math.Abs(currentMousePosition.X - Globals.MouseState.mouseDownStartPosition.X) > SystemParameters.MinimumHorizontalDragDistance) ||
                     (Math.Abs(currentMousePosition.Y - Globals.MouseState.mouseDownStartPosition.Y) > SystemParameters.MinimumVerticalDragDistance)))
                {
                    this.MouseDraggingWrapper(e.Source as UIElement);
                    DragDrop.DoDragDrop(Globals.MouseState.dummyMouseDragSource, new DataObject("UIElement", e.Source, true), DragDropEffects.Move);
                }
            }
        }

        private void ControlsPanel_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.MouseReleaseWrapper();
        }


        private void ControlsPanel_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("UIElement"))
            {
                e.Effects = DragDropEffects.Move;
            }
        }

        private void ControlsPanel_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("UIElement"))
            {
                UIElement dropTarget = e.Source as UIElement;
                int control = 0;
                int dropTargetIndex = -1;
                foreach (UIElement element in this.ControlsPanel.Children)
                {
                    if (element.Equals(dropTarget))
                    {
                        dropTargetIndex = control;
                        break;
                    }

                    // Check if its a stack panel, and if so check to see if its children are the drop target
                    if (element is StackPanel stackPanel)
                    {
                        // Check the children...
                        foreach (UIElement subelement in stackPanel.Children)
                        {
                            if (subelement.Equals(dropTarget))
                            {
                                dropTargetIndex = control;
                                break;
                            }
                        }
                    }
                    control++;
                }
                if (dropTargetIndex != -1)
                {
                    // Check if the drag source is a stack panel, and if not reset it to the source's parent stack panel
                    this.MouseUpdateDragSourceToStackPanelParentIfNeeded();

                    // Reorder the control by removing it from its current location and inserting it at the new location
                    this.ControlsPanel.Children.Remove(Globals.MouseState.realMouseDragSource);
                    this.ControlsPanel.Children.Insert(dropTargetIndex, Globals.MouseState.realMouseDragSource);

                    // This then rebuilds everything depending on the order of those controls
                    Globals.RootEditor.TemplateDoUpdateControlOrder();
                }
                this.MouseReleaseWrapper();
            }
        }
        #endregion

        #region Private Utilities to set various Mouse States
        private void MouseDownWrapper(MouseButtonEventArgs e)
        {
            Globals.MouseState.isMouseDown = true;
            Globals.MouseState.mouseDownStartPosition = e.GetPosition(this.ControlsPanel);
        }

        private void MouseReleaseWrapper()
        {

            Globals.MouseState.isMouseDown = false;
            Globals.MouseState.isMouseDragging = false;
            // Note that ReleaseMouseCapture can be null
            Globals.MouseState.realMouseDragSource?.ReleaseMouseCapture();
        }

        private void MouseDraggingWrapper(UIElement uiElement)
        {
            Globals.MouseState.isMouseDragging = true;
            Globals.MouseState.realMouseDragSource = uiElement;
            if (Globals.MouseState.realMouseDragSource == null)
            {
                TracePrint.NullException();
                return;
            }
            Globals.MouseState.realMouseDragSource.CaptureMouse();
        }

        // If the drag source isn't a stack panel, set it to the source's parent stack panel
        private void MouseUpdateDragSourceToStackPanelParentIfNeeded()
        {
            if (!(Globals.MouseState.realMouseDragSource is StackPanel))
            {
                StackPanel parent = Utilities.FindVisualParent<StackPanel>(Globals.MouseState.realMouseDragSource);
                Globals.MouseState.realMouseDragSource = parent;
            }
        }
        #endregion
    }
}
