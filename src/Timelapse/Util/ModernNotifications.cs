using System;
using System.Runtime.InteropServices;
using System.Windows;
using Timelapse.Constant;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Timelapse.Util
{
    /// <summary>
    /// Modern notification system using native Windows-style notifications
    /// Replaces the old ToastNotifications library
    /// </summary>
    public class ModernNotifier(Window owner)
    {
        private Popup _currentPopup;

        /// <summary>
        /// Show a compact information notification by the cursor
        /// </summary>
        public void ShowInformation(string message, NotificationOptions options = null)
        {
            ShowNotification(message, NotificationType.Information, options);
        }

        /// <summary>
        /// Show an information notification
        /// </summary>
        public void ShowInformationByCursor(string message)
        {
            ShowNotification(message, NotificationType.Information,
                new NotificationOptions
                {
                    ShowCloseButton = true,
                    CloseAfter = 3000,
                    Compact = true,
                    AttachToCursor = true,
                });
        }

        /// <summary>
        /// Show a warning notification  
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public void ShowWarning(string message, NotificationOptions options = null)
        {
            ShowNotification(message, NotificationType.Warning, options);
        }

        /// <summary>
        /// Show an error notification
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public void ShowError(string message, NotificationOptions options = null)
        {
            ShowNotification(message, NotificationType.Error, options);
        }

        /// <summary>
        /// Show a success notification
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public void ShowSuccess(string message, NotificationOptions options = null)
        {
            ShowNotification(message, NotificationType.Success, options);
        }

        public void Dismiss()
        {
            if (_currentPopup is { IsOpen: true })
                _currentPopup.IsOpen = false;
        }

        private void ShowNotification(string message, NotificationType type, NotificationOptions options = null)
        {
            options ??= new();

            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                try
                {
                    // Try to use Windows 10/11 native notifications if available
                    if (Environment.OSVersion.Version.Major >= 10)
                    {
                        // ShowWindowsNotification(message, type);
                        ShowWindowsNotification(message, type, options);
                    }
                    else
                    {
                        // Fallback to in-app notification for older Windows versions
                        ShowInAppNotification(message, type, options);
                    }
                }
                catch
                {
                    // Fallback to in-app notification if Windows notifications fail
                    ShowInAppNotification(message, type, options);
                }
            }));
        }

        private void ShowWindowsNotification(string message, NotificationType type, NotificationOptions options)
        {
            // For Windows 10/11, we can use a simple MessageBox-style approach
            // In a full implementation, you might use Windows.UI.Notifications.ToastNotification
            // For now, we'll use a clean in-app notification that looks modern
            //ShowInAppNotification(message, type, new NotificationOptions());
            ShowInAppNotification(message, type, options);
        }

        private void ShowInAppNotification(string message, NotificationType type, NotificationOptions options)
        {
            if (owner == null) return;

            // Create a modern-looking notification popup
            var popup = new Popup
            {
                AllowsTransparency = true,
                Placement = PlacementMode.Relative,
                PlacementTarget = owner,
                StaysOpen = true   // false triggers WPF mouse capture, which swallows WM_MOUSEWHEEL
            };

            var border = new Border
            {
                Background = GetBackgroundColor(type),
                BorderBrush = GetBorderColor(type),
                BorderThickness = new(1),
                CornerRadius = options.Compact
                ? new CornerRadius(3)
                : new CornerRadius(8),
                Margin = options.Compact
                    ? new Thickness(2)
                    : new Thickness(20),
                Padding = options.Compact
                    ? new Thickness(2)
                    : new Thickness(16, 12, 16, 12),
                //Padding = new Thickness(20),
                //MaxWidth = 400,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 10,
                    ShadowDepth = 2,
                    Opacity = 0.3
                }
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
            };
            var textBlock = new TextBlock
            {
                Text = message,
                Foreground = GetForegroundColor(type),
                FontFamily = new("Segoe UI"),
                FontSize = options.Compact ? 10 : 14,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            var closeButton = new Button
            {
                Content = "X",
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Margin = options.Compact
                    ? new(4, 0, 0, 0)
                    : new(10, 0, 0, 0),
                FontSize = 16,
                Width = options.Compact ? 24 : 32,
                Height = options.Compact ? 24 : 32,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Foreground = GetForegroundColor(type),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Right,
                Visibility = options.ShowCloseButton ? Visibility.Visible : Visibility.Collapsed
            };
            closeButton.Click += (_, _) => popup.IsOpen = false;

            stackPanel.Children.Add(textBlock);
            stackPanel.Children.Add(closeButton);

            border.Child = stackPanel;
            popup.Child = border;

            // Position: AttachToCursor overrides all offset options.
            if (options.AttachToCursor)
            {
                Point pos = GetCursorPosRelativeToOwner();
                popup.HorizontalOffset = pos.X;
                popup.VerticalOffset = pos.Y + 20;
            }
            else if (options.OffsetX.HasValue && options.OffsetY.HasValue)
            {
                popup.HorizontalOffset = options.OffsetX.Value;
                popup.VerticalOffset = options.OffsetY.Value;
            }
            else if (options.TopLeft)
            {
                popup.HorizontalOffset = 0;
                popup.VerticalOffset = 0;
            }
            else
            {
                popup.HorizontalOffset = owner.ActualWidth / 2.0 - 220;
                popup.VerticalOffset = owner.ActualHeight / 2.0 - 80;
            }

            // Make the popup transparent to all mouse events except the close button.
            // Returning HTTRANSPARENT from WM_NCHITTEST causes Windows to re-route every
            // mouse message (clicks, wheel, horizontal scroll, extra buttons) to the window
            // beneath the cursor, so nothing is swallowed by the popup's HWND.
            popup.Opened += (_, _) =>
            {
                if (PresentationSource.FromVisual(popup.Child) is HwndSource hwndSource)
                {
                    hwndSource.AddHook((_, msg, _, lParam, ref handled) =>
                    {
                        const int WM_NCHITTEST = 0x0084;
                        const int HTTRANSPARENT = -1;
                        if (msg == WM_NCHITTEST)
                        {
                            if (options.ShowCloseButton)
                            {
                                long lp = lParam.ToInt64();
                                int screenX = (short)(lp & 0xFFFF);
                                int screenY = (short)((lp >> 16) & 0xFFFF);
                                Point btnTopLeft = closeButton.PointToScreen(new Point(0, 0));
                                var btnRect = new Rect(btnTopLeft.X, btnTopLeft.Y, closeButton.ActualWidth, closeButton.ActualHeight);
                                if (btnRect.Contains(screenX, screenY))
                                    return IntPtr.Zero;
                            }
                            handled = true;
                            return new IntPtr(HTTRANSPARENT);
                        }
                        return IntPtr.Zero;
                    });
                }
            };

            if (_currentPopup is { IsOpen: true })
                _currentPopup.IsOpen = false;
            _currentPopup = popup;
            popup.IsOpen = true;

            // Track the cursor position while the popup is open.
            if (options.AttachToCursor)
            {
                var trackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                trackTimer.Tick += (_, _) =>
                {
                    if (!popup.IsOpen) { trackTimer.Stop(); return; }
                    Point p = GetCursorPosRelativeToOwner();
                    popup.HorizontalOffset = p.X;
                    popup.VerticalOffset = p.Y + 20;
                };
                trackTimer.Start();
                popup.Closed += (_, _) => trackTimer.Stop();
            }

            // Auto-close: wait (CloseAfter - 500ms), then fade to transparent over 500ms.
            const int fadeDurationMs = 500;
            int waitMs = Math.Max(0, options.CloseAfter - fadeDurationMs);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(waitMs) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(fadeDurationMs));
                fadeOut.Completed += (_, _) => popup.IsOpen = false;
                border.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            };
            timer.Start();

        }

        private static SolidColorBrush GetBackgroundColor(NotificationType type)
        {
            return type switch
            {
                NotificationType.Information => Colours.Notification.BackgroundInformation,
                NotificationType.Success     => Colours.Notification.BackgroundSuccess,
                NotificationType.Warning     => Colours.Notification.BackgroundWarning,
                NotificationType.Error       => Colours.Notification.BackgroundError,
                _                            => Colours.Notification.BackgroundNeutral
            };
        }

        private static SolidColorBrush GetBorderColor(NotificationType type)
        {
            return type switch
            {
                NotificationType.Information => Colours.Notification.BorderInformation,
                NotificationType.Success     => Colours.Notification.BorderSuccess,
                NotificationType.Warning     => Colours.Notification.BorderWarning,
                NotificationType.Error       => Colours.Notification.BorderError,
                _                            => Colours.Notification.BorderNeutral
            };
        }

        private static SolidColorBrush GetForegroundColor(NotificationType type)
        {
            return type switch
            {
                NotificationType.Information => Colours.Notification.ForegroundInformation,
                NotificationType.Success     => Colours.Notification.ForegroundSuccess,
                NotificationType.Warning     => Colours.Notification.ForegroundWarning,
                NotificationType.Error       => Colours.Notification.ForegroundError,
                _                            => Colours.Notification.ForegroundNeutral
            };
        }

        private Point GetCursorPosRelativeToOwner()
        {
            GetCursorPos(out NativePoint p);
            return owner.PointFromScreen(new Point(p.X, p.Y));
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out NativePoint pt);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint { public int X, Y; }
    }

    public enum NotificationType
    {
        Information,
        Success,
        Warning,
        Error
    }

    public class NotificationOptions
    {
        public int CloseAfter { get; set; } = 3000; // Default 3 seconds
        public bool ShowCloseButton { get; set; } = true;
        public string Tag { get; set; } = "";
        public bool TopLeft { get; set; } = false;
        public bool Compact { get; set; }
        public double? OffsetX { get; set; } = null;
        public double? OffsetY { get; set; } = null;
        public bool AttachToCursor { get; set; }
    }
}