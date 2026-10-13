#pragma once

#if defined(_WIN32)
	#define OVERLAY_API __declspec(dllexport)
#else
	#define OVERLAY_API __attribute__((visibility("default")))
#endif

#pragma pack(push, 8)
struct OverlayPointNative
{
	double x;
	double y;
};
#pragma pack(pop)

extern "C"
{
	// Assigns each input point to the first zone polygon that contains it.
	// Zones are supplied as a flat vertex buffer plus per-zone ring sizes:
	// zone i owns the vertices in the contiguous slice whose length is
	// ringSizes[i], laid out in the same order in `polygonVertices`.
	// Containment uses even-odd ray casting. Writes the zero-based zone index
	// (or -1 when a point falls outside every zone) into `assignments`.
	// Returns the number of points assigned to a zone (>= 0), negative on error:
	//    -1  invalid argument: a null points, ringSizes or assignments; pointCount or zoneCount <= 0;
	//        outputLength < pointCount; or ring sizes whose sum does not fit in an int
	//    -2  no polygon vertices: a null polygonVertices or totalVertices <= 0
	//    -3  a ring with fewer than three vertices
	//    -4  ring sizes that do not add up to totalVertices exactly
	//   -99  internal exception
	// Statuses -1 to -4 are returned before anything is written to `assignments`. The sum of the
	// ring sizes is formed in 64 bits, so it cannot wrap to the value of totalVertices.
	OVERLAY_API int Overlay_AssignPointsToZones(
		const OverlayPointNative* points,
		int pointCount,
		const OverlayPointNative* polygonVertices,
		int totalVertices,
		const int* ringSizes,
		int zoneCount,
		int* assignments,
		int outputLength);
}
