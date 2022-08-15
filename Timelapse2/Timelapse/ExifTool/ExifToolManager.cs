using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Timelapse.ExifTool
{
    // The ExifToolManager gives a high level globally accessible management to the ExifTool, which is invoked as an external process.

    // ExifTool has a 'stay open'process, which means that once started, it can accept and process multiple commands, thus
    // avoiding the startup overhead when getting metadata from multiple files. However, it doesn't always exit gracefully.

    // Importantly, this manager puts in redundant checks to start and stop the tool as needed (including killing any lingering ExifTool processes)
    // as well as bundle the commonly used Exiftool methods and data structures into one place.

    // Note: It could be that other ExiftoolWrappers are more efficient than the one used here.
    // If so, we should be able to transparently replace the wrapper used by the manager
    public class ExifToolManager : IDisposable
    {
        #region Private Properties
        // Used to check and kill lingering Exiftool processes
        private DispatcherTimer KillTimer;

        // The ExifTool Wrapper is a 3rd party wrapper (included here as source) that gives access to the ExifTool.exe
        // It is private to ensure that all access to it is done through the ExifToolManager
        private ExifToolWrapper ExifTool { get; set; }
        #endregion

        #region Public Properties
        // Returns true if the exif tool is started and ready.
        // Note that its not foolproof: if we call it immediately after we try to stop the tool, it can still report it as started
        // This could actually be private, as other classes don't really have to test it, as invoking other methods will try to start or stop it as needed
        public bool IsStarted
        {
            get
            {
                if (this.ExifTool == null) return false;
                return this.ExifTool.Status == ExifToolWrapper.ExeStatus.Ready;
            }
        }
        #endregion

        #region Initialization
        public ExifToolManager()
        {
        }
        #endregion Initialization

        #region Start ExifTool
        public void StartIfNotAlreadyStarted()
        {
            // Start to exiftool if it is not already started
            if (this.ExifTool == null)
            {
                // Yes, start  exiftool
                this.ExifTool = new ExifToolWrapper();
                this.ExifTool.Start();
            }
        }
        #endregion

        #region Stop the Exiftool, including killing any lingering ExifTool processes 
        public void Stop()
        {
            try
            {
                // Check to see if the exiftool actually needs stopping
                if (this.ExifTool != null)
                {
                    // If the ExifTool is already stopped, this should still work without any consequences.
                    Task.Run(() => this.ExifTool.Stop());

                    // Sometimes Exiftool process seems to linger. This is a further way to ensure that those processes are killed.
                    if (this.KillTimer == null)
                    {
                        this.KillTimer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(1) };
                        this.KillTimer.Tick += this.KillTimer_Tick;
                    }
                    this.KillTimer.Start();
                }
            }
            catch
            {
                // System.Diagnostics.Debug.Print("Catch in ExifToolManager:Stop");
            }
        }

        // Kill Exiftool processes.
        private void KillTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // Debug.Print("In kill timer");
                foreach (var process in Process.GetProcessesByName("exiftool(-k)"))
                {
                    // System.Diagnostics.Debug.Print(process.ProcessName);
                    process.Kill();
                }
                KillTimer.Stop();
                this.ExifTool = null;
            }
            catch
            {
                // System.Diagnostics.Debug.Print("ExifToolManager:KillTimer");
            }
        }
        #endregion

        #region Fetch ExifData
        // Fetch the particular Exif data associated with the file identified in filepath
        // A dictionary is returned, where
        // Key: the Exif tag (without the directory)
        // Value: the value associated with that Exif tag
        // If the file does not exist or is not accessible, the dictionary will have a count of 0

        // Version 1: Return all Exif tag/value data associated with the file identified in filepath
        public Dictionary<string, string> FetchExifFrom(string filepath)
        {
            this.StartIfNotAlreadyStarted();
            return this.ExifTool.FetchExifFrom(filepath);
        }
        // Version 2: specifies the tags of interest, where only those tag/values are returned.
        // If the tag does not exist, the returned data structure will not contain that tag
        public Dictionary<string, string> FetchExifFrom(string filepath, string[] tags)
        {
            this.StartIfNotAlreadyStarted();
            return this.ExifTool.FetchExifFrom(filepath, tags);
        }
        #endregion

        #region Disposing
        // To follow design pattern in  CA1001 Types that own disposable fields should be disposable
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose managed resources
                if (this.ExifTool != null)
                {
                    this.ExifTool.Stop();
                    // Stop  kills the process, but lets dispose the exif tool anyways
                    this.ExifTool.Dispose();
                }
            }
            // free native resources
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
