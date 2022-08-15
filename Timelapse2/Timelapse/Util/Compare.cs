using System.Collections.Generic;
using System.Linq;
using Timelapse.Enums;

namespace Timelapse.Util
{
    /// <summary>
    /// Compare various objects for differences
    /// </summary>

    public static class Compare
    {
        /// <summary>
        /// Given two dictionaries, return a dictionary that contains only those key / value pairs in dictionary1 that are not in dictionary2  
        /// </summary>
        /// <param name="dictionary1"></param>
        /// <param name="dictionary2"></param>
        /// <returns></returns>
        public static Dictionary<string, string> Dictionary1ExceptDictionary2(Dictionary<string, string> dictionary1, Dictionary<string, string> dictionary2)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(dictionary1, nameof(dictionary1));
            ThrowIf.IsNullArgument(dictionary2, nameof(dictionary2));

            Dictionary<string, string> dictionaryDifferences = new Dictionary<string, string>();
            List<string> differencesByKeys = dictionary1.Keys.Except(dictionary2.Keys).ToList();
            foreach (string key in differencesByKeys)
            {
                dictionaryDifferences.Add(key, dictionary1[key]);
            }
            return dictionaryDifferences;
        }

        /// <summary>
        /// Given two Lists of strings, return whether they both contain the same values 
        /// </summary>
        /// <param name="list1"></param>
        /// <param name="list2"></param>
        /// <returns></returns>
        public static ListComparisonEnum CompareLists(List<string> list1, List<string> list2)
        {
            if (list1 == null && list2 == null)
            {
                // true as they both contain nothing
                return ListComparisonEnum.Identical;
            }
            if (list1 == null || list2 == null)
            {
                // false as one is null and the other isn't
                return ListComparisonEnum.ElementsDiffer; ;
            }
            List<string> firstNotSecond = list1.Except(list2).ToList();
            List<string> secondNotFirst = list2.Except(list1).ToList();
            if (firstNotSecond.Any() || secondNotFirst.Any())
            {
                // At least one element in either list differs
                return ListComparisonEnum.ElementsDiffer;
            }
            if (list1.SequenceEqual(list2))
            {
                return ListComparisonEnum.Identical;
            }
            else
            {
                return ListComparisonEnum.ElementsSameButOrderDifferent;
            }
        }

        /// <summary>
        /// Unused - Return a string that is the longest common suffix between two strings
        /// </summary>
        /// <returns></returns>
        //{
        //    string suffix = String.Empty;
        //    if (s1 == null || s2 == null)
        //    {
        //        return suffix;
        //    }
        //    List<string> result = new List<string>();
        //    int s1length = s1.Length;
        //    int s2length = s2.Length;
        //    int length = Math.Min(s1length, s2length);

        //    // Starting from the last character of each string
        //    for (int i = 0; i < length; i++)
        //    {

        //        if (s1[s1length - i - 1] == s2[s2length - i - 1])
        //        {
        //            // If the character is the same, add it to the suffix. 
        //            suffix = s1[s1length - i - 1] + suffix;
        //        }
        //        else
        //        {
        //            // Otherwise we are done as we have the longest common suffix
        //            break;
        //        }
        //    }
        //    return suffix;
        //}
    }
}
