using System.Collections;
using System.Collections.Generic;

namespace Timelapse.DataStructures
{
    /// <summary>
    /// Maintain a recency ordered collection of items
    /// </summary>
    /// <typeparam name="TElement"></typeparam>
    /// <remarks>
    /// Initialize a recency order collection that holds maximumItems
    /// When the collection is full, adding an item drops off the oldest item 
    /// </remarks>
    /// <param name="maximumItems"></param>
    public class RecencyOrderedList<TElement>(int maximumItems) : IEnumerable<TElement>
    {
        #region Public Properties
        /// <summary>
        /// Number of items in the collection
        /// </summary>
        public int Count => list.Count;

        #endregion

        #region Private variables
        private readonly LinkedList<TElement> list = [];
        #endregion

        #region Public Methods
        /// <summary>
        /// Whether or not the collection is full i.e, it has the maximum number of items
        /// </summary>
        /// <returns></returns>
        public bool IsFull()
        {
            return list.Count == maximumItems;
        }

        /// <summary>
        /// Add or move an item to the most recent position.
        /// If the list is full, drop the oldest item
        /// </summary>
        /// <param name="mostRecent"></param>
        public void SetMostRecent(TElement mostRecent)
        {
            if (list.Remove(mostRecent) == false)
            {
                // item wasn't already in the list
                if (list.Count >= maximumItems)
                {
                    // list was full, drop the oldest item to make room for new item
                    list.RemoveLast();
                }
            }
            // make the item the most current in the list
            list.AddFirst(mostRecent);
        }

        /// <summary>
        /// Get the most recent item and put it in mostRecent
        /// </summary>
        /// <param name="mostRecent"></param>
        /// <returns>false if the list is empty</returns>
        public bool TryGetMostRecent(out TElement mostRecent)
        {
            if (list.Count > 0 && list.First != null)
            {
                mostRecent = list.First.Value;
                return true;
            }
            mostRecent = default;
            return false;
        }

        /// <summary>
        /// Get the oldest item and put it in mostRecent
        /// </summary>
        /// <param name="leastRecent"></param>
        /// <returns>false if the list is empty</returns>
        public bool TryGetLeastRecent(out TElement leastRecent)
        {
            if (list.Count > 0)
            {
                leastRecent = list.Last!.Value;
                return true;
            }
            leastRecent = default;
            return false;
        }

        /// <summary>
        /// Remove the item indicated by value
        /// </summary>
        /// <param name="value"></param>
        /// <returns>false if the collecton does not contain that item </returns>
        public bool TryRemove(TElement value)
        {
            return list.Remove(value);
        }
        #endregion

        #region Enumerator-related
        public IEnumerator<TElement> GetEnumerator()
        {
            // ReSharper disable once NotDisposedResourceIsReturned
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            // ReSharper disable once NotDisposedResourceIsReturned
            return list.GetEnumerator();
        }

        #endregion
    }
}
