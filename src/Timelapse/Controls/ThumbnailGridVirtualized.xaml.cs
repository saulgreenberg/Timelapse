using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Timelapse.Constant;
using Timelapse.ControlsDataEntry;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.EventArguments;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.Controls
{
    public partial class ThumbnailGridVirtualized
    {
        #region Public Properties
        public DataEntryControls DataEntryControls { get; set; }
        public FileTable FileTable { get; set; }
        public int FileTableStartIndex { get; set; }
        public string RootPathToImages { get; set; } = string.Empty;
        public int AvailableColumns => columnCount;
        public int AvailableRows => cellHeight > 0 ? Math.Max(1, (int)Math.Ceiling(currentGridHeight / cellHeight)) : 0;
        public bool IsGridActive => cellHeight > 0;

        // File index of the top-left cell currently visible in the viewport.
        // Used as the navigation base so FilePlayer/keyboard nav starts from where the user is looking,
        // not from the last clicked thumbnail (which may be far off-screen after a scroll).
        public int FirstVisibleFileIndex =>
            columnCount > 0 && cellHeight > 0
                ? Math.Max(0, currentFirstRow * columnCount - startPadding)
                : FileTableStartIndex;

        // True when the bottom of the canvas is within the viewport — the last file row is visible.
        // WPF may clamp VerticalOffset when the canvas is shorter than viewport+targetRow, so we
        // check the scroll position directly rather than inferring from currentFirstRow/startPadding.
        public bool IsAtScrollEnd =>
            cellHeight > 0 && ScrollViewer.VerticalOffset + currentGridHeight >= VirtualCanvas.Height - 0.5;

        // True when we are scrolled all the way to the top of the canvas.
        public bool IsAtScrollStart => ScrollViewer.VerticalOffset <= 0.5;
        #endregion

        #region Private State
        private double cellHeight;
        private double cellWidth;
        private int columnCount;
        private double currentGridWidth;
        private double currentGridHeight;
        private int currentFirstRow;       // row index at the top of the viewport
        private int zoomAnchorFileIndex;   // file index that anchors zoom — persists across zoom steps, only updated on manual scroll
        private bool suppressAnchorUpdate; // true while a zoom-triggered ScrollChanged is still pending
        private int zoomLevel;             // discrete zoom counter; 0 = deactivated, 1 = initial view, N = more zoomed out
        private double initialCellHeight;  // cellHeight at zoomLevel 1; all levels derived from this × ZoomStep^(level-1)
        private readonly DispatcherTimer snapAnimTimer;
        private double snapAnimStart;
        private double snapAnimTarget;
        private int snapAnimStep;
        private int snapAnimTotalSteps;
        private const int SnapAnimStepsSnap = 8;   // thumb-drag row-snap: ~128 ms
        private Action snapAnimCompletionAction;
        private bool homeSlideActive;
        private readonly List<ThumbnailInCell> pool = [];
        private readonly HashSet<int> selectedIndices = [];
        private int anchorIndex = -1;
        private BackgroundWorker backgroundWorker;
        // Bitmap cache — keyed by FileTable index; cleared when cell dimensions change (zoom/reset)
        private readonly Dictionary<int, BitmapSource> bitmapCache = [];
        private readonly Queue<int> cacheInsertionOrder = new();
        private const int BitmapCacheMaxSize = 500;
        // Mouse / drag tracking
        private int dragStartFileIndex;
        private bool modifierPressedOnMouseDown;
        private bool isDraggingFromCanvas;   // true only when mouse-down landed on a valid cell
        private Point lastDragViewerPoint;   // cursor in ScrollViewer coords, kept current during drag
        private readonly DispatcherTimer autoScrollTimer;
        private double autoScrollDelta;      // ±cellHeight: one row per tick, sign = direction
        private int startPadding;            // empty cells before file[0] so the zoom-anchor file lands at col=0
        private DateTime trackScrollHoldStart = DateTime.MinValue; // for track-click ease-in
        private bool trackScrollFirstClick;                        // true until first Click fires
        #endregion

        #region Constructor
        public ThumbnailGridVirtualized()
        {
            InitializeComponent();
            FileTableStartIndex = 0;
            snapAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            snapAnimTimer.Tick += SnapAnimTimer_Tick;
            autoScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            autoScrollTimer.Tick += AutoScrollTimer_Tick;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Replace the scrollbar track's page-scroll buttons with single-row scrolling.
            // RepeatButton fires Click on every auto-repeat pulse, so held click auto-scrolls.
            ScrollViewer.ApplyTemplate();
            if (ScrollViewer.Template?.FindName("PART_VerticalScrollBar", ScrollViewer) is not ScrollBar vsb)
                return;
            vsb.ApplyTemplate();
            if (vsb.Template?.FindName("PART_Track", vsb) is not Track track)
                return;
            track.IncreaseRepeatButton.Command = null;
            track.DecreaseRepeatButton.Command = null;
            track.IncreaseRepeatButton.PreviewMouseLeftButtonDown += (_, _) => { trackScrollHoldStart = DateTime.Now; trackScrollFirstClick = true; };
            track.DecreaseRepeatButton.PreviewMouseLeftButtonDown += (_, _) => { trackScrollHoldStart = DateTime.Now; trackScrollFirstClick = true; };
            track.IncreaseRepeatButton.PreviewMouseLeftButtonUp += (_, _) => { trackScrollHoldStart = DateTime.MinValue; SnapToRowBoundary(); };
            track.DecreaseRepeatButton.PreviewMouseLeftButtonUp += (_, _) => { trackScrollHoldStart = DateTime.MinValue; SnapToRowBoundary(); };
            track.IncreaseRepeatButton.Click += (_, _) => DoTrackScroll(+1);
            track.DecreaseRepeatButton.Click += (_, _) => DoTrackScroll(-1);
            track.Thumb?.DragCompleted += ScrollThumb_DragCompleted;

            // Replace the arrow (line) buttons with single-row scrolling, matching track behaviour.
            // Walk the visual tree instead of relying on template part names (which vary by WPF theme).
            WireLineButton(vsb, ScrollBar.LineUpCommand,   -1);
            WireLineButton(vsb, ScrollBar.LineDownCommand, +1);
        }

        private void WireLineButton(DependencyObject root, ICommand targetCommand, double direction)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is RepeatButton rb && Equals(rb.Command, targetCommand))
                {
                    rb.Command = null;
                    rb.PreviewMouseLeftButtonDown += (_, _) => { trackScrollHoldStart = DateTime.Now; trackScrollFirstClick = true; };
                    rb.PreviewMouseLeftButtonUp += (_, _) => { trackScrollHoldStart = DateTime.MinValue; SnapToRowBoundary(); };
                    rb.Click += (_, _) => DoTrackScroll(direction);
                    return;
                }
                WireLineButton(child, targetCommand, direction);
            }
        }

        private void ScrollThumb_DragCompleted(object sender, DragCompletedEventArgs e) => SnapToRowBoundary();

        private void SnapToRowBoundary()
        {
            if (cellHeight <= 0) return;
            if (snapAnimTimer.IsEnabled) return; // first-click animation still running — let it complete
            double target = Math.Round(ScrollViewer.VerticalOffset / cellHeight) * cellHeight;
            AnimateSnapScroll(target);
        }

        // Handles a track-click RepeatButton tick. direction is +1 (down) or -1 (up).
        // First click animates smoothly to the next row boundary.
        // Subsequent repeats ease-in from slow fractional steps to a full row per tick.
        private void DoTrackScroll(double direction)
        {
            if (cellHeight <= 0) return;
            if (trackScrollFirstClick)
            {
                trackScrollFirstClick = false;
                double target = Math.Round(ScrollViewer.VerticalOffset / cellHeight) * cellHeight
                                + direction * cellHeight;
                AnimateSnapScroll(target);
                return;
            }
            double elapsed = (DateTime.Now - trackScrollHoldStart).TotalMilliseconds;
            double t = Math.Min((elapsed - 600) / 2400.0, 1.0);
            double step = Math.Max(0.1, t * t) * cellHeight;
            ScrollViewer.ScrollToVerticalOffset(ScrollViewer.VerticalOffset + direction * step);
        }

        private void AnimateSnapScroll(double target, Action onComplete = null, int steps = SnapAnimStepsSnap)
        {
            snapAnimTimer.Stop();
            snapAnimCompletionAction = null;
            snapAnimStart = ScrollViewer.VerticalOffset;
            if (Math.Abs(target - snapAnimStart) < 0.5)
            {
                onComplete?.Invoke();
                return;
            }
            snapAnimCompletionAction = onComplete;
            snapAnimTotalSteps = steps;
            snapAnimTarget = target;
            snapAnimStep = 0;
            snapAnimTimer.Start();
        }

        private void SnapAnimTimer_Tick(object sender, EventArgs e)
        {
            snapAnimStep++;
            double t = Math.Min(1.0, (double)snapAnimStep / snapAnimTotalSteps);
            double eased = 1.0 - Math.Pow(1.0 - t, 2); // ease-out quadratic
            suppressAnchorUpdate = true;
            ScrollViewer.ScrollToVerticalOffset(snapAnimStart + (snapAnimTarget - snapAnimStart) * eased);
            if (snapAnimStep >= snapAnimTotalSteps)
            {
                snapAnimTimer.Stop();
                Action completion = snapAnimCompletionAction;
                snapAnimCompletionAction = null;
                completion?.Invoke();
            }
        }
        #endregion

        #region Public Methods

        public void Reset()
        {
            CancelUpdate();
            ClearPool();
            selectedIndices.Clear();
            anchorIndex = -1;
            cellHeight = 0;
            currentFirstRow = 0;
            zoomAnchorFileIndex = 0;
            suppressAnchorUpdate = false;
            zoomLevel = 0;
            initialCellHeight = 0;
            BeginAnimation(OpacityProperty, null);
            Opacity = 1.0;
            VirtualCanvas.BeginAnimation(OpacityProperty, null);
            VirtualCanvas.Opacity = 1.0;
            homeSlideActive = false;
            RenderTransformOrigin = new Point(0.5, 0.5);
            RenderTransform = Transform.Identity;
            ResetHomePreviewOverlay();
            snapAnimTimer.Stop();
            snapAnimCompletionAction = null;
            autoScrollTimer.Stop();
            startPadding = 0;
            HideSelectionBadge();
        }

        public ThumbnailGridRefreshStatus Refresh(double newGridWidth, double newGridHeight, bool? zoomIn)
        {
            if (FileTable == null || FileTable.RowCount == 0)
                return ThumbnailGridRefreshStatus.Aborted;
            if (newGridWidth <= 0 || newGridHeight <= 0)
                return ThumbnailGridRefreshStatus.Aborted;

            currentGridWidth = newGridWidth;
            currentGridHeight = newGridHeight;

            // Pass the persistent zoom anchor so RebuildPool scrolls to the row that contains
            // the same file after column count changes. zoomAnchorFileIndex is only updated on
            // manual scrolls — zoom-triggered ScrollChanged events are suppressed so the anchor
            // does not drift with each zoom step.
            int? anchorFileIndex = IsGridActive ? zoomAnchorFileIndex : null;

            // Discrete zoom levels make every zoom-out exactly reversible by one zoom-in.
            // Level 0 = deactivated. Level 1 = initial view (initialCellHeight).
            // Level N: cellHeight = initialCellHeight * ZoomStep^(N-1).
            const double ZoomStep = 0.8;

            if (zoomIn == false)
            {
                if (zoomLevel == 0)
                {
                    zoomLevel = 1;
                    initialCellHeight = newGridHeight / 3.0;
                    cellHeight = initialCellHeight;
                }
                else
                {
                    double proposed = initialCellHeight * Math.Pow(ZoomStep, zoomLevel);
                    if (proposed < Constant.ThumbnailGrid.MinumumThumbnailHeight)
                        return ThumbnailGridRefreshStatus.AtMaximumZoomLevel;
                    zoomLevel++;
                    cellHeight = proposed;
                }
            }
            else if (zoomIn == true)
            {
                if (zoomLevel <= 1)
                {
                    // If the anchor differs from the home thumbnail, animate the grid scrolling
                    // to the home thumbnail before deactivating — visual feedback that we are
                    // returning to the home image.
                    if (zoomLevel == 1 && zoomAnchorFileIndex != FileTableStartIndex
                        && columnCount > 0 && cellHeight > 0)
                    {
                        homeSlideActive = true;

                        int homeRow = (startPadding + FileTableStartIndex) / columnCount;
                        double scrollStart = ScrollViewer.VerticalOffset;
                        double rawRange   = homeRow * cellHeight - scrollStart;
                        // Cap at one screen height; preserve direction for off-screen home.
                        double scrollRange = rawRange == 0 ? 0
                            : Math.Sign(rawRange) * Math.Min(currentGridHeight, Math.Abs(rawRange));
                        DateTime animStart = DateTime.Now;
                        double totalMs = 1000.0;

                        DispatcherTimer scrollTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
                        scrollTimer.Tick += (t, _) =>
                        {
                            if (!homeSlideActive) { ((DispatcherTimer)t)?.Stop(); return; }
                            double progress = Math.Min((DateTime.Now - animStart).TotalMilliseconds / totalMs, 1.0);
                            ScrollViewer.ScrollToVerticalOffset(scrollStart + scrollRange * Math.Pow(progress, 3));
                            if (progress >= 1.0) ((DispatcherTimer)t)?.Stop();
                        };
                        scrollTimer.Start();

                        DoubleAnimation fade = new(1, 0, new Duration(TimeSpan.FromMilliseconds(totalMs)))
                        {
                            EasingFunction = new PowerEase { Power = 5, EasingMode = EasingMode.EaseIn }
                        };
                        fade.Completed += (_, _) =>
                        {
                            if (!homeSlideActive) return;
                            homeSlideActive = false;
                            scrollTimer.Stop();
                            BeginAnimation(OpacityProperty, null);
                            Opacity = 1.0;
                            zoomLevel = 0;
                            cellHeight = 0;
                            OnDeactivateRequested();
                        };
                        BeginAnimation(OpacityProperty, fade);
                        return ThumbnailGridRefreshStatus.AnimatingToHome;
                    }
                    zoomLevel = 0;
                    cellHeight = 0;
                    return ThumbnailGridRefreshStatus.AtZeroZoomLevel;
                }
                zoomLevel--;
                cellHeight = initialCellHeight * Math.Pow(ZoomStep, zoomLevel - 1);
            }

            if (cellHeight <= 0)
                return ThumbnailGridRefreshStatus.Aborted;

            // Sample aspect ratio from the top-left visible image
            BitmapSource sampleBm = FileTable[FileTableStartIndex].LoadBitmap(
                RootPathToImages, ImageValues.PreviewWidth32,
                ImageDisplayIntentEnum.Ephemeral, ImageDimensionEnum.UseWidth, out _);
            double aspectRatio = (sampleBm == null || sampleBm.PixelHeight == 0)
                ? Constant.ThumbnailGrid.AspectRatioDefault
                : (double)sampleBm.PixelWidth / sampleBm.PixelHeight;

            CancelUpdate();
            RebuildPool(cellHeight * aspectRatio, cellHeight, anchorFileIndex);

            if (pool.Count == 0)
                return ThumbnailGridRefreshStatus.NotEnoughSpaceForEvenOneCell;

            // SelectInitialCellOnly() is intentionally NOT called here.
            // Selection is preserved across zoom and resize. The initial-activation
            // call in TryZoomInOrOutVirtualized handles the first-time selection.

            return ThumbnailGridRefreshStatus.Ok;
        }

        public void CancelUpdate()
        {
            try { backgroundWorker?.CancelAsync(); }
            catch { /* cancellation errors are acceptable */ }
        }

        public void SelectInitialCellOnly()
        {
            selectedIndices.Clear();
            anchorIndex = -1;
            if (FileTable != null && FileTableStartIndex >= 0 && FileTableStartIndex < FileTable.RowCount)
            {
                selectedIndices.Add(FileTableStartIndex);
                anchorIndex = FileTableStartIndex;
            }
            RefreshSelectionVisuals();
            EnableOrDisableControlsAsNeeded();
            OnSelectionChanged(new ThumbnailGridVirtualizedEventArgs(null));
        }

        // Navigate to a specific file: select it and scroll the viewport to show it.
        // Called by FilePlayer / View-menu navigation when the virtual grid is active.
        // Scroll the viewport so fileIndex appears at the top-left corner.
        // Does not alter the user's selection — only the visible position changes.
        public void NavigateTo(int fileIndex)
        {
            if (FileTable == null || fileIndex < 0 || fileIndex >= FileTable.RowCount) return;
            if (columnCount > 0 && cellHeight > 0)
                RelayoutForAnchor(fileIndex);
        }

        public List<int> GetSelected() => [.. selectedIndices];

        public int SelectedCount() => selectedIndices.Count;

        public void RefreshBoundingBoxesAndEpisodeInfo()
        {
            foreach (ThumbnailInCell cell in pool.Where(c => c.Visibility == Visibility.Visible && c.ImageRow != null))
                cell.RefreshBoundingBoxesDuplicatesAndEpisodeInfo(FileTable, cell.FileTableIndex);
        }

        private void ShowSelectionBadge(int count)
        {
            if (count == 0) { HideSelectionBadge(); return; }

            SelectionBadge.BeginAnimation(OpacityProperty, null);
            SelectionBadge.Opacity = 1.0;
            SelectionBadgeText.Text = count == 1 ? "1 selected" : $"{count:N0} selected";

            Point pos = Mouse.GetPosition(this);
            const double BadgeW = 130, BadgeH = 30, CursorH = 20;
            double left = pos.X;
            if (left + BadgeW > ActualWidth) left = pos.X - BadgeW;
            double top = pos.Y + CursorH;
            if (top + BadgeH > ActualHeight) top = pos.Y - BadgeH;
            SelectionBadge.Margin = new Thickness(Math.Max(0, left), Math.Max(0, top), 0, 0);
            SelectionBadge.Visibility = Visibility.Visible;
        }

        private void ResetHomePreviewOverlay()
        {
            HomePreviewBorder.BeginAnimation(OpacityProperty, null);
            HomePreviewBorder.Opacity = 1;
            if (HomePreviewBorder.RenderTransform is ScaleTransform st)
            { st.ScaleX = 1; st.ScaleY = 1; }
            HomePreviewBorder.Visibility = Visibility.Collapsed;
        }

        private void HideSelectionBadge()
        {
            SelectionBadge.BeginAnimation(OpacityProperty, null);
            SelectionBadge.Visibility = Visibility.Collapsed;
        }

        private void StartSelectionBadgeFade()
        {
            DoubleAnimation fade = new(1.0, 0.0, TimeSpan.FromMilliseconds(700));
            fade.Completed += (_, _) => SelectionBadge.Visibility = Visibility.Collapsed;
            SelectionBadge.BeginAnimation(OpacityProperty, fade);
        }

        #endregion

        #region Private: Pool Management

        private void RebuildPool(double newCellWidth, double newCellHeight, int? anchorFileIndex = null)
        {
            // Cancel any in-progress animations so stale callbacks never fire.
            BeginAnimation(OpacityProperty, null);
            Opacity = 1.0;
            VirtualCanvas.BeginAnimation(OpacityProperty, null);
            VirtualCanvas.Opacity = 1.0;
            homeSlideActive = false;
            RenderTransformOrigin = new Point(0.5, 0.5);
            RenderTransform = Transform.Identity;
            ResetHomePreviewOverlay();
            snapAnimTimer.Stop();
            snapAnimCompletionAction = null;
            ClearPool();

            if (newCellHeight <= 0 || newCellWidth <= 0 || FileTable == null || FileTable.RowCount == 0)
                return;

            cellWidth = newCellWidth;
            cellHeight = newCellHeight;

            // Column count must use the viewport content width (excluding the scrollbar).
            // Using the full control width causes VirtualCanvas to extend into the scrollbar area;
            // clicks on the scrollbar then map to the last column and trigger spurious selection.
            double contentWidth = ScrollViewer.ViewportWidth > 0 ? ScrollViewer.ViewportWidth : currentGridWidth;
            columnCount = Math.Max(1, (int)(contentWidth / cellWidth));

            // When the grid is narrower than one cell, scale both dimensions proportionally
            // so the single column always fits without clipping.
            if (columnCount == 1 && contentWidth < cellWidth)
            {
                double scale = contentWidth / cellWidth;
                cellWidth = contentWidth;
                cellHeight *= scale;
            }

            int anchorFile = anchorFileIndex ?? FileTableStartIndex;

            // Start with positive padding so the anchor file aligns to col=0 of its row.
            int positiveStartPadding = (columnCount - anchorFile % columnCount) % columnCount;
            int totalVirtualPositions = FileTable.RowCount + positiveStartPadding;
            int totalRows = (totalVirtualPositions + columnCount - 1) / columnCount;

            // Use positive padding so the anchor aligns to col=0 of its row.
            // Files before the anchor are accessible by scrolling up above the anchor row.
            // If the natural canvas height is too short for the scrollbar to reach the anchor row
            // (e.g. when all files from anchor to end fit in the viewport), extend the canvas
            // with empty rows at the bottom so the anchor position is always scrollable-to.
            startPadding = positiveStartPadding;

            // Always check: if the anchor row can't be reached with the natural canvas height
            // (e.g. first zoom onto a late file, or zoomed out so anchor-to-end fits in view),
            // extend the canvas with empty rows at the bottom so the scrollbar can reach it.
            // Files before the anchor remain accessible by scrolling up.
            {
                int anchorRowPositive = (positiveStartPadding + anchorFile) / columnCount;
                double desiredScroll = anchorRowPositive * cellHeight;
                double maxScroll = Math.Max(0.0, totalRows * cellHeight - currentGridHeight);

                if (desiredScroll > maxScroll)
                {
                    int anchorVisibleRows = (int)Math.Ceiling(currentGridHeight / cellHeight);
                    totalRows = Math.Max(totalRows, anchorRowPositive + anchorVisibleRows);
                }
            }

            VirtualCanvas.Height = totalRows * cellHeight;
            VirtualCanvas.Width = columnCount * cellWidth;

            int visibleRows = (int)Math.Ceiling(currentGridHeight / cellHeight) + 1;
            int poolSize = (visibleRows + 4) * columnCount;

            for (int i = 0; i < poolSize; i++)
            {
                ThumbnailInCell cell = new(cellWidth, cellHeight)
                {
                    Width = cellWidth,
                    Height = cellHeight,
                    Visibility = Visibility.Collapsed
                };
                Canvas.SetLeft(cell, 0);
                Canvas.SetTop(cell, 0);
                VirtualCanvas.Children.Add(cell);
                pool.Add(cell);
            }

            // anchorVirtualPos = startPadding + anchorFile.
            // When startPadding = -anchorFile (clamped-scroll path), anchorVirtualPos = 0
            // and anchorRow = 0 — the scroll target is always 0 in that path.
            int anchorVirtualPos = startPadding + anchorFile;
            int anchorRow = anchorVirtualPos / columnCount;
            int startRow = Math.Clamp(anchorRow, 0, Math.Max(0, totalRows - 1));
            if (!anchorFileIndex.HasValue)
                zoomAnchorFileIndex = anchorFile;
            currentFirstRow = startRow;

            // Suppress the ScrollChanged that ScrollToVerticalOffset is about to trigger so it
            // does not overwrite zoomAnchorFileIndex with the post-zoom top-left file.
            suppressAnchorUpdate = true;
            // Clamp to the actual scrollable range so AssignAndLoad always receives the offset
            // that WPF will land on (e.g. 0 when the canvas fits entirely in the viewport).
            double maxScrollOffset = Math.Max(0.0, VirtualCanvas.Height - currentGridHeight);
            double desiredOffset = Math.Min(startRow * cellHeight, maxScrollOffset);
            ScrollViewer.ScrollToVerticalOffset(desiredOffset);

            AssignAndLoad(desiredOffset);
        }

        // Reposition the layout so anchorFile sits at column 0 of its row and scrolls to the top.
        // Re-uses the existing pool — no items are created or destroyed.
        // For row/page increments startPadding is unchanged (fast path: just a scroll).
        // For single-file increments startPadding shifts by one position (canvas height may change by one row).
        private void RelayoutForAnchor(int anchorFile)
        {
            if (columnCount <= 0 || cellHeight <= 0 || FileTable == null) return;

            int newStartPadding = (columnCount - anchorFile % columnCount) % columnCount;
            int totalVirtualPositions = FileTable.RowCount + newStartPadding;
            int totalRows = (totalVirtualPositions + columnCount - 1) / columnCount;
            int targetRow = (newStartPadding + anchorFile) / columnCount;

            // Mirror RebuildPool: extend canvas so the anchor row can always be scrolled to the top.
            double desiredScroll = targetRow * cellHeight;
            double maxScroll = Math.Max(0.0, totalRows * cellHeight - currentGridHeight);
            if (desiredScroll > maxScroll)
                totalRows = targetRow + (int)Math.Ceiling(currentGridHeight / cellHeight);

            double newHeight = totalRows * cellHeight;
            if (newStartPadding != startPadding || VirtualCanvas.Height != newHeight)
            {
                startPadding = newStartPadding;
                VirtualCanvas.Height = newHeight;
            }

            zoomAnchorFileIndex = anchorFile;
            currentFirstRow = targetRow;
            suppressAnchorUpdate = true;
            ScrollViewer.ScrollToVerticalOffset(desiredScroll);
            AssignAndLoad(desiredScroll);
        }

        private void AssignPoolControls(double? scrollOffset = null)
        {
            if (pool.Count == 0 || FileTable == null || columnCount == 0 || cellHeight <= 0)
                return;

            // Use the supplied offset (e.g. from RebuildPool before ScrollViewer has applied
            // ScrollToVerticalOffset) or fall back to the current scroll position.
            double offset = scrollOffset ?? ScrollViewer.VerticalOffset;
            int firstVisibleRow = (int)(offset / cellHeight);
            const int BufferRows = 2;
            int firstBufferedRow = Math.Max(0, firstVisibleRow - BufferRows);
            int totalFileCount = FileTable.RowCount;

            for (int poolIdx = 0; poolIdx < pool.Count; poolIdx++)
            {
                ThumbnailInCell cell = pool[poolIdx];
                int row = firstBufferedRow + poolIdx / columnCount;
                int col = poolIdx % columnCount;
                int virtualPos = row * columnCount + col;
                int fileIdx = virtualPos - startPadding;

                if (fileIdx < 0 || fileIdx >= totalFileCount)
                {
                    cell.Visibility = Visibility.Collapsed;
                    continue;
                }

                ImageRow imageRow = FileTable[fileIdx];
                bool fileChanged = cell.FileTableIndex != fileIdx || cell.ImageRow != imageRow;

                cell.ImageRow = imageRow;
                cell.FileTableIndex = fileIdx;
                cell.GridIndex = poolIdx;
                cell.Row = row;
                cell.Column = col;
                cell.RootPathToImages = RootPathToImages;

                if (fileChanged)
                {
                    cell.BoundingBoxes = GlobalReferences.MainWindow?.GetBoundingBoxesForCurrentFile(imageRow.ID, true)
                        ?? new BoundingBoxes();
                    cell.DateTimeLastBitmapWasSet = DateTime.MinValue;

                    if (bitmapCache.TryGetValue(fileIdx, out BitmapSource cached))
                        cell.SetThumbnail(cached);
                    else
                        cell.ClearThumbnail();

                    if (imageRow.IsVideo)
                    {
                        cell.InitializePlayButton();
                        cell.PlayButton.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        cell.PlayButton.Visibility = Visibility.Collapsed;
                    }

                    cell.RefreshBoundingBoxesDuplicatesAndEpisodeInfo(FileTable, fileIdx);
                }

                cell.IsSelected = selectedIndices.Contains(fileIdx);
                cell.IsHome = fileIdx == FileTableStartIndex;

                Canvas.SetLeft(cell, col * cellWidth);
                Canvas.SetTop(cell, row * cellHeight);
                cell.Visibility = Visibility.Visible;
            }
        }

        private void AssignAndLoad(double? scrollOffset = null)
        {
            AssignPoolControls(scrollOffset);
            StartBackgroundLoad();
        }

        private void ClearPool()
        {
            foreach (ThumbnailInCell cell in pool)
                VirtualCanvas.Children.Remove(cell);
            pool.Clear();
            bitmapCache.Clear();
            cacheInsertionOrder.Clear();
        }

        private void RefreshSelectionVisuals()
        {
            foreach (ThumbnailInCell cell in pool)
            {
                cell.IsSelected = selectedIndices.Contains(cell.FileTableIndex);
                cell.IsHome = cell.FileTableIndex == FileTableStartIndex;
            }
        }

        private void EnableOrDisableControlsAsNeeded()
        {
            if (DataEntryControls == null) return;
            DataEntryControls.SetEnableState(
                Visibility == Visibility.Collapsed
                    ? ControlsEnableStateEnum.SingleImageView
                    : ControlsEnableStateEnum.MultipleImageView,
                Visibility == Visibility.Collapsed ? -1 : SelectedCount());
        }

        #endregion

        #region Private: Bitmap Loading

        private void StartBackgroundLoad()
        {
            CancelUpdate();

            // Snapshot visible cells for the worker — safe to iterate off-thread
            List<ThumbnailInCell> cellsToLoad = [.. pool.Where(c => c.Visibility == Visibility.Visible && c.ImageRow != null)];

            if (cellsToLoad.Count == 0) return;

            double captureCellWidth = cellWidth;
            double captureCellHeight = cellHeight;

            // Capture a local reference so each lambda closure always refers to this specific
            // worker instance. If StartBackgroundLoad is called again before this one finishes,
            // the field is replaced; without a local, the old lambda would call ReportProgress
            // on the new (not-yet-started or already-completed) worker and crash.
            BackgroundWorker worker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            backgroundWorker = worker;

            worker.DoWork += (_, ea) =>
            {
                try
                {
                    // Pass 1: images (faster)
                    foreach (ThumbnailInCell cell in cellsToLoad)
                    {
                        if (worker.CancellationPending) { ea.Cancel = true; return; }
                        if (cell.ImageRow?.IsVideo == true || cell.IsBitmapSet) continue;

                        BitmapSource bm = cell.GetThumbnail(captureCellWidth, captureCellHeight);
                        DateTime stamp = DateTime.Now;
                        cell.DateTimeLastBitmapWasSet = stamp;

                        if (worker.CancellationPending) { ea.Cancel = true; return; }

                        worker.ReportProgress(0, new LoadImageProgressStatus
                        {
                            ThumbnailInCell = cell,
                            BitmapSource = bm,
                            GridIndex = cell.GridIndex,
                            CellWidth = captureCellWidth,
                            FileTableIndex = cell.FileTableIndex,
                            DateTimeLipInvoked = stamp
                        });
                    }

                    // Pass 2: videos (slower)
                    foreach (ThumbnailInCell cell in cellsToLoad)
                    {
                        if (worker.CancellationPending) { ea.Cancel = true; return; }
                        if (cell.ImageRow?.IsVideo != true || cell.IsBitmapSet) continue;

                        BitmapSource bm = cell.GetThumbnail(captureCellWidth, captureCellHeight);
                        DateTime stamp = DateTime.Now;
                        cell.DateTimeLastBitmapWasSet = stamp;

                        if (worker.CancellationPending) { ea.Cancel = true; return; }

                        worker.ReportProgress(0, new LoadImageProgressStatus
                        {
                            ThumbnailInCell = cell,
                            BitmapSource = bm,
                            GridIndex = cell.GridIndex,
                            CellWidth = captureCellWidth,
                            FileTableIndex = cell.FileTableIndex,
                            DateTimeLipInvoked = stamp
                        });
                    }
                }
                catch (Exception e)
                {
                    TracePrint.CatchException("ThumbnailGridVirtualized bitmap load: " + e.Message);
                }
            };

            worker.ProgressChanged += (_, ea) =>
                UpdateThumbnailLoadProgress((LoadImageProgressStatus)ea.UserState);

            worker.RunWorkerCompleted += (_, _) =>
                worker.Dispose();

            worker.RunWorkerAsync();
        }

        private void UpdateThumbnailLoadProgress(LoadImageProgressStatus lip)
        {
            try
            {
                if (lip.BitmapSource == null) return;
                ThumbnailInCell cell = lip.ThumbnailInCell;

                // Stale-bitmap guard: reject if cell was reassigned since this load started
                if (cell.DateTimeLastBitmapWasSet != lip.DateTimeLipInvoked) return;
                if (cell.FileTableIndex != lip.FileTableIndex) return;

                cell.SetThumbnail(lip.BitmapSource);
                cell.RefreshBoundingBoxesDuplicatesAndEpisodeInfo(FileTable, lip.FileTableIndex);

                if (!bitmapCache.ContainsKey(lip.FileTableIndex))
                {
                    bitmapCache[lip.FileTableIndex] = lip.BitmapSource;
                    cacheInsertionOrder.Enqueue(lip.FileTableIndex);
                    if (bitmapCache.Count > BitmapCacheMaxSize)
                    {
                        int evicted = cacheInsertionOrder.Dequeue();
                        bitmapCache.Remove(evicted);
                    }
                }
            }
            catch
            {
                // Acceptable during rapid scroll/cancel
            }
        }

        #endregion

        #region Events

        public event EventHandler<ThumbnailGridVirtualizedEventArgs> DoubleClick;
        public event EventHandler<ThumbnailGridVirtualizedEventArgs> SelectionChanged;
        public event EventHandler DeactivateRequested;

        protected virtual void OnDoubleClick(ThumbnailGridVirtualizedEventArgs e) => DoubleClick?.Invoke(this, e);
        protected virtual void OnSelectionChanged(ThumbnailGridVirtualizedEventArgs e) => SelectionChanged?.Invoke(this, e);
        protected virtual void OnDeactivateRequested() => DeactivateRequested?.Invoke(this, EventArgs.Empty);

        #endregion

        #region Mouse Event Handlers

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // GetPosition(VirtualCanvas) gives canvas coordinates directly,
                // accounting for scroll offset via WPF's visual transform — no manual math needed.
                Point canvasPt = e.GetPosition(VirtualCanvas);
                double canvasX = canvasPt.X;
                double canvasY = canvasPt.Y;
                int clickedIdx = FileIndexFromCanvasPoint(canvasX, canvasY);
                if (clickedIdx < 0)
                {
                    // Click landed outside a cell (e.g. scrollbar area): suppress drag selection
                    // so the stale dragStartFileIndex is not used in OnMouseMove.
                    isDraggingFromCanvas = false;
                    return;
                }

                bool ctrlDown = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
                bool shiftDown = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
                modifierPressedOnMouseDown = ctrlDown || shiftDown;
                isDraggingFromCanvas = true;
                dragStartFileIndex = clickedIdx;
                Mouse.Capture(this);

                if (e.ClickCount == 2)
                {
                    ThumbnailInCell cell = pool.FirstOrDefault(c => c.Visibility == Visibility.Visible && c.FileTableIndex == clickedIdx);
                    OnDoubleClick(new ThumbnailGridVirtualizedEventArgs(cell?.ImageRow));
                    cellHeight = 0;
                    e.Handled = true;
                    return;
                }

                if (ctrlDown)
                {
                    if (!selectedIndices.Remove(clickedIdx))
                        selectedIndices.Add(clickedIdx);
                    anchorIndex = clickedIdx;
                }
                else if (shiftDown)
                {
                    if (anchorIndex < 0) anchorIndex = clickedIdx;
                    int lo = Math.Min(anchorIndex, clickedIdx);
                    int hi = Math.Max(anchorIndex, clickedIdx);
                    selectedIndices.Clear();
                    for (int i = lo; i <= hi; i++)
                        selectedIndices.Add(i);
                    // anchorIndex is intentionally not updated on shift-click
                }
                else
                {
                    selectedIndices.Clear();
                    selectedIndices.Add(clickedIdx);
                    anchorIndex = clickedIdx;
                }

                RefreshSelectionVisuals();
                EnableOrDisableControlsAsNeeded();
                ShowSelectionBadge(selectedIndices.Count);
                OnSelectionChanged(new ThumbnailGridVirtualizedEventArgs(null));
                e.Handled = true;
            }
            catch { /* ignored */ }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || modifierPressedOnMouseDown || !isDraggingFromCanvas) return;
            if (cellWidth <= 0 || cellHeight <= 0 || FileTable == null) return;

            lastDragViewerPoint = e.GetPosition(ScrollViewer);
            UpdateDragSelection();

            const double zone = 60.0;
            double viewportHeight = ScrollViewer.ViewportHeight;
            double y = lastDragViewerPoint.Y;

            if (y < zone && ScrollViewer.VerticalOffset > 0)
            {
                autoScrollDelta = -cellHeight;
                if (!autoScrollTimer.IsEnabled) autoScrollTimer.Start();
            }
            else if (y > viewportHeight - zone && ScrollViewer.VerticalOffset < ScrollViewer.ScrollableHeight)
            {
                autoScrollDelta = cellHeight;
                if (!autoScrollTimer.IsEnabled) autoScrollTimer.Start();
            }
            else
            {
                autoScrollDelta = 0;
                autoScrollTimer.Stop();
            }
        }

        private void UpdateDragSelection()
        {
            double canvasX = Math.Max(0, lastDragViewerPoint.X);
            double canvasY = Math.Max(0, lastDragViewerPoint.Y + ScrollViewer.VerticalOffset);

            int col = Math.Clamp((int)(canvasX / cellWidth), 0, columnCount - 1);
            int row = (int)(canvasY / cellHeight);
            int currentIdx = Math.Clamp(row * columnCount + col - startPadding, 0, FileTable.RowCount - 1);

            int lo = Math.Min(dragStartFileIndex, currentIdx);
            int hi = Math.Max(dragStartFileIndex, currentIdx);

            selectedIndices.Clear();
            for (int i = lo; i <= hi; i++)
                selectedIndices.Add(i);

            RefreshSelectionVisuals();
            EnableOrDisableControlsAsNeeded();
            ShowSelectionBadge(selectedIndices.Count);
        }

        private void AutoScrollTimer_Tick(object sender, EventArgs e)
        {
            if (!isDraggingFromCanvas)
            {
                autoScrollTimer.Stop();
                return;
            }
            ScrollViewer.ScrollToVerticalOffset(ScrollViewer.VerticalOffset + autoScrollDelta);
            UpdateDragSelection();
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isDraggingFromCanvas)
                e.Handled = true;  // prevent bubbling to DataGrid row handler which would switch to single-image view
            StopDrag();
        }

        private void OnLostMouseCapture(object sender, MouseEventArgs e)
        {
            StopDrag();
        }

        private void StopDrag()
        {
            if (!isDraggingFromCanvas) return;
            isDraggingFromCanvas = false;
            modifierPressedOnMouseDown = false;
            autoScrollTimer.Stop();
            Mouse.Capture(null);
            StartSelectionBadgeFade();
            OnSelectionChanged(new ThumbnailGridVirtualizedEventArgs(null));
        }

        private int FileIndexFromCanvasPoint(double canvasX, double canvasY)
        {
            if (cellWidth <= 0 || cellHeight <= 0 || FileTable == null) return -1;
            int col = (int)(canvasX / cellWidth);
            int row = (int)(canvasY / cellHeight);
            if (col < 0 || col >= columnCount) return -1;
            int fileIdx = row * columnCount + col - startPadding;
            return fileIdx < 0 || fileIdx >= FileTable.RowCount ? -1 : fileIdx;
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange == 0 && e.ViewportHeightChange == 0 && e.ExtentHeightChange == 0) return;
            if (cellHeight > 0)
                currentFirstRow = (int)Math.Round(ScrollViewer.VerticalOffset / cellHeight);
            if (suppressAnchorUpdate)
            {
                // This ScrollChanged was triggered by a zoom-initiated ScrollToVerticalOffset.
                // Do not update zoomAnchorFileIndex — the anchor must persist across zoom steps.
                suppressAnchorUpdate = false;
            }
            else if (columnCount > 0 && e.ExtentHeightChange == 0)
            {
                // Genuine user scroll (not a canvas-resize event): update the anchor and arm
                // the row-snap timer. The top-left virtual position is currentFirstRow *
                // columnCount; subtract startPadding to get the file index, clamped to >= 0.
                zoomAnchorFileIndex = Math.Max(0, currentFirstRow * columnCount - startPadding);
            }
            CancelUpdate();
            AssignAndLoad();
        }

        // Raised when the user Ctrl+scrolls over the grid so MarkableCanvas can zoom.
        // ScrollViewer_PreviewMouseWheel intercepts all wheel events (blocking the ScrollViewer's
        // own scroll), so Ctrl+scroll cannot bubble naturally — the event carries the delta instead.
        public event EventHandler<MouseWheelEventArgs> CtrlMouseWheelScrolled;

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (NativeMethods.IsCtrlKeyDown())
            {
                e.Handled = true;
                CtrlMouseWheelScrolled?.Invoke(this, e);
            }
            // Plain scroll: let the ScrollViewer handle it naturally
        }

        #endregion
    }

    internal class LoadImageProgressStatus
    {
        public ThumbnailInCell ThumbnailInCell { get; set; }
        public BitmapSource BitmapSource { get; set; }
        public int GridIndex { get; set; }
        public double CellWidth { get; set; }
        public int FileTableIndex { get; set; }
        public DateTime DateTimeLipInvoked { get; set; }
    }
}
