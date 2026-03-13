using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace Portfolio.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class FiberFlowSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Demo UserId for all seed data
            var demoUserId = new Guid("00000000-0000-0000-0000-000000000001");

            // -------------------------------------------------------
            // FiberClients
            // -------------------------------------------------------
            migrationBuilder.InsertData(
                table: "FiberClients",
                columns: new[] { "id", "user_id", "name", "contact_name", "email", "phone", "city", "state", "latitude", "longitude", "created_date" },
                values: new object[,]
                {
                    { 1, demoUserId, "Gulf Coast Chemical", "Linda Martinez", "linda@gulfchem.com", "713-555-0101", "Houston", "TX", 29.7604, -95.3698, new DateTime(2024, 1, 5, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, demoUserId, "Lone Star Refining", "James Carter", "jcarter@lonestarref.com", "409-555-0112", "Beaumont", "TX", 30.0860, -94.1018, new DateTime(2024, 1, 12, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, demoUserId, "Delta Processing Co", "Sarah Lee", "slee@deltaproc.com", "225-555-0123", "Baton Rouge", "LA", 30.4515, -91.1871, new DateTime(2024, 2, 3, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, demoUserId, "Bayou Industrial", "Mike Johnson", "mjohnson@bayouind.com", "504-555-0134", "New Orleans", "LA", 29.9511, -90.0715, new DateTime(2024, 2, 18, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, demoUserId, "Sooner Plant Services", "Emily White", "ewhite@soonerplant.com", "405-555-0145", "Oklahoma City", "OK", 35.4676, -97.5164, new DateTime(2024, 3, 7, 0, 0, 0, DateTimeKind.Utc) },
                    { 6, demoUserId, "Arkansas Fabricators", "Robert King", "rking@arkfab.com", "501-555-0156", "Little Rock", "AR", 34.7465, -92.2896, new DateTime(2024, 3, 22, 0, 0, 0, DateTimeKind.Utc) },
                    { 7, demoUserId, "Magnolia Chemical", "Patricia Green", "pgreen@magnoliachem.com", "601-555-0167", "Jackson", "MS", 32.2988, -90.1848, new DateTime(2024, 4, 9, 0, 0, 0, DateTimeKind.Utc) },
                    { 8, demoUserId, "Steel City Industries", "William Brown", "wbrown@steelcity.com", "205-555-0178", "Birmingham", "AL", 33.5186, -86.8104, new DateTime(2024, 4, 25, 0, 0, 0, DateTimeKind.Utc) },
                    { 9, demoUserId, "Cumberland Manufacturing", "Jessica Adams", "jadams@cumberlandmfg.com", "615-555-0189", "Nashville", "TN", 36.1627, -86.7816, new DateTime(2024, 5, 14, 0, 0, 0, DateTimeKind.Utc) },
                    { 10, demoUserId, "Peach State Processing", "David Wilson", "dwilson@peachstate.com", "404-555-0190", "Atlanta", "GA", 33.7490, -84.3880, new DateTime(2024, 6, 2, 0, 0, 0, DateTimeKind.Utc) }
                });

            // -------------------------------------------------------
            // FiberMaterials
            // -------------------------------------------------------
            migrationBuilder.InsertData(
                table: "FiberMaterials",
                columns: new[] { "id", "user_id", "name", "sku", "unit_of_measure", "qty_on_hand", "reorder_point", "reorder_qty", "unit_cost", "supplier", "warehouse_location", "last_updated" },
                values: new object[,]
                {
                    { 1,  demoUserId, "Fiberglass Woven Roving", "RM-FGW",  "rolls",  85m,  20m,  40m,  42.00m, "TexMat Co",     "A1", new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
                    { 2,  demoUserId, "Polyester Resin",          "RM-PR",   "gal",    12m,  30m,  60m,  18.50m, "ResinWorks",    "B2", new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
                    { 3,  demoUserId, "Hardener MEKP",            "RM-MEK",  "gal",     8m,  15m,  30m,  24.75m, "ChemCore",      "B2", new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
                    { 4,  demoUserId, "Release Wax",              "RM-RW",   "lbs",    45m,  10m,  20m,   8.25m, "ReleasePro",    "C3", new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
                    { 5,  demoUserId, "Fiberglass Mat 1.5oz",     "RM-FGM",  "rolls",  60m,  25m,  40m,  31.00m, "TexMat Co",     "A2", new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
                    { 6,  demoUserId, "Pigment - Gray",           "RM-PGR",  "lbs",    22m,  10m,  20m,  12.50m, "PigmentX",      "D1", new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
                    { 7,  demoUserId, "Pigment - White",          "RM-PWH",  "lbs",     6m,  10m,  20m,  12.50m, "PigmentX",      "D1", new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
                    { 8,  demoUserId, "PVC Core Sheet",           "RM-PVC",  "sheets", 140m, 50m, 100m,   5.80m, "CorePlastics",  "E1", new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
                    { 9,  demoUserId, "Flanges 4in",              "RM-FL4",  "units",  320m, 100m, 200m,  2.15m, "DuctParts",     "F1", new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
                    { 10, demoUserId, "Flanges 6in",              "RM-FL6",  "units",   88m, 100m, 200m,  3.40m, "DuctParts",     "F1", new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
                    { 11, demoUserId, "Sandpaper 80-grit",        "RM-SP80", "sheets", 500m, 200m, 400m,  0.35m, "AbrasiveCo",    "G1", new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
                    { 12, demoUserId, "Acetone Solvent",          "RM-ACE",  "gal",    30m,  10m,  20m,  14.00m, "Solvex",        "H1", new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
                    { 13, demoUserId, "Epoxy Resin",              "RM-EPX",  "gal",    25m,  15m,  30m,  27.00m, "ResinWorks",    "B3", new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
                    { 14, demoUserId, "Glass Microspheres",       "RM-GMS",  "lbs",    18m,   8m,  16m,  19.00m, "MicroFill",     "C2", new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
                    { 15, demoUserId, "Chopped Strand Mat",       "RM-CSM",  "rolls",  33m,  12m,  24m,  29.00m, "TexMat Co",     "A3", new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc) }
                });

            // -------------------------------------------------------
            // FiberOrders — 30 records spanning Aug 2024 – Jan 2025
            // Status mix: 3 Draft, 5 Confirmed, 7 In Production,
            //             8 Shipped, 7 Delivered
            // -------------------------------------------------------
            migrationBuilder.InsertData(
                table: "FiberOrders",
                columns: new[] { "id", "user_id", "client_id", "product_name", "product_sku", "quantity", "unit_price", "total_value", "status", "order_date", "ship_date" },
                values: new object[,]
                {
                    // --- Delivered (7) ---
                    { 1,  demoUserId, 1,  "4\" Round Fiberglass Duct",       "FD-4R",     200, 14.50m,  2900.00m, "Delivered",     new DateTime(2024, 8,  5, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 8, 14, 0, 0, 0, DateTimeKind.Utc) },
                    { 2,  demoUserId, 3,  "6\" Round Fiberglass Duct",       "FD-6R",     150, 22.00m,  3300.00m, "Delivered",     new DateTime(2024, 8, 18, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 8, 27, 0, 0, 0, DateTimeKind.Utc) },
                    { 3,  demoUserId, 7,  "Chemical-Resistant 90° Elbow 4\"","EL-90-4CR",  75, 67.00m,  5025.00m, "Delivered",     new DateTime(2024, 9,  2, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 9, 12, 0, 0, 0, DateTimeKind.Utc) },
                    { 4,  demoUserId, 9,  "12\" Duct Section",               "FD-12S",     50, 118.00m, 5900.00m, "Delivered",     new DateTime(2024, 9, 20, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 9, 30, 0, 0, 0, DateTimeKind.Utc) },
                    { 5,  demoUserId, 2,  "High-Temp Flanged Fitting 6\"",   "FF-6HT",     40, 95.50m,  3820.00m, "Delivered",     new DateTime(2024, 10, 8, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 10,18, 0, 0, 0, DateTimeKind.Utc) },
                    { 6,  demoUserId, 5,  "8\" Rectangular Duct",            "FD-8X",     120, 38.75m,  4650.00m, "Delivered",     new DateTime(2024, 10,25, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 11, 4, 0, 0, 0, DateTimeKind.Utc) },
                    { 7,  demoUserId, 10, "Custom Exhaust Hood",             "EH-CUST",    10, 285.00m, 2850.00m, "Delivered",     new DateTime(2024, 11, 5, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 11,16, 0, 0, 0, DateTimeKind.Utc) },

                    // --- Shipped (8) ---
                    { 8,  demoUserId, 4,  "4\" Round Fiberglass Duct",       "FD-4R",     500, 14.50m,  7250.00m, "Shipped",       new DateTime(2024, 11,12, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 11,22, 0, 0, 0, DateTimeKind.Utc) },
                    { 9,  demoUserId, 6,  "6\" Round Fiberglass Duct",       "FD-6R",     200, 22.00m,  4400.00m, "Shipped",       new DateTime(2024, 11,19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 11,29, 0, 0, 0, DateTimeKind.Utc) },
                    { 10, demoUserId, 8,  "Chemical-Resistant 90° Elbow 4\"","EL-90-4CR", 100, 67.00m,  6700.00m, "Shipped",       new DateTime(2024, 11,28, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 12, 8, 0, 0, 0, DateTimeKind.Utc) },
                    { 11, demoUserId, 1,  "High-Temp Flanged Fitting 6\"",   "FF-6HT",     60, 95.50m,  5730.00m, "Shipped",       new DateTime(2024, 12, 3, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 12,13, 0, 0, 0, DateTimeKind.Utc) },
                    { 12, demoUserId, 3,  "12\" Duct Section",               "FD-12S",     80, 118.00m, 9440.00m, "Shipped",       new DateTime(2024, 12,10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 12,20, 0, 0, 0, DateTimeKind.Utc) },
                    { 13, demoUserId, 7,  "8\" Rectangular Duct",            "FD-8X",     175, 38.75m,  6781.25m, "Shipped",       new DateTime(2024, 12,17, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 12,27, 0, 0, 0, DateTimeKind.Utc) },
                    { 14, demoUserId, 9,  "4\" Round Fiberglass Duct",       "FD-4R",     300, 14.50m,  4350.00m, "Shipped",       new DateTime(2024, 12,22, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1,  1, 0, 0, 0, DateTimeKind.Utc) },
                    { 15, demoUserId, 2,  "Custom Exhaust Hood",             "EH-CUST",    15, 285.00m, 4275.00m, "Shipped",       new DateTime(2024, 12,29, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1,  8, 0, 0, 0, DateTimeKind.Utc) },

                    // --- In Production (7) ---
                    { 16, demoUserId, 4,  "6\" Round Fiberglass Duct",       "FD-6R",     250, 22.00m,  5500.00m, "In Production", new DateTime(2025, 1,  2, 0, 0, 0, DateTimeKind.Utc), null },
                    { 17, demoUserId, 5,  "Chemical-Resistant 90° Elbow 4\"","EL-90-4CR",  90, 67.00m,  6030.00m, "In Production", new DateTime(2025, 1,  6, 0, 0, 0, DateTimeKind.Utc), null },
                    { 18, demoUserId, 6,  "High-Temp Flanged Fitting 6\"",   "FF-6HT",     35, 95.50m,  3342.50m, "In Production", new DateTime(2025, 1,  9, 0, 0, 0, DateTimeKind.Utc), null },
                    { 19, demoUserId, 8,  "12\" Duct Section",               "FD-12S",     60, 118.00m, 7080.00m, "In Production", new DateTime(2025, 1, 13, 0, 0, 0, DateTimeKind.Utc), null },
                    { 20, demoUserId, 10, "8\" Rectangular Duct",            "FD-8X",     200, 38.75m,  7750.00m, "In Production", new DateTime(2025, 1, 16, 0, 0, 0, DateTimeKind.Utc), null },
                    { 21, demoUserId, 1,  "4\" Round Fiberglass Duct",       "FD-4R",     400, 14.50m,  5800.00m, "In Production", new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc), null },
                    { 22, demoUserId, 3,  "Custom Exhaust Hood",             "EH-CUST",    20, 285.00m, 5700.00m, "In Production", new DateTime(2025, 1, 22, 0, 0, 0, DateTimeKind.Utc), null },

                    // --- Confirmed (5) ---
                    { 23, demoUserId, 2,  "6\" Round Fiberglass Duct",       "FD-6R",     180, 22.00m,  3960.00m, "Confirmed",     new DateTime(2025, 1, 24, 0, 0, 0, DateTimeKind.Utc), null },
                    { 24, demoUserId, 4,  "Chemical-Resistant 90° Elbow 4\"","EL-90-4CR",  50, 67.00m,  3350.00m, "Confirmed",     new DateTime(2025, 1, 26, 0, 0, 0, DateTimeKind.Utc), null },
                    { 25, demoUserId, 7,  "High-Temp Flanged Fitting 6\"",   "FF-6HT",     45, 95.50m,  4297.50m, "Confirmed",     new DateTime(2025, 1, 27, 0, 0, 0, DateTimeKind.Utc), null },
                    { 26, demoUserId, 9,  "12\" Duct Section",               "FD-12S",     70, 118.00m, 8260.00m, "Confirmed",     new DateTime(2025, 1, 28, 0, 0, 0, DateTimeKind.Utc), null },
                    { 27, demoUserId, 5,  "8\" Rectangular Duct",            "FD-8X",     130, 38.75m,  5037.50m, "Confirmed",     new DateTime(2025, 1, 29, 0, 0, 0, DateTimeKind.Utc), null },

                    // --- Draft (3) ---
                    { 28, demoUserId, 6,  "4\" Round Fiberglass Duct",       "FD-4R",     100, 14.50m,  1450.00m, "Draft",         new DateTime(2025, 1, 30, 0, 0, 0, DateTimeKind.Utc), null },
                    { 29, demoUserId, 8,  "Custom Exhaust Hood",             "EH-CUST",    12, 285.00m, 3420.00m, "Draft",         new DateTime(2025, 1, 31, 0, 0, 0, DateTimeKind.Utc), null },
                    { 30, demoUserId, 10, "6\" Round Fiberglass Duct",       "FD-6R",     220, 22.00m,  4840.00m, "Draft",         new DateTime(2025, 2,  1, 0, 0, 0, DateTimeKind.Utc), null }
                });

            // -------------------------------------------------------
            // FiberShipments — 22 records
            // Linked to Shipped (orders 8–15) and Delivered (orders 1–7)
            // Origin always Houston: 29.7604, -95.3698
            // Status mix: 8 Shipped orders (In Transit/Delayed),
            //             7 Delivered orders (Delivered),
            //             plus 7 additional historical shipments for chart depth
            // -------------------------------------------------------
            migrationBuilder.InsertData(
                table: "FiberShipments",
                columns: new[] { "id", "user_id", "order_id", "carrier_name", "tracking_number", "status", "ship_date", "estimated_arrival", "origin_lat", "origin_lng", "destination_lat", "destination_lng", "destination_city", "destination_state" },
                values: new object[,]
                {
                    // Delivered shipments (orders 1–7)
                    { 1,  demoUserId, 1,  "FedEx Freight",   "FX-2024-10041", "Delivered",  new DateTime(2024, 8, 14, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 8, 19, 0, 0, 0, DateTimeKind.Utc), 29.7604, -95.3698, 29.7604, -95.3698, "Houston",       "TX" },
                    { 2,  demoUserId, 2,  "XPO Logistics",   "XP-2024-20083", "Delivered",  new DateTime(2024, 8, 27, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 9,  1, 0, 0, 0, DateTimeKind.Utc), 29.7604, -95.3698, 30.4515, -91.1871, "Baton Rouge",   "LA" },
                    { 3,  demoUserId, 3,  "Old Dominion",    "OD-2024-30127", "Delivered",  new DateTime(2024, 9, 12, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 9, 17, 0, 0, 0, DateTimeKind.Utc), 29.7604, -95.3698, 32.2988, -90.1848, "Jackson",       "MS" },
                    { 4,  demoUserId, 4,  "Estes Express",   "ES-2024-40162", "Delivered",  new DateTime(2024, 9, 30, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 10, 5, 0, 0, 0, DateTimeKind.Utc), 29.7604, -95.3698, 36.1627, -86.7816, "Nashville",     "TN" },
                    { 5,  demoUserId, 5,  "FedEx Freight",   "FX-2024-50198", "Delivered",  new DateTime(2024, 10,18, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 10,23, 0, 0, 0, DateTimeKind.Utc), 29.7604, -95.3698, 30.0860, -94.1018, "Beaumont",      "TX" },
                    { 6,  demoUserId, 6,  "XPO Logistics",   "XP-2024-60214", "Delivered",  new DateTime(2024, 11, 4, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 11, 9, 0, 0, 0, DateTimeKind.Utc), 29.7604, -95.3698, 35.4676, -97.5164, "Oklahoma City", "OK" },
                    { 7,  demoUserId, 7,  "Old Dominion",    "OD-2024-70251", "Delivered",  new DateTime(2024, 11,16, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 11,21, 0, 0, 0, DateTimeKind.Utc), 29.7604, -95.3698, 33.7490, -84.3880, "Atlanta",       "GA" },

                    // In Transit shipments (orders 8–13, 15)
                    { 8,  demoUserId, 8,  "FedEx Freight",   "FX-2024-80289", "In Transit", new DateTime(2024, 11,22, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 2, 10, 0, 0, 0, DateTimeKind.Utc), 29.7604, -95.3698, 29.9511, -90.0715, "New Orleans",   "LA" },
                    { 9,  demoUserId, 9,  "Estes Express",   "ES-2024-90312", "In Transit", new DateTime(2024, 11,29, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 2, 14, 0, 0, 0, DateTimeKind.Utc), 29.7604, -95.3698, 34.7465, -92.2896, "Little Rock",   "AR" },
                    { 10, demoUserId, 10, "XPO Logistics",   "XP-2024-10348", "In Transit", new DateTime(2024, 12, 8, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 2, 18, 0, 0, 0, DateTimeKind.Utc), 29.7604, -95.3698, 33.5186, -86.8104, "Birmingham",    "AL" },
                    { 11, demoUserId, 11, "Old Dominion",    "OD-2024-11371", "In Transit", new DateTime(2024, 12,13, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 2, 20, 0, 0, 0, DateTimeKind.Utc), 29.7604, -95.3698, 29.7604, -95.3698, "Houston",       "TX" },
                    { 12, demoUserId, 12, "FedEx Freight",   "FX-2024-12405", "In Transit", new DateTime(2024, 12,20, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 2, 24, 0, 0, 0, DateTimeKind.Utc), 29.7604, -95.3698, 30.4515, -91.1871, "Baton Rouge",   "LA" },
                    { 13, demoUserId, 13, "Estes Express",   "ES-2024-13438", "In Transit", new DateTime(2024, 12,27, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 2, 28, 0, 0, 0, DateTimeKind.Utc), 29.7604, -95.3698, 32.2988, -90.1848, "Jackson",       "MS" },
                    { 14, demoUserId, 15, "XPO Logistics",   "XP-2025-14012", "In Transit", new DateTime(2025, 1,  8, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 3,  4, 0, 0, 0, DateTimeKind.Utc), 29.7604, -95.3698, 30.0860, -94.1018, "Beaumont",      "TX" },

                    // Delayed shipments (orders 14 split — 1 delayed leg)
                    { 15, demoUserId, 14, "Old Dominion",    "OD-2025-15051", "Delayed",    new DateTime(2025, 1,  1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 3,  8, 0, 0, 0, DateTimeKind.Utc), 29.7604, -95.3698, 36.1627, -86.7816, "Nashville",     "TN" },
                    { 16, demoUserId, 10, "FedEx Freight",   "FX-2024-16092", "Delayed",    new DateTime(2024, 12, 9, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 3, 12, 0, 0, 0, DateTimeKind.Utc), 29.7604, -95.3698, 33.5186, -86.8104, "Birmingham",    "AL" },

                    // Extra historical shipments for chart richness (no order FK — set to existing orders)
                    { 17, demoUserId, 1,  "Estes Express",   "ES-2024-17001", "Delivered",  new DateTime(2024, 7,  5, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 7, 12, 0, 0, 0, DateTimeKind.Utc), 29.7604, -95.3698, 33.7490, -84.3880, "Atlanta",       "GA" },
                    { 18, demoUserId, 2,  "FedEx Freight",   "FX-2024-18004", "Delivered",  new DateTime(2024, 7, 18, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 7, 25, 0, 0, 0, DateTimeKind.Utc), 29.7604, -95.3698, 35.4676, -97.5164, "Oklahoma City", "OK" },
                    { 19, demoUserId, 3,  "XPO Logistics",   "XP-2024-19009", "Delivered",  new DateTime(2024, 7, 30, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 8,  6, 0, 0, 0, DateTimeKind.Utc), 29.7604, -95.3698, 34.7465, -92.2896, "Little Rock",   "AR" },
                    { 20, demoUserId, 4,  "Old Dominion",    "OD-2024-20014", "Delivered",  new DateTime(2024, 8,  2, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 8,  9, 0, 0, 0, DateTimeKind.Utc), 29.7604, -95.3698, 29.9511, -90.0715, "New Orleans",   "LA" },
                    { 21, demoUserId, 5,  "FedEx Freight",   "FX-2024-21019", "Delivered",  new DateTime(2024, 8, 15, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 8, 22, 0, 0, 0, DateTimeKind.Utc), 29.7604, -95.3698, 32.2988, -90.1848, "Jackson",       "MS" },
                    { 22, demoUserId, 6,  "Estes Express",   "ES-2024-22024", "Delivered",  new DateTime(2024, 9,  1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 9,  8, 0, 0, 0, DateTimeKind.Utc), 29.7604, -95.3698, 33.5186, -86.8104, "Birmingham",    "AL" }
                });

            // -------------------------------------------------------
            // FiberInventoryTransactions — 30 records
            // Mix of Receive, Consume, Adjust, WriteOff across materials
            // Represents a realistic running history of stock movements
            // -------------------------------------------------------
            migrationBuilder.InsertData(
                table: "FiberInventoryTransactions",
                columns: new[] { "id", "user_id", "material_id", "transaction_type", "quantity", "qty_before_transaction", "qty_after_transaction", "notes", "transaction_date" },
                values: new object[,]
                {
                    // Aug 2024 — initial stock receives
                    { 1,  demoUserId, 1,  "Receive",  100m, 0m,    100m,  "Initial stock receipt — TexMat Q3 order",          new DateTime(2024, 8,  1, 0, 0, 0, DateTimeKind.Utc) },
                    { 2,  demoUserId, 2,  "Receive",   80m, 0m,     80m,  "Initial stock receipt — ResinWorks Q3 order",       new DateTime(2024, 8,  1, 0, 0, 0, DateTimeKind.Utc) },
                    { 3,  demoUserId, 3,  "Receive",   40m, 0m,     40m,  "Initial stock receipt — ChemCore Q3 order",         new DateTime(2024, 8,  1, 0, 0, 0, DateTimeKind.Utc) },
                    { 4,  demoUserId, 9,  "Receive",  500m, 0m,    500m,  "Initial stock receipt — DuctParts flanges",         new DateTime(2024, 8,  2, 0, 0, 0, DateTimeKind.Utc) },
                    { 5,  demoUserId, 10, "Receive",  250m, 0m,    250m,  "Initial stock receipt — DuctParts flanges 6in",     new DateTime(2024, 8,  2, 0, 0, 0, DateTimeKind.Utc) },

                    // Aug–Sep 2024 — consumption for orders 1–3
                    { 6,  demoUserId, 1,  "Consume",  -15m, 100m,  85m,  "Consumed for Order #1 — 4in Round Duct run",        new DateTime(2024, 8, 10, 0, 0, 0, DateTimeKind.Utc) },
                    { 7,  demoUserId, 2,  "Consume",  -30m,  80m,  50m,  "Consumed for Order #1 — resin for duct batch",      new DateTime(2024, 8, 10, 0, 0, 0, DateTimeKind.Utc) },
                    { 8,  demoUserId, 3,  "Consume",  -10m,  40m,  30m,  "Consumed for Order #1 — hardener",                  new DateTime(2024, 8, 10, 0, 0, 0, DateTimeKind.Utc) },
                    { 9,  demoUserId, 5,  "Consume",  -12m,  72m,  60m,  "Consumed for Order #2 — 6in Round Duct run",        new DateTime(2024, 8, 22, 0, 0, 0, DateTimeKind.Utc) },
                    { 10, demoUserId, 2,  "Consume",  -18m,  50m,  32m,  "Consumed for Order #2 — resin",                     new DateTime(2024, 8, 22, 0, 0, 0, DateTimeKind.Utc) },

                    // Sep 2024 — resin restock after running low
                    { 11, demoUserId, 2,  "Receive",   60m,  32m,  92m,  "Emergency restock — resin below reorder point",     new DateTime(2024, 9,  5, 0, 0, 0, DateTimeKind.Utc) },
                    { 12, demoUserId, 3,  "Receive",   30m,  30m,  60m,  "Restock — hardener MEKP from ChemCore",             new DateTime(2024, 9,  5, 0, 0, 0, DateTimeKind.Utc) },

                    // Sep–Oct 2024 — production consumes for orders 3–5
                    { 13, demoUserId, 4,  "Consume",   -8m,  53m,  45m,  "Consumed for Order #3 — release wax application",  new DateTime(2024, 9,  8, 0, 0, 0, DateTimeKind.Utc) },
                    { 14, demoUserId, 9,  "Consume", -120m, 500m, 380m,  "Consumed for Order #3 — 4in flanges installed",     new DateTime(2024, 9,  8, 0, 0, 0, DateTimeKind.Utc) },
                    { 15, demoUserId, 10, "Consume",  -80m, 250m, 170m,  "Consumed for Order #4 — 6in flanges installed",     new DateTime(2024, 9, 25, 0, 0, 0, DateTimeKind.Utc) },
                    { 16, demoUserId, 1,  "Consume",  -20m,  85m,  65m,  "Consumed for Order #5 — flanged fitting woven mat", new DateTime(2024, 10,12, 0, 0, 0, DateTimeKind.Utc) },
                    { 17, demoUserId, 6,  "Consume",   -5m,  27m,  22m,  "Consumed for Order #5 — gray pigment batch",        new DateTime(2024, 10,12, 0, 0, 0, DateTimeKind.Utc) },

                    // Oct 2024 — inventory adjustment after physical count
                    { 18, demoUserId, 8,  "Adjust",   -10m, 150m, 140m,  "Physical count adjustment — PVC core overstated",   new DateTime(2024, 10,31, 0, 0, 0, DateTimeKind.Utc) },
                    { 19, demoUserId, 11, "Adjust",    50m, 450m, 500m,  "Physical count adjustment — sandpaper understated", new DateTime(2024, 10,31, 0, 0, 0, DateTimeKind.Utc) },

                    // Nov 2024 — production consumes for orders 6–8
                    { 20, demoUserId, 5,  "Consume",  -22m,  60m,  38m,  "Consumed for Order #6 — 8in rect duct mat",         new DateTime(2024, 11, 2, 0, 0, 0, DateTimeKind.Utc) },
                    { 21, demoUserId, 2,  "Consume",  -40m,  92m,  52m,  "Consumed for Order #6 — resin for rect duct",       new DateTime(2024, 11, 2, 0, 0, 0, DateTimeKind.Utc) },
                    { 22, demoUserId, 3,  "Consume",  -20m,  60m,  40m,  "Consumed for Order #7 — custom hood hardener",      new DateTime(2024, 11, 8, 0, 0, 0, DateTimeKind.Utc) },
                    { 23, demoUserId, 7,  "Consume",   -8m,  14m,   6m,  "Consumed for Order #7 — white pigment finish coat", new DateTime(2024, 11, 8, 0, 0, 0, DateTimeKind.Utc) },

                    // Nov 2024 — restock ahead of large Dec orders
                    { 24, demoUserId, 2,  "Receive",   60m,  52m, 112m,  "Scheduled restock — resin for Q4 production",       new DateTime(2024, 11,15, 0, 0, 0, DateTimeKind.Utc) },
                    { 25, demoUserId, 3,  "Receive",   30m,  40m,  70m,  "Scheduled restock — hardener for Q4 production",    new DateTime(2024, 11,15, 0, 0, 0, DateTimeKind.Utc) },
                    { 26, demoUserId, 10, "Receive",  200m, 170m, 370m,  "Restock — 6in flanges from DuctParts",              new DateTime(2024, 11,20, 0, 0, 0, DateTimeKind.Utc) },

                    // Dec 2024 — heavy production consumes for orders 9–13
                    { 27, demoUserId, 1,  "Consume",  -30m,  65m,  35m,  "Consumed for Orders #9–10 — duct runs",             new DateTime(2024, 12, 5, 0, 0, 0, DateTimeKind.Utc) },
                    { 28, demoUserId, 2,  "Consume", -100m, 112m,  12m,  "Consumed for Orders #9–13 — resin batch",           new DateTime(2024, 12, 5, 0, 0, 0, DateTimeKind.Utc) },
                    { 29, demoUserId, 3,  "Consume",  -62m,  70m,   8m,  "Consumed for Orders #9–13 — hardener",              new DateTime(2024, 12, 5, 0, 0, 0, DateTimeKind.Utc) },

                    // Dec 2024 — write-off for damaged pigment
                    { 30, demoUserId, 7,  "WriteOff",  -8m,   6m,  -2m,  "WriteOff — pigment white contaminated batch",        new DateTime(2024, 12,20, 0, 0, 0, DateTimeKind.Utc) },

                    // Jan 2025 — partial restock bringing resin back above zero but still low
                    { 31, demoUserId, 2,  "Receive",   12m,  12m,  12m,  "Partial receive — resin backorder partial fill",     new DateTime(2025, 1,  8, 0, 0, 0, DateTimeKind.Utc) },
                    { 32, demoUserId, 3,  "Receive",    8m,   8m,   8m,  "Partial receive — hardener backorder partial fill",  new DateTime(2025, 1,  8, 0, 0, 0, DateTimeKind.Utc) },

                    // Jan 2025 — consumption for current In Production orders
                    { 33, demoUserId, 5,  "Consume",  -10m,  38m,  28m,  "Consumed for Order #16 — 6in duct mat",             new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc) },
                    { 34, demoUserId, 6,  "Consume",   -6m,  22m,  16m,  "Consumed for Order #17 — gray pigment",             new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc) },
                    { 35, demoUserId, 10, "Consume", -282m, 370m,  88m,  "Consumed for Orders #16–22 — 6in flanges",          new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "FiberInventoryTransactions",
                keyColumn: "id",
                keyValues: new object[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35 });

            migrationBuilder.DeleteData(
                table: "FiberShipments",
                keyColumn: "id",
                keyValues: new object[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 });

            migrationBuilder.DeleteData(
                table: "FiberOrders",
                keyColumn: "id",
                keyValues: new object[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30 });

            migrationBuilder.DeleteData(
                table: "FiberMaterials",
                keyColumn: "id",
                keyValues: new object[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 });

            migrationBuilder.DeleteData(
                table: "FiberClients",
                keyColumn: "id",
                keyValues: new object[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
        }
    }
}