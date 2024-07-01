using System;

namespace Timelapse.EventArguments
{
    // Whenever the ImageAdjuster raises an event, it requests the following image processing values 
    public class ImageAdjusterEventArgs : EventArgs
    {
        public int Brightness { get; set; }
        public int Contrast { get; set; }
        public bool DetectEdges { get; set; }
        public bool Sharpen { get; set; }
        public bool UseGamma { get; set; }
        public float GammaValue { get; set; }
        public bool OpenExternalViewer { get; set; }
        public bool ForceUpdate { get; set; }

        public ImageAdjusterEventArgs(int brightness, int contrast, bool sharpen, bool detectEdges, bool useGamma, float gammaValue, bool openExternalViewer, bool forceUpdate)
        {
            this.Brightness = brightness;
            this.Contrast = contrast;
            this.Sharpen = sharpen;
            this.DetectEdges = detectEdges;
            this.UseGamma = useGamma;
            this.GammaValue = gammaValue;
            this.OpenExternalViewer = openExternalViewer;
            this.ForceUpdate = forceUpdate;
        }
    }
}
