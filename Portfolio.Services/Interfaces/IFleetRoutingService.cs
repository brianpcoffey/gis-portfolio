using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    /// <summary>
    /// Solves the capacitated vehicle routing problem with time windows (CVRPTW) over the
    /// Redlands road network: which truck serves which stops, in what order, and when.
    /// </summary>
    public interface IFleetRoutingService
    {
        /// <summary>Returns a named demo delivery scenario: depot, stops, and fleet parameters.</summary>
        Task<FleetScenarioDto> GetScenarioAsync(string presetName, CancellationToken cancellationToken = default);

        /// <summary>Assigns stops to vehicles and sequences each route, respecting capacity and every time window.</summary>
        Task<OptimizeResultDto> OptimizeAsync(OptimizeRequestDto request, CancellationToken cancellationToken = default);
    }
}
