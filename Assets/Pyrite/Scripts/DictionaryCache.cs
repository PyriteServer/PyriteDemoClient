namespace Pyrite
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class DictionaryCache<TKey, TValue>
    {
        protected class ValueEntry
        {
            public TValue Value;
            public LinkedListNode<TKey> KeyNode;
        }

        protected readonly Dictionary<TKey, ValueEntry> Dictionary = new Dictionary<TKey, ValueEntry>();
        protected readonly LinkedList<TKey> EvictionQueue = new LinkedList<TKey>();

        public int MaximumLength { get; set; }

        public DictionaryCache(int maximumLength)
        {
            if (maximumLength < 0)
            {
                throw new ArgumentException("Length must be >= 0", "maximumLength");
            }

            MaximumLength = maximumLength;
        }

        public TValue this[TKey key]
        {
            get { return GetValueForKey(key); }

            set { Add(key, value); }
        }

        private TValue GetValueForKey(TKey key)
        {
            var node = Dictionary[key].KeyNode;
            EvictionQueue.Remove(node);
            EvictionQueue.AddFirst(node);
            return Dictionary[key].Value;
        }

        public int Count
        {
            get { return Dictionary.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        protected virtual void EvictCacheEntry()
        {
            Dictionary.Remove(EvictionQueue.Last());
            EvictionQueue.RemoveLast();
        }

        public virtual void Add(TKey key, TValue value)
        {
            if (MaximumLength == 0)
            {
                return;
            }

            if (Dictionary.Count >= MaximumLength)
            {
                EvictCacheEntry();
            }

            if (Dictionary.ContainsKey(key))
            {
                var node = Dictionary[key].KeyNode;
                Dictionary[key].Value = value;
                EvictionQueue.Remove(node);
                EvictionQueue.AddFirst(node);
            }
            else
            {
                var node = EvictionQueue.AddFirst(key);
                Dictionary.Add(key, new ValueEntry {Value = value, KeyNode = node});
            }
        }

        public bool ContainsKey(TKey key)
        {
            return Dictionary.ContainsKey(key);
        }

        public virtual bool Remove(TKey key)
        {
            Dictionary.Remove(key);
            return EvictionQueue.Remove(key);
        }
    }
}