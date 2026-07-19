using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portfolio.Common.ArcGis;
using Portfolio.Common.Configuration;
using Portfolio.Common.DTOs;
using Portfolio.Common.Serialization;
using Portfolio.Services.Interfaces;
using System.Net.Http.Json;
using System.Text.Json;

namespace Portfolio.Services.Services
{
    public class ReverseGeocodingService : IReverseGeocodingService
    {
        private readonly HttpClient _httpClient;
        private readonly IDistributedCache _cache;
        private readonly ILogger<ReverseGeocodingService> _logger;
        private readonly double _gridResolution;
        private readonly int _cacheSlidingExpirationMinutes;

        private const string ReverseGeocodeUrl =
            "https://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer/reverseGeocode";

        public ReverseGeocodingService(
            HttpClient httpClient,
            IDistributedCache cache,
            ILogger<ReverseGeocodingService> logger,
            IOptions<ReverseGeocodingOptions> options)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
            var opts = options.Value;
            _gridResolution = opts.GridResolutionDegrees;
            _cacheSlidingExpirationMinutes = opts.CacheSlidingExpirationMinutes;
        }

        // Validates coordinates, snaps to grid, checks cache, then calls ArcGIS if needed.
        // Throws ArgumentException for invalid coordinates, KeyNotFoundException when no result is found.
        public async Task<ReverseGeocodingResultDto> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
        {
            if (latitude < -90.0 || latitude > 90.0)
                throw new ArgumentException("Latitude must be between -90 and 90", nameof(latitude));

            if (longitude < -180.0 || longitude > 180.0)
                throw new ArgumentException("Longitude must be between -180 and 180", nameof(longitude));

            // Snap to grid to enable spatial cache hits for nearby coordinates.
            var snappedLat = SnapToGrid(latitude, _gridResolution);
            var snappedLng = SnapToGrid(longitude, _gridResolution);
            var cacheKey = $"reversegeocode:{snappedLat:R}:{snappedLng:R}";

            var cachedBytes = await _cache.GetAsync(cacheKey, cancellationToken);
            if (cachedBytes is not null)
            {
                var cached = JsonSerializer.Deserialize<ReverseGeocodingResultDto>(
                    cachedBytes, PortfolioJsonOptions.Default);
                if (cached is not null)
                    return cached;
            }

            var result = await FetchReverseGeocodeAsync(snappedLat, snappedLng, cancellationToken);

            await _cache.SetAsync(cacheKey,
                JsonSerializer.SerializeToUtf8Bytes(result, PortfolioJsonOptions.Default),
                new DistributedCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(_cacheSlidingExpirationMinutes)
                }, cancellationToken);

            return result;
        }

        // Makes the HTTP call to the ArcGIS World reverse geocoding REST API.
        // Throws KeyNotFoundException when ArcGIS returns no address result.
        private async Task<ReverseGeocodingResultDto> FetchReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken)
        {
            // Encode all caller-supplied values to prevent URL injection.
            var location = Uri.EscapeDataString($"{longitude},{latitude}");
            var url = $"{ReverseGeocodeUrl}?location={location}&f=json&returnIntersection=false";

            ArcGisReverseGeocodeResponse? parsed;
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                parsed = await response.Content.ReadFromJsonAsync<ArcGisReverseGeocodeResponse>(cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error during reverse geocode for ({Lat}, {Lng})", latitude, longitude);
                throw new InvalidOperationException(
                    $"Upstream geocoding service failed for coordinates ({latitude}, {longitude}).", ex);
            }

            if (parsed?.Address is null)
            {
                _logger.LogWarning("No reverse geocode result from ArcGIS for ({Lat}, {Lng})", latitude, longitude);
                throw new KeyNotFoundException($"No result found for coordinates ({latitude}, {longitude}).");
            }

            return MapToDto(latitude, longitude, parsed);
        }

        private static ReverseGeocodingResultDto MapToDto(double latitude, double longitude, ArcGisReverseGeocodeResponse response)
        {
            var addr = response.Address!;
            return new ReverseGeocodingResultDto
            {
                Latitude = latitude,
                Longitude = longitude,
                MatchedAddress = addr.LongLabel ?? addr.MatchAddr ?? string.Empty,
                HouseNumber = addr.AddNum ?? string.Empty,
                Street = !string.IsNullOrWhiteSpace(addr.StAddr)
                    ? addr.StAddr
                    : addr.Address ?? string.Empty,
                City = addr.City ?? string.Empty,
                Region = addr.Region ?? string.Empty,
                PostalCode = addr.Postal ?? string.Empty,
                CountryCode = addr.CountryCode ?? string.Empty,
                LocationType = addr.AddrType ?? string.Empty
            };
        }

        // Snaps a coordinate value to the nearest multiple of the grid resolution.
        private static double SnapToGrid(double value, double resolution)
        {
            return Math.Round(value / resolution) * resolution;
        }
    }
}
