using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Timelapse.Controls;
using Timelapse.Dialog;
using Timelapse.Enums;
using Timelapse.EventArguments;
using Timelapse.Util;

namespace Timelapse.Images
{
    // This portion of the Markable Canvas 
    // - handles image procesing adjustments as requested by events sent via the ImageAdjuster.
    // - generates events indicating image state to be consumed by the Image Adjuster to adjust its own state (e.g., disabled, reset, etc).
    public partial class MarkableCanvas : Canvas
    {
        #region EventHandler definitions
        // Whenever an image state is changed, raise an event (to be consumed by ImageAdjuster)
        public event EventHandler<ImageStateEventArgs> ImageStateChanged; // raise when an image state is changed (to be consumed by ImageAdjuster)
        #endregion

        #region Private variables
        // State information - whether the current image is being processed
        private bool Processing;

        // When started, the timer tries to updates image processing to ensure that the last image processing values are applied
        private readonly DispatcherTimer timerImageProcessingUpdate = new DispatcherTimer();

        // image processing parameters
        private int contrast;
        private int brightness;
        private bool detectEdges;
        private bool sharpen;
        private bool useGamma;
        private float gammaValue;

        // We track the last parameters used, as if they haven't changed we won't update the image
        private int lastContrast;
        private int lastBrightness;
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
            this.timerImageProcessingUpdate.Interval = TimeSpan.FromSeconds(0.1);
            this.timerImageProcessingUpdate.Tick += this.TimerImageProcessingUpdate_Tick;
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
            if (String.IsNullOrEmpty(path))
            {
                // The file cannot be opened or is not displayable. 
                // Signal change in image state, which essentially says there is no displayable image to adjust (consumed by ImageAdjuster)
                this.OnImageStateChanged(new ImageStateEventArgs(false)); //  Signal change in image state (consumed by ImageAdjuster)
                return;
            }

            if (e.OpenExternalViewer)
            {
                // The event says to open an external photo viewer. Try to do so.
                // Note that we don't do any image processing on this event if if this is the case.
                if (ProcessExecution.TryProcessStart(path) == false)
                {
                    // Can't open the image file with an external view. Note that file must exist at this point as we checked for that above.
                    Dialogs.MarkableCanvasCantOpenExternalPhotoViewerDialog(Util.GlobalReferences.MainWindow, Path.GetExtension(path));
                }
                return;
            }

            // Process the image based on the current image processing arguments. 
            if (e.ForceUpdate == false && (e.Contrast == this.lastContrast && e.Brightness == this.lastBrightness && e.DetectEdges == this.lastDetectEdges && e.Sharpen == this.lastSharpen && e.UseGamma == this.lastUseGamma && e.GammaValue == this.lastGammaValue))
            {
                // If there is no change from the last time we processed an image, abort as it would not make any difference to what the user sees
                return;
            }
            this.contrast = e.Contrast;
            this.brightness = e.Brightness;
            this.detectEdges = e.DetectEdges;
            this.sharpen = e.Sharpen;
            this.useGamma = e.UseGamma;
            this.gammaValue = e.GammaValue;
            this.timerImageProcessingUpdate.Start();
            await UpdateAndProcessImage().ConfigureAwait(true);
        }

        // Because an event may come in while an image is being processed, the timer
        // will try to continue the processing the image with the latest image processing parameters (if any) 
        private async void TimerImageProcessingUpdate_Tick(object sender, EventArgs e)
        {
            if (this.Processing)
            {
                return;
            }
            if (this.contrast != this.lastContrast || this.brightness != this.lastBrightness || this.detectEdges != this.lastDetectEdges || this.sharpen != this.lastSharpen || this.lastUseGamma != this.useGamma || this.lastGammaValue != this.gammaValue)
            {
                // Update the image as at least one parameter has changed (which will affect the image's appearance)
                await this.UpdateAndProcessImage().ConfigureAwait(true);
            }
            this.timerImageProcessingUpdate.Stop();
        }

        // Update the image according to the image processing parameters.
        private async Task UpdateAndProcessImage()
        {
            // If its processing the image, try again later (via the time),
            if (this.Processing)
            {
                return;
            }
            try
            {
                string path = DataEntryHandler.TryGetFilePathFromGlobalDataHandler(); ;
                if (String.IsNullOrEmpty(path))
                {
                    // If we cannot get a valid file, there is no image to manipulate. 
                    // So abort and signal a change in image state that says there is no displayable image to adjust (consumed by ImageAdjuster)
                    this.OnImageStateChanged(new ImageStateEventArgs(false));
                }

                // Set the state to Processing is used to indicate that other attempts to process the image should be aborted util this is done.
                this.Processing = true;
                using (MemoryStream imageStream = new MemoryStream(File.ReadAllBytes(path)))
                {
                    // Remember the currently selected image processing states, so we can compare them later for changes
                    this.lastBrightness = this.brightness;
                    this.lastContrast = this.contrast;
                    this.lastSharpen = this.sharpen;
                    this.lastDetectEdges = this.detectEdges;
                    this.lastUseGamma = this.useGamma;
                    this.lastGammaValue = this.gammaValue;
                    BitmapFrame bf = await ImageProcess.StreamToImageProcessedBitmap(imageStream, this.brightness, this.contrast, this.sharpen, this.detectEdges, this.useGamma, this.gammaValue).ConfigureAwait(true);
                    if (bf != null)
                    {
                        this.ImageToDisplay.Source = await ImageProcess.StreamToImageProcessedBitmap(imageStream, this.brightness, this.contrast, this.sharpen, this.detectEdges, this.useGamma, this.gammaValue).ConfigureAwait(true);
                    }
                }
            }
            catch
            {
                // We failed on this image. To avoid this happening again,
                // Signal change in image state, which essentially says there is no adjustable image (consumed by ImageAdjuster)
                this.OnImageStateChanged(new ImageStateEventArgs(false));
            }
            this.Processing = false;
        }
        #endregion

        #region Generate ImageStateChange event
        // Explicit check the current status of the image state and generate an event to reflect that.
        // Typically used when the image adjustment window is opened for the first time, as the markable canvas needs to signal its state to it.
        public void GenerateImageStateChangeEventToReflectCurrentStatus()
        {
            if (this.ThumbnailGrid.IsGridActive)
            {
                // In the overview
                this.GenerateImageStateChangeEvent(false); //  Signal change in image state (consumed by ImageAdjuser)
                return;
            }
            ImageCache imageCache = Util.GlobalReferences.MainWindow?.DataHandler?.ImageCache;
            if (imageCache != null)
            {
                if (imageCache.Current?.IsVideo == true)
                {
                    // Its a video
                    this.GenerateImageStateChangeEvent(false); //  Signal change in image state (consumed by ImageAdjuser)
                    return;
                }
                // Its a primary image, but we need to consider whether we are in either the differencing state or displaying a placeholder image
                bool isImageView = imageCache.CurrentDifferenceState == ImageDifferenceEnum.Unaltered && this.ImageToDisplay.Source != Constant.ImageValues.Corrupt.Value && this.ImageToDisplay.Source != Constant.ImageValues.FileNoLongerAvailable.Value;
                this.GenerateImageStateChangeEvent(isImageView); //  Signal change in image state (consumed by ImageAdjuser)
            }
        }

        // Generate an event indicating the image state. To be consumed by the Image Adjuster to adjust its own state (e.g., disabled, reset, etc).
        private void GenerateImageStateChangeEvent(bool isImageView)
        {
            this.OnImageStateChanged(new ImageStateEventArgs(isImageView)); //  Signal change in image state (consumed by ImageAdjuster, but only if its visible)
        }

        protected virtual void OnImageStateChanged(ImageStateEventArgs e)
        {
            ImageStateChanged?.Invoke(this, e);
        }
        #endregion
    }
}
