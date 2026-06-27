using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using Timelapse.DebuggingSupport;

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
                AppLog.Warning("processStartInfo is null — this should not happen.");
                return false;
            }

            using Process process = new();
            process.StartInfo = processStartInfo;
            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                // A noop so we catch it cleanly but still leave the dialog running
                AppLog.Warning($"Failed to start process '{processStartInfo.FileName}'. Possible causes: executable not found, invalid path, or permissions error.", ex);
                SystemSounds.Beep.Play();
                return false;
            }
            return true;
        }

        /// Try to open the uri with whatever default program is used to open that file
        /// /// <param name="uri">should contain a valid Uri</param>
        /// <returns>true/false if the process started or not</returns>
        public static bool TryProcessStart(Uri uri)
        {
            if (uri == null)
            {
                AppLog.Warning("uri is null — this should not happen.");
                return false;
            }
            ProcessStartInfo processStartInfo = new(uri.AbsoluteUri)
            {
                UseShellExecute = true
            };
            return TryProcessStart(processStartInfo);
        }

        /// Try to open the filepath with whatever default program is used to openn that file
        /// <param name="filePath">should contain a valid file path</param>
        /// <returns>true/false if the process started or not</returns>
        public static bool TryProcessStart(string filePath)
        {
            if (File.Exists(filePath) == false)
            {
                AppLog.Warning($"Cannot start process — file does not exist: '{filePath}'.");
                return false;
            }
            ProcessStartInfo processStartInfo = new()
            {
                FileName = filePath,
                UseShellExecute = true

            };
            return TryProcessStart(processStartInfo);
        }

        // Run an arbitrary command within an (invisible) cmd window.
        public static bool TryProcessRunCommand(string cmd)
        {
            //cmd = @"/c echo foo";
            using Process process = new()
            {
                StartInfo = new()
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = false,
                    FileName = "cmd.exe",
                    Arguments = cmd
                }
            };
            try
            {
                return process.Start();
            }
            catch (Exception ex)
            {
                AppLog.Warning($"Failed to run command '{cmd}'. Possible causes: cmd.exe not found, or permissions error.", ex);
                return false;
            }
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
                AppLog.Warning($"Cannot open File Explorer — folder does not exist: '{folderPath}'.");
                return false;
            }
            ProcessStartInfo processStartInfo = new()
            {
                FileName = "explorer.exe",
                Arguments = folderPath
            };
            return TryProcessStart(processStartInfo);
        }

        /// Try to open file explorer with the file selected.
        /// <param name="filePath">should contain a valid file path</param>
        /// <returns>true/false if the process started or not</returns>
        public static bool TryProcessStartUsingFileExplorerToSelectFile(string filePath)
        {
            if (File.Exists(filePath) == false)
            {
                AppLog.Warning($"Cannot open File Explorer — file does not exist: '{filePath}'.");
                return false;
            }

            ProcessStartInfo processStartInfo = new()
            {
                FileName = "explorer.exe",
                Arguments = $"/e, /select, \"{filePath}\""
            };
            return TryProcessStart(processStartInfo);
        }
        #endregion
    }
}
