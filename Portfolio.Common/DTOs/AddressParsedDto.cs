namespace Portfolio.Common.DTOs
{
    public class AddressParsedDto
    {
        public string HouseNumber { get; set; } = string.Empty;
        public string StreetName { get; set; } = string.Empty;
        public string StreetSuffix { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string StandardizedAddress { get; set; } = string.Empty;
        public double ParseConfidence { get; set; }
    }
}
