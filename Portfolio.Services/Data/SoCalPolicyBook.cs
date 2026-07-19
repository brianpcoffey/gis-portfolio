using Portfolio.Common.DTOs;

namespace Portfolio.Services.Data
{
    /// <summary>
    /// A deterministic synthetic property-insurance book across the San Bernardino and
    /// Riverside county foothills — genuine wildland-urban interface territory — paired
    /// with a stochastic wildfire event catalog.
    ///
    /// Generated from a fixed-seed LCG so the book is byte-identical on every run. No
    /// database, no persistence: this is a stateless demo dataset in the same shape as
    /// <c>RedlandsRoadNetwork</c>.
    /// </summary>
    internal static class SoCalPolicyBook
    {
        public const string BookName = "San Bernardino / Riverside Foothills — Wildfire Book";

        // Total catalog frequency, in events per year, across the whole region.
        private const double TargetAnnualRate = 1.5;

        private sealed class Community
        {
            public required string Name { get; init; }
            public required double Latitude { get; init; }
            public required double Longitude { get; init; }
            public required double SpreadDegrees { get; init; }
            public required int Count { get; init; }
            public required (double Min, double Max) Hazard { get; init; }
            public required (double Min, double Max) Tiv { get; init; }
        }

        private static readonly Community[] Communities =
        [
            // Canyon community, heavy chaparral fuel, steep terrain — the worst exposure.
            new() { Name = "Forest Falls",        Latitude = 34.0894, Longitude = -116.9200, SpreadDegrees = 0.018, Count = 140, Hazard = (0.75, 0.95), Tiv = (400_000, 1_200_000) },
            // Foothill orchard country, moderate fuel load.
            new() { Name = "Oak Glen",            Latitude = 34.0533, Longitude = -116.9500, SpreadDegrees = 0.022, Count = 120, Hazard = (0.55, 0.75), Tiv = (500_000, 1_500_000) },
            // Ridgeline WUI — homes pushed up against open slope.
            new() { Name = "Yucaipa Ridge",       Latitude = 34.0336, Longitude = -117.0431, SpreadDegrees = 0.026, Count = 150, Hazard = (0.60, 0.85), Tiv = (450_000, 1_100_000) },
            // Irrigated urban edge; defensible space and hydrant coverage.
            new() { Name = "Redlands Heights",    Latitude = 34.0556, Longitude = -117.1825, SpreadDegrees = 0.024, Count = 180, Hazard = (0.30, 0.50), Tiv = (600_000, 2_500_000) },
            // Dense urban core, minimal wildland fuel.
            new() { Name = "San Bernardino Flats", Latitude = 34.1083, Longitude = -117.2898, SpreadDegrees = 0.030, Count = 200, Hazard = (0.10, 0.25), Tiv = (250_000, 700_000) },
            // Rural grass, wind-exposed through the pass.
            new() { Name = "Cherry Valley",       Latitude = 33.9722, Longitude = -116.9711, SpreadDegrees = 0.028, Count = 110, Hazard = (0.45, 0.70), Tiv = (300_000, 900_000) }
        ];

        // Declared after the data arrays above so static-field initialization order is correct.
        public static IReadOnlyList<CatLocationDto> Locations { get; } = BuildLocations();

        public static IReadOnlyList<CatEventDto> Events { get; } = BuildEvents();

        private static List<CatLocationDto> BuildLocations()
        {
            var rng = new Lcg(2_654_435_761u); // fixed seed -> deterministic book
            var locations = new List<CatLocationDto>(Communities.Sum(c => c.Count));
            var id = 1;

            foreach (var community in Communities)
            {
                for (var i = 0; i < community.Count; i++)
                {
                    // Offset within the community footprint, in polar form so density
                    // falls off toward the edges rather than filling a square.
                    var angle = rng.NextDouble() * 2.0 * Math.PI;
                    var radial = Math.Sqrt(rng.NextDouble()); // sqrt keeps area density even
                    var latitude = community.Latitude + Math.Sin(angle) * radial * community.SpreadDegrees;
                    var longitude = community.Longitude + Math.Cos(angle) * radial * community.SpreadDegrees * 1.2;

                    // Hazard is terrain-driven, not drawn independently: locations toward the
                    // community edge sit closer to open fuel, and a slope proxy supplies the
                    // rest. This makes the exposure map read as a gradient rather than noise.
                    var slopeProxy = rng.NextDouble();
                    var hazardMix = Math.Clamp(0.55 * radial + 0.45 * slopeProxy, 0.0, 1.0);
                    var siteHazard = Lerp(community.Hazard.Min, community.Hazard.Max, hazardMix);

                    // Hillside lots carry a premium, so TIV correlates mildly with position.
                    var tivMix = Math.Clamp(0.35 * radial + 0.65 * rng.NextDouble(), 0.0, 1.0);
                    var insuredValue = Math.Round(Lerp(community.Tiv.Min, community.Tiv.Max, tivMix) / 1000.0) * 1000.0;

                    locations.Add(new CatLocationDto
                    {
                        Id = id,
                        Name = $"{community.Name} #{i + 1:D3}",
                        Community = community.Name,
                        Latitude = Math.Round(latitude, 6),
                        Longitude = Math.Round(longitude, 6),
                        InsuredValue = insuredValue,
                        SiteHazard = Math.Round(siteHazard, 4),
                        // Wildfire deductibles are a percentage of TIV, not a flat dollar amount.
                        DeductibleRate = Math.Round(0.02 + rng.NextDouble() * 0.03, 4),
                        LimitRate = Math.Round(0.80 + rng.NextDouble() * 0.20, 4)
                    });

                    id++;
                }
            }

            return locations;
        }

        private static List<CatEventDto> BuildEvents()
        {
            const int eventCount = 5000;
            var rng = new Lcg(1_013_904_223u); // distinct seed from the book
            var events = new List<CatEventDto>(eventCount);

            // Ignition is biased toward the high-fuel communities, so build a cumulative
            // weight table over mean community hazard.
            var weights = Communities.Select(c => (c.Hazard.Min + c.Hazard.Max) * 0.5).ToArray();
            var cumulative = new double[weights.Length];
            var running = 0.0;
            for (var i = 0; i < weights.Length; i++)
            {
                running += weights[i];
                cumulative[i] = running;
            }

            var rawRates = new double[eventCount];

            for (var e = 0; e < eventCount; e++)
            {
                var pick = rng.NextDouble() * running;
                var index = 0;
                while (index < cumulative.Length - 1 && pick > cumulative[index])
                    index++;
                var origin = Communities[index];

                // Epicenters wander well beyond the community footprint — fires start in
                // the open country between towns.
                var angle = rng.NextDouble() * 2.0 * Math.PI;
                var radial = Math.Sqrt(rng.NextDouble());
                var latitude = origin.Latitude + Math.Sin(angle) * radial * 0.09;
                var longitude = origin.Longitude + Math.Cos(angle) * radial * 0.11;

                // Footprint skewed small: cubing a uniform draw makes large fires rare.
                var sizeDraw = rng.NextDouble();
                var radiusKm = 2.0 + Math.Pow(sizeDraw, 3.0) * 23.0;
                var intensity = 0.30 + rng.NextDouble() * 0.70;

                // Severity and frequency trade off — big, hot fires are the rare tail.
                var severity = (radiusKm / 25.0) * intensity;
                rawRates[e] = 1.0 / (1.0 + severity * 40.0);

                events.Add(new CatEventDto
                {
                    Id = e + 1,
                    Latitude = Math.Round(latitude, 6),
                    Longitude = Math.Round(longitude, 6),
                    Intensity = Math.Round(intensity, 4),
                    RadiusKm = Math.Round(radiusKm, 3),
                    AnnualRate = 0 // filled in below once the catalog is normalized
                });
            }

            // Normalize so the whole catalog carries the target regional frequency.
            var rateSum = rawRates.Sum();
            var scale = TargetAnnualRate / rateSum;
            for (var e = 0; e < eventCount; e++)
                events[e].AnnualRate = rawRates[e] * scale;

            return events;
        }

        private static double Lerp(double min, double max, double t) => min + (max - min) * t;

        private sealed class Lcg
        {
            private uint _state;

            public Lcg(uint seed) => _state = seed == 0 ? 1u : seed;

            private uint NextUInt()
            {
                // Numerical Recipes LCG constants.
                _state = unchecked(_state * 1_664_525u + 1_013_904_223u);
                return _state;
            }

            public double NextDouble() => NextUInt() / 4_294_967_296.0;
        }
    }
}
