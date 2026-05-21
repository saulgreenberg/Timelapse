namespace Timelapse.Enums
{
    public enum CSVReadingResult
    {
        Success,
        FileNotReadable,
        NoDataPresent,
        AbortedAsHeaderErrors,
        AbortedAsDataErrors,
        Cancelled
    }
}
