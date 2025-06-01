using System.Threading;
using Timelapse.Controls;
using Timelapse.State;

namespace Timelapse.DataStructures
{
    public static class GlobalReferences
    {
        /// <summary>
        /// Stores, as a global reference, pointers to commonly-needed instances or variables
        /// - Occassionaly, a class will have to access various instances or variables set elsewhere
        ///   Rather than do extensive refactoring, or to contort method calls to try to pass it as a parameter, we just make those available here.
        /// </summary>

        // The main Timelapse window instance
        public static TimelapseWindow MainWindow { get; set; }

        // The top level Busy CancelIndicator
        public static BusyCancelIndicator BusyCancelIndicator { get; set; }

        // The Cancelation token used by the top level BusyCancelIndicator
        // Note that this is the only instance of this
        public static CancellationTokenSource CancelTokenSource { get; set; }

        // TimelapseState instance
        public static TimelapseState TimelapseState { get; set; }

        // Whether or not detections exist
        public static bool DetectionsExists { get; set; }

        // Whether or not we should hid detections
        public static bool HideBoundingBoxes { get; set; } = false;
    }
}
