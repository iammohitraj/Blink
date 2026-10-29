// Blink · mohit

using System.Collections;

namespace Blink.Core.Collections;

/// <summary>
/// Growable array with amortised O(1) append, O(1) random access and O(n) insert/remove.
/// </summary>
public sealed class DynamicArray<T> : IReadOnlyList<T>
{
    private const int DefaultCapacity = 4;

    private T[] items;
    private int count;

    public DynamicArray(int capacity = DefaultCapacity)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        items = capacity == 0 ? Array.Empty<T>() : new T[capacity];
    }

    public int Count => count;

    public int Capacity => items.Length;

    public T this[int index]
    {
        get
        {
            CheckIndex(index);
            return items[index];
        }
        set
        {
            CheckIndex(index);
            items[index] = value;
        }
    }

    public void Add(T item)
    {
        if (count == items.Length)
        {
            Grow(count + 1);
        }

        items[count++] = item;
    }

    public void Insert(int index, T item)
    {
        if ((uint)index > (uint)count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (count == items.Length)
        {
            Grow(count + 1);
        }

        if (index < count)
        {
            Array.Copy(items, index, items, index + 1, count - index);
        }

        items[index] = item;
        count++;
    }

    public bool Remove(T item)
    {
        int index = IndexOf(item);
        if (index < 0)
        {
            return false;
        }

        RemoveAt(index);
        return true;
    }

    public void RemoveAt(int index)
    {
        CheckIndex(index);
        count--;

        if (index < count)
        {
            Array.Copy(items, index + 1, items, index, count - index);
        }

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            items[count] = default!;
        }
    }

    public int IndexOf(T item) => Array.IndexOf(items, item, 0, count);

    public bool Contains(T item) => IndexOf(item) >= 0;

    public void Clear()
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Array.Clear(items, 0, count);
        }

        count = 0;
    }

    public void TrimExcess()
    {
        if (count == items.Length)
        {
            return;
        }

        if (count == 0)
        {
            items = Array.Empty<T>();
            return;
        }

        Array.Resize(ref items, count);
    }

    public T[] ToArray()
    {
        if (count == 0)
        {
            return Array.Empty<T>();
        }

        T[] result = new T[count];
        Array.Copy(items, result, count);
        return result;
    }

    public Span<T> AsSpan() => items.AsSpan(0, count);

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < count; i++)
        {
            yield return items[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void Grow(int minimum)
    {
        int next = Math.Max(minimum, items.Length == 0 ? DefaultCapacity : items.Length * 2);
        Array.Resize(ref items, next);
    }

    private void CheckIndex(int index)
    {
        if ((uint)index >= (uint)count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
