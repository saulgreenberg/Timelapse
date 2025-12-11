using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;

namespace TimelapseWpf.Toolkit
{
    // FormattedDialog: A reusable WPF dialog window for displaying formatted text messages
    // Now uses FormattedMessageContent user control for header and content display
    // Maintains same property-based interface for backward compatibility

    // NOTE: This control was created using AI prompting
    public partial class FormattedDialog
    {
        #region Properties

        // Note: Using base Window.DialogResult property instead of custom property
        // This ensures ShowDialog() returns the correct value

        // String properties for organizing dialog content into structured sections
        // These properties are stored internally and forwarded to the FormattedMessageContent user control
        // Each property supports formatting directives: **bold**, *italic*, __underline__, #Color[text], [link:url|display text]

        // Internal storage for properties
        private string _dialogTitle = "";
        private string _what = "";
        private string _problem = "";
        private string _reason = "";
        private string _solution = "";
        private string _result = "";
        private string _hint = "";
        private string _details = "";
        private DialogIconType _icon = DialogIconType.None;
        private bool isModal = true;

        // Dialog window title - also sets the actual window title
        public string DialogTitle
        {
            get => _dialogTitle;
            set => _dialogTitle = value;
        }

        // Description of what the dialog is about
        public string What
        {
            get => _what;
            set => _what = value;
        }

        // Description of a problem or issue
        public string Problem
        {
            get => _problem;
            set => _problem = value;
        }

        // Explanation of why something occurred
        public string Reason
        {
            get => _reason;
            set => _reason = value;
        }

        // Proposed solution or action
        public string Solution
        {
            get => _solution;
            set => _solution = value;
        }

        // Outcome or expected result
        public string Result
        {
            get => _result;
            set => _result = value;
        }

        // Additional tips or helpful information
        public string Hint
        {
            get => _hint;
            set => _hint = value;
        }

        // Detailed information displayed in a separate section with horizontal separator
        public string Details
        {
            get => _details;
            set => _details = value;
        }

        // Icon to display in the dialog header (Error, Information, Warning, Question, or None)
        public new DialogIconType Icon
        {
            get => _icon;
            set => _icon = value;
        }

        // If true, property names (headings) will not be shown and content will be unindented
        private bool _noHeadings;
        public bool NoHeadings
        {
            get => _noHeadings;
            set => _noHeadings = value;
        }

        #endregion

        #region Constructors

        // Button configuration for the dialog
        private readonly MessageBoxButtonType _buttonType;

        // Store initial values to detect explicit setting
        private double _initialWidth = double.NaN;
        private double _initialHeight = double.NaN;

        // Default constructor - initializes the dialog with empty content and OK/Cancel buttons
        public FormattedDialog(MessageBoxButtonType messageBoxButtonType = MessageBoxButtonType.OKCancel)
        {
            _buttonType = messageBoxButtonType;
            InitializeComponent();
            ConfigureButtons();
        }

        private void FormattedDialog_OnLoaded(object sender, RoutedEventArgs e)
        {
            TryPositionAndFitDialogIntoWindow(this);
        }
        #endregion

        #region BuildAndShowDialog
        public bool? BuildAndShowDialog()
        {
            // Build the content from properties before showing the dialog
            BuildContentFromProperties();
            // Show the dialog and return the result
            return ShowDialog();
        }

        public void BuildAndShowNonModalDialog()
        {
            // Build the content from properties before showing the dialog
            BuildContentFromProperties();
            // Show the dialog and return the result
            isModal = false;
            Show();
        }
        #endregion

        #region Main Content Building

        // Main method to build dialog content from string properties
        // Now delegates to the FormattedMessageContent user control
        // Call this after setting properties to generate the visual content
        public void BuildContentFromProperties()
        {
            // Set the window title if DialogTitle property is provided
            if (!string.IsNullOrEmpty(_dialogTitle))
            {
                Title = _dialogTitle;
            }

            // Find the user control by name and transfer properties
            if (this.FindName("MessageContent") is FormattedMessageContent messageContent)
            {
                messageContent.DialogTitle = _dialogTitle;
                messageContent.What = _what;
                messageContent.Problem = _problem;
                messageContent.Reason = _reason;
                messageContent.Solution = _solution;
                messageContent.Result = _result;
                messageContent.Hint = _hint;
                messageContent.Details = _details;
                messageContent.Icon = _icon;
                messageContent.NoHeadings = _noHeadings;

                // Delegate content building to the user control
                messageContent.BuildContentFromProperties();

                // Optimize dialog size after content is built
                this.Loaded += (_, _) =>
                {
                    // Capture initial values to detect explicit sizing
                    if (double.IsNaN(_initialWidth)) _initialWidth = Width;
                    if (double.IsNaN(_initialHeight)) _initialHeight = Height;

                    OptimizeDialogSizeSimple();
                };
            }
        }

        // Simple approach: measure content and size dialog appropriately
        // Respects manually set Height and/or Width properties
        private void OptimizeDialogSizeSimple()
        {
            try
            {
                var messageContent = this.FindName("MessageContent") as FormattedMessageContent;

                if (messageContent?.FindName("ContentPanel") is not System.Windows.Controls.StackPanel) return;

                // Check if Width or Height were explicitly set (different from XAML defaults)
                var defaultWidth = 600.0;  // Default from XAML
                var defaultHeight = 400.0; // Default from XAML
                var currentWidth = Width;
                var currentHeight = Height;
                var hasManualWidth = Math.Abs(currentWidth - defaultWidth) > 0.1;
                var hasManualHeight = Math.Abs(currentHeight - defaultHeight) > 0.1;



                // If both dimensions are manually set, don't optimize
                if (hasManualWidth && hasManualHeight)
                {
                    return;
                }

                // Only use SizeToContent measurement if both dimensions need optimization
                double naturalWidth = currentWidth;
                double naturalHeight = currentHeight;

                // Temporarily use SizeToContent to measure natural size
                var originalSizeToContent = SizeToContent;
                SizeToContent = hasManualWidth ? SizeToContent.Height : (hasManualHeight ? SizeToContent.Width : SizeToContent.WidthAndHeight);

                // Force layout update to get measurements
                this.UpdateLayout();

                // Get the measured size (only for dimensions that need optimization)
                if (!hasManualWidth) naturalWidth = this.ActualWidth;
                if (!hasManualHeight) naturalHeight = this.ActualHeight;

                // Restore manual sizing
                SizeToContent = originalSizeToContent;

                // Apply size constraints with slight height increase for better spacing
                var constrainedWidth = hasManualWidth ? currentWidth : Math.Max(MinWidth, Math.Min(naturalWidth, MaxWidth));
                var constrainedHeight = hasManualHeight ? currentHeight : Math.Max(MinHeight, Math.Min(naturalHeight + 20, MaxHeight)); // Add 20px for better spacing



                // Set the optimized size (only for dimensions that weren't manually set)
                if (!hasManualWidth) Width = constrainedWidth;
                if (!hasManualHeight) Height = constrainedHeight;

                // Final layout update
                this.UpdateLayout();
            }
            catch
            {
                // Fallback to reasonable default size (only if not manually set)
                var defaultWidth = 600.0;
                var defaultHeight = 400.0;
                var hasManualWidth = Math.Abs(_initialWidth - defaultWidth) > 0.1;
                var hasManualHeight = Math.Abs(_initialHeight - defaultHeight) > 0.1;

                if (!hasManualWidth) Width = Math.Min(600, MaxWidth);
                if (!hasManualHeight) Height = Math.Min(400, MaxHeight);
            }
        }

        // Method to configure button visibility and properties based on button type
        // Called from constructor to set up the appropriate buttons for the dialog
        private void ConfigureButtons()
        {
            // Hide all buttons initially
            YesButton.Visibility = Visibility.Collapsed;
            NoButton.Visibility = Visibility.Collapsed;
            OkButton.Visibility = Visibility.Collapsed;
            CancelButton.Visibility = Visibility.Collapsed;

            // Reset default and cancel button properties
            YesButton.IsDefault = false;
            NoButton.IsDefault = false;
            OkButton.IsDefault = false;
            CancelButton.IsDefault = false;
            YesButton.IsCancel = false;
            NoButton.IsCancel = false;
            OkButton.IsCancel = false;
            CancelButton.IsCancel = false;

            // Configure buttons based on the specified type
            switch (_buttonType)
            {
                case MessageBoxButtonType.OK:
                    OkButton.Visibility = Visibility.Visible;
                    OkButton.IsDefault = true; // First positive button
                    break;

                case MessageBoxButtonType.OKCancel:
                    OkButton.Visibility = Visibility.Visible;
                    CancelButton.Visibility = Visibility.Visible;
                    OkButton.IsDefault = true; // First positive button
                    CancelButton.IsCancel = true; // Allow Escape key to cancel
                    break;

                case MessageBoxButtonType.YesNo:
                    YesButton.Visibility = Visibility.Visible;
                    NoButton.Visibility = Visibility.Visible;
                    YesButton.IsDefault = true; // First positive button
                    break;

                case MessageBoxButtonType.YesNoCancel:
                    YesButton.Visibility = Visibility.Visible;
                    NoButton.Visibility = Visibility.Visible;
                    CancelButton.Visibility = Visibility.Visible;
                    YesButton.IsDefault = true; // First positive button
                    CancelButton.IsCancel = true; // Allow Escape key to cancel
                    break;
            }
        }

        #endregion

        #region Dialog Box Positioning and Fitting (copied from Timelapse.Dialog.Dialogs)

        // Most (but not all) invocations of SetDefaultDialogPosition and TryFitDialogInWorkingArea
        // are done together, so collapse it into a single call
        private static void TryPositionAndFitDialogIntoWindow(Window window)
        {
            SetDefaultDialogPosition(window);
            TryFitDialogInWorkingArea(window);
        }

        // Position the dialog box within its owner's window
        private static void SetDefaultDialogPosition(Window window)
        {
            // Check the arguments for null
            if (window?.Owner == null)
            {
                // Treat it as a no-op if no owner
                return;
            }

            window.Left = window.Owner.Left + (window.Owner.Width - window.ActualWidth) / 2; // Center it horizontally
            window.Top = window.Owner.Top + 20; // Offset it from the windows'top by 20 pixels downwards
        }

        // Used to ensure that the window is positioned within the screen
        // This will sort of fail if a window's minimum size is larger than the available screen space.
        // It should still show the window, but it may cut off the bottom of it.
        private static void TryFitDialogInWorkingArea(Window window)
        {
            int minimumWidthOrHeightValue = 200;
            int makeItATouchSmaller = 10;
            if (window == null)
            {
              return;
            }

            if (double.IsNaN(window.Left))
            {
                window.Left = 0;
            }

            if (double.IsNaN(window.Top))
            {
                window.Top = 0;
            }

            // Get DPI factor from the main window
            double dpiWidthFactor = 1;
            double dpiHeightFactor = 1;
            Window mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow == null)
            {
              return;
            }

            PresentationSource presentationSource = PresentationSource.FromVisual(mainWindow);
            if (presentationSource?.CompositionTarget != null)
            {
                Matrix m = presentationSource.CompositionTarget.TransformToDevice;
                dpiWidthFactor = m.M11;
                dpiHeightFactor = m.M22;
            }

            // Get the monitor screen that this window appears to be on
            Screen screenInDpi = Screen.FromHandle(new WindowInteropHelper(window).Handle);

            // Validate minimum size
            if (window.Width * dpiWidthFactor <= minimumWidthOrHeightValue || window.Height * dpiHeightFactor <= minimumWidthOrHeightValue)
            {
              return;
            }

            // Get a rectangle defining the current window, converted to dpi
            Rectangle windowPositionInDpi = new(
                (int)(window.Left * dpiWidthFactor),
                (int)(window.Top * dpiHeightFactor),
                (int)(window.Width * dpiWidthFactor),
                (int)(window.Height * dpiHeightFactor));

            // Get a rectangle defining the working area for this window
            Rectangle workingAreaInDpi = Screen.GetWorkingArea(windowPositionInDpi);

            // If needed, adjust the window's height to be somewhat smaller than the screen's height
            if (windowPositionInDpi.Height > screenInDpi.WorkingArea.Height)
            {
                double height = (screenInDpi.WorkingArea.Height - makeItATouchSmaller) / dpiHeightFactor;
                if (height < minimumWidthOrHeightValue || height < 0)
                {
                  return;
                }

                window.Height = height;
                window.Top = 0;
            }

            // move window up if it extends below the working area
            if (windowPositionInDpi.Bottom > workingAreaInDpi.Bottom)
            {
                int dpiToMoveUp = Convert.ToInt32(windowPositionInDpi.Bottom) - workingAreaInDpi.Bottom;
                if (dpiToMoveUp > windowPositionInDpi.Top)
                {
                    // window is too tall and has to shorten to fit screen
                    window.Top = 0;
                    double height = workingAreaInDpi.Bottom / dpiHeightFactor - makeItATouchSmaller;
                    if (height < minimumWidthOrHeightValue || height < 0)
                    {
                      return;
                    }

                    window.Height = height;
                }
                else if (dpiToMoveUp > 0)
                {
                    // move window up
                    window.Top -= dpiToMoveUp / dpiHeightFactor;
                }
            }

            // move window left if it extends right of the working area
            if (windowPositionInDpi.Right > workingAreaInDpi.Right)
            {
                int dpiToMoveLeft = windowPositionInDpi.Right - workingAreaInDpi.Right;
                if (windowPositionInDpi.Left >= 0 && dpiToMoveLeft > windowPositionInDpi.Left)
                {
                    // window is too wide and has to narrow to fit screen
                    window.Left = 0;
                    // So we don't get massively sized windows in case its a large working area with multiple monitors
                    window.Width = Math.Min(workingAreaInDpi.Width, windowPositionInDpi.Width);
                }
                else if (dpiToMoveLeft > 0)
                {
                    // move window left
                    window.Left -= dpiToMoveLeft / dpiWidthFactor;
                }
            }
        }

        #endregion

        #region Event Handlers

        // Yes button click handler - sets positive dialog result and closes
        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            if (isModal) base.DialogResult = true; // Yes maps to true
            Close();
        }

        // No button click handler - sets negative dialog result and closes
        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            if (isModal) base.DialogResult = false; // No maps to false
            Close();
        }

        // OK button click handler - sets positive dialog result and closes
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (isModal) base.DialogResult = true; // OK maps to true
            Close();
        }

        // Cancel button click handler - sets negative dialog result and closes
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (isModal) base.DialogResult = false; // Cancel maps to false
            Close();
        }

        #endregion

    }
}
