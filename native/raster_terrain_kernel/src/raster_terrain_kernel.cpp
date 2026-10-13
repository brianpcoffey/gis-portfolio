#include "raster_terrain_kernel.h"

#include <algorithm>
#include <cmath>
#include <limits>

namespace
{
	// Number of cells in a width x height grid, or -1 when the grid is unusable: a dimension that
	// is not positive, or more cells than a 32-bit int can index. The product is formed in 64 bits
	// (two positive ints always fit), so it can neither overflow nor wrap to a small value, and
	// every `y * width + x` below stays under INT_MAX once this has accepted the grid.
	long long grid_cell_count(int width, int height)
	{
		if (width <= 0 || height <= 0)
			return -1;

		const long long cells = static_cast<long long>(width) * height;
		return cells > std::numeric_limits<int>::max() ? -1 : cells;
	}

	double sample(const double* elevation, int width, int height, int x, int y)
	{
		const int clampedX = std::clamp(x, 0, width - 1);
		const int clampedY = std::clamp(y, 0, height - 1);
		return elevation[clampedY * width + clampedX];
	}

	double radians(double degrees)
	{
		return degrees * 3.14159265358979323846 / 180.0;
	}
}

extern "C" RASTER_API int Raster_GenerateHillshade(
	const double* elevation,
	int elevationLength,
	int width,
	int height,
	double cellSize,
	double azimuthDegrees,
	double altitudeDegrees,
	std::uint8_t* intensities,
	int outputLength)
{
	try
	{
		const long long cells = grid_cell_count(width, height);
		if (!elevation || !intensities || cells < 0 || cellSize <= 0 || elevationLength != cells || outputLength < cells)
			return -1;

		const double azimuth = radians(360.0 - azimuthDegrees + 90.0);
		const double altitude = radians(altitudeDegrees);

		for (int y = 0; y < height; ++y)
		{
			for (int x = 0; x < width; ++x)
			{
				const double dzdx = sample(elevation, width, height, x + 1, y) - sample(elevation, width, height, x - 1, y);
				const double dzdy = sample(elevation, width, height, x, y + 1) - sample(elevation, width, height, x, y - 1);
				const double slope = std::atan(std::sqrt(dzdx * dzdx + dzdy * dzdy) / (2.0 * cellSize));
				const double aspect = std::atan2(dzdy, -dzdx);
				const double shaded = std::sin(altitude) * std::cos(slope)
					+ std::cos(altitude) * std::sin(slope) * std::cos(azimuth - aspect);
				intensities[y * width + x] = static_cast<std::uint8_t>(std::clamp(std::round(255.0 * std::max(0.0, shaded)), 0.0, 255.0));
			}
		}

		return 0;
	}
	catch (...)
	{
		return -99;
	}
}

extern "C" RASTER_API int Raster_GenerateHeatmap(
	const WeightedPointNative* points,
	int pointCount,
	const RasterExtentNative* extent,
	int width,
	int height,
	double radius,
	double* values,
	int outputLength)
{
	try
	{
		const long long cells = grid_cell_count(width, height);
		if (!points || !extent || !values || pointCount < 0 || cells < 0 || radius <= 0 || outputLength < cells)
			return -1;
		if (extent->minX >= extent->maxX || extent->minY >= extent->maxY)
			return -2;

		const double xStep = (extent->maxX - extent->minX) / width;
		const double yStep = (extent->maxY - extent->minY) / height;
		double maxValue = 0.0;

		for (int y = 0; y < height; ++y)
		{
			const double py = extent->minY + (y + 0.5) * yStep;
			for (int x = 0; x < width; ++x)
			{
				const double px = extent->minX + (x + 0.5) * xStep;
				double value = 0.0;
				for (int i = 0; i < pointCount; ++i)
				{
					const double dx = px - points[i].x;
					const double dy = py - points[i].y;
					const double distanceSquared = dx * dx + dy * dy;
					value += points[i].weight * std::exp(-distanceSquared / (2.0 * radius * radius));
				}

				values[y * width + x] = value;
				maxValue = std::max(maxValue, value);
			}
		}

		if (maxValue > 0.0)
		{
			const int total = static_cast<int>(cells);
			for (int i = 0; i < total; ++i)
				values[i] /= maxValue;
		}

		return 0;
	}
	catch (...)
	{
		return -99;
	}
}

extern "C" RASTER_API std::int32_t Raster_AbiVersion(void)
{
	return RASTER_ABI_VERSION;
}
