#pragma once

#include <cstdint>

#if defined(_WIN32)
	#define RASTER_API __declspec(dllexport)
#else
	#define RASTER_API __attribute__((visibility("default")))
#endif

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

extern "C"
{
	RASTER_API int Raster_GenerateHillshade(
		const double* elevation,
		int width,
		int height,
		double cellSize,
		double azimuthDegrees,
		double altitudeDegrees,
		std::uint8_t* intensities,
		int outputLength);

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
