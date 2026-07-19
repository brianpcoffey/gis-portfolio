namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// A free-form address broken into its individual components after parsing.
    /// </summary>
    public class AddressParsedDto
    {
        /// <summary>Street/house number portion of the address (e.g. "123").</summary>
        public string HouseNumber { get; set; } = string.Empty;

        /// <summary>Street name without the suffix (e.g. "Main").</summary>
        public string StreetName { get; set; } = string.Empty;

        /// <summary>Street type suffix (e.g. "St", "Ave", "Blvd").</summary>
        public string StreetSuffix { get; set; } = string.Empty;

        /// <summary>Secondary unit designator such as apartment or suite (e.g. "Apt 4B").</summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>City or locality name.</summary>
        public string City { get; set; } = string.Empty;

        /// <summary>State or region, typically the two-letter abbreviation.</summary>
        public string State { get; set; } = string.Empty;

        /// <summary>Postal (ZIP) code.</summary>
        public string PostalCode { get; set; } = string.Empty;

        /// <summary>Full address reassembled into a normalized, standardized form.</summary>
        public string StandardizedAddress { get; set; } = string.Empty;

        /// <summary>Confidence of the parse, from 0.0 (low) to 1.0 (high).</summary>
        public double ParseConfidence { get; set; }
    }
}
