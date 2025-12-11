using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
using Point = System.Windows.Point;

namespace TimelapseTemplateEditor.Controls
{
    // The code below is primarily to allow  a user to to re-order  controls 
    // by dragging and dropping the chosen control's label as displayed in the preview panel 
    public partial class TemplateDataEntryPreviewPanel
    {
        #region Constructor
        public TemplateDataEntryPreviewPanel()
        {
            InitializeComponent();
            if (Globals.Root?.dataGridBeingUpdatedByCode != null)
            {
                Globals.Root.dataGridBeingUpdatedByCode = false;
            }
        }
        #endregion

        #region Public: Generate the controls to show in the preview panel
        public void GeneratePreviewControls(WrapPanel parent, DataTableBackedList<ControlRow> templateTable)
        {
            // used for styling all content and label controls except ComboBoxes since the combo box style is commented out in DataEntryControls.xaml
            // and defined instead in MainWindow.xaml as an exception workaround
            DataEntryControls styleProvider = new();

            parent.Children.Clear();
            foreach (ControlRow control in templateTable)
            {
                // instantiate control UX objects
                StackPanel stackPanel;
                switch (control.Type)
                {
                    case DatabaseColumn.File:
                    case DatabaseColumn.RelativePath:
                        Label fileAndPathLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        ImprintTextBox fileAndPathContent = PreviewControlCommon.CreateTextBox(styleProvider, control, ControlTypeEnum.TemplateControl);
                        fileAndPathContent.IsEnabled = false;
                        stackPanel = PreviewControlCommon.CreateStackPanel(styleProvider, fileAndPathLabel, fileAndPathContent);
                        fileAndPathContent.FontStyle = FontStyles.Italic;
                        fileAndPathContent.Foreground = Brushes.Gray;
                        fileAndPathContent.Text = control.Type == DatabaseColumn.File
                            ? "the file's name"
                            : "the file's path";
                        break;

                    case DatabaseColumn.DateTime:
                        Label dateTimeLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        WatermarkDateTimePicker dateTimeContent = CreateControls.CreateWatermarkDateTimePicker(control, DateTimeFormatEnum.DateAndTime, ControlDefault.DateTimeDefaultValue);
                        dateTimeContent.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_HandleKeyDownForEnter;
                        stackPanel = PreviewControlCommon.CreateStackPanel(styleProvider, dateTimeLabel, dateTimeContent);
                        break;

                    case Control.Note:
                        Label noteLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        ImprintTextBox noteContent = PreviewControlCommon.CreateTextBox(styleProvider, control, ControlTypeEnum.TemplateControl);
                        noteContent.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_HandleKeyDownForEnter;
                        stackPanel = PreviewControlCommon.CreateStackPanel(styleProvider, noteLabel, noteContent);
                        break;
                    case Control.AlphaNumeric:
                        Label alphanumericLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        TextBox alphanumericContent = PreviewControlCommon.CreateTextBox(styleProvider, control, ControlTypeEnum.TemplateControl);
                        alphanumericContent.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_TextBoxNoSpaces;
                        alphanumericContent.PreviewTextInput += Timelapse.Util.ValidationCallbacks.PreviewInput_AlphaNumericCharacterOnly;
                        alphanumericContent.TextChanged += Timelapse.Util.ValidationCallbacks.TextChanged_AlphaNumericTextOnly;
                        DataObject.AddPastingHandler(alphanumericContent, Timelapse.Util.ValidationCallbacks.Paste_OnlyIfAlphaNumeric);
                        stackPanel = PreviewControlCommon.CreateStackPanel(styleProvider, alphanumericLabel, alphanumericContent);
                        break;
                    case Control.MultiLine:
                        Label multilineLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        MultiLineText multiLineContent = PreviewControlCommon.CreateMultiLine(styleProvider, control, ControlTypeEnum.TemplateControl);
                        multiLineContent.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_HandleKeyDownForEnter;
                        stackPanel = PreviewControlCommon.CreateStackPanel(styleProvider, multilineLabel, multiLineContent);
                        break;

                    case Control.Counter:
                        RadioButton counterLabel = PreviewControlCommon.CreateCounterLabelButton(styleProvider, control);
                        IntegerUpDown counterContent = PreviewControlCommon.CreateIntegerUpDown(styleProvider, control, ControlTypeEnum.TemplateControl, true);
                        counterContent.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_IntegerUpDownNoSpaces;
                        counterContent.PreviewTextInput += Timelapse.Util.ValidationCallbacks.PreviewInput_IntegerPositiveCharacterOnly;
                        DataObject.AddPastingHandler(counterContent, Timelapse.Util.ValidationCallbacks.Paste_OnlyIfIntegerPositive);
                        stackPanel = PreviewControlCommon.CreateStackPanel(styleProvider, counterLabel, counterContent);
                        counterLabel.IsTabStop = false;
                        counterContent.GotFocus +=  ControlsDataHelpers.Control_GotFocus;
                        counterContent.LostFocus += ControlsDataHelpers.Control_LostFocus;
                        break;
                    case Control.IntegerAny:
                    case Control.IntegerPositive:
                        Label integerLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        // Last argument sets the integerUpDown to positive only if needed
                        IntegerUpDown integerContent = PreviewControlCommon.CreateIntegerUpDown(styleProvider, control, ControlTypeEnum.TemplateControl, control.Type == Control.IntegerPositive);
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
                        integerContent.GotFocus += ControlsDataHelpers.Control_GotFocus;
                        integerContent.LostFocus += ControlsDataHelpers.Control_LostFocus;
                        stackPanel = PreviewControlCommon.CreateStackPanel(styleProvider, integerLabel, integerContent);
                        break;
                    case Control.DecimalAny:
                    case Control.DecimalPositive:
                        Label decimalAnyLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        // Last argument sets the doubleUpDown to positive only if needed
                        DoubleUpDown decimalContent = PreviewControlCommon.CreateDoubleUpDown(styleProvider, control, ControlTypeEnum.TemplateControl, control.Type == Control.DecimalPositive);
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
                        decimalContent.GotFocus += ControlsDataHelpers.Control_GotFocus;
                        decimalContent.LostFocus += ControlsDataHelpers.Control_LostFocus;

                        stackPanel = PreviewControlCommon.CreateStackPanel(styleProvider, decimalAnyLabel, decimalContent);
                        break;

                    case Control.Flag:
                    case DatabaseColumn.DeleteFlag:
                        Label flagLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        CheckBox flagContent = CreateControls.CreateFlag(styleProvider, control);
                        flagContent.IsChecked = String.Equals(control.DefaultValue, BooleanValue.True, StringComparison.OrdinalIgnoreCase);
                        flagContent.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_HandleKeyDownForEnter;
                        stackPanel = PreviewControlCommon.CreateStackPanel(styleProvider, flagLabel, flagContent);
                        break;

                    case Control.FixedChoice:
                        Label choiceLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        WatermarkComboBox choiceContent = PreviewControlCommon.CreateComboBox(styleProvider, control, ControlTypeEnum.TemplateControl);
                        choiceContent.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_HandleKeyDownForEnter;
                        stackPanel = PreviewControlCommon.CreateStackPanel(styleProvider, choiceLabel, choiceContent);
                        break;

                    case Control.MultiChoice:
                        Label multiChoiceLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        WatermarkCheckComboBox multiChoiceContent = PreviewControlCommon.CreateMultiChoiceComboBox(styleProvider, control, ControlTypeEnum.TemplateControl);
                        multiChoiceContent.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_HandleKeyDownForEnter;
                        multiChoiceContent.Opened += ControlsDataHelpers.WatermarkCheckComboBox_DropDownOpened;
                        multiChoiceContent.Closed += ControlsDataHelpers.WatermarkCheckComboBox_DropDownClosed;
                        multiChoiceContent.ItemSelectionChanged += ControlsDataHelpers.MultiChoice_ItemSelectionChanged;
                        multiChoiceContent.Text = control.DefaultValue;
                        stackPanel = PreviewControlCommon.CreateStackPanel(styleProvider, multiChoiceLabel, multiChoiceContent);
                        break;

                    case Control.DateTime_:
                        Label dateTimeCustomLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        WatermarkDateTimePicker dateTimeCustomContent = CreateControls.CreateWatermarkDateTimePicker(control, DateTimeFormatEnum.DateAndTime,
                            DateTimeHandler.TryParseDatabaseDateTime(control.DefaultValue, out DateTime dateTime)
                            ? dateTime
                            : ControlDefault.DateTimeCustomDefaultValue);
                        dateTimeCustomContent.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_HandleKeyDownForEnter;
                        stackPanel = PreviewControlCommon.CreateStackPanel(styleProvider, dateTimeCustomLabel, dateTimeCustomContent);
                        break;
                    case Control.Date_:
                        Label dateLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        WatermarkDateTimePicker dateContent = CreateControls.CreateWatermarkDateTimePicker(control, DateTimeFormatEnum.DateOnly,
                            DateTimeHandler.TryParseDatabaseDate(control.DefaultValue, out DateTime date)
                                ? date
                                : ControlDefault.Date_DefaultValue);
                        dateContent.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_HandleKeyDownForEnter;
                        stackPanel = PreviewControlCommon.CreateStackPanel(styleProvider, dateLabel, dateContent);
                        break;
                    case Control.Time_:
                        Label timeLabel = PreviewControlCommon.CreateLabel(styleProvider, control);
                        TimePicker timeContent = CreateControls.CreateWatermarkTimePicker(control,
                            DateTimeHandler.TryParseDatabaseTime(control.DefaultValue, out DateTime time)
                                ? time
                                : ControlDefault.Time_DefaultValue);
                        timeContent.PreviewKeyDown += Timelapse.Util.ValidationCallbacks.PreviewKeyDown_HandleKeyDownForEnter;
                        stackPanel = PreviewControlCommon.CreateStackPanel(styleProvider, timeLabel, timeContent);
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

        #region Private Dragging and Dropping of Controls to Reorder them
        private void ControlsPanel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Equals(e.Source, ControlsPanel)) return;
            MouseDownWrapper(e);
        }

        private void ControlsPanel_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (Globals.MouseState.isMouseDown)
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
                    // Abort if the the drag source and drop destination is the same.
                    StackPanel srcStackPanel = Globals.MouseState.realMouseDragSource as StackPanel 
                                               ?? Utilities.FindVisualParent<StackPanel>(Globals.MouseState.realMouseDragSource);
                    StackPanel destStackPanel = dropTarget as StackPanel 
                                                ?? Utilities.FindVisualParent<StackPanel>(dropTarget);
                    if (Equals(srcStackPanel, destStackPanel))
                    {
                        return;
                    }

                    // Check if the drag source is a stack panel, and if not reset it to the source's parent stack panel
                    MouseUpdateDragSourceToStackPanelParentIfNeeded();

                    // Reorder the control by removing it from its current location and inserting it at the new location
                    ControlsPanel.Children.Remove(Globals.MouseState.realMouseDragSource);
                    ControlsPanel.Children.Insert(dropTargetIndex, Globals.MouseState.realMouseDragSource);

                    // This then rebuilds everything depending on the order of those controls
                    Globals.RootEditor.TemplateDoUpdateControlOrder();
                    Globals.TemplateDataGridControl.DoLayoutUpdated(true);
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

        // If the drag source isn't a stack panel, set it to the source's parent stack panel
        private void MouseUpdateDragSourceToStackPanelParentIfNeeded()
        {
            if (Globals.MouseState.realMouseDragSource is not StackPanel)
            {
                StackPanel parent = Utilities.FindVisualParent<StackPanel>(Globals.MouseState.realMouseDragSource);
                Globals.MouseState.realMouseDragSource = parent;
            }
        }
        #endregion
    }
}
