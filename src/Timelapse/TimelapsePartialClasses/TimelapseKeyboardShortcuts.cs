using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.ControlsMetadata;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.Util;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    // Keyboard shortcuts
    public partial class TimelapseWindow
    {
        #region Callbacks - PreviewKeyDown and PreviewKeyUp
        // If its an arrow key and the textbox doesn't have the focus,
        // navigate left/right image or up/down to look at differenced image
        private void Window_PreviewKeyDown(object sender, KeyEventArgs currentKey)
        {
            if (DataHandler == null ||
                DataHandler.FileDatabase == null ||
                DataHandler.FileDatabase.CountAllCurrentlySelectedFiles == 0)
            {
                // PERHAPS BUG - this only works when the datagrid pane is in a tab, and when files are loaded.
                // Maybe we need to change the enable state?
                // TODO: Not really sure what these keys do.
                switch (currentKey.Key)
                {
                    case Key.Home:
                        ImageSetPane.IsEnabled = true;
                        ImageSetPane.IsSelected = true;
                        break;
                    case Key.End:
                        DataGridPane.IsEnabled = true;
                        DataGridPane.IsSelected = true;
                        // ToDo If its floating, we should really be making it topmost by iterating through the floating windows and set it. e.g., by if (this.DataGridPane.IsFloating){}
                        break;
                }
                return; // No images are loaded, so don't try to interpret any keys
            }
            Handle_PreviewKeyDown(currentKey, false);
        }

        // There is a bug in avalondock, where a floating window will always have the IsRepeat set to true. 
        // Thus we have to implement our own version of it
        private void Window_PreviewKeyUp(object sender, KeyEventArgs currentKey)
        {
            // Force the end of a key repeat cycle
            State.ResetKeyRepeat();
        }
        #endregion

        #region Handle PreviewKeyDown - This actually does all the work
        public void Handle_PreviewKeyDown(KeyEventArgs currentKey, bool forceSendToMainWindow)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(currentKey, nameof(currentKey));

            // QuickPaste shortcut key?
            // If so, translate key into a quickpaste shortcut key. 
            //        send it to the Quickpaste window and mark the event as handled.
            if (IsCondition.IsKeyControlDown() &&
                 (currentKey.Key is >= Key.D0 and <= Key.D9 || currentKey.Key is >= Key.NumPad0 and <= Key.NumPad9))
            {
                if (quickPasteWindow is { Visibility: Visibility.Visible })
                {
                    // The quickpaste window is visible, and thus able to take shortcuts.
                    string key = new KeyConverter().ConvertToString(currentKey.Key);
                    if (key == null)
                    {
                        // Shouldn't happen
                        TracePrint.NullException(nameof(key));
                        return;
                    }
                    if (key.StartsWith("NumPad"))
                    {
                        key = key.Remove(0, 6);
                    }
                    if (Int32.TryParse(key, out int shortcutIndex) && shortcutIndex != 0)
                    {
                        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                        {
                            // if Shift is pressed, this specifies the quickkey range 10 - 18, so we add 9 to it.
                            shortcutIndex += 9;
                        }
                        quickPasteWindow.TryQuickPasteShortcut(shortcutIndex);
                        currentKey.Handled = true;
                    }
                }
                return;
            }

            // Next, - but only if forceSendToMainWindow is true,
            // don't interpret keyboard shortcuts if the focus is on a control in the control grid, as the text entered may be directed
            // to the controls within it. That is, if a textbox or combo box or other control has the focus, then take no as this is normal text input
            // and NOT a shortcut key.  Similarly, if a menu is displayed keys it should be directed to the menu rather than interpreted as
            // shortcuts.
            if (forceSendToMainWindow == false && SendKeyToDataEntryControlOrMenu(currentKey))
            {
                return;
            }

            // Now test for particular shortcut keys and take the appropriate action as needed
            int keyRepeatCount = State.GetKeyRepeatCount(currentKey);
            switch (currentKey.Key)
            {
                case Key.F:
                    if (IsCondition.IsKeyControlDown())
                    {
                        // Open the FindBox
                        FindBoxSetVisibility(true);
                    }
                    break;

                case Key.S:
                    // Open the custom select dialog.
                    if (IsCondition.IsKeyControlDown() && !currentKey.IsRepeat)
                    {

                        // TODO not sure if this allows the custom select dialog to be opened when it shouldn't be...
                        // should add further checks perhaps?
                        this.MenuItemSelectCustomSelection_Click(null, null);
                    }
                    break;

                case Key.F5:
                    // Refresh the current selection
                    if (!currentKey.IsRepeat)
                    {
                        this.MenuItemSelectReselect_Click(null, null);
                    }
                    break;

                case Key.B:
                    // Save a Bookmark of the current pan / zoom level of the image
                    MarkableCanvas.SetBookmark();
                    break;

                case Key.Escape:
                    // Set focus to MarkableCanvas
                    TrySetKeyboardFocusToMarkableCanvas(false, currentKey);
                    break;


                case Key.OemPlus: // +
                case Key.Add:
                    // Restore the zoom level / pan coordinates of the bookmark
                    MarkableCanvas.ApplyBookmark();
                    break;


                case Key.OemMinus: // -
                case Key.Subtract:
                    // Restore the zoom level / pan coordinates of the bookmark
                    MarkableCanvas.ZoomOutAllTheWay();
                    break;


                case Key.K:
                    // Dogear the current image or switch between the dogear and the last seen image
                    if (this.MarkableCanvas.IsThumbnailGridVisible)
                    {
                        return;
                    }

                    if (IsCondition.IsKeyShiftDown())
                    {
                        // shift-K: Set the dogear to the current image
                        this.ImageDogear?.TrySetDogearToCurrentImage();
                    }
                    else
                    {
                        // K: Toggle to/from the doggeared image from/to the last seen images
                        if (this.ImageDogear != null)
                        {
                            int index = this.ImageDogear.TryGetDogearOrPreviouslySeenImageIndex();
                            if (index != Constant.DatabaseValues.InvalidRow)
                            {
                                // Show the image at the bookmark index
                                this.FileShow(index);
                            }
                        }
                    }
                    break;

                case Key.M:
                    // Toggle the magnifying glass on and off
                    MenuItemDisplayMagnifyingGlass_Click(this, null);
                    break;

                case Key.U:
                    // Increase the magnifing glass zoom level
                    FilePlayer_Stop();      // In case the FilePlayer is going
                    MarkableCanvas.MagnifierOrOffsetChangeZoomLevel(ZoomDirection.ZoomIn);
                    break;

                case Key.D:
                    // Decrease the magnifing glass zoom level
                    if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                    {
                        if (!currentKey.IsRepeat)
                        {
                            if (!currentKey.IsRepeat && Control.ModifierKeys == (Keys.Control | Keys.Shift))
                            {
                                // ctl-shift-D: Duplicate record with defaults
                                MenuItemEditDuplicateRecord_Click(this.MenuItemDuplicateRecordUsingDefaultValues, null);
                            }
                            else
                            {
                                // ctl-d: Duplicate record with current values
                                MenuItemEditDuplicateRecord_Click(this.MenuItemDuplicateRecordUsingCurrentValues, null);
                            }
                        }
                    }
                    else
                    {
                        FilePlayer_Stop();      // In case the FilePlayer is going
                        MarkableCanvas.MagnifierOrOffsetChangeZoomLevel(ZoomDirection.ZoomOut);
                    }
                    break;

                case Key.Right:
                case Key.Left:
                case Key.PageDown:
                case Key.PageUp:
                    // next /previous image
                    // Note that if individual data entry controls have the focus (see DataEntryControls.cs),
                    // they will handle left/right arrow keys themselves, and only pass the key to here if Ctl is pressed
                    // This allows more flexible shortcut use. For example:
                    // - if a control has the focus, it
                    //    - uses the left/right for its own navigation, but
                    //    - passes Ctl-left/right to here to navigate images and episodes.
                    // - if the markable canvas has the focus, it
                    //   - also allows the left/right to be used to navigate images and episodes.
                    FilePlayer_Stop();      // In case the FilePlayer is going
                    var direction = currentKey.Key is Key.Right or Key.PageDown
                        ? DirectionEnum.Next
                        : DirectionEnum.Previous;

                    // Initially, assume that we are just going to the next/previous file.
                    int increment = 1;

                    // If the shift key is down, modify the increment so it goes to the beginning of the next episode.
                    if (IsCondition.IsKeyShiftDown())
                    {
                        if (DataHandler.ImageCache.Current == null)
                        {
                            // Shouldn't happen
                            TracePrint.NullException(nameof(DataHandler.ImageCache.Current));
                            return;
                        }
                        long currentFileID = DataHandler.ImageCache.Current.ID;
                        bool result = Episodes.Episodes.GetIncrementToNextEpisode(DataHandler.FileDatabase.FileTable, DataHandler.FileDatabase.GetFileOrNextFileIndex(currentFileID), direction, out increment);
                        if (result)
                        {
                            if (Episodes.Episodes.ShowEpisodes == false)
                            {
                                // turn on Episode display if its not already on
                                EpisodeShowHide(true);
                            }
                            // At this point, the episodes should be showing and the increment amount should be reset (see the out parameter above)
                        }
                    }

                    // Now show the file at the indicated increment / diretion
                    if (currentKey.IsRepeat == false || (currentKey.IsRepeat && keyRepeatCount % State.Throttles.RepeatedKeyAcceptanceInterval == 0))
                    {

                        TryFileShowWithoutSliderCallback(direction, increment);
                    }
                    break;

                case Key.Up:
                case Key.Down:
                    // See  constraints to Left/Right arrow keys, as similar constraints apply here.
                    // Single view 
                    // - Up/Down image processing
                    // - Ctl and Ctl-Shift Up/Down - Same 
                    // Overview    ud - next row
                    // - Up/Down - Next row
                    // - Ctl Up/Down - next row
                    // - Ctl Shift UD - next page
                    bool up = currentKey.Key == Key.Up;
                    if (IsDisplayingMultipleImagesInOverview())
                    {
                        // Go to next row in the overview
                        FilePlayer.Direction = up ? DirectionEnum.Previous : DirectionEnum.Next;
                        if (IsCondition.IsKeyShiftDown())
                        {
                            FilePlayer_ScrollPage();
                        }
                        else
                        {
                            FilePlayer_ScrollRow();
                        }
                    }
                    else
                    {
                        // show visual difference to next image
                        FilePlayer_Stop(); // In case the FilePlayer is going
                        if (up)
                        {
                            TryViewPreviousOrNextDifference();
                        }
                        else
                        {
                            TryViewCombinedDifference();
                        }
                    }
                    break;

                case Key.C:
                    if (State.IsViewOnly == false)
                    {
                        // We only allow this shortcut if we are not in viewonly mode, as its an editing operation
                        CopyPreviousValues_Click();
                    }
                    break;

                case Key.E:
                    MenuItemEpisodeShowHide_Click(null, null);
                    break;

                case Key.Q:
                    // Toggle the QuickPaste window
                    if (quickPasteWindow == null || (quickPasteWindow.Visibility != Visibility.Visible))
                    {
                        if (State.IsViewOnly == false)
                        {
                            // We only allow this shortcut if we are not in viewonly mode, as it is editing oriented
                            QuickPasteWindowShow();
                        }
                    }
                    else
                    {
                        QuickPasteWindowHide();
                    }
                    break;

                case Key.Tab:
                    FilePlayer_Stop(); // In case the FilePlayer is going
                    if (IsFocusedControlInAMetadataEntryPanel(this))
                    {
                        // We don't want to interpret tabs on controls in MetadataEntryPanels.
                        // Yes, its a hack but I can't figure out the focus otherwise.
                        return;
                    }
                    MoveFocusToNextOrPreviousControlOrCopyPreviousButton(IsCondition.IsKeyShiftDown());
                    break;

                case Key.Home:
                case Key.End:
                    {
                        DataGridPane.IsActive = true;
                        break;
                    }

                default:
                    return;
            }
            currentKey.Handled = true;

        }

        // Returns true if the currently focused element is contained by a MetadataEntryPanel
        private static bool IsFocusedControlInAMetadataEntryPanel(TimelapseWindow mainWindow)
        {
            // Test to see if a parent of this control (generically a FrameworkElement)
            // is a MetadataEntryPanel (typical test)
            // or a Popup (rare test, to catch the case when a user tabs from a MultilineText)
            if (FocusManager.GetFocusedElement(mainWindow) is FrameworkElement focusedFrameworkElement)
            {
                DependencyObject visParent = VisualTreeHelper.GetParent(focusedFrameworkElement);
                MetadataDataEntryPanel panel = null;
                bool isPopup = false;
                while (panel == null && visParent != null)
                {
                    var logicalRoot = LogicalTreeHelper.GetParent(visParent);
                    if (logicalRoot is Popup)
                    {
                        isPopup = true;
                        break;
                    }
                    panel = visParent as MetadataDataEntryPanel;
                    visParent = VisualTreeHelper.GetParent(visParent);
                }
                return (panel != null || isPopup);
            }
            // NOt sure what it is, so just say its not in the MetadataPanel
            return false;
        }
        #endregion
    }
}
