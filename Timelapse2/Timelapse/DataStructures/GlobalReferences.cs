using Timelapse.Controls;

namespace Timelapse.Util
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

        // TimelapseState instance
        public static TimelapseState TimelapseState { get; set; }

        // Whether or not detections exist
        public static bool DetectionsExists { get; set; }

        public static bool UseClassifications { get; set; } = true;
    }
}
