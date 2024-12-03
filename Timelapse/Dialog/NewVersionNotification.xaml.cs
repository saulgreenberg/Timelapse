using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using Timelapse.Constant;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// NewVersionNotification.xaml
    /// Displays a dialog box with pretty-printed version change information retrieved from the web.
    /// </summary>
    public partial class NewVersionNotification
    {
        #region Constructor, Loaded
        public NewVersionNotification(Window owner, string applicationName, Version currentVersionNumber, Version latestVersionNumber)
        {

            InitializeComponent();
            Owner = owner;

            // Construct the template message
            Title = $"A new version of {applicationName} is available.";

            Message.Title = Title;
            Message.What = $"A new {applicationName} version is available: {latestVersionNumber}";
            Message.What += Environment.NewLine;
            Message.What += $"You are running an older version:       {currentVersionNumber} ";

            Message.Reason = "We always recommend updating. Updates include bug fixes, enhancements, new features, and more. ";
            Message.Reason += Environment.NewLine + "Select 'Download New Version' to download it at the Timelapse download page.";

            // Create a flow document and load it with the contents of the file
            try
            {
                // Create a flow document
                FlowDocument content = new FlowDocument
                {
                    FontFamily = new FontFamily("Segui UI"),
                    FontSize = 12
                };
                TextRange textRange = new TextRange(content.ContentStart, content.ContentEnd);

                // Try to load the rtf file pointed at by the URI as a string
                string filename = VersionUpdates.LatestVersionFileNamePrefix + $"{latestVersionNumber}" + VersionUpdates.LatestVersionFileNameSuffix;
                Uri uri = new Uri(VersionUpdates.LatestVersionBaseAddress, filename);
                WebResponse response = WebRequest.Create(uri).GetResponse();
                Stream streamfromuri = response.GetResponseStream();
                if (streamfromuri == null)
                {
                    throw new ArgumentNullException(nameof(streamfromuri), "Unexpected null");
                }
                using (StreamReader reader = new StreamReader(streamfromuri))
                {
                    string s = reader.ReadToEnd();

                    // Convert the string to a stream
                    MemoryStream stream = new MemoryStream();
                    using (StreamWriter writer = new StreamWriter(stream))
                    {
                        writer.Write(s);
                        writer.Flush();
                        stream.Position = 0;

                        // Load the stream into the Flow Document, converting hyperlinks to active links
                        textRange.Load(stream, DataFormats.Rtf);
                        SubscribeToAllHyperlinks(content);

                        // Add the document to the FlowDocumentScollViewer
                        ChangeDescription.Document = content;
                    }
                }
            }
            catch
            {
                // We couldn't get the version notes. Display a generic message instead
                FlowDocument content = new FlowDocument
                {
                    FontFamily = new FontFamily("Segui UI"),
                    FontSize = 12
                };
                Paragraph p1 = new Paragraph();
                p1.Inlines.Add("See version change details at: ");
                Hyperlink h1 = new Hyperlink();
                h1.Inlines.Add("Timelapse Version History Page");
                h1.NavigateUri = ExternalLinks.TimlapseVersionChangesLink;
                h1.RequestNavigate += Link_RequestNavigate;
                p1.Inlines.Add(h1);
                content.Blocks.Add(p1);
                ChangeDescription.Document = content;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
        }
        #endregion

        #region Activate Hyperlinks in the flow document
        private void SubscribeToAllHyperlinks(FlowDocument flowDocument)
        {
            var hyperlinks = GetVisuals(flowDocument).OfType<Hyperlink>();
            foreach (var link in hyperlinks)
            {
                link.RequestNavigate += Link_RequestNavigate;
            }
        }

        private static IEnumerable<DependencyObject> GetVisuals(DependencyObject root)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            {
                yield return child;
                foreach (var descendants in GetVisuals(child))
                {
                    yield return descendants;
                }
            }
        }

        private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            ProcessExecution.TryProcessStart(e.Uri);
            e.Handled = true;
        }
        #endregion Activate Hyperlinks in the Rich Text box

        #region Button Callbacks

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
        #endregion
    }
}
