# Plan 0 — Spatial Graph Engine Extensions

**Prerequisite for Plan 2 (Fleet VRP) and Plan 4 (Response Coverage). Not a user-facing project.**

Extends the existing `spatial_graph_engine` kernel with the two capabilities the current ABI
cannot express, and fixes one latent correctness bug. Small, self-contained, no UI.

---

## Why

`Graph_ComputeServiceArea` runs Dijkstra to exhaustion and builds the complete single-source
distance vector — then discards it, returning only the IDs of nodes under `maxCost`. So:

- **Isochrones are impossible.** Graded time bands (0–4 min, 4–8 min, 8–12 min) need the
  per-node cost, which never crosses the boundary.
- **Vehicle routing is impossible.** CVRPTW needs an N×N cost matrix between stops. Today
  that would mean N separate `Graph_FindShortestPath` calls, each of which rebuilds the
  entire `std::map` adjacency structure from scratch — 6,374 red-black-tree insertions per
  call, before any search work happens.

Both are fixed by exposing what the kernel already computes.

---

## Changes to `native/spatial_graph_engine/include/spatial_graph_engine.h`

Add two exports. Do not modify the existing two — they have callers.

```c
// One-to-all shortest-path costs from a single origin.
//
// outDistances is PARALLEL TO THE INPUT `nodes` ARRAY: outDistances[i] is the cost to
// nodes[i], NOT to node id i. This deliberately avoids the one-based/zero-based node-id
// trap. Unreachable nodes receive INFINITY.
//
// Returns the number of reachable nodes (>= 0), negative on error.
GRAPH_API int Graph_ComputeDistances(
	const GraphNodeNative* nodes,
	int nodeCount,
	const GraphEdgeNative* edges,
	int edgeCount,
	int originNodeId,
	double* outDistances,
	int outputLength);

// Many-to-many shortest-path cost matrix.
//
// Row-major: outMatrix[s * targetCount + t] is the cost from sourceIds[s] to targetIds[t].
// Unreachable pairs receive INFINITY. Builds the adjacency structure ONCE and runs
// sourceCount Dijkstras over it.
//
// Returns 0 on success, negative on error.
GRAPH_API int Graph_ComputeDistanceMatrix(
	const GraphNodeNative* nodes,
	int nodeCount,
	const GraphEdgeNative* edges,
	int edgeCount,
	const int* sourceIds,
	int sourceCount,
	const int* targetIds,
	int targetCount,
	double* outMatrix,
	int outputLength);
```

Status codes are unchanged: `-1` null/invalid, `-2` id not found, `-3` output buffer too
small, `-99` unhandled exception.

Validation for the matrix function: `outputLength` must be `>= sourceCount * targetCount`,
and every id in `sourceIds`/`targetIds` must exist in `nodes` (`-2` otherwise).

---

## Changes to `native/spatial_graph_engine/src/spatial_graph_engine.cpp`

### 1. Fix the phantom-node bug (required)

`distances[edge.to]` and `previous[current]` use `std::map::operator[]`, which
**default-constructs a missing key to `0.0`**. In a shortest-path context that reads as
"reachable at zero cost". `Graph_ComputeServiceArea` then iterates the entire `distances`
map in its final pass, so a node that appears in an edge but not in the node array is
reported as reachable at cost 0.

The shipped Redlands data cannot trigger this (a test asserts every edge endpoint exists),
but Plan 2 and Plan 4 both build graphs at runtime, and Plan 3's user-editable network can
trigger it directly.

Replace every `map[key]` read with a `.find()` + explicit check. Also add an iteration cap
to the `previous[]` path-reconstruction walk — today a missing predecessor yields `0` and
the `while (current != startNodeId)` loop can spin forever.

### 2. Extract the shared Dijkstra core

Both new functions and both existing ones want the same inner loop. Factor out:

```cpp
// Runs Dijkstra from `originId` over a prebuilt adjacency map.
// Fills `distances` (node id -> cost). No early exit, no cutoff.
static void dijkstra_all(
    const std::map<int, std::vector<Edge>>& adjacency,
    const std::vector<int>& nodeIds,
    int originId,
    std::map<int, double>& distances);
```

`Graph_ComputeDistanceMatrix` calls `build_adjacency` once, then loops `dijkstra_all` per
source. That single change is the whole performance argument: the current per-call rebuild
would dominate an N×N matrix by roughly N×.

### 3. Optional but recommended — CSR conversion

The C++ uses `std::map<int, std::vector<Edge>>` because the ABI is written for arbitrary
node IDs. The actual Redlands data is contiguous `1..2530`, so every lookup pays `O(log N)`
for flexibility the data never uses.

If you convert to CSR (`rowOffsets` / `colIndices` / `weights` `std::vector`s with IDs
compacted to `0..N-1`), do it **inside** `build_adjacency` behind the same signature so the
existing functions benefit for free. **Watch the off-by-one:** `EsriHqNodeId = 2530` equals
`nodes.Count`, so array index is `id - 1`; any code assuming zero-based silently reads the
wrong node.

This is where the benchmark story lives — measure before and after.

---

## Managed side

### `Portfolio.Services/Native/SpatialGraphNativeInterop.cs`

Add two `DllImport`s following the existing declarations exactly:

```csharp
[DllImport(LibName, EntryPoint = "Graph_ComputeDistances", CallingConvention = CallingConvention.Cdecl)]
[DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
internal static extern int ComputeDistances(
    [In] GraphNodeNative[] nodes, int nodeCount,
    [In] GraphEdgeNative[] edges, int edgeCount,
    int originNodeId,
    [Out] double[] outDistances, int outputLength);

[DllImport(LibName, EntryPoint = "Graph_ComputeDistanceMatrix", CallingConvention = CallingConvention.Cdecl)]
[DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.ApplicationDirectory)]
internal static extern int ComputeDistanceMatrix(
    [In] GraphNodeNative[] nodes, int nodeCount,
    [In] GraphEdgeNative[] edges, int edgeCount,
    [In] int[] sourceIds, int sourceCount,
    [In] int[] targetIds, int targetCount,
    [Out] double[] outMatrix, int outputLength);
```

### `Portfolio.Services/Native/SpatialGraphNativeBridge.cs`

Add `TryComputeDistances` and `TryComputeDistanceMatrix` mirroring the existing
`TryFindShortestPath` shape. Reuse the existing `MapNode`/`MapEdge` projections.

### `Portfolio.Services/Services/SpatialGraphService.cs`

Add to `ISpatialGraphService`:

```csharp
/// <summary>Computes shortest-path cost from one origin to every node, parallel to the node list.</summary>
Task<double[]> ComputeDistancesAsync(RoadGraphDto graph, int originNodeId, CancellationToken cancellationToken = default);

/// <summary>Computes a row-major shortest-path cost matrix between the given source and target nodes.</summary>
Task<double[]> ComputeDistanceMatrixAsync(RoadGraphDto graph, IReadOnlyList<int> sourceIds, IReadOnlyList<int> targetIds, CancellationToken cancellationToken = default);
```

Managed fallbacks reuse the existing private `ComputeDistances(...)` helper — it already
does exactly this and currently has its result thrown away by `ComputeServiceAreaAsync`.
The matrix fallback loops it per source over one prebuilt adjacency dictionary.

**Also add a nearest-node snap helper**, which both Plan 2 and Plan 4 need:

```csharp
/// <summary>Returns the id of the graph node nearest to the given coordinate (haversine).</summary>
int SnapToNearestNode(RoadGraphDto graph, double latitude, double longitude);
```

Linear scan over 2,530 nodes is fine; do not build a spatial index for this.

Add `private const int MaxMatrixCells = 250_000;` and reject larger requests with
`"Distance matrices are limited to {MaxMatrixCells} cells."`

---

## Tests — `Portfolio.Tests/Services/SpatialComputeServiceTests.cs`

Add to the existing file (it already holds the graph tests):

| Test | Assertion |
|---|---|
| `ComputeDistances_SmallGraph_ReturnsCostsParallelToNodeList` | index alignment, not id alignment |
| `ComputeDistances_DisconnectedNode_ReturnsInfinity` | `double.IsInfinity(result[i])` |
| `ComputeDistances_OriginIsZeroCost` | `result[originIndex] == 0` |
| `ComputeDistances_UnknownOrigin_ThrowsArgumentException` | |
| `ComputeDistanceMatrix_MatchesPairwiseShortestPaths` | cross-check against `FindShortestPathAsync` on a hand-built 6-node graph |
| `ComputeDistanceMatrix_IsRowMajor` | asymmetric graph, assert `[s * targets + t]` ordering |
| `ComputeDistanceMatrix_ExceedsCellLimit_ThrowsArgumentException` | |
| `SnapToNearestNode_ReturnsClosestByHaversine` | |
| `SnapToNearestNode_ExactNodeCoordinate_ReturnsThatNode` | |

Add a regression test for the phantom-node fix:

| Test | Assertion |
|---|---|
| `ComputeDistances_EdgeReferencingMissingNode_DoesNotReportZeroCost` | build a graph whose edge list names a node id absent from the node array; assert it is not reported reachable at cost 0 |

---

## Not in scope

Deliberately excluded — note them in the Details pages of Plans 2 and 4 as known evolution paths:

- **A reusable graph handle** (`Graph_Create` / `Graph_Destroy`). The single highest-leverage
  future change, but it breaks the stateless-kernel convention every other kernel follows.
  Discuss it; don't build it yet.
- **Parallel matrix rows** (`std::execution::par`). Mention as the obvious next optimization.
- **Response compression.** `GET /network/graph` returns ~425 KB uncompressed and nothing in
  `Portfolio.Web` registers `AddResponseCompression`. One-line fix, real win — but it belongs
  in its own commit, not here.
- **Turn penalties / per-edge speeds.** Note that A\*'s haversine heuristic is admissible
  **only because edge costs are along-road kilometres.** If costs ever become minutes, A\*
  silently starts returning suboptimal routes. Plan 2 depends on this — it converts distance
  to time *outside* the graph search, never inside it.
