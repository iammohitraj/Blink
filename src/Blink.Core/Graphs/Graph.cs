// Blink · mohit

using Blink.Core.Collections;

namespace Blink.Core.Graphs;

public readonly record struct Edge(int To, int Weight);

/// <summary>
/// Adjacency-list graph over vertices 0..n-1. Supports directed or undirected edges and
/// provides BFS, DFS, Dijkstra and Kahn's topological sort.
/// </summary>
public sealed class Graph
{
    private readonly List<Edge>[] adjacency;

    public Graph(int vertexCount, bool directed = true)
    {
        if (vertexCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vertexCount));
        }

        adjacency = new List<Edge>[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            adjacency[i] = new List<Edge>();
        }

        Directed = directed;
    }

    public int VertexCount => adjacency.Length;

    public bool Directed { get; }

    public void AddEdge(int from, int to, int weight = 1)
    {
        Validate(from);
        Validate(to);

        if (weight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), "Edge weights must be non-negative.");
        }

        adjacency[from].Add(new Edge(to, weight));

        if (!Directed)
        {
            adjacency[to].Add(new Edge(from, weight));
        }
    }

    public IReadOnlyList<Edge> Neighbours(int vertex)
    {
        Validate(vertex);
        return adjacency[vertex];
    }

    public int[] Bfs(int source)
    {
        Validate(source);

        int[] order = new int[VertexCount];
        bool[] visited = new bool[VertexCount];
        CircularQueue<int> queue = new();

        visited[source] = true;
        queue.Enqueue(source);
        int index = 0;

        while (queue.TryDequeue(out int vertex))
        {
            order[index++] = vertex;

            foreach (Edge edge in adjacency[vertex])
            {
                if (visited[edge.To])
                {
                    continue;
                }

                visited[edge.To] = true;
                queue.Enqueue(edge.To);
            }
        }

        Array.Resize(ref order, index);
        return order;
    }

    public int[] Dfs(int source)
    {
        Validate(source);

        int[] order = new int[VertexCount];
        bool[] visited = new bool[VertexCount];
        ArrayStack<int> stack = new();

        stack.Push(source);
        visited[source] = true;
        int index = 0;

        while (stack.TryPop(out int vertex))
        {
            order[index++] = vertex;

            for (int i = adjacency[vertex].Count - 1; i >= 0; i--)
            {
                int next = adjacency[vertex][i].To;
                if (visited[next])
                {
                    continue;
                }

                visited[next] = true;
                stack.Push(next);
            }
        }

        Array.Resize(ref order, index);
        return order;
    }

    public long[] Dijkstra(int source)
    {
        Validate(source);

        long[] distances = new long[VertexCount];
        Array.Fill(distances, long.MaxValue);
        distances[source] = 0;

        bool[] settled = new bool[VertexCount];
        BinaryHeap<(long Distance, int Vertex)> queue = new();
        queue.Push((0, source));

        while (queue.TryPop(out (long Distance, int Vertex) current))
        {
            if (settled[current.Vertex])
            {
                continue;
            }

            settled[current.Vertex] = true;

            foreach (Edge edge in adjacency[current.Vertex])
            {
                long candidate = current.Distance + edge.Weight;

                if (candidate >= distances[edge.To])
                {
                    continue;
                }

                distances[edge.To] = candidate;
                queue.Push((candidate, edge.To));
            }
        }

        return distances;
    }

    public bool TryTopologicalSort(out int[] order)
    {
        int[] incoming = new int[VertexCount];

        foreach (List<Edge> edges in adjacency)
        {
            foreach (Edge edge in edges)
            {
                incoming[edge.To]++;
            }
        }

        CircularQueue<int> ready = new();
        for (int vertex = 0; vertex < VertexCount; vertex++)
        {
            if (incoming[vertex] == 0)
            {
                ready.Enqueue(vertex);
            }
        }

        order = new int[VertexCount];
        int index = 0;

        while (ready.TryDequeue(out int vertex))
        {
            order[index++] = vertex;

            foreach (Edge edge in adjacency[vertex])
            {
                if (--incoming[edge.To] == 0)
                {
                    ready.Enqueue(edge.To);
                }
            }
        }

        if (index == VertexCount)
        {
            return true;
        }

        order = Array.Empty<int>();
        return false;
    }

    private void Validate(int vertex)
    {
        if ((uint)vertex >= (uint)adjacency.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(vertex));
        }
    }
}
