using Portfolio.Common.Enums;

namespace Portfolio.Common.DTOs
{
    public class AddressValidationResultDto
    {
        public AddressParsedDto Parsed { get; set; } = new();
        public string MatchedAddress { get; set; } = string.Empty;
        public double Score { get; set; }
        public ConfidenceTier ConfidenceTier { get; set; }
    }
}
