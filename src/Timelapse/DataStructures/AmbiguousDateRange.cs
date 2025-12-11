namespace Timelapse.DataStructures
{
    /// <summary>
    /// A class that stores various properties for each ambiguous date found,
    /// including indexes in the file table that define a range of ambiguous dates. 
    /// </summary>
    public class AmbiguousDateRange(int startRange, int endRange, int count, bool swapDates)
    {
        #region Public Properties
        // The start and end indeces in the file table that define a range of ambiguous dates
        public int StartIndex { get; set; } = startRange;
        public int EndIndex { get; set; } = endRange;

        // How many images are affected
        public int Count { get; set; } = count;

        // Whether those dates should be swapped
        public bool SwapDates { get; set; } = swapDates;

        #endregion
    }
}
