using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Repositories;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Golden
{
    // Golden output for UserProfileSeedService, captured before its single 1,169-line method was
    // split. The seed is fixed data, so the exact rows a fresh user receives can be
    // pinned: per-entity counts, an ordered text of every business field (no ids, no
    // timestamps), idempotency, and isolation between users.
    //
    // The Properties table is global (no UserId), so it is asserted in its own test.
    public class UserProfileSeedGoldenTests
    {
        private static PortfolioDbContext NewDb() =>
            new(new DbContextOptionsBuilder<PortfolioDbContext>()
                .UseInMemoryDatabase($"GoldenSeed_{Guid.NewGuid()}")
                .Options);

        private static UserProfileSeedService NewService(PortfolioDbContext db) =>
            new(db, NullLogger<UserProfileSeedService>.Instance);

        private static string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);
        private static string N(decimal v) => v.ToString(CultureInfo.InvariantCulture);
        private static string N(double? v) => v is null ? "null" : N(v.Value);

        // Business fields only, one line per row, rows sorted ordinally so the text does
        // not depend on insertion order or on database-assigned ids.
        internal static async Task<string> CanonicalAsync(PortfolioDbContext db, Guid userId)
        {
            var lines = new List<string>();

            void Section(string title, IEnumerable<string> rows)
            {
                lines.Add($"## {title}");
                lines.AddRange(rows.OrderBy(r => r, StringComparer.Ordinal));
            }

            Section("clients", (await db.FiberClients.Where(c => c.UserId == userId).ToListAsync())
                .Select(c => $"{c.Name}|{c.ContactName}|{c.Email}|{c.Phone}|{c.City}|{c.State}|{N(c.Latitude)}|{N(c.Longitude)}"));

            Section("materials", (await db.FiberMaterials.Where(m => m.UserId == userId).ToListAsync())
                .Select(m => $"{m.Sku}|{m.Name}|{m.UnitOfMeasure}|{m.Category}|{N(m.QtyOnHand)}|{N(m.ReorderPoint)}|{N(m.ReorderQty)}|{N(m.UnitCost)}|{m.Supplier}|{m.WarehouseLocation}"));

            Section("orders", (await db.FiberOrders.Where(o => o.UserId == userId).ToListAsync())
                .Select(o => $"{o.OrderNumber}|{o.ClientName}|{o.ProductName}|{o.Quantity}|{N(o.UnitPrice)}|{o.Status}|{N(o.ClientLat)}|{N(o.ClientLng)}"));

            Section("shipments", (await db.FiberShipments.Where(s => s.UserId == userId).ToListAsync())
                .Select(s => $"{s.TrackingNumber}|{s.CarrierName}|{s.Status}|{N(s.OriginLat)}|{N(s.OriginLng)}|{N(s.DestinationLat)}|{N(s.DestinationLng)}|{s.DestinationCity}|{s.DestinationState}|{s.RouteJson ?? "null"}"));

            var skuById = (await db.FiberMaterials.Where(m => m.UserId == userId).ToListAsync())
                .ToDictionary(m => m.Id, m => m.Sku);
            Section("transactions", (await db.FiberInventoryTransactions.Where(t => t.UserId == userId).ToListAsync())
                .Select(t => $"{skuById[t.MaterialId]}|{t.TransactionType}|{N(t.Quantity)}|{N(t.QtyBeforeTransaction)}|{N(t.QtyAfterTransaction)}|{t.Notes ?? "null"}"));

            return string.Join("\n", lines);
        }

        [Fact]
        public async Task FreshUser_GetsPinnedRowCountsPerEntity()
        {
            // Arrange
            using var db = NewDb();
            var userId = Guid.NewGuid();

            // Act
            await NewService(db).SeedForUserAsync(userId);

            // Assert
            Assert.Equal(12, await db.FiberClients.CountAsync(c => c.UserId == userId));
            Assert.Equal(12, await db.FiberMaterials.CountAsync(m => m.UserId == userId));
            Assert.Equal(17, await db.FiberOrders.CountAsync(o => o.UserId == userId));
            Assert.Equal(11, await db.FiberShipments.CountAsync(s => s.UserId == userId));
            Assert.Equal(23, await db.FiberInventoryTransactions.CountAsync(t => t.UserId == userId));
        }

        [Fact]
        public async Task FreshUser_BusinessFieldsMatchPinnedText()
        {
            // Arrange
            using var db = NewDb();
            var userId = Guid.NewGuid();

            // Act
            await NewService(db).SeedForUserAsync(userId);

            // Assert
            var actual = await CanonicalAsync(db, userId);
            Assert.Equal(ExpectedCanonical.ReplaceLineEndings("\n"), actual);
        }

        [Fact]
        public async Task SeedingTwice_AddsNothing()
        {
            // Arrange
            using var db = NewDb();
            var userId = Guid.NewGuid();
            var service = NewService(db);

            await service.SeedForUserAsync(userId);
            var before = await CanonicalAsync(db, userId);
            var totalBefore = await TotalRowsAsync(db);

            // Act
            await service.SeedForUserAsync(userId);

            // Assert
            Assert.Equal(totalBefore, await TotalRowsAsync(db));
            Assert.Equal(before, await CanonicalAsync(db, userId));
        }

        [Fact]
        public async Task SeedingUserB_LeavesUserAUntouched_AndGivesBTheSameBusinessData()
        {
            // Arrange
            using var db = NewDb();
            var userA = Guid.NewGuid();
            var userB = Guid.NewGuid();
            var service = NewService(db);

            await service.SeedForUserAsync(userA);
            var aBefore = await CanonicalAsync(db, userA);

            // Act
            await service.SeedForUserAsync(userB);

            // Assert
            Assert.Equal(aBefore, await CanonicalAsync(db, userA));
            Assert.Equal(aBefore, await CanonicalAsync(db, userB));
            Assert.Equal(12, await db.FiberClients.CountAsync(c => c.UserId == userA));
            Assert.Equal(12, await db.FiberClients.CountAsync(c => c.UserId == userB));
            Assert.Equal(0, await db.FiberOrders.CountAsync(o => o.UserId != userA && o.UserId != userB));
            Assert.Equal(0, await db.FiberInventoryTransactions.CountAsync(t => t.UserId != userA && t.UserId != userB));
        }

        // Properties has no UserId. Decision D-07 removed the legacy Properties block from
        // the seeder (a migration replaced those listings), so seeding adds none.
        [Fact]
        public async Task Seeding_AddsNoProperties()
        {
            // Arrange
            using var db = NewDb();
            var service = NewService(db);

            // Act
            await service.SeedForUserAsync(Guid.NewGuid());
            await service.SeedForUserAsync(Guid.NewGuid());

            // Assert
            Assert.Equal(0, await db.Properties.CountAsync());
        }

        private static async Task<int> TotalRowsAsync(PortfolioDbContext db) =>
            await db.FiberClients.CountAsync() + await db.FiberMaterials.CountAsync() +
            await db.FiberOrders.CountAsync() + await db.FiberShipments.CountAsync() +
            await db.FiberInventoryTransactions.CountAsync() + await db.Properties.CountAsync();

        private const string ExpectedCanonical = """
            ## clients
            Appalachian Containment Systems|Tom Brightwell|tbrightwell@appalcs.com|304-555-0966|Charleston|WV|38.3498|-81.6326
            Bayou Industrial Group|Sandra Thibodaux|sthibodaux@bayouind.com|504-555-0234|Baton Rouge|LA|30.4515|-91.1871
            Coastal Pipe & Fittings|Marcus Webb|mwebb@coastalpipe.com|251-555-0528|Mobile|AL|30.6954|-88.0399
            Delta Tank & Vessel|Patricia Nguyen|pnguyen@deltatank.com|601-555-0417|Jackson|MS|32.2988|-90.1848
            Great Lakes Fiberglass Works|Amy Kowalski|akowalski@glfworks.com|313-555-0177|Detroit|MI|42.3314|-83.0458
            Gulf Coast Chemical|Linda Martinez|linda@gulfchem.com|713-555-0101|Houston|TX|29.7604|-95.3698
            Lone Star Refining|James Carter|jcarter@lonestarref.com|409-555-0112|Beaumont|TX|30.086|-94.1018
            Pacific Rim Tank Systems|Daniel Hwang|dhwang@pacrimtank.com|503-555-0288|Portland|OR|45.5051|-122.675
            Permian Basin Composites|Rick Holloway|rholloway@pbcomposites.com|432-555-0309|Midland|TX|31.9974|-102.0779
            Saguaro Structural Composites|Elena Ruiz|eruiz@saguarosc.com|602-555-0855|Phoenix|AZ|33.4484|-112.074
            Sunbelt Process Equipment|Donna Kline|dkline@sunbeltpe.com|205-555-0633|Birmingham|AL|33.5186|-86.8104
            Tri-State Corrosion Solutions|Kevin Ostrowski|kostrowski@tristatecorr.com|918-555-0744|Tulsa|OK|36.154|-95.9928
            ## materials
            RM-AJ|Acetone Solvent (55-gal)|drums|Solvents|7|2|4|195.00|ChemSource Direct|C3
            RM-CF|Carbon Fiber Tow 12K|spools|Reinforcements|15|4|8|185.00|Apex Composites LLC|A4
            RM-CR|Isophthalic Corrosion Liner Resin|drums|Resins|9|4|8|510.00|ChemSource Direct|B3
            RM-CSM|Chopped Strand Mat 1.5 oz|rolls|Reinforcements|120|30|60|28.50|TexMat Co|A2
            RM-FC|Foam Core (Divinycell H80)|sheets|Cores|60|15|30|74.25|Gulf Fiber Supply|D1
            RM-FGW|Fiberglass Woven Roving|rolls|Reinforcements|85|20|40|42.00|TexMat Co|A1
            RM-GC|Gel Coat (White ISO)|gallons|Gel Coats|55|12|24|38.50|TexMat Co|C2
            RM-GV|Fiberglass Veil (C-Glass)|rolls|Reinforcements|44|10|20|58.75|Gulf Fiber Supply|A3
            RM-MK|MEKP Catalyst|gallons|Catalysts|32|8|16|22.00|ChemSource Direct|C1
            RM-OR|Orthophthalic Polyester Resin|drums|Resins|24|6|12|295.00|ChemSource Direct|B2
            RM-SF|Stainless Steel Flange Inserts 4"|each||200|40|80|11.25|Apex Composites LLC|E1
            RM-VE|Vinyl Ester Resin|drums|Resins|18|5|10|415.00|ChemSource Direct|B1
            ## orders
            |Appalachian Containment Systems|2500-Gallon FRP Double-Wall Tank|3|11200.00|Pending|null|null
            |Bayou Industrial Group|500-Gallon FRP Containment Basin|10|3200.00|Delivered|null|null
            |Bayou Industrial Group|FRP Grating — Clear Span 1" x 4"|300|41.00|Delivered|null|null
            |Coastal Pipe & Fittings|12" FRP Straight Pipe (20 ft)|60|165.00|Shipped|null|null
            |Delta Tank & Vessel|1000-Gallon Vinyl Ester Tank|6|5400.00|Delivered|null|null
            |Great Lakes Fiberglass Works|10" Round Fiberglass Duct|90|44.00|Delivered|null|null
            |Gulf Coast Chemical|3000-Gallon FRP Storage Tank|4|8750.00|Delivered|null|null
            |Gulf Coast Chemical|4" Round Fiberglass Duct|200|14.50|Delivered|null|null
            |Gulf Coast Chemical|6" Vinyl Ester Elbow 90°|80|62.00|In Production|null|null
            |Lone Star Refining|6" Round Fiberglass Duct|120|22.75|Shipped|null|null
            |Lone Star Refining|8" FRP Straight Pipe (20 ft)|150|88.00|Delivered|null|null
            |Pacific Rim Tank Systems|5000-Gallon FRP Chemical Storage Tank|2|17800.00|Shipped|null|null
            |Permian Basin Composites|4" FRP Flange Set (Pair)|200|38.50|Pending|null|null
            |Permian Basin Composites|4" FRP Straight Pipe (20 ft)|400|54.00|Delivered|null|null
            |Saguaro Structural Composites|FRP Composite Panel 4' x 8' x 1/4"|250|78.00|Delivered|null|null
            |Sunbelt Process Equipment|FRP Scrubber Tower 24" Dia|2|14500.00|In Production|null|null
            |Tri-State Corrosion Solutions|FRP Structural I-Beam 4"|100|92.00|Delivered|null|null
            ## shipments
            FX-2024-10041|FedEx Freight|Delivered|29.7604|-95.3698|29.7604|-95.3698|Houston|TX|null
            FX-2024-10558|FedEx Freight|Delivered|29.7604|-95.3698|30.086|-94.1018|Beaumont|TX|null
            FX-2024-19984|FedEx Freight|Delivered|29.7604|-95.3698|32.2988|-90.1848|Jackson|MS|null
            FX-2025-20815|FedEx Freight|In Transit|29.7604|-95.3698|45.5051|-122.675|Portland|OR|null
            RL-2024-44102|R+L Carriers|Delivered|29.7604|-95.3698|30.4515|-91.1871|Baton Rouge|LA|null
            RL-2024-55988|R+L Carriers|Delivered|29.7604|-95.3698|30.4515|-91.1871|Baton Rouge|LA|null
            RL-2024-63310|R+L Carriers|Delivered|29.7604|-95.3698|36.154|-95.9928|Tulsa|OK|null
            UPF-2024-77541|UPS Freight|Delivered|29.7604|-95.3698|42.3314|-83.0458|Detroit|MI|null
            UPF-2025-00342|UPS Freight|In Transit|29.7604|-95.3698|30.6954|-88.0399|Mobile|AL|null
            XPO-2024-33471|XPO Logistics|Delivered|29.7604|-95.3698|31.9974|-102.0779|Midland|TX|null
            XPO-2024-44892|XPO Logistics|Delivered|29.7604|-95.3698|33.4484|-112.074|Phoenix|AZ|null
            ## transactions
            RM-CF|Consume|-5|20|15|Consumed — Tri-State structural beam prototype layup
            RM-CF|Receive|20|0|20|Carbon fiber tow — Apex Composites pilot order
            RM-CR|Consume|-1|10|9|Consumed — Gulf Coast FT-3000 tank liner coats
            RM-CR|Receive|10|0|10|Initial corrosion liner resin — ChemSource
            RM-CSM|Consume|-30|150|120|Consumed — Bayou FT-500 containment basin build
            RM-CSM|Receive|150|0|150|Initial CSM stock — TexMat Q3 shipment
            RM-FC|Consume|-20|80|60|Consumed — Saguaro sandwich panel core material
            RM-FC|Receive|80|0|80|Foam core initial stock — Gulf Fiber Supply
            RM-FGW|Consume|-25|110|85|Consumed for Lone Star FP-8S production run
            RM-FGW|Consume|-30|100|70|Consumed for Gulf Coast FD-4R production run
            RM-FGW|Receive|100|0|100|Initial stock receipt — TexMat Q3 order
            RM-FGW|Receive|40|70|110|TexMat Q4 restock received — Warehouse A1
            RM-GC|Consume|-5|60|55|Consumed — Gulf Coast tank exterior finish coats
            RM-GC|Receive|60|0|60|White ISO gel coat — TexMat first shipment
            RM-GV|Consume|-6|50|44|Consumed — Permian Basin FP-4S inner liner lamination
            RM-GV|Receive|50|0|50|Gulf Fiber Supply initial veil delivery
            RM-MK|Consume|-8|40|32|Consumed — routine production Q4 cure cycles
            RM-MK|Receive|40|0|40|MEKP catalyst — initial ChemSource order
            RM-OR|Consume|-6|30|24|Consumed — Saguaro composite panel production batch
            RM-OR|Receive|30|0|30|ChemSource initial ortho resin delivery
            RM-VE|Adjustment|3|15|18|Qty adjustment after Q4 physical inventory count
            RM-VE|Consume|-5|20|15|Consumed — Delta Tank FT-1000VE production
            RM-VE|Receive|20|0|20|ChemSource initial vinyl ester delivery
            """;
    }
}
