#include "spatial_geometry_kernel.h"

#include <algorithm>
#include <cstddef>
#include <vector>

namespace
{
	enum class Edge { MinX, MaxX, MinY, MaxY };

	bool inside(const CoordinateNative& p, Edge edge, double limit)
	{
		switch (edge)
		{
		case Edge::MinX: return p.x >= limit;
		case Edge::MaxX: return p.x <= limit;
		case Edge::MinY: return p.y >= limit;
		default:         return p.y <= limit;
		}
	}

	// Intersection of segment a->b with the clip line. Only called when the endpoints
	// straddle the boundary, so the denominator is non-zero.
	CoordinateNative intersect(const CoordinateNative& a, const CoordinateNative& b, Edge edge, double limit)
	{
		CoordinateNative r{};
		if (edge == Edge::MinX || edge == Edge::MaxX)
		{
			const double t = (limit - a.x) / (b.x - a.x);
			r.x = limit;
			r.y = a.y + t * (b.y - a.y);
		}
		else
		{
			const double t = (limit - a.y) / (b.y - a.y);
			r.x = a.x + t * (b.x - a.x);
			r.y = limit;
		}
		return r;
	}

	// One Sutherland-Hodgman pass. `scratch` is reused across all four calls rather than
	// allocated per pass — per-iteration allocation is exactly what made two sibling
	// kernels in this repo slower than their managed fallbacks.
	void clip_half_plane(std::vector<CoordinateNative>& polygon, std::vector<CoordinateNative>& scratch, Edge edge, double limit)
	{
		scratch.clear();
		const std::size_t n = polygon.size();
		if (n == 0)
			return;

		for (std::size_t i = 0; i < n; ++i)
		{
			const CoordinateNative& cur = polygon[i];
			const CoordinateNative& prev = polygon[(i + n - 1) % n];
			const bool curIn = inside(cur, edge, limit);
			const bool prevIn = inside(prev, edge, limit);

			if (curIn)
			{
				if (!prevIn)
					scratch.push_back(intersect(prev, cur, edge, limit)); // entering the box
				scratch.push_back(cur);
			}
			else if (prevIn)
			{
				scratch.push_back(intersect(prev, cur, edge, limit)); // leaving the box
			}
		}

		polygon.swap(scratch);
	}
}

extern "C" GEOMETRY_API int Geometry_TriangulateFan(
	const CoordinateNative* points,
	int pointCount,
	TriangleNative* triangles,
	int triangleCapacity,
	int* triangleCount)
{
	try
	{
		if (!points || !triangles || !triangleCount || pointCount < 3)
			return -1;

		const int required = pointCount - 2;
		if (triangleCapacity < required)
			return -2;

		for (int i = 0; i < required; ++i)
		{
			triangles[i].a = points[0];
			triangles[i].b = points[i + 1];
			triangles[i].c = points[i + 2];
		}

		*triangleCount = required;
		return 0;
	}
	catch (...)
	{
		return -99;
	}
}

extern "C" GEOMETRY_API int Geometry_ClipToBoundingBox(
	const CoordinateNative* subject,
	int subjectCount,
	const BoundingBoxNative* bounds,
	CoordinateNative* output,
	int outputCapacity,
	int* outputCount)
{
	try
	{
		if (!subject || !bounds || !output || !outputCount || subjectCount < 0 || outputCapacity < subjectCount)
			return -1;
		if (bounds->minX >= bounds->maxX || bounds->minY >= bounds->maxY)
			return -2;

		// Sutherland-Hodgman: clip the subject polygon against each of the four box
		// half-planes in turn, mirroring SpatialGeometryService.ClipToBoundingBoxAsync
		// exactly — same half-plane order, same previous-vertex indexing, same
		// intersection formulas — so both paths return identical geometry.
		//
		// This previously clamped each vertex independently, which is not polygon
		// clipping: it produced a vertex count equal to the input with every outside
		// vertex squashed onto the box edge, never emitted a true edge/box intersection,
		// and returned a full-size degenerate polygon for a subject lying entirely
		// outside the box instead of an empty one.
		std::vector<CoordinateNative> current(subject, subject + subjectCount);
		std::vector<CoordinateNative> next;
		next.reserve(static_cast<std::size_t>(subjectCount) + 4);

		clip_half_plane(current, next, Edge::MinX, bounds->minX);
		clip_half_plane(current, next, Edge::MaxX, bounds->maxX);
		clip_half_plane(current, next, Edge::MinY, bounds->minY);
		clip_half_plane(current, next, Edge::MaxY, bounds->maxY);

		if (static_cast<int>(current.size()) > outputCapacity)
			return -3;

		for (std::size_t i = 0; i < current.size(); ++i)
			output[i] = current[i];

		*outputCount = static_cast<int>(current.size());
		return 0;
	}
	catch (...)
	{
		return -99;
	}
}
