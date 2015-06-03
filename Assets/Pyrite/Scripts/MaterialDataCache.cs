namespace Pyrite
{
    using System;
    using System.Collections.Generic;
    using Object = UnityEngine.Object;

    public class MaterialDataCache : DictionaryCache<string, MaterialData>
    {
        private readonly Dictionary<string, int> _references = new Dictionary<string, int>();

        public int Evictions { get; private set; }

        public MaterialDataCache(int capacity) : base(capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentException("Material data cache requires substance", "capacity");
            }
        }

        public void AddRef(string materialKey)
        {
            if (_references.ContainsKey(materialKey))
            {
                _references[materialKey]++;
            }
            else
            {
                _references[materialKey] = 1;
            }
        }

        public void Release(string materialKey)
        {
            Release(materialKey, false);
        }

        private void Release(string materialKey, bool evicting)
        {
            if (_references.ContainsKey(materialKey))
            {
                _references[materialKey]--;

                if ((_references[materialKey]) == 0)
                {
                    if (Dictionary[materialKey].Value != null && Dictionary[materialKey].Value.DiffuseTex != null)
                    {
                        Object.Destroy(Dictionary[materialKey].Value.DiffuseTex);
                    }
                    _references.Remove(materialKey);
                }
                else if (Count > MaximumLength && (_references[materialKey]) == 1)
                {
                    EvictCacheEntry();
                }
            }
        }

        protected override void EvictCacheEntry()
        {
            var current = EvictionQueue.Last;
            while (current != EvictionQueue.First && current != null)
            {
                if (_references[current.Value] > 1 || Dictionary[current.Value].Value == null)
                {
                    current = current.Previous;
                }
                else
                {
                    Evictions++;
                    Release(current.Value, true);
                    var nextToConsiderEvicting = current.Previous;
                    EvictionQueue.Remove(current);
                    Dictionary.Remove(current.Value);
                    // Check if we still could evict more
                    if (Dictionary.Count >= MaximumLength)
                    {
                        current = nextToConsiderEvicting;
                        continue;
                    }
                    break;
                }
            }
        }

        public override void Add(string key, MaterialData value)
        {
            if (_references.ContainsKey(key))
            {
                if (_references[key] != 1)
                {
                    throw new InvalidOperationException("Cannot replace item that has outstanding references." +
                                                        _references[key]);
                }
            }
            else
            {
                AddRef(key);
            }

            base.Add(key, value);
        }

        public override bool Remove(string key)
        {
            _references.Remove(key);
            return base.Remove(key);
        }
    }
}