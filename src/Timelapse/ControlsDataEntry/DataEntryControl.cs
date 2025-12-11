using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Timelapse.ControlsCore;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;
using TimelapseWpf.Toolkit;
using Control = System.Windows.Controls.Control;

namespace Timelapse.ControlsDataEntry
{
    // Two abstract classes are defined in this file:
    // - DataEntryControl defines DataEntry-specific aspects (TopLeft, Copyable, preview methods)
    // - DataEntryControl<TContent, TLabel> defines the typed label and control
    //
    // Shared logic is in DataEntryControlBase (ControlsCore namespace)

    /// <summary>
    /// DataEntry-specific base class.
    /// Extends DataEntryControlBase with DataEntry-only features:
    /// - TopLeft property (screen position)
    /// - Copyable property
    /// - Abstract preview methods (required for all DataEntry controls)
    /// </summary>
    public abstract class DataEntryControl : DataEntryControlBase
    {
        #region DataEntry-Specific Properties
        /// <summary>Gets the screen position of the content control (DataEntry mode only)</summary>
        public abstract Point TopLeft { get; }

        /// <summary>Gets or sets whether the control's contents are copyable (DataEntry mode only)</summary>
        public bool Copyable { get; set; }
        #endregion

        #region Constructor
        protected DataEntryControl(ControlRow control, DataEntryControls styleProvider)
            : base(control, styleProvider)
        {
            // Check the arguments for null
            ThrowIf.IsNullArgument(control, nameof(control));

            // Set DataEntry-specific property
            Copyable = control.Copyable;
        }
        #endregion

        #region Abstract Methods - Required for DataEntry Mode
        // DataEntry mode requires these preview methods for quick paste and copy previous features
        // Use 'new' keyword because base class has virtual versions
        public new abstract void FlashContentControl(FlashEnum flashEnum);
        public new abstract void ShowPreviewControlValue(string value);
        public new abstract void HidePreviewControlValue();
        public new abstract void FlashPreviewControlValue();
        #endregion
    }

    /// <summary>
    /// Generic DataEntry control with typed content and label.
    /// Contains DataEntry-specific implementation details.
    /// </summary>
    public abstract class DataEntryControl<TContent, TLabel> : DataEntryControl
        where TContent : Control, new()
        where TLabel : ContentControl, new()
    {
        #region Properties
        // These properties are needed for concrete typed access, even though similar properties exist in base
        public TContent ContentControl { get; }
        public TLabel LabelControl { get; }

        /// <summary>Gets the control label's value</summary>
        public string Label => (string)LabelControl.Content;

        /// <summary>Gets or sets the width of the content control</summary>
        public int Width
        {
            get => (int)ContentControl.Width;
            set => ContentControl.Width = value;
        }

        /// <summary>Gets or sets whether this control is enabled or disabled</summary>
        public bool IsEnabled
        {
            get => Container.IsEnabled;
            set
            {
                ContentControl.IsEnabled = value;
                LabelControl.IsEnabled = value;
                Container.IsEnabled = value;
                ContentControl.Foreground = value ? Brushes.Black : Brushes.DimGray;
            }
        }

        /// <summary>Gets or sets a value indicating whether the control's content is user editable</summary>
        public override bool ContentReadOnly
        {
            get => GetContentControlReadOnly();
            set
            {
                if (GlobalReferences.TimelapseState.IsViewOnly)
                {
                    SetContentControlReadOnly(true);
                    ContentControl.IsHitTestVisible = false;
                }
                else
                {
                    SetContentControlReadOnly(value);
                }
            }
        }

        /// <summary>
        /// Gets the readonly state of the content control.
        /// Override this if your control uses a different property name.
        /// </summary>
        protected virtual bool GetContentControlReadOnly()
        {
            // Most controls have an IsReadOnly property
            dynamic control = ContentControl;
            return control.IsReadOnly;
        }

        /// <summary>
        /// Sets the readonly state of the content control.
        /// Override this if your control uses a different property name or needs special handling.
        /// </summary>
        protected virtual void SetContentControlReadOnly(bool isReadOnly)
        {
            // Most controls have an IsReadOnly property
            dynamic control = ContentControl;
            control.IsReadOnly = isReadOnly;
        }
        #endregion

        #region Constructor
        protected DataEntryControl(ControlRow control, DataEntryControls styleProvider, ControlContentStyleEnum? contentStyleName, ControlLabelStyleEnum labelStyleName) :
            base(control, styleProvider)
        {
            // Check the arguments for null
            ThrowIf.IsNullArgument(control, nameof(control));
            ThrowIf.IsNullArgument(styleProvider, nameof(styleProvider));

            // Create content control
            ContentControl = new()
            {
                IsTabStop = true
            };
            if (contentStyleName.HasValue)
            {
                ContentControl.Style = (Style)styleProvider.FindResource(contentStyleName.Value.ToString());
            }
            ContentControl.IsEnabled = true;
            Width = control.Width;

            // use the content's tag to point back to this so event handlers can access the DataEntryControl as well as just ContentControl
            // the data update callback for each control type in TimelapseWindow, such as NoteControl_TextChanged(), relies on this
            ContentControl.Tag = this;

            // Create the label (which is an actual label)
            LabelControl = new()
            {
                Content = control.Label,
                Style = (Style)styleProvider.FindResource(labelStyleName.ToString()),
                ToolTip = control.Tooltip
            };

            // add the label and content to the stack panel
            Container.Children.Add(LabelControl);
            Container.Children.Add(ContentControl);
            Container.PreviewKeyDown += Container_PreviewKeyDown;
        }
        #endregion

        #region PreviewKeyDown and Focus
        /// <summary>
        /// Determines if the base class should handle keyboard navigation shortcuts.
        /// Override and return false in controls that implement their own keyboard handling.
        /// </summary>
        protected virtual bool HandleKeyboardNavigationInBase()
        {
            return true; // Default: base class handles navigation
        }

        // DataEntry-specific keyboard handling
        // Generally, when the Ctl is pressed, interpret these keys as shortcut keys.
        // This is true for all DataEntry controls, excepting numeric controls and WatermarkCheckComboBox which do their own special handling.
        protected void Container_PreviewKeyDown(object sender, KeyEventArgs keyEvent)
        {
            if (!HandleKeyboardNavigationInBase())
            {
                return;
            }

            // Possible shortcut keys (delegated to main window):
            // - any Control key press could indicate a Shortcut key, and
            // - a few very specific keys that don't require a Control key press
            if (IsCondition.IsKeyControlDown() ||
                IsCondition.IsKeyPageUpDown(keyEvent.Key))
            {
                DelegateKeyEventToMainWindow(keyEvent, true);
            }
        }

        // Set whether the event is handed, and then send the key to the main window
        protected void DelegateKeyEventToMainWindow(KeyEventArgs keyEvent, bool handled)
        {
            keyEvent.Handled = handled;
            GlobalReferences.MainWindow.Handle_PreviewKeyDown(keyEvent, true);
        }

        public override IInputElement Focus(DependencyObject focusScope)
        {
            // request the focus manager figure out how to assign focus within the edit control as not all controls are focusable at their top level
            // This is not reliable at small focus scopes, possibly due to interaction with TimelapseWindow's focus management, but seems reasonably
            // well behaved at application scope.
            FocusManager.SetFocusedElement(focusScope, ContentControl);
            return ContentControl;
        }
        #endregion

        #region Visual Effects and Popup Previews
        protected virtual Popup CreatePopupPreview(Control control, Thickness padding, double width, double horizontalOffset)
        {
            // Create a textblock and align it so the text is exactly at the same position as the control's text
            TextBlock popupText = new()
            {
                Text = string.Empty,
                Width = width < 20 ? 80 : width,
                Height = control?.Height ?? double.NaN,
                Padding = padding,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Left,

                Background = Constant.Control.QuickPasteFieldHighlightBrush,
                Foreground = Brushes.Green,
                FontStyle = FontStyles.Italic,

                // Numeric controls are left-aligned, all others are right-aligned
                TextAlignment = control is IntegerUpDown or DoubleUpDown
                    ? TextAlignment.Right
                    : TextAlignment.Left,
            };

            Border border = new()
            {
                BorderBrush = Brushes.Green,
                BorderThickness = new(1),
                Child = popupText,
            };

            Popup popup = new()
            {
                Width = width < 5 ? double.NaN : width,
                Height = control?.Height ?? double.NaN,
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
            Border border = (Border)PopupPreview.Child;
            TextBlock popupText = (TextBlock)border.Child;
            popupText.Text = value;
            PopupPreview.IsOpen = true;
        }

        protected void HidePopupPreview()
        {
            if (PopupPreview == null || PopupPreview.Child == null)
            {
                // There is no popupPreview being displayed, so there is nothing to hide.
                return;
            }
            Border border = (Border)PopupPreview.Child;
            TextBlock popupText = (TextBlock)border.Child;
            popupText.Text = string.Empty;
            PopupPreview.IsOpen = false;
        }

        // Create a flash effect for the popup. We use this to signal that the
        // preview text has been selected
        protected virtual void FlashPopupPreview()
        {
            if (PopupPreview == null || PopupPreview.Child == null)
            {
                return;
            }

            // Get the TextBlock
            Border border = (Border)PopupPreview.Child;
            TextBlock popupText = (TextBlock)border.Child;

            // Revert to normal fontstyle, and set up a
            // timer to change it back to italics after a short duration
            popupText.FontStyle = FontStyles.Normal;
            DispatcherTimer timer = new()
            {
                Interval = TimeSpan.FromSeconds(.4),
                Tag = popupText,
            };
            timer.Tick += FlashFontTimer_Tick;

            // Animate the color from white back to its current color
            ColorAnimation animation = new()
            {
                From = Colors.White,
                AutoReverse = false,
                Duration = new(TimeSpan.FromSeconds(.6)),
                EasingFunction = new ExponentialEase
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
            if (sender is not DispatcherTimer timer)
            {
                return;
            }
            ((TextBlock)timer.Tag).FontStyle = FontStyles.Italic;
            timer.Stop();
        }

        // This is a standard color animation scheme that can be accessed by the other controls
        protected ColorAnimation GetColorAnimationForPasting()
        {
            return new()
            {
                From = Colors.LightGreen,
                AutoReverse = false,
                Duration = new(TimeSpan.FromSeconds(.6)),
                EasingFunction = new ExponentialEase
                {
                    EasingMode = EasingMode.EaseIn
                },
            };
        }

        protected ColorAnimation GetColorAnimationWarning()
        {
            return GetColorAnimation(Colors.LightCoral, new(TimeSpan.FromSeconds(.1)));
        }

        protected ColorAnimation GetColorAnimation(Color color, Duration duration)
        {
            return new()
            {
                From = color,
                AutoReverse = false,
                Duration = duration,
                EasingFunction = new ExponentialEase
                {
                    EasingMode = EasingMode.EaseIn
                },
            };
        }
        #endregion
    }
}
