namespace Timelapse.Enums
{
    public enum DatabaseFileErrorsEnum
    {
        Ok,
        OkButOpenedWithAnOlderTimelapseVersion,
        InvalidDatabase,
        PreVersion2300,
        UTCOffsetTypeExistsInUpgradedVersion,
        FileInSystemOrHiddenFolder,

        NotATimelapseFile,
        FileInRootDriveFolder,
        DoesNotExist,
        PathTooLong,
        Cancelled,
        UnknownError,

        // These results are used during merge testing for incompatabilities
        TemplateElementsDiffer,
        TemplateElementsSameButOrderDifferent,
        DetectionCategoriesDiffer,
        ClassificationCategoriesDiffer,

    }
}
