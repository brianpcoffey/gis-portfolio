using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    public interface IReverseGeocodingService
    {
        // Reverse-geocodes a WGS84 coordinate and returns structured place data.
        // Throws ArgumentException for out-of-range coordinates.
        // Throws KeyNotFoundException when ArcGIS returns no result.
        Task<ReverseGeocodingResultDto> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
    }
}
