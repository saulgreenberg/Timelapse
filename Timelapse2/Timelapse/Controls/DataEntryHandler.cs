using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.Dialog;
using Timelapse.Images;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Controls
{
    /// <summary>
    /// The code in here propagates values of a control across the various images in various ways.
    /// Note that this is control-type specific, which means this code would have to be modified to handle new control types
    /// Pay attention to the hacks described by SAULXXX DateTimePicker Workaround as these may not be needed if future versions of the DateTimePicker work as they are supposed to.
    /// </summary>
    public class DataEntryHandler : IDisposable
    {
        #region Public Properties and Private variables
        public FileDatabase FileDatabase { get; private set; }
        public ImageCache ImageCache { get; private set; }
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

        #region Loadin
        public DataEntryHandler(FileDatabase fileDatabase)
        {
            this.disposed = false;
            this.ImageCache = new ImageCache(fileDatabase);
            this.FileDatabase = fileDatabase;  // We need a reference to the database if we are going to update it.
            this.IsProgrammaticControlUpdate = false;
        }

        #endregion

        #region Configuration, including Callback Configuration
        public static void Configure(DateTimePicker dateTimePicker, DateTime? defaultValue)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(dateTimePicker, nameof(dateTimePicker));

            dateTimePicker.AutoCloseCalendar = true;
            dateTimePicker.Format = DateTimeFormat.Custom;
            dateTimePicker.FormatString = Constant.Time.DateTimeDisplayFormat;
            dateTimePicker.TimeFormat = DateTimeFormat.Custom;
            dateTimePicker.TimeFormatString = Constant.Time.TimeFormat;
            dateTimePicker.CultureInfo = System.Globalization.CultureInfo.CreateSpecificCulture("en-US");
            dateTimePicker.Value = defaultValue;
        }

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
                string controlType = this.FileDatabase.FileTableColumnsByDataLabel[pair.Key].ControlType;
                switch (controlType)
                {
                    case Constant.Control.Note:
                    case Constant.DatabaseColumn.File:
                    case Constant.DatabaseColumn.RelativePath:
                        DataEntryNote note = (DataEntryNote)pair.Value;
                        note.ContentControl.TextAutocompleted += this.NoteControl_TextAutocompleted;
                        //if (controlType == Constant.Control.Note)
                        //{
                        this.SetContextMenuCallbacks(note);
                        //}
                        break;
                    case Constant.DatabaseColumn.DateTime:
                        // Note. There are several issues with the XCEED DateTimePicker. In particular, the date in the 
                        // text date area is not well coordinated with the date in the calendar, i.e., the two aren't necessarily in
                        // sync. As well, changing a date on the calendar doesnt' appear to trigger the DateTimeContro_ValueChanged event
                        // Various workarounds are implemented as commented below with SAULXXX DateTimePicker Workaround.
                        // If the toolkit is updated to fix them, then those workarounds can be deleted (but test them first).
                        DataEntryDateTime dateTime = (DataEntryDateTime)pair.Value;
                        dateTime.ContentControl.ValueChanged += this.DateTimeControl_ValueChanged;
                        // We need the lines below as otherwise it will show the panel's context menu, which is confusing, instead of nothing
                        dateTime.ContentControl.ContextMenu = new ContextMenu
                        {
                            Visibility = Visibility.Collapsed
                        };

                        // SAULXXX DateTimePicker Workaround. 
                        // We need to access the calendar part of the DateTImePicker, but 
                        // we can't do that until the control is loaded.
                        dateTime.ContentControl.Loaded += this.DateTimePicker_Loaded;
                        break;
                    case Constant.DatabaseColumn.DeleteFlag:
                    case Constant.Control.Flag:
                        DataEntryFlag flag = (DataEntryFlag)pair.Value;
                        flag.ContentControl.Checked += this.FlagControl_CheckedChanged;
                        flag.ContentControl.Unchecked += this.FlagControl_CheckedChanged;
                        this.SetContextMenuCallbacks(flag);
                        break;
                    case Constant.Control.FixedChoice:
                        DataEntryChoice choice = (DataEntryChoice)pair.Value;
                        choice.ContentControl.SelectionChanged += this.ChoiceControl_SelectionChanged;
                        this.SetContextMenuCallbacks(choice);
                        break;
                    case Constant.Control.Counter:
                        DataEntryCounter counter = (DataEntryCounter)pair.Value;
                        counter.ContentControl.ValueChanged += this.CounterControl_ValueChanged;
                        this.SetContextMenuCallbacks(counter);
                        break;
                }
            }
        }

        // SAULXXX DateTimePicker Workaround. 
        // Access the calendar part of the datetimepicker, and
        // add an event to it that is triggered whenever the user changes the calendar.
        // For convenience, we use the calendar's tag to store the DateTimePicker control so we can retrieve it from the event.
        private void DateTimePicker_Loaded(object sender, RoutedEventArgs e)
        {
            DateTimePicker dateTimePicker = sender as DateTimePicker;
            if (dateTimePicker == null) return;
            if (dateTimePicker.Template.FindName("PART_Calendar", dateTimePicker) is Calendar calendar)
            {
                // Debug.Print("DateTimePicker_Loaded: Adding calendar event ");
                calendar.Tag = dateTimePicker;
                calendar.IsTodayHighlighted = false; // Don't highlight today's date, as it could be confusing given what this control is used for.
                calendar.SelectedDatesChanged += this.Calendar_SelectedDatesChanged;
            }
        }

        // Create the Context menu, incluidng settings its callbakcs
        private void SetContextMenuCallbacks(DataEntryControl control)
        {
            if (GlobalReferences.TimelapseState.IsViewOnly)
            {
                // In view-only mode, we don't create these menus as they allow editing
                return;
            }

            // Start with an empty clipboard
            // Its in a try / catch as one user reported an unusual error: OpenClipboardFailed
            try
            {
                Clipboard.SetText(String.Empty);
            }
            catch
            {
                Debug.Print("Error in setting text in clipboard (see SetContextMenuCallbacks in DataEntryHandler");
            }

            MenuItem menuItemPropagateFromLastValue = new MenuItem()
            {
                IsCheckable = false,
                Tag = control,
                Header = "Propagate from the last non-empty value to here"
            };
            if (control is DataEntryCounter)
            {
                menuItemPropagateFromLastValue.Header = "Propagate from the last non-zero value to here";
            }
            menuItemPropagateFromLastValue.Click += this.MenuItemPropagateFromLastValue_Click;

            MenuItem menuItemCopyForward = new MenuItem()
            {
                IsCheckable = false,
                Header = "Copy forward to end",
                ToolTip = "The value of this field will be copied forward from this file to the last file in this set",
                Tag = control
            };
            menuItemCopyForward.Click += this.MenuItemPropagateForward_Click;
            MenuItem menuItemCopyCurrentValue = new MenuItem()
            {
                IsCheckable = false,
                Header = "Copy to all",
                Tag = control
            };
            menuItemCopyCurrentValue.Click += this.MenuItemCopyCurrentValueToAll_Click;

            MenuItem menuItemCopy = new MenuItem()
            {
                IsCheckable = false,
                Header = "Copy",
                ToolTip = "Copy will copy this field's entire content to the clipboard",
                Tag = control
            };
            menuItemCopy.Click += this.MenuItemCopyToClipboard_Click;

            MenuItem menuItemPaste = new MenuItem()
            {
                IsCheckable = false,
                Header = "Paste",
                ToolTip = "Paste will replace this field's content with the clipboard's content",
                Tag = control
            };
            menuItemPaste.Click += this.MenuItemPasteFromClipboard_Click;

            // DataEntrHandler.PropagateFromLastValueIndex and CopyForwardIndex must be kept in sync with the add order here
            ContextMenu menu = new ContextMenu();
            menu.Items.Add(menuItemPropagateFromLastValue);
            menu.Items.Add(menuItemCopyForward);
            menu.Items.Add(menuItemCopyCurrentValue);
            Separator menuSeparator = new Separator();
            menu.Items.Add(new Separator());
            menu.Items.Add(menuItemCopy);
            menu.Items.Add(menuItemPaste);

            control.Container.ContextMenu = menu;
            control.Container.PreviewMouseRightButtonDown += this.Container_PreviewMouseRightButtonDown;

            // For the File/RelativePath controls, all which are read only, hide the irrelevant menu items.
            // This could be made more efficient by simply not creating those items, but given the low case we just left it as is.
            if (control.DataLabel == Constant.DatabaseColumn.File || control.DataLabel == Constant.DatabaseColumn.RelativePath)
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
            else if (control is DataEntryNote note)
            {
                note.ContentControl.ContextMenu = menu;
            }
            else if (control is DataEntryChoice choice)
            {
                choice.ContentControl.ContextMenu = menu;
            }
            else if (control is DataEntryFlag flag)
            {
                flag.ContentControl.ContextMenu = menu;
            }
            else
            {
                throw new NotSupportedException($"Unhandled control type {control.GetType().Name}.");
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
            string valueToCopy = checkForZero ? "0" : String.Empty;

            // Search for the row with some value in it, starting from the previous row
            int currentRowIndex = (this.ThumbnailGrid.IsVisible == false) ? this.ImageCache.CurrentRow : this.ThumbnailGrid.GetSelected()[0];
            for (int previousIndex = currentRowIndex - 1; previousIndex >= 0; previousIndex--)
            {
                ImageRow file = this.FileDatabase.FileTable[previousIndex];
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
                        (isFlag && !valueToCopy.Equals(Constant.BooleanValue.False, StringComparison.OrdinalIgnoreCase)) || // Skip over false values for flags
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
            if (Dialogs.DataEntryConfirmPropagateFromLastValueDialog(Application.Current.MainWindow, valueToCopy, filesAffected) != true)
            {
                return; // operation cancelled
                // newContent = this.FileDatabase.FileTable[currentRowIndex].GetValueDisplayString(control.DataLabel); // No change, so return the current value
            }

            // Update the affected files. Note that we start on the row after the one with a value in it to the current row.
            Mouse.OverrideCursor = Cursors.Wait;
            this.FileDatabase.UpdateFiles(valueSource, control.DataLabel, indexToCopyFrom + 1, currentRowIndex);
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

            // Display a dialog box that explains what will happen. Arguments indicate how many files will be affected, and is tuned to the type of control 
            bool checkForZero = control is DataEntryCounter;
            int filesAffected = this.FileDatabase.CountAllCurrentlySelectedFiles;
            if (Dialogs.DataEntryConfirmCopyCurrentValueToAllDialog(Application.Current.MainWindow, control.Content, filesAffected, checkForZero) != true)
            {
                return;
            }

            // Update all files to match the value of the control (identified by the data label) in the currently selected image row.
            Mouse.OverrideCursor = Cursors.Wait;
            ImageRow imageRow = (this.ThumbnailGrid.IsVisible == false) ? this.ImageCache.Current : this.FileDatabase.FileTable[this.ThumbnailGrid.GetSelected()[0]];
            this.FileDatabase.UpdateFiles(imageRow, control.DataLabel);
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

            int currentRowIndex = (this.ThumbnailGrid.IsVisible == false) ? this.ImageCache.CurrentRow : this.ThumbnailGrid.GetSelected()[0];
            int imagesAffected = this.FileDatabase.CountAllCurrentlySelectedFiles - currentRowIndex - 1;
            if (imagesAffected == 0)
            {
                // Display a dialog box saying there is nothing to copy forward. 
                // Note that this should never be displayed, as the menu shouldn't be highlit if we are on the last image
                // But just in case...
                Dialogs.DataEntryNothingToCopyForwardDialog(Application.Current.MainWindow);
                return;
            }

            // Display the appropriate dialog box that explains what will happen. Arguments indicate how many files will be affected, and is tuned to the type of control 
            ImageRow imageRow = (this.ThumbnailGrid.IsVisible == false) ? this.ImageCache.Current : this.FileDatabase.FileTable[this.ThumbnailGrid.GetSelected()[0]];
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
            int nextRowIndex = (this.ThumbnailGrid.IsVisible == false) ? this.ImageCache.CurrentRow + 1 : this.ThumbnailGrid.GetSelected()[0] + 1;
            this.FileDatabase.UpdateFiles(imageRow, control.DataLabel, nextRowIndex, this.FileDatabase.CountAllCurrentlySelectedFiles - 1);
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
            this.UpdateRowsDependingOnThumbnailGridState(control.DataLabel, newContent);
        }

        // Enable or disable particular context menu items
        protected virtual void Container_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(sender, nameof(sender));

            StackPanel stackPanel = (StackPanel)sender;
            DataEntryControl control = (DataEntryControl)stackPanel.Tag;

            if (stackPanel.ContextMenu == null)
            {
                TracePrint.NullException(nameof(stackPanel));
                return;
            }
            MenuItem menuItemCopyToAll = (MenuItem)stackPanel.ContextMenu.Items[DataEntryHandler.CopyToAllIndex];
            MenuItem menuItemCopyForward = (MenuItem)stackPanel.ContextMenu.Items[DataEntryHandler.CopyForwardIndex];
            MenuItem menuItemPropagateFromLastValue = (MenuItem)stackPanel.ContextMenu.Items[DataEntryHandler.PropagateFromLastValueIndex];
            MenuItem menuItemCopyToClipboard = (MenuItem)stackPanel.ContextMenu.Items[DataEntryHandler.CopyToClipboardIndex];
            MenuItem menuItemPasteFromClipboard = (MenuItem)stackPanel.ContextMenu.Items[DataEntryHandler.PasteFromClipboardIndex];

            // Behaviour: 
            // - if the ThumbnailInCell is visible, disable Copy to all / Copy forward / Propagate if a single item isn't selected
            // - otherwise enable the menu item only if the resulting action is coherent
            bool enabledIsPossible = this.ThumbnailGrid.IsVisible == false || this.ThumbnailGrid.SelectedCount() == 1;
            menuItemCopyToAll.IsEnabled = enabledIsPossible;
            menuItemCopyForward.IsEnabled = enabledIsPossible && (menuItemCopyForward.IsEnabled = this.IsCopyForwardPossible());
            menuItemPropagateFromLastValue.IsEnabled = enabledIsPossible && this.IsCopyFromLastNonEmptyValuePossible(control);

            // Enable Copy menu if
            // - its not empty / white space and not in the overview with different contents (i.e., ellipsis is showing)
            menuItemCopyToClipboard.IsEnabled = !(String.IsNullOrWhiteSpace(control.Content) || control.Content == Constant.Unicode.Ellipsis);

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
                clipboardText = String.Empty;
                Debug.Print("Error in setting text in clipboard (see Container_PreviewMouseRightButtonDown in DataEntryHandler");
            }
            if (string.IsNullOrEmpty(clipboardText))
            {
                menuItemPasteFromClipboard.IsEnabled = false;
            }
            else
            {
                if (control is DataEntryNote)
                {
                    // Any string is valid
                    menuItemPasteFromClipboard.IsEnabled = true;
                }
                else if (control is DataEntryFlag)
                {
                    // Only true / false is valid
                    menuItemPasteFromClipboard.IsEnabled = (clipboardText == "true" || clipboardText == "false");
                }
                else if (control is DataEntryCounter)
                {
                    // Only a positive integer is valid
                    menuItemPasteFromClipboard.IsEnabled = Int32.TryParse(clipboardText, out int x) && x >= 0;
                }
                else if (control is DataEntryChoice choiceControl)
                {
                    // Only a value present as a menu choice is valid 
                    menuItemPasteFromClipboard.IsEnabled = false;
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

                // Alter the paste header to show the text that will be pasted e.g Paste 'Lion'
                if (menuItemPasteFromClipboard.IsEnabled)
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
                    menuItemPasteFromClipboard.Header = "Paste '" + (clipboardText.Length > 20 ? clipboardText.Substring(0, 20) + Constant.Unicode.Ellipsis : clipboardText) + "'";
                }
                else
                {
                    // Since there is nothing in the clipboard, just show 'Paste'
                    menuItemPasteFromClipboard.Header = "Paste";
                }

                // Alter the copy header to show the text that will be copied, i.e. Copy 'Lion'
                if (menuItemCopyToClipboard.IsEnabled)
                {
                    string content = control.Content.Trim();
                    menuItemCopyToClipboard.Header = "Copy '" + (content.Length > 20 ? content.Substring(0, 20) + Constant.Unicode.Ellipsis : content) + "'";
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
            if (this.ImageCache.Current == null)
            {
                return false;
            }

            // The current row depends on wheter we are in the thumbnail grid or the normal view
            int currentRow = (this.ThumbnailGrid.IsVisible == false) ? this.ImageCache.CurrentRow : this.ThumbnailGrid.GetSelected()[0];
            int filesAffected = this.FileDatabase.CountAllCurrentlySelectedFiles - currentRow - 1;
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

            int currentIndex = 0; // So we can print the value in the catch
            bool checkCounter = control is DataEntryCounter;
            bool checkFlag = control is DataEntryFlag;
            int nearestRowWithCopyableValue = -1;
            // Its in a try/catch as very very occassionally we get a 'system.indexoutofrangeexception'
            try
            {
                // The current row depends on wheter we are in the thumbnail grid or the normal view
                int currentRow = (this.ThumbnailGrid.IsVisible == false) ? this.ImageCache.CurrentRow : this.ThumbnailGrid.GetSelected()[0];
                for (int fileIndex = currentRow - 1; fileIndex >= 0; fileIndex--)
                {
                    currentIndex = fileIndex;
                    // Search for the row with some value in it, starting from the previous row
                    string valueToCopy = this.FileDatabase.FileTable[fileIndex].GetValueDatabaseString(control.DataLabel);
                    if (String.IsNullOrWhiteSpace(valueToCopy) == false)
                    {
                        // for flags, we skip over falses
                        // for counters, we skip over 0
                        // for all others, any value will work as long as its not null or white space
                        if ((checkFlag && !valueToCopy.Equals("false")) ||
                             (checkCounter && !valueToCopy.Equals("0")) ||
                             (!checkCounter && !checkFlag))
                        {
                            nearestRowWithCopyableValue = fileIndex;    // We found a non-empty value
                            break;
                        }
                    }
                }
            }
            catch (IndexOutOfRangeException e)
            {
                // I don't know why we get this occassional error, so this is an attempt to print out the result so we can debug it
                Debug.Print(
                    $"IsCopyFromLastNonEmptyValuePossible: IndexOutOfRange Exception, where index is: {currentIndex}{Environment.NewLine}{e.Message}");
                return (nearestRowWithCopyableValue >= 0);
            }
            return (nearestRowWithCopyableValue >= 0);
        }
        #endregion

        #region Event handlers - Content Selections and Changes
        private void DateTimeControl_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Debug.Print("DateTimeControl_ValueChanged triggered");
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }

            DateTimePicker dateTimePicker = (DateTimePicker)sender;
            if (dateTimePicker.Value.HasValue == false)
            {
                return;
            }
            // Update file data table and write the new DateTime, Date, and Time to the database
            this.DateTimeUpdate(dateTimePicker, dateTimePicker.Value.Value);

            // SAULXXX DateTimePicker Workaround. 
            // There is a bug (?) in the dateTimePicker where it doesn't update the calendar to the
            // changed date. This means that if you open the calendar it shows the
            // original date, and when you close it (even without selecting a date) it reverts to the old date.
            // The fix below updates the calendar to the current date.
            if (dateTimePicker.Template.FindName("PART_Calendar", dateTimePicker) is Calendar calendar)
            {
                this.IsProgrammaticControlUpdate = true;
                // Debug.Print("Got it " + calendar.ToString());
                calendar.DisplayDate = dateTimePicker.Value.Value;
                calendar.SelectedDate = dateTimePicker.Value.Value;
                if (calendar.Template.FindName("PART_TimePicker", calendar) is TimePicker timepicker)
                {
                    timepicker.Value = dateTimePicker.Value.Value;
                    // Debug.Print("Setting Time pickker");
                }
                this.IsProgrammaticControlUpdate = false;
            }
            // else
            // {
            //    Debug.Print("Not a calendar");
            // }
        }

        // SAULXXX DateTimePicker Workaround. 
        // Sync changes from the datetimepicker's calendar back to the datetimepicker text 
        // and updates the database
        private void Calendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }
            Calendar calendar = sender as Calendar;
            if (calendar == null)
            {
                TracePrint.NullException(nameof(calendar));
                return;
            }
            DateTimePicker dateTimePicker = (DateTimePicker)calendar.Tag;
            if (dateTimePicker == null)
            {
                TracePrint.NullException(nameof(dateTimePicker));
                return;
            }
            if (dateTimePicker.Value == null)
            {
                TracePrint.NullException(nameof(dateTimePicker.Value));
                return;
            }
            TimeSpan timespan = dateTimePicker.Value.Value.TimeOfDay;
            this.IsProgrammaticControlUpdate = true;
            dateTimePicker.Value = calendar.SelectedDate + timespan; // + dateTimePicker.Value.Value.TimeOfDay;

            // Update file data table and write the new DateTime, Date, and Time to the database
            if (dateTimePicker.Value != null)
            {
                this.DateTimeUpdate(dateTimePicker, (DateTime)dateTimePicker.Value);
            }
            else
            {
                TracePrint.NullException(nameof(dateTimePicker.Value));
            }

            // Debug.Print("Got calendar event " + calendar.SelectedDate.ToString());
            this.IsProgrammaticControlUpdate = false;
        }

        // Helper method for above DateTime changes.
        private void DateTimeUpdate(DateTimePicker dateTimePicker, DateTime dateTime)
        {
            // update file data table and write the new DateTime, Date, and Time to the database
            if (this.ImageCache?.Current == null)
            {
                TracePrint.NullException(nameof(this.ImageCache));
                return;
            }
            this.ImageCache.Current.SetDateTime(dateTime);
            dateTimePicker.ToolTip = DateTimeHandler.ToStringDisplayDateTime(dateTime);

            List<ColumnTuplesWithWhere> imageToUpdate = new List<ColumnTuplesWithWhere>() { this.ImageCache.Current.GetDateTimeColumnTuples() };
            this.FileDatabase.UpdateFiles(imageToUpdate);
        }

        // When the text in a particular note box changes, update the particular note field(s) in the database 
        private void NoteControl_TextAutocompleted(object sender, TextChangedEventArgs e)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }

            DataEntryNote control = (DataEntryNote)((TextBox)sender).Tag;
            control.ContentChanged = true;

            // Note that  trailing whitespace is removed only from the database as further edits may use it.
            this.UpdateRowsDependingOnThumbnailGridState(control.DataLabel, control.Content.Trim());
        }

        // When the number in a particular counter box changes, update the particular counter field(s) in the database
        private void CounterControl_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }
            IntegerUpDown integerUpDown = (IntegerUpDown)sender;

            // Get the key identifying the control, and then add its value to the database
            DataEntryControl control = (DataEntryControl)integerUpDown.Tag;
            control.SetContentAndTooltip(integerUpDown.Value.ToString());
            this.UpdateRowsDependingOnThumbnailGridState(control.DataLabel, control.Content);
        }

        // When a choice changes, update the particular choice field(s) in the database
        private void ChoiceControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }
            ComboBox comboBox = (ComboBox)sender;

            if (comboBox.SelectedItem == null)
            {
                // no item selected (probably the user cancelled)
                return;
            }

            // Get the key identifying the control, and then add its value to the database
            DataEntryControl control = (DataEntryControl)comboBox.Tag;
            // This guards against selecting the Separator
            if (comboBox.SelectedItem is ComboBoxItem cbi)
            {
                control.SetContentAndTooltip(cbi.Content.ToString());
            }
            this.UpdateRowsDependingOnThumbnailGridState(control.DataLabel, control.Content);
        }

        // When a flag changes, update the particular flag field(s) in the database
        private void FlagControl_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (this.IsProgrammaticControlUpdate)
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
            string value = checkBox.IsChecked == true ? Constant.BooleanValue.True : Constant.BooleanValue.False;

            control.SetContentAndTooltip(value);
            this.UpdateRowsDependingOnThumbnailGridState(control.DataLabel, control.Content);
        }
        #endregion

        #region Update Rows
        // Update either the current row or the selected rows in the database, 
        // depending upon whether we are in the single image or  theThumbnailGrid view respectively.
        private void UpdateRowsDependingOnThumbnailGridState(string datalabel, string content)
        {
            if (this.ThumbnailGrid.IsVisible == false && this.ThumbnailGrid.IsGridActive == false)
            {
                // Only a single image is displayed: update the database for the current row with the control's value
                if (this.ImageCache?.Current == null)
                {
                    TracePrint.NullException(nameof(ImageCache));
                    return;
                }
                this.FileDatabase.UpdateFile(this.ImageCache.Current.ID, datalabel, content);
            }
            else
            {
                // Multiple images are displayed: update the database for all selected rows with the control's value
                this.FileDatabase.UpdateFiles(this.ThumbnailGrid.GetSelected(), datalabel, content.Trim());
            }
        }
        #endregion

        #region Utilities
        public static bool TryFindFocusedControl(IInputElement focusedElement, out DataEntryControl focusedControl)
        {
            if (focusedElement is FrameworkElement focusedFrameworkElement)
            {
                focusedControl = (DataEntryControl)focusedFrameworkElement.Tag;
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
                else if (focusedFrameworkElement.TemplatedParent != null && focusedFrameworkElement.TemplatedParent is FrameworkElement element)
                {
                    parent = element;
                }

                if (parent != null)
                {
                    return DataEntryHandler.TryFindFocusedControl(parent, out focusedControl);
                }
            }
            focusedControl = null;
            return false;
        }

        // If the is a common (trimmed) data value for the provided data label in the given fileIDs, return that value, otherwise null.
        public string GetValueDisplayStringCommonToFileIds(string dataLabel)
        {
            List<int> fileIds = this.ThumbnailGrid.GetSelected();
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
                ImageRow imageRow = this.FileDatabase.FileTable[fileIds[0]];

                // The above line is what causes the crash, when the id in fileIds[0] doesn't exist
                // Debug.Print("Success: " + dataLabel + ": " + fileIds[0]);

                string contents = imageRow.GetValueDisplayString(dataLabel);
                contents = contents.Trim();

                // If the values of success imagerows (as defined by the fileIDs) are the same as the first one,
                // then return that as they all have a common value. Otherwise return an empty string.
                int fileIdsCount = (fileIds == null) ? 0 : fileIds.Count;
                for (int i = 1; i < fileIdsCount; i++)
                {
                    imageRow = this.FileDatabase.FileTable[fileIds[i]];
                    string new_contents = imageRow.GetValueDisplayString(dataLabel);
                    new_contents = new_contents.Trim();
                    if (new_contents != contents)
                    {
                        // We have a mismatch
                        return null;
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
        // If we can't, or if it does not exist, return String.Empty
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
                string path = handler.ImageCache.Current.GetFilePath(handler.FileDatabase.FolderPath);
                return File.Exists(path) ? path : null;
            }
            return null;
        }
        #endregion

        #region Disposing

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.FileDatabase?.Dispose();
            }
            this.disposed = true;
        }
        
        public void DisposeAsNeeded()
        {
            try
            {
                this.Dispose(); 
                this.ImageCache = null;
                this.FileDatabase = null;   
            }
            catch
            {
                Debug.Print("Failed in DataEntryHandler-DisposeAsNeeded");
            }
        }
        #endregion
    }
}
