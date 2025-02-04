using System;
using Timelapse.Controls;
using Timelapse.Enums;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    // Image Differencing 
    public partial class TimelapseWindow
    {
        #region Try View Previous or Next Difference
        // Cycle through the image differences in the order: current, then previous and next differenced images.
        // Create and cache the differenced images.
        private void TryViewPreviousOrNextDifference()
        {
            // Only allow differencing in single image mode.
            if (!IsDisplayingActiveSingleImageOrVideo())
            {
                return;
            }

            // Note:  No matter what image we are viewing, the source image should have  been cached before entering this function\
            // If it isn't (or if its a video), abort
            if (DataHandler == null ||
                DataHandler.ImageCache == null ||
                DataHandler.ImageCache.Current == null ||
                DataHandler.ImageCache.Current.IsVideo)
            {
                StatusBar.SetMessage("Differences can't be shown for videos, missing, or corrupt files");
                return;
            }

            // Go to the next image in the cycle we want to show.
            DataHandler.ImageCache.MoveToNextStateInPreviousNextDifferenceCycle();

            // If we are supposed to display the unaltered image, do it and get out of here.
            // The unaltered image will always be cached at this point, so there is no need to check.
            if (DataHandler.ImageCache.CurrentDifferenceState == ImageDifferenceEnum.Unaltered)
            {
                MarkableCanvas.SetDisplayImage(DataHandler.ImageCache.GetCurrentImage);

                // Check if its a corrupted image
                if (!DataHandler.ImageCache.Current.IsDisplayable(RootPathToImages))
                {
                    // TO DO AS WE MAY HAVE TO GET THE INDEX OF THE NEXT IN CYCLE IMAGE???
                    StatusBar.SetMessage("Difference can't be shown: the current file is likely missing or corrupted");
                }
                else
                {
                    StatusBar.ClearMessage();
                }
                return;
            }

            // Generate and cache difference image if needed
            if (DataHandler.ImageCache.GetCurrentImage == null)
            {
                ImageDifferenceResultEnum result = DataHandler.ImageCache.TryCalculateDifference();
                switch (result)
                {
                    case ImageDifferenceResultEnum.CurrentImageNotAvailable:
                    case ImageDifferenceResultEnum.NextImageNotAvailable:
                    case ImageDifferenceResultEnum.PreviousImageNotAvailable:
                    case ImageDifferenceResultEnum.NotCalculable:
                        StatusBar.SetMessage(
                            $"Difference can't be shown: the {(DataHandler.ImageCache.CurrentDifferenceState == ImageDifferenceEnum.Previous ? "previous" : "next")} file is a video, missing, corrupt, or a different size");
                        return;
                    case ImageDifferenceResultEnum.Success:
                        StatusBar.SetMessage(
                            $"Viewing difference from {(DataHandler.ImageCache.CurrentDifferenceState == ImageDifferenceEnum.Previous ? "previous" : "next")} file.");
                        break;
                    default:
                        throw new NotSupportedException($"Unhandled difference result {result}.");
                }
            }

            // display the differenced image
            // the magnifying glass always displays the original non-diferenced image so ImageToDisplay is updated and ImageToMagnify left unchnaged
            // this allows the user to examine any particular differenced area and see what it really looks like in the non-differenced image. 
            MarkableCanvas.SetDisplayImage(DataHandler.ImageCache.GetCurrentImage);
            StatusBar.SetMessage(
                $"Viewing difference from {(DataHandler.ImageCache.CurrentDifferenceState == ImageDifferenceEnum.Previous ? "previous" : "next")} file.");
        }
        #endregion

        #region Try View Combined Difference
        // View the differences between the current, previous, and next image
        private void TryViewCombinedDifference()
        {
            // Only allow differencing in single image mode.
            if (!IsDisplayingActiveSingleImageOrVideo())
            {
                return;
            }

            if (DataHandler == null ||
                DataHandler.ImageCache == null ||
                DataHandler.ImageCache.Current == null ||
                DataHandler.ImageCache.Current.IsVideo)
            {
                StatusBar.SetMessage("Combined differences can't be shown for videos, missing, or corrupt files");
                return;
            }

            // If we are in any state other than the unaltered state, go to the unaltered state, otherwise the combined diff state
            DataHandler.ImageCache.MoveToNextStateInCombinedDifferenceCycle();
            if (DataHandler.ImageCache.CurrentDifferenceState == ImageDifferenceEnum.Unaltered)
            {
                MarkableCanvas.SetDisplayImage(DataHandler.ImageCache.GetCurrentImage);
                StatusBar.ClearMessage();
                return;
            }

            // Generate and cache difference image if needed
            if (DataHandler.ImageCache.GetCurrentImage == null)
            {
                ImageDifferenceResultEnum result = DataHandler.ImageCache.TryCalculateCombinedDifference(State.DifferenceThreshold);
                switch (result)
                {
                    case ImageDifferenceResultEnum.CurrentImageNotAvailable:
                        StatusBar.SetMessage("Combined difference can't be shown: the current file is a video, missing, corrupt, or a different size");
                        return;
                    case ImageDifferenceResultEnum.NextImageNotAvailable:
                    case ImageDifferenceResultEnum.NotCalculable:
                    case ImageDifferenceResultEnum.PreviousImageNotAvailable:
                        StatusBar.SetMessage("Combined differences can't be shown: surrounding files include a video, missing, corrupt, or a different size file");
                        return;
                    case ImageDifferenceResultEnum.Success:
                        StatusBar.SetMessage("Viewing differences from both the next and previous files");
                        break;
                    default:
                        throw new NotSupportedException($"Unhandled combined difference result {result}.");
                }
            }

            // display differenced image
            MarkableCanvas.SetDisplayImage(DataHandler.ImageCache.GetCurrentImage);
            StatusBar.SetMessage("Viewing differences from both the next and previous files");
        }
        #endregion
    }
}
