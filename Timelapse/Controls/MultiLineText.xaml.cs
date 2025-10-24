using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;

// Disable CS8632 - The annotation for nullable reference types should only be used in code within a #nullable annotations context.
#nullable enable

namespace Timelapse.Controls
{
    public class MultiLineText : Control
    {

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(MultiLineText),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(MultiLineText),
                new PropertyMetadata(false));

        public static readonly DependencyProperty MaxLengthProperty =
            DependencyProperty.Register(nameof(MaxLength), typeof(int), typeof(MultiLineText),
                new PropertyMetadata(0));

        public static readonly DependencyProperty AcceptsReturnProperty =
            DependencyProperty.Register(nameof(AcceptsReturn), typeof(bool), typeof(MultiLineText),
                new PropertyMetadata(true));

        public static readonly DependencyProperty TextWrappingProperty =
            DependencyProperty.Register(nameof(TextWrapping), typeof(TextWrapping), typeof(MultiLineText),
                new PropertyMetadata(TextWrapping.Wrap));

        public static readonly DependencyProperty PopupMinHeightProperty =
            DependencyProperty.Register(nameof(PopupMinHeight), typeof(double), typeof(MultiLineText),
                new PropertyMetadata(100.0));

        public static readonly DependencyProperty PopupMinWidthProperty =
            DependencyProperty.Register(nameof(PopupMinWidth), typeof(double), typeof(MultiLineText),
                new PropertyMetadata(200.0));

        public static readonly DependencyProperty PopupMaxWidthProperty =
            DependencyProperty.Register(nameof(PopupMaxWidth), typeof(double), typeof(MultiLineText),
                new PropertyMetadata(600.0));

        public static readonly DependencyProperty PopupMaxHeightProperty =
            DependencyProperty.Register(nameof(PopupMaxHeight), typeof(double), typeof(MultiLineText),
                new PropertyMetadata(350.0));

        public static readonly DependencyProperty PopupAutoSizeProperty =
            DependencyProperty.Register(nameof(PopupAutoSize), typeof(bool), typeof(MultiLineText),
                new PropertyMetadata(true));

        public static readonly DependencyProperty DisplayTextProperty =
            DependencyProperty.Register(nameof(DisplayText), typeof(string), typeof(MultiLineText),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDisplayTextChanged));

        public event EventHandler<MultiLineTextChangedEventArgs>? TextChanged;
        public event EventHandler? PopupOpened;
        public event EventHandler? PopupClosed;
        public new event EventHandler<KeyEventArgs>? KeyDown;

        private readonly bool _isPopupOpenAnimating = false;
        private HwndSource? _windowSource;
        
        // Template parts
        private Grid? MainGrid;
        private TextBox? MainTextBox;
        private Border? IconBorder;
        public Popup? EditorPopup;
        public TextBox? PopupTextBox;
        private Thumb? ResizeThumb;

        static MultiLineText()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(MultiLineText), new FrameworkPropertyMetadata(typeof(MultiLineText)));
        }

        public MultiLineText()
        {
            Loaded += MultiLineText_Loaded;
            Unloaded += MultiLineText_Unloaded;
            
            // Handle focus events for tab navigation
            //GotFocus += MultiLineText_GotFocus;
            LostFocus += MultiLineText_LostFocus;

            //GotKeyboardFocus += MultiLineText_GotFocus;
            LostKeyboardFocus += MultiLineText_LostFocus;

            // Handle key events for the control itself
            PreviewKeyDown += MultiLineText_PreviewKeyDown;
            
            // Handle clicks outside the popup when loaded
            Loaded += (_, _) =>
            {
                if (Application.Current?.MainWindow != null)
                {
                    Application.Current.MainWindow.AddHandler(UIElement.MouseDownEvent, new MouseButtonEventHandler(MainWindow_MouseDown), true);
                    Application.Current.MainWindow.Deactivated += MainWindow_Deactivated;
                    Application.Current.MainWindow.LocationChanged += MainWindow_LocationChanged;
                    Application.Current.MainWindow.SizeChanged += MainWindow_SizeChanged;
                    
                    // Hook into window messages to catch title bar clicks
                    _windowSource = HwndSource.FromHwnd(new WindowInteropHelper(Application.Current.MainWindow).Handle);
                    _windowSource?.AddHook(WindowProc);
                }
            };
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            
            // Get template parts
            MainGrid = GetTemplateChild("PART_MainGrid") as Grid;
            MainTextBox = GetTemplateChild("PART_MainTextBox") as TextBox;
            IconBorder = GetTemplateChild("PART_IconBorder") as Border;
            EditorPopup = GetTemplateChild("PART_EditorPopup") as Popup;
            PopupTextBox = GetTemplateChild("PART_PopupTextBox") as TextBox;
            ResizeThumb = GetTemplateChild("PART_ResizeThumb") as Thumb;
            
            // Wire up events
            if (MainTextBox != null)
            {
                MainTextBox.PreviewMouseLeftButtonDown += MainTextBox_PreviewMouseLeftButtonDown;
                MainTextBox.PreviewKeyDown += MainTextBox_PreviewKeyDown;
            }
            
            if (IconBorder != null)
            {
                IconBorder.PreviewMouseLeftButtonDown += MainTextBox_PreviewMouseLeftButtonDown;
            }
            
            if (PopupTextBox != null)
            {
                PopupTextBox.KeyDown += PopupTextBox_KeyDown;
                PopupTextBox.LostFocus += PopupTextBox_LostFocus;
                PopupTextBox.TextChanged += PopupTextBox_TextChanged;
            }
            
            if (ResizeThumb != null)
            {
                ResizeThumb.DragDelta += ResizeThumb_DragDelta;
            }
            
            if (EditorPopup != null)
            {
                // Set custom placement callback
                EditorPopup.CustomPopupPlacementCallback = PopupPlacement_Callback;
            }
            
            UpdateMainTextBox();
        }

        private void MultiLineText_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateMainTextBox();
        }

        private void MultiLineText_Unloaded(object sender, RoutedEventArgs e)
        {
            // Clean up event handlers
            if (Application.Current?.MainWindow != null)
            {
                Application.Current.MainWindow.RemoveHandler(UIElement.MouseDownEvent, new MouseButtonEventHandler(MainWindow_MouseDown));
                Application.Current.MainWindow.Deactivated -= MainWindow_Deactivated;
                Application.Current.MainWindow.LocationChanged -= MainWindow_LocationChanged;
                Application.Current.MainWindow.SizeChanged -= MainWindow_SizeChanged;
            }
            
            // Clean up window message hook
            _windowSource?.RemoveHook(WindowProc);
        }

        //private void MultiLineText_GotFocus(object sender, RoutedEventArgs e)
        //{
        //    // No automatic popup opening on focus - user must explicitly trigger it
        //    if (MainTextBox == null)
        //    {
        //        return;
        //    }
        //    if (this.IsFocused || this.IsKeyboardFocused)
        //    {
        //        this.MainTextBox.BorderThickness = new Thickness(3);
        //        this.MainTextBox.BorderBrush = Brushes.Blue;
        //    }
        //}

        private void MultiLineText_LostFocus(object sender, RoutedEventArgs e)
        {
            if (MainTextBox == null)
            {
                return;
            }
            //if (this.IsFocused == false && this.IsKeyboardFocused == false)
            //{
            //    this.MainTextBox.BorderThickness = new Thickness(1);
            //    this.MainTextBox.BorderBrush = Brushes.LightBlue;
            //}


            // Close popup when control loses focus (unless focus moved to the popup itself)
            if (EditorPopup != null && PopupTextBox != null && 
                EditorPopup.IsOpen && !PopupTextBox.IsFocused)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Double check after dispatcher to ensure focus hasn't moved to popup
                    if (EditorPopup != null && PopupTextBox != null &&
                        EditorPopup.IsOpen && !PopupTextBox.IsFocused && !IsMouseOverPopup())
                    {
                        CommitAndClosePopup();
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void MultiLineText_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (EditorPopup == null || PopupTextBox == null) return;
            
            // If the popup is not open and user starts typing, open it
            if (!EditorPopup.IsOpen && IsTextInputKey(e.Key))
            {
                ShowPopupEditor();
                
                // Forward the key to the popup textbox after it's focused
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (PopupTextBox is { IsFocused: true })
                    {
                        // Create a new KeyEventArgs for the popup textbox
                        if (Keyboard.PrimaryDevice.ActiveSource != null)
                        {
                            var keyEventArgs = new KeyEventArgs(Keyboard.PrimaryDevice, Keyboard.PrimaryDevice.ActiveSource, 0, e.Key)
                            {
                                RoutedEvent = PreviewKeyDownEvent
                            };
                            PopupTextBox.RaiseEvent(keyEventArgs);
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
                
                e.Handled = true;
            }
            else if (!EditorPopup.IsOpen && (e.Key == Key.Space || e.Key == Key.Enter || e.Key == Key.Down))
            {
                // Open popup on Space, Enter, or Down arrow keys
                ShowPopupEditor();
                e.Handled = true;
            }
        }

        private static bool IsTextInputKey(Key key)
        {
            // Check if the key represents a text input character
            return key is >= Key.A and <= Key.Z ||
                   key is >= Key.D0 and <= Key.D9 ||
                   key is >= Key.NumPad0 and <= Key.NumPad9 ||
                   key == Key.Space ||
                   key == Key.OemPeriod ||
                   key == Key.OemComma ||
                   key == Key.OemQuestion ||
                   key == Key.OemSemicolon ||
                   key == Key.OemQuotes ||
                   key == Key.OemOpenBrackets ||
                   key == Key.OemCloseBrackets ||
                   key == Key.OemMinus ||
                   key == Key.OemPlus ||
                   key == Key.Back ||
                   key == Key.Delete;
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public int MaxLength
        {
            get => (int)GetValue(MaxLengthProperty);
            set => SetValue(MaxLengthProperty, value);
        }

        public bool AcceptsReturn
        {
            get => (bool)GetValue(AcceptsReturnProperty);
            set => SetValue(AcceptsReturnProperty, value);
        }

        public TextWrapping TextWrapping
        {
            get => (TextWrapping)GetValue(TextWrappingProperty);
            set => SetValue(TextWrappingProperty, value);
        }

        public double PopupMinHeight
        {
            get => (double)GetValue(PopupMinHeightProperty);
            set => SetValue(PopupMinHeightProperty, value);
        }

        public double PopupMinWidth
        {
            get => (double)GetValue(PopupMinWidthProperty);
            set => SetValue(PopupMinWidthProperty, value);
        }

        public double PopupMaxWidth
        {
            get => (double)GetValue(PopupMaxWidthProperty);
            set => SetValue(PopupMaxWidthProperty, value);
        }

        public double PopupMaxHeight
        {
            get => (double)GetValue(PopupMaxHeightProperty);
            set => SetValue(PopupMaxHeightProperty, value);
        }

        public bool PopupAutoSize
        {
            get => (bool)GetValue(PopupAutoSizeProperty);
            set => SetValue(PopupAutoSizeProperty, value);
        }

        public string? DisplayText
        {
            get => (string?)GetValue(DisplayTextProperty);
            set => SetValue(DisplayTextProperty, value);
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MultiLineText control)
            {
                control.UpdateMainTextBox();
                control.OnTextChanged(new MultiLineTextChangedEventArgs 
                { 
                    NewText = e.NewValue as string, 
                    OldText = e.OldValue as string 
                });
            }
        }

        private static void OnDisplayTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MultiLineText control)
            {
                control.UpdateMainTextBox();
            }
        }

        private void UpdateMainTextBox()
        {
            if (MainTextBox != null)
            {
                MainTextBox.Text = DisplayText ?? Text;
            }
        }

        private void MainTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isPopupOpenAnimating && EditorPopup != null)
            {
                // Check if this is specifically a click on the icon area
                if (sender is Border && EditorPopup.IsOpen)
                {
                    CommitAndClosePopup();
                    e.Handled = true;
                    return;
                }
                
                if (!EditorPopup.IsOpen)
                {
                    // Give the control focus when clicked
                    this.Focus();
                    ShowPopupEditor();
                    e.Handled = true;
                }
            }
        }

        private void MainTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                // Let the MainTextBox handle tab navigation normally by not handling the event
                // This will allow WPF's normal tab navigation to work
                return;
            }
            if (e.Key == Key.Space || e.Key == Key.Down)
            {
                // Open popup on Space or Down arrow
                if (EditorPopup is { IsOpen: false })
                {
                    ShowPopupEditor();
                    e.Handled = true;
                }
            }
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_NCLBUTTONDOWN = 0x00A1; // Non-client area left button down
            const int WM_NCRBUTTONDOWN = 0x00A4; // Non-client area right button down
            
            if ((msg == WM_NCLBUTTONDOWN || msg == WM_NCRBUTTONDOWN) && EditorPopup is { IsOpen: true })
            {
                CommitAndClosePopup();
            }
            
            return IntPtr.Zero;
        }

        private void MainWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Check if popup is open and click is outside the popup
            if (EditorPopup is { IsOpen: true })
            {
                var isClickInsidePopup = false;

                // Check if click is inside the popup
                if (e.OriginalSource is DependencyObject source)
                {
                    DependencyObject? current = source;
                    while (current != null)
                    {
                        if (EditorPopup != null && current == EditorPopup.Child)
                        {
                            isClickInsidePopup = true;
                            break;
                        }
                        // need this if the parent is not a Visual or Visual3D (e.g., ContentElement)
                        try
                        {
                            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
                        }
                        catch 
                        {
                            return;
                        }
                    }
                }

                // If click is outside the popup, close it (this includes title bar, window chrome, etc.)
                if (!isClickInsidePopup)
                {
                    CommitAndClosePopup();
                }
            }
        }

        private void MainWindow_LocationChanged(object? sender, EventArgs e)
        {
            // Close popup when window is moved (e.g., by dragging title bar)
            if (EditorPopup is { IsOpen: true })
            {
                CommitAndClosePopup();
            }
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Close popup when window is resized
            if (EditorPopup is { IsOpen: true })
            {
                CommitAndClosePopup();
            }
        }

        private void MainWindow_Deactivated(object? sender, EventArgs e)
        {
            // Close popup when window loses focus
            if (EditorPopup is { IsOpen: true })
            {
                CommitAndClosePopup();
            }
        }

        private void ShowPopupEditor()
        {
            if (EditorPopup == null || EditorPopup.IsOpen) return;
            if (PopupTextBox == null) return;

            PopupTextBox.Text = Text;
            
            // Set the popup width to match the main control width
            if (EditorPopup.Child is Border border && MainGrid != null)
            {
                border.Width = MainGrid.ActualWidth;
                
                // Apply auto-sizing if enabled
                if (PopupAutoSize)
                {
                    UpdatePopupSize();
                }
            }
            
            EditorPopup.IsOpen = true;
            PopupOpened?.Invoke(this, EventArgs.Empty);
            
            // Focus the editor
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (PopupTextBox != null)
                {
                    PopupTextBox.Focus();
                    PopupTextBox.CaretIndex = PopupTextBox.Text.Length;
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void CommitAndClosePopup()
        {
            if (PopupTextBox != null && EditorPopup != null)
            {
                var newText = PopupTextBox.Text;
                if (newText != Text)
                {
                    Text = newText;
                }
                
                // Set DisplayText to match the actual content after changes
                DisplayText = Text;
                
                EditorPopup.IsOpen = false;
                PopupClosed?.Invoke(this, EventArgs.Empty);
                
                // FOCUS FIX: Restore focus to the main control itself to maintain visual styling
                // Use base.Focus() to bypass the overridden Focus() method that focuses MainTextBox
                // This ensures IsFocused=true on MultiLineText control, triggering the blue border style
                base.Focus();
            }
        }

        private void PopupTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (EditorPopup == null) return;
            
            if (e.Key == Key.Escape)
            {
                EditorPopup.IsOpen = false;
                PopupClosed?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                CommitAndClosePopup();
                e.Handled = true;
            }
            else if (e.Key == Key.Tab)
            {
                // Close popup and focus the MainTextBox
                CommitAndClosePopup();
                
                // Focus the MainTextBox so it can handle the tab navigation
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MainTextBox?.Focus();
                }), System.Windows.Threading.DispatcherPriority.Background);
                
                e.Handled = true;
            }
            KeyDown?.Invoke(this, e);
        }

        private void PopupTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Add a small delay to handle cases where focus is temporarily lost during resize operations
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Check if focus moved outside the popup entirely
                if (EditorPopup != null && PopupTextBox != null && 
                    EditorPopup.IsOpen && !PopupTextBox.IsFocused && !IsMouseOverPopup())
                {
                    CommitAndClosePopup();
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private bool IsMouseOverPopup()
        {
            if (EditorPopup?.Child is FrameworkElement popupChild)
            {
                var mousePos = Mouse.GetPosition(popupChild);
                return mousePos is { X: >= 0, Y: >= 0 } && 
                       mousePos.X <= popupChild.ActualWidth && 
                       mousePos.Y <= popupChild.ActualHeight;
            }
            return false;
        }

        private void PopupTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Auto-size popup when text changes if auto-sizing is enabled
            if (PopupAutoSize && EditorPopup is { IsOpen: true })
            {
                Dispatcher.BeginInvoke(new Action(UpdatePopupSize), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void UpdatePopupSize()
        {
            if (PopupTextBox == null || EditorPopup?.Child is not Border border) return;

            // Measure the text size
            var textToMeasure = string.IsNullOrEmpty(PopupTextBox.Text) ? "A" : PopupTextBox.Text;
            
            // First, measure text without width constraint to get natural width
            var formattedTextForWidth = new FormattedText(
                textToMeasure,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(PopupTextBox.FontFamily, PopupTextBox.FontStyle, PopupTextBox.FontWeight, PopupTextBox.FontStretch),
                PopupTextBox.FontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            // Calculate desired width based on longest line, with constraints
            var minWidth = Math.Max(PopupMinWidth, MainGrid?.ActualWidth ?? PopupMinWidth); // Use PopupMinWidth property
            var naturalWidth = formattedTextForWidth.WidthIncludingTrailingWhitespace + 40; // Add padding for scrollbar and margins
            var desiredWidth = Math.Max(minWidth, Math.Min(naturalWidth, PopupMaxWidth));

            // Now measure text with constrained width for height calculation
            var formattedTextForHeight = new FormattedText(
                textToMeasure,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(PopupTextBox.FontFamily, PopupTextBox.FontStyle, PopupTextBox.FontWeight, PopupTextBox.FontStretch),
                PopupTextBox.FontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                MaxTextWidth = desiredWidth - 40 // Account for padding
            };

            // Calculate height with padding and minimum constraints
            var desiredHeight = Math.Max(PopupMinHeight, formattedTextForHeight.Height + 60); // Add extra padding for textbox
            var constrainedHeight = Math.Min(desiredHeight, PopupMaxHeight);

            // Apply the new size
            border.Width = desiredWidth;
            border.Height = constrainedHeight;
        }

        private void ResizeThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            if (EditorPopup?.Child is Border border)
            {
                var newWidth = Math.Max(PopupMinWidth, Math.Min(PopupMaxWidth, border.ActualWidth + e.HorizontalChange));
                var newHeight = Math.Max(PopupMinHeight, Math.Min(PopupMaxHeight, border.ActualHeight + e.VerticalChange));
                border.Width = newWidth;
                border.Height = newHeight;
            }
        }


        protected virtual void OnTextChanged(MultiLineTextChangedEventArgs e)
        {
            TextChanged?.Invoke(this, e);
        }

        public new void Focus()
        {
            if (MainTextBox != null)
            {
                MainTextBox.Focus();
            }
            else
            {
                base.Focus();
            }
        }

        public void SelectAll()
        {
            if (EditorPopup != null && PopupTextBox != null && EditorPopup.IsOpen)
            {
                PopupTextBox.SelectAll();
            }
        }

        public void Clear()
        {
            Text = string.Empty;
        }


        public static CustomPopupPlacement[] PopupPlacement_Callback(Size popupSize, Size targetSize, Point offset)
        {
            return [
                new CustomPopupPlacement(new Point(0, targetSize.Height), PopupPrimaryAxis.Horizontal)
            ];
        }
    }

    public class MultiLineTextChangedEventArgs : EventArgs
    {
        public string? NewText { get; set; }
        public string? OldText { get; set; }
    }

    // Converter to create border thickness for textbox (removes right border)
    public class LeftTopRightBottomThicknessConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Thickness thickness)
            {
                // Return thickness with no right border for textbox
                return new Thickness(thickness.Left, thickness.Top, 0, thickness.Bottom);
            }
            return new Thickness(1, 1, 0, 1);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}