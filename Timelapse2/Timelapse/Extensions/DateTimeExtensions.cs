using System;

namespace Timelapse.Util
{
    /// <summary>
    /// DateTime Extensions
    /// </summary>
    internal static class DateTimeExtensions
    {
        /// <summary>
        /// Return a DateTime from an unspecified dateTime
        /// </summary>
        public static DateTime AsUnspecifed(this DateTime dateTime)
        {
            return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Millisecond, DateTimeKind.Unspecified);
        }
    }
}
