using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Dialog;
using Timelapse.Standards;
using Timelapse.Util;
using Control = Timelapse.Constant.Control;

namespace TimelapseTemplateEditor.EditorCode
{
    // These methods are used by both the MetadataDataGridControl and the TemplateDataGridControl
    public static class DataGridCommonCode
    {
        #region ApplyPendingEdits
        // Apply and commit any pending edits that may be pending 
        // e.g., this is invoke to guarantee that current edits, if any, are committed
        // Examples include cases where the enter key was not pressed, a menu being selected during editing, etc.
        public static void ApplyPendingEdits(DataGrid dataGrid)
        {
            Globals.Root.dataGridBeingUpdatedByCode = false;
            dataGrid.CommitEdit();
        }
        #endregion

        #region Callback actions (names reflect Callback) BeginningEdit, CurrentCellChanged
        // Before cell editing begins on a cell click, the cell is disabled if it is grey (meaning cannot be edited).
        // Another method will re-enable the cell immediately afterwards.
        // The reason for this implementation is because disabled cells cannot be single clicked,
        // but we still need to recognize the single click for row actions.
        public static void BeginningEdit(DataGrid dataGrid)
        {
            if (TryGetCurrentCell(dataGrid, out DataGridCell currentCell, out _) == false)
            {
                return;
            }

            if (currentCell.Background.Equals(EditorConstant.NotEditableCellColor))
            {
                currentCell.IsEnabled = false;
                dataGrid.CancelEdit();
            }
        }

        // After cell editing ends (prematurely or no), re-enable the cell, if its disabled.
        // See MetadataDataGrid_BeginningEdit below for full explanation.
        public static void CurrentCellChanged(DataGrid dataGrid)
        {
            if (TryGetCurrentCell(dataGrid, out DataGridCell currentCell, out _) == false)
            {
                return;
            }
            if (currentCell.Background.Equals(EditorConstant.NotEditableCellColor))
            {
                currentCell.IsEnabled = true;
            }
        }

        public static void UpdateCellEditabilityAndVisibility(DataGrid dataGrid, string standard, int level)
        {
            bool isCamtrapDP = standard == Timelapse.Constant.Standards.CamtrapDPStandard;
            // Greys out cells and updates the visibility of particular rows as defined by logic. 
            // This is to  show the user uneditable cells. Color is also used by code to check whether a cell can be edited.
            // This method should be called after row are added/moved/deleted to update the colors. 
            // This also disables checkboxes that cannot be edited. Disabling checkboxes does not effect row interactions.
            // Finally, it collapses or shows various date-associated rows.
            for (int rowIndex = 0; rowIndex < dataGrid.Items.Count; rowIndex++)
            {

                // Approach :  In order for ItemContainerGenerator to work, we need to set the dataGrid in the XAML to VirtualizingStackPanel.IsVirtualizing="False"
                //             but that didn't seem to work.
                // As an alternate, we  do the UpdataLayout and ScrollIntoView which forces it to appear
                dataGrid.UpdateLayout();
                dataGrid.ScrollIntoView(rowIndex + 1);
                DataGridRow row = (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex);
                if (row == null)
                {
                    continue;
                }

                // grid cells are editable by default
                // disable cells which should not be editable
                DataGridCellsPresenter presenter = VisualChildren.GetVisualChild<DataGridCellsPresenter>(row);
                for (int column = 0; column < dataGrid.Columns.Count; column++)
                {
                    DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(column);
                    if (cell == null)
                    {
                        // cell will be null for columns with Visibility = Hidden
                        continue;
                    }


                    MetadataControlRow control = new(((DataRowView)dataGrid.Items[rowIndex]).Row);
                    string controlType = control.Type;

                    // These columns should always be editable
                    // Note that Width is normally editable unless it is a Flag (as the checkbox is set to the optimal width)
                    string columnHeader = (string)dataGrid.Columns[column].Header;

                    // If we are using the camtrapDP standard and are checking the Data Label column,
                    // get the data label and check to see if it is defined in that standard
                    //bool isStandardDatalabel = false;
                    //if (isCamtrapDP && columnHeader == EditorConstant.ColumnHeader.DataLabel && cell.Content is TextBlock tb)
                    //{
                    //    isStandardDatalabel = CamtrapDPStandard.MediaAndObservationValues.Contains(tb.Text);
                    //}

                    if (columnHeader == Control.Label ||
                        columnHeader == Control.Tooltip ||
                        columnHeader == Control.Visible
                        )
                    {
                        cell.SetValue(System.Windows.Controls.Control.IsTabStopProperty, true); // Allow tabbing in non-editable fields
                        continue;
                    }

                    // The following attributes should NOT be editable
                    ContentPresenter cellContent = cell.Content as ContentPresenter;
                    string sortMemberPath = dataGrid.Columns[column].SortMemberPath;
                    if (
                        // Types are never editable
                        String.Equals(sortMemberPath, DatabaseColumn.ID, StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(sortMemberPath, Control.ControlOrder, StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(sortMemberPath, Control.SpreadsheetOrder, StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(sortMemberPath, Control.Type, StringComparison.OrdinalIgnoreCase) ||

                        // These four standard controls are treated as special cases, where certain columns are never editable
                        (controlType == DatabaseColumn.File &&
                            (columnHeader == Control.Type ||
                             columnHeader == EditorConstant.ColumnHeader.DefaultValue ||
                             columnHeader == EditorConstant.ColumnHeader.DataLabel ||
                             columnHeader == Control.List ||
                             columnHeader == Control.Copyable ||
                             columnHeader == EditorConstant.ColumnHeader.Export)) ||
                        (controlType == DatabaseColumn.RelativePath &&
                            (columnHeader == Control.Type ||
                             columnHeader == EditorConstant.ColumnHeader.DefaultValue ||
                             columnHeader == EditorConstant.ColumnHeader.DataLabel ||
                             columnHeader == Control.List ||
                             columnHeader == Control.Copyable ||
                             columnHeader == EditorConstant.ColumnHeader.Export)) ||
                        (controlType == DatabaseColumn.DateTime &&
                            (columnHeader == Control.Type ||
                             columnHeader == EditorConstant.ColumnHeader.DefaultValue ||
                             columnHeader == EditorConstant.ColumnHeader.DataLabel ||
                             columnHeader == Control.List ||
                             columnHeader == Control.Copyable)) ||
                        (controlType == DatabaseColumn.DeleteFlag &&
                            (columnHeader == Control.Type ||
                             columnHeader == EditorConstant.ColumnHeader.DefaultValue ||
                             columnHeader == EditorConstant.ColumnHeader.DataLabel ||
                             columnHeader == Control.List ||
                             columnHeader == Control.Copyable ||
                             columnHeader == EditorConstant.ColumnHeader.Width)) ||

                        // These default values are editable but if we want to change that, uncomment this
                        //(controlType == Control.DateTime_ && columnHeader == EditorConstant.ColumnHeader.DefaultValue) ||
                        //(controlType == Control.Date_ && columnHeader == EditorConstant.ColumnHeader.DefaultValue) ||
                        //(controlType == Control.Time_ && columnHeader == EditorConstant.ColumnHeader.DefaultValue) ||
                        //(controlType == Control.MultiChoice && columnHeader == EditorConstant.ColumnHeader.DefaultValue) ||

                        // Flag widths are never editable
                        (controlType == Control.Flag && columnHeader == EditorConstant.ColumnHeader.Width) ||

                        // Any control's List except for FixedChoice and MultiChoice are never editable
                        ((controlType != Control.FixedChoice && controlType != Control.MultiChoice) && columnHeader == Control.List) ||

                        // If its a camtrapDP file...
                        (isCamtrapDP &&

                            // camtrapDP type, lists, export should not be editable at any level, unless its an added custom field
                            ((columnHeader == Control.Type ||
                               columnHeader == Control.List ||
                               columnHeader == "Export") &&
                              (level == -1 && CamtrapDPHelpers.IsMediaOrObservationField(control.DataLabel)) ||
                              (level == 1 && CamtrapDPHelpers.IsDataPackageField(control.DataLabel)) ||
                              (level == 2 && CamtrapDPHelpers.IsDeploymentField(control.DataLabel))) ||

                             // camtrapDP data labels should not be editable at any level, unless its an added custom field
                             (columnHeader == EditorConstant.ColumnHeader.DataLabel &&
                                (level == -1 && CamtrapDPHelpers.IsMediaOrObservationField(control.DataLabel)) ||
                                (level == 1 && CamtrapDPHelpers.IsDataPackageField(control.DataLabel)) ||
                                (level == 2 && CamtrapDPHelpers.IsDeploymentField(control.DataLabel))) ||

                             // camtrapDP lists should not be editable 

                             // a subset of camtrapDP default fields should not be editable at any level, but added custom fields are always editable
                             (columnHeader == EditorConstant.ColumnHeader.DefaultValue &&
                              (level == -1 && CamtrapDPHelpers.IsMediaObservationsFieldNonEditableDefault(control.DataLabel)) ||
                              (level == 1 && columnHeader == EditorConstant.ColumnHeader.DefaultValue && CamtrapDPHelpers.IsDataPackageFieldNonEditableDefault(control.DataLabel)) ||
                              (level == 2 && columnHeader == EditorConstant.ColumnHeader.DefaultValue && CamtrapDPHelpers.IsDeploymentFieldNonEditableDefault(control.DataLabel)))
                         )
                       )

                    {
                        // Disable the Type ComboBox for the standard (required) controls
                        if (columnHeader == Control.Type && (controlType == DatabaseColumn.File || controlType == DatabaseColumn.RelativePath ||
                                                            controlType == DatabaseColumn.DateTime || controlType == DatabaseColumn.DeleteFlag))
                        {
                            if (cellContent?.ContentTemplate.FindName("typeComboBox", cellContent) is ComboBox comboBox)
                            {
                                comboBox.IsEditable = false;
                                comboBox.IsEnabled = false;
                                comboBox.Focusable = false;
                                cell.IsEditing = false;
                                //cell.IsEnabled = false;
                                if (controlType == DatabaseColumn.File) cell.ToolTip = "File field is filled in automatically by the system with the file name of the image or video";
                                else if (controlType == DatabaseColumn.RelativePath) cell.ToolTip = "RelativePath field is filled in automatically by the system with the path of the image or video from the root folder";
                                else if (controlType == DatabaseColumn.DateTime) cell.ToolTip = "DateTime field is filled in automatically by the system with the creation time of the image or video file";
                                else if (controlType == DatabaseColumn.DeleteFlag) cell.ToolTip = "DeleteFlag field is a standaard Timelapse control used to mark image or video files for later deletion";
                                Border border = VisualChildren.GetVisualChild<Border>(comboBox);
                                if (null != border)
                                {
                                    border.Background = EditorConstant.NotEditableCellColor;
                                }
                            }
                        }

                        if (controlType == Control.Date_ && columnHeader == EditorConstant.ColumnHeader.DefaultValue)
                        {
                            // Date_ values are the complete date/time. As we only want to display the date portion, we trim the Time_ portion off here.
                            if (cell.Content is TextBlock textBlock && false == string.IsNullOrWhiteSpace(textBlock.Text))
                            {
                                textBlock.Text = textBlock.Text.Split()[0];
                            }
                        }
                        else if (controlType == Control.Time_ && columnHeader == EditorConstant.ColumnHeader.DefaultValue)
                        {
                            // Time_ values are the complete date/time. As we only want to display the time portion, we trim the Date_ portion here.
                            if (cell.Content is TextBlock textBlock && false == string.IsNullOrWhiteSpace(textBlock.Text))
                            {
                                string[] splitText = textBlock.Text.Split();
                                if (splitText.Length > 1)
                                {
                                    textBlock.Text = textBlock.Text.Split()[1];
                                }
                            }
                        }
                        cell.Background = EditorConstant.NotEditableCellColor;
                        cell.Foreground = Brushes.Gray;
                        cell.SetValue(System.Windows.Controls.Control.IsTabStopProperty, false);  // Disallow tabbing in non-editable fields

                        // This actually disallows editing 
                        cell.IsEditing = false;

                        // if cell has a checkbox, also disable it.
                        if (cellContent?.ContentTemplate.FindName("CheckBox", cellContent) is CheckBox checkbox)
                        {
                            checkbox.IsEnabled = false;
                        }
                    }
                    else
                    {
                        // An editable cell
                        cell.ClearValue(System.Windows.Controls.Control.BackgroundProperty); // otherwise when scrolling cells offscreen get colored randomly
                        cell.SetValue(System.Windows.Controls.Control.IsTabStopProperty, true);
                        // if cell has a checkbox, enable it.
                        if (cellContent != null)
                        {
                            if (cellContent.ContentTemplate.FindName("CheckBox", cellContent) is CheckBox checkbox)
                            {
                                checkbox.IsEnabled = true;
                            }

                            // For some odd reason, the style setting which should do this is not working,
                            // So I have to hard code making the List button for Choice types visible.
                            if (controlType == Control.FixedChoice &&
                                columnHeader == Control.List &&
                                cellContent.ContentTemplate.FindName("btnButton", cellContent) is Button button)
                            {
                                button.Visibility = Visibility.Visible;
                                button.Background = Brushes.White;
                            }
                            if (controlType == Control.MultiChoice &&
                                columnHeader == Control.List &&
                                cellContent.ContentTemplate.FindName("btnButton", cellContent) is Button button2)
                            {
                                button2.Visibility = Visibility.Visible;
                                button2.Background = Brushes.White;
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region Retrieving cells from the datagrid
        // If we can, return the curentCell and the current Row
        public static bool TryGetCurrentCell(DataGrid dataGrid, out DataGridCell currentCell, out DataGridRow currentRow)
        {
            if (dataGrid.SelectedIndex == -1 || dataGrid.CurrentColumn == null)
            {
                currentCell = null;
                currentRow = null;
                return false;
            }

            currentRow = (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromIndex(dataGrid.SelectedIndex);
            DataGridCellsPresenter presenter = VisualChildren.GetVisualChild<DataGridCellsPresenter>(currentRow);
            currentCell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(dataGrid.CurrentColumn.DisplayIndex);
            return currentCell != null;
        }

        public static bool TryGetCurrentEditableCell(DataGrid dataGrid, out DataGridCell currentCell, out DataGridRow currentRow)
        {
            return TryGetCurrentCell(dataGrid, out currentCell, out currentRow)
                   && !currentCell.Background.Equals(EditorConstant.NotEditableCellColor);
        }
        #endregion

        #region Utilities
        public static bool IsRowOrColumNull(DataGridRow row, DataGridColumn column)
        {
            if (row == null)
            {
                return true;
            }

            if (column == null)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region Callback: Type ComboBox specific handlers
        // Manipulate the TypeComboBox dropdown based on its current value,
        // where we disable the visibility of items that don't make sense for the current type.
        // That is, make it so the use can only select an item that changes from one type to another equivalent, or to a more general type.
        public static void DoTypeComboBoxDropDownOpened(ComboBox comboBox, string type)
        {

            bool isShift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            // If the shift key is held down, show all menu items
            if (isShift)
            {
                foreach (ComboBoxItem item in comboBox.Items)
                {
                    string itemType = (string)item.Content;
                    item.Visibility = IsCondition.IsStandardControlType(itemType)
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                }
                return;
            }

            foreach (ComboBoxItem item in comboBox.Items)
            {

                string itemType = (string)item.Content;
                switch (type)
                {
                    case Control.Note:
                    case Control.MultiLine:
                        item.Visibility = itemType == Control.MultiLine ||
                                          itemType == Control.Note
                           ? Visibility.Visible : Visibility.Collapsed;
                        break;

                    case Control.AlphaNumeric:
                        item.Visibility = itemType == Control.MultiLine ||
                                          itemType == Control.Note ||
                                          itemType == Control.AlphaNumeric
                            ? Visibility.Visible : Visibility.Collapsed;
                        break;

                    case Control.IntegerPositive:
                    case Control.Counter:
                        item.Visibility = itemType == Control.Counter ||
                                          itemType == Control.IntegerPositive ||
                                          itemType == Control.IntegerAny ||
                                          itemType == Control.DecimalPositive ||
                                          itemType == Control.DecimalAny ||
                                          itemType == Control.MultiLine ||
                                          itemType == Control.Note ||
                                          itemType == Control.AlphaNumeric
                            ? Visibility.Visible : Visibility.Collapsed;
                        break;

                    case Control.IntegerAny:
                        item.Visibility = itemType == Control.IntegerAny ||
                                          itemType == Control.DecimalAny ||
                                          itemType == Control.MultiLine ||
                                          itemType == Control.Note ||
                                          itemType == Control.AlphaNumeric
                            ? Visibility.Visible : Visibility.Collapsed;
                        break;
                    case Control.DecimalPositive:
                        item.Visibility = itemType == Control.DecimalPositive ||
                                          itemType == Control.DecimalAny ||
                                          itemType == Control.MultiLine ||
                                          itemType == Control.Note
                            ? Visibility.Visible : Visibility.Collapsed;
                        break;
                    case Control.DecimalAny:
                        item.Visibility = itemType == Control.DecimalAny ||
                                          itemType == Control.MultiLine ||
                                          itemType == Control.Note
                            ? Visibility.Visible : Visibility.Collapsed;
                        break;

                    case Control.FixedChoice:
                        item.Visibility = itemType == Control.FixedChoice ||
                                          itemType == Control.MultiChoice ||
                                          itemType == Control.MultiLine ||
                                          itemType == Control.Note
                            ? Visibility.Visible : Visibility.Collapsed;
                        break;
                    case Control.MultiChoice:
                        item.Visibility = itemType == Control.MultiChoice ||
                                          itemType == Control.MultiLine ||
                                          itemType == Control.Note
                            ? Visibility.Visible : Visibility.Collapsed;
                        break;

                    case Control.DateTime_:
                    case Control.Date_:
                    case Control.Time_:
                        // While it apparantly makes sense to convert between date types, we don't do that as
                        // we would then have to go into the database and convert all the values as well.
                        item.Visibility = itemType == Control.MultiLine ||
                                          itemType == Control.Note
                            ? Visibility.Visible : Visibility.Collapsed;
                        break;
                    case Control.Flag:
                        item.Visibility = itemType is Control.Flag or Control.MultiLine or Control.Note or Control.AlphaNumeric
                            ? Visibility.Visible : Visibility.Collapsed;
                        break;
                    default:
                        item.Visibility = Visibility.Collapsed;
                        break;
                }
            }
        }

        // Change the type field if it differs from whatever is already there.
        public static bool DoTypeComboBox_SelectionChanged(ComboBox comboBox, IList removedItems, string currentType, string currentDefaultValue, string jsonList, out string newType, out string newDefaultValue)
        {
            newType = currentType;
            newDefaultValue = currentDefaultValue;

            ComboBoxItem item = comboBox.SelectedItem as ComboBoxItem;

            // Note that this test also checks to make sure that the type differs,
            // If not, it will fall through and return false.
            if (item?.Content is string textContent && currentType != textContent)
            {

                if (false == Dialogs.TypeChangeInformationDialog(Globals.RootEditor, comboBox.Text, textContent))
                {
                    if (removedItems.Count == 1)
                    {
                        comboBox.SelectedItem = removedItems[0];
                    }
                    return false;
                }
                newType = textContent;

                // Check the defaults and change them if its not appropriate
                switch (newType)
                {
                    case DatabaseColumn.File:
                    case DatabaseColumn.RelativePath:
                    case DatabaseColumn.DateTime:
                    case DatabaseColumn.DeleteFlag:
                        // Should never change the type of the standard controls
                        return false;
                    case Control.Note:
                    case Control.MultiLine:
                        // Defaults are always type-safe as everything can be converted to text
                        return true;
                    case Control.AlphaNumeric:
                        if (false == (currentDefaultValue == string.Empty || IsCondition.IsAlphaNumeric(currentDefaultValue)))
                        {
                            newDefaultValue = ControlDefault.AlphaNumericDefaultValue;
                        }
                        return true;
                    case Control.IntegerPositive:
                    case Control.Counter:
                        if (false == (currentDefaultValue == string.Empty || IsCondition.IsIntegerPositive(currentDefaultValue)))
                        {
                            newDefaultValue = ControlDefault.NumberDefaultValue;
                        }
                        return true;
                    case Control.IntegerAny:
                        if (false == (currentDefaultValue == string.Empty || IsCondition.IsInteger(currentDefaultValue)))
                        {
                            newDefaultValue = ControlDefault.NumberDefaultValue;
                        }
                        return true;

                    case Control.DecimalPositive:
                        if (false == (currentDefaultValue == string.Empty || IsCondition.IsDecimalPositive(currentDefaultValue)))
                        {
                            newDefaultValue = ControlDefault.NumberDefaultValue;
                        }
                        return true;
                    case Control.DecimalAny:
                        if (false == (currentDefaultValue == string.Empty || IsCondition.IsDecimal(currentDefaultValue)))
                        {
                            newDefaultValue = ControlDefault.NumberDefaultValue;
                        }
                        return true;

                    case Control.FixedChoice:
                        Choices choices = Choices.ChoicesFromJson(jsonList);
                        // Empties not allowed
                        if (false == choices.IncludeEmptyChoice && (string.IsNullOrEmpty(currentDefaultValue) || false == choices.Contains(currentDefaultValue)))
                        {
                            newDefaultValue = choices.ChoiceList[0];
                        }
                        // If a non-empty default value matches an entry on the edited choice menu, Use that. Otherwise set it to empty
                        else if (!string.IsNullOrEmpty(currentDefaultValue) && choices.Contains(currentDefaultValue) == false)
                        {
                            newDefaultValue = string.Empty;
                        }
                        return true;

                    //    break;
                    case Control.MultiChoice:

                        if (string.IsNullOrWhiteSpace(currentDefaultValue))
                        {
                            // An empty default is always valid
                            return true;
                        }
                        // Parse the current comma-separated textBox items as a list
                        Choices multiChoices = Choices.ChoicesFromJson(jsonList);
                        List<string> sortedList = [];
                        string[] parsedText = currentDefaultValue.Split(',');
                        foreach (string str in parsedText)
                        {
                            if (multiChoices.Contains(str) == false)
                            {
                                newDefaultValue = ControlDefault.MultiChoiceDefaultValue;
                                return true;
                            }
                            sortedList.Add(str);
                        }
                        // If we get here, it means the current default is valid, but make sure its sorted correctly
                        sortedList.Sort();
                        newDefaultValue = string.Join(",", sortedList).Trim(',');
                        // If we get here, it means the current default is valid
                        return true;

                    case Control.DateTime_:
                        if (false == IsCondition.IsDateTime(currentDefaultValue))
                        {
                            newDefaultValue = DateTimeHandler.ToStringDatabaseDateTime(ControlDefault.DateTimeCustomDefaultValue);
                        }
                        return true;
                    case Control.Date_:
                        if (false == IsCondition.IsDateTime(currentDefaultValue))
                        {
                            newDefaultValue = DateTimeHandler.ToStringDatabaseDate(ControlDefault.Date_DefaultValue);
                        }
                        return true;
                    case Control.Time_:
                        if (false == IsCondition.IsTime(currentDefaultValue))
                        {
                            newDefaultValue = DateTimeHandler.ToStringTime(ControlDefault.Time_DefaultValue);
                        }
                        return true;

                    case Control.Flag:
                        if (false == (currentDefaultValue == string.Empty || IsCondition.IsBoolean(currentDefaultValue)))
                        {
                            newDefaultValue = ControlDefault.FlagValue;
                        }
                        return true;
                    default:
                        return false;
                }
            }
            return false;
        }
        #endregion
    }

    #region Class CellTextBlockConverter
    public class CellTextBlockConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string valString = value as string;
            if (!string.IsNullOrEmpty(valString))
            {
                return valString.Trim();
            }
            return string.Empty;
        }
    }
    #endregion
}
