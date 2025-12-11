using System.Collections.Generic;
using System.Linq;

namespace Timelapse.Util
{
    public static class Dictionaries
    {
        // Returns true only if the two string-based dictionaries have the same keys, and each dictionaries key has the same value
        // regardless of order of the key/value pairs in the dictionary. 
        // ReSharper disable once UnusedMember.Global
        public static bool AreKeysAndTheirStringValuesTheSame(Dictionary<string, string> dict1, Dictionary<string, string> dict2)
        {
            if (dict1 == null && dict2 == null)
            {
                // both null, so equal
                return true;
            }

            if (dict1 == null || dict2 == null)
            {
                // only one of them is null, so unequal
                return false;
            }

            if (dict1.Count != dict2.Count)
            {
                // If the count isn't the same, its unequal
                return false;
            }

            foreach (var pair in dict1)
            {
                if (dict2.TryGetValue(pair.Key, out string value))
                {
                    // The value is not equal, so false.
                    if (value != pair.Value)
                    {
                        return false;
                    }
                }
                else
                {
                    // The key is not present, so false.
                    return false;
                }
            }
            // All keys are present with the same values.
            return true;
        }

        // Merge Dictionaries
        // - The new dictionary will contain the union of keys present in both dictionaries
        // - If a key is present in both dictionaries, it must have the same value
        // - otherwise, the merge will fail and a false is returned
        public static bool MergeDictionaries(Dictionary<string, string> dict1, Dictionary<string, string> dict2, out Dictionary<string, string> mergedDictionary, out bool differentKeysToSameValueDetected)
        {
            mergedDictionary = [];
            differentKeysToSameValueDetected = false;

            if ((dict1 == null || dict1.Count == 0) && (dict2 == null || dict2.Count == 0))
            {
                // both null or empty
                // so mergedDictionary has 0 items
                return true;
            }

            if (dict1 == null || dict1.Count == 0)
            {
                // Stuff in dict2 only (although count could be 0)
                // mergedDictionary is dict2
                mergedDictionary = dict2.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                return true;
            }

            if (dict2 == null || dict2.Count == 0)
            {
                // Stuff in dict1 only (although count could be 0)
                // mergedDictionary is dict1
                mergedDictionary = dict1.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                return true;
            }

            // Check if the dictionaries are compatable, i.e., that the common keys have the same values
            foreach (KeyValuePair<string, string> pair in dict1)
            {
                if (dict2.TryGetValue(pair.Key, out string value))
                {
                    // If the key is in dict2 and it has a different value, we've failed
                    if (value != pair.Value)
                    {
                        mergedDictionary = null;
                        return false;
                    }
                }
            }
            foreach (KeyValuePair<string, string> pair in dict1)
            {
                if (dict1.TryGetValue(pair.Key, out string value))
                {
                    // If the key is in dict1 and it has a different value, we've failed
                    if (value != pair.Value)
                    {
                        mergedDictionary = null;
                        return false;
                    }
                }
            }

            // At this point we know that the dictionaries are compatable. 
            // Start by adding all KVP in dict1 to the merged dictionary
            mergedDictionary = dict1.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Now add the KVP in dict2 that aren't present in dict1
            foreach (KeyValuePair<string, string> pair in dict2)
            {
                if (false == dict1.ContainsKey(pair.Key))
                {
                    mergedDictionary.Add(pair.Key, pair.Value);
                }
            }
            return true;
        }

        public static Dictionary<string, string> MergeDictionariesPreferringNonEmptyValues(Dictionary<string, string> dict1, Dictionary<string, string> dict2)
        {
            Dictionary<string, string> mergedDictionary = [];

            if ((dict1 == null || dict1.Count == 0) && (dict2 == null || dict2.Count == 0))
            {
                // both null or empty
                // so mergedDictionary has 0 items
                return mergedDictionary;
            }

            if (dict1 == null || dict1.Count == 0)
            {
                // Stuff in dict2 only (although count could be 0)
                // mergedDictionary is dict2
                return dict2.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            if (dict2 == null || dict2.Count == 0)
            {
                // Stuff in dict1 only (although count could be 0)
                // mergedDictionary is dict1
                return dict1.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            // Add all KVP in dict1 to the merged dictionary
            // If one of the values is empty, use the non-empty one
            // Otherwise use the dict1 value. 
            //mergedDictionary = dict1.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Now add the KVP in dict2 that aren't present in dict1
            foreach (KeyValuePair<string, string> dict1Kvp in dict1)
            {
                if (string.IsNullOrWhiteSpace(dict1Kvp.Value) && dict2.TryGetValue(dict1Kvp.Key, out string value))
                {
                    // If the dict1 value is empty, use the (possibly empty) value from dict2 otherwise empty
                    mergedDictionary.Add(dict1Kvp.Key, value);
                }
                else
                {
                    // If the dict1 value is not empty, use the dict1 value
                    // This means that if the dict2 value has a result, we still use dict1
                    mergedDictionary.Add(dict1Kvp.Key, dict1Kvp.Value);
                }
            }
            foreach (KeyValuePair<string, string> dict2Kvp in dict2)
            {
                if (dict1.ContainsKey(dict2Kvp.Key))
                {
                    // If the key is already in dict1, skip it as it would have been added in the above loop
                    continue;
                }
                // this key only exists in dict2, so add it as it would not have been added previously
                mergedDictionary.Add(dict2Kvp.Key, dict2Kvp.Value);
            }
            return mergedDictionary;
        }
    }
}
