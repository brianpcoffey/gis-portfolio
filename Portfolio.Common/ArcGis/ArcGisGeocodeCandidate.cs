using System.Text.Json.Serialization;

namespace Portfolio.Common.ArcGis
{
    public class ArcGisGeocodeCandidate
    {
        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("location")]
        public ArcGisLocation? Location { get; set; }
    }
}
