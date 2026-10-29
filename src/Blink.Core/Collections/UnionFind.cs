// Blink · mohit

namespace Blink.Core.Collections;

/// <summary>
/// Disjoint-set forest with path compression and union by rank.
/// Both operations run in O(alpha(n)), effectively constant.
/// </summary>
public sealed class UnionFind
{
    private readonly int[] parent;
    private readonly int[] rank;
    private readonly int[] size;

    public UnionFind(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        parent = new int[count];
        rank = new int[count];
        size = new int[count];

        for (int i = 0; i < count; i++)
        {
            parent[i] = i;
            size[i] = 1;
        }

        Count = count;
    }

    public int Count { get; private set; }

    public int Find(int value)
    {
        Validate(value);

        int root = value;
        while (parent[root] != root)
        {
            root = parent[root];
        }

        while (parent[value] != value)
        {
            int next = parent[value];
            parent[value] = root;
            value = next;
        }

        return root;
    }

    public bool Union(int first, int second)
    {
        int firstRoot = Find(first);
        int secondRoot = Find(second);

        if (firstRoot == secondRoot)
        {
            return false;
        }

        if (rank[firstRoot] < rank[secondRoot])
        {
            (firstRoot, secondRoot) = (secondRoot, firstRoot);
        }

        parent[secondRoot] = firstRoot;
        size[firstRoot] += size[secondRoot];

        if (rank[firstRoot] == rank[secondRoot])
        {
            rank[firstRoot]++;
        }

        Count--;
        return true;
    }

    public bool Connected(int first, int second) => Find(first) == Find(second);

    public int SetSize(int value) => size[Find(value)];

    private void Validate(int value)
    {
        if ((uint)value >= (uint)parent.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
