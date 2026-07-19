#pragma once

#include <cstdint>

#if defined(_WIN32)
	#define GRAPH_API __declspec(dllexport)
#else
	#define GRAPH_API __attribute__((visibility("default")))
#endif

#pragma pack(push, 8)
struct GraphNodeNative
{
	std::int32_t id;
	double latitude;
	double longitude;
};

struct GraphEdgeNative
{
	std::int32_t fromNodeId;
	std::int32_t toNodeId;
	double cost;
	std::int32_t bidirectional;
};

struct GraphPathResultNative
{
	std::int32_t found;
	double totalCost;
	std::int32_t pathCount;
};
#pragma pack(pop)

extern "C"
{
	GRAPH_API int Graph_FindShortestPath(
		const GraphNodeNative* nodes,
		int nodeCount,
		const GraphEdgeNative* edges,
		int edgeCount,
		int startNodeId,
		int endNodeId,
		int* outputNodeIds,
		int outputCapacity,
		GraphPathResultNative* result);

	GRAPH_API int Graph_ComputeServiceArea(
		const GraphNodeNative* nodes,
		int nodeCount,
		const GraphEdgeNative* edges,
		int edgeCount,
		int originNodeId,
		double maxCost,
		int* reachableNodeIds,
		int outputCapacity,
		int* reachableCount);

	// One-to-all shortest-path costs from a single origin.
	//
	// `outDistances` is PARALLEL TO THE INPUT `nodes` ARRAY: outDistances[i] is the cost
	// to nodes[i], not to node id i. This deliberately avoids the one-based/zero-based
	// node-id trap. Unreachable nodes receive infinity.
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
	// Row-major: outMatrix[s * targetCount + t] is the cost from sourceIds[s] to
	// targetIds[t]. Unreachable pairs receive infinity. The adjacency structure is built
	// ONCE and reused across all sourceCount searches, which is the whole point of having
	// a matrix entry point rather than looping Graph_FindShortestPath.
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
}
