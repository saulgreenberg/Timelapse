using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Timelapse.DataStructures;
using Timelapse.Dialog;

namespace Timelapse.Database
{
    /// <summary>
    /// Describes the outcome of an SQLite operation.
    /// </summary>
    public enum SqlResultStatus
    {
        /// <summary>The operation completed successfully.</summary>
        Success,

        /// <summary>
        /// The operation failed due to an SQLite error or unexpected exception.
        /// Check <see cref="SqlOperationResult.ErrorMessage"/>,
        /// <see cref="SqlOperationResult.Exception"/>, and
        /// <see cref="SqlOperationResult.FailingStatement"/> for details.
        /// </summary>
        Failed,

        /// <summary>
        /// The operation was cancelled by the user before it completed.
        /// Any changes made up to the cancellation point were rolled back.
        /// </summary>
        Cancelled
    }

    /// <summary>
    /// Represents the result of an SQLite operation that returns no value (void equivalent).
    /// <para>
    /// Returned by write operations such as <c>ExecuteNonQueryWithRollback</c>, <c>Insert</c>,
    /// <c>Update</c>, and schema-modification methods.
    /// </para>
    /// <para>
    /// Use <see cref="Success"/> to check whether the operation succeeded, or
    /// <see cref="WasCancelled"/> to distinguish a user cancellation from an error.
    /// On failure, <see cref="ErrorMessage"/> provides a human-readable description,
    /// <see cref="Exception"/> carries the original exception, and
    /// <see cref="FailingStatement"/> holds the SQL statement that was executing when
    /// the failure occurred — useful for composing a bug report that can be sent for diagnosis.
    /// </para>
    /// <para>
    /// Create instances only via the static factory methods <see cref="Ok"/>,
    /// <see cref="Fail"/>, and <see cref="Cancel"/> rather than directly constructing the class.
    /// </para>
    /// </summary>
    public class SqlOperationResult
    {
        #region Properties

        /// <summary>
        /// The outcome of the operation: Success, Failed, or Cancelled.
        /// </summary>
        public SqlResultStatus Status { get; init; }

        /// <summary>
        /// True if the operation completed without error and was not cancelled.
        /// </summary>
        public bool Success => Status == SqlResultStatus.Success;

        /// <summary>
        /// True if the operation was cancelled by the user.
        /// Distinguishes a deliberate cancellation from an unexpected failure so that
        /// callers can show an appropriate UI message (e.g. "Cancelled" vs "Error").
        /// </summary>
        public bool WasCancelled => Status == SqlResultStatus.Cancelled;

        /// <summary>
        /// A short human-readable description of the failure or cancellation.
        /// Null when <see cref="Success"/> is true.
        /// Suitable for display in a bug-report dialog shown to the user.
        /// </summary>
        public string ErrorMessage { get; init; }

        /// <summary>
        /// The exception that caused the failure, if any.
        /// Null when the operation succeeded or was cancelled without an exception.
        /// Contains the full SQLite error details including error code and inner exceptions.
        /// </summary>
        public Exception Exception { get; init; }

        /// <summary>
        /// The SQL statement that was executing at the moment the failure occurred.
        /// Null when the operation succeeded or was cancelled.
        /// <para>
        /// For single-statement operations this is the statement itself.
        /// For multi-statement transactions this is the last statement executed before
        /// the exception was thrown, which is almost always the one that caused the failure.
        /// </para>
        /// <para>
        /// This is intentionally kept as raw SQL (not truncated) so that it can be included
        /// verbatim in a bug report emailed by the user, giving full context for diagnosis.
        /// Note that some generated statements may be very long (e.g. bulk INSERTs); consider
        /// truncating for display purposes while preserving the full text for the report.
        /// </para>
        /// </summary>
        public string FailingStatement { get; init; }

        public string Context = string.Empty;

        #endregion

        #region Generate the exception dialog

        public static void GenerateExceptionDialog(SqlOperationResult result, string context = "")
        {
            if (context != null)
            {
                result.Context = context + $" | {result.Context}";
            }

            Task.Run(() =>
            {
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    // If already on the UI thread, execute the action directly
                    Dialog.ExceptionShutdownDialog dialog = new ExceptionShutdownDialog(
                        GlobalReferences.MainWindow,
                        new UnhandledExceptionEventArgs(result.Exception, false), result);
                    bool? dresult = dialog.ShowDialog();
                    if (dresult == false)
                    {
                        GlobalReferences.MainWindow.Close();
                        Application.Current.Shutdown();
                    }
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Dialog.ExceptionShutdownDialog dialog = new ExceptionShutdownDialog(
                            GlobalReferences.MainWindow,
                            new UnhandledExceptionEventArgs(result.Exception, false), result);
                        bool? dresult = dialog.ShowDialog();
                        if (dresult == false)
                        {
                            GlobalReferences.MainWindow.Close();
                            Application.Current.Shutdown();
                        }
                    });
                }

            });
        }
        #endregion

        #region Factory methods

        /// <summary>
        /// Returns a result indicating the operation completed successfully.
        /// </summary>
        public static SqlOperationResult Ok()
            => new() { Status = SqlResultStatus.Success };

        /// <summary>
        /// Returns a result indicating the operation failed.
        /// </summary>
        /// <param name="message">
        /// A short human-readable description of what went wrong.
        /// Will be shown to the user in an error or bug-report dialog.
        /// </param>
        /// <param name="exception">
        /// The exception that caused the failure. Pass null if no exception is available.
        /// </param>
        /// <param name="failingStatement">
        /// The SQL statement that was executing when the failure occurred.
        /// Pass null if the statement is not known or not applicable.
        /// This is included in bug reports to help diagnose the problem.
        /// </param>
        public static SqlOperationResult Fail(string message, Exception exception = null, string failingStatement = null)
            => new()
            {
                Status = SqlResultStatus.Failed,
                ErrorMessage = message,
                Exception = exception,
                FailingStatement = failingStatement
            };

        /// <summary>
        /// Returns a result indicating the operation was cancelled by the user.
        /// Any changes made up to the cancellation point were rolled back before this is returned.
        /// </summary>
        public static SqlOperationResult Cancel()
            => new()
            {
                Status = SqlResultStatus.Cancelled,
                ErrorMessage = "The operation was cancelled."
            };

        #endregion
    }

    /// <summary>
    /// Represents the result of an SQLite operation that returns a value of type
    /// <typeparamref name="T"/> on success (e.g. a <c>DataTable</c>, a scalar, or a list).
    /// <para>
    /// Use <see cref="SqlOperationResult.Success"/> to check the outcome before accessing
    /// <see cref="Value"/>. If the operation failed or was cancelled, <see cref="Value"/>
    /// will be the default for <typeparamref name="T"/> (typically null for reference types).
    /// </para>
    /// <para>
    /// Create instances only via the static factory methods <see cref="Ok"/>,
    /// <see cref="Fail"/>, and <see cref="Cancel"/>.
    /// </para>
    /// </summary>
    /// <typeparam name="T">
    /// The type of the value returned on success, e.g. <c>DataTable</c>, <c>List&lt;string&gt;</c>,
    /// <c>bool</c>, <c>int</c>, etc.
    /// </typeparam>
    public class SqlOperationResult<T> : SqlOperationResult
    {
        #region Properties

        /// <summary>
        /// The value produced by the operation on success.
        /// Will be the default for <typeparamref name="T"/> when
        /// <see cref="SqlOperationResult.Success"/> is false.
        /// Always check <see cref="SqlOperationResult.Success"/> before using this value.
        /// </summary>
        public T Value { get; init; }

        #endregion

        #region Factory methods

        /// <summary>
        /// Returns a result indicating the operation completed successfully, carrying
        /// <paramref name="value"/> as the result.
        /// </summary>
        public static SqlOperationResult<T> Ok(T value)
            => new() { Status = SqlResultStatus.Success, Value = value };

        /// <summary>
        /// Returns a result indicating the operation failed.
        /// <see cref="Value"/> will be the default for <typeparamref name="T"/>.
        /// </summary>
        /// <inheritdoc cref="SqlOperationResult.Fail(string, Exception, string)"/>
        public new static SqlOperationResult<T> Fail(string message, Exception exception = null, string failingStatement = null)
            => new()
            {
                Status = SqlResultStatus.Failed,
                ErrorMessage = message,
                Exception = exception,
                FailingStatement = failingStatement
            };

        /// <summary>
        /// Returns a result indicating the operation was cancelled by the user.
        /// <see cref="Value"/> will be the default for <typeparamref name="T"/>.
        /// Any changes made up to the cancellation point were rolled back before this is returned.
        /// </summary>
        public new static SqlOperationResult<T> Cancel()
            => new()
            {
                Status = SqlResultStatus.Cancelled,
                ErrorMessage = "The operation was cancelled."
            };

        #endregion
    }

    /// <summary>
    /// Holds the first SQL error that occurred during the session.
    /// <para>
    /// When <see cref="SQLiteWrapper.OnReadError"/> fires, it calls <see cref="TryRecord"/>
    /// instead of immediately displaying a dialog. This lets async operations complete (or
    /// wind down naturally) while the error is held here for inspection.
    /// </para>
    /// <para>
    /// Callers at natural task-completion points should check <see cref="HasError"/> and, if
    /// true, call <see cref="SqlOperationResult.GenerateExceptionDialog"/> passing
    /// <see cref="SqlOperationResult"/> and <see cref="Context"/>.
    /// </para>
    /// <para>
    /// Only the <em>first</em> error is recorded; subsequent calls to <see cref="TryRecord"/>
    /// are ignored. This prevents a cascade of async operations each trying to overwrite or
    /// re-report the same root failure. Call <see cref="Reset"/> to clear the state (e.g. after
    /// the error has been shown and the application is recovering or shutting down).
    /// </para>
    /// </summary>
    public static class SqlErrorState
    {
        // Backing field for SqlOperationResult. Interlocked.CompareExchange gives us
        // an atomic "set only if null" without needing a lock.
        private static SqlOperationResult _sqlOperationResult;

        /// <summary>
        /// The first <see cref="SqlOperationResult"/> recorded via <see cref="TryRecord"/>,
        /// or <c>null</c> if no error has occurred.
        /// </summary>
        public static SqlOperationResult SqlOperationResult => _sqlOperationResult;

        /// <summary>
        /// The context string (typically the method name) supplied when the error was first
        /// recorded. <see cref="string.Empty"/> if no error has been recorded.
        /// </summary>
        public static string Context { get; set; } = string.Empty;

        /// <summary>
        /// <c>true</c> if at least one SQL error has been recorded and not yet cleared.
        /// </summary>
        public static bool HasError => _sqlOperationResult != null;

        /// <summary>
        /// Records <paramref name="result"/> and <paramref name="context"/> as the current
        /// error state, but only if no error has already been recorded (first-error-wins).
        /// Thread-safe: multiple threads may call this simultaneously; exactly one will win.
        /// </summary>
        /// <param name="result">The failed <see cref="SqlOperationResult"/>.</param>
        /// <param name="context">
        /// A short string identifying where the error occurred (e.g. the method name passed
        /// to <see cref="SQLiteWrapper.OnReadError"/>).
        /// </param>
        /// <returns>
        /// <c>true</c> if this call won the race and recorded the error;
        /// <c>false</c> if an error was already recorded and this call was ignored.
        /// </returns>
        public static bool TryRecord(SqlOperationResult result, string context)
        {
            if (Interlocked.CompareExchange(ref _sqlOperationResult, result, null) != null)
            {
                // An error was already recorded — ignore this one.
                return false;
            }

            // This thread won: record the context. String assignment is atomic on all
            // supported .NET runtimes, so no lock is needed.
            Context = context ?? string.Empty;
            return true;
        }

        /// <summary>
        /// Clears the recorded error state, allowing new errors to be recorded.
        /// Call this after the error has been handled (e.g. after the exception dialog
        /// has been shown and the application is shutting down or recovering).
        /// </summary>
        public static void Reset()
        {
            Interlocked.Exchange(ref _sqlOperationResult, null);
            Context = string.Empty;
        }
    }

    /// <summary>
    /// An exception that wraps a failed <see cref="SqlOperationResult"/>, allowing SQLite
    /// failures to propagate as typed exceptions and be caught by a top-level handler such as
    /// <c>AppDomain.CurrentDomain.UnhandledException</c>.
    /// <para>
    /// Use this when a failure is severe enough that normal control-flow return values are
    /// insufficient — for example, when an unrecoverable database error should shut the
    /// application down and present the user with a bug-report dialog.
    /// </para>
    /// <para>
    /// Catch it by type in the unhandled-exception handler:
    /// <code>
    /// if (e.ExceptionObject is SqlOperationException sqlEx)
    /// {
    ///     // sqlEx.Result.ErrorMessage     — short description for display
    ///     // sqlEx.Result.FailingStatement — full SQL for the bug report
    ///     // sqlEx.Result.Exception        — original SQLite exception
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// For non-critical failures, prefer checking <see cref="SqlOperationResult.Success"/>
    /// on the returned result and showing a recoverable dialog rather than throwing.
    /// </para>
    /// <para>
    /// <b>Threading nuance:</b> <c>AppDomain.CurrentDomain.UnhandledException</c> catches
    /// unhandled exceptions from any thread, but whether a thrown
    /// <see cref="SqlOperationException"/> actually reaches it depends on the calling context:
    /// <list type="bullet">
    ///   <item>
    ///     <b>UI-thread call sites</b> (synchronous methods, event handlers): the exception
    ///     propagates synchronously up the call stack. If nothing catches it,
    ///     <c>OnUnhandledException</c> fires immediately. ✓
    ///   </item>
    ///   <item>
    ///     <b>Async Task call sites</b>: the exception is captured inside the returned
    ///     <c>Task</c>. It re-throws on the UI thread and reaches <c>OnUnhandledException</c>
    ///     only if the <c>Task</c> is <c>await</c>ed all the way up to an <c>async void</c>
    ///     event handler or equivalent UI-thread entry point. ✓ (when awaited)
    ///   </item>
    ///   <item>
    ///     <b>Fire-and-forget Tasks</b> (Task not awaited): the exception is unobserved.
    ///     In .NET 4.5+, unobserved task exceptions are silently swallowed —
    ///     <c>OnUnhandledException</c> is <b>not</b> called. ✗
    ///   </item>
    /// </list>
    /// Conclusion: this pattern is reliable as long as async call chains use <c>await</c>
    /// throughout. Avoid fire-and-forget Tasks in any method that may throw this exception.
    /// </para>
    /// </summary>
    public class SqlOperationException : Exception
    {
        /// <summary>
        /// The <see cref="SqlOperationResult"/> that describes the failure in detail,
        /// including <see cref="SqlOperationResult.ErrorMessage"/>,
        /// <see cref="SqlOperationResult.FailingStatement"/>, and
        /// <see cref="SqlOperationResult.Exception"/>.
        /// </summary>
        public SqlOperationResult Result { get; }

        /// <summary>
        /// Initialises a new <see cref="SqlOperationException"/>.
        /// </summary>
        /// <param name="message">
        /// A short description of the context in which the failure occurred
        /// (e.g. the name of the method that detected the failure).
        /// This becomes <see cref="Exception.Message"/>.
        /// </param>
        /// <param name="result">
        /// The failed <see cref="SqlOperationResult"/>. Its
        /// <see cref="SqlOperationResult.Exception"/> is set as the inner exception so that
        /// the full SQLite error is preserved in the exception chain.
        /// </param>
        public SqlOperationException(string message, SqlOperationResult result)
            : base(message, result.Exception)
        {
            Result = result;
        }
    }
}
