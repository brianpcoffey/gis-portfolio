namespace Portfolio.Common.Enums
{
    /// <summary>The ArcGIS pipeline family whose circuit breaker an action's <c>[ArcGisUnavailable]</c> reports on.</summary>
    public enum ArcGisUpstream
    {
        /// <summary>Batch, reverse and address geocoding.</summary>
        Geocoding,

        /// <summary>Feature-service queries.</summary>
        Features
    }
}
