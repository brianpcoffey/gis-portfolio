#pragma once

#include <cstdint>

#if defined(_WIN32)
	#define GEOSTREAM_API __declspec(dllexport)
#else
	#define GEOSTREAM_API __attribute__((visibility("default")))
#endif

#pragma pack(push, 8)
struct TelemetryEventNative
{
	std::int64_t timestampUnixMs;
	std::int32_t entityId;
	double latitude;
	double longitude;
	double speedMetersPerSecond;
	double headingDegrees;
};

struct GeoStreamOptionsNative
{
	double gridSizeDegrees;
	double anomalySpeedThresholdMetersPerSecond;
};

struct GridAggregateNative
{
	std::int32_t cellX;
	std::int32_t cellY;
	std::int32_t count;
	double averageSpeedMetersPerSecond;
	double maxSpeedMetersPerSecond;
	std::int32_t anomalyCount;
	double centerLatitude;
	double centerLongitude;
};

struct GeoStreamResultNative
{
	std::int32_t totalEvents;
	std::int32_t validEvents;
	std::int32_t invalidEvents;
	std::int32_t anomalyCount;
	std::int32_t aggregateCount;
};
#pragma pack(pop)

extern "C"
{
	GEOSTREAM_API int GeoStream_ProcessTelemetryBatch(
		const TelemetryEventNative* events,
		int eventCount,
		const GeoStreamOptionsNative* options,
		GridAggregateNative* aggregates,
		int aggregateCapacity,
		GeoStreamResultNative* result);
}
