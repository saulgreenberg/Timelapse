using System;
using System.Windows;
using Timelapse.Enums;
using Timelapse.EventArguments;

namespace Timelapse
{
    // FilePlayer and FilePlayerTimer
    public partial class TimelapseWindow : Window, IDisposable
    {
        #region Callbacks
        // FilePlayerChange: The user has clicked on the file player. Take action on what was requested
        private void FilePlayer_FilePlayerChange(object sender, FilePlayerEventArgs args)
        {
            switch (args.Selection)
            {
                case FilePlayerSelectionEnum.First:
                    this.FilePlayer_Stop();
                    this.FileNavigatorSlider.Value = 1;
                    break;
                case FilePlayerSelectionEnum.Page:
                    this.FilePlayer_ScrollPage();
                    break;
                case FilePlayerSelectionEnum.Row:
                    this.FilePlayer_ScrollRow();
                    break;
                case FilePlayerSelectionEnum.Last:
                    this.FilePlayer_Stop();
                    this.FileNavigatorSlider.Value = this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles;
                    break;
                case FilePlayerSelectionEnum.Step:
                    this.FilePlayer_Stop();
                    this.FilePlayerTimer_Tick(null, null);
                    break;
                case FilePlayerSelectionEnum.PlayFast:
                    this.FilePlayer_Play(TimeSpan.FromSeconds(this.State.FilePlayerFastValue));
                    break;
                case FilePlayerSelectionEnum.PlaySlow:
                    this.FilePlayer_Play(TimeSpan.FromSeconds(this.State.FilePlayerSlowValue));
                    break;
                case FilePlayerSelectionEnum.Stop:
                default:
                    this.FilePlayer_Stop();
                    break;
            }
        }

        // TimerTick: On every tick, try to show the next/previous file as indicated by the direction
        private void FilePlayerTimer_Tick(object sender, EventArgs e)
        {
            this.TryFileShowWithoutSliderCallback(this.FilePlayer.Direction);

            // Stop the timer if the image reaches the beginning or end of the image set
            if ((this.DataHandler.ImageCache.CurrentRow >= this.DataHandler.FileDatabase.CountAllCurrentlySelectedFiles - 1) || (this.DataHandler.ImageCache.CurrentRow <= 0))
            {
                this.FilePlayer_Stop();
            }
        }
        #endregion

        #region Private methods to actually do FilePlayer actions

        // Play. Stop the timer, reset the timer interval, and then restart the timer 
        private void FilePlayer_Play(TimeSpan timespan)
        {
            this.FilePlayerTimer.Stop();
            this.FilePlayerTimer.Interval = timespan;
            this.FilePlayerTimer.Start();
        }

        // Stop: both the file player and the timer
        private void FilePlayer_Stop()
        {
            this.FilePlayerTimer.Stop();
            this.FilePlayer.Stop();
        }

        // Scroll Row - a row of images the ThumbnailGrid
        private void FilePlayer_ScrollRow()
        {
            this.TryFileShowWithoutSliderCallback(this.FilePlayer.Direction, this.MarkableCanvas.ThumbnailGrid.AvailableColumns);
        }

        // ScrollPage: a page of images the ThumbnailGrid
        private void FilePlayer_ScrollPage()
        {
            this.TryFileShowWithoutSliderCallback(this.FilePlayer.Direction, this.MarkableCanvas.ThumbnailGrid.AvailableColumns * this.MarkableCanvas.ThumbnailGrid.AvailableRows);
        }
        #endregion
    }
}
