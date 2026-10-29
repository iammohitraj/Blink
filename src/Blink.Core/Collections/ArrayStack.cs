// Blink · mohit

using System.Diagnostics.CodeAnalysis;

namespace Blink.Core.Collections;

/// <summary>Array-backed LIFO stack. Push/pop are amortised O(1).</summary>
public sealed class ArrayStack<T>
{
    private T[] items;
    private int count;

    public ArrayStack(int capacity = 4)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        items = capacity == 0 ? Array.Empty<T>() : new T[capacity];
    }

    public int Count => count;

    public bool IsEmpty => count == 0;

    public void Push(T item)
    {
        if (count == items.Length)
        {
            Array.Resize(ref items, items.Length == 0 ? 4 : items.Length * 2);
        }

        items[count++] = item;
    }

    public T Pop()
    {
        if (!TryPop(out T? item))
        {
            throw new InvalidOperationException("The stack is empty.");
        }

        return item;
    }

    public bool TryPop([MaybeNullWhen(false)] out T item)
    {
        if (count == 0)
        {
            item = default;
            return false;
        }

        count--;
        item = items[count];

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            items[count] = default!;
        }

        return true;
    }

    public T Peek()
    {
        if (!TryPeek(out T? item))
        {
            throw new InvalidOperationException("The stack is empty.");
        }

        return item;
    }

    public bool TryPeek([MaybeNullWhen(false)] out T item)
    {
        if (count == 0)
        {
            item = default;
            return false;
        }

        item = items[count - 1];
        return true;
    }

    public void Clear()
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Array.Clear(items, 0, count);
        }

        count = 0;
    }

    public T[] ToArray()
    {
        T[] result = new T[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = items[count - 1 - i];
        }

        return result;
    }
}
