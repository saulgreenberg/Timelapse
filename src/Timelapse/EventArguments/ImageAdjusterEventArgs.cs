using System;

namespace Timelapse.EventArguments
{
    // Whenever the ImageAdjuster raises an event, it requests the following image processing values 
    public class ImageAdjusterEventArgs(double brightness, int contrast, bool sharpen, bool detectEdges, bool useGamma, float gammaValue, bool openExternalViewer, bool forceUpdate)
        : EventArgs
    {
        public double Brightness { get; set; } = brightness;
        public int Contrast { get; set; } = contrast;
        public bool DetectEdges { get; set; } = detectEdges;
        public bool Sharpen { get; set; } = sharpen;
        public bool UseGamma { get; set; } = useGamma;
        public float GammaValue { get; set; } = gammaValue;
        public bool OpenExternalViewer { get; set; } = openExternalViewer;
        public bool ForceUpdate { get; set; } = forceUpdate;
    }
}
