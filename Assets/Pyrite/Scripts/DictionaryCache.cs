namespace Pyrite
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public class DictionaryCache<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> _dictionary = new Dictionary<TKey, TValue>();
        private readonly LinkedList<TKey> _queue = new LinkedList<TKey>();

        public int MaximumLength { get; set; }

        public DictionaryCache(int maximumLength)
        {
            if (maximumLength < 2)
            {
                throw new ArgumentException("Length must be >= 2", "maximumLength");
            }

            MaximumLength = maximumLength;
        }

        public TValue this[TKey key]
        {
            get { return _dictionary[key]; }

            set { _dictionary[key] = value; }
        }

        public int Count
        {
            get { return _dictionary.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public ICollection<TKey> Keys
        {
            get { return _dictionary.Keys; }
        }

        public ICollection<TValue> Values
        {
            get { return _dictionary.Values; }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public void Add(TKey key, TValue value)
        {
            if (_dictionary.Count >= MaximumLength)
            {
                _dictionary.Remove(_queue.Last());
                _queue.RemoveLast();
            }

            _dictionary.Add(key, value);
            _queue.AddFirst(key);
        }

        public void Clear()
        {
            _dictionary.Clear();
            _queue.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return _dictionary.Contains(item);
        }

        public bool ContainsKey(TKey key)
        {
            return _dictionary.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return _dictionary.GetEnumerator();
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            throw new NotImplementedException();
        }

        public bool Remove(TKey key)
        {
            _dictionary.Remove(key);
            return _queue.Remove(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return _dictionary.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _dictionary.GetEnumerator();
        }
    }
}