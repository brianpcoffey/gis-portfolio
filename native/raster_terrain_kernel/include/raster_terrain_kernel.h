#pragma once

#include <cstdint>

#if defined(_WIN32)
	#define RASTER_API __declspec(dllexport)
#else
	#define RASTER_API __attribute__((visibility("default")))
#endif

// Version of this library's exported C ABI. The managed bridge calls Raster_AbiVersion before anything
// else and does not use a library that reports another version or has no such export, so a library built
// before an ABI change is never called with the new argument list (a call made against the wrong
// signature takes whatever is left in the registers for pointers and lengths).
//   (no export)  Raster_GenerateHillshade without elevationLength
//   2            elevationLength added after `elevation`
// Bump it whenever an exported signature or struct layout changes, together with
// RasterTerrainNativeInterop.ExpectedAbiVersion in Portfolio.Services/Native.
#define RASTER_ABI_VERSION 2

#pragma pack(push, 8)
struct WeightedPointNative
{
	double x;
	double y;
	double weight;
};

struct RasterExtentNative
{
	double minX;
	double minY;
	double maxX;
	double maxY;
};
#pragma pack(pop)

// ABI conventions shared by every entry point below.
//   * Every length and count is in ELEMENTS, not bytes.
//   * Buffers are caller-allocated and caller-owned. The kernel keeps no pointer past its return,
//     allocates nothing the caller must free, and never lets a C++ exception cross the boundary.
//   * Grids are row-major: cell (x, y) of a width x height grid is element y * width + x.
//   * width and height must both be > 0 and width * height must fit in a 32-bit int. The product
//     is formed in 64 bits, so it can neither overflow nor wrap to a small value.
//   * Return value: 0 on success, otherwise a negative status (Raster_AbiVersion is the exception: it
//     returns the version number).
//        -1  invalid argument (null pointer; width or height <= 0; width * height larger than
//            INT_MAX; a non-positive cellSize or radius; a length that does not fit the grid;
//            a negative pointCount)
//        -2  degenerate extent (heatmap only): minX >= maxX or minY >= maxY
//       -99  internal exception
//     Statuses -1 and -2 are returned before anything is written to an output buffer.
//   * Preconditions the kernel cannot check: each pointer really addresses the number of elements
//     its paired length claims, and every double is finite. The library is built with fast-math,
//     so NaN and infinity are not detected; the managed service rejects them before any call.
extern "C"
{
	// Returns RASTER_ABI_VERSION. Takes no arguments and touches no memory.
	RASTER_API std::int32_t Raster_AbiVersion(void);

	// Shaded relief of an elevation grid. Each cell's slope and aspect come from the central
	// differences of its neighbours (the grid edge is clamped) and are lit by a sun at
	// azimuthDegrees and altitudeDegrees. Writes one intensity byte (0 = dark, 255 = fully lit)
	// per cell.
	//   elevation        in   Non-null. `elevationLength` doubles, row-major. Only read.
	//   elevationLength  in   Number of doubles readable at `elevation`. Must equal width * height
	//                         exactly, the same rule the managed service enforces.
	//   width, height    in   Grid size in cells (see the shared rules above).
	//   cellSize         in   Edge length of one cell, same unit as the elevations. Must be > 0.
	//   azimuthDegrees   in   Sun azimuth in degrees (converted as 360 - azimuth + 90).
	//   altitudeDegrees  in   Sun altitude in degrees.
	//   intensities      out  Non-null, one byte per cell, same row-major layout.
	//   outputLength     in   Number of bytes writable at `intensities`. Must be >= width * height;
	//                         exactly width * height bytes are written.
	RASTER_API int Raster_GenerateHillshade(
		const double* elevation,
		int elevationLength,
		int width,
		int height,
		double cellSize,
		double azimuthDegrees,
		double altitudeDegrees,
		std::uint8_t* intensities,
		int outputLength);

	// Gaussian-weighted density of weighted points over a width x height grid spanning `extent`,
	// sampled at cell centres and divided by its largest value when that is positive, so the
	// peak is 1. Cost is width * height * pointCount evaluations; the managed service caps it.
	//   points           in   `pointCount` WeightedPointNative records. Non-null, even for a count
	//                         of zero (pass a valid address). Only read.
	//   pointCount       in   Number of records at `points`; >= 0.
	//   extent           in   Non-null, exactly ONE RasterExtentNative (not an array); needs
	//                         minX < maxX and minY < maxY, else status -2. Only read.
	//   width, height    in   Grid size in cells (see the shared rules above).
	//   radius           in   Gaussian sigma in the extent's units. Must be > 0.
	//   values           out  Non-null, one double per cell, same row-major layout.
	//   outputLength     in   Number of doubles (not bytes) writable at `values`. Must be
	//                         >= width * height; exactly width * height doubles are written.
	RASTER_API int Raster_GenerateHeatmap(
		const WeightedPointNative* points,
		int pointCount,
		const RasterExtentNative* extent,
		int width,
		int height,
		double radius,
		double* values,
		int outputLength);
}
