using System;
using System.Collections.Generic;
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
    public partial class CustomSelection
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
