using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Native;

namespace Portfolio.Services.Services
{
    public class GeoStreamProcessorService : IGeoStreamProcessorService
    {
        private const int MaxEventCount = 10000;
        private const double MinGridSizeDegrees = 1e-6;
        private readonly ILogger<GeoStreamProcessorService> _logger;

        public GeoStreamProcessorService(ILogger<GeoStreamProcessorService> logger)
        {
            _logger = logger;
            GeoStreamNativeBridge.LogAvailability(_logger);
        }

        public Task<GeoStreamBatchResultDto> ProcessBatchAsync(
            GeoStreamBatchRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.Events is null || request.Events.Count == 0)
                throw new ArgumentException("At least one telemetry event is required.", nameof(request));
            if (request.Events.Count > MaxEventCount)
                throw new ArgumentException($"Telemetry batches are limited to {MaxEventCount} events.", nameof(request));
            if (request.GridSizeDegrees <= 0 || !double.IsFinite(request.GridSizeDegrees))
                throw new ArgumentException("Grid size must be a finite value greater than zero.", nameof(request));
            if (request.AnomalySpeedThresholdMetersPerSecond < 0 || !double.IsFinite(request.AnomalySpeedThresholdMetersPerSecond))
                throw new ArgumentException("Anomaly speed threshold must be a finite non-negative value.", nameof(request));
            if (request.GridSizeDegrees < MinGridSizeDegrees)
                throw new ArgumentException("Grid size must be at least 0.000001 degrees.", nameof(request));

            // Non-finite speeds are rejected outright. Events with a non-finite position are
            // counted as invalid (like out-of-range ones); the native kernel only range-checks
            // them, so a batch containing one is processed on the managed path.
            var hasNonFinitePosition = ValidateEvents(request);

            cancellationToken.ThrowIfCancellationRequested();

            if (!hasNonFinitePosition && GeoStreamNativeBridge.TryProcessBatch(request, _logger, out var nativeResult))
                return Task.FromResult(NormalizeResult(nativeResult!));

            return Task.FromResult(NormalizeResult(ProcessManaged(request, cancellationToken)));
        }

        // Throws on a non-finite speed; returns whether any event has a non-finite position.
        private static bool ValidateEvents(GeoStreamBatchRequestDto request)
        {
            var hasNonFinitePosition = false;
            foreach (var telemetryEvent in request.Events)
            {
                if (!double.IsFinite(telemetryEvent.SpeedMetersPerSecond))
                    throw new ArgumentException("Event speed must be a finite value.", nameof(request));
                if (!double.IsFinite(telemetryEvent.Latitude) || !double.IsFinite(telemetryEvent.Longitude))
                    hasNonFinitePosition = true;
            }
            return hasNonFinitePosition;
        }

        private static GeoStreamBatchResultDto ProcessManaged(GeoStreamBatchRequestDto request, CancellationToken cancellationToken)
        {
            var cells = new Dictionary<(int CellX, int CellY), (int Count, double SpeedSum, double MaxSpeed, int AnomalyCount)>();
            var validCount = 0;
            var anomalyCount = 0;

            foreach (var telemetryEvent in request.Events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!double.IsFinite(telemetryEvent.Latitude) || !double.IsFinite(telemetryEvent.Longitude))
                    continue;
                if (telemetryEvent.Latitude < -90 || telemetryEvent.Latitude > 90 || telemetryEvent.Longitude < -180 || telemetryEvent.Longitude > 180)
                    continue;

                validCount++;
                var cellX = (int)Math.Floor((telemetryEvent.Longitude + 180.0) / request.GridSizeDegrees);
                var cellY = (int)Math.Floor((telemetryEvent.Latitude + 90.0) / request.GridSizeDegrees);
                cells.TryGetValue((cellX, cellY), out var accumulator);
                var eventIsAnomaly = telemetryEvent.SpeedMetersPerSecond > request.AnomalySpeedThresholdMetersPerSecond;
                cells[(cellX, cellY)] = (
                    accumulator.Count + 1,
                    accumulator.SpeedSum + telemetryEvent.SpeedMetersPerSecond,
                    Math.Max(accumulator.MaxSpeed, telemetryEvent.SpeedMetersPerSecond),
                    accumulator.AnomalyCount + (eventIsAnomaly ? 1 : 0));

                if (eventIsAnomaly)
                    anomalyCount++;
            }

            var aggregates = cells.Select(kvp => new GridAggregateDto
            {
                CellX = kvp.Key.CellX,
                CellY = kvp.Key.CellY,
                Count = kvp.Value.Count,
                AverageSpeedMetersPerSecond = kvp.Value.Count > 0 ? kvp.Value.SpeedSum / kvp.Value.Count : 0,
                MaxSpeedMetersPerSecond = kvp.Value.MaxSpeed,
                AnomalyCount = kvp.Value.AnomalyCount,
                CenterLatitude = (kvp.Key.CellY + 0.5) * request.GridSizeDegrees - 90.0,
                CenterLongitude = (kvp.Key.CellX + 0.5) * request.GridSizeDegrees - 180.0
            }).ToList();

            return new GeoStreamBatchResultDto
            {
                TotalEvents = request.Events.Count,
                ValidEvents = validCount,
                InvalidEvents = request.Events.Count - validCount,
                AnomalyCount = anomalyCount,
                NativeAccelerated = false,
                Aggregates = aggregates
            };
        }

        private static GeoStreamBatchResultDto NormalizeResult(GeoStreamBatchResultDto result)
        {
            result.Aggregates = result.Aggregates
                .Select(a => new GridAggregateDto
                {
                    CellX = a.CellX,
                    CellY = a.CellY,
                    Count = a.Count,
                    AverageSpeedMetersPerSecond = Math.Round(a.AverageSpeedMetersPerSecond, 2),
                    MaxSpeedMetersPerSecond = Math.Round(a.MaxSpeedMetersPerSecond, 2),
                    AnomalyCount = a.AnomalyCount,
                    CenterLatitude = Math.Round(a.CenterLatitude, 6),
                    CenterLongitude = Math.Round(a.CenterLongitude, 6)
                })
                .OrderBy(a => a.CellY)
                .ThenBy(a => a.CellX)
                .ToList();

            return result;
        }
    }
}
