#pragma once

/*
 * geostream_processor.h
 *
 * Public C ABI for the geospatial telemetry stream kernel: it bins a batch of position events
 * into a square lat/lon grid and reports per-cell counts, speed statistics and anomaly counts.
 *
 * The structs below are blittable and naturally aligned (#pragma pack(8), so they carry padding
 * where a field needs it); they mirror the C# structs in
 * Portfolio.Services/Native/GeoStreamNativeStructs.cs field for field, and the entry point is
 * declared in GeoStreamNativeInterop.cs. Change the header, those two files, the source and the
 * parity tests together.
 *
 * Ownership and lifetime
 *   - The caller allocates every buffer (events, aggregates, options, result) and keeps
 *     ownership of all of them. The kernel never allocates memory that crosses the boundary,
 *     never frees caller memory and never keeps a pointer after the call returns.
 *   - Every pointer is followed by the element count the kernel may touch: `events` by
 *     `eventCount`, `aggregates` by `aggregateCapacity`. `options` and `result` point to exactly
 *     one struct each.
 *
 * Threading
 *   - Fully reentrant: no global or static mutable state, so any number of threads may call it
 *     at once as long as they do not share an output buffer.
 *
 * Errors
 *   - No C++ exception crosses the boundary: every failure is a negative status code (below).
 *   - On a non-zero status the contents of `aggregates` and `*result` are unspecified.
 */

#include <cstdint>

#if defined(_WIN32)
	#define GEOSTREAM_API __declspec(dllexport)
#else
	#define GEOSTREAM_API __attribute__((visibility("default")))
#endif

/*
 * Status codes returned by GeoStream_ProcessTelemetryBatch.
 *
 *    0  GEOSTREAM_OK                    success
 *   -1  GEOSTREAM_ERR_INVALID_ARGUMENT a null pointer, a negative eventCount or aggregateCapacity,
 *                                       or an options->gridSizeDegrees that is not finite and > 0
 *   -2  GEOSTREAM_ERR_BUFFER_TOO_SMALL  more distinct non-empty cells than aggregateCapacity
 *  -99  GEOSTREAM_ERR_EXCEPTION         a C++ exception was caught at the boundary (for example
 *                                       std::bad_alloc)
 *
 * This kernel reports a too-small buffer as -2 (the other kernels use -3 for that); the value is
 * fixed by the existing C# bridge, so it is documented as it is.
 */
#define GEOSTREAM_OK 0
#define GEOSTREAM_ERR_INVALID_ARGUMENT (-1)
#define GEOSTREAM_ERR_BUFFER_TOO_SMALL (-2)
#define GEOSTREAM_ERR_EXCEPTION (-99)

#pragma pack(push, 8)
/* One position report. The kernel reads latitude, longitude and speedMetersPerSecond only;
 * timestampUnixMs, entityId and headingDegrees are carried so the layout matches the managed DTO. */
struct TelemetryEventNative
{
	std::int64_t timestampUnixMs;
	std::int32_t entityId;
	double latitude;             /* degrees; valid when finite and within [-90, 90] */
	double longitude;            /* degrees; valid when finite and within [-180, 180] */
	double speedMetersPerSecond; /* expected finite; not validated (see GeoStream_ProcessTelemetryBatch) */
	double headingDegrees;
};

struct GeoStreamOptionsNative
{
	double gridSizeDegrees;                       /* cell edge in degrees; must be finite and > 0 */
	double anomalySpeedThresholdMetersPerSecond;  /* an event is an anomaly when its speed is strictly greater */
};

/* One non-empty grid cell. */
struct GridAggregateNative
{
	std::int32_t cellX;                  /* floor((longitude + 180) / grid), saturated to int32 */
	std::int32_t cellY;                  /* floor((latitude + 90) / grid), saturated to int32 */
	std::int32_t count;                  /* valid events in the cell */
	double averageSpeedMetersPerSecond;  /* arithmetic mean of the cell's speeds */
	double maxSpeedMetersPerSecond;      /* running maximum, starting from 0 */
	std::int32_t anomalyCount;
	double centerLatitude;               /* (cellY + 0.5) * grid - 90 */
	double centerLongitude;              /* (cellX + 0.5) * grid - 180 */
};

struct GeoStreamResultNative
{
	std::int32_t totalEvents;     /* == eventCount */
	std::int32_t validEvents;
	std::int32_t invalidEvents;   /* totalEvents - validEvents */
	std::int32_t anomalyCount;    /* anomalies among the valid events */
	std::int32_t aggregateCount;  /* aggregates written */
};
#pragma pack(pop)

extern "C"
{
	/*
	 * GeoStream_ProcessTelemetryBatch - aggregate a batch of telemetry events into grid cells.
	 *
	 * Parameters
	 *   events             in   eventCount elements, read only. Must be non-null, even when
	 *                           eventCount is 0.
	 *   eventCount         in   number of events; >= 0.
	 *   options            in   one GeoStreamOptionsNative, read only.
	 *   aggregates         out  caller-allocated buffer of aggregateCapacity elements; the first
	 *                           result->aggregateCount are written, ordered by cellX then cellY.
	 *   aggregateCapacity  in   capacity of `aggregates` in elements; >= 0. The number of distinct
	 *                           cells never exceeds eventCount, so eventCount (at least 1) is
	 *                           always enough.
	 *   result             out  one GeoStreamResultNative, written on success.
	 *
	 * Returns a status code (above). 0 on success.
	 *
	 * Event validity: an event with a NaN, infinite or out-of-range latitude or longitude is
	 * counted in invalidEvents and otherwise ignored. This is the managed rule, so native and
	 * managed agree on such events.
	 *
	 * Cell index conversion: the quotient floor((coordinate + offset) / grid) is converted to
	 * int32 the way .NET converts (int)double - an out-of-range value saturates to INT32_MIN or
	 * INT32_MAX and NaN becomes 0 - never with a raw C++ cast, whose behaviour for such a value is
	 * undefined. With a grid of at least 1e-6 degrees (the managed service minimum) the index
	 * always fits, so saturation only matters for a smaller grid supplied directly to the kernel.
	 * The division is a true IEEE-754 division: the library is built without fast math (see its
	 * CMakeLists.txt), which would otherwise multiply by 1 / grid and move a coordinate that sits
	 * on a cell boundary into the neighbouring cell. The index is therefore exactly the one the
	 * managed service computes.
	 *
	 * Speeds: the kernel does not validate speedMetersPerSecond or the anomaly threshold. The
	 * managed service rejects non-finite values before any call. A non-finite value passed
	 * directly is memory-safe but makes the affected cell's statistics meaningless.
	 */
	GEOSTREAM_API int GeoStream_ProcessTelemetryBatch(
		const TelemetryEventNative* events,
		int eventCount,
		const GeoStreamOptionsNative* options,
		GridAggregateNative* aggregates,
		int aggregateCapacity,
		GeoStreamResultNative* result);
}
