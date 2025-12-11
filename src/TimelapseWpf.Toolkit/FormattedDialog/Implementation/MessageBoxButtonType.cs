namespace TimelapseWpf.Toolkit
{
    // Enumeration to specify which buttons should be displayed in FormattedDialog
    // Determines the available user actions and dialog result behavior
    public enum MessageBoxButtonType
    {
        // Display only an OK button
        // Returns: true for OK, null for window close
        OK,

        // Display OK and Cancel buttons (default)
        // Returns: true for OK, false for Cancel, null for window close
        OKCancel,

        // Display Yes and No buttons
        // Returns: true for Yes, false for No, null for window close
        YesNo,

        // Display Yes, No, and Cancel buttons
        // Returns: true for Yes, false for No/Cancel, null for window close
        YesNoCancel
    }
}
