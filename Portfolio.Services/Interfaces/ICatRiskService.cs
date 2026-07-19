using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    /// <summary>
    /// Catastrophe risk analytics: exposure concentration and Monte Carlo loss simulation
    /// over a book of insured locations.
    /// </summary>
    public interface ICatRiskService
    {
        /// <summary>Returns the demo policy book and its paired stochastic event catalog.</summary>
        Task<PolicyBookDto> GetPolicyBookAsync(CancellationToken cancellationToken = default);

        /// <summary>Computes ring accumulation (TIV within a radius of each location) and flags concentration breaches.</summary>
        Task<AccumulationResultDto> ComputeAccumulationAsync(
            AccumulationRequestDto request,
            CancellationToken cancellationToken = default);

        /// <summary>Simulates the event catalog against the book, producing AAL, PML, and the OEP curve.</summary>
        Task<SimulationResultDto> SimulateAsync(
            SimulationRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
