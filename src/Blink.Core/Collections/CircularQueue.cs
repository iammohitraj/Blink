// Blink · mohit

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Blink.Core.Collections;

/// <summary>FIFO queue built on <see cref="Deque{T}"/>. Enqueue and dequeue are amortised O(1).</summary>
public sealed class CircularQueue<T> : IReadOnlyCollection<T>
{
    private readonly Deque<T> items = new();

    public int Count => items.Count;

    public bool IsEmpty => items.IsEmpty;

    public void Enqueue(T item) => items.PushBack(item);

    public T Dequeue() => items.PopFront();

    public bool TryDequeue([MaybeNullWhen(false)] out T item) => items.TryPopFront(out item);

    public T Peek() => items.PeekFront();

    public bool TryPeek([MaybeNullWhen(false)] out T item)
    {
        if (items.IsEmpty)
        {
            item = default;
            return false;
        }

        item = items.PeekFront();
        return true;
    }

    public void Clear() => items.Clear();

    public T[] ToArray() => items.ToArray();

    public IEnumerator<T> GetEnumerator() => items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
