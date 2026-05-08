#pragma once

#if defined(_WIN32)
	#define GEOMETRY_API __declspec(dllexport)
#else
	#define GEOMETRY_API __attribute__((visibility("default")))
#endif

#pragma pack(push, 8)
struct CoordinateNative
{
	double x;
	double y;
};

struct TriangleNative
{
	CoordinateNative a;
	CoordinateNative b;
	CoordinateNative c;
};

struct BoundingBoxNative
{
	double minX;
	double minY;
	double maxX;
	double maxY;
};
#pragma pack(pop)

extern "C"
{
	GEOMETRY_API int Geometry_TriangulateFan(
		const CoordinateNative* points,
		int pointCount,
		TriangleNative* triangles,
		int triangleCapacity,
		int* triangleCount);

	GEOMETRY_API int Geometry_ClipToBoundingBox(
		const CoordinateNative* subject,
		int subjectCount,
		const BoundingBoxNative* bounds,
		CoordinateNative* output,
		int outputCapacity,
		int* outputCount);
}
