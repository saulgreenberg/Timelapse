﻿namespace Timelapse.DataStructures
{
    /// <summary>
    /// A class that stores various properties for each ambiguous date found,
    /// including indexes in the file table that define a range of ambiguous dates. 
    /// </summary>
    public class AmbiguousDateRange
    {
        #region Public Properties
        // The start and end indeces in the file table that define a range of ambiguous dates
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }

        // How many images are affected
        public int Count { get; set; }

        // Whether those dates should be swapped
        public bool SwapDates { get; set; }
        #endregion

        #region Constructor
        public AmbiguousDateRange(int startRange, int endRange, int count, bool swapDates)
        {
            StartIndex = startRange;
            EndIndex = endRange;
            SwapDates = swapDates;
            Count = count;
        }
        #endregion
    }
}
