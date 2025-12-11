using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using Timelapse.Constant;
using Timelapse.DataStructures;
using TimelapseTemplateEditor.Controls;
using TimelapseTemplateEditor.ControlsMetadata;
using TimelapseTemplateEditor.EditorCode;
using TimelapseTemplateEditor.Standards;
using Control = Timelapse.Constant.Control;

namespace TimelapseTemplateEditor
{
    public partial class TemplateEditorWindow
    {
        private void DoCreateMetadataStandardFields(List<StandardsRow> metadataRowList, List<StandardsRow> imageTemplateRows, Dictionary<int, string> aliases)
        {
            if (null == Globals.TemplateDatabase)
            {
                return;
            }
            Mouse.OverrideCursor = Cursors.Wait;
            List<ColumnTuplesWithWhere> infoColumnsTuplesWithWhereList = []; // holds columns which have changed for the current control
            List<ColumnTuplesWithWhere> rowsColumnsTuplesWithWhereList = []; // holds columns which have changed for the current control

            //
            // Add the folder metadata fields
            //
            MetadataUI.RemoveAllMetadataLevelTabs();

            int currentLevel = 0;
            long id = 1;

            foreach (StandardsRow sr in metadataRowList)
            {
                if (currentLevel != sr.Level)
                {
                    // a new level. As we are doing this from scratch (i.e., no info rows) we have to add GUIDS as 
                    // all entries will be inserted rather than updated
                    if (aliases.TryGetValue(sr.Level, out var alias))
                    {
                        Globals.TemplateDatabase.UpsertMetadataInfoTableRow(sr.Level, guid: Guid.NewGuid().ToString(), alias: alias);
                    }

                    currentLevel = sr.Level;

                    // We are now on a new level, so create it
                    MetadataUI.CreateNewTabLevelIfNeeded(sr.Level);
                }

                // Add a row of a particular type, with its default values
                MetadataEditRowsControl.DoAddNewRow(sr.Level, sr.Type);

                // Update the row to the values specified in the standard
                // Also create the where condition with the ID (which starts at 1 and is incremented by 1)
                List<ColumnTuple> columnTupleList =
                [
                    new(Control.DefaultValue, sr.DefaultValue),
                    new(Control.Label, sr.Label),
                    new(Control.DataLabel, sr.DataLabel),
                    new(Control.Tooltip, sr.Tooltip),
                    new(Control.Visible, sr.Visible)
                ];
                if (null != sr.Choice)
                {
                    columnTupleList.Add(new(Control.List, sr.Choice));
                }
                infoColumnsTuplesWithWhereList.Add(new(columnTupleList, id++));
            }
            // Now add it to the database
            Globals.TemplateDatabase.Database.Update(DBTables.MetadataTemplate, infoColumnsTuplesWithWhereList);

            // Refresh the metadata controls based on the new database contents
            Globals.TemplateDatabase.LoadMetadataControlsAndInfoFromTemplateDBSortedByControlOrder();

            //
            // Add the image template data fields
            //
            //infoColumnsTuplesWithWhereList.Clear();
            id = 1 + Globals.TemplateDatabase.Database.ScalarGetMaxValueAsLong(DBTables.Template, DatabaseColumn.ID);
            foreach (StandardsRow sr in imageTemplateRows)
            {
                // Add a row of a particular type, with its default values
                TemplateEditRowsControlNew.DoAddNewRow(sr.Type);

                // Update the row to the values specified in the standard
                // Also create the where condition with the ID (which starts at 1 and is incremented by 1)
                List<ColumnTuple> columnTupleList =
                [
                    new(Control.DefaultValue, sr.DefaultValue),
                    new(Control.Label, sr.Label),
                    new(Control.DataLabel, sr.DataLabel),
                    new(Control.Tooltip, sr.Tooltip),
                    new(Control.Copyable, sr.Copyable.ToString()),
                    new(Control.Visible, sr.Visible.ToString())
                ];
                if (null != sr.Choice)
                {
                    columnTupleList.Add(new(Control.List, sr.Choice));
                }

                rowsColumnsTuplesWithWhereList.Add(new(columnTupleList, id++));
            }
            Globals.TemplateDatabase.Database.Update(DBTables.Template, rowsColumnsTuplesWithWhereList);

            // Refresh the metadata controls based on the new database contents
            Globals.TemplateDatabase.LoadControlsFromTemplateDBSortedByControlOrder();

            // Update the data entry previews so they reflects the current values in the database
            Globals.TemplateDataEntryPreviewPanelControl.GeneratePreviewControls(Globals.TemplateUI.TemplateDataEntryPreviewPanel.ControlsPanel, Globals.TemplateDatabase.Controls);
            Globals.TemplateSpreadsheet.GenerateSpreadsheet();

            // Ensure the metadata preview panels are all scrolled to the top (which matches the datagrid scrolling position)
            if (null != Globals.Root?.MetadataUI?.MetadataTabs?.Items)
            {
                for (int i = 1; i < Globals.Root.MetadataUI.MetadataTabs.Items.Count; i++)
                {
                    if (Globals.Root.MetadataUI.MetadataTabs.Items[i] is TabItem { Content: MetadataTabControl metadataTabControl })
                    {
                        metadataTabControl.MetadataDataEntryPreviewPanel?.ScrollIntoViewFirstRow();
                    }
                }
            }

            // Switch to the metadata pane
            // Globals.Root.MetadataPane.IsSelected = true;
            Mouse.OverrideCursor = null;
        }

        // Given a CamtrapDP Json file return a list of rows, 
        // TODO: PROBLEM WITH ENUM AS IT A KEYWORD
        // TODO: PROBLEM WITH DUPLICATE FIELDS
        // TODO: We can likely delete this, but unsure yet
        List<StandardsRow> GetStandardRowsFromCamtrapDPJson(JsonMetadataTemplate template, bool richTypes, List<string> usedDataLabels)
        {

            int uniqueNumber = 1;
            List<StandardsRow> rows = [];
            foreach (field field in template.fields)
            {
                string type;
                string choices = null;
                if (field.constraints.@enum is { Count: > 0 })
                {
                    type = Timelapse.Constant.Control.FixedChoice;
                }
                else
                {
                    switch (field.type)
                    {
                        case "boolean":
                            type = Timelapse.Constant.Control.Flag;
                            break;
                        case "integer":
                            type = richTypes ? Timelapse.Constant.Control.IntegerAny : Control.Note;
                            break;
                        case "number":
                            type = richTypes ? Timelapse.Constant.Control.DecimalAny : Control.Note;
                            break;
                        //case "string":
                        //case "datetime":
                        default:
                            type = Timelapse.Constant.Control.Note;
                            break;
                    }
                }

                string datalabel = field.name ?? "unknownLabel" + uniqueNumber++;
                if (usedDataLabels.Contains(datalabel))
                {
                    // THIS IS PROBLEMATIC FOR NOW. 
                    continue;
                }
                usedDataLabels.Add(datalabel);
                string label = CreateLabel(datalabel);

                string contraintline = string.Empty;
                string tooltip = field.description ?? "No description available.";
                if (!string.IsNullOrWhiteSpace(field.constraints.minimum))
                {
                    contraintline += $"Minimum: {field.constraints.minimum} ";
                }
                if (!string.IsNullOrWhiteSpace(field.constraints.maximum))
                {
                    contraintline += $"Maximum: {field.constraints.maximum} ";
                }
                if (!string.IsNullOrWhiteSpace(field.constraints.pattern))
                {
                    contraintline += $"Pattern: {field.constraints.maximum} ";
                }
                if (field.constraints.unique is true)
                {
                    contraintline += $"Unique: {field.constraints.unique} ";
                }
                if (!string.IsNullOrWhiteSpace(contraintline))
                {
                    tooltip += $"{Environment.NewLine}{contraintline}";
                }

                if (null != field.constraints.required)
                {
                    string requiredLine = true == field.constraints.required ? "Required" : "Optional";
                    tooltip += $"{Environment.NewLine}{requiredLine}";
                }

                if (type == Timelapse.Constant.Control.FixedChoice || type == Timelapse.Constant.Control.MultiChoice)
                {
                    choices = StandardsBase.CreateChoiceList(true, field.constraints.@enum);
                }

                if (null != field.example)
                {
                    tooltip += $"{Environment.NewLine}e.g., \"{field.example}\"";
                }
                StandardsRow row = new(type, 1, "", label, datalabel, tooltip, choices);
                rows.Add(row);
            }
            return rows;
        }

    }
}
