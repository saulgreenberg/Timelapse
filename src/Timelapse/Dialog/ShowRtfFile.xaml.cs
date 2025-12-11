using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;
using System.Windows.Resources;
using Timelapse.DebuggingSupport;
using Timelapse.Util;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Show an rtf file in a message. The rtf file is usually a resource, but it doesn't have to be.
    /// </summary>
    public partial class ShowRtfFile
    {
        private string MessageTitle { get;  }
        private string MessageWhat { get; }
        private string Filename { get;  }
        public ShowRtfFile(Window owner, string title, string what, string fileName)
        {
            InitializeComponent();
            FormattedDialogHelper.SetupStaticReferenceResolver(Message);
            Owner = owner;
            MessageTitle = title;
            MessageWhat = what;
            Filename = fileName;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dialogs.TryPositionAndFitDialogIntoWindow(this);
            // Construct the template message
            Title = MessageTitle;

            Message.DialogTitle = MessageTitle;
            Message.What = MessageWhat;
            Message.BuildContentFromProperties();

            // Create a flow document and load it with the contents of the file
            try
            {
                // Create a flow document
                FlowDocument content = new()
                {
                    FontFamily = new("Segui UI"),
                    FontSize = 12
                };

                // create a string containing the help text from the rtf help file
                StreamResourceInfo sri = Application.GetResourceStream(new(Filename));
                if (null == sri?.Stream)
                {
                    TracePrint.NullException(nameof(sri));
                    DialogResult = false;
                    return;
                }

                StreamReader reader = new(sri.Stream);
                string helpText = reader.ReadToEnd();

                // Write the help text to a stream
                MemoryStream stream = new();
                StreamWriter writer = new(stream);
                writer.Write(helpText);
                writer.Flush();

                // Load the entire text into the Flow Document
                TextRange textRange = new(content.ContentStart, content.ContentEnd);
                textRange.Load(stream, DataFormats.Rtf);

                // Add the document to the FlowDocumentScollViewer
                RtfFlowDocumentScrollViewer.Document = content;
                // We can now displose of the reader and write as we no longer need them.
                reader.Dispose();
                writer.Dispose();
                SubscribeToAllHyperlinks(RtfFlowDocumentScrollViewer.Document);
            }
            catch
            {
                TracePrint.NullException("In catch!");
                DialogResult = false;
            }
        }

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
