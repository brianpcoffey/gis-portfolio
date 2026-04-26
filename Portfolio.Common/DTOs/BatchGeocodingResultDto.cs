namespace Portfolio.Common.DTOs
{
    public class BatchGeocodingResultDto
    {
        public string OriginalAddress { get; set; } = string.Empty;
        public bool Matched { get; set; }
        public string MatchedAddress { get; set; } = string.Empty;
        public double Score { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
