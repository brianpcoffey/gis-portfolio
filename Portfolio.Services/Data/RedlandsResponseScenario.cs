using Portfolio.Common.DTOs;

namespace Portfolio.Services.Data
{
    /// <summary>
    /// A deterministic emergency-response scenario laid over the Redlands OSM road
    /// network: clustered call demand, candidate station sites, and the two stations
    /// operating today.
    ///
    /// Every location is a real road-network node rather than a snapped coordinate, so
    /// the candidate-to-demand travel-time matrix has no unreachable pairs. Generated
    /// from a fixed-seed LCG, in the same shape as <c>SoCalPolicyBook</c>.
    /// </summary>
    internal static class RedlandsResponseScenario
    {
        public const string ScenarioName = "Redlands, CA — Fire Response Coverage";

        private const int DemandPointCount = 450;
        private const int CandidateCount = 24;

        /// <summary>
        /// A call-generating district. Demand density and call volume both follow the
        /// district, which is what gives the optimizer something to find — uniform demand
        /// makes every siting look equally good.
        /// </summary>
        private sealed class District
        {
            public required string Name { get; init; }
            public required double Latitude { get; init; }
            public required double Longitude { get; init; }
            public required double SpreadDegrees { get; init; }
            public required int Count { get; init; }
            public required (double Min, double Max) CallVolume { get; init; }
        }

        // Call volume per district reflects the usual pattern: the downtown core and the
        // hospital corridor generate the medical-aid calls, dense residential is moderate,
        // and the industrial fringe is low but geographically remote.
        private static readonly District[] Districts =
        [
            new() { Name = "Downtown core",        Latitude = 34.0556, Longitude = -117.1825, SpreadDegrees = 0.0090, Count = 120, CallVolume = (55, 130) },
            new() { Name = "Hospital corridor",    Latitude = 34.0468, Longitude = -117.2065, SpreadDegrees = 0.0080, Count =  85, CallVolume = (45, 105) },
            new() { Name = "North Redlands",       Latitude = 34.0742, Longitude = -117.1930, SpreadDegrees = 0.0130, Count =  80, CallVolume = (12,  34) },
            new() { Name = "South hills",          Latitude = 34.0348, Longitude = -117.1690, SpreadDegrees = 0.0150, Count =  70, CallVolume = (8,   24) },
            new() { Name = "East Redlands",        Latitude = 34.0600, Longitude = -117.1450, SpreadDegrees = 0.0140, Count =  60, CallVolume = (10,  28) },
            new() { Name = "Industrial / airport", Latitude = 34.0870, Longitude = -117.1480, SpreadDegrees = 0.0160, Count =  35, CallVolume = (3,   11) }
        ];

        // The two stations in service today. Coordinates are approximate district centres
        // rather than surveyed addresses; each is snapped to its nearest network node.
        private static readonly (string Label, double Latitude, double Longitude)[] ExistingStations =
        [
            ("Station 261 — Downtown",      34.0560, -117.1830),
            ("Station 263 — North Redlands", 34.0755, -117.1935)
        ];

        // Proposed sites are spread across the network so the optimizer has real choices.
        // The generator picks the network node nearest each of these anchors.
        private static readonly (double Latitude, double Longitude)[] CandidateAnchors =
        [
            (34.0470, -117.2070), (34.0350, -117.1700), (34.0605, -117.1455), (34.0865, -117.1490),
            (34.0640, -117.2050), (34.0420, -117.1560), (34.0730, -117.1690), (34.0510, -117.1420),
            (34.0300, -117.1880), (34.0800, -117.2100), (34.0660, -117.1750), (34.0450, -117.1930),
            (34.0560, -117.2200), (34.0900, -117.1800), (34.0380, -117.2050), (34.0700, -117.1350),
            (34.0250, -117.1650), (34.0840, -117.1620), (34.0530, -117.1640), (34.0610, -117.2160),
            (34.0390, -117.1790), (34.0770, -117.1520)
        ];

        private static ResponseScenarioDto? _cached;
        private static readonly Lock _gate = new();

        /// <summary>
        /// Builds the scenario against the supplied road graph. The result is cached: the
        /// graph never changes at runtime, and the nearest-node scan is the only
        /// non-trivial work.
        /// </summary>
        public static ResponseScenarioDto Build(RoadGraphDto graph)
        {
            ArgumentNullException.ThrowIfNull(graph);

            if (_cached is not null)
                return _cached;

            lock (_gate)
            {
                _cached ??= BuildCore(graph);
                return _cached;
            }
        }

        private static ResponseScenarioDto BuildCore(RoadGraphDto graph)
        {
            var nodes = graph.Nodes;
            var candidates = BuildCandidates(nodes);
            var usedByCandidate = new HashSet<int>(candidates.Select(c => c.NodeId));
            var demandPoints = BuildDemandPoints(nodes, usedByCandidate);

            return new ResponseScenarioDto
            {
                Name = ScenarioName,
                DemandPoints = demandPoints,
                Candidates = candidates,
                ExistingStationIds = [.. candidates.Where(c => c.IsExisting).Select(c => c.Id)],
                TotalCallVolume = demandPoints.Sum(d => d.CallVolume)
            };
        }

        private static List<CandidateSiteDto> BuildCandidates(IReadOnlyList<GraphNodeDto> nodes)
        {
            var candidates = new List<CandidateSiteDto>(CandidateCount);
            var used = new HashSet<int>();
            var id = 1;

            foreach (var (label, latitude, longitude) in ExistingStations)
            {
                var node = NearestUnusedNode(nodes, latitude, longitude, used);
                used.Add(node.Id);
                candidates.Add(new CandidateSiteDto
                {
                    Id = id++,
                    NodeId = node.Id,
                    Label = label,
                    Latitude = node.Latitude,
                    Longitude = node.Longitude,
                    IsExisting = true
                });
            }

            foreach (var (latitude, longitude) in CandidateAnchors)
            {
                var node = NearestUnusedNode(nodes, latitude, longitude, used);
                used.Add(node.Id);
                candidates.Add(new CandidateSiteDto
                {
                    Id = id,
                    NodeId = node.Id,
                    // The node label is the OSM street name, which makes the proposed site
                    // read like a real address rather than a coordinate.
                    Label = string.IsNullOrWhiteSpace(node.Label)
                        ? $"Proposed site {id - ExistingStations.Length}"
                        : $"Proposed — {node.Label}",
                    Latitude = node.Latitude,
                    Longitude = node.Longitude,
                    IsExisting = false
                });
                id++;
            }

            return candidates;
        }

        private static List<DemandPointDto> BuildDemandPoints(
            IReadOnlyList<GraphNodeDto> nodes,
            HashSet<int> reserved)
        {
            var rng = new Lcg(3_141_592_653u);
            var demandPoints = new List<DemandPointDto>(DemandPointCount);
            var used = new HashSet<int>(reserved);
            var id = 1;

            foreach (var district in Districts)
            {
                for (var i = 0; i < district.Count; i++)
                {
                    // Polar offset so density falls off toward the district edge rather
                    // than filling a square.
                    var angle = rng.NextDouble() * 2.0 * Math.PI;
                    var radial = Math.Sqrt(rng.NextDouble());
                    var latitude = district.Latitude + Math.Sin(angle) * radial * district.SpreadDegrees;
                    var longitude = district.Longitude + Math.Cos(angle) * radial * district.SpreadDegrees * 1.2;

                    var node = NearestUnusedNode(nodes, latitude, longitude, used);
                    used.Add(node.Id);

                    // Volume falls off from the district centre, so the heaviest demand
                    // concentrates rather than scattering evenly across the district.
                    var volumeMix = Math.Clamp(0.70 * (1.0 - radial) + 0.30 * rng.NextDouble(), 0.0, 1.0);
                    var callVolume = Math.Round(
                        Lerp(district.CallVolume.Min, district.CallVolume.Max, volumeMix));

                    demandPoints.Add(new DemandPointDto
                    {
                        Id = id++,
                        NodeId = node.Id,
                        Latitude = node.Latitude,
                        Longitude = node.Longitude,
                        CallVolume = Math.Max(callVolume, 1)
                    });
                }
            }

            return demandPoints;
        }

        // Linear scan for the nearest node not already taken. At 2,530 nodes and ~470
        // lookups this is a couple of million squared-distance tests — well under the cost
        // of building a spatial index, and the result is cached for the process lifetime.
        private static GraphNodeDto NearestUnusedNode(
            IReadOnlyList<GraphNodeDto> nodes,
            double latitude,
            double longitude,
            HashSet<int> used)
        {
            // Longitude degrees are shorter than latitude degrees at this latitude; scaling
            // by cos(lat) keeps "nearest" isotropic without paying for a haversine.
            var cosLat = Math.Cos(latitude * Math.PI / 180.0);
            GraphNodeDto? best = null;
            var bestDistance = double.PositiveInfinity;
            GraphNodeDto? fallback = null;
            var fallbackDistance = double.PositiveInfinity;

            foreach (var node in nodes)
            {
                var dLat = node.Latitude - latitude;
                var dLon = (node.Longitude - longitude) * cosLat;
                var distance = dLat * dLat + dLon * dLon;

                if (distance < fallbackDistance)
                {
                    fallbackDistance = distance;
                    fallback = node;
                }

                if (used.Contains(node.Id))
                    continue;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = node;
                }
            }

            return best ?? fallback!;
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
