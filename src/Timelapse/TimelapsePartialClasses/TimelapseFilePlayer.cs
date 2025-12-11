using System;
using Timelapse.Enums;
using Timelapse.EventArguments;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    // FilePlayer and FilePlayerTimer
    public partial class TimelapseWindow
    {
        #region Callbacks
        // FilePlayerChange: The user has clicked on the file player. Take action on what was requested
        private void FilePlayer_FilePlayerChange(object sender, FilePlayerEventArgs args)
        {
            switch (args.Selection)
            {
                case FilePlayerSelectionEnum.First:
                    FilePlayer_Stop();
                    FileNavigatorSlider.Value = 1;
                    break;
                case FilePlayerSelectionEnum.Page:
                    FilePlayer_ScrollPage();
                    break;
                case FilePlayerSelectionEnum.Row:
                    FilePlayer_ScrollRow();
                    break;
                case FilePlayerSelectionEnum.Last:
                    FilePlayer_Stop();
                    FileNavigatorSlider.Value = DataHandler.FileDatabase.CountAllCurrentlySelectedFiles;
                    break;
                case FilePlayerSelectionEnum.Step:
                    FilePlayer_Stop();
                    FilePlayerTimer_Tick(null, null);
                    break;
                case FilePlayerSelectionEnum.PlayFast:
                    FilePlayer_Play(TimeSpan.FromSeconds(State.FilePlayerFastValue));
                    break;
                case FilePlayerSelectionEnum.PlaySlow:
                    FilePlayer_Play(TimeSpan.FromSeconds(State.FilePlayerSlowValue));
                    break;
                case FilePlayerSelectionEnum.Stop:
                default:
                    FilePlayer_Stop();
                    break;
            }
        }

        // TimerTick: On every tick, try to show the next/previous file as indicated by the direction
        private void FilePlayerTimer_Tick(object sender, EventArgs e)
        {
            TryFileShowWithoutSliderCallback(FilePlayer.Direction);

            // Stop the timer if the image reaches the beginning or end of the image set
            if ((DataHandler.ImageCache.CurrentRow >= DataHandler.FileDatabase.CountAllCurrentlySelectedFiles - 1) || (DataHandler.ImageCache.CurrentRow <= 0))
            {
                FilePlayer_Stop();
            }
        }
        #endregion

        #region Private methods to actually do FilePlayer actions

        // Play. Stop the timer, reset the timer interval, and then restart the timer 
        private void FilePlayer_Play(TimeSpan timespan)
        {
            FilePlayerTimer.Stop();
            FilePlayerTimer.Interval = timespan;
            FilePlayerTimer.Start();
        }

        // Stop: both the file player and the timer
        private void FilePlayer_Stop()
        {
            FilePlayerTimer.Stop();
            FilePlayer.Stop();
        }

        // Scroll Row - a row of images the ThumbnailGrid
        private void FilePlayer_ScrollRow()
        {
            TryFileShowWithoutSliderCallback(FilePlayer.Direction, MarkableCanvas.ThumbnailGrid.AvailableColumns);
        }

        // ScrollPage: a page of images the ThumbnailGrid
        private void FilePlayer_ScrollPage()
        {
            TryFileShowWithoutSliderCallback(FilePlayer.Direction, MarkableCanvas.ThumbnailGrid.AvailableColumns * MarkableCanvas.ThumbnailGrid.AvailableRows);
        }
        #endregion
    }
}
