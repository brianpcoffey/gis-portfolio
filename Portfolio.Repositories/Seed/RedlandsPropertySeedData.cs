using Portfolio.Common.Models;

namespace Portfolio.Repositories.Seed
{
    /// <summary>
    /// Canonical, curated Redlands, CA home dataset for the Smart Home Finder.
    /// The records are synthetic ("mock Kaggle"-style) but modelled on real Redlands
    /// neighborhoods, streets, and Redfin/Zillow-style listing attributes so the demo
    /// looks and scores realistically. Generation is fully deterministic (fixed seed,
    /// no time or randomness from the environment) so the same dataset is produced on
    /// every build and can be shipped through a migration.
    /// </summary>
    public static class RedlandsPropertySeedData
    {
        private static readonly string[] Brokerages =
        [
            "Redfin", "Zillow Premier Agent", "Coldwell Banker Realty", "Keller Williams Realty",
            "RE/MAX Advantage", "Century 21 Lois Lauer Realty", "Berkshire Hathaway HomeServices",
            "Compass", "eXp Realty", "Windermere Real Estate"
        ];

        private sealed class Neighborhood
        {
            public required string Zip;
            public required double CenterLat;
            public required double CenterLng;
            public required string[] Streets;
            public required (int Min, int Max) PriceK;      // price range in $thousands
            public required (int Min, int Max) School;
            public required (int Min, int Max) Walk;
            public required (int Min, int Max) Transit;
            public required (int Min, int Max) Crime;       // higher = worse
            public required (int Min, int Max) Amenities;
            public required (int Min, int Max) YearBuilt;
            public required (int Min, int Max) Flood;
            public required (int Min, int Max) Noise;
            public required int Count;
            public required string DefaultType;
        }

        // Real Redlands neighborhoods with representative streets and characteristics.
        private static readonly Neighborhood[] Neighborhoods =
        [
            new()
            {
                Zip = "92373", CenterLat = 34.0385, CenterLng = -117.1790,
                Streets = ["Cajon St", "Olive Ave", "Alvarado St", "Buena Vista St", "Cypress Ave", "Highland Ave", "Terracina Blvd", "Chestnut Ave"],
                PriceK = (820, 1650), School = (85, 96), Walk = (55, 78), Transit = (30, 48),
                Crime = (8, 22), Amenities = (72, 90), YearBuilt = (1900, 1955), Flood = (5, 20), Noise = (18, 38),
                Count = 9, DefaultType = "Single Family"
            }, // South Redlands historic district
            new()
            {
                Zip = "92373", CenterLat = 34.0270, CenterLng = -117.1650,
                Streets = ["Sunset Dr N", "Mariposa Dr", "Prospect Dr", "Panorama Dr", "Serpentine Dr", "Crescent Ave", "Reservoir Rd"],
                PriceK = (760, 1450), School = (82, 93), Walk = (28, 46), Transit = (22, 38),
                Crime = (7, 18), Amenities = (55, 72), YearBuilt = (1968, 2004), Flood = (4, 16), Noise = (12, 28),
                Count = 8, DefaultType = "Single Family"
            }, // South Redlands hillside / view homes
            new()
            {
                Zip = "92373", CenterLat = 34.0556, CenterLng = -117.1825,
                Streets = ["University St", "Grove St", "Vine St", "Campus Ave", "Brockton Ave", "Clark St", "Fern Ave", "Center St"],
                PriceK = (500, 815), School = (76, 89), Walk = (66, 88), Transit = (46, 62),
                Crime = (16, 30), Amenities = (76, 92), YearBuilt = (1912, 1978), Flood = (8, 24), Noise = (34, 55),
                Count = 9, DefaultType = "Single Family"
            }, // Downtown / University of Redlands
            new()
            {
                Zip = "92374", CenterLat = 34.0705, CenterLng = -117.1900,
                Streets = ["Lugonia Ave", "Church St", "Texas St", "Pennsylvania Ave", "Nevada St", "Dearborn St", "Kansas St", "Orange St"],
                PriceK = (420, 660), School = (66, 82), Walk = (44, 62), Transit = (40, 56),
                Crime = (26, 42), Amenities = (54, 72), YearBuilt = (1948, 1990), Flood = (12, 30), Noise = (30, 52),
                Count = 10, DefaultType = "Single Family"
            }, // North Redlands
            new()
            {
                Zip = "92374", CenterLat = 34.0640, CenterLng = -117.1540,
                Streets = ["San Bernardino Ave", "Wabash Ave", "Independence Way", "Tribune St", "Redlands Blvd", "Ford St"],
                PriceK = (455, 720), School = (72, 86), Walk = (34, 52), Transit = (30, 46),
                Crime = (18, 34), Amenities = (52, 70), YearBuilt = (1986, 2018), Flood = (10, 26), Noise = (22, 40),
                Count = 9, DefaultType = "Single Family"
            }, // East Redlands (newer tracts)
        ];

        // Deterministic dataset, materialized once. IDs are stable 1..N. Declared after
        // the data arrays above so static-field initialization order is correct.
        public static IReadOnlyList<Property> All { get; } = Build();

        private static IReadOnlyList<Property> Build()
        {
            var list = new List<Property>();
            var rng = new Lcg(2_654_435_761u); // fixed seed → deterministic dataset
            var id = 1;
            var brokerIndex = 0;

            foreach (var n in Neighborhoods)
            {
                for (var i = 0; i < n.Count; i++)
                {
                    var street = n.Streets[rng.Next(n.Streets.Length)];
                    var houseNumber = 100 + rng.Next(1900);

                    // Price with realistic spread inside the tier.
                    var priceK = rng.Range(n.PriceK.Min, n.PriceK.Max);
                    var price = Math.Round(priceK * 1000m / 1000m) * 1000m; // whole-thousand price

                    // Size correlated with price tier.
                    var bedrooms = priceK switch
                    {
                        >= 1100 => rng.Range(4, 6),
                        >= 750 => rng.Range(3, 5),
                        >= 550 => rng.Range(3, 4),
                        _ => rng.Range(2, 4)
                    };
                    var bathrooms = Math.Max(1, bedrooms - rng.Range(0, 1));
                    var livingSqft = 700 + (int)(price / 420m) + rng.Range(-180, 220);
                    livingSqft = Math.Clamp(livingSqft, 820, 4200);
                    var acreLot = Math.Round((decimal)(0.11 + rng.NextDouble() * (priceK >= 900 ? 0.5 : 0.28)), 2);

                    var isCondo = priceK < 470 && rng.NextDouble() < 0.5;
                    var propertyType = isCondo
                        ? (rng.NextDouble() < 0.5 ? "Condo" : "Townhouse")
                        : n.DefaultType;

                    var yearBuilt = rng.Range(n.YearBuilt.Min, n.YearBuilt.Max);
                    int? lastReno = rng.NextDouble() < 0.55 ? rng.Range(Math.Max(yearBuilt, 2005), 2024) : null;

                    var price2 = price;
                    var propertyTax = Math.Round(price2 * 0.0115m, 0);   // CA ~1.15%/yr
                    var hoa = isCondo ? rng.Range(180, 420) : (rng.NextDouble() < 0.15 ? rng.Range(45, 160) : 0);
                    var utilities = rng.Range(150, 340);

                    var commute = 8 + rng.Range(0, 34); // minutes to a job center

                    list.Add(new Property
                    {
                        Id = id++,
                        BrokeredBy = Brokerages[brokerIndex++ % Brokerages.Length],
                        Status = "active",
                        Price = price,
                        Bedrooms = bedrooms,
                        Bathrooms = bathrooms,
                        AcreLot = acreLot,
                        LotSqft = livingSqft,

                        PropertyType = propertyType,
                        GarageSpaces = isCondo ? rng.Range(0, 2) : rng.Range(1, 3),
                        HasPool = !isCondo && priceK >= 650 && rng.NextDouble() < 0.35,
                        Stories = (isCondo || rng.NextDouble() < 0.55) ? 1 : 2,
                        DaysOnMarket = rng.Range(2, 96),

                        Street = $"{houseNumber} {street}",
                        City = "Redlands",
                        State = "CA",
                        ZipCode = n.Zip,

                        Latitude = Math.Round(n.CenterLat + (rng.NextDouble() - 0.5) * 0.018, 6),
                        Longitude = Math.Round(n.CenterLng + (rng.NextDouble() - 0.5) * 0.022, 6),

                        HoaFee = hoa,
                        PropertyTax = propertyTax,
                        Utilities = utilities,

                        SchoolRating = rng.Range(n.School.Min, n.School.Max),
                        CrimeScore = rng.Range(n.Crime.Min, n.Crime.Max),
                        Walkability = rng.Range(n.Walk.Min, n.Walk.Max),
                        TransitAccess = rng.Range(n.Transit.Min, n.Transit.Max),
                        AmenitiesScore = rng.Range(n.Amenities.Min, n.Amenities.Max),

                        CommuteMin = commute,

                        YearBuilt = yearBuilt,
                        LastRenovation = lastReno,
                        RoofCondition = ConditionFor(yearBuilt, lastReno, rng),
                        AcCondition = ConditionFor(yearBuilt, lastReno, rng),
                        PlumbingCondition = ConditionFor(yearBuilt, lastReno, rng),
                        ElectricalCondition = ConditionFor(yearBuilt, lastReno, rng),
                        FloorPlanScore = rng.Range(58, 95),

                        FutureAppreciation = rng.Range(58, 92),
                        ResalePotential = rng.Range(60, 94),

                        FloodRisk = rng.Range(n.Flood.Min, n.Flood.Max),
                        NoiseLevel = rng.Range(n.Noise.Min, n.Noise.Max)
                    });
                }
            }

            return list;
        }

        // Newer builds and recent renovations score higher on system condition.
        private static int ConditionFor(int yearBuilt, int? lastReno, Lcg rng)
        {
            var reference = lastReno ?? yearBuilt;
            var age = 2025 - reference;
            var baseScore = Math.Clamp(96 - age, 45, 96);
            return Math.Clamp(baseScore + rng.Range(-6, 6), 40, 99);
        }

        // Small deterministic linear-congruential generator (no environment randomness).
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

            public int Next(int exclusiveMax) => (int)(NextDouble() * exclusiveMax);

            // Inclusive range [min, max].
            public int Range(int min, int max) => max <= min ? min : min + (int)(NextDouble() * (max - min + 1));
        }
    }
}
