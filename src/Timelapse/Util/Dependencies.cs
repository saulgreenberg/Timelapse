using System.Collections.Generic;
using System.IO;
using System.Reflection;
using File = System.IO.File;

namespace Timelapse.Util
{
    /// <summary>
    /// Timelapse and / or the Timelapse Editor is dependent on various files being present
    /// These methods check to see if a few of them exist as a reasonable test
    /// to see if the application was installed correctly.
    /// </summary>
    public static class Dependencies
    {
        #region Private patial lists of required files, used to see if at least some of the required dependencies are present
        private static readonly List<string> SelectedRequiredBinaries =
        [
            "exiftool(-k).exe",

            // MetadataExtractor
            "MetadataExtractor.dll",
            "XmpCore.dll",

            //  Newtonsoft
            "Newtonsoft.Json.dll",

            // SixLabors ImageSharp
            "SixLabors.ImageSharp.dll",

            // SQLite
            "System.Data.SQLite.dll",

            // TimelapseWpf.Toolkit, a customized DotNetProjects.Wpf.Extended Toolkit (MS-PL license fork of Xceed.Wpf.Toolkit)
            "TimelapseWpf.Toolkit.dll",

            // AvalonDock (Dirkster fork of Xceed.Wpf.Toolkit)
            "AvalonDock.dll",

            "Microsoft.WindowsAPICodePack.dll", // required by Microsoft.WindowsAPICodePack.Shell.dll
            "Microsoft.WindowsAPICodePack.Shell.dll", // just for TimelapseWindow's use of CommonOpenFileDialog
            
            // FFMPEG (for video and thumbnail creation)
            "ffmpeg.exe",
            "NReco.VideoConverter.dll"

        ];
        #endregion

        #region Public Methods
        /// <summary>
        /// If any dependency files are missing, return false else true
        /// </summary>
        public static bool AreRequiredBinariesPresent(Assembly executingAssembly, out string missingAssemblies)
        {
            missingAssemblies = string.Empty;
            bool binariesAllPresent = true;

            // Check the arguments for null 
            ThrowIf.IsNullArgument(executingAssembly, nameof(executingAssembly));

            string directoryContainingCurrentExecutable = Path.GetDirectoryName(executingAssembly.Location);
            if (directoryContainingCurrentExecutable == null)
            {
                // If the path consists of a root directory, such as "c:\", null is returned.
                // That shouldn't happen
                return false;
            }
            foreach (string binaryName in SelectedRequiredBinaries)
            {
                if (File.Exists(Path.Combine(directoryContainingCurrentExecutable, binaryName)) == false)
                {
                    binariesAllPresent = false;
                    missingAssemblies += " " + binaryName;
                }
            }

            return binariesAllPresent;
        }
        #endregion
    }
}
