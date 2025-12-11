using System;
using System.Windows;
using System.Windows.Controls;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;

namespace TimelapseTemplateEditor.Dialog
{
    public partial class EditTooltipText
    {
        private readonly UIElement PositionReference;
        public string TooltipText { get; private set; }

        private readonly string initialTooltipText;
        /// <summary>
        /// Creates a new EditTooltip dialog for editing tooltip text.
        /// </summary>
        /// <param name="positionReference">The UI element (button) to position the dialog relative to</param>
        /// <param name="tooltipText">The current tooltip to edit</param>
        /// <param name="owner">The owner window</param>
        public EditTooltipText(UIElement positionReference, string tooltipText, Window owner)
        {
            InitializeComponent();
            Owner = owner;
            PositionReference = positionReference;
            TooltipText = tooltipText;
            initialTooltipText = tooltipText;

            // MULTI-MONITOR POSITIONING WORKAROUND:
            // When the owner window is on a secondary monitor positioned to the left of or above 
            // the primary monitor, Owner.Left or Owner.Top becomes negative. This causes
            // coordinate calculation issues in WPF's multi-monitor coordinate system.
            // Solution: Use WPF's built-in CenterOwner positioning instead of manual calculations.
            if (owner.Left < 0 || owner.Top < 0)
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            TextBoxEditTooltip.Text = TooltipText;
            
            // Size the TextBox based on content
            SizeTextBoxToContent();
        }

        /// <summary>
        /// Sizes the TextBox to fit its content
        /// </summary>
        private void SizeTextBoxToContent()
        {
            if (string.IsNullOrEmpty(TextBoxEditTooltip.Text))
            {
                return;
            }

            // Create a TextBlock to measure the text size
            var measuringBlock = new TextBlock
            {
                Text = TextBoxEditTooltip.Text,
                FontSize = TextBoxEditTooltip.FontSize,
                FontFamily = TextBoxEditTooltip.FontFamily,
                TextWrapping = TextWrapping.NoWrap
            };

            // Measure the text
            measuringBlock.Measure(new(double.PositiveInfinity, double.PositiveInfinity));
            
            // Calculate desired dimensions with padding
            double desiredWidth = Math.Min(Math.Max(measuringBlock.DesiredSize.Width + 40, MinWidth), MaxWidth);
            double desiredHeight = Math.Min(Math.Max(measuringBlock.DesiredSize.Height + 120, MinHeight), MaxHeight);

            // Set the window size
            Width = desiredWidth;
            Height = desiredHeight;
        }

        #region Loaded: Position dialog
        /// <summary>
        /// Positions the dialog window relative to the button that triggered it.
        /// 
        /// POSITIONING BEHAVIOR:
        /// - Primary Monitor: Dialog appears directly underneath the button with precise positioning
        /// - Secondary Monitor: Dialog appears centered on the Template Editor window (fallback)
        /// 
        /// MULTI-MONITOR ISSUE:
        /// WPF's coordinate system has issues when secondary monitors are positioned to the left of 
        /// or above the primary monitor. In these cases, Owner.Left or Owner.Top becomes negative,
        /// causing coordinate calculation errors that can position the dialog completely outside 
        /// the visible window area.
        /// 
        /// SOLUTION:
        /// Detect problematic coordinates (negative Owner position) in the constructor and use
        /// WindowStartupLocation.CenterOwner instead of manual positioning calculations.
        /// 
        /// TESTED CONFIGURATIONS:
        /// - Single monitor: Perfect positioning ✓
        /// - Dual monitors (secondary to right): Perfect positioning ✓  
        /// - Dual monitors (secondary to left): Centered positioning ✓
        /// - Dual monitors (secondary above/below): Centered positioning ✓
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (Owner == null)
            {
                return;
            }

            // If we're using CenterOwner (multi-monitor workaround), don't override the positioning
            if (WindowStartupLocation == WindowStartupLocation.CenterOwner)
            {
                return;
            }

            try
            {
                // PRECISE POSITIONING: Position dialog underneath the button
                // Get button's position relative to owner window's client area
                Point buttonPosition = PositionReference.TranslatePoint(new(0, 0), Owner);
                double buttonHeight = PositionReference.RenderSize.Height;
                
                // Account for owner window chrome (title bar and borders)
                double titleBarHeight = SystemParameters.WindowCaptionHeight;
                double borderThickness = SystemParameters.ResizeFrameHorizontalBorderHeight;
                
                // Calculate screen coordinates: owner position + chrome + button position
                Left = Owner.Left + borderThickness + buttonPosition.X;
                Top = Owner.Top + titleBarHeight + borderThickness + buttonPosition.Y + buttonHeight + 3; // +3 for dialog border offset
                
                // Ensure dialog doesn't go off screen
                if (Top < 0)
                {
                    Top = 0;
                }
                if (Left < 0)
                {
                    Left = 0;
                }
            }
            catch
            {
                // Fallback to center on owner if positioning fails
                Left = Owner.Left + (Owner.Width - ActualWidth) / 2;
                Top = Owner.Top + (Owner.Height - ActualHeight) / 2;
            }

            // On some older Windows versions the above positioning doesn't work as the list ends up to the right of the main window
            // Check to make sure it's in the main window, and if not, we try to position it there
            if (Application.Current != null)
            {
                double choiceRightSide = Left + ActualWidth;
                if (Application.Current.MainWindow == null)
                {
                    TracePrint.NullException(nameof(Application.Current.MainWindow));
                    return;
                }
                double mainWindowRightSide = Application.Current.MainWindow.Left + Application.Current.MainWindow.ActualWidth;
                if (choiceRightSide > mainWindowRightSide)
                {
                    Left = mainWindowRightSide - ActualWidth - 100;
                }
            }
            Dialogs.TryFitDialogInWorkingArea(this);
        }
        #endregion

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.TooltipText = TextBoxEditTooltip.Text;
            // If the tooltip text is unchanged, treat this as a cancel
            DialogResult = initialTooltipText != TooltipText;
           
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
