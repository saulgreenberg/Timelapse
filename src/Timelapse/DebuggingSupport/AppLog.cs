using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using File = Timelapse.Constant.File;

namespace Timelapse.DebuggingSupport
{
    /// <summary>
    /// Persistent error/warning log written to %LocalAppData%\Timelapse\Timelapse.txt.
    /// Call <see cref="Initialize"/> once at startup when the root path is known.
    /// All methods are thread-safe and are silent no-ops if initialization has not occurred or failed.
    /// </summary>
    public static class AppLog
    {
        private const long MaxLogSizeBytes = 2 * 1024 * 1024; // 2 MB — trim threshold
        private const long TrimToBytes     = 1 * 1024 * 1024; // 1 MB — keep this many bytes from the end

        private static string _logFilePath;
        private static string _rootPath;
        private static DateTime _sessionStart;
        private static bool _sessionHeaderWritten;
        private static readonly Lock _lock = new();

        static AppLog()
        {
            try
            {
                string logFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    File.LogFolder);
                Directory.CreateDirectory(logFolder);
                _logFilePath = Path.Combine(logFolder, File.LogFile);
            }
            catch
            {
                _logFilePath = null;
            }
        }

        /// <summary>The full path to the log file, or null if not yet initialized.</summary>
        public static string LogFilePath => _logFilePath;

        /// <summary>
        /// The expected log file path based on well-known constants, regardless of whether
        /// Initialize has been called. Use this to check for an existing log file at any time.
        /// </summary>
        public static string DefaultLogFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            File.LogFolder,
            File.LogFile);

        #region Initialization
        /// <summary>
        /// Records the root path (folder containing the .tdb / .ddb files) for the session header,
        /// and trims the log file to 1 MB if it exceeds 2 MB.
        /// Called each time a database is opened.
        /// </summary>
        public static void Initialize(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                return;
            }

            _rootPath = rootPath;
            _sessionStart = DateTime.Now;
            _sessionHeaderWritten = false;

            // Trim if over the size cap: keep the last 1 MB, starting on a clean line boundary
            if (_logFilePath != null &&
                System.IO.File.Exists(_logFilePath) &&
                new FileInfo(_logFilePath).Length > MaxLogSizeBytes)
            {
                try
                {
                    byte[] allBytes = System.IO.File.ReadAllBytes(_logFilePath);
                    int trimStart = (int)(allBytes.Length - TrimToBytes);
                    while (trimStart < allBytes.Length && allBytes[trimStart] != '\n')
                    {
                        trimStart++;
                    }
                    trimStart++;
                    byte[] trimmed = new byte[allBytes.Length - trimStart];
                    Array.Copy(allBytes, trimStart, trimmed, 0, trimmed.Length);
                    System.IO.File.WriteAllBytes(_logFilePath, trimmed);
                }
                catch
                {
                    // If trimming fails, leave the file as-is and continue
                }
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Log a warning — something unexpected but recoverable.
        /// Caller location is captured automatically; no extra arguments needed.
        /// </summary>
        public static void Warning(string message,
            [CallerFilePath]   string file   = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int    line   = 0)
        {
            WriteEntry("WARNING", message, null, file, member, line);
        }

        /// <summary>
        /// Log a warning with the associated exception.
        /// The full exception chain (type, message, stack trace) is appended to the log entry.
        /// Caller location is captured automatically; no extra arguments needed.
        /// </summary>
        public static void Warning(string message, Exception ex,
            [CallerFilePath]   string file   = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int    line   = 0)
        {
            WriteEntry("WARNING", message, ex, file, member, line);
        }

        /// <summary>
        /// Log an error — an operation failed.
        /// Caller location is captured automatically; no extra arguments needed.
        /// </summary>
        public static void Error(string message,
            [CallerFilePath]   string file   = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int    line   = 0)
        {
            WriteEntry("ERROR  ", message, null, file, member, line);
        }

        /// <summary>
        /// Log an error with the associated exception.
        /// The full exception chain (type, message, stack trace) is appended to the log entry.
        /// Caller location is captured automatically; no extra arguments needed.
        /// </summary>
        public static void Error(string message, Exception ex,
            [CallerFilePath]   string file   = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int    line   = 0)
        {
            WriteEntry("ERROR  ", message, ex, file, member, line);
        }
        #endregion

        #region Private Helpers
        private static void WriteEntry(string level, string message, Exception ex,
                                       string filePath, string member, int line)
        {
            if (_logFilePath == null)
            {
                return;
            }

            if (!_sessionHeaderWritten)
            {
                const string rule = "=====================================================================";
                string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
                WriteRaw($"{rule}{Environment.NewLine}Session started {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Timelapse v{version} | {_rootPath}{Environment.NewLine}{rule}");
                _sessionHeaderWritten = true;
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string caller    = $"{Path.GetFileName(filePath)}({line}) {member}";
            string entry     = $"{timestamp} | {level} | {caller} | {message}";

            if (ex != null)
            {
                // ex.ToString() includes the exception type, message, and full stack trace.
                // Indent continuation lines so the block is visually distinct from the header.
                const string indent = "                              ";   // 30 spaces
                string exText = ex.ToString()
                    .Replace(Environment.NewLine, Environment.NewLine + indent);
                entry += Environment.NewLine + indent + exText + Environment.NewLine;
            }

            WriteRaw(entry);
        }

        private static void WriteRaw(string text)
        {
            if (_logFilePath == null)
            {
                return;
            }
            try
            {
                lock (_lock)
                {
                    using StreamWriter writer = new(_logFilePath, append: true);
                    writer.WriteLine(text);
                }
            }
            catch
            {
                // If the write fails (drive full, etc.) give up silently.
            }
        }
        #endregion
    }
}
