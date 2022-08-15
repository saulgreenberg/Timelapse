namespace Timelapse.Enums
{
    /// <summary>
    /// Return values for ThumbnailGrid Refresh invocations
    /// </summary>
    public enum ThumbnailGridRefreshStatus
    {
        Ok,
        Aborted,
        NotEnoughSpaceForEvenOneCell,
        AtMaximumZoomLevel,
        AtZeroZoomLevel
    }
}
