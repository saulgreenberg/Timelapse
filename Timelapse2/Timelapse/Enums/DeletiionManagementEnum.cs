namespace Timelapse.Enums
{
    // Directs how the Delete Folder is managed by Timelapse
    // We set the numbers explicitly, as this number will be written in the Registry and shouldn't be affected if the Enum order is changed
    public enum DeleteFolderManagementEnum : int
    {
        ManualDelete = 0,
        AskToDeleteOnExit = 1,
        AutoDeleteOnExit = 2,
    }
}
