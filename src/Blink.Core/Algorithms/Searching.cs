// Blink · mohit

namespace Blink.Core.Algorithms;

/// <summary>Binary searching over sorted spans in O(log n).</summary>
public static class Searching
{
    public static int BinarySearch<T>(ReadOnlySpan<T> items, T value, IComparer<T>? comparer = null)
    {
        comparer ??= Comparer<T>.Default;
        int low = 0;
        int high = items.Length - 1;

        while (low <= high)
        {
            int middle = low + ((high - low) >> 1);
            int comparison = comparer.Compare(items[middle], value);

            if (comparison == 0)
            {
                return middle;
            }

            if (comparison < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return -1;
    }

    /// <summary>Index of the first item that is not less than <paramref name="value"/>.</summary>
    public static int LowerBound<T>(ReadOnlySpan<T> items, T value, IComparer<T>? comparer = null)
    {
        comparer ??= Comparer<T>.Default;
        int low = 0;
        int high = items.Length;

        while (low < high)
        {
            int middle = low + ((high - low) >> 1);

            if (comparer.Compare(items[middle], value) < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    /// <summary>Index of the first item that is greater than <paramref name="value"/>.</summary>
    public static int UpperBound<T>(ReadOnlySpan<T> items, T value, IComparer<T>? comparer = null)
    {
        comparer ??= Comparer<T>.Default;
        int low = 0;
        int high = items.Length;

        while (low < high)
        {
            int middle = low + ((high - low) >> 1);

            if (comparer.Compare(items[middle], value) <= 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    /// <summary>Doubles the search window first, then binary searches inside it. Useful for unbounded data.</summary>
    public static int ExponentialSearch<T>(ReadOnlySpan<T> items, T value, IComparer<T>? comparer = null)
    {
        comparer ??= Comparer<T>.Default;

        if (items.Length == 0)
        {
            return -1;
        }

        if (comparer.Compare(items[0], value) == 0)
        {
            return 0;
        }

        int bound = 1;
        while (bound < items.Length && comparer.Compare(items[bound], value) < 0)
        {
            bound <<= 1;
        }

        int low = bound >> 1;
        int high = Math.Min(bound, items.Length - 1);

        while (low <= high)
        {
            int middle = low + ((high - low) >> 1);
            int comparison = comparer.Compare(items[middle], value);

            if (comparison == 0)
            {
                return middle;
            }

            if (comparison < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return -1;
    }
}
