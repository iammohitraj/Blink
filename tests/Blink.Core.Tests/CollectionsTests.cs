// Blink · mohit

using Blink.Core.Collections;

namespace Blink.Core.Tests;

public sealed class CollectionsTests
{
    [Fact]
    public void DynamicArray_GrowsAndPreservesOrder()
    {
        DynamicArray<int> array = new(1);

        for (int i = 0; i < 10; i++)
        {
            array.Add(i);
        }

        Assert.Equal(10, array.Count);
        Assert.True(array.Capacity >= 10);
        Assert.Equal(4, array[4]);

        array.Insert(0, -1);
        Assert.Equal(-1, array[0]);
        Assert.Equal(5, array[6]);
        Assert.True(array.Remove(5));
        Assert.False(array.Contains(5));

        array.RemoveAt(0);
        Assert.Equal(0, array[0]);

        array.Clear();
        Assert.Empty(array);
    }

    [Fact]
    public void ArrayStack_IsLastInFirstOut()
    {
        ArrayStack<int> stack = new();
        stack.Push(1);
        stack.Push(2);

        Assert.Equal(2, stack.Peek());
        Assert.Equal(2, stack.Pop());
        Assert.Equal(1, stack.Pop());
        Assert.True(stack.IsEmpty);
        Assert.False(stack.TryPop(out _));
    }

    [Fact]
    public void CircularQueue_WrapsAroundTheBuffer()
    {
        CircularQueue<int> queue = new();

        for (int i = 1; i <= 5; i++)
        {
            queue.Enqueue(i);
        }

        Assert.Equal(1, queue.Dequeue());
        Assert.Equal(2, queue.Dequeue());
        queue.Enqueue(6);
        queue.Enqueue(7);

        Assert.Equal(new[] { 3, 4, 5, 6, 7 }, queue.ToArray());
        Assert.Equal(3, queue.Peek());
    }

    [Fact]
    public void Deque_PushesAndPopsAtBothEnds()
    {
        Deque<int> deque = new(1);
        deque.PushBack(2);
        deque.PushBack(3);
        deque.PushFront(1);

        Assert.Equal(new[] { 1, 2, 3 }, deque.ToArray());
        Assert.Equal(1, deque.PopFront());
        Assert.Equal(3, deque.PopBack());
        Assert.Equal(2, deque.PeekFront());
        Assert.Equal(2, deque[0]);
    }

    [Fact]
    public void RingBuffer_OverwritesTheOldestItem()
    {
        RingBuffer<int> ring = new(3);

        for (int i = 1; i <= 5; i++)
        {
            ring.Add(i);
        }

        Assert.True(ring.IsFull);
        Assert.Equal(new[] { 3, 4, 5 }, ring.ToArray());
        Assert.Equal(4, ring[1]);
    }

    [Fact]
    public void BinaryHeap_OrdersItemsInBothDirections()
    {
        int[] values = { 5, 1, 4, 2, 3 };

        BinaryHeap<int> min = new();
        BinaryHeap<int> max = new(minHeap: false);

        foreach (int value in values)
        {
            min.Push(value);
            max.Push(value);
        }

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, Drain(min));
        Assert.Equal(new[] { 5, 4, 3, 2, 1 }, Drain(max));
        Assert.Equal(1, new BinaryHeap<int>(values).Peek());
    }

    [Fact]
    public void HashMap_HandlesGrowthUpdatesAndRemoval()
    {
        HashMap<string, int> map = new(2);

        Assert.True(map.TryAdd("a", 1));
        Assert.False(map.TryAdd("a", 2));
        map["b"] = 2;
        Assert.Equal(2, map["b"]);

        Assert.True(map.Remove("a"));
        Assert.False(map.Remove("a"));
        Assert.False(map.ContainsKey("a"));

        for (int i = 0; i < 500; i++)
        {
            map[$"k{i}"] = i;
        }

        Assert.Equal(501, map.Count);
        Assert.Equal(42, map["k42"]);
        Assert.True(map.TryGetValue("k499", out int last));
        Assert.Equal(499, last);

        map["k42"] = 99;
        Assert.Equal(99, map["k42"]);
        Assert.False(map.TryGetValue("missing", out _));
    }

    [Fact]
    public void OpenHashSet_RejectsDuplicates()
    {
        OpenHashSet<string> set = new();

        Assert.True(set.Add("blink"));
        Assert.False(set.Add("blink"));
        Assert.True(set.Contains("blink"));
        Assert.True(set.Remove("blink"));
        Assert.False(set.Contains("blink"));
        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void LruCache_EvictsTheLeastRecentlyUsedEntry()
    {
        LruCache<int, string> cache = new(2);
        cache.Set(1, "a");
        cache.Set(2, "b");

        Assert.True(cache.TryGet(1, out _));
        cache.Set(3, "c");

        Assert.False(cache.TryGet(2, out _));
        Assert.True(cache.TryGet(1, out _));
        Assert.True(cache.TryGet(3, out _));
        Assert.Equal(2, cache.Count);

        Assert.Equal("d", cache.GetOrAdd(4, _ => "d"));
        Assert.True(cache.Remove(3));
        Assert.False(cache.Remove(3));
    }

    [Fact]
    public void UnionFind_TracksComponents()
    {
        UnionFind sets = new(6);

        Assert.True(sets.Union(0, 1));
        Assert.True(sets.Union(1, 2));
        Assert.False(sets.Union(2, 0));

        Assert.True(sets.Connected(0, 2));
        Assert.False(sets.Connected(0, 3));
        Assert.Equal(3, sets.SetSize(2));
        Assert.Equal(4, sets.Count);
    }

    private static int[] Drain(BinaryHeap<int> heap)
    {
        List<int> values = new();

        while (heap.TryPop(out int value))
        {
            values.Add(value);
        }

        return values.ToArray();
    }
}
