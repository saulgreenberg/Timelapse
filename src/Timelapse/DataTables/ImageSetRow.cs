using System.Collections.Generic;
using System.Data;
using Newtonsoft.Json;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.Extensions;
using Timelapse.SearchingAndSorting;

namespace Timelapse.DataTables
{
    /// <summary>
    ///  An ImageSet Row defines the contents of the (single) row in the ImageSet DataTable
    /// </summary>
    public class ImageSetRow(DataRow row) : DataRowBackedObject(row)
    {
        #region Public Properties to set / get the various row values
        // Name of the root folder containing the template
        public string RootFolderName
        {
            get => Row.GetStringField(DatabaseColumn.RootFolder);
            set => Row.SetField(DatabaseColumn.RootFolder, value);
        }

        // Most recently selected File by its ID
        public long MostRecentFileID
        {
            get => Row.GetLongStringField(DatabaseColumn.MostRecentFileID);
            set => Row.SetField(DatabaseColumn.MostRecentFileID, value);
        }

        // The log contains text that the user can set, which can serve as notes
        public string Log
        {
            get => Row.GetStringField(DatabaseColumn.Log);
            set => Row.SetField(DatabaseColumn.Log, value);
        }

        // The most recent timelapse version used to open the files
        public string VersionCompatability
        {
            get => Row.GetStringField(DatabaseColumn.VersionCompatibility);
            set => Row.SetField(DatabaseColumn.VersionCompatibility, value);
        }

        // The most recent timelapse version used to open the files
        public string BackwardsCompatability
        {
            get => Row.GetStringField(DatabaseColumn.BackwardsCompatibility);
            set => Row.SetField(DatabaseColumn.BackwardsCompatibility, value);
        }

        // JSON description of the QuickPasteEntries.
        public string QuickPasteAsJSON
        {
            get => Row.GetStringField(DatabaseColumn.QuickPasteTerms);
            set => Row.SetField(DatabaseColumn.QuickPasteTerms, value);
        }

        // JSON description of the SearchTerms
        public string SearchTermsAsJSON
        {
            get => Row.GetStringField(DatabaseColumn.SearchTerms);
            set => Row.SetField(DatabaseColumn.SearchTerms, value);
        }

        // The standard contains the name of the standard used to create the template,otherwise empty
        public string Standard
        {
            get => Row.GetStringField(DatabaseColumn.Standard);
            set => Row.SetField(DatabaseColumn.Standard, value);
        }
        #endregion

        #region Private Properties to set / get the various row values
        // The SortTerms comprises a list of sort terms e.g., "RelativePath, File,,"
        // Helper functions unpack and pack those terms (see below)
        // Currently, only two terms are used and expected, where the first and second terms act as Sorting pairs.
        // For some cases, the first term in a pair
        // is the sorting term, where the second term is empty. 
        // However, some sorting criteria are compound. For example, if the user specifies 'Date' the pair 
        // will actually comprise Date,Time. Similarly File is 'RelativePath,File'.
        // The sort term is stored in the database as a json string
        //  DataLabel, Label, ControlType, IsAscending

        // A list that collects the sort terms
        private List<SortTerm> SortTerms;
        // A string stored in the database holding the JSON reprentation of the sort terms
        // This is not accessed directly, but rather by the Get/SetSortTerm functions
        private string SortTermsAsJsonString
        {
            get => Row.GetStringField(DatabaseColumn.SortTerms);
            set => Row.SetField(DatabaseColumn.SortTerms, value);
        }
        #endregion

        #region Public Methods - Create ColumnTuplesWithWhere 
        // Construct a ColumnTuplesWithWhere containing the entire row contents 
        // Where is the current (and only?) imageset ID
        public override ColumnTuplesWithWhere CreateColumnTuplesWithWhereByID()
        {
            List<ColumnTuple> columnTuples =
            [
                new(DatabaseColumn.RootFolder, RootFolderName),
                new(DatabaseColumn.Log, Log),
                new(DatabaseColumn.MostRecentFileID, MostRecentFileID),
                new(DatabaseColumn.VersionCompatibility, VersionCompatability),
                new(DatabaseColumn.BackwardsCompatibility, BackwardsCompatability),
                new(DatabaseColumn.SortTerms, SortTermsAsJsonString),
                new(DatabaseColumn.SearchTerms, SearchTermsAsJSON),
                new(DatabaseColumn.QuickPasteTerms, QuickPasteAsJSON),
                new(DatabaseColumn.Standard, Standard)
            ];
            return new(columnTuples, ID);
        }
        #endregion

        #region Get/Set SortTerm functions: setting and getting individual terms in the internally maintained sort term list
        // Return the specified sort term from the list. Currently, we only use 2 sort terms, where the 2nd can be empty
        public SortTerm GetSortTerm(int whichOne)
        {
            try
            {
                SortTerms = JsonConvert.DeserializeObject<List<SortTerm>>(SortTermsAsJsonString);
                return SortTerms[whichOne];
            }
            catch
            {
                // While this shouldn't happen, if there is a problem getting the sort terms (e.g., bad json, index out of bounds), revert to the default sort
                SortTerms = JsonConvert.DeserializeObject<List<SortTerm>>(DatabaseValues.DefaultSortTerms);
                return SortTerms[whichOne];
            }
        }

        // Set the sort terms. Currently, we only use 2 sort terms
        public void SetSortTerms(SortTerm sortTerm1, SortTerm sortTerm2)
        {
            // Check the arguments for null 
            if (SortTerms == null)
            {
                SortTerms = [];
            }
            else
            {
                SortTerms.Clear();
            }
            SortTerms.Add(sortTerm1 ?? new SortTerm()); // Note that this could break if sort term doesn't contained the expected values
            SortTerms.Add(sortTerm2 ?? new SortTerm());
            SortTermsAsJsonString = JsonConvert.SerializeObject(SortTerms, Formatting.Indented);
        }
        #endregion
    }
}