using System;

namespace Timelapse.Extensions
{
    public static class StringExtension
    {
        // Replace the first occurrence the oldSubstring with the newSubString in the source string
        // eg., ReplaceLastOccurence(@"Foo\Bar\Butt\Bar", "Bar", "XXX") -> Foo\XXX\Butt\Bar
        public static string ReplaceFirstOccurrence(this string sourceString, string oldSubString, string newSubString)
        {
            int place = sourceString.IndexOf(oldSubString, StringComparison.OrdinalIgnoreCase);
            if (place == -1)
                return sourceString;
            return sourceString.Remove(place, oldSubString.Length).Insert(place, newSubString);
        }


        // Replace the last occurrence the oldSubstring with the newSubString in the source string
        // eg., ReplaceLastOccurence(@"Foo\Bar\Butt\Bar", "Bar", "XXX") -> Foo\Bar\Butt\XXX

        public static string ReplaceLastOccurrence(this string sourceString, string oldSubString, string newSubString)
        {
            int place = sourceString.LastIndexOf(oldSubString, StringComparison.OrdinalIgnoreCase);
            if (place == -1)
                return sourceString;
            return sourceString.Remove(place, oldSubString.Length).Insert(place, newSubString);
        }
    }
}
