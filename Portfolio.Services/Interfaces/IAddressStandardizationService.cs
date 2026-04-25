using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    public interface IAddressStandardizationService
    {
        // Parses and standardizes a raw freeform address string into structured components.
        // Throws ArgumentException when rawAddress is null or whitespace.
        Task<AddressParsedDto> ParseAsync(string rawAddress, CancellationToken cancellationToken = default);

        // Parses the raw address and validates it against ArcGIS geocoding.
        // Applies fallback to City+State+ZIP if the full address scores below 75.
        // Throws ArgumentException when rawAddress is null or whitespace.
        Task<AddressValidationResultDto> ValidateAsync(string rawAddress, CancellationToken cancellationToken = default);
    }
}
