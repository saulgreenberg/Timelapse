using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Constant;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using TimelapseWpf.Toolkit;
using Control = System.Windows.Controls.Control;

namespace TimelapseTemplateEditor.EditorCode
{
    public static class PreviewControlCommon
    {
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
        public static StackPanel CreateStackPanel(DataEntryControls styleProvider, Control label, Control content)
        {
            StackPanel stackPanel = new();
            stackPanel.Children.Add(label);
            stackPanel.Children.Add(content);

            Style style = styleProvider.FindResource(ControlStyle.StackPanelContainerStyle) as Style;
            stackPanel.Style = style;
            return stackPanel;
        }

        public static Grid CreateGrid(DataEntryControls styleProvider, Control label, Control content)
        {
            Grid grid = new();
            grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
 
            Grid.SetColumn(label, 0);
            Grid.SetColumn(content, 1);
            grid.Children.Add(label);
            grid.Children.Add(content);

            Style style = styleProvider.FindResource(ControlStyle.GridContainerStyle) as Style;
            grid.Style = style;
            return grid;
        }


        // Create Label
        public static Label CreateLabel(DataEntryControls styleProvider, CommonControlRow control)
        {
            Label label = new()
            {
                Content = control.Label,
                ToolTip = control.Tooltip,
                Style = styleProvider.FindResource(ControlLabelStyleEnum.DefaultLabel.ToString()) as Style
            };
            return label;
        }

        // Create TextBox
        public static ImprintTextBox CreateTextBox(DataEntryControls styleProvider, CommonControlRow control, ControlTypeEnum controlType)
        {
            ImprintAutoCompleteTextBox textBox = new()
            {
                Text = control.DefaultValue,
                ToolTip = control.Tooltip,
                Width = controlType == ControlTypeEnum.MetadataControl
                    ? ControlDefault.MetadataDataEntryControlDefaultWidth
                    : ((ControlRow)control).Width,
                Style = styleProvider.FindResource(ControlContentStyleEnum.ImprintNoteTextBox.ToString()) as Style
            };
            return textBox;
        }

        // Create MultiLineText
        public static MultiLineText CreateMultiLine(DataEntryControls styleProvider, CommonControlRow control, ControlTypeEnum controlType)
        {
            MultiLineText textBox = new()
            {
                Text = control.DefaultValue,
                // Content = control.DefaultValue,
                ToolTip = control.Tooltip,

                Width = controlType == ControlTypeEnum.MetadataControl
                    ? ControlDefault.MetadataDataEntryControlDefaultWidth
                    : ((ControlRow) control).Width,
                Style = styleProvider.FindResource(ControlContentStyleEnum.MultiLineTextBox.ToString()) as Style
            };
            return textBox;
        }

        // Create IntegerUpDown
        public static IntegerUpDown CreateIntegerUpDown(DataEntryControls styleProvider, CommonControlRow control, ControlTypeEnum controlType, bool isPositiveOnly)
        {
            IntegerUpDown integerUpDown = new()
            {
                // Adjust the look
                Text = control.DefaultValue,
                ToolTip = control.Tooltip,
                // 18 accounts for the width of the spinner
                Width = controlType == ControlTypeEnum.MetadataControl
                    ? ControlDefault.MetadataDataEntryControlDefaultWidth + 18
                    : ((ControlRow)control).Width + 18,

                // Adjust behaviors
                Minimum = isPositiveOnly ? 0 : Int32.MinValue,
                Maximum = Int32.MaxValue,
                DisplayDefaultValueOnEmptyText = true,
                DefaultValue = null,
                UpdateValueOnEnterKey = true,
                Style = styleProvider.FindResource(ControlContentStyleEnum.IntegerTextBox.ToString()) as Style
            };
            return integerUpDown;
        }

        // Create DecimalUpDown (actually a double up down)
        public static DoubleUpDown CreateDoubleUpDown(DataEntryControls styleProvider, CommonControlRow control, ControlTypeEnum controlType, bool isPositiveOnly)
        {
            DoubleUpDown doubleUpDown = new()
            {
                // Adjust the look
                Text = control.DefaultValue,
                ToolTip = control.Tooltip,
                // accounts for the width of the spinner
                Width = controlType == ControlTypeEnum.MetadataControl
                    ? ControlDefault.MetadataDataEntryControlDefaultWidth + 18
                    : ((ControlRow)control).Width + 18,

                // Adjust behaviors
                Minimum = isPositiveOnly ? 0 : Double.MinValue,
                Maximum = Double.MaxValue,
                DisplayDefaultValueOnEmptyText = true,
                DefaultValue = null,
                UpdateValueOnEnterKey = true,
                CultureInfo = CultureInfo.InvariantCulture,
                FormatString = Timelapse.Constant.ControlDefault.DecimalFormatString,
                Style = styleProvider.FindResource(ControlContentStyleEnum.DoubleTextBox.ToString()) as Style
            };
            return doubleUpDown;
        }

        // Create CounterLabel Button
        public static RadioButton CreateCounterLabelButton(DataEntryControls styleProvider, CommonControlRow control)
        {
            RadioButton radioButton = new()
            {
                GroupName = "DataEntryCounter",
                Content = control.Label,
                ToolTip = control.Tooltip,
                Style = styleProvider.FindResource(ControlLabelStyleEnum.CounterButton.ToString()) as Style
            };
            return radioButton;
        }

        // Create ComboBox
        public static WatermarkComboBox CreateComboBox(DataEntryControls styleProvider, CommonControlRow control, ControlTypeEnum controlType)
        {
            WatermarkComboBox comboBox = new()
            {
                ToolTip = control.Tooltip,
                Width = controlType == ControlTypeEnum.MetadataControl
                    ? ControlDefault.MetadataDataEntryControlDefaultWidth
                    : ((ControlRow)control).Width,
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

        // Create ComboBox
        public static WatermarkCheckComboBox CreateMultiChoiceComboBox(DataEntryControls styleProvider, CommonControlRow control, ControlTypeEnum controlType)
        {
            WatermarkCheckComboBox comboBox = new()
            {
                ToolTip = control.Tooltip,
                Width = controlType == ControlTypeEnum.MetadataControl
                    ? ControlDefault.MetadataDataEntryControlDefaultWidth
                    : ((ControlRow)control).Width,
                Style = styleProvider.FindResource(ControlContentStyleEnum.MultiChoiceComboBox.ToString()) as Style
            };

            // Add items to the combo box
            Choices choices = Choices.ChoicesFromJson(control.List);
            // If there are no items, put in a space just so something is there when its clicked
            if (choices.ChoiceList.Count == 0)
            {
                comboBox.Items.Add(string.Empty);
            }
            else
            {
                foreach (string choice in choices.ChoiceList)
                {
                    comboBox.Items.Add(choice);
                }
            }

            // The displayed item shoudl be the control's currentdefault value
            comboBox.SelectedItem = control.DefaultValue;
            return comboBox;
        }
        #endregion

    }
}
