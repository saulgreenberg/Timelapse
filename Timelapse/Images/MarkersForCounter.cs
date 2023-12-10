using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using Timelapse.DebuggingSupport;

namespace Timelapse.Images
{
    // A class containing a list of Markers associated with a counter's data label 
    // Each marker represents the coordinates of an entity on the screen being counted
    public class MarkersForCounter
    {
        #region Public Properties
        // The counter's data label
        public string DataLabel { get; }

        // the list of markers associated with the counter
        public List<Marker> Markers { get; }
        #endregion

        #region Constructor
        public MarkersForCounter(string dataLabel)
        {
            this.DataLabel = dataLabel;
            this.Markers = new List<Marker>();
        }
        #endregion

        #region Public Methods - Add / Remove markers
        // Add a Marker to the Marker's list
        public void AddMarker(Marker marker)
        {
            this.Markers.Add(marker);
        }

        // Create a marker with the given point and add it to the marker list
        public void AddMarker(Point point)
        {
            this.AddMarker(new Marker(this.DataLabel, point));
        }

        public void RemoveMarker(Marker marker)
        {
            int index = this.Markers.IndexOf(marker);
            if (index == -1)
            {
                TracePrint.PrintMessage("RemoveMarker: Expected marker to be present in list, but its not there.");
                return;
            }
            this.Markers.RemoveAt(index);
        }
        #endregion

        #region Public Methods - Get / Parse PointList
        public string GetPointList()
        {
            List<Point> pointList = new List<Point>();
            foreach (Marker markerForCounter in this.Markers)
            {
                pointList.Add(new Point(Math.Round(markerForCounter.Position.X, 4), Math.Round(markerForCounter.Position.Y, 4)));
            }
            return JsonConvert.SerializeObject(pointList);
        }

        public void ParsePointList(string pointList)
        {
            if (string.IsNullOrEmpty(pointList))
            {
                return;
            }
            try
            {
                List<Point> points = JsonConvert.DeserializeObject<List<Point>>(pointList);
                foreach (Point point in points)
                {
                    this.AddMarker(point);  // add the marker to the list;
                }
            }
            catch
            {
                // Just in case there is a weird format in the point list.
                // essentially it will add points to the list until the first parsing failure
                Debug.Print("Parsing issue in MarkersForCounter.ParsePointList");
            }
        }
        #endregion
    }
}
