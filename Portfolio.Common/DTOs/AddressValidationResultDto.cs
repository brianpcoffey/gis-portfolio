using Portfolio.Common.Enums;

namespace Portfolio.Common.DTOs
{
    /// <summary>
    /// Result of validating and standardizing an address, with match score and confidence.
    /// </summary>
    public class AddressValidationResultDto
    {
        /// <summary>The submitted address broken into its parsed components.</summary>
        public AddressParsedDto Parsed { get; set; } = new();

        /// <summary>Full formatted address that the validator matched.</summary>
        public string MatchedAddress { get; set; } = string.Empty;

        /// <summary>Match quality score, typically 0-100 where higher is a better match.</summary>
        public double Score { get; set; }

        /// <summary>Bucketed confidence level derived from the match score.</summary>
        public ConfidenceTier ConfidenceTier { get; set; }
    }
}
