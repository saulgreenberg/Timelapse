using System;
using System.Collections.Generic;
using Timelapse.Constant;

namespace TimelapseTemplateEditor.Standards
{
    public static class AllControlsStandard
    {
        // The All controls standard specification: a list of StandardsRow, common species (used by one row), and level aliases

        #region The Alias specification associated with each level

        public static Dictionary<int, string> Aliases = new()
        {
            { 1, "Project" },
            { 2, "Station" },
            { 3, "Deployment" },
        };

        #endregion

        public static List<StandardsRow> FolderMetadataRows =
        [
            #region Level 1: Project

            new(
                Control.Note, 1, "Note default", "Note1", "note",
                "An example note.",
                null),

            new(
                Control.MultiLine, 1, "Multiline default", "Multiline1", "multiline",
                $"An example multiline.{Environment.NewLine}• e.g., Second line.",
                null),

            new(
                Control.AlphaNumeric, 1, "AlphaNumeric_Default", "AlphaNumeric1", "alphaNumeric",
                "An example alphaNumeric.",
                null),

            new(
                Control.IntegerAny, 1, "-5", "IntegerAny1", "integerAny",
                "An example IntegerAny.",
                null),

            new(
                Control.IntegerPositive, 1, "6", "IntegerPositive1", "integerPositive",
                "An example IntegerPositive.",
                null),

            new(
                Control.DecimalAny, 1, "-7.56", "DecimalAny1", "decimalAny",
                "An example DecimalAny.",
                null),

            new(
                Control.DecimalPositive, 1, "8.56", "DecimalPositive1", "decimalPositive",
                "An example DecimalPositive.",
                null),

            new(Control.FixedChoice, 1, "Bbb", "FixedChoice1", "FixedChoice1",
                "FixedChoice with blank option true",
                StandardsBase.CreateChoiceList(true, ["Aaa", "Bbb", "Ccc", "Should have a blank option"])),

            new(Control.FixedChoice, 1, "Ccc", "FixedChoice2", "FixedChoice2",
                "FixedChoice with blank option false",
                StandardsBase.CreateChoiceList(false, ["Aaa", "Bbb", "Ccc", "Should not have a blank option"])),

            new(Control.MultiChoice, 1, "Fff", "MultiChoice1", "multiChoice1",
                "Multichoice with blank option true",
                StandardsBase.CreateChoiceList(true, ["Aaa", "Bbb", "Fff", "Configured with a blank option"])),

            new(Control.MultiChoice, 1, "Ggg", "MultiChoice2", "multiChoice2",
                "Multichoice with blank option false",
                StandardsBase.CreateChoiceList(false, ["Aaa", "Bbb", "Ggg", "Configured without a blank option"])),

            new(Control.DateTime_, 1, "", Control.DateTime_, $"{Control.DateTime_}1",
                "DateTime_ - no defaults permitted",
                null),

            new(Control.Date_, 1, "", Control.Date_, $"{Control.Date_}1",
                "Date_ - no defaults permitted",
                null),

            new(Control.Time_, 1, "", Control.Time_, $"{Timelapse.Constant.Control.Time_}1",
                "Time_ - no defaults permitted",
                null),

            new(Control.Flag, 1, "true", "Flag", "Flag1",
                "An example Flag",
                null),

            #endregion

            #region Level 2: Station

            new(
                Control.Note, 2, "", "Note2", "noteZ",
                "An example note.",
                null),

            new(
                Control.MultiLine, 2, "", "Multiline2", "multilineZ",
                $"An example multiline.{Environment.NewLine}• e.g., Second line.",
                null),

            new(
                Control.AlphaNumeric, 2, "", "AlphaNumeric2", "alphaNumericZ",
                "An example alphaNumeric.",
                null),

            new(
                Control.IntegerAny, 2, "", "IntegerAny2", "integerAnyZ",
                "An example IntegerAny.",
                null),

            new(
                Control.IntegerPositive, 2, "", "IntegerPositive2", "integerPositiveZ",
                "An example IntegerPositive.",
                null),

            new(
                Control.DecimalAny, 2, "", "DecimalAny2", "decimalAnyZ",
                "An example DecimalAny.",
                null),

            new(
                Control.DecimalPositive, 2, "", "DecimalPositive2", "decimalPositiveZ",
                "An example DecimalPositive.",
                null),

            new(Control.FixedChoice, 2, "", "FixedChoice2", "FixedChoiceZa",
                "FixedChoice with blank option true",
                StandardsBase.CreateChoiceList(true, ["Aaa", "Bbb", "Ccc", "Should have a blank option"])),

            new(Control.FixedChoice, 2, "", "FixedChoice2", "FixedChoiceZb",
                "FixedChoice with blank option false",
                StandardsBase.CreateChoiceList(false, ["Aaa", "Bbb", "Ccc", "Should not have a blank option"])),

            new(Control.MultiChoice, 2, "", "MultiChoice2a", "multiChoiceZa",
                "Multichoice with blank option true",
                StandardsBase.CreateChoiceList(true, ["Aaa", "Bbb", "Fff", "Configured with a blank option"])),

            new(Control.MultiChoice, 2, "", "MultiChoice2b", "multiChoiceZb",
                "Multichoice with blank option false",
                StandardsBase.CreateChoiceList(false, ["Aaa", "Bbb", "Ggg", "Configured without a blank option"])),

            new(Control.DateTime_, 2, "", "DateTimeCustom2", "DateTimeCustomX",
                "DateTime_ - no defaults permitted",
                null),

            new(Control.Date_, 2, "", "Date2", "DateZ",
                "Date_ - no defaults permitted",
                null),

            new(Control.Time_, 2, "", "Time2", "TimeZ",
                "Time_ - no defaults permitted",
                null),

            new(Control.Flag, 2, "", "Flag2", "FlagZ",
                "An example Flag",
                null),
            #endregion

            #region Deployment level
            new(Control.Flag, 3, "", "Flag3", "Flag3",
                "An example Flag",
                null),
            #endregion
        ];

        #region All controls Image Set:Image template as a Row List

        public static List<StandardsRow> ImageTemplateRows =
        [
            new(
                Control.Note, 1, "a note", "iNote", "img_note",
                "A note for the image set",
                null),


            new(
                Control.MultiLine, 1, "a multiline", "iMultiline", "img_multiline",
                "A Multiline for the image set",
                null),


            new(
                Control.AlphaNumeric, 1, "An_AlphaNumeric", "iAlphaNumeric", "img_alphaNumeric",
                "An AlphaNumeric for the image set",
                null),


            new(
                Control.Counter, 1, "1", "iCount", "img_count",
                "A count for the image set",
                null),


            new(
                Control.IntegerAny, 1, "-2", "iIntegerAny", "img_IntegerAny",
                "An IntegerAny for the image set",
                null),


            new(
                Control.IntegerPositive, 1, "3", "iIntegerPositive", "img_IntegerPositive",
                "An IntegerPositive for the image set",
                null),


            new(
                Control.DecimalAny, 1, "-4.0", "iDecimalAny", "img_DecimalAny",
                "A DecimalAny for the image set",
                null),


            new(
                Control.DecimalPositive, 1, "5.0", "iDecimalPositive", "img_DecimalPositive",
                "A DecimalPositive for the image set",
                null),


            new(
                Control.FixedChoice, 1, "Bbb", "iFixedchoice", "img_fixedchoice",
                "A FixedChoice for the image set",
                StandardsBase.CreateChoiceList(true, ["Aaa", "Bbb", "Ccc", "Should have a blank option"])),


            new(
                Control.MultiChoice, 1, "Yyy,Xxx", "iMultichoice", "img_multichoice",
                "A MultiChoice for the image set",
                StandardsBase.CreateChoiceList(false, ["Xxx", "Yyy", "Zzz", "No blank option"])),


            new(
                Control.DateTime_, 1, "2025-05-05 05:05:05", "iDateTime_", "img_datetime_",
                "A DateTime_ for the image set",
                null),


            new(
                Control.Date_, 1, "2025-06-06", "iDate_", "img_date_",
                "A Date_ for the image set",
                null),


            new(
                Control.Time_, 1, "06:06:06", "iTime_", "img_time_",
                "A Time_ for the image set",
                null),


            new(
                Control.Flag, 1, "true", "iFlag", "i_flag",
                "A flag for the image set",
                null)

        ];

        #endregion
    }
}
