using Portfolio.Common.DTOs;

namespace Portfolio.Services.Data
{
    /// <summary>
    /// Deterministic delivery scenarios over the Redlands OSM road network.
    ///
    /// Stops are drawn from actual road-network node coordinates, so snapping is exact and
    /// every stop is reachable from the depot. A fixed-seed LCG makes the whole scenario
    /// byte-identical on every run, exactly like <c>SoCalPolicyBook</c> and
    /// <c>RedlandsRoadNetwork</c> — no database, no persistence.
    /// </summary>
    internal static class RedlandsDeliveryScenario
    {
        // The depot sits on the industrial west side of the network, off Redlands Boulevard.
        private const double DepotSeedLatitude = 34.0620;
        private const double DepotSeedLongitude = -117.2255;
        private const string DepotLabel = "West Redlands Distribution Center";

        // Minutes from midnight: an eight-hour shift from 08:00 to 16:00.
        private const double ShiftStart = 480;
        private const double ShiftEnd = 960;

        // Slack reserved at the end of the shift so a due time never lands on the horizon
        // itself — a vehicle still has to drive home after its last delivery.
        private const double ReturnReserveMinutes = 45;

        private sealed class Preset
        {
            public required string Key { get; init; }
            public required string Name { get; init; }
            public required string Description { get; init; }
            public required int StopCount { get; init; }
            public required int VehicleCount { get; init; }
            public required double VehicleCapacity { get; init; }
            public required double VehicleFixedCost { get; init; }
            public required (double Min, double Max) Demand { get; init; }
            public required (double Min, double Max) ServiceMinutes { get; init; }
            public required (double Min, double Max) WindowWidth { get; init; }
            public required uint Seed { get; init; }
        }

        private static readonly Preset[] Presets =
        [
            new()
            {
                Key = "morning",
                Name = "Morning parcel run",
                Description = "25 residential parcels, three vans, wide delivery windows. The easy case: capacity binds before time does.",
                StopCount = 25,
                VehicleCount = 3,
                VehicleCapacity = 1000,
                VehicleFixedCost = 25,
                Demand = (20, 120),
                ServiceMinutes = (6, 12),
                WindowWidth = (180, 300),
                Seed = 20_260_101u
            },
            new()
            {
                Key = "fullday",
                Name = "Full day route",
                Description = "40 mixed commercial and residential drops across the whole network, five trucks, half-day windows.",
                StopCount = 40,
                VehicleCount = 5,
                VehicleCapacity = 1200,
                VehicleFixedCost = 25,
                Demand = (20, 150),
                ServiceMinutes = (8, 15),
                WindowWidth = (90, 180),
                Seed = 20_260_202u
            },
            new()
            {
                Key = "tight",
                Name = "Tight windows",
                Description = "The same 40 drops with one-hour appointment windows. Time, not capacity, decides how many trucks roll.",
                StopCount = 40,
                VehicleCount = 6,
                VehicleCapacity = 1000,
                VehicleFixedCost = 25,
                Demand = (20, 150),
                ServiceMinutes = (8, 15),
                WindowWidth = (45, 75),
                Seed = 20_260_303u
            }
        ];

        /// <summary>Preset keys accepted by <see cref="Build"/>, in demo order.</summary>
        public static IReadOnlyList<string> PresetKeys { get; } = [.. Presets.Select(p => p.Key)];

        /// <summary>
        /// Builds the named scenario over the supplied road graph. Returns null when the
        /// preset key is not recognised.
        /// </summary>
        public static FleetScenarioDto? Build(RoadGraphDto graph, string preset)
        {
            var definition = Presets.FirstOrDefault(p =>
                string.Equals(p.Key, preset, StringComparison.OrdinalIgnoreCase));
            if (definition is null)
                return null;

            var depotIndex = NearestNodeIndex(graph, DepotSeedLatitude, DepotSeedLongitude);
            var selected = SelectSpreadNodes(graph, definition.StopCount, depotIndex, definition.Seed);

            var rng = new Lcg(definition.Seed);
            var stops = new List<FleetStopDto>(selected.Count);

            for (var i = 0; i < selected.Count; i++)
            {
                var node = graph.Nodes[selected[i]];

                var demand = Math.Round(Lerp(definition.Demand.Min, definition.Demand.Max, rng.NextDouble()) / 5.0) * 5.0;
                var service = Math.Round(Lerp(definition.ServiceMinutes.Min, definition.ServiceMinutes.Max, rng.NextDouble()));
                var width = Math.Round(Lerp(definition.WindowWidth.Min, definition.WindowWidth.Max, rng.NextDouble()) / 5.0) * 5.0;

                var latestStart = ShiftEnd - ReturnReserveMinutes - width;
                var ready = Math.Round(Lerp(ShiftStart, Math.Max(ShiftStart, latestStart), rng.NextDouble()) / 5.0) * 5.0;

                stops.Add(new FleetStopDto
                {
                    Id = i + 1,
                    Label = string.IsNullOrWhiteSpace(node.Label) ? $"Stop {i + 1}" : $"{node.Label} #{i + 1}",
                    Latitude = node.Latitude,
                    Longitude = node.Longitude,
                    Demand = demand,
                    ReadyMinutes = ready,
                    DueMinutes = ready + width,
                    ServiceMinutes = service
                });
            }

            var depot = graph.Nodes[depotIndex];

            return new FleetScenarioDto
            {
                Preset = definition.Key,
                Name = definition.Name,
                Description = definition.Description,
                DepotLatitude = depot.Latitude,
                DepotLongitude = depot.Longitude,
                DepotLabel = DepotLabel,
                Stops = stops,
                VehicleCount = definition.VehicleCount,
                VehicleCapacity = definition.VehicleCapacity,
                ShiftStartMinutes = ShiftStart,
                ShiftEndMinutes = ShiftEnd,
                VehicleFixedCost = definition.VehicleFixedCost
            };
        }

        // Picks `count` well-separated node indices by bucketing the network into a grid and
        // taking at most one node per cell per pass. Sampling the node list directly would
        // clump the stops, because OSM node order follows way order, not geography.
        private static List<int> SelectSpreadNodes(RoadGraphDto graph, int count, int depotIndex, uint seed)
        {
            var nodes = graph.Nodes;
            double minLat = double.MaxValue, maxLat = double.MinValue;
            double minLon = double.MaxValue, maxLon = double.MinValue;

            foreach (var node in nodes)
            {
                if (node.Latitude < minLat) minLat = node.Latitude;
                if (node.Latitude > maxLat) maxLat = node.Latitude;
                if (node.Longitude < minLon) minLon = node.Longitude;
                if (node.Longitude > maxLon) maxLon = node.Longitude;
            }

            var gridSize = Math.Max(2, (int)Math.Ceiling(Math.Sqrt(count * 2.0)));
            var latSpan = Math.Max(maxLat - minLat, 1e-9);
            var lonSpan = Math.Max(maxLon - minLon, 1e-9);

            var cells = new List<int>[gridSize * gridSize];
            for (var c = 0; c < cells.Length; c++)
                cells[c] = [];

            for (var i = 0; i < nodes.Count; i++)
            {
                if (i == depotIndex)
                    continue;

                var row = (int)((nodes[i].Latitude - minLat) / latSpan * gridSize);
                var col = (int)((nodes[i].Longitude - minLon) / lonSpan * gridSize);
                if (row >= gridSize) row = gridSize - 1;
                if (col >= gridSize) col = gridSize - 1;
                cells[row * gridSize + col].Add(i);
            }

            var rng = new Lcg(seed);
            var chosen = new List<int>(count);
            var taken = new HashSet<int>();

            // Repeated passes over the grid: one node per non-empty cell each time, so the
            // stops fill the map evenly before any cell contributes a second stop.
            for (var pass = 0; pass < 16 && chosen.Count < count; pass++)
            {
                for (var c = 0; c < cells.Length && chosen.Count < count; c++)
                {
                    var bucket = cells[c];
                    if (bucket.Count == 0)
                        continue;

                    for (var attempt = 0; attempt < 8; attempt++)
                    {
                        var candidate = bucket[(int)(rng.NextDouble() * bucket.Count) % bucket.Count];
                        if (taken.Add(candidate))
                        {
                            chosen.Add(candidate);
                            break;
                        }
                    }
                }
            }

            chosen.Sort();
            return chosen;
        }

        private static int NearestNodeIndex(RoadGraphDto graph, double latitude, double longitude)
        {
            var best = 0;
            var bestDistance = double.MaxValue;
            var cosLat = Math.Cos(latitude * Math.PI / 180.0);

            for (var i = 0; i < graph.Nodes.Count; i++)
            {
                var dLat = graph.Nodes[i].Latitude - latitude;
                var dLon = (graph.Nodes[i].Longitude - longitude) * cosLat;
                var distance = dLat * dLat + dLon * dLon;
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = i;
            }

            return best;
        }

        private static double Lerp(double min, double max, double t) => min + (max - min) * t;

        // Park-Miller minimal-standard LCG: the same generator the rest of the portfolio's
        // demo datasets use, so scenarios are reproducible across processes and platforms.
        private sealed class Lcg
        {
            private long _state;

            public Lcg(uint seed) => _state = seed % 2147483647L;

            public double NextDouble()
            {
                _state = _state * 48271L % 2147483647L;
                return _state / 2147483647.0;
            }
        }
    }
}
