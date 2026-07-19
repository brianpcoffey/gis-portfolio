#include "spatial_cluster_kernel.h"

#include <cmath>
#include <vector>

namespace
{
	constexpr std::int32_t kUnvisited = -2;
	constexpr std::int32_t kNoise = -1;

	// Collects the indices of every point within `epsilonSquared` of point `origin`.
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
		if (!points || !labels || count <= 0 || outputLength < count || epsilon <= 0.0 || minPoints < 1)
			return -1;

		for (int i = 0; i < count; ++i)
			labels[i] = kUnvisited;

		const double epsilonSquared = epsilon * epsilon;
		int clusterId = 0;
		std::vector<int> neighbors;
		std::vector<int> seeds;

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
			seeds = neighbors;
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
				{
					for (const int neighbor : neighbors)
						seeds.push_back(neighbor);
				}
			}

			++clusterId;
		}

		return clusterId;
	}
	catch (...)
	{
		return -99;
	}
}
