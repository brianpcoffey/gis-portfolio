namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// Request to process a batch of telemetry events into grid aggregates and anomaly flags.
    /// </summary>
    public class GeoStreamBatchRequestDto
    {
        /// <summary>Telemetry events to ingest and aggregate.</summary>
        public List<TelemetryEventDto> Events { get; set; } = [];

        /// <summary>Size of each aggregation grid cell, in decimal degrees.</summary>
        public double GridSizeDegrees { get; set; } = 0.01;

        /// <summary>Speed above which an event is flagged as an anomaly, in meters per second.</summary>
        public double AnomalySpeedThresholdMetersPerSecond { get; set; } = 45;
    }

    /// <summary>
    /// A single telemetry reading for a moving entity at a point in time.
    /// </summary>
    public class TelemetryEventDto
    {
        /// <summary>Identifier of the entity that produced the reading.</summary>
        public int EntityId { get; set; }

        /// <summary>UTC timestamp of the reading.</summary>
        public DateTime TimestampUtc { get; set; }

        /// <summary>WGS84 latitude of the entity at the time of the reading.</summary>
        public double Latitude { get; set; }

        /// <summary>WGS84 longitude of the entity at the time of the reading.</summary>
        public double Longitude { get; set; }

        /// <summary>Instantaneous speed of the entity, in meters per second.</summary>
        public double SpeedMetersPerSecond { get; set; }

        /// <summary>Heading of the entity in degrees clockwise from north.</summary>
        public double HeadingDegrees { get; set; }
    }

    /// <summary>
    /// Result of a batch telemetry run: event counts and per-cell aggregates.
    /// </summary>
    public class GeoStreamBatchResultDto
    {
        /// <summary>Total number of events submitted in the batch.</summary>
        public int TotalEvents { get; set; }

        /// <summary>Number of events that passed validation and were aggregated.</summary>
        public int ValidEvents { get; set; }

        /// <summary>Number of events rejected as invalid.</summary>
        public int InvalidEvents { get; set; }

        /// <summary>Number of events flagged as anomalies across the batch.</summary>
        public int AnomalyCount { get; set; }

        /// <summary>True when the computation was performed by the native-accelerated engine.</summary>
        public bool NativeAccelerated { get; set; }

        /// <summary>Per grid-cell aggregates computed from the events.</summary>
        public List<GridAggregateDto> Aggregates { get; set; } = [];
    }

    /// <summary>
    /// Aggregated telemetry statistics for a single grid cell.
    /// </summary>
    public class GridAggregateDto
    {
        /// <summary>Column index of the grid cell.</summary>
        public int CellX { get; set; }

        /// <summary>Row index of the grid cell.</summary>
        public int CellY { get; set; }

        /// <summary>Number of events that fell within the cell.</summary>
        public int Count { get; set; }

        /// <summary>Average speed of events in the cell, in meters per second.</summary>
        public double AverageSpeedMetersPerSecond { get; set; }

        /// <summary>Maximum speed observed in the cell, in meters per second.</summary>
        public double MaxSpeedMetersPerSecond { get; set; }

        /// <summary>Number of anomaly events within the cell.</summary>
        public int AnomalyCount { get; set; }

        /// <summary>WGS84 latitude of the cell center.</summary>
        public double CenterLatitude { get; set; }

        /// <summary>WGS84 longitude of the cell center.</summary>
        public double CenterLongitude { get; set; }
    }
}
