using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.QuickPaste
{
    // The QuickPasteOperations  class provides static utility functions for creating and altering quick paste entries.
    public static class QuickPasteOperations
    {
        #region Public Methods - QuickPaste Import from DB
        public static List<QuickPasteEntry> QuickPasteImportFromDB(FileDatabase fileDatabase, string ddbFile)
        {
            return QuickPasteOperations.QuickPasteEntriesFromJSON(fileDatabase, FileDatabase.TryGetQuickPasteJSONFromDatabase(ddbFile));
        }
        #endregion

        #region Public Methods - Try Get QuickPaste Item From Field
        // Return a QuickPaste Entry, where its title and each of its items represents a potential pastable control 
        // If there is no valid row to make the entry from, return null
        public static QuickPasteEntry TryGetQuickPasteItemFromDataFields(FileDatabase fileDatabase, int rowIndex, string title)
        {
            // If the row isn't valid, we can't make a quickpaste entry out of it
            // Note that fileDatabase should never be null, but we do want to Check the arguments for null 
            if (fileDatabase == null || fileDatabase.IsFileRowInRange(rowIndex) == false)
            {
                return null;
            }

            // Create a quick paste entry for each non-standard control.
            QuickPasteEntry quickPasteEntry = new QuickPasteEntry()
            {
                Title = title,
                Items = new List<QuickPasteItem>()
            };
            foreach (ControlRow row in fileDatabase.Controls)
            {
                string value = fileDatabase.FileTable[rowIndex].GetValueDisplayString(row.DataLabel);
                if (value == null)
                {
                    value = String.Empty;
                }
                switch (row.Type)
                {
                    // User defined control types are the potential items to paste
                    // 'Use' is initially set to whether the control is copyable
                    case Constant.Control.FixedChoice:
                    case Constant.Control.Note:
                    case Constant.Control.Flag:
                    case Constant.Control.Counter:
                        quickPasteEntry.Items.Add(new QuickPasteItem(
                            row.DataLabel,
                            row.Label,
                            value,
                            row.Copyable,
                            row.Type));
                        break;
                    default:
                        // Standard controls are not used in quick pastes, as it is unlikely the user will want to alter their contents
                        break;
                }
            }
            return quickPasteEntry;
        }
        #endregion

        #region Public Method DeleteQuickPasteEntry
        // Delete the quickPasteEntry from the quickPasteEntries list
        public static List<QuickPasteEntry> DeleteQuickPasteEntry(List<QuickPasteEntry> quickPasteEntries, QuickPasteEntry quickPasteEntry)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(quickPasteEntries, nameof(quickPasteEntries));

            quickPasteEntries.RemoveAll(x => x.Equals(quickPasteEntry));
            return quickPasteEntries;
        }
        #endregion

        #region Public Methods - Decode quickpaste entries into JSON
        // Transform the JSON string (stored in the ImageSetTable) into a QuickPasteEntries data structure robustly
        // Compare it to the actual controls, and alter the data structure as needed, where quickpaste entries only
        // specify existing controls (e.g., a quickpaste entry to a non-existent control will be removed)
        public static List<QuickPasteEntry> QuickPasteEntriesFromJSON(FileDatabase fileDatabase, string quickpasteAsJSON)
        {
            List<QuickPasteEntry> quickPasteEntries = new List<QuickPasteEntry>();
            if (string.IsNullOrEmpty(quickpasteAsJSON.Trim()))
            {
                return quickPasteEntries;
            }

            // Check the arguments for null 
            if (fileDatabase == null)
            {
                // this should not happen
                TracePrint.PrintStackTrace(1);
                return quickPasteEntries;
                // Not sure if the above return is effective. We could do the following instead
                // throw new ArgumentNullException(nameof(fileDatabase));
            }

            try
            {
                quickPasteEntries = JsonConvert.DeserializeObject<List<QuickPasteEntry>>(quickpasteAsJSON);
            }
            catch
            {
                return new List<QuickPasteEntry>();
            }

            quickPasteEntries = CheckAndSyncQuickPasteItemIfNeeded(fileDatabase, quickPasteEntries);
            return quickPasteEntries;
        }
        #endregion

        #region Private Methods- Check And Sync QuickPaste Item If Needed
        // Its possible (albeit rare) that the template has been changed since the quickpaste xml was last saved and the stored 
        // Thus we check for differences between each quickpaste entry's items and the datalabel of the  available controls.
        // If the quickpaste items list has an item that doesn't match a data label, we remove it.
        // If the available controls has a data label that is not matched by a quickpaste item, we create a quickpaste item that matches that (albeit with the non-use flag set, and blank or false values)
        // While not foolproof for large changes, its better than just removing the entire quickpaste entries. 
        private static List<QuickPasteEntry> CheckAndSyncQuickPasteItemIfNeeded(FileDatabase fileDatabase, List<QuickPasteEntry> originalQuickPasteEntries)
        {
            List<QuickPasteEntry> adjustedQuickPasteEntries = new List<QuickPasteEntry>();

            // If a control is not in a  quickpasteEntry Items list, add it to the Items list (although with the USE flag off).
            // If a quickPasteEntry Item does not contain a matching control, then don't add it. 
            foreach (QuickPasteEntry oldQuickPasteEntry in originalQuickPasteEntries)
            {
                bool oneOrMoreItemsCopied = false;
                bool isUsed = false;

                // Create a new entry with the same title as the old entry, and with an empty Items list
                QuickPasteEntry newQuickPasteEntry = new QuickPasteEntry()
                {
                    Title = oldQuickPasteEntry.Title,
                    Items = new List<QuickPasteItem>()
                };
                foreach (ControlRow row in fileDatabase.Controls)
                {
                    bool noItemsMatch = true;
                    switch (row.Type)
                    {
                        // We only consider the non-standard controls as quickpaste candidates
                        case Constant.Control.FixedChoice:
                        case Constant.Control.Note:
                        case Constant.Control.Flag:
                        case Constant.Control.Counter:
                            // Searh the old Items list to see if the control is there. If so, copy it to the new Items list
                            foreach (QuickPasteItem oldItem in oldQuickPasteEntry.Items)
                            {
                                if (row.DataLabel == oldItem.DataLabel)
                                {
                                    // We have a valid quickPasteItem, as it matches a control. So copy that item to the new list
                                    newQuickPasteEntry.Items.Add(new QuickPasteItem(oldItem.DataLabel, oldItem.Label, oldItem.Value, oldItem.Use, row.Type));
                                    noItemsMatch = false;
                                    oneOrMoreItemsCopied = true;
                                    if (oldItem.Use)
                                    {
                                        // At least one item should be used if we are going to copy it over
                                        isUsed = true;
                                    }
                                    break;
                                }
                            }
                            // If we arrive here, it means that we have a control with no matching entry. So we should add that
                            if (noItemsMatch)
                            {
                                string value = (row.Type == Constant.Control.Flag) ? "False" : String.Empty;
                                newQuickPasteEntry.Items.Add(new QuickPasteItem(row.DataLabel, row.Label, value, false, row.Type));
                            }
                            break;
                        default:
                            break;
                    }
                }

                // Add the entry if there is at least one item copied from the old entry to the new entry, and if at least one attribute is marked as 'Used'
                // This avoids importing quickpastes with no user-specified settings or that do nothing. Yes, its a heuristic, but it should work
                // for all but the worst mismatches
                if (oneOrMoreItemsCopied && isUsed)
                {
                    // Add the entry if it has any items in it. 
                    adjustedQuickPasteEntries.Add(newQuickPasteEntry);
                }
            }
            return adjustedQuickPasteEntries;
        }
        #endregion
    }
}
