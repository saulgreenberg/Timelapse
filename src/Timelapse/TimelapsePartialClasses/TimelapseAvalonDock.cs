using System;
using System.ComponentModel;
using System.Linq;
using Timelapse.Constant;
using Timelapse.DebuggingSupport;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    // AvalonDock callbacks and methods
    public partial class TimelapseWindow
    {
        #region Private Fields
        // Timer-based resize constraint mechanism to fix bouncing/oscillating window size during drag
        // Problem: Applying constraints during resize caused feedback loop - ScrollViewer dimensions change
        // as window size changes, causing constraints to chase a moving target and create oscillation.
        // Solution: Use debounce timer - only apply constraints 300ms after user stops dragging AND releases mouse.
        private System.Windows.Threading.DispatcherTimer resizeCompleteTimer;

        // Flag to prevent re-triggering timer when we apply constraints (avoids feedback loop)
        private bool isApplyingConstraints;

        // Flag to track if user is actively resizing (used to disable LayoutUpdated handler during resize)
        private bool isActivelyResizing;

        // Cached chrome dimensions (calculated once when window is properly sized)
        private double? cachedChromeHeight;
        private double? cachedChromeWidth;
        #endregion

        #region Callback - Property changing
        // Handles FloatingHeight/FloatingWidth property changes during window resize
        // Uses debounce timer to delay constraint application until resize operation completes
        public void LayoutAnchorable_PropertyChanging(object sender, PropertyChangingEventArgs e)
        {
            // Check the arguments for null
            if (sender == null || e == null)
            {
                // this should not happen
                TracePrint.StackTrace(1);
                // throw new ArgumentNullException(nameof(sender));
                // Try treating this as a no-op instead of a throw
                return;
            }

            if (!(sender is LayoutAnchorable la)) return;
            if (la.ContentId == "ContentIDDataEntryControlPanel" && (e.PropertyName == AvalonDockValues.FloatingWindowFloatingHeightProperty || e.PropertyName == AvalonDockValues.FloatingWindowFloatingWidthProperty))
            {
                // If we're applying constraints, ignore this event to prevent feedback loop
                if (isApplyingConstraints)
                {
                    return;
                }

                // Mark that user is actively resizing (will disable LayoutUpdated handler)
                isActivelyResizing = true;

                // Don't apply constraints during resize - use timer to detect when resize stops
                if (resizeCompleteTimer == null)
                {
                    resizeCompleteTimer = new()
                    {
                        Interval = TimeSpan.FromMilliseconds(300) // Delay before applying constraints after resize stops
                    };
                    resizeCompleteTimer.Tick += (_, _) =>
                    {
                        // Only apply constraints if mouse button is released (not just paused during drag)
                        // This prevents constraints from applying when user pauses mid-drag with mouse still down
                        if (System.Windows.Input.Mouse.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                        {
                            // Mouse still down - keep timer running, check again on next tick
                            return;
                        }

                        resizeCompleteTimer.Stop();

                        // Set flag to prevent feedback loop when our constraint changes trigger PropertyChanging again
                        isApplyingConstraints = true;
                        try
                        {
                            DockingManager_FloatingDataEntryWindowLimitSize();
                        }
                        finally
                        {
                            isApplyingConstraints = false;
                            // Re-enable LayoutUpdated processing now that resize is complete
                            isActivelyResizing = false;
                        }
                    };
                }

                // Reset timer on each resize event - only fires when events stop
                resizeCompleteTimer.Stop();
                resizeCompleteTimer.Start();
            }
        }
        #endregion

        #region Callback - Layout Updated
        private void DockingManager_LayoutUpdated(object sender, EventArgs e)
        {
            // Skip entirely during active resize to reduce (but not eliminate) chrome flickering
            // Note: Title bar/border flicker during resize is a known AvalonDock/WPF WindowChrome limitation
            // See: https://github.com/Dirkster99/AvalonDock/issues/85
            //      https://github.com/xceedsoftware/wpftoolkit/issues/1542
            // The flicker is cosmetic, doesn't affect content or functionality, and has no reliable fix.
            if (isActivelyResizing)
            {
                return;
            }

            if (DockingManager1.FloatingWindows.Any())
            {
                DockingManager_FloatingDataEntryWindowTopmost(false, DockingManager1);
            }
            if (DockingManager2.FloatingWindows.Any())
            {
                DockingManager_FloatingDataEntryWindowTopmost(false, DockingManager2);
            }
        }
        #endregion

        #region Private Methods
        // Enable or disable floating windows normally always being on top.
        // Also shows floating windows in the task bar if it can be hidden
        private void DockingManager_FloatingDataEntryWindowTopmost(bool topMost, DockingManager dockingManager)
        {
            foreach (LayoutFloatingWindowControl floatingWindow in dockingManager.FloatingWindows)
            {
                // This checks to see if its the data entry window, which is the only layoutanchorable present.
                // If its not, then the value will be null (i.e., its the DataGrid layoutdocument)
                if (!(floatingWindow.Model is LayoutAnchorableFloatingWindow))
                {
                    // SAULXXX: Note that the Floating DocumentPane (i.e., the DataGrid) behaviour is not what we want
                    // That is, it always appears topmost. yet if we set it to null, then it disappears behind the main
                    // window when the mouse is moved over the main window (vs. clicking in it).
                    if (floatingWindow.Owner != null)
                    {
                        floatingWindow.Owner = null;
                    }
                    if (!floatingWindow.ShowInTaskbar)
                    {
                        floatingWindow.ShowInTaskbar = true;
                    }
                    continue;
                }

                // Only set min dimensions if they're different (avoid triggering layout updates)
                if (Math.Abs(floatingWindow.MinHeight - AvalonDockValues.FloatingWindowMinimumHeight) > .001)
                {
                    floatingWindow.MinHeight = AvalonDockValues.FloatingWindowMinimumHeight;
                }
                if (Math.Abs(floatingWindow.MinWidth - AvalonDockValues.FloatingWindowMinimumWidth) > .001)
                {
                    floatingWindow.MinWidth = AvalonDockValues.FloatingWindowMinimumWidth;
                }

                if (topMost)
                {
                    floatingWindow.Owner ??= this;
                }
                else if (floatingWindow.Owner != null)
                {
                    // Set this to null if we want windows NOT to appear always on top, otherwise to true
                    // floatingWindow.Owner = null;
                    if (!floatingWindow.ShowInTaskbar)
                    {
                        floatingWindow.ShowInTaskbar = true;
                    }
                }
            }
        }

        // Helper methods - used only by the above methods

        // Constrains floating window to fit the control grid content (auto-size to match data entry controls)
        // Called only after resize completes (via timer), not during active drag operations
        // This prevents the bouncing/oscillation that occurred when constraints were applied during resize
        private void DockingManager_FloatingDataEntryWindowLimitSize()
        {
            foreach (var floatingWindow in DockingManager1.FloatingWindows)
            {
                // This checks to see if its the data entry window, which is the only layoutanchorable present.
                // If its not, then the value will be null (i.e., its the DataGrid layoutdocument)
                if (!(floatingWindow.Model is LayoutAnchorableFloatingWindow))
                {
                    continue;
                }
                if (floatingWindow.HasContent)
                {
                    // Calculate chrome dimensions (window borders + title bar + padding)
                    // Evaluate height and width INDEPENDENTLY - each dimension caches only when well-sized
                    double scrollViewerContentFitHeight = Math.Abs(DataEntryScrollViewer.ActualHeight - ControlGrid.ActualHeight);
                    double scrollViewerContentFitWidth = Math.Abs(DataEntryScrollViewer.ActualWidth - ControlGrid.ActualWidth);

                    // Check each dimension independently for good fit
                    bool heightIsWellSized = scrollViewerContentFitHeight < 10;
                    bool widthIsWellSized = scrollViewerContentFitWidth < 10;

                    // Calculate and cache HEIGHT chrome (only if not cached OR height dimension is well-sized)
                    if (!cachedChromeHeight.HasValue || heightIsWellSized)
                    {
                        double chromeHeight = floatingWindow.ActualHeight - DataEntryScrollViewer.ActualHeight;
                        // Validate and cache if reasonable
                        if (chromeHeight is >= 0 and <= 200)
                        {
                            cachedChromeHeight = chromeHeight;
                        }
                    }

                    // Calculate and cache WIDTH chrome (only if not cached OR width dimension is well-sized)
                    if (!cachedChromeWidth.HasValue || widthIsWellSized)
                    {
                        double chromeWidth = floatingWindow.ActualWidth - DataEntryScrollViewer.ActualWidth;
                        // Validate and cache if reasonable
                        if (chromeWidth is >= 0 and <= 100)
                        {
                            cachedChromeWidth = chromeWidth;
                        }
                    }

                    // Use cached chrome or fallback to constants
                    double finalChromeHeight = cachedChromeHeight ?? AvalonDockValues.FloatingWindowLimitSizeHeightCorrection;
                    double finalChromeWidth = cachedChromeWidth ?? AvalonDockValues.FloatingWindowLimitSizeWidthCorrection;

                    // Use ControlGrid.ActualHeight/ActualWidth (the actual content size), not ScrollViewer size
                    // ScrollViewer dimensions change as window size changes, creating a moving target
                    double idealHeight = ControlGrid.ActualHeight + finalChromeHeight;
                    double idealWidth = ControlGrid.ActualWidth + finalChromeWidth;

                    // Auto-size window to match content (grows window if too small, shrinks if too large)
                    if (Math.Abs(floatingWindow.Height - idealHeight) > .001)
                    {
                        floatingWindow.Height = idealHeight;
                    }
                    if (Math.Abs(floatingWindow.Width - idealWidth) > .001)
                    {
                        floatingWindow.Width = idealWidth;
                    }
                    break;
                }
            }
        }
        #endregion
    }
}
