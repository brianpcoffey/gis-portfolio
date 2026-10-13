#pragma once

#include <cstdint>

#if defined(_WIN32)
	#define VIEWSHED_API __declspec(dllexport)
#else
	#define VIEWSHED_API __attribute__((visibility("default")))
#endif

// Version of this library's exported C ABI. The managed bridge calls Viewshed_AbiVersion before anything
// else and does not use a library that reports another version or has no such export, so a library built
// before an ABI change is never called with the new argument list (a call made against the wrong
// signature takes whatever is left in the registers for pointers and lengths).
//   (no export)  Viewshed_Compute without elevationLength
//   2            elevationLength added after `elevation`
// Bump it whenever an exported signature or struct layout changes, together with
// ViewshedNativeInterop.ExpectedAbiVersion in Portfolio.Services/Native.
#define VIEWSHED_ABI_VERSION 2

extern "C"
{
	// Returns VIEWSHED_ABI_VERSION. Takes no arguments and touches no memory.
	VIEWSHED_API std::int32_t Viewshed_AbiVersion(void);

	// Computes a line-of-sight viewshed over an elevation grid from an observer cell.
	// `elevation` is row-major: cell (x, y) is elevation[y * width + x]. The observer sits at
	// (observerX, observerY) at elevation[cell] + observerHeight. Every cell is
	// tested by walking the ray from the observer and tracking the maximum vertical
	// angle: a cell is visible when its own angle meets or exceeds the running
	// maximum. Writes 1 (visible) or 0 (hidden) per cell into `visibility`.
	//
	// Parameters. Every length and count is in ELEMENTS, not bytes.
	//   elevation        in   Non-null. `elevationLength` doubles, row-major. The kernel only reads it.
	//   elevationLength  in   Number of doubles readable at `elevation`. Must equal width * height
	//                         exactly, the same rule the managed service enforces.
	//   width, height    in   Grid size in cells. Both must be > 0, and width * height must fit in a
	//                         32-bit int (it is formed in 64 bits, so it cannot overflow).
	//   cellSize         in   Edge length of one cell, in the same unit as the elevations. Must be > 0.
	//   observerX/Y      in   Observer cell: 0 <= observerX < width and 0 <= observerY < height.
	//   observerHeight   in   Height above the observer cell's elevation, same unit as the elevations.
	//   visibility       out  Non-null, caller-allocated, same row-major layout, one byte per cell.
	//   outputLength     in   Number of bytes writable at `visibility`. Must be >= width * height;
	//                         exactly width * height bytes are written.
	//
	// Preconditions the kernel cannot check: `elevation` really holds `elevationLength` readable
	// doubles and `visibility` really holds `outputLength` writable bytes, and every double is
	// finite. The library is built with fast-math, so NaN and infinity are not detected; the
	// managed service rejects them before any call.
	//
	// Ownership: the caller owns both buffers for the duration of the call. The kernel keeps no
	// pointer past its return, allocates nothing the caller must free, and never lets a C++
	// exception cross the boundary.
	//
	// Returns the number of visible cells (>= 0) on success, otherwise a negative status:
	//    -1  invalid argument: null pointer; width or height <= 0; width * height larger than
	//        INT_MAX; cellSize <= 0; elevationLength != width * height; outputLength < width * height
	//    -2  observer cell outside the grid
	//   -99  internal exception
	// Statuses -1 and -2 are returned before anything is written, so `visibility` is left untouched.
	VIEWSHED_API int Viewshed_Compute(
		const double* elevation,
		int elevationLength,
		int width,
		int height,
		double cellSize,
		int observerX,
		int observerY,
		double observerHeight,
		std::uint8_t* visibility,
		int outputLength);
}
