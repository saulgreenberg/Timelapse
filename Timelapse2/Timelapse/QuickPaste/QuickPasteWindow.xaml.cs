using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Dialog;

namespace Timelapse.QuickPaste
{
    // A set of buttons including context menus that lets the user create and use quick paste controls
    public partial class QuickPasteWindow : Window
    {
        #region Events
        public event EventHandler<QuickPasteEventArgs> QuickPasteEvent;

        private void SendQuickPasteEvent(QuickPasteEventArgs e)
        {
            this.QuickPasteEvent?.Invoke(this, e);
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
            this.InitializeComponent();
        }

        // When the window is loaded
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Adjust this dialog window position, and add an event handler to signal when the position has changed 
            if (this.Position.Left == 0 && this.Position.Top == 0)
            {
                // 0,0 signals that there is no saved window position
                Dialogs.SetDefaultDialogPosition(this);
            }
            else
            {
                this.Top = this.Position.Top;
                this.Left = this.Position.Left;
            }
            this.SetPosition();
            this.LocationChanged += this.QuickPasteWindow_LocationChanged;

            // Build the window contents
            this.Refresh(this.QuickPasteEntries);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.SetPosition();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            // To overcome a wpf bug where Visibility stayed at Visibility.Visible.
            this.Visibility = Visibility.Collapsed;
        }
        #endregion

        #region Public Refresh
        public void Refresh(List<QuickPasteEntry> quickPasteEntries)
        {
            // This shouldn't happen
            if (quickPasteEntries == null) return;

            // Update the quickPasteEntries
            this.QuickPasteEntries = quickPasteEntries;

            // Clear the QuickPasteGrid, so we can start afresh
            this.QuickPasteGrid.RowDefinitions.Clear();
            this.QuickPasteGrid.Children.Clear();
            int gridRowIndex = 0;

            int shortcutKey = 1;
            foreach (QuickPasteEntry quickPasteEntry in this.QuickPasteEntries)
            {
                // Create the tooltip text for the QuickPaste control
                string tooltipText = String.Empty;
                foreach (QuickPasteItem item in quickPasteEntry.Items)
                {
                    if (item.Use)
                    {
                        if (!string.IsNullOrEmpty(tooltipText))
                        {
                            tooltipText += Environment.NewLine;
                        }
                        tooltipText += item.Label + ": " + item.Value.ToString();
                    }
                }

                // Compose the button content: a title and shortcut key
                TextBlock textblockTitle = new TextBlock()
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                textblockTitle.Inlines.Add(quickPasteEntry.Title);

                TextBlock textblockShortcut = new TextBlock
                {
                    Padding = new Thickness(5, 0, 5, 0),
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                // We can't have more than 9 shortcut keys... one for each digit (except 0)
                if (shortcutKey < 10)
                {
                    textblockShortcut.Inlines.Add("ctrl-" + shortcutKey++);
                }
                DockPanel dockPanel = new DockPanel();
                DockPanel.SetDock(textblockTitle, Dock.Left);
                DockPanel.SetDock(textblockShortcut, Dock.Right);
                dockPanel.Children.Add(textblockTitle);
                dockPanel.Children.Add(textblockShortcut);

                // Create and configure the QuickPaste control, and add its callbacks
                Button quickPasteControl = new Button()
                {
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Style = this.Owner.FindResource("QuickPasteButtonStyle") as Style,
                    Content = dockPanel,
                    ToolTip = tooltipText,
                    Tag = quickPasteEntry
                };
                // Mimic an inactive button if there are no items marked as Used
                textblockTitle.FontStyle = quickPasteEntry.IsAtLeastOneItemPastable() ? FontStyles.Normal : FontStyles.Italic;
                textblockShortcut.FontStyle = textblockTitle.FontStyle;

                // Create a Context Menu for each button that allows the user to
                // - Delete the item
                ContextMenu contextMenu = new ContextMenu();
                quickPasteControl.ContextMenu = contextMenu;

                MenuItem editItem = new MenuItem()
                {
                    Header = "Edit",
                    Tag = quickPasteEntry
                };
                editItem.Click += this.EditItem_Click;
                contextMenu.Items.Add(editItem);

                MenuItem deleteItem = new MenuItem()
                {
                    Header = "Delete",
                    Tag = quickPasteEntry
                };
                deleteItem.Click += this.DeleteItem_Click;
                contextMenu.Items.Add(deleteItem);

                quickPasteControl.Click += this.QuickPasteControl_Click;
                quickPasteControl.MouseEnter += this.QuickPasteControl_MouseEnter;
                quickPasteControl.MouseLeave += this.QuickPasteControl_MouseLeave;

                // Create a grid row and add the QuickPaste control to it
                RowDefinition gridRow = new RowDefinition()
                {
                    Height = GridLength.Auto
                };
                this.QuickPasteGrid.RowDefinitions.Add(gridRow);
                Grid.SetRow(quickPasteControl, gridRowIndex);
                Grid.SetColumn(quickPasteControl, gridRowIndex);
                this.QuickPasteGrid.Children.Add(quickPasteControl);
                gridRowIndex++;
            }
        }

        // Check if the mouse is over any of the quickPasteControl buttons
        // If so, we should refresh the preview with that button's quickpaste entry
        public void RefreshQuickPasteWindowPreviewAsNeeded()
        {
            // If the quickPaste Window is visible
            if (this.IsEnabled == false && this.IsLoaded == false)
            {
                return;
            }
            foreach (Button quickPasteControl in this.QuickPasteGrid.Children)
            {
                if (quickPasteControl.IsMouseOver)
                {
                    this.SendQuickPasteEvent(new QuickPasteEventArgs((QuickPasteEntry)quickPasteControl.Tag, QuickPasteEventIdentifierEnum.MouseEnter));
                    return;
                }
            }
        }
        #endregion

        #region Public Methods - TryQuickPasteShortcut
        public void TryQuickPasteShortcut(int shortcutIndex)
        {
            if (shortcutIndex <= this.QuickPasteEntries.Count)
            {
                QuickPasteEntry quickPasteEntry = this.QuickPasteEntries[shortcutIndex - 1];
                this.SendQuickPasteEvent(new QuickPasteEventArgs(quickPasteEntry, QuickPasteEventIdentifierEnum.ShortcutPaste));
            }
        }
        #endregion

        #region Generate Events
        // Generate Event: New quickpaste entry
        private void NewQuickPasteEntryButton_Click(object sender, RoutedEventArgs e)
        {
            this.SendQuickPasteEvent(new QuickPasteEventArgs(null, QuickPasteEventIdentifierEnum.New));
        }

        // Generate Event: Edit the quickpaste emtru
        private void EditItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            QuickPasteEntry quickPasteEntry = (QuickPasteEntry)menuItem.Tag;
            this.SendQuickPasteEvent(new QuickPasteEventArgs(quickPasteEntry, QuickPasteEventIdentifierEnum.Edit));
        }

        // Generate Event: Delete the quickpaste emtru
        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            QuickPasteEntry quickPasteEntry = (QuickPasteEntry)menuItem.Tag;
            this.SendQuickPasteEvent(new QuickPasteEventArgs(quickPasteEntry, QuickPasteEventIdentifierEnum.Delete));
        }

        // Generate Event: MouseEnter on the quickpaste control
        private void QuickPasteControl_MouseEnter(object sender, MouseEventArgs e)
        {
            Button button = sender as Button;
            QuickPasteEntry quickPasteEntry = (QuickPasteEntry)button.Tag;
            this.SendQuickPasteEvent(new QuickPasteEventArgs(quickPasteEntry, QuickPasteEventIdentifierEnum.MouseEnter));
        }

        // Generate Event: MouseLeave on the quickpaste control
        private void QuickPasteControl_MouseLeave(object sender, MouseEventArgs e)
        {
            Button button = sender as Button;
            QuickPasteEntry quickPasteEntry = (QuickPasteEntry)button.Tag;
            this.SendQuickPasteEvent(new QuickPasteEventArgs(quickPasteEntry, QuickPasteEventIdentifierEnum.MouseLeave));
        }

        // Generate Event: Select the quickpaste entry (quickpaste control has been activated)
        private void QuickPasteControl_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            QuickPasteEntry quickPasteEntry = (QuickPasteEntry)button.Tag;
            this.SendQuickPasteEvent(new QuickPasteEventArgs(quickPasteEntry, QuickPasteEventIdentifierEnum.Paste));
        }
        #endregion

        #region Callbacks
        private void QuickPasteWindow_LocationChanged(object sender, EventArgs e)
        {
            this.SetPosition();
        }

        // Use the arrow and page up/down keys to navigate images
        private void Window_PreviewKeyDown(object sender, KeyEventArgs keyEvent)
        {
            if (keyEvent.Key == Key.Right || keyEvent.Key == Key.Left || keyEvent.Key == Key.PageUp || keyEvent.Key == Key.PageDown)
            {
                keyEvent.Handled = true;
                Util.GlobalReferences.MainWindow.Handle_PreviewKeyDown(keyEvent, true);
            }
        }
        #endregion

        #region Private Methods
        private void SetPosition()
        {
            this.Position = new Rect(this.Left, this.Top, this.Width, this.Height);
            this.SendQuickPasteEvent(new QuickPasteEventArgs(null, QuickPasteEventIdentifierEnum.PositionChanged));
        }
        #endregion
    }
}
