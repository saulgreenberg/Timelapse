namespace Timelapse.Enums
{
    public enum CreateSubfolderResultEnum
    {
        Success,
        FailAsSourceFolderDoesNotExist,
        FailAsDestinationFolderExists,
        FailDueToSystemCreateException
    }
}
