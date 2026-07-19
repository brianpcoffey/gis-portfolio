#pragma once

#include <cstdint>

#if defined(_WIN32)
	#define VIEWSHED_API __declspec(dllexport)
#else
	#define VIEWSHED_API __attribute__((visibility("default")))
#endif

extern "C"
{
	// Computes a line-of-sight viewshed over an elevation grid from an observer cell.
	// `elevation` is row-major (width * height). The observer sits at
	// (observerX, observerY) at elevation[cell] + observerHeight. Every cell is
	// tested by walking the ray from the observer and tracking the maximum vertical
	// angle: a cell is visible when its own angle meets or exceeds the running
	// maximum. Writes 1 (visible) or 0 (hidden) per cell into `visibility`.
	// Returns the number of visible cells (>= 0) on success, negative on error.
	VIEWSHED_API int Viewshed_Compute(
		const double* elevation,
		int width,
		int height,
		double cellSize,
		int observerX,
		int observerY,
		double observerHeight,
		std::uint8_t* visibility,
		int outputLength);
}
