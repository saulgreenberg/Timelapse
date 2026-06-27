using System;
using System.Collections.Generic;
using Timelapse.Constant;
namespace TimelapseTemplateEditor.Standards
{
    public static class PracticeImageSetMetadataExample
    {
        // The standard specification: a list of StandardsRow, common species (used by one row), and level aliases
        #region The Alias specification associated with each level
        public static Dictionary<int, string> Aliases = new()
        {
            {1, "Project"},
            {2, "Station"},
            {3, "Deployment"}
        };
        #endregion

        #region List of common species
        public static List<string> SpeciesCommonList =
        [
            "bear",
            "bighorn sheep",
            "bobcat",
            "cattle",
            "coyote",
            "deer",
            "elk",
            "fox",
            "lagomorph",
            "moose",
            "mountain goat",
            "mountain lion",
            "rabbit",
            "raccoon",
            "squirrel",
            "wolf"
        ];
        #endregion

        #region PracticeImageSet Folder Metadata as a Row List

        public static List<StandardsRow> FolderMetadataRows =
        [
            #region Level 1: Project
            new(
                Control.Note, 1, "Timelapse Metadata Example", "Project Name", "project_name",
                $"The name of the project.{Environment.NewLine}" +
                "• e.g., \"Timelapse Metadata Example\".",
                null),

            new(
                Control.AlphaNumeric, 1, "Project_1", "Project ID", "project_id",
                $"A unique alphanumeric id identifying this project{Environment.NewLine}" +
                "• e.g., \"Timelapse_Project1\".",
                null),

            new(
                Control.Note, 1, "Greenberg Consulting, Inc.", "Organization", "project_org",
                $"The organization responsible for the project.{Environment.NewLine}" +
                "• e.g., \"Greenberg Consulting, Inc.\".",
                null),

            new(Control.Note, 1, "", "Contact Person", "project_coord",
                $"The first and last name of the primary contact for the project.{Environment.NewLine}" +
                " • e.g., \"John Smith\".",
                null),

            new(Control.Note, 1, "", "Contact Email", "project_coord_email",
                $"The email address of the Project Coordinator.{Environment.NewLine}\" +" +
                " • e.g., \"John.Smith@gmail.com\"",
                null),

            new(Control.MultiLine, 1, "", "Purpose", "project_purpose",
                $"A short description of what this project is about.{Environment.NewLine}\" +" +
                " • e.g., \"This project is used only to illustrate a simple metadata hierarchy structure.\"",
                null),
            #endregion

            #region Level 2: Station
            new(Control.Note, 2, "", "Station Name", "station_name",
                $"The station name, preferably the same as the name of the corresponding station folder.{Environment.NewLine}" +
                "• e.g., \"Station1\"",
                null),

            new(Control.DecimalAny, 2, "", "Latitude", "station_latitude",
                $"The latitude of the station's location in decimal degrees to five decimal places.{Environment.NewLine}" +
                "• e.g., \"53.56789\"",
                null),

            new(Control.DecimalAny, 2, "", "Longitude", "station_longitude",
                $"The longitude of the station's location in decimal degrees to five decimal places.{Environment.NewLine}" +
                "• e.g., \"75.1234\"",
                null),

            new(Control.MultiLine, 2, "", "Location Comments", "station_comments",
                $"Describe any relevant details about this station's location{Environment.NewLine}" +
                "• e.g., \"Grasslands meadow with evident game trails.\"",
                null),
            #endregion

            #region Level 3: Deployment
            new(Control.Note, 3, "", "Deployment Name", "deployment_name",
                $"The deployment name, preferably the same as the name of the corresponding deployment folder.{Environment.NewLine}" +
                "• e.g., \"Deployment1a\".",
                null),

            new(Control.MultiLine, 3, "", "Deployment Crew", "deployment_crew",
                $"The first and last names of all the individuals who collected data during deployment visit.{Environment.NewLine}" +
                "• e.g., \"John Smith\".",
                null),

            new(Control.Date_, 3, "", "Deployment Start Date_", "deployment_start_date",
                "The start date that the camera started recording for this deployment.",
                null),

            new(Control.Date_, 3, "", "Deployment End Date_", "deployment_end_date",
                "The end date that the camera ended recording for this deployment.",
                null),

            new(Control.MultiLine, 3, "", "Visit Comments", "deployment_comments",
                $"Describe any additional details about a visit to a camera location.{Environment.NewLine}" +
                "• e.g., camera snow-covered; batteries replaced.",
                null),

            new(Control.AlphaNumeric, 3, "", "Camera ID", "camera_id",
                $"A unique alphanumeric ID for the camera that distinguishes it from other cameras of the same Camera Make or Camera Model{Environment.NewLine}" +
                "• e.g., \"RECONPC900-1\"",
                null),

            new(Control.Note, 3, "", "Camera Make", "camera_make",
                "The make (i.e., the manufacturer; e.g., Reconyx or Bushnell etc) of a particular camera." +
                "• e.g., \"Reconyx\"", null),

            new(Control.Note, 3, "", "Camera Model", "camera_model",
                $"The model number or name (e.g., PC900 or Trophy Cam HD etc) of a particular camera.{Environment.NewLine}" +
                "• e.g., \"PC900\"",
                null),

            new(Control.MultiChoice, 3, "", "Trigger Mode(s)", "deployment_trig_modes",
                $"The camera setting(s) that determine how the camera will trigger:{Environment.NewLine}" +
                $"Select one or more of the options from the dropdown list provided, explained as follows:{Environment.NewLine}." +
                $"• by motion (\"Motion Image\"),{Environment.NewLine}" +
                $"• at set intervals (\"Time_-lapse Image\"),{Environment.NewLine}" +
                "• and/or by video (\"Video\")",
                StandardsBase.CreateChoiceList(true,
                [
                    "Motion Image", "Time_-lapse Image", "Video", "Motion Image + Time_-lapse Image", "Motion Image + Time_-lapse Image + Video", "Time_-lapse Image + Video",
                    "Motion Image + Video"
                ])),
  
            new(Control.MultiLine, 3, "", "Analyst", "deployment_analyst",
                $"The people who analyzed the images in this deployment.{Environment.NewLine}" +
                "e.g., \"Susie Smith\"",
                null),

        ];
        #endregion
        #endregion

        #region Practice Image Set:Image template as a Row List

        public static List<StandardsRow> ImageTemplateRows =
        [
            new(
                Control.FixedChoice, 1, "", "Species", "img_species",
                $"The species seen in the image.{Environment.NewLine}" +
                "If there is more than one wildlife species present, then chose Edit|Duplicate and enter the other species on the duplicate entry",
                StandardsBase.CreateChoiceList(true, SpeciesCommonList)),


            new(
                Control.Counter, 1, "", "Count", "img_individual_count",
                $"The number of unique individuals of that species captured in the image.{Environment.NewLine}" +
                "e.g., \"25\"",
                null),


            new(
                Control.Note, 1, "", "Sequence", "img_sequence",
                "Use Edit|Populate a field with episode data... to automatcially fill in the position of an image in a motion-triggered sequence.",
                null),


            new(
                Control.Note, 1, "", "Temperature", "img_temperature",
                "Use Edit|Populate one or more fields with metadata... to find and automatcially fill in this field with an image's temperature data.",
                null),


            new(
                Control.FixedChoice, 1, "", "Problem", "img_problem",
                "A condition that makes it difficult to evaluate the image.",
                StandardsBase.CreateChoiceList(true, [
                    "wind triggered", "lens obscured", "malfunction", "misdirected", "snow or rain on lens",
                    "vegetation obstruction", "weather (fog, rain)"
                ])),


            new(
                Control.MultiLine, 1, "", "Comment", "img_comment",
                "Any comment you wish to add",
                null),


            new(
                Control.Note, 1, "", "Analyst", "img_analysst",
                "The name of the person who analyzed these images",
                null),


            new(
                Control.Flag, 1, "", "Publicity?", "img_publicity",
                "If this is a really good image useful for publicity purposes",
                null),


            new(
                Control.Flag, 1, "", "Dark?", "img_dark",
                "Use Edit | Populate a field with Dark Classifications ... to automatically set this field to 'true' if the image is dark.",
                null),

            new(
                Control.Flag, 1, "", "Empty?", "img_empty",
                "Is the image empty i.e., no people or wildlife? (see image recognition workflow).",
                null),


            new(
                Control.Flag, 1, "", "People?", "img_people",
                "Are people present in the image? (see image recognition workflow.)",
                null),


            new(
                Control.Flag, 1, "", "Wildlife?", "img_wildlife",
                "Is wildlife present in the image? (see image recognition workflow.)",
                null)

        ];
        #endregion
    }
}
