using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Timelapse.Util
{
    /// <summary>
    /// Timelapse and / or the Timelapse Editor is dependent on these files being present
    /// These methods check to see if they exist.
    /// </summary>
    public static class Dependencies
    {
        #region Private Lists of required files
        private static readonly List<string> CommonRequiredBinaries = new List<string>()
        {
            // Exiftool
            "exiftool(-k).exe",

            // ImageProcessor
            "ImageProcessor.dll",
            "System.ValueTuple.dll",
            "System.ValueTuple.xml",

            // MetadataExtractor
            "MetadataExtractor.dll",
            "MetadataExtractor.xml",
            "XmpCore.dll",
            
            //  Newtonsoft
            "Newtonsoft.Json.dll",

            // SQLite
            "System.Data.SQLite.dll",
            "System.Data.SQLite.xml",
            "x64/SQLite.Interop.dll",
            "x86/SQLite.Interop.dll",

            // Extended WPF toolkit
            "Xceed.Wpf.AvalonDock.dll",
            "Xceed.Wpf.Toolkit.dll",
        };

        private static readonly List<string> TimelapseRequiredBinaries = new List<string>()
        {
            "Microsoft.WindowsAPICodePack.dll", // required by Microsoft.WindowsAPICodePack.Shell.dll
            "Microsoft.WindowsAPICodePack.Shell.dll", // just for TimelapseWindow's use of CommonOpenFileDialog
            // For getting thumbnails from videos
            "ffmpeg.exe",
            "NReco.VideoConverter.dll"
        };

        private static readonly List<string> EditorRequiredBinaries = new List<string>()
        {
            "Timelapse2.exe"
        };
        #endregion

        #region Public Methods
        /// <summary>
        /// If any dependency files are missing, return false else true
        /// </summary>
        public static bool AreRequiredBinariesPresent(string applicationName, Assembly executingAssembly)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(executingAssembly, nameof(executingAssembly));

            string directoryContainingCurrentExecutable = Path.GetDirectoryName(executingAssembly.Location);
            foreach (string binaryName in Dependencies.CommonRequiredBinaries)
            {
                if (File.Exists(Path.Combine(directoryContainingCurrentExecutable, binaryName)) == false)
                {
                    return false;
                }
            }

            if (applicationName == Constant.VersionUpdates.ApplicationName)
            {
                foreach (string binaryName in Dependencies.TimelapseRequiredBinaries)
                {
                    if (File.Exists(Path.Combine(directoryContainingCurrentExecutable, binaryName)) == false)
                    {
                        return false;
                    }
                }
            }
            else
            {
                foreach (string binaryName in Dependencies.EditorRequiredBinaries)
                {
                    if (File.Exists(Path.Combine(directoryContainingCurrentExecutable, binaryName)) == false)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        #endregion
    }
}
