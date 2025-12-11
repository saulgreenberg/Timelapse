using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Constant;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    // File Navigation Slider (including Timer) callbacks and related
    public partial class TimelapseWindow
    {
        #region Callbacks
        // PreviewMouseDown callback - Force commit of any focused control's value before navigation
        private void FileNavigatorSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Take keyboard focus, which causes any focused data entry control to lose focus
            // This triggers their LostKeyboardFocus handlers, which commit their values
            FileNavigatorSlider.Focus();
        }

        // Drag Started callback
        private void FileNavigatorSlider_DragStarted(object sender, DragStartedEventArgs args)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going
            timerFileNavigator.Start(); // The timer forces an image display update to the current slider position if the user pauses longer than the timer's interval.
            State.FileNavigatorSliderDragging = true;
        }

        // Drag Completed callback
        private void FileNavigatorSlider_DragCompleted(object sender, DragCompletedEventArgs args)
        {
            FilePlayer_Stop(); // In case the FilePlayer is going
            State.FileNavigatorSliderDragging = false;
            FileShow(FileNavigatorSlider);
            timerFileNavigator.Stop();
        }

        // Value Changed callback
        private void FileNavigatorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> args)
        {
            // since the minimum value is 1 there's a value change event during InitializeComponent() to ignore
            if (State == null)
            {
                return;
            }

            // Stop the timer, but restart it if we are dragging
            timerFileNavigator.Stop();
            if (State.FileNavigatorSliderDragging)
            {
                timerFileNavigator.Interval = State.Throttles.DesiredIntervalBetweenRenders; // Throttle values may have changed, so we reset it just in case.
                timerFileNavigator.Start();
                FileNavigatorSlider.AutoToolTipContent = DataHandler?.ImageCache?.Current != null
                    ? DataHandler.ImageCache.Current.File
                    : string.Empty;
            }

            DateTime utcNow = DateTime.UtcNow;
            if ((State.FileNavigatorSliderDragging == false) || (utcNow - State.MostRecentDragEvent > timerFileNavigator.Interval))
            {
                FileShow(FileNavigatorSlider);
                State.MostRecentDragEvent = utcNow;
            }
        }

        // Got Focus: Create a semi-transparent visible blue border around the slider when it has the focus. Its semi-transparent to mute it somewhat...
        private void FileNavigatorSlider_GotFocus(object sender, RoutedEventArgs e)
        {
            SolidColorBrush brush = Control.BorderColorHighlight.Clone();
            brush.Opacity = .5;
            AutoToolTipSliderBorder.BorderBrush = brush;
        }

        // Lost Focus: revert border color to its default state
        private void FileNavigatorSlider_LostFocus(object sender, RoutedEventArgs e)
        {
            AutoToolTipSliderBorder.BorderBrush = Brushes.Transparent;
        }

        // Timer callback that forces image update to the current slider position. Invoked as the user pauses dragging the image slider
        private void TimerFileNavigator_Tick(object sender, EventArgs e)
        {
            timerFileNavigator.Stop();
            FileShow(FileNavigatorSlider);
            FileNavigatorSlider.AutoToolTipContent = DataHandler?.ImageCache?.Current != null
                ? DataHandler.ImageCache.Current.File
                : string.Empty;
        }
        #endregion

        #region Private Methods - Enable, Disable, Reset
        private void FileNavigatorSlider_EnableOrDisableValueChangedCallback(bool enableCallback)
        {
            if (enableCallback)
            {
                FileNavigatorSlider.ValueChanged += FileNavigatorSlider_ValueChanged;
            }
            else
            {
                FileNavigatorSlider.ValueChanged -= FileNavigatorSlider_ValueChanged;
            }
        }

        // Reset the slider: usually done to disable the FileNavigator when there is no image set to display.
        private void FileNavigatorSliderReset()
        {
            bool filesSelected = (IsFileDatabaseAvailable() && DataHandler.FileDatabase.CountAllCurrentlySelectedFiles > 0);

            timerFileNavigator.Stop();
            FileNavigatorSlider_EnableOrDisableValueChangedCallback(filesSelected);
            FileNavigatorSlider.IsEnabled = filesSelected;
            FileNavigatorSlider.Maximum = filesSelected ? DataHandler.FileDatabase.CountAllCurrentlySelectedFiles : 0;
        }
        #endregion
    }
}
