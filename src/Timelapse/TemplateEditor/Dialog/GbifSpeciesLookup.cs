using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TimelapseTemplateEditor.Dialog
{
    // The taxonomic information GBIF returns for a scientific name
    public class GbifSpeciesMatch
    {
        public string ScientificName { get; init; }
        public string TaxonID { get; init; }
        public string TaxonRank { get; init; }
        public string Kingdom { get; init; }
        public string Phylum { get; init; }
        public string Class { get; init; }
        public string Order { get; init; }
        public string Family { get; init; }
        public string Genus { get; init; }
        public string CommonName { get; init; }

        // The name searched for is a synonym, so this match is for GBIF's accepted name
        public bool IsSynonym { get; init; }

        // False if GBIF returned a different name, e.g., a corrected spelling
        public bool IsExactMatch { get; init; }
    }

    // A possible taxon for the user to choose from
    public class GbifCandidate
    {
        public string Key { get; init; }
        public string Name { get; init; }
        public string Rank { get; init; }
        public string Status { get; init; }
        public string Family { get; init; }
        public string Kingdom { get; init; }
    }

    // A lookup's outcome: candidates for the user to choose from if there are any, otherwise the match (null if none)
    public class GbifLookupResult
    {
        public GbifSpeciesMatch Match { get; init; }
        public List<GbifCandidate> Candidates { get; init; } = [];
    }

    // Looks up species in the GBIF species API, which does not require an account or key
    public static class GbifSpeciesLookup
    {
        public const string CommonNameLanguageCode = "eng";
        public const string KingdomRank = "KINGDOM";
        public const string PhylumRank = "PHYLUM";
        public const string ClassRank = "CLASS";
        public const string OrderRank = "ORDER";
        public const string FamilyRank = "FAMILY";
        public const string GenusRank = "GENUS";

        private const string MatchUrl = "https://api.gbif.org/v2/species/match?verbose=true&scientificName=";
        private const string RankMatchUrl = "https://api.gbif.org/v2/species/match?verbose=true&taxonRank={0}&scientificName=";

        // API v2 only offers name matching, so everything else uses v1
        private const string SpeciesUrl = "https://api.gbif.org/v1/species/";
        private const string SuggestUrl = "https://api.gbif.org/v1/species/suggest?limit=50&datasetKey=d7dddbf4-2cf0-4f39-9b2a-bb099caae36c&q=";
        private const string SpeciesPageUrl = "https://www.gbif.org/species/";
        private const string CommonNameSearchUrl = "https://api.gbif.org/v1/species/search?qField=VERNACULAR&status=ACCEPTED&limit=100&datasetKey=d7dddbf4-2cf0-4f39-9b2a-bb099caae36c&q=";
        private const int CommonNameSearchPageSize = 100;
        private const int CommonNameSearchMaxResults = 1000;
        private const string ExactMatchType = "EXACT";

        private static readonly string[] RanksAboveSpecies = ["KINGDOM", "PHYLUM", "CLASS", "ORDER", "FAMILY", "SUBFAMILY", "TRIBE", "GENUS", "SUBGENUS"];
        private const string GenusQualifier = " sp.";
        private const string AboveGenusQualifier = " indet.";
        private const string SpeciesRank = "SPECIES";

        // In lower case, as in GbifSpeciesMatch.TaxonRank
        private static readonly string[] RanksAboveGenus =
        [
            "kingdom", "subkingdom", "phylum", "subphylum", "superclass", "class", "subclass", "infraclass",
            "superorder", "order", "suborder", "infraorder", "superfamily", "family", "subfamily", "tribe", "subtribe"
        ];
        private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

        // These all throw HttpRequestException, TaskCanceledException (timeout or cancelled) or JsonException if GBIF can't be reached or understood.
        public static async Task<GbifLookupResult> LookupAsync(string scientificName, CancellationToken cancellationToken = default)
        {
            JObject result = await GetJsonAsync(MatchUrl + Uri.EscapeDataString(scientificName), cancellationToken);
            if (result["usage"] is not JObject usage)
            {
                // No single match. Either several taxa share this name, or it may be just the start of a name
                List<GbifCandidate> candidates = ExactAlternatives(result);
                return new() { Candidates = candidates.Count > 0 ? candidates : await SuggestAsync(scientificName, cancellationToken) };
            }

            // The classification of a synonym is that of its accepted name, so use the accepted name throughout
            bool isSynonym = result["acceptedUsage"] is JObject;
            JObject acceptedUsage = isSynonym ? (JObject)result["acceptedUsage"] : usage;
            bool isExactMatch = false == isSynonym &&
                                (string.Equals((string)usage["canonicalName"], scientificName, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals((string)usage["name"], scientificName, StringComparison.OrdinalIgnoreCase));

            // Matching a genus (say) likely means the user wants to choose something within it.
            if (RanksAboveSpecies.Contains((string)acceptedUsage["rank"]))
            {
                // If only the first part of the name matched, the name may just be incomplete, e.g., Ursus ar
                List<GbifCandidate> candidates = isExactMatch ? [] : await SuggestAsync(scientificName, cancellationToken);
                if (candidates.Count == 0)
                {
                    candidates = await ChildrenWithParentAsync(result, acceptedUsage, cancellationToken);
                }
                if (candidates.Count > 0)
                {
                    return new() { Candidates = candidates };
                }
            }

            return new() { Match = await MatchFromUsageAsync(result, acceptedUsage, isSynonym, isExactMatch, cancellationToken) };
        }

        // As above, but for a name at a given rank, e.g., a genus or family.
        // Candidates are only offered if GBIF can't identify a single taxon at that rank.
        public static async Task<GbifLookupResult> LookupAtRankAsync(string name, string rank, CancellationToken cancellationToken = default)
        {
            // GBIF's matching at a given rank is case sensitive, and names at these ranks are capitalized
            name = char.ToUpperInvariant(name[0]) + name[1..].ToLowerInvariant();
            JObject result = await GetJsonAsync(string.Format(RankMatchUrl, rank) + Uri.EscapeDataString(name), cancellationToken);
            if (result["usage"] is JObject usage && (string)usage["rank"] == rank)
            {
                bool isSynonym = result["acceptedUsage"] is JObject;
                bool isExactMatch = false == isSynonym && string.Equals((string)usage["canonicalName"], name, StringComparison.OrdinalIgnoreCase);
                return new() { Match = await MatchFromUsageAsync(result, isSynonym ? (JObject)result["acceptedUsage"] : usage, isSynonym, isExactMatch, cancellationToken) };
            }

            // Several taxa may share this name. Otherwise it may be misspelled, or just the start of a name
            List<GbifCandidate> candidates = Alternatives(result, rank, true);
            if (candidates.Count == 0)
            {
                List<GbifCandidate> similarNames = Alternatives(result, rank, false);
                similarNames.AddRange((await SuggestAsync(name, cancellationToken)).Where(candidate => candidate.Rank == Tidy(rank)));
                candidates = Sorted(similarNames);
            }
            return new() { Candidates = candidates };
        }

        private static async Task<GbifSpeciesMatch> MatchFromUsageAsync(JObject result, JObject acceptedUsage, bool isSynonym, bool isExactMatch, CancellationToken cancellationToken)
        {
            string key = (string)acceptedUsage["key"];
            return new()
            {
                ScientificName = (string)acceptedUsage["canonicalName"],
                TaxonID = SpeciesPageUrl + key,
                TaxonRank = ((string)acceptedUsage["rank"])?.ToLowerInvariant(),
                Kingdom = NameAtRank(result, "KINGDOM"),
                Phylum = NameAtRank(result, "PHYLUM"),
                Class = NameAtRank(result, "CLASS"),
                Order = NameAtRank(result, "ORDER"),
                Family = NameAtRank(result, FamilyRank),
                Genus = NameAtRank(result, GenusRank),
                CommonName = await TryGetCommonNameAsync(key, cancellationToken),
                IsSynonym = isSynonym,
                IsExactMatch = isExactMatch
            };
        }

        // Get the details of a candidate the user chose. If it is a synonym, this is its accepted name.
        public static async Task<GbifSpeciesMatch> GetMatchAsync(string key, CancellationToken cancellationToken = default)
        {
            JObject species = await GetJsonAsync(SpeciesUrl + key, cancellationToken);
            if ((string)species["acceptedKey"] is { } acceptedKey && acceptedKey != key)
            {
                key = acceptedKey;
                species = await GetJsonAsync(SpeciesUrl + key, cancellationToken);
            }
            return new()
            {
                ScientificName = (string)species["canonicalName"],
                TaxonID = SpeciesPageUrl + key,
                TaxonRank = ((string)species["rank"])?.ToLowerInvariant(),
                Kingdom = (string)species["kingdom"],
                Phylum = (string)species["phylum"],
                Class = (string)species["class"],
                Order = (string)species["order"],
                Family = (string)species["family"],
                Genus = (string)species["genus"],
                CommonName = await TryGetCommonNameAsync(key, cancellationToken),
                IsExactMatch = true
            };
        }

        // Find exactly this name, returning only what identifies it (the id, rank, kingdom and phylum), or null if it isn't found.
        // The rank (null if unknown) and any class and family narrow the search, which also separates taxa that share a name.
        // A synonym is still found, and its own id and rank are returned as the definition keeps that name.
        public static async Task<GbifSpeciesMatch> FindExactNameAsync(string name, string rank, string classification, string family, CancellationToken cancellationToken = default)
        {
            string url = MatchUrl + Uri.EscapeDataString(name);
            url += rank == null ? string.Empty : "&taxonRank=" + rank;
            url += string.IsNullOrWhiteSpace(classification) ? string.Empty : "&class=" + Uri.EscapeDataString(classification);
            url += string.IsNullOrWhiteSpace(family) ? string.Empty : "&family=" + Uri.EscapeDataString(family);

            JObject result = await GetJsonAsync(url, cancellationToken);
            if (result["usage"] is not JObject usage
                || false == string.Equals((string)usage["canonicalName"], name, StringComparison.OrdinalIgnoreCase)
                || (rank != null && (string)usage["rank"] != rank))
            {
                return null;
            }
            return new()
            {
                TaxonID = SpeciesPageUrl + (string)usage["key"],
                TaxonRank = ((string)usage["rank"])?.ToLowerInvariant(),
                Kingdom = NameAtRank(result, KingdomRank),
                Phylum = NameAtRank(result, PhylumRank)
            };
        }

        // Find the taxon with this English common name, or null if there isn't one, without asking the user.
        // Several taxa can share a name, e.g., the subspecies of a species. This uses the first of TaxaWithCommonNameAsync.
        public static async Task<GbifSpeciesMatch> FindByCommonNameAsync(string commonName, CancellationToken cancellationToken = default)
        {
            List<JToken> taxa = await TaxaWithCommonNameAsync(commonName, cancellationToken);
            return taxa.Count == 0 ? null : MatchFromSearchResult(taxa[0], commonName);
        }

        // As above, but if several taxa share the name, they are candidates for the user to choose from.
        public static async Task<GbifLookupResult> LookupByCommonNameAsync(string commonName, CancellationToken cancellationToken = default)
        {
            List<JToken> taxa = await TaxaWithCommonNameAsync(commonName, cancellationToken);
            if (taxa.Count < 2)
            {
                return new() { Match = taxa.Count == 0 ? null : MatchFromSearchResult(taxa[0], commonName) };
            }
            return new()
            {
                Candidates = [.. taxa.Select(taxon => new GbifCandidate
                {
                    Key = (string)taxon["nubKey"] ?? (string)taxon["key"],
                    Name = (string)taxon["canonicalName"],
                    Rank = Tidy((string)taxon["rank"]),
                    Status = Tidy((string)taxon["taxonomicStatus"]),
                    Family = (string)taxon["family"],
                    Kingdom = (string)taxon["kingdom"]
                })]
            };
        }

        // The accepted taxa with exactly this name, with the most likely first: species, then those listing the name most often.
        // GBIF's search also returns partial matches (a search for gray wolf includes the Gray Wolf Moth) and doesn't put exact
        // names first (wolf finds Canis lupus around result 250), so this reads through the results.
        private static async Task<List<JToken>> TaxaWithCommonNameAsync(string commonName, CancellationToken cancellationToken)
        {
            List<JToken> taxa = [];
            bool isEndOfRecords = false;
            for (int offset = 0; false == isEndOfRecords && offset < CommonNameSearchMaxResults; offset += CommonNameSearchPageSize)
            {
                JObject result = await GetJsonAsync($"{CommonNameSearchUrl}{Uri.EscapeDataString(commonName)}&offset={offset}", cancellationToken);
                taxa.AddRange(result["results"]?.Where(taxon => CountOfCommonName(taxon, commonName) > 0 && false == string.IsNullOrWhiteSpace((string)taxon["canonicalName"])) ?? []);
                isEndOfRecords = (bool?)result["endOfRecords"] ?? true;
            }
            return [.. taxa
                .OrderBy(taxon => (string)taxon["rank"] == SpeciesRank ? 0 : 1)
                .ThenByDescending(taxon => CountOfCommonName(taxon, commonName))];
        }

        private static GbifSpeciesMatch MatchFromSearchResult(JToken taxon, string commonName)
        {
            return new()
            {
                ScientificName = (string)taxon["canonicalName"],
                TaxonID = SpeciesPageUrl + ((string)taxon["nubKey"] ?? (string)taxon["key"]),
                TaxonRank = ((string)taxon["rank"])?.ToLowerInvariant(),
                Kingdom = (string)taxon["kingdom"],
                Phylum = (string)taxon["phylum"],
                Class = (string)taxon["class"],
                Order = (string)taxon["order"],
                Family = (string)taxon["family"],
                Genus = (string)taxon["genus"],
                CommonName = commonName,
                IsExactMatch = true
            };
        }

        private static int CountOfCommonName(JToken searchResult, string commonName)
        {
            return searchResult["vernacularNames"]?.Count(name =>
                (string)name["language"] is CommonNameLanguageCode or null or "" &&
                string.Equals((string)name["vernacularName"], commonName, StringComparison.OrdinalIgnoreCase)) ?? 0;
        }

        #region Qualifiers
        // Append the qualifier for the rank, if any, e.g., Ursus sp. for a genus and Ursidae indet. for a family
        public static string QualifiedName(string name, string rank)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
            if (rank == Tidy(GenusRank))
            {
                return name + GenusQualifier;
            }
            return RanksAboveGenus.Contains(rank) ? name + AboveGenusQualifier : name;
        }

        // Whether a taxon at this rank (as in GbifSpeciesMatch.TaxonRank) is more general than a species
        public static bool IsAboveSpecies(string rank)
        {
            return rank is "genus" or "subgenus" || RanksAboveGenus.Contains(rank);
        }

        // Remove a qualifier from a name, as GBIF needs the name by itself
        public static string WithoutQualifier(string name)
        {
            foreach (string qualifier in new[] { GenusQualifier, AboveGenusQualifier })
            {
                if (name.EndsWith(qualifier, StringComparison.OrdinalIgnoreCase))
                {
                    return name[..^qualifier.Length].Trim();
                }
            }
            return name;
        }
        #endregion

        #region Candidates
        // The taxa that exactly match the name, for a name shared by several taxa, e.g., the plant and bird genera called Oenanthe
        private static List<GbifCandidate> ExactAlternatives(JObject result)
        {
            return Alternatives(result, null, true);
        }

        // The alternatives GBIF offers, optionally only those of a given rank
        private static List<GbifCandidate> Alternatives(JObject result, string rank, bool exactOnly)
        {
            IEnumerable<GbifCandidate> candidates = result["diagnostics"]?["alternatives"]?
                .Where(item => item["usage"] is JObject)
                .Where(item => false == exactOnly || (string)item["diagnostics"]?["matchType"] == ExactMatchType)
                .Where(item => rank == null || (string)item["usage"]["rank"] == rank)
                .Select(item => new GbifCandidate
                {
                    Key = (string)item["usage"]["key"],
                    Name = (string)item["usage"]["canonicalName"],
                    Rank = Tidy((string)item["usage"]["rank"]),
                    Status = Tidy((string)item["usage"]["status"]),
                    Family = NameAtRank(item, "FAMILY"),
                    Kingdom = NameAtRank(item, "KINGDOM")
                });
            return Sorted(candidates ?? []);
        }

        // The names starting with the given text
        private static async Task<List<GbifCandidate>> SuggestAsync(string text, CancellationToken cancellationToken)
        {
            JArray results = JArray.Parse(await HttpClient.GetStringAsync(SuggestUrl + Uri.EscapeDataString(text), cancellationToken));
            return Sorted(results.Select(CandidateFromV1));
        }

        // The taxa within the matched taxon, following the taxon itself so it can be chosen too
        private static async Task<List<GbifCandidate>> ChildrenWithParentAsync(JObject result, JObject parentUsage, CancellationToken cancellationToken)
        {
            string key = (string)parentUsage["key"];
            JObject children = await GetJsonAsync($"{SpeciesUrl}{key}/children?limit=1000", cancellationToken);
            List<GbifCandidate> candidates = Sorted((children["results"] ?? new JArray()).Select(CandidateFromV1));
            if (candidates.Count > 0)
            {
                candidates.Insert(0, new()
                {
                    Key = key,
                    Name = (string)parentUsage["canonicalName"],
                    Rank = Tidy((string)parentUsage["rank"]),
                    Status = "accepted",
                    Family = NameAtRank(result, "FAMILY"),
                    Kingdom = NameAtRank(result, "KINGDOM")
                });
            }
            return candidates;
        }

        // Each candidate only once, with accepted names first
        private static List<GbifCandidate> Sorted(IEnumerable<GbifCandidate> candidates)
        {
            return candidates
                .Where(candidate => false == string.IsNullOrWhiteSpace(candidate.Key) && false == string.IsNullOrWhiteSpace(candidate.Name))
                .GroupBy(candidate => candidate.Key)
                .Select(group => group.First())
                .OrderBy(candidate => candidate.Status == "accepted" ? 0 : 1)
                .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static GbifCandidate CandidateFromV1(JToken item)
        {
            return new()
            {
                Key = (string)item["key"],
                Name = (string)item["canonicalName"],
                Rank = Tidy((string)item["rank"]),
                Status = Tidy((string)item["status"] ?? (string)item["taxonomicStatus"]),
                Family = (string)item["family"],
                Kingdom = (string)item["kingdom"]
            };
        }

        // Make a GBIF value such as PROVISIONALLY_ACCEPTED suitable for display
        private static string Tidy(string value)
        {
            return value?.Replace('_', ' ').ToLowerInvariant();
        }
        #endregion

        #region Helpers
        private static async Task<JObject> GetJsonAsync(string url, CancellationToken cancellationToken = default)
        {
            return JObject.Parse(await HttpClient.GetStringAsync(url, cancellationToken));
        }

        private static string NameAtRank(JToken result, string rank)
        {
            return result["classification"]?.FirstOrDefault(item => (string)item["rank"] == rank)?["name"]?.ToString();
        }

        // Some common names are listed in all capitals, e.g., BEARS
        private static string TidyCommonName(string name)
        {
            return name == name.ToUpperInvariant()
                ? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.ToLowerInvariant())
                : name;
        }

        // GBIF lists many common names without identifying a main one, so use the most frequently listed English one.
        // The common name is an optional extra, so failing to get it shouldn't lose the rest of the match.
        private static async Task<string> TryGetCommonNameAsync(string key, CancellationToken cancellationToken)
        {
            try
            {
                JObject result = await GetJsonAsync($"{SpeciesUrl}{key}/vernacularNames?limit=1000", cancellationToken);
                return result["results"]?
                    .Where(item => (string)item["language"] == CommonNameLanguageCode)
                    .Select(item => (string)item["vernacularName"])
                    .Where(name => false == string.IsNullOrWhiteSpace(name))
                    .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(group => group.Count())
                    .FirstOrDefault()?
                    .Select(TidyCommonName)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .First();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or Newtonsoft.Json.JsonException && false == cancellationToken.IsCancellationRequested)
            {
                return null;
            }
        }
        #endregion
    }
}
