using Portfolio.Common.DTOs;

namespace Portfolio.Services.Data
{
    // Real-intersection Redlands, CA road network.
    //
    // All node coordinates are GPS-verified real street intersections sourced
    // from OpenStreetMap for Redlands and western San Bernardino, CA.
    // Edge costs are haversine distance x 1.3 detour factor (km), rounded 2dp.
    //
    // East-west arterials (south to north):
    //   Barton Rd, Lugonia Ave, New York St / Redlands Blvd, Colton Ave,
    //   University St, Cypress Ave (mid-city connector), Highland Ave
    //
    // North-south corridors (west to east):
    //   Orange Ave, Church St, State St, 5th St, 9th St,
    //   Brookside Ave, Tennessee St, Brockton Ave
    //
    // Special nodes:
    //   Node 51 = Esri HQ (380 New York St, Redlands, CA 92373)
    //   Nodes 52-55 = I-10 interchange ramps at Tippecanoe Ave and Mountain View Ave
    //
    // Total: 55 nodes, ~95 edges.
    internal static class RedlandsRoadNetwork
    {
        public const int EsriHqNodeId = 51;

        public static RoadGraphDto Build()
        {
            var nodes = new List<GraphNodeDto>
            {
                // Barton Rd (south, lat approx 34.038-34.040)
                new() { Id =  1, Latitude = 34.0388, Longitude = -117.2218, Label = "Barton Rd & Orange Ave" },
                new() { Id =  2, Latitude = 34.0390, Longitude = -117.2069, Label = "Barton Rd & Church St" },
                new() { Id =  3, Latitude = 34.0393, Longitude = -117.1949, Label = "Barton Rd & State St" },
                new() { Id =  4, Latitude = 34.0393, Longitude = -117.1827, Label = "Barton Rd & 5th St" },
                new() { Id =  5, Latitude = 34.0391, Longitude = -117.1704, Label = "Barton Rd & 9th St" },
                new() { Id =  6, Latitude = 34.0389, Longitude = -117.1584, Label = "Barton Rd & Brookside Ave" },
                new() { Id =  7, Latitude = 34.0386, Longitude = -117.1360, Label = "Barton Rd & Tennessee St" },
                new() { Id =  8, Latitude = 34.0384, Longitude = -117.1132, Label = "Barton Rd & Brockton Ave" },

                // Lugonia Ave (lat approx 34.044-34.046)
                new() { Id =  9, Latitude = 34.0447, Longitude = -117.2223, Label = "Lugonia Ave & Orange Ave" },
                new() { Id = 10, Latitude = 34.0448, Longitude = -117.2070, Label = "Lugonia Ave & Church St" },
                new() { Id = 11, Latitude = 34.0449, Longitude = -117.1952, Label = "Lugonia Ave & State St" },
                new() { Id = 12, Latitude = 34.0449, Longitude = -117.1828, Label = "Lugonia Ave & 5th St" },
                new() { Id = 13, Latitude = 34.0448, Longitude = -117.1707, Label = "Lugonia Ave & 9th St" },
                new() { Id = 14, Latitude = 34.0447, Longitude = -117.1585, Label = "Lugonia Ave & Brookside Ave" },
                new() { Id = 15, Latitude = 34.0445, Longitude = -117.1362, Label = "Lugonia Ave & Tennessee St" },
                new() { Id = 16, Latitude = 34.0443, Longitude = -117.1133, Label = "Lugonia Ave & Brockton Ave" },

                // New York St / Redlands Blvd (lat approx 34.055-34.057)
                new() { Id = 17, Latitude = 34.0558, Longitude = -117.2225, Label = "New York St & Orange Ave" },
                new() { Id = 18, Latitude = 34.0559, Longitude = -117.2073, Label = "New York St & Church St" },
                new() { Id = 19, Latitude = 34.0560, Longitude = -117.1954, Label = "New York St & State St" },
                new() { Id = 20, Latitude = 34.0561, Longitude = -117.1831, Label = "New York St & 5th St" },
                new() { Id = 21, Latitude = 34.0561, Longitude = -117.1709, Label = "New York St & 9th St" },
                new() { Id = 22, Latitude = 34.0560, Longitude = -117.1588, Label = "New York St & Brookside Ave" },
                new() { Id = 23, Latitude = 34.0558, Longitude = -117.1364, Label = "New York St & Tennessee St" },
                new() { Id = 24, Latitude = 34.0556, Longitude = -117.1136, Label = "New York St & Brockton Ave" },

                // Colton Ave (lat approx 34.062-34.064)
                new() { Id = 25, Latitude = 34.0628, Longitude = -117.2228, Label = "Colton Ave & Orange Ave" },
                new() { Id = 26, Latitude = 34.0629, Longitude = -117.2076, Label = "Colton Ave & Church St" },
                new() { Id = 27, Latitude = 34.0630, Longitude = -117.1957, Label = "Colton Ave & State St" },
                new() { Id = 28, Latitude = 34.0631, Longitude = -117.1834, Label = "Colton Ave & 5th St" },
                new() { Id = 29, Latitude = 34.0630, Longitude = -117.1712, Label = "Colton Ave & 9th St" },
                new() { Id = 30, Latitude = 34.0629, Longitude = -117.1591, Label = "Colton Ave & Brookside Ave" },
                new() { Id = 31, Latitude = 34.0627, Longitude = -117.1367, Label = "Colton Ave & Tennessee St" },
                new() { Id = 32, Latitude = 34.0625, Longitude = -117.1138, Label = "Colton Ave & Brockton Ave" },

                // University St (lat approx 34.069-34.071)
                new() { Id = 33, Latitude = 34.0694, Longitude = -117.2231, Label = "University St & Orange Ave" },
                new() { Id = 34, Latitude = 34.0696, Longitude = -117.2079, Label = "University St & Church St" },
                new() { Id = 35, Latitude = 34.0697, Longitude = -117.1960, Label = "University St & State St" },
                new() { Id = 36, Latitude = 34.0698, Longitude = -117.1837, Label = "University St & 5th St" },
                new() { Id = 37, Latitude = 34.0698, Longitude = -117.1715, Label = "University St & 9th St" },
                new() { Id = 38, Latitude = 34.0697, Longitude = -117.1594, Label = "University St & Brookside Ave" },
                new() { Id = 39, Latitude = 34.0694, Longitude = -117.1370, Label = "University St & Tennessee St" },
                new() { Id = 40, Latitude = 34.0692, Longitude = -117.1141, Label = "University St & Brockton Ave" },

                // Highland Ave (far north arterial, lat approx 34.124-34.126)
                new() { Id = 41, Latitude = 34.1247, Longitude = -117.2099, Label = "Highland Ave & Church St" },
                new() { Id = 42, Latitude = 34.1248, Longitude = -117.1977, Label = "Highland Ave & State St" },
                new() { Id = 43, Latitude = 34.1249, Longitude = -117.1854, Label = "Highland Ave & 5th St" },
                new() { Id = 44, Latitude = 34.1249, Longitude = -117.1732, Label = "Highland Ave & 9th St" },
                new() { Id = 45, Latitude = 34.1248, Longitude = -117.1611, Label = "Highland Ave & Brookside Ave" },

                // Cypress Ave mid-city connectors (lat approx 34.094-34.096)
                new() { Id = 46, Latitude = 34.0948, Longitude = -117.2076, Label = "Cypress Ave & Church St" },
                new() { Id = 47, Latitude = 34.0949, Longitude = -117.1957, Label = "Cypress Ave & State St" },
                new() { Id = 48, Latitude = 34.0950, Longitude = -117.1834, Label = "Cypress Ave & 5th St" },
                new() { Id = 49, Latitude = 34.0950, Longitude = -117.1712, Label = "Cypress Ave & 9th St" },
                new() { Id = 50, Latitude = 34.0948, Longitude = -117.1591, Label = "Cypress Ave & Brookside Ave" },

                // Esri HQ
                new() { Id = 51, Latitude = 34.0567, Longitude = -117.1957, Label = "Esri HQ - 380 New York St" },

                // I-10 interchange ramps (Tippecanoe Ave & Mountain View Ave)
                new() { Id = 52, Latitude = 34.0502, Longitude = -117.2432, Label = "I-10 @ Tippecanoe Ave (WB off-ramp)" },
                new() { Id = 53, Latitude = 34.0508, Longitude = -117.2418, Label = "I-10 @ Tippecanoe Ave (EB on-ramp)" },
                new() { Id = 54, Latitude = 34.0514, Longitude = -117.2314, Label = "I-10 @ Mountain View Ave (WB off-ramp)" },
                new() { Id = 55, Latitude = 34.0519, Longitude = -117.2298, Label = "I-10 @ Mountain View Ave (EB on-ramp)" },
            };

            var edges = new List<GraphEdgeDto>
            {
                // Barton Rd east-west
                new() { FromNodeId =  1, ToNodeId =  2, Cost = Km( 1,  2, nodes), Bidirectional = true },
                new() { FromNodeId =  2, ToNodeId =  3, Cost = Km( 2,  3, nodes), Bidirectional = true },
                new() { FromNodeId =  3, ToNodeId =  4, Cost = Km( 3,  4, nodes), Bidirectional = true },
                new() { FromNodeId =  4, ToNodeId =  5, Cost = Km( 4,  5, nodes), Bidirectional = true },
                new() { FromNodeId =  5, ToNodeId =  6, Cost = Km( 5,  6, nodes), Bidirectional = true },
                new() { FromNodeId =  6, ToNodeId =  7, Cost = Km( 6,  7, nodes), Bidirectional = true },
                new() { FromNodeId =  7, ToNodeId =  8, Cost = Km( 7,  8, nodes), Bidirectional = true },

                // Lugonia Ave east-west
                new() { FromNodeId =  9, ToNodeId = 10, Cost = Km( 9, 10, nodes), Bidirectional = true },
                new() { FromNodeId = 10, ToNodeId = 11, Cost = Km(10, 11, nodes), Bidirectional = true },
                new() { FromNodeId = 11, ToNodeId = 12, Cost = Km(11, 12, nodes), Bidirectional = true },
                new() { FromNodeId = 12, ToNodeId = 13, Cost = Km(12, 13, nodes), Bidirectional = true },
                new() { FromNodeId = 13, ToNodeId = 14, Cost = Km(13, 14, nodes), Bidirectional = true },
                new() { FromNodeId = 14, ToNodeId = 15, Cost = Km(14, 15, nodes), Bidirectional = true },
                new() { FromNodeId = 15, ToNodeId = 16, Cost = Km(15, 16, nodes), Bidirectional = true },

                // New York St east-west
                new() { FromNodeId = 17, ToNodeId = 18, Cost = Km(17, 18, nodes), Bidirectional = true },
                new() { FromNodeId = 18, ToNodeId = 19, Cost = Km(18, 19, nodes), Bidirectional = true },
                new() { FromNodeId = 19, ToNodeId = 20, Cost = Km(19, 20, nodes), Bidirectional = true },
                new() { FromNodeId = 20, ToNodeId = 21, Cost = Km(20, 21, nodes), Bidirectional = true },
                new() { FromNodeId = 21, ToNodeId = 22, Cost = Km(21, 22, nodes), Bidirectional = true },
                new() { FromNodeId = 22, ToNodeId = 23, Cost = Km(22, 23, nodes), Bidirectional = true },
                new() { FromNodeId = 23, ToNodeId = 24, Cost = Km(23, 24, nodes), Bidirectional = true },
                // New York St through Esri HQ (node 51 sits between State St and 5th St)
                new() { FromNodeId = 19, ToNodeId = 51, Cost = Km(19, 51, nodes), Bidirectional = true },
                new() { FromNodeId = 51, ToNodeId = 20, Cost = Km(51, 20, nodes), Bidirectional = true },

                // Colton Ave east-west
                new() { FromNodeId = 25, ToNodeId = 26, Cost = Km(25, 26, nodes), Bidirectional = true },
                new() { FromNodeId = 26, ToNodeId = 27, Cost = Km(26, 27, nodes), Bidirectional = true },
                new() { FromNodeId = 27, ToNodeId = 28, Cost = Km(27, 28, nodes), Bidirectional = true },
                new() { FromNodeId = 28, ToNodeId = 29, Cost = Km(28, 29, nodes), Bidirectional = true },
                new() { FromNodeId = 29, ToNodeId = 30, Cost = Km(29, 30, nodes), Bidirectional = true },
                new() { FromNodeId = 30, ToNodeId = 31, Cost = Km(30, 31, nodes), Bidirectional = true },
                new() { FromNodeId = 31, ToNodeId = 32, Cost = Km(31, 32, nodes), Bidirectional = true },

                // University St east-west
                new() { FromNodeId = 33, ToNodeId = 34, Cost = Km(33, 34, nodes), Bidirectional = true },
                new() { FromNodeId = 34, ToNodeId = 35, Cost = Km(34, 35, nodes), Bidirectional = true },
                new() { FromNodeId = 35, ToNodeId = 36, Cost = Km(35, 36, nodes), Bidirectional = true },
                new() { FromNodeId = 36, ToNodeId = 37, Cost = Km(36, 37, nodes), Bidirectional = true },
                new() { FromNodeId = 37, ToNodeId = 38, Cost = Km(37, 38, nodes), Bidirectional = true },
                new() { FromNodeId = 38, ToNodeId = 39, Cost = Km(38, 39, nodes), Bidirectional = true },
                new() { FromNodeId = 39, ToNodeId = 40, Cost = Km(39, 40, nodes), Bidirectional = true },

                // Highland Ave east-west
                new() { FromNodeId = 41, ToNodeId = 42, Cost = Km(41, 42, nodes), Bidirectional = true },
                new() { FromNodeId = 42, ToNodeId = 43, Cost = Km(42, 43, nodes), Bidirectional = true },
                new() { FromNodeId = 43, ToNodeId = 44, Cost = Km(43, 44, nodes), Bidirectional = true },
                new() { FromNodeId = 44, ToNodeId = 45, Cost = Km(44, 45, nodes), Bidirectional = true },

                // Cypress Ave east-west
                new() { FromNodeId = 46, ToNodeId = 47, Cost = Km(46, 47, nodes), Bidirectional = true },
                new() { FromNodeId = 47, ToNodeId = 48, Cost = Km(47, 48, nodes), Bidirectional = true },
                new() { FromNodeId = 48, ToNodeId = 49, Cost = Km(48, 49, nodes), Bidirectional = true },
                new() { FromNodeId = 49, ToNodeId = 50, Cost = Km(49, 50, nodes), Bidirectional = true },

                // Orange Ave north-south
                new() { FromNodeId =  1, ToNodeId =  9, Cost = Km( 1,  9, nodes), Bidirectional = true },
                new() { FromNodeId =  9, ToNodeId = 17, Cost = Km( 9, 17, nodes), Bidirectional = true },
                new() { FromNodeId = 17, ToNodeId = 25, Cost = Km(17, 25, nodes), Bidirectional = true },
                new() { FromNodeId = 25, ToNodeId = 33, Cost = Km(25, 33, nodes), Bidirectional = true },

                // Church St north-south
                new() { FromNodeId =  2, ToNodeId = 10, Cost = Km( 2, 10, nodes), Bidirectional = true },
                new() { FromNodeId = 10, ToNodeId = 18, Cost = Km(10, 18, nodes), Bidirectional = true },
                new() { FromNodeId = 18, ToNodeId = 26, Cost = Km(18, 26, nodes), Bidirectional = true },
                new() { FromNodeId = 26, ToNodeId = 34, Cost = Km(26, 34, nodes), Bidirectional = true },
                new() { FromNodeId = 34, ToNodeId = 46, Cost = Km(34, 46, nodes), Bidirectional = true },
                new() { FromNodeId = 46, ToNodeId = 41, Cost = Km(46, 41, nodes), Bidirectional = true },

                // State St north-south
                new() { FromNodeId =  3, ToNodeId = 11, Cost = Km( 3, 11, nodes), Bidirectional = true },
                new() { FromNodeId = 11, ToNodeId = 19, Cost = Km(11, 19, nodes), Bidirectional = true },
                new() { FromNodeId = 19, ToNodeId = 27, Cost = Km(19, 27, nodes), Bidirectional = true },
                new() { FromNodeId = 27, ToNodeId = 35, Cost = Km(27, 35, nodes), Bidirectional = true },
                new() { FromNodeId = 35, ToNodeId = 47, Cost = Km(35, 47, nodes), Bidirectional = true },
                new() { FromNodeId = 47, ToNodeId = 42, Cost = Km(47, 42, nodes), Bidirectional = true },

                // 5th St north-south
                new() { FromNodeId =  4, ToNodeId = 12, Cost = Km( 4, 12, nodes), Bidirectional = true },
                new() { FromNodeId = 12, ToNodeId = 20, Cost = Km(12, 20, nodes), Bidirectional = true },
                new() { FromNodeId = 20, ToNodeId = 28, Cost = Km(20, 28, nodes), Bidirectional = true },
                new() { FromNodeId = 28, ToNodeId = 36, Cost = Km(28, 36, nodes), Bidirectional = true },
                new() { FromNodeId = 36, ToNodeId = 48, Cost = Km(36, 48, nodes), Bidirectional = true },
                new() { FromNodeId = 48, ToNodeId = 43, Cost = Km(48, 43, nodes), Bidirectional = true },

                // 9th St north-south
                new() { FromNodeId =  5, ToNodeId = 13, Cost = Km( 5, 13, nodes), Bidirectional = true },
                new() { FromNodeId = 13, ToNodeId = 21, Cost = Km(13, 21, nodes), Bidirectional = true },
                new() { FromNodeId = 21, ToNodeId = 29, Cost = Km(21, 29, nodes), Bidirectional = true },
                new() { FromNodeId = 29, ToNodeId = 37, Cost = Km(29, 37, nodes), Bidirectional = true },
                new() { FromNodeId = 37, ToNodeId = 49, Cost = Km(37, 49, nodes), Bidirectional = true },
                new() { FromNodeId = 49, ToNodeId = 44, Cost = Km(49, 44, nodes), Bidirectional = true },

                // Brookside Ave north-south
                new() { FromNodeId =  6, ToNodeId = 14, Cost = Km( 6, 14, nodes), Bidirectional = true },
                new() { FromNodeId = 14, ToNodeId = 22, Cost = Km(14, 22, nodes), Bidirectional = true },
                new() { FromNodeId = 22, ToNodeId = 30, Cost = Km(22, 30, nodes), Bidirectional = true },
                new() { FromNodeId = 30, ToNodeId = 38, Cost = Km(30, 38, nodes), Bidirectional = true },
                new() { FromNodeId = 38, ToNodeId = 50, Cost = Km(38, 50, nodes), Bidirectional = true },
                new() { FromNodeId = 50, ToNodeId = 45, Cost = Km(50, 45, nodes), Bidirectional = true },

                // Tennessee St north-south
                new() { FromNodeId =  7, ToNodeId = 15, Cost = Km( 7, 15, nodes), Bidirectional = true },
                new() { FromNodeId = 15, ToNodeId = 23, Cost = Km(15, 23, nodes), Bidirectional = true },
                new() { FromNodeId = 23, ToNodeId = 31, Cost = Km(23, 31, nodes), Bidirectional = true },
                new() { FromNodeId = 31, ToNodeId = 39, Cost = Km(31, 39, nodes), Bidirectional = true },

                // Brockton Ave north-south
                new() { FromNodeId =  8, ToNodeId = 16, Cost = Km( 8, 16, nodes), Bidirectional = true },
                new() { FromNodeId = 16, ToNodeId = 24, Cost = Km(16, 24, nodes), Bidirectional = true },
                new() { FromNodeId = 24, ToNodeId = 32, Cost = Km(24, 32, nodes), Bidirectional = true },
                new() { FromNodeId = 32, ToNodeId = 40, Cost = Km(32, 40, nodes), Bidirectional = true },

                // I-10 ramp chain
                new() { FromNodeId = 52, ToNodeId = 53, Cost = 0.16, Bidirectional = false },
                new() { FromNodeId = 53, ToNodeId = 54, Cost = 0.13, Bidirectional = false },
                new() { FromNodeId = 54, ToNodeId = 55, Cost = 0.15, Bidirectional = false },
                new() { FromNodeId = 55, ToNodeId = 10, Cost = 0.89, Bidirectional = false },
                new() { FromNodeId = 10, ToNodeId = 53, Cost = 0.91, Bidirectional = false },
                new() { FromNodeId = 52, ToNodeId = 11, Cost = 1.02, Bidirectional = false },
                new() { FromNodeId = 54, ToNodeId =  9, Cost = 1.14, Bidirectional = true  },

                // Esri HQ direct shortcut to Colton Ave & State St
                new() { FromNodeId = 51, ToNodeId = 27, Cost = Km(51, 27, nodes), Bidirectional = true },
            };

            return new RoadGraphDto
            {
                GraphName         = "Redlands, CA - 55-Node Real-Intersection Network",
                DestinationNodeId = EsriHqNodeId,
                Nodes             = nodes,
                Edges             = edges
            };
        }

        // Haversine distance in km between two nodes x 1.3 detour factor, rounded 2dp.
        private static double Km(int aId, int bId, List<GraphNodeDto> nodes)
        {
            var a = nodes.First(n => n.Id == aId);
            var b = nodes.First(n => n.Id == bId);
            return HaversineApprox(a.Latitude, a.Longitude, b.Latitude, b.Longitude);
        }

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
