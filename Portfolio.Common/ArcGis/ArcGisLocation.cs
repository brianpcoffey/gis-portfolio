using System.Text.Json.Serialization;

namespace Portfolio.Common.ArcGis
{
    public class ArcGisLocation
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }
    }
}
