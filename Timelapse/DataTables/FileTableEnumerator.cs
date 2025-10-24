using System;
using System.Collections;
using System.Collections.Generic;
using Timelapse.Constant;
using Timelapse.Database;

namespace Timelapse.DataTables
{
    public class FileTableEnumerator(FileDatabase fileDatabase) : IEnumerator<ImageRow>
    {
        protected FileDatabase Database { get;} = fileDatabase;

        // the current image, null if its no been set or if the database is empty
        public ImageRow Current { get; private set; }
        public int CurrentRow { get; private set; }

        //, int startingPosition)

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        object IEnumerator.Current => Current;

        /// <summary>
        /// Go to the next image, returning false if we can't (e.g., if we are at the end) 
        /// </summary>
        public bool MoveNext()
        {
            return TryMoveToFile(CurrentRow + 1);
        }

        public virtual void Reset()
        {
            Current = null;
            CurrentRow = DatabaseValues.InvalidRow;
        }

        /// <summary>
        /// Go to the previous image, returning true if we can otherwise false (e.g., if we are at the beginning)
        /// </summary>
        public bool MovePrevious()
        {
            return TryMoveToFile(CurrentRow - 1);
        }

        /// <summary>
        /// Attempt to go to a particular image, returning true if we can otherwise false (e.g., if the index is out of range)
        /// Remember, that we are zero based, so (for example) and index of 5 will go to the sixth image
        /// </summary>
        public virtual bool TryMoveToFile(int imageRowIndex)
        {
            if (Database.IsFileRowInRange(imageRowIndex))
            {
                CurrentRow = imageRowIndex;
                // rebuild ImageProperties regardless of whether the row changed or not as this seek may be a refresh after a database change
                Current = Database.FileTable[imageRowIndex];
                return true;
            }

            return false;
        }

        protected virtual void Dispose(bool disposing)
        {
            // nothing to do but required by IEnumerator<T>
        }
    }
}
