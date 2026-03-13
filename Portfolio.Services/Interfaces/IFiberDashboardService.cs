using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces;

public interface IFiberDashboardService
{
    Task<FiberDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
}
