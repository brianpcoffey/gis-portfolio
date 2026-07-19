using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    /// <summary>
    /// Parses and validates freeform street addresses into standardized structured components.
    /// </summary>
    public interface IAddressStandardizationService
    {
        /// <summary>Parses and standardizes a raw freeform address string into structured components.</summary>
        /// <exception cref="System.ArgumentException">Thrown when <paramref name="rawAddress"/> is null or whitespace.</exception>
        Task<AddressParsedDto> ParseAsync(string rawAddress, CancellationToken cancellationToken = default);

        /// <summary>
        /// Parses the raw address and validates it against ArcGIS geocoding, falling back to
        /// City+State+ZIP if the full address scores below 75.
        /// </summary>
        /// <exception cref="System.ArgumentException">Thrown when <paramref name="rawAddress"/> is null or whitespace.</exception>
        Task<AddressValidationResultDto> ValidateAsync(string rawAddress, CancellationToken cancellationToken = default);
    }
}
