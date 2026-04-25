using System.Text.Json.Serialization;

namespace Portfolio.Common.ArcGis
{
    public class ArcGisGeocodeResponse
    {
        [JsonPropertyName("candidates")]
        public List<ArcGisGeocodeCandidate>? Candidates { get; set; }
    }
}
