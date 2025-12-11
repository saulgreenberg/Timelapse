namespace Timelapse.Enums
{
    public enum DatabaseFileErrorsEnum
    {
        Ok,
        OkButOpenedWithAnOlderTimelapseVersion,
        InvalidDatabase,
        PreVersion2300,
        IncompatibleVersionForMerging,
        IncompatibleVersion,
        UTCOffsetTypeExistsInUpgradedVersion,
        FileInSystemOrHiddenFolder,

        NotATimelapseFile,
        FileInRootDriveFolder,
        DoesNotExist,
        PathTooLong,
        Cancelled,

        // These results are used during merge testing for incompatabilities
        TemplateElementsDiffer,
        TemplateElementsSameButOrderDifferent,
        DetectionCategoriesIncompatible,
        ClassificationCategoriesIncompatible,
        MetadataLevelsDiffer,

    }
}
