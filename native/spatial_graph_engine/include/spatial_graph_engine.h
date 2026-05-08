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
}
