#include "geostream_processor.h"

#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <map>
#include <utility>

namespace
{
	struct Accumulator
	{
		int count{};
		double speedSum{};
		double maxSpeed{};
		int anomalyCount{};
	};

	// The two tests below read the IEEE-754 bit pattern instead of calling std::isfinite / std::isnan
	// or comparing the value with itself. This kernel is built without fast math (see its CMakeLists.txt:
	// the cell-index division has to stay a division), so those calls would work today. The bit tests
	// stay so that the validation does not depend on the build flags: under -ffast-math (GCC) or
	// /fp:fast (MSVC) the compiler may assume no NaN or infinity exists and fold a float test to a
	// constant, and integer operations on the bits cannot be folded that way.
	constexpr std::uint64_t kExponentMask = 0x7FF0000000000000ULL;
	constexpr std::uint64_t kMantissaMask = 0x000FFFFFFFFFFFFFULL;

	std::uint64_t bits_of(double v) noexcept
	{
		std::uint64_t bits;
		std::memcpy(&bits, &v, sizeof bits);
		return bits;
	}

	bool is_finite(double v) noexcept
	{
		return (bits_of(v) & kExponentMask) != kExponentMask;
	}

	bool is_nan(double v) noexcept
	{
		const std::uint64_t bits = bits_of(v);
		return (bits & kExponentMask) == kExponentMask && (bits & kMantissaMask) != 0;
	}

	// Converts a double to int exactly as .NET does for (int)value, which the managed service uses for
	// the same cell index: NaN becomes 0, and a value outside the int range saturates to INT_MIN or
	// INT_MAX. static_cast<int> of such a value is undefined behaviour in C++, and the instruction the
	// compiler emits for it differs between x86 and ARM, so it must never see an out-of-range operand.
	int saturating_to_int(double v) noexcept
	{
		if (is_nan(v))
			return 0;
		if (v >= 2147483648.0)
			return INT_MAX;
		if (v <= -2147483648.0)
			return INT_MIN;
		return static_cast<int>(v);
	}

	// An event is valid when both coordinates are finite and inside the WGS84 range; this is the same
	// rule as the managed ProcessManaged, so NaN and infinite positions count as invalid there too.
	bool has_valid_position(const TelemetryEventNative& e) noexcept
	{
		return is_finite(e.latitude) && is_finite(e.longitude)
			&& e.latitude >= -90.0 && e.latitude <= 90.0
			&& e.longitude >= -180.0 && e.longitude <= 180.0;
	}
}

extern "C" GEOSTREAM_API int GeoStream_ProcessTelemetryBatch(
	const TelemetryEventNative* events,
	int eventCount,
	const GeoStreamOptionsNative* options,
	GridAggregateNative* aggregates,
	int aggregateCapacity,
	GeoStreamResultNative* result)
{
	if (!events || !options || !aggregates || !result || eventCount < 0 || aggregateCapacity < 0)
		return GEOSTREAM_ERR_INVALID_ARGUMENT;

	// A NaN grid passes `<= 0`, and an infinite one makes every cell index 0, so require finite.
	// Every positive finite grid is accepted: a tiny one is handled by saturating_to_int below.
	if (!is_finite(options->gridSizeDegrees) || options->gridSizeDegrees <= 0.0)
		return GEOSTREAM_ERR_INVALID_ARGUMENT;

	try
	{
		const double grid = options->gridSizeDegrees;
		std::map<std::pair<int, int>, Accumulator> cells;
		*result = {};
		result->totalEvents = eventCount;

		for (int i = 0; i < eventCount; ++i)
		{
			const auto& e = events[i];
			if (!has_valid_position(e))
			{
				++result->invalidEvents;
				continue;
			}

			++result->validEvents;
			const int cellX = saturating_to_int(std::floor((e.longitude + 180.0) / grid));
			const int cellY = saturating_to_int(std::floor((e.latitude + 90.0) / grid));
			auto& acc = cells[{cellX, cellY}];
			++acc.count;
			acc.speedSum += e.speedMetersPerSecond;
			acc.maxSpeed = std::max(acc.maxSpeed, e.speedMetersPerSecond);
			if (e.speedMetersPerSecond > options->anomalySpeedThresholdMetersPerSecond)
			{
				++acc.anomalyCount;
				++result->anomalyCount;
			}
		}

		if (cells.size() > static_cast<std::size_t>(aggregateCapacity))
			return GEOSTREAM_ERR_BUFFER_TOO_SMALL;

		int index = 0;
		for (const auto& [key, acc] : cells)
		{
			aggregates[index].cellX = key.first;
			aggregates[index].cellY = key.second;
			aggregates[index].count = acc.count;
			aggregates[index].averageSpeedMetersPerSecond = acc.count > 0 ? acc.speedSum / acc.count : 0.0;
			aggregates[index].maxSpeedMetersPerSecond = acc.maxSpeed;
			aggregates[index].anomalyCount = acc.anomalyCount;
			aggregates[index].centerLongitude = (key.first + 0.5) * grid - 180.0;
			aggregates[index].centerLatitude = (key.second + 0.5) * grid - 90.0;
			++index;
		}

		result->aggregateCount = index;
		return GEOSTREAM_OK;
	}
	catch (...)
	{
		return GEOSTREAM_ERR_EXCEPTION;
	}
}
