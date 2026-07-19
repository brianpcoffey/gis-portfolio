#include "spatial_overlay_kernel.h"

namespace
{
	// Even-odd (crossing-number) point-in-polygon test over a contiguous ring.
	bool point_in_ring(const OverlayPointNative* ring, int size, double px, double py)
	{
		bool inside = false;
		for (int i = 0, j = size - 1; i < size; j = i++)
		{
			const double xi = ring[i].x;
			const double yi = ring[i].y;
			const double xj = ring[j].x;
			const double yj = ring[j].y;
			const bool crosses = ((yi > py) != (yj > py)) &&
				(px < (xj - xi) * (py - yi) / (yj - yi) + xi);
			if (crosses)
				inside = !inside;
		}
		return inside;
	}
}

extern "C" OVERLAY_API int Overlay_AssignPointsToZones(
	const OverlayPointNative* points,
	int pointCount,
	const OverlayPointNative* polygonVertices,
	int totalVertices,
	const int* ringSizes,
	int zoneCount,
	int* assignments,
	int outputLength)
{
	try
	{
		if (!points || !ringSizes || !assignments || pointCount <= 0 || zoneCount <= 0 || outputLength < pointCount)
			return -1;
		if (!polygonVertices || totalVertices <= 0)
			return -2;

		// Validate that the ring sizes account for exactly the supplied vertices.
		int accumulated = 0;
		for (int z = 0; z < zoneCount; ++z)
		{
			if (ringSizes[z] < 3)
				return -3;
			accumulated += ringSizes[z];
		}
		if (accumulated != totalVertices)
			return -4;

		int assignedCount = 0;
		for (int p = 0; p < pointCount; ++p)
		{
			const double px = points[p].x;
			const double py = points[p].y;
			int matched = -1;
			int offset = 0;
			for (int z = 0; z < zoneCount; ++z)
			{
				const int size = ringSizes[z];
				if (point_in_ring(polygonVertices + offset, size, px, py))
				{
					matched = z;
					break;
				}
				offset += size;
			}

			assignments[p] = matched;
			if (matched >= 0)
				++assignedCount;
		}

		return assignedCount;
	}
	catch (...)
	{
		return -99;
	}
}
