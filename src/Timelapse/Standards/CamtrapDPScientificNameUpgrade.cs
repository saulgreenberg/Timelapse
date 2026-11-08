using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.Database;
using Timelapse.DataStructures;
using Timelapse.DebuggingSupport;
using Timelapse.Dialog;
using Timelapse.Util;
using TimelapseTemplateEditor.Dialog;
using File = System.IO.File;

namespace Timelapse.Standards
{
    /// <summary>
    /// Earlier CamtrapDP templates made the scientific name a Note, while it is now a Choice.
    /// This finds a template, or a data file's copy of it, that still has the Note, and with the user's agreement
    /// changes both to a Choice whose menu has the scientific names already entered.
    /// It works on the files themselves, before Timelapse compares the template with the data file, so the two still match afterwards.
    /// </summary>
    public static class CamtrapDPScientificNameUpgrade
    {
        // How many names are checked in GBIF at once
        private const int GbifChecksAtOnce = 4;

        // Ask the user and, if they agree, update the template and the data file (if it exists). Returns true if anything was updated.
        // Before anything is changed, the menu's names are checked in GBIF, and the user can cancel if some aren't scientific names.
        public static async Task<bool> TryUpgradeAsync(Window owner, string templateFilePath, string dataFilePath)
        {
            try
            {
                if (false == IsCamtrapDPTemplate(templateFilePath))
                {
                    return false;
                }
                bool hasDataFile = false == string.IsNullOrEmpty(dataFilePath) && File.Exists(dataFilePath) && new FileInfo(dataFilePath).Length > 0;
                SQLiteWrapper template = new(templateFilePath);
                SQLiteWrapper data = hasDataFile ? new SQLiteWrapper(dataFilePath) : null;
                DataRow templateRow = ScientificNameControl(template);
                DataRow dataRow = data == null ? null : ScientificNameControl(data);
                if (templateRow == null || (false == IsNote(templateRow) && (dataRow == null || false == IsNote(dataRow))))
                {
                    return false;
                }

                // The menu keeps any choices either already has, and adds the names entered in the data file.
                // A name entered with different capitals appears once, preferring a capitalized genus, e.g., Alces alces rather than alces alces.
                List<string> enteredNames = data == null ? [] : EnteredScientificNames(data);
                List<string> names = [.. ExistingChoices(templateRow).Concat(ExistingChoices(dataRow)).Concat(enteredNames)
                    .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.FirstOrDefault(name => char.IsUpper(name[0])) ?? group.First())
                    .Order(StringComparer.CurrentCultureIgnoreCase)];

                if (true != Dialogs.CamtrapDPUpdateScientificNameToChoiceDialog(owner, hasDataFile, enteredNames.Distinct(StringComparer.OrdinalIgnoreCase).Count()))
                {
                    return false;
                }
                if (false == await NamesAreAcceptedAsync(owner, names))
                {
                    return false;
                }

                FileBackup.TryCreateBackup(templateFilePath);
                string listJson = new Choices(string.Join(Environment.NewLine, names), true).GetAsJson;
                SetToChoice(template, listJson);
                if (data != null && dataRow != null)
                {
                    FileBackup.TryCreateBackup(dataFilePath);
                    SetToChoice(data, listJson);
                }
                return true;
            }
            catch (Exception exception)
            {
                // Leaving the field as a Note is safe, so a problem here shouldn't stop the template or data file from opening
                AppLog.Error("CamtrapDPScientificNameUpgrade: could not check or update the scientific name field.", exception);
                return false;
            }
        }

        #region Private helpers
        // Checks each name in GBIF, and asks the user whether to go on if some have no match there, or if GBIF can't be reached
        private static async Task<bool> NamesAreAcceptedAsync(Window owner, List<string> names)
        {
            if (names.Count == 0)
            {
                return true;
            }
            List<string> unmatched;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                unmatched = await NamesWithoutGbifMatchAsync(names);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or Newtonsoft.Json.JsonException)
            {
                Mouse.OverrideCursor = null;
                return true == Dialogs.CamtrapDPScientificNamesNotCheckedDialog(owner, ex.Message);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
            return unmatched.Count == 0 || true == Dialogs.CamtrapDPScientificNamesNotMatchedDialog(owner, unmatched);
        }

        // The names GBIF has no taxon for. A qualifier such as sp. is left out, so Ursus sp. is checked as the genus Ursus.
        // A synonym counts as a match, as it is still a scientific name.
        private static async Task<List<string>> NamesWithoutGbifMatchAsync(List<string> names)
        {
            using SemaphoreSlim checksAllowed = new(GbifChecksAtOnce);
            bool[] matched = await Task.WhenAll(names.Select(async name =>
            {
                // ReSharper disable once AccessToDisposedClosure
                await checksAllowed.WaitAsync();
                try
                {
                    return await GbifSpeciesLookup.FindExactNameAsync(GbifSpeciesLookup.WithoutQualifier(name), null, null, null) != null;
                }
                finally
                {
                    // ReSharper disable once AccessToDisposedClosure
                    checksAllowed.Release();
                }
            }));
            return [.. names.Where((_, i) => false == matched[i])];
        }

        private static bool IsCamtrapDPTemplate(string templateFilePath)
        {
            if (string.IsNullOrEmpty(templateFilePath) || false == File.Exists(templateFilePath))
            {
                return false;
            }
            SQLiteWrapper template = new(templateFilePath);
            if (false == template.TableExists(DBTables.TemplateInfo) || false == template.SchemaIsColumnInTable(DBTables.TemplateInfo, DatabaseColumn.Standard))
            {
                return false;
            }
            DataTable table = template.GetDataTableFromSelect($"{Sql.Select}{DatabaseColumn.Standard}{Sql.From}{DBTables.TemplateInfo}");
            return table?.Rows.Count > 0 && table.Rows[0][DatabaseColumn.Standard] as string == Constant.Standards.CamtrapDPStandard;
        }

        private static DataRow ScientificNameControl(SQLiteWrapper database)
        {
            if (false == database.TableExists(DBTables.Template))
            {
                return null;
            }
            DataTable table = database.GetDataTableFromSelect(
                $"{Sql.SelectStarFrom}{DBTables.Template}{Sql.Where}{Control.DataLabel}{Sql.Equal}{Sql.Quote(CamtrapDPConstants.Observations.ScientificName)}");
            return table?.Rows.Count == 1 ? table.Rows[0] : null;
        }

        private static bool IsNote(DataRow controlRow)
        {
            return controlRow[Control.Type] as string == Control.Note;
        }

        private static IEnumerable<string> ExistingChoices(DataRow controlRow)
        {
            return controlRow == null || IsNote(controlRow) || controlRow[Control.List] is not string list || string.IsNullOrWhiteSpace(list)
                ? []
                : Choices.ChoicesFromJson(list).ChoiceList;
        }

        // The different scientific names entered in the data file
        private static List<string> EnteredScientificNames(SQLiteWrapper data)
        {
            string column = CamtrapDPConstants.Observations.ScientificName;
            if (false == data.TableExists(DBTables.FileData) || false == data.SchemaIsColumnInTable(DBTables.FileData, column))
            {
                return [];
            }
            DataTable table = data.GetDataTableFromSelect($"{Sql.SelectDistinct}{column}{Sql.From}{DBTables.FileData}");
            return table == null
                ? []
                : [.. table.Rows.Cast<DataRow>()
                    .Select(row => (row[0] as string)?.Trim())
                    .Where(name => false == string.IsNullOrEmpty(name))
                    .Distinct()];
        }

        private static void SetToChoice(SQLiteWrapper database, string listJson)
        {
            ColumnTuplesWithWhere update = new(
                [new(Control.Type, Control.FixedChoice), new(Control.List, listJson)],
                new ColumnTuple(Control.DataLabel, CamtrapDPConstants.Observations.ScientificName));
            SqlOperationResult result = database.Update(DBTables.Template, update);
            if (false == result.Success)
            {
                throw new InvalidOperationException(result.ErrorMessage);
            }
        }
        #endregion
    }
}
