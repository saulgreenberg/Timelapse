using System;
using System.Diagnostics;
using System.IO;

namespace Timelapse.Util
{
    /// <summary>
    /// Try to start a default external process on the given file.
    /// Each call type returns true / false if the Process successfully started
    /// </summary>
    public static class ProcessExecution
    {
        #region Public Methods - Try Process Start - Various forms
        /// <param name="processStartInfo">should contain the necessary information to configure the process</param>
        /// <returns>true/false if the process started or not</returns>
        public static bool TryProcessStart(ProcessStartInfo processStartInfo)
        {
            if (processStartInfo == null)
            {
                return false;
            }
            using (Process process = new Process())
            {
                process.StartInfo = processStartInfo;
                try
                {
                    process.Start();
                }
                catch
                {
                    // Error. A noop so we catch it cleanly but still leave the dialog running
                    System.Media.SystemSounds.Beep.Play();
                    return false;
                }
                return true;
            }
        }

        /// Try to open the uri with whatever default program is used to openn that file
        /// /// <param name="uri">should contain a valid Uri</param>
        /// <returns>true/false if the process started or not</returns>
        public static bool TryProcessStart(Uri uri)
        {
            if (uri == null)
            {
                return false;
            }
            ProcessStartInfo processStartInfo = new ProcessStartInfo(uri.AbsoluteUri);
            return ProcessExecution.TryProcessStart(processStartInfo);
        }

        /// Try to open the filepath with whatever default program is used to openn that file
        /// <param name="filePath">should contain a valid file path</param>
        /// <returns>true/false if the process started or not</returns>
        public static bool TryProcessStart(string filePath)
        {
            if (File.Exists(filePath) == false)
            {
                // Don't even try to start the process if the file doesn't exist.
                return false;
            }
            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = filePath
            };
            return ProcessExecution.TryProcessStart(processStartInfo);
        }
        #endregion

        #region Public Methods - Try Process Start using File Explorer
        /// Try to open Windows file explorer on the provided folder path.
        /// <param name="folderPath">should contain a valid file path, as its otherwise aborted</param>
        /// <returns>true/false if the process started or not</returns>
        public static bool TryProcessStartUsingFileExplorerOnFolder(string folderPath)
        {
            if (Directory.Exists(folderPath) == false)
            {
                // Don't even try to start the process if the file doesn't exist.
                return false;
            }
            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = folderPath
            };
            return ProcessExecution.TryProcessStart(processStartInfo);
        }

        /// Try to open file explorer with the file selected.
        /// <param name="filePath">should contain a valid file path</param>
        /// <returns>true/false if the process started or not</returns>
        public static bool TryProcessStartUsingFileExplorerToSelectFile(string filePath)
        {
            if (File.Exists(filePath) == false)
            {
                // Don't even try to start the process if the file doesn't exist.
                return false;
            }

            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/e, /select, \"{filePath}\""
            };
            return ProcessExecution.TryProcessStart(processStartInfo);
        }
        #endregion
    }
}
