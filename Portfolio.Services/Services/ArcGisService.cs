using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using System.Net.Http.Json;
using System.Text.Json;

namespace Portfolio.Services.Services
{
    public class ArcGisService(HttpClient httpClient, ILogger<ArcGisService> logger) : IArcGisService
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly ILogger<ArcGisService> _logger = logger;

        public async Task<List<FeatureDto>> QueryFeaturesAsync(string layerId, string? bbox = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(layerId, nameof(layerId));

            var url = $"https://sampleserver6.arcgisonline.com/arcgis/rest/services/Census/MapServer/{layerId}/query?where=1=1&f=json&outFields=*";
            if (!string.IsNullOrEmpty(bbox))
                url += $"&geometry={bbox}&geometryType=esriGeometryEnvelope&inSR=4326&spatialRel=esriSpatialRelIntersects";

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
            var features = await QueryFeaturesAsync(layerId, null, cancellationToken);
            return features.Find(f => f.FeatureId == featureId);
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