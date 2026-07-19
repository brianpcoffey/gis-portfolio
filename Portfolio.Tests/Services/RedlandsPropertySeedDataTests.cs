using Portfolio.Repositories.Seed;

namespace Portfolio.Tests.Services
{
    public class RedlandsPropertySeedDataTests
    {
        [Fact]
        public void All_ProducesADenseCuratedDataset()
        {
            var all = RedlandsPropertySeedData.All;
            Assert.True(all.Count >= 40, $"Expected a rich dataset but got {all.Count} properties.");
        }

        [Fact]
        public void All_HasUniqueSequentialIds()
        {
            var ids = RedlandsPropertySeedData.All.Select(p => p.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
            Assert.Equal(Enumerable.Range(1, ids.Count), ids.OrderBy(i => i));
        }

        [Fact]
        public void All_IsDeterministicAcrossCalls()
        {
            // The dataset is materialized once and must be stable so the migration
            // that ships it produces identical rows on every build.
            var first = RedlandsPropertySeedData.All;
            var second = RedlandsPropertySeedData.All;
            Assert.Same(first, second);
        }

        [Fact]
        public void All_AreRedlandsPropertiesWithPlausibleGeographyAndPricing()
        {
            foreach (var p in RedlandsPropertySeedData.All)
            {
                Assert.Equal("Redlands", p.City);
                Assert.Equal("CA", p.State);
                Assert.Contains(p.ZipCode, new[] { "92373", "92374" });

                // Within the Redlands bounding box.
                Assert.InRange(p.Latitude, 34.01, 34.10);
                Assert.InRange(p.Longitude, -117.25, -117.13);

                // Realistic listing values.
                Assert.InRange(p.Price, 380_000m, 1_800_000m);
                Assert.InRange(p.Bedrooms, 1, 6);
                Assert.InRange(p.Bathrooms, 1, 6);
                Assert.InRange(p.LotSqft, 700, 4500);
                Assert.InRange(p.YearBuilt, 1890, 2025);
                Assert.True(p.PropertyTax > 0);
                Assert.False(string.IsNullOrWhiteSpace(p.Street));
                Assert.False(string.IsNullOrWhiteSpace(p.PropertyType));

                // 0–100 score fields stay in range.
                foreach (var score in new[] { p.SchoolRating, p.CrimeScore, p.Walkability, p.TransitAccess, p.AmenitiesScore, p.FloodRisk, p.NoiseLevel, p.FutureAppreciation, p.ResalePotential })
                    Assert.InRange(score, 0, 100);
            }
        }

        [Fact]
        public void All_CoversMultiplePriceTiersAndPropertyTypes()
        {
            var all = RedlandsPropertySeedData.All;
            Assert.Contains(all, p => p.Price < 550_000m);       // entry tier
            Assert.Contains(all, p => p.Price > 1_000_000m);     // luxury tier
            Assert.Contains(all, p => p.PropertyType == "Single Family");
            Assert.Contains(all, p => p.HasPool);
            Assert.Contains(all, p => p.GarageSpaces > 0);
        }
    }
}
