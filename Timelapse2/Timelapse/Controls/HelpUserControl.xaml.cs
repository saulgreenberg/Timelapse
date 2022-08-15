using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Resources;
using Timelapse.Util;

namespace Timelapse.Controls
{
    /// <summary>
    /// Create a control - a grid - that contains a flow document with all the help information
    /// </summary>
    public partial class HelpUserControl : UserControl
    {
        #region Public properties and Private variables
        // Substitute for parameter passing: 
        // The HelpFileProperty/ HelpFile lets us specify the location of the helpfile resource in the XAML
        // The Xaml using this user control should contain something like HelpFile="pack://application:,,/Resources/TimelapseHelp.rtf"
        public static readonly DependencyProperty HelpFileProperty =
            DependencyProperty.Register("HelpFile", typeof(string), typeof(HelpUserControl));
        public string HelpFile
        {
            get { return this.GetValue(HelpFileProperty) as string; }
            set { this.SetValue(HelpFileProperty, value); }
        }

        // Set this (before the control is loaded) to a non-English (US or CAD) language, which will be used to add a warning about regions to the document.
        public string WarningRegionLanguage { get; set; }

        private FlowDocument flowDocument;
        #endregion

        #region Constructor / Loaded
        public HelpUserControl()
        {
            this.InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            this.CreateFlowDocument();

            // Check to see if a language has been set. If so, warn the user that they may be better off setting en-US or en-CAN as the region
            if (!String.IsNullOrEmpty(this.WarningRegionLanguage))
            {
                this.InsertCultureWarning();
            }
        }
        #endregion

        #region Public Insert Culture Warning
        // Insert a warning into the beginning of the document about possible region issues. 
        public void InsertCultureWarning()
        {
            Paragraph p1 = new Paragraph
            {
                Foreground = Brushes.DarkRed,
                FontFamily = new FontFamily("Segui UI"),
                FontSize = 12,
                FontWeight = FontWeights.Normal,
            };

            Run run1 = new Run
            {
                FontWeight = FontWeights.Bold,
                FontSize = 18,
                Text = "Warning about your current Window's 'Region' setting",
            };

            Run run2 = new Run
            {
                FontWeight = FontWeights.Normal,
                Text = "Timelapse was designed to work best in ",
            };
            Run run3 = new Run
            {
                FontWeight = FontWeights.Bold,
                Text = "English(US)",
            };

            Run run4 = new Run
            {
                FontWeight = FontWeights.Normal,
                Text = ". However, your Windows region setting is ",
            };

            Run run5 = new Run
            {
                FontWeight = FontWeights.Bold,
                Text = this.WarningRegionLanguage,
            };

            Run run6 = new Run
            {
                FontWeight = FontWeights.Normal,
                Text = ", which may format dates and numbers different from what Timelapse expects. Avoid possible issues by setting your Region and Region Format(date, time, numbers) to either English(US) or English(Canada) via your Windows Control Panel."
            };

            p1.Inlines.Add(run1);
            p1.Inlines.Add(Environment.NewLine);
            p1.Inlines.Add(Environment.NewLine);
            p1.Inlines.Add(run2);
            p1.Inlines.Add(run3);
            p1.Inlines.Add(run4);
            p1.Inlines.Add(run5);
            p1.Inlines.Add(run6);

            this.flowDocument.Blocks.InsertBefore(this.flowDocument.Blocks.FirstBlock, p1);
        }
        #endregion

        #region Private: Create Flow Document
        // Create a flow document containing the contents of the resource specified in HelpFile
        private void CreateFlowDocument()
        {
            this.flowDocument = new FlowDocument();
            try
            {
                // create a string containing the help text from the rtf help file
                StreamResourceInfo sri = Application.GetResourceStream(new Uri(this.HelpFile));
                StreamReader reader = new StreamReader(sri.Stream);
                string helpText = reader.ReadToEnd();

                // Write the help text to a stream
                MemoryStream stream = new MemoryStream();
                StreamWriter writer = new StreamWriter(stream);
                writer.Write(helpText);
                writer.Flush();

                // Load the entire text into the Flow Document
                TextRange textRange = new TextRange(this.flowDocument.ContentStart, this.flowDocument.ContentEnd);
                textRange.Load(stream, DataFormats.Rtf);

                if (reader != null)
                {
                    reader.Dispose();
                }
                if (writer != null)
                {
                    writer.Dispose();
                }
            }
            catch
            {
                // We couldn't get the help file. Display a generic message instead
                this.flowDocument.FontFamily = new FontFamily("Segui UI");
                this.flowDocument.FontSize = 14;
                Paragraph p1 = new Paragraph();
                p1.Inlines.Add("Brief instructions are currently unavailable.");
                p1.Inlines.Add(Environment.NewLine + Environment.NewLine);
                p1.Inlines.Add("If you need help, please download and read the ");
                Hyperlink h1 = new Hyperlink();
                h1.Inlines.Add("Timelapse Tutorial Manual");
                h1.NavigateUri = Constant.ExternalLinks.UserManualLink;
                h1.RequestNavigate += this.Link_RequestNavigate;
                p1.Inlines.Add(h1);
                this.flowDocument.Blocks.Add(p1);
            }
            // Add the document to the FlowDocumentScollViewer, converting hyperlinks to active links
            this.SubscribeToAllHyperlinks(this.flowDocument);
            this.ScrollViewer.Document = this.flowDocument;
        }
        #endregion

        #region Private: Activate all hyperlinks in the flow document
        private void SubscribeToAllHyperlinks(FlowDocument flowDocument)
        {
            var hyperlinks = GetVisuals(flowDocument).OfType<Hyperlink>();
            foreach (var link in hyperlinks)
            {
                link.RequestNavigate += new System.Windows.Navigation.RequestNavigateEventHandler(this.Link_RequestNavigate);
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

        // Load the Uri provided in a web browser  
        private void Link_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            ProcessExecution.TryProcessStart(e.Uri);
            e.Handled = true;
        }
        #endregion
    }
}
