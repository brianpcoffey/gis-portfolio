#include "spatial_cluster_kernel.h"

#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <vector>

namespace
{
	constexpr std::int32_t kUnvisited = -2;
	constexpr std::int32_t kNoise = -1;
	constexpr std::int32_t kNeverQueued = -1;

	// Bit-level finite test. std::isfinite and comparisons against NaN can be folded away by
	// -ffast-math (GCC) and /fp:fast (MSVC), which let the compiler assume no NaN or infinity exists.
	bool is_finite(double v) noexcept
	{
		constexpr std::uint64_t kExponentMask = 0x7FF0000000000000ULL;
		std::uint64_t bits;
		std::memcpy(&bits, &v, sizeof bits);
		return (bits & kExponentMask) != kExponentMask;
	}

	void region_query(
		const ClusterPointNative* points,
		int count,
		int origin,
		double epsilonSquared,
		std::vector<int>& neighbors)
	{
		neighbors.clear();
		const double ox = points[origin].x;
		const double oy = points[origin].y;
		for (int i = 0; i < count; ++i)
		{
			const double dx = points[i].x - ox;
			const double dy = points[i].y - oy;
			if (dx * dx + dy * dy <= epsilonSquared)
				neighbors.push_back(i);
		}
	}

	// Appends to `seeds` each neighbour that is not already queued for `clusterId`.
	//
	// queuedIn[i] is the cluster that last queued point i. A point queued twice for one cluster is a
	// no-op the second time (the first visit already labelled it), so skipping the duplicate leaves
	// every label unchanged while bounding `seeds` to one entry per point. Without it, each core
	// point appended all of its neighbours again and the list grew quadratically: 12,000 coincident
	// points needed about 1 GB. This is the same bookkeeping as SpatialClusterService.QueueSeeds.
	void queue_seeds(
		std::vector<int>& seeds,
		const std::vector<int>& neighbors,
		std::vector<std::int32_t>& queuedIn,
		std::int32_t clusterId)
	{
		for (const int neighbor : neighbors)
		{
			if (queuedIn[neighbor] == clusterId)
				continue;

			queuedIn[neighbor] = clusterId;
			seeds.push_back(neighbor);
		}
	}
}

extern "C" CLUSTER_API int Cluster_RunDbscan(
	const ClusterPointNative* points,
	int count,
	double epsilon,
	int minPoints,
	std::int32_t* labels,
	int outputLength)
{
	try
	{
		// `epsilon <= 0.0` is false for NaN, and an infinite epsilon would put every point in one
		// neighbourhood, so require a finite positive value.
		if (!points || !labels || count <= 0 || outputLength < count || !is_finite(epsilon) || epsilon <= 0.0 || minPoints < 1)
			return CLUSTER_ERR_INVALID_ARGUMENT;

		for (int i = 0; i < count; ++i)
			labels[i] = kUnvisited;

		const double epsilonSquared = epsilon * epsilon;
		int clusterId = 0;
		std::vector<int> neighbors;
		std::vector<int> seeds;
		std::vector<std::int32_t> queuedIn(static_cast<std::size_t>(count), kNeverQueued);

		for (int p = 0; p < count; ++p)
		{
			if (labels[p] != kUnvisited)
				continue;

			region_query(points, count, p, epsilonSquared, neighbors);
			if (static_cast<int>(neighbors.size()) < minPoints)
			{
				labels[p] = kNoise;
				continue;
			}

			labels[p] = clusterId;
			seeds.clear();
			queue_seeds(seeds, neighbors, queuedIn, clusterId);
			for (std::size_t s = 0; s < seeds.size(); ++s)
			{
				const int q = seeds[s];
				if (labels[q] == kNoise)
					labels[q] = clusterId; // border point reclaimed from noise
				if (labels[q] != kUnvisited)
					continue;

				labels[q] = clusterId;
				region_query(points, count, q, epsilonSquared, neighbors);
				if (static_cast<int>(neighbors.size()) >= minPoints)
					queue_seeds(seeds, neighbors, queuedIn, clusterId);
			}

			++clusterId;
		}

		return clusterId;
	}
	catch (...)
	{
		return CLUSTER_ERR_EXCEPTION;
	}
}
