using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;

namespace Timelapse.QuickPaste
{
    // A set of buttons including context menus that lets the user create and use quick paste controls
    public partial class QuickPasteWindow
    {
        #region Events
        public event EventHandler<QuickPasteEventArgs> QuickPasteEvent;

        private void SendQuickPasteEvent(QuickPasteEventArgs e)
        {
            QuickPasteEvent?.Invoke(this, e);
        }
        #endregion

        #region Public Properties
        public List<QuickPasteEntry> QuickPasteEntries { get; set; }

        // Position of the window, so we can save/restore it between sessions
        // (note that while I save the width and height, I only use the top left to position the window)
        public Rect Position { get; set; }
        #endregion

        #region Constructor, Loaded, Closing, Closed
        public QuickPasteWindow()
        {
            InitializeComponent();
        }

        // When the window is loaded
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Adjust this dialog window position, and add an event handler to signal when the position has changed 
            if (Position is { Left: 0, Top: 0 })
            {
                // 0,0 signals that there is no saved window position
                Dialogs.SetDefaultDialogPosition(this);
            }
            else
            {
                Top = Position.Top;
                Left = Position.Left;
            }
            SetPosition();
            LocationChanged += QuickPasteWindow_LocationChanged;

            // Build the window contents
            Refresh(QuickPasteEntries);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            SetPosition();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            // To overcome a wpf bug where Visibility stayed at Visibility.Visible.
            Visibility = Visibility.Collapsed;
        }
        #endregion

        #region Public Refresh
        public void Refresh(List<QuickPasteEntry> quickPasteEntries)
        {
            // This shouldn't happen
            if (quickPasteEntries == null) return;

            // Update the quickPasteEntries
            QuickPasteEntries = quickPasteEntries;

            // Clear the QuickPasteGrid, so we can start afresh
            QuickPasteGrid.RowDefinitions.Clear();
            QuickPasteGrid.Children.Clear();
            int gridRowIndex = 0;

            int shortcutKey = 1;
            foreach (QuickPasteEntry quickPasteEntry in QuickPasteEntries)
            {
                // Create the tooltip text for the QuickPaste control
                string tooltipText = string.Empty;
                foreach (QuickPasteItem item in quickPasteEntry.Items)
                {
                    if (item.Use)
                    {
                        if (!string.IsNullOrEmpty(tooltipText))
                        {
                            tooltipText += Environment.NewLine;
                        }
                        tooltipText += item.Label + ": " + item.Value;
                    }
                }

                // Compose the button content: a title and shortcut key
                TextBlock textblockTitle = new()
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                textblockTitle.Inlines.Add(quickPasteEntry.Title);

                TextBlock textblockShortcut = new()
                {
                    Padding = new(5, 0, 5, 0),
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                // We can't have more than 19 shortcut keys... one for each digit (except 0) with ctl and shift-ctl
                if (shortcutKey < 10)
                {
                    textblockShortcut.Inlines.Add("ctrl-" + shortcutKey++);
                }
                else if (shortcutKey < 19)
                {
                    textblockShortcut.Inlines.Add("shift-ctrl-" + (shortcutKey++ - 9));
                }
                DockPanel dockPanel = new();
                DockPanel.SetDock(textblockTitle, Dock.Left);
                DockPanel.SetDock(textblockShortcut, Dock.Right);
                dockPanel.Children.Add(textblockTitle);
                dockPanel.Children.Add(textblockShortcut);

                // Create and configure the QuickPaste control, and add its callbacks
                Button quickPasteControl = new()
                {
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Style = Owner.FindResource("QuickPasteButtonStyle") as Style,
                    Content = dockPanel,
                    ToolTip = tooltipText,
                    Tag = quickPasteEntry
                };
                // Mimic an inactive button if there are no items marked as Used
                textblockTitle.FontStyle = quickPasteEntry.IsAtLeastOneItemPastable() ? FontStyles.Normal : FontStyles.Italic;
                textblockShortcut.FontStyle = textblockTitle.FontStyle;

                // Create a Context Menu for each button that allows the user to
                // - Delete the item
                ContextMenu contextMenu = new();
                quickPasteControl.ContextMenu = contextMenu;

                MenuItem editItem = new()
                {
                    Header = "Edit",
                    Tag = quickPasteEntry
                };
                editItem.Click += EditItem_Click;
                contextMenu.Items.Add(editItem);

                MenuItem deleteItem = new()
                {
                    Header = "Delete",
                    Tag = quickPasteEntry
                };
                deleteItem.Click += DeleteItem_Click;
                contextMenu.Items.Add(deleteItem);

                // Move item up the quickpaste list
                MenuItem moveUpItem = new()
                {
                    Header = "Move up",
                    Tag = quickPasteEntry,
                    IsEnabled = QuickPasteEntries.First() != quickPasteEntry
                };
                moveUpItem.Click += MoveUpItem_Click;
                contextMenu.Items.Add(moveUpItem);

                // Move item down the quickpaste list
                MenuItem moveDownItem = new()
                {
                    Header = "Move down",
                    Tag = quickPasteEntry,
                    IsEnabled = QuickPasteEntries.Last() != quickPasteEntry
                };
                moveDownItem.Click += MoveDownItem_Click;
                contextMenu.Items.Add(moveDownItem);

                quickPasteControl.Click += QuickPasteControl_Click;
                quickPasteControl.MouseEnter += QuickPasteControl_MouseEnter;
                quickPasteControl.MouseLeave += QuickPasteControl_MouseLeave;

                // Create a grid row and add the QuickPaste control to it
                RowDefinition gridRow = new()
                {
                    Height = GridLength.Auto
                };
                QuickPasteGrid.RowDefinitions.Add(gridRow);
                Grid.SetRow(quickPasteControl, gridRowIndex);
                Grid.SetColumn(quickPasteControl, gridRowIndex);
                QuickPasteGrid.Children.Add(quickPasteControl);
                gridRowIndex++;
            }
        }

        // Check if the mouse is over any of the quickPasteControl buttons
        // If so, we should refresh the preview with that button's quickpaste entry
        public void RefreshQuickPasteWindowPreviewAsNeeded()
        {
            // If the quickPaste Window is visible
            if (IsEnabled == false && IsLoaded == false)
            {
                return;
            }
            foreach (Button quickPasteControl in QuickPasteGrid.Children)
            {
                if (quickPasteControl.IsMouseOver)
                {
                    SendQuickPasteEvent(new((QuickPasteEntry)quickPasteControl.Tag, QuickPasteEventIdentifierEnum.MouseEnter));
                    return;
                }
            }
        }
        #endregion

        #region Public Methods - TryQuickPasteShortcut
        public void TryQuickPasteShortcut(int shortcutIndex)
        {
            if (shortcutIndex <= QuickPasteEntries.Count)
            {
                QuickPasteEntry quickPasteEntry = QuickPasteEntries[shortcutIndex - 1];
                SendQuickPasteEvent(new(quickPasteEntry, QuickPasteEventIdentifierEnum.ShortcutPaste));
            }
        }
        #endregion

        #region Generate Events
        // Generate Event: New quickpaste entry
        private void NewQuickPasteEntryButton_Click(object sender, RoutedEventArgs e)
        {
            SendQuickPasteEvent(new(null, QuickPasteEventIdentifierEnum.New));
        }

        // Generate Event: Edit the quickpaste emtru
        private void EditItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem == false)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            QuickPasteEntry quickPasteEntry = (QuickPasteEntry)menuItem.Tag;
            SendQuickPasteEvent(new(quickPasteEntry, QuickPasteEventIdentifierEnum.Edit));
        }

        // Generate Event: Delete the quickpaste entry
        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem == false)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            QuickPasteEntry quickPasteEntry = (QuickPasteEntry)menuItem.Tag;
            SendQuickPasteEvent(new(quickPasteEntry, QuickPasteEventIdentifierEnum.Delete));
        }

        // Generate Event: Move the quickpaste entry up the list
        private void MoveUpItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem == false)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            QuickPasteEntry quickPasteEntry = (QuickPasteEntry)menuItem.Tag;
            SendQuickPasteEvent(new(quickPasteEntry, QuickPasteEventIdentifierEnum.MoveUp));
        }

        // Generate Event: Move the quickpaste entry down the list
        private void MoveDownItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem == false)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            QuickPasteEntry quickPasteEntry = (QuickPasteEntry)menuItem.Tag;
            SendQuickPasteEvent(new(quickPasteEntry, QuickPasteEventIdentifierEnum.MoveDown));
        }

        // Generate Event: MouseEnter on the quickpaste control
        private void QuickPasteControl_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Button button == false)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(sender));
                return;
            }
            QuickPasteEntry quickPasteEntry = (QuickPasteEntry)button.Tag;
            SendQuickPasteEvent(new(quickPasteEntry, QuickPasteEventIdentifierEnum.MouseEnter));
        }

        // Generate Event: MouseLeave on the quickpaste control
        private void QuickPasteControl_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
            {
                QuickPasteEntry quickPasteEntry = (QuickPasteEntry)button.Tag;
                SendQuickPasteEvent(new(quickPasteEntry,
                QuickPasteEventIdentifierEnum.MouseLeave));
            }
        }

        // Generate Event: Select the quickpaste entry (quickpaste control has been activated)
        private void QuickPasteControl_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button == false)
            {
               TracePrint.NullException(nameof(sender));
               return;
            }
            QuickPasteEntry quickPasteEntry = (QuickPasteEntry)button.Tag;
            SendQuickPasteEvent(new(quickPasteEntry, QuickPasteEventIdentifierEnum.Paste));
        }
        #endregion

        #region Callbacks
        private void QuickPasteWindow_LocationChanged(object sender, EventArgs e)
        {
            SetPosition();
        }

        // Use the arrow and page up/down keys to navigate images
        private void Window_PreviewKeyDown(object sender, KeyEventArgs keyEvent)
        {
            if (keyEvent.Key == Key.Right || keyEvent.Key == Key.Left || keyEvent.Key == Key.PageUp || keyEvent.Key == Key.PageDown)
            {
                keyEvent.Handled = true;
                GlobalReferences.MainWindow.Handle_PreviewKeyDown(keyEvent, true);
            }
        }
        #endregion

        #region Private Methods
        private void SetPosition()
        {
            Position = new(Left, Top, Width, Height);
            SendQuickPasteEvent(new(null, QuickPasteEventIdentifierEnum.PositionChanged));
        }
        #endregion
    }
}
