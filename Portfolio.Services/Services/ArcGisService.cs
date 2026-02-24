using Microsoft.Extensions.Logging;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using System.Net.Http.Json;

namespace Portfolio.Services.Services
{
    public class ArcGisService : IArcGisService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ArcGisService> _logger;

        public ArcGisService(HttpClient httpClient, ILogger<ArcGisService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<List<FeatureDto>> QueryFeaturesAsync(string layerId, string? bbox = null)
        {
            // Example: https://sampleserver6.arcgisonline.com/arcgis/rest/services/Census/MapServer/3/query?where=1=1&f=json
            var url = $"https://sampleserver6.arcgisonline.com/arcgis/rest/services/Census/MapServer/{layerId}/query?where=1=1&f=json";
            if (!string.IsNullOrEmpty(bbox))
                url += $"&geometry={bbox}&geometryType=esriGeometryEnvelope&inSR=4326&spatialRel=esriSpatialRelIntersects";

            try
            {
                var response = await _httpClient.GetFromJsonAsync<ArcGisQueryResponse>(url);
                if (response?.features == null) return new List<FeatureDto>();
                return response.features.Select(f => new FeatureDto
                {
                    LayerId = layerId,
                    FeatureId = f.attributes["OBJECTID"].ToString(),
                    Name = f.attributes.ContainsKey("NAME") ? f.attributes["NAME"].ToString() : $"Feature {f.attributes["OBJECTID"]}",
                    GeometryJson = System.Text.Json.JsonSerializer.Serialize(f.geometry)
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying ArcGIS features");
                return new List<FeatureDto>();
            }
        }

        public async Task<FeatureDto?> GetFeatureAsync(string layerId, string featureId)
        {
            var features = await QueryFeaturesAsync(layerId);
            return features.FirstOrDefault(f => f.FeatureId == featureId);
        }

        // Helper class for deserialization
        private class ArcGisQueryResponse
        {
            public List<ArcGisFeature> features { get; set; } = new();
        }
        private class ArcGisFeature
        {
            public Dictionary<string, object> attributes { get; set; } = new();
            public object geometry { get; set; } = new();
        }
    }
}