namespace Pyrite
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;

    internal static class ConcurrentExtensions
    {
        /// <summary>
        /// Acquires lock and then enqueues the item. Method yields until the lock has been acquired.
        /// Return must be iterated to completion or this method should be started as a coroutine
        /// </summary>
        /// <typeparam name="T">queue item type</typeparam>
        /// <param name="queue">queue to lock and enqueue item in</param>
        /// <param name="item">item to enqueue</param>
        /// <returns></returns>
        public static IEnumerator ConcurrentEnqueue<T>(this Queue<T> queue, T item)
        {
            while (!Monitor.TryEnter(queue))
            {
                yield return null;
            }
            queue.Enqueue(item);
            Monitor.Exit(queue);
        }

        /// <summary>
        /// Acquires lock and adds item to the collection. Method yields until lock has been acquired and the item is added.
        /// Return should be enumerated to completion or started as a coroutine
        /// </summary>
        /// <typeparam name="T">Type of collection</typeparam>
        /// <param name="collection">Collection to add item to, also the target of the lock</param>
        /// <param name="item">item to add to collection</param>
        /// <returns></returns>
        public static IEnumerator ConcurrentAdd<T>(this ICollection<T> collection, T item)
        {
            while (!Monitor.TryEnter(collection))
            {
                yield return null;
            }

            collection.Add(item);
            Monitor.Exit(collection);
        }

        /// <summary>
        /// Acquires lock on collection and removes item from it. Method yields while waiting for lock
        /// Method should be enumerated until complete or started as a coroutine to ensure completion
        /// </summary>
        /// <typeparam name="T">Type of collection</typeparam>
        /// <param name="collection">Collection to remove item from, will also be locked</param>
        /// <param name="item">Item to remove from collection</param>
        /// <returns></returns>
        public static IEnumerator ConcurrentRemove<T>(this ICollection<T> collection, T item)
        {
            while (!Monitor.TryEnter(collection))
            {
                yield return null;
            }

            collection.Remove(item);
            Monitor.Exit(collection);
        }

        /// <summary>
        /// Blocks until the IEnumerator is done. Intended to be used for coroutine type methods
        /// </summary>
        /// <param name="routine"></param>
        public static void Wait(this IEnumerator routine)
        {
            while (routine.MoveNext())
            {
            }
        }
    }
}