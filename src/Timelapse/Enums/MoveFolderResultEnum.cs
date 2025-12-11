namespace Timelapse.Enums
{
    public enum MoveFolderResultEnum
    {
        Success,
        FailAsSourceFolderDoesNotExist,
        FailAsDestinationFolderExists,
        FailDueToSystemMoveException
    }
}
