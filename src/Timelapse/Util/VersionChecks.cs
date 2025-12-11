using System;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Xml;
using Timelapse.Constant;
using Timelapse.Dialog;

namespace Timelapse.Util
{
    /// <summary>
    /// Check if the version currently being run is the latest version
    /// </summary>
    public class VersionChecks
    {
        #region Private variables
        private readonly Uri latestVersionAddress; // The url of the timelapse_template_version timelapse_version xml file containing the versioo information
        private readonly string applicationName;  // Either Timelapse or TimelapseEditor
        private readonly Window window;
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor. For convenience in later calls, it stores various parameters for reuse in its methods
        /// </summary>
        public VersionChecks(Window window, string applicationName, Uri latestVersionAddress)
        {
            this.applicationName = applicationName;
            this.latestVersionAddress = latestVersionAddress;
            this.window = window;
        }
        #endregion

        #region Public Methods - Check for New Version and Display Results if Needed
        /// <summary>
        /// Checks for updates by comparing the current version number of Timelapse or the Editor with a version stored on the Timelapse website in an xml file in either
        /// timelapse_version.xml or timelapse_template_version.xml (as specified in the latestVersionAddress). 
        /// Displays a notification with recent update information
        /// Optionally displays a message to the user (if showNoUpdatesMessage is false) indicating the status
        /// </summary>
        /// <param name="showNoUpdatesMessage"></param>
        /// <returns>True if an update is available, else false</returns>
        public bool TryCheckForNewVersionAndDisplayResultsAsNeeded(bool showNoUpdatesMessage)
        {
            string url = string.Empty; // THE URL where the new version is located
            Version latestVersionNumber = null;  // if a new version is available, store the new version number here  
            XmlReader reader = null;
            try
            {
                // Use HttpClient with timeout instead of WebRequest
                using HttpClient httpClient = new();
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                using var response = httpClient.GetAsync(latestVersionAddress.AbsoluteUri).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();
                using var stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                // This pattern follows recommended correction to CA3075: Insecure DTD Processing
                // provide the XmlReader with the response stream
                XmlReaderSettings settings = new() { XmlResolver = null };
                reader = XmlReader.Create(stream, settings);
                reader.MoveToContent(); // skip the junk at the beginning  

                // As the XmlTextReader moves only forward, we save current xml element name in elementName variable. 
                // When we parse a  text node, we refer to elementName to check what was the node name  
                string elementName = string.Empty;
                // Check if the xml starts with a <timelapse> Element  
                if (reader.NodeType == XmlNodeType.Element && reader.Name == VersionXml.Timelapse)
                {
                    // Read the various elements and their associated contents
                    while (reader.Read())
                    {
                        // when we find an element node, we remember its name  
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            elementName = reader.Name;
                        }
                        else
                        {
                            // for text nodes...  
                            if (reader.NodeType == XmlNodeType.Text && reader.HasValue)
                            {
                                // we check what the name of the node was  
                                switch (elementName)
                                {
                                    case VersionXml.Version:
                                        // we keep the version info in xxx.xxx.xxx.xxx format as the Version class does the  parsing for us  
                                        latestVersionNumber = new(reader.Value);
                                        break;
                                    case VersionXml.Url:
                                        url = reader.Value;
                                        break;
                                }
                            }
                        }
                    }
                }
            }

            catch (HttpRequestException)
            {
                // Put message in saying we can't connect
                if (showNoUpdatesMessage)
                {
                    Dialogs.CheckUpdatesTimedOutDialog(Application.Current.MainWindow);
                }

                return false;
            }
            catch (Exception)
            {
                if (showNoUpdatesMessage)
                {
                    Dialogs.CheckUpdatesTimedOutDialog(Application.Current.MainWindow);
                }

                return false;
            }
            finally
            {
                reader?.Close();
            }

            // get the running version  
            Version currentVersionNumber = GetTimelapseCurrentVersionNumber();

            // compare the versions  
            if (currentVersionNumber < latestVersionNumber)
            {
                NewVersionNotification newVersionNotification = new(window, applicationName, currentVersionNumber, latestVersionNumber);
                if (newVersionNotification.ShowDialog() == true)
                {
                    // navigate the default web browser to our app homepage (the url comes from the xml content)  
                    ProcessExecution.TryProcessStart(new Uri(url));
                }
            }
            else if (showNoUpdatesMessage)
            {
                Dialogs.NoUpdatesAvailableDialog(Application.Current.MainWindow, applicationName, currentVersionNumber);
            }
            return true;
        }
        #endregion

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
            try
            {
                Version version1 = new(versionNumber1);
                Version version2 = new(versionNumber2);
                return version1 > version2;
            }
            catch 
            {
                return false;
            }
        }

        /// <summary>
        /// Compare version numbers 
        /// </summary>
        /// <param name="versionNumber1"></param>
        /// <param name="versionNumber2"></param>
        /// <returns>True if versionNumber1 is greater than or equal to versionNumber2</returns>
        public static bool IsVersion1GreaterOrEqualToVersion2(string versionNumber1, string versionNumber2)
        {
            Version version1 = new(versionNumber1);
            Version version2 = new(versionNumber2);
            return version1 >= version2;
        }
        #endregion
    }
}
