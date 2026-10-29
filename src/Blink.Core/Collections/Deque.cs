// Blink · mohit

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Blink.Core.Collections;

/// <summary>
/// Double-ended queue backed by a circular array. Both ends are amortised O(1)
/// and the buffer doubles only when it fills up.
/// </summary>
public sealed class Deque<T> : IReadOnlyList<T>
{
    private T[] items;
    private int head;
    private int count;

    public Deque(int capacity = 4)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        items = capacity == 0 ? Array.Empty<T>() : new T[capacity];
    }

    public int Count => count;

    public bool IsEmpty => count == 0;

    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return items[(head + index) % items.Length];
        }
    }

    public void PushFront(T item)
    {
        if (count == items.Length)
        {
            Grow();
        }

        head = (head - 1 + items.Length) % items.Length;
        items[head] = item;
        count++;
    }

    public void PushBack(T item)
    {
        if (count == items.Length)
        {
            Grow();
        }

        items[(head + count) % items.Length] = item;
        count++;
    }

    public T PopFront()
    {
        if (!TryPopFront(out T? item))
        {
            throw new InvalidOperationException("The deque is empty.");
        }

        return item;
    }

    public T PopBack()
    {
        if (!TryPopBack(out T? item))
        {
            throw new InvalidOperationException("The deque is empty.");
        }

        return item;
    }

    public bool TryPopFront([MaybeNullWhen(false)] out T item)
    {
        if (count == 0)
        {
            item = default;
            return false;
        }

        item = items[head];
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            items[head] = default!;
        }

        head = (head + 1) % items.Length;
        count--;
        return true;
    }

    public bool TryPopBack([MaybeNullWhen(false)] out T item)
    {
        if (count == 0)
        {
            item = default;
            return false;
        }

        int index = (head + count - 1) % items.Length;
        item = items[index];
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            items[index] = default!;
        }

        count--;
        return true;
    }

    public T PeekFront()
    {
        if (count == 0)
        {
            throw new InvalidOperationException("The deque is empty.");
        }

        return items[head];
    }

    public T PeekBack()
    {
        if (count == 0)
        {
            throw new InvalidOperationException("The deque is empty.");
        }

        return items[(head + count - 1) % items.Length];
    }

    public void Clear()
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            for (int i = 0; i < count; i++)
            {
                items[(head + i) % items.Length] = default!;
            }
        }

        head = 0;
        count = 0;
    }

    public T[] ToArray()
    {
        T[] result = new T[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = items[(head + i) % items.Length];
        }

        return result;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < count; i++)
        {
            yield return items[(head + i) % items.Length];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void Grow()
    {
        int capacity = items.Length == 0 ? 4 : items.Length * 2;
        T[] next = new T[capacity];

        for (int i = 0; i < count; i++)
        {
            next[i] = items[(head + i) % items.Length];
        }

        items = next;
        head = 0;
    }
}
