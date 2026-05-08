using System.Runtime.InteropServices;

namespace Portfolio.Services.Native
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct TelemetryEventNative
    {
        public long TimestampUnixMs;
        public int EntityId;
        public double Latitude;
        public double Longitude;
        public double SpeedMetersPerSecond;
        public double HeadingDegrees;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct GeoStreamOptionsNative
    {
        public double GridSizeDegrees;
        public double AnomalySpeedThresholdMetersPerSecond;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct GridAggregateNative
    {
        public int CellX;
        public int CellY;
        public int Count;
        public double AverageSpeedMetersPerSecond;
        public double MaxSpeedMetersPerSecond;
        public int AnomalyCount;
        public double CenterLatitude;
        public double CenterLongitude;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct GeoStreamResultNative
    {
        public int TotalEvents;
        public int ValidEvents;
        public int InvalidEvents;
        public int AnomalyCount;
        public int AggregateCount;
    }
}
