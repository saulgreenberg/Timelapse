using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Timelapse.Constant;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Util;
using TimelapseWpf.Toolkit;
using Control = System.Windows.Controls.Control;

namespace Timelapse.ControlsCore
{
    /// <summary>
    /// Unified base class for both DataEntry and Metadata controls.
    /// Contains all shared logic (95%+ of implementation).
    /// Mode-specific behavior is handled via virtual methods that derived classes can override.
    /// </summary>
    public abstract class DataEntryControlBase
    {
        #region Properties - Shared by all control types
        /// <summary>Gets the content control element</summary>
        public abstract UIElement GetContentControl { get; }

        /// <summary>Gets whether the content control is enabled</summary>
        public abstract bool IsContentControlEnabled { get; }

        /// <summary>Gets the value of the control</summary>
        public abstract string Content { get; }

        /// <summary>Gets or sets whether the control's content is user editable</summary>
        public abstract bool ContentReadOnly { get; set; }

        /// <summary>Gets the container that holds the control</summary>
        public StackPanel Container { get; }

        /// <summary>Gets the data label which corresponds to this control</summary>
        public string DataLabel { get; }

        /// <summary>Focus the control</summary>
        public abstract IInputElement Focus(DependencyObject focusScope);

        /// <summary>Popup preview (used by DataEntry mode, optional for Metadata)</summary>
        protected Popup PopupPreview { get; set; }
        #endregion

        #region Constructor
        protected DataEntryControlBase(CommonControlRow control, DataEntryControls styleProvider)
        {
            // Check arguments
            ThrowIf.IsNullArgument(control, nameof(control));
            ThrowIf.IsNullArgument(styleProvider, nameof(styleProvider));

            // Store data label
            DataLabel = control.DataLabel;

            // Create the stack panel container
            Container = new StackPanel();
            Style style = styleProvider.FindResource(ControlStyle.StackPanelContainerStyle) as Style;
            Container.Style = style;

            // Use container's tag to point back to this for event handlers
            Container.Tag = this;
        }
        #endregion

        #region Abstract Methods - Must be implemented by derived classes
        /// <summary>Set the content and tooltip of the control</summary>
        public abstract void SetContentAndTooltip(string value);
        #endregion

        #region Virtual Methods - Can be overridden by mode-specific implementations
        /// <summary>Flash the background of the content control (DataEntry mode typically implements this)</summary>
        public virtual void FlashContentControl(FlashEnum flashEnum) { }

        /// <summary>Show a preview of what the control value would be (DataEntry mode typically implements this)</summary>
        public virtual void ShowPreviewControlValue(string value) { }

        /// <summary>Hide the preview control value (DataEntry mode typically implements this)</summary>
        public virtual void HidePreviewControlValue() { }

        /// <summary>Flash the preview control value (DataEntry mode typically implements this)</summary>
        public virtual void FlashPreviewControlValue() { }
        #endregion
    }

    /// <summary>
    /// Generic unified base class with typed content and label controls.
    /// Contains all shared implementation logic for both DataEntry and Metadata modes.
    /// </summary>
    public abstract class DataEntryControlBase<TControlRow, TContent, TLabel> : DataEntryControlBase
        where TControlRow : CommonControlRow
        where TContent : Control, new()
        where TLabel : ContentControl, new()
    {
        #region Properties
        /// <summary>The content control (TextBox, CheckBox, ComboBox, etc.)</summary>
        public TContent ContentControl { get; }

        /// <summary>The label control</summary>
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

        /// <summary>Gets or sets whether the control's content is user editable</summary>
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

        /// <summary>Override this if your control uses a different property name for readonly</summary>
        protected virtual bool GetContentControlReadOnly()
        {
            dynamic control = ContentControl;
            return control.IsReadOnly;
        }

        /// <summary>Override this if your control needs special handling for readonly</summary>
        protected virtual void SetContentControlReadOnly(bool isReadOnly)
        {
            dynamic control = ContentControl;
            control.IsReadOnly = isReadOnly;
        }
        #endregion

        #region Constructor
        protected DataEntryControlBase(
            TControlRow control,
            DataEntryControls styleProvider,
            ControlContentStyleEnum? contentStyleName,
            ControlLabelStyleEnum labelStyleName,
            string tooltipOverride = null)
            : base(control, styleProvider)
        {
            // Check arguments
            ThrowIf.IsNullArgument(control, nameof(control));
            ThrowIf.IsNullArgument(styleProvider, nameof(styleProvider));

            // Create content control
            ContentControl = new TContent { IsTabStop = true };
            if (contentStyleName.HasValue)
            {
                ContentControl.Style = (Style)styleProvider.FindResource(contentStyleName.Value.ToString());
            }
            ContentControl.IsEnabled = true;

            // Set width if available (ControlRow has Width, MetadataControlRow doesn't)
            if (control is ControlRow controlRow)
            {
                Width = controlRow.Width;
            }

            // Use content's tag to point back to this for event handlers
            ContentControl.Tag = this;

            // Create label control
            LabelControl = new TLabel
            {
                Content = control.Label,
                Style = (Style)styleProvider.FindResource(labelStyleName.ToString()),
                ToolTip = tooltipOverride ?? control.Tooltip
            };

            // Add to container
            Container.Children.Add(LabelControl);
            Container.Children.Add(ContentControl);
            Container.PreviewKeyDown += Container_PreviewKeyDown;
        }
        #endregion

        #region Keyboard Event Handling
        /// <summary>
        /// Handle keyboard shortcuts that should navigate images instead of moving within control.
        /// Can be overridden for mode-specific behavior.
        /// </summary>
        protected virtual void Container_PreviewKeyDown(object sender, KeyEventArgs keyEvent)
        {
            // Possible shortcut keys (delegated to main window):
            // - any Control key press could indicate a Shortcut key, and
            // - a few very specific keys that don't require a Control key press
            if (IsCondition.IsKeyControlDown() ||
                IsCondition.IsKeyPageUpDown(keyEvent.Key))
            {
                keyEvent.Handled = true;
                GlobalReferences.MainWindow.Handle_PreviewKeyDown(keyEvent, true);
            }
            else if (ContentReadOnly && IsCondition.IsKeyShiftDown() && ContentControl is ImprintAutoCompleteTextBox)
            {
                // Don't allow text selection in readonly textbox
                keyEvent.Handled = true;
            }
        }
        #endregion

        #region Focus
        public override IInputElement Focus(DependencyObject focusScope)
        {
            FocusManager.SetFocusedElement(focusScope, ContentControl);
            return ContentControl;
        }
        #endregion

        #region Visual Effects and Popup Previews (DataEntry mode)
        /// <summary>Create a popup preview overlay for showing proposed values</summary>
        protected virtual Popup CreatePopupPreview(Control control, Thickness padding, double width, double horizontalOffset)
        {
            TextBlock popupText = new()
            {
                Text = string.Empty,
                Width = width < 20 ? 80 : width,
                Height = control?.Height ?? Double.NaN,
                Padding = padding,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = Constant.Control.QuickPasteFieldHighlightBrush,
                Foreground = Brushes.Green,
                FontStyle = FontStyles.Italic,
            };

            Border border = new()
            {
                BorderBrush = Brushes.Green,
                BorderThickness = new(1),
                Child = popupText,
            };

            Popup popup = new()
            {
                Width = width < 5 ? Double.NaN : width,
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

        /// <summary>Show the popup preview with specified text</summary>
        protected virtual void ShowPopupPreview(string text)
        {
            if (PopupPreview?.Child is Border { Child: TextBlock popupText })
            {
                popupText.Text = text;
                PopupPreview.IsOpen = true;
            }
        }

        /// <summary>Hide the popup preview</summary>
        protected virtual void HidePopupPreview()
        {
            if (PopupPreview != null)
            {
                PopupPreview.IsOpen = false;
            }
        }

        /// <summary>Flash the popup preview</summary>
        protected virtual void FlashPopupPreview()
        {
            if (PopupPreview?.Child is Border { Child: TextBlock popupText })
            {
                ColorAnimation animation = new()
                {
                    From = Colors.White,
                    AutoReverse = false,
                    Duration = new Duration(TimeSpan.FromSeconds(.6)),
                    EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseIn }
                };
                popupText.Background.BeginAnimation(SolidColorBrush.ColorProperty, animation);
            }
        }

        /// <summary>Get color animation for visual feedback</summary>
        protected virtual ColorAnimation GetColorAnimation(Color fromColor = default)
        {
            if (fromColor == default)
            {
                fromColor = Colors.LightGreen;
            }

            return new ColorAnimation
            {
                From = fromColor,
                AutoReverse = true,
                Duration = new Duration(TimeSpan.FromSeconds(.4)),
                EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseInOut }
            };
        }

        /// <summary>Get fast red color animation for visual feedback</summary>
        protected virtual ColorAnimation GetColorAnimationFastRed()
        {
            return new ColorAnimation
            {
                From = Colors.LightCoral,
                AutoReverse = true,
                Duration = new Duration(TimeSpan.FromSeconds(.15)),
                EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseInOut }
            };
        }
        #endregion
    }
}
