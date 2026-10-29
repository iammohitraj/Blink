// Blink · mohit

using Blink.Core.Collections;

namespace Blink.Core.Tests;

public sealed class TreeTests
{
    [Fact]
    public void BinarySearchTree_SupportsInsertSearchAndDelete()
    {
        BinarySearchTree<int> tree = new();

        foreach (int value in new[] { 50, 30, 70, 20, 40, 60, 80 })
        {
            tree.Insert(value);
        }

        Assert.Equal(7, tree.Count);
        Assert.True(tree.Contains(40));
        Assert.False(tree.Contains(41));
        Assert.Equal(new[] { 20, 30, 40, 50, 60, 70, 80 }, tree.InOrder());
        Assert.Equal(20, tree.Min());
        Assert.Equal(80, tree.Max());

        Assert.True(tree.Remove(20));
        Assert.False(tree.Contains(20));
        Assert.True(tree.Remove(30));
        Assert.Equal(new[] { 40, 50, 60, 70, 80 }, tree.InOrder());
        Assert.True(tree.Remove(50));
        Assert.Equal(new[] { 40, 60, 70, 80 }, tree.InOrder());
        Assert.Equal(4, tree.Count);
    }

    [Fact]
    public void AvlTree_StaysBalancedOnSortedInput()
    {
        AvlTree<int> tree = new();

        for (int value = 1; value <= 1000; value++)
        {
            tree.Insert(value);
        }

        Assert.Equal(1000, tree.Count);
        Assert.InRange(tree.Height, 1, 15);
        Assert.Equal(Enumerable.Range(1, 1000), tree.InOrder());

        for (int value = 1; value <= 500; value++)
        {
            Assert.True(tree.Remove(value));
        }

        Assert.Equal(500, tree.Count);
        Assert.Equal(Enumerable.Range(501, 500), tree.InOrder());
        Assert.True(tree.Contains(750));
        Assert.False(tree.Contains(250));
    }

    [Fact]
    public void Trie_FindsPrefixesAndPrunesRemovedBranches()
    {
        Trie trie = new();

        foreach (string word in new[] { "cat", "car", "cart", "dog" })
        {
            trie.Insert(word);
        }

        Assert.Equal(4, trie.Count);
        Assert.True(trie.Contains("cat"));
        Assert.False(trie.Contains("ca"));
        Assert.True(trie.StartsWith("ca"));
        Assert.Equal(new[] { "car", "cart", "cat" }, trie.WordsWithPrefix("ca").OrderBy(word => word));

        Assert.True(trie.Remove("car"));
        Assert.False(trie.Contains("car"));
        Assert.True(trie.Contains("cart"));
        Assert.Equal(3, trie.Count);

        Assert.True(trie.Remove("cat"));
        Assert.True(trie.Remove("cart"));
        Assert.True(trie.Remove("dog"));
        Assert.Equal(0, trie.Count);
        Assert.Empty(trie.Words());
    }
}
