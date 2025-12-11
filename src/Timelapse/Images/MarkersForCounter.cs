using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using Newtonsoft.Json;
using Timelapse.DebuggingSupport;

namespace Timelapse.Images
{
    // A class containing a list of Markers associated with a counter's data label 
    // Each marker represents the coordinates of an entity on the screen being counted
    public class MarkersForCounter(string dataLabel)
    {
        #region Public Properties
        // The counter's data label
        public string DataLabel { get; } = dataLabel;

        // the list of markers associated with the counter
        public List<Marker> Markers { get; } = [];

        #endregion

        #region Public Methods - Add / Remove markers
        // Add a Marker to the Marker's list
        public void AddMarker(Marker marker)
        {
            Markers.Add(marker);
        }

        // Create a marker with the given point and add it to the marker list
        public void AddMarker(Point point)
        {
            AddMarker(new Marker(DataLabel, point));
        }

        public void RemoveMarker(Marker marker)
        {
            int index = Markers.IndexOf(marker);
            if (index == -1)
            {
                TracePrint.PrintMessage("RemoveMarker: Expected marker to be present in list, but its not there.");
                return;
            }
            Markers.RemoveAt(index);
        }
        #endregion

        #region Public Methods - Get / Parse PointList
        public string GetPointList()
        {
            List<Point> pointList = [];
            foreach (Marker markerForCounter in Markers)
            {
                pointList.Add(new(Math.Round(markerForCounter.Position.X, 4), Math.Round(markerForCounter.Position.Y, 4)));
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
                    AddMarker(point);  // add the marker to the list;
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
