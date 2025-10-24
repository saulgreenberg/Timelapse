using System;

namespace Timelapse.DataStructures
{
    // Collects the valid arguments. Flags are case-insensitive
    // valid combinations of flags are:
    // <tdbfile> : opens the specified template in Timelapse
    // <ddbfile> : opens the specified database in Timelapse, if there is an accompanying template in the same folder
    // <tdbfile> -viewonly : as a with .tdbfile, but opens in view-only mode
    // <ddbfile> -viewonly : as a with .ddbfile, but opens in view-only mode
    // <tdbfile> -relativePath <relative path> : as a with .tdbfile, but constrains all folder actions to the relative path and its subfolders
    // <ddbfile> -relativePath <relative path> : as a with .ddbfile, but constrains all folder actions to the relative path and its subfolders
    // <tdbfile> -viewonly -relativePath <relative path> : as a with .tdbfile, but constrains all folder actions to the relative path and its subfolders
    // <ddbfile> -viewonly -relativePath <relative path> : as a with .ddbfile, but constrains all folder actions to the relative path and its subfolders
    public class Arguments
    {
        // The full Timelape template path
        public string TdbFile { get; set; } = string.Empty;
        public string DdbFile { get; set; } = string.Empty;

        // Constrain all database actions to the relative path and its subfolders
        public string RelativePath { get; set; } = string.Empty;

        // Whether Timelapse was opened with a -viewonly argument
        public bool IsViewOnly { get; set; }

        public bool IsOpenInTemplateEditor { get; set; }

        // Constrain all database actions to the relative path and its subfolders
        // If ConstrainToRelativePath is true, the user is contrained to select folders that are either the relative path or subfolders of it.
        public bool ConstrainToRelativePath =>
            // if relativePath is empty, we shouldn't constrain to it
            !string.IsNullOrWhiteSpace(RelativePath);

        public Arguments(string[] arguments)
        {
            // Note that the first argument of interest starts at 1, as the zeroth element of the array is the name of the executing program
            if (arguments == null || arguments.Length < 2)
            {
                return;
            }

            int startIndex = 1;
            // If the first argument is a tdb or ddb file, set its corresponding property and return
            if (arguments[startIndex].EndsWith(Constant.File.TemplateDatabaseFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                // its a tdb file
                TdbFile = arguments[startIndex];
                startIndex++;
            }
            else if (arguments[startIndex].EndsWith(Constant.File.FileDatabaseFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                DdbFile = arguments[startIndex];
                startIndex++;
            }

            // Process other arguments, if any 
            for (int index = startIndex; index < arguments.Length; index += 1)
            {
                switch (arguments[index].ToLower())
                {
                    // Open in view-only mode
                    case Constant.Arguments.ViewOnlyArgument:
                        IsViewOnly = true;
                        break;

                    case Constant.Arguments.RelativePathArgument:
                        // Make sure that this is followed by an arguement specifying the relative path
                        // Of course, that doesn't mean the relative path is valid, just that something was specified
                        if (index + 1 < arguments.Length)
                        {
                            index++;
                            RelativePath = arguments[index];
                        }
                        break;

                    // Open in the template editor rather than in the main Timelapse window
                    case Constant.Arguments.TemplateEditorArgument:
                        IsOpenInTemplateEditor = true;
                        break;
                }
            }
        }
    }
}

