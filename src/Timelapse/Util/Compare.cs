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

            Dictionary<string, string> dictionaryDifferences = [];
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
                return ListComparisonEnum.ElementsDiffer;
            }
            List<string> firstNotSecond = list1.Except(list2).ToList();
            List<string> secondNotFirst = list2.Except(list1).ToList();
            if (firstNotSecond.Any() || secondNotFirst.Any())
            {
                // At least one element in either list differs
                return ListComparisonEnum.ElementsDiffer;
            }
            return (list1.SequenceEqual(list2))
            ? ListComparisonEnum.Identical
            : ListComparisonEnum.ElementsSameButOrderDifferent;
        }
    }
}
