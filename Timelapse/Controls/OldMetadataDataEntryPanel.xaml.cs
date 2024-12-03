using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms.VisualStyles;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace Timelapse.Controls
{
    /// <summary>
    /// Interaction logic for MetadataDataEntryPanel.xaml
    /// </summary>
    public partial class MetadataDataEntryPanel : UserControl
    {
        public int Level { get; set; }

        private FileDatabase FileDatabase => GlobalReferences.MainWindow.DataHandler.FileDatabase;

        public MetadataDataEntryPanel(int level)
        {
            InitializeComponent();
            this.Level = level;
        }

        
        #region Public: Generate the controls to show in the preview panel
        public void GenerateControls(int level, DataTableBackedList<MetadataControlRow> metadataTable)
        {
            // Always clear the children 
            // This clears things if this is invoked after all rows have been removed
            // and prepares things if things have been changed or added
            this.ControlsPanel.Children.Clear();

            // Return if no data for that level exists. 
            // e.g., when a new level is just being created, or when a level has no controls or no data is associated with it,
            if (null == metadataTable)
            {
                return;
            }

            // used for styling all content and label controls except ComboBoxes since the combo box style is commented out in DataEntryControls.xaml
            // and defined instead in MainWindow.xaml as an exception workaround
            DataEntryControls styleProvider = new DataEntryControls();
            int row = 0;
            foreach (MetadataControlRow control in metadataTable)
            {
                // instantiate control UX objects
                Grid grid;
                bool isPositive;
                switch (control.Type)
                {
                    case Constant.Control.Note:
                    case DatabaseColumn.File:
                    case DatabaseColumn.RelativePath:
                        Label noteLabel = MetadataCreateControl.CreateLabel(styleProvider, control);
                        TextBox noteContent = MetadataCreateControl.CreateTextBox(styleProvider, control, ControlTypeEnum.MetadataControl);
                        grid = MetadataCreateControl.CreateGrid(styleProvider, noteLabel, noteContent);
                        if (control.Type == Constant.Control.Note)
                        {
                            this.ConfigureNote(control, noteContent, this.FileDatabase);
                        }
                        break;
                    case Constant.Control.AlphaNumeric:
                        Label alphanumericLabel = MetadataCreateControl.CreateLabel(styleProvider, control);
                        TextBox alphanumericContent = MetadataCreateControl.CreateTextBox(styleProvider, control, ControlTypeEnum.MetadataControl);
                        alphanumericContent.PreviewKeyDown += AlphanumericContent_PreviewKeyDown;
                        alphanumericContent.PreviewTextInput += AlphanumericContent_PreviewTextInput;
                        grid = MetadataCreateControl.CreateGrid(styleProvider, alphanumericLabel, alphanumericContent);
                        break;
                    case Constant.Control.MultiLine:
                        Label multiLineLabel = MetadataCreateControl.CreateLabel(styleProvider, control);
                        MultiLineTextEditor multiLineContent = MetadataCreateControl.CreateMultiLine(styleProvider, control, ControlTypeEnum.MetadataControl);
                        grid = MetadataCreateControl.CreateGrid(styleProvider, multiLineLabel, multiLineContent);
                        break;
                    case Constant.Control.Counter:
                        RadioButton counterLabel = MetadataCreateControl.CreateCounterLabelButton(styleProvider, control);
                        IntegerUpDown counterContent = MetadataCreateControl.CreateIntegerUpDown(styleProvider, control, ControlTypeEnum.MetadataControl, true);
                        grid = MetadataCreateControl.CreateGrid(styleProvider, counterLabel, counterContent);
                        counterLabel.IsTabStop = false;
                        //            counterContent.GotFocus += Control_GotFocus;
                        //            counterContent.LostFocus += Control_LostFocus;
                        break;
                    case Constant.Control.Flag:
                    case DatabaseColumn.DeleteFlag:
                        Label flagLabel = MetadataCreateControl.CreateLabel(styleProvider, control);
                        CheckBox flagContent = MetadataCreateControl.CreateFlag(styleProvider, control);
                        flagContent.IsChecked = String.Equals(control.DefaultValue, BooleanValue.True, StringComparison.OrdinalIgnoreCase);
                        grid = MetadataCreateControl.CreateGrid(styleProvider, flagLabel, flagContent);
                        break;
                    case Constant.Control.FixedChoice:
                        Label choiceLabel = MetadataCreateControl.CreateLabel(styleProvider, control);
                        ComboBox choiceContent = MetadataCreateControl.CreateComboBox(styleProvider, control, ControlTypeEnum.MetadataControl);
                        grid = MetadataCreateControl.CreateGrid(styleProvider, choiceLabel, choiceContent);
                        break;
                    case Constant.Control.MultiChoice:
                        Label multiChoiceLabel = MetadataCreateControl.CreateLabel(styleProvider, control);
                        CheckComboBox multiChoiceContent = MetadataCreateControl.CreateMultiChoiceComboBox(styleProvider, control, ControlTypeEnum.MetadataControl);
                        grid = MetadataCreateControl.CreateGrid(styleProvider, multiChoiceLabel, multiChoiceContent);
                        break;
                    case Constant.Control.DateTimeCustom:
                        Label dateTimeCustomLabel = MetadataCreateControl.CreateLabel(styleProvider, control);
                        DateTimePicker dateTimeCustomContent = MetadataCreateControl.CreateDateTimePicker(control);
                        grid = MetadataCreateControl.CreateGrid(styleProvider, dateTimeCustomLabel, dateTimeCustomContent);
                        break;
                    case Constant.Control.IntegerPositive:
                    case Constant.Control.IntegerAny:
                        Label integerLabel = MetadataCreateControl.CreateLabel(styleProvider, control);
                        isPositive = control.Type == Constant.Control.IntegerPositive;
                        IntegerUpDown integerContent = MetadataCreateControl.CreateIntegerUpDown(styleProvider, control, ControlTypeEnum.MetadataControl, isPositive);
                        grid = MetadataCreateControl.CreateGrid(styleProvider, integerLabel, integerContent);
                        integerLabel.IsTabStop = false;
                        //        integerContent.GotFocus += Control_GotFocus; // TODO: are these two got/lost focus needed? Check
                        //        integerContent.LostFocus += Control_LostFocus;
                        break;
                    case Constant.Control.DecimalAny:
                    case Constant.Control.DecimalPositive:
                        Label decimalLabel = MetadataCreateControl.CreateLabel(styleProvider, control);
                        isPositive = control.Type == Constant.Control.DecimalPositive;
                        DoubleUpDown decimalContent = MetadataCreateControl.CreateDoubleUpDown(styleProvider, control, ControlTypeEnum.MetadataControl, isPositive);
                        grid = MetadataCreateControl.CreateGrid(styleProvider, decimalLabel, decimalContent);
                        decimalLabel.IsTabStop = false;
                        //        decimalContent.GotFocus += Control_GotFocus; // TODO: are these two got/lost focus needed? Check
                        //       decimalContent.LostFocus += Control_LostFocus;
                        break;
                    default:
                        throw new NotSupportedException($"Unhandled control type {control.Type}.");
                }

                if (control.Visible == false)
                {
                    grid.Visibility = Visibility.Collapsed;
                }
                this.ControlsPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                Grid.SetRow(grid, row++);
                this.ControlsPanel.Children.Add(grid);
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
            foreach (Grid child in this.ControlsPanel.Children)
            {
                Label label = VisualChildren.GetVisualChild<Label>(child);
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                maxColumn1Width = Math.Max(label.DesiredSize.Width, maxColumn1Width);

                System.Windows.Controls.Control thisControl = (System.Windows.Controls.Control)child.Children[1];
                thisControl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                maxColumn2Width = Math.Max(thisControl.DesiredSize.Width, maxColumn2Width);
            }

            // Now we resize each label to the maximum width and right adjust them
            foreach (Grid child in this.ControlsPanel.Children)
            {
                // decrease the vertical spacing between controls
                child.Margin = new Thickness(0, -3, 0, -3);

                // Right adjusts the labels with each having the same width
                Label label = VisualChildren.GetVisualChild<Label>(child);
                if (label != null)
                {
                    label.HorizontalContentAlignment = HorizontalAlignment.Right;
                    label.Width = maxColumn1Width;
                }

                // Adjust each control to the same width
                UIElementCollection children = child.Children;
                if (children != null && children.Count >= 2)
                {
                    System.Windows.Controls.Control thisControl = (System.Windows.Controls.Control)child.Children[1];
                    thisControl.Width = maxColumn2Width;
                    // Add an ellipsis if there is more than one line
                    string[] lines = (thisControl.ToolTip.ToString()).Split('\r', '\n');
                    string firstLine = lines.Length == 1
                        ? lines[0]
                        : lines[0] + "\u2026";
                    TextBlock description = new TextBlock
                    {
                        Height = 16,
                        Padding = new Thickness(0, 0, 0, 0),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        TextWrapping = TextWrapping.NoWrap,
                        Text = firstLine,
                        Margin = new Thickness(10, 0, 10, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        ToolTip = new ToolTip
                        {
                            // So the tooltip width doesn't go crazy when we have long sentences
                            //MaxWidth = 550,
                            Content = new TextBlock
                            {
                                TextWrapping = TextWrapping.Wrap,
                                Text = thisControl.ToolTip.ToString(),
                            }
                        },
                    };
                    description.SetValue(ToolTipService.InitialShowDelayProperty, 0);
                    Grid.SetColumn(description, 2);
                    children.Add(description);
                }
            }
        }

        private void ConfigureNote(MetadataControlRow control, TextBox textBox, FileDatabase fileDatabase)
        {
            textBox.PreviewKeyDown += TextBox_PreviewKeyDown;
            textBox.GotFocus += (sender, args) =>
            {
                Debug.Print($"Intial text: {textBox.Text}");
                textBox.Tag = textBox.Text;
            };
            textBox.LostFocus += (sender, args) =>
            {
                // remove white space
                textBox.Text = textBox.Text.Trim();
                if (null != textBox.Tag && (string)textBox.Tag != textBox.Text)
                {
                    Debug.Print($"Changed text: {textBox.Text}");
                    string tableName = $"Level{control.Level}";
                    ColumnTuplesWithWhere ctww = new ColumnTuplesWithWhere();
                    ColumnTuplesWithWhere columnToUpdate = new ColumnTuplesWithWhere();
                    columnToUpdate.Columns.Add(new ColumnTuple(control.DataLabel, textBox.Text)); // Populate the data 
                    columnToUpdate.SetWhere(control.Level);
                    FileDatabase.Database.Update(tableName, columnToUpdate);
                }
                textBox.Tag = null;
            };
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox && (e.Key == Key.Return || e.Key == Key.Enter))
            {
                textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }


        // Alphanumeric control - to allow only letters, numbers, _ but still work with special characters like backspace, tab, etc.
        private void AlphanumericContent_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Ignore spaces
            e.Handled = e.Key == Key.Space;
        }
        private void AlphanumericContent_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Ignore all but letters, numbers and _
            e.Handled = !Regex.IsMatch(e.Text, "^[a-zA-Z0-9_]");
        }

        #endregion

        public void ShowAddButton(bool show)
        {
            this.AddMetadata.Visibility = show
            ? Visibility.Visible
            : Visibility.Collapsed;
        }

        private void AddMetadata_OnClick(object sender, RoutedEventArgs e)
        {
            string tableName = $"Level{this.Level}";
            if (false == this.FileDatabase.Database.TableExists(tableName))
            {
                // say something here
                return;
            }

            string query = $"{Sql.SelectStarFrom} {tableName} {Sql.Where}" +
                           $"{Constant.DatabaseColumn.MetadataFolderPath} {Sql.Equal} {Sql.Quote(this.RelativePathToCurrentImage.Text)}";
            DataTable dataTable = this.FileDatabase.Database.GetDataTableFromSelect(query);
            if (dataTable.Rows.Count == 1)
            {
                foreach (DataColumn column in dataTable.Columns)
                {
                    Debug.Print($"{column} : '{dataTable.Rows[0][column]}'");
                }
            }
            else
            {
                // Populate the table from the control defaults
                DataTableBackedList<MetadataControlRow> metadataCrowsontrolRows = this.FileDatabase.MetadataControlsByLevel[this.Level];
                List<List<ColumnTuple>> newTableTuples = new List<List<ColumnTuple>>();

                ColumnTuplesWithWhere columnTuplesWithWhere = new ColumnTuplesWithWhere();

                columnTuplesWithWhere.Columns.Add(new ColumnTuple(Constant.DatabaseColumn.MetadataFolderPath, this.RelativePathToCurrentImage.Text));
                foreach (MetadataControlRow row in metadataCrowsontrolRows)
                {
                    columnTuplesWithWhere.Columns.Add(new ColumnTuple(row.DataLabel, row.DefaultValue));
                }
                newTableTuples.Add(columnTuplesWithWhere.Columns);
                this.FileDatabase.Database.Insert(tableName, newTableTuples);
                this.ShowAddButton(false);
            }
        }
    }
}
