using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using System.Net.Http.Json;
using System.Text.Json;

namespace Portfolio.Services.Services
{
    public class ArcGisService : IArcGisService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ArcGisService> _logger;
        private readonly string _baseUrl;

        public ArcGisService(HttpClient httpClient, ILogger<ArcGisService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _baseUrl = configuration["ArcGis:BaseUrl"]
                ?? throw new InvalidOperationException("ArcGis:BaseUrl configuration is missing.");
        }

        public Task<List<FeatureDto>> QueryFeaturesAsync(string layerId, string? bbox = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(layerId, nameof(layerId));
            return QueryFeaturesCoreAsync(layerId, "1=1", bbox, cancellationToken);
        }

        // Core query shared by the public list query and the single-feature lookup. whereClause is
        // always code-supplied ("1=1" or "OBJECTID=<validated int>"), never raw user input, so it
        // cannot inject into the ArcGIS where filter.
        private async Task<List<FeatureDto>> QueryFeaturesCoreAsync(string layerId, string whereClause, string? bbox, CancellationToken cancellationToken)
        {
            // Encode the caller-supplied layerId to prevent URL injection. whereClause is NOT
            // encoded: it is always code-supplied ("1=1" or "OBJECTID=<validated int>"), never user
            // input, and ArcGIS expects the raw `where=1=1` form (percent-encoding the '=' can break
            // servers that don't decode it inside the where value).
            var encodedLayerId = Uri.EscapeDataString(layerId);
            var url = $"{_baseUrl}/{encodedLayerId}/query?where={whereClause}&f=json&outFields=*";
            if (!string.IsNullOrEmpty(bbox))
            {
                // bbox is caller-supplied; encode it to prevent URL injection.
                var encodedBbox = Uri.EscapeDataString(bbox);
                url += $"&geometry={encodedBbox}&geometryType=esriGeometryEnvelope&inSR=4326&spatialRel=esriSpatialRelIntersects";
            }

            try
            {
                _logger.LogInformation("Querying ArcGIS features: {Url}", url);
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                var arcGisResponse = await response.Content.ReadFromJsonAsync<ArcGisQueryResponse>(cancellationToken: cancellationToken);
                if (arcGisResponse?.Features is null or { Count: 0 })
                    return [];

                return [.. arcGisResponse.Features.Select(f => new FeatureDto
                {
                    LayerId = layerId,
                    FeatureId = f.Attributes.TryGetValue("OBJECTID", out var id) ? id?.ToString() ?? "" : "",
                    Name = GetFeatureName(f.Attributes),
                    GeometryJson = JsonSerializer.Serialize(f.Geometry),
                    Attributes = f.Attributes // Include all attributes from ArcGIS
                })];
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("ArcGIS feature query was canceled.");
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error querying ArcGIS features");
                return [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying ArcGIS features");
                return [];
            }
        }

        public async Task<FeatureDto?> GetFeatureAsync(string layerId, string featureId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(layerId, nameof(layerId));

            // featureId is the ArcGIS OBJECTID (an integer). Validate it so it can be placed in the
            // where clause safely, and query just that one feature rather than the entire layer.
            if (!long.TryParse(featureId, out var objectId))
                return null;

            var features = await QueryFeaturesCoreAsync(layerId, $"OBJECTID={objectId}", null, cancellationToken);
            return features.FirstOrDefault();
        }

        private static string GetFeatureName(Dictionary<string, object?> attributes)
        {
            if (attributes.TryGetValue("STATE_NAME", out var stateName) && stateName is not null)
                return stateName.ToString() ?? "";

            if (attributes.TryGetValue("NAME", out var name) && name is not null)
                return name.ToString() ?? "";

            if (attributes.TryGetValue("OBJECTID", out var objectId) && objectId is not null)
                return $"Feature {objectId}";

            return "Unknown Feature";
        }

        private sealed class ArcGisQueryResponse
        {
            public List<ArcGisFeature> Features { get; set; } = [];
        }

        private sealed class ArcGisFeature
        {
            public Dictionary<string, object?> Attributes { get; set; } = [];
            public object? Geometry { get; set; }
        }
    }
}