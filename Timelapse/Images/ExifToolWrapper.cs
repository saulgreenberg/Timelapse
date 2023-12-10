/*
derived from the v2 C# wrapper for Exif Tool by Phil Harvey, retrieved from http://u88.n24.queensu.ca/exiftool/forum/index.php/topic,5262.0.html
see also http://www.sno.phy.queensu.ca/~phil/exiftool/

bug fixes and StyleCop cleanup pass applied
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Timelapse.Util;

namespace Timelapse.Images
{
    public class ExifToolWrapper : IDisposable
    {
        // must be kept in sync with Constants.Time.DateTimeExifToolFormat, -g for groups
        private const string Arguments = "-fast -m -q -q -stay_open True -@ - -common_args -d \"%Y:%m:%d %H:%M:%S\" -c \"%d %d %.6f\" -t";
        private const string ExeName = "exiftool(-k).exe";
        private static readonly object LockObj = new object();
        private static readonly int[] OrientationPositions = { 1, 6, 3, 8 };

        private readonly StringBuilder output = new StringBuilder();
        private readonly ProcessStartInfo processStartInfo;
        private readonly ManualResetEvent waitHandle = new ManualResetEvent(true);

        private bool disposed = false;
        private int commandCount = 1;
        private Process exifTool = null;
        private bool stopRequested = false;

        public enum Statuses
        {
            Stopped,
            Starting,
            Ready,
            Stopping
        }

        public string Exe { get; private set; }
        public string ExiftoolVersion { get; private set; }
        public bool Resurrect { get; set; }
        public Statuses Status { get; private set; }

        public ExifToolWrapper(string path = null)
        {
            this.Resurrect = true;

            this.Exe = String.IsNullOrEmpty(path) ? Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), ExeName) : path;
            if (!File.Exists(this.Exe))
            {
                throw new ExifToolException(ExeName + " not found");
            }

            this.processStartInfo = new ProcessStartInfo
            {
                FileName = this.Exe,
                Arguments = Arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true
            };

            this.Status = Statuses.Stopped;
            this.stopRequested = false;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            Debug.Assert(this.Status == Statuses.Ready || this.Status == Statuses.Stopped, "Invalid state");

            this.Stop();
            this.waitHandle.Dispose();

            this.disposed = true;
        }

        public Dictionary<string, string> FetchExifFrom(string path, IEnumerable<string> tagsToKeep = null, bool keepKeysWithEmptyValues = true)
        {
            Dictionary<string, string> res = new Dictionary<string, string>();

            bool filter = tagsToKeep != null && tagsToKeep.Any();
            string lines = this.SendCommand(path);
            foreach (string line in lines.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = line.Split('\t');

                // if unexpectedly passed a directory ExifTool will list results for all files in the directory
                // this results in lines containing "==== <directory> name", "directories scanned", etc. which do not
                // conform to the tab separated format
                if (kv.Length != 2)
                {
                    throw new FormatException(String.Format("Expected line returned from {0} to be of the form <key>\t<value> but encountered the line {1} instead.", this.Exe, line));
                }

                if (kv.Length != 2 || (!keepKeysWithEmptyValues && String.IsNullOrEmpty(kv[1])))
                {
                    continue;
                }
                if (filter && !tagsToKeep.Contains(kv[0]))
                {
                    continue;
                }

                res[kv[0]] = kv[1];
            }

            return res;
        }

        public string SendCommand(string cmd, params object[] args)
        {
            if (this.Status != Statuses.Ready)
            {
                throw new ExifToolException("Process must be ready");
            }

            string exifToolOutput;
            lock (LockObj)
            {
                this.waitHandle.Reset();
                this.exifTool.StandardInput.WriteLine("{0}\n-execute{1}", args.Length == 0 ? cmd : String.Format(cmd, args), this.commandCount);
                this.waitHandle.WaitOne();

                this.commandCount++;

                exifToolOutput = this.output.ToString();
                this.output.Clear();
            }

            return exifToolOutput;
        }

        public void Start()
        {
            if (this.Status != Statuses.Stopped)
            {
                throw new ExifToolException("Process is not stopped");
            }

            lock (this.processStartInfo)
            {
                if (this.stopRequested)
                {
                    return;
                }

                this.Status = Statuses.Starting;

                this.exifTool = new Process { StartInfo = this.processStartInfo, EnableRaisingEvents = true };
                this.exifTool.OutputDataReceived += this.OnOutputDataReceived;
                this.exifTool.Exited += this.OnExifToolExited;
                this.exifTool.Start();

                this.exifTool.BeginOutputReadLine();

                this.waitHandle.Reset();
                this.exifTool.StandardInput.WriteLine("-ver\n-execute0000");
                this.waitHandle.WaitOne();

                this.Status = Statuses.Ready;
            }
        }

        public void Stop()
        {
            lock (this.processStartInfo)
            {
                if (this.Status != Statuses.Ready)
                {
                    throw new ExifToolException("Process must be ready");
                }

                this.stopRequested = true;
                this.Status = Statuses.Stopping;
                this.waitHandle.Reset();
                // tell ExifTool to exit
                this.exifTool.StandardInput.WriteLine("-stay_open\nFalse\n");
                // ExifTool responds with -- press RETURN --
                this.exifTool.StandardInput.WriteLine(String.Empty);
                if (!this.waitHandle.WaitOne(TimeSpan.FromSeconds(5)))
                {
                    if (this.exifTool != null)
                    {
                        // silently swallow any eventual exception
                        try
                        {
                            this.exifTool.Kill();
                            this.exifTool.WaitForExit(2000);
                            this.exifTool.Dispose();
                        }
                        catch (Exception exception)
                        {
                            Utilities.PrintFailure(String.Format("Shutdown of ExifTool failed. {0}", exception.ToString()));
                        }

                        this.exifTool = null;
                    }

                    this.Status = Statuses.Stopped;
                }
            }
        }

        private void OnExifToolExited(object sender, EventArgs e)
        {
            if (this.exifTool != null)
            {
                this.exifTool.Dispose();
                this.exifTool = null;
            }
            this.Status = Statuses.Stopped;

            this.waitHandle.Set();

            if (!this.stopRequested && this.Resurrect)
            {
                this.Start();
            }
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (String.IsNullOrEmpty(e.Data))
            {
                return;
            }

            if (this.Status == Statuses.Starting)
            {
                this.ExiftoolVersion = e.Data;
                this.waitHandle.Set();
            }
            else
            {
                if (e.Data.ToLower() == String.Format("{{ready{0}}}", this.commandCount))
                {
                        this.waitHandle.Set();
                    }
                else
                {
                    this.output.AppendLine(e.Data);
                }
            }
        }
    }
}
