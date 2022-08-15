using System;

namespace DialogUpgradeFiles.Util
{
    // Collects a single argument. A valid argument is a valid folder path.
    public class Arguments
    {
        // The full Timelape template path
        public string FolderPath { get; set; } = String.Empty;

        public Arguments(string[] arguments)
        {
            if (arguments == null || arguments.Length < 2)
            {
                // The argument list contains the name of the program, so we check if there are at least two in the list
                // No arguments, so do nothing
                return;
            }
            this.FolderPath = arguments[1];
            System.Diagnostics.Debug.Print(this.FolderPath);
            // If the argument exists, invoke the appropriate action
            // Note that we only look at the first argument, as the zero'th element of the array is the name of the executing program
        }
    }
}
