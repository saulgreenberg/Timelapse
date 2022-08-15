using Microsoft.Win32;
using System;
using System.Threading.Tasks;

namespace Timelapse.Util
{
    /// <summary>
    /// Various static methods to get information about the running environments
    /// </summary>
    public static class SystemStatus
    {
        #region Public Static Methods - Language and Culture
        /// <summary>
        /// Get the current language and culture 
        /// </summary>
        /// <param name="language"></param>
        /// <param name="culturename"></param>
        /// <param name="displayname"></param>
        /// <returns>true if the language is english (en) and culture is en-US or en-CA.</returns>
        public static bool CheckAndGetLangaugeAndCulture(out string language, out string culturename, out string displayname)
        {
            System.Globalization.CultureInfo cultureInfo = System.Threading.Thread.CurrentThread.CurrentCulture;
            language = cultureInfo.TwoLetterISOLanguageName;
            culturename = cultureInfo.Name;
            displayname = cultureInfo.DisplayName;
            return language == "en" && (culturename == "en-US" || culturename == "en-CA");
        }
        #endregion

        #region Public Static Methods - Dot Net Version
        /// <summary>
        /// Get the latest version of Dot Net running on this system
        /// </summary>
        /// <returns>a string indicating a message with the latest version number</returns>
        public static string GetDotNetVersion()
        {
            // adapted from https://msdn.microsoft.com/en-us/library/hh925568.aspx.
            int release = 0;
            using (RegistryKey tempRegKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            {
                using (RegistryKey ndpKey = tempRegKey.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\"))
                {
                    if (ndpKey != null)
                    {
                        object releaseAsObject = ndpKey.GetValue("Release");
                        if (releaseAsObject != null)
                        {
                            release = (int)releaseAsObject;
                        }
                    }

                    if (release >= 394802)
                    {
                        return "4.6.2 or later";
                    }
                    if (release >= 394254)
                    {
                        return "4.6.1";
                    }
                    if (release >= 393295)
                    {
                        return "4.6";
                    }
                    if (release >= 379893)
                    {
                        return "4.5.2";
                    }
                    if (release >= 378675)
                    {
                        return "4.5.1";
                    }
                    if (release >= 378389)
                    {
                        return "4.5";
                    }
                    return "4.5 or later not detected";
                }
            }
        }
        #endregion

        #region Public Static Methods - Available Processors
        /// <summary>
        /// Get the minimum between the number of processors available / desired for parallel operations
        /// </summary>
        /// <param name="maximumDegreeOfParallelismDesired"></param>
        /// <returns>ParallelOptions with MaxDegreeOfParallelism filled in</returns>
        public static ParallelOptions GetParallelOptions(int maximumDegreeOfParallelismDesired)
        {
            ParallelOptions parallelOptions = new ParallelOptions()
            {
                MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, maximumDegreeOfParallelismDesired)
            };
            return parallelOptions;
        }
        #endregion
    }
}
