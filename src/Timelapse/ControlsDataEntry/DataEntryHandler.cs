using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.Images;
using Timelapse.Util;
using TimelapseWpf.Toolkit;
using TimelapseWpf.Toolkit.Primitives;
using Control = Timelapse.Constant.Control;
using File = System.IO.File;
using MarkableCanvas = Timelapse.Images.MarkableCanvas;
using ThumbnailGrid = Timelapse.Controls.ThumbnailGrid;

namespace Timelapse.ControlsDataEntry
{
    /// <summary>
    /// The code in here propagates values of a control across the various images in various ways.
    /// Note that this is control-type specific, which means this code would have to be modified to handle new control types
    /// Pay attention to the hacks described by SAULXXX WatermarkDateTimePicker Workaround as these may not be needed if future versions of the WatermarkDateTimePicker work as they are supposed to.
    /// </summary>
    public class DataEntryHandler(FileDatabase fileDatabase) : IDisposable
    {
        #region Public Properties and Private variables
        public FileDatabase FileDatabase { get; private set; } = fileDatabase; // We need a reference to the database if we are going to update it.
        public ImageCache ImageCache { get; private set; } = new(fileDatabase);
        public bool IsProgrammaticControlUpdate { get; set; }

        // We need to get selected files from the ThumbnailGrid, so we need this reference
        public ThumbnailGrid ThumbnailGrid { get; set; }
        public MarkableCanvas MarkableCanvas { get; set; }

        // Index location of these menu items in the context menu
        private const int PropagateFromLastValueIndex = 0;
        private const int CopyForwardIndex = 1;
        private const int CopyToAllIndex = 2;
        private const int CopyToClipboardIndex = 4;
        private const int PasteFromClipboardIndex = 5;
        private bool disposed;
        #endregion

        #region Callback Configuration

        /// <summary>
        /// Add data event handler callbacks for (possibly invisible) controls
        /// </summary>
        public void SetDataEntryCallbacks(Dictionary<string, DataEntryControl> controlsByDataLabel)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(controlsByDataLabel, nameof(controlsByDataLabel));

            // Add data entry callbacks to all editable controls. When the user changes a file's attribute using a particular control,
            // the callback updates the matching field for that file in the database.
            foreach (KeyValuePair<string, DataEntryControl> pair in controlsByDataLabel)
            {
                string controlType = FileDatabase.FileTableColumnsByDataLabel[pair.Key].ControlType;
                switch (controlType)
                {
                    case Control.Note:
                    case DatabaseColumn.File:
                    case DatabaseColumn.RelativePath:
                        DataEntryNote note = (DataEntryNote)pair.Value;
                        note.ContentControl.TextChanged += NoteControl_TextChanged;
                        //if (controlType == Constant.Control.Note)
                        //{
                        SetContextMenuCallbacks(note);
                        //}
                        break;
                    case Control.MultiLine:
                        DataEntryMultiLine multiLine = (DataEntryMultiLine)pair.Value;
                        multiLine.ContentControl.TextChanged += MultiLineControl_TextHasChanged;
                        SetContextMenuCallbacks(multiLine);
                        break;
                    case Control.AlphaNumeric:
                        DataEntryAlphaNumeric alphaNumeric = (DataEntryAlphaNumeric)pair.Value;
                        alphaNumeric.ContentControl.TextChanged += AlphaNumericControl_TextChanged;
                        SetContextMenuCallbacks(alphaNumeric);
                        break;
                    case DatabaseColumn.DateTime:
                        // Note. There are several issues with the XCEED WatermarkDateTimePicker. In particular, the date in the 
                        // text date area is not well coordinated with the date in the calendar, i.e., the two aren't necessarily in
                        // sync. As well, changing a date on the calendar doesnt' appear to trigger the DateTimeContro_ValueChanged event
                        // Various workarounds are implemented as commented below with SAULXXX WatermarkDateTimePicker Workaround.
                        // If the toolkit is updated to fix them, then those workarounds can be deleted (but test them first).
                        DataEntryDateTime dateTime = (DataEntryDateTime)pair.Value;
                        // SAULXXX WatermarkDateTimePicker Workaround. 
                        // We need to access the calendar part of the WatermarkDateTimePicker, but 
                        // we can't do that until the control is loaded.
                        dateTime.ContentControl.Loaded += WatermarkDateTimePicker_Loaded;
                        dateTime.ContentControl.ValueChanged += DateTimeControl_ValueChanged;
                        // We need the lines below as otherwise it will show the panel's context menu, which is confusing, instead of nothing
                        dateTime.ContentControl.ContextMenu = new()
                        {
                            Visibility = Visibility.Collapsed
                        };

                        break;
                    case DatabaseColumn.DeleteFlag:
                    case Control.Flag:
                        DataEntryFlag flag = (DataEntryFlag)pair.Value;
                        flag.ContentControl.Checked += FlagControl_CheckedChanged;
                        flag.ContentControl.Unchecked += FlagControl_CheckedChanged;
                        SetContextMenuCallbacks(flag);
                        break;
                    case Control.FixedChoice:
                        DataEntryChoice choice = (DataEntryChoice)pair.Value;
                        // Subscribe only to SelectionConfirmed - it now fires for all selection events:
                        // - Arrow key navigation (with or without dropdown open)
                        // - Clicking items in dropdown
                        // - Enter/Return/Tab key press
                        // - Re-selecting the same item
                        // SelectionChanged is no longer needed as SelectionConfirmed is now a superset
                        choice.ContentControl.SelectionConfirmed += ChoiceControl_SelectionConfirmed;
                        SetContextMenuCallbacks(choice);
                        break;
                    case Control.MultiChoice:
                        DataEntryMultiChoice multiChoice = (DataEntryMultiChoice)pair.Value;
                        multiChoice.ContentControl.ItemSelectionChanged += MultiChoiceControl_ItemSelectionChanged;
                        SetContextMenuCallbacks(multiChoice);
                        break;
                    case Control.Counter:
                        DataEntryCounter counter = (DataEntryCounter)pair.Value;
                        counter.ContentControl.ValueChanged += IntegerControl_ValueChanged;
                        counter.ContentControl.Spinned += IntegerContentControl_Spinned;
                        SetContextMenuCallbacks(counter);
                        break;
                    case Control.IntegerAny:
                        DataEntryIntegerAny integerAny = (DataEntryIntegerAny)pair.Value;
                        integerAny.ContentControl.ValueChanged += IntegerControl_ValueChanged;
                        integerAny.ContentControl.Spinned += IntegerContentControl_Spinned;
                        SetContextMenuCallbacks(integerAny);
                        break;
                    case Control.IntegerPositive:
                        DataEntryIntegerPositive integerPositive = (DataEntryIntegerPositive)pair.Value;
                        integerPositive.ContentControl.ValueChanged += IntegerControl_ValueChanged;
                        integerPositive.ContentControl.Spinned += IntegerContentControl_Spinned;
                        SetContextMenuCallbacks(integerPositive);
                        break;
                    case Control.DecimalAny:
                        DataEntryDecimalAny decimalAny = (DataEntryDecimalAny)pair.Value;
                        decimalAny.ContentControl.ValueChanged += DecimalControl_ValueChanged;
                        decimalAny.ContentControl.Spinned += DecimalAnyContentControl_Spinned;
                        SetContextMenuCallbacks(decimalAny);
                        break;
                    case Control.DecimalPositive:
                        DataEntryDecimalPositive decimalPositive = (DataEntryDecimalPositive)pair.Value;
                        decimalPositive.ContentControl.ValueChanged += DecimalControl_ValueChanged;
                        decimalPositive.ContentControl.Spinned += DecimalPositiveContentControl_Spinned;
                        SetContextMenuCallbacks(decimalPositive);
                        break;
                    case Control.DateTime_:
                        DataEntryDateTimeCustom dateTimeCustom = (DataEntryDateTimeCustom)pair.Value;
                        dateTimeCustom.ContentControl.ValueChanged += DateTimeCustomControl_ValueChanged;
                        // Add keyboard paste support for Ctl-V
                        dateTimeCustom.ContentControl.PreviewKeyDown += DateTimeCustomControl_PreviewKeyDown;
                        SetContextMenuCallbacks(dateTimeCustom);
                        break;
                    case Control.Date_:
                        DataEntryDate date = (DataEntryDate)pair.Value;
                        date.ContentControl.ValueChanged += DateControl_ValueChanged;
                        // Add keyboard paste support for Ctl-V
                        date.ContentControl.PreviewKeyDown += DateControl_PreviewKeyDown;
                        SetContextMenuCallbacks(date);
                        break;
                    case Control.Time_:
                        DataEntryTime time = (DataEntryTime)pair.Value;
                        time.ContentControl.ValueChanged += TimeControl_ValueChanged;
                        // Add keyboard paste support for Ctl-V
                        time.ContentControl.PreviewKeyDown += TimeControl_PreviewKeyDown;
                        SetContextMenuCallbacks(time);
                        break;

                }
            }
        }

        // Create the Context menu, including settings its callbakcs
        private void SetContextMenuCallbacks(DataEntryControl control)
        {
            if (GlobalReferences.TimelapseState.IsViewOnly)
            {
                // In view-only mode, we don't create these menus as they allow editing
                return;
            }

            MenuItem menuItemPropagateFromLastValue = new()
            {
                IsCheckable = false,
                Tag = control,
                Header = "Propagate from the last non-empty value to here"
            };

            if (control is DataEntryCounter)
            {
                menuItemPropagateFromLastValue.Header = "Propagate from the last non-zero value to here";
            }
            menuItemPropagateFromLastValue.Click += MenuItemPropagateFromLastValue_Click;

            MenuItem menuItemCopyForward = new()
            {
                IsCheckable = false,
                Header = "Copy forward to end",
                ToolTip = "The value of this field will be copied forward from this file to the last file in this set",
                Tag = control
            };
            menuItemCopyForward.Click += MenuItemPropagateForward_Click;
            MenuItem menuItemCopyCurrentValue = new()
            {
                IsCheckable = false,
                Header = "Copy to all",
                Tag = control
            };
            menuItemCopyCurrentValue.Click += MenuItemCopyCurrentValueToAll_Click;

            MenuItem menuItemCopy = new()
            {
                IsCheckable = false,
                Header = "Copy",
                ToolTip = "Copy will copy this field's entire content to the clipboard",
                Tag = control
            };
            menuItemCopy.Click += MenuItemCopyToClipboard_Click;

            MenuItem menuItemPaste = new()
            {
                IsCheckable = false,
                Header = "Paste",
                ToolTip = "Paste will replace this field's content with the clipboard's content",
                Tag = control
            };
            menuItemPaste.Click += MenuItemPasteFromClipboard_Click;

            // DataEntrHandler.PropagateFromLastValueIndex and CopyForwardIndex must be kept in sync with the add order here
            ContextMenu menu = new();
            menu.Items.Add(menuItemPropagateFromLastValue);
            menu.Items.Add(menuItemCopyForward);
            menu.Items.Add(menuItemCopyCurrentValue);
            Separator menuSeparator = new();
            menu.Items.Add(new Separator());
            menu.Items.Add(menuItemCopy);
            menu.Items.Add(menuItemPaste);

            control.Container.ContextMenu = menu;
            control.Container.PreviewMouseRightButtonDown += Container_PreviewMouseRightButtonDown;

            // For the File/RelativePath controls, all which are read only, hide the irrelevant menu items.
            // This could be made more efficient by simply not creating those items, but given the low case we just left it as is.
            if (control.DataLabel == DatabaseColumn.File || control.DataLabel == DatabaseColumn.RelativePath)
            {
                if (control is DataEntryNote note)
                {
                    note.ContentControl.ContextMenu = menu;
                    menuItemPropagateFromLastValue.Visibility = Visibility.Collapsed;
                    menuItemCopyForward.Visibility = Visibility.Collapsed;
                    menuItemCopyCurrentValue.Visibility = Visibility.Collapsed;
                    menuSeparator.Visibility = Visibility.Collapsed;
                    if (control.ContentReadOnly)
                    {
                        menuItemPaste.Visibility = Visibility.Collapsed;
                    }
                }
            }
            else if (control is DataEntryCounter counter)
            {
                counter.ContentControl.ContextMenu = menu;
            }
            else if (control is DataEntryIntegerPositive integerPositive)
            {
                integerPositive.ContentControl.ContextMenu = menu;
            }
            else if (control is DataEntryIntegerAny integerAny)
            {
                integerAny.ContentControl.ContextMenu = menu;
            }
            else if (control is DataEntryDecimalPositive decimalPositive)
            {
                decimalPositive.ContentControl.ContextMenu = menu;
            }
            else if (control is DataEntryDecimalAny decimalAny)
            {
                decimalAny.ContentControl.ContextMenu = menu;
            }
            else if (control is DataEntryAlphaNumeric alphaNumeric)
            {
                // We need this befor the DataEntryNote check, as otherwise 
                // the alphaNumeric control will match as its a derived class
                alphaNumeric.ContentControl.ContextMenu = menu;
            }
            else if (control is DataEntryNote note)
            {
                note.ContentControl.ContextMenu = menu;
            }
            else if (control is DataEntryMultiLine multiLine)
            {
                multiLine.ContentControl.ContextMenu = menu;
            }

            else if (control is DataEntryChoice choice)
            {
                choice.ContentControl.ContextMenu = menu;
            }

            else if (control is DataEntryMultiChoice multiChoice)
            {
                multiChoice.ContentControl.ContextMenu = menu;
            }

            else if (control is DataEntryFlag flag)
            {
                flag.ContentControl.ContextMenu = menu;
            }
            else if (control is DataEntryDateTimeCustom dateTime_)
            {
                menuItemPropagateFromLastValue.Visibility = Visibility.Collapsed;
                dateTime_.ContentControl.ContextMenu = menu;
            }
            else if (control is DataEntryDate date)
            {
                menuItemPropagateFromLastValue.Visibility = Visibility.Collapsed;
                date.ContentControl.ContextMenu = menu;
            }
            else if (control is DataEntryTime time)
            {
                menuItemPropagateFromLastValue.Visibility = Visibility.Collapsed;
                time.ContentControl.ContextMenu = menu;
            }
            else
            {
                throw new NotSupportedException($"Unhandled control type {control.GetType().Name}.");
            }
        }
        #endregion

        #region DateTime_ / Date_  / Time callbacks to work around its limitations
        // SAULXXX WatermarkDateTimePicker Workaround. 
        // Access the calendar part of the datetimepicker, and
        // add an event to it that is triggered whenever the user changes the calendar.
        // For convenience, we use the calendar's tag to store the WatermarkDateTimePicker control so we can retrieve it from the event.
        private void WatermarkDateTimePicker_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not WatermarkDateTimePicker dateTimePicker) return;
            if (dateTimePicker.Template.FindName("PART_Calendar", dateTimePicker) is System.Windows.Controls.Calendar calendar)
            {
                calendar.Tag = dateTimePicker;
                calendar.IsTodayHighlighted = false; // Don't highlight today's date, as it could be confusing given what this control is used for.
            }
        }

        // DateTime_: Handle Ctl-V paste operations
        private void DateTimeCustomControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                WatermarkDateTimePicker dateTimePicker = (WatermarkDateTimePicker)sender;
                DataEntryControl control = (DataEntryControl)dateTimePicker.Tag;

                try
                {
                    string clipboardText = Clipboard.GetText().Trim();
                    if (DateTimeHandler.TryParseDateTimeDatabaseAndDisplayFormats(clipboardText, out DateTime dateTime))
                    {
                        string databaseValue = DateTimeHandler.ToStringDatabaseDateTime(dateTime);
                        control.SetContentAndTooltip(databaseValue);
                        UpdateRowsDependingOnThumbnailGridState(control.DataLabel, databaseValue);
                        e.Handled = true; // This prevents the system paste
                    }
                }
                catch
                {
                    // Handle clipboard access errors gracefully, by just ignoring the paste operation.
                }
            }
        }

        private void DateControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                WatermarkDateTimePicker dateTimePicker = (WatermarkDateTimePicker)sender;
                DataEntryControl control = (DataEntryControl)dateTimePicker.Tag;

                try
                {
                    string clipboardText = Clipboard.GetText().Trim();
                    if (DateTimeHandler.TryParseDateDatabaseAndDisplayFormats(clipboardText, out DateTime dateTime))
                    {
                        string databaseValue = DateTimeHandler.ToStringDatabaseDate(dateTime);
                        control.SetContentAndTooltip(databaseValue);
                        UpdateRowsDependingOnThumbnailGridState(control.DataLabel, databaseValue);
                        e.Handled = true; // This prevents the system paste
                    }
                }
                catch
                {
                    // Handle clipboard access errors gracefully
                }
            }
        }

        private void TimeControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                WatermarkTimePicker timePicker = (WatermarkTimePicker)sender;  // Note: Time uses WatermarkTimePicker, not WatermarkDateTimePicker
                DataEntryControl control = (DataEntryControl)timePicker.Tag;

                try
                {
                    string clipboardText = Clipboard.GetText().Trim();
                    if (DateTimeHandler.TryParseDatabaseTime(clipboardText, out DateTime dateTime))
                    {
                        string databaseValue = DateTimeHandler.ToStringTime(dateTime);
                        control.SetContentAndTooltip(databaseValue);
                        UpdateRowsDependingOnThumbnailGridState(control.DataLabel, databaseValue);
                        e.Handled = true; // This prevents the system paste
                    }
                }
                catch
                {
                    // Handle clipboard access errors gracefully
                }
            }
        }

        #endregion 

        #region Context menu event handlers
        // Menu selections for propagating or copying the current value of this control to all images

        // Copy the last non-empty value in this control preceding this file up to the current image
        protected virtual void MenuItemPropagateFromLastValue_Click(object sender, RoutedEventArgs e)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(sender, nameof(sender));

            // Get the chosen data entry control
            DataEntryControl control = (DataEntryControl)((MenuItem)sender).Tag;
            if (control == null)
            {
                return;
            }

            bool checkForZero = control is DataEntryCounter;
            bool isFlag = control is DataEntryFlag;
            int indexToCopyFrom = -1;
            ImageRow valueSource = null;
            string valueToCopy = checkForZero ? "0" : string.Empty;

            // Search for the row with some value in it, starting from the previous row
            int currentRowIndex = (ThumbnailGrid.IsVisible == false) ? ImageCache.CurrentRow : ThumbnailGrid.GetSelected()[0];
            for (int previousIndex = currentRowIndex - 1; previousIndex >= 0; previousIndex--)
            {
                ImageRow file = FileDatabase.FileTable[previousIndex];
                if (file == null)
                {
                    continue;
                }
                valueToCopy = file.GetValueDatabaseString(control.DataLabel);
                if (valueToCopy == null)
                {
                    continue;
                }
                valueToCopy = valueToCopy.Trim();
                if (valueToCopy.Length > 0)
                {
                    if ((checkForZero && !valueToCopy.Equals("0")) ||             // Skip over non-zero values for counters
                        (isFlag && !valueToCopy.Equals(BooleanValue.False, StringComparison.OrdinalIgnoreCase)) || // Skip over false values for flags
                        (!checkForZero && !isFlag))
                    {
                        indexToCopyFrom = previousIndex;    // We found a non-empty value
                        valueSource = file;
                        break;
                    }
                }
            }

            string newContent = valueToCopy;
            if (indexToCopyFrom < 0)
            {
                // Display a dialog box saying there is nothing to propagate. 
                // Note that this should never be displayed, as the menu shouldn't be highlit if there is nothing to propagate
                // But just in case...
                Dialogs.DataEntryNothingToPropagateDialog(Application.Current.MainWindow);
                return;
            }

            // Display the appropriate dialog box that explains what will happen. Arguments indicate what is to be propagated and how many files will be affected
            int filesAffected = currentRowIndex - indexToCopyFrom;

            // Show the warning (if not supressed)
            if (GlobalReferences.TimelapseState.SuppressPropagateFromLastNonEmptyValuePrompt == false)
            {
                if (Dialogs.DataEntryConfirmPropagateFromLastValueDialog(Application.Current.MainWindow, valueToCopy, filesAffected) != true)
                {
                    return; // operation cancelled
                }
            }
            // Update the affected files. Note that we start on the row after the one with a value in it to the current row.
            Mouse.OverrideCursor = Cursors.Wait;
            //this.FileDatabase.UpdateFiles(valueSource, control.DataLabel, indexToCopyFrom + 1, currentRowIndex);
            FileDatabase.UpdateFiles(valueSource, control, indexToCopyFrom + 1, currentRowIndex);
            control.SetContentAndTooltip(newContent);
            Mouse.OverrideCursor = null;
        }

        // Copy the current value of this control to all images
        protected virtual void MenuItemCopyCurrentValueToAll_Click(object sender, RoutedEventArgs e)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(sender, nameof(sender));

            // Get the chosen data entry control
            DataEntryControl control = (DataEntryControl)((MenuItem)sender).Tag;
            if (control == null)
            {
                return;
            }

            if (control.Content == null)
            {
                // This shouldn't happen, but I did receive an error report ...
                Dialogs.DataEntryCantCopyAsNullDialog(Application.Current.MainWindow);
                return;
            }

            // Display a dialog box that explains what will happen. Arguments indicate how many files will be affected, and is tuned to the type of control 
            bool checkForZero = control is DataEntryCounter;
            int filesAffected = FileDatabase.CountAllCurrentlySelectedFiles;
            if (Dialogs.DataEntryConfirmCopyCurrentValueToAllDialog(Application.Current.MainWindow, control.Content, filesAffected, checkForZero) != true)
            {
                return;
            }

            // Update all files to match the value of the control (identified by the data label) in the currently selected image row.
            Mouse.OverrideCursor = Cursors.Wait;
            ImageRow imageRow = (ThumbnailGrid.IsVisible == false) ? ImageCache.Current : FileDatabase.FileTable[ThumbnailGrid.GetSelected()[0]];
            if (imageRow == null)
            {
                // This shouldn't happen, but I did receive an error report ...
                Dialogs.DataEntryCantCopyAsNullDialog(Application.Current.MainWindow);
                return;
            }

            FileDatabase.UpdateFiles(imageRow, control);
            Mouse.OverrideCursor = null;
        }

        // Propagate the current value of this control forward from this point across the current set of selected images
        protected virtual void MenuItemPropagateForward_Click(object sender, RoutedEventArgs e)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(sender, nameof(sender));

            // Get the chosen data entry control
            DataEntryControl control = (DataEntryControl)((MenuItem)sender).Tag;
            if (control == null)
            {
                return;
            }

            int currentRowIndex = (ThumbnailGrid.IsVisible == false) ? ImageCache.CurrentRow : ThumbnailGrid.GetSelected()[0];
            int imagesAffected = FileDatabase.CountAllCurrentlySelectedFiles - currentRowIndex - 1;
            if (imagesAffected == 0)
            {
                // Display a dialog box saying there is nothing to copy forward. 
                // Note that this should never be displayed, as the menu shouldn't be highlit if we are on the last image
                // But just in case...
                Dialogs.DataEntryNothingToCopyForwardDialog(Application.Current.MainWindow);
                return;
            }

            // Display the appropriate dialog box that explains what will happen. Arguments indicate how many files will be affected, and is tuned to the type of control 
            ImageRow imageRow = (ThumbnailGrid.IsVisible == false) ? ImageCache.Current : FileDatabase.FileTable[ThumbnailGrid.GetSelected()[0]];
            if (imageRow == null)
            {
                TracePrint.NullException(nameof(imageRow));
                return;
            }
            string valueToCopy = imageRow.GetValueDisplayString(control.DataLabel);
            bool checkForZero = control is DataEntryCounter;
            if (Dialogs.DataEntryConfirmCopyForwardDialog(Application.Current.MainWindow, valueToCopy, imagesAffected, checkForZero) != true)
            {
                return;
            }

            // Update the files from the next row (as we are copying from the current row) to the end.
            Mouse.OverrideCursor = Cursors.Wait;
            int nextRowIndex = (ThumbnailGrid.IsVisible == false) ? ImageCache.CurrentRow + 1 : ThumbnailGrid.GetSelected()[0] + 1;
            FileDatabase.UpdateFiles(imageRow, control, nextRowIndex, FileDatabase.CountAllCurrentlySelectedFiles - 1);
            Mouse.OverrideCursor = null;
        }

        // Copy the  value of the current control to the clipboard
        protected virtual void MenuItemCopyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(sender, nameof(sender));

            // Get the chosen data entry control
            DataEntryControl control = (DataEntryControl)((MenuItem)sender).Tag;
            if (control == null)
            {
                return;
            }

            // Its in a try / catch as one user reported an unusual error: OpenClipboardFailed
            try
            {
                Clipboard.SetText(control.Content);
            }
            catch
            {
                Debug.Print("Error in setting text in clipboard (see MenuItemCopyToClipboard_Click in DataEntryHandler");
            }
        }

        // Paste the contents of the clipboard into the current or selected controls
        // Note that we don't do any checks against the control's type, as that would be handled by the menu enablement
        protected virtual void MenuItemPasteFromClipboard_Click(object sender, RoutedEventArgs e)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(sender, nameof(sender));

            // Get the chosen data entry control
            DataEntryControl control = (DataEntryControl)((MenuItem)sender).Tag;
            if (control == null)
            {
                return;
            }
            string newContent = Clipboard.GetText().Trim();
            if (control is DataEntryCounter)
            {
                // For counters, removing any leading 0's, but if this ends up with an empty string, then revert to 0
                newContent = newContent.TrimStart('0');
                if (string.IsNullOrEmpty(newContent))
                {
                    newContent = "0";
                }
            }

            control.SetContentAndTooltip(newContent);
            UpdateRowsDependingOnThumbnailGridState(control.DataLabel, newContent);
        }

        // Enable or disable particular context menu items
        protected virtual void Container_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(sender, nameof(sender));

            StackPanel stackPanel = (StackPanel)sender;
            DataEntryControl control = (DataEntryControl)stackPanel.Tag;
            string dateTimeCustomPasteHeader = string.Empty;
            if (stackPanel.ContextMenu == null)
            {
                TracePrint.NullException(nameof(stackPanel));
                return;
            }
            MenuItem menuItemCopyToAll = (MenuItem)stackPanel.ContextMenu.Items[CopyToAllIndex];
            MenuItem menuItemCopyForward = (MenuItem)stackPanel.ContextMenu.Items[CopyForwardIndex];
            MenuItem menuItemPropagateFromLastValue = (MenuItem)stackPanel.ContextMenu.Items[PropagateFromLastValueIndex];
            MenuItem menuItemCopyToClipboard = (MenuItem)stackPanel.ContextMenu.Items[CopyToClipboardIndex];
            MenuItem menuItemPasteFromClipboard = (MenuItem)stackPanel.ContextMenu.Items[PasteFromClipboardIndex];

            // Behaviour: 
            // - if the ThumbnailInCell is visible, disable Copy to all / Copy forward / Propagate if a single item isn't selected
            // - otherwise enable the menu item only if the resulting action is coherent
            bool enabledIsPossible = ThumbnailGrid.IsVisible == false || ThumbnailGrid.SelectedCount() == 1;
            menuItemCopyToAll!.IsEnabled = enabledIsPossible;
            menuItemCopyForward!.IsEnabled = enabledIsPossible && (menuItemCopyForward.IsEnabled = IsCopyForwardPossible());
            menuItemPropagateFromLastValue!.IsEnabled = enabledIsPossible && IsCopyFromLastNonEmptyValuePossible(control);

            // Enable Copy menu if
            // - its not empty / white space and not in the overview with different contents (i.e., ellipsis is showing)
            menuItemCopyToClipboard!.IsEnabled = !(string.IsNullOrWhiteSpace(control.Content) || control.Content == Unicode.Ellipsis);

            // Enable Paste menu only if
            // - the clipboard is not empty or white space, 
            // - the string matches the contents expected by the control's type
            // - we are not in the overview with different contents selected (i.e., ellipsis is showing)
            // Its in a try / catch as one user reported an unusual error: OpenClipboardFailed
            string clipboardText;
            try
            {
                clipboardText = Clipboard.GetText().Trim();
            }
            catch
            {
                clipboardText = string.Empty;
                Debug.Print("Error in setting text in clipboard (see Container_PreviewMouseRightButtonDown in DataEntryHandler");
            }
            if (string.IsNullOrEmpty(clipboardText))
            {
                menuItemPasteFromClipboard!.IsEnabled = false;
            }
            else
            {
                if (control is DataEntryAlphaNumeric)
                {
                    // Any string is valid
                    menuItemPasteFromClipboard!.IsEnabled = IsCondition.IsAlphaNumeric(clipboardText);
                }
                else if (control is DataEntryNote || control is DataEntryMultiLine)
                {
                    // Any string is valid
                    menuItemPasteFromClipboard!.IsEnabled = true;
                }
                else if (control is DataEntryFlag)
                {
                    // Only true / false is valid
                    menuItemPasteFromClipboard!.IsEnabled = (clipboardText == "true" || clipboardText == "false");
                }
                else if (control is DataEntryCounter || control is DataEntryIntegerPositive)
                {
                    // Only a positive integer is valid
                    menuItemPasteFromClipboard!.IsEnabled = Int32.TryParse(clipboardText, out int x) && x >= 0;
                }
                else if (control is DataEntryIntegerAny)
                {
                    // Only an integer is valid
                    menuItemPasteFromClipboard!.IsEnabled = Int32.TryParse(clipboardText, out int _);
                }
                else if (control is DataEntryDecimalPositive)
                {
                    // Only a positive decimal is valid
                    menuItemPasteFromClipboard!.IsEnabled = Double.TryParse(clipboardText, NumberStyles.Float, CultureInfo.InvariantCulture, out double x) && x >= 0;
                    clipboardText = x.ToString(CultureInfo.InvariantCulture);
                }
                else if (control is DataEntryDecimalAny)
                {
                    // Only a double is valid
                    menuItemPasteFromClipboard!.IsEnabled = Double.TryParse(clipboardText, NumberStyles.Float, CultureInfo.InvariantCulture, out double y);
                    clipboardText = y.ToString(CultureInfo.InvariantCulture);
                }
                else if (control is DataEntryChoice choiceControl)
                {
                    // Only a value present as a menu choice is valid 
                    menuItemPasteFromClipboard!.IsEnabled = false;
                    ComboBox comboBox = choiceControl.ContentControl;
                    foreach (Object t in comboBox.Items)
                    {
                        // This check skips over the Separator
                        if (t is ComboBoxItem cbi)
                        {
                            if (clipboardText == ((string)cbi.Content).Trim())
                            {
                                // We found a matching value, so pasting is possible
                                menuItemPasteFromClipboard.IsEnabled = true;
                                break;
                            }
                        }
                    }
                }
                else if (control is DataEntryMultiChoice multiChoice)
                {
                    // Only a value present as a menu choice is valid 
                    menuItemPasteFromClipboard!.IsEnabled = false;
                    WatermarkCheckComboBox checkComboBox = multiChoice.ContentControl;

                    // Parse the current checkComboBox items a text string to update the checkComboBox text as needed
                    List<string> list = [];
                    foreach (string item in checkComboBox.Items)
                    {
                        list.Add(item);
                    }
                    // Assume a comma-separated list. Get each element and check to see if its in the list
                    string[] newText = clipboardText.Split(',');
                    foreach (string str in newText)
                    {
                        if (list.Contains(str) == false)
                        {
                            menuItemPasteFromClipboard.IsEnabled = false;
                            break;
                        }
                        menuItemPasteFromClipboard.IsEnabled = true;
                    }
                }
                else if (control is DataEntryDateTimeCustom)
                {
                    // Only a display or database datetime is valid for copying. However, we have to convert it to a database time to make it work properly.
                    bool parsable = DateTimeHandler.TryParseDateTimeDatabaseAndDisplayFormats(clipboardText, out DateTime dateTime);
                    menuItemPasteFromClipboard!.IsEnabled = parsable;
                    if (parsable)
                    {
                        // We save the displayable text so we can put it in the paste menu header later
                        dateTimeCustomPasteHeader = DateTimeHandler.ToStringDisplayDateTime(dateTime);
                        Clipboard.SetText(DateTimeHandler.ToStringDatabaseDateTime(dateTime));
                        clipboardText = Clipboard.GetText();
                    }
                }
                else if (control is DataEntryDate)
                {
                    // Only a display or database date is valid for copying. However, we have to convert it to a database date to make it work properly.
                    bool parsable = DateTimeHandler.TryParseDateDatabaseAndDisplayFormats(clipboardText, out DateTime dateTime);
                    menuItemPasteFromClipboard!.IsEnabled = parsable;
                    if (parsable)
                    {
                        // We save the displayable text so we can put it in the paste menu header later
                        dateTimeCustomPasteHeader = DateTimeHandler.ToStringDisplayDatePortion(dateTime);
                        Clipboard.SetText(DateTimeHandler.ToStringDatabaseDate(dateTime));
                        clipboardText = Clipboard.GetText();
                    }
                }
                else if (control is DataEntryTime)
                {
                    // Only a display or database date is valid for copying. However, we have to convert it to a database date to make it work properly.
                    bool parsable = DateTimeHandler.TryParseDatabaseTime(clipboardText, out DateTime dateTime);
                    menuItemPasteFromClipboard!.IsEnabled = parsable;
                    if (parsable)
                    {
                        // We save the displayable text so we can put it in the paste menu header later
                        dateTimeCustomPasteHeader = DateTimeHandler.ToStringTime(dateTime);
                        Clipboard.SetText(DateTimeHandler.ToStringTime(dateTime));
                        clipboardText = Clipboard.GetText();
                    }
                }

                // Alter the paste header to show the text that will be pasted e.g Paste 'Lion'
                if (menuItemPasteFromClipboard!.IsEnabled)
                {
                    if (control is DataEntryCounter)
                    {
                        // removing any leading 0's, but if its empty make it a 0
                        clipboardText = clipboardText.TrimStart('0');
                        if (string.IsNullOrEmpty(clipboardText))
                        {
                            clipboardText = "0";
                        }
                    }

                    if (control is DataEntryDateTimeCustom || control is DataEntryDate || control is DataEntryTime)
                    {
                        menuItemPasteFromClipboard.Header = "Paste '" + (clipboardText.Length > 20 ? clipboardText[..20] + Unicode.Ellipsis : dateTimeCustomPasteHeader) + "'";
                    }
                    else
                    {
                        menuItemPasteFromClipboard.Header = "Paste '" + (clipboardText.Length > 20 ? clipboardText[..20] + Unicode.Ellipsis : clipboardText) + "'";
                    }
                }
                else
                {
                    // Since there is nothing in the clipboard, just show 'Paste'
                    menuItemPasteFromClipboard.Header = "Paste";
                }

                // Alter the copy header to show the text that will be copied, i.e. Copy 'Lion'
                if (menuItemCopyToClipboard.IsEnabled)
                {
                    string content = control.Content!.Trim();
                    menuItemCopyToClipboard.Header = "Copy '" + (content.Length > 20 ? content[..20] + Unicode.Ellipsis : content) + "'";
                }
                else
                {
                    // Since there an empty string to Copy, just show 'Copy'
                    menuItemCopyToClipboard.Header = "Copy";
                }
            }
        }
        #endregion

        #region Helpers for Copy Forward/Backwards etc.
        public bool IsCopyForwardPossible()
        {
            if (ImageCache.Current == null)
            {
                return false;
            }

            // The current row depends on wheter we are in the thumbnail grid or the normal view
            int currentRow = (ThumbnailGrid.IsVisible == false) ? ImageCache.CurrentRow : ThumbnailGrid.GetSelected()[0];
            int filesAffected = FileDatabase.CountAllCurrentlySelectedFiles - currentRow - 1;
            return (filesAffected > 0);
        }

        // Return true if there is a non-empty value available
        public bool IsCopyFromLastNonEmptyValuePossible(DataEntryControl control)
        {
            // Check the arguments for null 
            if (null == control)
            {
                // Since we don't have a valid control, copying isn't possible
                return false;
            }

            // ReSharper disable once RedundantAssignment
            int currentIndex = 0; // So we can print the value in the catch
            bool checkCounter = control is DataEntryCounter;
            bool checkFlag = control is DataEntryFlag;
            int nearestRowWithCopyableValue = -1;
            // Its in a try/catch as very very occassionally we get a 'system.indexoutofrangeexception'
            try
            {
                // The current row depends on wheter we are in the thumbnail grid or the normal view
                int currentRow = (ThumbnailGrid.IsVisible == false) ? ImageCache.CurrentRow : ThumbnailGrid.GetSelected()[0];
                for (int fileIndex = currentRow - 1; fileIndex >= 0; fileIndex--)
                {
                    // ReSharper disable once RedundantAssignment
                    currentIndex = fileIndex;
                    // Search for the row with some value in it, starting from the previous row
                    string valueToCopy = FileDatabase.FileTable[fileIndex].GetValueDatabaseString(control.DataLabel);
                    if (string.IsNullOrWhiteSpace(valueToCopy) == false)
                    {
                        // for flags, we skip over falses
                        // for counters, we skip over 0
                        // for all others, any value will work as long as its not null or white space
                        if ((checkFlag && !valueToCopy.Equals("false")) ||
                            (checkCounter && !valueToCopy.Equals("0")) ||
                            (!checkCounter && !checkFlag))
                        {
                            nearestRowWithCopyableValue = fileIndex; // We found a non-empty value
                            break;
                        }
                    }
                }
            }
            catch (IndexOutOfRangeException e)
            {
                // I don't know why we get this occassional error, so this is an attempt to print out the result so we can debug it
                TracePrint.PrintMessage(
                    $"IsCopyFromLastNonEmptyValuePossible: IndexOutOfRange Exception, where index is: {currentIndex}{Environment.NewLine}{e.Message}");
                return (nearestRowWithCopyableValue >= 0);
            }
            catch (Exception exception)
            {
                // A user reported a system.NullReference exception, although I have no idea where that would occur
                // so we put in a general catch here. I have tried to generate either of these catches, with no luck
                // but I suspect its some odd combination of trying to get the FileTable[fileIndex] above.
                TracePrint.PrintMessage(
                    $"IsCopyFromLastNonEmptyValuePossible: {exception.Message}");
                return false;
            }
            return (nearestRowWithCopyableValue >= 0);
        }
        #endregion

        #region Event handlers - Content Selections and Changes
        private void DateTimeControl_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Debug.Print("DateTimeControl_ValueChanged triggered");
            if (IsProgrammaticControlUpdate)
            {
                return;
            }

            WatermarkDateTimePicker dateTimePicker = (WatermarkDateTimePicker)sender;
            if (dateTimePicker.Value.HasValue == false)
            {
                return;
            }
            // Update file data table and write the new DateTime, Date, and Time to the database
            DateTimeUpdate(dateTimePicker, dateTimePicker.Value.Value);

            // SAULXXX WatermarkDateTimePicker Workaround. 
            // There is a bug (?) in the dateTimePicker where it doesn't update the calendar to the
            // changed date. This means that if you open the calendar it shows the
            // original date, and when you close it (even without selecting a date) it reverts to the old date.
            // The fix below updates the calendar to the current date.
            if (dateTimePicker.Template.FindName("PART_Calendar", dateTimePicker) is System.Windows.Controls.Calendar calendar)
            {
                IsProgrammaticControlUpdate = true;
                calendar.DisplayDate = dateTimePicker.Value.Value;
                calendar.SelectedDate = dateTimePicker.Value.Value;
                if (calendar.Template.FindName("PART_TimePicker", calendar) is TimePicker timepicker)
                {
                    timepicker.Value = dateTimePicker.Value.Value;
                }
                IsProgrammaticControlUpdate = false;
            }
        }

        // Helper method for above DateTime changes.
        private void DateTimeUpdate(WatermarkDateTimePicker dateTimePicker, DateTime dateTime)
        {
            // update file data table and write the new DateTime, Date, and Time to the database
            if (ImageCache?.Current == null)
            {
                return;
            }
            ImageCache.Current.SetDateTime(dateTime);
            dateTimePicker.ToolTip = DateTimeHandler.ToStringDisplayDateTime(dateTime);

            List<ColumnTuplesWithWhere> imageToUpdate = [ImageCache.Current.GetDateTimeColumnTuples()];
            FileDatabase.UpdateFiles(imageToUpdate);
        }

        // When the text in a particular note box changes, update the particular note field(s) in the database
        private void NoteControl_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsProgrammaticControlUpdate)
            {
                return;
            }
            DataEntryNote control = (DataEntryNote)((TextBox)sender).Tag;
            control.ContentChanged = true;

            // Note that  trailing whitespace is removed only from the database as further edits may use it.
            UpdateRowsDependingOnThumbnailGridState(control.DataLabel, control.Content.Trim());
        }

        // When the text in a particular note box changes, update the particular note field(s) in the database 
        private void AlphaNumericControl_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsProgrammaticControlUpdate)
            {
                return;
            }

            DataEntryAlphaNumeric control = (DataEntryAlphaNumeric)((TextBox)sender).Tag;
            control.ContentChanged = true;

            // Note that  trailing whitespace is removed only from the database as further edits may use it.
            UpdateRowsDependingOnThumbnailGridState(control.DataLabel, control.Content.Trim());
        }

        // When the text in a multiLine control changes, update the particular field(s) in the database
        private void MultiLineControl_TextHasChanged(object sender, EventArgs e)
        {
            if (IsProgrammaticControlUpdate)
            {
                return;
            }
            DataEntryMultiLine control = (DataEntryMultiLine)((MultiLineText)sender).Tag;
            {
                control.ContentChanged = true;
                control.SetContentAndTooltip(control.Content.Trim());
                // Note that  trailing whitespace is removed only from the database as further edits may use it.
                UpdateRowsDependingOnThumbnailGridState(control.DataLabel, control.Content.Trim());
            }
        }

        // When the number in a particular counter or integer box changes, update the particular field(s) in the database
        private void IntegerControl_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (IsProgrammaticControlUpdate)
            {
                return;
            }
            IntegerUpDown integerUpDown = (IntegerUpDown)sender;
            // Get the key identifying the control, and then add its value to the database
            DataEntryControl control = (DataEntryControl)integerUpDown.Tag;
            control.SetContentAndTooltip(integerUpDown.Value.ToString());
            UpdateRowsDependingOnThumbnailGridState(control.DataLabel, control.Content);
        }

        // For some reason, the spinner wasn't triggering IntegerControl_ValueChanged.
        // By setting the value here, it does the right thing 
        private void IntegerContentControl_Spinned(object sender, SpinEventArgs e)
        {
            if (IsProgrammaticControlUpdate)
            {
                return;
            }
            IntegerUpDown integerUpDown = (IntegerUpDown)sender;
            DataEntryControl control = (DataEntryControl)integerUpDown.Tag;
            if (Int32.TryParse(control.Content, out int newValue))
            {
                integerUpDown.Value = newValue;
                return;
            }
            // I think the above will always succeed, but I will leave this here just in case
            if (null == integerUpDown.Value)
            {
                // If the value is blank, the 1st spinner operation goes to 0
                // regardless of the spin direction
                integerUpDown.Value = 0;
            }
            else if (e.Direction == SpinDirection.Increase)
            {
                integerUpDown.Value++;
            }
            else
            {
                integerUpDown.Value--;
            }
        }

        // When the number in a particular decimal box changes, update the particular decimal field(s) in the database
        private void DecimalControl_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (IsProgrammaticControlUpdate)
            {
                return;
            }
            DoubleUpDown doubleUpDown = (DoubleUpDown)sender;
            // Get the key identifying the control, and then add its value to the database
            DataEntryControl control = (DataEntryControl)doubleUpDown.Tag;

            if (doubleUpDown.Value == null)
            {
                // Value is null - field is empty or being cleared
                control.SetContentAndTooltip(doubleUpDown.Text ?? string.Empty);
            }
            else
            {
                // Value has a valid double - use it
                control.SetContentAndTooltip(((double)doubleUpDown.Value).ToString(CultureInfo.InvariantCulture));
            }

            UpdateRowsDependingOnThumbnailGridState(control.DataLabel, control.Content);
        }

        // For some reason, the spinner wasn't triggering IntegerControl_ValueChanged.
        // By setting the value here, it does the right thing 
        private void DecimalAnyContentControl_Spinned(object sender, SpinEventArgs e)
        {
            DecimalContentControl_Spinned(sender, e, false);
        }
        private void DecimalPositiveContentControl_Spinned(object sender, SpinEventArgs e)
        {
            DecimalContentControl_Spinned(sender, e, true);
        }
        private void DecimalContentControl_Spinned(object sender, SpinEventArgs e, bool positiveOnly)
        {
            if (IsProgrammaticControlUpdate)
            {
                return;
            }
            DoubleUpDown doubleUpDown = (DoubleUpDown)sender;
            DataEntryControl control = (DataEntryControl)doubleUpDown.Tag;
            if (Double.TryParse(control.Content, out double newValue))
            {
                doubleUpDown.Value = newValue;
                return;
            }

            // I think the above will always succeed, but I will leave this here just in case
            if (null == doubleUpDown.Value)
            {
                // If the value is blank, the 1st spinner operation goes to 0
                // regardless of the spin direction
                doubleUpDown.Value = 0;
            }
            else if (e.Direction == SpinDirection.Increase)
            {
                doubleUpDown.Value++;
            }
            else
            {
                doubleUpDown.Value--;
            }
            if (positiveOnly && doubleUpDown.Value < 0)
            {
                doubleUpDown.Value = 0;
            }
        }

        /// <summary>
        /// Handles all choice selection events and updates the database.
        /// SelectionConfirmed now fires for ALL user selection actions:
        /// - Arrow key navigation (up/down arrows change selection immediately)
        /// - Clicking an item in dropdown
        /// - Enter/Return/Tab key press
        /// - Re-selecting the same item (doesn't change value but confirms it)
        ///
        /// This is the single event handler needed for choice controls.
        /// SelectionChanged is no longer used as SelectionConfirmed is now a superset.
        /// </summary>
        private void ChoiceControl_SelectionConfirmed(object sender, TimelapseWpf.Toolkit.SelectionConfirmedEventArgs e)
        {
            // Respect the programmatic update flag (suppression is also set on control itself)
            if (IsProgrammaticControlUpdate)
            {
                return;
            }

            WatermarkComboBox comboBox = (WatermarkComboBox)sender;

            if (comboBox.SelectedItem == null)
            {
                // no item selected
                return;
            }

            // Get the key identifying the control, and then add its value to the database
            DataEntryControl control = (DataEntryControl)comboBox.Tag;
            // This guards against selecting the Separator
            if (comboBox.SelectedItem is ComboBoxItem cbi)
            {
                control.SetContentAndTooltip(cbi.Content.ToString());
            }
            UpdateRowsDependingOnThumbnailGridState(control.DataLabel, control.Content);
        }

        private void MultiChoiceControl_ItemSelectionChanged(object sender, ItemSelectionChangedEventArgs e)
        {
            if (IsProgrammaticControlUpdate)
            {
                return;
            }
            WatermarkCheckComboBox checkComboBox = (WatermarkCheckComboBox)sender;

            // Get the key identifying the control, and then add its value to the database
            DataEntryMultiChoice control = (DataEntryMultiChoice)checkComboBox.Tag;
            if (control.IgnoreSelectionChanged)
            {
                return;
            }

            // Build text from current selection in dropdown order
            // Note: We can't use checkComboBox.Text here because ItemSelectionChanged fires
            // BEFORE UpdateText() updates the Text property, so Text is always one step behind
            List<string> orderedList = [];
            foreach (object item in checkComboBox.Items)
            {
                string itemString = item?.ToString();
                if (itemString != null && checkComboBox.SelectedItemsOverride?.Contains(itemString) == true)
                {
                    orderedList.Add(itemString.Trim());
                }
            }
            string orderedText = string.Join(",", orderedList);
            control.SetContentAndTooltip(orderedText);
            UpdateRowsDependingOnThumbnailGridState(control.DataLabel, orderedText);
        }

        // When a flag changes, update the particular flag field(s) in the database
        private void FlagControl_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (IsProgrammaticControlUpdate)
            {
                return;
            }
            CheckBox checkBox = (CheckBox)sender;
            if (checkBox == null)
            {
                TracePrint.NullException(nameof(checkBox));
                return;
            }
            DataEntryControl control = (DataEntryControl)checkBox.Tag;
            string value = checkBox.IsChecked == true ? BooleanValue.True : BooleanValue.False;
            control.SetContentAndTooltip(value);
            UpdateRowsDependingOnThumbnailGridState(control.DataLabel, control.Content);
        }
        private void DateTimeCustomControl_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (IsProgrammaticControlUpdate)
            {
                return;
            }
            WatermarkDateTimePicker dateTimePicker = (WatermarkDateTimePicker)sender;
            if (dateTimePicker.Value.HasValue == false)
            {
                return;
            }
            DataEntryControl control = (DataEntryControl)dateTimePicker.Tag;

            string value = DateTimeHandler.ToStringDatabaseDateTime((DateTime)dateTimePicker.Value);
            control.SetContentAndTooltip(value);
            //this.UpdateRowsDependingOnThumbnailGridState(control.DataLabel, control.Content);
            UpdateRowsDependingOnThumbnailGridState(control.DataLabel, value);
        }

        private void DateControl_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (IsProgrammaticControlUpdate)
            {
                return;
            }
            WatermarkDateTimePicker dateTimePicker = (WatermarkDateTimePicker)sender;
            if (dateTimePicker.Value.HasValue == false)
            {
                return;
            }
            DataEntryControl control = (DataEntryControl)dateTimePicker.Tag;

            string value = DateTimeHandler.ToStringDatabaseDate((DateTime)dateTimePicker.Value);
            control.SetContentAndTooltip(value);
            UpdateRowsDependingOnThumbnailGridState(control.DataLabel, value);
        }

        private void TimeControl_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (IsProgrammaticControlUpdate)
            {
                return;
            }
            WatermarkTimePicker timePicker = (WatermarkTimePicker)sender;
            if (timePicker.Value.HasValue == false)
            {
                return;
            }
            DataEntryControl control = (DataEntryControl)timePicker.Tag;

            string value = DateTimeHandler.ToStringTime((DateTime)timePicker.Value);
            control.SetContentAndTooltip(value);
            UpdateRowsDependingOnThumbnailGridState(control.DataLabel, value);
        }
        #endregion

        #region Update Rows
        // Update either the current row or the selected rows in the database, 
        // depending upon whether we are in the single image or  theThumbnailGrid view respectively.
        public void UpdateRowsDependingOnThumbnailGridState(string datalabel, string content)
        {
            if (ThumbnailGrid == null) return;
            if (ThumbnailGrid.IsVisible == false && ThumbnailGrid.IsGridActive == false)
            {
                // Only a single image is displayed: update the database for the current row with the control's value
                if (ImageCache?.Current == null)
                {
                    return;
                }
                FileDatabase.UpdateFile(ImageCache.Current.ID, datalabel, content);
            }
            else
            {
                // Multiple images are displayed: update the database for all selected rows with the control's value
                FileDatabase.UpdateFiles(ThumbnailGrid.GetSelected(), datalabel, content.Trim());
            }
        }
        #endregion

        #region Utilities
        public static bool TryFindFocusedControl(IInputElement focusedElement, out DataEntryControl focusedControl)
        {
            if (focusedElement is FrameworkElement focusedFrameworkElement)
            {
                try
                {
                    // this can fail if, for example, the element is MetadataDataEntryTag.
                    // While I think it won't happen, this is here as a precaustion.
                    focusedControl = (DataEntryControl)focusedFrameworkElement.Tag;
                }
                catch
                {
                    focusedControl = null;
                    return false;
                }

                if (focusedControl != null)
                {
                    return true;
                }

                // for complex controls which dynamic generate child controls, such as date time pickers, the tag of the focused element can't be set
                // so try to locate a parent of the focused element with a tag indicating the control
                FrameworkElement parent = null;
                if (focusedFrameworkElement.Parent != null && focusedFrameworkElement.TemplatedParent is FrameworkElement)
                {
                    parent = (FrameworkElement)focusedFrameworkElement.Parent;
                }
                else if (focusedFrameworkElement.TemplatedParent is FrameworkElement element)
                {
                    parent = element;
                }

                if (parent != null)
                {
                    return TryFindFocusedControl(parent, out focusedControl);
                }
            }
            focusedControl = null;
            return false;
        }

        // If the is a common (trimmed) data value for the provided data label in the given fileIDs, return that value, otherwise null.
        // If the controlType is provided, it is used to determine if it is a numeric type, as we would then use numeric equality tests.
        public string GetValueDisplayStringCommonToFileIds(string dataLabel, ControlContentStyleEnum controlType = ControlContentStyleEnum.ImprintNoteTextBox)
        {
            double contentsAsDouble = double.MaxValue;
            int contentsAsInteger = int.MaxValue;
            List<int> fileIds = ThumbnailGrid.GetSelected();
            // There used to be a bug in this code, which resulted from this being invoked in SwitchToThumbnailGridView() when the grid was already being displayed.
            //  I have kept the try/catch in just in case it rears its ugly head elsewhere. Commented out Debug statements are here just in case we need to reexamine it.
            try
            {
                // If there are no file ids, there is nothing to show
                if (fileIds.Count == 0)
                {
                    return null;
                }

                // This can cause the crash, when the id in fileIds[0] doesn't exist
                ImageRow imageRow = FileDatabase.FileTable[fileIds[0]];

                // The above line is what causes the crash, when the id in fileIds[0] doesn't exist
                // Debug.Print("Success: " + dataLabel + ": " + fileIds[0]);

                string contents = imageRow.GetValueDisplayString(dataLabel);
                contents = contents.Trim();
                // By default, we treat the contents as a string.
                // However, if its a numeric control type, we parse the contents as a number so we can use numeric comparisons
                if (controlType == ControlContentStyleEnum.DoubleTextBox)
                {
                    if (!double.TryParse(contents, NumberStyles.Float, CultureInfo.InvariantCulture, out contentsAsDouble))
                    {
                        // If we can't parse the first value as a double, then we should just treat it as a string
                        // simply by resetting the controlType
                        controlType = ControlContentStyleEnum.ImprintNoteTextBox;
                    }
                }
                else if (controlType == ControlContentStyleEnum.IntegerTextBox)
                {
                    // Not sure if we really need to do this with integers, but it does handle leading 0s (which may not occur)
                    if (!int.TryParse(contents, NumberStyles.Integer, CultureInfo.InvariantCulture, out contentsAsInteger))
                    {
                        // If we can't parse the first value as an integer, then we should just treat it as a string
                        // simply by resetting the controlType
                        controlType = ControlContentStyleEnum.ImprintNoteTextBox;
                    }
                }

                // If the values of success imagerows (as defined by the fileIDs) are the same as the first one,
                // then return that as they all have a common value. Otherwise return an empty string.
                int fileIdsCount = fileIds.Count;
                for (int i = 1; i < fileIdsCount; i++)
                {
                    imageRow = FileDatabase.FileTable[fileIds[i]];
                    string new_contents = imageRow.GetValueDisplayString(dataLabel);

                    // If its a double, parse it so we can use numeric comparisons
                    if (controlType == ControlContentStyleEnum.DoubleTextBox)
                    {
                        if (!double.TryParse(new_contents, NumberStyles.Float, CultureInfo.InvariantCulture, out var new_contentsAsDouble))
                        {
                            // If we can't parse the first value as a double, it won't be equal to anything else
                            return null;
                        }
                        if (Math.Abs(contentsAsDouble - new_contentsAsDouble) > 0.000000001)
                        {
                            // We have a mismatch
                            return null;
                        }
                    }
                    // If its an integer, parse it so we can use numeric comparisons.

                    else if (controlType == ControlContentStyleEnum.IntegerTextBox)
                    {
                        if (!int.TryParse(new_contents, NumberStyles.Integer, CultureInfo.InvariantCulture, out var new_contentsAsInteger))
                        {
                            // If we can't parse the first value as a double, it won't be equal to anything else
                            return null;
                        }
                        if (contentsAsInteger != new_contentsAsInteger)
                        {
                            // We have a mismatch
                            return null;
                        }
                    }
                    else
                    {
                        // Its not a number, so do a string comparison
                        new_contents = new_contents.Trim();
                        if (new_contents != contents)
                        {
                            // We have a mismatch
                            return null;
                        }
                    }
                }
                // All values match
                return contents;
            }
            catch
            {
                // This catch occurs when the id in fileIds[0] doesn't exist
                Debug.Write("Catch in GetValueDisplayStringCommonToFileIds: " + dataLabel);
                return null;
            }
        }
        #endregion

        #region Static public methods
        // Get a file path from the global datahandler. 
        // If we can't, or if it does not exist, return string.Empty
        public static string TryGetFilePathFromGlobalDataHandler()
        {
            // If anything is null, we defer resetting anything. Note that we may get an update later (e.g., via the timer)
            DataEntryHandler handler = GlobalReferences.MainWindow?.DataHandler;
            if (handler == null)
            {
                TracePrint.NullException(nameof(handler));
                return null;
            }
            if (handler.ImageCache?.Current == null)
            {
                TracePrint.NullException(nameof(handler.ImageCache));
                return null;
            }
            if (handler.ImageCache?.CurrentDifferenceState != null && handler.FileDatabase != null)
            {
                // Get the path
                string path = handler.ImageCache.Current.GetFilePath(handler.FileDatabase.RootPathToImages);
                return File.Exists(path) ? path : null;
            }
            return null;
        }
        #endregion

        #region Disposing

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                FileDatabase?.Dispose();
            }
            disposed = true;
        }

        public void DisposeAsNeeded()
        {
            try
            {
                Dispose();
                ImageCache = null;
                FileDatabase = null;
            }
            catch
            {
                Debug.Print("Failed in DataEntryHandler-DisposeAsNeeded");
            }
        }
        #endregion
    }
}
