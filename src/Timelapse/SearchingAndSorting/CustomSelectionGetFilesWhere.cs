using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.Enums;

namespace Timelapse.SearchingAndSorting
{
    // This file contains the method GetFilesWhere, which creates and returns a well-formed query composed from the search term list.
    public partial class CustomSelection
    {
        #region Public Methods- GetFilesWhere() creates and returns a well-formed query

        public string GetFilesWhere()
        {
            return GetFilesWhere(false, false);
        }

        // Create and return the query composed from the search term list
        // If { is true, we return only  search terms related to the data fields (i.e., no Detection or Classification terms)
        public string GetFilesWhere(bool dataFieldsOnly, bool excludeWhereString, bool ignoreRecognitions=false)
        {
            string where = string.Empty;

            // Collect all the standard search terms which the user currently selected as UseForSearching
            IEnumerable<SearchTerm> standardSearchTerms = SearchTerms.Where(
                term => term.UseForSearching
                        && (term.DataLabel == DatabaseColumn.File
                            || term.DataLabel == DatabaseColumn.RelativePath
                            || term.DataLabel == DatabaseColumn.DateTime
                            || term.DataLabel == DatabaseColumn.DeleteFlag));

            // Collect all the non-standard search terms which the user currently selected as UseForSearching
            // ReSharper disable once PossibleMultipleEnumeration
            IEnumerable<SearchTerm> nonstandardSearchTerms = SearchTerms.Where(term => term.UseForSearching).Except(standardSearchTerms);

            // Combine the standard terms using the AND operator
            // ReSharper disable once PossibleMultipleEnumeration
            string standardWhere = CombineSearchTermsAndOperator(standardSearchTerms, CustomSelectionOperatorEnum.And);

            // Combine the non-standard terms using the operator defined by the user (either AND or OR)
            string nonStandardWhere = CombineSearchTermsAndOperator(nonstandardSearchTerms, TermCombiningOperator);

            string whereString = excludeWhereString ? string.Empty : Sql.Where;

            // Combine the standardWhere and nonStandardWhere clauses, depending if one or both of them exists
            if (   string.IsNullOrWhiteSpace(standardWhere) is false
                && string.IsNullOrWhiteSpace(nonStandardWhere) is false)
            {
                // We have both standard and non-standard clauses, so surround them with parenthesis and combine them with an AND
                // Form: WHERE (standardWhere clauses) AND (nonStandardWhere clauses)
                where += whereString + Sql.OpenParenthesis + standardWhere + Sql.CloseParenthesis
                         + Sql.And
                         + Sql.OpenParenthesis + nonStandardWhere + Sql.CloseParenthesis;
            }
            else if (   string.IsNullOrWhiteSpace(standardWhere) is false
                     && string.IsNullOrWhiteSpace(nonStandardWhere))
            {
                // We only have a standard clause
                // Form: WHERE (standardWhere clauses)
                where += whereString + Sql.OpenParenthesis + standardWhere + Sql.CloseParenthesis;
            }
            else if (   string.IsNullOrWhiteSpace(standardWhere) 
                     && string.IsNullOrWhiteSpace(nonStandardWhere) is false)
            {
                // We only have a non-standard clause
                // Form: WHERE nonStandardWhere clauses
                where += whereString + nonStandardWhere;
            }


            // If no detections, or if the detectons are of RecognitionType none, or if we want to ignore recognition terms, we are done.
            // Return the current where clause
            if (false == ignoreRecognitions)
            {
                if (dataFieldsOnly is false
                    && GlobalReferences.DetectionsExists
                    && RecognitionSelections.UseRecognition
                    && RecognitionSelections.RecognitionType is not RecognitionType.Empty
                   )
                {
                    // Add the Detection selection terms

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
                    if (string.IsNullOrEmpty(where)
                        && RecognitionSelections.AllDetections is false
                        && RecognitionSelections.InterpretAllDetectionsAsEmpty is false)
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
                    if (RecognitionSelections.AllDetections is false
                        && RecognitionSelections.InterpretAllDetectionsAsEmpty is false
                        && RecognitionSelections.RecognitionType is not RecognitionType.Empty)
                    {
                        if (addAndOr)
                        {
                            where += Sql.And;
                        }

                        // Form example: Detections.Category = detectionCategory
                        // The line below ensures that the detection category number is always set to All when counting classifications.
                        string detectionCategoryNumber = this.RecognitionSelections.RecognitionType is RecognitionType.Detection
                            ? RecognitionSelections.DetectionCategoryNumber
                            : Constant.RecognizerValues.AllDetectionCategoryNumber;
                        where += SqlPhrase.DetectionCategoryEqualsDetectionCategory(detectionCategoryNumber);
                    }

                    // Form:  see below to use the confidence range
                    // Note that a confidence of 0 captures empty items with 0 confidence i.e., images with no detections in them
                    // For the All category, we really don't wan't to include those, so the confidence has been bumped up slightly(in Item1) above 0
                    // For the Empty category, we invert the confidence

                    if (RecognitionSelections.RecognitionType is RecognitionType.Detection)
                    {
                        if (RecognitionSelections.RankByDetectionConfidence is false)
                        {
                            Tuple<double, double> detectionConfidenceBounds = RecognitionSelections.ConfidenceDetectionThresholdForSelect;
                            if (this.RecognitionSelections.AllDetections
                                && this.RecognitionSelections.InterpretAllDetectionsAsEmpty)
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
                            // Sorting works on everything. As we need to get all detections, we use the confidence range of 0 to 1
                            if (RecognitionSelections.AllDetections
                                && RecognitionSelections.InterpretAllDetectionsAsEmpty)
                            {
                                where += SqlPhrase.DetectionsByDetectionCategoryAndConfidence(0, 1);
                            }
                        }
                    }
                    else if (RecognitionSelections.RecognitionType is RecognitionType.Classification)
                    {
                        if (RecognitionSelections.RankByDetectionConfidence is false
                            && RecognitionSelections.RankByClassificationConfidence is false)
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
                                RecognitionSelections.ClassificationCategoryNumber, 0, 1);
                        }
                    }
                }
            }

            // Debug.Print("CustomSelection GetFilesWhere: " + where);
            return where;
        }

        #endregion

        #region Helpers: CombineSearchTermsAndOperato
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
    }
}
