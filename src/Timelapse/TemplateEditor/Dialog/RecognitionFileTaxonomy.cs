using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Timelapse;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.Standards;
using ConstantFile = Timelapse.Constant.File;

namespace TimelapseTemplateEditor.Dialog
{
    // Why two definitions need comparing
    public enum DefinitionConflict
    {
        // A row in the list and a definition in the file are for the same taxon, but their contents differ
        SameTaxon,

        // The file's definition has already been compared with one row in the list, and this is another row for the same taxon
        AnotherRow,

        // A row in the list and a definition in the file have the same common name but different scientific names
        SameCommonName,

        // Two definitions in the file share a name
        FileSameScientificName,
        FileSameCommonName,

        // Two rows in the list share a name
        ListSameScientificName,
        ListSameCommonName
    }

    public enum DefinitionChoice
    {
        KeepFirst,
        KeepSecond,
        KeepBoth
    }

    // Two conflicting definitions and what the user chose to do about them. If one is in the list and the other in the file, the list's is first.
    public class DefinitionConflictItem
    {
        public taxonomic First { get; init; }
        public taxonomic Second { get; init; }
        public DefinitionConflict Conflict { get; init; }
        public DefinitionChoice Choice { get; set; } = DefinitionChoice.KeepFirst;
        public bool IsListVersusFile => Conflict is DefinitionConflict.SameTaxon or DefinitionConflict.AnotherRow or DefinitionConflict.SameCommonName;
    }

    // The taxonomic information in a recognition file, a Timelapse database or an exported species list, and how to turn it into taxonomic definitions
    public class RecognitionFileTaxonomy
    {
        private const string CategoriesProperty = "classification_categories";
        private const string DescriptionsProperty = "classification_category_descriptions";
        private const int DescriptionPartsWithIdentifier = 7;
        private const string ImagesProperty = "images";

        // How far back to look for a section after the images section. This is far more than these sections need.
        private const int EndOfFileSearchSize = 4 * 1024 * 1024;

        public Dictionary<string, string> Categories { get; private init; } = [];
        public Dictionary<string, string> Descriptions { get; private init; } = [];


        // The definitions in an exported species list, which are complete as is
        public List<taxonomic> ExportedDefinitions { get; private init; } = [];

        public bool HasExportedDefinitions => ExportedDefinitions.Count > 0;
        public bool HasDescriptions => Descriptions.Values.Any(description => false == string.IsNullOrWhiteSpace(description));
        public bool HasCategories => Categories.Values.Any(category => false == string.IsNullOrWhiteSpace(category));

        #region Reading the file
        public static bool IsDatabaseFile(string filePath)
        {
            return string.Equals(Path.GetExtension(filePath), ConstantFile.FileDatabaseFileExtension, StringComparison.OrdinalIgnoreCase);
        }

        // The definitions in a species list file, or null if the file isn't a species list, i.e., a json array of
        // definitions that each have a scientific or common name. It stops at the first token if the file is something else.
        public static List<taxonomic> ReadSpeciesList(string filePath)
        {
            try
            {
                using StreamReader streamReader = new(filePath);
                using JsonTextReader reader = new(streamReader);
                if (false == reader.Read() || reader.TokenType != JsonToken.StartArray)
                {
                    return null;
                }
                List<taxonomic> definitions = JsonSerializer.CreateDefault().Deserialize<List<taxonomic>>(reader);
                return definitions is { Count: > 0 } && definitions.TrueForAll(definition =>
                    definition != null && (false == string.IsNullOrWhiteSpace(definition.scientificName) || false == string.IsNullOrWhiteSpace(CommonName(definition))))
                    ? definitions
                    : null;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        // The file's taxonomic information, which is empty if the file doesn't have any.
        // Throws SQLiteException, JsonException or an IOException / UnauthorizedAccessException if the file can't be read,
        // or an OperationCanceledException if cancelled.
        public static RecognitionFileTaxonomy Read(string filePath, CancellationToken cancellationToken = default)
        {
            return IsDatabaseFile(filePath) ? ReadDatabaseFile(filePath) : ReadRecognitionFile(filePath, cancellationToken);
        }

        // The database is opened as read only, and directly rather than through SQLiteWrapper,
        // as a wrapper's read failures would look the same as a database without any classification categories.
        private static RecognitionFileTaxonomy ReadDatabaseFile(string filePath)
        {
            SQLiteConnectionStringBuilder connectionString = new() { DataSource = filePath, ReadOnly = true };
            using SQLiteConnection connection = SQLiteWrapper.GetNewSqliteConnection(connectionString.ConnectionString);
            connection.Open();

            using SQLiteCommand tableExists = new(Sql.SelectNameFromSqliteMasterWhereTypeEqualTableAndNameEquals + Sql.Quote(DBTables.ClassificationCategories) + Sql.Semicolon, connection);
            if (tableExists.ExecuteScalar() == null)
            {
                return new();
            }

            using SQLiteCommand select = new(Sql.SelectStarFrom + DBTables.ClassificationCategories, connection);
            using SQLiteDataReader reader = select.ExecuteReader();
            using DataTable table = new();
            table.Load(reader);

            Dictionary<string, string> categories = [];
            Dictionary<string, string> descriptions = [];
            foreach (DataRow row in table.Rows)
            {
                string classification = ColumnValue(row, ClassificationCategoriesColumns.Category);
                categories[classification] = ColumnValue(row, ClassificationCategoriesColumns.Label);
                descriptions[classification] = ColumnValue(row, ClassificationCategoriesColumns.Description);
            }
            return new() { Categories = categories, Descriptions = descriptions };
        }

        private static string ColumnValue(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) ? Convert.ToString(row[column]) : string.Empty;
        }

        // Recognition files can be very large, but nearly all of that is the images section, which isn't needed.
        // The taxonomic sections are either before or after it, so this reads up to the images section
        // and then, for any section not yet found, looks at the end of the file.
        // Throws JsonException or an IOException / UnauthorizedAccessException if the file can't be read.
        private static RecognitionFileTaxonomy ReadRecognitionFile(string filePath, CancellationToken cancellationToken)
        {
            Dictionary<string, string> categories = null;
            Dictionary<string, string> descriptions = null;
            bool isAtImages = false;

            using (StreamReader streamReader = new(filePath))
            using (JsonTextReader reader = new(streamReader))
            {
                if (false == reader.Read())
                {
                    return new();
                }

                // An exported species list is an array of definitions, while a recognition file is an object
                if (reader.TokenType == JsonToken.StartArray)
                {
                    List<taxonomic> exportedDefinitions = JsonSerializer.CreateDefault().Deserialize<List<taxonomic>>(reader);
                    return new() { ExportedDefinitions = [.. exportedDefinitions.Where(definition => definition != null && HasContent(definition))] };
                }
                if (reader.TokenType != JsonToken.StartObject)
                {
                    return new();
                }

                while ((categories == null || descriptions == null) && reader.Read() && reader.TokenType == JsonToken.PropertyName)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string propertyName = (string)reader.Value;
                    isAtImages = propertyName == ImagesProperty;
                    if (isAtImages)
                    {
                        break;
                    }
                    reader.Read();
                    switch (propertyName)
                    {
                        case CategoriesProperty:
                            categories = ReadStringValues(reader);
                            break;
                        case DescriptionsProperty:
                            descriptions = ReadStringValues(reader);
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }

                // Reading all the way through without finding the images section should end at the end of the object
                bool isFinishedEarly = isAtImages || (categories != null && descriptions != null);
                if (false == isFinishedEarly && (reader.TokenType != JsonToken.EndObject || reader.Depth != 0))
                {
                    throw new JsonReaderException("The file ends before its contents are complete.");
                }
            }

            if (isAtImages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                categories ??= ReadSectionNearEndOfFile(filePath, CategoriesProperty);
                descriptions ??= ReadSectionNearEndOfFile(filePath, DescriptionsProperty);
            }
            return new() { Categories = categories ?? [], Descriptions = descriptions ?? [] };
        }

        // The named section, or null if it isn't near the end of the file.
        // A quote in a json text value is always escaped, so text in the images section can't be mistaken for the section's name.
        private static Dictionary<string, string> ReadSectionNearEndOfFile(string filePath, string sectionName)
        {
            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            long start = Math.Max(0, stream.Length - EndOfFileSearchSize);
            stream.Seek(start, SeekOrigin.Begin);
            byte[] endOfFile = new byte[stream.Length - start];
            stream.ReadExactly(endOfFile);

            int sectionStart = LastIndexOfSectionStart(endOfFile, sectionName);
            if (sectionStart < 0)
            {
                return null;
            }
            stream.Seek(start + sectionStart, SeekOrigin.Begin);
            using StreamReader streamReader = new(stream);
            using JsonTextReader reader = new(streamReader);
            reader.Read();
            return ReadStringValues(reader);
        }

        // Where the object starts in "sectionName" : { ... }, or -1 if it isn't there
        private static int LastIndexOfSectionStart(byte[] bytes, string sectionName)
        {
            byte[] name = Encoding.UTF8.GetBytes($"\"{sectionName}\"");
            int end = bytes.Length;
            while (end > 0)
            {
                int nameStart = bytes.AsSpan(0, end).LastIndexOf(name);
                if (nameStart < 0)
                {
                    return -1;
                }
                end = nameStart;

                int position = SkipWhiteSpace(bytes, nameStart + name.Length);
                if (position < bytes.Length && bytes[position] == ':' && (nameStart == 0 || bytes[nameStart - 1] != '\\'))
                {
                    position = SkipWhiteSpace(bytes, position + 1);
                    if (position < bytes.Length && bytes[position] == '{')
                    {
                        return position;
                    }
                }
            }
            return -1;
        }

        private static int SkipWhiteSpace(byte[] bytes, int position)
        {
            while (position < bytes.Length && bytes[position] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            {
                position++;
            }
            return position;
        }

        private static Dictionary<string, string> ReadStringValues(JsonReader reader)
        {
            return JToken.ReadFrom(reader) is JObject values
                ? values.Properties()
                    .Where(property => property.Value.Type == JTokenType.String)
                    .ToDictionary(property => property.Name, property => (string)property.Value)
                : [];
        }
        #endregion

        #region Creating definitions from the descriptions
        // Each description has a key that may match a category. A category without a description only has its common name.
        public List<taxonomic> DefinitionsFromDescriptions()
        {
            List<taxonomic> definitions = [];
            foreach (KeyValuePair<string, string> description in Descriptions)
            {
                Categories.TryGetValue(description.Key, out string category);
                taxonomic definition = DefinitionFromDescription(description.Value, category);
                if (HasContent(definition))
                {
                    definitions.Add(definition);
                }
            }
            foreach (KeyValuePair<string, string> category in Categories.Where(category => false == Descriptions.ContainsKey(category.Key)))
            {
                taxonomic definition = DefinitionFromDescription(null, category.Value);
                if (HasContent(definition))
                {
                    definitions.Add(definition);
                }
            }
            return definitions;
        }

        // A description is semicolon-separated, ending with the common name and working up the taxonomy, i.e.,
        //   identifier;class;order;family;genus;species;common name
        // The identifier is ignored. It's a guid in a recognition file and usually a name in a database, e.g.,
        //   ba76d46e-25de-45e2-90a8-bd279b650f7c;mammalia;carnivora;felidae;lynx;rufus;bobcat
        //   bobcat;mammalia;carnivora;felidae;lynx;rufus;bobcat
        // Any part may be empty. The species part is only the second word of the scientific name, so it is combined with the genus.
        public static taxonomic DefinitionFromDescription(string description, string defaultCommonName = null)
        {
            List<string> parts = [.. (description ?? string.Empty).Trim().Trim('"').Split(';').Select(part => part.Trim())];
            if (parts.Count == DescriptionPartsWithIdentifier || Guid.TryParse(parts[0], out _))
            {
                parts.RemoveAt(0);
            }
            string PartFromEnd(int position) => parts.Count >= position ? parts[^position] : string.Empty;

            string commonName = PartFromEnd(1);
            string species = PartFromEnd(2);
            string genus = Capitalized(PartFromEnd(3));
            string scientificName = species.Length == 0
                ? null
                : genus != null && false == species.Contains(' ') ? $"{genus} {species}" : Capitalized(species);

            return new()
            {
                vernacularNames = NewVernacularNames(commonName.Length > 0 ? commonName : defaultCommonName),
                scientificName = scientificName,
                genus = genus,
                family = Capitalized(PartFromEnd(4)),
                order = Capitalized(PartFromEnd(5)),
                class_ = Capitalized(PartFromEnd(6)),
                phylum = Capitalized(PartFromEnd(7)),
                kingdom = Capitalized(PartFromEnd(8))
            };
        }

        public static Dictionary<string, string> NewVernacularNames(string commonName)
        {
            return string.IsNullOrWhiteSpace(commonName)
                ? null
                : new() { [GbifSpeciesLookup.CommonNameLanguageCode] = commonName.Trim() };
        }

        private static string Capitalized(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : char.ToUpperInvariant(value[0]) + value[1..];
        }
        #endregion

        #region Comparing and merging definitions
        // Definitions that are the same in every field add nothing, so only the first is kept
        public static List<taxonomic> WithoutExactDuplicates(IEnumerable<taxonomic> definitions)
        {
            List<taxonomic> unique = [];
            foreach (taxonomic definition in definitions)
            {
                if (false == unique.Exists(other => HasSameContents(other, definition)))
                {
                    unique.Add(definition);
                }
            }
            return unique;
        }

        // Definitions that share a scientific name or a common name, where each is compared with the first to have that name.
        // Both are kept unless the user chooses otherwise.
        private static List<DefinitionConflictItem> FindSameNameConflicts(List<taxonomic> definitions, DefinitionConflict sameScientificName, DefinitionConflict sameCommonName)
        {
            List<DefinitionConflictItem> conflicts = [];
            void Find(DefinitionConflict conflict, Func<taxonomic, string> getName)
            {
                foreach (IGrouping<string, taxonomic> group in definitions.GroupBy(definition => getName(definition).Trim(), StringComparer.OrdinalIgnoreCase).Where(group => group.Key.Length > 0))
                {
                    foreach (taxonomic other in group.Skip(1).Where(other => false == conflicts.Exists(item => item.First == group.First() && item.Second == other)))
                    {
                        conflicts.Add(new() { First = group.First(), Second = other, Conflict = conflict, Choice = DefinitionChoice.KeepBoth });
                    }
                }
            }
            Find(sameScientificName, definition => GbifSpeciesLookup.WithoutQualifier((definition.scientificName ?? string.Empty).Trim()));
            Find(sameCommonName, CommonName);
            return conflicts;
        }

        public static List<DefinitionConflictItem> FindFileConflicts(List<taxonomic> newDefinitions)
        {
            return FindSameNameConflicts(newDefinitions, DefinitionConflict.FileSameScientificName, DefinitionConflict.FileSameCommonName);
        }

        // Rows for a taxon that are already being compared with a new definition, as another row for that taxon, aren't asked about twice
        public static List<DefinitionConflictItem> FindListConflicts(List<taxonomic> list, List<DefinitionConflictItem> listVersusFileConflicts)
        {
            List<DefinitionConflictItem> conflicts = FindSameNameConflicts(list, DefinitionConflict.ListSameScientificName, DefinitionConflict.ListSameCommonName);
            conflicts.RemoveAll(item => item.Conflict == DefinitionConflict.ListSameScientificName
                && listVersusFileConflicts.Exists(other => other.Conflict == DefinitionConflict.AnotherRow && (other.First == item.First || other.First == item.Second)));
            return conflicts;
        }

        // The list that results from the user's choices. If replacing the list, only the conflicts within the file apply.
        public static List<taxonomic> ApplyChoices(List<taxonomic> list, List<taxonomic> newDefinitions, bool replaceList,
            List<DefinitionConflictItem> listVersusFileConflicts, List<DefinitionConflictItem> fileConflicts, List<DefinitionConflictItem> listConflicts)
        {
            HashSet<taxonomic> removedDefinitions = RemovedDefinitions(fileConflicts);
            List<taxonomic> remainingDefinitions = newDefinitions.FindAll(definition => false == removedDefinitions.Contains(definition));
            if (replaceList)
            {
                return remainingDefinitions;
            }
            HashSet<taxonomic> removedRows = RemovedDefinitions(listConflicts);
            List<taxonomic> remainingRows = list.FindAll(row => false == removedRows.Contains(row));
            Merge(remainingRows, remainingDefinitions, listVersusFileConflicts);
            return remainingRows;
        }

        private static HashSet<taxonomic> RemovedDefinitions(List<DefinitionConflictItem> conflicts)
        {
            HashSet<taxonomic> removed = [];
            foreach (DefinitionConflictItem item in conflicts)
            {
                if (item.Choice == DefinitionChoice.KeepFirst)
                {
                    removed.Add(item.Second);
                }
                else if (item.Choice == DefinitionChoice.KeepSecond)
                {
                    removed.Add(item.First);
                }
            }
            return removed;
        }

        // Add each new definition to the list. A definition already in the list isn't added again.
        // New definitions are only compared with the rows originally in the list, as FindFileConflicts compares them with each other.
        // If it conflicts with any rows in the list, choose(row, newDefinition, conflict) decides what to do for each of those rows:
        // - KeepFirst leaves the row as is,
        // - KeepSecond puts the new definition in the row's place, or removes the row if the new definition has already been placed in another row,
        // - KeepBoth leaves the row as is, and adds the new definition as its own row.
        // The new definition isn't added if all its conflicts were resolved by KeepFirst.
        public static void Merge(List<taxonomic> list, IEnumerable<taxonomic> newDefinitions, Func<taxonomic, taxonomic, DefinitionConflict, DefinitionChoice> choose)
        {
            List<taxonomic> originalRows = [.. list];
            foreach (taxonomic newDefinition in newDefinitions)
            {
                List<taxonomic> sameTaxonRows = originalRows.FindAll(row => IsSameTaxon(row, newDefinition));
                if (sameTaxonRows.Exists(row => HasSameContents(row, newDefinition)))
                {
                    continue;
                }
                List<taxonomic> sameCommonNameRows = originalRows.FindAll(row => HasSameCommonNameOnly(row, newDefinition));
                if (sameTaxonRows.Count == 0 && sameCommonNameRows.Count == 0)
                {
                    list.Add(newDefinition);
                    continue;
                }

                bool isPlaced = false;
                bool addAsOwnRow = false;
                void Resolve(taxonomic row, DefinitionConflict conflict)
                {
                    switch (choose(row, newDefinition, conflict))
                    {
                        case DefinitionChoice.KeepSecond when isPlaced:
                            list.Remove(row);
                            break;
                        case DefinitionChoice.KeepSecond:
                            // Another new definition may have already taken this row's place
                            if (list.Contains(row))
                            {
                                list[list.IndexOf(row)] = newDefinition;
                            }
                            else
                            {
                                list.Add(newDefinition);
                            }
                            isPlaced = true;
                            break;
                        case DefinitionChoice.KeepBoth:
                            addAsOwnRow = true;
                            break;
                    }
                }

                for (int i = 0; i < sameTaxonRows.Count; i++)
                {
                    Resolve(sameTaxonRows[i], i == 0 ? DefinitionConflict.SameTaxon : DefinitionConflict.AnotherRow);
                }
                sameCommonNameRows.ForEach(row => Resolve(row, DefinitionConflict.SameCommonName));

                if (addAsOwnRow && false == isPlaced)
                {
                    list.Add(newDefinition);
                }
            }
        }

        // The conflicts that merging would have to resolve, in the order they arise
        public static List<DefinitionConflictItem> FindConflicts(List<taxonomic> list, IEnumerable<taxonomic> newDefinitions)
        {
            List<DefinitionConflictItem> conflicts = [];
            Merge([.. list], newDefinitions, (row, newDefinition, conflict) =>
            {
                conflicts.Add(new() { First = row, Second = newDefinition, Conflict = conflict });
                return DefinitionChoice.KeepFirst;
            });
            return conflicts;
        }

        // Merge using the choices made for those conflicts
        public static void Merge(List<taxonomic> list, IEnumerable<taxonomic> newDefinitions, List<DefinitionConflictItem> conflicts)
        {
            Merge(list, newDefinitions, (row, newDefinition, _) =>
                conflicts.Find(item => item.First == row && item.Second == newDefinition)?.Choice ?? DefinitionChoice.KeepFirst);
        }

        // Definitions are for the same taxon if their scientific names match.
        // If one or both don't have a scientific name, it's whether their common names match.
        public static bool IsSameTaxon(taxonomic first, taxonomic second)
        {
            string firstName = GbifSpeciesLookup.WithoutQualifier((first.scientificName ?? string.Empty).Trim());
            string secondName = GbifSpeciesLookup.WithoutQualifier((second.scientificName ?? string.Empty).Trim());
            if (firstName.Length > 0 && secondName.Length > 0)
            {
                return AreSame(firstName, secondName);
            }
            string firstCommonName = CommonName(first);
            return firstCommonName.Length > 0 && AreSame(firstCommonName, CommonName(second));
        }

        // Whether both have scientific names that differ, but the same common name
        private static bool HasSameCommonNameOnly(taxonomic first, taxonomic second)
        {
            return false == string.IsNullOrWhiteSpace(first.scientificName) && false == string.IsNullOrWhiteSpace(second.scientificName)
                && false == IsSameTaxon(first, second)
                && CommonName(first).Trim().Length > 0 && AreSame(CommonName(first), CommonName(second));
        }

        public static bool HasSameContents(taxonomic first, taxonomic second)
        {
            return AreSame(first.scientificName, second.scientificName) && AreSame(first.taxonID, second.taxonID)
                && AreSame(first.taxonRank, second.taxonRank) && AreSame(first.kingdom, second.kingdom)
                && AreSame(first.phylum, second.phylum) && AreSame(first.class_, second.class_)
                && AreSame(first.order, second.order) && AreSame(first.family, second.family)
                && AreSame(first.genus, second.genus) && AreSame(CommonName(first), CommonName(second));
        }

        public static bool HasContent(taxonomic definition)
        {
            return false == string.IsNullOrWhiteSpace(definition.scientificName) || false == string.IsNullOrWhiteSpace(definition.taxonID)
                || false == string.IsNullOrWhiteSpace(definition.taxonRank) || false == string.IsNullOrWhiteSpace(definition.kingdom)
                || false == string.IsNullOrWhiteSpace(definition.phylum) || false == string.IsNullOrWhiteSpace(definition.class_)
                || false == string.IsNullOrWhiteSpace(definition.order) || false == string.IsNullOrWhiteSpace(definition.family)
                || false == string.IsNullOrWhiteSpace(definition.genus) || definition.vernacularNames?.Count > 0;
        }

        // The names of the fields that define a taxon, in the same order as FieldValues
        public static readonly string[] FieldNames = ["Common name", "Scientific name", "Taxonomic rank", "Kingdom", "Phylum", "Class", "Order", "Family", "Genus", "Taxon id"];

        public static string[] FieldValues(taxonomic definition)
        {
            return
            [
                CommonName(definition), definition.scientificName, definition.taxonRank, definition.kingdom, definition.phylum,
                definition.class_, definition.order, definition.family, definition.genus, definition.taxonID
            ];
        }

        public static string CommonName(taxonomic definition)
        {
            return definition.vernacularNames != null && definition.vernacularNames.TryGetValue(GbifSpeciesLookup.CommonNameLanguageCode, out string commonName)
                ? commonName ?? string.Empty
                : string.Empty;
        }

        // Case differences (e.g., gray wolf vs Gray Wolf), surrounding spaces, and empty vs missing values don't count as differences
        public static bool AreSame(string first, string second)
        {
            return string.Equals((first ?? string.Empty).Trim(), (second ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
        }
        #endregion
    }
}
