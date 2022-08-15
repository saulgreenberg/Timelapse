using System.Collections;
using System.Collections.Generic;

namespace Timelapse.Util
{
    /// <summary>
    /// Maintain a recency-ordered collection of items
    /// </summary>
    /// <typeparam name="TElement"></typeparam>
    public class RecencyOrderedList<TElement> : IEnumerable<TElement>
    {
        #region Public Properties
        /// <summary>
        /// Number of items in the collection
        /// </summary>
        public int Count
        {
            get { return this.list.Count; }
        }
        #endregion

        #region Private variables
        private readonly LinkedList<TElement> list;
        private readonly int maximumItems;
        #endregion

        #region Constructor
        /// <summary>
        /// Initalize a recency order collection that holds maximumItems
        /// When the collection is full, adding an item drops off the oldest item 
        /// </summary>
        /// <param name="maximumItems"></param>
        public RecencyOrderedList(int maximumItems)
        {
            this.list = new LinkedList<TElement>();

            // subtract one off the list's maximum size as SetMostRecent() checks it after 
            this.maximumItems = maximumItems;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Whether or not the collection is full i.e, it has the maximum number of items
        /// </summary>
        /// <returns></returns>
        public bool IsFull()
        {
            return this.list.Count == this.maximumItems;
        }

        /// <summary>
        /// Add or move an item to the most recent position.
        /// If the list is full, drop the oldest item
        /// </summary>
        /// <param name="mostRecent"></param>
        public void SetMostRecent(TElement mostRecent)
        {
            if (this.list.Remove(mostRecent) == false)
            {
                // item wasn't already in the list
                if (this.list.Count >= this.maximumItems)
                {
                    // list was full, drop the oldest item to make room for new item
                    this.list.RemoveLast();
                }
            }
            // make the item the most current in the list
            this.list.AddFirst(mostRecent);
        }

        /// <summary>
        /// Get the most recent item and put it in mostRecent
        /// </summary>
        /// <param name="mostRecent"></param>
        /// <returns>false if the list is empty</returns>
        public bool TryGetMostRecent(out TElement mostRecent)
        {
            if (this.list.Count > 0)
            {
                mostRecent = this.list.First.Value;
                return true;
            }
            mostRecent = default;
            return false;
        }

        /// <summary>
        /// Get the oldest item and put it in mostRecent
        /// </summary>
        /// <param name="mostRecent"></param>
        /// <returns>false if the list is empty</returns>
        public bool TryGetLeastRecent(out TElement leastRecent)
        {
            if (this.list.Count > 0)
            {
                leastRecent = this.list.Last.Value;
                return true;
            }
            leastRecent = default;
            return false;
        }

        /// <summary>
        /// Remove the itme indicated by value
        /// </summary>
        /// <param name="mostRecent"></param>
        /// <returns>false if the collecton does not contain that item </returns>
        public bool TryRemove(TElement value)
        {
            return this.list.Remove(value);
        }
        #endregion

        #region Enumerator-related
        public IEnumerator<TElement> GetEnumerator()
        {
            return this.list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.list.GetEnumerator();
        }
        #endregion
    }
}
