using Newtonsoft.Json;
using System.Collections.Generic;
using System.Data;

namespace Timelapse.Database
{
    /// <summary>
    ///  An ImageSet Row defines the contents of the (single) row in the ImageSet DataTable
    /// </summary>
    public class ImageSetRow : DataRowBackedObject
    {
        #region Public Properties to set / get the various row values
        // Name of the root folder containing the template
        public string RootFolder
        {
            get => this.Row.GetStringField(Constant.DatabaseColumn.RootFolder);
            set => this.Row.SetField(Constant.DatabaseColumn.RootFolder, value);
        }

        // Most recently selected File by its ID
        public long MostRecentFileID
        {
            get => this.Row.GetLongStringField(Constant.DatabaseColumn.MostRecentFileID);
            set => this.Row.SetField(Constant.DatabaseColumn.MostRecentFileID, value);
        }

        // The log contains text that the user can set, which can serve as notes
        public string Log
        {
            get => this.Row.GetStringField(Constant.DatabaseColumn.Log);
            set => this.Row.SetField(Constant.DatabaseColumn.Log, value);
        }

        // The most recent timelapse version used to open the files
        public string VersionCompatability
        {
            get => this.Row.GetStringField(Constant.DatabaseColumn.VersionCompatabily);
            set => this.Row.SetField(Constant.DatabaseColumn.VersionCompatabily, value);
        }

        // JSON description of the QuickPasteEntries.
        public string QuickPasteAsJSON
        {
            get => this.Row.GetStringField(Constant.DatabaseColumn.QuickPasteTerms);
            set => this.Row.SetField(Constant.DatabaseColumn.QuickPasteTerms, value);
        }

        // JSON description of the SearchTerms
        public string SearchTermsAsJSON
        {
            get => this.Row.GetStringField(Constant.DatabaseColumn.SearchTerms);
            set => this.Row.SetField(Constant.DatabaseColumn.SearchTerms, value);
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
            get => this.Row.GetStringField(Constant.DatabaseColumn.SortTerms);
            set => this.Row.SetField(Constant.DatabaseColumn.SortTerms, value);
        }
        #endregion

        #region Constructors
        public ImageSetRow(DataRow row)
            : base(row)
        {
        }
        #endregion

        #region Public Methods - Create ColumnTuplesWithWhere 
        // Construct a ColumnTuplesWithWhere containing the entire row contents 
        // Where is the current (and only?) imageset ID
        public override ColumnTuplesWithWhere CreateColumnTuplesWithWhereByID()
        {
            List<ColumnTuple> columnTuples = new List<ColumnTuple>
            {
                //new ColumnTuple(Constant.DatabaseColumn.Selection, (int)this.FileSelection),
                new ColumnTuple(Constant.DatabaseColumn.RootFolder,this.RootFolder),
                new ColumnTuple(Constant.DatabaseColumn.Log, this.Log),
                new ColumnTuple(Constant.DatabaseColumn.MostRecentFileID, this.MostRecentFileID),
                new ColumnTuple(Constant.DatabaseColumn.VersionCompatabily, this.VersionCompatability),
                new ColumnTuple(Constant.DatabaseColumn.SortTerms, this.SortTermsAsJsonString),
                new ColumnTuple(Constant.DatabaseColumn.SearchTerms, this.SearchTermsAsJSON),
                new ColumnTuple(Constant.DatabaseColumn.QuickPasteTerms, this.QuickPasteAsJSON),
                //new ColumnTuple(Constant.DatabaseColumn.SelectedFolder, this.SelectedFolder)
            };
            return new ColumnTuplesWithWhere(columnTuples, this.ID);
        }
        #endregion

        #region Get/Set SortTerm functions: setting and getting individual terms in the internally maintained sort term list
        // Return the specified sort term from the list. Currently, we only use 2 sort terms, where the 2nd can be empty
        public SortTerm GetSortTerm(int whichOne)
        {
            try
            {
                this.SortTerms = JsonConvert.DeserializeObject<List<SortTerm>>(this.SortTermsAsJsonString);
                return this.SortTerms[whichOne];
            }
            catch
            {
                // While this shouldn't happen, if there is a problem getting the sort terms (e.g., bad json, index out of bounds), revert to the default sort
                this.SortTerms = JsonConvert.DeserializeObject<List<SortTerm>>(Constant.DatabaseValues.DefaultSortTerms);
                return this.SortTerms[whichOne];
            }
        }

        // Set the sort terms. Currently, we only use 2 sort terms
        public void SetSortTerms(SortTerm sortTerm1, SortTerm sortTerm2)
        {
            // Check the arguments for null 
            if (this.SortTerms == null)
            {
                this.SortTerms = new List<SortTerm>();
            }
            else
            {
                this.SortTerms.Clear();
            }
            this.SortTerms.Add(sortTerm1 ?? new SortTerm()); // Note that this could break if sort term doesn't contained the expected values
            this.SortTerms.Add(sortTerm2 ?? new SortTerm());
            this.SortTermsAsJsonString = JsonConvert.SerializeObject(this.SortTerms, Formatting.Indented);
        }
        #endregion
    }
}