using Portfolio.Common.DTOs;

namespace Portfolio.Services.Data
{
    /// <summary>
    /// A deterministic synthetic electric distribution network over Redlands: one
    /// substation, two radial feeders with their protective devices and fused laterals,
    /// and the normally-open tie switch between them.
    ///
    /// Generated from a fixed-seed LCG so the circuit is identical on every run. No
    /// database, no persistence: this is a stateless demo dataset in the same shape as
    /// <c>RedlandsRoadNetwork</c> and <c>SoCalPolicyBook</c>.
    ///
    /// The tie is the whole point of the dataset. Without a normally-open point between
    /// the two feeders there is nothing to restore from, and the restoration search would
    /// have no candidate to evaluate.
    /// </summary>
    internal static class RedlandsDistributionNetwork
    {
        public const string NetworkName = "Redlands Substation — Feeders A and B";

        /// <summary>Node id of the substation bus. Every feeder breaker leaves this node.</summary>
        public const int SubstationNodeId = 1;

        private const double SubstationLatitude = 34.0421;
        private const double SubstationLongitude = -117.2130;

        // Longitude degrees are shorter than latitude degrees at this latitude, so spans
        // drawn east/west are stretched to keep the circuit geographically plausible.
        private const double LongitudeStretch = 1.21;

        private const double DegToRad = Math.PI / 180.0;

        public static IReadOnlyList<NetworkElementDto> Elements { get; } = BuildElements();

        public static int TotalCustomers { get; } = Elements.Sum(e => e.CustomerCount);

        public static IReadOnlyList<string> FeederNames { get; } = ["Feeder A", "Feeder B"];

        private sealed class Builder
        {
            private readonly Lcg _rng;
            private readonly Dictionary<int, (double Lat, double Lon)> _coordinates = new();
            private int _nextNodeId = SubstationNodeId;
            private int _nextElementId;

            public Builder(uint seed)
            {
                _rng = new Lcg(seed);
                _coordinates[NewNode()] = (SubstationLatitude, SubstationLongitude);
            }

            public List<NetworkElementDto> Elements { get; } = [];

            public double Next() => _rng.NextDouble();

            public int NewNode() => _nextNodeId++;

            // Creates the node reached by travelling `lengthDegrees` from `fromNode` along
            // `bearingRadians`, measured counter-clockwise from due east.
            public int Extend(int fromNode, double bearingRadians, double lengthDegrees)
            {
                var origin = _coordinates[fromNode];
                var node = NewNode();
                _coordinates[node] = (
                    origin.Lat + Math.Sin(bearingRadians) * lengthDegrees,
                    origin.Lon + Math.Cos(bearingRadians) * lengthDegrees * LongitudeStretch);
                return node;
            }

            public NetworkElementDto Add(
                string label,
                int fromNode,
                int toNode,
                int deviceType,
                bool isOpen,
                int customerCount,
                string feederName)
            {
                var from = _coordinates[fromNode];
                var to = _coordinates[toNode];

                var element = new NetworkElementDto
                {
                    Id = ++_nextElementId,
                    Label = label,
                    FromNodeId = fromNode,
                    ToNodeId = toNode,
                    DeviceType = deviceType,
                    IsOpen = isOpen,
                    CustomerCount = customerCount,
                    FromLatitude = Math.Round(from.Lat, 6),
                    FromLongitude = Math.Round(from.Lon, 6),
                    ToLatitude = Math.Round(to.Lat, 6),
                    ToLongitude = Math.Round(to.Lon, 6),
                    FeederName = feederName
                };

                Elements.Add(element);
                return element;
            }
        }

        private static List<NetworkElementDto> BuildElements()
        {
            var builder = new Builder(2_246_822_519u);

            // Feeder A sweeps east then north; Feeder B sweeps north then east. The two
            // trunks therefore end up adjacent in the north-east corner of the service
            // territory, which is exactly where a utility would install the tie.
            var endOfA = BuildFeeder(builder, "A", "Feeder A", trunkSegments: 14,
                startBearingDegrees: 8, endBearingDegrees: 82, stepDegrees: 0.0042,
                recloserAfter: [4, 9], switchAfter: [3, 6, 9, 12], lateralAfter: [1, 2, 3, 5, 7, 8, 10, 11, 13]);

            var endOfB = BuildFeeder(builder, "B", "Feeder B", trunkSegments: 12,
                startBearingDegrees: 82, endBearingDegrees: 8, stepDegrees: 0.0049,
                recloserAfter: [5], switchAfter: [2, 6, 10], lateralAfter: [1, 2, 4, 6, 7, 8, 10, 11]);

            // The normally-open point. Closing it backfeeds Feeder A's tail from Feeder B
            // (or the reverse), which is the only reason restoration is possible at all.
            builder.Add("TIE T-1", endOfA, endOfB, DeviceTypes.TieSwitch, isOpen: true, customerCount: 0, feederName: "Tie");

            return builder.Elements;
        }

        // Builds one radial feeder: breaker, trunk with inline protective devices and
        // sectionalizing switches, and fused laterals hanging off the trunk. Returns the
        // node at the far end of the trunk, which is where a tie attaches.
        private static int BuildFeeder(
            Builder builder,
            string code,
            string feederName,
            int trunkSegments,
            double startBearingDegrees,
            double endBearingDegrees,
            double stepDegrees,
            int[] recloserAfter,
            int[] switchAfter,
            int[] lateralAfter)
        {
            const double deviceLength = 0.0006;

            var bearing = startBearingDegrees * DegToRad;
            var breakerNode = builder.Extend(SubstationNodeId, bearing, deviceLength);
            builder.Add($"BRK {code}", SubstationNodeId, breakerNode, DeviceTypes.Breaker, isOpen: false, customerCount: 0, feederName);

            var recloserIndex = 0;
            var lateralIndex = 0;
            var node = breakerNode;

            for (var i = 1; i <= trunkSegments; i++)
            {
                var t = trunkSegments <= 1 ? 0.0 : (double)(i - 1) / (trunkSegments - 1);
                bearing = (startBearingDegrees + (endBearingDegrees - startBearingDegrees) * t) * DegToRad;
                bearing += (builder.Next() - 0.5) * 0.22;

                var next = builder.Extend(node, bearing, stepDegrees);
                builder.Add($"{code}-TRUNK-{i:D2}", node, next, DeviceTypes.Conductor, isOpen: false, customerCount: 0, feederName);
                node = next;

                if (Array.IndexOf(recloserAfter, i) >= 0)
                {
                    recloserIndex++;
                    var devNode = builder.Extend(node, bearing, deviceLength);
                    builder.Add($"REC {code}-{recloserIndex}", node, devNode, DeviceTypes.Recloser, isOpen: false, customerCount: 0, feederName);
                    node = devNode;
                }

                if (Array.IndexOf(switchAfter, i) >= 0)
                {
                    var devNode = builder.Extend(node, bearing, deviceLength);
                    builder.Add($"SW {code}-{i:D2}", node, devNode, DeviceTypes.Switch, isOpen: false, customerCount: 0, feederName);
                    node = devNode;
                }

                if (Array.IndexOf(lateralAfter, i) >= 0)
                {
                    lateralIndex++;
                    // Laterals alternate sides of the trunk so the circuit reads as a tree
                    // rather than a comb hanging off one edge.
                    var side = lateralIndex % 2 == 0 ? 1.0 : -1.0;
                    var lateralBearing = bearing + side * (Math.PI / 2.0) + (builder.Next() - 0.5) * 0.5;
                    BuildLateral(builder, code, feederName, lateralIndex, node, lateralBearing, depth: 0);
                }
            }

            return node;
        }

        // A fused lateral: a fuse at the tap, then conductor spans each terminating in a
        // service transformer. Occasionally a sub-lateral taps off the middle of one.
        private static void BuildLateral(
            Builder builder,
            string code,
            string feederName,
            int lateralIndex,
            int tapNode,
            double bearing,
            int depth)
        {
            const double deviceLength = 0.0006;
            const double spanLength = 0.0021;

            var name = depth == 0 ? $"{code}-LAT-{lateralIndex}" : $"{code}-LAT-{lateralIndex}-SUB";

            var fuseNode = builder.Extend(tapNode, bearing, deviceLength);
            builder.Add($"FUSE {name}", tapNode, fuseNode, DeviceTypes.Fuse, isOpen: false, customerCount: 0, feederName);

            var segments = depth == 0 ? 4 + (int)(builder.Next() * 4) : 2 + (int)(builder.Next() * 2);
            var node = fuseNode;

            for (var s = 1; s <= segments; s++)
            {
                var segmentBearing = bearing + (builder.Next() - 0.5) * 0.55;
                var next = builder.Extend(node, segmentBearing, spanLength);
                builder.Add($"{name}-SEG-{s}", node, next, DeviceTypes.Conductor, isOpen: false, customerCount: 0, feederName);
                node = next;

                // Service transformer on a short spur off the span end.
                var spurBearing = segmentBearing + (s % 2 == 0 ? 1.0 : -1.0) * 0.8;
                var spurNode = builder.Extend(node, spurBearing, deviceLength * 1.6);
                var customers = 8 + (int)(builder.Next() * 33);
                builder.Add($"XFMR {name}-{s}", node, spurNode, DeviceTypes.Transformer, isOpen: false, customers, feederName);

                // One sub-lateral per third lateral, tapped mid-run.
                if (depth == 0 && lateralIndex % 3 == 0 && s == 2)
                    BuildLateral(builder, code, feederName, lateralIndex, node, segmentBearing - 1.15, depth: 1);
            }
        }

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
