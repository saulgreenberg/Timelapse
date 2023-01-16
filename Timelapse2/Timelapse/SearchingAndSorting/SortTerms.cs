using System.Collections.Generic;
using Timelapse.Util;

namespace Timelapse.Database
{
    public static class SortTerms
    {
        /// <summary>
        /// Create a CustomSort, where we build a list of potential sort terms based on the controls found in the sorted template table
        /// We do this by using and modifying the values returned by CustomSelect. 
        /// While a bit of a hack and less efficient, its easier than re-implementing what we can already get from customSelect.
        /// Note that each sort term is a triplet indicating the Data Label, Label, and a string flag on whether the sort should be ascending (default) or descending.
        /// </summary>
        #region Public Static Methods - Get Sort Terms
        // Construct a list of sort terms holding the default Sort criteria,
        // That is, by RelativePath and DateTime both ascending
        public static List<SortTerm> GetDefaultSortTerms()
        {
            List<SortTerm> sortTerms = new List<SortTerm>
            {
                new SortTerm(Constant.DatabaseColumn.RelativePath, Constant.SortTermValues.RelativePathDisplayLabel, Constant.DatabaseColumn.RelativePath, Constant.BooleanValue.True),
                new SortTerm(Constant.DatabaseColumn.DateTime, Constant.SortTermValues.DateDisplayLabel, Constant.DatabaseColumn.DateTime, Constant.BooleanValue.True)
            };
            return sortTerms;
        }
        public static List<SortTerm> GetSortTerms(List<SearchTerm> searchTerms)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(searchTerms, nameof(searchTerms));

            List<SortTerm> sortTerms = new List<SortTerm>();

            // Constraints. 
            // - the SearchTerms list excludes Id. It also includes two DateTime copies of DateTime
            // - Add Id 
            // - Remove the 2nd DateTiime
            // - Add Id as it is missing
            bool firstDateTimeSeen = false;
            sortTerms.Add(new SortTerm(Constant.DatabaseColumn.ID, Constant.DatabaseColumn.ID, Sql.IntegerType, Constant.BooleanValue.True));

            foreach (SearchTerm searchTerm in searchTerms)
            {
                // Necessary modifications:
                // - Exclude RelativePath           
                if (searchTerm.DataLabel == Constant.DatabaseColumn.File)
                {
                    sortTerms.Add(new SortTerm(searchTerm.DataLabel, Constant.SortTermValues.FileDisplayLabel, searchTerm.ControlType, Constant.BooleanValue.True));
                }
                else if (searchTerm.DataLabel == Constant.DatabaseColumn.DateTime)
                {
                    // Skip the second DateTime
                    if (firstDateTimeSeen)
                    {
                        continue;
                    }
                    firstDateTimeSeen = true;
                    sortTerms.Add(new SortTerm(searchTerm.DataLabel, Constant.SortTermValues.DateDisplayLabel, searchTerm.ControlType, Constant.BooleanValue.True));
                }

                sortTerms.Add(searchTerm.DataLabel == Constant.DatabaseColumn.RelativePath
                    ? new SortTerm(searchTerm.DataLabel, Constant.SortTermValues.RelativePathDisplayLabel,
                        searchTerm.ControlType, Constant.BooleanValue.True)
                    : new SortTerm(searchTerm.DataLabel, searchTerm.Label, searchTerm.ControlType,
                        Constant.BooleanValue.True));
            }
            return sortTerms;
        }
        #endregion
    }
}
