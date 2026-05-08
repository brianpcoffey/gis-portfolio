namespace Portfolio.Common.DTOs
{
    public class GeoStreamBatchRequestDto
    {
        public List<TelemetryEventDto> Events { get; set; } = [];
        public double GridSizeDegrees { get; set; } = 0.01;
        public double AnomalySpeedThresholdMetersPerSecond { get; set; } = 45;
    }

    public class TelemetryEventDto
    {
        public int EntityId { get; set; }
        public DateTime TimestampUtc { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double SpeedMetersPerSecond { get; set; }
        public double HeadingDegrees { get; set; }
    }

    public class GeoStreamBatchResultDto
    {
        public int TotalEvents { get; set; }
        public int ValidEvents { get; set; }
        public int InvalidEvents { get; set; }
        public int AnomalyCount { get; set; }
        public bool NativeAccelerated { get; set; }
        public List<GridAggregateDto> Aggregates { get; set; } = [];
    }

    public class GridAggregateDto
    {
        public int CellX { get; set; }
        public int CellY { get; set; }
        public int Count { get; set; }
        public double AverageSpeedMetersPerSecond { get; set; }
        public double MaxSpeedMetersPerSecond { get; set; }
        public int AnomalyCount { get; set; }
        public double CenterLatitude { get; set; }
        public double CenterLongitude { get; set; }
    }
}
