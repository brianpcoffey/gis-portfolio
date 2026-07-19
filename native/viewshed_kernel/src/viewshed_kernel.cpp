#include "viewshed_kernel.h"

#include <algorithm>
#include <cmath>

namespace
{
	int sample_index(int x, int y, int width)
	{
		return y * width + x;
	}

	// Half-up rounding, stated explicitly rather than delegating to a language default.
	// std::lround rounds half away from zero and .NET's Math.Round rounds half to even,
	// so relying on either made the two paths walk a different cell whenever a ray sample
	// landed exactly on .5 — 43 cells out of 250,000 on a 500x500 grid. Sample coordinates
	// here are always non-negative, so floor(v + 0.5) is unambiguous and identical to the
	// managed ViewshedService.
	int round_to_int(double value)
	{
		return static_cast<int>(std::floor(value + 0.5));
	}
}

extern "C" VIEWSHED_API int Viewshed_Compute(
	const double* elevation,
	int width,
	int height,
	double cellSize,
	int observerX,
	int observerY,
	double observerHeight,
	std::uint8_t* visibility,
	int outputLength)
{
	try
	{
		if (!elevation || !visibility || width <= 0 || height <= 0 || cellSize <= 0 || outputLength < width * height)
			return -1;
		if (observerX < 0 || observerX >= width || observerY < 0 || observerY >= height)
			return -2;

		const double observerElevation = elevation[sample_index(observerX, observerY, width)] + observerHeight;
		int visibleCount = 0;

		for (int ty = 0; ty < height; ++ty)
		{
			for (int tx = 0; tx < width; ++tx)
			{
				if (tx == observerX && ty == observerY)
				{
					visibility[sample_index(tx, ty, width)] = 1;
					++visibleCount;
					continue;
				}

				const double dx = static_cast<double>(tx - observerX);
				const double dy = static_cast<double>(ty - observerY);
				const int steps = static_cast<int>(std::max(std::abs(dx), std::abs(dy)));
				const double targetDistance = std::sqrt(dx * dx + dy * dy) * cellSize;
				const double targetAngle = (elevation[sample_index(tx, ty, width)] - observerElevation) / targetDistance;

				double maxAngle = -1e30;
				bool blocked = false;
				for (int i = 1; i < steps; ++i)
				{
					const double fraction = static_cast<double>(i) / steps;
					const int cx = round_to_int(observerX + dx * fraction);
					const int cy = round_to_int(observerY + dy * fraction);
					const double distance = fraction * targetDistance;
					if (distance <= 0.0)
						continue;

					const double angle = (elevation[sample_index(cx, cy, width)] - observerElevation) / distance;
					if (angle > maxAngle)
						maxAngle = angle;
					if (targetAngle < maxAngle)
					{
						blocked = true;
						break;
					}
				}

				const std::uint8_t visible = blocked ? 0 : 1;
				visibility[sample_index(tx, ty, width)] = visible;
				visibleCount += visible;
			}
		}

		return visibleCount;
	}
	catch (...)
	{
		return -99;
	}
}
