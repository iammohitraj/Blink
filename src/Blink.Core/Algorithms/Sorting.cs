// Blink · mohit

using System.Buffers;

namespace Blink.Core.Algorithms;

/// <summary>
/// In-place and stable sorting routines over spans. Every method accepts an optional
/// comparer so callers can sort records without allocating wrapper objects.
/// </summary>
public static class Sorting
{
    public static void InsertionSort<T>(Span<T> items, IComparer<T>? comparer = null)
    {
        comparer ??= Comparer<T>.Default;

        for (int i = 1; i < items.Length; i++)
        {
            T key = items[i];
            int j = i - 1;

            while (j >= 0 && comparer.Compare(items[j], key) > 0)
            {
                items[j + 1] = items[j];
                j--;
            }

            items[j + 1] = key;
        }
    }

    public static void SelectionSort<T>(Span<T> items, IComparer<T>? comparer = null)
    {
        comparer ??= Comparer<T>.Default;

        for (int i = 0; i < items.Length - 1; i++)
        {
            int smallest = i;

            for (int j = i + 1; j < items.Length; j++)
            {
                if (comparer.Compare(items[j], items[smallest]) < 0)
                {
                    smallest = j;
                }
            }

            if (smallest != i)
            {
                Swap(items, i, smallest);
            }
        }
    }

    public static void BubbleSort<T>(Span<T> items, IComparer<T>? comparer = null)
    {
        comparer ??= Comparer<T>.Default;

        for (int end = items.Length - 1; end > 0; end--)
        {
            bool swapped = false;

            for (int i = 0; i < end; i++)
            {
                if (comparer.Compare(items[i], items[i + 1]) <= 0)
                {
                    continue;
                }

                Swap(items, i, i + 1);
                swapped = true;
            }

            if (!swapped)
            {
                return;
            }
        }
    }

    public static void MergeSort<T>(Span<T> items, IComparer<T>? comparer = null)
    {
        if (items.Length < 2)
        {
            return;
        }

        comparer ??= Comparer<T>.Default;

        T[] buffer = ArrayPool<T>.Shared.Rent(items.Length);
        try
        {
            MergeSort(items, buffer.AsSpan(0, items.Length), 0, items.Length - 1, comparer);
        }
        finally
        {
            ArrayPool<T>.Shared.Return(buffer, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }

    public static void QuickSort<T>(Span<T> items, IComparer<T>? comparer = null)
    {
        if (items.Length < 2)
        {
            return;
        }

        QuickSort(items, 0, items.Length - 1, comparer ?? Comparer<T>.Default);
    }

    public static void HeapSort<T>(Span<T> items, IComparer<T>? comparer = null)
    {
        comparer ??= Comparer<T>.Default;
        int length = items.Length;

        for (int i = (length / 2) - 1; i >= 0; i--)
        {
            SiftDown(items, i, length, comparer);
        }

        for (int end = length - 1; end > 0; end--)
        {
            Swap(items, 0, end);
            SiftDown(items, 0, end, comparer);
        }
    }

    /// <summary>O(n + k) counting sort. Uses the value range, so keep it for compact integers.</summary>
    public static void CountingSort(Span<int> items)
    {
        if (items.Length < 2)
        {
            return;
        }

        int minimum = items[0];
        int maximum = items[0];

        foreach (int value in items)
        {
            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
        }

        long range = (long)maximum - minimum + 1;
        if (range > 10_000_000)
        {
            throw new ArgumentException("The value range is too wide for a counting sort.", nameof(items));
        }

        int[] counts = new int[(int)range];

        foreach (int value in items)
        {
            counts[value - minimum]++;
        }

        int index = 0;
        for (int value = 0; value < counts.Length; value++)
        {
            for (int repeat = 0; repeat < counts[value]; repeat++)
            {
                items[index++] = value + minimum;
            }
        }
    }

    /// <summary>LSD radix sort for non-negative integers. Four byte passes, O(n) extra space.</summary>
    public static void RadixSort(Span<int> items)
    {
        if (items.Length < 2)
        {
            return;
        }

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(items), "Radix sort only supports non-negative integers.");
            }
        }

        int[] source = items.ToArray();
        int[] target = new int[items.Length];
        int[] counts = new int[256];

        for (int shift = 0; shift < 32; shift += 8)
        {
            Array.Clear(counts, 0, counts.Length);

            foreach (int value in source)
            {
                counts[(value >> shift) & 0xFF]++;
            }

            int position = 0;
            for (int bucket = 0; bucket < counts.Length; bucket++)
            {
                int size = counts[bucket];
                counts[bucket] = position;
                position += size;
            }

            foreach (int value in source)
            {
                int bucket = (value >> shift) & 0xFF;
                target[counts[bucket]++] = value;
            }

            (source, target) = (target, source);
        }

        source.CopyTo(items);
    }

    public static bool IsSorted<T>(ReadOnlySpan<T> items, IComparer<T>? comparer = null)
    {
        comparer ??= Comparer<T>.Default;

        for (int i = 1; i < items.Length; i++)
        {
            if (comparer.Compare(items[i - 1], items[i]) > 0)
            {
                return false;
            }
        }

        return true;
    }

    private static void MergeSort<T>(Span<T> items, Span<T> buffer, int low, int high, IComparer<T> comparer)
    {
        if (low >= high)
        {
            return;
        }

        int middle = low + ((high - low) >> 1);
        MergeSort(items, buffer, low, middle, comparer);
        MergeSort(items, buffer, middle + 1, high, comparer);

        if (comparer.Compare(items[middle], items[middle + 1]) <= 0)
        {
            return;
        }

        int left = low;
        int right = middle + 1;
        int output = low;

        while (left <= middle && right <= high)
        {
            buffer[output++] = comparer.Compare(items[left], items[right]) <= 0
                ? items[left++]
                : items[right++];
        }

        while (left <= middle)
        {
            buffer[output++] = items[left++];
        }

        while (right <= high)
        {
            buffer[output++] = items[right++];
        }

        buffer[low..(high + 1)].CopyTo(items[low..(high + 1)]);
    }

    private static void QuickSort<T>(Span<T> items, int low, int high, IComparer<T> comparer)
    {
        while (low < high)
        {
            if (high - low < 16)
            {
                InsertionSort(items[low..(high + 1)], comparer);
                return;
            }

            T pivot = items[low + ((high - low) >> 1)];
            int less = low;
            int index = low;
            int greater = high;

            while (index <= greater)
            {
                int comparison = comparer.Compare(items[index], pivot);

                if (comparison < 0)
                {
                    Swap(items, less++, index++);
                }
                else if (comparison > 0)
                {
                    Swap(items, index, greater--);
                }
                else
                {
                    index++;
                }
            }

            if (less - low < high - greater)
            {
                QuickSort(items, low, less - 1, comparer);
                low = greater + 1;
            }
            else
            {
                QuickSort(items, greater + 1, high, comparer);
                high = less - 1;
            }
        }
    }

    private static void SiftDown<T>(Span<T> items, int root, int length, IComparer<T> comparer)
    {
        while (true)
        {
            int child = (root * 2) + 1;
            if (child >= length)
            {
                return;
            }

            int right = child + 1;
            if (right < length && comparer.Compare(items[right], items[child]) > 0)
            {
                child = right;
            }

            if (comparer.Compare(items[root], items[child]) >= 0)
            {
                return;
            }

            Swap(items, root, child);
            root = child;
        }
    }

    private static void Swap<T>(Span<T> items, int first, int second) =>
        (items[first], items[second]) = (items[second], items[first]);
}
