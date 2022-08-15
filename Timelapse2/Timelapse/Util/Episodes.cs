using System;
using System.Collections.Generic;
using Timelapse.Database;
using Timelapse.Enums;

namespace Timelapse
{
    /// <summary>
    /// Episodes is a static class that calculates and saves state information in various static data structures. Notabley, 
    // - ShowEpisodes is a boolean that detemines whether Episode information should be displayed
    // - EpisodeDictionary caches episode information by FileTable index, where that information is created on demand 
    /// </summary>

    public static class Episodes
    {
        #region Public Static Properties
        // A dictionary defining episodes across files in the file table.
        // An example dictionary beginning with an episode of 2 files then of 1 file would return, e.g., 
        // - 0,(1,2) (0th file, 1 out of 2 images in the episode) 
        // - 1,(2,2) (1st file, 2 out of 2 images in the episode) 
        // - 2,(1,1) (2nd file, 1 out of 1 images in the episode) etc
        public static Dictionary<int, Tuple<int, int>> EpisodesDictionary { get; set; }

        /// <summary>
        /// Whether or not Episodes should be displayed
        /// </summary>
        public static bool ShowEpisodes { get; set; }

        /// <summary>
        /// The Time threshold between successive images that determine whether they belong together in an episode
        /// </summary>
        public static TimeSpan TimeThreshold { get; set; } = TimeSpan.FromMinutes(Constant.EpisodeDefaults.TimeThresholdDefault);
        #endregion

        #region Public Methods - Reset
        /// <summary>
        /// Reset the EpisodesDictionary defining all episodes across all files in the file table. Note that it assumes :
        /// - files are sorted to give meaningful results, e.g., by date or by RelativePath/date if images are in different folders
        /// - if the file table is the result of a selection (i.e. as subset of all files), the episode definition is still meaningful
        /// </summary>
        public static void Reset()
        {
            Episodes.EpisodesDictionary = new Dictionary<int, Tuple<int, int>>();
            return;
        }
        #endregion

        #region Public Static - EpisodeGet
        /// <summary>
        /// Return the episodes for a given range within the file table
        /// </summary>
        /// <param name="fileTable"></param>
        /// <param name="fileTableIndex"></param>
        public static void EpisodeGetEpisodesInRange(FileTable fileTable, int fileTableIndex)
        {
            EpisodeGetEpisodesInRange(fileTable, fileTableIndex, Util.GlobalReferences.TimelapseState.EpisodeMaxRangeToSearch);
        }

        public static void EpisodeGetEpisodesInRange(FileTable fileTable, int fileTableIndex, int maxRangeToSearch)
        {
            if (Episodes.EpisodesDictionary == null)
            {
                Episodes.Reset();
            }
            int index = fileTableIndex;

            // Ensure the argument is valid
            if (fileTable == null || index < 0 || index >= fileTable.RowCount)
            {
                return;
            }

            bool inRange = Episodes.EpisodeGetAroundIndex(fileTable, fileTableIndex, maxRangeToSearch, out int first, out int count);

            // foreach fileindex within the episode, ranging from first to last, add its episode information to the episode dictionary
            for (int i = 1; i <= count; i++)
            {
                int currentFileIndex = first + i - 1;
                if (!Episodes.EpisodesDictionary.ContainsKey(currentFileIndex))
                {
                    Tuple<int, int> tuple = inRange ? new Tuple<int, int>(i, count) : new Tuple<int, int>(int.MaxValue, int.MaxValue);
                    Episodes.EpisodesDictionary.Add(currentFileIndex, tuple);
                }
            }
        }

        /// <summary>
        /// Depending on the direction, get - as an increment - the following.
        /// - if going forward, the number of files that we need to increment to get to the beginning of the next episode
        /// - if going backwards, the number of files that we need to decrement to get to 
        /// - a) the beginning of the current episode if we are not already on the 1st image or 
        /// - b) the previous episode if we are on the first image of the current episode.
        /// </summary>
        public static bool GetIncrementToNextEpisode(FileTable files, int index, DirectionEnum direction, out int increment)
        {
            increment = 1;
            if (files == null)
            {
                return false;
            }
            DateTime date1;
            DateTime date2;
            ImageRow file;
            int fileCount = files.RowCount;

            // Default in case there is only one file in this episode

            int first = index;
            int last = index;
            int current = index;
            // Note that numberOfFiles should never return zero if the provided index is valid
            if (files == null)
            {
                return false;
            }

            file = files[index];
            date1 = file.DateTime;

            // We want the next Episode in the forward direction
            if (direction == DirectionEnum.Next)
            {
                // go forwards in the filetable until we find the last file in the episode, or we fail
                // as we have gone forwards maxSearch times
                int maxSearch = Util.GlobalReferences.TimelapseState.EpisodeMaxRangeToSearch;
                while (current < fileCount && maxSearch != 0)
                {
                    file = files[current];
                    date2 = file.DateTime;
                    TimeSpan difference = date2 - date1;
                    bool aboveThreshold = difference.Duration() > Episodes.TimeThreshold;
                    if (aboveThreshold)
                    {
                        break;
                    }
                    date1 = date2;
                    last = current;
                    current++;
                    maxSearch--;
                }
                increment = last - first + 1;
                return !(maxSearch == 0);
            }

            // What is left is direction == DirectionEnum.Previous
            // If we are on the first image in the episode, we want the previous Episode in the backwards direction
            // Otherwise we want the first image in the episode (maybe??)
            int minSearch = Util.GlobalReferences.TimelapseState.EpisodeMaxRangeToSearch;
            current = index - 1;
            // Go backwards in the filetable until we find the first file in the episode, or we fail
            // as we have gone back minSearch times
            bool onFirstTwoImages = true;
            while (current >= 0 && minSearch != 0)
            {
                file = files[current];
                date2 = file.DateTime;
                TimeSpan difference = date1 - date2;
                bool aboveThreshold = difference.Duration() > Episodes.TimeThreshold;
                if (aboveThreshold && onFirstTwoImages == false)
                {
                    break;
                }
                onFirstTwoImages = false;
                first = current;
                date1 = date2;
                current--;
                minSearch--;
            }
            increment = last - first + 1;
            if (increment > 1)
            {
                increment--;
            }
            return !(minSearch == 0);
        }

        /// <summary>
        /// Given an index into the filetable, get the episode (defined by the first and last index) that the indexed file belongs to
        /// </summary>
        private static bool EpisodeGetAroundIndex(FileTable files, int index, int maxRangeToSearch, out int first, out int count)
        {
            DateTime date1;
            DateTime date2;
            ImageRow file;
            int fileCount = files.RowCount;
            // Default in case there is only one file in this episode
            first = index;
            int last = index;
            count = 1;

            // Note that numberOfFiles should never return zero if the provided index is valid
            if (files == null)
            {
                return false;
            }

            file = files[index];
            date1 = file.DateTime;

            int current = index - 1;
            int minSearch = maxRangeToSearch;
            int maxSearch = maxRangeToSearch;
            // Go backwards in the filetable until we find the first file in the episode, or we fail
            // as we have gone back minSearch times
            while (current >= 0 && minSearch != 0)
            {
                file = files[current];
                date2 = file.DateTime;
                TimeSpan difference = date1 - date2;
                bool aboveThreshold = difference.Duration() > Episodes.TimeThreshold;
                if (aboveThreshold)
                {
                    break;
                }
                first = current;
                date1 = date2;
                current--;
                minSearch--;
            }

            // Now go forwards in the filetable until we find the last file in the episode, or we fail
            // as we have gone forwards maxSearch times
            current = index + 1;
            file = files[index];
            date1 = file.DateTime;
            while (current < fileCount && maxSearch != 0)
            {
                file = files[current];
                date2 = file.DateTime;
                TimeSpan difference = date2 - date1;
                bool aboveThreshold = difference.Duration() > Episodes.TimeThreshold;
                if (aboveThreshold)
                {
                    break;
                }
                date1 = date2;
                last = current;
                current++;
                maxSearch--;
            }
            count = last - first + 1;
            return !(minSearch == 0 || maxSearch == 0);
        }
        #endregion
    }
}
