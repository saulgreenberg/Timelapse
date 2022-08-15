namespace Timelapse.Enums
{
    public enum DatabaseFileErrorsEnum
    {
        Ok,
        InvalidDatabase,
        PreVersion2300,
        FileInSystemOrHiddenFolder,

        NotATimelapseFile,
        FileInRootDriveFolder,
        DoesNotExist,
        PathTooLong,

        // These results are used during merge testing for incompatabilities
        TemplateElementsDiffer,
        TemplateElementsSameButOrderDifferent,
        DetectionCategoriesDiffers,
        ClassificationDictionaryDiffers,

    }
}
