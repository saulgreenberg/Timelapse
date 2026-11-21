using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using Timelapse.Constant;
using Timelapse.DebuggingSupport;
using Timelapse.Recognition;
using Timelapse.Util;

namespace Timelapse.Database
{
    public partial class FileDatabase
    {
        // This file repairs databases where a recognition file that numbers its detection categories from 0 (e.g., 0 = animal) was imported.
        // Timelapse reserves detection category 0 for Empty, so those detections were mixed up with the empties.
        // Two situations are repaired:
        //  - The import was never merged: DetectionCategories still has 0 = <label other than Empty>.
        //    Every real detection and every detection category is shifted up by 1, and 0 = Empty is added.
        //  - A later merge already replaced 0's label with Empty: real detections of files not in the merged file are stranded at 0.
        //    Their label is looked for in the database's backups. If no backup has it, they are moved to a new 'Unknown (probably animal)' category,
        //    as 0 = animal is only a common convention.
        // Timelapse's own Empty rows (category 0, confidence 0, no bounding box) are left at 0 in both cases.

        // A message for the user describing the repair, or null if no repair was done
        public string DetectionCategoriesRepairReport { get; private set; }

        // The category for stranded detections whose original category can't be found
        private const string UnknownZeroLabel = "Unknown (probably animal)";

        // A real detection, as opposed to the Empty row Timelapse adds for a file without detections
        private static readonly string WhereRealDetection =
            $" NOT ({DetectionColumns.Conf} = 0 AND ({DetectionColumns.BBox} IS NULL OR {DetectionColumns.BBox} = '')) ";

        private void RepairZeroBasedDetectionCategoriesIfNeeded()
        {
            try
            {
                if (false == Database.TableExists(DBTables.DetectionCategories) || false == Database.TableExists(DBTables.Detections))
                {
                    return;
                }

                // The category table is tiny, so reading it costs nothing
                Dictionary<string, string> categories = [];
                using (DataTable dataTable = Database.GetDataTableFromSelect(Sql.SelectStarFrom + DBTables.DetectionCategories))
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        // Convert.ToString is declared as possibly returning null (it doesn't for a DataRow value, as DBNull becomes ""),
                        // and a dictionary key can't be null
                        categories[Convert.ToString(row[DetectionCategoriesColumns.Category], CultureInfo.InvariantCulture) ?? string.Empty] =
                            Convert.ToString(row[DetectionCategoriesColumns.Label], CultureInfo.InvariantCulture) ?? string.Empty;
                    }
                }
                if (categories.Keys.Any(key => false == int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
                {
                    // Not something we know how to repair
                    return;
                }

                if (Recognizer.IsZeroBased(categories))
                {
                    RepairUnmergedZeroBasedDetections(categories);
                }
                else
                {
                    RepairStrandedZeroDetections(categories);
                }
            }
            catch (Exception ex)
            {
                AppLog.Warning("RepairZeroBasedDetectionCategoriesIfNeeded: the repair failed, so the database was left as it was.", ex);
            }
        }

        // DetectionCategories still has 0 = <label>: shift everything up by 1
        private void RepairUnmergedZeroBasedDetections(Dictionary<string, string> categories)
        {
            Dictionary<int, string> shiftedCategories = categories.ToDictionary(
                kvp => int.Parse(kvp.Key, NumberStyles.None, CultureInfo.InvariantCulture) + 1,
                kvp => kvp.Value);
            shiftedCategories[0] = RecognizerValues.EmptyDetectionLabel;

            // category is the primary key of DetectionCategories, so an in-place 'category + 1' could collide (0 -> 1 while 1 still exists).
            // Instead, delete the rows and insert the shifted ones. Detections.category has no such constraint.
            List<string> statements =
            [
                $"UPDATE {DBTables.Detections} SET {DetectionColumns.Category} = {DetectionColumns.Category} + 1 WHERE {WhereRealDetection}",
                .. ReplaceDetectionCategoriesStatements(shiftedCategories)
            ];

            // A saved Custom Selection of a detection category has to follow its category
            string searchTermsStatement = ShiftSavedDetectionCategoryStatement();
            if (searchTermsStatement != null)
            {
                statements.Add(searchTermsStatement);
            }

            if (false == BackupAndExecute(statements))
            {
                return;
            }

            List<string> mapping = categories
                .OrderBy(kvp => int.Parse(kvp.Key, NumberStyles.None, CultureInfo.InvariantCulture))
                .Select(kvp => $"{kvp.Key} {kvp.Value} -> {int.Parse(kvp.Key, NumberStyles.None, CultureInfo.InvariantCulture) + 1} {kvp.Value}")
                .ToList();
            DetectionCategoriesRepairReport =
                "The recognitions in this image set were imported from a file that numbers its detection categories from 0. " +
                "Timelapse reserves 0 for Empty, so those detections were mixed up with the empty images. " +
                "Every detection category was moved up by 1:" + Environment.NewLine +
                string.Join(Environment.NewLine, mapping);
            AppLog.Warning($"RepairZeroBasedDetectionCategoriesIfNeeded: {FilePath}: shifted 0-based detection categories ({string.Join("; ", mapping)}).");
        }

        // DetectionCategories has 0 = Empty, but real detections may have been stranded at 0 by a merge after a 0-based import.
        // Note: this is a full scan of Detections (category has no index), but EXISTS stops at the first stranded detection found.
        private void RepairStrandedZeroDetections(Dictionary<string, string> categories)
        {
            string whereStranded = $" {DetectionColumns.Category} IN (0, '0') AND {WhereRealDetection}";
            string exists = Database.ScalarGetScalarFromSelectAsString($"SELECT EXISTS (SELECT 1 FROM {DBTables.Detections} WHERE {whereStranded})");
            if (exists != "1")
            {
                return;
            }

            // The merge replaced 0's label with Empty. A backup made before the merge may still have it.
            // If not, use a category whose name says it isn't known: 0 = animal is only a common convention.
            string backupLabel = FindZeroLabelInBackups();
            string label = backupLabel ?? UnknownZeroLabel;

            Dictionary<int, string> newCategories = categories.ToDictionary(kvp => int.Parse(kvp.Key, NumberStyles.None, CultureInfo.InvariantCulture), kvp => kvp.Value);
            newCategories[0] = RecognizerValues.EmptyDetectionLabel;
            int newCategory = newCategories.FirstOrDefault(kvp => kvp.Key != 0 && string.Equals(kvp.Value, label, StringComparison.OrdinalIgnoreCase)).Key;
            if (newCategory == 0)
            {
                newCategory = newCategories.Keys.Max() + 1;
                newCategories[newCategory] = label;
            }

            string count = Database.ScalarGetScalarFromSelectAsString($"SELECT COUNT(*) FROM {DBTables.Detections} WHERE {whereStranded}");
            List<string> statements =
            [
                $"UPDATE {DBTables.Detections} SET {DetectionColumns.Category} = {newCategory} WHERE {whereStranded}",
                .. ReplaceDetectionCategoriesStatements(newCategories)
            ];
            if (false == BackupAndExecute(statements))
            {
                return;
            }

            string intro = $"{count} detections in this image set had lost their detection category. " +
                           "This happens when a recognition file that numbers its detection categories from 0 was imported, and another recognition file was merged into it later.";
            DetectionCategoriesRepairReport = backupLabel != null
                ? intro + Environment.NewLine +
                  $"A backup from before the merge shows they were '{backupLabel}', so they were moved back to the '{newCategories[newCategory]}' category."
                : intro + Environment.NewLine +
                  $"Their original category could not be determined, so they were moved to a new category, '{label}'." + Environment.NewLine +
                  $"To see what they are, use Custom Selection, select Recognitions, and choose '{label}'." + Environment.NewLine +
                  "If you still have the original recognition file, re-importing it with Replace gives them their correct category.";
            AppLog.Warning($"RepairZeroBasedDetectionCategoriesIfNeeded: {FilePath}: moved {count} stranded category 0 detections to category {newCategory} ({newCategories[newCategory]}){(backupLabel != null ? ", label found in a backup" : string.Empty)}.");
        }

        // Look for the label of a 0-based category 0 in this database's backups, newest first. Returns null if none has one.
        // Only each backup's tiny DetectionCategories table is read, and the backups are opened read-only.
        private string FindZeroLabelInBackups()
        {
            try
            {
                DirectoryInfo backupFolder = new(Path.Combine(Path.GetDirectoryName(FilePath) ?? string.Empty, Constant.File.BackupFolder));
                if (false == backupFolder.Exists)
                {
                    return null;
                }
                // Backups are named <name>.<timestamp>.ddb or <name>.Checkpoint-<reason>.<timestamp>.ddb
                string prefix = Path.GetFileNameWithoutExtension(FilePath) + ".";
                IEnumerable<FileInfo> backups = backupFolder.GetFiles("*" + Path.GetExtension(FilePath))
                    .Where(file => file.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(file => file.LastWriteTime);
                foreach (FileInfo backup in backups)
                {
                    string label = ReadZeroLabel(backup.FullName);
                    if (label != null)
                    {
                        return label;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Warning("FindZeroLabelInBackups: could not search the backups (non-critical).", ex);
            }
            return null;
        }

        // The label of category 0 in that database, if it isn't Empty; otherwise null
        private static string ReadZeroLabel(string path)
        {
            try
            {
                SQLiteConnectionStringBuilder builder = new() { DataSource = path, ReadOnly = true, FailIfMissing = true };
                // Created through SQLiteWrapper, as all connections are, so the SQLite Console can show its statement
                using SQLiteConnection connection = SQLiteWrapper.GetNewSqliteConnection(builder.ConnectionString);
                connection.Open();
                using SQLiteCommand command = new(
                    $"SELECT {DetectionCategoriesColumns.Label} FROM {DBTables.DetectionCategories} WHERE {DetectionCategoriesColumns.Category} IN (0, '0')", connection);
                string label = command.ExecuteScalar() as string;
                return string.IsNullOrWhiteSpace(label) || string.Equals(label, RecognizerValues.EmptyDetectionLabel, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : label;
            }
            catch (SQLiteException)
            {
                // e.g., an older backup without recognitions, or an unreadable file
                return null;
            }
        }

        private static List<string> ReplaceDetectionCategoriesStatements(Dictionary<int, string> categories)
        {
            List<string> statements = [$"DELETE FROM {DBTables.DetectionCategories}"];
            foreach (KeyValuePair<int, string> kvp in categories.OrderBy(kvp => kvp.Key))
            {
                statements.Add($"INSERT INTO {DBTables.DetectionCategories} ({DetectionCategoriesColumns.Category}, {DetectionCategoriesColumns.Label}) " +
                               $"VALUES ({Sql.Quote(kvp.Key.ToString(CultureInfo.InvariantCulture))}, {Sql.Quote(kvp.Value)})");
            }
            return statements;
        }

        // Returns the statement that shifts the detection category of the saved Custom Selection up by 1, or null if there is nothing to shift
        private string ShiftSavedDetectionCategoryStatement()
        {
            if (false == Database.SchemaIsColumnInTable(DBTables.ImageSet, DatabaseColumn.SearchTerms))
            {
                return null;
            }
            string searchTerms = Database.ScalarGetScalarFromSelectAsString($"SELECT {DatabaseColumn.SearchTerms} FROM {DBTables.ImageSet}");
            if (string.IsNullOrWhiteSpace(searchTerms))
            {
                return null;
            }
            try
            {
                JObject customSelection = JObject.Parse(searchTerms);
                if (customSelection["RecognitionSelections"] is not JObject recognitionSelections
                    || false == int.TryParse(recognitionSelections["DetectionCategoryNumber"]?.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out int category)
                    || category == 0)
                {
                    // Missing, All (-1), or Empty (0). The selector never offers the 0-based category 0, so a saved 0 can only mean Empty
                    return null;
                }
                recognitionSelections["DetectionCategoryNumber"] = (category + 1).ToString(CultureInfo.InvariantCulture);
                return $"UPDATE {DBTables.ImageSet} SET {DatabaseColumn.SearchTerms} = {Sql.Quote(customSelection.ToString(Formatting.Indented))}";
            }
            catch (JsonException)
            {
                // Unreadable saved selection: Timelapse falls back to the default selection anyway
                return null;
            }
        }

        // Back up the database as a checkpoint file, then run the statements in a single transaction
        private bool BackupAndExecute(List<string> statements)
        {
            FileBackup.TryCreateBackup(Path.GetDirectoryName(FilePath), Path.GetFileName(FilePath), false, "pre-detection-category-repair");
            mostRecentBackup = DateTime.Now;

            SqlOperationResult result = Database.ExecuteNonQueryWithRollback(statements);
            if (false == result.Success)
            {
                AppLog.Warning($"RepairZeroBasedDetectionCategoriesIfNeeded: the repair was rolled back, so the database was left as it was. {result.ErrorMessage}");
                return false;
            }
            detectionCategoriesDictionary = null;
            detectionDataTable = null;
            return true;
        }
    }
}
