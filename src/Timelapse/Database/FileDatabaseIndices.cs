using System;
using System.Collections.Generic;
using Timelapse.Constant;

namespace Timelapse.Database
{
    public partial class FileDatabase
    {
        // Performance note.
        // The time to check if an index exists is very brief (Less than a millisecond on my computer)
        #region Indexes to rapidly access File/RelativePath queries

        // Create indexes to rapidly access File/RelativePath queries:
        // -IndexRelativePath
        // -IndexFile
        // -IndexRelativePathFile
        // -IndexRelativePathDateTimeFile
        public void IndexCreateForFileAndRelativePathIfNeeded()
        {
            List<Tuple<string, string, string>> tuples =
            [
                new(DatabaseValues.IndexRelativePath, DBTables.FileData, DatabaseColumn.RelativePath),
                new(DatabaseValues.IndexFile, DBTables.FileData, DatabaseColumn.File),
                new(DatabaseValues.IndexRelativePathFile, DBTables.FileData, DatabaseColumn.RelativePath + "," + DatabaseColumn.File),
                new(DatabaseValues.IndexRelativePathDateTimeFile, DBTables.FileData, 
                   $"{DatabaseColumn.RelativePath}, {DatabaseColumn.DateTime}, {DatabaseColumn.File}")
            ];
            Database.IndexCreateMultipleIfNotExists(tuples);
        }

        #endregion

        #region Create indexes to rapidly access Detection-Related queries
        public void IndexCreateForDetectionsIfNeeded()
        {
            IndexCreateForDetectionsIfNeeded(Database);
        }

        public static void IndexCreateForDetectionsIfNeeded(SQLiteWrapper database)
        {
            List<Tuple<string, string, string>> tuples =
            [
                new(DatabaseValues.IndexDetectionID, DBTables.Detections, DatabaseColumn.ID),
                new(DatabaseValues.IndexDetectionVideoID, DBTables.DetectionsVideo, DetectionColumns.DetectionID),
                new(DatabaseValues.IndexDetectionsClassificationConfidence, DBTables.Detections, 
                    $"{DetectionColumns.Classification}, {DetectionColumns.Conf}, {DetectionColumns.ClassificationConf}, {DetectionColumns.ImageID}"),
            ];
            database.IndexCreateMultipleIfNotExists(tuples);
        }
        #endregion

        // Create index to rapidly access Episode combined with Detection-related queries
        // Without it, a custom select using recognitions that includes all images in an episode is extremely slow
        // TODO: the episode data label can be changed by the user, so we should consider how to handle that. We could either:
        // Find an drop the index when the episode data label is changed,
        // or we could create the index with a generic name and then update the index to use the new episode data label when it is changed.
        // For now, we'll just assume that the episode data label is not changed after it is set.
        public void IndexCreateForEpisodeFieldIfNeeded(string episodeDataLabel)
        {
            Database.IndexCreateIfNotExists(DatabaseValues.IndexEpisodeField, DBTables.FileData, episodeDataLabel);
        }
    }
}
