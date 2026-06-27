using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media;
using Timelapse.Constant;
using Timelapse.ControlsDataEntry;
using Timelapse.DebuggingSupport;
using Timelapse.EventArguments;
using Timelapse.Images;

// ReSharper disable once CheckNamespace
namespace Timelapse
{
    // Marking and Counting
    public partial class TimelapseWindow
    {
        #region Event Handler
        // Event handler: A marker, as defined in e.Marker, has been either added (if e.IsNew is true) or deleted (if it is false)
        // Depending on which it is, add or delete the tag from the current counter control's list of tags 
        // If its deleted, remove the tag from the current counter control's list of tags
        // Every addition / deletion requires us to:
        // - update the contents of the counter control 
        // - update the data held by the image
        // - update the list of markers held by that counter
        // - regenerate the list of markers used by the markableCanvas
        private void MarkableCanvas_RaiseMarkerEvent(object sender, MarkerEventArgs e)
        {
            if (DataHandler.ImageCache.Current == null)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(DataHandler.ImageCache.Current));
                return;
            }
            if (e.IsNew)
            {
                // A marker has been added
                DataEntryCounter currentCounter = FindSelectedCounter(); // No counters are selected, so don't mark anything
                if (currentCounter == null)
                {
                    return;
                }
                MarkableCanvas_AddMarker(currentCounter, e.Marker);
                return;
            }
            // An existing marker has been deleted.
            DataEntryCounter counter = (DataEntryCounter)DataEntryControls.ControlsByDataLabelThatAreVisible[e.Marker.DataLabel];

            // Part 1. Decrement the counter only if there is a number in it
            string oldCounterData = counter.Content;
            if (!string.IsNullOrEmpty(oldCounterData))
            {
                int count = Convert.ToInt32(oldCounterData);
                count = (count == 0) ? 0 : count - 1;           // Make sure its never negative, which could happen if a person manually enters the count 
                string newCounterData = count.ToString();

                if (!newCounterData.Equals(oldCounterData))
                {
                    // Don't bother updating if the value hasn't changed (i.e., already at a 0 count)
                    // Update the datatable and database with the new counter values
                    DataHandler.IsProgrammaticControlUpdate = true;
                    counter.SetContentAndTooltip(newCounterData);
                    DataHandler.IsProgrammaticControlUpdate = false;
                    DataHandler.FileDatabase.UpdateFile(DataHandler.ImageCache.Current.ID, counter.DataLabel, newCounterData);
                }
            }

            // Part 2. Remove the marker in memory and from the database
            // Each marker in the countercoords list reperesents a different control. 
            // So just check the first markers's DataLabel in each markersForCounters list to see if it matches the counter's datalabel.
            MarkersForCounter markersForCounter = null;
            foreach (MarkersForCounter markers in markersOnCurrentFile)
            {
                // If there are no markers, we don't have to do anything.
                if (markers.Markers.Count == 0)
                {
                    continue;
                }

                if (markers.Markers[0].DataLabel == e.Marker.DataLabel)
                {
                    // We found the marker counter associated with that control
                    markersForCounter = markers;
                    break;
                }
            }

            // Part 3. Remove the found metatag from the metatagcounter and from the database
            if (markersForCounter != null)
            {
                markersForCounter.RemoveMarker(e.Marker);
                if (0 == markersOnCurrentFile.Count(x => x.Markers.Count > 0))
                {
                    DataHandler.FileDatabase.MarkersRemoveMarkerRow(DataHandler.ImageCache.Current.ID);
                }
                else
                {
                    DataHandler.FileDatabase.MarkersUpdateMarkerRow(DataHandler.ImageCache.Current.ID, markersForCounter);
                }
            }
            MarkableCanvas_UpdateMarkers(); // Refresh the Markable Canvas, where it will also delete the markers at the same time
        }
        #endregion

        #region AddMarker
        /// <summary>
        /// A new marker associated with a counter control has been created;
        /// Increment the counter controls value, and add the marker to all data structures (including the database)
        /// </summary>
        private void MarkableCanvas_AddMarker(DataEntryCounter counter, Marker marker)
        {
            if (DataHandler.ImageCache.Current == null)
            {
                // Shouldn't happen
                TracePrint.NullException(nameof(DataHandler.ImageCache.Current));
                return;
            }

            try // Make this a noop to handle the rare bug 'Object reference not set to an instance of an object.' as not sure which object was the problematic one
            {
                if (counter == null || marker == null)
                {
                    // This shouldn't happen, but a user reported a 'null' crash somewhere in this method, so just in case...
                    Debug.Print("In MarkableCanvas_AddMarker. Counter or marker is null (and it shouldn't be");
                    return;
                }

                // Get the Counter Control's contents,  increment its value (as we have added a new marker) 
                // Then update the control's content as well as the database
                // If we can't convert it to an int, assume that someone set the default value to either a non-integer in the template, or that it's a space. In either case, revert it to zero.
                // If we can't convert it to an int, assume that someone set the default value to either a non-integer in the template, or that it's a space. In either case, revert it to zero.
                if (Int32.TryParse(counter.Content, out int count) == false)
                {
                    count = 0;
                }
                ++count;

                string counterContent = count.ToString();
                DataHandler.IsProgrammaticControlUpdate = true;
                DataHandler.FileDatabase.UpdateFile(DataHandler.ImageCache.Current.ID, counter.DataLabel, counterContent);
                counter.SetContentAndTooltip(counterContent);
                DataHandler.IsProgrammaticControlUpdate = false;

                // Find the MarkersForCounters associated with this particular control so we can add a marker to it
                MarkersForCounter markersForCounter = null;

                // Insert markers into the MarkersTable if all markers are empty,
                // which should only occur if the current file has no markers associated with it.
                if (0 == markersOnCurrentFile.Count(e => e.Markers.Count > 0))
                {
                    // As there is no row in the marker table with that ID, an empty row (with [] values) will be added to the database
                    // The Markers list held by the database will be updated accordingly after this IF section
                    // PERFORMANCE: The call below is inefficient, as it re-reads the entire Markers table from the data table.
                    // This is necessary to update the markers table, as otherwise it will not contain a row with the current Id
                    // But given that the marker table should be relatively small, it shouldn't be too costly.
                    if (DataHandler.FileDatabase.MarkersTryInsertNewMarkerRow(DataHandler.ImageCache.Current.ID))
                    {
                        // We added a new marker row, so we need to update the various markers data structures to reflect the new marker
                        // markersForCounter = new MarkersForCounter(counter.DataLabel);
                        markersOnCurrentFile = DataHandler.FileDatabase.MarkersGetMarkersForCurrentFile(DataHandler.ImageCache.Current.ID);
                    }
                }

                // Find the markers for this Counter
                foreach (MarkersForCounter markers in markersOnCurrentFile)
                {
                    if (markers.DataLabel == counter.DataLabel)
                    {
                        markersForCounter = markers;
                        break;
                    }
                }

                if (markersForCounter == null)
                {
                    // Shouldn't happen
                    TracePrint.NullException(nameof(markersForCounter));
                    return;
                }

                // fill in marker information
                marker.ShowLabel = true; // Show the annotation as its created. We will clear it on the next refresh
                marker.LabelShownPreviously = false;
                marker.Brush = Brushes.Red;               // Make it Red (for now)
                marker.DataLabel = counter.DataLabel;
                marker.Tooltip = counter.Label;   // The tooltip will be the counter label plus its data label
                marker.Tooltip += "\n" + counter.DataLabel;
                markersForCounter.AddMarker(marker);

                // update this counter's list of points in the database
                DataHandler.FileDatabase.MarkersUpdateMarkerRow(DataHandler.ImageCache.Current.ID, markersForCounter);
                MarkableCanvas.Markers = GetDisplayMarkers();
            }
            catch
            {
                TracePrint.CatchException("In MarkableCanvas_AddMarker / Catch. Converted to a no-op due to problem.");
            }
        }
        #endregion

        #region Update Markers, GetDisplayMarkers
        // Create a list of markers from those stored in each image's counters, 
        // and then set the markableCanvas's list of markers to that list. We also reset the emphasis for those tags as needed.
        private void MarkableCanvas_UpdateMarkers()
        {
            MarkableCanvas.Markers = GetDisplayMarkers(); // By default, we don't show the annotation
        }

        private List<Marker> GetDisplayMarkers()
        {
            // No markers?
            if (markersOnCurrentFile == null)
            {
                return null;
            }

            // The markable canvas uses a simple list of markers to decide what to do.
            // So we just create that list here, where we also reset the emphasis of some of the markers
            List<Marker> markers = [];
            DataEntryCounter selectedCounter = FindSelectedCounter();
            int markersOnCurrentFileCount = markersOnCurrentFile.Count;
            for (int counter = 0; counter < markersOnCurrentFileCount; counter++)
            {
                MarkersForCounter markersForCounter = markersOnCurrentFile[counter];
                if (DataEntryControls.ControlsByDataLabelThatAreVisible.TryGetValue(markersForCounter.DataLabel, out _) == false)
                {
                    // If we can't find the counter, its likely because the control was made invisible in the template,
                    // which means that there is no control associated with the marker. So just don't create the 
                    // markers associated with this control. Note that if the control is later made visible in the template,
                    // the markers will then be shown. 
                    continue;
                }

                // Update the emphasise for each tag to reflect how the user is interacting with tags
                DataEntryCounter currentCounter = (DataEntryCounter)DataEntryControls.ControlsByDataLabelThatAreVisible[markersForCounter.DataLabel];
                bool emphasize = markersForCounter.DataLabel == State.MouseOverCounter;
                foreach (Marker marker in markersForCounter.Markers)
                {
                    // the first time through, show an annotation. Otherwise we clear the flags to hide the annotation.
                    if (marker.ShowLabel && !marker.LabelShownPreviously)
                    {
                        marker.ShowLabel = true;
                        marker.LabelShownPreviously = true;
                    }
                    else
                    {
                        marker.ShowLabel = false;
                    }

                    if (selectedCounter != null && currentCounter.DataLabel == selectedCounter.DataLabel)
                    {
                        marker.Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(Defaults.SelectionColour);
                    }
                    else
                    {
                        marker.Brush = (SolidColorBrush)new BrushConverter().ConvertFromString(Defaults.StandardColour);
                    }

                    marker.Emphasise = emphasize;
                    marker.Tooltip = currentCounter.Label;
                    markers.Add(marker); // Add the MetaTag in the list 
                }
            }
            return markers;
        }
        #endregion
    }
}
