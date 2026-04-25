using System.Text.Json.Serialization;

namespace Portfolio.Common.ArcGis
{
    public class ArcGisReverseGeocodeResponse
    {
        [JsonPropertyName("address")]
        public ArcGisReverseAddress? Address { get; set; }
    }
}
