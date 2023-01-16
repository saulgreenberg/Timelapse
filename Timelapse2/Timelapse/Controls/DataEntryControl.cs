using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.Enums;
using Timelapse.Util;
using Xceed.Wpf.Toolkit;

namespace Timelapse.Controls
{
    /// Two abstact classes are defined in this file:
    /// - DataEntryControl defines the base aspects of the control portion of a data entry control
    /// - DataEntryControl<TContent, TLabel> defines the label and actual control presented on the display</TContent>

    /// <summary>
    /// Abstract class that defines the base aspects of a data entry control
    /// </summary>
    public abstract class DataEntryControl
    {
        #region DataEntryControl Properties
        /// <summary>Gets the position of the content control</summary>
        public abstract Point TopLeft { get; }

        /// <summary>Gets the position of the content control</summary>
        public abstract UIElement GetContentControl { get; }

        public abstract bool IsContentControlEnabled { get; }

        /// <summary>Gets the value of the control</summary>
        public abstract string Content { get; }

        /// <summary>Gets or sets a value indicating whether the control's content is user editable</summary>
        public abstract bool ContentReadOnly { get; set; }

        /// <summary>Gets or sets a value indicating whether the control's contents are copyable.</summary>
        public bool Copyable { get; set; }

        /// <summary>Gets the container that holds the control.</summary>
        public StackPanel Container { get; private set; }

        /// <summary>Gets the data label which corresponds to this control.</summary>
        public string DataLabel { get; private set; }

        public abstract IInputElement Focus(DependencyObject focusScope);

        // used to remember and restore state when
        // displayTemporaryContents and RestoreTemporaryContents are used
        protected Popup PopupPreview { get; set; }
        #endregion

        #region Base constructor for all data entry controls
        protected DataEntryControl(ControlRow control, DataEntryControls styleProvider)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(control, nameof(control));
            ThrowIf.IsNullArgument(styleProvider, nameof(styleProvider));

            // populate properties from database definition of control
            // this.Content and Tooltip can't be set, however, as the caller hasn't instantiated the content control yet
            this.Copyable = control.Copyable;
            this.DataLabel = control.DataLabel;

            // Create the stack panel
            this.Container = new StackPanel();
            Style style = styleProvider.FindResource(Constant.ControlStyle.ContainerStyle) as Style;
            this.Container.Style = style;

            // use the containers's tag to point back to this so event handlers can access the DataEntryControl
            // this is needed by callbacks such as DataEntryHandler.Container_PreviewMouseRightButtonDown() and TimelapseWindow.CounterControl_MouseLeave()
            this.Container.Tag = this;
        }
        #endregion

        #region Abstract methods
        public abstract void SetContentAndTooltip(string value);

        // Flash the background of the content control area
        public abstract void FlashContentControl();

        // These methods allow us to temporarily display an arbitrary string value into the data field
        // This should alwasy be followed by restoring the original contents.
        // An example of its use is to show the user what will be placed in the data control if the user continues their action
        // e.g., moving the mouse over a quickpaste or copyprevious buttons will display potential values,
        //       while moving the mouse out of those buttons will restore those values.
        public abstract void ShowPreviewControlValue(string value);
        public abstract void HidePreviewControlValue();
        public abstract void FlashPreviewControlValue();
        #endregion
    }

    //    [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:FileMayOnlyContainASingleClass", Justification = "StyleCop limitation.")]
    /// <summary> A generic control comprises a stack panel containing 
    /// - a control containing at least a descriptive label 
    /// - another control for displaying / entering data at a given width
    /// </summary>
    public abstract class DataEntryControl<TContent, TLabel> : DataEntryControl
        where TContent : Control, new()
        where TLabel : ContentControl, new()
    {
        #region DataEntryControl<TContent, TLabel> Properties
        public TContent ContentControl { get; private set; }

        /// <summary>Gets the control label's value</summary>
        public string Label => (string)this.LabelControl.Content;

        public TLabel LabelControl { get; private set; }

        /// <summary>Gets or sets the width of the content control</summary>
        public int Width
        {
            get => (int)this.ContentControl.Width;
            set => this.ContentControl.Width = value;
        }

        // Sets or gets whether this control is enabled or disabled</summary>
        public bool IsEnabled
        {
            get => this.Container.IsEnabled;
            set
            {
                this.ContentControl.IsEnabled = value;
                this.LabelControl.IsEnabled = value;
                this.Container.IsEnabled = value;
                this.ContentControl.Foreground = value ? Brushes.Black : Brushes.DimGray;
            }
        }
        #endregion

        #region Base constructor for a DataEntryControl<,>
        protected DataEntryControl(ControlRow control, DataEntryControls styleProvider, ControlContentStyleEnum? contentStyleName, ControlLabelStyleEnum labelStyleName) :
            base(control, styleProvider)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(control, nameof(control));
            ThrowIf.IsNullArgument(styleProvider, nameof(styleProvider));

            this.ContentControl = new TContent()
            {
                IsTabStop = true
            };
            if (contentStyleName.HasValue)
            {
                this.ContentControl.Style = (Style)styleProvider.FindResource(contentStyleName.Value.ToString());
            }
            this.ContentReadOnly = false;
            this.ContentControl.IsEnabled = true;
            this.Width = control.Width;

            // use the content's tag to point back to this so event handlers can access the DataEntryControl as well as just ContentControl
            // the data update callback for each control type in TimelapseWindow, such as NoteControl_TextChanged(), relies on this
            this.ContentControl.Tag = this;

            // Create the label (which is an actual label)
            this.LabelControl = new TLabel()
            {
                Content = control.Label,
                Style = (Style)styleProvider.FindResource(labelStyleName.ToString()),
                ToolTip = control.Tooltip
            };

            // add the label and content to the stack panel
            this.Container.Children.Add(this.LabelControl);
            this.Container.Children.Add(this.ContentControl);
            this.Container.PreviewKeyDown += this.Container_PreviewKeyDown;
        }
        #endregion

        #region PreviewKeyDown and Focus
        // We want to capture the Shift/Arrow key presses so we can navigate images. However, both the UTCOffset and the DateTime picker consume 
        // those PreviewKeyDown event. As a workaround, we attach a preview keydown to the container and take action on that.
        private void Container_PreviewKeyDown(object sender, KeyEventArgs keyEvent)
        {
            // We are only interested in interpretting the Shift/Arrow key for the following controls
            // The DataEntryChoice and DataEntryFlags do their own previewKeyDown processing to acheive a similar effect
            if (this.ContentControl is DateTimePicker ||
                this.ContentControl is IntegerUpDown ||
                this.ContentControl is AutocompleteTextBox)
            {
                // Use the SHIFT right/left pageUp/PageDownkey to go to the next/previous image for datetimepicker
                // The right/left arrow keys normally navigate through text characters when the text is enabled.
                // which restricts the use the arrow keys to cycle through images.
                // As a work-around, we use the arrow keys for cycling through the image when:
                // - it is a read-only note (as we don't have to navigate text)
                // - the SHIFT key is held down when its not a read only note
                // Note that redirecting the event to the main window, while prefered, won't work
                // as the main window ignores the arrow keys if the focus is set to a control.
                if ((this.ContentReadOnly || Keyboard.Modifiers == ModifierKeys.Shift) && (keyEvent.Key == Key.Right || keyEvent.Key == Key.Left || keyEvent.Key == Key.PageUp || keyEvent.Key == Key.PageDown))
                {
                    keyEvent.Handled = true;
                    GlobalReferences.MainWindow.Handle_PreviewKeyDown(keyEvent, true);
                }
            }
        }

        public override IInputElement Focus(DependencyObject focusScope)
        {
            // request the focus manager figure out how to assign focus within the edit control as not all controls are focusable at their top level
            // This is not reliable at small focus scopes, possibly due to interaction with TimelapseWindow's focus management, but seems reasonably
            // well behaved at application scope.
            FocusManager.SetFocusedElement(focusScope, this.ContentControl);
            return this.ContentControl;
        }
        #endregion

        #region Visual Effects and Popup Previews
        protected virtual Popup CreatePopupPreview(Control control, Thickness padding, double width, double horizontalOffset)
        {

            // Create a textblock and align it so the text is exactly at the same position as the control's text
            // Note that the null check for control (which should not happen) gives it an auto value 'just in case'
            TextBlock popupText = new TextBlock
            {
                Text = String.Empty,
                Width = width < 20 ? 80 : width,
                Height = control?.Height ?? Double.NaN,
                Padding = padding,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = Constant.Control.QuickPasteFieldHighlightBrush,
                Foreground = Brushes.Green,
                FontStyle = FontStyles.Italic,
            };

            Border border = new Border
            {
                BorderBrush = Brushes.Green,
                BorderThickness = new Thickness(1),
                Child = popupText,
            };

            Popup popup = new Popup
            {
                Width = width,
                Height = control?.Height ?? Double.NaN,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Placement = PlacementMode.Center,
                VerticalOffset = 0,
                HorizontalOffset = horizontalOffset,
                PlacementTarget = control,
                IsOpen = false,
                Child = border
            };
            return popup;
        }

        protected virtual void ShowPopupPreview(string value)
        {
            Border border = (Border)this.PopupPreview.Child;
            TextBlock popupText = (TextBlock)border.Child;
            popupText.Text = value;
            this.PopupPreview.IsOpen = true;
        }

        protected void HidePopupPreview()
        {
            if (this.PopupPreview == null || this.PopupPreview.Child == null)
            {
                // There is no popupPreview being displayed, so there is nothing to hide.
                return;
            }
            Border border = (Border)this.PopupPreview.Child;
            TextBlock popupText = (TextBlock)border.Child;
            popupText.Text = String.Empty;
            this.PopupPreview.IsOpen = false;
        }

        // Create a flash effect for the popup. We use this to signal that the 
        // preview text has been selected
        protected virtual void FlashPopupPreview()
        {
            if (this.PopupPreview == null || this.PopupPreview.Child == null)
            {
                return;
            }

            // Get the TextBlock
            Border border = (Border)this.PopupPreview.Child;
            TextBlock popupText = (TextBlock)border.Child;

            // Revert to normal fontstyle, and set up a
            // timer to change it back to italics after a short duration
            popupText.FontStyle = FontStyles.Normal;
            DispatcherTimer timer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromSeconds(.4),
                Tag = popupText,
            };
            timer.Tick += this.FlashFontTimer_Tick;

            // Animate the color from white back to its current color
            ColorAnimation animation = new ColorAnimation()
            {
                From = Colors.White,
                AutoReverse = false,
                Duration = new Duration(TimeSpan.FromSeconds(.6)),
                EasingFunction = new ExponentialEase()
                {
                    EasingMode = EasingMode.EaseIn
                },
            };

            // Get it all going
            popupText.Background.BeginAnimation(SolidColorBrush.ColorProperty, animation);
            timer.Start();
        }
        private void FlashFontTimer_Tick(object sender, EventArgs e)
        {
            DispatcherTimer timer = sender as DispatcherTimer;
            ((TextBlock)timer.Tag).FontStyle = FontStyles.Italic;
            timer.Stop();
        }

        // This is a standard color animation scheme that can be accessed by the other controls
        protected ColorAnimation GetColorAnimation()
        {
            return new ColorAnimation()
            {
                From = Colors.LightGreen,
                AutoReverse = false,
                Duration = new Duration(TimeSpan.FromSeconds(.6)),
                EasingFunction = new ExponentialEase()
                {
                    EasingMode = EasingMode.EaseIn
                },
            };
        }
        #endregion
    }
}