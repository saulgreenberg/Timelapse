using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Timelapse.Constant;
using Timelapse.Controls;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.SearchingAndSorting;

namespace Timelapse.Database
{
    partial class FileDatabase
    {
        // CODECLEANUP:  should probably merge all 'special cases' of selection (e.g., detections, etc.) into a single class so they are treated the same way, 
        // eg., to simplify CountAllFilesMatchingSelectionCondition vs SelectFilesAsync
        // PERFORMANCE can be a slow query on very large databases. Could check for better SQL expressions or database design, but need an SQL expert for that

        #region Counts or Exists 1 of matching files
        // Return a total count of the currently selected files in the file table.
        public int CountAllCurrentlySelectedFiles => FileTable?.RowCount ?? 0;

        // Return the count of the files matching the fileSelection condition in the entire database
        // Form examples
        // - Select Count(*) FROM (Select * From Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id WHERE <some condition> GROUP BY Detections.Id HAVING  MAX  ( Detections.conf )  <= 0.9)
        // - Select Count(*) FROM (Select * From Classifications INNER JOIN DataTable ON DataTable.Id = Detections.Id  INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID WHERE DataTable.Person<>'true' 
        // AND Classifications.category = 6 GROUP BY Classifications.classificationID HAVING  MAX  (Classifications.conf ) BETWEEN 0.8 AND 1 
        public int CountAllFilesMatchingSelectionCondition(FileSelectionEnum fileSelection)
        {
            string query;
            bool detectionsAndEpisodeShowAllHandled = false;

            if (GlobalReferences.DetectionsExists
                     && CustomSelection.ShowMissingDetections == false
                     && CustomSelection.RecognitionSelections.UseRecognition
                     && CustomSelection.RecognitionSelections.RecognitionType is RecognitionType.Detection or RecognitionType.Classification
                     && CustomSelection.RecognitionSelections.RankByDetectionConfidence)
            {
                // Special case limited query: Rank by detection confidence
                // does not work with missing detections, random selection, or episode show all
                string whereExpression = CustomSelection.GetFilesWhere(false, false, true);

                if (CustomSelection.RecognitionSelections.RecognitionType is RecognitionType.Detection)
                {
                    // Detection selection by detection conf order 
                    string detectionCategoryNumber = this.CustomSelection.RecognitionSelections.DetectionCategoryNumber;
                    if (int.TryParse(detectionCategoryNumber, out int number) && number != 0)
                    {
                        query = SqlGetAllRecognitionsSortedBy.ByDetectionConfidence(whereExpression, detectionCategoryNumber, true);
                    }
                    else
                    {
                        // 0 is the empty category, Since we are looking at All detection regardless of its confidence, there will be 0 matches.
                        return 0;
                    }
                }
                else
                {
                    // Classification selection by detection conf order 
                    string classificationCategoryNumber = this.CustomSelection.RecognitionSelections.ClassificationCategoryNumber;
                    if (int.TryParse(classificationCategoryNumber, out _) )
                    {
                        query = SqlGetAllRecognitionsSortedBy.ByClassificationConfidence(whereExpression, classificationCategoryNumber, true);
                    }
                    else
                    {
                        // We should always get a match, so this shouldn't happen
                        return 0;
                    }
                }
                return DoGetCountFromSelect(query);
            }

            // PART 1 of Query
            if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && CustomSelection.ShowMissingDetections)
            {
                // MISSING DETECTIONS
                // Create a query that returns a count of missing detections
                // Form: SELECT COUNT ( DataTable.Id ) FROM DataTable LEFT JOIN Detections ON DataTable.ID = Detections.Id WHERE Detections.Id IS NULL 
                query = SqlPhrase.SelectMissingDetections(SelectTypesEnum.Count);
            }

            else if (CustomSelection.EpisodeShowAllIfAnyMatch
                     && CustomSelection.EpisodeNoteField != string.Empty
                     && fileSelection == FileSelectionEnum.Custom
                     && GlobalReferences.DetectionsExists
                     && CustomSelection.RecognitionSelections.UseRecognition)
            {
                // EpisodeShowAll and Detections are selected (special case query)
                string episodeFieldName = CustomSelection.EpisodeNoteField;
                detectionsAndEpisodeShowAllHandled = true;

                // Common query fragment for Count and Select
                query = ComposeQueryFragmentForDetectionsAndEpisodeShowAll(CustomSelection, episodeFieldName, true);
            }

            else if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && CustomSelection.RecognitionSelections.UseRecognition && CustomSelection.RecognitionSelections.RecognitionType == RecognitionType.Detection)
            {
                // DETECTIONS 
                // Create a query that returns a count of detections matching some conditions (which can include classifications within a detection)
                // Form: SELECT COUNT  ( * )  FROM  (  SELECT * FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id
                query = SqlPhrase.SelectDetections(SelectTypesEnum.Count);
            }

            else if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && CustomSelection.RecognitionSelections.UseRecognition && CustomSelection.RecognitionSelections.RecognitionType == RecognitionType.Classification)
            {
                // CLASSIFICATIONS
                // Same form as Detections but with error checks
                if (null == this.detectionCategoriesDictionary)
                {
                    // Error
                    return -1;
                }
                query = SqlPhrase.SelectDetections(SelectTypesEnum.Count);
            }
            else
            {
                // STANDARD (NO DETECTIONS/CLASSIFICATIONS)
                // Create a query that returns a count that does not consider detections
                query = Sql.SelectCountStarFrom + DBTables.FileData;
            }

            if (detectionsAndEpisodeShowAllHandled == false)
            {
                // PART 2 of Query
                // Now add the Where conditions to the query.
                // If the selection is All, there is no where clause needed.
                if (fileSelection != FileSelectionEnum.All)
                {
                    if (GlobalReferences.DetectionsExists)
                    {
                        if (CustomSelection.ShowMissingDetections == false)
                        {
                            string where = CustomSelection.GetFilesWhere(); //this.GetFilesConditionalExpression(fileSelection);
                            if (!string.IsNullOrEmpty(where))
                            {
                                query += where;
                            }

                            if (fileSelection == FileSelectionEnum.Custom &&
                                CustomSelection.RecognitionSelections.UseRecognition) // && CustomSelection.RecognitionSelections.RecognitionType != RecognitionType.Empty)
                            {
                                // Add a close parenthesis if we are querying for detections
                                query += Sql.CloseParenthesis;
                            }
                        }
                        else
                        {
                            // Show missing recognitions is selected: the where clause should only include the data fields (i.e., no recognition conditions), if any
                            string where = CustomSelection.GetFilesWhere(true, true);
                            if (!string.IsNullOrEmpty(where))
                            {
                                query += $"{Sql.And} {where}";
                            }
                        }
                    }
                    else
                    {
                        if (GlobalReferences.DetectionsExists == false || CustomSelection.ShowMissingDetections == false)
                        {
                            // Standard where 
                            string whereExpression = CustomSelection.GetFilesWhere();
                            if (string.IsNullOrEmpty(whereExpression) == false)
                            {
                                query += whereExpression;
                            }
                        }
                    }
                }

                // EPISODES-related addition to query. (ignored when ShowMissingDetections is selected)
                // If Episodes  is turned on, then the Episode Note field contains values in the Episode format (e.g.) 25:1/8.
                // We construct a wrapper for counting  files where all files in an episode have at least one file matching the surrounded search condition
                if (false == CustomSelection.ShowMissingDetections
                    && CustomSelection.EpisodeShowAllIfAnyMatch
                    && CustomSelection.EpisodeNoteField != string.Empty
                    && fileSelection == FileSelectionEnum.Custom)
                {
                    // Adjust the front of the string
                    query = query.Replace(Sql.SelectCountStarFrom, string.Empty);

                    string frontWrapper = SqlPhrase.CountOrSelectFilesInEpisodeIfOneFileMatchesFrontWrapper(DBTables.FileData, CustomSelection.EpisodeNoteField, true);
                    string backWrapper = Sql.CloseParenthesis;
                    query = frontWrapper + query + backWrapper;
                }
            }

            return DoGetCountFromSelect(query);
        }
        #endregion

        // Actually load the file table with the results of the query.
        // We do this in a separate method so that we can call it from other methods that need to select files with specialized queries (e.g., sort by detection confidence)
        private int DoGetCountFromSelect(string query)
        {
            // Uncommment this to see the complete query
            // System.Diagnostics.Debug.Print("File Counts: " + query);
            return Database.ScalarGetScalarFromSelectAsInt(query);
        }

        #region Exists 1 of matching files

        // Return true if even one file matches the fileSelection condition in the entire database
        // NOTE: Currently only used by 1 method to check if deleteflags exists. Check how well this works if other methods start using it.
        // NOTE: This method is somewhat similar to CountAllFilesMatchingSelectionCondition. They could be combined, but its easier for now to keep them separate
        // Form examples
        // -  No detections:  SELECT EXISTS (  SELECT 1  FROM DataTable WHERE  ( DeleteFlag='true' )  )  //
        // -  detections:     SELECT EXISTS (  SELECT 1  FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id WHERE  ( DataTable.DeleteFlag='true' )  GROUP BY Detections.Id HAVING  MAX  ( Detections.conf )  BETWEEN 0.8 AND 1 )
        // -  recognitions:   SELECT EXISTS (  SELECT 1  FROM  (  SELECT DISTINCT DataTable.* FROM Classifications INNER JOIN DataTable ON DataTable.Id = Detections.Id INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 
        //                    WHERE  ( DataTable.DeleteFlag='true' )  AND Classifications.category = 1 GROUP BY Classifications.classificationID HAVING  MAX  ( Classifications.conf )  BETWEEN 0.8 AND 1 )  ) :1
        public bool ExistsFilesMatchingSelectionCondition(FileSelectionEnum fileSelection)
        {
            bool skipWhere = false;
            string query = " SELECT EXISTS ( ";

            // PART 1 of Query
            if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && CustomSelection.ShowMissingDetections)
            {
                // MISSING DETECTIONS
                // Create a query that returns a count of missing detections
                // Form: SELECT COUNT ( DataTable.Id ) FROM DataTable LEFT JOIN Detections ON DataTable.ID = Detections.Id WHERE Detections.Id IS NULL 
                query += SqlPhrase.SelectMissingDetections(SelectTypesEnum.One);
                skipWhere = true;
            }
            else if (fileSelection == FileSelectionEnum.Custom && GlobalReferences.DetectionsExists && CustomSelection.RecognitionSelections.UseRecognition
                     && (CustomSelection.RecognitionSelections.RecognitionType == RecognitionType.Detection || CustomSelection.RecognitionSelections.RecognitionType == RecognitionType.Classification))
            {
                // DETECTIONS AND CLASSIFICATIONS
                // Create a query that returns a count of detections matching some conditions
                // Form: SELECT COUNT  ( * )  FROM  (  SELECT * FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id
                query += SqlPhrase.SelectDetections(SelectTypesEnum.One);
            }
            else
            {
                // STANDARD (NO DETECTIONS/CLASSIFICATIONS)
                // Create a query that returns a count that does not consider detections
                query += Sql.SelectOne + Sql.From + DBTables.FileData;
            }

            // PART 2 of Query
            // Now add the Where conditions to the query
            if ((GlobalReferences.DetectionsExists && CustomSelection.ShowMissingDetections == false) || skipWhere == false)
            {
                string where = CustomSelection.GetFilesWhere();
                if (!string.IsNullOrEmpty(where))
                {
                    query += where;
                }
            }
            query += Sql.CloseParenthesis;

            return Database.ScalarGetScalarFromSelectAsInt(query) != 0;
        }
        #endregion

        #region Select General version - Select Files in the file table
        /// <summary> 
        /// Rebuild the file table with all files in the database table which match the specified selection.
        /// </summary>
        public async Task SelectFilesAsync(FileSelectionEnum selection)
        {
            string query = string.Empty;
            bool detectionsAndEpisodeShowAllHandled = false;
            GlobalReferences.MainWindow.ImageDogear = new(GlobalReferences.MainWindow.DataHandler);
            this.ResetAfterPossibleRelativePathChanges();

            if (CustomSelection == null)
            {
                // If no custom selections are configure, then just use a standard query
                query += $"{Sql.SelectStarFrom} {DBTables.FileData}";
            }

            else if (GlobalReferences.DetectionsExists
                     && CustomSelection.ShowMissingDetections == false
                     && CustomSelection.RecognitionSelections.UseRecognition
                     && CustomSelection.RecognitionSelections.RecognitionType is RecognitionType.Detection or RecognitionType.Classification
                     && CustomSelection.RecognitionSelections.RankByDetectionConfidence)
            {
                // Special case limited query: Rank by detection confidence
                // does not work with missing detections, random selection, or episode show all
                string whereExpression = CustomSelection.GetFilesWhere(false, false, true);


                if (CustomSelection.RecognitionSelections.RecognitionType is RecognitionType.Detection)
                {
                    // Detection selection by detection order
                    string detectionCategoryNumber = this.CustomSelection.RecognitionSelections.DetectionCategoryNumber;
                    if (int.TryParse(detectionCategoryNumber, out int number) && number != 0)
                    {
                        query = SqlGetAllRecognitionsSortedBy.ByDetectionConfidence(whereExpression, detectionCategoryNumber, false);
                    }
                    else
                    {
                        // Should not happen
                        // As 0 is the empty category, previous counting should have returned 0 matches, which should have disabled selection.
                        // but just in case, we ignore the query.
                        // So we return an empty table with the same schema as the normal query would return, but with no rows.
                        query = $"{Sql.SelectStarFrom} {DBTables.FileData}{Sql.Where} 1 = 0";
                    }
                }
                else
                {
                    // Classification selection: Create query for detection 
                    string classificationCategoryNumber = this.CustomSelection.RecognitionSelections.ClassificationCategoryNumber;
                    query = int.TryParse(classificationCategoryNumber, out _) 
                        ? SqlGetAllRecognitionsSortedBy.ByClassificationConfidence(whereExpression, classificationCategoryNumber, false) 
                        : // We should always get a match, so this shouldn't happen. 
                          // So we return an empty table with the same schema as the normal query would return, but with no rows.
                          $"{Sql.SelectStarFrom} {DBTables.FileData}{Sql.Where} 1 = 0";
                }

                await DoFileTableGetDataTableFromSelect(query);
                return;
            }

            else
            {
                if (CustomSelection.RandomSample > 0)
                {
                    // Select * from DataTable WHERE id IN (SELECT id FROM (
                    query += SqlPhrase.GetRandomSamplePrefix();
                }

                // If its a pre-configured selection type, set the search terms to match that selection type
                CustomSelection.SetSearchTermsFromSelection(selection, GetSelectedFolder);

                if (GlobalReferences.DetectionsExists && CustomSelection.ShowMissingDetections)
                {
                    // MISSING DETECTIONS 
                    // Create a partial query that returns all missing detections
                    // Form: SELECT DataTable.* FROM DataTable LEFT JOIN Detections ON DataTable.ID = Detections.Id WHERE Detections.Id IS NULL
                    query += SqlPhrase.SelectMissingDetections(SelectTypesEnum.Star);
                }


                else if (CustomSelection.EpisodeShowAllIfAnyMatch
                    && CustomSelection.EpisodeNoteField != string.Empty
                    && GlobalReferences.DetectionsExists
                    && CustomSelection.RecognitionSelections.UseRecognition)
                {
                    // EPISODE SHOW ALL and DETECTIONS are selected (special case query)
                    string episodeFieldName = CustomSelection.EpisodeNoteField;
                    detectionsAndEpisodeShowAllHandled = true;
                    query += ComposeQueryFragmentForDetectionsAndEpisodeShowAll(CustomSelection, episodeFieldName, false);
                }

                else if (GlobalReferences.DetectionsExists
                         && CustomSelection.RecognitionSelections.UseRecognition
                         && CustomSelection.RecognitionSelections.RecognitionType == RecognitionType.Detection)
                {
                    // DETECTIONS
                    // Create a partial query that returns detections matching some conditions
                    // Form: SELECT DataTable.* FROM Detections INNER JOIN DataTable ON DataTable.Id = Detections.Id
                    query += SqlPhrase.SelectDetections(SelectTypesEnum.Star);
                }

                else if (GlobalReferences.DetectionsExists
                         && CustomSelection.RecognitionSelections.UseRecognition
                         && CustomSelection.RecognitionSelections.RecognitionType == RecognitionType.Classification)
                {
                    // CLASSIFICATIONS 
                    // Same form as Detections but with error checks
                    if (null == this.detectionCategoriesDictionary)
                    {
                        // Error
                        return;
                    }
                    query += SqlPhrase.SelectDetections(SelectTypesEnum.Star);
                }

                else
                {
                    // Standard query (ie., no detections, no missing detections, no classifications 
                    query += $"{Sql.SelectStarFrom} {DBTables.FileData}";
                }
            }

            // Only do this block if it wasn't already handled
            if (detectionsAndEpisodeShowAllHandled is false)
            {
                if (CustomSelection != null) // && (GlobalReferences.DetectionsExists == false || CustomSelection.ShowMissingDetections == false))
                {
                    if (GlobalReferences.DetectionsExists is false || CustomSelection.ShowMissingDetections is false)
                    {
                        // Standard where 
                        string whereExpression = CustomSelection.GetFilesWhere();
                        if (string.IsNullOrEmpty(whereExpression) is false)
                        {
                            query += whereExpression;
                        }
                    }
                    else if (GlobalReferences.DetectionsExists || CustomSelection.ShowMissingDetections)
                    {
                        // Show missing recognitions is selected: the where clause should only include the data fields (i.e., no recognition conditions), if any
                        string where = CustomSelection.GetFilesWhere(true, true);
                        if (!string.IsNullOrEmpty(where))
                        {
                            query += $"{Sql.And} {where}";
                        }
                    }
                }

                // EPISODES-related addition to query.
                // If EpisodeShowAllIfAnyMatch is turned on, and the Episode Note field contains values in the Episode format (e.g.) 25:1/8....
                // We construct a wrapper for selecting files where all files in an episode have at least one file matching the surrounded search condition 
                if (CustomSelection is { EpisodeShowAllIfAnyMatch: true } && CustomSelection.EpisodeNoteField != string.Empty)
                {
                    string frontWrapper = SqlPhrase.CountOrSelectFilesInEpisodeIfOneFileMatchesFrontWrapper(DBTables.FileData, CustomSelection.EpisodeNoteField, false);
                    string backWrapper = $"{Sql.CloseParenthesis}{Sql.CloseParenthesis}";
                    query = $"{frontWrapper} {query} {backWrapper}";
                }
            }

            // Sort by primary and secondary sort criteria if an image set is actually initialized (i.e., not null)
            if (ImageSet != null)
            {
                // If the use click the Rank by Detection or Classification confidence, we have to create a sort term for that
                // If so, we will insert that into the normal sort term string shortly
                string rankSortingTerm = string.Empty;
                if (CustomSelection != null
                         && CustomSelection.RecognitionSelections.UseRecognition
                         && CustomSelection.RecognitionSelections.RecognitionType == RecognitionType.Classification
                         && CustomSelection.RecognitionSelections.RankByClassificationConfidence)
                {
                    // Classifications selected: Override any sorting as we have asked to rank the results by classification confidence values (using detection values as a secondary sort)
                    rankSortingTerm = $"{DBTables.Detections}.{DetectionColumns.ClassificationConf}{Sql.Descending},{DBTables.Detections}.{DetectionColumns.Conf}{Sql.Descending}";

                }

                // Get the specified sort order. We do this by retrieving the two sort terms
                string[] term = GetSortTermsButResetToDefaultIfNone();

                // Random selection - Add suffix
                if (CustomSelection is { RandomSample: > 0 })
                {
                    query += SqlPhrase.GetRandomSampleSuffix(CustomSelection.RandomSample);
                }

                if (!string.IsNullOrEmpty(rankSortingTerm))
                {
                    // As there is a rank sort term, we insert that at the beginning of the sort order
                    query += SqlPhrase.GetOrderByTerm(rankSortingTerm);
                }

                if (!string.IsNullOrEmpty(term[0]))
                {
                    if (string.IsNullOrEmpty(rankSortingTerm))
                    {
                        // Since there was no rank sort term inserted, we  need to insert an OrderBy
                        query += SqlPhrase.GetOrderByTerm(term[0]);
                    }
                    else
                    {
                        // As we added a rankSorting term, we already have an OrderBy so we just insert a comma
                        query += SqlPhrase.GetCommaThenTerm(term[0]);
                    }

                    // If there is a second sort key, add it here
                    if (!string.IsNullOrEmpty(term[1]))
                    {
                        query += SqlPhrase.GetCommaThenTerm(term[1]);
                    }

                    // If there is a third sort key (which would only ever be 'File') add it here.
                    // NOTE: I am not sure if this will always work on every ocassion, but my limited test says its ok.
                    if (!string.IsNullOrEmpty(term[2]))
                    {
                        query += SqlPhrase.GetCommaThenTerm(term[2]);
                    }
                }

            }

            await DoFileTableGetDataTableFromSelect(query);
        }

        // Actually load the file table with the results of the query.
        // We do this in a separate method so that we can call it from other methods that need to select files with specialized queries (e.g., sort by detection confidence)
        private async Task DoFileTableGetDataTableFromSelect(string query)
        {
            // PERFORMANCE  Running a query on a large database that returns a large datatable is very slow.
            // Async call allows busyindicator to run smoothly
            // System.Diagnostics.Debug.Print($"SelectFilesAsync Query: {Environment.NewLine}{query}");
            GlobalReferences.TimelapseState.IsNewSelection = true;
            DataTable filesTable = await Database.GetDataTableFromSelectAsync(query);
            FileTable = new(filesTable);
        }
        #endregion

        #region Select Specialized versions - Select Files in the file table
        /// <summary> 
        /// Rebuild the file table  in the database table as specified in each method below.
        /// Note that FileDatabaseCountOrSelectFiles contains the Select used for various custom select conditions
        /// </summary>

        // Select all files in the file table
        public FileTable SelectAllFiles()
        {
            string query = Sql.SelectStarFrom + DBTables.FileData;
            DataTable filesTable = Database.GetDataTableFromSelect(query);
            return new(filesTable);
        }

        public List<long> SelectFilesByRelativePathAndFileName(string relativePath, string fileName)
        {
            string query = Sql.SelectStarFrom + DBTables.FileData + Sql.Where + DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(relativePath) + Sql.And + DatabaseColumn.File + Sql.Equal + Sql.Quote(fileName);
            DataTable fileTable = Database.GetDataTableFromSelect(query);
            List<long> idList = [];
            for (int i = 0; i < fileTable.Rows.Count; i++)
            {
                idList.Add((long)fileTable.Rows[i].ItemArray[0]!);
            }
            return idList;
        }

        // Check for the existence of missing files in the current selection, and return a list of IDs of those that are missing
        // PERFORMANCE this can be slow if there are many files
        public async Task<SelectMissingFilesResultEnum> SelectMissingFilesFromCurrentlySelectedFiles(IProgress<ProgressBarArguments> progress, CancellationTokenSource cancelTokenSource)
        {
            if (FileTable == null)
            {
                return SelectMissingFilesResultEnum.Cancelled;
            }
            string commaSeparatedListOfIDs = string.Empty;
            SelectMissingFilesResultEnum resultEnum = await Task.Run(() =>
            {
                int fileCount = FileTable.RowCount;
                int i = 0;
                // Check if each file exists. Get all missing files in the selection as a list of file ids, e.g., "1,2,8,10" 
                foreach (ImageRow image in FileTable)
                {
                    // Update the progress bar and populate the detection tables
                    //int percentDone = Convert.ToInt32(i++ * 100.0 / fileCount);
                    if (ReadyToRefresh()) //if (newPercentDone != percentDone)
                    {
                        if (cancelTokenSource.Token.IsCancellationRequested)
                        {
                            return SelectMissingFilesResultEnum.Cancelled;
                        }
                        Thread.Sleep(ThrottleValues.ProgressBarSleepInterval); // Allows the UI thread to update every now and then
                        progress.Report(new(
                            Convert.ToInt32(i++ * 100.0 / fileCount),
                            $"Checking to see which files, if any, are missing (now on {i}/{fileCount})",
                            true, false));
                    }

                    if (!System.IO.File.Exists(Path.Combine(RootPathToImages, image.RelativePath, image.File)))
                    {
                        commaSeparatedListOfIDs += image.ID + ",";
                    }
                    i++;
                }

                // remove the trailing comma
                commaSeparatedListOfIDs = commaSeparatedListOfIDs.TrimEnd(',');
                return string.IsNullOrEmpty(commaSeparatedListOfIDs)
                    ? SelectMissingFilesResultEnum.NoMissingFiles
                    : SelectMissingFilesResultEnum.MissingFilesFound;
            }).ConfigureAwait(true);

            if (SelectMissingFilesResultEnum.MissingFilesFound == resultEnum)
            {
                // the search for missing files was successful, where missing files were found.
                // So we need to select them in the data table
                FileTable = SelectFilesInDataTableByCommaSeparatedIds(commaSeparatedListOfIDs);
                FileTable.BindDataGrid(boundGrid, onFileDataTableRowChanged);
            }
            return resultEnum;
        }

        public List<string> SelectFileNamesWithRelativePathFromDatabase(string relativePath)
        {
            List<string> files = [];
            // Form: Select * From DataTable Where RelativePath = '<relativePath>'
            string query = Sql.Select + DatabaseColumn.File + Sql.From + DBTables.FileData + Sql.Where + DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(relativePath);
            DataTable images = Database.GetDataTableFromSelect(query);
            int count = images.Rows.Count;
            for (int i = 0; i < count; i++)
            {
                files.Add((string)images.Rows[i].ItemArray[0]);
            }
            images.Dispose();
            return files;
        }

        // Select only those files that are marked for deletion i.e. DeleteFlag = true
        public FileTable SelectFilesMarkedForDeletion()
        {
            string where = DataLabelFromStandardControlType[DatabaseColumn.DeleteFlag] + "=" + Sql.Quote(BooleanValue.True); // = value
            string query = Sql.SelectStarFrom + DBTables.FileData + Sql.Where + where;
            DataTable filesTable = Database.GetDataTableFromSelect(query);
            return new(filesTable);
        }

        // Select files with matching IDs where IDs are a comma-separated string i.e.,
        // Select * From DataTable Where  Id IN(1,2,4 )
        public FileTable SelectFilesInDataTableByCommaSeparatedIds(string listOfIds)
        {
            string query = Sql.SelectStarFrom + DBTables.FileData + Sql.WhereIDIn + Sql.OpenParenthesis + listOfIds + Sql.CloseParenthesis;
            DataTable filesTable = Database.GetDataTableFromSelect(query);
            return new(filesTable);
        }

        public FileTable SelectFileInDataTableById(string id)
        {
            string query = Sql.SelectStarFrom + DBTables.FileData + Sql.WhereIDEquals + Sql.Quote(id) + Sql.LimitOne;
            DataTable filesTable = Database.GetDataTableFromSelect(query);
            return new(filesTable);
        }

        // A specialized call: Given a relative path and two dates (in database DateTime format without the offset)
        // return a table containing ID, DateTime that matches the relative path and is inbetween the two datetime intervals
        public DataTable GetIDandDateWithRelativePathAndBetweenDates(string relativePath, string lowerDateTime, string upperDateTime)
        {
            // datetimes are in database format e.g., 2017-06-14T18:36:52.000Z 
            // Form: Select ID,DateTime from DataTable where RelativePath='relativePath' and DateTime BETWEEN 'lowerDateTime' AND 'uppderDateTime' ORDER BY DateTime ORDER BY DateTime  
            string query = Sql.Select + DatabaseColumn.ID + Sql.Comma + DatabaseColumn.DateTime + Sql.From + DBTables.FileData;
            query += Sql.Where + DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(relativePath);
            query += Sql.And + DatabaseColumn.DateTime + Sql.Between + Sql.Quote(lowerDateTime) + Sql.And + Sql.Quote(upperDateTime);
            query += Sql.OrderBy + DatabaseColumn.DateTime;
            return (Database.GetDataTableFromSelect(query));
        }
        #endregion

        #region Common SQL Query Fragments for above methods
        private static string ComposeQueryFragmentForDetectionsAndEpisodeShowAll(CustomSelection customSelection, string episodeFieldName, bool useCountForm)
        {

            // WITH MatchingEpisodes AS(
            //    SELECT DISTINCT SUBSTR(DataTable.<episodeFieldName>, 1, INSTR(DataTable.<episodeFieldName>, ':') - 1) AS ep_prefix
            // FROM Detections
            //   INNER JOIN DataTable DataTable ON DataTable.Id = Detections.Id
            //   WHERE <custom selection conditions on detections and data table, but not including any conditions on the episode note field>

            // -- if useCountForm is true we use the first expression, otherwise the second
            // )  SELECT COUNT(*)  FROM  DataTable 
            // )  SELECT * FROM  DataTable     

            // WHERE   SUBSTR   (  Episode ,  1 ,   INSTR   (  Episode ,  ':'  )  - 1  )  In   (   SELECT  ep_prefix  FROM  MatchingEpisodes  )  ORDER BY  RelativePath,  DateTime,  File
            string selectOrCountExpression = useCountForm
                ? Sql.SelectCountStarFrom
                : Sql.SelectStarFrom;
            string query = $"{Sql.With} MatchingEpisodes {Sql.As} {Sql.OpenParenthesis}{Environment.NewLine}"
                    + $"{Sql.SelectDistinct} {Sql.Substr} {Sql.OpenParenthesis}"
                    + $"{DBTables.FileData}.{episodeFieldName}{Sql.Comma} 1 {Sql.Comma} "
                    + $"{Sql.Instr} {Sql.OpenParenthesis} "
                    + $"{DBTables.FileData}.{episodeFieldName}{Sql.Comma} ':'"
                    + $"{Sql.CloseParenthesis} - 1 {Sql.CloseParenthesis} "
                    + $"{Sql.As} ep_prefix {Environment.NewLine}"
                    + $"{Sql.From} {DBTables.Detections} {Environment.NewLine}"
                    + $"{Sql.InnerJoin} {DBTables.FileData} {Sql.On} "
                    + $"{DBTables.FileData}.{DatabaseColumn.ID} {Sql.Equal} {DBTables.Detections}.{DatabaseColumn.ID} {Environment.NewLine}";
            query += customSelection.GetFilesWhere() + Environment.NewLine;
            query += $"{Sql.CloseParenthesis} {Environment.NewLine}"
                     + $" {selectOrCountExpression} {DBTables.FileData} {Environment.NewLine}"
                     + $" {Sql.Where} {Sql.Substr} {Sql.OpenParenthesis} {episodeFieldName} {Sql.Comma} 1 {Sql.Comma}"
                     + $" {Sql.Instr} {Sql.OpenParenthesis} {episodeFieldName} {Sql.Comma} ':' {Sql.CloseParenthesis} - 1 {Sql.CloseParenthesis}"
                     + $"{Sql.In} {Sql.OpenParenthesis} {Sql.Select} ep_prefix {Sql.From} MatchingEpisodes {Sql.CloseParenthesis}";
            return query;
        }
        #endregion

        #region Sort Term Helpers for above

        private string[] GetSortTermsButResetToDefaultIfNone()
        {
            string[] term = [string.Empty, string.Empty, string.Empty, string.Empty];
            SortTerm[] sortTerm = new SortTerm[2];
            for (int i = 0; i <= 1; i++)
            {
                sortTerm[i] = ImageSet.GetSortTerm(i);
                // If we see an empty data label, we don't have to construct any more terms as there will be nothing more to sort
                if (string.IsNullOrEmpty(sortTerm[i].DataLabel))
                {
                    if (i == 0)
                    {
                        // If the first term is not set, reset the sort back to the default
                        ResetSortTermsToDefault(term);
                    }
                    break;
                }

                if (sortTerm[i].DataLabel == DatabaseColumn.DateTime)
                {
                    // DateTime is already in the correct format for sorting so we just sort by the DateTime column.
                    term[i] = $"{DatabaseColumn.DateTime}";

                    // DUPLICATE RECORDS Special case if DateTime is the first search term and there is no 2nd search term. 
                    // If there are multiple files with the same date/time and one of them is a duplicate,
                    // then the duplicate may not necessarily appear in a sequence, as ambiguities just use the ID (a duplicate is created with a new ID that may be very distant from the original record).
                    // So, we default the final sort term to 'File'. However, if this is not the first search term, it can be over-written 
                    term[2] = DatabaseColumn.File;
                }
                else if (sortTerm[i].DataLabel == DatabaseColumn.File)
                {
                    // File: the modified term creates a file path by concatenating relative path and file
                    term[i] =
                        $"{DatabaseColumn.RelativePath}{Sql.Comma}{DatabaseColumn.File}";
                }

                else if (sortTerm[i].DataLabel != DatabaseColumn.ID
                         && false == CustomSelection?.SearchTerms?.Exists(x => x.DataLabel == sortTerm[i].DataLabel))
                {
                    // The Sorting data label doesn't exist (likely because that datalabel was deleted or renamed in the template)
                    // Note: as ID isn't in the list, we have to check that so it can pass through as a sort option
                    // Revert back to the default sort everywhere.
                    ResetSortTermsToDefault(term);
                    break;
                }
                else if (sortTerm[i].ControlType == Control.Counter ||
                         sortTerm[i].ControlType == Control.IntegerAny ||
                         sortTerm[i].ControlType == Control.IntegerPositive)
                {
                    // Its a counter or number type: modify sorting of blanks by transforming it into a '-1' and then by casting it as an integer
                    // Form Cast(COALESCE(NULLIF({sortTerm[i].DataLabel}, ''), '-1') as Integer);
                    term[i] = SqlPhrase.GetCastCoalesceSorttermAsType(sortTerm[i].DataLabel, Sql.AsInteger);
                }
                else if (sortTerm[i].ControlType == Control.DecimalAny ||
                         sortTerm[i].ControlType == Control.DecimalPositive)
                {
                    // Its a decimal type: modify sorting of blanks by transforming it into a '-1' and then by casting it as a decimal
                    // Form: Cast(COALESCE(NULLIF({sortTerm[i].DataLabel}, ''), '-1') as Real)
                    term[i] = term[i] = SqlPhrase.GetCastCoalesceSorttermAsType(sortTerm[i].DataLabel, Sql.AsReal);
                }
                else
                {
                    // Default: just sort by the data label
                    term[i] = sortTerm[i].DataLabel;
                }

                // Add Descending sort, if needed. Default is Ascending, so we don't have to add that
                if (sortTerm[i].IsAscending == BooleanValue.False)
                {
                    term[i] += Sql.Descending;
                }
            }

            return term;
        }

        // Used by the above
        // Reset sort terms back to the defaults
        private void ResetSortTermsToDefault(string[] term)
        {

            // The Search terms should contain some of the necessary information
            SearchTerm st1 = CustomSelection.SearchTerms.Find(x => x.DataLabel == DatabaseColumn.RelativePath);
            SearchTerm st2 = CustomSelection.SearchTerms.Find(x => x.DataLabel == DatabaseColumn.DateTime);

            SortTerm s1;
            SortTerm s2;
            if (st1 == null || st2 == null)
            {
                // Just in case the search terms aren't filled in, we use default values.
                // This will work, but the Label may not be the one defined by the use which shouldn't be a big deal
                List<SortTerm> defaultSortTerms = SortTerms.GetDefaultSortTerms();
                s1 = defaultSortTerms[0];
                s2 = defaultSortTerms[1];
            }
            else
            {
                s1 = new(st1.DataLabel, st1.Label, st1.ControlType, BooleanValue.True);
                s2 = new(st2.DataLabel, st2.Label, st2.ControlType, BooleanValue.True);
            }
            term[0] = s1.DataLabel;
            term[1] = s2.DataLabel;

            // Update the Image Set with the new sort terms
            ImageSet.SetSortTerms(s1, s2);
            UpdateSyncImageSetToDatabase();
        }
        #endregion

        #region SqlGetAllRecognitionsSortedBy 
        // for sorting all by detection or classification confidence

        internal static class SqlGetAllRecognitionsSortedBy
        {
            #region

            // static strings used by the SQL queries below
            private static readonly string lf = Environment.NewLine;
            private static readonly string rowNumber = "rowNumber";
            private static readonly string best = "best";
            private static readonly string datatable = $" {DBTables.FileData}";
            private static readonly string detTable = $" {DBTables.Detections}";
            private static readonly string dtStar = $" {datatable}.* ";
            private static readonly string id = $"{DatabaseColumn.ID}";
            private static readonly string conf = $"{DetectionColumns.Conf}";
            private static readonly string classConf = $"{DetectionColumns.ClassificationConf}";
            #endregion
            
            internal static string ByDetectionConfidence(string where, string categoryNumber, bool asCount)
            {
                // Sort all files by detection confidence (with classification confidence as a secondary sort)

                //  SELECT    DataTable.*   FROM   DataTable
                //  INNER JOIN  ( 
                //    SELECT  Id, conf, classification_conf, 
                //       ROW_NUMBER() OVER  (  PARTITION BY  Id  ORDER BY  conf  DESC , classification_conf  DESC  )  AS  rowNumber 
                //    FROM   Detections
                //      WHERE  category  =  1 
                //  )  AS  best  ON  best.Id  =   DataTable.Id  AND  best.rowNumber  =  1  
                //  WHERE (DataTable.img_species='bear')
                //  ORDER BY  best.conf  DESC , best.classification_conf  DESC , RelativePath, DateTime, File

                // As -1 is All, we can just drop the where condition
                string whereCategoryPhrase = categoryNumber == "-1" ? string.Empty : $"  {Sql.Where} {DetectionColumns.Category} {Sql.Equal} {categoryNumber}";

                // Order not needed if we are just doing counts
                string orderByPhrase = asCount
                    ? string.Empty
                    : $"{Sql.OrderBy} {best}.{conf} {Sql.Descending}, {best}.{classConf} {Sql.Descending}, {DatabaseColumn.RelativePath}, {DatabaseColumn.DateTime}, {DatabaseColumn.File}{lf}";

                return $"{GetCommonHeader(asCount)}"
                       + $"  {whereCategoryPhrase} {lf}"
                       + $"{GetCommonMiddle()}"
                       + $" {where}{lf}"
                       + $" {orderByPhrase}"
                       + $"{lf}";
            }

            internal static string ByClassificationConfidence(string where, string categoryNumber, bool asCount)
            {
                // SELECT DataTable.* FROM DataTable
                // INNER JOIN (
                //     SELECT Id, conf, classification_conf,
                //            ROW_NUMBER() OVER (PARTITION BY Id ORDER BY classification_conf DESC, conf DESC) AS rowNumber
                //     FROM Detections
                //     WHERE classification = 1
                // ) AS best ON best.Id = DataTable.Id AND best.rowNumber = 1
                // WHERE DataTable.img_species = 'bear'
                // ORDER BY best.classification_conf DESC, best.conf DESC, RelativePath, DateTime, File
                string whereCategoryPhrase = $"  {Sql.Where} {DetectionColumns.Classification} {Sql.Equal} {categoryNumber}";

                return $"{GetCommonHeader(asCount)}"
                       + $"  {whereCategoryPhrase} {lf}"
                       + $"{GetCommonMiddle()}"
                       + $" {where}{lf}"
                       + $" {Sql.OrderBy} {best}.{conf} {Sql.Descending}, {best}.{classConf} {Sql.Descending}, {DatabaseColumn.RelativePath}, {DatabaseColumn.DateTime}, {DatabaseColumn.File}{lf}"
                       + $"{lf}";
            }

            private static string GetCommonHeader(bool asCount)
            {
                // normal:  SELECT  DataTable.* FROM DataTable
                // asCount: SELECT  Count(*) FROM DataTable
                //  INNER JOIN  ( 
                //    SELECT  Id, conf, classification_conf, 
                //       ROW_NUMBER() OVER  (  PARTITION BY  Id  ORDER BY  conf  DESC , classification_conf  DESC  )  AS  rowNumber 
                //    FROM   Detections
                string selectForm = asCount
                    ? $"{Sql.SelectCountStarFrom} "
                    : $"{Sql.Select} {dtStar} {Sql.From}";
                return $"{selectForm} {datatable}{lf}"
                       + $"{Sql.InnerJoin} ( {lf}"
                       + $"  {Sql.Select} {id}, {conf}, {classConf}, {lf}"
                       + $"     {Sql.RowNumberOver} ( {Sql.PartitionBy} {id} {Sql.OrderBy} {conf} {Sql.Descending}, {classConf} {Sql.Descending} ) {Sql.As} {rowNumber} {lf}"
                       + $"  {Sql.From} {detTable}{lf}";
            }

            private static string GetCommonMiddle()
            {
                //  )  AS  best  ON  best.Id  =   DataTable.Id  AND  best.rowNumber  =  1  
                return $" ) {Sql.As} {best} {Sql.On} {best}.{DatabaseColumn.ID} {Sql.Equal} {datatable}.{DatabaseColumn.ID} {Sql.And} {best}.{rowNumber} {Sql.Equal} 1  {lf}";
            }

        }


        #endregion

    }
}
