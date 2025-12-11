using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace TimelapseWpf.Toolkit
{
    // FormattedMessageContent: A reusable WPF user control for displaying formatted text messages
    // Contains header area (icon + title) and scrollable content area with rich text formatting
    // Supports the same property-based interface as FormattedDialog for consistent usage

    // FORMATTING DIRECTIVES:
    // All text properties (What, Problem, Reason, Solution, Result, Hint, Details) support the following formatting:
    //
    // Text Styling:
    //   [b]bold[/b]   - Semi-bold text: [b]This is semi-bold[/b]
    //   [i]italic[/i] - Italic text: [i]This is italic[/i]
    //   [u]underline[/u] - Underlined text: [u]This is underlined[/u]
    //   [e]emphasis[/e] - Semi-bold italic text: [e]This is semi-bold italic[/e]
    //   [f #]text[/f] - Font size adjustment: [f 4]Larger text[/f] [f -2]Smaller text[/f]
    //                   Parameter adds/subtracts points from current font size (range: -10 to +20)
    //
    // Colors:
    //   #Color[text] - Colored text: #Red[This is red] #Goldenrod[Goldenrod] #PaleGreen[PaleGreen]
    //                  Supports all System.Windows.Media colors (140+ names) and hex colors (#FF0000)
    //                  Examples: Crimson, DeepSkyBlue, ForestGreen, CornflowerBlue, DarkOrchid, etc.
    //
    // Links:
    //   [link:url|display text] - Clickable hyperlink: [link:https://example.com|Click here]
    //
    // Line Breaks and Spacing:
    //   [br]         - Single line break: Line 1[br]Line 2
    //   [br 10]      - Line break with custom spacing: Line 1[br 10]Line 2 (10pt spacing)
    //
    // Lists (using directive syntax):
    //   [li]text     - Bullet list item: [li]First item
    //   [li 2]text   - Indented bullet (level 2): [li 2]Nested item
    //   [li 3]text   - Deeply nested bullet (level 3): [li 3]Deep item
    //   [ni]text     - Numbered list item: [ni]First item (1.)
    //   [ni 2]text   - Indented number (level 2): [ni 2]Nested item (a))
    //   [ni 3]text   - Deeply nested number (level 3): [ni 3]Deep item (i))
    //
    // Example combining multiple formats:
    //   "[b]Problem:[/b] The system encountered an error.[br 8]#Red[Critical failure] occurred in module XYZ.[br][li]Check system logs[li]Restart service[li 2]Verify configuration[br]For help: [link:https://support.example.com|Contact Support]"

    // NOTE: This control was created using AI prompting 
    public partial class FormattedMessageContent
    {
        #region Properties
        
        // String properties for organizing dialog content into structured sections
        // Each property supports formatting directives: **bold**, *italic*, __underline__, #Color[text], [link:url|display text]
        
        // Dialog title displayed in header area
        public string DialogTitle { get; set; } = "";
        
        // Description of what the dialog is about
        public string What { get; set; } = "";
        
        // Description of a problem or issue
        public string Problem { get; set; } = "";
        
        // Explanation of why something occurred
        public string Reason { get; set; } = "";
        
        // Proposed solution or action
        public string Solution { get; set; } = "";
        
        // Outcome or expected result
        public string Result { get; set; } = "";
        
        // Additional tips or helpful information
        public string Hint { get; set; } = "";
        
        // Detailed information displayed in a separate section with horizontal separator
        public string Details { get; set; } = "";
        
        // Icon to display in the dialog header (Error, Information, Warning, Question, or None)
        public DialogIconType Icon { get; set; } = DialogIconType.None;

        // If true, property names (headings) will not be shown and content will be unindented
        public bool NoHeadings { get; set; } = false;

        // Whether to show the explanation visibility toggle
        public bool ShowExplanationVisibility
        {
            get => HideText.Visibility == Visibility.Visible;
            set
            {
                HideText.Visibility = value
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                SetExplanationVisibility();
            }
        }

        // Optional callback for resolving static references in formatted text
        // Signature: (namespacePrefix, className, memberName) => resolvedValue
        // Example: ("constant", "ExternalLinks", "TimelapseGuideReference") => "https://example.com"
        // If null, static references will not be resolved
        public Func<string, string, string, string> StaticReferenceResolver { get; set; }

        #endregion

        #region Constructors

        // Default constructor - initializes the user control with empty content
        public FormattedMessageContent()
        {
            InitializeComponent();
        }

        #endregion

        #region Main Content Building

        // Main method to build dialog content from string properties
        // This is the primary entry point for property-based content creation
        // Call this after setting properties to generate the visual content
        public void BuildContentFromProperties()
        {
            // Clear any existing content to start fresh
            ContentPanel.Children.Clear();

            // Handle header area (icon + title)
            // Show header if either an icon OR title is specified
            if (Icon != DialogIconType.None || !string.IsNullOrEmpty(DialogTitle))
            {
                if (Icon != DialogIconType.None)
                {
                    var iconElement = DialogIconFactory.CreateIcon(Icon);
                    if (iconElement != null)
                    {
                        IconPresenter.Content = iconElement;
                    }
                }
                
                // Set the title text (supports formatting)
                if (!string.IsNullOrEmpty(DialogTitle))
                {
                    HeaderTitle.Inlines.Clear();
                    var formattedTitleInlines = ParseFormattedText(DialogTitle);
                    foreach (var inline in formattedTitleInlines)
                    {
                        HeaderTitle.Inlines.Add(inline);
                    }
                }
                
                HeaderBorder.Visibility = Visibility.Visible;
            }
            else
            {
                // Hide header completely when no icon or title is specified
                HeaderBorder.Visibility = Visibility.Collapsed;
            }

            // Define the standard property sections in display order
            // Only properties with content will be displayed
            var properties = new[]
            {
                ("What", What),
                ("Problem", Problem), 
                ("Reason", Reason),
                ("Solution", Solution),
                ("Result", Result),
                ("Hint", Hint)
            };

            // Process each property and create UI sections for non-empty values
            bool hasContent = false; 
            foreach (var (propertyName, propertyValue) in properties)
            {
                if (!string.IsNullOrEmpty(propertyValue))
                {
                    AddPropertySection(propertyName, propertyValue);
                    hasContent = true; // At least one property has content
                }
            }

            // Handle Details section separately with visual separator
            // Details appears at the end with a horizontal line above it
            if (!string.IsNullOrEmpty(Details))
            {
                AddDetailsSeparator();
                AddDetailsSection();
                hasContent = true; // At least one property has content
            }

            // If no properties have content, hide the content area completely
            if (!hasContent)
            {
                ContentAreaBorder.Visibility = Visibility.Collapsed; // Hide content area if no properties have content
            }
        }

        // Creates a property section with heading and formatted content
        // Uses a two-column Grid layout: fixed-width heading on left, flexible content on right
        private void AddPropertySection(string propertyName, string propertyValue)
        {
            // Create the main container grid for this property section
            var grid = new Grid
            {
                Margin = new(0, 0, 0, 15) // Bottom margin for spacing between sections
            };

            if (NoHeadings)
            {
                // Single column layout - content takes full width with no heading
                grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });

                // Create the content TextBlock with text wrapping enabled
                var contentTextBlock = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                // Parse the property value for formatting directives and add to TextBlock
                var formattedInlines = ParseFormattedText(propertyValue);
                foreach (var inline in formattedInlines)
                {
                    contentTextBlock.Inlines.Add(inline);
                }

                Grid.SetColumn(contentTextBlock, 0);
                grid.Children.Add(contentTextBlock);
            }
            else
            {
                // Two column layout: 75px for heading, remaining space for content
                grid.ColumnDefinitions.Add(new() { Width = new(75) });
                grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });

                // Create the property heading label (bold text with colon)
                var headingLabel = new Label
                {
                    Content = $"{propertyName}:",
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Padding = new(0, -6, 10, 0) // Further increased negative top padding to move label text up for baseline alignment
                };
                Grid.SetColumn(headingLabel, 0);
                grid.Children.Add(headingLabel);

                // Create the content TextBlock with text wrapping enabled
                var contentTextBlock = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                // Parse the property value for formatting directives and add to TextBlock
                var formattedInlines = ParseFormattedText(propertyValue);
                foreach (var inline in formattedInlines)
                {
                    contentTextBlock.Inlines.Add(inline);
                }

                Grid.SetColumn(contentTextBlock, 1);
                grid.Children.Add(contentTextBlock);
            }

            // Add the completed property section to the main content panel
            ContentPanel.Children.Add(grid);
        }

        // Adds a horizontal separator line above the Details section
        // Provides visual separation between regular properties and details
        private void AddDetailsSeparator()
        {
            var separator = new Separator
            {
                Margin = new(0, 10, 0, 10) // Vertical spacing around separator
            };
            ContentPanel.Children.Add(separator);
        }

        // Creates the Details section as a collapsible toggle with scrollable content area
        // Details appears below a separator line with clickable header to expand/collapse
        private void AddDetailsSection()
        {
            // Main container for the entire Details section
            var mainContainer = new StackPanel
            {
                Margin = new(0, 0, 0, 15)
            };

            // Create clickable header row with toggle functionality
            var headerGrid = new Grid
            {
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = Brushes.Transparent // Make entire area clickable
            };
            headerGrid.ColumnDefinitions.Add(new() { Width = new(75) });
            headerGrid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });

            // Create "Details:" heading (same as other headings but clickable)
            var headingLabel = new Label
            {
                Content = "Details:",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new(0, -6, 10, 0) // Further increased negative top padding to move label text up for baseline alignment
            };
            Grid.SetColumn(headingLabel, 0);
            headerGrid.Children.Add(headingLabel);

            // Create container for toggle indicator and text
            var toggleContainer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // Create toggle indicator (arrow) that shows expand/collapse state
            var toggleArrow = new TextBlock
            {
                Text = "▶", // Right arrow (collapsed state)
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new(0, 2, 5, 0) // Align with heading text and add right spacing
            };
            toggleContainer.Children.Add(toggleArrow);
            
            // Create toggle text that shows current state
            var toggleText = new TextBlock
            {
                Text = "Click to show details", // Initial text (collapsed state)
                FontSize = 12,
                FontStyle = FontStyles.Italic,
                Foreground = Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new(0, 2, 0, 0) // Align with heading text
            };
            toggleContainer.Children.Add(toggleText);
            
            Grid.SetColumn(toggleContainer, 1);
            headerGrid.Children.Add(toggleContainer);

            // Create the collapsible content area (initially hidden)
            var contentContainer = new Grid
            {
                Visibility = Visibility.Collapsed
            };
            contentContainer.ColumnDefinitions.Add(new() { Width = new(75) });
            contentContainer.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
            contentContainer.Margin = new(0, 5, 0, 0);

            // Empty space in first column to align with header
            var spacer = new Label();
            Grid.SetColumn(spacer, 0);
            contentContainer.Children.Add(spacer);

            // Create ScrollViewer for the Details content
            var scrollViewer = new ScrollViewer
            {
                MaxHeight = 300,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new(1),
                Padding = new(5)
            };

            // Create content TextBlock inside the ScrollViewer
            var contentTextBlock = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // Parse Details property for formatting and add to TextBlock
            var formattedInlines = ParseFormattedText(Details);
            foreach (var inline in formattedInlines)
            {
                contentTextBlock.Inlines.Add(inline);
            }
            
            scrollViewer.Content = contentTextBlock;
            Grid.SetColumn(scrollViewer, 1);
            contentContainer.Children.Add(scrollViewer);

            // Add click event handler to toggle content visibility
            headerGrid.MouseLeftButtonUp += (_, _) =>
            {
                if (contentContainer.Visibility == Visibility.Collapsed)
                {
                    // Expand: Show content and change arrow/text to expanded state
                    contentContainer.Visibility = Visibility.Visible;
                    toggleArrow.Text = "▼"; // Down arrow (expanded state)
                    toggleText.Text = "Click to hide details"; // Updated text for expanded state
                    
                    // Scroll the Details section to the top of the visible area
                    ScrollDetailsToTop(mainContainer);
                }
                else
                {
                    // Collapse: Hide content and change arrow/text to collapsed state
                    contentContainer.Visibility = Visibility.Collapsed;
                    toggleArrow.Text = "▶"; // Right arrow (collapsed state)
                    toggleText.Text = "Click to show details"; // Updated text for collapsed state
                }
            };

            // Add both header and content to main container
            mainContainer.Children.Add(headerGrid);
            mainContainer.Children.Add(contentContainer);

            // Add the complete Details section to the main content panel
            ContentPanel.Children.Add(mainContainer);
        }

        #endregion

        #region Scroll Functionality

        // Scrolls the Details section to the top of the visible area when expanded
        // Finds the parent ScrollViewer and scrolls to bring the Details section into view
        private static void ScrollDetailsToTop(FrameworkElement detailsContainer)
        {
            // Find the parent ScrollViewer that contains the content
            var scrollViewer = FindParentScrollViewer(detailsContainer);
            if (scrollViewer != null)
            {
                // Get the position of the Details container relative to the ScrollViewer content
                var transform = detailsContainer.TransformToAncestor(scrollViewer);
                var position = transform.Transform(new(0, 0));
                
                // Add a small offset (10px) to ensure the top of the Details section is fully visible
                // This prevents the first line from being cut off at the top edge
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + position.Y - 10);
            }
        }

        // Helper method to find the parent ScrollViewer in the visual tree
        // Walks up the visual tree until it finds a ScrollViewer control
        // Remove the nullable annotation from the return type to fix CS8632
        private static ScrollViewer FindParentScrollViewer(DependencyObject child)
        {
            var parent = VisualTreeHelper.GetParent(child);
            
            while (parent != null)
            {
                if (parent is ScrollViewer scrollViewer)
                {
                    return scrollViewer;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
            
            return null;
        }

        #endregion

        #region Text Formatting and Parsing

        // Resolves {x:Static} references in text before formatting
        // Supports syntax like {x:Static constant:ExternalLinks.TimelapseGuideReference}
        // Uses the optional StaticReferenceResolver callback if provided
        private string ResolveStaticReferences(string text)
        {
            // If no resolver is configured, return text unchanged
            if (StaticReferenceResolver == null)
            {
                return text;
            }

            var staticRefPattern = @"\{x:Static\s+(\w+):(\w+)\.(\w+)\}";
            return Regex.Replace(text, staticRefPattern, match =>
            {
                var namespacePrefix = match.Groups[1].Value; // e.g., "constant"
                var className = match.Groups[2].Value;       // e.g., "ExternalLinks"
                var memberName = match.Groups[3].Value;      // e.g., "TimelapseGuideReference"

                try
                {
                    // Call the configured resolver callback
                    var resolvedValue = StaticReferenceResolver(namespacePrefix, className, memberName);
                    return resolvedValue ?? match.Value; // Return resolved value or original if null
                }
                catch (Exception ex)
                {
                    // Log the error for debugging but don't break the UI
                    System.Diagnostics.Debug.WriteLine($"Failed to resolve static reference {match.Value}: {ex.Message}");
                    return match.Value; // Return original if resolver throws
                }
            });
        }

        // Core text parsing method that converts formatted strings into WPF Inline elements
        // Supports formatting directives: **bold**, *italic*, __underline__, #Color[text], [link:url|display text]
        // Supports lists: lines starting with * for bullets, # for numbered items
        // Handles overlapping patterns and prevents text duplication issues
        private List<Inline> ParseFormattedText(string text)
        {
            // Resolve static references first
            text = ResolveStaticReferences(text);
            
            // Check if text contains list items (lines starting with * or #)
            if (ContainsListItems(text))
            {
                return ParseTextWithListsAsBlocks(text);
            }
            
            // Use existing inline parsing for non-list text
            return ParseInlineFormatting(text);
        }

        // Parse text with lists using new [li] and [ni] syntax with BlockUIContainer for proper text wrapping
        private List<Inline> ParseTextWithListsAsBlocks(string text)
        {
            var inlines = new List<Inline>();
            var globalNumbering = new Dictionary<int, int>();
            
            // Process the text by finding and replacing list directives and [br] directives
            var segments = new List<(string type, string content, int level)>();
            
            // Find all [br], [li], and [ni] directives in the text
            var directivePattern = @"\[(?:(br)(?:\s+(\d+))?|(li)(?:\s+([1-3]))?|(ni)(?:\s+([1-3]))?)\]";
            var matches = Regex.Matches(text, directivePattern).ToList();
            
            // Create a set of indices for [br] directives that should be ignored
            var ignoredBrIndices = new HashSet<int>();

            for (int i = 0; i < matches.Count - 1; i++) // -1 since we check i+1
            {
                var currentMatch = matches[i];

                // Check if this is a [br] directive without arguments
                if (currentMatch.Groups[1].Success && !currentMatch.Groups[2].Success)
                {
                    var nextMatch = matches[i + 1];
                    var textBetween = text.Substring(
                        currentMatch.Index + currentMatch.Length,
                        nextMatch.Index - (currentMatch.Index + currentMatch.Length));

                    // If there's only whitespace between [br] and next [li]/[ni] directive
                    if (string.IsNullOrWhiteSpace(textBetween) &&
                        (nextMatch.Groups[3].Success || nextMatch.Groups[5].Success))
                    {
                        ignoredBrIndices.Add(i);
                    }
                }
            }
            int lastIndex = 0;
            
            foreach (var (match, index) in matches.Select((m, i) => (m, i)))
            {
                // Add any text before this directive
                if (match.Index > lastIndex)
                {
                    var beforeText = text.Substring(lastIndex, match.Index - lastIndex).Trim();
                    if (!string.IsNullOrEmpty(beforeText))
                    {
                        segments.Add(("text", beforeText, 0));
                    }
                }
                
                // Process the directive (skip ignored [br] directives)
                if (match.Groups[1].Success) // [br]
                {
                    // Skip this [br] directive if it's in the ignored list
                    if (!ignoredBrIndices.Contains(index))
                    {
                        var spacing = match.Groups[2].Success ? match.Groups[2].Value : "";
                        segments.Add(("br", spacing, 0));
                    }
                }
                else if (match.Groups[3].Success) // [li]
                {
                    var level = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 1;
                    // Get the content after this directive (until the next directive or end of text)
                    var contentStart = match.Index + match.Length;
                    var nextMatch = matches.FirstOrDefault(m => m.Index > match.Index);
                    var contentEnd = nextMatch?.Index ?? text.Length;
                    var content = text.Substring(contentStart, contentEnd - contentStart).Trim();
                    segments.Add(("li", content, level));
                    
                    // Skip processing this content again
                    lastIndex = contentEnd;
                    continue;
                }
                else if (match.Groups[5].Success) // [ni]
                {
                    var level = match.Groups[6].Success ? int.Parse(match.Groups[6].Value) : 1;
                    // Get the content after this directive (until the next directive or end of text)
                    var contentStart = match.Index + match.Length;
                    var nextMatch = matches.FirstOrDefault(m => m.Index > match.Index);
                    var contentEnd = nextMatch?.Index ?? text.Length;
                    var content = text.Substring(contentStart, contentEnd - contentStart).Trim();
                    segments.Add(("ni", content, level));
                    
                    // Skip processing this content again
                    lastIndex = contentEnd;
                    continue;
                }
                
                lastIndex = match.Index + match.Length;
            }
            
            // Add any remaining text
            if (lastIndex < text.Length)
            {
                var remainingText = text[lastIndex..].Trim();
                if (!string.IsNullOrEmpty(remainingText))
                {
                    segments.Add(("text", remainingText, 0));
                }
            }
            
            // If no directives were found, treat as regular text
            if (segments.Count == 0)
            {
                segments.Add(("text", text, 0));
            }
            
            // Build the inlines from segments
            bool isFirstSegment = true;
            
            foreach (var (type, content, level) in segments)
            {
                if (string.IsNullOrWhiteSpace(content) && type != "br") continue;
                
                // Add line breaks between segments (except for the first one and [br] which handles its own spacing)
                if (!isFirstSegment && inlines.Count > 0 && type != "br")
                {
                    inlines.Add(new LineBreak());
                }
                
                switch (type)
                {
                    case "br":
                        inlines.Add(CreateLineBreakWithSpacing(content));
                        break;
                    case "li":
                        AddSingleBulletItem(inlines, content, level);
                        break;
                    case "ni":
                        AddSingleNumberedItem(inlines, content, level, globalNumbering);
                        break;
                    case "text":
                        var textInlines = ParseInlineFormatting(content);
                        inlines.AddRange(textInlines);
                        break;
                }
                
                isFirstSegment = false;
            }
            
            return inlines;
        }

        // Checks if text contains list items (using [li] or [ni] syntax)
        private static bool ContainsListItems(string text)
        {
            // Normalize both [br] and [br #] patterns to line breaks for list detection
            var normalizedText = Regex.Replace(text, @"\[br(?:\s+\d+)?\]", "\n");
            // Check for new list syntax: [li], [li 2], [li 3], [ni], [ni 2], [ni 3]
            return Regex.IsMatch(normalizedText, @"\[li(?:\s+[1-3])?\]|\[ni(?:\s+[1-3])?\]");
        }

        // Converts number to letter for deeper list levels (1->a, 2->b, etc.)
        private static string ToLetter(int number)
        {
            return ((char)('a' + (number - 1) % 26)).ToString();
        }

        // Converts number to Roman numeral for deepest list levels (1->i, 2->ii, etc.)
        private static string ToRomanNumeral(int number)
        {
            // Simple Roman numeral conversion for small numbers (1-20 should be sufficient for lists)
            return number switch
            {
                1 => "i",
                2 => "ii", 
                3 => "iii",
                4 => "iv",
                5 => "v",
                6 => "vi",
                7 => "vii",
                8 => "viii",
                9 => "ix",
                10 => "x",
                11 => "xi",
                12 => "xii",
                13 => "xiii",
                14 => "xiv",
                15 => "xv",
                16 => "xvi",
                17 => "xvii",
                18 => "xviii",
                19 => "xix",
                20 => "xx",
                _ => number.ToString() // Fallback to regular number for values > 20
            };
        }

        // Adds a single bullet item with proper indentation based on level
        private static void AddSingleBulletItem(List<Inline> inlines, string content, int level, double currentFontSize = 12.0, FontFamily currentFontFamily = null)
        {
            // Create a grid for proper bullet and text alignment
            var grid = new Grid();

            // Calculate indentation based on level
            // Level 2: Align bullets with level 1 text (indent by approximate width of level 1 bullet column)
            // Level 3+: Additional 20px per level beyond level 2
            double totalIndent = level switch
            {
                1 => 0,  // No indentation for level 1
                2 => 12, // Align with level 1 text (reduced width of "• " column)
                3 => 16, // Align with level 2 text (reduced from 20px)
                _ => 16 + (level - 3) * 20 // Base level 3 position + 20px per additional level
            };
            grid.Margin = new(totalIndent, 0, 0, 0);

            // Define columns: fixed width for level 2 bullets for tighter spacing, auto for others
            var bulletColumnWidth = level == 2 ? new(8) : GridLength.Auto; // Very tight spacing for level 2 bullets
            grid.ColumnDefinitions.Add(new() { Width = bulletColumnWidth });
            grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });

            // Add bullet character based on level
            var bulletChar = level switch
            {
                1 => "• ",
                2 => "◦ ",
                3 => "▪ ",
                _ => "• "
            };

            var bulletLabel = new Label
            {
                Content = bulletChar,
                Padding = new(0),
                Margin = level == 2 ? new(0, 0, -4, 0) : new Thickness(0), // Increased negative right margin for level 2 very tight spacing
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetColumn(bulletLabel, 0);
            grid.Children.Add(bulletLabel);

            // Add content with text wrapping
            var contentTextBlock = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new(0, 0, 0, 0)
            };

            // Parse and add formatted content with font context
            var itemInlines = ParseInlineFormatting(content, currentFontSize, currentFontFamily);
            foreach (var inline in itemInlines)
            {
                contentTextBlock.Inlines.Add(inline);
            }

            Grid.SetColumn(contentTextBlock, 1);
            grid.Children.Add(contentTextBlock);

            // Add the grid as an inline element
            inlines.Add(new InlineUIContainer(grid));
        }

        // Adds a single numbered item with proper indentation and global numbering
        private void AddSingleNumberedItem(List<Inline> inlines, string content, int level, Dictionary<int, int> globalNumbering, double currentFontSize = 12.0, FontFamily currentFontFamily = null)
        {
            // Update global numbering for this level
            if (!globalNumbering.TryAdd(level, 1))
            {
                globalNumbering[level]++;
            }

            // Reset numbering for deeper levels when returning to a shallower level
            var keysToRemove = globalNumbering.Keys.Where(k => k > level).ToList();
            foreach (var key in keysToRemove)
            {
                globalNumbering.Remove(key);
            }

            // Create a grid for proper number and text alignment
            var grid = new Grid();

            // Calculate indentation based on level
            // Level 2: Align numbers with level 1 text (indent by approximate width of level 1 number column)
            // Level 3+: Additional 20px per level beyond level 2
            double totalIndent = level switch
            {
                1 => 0,  // No indentation for level 1
                2 => 14, // Align with level 1 text (reduced width of "1. " column)
                3 => 20, // Align with level 2 text (reduced from 28px)
                _ => 20 + (level - 3) * 20 // Base level 3 position + 20px per additional level
            };
            grid.Margin = new(totalIndent, 0, 0, 0);

            // Calculate number text with space included based on level
            var numberText = level switch
            {
                1 => $"{globalNumbering[level]}. ",
                2 => $"{ToLetter(globalNumbering[level])}) ",
                3 => $"{ToRomanNumeral(globalNumbering[level])})",  // Removed space after parenthesis for tighter spacing
                _ => $"{globalNumbering[level]}. "
            };

            // Define columns: fixed/reduced width for levels 2 and 3 to ensure alignment and tighter spacing
            var numberColumnWidth = level switch
            {
                2 => new(14), // Very tight spacing for letters with parentheses: a) b) c)
                3 => new(20), // Very tight spacing for Roman numerals: i) ii) iii)
                _ => GridLength.Auto     // Auto sizing for level 1: 1. 2. 3.
            };
            grid.ColumnDefinitions.Add(new() { Width = numberColumnWidth });
            grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });

            // Add number
            var numberLabel = new Label
            {
                Content = numberText,
                Padding = new(0),
                Margin = level switch
                {
                    2 => new(0, 0, -3, 0), // Increased negative right margin for level 2 very tight spacing
                    3 => new(0, 0, -2, 0), // Negative right margin for level 3 very tight spacing
                    _ => new(0)            // Default margin for level 1
                },
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetColumn(numberLabel, 0);
            grid.Children.Add(numberLabel);

            // Add content with text wrapping
            var contentTextBlock = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new(0, 0, 0, 0)
            };

            // Parse and add formatted content with font context
            var itemInlines = ParseInlineFormatting(content, currentFontSize, currentFontFamily);
            foreach (var inline in itemInlines)
            {
                contentTextBlock.Inlines.Add(inline);
            }

            Grid.SetColumn(contentTextBlock, 1);
            grid.Children.Add(contentTextBlock);

            // Add the grid as an inline element
            inlines.Add(new InlineUIContainer(grid));
        }

        // Original inline formatting logic extracted into separate method
        // currentFontSize parameter allows nested [f] directives to adjust from the correct baseline
        // currentFontFamily parameter preserves font family through [f] directives to prevent font changes
        private static List<Inline> ParseInlineFormatting(string text, double currentFontSize = 12.0, FontFamily currentFontFamily = null)
        {
            var inlines = new List<Inline>();
            
            // Define regex patterns for each formatting type
            // Uses non-greedy matching (.*?) to handle multiple formatted sections properly
            var patterns = new[]
            {
                (@"\[b\](.*?)\[/b\]", "bold"),       // [b]text[/b] -> Bold
                (@"\[i\](.*?)\[/i\]", "italic"),    // [i]text[/i] -> Italic  
                (@"\[u\](.*?)\[/u\]", "underline"), // [u]text[/u] -> Underlined
                (@"\[e\](.*?)\[/e\]", "emphasis"),  // [e]text[/e] -> Bold Italic
                (@"\[f\s*([-+]?\d+)\](.*?)\[/f\]", "fontsize"), // [f #]text[/f] -> Font size adjustment (space is optional)
                (@"#(\w+)\[((?:[^\[\]]|\[[^\[\]]*\])*)\]", "color"), // #Color[text] -> Colored text (handles nested brackets)
                (@"\[link:(.*?)\|(.*?)\]", "link"), // [link:url|display text] -> Clickable hyperlink
                (@"\[br(?:\s+(\d+))?\]", "linebreak") // [br] or [br 8] -> Line break with optional custom spacing
            };

            // First pass: Find all formatting segments in the text
            var segments = new List<(int start, int end, string type, string content, string extra)>();
            
            foreach (var (pattern, type) in patterns)
            {
                var matches = Regex.Matches(text, pattern);
                foreach (Match match in matches)
                {
                    if (type == "color")
                    {
                        // Color pattern has two groups: color name and content
                        // Groups[1] = color name, Groups[2] = text content
                        segments.Add((match.Index, match.Index + match.Length, type, match.Groups[2].Value, match.Groups[1].Value));
                    }
                    else if (type == "link")
                    {
                        // Link pattern has two groups: URL and display text
                        // Groups[1] = URL, Groups[2] = display text
                        segments.Add((match.Index, match.Index + match.Length, type, match.Groups[2].Value, match.Groups[1].Value));
                    }
                    else if (type == "linebreak")
                    {
                        // Line break pattern may have optional spacing parameter
                        // Groups[1] = optional font size parameter (empty if not specified)
                        var spacingParam = match.Groups[1].Success ? match.Groups[1].Value : "";
                        segments.Add((match.Index, match.Index + match.Length, type, "", spacingParam));
                    }
                    else if (type == "fontsize")
                    {
                        // Font size pattern has two groups: size adjustment and content
                        // Groups[1] = size adjustment value, Groups[2] = text content
                        segments.Add((match.Index, match.Index + match.Length, type, match.Groups[2].Value, match.Groups[1].Value));
                    }
                    else
                    {
                        // Other patterns have one content group
                        // Groups[1] = text content to format
                        segments.Add((match.Index, match.Index + match.Length, type, match.Groups[1].Value, ""));
                    }
                }
            }

            // Second pass: Remove overlapping segments to prevent text duplication
            // This fixes the bug where formatted text appeared twice (formatted + plain)
            segments.Sort((a, b) => a.start.CompareTo(b.start));
            var filteredSegments = new List<(int start, int end, string type, string content, string extra)>();
            
            foreach (var segment in segments)
            {
                bool overlaps = false;
                // Check if this segment overlaps with any already accepted segment
                foreach (var existing in filteredSegments)
                {
                    // Three overlap conditions:
                    // 1. Segment starts inside existing segment
                    // 2. Segment ends inside existing segment  
                    // 3. Segment completely contains existing segment
                    if ((segment.start >= existing.start && segment.start < existing.end) ||
                        (segment.end > existing.start && segment.end <= existing.end) ||
                        (segment.start <= existing.start && segment.end >= existing.end))
                    {
                        overlaps = true;
                        break;
                    }
                }
                // Only add segments that don't overlap (first-match priority)
                if (!overlaps)
                {
                    filteredSegments.Add(segment);
                }
            }

            // Third pass: Build the final inline collection
            // Process segments in order, adding plain text between formatted segments
            int currentPos = 0;
            foreach (var segment in filteredSegments)
            {
                // Add any plain text that appears before this formatted segment
                if (segment.start > currentPos)
                {
                    var plainText = text[currentPos..segment.start];
                    if (!string.IsNullOrEmpty(plainText))
                    {
                        inlines.Add(new Run(plainText));
                    }
                }

                // Create and add the formatted inline element
                var formattedInline = CreateFormattedInline(segment.type, segment.content, segment.extra, currentFontSize, currentFontFamily);
                inlines.Add(formattedInline);

                // Move position to end of this segment
                currentPos = segment.end;
            }

            // Add any remaining plain text after the last formatted segment
            if (currentPos < text.Length)
            {
                var remainingText = text[currentPos..];
                if (!string.IsNullOrEmpty(remainingText))
                {
                    inlines.Add(new Run(remainingText));
                }
            }

            // Fallback: If no formatting was found, treat entire text as plain
            if (inlines.Count == 0)
            {
                inlines.Add(new Run(text));
            }

            return inlines;
        }

        // Factory method to create formatted inline elements based on type
        // Converts parsed formatting segments into actual WPF inline elements
        // Supports recursive parsing for nested formatting directives
        // currentFontSize and currentFontFamily are passed to maintain font context through nested directives
        private static Inline CreateFormattedInline(string type, string content, string extra, double currentFontSize = 12.0, FontFamily currentFontFamily = null)
        {
            return type switch
            {
                "bold" => CreateSemiBoldInline(content, currentFontSize, currentFontFamily),                      // [b]text[/b] -> Semi-bold with nested parsing
                "italic" => CreateNestedFormattedInline<Italic>(content, currentFontSize, currentFontFamily),       // [i]text[/i] -> Italic with nested parsing
                "underline" => CreateNestedFormattedInline<Underline>(content, currentFontSize, currentFontFamily), // [u]text[/u] -> Underlined with nested parsing
                "emphasis" => CreateEmphasisInline(content, currentFontSize, currentFontFamily),                   // [e]text[/e] -> Semi-bold Italic with nested parsing
                "fontsize" => CreateFontSizeInline(content, extra, currentFontSize, currentFontFamily),           // [f #]text[/f] -> Font size adjusted with nested parsing
                "color" => CreateColorInline(content, extra, currentFontSize, currentFontFamily),                 // #Color[text] -> Colored with nested parsing
                "link" => CreateHyperlink(content, extra),                    // [link:url|display] -> Clickable Hyperlink (no nesting)
                "linebreak" => CreateLineBreakWithSpacing(extra),             // [br] or [br 8] -> Line break (no nesting)
                _ => new Run(content)                                          // Fallback to plain text
            };
        }

        // Generic method to create formatted inline elements that support nested content
        private static T CreateNestedFormattedInline<T>(string content, double currentFontSize = 12.0, FontFamily currentFontFamily = null) where T : Span, new()
        {
            var element = new T();
            var nestedInlines = ParseInlineFormatting(content, currentFontSize, currentFontFamily); // Recursive parsing with font context
            foreach (var inline in nestedInlines)
            {
                element.Inlines.Add(inline);
            }
            return element;
        }

        // Creates semi-bold content with nested parsing support
        private static Span CreateSemiBoldInline(string content, double currentFontSize = 12.0, FontFamily currentFontFamily = null)
        {
            var span = new Span
            {
                FontWeight = FontWeights.SemiBold
            };
            var nestedInlines = ParseInlineFormatting(content, currentFontSize, currentFontFamily); // Recursive parsing with font context
            foreach (var inline in nestedInlines)
            {
                span.Inlines.Add(inline);
            }
            return span;
        }

        // Creates semi-bold italic emphasis with nested parsing support
        private static Span CreateEmphasisInline(string content, double currentFontSize = 12.0, FontFamily currentFontFamily = null)
        {
            var span = new Span
            {
                FontWeight = FontWeights.SemiBold
            };
            var italic = new Italic();
            var nestedInlines = ParseInlineFormatting(content, currentFontSize, currentFontFamily); // Recursive parsing with font context
            foreach (var inline in nestedInlines)
            {
                italic.Inlines.Add(inline);
            }
            span.Inlines.Add(italic);
            return span;
        }

        // Creates font size adjusted content with nested parsing support
        private static Span CreateFontSizeInline(string content, string sizeAdjustment, double currentFontSize = 12.0, FontFamily currentFontFamily = null)
        {
            // Create a Span to hold the font-sized content with nested formatting
            var span = new Span();
            double newSize = currentFontSize; // Default to current size if parsing fails

            try
            {
                // Parse the size adjustment value
                if (int.TryParse(sizeAdjustment, out var adjustment))
                {
                    // Apply safety limits: -10 to +20 points
                    adjustment = Math.Max(-10, Math.Min(adjustment, 20));

                    // Use the CURRENT font size as baseline (not hardcoded 12.0)
                    // This allows nested [f] directives to work correctly
                    newSize = currentFontSize + adjustment;

                    // Ensure the result is still reasonable (minimum 6pt, maximum 72pt)
                    newSize = Math.Max(6, Math.Min(newSize, 72));

                    span.FontSize = newSize;
                }
            }
            catch
            {
                // If parsing fails, use current font size
            }

            // Preserve font family to prevent font changes when size is adjusted
            if (currentFontFamily != null)
            {
                span.FontFamily = currentFontFamily;
            }

            // Parse nested content with the NEW font size and SAME font family as context
            // This ensures nested [f] directives adjust from the correct baseline and preserve font
            var nestedInlines = ParseInlineFormatting(content, newSize, currentFontFamily);
            foreach (var inline in nestedInlines)
            {
                span.Inlines.Add(inline);
            }

            return span;
        }

        // Creates colored content with nested parsing support
        private static Inline CreateColorInline(string content, string colorName, double currentFontSize = 12.0, FontFamily currentFontFamily = null)
        {
            var span = new Span();

            try
            {
                // Attempt to parse color name (supports named colors and hex values)
                var color = (Color)ColorConverter.ConvertFromString(colorName)!;
                span.Foreground = new SolidColorBrush(color);
            }
            catch
            {
                // If color parsing fails, use default text color
            }

            // Parse nested content and add to span
            var nestedInlines = ParseInlineFormatting(content, currentFontSize, currentFontFamily); // Recursive parsing with font context
            foreach (var inline in nestedInlines)
            {
                span.Inlines.Add(inline);
            }

            return span;
        }


        // Creates a clickable hyperlink element with URL navigation
        // Provides visual feedback (blue color, underline) and click handling
        private static Hyperlink CreateHyperlink(string displayText, string url)
        {
            var hyperlink = new Hyperlink(new Run(displayText))
            {
                // Set standard hyperlink styling
                Foreground = Brushes.Blue,
                TextDecorations = TextDecorations.Underline
            };

            // Add click event handler to open URL in default browser
            hyperlink.Click += (_, _) => OpenUrl(url);
            
            // Store URL for potential future use (tooltips, etc.)
            hyperlink.NavigateUri = new(url, UriKind.RelativeOrAbsolute);
            
            return hyperlink;
        }

        // Creates a line break with proper spacing using margin/padding
        private static Span CreateLineBreakWithSpacing(string spacingParam = "")
        {
            // Create a span with a line break followed by proper spacing
            var span = new Span();
            span.Inlines.Add(new LineBreak()); // First add the line break
            
            // Parse the spacing parameter, default to 6 points if not specified
            var spacing = 6.0; // Default spacing in points
            if (!string.IsNullOrEmpty(spacingParam) && double.TryParse(spacingParam, out var parsedSpacing))
            {
                spacing = Math.Max(0, Math.Min(parsedSpacing, 100)); // Clamp between 0 and 100 for safety
            }
            
            // Create an InlineUIContainer with a Border for spacing
            var spacerBorder = new Border
            {
                Height = spacing,
                Width = 0
            };
            
            var spacerContainer = new InlineUIContainer(spacerBorder);
            span.Inlines.Add(spacerContainer);
            
            return span;
        }

        // Opens a URL in the default web browser
        // Handles both relative and absolute URLs with error handling
        private static void OpenUrl(string url)
        {
            try
            {
                // Use Process.Start to open URL in default browser
                // This works with http, https, mailto, and other registered protocols
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true // Required for opening URLs in .NET Core/5+
                });
            }
            catch
            {
                // Silently handle errors (invalid URLs, no default browser, etc.)
                // In a production app, you might want to show an error message
            }
        }

        #endregion

        // This will toggle the visibility of the explanation panel
        private void HideTextButton_StateChange(object sender, RoutedEventArgs e)
        {
            SetExplanationVisibility();
        }

        private void SetExplanationVisibility()
        {
            if (HideText.IsChecked == true)
            {
                ContentAreaBorder.Visibility = Visibility.Collapsed;
                return;
            }
            ContentAreaBorder.Visibility = Visibility.Visible;
        }
    }
}
