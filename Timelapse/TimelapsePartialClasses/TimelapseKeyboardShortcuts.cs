using System;
using System.Media;
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
                switch (currentKey.Key)
                {
                    case Key.Home:
                        ImageSetPane.IsEnabled = true;
                        ImageSetPane.IsSelected = true;
                        break;
                    case Key.End:
                        DataGridPane.IsEnabled = true;
                        DataGridPane.IsSelected = true;
                        // SAULXXX: If its floating, we should really be making it topmost
                        // To do that, we would have to iterate through the floating windows and set it.
                        // if (this.DataGridPane.IsFloating)
                        // {

                        // }
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

            // First, try to interpret key as a possible valid quickpaste shortcut key. 
            // If so, send it to the Quickpaste window and mark the event as handled.
            if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) &&
                 ((currentKey.Key >= Key.D0 && currentKey.Key <= Key.D9) || (currentKey.Key >= Key.NumPad0 && currentKey.Key <= Key.NumPad9)))
            {
                if (quickPasteWindow != null && quickPasteWindow.Visibility == Visibility.Visible)
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
            // to the controls within it. That is, if a textbox or combo box has the focus, then take no as this is normal text input
            // and NOT a shortcut key.  Similarly, if a menu is displayed keys should be directed to the menu rather than interpreted as
            // shortcuts.
            if (forceSendToMainWindow == false && SendKeyToDataEntryControlOrMenu(currentKey))
            {
                return;
            }

            // Finally, test for other shortcut keys and take the appropriate action as needed
            DirectionEnum direction;
            int keyRepeatCount = State.GetKeyRepeatCount(currentKey);
            switch (currentKey.Key)
            {
                case Key.S:
                    if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                    {
                        if (!currentKey.IsRepeat)
                        {
                            // Open the custom select dialog. 
                            // TODO not sure if this allows the custom select dialog to be opened when it shouldn't be...
                            // should add further checks perhaps?
                            this.MenuItemSelectCustomSelection_Click(null, null);
                        }
                    }
                    break;
                case Key.B:                 // Save a Bookmark of the current pan / zoom level of the image
                        MarkableCanvas.SetBookmark();
                    break;

                case Key.Escape:
                    TrySetKeyboardFocusToMarkableCanvas(false, currentKey);
                    break;
                case Key.OemPlus:           // Restore the zoom level / pan coordinates of the bookmark
                    MarkableCanvas.ApplyBookmark();
                    break;
                case Key.OemMinus:          // Restore the zoom level / pan coordinates of the bookmark
                    MarkableCanvas.ZoomOutAllTheWay();
                    break;
                case Key.K:                 // Dogear the current image or switch between the dogear and the last seen image
                    if (this.MarkableCanvas.IsThumbnailGridVisible)
                    {
                        return;
                    }
                    
                    if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                    {
                        // ctl-K: Show the image at the bookmark index
                        this.ImageDogear?.TrySetDogearToCurrentImage();
                    }
                    else
                    {
                        // K: Toggle to/from the bookmark
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
                case Key.M:                 // Toggle the magnifying glass on and off
                    MenuItemDisplayMagnifyingGlass_Click(this, null);
                    break;
                case Key.U:                 // Increase the magnifing glass zoom level
                    FilePlayer_Stop();      // In case the FilePlayer is going
                    MarkableCanvas.MagnifierOrOffsetChangeZoomLevel(ZoomDirection.ZoomIn);
                    break;
                case Key.D:                 // Decrease the magnifing glass zoom level
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
                case Key.Right:             // next /previous image
                case Key.Left:              // previous image
                    int increment = 1;
                    FilePlayer_Stop();      // In case the FilePlayer is going
                    direction = currentKey.Key == Key.Right ? DirectionEnum.Next : DirectionEnum.Previous;
                    if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.LeftCtrl))
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
                    if (currentKey.IsRepeat == false || (currentKey.IsRepeat && keyRepeatCount % State.Throttles.RepeatedKeyAcceptanceInterval == 0))
                    {
                        TryFileShowWithoutSliderCallback(direction, increment);
                    }
                    break;
                case Key.Up:                // show visual difference to next image
                    if (IsDisplayingMultipleImagesInOverview())
                    {
                        FilePlayer.Direction = DirectionEnum.Previous;
                        FilePlayer_ScrollRow();
                    }
                    else
                    {
                        FilePlayer_Stop(); // In case the FilePlayer is going
                        TryViewPreviousOrNextDifference();
                    }
                    break;
                case Key.Down:              // show visual difference to previous image
                    if (IsDisplayingMultipleImagesInOverview())
                    {
                        FilePlayer.Direction = DirectionEnum.Next;
                        FilePlayer_ScrollRow();
                    }
                    else
                    {
                        FilePlayer_Stop(); // In case the FilePlayer is going
                        TryViewCombinedDifference();
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
                    MoveFocusToNextOrPreviousControlOrCopyPreviousButton(Keyboard.Modifiers == ModifierKeys.Shift);
                    break;
                case Key.PageDown:
                case Key.PageUp:
                    direction = currentKey.Key == Key.PageDown ? DirectionEnum.Next : DirectionEnum.Previous;
                    if (IsDisplayingMultipleImagesInOverview())
                    {
                        FilePlayer.Direction = direction;
                        FilePlayer_ScrollPage();
                    }
                    else
                    {
                        FilePlayer_Stop();      // In case the FilePlayer is going
                        if (currentKey.IsRepeat == false || (currentKey.IsRepeat && keyRepeatCount % State.Throttles.RepeatedKeyAcceptanceInterval == 0))
                        {
                            TryFileShowWithoutSliderCallback(direction);
                        }
                    }
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
            // or a Popup (rare test, to catch the case when a user tabs from a MultilineTextEditor)
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
