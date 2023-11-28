using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.DebuggingSupport;
using Timelapse.Enums;
using Timelapse.EventArguments;
using RowColumn = System.Drawing.Point;

namespace Timelapse.Controls
{
    // Thumbnail Grid Overview - including cancellable asynchronous loading of images.
    // A user can use the mouse wheel to not only zoom into an image, but also to zoom out into an overview that displays 
    // multiple thumbnails at the same time in a grid. There are multiple levels of overviews, 
    // each adding an additional row of images at smaller sizes up to a minimum size and a maximum number of rows.
    // The user can multi-select images, where any data entered will be applied to the selected images.  
    // However, selections are reset between navigations and zoom levels.

    // While not yet done, this could  be extended to  use infinite scroll, but that could introduce some issues  in how user selections are done, 
    // where mis-selections are possible as some images will be out of site.

    public partial class ThumbnailGrid
    {
        #region Public properties

        // DataEntryControls needs to be set externally
        public DataEntryControls DataEntryControls { get; set; }

        // FileTable needs to be set externally
        public FileTable FileTable { set; get; }

        // FileTableStartIndex needs to be set externally
        public int FileTableStartIndex { get; set; }

        // FullSubFolderPath needs to be set externally
        // The root folder containing the template
        public string FolderPath { get; set; }

        // The number of columns that exist in the grid
        public int AvailableColumns => this.Grid.ColumnDefinitions.Count;

        // The number of rows that currently exist in the ThumbnailGrid
        public int AvailableRows => this.Grid.RowDefinitions.Count;

        // Whether the Grid is activated (i..e, with a zoom level), 
        public bool IsGridActive => Level > 0;
        #endregion

        #region Private variables
        private List<ThumbnailInCell> thumbnailInCells;

        // Track states between mouse down / move and up 
        private RowColumn cellChosenOnMouseDown;
        private bool modifierKeyPressedOnMouseDown;
        private RowColumn cellWithLastMouseOver = new RowColumn(-1, -1);
        private List<ThumbnailInCell> thumbnailsAlreadyInGrid = new List<ThumbnailInCell>();
        private double oldGridWidth;
        private double oldGridHeight;
        private int oldCellHeight;
        private int Level; // 0 Grid not active, 1 to max progressively zooms out
        #endregion

        #region Constructor
        public ThumbnailGrid()
        {
            this.InitializeComponent();
            this.FileTableStartIndex = 0;
        }
        #endregion

        #region Public Reset
        public void Reset()
        {
            this.thumbnailInCells = null;
            this.Level = 0;
        }
        #endregion

        #region Public Refresh 
        // Rebuild the grid, based on 
        // - fitting the image into as many cells of the same size that can fit within the grid
        // - retaining information about images previously shown on this grid.
        // Probably way more complicated than it has to be, but it works!
        // Note: every refresh unselects previously selected images
        public ThumbnailGridRefreshStatus Refresh(double gridWidth, double gridHeight, bool? showFewerCells)
        {
            // Lots of tests for particular conditions and optimizations before we do the actual refresh
            if (this.FileTable == null || !this.FileTable.Any())
            {
                // There aren't any images to show, so no point in going on
                return ThumbnailGridRefreshStatus.Aborted;
            }

            bool? showMoreCells = !showFewerCells; // For clarity in reading code
            // Set the zoomOut levels, but abort if we are at either zoom limit
            if (showMoreCells == true && this.Level >= Constant.ThumbnailGrid.MaxRows)
            {
                // Showing more cells aborted as already displaying the maximum amount of rows. 
                //Debug.Print(String.Format("0 ThumbnailGridRefreshStatus.AtMaximumZoomLevel {0}", this.Level));
                return ThumbnailGridRefreshStatus.AtMaximumZoomLevel;
            }
            else if (showMoreCells == true)
            {
                // Showing more cells, so increase the zoom out by one level
                this.Level++;
            }
            else if (showFewerCells == true)
            {
                // Showing fewer cells, so decrease the zoom out by one level
                this.Level--;
                if (this.Level <= 0)
                {
                    // Showing fewer cells aborted as we are already at or below zero 
                    //Debug.Print(String.Format("2b this.Level <= 0 {0}", this.Level));
                    this.Level = 0;
                    return ThumbnailGridRefreshStatus.AtZeroZoomLevel;
                }
            }

            // Find the current height of the available space and split it the number of rows defined by the state. i.e. state 1 is 2 rows, 2 is 3 rows, etc.
            // Note that the level should be unaltered if its a resize or navigation
            int desiredCellHeight = Convert.ToInt32(gridHeight / (this.Level + 1)) - 1;  // Should be 2 rows, 3 rows, 4 rows.
            if (desiredCellHeight <= 0)
            {
                // this shouldn't happen, but just in case...
                return ThumbnailGridRefreshStatus.Aborted;
            }

            if (showFewerCells != null && desiredCellHeight < Constant.ThumbnailGrid.MinumumThumbnailHeight)
            {
                //Zoom in or out aborted, as the desired cell height at the new zoom level is less than our minimum height.
                this.Level = this.Level == 0 ? 0 : this.Level - 1; // Revert the ZoomOutLevel as its currently too small
                return ThumbnailGridRefreshStatus.AtMaximumZoomLevel;
            }

            // Now perform various checks if the refresh was due to a navigation or resize (i.e., a change in zoom level was not explicitly requested when zoomIn is null)
            //this.thumbnailsAlreadyInGrid.Clear(); // we will fill this only if we are navigating
            int desiredCellWidth = 0;
            bool navigating = false;
            if (showFewerCells == null)
            {
                // On a resize, we try to keep the cell size constant
                desiredCellHeight = oldCellHeight;
                desiredCellWidth = (this.thumbnailInCells == null || this.thumbnailInCells.Count == 0)
                    ? 0
                    : Convert.ToInt32(desiredCellHeight * this.thumbnailInCells[0].CellWidth / this.thumbnailInCells[0].CellHeight); // From the aspect ratio

                if (Math.Abs(gridWidth - this.oldGridWidth) < .0001 && Math.Abs(gridHeight - this.oldGridHeight) < .0001)
                {
                    // If the grid size hasn't changed, we must be navigating
                    navigating = true;
                }
                else
                {
                    if (desiredCellWidth == 0 || desiredCellHeight == 0)
                    {
                        // This shouldn't happen, but lets print this out just in case its a result of setting a bogus desiredCellWidth above
                        // And it is a check for 0 which would otherwise crash the next test
                    }
                    else if (this.AvailableRows == Convert.ToInt32(Math.Floor(gridHeight / desiredCellHeight))
                          && this.AvailableColumns == Convert.ToInt32(Math.Floor(gridWidth / desiredCellWidth)))
                    {
                        // A possible resize did not affect the number of available rows/columns, then we don't have to do anything
                        // Although we still ahve to save the old size, as otherwise it won't recognize that things have changed.
                        this.oldGridWidth = gridWidth;
                        this.oldGridHeight = gridHeight;
                        return ThumbnailGridRefreshStatus.Ok;
                    }
                    else if (desiredCellHeight < Constant.ThumbnailGrid.MinumumThumbnailHeight)
                    {
                        // Resizing, but we are trying to shrink it smaller than the minimum cell size
                        // Thus try refreshing again. but at a lesser level
                        this.Level--;
                        if (this.Level < 0)
                        {
                            // Zoomed in all the way, so inform the calling app that we can't do this.
                            this.Level = 0;
                            return ThumbnailGridRefreshStatus.NotEnoughSpaceForEvenOneCell;
                        }
                        return Refresh(gridWidth, gridHeight, null);
                    }
                }
            }
            this.thumbnailsAlreadyInGrid.Clear(); // we will fill this only if we are navigating
            int fileTableCount = this.FileTable.RowCount;
            if (showFewerCells == null)
            {
                // if we made it this far for navigating and resizing, then lets see if we can reuse some of thumbnails
                // as the cell size should be the same.
                this.thumbnailsAlreadyInGrid = this.GetThumbnailsAlreadyInGrid(desiredCellHeight, fileTableCount);
            }
            this.oldGridWidth = gridWidth;
            this.oldGridHeight = gridHeight;

            Mouse.OverrideCursor = Cursors.Wait;
            int cellWidth;
            if (this.thumbnailsAlreadyInGrid == null || this.thumbnailsAlreadyInGrid.Count == 0)
            {
                // As we are not reusing thumbnails, we have to calculate the cell width.
                // Get the first image as a sample to determine the apect ration, which we will use the set the width of all columns. 
                // It may not be a representative aspect ratio of all images, but its a reasonably heuristic. 
                // Note that the choice for getting the aspect ratio this way is a bit complicated. We can't just get the 'imageToDisplay' as it may
                // not be the correct one if we are navigating on the thumbnailGrid, or if it happens to be a video. So the easiest - albeit slightly less efficient -
                // way to do it is to grab the aspect ratio of the first image that will be displayed in the Thumbnail Grid. If it doesn't exist, we just use a default aspect ratio
                // Another option - to avoid the cost of gettng a bitmap on a video - is to check if its a video (jut check the path suffix) and if so use the default aspect ratio OR
                // use FFMPEG Probe (but that may mean another dll?)
                BitmapSource bm = this.FileTable[this.FileTableStartIndex].LoadBitmap(this.FolderPath, Constant.ImageValues.PreviewWidth32, ImageDisplayIntentEnum.Ephemeral, ImageDimensionEnum.UseWidth, out _);
                cellWidth = (bm == null || bm.PixelHeight == 0) ? Convert.ToInt32(desiredCellHeight * Constant.ThumbnailGrid.AspectRatioDefault) : Convert.ToInt32(desiredCellHeight * bm.PixelWidth / bm.PixelHeight);
                //Debug.Print(String.Format("Bitmap {0} {1}", cellWidth, desiredCellHeight));
            }
            else
            {
                cellWidth = desiredCellWidth;
            }

            // Reconstruct the Grid with the appropriate rows/columns 
            if (this.ReconstructGrid(cellWidth, desiredCellHeight, gridWidth, gridHeight, fileTableCount, FileTableStartIndex, navigating) == false)
            {
                // Abort as the grid cannot  display even a single image
                Mouse.OverrideCursor = null;
                return ThumbnailGridRefreshStatus.NotEnoughSpaceForEvenOneCell;
            }
            this.oldCellHeight = desiredCellHeight;

            // Unselect all cells except the first one
            this.SelectInitialCellOnly();
            Mouse.OverrideCursor = null;
            return ThumbnailGridRefreshStatus.Ok;
        }
        #endregion

        #region Mouse callbacks
        // Mouse left down. Select images
        // The selection behaviours depend upon whether the CTL or SHIFT modifier key is pressed, or whether this is a double click 
        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ThumbnailInCell thumbnailInCell;
            this.cellChosenOnMouseDown = this.GetCellFromPoint(Mouse.GetPosition(this.Grid));
            RowColumn currentCell = this.GetCellFromPoint(Mouse.GetPosition(this.Grid));
            this.cellWithLastMouseOver = currentCell;

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                // CTL mouse down: change that cell (and only that cell's) state
                this.modifierKeyPressedOnMouseDown = true;
                if (Equals(this.cellChosenOnMouseDown, currentCell))
                {
                    thumbnailInCell = this.GetThumbnailInCellFromCell(currentCell);
                    if (thumbnailInCell != null)
                    {
                        thumbnailInCell.IsSelected = !thumbnailInCell.IsSelected;
                    }
                }
            }
            else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                // SHIFT mouse down: extend the selection (if any) to this point.
                this.modifierKeyPressedOnMouseDown = true;
                this.SelectExtendSelectionFrom(currentCell);
            }
            else
            {
                // Left mouse down, no modifiers keys. 
                // Select only the current cell, unselecting others.
                thumbnailInCell = this.GetThumbnailInCellFromCell(currentCell);
                if (thumbnailInCell != null)
                {
                    this.SelectNone();
                    thumbnailInCell.IsSelected = true;
                }
            }

            // If this is a double click, raise the Double click event, e.g., so that the calling app can navigate to that image.
            if (e.ClickCount == 2)
            {
                thumbnailInCell = this.GetThumbnailInCellFromCell(currentCell);
                this.Level = 0; // So we won't be zoomed out anymore
                ThumbnailGridEventArgs eventArgs = new ThumbnailGridEventArgs(this, thumbnailInCell?.ImageRow);
                this.OnDoubleClick(eventArgs);
                e.Handled = true; // Stops the double click from generating a marker on the MarkableImageCanvas
            }
            this.EnableOrDisableControlsAsNeeded();
            ThumbnailGridEventArgs selectionEventArgs = new ThumbnailGridEventArgs(this, null);
            this.OnSelectionChanged(selectionEventArgs);
        }

        // If a mouse-left drag movement, select all cells between the starting and current cell
        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            // We only pay attention to mouse-left moves without any modifier keys pressed (i.e., a drag action).
            if (e.LeftButton != MouseButtonState.Pressed || this.modifierKeyPressedOnMouseDown)
            {
                return;
            }

            // Get the cell under the mouse pointer
            RowColumn currentCell = this.GetCellFromPoint(Mouse.GetPosition(this.Grid));

            // Ignore if the cell has already been handled in the last mouse down or move event,
            if (Equals(currentCell, this.cellWithLastMouseOver))
            {
                return;
            }
            this.SelectFromInitialCellTo(currentCell);
            this.cellWithLastMouseOver = currentCell;
            this.EnableOrDisableControlsAsNeeded();
        }

        // On the mouse up, select all cells contained by its bounding box. Note that this is needed
        // as well as the mouse move version, as otherwise a down/up on the same spot won't select the cell.
        private void Grid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.cellWithLastMouseOver.X = -1;
            this.cellWithLastMouseOver.Y = -1;
            if (this.modifierKeyPressedOnMouseDown)
            {
                this.modifierKeyPressedOnMouseDown = false;
            }
        }
        #endregion

        #region Grid Selection 
        // Unselect all elements in the grid
        // Select the first (and only the first) image in the current grid
        public void SelectInitialCellOnly()
        {
            if (this.thumbnailInCells == null)
            {
                return;
            }
            this.SelectNone(); // Clear the selections
            if (this.thumbnailInCells.Any())
            {
                ThumbnailInCell ci = this.thumbnailInCells[0];
                ci.IsSelected = true;
            }
            ThumbnailGridEventArgs eventArgs = new ThumbnailGridEventArgs(this, null);
            this.OnSelectionChanged(eventArgs);
        }

        private void SelectNone()
        {
            // Unselect all ThumbnailInCells
            if (this.thumbnailInCells == null)
            {
                return;
            }
            foreach (ThumbnailInCell ci in this.thumbnailInCells)
            {
                ci.IsSelected = false;
            }
        }

        // Select all cells between the initial and currently selected cell
        private void SelectFromInitialCellTo(RowColumn currentCell)
        {
            // If the first selected cell doesn't exist, make it the same as the currently selected cell
            // ReSharper disable All
            // While Resharper says this is heuristically unreachable, I'm unsure so I am leaving it in...
            if (this.cellChosenOnMouseDown == null)
            {
                
                this.cellChosenOnMouseDown = currentCell;
            }
            // ReSharper restore All
            this.SelectNone(); // Clear the selections

            // Determine which cell is 
            DetermineTopLeftBottomRightCells(this.cellChosenOnMouseDown, currentCell, out RowColumn startCell, out RowColumn endCell);

            // Select the cells defined by the cells running from the topLeft cell to the BottomRight cell
            RowColumn indexCell = startCell;

            while (true)
            {
                ThumbnailInCell ci = this.GetThumbnailInCellFromCell(indexCell);
                // If the cell doesn't contain a ThumbnailInCell, then we are at the end.
                if (ci == null)
                {
                    break;
                }
                ci.IsSelected = true;

                // If there is no next cell, then we are at the end.
                if (this.GridGetNextCell(indexCell, endCell, out RowColumn nextCell) == false)
                {
                    break;
                }
                indexCell = nextCell;
            }
            ThumbnailGridEventArgs eventArgs = new ThumbnailGridEventArgs(this, null);
            this.OnSelectionChanged(eventArgs);
        }

        // Select all cells between the initial and currently selected cell
        private void SelectFromTo(RowColumn cell1, RowColumn cell2)
        {
            DetermineTopLeftBottomRightCells(cell1, cell2, out RowColumn startCell, out RowColumn endCell);

            // Select the cells defined by the cells running from the topLeft cell to the BottomRight cell
            RowColumn indexCell = startCell;

            while (true)
            {
                ThumbnailInCell ci = this.GetThumbnailInCellFromCell(indexCell);
                // This shouldn't happen, but ensure that the cell contains a ThumbnailInCell.
                if (ci == null)
                {
                    break;
                }
                ci.IsSelected = true;

                // If there is no next cell, then we are at the end.
                if (this.GridGetNextCell(indexCell, endCell, out RowColumn nextCell) == false)
                {
                    break;
                }
                indexCell = nextCell;
            }
            ThumbnailGridEventArgs eventArgs = new ThumbnailGridEventArgs(this, null);
            this.OnSelectionChanged(eventArgs);
        }

        private void SelectExtendSelectionFrom(RowColumn currentCell)
        {
            // If there is no previous cell, then we are at the end.
            if (this.GridGetPreviousSelectedCell(currentCell, out RowColumn previousCell))
            {
                this.SelectFromTo(previousCell, currentCell);
            }
            else if (this.GridGetNextSelectedCell(currentCell, out RowColumn nextCell))
            {
                this.SelectFromTo(currentCell, nextCell);
            }
        }

        // Get the Selected times as a list of file table indexes to the current displayed selection of files (note these are not the IDs)
        public List<int> GetSelected()
        {
            List<int> selected = new List<int>();
            if (this.thumbnailInCells == null)
            {
                return selected;
            }
            foreach (ThumbnailInCell ci in this.thumbnailInCells)
            {
                if (ci.IsSelected)
                {
                    int fileIndex = ci.FileTableIndex;
                    selected.Add(fileIndex);
                }
            }
            return selected;
        }

        public int SelectedCount()
        {
            return this.GetSelected().Count;
        }
        #endregion

        #region Reconstruct the grid (using a background worker to display thumbnails dynamically), including clearing it
        private BackgroundWorker BackgroundWorker;
        private bool ReconstructGrid(double cellWidth, double cellHeight, double gridWidth, double gridHeight, int fileTableCount, int fileTableStartIndex, bool navigating)
        {
            int fileTableIndex;
            this.BackgroundWorker = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            this.BackgroundWorker.DoWork += (ow, ea) =>
            {
                try
                {
                    LoadImageProgressStatus lip;

                    // Pass 1: Render the images (as they render faster)
                    fileTableIndex = fileTableStartIndex;
                    foreach (ThumbnailInCell thumbnailInCell in thumbnailInCells)
                    {
                        if (thumbnailInCell.ImageRow.IsVideo == false)
                        {
                            if (this.BackgroundWorker.CancellationPending)
                            {
                                ea.Cancel = true;
                                this.BackgroundWorker.WorkerReportsProgress = false;
                                return;
                            }

                            // This may be somewhat expensive, maybe a better way to do this (e.g., set a flag in tic to say its rendered?
                            if (thumbnailInCell.IsBitmapSet)
                            {
                                fileTableIndex++;
                                continue;
                            }

                            BitmapSource bm = thumbnailInCell.GetThumbnail(cellWidth, cellHeight);
                            thumbnailInCell.DateTimeLastBitmapWasSet = DateTime.Now;
                            lip = new LoadImageProgressStatus
                            {
                                ThumbnailInCell = thumbnailInCell,
                                BitmapSource = bm,
                                GridIndex = thumbnailInCell.GridIndex,
                                CellWidth = cellWidth,
                                FileTableIndex = fileTableIndex,
                                DateTimeLipInvoked = thumbnailInCell.DateTimeLastBitmapWasSet
                            };

                            if (this.BackgroundWorker.CancellationPending)
                            {
                                ea.Cancel = true;
                                return;
                            }
                            this.BackgroundWorker.ReportProgress(0, lip);
                        }
                        fileTableIndex++;
                    }

                    // Pass 2: Then render the videos (as these are slower)
                    fileTableIndex = fileTableStartIndex;
                    foreach (ThumbnailInCell thumbnailInCell in thumbnailInCells)
                    {
                        if (thumbnailInCell.ImageRow.IsVideo)
                        {
                            if (thumbnailInCell.IsBitmapSet)
                            {
                                fileTableIndex++;
                                continue;
                            }
                            if (this.BackgroundWorker.CancellationPending)
                            {
                                ea.Cancel = true;
                                return;
                            }
                            BitmapSource bm = thumbnailInCell.GetThumbnail(cellWidth, cellHeight);
                            thumbnailInCell.DateTimeLastBitmapWasSet = DateTime.Now;
                            lip = new LoadImageProgressStatus
                            {
                                ThumbnailInCell = thumbnailInCell,
                                BitmapSource = bm,
                                GridIndex = thumbnailInCell.GridIndex,
                                CellWidth = cellWidth,
                                FileTableIndex = fileTableIndex,
                                DateTimeLipInvoked = thumbnailInCell.DateTimeLastBitmapWasSet
                            };
                            if (this.BackgroundWorker.CancellationPending)
                            {
                                ea.Cancel = true;
                                return;
                            }
                            this.BackgroundWorker.ReportProgress(0, lip);
                        }
                        fileTableIndex++;
                    }
                }
                catch
                {
                    TracePrint.CatchException("Catch is acceptable");
                }
            };

            this.BackgroundWorker.ProgressChanged += (o, ea) =>
            {
                // this gets called on the UI thread
                this.UpdateThumbnailsLoadProgress((LoadImageProgressStatus)ea.UserState);
            };

            this.BackgroundWorker.RunWorkerCompleted += (o, ea) =>
            {
                this.BackgroundWorker.Dispose();
            };

            this.CancelUpdate();

            if (this.thumbnailsAlreadyInGrid.Count > 0)
            {
                // Navigation or Resizing. 
                // ThumbnailsIsAlreadyInGrid will only have thumbnails if it is a navigation or resize action. 
                // Because the cell size doesn't change during these actions, we can reuse existing thumbnails. 
                if (navigating)
                {
                    // Navigation. The grid layout / cell size doesn't change during navigation,
                    // so this method uses the grid as is while reusing any thumbnails it can
                    this.ReuseGrid(cellWidth, cellHeight, fileTableCount, fileTableStartIndex);
                }
                else
                {
                    // Resize. The grid layout changes, so it has to be reconstructed.  
                    // However, because the cell size is the same, we can reusue existing thumbnails 
                    if (this.RebuildButReuseGrid(gridWidth, gridHeight, cellWidth, cellHeight, fileTableCount, fileTableStartIndex) == false)
                    {
                        // We can't even fit a single cell into the grid, so abort
                        return false;
                    }
                }
            }
            else
            {
                // Zoom level change, 
                // The grid and /or cell size will have changed.
                // We have to rebuild the grid from scratch, including regenerating all thumbnails sized to fit into the grid
                if (this.RebuildGrid(gridWidth, gridHeight, cellWidth, cellHeight, fileTableCount, fileTableStartIndex) == false)
                {
                    // We can't even fit a single cell into the grid, so abort
                    return false;
                }
            }
            this.BackgroundWorker.RunWorkerAsync();
            return true;
        }
        #endregion

        #region Private GetThumbnailsAlreadyInGrid
        // Returns a list of reusable thumbnails that are already in the grid. 
        // Note that if the existing grid's cell height does not match the desired cell height, there will be no reusable thumbnails
        // as they have to be re-rendered to the new size anyways
        private List<ThumbnailInCell> GetThumbnailsAlreadyInGrid(double cellHeight, int fileTableCount)
        {
            List<ThumbnailInCell> thumbnailsAlreadyInGridList = new List<ThumbnailInCell>();
            int fileTableIndex = this.FileTableStartIndex;
            int cellsInGrid = this.AvailableColumns * this.AvailableRows;

            if (cellsInGrid <= 0 || Math.Abs(this.Grid.RowDefinitions[0].ActualHeight - cellHeight) > .0001)
            {
                // If The grid has nothing in it, or if the requested cell height isn't the same as the current one, return an empty list
                // i.e., assumes this is a change in zoom level, thus we don't reuse thumbnails as we have to resize them anyways
                return thumbnailsAlreadyInGridList;
            }

            // For each image row we want to display as a thumbnail, check if its thumbnail is already in the grid in a reusable form where:
            // - its path is already in the list
            // - the bitmap image is present (and thus renderable)
            for (int i = 0; i < cellsInGrid && fileTableIndex < fileTableCount; i++)
            {
                string path = Path.Combine(this.FileTable[fileTableIndex].RelativePath, this.FileTable[fileTableIndex].File);
                foreach (ThumbnailInCell tic in this.thumbnailInCells)
                {
                    if (String.Equals(tic.Path, path) && tic.Image.Source != null)
                    {
                        // a reusuable thumbnail exists, so add it to the list
                        thumbnailsAlreadyInGridList.Add(tic);
                        break;
                    }
                }
                fileTableIndex++;
            }
            return thumbnailsAlreadyInGridList;
        }
        #endregion

        #region Private: Reuse or Rebuild the grid
        // Rebuild the grid with a thumbnailInCell user control in each cell, but try to reuse thumbnails.
        // Note that this should be called only if the thumbnail size is known not to have changed (e.g., during navigation or resize events) 
        // Typically invoked for resizing actions.
        private bool RebuildButReuseGrid(double gridWidth, double gridHeight, double cellWidth, double cellHeight, int fileTableCount, int fileTableIndex)
        {
            if (this.thumbnailsAlreadyInGrid.Count == 0)
            {
                // Since there are no reusable thumbnails, we may as well rebuild the grid from scratch
                return RebuildGrid(gridWidth, gridHeight, cellWidth, cellHeight, fileTableCount, fileTableIndex);
            }
            // Calculated the number of rows/columns that can fit into the available space,
            int rowCount = Convert.ToInt32(Math.Floor(gridHeight / cellHeight));
            int columnCount = Convert.ToInt32(Math.Floor(gridWidth / cellWidth));
            if (rowCount == 0 || columnCount == 0)
            {
                // We can't even fit a single row or column in, so no point in continuing.
                return false; // rowsColumns;
            }

            // Clear the Grid so we can start afresh
            this.Grid.RowDefinitions.Clear();
            this.Grid.ColumnDefinitions.Clear();
            this.Grid.Children.Clear();

            // Add as many columns of the and rows of the given cell width and height as can fit into the grid's available space
            for (int currentColumn = 0; currentColumn < columnCount; currentColumn++)
            {
                this.Grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(cellWidth, GridUnitType.Pixel) });
            }
            for (int currentRow = 0; currentRow < rowCount; currentRow++)
            {
                this.Grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(cellHeight, GridUnitType.Pixel) });
            }

            int gridIndex = 0;
            this.thumbnailInCells = new List<ThumbnailInCell>();

            // Add an empty thumbnailInCell to each grid cell until no more cells or files to display. The bitmap will be added via the backgroundWorker.
            for (int currentRow = 0; currentRow < rowCount; currentRow++)
            {
                for (int currentColumn = 0; currentColumn < columnCount && fileTableIndex < fileTableCount; currentColumn++)
                {

                    // Check to see if the thumbnail already exists in a reusable form in the grid. 
                    string path = Path.Combine(this.FileTable[fileTableIndex].RelativePath, this.FileTable[fileTableIndex].File);
                    ThumbnailInCell tic = thumbnailsAlreadyInGrid.Find(x => String.Equals(x.Path, path));

                    if (tic == null || tic.Image.Source == null || this.Grid.Children.Contains(tic))
                    {
                        // 1. A reusable thumbnail isn't available, so create one
                        // 2. Or, to make it work for duplicates, we recreate the thumbnail if more than one of them is already in the grid.
                        // Otherwise, we will end up getting the first one (instead of the duplicates) and will try to add it. This will fail as its already a visual child.
                        // SAULXXX Note that I've tried to do this properly, where we could perhaps just clone the thumbnailInCell. But I didn't get it to work,
                        // likely because the code elsewhere just looks at the relative path/file to try to get it.
                        // It does deserve a revisit, e.g., something like (Code is in there, but commented out)
                        //  thumbnailInCell = (ThumbnailInCell) thumbnailInCell.CloneMe(fileTableIndex, gridIndex, cellWidth, cellHeight, currentRow, currentColumn);
                        tic = CreateEmptyThumbnail(fileTableIndex, gridIndex, cellWidth, cellHeight, currentRow, currentColumn);
                    }
                    else
                    {
                        // A reusable thumbnail is available. Reset its position in the grid
                        tic.GridIndex = gridIndex;
                        tic.Row = currentRow;
                        tic.Column = currentColumn;
                        tic.DateTimeLastBitmapWasSet = DateTime.Now;
                    }

                    Grid.SetRow(tic, currentRow);
                    Grid.SetColumn(tic, currentColumn);
                    this.Grid.Children.Add(tic);
                    this.thumbnailInCells.Add(tic);
                    fileTableIndex++;
                    gridIndex++;
                }
            }
            return true;
        }

        // Reuse the grid (including reusing existing thumbnails) as the grid nor cell size has  changed.
        // Typically invoked for navigation actions.
        private void ReuseGrid(double cellWidth, double cellHeight, int fileTableCount, int fileTableIndex)
        {
            if (this.thumbnailsAlreadyInGrid.Count > 0)
            {
                // We can reuse the grid as it hasn't changed
                this.Grid.Children.Clear();
                int gridIndex = 0;
                int rowCount = this.AvailableRows;
                int columnCount = this.AvailableColumns;
                this.thumbnailInCells = new List<ThumbnailInCell>();

                // Add an existing thumbnail to the grid.
                for (int currentRow = 0; currentRow < rowCount; currentRow++)
                {
                    for (int currentColumn = 0; currentColumn < columnCount && fileTableIndex < fileTableCount; currentColumn++)
                    {
                        // Check to see if the thumbnail already exists in a reusable form in the grid 
                        // and that it hasn't been deleted since the last refresh. 
                        string path = Path.Combine(this.FileTable[fileTableIndex].RelativePath, this.FileTable[fileTableIndex].File);
                        ThumbnailInCell thumbnailInCell = thumbnailsAlreadyInGrid.Find(x => String.Equals(x.Path, path));
                        // The Contains checks if the found thumbnail was a duplicate and thus already in the grid. If so we need to create a new thumbnail
                        // A reusable thumbnail isn't available, so create one
                        if (thumbnailInCell == null || thumbnailInCell.Image.Source == null || this.Grid.Children.Contains(thumbnailInCell))
                        {
                            // 1. A reusable thumbnail isn't available, so create one
                            // 2. Or, to make it work for duplicates, we recreate the thumbnail if more than one of them is already in the grid.
                            // Otherwise, we will end up getting the first one (instead of the duplicates) and will try to add it. This will fail as its already a visual child.
                            // SAULXXX Note that I've tried to do this properly, where we could perhaps just clone the thumbnailInCell. But I didn't get it to work,
                            // likely because the code elsewhere just looks at the relative path/file to try to get it.
                            // It does deserve a revisit, e.g., something like (Code is in there, but commented out)
                            //  thumbnailInCell = (ThumbnailInCell) thumbnailInCell.CloneMe(fileTableIndex, gridIndex, cellWidth, cellHeight, currentRow, currentColumn);
                            // See also RebuildButReuseGrid
                            thumbnailInCell = CreateEmptyThumbnail(fileTableIndex, gridIndex, cellWidth, cellHeight, currentRow, currentColumn);
                        }
                        else
                        {
                            // A reusable thumbnail is available. Reset its position in the grid
                            thumbnailInCell.GridIndex = gridIndex;
                            thumbnailInCell.Row = currentRow;
                            thumbnailInCell.Column = currentColumn;
                            thumbnailInCell.DateTimeLastBitmapWasSet = DateTime.Now;
                        }

                        Grid.SetRow(thumbnailInCell, currentRow);
                        Grid.SetColumn(thumbnailInCell, currentColumn);
                        thumbnailInCell.RefreshBoundingBoxesDuplicatesAndEpisodeInfo(this.FileTable, fileTableIndex);

                        this.Grid.Children.Add(thumbnailInCell);
                        this.thumbnailInCells.Add(thumbnailInCell);
                        fileTableIndex++;
                        gridIndex++;
                    }
                }
            }
        }

        // Rebuild the grid by clearing it, and then recreating it with an empty thumbnailInCell user control in each cell
        // Typically invoked for zooming in/out actions.
        private bool RebuildGrid(double gridWidth, double gridHeight, double cellWidth, double cellHeight, int fileTableCount, int fileTableIndex)
        {
            // Calculated the number of rows/columns that can fit into the available space,
            int rowCount;
            int columnCount;
            try
            {
                rowCount = Convert.ToInt32(Math.Floor(gridHeight / cellHeight));
                columnCount = Convert.ToInt32(Math.Floor(gridWidth / cellWidth));
            }
            catch
            {
                TracePrint.PrintMessage("In RebuildGrid: rowCount or columnCount cannot be converted to Int. Aborted");
                return false;
            }

            if (rowCount == 0 || columnCount == 0)
            {
                // We can't even fit a single row or column in, so no point in continuing.
                return false; // rowsColumns;
            }

            // Clear the Grid so we can start afresh
            this.Grid.RowDefinitions.Clear();
            this.Grid.ColumnDefinitions.Clear();
            this.Grid.Children.Clear();

            // Add as many columns of the and rows of the given cell width and height as can fit into the grid's available space
            for (int currentColumn = 0; currentColumn < columnCount; currentColumn++)
            {
                this.Grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(cellWidth, GridUnitType.Pixel) });
            }
            for (int currentRow = 0; currentRow < rowCount; currentRow++)
            {
                this.Grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(cellHeight, GridUnitType.Pixel) });
            }

            int gridIndex = 0;
            this.thumbnailInCells = new List<ThumbnailInCell>();

            // Add an empty thumbnailInCell to each grid cell until no more cells or files to display. The bitmap will be added via the backgroundWorker.
            for (int currentRow = 0; currentRow < rowCount; currentRow++)
            {
                for (int currentColumn = 0; currentColumn < columnCount && fileTableIndex < fileTableCount; currentColumn++)
                {
                    ThumbnailInCell thumbnailInCell = CreateEmptyThumbnail(fileTableIndex++, gridIndex++, cellWidth, cellHeight, currentRow, currentColumn);
                    Grid.SetRow(thumbnailInCell, currentRow);
                    Grid.SetColumn(thumbnailInCell, currentColumn);
                    this.Grid.Children.Add(thumbnailInCell);
                    this.thumbnailInCells.Add(thumbnailInCell);
                }
            }
            return true;
        }
        #endregion

        #region Progress and Cancelling 
        private void UpdateThumbnailsLoadProgress(LoadImageProgressStatus lip)
        {
            try
            {
                // As we are cancelling updates rapidly, check to make sure that we can still access the variables
                if (lip.ThumbnailInCell.GridIndex < this.thumbnailInCells.Count && lip.BitmapSource != null)
                {
                    ThumbnailInCell thumbnailInCell = this.thumbnailInCells[lip.GridIndex];
                    if (thumbnailInCell.DateTimeLastBitmapWasSet != lip.DateTimeLipInvoked)
                    {
                        // If the dates don't match, this means that the bitmap we are trying to set is stale 
                        // i.e., its the wrong one. As some async operations are slower than others, this could
                        // be an 'old' request trying to overwrite a newer request to set the bitmap
                        return;
                    }
                    thumbnailInCell.SetThumbnail(lip.BitmapSource);
                    thumbnailInCell.RefreshBoundingBoxesDuplicatesAndEpisodeInfo(this.FileTable, lip.FileTableIndex);
                }
            }
            catch
            {
                // Uncomment for tracing purposes
                // Debug.Print("UpdateThumbnailsLoadProgress Aborted | Catch");
            }
        }

        // Request cancellation of a pending BackgroundWorker action
        public void CancelUpdate()
        {
            try
            {
                if (this.BackgroundWorker != null)
                {
                    this.BackgroundWorker.CancelAsync();
                }
            }
            catch
            {
                TracePrint.CatchException("Catch is an acceptable.");
            }
        }
        #endregion

        #region CreateThumbnail
        private ThumbnailInCell CreateEmptyThumbnail(int fileTableIndex, int gridIndex, double cellWidth, double cellHeight, int row, int column)
        {
            ThumbnailInCell thumbnailInCell = new ThumbnailInCell(cellWidth, cellHeight)
            {
                GridIndex = gridIndex,
                Row = row,
                Column = column,
                RootFolder = this.FolderPath,
                ImageRow = this.FileTable[fileTableIndex],
                FileTableIndex = fileTableIndex,
                BoundingBoxes = GlobalReferences.MainWindow.GetBoundingBoxesForCurrentFile(this.FileTable[fileTableIndex].ID)
            };
            return thumbnailInCell;
        }
        #endregion

        #region Refresh Episode and Bounding Boxes (if they are turned on)
        public void RefreshEpisodeTextIfWarranted()
        {
            foreach (ThumbnailInCell thumbnailInCell in this.thumbnailInCells)
            {
                thumbnailInCell.RefreshEpisodeInfo(this.FileTable, thumbnailInCell.FileTableIndex);
            }
        }

        public void RefreshBoundingBoxesAndEpisodeInfo()
        {
            foreach (ThumbnailInCell thumbnailInCell in this.thumbnailInCells)
            {
                thumbnailInCell.RefreshBoundingBoxesDuplicatesAndEpisodeInfo(this.FileTable, thumbnailInCell.FileTableIndex);
            }
        }

        // ReSharper disable once UnusedMember.Global
        public void RefreshDuplicateTextIfWarranted()
        {
            foreach (ThumbnailInCell thumbnailInCell in this.thumbnailInCells)
            {
                thumbnailInCell.RefreshDuplicateInfo(this.FileTable, thumbnailInCell.FileTableIndex);
            }
        }
        #endregion

        #region Cell Navigation methods
        private bool GridGetNextSelectedCell(RowColumn cell, out RowColumn nextCell)
        {
            RowColumn lastCell = new RowColumn(this.Grid.RowDefinitions.Count - 1, this.Grid.ColumnDefinitions.Count - 1);
            while (this.GridGetNextCell(cell, lastCell, out nextCell))
            {
                ThumbnailInCell ci = this.GetThumbnailInCellFromCell(nextCell);

                // If there is no cell, we've reached the end, 
                if (ci == null)
                {
                    return false;
                }
                // We've found a selected cell
                if (ci.IsSelected)
                {
                    return true;
                }
                cell = nextCell;
            }
            return false;
        }

        private bool GridGetPreviousSelectedCell(RowColumn cell, out RowColumn previousCell)
        {
            RowColumn lastCell = new RowColumn(0, 0);
            while (this.GridGetPreviousCell(cell, lastCell, out previousCell))
            {
                ThumbnailInCell ci = this.GetThumbnailInCellFromCell(previousCell);

                // If there is no cell, terminate as we've reached the beginning
                if (ci == null)
                {
                    return false;
                }
                // We've found a selected cell
                if (ci.IsSelected)
                {
                    return true;
                }
                cell = previousCell;
            }
            return false;
        }
        // Get the next cell and return true
        // Return false if we hit the lastCell or the end of the grid.
        private bool GridGetNextCell(RowColumn cell, RowColumn lastCell, out RowColumn nextCell)
        {
            nextCell = new RowColumn(cell.X, cell.Y);
            // Try to go to the next column or wrap around to the next row if we are at the end of the row
            nextCell.Y++;
            if (nextCell.Y == this.Grid.ColumnDefinitions.Count)
            {
                // start a new row
                nextCell.Y = 0;
                nextCell.X++;
            }

            if (nextCell.X > lastCell.X || (nextCell.X == lastCell.X && nextCell.Y > lastCell.Y))
            {
                // We just went beyond the last cell, so we've reached the end.
                return false;
            }
            return true;
        }

        // Get the previous cell. Return true if we can, otherwise false.
        private bool GridGetPreviousCell(RowColumn cell, RowColumn firstCell, out RowColumn previousCell)
        {
            previousCell = new RowColumn(cell.X, cell.Y);
            // Try to go to the previous column or wrap around to the previous row if we are at the beginning of the row
            previousCell.Y--;
            if (previousCell.Y < 0)
            {
                // go to the previous row
                previousCell.Y = this.Grid.ColumnDefinitions.Count - 1;
                previousCell.X--;
            }

            if (previousCell.X < firstCell.X || (previousCell.X == firstCell.X && previousCell.Y < firstCell.Y))
            {
                // We just went beyond the last cell, so we've reached the end.
                return false;
            }
            return true;
        }
        #endregion

        #region Cell Calculation methods
        // Given two cells, determine which one is the start vs the end cell
        private static void DetermineTopLeftBottomRightCells(RowColumn cell1, RowColumn cell2, out RowColumn startCell, out RowColumn endCell)
        {
            startCell = (cell1.X < cell2.X || (cell1.X == cell2.X && cell1.Y <= cell2.Y)) ? cell1 : cell2;
            endCell = Equals(startCell, cell1) ? cell2 : cell1;
        }

        // Given a mouse point, return a point that indicates the (row, column) of the grid that the mouse point is over
        private RowColumn GetCellFromPoint(Point mousePoint)
        {
            RowColumn cell = new RowColumn(0, 0);
            double accumulatedHeight = 0.0;
            double accumulatedWidth = 0.0;

            // Calculate which row the mouse was over
            foreach (var rowDefinition in this.Grid.RowDefinitions)
            {
                accumulatedHeight += rowDefinition.ActualHeight;
                if (accumulatedHeight >= mousePoint.Y)
                {
                    break;
                }
                cell.X++;
            }

            // Calculate which column the mouse was over
            foreach (var columnDefinition in this.Grid.ColumnDefinitions)
            {
                accumulatedWidth += columnDefinition.ActualWidth;
                if (accumulatedWidth >= mousePoint.X)
                {
                    break;
                }
                cell.Y++;
            }
            return cell;
        }

        // Get the ThumbnailInCell held by the Grid's specified row,column coordinates 
        private ThumbnailInCell GetThumbnailInCellFromCell(RowColumn cell)
        {
            return this.Grid.Children.Cast<ThumbnailInCell>().FirstOrDefault(exp => Grid.GetColumn(exp) == cell.Y && Grid.GetRow(exp) == cell.X);
        }
        #endregion

        #region Enabling controls
        // Update the data entry controls to match the current selection(s)
        private void EnableOrDisableControlsAsNeeded()
        {
            if (this.Visibility == Visibility.Collapsed)
            {
                this.DataEntryControls.SetEnableState(ControlsEnableStateEnum.SingleImageView, -1);
            }
            else
            {
                this.DataEntryControls.SetEnableState(ControlsEnableStateEnum.MultipleImageView, this.SelectedCount());
            }
        }
        #endregion

        #region Events
        public event EventHandler<ThumbnailGridEventArgs> DoubleClick;
        public event EventHandler<ThumbnailGridEventArgs> SelectionChanged;

        protected virtual void OnDoubleClick(ThumbnailGridEventArgs e)
        {
            this.DoubleClick?.Invoke(this, e);
        }

        protected virtual void OnSelectionChanged(ThumbnailGridEventArgs e)
        {
            this.SelectionChanged?.Invoke(this, e);
        }
        #endregion
    }

    #region Class: LoadImageProgressStatus
    // Used by ReportProgress to pass specific values to Progress Changed as a parameter 
    internal class LoadImageProgressStatus
    {
        public ThumbnailInCell ThumbnailInCell { get; set; }
        public BitmapSource BitmapSource { get; set; }
        public int GridIndex { get; set; }
        public double CellWidth { get; set; }
        public int FileTableIndex { get; set; }
        public DateTime DateTimeLipInvoked { get; set; }
    }
    #endregion
}