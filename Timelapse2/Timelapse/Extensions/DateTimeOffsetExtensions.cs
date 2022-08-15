using System;

namespace Timelapse.Util
{
    /// <summary>
    /// DateTimeOffset Extensions
    /// </summary>
    internal static class DateTimeOffsetExtensions
    {
        /// <summary>
        /// Return a DateTimeOffset from an unspecified dateTimeOffset
        /// </summary>
        public static DateTimeOffset SetOffset(this DateTimeOffset dateTime, TimeSpan offset)
        {
            return new DateTimeOffset(dateTime.DateTime.AsUnspecifed(), offset);
        }
    }
}
