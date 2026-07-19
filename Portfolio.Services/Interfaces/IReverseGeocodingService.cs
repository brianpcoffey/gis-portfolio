using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    /// <summary>
    /// Resolves WGS84 coordinates into structured place data via reverse geocoding.
    /// </summary>
    public interface IReverseGeocodingService
    {
        /// <summary>Reverse-geocodes a WGS84 coordinate and returns structured place data.</summary>
        /// <exception cref="System.ArgumentException">Thrown for out-of-range coordinates.</exception>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">Thrown when ArcGIS returns no result.</exception>
        Task<ReverseGeocodingResultDto> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
    }
}
