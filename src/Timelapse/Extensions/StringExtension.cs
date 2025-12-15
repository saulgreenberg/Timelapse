using System;
using System.Text.RegularExpressions;

namespace Timelapse.Extensions
{
    public static class StringExtension
    {
        // Replace the first occurrence the oldSubstring with the newSubString in the source string
        // eg., ReplaceLastOccurence(@"Foo\Bar\Butt\Bar", "Bar", "XXX") -> Foo\XXX\Butt\Bar
        extension(string sourceString)
        {
            public string ReplaceFirstOccurrence(string oldSubString, string newSubString)
            {
                int place = sourceString.IndexOf(oldSubString, StringComparison.OrdinalIgnoreCase);
                if (place == -1)
                    return sourceString;
                return sourceString.Remove(place, oldSubString.Length).Insert(place, newSubString);
            }

            public string ReplaceLastOccurrence(string oldSubString, string newSubString)
            {
                int place = sourceString.LastIndexOf(oldSubString, StringComparison.OrdinalIgnoreCase);
                if (place == -1)
                    return sourceString;
                return sourceString.Remove(place, oldSubString.Length).Insert(place, newSubString);
            }

            public string ToWindowsPathSeparatorsAsNeeded()
            {
                return sourceString.Replace('/', '\\');
            }

            public int NthIndexOf(char charToFind, int index)
            {
                return sourceString.NthIndexOf(charToFind.ToString(), index);
            }

            public int NthIndexOf(string stringToFind, int index)
            {
                Match m = Regex.Match(sourceString, "((" + Regex.Escape(stringToFind) + ").*?){" + index + "}");
                return m.Success
                    ? m.Groups[2].Captures[index - 1].Index
                    : -1;
            }
        }


        // Replace the last occurrence the oldSubstring with the newSubString in the source string
        // eg., ReplaceLastOccurence(@"Foo\Bar\Butt\Bar", "Bar", "XXX") -> Foo\Bar\Butt\XXX

        // Convert unix-style path separators to Windows separators

        // Return the index of charToFind or stringToFind occuring the nth time in a string
        // e.g. if the string is "a" and index is 3, "012a4a6a89a0" it should return 7 (i.e., the 3rd a, counting from 0)
        // e.g. if the string is "aa" and index is 3, "012aaaaaaaaa4a6a89a0" will return 7 as it counts individual successive aa's (012 aa aa aa ...)
    }

}
