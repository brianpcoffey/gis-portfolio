#pragma once

/*
 * spatial_cluster_kernel.h
 *
 * Public C ABI for the density-based clustering (DBSCAN) kernel.
 *
 * ClusterPointNative is blittable and packed (#pragma pack(8)); it mirrors the C# struct in
 * Portfolio.Services/Native/SpatialClusterNativeStructs.cs, and the entry point is declared in
 * SpatialClusterNativeInterop.cs. Change the header, those two files, the source and the parity
 * tests together.
 *
 * Ownership and lifetime
 *   - The caller allocates `points` and `labels` and keeps ownership of both. The kernel never
 *     frees caller memory and never keeps a pointer after the call returns.
 *   - Every pointer is followed by the element count the kernel may touch: `points` by `count`,
 *     `labels` by `outputLength`.
 *
 * Threading
 *   - Fully reentrant: no global or static mutable state.
 *
 * Errors
 *   - No C++ exception crosses the boundary; failure is a negative return value (below).
 *   - On a negative return the contents of `labels` are unspecified.
 */

#include <cstdint>

#if defined(_WIN32)
	#define CLUSTER_API __declspec(dllexport)
#else
	#define CLUSTER_API __attribute__((visibility("default")))
#endif

/*
 * Return value of Cluster_RunDbscan.
 *
 *  >= 0  success: the number of clusters found
 *    -1  CLUSTER_ERR_INVALID_ARGUMENT  a null pointer, count <= 0, outputLength < count,
 *                                      epsilon not finite and > 0, or minPoints < 1
 *   -99  CLUSTER_ERR_EXCEPTION         a C++ exception was caught at the boundary (for example
 *                                      std::bad_alloc)
 *
 * A labels buffer that is too small is reported as -1; this kernel has no separate
 * "buffer too small" code.
 */
#define CLUSTER_ERR_INVALID_ARGUMENT (-1)
#define CLUSTER_ERR_EXCEPTION (-99)

#pragma pack(push, 8)
struct ClusterPointNative
{
	double x;
	double y;
};
#pragma pack(pop)

extern "C"
{
	/*
	 * Cluster_RunDbscan - density-based clustering of 2D points.
	 *
	 * Parameters
	 *   points        in   `count` points, read only.
	 *   count         in   number of points; must be > 0.
	 *   epsilon       in   neighbourhood radius (Euclidean, inclusive); finite and > 0.
	 *   minPoints     in   minimum neighbourhood size for a core point, counting the point
	 *                      itself; >= 1.
	 *   labels        out  caller-allocated buffer of at least `outputLength` elements; the first
	 *                      `count` are written, one per input point and in input order:
	 *                      -1 for noise, otherwise a zero-based cluster id.
	 *   outputLength  in   capacity of `labels` in elements; must be >= count.
	 *
	 * Returns the number of clusters (>= 0) or a negative status (above).
	 *
	 * Cluster ids are assigned in the order the input scan first reaches an unlabelled core point,
	 * and a border point reachable from two clusters keeps the first one that reached it. The
	 * managed SpatialClusterService produces exactly the same labels.
	 *
	 * Cost: O(count^2) time (one linear region query per point visited) and O(count) extra
	 * memory. Cluster expansion queues each point at most once per cluster, so the work list
	 * never holds more than `count` entries. The kernel enforces no upper bound on `count`; the
	 * managed service caps it at 5,000 points.
	 *
	 * Coordinates are not validated. The managed service rejects non-finite values before any
	 * call; passing one directly is memory-safe, but the labels involving it are unspecified.
	 */
	CLUSTER_API int Cluster_RunDbscan(
		const ClusterPointNative* points,
		int count,
		double epsilon,
		int minPoints,
		std::int32_t* labels,
		int outputLength);
}
