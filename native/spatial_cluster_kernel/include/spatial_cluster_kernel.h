#pragma once

#include <cstdint>

#if defined(_WIN32)
	#define CLUSTER_API __declspec(dllexport)
#else
	#define CLUSTER_API __attribute__((visibility("default")))
#endif

#pragma pack(push, 8)
struct ClusterPointNative
{
	double x;
	double y;
};
#pragma pack(pop)

extern "C"
{
	// Runs density-based clustering (DBSCAN) over the supplied points.
	// Writes one label per point into `labels`: -1 for noise, otherwise a
	// zero-based cluster id. Returns the number of clusters found (>= 0) on
	// success, or a negative status on invalid input.
	CLUSTER_API int Cluster_RunDbscan(
		const ClusterPointNative* points,
		int count,
		double epsilon,
		int minPoints,
		std::int32_t* labels,
		int outputLength);
}
