using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace Timelapse.Util
{
    /// <summary>
    /// Modern notification system using native Windows-style notifications
    /// Replaces the old ToastNotifications library
    /// </summary>
    public class ModernNotifier(Window owner)
    {
        /// <summary>
        /// Show an information notification
        /// </summary>
        public void ShowInformation(string message, NotificationOptions options = null)
        {
            ShowNotification(message, NotificationType.Information, options);
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
                StaysOpen = false
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
                Margin =   options.Compact              
                    ? new(4, 0, 0, 0)
                    : new(10, 0, 0, 0),
                FontSize = 16,
                Width = options.Compact  ? 24 : 32,
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

            // Position notification in center of the owner window unless explicitely asked to put it on the top left
            // The centering math here is a hack as  we don't know the size of the popup, but it works well enough for now
            if (options.TopLeft)
            {
                popup.HorizontalOffset = 0;
                popup.VerticalOffset = 0;
            }
            else
            {
                popup.HorizontalOffset = owner.ActualWidth / 2.0 - 220;
                popup.VerticalOffset = owner.ActualHeight / 2.0 - 80;
            }

            popup.IsOpen = true;

            // Auto-close after specified time
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(options.CloseAfter)
            };
            
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                popup.IsOpen = false;
            };
            
            timer.Start();

            // Allow clicking to close, although we have a close button too
            border.MouseLeftButtonUp += (_, _) => popup.IsOpen = false;
        }

        private Brush GetBackgroundColor(NotificationType type)
        {
            switch (type)
            {
                case NotificationType.Information:
                    return new SolidColorBrush(Color.FromRgb(217, 237, 247)); // Light blue
                case NotificationType.Success:
                    return new SolidColorBrush(Color.FromRgb(223, 240, 216)); // Light green
                case NotificationType.Warning:
                    return new SolidColorBrush(Color.FromRgb(252, 248, 227)); // Light yellow
                case NotificationType.Error:
                    return new SolidColorBrush(Color.FromRgb(248, 215, 218)); // Light red
                default:
                    return new SolidColorBrush(Color.FromRgb(248, 249, 250)); // Light gray
            }
        }

        private Brush GetBorderColor(NotificationType type)
        {
            switch (type)
            {
                case NotificationType.Information:
                    return new SolidColorBrush(Color.FromRgb(174, 213, 129)); // Blue
                case NotificationType.Success:
                    return new SolidColorBrush(Color.FromRgb(155, 204, 145)); // Green
                case NotificationType.Warning:
                    return new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Yellow
                case NotificationType.Error:
                    return new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Red
                default:
                    return new SolidColorBrush(Color.FromRgb(108, 117, 125)); // Gray
            }
        }

        private Brush GetForegroundColor(NotificationType type)
        {
            switch (type)
            {
                case NotificationType.Information:
                    return new SolidColorBrush(Color.FromRgb(13, 110, 180)); // Dark blue
                case NotificationType.Success:
                    return new SolidColorBrush(Color.FromRgb(25, 135, 84)); // Dark green
                case NotificationType.Warning:
                    return new SolidColorBrush(Color.FromRgb(102, 77, 3)); // Dark yellow
                case NotificationType.Error:
                    return new SolidColorBrush(Color.FromRgb(114, 28, 36)); // Dark red
                default:
                    return new SolidColorBrush(Color.FromRgb(33, 37, 41)); // Dark gray
            }
        }
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
        public int CloseAfter { get; set; } = 8000; // Default 8 seconds
        public bool ShowCloseButton { get; set; } = true;
        public string Tag { get; set; } = "";
        public bool TopLeft { get; set; } = false;
        public bool Compact { get; set; } = false;
    }
}