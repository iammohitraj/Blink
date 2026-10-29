// Blink · mohit

using System.Text;

namespace Blink.Core.Collections;

/// <summary>
/// Prefix tree. Insert, lookup and prefix checks are O(length of the word), independent
/// of how many words are stored.
/// </summary>
public sealed class Trie
{
    private sealed class Node
    {
        public Dictionary<char, Node>? Children;
        public bool IsWord;
    }

    private readonly Node root = new();

    public int Count { get; private set; }

    public void Insert(ReadOnlySpan<char> word)
    {
        Node node = root;

        foreach (char character in word)
        {
            node.Children ??= new Dictionary<char, Node>();

            if (!node.Children.TryGetValue(character, out Node? child))
            {
                child = new Node();
                node.Children[character] = child;
            }

            node = child;
        }

        if (!node.IsWord)
        {
            node.IsWord = true;
            Count++;
        }
    }

    public bool Contains(ReadOnlySpan<char> word)
    {
        Node? node = Find(word);
        return node is not null && node.IsWord;
    }

    public bool StartsWith(ReadOnlySpan<char> prefix) => Find(prefix) is not null;

    public bool Remove(ReadOnlySpan<char> word)
    {
        List<(Node Parent, char Character, Node Child)> path = new(word.Length);
        Node node = root;

        foreach (char character in word)
        {
            if (node.Children is null || !node.Children.TryGetValue(character, out Node? child))
            {
                return false;
            }

            path.Add((node, character, child));
            node = child;
        }

        if (!node.IsWord)
        {
            return false;
        }

        node.IsWord = false;
        Count--;

        for (int i = path.Count - 1; i >= 0; i--)
        {
            (Node parent, char character, Node child) = path[i];

            if (child.IsWord || child.Children is { Count: > 0 })
            {
                break;
            }

            parent.Children!.Remove(character);

            if (parent.Children.Count == 0)
            {
                parent.Children = null;
            }
        }

        return true;
    }

    public IEnumerable<string> WordsWithPrefix(ReadOnlySpan<char> prefix) =>
        WordsWithPrefix(prefix.ToString());

    public IEnumerable<string> WordsWithPrefix(string prefix)
    {
        Node? node = Find(prefix);
        if (node is null)
        {
            yield break;
        }

        StringBuilder builder = new(prefix.Length + 8);
        builder.Append(prefix);

        foreach (string word in Walk(node, builder))
        {
            yield return word;
        }
    }

    public IEnumerable<string> Words() => WordsWithPrefix(string.Empty);

    private Node? Find(ReadOnlySpan<char> text)
    {
        Node node = root;

        foreach (char character in text)
        {
            if (node.Children is null || !node.Children.TryGetValue(character, out Node? child))
            {
                return null;
            }

            node = child;
        }

        return node;
    }

    private static IEnumerable<string> Walk(Node node, StringBuilder prefix)
    {
        if (node.IsWord)
        {
            yield return prefix.ToString();
        }

        if (node.Children is null)
        {
            yield break;
        }

        foreach ((char character, Node child) in node.Children)
        {
            prefix.Append(character);

            foreach (string word in Walk(child, prefix))
            {
                yield return word;
            }

            prefix.Length--;
        }
    }
}
