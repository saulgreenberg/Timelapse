using System.Windows;
using System.Windows.Controls;
using Timelapse.Constant;
using Timelapse.ControlsDataCommon;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Controls
{
    /// <summary>
    /// Static helper class for creating metadata controls for the old metadata data entry panel
    /// </summary>
    public static class MetadataCreateControl
    {
        // UNUSED
        //#region UNUSED Grid Creation
        //public static Grid CreateGrid(DataEntryControls styleProvider, Control label, Control content)
        //{
        //    Grid grid = new Grid();
        //    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        //    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        //    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        //    Grid.SetColumn(label, 0);
        //    Grid.SetColumn(content, 1);
        //    grid.Children.Add(label);
        //    grid.Children.Add(content);

        //    Style style = styleProvider.FindResource(ControlStyle.GridContainerStyle) as Style;
        //    grid.Style = style;
        //    return grid;
        //}
        //#endregion

        #region Label Creation
        public static Label CreateLabel(DataEntryControls styleProvider, MetadataControlRow control)
        {
            Label label = new Label
            {
                Content = control.Label,
                ToolTip = control.Tooltip
            };
            Style style = styleProvider.FindResource(ControlContentStyleEnum.NoteTextBox.ToString()) as Style;
            label.Style = style;
            return label;
        }
        #endregion

        #region TextBox Creation
        public static TextBox CreateTextBox(DataEntryControls styleProvider, MetadataControlRow control, ControlTypeEnum controlType)
        {
            TextBox textBox = new TextBox
            {
                Text = control.DefaultValue,
                ToolTip = control.Tooltip,
                Width = controlType == ControlTypeEnum.MetadataControl
                    ? ControlDefault.MetadataDataEntryControlDefaultWidth
                    : ControlDefault.NoteDefaultWidth
            };
            
            Style style = styleProvider.FindResource(ControlContentStyleEnum.NoteTextBox.ToString()) as Style;
            textBox.Style = style;
            return textBox;
        }
        #endregion

        #region MultiLine Creation
        public static MultiLineText CreateMultiLine(DataEntryControls styleProvider, MetadataControlRow control, ControlTypeEnum controlType)
        {
            MultiLineText textBox = new MultiLineText
            {
                Text = control.DefaultValue,
                // Content = control.DefaultValue,
                ToolTip = control.Tooltip,
                Width = controlType == ControlTypeEnum.MetadataControl
                    ? ControlDefault.MetadataDataEntryControlDefaultWidth
                    : ControlDefault.NoteDefaultWidth,
                Height = 60
            };
            
            Style style = styleProvider.FindResource(ControlContentStyleEnum.MultiLineTextBox.ToString()) as Style;
            textBox.Style = style;
            return textBox;
        }
        #endregion

        #region Counter Creation
        public static RadioButton CreateCounterLabelButton(DataEntryControls styleProvider, MetadataControlRow control)
        {
            RadioButton radioButton = new RadioButton
            {
                GroupName = "DataEntry_Counter",
                Content = control.Label,
                ToolTip = control.Tooltip
            };
            
            Style style = styleProvider.FindResource(ControlContentStyleEnum.CounterTextBox.ToString()) as Style;
            radioButton.Style = style;
            return radioButton;
        }
        #endregion

        #region Integer Creation
        public static IntegerUpDown CreateIntegerUpDown(DataEntryControls styleProvider, MetadataControlRow control, ControlTypeEnum controlType, bool isPositiveOnly)
        {
            IntegerUpDown integerUpDown = new IntegerUpDown
            {
                ToolTip = control.Tooltip,
                Width = controlType == ControlTypeEnum.MetadataControl
                    ? ControlDefault.MetadataDataEntryControlDefaultWidth
                    : ControlDefault.CounterWidth,
                Minimum = isPositiveOnly ? 0 : int.MinValue,
                Maximum = int.MaxValue
            };

            if (int.TryParse(control.DefaultValue, out int defaultValue))
            {
                integerUpDown.Value = defaultValue;
            }

            Style style = styleProvider.FindResource(ControlContentStyleEnum.IntegerTextBox.ToString()) as Style;
            integerUpDown.Style = style;
            return integerUpDown;
        }
        #endregion

        #region Double Creation
        public static DoubleUpDown CreateDoubleUpDown(DataEntryControls styleProvider, MetadataControlRow control, ControlTypeEnum controlType, bool isPositiveOnly)
        {
            DoubleUpDown doubleUpDown = new DoubleUpDown
            {
                ToolTip = control.Tooltip,
                Width = controlType == ControlTypeEnum.MetadataControl
                    ? ControlDefault.MetadataDataEntryControlDefaultWidth
                    : ControlDefault.CounterWidth,
                Minimum = isPositiveOnly ? 0.0 : double.MinValue,
                Maximum = double.MaxValue
            };

            if (double.TryParse(control.DefaultValue, out double defaultValue))
            {
                doubleUpDown.Value = defaultValue;
            }

            Style style = styleProvider.FindResource(ControlContentStyleEnum.DoubleTextBox.ToString()) as Style;
            doubleUpDown.Style = style;
            return doubleUpDown;
        }
        #endregion

        #region ComboBox Creation
        public static ComboBox CreateComboBox(DataEntryControls styleProvider, MetadataControlRow control, ControlTypeEnum controlType)
        {
            ComboBox comboBox = new ComboBox
            {
                ToolTip = control.Tooltip,
                Width = controlType == ControlTypeEnum.MetadataControl
                    ? ControlDefault.MetadataDataEntryControlDefaultWidth
                    : ControlDefault.FixedChoiceDefaultWidth
            };

            // Add items to the combo box
            Choices choices = Choices.ChoicesFromJson(control.List);
            foreach (string choice in choices.ChoiceList)
            {
                comboBox.Items.Add(choice);
            }

            // Set the default selection
            comboBox.SelectedItem = control.DefaultValue;

            Style style = styleProvider.FindResource(ControlContentStyleEnum.ChoiceComboBox.ToString()) as Style;
            comboBox.Style = style;
            return comboBox;
        }
        #endregion

        #region MultiChoice ComboBox Creation
        public static CheckComboBox CreateMultiChoiceComboBox(DataEntryControls styleProvider, MetadataControlRow control, ControlTypeEnum controlType)
        {
            CheckComboBox comboBox = new CheckComboBox
            {
                ToolTip = control.Tooltip,
                Width = controlType == ControlTypeEnum.MetadataControl
                    ? ControlDefault.MetadataDataEntryControlDefaultWidth
                    : ControlDefault.FixedChoiceDefaultWidth
            };

            // Add items to the combo box
            Choices choices = Choices.ChoicesFromJson(control.List);
            foreach (string choice in choices.ChoiceList)
            {
                comboBox.Items.Add(choice);
            }

            Style style = styleProvider.FindResource(ControlContentStyleEnum.MultiChoiceComboBox.ToString()) as Style;
            comboBox.Style = style;
            return comboBox;
        }
        #endregion

        #region Flag Creation
        public static CheckBox CreateFlag(DataEntryControls styleProvider, MetadataControlRow control)
        {
            return CreateControls.CreateFlag(styleProvider, control);
        }
        #endregion

        #region DateTimePicker Creation  
        public static DateTimePicker CreateDateTimePicker(MetadataControlRow control)
        {
            return CreateControls.CreateDateTimePicker(control, DateTimeFormatEnum.DateAndTime, ControlDefault.DateTimeCustomDefaultValue);
        }
        #endregion
    }
}