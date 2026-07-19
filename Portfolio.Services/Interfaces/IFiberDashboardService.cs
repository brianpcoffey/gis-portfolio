using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces;

/// <summary>
/// Aggregates fiber plant operations data into a single dashboard summary.
/// </summary>
public interface IFiberDashboardService
{
    /// <summary>Returns the aggregated fiber operations dashboard.</summary>
    Task<FiberDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
}
