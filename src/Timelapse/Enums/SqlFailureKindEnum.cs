namespace Timelapse.Enums
{
    // Distinguishes the wording used by TimelapseNeedsToShutDownAsSQLErrorDialog: a failed write
    // risks losing the last operation, while a failed read does not modify data but may mean
    // what's currently displayed is incomplete or inaccurate.
    public enum SqlFailureKindEnum
    {
        Write,
        Read
    }
}
