namespace Portfolio.Services.Interfaces;

public interface IFiberSeedService
{
    Task<bool> UserHasSeedDataAsync(CancellationToken cancellationToken = default);
    Task SeedForUserAsync(CancellationToken cancellationToken = default);
}
