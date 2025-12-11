using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.ControlsCore;
using Timelapse.ControlsDataEntry;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Util;
using TimelapseTemplateEditor.EditorCode;
using TimelapseWpf.Toolkit;
using Control = Timelapse.Constant.Control;

namespace TimelapseTemplateEditor.ControlsMetadata
{
    /// <summary>
    /// Interaction logic for MetadataEntryPreviewPanel.xaml
    /// </summary>
    public partial class MetadataEntryPreviewPanel
    {
        public MetadataTabControl ParentTab { get; set; }

        #region Constructor
        public MetadataEntryPreviewPanel()
        {
            InitializeComponent();
            if (Globals.Root?.dataGridBeingUpdatedByCode != null)
            {
                Globals.Root.dataGridBeingUpdatedByCode = false;
            }
        }
        #endregion

        #region Public: Generate the controls to show in the preview panel
        public void GeneratePreviewControls(int level)
        {
            // Always clear the children 
            // This clears things if this is invoked after all rows have been removed
            // and prepares things if things have been changed or added
            ControlsPanel.Children.Clear();

            // Return if no data for that level exists. 
            // e.g., when a new level is just being created, or when a level has no controls or no data is associated with it,
            if (null == Globals.Root?.templateDatabase?.MetadataControlsByLevel || false == Globals.Root.templateDatabase.MetadataControlsByLevel.ContainsKey(level))
            {
                return;
            }

            // used for styling all content and label controls except ComboBoxes since the combo box style is commented out in DataEntryControls.xaml
            // and defined instead in MainWindow.xaml as an exception workaround
            DataEntryControls styleProvider = new();
            DataTableBackedList<MetadataControlRow> metadataTable = Globals.Root.templateDatabase.MetadataControlsByLevel[level];
            int row = 0;
            foreach (MetadataControlRow control in metadataTable)
            {
                // instantiate control UX objects
                Grid grid;
                bool isPositive;
                switch (control.Type)
                {
                    // These don't exist as metadata controls, so we can omit them
                    // DatabaseColumn.File:
                    // DatabaseColumn.RelativePath:
                    // DatabaseColumn.DateTime
                    // Control.Count
                    // DatabaseColumn.DeleteFlag
                    case Control.Note:
                        Label noteLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        ImprintTextBox noteContent = PreviewControlCommon.CreateTextBox(styleProvider, control, ControlTypeEnum.MetadataControl);
                        noteContent.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_HandleKeyDownForEnter;
                        grid = PreviewControlCommon.CreateGrid(styleProvider, noteLabel, noteContent);
                        break;
                    case Control.AlphaNumeric:
                        Label alphanumericLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        ImprintTextBox alphanumericContent = PreviewControlCommon.CreateTextBox(styleProvider, control, ControlTypeEnum.MetadataControl);
                        alphanumericContent.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_TextBoxNoSpaces;
                        alphanumericContent.PreviewTextInput += Timelapse.Util.ValidationCallbacks.PreviewInput_AlphaNumericCharacterOnly;
                        alphanumericContent.TextChanged += Timelapse.Util.ValidationCallbacks.TextChanged_AlphaNumericTextOnly;
                        DataObject.AddPastingHandler(alphanumericContent, Timelapse.Util.ValidationCallbacks.Paste_OnlyIfAlphaNumeric);
                        grid = PreviewControlCommon.CreateGrid(styleProvider, alphanumericLabel, alphanumericContent);
                        break;
                    case Control.MultiLine:
                        Label multiLineLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        MultiLineText multiLineContent = PreviewControlCommon.CreateMultiLine(styleProvider, control, ControlTypeEnum.MetadataControl);
                        multiLineContent.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_HandleKeyDownForEnter;
                        grid = PreviewControlCommon.CreateGrid(styleProvider, multiLineLabel, multiLineContent);
                        break;

                    case Control.IntegerPositive:
                    case Control.IntegerAny:
                        Label integerLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        isPositive = control.Type == Control.IntegerPositive;
                        // Last argument sets the integerUpDown to positive only if needed
                        IntegerUpDown integerContent = PreviewControlCommon.CreateIntegerUpDown(styleProvider, control, ControlTypeEnum.MetadataControl, isPositive);
                        integerContent.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_IntegerUpDownNoSpaces;
                        if (control.Type == Control.IntegerAny)
                        {
                            integerContent.PreviewTextInput += Timelapse.Util.ValidationCallbacks.PreviewInput_IntegerCharacterOnly;
                            DataObject.AddPastingHandler(integerContent, Timelapse.Util.ValidationCallbacks.Paste_OnlyIfIntegerAny);
                        }
                        else
                        {
                            integerContent.PreviewTextInput += Timelapse.Util.ValidationCallbacks.PreviewInput_IntegerPositiveCharacterOnly;
                            DataObject.AddPastingHandler(integerContent, Timelapse.Util.ValidationCallbacks.Paste_OnlyIfIntegerPositive);
                        }
                        grid = PreviewControlCommon.CreateGrid(styleProvider, integerLabel, integerContent);
                        integerLabel.IsTabStop = false;
                        integerContent.GotFocus += ControlsDataHelpers.Control_GotFocus; 
                        integerContent.LostFocus += ControlsDataHelpers.Control_LostFocus;
                        break;
                    case Control.DecimalAny:
                    case Control.DecimalPositive:
                        Label decimalLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        isPositive = control.Type == Control.DecimalPositive;
                        DoubleUpDown decimalContent = PreviewControlCommon.CreateDoubleUpDown(styleProvider, control, ControlTypeEnum.MetadataControl, isPositive);
                        decimalContent.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_DecimalUpDownNoSpaces;
                        if (control.Type == Control.DecimalAny)
                        {
                            decimalContent.PreviewTextInput += Timelapse.Util.ValidationCallbacks.PreviewInput_DecimalCharacterOnly;
                            DataObject.AddPastingHandler(decimalContent, Timelapse.Util.ValidationCallbacks.Paste_OnlyIfDecimalAny);
                        }
                        else
                        {
                            decimalContent.PreviewTextInput += Timelapse.Util.ValidationCallbacks.PreviewInput_DecimalPositiveCharacterOnly;
                            DataObject.AddPastingHandler(decimalContent, Timelapse.Util.ValidationCallbacks.Paste_OnlyIfDecimalPositive);
                        }
                        grid = PreviewControlCommon.CreateGrid(styleProvider, decimalLabel, decimalContent);
                        decimalLabel.IsTabStop = false;
                        decimalContent.GotFocus += ControlsDataHelpers.Control_GotFocus; 
                        decimalContent.LostFocus += ControlsDataHelpers.Control_LostFocus;
                        break;

                    case Control.Flag:
                        Label flagLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        CheckBox flagContent = CreateControls.CreateFlag(styleProvider, control);
                        flagContent.IsChecked = String.Equals(control.DefaultValue, BooleanValue.True, StringComparison.OrdinalIgnoreCase);
                        flagContent.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_HandleKeyDownForEnter; 
                        grid = PreviewControlCommon.CreateGrid(styleProvider, flagLabel, flagContent);
                        break;

                    case Control.FixedChoice:
                        Label choiceLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        ComboBox choiceContent = PreviewControlCommon.CreateComboBox(styleProvider, control, ControlTypeEnum.MetadataControl);
                        choiceContent.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_HandleKeyDownForEnter; 
                        grid = PreviewControlCommon.CreateGrid(styleProvider, choiceLabel, choiceContent);
                        break;
                    case Control.MultiChoice:
                        Label multiChoiceLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        WatermarkCheckComboBox multiChoiceContent = PreviewControlCommon.CreateMultiChoiceComboBox(styleProvider, control, ControlTypeEnum.MetadataControl);
                        multiChoiceContent.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_HandleKeyDownForEnter;
                        multiChoiceContent.Opened += ControlsDataHelpers.WatermarkCheckComboBox_DropDownOpened;
                        multiChoiceContent.Closed += ControlsDataHelpers.WatermarkCheckComboBox_DropDownClosed;
                        multiChoiceContent.ItemSelectionChanged += ControlsDataHelpers.MultiChoice_ItemSelectionChanged;
                        multiChoiceContent.Text = control.DefaultValue;
                        grid = PreviewControlCommon.CreateGrid(styleProvider, multiChoiceLabel, multiChoiceContent);
                        break;

                    case Control.DateTime_:
                        Label dateTimeCustomLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        WatermarkDateTimePicker dateTimeCustomContent = CreateControls.CreateWatermarkDateTimePicker(control, DateTimeFormatEnum.DateAndTime,
                            DateTimeHandler.TryParseDatabaseDateTime(control.DefaultValue, out DateTime dateTime)
                                ? dateTime
                                : ControlDefault.DateTimeCustomDefaultValue);
                        dateTimeCustomContent.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_HandleKeyDownForEnter;
                        grid = PreviewControlCommon.CreateGrid(styleProvider, dateTimeCustomLabel, dateTimeCustomContent);
                        break;
                    case Control.Date_:
                        Label dateLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        WatermarkDateTimePicker dateContent = CreateControls.CreateWatermarkDateTimePicker(control, DateTimeFormatEnum.DateOnly,
                            DateTimeHandler.TryParseDatabaseDate(control.DefaultValue, out DateTime date)
                                ? date
                                : ControlDefault.Date_DefaultValue);
                        dateContent.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_HandleKeyDownForEnter;
                        grid = PreviewControlCommon.CreateGrid(styleProvider, dateLabel, dateContent);
                        break;
                    case Control.Time_:
                        Label timeLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        TimePicker timeContent = CreateControls.CreateWatermarkTimePicker(control,
                            DateTimeHandler.TryParseDatabaseTime(control.DefaultValue, out DateTime time)
                                ? time
                                : ControlDefault.Time_DefaultValue);
                        timeContent.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_HandleKeyDownForEnter;
                        grid = PreviewControlCommon.CreateGrid(styleProvider, timeLabel, timeContent);
                        break;
 
                    default:
                        throw new NotSupportedException($"Unhandled control type {control.Type}.");
                }

                grid.Tag = control.DataLabel;
                if (control.Visible == false)
                {
                    grid.Visibility = Visibility.Collapsed;
                }
                ControlsPanel.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Auto) });
                Grid.SetRow(grid, row++);
                ControlsPanel.Children.Add(grid);
            }

            // Rejig our controls so they are set up for displaying one per line, along with
            // a description derived from the tooltip. To do so:
            // - right adjusts the labels with each having the same width, so we need to find the maximum width across labels
            // - then add the control, each with the same width, so we need to find the maximum width across controls.
            // - decrease the vertical spacing between controls
            // TODO: Maybe just use a fixed width?
            // TODO: Error check if VisualChild is null?
            double maxColumn1Width = 0;
            double maxColumn2Width = 0;
            foreach (Grid child in ControlsPanel.Children)
            {
                Label label = VisualChildren.GetVisualChild<Label>(child);
                label.Measure(new(double.PositiveInfinity, double.PositiveInfinity));
                maxColumn1Width = Math.Max(label.DesiredSize.Width, maxColumn1Width);

                System.Windows.Controls.Control thisControl = (System.Windows.Controls.Control)child.Children[1];
                thisControl.Measure(new(double.PositiveInfinity, double.PositiveInfinity));
                maxColumn2Width = Math.Max(thisControl.DesiredSize.Width, maxColumn2Width);
            }

            // Now we resize each label to the maximum width and right adjust them
            foreach (Grid child in ControlsPanel.Children)
            {
                // decrease the vertical spacing between controls
                child.Margin = new(0, -3, 0, -3);

                // Right adjusts the labels with each having the same width
                Label label = VisualChildren.GetVisualChild<Label>(child);
                if (label != null)
                {
                    label.HorizontalContentAlignment = HorizontalAlignment.Right;
                    label.Width = maxColumn1Width;
                }

                // Adjust each control to the same width
                UIElementCollection children = child.Children;
                if (children is { Count: >= 2 })
                {
                    System.Windows.Controls.Control thisControl = (System.Windows.Controls.Control)child.Children[1];
                    thisControl.Width = maxColumn2Width;
                    // Add an ellipsis if there is more than one line
                    string[] lines = (thisControl.ToolTip.ToString()).Split('\r', '\n');
                    string firstLine = lines.Length == 1 
                        ? lines[0]
                        : lines[0] + "\u2026";
                    TextBlock description = new()
                    {
                        Height=16,
                        Padding=new(0,0,0,0),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        TextWrapping = TextWrapping.NoWrap,
                        Text = firstLine,
                        Margin = new(10, 0, 10, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        ToolTip = new ToolTip
                        {
                            // So the tooltip width doesn't go crazy when we have long sentences
                            //MaxWidth = 550,
                            Content = new TextBlock
                            {
                                TextWrapping = TextWrapping.Wrap,
                                Text= thisControl.ToolTip.ToString(),
                            }
                        },
                    };
                    description.SetValue(ToolTipService.InitialShowDelayProperty, 0);
                    Grid.SetColumn(description, 2);
                    children.Add(description);
                }
            }
        }
        #endregion

        #region Public: Scroll last row into view
        public void ScrollIntoViewLastRow()
        {
            PreviewScrollViewer.ScrollToEnd();
        }

        public void ScrollIntoViewFirstRow()
        {
            PreviewScrollViewer.ScrollToTop();
        }

        // Try to scroll the label into view
        // Note that this is a Noop if the label isn't visible
        public void ScrollLabelIntoView(string labelToFind)
        {
            foreach (Grid child in ControlsPanel.Children)
            {
                Label label = VisualChildren.GetVisualChild<Label>(child);
                
                if (label.Content.ToString() == labelToFind)
                {
                    label.BringIntoView();
                    return;
                }
            }
        }
        #endregion

        #region Private Dragging and Dropping of Controls to Reorder them
        private void ControlsPanel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Equals(e.Source, ControlsPanel)) return;
            MouseDownWrapper(e);
        }

        private void ControlsPanel_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (Globals.MouseState.isMouseDown && e.Source is Label)
            {
                Point currentMousePosition = e.GetPosition(ControlsPanel);
                if ((Globals.MouseState.isMouseDragging == false) &&
                    ((Math.Abs(currentMousePosition.X - Globals.MouseState.mouseDownStartPosition.X) > SystemParameters.MinimumHorizontalDragDistance) ||
                     (Math.Abs(currentMousePosition.Y - Globals.MouseState.mouseDownStartPosition.Y) > SystemParameters.MinimumVerticalDragDistance)))
                {
                    // Ignore drag/drop operations if it occurs over an open multiLineText
                    // Otherwise one can't do any mouse move interactions with that editor e.g,
                    // selecting its text.
                    if (e.Source is MultiLineText { EditorPopup.IsOpen: true })
                    {
                        return;
                    }
                    MouseDraggingWrapper(e.Source as UIElement);
                    DragDrop.DoDragDrop(Globals.MouseState.dummyMouseDragSource, new DataObject("UIElement", e.Source, true), DragDropEffects.Move);
                }
            }
        }

        private void ControlsPanel_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            MouseReleaseWrapper();
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
                foreach (UIElement element in ControlsPanel.Children)
                {
                    if (element.Equals(dropTarget))
                    {
                        dropTargetIndex = control;
                        break;
                    }

                    // Check if its a stack panel, and if so check to see if its children are the drop target
                    if (element is Grid grid)
                    {
                        // Check the children...
                        foreach (UIElement subelement in grid.Children)
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
                    // Abort if the the drag source and drop destination is the same.
                    Grid srcGrid = Globals.MouseState.realMouseDragSource as Grid 
                                               ?? Utilities.FindVisualParent<Grid>(Globals.MouseState.realMouseDragSource);
                    Grid destGrid = dropTarget as Grid ?? Utilities.FindVisualParent<Grid>(dropTarget);
                    if (Equals(srcGrid, destGrid))
                    {
                        return;
                    }

                    // Check if the drag source is a grid, and if not reset it to the source's parent grid
                    MouseUpdateDragSourceToGridParentIfNeeded();

                    // Reorder the control by removing it from its current location and inserting it at the new location
                    ControlsPanel.Children.Remove(Globals.MouseState.realMouseDragSource);
                    ControlsPanel.Children.Insert(dropTargetIndex, Globals.MouseState.realMouseDragSource);

                    // This then rebuilds everything depending on the order of those controls
                    Globals.RootEditor.DoUpdateMetadataControlOrder(this, ParentTab.Level);
                    this.ParentTab.MetadataGridControl.DoLayoutUpdated(true);
                }
                MouseReleaseWrapper();
            }
        }
        #endregion

        #region Private Utilities to set various Mouse States
        private void MouseDownWrapper(MouseButtonEventArgs e)
        {
            Globals.MouseState.isMouseDown = true;
            Globals.MouseState.mouseDownStartPosition = e.GetPosition(ControlsPanel);
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

        // If the drag source isn't a grid, set it to the source's parent stack panel
        private void MouseUpdateDragSourceToGridParentIfNeeded()
        {
            if (Globals.MouseState.realMouseDragSource is not Grid)
            {
                Grid parent = Utilities.FindVisualParent<Grid>(Globals.MouseState.realMouseDragSource);
                Globals.MouseState.realMouseDragSource = parent;
            }
        }
        #endregion
    }
}

