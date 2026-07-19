using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;

namespace Portfolio.Benchmarks;

/// <summary>
/// A fixed-seed, in-memory <see cref="IPropertyRepository"/>.
///
/// <para>
/// <c>HomeScoringService</c> is the only native-backed service whose public entry point
/// reads from a database. Every other kernel has a deterministic in-repo dataset behind a
/// public API. Rather than skip <c>portfolio_scoring</c>, the harness supplies a generated
/// listing set: the rows are identical on every run and in both child processes, so the
/// scoring loop — the part the kernel actually implements — is measured on identical input.
/// </para>
/// <para>
/// The filter call returns the pre-built list directly. Copying it would put an allocation
/// of the whole set inside the timed region on both paths and dilute the measurement.
/// </para>
/// </summary>
internal sealed class DeterministicPropertyRepository : IPropertyRepository
{
    private readonly List<Property> _properties;

    public DeterministicPropertyRepository(int count, uint seed)
    {
        _properties = new List<Property>(count);
        var state = seed == 0 ? 1L : seed;

        double Next()
        {
            state = state * 48271 % 2147483647L;
            return (state - 1) / 2147483646.0;
        }

        int Range(int minInclusive, int maxInclusive)
            => minInclusive + (int)(Next() * (maxInclusive - minInclusive + 1));

        for (var i = 0; i < count; i++)
        {
            var sqft = Range(600, 4_200);
            _properties.Add(new Property
            {
                Id = i + 1,
                BrokeredBy = "Benchmark Realty",
                Status = "active",
                Price = 220_000m + (decimal)(Next() * 1_400_000.0),
                Bedrooms = Range(1, 6),
                Bathrooms = Range(1, 4),
                AcreLot = (decimal)(0.05 + Next() * 0.5),
                LotSqft = sqft,
                PropertyType = "Single Family",
                GarageSpaces = Range(0, 3),
                HasPool = Next() < 0.25,
                Stories = Range(1, 3),
                DaysOnMarket = Range(1, 400),
                Street = $"{1000 + i} Benchmark Way",
                City = "Redlands",
                State = "CA",
                ZipCode = "92373",
                Latitude = 33.95 + Next() * 0.20,
                Longitude = -117.35 + Next() * 0.25,
                HoaFee = (decimal)(Next() * 350.0),
                PropertyTax = 200m + (decimal)(Next() * 900.0),
                Utilities = 120m + (decimal)(Next() * 320.0),
                SchoolRating = Range(1, 10),
                CrimeScore = Range(1, 100),
                Walkability = Range(1, 100),
                TransitAccess = Range(1, 100),
                AmenitiesScore = Range(1, 100),
                CommuteMin = Range(5, 110),
                YearBuilt = Range(1930, 2025),
                LastRenovation = Next() < 0.5 ? Range(1990, 2025) : null,
                RoofCondition = Range(1, 100),
                AcCondition = Range(1, 100),
                PlumbingCondition = Range(1, 100),
                ElectricalCondition = Range(1, 100),
                FloorPlanScore = Range(1, 100),
                FutureAppreciation = Range(1, 100),
                ResalePotential = Range(1, 100),
                FloodRisk = Range(1, 100),
                NoiseLevel = Range(1, 100)
            });
        }
    }

    public Task<List<Property>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_properties);

    public Task<Property?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(_properties.FirstOrDefault(p => p.Id == id));

    public Task<List<Property>> GetFilteredAsync(
        decimal maxPrice,
        int minBedrooms,
        int minBathrooms,
        int minSqft,
        int maxCommuteMin,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_properties);

    public Task<Property> AddAsync(Property property, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The benchmark repository is read-only.");

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The benchmark repository is read-only.");

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(_properties.Exists(p => p.Id == id));

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_properties.Count);
}
