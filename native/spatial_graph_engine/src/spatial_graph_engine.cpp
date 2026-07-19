#include "spatial_graph_engine.h"

#include <algorithm>
#include <cmath>
#include <limits>
#include <queue>
#include <utility>
#include <vector>

namespace
{
	constexpr double kInfinity = std::numeric_limits<double>::infinity();

	// Compressed-sparse-row adjacency.
	//
	// The first version of this kernel used std::map<int, std::vector<Edge>> and
	// std::map<int, double> for distances, which meant an O(log N) tree descent for every
	// edge relaxation plus a heap allocation per node's edge vector. Two sibling kernels in
	// this repo measured *slower than their managed C# fallbacks* with that exact shape
	// before being rewritten this way — the .NET nursery allocator beats naive per-node
	// allocation, and RyuJIT beats a red-black tree walk.
	//
	// Here node ids are densified once into 0..N-1, edges are packed into three flat
	// arrays, and the relaxation loop touches nothing but contiguous memory: colIndices
	// already holds dense indices, so there is no lookup of any kind in the hot path.
	struct Csr
	{
		std::vector<int> ids;          // dense index -> original node id, ascending
		std::vector<int> rowOffsets;   // size N + 1
		std::vector<int> colIndices;   // dense target index per directed edge
		std::vector<double> weights;   // cost per directed edge

		int size() const { return static_cast<int>(ids.size()); }

		// Binary search over the ascending id array. Used only at setup and when mapping
		// results back out — never inside the search loop.
		int indexOf(int id) const
		{
			const auto it = std::lower_bound(ids.begin(), ids.end(), id);
			if (it == ids.end() || *it != id)
				return -1;
			return static_cast<int>(it - ids.begin());
		}
	};

	Csr build_csr(const GraphNodeNative* nodes, int nodeCount, const GraphEdgeNative* edges, int edgeCount)
	{
		Csr csr;
		csr.ids.reserve(nodeCount);
		for (int i = 0; i < nodeCount; ++i)
			csr.ids.push_back(nodes[i].id);

		std::sort(csr.ids.begin(), csr.ids.end());
		csr.ids.erase(std::unique(csr.ids.begin(), csr.ids.end()), csr.ids.end());

		const int n = csr.size();
		csr.rowOffsets.assign(n + 1, 0);

		// Pass 1: out-degree per dense index. Edges referencing a node that was never
		// declared are dropped here rather than relaxed later — relaxing them is what let
		// the old std::map version insert a phantom zero-cost entry.
		for (int e = 0; e < edgeCount; ++e)
		{
			const int from = csr.indexOf(edges[e].fromNodeId);
			const int to = csr.indexOf(edges[e].toNodeId);
			if (from < 0 || to < 0)
				continue;

			++csr.rowOffsets[from + 1];
			if (edges[e].bidirectional != 0)
				++csr.rowOffsets[to + 1];
		}

		for (int i = 0; i < n; ++i)
			csr.rowOffsets[i + 1] += csr.rowOffsets[i];

		const int directed = csr.rowOffsets[n];
		csr.colIndices.assign(directed, 0);
		csr.weights.assign(directed, 0.0);

		// Pass 2: fill, using a moving cursor per row.
		std::vector<int> cursor(csr.rowOffsets.begin(), csr.rowOffsets.end() - 1);
		for (int e = 0; e < edgeCount; ++e)
		{
			const int from = csr.indexOf(edges[e].fromNodeId);
			const int to = csr.indexOf(edges[e].toNodeId);
			if (from < 0 || to < 0)
				continue;

			const double cost = edges[e].cost;
			csr.colIndices[cursor[from]] = to;
			csr.weights[cursor[from]] = cost;
			++cursor[from];

			if (edges[e].bidirectional != 0)
			{
				csr.colIndices[cursor[to]] = from;
				csr.weights[cursor[to]] = cost;
				++cursor[to];
			}
		}

		return csr;
	}

	// Lazy-deletion Dijkstra over CSR. `distances` is indexed by dense node index and must
	// be sized to csr.size(). `previous`, when non-null, records the predecessor tree in
	// dense indices. `endIndex`, when >= 0, stops the search once that node is settled.
	void dijkstra(const Csr& csr, std::vector<double>& distances, int originIndex, std::vector<int>* previous, int endIndex)
	{
		using QueueItem = std::pair<double, int>;
		std::priority_queue<QueueItem, std::vector<QueueItem>, std::greater<QueueItem>> queue;

		distances[originIndex] = 0.0;
		queue.push({ 0.0, originIndex });

		while (!queue.empty())
		{
			const auto [distance, current] = queue.top();
			queue.pop();

			if (distance > distances[current])
				continue;
			if (endIndex >= 0 && current == endIndex)
				break;

			const int begin = csr.rowOffsets[current];
			const int end = csr.rowOffsets[current + 1];
			for (int k = begin; k < end; ++k)
			{
				const int target = csr.colIndices[k];
				const double candidate = distance + csr.weights[k];
				if (candidate < distances[target])
				{
					distances[target] = candidate;
					if (previous)
						(*previous)[target] = current;
					queue.push({ candidate, target });
				}
			}
		}
	}
}

extern "C" GRAPH_API int Graph_FindShortestPath(
	const GraphNodeNative* nodes,
	int nodeCount,
	const GraphEdgeNative* edges,
	int edgeCount,
	int startNodeId,
	int endNodeId,
	int* outputNodeIds,
	int outputCapacity,
	GraphPathResultNative* result)
{
	try
	{
		if (!nodes || !edges || !outputNodeIds || !result || nodeCount <= 0 || edgeCount <= 0 || outputCapacity <= 0)
			return -1;

		const Csr csr = build_csr(nodes, nodeCount, edges, edgeCount);
		const int start = csr.indexOf(startNodeId);
		const int end = csr.indexOf(endNodeId);
		if (start < 0 || end < 0)
			return -2;

		std::vector<double> distances(csr.size(), kInfinity);
		std::vector<int> previous(csr.size(), -1);
		dijkstra(csr, distances, start, &previous, end);

		*result = {};
		if (!std::isfinite(distances[end]))
			return 0;

		// Walk the predecessor tree back to the origin. A -1 predecessor means the tree is
		// malformed; the step count is bounded by the node count so this cannot spin.
		std::vector<int> path;
		int current = end;
		int guard = 0;
		while (current != start)
		{
			if (++guard > csr.size() || current < 0)
				return -99;
			path.push_back(csr.ids[current]);
			current = previous[current];
		}
		path.push_back(startNodeId);
		std::reverse(path.begin(), path.end());

		if (static_cast<int>(path.size()) > outputCapacity)
			return -3;

		for (int i = 0; i < static_cast<int>(path.size()); ++i)
			outputNodeIds[i] = path[i];

		result->found = 1;
		result->totalCost = distances[end];
		result->pathCount = static_cast<int>(path.size());
		return 0;
	}
	catch (...)
	{
		return -99;
	}
}

extern "C" GRAPH_API int Graph_ComputeServiceArea(
	const GraphNodeNative* nodes,
	int nodeCount,
	const GraphEdgeNative* edges,
	int edgeCount,
	int originNodeId,
	double maxCost,
	int* reachableNodeIds,
	int outputCapacity,
	int* reachableCount)
{
	try
	{
		if (!nodes || !edges || !reachableNodeIds || !reachableCount || nodeCount <= 0 || edgeCount <= 0 || outputCapacity <= 0 || maxCost < 0)
			return -1;

		const Csr csr = build_csr(nodes, nodeCount, edges, edgeCount);
		const int origin = csr.indexOf(originNodeId);
		if (origin < 0)
			return -2;

		std::vector<double> distances(csr.size(), kInfinity);
		dijkstra(csr, distances, origin, nullptr, -1);

		// Dense indices ascend with node id, so walking them yields ids in ascending order,
		// matching the ordering callers relied on from the previous std::map implementation.
		int count = 0;
		for (int i = 0; i < csr.size(); ++i)
		{
			if (distances[i] <= maxCost)
			{
				if (count >= outputCapacity)
					return -3;
				reachableNodeIds[count++] = csr.ids[i];
			}
		}

		*reachableCount = count;
		return 0;
	}
	catch (...)
	{
		return -99;
	}
}

extern "C" GRAPH_API int Graph_ComputeDistances(
	const GraphNodeNative* nodes,
	int nodeCount,
	const GraphEdgeNative* edges,
	int edgeCount,
	int originNodeId,
	double* outDistances,
	int outputLength)
{
	try
	{
		if (!nodes || !edges || !outDistances || nodeCount <= 0 || edgeCount <= 0)
			return -1;
		if (outputLength < nodeCount)
			return -3;

		const Csr csr = build_csr(nodes, nodeCount, edges, edgeCount);
		const int origin = csr.indexOf(originNodeId);
		if (origin < 0)
			return -2;

		std::vector<double> distances(csr.size(), kInfinity);
		dijkstra(csr, distances, origin, nullptr, -1);

		int reachable = 0;
		for (int i = 0; i < nodeCount; ++i)
		{
			const int index = csr.indexOf(nodes[i].id);
			const double d = index < 0 ? kInfinity : distances[index];
			outDistances[i] = d;
			if (std::isfinite(d))
				++reachable;
		}

		return reachable;
	}
	catch (...)
	{
		return -99;
	}
}

extern "C" GRAPH_API int Graph_ComputeDistanceMatrix(
	const GraphNodeNative* nodes,
	int nodeCount,
	const GraphEdgeNative* edges,
	int edgeCount,
	const int* sourceIds,
	int sourceCount,
	const int* targetIds,
	int targetCount,
	double* outMatrix,
	int outputLength)
{
	try
	{
		if (!nodes || !edges || !sourceIds || !targetIds || !outMatrix)
			return -1;
		if (nodeCount <= 0 || edgeCount <= 0 || sourceCount <= 0 || targetCount <= 0)
			return -1;

		const long long required = static_cast<long long>(sourceCount) * static_cast<long long>(targetCount);
		if (required > static_cast<long long>(outputLength))
			return -3;

		// Built once for the whole matrix — the entire reason this entry point exists
		// rather than looping Graph_FindShortestPath.
		const Csr csr = build_csr(nodes, nodeCount, edges, edgeCount);

		std::vector<int> sources(sourceCount), targets(targetCount);
		for (int s = 0; s < sourceCount; ++s)
		{
			sources[s] = csr.indexOf(sourceIds[s]);
			if (sources[s] < 0)
				return -2;
		}
		for (int t = 0; t < targetCount; ++t)
		{
			targets[t] = csr.indexOf(targetIds[t]);
			if (targets[t] < 0)
				return -2;
		}

		std::vector<double> distances(csr.size());
		for (int s = 0; s < sourceCount; ++s)
		{
			std::fill(distances.begin(), distances.end(), kInfinity);
			dijkstra(csr, distances, sources[s], nullptr, -1);

			double* row = outMatrix + static_cast<long long>(s) * targetCount;
			for (int t = 0; t < targetCount; ++t)
				row[t] = distances[targets[t]];
		}

		return 0;
	}
	catch (...)
	{
		return -99;
	}
}
