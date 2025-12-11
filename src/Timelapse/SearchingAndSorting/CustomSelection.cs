using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;
using Timelapse.Recognition;
using Timelapse.Util;
using Arguments = Timelapse.DataStructures.Arguments;

namespace Timelapse.SearchingAndSorting
{
    /// <summary>
    /// Class CustomSelection holds a list search term particles, each reflecting criteria for a given field
    /// Note: Serializable is necessary to allow the CustomSelection to be serialized
    /// </summary>
    [Serializable]
    public class CustomSelection
    {
        #region Public Properties
        public List<SearchTerm> SearchTerms { get; set; }

        public int RandomSample { get; set; }

        // Whether we should select by the time or the date in the DateTime field
        public bool UseTimeInsteadOfDate { get; set; } = false;

        public CustomSelectionOperatorEnum TermCombiningOperator { get; set; }
        public bool ShowMissingDetections { get; set; }
        public RecognitionSelections RecognitionSelections { get; set; }

        // Episode-specific data
        public bool EpisodeShowAllIfAnyMatch { get; set; } = false;
        public string EpisodeNoteField { get; set; } = string.Empty;

        #endregion

        #region Constructor
        /// <summary>
        /// Create a CustomSelection, where we build a list of potential search terms based on the controls found in the sorted template table
        /// The search term will be used only if its 'UseForSearching' field is true
        /// </summary>

        // This one is invoked only for Json serialization/deserialization
        public CustomSelection()
        {
        }

        // this one will create the default Custom Selection
        public CustomSelection(DataTableBackedList<ControlRow> templateTable) : this(templateTable, CustomSelectionOperatorEnum.And)
        {
        }

        // this creates the default Custom Selection, but with the given termCombiningOperator (And or Or)
        public CustomSelection(DataTableBackedList<ControlRow> templateTable, CustomSelectionOperatorEnum termCombiningOperator)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(templateTable, nameof(templateTable));

            RecognitionSelections = new();
            SearchTerms = [];
            TermCombiningOperator = termCombiningOperator;

            // skip hidden controls as they're not normally a part of the user experience
            // this is potentially problematic in corner cases; perhaps add an option to show terms for all controls can be added if needed?
            foreach (ControlRow control in templateTable)
            {
                // If you don't want a control to appear in the CustomSelection, add it here
                string controlType = control.Type;

                // create search term for the control
                SearchTerm searchTerm = new()
                {
                    ControlType = controlType,
                    DataLabel = control.DataLabel,
                    DatabaseValue = control.DefaultValue,
                    Operator = SearchTermOperator.Equal,
                    Label = control.Label,
                    List = control.Type == Constant.Control.FixedChoice || control.Type == Constant.Control.MultiChoice
                        ? Choices.ChoicesFromJson(control.List).ChoiceList
                        : [],
                    UseForSearching = false
                };

                if (searchTerm.List.Count > 0)
                {
                    // Add the empty string to the beginning of the search list, which allows the option of searching for empty items
                    searchTerm.List.Insert(0, string.Empty);
                }
                SearchTerms.Add(searchTerm);

                // Create a new search term for each row, where each row specifies a particular control and how it can be searched
                if (controlType == Control.Counter ||
                    controlType == Control.IntegerAny ||
                    controlType == Control.IntegerPositive ||
                    controlType == Control.DecimalAny ||
                    controlType == Control.DecimalPositive)
                {
                    searchTerm.DatabaseValue = "0";
                    searchTerm.Operator = SearchTermOperator.GreaterThan;  // Makes more sense that people will test for > as the default rather than counters
                }
                else if (controlType == DatabaseColumn.DateTime && false == UseTimeInsteadOfDate)
                {
                    // Because UseTimeInsteadofDate is false, we use the Date portion of the DateTime
                    // Note that the first time the CustomSelection dialog is popped Timelapse calls SetDateTime() to changes the default date time to the date time 
                    // of the current image
                    searchTerm.DatabaseValue = DateTimeHandler.ToStringDatabaseDateTime(ControlDefault.DateTimeDefaultValue);
                    searchTerm.Operator = SearchTermOperator.GreaterThanOrEqual;

                    // support querying on a range of datetimes by giving the user two search terms, one configured for the start of the interval and one
                    // for the end
                    SearchTerm dateTimeLessThanOrEqual = new(searchTerm)
                    {
                        Operator = SearchTermOperator.LessThanOrEqual
                    };
                    SearchTerms.Add(dateTimeLessThanOrEqual);
                }
                else if (controlType == DatabaseColumn.DateTime && UseTimeInsteadOfDate)
                {
                    // Because UseTimeInsteadofDate is true, we use the Time portion of the DateTime
                    // the first time the CustomSelection dialog is popped Timelapse calls SetDateTime() to changes the default date time to the date time 
                    // of the current image
                    searchTerm.DatabaseValue = DateTimeHandler.ToStringTime(ControlDefault.DateTimeDefaultValue);
                    searchTerm.Operator = SearchTermOperator.GreaterThanOrEqual;

                    // support querying on a range of datetimes by giving the user two search terms, one configured for the start of the interval and one
                    // for the end
                    SearchTerm dateTimeLessThanOrEqual = new(searchTerm)
                    {
                        Operator = SearchTermOperator.LessThanOrEqual
                    };
                    SearchTerms.Add(dateTimeLessThanOrEqual);
                }
                else if (controlType == Control.Flag)
                {
                    searchTerm.DatabaseValue = BooleanValue.False;
                }
            }

            // The hacky mess below is simply to get the search terms in an ordered list, where:
            // - the standard visible search terms are at the beginning, in a specific order
            // - the remaining non-standard search terms follow, in the order specified in the template

            // Get the unordered standard search tersm
            IEnumerable<SearchTerm> unorderedStandardSearchTerms = SearchTerms.Where(
                term => term.DataLabel == DatabaseColumn.File ||
                           term.DataLabel == DatabaseColumn.RelativePath || term.DataLabel == DatabaseColumn.DateTime ||
                           term.DataLabel == DatabaseColumn.DeleteFlag);

            // Create a dictionary that will contain items in the correct order
            string secondDateTimeLabel = "2nd" + DatabaseColumn.DateTime;
            Dictionary<string, SearchTerm> dictOrderedTerms = new()
            {
                { DatabaseColumn.File, null },
                { DatabaseColumn.RelativePath, null },
                { DatabaseColumn.DateTime, null },
                { secondDateTimeLabel, null },
                { DatabaseColumn.DeleteFlag, null }
            };

            // Add the unordered search terms into the dictionary, which will put them in the correct order
            // ReSharper disable once PossibleMultipleEnumeration
            foreach (SearchTerm searchTerm in unorderedStandardSearchTerms)
            {
                if (dictOrderedTerms.ContainsKey(searchTerm.DataLabel))
                {
                    if (searchTerm.DataLabel == DatabaseColumn.DateTime && dictOrderedTerms[searchTerm.DataLabel] != null)
                    {
                        // We need to use the 2nd datetime label as there may be two DateTime entries (i.e. to allow a user to select a date range)
                        dictOrderedTerms[secondDateTimeLabel] = searchTerm;
                    }
                    else
                    {
                        dictOrderedTerms[searchTerm.DataLabel] = searchTerm;
                    }
                }
            }
            // Create a new ordered list of standard search terms based on the non-null (and correctly ordered) search terms in the dictionary
            List<SearchTerm> standardSearchTerms = [];
            foreach (KeyValuePair<string, SearchTerm> kvp in dictOrderedTerms)
            {
                if (kvp.Value != null)
                {
                    standardSearchTerms.Add(kvp.Value);
                }
            }

            // Collect all the non-standard search terms which the user currently selected as UseForSearching
            // ReSharper disable once PossibleMultipleEnumeration
            IEnumerable<SearchTerm> nonStandardSearchTerms = SearchTerms.Except(unorderedStandardSearchTerms).ToList();
            // Finally, concat the two lists together to collect all the correctly ordered search terms into a single list
            SearchTerms = standardSearchTerms.Concat(nonStandardSearchTerms).ToList();
        }
        #endregion

        #region Public Methods - Set Custom Search From Selection
        // Whenever a shortcut selection is done (other than a custom selection),
        // set the custom selection search terms to mirror that.
        public void SetSearchTermsFromSelection(FileSelectionEnum selection, string relativePath)
        {
            // Don't do anything if the selection was a custom selection
            // Note that FileSelectonENum.Folders is set elsewhere (in MenuItemSelectFOlder_Click) so we don't have to do it here.
            if (SearchTerms == null)
            {
                // This shouldn't happen, but just in case treat it as a no-op
                return;
            }

            // The various selections dictate what kinds of search terms to set and use.
            switch (selection)
            {
                case FileSelectionEnum.Custom:
                    // If its a custom selection we use the existing settings
                    // so nothing should be reset
                    return;
                case FileSelectionEnum.All:
                    // Clearing all use fields is the same as selecting All Files
                    ClearCustomSearchUses();
                    break;
                case FileSelectionEnum.Folders:
                    // Set and only use the relative path as a search term
                    // Note that we do a return here, so we don't reset the relative path to the constrained root (if the arguments are specified)
                    ClearCustomSearchUses();
                    SetAndUseRelativePathSearchTerm(relativePath);
                    return;
                case FileSelectionEnum.MarkedForDeletion:
                    // Set and only use the DeleteFlag as true as a search term
                    ClearCustomSearchUses();
                    SetAndUseDeleteFlagSearchTerm();
                    break;
                default:
                    // Shouldn't get here, but just in case it makes it a no-op
                    return;
            }

            Arguments arguments = GlobalReferences.MainWindow.Arguments;
            // For all other special cases, we also set the relative path if we are contrained to a relative path
            if (arguments is { ConstrainToRelativePath: true })
            {
                SetAndUseRelativePathSearchTerm(arguments.RelativePath);
            }
        }
        #endregion

        #region Public Methods- GetFilesWhere() creates and returns a well-formed query

        public string GetFilesWhere()
        {
            return GetFilesWhere(false, false);
        }
        // Create and return the query composed from the search term list
        // If { is true, we return only  search terms related to the data fields (i.e., no Detection or Classification terms)
        public string GetFilesWhere(bool dataFieldsOnly, bool excludeWhereString)
        {
            string where = string.Empty;

            // Collect all the standard search terms which the user currently selected as UseForSearching
            IEnumerable<SearchTerm> standardSearchTerms = SearchTerms.Where(term => term.UseForSearching
                                                                                    && (term.DataLabel == DatabaseColumn.File ||
                                                                                        term.DataLabel == DatabaseColumn.RelativePath ||
                                                                                        term.DataLabel == DatabaseColumn.DateTime ||
                                                                                        term.DataLabel == DatabaseColumn.DeleteFlag));

            // Collect all the non-standard search terms which the user currently selected as UseForSearching
            // ReSharper disable once PossibleMultipleEnumeration
            IEnumerable<SearchTerm> nonstandardSearchTerms = SearchTerms.Where(term => term.UseForSearching).Except(standardSearchTerms);

            // Combine the standard terms using the AND operator
            // ReSharper disable once PossibleMultipleEnumeration
            string standardWhere = CombineSearchTermsAndOperator(standardSearchTerms, CustomSelectionOperatorEnum.And);

            // Combine the non-standard terms using the operator defined by the user (either AND or OR)
            string nonStandarWhere = CombineSearchTermsAndOperator(nonstandardSearchTerms, TermCombiningOperator);

            string whereString = excludeWhereString ? string.Empty : Sql.Where;
            // Combine the standardWhere and nonStandardWhere clauses, depending if one or both of them exists
            if (false == string.IsNullOrWhiteSpace(standardWhere) && false == string.IsNullOrWhiteSpace(nonStandarWhere))
            {
                // We have both standard and non-standard clauses, so surround them with parenthesis and combine them with an AND
                // Form: WHERE (standardWhere clauses) AND (nonStandardWhere clauses)
                where += whereString + Sql.OpenParenthesis + standardWhere + Sql.CloseParenthesis
                         + Sql.And
                         + Sql.OpenParenthesis + nonStandarWhere + Sql.CloseParenthesis;
            }
            else if (false == string.IsNullOrWhiteSpace(standardWhere) && string.IsNullOrWhiteSpace(nonStandarWhere))
            {
                // We only have a standard clause
                // Form: WHERE (standardWhere clauses)
                where += whereString + Sql.OpenParenthesis + standardWhere + Sql.CloseParenthesis;
            }
            else if (string.IsNullOrWhiteSpace(standardWhere) && false == string.IsNullOrWhiteSpace(nonStandarWhere))
            {
                // We only have a non-standard clause
                // Form: WHERE nonStandardWhere clauses
                where += whereString + nonStandarWhere;
            }

            // If no detections, or if the detectons of of RecognitionType none, we are done. Return the current where clause
            if (dataFieldsOnly || GlobalReferences.DetectionsExists == false || RecognitionSelections.UseRecognition == false ||
                RecognitionSelections.RecognitionType == RecognitionType.Empty)
            {
                return where;
            }

            // Add the Detection selection terms
            // Form prior to this point: SELECT DataTable.* INNER JOIN DataTable ON DataTable.Id = Detections.Id  
            // (and if a classification it adds: // INNER JOIN Detections ON Detections.detectionID = Classifications.detectionID 

            // There are four basic forms to come up as follows, which determines whether we should add 'WHERE'
            // The first is a detection and uses the detection category (i.e., any category but All Detections)
            // - WHERE Detections.category = <DetectionCategoryNumber> GROUP BY ...
            // The second is a dection but does not use a detection category(i.e., All Detections chosen)
            // - GROUP BY ...
            // XXXX The third uses applies to both
            // - WHERE Detections.category = <DetectionCategoryNumber> GROUP BY ...
            // - GROUP BY...

            // Form: WHERE or AND/OR
            // Add Where if we are using the first form, otherwise AND
            bool addAndOr = false;
            if (string.IsNullOrEmpty(where) && RecognitionSelections.AllDetections == false && RecognitionSelections.InterpretAllDetectionsAsEmpty == false)
            {
                where += whereString;
            }
            else
            {
                addAndOr = true;
            }

            // DETECTION, NOT ALL
            // FORM: 
            // Only added if we are using a detection category (i.e., any category but All Detections)
            if (RecognitionSelections.AllDetections == false && RecognitionSelections.InterpretAllDetectionsAsEmpty == false &&
                RecognitionSelections.RecognitionType != RecognitionType.Empty)
            {
                if (addAndOr)
                {
                    where += Sql.And;
                }

                // Form example: Detections.Category = detectionCategory
                // The line below ensures that the detection category number is always set to All when counting classifications.
                string detectionCategoryNumber = this.RecognitionSelections.RecognitionType == RecognitionType.Detection 
                    ? RecognitionSelections.DetectionCategoryNumber 
                    : Constant.RecognizerValues.AllDetectionCategoryNumber;
                where += SqlPhrase.DetectionCategoryEqualsDetectionCategory(detectionCategoryNumber);
            }

            // Form:  see below to use the confidence range
            // Note that a confidence of 0 captures empty items with 0 confidence i.e., images with no detections in them
            // For the All category, we really don't wan't to include those, so the confidence has been bumped up slightly(in Item1) above 0
            // For the Empty category, we invert the confidence

            if (RecognitionSelections.RecognitionType == RecognitionType.Detection )
            {
                if (RecognitionSelections.RankByDetectionConfidence == false)
                {
                    Tuple<double, double> detectionConfidenceBounds = RecognitionSelections.ConfidenceDetectionThresholdForSelect;
                    if (this.RecognitionSelections.AllDetections && this.RecognitionSelections.InterpretAllDetectionsAsEmpty)
                    {
                        // Empty needs to operate on the MAX confidence of all detections within an image,
                        // as otherwise it will identify an image as empty if one of its detections happens to be below the confidence
                        // even if others are above it.
                        // Detection. Form: Group By Detections.Id Having Max ( Detections.conf ) BETWEEN <Item1> AND <Item2>  e.g.. Between .8 and 1
                        where += SqlPhrase.GroupByDetectionsIdHavingMaxDetectionsConf(detectionConfidenceBounds.Item1, detectionConfidenceBounds.Item2);
                    }
                    else
                    {
                        // All other detection types
                        where += SqlPhrase.DetectionsByDetectionCategoryAndConfidence(detectionConfidenceBounds.Item1, detectionConfidenceBounds.Item2);
                    }
                }
                else
                {
                    // Sorting works on everything. So we need to get all detections, so we use the confidence range of 0 to 1
                    if (RecognitionSelections.AllDetections && RecognitionSelections.InterpretAllDetectionsAsEmpty)
                    {
                        where += SqlPhrase.DetectionsByDetectionCategoryAndConfidence(0, 0);
                    }
                }
            }
            else if (RecognitionSelections.RecognitionType == RecognitionType.Classification)
            {
                if (RecognitionSelections.RankByDetectionConfidence == false && RecognitionSelections.RankByClassificationConfidence == false)
                {
                    // Note: we omit this phrase if we are ranking by confidence, as we want to return all classifications
                    // where includes datalabel fields (if any), detection category at a given confidence, classification category at a given confidence
                    // Example form:  WHERE  ( DataTable.Note0 IS NULL  OR DataTable.Note0 =  '')  AND Detections.category = 1 AND  Detections.conf  BETWEEN  0.85  AND  1  AND  Detections.classification  =  '17' AND  Detections.classification_conf  BETWEEN  0.6  AND  1
                    Tuple<double, double> detectionConfidenceBounds = RecognitionSelections.ConfidenceDetectionThresholdForSelect;
                    where += SqlPhrase.ClassificationsByDetectionsAndClassificationCategoryAndConfidence(detectionConfidenceBounds.Item1, detectionConfidenceBounds.Item2,
                        RecognitionSelections.ClassificationCategoryNumber, RecognitionSelections.ClassificationConfidenceLowerForUI,
                        RecognitionSelections.ClassificationConfidenceHigherForUI);
                }
                else 
                {
                    // Sorting works on everything. So need to get all detections and classifications, so we use the confidence range of 0 to 1 for both
                    where += SqlPhrase.ClassificationsByDetectionsAndClassificationCategoryAndConfidence(0, 1,
                        RecognitionSelections.ClassificationCategoryNumber, 0,1);
                }
            }
            return where;
        }

        // Combine the search terms in searchTerms using the termCombiningOperator (i.e. And or OR), and special cases in as needed.
        private string CombineSearchTermsAndOperator(IEnumerable<SearchTerm> searchTerms, CustomSelectionOperatorEnum termCombiningOperator)
        {
            string where = string.Empty;

            // Special case on Time.
            // If there are two time terms and the select goes over midnight, we combine them with an OR instead of AND
            // This allows a select between (say) 10pm and 7am
            // ReSharper disable once PossibleMultipleEnumeration
            bool areTimeTermsCombined = CombineTimeSearchTermsIfNeeded(UseTimeInsteadOfDate, searchTerms, RecognitionSelections.UseRecognition, out string combinedTimeTerm);

            bool timeHandled = false; // Allows us to track whether we are on the first or second time term
            // ReSharper disable once PossibleMultipleEnumeration
            foreach (SearchTerm searchTerm in searchTerms)
            {
                // Basic Form after the ForEach iteration should be:
                // "" if nothing in it
                // a=b for the first term
                // ... AND/OR c=d ... for subsequent terms (AND/OR defined in termCombiningOperator
                // variations are special cases for relative path and datetime
                string whereForTerm = string.Empty;

                if (areTimeTermsCombined && searchTerm.DataLabel == Constant.DatabaseColumn.DateTime)
                {
                    // Handle the special case dealing with the combined Time terms
                    if (timeHandled)
                    {
                        // We are on the second time term, so we can skip it as its already handled in the combined expression.
                        continue;
                    }

                    // We are on the first time term, so we use the combined Time expression
                    whereForTerm = combinedTimeTerm;
                    timeHandled = true;
                }
                else
                {
                    // If we are using detections, then we have to qualify the data label e.g., DataTable.X
                    string dataLabel = RecognitionSelections.UseRecognition ? DBTables.FileData + "." + searchTerm.DataLabel : searchTerm.DataLabel;

                    // Check to see if the search term is querying for an empty string
                    if (string.IsNullOrEmpty(searchTerm.DatabaseValue) && searchTerm.Operator == SearchTermOperator.Equal)
                    {
                        // It is, so we also need to expand the query to check for both nulls an empty string, as both are considered equivalent for query purposes
                        // Form: ( dataLabel IS NULL OR  dataLabel = '' );
                        whereForTerm = SqlPhrase.LabelIsNullOrDataLabelEqualsEmpty(dataLabel);
                    }
                    else
                    {
                        // The search term is querying for a non-empty value.
                        Debug.Assert(searchTerm.DatabaseValue!.Contains("\"") == false,
                            $"Search term '{searchTerm.DatabaseValue}' contains quotation marks and could be used for SQL injection.");
                        if (dataLabel == DatabaseColumn.RelativePath ||
                            dataLabel == DBTables.FileData + "." + DatabaseColumn.RelativePath)
                        {
                            // Special case for RelativePath and DataTable.RelativePath, 
                            // as we want to return images not only in the relative path folder, but its subfolder as well.
                            // Form: ( DataTable.RelativePath='relpathValue' OR DataTable.RelativePath GLOB 'relpathValue\*' )
                            string term1 = SqlPhrase.DataLabelOperatorValue(dataLabel, TermToSqlOperator(SearchTermOperator.Equal), searchTerm.DatabaseValue, Sql.Text);
                            string term2 = SqlPhrase.DataLabelOperatorValue(dataLabel, TermToSqlOperator(SearchTermOperator.Glob), searchTerm.DatabaseValue + @"\*", Sql.Text);
                            whereForTerm += Sql.OpenParenthesis + term1 + Sql.Or + term2 + Sql.CloseParenthesis;
                        }
                        else if ((dataLabel == DatabaseColumn.DateTime ||
                                  dataLabel == DBTables.FileData + "." + DatabaseColumn.DateTime) && false == UseTimeInsteadOfDate)
                        {
                            // Custom search by date only (regardless of time of day): this form matches only the Date portion of the DateTime
                            whereForTerm = SqlPhrase.DataLabelDateTimeOperatorValue(dataLabel, TermToSqlOperator(searchTerm.Operator), searchTerm.DatabaseValue);
                        }
                        else if ((dataLabel == DatabaseColumn.DateTime ||
                                  dataLabel == DBTables.FileData + "." + DatabaseColumn.DateTime) && UseTimeInsteadOfDate)
                        {
                            // Custom search by time only (regardless of date): this form matches only the Time portion of the DateTime
                            whereForTerm = SqlPhrase.DataLabelTimeOperatorValue(dataLabel, TermToSqlOperator(searchTerm.Operator), searchTerm.DatabaseValue);
                        }
                        else
                        {
                            // Standard search term
                            // Form: dataLabel operator "value", e.g., DataLabel > "5"
                            string sqlType = Sql.Text;
                            if (searchTerm.ControlType == Control.Counter ||
                                searchTerm.ControlType == Control.IntegerAny ||
                                searchTerm.ControlType == Control.IntegerPositive)
                            {
                                sqlType = Sql.IntegerType;
                            }
                            else if (searchTerm.ControlType == Control.DecimalAny ||
                                searchTerm.ControlType == Control.DecimalPositive)
                            {
                                sqlType = Sql.RealType;
                            }

                            // DO MULTICHOICE AROUND HERE,  ADD NOT GLOB
                            if (searchTerm.ControlType == Control.MultiChoice &&
                                //false == string.IsNullOrEmpty(searchTerm.DatabaseValue) &&  // Including this makes it the same as '=', which doesn't really have the same semantics as Include. But unsure...
                                (searchTerm.Operator == SearchTermOperator.Includes || searchTerm.Operator == SearchTermOperator.Excludes))
                            {
                                // MultiChoice globs have to be constructed in a different way, where we interpret it to mean
                                // 'contains at least the selected items'. See the specialized method below on how this is done.
                                whereForTerm = SqlPhrase.DataLabelOperatorValue(dataLabel, TermToSqlOperator(searchTerm.Operator), searchTerm.DatabaseValue);
                            }
                            else
                            {
                                whereForTerm = SqlPhrase.DataLabelOperatorValue(dataLabel, TermToSqlOperator(searchTerm.Operator), searchTerm.DatabaseValue, sqlType);
                            }
                        }

                        if (searchTerm.ControlType == Control.Flag)
                        {
                            // Because flags can have capitals or lower case, we need to make the search case insenstive
                            whereForTerm += Sql.CollateNocase; // so that true and false comparisons are case-insensitive
                        }
                    }
                }

                // We are now ready to assemble the search term
                // First, and only if there terms have already been added to  the query, we need to add the appropriate operator
                if (!string.IsNullOrEmpty(where))
                {
                    switch (termCombiningOperator)
                    {
                        case CustomSelectionOperatorEnum.And:
                            where += Sql.And;
                            break;
                        case CustomSelectionOperatorEnum.Or:
                            where += Sql.Or;
                            break;
                        default:
                            throw new NotSupportedException($"Unhandled logical operator {termCombiningOperator}.");
                    }
                }
                // Now we add the actual search terms
                where += whereForTerm;
            }
            // Done. Return this portion of the where clause
            if (false == string.IsNullOrWhiteSpace(where))
            {
                // surround it in brackets - this is needed to ensure that OR conditions are properly grouped
                where = $"({where})";
            }
            return where;
        }

        // Time expressions
        // To go over midnight, we need to special case searches by creating a single combined expression when
        // - two time ranges are selected
        // - when using time > time1 and time < time2 and the times go over midnight (i.e. time1 > time2)
        // - when using time < time1 and time > time2 and the times go over midnight (i.e. time1 < time2)
        // The combined expression will be surrounded by brackets and combined with OR
        private static bool CombineTimeSearchTermsIfNeeded(bool useTimeInsteadOfDate, IEnumerable<SearchTerm> searchTerms, bool useFullyQualifiedDataLabel, out string expression)
        {
            expression = string.Empty;
            if (useTimeInsteadOfDate == false)
            {
                // We aren't using Time
                return false;
            }

            IEnumerable<SearchTerm> timeTerms = searchTerms.Where(term => term.DataLabel == DatabaseColumn.DateTime && term.UseForSearching);
            // ReSharper disable once PossibleMultipleEnumeration
            if (timeTerms.Count() != 2)
            {
                // We don't have two Time terms to combine
                return false;
            }

            // ReSharper disable once PossibleMultipleEnumeration
            SearchTerm st1 = timeTerms.ElementAt(0);
            // ReSharper disable once PossibleMultipleEnumeration
            SearchTerm st2 = timeTerms.ElementAt(1);
            TimeSpan ts1 = st1.GetDateTime().TimeOfDay;
            TimeSpan ts2 = st2.GetDateTime().TimeOfDay;
            switch (st1.Operator)
            {
                case SearchTermOperator.GreaterThanOrEqual:
                case SearchTermOperator.GreaterThan:
                    // Case 1: time > time1, time < time2,  and if time1 > time2, i.e., the range should span midnight so it should be combined
                    if (st2.Operator == SearchTermOperator.LessThanOrEqual || st2.Operator == SearchTermOperator.LessThan)
                    {
                        if (ts1 > ts2)
                        {
                            expression = "Case 1";
                            break;
                        }

                        return false;
                    }
                    return false;
                case SearchTermOperator.LessThanOrEqual:
                case SearchTermOperator.LessThan:
                    // Case 2: time < time1, time > time2,  and if time1 < time2, i.e., the range should span midnight so it should be combined
                    if (st2.Operator == SearchTermOperator.GreaterThanOrEqual || st2.Operator == SearchTermOperator.GreaterThan)
                    {
                        if (ts1 < ts2)
                        {
                            expression = "Case 2";
                            break;
                        }

                        return false;
                    }
                    return false;
                default:
                    return false;
            }
            string dataLabel = useFullyQualifiedDataLabel ? DBTables.FileData + "." + st1.DataLabel : st1.DataLabel;
            expression = Sql.OpenParenthesis
                + SqlPhrase.DataLabelTimeOperatorValue(dataLabel, TermToSqlOperator(st1.Operator), st1.DatabaseValue)
                + Sql.Or
                + SqlPhrase.DataLabelTimeOperatorValue(dataLabel, TermToSqlOperator(st2.Operator), st2.DatabaseValue)
                + Sql.CloseParenthesis;
            //Debug.Print("Time term 1: " + st1.Operator + "|" + ts1.ToString());
            //Debug.Print("Time term 2: " + st2.Operator + "|" + ts2.ToString());
            return true;
        }
        #endregion

        #region Public Methods - Various Gets
        public DateTime GetDateTimePLAINVERSION(int dateTimeSearchTermIndex)
        {
            // Get the date/time
            return SearchTerms[dateTimeSearchTermIndex].GetDateTime();
        }

        public string GetRelativePathFolder
        {
            get
            {
                foreach (SearchTerm searchTerm in SearchTerms)
                {
                    if (searchTerm.DataLabel == DatabaseColumn.RelativePath)
                    {
                        return searchTerm.DatabaseValue;
                    }
                }
                return string.Empty;
            }
        }
        #endregion

        #region Public Methods - SetAndUse a particular search term
        // Set and use the RelativePath search term to search for the provided relativePath
        // Note that the query using the relative path will be created elsewhere to include sub-folders of the relative path via a GLOB operator
        public void SetAndUseRelativePathSearchTerm(string relativePath)
        {
            SearchTerm searchTerm = SearchTerms.First(term => term.DataLabel == DatabaseColumn.RelativePath);
            searchTerm.DatabaseValue = relativePath;
            searchTerm.Operator = SearchTermOperator.Equal;
            searchTerm.UseForSearching = true;
        }

        public void SetAndUseDeleteFlagSearchTerm()
        {
            // Set the use field for DeleteFlag, and its value to true
            SearchTerm searchTerm = SearchTerms.First(term => term.DataLabel == DatabaseColumn.DeleteFlag);
            searchTerm.DatabaseValue = BooleanValue.True;
            searchTerm.Operator = SearchTermOperator.Equal;
            searchTerm.UseForSearching = true;
        }
        #endregion

        #region Public Methods - Various Sets to initialize DateTimes
        public void SetDateTime(int dateTimeSearchTermIndex, DateTime newDateTime)
        {
            SearchTerms[dateTimeSearchTermIndex].SetDatabaseValue(newDateTime);
        }

        public void SetDateTimes(DateTime dateTime)
        {
            foreach (SearchTerm dateTimeTerm in SearchTerms.Where(term => term.DataLabel == DatabaseColumn.DateTime))
            {
                dateTimeTerm.SetDatabaseValue(dateTime);
            }
        }
        #endregion

        #region Public Methods - Clear Custom Search Uses
        // Clear all the 'use' flags in the custom search term and in the detections (if any)
        public void ClearCustomSearchUses()
        {
            foreach (SearchTerm searchTerm in SearchTerms)
            {
                searchTerm.UseForSearching = false;
            }
            if (GlobalReferences.DetectionsExists && RecognitionSelections != null)
            {
                RecognitionSelections.ClearAllDetectionsUses();
            }
            ShowMissingDetections = false;
        }
        #endregion

        #region Public static methods: Set Detection Ranges
        // If recognitions are selected, we set the over-ride of which bounding boxes are displayed by expanding the range to include the selection confidence values.
        // The BoundingBoxDisplayThreshold is the user-defined default set in preferences, while the BoundingBoxThresholdOveride is the threshold
        // determined in this select dialog. For example, if (say) the preference setting is .6 but the selection is at .4 confidence, then we should 
        // show bounding boxes when the confidence is .4 or more. On the other  hand, we don't want to show spurious detections when empty is selected,
        // so we set a minimum value.
        public static void SetDetectionRanges(RecognitionSelections recognitionSelections)
        {
            if (null == recognitionSelections)
            {
                return;
            }
            // When recognitions are selected, we may over-ride which bounding boxes are displayed by expanding the range to include the selection confidence values.
            if (recognitionSelections.UseRecognition)
            {
                GlobalReferences.TimelapseState.BoundingBoxThresholdOveride = recognitionSelections.InterpretAllDetectionsAsEmpty
                    // For empty, set the over-ride to a small value i.e., equal to epsilon
                    // This avoids displaying spurious bounding boxes (extremely low confidence ones) while still showing
                    // the ones that have low confidence bounding boxes that are still useful.
                    ? 0.1
                    // For non-empty, set the over-ride to include the bounding boxes within the current range
                    : recognitionSelections.DetectionConfidenceLowerForUI;
            }
            else
            {
                // When no recognitions are selected, setting the override to 1 essentially means the override value is ignore.
                // That is, the user's bounding box preference is used to decide the cut-off for displaying bounding boxes.
                GlobalReferences.TimelapseState.BoundingBoxThresholdOveride = 1;
            }
        }
        #endregion

        #region Private Methods - Used by above
        // Special case term for RelativePath 
        // as we want to return images not only in the relative path folder, but its subfolder as well.
        // Construct Partial Sql phrase used in Where for RelativePath that includes subfolders
        // Form: ( RelativePath='Station1\\Deployment2' OR RelativePath GLOB 'Station1\\Deployment2\\*' )  
        //   or if relativePath is empty: ""
        public static string RelativePathGlobToIncludeSubfolders(string relativePathColumnName, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return string.Empty;
            }

            // Form: ( DataTable.RelativePath='relpathValue' OR DataTable.RelativePath GLOB 'relpathValue\*' )
            string term1 = SqlPhrase.DataLabelOperatorValue(relativePathColumnName, TermToSqlOperator(SearchTermOperator.Equal), relativePath, Sql.Text);
            string term2 = SqlPhrase.DataLabelOperatorValue(relativePathColumnName, TermToSqlOperator(SearchTermOperator.Glob), Path.Combine(relativePath, "*"), Sql.Text);
            return Sql.OpenParenthesis + term1 + Sql.Or + term2 + Sql.CloseParenthesis;
        }

        // return SQL expressions to database equivalents
        // this is needed as the searchterm operators are unicodes representing symbols rather than real opeators 
        // e.g., \u003d is the symbol for '='
        public static string TermToSqlOperator(string expression)
        {
            switch (expression)
            {
                case SearchTermOperator.Equal:
                    return "=";
                case SearchTermOperator.NotEqual:
                    return "<>";
                case SearchTermOperator.LessThan:
                    return "<";
                case SearchTermOperator.GreaterThan:
                    return ">";
                case SearchTermOperator.LessThanOrEqual:
                    return "<=";
                case SearchTermOperator.GreaterThanOrEqual:
                    return ">=";
                case SearchTermOperator.Glob:
                case SearchTermOperator.Includes:
                    return SearchTermOperator.Glob;
                case SearchTermOperator.NotGlob:
                case SearchTermOperator.Excludes:
                    return " NOT GLOB ";
                default:
                    return string.Empty;
            }
        }
        #endregion
    }
}
