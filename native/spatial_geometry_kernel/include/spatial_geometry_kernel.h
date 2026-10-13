#pragma once

/*
 * spatial_geometry_kernel.h
 *
 * Public C ABI for the 2D geometry kernel: fan triangulation and Sutherland-Hodgman clipping of
 * a polygon against an axis-aligned box.
 *
 * The structs are blittable and packed (#pragma pack(8)); they mirror the C# structs in
 * Portfolio.Services/Native/SpatialGeometryNativeStructs.cs, and the entry points are declared in
 * SpatialGeometryNativeInterop.cs. Change the header, those two files, the source and the parity
 * tests together.
 *
 * Ownership and lifetime
 *   - The caller allocates every buffer and keeps ownership of all of them. The kernel never
 *     frees caller memory and never keeps a pointer after the call returns. (ClipToBoundingBox
 *     copies the subject into a temporary buffer of its own, released before it returns.)
 *   - Every array pointer is followed by the element count the kernel may touch: input arrays by
 *     their count, output arrays by their capacity. Single-struct pointers (bounds, the count
 *     outputs) point to exactly one element.
 *
 * Threading
 *   - Fully reentrant: no global or static mutable state, so any number of threads may call at
 *     once as long as they do not share an output buffer.
 *
 * Errors
 *   - No C++ exception crosses the boundary: every entry point catches everything and returns a
 *     negative status code, listed per function below. Status 0 is success.
 *   - On a non-zero status the output buffer and the count output are unspecified.
 *   - Coordinates and bounds are not validated for NaN or infinity; the managed service validates
 *     the request before any call.
 */

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
	/*
	 * Geometry_TriangulateFan - triangulate a polygon as a fan from its first vertex.
	 *
	 * Parameters
	 *   points            in   `pointCount` vertices in order, read only. The fan is only a valid
	 *                          triangulation for a polygon that is star-shaped from vertex 0
	 *                          (convex polygons always are); the kernel does not check.
	 *   pointCount        in   number of vertices; must be >= 3.
	 *   triangles         out  caller-allocated buffer of `triangleCapacity` triangles.
	 *   triangleCapacity  in   capacity of `triangles` in triangles; must be >= pointCount - 2.
	 *   triangleCount     out  one int, set to the number of triangles written (pointCount - 2).
	 *
	 * Writes triangle i as (points[0], points[i + 1], points[i + 2]) for i in [0, pointCount - 2).
	 *
	 * Status
	 *    0  success
	 *   -1  a null pointer, or pointCount < 3
	 *   -2  triangleCapacity < pointCount - 2 (the triangle buffer is too small)
	 *  -99  a C++ exception was caught at the boundary
	 */
	GEOMETRY_API int Geometry_TriangulateFan(
		const CoordinateNative* points,
		int pointCount,
		TriangleNative* triangles,
		int triangleCapacity,
		int* triangleCount);

	/*
	 * Geometry_ClipToBoundingBox - clip a polygon to an axis-aligned box.
	 *
	 * Parameters
	 *   subject         in   `subjectCount` vertices in order (the closing edge from the last
	 *                        vertex back to the first is implied), read only.
	 *   subjectCount    in   number of vertices; must be >= 0.
	 *   bounds          in   one BoundingBoxNative, read only; must satisfy minX < maxX and
	 *                        minY < maxY.
	 *   output          out  caller-allocated buffer of `outputCapacity` coordinates.
	 *   outputCapacity  in   capacity of `output` in coordinates. It must be at least
	 *                        subjectCount, and large enough for the clipped polygon (below).
	 *   outputCount     out  one int, set to the number of vertices written; 0 when the polygon
	 *                        lies entirely outside the box.
	 *
	 * The polygon is clipped against the four half-planes in the order minX, maxX, minY, maxY
	 * (Sutherland-Hodgman, same intersection formulas as the managed SpatialGeometryService), so
	 * both paths return the same vertices in the same order. Clipping can add vertices, so the
	 * result can be longer than the subject; the managed bridge allocates 2 * subjectCount and
	 * treats -3 as a native failure, which falls back to the managed implementation.
	 *
	 * Status
	 *    0  success
	 *   -1  a null pointer, subjectCount < 0, or outputCapacity < subjectCount
	 *   -2  degenerate bounds (minX >= maxX or minY >= maxY)
	 *   -3  the clipped polygon has more vertices than outputCapacity
	 *  -99  a C++ exception was caught at the boundary
	 */
	GEOMETRY_API int Geometry_ClipToBoundingBox(
		const CoordinateNative* subject,
		int subjectCount,
		const BoundingBoxNative* bounds,
		CoordinateNative* output,
		int outputCapacity,
		int* outputCount);
}
