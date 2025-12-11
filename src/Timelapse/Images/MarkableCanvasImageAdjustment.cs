using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Timelapse.Constant;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.EventArguments;
using Timelapse.Util;
using File = System.IO.File;

namespace Timelapse.Images
{
    // This portion of the Markable Canvas 
    // - handles image procesing adjustments as requested by events sent via the ImageAdjuster.
    // - generates events indicating image state to be consumed by the Image Adjuster to adjust its own state (e.g., disabled, reset, etc).
    public partial class MarkableCanvas
    {
        #region EventHandler definitions
        // Whenever an image state is changed, raise an event (to be consumed by ImageAdjuster)
        public event EventHandler<ImageStateEventArgs> ImageStateChanged; // raise when an image state is changed (to be consumed by ImageAdjuster)
        #endregion

        #region Private variables
        // State information - whether the current image is being processed
        private bool Processing;

        // When started, the timer tries to updates image processing to ensure that the last image processing values are applied
        private readonly DispatcherTimer timerImageProcessingUpdate = new();

        // image processing parameters
        private int contrast;
        private double brightness;
        private bool detectEdges;
        private bool sharpen;
        private bool useGamma;
        private float gammaValue;

        // We track the last parameters used, as if they haven't changed we won't update the image
        private int lastContrast;
        private double lastBrightness;
        private bool lastDetectEdges;
        private bool lastSharpen;
        private bool lastUseGamma;
        private float lastGammaValue = 1;
        #endregion

        #region Consume and handle image processing events
        // This should be invoked by the MarkableCanvas Constructor to initialize aspects of this partial class
        private void InitializeImageAdjustment()
        {
            // When started, ensures that the final image processing parameters are applied to the image
            timerImageProcessingUpdate.Interval = TimeSpan.FromSeconds(0.1);
            timerImageProcessingUpdate.Tick += TimerImageProcessingUpdate_Tick;
        }

        // Receive an event containing new image processing parameters.
        // Store these parameters and then try to update the image
        public async void AdjustImage_EventHandler(object sender, ImageAdjusterEventArgs e)
        {
            if (e == null)
            {
                // Shouldn't happen, but...
                return;
            }

            string path = DataEntryHandler.TryGetFilePathFromGlobalDataHandler();
            if (string.IsNullOrEmpty(path))
            {
                // The file cannot be opened or is not displayable. 
                // Signal change in image state, which essentially says there is no displayable image to adjust (consumed by ImageAdjuster)
                OnImageStateChanged(new(false)); //  Signal change in image state (consumed by ImageAdjuster)
                return;
            }

            if (e.OpenExternalViewer)
            {
                // The event says to open an external photo viewer. Try to do so.
                // Note that we don't do any image processing on this event if if this is the case.
                if (ProcessExecution.TryProcessStart(path) == false)
                {
                    // Can't open the image file with an external view. Note that file must exist at this point as we checked for that above.
                    Dialogs.MarkableCanvasCantOpenExternalPhotoViewerDialog(GlobalReferences.MainWindow, Path.GetExtension(path));
                }
                return;
            }

            // Process the image based on the current image processing arguments. 
            if (e.ForceUpdate == false && (e.Contrast == lastContrast && Math.Abs(e.Brightness - lastBrightness) < .001 && e.DetectEdges == lastDetectEdges && e.Sharpen == lastSharpen && e.UseGamma == lastUseGamma && Math.Abs(e.GammaValue - lastGammaValue) < .0001))
            {
                // If there is no change from the last time we processed an image, abort as it would not make any difference to what the user sees
                return;
            }
            contrast = e.Contrast;
            brightness = e.Brightness;
            detectEdges = e.DetectEdges;
            sharpen = e.Sharpen;
            useGamma = e.UseGamma;
            gammaValue = e.GammaValue;
            timerImageProcessingUpdate.Start();
            await UpdateAndProcessImage().ConfigureAwait(true);
        }

        // Because an event may come in while an image is being processed, the timer
        // will try to continue the processing the image with the latest image processing parameters (if any) 
        private async void TimerImageProcessingUpdate_Tick(object sender, EventArgs e)
        {
            if (Processing)
            {
                return;
            }
            if (contrast != lastContrast || Math.Abs(brightness - lastBrightness) > .01 || detectEdges != lastDetectEdges || sharpen != lastSharpen || lastUseGamma != useGamma || Math.Abs(lastGammaValue - gammaValue) > .0001)
            {
                // Update the image as at least one parameter has changed (which will affect the image's appearance)
                await UpdateAndProcessImage().ConfigureAwait(true);
            }
            timerImageProcessingUpdate.Stop();
        }

        // Update the image according to the image processing parameters.
        private async Task UpdateAndProcessImage()
        {
            // If its processing the image, try again later (via the time),
            if (Processing)
            {
                return;
            }
            try
            {
                string path = DataEntryHandler.TryGetFilePathFromGlobalDataHandler();
                if (string.IsNullOrEmpty(path))
                {
                    // If we cannot get a valid file, there is no image to manipulate. 
                    // So abort and signal a change in image state that says there is no displayable image to adjust (consumed by ImageAdjuster)
                    OnImageStateChanged(new(false));
                    Processing = false;
                    return;
                }

                // Get the EXIF orientation from the file, if any, in various formats, and pass it on as an argument so that the image can be rotated if needed.
                // Note that if the exif isn't present, this method will set them all to 0 and return false.
                BitmapUtilities.MetadataExtractorGetOrientation(path, out int angle, out _, out RotateFlipType rotateFlip);

                // Set the state to Processing is used to indicate that other attempts to process the image should be aborted util this is done.
                Processing = true;
                using MemoryStream imageStream = new(await File.ReadAllBytesAsync(path));
                // Remember the currently selected image processing states, so we can compare them later for changes
                lastBrightness = brightness;
                lastContrast = contrast;
                lastSharpen = sharpen;
                lastDetectEdges = detectEdges;
                lastUseGamma = useGamma;
                lastGammaValue = gammaValue;
                BitmapFrame bf = await ImageProcess.StreamToImageProcessedBitmap(imageStream, brightness, contrast, sharpen, detectEdges, useGamma, gammaValue, angle, rotateFlip).ConfigureAwait(true);
                if (bf != null)
                {
                    ImageToDisplay.Source = bf;
                    // In an earlier version, I was I was invoking StreamToImageProcessedBitmap twice, but am not sure why. So I commented it out but left it here just in case there was a reason for this.
                    // await ImageProcess.StreamToImageProcessedBitmap(imageStream, this.brightness, this.contrast, this.sharpen, this.detectEdges, this.useGamma, this.gammaValue).ConfigureAwait(true);
                }
            }
            catch
            {
                // We failed on this image. To avoid this happening again,
                // Signal change in image state, which essentially says there is no adjustable image (consumed by ImageAdjuster)
                OnImageStateChanged(new(false));
            }
            Processing = false;
        }
        #endregion

        #region Generate ImageStateChange event
        // Explicit check the current status of the image state and generate an event to reflect that.
        // Typically used when the image adjustment window is opened for the first time, as the markable canvas needs to signal its state to it.
        public void GenerateImageStateChangeEventToReflectCurrentStatus()
        {
            if (ThumbnailGrid.IsGridActive)
            {
                // In the overview
                GenerateImageStateChangeEvent(false); //  Signal change in image state (consumed by ImageAdjuser)
                return;
            }
            ImageCache imageCache = GlobalReferences.MainWindow?.DataHandler?.ImageCache;
            if (imageCache != null)
            {
                if (imageCache.Current?.IsVideo == true)
                {
                    // Its a video
                    GenerateImageStateChangeEvent(false); //  Signal change in image state (consumed by ImageAdjuser)
                    return;
                }
                // Its a primary image, but we need to consider whether we are in either the differencing state or displaying a placeholder image
                bool isImageView = imageCache.CurrentDifferenceState == ImageDifferenceEnum.Unaltered && ImageToDisplay.Source != ImageValues.Corrupt.Value && ImageToDisplay.Source != ImageValues.FileNoLongerAvailable.Value;
                GenerateImageStateChangeEvent(isImageView); //  Signal change in image state (consumed by ImageAdjuser)
            }
        }

        // Generate an event indicating the image state. To be consumed by the Image Adjuster to adjust its own state (e.g., disabled, reset, etc).
        private void GenerateImageStateChangeEvent(bool isImageView)
        {
            OnImageStateChanged(new(isImageView)); //  Signal change in image state (consumed by ImageAdjuster, but only if its visible)
        }

        protected virtual void OnImageStateChanged(ImageStateEventArgs e)
        {
            ImageStateChanged?.Invoke(this, e);
        }
        #endregion
    }
}
