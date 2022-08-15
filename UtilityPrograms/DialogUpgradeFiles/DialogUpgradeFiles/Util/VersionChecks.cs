using System;
using System.Reflection;

namespace DialogUpgradeFiles.Util
{
    /// <summary>
    /// Check if the version currently being run is the latest version
    /// </summary>
    public static class VersionChecks
    {
        #region Public Methods - Get / Compare Version Numbers
        /// <summary>
        /// Return the current timelapse version number
        /// </summary>
        /// <returns>Version instance detailing the version number </returns>
        public static Version GetTimelapseCurrentVersionNumber()
        {
            return Assembly.GetExecutingAssembly().GetName().Version;
        }

        /// <summary>
        /// Compare version numbers 
        /// </summary>
        /// <param name="versionNumber1"></param>
        /// <param name="versionNumber2"></param>
        /// <returns>True if versionNumber1 is greater than versionNumber2</returns>
        public static bool IsVersion1GreaterThanVersion2(string versionNumber1, string versionNumber2)
        {
            Version version1 = new Version(versionNumber1);
            Version version2 = new Version(versionNumber2);
            return version1 > version2;
        }

        /// <summary>
        /// Compare version numbers 
        /// </summary>
        /// <param name="versionNumber1"></param>
        /// <param name="versionNumber2"></param>
        /// <returns>True if versionNumber1 is greater than or equal to versionNumber2</returns>
        public static bool IsVersion1GreaterOrEqualToVersion2(string versionNumber1, string versionNumber2)
        {
            Version version1 = new Version(versionNumber1);
            Version version2 = new Version(versionNumber2);
            return version1 >= version2;
        }
        #endregion
    }
}
