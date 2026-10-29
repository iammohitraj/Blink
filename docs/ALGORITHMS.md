# Algorithms and data structures

`Blink.Core` is the algorithmic backbone of the app. It is deliberately small: every
structure is contiguous where it can be, avoids per-operation allocations, and exposes
spans or pooled buffers when that matters.

Complexities use the usual notation: *n* is the number of elements, *V* and *E* are
graph vertices and edges.

## Collections

| Type | Core operations | Time | Space | Notes |
|---|---|---|---|---|
| `DynamicArray<T>` | `Add`, `Insert`, `RemoveAt`, indexer | amortised O(1) add, O(n) insert/remove | O(n) | Doubles capacity; `AsSpan()` exposes the live window |
| `ArrayStack<T>` | `Push`, `Pop`, `Peek` | O(1) amortised | O(n) | Backed by a plain array |
| `CircularQueue<T>` | `Enqueue`, `Dequeue`, `Peek` | O(1) amortised | O(n) | A thin FIFO over `Deque<T>` |
| `Deque<T>` | `PushFront/Back`, `PopFront/Back` | O(1) amortised | O(n) | Circular array, grows by doubling |
| `RingBuffer<T>` | `Add`, indexer | O(1) | O(capacity) | Fixed size; the oldest item is overwritten |
| `BinaryHeap<T>` | `Push`, `Pop`, `Peek` | O(log n) | O(n) | Min or max; O(n) heapify constructor |
| `HashMap<TKey,TValue>` | `Add`, indexer, `TryGetValue`, `Remove` | O(1) average, O(n) worst | O(n) | Open addressing, linear probing, tombstones, 0.7 load factor |
| `OpenHashSet<T>` | `Add`, `Contains`, `Remove` | O(1) average | O(n) | A `HashMap<T, byte>` with the value stripped away |
| `LruCache<TKey,TValue>` | `TryGet`, `Set`, `GetOrAdd` | O(1) | O(capacity) | Dictionary plus an intrusive doubly linked list |
| `UnionFind` | `Find`, `Union`, `Connected` | near O(1) amortised (inverse Ackermann) | O(n) | Path compression and union by rank |
| `BinarySearchTree<T>` | `Insert`, `Contains`, `Remove` | O(h), worst O(n) | O(n) | In/pre/post-order iterators, `Min`, `Max`, `Height` |
| `AvlTree<T>` | `Insert`, `Remove`, `Contains` | O(log n) | O(n) | Height-balanced with single and double rotations |
| `Trie` | `Insert`, `Contains`, `StartsWith`, `WordsWithPrefix` | O(length) | O(total characters) | Removes nodes while pruning empty branches |

### Why these choices

- **Open addressing over chaining.** One array of entries beats a node per key: fewer
  allocations, better cache locality, and no pointer chasing on lookup.
- **Tombstones and rehashing.** Removing a key marks its slot deleted, so probe chains
  stay intact. When deleted slots exceed a quarter of the table, the map rehashes and
  compacts at once.
- **Intrusive linked list for LRU.** The list nodes live inside the dictionary entries
  instead of separate objects, so an eviction touches no allocator.
- **`ArrayPool<byte>` for downloads.** A single rented buffer replaces the 80 KB array
  that would otherwise be allocated for every update.

## Graphs

`Graph` stores an adjacency list and supports directed or undirected edges with weights.

| Operation | Time | Notes |
|---|---|---|
| `Bfs(source)` | O(V + E) | Returns hop counts; unreachable vertices stay `-1` |
| `Dfs(source)` | O(V + E) | Iterative, so deep graphs cannot overflow the stack |
| `Dijkstra(source)` | O((V + E) log V) | Binary heap, `long` distances, `long.MaxValue` for unreachable |
| `TryTopologicalSort()` | O(V + E) | Kahn's algorithm; returns `false` when a cycle exists |

## Sorting

| Algorithm | Best | Average | Worst | Space | Stable |
|---|---|---|---|---|---|
| `InsertionSort` | O(n) | O(n²) | O(n²) | O(1) | yes |
| `SelectionSort` | O(n²) | O(n²) | O(n²) | O(1) | no |
| `BubbleSort` | O(n) | O(n²) | O(n²) | O(1) | yes |
| `MergeSort` | O(n log n) | O(n log n) | O(n log n) | O(n), pooled | yes |
| `QuickSort` | O(n log n) | O(n log n) | O(n²) | O(log n) | no |
| `HeapSort` | O(n log n) | O(n log n) | O(n log n) | O(1) | no |
| `CountingSort` | O(n + k) | O(n + k) | O(n + k) | O(k) | yes |
| `RadixSort` | O(4n) | O(4n) | O(4n) | O(n) | yes |

Implementation details worth knowing:

- `MergeSort` rents its scratch buffer from `ArrayPool<T>` and clears references before
  returning it, so pooled arrays never keep objects alive.
- `QuickSort` is three-way partitioned (fast on duplicate-heavy data) and falls back to
  insertion sort below 16 elements, where recursion overhead dominates.
- `CountingSort` handles negative values; it rejects ranges larger than ten million to
  keep memory predictable.
- `RadixSort` is base-256 with four passes and expects non-negative integers.
- `IsSorted` short-circuits on the first inversion.

## Searching

| Algorithm | Time | Notes |
|---|---|---|
| `BinarySearch` | O(log n) | Returns an index or `-1` |
| `LowerBound` | O(log n) | First element not less than the target |
| `UpperBound` | O(log n) | First element greater than the target |
| `ExponentialSearch` | O(log n) | Finds a bound first, useful for unbounded or skewed data |

All search methods take `ReadOnlySpan<T>` and never allocate.

## Strings and numbers

| Algorithm | Time | Notes |
|---|---|---|
| `KmpSearch` | O(n + m) | Prefix table; no backtracking in the text |
| `RabinKarpSearch` | O(n + m) average | Rolling hash with overflow-checked arithmetic |
| `IsPalindrome` | O(n) | Ignores case and non-alphanumeric characters |
| `Gcd` / `Lcm` | O(log min(a,b)) | Euclid, with overflow-safe `Lcm` |
| `ModPow` | O(log exponent) | Binary exponentiation |
| `IsPrime` | O(√n) | 6k ± 1 trial division |
| `Sieve` | O(n log log n) | All primes below a bound |
| `Fibonacci` | O(n) | Iterative, `long`, overflow-checked |

## Version comparison

`SemanticVersion` implements SemVer 2.0 precedence:

1. Compare `major`, then `minor`, then `patch`.
2. A version without a prerelease outranks one with a prerelease.
3. Prerelease identifiers are compared dot by dot; numeric identifiers rank below
   alphanumeric ones, and a shorter list ranks lower when all shared parts are equal.
4. Build metadata (`+build`) is ignored for ordering.

This is what lets Blink compare a GitHub tag such as `v1.2.0-rc.1` against its own
`1.2.0` without guessing.

## Where Blink uses the library

| Hot path | Before | After |
|---|---|---|
| Update check | `System.Version` (no prerelease support) | `SemanticVersion` |
| Icon cache | Unbounded `Dictionary<int, Icon>` | 8-entry `LruCache<int, Icon>` |
| Update download | New 80 KB buffer per download | Rented `ArrayPool<byte>` buffer |
| Window class names | `StringBuilder` per hit test | Reused thread-static `char[]` |
| Double-click watcher | `Task.Delay` loop | `PeriodicTimer` |
| Settings listener | `WaitOne(300)` poll | `WaitHandle.WaitAny` |
| Settings save | New `JsonSerializerOptions` each time | Static options, atomic temp-file swap |
