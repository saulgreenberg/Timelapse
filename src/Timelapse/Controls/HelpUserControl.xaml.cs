using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Resources;
using Timelapse.Constant;
using Timelapse.DebuggingSupport;
using Timelapse.Util;

namespace Timelapse.Controls
{
    /// <summary>
    /// Create a control - a grid - that contains a flow document with all the help information
    /// </summary>
    public partial class HelpUserControl
    {
        #region Public properties and Private variables
        // Substitute for parameter passing: 
        // The HelpFileProperty/ HelpFile lets us specify the location of the helpfile resource in the XAML
        // The Xaml using this user control should contain something like HelpFile="pack://application:,,/Resources/TimelapseHelp.rtf"
        public static readonly DependencyProperty HelpFileProperty =
            DependencyProperty.Register(nameof(HelpFile), typeof(string), typeof(HelpUserControl));
        public string HelpFile
        {
            get => GetValue(HelpFileProperty) as string;
            set => SetValue(HelpFileProperty, value);
        }

        // Set this (before the control is loaded) to a non-English (US or CAD) language, which will be used to add a warning about regions to the document.
        public string WarningRegionLanguage { get; set; }

        private FlowDocument flowDocument;
        #endregion

        #region Constructor / Loaded
        public HelpUserControl()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            CreateFlowDocument();

            // Check to see if a language has been set. If so, warn the user that they may be better off setting en-US or en-CAN as the region
            if (!string.IsNullOrEmpty(WarningRegionLanguage))
            {
                InsertCultureWarning();
            }
        }
        #endregion

        #region Public Insert Culture Warning
        // Insert a warning into the beginning of the document about possible region issues. 
        public void InsertCultureWarning()
        {
            Paragraph p1 = new()
            {
                Foreground = Brushes.DarkRed,
                FontFamily = new("Segui UI"),
                FontSize = 12,
                FontWeight = FontWeights.Normal,
            };

            Run run1 = new()
            {
                FontWeight = FontWeights.Bold,
                FontSize = 18,
                Text = "Warning about your current Window's 'Region' setting",
            };

            Run run2 = new()
            {
                FontWeight = FontWeights.Normal,
                Text = "Timelapse was designed to work best in ",
            };
            Run run3 = new()
            {
                FontWeight = FontWeights.Bold,
                Text = "English(US)",
            };

            Run run4 = new()
            {
                FontWeight = FontWeights.Normal,
                Text = ". However, your Windows region setting is ",
            };

            Run run5 = new()
            {
                FontWeight = FontWeights.Bold,
                Text = WarningRegionLanguage,
            };

            Run run6 = new()
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

            if (null != flowDocument.Blocks.FirstBlock)
            {
                TracePrint.NullException(nameof(flowDocument.Blocks.FirstBlock));
                flowDocument.Blocks.InsertBefore(flowDocument.Blocks.FirstBlock, p1);
            }
            else
            {
                flowDocument.Blocks.Add(p1);
            }
        }
        #endregion

        #region Private: Create Flow Document
        // Create a flow document containing the contents of the resource specified in HelpFile
        private void CreateFlowDocument()
        {
            flowDocument = new();
            try
            {
                // create a string containing the help text from the rtf help file
                StreamResourceInfo sri = Application.GetResourceStream(new(HelpFile));
                if (null == sri)
                {
                    TracePrint.NullException(nameof(sri));
                    throw new("Could not get the help file from the resource");
                }
                StreamReader reader = new(sri.Stream);
                string helpText = reader.ReadToEnd();

                // Write the help text to a stream
                MemoryStream stream = new();
                StreamWriter writer = new(stream);
                writer.Write(helpText);
                writer.Flush();

                // Load the entire text into the Flow Document
                TextRange textRange = new(flowDocument.ContentStart, flowDocument.ContentEnd);
                textRange.Load(stream, DataFormats.Rtf);

                // We can now displose of the reader and write as we no longer need them.
                reader.Dispose();
                writer.Dispose();
            }
            catch
            {
                // We couldn't get the help file. Display a generic message instead
                flowDocument.FontFamily = new("Segui UI");
                flowDocument.FontSize = 14;
                Paragraph p1 = new();
                p1.Inlines.Add("Brief instructions are currently unavailable.");
                p1.Inlines.Add(Environment.NewLine + Environment.NewLine);
                p1.Inlines.Add("If you need help, read the Timelapse QuickStart Guide on this page: ");
                Hyperlink h1 = new();
                h1.Inlines.Add("Timelapse Tutorial Manual");
                h1.NavigateUri = new(ExternalLinks.TimelapseGuidesPage);
                h1.RequestNavigate += Link_RequestNavigate;
                p1.Inlines.Add(h1);
                flowDocument.Blocks.Add(p1);
            }
            // Add the document to the FlowDocumentScollViewer, converting hyperlinks to active links
            SubscribeToAllHyperlinks(flowDocument);
            ScrollViewer.Document = flowDocument;
        }
        #endregion

        #region Private: Activate all hyperlinks in the flow document
        private void SubscribeToAllHyperlinks(FlowDocument myflowDocument)
        {
            var hyperlinks = GetVisuals(myflowDocument).OfType<Hyperlink>();
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

        // Load the Uri provided in a web browser  
        private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            ProcessExecution.TryProcessStart(e.Uri);
            e.Handled = true;
        }
        #endregion
    }
}
