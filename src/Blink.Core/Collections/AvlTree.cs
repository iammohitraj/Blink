// Blink · mohit

using System.Collections;

namespace Blink.Core.Collections;

/// <summary>
/// Self-balancing AVL tree. Rotations keep the height at O(log n) even for sorted input,
/// so search, insert and delete all stay O(log n).
/// </summary>
public sealed class AvlTree<T> : IEnumerable<T>
{
    private sealed class Node
    {
        public Node(T value) => Value = value;

        public T Value;
        public Node? Left;
        public Node? Right;
        public int Height = 1;
    }

    private readonly IComparer<T> comparer;
    private Node? root;

    public AvlTree(IComparer<T>? comparer = null) =>
        this.comparer = comparer ?? Comparer<T>.Default;

    public int Count { get; private set; }

    public int Height => HeightOf(root);

    public void Insert(T value)
    {
        root = Insert(root, value);
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
        int before = Count;
        root = Remove(root, value);
        return Count != before;
    }

    public IEnumerable<T> InOrder()
    {
        Stack<Node> stack = new();
        Node? current = root;

        while (current is not null || stack.Count > 0)
        {
            while (current is not null)
            {
                stack.Push(current);
                current = current.Left;
            }

            current = stack.Pop();
            yield return current.Value;
            current = current.Right;
        }
    }

    public IEnumerator<T> GetEnumerator() => InOrder().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private Node Insert(Node? node, T value)
    {
        if (node is null)
        {
            Count++;
            return new Node(value);
        }

        int comparison = comparer.Compare(value, node.Value);
        if (comparison < 0)
        {
            node.Left = Insert(node.Left, value);
        }
        else if (comparison > 0)
        {
            node.Right = Insert(node.Right, value);
        }
        else
        {
            return node;
        }

        return Balance(node);
    }

    private Node? Remove(Node? node, T value)
    {
        if (node is null)
        {
            return null;
        }

        int comparison = comparer.Compare(value, node.Value);
        if (comparison < 0)
        {
            node.Left = Remove(node.Left, value);
        }
        else if (comparison > 0)
        {
            node.Right = Remove(node.Right, value);
        }
        else
        {
            if (node.Left is null)
            {
                Count--;
                return node.Right;
            }

            if (node.Right is null)
            {
                Count--;
                return node.Left;
            }

            Node successor = node.Right;
            while (successor.Left is not null)
            {
                successor = successor.Left;
            }

            node.Value = successor.Value;
            node.Right = RemoveMin(node.Right);
        }

        return Balance(node);
    }

    private Node? RemoveMin(Node? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node.Left is null)
        {
            Count--;
            return node.Right;
        }

        node.Left = RemoveMin(node.Left);
        return Balance(node);
    }

    private Node Balance(Node node)
    {
        UpdateHeight(node);
        int balance = HeightOf(node.Left) - HeightOf(node.Right);

        if (balance > 1)
        {
            if (HeightOf(node.Left!.Left) < HeightOf(node.Left.Right))
            {
                node.Left = RotateLeft(node.Left);
            }

            return RotateRight(node);
        }

        if (balance < -1)
        {
            if (HeightOf(node.Right!.Right) < HeightOf(node.Right.Left))
            {
                node.Right = RotateRight(node.Right);
            }

            return RotateLeft(node);
        }

        return node;
    }

    private static Node RotateLeft(Node node)
    {
        Node pivot = node.Right!;
        node.Right = pivot.Left;
        pivot.Left = node;

        UpdateHeight(node);
        UpdateHeight(pivot);
        return pivot;
    }

    private static Node RotateRight(Node node)
    {
        Node pivot = node.Left!;
        node.Left = pivot.Right;
        pivot.Right = node;

        UpdateHeight(node);
        UpdateHeight(pivot);
        return pivot;
    }

    private static void UpdateHeight(Node node) =>
        node.Height = 1 + Math.Max(HeightOf(node.Left), HeightOf(node.Right));

    private static int HeightOf(Node? node) => node?.Height ?? 0;
}
