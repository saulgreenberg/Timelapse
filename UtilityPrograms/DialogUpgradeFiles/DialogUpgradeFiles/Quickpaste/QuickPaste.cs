using DialogUpgradeFiles.Database;
using DialogUpgradeFiles.Util;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace DialogUpgradeFiles.QuickPaste
{
    // The QuickPasteOperations  class provides static utility functions for creating and altering quick paste entries.
    public static class QuickPasteOperations
    {
        #region Public Methods - QuickPaste Import from DB
        public static List<QuickPasteEntry> QuickPasteImportFromDB(FileDatabase fileDatabase, string ddbFile)
        {
            string xml = FileDatabase.TryGetQuickPasteXMLFromDatabase(ddbFile);
            if (string.IsNullOrEmpty(xml.Trim()))
            {
                return new List<QuickPasteEntry>();
            }
            else
            {
                return QuickPasteOperations.QuickPasteEntriesFromXML(fileDatabase, xml);
            }
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
                string value = fileDatabase.FileTable[rowIndex].GetValueDisplayString(row.DataLabel) ?? string.Empty;
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

        #region Public Methods - Encode/Decode quickpaste entries into XML
        // Transform the QuickPasteEntries data structure into an XML document that can will eventually be saved as a string in the ImageSetTable database
        public static string QuickPasteEntriesToXML(List<QuickPasteEntry> quickPasteEntries)
        {
            XDocument xDocument = new XDocument(new XElement("Entries",
                quickPasteEntries.Select(i => new XElement("Entry",
                     new XElement("Title", i.Title),
                        i.Items.Select(v => new XElement("Item",
                            new XElement("Label", v.Label),
                            new XElement("DataLabel", v.DataLabel),
                            new XElement("Value", (v.Value == null) ? string.Empty : v.Value.ToString()),
                            new XElement("Use", v.Use.ToString()),
                            new XElement("ControlType", v.ControlType.ToString())))))));
            return xDocument.ToString();
        }

        // Transform the XML string (stored in the ImageSetTable) into a QuickPasteEntries data structure 
        // Compare it to the actual controls, and alter the data structure if needed
        public static List<QuickPasteEntry> QuickPasteEntriesFromXML(FileDatabase fileDatabase, string xml)
        {
            List<QuickPasteEntry> quickPasteEntries = new List<QuickPasteEntry>();
            // Check the arguments for null 
            if (fileDatabase == null)
            {
                // this should not happen
                return quickPasteEntries;
                // Not sure if the above return is effective. We could do the following instead
                // throw new ArgumentNullException(nameof(fileDatabase));
            }

            XDocument xDocument = XDocument.Parse(xml);

            IEnumerable entries =
                from r in xDocument.Descendants("Entry")
                select new QuickPasteEntry
                {
                    Title = (string)r.Element("Title"),
                    Items = (from v in r.Elements("Item")
                             select new QuickPasteItem
                             {
                                 DataLabel = (string)v.Element("DataLabel"),
                                 Label = (string)v.Element("Label"),
                                 Value = (string)v.Element("Value"),
                                 Use = (bool)v.Element("Use"),
                                 ControlType = (string)v.Element("ControlType")
                             }).ToList()
                };


            foreach (QuickPasteEntry quickPasteEntry in entries)
            {
                quickPasteEntries.Add(quickPasteEntry);
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
                                string value = (row.Type == Constant.Control.Flag) ? "False" : string.Empty;
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
