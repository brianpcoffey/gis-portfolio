using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portfolio.Common.ArcGis;
using Portfolio.Common.Configuration;
using Portfolio.Common.DTOs;
using Portfolio.Common.Serialization;
using Portfolio.Services.Interfaces;

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

        public async Task<ReverseGeocodingResultDto> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
        {
            if (latitude < -90.0 || latitude > 90.0)
                throw new ArgumentException("Latitude must be between -90 and 90", nameof(latitude));

            if (longitude < -180.0 || longitude > 180.0)
                throw new ArgumentException("Longitude must be between -180 and 180", nameof(longitude));

            // NaN passes both range checks above, so reject it here before any outbound call.
            if (!double.IsFinite(latitude) || !double.IsFinite(longitude))
                throw new ArgumentException("Latitude and longitude must be finite numbers", double.IsFinite(latitude) ? nameof(longitude) : nameof(latitude));

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
                // The coordinates stay out of the log (they are a visitor's map click); the exception carries them to the caller.
                _logger.LogError(ex, "HTTP error during reverse geocode");
                throw new InvalidOperationException(
                    $"Upstream geocoding service failed for coordinates ({latitude}, {longitude}).", ex);
            }

            if (parsed?.Address is null)
            {
                _logger.LogWarning("No reverse geocode result from ArcGIS");
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

        private static double SnapToGrid(double value, double resolution)
        {
            return Math.Round(value / resolution) * resolution;
        }
    }
}
