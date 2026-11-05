using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Timelapse.Controls;
using Timelapse.Dialog;

namespace TimelapseTemplateEditor.Dialog
{
    /// <summary>
    /// A dialog that looks up taxa in GBIF, showing a busy indicator with a cancel button during each lookup.
    /// </summary>
    public class GbifLookupDialogWindow : BusyableDialogWindow
    {
        private BusyCancelIndicator gbifBusyIndicator;

        public GbifLookupDialogWindow(Window owner) : base(owner)
        {
        }

        // This must be invoked by the parent in Loaded
        protected void InitializeGbifLookups(BusyCancelIndicator busyCancelIndicator)
        {
            gbifBusyIndicator = busyCancelIndicator;
            InitalizeProgressHandler(busyCancelIndicator);
        }

        // Do the lookup, asking the user to choose or confirm if GBIF doesn't return a clear match.
        // Returns null if there is no match to use, after telling the user why if needed.
        protected async Task<GbifSpeciesMatch> ResolveGbifMatchAsync(string name, Func<CancellationToken, Task<GbifLookupResult>> lookup)
        {
            GbifLookupResult result = await TryGbifRequestAsync(name, lookup);
            if (result == null)
            {
                return null;
            }

            if (result.Candidates.Count > 0)
            {
                // There is no clear single match, so let the user choose
                SelectGbifCandidate selectCandidate = new(this, name, result.Candidates);
                return true == selectCandidate.ShowDialog()
                    ? await TryGbifRequestAsync(selectCandidate.SelectedCandidate.Name, token => GbifSpeciesLookup.GetMatchAsync(selectCandidate.SelectedCandidate.Key, token))
                    : null;
            }

            GbifSpeciesMatch match = result.Match;
            if (match == null)
            {
                Dialogs.EditorSpeciesListGbifNoMatchDialog(this, name);
                return null;
            }

            return match.IsExactMatch || true == Dialogs.EditorSpeciesListGbifUseClosestMatchDialog(this, name, match.ScientificName, match.TaxonRank, match.IsSynonym)
                ? match
                : null;
        }

        // Show a busy indicator with a cancel button during the request, as it can be slow.
        // Returns null if the user cancelled, or if the request failed (after telling the user).
        private async Task<T> TryGbifRequestAsync<T>(string name, Func<CancellationToken, Task<T>> request) where T : class
        {
            TokenReset();
            Progress.Report(new(0, $"Looking up {name} in GBIF...", true, true));
            gbifBusyIndicator.IsBusy = true;
            WindowCloseButtonIsEnabled(false);
            string failureReason = null;
            T result = null;
            try
            {
                result = await request(Token);
            }
            catch (OperationCanceledException) when (Token.IsCancellationRequested)
            {
                // The user cancelled the lookup, so nothing changes
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                failureReason = ex.Message;
            }
            WindowCloseButtonIsEnabled(true);
            gbifBusyIndicator.IsBusy = false;
            if (failureReason != null)
            {
                Dialogs.EditorSpeciesListGbifNotAvailableDialog(this, failureReason);
            }
            return result;
        }
    }
}
