#include "spatial_geometry_kernel.h"

#include <algorithm>

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

		for (int i = 0; i < subjectCount; ++i)
		{
			output[i].x = std::clamp(subject[i].x, bounds->minX, bounds->maxX);
			output[i].y = std::clamp(subject[i].y, bounds->minY, bounds->maxY);
		}

		*outputCount = subjectCount;
		return 0;
	}
	catch (...)
	{
		return -99;
	}
}
