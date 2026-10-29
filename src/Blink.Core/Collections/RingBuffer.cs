// Blink · mohit

using System.Collections;

namespace Blink.Core.Collections;

/// <summary>
/// Fixed-capacity circular buffer. Adding to a full buffer overwrites the oldest item,
/// so the newest N samples stay available without ever allocating again.
/// </summary>
public sealed class RingBuffer<T> : IReadOnlyList<T>
{
    private readonly T[] items;
    private int start;
    private int count;

    public RingBuffer(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        items = new T[capacity];
    }

    public int Capacity => items.Length;

    public int Count => count;

    public bool IsFull => count == items.Length;

    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return items[(start + index) % items.Length];
        }
    }

    public void Add(T item)
    {
        if (count < items.Length)
        {
            items[(start + count) % items.Length] = item;
            count++;
            return;
        }

        items[start] = item;
        start = (start + 1) % items.Length;
    }

    public void Clear()
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Array.Clear(items, 0, items.Length);
        }

        start = 0;
        count = 0;
    }

    public T[] ToArray()
    {
        T[] result = new T[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = this[i];
        }

        return result;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
