using System;
using System.Text.RegularExpressions;

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

        // Convert unix-style path separators to Windows separators
        public static string ToWindowsPathSeparatorsAsNeeded(this string sourceString)
        {
            return sourceString.Replace('/', '\\');
        }

        // Return the index of charToFind or stringToFind occuring the nth time in a string
        // e.g. if the string is "a" and index is 3, "012a4a6a89a0" it should return 7 (i.e., the 3rd a, counting from 0)
        // e.g. if the string is "aa" and index is 3, "012aaaaaaaaa4a6a89a0" will return 7 as it counts individual successive aa's (012 aa aa aa ...)
        public static int NthIndexOf(this string sourceString, char charToFind, int index)
        {
            return sourceString.NthIndexOf(charToFind.ToString(), index);
        }
        public static int NthIndexOf(this string sourceString, string stringToFind, int index)
        {
            Match m = Regex.Match(sourceString, "((" + Regex.Escape(stringToFind) + ").*?){" + index + "}");
            return m.Success
                ? m.Groups[2].Captures[index - 1].Index
                : -1;
        }
    }

}
