namespace Portfolio.Common.DTOs
{
    public class ReverseGeocodingResultDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string MatchedAddress { get; set; } = string.Empty;
        public string HouseNumber { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string CountryCode { get; set; } = string.Empty;
        public string LocationType { get; set; } = string.Empty;
    }
}
