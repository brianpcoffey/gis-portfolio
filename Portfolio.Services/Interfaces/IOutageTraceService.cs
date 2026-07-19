using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    /// <summary>
    /// Outage management over an electric distribution network: fault tracing, fault
    /// isolation, and restoration switching plans.
    /// </summary>
    public interface IOutageTraceService
    {
        /// <summary>Returns the demo distribution network — two feeders and the tie between them.</summary>
        Task<DistributionNetworkDto> GetNetworkAsync(CancellationToken cancellationToken = default);

        /// <summary>Traces a fault: who is de-energized, what feeds them, and what isolates it.</summary>
        Task<TraceResultDto> TraceAsync(
            TraceRequestDto request,
            CancellationToken cancellationToken = default);

        /// <summary>Searches the open ties for the switching plan that restores the most customers.</summary>
        Task<RestoreResultDto> ProposeRestorationAsync(
            RestoreRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
