#include "spatial_graph_engine.h"

#include <algorithm>
#include <cmath>
#include <limits>
#include <map>
#include <queue>
#include <utility>
#include <vector>

namespace
{
	struct Edge
	{
		int to;
		double cost;
	};

	using Adjacency = std::map<int, std::vector<Edge>>;
	using Distances = std::map<int, double>;

	constexpr double kInfinity = std::numeric_limits<double>::infinity();

	Adjacency build_adjacency(const GraphEdgeNative* edges, int edgeCount)
	{
		Adjacency adjacency;
		for (int i = 0; i < edgeCount; ++i)
		{
			adjacency[edges[i].fromNodeId].push_back({ edges[i].toNodeId, edges[i].cost });
			if (edges[i].bidirectional != 0)
				adjacency[edges[i].toNodeId].push_back({ edges[i].fromNodeId, edges[i].cost });
		}

		return adjacency;
	}

	Distances initialize_distances(const GraphNodeNative* nodes, int nodeCount)
	{
		Distances distances;
		for (int i = 0; i < nodeCount; ++i)
			distances[nodes[i].id] = kInfinity;
		return distances;
	}

	double distance_of(const Distances& distances, int nodeId)
	{
		const auto it = distances.find(nodeId);
		return it == distances.end() ? kInfinity : it->second;
	}

	// Lazy-deletion Dijkstra over a prebuilt adjacency structure.
	//
	// `distances` must already hold an entry (infinity) for every declared node. Every
	// lookup goes through find() rather than operator[], because operator[] on a missing
	// key default-constructs to 0.0 — which in a shortest-path context reads as "reachable
	// at zero cost". Edges pointing at nodes absent from the node array are skipped for
	// the same reason: relaxing them would insert phantom entries that later surface as
	// bogus reachable nodes.
	//
	// `previous`, when non-null, records the predecessor tree.
	// `endNodeId`, when non-null, stops the search as soon as that node is settled.
	void dijkstra(
		const Adjacency& adjacency,
		Distances& distances,
		int originId,
		std::map<int, int>* previous,
		const int* endNodeId)
	{
		using QueueItem = std::pair<double, int>;
		std::priority_queue<QueueItem, std::vector<QueueItem>, std::greater<QueueItem>> queue;

		const auto originIt = distances.find(originId);
		if (originIt == distances.end())
			return;

		originIt->second = 0.0;
		queue.push({ 0.0, originId });

		while (!queue.empty())
		{
			const auto [distance, current] = queue.top();
			queue.pop();

			const auto currentIt = distances.find(current);
			if (currentIt == distances.end() || distance > currentIt->second)
				continue;
			if (endNodeId && current == *endNodeId)
				break;

			const auto adjacencyIt = adjacency.find(current);
			if (adjacencyIt == adjacency.end())
				continue;

			for (const auto& edge : adjacencyIt->second)
			{
				const auto targetIt = distances.find(edge.to);
				if (targetIt == distances.end())
					continue; // edge references a node that was never declared

				const double candidate = currentIt->second + edge.cost;
				if (candidate < targetIt->second)
				{
					targetIt->second = candidate;
					if (previous)
						(*previous)[edge.to] = current;
					queue.push({ candidate, edge.to });
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

		auto adjacency = build_adjacency(edges, edgeCount);
		auto distances = initialize_distances(nodes, nodeCount);

		if (!distances.contains(startNodeId) || !distances.contains(endNodeId))
			return -2;

		std::map<int, int> previous;
		dijkstra(adjacency, distances, startNodeId, &previous, &endNodeId);

		*result = {};
		const double total = distance_of(distances, endNodeId);
		if (!std::isfinite(total))
			return 0;

		// Walk the predecessor tree back to the origin. The step count is bounded by the
		// node count: a malformed tree would otherwise loop forever.
		std::vector<int> path;
		int current = endNodeId;
		int guard = 0;
		while (current != startNodeId)
		{
			if (++guard > nodeCount)
				return -99;

			path.push_back(current);
			const auto it = previous.find(current);
			if (it == previous.end())
				return -99;
			current = it->second;
		}
		path.push_back(startNodeId);
		std::reverse(path.begin(), path.end());

		if (static_cast<int>(path.size()) > outputCapacity)
			return -3;

		for (int i = 0; i < static_cast<int>(path.size()); ++i)
			outputNodeIds[i] = path[i];

		result->found = 1;
		result->totalCost = total;
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

		auto adjacency = build_adjacency(edges, edgeCount);
		auto distances = initialize_distances(nodes, nodeCount);

		if (!distances.contains(originNodeId))
			return -2;

		dijkstra(adjacency, distances, originNodeId, nullptr, nullptr);

		// Iterate the declared node array rather than the distance map, so only real
		// nodes can ever be reported. Output stays ordered by node id to match the
		// previous behaviour, which callers rely on.
		int count = 0;
		for (const auto& [nodeId, distance] : distances)
		{
			if (distance <= maxCost)
			{
				if (count >= outputCapacity)
					return -3;
				reachableNodeIds[count++] = nodeId;
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

		auto adjacency = build_adjacency(edges, edgeCount);
		auto distances = initialize_distances(nodes, nodeCount);

		if (!distances.contains(originNodeId))
			return -2;

		dijkstra(adjacency, distances, originNodeId, nullptr, nullptr);

		int reachable = 0;
		for (int i = 0; i < nodeCount; ++i)
		{
			const double d = distance_of(distances, nodes[i].id);
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

		auto adjacency = build_adjacency(edges, edgeCount);
		const auto pristine = initialize_distances(nodes, nodeCount);

		for (int s = 0; s < sourceCount; ++s)
			if (!pristine.contains(sourceIds[s]))
				return -2;
		for (int t = 0; t < targetCount; ++t)
			if (!pristine.contains(targetIds[t]))
				return -2;

		for (int s = 0; s < sourceCount; ++s)
		{
			// Copying the initialized map is cheaper than rebuilding it, and the
			// adjacency structure above is built once for the whole matrix.
			Distances distances = pristine;
			dijkstra(adjacency, distances, sourceIds[s], nullptr, nullptr);

			for (int t = 0; t < targetCount; ++t)
				outMatrix[static_cast<long long>(s) * targetCount + t] = distance_of(distances, targetIds[t]);
		}

		return 0;
	}
	catch (...)
	{
		return -99;
	}
}
