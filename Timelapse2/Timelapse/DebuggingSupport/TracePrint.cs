using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace Timelapse.Util
{
    /// <summary>
    /// Debugging: Various forms of printing out trace infromation containing a message and a stack trace of the method names 
    /// Only active when the TRACE flag is set in the Project properties
    /// </summary>
    public static class TracePrint
    {
        #region Public methods
        /// <summary>
        /// Print a message and stack trace to a file
        /// </summary>
        /// <param name="message"></param>
        public static void PrintStackTraceToFile(string message)
        {
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(Path.Combine(Util.GlobalReferences.MainWindow.FolderPath, Constant.File.TraceFile), true))
            {
                file.WriteLine(GetMethodNameStack(message, 5));
                file.WriteLine("----");
            }
        }

        /// <summary>
        /// Debug print a message
        /// </summary>
        /// <param name="message"></param>
        [Conditional("TRACE")]
        // Option to print various failure messagesfor debugging
        public static void PrintMessage(string message)
        {
            Debug.Print("PrintFailure: " + message);
        }

        /// <summary>
        /// Debug print the method name followed by its stack level 
        /// </summary>
        [Conditional("TRACE")]
        public static void PrintStackTrace(int level)
        {
            Debug.Print(GetMethodNameStack(String.Empty, level));
        }

        /// <summary>
        /// Debug print the method name followed a message
        /// </summary>
        [Conditional("TRACE")]
        public static void PrintStackTrace(string message)
        {
            Debug.Print(GetMethodNameStack(message, 1));
        }

        /// <summary>
        /// Debug print the method name followed a message and stack level
        /// </summary>
        public static void PrintStackTrace(string message, int level)
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
            StackTrace st = new StackTrace(true);
            StackFrame sf;
            string methodStack = String.Empty;
            for (int i = 1; i <= level; i++)
            {
                sf = st.GetFrame(i);
                methodStack += Path.GetFileName(sf.GetFileName()) + ": ";
                methodStack += sf.GetMethod().Name;
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