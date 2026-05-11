using Portfolio.Common.DTOs;

namespace Portfolio.Services.Data
{
    // Real-intersection Redlands, CA road network — 175-node edition.
    // Coordinates sourced from OpenStreetMap, Caltrans Cal-NExUS, and authoritative geocoding.
    // Edge costs are haversine distance × 1.3 detour factor (km), rounded to 2 dp.
    //
    // Node 51        = Esri HQ (380 New York St, Redlands, CA 92373)
    // Nodes 52–55    = I-10 ramps at Tippecanoe Ave and Mountain View Ave
    // Nodes 100–111  = I-10 interchanges (California St → Ford St / Redlands Blvd)
    // Nodes 112–117  = SR-210 Foothill Freeway mainline
    // Nodes 118–120  = SR-38 / Orange St (Big Bear route)
    // Nodes 156–175  = Ford St corridor, Wabash Ave, Citrus Ave, E Redlands Blvd
    //
    // Total: 175 nodes, ~310 edges.
    internal static class RedlandsRoadNetwork
    {
        public const int EsriHqNodeId = 51;

        public static RoadGraphDto Build()
        {
            var nodes = new List<GraphNodeDto>
            {
                // ── Barton Rd ────────────────────────────────────────────────────────────
                new() { Id =   1, Latitude = 34.0387, Longitude = -117.2193, Label = "Barton Rd & Orange St" },
                new() { Id =   2, Latitude = 34.0390, Longitude = -117.2069, Label = "Barton Rd & Church St" },
                new() { Id =   3, Latitude = 34.0393, Longitude = -117.1960, Label = "Barton Rd & State St" },
                new() { Id =   4, Latitude = 34.0393, Longitude = -117.1833, Label = "Barton Rd & Alabama St" },
                new() { Id =   5, Latitude = 34.0391, Longitude = -117.1710, Label = "Barton Rd & Tennessee St (S)" },
                new() { Id =   6, Latitude = 34.0389, Longitude = -117.1590, Label = "Barton Rd & Brookside Ave" },
                new() { Id =   7, Latitude = 34.0386, Longitude = -117.1360, Label = "Barton Rd & Tennessee St" },
                new() { Id =   8, Latitude = 34.0384, Longitude = -117.1132, Label = "Barton Rd & Brockton Ave" },
                // ── Lugonia Ave ──────────────────────────────────────────────────────────
                new() { Id =   9, Latitude = 34.0447, Longitude = -117.2196, Label = "Lugonia Ave & Orange St" },
                new() { Id =  10, Latitude = 34.0448, Longitude = -117.2070, Label = "Lugonia Ave & Church St" },
                new() { Id =  11, Latitude = 34.0449, Longitude = -117.1960, Label = "Lugonia Ave & State St" },
                new() { Id =  12, Latitude = 34.0449, Longitude = -117.1833, Label = "Lugonia Ave & Alabama St" },
                new() { Id =  13, Latitude = 34.0448, Longitude = -117.1710, Label = "Lugonia Ave & Tennessee St (S)" },
                new() { Id =  14, Latitude = 34.0447, Longitude = -117.1590, Label = "Lugonia Ave & Brookside Ave" },
                new() { Id =  15, Latitude = 34.0445, Longitude = -117.1362, Label = "Lugonia Ave & Tennessee St" },
                new() { Id =  16, Latitude = 34.0443, Longitude = -117.1133, Label = "Lugonia Ave & Brockton Ave" },
                // ── New York St ──────────────────────────────────────────────────────────
                new() { Id =  17, Latitude = 34.0558, Longitude = -117.2199, Label = "New York St & Orange St" },
                new() { Id =  18, Latitude = 34.0559, Longitude = -117.2073, Label = "New York St & Church St" },
                new() { Id =  19, Latitude = 34.0560, Longitude = -117.1960, Label = "New York St & State St" },
                new() { Id =  20, Latitude = 34.0561, Longitude = -117.1833, Label = "New York St & Alabama St" },
                new() { Id =  21, Latitude = 34.0561, Longitude = -117.1710, Label = "New York St & Tennessee St (S)" },
                new() { Id =  22, Latitude = 34.0560, Longitude = -117.1590, Label = "New York St & Brookside Ave" },
                new() { Id =  23, Latitude = 34.0558, Longitude = -117.1364, Label = "New York St & Tennessee St" },
                new() { Id =  24, Latitude = 34.0556, Longitude = -117.1136, Label = "New York St & Brockton Ave" },
                // ── Colton Ave ───────────────────────────────────────────────────────────
                new() { Id =  25, Latitude = 34.0628, Longitude = -117.2202, Label = "Colton Ave & Orange St" },
                new() { Id =  26, Latitude = 34.0629, Longitude = -117.2076, Label = "Colton Ave & Church St" },
                new() { Id =  27, Latitude = 34.0630, Longitude = -117.1960, Label = "Colton Ave & State St" },
                new() { Id =  28, Latitude = 34.0631, Longitude = -117.1834, Label = "Colton Ave & Alabama St" },
                new() { Id =  29, Latitude = 34.0630, Longitude = -117.1712, Label = "Colton Ave & Tennessee St (S)" },
                new() { Id =  30, Latitude = 34.0629, Longitude = -117.1591, Label = "Colton Ave & Brookside Ave" },
                new() { Id =  31, Latitude = 34.0627, Longitude = -117.1367, Label = "Colton Ave & Tennessee St" },
                new() { Id =  32, Latitude = 34.0625, Longitude = -117.1138, Label = "Colton Ave & Brockton Ave" },
                // ── University St ────────────────────────────────────────────────────────
                new() { Id =  33, Latitude = 34.0694, Longitude = -117.2205, Label = "University St & Orange St" },
                new() { Id =  34, Latitude = 34.0696, Longitude = -117.2079, Label = "University St & Church St" },
                new() { Id =  35, Latitude = 34.0697, Longitude = -117.1960, Label = "University St & State St" },
                new() { Id =  36, Latitude = 34.0698, Longitude = -117.1837, Label = "University St & Alabama St" },
                new() { Id =  37, Latitude = 34.0698, Longitude = -117.1715, Label = "University St & Tennessee St (S)" },
                new() { Id =  38, Latitude = 34.0697, Longitude = -117.1594, Label = "University St & Brookside Ave" },
                new() { Id =  39, Latitude = 34.0694, Longitude = -117.1370, Label = "University St & Tennessee St" },
                new() { Id =  40, Latitude = 34.0692, Longitude = -117.1141, Label = "University St & Brockton Ave" },
                // ── Highland Ave ─────────────────────────────────────────────────────────
                new() { Id =  41, Latitude = 34.1247, Longitude = -117.2099, Label = "Highland Ave & Church St" },
                new() { Id =  42, Latitude = 34.1248, Longitude = -117.1977, Label = "Highland Ave & State St" },
                new() { Id =  43, Latitude = 34.1249, Longitude = -117.1854, Label = "Highland Ave & Alabama St" },
                new() { Id =  44, Latitude = 34.1249, Longitude = -117.1732, Label = "Highland Ave & Tennessee St (S)" },
                new() { Id =  45, Latitude = 34.1248, Longitude = -117.1611, Label = "Highland Ave & Brookside Ave" },
                // ── Cypress Ave ──────────────────────────────────────────────────────────
                new() { Id =  46, Latitude = 34.0948, Longitude = -117.2076, Label = "Cypress Ave & Church St" },
                new() { Id =  47, Latitude = 34.0949, Longitude = -117.1957, Label = "Cypress Ave & State St" },
                new() { Id =  48, Latitude = 34.0950, Longitude = -117.1834, Label = "Cypress Ave & Alabama St" },
                new() { Id =  49, Latitude = 34.0950, Longitude = -117.1712, Label = "Cypress Ave & Tennessee St (S)" },
                new() { Id =  50, Latitude = 34.0948, Longitude = -117.1591, Label = "Cypress Ave & Brookside Ave" },
                // ── Special ──────────────────────────────────────────────────────────────
                new() { Id =  51, Latitude = 34.0565, Longitude = -117.1950, Label = "Esri HQ - 380 New York St" },
                // ── I-10 ramps: Tippecanoe Ave and Mountain View Ave ─────────────────────
                new() { Id =  52, Latitude = 34.0448, Longitude = -117.2428, Label = "I-10 @ Tippecanoe Ave (WB off-ramp)" },
                new() { Id =  53, Latitude = 34.0443, Longitude = -117.2412, Label = "I-10 @ Tippecanoe Ave (EB on-ramp)" },
                new() { Id =  54, Latitude = 34.0451, Longitude = -117.2251, Label = "I-10 @ Mountain View Ave (WB off-ramp)" },
                new() { Id =  55, Latitude = 34.0447, Longitude = -117.2238, Label = "I-10 @ Mountain View Ave (EB on-ramp)" },
                // ── W Redlands Blvd ──────────────────────────────────────────────────────
                new() { Id =  56, Latitude = 34.0515, Longitude = -117.2201, Label = "W Redlands Blvd & Orange St" },
                new() { Id =  57, Latitude = 34.0516, Longitude = -117.2075, Label = "W Redlands Blvd & Church St" },
                new() { Id =  58, Latitude = 34.0517, Longitude = -117.1960, Label = "W Redlands Blvd & State St" },
                new() { Id =  59, Latitude = 34.0517, Longitude = -117.1833, Label = "W Redlands Blvd & Alabama St" },
                new() { Id =  60, Latitude = 34.0516, Longitude = -117.1711, Label = "W Redlands Blvd & Tennessee St (S)" },
                new() { Id =  61, Latitude = 34.0515, Longitude = -117.1590, Label = "W Redlands Blvd & Brookside Ave" },
                new() { Id =  62, Latitude = 34.0513, Longitude = -117.1366, Label = "W Redlands Blvd & Tennessee St" },
                new() { Id =  63, Latitude = 34.0511, Longitude = -117.1137, Label = "W Redlands Blvd & Brockton Ave" },
                // ── Stuart Ave ───────────────────────────────────────────────────────────
                new() { Id =  64, Latitude = 34.0651, Longitude = -117.2200, Label = "Stuart Ave & Orange St" },
                new() { Id =  65, Latitude = 34.0652, Longitude = -117.2077, Label = "Stuart Ave & Church St" },
                new() { Id =  66, Latitude = 34.0653, Longitude = -117.1958, Label = "Stuart Ave & State St" },
                new() { Id =  67, Latitude = 34.0654, Longitude = -117.1835, Label = "Stuart Ave & Alabama St" },
                new() { Id =  68, Latitude = 34.0654, Longitude = -117.1713, Label = "Stuart Ave & Tennessee St (S)" },
                new() { Id =  69, Latitude = 34.0653, Longitude = -117.1592, Label = "Stuart Ave & Brookside Ave" },
                // ── San Bernardino Ave ───────────────────────────────────────────────────
                new() { Id =  70, Latitude = 34.0760, Longitude = -117.2078, Label = "San Bernardino Ave & Church St" },
                new() { Id =  71, Latitude = 34.0761, Longitude = -117.1959, Label = "San Bernardino Ave & State St" },
                new() { Id =  72, Latitude = 34.0762, Longitude = -117.1836, Label = "San Bernardino Ave & Alabama St" },
                new() { Id =  73, Latitude = 34.0762, Longitude = -117.1714, Label = "San Bernardino Ave & Tennessee St (S)" },
                new() { Id =  74, Latitude = 34.0761, Longitude = -117.1593, Label = "San Bernardino Ave & Brookside Ave" },
                new() { Id =  75, Latitude = 34.0758, Longitude = -117.1369, Label = "San Bernardino Ave & Tennessee St" },
                new() { Id =  76, Latitude = 34.0756, Longitude = -117.1140, Label = "San Bernardino Ave & Brockton Ave" },
                // ── Base Line Rd ─────────────────────────────────────────────────────────
                new() { Id =  77, Latitude = 34.1083, Longitude = -117.2077, Label = "Base Line Rd & Church St" },
                new() { Id =  78, Latitude = 34.1084, Longitude = -117.1958, Label = "Base Line Rd & State St" },
                new() { Id =  79, Latitude = 34.1085, Longitude = -117.1835, Label = "Base Line Rd & Alabama St" },
                new() { Id =  80, Latitude = 34.1085, Longitude = -117.1713, Label = "Base Line Rd & Tennessee St (S)" },
                new() { Id =  81, Latitude = 34.1084, Longitude = -117.1592, Label = "Base Line Rd & Brookside Ave" },
                // ── Texas St ─────────────────────────────────────────────────────────────
                new() { Id =  82, Latitude = 34.0391, Longitude = -117.1887, Label = "Barton Rd & Texas St" },
                new() { Id =  83, Latitude = 34.0449, Longitude = -117.1888, Label = "Lugonia Ave & Texas St" },
                new() { Id =  84, Latitude = 34.0517, Longitude = -117.1889, Label = "W Redlands Blvd & Texas St" },
                new() { Id =  85, Latitude = 34.0561, Longitude = -117.1889, Label = "New York St & Texas St" },
                new() { Id =  86, Latitude = 34.0631, Longitude = -117.1889, Label = "Colton Ave & Texas St" },
                new() { Id =  87, Latitude = 34.0654, Longitude = -117.1889, Label = "Stuart Ave & Texas St" },
                new() { Id =  88, Latitude = 34.0698, Longitude = -117.1889, Label = "University St & Texas St" },
                // ── Eureka St ────────────────────────────────────────────────────────────
                new() { Id =  89, Latitude = 34.0389, Longitude = -117.1612, Label = "Barton Rd & Eureka St" },
                new() { Id =  90, Latitude = 34.0447, Longitude = -117.1612, Label = "Lugonia Ave & Eureka St" },
                new() { Id =  91, Latitude = 34.0515, Longitude = -117.1612, Label = "W Redlands Blvd & Eureka St" },
                new() { Id =  92, Latitude = 34.0559, Longitude = -117.1613, Label = "New York St & Eureka St" },
                new() { Id =  93, Latitude = 34.0628, Longitude = -117.1614, Label = "Colton Ave & Eureka St" },
                new() { Id =  94, Latitude = 34.0694, Longitude = -117.1615, Label = "University St & Eureka St" },
                // ── California St ────────────────────────────────────────────────────────
                new() { Id =  95, Latitude = 34.0390, Longitude = -117.2101, Label = "Barton Rd & California St" },
                new() { Id =  96, Latitude = 34.0448, Longitude = -117.2101, Label = "Lugonia Ave & California St" },
                new() { Id =  97, Latitude = 34.0516, Longitude = -117.2101, Label = "W Redlands Blvd & California St" },
                new() { Id =  98, Latitude = 34.0559, Longitude = -117.2101, Label = "New York St & California St" },
                new() { Id =  99, Latitude = 34.0629, Longitude = -117.2101, Label = "Colton Ave & California St" },
                // ── I-10 interchange chain ────────────────────────────────────────────────
                new() { Id = 100, Latitude = 34.0461, Longitude = -117.2098, Label = "I-10 @ California St (WB off-ramp)" },
                new() { Id = 101, Latitude = 34.0457, Longitude = -117.2071, Label = "I-10 @ California St (EB on-ramp)" },
                new() { Id = 102, Latitude = 34.0462, Longitude = -117.1893, Label = "I-10 @ Alabama St (WB off-ramp)" },
                new() { Id = 103, Latitude = 34.0457, Longitude = -117.1867, Label = "I-10 @ Alabama St (EB on-ramp)" },
                new() { Id = 104, Latitude = 34.0463, Longitude = -117.1805, Label = "I-10 @ Tennessee St / SR-210 (WB)" },
                new() { Id = 105, Latitude = 34.0458, Longitude = -117.1778, Label = "I-10 @ Tennessee St / SR-210 (EB)" },
                new() { Id = 106, Latitude = 34.0462, Longitude = -117.1601, Label = "I-10 @ SR-38 / Orange St (WB off-ramp)" },
                new() { Id = 107, Latitude = 34.0457, Longitude = -117.1573, Label = "I-10 @ SR-38 / Orange St (EB on-ramp)" },
                new() { Id = 108, Latitude = 34.0461, Longitude = -117.1418, Label = "I-10 @ University St (WB off-ramp)" },
                new() { Id = 109, Latitude = 34.0456, Longitude = -117.1390, Label = "I-10 @ University St (EB on-ramp)" },
                new() { Id = 110, Latitude = 34.0459, Longitude = -117.1185, Label = "I-10 @ Ford St / Redlands Blvd (WB off-ramp)" },
                new() { Id = 111, Latitude = 34.0454, Longitude = -117.1155, Label = "I-10 @ Ford St / Redlands Blvd (EB on-ramp)" },
                // ── SR-210 Foothill Freeway ───────────────────────────────────────────────
                new() { Id = 112, Latitude = 34.0718, Longitude = -117.1706, Label = "SR-210 @ Tennessee St on-ramp (EB)" },
                new() { Id = 113, Latitude = 34.0724, Longitude = -117.1711, Label = "SR-210 @ Tennessee St off-ramp (WB)" },
                new() { Id = 114, Latitude = 34.0755, Longitude = -117.1586, Label = "SR-210 @ Brookside Ave interchange" },
                new() { Id = 115, Latitude = 34.0820, Longitude = -117.1435, Label = "SR-210 mainline (NE corridor)" },
                new() { Id = 116, Latitude = 34.0914, Longitude = -117.1380, Label = "SR-210 @ Greenspot Rd interchange" },
                new() { Id = 117, Latitude = 34.1020, Longitude = -117.1270, Label = "SR-210 → SR-330 / Highland junction" },
                // ── SR-38 / Orange St corridor ────────────────────────────────────────────
                new() { Id = 118, Latitude = 34.0552, Longitude = -117.1960, Label = "SR-38 & State St (downtown Redlands)" },
                new() { Id = 119, Latitude = 34.0550, Longitude = -117.1710, Label = "SR-38 & Tennessee St (S) (east Redlands)" },
                new() { Id = 120, Latitude = 34.0548, Longitude = -117.1360, Label = "SR-38 & Tennessee St (east exit)" },
                // ── Downtown fine detail ─────────────────────────────────────────────────
                new() { Id = 121, Latitude = 34.0523, Longitude = -117.2077, Label = "Cajon St & Church St" },
                new() { Id = 122, Latitude = 34.0524, Longitude = -117.1957, Label = "Cajon St & State St" },
                new() { Id = 123, Latitude = 34.0524, Longitude = -117.1834, Label = "Cajon St & Alabama St" },
                new() { Id = 124, Latitude = 34.0523, Longitude = -117.1712, Label = "Cajon St & Tennessee St (S)" },
                new() { Id = 125, Latitude = 34.0522, Longitude = -117.1589, Label = "Cajon St & Brookside Ave" },
                new() { Id = 126, Latitude = 34.0540, Longitude = -117.1958, Label = "6th St & State St" },
                new() { Id = 127, Latitude = 34.0540, Longitude = -117.1835, Label = "6th St & Alabama St" },
                new() { Id = 128, Latitude = 34.0540, Longitude = -117.1889, Label = "6th St & Texas St" },
                new() { Id = 129, Latitude = 34.0590, Longitude = -117.2073, Label = "Olive Ave & Church St" },
                new() { Id = 130, Latitude = 34.0591, Longitude = -117.1955, Label = "Olive Ave & State St" },
                new() { Id = 131, Latitude = 34.0592, Longitude = -117.1833, Label = "Olive Ave & Alabama St" },
                new() { Id = 132, Latitude = 34.0482, Longitude = -117.2076, Label = "Center St & Church St" },
                new() { Id = 133, Latitude = 34.0483, Longitude = -117.1957, Label = "Center St & State St" },
                new() { Id = 134, Latitude = 34.0483, Longitude = -117.1834, Label = "Center St & Alabama St" },
                new() { Id = 135, Latitude = 34.0482, Longitude = -117.1712, Label = "Center St & Tennessee St (S)" },
                // ── Highland / Brockton extension ─────────────────────────────────────────
                new() { Id = 136, Latitude = 34.1246, Longitude = -117.1489, Label = "Highland Ave & Tennessee St" },
                new() { Id = 137, Latitude = 34.1245, Longitude = -117.1260, Label = "Highland Ave & Brockton Ave" },
                // ── Brockton Ave north extension ──────────────────────────────────────────
                new() { Id = 138, Latitude = 34.0948, Longitude = -117.1132, Label = "Cypress Ave & Brockton Ave" },
                new() { Id = 139, Latitude = 34.0852, Longitude = -117.1136, Label = "Brockton Ave (San Bernardino Ave to Cypress Ave)" },
                // ── East Redlands / Pioneer Ave ───────────────────────────────────────────
                new() { Id = 140, Latitude = 34.0389, Longitude = -117.0963, Label = "Barton Rd & Pioneer Ave" },
                new() { Id = 141, Latitude = 34.0446, Longitude = -117.0964, Label = "Lugonia Ave & Pioneer Ave" },
                new() { Id = 142, Latitude = 34.0558, Longitude = -117.0965, Label = "New York St & Pioneer Ave" },
                new() { Id = 143, Latitude = 34.0628, Longitude = -117.0966, Label = "Colton Ave & Pioneer Ave" },
                new() { Id = 144, Latitude = 34.0694, Longitude = -117.0967, Label = "University St & Pioneer Ave" },
                // ── I-10 @ Wabash Ave ─────────────────────────────────────────────────────
                new() { Id = 145, Latitude = 34.0458, Longitude = -117.0955, Label = "I-10 @ Wabash Ave (WB off-ramp)" },
                new() { Id = 146, Latitude = 34.0453, Longitude = -117.0927, Label = "I-10 @ Wabash Ave (EB on-ramp)" },
                // ── Ford St / I-10 surface link ───────────────────────────────────────────
                new() { Id = 147, Latitude = 34.0500, Longitude = -117.1179, Label = "Ford St & Brockton Ave (I-10 link)" },
                // ── West approach: Tippecanoe & Mountain View surface roads ───────────────
                new() { Id = 148, Latitude = 34.0450, Longitude = -117.2437, Label = "Lugonia Ave & Tippecanoe Ave" },
                new() { Id = 149, Latitude = 34.0452, Longitude = -117.2260, Label = "Lugonia Ave & Mountain View Ave" },
                new() { Id = 150, Latitude = 34.0387, Longitude = -117.2438, Label = "Barton Rd & Tippecanoe Ave" },
                new() { Id = 151, Latitude = 34.0388, Longitude = -117.2260, Label = "Barton Rd & Mountain View Ave" },
                new() { Id = 152, Latitude = 34.0558, Longitude = -117.2438, Label = "New York St & Tippecanoe Ave" },
                new() { Id = 153, Latitude = 34.0558, Longitude = -117.2260, Label = "New York St & Mountain View Ave" },
                new() { Id = 154, Latitude = 34.0628, Longitude = -117.2437, Label = "Colton Ave & Tippecanoe Ave" },
                new() { Id = 155, Latitude = 34.0628, Longitude = -117.2260, Label = "Colton Ave & Mountain View Ave" },
                // ── Ford St E-W corridor ──────────────────────────────────────────────────
                new() { Id = 156, Latitude = 34.0496, Longitude = -117.2076, Label = "Ford St & Church St" },
                new() { Id = 157, Latitude = 34.0497, Longitude = -117.1960, Label = "Ford St & State St" },
                new() { Id = 158, Latitude = 34.0497, Longitude = -117.1833, Label = "Ford St & Alabama St" },
                new() { Id = 159, Latitude = 34.0496, Longitude = -117.1710, Label = "Ford St & Tennessee St (S)" },
                new() { Id = 160, Latitude = 34.0495, Longitude = -117.1590, Label = "Ford St & Brookside Ave" },
                // ── Wabash Ave N-S column ─────────────────────────────────────────────────
                new() { Id = 161, Latitude = 34.0559, Longitude = -117.0963, Label = "New York St & Wabash Ave" },
                new() { Id = 162, Latitude = 34.0629, Longitude = -117.0963, Label = "Colton Ave & Wabash Ave" },
                new() { Id = 163, Latitude = 34.0695, Longitude = -117.0963, Label = "University St & Wabash Ave" },
                // ── SR-38 east corridor additional nodes ──────────────────────────────────
                new() { Id = 164, Latitude = 34.0547, Longitude = -117.1132, Label = "SR-38 & Brockton Ave" },
                new() { Id = 165, Latitude = 34.0544, Longitude = -117.0963, Label = "SR-38 & Wabash Ave (Mentone)" },
                // ── SR-210 Brookside WB surface ramp ──────────────────────────────────────
                new() { Id = 166, Latitude = 34.0728, Longitude = -117.1590, Label = "SR-210 @ Brookside Ave (WB off-ramp surface)" },
                // ── Lugonia Ave intermediate node ─────────────────────────────────────────
                new() { Id = 167, Latitude = 34.0448, Longitude = -117.2228, Label = "Lugonia Ave & Plympton Ave" },
                // ── E Redlands Blvd east extension ────────────────────────────────────────
                new() { Id = 169, Latitude = 34.0600, Longitude = -117.1369, Label = "E Redlands Blvd & Tennessee St" },
                new() { Id = 170, Latitude = 34.0600, Longitude = -117.1132, Label = "E Redlands Blvd & Brockton Ave" },
                new() { Id = 171, Latitude = 34.0600, Longitude = -117.0963, Label = "E Redlands Blvd & Wabash Ave" },
                // ── SR-330 south approach ─────────────────────────────────────────────────
                new() { Id = 172, Latitude = 34.1050, Longitude = -117.1170, Label = "SR-330 south approach (Brockton Ave)" },
                // ── Citrus Ave ────────────────────────────────────────────────────────────
                new() { Id = 174, Latitude = 34.0685, Longitude = -117.2077, Label = "Citrus Ave & Church St" },
                new() { Id = 175, Latitude = 34.0685, Longitude = -117.1960, Label = "Citrus Ave & State St" },
            };

            var edges = new List<GraphEdgeDto>
            {
                // ── Barton Rd east-west ───────────────────────────────────────────────────
                new() { FromNodeId = 150, ToNodeId = 151, Cost = Km(150, 151, nodes), Bidirectional = true },
                new() { FromNodeId = 151, ToNodeId =   1, Cost = Km(151,   1, nodes), Bidirectional = true },
                new() { FromNodeId =   1, ToNodeId =  95, Cost = Km(  1,  95, nodes), Bidirectional = true },
                new() { FromNodeId =  95, ToNodeId =   2, Cost = Km( 95,   2, nodes), Bidirectional = true },
                new() { FromNodeId =   2, ToNodeId =   3, Cost = Km(  2,   3, nodes), Bidirectional = true },
                new() { FromNodeId =   3, ToNodeId =  82, Cost = Km(  3,  82, nodes), Bidirectional = true },
                new() { FromNodeId =  82, ToNodeId =   4, Cost = Km( 82,   4, nodes), Bidirectional = true },
                new() { FromNodeId =   4, ToNodeId =   5, Cost = Km(  4,   5, nodes), Bidirectional = true },
                new() { FromNodeId =   5, ToNodeId =  89, Cost = Km(  5,  89, nodes), Bidirectional = true },
                new() { FromNodeId =  89, ToNodeId =   6, Cost = Km( 89,   6, nodes), Bidirectional = true },
                new() { FromNodeId =   6, ToNodeId =   7, Cost = Km(  6,   7, nodes), Bidirectional = true },
                new() { FromNodeId =   7, ToNodeId =   8, Cost = Km(  7,   8, nodes), Bidirectional = true },
                new() { FromNodeId =   8, ToNodeId = 140, Cost = Km(  8, 140, nodes), Bidirectional = true },
                // ── Lugonia Ave east-west ─────────────────────────────────────────────────
                new() { FromNodeId = 148, ToNodeId = 149, Cost = Km(148, 149, nodes), Bidirectional = true },
                new() { FromNodeId = 149, ToNodeId = 167, Cost = Km(149, 167, nodes), Bidirectional = true },
                new() { FromNodeId = 167, ToNodeId =   9, Cost = Km(167,   9, nodes), Bidirectional = true },
                new() { FromNodeId =   9, ToNodeId =  96, Cost = Km(  9,  96, nodes), Bidirectional = true },
                new() { FromNodeId =  96, ToNodeId =  10, Cost = Km( 96,  10, nodes), Bidirectional = true },
                new() { FromNodeId =  10, ToNodeId =  11, Cost = Km( 10,  11, nodes), Bidirectional = true },
                new() { FromNodeId =  11, ToNodeId =  83, Cost = Km( 11,  83, nodes), Bidirectional = true },
                new() { FromNodeId =  83, ToNodeId =  12, Cost = Km( 83,  12, nodes), Bidirectional = true },
                new() { FromNodeId =  12, ToNodeId =  13, Cost = Km( 12,  13, nodes), Bidirectional = true },
                new() { FromNodeId =  13, ToNodeId =  90, Cost = Km( 13,  90, nodes), Bidirectional = true },
                new() { FromNodeId =  90, ToNodeId =  14, Cost = Km( 90,  14, nodes), Bidirectional = true },
                new() { FromNodeId =  14, ToNodeId =  15, Cost = Km( 14,  15, nodes), Bidirectional = true },
                new() { FromNodeId =  15, ToNodeId =  16, Cost = Km( 15,  16, nodes), Bidirectional = true },
                new() { FromNodeId =  16, ToNodeId = 141, Cost = Km( 16, 141, nodes), Bidirectional = true },
                // ── Center St east-west ───────────────────────────────────────────────────
                new() { FromNodeId = 132, ToNodeId = 133, Cost = Km(132, 133, nodes), Bidirectional = true },
                new() { FromNodeId = 133, ToNodeId = 134, Cost = Km(133, 134, nodes), Bidirectional = true },
                new() { FromNodeId = 134, ToNodeId = 135, Cost = Km(134, 135, nodes), Bidirectional = true },
                // ── Cajon St east-west ────────────────────────────────────────────────────
                new() { FromNodeId = 121, ToNodeId = 122, Cost = Km(121, 122, nodes), Bidirectional = true },
                new() { FromNodeId = 122, ToNodeId = 123, Cost = Km(122, 123, nodes), Bidirectional = true },
                new() { FromNodeId = 123, ToNodeId = 124, Cost = Km(123, 124, nodes), Bidirectional = true },
                new() { FromNodeId = 124, ToNodeId = 125, Cost = Km(124, 125, nodes), Bidirectional = true },
                // ── 6th St east-west ──────────────────────────────────────────────────────
                new() { FromNodeId = 126, ToNodeId = 128, Cost = Km(126, 128, nodes), Bidirectional = true },
                new() { FromNodeId = 128, ToNodeId = 127, Cost = Km(128, 127, nodes), Bidirectional = true },
                // ── Ford St east-west ─────────────────────────────────────────────────────
                new() { FromNodeId = 156, ToNodeId = 157, Cost = Km(156, 157, nodes), Bidirectional = true },
                new() { FromNodeId = 157, ToNodeId = 158, Cost = Km(157, 158, nodes), Bidirectional = true },
                new() { FromNodeId = 158, ToNodeId = 159, Cost = Km(158, 159, nodes), Bidirectional = true },
                new() { FromNodeId = 159, ToNodeId = 160, Cost = Km(159, 160, nodes), Bidirectional = true },
                new() { FromNodeId = 160, ToNodeId = 147, Cost = Km(160, 147, nodes), Bidirectional = true },
                // ── W Redlands Blvd east-west ─────────────────────────────────────────────
                new() { FromNodeId =  56, ToNodeId =  97, Cost = Km( 56,  97, nodes), Bidirectional = true },
                new() { FromNodeId =  97, ToNodeId =  57, Cost = Km( 97,  57, nodes), Bidirectional = true },
                new() { FromNodeId =  57, ToNodeId =  58, Cost = Km( 57,  58, nodes), Bidirectional = true },
                new() { FromNodeId =  58, ToNodeId =  84, Cost = Km( 58,  84, nodes), Bidirectional = true },
                new() { FromNodeId =  84, ToNodeId =  59, Cost = Km( 84,  59, nodes), Bidirectional = true },
                new() { FromNodeId =  59, ToNodeId =  60, Cost = Km( 59,  60, nodes), Bidirectional = true },
                new() { FromNodeId =  60, ToNodeId =  91, Cost = Km( 60,  91, nodes), Bidirectional = true },
                new() { FromNodeId =  91, ToNodeId =  61, Cost = Km( 91,  61, nodes), Bidirectional = true },
                new() { FromNodeId =  61, ToNodeId =  62, Cost = Km( 61,  62, nodes), Bidirectional = true },
                new() { FromNodeId =  62, ToNodeId =  63, Cost = Km( 62,  63, nodes), Bidirectional = true },
                // ── New York St east-west ─────────────────────────────────────────────────
                new() { FromNodeId = 152, ToNodeId = 153, Cost = Km(152, 153, nodes), Bidirectional = true },
                new() { FromNodeId = 153, ToNodeId =  17, Cost = Km(153,  17, nodes), Bidirectional = true },
                new() { FromNodeId =  17, ToNodeId =  98, Cost = Km( 17,  98, nodes), Bidirectional = true },
                new() { FromNodeId =  98, ToNodeId =  18, Cost = Km( 98,  18, nodes), Bidirectional = true },
                new() { FromNodeId =  18, ToNodeId =  19, Cost = Km( 18,  19, nodes), Bidirectional = true },
                new() { FromNodeId =  19, ToNodeId =  51, Cost = Km( 19,  51, nodes), Bidirectional = true },
                new() { FromNodeId =  51, ToNodeId =  85, Cost = Km( 51,  85, nodes), Bidirectional = true },
                new() { FromNodeId =  85, ToNodeId =  20, Cost = Km( 85,  20, nodes), Bidirectional = true },
                new() { FromNodeId =  20, ToNodeId =  21, Cost = Km( 20,  21, nodes), Bidirectional = true },
                new() { FromNodeId =  21, ToNodeId =  92, Cost = Km( 21,  92, nodes), Bidirectional = true },
                new() { FromNodeId =  92, ToNodeId =  22, Cost = Km( 92,  22, nodes), Bidirectional = true },
                new() { FromNodeId =  22, ToNodeId =  23, Cost = Km( 22,  23, nodes), Bidirectional = true },
                new() { FromNodeId =  23, ToNodeId =  24, Cost = Km( 23,  24, nodes), Bidirectional = true },
                new() { FromNodeId =  24, ToNodeId = 142, Cost = Km( 24, 142, nodes), Bidirectional = true },
                new() { FromNodeId = 142, ToNodeId = 161, Cost = Km(142, 161, nodes), Bidirectional = true },
                // ── Olive Ave east-west ───────────────────────────────────────────────────
                new() { FromNodeId = 129, ToNodeId = 130, Cost = Km(129, 130, nodes), Bidirectional = true },
                new() { FromNodeId = 130, ToNodeId = 131, Cost = Km(130, 131, nodes), Bidirectional = true },
                // ── Colton Ave east-west ──────────────────────────────────────────────────
                new() { FromNodeId = 154, ToNodeId = 155, Cost = Km(154, 155, nodes), Bidirectional = true },
                new() { FromNodeId = 155, ToNodeId =  25, Cost = Km(155,  25, nodes), Bidirectional = true },
                new() { FromNodeId =  25, ToNodeId =  99, Cost = Km( 25,  99, nodes), Bidirectional = true },
                new() { FromNodeId =  99, ToNodeId =  26, Cost = Km( 99,  26, nodes), Bidirectional = true },
                new() { FromNodeId =  26, ToNodeId =  27, Cost = Km( 26,  27, nodes), Bidirectional = true },
                new() { FromNodeId =  27, ToNodeId =  86, Cost = Km( 27,  86, nodes), Bidirectional = true },
                new() { FromNodeId =  86, ToNodeId =  28, Cost = Km( 86,  28, nodes), Bidirectional = true },
                new() { FromNodeId =  28, ToNodeId =  29, Cost = Km( 28,  29, nodes), Bidirectional = true },
                new() { FromNodeId =  29, ToNodeId =  93, Cost = Km( 29,  93, nodes), Bidirectional = true },
                new() { FromNodeId =  93, ToNodeId =  30, Cost = Km( 93,  30, nodes), Bidirectional = true },
                new() { FromNodeId =  30, ToNodeId =  31, Cost = Km( 30,  31, nodes), Bidirectional = true },
                new() { FromNodeId =  31, ToNodeId =  32, Cost = Km( 31,  32, nodes), Bidirectional = true },
                new() { FromNodeId =  32, ToNodeId = 143, Cost = Km( 32, 143, nodes), Bidirectional = true },
                new() { FromNodeId = 143, ToNodeId = 162, Cost = Km(143, 162, nodes), Bidirectional = true },
                // ── Stuart Ave east-west ──────────────────────────────────────────────────
                new() { FromNodeId =  64, ToNodeId =  65, Cost = Km( 64,  65, nodes), Bidirectional = true },
                new() { FromNodeId =  65, ToNodeId =  66, Cost = Km( 65,  66, nodes), Bidirectional = true },
                new() { FromNodeId =  66, ToNodeId =  87, Cost = Km( 66,  87, nodes), Bidirectional = true },
                new() { FromNodeId =  87, ToNodeId =  67, Cost = Km( 87,  67, nodes), Bidirectional = true },
                new() { FromNodeId =  67, ToNodeId =  68, Cost = Km( 67,  68, nodes), Bidirectional = true },
                new() { FromNodeId =  68, ToNodeId =  69, Cost = Km( 68,  69, nodes), Bidirectional = true },
                // ── Citrus Ave east-west ──────────────────────────────────────────────────
                new() { FromNodeId = 174, ToNodeId = 175, Cost = Km(174, 175, nodes), Bidirectional = true },
                // ── University St east-west ───────────────────────────────────────────────
                new() { FromNodeId =  33, ToNodeId =  34, Cost = Km( 33,  34, nodes), Bidirectional = true },
                new() { FromNodeId =  34, ToNodeId =  35, Cost = Km( 34,  35, nodes), Bidirectional = true },
                new() { FromNodeId =  35, ToNodeId =  88, Cost = Km( 35,  88, nodes), Bidirectional = true },
                new() { FromNodeId =  88, ToNodeId =  36, Cost = Km( 88,  36, nodes), Bidirectional = true },
                new() { FromNodeId =  36, ToNodeId =  37, Cost = Km( 36,  37, nodes), Bidirectional = true },
                new() { FromNodeId =  37, ToNodeId =  94, Cost = Km( 37,  94, nodes), Bidirectional = true },
                new() { FromNodeId =  94, ToNodeId =  38, Cost = Km( 94,  38, nodes), Bidirectional = true },
                new() { FromNodeId =  38, ToNodeId =  39, Cost = Km( 38,  39, nodes), Bidirectional = true },
                new() { FromNodeId =  39, ToNodeId =  40, Cost = Km( 39,  40, nodes), Bidirectional = true },
                new() { FromNodeId =  40, ToNodeId = 144, Cost = Km( 40, 144, nodes), Bidirectional = true },
                new() { FromNodeId = 144, ToNodeId = 163, Cost = Km(144, 163, nodes), Bidirectional = true },
                // ── San Bernardino Ave east-west ──────────────────────────────────────────
                new() { FromNodeId =  70, ToNodeId =  71, Cost = Km( 70,  71, nodes), Bidirectional = true },
                new() { FromNodeId =  71, ToNodeId =  72, Cost = Km( 71,  72, nodes), Bidirectional = true },
                new() { FromNodeId =  72, ToNodeId =  73, Cost = Km( 72,  73, nodes), Bidirectional = true },
                new() { FromNodeId =  73, ToNodeId =  74, Cost = Km( 73,  74, nodes), Bidirectional = true },
                new() { FromNodeId =  74, ToNodeId =  75, Cost = Km( 74,  75, nodes), Bidirectional = true },
                new() { FromNodeId =  75, ToNodeId = 139, Cost = Km( 75, 139, nodes), Bidirectional = true },
                new() { FromNodeId = 139, ToNodeId =  76, Cost = Km(139,  76, nodes), Bidirectional = true },
                // ── Base Line Rd east-west ────────────────────────────────────────────────
                new() { FromNodeId =  77, ToNodeId =  78, Cost = Km( 77,  78, nodes), Bidirectional = true },
                new() { FromNodeId =  78, ToNodeId =  79, Cost = Km( 78,  79, nodes), Bidirectional = true },
                new() { FromNodeId =  79, ToNodeId =  80, Cost = Km( 79,  80, nodes), Bidirectional = true },
                new() { FromNodeId =  80, ToNodeId =  81, Cost = Km( 80,  81, nodes), Bidirectional = true },
                // ── Highland Ave east-west ────────────────────────────────────────────────
                new() { FromNodeId =  41, ToNodeId =  42, Cost = Km( 41,  42, nodes), Bidirectional = true },
                new() { FromNodeId =  42, ToNodeId =  43, Cost = Km( 42,  43, nodes), Bidirectional = true },
                new() { FromNodeId =  43, ToNodeId =  44, Cost = Km( 43,  44, nodes), Bidirectional = true },
                new() { FromNodeId =  44, ToNodeId =  45, Cost = Km( 44,  45, nodes), Bidirectional = true },
                new() { FromNodeId =  45, ToNodeId = 136, Cost = Km( 45, 136, nodes), Bidirectional = true },
                new() { FromNodeId = 136, ToNodeId = 137, Cost = Km(136, 137, nodes), Bidirectional = true },
                // ── E Redlands Blvd east extension ────────────────────────────────────────
                new() { FromNodeId =  63, ToNodeId = 169, Cost = Km( 63, 169, nodes), Bidirectional = true },
                new() { FromNodeId = 169, ToNodeId = 170, Cost = Km(169, 170, nodes), Bidirectional = true },
                new() { FromNodeId = 170, ToNodeId = 171, Cost = Km(170, 171, nodes), Bidirectional = true },
                // ── Orange St north-south ─────────────────────────────────────────────────
                new() { FromNodeId =   1, ToNodeId =   9, Cost = Km(  1,   9, nodes), Bidirectional = true },
                new() { FromNodeId =   9, ToNodeId =  56, Cost = Km(  9,  56, nodes), Bidirectional = true },
                new() { FromNodeId =  56, ToNodeId =  17, Cost = Km( 56,  17, nodes), Bidirectional = true },
                new() { FromNodeId =  17, ToNodeId =  25, Cost = Km( 17,  25, nodes), Bidirectional = true },
                new() { FromNodeId =  25, ToNodeId =  64, Cost = Km( 25,  64, nodes), Bidirectional = true },
                new() { FromNodeId =  64, ToNodeId =  33, Cost = Km( 64,  33, nodes), Bidirectional = true },
                // ── Church St north-south ─────────────────────────────────────────────────
                new() { FromNodeId =   2, ToNodeId =  10, Cost = Km(  2,  10, nodes), Bidirectional = true },
                new() { FromNodeId =  10, ToNodeId = 132, Cost = Km( 10, 132, nodes), Bidirectional = true },
                new() { FromNodeId = 132, ToNodeId = 156, Cost = Km(132, 156, nodes), Bidirectional = true },
                new() { FromNodeId = 156, ToNodeId = 121, Cost = Km(156, 121, nodes), Bidirectional = true },
                new() { FromNodeId = 121, ToNodeId =  57, Cost = Km(121,  57, nodes), Bidirectional = true },
                new() { FromNodeId =  57, ToNodeId =  18, Cost = Km( 57,  18, nodes), Bidirectional = true },
                new() { FromNodeId =  18, ToNodeId = 129, Cost = Km( 18, 129, nodes), Bidirectional = true },
                new() { FromNodeId = 129, ToNodeId =  26, Cost = Km(129,  26, nodes), Bidirectional = true },
                new() { FromNodeId =  26, ToNodeId =  65, Cost = Km( 26,  65, nodes), Bidirectional = true },
                new() { FromNodeId =  65, ToNodeId = 174, Cost = Km( 65, 174, nodes), Bidirectional = true },
                new() { FromNodeId = 174, ToNodeId =  34, Cost = Km(174,  34, nodes), Bidirectional = true },
                new() { FromNodeId =  34, ToNodeId =  70, Cost = Km( 34,  70, nodes), Bidirectional = true },
                new() { FromNodeId =  70, ToNodeId =  46, Cost = Km( 70,  46, nodes), Bidirectional = true },
                new() { FromNodeId =  46, ToNodeId =  77, Cost = Km( 46,  77, nodes), Bidirectional = true },
                new() { FromNodeId =  77, ToNodeId =  41, Cost = Km( 77,  41, nodes), Bidirectional = true },
                // ── State St north-south ──────────────────────────────────────────────────
                new() { FromNodeId =   3, ToNodeId =  11, Cost = Km(  3,  11, nodes), Bidirectional = true },
                new() { FromNodeId =  11, ToNodeId = 133, Cost = Km( 11, 133, nodes), Bidirectional = true },
                new() { FromNodeId = 133, ToNodeId = 157, Cost = Km(133, 157, nodes), Bidirectional = true },
                new() { FromNodeId = 157, ToNodeId = 122, Cost = Km(157, 122, nodes), Bidirectional = true },
                new() { FromNodeId = 122, ToNodeId = 126, Cost = Km(122, 126, nodes), Bidirectional = true },
                new() { FromNodeId = 126, ToNodeId =  58, Cost = Km(126,  58, nodes), Bidirectional = true },
                new() { FromNodeId =  58, ToNodeId =  19, Cost = Km( 58,  19, nodes), Bidirectional = true },
                new() { FromNodeId =  19, ToNodeId = 118, Cost = Km( 19, 118, nodes), Bidirectional = true },
                new() { FromNodeId = 118, ToNodeId = 130, Cost = Km(118, 130, nodes), Bidirectional = true },
                new() { FromNodeId = 130, ToNodeId =  27, Cost = Km(130,  27, nodes), Bidirectional = true },
                new() { FromNodeId =  27, ToNodeId =  66, Cost = Km( 27,  66, nodes), Bidirectional = true },
                new() { FromNodeId =  66, ToNodeId = 175, Cost = Km( 66, 175, nodes), Bidirectional = true },
                new() { FromNodeId = 175, ToNodeId =  35, Cost = Km(175,  35, nodes), Bidirectional = true },
                new() { FromNodeId =  35, ToNodeId =  71, Cost = Km( 35,  71, nodes), Bidirectional = true },
                new() { FromNodeId =  71, ToNodeId =  47, Cost = Km( 71,  47, nodes), Bidirectional = true },
                new() { FromNodeId =  47, ToNodeId =  78, Cost = Km( 47,  78, nodes), Bidirectional = true },
                new() { FromNodeId =  78, ToNodeId =  42, Cost = Km( 78,  42, nodes), Bidirectional = true },
                // ── Alabama St north-south ────────────────────────────────────────────────
                new() { FromNodeId =   4, ToNodeId =  12, Cost = Km(  4,  12, nodes), Bidirectional = true },
                new() { FromNodeId =  12, ToNodeId = 134, Cost = Km( 12, 134, nodes), Bidirectional = true },
                new() { FromNodeId = 134, ToNodeId = 158, Cost = Km(134, 158, nodes), Bidirectional = true },
                new() { FromNodeId = 158, ToNodeId = 123, Cost = Km(158, 123, nodes), Bidirectional = true },
                new() { FromNodeId = 123, ToNodeId = 127, Cost = Km(123, 127, nodes), Bidirectional = true },
                new() { FromNodeId = 127, ToNodeId =  59, Cost = Km(127,  59, nodes), Bidirectional = true },
                new() { FromNodeId =  59, ToNodeId =  20, Cost = Km( 59,  20, nodes), Bidirectional = true },
                new() { FromNodeId =  20, ToNodeId = 131, Cost = Km( 20, 131, nodes), Bidirectional = true },
                new() { FromNodeId = 131, ToNodeId =  28, Cost = Km(131,  28, nodes), Bidirectional = true },
                new() { FromNodeId =  28, ToNodeId =  67, Cost = Km( 28,  67, nodes), Bidirectional = true },
                new() { FromNodeId =  67, ToNodeId =  36, Cost = Km( 67,  36, nodes), Bidirectional = true },
                new() { FromNodeId =  36, ToNodeId =  72, Cost = Km( 36,  72, nodes), Bidirectional = true },
                new() { FromNodeId =  72, ToNodeId =  48, Cost = Km( 72,  48, nodes), Bidirectional = true },
                new() { FromNodeId =  48, ToNodeId =  79, Cost = Km( 48,  79, nodes), Bidirectional = true },
                new() { FromNodeId =  79, ToNodeId =  43, Cost = Km( 79,  43, nodes), Bidirectional = true },
                // ── Texas St north-south ──────────────────────────────────────────────────
                new() { FromNodeId =  82, ToNodeId =  83, Cost = Km( 82,  83, nodes), Bidirectional = true },
                new() { FromNodeId =  83, ToNodeId =  84, Cost = Km( 83,  84, nodes), Bidirectional = true },
                new() { FromNodeId =  84, ToNodeId = 128, Cost = Km( 84, 128, nodes), Bidirectional = true },
                new() { FromNodeId = 128, ToNodeId =  85, Cost = Km(128,  85, nodes), Bidirectional = true },
                new() { FromNodeId =  85, ToNodeId =  86, Cost = Km( 85,  86, nodes), Bidirectional = true },
                new() { FromNodeId =  86, ToNodeId =  87, Cost = Km( 86,  87, nodes), Bidirectional = true },
                new() { FromNodeId =  87, ToNodeId =  88, Cost = Km( 87,  88, nodes), Bidirectional = true },
                // ── Tennessee St (S) north-south ──────────────────────────────────────────
                new() { FromNodeId =   5, ToNodeId =  13, Cost = Km(  5,  13, nodes), Bidirectional = true },
                new() { FromNodeId =  13, ToNodeId = 135, Cost = Km( 13, 135, nodes), Bidirectional = true },
                new() { FromNodeId = 135, ToNodeId = 159, Cost = Km(135, 159, nodes), Bidirectional = true },
                new() { FromNodeId = 159, ToNodeId = 124, Cost = Km(159, 124, nodes), Bidirectional = true },
                new() { FromNodeId = 124, ToNodeId =  60, Cost = Km(124,  60, nodes), Bidirectional = true },
                new() { FromNodeId =  60, ToNodeId =  21, Cost = Km( 60,  21, nodes), Bidirectional = true },
                new() { FromNodeId =  21, ToNodeId = 119, Cost = Km( 21, 119, nodes), Bidirectional = true },
                new() { FromNodeId = 119, ToNodeId =  29, Cost = Km(119,  29, nodes), Bidirectional = true },
                new() { FromNodeId =  29, ToNodeId =  68, Cost = Km( 29,  68, nodes), Bidirectional = true },
                new() { FromNodeId =  68, ToNodeId =  37, Cost = Km( 68,  37, nodes), Bidirectional = true },
                new() { FromNodeId =  37, ToNodeId =  73, Cost = Km( 37,  73, nodes), Bidirectional = true },
                new() { FromNodeId =  73, ToNodeId =  49, Cost = Km( 73,  49, nodes), Bidirectional = true },
                new() { FromNodeId =  49, ToNodeId =  80, Cost = Km( 49,  80, nodes), Bidirectional = true },
                new() { FromNodeId =  80, ToNodeId =  44, Cost = Km( 80,  44, nodes), Bidirectional = true },
                // ── Eureka St north-south ─────────────────────────────────────────────────
                new() { FromNodeId =  89, ToNodeId =  90, Cost = Km( 89,  90, nodes), Bidirectional = true },
                new() { FromNodeId =  90, ToNodeId =  91, Cost = Km( 90,  91, nodes), Bidirectional = true },
                new() { FromNodeId =  91, ToNodeId =  92, Cost = Km( 91,  92, nodes), Bidirectional = true },
                new() { FromNodeId =  92, ToNodeId =  93, Cost = Km( 92,  93, nodes), Bidirectional = true },
                new() { FromNodeId =  93, ToNodeId =  94, Cost = Km( 93,  94, nodes), Bidirectional = true },
                // ── Brookside Ave north-south ─────────────────────────────────────────────
                new() { FromNodeId =   6, ToNodeId =  14, Cost = Km(  6,  14, nodes), Bidirectional = true },
                new() { FromNodeId =  14, ToNodeId = 125, Cost = Km( 14, 125, nodes), Bidirectional = true },
                new() { FromNodeId = 125, ToNodeId = 160, Cost = Km(125, 160, nodes), Bidirectional = true },
                new() { FromNodeId = 160, ToNodeId =  61, Cost = Km(160,  61, nodes), Bidirectional = true },
                new() { FromNodeId =  61, ToNodeId =  22, Cost = Km( 61,  22, nodes), Bidirectional = true },
                new() { FromNodeId =  22, ToNodeId =  30, Cost = Km( 22,  30, nodes), Bidirectional = true },
                new() { FromNodeId =  30, ToNodeId =  69, Cost = Km( 30,  69, nodes), Bidirectional = true },
                new() { FromNodeId =  69, ToNodeId =  38, Cost = Km( 69,  38, nodes), Bidirectional = true },
                new() { FromNodeId =  38, ToNodeId =  74, Cost = Km( 38,  74, nodes), Bidirectional = true },
                new() { FromNodeId =  74, ToNodeId =  50, Cost = Km( 74,  50, nodes), Bidirectional = true },
                new() { FromNodeId =  50, ToNodeId =  81, Cost = Km( 50,  81, nodes), Bidirectional = true },
                new() { FromNodeId =  81, ToNodeId =  45, Cost = Km( 81,  45, nodes), Bidirectional = true },
                // ── Tennessee St north-south ──────────────────────────────────────────────
                new() { FromNodeId =   7, ToNodeId =  15, Cost = Km(  7,  15, nodes), Bidirectional = true },
                new() { FromNodeId =  15, ToNodeId =  62, Cost = Km( 15,  62, nodes), Bidirectional = true },
                new() { FromNodeId =  62, ToNodeId =  23, Cost = Km( 62,  23, nodes), Bidirectional = true },
                new() { FromNodeId =  23, ToNodeId = 120, Cost = Km( 23, 120, nodes), Bidirectional = true },
                new() { FromNodeId = 120, ToNodeId =  31, Cost = Km(120,  31, nodes), Bidirectional = true },
                new() { FromNodeId =  31, ToNodeId =  39, Cost = Km( 31,  39, nodes), Bidirectional = true },
                new() { FromNodeId =  39, ToNodeId =  75, Cost = Km( 39,  75, nodes), Bidirectional = true },
                new() { FromNodeId =  75, ToNodeId = 169, Cost = Km( 75, 169, nodes), Bidirectional = true },
                // ── Brockton Ave north-south ──────────────────────────────────────────────
                new() { FromNodeId =   8, ToNodeId =  16, Cost = Km(  8,  16, nodes), Bidirectional = true },
                new() { FromNodeId =  16, ToNodeId =  63, Cost = Km( 16,  63, nodes), Bidirectional = true },
                new() { FromNodeId =  63, ToNodeId = 147, Cost = Km( 63, 147, nodes), Bidirectional = true },
                new() { FromNodeId = 147, ToNodeId =  24, Cost = Km(147,  24, nodes), Bidirectional = true },
                new() { FromNodeId =  24, ToNodeId =  32, Cost = Km( 24,  32, nodes), Bidirectional = true },
                new() { FromNodeId =  32, ToNodeId =  40, Cost = Km( 32,  40, nodes), Bidirectional = true },
                new() { FromNodeId =  40, ToNodeId =  76, Cost = Km( 40,  76, nodes), Bidirectional = true },
                new() { FromNodeId =  76, ToNodeId = 170, Cost = Km( 76, 170, nodes), Bidirectional = true },
                new() { FromNodeId =  76, ToNodeId = 138, Cost = Km( 76, 138, nodes), Bidirectional = true },
                new() { FromNodeId = 138, ToNodeId = 172, Cost = Km(138, 172, nodes), Bidirectional = true },
                new() { FromNodeId = 172, ToNodeId = 137, Cost = Km(172, 137, nodes), Bidirectional = true },
                // ── California St north-south ─────────────────────────────────────────────
                new() { FromNodeId =  95, ToNodeId =  96, Cost = Km( 95,  96, nodes), Bidirectional = true },
                new() { FromNodeId =  96, ToNodeId =  97, Cost = Km( 96,  97, nodes), Bidirectional = true },
                new() { FromNodeId =  97, ToNodeId =  98, Cost = Km( 97,  98, nodes), Bidirectional = true },
                new() { FromNodeId =  98, ToNodeId =  99, Cost = Km( 98,  99, nodes), Bidirectional = true },
                // ── Pioneer Ave north-south ───────────────────────────────────────────────
                new() { FromNodeId = 140, ToNodeId = 141, Cost = Km(140, 141, nodes), Bidirectional = true },
                new() { FromNodeId = 141, ToNodeId = 142, Cost = Km(141, 142, nodes), Bidirectional = true },
                new() { FromNodeId = 142, ToNodeId = 143, Cost = Km(142, 143, nodes), Bidirectional = true },
                new() { FromNodeId = 143, ToNodeId = 144, Cost = Km(143, 144, nodes), Bidirectional = true },
                // ── Wabash Ave north-south ────────────────────────────────────────────────
                new() { FromNodeId = 161, ToNodeId = 162, Cost = Km(161, 162, nodes), Bidirectional = true },
                new() { FromNodeId = 162, ToNodeId = 163, Cost = Km(162, 163, nodes), Bidirectional = true },
                new() { FromNodeId = 171, ToNodeId = 161, Cost = Km(171, 161, nodes), Bidirectional = true },
                // ── Tippecanoe Ave north-south ────────────────────────────────────────────
                new() { FromNodeId = 150, ToNodeId = 148, Cost = Km(150, 148, nodes), Bidirectional = true },
                new() { FromNodeId = 148, ToNodeId = 152, Cost = Km(148, 152, nodes), Bidirectional = true },
                new() { FromNodeId = 152, ToNodeId = 154, Cost = Km(152, 154, nodes), Bidirectional = true },
                // ── Mountain View Ave north-south ─────────────────────────────────────────
                new() { FromNodeId = 151, ToNodeId = 149, Cost = Km(151, 149, nodes), Bidirectional = true },
                new() { FromNodeId = 149, ToNodeId = 153, Cost = Km(149, 153, nodes), Bidirectional = true },
                new() { FromNodeId = 153, ToNodeId = 155, Cost = Km(153, 155, nodes), Bidirectional = true },
                // ── I-10 mainline chain (west → east, one-way) ───────────────────────────
                new() { FromNodeId =  52, ToNodeId =  53, Cost = 0.13, Bidirectional = false },
                new() { FromNodeId =  53, ToNodeId =  54, Cost = 0.12, Bidirectional = false },
                new() { FromNodeId =  54, ToNodeId =  55, Cost = 0.14, Bidirectional = false },
                new() { FromNodeId = 100, ToNodeId = 101, Cost = 0.10, Bidirectional = false },
                new() { FromNodeId = 101, ToNodeId = 102, Cost = Km(101, 102, nodes), Bidirectional = false },
                new() { FromNodeId = 102, ToNodeId = 103, Cost = 0.10, Bidirectional = false },
                new() { FromNodeId = 103, ToNodeId = 104, Cost = Km(103, 104, nodes), Bidirectional = false },
                new() { FromNodeId = 104, ToNodeId = 105, Cost = 0.10, Bidirectional = false },
                new() { FromNodeId = 105, ToNodeId = 106, Cost = Km(105, 106, nodes), Bidirectional = false },
                new() { FromNodeId = 106, ToNodeId = 107, Cost = 0.10, Bidirectional = false },
                new() { FromNodeId = 107, ToNodeId = 108, Cost = Km(107, 108, nodes), Bidirectional = false },
                new() { FromNodeId = 108, ToNodeId = 109, Cost = 0.10, Bidirectional = false },
                new() { FromNodeId = 109, ToNodeId = 110, Cost = Km(109, 110, nodes), Bidirectional = false },
                new() { FromNodeId = 110, ToNodeId = 111, Cost = 0.10, Bidirectional = false },
                new() { FromNodeId = 111, ToNodeId = 145, Cost = Km(111, 145, nodes), Bidirectional = false },
                new() { FromNodeId = 145, ToNodeId = 146, Cost = 0.10, Bidirectional = false },
                // ── I-10 ramp surface connections ─────────────────────────────────────────
                new() { FromNodeId =  55, ToNodeId = 149, Cost = Km( 55, 149, nodes), Bidirectional = false }, // EB on-ramp -> Lugonia & Mountain View Ave
                new() { FromNodeId = 148, ToNodeId =  53, Cost = Km(148,  53, nodes), Bidirectional = false }, // Lugonia & Tippecanoe -> EB on-ramp
                new() { FromNodeId =  52, ToNodeId = 148, Cost = Km( 52, 148, nodes), Bidirectional = false }, // WB off-ramp -> Lugonia & Tippecanoe Ave
                new() { FromNodeId =  54, ToNodeId = 149, Cost = Km( 54, 149, nodes), Bidirectional = true  }, // Mountain View WB off-ramp <-> Lugonia & Mountain View Ave
                new() { FromNodeId = 100, ToNodeId =  96, Cost = Km(100,  96, nodes), Bidirectional = false },
                new() { FromNodeId =  97, ToNodeId = 101, Cost = Km( 97, 101, nodes), Bidirectional = false },
                new() { FromNodeId = 102, ToNodeId =  12, Cost = Km(102,  12, nodes), Bidirectional = false },
                new() { FromNodeId =  12, ToNodeId = 103, Cost = Km( 12, 103, nodes), Bidirectional = false },
                new() { FromNodeId = 104, ToNodeId =  13, Cost = Km(104,  13, nodes), Bidirectional = false },
                new() { FromNodeId =  13, ToNodeId = 105, Cost = Km( 13, 105, nodes), Bidirectional = false },
                new() { FromNodeId = 106, ToNodeId =  14, Cost = Km(106,  14, nodes), Bidirectional = false },
                new() { FromNodeId =  14, ToNodeId = 107, Cost = Km( 14, 107, nodes), Bidirectional = false },
                new() { FromNodeId = 108, ToNodeId =  39, Cost = Km(108,  39, nodes), Bidirectional = false },
                new() { FromNodeId =  39, ToNodeId = 109, Cost = Km( 39, 109, nodes), Bidirectional = false },
                new() { FromNodeId = 110, ToNodeId = 147, Cost = Km(110, 147, nodes), Bidirectional = false },
                new() { FromNodeId = 147, ToNodeId = 111, Cost = Km(147, 111, nodes), Bidirectional = false },
                new() { FromNodeId = 145, ToNodeId = 140, Cost = Km(145, 140, nodes), Bidirectional = false },
                new() { FromNodeId = 141, ToNodeId = 146, Cost = Km(141, 146, nodes), Bidirectional = false },
                // ── SR-210 Foothill Freeway mainline (one-way EB) ────────────────────────
                new() { FromNodeId = 105, ToNodeId = 112, Cost = Km(105, 112, nodes), Bidirectional = false },
                new() { FromNodeId = 113, ToNodeId = 104, Cost = Km(113, 104, nodes), Bidirectional = false },
                new() { FromNodeId = 112, ToNodeId = 114, Cost = Km(112, 114, nodes), Bidirectional = false },
                new() { FromNodeId = 114, ToNodeId = 115, Cost = Km(114, 115, nodes), Bidirectional = false },
                new() { FromNodeId = 115, ToNodeId = 116, Cost = Km(115, 116, nodes), Bidirectional = false },
                new() { FromNodeId = 116, ToNodeId = 117, Cost = Km(116, 117, nodes), Bidirectional = false },
                new() { FromNodeId = 114, ToNodeId =  50, Cost = Km(114,  50, nodes), Bidirectional = true  },
                new() { FromNodeId =  38, ToNodeId = 113, Cost = Km( 38, 113, nodes), Bidirectional = false },
                new() { FromNodeId = 166, ToNodeId = 113, Cost = Km(166, 113, nodes), Bidirectional = false },
                new() { FromNodeId =  50, ToNodeId = 166, Cost = Km( 50, 166, nodes), Bidirectional = false },
                // ── SR-38 surface connections ─────────────────────────────────────────────
                new() { FromNodeId = 118, ToNodeId = 119, Cost = Km(118, 119, nodes), Bidirectional = true },
                new() { FromNodeId = 119, ToNodeId = 120, Cost = Km(119, 120, nodes), Bidirectional = true },
                new() { FromNodeId = 120, ToNodeId = 164, Cost = Km(120, 164, nodes), Bidirectional = true },
                new() { FromNodeId = 164, ToNodeId = 165, Cost = Km(164, 165, nodes), Bidirectional = true },
                new() { FromNodeId = 118, ToNodeId = 106, Cost = Km(118, 106, nodes), Bidirectional = false },
                new() { FromNodeId = 107, ToNodeId = 118, Cost = Km(107, 118, nodes), Bidirectional = false },
            };

            return new RoadGraphDto
            {
                GraphName = "Redlands, CA - 175-Node Real-Intersection Network (v4 coordinate-validated)",
                DestinationNodeId = EsriHqNodeId,
                Nodes = nodes,
                Edges = edges
            };
        }

        // Haversine distance in km between two nodes × 1.3 detour factor, rounded to 2 dp.
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
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                        + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
                        * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            double dist = R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return Math.Round(dist * 1.3, 2);
        }
    }
}