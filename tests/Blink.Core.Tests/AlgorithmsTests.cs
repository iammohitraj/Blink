// Blink · mohit

using Blink.Core.Algorithms;

namespace Blink.Core.Tests;

public sealed class AlgorithmsTests
{
    [Fact]
    public void ComparisonSorts_MatchTheReferenceImplementation()
    {
        int[] source = CreateRandomData(321, -500, 500);

        AssertSorts(source, items => Sorting.InsertionSort(items.AsSpan()));
        AssertSorts(source, items => Sorting.SelectionSort(items.AsSpan()));
        AssertSorts(source, items => Sorting.BubbleSort(items.AsSpan()));
        AssertSorts(source, items => Sorting.MergeSort(items.AsSpan()));
        AssertSorts(source, items => Sorting.QuickSort(items.AsSpan()));
        AssertSorts(source, items => Sorting.HeapSort(items.AsSpan()));
    }

    [Fact]
    public void StableSorts_KeepEqualItemsInInsertionOrder()
    {
        Item[] items =
        {
            new(2, 0), new(1, 1), new(2, 2), new(1, 3), new(2, 4)
        };

        IComparer<Item> byKey = Comparer<Item>.Create((left, right) => left.Key.CompareTo(right.Key));

        Item[] merge = (Item[])items.Clone();
        Sorting.MergeSort(merge.AsSpan(), byKey);

        Item[] insertion = (Item[])items.Clone();
        Sorting.InsertionSort(insertion.AsSpan(), byKey);

        Assert.Equal(new[] { 1, 3, 0, 2, 4 }, merge.Select(item => item.Order));
        Assert.Equal(new[] { 1, 3, 0, 2, 4 }, insertion.Select(item => item.Order));
    }

    [Fact]
    public void CountingAndRadixSorts_HandleTheirInputRanges()
    {
        int[] withNegatives = { 4, -2, 0, 7, -9, 4, 1 };
        int[] expected = withNegatives.OrderBy(value => value).ToArray();

        int[] counting = (int[])withNegatives.Clone();
        Sorting.CountingSort(counting);
        Assert.Equal(expected, counting);

        int[] nonNegative = { 42, 7, 1000, 0, 7, 255, 256 };
        int[] expectedNonNegative = nonNegative.OrderBy(value => value).ToArray();

        int[] radix = (int[])nonNegative.Clone();
        Sorting.RadixSort(radix);
        Assert.Equal(expectedNonNegative, radix);
    }

    [Fact]
    public void SearchHelpers_RespectDuplicateBoundaries()
    {
        int[] values = { 1, 2, 3, 3, 3, 4 };

        Assert.Equal(2, Searching.LowerBound(values.AsSpan(), 3));
        Assert.Equal(5, Searching.UpperBound(values.AsSpan(), 3));
        Assert.Equal(-1, Searching.BinarySearch(values.AsSpan(), 9));

        int[] range = Enumerable.Range(0, 128).ToArray();
        Assert.Equal(0, Searching.ExponentialSearch(range.AsSpan(), 0));
        Assert.Equal(99, Searching.ExponentialSearch(range.AsSpan(), 99));
        Assert.Equal(-1, Searching.ExponentialSearch(range.AsSpan(), 500));
    }

    [Fact]
    public void StringSearchAlgorithms_FindEveryOccurrence()
    {
        const string text = "abababa";
        const string pattern = "aba";

        Assert.Equal(new[] { 0, 2, 4 }, StringAlgorithms.KmpSearch(text, pattern));
        Assert.Equal(new[] { 0, 2, 4 }, StringAlgorithms.RabinKarpSearch(text, pattern));
        Assert.Empty(StringAlgorithms.KmpSearch("abc", "zzz"));
        Assert.True(StringAlgorithms.IsPalindrome("racecar"));
        Assert.False(StringAlgorithms.IsPalindrome("blink"));
    }

    [Fact]
    public void MathHelpers_ProduceKnownValues()
    {
        Assert.Equal(6L, MathAlgorithms.Gcd(54, 24));
        Assert.Equal(216L, MathAlgorithms.Lcm(54, 24));
        Assert.Equal(24L, MathAlgorithms.ModPow(2, 10, 1000));
        Assert.True(MathAlgorithms.IsPrime(97));
        Assert.False(MathAlgorithms.IsPrime(1));
        Assert.Equal(55L, MathAlgorithms.Fibonacci(10));

        bool[] primes = MathAlgorithms.Sieve(30);
        int[] found = Enumerable.Range(0, 31).Where(value => primes[value]).ToArray();
        Assert.Equal(new[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29 }, found);
    }

    private static void AssertSorts(int[] source, Action<int[]> sort)
    {
        int[] copy = (int[])source.Clone();
        sort(copy);

        Assert.True(Sorting.IsSorted<int>(copy.AsSpan()));
        Assert.Equal(source.OrderBy(value => value), copy);
    }

    private static int[] CreateRandomData(int seed, int minimum, int maximum)
    {
        Random random = new(seed);
        int[] data = new int[257];

        for (int i = 0; i < data.Length; i++)
        {
            data[i] = random.Next(minimum, maximum);
        }

        return data;
    }

    private readonly record struct Item(int Key, int Order);
}
