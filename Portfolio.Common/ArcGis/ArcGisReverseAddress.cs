using System.Text.Json.Serialization;

namespace Portfolio.Common.ArcGis
{
    public class ArcGisReverseAddress
    {
        [JsonPropertyName("LongLabel")]
        public string? LongLabel { get; set; }

        [JsonPropertyName("Match_addr")]
        public string? Match_addr { get; set; }

        [JsonPropertyName("AddNum")]
        public string? AddNum { get; set; }

        [JsonPropertyName("StAddr")]
        public string? StAddr { get; set; }

        [JsonPropertyName("City")]
        public string? City { get; set; }

        [JsonPropertyName("Region")]
        public string? Region { get; set; }

        [JsonPropertyName("Postal")]
        public string? Postal { get; set; }

        [JsonPropertyName("CountryCode")]
        public string? CountryCode { get; set; }

        [JsonPropertyName("Addr_type")]
        public string? Addr_type { get; set; }
    }
}
