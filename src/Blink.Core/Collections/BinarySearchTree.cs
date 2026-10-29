// Blink · mohit

using System.Collections;

namespace Blink.Core.Collections;

/// <summary>
/// Classic unbalanced binary search tree. Search, insert and delete are O(h), where h can
/// degrade to n on sorted input; use <see cref="AvlTree{T}"/> when that matters.
/// </summary>
public sealed class BinarySearchTree<T> : IEnumerable<T>
{
    private sealed class Node
    {
        public Node(T value) => Value = value;

        public T Value;
        public Node? Left;
        public Node? Right;
    }

    private readonly IComparer<T> comparer;
    private Node? root;

    public BinarySearchTree(IComparer<T>? comparer = null) =>
        this.comparer = comparer ?? Comparer<T>.Default;

    public int Count { get; private set; }

    public bool IsEmpty => root is null;

    public T Min()
    {
        if (root is null)
        {
            throw new InvalidOperationException("The tree is empty.");
        }

        Node node = root;
        while (node.Left is not null)
        {
            node = node.Left;
        }

        return node.Value;
    }

    public T Max()
    {
        if (root is null)
        {
            throw new InvalidOperationException("The tree is empty.");
        }

        Node node = root;
        while (node.Right is not null)
        {
            node = node.Right;
        }

        return node.Value;
    }

    public void Insert(T value)
    {
        if (root is null)
        {
            root = new Node(value);
            Count++;
            return;
        }

        Node current = root;
        while (true)
        {
            int comparison = comparer.Compare(value, current.Value);
            if (comparison == 0)
            {
                return;
            }

            if (comparison < 0)
            {
                if (current.Left is null)
                {
                    current.Left = new Node(value);
                    Count++;
                    return;
                }

                current = current.Left;
            }
            else
            {
                if (current.Right is null)
                {
                    current.Right = new Node(value);
                    Count++;
                    return;
                }

                current = current.Right;
            }
        }
    }

    public bool Contains(T value)
    {
        Node? current = root;
        while (current is not null)
        {
            int comparison = comparer.Compare(value, current.Value);
            if (comparison == 0)
            {
                return true;
            }

            current = comparison < 0 ? current.Left : current.Right;
        }

        return false;
    }

    public bool Remove(T value)
    {
        bool removed = false;
        root = Remove(root, value, ref removed);

        if (removed)
        {
            Count--;
        }

        return removed;
    }

    public int Height() => Height(root);

    public IEnumerable<T> InOrder() => WalkInOrder(root);

    public IEnumerable<T> PreOrder() => WalkPreOrder(root);

    public IEnumerable<T> PostOrder() => WalkPostOrder(root);

    public IEnumerator<T> GetEnumerator() => InOrder().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private Node? Remove(Node? node, T value, ref bool removed)
    {
        if (node is null)
        {
            return null;
        }

        int comparison = comparer.Compare(value, node.Value);
        if (comparison < 0)
        {
            node.Left = Remove(node.Left, value, ref removed);
        }
        else if (comparison > 0)
        {
            node.Right = Remove(node.Right, value, ref removed);
        }
        else
        {
            removed = true;

            if (node.Left is null)
            {
                return node.Right;
            }

            if (node.Right is null)
            {
                return node.Left;
            }

            Node successor = MinNode(node.Right);
            node.Value = successor.Value;
            node.Right = RemoveMin(node.Right);
        }

        return node;
    }

    private static Node MinNode(Node node)
    {
        while (node.Left is not null)
        {
            node = node.Left;
        }

        return node;
    }

    private static Node? RemoveMin(Node? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node.Left is null)
        {
            return node.Right;
        }

        node.Left = RemoveMin(node.Left);
        return node;
    }

    private static int Height(Node? node) =>
        node is null ? 0 : 1 + Math.Max(Height(node.Left), Height(node.Right));

    private static IEnumerable<T> WalkInOrder(Node? node)
    {
        if (node is null)
        {
            yield break;
        }

        foreach (T value in WalkInOrder(node.Left))
        {
            yield return value;
        }

        yield return node.Value;

        foreach (T value in WalkInOrder(node.Right))
        {
            yield return value;
        }
    }

    private static IEnumerable<T> WalkPreOrder(Node? node)
    {
        if (node is null)
        {
            yield break;
        }

        yield return node.Value;

        foreach (T value in WalkPreOrder(node.Left))
        {
            yield return value;
        }

        foreach (T value in WalkPreOrder(node.Right))
        {
            yield return value;
        }
    }

    private static IEnumerable<T> WalkPostOrder(Node? node)
    {
        if (node is null)
        {
            yield break;
        }

        foreach (T value in WalkPostOrder(node.Left))
        {
            yield return value;
        }

        foreach (T value in WalkPostOrder(node.Right))
        {
            yield return value;
        }

        yield return node.Value;
    }
}
