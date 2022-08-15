using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Timelapse
{
    // File Navigation Slider (including Timer) callbacks and related
    public partial class TimelapseWindow : Window, IDisposable
    {
        #region Callbacks
        // Drag Started callback
        private void FileNavigatorSlider_DragStarted(object sender, DragStartedEventArgs args)
        {
            this.FilePlayer_Stop(); // In case the FilePlayer is going
            this.timerFileNavigator.Start(); // The timer forces an image display update to the current slider position if the user pauses longer than the timer's interval. 
            this.State.FileNavigatorSliderDragging = true;
        }

        // Drag Completed callback
        private void FileNavigatorSlider_DragCompleted(object sender, DragCompletedEventArgs args)
        {
            this.FilePlayer_Stop(); // In case the FilePlayer is going
            this.State.FileNavigatorSliderDragging = false;
            this.FileShow(this.FileNavigatorSlider);
            this.timerFileNavigator.Stop();
        }

        // Value Changed callback
        private void FileNavigatorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> args)
        {
            // since the minimum value is 1 there's a value change event during InitializeComponent() to ignore
            if (this.State == null)
            {
                return;
            }

            // Stop the timer, but restart it if we are dragging
            this.timerFileNavigator.Stop();
            if (this.State.FileNavigatorSliderDragging == true)
            {
                this.timerFileNavigator.Interval = this.State.Throttles.DesiredIntervalBetweenRenders; // Throttle values may have changed, so we reset it just in case.
                this.timerFileNavigator.Start();
            }

            DateTime utcNow = DateTime.UtcNow;
            if ((this.State.FileNavigatorSliderDragging == false) || (utcNow - this.State.MostRecentDragEvent > this.timerFileNavigator.Interval))
            {
                this.FileShow(this.FileNavigatorSlider);
                this.State.MostRecentDragEvent = utcNow;
                this.FileNavigatorSlider.AutoToolTipContent = this.DataHandler.ImageCache.Current.File;
            }
        }

        // Got Focus: Create a semi-transparent visible blue border around the slider when it has the focus. Its semi-transparent to mute it somewhat...
        private void FileNavigatorSlider_GotFocus(object sender, RoutedEventArgs e)
        {
            SolidColorBrush brush = Constant.Control.BorderColorHighlight.Clone();
            brush.Opacity = .5;
            this.AutoToolTipSliderBorder.BorderBrush = brush;
        }

        // Lost Focus: revert border color to its default state
        private void FileNavigatorSlider_LostFocus(object sender, RoutedEventArgs e)
        {
            this.AutoToolTipSliderBorder.BorderBrush = Brushes.Transparent;
        }

        // Timer callback that forces image update to the current slider position. Invoked as the user pauses dragging the image slider 
        private void TimerFileNavigator_Tick(object sender, EventArgs e)
        {
            this.timerFileNavigator.Stop();
            this.FileShow(this.FileNavigatorSlider);
            this.FileNavigatorSlider.AutoToolTipContent = this.DataHandler.ImageCache.Current.File;
        }
        #endregion

        #region Private Methods - Enable, Disable, Reset
        private void FileNavigatorSlider_EnableOrDisableValueChangedCallback(bool enableCallback)
        {
            if (enableCallback)
            {
                this.FileNavigatorSlider.ValueChanged += new RoutedPropertyChangedEventHandler<double>(this.FileNavigatorSlider_ValueChanged);
            }
            else
            {
                this.FileNavigatorSlider.ValueChanged -= new RoutedPropertyChangedEventHandler<double>(this.FileNavigatorSlider_ValueChanged);
            }
        }

        // Reset the slider: usually done to disable the FileNavigator when there is no image set to display.
        private void FileNavigatorSliderReset()
        {
            bool filesSelected = (this.IsFileDatabaseAvailable() && this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles > 0);

            this.timerFileNavigator.Stop();
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(filesSelected);
            this.FileNavigatorSlider.IsEnabled = filesSelected;
            this.FileNavigatorSlider.Maximum = filesSelected ? this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles : 0;
        }
        #endregion
    }
}
