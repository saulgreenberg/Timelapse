using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Timelapse.DebuggingSupport;
using Timelapse.Util;

namespace Timelapse.ExifTool
{
    /// <summary>
    /// This code was imported and slightly modified from a Github project see  http://brain2cpu.com/devtools.html
    /// </summary>
//#pragma warning disable CA1815 // Override equals and operator equals on value types
    public struct ExifToolResponse
    //#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public bool IsSuccess { get; }
        public string Result { get; }

        public ExifToolResponse(string response)
        {
            // Check the arguments for null 
            if (response == null)
            {
                // this should not happen
                TracePrint.StackTrace(1);
                // throw new ArgumentNullException(nameof(response));
                // Treat it as a failure case?
                this.IsSuccess = false;
                this.Result = string.Empty;
                return;
            }

            this.IsSuccess = response.ToLowerInvariant().Contains(ExifToolWrapper.SuccessMessage);
            this.Result = response;
        }

        public ExifToolResponse(bool b, string r)
        {
            this.IsSuccess = b;
            this.Result = r;
        }

        //to use ExifToolResponse directly in if (discarding response)
        //#pragma warning disable CA2225 // Operator overloads have named alternates. Reason: don't think this is necessary
        public static implicit operator bool(ExifToolResponse r) => r.IsSuccess;
        //#pragma warning restore CA2225 // Operator overloads have named alternates
    }

    public sealed class ExifToolWrapper : IDisposable
    {
        public string ExifToolPath { get; }
        public string ExifToolVersion { get; private set; }

        private const string ExeName = "exiftool(-k).exe";
        private const string Arguments = "-fast  -m -q -q -stay_open True -@ - -common_args -d \"%Y.%m.%d %H:%M:%S\" -c \"%d %d %.6f\" -t";   //-g for groups
        private const string ArgumentsFaster = "-fast2 -m -q -q -stay_open True -@ - -common_args -d \"%Y.%m.%d %H:%M:%S\" -c \"%d %d %.6f\" -t";
        private const string ExitMessage = "-- press RETURN --";
        internal const string SuccessMessage = "1 image files updated";

        //-fast2 also causes exiftool to avoid extracting any EXIF MakerNote information

        public double SecondsToWaitForError { get; set; } = 1;
        public double SecondsToWaitForStop { get; set; } = 5;

        public enum ExeStatus { Stopped, Starting, Ready, Stopping }
        public ExeStatus Status { get; private set; }

        //ViaFile: for every command an argument file is created, it works for files with accented characters but it is slower
        public enum CommunicationMethod { Auto, Direct, ViaFile }
        public CommunicationMethod Method { get; set; } = CommunicationMethod.Auto;

        public bool Resurrect { get; set; } = true;

        private bool _stopRequested;

        private int _cmdCnt;
        private readonly StringBuilder _output = new StringBuilder();
        private readonly StringBuilder _error = new StringBuilder();

        private readonly ProcessStartInfo _psi;
        private Process _proc;

        private readonly ManualResetEvent _waitHandle = new ManualResetEvent(true);
        private readonly ManualResetEvent _waitForErrorHandle = new ManualResetEvent(true);

        public ExifToolWrapper(string path = null, bool faster = false)
        {
            if (string.IsNullOrEmpty(path))
            {
                if (File.Exists(ExeName)) //in current directory
                    this.ExifToolPath = Path.GetFullPath(ExeName);
                else
                {
                    try
                    {
                        string dir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
                        if (dir == null)
                        {
                            TracePrint.NullException(nameof(dir));
                            this.ExifToolPath = ExeName;
                        }
                        else
                        {
                            this.ExifToolPath = Path.Combine(dir, ExeName);
                        }
                    }
                    catch (Exception xcp)
                    {
                        Debug.WriteLine(xcp.ToString());
                        this.ExifToolPath = ExeName;
                    }
                }
            }
            else
                this.ExifToolPath = path;

            if (!File.Exists(this.ExifToolPath))
                throw new ExifToolException($"{ExeName} not found");

            this._psi = new ProcessStartInfo
            {
                FileName = ExifToolPath,
                Arguments = faster ? ArgumentsFaster : Arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true
            };

            this.Status = ExeStatus.Stopped;
        }

        private void OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e?.Data))
                return;

            if (this.Status == ExeStatus.Starting)
            {
                this.ExifToolVersion = e.Data;
                this._waitHandle.Set();

                return;
            }

            if (string.Equals(e.Data, $"{{ready{this._cmdCnt}}}", StringComparison.OrdinalIgnoreCase))
            {
                this._waitHandle.Set();

                return;
            }

            this._output.AppendLine(e.Data);
        }

        //the error message has no 'ready' or other terminator so we must assume it has a single line (or it is received fast enough)
        private void ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e?.Data))
                return;

            if (string.Equals(e.Data, ExitMessage, StringComparison.OrdinalIgnoreCase))
            {
                this._proc?.StandardInput.WriteLine();

                return;
            }

            this._error.AppendLine(e.Data);
            this._waitForErrorHandle.Set();
        }

        public void Start()
        {
            this._stopRequested = false;

            if (this.Status != ExeStatus.Stopped)
                throw new ExifToolException("Process is not stopped");

            this.Status = ExeStatus.Starting;

            this._proc = new Process { StartInfo = _psi, EnableRaisingEvents = true };
            this._proc.OutputDataReceived += this.OutputDataReceived;
            this._proc.ErrorDataReceived += this.ErrorDataReceived;
            this._proc.Exited += this.ProcExited;
            this._proc.Start();

            this._proc.BeginOutputReadLine();
            this._proc.BeginErrorReadLine();
            this._proc.StandardInput.AutoFlush = true;

            this._waitHandle.Reset();
            this._proc.StandardInput.Write("-ver\n-execute0000\n");
            this._waitHandle.WaitOne();

            this.Status = ExeStatus.Ready;
        }

        //detect if process was killed
        private void ProcExited(object sender, EventArgs e)
        {
            if (this._proc != null)
            {
                this._proc.Dispose();
                this._proc = null;
            }

            this.Status = ExeStatus.Stopped;

            try
            {
                this._waitHandle.Set();
            }
            catch
            {
                TracePrint.CatchException("Acceptable catch.");
            }
            if (!this._stopRequested && this.Resurrect)
                this.Start();
        }

        public void Stop()
        {
            this._stopRequested = true;

            if (this.Status != ExeStatus.Ready)
            {
                Debug.Print("ExifToolWrapper: Can't kill the process as its not ready");
                // throw new ExifToolException("Process must be ready"); 
                return;

            }
            this.Status = ExeStatus.Stopping;

            this._waitHandle.Reset();
            this._proc.StandardInput.Write("-stay_open\nFalse\n");

            if (!this._waitHandle.WaitOne(TimeSpan.FromSeconds(this.SecondsToWaitForStop)))
            {
                if (this._proc != null)
                {
                    try
                    {
                        this._proc.Kill();
                        this._proc.WaitForExit((int)(1000 * this.SecondsToWaitForStop / 2));
                        if (this._proc != null)
                        {
                            this._proc.Dispose();
                        }
                    }
                    catch (Exception xcp)
                    {
                        Debug.WriteLine(xcp.ToString());
                    }

                    this._proc = null;
                }

                this.Status = ExeStatus.Stopped;
            }
        }

        private readonly object _lockObj = new object();

        private void DirectSend(string cmd, params object[] args)
        {
            //tried some re-encoding like this, no success
            //var ba = Encoding.Convert(Encoding.Unicode, Encoding.UTF8, Encoding.Unicode.GetBytes(cmd));
            //string b = Encoding.UTF8.GetString(ba);
            //proc.StandardInput.Write("-charset\nfilename=UTF8\n{0}\n-execute{1}\n", args.Length == 0 ? b : string.Format(b, args), cmdCnt);

            this._proc.StandardInput.Write("{0}\n-execute{1}\n", args.Length == 0 ? cmd : string.Format(cmd, args), this._cmdCnt);
            this._waitHandle.WaitOne();
        }

        //http://u88.n24.queensu.ca/exiftool/forum/index.php?topic=8382.0
        private void SendViaFile(string cmd, params object[] args)
        {
            var argFile = Path.GetTempFileName();
            try
            {
                using (var sw = new StreamWriter(argFile))
                {
                    sw.WriteLine(args.Length == 0 ? cmd : string.Format(cmd, args), this._cmdCnt);
                }

                this._proc.StandardInput.Write("-charset\nfilename=UTF8\n-@\n{0}\n-execute{1}\n", argFile, this._cmdCnt);
                this._waitHandle.WaitOne();
            }
            finally
            {
               FilesFolders.TryDeleteFileIfExists(argFile);
            }
        }

        public ExifToolResponse SendCommand(string cmd, params object[] args) => this.SendCommand(this.Method, cmd, args);

        private ExifToolResponse SendCommand(CommunicationMethod method, string cmd, params object[] args)
        {
            if (this.Status != ExeStatus.Ready)
                throw new ExifToolException("Process must be ready");

            ExifToolResponse resp;
            lock (this._lockObj)
            {
                this._waitHandle.Reset();
                this._waitForErrorHandle.Reset();

                if (method == CommunicationMethod.ViaFile)
                    this.SendViaFile(cmd, args);
                else
                    this.DirectSend(cmd, args);

                //if no output then probably there is an error, so wait at most SecondsToWaitForError for the error message to arrive 
                if (this._output.Length == 0)
                {
                    this._waitForErrorHandle.WaitOne(TimeSpan.FromSeconds(this.SecondsToWaitForError));
                    resp = new ExifToolResponse(false, this._error.ToString());
                    this._error.Clear();
                }
                else
                {
                    resp = new ExifToolResponse(true, this._output.ToString());
                    this._output.Clear();
                }

                this._cmdCnt++;
            }

            if (!resp.IsSuccess && method == CommunicationMethod.Auto)
            {
                string err = resp.Result.ToLowerInvariant();

                if (err.Contains("file not found") || err.Contains("invalid filename encoding"))
                    return this.SendCommand(CommunicationMethod.ViaFile, cmd, args);
            }

            return resp;
        }

        public ExifToolResponse SetExifInto(string path, string key, string val, bool overwriteOriginal = true) =>
            this.SetExifInto(path, new Dictionary<string, string> { [key] = val }, overwriteOriginal);

        public ExifToolResponse SetExifInto(string path, Dictionary<string, string> data, bool overwriteOriginal = true)
        {
            // Check the arguments for null 
            if (data == null)
            {
                // this should not happen
                TracePrint.StackTrace(1);
                // throw new ArgumentNullException(nameof(data));
                // try this to indicate the failure case
                return new ExifToolResponse(false, "data dictionary is null");
            }

            if (!File.Exists(path))
            {
                return new ExifToolResponse(false, $"'{path}' not found");
            }

            var cmd = new StringBuilder();
            foreach (KeyValuePair<string, string> kv in data)
            {
                cmd.AppendFormat("-{0}={1}\n", kv.Key, kv.Value);
            }

            if (overwriteOriginal)
            {
                cmd.Append("-overwrite_original\n");
            }

            cmd.Append(path);
            var cmdRes = this.SendCommand(cmd.ToString());

            //if failed return as it is, if it's success must check the response
            return cmdRes ? new ExifToolResponse(cmdRes.Result) : cmdRes;
        }

        public Dictionary<string, string> FetchExifFrom(string path, IEnumerable<string> tagsToKeep = null, bool keepKeysWithEmptyValues = true)
        {
            var res = new Dictionary<string, string>();

            if (!File.Exists(path))
            {
                return res;
            }

            var tagsTable = tagsToKeep?.ToDictionary(x => x, x => 1);
            bool filter = tagsTable != null && tagsTable.Count > 0;
            var cmdRes = this.SendCommand(path);
            if (!cmdRes)
            {
                return res;
            }

            foreach (string s in cmdRes.Result.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = s.Split('\t');
                if (kv.Length == 1 && kv[0] == "{ready0000}")
                {
                    // This was introduced in the results in a later version of EXIF.
                    // I catch and discard it here as otherwise it generates the Debug.Assert message in the next test whenever exiftool is spawned.
                    // Debug.Print("ExifToolWrapper: Ready0000 caught and ignored.");
                    continue;
                }
                Debug.Assert(kv.Length == 2, $"Can not parse line :'{s}'");

                if (kv.Length != 2 || (!keepKeysWithEmptyValues && string.IsNullOrEmpty(kv[1])))
                {
                    continue;
                }
                if (filter && !tagsTable.ContainsKey(kv[0]))
                {
                    continue;
                }
                res[kv[0]] = kv[1];
            }
            return res;
        }

        public List<string> FetchExifToListFrom(string path, IEnumerable<string> tagsToKeep = null, bool keepKeysWithEmptyValues = true, string separator = ": ")
        {
            var res = new List<string>();

            if (!File.Exists(path))
            {
                return res;
            }

            var tagsTable = tagsToKeep?.ToDictionary(x => x, x => 1);
            bool filter = tagsTable?.Count > 0;
            var cmdRes = this.SendCommand(path);
            if (!cmdRes)
            {
                return res;
            }
            foreach (string s in cmdRes.Result.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = s.Split('\t');
                Debug.Assert(kv.Length == 2, $"Can not parse line :'{s}'");

                if (kv.Length != 2 || (!keepKeysWithEmptyValues && string.IsNullOrEmpty(kv[1])))
                    continue;

                if (filter && !tagsTable.ContainsKey(kv[0]))
                    continue;

                res.Add($"{kv[0]}{separator}{kv[1]}");
            }

            return res;
        }

        public ExifToolResponse CloneExif(string source, string dest, bool backup = false)
        {
            if (!File.Exists(source) || !File.Exists(dest))
                return new ExifToolResponse(false, $"'{source}' or '{dest}' not found");

            var cmdRes = this.SendCommand("{0}-tagsFromFile\n{1}\n{2}", backup ? "" : "-overwrite_original\n", source, dest);

            return cmdRes ? new ExifToolResponse(cmdRes.Result) : cmdRes;
        }

        public ExifToolResponse ClearExif(string path, bool backup = false)
        {
            if (!File.Exists(path))
                return new ExifToolResponse(false, $"'{path}' not found");

            var cmdRes = this.SendCommand("{0}-all=\n{1}", backup ? "" : "-overwrite_original\n", path);

            return cmdRes ? new ExifToolResponse(cmdRes.Result) : cmdRes;
        }

        public DateTime? GetCreationTime(string path)
        {
            if (!File.Exists(path))
                return null;

            var cmdRes = this.SendCommand("-DateTimeOriginal\n-s3\n{0}", path);
            if (!cmdRes)
                return null;

            if (DateTime.TryParseExact(cmdRes.Result,
                "yyyy.MM.dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out DateTime dt))
                return dt;

            return null;
        }

        public int GetOrientation(string path)
        {
            if (!File.Exists(path))
                return 1;

            var cmdRes = this.SendCommand("-Orientation\n-n\n-s3\n{0}", path);
            if (!cmdRes)
                return 1;

            if (int.TryParse(cmdRes.Result.Trim('\t', '\r', '\n'), out int o))
                return o;

            return 1;
        }

        public int GetOrientationDeg(string path) => OrientationPos2Deg(this.GetOrientation(path));

        public ExifToolResponse SetOrientation(string path, int ori, bool overwriteOriginal = true)
        {
            if (!File.Exists(path))
                return new ExifToolResponse(false, $"'{path}' not found");

            var cmd = new StringBuilder();
            cmd.AppendFormat("-Orientation={0}\n-n\n-s3\n", ori);

            if (overwriteOriginal)
                cmd.Append("-overwrite_original\n");

            cmd.Append(path);
            var cmdRes = this.SendCommand(cmd.ToString());

            return cmdRes ? new ExifToolResponse(cmdRes.Result) : cmdRes;
        }

        public ExifToolResponse SetOrientationDeg(string path, int ori, bool overwriteOriginal = true) =>
            this.SetOrientation(path, OrientationDeg2Pos(ori), overwriteOriginal);

        #region Static orientation helpers

        /*
         
1        2       3      4         5            6           7          8

888888  888888      88  88      8888888888          88  8888888888   88          
88          88      88  88      88  88          88  88      88  88   88  88      
8888      8888    8888  8888    88          8888888888          88   8888888888  
88          88      88  88
88          88  888888  888888

        1 => 'Horizontal (normal)',
        2 => 'Mirror horizontal',
        3 => 'Rotate 180',
        4 => 'Mirror vertical',
        5 => 'Mirror horizontal and rotate 270 CW',
        6 => 'Rotate 90 CW',
        7 => 'Mirror horizontal and rotate 90 CW',
        8 => 'Rotate 270 CW'
         */

        public static int OrientationPos2Deg(int pos)
        {
            switch (pos)
            {
                case 8:
                    return 270;
                case 3:
                    return 180;
                case 6:
                    return 90;
                default:
                    return 0;
            }
        }

        public static int OrientationDeg2Pos(int deg)
        {
            switch (deg)
            {
                case 270:
                    return 8;
                case 180:
                    return 3;
                case 90:
                    return 6;
                default:
                    return 1;
            }
        }

        public static int OrientationString2Deg(string pos)
        {
            switch (pos)
            {
                case "Rotate 270 CW":
                    return 270;
                case "Rotate 180":
                    return 180;
                case "Rotate 90 CW":
                    return 90;
                default:
                    return 0;
            }
        }

        public static string OrientationDeg2String(int deg)
        {
            switch (deg)
            {
                case 270:
                    return "Rotate 270 CW";
                case 180:
                    return "Rotate 180";
                case 90:
                    return "Rotate 90 CW";
                default:
                    return "Horizontal (normal)";
            }
        }

        private static readonly int[] OrientationPositions = { 1, 6, 3, 8 };

        public static int RotateOrientation(int crtOri, bool clockwise, int steps = 1)
        {
            int newOri = 1;
            int len = OrientationPositions.Length;

            if (steps % len == 0)
                return crtOri;

            for (int i = 0; i < len; i++)
            {
                if (crtOri == OrientationPositions[i])
                {
                    newOri = clockwise
                        ? OrientationPositions[(i + steps) % len]
                        : OrientationPositions[(i + (1 + steps / len) * len - steps) % OrientationPositions.Length];

                    break;
                }
            }

            return newOri;
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            Debug.Assert(this.Status == ExeStatus.Ready || this.Status == ExeStatus.Stopped, "Invalid state");

            if (this._proc != null && this.Status == ExeStatus.Ready)
                this.Stop();
            if (this._waitHandle != null)
            {
                this._waitHandle.Dispose();
            }
            if (this._waitForErrorHandle != null)
            {
                this._waitForErrorHandle.Dispose();
            }
        }

        #endregion
    }
}
