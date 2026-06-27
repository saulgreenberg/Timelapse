using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Timelapse.Constant;
using Timelapse.ControlsCore;
using Timelapse.DataTables;
using Timelapse.Enums;
using Control = System.Windows.Controls.Control;

namespace Timelapse.ControlsDataEntry
{
    // A flag comprises a stack panel containing
    // - a label containing the descriptive label)
    // - checkbox (the content) at the given width
    public class DataEntryFlag : DataEntryControl<CheckBox, Label>
    {
        private readonly FlagControlCore core;

        #region Public Properties
        // Return the TopLeft corner of the content control as a point
        public override Point TopLeft => ContentControl.PointToScreen(new(0, 0));

        public override UIElement GetContentControl => ContentControl;

        public override bool IsContentControlEnabled => ContentControl.IsEnabled;

        /// <summary>Gets or sets the Content of the Flag</summary>
        public override string Content => core.GetContent();

        protected override bool GetContentControlReadOnly() => core.ContentReadOnly;

        protected override void SetContentControlReadOnly(bool isReadOnly) => core.ContentReadOnly = isReadOnly;
        #endregion

        #region Constructor
        public DataEntryFlag(ControlRow control, DataEntryControls styleProvider)
            : base(control, styleProvider, ControlContentStyleEnum.FlagCheckBox, ControlLabelStyleEnum.DefaultLabel)
        {
            // Create core shared implementation
            core = new FlagControlCore(ContentControl);

            // Callback used to allow Enter to select the highlit item
            ContentControl.PreviewKeyDown += ContentControl_PreviewKeyDown;
        }
        #endregion

        #region Event Handlers
        // Note: DataEntryControlContainer_PreviewKeyDown will handle all the basic shortcut keys eg, Ctl-left,right,up,down etc

        // Delegate to core for other basic key handling, e.g. to ignore non-ctl arrow keys which would otherwise tab
        private void ContentControl_PreviewKeyDown(object sender, KeyEventArgs keyEvent)
        {
            core.HandleNavigationKeys(keyEvent, true);
        }
        #endregion

        #region Setting Content and Tooltip
        public override void SetContentAndTooltip(string value)
        {
            // If the value is null, an ellipsis will be drawn in the checkbox (see Checkbox style)
            // Used to signify the indeterminate state in no or multiple selections in the overview.
            if (value == null)
            {
                ContentControl.IsChecked = null;
                ContentControl.ToolTip = "Click to change the " + Label + " for all selected images";
                return;
            }

            // Otherwise, the checkbox will be checked depending on whether the value is true or false,
            // and the tooltip will be set to true or false. 
            value = value.ToLower();
            ContentControl.IsChecked = (value == BooleanValue.True);
            ContentControl.ToolTip = LabelControl.ToolTip;
        }
        #endregion

        #region Visual Effects and Popup Previews
        // Flash the content area of the control
        private Border MainDisplayField;
        public override void FlashContentControl(FlashEnum flashEnum)
        {
            MainDisplayField ??= (Border)ContentControl.Template.FindName("checkBoxBorder", ContentControl);
            if (MainDisplayField == null) return;
            MainDisplayField.Background = new SolidColorBrush(Colors.White);
            MainDisplayField.Background.BeginAnimation(SolidColorBrush.ColorProperty, flashEnum == FlashEnum.UsePasteFlash
                ? GetColorAnimationForPasting()
                : GetColorAnimationWarning());
        }

        protected override Popup CreatePopupPreview(Control control, Thickness padding, double width, double horizontalOffset)
        {
            Style style = (Style)ContentControl.FindResource(nameof(ControlContentStyleEnum.FlagCheckBox));

            // Creatre a textblock and align it so the text is exactly at the same position as the control's text
            // Note that if control is null (which shouldn't happen) we use an autoheight
            CheckBox popupText = new()
            {
                Width = width < 20 ? 20 : width,
                Height = control?.Height ?? Double.NaN,
                Padding = padding,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = Constant.Control.QuickPasteFieldHighlightBrush,
                Foreground = Brushes.Green,
                FontStyle = FontStyles.Italic,
                Style = style
            };

            Border border = new()
            {
                BorderBrush = Brushes.Green,
                BorderThickness = new(0),
                Child = popupText,
                Width = 17,
                Height = 17,
                CornerRadius = new(2),
            };

            Popup popup = new()
            {
                Width = width,
                Height = control?.Height ?? Double.NaN,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Left,
                Placement = PlacementMode.Center,
                VerticalOffset = 0,
                HorizontalOffset = horizontalOffset,
                PlacementTarget = control,
                IsOpen = false,
                Child = border,
                AllowsTransparency = true,
                Opacity = 0
            };
            return popup;
        }
        public override void ShowPreviewControlValue(string value)
        {
            value ??= string.Empty;

            // Create the popup overlay
            if (PopupPreview == null)
            {
                // We want to shrink the width a bit, as its otherwise a bit wide
                double widthCorrection = 0;
                double width = ContentControl.Width - widthCorrection;
                double horizontalOffset = 0;

                // Padding is used to align the text so it begins at the same spot as the control's text
                Thickness padding = new(0, 0, 0, 0);

                PopupPreview = CreatePopupPreview(ContentControl, padding, width, horizontalOffset);
            }
            // Convert the true/false to a checkmark or none, then show the Popup
            bool check = value.ToLower() == BooleanValue.True;
            ShowPopupPreview(check);
        }
        protected void ShowPopupPreview(bool value)
        {
            Border border = (Border)PopupPreview.Child;
            CheckBox popupText = (CheckBox)border.Child;
            popupText.IsChecked = value;
            PopupPreview.IsOpen = true;
            Border cbborder = (Border)popupText.Template.FindName("checkBoxBorder", popupText);
            if (cbborder != null)
            {
                cbborder.Background = Constant.Control.QuickPasteFieldHighlightBrush;
            }
        }

        public override void HidePreviewControlValue()
        {
            if (PopupPreview == null || PopupPreview.Child == null)
            {
                // There is no popupPreview being displayed, so there is nothing to hide.
                return;
            }
            Border border = (Border)PopupPreview.Child;
            CheckBox popupText = (CheckBox)border.Child;
            popupText.IsChecked = false;
            PopupPreview.IsOpen = false;
        }

        public override void FlashPreviewControlValue()
        {
            FlashPopupPreview();
        }

        protected override void FlashPopupPreview()
        {
            if (PopupPreview == null || PopupPreview.Child == null)
            {
                return;
            }

            // Get the TextBlock
            Border border = (Border)PopupPreview.Child;
            CheckBox popupText = (CheckBox)border.Child;

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
        }
        #endregion
    }
}
