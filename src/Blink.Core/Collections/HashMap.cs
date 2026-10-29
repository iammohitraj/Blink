// Blink · mohit

using System.Collections;

namespace Blink.Core.Collections;

/// <summary>
/// Open-addressed hash map with linear probing. Entries live inline, which avoids the
/// per-entry node allocation a chained map needs. Average lookup, insert and remove are O(1).
/// </summary>
public sealed class HashMap<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    where TKey : notnull
{
    private const byte Empty = 0;
    private const byte Used = 1;
    private const byte Deleted = 2;
    private const double LoadFactor = 0.7;

    private struct Entry
    {
        public TKey Key;
        public TValue Value;
        public int Hash;
        public byte State;
    }

    private readonly IEqualityComparer<TKey> comparer;
    private Entry[] entries;
    private int count;
    private int used;

    public HashMap(int capacity = 16, IEqualityComparer<TKey>? comparer = null)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        this.comparer = comparer ?? EqualityComparer<TKey>.Default;

        int size = 4;
        while (size < capacity)
        {
            size <<= 1;
        }

        entries = new Entry[size];
    }

    public int Count => count;

    public bool IsEmpty => count == 0;

    public TValue this[TKey key]
    {
        get => TryGetValue(key, out TValue value)
            ? value
            : throw new KeyNotFoundException($"The key '{key}' was not found.");
        set => Insert(key, value, throwIfExists: false);
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);

        int hash = Hash(key);
        int mask = entries.Length - 1;
        int index = hash & mask;

        for (int probes = 0; probes < entries.Length; probes++)
        {
            ref Entry entry = ref entries[index];

            if (entry.State == Empty)
            {
                value = default!;
                return false;
            }

            if (entry.State == Used && entry.Hash == hash && comparer.Equals(entry.Key, key))
            {
                value = entry.Value;
                return true;
            }

            index = (index + 1) & mask;
        }

        value = default!;
        return false;
    }

    public bool ContainsKey(TKey key) => TryGetValue(key, out _);

    public void Add(TKey key, TValue value) => Insert(key, value, throwIfExists: true);

    public bool TryAdd(TKey key, TValue value) => !ContainsKey(key) && InsertIfMissing(key, value);

    public bool Remove(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        int hash = Hash(key);
        int mask = entries.Length - 1;
        int index = hash & mask;

        for (int probes = 0; probes < entries.Length; probes++)
        {
            ref Entry entry = ref entries[index];

            if (entry.State == Empty)
            {
                return false;
            }

            if (entry.State == Used && entry.Hash == hash && comparer.Equals(entry.Key, key))
            {
                entry.State = Deleted;
                entry.Key = default!;
                entry.Value = default!;
                count--;
                return true;
            }

            index = (index + 1) & mask;
        }

        return false;
    }

    public void Clear()
    {
        Array.Clear(entries, 0, entries.Length);
        count = 0;
        used = 0;
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].State == Used)
            {
                yield return new KeyValuePair<TKey, TValue>(entries[i].Key, entries[i].Value);
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private bool InsertIfMissing(TKey key, TValue value)
    {
        Insert(key, value, throwIfExists: false);
        return true;
    }

    private void Insert(TKey key, TValue value, bool throwIfExists)
    {
        ArgumentNullException.ThrowIfNull(key);
        EnsureCapacity();

        int hash = Hash(key);
        int mask = entries.Length - 1;
        int index = hash & mask;
        int firstDeleted = -1;

        for (int probes = 0; probes < entries.Length; probes++)
        {
            ref Entry entry = ref entries[index];

            if (entry.State == Deleted)
            {
                if (firstDeleted < 0)
                {
                    firstDeleted = index;
                }
            }
            else if (entry.State == Used)
            {
                if (entry.Hash == hash && comparer.Equals(entry.Key, key))
                {
                    if (throwIfExists)
                    {
                        throw new ArgumentException("An item with the same key already exists.", nameof(key));
                    }

                    entry.Value = value;
                    return;
                }
            }
            else
            {
                int target = firstDeleted >= 0 ? firstDeleted : index;
                ref Entry slot = ref entries[target];
                slot.Key = key;
                slot.Value = value;
                slot.Hash = hash;
                slot.State = Used;
                count++;

                if (firstDeleted < 0)
                {
                    used++;
                }

                return;
            }

            index = (index + 1) & mask;
        }

        Resize(entries.Length * 2);
        Insert(key, value, throwIfExists);
    }

    private void EnsureCapacity()
    {
        if ((used + 1) <= entries.Length * LoadFactor)
        {
            return;
        }

        int next = (count + 1) > entries.Length * LoadFactor ? entries.Length * 2 : entries.Length;
        Resize(next);
    }

    private void Resize(int capacity)
    {
        Entry[] previous = entries;
        entries = new Entry[capacity];
        count = 0;
        used = 0;

        foreach (Entry entry in previous)
        {
            if (entry.State == Used)
            {
                Insert(entry.Key, entry.Value, throwIfExists: false);
            }
        }
    }

    private int Hash(TKey key) => comparer.GetHashCode(key) & 0x7FFFFFFF;
}
