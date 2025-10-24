using System.Runtime.CompilerServices;
using System.Windows;

// WPF ThemeInfo attribute - critical for WPF resource dictionary location
[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,            // where theme specific resource dictionaries are located
    ResourceDictionaryLocation.SourceAssembly   // where the generic resource dictionary is located
)]

// Allow unit tests to access internal members
[assembly: InternalsVisibleTo("Timelapse.UnitTests")]
