using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Timelapse.DataStructures;
using File = Timelapse.Constant.File;

namespace Timelapse.DebuggingSupport
{
    /// <summary>
    /// Debugging: Various forms of printing out trace information containing a message and a stack trace of the method names 
    /// Only active when the TRACE flag is set in the Project properties
    /// </summary>
    public static class TracePrint
    {
        #region Noop
        // Its sometimes handy to invoke a noop operation, such as in a catch that doesn't do anything.
        // This stops resharper from complaining.
        public static void Noop()
        {
            ((Action)(() => { }))();
        }

        #endregion

        #region Specialized messages
        public static void NullException()
        {
            NullException(string.Empty);
        }

        public static void NullException(string nullVariableName)
        {
            // ReSharper disable once RedundantAssignment
            string message = string.IsNullOrWhiteSpace(nullVariableName)
                ? "Null Exception"
                : "Null Exception: " + nullVariableName;
            Debug.Print(GetMethodNameStack(message, 2));
        }
        
        public static void CatchException(string message)
        {
            Debug.Print(GetMethodNameStack("Catch: " + message, 2));
        }

        public static void UnexpectedException(string message)
        {
            Debug.Print(GetMethodNameStack("Unexpected exception: " + message, 2));
        }

        #endregion

        #region Public methods
        /// <summary>
        /// Print a message and stack trace to a file
        /// </summary>
        /// <param name="message"></param>
        // ReSharper disable once UnusedMember.Global
        public static void StackTraceToFile(string message)
        {
            using StreamWriter file = new(Path.Combine(GlobalReferences.MainWindow.RootPathToDatabase, File.TraceFile), true);
            file.WriteLine(GetMethodNameStack(message, 5));
            file.WriteLine("----");
        }

        public static void StackTraceToOutput(string message)
        {
            Debug.Print(GetMethodNameStack(message, 5));
        }

        /// <summary>
        /// Debug print a message
        /// </summary>
        /// <param name="message"></param>
        [Conditional("TRACE")]
        // Option to print various informational messages for debugging
        public static void PrintMessageOnly(string message)
        {
            Debug.Print("Status: " + message);
        }

        /// <summary>
        /// Debug print a message
        /// </summary>
        /// <param name="message"></param>
        [Conditional("TRACE")]
        // Option to print various failure messages for debugging
        public static void PrintMessage(string message)
        {
            Debug.Print("Failure: " + message);
        }

        /// <summary>
        /// Debug print the method name followed by its stack level 
        /// </summary>
        [Conditional("TRACE")]
        public static void StackTrace(int level)
        {
            Debug.Print(GetMethodNameStack(string.Empty, level));
        }

        /// <summary>
        /// Debug print the method name followed a message
        /// </summary>
        [Conditional("TRACE")]
        // ReSharper disable once UnusedMember.Global
        public static void StackTrace(string message)
        {
            Debug.Print(GetMethodNameStack(message));
        }

        /// <summary>
        /// Debug print the method name followed a message and stack level
        /// </summary>
        public static void StackTrace(string message, int level)
        {
            Debug.Print(GetMethodNameStack(message, level));
        }
        #endregion


        #region Private (Internal) methods
        [MethodImpl(MethodImplOptions.NoInlining)]
        // Return the order and number of calls on a method, i.e., to illustrate the method calling stack.
        // The optional message string can be anything you want included in the output.
        // The optional level is the depth of the stack that should be printed 
        // (1 returns the current method name; 2 adds the caller name of that method, etc.)
        private static string GetMethodNameStack(string message = "", int level = 1)
        {
            StackTrace st = new(true);
            string methodStack = string.Empty;
            for (int i = 1; i <= level; i++)
            {
                StackFrame sf = st.GetFrame(i);
                methodStack += Path.GetFileName(sf?.GetFileName()) + ": ";
                methodStack += sf?.GetMethod()?.Name;
                if (i < level)
                {
                    methodStack += " <- ";
                }
            }
            methodStack += ": " + message;
            return methodStack;
        }
        #endregion
    }
}