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
	// Returns the number of points assigned to a zone (>= 0), negative on error.
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
