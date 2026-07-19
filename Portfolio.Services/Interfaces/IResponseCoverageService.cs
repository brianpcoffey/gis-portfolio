using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    /// <summary>
    /// Emergency response coverage analytics over the Redlands road network: drive-time
    /// isochrones and p-median station siting measured against NFPA 1710.
    /// </summary>
    public interface IResponseCoverageService
    {
        /// <summary>Returns the demo scenario: clustered call demand, candidate sites, and today's stations.</summary>
        Task<ResponseScenarioDto> GetScenarioAsync(CancellationToken cancellationToken = default);

        /// <summary>Computes drive-time bands over the road network from a single origin node.</summary>
        Task<IsochroneResultDto> ComputeIsochroneAsync(
            IsochroneRequestDto request,
            CancellationToken cancellationToken = default);

        /// <summary>Sites the requested number of stations and reports coverage against the baseline.</summary>
        Task<OptimizeCoverageResultDto> OptimizeAsync(
            OptimizeCoverageRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
