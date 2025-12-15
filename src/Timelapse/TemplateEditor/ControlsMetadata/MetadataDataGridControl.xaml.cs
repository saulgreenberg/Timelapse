using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.ControlsCore;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Standards;
using Timelapse.Util;
using TimelapseTemplateEditor.Dialog;
using TimelapseTemplateEditor.EditorCode;
using Control = Timelapse.Constant.Control;

namespace TimelapseTemplateEditor.ControlsMetadata
{
    // MetadataDataGridControl and TemplateDataControl are similar, and thus share some static methods in DataGridCommonCode
    // Differences are due to the different control types, and the differing columns in the datagrid.
    public partial class MetadataDataGridControl
    {
        public MetadataTabControl ParentTab { get; set; }
        public DataGrid DataGridInstance { get; set; }
        public ObservableCollection<DataGridColumn> Columns => this.DataGridInstance.Columns;
        private int LastRowCount = -1; // Tracked to see if we need to update the layout: only if row count changes

        #region Constructor, Loaded, LayoutUpdated

        public MetadataDataGridControl()
        {
            InitializeComponent();
            this.DataGridInstance = this.DataGrid;
        }

        // Update the layout when its first created
        private void DataGrid_OnLoaded(object sender, RoutedEventArgs e)
        {
            this.DoLayoutUpdated(true);
        }

        // Updates colors when rows are added, moved, or deleted.
        // Only done if row count changes
        private void MetadataDataGrid_LayoutUpdated(object sender, EventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(new()))
            {
                return;
            }
            this.DoLayoutUpdated(false);
        }
        #endregion

        #region Public methods
        // Update the layout
        public void DoLayoutUpdated(bool alwaysUpdate)
        {
            // The sender null forces this when its not a callback i.e. when invoked from the preview panel
            if (alwaysUpdate || DataGrid.Items.Count != LastRowCount)
            {
                LastRowCount = DataGrid.Items.Count;
                DataGridCommonCode.UpdateCellEditabilityAndVisibility(DataGrid, Globals.Root.standardType, this.ParentTab.Level);
            }
        }
        #endregion

        #region Public Callback: RowChanged

        // Whenever a row changes, save that row to the database. This also updates the grid colors.
        // Note that bulk changes due to code update defers this, so that updates can be done collectively and more efficiently later
        // This method is bound to the DataGrid from an external method when a new data grid is loaded
        public void MetadataDataGrid_RowChanged(object sender, DataRowChangeEventArgs e)
        {
            if (Globals.Root.dataGridBeingUpdatedByCode == false)
            {
                Globals.Root.DoSyncMetadataControlToDatabase(new(e.Row));
                this.DoLayoutUpdated(true);
            }
        }

        #endregion

        #region Callback: BeginningEdit, CurrentCellChanged, and CellEditEnding

        /// <summary>
        /// *** Before cell editing begins on a cell click, the cell is disabled if it is grey (meaning cannot be edited).
        /// Another method re-enables the cell immediately afterwards.
        /// The reason for this implementation is because disabled cells cannot be single clicked, which is needed for row actions.
        /// </summary>
        private void MetadataDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            bool isStandardButNotCamtrapDP = EditorConstant.templateEditorWindow.standardType != string.Empty &&
                                             EditorConstant.templateEditorWindow.standardType != Timelapse.Constant.Standards.CamtrapDPStandard;
            // Generate a warning if the user tries to edit a data label when using a standard (except CamtrapDP, as the dataLabel fields will be disabled)
            if ((string)e.Column.Header == EditorConstant.ColumnHeader.DataLabel
                && isStandardButNotCamtrapDP
                && false == Dialogs.ChangesToStandardWarning(EditorConstant.templateEditorWindow, "Editing controls", EditorConstant.templateEditorWindow.standardType))
            {
                e.Cancel = true;
                return;
            }

            DataGridCommonCode.BeginningEdit(DataGrid);
        }

        // After cell editing ends (prematurely or no), re-enable disabled cells.
        // See MetadataDataGrid_BeginningEdit below for full explanation.
        private void MetadataDataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            DataGridCommonCode.CurrentCellChanged(DataGrid);
        }

        // After editing is complete, validate the data labels, default values, and widths as needed.
        // Also commit the edit, which will raise the RowChanged event
        private bool manualCommitEdit;

        private void MetadataDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Stop re-entering, which can occur after we manually perform a CommitEdit (see below) 
            if (manualCommitEdit)
            {
                manualCommitEdit = false;
                return;
            }

            if (DataGridCommonCode.IsRowOrColumNull(e.Row, e.Column))
            {
                return;
            }

            switch ((string)e.Column.Header)
            {
                case EditorConstant.ColumnHeader.DataLabel:
                    ValidateDataLabel(e);
                    break;
                case EditorConstant.ColumnHeader.DefaultValue:
                    ValidateDefaults(e, e.Row);
                    break;
                case Control.Label:
                    ValidateLabels(e, e.Row);
                    break;
                case EditorConstant.ColumnHeader.Width:
                    ValidateWidths(e, e.Row);
                    break;
                    //default: no restrictions on any of the other editable columns
            }

            // While hitting return after editing (say) a note will raise a RowChanged event, 
            // clicking out of that cell does not raise a RowChangedEvent, even though the cell has been edited. 
            // Thus we manually commit the edit. 
            Globals.RootEditor.dataGridBeingUpdatedByCode = false;
            manualCommitEdit = true;
            DataGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        #endregion

        #region Callback: PreviewKeyDown, PreviewTextInput

        // Cell editing: Preview character by character entry to disallow spaces in particular fields (DataLabels, Width, Counters)
        private void MetadataDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Ignore non-editable cells
            if (false == DataGridCommonCode.TryGetCurrentEditableCell(DataGrid, out DataGridCell cell, out DataGridRow currentRow))
            {
                e.Handled = true;
                return;
            }

            switch ((string)DataGrid.CurrentColumn.Header)
            {
                case EditorConstant.ColumnHeader.DataLabel:

                    if (e.Key == Key.Space)
                    {
                        // Datalabels should not accept spaces - display a warning if needed
                        Dialogs.EditorDataLabelRequirementsDialog(Globals.RootEditor);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Tab)
                    {
                        // Commit the edit before going to the next cell
                        DataGridCommonCode.ApplyPendingEdits(DataGrid);
                    }

                    break;
                case EditorConstant.ColumnHeader.Width:
                    // Width should  not accept spaces 
                    ValidationCallbacks.PreviewKeyDown_TextBoxNoSpaces(cell.Content, e);
                    break;

                case EditorConstant.ColumnHeader.DefaultValue:
                    // These controls should not accept spaces 
                    ControlRow control = new((currentRow.Item as DataRowView)?.Row);
                    switch (control.Type)
                    {
                        case Control.AlphaNumeric:
                        case Control.IntegerAny:
                        case Control.IntegerPositive:
                        case Control.DecimalAny:
                        case Control.DecimalPositive:
                        case Control.Date_:
                        case Control.Time_:
                        case Control.Flag:
                            ValidationCallbacks.PreviewKeyDown_TextBoxNoSpaces(cell.Content, e);
                            break;
                    }
                    break;
            }
        }

        // Cell editing: Preview string entry.
        // Accept only numbers in counters and widths, t and f in flags (translated to true and false), and alphanumeric or _ in datalabels
        private void MetadataDataGrid_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (false == DataGridCommonCode.TryGetCurrentEditableCell(DataGrid, out DataGridCell cell, out DataGridRow currentRow))
            {
                e.Handled = true;
                return;
            }

            switch ((string)DataGrid.CurrentColumn.Header)
            {
                // Data label: Restrict Inputs
                case EditorConstant.ColumnHeader.DataLabel:
                    // Only allow alphanumeric and '_' in data labels
                    if (!IsCondition.IsLetterDigitUnderscoreCharacters(e.Text))
                    {
                        Dialogs.EditorDataLabelRequirementsDialog(Globals.RootEditor);
                        e.Handled = true;
                    }
                    break;

                case EditorConstant.ColumnHeader.DefaultValue:
                    // Restrict certain default values for counters, flags (and perhaps fixed choices in the future)
                    MetadataControlRow control = new((currentRow.Item as DataRowView)?.Row);
                    switch (control.Type)
                    {
                        // Text character input limits
                        case Control.AlphaNumeric:
                            ValidationCallbacks.PreviewInput_AlphaNumericCharacterOnly(cell.Content, e);
                            e.Handled = !IsCondition.IsAlphaNumeric(e.Text);
                            break;

                        // Number character input limits
                        case Control.Counter:
                        case Control.IntegerPositive:
                            // Only allow positive integer characters 
                            ValidationCallbacks.PreviewInput_IntegerPositiveCharacterOnly(cell.Content, e);
                            break;
                        case Control.IntegerAny:
                            ValidationCallbacks.PreviewInput_IntegerCharacterOnly(cell.Content, e);
                            break;
                        case Control.DecimalPositive:
                            ValidationCallbacks.PreviewInput_DecimalPositiveCharacterOnly(cell.Content, e);
                            break;
                        case Control.DecimalAny:
                            ValidationCallbacks.PreviewInput_DecimalCharacterOnly(cell.Content, e);
                            break;

                        // Flag character input limits
                        case Control.Flag:
                            // Only allow t/f and translate to true/false
                            // Otherwise flash the control
                            if (ControlsDataHelpers.TextBoxHandleFlagPreviewInput((TextBox)cell.Content, e, out string newValue))
                            {
                                control.DefaultValue = newValue;
                                Globals.RootEditor.DoSyncMetadataControlToDatabase(control);
                            }
                            e.Handled = true;
                            break;

                        // Choice character input limits
                        case Control.FixedChoice:
                        case Control.MultiChoice:
                            // The default value should be constrained to one of the choices, but that introduces a chicken and egg problem
                            // So lets just ignore it for now.
                            break;

                        // Date character input  limits
                        // While these don't correct for wrong orders, they will at least eliminate spurious characters
                        case Control.DateTime_:
                            e.Handled = !IsCondition.IsDateTimeDataBaseFormatCharacters(e.Text);
                            break;
                        case Control.Date_:
                            e.Handled = !IsCondition.IsDateDataBaseFormatCharacters(e.Text);
                            break;
                        case Control.Time_:
                            e.Handled = !IsCondition.IsTimeCharacters(e.Text);
                            break;
                    }
                    break;
            }
        }

        #endregion

        #region Callback: SelectionChanged

        // Logic to enable/disable editing buttons depending on there being a row selection
        // Also sets the text for the remove row button.
        // TODO: THE REMOVE BUTTON STAYS ON EVEN WHEN WE ARE OFF THE DATAGRID
        private void MetadataDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGrid.SelectedItem is DataRowView selectedRowView)
            {
                MetadataControlRow control = new(selectedRowView.Row);
                ParentTab.MetadataEditRowControls.RemoveControlButton.IsEnabled =

                    // Disable the remove control for standard controls
                    !Control.StandardTypes.Contains(control.Type) &&

                    // Disable the remove control for known CamtrapDP fields
                    !(Globals.Root.standardType == Timelapse.Constant.Standards.CamtrapDPStandard &&
                         CamtrapDPHelpers.IsDataPackageDeploymentField(control.DataLabel));

                // Now find the corresponding entry in the MetadataPreviewPanel and scroll it into view
                ParentTab.MetadataDataEntryPreviewPanel.ScrollLabelIntoView(control.Label);
            }
            else
            {
                ParentTab.MetadataEditRowControls.RemoveControlButton.IsEnabled = false;
            }
        }

        #endregion

        #region Callback: ChoiceListButton_Click

        // When the  choice list button is clicked, raise a dialog box that lets the user edit the list of choices
        private void ChoiceListButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;

            // The button tag holds the Control Order of the row the button is in, not the ID.
            // So we have to search through the rows to find the one with the correct control order
            // and retrieve / set the ItemList menu in that row.
            MetadataControlRow choiceControl = Globals.TemplateDatabase.MetadataControlsByLevel[ParentTab.Level]
                .FirstOrDefault(control => control.ControlOrder.ToString().Equals(button.Tag.ToString()));
            if (choiceControl == null)
            {
                TracePrint.PrintMessage($"Control named {button.Tag} not found.");
                return;
            }

            Choices choices = Choices.ChoicesFromJson(choiceControl.List);
            bool showEmptyChoiceOption = choiceControl.Type == Control.FixedChoice;
            EditChoiceList choiceListDialog = new(button, choices, showEmptyChoiceOption, Globals.RootEditor);
            bool? result = choiceListDialog.ShowDialog();
            if (result == true)
            {
                // Ensure that choices disallowing empties have an appropriate default value
                if (false == choiceListDialog.Choices.IncludeEmptyChoice &&
                    (string.IsNullOrEmpty(choiceControl.DefaultValue) || false == choiceListDialog.Choices.Contains(choiceControl.DefaultValue)))
                {
                    Dialogs.EditorDefaultChoiceValuesMustMatchNonEmptyChoiceListsDialog(Globals.RootEditor, choiceControl.DefaultValue);
                    choiceControl.DefaultValue = choiceListDialog.Choices.ChoiceList[0];
                }
                // Ensure that non-empty default values matches an entry on the edited choice menu
                else if (!string.IsNullOrEmpty(choiceControl.DefaultValue) && choiceListDialog.Choices.Contains(choiceControl.DefaultValue) == false)
                {
                    Dialogs.EditorDefaultChoiceValuesMustMatchChoiceListsDialog(Globals.RootEditor, choiceControl.DefaultValue);
                    choiceControl.DefaultValue = string.Empty;
                }

                choiceControl.List = choiceListDialog.Choices.GetAsJson;
                Globals.RootEditor.DoSyncMetadataControlToDatabase(choiceControl);
            }
        }

        #endregion

        #region Callback: EditTooltipButton_Click
        // When the  choice list button is clicked, raise a dialog box that lets the user edit the list of choices
        private void EditTooltipButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            // The button tag holds the Control Order of the row the button is in, not the ID.
            // So we have to search through the rows to find the one with the correct control order
            // and retrieve / set the ItemList menu in that row.
            MetadataControlRow toolTipControl = Globals.TemplateDatabase.MetadataControlsByLevel[ParentTab.Level]
                .FirstOrDefault(control => control.ControlOrder.ToString().Equals(button.Tag.ToString()));

            if (toolTipControl == null)
            {
                TracePrint.PrintMessage($"Control named {button.Tag} not found.");
                return;
            }

            string tooltipText = toolTipControl.Tooltip;
            Dialog.EditTooltipText EditTooltipDialog = new(button, tooltipText, Globals.RootEditor);
            bool? result = EditTooltipDialog.ShowDialog();
            if (result == true)
            {

                toolTipControl.Tooltip = EditTooltipDialog.TooltipText;
                Globals.RootEditor.DoSyncMetadataControlToDatabase(toolTipControl);
            }
        }
        #endregion

        #region Callback: OnLostFocus

        private void DataGrid_OnLostFocus(object sender, RoutedEventArgs e)
        {
            // If the focus goes to the remove button, it means we are trying to remove this row
            // We don't disable the remove button here, because if we did the click event won't fire. 
            IInputElement focusedControl = FocusManager.GetFocusedElement(Globals.RootEditor);
            if (Equals(focusedControl, ParentTab.MetadataEditRowControls.RemoveControlButton))
            {
                return;
            }

            if (DataGrid.SelectedItem is DataRowView selectedRowView)
            {
                MetadataControlRow control = new(selectedRowView.Row);
                ParentTab.MetadataEditRowControls.RemoveControlButton.IsEnabled =
                    DataGrid.IsKeyboardFocusWithin &&

                    // Disable the remove control for standard controls
                    !Control.StandardTypes.Contains(control.Type) &&

                    // Disable the remove control for known CamtrapDP fields
                    !(Globals.Root.standardType == Timelapse.Constant.Standards.CamtrapDPStandard &&
                         CamtrapDPHelpers.IsDataPackageDeploymentField(control.DataLabel));
            }
        }

        #endregion

        #region Validation of Cell contents (most are SAME except invokes MetadataControlRow instead of ControlRow)

        // *** Validate the data label to correct for empty, duplicate, or non-legal naming
        private void ValidateDataLabel(DataGridCellEditEndingEventArgs e)
        {

            // Check to see if the data label entered is a reserved word or if its a non-unique label
            if (e.EditingElement is not TextBox textBox)
            {
                TracePrint.NullException();
                return;
            }

            // A crash report was generated in this method, but I don't know where and can't replicate.
            // So we'll add a try-catch to log any exceptions
            try
            {
                if (Globals.TemplateDatabase.MetadataControlsByLevel == null)
                {
                    return;
                }

                string dataLabel = textBox.Text;

                // Check to see if the data label is empty. If it is, generate a unique data label and warn the user
                if (string.IsNullOrWhiteSpace(dataLabel))
                {
                    Dialogs.EditorDataLabelsCannotBeEmptyDialog(Globals.RootEditor);
                    textBox.Text = Globals.TemplateDatabase.GetNextUniqueDataLabelInMetadataControls(ParentTab.Level, "DataLabel");
                }

                // Check to see if the data label is unique. If not, generate a unique data label and warn the user
                for (int row = 0; row < Globals.TemplateDatabase.MetadataControlsByLevel[ParentTab.Level].RowCount; row++)
                {
                    MetadataControlRow control = Globals.TemplateDatabase.MetadataControlsByLevel[ParentTab.Level][row];
                    if (dataLabel.Equals(control.DataLabel))
                    {
                        if (DataGrid.SelectedIndex == row)
                        {
                            continue; // Its the same row, so its the same key, so skip it
                        }

                        Dialogs.EditorDataLabelsMustBeUniqueDialog(Globals.RootEditor, textBox.Text);
                        textBox.Text = Globals.TemplateDatabase.GetNextUniqueDataLabelInMetadataControls(ParentTab.Level, dataLabel);
                        break;
                    }
                }

                // Check to see if the datalabel (if its not empty, which it shouldn't be) has any illegal characters.
                // Note that most of this is redundant, as we have already checked for illegal characters as they are typed. However,
                // we have not checked to see if the first letter is alphabetic.
                if (dataLabel.Length > 0)
                {
                    Regex alphanumdash = new("^[a-zA-Z0-9_]*$");
                    Regex alpha = new("^[a-zA-Z]*$");

                    string firstCharacter = dataLabel[0].ToString();

                    if (!(alpha.IsMatch(firstCharacter) && alphanumdash.IsMatch(dataLabel)))
                    {
                        string replacementDataLabel = dataLabel;

                        if (!alpha.IsMatch(firstCharacter))
                        {
                            replacementDataLabel = "X" + replacementDataLabel[1..];
                        }

                        replacementDataLabel = Regex.Replace(replacementDataLabel, @"[^A-Za-z0-9_]+", "X");

                        Dialogs.EditorDataLabelIsInvalidDialog(Globals.RootEditor, textBox.Text, replacementDataLabel);
                        textBox.Text = replacementDataLabel;
                    }
                }

                // Check to see if its a reserved word
                foreach (string sqlKeyword in EditorConstant.ReservedSqlKeywords)
                {
                    if (String.Equals(sqlKeyword, dataLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        if (dataLabel == "Date" || dataLabel == "Time")
                        {
                            Dialogs.EditorDateAndTimeLabelAreReservedWordsDialog(Globals.RootEditor, textBox.Text, dataLabel == "Date");
                        }
                        else
                        {
                            Dialogs.EditorDataLabelIsAReservedWordDialog(Globals.RootEditor, textBox.Text);
                        }
                        textBox.Text += "_";
                        break;
                    }
                }
            }
            catch (Exception exception)
            {
                new Random().Next(100);
                string replacementText = "DataLabel_" + new Random().Next(99);
                Dialogs.EditorDataLabelIsInvalidDialog(Globals.RootEditor, "", replacementText);
                textBox.Text = replacementText;
                TracePrint.PrintMessage(exception.Message);
                
            }
        }

        // *** Check to see if the current label is a duplicate of another label
        private void ValidateLabels(DataGridCellEditEndingEventArgs e, DataGridRow currentRow)
        {
            bool editorDialogAlreadyShown = false;

            // ControlRow currentControl = new ControlRow((currentRow.Item as DataRowView).Row);
            if (e.EditingElement is TextBox textBox)
            {
                string label = textBox.Text;
                // Check to see if the data label is empty. If it is, generate a unique data label and warn the user
                if (string.IsNullOrWhiteSpace(label))
                {
                    Dialogs.EditorLabelsCannotBeEmptyDialog(Globals.RootEditor);
                    if (currentRow != null)
                    {
                        DataGridCellsPresenter presenter = VisualChildren.GetVisualChild<DataGridCellsPresenter>(currentRow);

                        // try to get the cell but it may possibly be virtualized
                        DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(7);
                        string s = (cell == null)
                            ? "Label"
                            : ((TextBlock)cell.Content).Text;
                        textBox.Text = s; //this.TemplateDatabase.GetNextUniqueLabelInControls(s);
                        label = s;
                        editorDialogAlreadyShown = true;
                    }
                }

                // Check to see if the data label is unique. If not, generate a unique data label and warn the user
                for (int row = 0; row < Globals.TemplateDatabase.MetadataControlsByLevel[ParentTab.Level].RowCount; row++)
                {
                    MetadataControlRow control = Globals.TemplateDatabase.MetadataControlsByLevel[ParentTab.Level][row];
                    if (label.Equals(control.Label))
                    {
                        if (DataGrid.SelectedIndex == row)
                        {
                            continue; // Its the same row, so its the same key, so skip it
                        }

                        if (false == editorDialogAlreadyShown)
                        {
                            Dialogs.EditorLabelsMustBeUniqueDialog(Globals.RootEditor, label);
                        }

                        textBox.Text = Globals.TemplateDatabase.GetNextUniqueLabelInMetadataControls(ParentTab.Level, label);
                        break;
                    }
                }
            }
        }

        // Validation of Defaults: Particular defaults (Flags) cannot be empty 
        // There is no need to check other validations here (e.g., unallowed characters) as these will have been caught previously 
        private static void ValidateDefaults(DataGridCellEditEndingEventArgs e, DataGridRow currentRow)
        {
            MetadataControlRow control = new((currentRow.Item as DataRowView)?.Row);
            if (e.EditingElement is not TextBox textBox)
            {
                TracePrint.NullException();
                return;
            }

            switch (control.Type)
            {
                // AlphaNumeric field
                case Control.AlphaNumeric:
                    ValidationCallbacks.TextChanged_AlphaNumericTextOnly(textBox, null);
                    break;

                // Various Number fields
                case Control.IntegerAny:
                    ValidationCallbacks.TextChanged_IntegerTextOnly(textBox, null, false);
                    break;
                case Control.IntegerPositive:
                    ValidationCallbacks.TextChanged_IntegerTextOnly(textBox, null, true);
                    break;
                case Control.DecimalAny:
                    ValidationCallbacks.TextChanged_DecimalTextOnly(textBox, null, false);
                    break;
                case Control.DecimalPositive:
                    ValidationCallbacks.TextChanged_DecimalTextOnly(textBox, null, true);
                    break;

                // Menu fields
                case Control.FixedChoice:
                    // Check to see if the value matches one of the items on the menu
                    Choices choices = Choices.ChoicesFromJson(control.List);
                    if (choices.IncludeEmptyChoice == false && string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        // Can't have an empty default if the IncludeEmptyChoice is unselecteds
                        Dialogs.EditorDefaultChoiceValuesMustMatchNonEmptyChoiceListsDialog(Globals.RootEditor, textBox.Text);
                        if (choices.ChoiceList.Count > 0)
                        {
                            // Set it to the first item
                            textBox.Text = choices.ChoiceList[0];
                        }
                        // Note: Undefined if we have an empty choice list with IncludeEmptyChoice of false!
                    }
                    else if (string.IsNullOrWhiteSpace(textBox.Text) == false)
                    {
                        ValidationCallbacks.TextChanged_FixedChoiceTextOnly(textBox, null, Choices.ChoicesFromJson(control.List));
                    }
                    break;
                case Control.MultiChoice:
                    // Check to see if the value matches one or more of the items on the menu, and resort them if needed
                    ValidationCallbacks.TextChanged_MultiChoiceTextOnly(textBox, null, Choices.ChoicesFromJson(control.List));
                    break;

                case Control.DateTime_:
                    // Check if its a valid DateTime in database format
                    ValidationCallbacks.TextChanged_DateTimeTextOnly(textBox, null, DateTimeFormatEnum.DateAndTime);
                    break;
                case Control.Date_:
                    ValidationCallbacks.TextChanged_DateTimeTextOnly(textBox, null, DateTimeFormatEnum.DateOnly);
                    break;
                case Control.Time_:
                    ValidationCallbacks.TextChanged_DateTimeTextOnly(textBox, null, DateTimeFormatEnum.TimeOnly);
                    break;

                // Flag fields
                case Control.Flag:
                    ValidationCallbacks.TextChanged_BooleanTextOnly(textBox, null);
                    break;
            }
        }


        // *** Validation of Widths: if a control's width is empty, reset it to its corresponding default width
        private static void ValidateWidths(DataGridCellEditEndingEventArgs e, DataGridRow currentRow)
        {
            if (currentRow?.Item == null || e.EditingElement is not TextBox textBox)
            {
                TracePrint.NullException();
                return;
            }

            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                return;
            }

            MetadataControlRow control = new((currentRow.Item as DataRowView)?.Row);
            textBox.Text = control.Type switch
            {
                DatabaseColumn.File => ControlDefault.FileWidth,
                DatabaseColumn.DateTime => ControlDefault.DateTimeDefaultWidth.ToString(),
                DatabaseColumn.RelativePath => ControlDefault.RelativePathWidth,
                DatabaseColumn.DeleteFlag or Control.Flag => ControlDefault.FlagWidth.ToString(),
                Control.FixedChoice => ControlDefault.FixedChoiceDefaultWidth.ToString(),
                Control.Counter => ControlDefault.CounterWidth.ToString(),
                _ => ControlDefault.NoteDefaultWidth.ToString()
            };
        }

        #endregion

        #region Callback: Type ComboBox specific handlers
        // Manipulate the TypeComboBox dropdown based on its current value,
        // where we disable the visibility of items that don't make sense for the current type.
        // That is, make it so the use can only select an item that changes from one type to another equivalent, or to a more general type.
        private void TypeComboBoxDropDownOpened(object sender, EventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                // The tag holds the Control Order of the row the button is in, not the ID.
                // So we have to search through the rows to find the one with the correct control order
                // and retrieve / set the ItemList menu in that row.
                MetadataControlRow controlRow = Globals.TemplateDatabase.MetadataControlsByLevel[ParentTab.Level].FirstOrDefault(control => control.ControlOrder.ToString().Equals(comboBox.Tag.ToString()));
                if (controlRow == null)
                {
                    TracePrint.PrintMessage($"Control named {comboBox.Tag} not found.");
                    return;
                }
                // The tag holds the Control Order of the row the button is in, not the ID.
                // So we have to search through the rows to find the one with the correct control order
                // and retrieve / set the ItemList menu in that row.
                DataGridCommonCode.DoTypeComboBoxDropDownOpened(comboBox, controlRow.Type);
            }
        }

        // Change the type of a template row. This comes with all sorts of checks and warnings to the user via a dialog.
        private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                if (comboBox.IsDropDownOpen == false)
                {
                    // we only want to continue if this is a user action, as SelectionChanged is also invoked when updating the grid.
                    return;
                }
                MetadataControlRow typeControl = Globals.TemplateDatabase.MetadataControlsByLevel[ParentTab.Level].FirstOrDefault(control => control.ControlOrder.ToString().Equals(comboBox.Tag.ToString()));
                if (null == typeControl || false == DataGridCommonCode.DoTypeComboBox_SelectionChanged(comboBox, e.RemovedItems, typeControl.Type, typeControl.DefaultValue, typeControl.List, out string newType, out string newDefaultValue))
                {
                    e.Handled = true;
                }
                else
                {
                    if (typeControl.DefaultValue == newDefaultValue && typeControl.Type == newType)
                    {
                        // If nothing changed, then do nothing.
                        return;
                    }
                    typeControl.DefaultValue = newDefaultValue;
                    typeControl.Type = newType;
                    Globals.RootEditor.DoSyncMetadataControlToDatabase(typeControl);
                }
            }
        }
        #endregion

    }
}
