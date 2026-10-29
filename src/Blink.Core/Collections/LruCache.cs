// Blink · mohit

namespace Blink.Core.Collections;

/// <summary>
/// Bounded least-recently-used cache. Lookups, inserts and evictions are O(1) thanks to a
/// dictionary pointing straight at an intrusive doubly linked list.
/// </summary>
public sealed class LruCache<TKey, TValue>
    where TKey : notnull
{
    private sealed class Node
    {
        public TKey Key = default!;
        public TValue Value = default!;
        public Node? Previous;
        public Node? Next;
    }

    private readonly Dictionary<TKey, Node> lookup;
    private readonly int capacity;
    private Node? newest;
    private Node? oldest;

    public LruCache(int capacity, IEqualityComparer<TKey>? comparer = null)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        this.capacity = capacity;
        lookup = new Dictionary<TKey, Node>(capacity, comparer);
    }

    public int Capacity => capacity;

    public int Count => lookup.Count;

    public bool TryGet(TKey key, out TValue value)
    {
        if (!lookup.TryGetValue(key, out Node? node))
        {
            value = default!;
            return false;
        }

        Touch(node);
        value = node.Value;
        return true;
    }

    public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
    {
        if (TryGet(key, out TValue value))
        {
            return value;
        }

        value = factory(key);
        Set(key, value);
        return value;
    }

    public void Set(TKey key, TValue value)
    {
        if (lookup.TryGetValue(key, out Node? node))
        {
            node.Value = value;
            Touch(node);
            return;
        }

        Node created = new() { Key = key, Value = value };
        lookup.Add(key, created);
        AddFirst(created);

        if (lookup.Count > capacity)
        {
            Node evicted = oldest!;
            RemoveNode(evicted);
            lookup.Remove(evicted.Key);
        }
    }

    public bool Remove(TKey key)
    {
        if (!lookup.Remove(key, out Node? node))
        {
            return false;
        }

        Unlink(node);
        return true;
    }

    public void Clear()
    {
        lookup.Clear();
        newest = null;
        oldest = null;
    }

    private void Touch(Node node)
    {
        if (ReferenceEquals(node, newest))
        {
            return;
        }

        Unlink(node);
        AddFirst(node);
    }

    private void AddFirst(Node node)
    {
        node.Previous = null;
        node.Next = newest;

        if (newest is not null)
        {
            newest.Previous = node;
        }

        newest = node;
        oldest ??= node;
    }

    private void Unlink(Node node)
    {
        if (node.Previous is not null)
        {
            node.Previous.Next = node.Next;
        }
        else
        {
            newest = node.Next;
        }

        if (node.Next is not null)
        {
            node.Next.Previous = node.Previous;
        }
        else
        {
            oldest = node.Previous;
        }

        node.Previous = null;
        node.Next = null;
    }

    private void RemoveNode(Node node) => Unlink(node);
}

