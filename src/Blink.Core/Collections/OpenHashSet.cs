// Blink · mohit

using System.Collections;

namespace Blink.Core.Collections;

/// <summary>Set built on <see cref="HashMap{TKey,TValue}"/> with a byte payload, so it shares the map's O(1) behaviour.</summary>
public sealed class OpenHashSet<T> : IEnumerable<T>
    where T : notnull
{
    private readonly HashMap<T, byte> map;

    public OpenHashSet(int capacity = 16, IEqualityComparer<T>? comparer = null) =>
        map = new HashMap<T, byte>(capacity, comparer);

    public int Count => map.Count;

    public bool Add(T item) => map.TryAdd(item, 0);

    public bool Contains(T item) => map.ContainsKey(item);

    public bool Remove(T item) => map.Remove(item);

    public void Clear() => map.Clear();

    public IEnumerator<T> GetEnumerator()
    {
        foreach (KeyValuePair<T, byte> pair in map)
        {
            yield return pair.Key;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
