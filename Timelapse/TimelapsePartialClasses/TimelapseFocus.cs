using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.ControlsDataEntry;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    // Setting the focus, including tabs
    public partial class TimelapseWindow
    {
        // Because of shortcut keys, we want to reset the focus when appropriate to the 
        // image control. This is done from various places.

        #region Callbacks
        // Whenever the user clicks on the image, reset the image focus to the image control 
        private void MarkableCanvas_PreviewMouseDown(object sender, MouseButtonEventArgs eventArgs)
        {
            this.FilePlayer_Stop(); // In case the FilePlayer is going
            this.TrySetKeyboardFocusToMarkableCanvas(true, eventArgs);
        }

        // When we move over the canvas and the user isn't in the midst of typing into a text field, reset the top level focus
        private void MarkableCanvas_MouseEnter(object sender, MouseEventArgs eventArgs)
        {

            IInputElement focusedElement = FocusManager.GetFocusedElement(this);
            if ((focusedElement == null) || (focusedElement is TextBox == false))
            {
                this.TrySetKeyboardFocusToMarkableCanvas(true, eventArgs);
            }
        }

        // Save/restore the focus whenever we leave / enter the controls or the file navigator
        private void FocusSaveOn_MouseLeave(object sender, MouseEventArgs e)
        {
            IInputElement focusedElement = FocusManager.GetFocusedElement(this);
            if (focusedElement == null ||
               focusedElement is Images.MarkableCanvas ||
               focusedElement is TabItem)
            {
                // We only want to save the focus on controls
                // string message = (lastControlWithFocus == null) ? "Leave: No control has focus" : "Leave: " + lastControlWithFocus.GetType().ToString();
                // Debug.Print(message);
                return;
            }
            this.lastControlWithFocus = focusedElement;
        }

        private void FocusRestoreOn_MouseEnter(object sender, MouseEventArgs e)
        {
            if (this.lastControlWithFocus != null && this.lastControlWithFocus.IsEnabled)
            {
                if (Equals(this.lastControlWithFocus, this.MarkableCanvas))
                {
                    this.MoveFocusToNextOrPreviousControlOrCopyPreviousButton(Keyboard.Modifiers == ModifierKeys.Shift);
                    this.CopyPreviousValuesSetEnableStatePreviewsAndGlowsAsNeeded();
                }
                else
                {
                    Keyboard.Focus(this.lastControlWithFocus);
                    this.CopyPreviousValuesSetEnableStatePreviewsAndGlowsAsNeeded();
                }
            }
        }
        #endregion

        #region Methods - Set or Move Keyboard Focus
        // Actually set the top level keyboard focus to the image control
        private void TrySetKeyboardFocusToMarkableCanvas(bool checkForControlFocus, InputEventArgs eventArgs)
        {
            // Ensures that a floating window does not go behind the main window 
            this.DockingManager_FloatingDataEntryWindowTopmost(true, this.DockingManager2);

            // If the text box or combobox has the focus, we usually don't want to reset the focus. 
            // However, there are a few instances (e.g., after enter has been pressed) where we no longer want it 
            // to have the focus, so we allow for that via this flag.
            if (checkForControlFocus && eventArgs is KeyEventArgs args)
            {
                // If we are in a data control, don't reset the focus.
                if (this.SendKeyToDataEntryControlOrMenu(args))
                {
                    return;
                }
            }

            // Don't raise the window just because we set the keyboard focus to it
            Keyboard.DefaultRestoreFocusMode = RestoreFocusMode.None;
            Keyboard.Focus(this.MarkableCanvas);
        }

        // Move the focus (usually because of tabbing or shift-tab)
        // It cycles between the data entry controls and the CopyPrevious button 
        private void MoveFocusToNextOrPreviousControlOrCopyPreviousButton(bool moveToPreviousControl)
        {
            // identify the currently selected control
            // if focus is currently set to the canvas this defaults to the first or last control, as appropriate
            int currentControl = moveToPreviousControl ? this.DataEntryControls.Controls.Count : -1;

            IInputElement focusedElement = FocusManager.GetFocusedElement(this);
            if (focusedElement != null)
            {
                Type type = focusedElement.GetType();
                // A hack to stop interpretting a tab beginning from a MetadatEntryControl
                // This isn't perfect as it means a tab from anything other than a DataEntry will not be interpretted
                //if (((FrameworkElement)focusedElement).Tag is MetadataDataEntryControl)
                //{
                //    return;
                //}

                // If we are moving the focus from outside to one of the controls in the data panel or the copy previous button,
                // then try to restore the focus to the last control that had the focus.
                if (Constant.Control.KeyboardInputTypes.Contains(type) == false && !Equals(focusedElement, this.CopyPreviousValuesButton))
                {
                    if (this.lastControlWithFocus != null && this.lastControlWithFocus.IsEnabled)
                    {
                        Keyboard.Focus(this.lastControlWithFocus);
                        this.CopyPreviousValuesSetEnableStatePreviewsAndGlowsAsNeeded();
                        return;
                    }
                }

                // Otherwise, try to find the control that has the current focus
                if (Constant.Control.KeyboardInputTypes.Contains(type))
                {
                    if (DataEntryHandler.TryFindFocusedControl(focusedElement, out DataEntryControl focusedControl))
                    {
                        int index = 0;
                        foreach (DataEntryControl control in this.DataEntryControls.Controls)
                        {
                            if (Object.ReferenceEquals(focusedControl, control))
                            {
                                // We found it, so no need to look further
                                currentControl = index;
                                break;
                            }
                            ++index;
                        }
                    }
                }
            }

            // Then move to the next or previous control as available
            Func<int, int> incrementOrDecrement;
            if (moveToPreviousControl)
            {
                incrementOrDecrement = index => --index;
            }
            else
            {
                incrementOrDecrement = index => ++index;
            }

            for (currentControl = incrementOrDecrement(currentControl);
                 currentControl > -1 && currentControl < this.DataEntryControls.Controls.Count;
                 currentControl = incrementOrDecrement(currentControl))
            {
                DataEntryControl control = this.DataEntryControls.Controls[currentControl];
                //Debug.Print(control.GetType().ToString() + ":" + control.Content);

                if (control.ContentReadOnly == false && control.IsContentControlEnabled && this.IsControlIncludedInTabOrder(control))
                {
                    this.lastControlWithFocus = control.Focus(this);
                    //Debug.Print($"Current focused element {focusedElement}");
                    //Debug.Print("In ReadOnly1: " + lastControlWithFocus.GetType().ToString() + " : "+  control.GetType().ToString() + ":" + control.Content);
                    // There is a bug with Avalon: when the data control pane is floating the focus does not go to it via the above call
                    // (although it does when its docked).
                    // Setting the focus to the actual content control seems to fix it.
                    //Debug.Print($"In ReadOnly2: {control.GetContentControl.GetType()} : {control.Content}" );
                    if (control is DataEntryMultiLine multiLine)
                    {
                        multiLine.GetContentControl.Focus();
                        return;
                    }
                    else if (control is DataEntryMultiChoice multiChoice)
                    {
                        multiChoice.GetContentControl.Focus();
                        return;
                    }
                    control.GetContentControl.Focus();
                    //Keyboard.Focus(control.GetContentControl);
                    return;
                }
            }

            // if we've gone thorugh all the controls and couldn't set the focus, then we must be at the beginning or at the end.
            if (this.CopyPreviousValuesButton.IsEnabled)
            {
                // So set the focus to the Copy PreviousValuesButton, unless it is disabled.
                this.CopyPreviousValuesButton.Focus();
                this.lastControlWithFocus = this.CopyPreviousValuesButton;
                this.CopyPreviousValuesSetEnableStatePreviewsAndGlowsAsNeeded();
            }
            else
            {
                // Skip the CopyPreviousValuesButton, as it is disabled.
                DataEntryControl candidateControl = moveToPreviousControl ? this.DataEntryControls.Controls.Last() : this.DataEntryControls.Controls.First();
                if (moveToPreviousControl)
                {
                    // Find the LAST control
                    foreach (DataEntryControl control in this.DataEntryControls.Controls)
                    {
                        if (control.ContentReadOnly == false)
                        {
                            candidateControl = control;
                        }
                    }
                }
                else
                {
                    // Find the FIRST control
                    foreach (DataEntryControl control in this.DataEntryControls.Controls)
                    {
                        if (control.ContentReadOnly == false)
                        {
                            candidateControl = control;
                            break;
                        }
                    }
                }
                if (candidateControl != null)
                {
                    this.lastControlWithFocus = candidateControl.Focus(this);
                }
            }
        }
        #endregion

        #region Methods -Send Key to Control or Menu
        // Return true if the current focus is in a textbox or combobox data control
        private bool SendKeyToDataEntryControlOrMenu(KeyEventArgs eventData)
        {
            // check if each menu type is open
            // it is sufficient to check one always visible item from each top level menu (file, edit, etc.)
            // NOTE: this must be kept in sync with the menu definitions in XAML
            if (this.MenuItemExit.IsVisible ||
                this.MenuItemCopyPreviousValues.IsVisible ||
                this.MenuItemViewNextImage.IsVisible ||
                this.MenuItemSelectAllFiles.IsVisible ||
                this.MenuItemAbout.IsVisible)
            {
                return true;
            }

            // by default focus will be on the MarkableCanvas
            // opening a menu doesn't change the focus
            IInputElement focusedElement = FocusManager.GetFocusedElement(this);
            if (focusedElement == null)
            {
                return false;
            }

            // check if focus is on a control
            // NOTE: this list must be kept in sync with the System.Windows classes used by the classes in Timelapse\Util\DataEntry*.cs
            Type type = focusedElement.GetType();
            if (Constant.Control.KeyboardInputTypes.Contains(type))
            {
                // send all keys to controls by default except
                // - escape as that's a natural way to back out of a control (the user can also hit enter)
                // - tab as that's the Windows keyboard navigation standard for moving between controls
                this.FilePlayer_Stop(); // In case the FilePlayer is going
                return eventData.Key != Key.Escape && eventData.Key != Key.Tab;
            }
            return false;
        }
        #endregion

        #region Helper used only here
        // Determine whether system-supplied fields should be skipped over or not.
        private bool IsControlIncludedInTabOrder(DataEntryControl control)
        {
            if (control.DataLabel == Constant.DatabaseColumn.DateTime && this.State.TabOrderIncludeDateTime == false)
            {
                return false;
            }
            else if (control.DataLabel == Constant.DatabaseColumn.DeleteFlag && this.State.TabOrderIncludeDeleteFlag == false)
            {
                return false;
            }
            return true;
        }
        #endregion
    }
}
