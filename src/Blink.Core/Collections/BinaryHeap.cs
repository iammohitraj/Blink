// Blink · mohit

using System.Diagnostics.CodeAnalysis;

namespace Blink.Core.Collections;

/// <summary>
/// Binary heap with O(log n) push/pop and O(1) peek. Works as a min-heap or max-heap
/// and can build a heap from an existing sequence in O(n).
/// </summary>
public sealed class BinaryHeap<T>
{
    private readonly IComparer<T> comparer;
    private readonly bool minHeap;
    private T[] items;
    private int count;

    public BinaryHeap(bool minHeap = true, IComparer<T>? comparer = null)
    {
        this.minHeap = minHeap;
        this.comparer = comparer ?? Comparer<T>.Default;
        items = new T[8];
    }

    public BinaryHeap(IEnumerable<T> source, bool minHeap = true, IComparer<T>? comparer = null)
        : this(minHeap, comparer)
    {
        items = source.ToArray();
        count = items.Length;

        for (int i = (count / 2) - 1; i >= 0; i--)
        {
            SiftDown(i);
        }
    }

    public int Count => count;

    public bool IsEmpty => count == 0;

    public T Peek()
    {
        if (count == 0)
        {
            throw new InvalidOperationException("The heap is empty.");
        }

        return items[0];
    }

    public void Push(T item)
    {
        if (count == items.Length)
        {
            Array.Resize(ref items, items.Length * 2);
        }

        items[count] = item;
        SiftUp(count);
        count++;
    }

    public T Pop()
    {
        if (!TryPop(out T? item))
        {
            throw new InvalidOperationException("The heap is empty.");
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

        item = items[0];
        count--;

        if (count > 0)
        {
            items[0] = items[count];
            SiftDown(0);
        }

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            items[count] = default!;
        }

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

    private void SiftUp(int index)
    {
        while (index > 0)
        {
            int parent = (index - 1) / 2;
            if (Compare(items[parent], items[index]) <= 0)
            {
                return;
            }

            Swap(parent, index);
            index = parent;
        }
    }

    private void SiftDown(int index)
    {
        while (true)
        {
            int left = (index * 2) + 1;
            if (left >= count)
            {
                return;
            }

            int right = left + 1;
            int smallest = right < count && Compare(items[right], items[left]) < 0 ? right : left;

            if (Compare(items[index], items[smallest]) <= 0)
            {
                return;
            }

            Swap(index, smallest);
            index = smallest;
        }
    }

    private int Compare(T left, T right) =>
        minHeap ? comparer.Compare(left, right) : comparer.Compare(right, left);

    private void Swap(int first, int second) => (items[first], items[second]) = (items[second], items[first]);
}
