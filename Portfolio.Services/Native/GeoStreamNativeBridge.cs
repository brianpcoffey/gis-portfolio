using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;

namespace Portfolio.Services.Native
{
    internal static class GeoStreamNativeBridge
    {
        private static readonly bool _available = NativeKernelLoader.TryLoad("geostream_processor");

        internal static bool IsAvailable => _available;

        internal static void LogAvailability(ILogger logger)
        {
            logger.LogInformation(_available
                ? "Native geostream processor loaded. Telemetry batches will use the C++ fast path."
                : "Native geostream processor unavailable; using managed telemetry processing implementation.");
        }

        internal static bool TryProcessBatch(
            GeoStreamBatchRequestDto request,
            ILogger logger,
            out GeoStreamBatchResultDto? result)
        {
            result = null;
            if (!_available)
                return false;

            try
            {
                result = ProcessBatch(request);
                return true;
            }
            catch (Exception ex) when (NativeKernelLoader.IsNativeInvocationException(ex))
            {
                logger.LogWarning(ex, "Native geostream processor failed; falling back to managed telemetry processing.");
                return false;
            }
        }

        internal static GeoStreamBatchResultDto ProcessBatch(GeoStreamBatchRequestDto request)
        {
            var events = request.Events.Select(MapEvent).ToArray();
            var aggregates = new GridAggregateNative[Math.Max(1, events.Length)];
            var options = new GeoStreamOptionsNative
            {
                GridSizeDegrees = request.GridSizeDegrees,
                AnomalySpeedThresholdMetersPerSecond = request.AnomalySpeedThresholdMetersPerSecond
            };

            var status = GeoStreamNativeInterop.ProcessTelemetryBatch(
                events,
                events.Length,
                in options,
                aggregates,
                aggregates.Length,
                out var result);

            ThrowIfFailed(status);

            return new GeoStreamBatchResultDto
            {
                TotalEvents = result.TotalEvents,
                ValidEvents = result.ValidEvents,
                InvalidEvents = result.InvalidEvents,
                AnomalyCount = result.AnomalyCount,
                NativeAccelerated = true,
                Aggregates = aggregates.Take(result.AggregateCount).Select(MapAggregate).ToList()
            };
        }

        private static TelemetryEventNative MapEvent(TelemetryEventDto dto)
        {
            return new TelemetryEventNative
            {
                TimestampUnixMs = new DateTimeOffset(dto.TimestampUtc).ToUnixTimeMilliseconds(),
                EntityId = dto.EntityId,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                SpeedMetersPerSecond = dto.SpeedMetersPerSecond,
                HeadingDegrees = dto.HeadingDegrees
            };
        }

        private static GridAggregateDto MapAggregate(GridAggregateNative aggregate)
        {
            return new GridAggregateDto
            {
                CellX = aggregate.CellX,
                CellY = aggregate.CellY,
                Count = aggregate.Count,
                AverageSpeedMetersPerSecond = aggregate.AverageSpeedMetersPerSecond,
                MaxSpeedMetersPerSecond = aggregate.MaxSpeedMetersPerSecond,
                AnomalyCount = aggregate.AnomalyCount,
                CenterLatitude = aggregate.CenterLatitude,
                CenterLongitude = aggregate.CenterLongitude
            };
        }

        // A non-zero status is a failure of the call itself, never a property of the data: the service has
        // already validated the request, and ProcessBatch sizes the aggregate buffer from the event count,
        // so none of these can occur for a validated request. Each one is surfaced as an
        // InvalidOperationException, which TryProcessBatch's IsNativeInvocationException filter turns
        // into a logged fallback to the managed path (the status is never mapped to a validation error,
        // because the managed service has already produced those).
        //   -1  invalid argument (null pointer, negative count, grid size not finite and > 0)
        //   -2  aggregate buffer too small
        //  -99  C++ exception caught at the native boundary
        private static void ThrowIfFailed(int status)
        {
            if (status != 0)
                throw new InvalidOperationException($"Native geostream processor failed with status {status} ({DescribeStatus(status)}).");
        }

        private static string DescribeStatus(int status)
        {
            return status switch
            {
                -1 => "invalid argument",
                -2 => "aggregate buffer too small",
                -99 => "native exception",
                _ => "unknown"
            };
        }
    }
}
