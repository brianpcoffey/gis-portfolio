namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// Result of reverse geocoding a coordinate into a matched postal address.
    /// </summary>
    public class ReverseGeocodingResultDto
    {
        /// <summary>WGS84 latitude of the queried location.</summary>
        public double Latitude { get; set; }

        /// <summary>WGS84 longitude of the queried location.</summary>
        public double Longitude { get; set; }

        /// <summary>Full formatted address matched to the coordinate.</summary>
        public string MatchedAddress { get; set; } = string.Empty;

        /// <summary>Street/house number of the matched address.</summary>
        public string HouseNumber { get; set; } = string.Empty;

        /// <summary>Street name of the matched address.</summary>
        public string Street { get; set; } = string.Empty;

        /// <summary>City or locality of the matched address.</summary>
        public string City { get; set; } = string.Empty;

        /// <summary>State, province, or region of the matched address.</summary>
        public string Region { get; set; } = string.Empty;

        /// <summary>Postal (ZIP) code of the matched address.</summary>
        public string PostalCode { get; set; } = string.Empty;

        /// <summary>ISO country code of the matched address.</summary>
        public string CountryCode { get; set; } = string.Empty;

        /// <summary>Granularity of the match (e.g. "rooftop", "street", "postal").</summary>
        public string LocationType { get; set; } = string.Empty;
    }
}
