using System.Collections;
using System.Runtime.CompilerServices;

//подоптимизированная ридонли версия словаря для использования в бар итераторе
//убран вызов object.Equals, валидация входящих параметров, вызов object.GetHashCode напрямую (а не через DefaulEqualityComparer) и т.п.
//требует на вход только уникальные, положительные хеш коды

namespace TradingStrategies.Utilities
{
    //core
    internal sealed partial class LiteDictionary<TKey, TValue> where TKey : notnull
    {
        private struct Entry
        {
            public TKey key;
            public TValue value;
            public int hash;
            public int next;
        }

        private readonly Entry[] _entries;
        private readonly int _size;

        public LiteDictionary(IReadOnlyList<TKey> keys, IReadOnlyList<TValue> items)
        {
            if (keys.Count != items.Count)
            {
                throw new ArgumentException($"{nameof(keys)} and {nameof(items)} must have same length");
            }

            _size = keys.Count;

            var count = _size;
            _entries = new Entry[count * 2];    //можно оптимизнуть память, но стоит ли
            var freeBuffer = count;

            for (int i = 0; i < count; i++)
            {
                var key = keys[i];
                var hash = key.GetHashCode();

                if (hash <= 0)
                {
                    throw new ArgumentException($"{nameof(keys)} must have positive hash codes, key: {key}");
                }

                var bucket = hash % count;
                ref var entry = ref _entries[bucket];

                while (entry.hash != 0)
                {
                    if (entry.hash == hash)
                    {
                        throw new ArgumentException($"{nameof(keys)} must have unique hash codes, key: {key}");
                    }

                    if (entry.next != 0)
                    {
                        entry = ref _entries[entry.next];
                        continue;
                    }
                    else
                    {
                        entry.next = freeBuffer;
                    }

                    entry = ref _entries[freeBuffer];
                    freeBuffer++;
                }
                entry.hash = hash;
                entry.value = items[i];
                entry.key = key;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TValue Find(TKey key)
        {
            var hash = key.GetHashCode();
            var bucket = hash % _size;
            ref var entry = ref _entries[bucket];

            while (entry.hash != hash)
            {
                if (entry.next != 0)
                {
                    entry = ref _entries[entry.next];
                }
                else
                {
                    throw new KeyNotFoundException($"key {key} does not exist");
                }
            }
            return entry.value;
        }
    }

    //adapter
    internal sealed partial class LiteDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    {
        //hot path
        public TValue this[TKey key] => Find(key);

        //appendix
        public int Count => _size;

        public bool TryGetValue(TKey key, out TValue value)
        {
            try
            {
                value = Find(key);
                return true;
            }
            catch (KeyNotFoundException)
            {
                value = default!;
                return false;
            }
        }

        public bool ContainsKey(TKey key) => TryGetValue(key, out _);

        public IEnumerable<TKey> Keys => this.Select(x => x.Key);
        public IEnumerable<TValue> Values => this.Select(x => x.Value);

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => new Enumerator(this);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            private readonly LiteDictionary<TKey, TValue> _dictionary;
            private int _index;
            private KeyValuePair<TKey, TValue> _current;

            public Enumerator(LiteDictionary<TKey, TValue> dictionary)
            {
                _dictionary = dictionary;
                _current = default;
                _index = 0;
            }

            public readonly KeyValuePair<TKey, TValue> Current => _current;
            readonly object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                var entries = _dictionary._entries;

                while (_index < _dictionary._entries.Length)
                {
                    var entry = _dictionary._entries[_index++];

                    if (entry.hash >= 0)
                    {
                        _current = new KeyValuePair<TKey, TValue>(entry.key, entry.value);
                        return true;
                    }
                }

                return false;
            }

            public readonly void Dispose()
            {
            }

            public void Reset()
            {
                _index = 0;
                _current = default;
            }
        }
    }
}
