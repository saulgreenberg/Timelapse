using System;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Controls;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Editor.Util
{
    /// <summary>Generates controls in the provided wrap panel based upon the information in the data grid templateTable.</summary>
    /// <remarks>
    /// It is meant to approximate what the controls will look like when rendered in the Timelapse UX by DataEntryControls but the
    /// two classes contain distinct code as rendering an immutable set of data entry controls is significantly different from the
    /// mutable set of controls which don't accept data in the editor.  Reusing the layout code in the DataEntryControl hierarchy
    /// is desirable but not currently feasible due to reliance on DataEntryControls.Propagate.
    /// </remarks>
    internal class EditorControls
    {
        public void Generate(WrapPanel parent, DataTableBackedList<ControlRow> templateTable)
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
                    case Constant.Control.Note:
                    case Constant.DatabaseColumn.File:
                    case Constant.DatabaseColumn.RelativePath:
                        Label noteLabel = EditorControls.CreateLabel(styleProvider, control);
                        TextBox noteContent = EditorControls.CreateTextBox(styleProvider, control);
                        stackPanel = EditorControls.CreateStackPanel(styleProvider, noteLabel, noteContent);
                        break;
                    case Constant.Control.Counter:
                        RadioButton counterLabel = EditorControls.CreateCounterLabelButton(styleProvider, control);
                        IntegerUpDown counterContent = EditorControls.CreateIntegerUpDown(styleProvider, control);
                        stackPanel = EditorControls.CreateStackPanel(styleProvider, counterLabel, counterContent);
                        counterLabel.IsTabStop = false;
                        counterContent.GotFocus += this.Control_GotFocus;
                        counterContent.LostFocus += this.Control_LostFocus;
                        break;
                    case Constant.Control.Flag:
                    case Constant.DatabaseColumn.DeleteFlag:
                        Label flagLabel = EditorControls.CreateLabel(styleProvider, control);
                        CheckBox flagContent = this.CreateFlag(styleProvider, control);
                        flagContent.IsChecked = String.Equals(control.DefaultValue, Constant.BooleanValue.True, StringComparison.OrdinalIgnoreCase);
                        stackPanel = EditorControls.CreateStackPanel(styleProvider, flagLabel, flagContent);
                        break;
                    case Constant.Control.FixedChoice:
                        Label choiceLabel = EditorControls.CreateLabel(styleProvider, control);
                        ComboBox choiceContent = EditorControls.CreateComboBox(styleProvider, control);
                        stackPanel = EditorControls.CreateStackPanel(styleProvider, choiceLabel, choiceContent);
                        break;
                    case Constant.DatabaseColumn.DateTime:
                        Label dateTimeLabel = EditorControls.CreateLabel(styleProvider, control);
                        DateTimePicker dateTimeContent = this.CreateDateTimePicker(control);
                        stackPanel = EditorControls.CreateStackPanel(styleProvider, dateTimeLabel, dateTimeContent);
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

        public static bool IsStandardControlType(string controlType)
        {
            return Constant.Control.StandardTypes.Contains(controlType);
        }

        private DateTimePicker CreateDateTimePicker(ControlRow control)
        {
            DateTimePicker dateTimePicker = new DateTimePicker()
            {
                ToolTip = control.Tooltip,
                Width = control.Width,
                CultureInfo = System.Globalization.CultureInfo.CreateSpecificCulture("en-US")
            };
            DataEntryHandler.Configure(dateTimePicker, Constant.ControlDefault.DateTimeDefaultValue);
            dateTimePicker.GotFocus += this.Control_GotFocus;
            dateTimePicker.LostFocus += this.Control_LostFocus;
            return dateTimePicker;
        }

        // Returns a stack panel containing two controls
        // The stack panel ensures that controls are layed out as a single unit with certain spatial characteristcs 
        // i.e.,  a given height, right margin, where contents will not be broken durring (say) panel wrapping
        private static StackPanel CreateStackPanel(DataEntryControls styleProvider, Control label, Control content)
        {
            StackPanel stackPanel = new StackPanel();
            stackPanel.Children.Add(label);
            stackPanel.Children.Add(content);

            Style style = styleProvider.FindResource(Constant.ControlStyle.ContainerStyle) as Style;
            stackPanel.Style = style;
            return stackPanel;
        }

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
                comboBox.Items.Add(String.Empty);
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

        // HIghlight control when it gets the focus (simulates aspects of tab control in Timelapse)
        private void Control_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                control.BorderThickness = new Thickness(Constant.Control.BorderThicknessHighlight);
                control.BorderBrush = Constant.Control.BorderColorHighlight;
            }
            else
            {
                TracePrint.StackTraceToOutput("Unexpected null");
            }
        }

        private void Control_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                control.BorderThickness = new Thickness(Constant.Control.BorderThicknessNormal);
                control.BorderBrush = Constant.Control.BorderColorNormal;
            }
            else
            {
                TracePrint.StackTraceToOutput("Unexpected null");
            }
        }
    }
}
