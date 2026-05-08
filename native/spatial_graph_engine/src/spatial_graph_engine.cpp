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

	std::map<int, std::vector<Edge>> build_adjacency(const GraphEdgeNative* edges, int edgeCount)
	{
		std::map<int, std::vector<Edge>> adjacency;
		for (int i = 0; i < edgeCount; ++i)
		{
			adjacency[edges[i].fromNodeId].push_back({ edges[i].toNodeId, edges[i].cost });
			if (edges[i].bidirectional != 0)
				adjacency[edges[i].toNodeId].push_back({ edges[i].fromNodeId, edges[i].cost });
		}

		return adjacency;
	}

	std::map<int, double> initialize_distances(const GraphNodeNative* nodes, int nodeCount)
	{
		std::map<int, double> distances;
		for (int i = 0; i < nodeCount; ++i)
			distances[nodes[i].id] = std::numeric_limits<double>::infinity();
		return distances;
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
		std::map<int, int> previous;
		using QueueItem = std::pair<double, int>;
		std::priority_queue<QueueItem, std::vector<QueueItem>, std::greater<QueueItem>> queue;

		if (!distances.contains(startNodeId) || !distances.contains(endNodeId))
			return -2;

		distances[startNodeId] = 0.0;
		queue.push({ 0.0, startNodeId });

		while (!queue.empty())
		{
			const auto [distance, current] = queue.top();
			queue.pop();
			if (distance > distances[current])
				continue;
			if (current == endNodeId)
				break;

			for (const auto& edge : adjacency[current])
			{
				const double candidate = distances[current] + edge.cost;
				if (candidate < distances[edge.to])
				{
					distances[edge.to] = candidate;
					previous[edge.to] = current;
					queue.push({ candidate, edge.to });
				}
			}
		}

		*result = {};
		if (!std::isfinite(distances[endNodeId]))
			return 0;

		std::vector<int> path;
		for (int current = endNodeId; current != startNodeId; current = previous[current])
			path.push_back(current);
		path.push_back(startNodeId);
		std::reverse(path.begin(), path.end());

		if (static_cast<int>(path.size()) > outputCapacity)
			return -3;

		for (int i = 0; i < static_cast<int>(path.size()); ++i)
			outputNodeIds[i] = path[i];

		result->found = 1;
		result->totalCost = distances[endNodeId];
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
		using QueueItem = std::pair<double, int>;
		std::priority_queue<QueueItem, std::vector<QueueItem>, std::greater<QueueItem>> queue;

		if (!distances.contains(originNodeId))
			return -2;

		distances[originNodeId] = 0.0;
		queue.push({ 0.0, originNodeId });

		while (!queue.empty())
		{
			const auto [distance, current] = queue.top();
			queue.pop();
			if (distance > distances[current])
				continue;

			for (const auto& edge : adjacency[current])
			{
				const double candidate = distances[current] + edge.cost;
				if (candidate < distances[edge.to])
				{
					distances[edge.to] = candidate;
					queue.push({ candidate, edge.to });
				}
			}
		}

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
