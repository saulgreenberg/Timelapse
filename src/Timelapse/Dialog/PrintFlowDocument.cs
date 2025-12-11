using System;
using System.IO;
using System.IO.Packaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;
using Timelapse.DebuggingSupport;

namespace Timelapse.Dialog
{
    /// <summary>
    /// Utility class for printing FlowDocument content with optional print preview and export capabilities.
    /// </summary>
    public static class PrintFlowDocument
    {
        #region Public Methods - Printing

        /// <summary>
        /// Shows print dialog and prints the FlowDocument if user clicks OK.
        /// </summary>
        /// <param name="document">The FlowDocument to print</param>
        /// <param name="documentName">Title shown in print dialog and print queue</param>
        /// <param name="owner">Optional owner window for the print dialog</param>
        /// <param name="pageMargin">Optional custom page margins (default: 1 inch all sides)</param>
        /// <returns>True if printed successfully, false if cancelled or error occurred</returns>
        public static bool Print(FlowDocument document, string documentName, Window owner = null, Thickness? pageMargin = null)
        {
            if (document == null)
            {
                TracePrint.PrintMessage("PrintFlowDocument.Print: document is null");
                return false;
            }

            try
            {
                // Create print dialog
                PrintDialog printDialog = new PrintDialog();

                // Set owner if provided
                if (owner != null)
                {
                    // Note: PrintDialog doesn't have Owner property, but we can work around this
                    // by using WindowInteropHelper if needed in future
                }

                // Show print dialog
                bool? result = printDialog.ShowDialog();
                if (result != true)
                {
                    return false; // User cancelled
                }

                // Apply page margins if specified
                document.PagePadding = pageMargin ?? new Thickness(96);

                // Set page size to match printer
                document.PageHeight = printDialog.PrintableAreaHeight;
                document.PageWidth = printDialog.PrintableAreaWidth;

                // Print the document
                IDocumentPaginatorSource paginator = document;
                printDialog.PrintDocument(paginator.DocumentPaginator, documentName);

                return true;
            }
            catch (Exception ex)
            {
                TracePrint.PrintMessage($"PrintFlowDocument.Print: Error printing document - {ex.Message}");
                MessageBox.Show(
                    $"Failed to print document:{Environment.NewLine}{ex.Message}",
                    "Print Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Shows print preview window with built-in print button.
        /// </summary>
        /// <param name="document">The FlowDocument to preview/print</param>
        /// <param name="documentName">Window title and print job name</param>
        /// <param name="owner">Parent window</param>
        /// <param name="pageMargin">Optional custom page margins</param>
        /// <returns>True if user printed from preview, false if cancelled</returns>
        public static bool PrintWithPreview(FlowDocument document, string documentName, Window owner, Thickness? pageMargin = null)
        {
            if (document == null)
            {
                TracePrint.PrintMessage("PrintFlowDocument.PrintWithPreview: document is null");
                return false;
            }

            try
            {
                // Apply page margins
                document.PagePadding = pageMargin ?? new Thickness(96); // 1 inch default

                // Set reasonable default page size for preview
                document.PageHeight = 11 * 96; // 11 inches at 96 DPI
                document.PageWidth = 8.5 * 96; // 8.5 inches at 96 DPI

                // Show preview window
                return ShowPrintPreview(document, documentName, owner);
            }
            catch (Exception ex)
            {
                TracePrint.PrintMessage($"PrintFlowDocument.PrintWithPreview: Error - {ex.Message}");
                MessageBox.Show(
                    $"Failed to show print preview:{Environment.NewLine}{ex.Message}",
                    "Print Preview Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        #endregion

        #region Public Methods - Export

        /// <summary>
        /// Exports the FlowDocument to XPS format.
        /// XPS files can be viewed in Windows and converted to PDF using third-party tools.
        /// </summary>
        /// <param name="document">The FlowDocument to export</param>
        /// <param name="filePath">Full path where XPS file should be saved</param>
        /// <param name="pageMargin">Optional custom page margins</param>
        /// <returns>True if exported successfully, false otherwise</returns>
        public static bool ExportToXps(FlowDocument document, string filePath, Thickness? pageMargin = null)
        {
            if (document == null)
            {
                TracePrint.PrintMessage("PrintFlowDocument.ExportToXps: document is null");
                return false;
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                TracePrint.PrintMessage("PrintFlowDocument.ExportToXps: filePath is null or empty");
                return false;
            }

            try
            {
                // Ensure directory exists
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Apply page margins
                document.PagePadding = pageMargin ?? new Thickness(96);

                // Set page size
                document.PageHeight = 11 * 96; // 11 inches
                document.PageWidth = 8.5 * 96; // 8.5 inches

                // Delete existing file if present
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                // Create XPS document
                using Package package = Package.Open(filePath, FileMode.Create, FileAccess.ReadWrite);
                using XpsDocument xpsDocument = new XpsDocument(package, CompressionOption.Maximum);
                // Get paginator
                IDocumentPaginatorSource paginator = document;

                // Write to XPS
                XpsDocumentWriter writer = XpsDocument.CreateXpsDocumentWriter(xpsDocument);
                writer.Write(paginator.DocumentPaginator);

                return true;
            }
            catch (Exception ex)
            {
                TracePrint.PrintMessage($"PrintFlowDocument.ExportToXps: Error - {ex.Message}");
                MessageBox.Show(
                    $"Failed to export document:{Environment.NewLine}{ex.Message}",
                    "Export Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        #endregion

        #region Public Methods - Page Setup

        /// <summary>
        /// Shows page setup dialog to configure print settings.
        /// Note: WPF doesn't have a built-in page setup dialog, so this shows the print dialog instead.
        /// For true page setup, would need to use Windows Forms PageSetupDialog or create custom dialog.
        /// </summary>
        /// <param name="owner">Optional owner window</param>
        public static void ShowPageSetup(Window owner = null)
        {
            try
            {
                // WPF doesn't have a native page setup dialog
                // Options:
                // 1. Use System.Windows.Forms.PageSetupDialog (requires WindowsForms reference)
                // 2. Show PrintDialog (which includes some page setup options)
                // 3. Create custom page setup dialog

                // For now, we'll show the print dialog which has some page setup options
                PrintDialog printDialog = new PrintDialog();
                printDialog.ShowDialog();

                // Note: If you want full page setup functionality, you would need to:
                // 1. Add reference to System.Windows.Forms
                // 2. Use System.Windows.Forms.PageSetupDialog
                // 3. Convert settings between WPF and WinForms
            }
            catch (Exception ex)
            {
                TracePrint.PrintMessage($"PrintFlowDocument.ShowPageSetup: Error - {ex.Message}");
                MessageBox.Show(
                    $"Failed to show page setup:{Environment.NewLine}{ex.Message}",
                    "Page Setup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion

        #region Private Methods - Print Preview Window

        /// <summary>
        /// Creates and shows a print preview window with DocumentViewer.
        /// Converts FlowDocument to FixedDocument for preview.
        /// </summary>
        /// <param name="document">The FlowDocument to preview</param>
        /// <param name="documentName">Window title</param>
        /// <param name="owner">Parent window</param>
        /// <returns>True if document was printed from preview, false otherwise</returns>
        private static bool ShowPrintPreview(FlowDocument document, string documentName, Window owner)
        {
            string tempFileName = null;
            XpsDocument xpsDoc = null;

            try
            {
                // Convert FlowDocument to FixedDocument via XPS
                var result = ConvertFlowDocumentToFixed(document);
                FixedDocumentSequence fixedDocument = result.Item1;
                tempFileName = result.Item2;
                xpsDoc = result.Item3;

                // Create preview window
                Window previewWindow = new Window
                {
                    Title = $"Print Preview - {documentName}",
                    Owner = owner,
                    Width = 800,
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ShowInTaskbar = false
                };

                // Create DocumentViewer with the FixedDocument
                DocumentViewer viewer = new DocumentViewer
                {
                    Document = fixedDocument
                };

                // Add viewer to window
                previewWindow.Content = viewer;

                // Clean up temp file when window closes
                previewWindow.Closed += (_, _) =>
                {
                    try
                    {
                        // Close the XpsDocument first
                        xpsDoc?.Close();

                        // Delete the temporary XPS file
                        if (!string.IsNullOrEmpty(tempFileName) && File.Exists(tempFileName))
                        {
                            File.Delete(tempFileName);
                        }
                    }
                    catch (Exception ex)
                    {
                        TracePrint.PrintMessage($"PrintFlowDocument: Error cleaning up temp file - {ex.Message}");
                    }
                };

                // Note: DocumentViewer has built-in Print button in its toolbar
                previewWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                // Clean up on error
                try
                {
                    xpsDoc?.Close();
                    if (!string.IsNullOrEmpty(tempFileName) && File.Exists(tempFileName))
                    {
                        File.Delete(tempFileName);
                    }
                }
                catch
                {
                    // Noop
                }

                TracePrint.PrintMessage($"PrintFlowDocument.ShowPrintPreview: Error - {ex.Message}");
                throw; // Re-throw to be caught by caller
            }

            return false;
        }

        /// <summary>
        /// Converts a FlowDocument to a FixedDocumentSequence for DocumentViewer.
        /// Uses temporary XPS file as intermediate format.
        /// Returns tuple of (FixedDocumentSequence, TempFileName, XpsDocument) for cleanup.
        /// </summary>
        private static Tuple<FixedDocumentSequence, string, XpsDocument> ConvertFlowDocumentToFixed(FlowDocument document)
        {
            // Create a temporary XPS file
            string tempFileName = Path.Combine(Path.GetTempPath(), $"preview_{Guid.NewGuid()}.xps");

            try
            {
                // Write the FlowDocument to XPS file
                using (Package package = Package.Open(tempFileName, FileMode.Create, FileAccess.ReadWrite))
                {
                    using (XpsDocument xpsDocument = new XpsDocument(package, CompressionOption.NotCompressed))
                    {
                        XpsDocumentWriter writer = XpsDocument.CreateXpsDocumentWriter(xpsDocument);
                        IDocumentPaginatorSource paginator = document;
                        writer.Write(paginator.DocumentPaginator);
                    }
                }

                // Read back the FixedDocumentSequence from the XPS file
                XpsDocument xpsDoc = new XpsDocument(tempFileName, FileAccess.Read);
                FixedDocumentSequence fixedDocSeq = xpsDoc.GetFixedDocumentSequence();

                // Return all three for proper cleanup later
                return Tuple.Create(fixedDocSeq, tempFileName, xpsDoc);
            }
            catch
            {
                // Clean up temp file if there was an error
                if (File.Exists(tempFileName))
                {
                    try
                    {
                        File.Delete(tempFileName);
                    } 
                    catch 
                    { 
                        // Noop
                    }
                }
                throw;
            }
        }

        #endregion

        #region Public Helper Methods - Cleanup

        /// <summary>
        /// Cleans up any leftover preview XPS files from the temp directory.
        /// Should be called on application startup to remove files from previous sessions
        /// that weren't cleaned up due to crashes or abnormal termination.
        /// </summary>
        public static void CleanupOldPreviewFiles()
        {
            try
            {
                string tempPath = Path.GetTempPath();
                string[] previewFiles = Directory.GetFiles(tempPath, "preview_*.xps");

                foreach (string file in previewFiles)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        // File might be in use or inaccessible, skip it
                        TracePrint.PrintMessage($"PrintFlowDocument: Could not delete old preview file '{file}' - {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                TracePrint.PrintMessage($"PrintFlowDocument.CleanupOldPreviewFiles: Error - {ex.Message}");
            }
        }

        #endregion

        #region Public Helper Methods - Document Creation Utilities

        /// <summary>
        /// Creates a simple FlowDocument with a title and paragraphs.
        /// Useful for quick document creation.
        /// </summary>
        /// <param name="title">Document title</param>
        /// <param name="content">Array of paragraph text</param>
        /// <returns>Formatted FlowDocument</returns>
        public static FlowDocument CreateSimpleDocument(string title, params string[] content)
        {
            FlowDocument document = new FlowDocument();

            // Add title
            if (!string.IsNullOrWhiteSpace(title))
            {
                Paragraph titlePara = new Paragraph(new Run(title))
                {
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 20)
                };
                document.Blocks.Add(titlePara);
            }

            // Add content paragraphs
            if (content != null)
            {
                foreach (string text in content)
                {
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        Paragraph para = new Paragraph(new Run(text))
                        {
                            Margin = new Thickness(0, 0, 0, 10)
                        };
                        document.Blocks.Add(para);
                    }
                }
            }

            return document;
        }

        /// <summary>
        /// Creates a FlowDocument with a table.
        /// Useful for structured data like keyboard shortcuts.
        /// </summary>
        /// <param name="title">Document title</param>
        /// <param name="columnHeaders">Headers for table columns</param>
        /// <param name="rows">Table data (each row is an array of cell values)</param>
        /// <returns>Formatted FlowDocument with table</returns>
        public static FlowDocument CreateTableDocument(string title, string[] columnHeaders, string[][] rows)
        {
            FlowDocument document = new FlowDocument();

            // Add title
            if (!string.IsNullOrWhiteSpace(title))
            {
                Paragraph titlePara = new Paragraph(new Run(title))
                {
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 20)
                };
                document.Blocks.Add(titlePara);
            }

            // Create table
            if (columnHeaders is { Length: > 0 })
            {
                Table table = new Table
                {
                    CellSpacing = 0,
                    BorderBrush = System.Windows.Media.Brushes.Black,
                    BorderThickness = new Thickness(1)
                };

                // Define columns
                for (int i = 0; i < columnHeaders.Length; i++)
                {
                    table.Columns.Add(new TableColumn());
                }

                // Create header row group
                table.RowGroups.Add(new TableRowGroup());
                TableRow headerRow = new TableRow
                {
                    Background = System.Windows.Media.Brushes.LightGray
                };

                foreach (string header in columnHeaders)
                {
                    TableCell cell = new TableCell(new Paragraph(new Run(header)))
                    {
                        FontWeight = FontWeights.Bold,
                        Padding = new Thickness(5),
                        BorderBrush = System.Windows.Media.Brushes.Black,
                        BorderThickness = new Thickness(1)
                    };
                    headerRow.Cells.Add(cell);
                }
                table.RowGroups[0].Rows.Add(headerRow);

                // Add data rows
                if (rows is { Length: > 0 })
                {
                    TableRowGroup dataRowGroup = new TableRowGroup();
                    foreach (string[] rowData in rows)
                    {
                        TableRow row = new TableRow();
                        for (int i = 0; i < columnHeaders.Length && i < rowData.Length; i++)
                        {
                            TableCell cell = new TableCell(new Paragraph(new Run(rowData[i] ?? string.Empty)))
                            {
                                Padding = new Thickness(5),
                                BorderBrush = System.Windows.Media.Brushes.Black,
                                BorderThickness = new Thickness(1)
                            };
                            row.Cells.Add(cell);
                        }
                        dataRowGroup.Rows.Add(row);
                    }
                    table.RowGroups.Add(dataRowGroup);
                }

                document.Blocks.Add(table);
            }

            return document;
        }

        #endregion

        #region Public Helper Methods - FormattedMessageContent Conversion

        /// <summary>
        /// Creates a FlowDocument from FormattedMessageContent markup text.
        /// Converts markup tags like [b], [li], [br] to FlowDocument structure.
        /// </summary>
        /// <param name="formattedContent">Text with FormattedMessageContent markup</param>
        /// <param name="title">Optional document title</param>
        /// <param name="fontSize">Base font size (default: 11)</param>
        /// <param name="fontFamily">Font family name (default: "Segoe UI")</param>
        /// <returns>Formatted FlowDocument ready for printing</returns>
        public static FlowDocument CreateDocumentFromFormattedText(
            string formattedContent,
            string title = null,
            double fontSize = 11,
            string fontFamily = "Segoe UI")
        {
            FlowDocument document = new FlowDocument
            {
                FontFamily = new System.Windows.Media.FontFamily(fontFamily),
                FontSize = fontSize
            };

            // Add title if provided
            if (!string.IsNullOrWhiteSpace(title))
            {
                Paragraph titlePara = new Paragraph(new Run(title))
                {
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 20)
                };
                document.Blocks.Add(titlePara);
            }

            // Convert formatted content to FlowDocument structure
            Section contentSection = new Section();
            ConvertFormattedTextToFlowDocument(contentSection, formattedContent, fontSize);
            document.Blocks.Add(contentSection);

            return document;
        }

        /// <summary>
        /// Converts FormattedMessageContent markup to FlowDocument blocks.
        /// Handles formatting tags: [b], [i], [e], [li], [ni], [br], [f]
        /// </summary>
        private static void ConvertFormattedTextToFlowDocument(Section section, string formattedText, double baseFontSize)
        {
            // Process text sequentially, handling sections, line breaks, and list items
            // Split by both [br] tags and list item markers to process in order
            var pattern = @"(\[br\s*\d*\]|\[li(?:\s+\d)?\]|\[ni(?:\s+\d)?\])";
            var parts = System.Text.RegularExpressions.Regex.Split(formattedText, pattern);

            Paragraph currentParagraph = null;
            double nextTopMargin = 0; // Track spacing from [br] tags

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];

                if (string.IsNullOrWhiteSpace(part)) continue;

                // Check if this is a [br] tag
                var brMatch = System.Text.RegularExpressions.Regex.Match(part, @"^\[br\s*(\d*)\]$");
                if (brMatch.Success)
                {
                    // Finish current paragraph if any
                    if (currentParagraph is { Inlines.Count: > 0 })
                    {
                        section.Blocks.Add(currentParagraph);
                        currentParagraph = null;
                    }

                    // Extract spacing parameter from [br #] - default to 6 if not specified
                    if (brMatch.Groups[1].Success && double.TryParse(brMatch.Groups[1].Value, out double spacing))
                    {
                        nextTopMargin = spacing;
                    }
                    else
                    {
                        nextTopMargin = 6; // Default spacing
                    }
                    continue;
                }

                // Check if this is a list item marker [li] or [ni]
                var listMatch = System.Text.RegularExpressions.Regex.Match(part, @"^\[(li|ni)(?:\s+(\d))?\]$");
                if (listMatch.Success)
                {
                    // Finish current paragraph if any
                    if (currentParagraph is { Inlines.Count: > 0 })
                    {
                        section.Blocks.Add(currentParagraph);
                        currentParagraph = null;
                    }

                    // Extract indentation level (default to 1 if not specified)
                    int indentLevel = 1;
                    if (listMatch.Groups[2].Success && int.TryParse(listMatch.Groups[2].Value, out int level))
                    {
                        indentLevel = level;
                    }

                    // Get the content after this marker (next part)
                    if (i + 1 < parts.Length)
                    {
                        string listContent = parts[i + 1].Trim();
                        if (!string.IsNullOrWhiteSpace(listContent))
                        {
                            // Calculate modest indentation: Level 1=20px, Level 2=35px, Level 3=50px
                            double leftPadding = 20 + ((indentLevel - 1) * 15);

                            // For level 2, use manual marker for smaller bullet
                            // Otherwise use built-in markers
                            TextMarkerStyle markerStyle;
                            bool useManualMarker = false;
                            string manualMarker = "";

                            if (indentLevel == 2)
                            {
                                // Level 2: Use None and manually add white bullet (smaller)
                                markerStyle = TextMarkerStyle.None;
                                useManualMarker = true;
                                manualMarker = "◦ ";  // U+25E6 White Bullet (smaller hollow circle)
                            }
                            else
                            {
                                // Other levels use built-in markers
                                markerStyle = indentLevel switch
                                {
                                    1 => TextMarkerStyle.Disc,   // Filled circle (•)
                                    3 => TextMarkerStyle.Square, // Filled square (■)
                                    _ => TextMarkerStyle.Box     // Hollow square for deeper levels
                                };
                            }

                            // Create a proper FlowDocument List for correct hanging indent
                            List list = new List
                            {
                                Margin = new Thickness(0, nextTopMargin, 0, 0),
                                Padding = new Thickness(leftPadding, 0, 0, 0),
                                MarkerStyle = markerStyle
                            };

                            // Reset top margin after using it
                            nextTopMargin = 0;

                            // Create list item with content
                            ListItem item = new ListItem();
                            Paragraph itemPara = new Paragraph
                            {
                                Margin = new Thickness(0)
                            };

                            // Add manual marker if needed and adjust for hanging indent
                            if (useManualMarker)
                            {
                                // Add manual marker and create hanging indent
                                // TextIndent pulls first line to the left, creating hanging indent effect
                                itemPara.TextIndent = -15;
                                itemPara.Inlines.Add(new Run(manualMarker));
                            }

                            // Add formatted content
                            AddFormattedRuns(itemPara, listContent, baseFontSize);
                            item.Blocks.Add(itemPara);
                            list.ListItems.Add(item);

                            section.Blocks.Add(list);
                        }
                        i++; // Skip the content part since we processed it
                    }
                    continue;
                }

                // This is regular content - add to current paragraph or create new one
                if (currentParagraph == null)
                {
                    currentParagraph = new Paragraph
                    {
                        Margin = new Thickness(0, nextTopMargin, 0, 2)
                    };
                    // Reset top margin after using it
                    nextTopMargin = 0;
                }

                AddFormattedRuns(currentParagraph, part, baseFontSize);
            }

            // Add any remaining paragraph
            if (currentParagraph is { Inlines.Count: > 0 })
            {
                section.Blocks.Add(currentParagraph);
            }
        }

        /// <summary>
        /// Adds formatted text runs to a paragraph, handling basic markup.
        /// Processes [b], [i], [e], and [f] tags to apply formatting.
        /// </summary>
        private static void AddFormattedRuns(Paragraph para, string text, double baseFontSize = 11)
        {
            // Process all formatting tags by finding them in order of appearance
            // Pattern matches any of: [b]...[/b], [i]...[/i], [e]...[/e], [f N]...[/f]
            var pattern = @"\[([bief])\s*([-+]?\d*)\](.*?)\[/\1\]";
            var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern);

            if (matches.Count > 0)
            {
                int lastPos = 0;
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    // Add text before match
                    if (match.Index > lastPos)
                    {
                        string beforeText = text.Substring(lastPos, match.Index - lastPos);
                        AddSimpleFormattedRuns(para, beforeText, baseFontSize: baseFontSize);
                    }

                    // Determine formatting type and apply it
                    string tagType = match.Groups[1].Value;
                    string parameter = match.Groups[2].Value; // Will be empty for [b] and [i], number for [f N]
                    string content = match.Groups[3].Value;

                    if (tagType == "b")
                    {
                        // Bold text - check if content has nested formatting
                        if (HasFormattingTags(content))
                        {
                            // Create a span for bold and recursively process nested formatting
                            var boldSpan = new Bold();
                            ProcessNestedFormatting(boldSpan.Inlines, content, baseFontSize);
                            para.Inlines.Add(boldSpan);
                        }
                        else
                        {
                            AddSimpleFormattedRuns(para, content, FontWeights.Bold, baseFontSize: baseFontSize);
                        }
                    }
                    else if (tagType == "i")
                    {
                        // Italic text - check if content has nested formatting
                        if (HasFormattingTags(content))
                        {
                            var italicSpan = new Italic();
                            ProcessNestedFormatting(italicSpan.Inlines, content, baseFontSize);
                            para.Inlines.Add(italicSpan);
                        }
                        else
                        {
                            AddSimpleFormattedRuns(para, content, null, FontStyles.Italic, baseFontSize: baseFontSize);
                        }
                    }
                    else if (tagType == "e")
                    {
                        // Emphasis - semi-bold italic - check if content has nested formatting
                        if (HasFormattingTags(content))
                        {
                            var emphasisSpan = new Span
                            {
                                FontWeight = FontWeights.SemiBold,
                                FontStyle = FontStyles.Italic
                            };
                            ProcessNestedFormatting(emphasisSpan.Inlines, content, baseFontSize);
                            para.Inlines.Add(emphasisSpan);
                        }
                        else
                        {
                            AddSimpleFormattedRuns(para, content, FontWeights.SemiBold, FontStyles.Italic, baseFontSize: baseFontSize);
                        }
                    }
                    else if (tagType == "f" && int.TryParse(parameter.Trim(), out int sizeAdjustment))
                    {
                        // Font size adjustment - check if content has nested formatting
                        if (HasFormattingTags(content))
                        {
                            var sizedSpan = new Span { FontSize = baseFontSize + sizeAdjustment };
                            ProcessNestedFormatting(sizedSpan.Inlines, content, baseFontSize + sizeAdjustment);
                            para.Inlines.Add(sizedSpan);
                        }
                        else
                        {
                            AddSimpleFormattedRuns(para, content, null, null, sizeAdjustment, baseFontSize);
                        }
                    }
                    else
                    {
                        // Couldn't parse - just add as plain text
                        AddSimpleFormattedRuns(para, content, baseFontSize: baseFontSize);
                    }

                    lastPos = match.Index + match.Length;
                }

                // Add remaining text
                if (lastPos < text.Length)
                {
                    AddSimpleFormattedRuns(para, text.Substring(lastPos), baseFontSize: baseFontSize);
                }
            }
            else
            {
                AddSimpleFormattedRuns(para, text, baseFontSize: baseFontSize);
            }
        }

        /// <summary>
        /// Adds simple formatted runs, handling remaining markup like colors.
        /// Applies font weight, style, and size adjustments to the text.
        /// </summary>
        private static void AddSimpleFormattedRuns(Paragraph para, string text, FontWeight? weight = null, FontStyle? style = null, int? fontSizeAdjustment = null, double baseFontSize = 11)
        {
            // Remove or simplify remaining markup (but NOT [b], [i], [e], or [f] - those are handled by AddFormattedRuns)
            text = System.Text.RegularExpressions.Regex.Replace(text, @"#\w+\[(.*?)\]", "$1"); // Colors -> plain
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\[/?\w+\]", ""); // Remove any remaining tags

            // Clean up unicode brackets
            text = text.Replace("〔", "[").Replace("〕", "]");

            if (!string.IsNullOrWhiteSpace(text))
            {
                Run run = new Run(text);

                if (weight.HasValue)
                {
                    run.FontWeight = weight.Value;
                }

                if (style.HasValue)
                {
                    run.FontStyle = style.Value;
                }

                if (fontSizeAdjustment.HasValue)
                {
                    // Adjust font size relative to base font size
                    run.FontSize = baseFontSize + fontSizeAdjustment.Value;
                }

                para.Inlines.Add(run);
            }
        }

        /// <summary>
        /// Checks if text contains any formatting tags that need to be processed.
        /// </summary>
        private static bool HasFormattingTags(string text)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(text, @"\[([bief])\s*([-+]?\d*)\]");
        }

        /// <summary>
        /// Processes nested formatting by creating a temporary paragraph and extracting its inlines.
        /// </summary>
        private static void ProcessNestedFormatting(InlineCollection inlines, string text, double baseFontSize)
        {
            // Create a temporary paragraph to hold the formatted content
            Paragraph tempPara = new Paragraph();
            AddFormattedRuns(tempPara, text, baseFontSize);

            // Move all inlines from temp paragraph to the target collection
            while (tempPara.Inlines.Count > 0)
            {
                var inline = tempPara.Inlines.FirstInline;
                tempPara.Inlines.Remove(inline);
                inlines.Add(inline);
            }
        }

        #endregion
    }
}
