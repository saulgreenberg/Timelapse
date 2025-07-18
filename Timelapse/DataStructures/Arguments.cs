﻿namespace Timelapse.DataStructures
{
    // Collects the valid arguments. All valid arguments are in the form of:
    // -flag1 value -flag2 value etc., where flags are case-insensitive
    public class Arguments
    {
        // The full Timelape template path
        public string Template { get; set; } = string.Empty;

        // Constrain all database actions to the relative path and its subfolders
        public string RelativePath { get; set; } = string.Empty;

        // Whether Timelapse was opened with a -viewonly argument
        public bool IsViewOnly { get; set; }

        // Constrain all database actions to the relative path and its subfolders
        // if ConstrainToRelativePath is true, the user is contrained to select folders that are either the relative path or subfolders of it.
        public bool ConstrainToRelativePath =>
            // if relativePath is empty, we shouldn't constrain to it
            !string.IsNullOrWhiteSpace(RelativePath);

        public Arguments(string[] arguments)
        {
            if (arguments == null)
            {
                return;
            }
            // If the argument exists, assign it
            // Note that we start at 1, as the first element of the array is the name of the executing program
            for (int index = 1; index < arguments.Length; index += 2)
            {
                switch (arguments[index].ToLower())
                {
                    case Constant.Arguments.TemplateArgument:
                        // Make sure there is an argument there
                        if (index + 1 < arguments.Length)
                        {
                            Template = arguments[index + 1];
                        }
                        break;
                    case Constant.Arguments.RelativePathArgument:
                        // Make sure there is an argument there
                        if (index + 1 < arguments.Length)
                        {
                            RelativePath = arguments[index + 1];
                        }
                        break;
                    case Constant.Arguments.ViewOnlyArgument:
                        IsViewOnly = true;
                        break;
                }
            }
        }
    }
}
