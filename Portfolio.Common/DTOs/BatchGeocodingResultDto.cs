namespace Portfolio.Common.DTOs
{
    public class BatchGeocodingResultDto
    {
        public string Id { get; set; } = string.Empty;
        public string InputAddress { get; set; } = string.Empty;
        public string MatchedAddress { get; set; } = string.Empty;
        public double Score { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
