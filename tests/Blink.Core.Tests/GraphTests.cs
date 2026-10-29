// Blink · mohit

using Blink.Core.Graphs;

namespace Blink.Core.Tests;

public sealed class GraphTests
{
    [Fact]
    public void Bfs_VisitsReachableVerticesInLayers()
    {
        Graph graph = BuildSample();

        int[] order = graph.Bfs(0);

        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, order);
    }

    [Fact]
    public void Dfs_VisitsEveryReachableVertexOnce()
    {
        Graph graph = BuildSample();

        int[] order = graph.Dfs(0);

        Assert.Equal(6, order.Length);
        Assert.Equal(0, order[0]);
        Assert.Equal(6, order.Distinct().Count());
        Assert.Contains(5, order);
    }

    [Fact]
    public void Dijkstra_FindsTheCheapestPath()
    {
        Graph graph = new(4);
        graph.AddEdge(0, 1, 4);
        graph.AddEdge(0, 2, 1);
        graph.AddEdge(2, 1, 2);
        graph.AddEdge(1, 3, 1);
        graph.AddEdge(2, 3, 5);

        long[] distances = graph.Dijkstra(0);

        Assert.Equal(0L, distances[0]);
        Assert.Equal(3L, distances[1]);
        Assert.Equal(1L, distances[2]);
        Assert.Equal(4L, distances[3]);
    }

    [Fact]
    public void UndirectedGraph_AddsBothDirections()
    {
        Graph graph = new(2, directed: false);
        graph.AddEdge(0, 1, 7);

        Assert.Single(graph.Neighbours(0));
        Assert.Single(graph.Neighbours(1));
        Assert.Equal(7L, graph.Dijkstra(1)[0]);
    }

    [Fact]
    public void TopologicalSort_WorksForDagsAndRejectsCycles()
    {
        Graph dag = new(4);
        dag.AddEdge(0, 1);
        dag.AddEdge(0, 2);
        dag.AddEdge(1, 3);
        dag.AddEdge(2, 3);

        Assert.True(dag.TryTopologicalSort(out int[] order));
        Assert.Equal(4, order.Length);
        Assert.True(Array.IndexOf(order, 0) < Array.IndexOf(order, 1));
        Assert.True(Array.IndexOf(order, 0) < Array.IndexOf(order, 2));
        Assert.True(Array.IndexOf(order, 1) < Array.IndexOf(order, 3));
        Assert.True(Array.IndexOf(order, 2) < Array.IndexOf(order, 3));

        Graph cycle = new(2);
        cycle.AddEdge(0, 1);
        cycle.AddEdge(1, 0);

        Assert.False(cycle.TryTopologicalSort(out _));
    }

    private static Graph BuildSample()
    {
        Graph graph = new(6);
        graph.AddEdge(0, 1);
        graph.AddEdge(0, 2);
        graph.AddEdge(1, 3);
        graph.AddEdge(2, 3);
        graph.AddEdge(3, 4);
        graph.AddEdge(4, 5);
        return graph;
    }
}
