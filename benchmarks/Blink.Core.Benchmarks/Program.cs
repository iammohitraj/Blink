// Blink · mohit

using System.Diagnostics;
using Blink.Core;
using Blink.Core.Algorithms;
using Blink.Core.Collections;

namespace Blink.Core.Benchmarks;

/// <summary>
/// Small, dependency-free benchmark harness. It measures wall-clock time and bytes
/// allocated on the calling thread, which is enough to catch accidental O(n²) work or
/// hidden allocations without pulling in BenchmarkDotNet.
/// </summary>
internal static class Program
{
    private const int CollectionSize = 200_000;
    private const int SortSize = 100_000;

    private static int Main()
    {
        Console.WriteLine($"Blink.Core benchmarks — .NET {Environment.Version}, {Environment.ProcessorCount} cores");
        Console.WriteLine(new string('-', 78));
        Console.WriteLine($"{"benchmark",-42}{"time",12}{"allocated",16}");
        Console.WriteLine(new string('-', 78));

        Measure("DynamicArray.Add", CollectionSize, () =>
        {
            DynamicArray<int> list = new(CollectionSize);
            for (int i = 0; i < CollectionSize; i++)
            {
                list.Add(i);
            }

            return list.Count;
        });

        Measure("List<T>.Add", CollectionSize, () =>
        {
            List<int> list = new(CollectionSize);
            for (int i = 0; i < CollectionSize; i++)
            {
                list.Add(i);
            }

            return list.Count;
        });

        Measure("HashMap insert + lookup", CollectionSize, () =>
        {
            HashMap<int, int> map = new();
            for (int i = 0; i < CollectionSize; i++)
            {
                map.Add(i, i);
            }

            int sum = 0;
            for (int i = 0; i < CollectionSize; i++)
            {
                if (map.TryGetValue(i, out int value))
                {
                    sum += value;
                }
            }

            return sum;
        });

        Measure("Dictionary insert + lookup", CollectionSize, () =>
        {
            Dictionary<int, int> map = new();
            for (int i = 0; i < CollectionSize; i++)
            {
                map.Add(i, i);
            }

            int sum = 0;
            for (int i = 0; i < CollectionSize; i++)
            {
                if (map.TryGetValue(i, out int value))
                {
                    sum += value;
                }
            }

            return sum;
        });

        Measure("BinaryHeap push + pop", CollectionSize, () =>
        {
            BinaryHeap<int> heap = new();
            for (int i = 0; i < CollectionSize; i++)
            {
                heap.Push((i * 1_103_515_245 + 12345) & 0xFFFF);
            }

            long sum = 0;
            while (heap.TryPop(out int value))
            {
                sum += value;
            }

            return sum;
        });

        Measure("PriorityQueue push + dequeue", CollectionSize, () =>
        {
            PriorityQueue<int, int> queue = new();
            for (int i = 0; i < CollectionSize; i++)
            {
                int key = (i * 1_103_515_245 + 12345) & 0xFFFF;
                queue.Enqueue(key, key);
            }

            long sum = 0;
            while (queue.TryDequeue(out int value, out _))
            {
                sum += value;
            }

            return sum;
        });

        int[] source = CreateRandomValues(SortSize);

        Measure("MergeSort", SortSize, () => RunSort(source, static span => Sorting.MergeSort(span)));
        Measure("QuickSort", SortSize, () => RunSort(source, static span => Sorting.QuickSort(span)));
        Measure("HeapSort", SortSize, () => RunSort(source, static span => Sorting.HeapSort(span)));
        Measure("Array.Sort", SortSize, () =>
        {
            int[] copy = (int[])source.Clone();
            Array.Sort(copy);
            return copy[^1];
        });

        Measure("LruCache warm hits", 100_000, () =>
        {
            LruCache<int, int> cache = new(128);
            for (int i = 0; i < 128; i++)
            {
                cache.Set(i, i);
            }

            int sum = 0;
            for (int i = 0; i < 100_000; i++)
            {
                if (cache.TryGet(i & 127, out int value))
                {
                    sum += value;
                }
            }

            return sum;
        });

        Measure("SemanticVersion.Parse", 100_000, () =>
        {
            int parsed = 0;
            for (int i = 0; i < 100_000; i++)
            {
                if (SemanticVersion.TryParse("v1.2.3-beta.4+build.7", out _))
                {
                    parsed++;
                }
            }

            return parsed;
        });

        Console.WriteLine(new string('-', 78));
        return 0;
    }

    private delegate void SpanSort(Span<int> items);

    private static int RunSort(int[] source, SpanSort sort)
    {
        int[] copy = (int[])source.Clone();
        sort(copy.AsSpan());
        return copy[^1];
    }

    private static int[] CreateRandomValues(int size)
    {
        int[] values = new int[size];
        Random random = new(20261030);
        for (int i = 0; i < size; i++)
        {
            values[i] = random.Next();
        }

        return values;
    }

    private static void Measure(string name, int operations, Func<long> body)
    {
        // One warm-up pass lets the JIT compile the generic code paths first.
        body();

        long before = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch stopwatch = Stopwatch.StartNew();
        long result = body();
        stopwatch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        string time = $"{stopwatch.Elapsed.TotalMilliseconds,9:F2} ms";
        string memory = $"{allocated / 1024d,13:F1} KB";
        Console.WriteLine($"{name,-42}{time}{memory}   ({operations:N0} ops, checksum {result})");
    }
}
