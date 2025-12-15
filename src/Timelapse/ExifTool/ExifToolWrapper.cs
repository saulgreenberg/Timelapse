using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Timelapse.DataStructures;
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
                IsSuccess = false;
                Result = string.Empty;
                return;
            }

            IsSuccess = response.ToLowerInvariant().Contains(ExifToolWrapper.SuccessMessage);
            Result = response;
        }

        public ExifToolResponse(bool b, string r)
        {
            IsSuccess = b;
            Result = r;
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
        //private const string Arguments = "-fast  -m -q -q -stay_open True -@ - -common_args -d \"%Y.%m.%d %H:%M:%S\" -c \"%d %d %.6f\" -t";   //-g for groups
        //private const string Arguments = "-a -m -q -q -stay_open True -@ - -common_args -G1 -s -d \"%Y.%m.%d %H:%M:%S\" -c \"%d %d %.6f\" -t";   // -G1 -s gives [Group]TagName format
        private const string Arguments = "-a -m -q -q -stay_open True -@ - -common_args -G1 -s -d \"%Y-%m-%d %H:%M:%S\" -c \"%d %d %.6f\" -t";   // -G1 -s gives [Group]TagName format
       
        //private const string ArgumentsFaster = "-fast2 -m -q -q -stay_open True -@ - -common_args -d \"%Y.%m.%d %H:%M:%S\" -c \"%d %d %.6f\" -t";
        //private const string ArgumentsFaster = "-a -m -q -q -stay_open True -@ - -common_args -G1 -s -d \"%Y.%m.%d %H:%M:%S\" -c \"%d %d %.6f\" -t";   // -G1 -s gives [Group]TagName format
        private const string ArgumentsFaster = "-a -m -q -q -stay_open True -@ - -common_args -G1 -s -d \"%Y-%m-%d %H:%M:%S\" -c \"%d %d %.6f\" -t";   // -G1 -s gives [Group]TagName format
        private const string ExitMessage = "-- press RETURN --";
        internal const string SuccessMessage = "1 image files updated";

        /// <summary>
        /// Get the ExifTool executable path
        /// </summary>
        private static string GetExifToolPath()
        {
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
                if (dir == null)
                {
                    return ExeName;
                }
                return Path.Combine(dir, ExeName);
            }
            catch
            {
                return ExeName;
            }
        }

        /// <summary>
        /// Escape special characters in metadata values for ExifTool
        /// </summary>
        internal static string EscapeMetadataValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value ?? string.Empty;

            // Escape backslashes first (must be done before other escapes)
            value = value.Replace("\\", "\\\\");
            // Escape newlines
            value = value.Replace("\n", "\\n");
            // Escape carriage returns
            value = value.Replace("\r", "\\r");
            // Escape tabs
            value = value.Replace("\t", "\\t");

            return value;
        }

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
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _error = new();

        private readonly ProcessStartInfo _psi;
        private Process _proc;

        private readonly ManualResetEvent _waitHandle = new(true);
        private readonly ManualResetEvent _waitForErrorHandle = new(true);

        public ExifToolWrapper(string path = null, bool faster = false)
        {
            if (string.IsNullOrEmpty(path))
            {
                // CHANGED: Always use the executable's directory to find exiftool, rather than checking
                // the current working directory first. This fixes MSI installation issues where the
                // working directory may be set to a different location (e.g., C:\Windows\System32),
                // causing exiftool not to be found even though it's deployed alongside the executable.
                // Since exiftool is always bundled with the application, using the executable's location
                // is more reliable than relying on the current working directory.
                try
                {
                    string dir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
                    if (dir == null)
                    {
                        TracePrint.NullException(nameof(dir));
                        ExifToolPath = ExeName;
                    }
                    else
                    {
                        ExifToolPath = Path.Combine(dir, ExeName);
                    }
                }
                catch (Exception xcp)
                {
                    Debug.WriteLine(xcp.ToString());
                    ExifToolPath = ExeName;
                }
            }
            else
                ExifToolPath = path;

            if (!File.Exists(ExifToolPath))
                throw new ExifToolException($"{ExeName} not found");

            _psi = new()
            {
                FileName = ExifToolPath,
                Arguments = faster ? ArgumentsFaster : Arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true
            };

            Status = ExeStatus.Stopped;
        }

        private void OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e?.Data))
                return;

            if (Status == ExeStatus.Starting)
            {
                ExifToolVersion = e.Data;
                _waitHandle.Set();

                return;
            }

            if (string.Equals(e.Data, $"{{ready{_cmdCnt}}}", StringComparison.OrdinalIgnoreCase))
            {
                _waitHandle.Set();

                return;
            }

            _output.AppendLine(e.Data);
        }

        //the error message has no 'ready' or other terminator so we must assume it has a single line (or it is received fast enough)
        private void ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e?.Data))
                return;

            if (string.Equals(e.Data, ExitMessage, StringComparison.OrdinalIgnoreCase))
            {
                _proc?.StandardInput.WriteLine();

                return;
            }

            _error.AppendLine(e.Data);
            _waitForErrorHandle.Set();
        }

        public void Start()
        {
            _stopRequested = false;

            if (Status != ExeStatus.Stopped)
                throw new ExifToolException("Process is not stopped");

            Status = ExeStatus.Starting;

            _proc = new() { StartInfo = _psi, EnableRaisingEvents = true };
            _proc.OutputDataReceived += OutputDataReceived;
            _proc.ErrorDataReceived += ErrorDataReceived;
            _proc.Exited += ProcExited;
            _proc.Start();

            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();
            _proc.StandardInput.AutoFlush = true;

            _waitHandle.Reset();
            _proc.StandardInput.Write("-ver\n-execute0000\n");
            _waitHandle.WaitOne();

            Status = ExeStatus.Ready;
        }

        //detect if process was killed
        private void ProcExited(object sender, EventArgs e)
        {
            if (_proc != null)
            {
                _proc.Dispose();
                _proc = null;
            }

            Status = ExeStatus.Stopped;

            try
            {
                _waitHandle.Set();
            }
            catch
            {
                TracePrint.CatchException("Acceptable catch.");
            }
            if (!_stopRequested && Resurrect)
                Start();
        }

        public void Stop()
        {
            _stopRequested = true;

            if (Status != ExeStatus.Ready)
            {
                Debug.Print("ExifToolWrapper: Can't kill the process as its not ready");
                // throw new ExifToolException("Process must be ready"); 
                return;

            }
            Status = ExeStatus.Stopping;

            _waitHandle.Reset();
            _proc.StandardInput.Write("-stay_open\nFalse\n");

            if (!_waitHandle.WaitOne(TimeSpan.FromSeconds(SecondsToWaitForStop)))
            {
                if (_proc != null)
                {
                    try
                    {
                        _proc.Kill();
                        _proc.WaitForExit((int)(1000 * SecondsToWaitForStop / 2));
                        _proc?.Dispose();
                    }
                    catch (Exception xcp)
                    {
                        Debug.WriteLine(xcp.ToString());
                    }

                    _proc = null;
                }

                Status = ExeStatus.Stopped;
            }
        }

        private readonly Lock _lockObj = new();

        private void DirectSend(string cmd, params object[] args)
        {
            //tried some re-encoding like this, no success
            //var ba = Encoding.Convert(Encoding.Unicode, Encoding.UTF8, Encoding.Unicode.GetBytes(cmd));
            //string b = Encoding.UTF8.GetString(ba);
            //proc.StandardInput.Write("-charset\nfilename=UTF8\n{0}\n-execute{1}\n", args.Length == 0 ? b : string.Format(b, args), cmdCnt);

            _proc.StandardInput.Write("{0}\n-execute{1}\n", args.Length == 0 ? cmd : string.Format(cmd, args), _cmdCnt);
            _waitHandle.WaitOne();
        }

        //http://u88.n24.queensu.ca/exiftool/forum/index.php?topic=8382.0
        private void SendViaFile(string cmd, params object[] args)
        {
            var argFile = Path.GetTempFileName();
            try
            {
                using (var sw = new StreamWriter(argFile))
                {
                    sw.WriteLine(args.Length == 0 ? cmd : string.Format(cmd, args), _cmdCnt);
                }

                _proc.StandardInput.Write("-charset\nfilename=UTF8\n-@\n{0}\n-execute{1}\n", argFile, _cmdCnt);
                _waitHandle.WaitOne();
            }
            finally
            {
               FilesFolders.TryDeleteFileIfExists(argFile);
            }
        }

        public ExifToolResponse SendCommand(string cmd, params object[] args) => SendCommand(Method, cmd, args);

        private ExifToolResponse SendCommand(CommunicationMethod method, string cmd, params object[] args)
        {
            if (Status != ExeStatus.Ready)
                throw new ExifToolException("Process must be ready");

            ExifToolResponse resp;
            lock (_lockObj)
            {
                _waitHandle.Reset();
                _waitForErrorHandle.Reset();

                if (method == CommunicationMethod.ViaFile)
                    SendViaFile(cmd, args);
                else
                    DirectSend(cmd, args);

                //if no output then probably there is an error, so wait at most SecondsToWaitForError for the error message to arrive 
                if (_output.Length == 0)
                {
                    _waitForErrorHandle.WaitOne(TimeSpan.FromSeconds(SecondsToWaitForError));
                    resp = new(false, _error.ToString());
                    _error.Clear();
                }
                else
                {
                    resp = new(true, _output.ToString());
                    _output.Clear();
                }

                _cmdCnt++;
            }

            if (!resp.IsSuccess && method == CommunicationMethod.Auto)
            {
                string err = resp.Result.ToLowerInvariant();

                if (err.Contains("file not found") || err.Contains("invalid filename encoding"))
                    return SendCommand(CommunicationMethod.ViaFile, cmd, args);
            }

            return resp;
        }

        // ReSharper disable once UnusedMember.Global
        public ExifToolResponse SetExifInto(string path, string key, string val, bool overwriteOriginal = true) =>
            SetExifInto(path, new() { [key] = val }, overwriteOriginal);

        public ExifToolResponse SetExifInto(string path, Dictionary<string, string> data, bool overwriteOriginal = true)
        {
            // Check the arguments for null 
            if (data == null)
            {
                // this should not happen
                TracePrint.StackTrace(1);
                // throw new ArgumentNullException(nameof(data));
                // try this to indicate the failure case
                return new(false, "data dictionary is null");
            }

            if (!File.Exists(path))
            {
                return new(false, $"'{path}' not found");
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
            var cmdRes = SendCommand(cmd.ToString());

            //if failed return as it is, if it's success must check the response
            return cmdRes ? new(cmdRes.Result) : cmdRes;
        }

        /// <summary>
        /// Write EXIF/XMP metadata to a file using a custom config file
        /// NOTE: This launches ExifTool as a one-shot process (not stay-open mode)
        /// because -config must be the first argument on the command line
        /// </summary>
        /// <param name="path">Path to the image file</param>
        /// <param name="data">List of tag=value pairs (e.g., "XMP-TimelapseData:Species"="Lion")</param>
        /// <param name="configFilePath">Path to the ExifTool config file</param>
        /// <param name="overwriteOriginal">If true, don't create backup file</param>
        /// <returns>ExifToolResponse indicating success or failure</returns>
        public static async System.Threading.Tasks.Task<ExifToolResponse> SetExifIntoWithConfigAsync(string path, List<KeyValuePair<string, string>> data,
            string configFilePath, bool overwriteOriginal = true)
        {
            // Validate inputs
            if (data == null)
            {
                TracePrint.StackTrace(1);
                return new(false, "data list is null");
            }

            if (!File.Exists(path))
            {
                return new(false, $"'{path}' not found");
            }

            if (!File.Exists(configFilePath))
            {
                return new(false, $"Config file '{configFilePath}' not found");
            }

            // Get ExifTool path directly (static method)
            string exifToolPath = GetExifToolPath();

            // Build arguments list for one-shot ExifTool invocation
            // -config MUST be first argument on command line
            var args = new List<string>
            {
                "-config",
                configFilePath
            };

            // Add each metadata tag
            foreach (KeyValuePair<string, string> kv in data)
            {
                string escapedValue = EscapeMetadataValue(kv.Value);
                args.Add($"-{kv.Key}={escapedValue}");
            }

            // Add overwrite flag
            if (overwriteOriginal)
            {
                args.Add("-overwrite_original");
            }

            // Add file path
            args.Add(path);

            // Launch ExifTool as one-shot process (not stay-open mode)
            // Use Task.Run to run synchronous process execution on background thread
            return await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = exifToolPath,
                        Arguments = string.Join(" ", args.Select(a => $"\"{a}\"")), // Quote each argument
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = true  // Redirect stdin to prevent hanging
                    };

                    using var proc = Process.Start(psi);
                    if (proc == null)
                    {
                        return new ExifToolResponse(false, "Failed to start ExifTool process");
                    }

                    // Close stdin immediately - we're not sending any input
                    proc.StandardInput.Close();

                    // Read output and error synchronously (we're on a background thread via Task.Run)
                    string output = proc.StandardOutput.ReadToEnd();
                    string error = proc.StandardError.ReadToEnd();

                    // Wait for process to exit
                    proc.WaitForExit();

                    // Check for success
                    if (output.Contains(SuccessMessage))
                    {
                        return new ExifToolResponse(true, output);
                    }

                    // Return error if present, otherwise return output
                    string result = !string.IsNullOrEmpty(error) ? error : output;
                    return new ExifToolResponse(false, result);
                }
                catch (Exception ex)
                {
                    return new ExifToolResponse(false, $"Exception launching ExifTool: {ex.Message}");
                }
            }).ConfigureAwait(false);
        }

        public Dictionary<string, ImageMetadata> FetchExifFrom(string path, IEnumerable<string> tagsToKeep = null, bool keepKeysWithEmptyValues = true)
        {
            var res = new Dictionary<string, ImageMetadata>();

            if (!File.Exists(path))
            {
                return res;
            }

            var tagsTable = tagsToKeep?.ToDictionary(x => x, _ => 1);
            bool filter = tagsTable is { Count: > 0 };
            var cmdRes = SendCommand(path);
            if (!cmdRes)
            {
                return res;
            }

            foreach (string s in cmdRes.Result.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = s.Split('\t');
                if (kv.Length == 1 && kv[0] == "{ready0000}")
                {
                    // This was introduced in the results in a later version of EXIF.
                    // I catch and discard it here as otherwise it generates the Debug.Assert message in the next test whenever exiftool is spawned.
                    // Debug.Print("ExifToolWrapper: Ready0000 caught and ignored.");
                    continue;
                }

                // Handle both 2-column and 3-column formats from ExifTool
                // 2-column (without -G1): TagName<tab>Value
                // 3-column (with -G1 -t): Group<tab>TagName<tab>Value

                string directory = string.Empty;
                string tagName;
                string value;

                if (kv.Length == 3)
                {
                    // 3-column format: Group<tab>TagName<tab>Value
                    directory = kv[0];
                    tagName = kv[1];
                    value = kv[2];
                }
                else if (kv.Length == 2)
                {
                    // 2-column format: TagName<tab>Value (or [Group]TagName<tab>Value)
                    string tagWithGroup = kv[0];
                    value = kv[1];
                    tagName = tagWithGroup;

                    // Extract group/directory if present in format [Group]TagName
                    if (tagWithGroup.StartsWith("[") && tagWithGroup.Contains("]"))
                    {
                        int closeBracket = tagWithGroup.IndexOf(']');
                        directory = tagWithGroup.Substring(1, closeBracket - 1);
                        tagName = tagWithGroup.Substring(closeBracket + 1).Trim();
                    }
                }
                else
                {
                    // Skip lines that don't match expected formats (2 or 3 columns)
                    continue;
                }

                // Skip empty values if requested
                if (!keepKeysWithEmptyValues && string.IsNullOrEmpty(value))
                {
                    continue;
                }

                // Filter by tag name
                if (filter && !tagsTable.ContainsKey(tagName))
                {
                    continue;
                }

                // Create ImageMetadata matching MetadataExtractor format
                var metadata = new ImageMetadata(directory, tagName, value);

                // Use TagName as dictionary key (same as MetadataExtractor does)
                // Note: If duplicate tag names exist in different groups, only the first is kept
                res.TryAdd(tagName, metadata);
            }
            return res;
        }

        // ReSharper disable once UnusedMember.Global
        public List<string> FetchExifToListFrom(string path, IEnumerable<string> tagsToKeep = null, bool keepKeysWithEmptyValues = true, string separator = ": ")
        {
            var res = new List<string>();

            if (!File.Exists(path))
            {
                return res;
            }

            var tagsTable = tagsToKeep?.ToDictionary(x => x, _ => 1);
            bool filter = tagsTable?.Count > 0;
            var cmdRes = SendCommand(path);
            if (!cmdRes)
            {
                return res;
            }
            foreach (string s in cmdRes.Result.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries))
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

        // ReSharper disable once UnusedMember.Global
        public ExifToolResponse CloneExif(string source, string dest, bool backup = false)
        {
            if (!File.Exists(source) || !File.Exists(dest))
                return new(false, $"'{source}' or '{dest}' not found");

            var cmdRes = SendCommand("{0}-tagsFromFile\n{1}\n{2}", backup ? "" : "-overwrite_original\n", source, dest);

            return cmdRes ? new(cmdRes.Result) : cmdRes;
        }

        // ReSharper disable once UnusedMember.Global
        public ExifToolResponse ClearExif(string path, bool backup = false)
        {
            if (!File.Exists(path))
                return new(false, $"'{path}' not found");

            var cmdRes = SendCommand("{0}-all=\n{1}", backup ? "" : "-overwrite_original\n", path);

            return cmdRes ? new(cmdRes.Result) : cmdRes;
        }

        // ReSharper disable once UnusedMember.Global
        public DateTime? GetCreationTime(string path)
        {
            if (!File.Exists(path))
                return null;

            var cmdRes = SendCommand("-DateTimeOriginal\n-s3\n{0}", path);
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

            var cmdRes = SendCommand("-Orientation\n-n\n-s3\n{0}", path);
            if (!cmdRes)
                return 1;

            if (int.TryParse(cmdRes.Result.Trim('\t', '\r', '\n'), out int o))
                return o;

            return 1;
        }

        // ReSharper disable once UnusedMember.Global
        public int GetOrientationDeg(string path) => OrientationPos2Deg(GetOrientation(path));

        public ExifToolResponse SetOrientation(string path, int ori, bool overwriteOriginal = true)
        {
            if (!File.Exists(path))
                return new(false, $"'{path}' not found");

            var cmd = new StringBuilder();
            cmd.AppendFormat("-Orientation={0}\n-n\n-s3\n", ori);

            if (overwriteOriginal)
                cmd.Append("-overwrite_original\n");

            cmd.Append(path);
            var cmdRes = SendCommand(cmd.ToString());

            return cmdRes ? new(cmdRes.Result) : cmdRes;
        }

        // ReSharper disable once UnusedMember.Global
        public ExifToolResponse SetOrientationDeg(string path, int ori, bool overwriteOriginal = true) =>
            SetOrientation(path, OrientationDeg2Pos(ori), overwriteOriginal);

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
            return pos switch
            {
                8 => 270,
                3 => 180,
                6 => 90,
                _ => 0
            };
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

        // ReSharper disable once UnusedMember.Global
        public static int OrientationString2Deg(string pos)
        {
            return pos switch
            {
                "Rotate 270 CW" => 270,
                "Rotate 180" => 180,
                "Rotate 90 CW" => 90,
                _ => 0
            };
        }

        // ReSharper disable once UnusedMember.Global
        public static string OrientationDeg2String(int deg)
        {
            return deg switch
            {
                270 => "Rotate 270 CW",
                180 => "Rotate 180",
                90 => "Rotate 90 CW",
                _ => "Horizontal (normal)"
            };
        }

        private static readonly int[] OrientationPositions = [1, 6, 3, 8];

        // ReSharper disable once UnusedMember.Global
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
            Debug.Assert(Status == ExeStatus.Ready || Status == ExeStatus.Stopped, "Invalid state");

            if (_proc != null && Status == ExeStatus.Ready)
                Stop();
            _waitHandle?.Dispose();
            _waitForErrorHandle?.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// Manages a dedicated stay-open ExifTool process for batch writing with custom config file.
    /// Optimized for writing metadata to thousands of files efficiently.
    /// </summary>
    public sealed class ExifToolConfigBatchWriter : IDisposable
    {
        private Process _process;
        private StreamWriter _stdin;
        private StreamReader _stdout;
        private StreamReader _stderr;
        private readonly string _configPath;
        private readonly string _exifToolPath;
        private bool _isStarted;
        private bool _disposed;

        private const string ReadyToken = "{ready}";
        private const string ExecuteCommand = "-execute\n";

        public ExifToolConfigBatchWriter(string configFilePath)
        {
            if (string.IsNullOrEmpty(configFilePath))
                throw new ArgumentNullException(nameof(configFilePath));
            if (!System.IO.File.Exists(configFilePath))
                throw new FileNotFoundException("Config file not found", configFilePath);

            _configPath = configFilePath;
            _exifToolPath = GetExifToolPath();
        }

        private static string GetExifToolPath()
        {
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
                if (dir == null)
                {
                    return "exiftool(-k).exe";
                }
                return Path.Combine(dir, "exiftool(-k).exe");
            }
            catch
            {
                return "exiftool(-k).exe";
            }
        }

        /// <summary>
        /// Start the stay-open ExifTool process with config file
        /// </summary>
        public void Start()
        {
            if (_isStarted)
                return;

            try
            {
                // Build command: exiftool -config path -stay_open True -@ -
                var psi = new ProcessStartInfo
                {
                    FileName = _exifToolPath,
                    Arguments = $"-config \"{_configPath}\" -stay_open True -@ -",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _process = Process.Start(psi);
                if (_process == null)
                    throw new Exception("Failed to start ExifTool process");

                _stdin = _process.StandardInput;
                _stdout = _process.StandardOutput;
                _stderr = _process.StandardError;

                // Set stdin to auto-flush
                _stdin.AutoFlush = true;

                // Read stderr on background thread to prevent buffer deadlock
                // After ~30 files with special characters, stderr buffer can fill up and block the process
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        while (_stderr.ReadLine() is { } line)
                        {
                            // Filter out the "-- press ENTER --" message from exiftool(-k).exe
                            // This is a harmless prompt that appears when using the -k flag version
                            if (line.Contains("-- press ENTER --") || line.Trim() == "--" || string.IsNullOrWhiteSpace(line))
                            {
                                continue;
                            }

                            // Log stderr output to prevent buffer filling
                            System.Diagnostics.Debug.WriteLine($"ExifTool stderr: {line}");
                        }
                    }
                    catch
                    {
                        // Ignore errors when reading stderr (process may have exited)
                    }
                });

                _isStarted = true;
            }
            catch (Exception ex)
            {
                Dispose();
                throw new Exception($"Failed to start ExifTool batch writer: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Write metadata to a single file
        /// </summary>
        public ExifToolResponse WriteFileMetadata(string filePath, List<KeyValuePair<string, string>> metadata, bool overwriteOriginal)
        {
            if (!_isStarted)
                throw new InvalidOperationException("Batch writer not started");
            if (_disposed)
                throw new ObjectDisposedException(nameof(ExifToolConfigBatchWriter));

            try
            {
                // Build command for this file
                foreach (var kv in metadata)
                {
                    // Use ^= operator to write empty string instead of deleting tag when value is empty
                    // See: https://exiftool.org/exiftool_pod.html
                    // -TAG^= writes empty string, -TAG= deletes the tag
                    string escapedValue = ExifToolWrapper.EscapeMetadataValue(kv.Value);
                    _stdin.WriteLine($"-{kv.Key}^={escapedValue}");
                }

                if (overwriteOriginal)
                {
                    _stdin.WriteLine("-overwrite_original");
                }

                // Suppress XMP-x:XMPToolkit tag that ExifTool adds automatically
                _stdin.WriteLine("-XMP-x:XMPToolkit=");

                _stdin.WriteLine(filePath);
                _stdin.WriteLine(ExecuteCommand);

                // Read response until we see {ready} token
                var response = new StringBuilder();
                while (_stdout.ReadLine() is { } line)
                {
                    if (line.Contains(ReadyToken))
                        break;
                    response.AppendLine(line);
                }

                return new ExifToolResponse(response.ToString());
            }
            catch (Exception ex)
            {
                return new ExifToolResponse(false, $"Error writing metadata: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                if (_isStarted && _process is { HasExited: false })
                {
                    // Send stop command
                    _stdin?.WriteLine("-stay_open");
                    _stdin?.WriteLine("False");

                    // Give it a moment to exit gracefully
                    if (!_process.WaitForExit(5000))
                    {
                        _process.Kill();
                    }
                }

                _stdin?.Dispose();
                _stdout?.Dispose();
                _stderr?.Dispose();
                _process?.Dispose();
            }
            catch
            {
                // Suppress disposal exceptions
            }
            finally
            {
                _disposed = true;
                _isStarted = false;
            }
        }
    }
}
