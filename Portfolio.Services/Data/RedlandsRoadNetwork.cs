using Portfolio.Common.DTOs;

namespace Portfolio.Services.Data
{
    // 105-node Redlands / San Bernardino road network.
    //
    // Layout: 7 east-west arterials × 15 north-south corridors forming a dense
    // grid, extended with an outer beltway, residential stubs, diagonal
    // shortcuts, freeway on/off ramps, and a mountain-approach spur to the
    // north.  Edge costs are approximate driving distances in km (straight-line
    // × 1.3 detour factor), rounded to 2 dp.
    //
    // Node 105 = Esri HQ (380 New York St, Redlands, CA 92373).
    //
    // Grid layout (rows south→north, columns west→east):
    //   Row A  nodes  1–15   Lat ≈ 34.038   Cypress Ave / Barton Rd
    //   Row B  nodes 16–30   Lat ≈ 34.044   Lugonia Ave
    //   Row C  nodes 31–45   Lat ≈ 34.050   W Redlands Blvd / New York St
    //   Row D  nodes 46–60   Lat ≈ 34.056   Colton Ave
    //   Row E  nodes 61–75   Lat ≈ 34.062   University St
    //   Row F  nodes 76–87   Lat ≈ 34.068   Highland Ave / outer beltway
    //   Row G  nodes 88–100  Lat ≈ 34.075   Mountain View Dr spur
    //   Specials 101–105
    //
    // 15 columns (west→east):
    //   Col  1  Lng ≈ -117.230  Orange Ave (far west / San Bernardino border)
    //   Col  2  Lng ≈ -117.218  Eureka St
    //   Col  3  Lng ≈ -117.206  Wabash Ave
    //   Col  4  Lng ≈ -117.194  Orange St / Church St
    //   Col  5  Lng ≈ -117.182  State St
    //   Col  6  Lng ≈ -117.170  5th St
    //   Col  7  Lng ≈ -117.158  9th St
    //   Col  8  Lng ≈ -117.146  Brookside Ave
    //   Col  9  Lng ≈ -117.134  Pennsylvania Ave
    //   Col 10  Lng ≈ -117.122  Nevada St
    //   Col 11  Lng ≈ -117.110  Tennessee St
    //   Col 12  Lng ≈ -117.098  New Jersey St
    //   Col 13  Lng ≈ -117.086  Brockton Ave
    //   Col 14  Lng ≈ -117.074  Alabama St
    //   Col 15  Lng ≈ -117.062  E Redlands / Wabash (far east)
    internal static class RedlandsRoadNetwork
    {
        public const int EsriHqNodeId = 105;

        // Column longitudes (index 0 = col 1)
        private static readonly double[] ColLng = [
            -117.230, -117.218, -117.206, -117.194, -117.182,
            -117.170, -117.158, -117.146, -117.134, -117.122,
            -117.110, -117.098, -117.086, -117.074, -117.062
        ];

        // Row latitudes (index 0 = row A)
        private static readonly double[] RowLat = [
            34.038, 34.044, 34.050, 34.056, 34.062, 34.068, 34.075
        ];

        private static readonly string[] RowNames = [
            "Barton Rd", "Lugonia Ave", "Redlands Blvd", "Colton Ave",
            "University St", "Highland Ave", "Mountain View Dr"
        ];

        private static readonly string[] ColNames = [
            "Orange Ave", "Eureka St", "Wabash Ave", "Orange St", "State St",
            "5th St", "9th St", "Brookside Ave", "Pennsylvania Ave", "Nevada St",
            "Tennessee St", "New Jersey St", "Brockton Ave", "Alabama St", "E Wabash"
        ];

        public static RoadGraphDto Build()
        {
            var nodes = new List<GraphNodeDto>(115);
            var edges = new List<GraphEdgeDto>(340);

            // ── Grid nodes 1-105 ────────────────────────────────────────────────
            // Rows A-G (0-5), 15 cols each = 90 grid nodes, then specials
            // Row A =  1-15, Row B = 16-30, Row C = 31-45,
            // Row D = 46-60, Row E = 61-75, Row F = 76-90
            // Row G = 91-104 (only 14 cols; col 15 is Esri HQ area)
            for (int row = 0; row < 6; row++)
            {
                for (int col = 0; col < 15; col++)
                {
                    int id = row * 15 + col + 1;
                    nodes.Add(new GraphNodeDto
                    {
                        Id        = id,
                        Latitude  = RowLat[row],
                        Longitude = ColLng[col],
                        Label     = $"{RowNames[row]} & {ColNames[col]}"
                    });
                }
            }

            // Row G — 14 cols (ids 91-104); col 15 slot is Esri HQ (105)
            for (int col = 0; col < 14; col++)
            {
                int id = 90 + col + 1;
                nodes.Add(new GraphNodeDto
                {
                    Id        = id,
                    Latitude  = RowLat[6],
                    Longitude = ColLng[col],
                    Label     = $"{RowNames[6]} & {ColNames[col]}"
                });
            }

            // Esri HQ
            nodes.Add(new GraphNodeDto
            {
                Id        = 105,
                Latitude  = 34.0567,
                Longitude = -117.1957,
                Label     = "Esri HQ — 380 New York St"
            });

            // ── East-west arterial edges (along each row) ───────────────────────
            // Rows A-F: cols 1-15 (15 nodes per row, 14 segments)
            for (int row = 0; row < 6; row++)
            {
                for (int col = 0; col < 14; col++)
                {
                    int a = row * 15 + col + 1;
                    int b = a + 1;
                    double cost = HaversineApprox(RowLat[row], ColLng[col], RowLat[row], ColLng[col + 1]);
                    edges.Add(new GraphEdgeDto { FromNodeId = a, ToNodeId = b, Cost = cost, Bidirectional = true });
                }
            }

            // Row G: cols 1-14 (13 segments between nodes 91-104)
            for (int col = 0; col < 13; col++)
            {
                int a = 91 + col;
                int b = a + 1;
                double cost = HaversineApprox(RowLat[6], ColLng[col], RowLat[6], ColLng[col + 1]);
                edges.Add(new GraphEdgeDto { FromNodeId = a, ToNodeId = b, Cost = cost, Bidirectional = true });
            }

            // ── North-south connector edges (along each column) ─────────────────
            // Rows A-F → A-G for cols 1-14; col 15 only rows A-F
            for (int col = 0; col < 15; col++)
            {
                int maxRow = (col < 14) ? 6 : 5; // col 15 stops at row F
                for (int row = 0; row < maxRow; row++)
                {
                    int a, b;
                    if (row < 6)
                    {
                        a = row * 15 + col + 1;
                        b = (row < 5) ? (row + 1) * 15 + col + 1 : 91 + col; // row F→G
                    }
                    else break;

                    double cost = HaversineApprox(RowLat[row], ColLng[col], RowLat[row + 1 < 7 ? row + 1 : 6], ColLng[col]);
                    edges.Add(new GraphEdgeDto { FromNodeId = a, ToNodeId = b, Cost = cost, Bidirectional = true });
                }
            }

            // ── Diagonal shortcuts (major intersections only) ───────────────────
            // Value tuples avoid allocating ~35 nested int[] arrays.
            (int row, int col)[] diagonalPairs = [
                (0,0),(0,2),(0,4),(0,6),(0,8),(0,10),(0,12),
                (1,1),(1,3),(1,5),(1,7),(1,9),(1,11),(1,13),
                (2,0),(2,2),(2,4),(2,6),(2,8),(2,10),(2,12),
                (3,1),(3,3),(3,5),(3,7),(3,9),(3,11),
                (4,0),(4,2),(4,4),(4,6),(4,8),(4,10),(4,12),
            ];
            foreach (var (row, col) in diagonalPairs)
            {
                int a = row * 15 + col + 1;
                int bRow = row + 1;
                int bCol = col + 1;
                int b = bRow < 6 ? bRow * 15 + bCol + 1 : 91 + bCol;
                double cost = HaversineApprox(RowLat[row], ColLng[col], RowLat[bRow < 7 ? bRow : 6], ColLng[bCol]);
                edges.Add(new GraphEdgeDto { FromNodeId = a, ToNodeId = b, Cost = Math.Round(cost * 1.05, 2), Bidirectional = true });
            }

            // ── Residential dead-end stubs (force exploration) ──────────────────
            // Pre-build a lookup so the inner body is O(1) instead of O(n).
            var nodeLookupForStubs = new Dictionary<int, GraphNodeDto>(nodes.Count);
            foreach (var n in nodes) nodeLookupForStubs[n.Id] = n;

            int[] stubAnchors = [31, 33, 35, 37, 39, 41, 43];
            for (int i = 0; i < stubAnchors.Length; i++)
            {
                int anchor = stubAnchors[i];
                var anchorNode = nodeLookupForStubs[anchor];
                int stubTarget = anchor + 2 <= 45 ? anchor + 2 : anchor - 2;
                edges.Add(new GraphEdgeDto { FromNodeId = anchor, ToNodeId = stubTarget, Cost = Math.Round(HaversineApprox(anchorNode.Latitude, anchorNode.Longitude, anchorNode.Latitude + 0.003, anchorNode.Longitude + 0.005) * 2.0, 2), Bidirectional = false });
            }

            // ── I-10 freeway on/off ramp cluster (nodes 106-109 specials) ───────
            // Represent freeway ramps near Redlands Blvd I-10 interchange.
            // These sit at row C latitude, west end — high-speed low-cost edges.
            nodes.Add(new GraphNodeDto { Id = 106, Latitude = 34.050, Longitude = -117.245, Label = "I-10 WB On-Ramp @ Tippecanoe" });
            nodes.Add(new GraphNodeDto { Id = 107, Latitude = 34.050, Longitude = -117.240, Label = "I-10 WB Off-Ramp @ Tippecanoe" });
            nodes.Add(new GraphNodeDto { Id = 108, Latitude = 34.051, Longitude = -117.235, Label = "I-10 EB On-Ramp @ Mountain View" });
            nodes.Add(new GraphNodeDto { Id = 109, Latitude = 34.051, Longitude = -117.232, Label = "I-10 EB Off-Ramp @ Alabama" });

            // Ramp to grid connections (high-speed, low cost)
            int rowCCol1 = 31; // Row C, col 1
            edges.Add(new GraphEdgeDto { FromNodeId = 106, ToNodeId = 107,      Cost = 0.55, Bidirectional = false }); // freeway segment
            edges.Add(new GraphEdgeDto { FromNodeId = 107, ToNodeId = 108,      Cost = 0.48, Bidirectional = false });
            edges.Add(new GraphEdgeDto { FromNodeId = 108, ToNodeId = 109,      Cost = 0.32, Bidirectional = false });
            edges.Add(new GraphEdgeDto { FromNodeId = 109, ToNodeId = rowCCol1, Cost = 0.28, Bidirectional = false }); // off-ramp to surface
            edges.Add(new GraphEdgeDto { FromNodeId = rowCCol1, ToNodeId = 106, Cost = 0.30, Bidirectional = false }); // on-ramp from surface
            // Freeway bypass shortcut across cols 1-4 (row C)
            edges.Add(new GraphEdgeDto { FromNodeId = 106, ToNodeId = 34,       Cost = 1.10, Bidirectional = true });  // 34 = Row C col 4
            edges.Add(new GraphEdgeDto { FromNodeId = 108, ToNodeId = 32,       Cost = 0.85, Bidirectional = true });  // 32 = Row C col 2

            // ── Esri HQ connections ─────────────────────────────────────────────
            // Node 105 (Esri HQ) connects to the four grid nodes surrounding it.
            // row D (index 3), col 3 (0-based) → id 49   Colton Ave & Orange St
            // row D (index 3), col 4 (0-based) → id 50   Colton Ave & State St
            // row C (index 2), col 4 (0-based) → id 35   Redlands Blvd & State St
            // row E (index 4), col 4 (0-based) → id 66   University St & State St
            // row D (index 3), col 5 (0-based) → id 51   Colton Ave & 5th St
            edges.Add(new GraphEdgeDto { FromNodeId = 105, ToNodeId = 49, Cost = 0.62, Bidirectional = true }); // → Colton Ave & Orange St
            edges.Add(new GraphEdgeDto { FromNodeId = 105, ToNodeId = 50, Cost = 0.41, Bidirectional = true }); // → Colton Ave & State St
            edges.Add(new GraphEdgeDto { FromNodeId = 105, ToNodeId = 35, Cost = 0.55, Bidirectional = true }); // → Redlands Blvd & State St
            edges.Add(new GraphEdgeDto { FromNodeId = 105, ToNodeId = 66, Cost = 0.48, Bidirectional = true }); // → University St & State St
            edges.Add(new GraphEdgeDto { FromNodeId = 105, ToNodeId = 51, Cost = 0.52, Bidirectional = true }); // → Colton Ave & 5th St

            return new RoadGraphDto
            {
                GraphName  = "Redlands / San Bernardino, CA — 109-Node Grid",
                DestinationNodeId = EsriHqNodeId,
                Nodes = nodes,
                Edges = edges
            };
        }

        // Haversine distance in km between two lat/lng points × 1.3 detour factor, rounded 2dp.
        private static double HaversineApprox(double lat1, double lng1, double lat2, double lng2)
        {
            const double R = 6371.0;
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLng = (lng2 - lng1) * Math.PI / 180.0;
            double a    = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                        + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
                        * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            double dist = R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return Math.Round(dist * 1.3, 2);
        }
    }
}
