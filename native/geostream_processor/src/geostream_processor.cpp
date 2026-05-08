#include "geostream_processor.h"

#include <algorithm>
#include <cmath>
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
}

extern "C" GEOSTREAM_API int GeoStream_ProcessTelemetryBatch(
	const TelemetryEventNative* events,
	int eventCount,
	const GeoStreamOptionsNative* options,
	GridAggregateNative* aggregates,
	int aggregateCapacity,
	GeoStreamResultNative* result)
{
	if (!events || !options || !aggregates || !result || eventCount < 0 || aggregateCapacity < 0 || options->gridSizeDegrees <= 0)
		return -1;

	try
	{
		std::map<std::pair<int, int>, Accumulator> cells;
		*result = {};
		result->totalEvents = eventCount;

		for (int i = 0; i < eventCount; ++i)
		{
			const auto& e = events[i];
			if (e.latitude < -90.0 || e.latitude > 90.0 || e.longitude < -180.0 || e.longitude > 180.0)
			{
				++result->invalidEvents;
				continue;
			}

			++result->validEvents;
			const auto cellX = static_cast<int>(std::floor((e.longitude + 180.0) / options->gridSizeDegrees));
			const auto cellY = static_cast<int>(std::floor((e.latitude + 90.0) / options->gridSizeDegrees));
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

		if (static_cast<int>(cells.size()) > aggregateCapacity)
			return -2;

		int index = 0;
		for (const auto& [key, acc] : cells)
		{
			aggregates[index].cellX = key.first;
			aggregates[index].cellY = key.second;
			aggregates[index].count = acc.count;
			aggregates[index].averageSpeedMetersPerSecond = acc.count > 0 ? acc.speedSum / acc.count : 0.0;
			aggregates[index].maxSpeedMetersPerSecond = acc.maxSpeed;
			aggregates[index].anomalyCount = acc.anomalyCount;
			aggregates[index].centerLongitude = (key.first + 0.5) * options->gridSizeDegrees - 180.0;
			aggregates[index].centerLatitude = (key.second + 0.5) * options->gridSizeDegrees - 90.0;
			++index;
		}

		result->aggregateCount = index;
		return 0;
	}
	catch (...)
	{
		return -99;
	}
}
