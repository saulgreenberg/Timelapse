using System.Collections.Generic;
using System.Linq;
using Timelapse.Constant;
using Timelapse.DataStructures;
using Timelapse.DataTables;
using Timelapse.Enums;

namespace Timelapse.SearchingAndSorting
{
    /// <summary>
    /// The folder metadata part of a custom selection.
    /// It is saved in its own database entry rather than with the other search terms, as earlier versions of Timelapse wouldn't understand it.
    /// </summary>
    public class FolderMetadataSelection
    {
        // Whether the folder metadata terms are used at all
        public bool UseFolderMetadata { get; set; }

        // How the folder metadata terms are combined with each other. They are always ANDed with the other search terms.
        public CustomSelectionOperatorEnum CombiningOperator { get; set; } = CustomSelectionOperatorEnum.And;

        // The levels that can be searched. The top level isn't one of them, as its values apply to all files.
        public List<FolderLevelSearchTerms> Levels { get; set; } = [];

        // The name of the top level, for telling the user why it isn't shown. It is null if that level has no fields.
        [Newtonsoft.Json.JsonIgnore]
        public string TopLevelName { get; set; }

        // Whether there is any folder metadata field in the template, whether or not it can be searched
        public bool HasFields()
        {
            return TopLevelName != null || Levels.Count > 0;
        }

        // The terms can be used only if metadata has been created for at least one folder at a level that can be searched
        public bool CanBeUsed()
        {
            return Levels.Any(level => level.HasMetadata);
        }

        public bool IsInUse()
        {
            return UseFolderMetadata && Levels.Any(level => level.SearchTerms.Any(term => term.UseForSearching));
        }

        public void ClearUses()
        {
            UseFolderMetadata = false;
            foreach (SearchTerm term in Levels.SelectMany(level => level.SearchTerms))
            {
                term.UseForSearching = false;
            }
        }

        // A level without metadata is disabled in the user interface, so nothing there should stay in use
        public void ClearUsesWithoutMetadata()
        {
            foreach (SearchTerm term in Levels.Where(level => false == level.HasMetadata).SelectMany(level => level.SearchTerms))
            {
                term.UseForSearching = false;
            }
            UseFolderMetadata = UseFolderMetadata && CanBeUsed();
        }

        // Create the search terms for a level's controls, set up in the same way as CustomSelection does for the other search terms.
        public void AddLevel(int level, string alias, IEnumerable<MetadataControlRow> controls, bool hasMetadata)
        {
            FolderLevelSearchTerms levelTerms = new() { Level = level, Alias = alias, HasMetadata = hasMetadata };
            foreach (MetadataControlRow control in controls)
            {
                SearchTerm term = new()
                {
                    ControlType = control.Type,
                    DataLabel = control.DataLabel,
                    DatabaseValue = control.DefaultValue,
                    Operator = SearchTermOperator.Equal,
                    Label = control.Label,
                    List = control.Type is Control.FixedChoice or Control.MultiChoice
                        ? Choices.ChoicesFromJson(control.List).ChoiceList
                        : [],
                    UseForSearching = false
                };
                if (term.List.Count > 0)
                {
                    // Add the empty string to the beginning of the search list, which allows the option of searching for empty items
                    term.List.Insert(0, string.Empty);
                }

                if (control.Type is Control.Counter or Control.IntegerAny or Control.IntegerPositive or Control.DecimalAny or Control.DecimalPositive)
                {
                    term.DatabaseValue = "0";
                    term.Operator = SearchTermOperator.GreaterThan;
                }
                else if (control.Type == Control.Flag)
                {
                    term.DatabaseValue = BooleanValue.False;
                }
                levelTerms.SearchTerms.Add(term);
            }
            Levels.Add(levelTerms);
        }

        // Keep whatever is still valid from the saved settings. As this selection's terms reflect the current folder template,
        // a saved term whose field was removed, or whose type or choices changed, is just left out.
        public void KeepSettingsFrom(FolderMetadataSelection saved)
        {
            if (saved?.Levels == null)
            {
                return;
            }
            UseFolderMetadata = saved.UseFolderMetadata;
            CombiningOperator = saved.CombiningOperator;
            foreach (FolderLevelSearchTerms level in Levels)
            {
                FolderLevelSearchTerms savedLevel = saved.Levels.Find(item => item.Level == level.Level);
                if (savedLevel?.SearchTerms == null)
                {
                    continue;
                }
                foreach (SearchTerm term in level.SearchTerms)
                {
                    SearchTerm savedTerm = savedLevel.SearchTerms.Find(item => item.DataLabel == term.DataLabel && item.ControlType == term.ControlType);
                    if (savedTerm != null && IsValueInChoices(term, savedTerm.DatabaseValue))
                    {
                        term.UseForSearching = savedTerm.UseForSearching;
                        term.Operator = savedTerm.Operator;
                        term.DatabaseValue = savedTerm.DatabaseValue;
                    }
                }
            }
            ClearUsesWithoutMetadata();
        }

        private static bool IsValueInChoices(SearchTerm term, string value)
        {
            return term.ControlType switch
            {
                Control.FixedChoice => term.List.Contains(value ?? string.Empty),
                Control.MultiChoice => string.IsNullOrEmpty(value) || value.Split(',').All(term.List.Contains),
                _ => true
            };
        }
    }

    // The search terms for one folder level
    public class FolderLevelSearchTerms
    {
        public int Level { get; set; }
        public string Alias { get; set; }

        // Whether metadata has been created for at least one folder at this level
        [Newtonsoft.Json.JsonIgnore]
        public bool HasMetadata { get; set; }

        public List<SearchTerm> SearchTerms { get; set; } = [];
    }
}
