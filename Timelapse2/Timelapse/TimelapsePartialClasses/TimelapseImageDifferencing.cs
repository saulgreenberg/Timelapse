using System;
using System.Windows;
using Timelapse.Controls;
using Timelapse.Enums;

namespace Timelapse
{
    // Image Differencing 
    public partial class TimelapseWindow : Window, IDisposable
    {
        #region Try View Previous or Next Difference
        // Cycle through the image differences in the order: current, then previous and next differenced images.
        // Create and cache the differenced images.
        private void TryViewPreviousOrNextDifference()
        {
            // Only allow differencing in single image mode.
            if (!this.IsDisplayingActiveSingleImage())
            {
                return;
            }

            // Note:  No matter what image we are viewing, the source image should have  been cached before entering this function\
            // If it isn't (or if its a video), abort
            if (this.DataHandler == null ||
                this.DataHandler.ImageCache == null ||
                this.DataHandler.ImageCache.Current == null ||
                this.DataHandler.ImageCache.Current.IsVideo)
            {
                this.StatusBar.SetMessage(String.Format("Differences can't be shown for videos, missing, or corrupt files"));
                return;
            }

            // Go to the next image in the cycle we want to show.
            this.DataHandler.ImageCache.MoveToNextStateInPreviousNextDifferenceCycle();

            // If we are supposed to display the unaltered image, do it and get out of here.
            // The unaltered image will always be cached at this point, so there is no need to check.
            if (this.DataHandler.ImageCache.CurrentDifferenceState == ImageDifferenceEnum.Unaltered)
            {
                this.MarkableCanvas.SetDisplayImage(this.DataHandler.ImageCache.GetCurrentImage);

                // Check if its a corrupted image
                if (!this.DataHandler.ImageCache.Current.IsDisplayable(this.FolderPath))
                {
                    // TO DO AS WE MAY HAVE TO GET THE INDEX OF THE NEXT IN CYCLE IMAGE???
                    this.StatusBar.SetMessage(String.Format("Difference can't be shown: the current file is likely missing or corrupted"));
                }
                else
                {
                    this.StatusBar.ClearMessage();
                }
                return;
            }

            // Generate and cache difference image if needed
            if (this.DataHandler.ImageCache.GetCurrentImage == null)
            {
                ImageDifferenceResultEnum result = this.DataHandler.ImageCache.TryCalculateDifference();
                switch (result)
                {
                    case ImageDifferenceResultEnum.CurrentImageNotAvailable:
                    case ImageDifferenceResultEnum.NextImageNotAvailable:
                    case ImageDifferenceResultEnum.PreviousImageNotAvailable:
                    case ImageDifferenceResultEnum.NotCalculable:
                        this.StatusBar.SetMessage(String.Format("Difference can't be shown: the {0} file is a video, missing, corrupt, or a different size", this.DataHandler.ImageCache.CurrentDifferenceState == ImageDifferenceEnum.Previous ? "previous" : "next"));
                        return;
                    case ImageDifferenceResultEnum.Success:
                        this.StatusBar.SetMessage(String.Format("Viewing difference from {0} file.", this.DataHandler.ImageCache.CurrentDifferenceState == ImageDifferenceEnum.Previous ? "previous" : "next"));
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled difference result {0}.", result));
                }
            }

            // display the differenced image
            // the magnifying glass always displays the original non-diferenced image so ImageToDisplay is updated and ImageToMagnify left unchnaged
            // this allows the user to examine any particular differenced area and see what it really looks like in the non-differenced image. 
            this.MarkableCanvas.SetDisplayImage(this.DataHandler.ImageCache.GetCurrentImage);
            this.StatusBar.SetMessage(String.Format("Viewing difference from {0} file.", this.DataHandler.ImageCache.CurrentDifferenceState == ImageDifferenceEnum.Previous ? "previous" : "next"));
        }
        #endregion

        #region Try View Combined Difference
        // View the differences between the current, previous, and next image
        private void TryViewCombinedDifference()
        {
            // Only allow differencing in single image mode.
            if (!this.IsDisplayingActiveSingleImage())
            {
                return;
            }

            if (this.DataHandler == null ||
                this.DataHandler.ImageCache == null ||
                this.DataHandler.ImageCache.Current == null ||
                this.DataHandler.ImageCache.Current.IsVideo)
            {
                this.StatusBar.SetMessage(String.Format("Combined differences can't be shown for videos, missing, or corrupt files"));
                return;
            }

            // If we are in any state other than the unaltered state, go to the unaltered state, otherwise the combined diff state
            this.DataHandler.ImageCache.MoveToNextStateInCombinedDifferenceCycle();
            if (this.DataHandler.ImageCache.CurrentDifferenceState == ImageDifferenceEnum.Unaltered)
            {
                this.MarkableCanvas.SetDisplayImage(this.DataHandler.ImageCache.GetCurrentImage);
                this.StatusBar.ClearMessage();
                return;
            }

            // Generate and cache difference image if needed
            if (this.DataHandler.ImageCache.GetCurrentImage == null)
            {
                ImageDifferenceResultEnum result = this.DataHandler.ImageCache.TryCalculateCombinedDifference(this.State.DifferenceThreshold);
                switch (result)
                {
                    case ImageDifferenceResultEnum.CurrentImageNotAvailable:
                        this.StatusBar.SetMessage("Combined difference can't be shown: the current file is a video, missing, corrupt, or a different size");
                        return;
                    case ImageDifferenceResultEnum.NextImageNotAvailable:
                    case ImageDifferenceResultEnum.NotCalculable:
                    case ImageDifferenceResultEnum.PreviousImageNotAvailable:
                        this.StatusBar.SetMessage(String.Format("Combined differences can't be shown: surrounding files include a video, missing, corrupt, or a different size file"));
                        return;
                    case ImageDifferenceResultEnum.Success:
                        this.StatusBar.SetMessage("Viewing differences from both the next and previous files");
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled combined difference result {0}.", result));
                }
            }

            // display differenced image
            this.MarkableCanvas.SetDisplayImage(this.DataHandler.ImageCache.GetCurrentImage);
            this.StatusBar.SetMessage("Viewing differences from both the next and previous files");
        }
        #endregion
    }
}
