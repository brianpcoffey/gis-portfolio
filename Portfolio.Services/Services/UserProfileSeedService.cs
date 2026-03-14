using Microsoft.EntityFrameworkCore;
using Portfolio.Common.Models;
using Portfolio.Repositories;

namespace Portfolio.Services.Services;

public class UserProfileSeedService
{
    private readonly PortfolioDbContext _db;

    public UserProfileSeedService(PortfolioDbContext db)
    {
        _db = db;
    }

    public async Task SeedForUserAsync(Guid userId)
    {
        using var tx = await _db.Database.BeginTransactionAsync();

        // ===================================================
        // FIBER CLIENTS
        // ===================================================

        // =================================================== 
        // FIBER CLIENTS
        // ===================================================

        if (!await _db.FiberClients.AnyAsync(c => c.UserId == userId && c.Name == "Gulf Coast Chemical"))
        {
            _db.FiberClients.Add(new FiberClient
            {
                UserId = userId,
                Name = "Gulf Coast Chemical",
                ContactName = "Linda Martinez",
                Email = "linda@gulfchem.com",
                Phone = "713-555-0101",
                City = "Houston",
                State = "TX",
                Latitude = 29.7604,
                Longitude = -95.3698,
                CreatedDate = DateTime.Parse("2024-01-05T00:00:00Z").ToUniversalTime()
            });
        }

        if (!await _db.FiberClients.AnyAsync(c => c.UserId == userId && c.Name == "Lone Star Refining"))
        {
            _db.FiberClients.Add(new FiberClient
            {
                UserId = userId,
                Name = "Lone Star Refining",
                ContactName = "James Carter",
                Email = "jcarter@lonestarref.com",
                Phone = "409-555-0112",
                City = "Beaumont",
                State = "TX",
                Latitude = 30.0860,
                Longitude = -94.1018,
                CreatedDate = DateTime.Parse("2024-01-12T00:00:00Z").ToUniversalTime()
            });
        }

        if (!await _db.FiberClients.AnyAsync(c => c.UserId == userId && c.Name == "Bayou Industrial Group"))
        {
            _db.FiberClients.Add(new FiberClient
            {
                UserId = userId,
                Name = "Bayou Industrial Group",
                ContactName = "Sandra Thibodaux",
                Email = "sthibodaux@bayouind.com",
                Phone = "504-555-0234",
                City = "Baton Rouge",
                State = "LA",
                Latitude = 30.4515,
                Longitude = -91.1871,
                CreatedDate = DateTime.Parse("2024-02-03T00:00:00Z").ToUniversalTime()
            });
        }

        if (!await _db.FiberClients.AnyAsync(c => c.UserId == userId && c.Name == "Permian Basin Composites"))
        {
            _db.FiberClients.Add(new FiberClient
            {
                UserId = userId,
                Name = "Permian Basin Composites",
                ContactName = "Rick Holloway",
                Email = "rholloway@pbcomposites.com",
                Phone = "432-555-0309",
                City = "Midland",
                State = "TX",
                Latitude = 31.9974,
                Longitude = -102.0779,
                CreatedDate = DateTime.Parse("2024-02-18T00:00:00Z").ToUniversalTime()
            });
        }

        if (!await _db.FiberClients.AnyAsync(c => c.UserId == userId && c.Name == "Delta Tank & Vessel"))
        {
            _db.FiberClients.Add(new FiberClient
            {
                UserId = userId,
                Name = "Delta Tank & Vessel",
                ContactName = "Patricia Nguyen",
                Email = "pnguyen@deltatank.com",
                Phone = "601-555-0417",
                City = "Jackson",
                State = "MS",
                Latitude = 32.2988,
                Longitude = -90.1848,
                CreatedDate = DateTime.Parse("2024-03-07T00:00:00Z").ToUniversalTime()
            });
        }

        if (!await _db.FiberClients.AnyAsync(c => c.UserId == userId && c.Name == "Coastal Pipe & Fittings"))
        {
            _db.FiberClients.Add(new FiberClient
            {
                UserId = userId,
                Name = "Coastal Pipe & Fittings",
                ContactName = "Marcus Webb",
                Email = "mwebb@coastalpipe.com",
                Phone = "251-555-0528",
                City = "Mobile",
                State = "AL",
                Latitude = 30.6954,
                Longitude = -88.0399,
                CreatedDate = DateTime.Parse("2024-03-22T00:00:00Z").ToUniversalTime()
            });
        }

        if (!await _db.FiberClients.AnyAsync(c => c.UserId == userId && c.Name == "Sunbelt Process Equipment"))
        {
            _db.FiberClients.Add(new FiberClient
            {
                UserId = userId,
                Name = "Sunbelt Process Equipment",
                ContactName = "Donna Kline",
                Email = "dkline@sunbeltpe.com",
                Phone = "205-555-0633",
                City = "Birmingham",
                State = "AL",
                Latitude = 33.5186,
                Longitude = -86.8104,
                CreatedDate = DateTime.Parse("2024-04-10T00:00:00Z").ToUniversalTime()
            });
        }

        if (!await _db.FiberClients.AnyAsync(c => c.UserId == userId && c.Name == "Tri-State Corrosion Solutions"))
        {
            _db.FiberClients.Add(new FiberClient
            {
                UserId = userId,
                Name = "Tri-State Corrosion Solutions",
                ContactName = "Kevin Ostrowski",
                Email = "kostrowski@tristatecorr.com",
                Phone = "918-555-0744",
                City = "Tulsa",
                State = "OK",
                Latitude = 36.1540,
                Longitude = -95.9928,
                CreatedDate = DateTime.Parse("2024-04-29T00:00:00Z").ToUniversalTime()
            });
        }

        if (!await _db.FiberClients.AnyAsync(c => c.UserId == userId && c.Name == "Saguaro Structural Composites"))
        {
            _db.FiberClients.Add(new FiberClient
            {
                UserId = userId,
                Name = "Saguaro Structural Composites",
                ContactName = "Elena Ruiz",
                Email = "eruiz@saguarosc.com",
                Phone = "602-555-0855",
                City = "Phoenix",
                State = "AZ",
                Latitude = 33.4484,
                Longitude = -112.0740,
                CreatedDate = DateTime.Parse("2024-05-14T00:00:00Z").ToUniversalTime()
            });
        }

        if (!await _db.FiberClients.AnyAsync(c => c.UserId == userId && c.Name == "Appalachian Containment Systems"))
        {
            _db.FiberClients.Add(new FiberClient
            {
                UserId = userId,
                Name = "Appalachian Containment Systems",
                ContactName = "Tom Brightwell",
                Email = "tbrightwell@appalcs.com",
                Phone = "304-555-0966",
                City = "Charleston",
                State = "WV",
                Latitude = 38.3498,
                Longitude = -81.6326,
                CreatedDate = DateTime.Parse("2024-06-02T00:00:00Z").ToUniversalTime()
            });
        }

        if (!await _db.FiberClients.AnyAsync(c => c.UserId == userId && c.Name == "Great Lakes Fiberglass Works"))
        {
            _db.FiberClients.Add(new FiberClient
            {
                UserId = userId,
                Name = "Great Lakes Fiberglass Works",
                ContactName = "Amy Kowalski",
                Email = "akowalski@glfworks.com",
                Phone = "313-555-0177",
                City = "Detroit",
                State = "MI",
                Latitude = 42.3314,
                Longitude = -83.0458,
                CreatedDate = DateTime.Parse("2024-06-20T00:00:00Z").ToUniversalTime()
            });
        }

        if (!await _db.FiberClients.AnyAsync(c => c.UserId == userId && c.Name == "Pacific Rim Tank Systems"))
        {
            _db.FiberClients.Add(new FiberClient
            {
                UserId = userId,
                Name = "Pacific Rim Tank Systems",
                ContactName = "Daniel Hwang",
                Email = "dhwang@pacrimtank.com",
                Phone = "503-555-0288",
                City = "Portland",
                State = "OR",
                Latitude = 45.5051,
                Longitude = -122.6750,
                CreatedDate = DateTime.Parse("2024-07-08T00:00:00Z").ToUniversalTime()
            });
        }

        await _db.SaveChangesAsync();

        // ===================================================
        // FIBER MATERIALS
        // ===================================================

        if (!await _db.FiberMaterials.AnyAsync(m => m.UserId == userId && m.Sku == "RM-FGW"))
        {
            _db.FiberMaterials.Add(new FiberMaterial
            {
                UserId = userId,
                Name = "Fiberglass Woven Roving",
                Sku = "RM-FGW",
                UnitOfMeasure = "rolls",
                QtyOnHand = 85,
                ReorderPoint = 20,
                ReorderQty = 40,
                UnitCost = 42.00m,
                Supplier = "TexMat Co",
                WarehouseLocation = "A1",
                LastUpdated = DateTime.Parse("2025-01-10T00:00:00Z").ToUniversalTime()
            });
        }

        if (!await _db.FiberMaterials.AnyAsync(m => m.UserId == userId && m.Sku == "RM-CSM"))
        {
            _db.FiberMaterials.Add(new FiberMaterial
            {
                UserId = userId,
                Name = "Chopped Strand Mat 1.5 oz",
                Sku = "RM-CSM",
                UnitOfMeasure = "rolls",
                QtyOnHand = 120,
                ReorderPoint = 30,
                ReorderQty = 60,
                UnitCost = 28.50m,
                Supplier = "TexMat Co",
                WarehouseLocation = "A2",
                LastUpdated = DateTime.Parse("2025-01-10T00:00:00Z").ToUniversalTime()
            });
        }

        if (!await _db.FiberMaterials.AnyAsync(m => m.UserId == userId && m.Sku == "RM-VE"))
        {
            _db.FiberMaterials.Add(new FiberMaterial
            {
                UserId = userId,
                Name = "Vinyl Ester Resin",
                Sku = "RM-VE",
                UnitOfMeasure = "drums",
                QtyOnHand = 18,
                ReorderPoint = 5,
                ReorderQty = 10,
                UnitCost = 415.00m,
                Supplier = "ChemSource Direct",
                WarehouseLocation = "B1",
                LastUpdated = DateTime.Parse("2025-01-14T00:00:00Z").ToUniversalTime()
            });
        }

        if (!await _db.FiberMaterials.AnyAsync(m => m.UserId == userId && m.Sku == "RM-OR"))
        {
            _db.FiberMaterials.Add(new FiberMaterial
            {
                UserId = userId,
                Name = "Orthophthalic Polyester Resin",
                Sku = "RM-OR",
                UnitOfMeasure = "drums",
                QtyOnHand = 24,
                ReorderPoint = 6,
                ReorderQty = 12,
                UnitCost = 295.00m,
                Supplier = "ChemSource Direct",
                WarehouseLocation = "B2",
                LastUpdated = DateTime.Parse("2025-01-14T00:00:00Z").ToUniversalTime()
            });
        }

        if (!await _db.FiberMaterials.AnyAsync(m => m.UserId == userId && m.Sku == "RM-CR"))
        {
            _db.FiberMaterials.Add(new FiberMaterial
            {
                UserId = userId,
                Name = "Isophthalic Corrosion Liner Resin",
                Sku = "RM-CR",
                UnitOfMeasure = "drums",
                QtyOnHand = 9,
                ReorderPoint = 4,
                ReorderQty = 8,
                UnitCost = 510.00m,
                Supplier = "ChemSource Direct",
                WarehouseLocation = "B3",
                LastUpdated = DateTime.Parse("2025-01-18T00:00:00Z").ToUniversalTime()
            });
        }

        if (!await _db.FiberMaterials.AnyAsync(m => m.UserId == userId && m.Sku == "RM-GV"))
        {
            _db.FiberMaterials.Add(new FiberMaterial
            {
                UserId = userId,
                Name = "Fiberglass Veil (C-Glass)",
                Sku = "RM-GV",
                UnitOfMeasure = "rolls",
                QtyOnHand = 44,
                ReorderPoint = 10,
                ReorderQty = 20,
                UnitCost = 58.75m,
                Supplier = "Gulf Fiber Supply",
                WarehouseLocation = "A3",
                LastUpdated = DateTime.Parse("2025-01-20T00:00:00Z").ToUniversalTime()
            });
        }

        if (!await _db.FiberMaterials.AnyAsync(m => m.UserId == userId && m.Sku == "RM-MK"))
        {
            _db.FiberMaterials.Add(new FiberMaterial
            {
                UserId = userId,
                Name = "MEKP Catalyst",
                Sku = "RM-MK",
                UnitOfMeasure = "gallons",
                QtyOnHand = 32,
                ReorderPoint = 8,
                ReorderQty = 16,
                UnitCost = 22.00m,
                Supplier = "ChemSource Direct",
                WarehouseLocation = "C1",
                LastUpdated = DateTime.Parse("2025-01-22T00:00:00Z").ToUniversalTime()
            });
        }

        if (!await _db.FiberMaterials.AnyAsync(m => m.UserId == userId && m.Sku == "RM-CF"))
        {
            _db.FiberMaterials.Add(new FiberMaterial
            {
                UserId = userId,
                Name = "Carbon Fiber Tow 12K",
                Sku = "RM-CF",
                UnitOfMeasure = "spools",
                QtyOnHand = 15,
                ReorderPoint = 4,
                ReorderQty = 8,
                UnitCost = 185.00m,
                Supplier = "Apex Composites LLC",
                WarehouseLocation = "A4",
                LastUpdated = DateTime.Parse("2025-02-01T00:00:00Z").ToUniversalTime()
            });
        }

        if (!await _db.FiberMaterials.AnyAsync(m => m.UserId == userId && m.Sku == "RM-FC"))
        {
            _db.FiberMaterials.Add(new FiberMaterial
            {
                UserId = userId,
                Name = "Foam Core (Divinycell H80)",
                Sku = "RM-FC",
                UnitOfMeasure = "sheets",
                QtyOnHand = 60,
                ReorderPoint = 15,
                ReorderQty = 30,
                UnitCost = 74.25m,
                Supplier = "Gulf Fiber Supply",
                WarehouseLocation = "D1",
                LastUpdated = DateTime.Parse("2025-02-05T00:00:00Z").ToUniversalTime()
            });
        }

        if (!await _db.FiberMaterials.AnyAsync(m => m.UserId == userId && m.Sku == "RM-GC"))
        {
            _db.FiberMaterials.Add(new FiberMaterial
            {
                UserId = userId,
                Name = "Gel Coat (White ISO)",
                Sku = "RM-GC",
                UnitOfMeasure = "gallons",
                QtyOnHand = 55,
                ReorderPoint = 12,
                ReorderQty = 24,
                UnitCost = 38.50m,
                Supplier = "TexMat Co",
                WarehouseLocation = "C2",
                LastUpdated = DateTime.Parse("2025-02-08T00:00:00Z").ToUniversalTime()
            });
        }

        if (!await _db.FiberMaterials.AnyAsync(m => m.UserId == userId && m.Sku == "RM-AJ"))
        {
            _db.FiberMaterials.Add(new FiberMaterial
            {
                UserId = userId,
                Name = "Acetone Solvent (55-gal)",
                Sku = "RM-AJ",
                UnitOfMeasure = "drums",
                QtyOnHand = 7,
                ReorderPoint = 2,
                ReorderQty = 4,
                UnitCost = 195.00m,
                Supplier = "ChemSource Direct",
                WarehouseLocation = "C3",
                LastUpdated = DateTime.Parse("2025-02-10T00:00:00Z").ToUniversalTime()
            });
        }

        if (!await _db.FiberMaterials.AnyAsync(m => m.UserId == userId && m.Sku == "RM-SF"))
        {
            _db.FiberMaterials.Add(new FiberMaterial
            {
                UserId = userId,
                Name = "Stainless Steel Flange Inserts 4\"",
                Sku = "RM-SF",
                UnitOfMeasure = "each",
                QtyOnHand = 200,
                ReorderPoint = 40,
                ReorderQty = 80,
                UnitCost = 11.25m,
                Supplier = "Apex Composites LLC",
                WarehouseLocation = "E1",
                LastUpdated = DateTime.Parse("2025-02-12T00:00:00Z").ToUniversalTime()
            });
        }

        await _db.SaveChangesAsync();

        // ===================================================
        // FIBER ORDERS
        // ===================================================

        var gulfClient = await _db.FiberClients
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Name == "Gulf Coast Chemical");
        var loneStarClient = await _db.FiberClients
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Name == "Lone Star Refining");
        var bayouClient = await _db.FiberClients
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Name == "Bayou Industrial Group");
        var permianClient = await _db.FiberClients
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Name == "Permian Basin Composites");
        var deltaClient = await _db.FiberClients
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Name == "Delta Tank & Vessel");
        var coastalClient = await _db.FiberClients
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Name == "Coastal Pipe & Fittings");
        var sunbeltClient = await _db.FiberClients
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Name == "Sunbelt Process Equipment");
        var triStateClient = await _db.FiberClients
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Name == "Tri-State Corrosion Solutions");
        var saguaroClient = await _db.FiberClients
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Name == "Saguaro Structural Composites");
        var appalachianClient = await _db.FiberClients
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Name == "Appalachian Containment Systems");
        var greatLakesClient = await _db.FiberClients
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Name == "Great Lakes Fiberglass Works");
        var pacificRimClient = await _db.FiberClients
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Name == "Pacific Rim Tank Systems");

        if (gulfClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientId == gulfClient.Id && o.ProductSku == "FD-4R"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientId = gulfClient.Id,
                ProductName = "4\" Round Fiberglass Duct",
                ProductSku = "FD-4R",
                Quantity = 200,
                UnitPrice = 14.50m,
                TotalValue = 2900.00m,
                Status = "Delivered",
                OrderDate = DateTime.Parse("2024-08-05T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2024-08-14T00:00:00Z").ToUniversalTime()
            });
        }

        if (gulfClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientId == gulfClient.Id && o.ProductSku == "FT-3000"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientId = gulfClient.Id,
                ProductName = "3000-Gallon FRP Storage Tank",
                ProductSku = "FT-3000",
                Quantity = 4,
                UnitPrice = 8750.00m,
                TotalValue = 35000.00m,
                Status = "Delivered",
                OrderDate = DateTime.Parse("2024-09-12T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2024-09-28T00:00:00Z").ToUniversalTime()
            });
        }

        if (gulfClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientId == gulfClient.Id && o.ProductSku == "FE-VE6"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientId = gulfClient.Id,
                ProductName = "6\" Vinyl Ester Elbow 90°",
                ProductSku = "FE-VE6",
                Quantity = 80,
                UnitPrice = 62.00m,
                TotalValue = 4960.00m,
                Status = "In Production",
                OrderDate = DateTime.Parse("2025-02-14T00:00:00Z").ToUniversalTime(),
                ShipDate = null
            });
        }

        if (loneStarClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientId == loneStarClient.Id && o.ProductSku == "FP-8S"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientId = loneStarClient.Id,
                ProductName = "8\" FRP Straight Pipe (20 ft)",
                ProductSku = "FP-8S",
                Quantity = 150,
                UnitPrice = 88.00m,
                TotalValue = 13200.00m,
                Status = "Delivered",
                OrderDate = DateTime.Parse("2024-07-20T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2024-08-02T00:00:00Z").ToUniversalTime()
            });
        }

        if (loneStarClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientId == loneStarClient.Id && o.ProductSku == "FD-6R"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientId = loneStarClient.Id,
                ProductName = "6\" Round Fiberglass Duct",
                ProductSku = "FD-6R",
                Quantity = 120,
                UnitPrice = 22.75m,
                TotalValue = 2730.00m,
                Status = "Shipped",
                OrderDate = DateTime.Parse("2025-01-08T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2025-01-19T00:00:00Z").ToUniversalTime()
            });
        }

        if (bayouClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientId == bayouClient.Id && o.ProductSku == "FT-500"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientId = bayouClient.Id,
                ProductName = "500-Gallon FRP Containment Basin",
                ProductSku = "FT-500",
                Quantity = 10,
                UnitPrice = 3200.00m,
                TotalValue = 32000.00m,
                Status = "Delivered",
                OrderDate = DateTime.Parse("2024-06-01T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2024-06-20T00:00:00Z").ToUniversalTime()
            });
        }

        if (bayouClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientId == bayouClient.Id && o.ProductSku == "FG-CLR"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientId = bayouClient.Id,
                ProductName = "FRP Grating — Clear Span 1\" x 4\"",
                ProductSku = "FG-CLR",
                Quantity = 300,
                UnitPrice = 41.00m,
                TotalValue = 12300.00m,
                Status = "Delivered",
                OrderDate = DateTime.Parse("2024-10-05T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2024-10-18T00:00:00Z").ToUniversalTime()
            });
        }

        if (permianClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientId == permianClient.Id && o.ProductSku == "FP-4S"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientId = permianClient.Id,
                ProductName = "4\" FRP Straight Pipe (20 ft)",
                ProductSku = "FP-4S",
                Quantity = 400,
                UnitPrice = 54.00m,
                TotalValue = 21600.00m,
                Status = "Delivered",
                OrderDate = DateTime.Parse("2024-05-10T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2024-05-25T00:00:00Z").ToUniversalTime()
            });
        }

        if (permianClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientId == permianClient.Id && o.ProductSku == "FC-4FLG"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientId = permianClient.Id,
                ProductName = "4\" FRP Flange Set (Pair)",
                ProductSku = "FC-4FLG",
                Quantity = 200,
                UnitPrice = 38.50m,
                TotalValue = 7700.00m,
                Status = "Pending",
                OrderDate = DateTime.Parse("2025-03-01T00:00:00Z").ToUniversalTime(),
                ShipDate = null
            });
        }

        if (deltaClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientId == deltaClient.Id && o.ProductSku == "FT-1000VE"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientId = deltaClient.Id,
                ProductName = "1000-Gallon Vinyl Ester Tank",
                ProductSku = "FT-1000VE",
                Quantity = 6,
                UnitPrice = 5400.00m,
                TotalValue = 32400.00m,
                Status = "Delivered",
                OrderDate = DateTime.Parse("2024-11-14T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2024-12-01T00:00:00Z").ToUniversalTime()
            });
        }

        if (coastalClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientId == coastalClient.Id && o.ProductSku == "FP-12S"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientId = coastalClient.Id,
                ProductName = "12\" FRP Straight Pipe (20 ft)",
                ProductSku = "FP-12S",
                Quantity = 60,
                UnitPrice = 165.00m,
                TotalValue = 9900.00m,
                Status = "Shipped",
                OrderDate = DateTime.Parse("2025-01-22T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2025-02-05T00:00:00Z").ToUniversalTime()
            });
        }

        if (sunbeltClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientId == sunbeltClient.Id && o.ProductSku == "FH-SCR"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientId = sunbeltClient.Id,
                ProductName = "FRP Scrubber Tower 24\" Dia",
                ProductSku = "FH-SCR",
                Quantity = 2,
                UnitPrice = 14500.00m,
                TotalValue = 29000.00m,
                Status = "In Production",
                OrderDate = DateTime.Parse("2025-02-01T00:00:00Z").ToUniversalTime(),
                ShipDate = null
            });
        }

        if (triStateClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientId == triStateClient.Id && o.ProductSku == "FG-SF"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientId = triStateClient.Id,
                ProductName = "FRP Structural I-Beam 4\"",
                ProductSku = "FG-SF",
                Quantity = 100,
                UnitPrice = 92.00m,
                TotalValue = 9200.00m,
                Status = "Delivered",
                OrderDate = DateTime.Parse("2024-12-03T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2024-12-17T00:00:00Z").ToUniversalTime()
            });
        }

        if (saguaroClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientId == saguaroClient.Id && o.ProductSku == "FC-PNL"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientId = saguaroClient.Id,
                ProductName = "FRP Composite Panel 4' x 8' x 1/4\"",
                ProductSku = "FC-PNL",
                Quantity = 250,
                UnitPrice = 78.00m,
                TotalValue = 19500.00m,
                Status = "Delivered",
                OrderDate = DateTime.Parse("2024-09-28T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2024-10-12T00:00:00Z").ToUniversalTime()
            });
        }

        if (appalachianClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientId == appalachianClient.Id && o.ProductSku == "FT-2500"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientId = appalachianClient.Id,
                ProductName = "2500-Gallon FRP Double-Wall Tank",
                ProductSku = "FT-2500",
                Quantity = 3,
                UnitPrice = 11200.00m,
                TotalValue = 33600.00m,
                Status = "Pending",
                OrderDate = DateTime.Parse("2025-03-05T00:00:00Z").ToUniversalTime(),
                ShipDate = null
            });
        }

        if (greatLakesClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientId == greatLakesClient.Id && o.ProductSku == "FD-10R"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientId = greatLakesClient.Id,
                ProductName = "10\" Round Fiberglass Duct",
                ProductSku = "FD-10R",
                Quantity = 90,
                UnitPrice = 44.00m,
                TotalValue = 3960.00m,
                Status = "Delivered",
                OrderDate = DateTime.Parse("2024-08-22T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2024-09-04T00:00:00Z").ToUniversalTime()
            });
        }

        if (pacificRimClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientId == pacificRimClient.Id && o.ProductSku == "FT-5000"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientId = pacificRimClient.Id,
                ProductName = "5000-Gallon FRP Chemical Storage Tank",
                ProductSku = "FT-5000",
                Quantity = 2,
                UnitPrice = 17800.00m,
                TotalValue = 35600.00m,
                Status = "Shipped",
                OrderDate = DateTime.Parse("2025-01-30T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2025-02-18T00:00:00Z").ToUniversalTime()
            });
        }

        await _db.SaveChangesAsync();

        // ===================================================
        // FIBER SHIPMENTS
        // ===================================================

        var orderFD4R = await _db.FiberOrders.FirstOrDefaultAsync(o => o.UserId == userId && o.ProductSku == "FD-4R");
        var orderFT3000 = await _db.FiberOrders.FirstOrDefaultAsync(o => o.UserId == userId && o.ProductSku == "FT-3000");
        var orderFP8S = await _db.FiberOrders.FirstOrDefaultAsync(o => o.UserId == userId && o.ProductSku == "FP-8S");
        var orderFD6R = await _db.FiberOrders.FirstOrDefaultAsync(o => o.UserId == userId && o.ProductSku == "FD-6R");
        var orderFT500 = await _db.FiberOrders.FirstOrDefaultAsync(o => o.UserId == userId && o.ProductSku == "FT-500");
        var orderFGCLR = await _db.FiberOrders.FirstOrDefaultAsync(o => o.UserId == userId && o.ProductSku == "FG-CLR");
        var orderFP4S = await _db.FiberOrders.FirstOrDefaultAsync(o => o.UserId == userId && o.ProductSku == "FP-4S");
        var orderFT1000VE = await _db.FiberOrders.FirstOrDefaultAsync(o => o.UserId == userId && o.ProductSku == "FT-1000VE");
        var orderFP12S = await _db.FiberOrders.FirstOrDefaultAsync(o => o.UserId == userId && o.ProductSku == "FP-12S");
        var orderFGSF = await _db.FiberOrders.FirstOrDefaultAsync(o => o.UserId == userId && o.ProductSku == "FG-SF");
        var orderFCPNL = await _db.FiberOrders.FirstOrDefaultAsync(o => o.UserId == userId && o.ProductSku == "FC-PNL");
        var orderFD10R = await _db.FiberOrders.FirstOrDefaultAsync(o => o.UserId == userId && o.ProductSku == "FD-10R");
        var orderFT5000 = await _db.FiberOrders.FirstOrDefaultAsync(o => o.UserId == userId && o.ProductSku == "FT-5000");

        if (orderFD4R != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "FX-2024-10041"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                OrderId = orderFD4R.Id,
                CarrierName = "FedEx Freight",
                TrackingNumber = "FX-2024-10041",
                Status = "Delivered",
                ShipDate = DateTime.Parse("2024-08-14T00:00:00Z").ToUniversalTime(),
                EstimatedArrival = DateTime.Parse("2024-08-19T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 29.7604,
                DestinationLng = -95.3698,
                DestinationCity = "Houston",
                DestinationState = "TX"
            });
        }

        if (orderFT3000 != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "UPF-2024-88821"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                OrderId = orderFT3000.Id,
                CarrierName = "UPS Freight",
                TrackingNumber = "UPF-2024-88821",
                Status = "Delivered",
                ShipDate = DateTime.Parse("2024-09-28T00:00:00Z").ToUniversalTime(),
                EstimatedArrival = DateTime.Parse("2024-10-05T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 29.7604,
                DestinationLng = -95.3698,
                DestinationCity = "Houston",
                DestinationState = "TX"
            });
        }

        if (orderFP8S != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "FX-2024-10558"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                OrderId = orderFP8S.Id,
                CarrierName = "FedEx Freight",
                TrackingNumber = "FX-2024-10558",
                Status = "Delivered",
                ShipDate = DateTime.Parse("2024-08-02T00:00:00Z").ToUniversalTime(),
                EstimatedArrival = DateTime.Parse("2024-08-08T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 30.0860,
                DestinationLng = -94.1018,
                DestinationCity = "Beaumont",
                DestinationState = "TX"
            });
        }

        if (orderFD6R != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "XPO-2025-00117"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                OrderId = orderFD6R.Id,
                CarrierName = "XPO Logistics",
                TrackingNumber = "XPO-2025-00117",
                Status = "In Transit",
                ShipDate = DateTime.Parse("2025-01-19T00:00:00Z").ToUniversalTime(),
                EstimatedArrival = DateTime.Parse("2025-01-25T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 30.0860,
                DestinationLng = -94.1018,
                DestinationCity = "Beaumont",
                DestinationState = "TX"
            });
        }

        if (orderFT500 != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "RL-2024-44102"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                OrderId = orderFT500.Id,
                CarrierName = "R+L Carriers",
                TrackingNumber = "RL-2024-44102",
                Status = "Delivered",
                ShipDate = DateTime.Parse("2024-06-20T00:00:00Z").ToUniversalTime(),
                EstimatedArrival = DateTime.Parse("2024-06-27T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 30.4515,
                DestinationLng = -91.1871,
                DestinationCity = "Baton Rouge",
                DestinationState = "LA"
            });
        }

        if (orderFGCLR != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "RL-2024-55988"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                OrderId = orderFGCLR.Id,
                CarrierName = "R+L Carriers",
                TrackingNumber = "RL-2024-55988",
                Status = "Delivered",
                ShipDate = DateTime.Parse("2024-10-18T00:00:00Z").ToUniversalTime(),
                EstimatedArrival = DateTime.Parse("2024-10-24T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 30.4515,
                DestinationLng = -91.1871,
                DestinationCity = "Baton Rouge",
                DestinationState = "LA"
            });
        }

        if (orderFP4S != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "XPO-2024-33471"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                OrderId = orderFP4S.Id,
                CarrierName = "XPO Logistics",
                TrackingNumber = "XPO-2024-33471",
                Status = "Delivered",
                ShipDate = DateTime.Parse("2024-05-25T00:00:00Z").ToUniversalTime(),
                EstimatedArrival = DateTime.Parse("2024-06-02T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 31.9974,
                DestinationLng = -102.0779,
                DestinationCity = "Midland",
                DestinationState = "TX"
            });
        }

        if (orderFT1000VE != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "FX-2024-19984"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                OrderId = orderFT1000VE.Id,
                CarrierName = "FedEx Freight",
                TrackingNumber = "FX-2024-19984",
                Status = "Delivered",
                ShipDate = DateTime.Parse("2024-12-01T00:00:00Z").ToUniversalTime(),
                EstimatedArrival = DateTime.Parse("2024-12-08T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 32.2988,
                DestinationLng = -90.1848,
                DestinationCity = "Jackson",
                DestinationState = "MS"
            });
        }

        if (orderFP12S != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "UPF-2025-00342"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                OrderId = orderFP12S.Id,
                CarrierName = "UPS Freight",
                TrackingNumber = "UPF-2025-00342",
                Status = "In Transit",
                ShipDate = DateTime.Parse("2025-02-05T00:00:00Z").ToUniversalTime(),
                EstimatedArrival = DateTime.Parse("2025-02-12T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 30.6954,
                DestinationLng = -88.0399,
                DestinationCity = "Mobile",
                DestinationState = "AL"
            });
        }

        if (orderFGSF != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "RL-2024-63310"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                OrderId = orderFGSF.Id,
                CarrierName = "R+L Carriers",
                TrackingNumber = "RL-2024-63310",
                Status = "Delivered",
                ShipDate = DateTime.Parse("2024-12-17T00:00:00Z").ToUniversalTime(),
                EstimatedArrival = DateTime.Parse("2024-12-23T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 36.1540,
                DestinationLng = -95.9928,
                DestinationCity = "Tulsa",
                DestinationState = "OK"
            });
        }

        if (orderFCPNL != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "XPO-2024-44892"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                OrderId = orderFCPNL.Id,
                CarrierName = "XPO Logistics",
                TrackingNumber = "XPO-2024-44892",
                Status = "Delivered",
                ShipDate = DateTime.Parse("2024-10-12T00:00:00Z").ToUniversalTime(),
                EstimatedArrival = DateTime.Parse("2024-10-20T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 33.4484,
                DestinationLng = -112.0740,
                DestinationCity = "Phoenix",
                DestinationState = "AZ"
            });
        }

        if (orderFD10R != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "UPF-2024-77541"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                OrderId = orderFD10R.Id,
                CarrierName = "UPS Freight",
                TrackingNumber = "UPF-2024-77541",
                Status = "Delivered",
                ShipDate = DateTime.Parse("2024-09-04T00:00:00Z").ToUniversalTime(),
                EstimatedArrival = DateTime.Parse("2024-09-11T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 42.3314,
                DestinationLng = -83.0458,
                DestinationCity = "Detroit",
                DestinationState = "MI"
            });
        }

        if (orderFT5000 != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "FX-2025-20815"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                OrderId = orderFT5000.Id,
                CarrierName = "FedEx Freight",
                TrackingNumber = "FX-2025-20815",
                Status = "In Transit",
                ShipDate = DateTime.Parse("2025-02-18T00:00:00Z").ToUniversalTime(),
                EstimatedArrival = DateTime.Parse("2025-02-27T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 45.5051,
                DestinationLng = -122.6750,
                DestinationCity = "Portland",
                DestinationState = "OR"
            });
        }

        await _db.SaveChangesAsync();

        // ===================================================
        // FIBER INVENTORY TRANSACTIONS
        // ===================================================

        var matFGW = await _db.FiberMaterials.FirstOrDefaultAsync(m => m.UserId == userId && m.Sku == "RM-FGW");
        var matCSM = await _db.FiberMaterials.FirstOrDefaultAsync(m => m.UserId == userId && m.Sku == "RM-CSM");
        var matVE = await _db.FiberMaterials.FirstOrDefaultAsync(m => m.UserId == userId && m.Sku == "RM-VE");
        var matOR = await _db.FiberMaterials.FirstOrDefaultAsync(m => m.UserId == userId && m.Sku == "RM-OR");
        var matCR = await _db.FiberMaterials.FirstOrDefaultAsync(m => m.UserId == userId && m.Sku == "RM-CR");
        var matGV = await _db.FiberMaterials.FirstOrDefaultAsync(m => m.UserId == userId && m.Sku == "RM-GV");
        var matMK = await _db.FiberMaterials.FirstOrDefaultAsync(m => m.UserId == userId && m.Sku == "RM-MK");
        var matCF = await _db.FiberMaterials.FirstOrDefaultAsync(m => m.UserId == userId && m.Sku == "RM-CF");
        var matFC = await _db.FiberMaterials.FirstOrDefaultAsync(m => m.UserId == userId && m.Sku == "RM-FC");
        var matGC = await _db.FiberMaterials.FirstOrDefaultAsync(m => m.UserId == userId && m.Sku == "RM-GC");

        if (matFGW != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "Initial stock receipt — TexMat Q3 order"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matFGW.Id,
                TransactionType = "Receive",
                Quantity = 100,
                QtyBeforeTransaction = 0,
                QtyAfterTransaction = 100,
                Notes = "Initial stock receipt — TexMat Q3 order",
                TransactionDate = DateTime.Parse("2024-08-01T00:00:00Z").ToUniversalTime()
            });
        }

        if (matFGW != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "Consumed for Gulf Coast FD-4R production run"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matFGW.Id,
                TransactionType = "Consume",
                Quantity = -30,
                QtyBeforeTransaction = 100,
                QtyAfterTransaction = 70,
                Notes = "Consumed for Gulf Coast FD-4R production run",
                TransactionDate = DateTime.Parse("2024-08-10T00:00:00Z").ToUniversalTime()
            });
        }

        if (matFGW != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "TexMat Q4 restock received — Warehouse A1"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matFGW.Id,
                TransactionType = "Receive",
                Quantity = 40,
                QtyBeforeTransaction = 70,
                QtyAfterTransaction = 110,
                Notes = "TexMat Q4 restock received — Warehouse A1",
                TransactionDate = DateTime.Parse("2024-11-05T00:00:00Z").ToUniversalTime()
            });
        }

        if (matFGW != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "Consumed for Lone Star FP-8S production run"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matFGW.Id,
                TransactionType = "Consume",
                Quantity = -25,
                QtyBeforeTransaction = 110,
                QtyAfterTransaction = 85,
                Notes = "Consumed for Lone Star FP-8S production run",
                TransactionDate = DateTime.Parse("2024-11-20T00:00:00Z").ToUniversalTime()
            });
        }

        if (matCSM != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "Initial CSM stock — TexMat Q3 shipment"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matCSM.Id,
                TransactionType = "Receive",
                Quantity = 150,
                QtyBeforeTransaction = 0,
                QtyAfterTransaction = 150,
                Notes = "Initial CSM stock — TexMat Q3 shipment",
                TransactionDate = DateTime.Parse("2024-08-01T00:00:00Z").ToUniversalTime()
            });
        }

        if (matCSM != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "Consumed — Bayou FT-500 containment basin build"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matCSM.Id,
                TransactionType = "Consume",
                Quantity = -30,
                QtyBeforeTransaction = 150,
                QtyAfterTransaction = 120,
                Notes = "Consumed — Bayou FT-500 containment basin build",
                TransactionDate = DateTime.Parse("2024-08-28T00:00:00Z").ToUniversalTime()
            });
        }

        if (matVE != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "ChemSource initial vinyl ester delivery"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matVE.Id,
                TransactionType = "Receive",
                Quantity = 20,
                QtyBeforeTransaction = 0,
                QtyAfterTransaction = 20,
                Notes = "ChemSource initial vinyl ester delivery",
                TransactionDate = DateTime.Parse("2024-09-01T00:00:00Z").ToUniversalTime()
            });
        }

        if (matVE != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "Consumed — Delta Tank FT-1000VE production"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matVE.Id,
                TransactionType = "Consume",
                Quantity = -5,
                QtyBeforeTransaction = 20,
                QtyAfterTransaction = 15,
                Notes = "Consumed — Delta Tank FT-1000VE production",
                TransactionDate = DateTime.Parse("2024-11-25T00:00:00Z").ToUniversalTime()
            });
        }

        if (matVE != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "Qty adjustment after Q4 physical inventory count"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matVE.Id,
                TransactionType = "Adjustment",
                Quantity = 3,
                QtyBeforeTransaction = 15,
                QtyAfterTransaction = 18,
                Notes = "Qty adjustment after Q4 physical inventory count",
                TransactionDate = DateTime.Parse("2024-12-31T00:00:00Z").ToUniversalTime()
            });
        }

        if (matOR != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "ChemSource initial ortho resin delivery"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matOR.Id,
                TransactionType = "Receive",
                Quantity = 30,
                QtyBeforeTransaction = 0,
                QtyAfterTransaction = 30,
                Notes = "ChemSource initial ortho resin delivery",
                TransactionDate = DateTime.Parse("2024-09-01T00:00:00Z").ToUniversalTime()
            });
        }

        if (matOR != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "Consumed — Saguaro composite panel production batch"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matOR.Id,
                TransactionType = "Consume",
                Quantity = -6,
                QtyBeforeTransaction = 30,
                QtyAfterTransaction = 24,
                Notes = "Consumed — Saguaro composite panel production batch",
                TransactionDate = DateTime.Parse("2024-10-08T00:00:00Z").ToUniversalTime()
            });
        }

        if (matCR != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "Initial corrosion liner resin — ChemSource"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matCR.Id,
                TransactionType = "Receive",
                Quantity = 10,
                QtyBeforeTransaction = 0,
                QtyAfterTransaction = 10,
                Notes = "Initial corrosion liner resin — ChemSource",
                TransactionDate = DateTime.Parse("2024-10-15T00:00:00Z").ToUniversalTime()
            });
        }

        if (matCR != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "Consumed — Gulf Coast FT-3000 tank liner coats"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matCR.Id,
                TransactionType = "Consume",
                Quantity = -1,
                QtyBeforeTransaction = 10,
                QtyAfterTransaction = 9,
                Notes = "Consumed — Gulf Coast FT-3000 tank liner coats",
                TransactionDate = DateTime.Parse("2024-11-02T00:00:00Z").ToUniversalTime()
            });
        }

        if (matGV != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "Gulf Fiber Supply initial veil delivery"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matGV.Id,
                TransactionType = "Receive",
                Quantity = 50,
                QtyBeforeTransaction = 0,
                QtyAfterTransaction = 50,
                Notes = "Gulf Fiber Supply initial veil delivery",
                TransactionDate = DateTime.Parse("2024-09-10T00:00:00Z").ToUniversalTime()
            });
        }

        if (matGV != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "Consumed — Permian Basin FP-4S inner liner lamination"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matGV.Id,
                TransactionType = "Consume",
                Quantity = -6,
                QtyBeforeTransaction = 50,
                QtyAfterTransaction = 44,
                Notes = "Consumed — Permian Basin FP-4S inner liner lamination",
                TransactionDate = DateTime.Parse("2024-10-30T00:00:00Z").ToUniversalTime()
            });
        }

        if (matMK != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "MEKP catalyst — initial ChemSource order"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matMK.Id,
                TransactionType = "Receive",
                Quantity = 40,
                QtyBeforeTransaction = 0,
                QtyAfterTransaction = 40,
                Notes = "MEKP catalyst — initial ChemSource order",
                TransactionDate = DateTime.Parse("2024-09-01T00:00:00Z").ToUniversalTime()
            });
        }

        if (matMK != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "Consumed — routine production Q4 cure cycles"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matMK.Id,
                TransactionType = "Consume",
                Quantity = -8,
                QtyBeforeTransaction = 40,
                QtyAfterTransaction = 32,
                Notes = "Consumed — routine production Q4 cure cycles",
                TransactionDate = DateTime.Parse("2024-12-15T00:00:00Z").ToUniversalTime()
            });
        }

        if (matCF != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "Carbon fiber tow — Apex Composites pilot order"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matCF.Id,
                TransactionType = "Receive",
                Quantity = 20,
                QtyBeforeTransaction = 0,
                QtyAfterTransaction = 20,
                Notes = "Carbon fiber tow — Apex Composites pilot order",
                TransactionDate = DateTime.Parse("2024-12-01T00:00:00Z").ToUniversalTime()
            });
        }

        if (matCF != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "Consumed — Tri-State structural beam prototype layup"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matCF.Id,
                TransactionType = "Consume",
                Quantity = -5,
                QtyBeforeTransaction = 20,
                QtyAfterTransaction = 15,
                Notes = "Consumed — Tri-State structural beam prototype layup",
                TransactionDate = DateTime.Parse("2024-12-20T00:00:00Z").ToUniversalTime()
            });
        }

        if (matFC != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "Foam core initial stock — Gulf Fiber Supply"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matFC.Id,
                TransactionType = "Receive",
                Quantity = 80,
                QtyBeforeTransaction = 0,
                QtyAfterTransaction = 80,
                Notes = "Foam core initial stock — Gulf Fiber Supply",
                TransactionDate = DateTime.Parse("2024-10-01T00:00:00Z").ToUniversalTime()
            });
        }

        if (matFC != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "Consumed — Saguaro sandwich panel core material"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matFC.Id,
                TransactionType = "Consume",
                Quantity = -20,
                QtyBeforeTransaction = 80,
                QtyAfterTransaction = 60,
                Notes = "Consumed — Saguaro sandwich panel core material",
                TransactionDate = DateTime.Parse("2024-10-09T00:00:00Z").ToUniversalTime()
            });
        }

        if (matGC != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "White ISO gel coat — TexMat first shipment"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matGC.Id,
                TransactionType = "Receive",
                Quantity = 60,
                QtyBeforeTransaction = 0,
                QtyAfterTransaction = 60,
                Notes = "White ISO gel coat — TexMat first shipment",
                TransactionDate = DateTime.Parse("2024-09-15T00:00:00Z").ToUniversalTime()
            });
        }

        if (matGC != null && !await _db.FiberInventoryTransactions.AnyAsync(t => t.UserId == userId && t.Notes == "Consumed — Gulf Coast tank exterior finish coats"))
        {
            _db.FiberInventoryTransactions.Add(new FiberInventoryTransaction
            {
                UserId = userId,
                MaterialId = matGC.Id,
                TransactionType = "Consume",
                Quantity = -5,
                QtyBeforeTransaction = 60,
                QtyAfterTransaction = 55,
                Notes = "Consumed — Gulf Coast tank exterior finish coats",
                TransactionDate = DateTime.Parse("2024-10-02T00:00:00Z").ToUniversalTime()
            });
        }

        await _db.SaveChangesAsync();


        // ===================================================
        // PROPERTIES
        // ===================================================

        if (!await _db.Properties.AnyAsync(p =>
            p.Street == "120 Franklin Ave" &&
            p.City == "Redlands"))
        {
            _db.Properties.Add(new Property
            {
                BrokeredBy = "BHHS PERRIE MUNDY REALTY GROUP",
                Status = "active",
                Price = 995000m,
                Bedrooms = 4,
                Bathrooms = 3,
                AcreLot = 0.25m,
                LotSqft = 2131,
                Street = "120 Franklin Ave",
                City = "Redlands",
                State = "CA",
                ZipCode = "92373",
                Latitude = 34.055,
                Longitude = -117.182,
                HoaFee = 0,
                PropertyTax = 9950,
                Utilities = 300,
                SchoolRating = 80,
                CrimeScore = 60,
                Walkability = 55,
                TransitAccess = 40,
                AmenitiesScore = 65,
                CommuteMin = 25,
                YearBuilt = 1985,
                LastRenovation = null,
                RoofCondition = 80,
                AcCondition = 80,
                PlumbingCondition = 75,
                ElectricalCondition = 75,
                FloorPlanScore = 78,
                FutureAppreciation = 70,
                ResalePotential = 72,
                FloodRisk = 10,
                NoiseLevel = 30
            });
        }

        var propertySeeds = new List<Property>
        {
            new Property { BrokeredBy = "CROWN REAL ESTATE TEAM", Status = "active", Price = 439000m, Bedrooms = 4, Bathrooms = 2, AcreLot = 0.18m, LotSqft = 1504, Street = "915 Alta St", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.060, Longitude = -117.170, HoaFee = 0, PropertyTax = 4390, Utilities = 250, SchoolRating = 65, CrimeScore = 58, Walkability = 60, TransitAccess = 35, AmenitiesScore = 55, CommuteMin = 28, YearBuilt = 1978, LastRenovation = null, RoofCondition = 70, AcCondition = 72, PlumbingCondition = 70, ElectricalCondition = 68, FloorPlanScore = 65, FutureAppreciation = 60, ResalePotential = 62, FloodRisk = 12, NoiseLevel = 35 },
            new Property { BrokeredBy = "COLDWELL BANKER REALTY", Status = "active", Price = 1395000m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.35m, LotSqft = 2776, Street = "543 E Mariposa Dr", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.052, Longitude = -117.188, HoaFee = 0, PropertyTax = 13950, Utilities = 350, SchoolRating = 88, CrimeScore = 70, Walkability = 52, TransitAccess = 30, AmenitiesScore = 75, CommuteMin = 24, YearBuilt = 1992, LastRenovation = 2018, RoofCondition = 85, AcCondition = 85, PlumbingCondition = 83, ElectricalCondition = 82, FloorPlanScore = 88, FutureAppreciation = 80, ResalePotential = 85, FloodRisk = 8, NoiseLevel = 20 },
            new Property { BrokeredBy = "KELLER WILLIAMS REALTY", Status = "active", Price = 999900m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.28m, LotSqft = 2310, Street = "1582 Franklin Ave", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.056, Longitude = -117.181, HoaFee = 0, PropertyTax = 9990, Utilities = 320, SchoolRating = 82, CrimeScore = 64, Walkability = 58, TransitAccess = 40, AmenitiesScore = 68, CommuteMin = 26, YearBuilt = 1987, LastRenovation = 2016, RoofCondition = 82, AcCondition = 84, PlumbingCondition = 78, ElectricalCondition = 79, FloorPlanScore = 80, FutureAppreciation = 75, ResalePotential = 76, FloodRisk = 9, NoiseLevel = 25 },
            new Property { BrokeredBy = "KELLER WILLIAMS REALTY", Status = "active", Price = 850000m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.30m, LotSqft = 2138, Street = "31397 Mesa Dr", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.050, Longitude = -117.195, HoaFee = 0, PropertyTax = 8500, Utilities = 300, SchoolRating = 78, CrimeScore = 60, Walkability = 45, TransitAccess = 25, AmenitiesScore = 60, CommuteMin = 30, YearBuilt = 1984, LastRenovation = null, RoofCondition = 75, AcCondition = 76, PlumbingCondition = 74, ElectricalCondition = 72, FloorPlanScore = 73, FutureAppreciation = 68, ResalePotential = 70, FloodRisk = 11, NoiseLevel = 28 },
            new Property { BrokeredBy = "CENTURY 21 LOIS LAUER REALTY", Status = "active", Price = 650000m, Bedrooms = 3, Bathrooms = 2, AcreLot = 0.40m, LotSqft = 1795, Street = "947 Nottingham Dr", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.058, Longitude = -117.187, HoaFee = 0, PropertyTax = 6500, Utilities = 280, SchoolRating = 75, CrimeScore = 55, Walkability = 52, TransitAccess = 32, AmenitiesScore = 60, CommuteMin = 27, YearBuilt = 1975, LastRenovation = 2009, RoofCondition = 72, AcCondition = 70, PlumbingCondition = 69, ElectricalCondition = 68, FloorPlanScore = 71, FutureAppreciation = 65, ResalePotential = 66, FloodRisk = 12, NoiseLevel = 30 },
            new Property { BrokeredBy = "EXP REALTY OF GREATER LOS ANGELES", Status = "active", Price = 610000m, Bedrooms = 4, Bathrooms = 2, AcreLot = 0.22m, LotSqft = 1308, Street = "1539 Robyn St", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.064, Longitude = -117.166, HoaFee = 0, PropertyTax = 6100, Utilities = 260, SchoolRating = 70, CrimeScore = 57, Walkability = 50, TransitAccess = 30, AmenitiesScore = 55, CommuteMin = 29, YearBuilt = 1980, LastRenovation = null, RoofCondition = 70, AcCondition = 72, PlumbingCondition = 71, ElectricalCondition = 70, FloorPlanScore = 68, FutureAppreciation = 63, ResalePotential = 64, FloodRisk = 13, NoiseLevel = 34 },
            new Property { BrokeredBy = "CENTURY 21 LOIS LAUER REALTY", Status = "active", Price = 949000m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.27m, LotSqft = 2451, Street = "171 Bellevue Ave", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.057, Longitude = -117.183, HoaFee = 0, PropertyTax = 9490, Utilities = 310, SchoolRating = 83, CrimeScore = 60, Walkability = 60, TransitAccess = 38, AmenitiesScore = 72, CommuteMin = 24, YearBuilt = 1988, LastRenovation = 2015, RoofCondition = 80, AcCondition = 82, PlumbingCondition = 79, ElectricalCondition = 78, FloorPlanScore = 82, FutureAppreciation = 72, ResalePotential = 74, FloodRisk = 10, NoiseLevel = 27 },
            new Property { BrokeredBy = "KELLER WILLIAMS EMPIRE ESTATES", Status = "active", Price = 365000m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.05m, LotSqft = 1543, Street = "135 E Cypress Ave", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.053, Longitude = -117.183, HoaFee = 350, PropertyTax = 3650, Utilities = 250, SchoolRating = 68, CrimeScore = 62, Walkability = 70, TransitAccess = 55, AmenitiesScore = 65, CommuteMin = 20, YearBuilt = 1995, LastRenovation = null, RoofCondition = 74, AcCondition = 75, PlumbingCondition = 72, ElectricalCondition = 72, FloorPlanScore = 70, FutureAppreciation = 60, ResalePotential = 61, FloodRisk = 10, NoiseLevel = 40 },
            new Property { BrokeredBy = "SHAW REAL ESTATE BROKERS", Status = "active", Price = 797000m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.26m, LotSqft = 2068, Street = "162 Lakeside Ave", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.055, Longitude = -117.178, HoaFee = 0, PropertyTax = 7970, Utilities = 300, SchoolRating = 80, CrimeScore = 58, Walkability = 55, TransitAccess = 35, AmenitiesScore = 68, CommuteMin = 26, YearBuilt = 1990, LastRenovation = 2017, RoofCondition = 82, AcCondition = 84, PlumbingCondition = 82, ElectricalCondition = 80, FloorPlanScore = 83, FutureAppreciation = 72, ResalePotential = 74, FloodRisk = 9, NoiseLevel = 25 },
            new Property { BrokeredBy = "FIRST TEAM REAL ESTATE", Status = "active", Price = 575000m, Bedrooms = 3, Bathrooms = 2, AcreLot = 0.20m, LotSqft = 1324, Street = "1427 Laramie Ave", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.061, Longitude = -117.169, HoaFee = 0, PropertyTax = 5750, Utilities = 260, SchoolRating = 70, CrimeScore = 56, Walkability = 52, TransitAccess = 32, AmenitiesScore = 58, CommuteMin = 28, YearBuilt = 1983, LastRenovation = null, RoofCondition = 70, AcCondition = 72, PlumbingCondition = 71, ElectricalCondition = 70, FloorPlanScore = 68, FutureAppreciation = 63, ResalePotential = 65, FloodRisk = 13, NoiseLevel = 34 },
            new Property { BrokeredBy = "EXP REALTY OF CALIFORNIA INC.", Status = "active", Price = 870000m, Bedrooms = 5, Bathrooms = 4, AcreLot = 0.33m, LotSqft = 3091, Street = "1678 Harrison Ln", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.063, Longitude = -117.168, HoaFee = 0, PropertyTax = 8700, Utilities = 320, SchoolRating = 77, CrimeScore = 55, Walkability = 50, TransitAccess = 30, AmenitiesScore = 60, CommuteMin = 30, YearBuilt = 1994, LastRenovation = 2020, RoofCondition = 85, AcCondition = 87, PlumbingCondition = 83, ElectricalCondition = 82, FloorPlanScore = 85, FutureAppreciation = 75, ResalePotential = 76, FloodRisk = 10, NoiseLevel = 30 },
            new Property { BrokeredBy = "LPT REALTY INC", Status = "active", Price = 899900m, Bedrooms = 5, Bathrooms = 3, AcreLot = 0.30m, LotSqft = 3116, Street = "1802 Pummelo Dr", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.064, Longitude = -117.167, HoaFee = 0, PropertyTax = 8999, Utilities = 320, SchoolRating = 78, CrimeScore = 58, Walkability = 48, TransitAccess = 28, AmenitiesScore = 62, CommuteMin = 29, YearBuilt = 1996, LastRenovation = null, RoofCondition = 82, AcCondition = 83, PlumbingCondition = 81, ElectricalCondition = 80, FloorPlanScore = 82, FutureAppreciation = 72, ResalePotential = 74, FloodRisk = 11, NoiseLevel = 30 },
            new Property { BrokeredBy = "COLDWELL BANKER REALTY", Status = "active", Price = 425000m, Bedrooms = 2, Bathrooms = 2, AcreLot = 0.03m, LotSqft = 1070, Street = "93 Kansas St APT 202", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.054, Longitude = -117.184, HoaFee = 350, PropertyTax = 4250, Utilities = 220, SchoolRating = 72, CrimeScore = 60, Walkability = 75, TransitAccess = 58, AmenitiesScore = 70, CommuteMin = 18, YearBuilt = 2000, LastRenovation = null, RoofCondition = 78, AcCondition = 80, PlumbingCondition = 76, ElectricalCondition = 76, FloorPlanScore = 72, FutureAppreciation = 62, ResalePotential = 63, FloodRisk = 8, NoiseLevel = 42 },
            new Property { BrokeredBy = "SHAW REAL ESTATE BROKERS", Status = "active", Price = 725000m, Bedrooms = 3, Bathrooms = 2, AcreLot = 0.18m, LotSqft = 1751, Street = "1612 Bellevue Rd", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.057, Longitude = -117.183, HoaFee = 0, PropertyTax = 7250, Utilities = 280, SchoolRating = 78, CrimeScore = 58, Walkability = 58, TransitAccess = 38, AmenitiesScore = 68, CommuteMin = 25, YearBuilt = 1988, LastRenovation = 2014, RoofCondition = 78, AcCondition = 80, PlumbingCondition = 76, ElectricalCondition = 75, FloorPlanScore = 76, FutureAppreciation = 68, ResalePotential = 70, FloodRisk = 9, NoiseLevel = 28 },
            new Property { BrokeredBy = "KELLER WILLIAMS RIVERSIDE CENT", Status = "active", Price = 465000m, Bedrooms = 3, Bathrooms = 2, AcreLot = 0.17m, LotSqft = 1512, Street = "1076 Occidental Cir", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.171, HoaFee = 0, PropertyTax = 4650, Utilities = 240, SchoolRating = 68, CrimeScore = 56, Walkability = 52, TransitAccess = 32, AmenitiesScore = 58, CommuteMin = 28, YearBuilt = 1982, LastRenovation = null, RoofCondition = 70, AcCondition = 71, PlumbingCondition = 70, ElectricalCondition = 69, FloorPlanScore = 67, FutureAppreciation = 62, ResalePotential = 63, FloodRisk = 13, NoiseLevel = 35 },
            new Property { BrokeredBy = "EXP REALTY OF GREATER LOS ANGELES", Status = "active", Price = 875000m, Bedrooms = 5, Bathrooms = 3, AcreLot = 0.26m, LotSqft = 2484, Street = "527 W Palm Ave", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.055, Longitude = -117.192, HoaFee = 0, PropertyTax = 8750, Utilities = 310, SchoolRating = 80, CrimeScore = 60, Walkability = 58, TransitAccess = 38, AmenitiesScore = 68, CommuteMin = 25, YearBuilt = 1986, LastRenovation = 2012, RoofCondition = 80, AcCondition = 82, PlumbingCondition = 79, ElectricalCondition = 78, FloorPlanScore = 80, FutureAppreciation = 72, ResalePotential = 74, FloodRisk = 9, NoiseLevel = 27 },
            new Property { BrokeredBy = "EXP REALTY OF SOUTHERN CALIFORNIA INC", Status = "active", Price = 869000m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.25m, LotSqft = 2650, Street = "356 Campbell Ave", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.053, Longitude = -117.186, HoaFee = 0, PropertyTax = 8690, Utilities = 320, SchoolRating = 82, CrimeScore = 62, Walkability = 56, TransitAccess = 36, AmenitiesScore = 70, CommuteMin = 24, YearBuilt = 1991, LastRenovation = 2016, RoofCondition = 82, AcCondition = 84, PlumbingCondition = 80, ElectricalCondition = 79, FloorPlanScore = 83, FutureAppreciation = 73, ResalePotential = 75, FloodRisk = 9, NoiseLevel = 25 },
            new Property { BrokeredBy = "RE/MAX ADVANTAGE", Status = "active", Price = 239000m, Bedrooms = 2, Bathrooms = 1, AcreLot = 0.03m, LotSqft = 918, Street = "48 N Center St", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.054, Longitude = -117.182, HoaFee = 300, PropertyTax = 2390, Utilities = 180, SchoolRating = 68, CrimeScore = 62, Walkability = 78, TransitAccess = 60, AmenitiesScore = 72, CommuteMin = 16, YearBuilt = 1975, LastRenovation = null, RoofCondition = 65, AcCondition = 66, PlumbingCondition = 64, ElectricalCondition = 63, FloorPlanScore = 66, FutureAppreciation = 55, ResalePotential = 56, FloodRisk = 10, NoiseLevel = 45 },
            new Property { BrokeredBy = "BERKSHIRE HATHAWAY HOMESERVICES CALIFORNIA REALTY", Status = "active", Price = 939000m, Bedrooms = 5, Bathrooms = 3, AcreLot = 0.24m, LotSqft = 2208, Street = "1525 Garden St", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.056, Longitude = -117.179, HoaFee = 0, PropertyTax = 9390, Utilities = 310, SchoolRating = 80, CrimeScore = 60, Walkability = 56, TransitAccess = 36, AmenitiesScore = 68, CommuteMin = 25, YearBuilt = 1990, LastRenovation = 2014, RoofCondition = 80, AcCondition = 82, PlumbingCondition = 79, ElectricalCondition = 78, FloorPlanScore = 81, FutureAppreciation = 72, ResalePotential = 74, FloodRisk = 9, NoiseLevel = 28 },
            new Property { BrokeredBy = "ARCH PACIFIC REALTY", Status = "active", Price = 685000m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.18m, LotSqft = 1746, Street = "622 Esther Way", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.054, Longitude = -117.185, HoaFee = 0, PropertyTax = 6850, Utilities = 270, SchoolRating = 76, CrimeScore = 58, Walkability = 60, TransitAccess = 40, AmenitiesScore = 66, CommuteMin = 23, YearBuilt = 1988, LastRenovation = null, RoofCondition = 76, AcCondition = 78, PlumbingCondition = 75, ElectricalCondition = 74, FloorPlanScore = 75, FutureAppreciation = 67, ResalePotential = 68, FloodRisk = 10, NoiseLevel = 30 },
            new Property { BrokeredBy = "ADAM CUNNINGHAM BROKER", Status = "active", Price = 489900m, Bedrooms = 3, Bathrooms = 2, AcreLot = 0.13m, LotSqft = 987, Street = "838 W Brockton Ave", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.060, Longitude = -117.176, HoaFee = 0, PropertyTax = 4899, Utilities = 240, SchoolRating = 68, CrimeScore = 56, Walkability = 55, TransitAccess = 34, AmenitiesScore = 58, CommuteMin = 27, YearBuilt = 1978, LastRenovation = 2022, RoofCondition = 72, AcCondition = 74, PlumbingCondition = 70, ElectricalCondition = 70, FloorPlanScore = 68, FutureAppreciation = 63, ResalePotential = 64, FloodRisk = 12, NoiseLevel = 35 },
            new Property { BrokeredBy = "SHAW REAL ESTATE BROKERS", Status = "active", Price = 585000m, Bedrooms = 2, Bathrooms = 2, AcreLot = 0.14m, LotSqft = 1151, Street = "936 Judson St", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.061, Longitude = -117.172, HoaFee = 0, PropertyTax = 5850, Utilities = 250, SchoolRating = 70, CrimeScore = 57, Walkability = 55, TransitAccess = 35, AmenitiesScore = 60, CommuteMin = 26, YearBuilt = 1985, LastRenovation = null, RoofCondition = 72, AcCondition = 73, PlumbingCondition = 71, ElectricalCondition = 70, FloorPlanScore = 70, FutureAppreciation = 64, ResalePotential = 65, FloodRisk = 12, NoiseLevel = 33 },
            new Property { BrokeredBy = "CENTURY 21 LOIS LAUER REALTY", Status = "active", Price = 425000m, Bedrooms = 3, Bathrooms = 2, AcreLot = 0.20m, LotSqft = 1960, Street = "1412 Medallion St", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.063, Longitude = -117.168, HoaFee = 0, PropertyTax = 4250, Utilities = 230, SchoolRating = 66, CrimeScore = 56, Walkability = 50, TransitAccess = 30, AmenitiesScore = 55, CommuteMin = 29, YearBuilt = 1979, LastRenovation = null, RoofCondition = 68, AcCondition = 69, PlumbingCondition = 67, ElectricalCondition = 66, FloorPlanScore = 65, FutureAppreciation = 60, ResalePotential = 61, FloodRisk = 13, NoiseLevel = 36 },
            new Property { BrokeredBy = "EXPERT REAL ESTATE & INVESTMENT", Status = "active", Price = 649900m, Bedrooms = 5, Bathrooms = 4, AcreLot = 0.28m, LotSqft = 3136, Street = "261 E Crescent Ave", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.058, Longitude = -117.178, HoaFee = 0, PropertyTax = 6499, Utilities = 280, SchoolRating = 78, CrimeScore = 62, Walkability = 55, TransitAccess = 35, AmenitiesScore = 65, CommuteMin = 26, YearBuilt = 1987, LastRenovation = null, RoofCondition = 74, AcCondition = 75, PlumbingCondition = 72, ElectricalCondition = 71, FloorPlanScore = 76, FutureAppreciation = 67, ResalePotential = 68, FloodRisk = 11, NoiseLevel = 30 },
            new Property { BrokeredBy = "KELLER WILLIAMS REALTY", Status = "active", Price = 690000m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.14m, LotSqft = 1375, Street = "1221 San Jacinto St", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.056, Longitude = -117.181, HoaFee = 0, PropertyTax = 6900, Utilities = 260, SchoolRating = 76, CrimeScore = 60, Walkability = 62, TransitAccess = 42, AmenitiesScore = 66, CommuteMin = 22, YearBuilt = 1992, LastRenovation = null, RoofCondition = 76, AcCondition = 78, PlumbingCondition = 74, ElectricalCondition = 74, FloorPlanScore = 74, FutureAppreciation = 67, ResalePotential = 68, FloodRisk = 10, NoiseLevel = 32 },
            new Property { BrokeredBy = "KELLER WILLIAMS REALTY", Status = "active", Price = 645000m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.22m, LotSqft = 2045, Street = "416 Sonora Cir", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.054, Longitude = -117.187, HoaFee = 0, PropertyTax = 6450, Utilities = 270, SchoolRating = 76, CrimeScore = 60, Walkability = 58, TransitAccess = 38, AmenitiesScore = 66, CommuteMin = 24, YearBuilt = 1989, LastRenovation = 2011, RoofCondition = 76, AcCondition = 78, PlumbingCondition = 75, ElectricalCondition = 74, FloorPlanScore = 75, FutureAppreciation = 67, ResalePotential = 69, FloodRisk = 10, NoiseLevel = 30 },
            new Property { BrokeredBy = "BERKSHIRE HATHAWAY HOMESERVICES CALIFORNIA REALTY", Status = "active", Price = 849000m, Bedrooms = 5, Bathrooms = 3, AcreLot = 0.25m, LotSqft = 2448, Street = "1235 W Cypress Ave", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.053, Longitude = -117.191, HoaFee = 0, PropertyTax = 8490, Utilities = 300, SchoolRating = 78, CrimeScore = 60, Walkability = 55, TransitAccess = 35, AmenitiesScore = 65, CommuteMin = 27, YearBuilt = 1985, LastRenovation = null, RoofCondition = 78, AcCondition = 79, PlumbingCondition = 77, ElectricalCondition = 76, FloorPlanScore = 77, FutureAppreciation = 70, ResalePotential = 72, FloodRisk = 11, NoiseLevel = 29 },
            new Property { BrokeredBy = "TOWN & COUNTRY REAL ESTATE", Status = "active", Price = 569900m, Bedrooms = 3, Bathrooms = 2, AcreLot = 0.17m, LotSqft = 1176, Street = "1323 Kingswood Dr", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.170, HoaFee = 0, PropertyTax = 5699, Utilities = 250, SchoolRating = 68, CrimeScore = 56, Walkability = 50, TransitAccess = 30, AmenitiesScore = 57, CommuteMin = 28, YearBuilt = 1983, LastRenovation = null, RoofCondition = 70, AcCondition = 71, PlumbingCondition = 69, ElectricalCondition = 68, FloorPlanScore = 67, FutureAppreciation = 62, ResalePotential = 63, FloodRisk = 13, NoiseLevel = 35 },
            new Property { BrokeredBy = "RE/MAX ADVANTAGE", Status = "active", Price = 699900m, Bedrooms = 4, Bathrooms = 2, AcreLot = 0.20m, LotSqft = 1678, Street = "1030 Fallbrook Ave", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.055, Longitude = -117.180, HoaFee = 0, PropertyTax = 6999, Utilities = 270, SchoolRating = 76, CrimeScore = 58, Walkability = 56, TransitAccess = 36, AmenitiesScore = 64, CommuteMin = 25, YearBuilt = 1987, LastRenovation = null, RoofCondition = 74, AcCondition = 76, PlumbingCondition = 73, ElectricalCondition = 72, FloorPlanScore = 73, FutureAppreciation = 67, ResalePotential = 69, FloodRisk = 10, NoiseLevel = 30 },
            new Property { BrokeredBy = "E HOMES", Status = "active", Price = 539999m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.07m, LotSqft = 1805, Street = "56 Dearborn Cir", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.063, Longitude = -117.175, HoaFee = 280, PropertyTax = 5400, Utilities = 240, SchoolRating = 72, CrimeScore = 57, Walkability = 58, TransitAccess = 42, AmenitiesScore = 62, CommuteMin = 22, YearBuilt = 2005, LastRenovation = null, RoofCondition = 78, AcCondition = 79, PlumbingCondition = 76, ElectricalCondition = 76, FloorPlanScore = 74, FutureAppreciation = 65, ResalePotential = 66, FloodRisk = 10, NoiseLevel = 38 },
            new Property { BrokeredBy = "REAL BROKER", Status = "active", Price = 1199999m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.55m, LotSqft = 2364, Street = "11891 San Timoteo Canyon Rd", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.041, Longitude = -117.148, HoaFee = 0, PropertyTax = 12000, Utilities = 380, SchoolRating = 74, CrimeScore = 45, Walkability = 30, TransitAccess = 15, AmenitiesScore = 50, CommuteMin = 38, YearBuilt = 1998, LastRenovation = 2015, RoofCondition = 82, AcCondition = 83, PlumbingCondition = 80, ElectricalCondition = 79, FloorPlanScore = 78, FutureAppreciation = 75, ResalePotential = 78, FloodRisk = 14, NoiseLevel = 15 },
            new Property { BrokeredBy = "EXP REALTY OF CALIFORNIA INC", Status = "active", Price = 849000m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.35m, LotSqft = 1894, Street = "30993 Palo Alto Dr", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.049, Longitude = -117.196, HoaFee = 0, PropertyTax = 8490, Utilities = 300, SchoolRating = 76, CrimeScore = 52, Walkability = 38, TransitAccess = 20, AmenitiesScore = 58, CommuteMin = 32, YearBuilt = 1983, LastRenovation = null, RoofCondition = 74, AcCondition = 75, PlumbingCondition = 72, ElectricalCondition = 71, FloorPlanScore = 72, FutureAppreciation = 68, ResalePotential = 70, FloodRisk = 12, NoiseLevel = 22 },
            new Property { BrokeredBy = "COLDWELL BANKER LEADERS", Status = "active", Price = 819900m, Bedrooms = 5, Bathrooms = 3, AcreLot = 0.24m, LotSqft = 2244, Street = "1410 Pleasant View Dr", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.063, Longitude = -117.167, HoaFee = 0, PropertyTax = 8199, Utilities = 310, SchoolRating = 78, CrimeScore = 56, Walkability = 50, TransitAccess = 30, AmenitiesScore = 62, CommuteMin = 29, YearBuilt = 1994, LastRenovation = null, RoofCondition = 80, AcCondition = 81, PlumbingCondition = 79, ElectricalCondition = 78, FloorPlanScore = 79, FutureAppreciation = 72, ResalePotential = 74, FloodRisk = 11, NoiseLevel = 30 },
            new Property { BrokeredBy = "HOME WORKS REALTY", Status = "active", Price = 220000m, Bedrooms = 3, Bathrooms = 2, AcreLot = 0.06m, LotSqft = 1000, Street = "455 Judson St Space 9", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.061, Longitude = -117.172, HoaFee = 250, PropertyTax = 2200, Utilities = 180, SchoolRating = 58, CrimeScore = 56, Walkability = 48, TransitAccess = 28, AmenitiesScore = 52, CommuteMin = 27, YearBuilt = 1972, LastRenovation = null, RoofCondition = 60, AcCondition = 60, PlumbingCondition = 58, ElectricalCondition = 57, FloorPlanScore = 58, FutureAppreciation = 50, ResalePotential = 51, FloodRisk = 13, NoiseLevel = 38 },
            new Property { BrokeredBy = "COLDWELL BANKER REALTY", Status = "active", Price = 125000m, Bedrooms = 1, Bathrooms = 1, AcreLot = 0.02m, LotSqft = 671, Street = "167 N Center St", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.055, Longitude = -117.182, HoaFee = 300, PropertyTax = 1250, Utilities = 150, SchoolRating = 65, CrimeScore = 62, Walkability = 78, TransitAccess = 60, AmenitiesScore = 68, CommuteMin = 16, YearBuilt = 1968, LastRenovation = null, RoofCondition = 58, AcCondition = 60, PlumbingCondition = 55, ElectricalCondition = 55, FloorPlanScore = 60, FutureAppreciation = 50, ResalePotential = 51, FloodRisk = 9, NoiseLevel = 48 },
            new Property { BrokeredBy = "REAL ESTATE MAVENS", Status = "active", Price = 1949990m, Bedrooms = 5, Bathrooms = 6, AcreLot = 1.20m, LotSqft = 4367, Street = "31615 Live Oak Canyon Rd", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.040, Longitude = -117.158, HoaFee = 0, PropertyTax = 19500, Utilities = 480, SchoolRating = 76, CrimeScore = 45, Walkability = 28, TransitAccess = 12, AmenitiesScore = 52, CommuteMin = 40, YearBuilt = 2005, LastRenovation = null, RoofCondition = 88, AcCondition = 89, PlumbingCondition = 86, ElectricalCondition = 86, FloorPlanScore = 88, FutureAppreciation = 82, ResalePotential = 85, FloodRisk = 12, NoiseLevel = 12 },
            new Property { BrokeredBy = "CENTURY 21 EXPERIENCE", Status = "active", Price = 1550000m, Bedrooms = 5, Bathrooms = 5, AcreLot = 0.65m, LotSqft = 4458, Street = "215 San Rafael St", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.058, Longitude = -117.190, HoaFee = 0, PropertyTax = 15500, Utilities = 420, SchoolRating = 86, CrimeScore = 62, Walkability = 55, TransitAccess = 32, AmenitiesScore = 74, CommuteMin = 25, YearBuilt = 2000, LastRenovation = 2019, RoofCondition = 88, AcCondition = 89, PlumbingCondition = 86, ElectricalCondition = 85, FloorPlanScore = 89, FutureAppreciation = 82, ResalePotential = 84, FloodRisk = 9, NoiseLevel = 22 },
            new Property { BrokeredBy = "EXP REALTY OF GREATER LOS ANGELES", Status = "active", Price = 760000m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.23m, LotSqft = 2232, Street = "412 Phlox Ct", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.053, Longitude = -117.186, HoaFee = 0, PropertyTax = 7600, Utilities = 290, SchoolRating = 80, CrimeScore = 60, Walkability = 56, TransitAccess = 36, AmenitiesScore = 68, CommuteMin = 25, YearBuilt = 1991, LastRenovation = null, RoofCondition = 80, AcCondition = 81, PlumbingCondition = 78, ElectricalCondition = 77, FloorPlanScore = 79, FutureAppreciation = 70, ResalePotential = 72, FloodRisk = 9, NoiseLevel = 27 },
            new Property { BrokeredBy = "PATRICIA HICKS REALTOR", Status = "active", Price = 949900m, Bedrooms = 4, Bathrooms = 4, AcreLot = 0.28m, LotSqft = 2854, Street = "1575 Grove St", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.063, Longitude = -117.167, HoaFee = 0, PropertyTax = 9499, Utilities = 320, SchoolRating = 80, CrimeScore = 56, Walkability = 50, TransitAccess = 30, AmenitiesScore = 64, CommuteMin = 28, YearBuilt = 1996, LastRenovation = null, RoofCondition = 82, AcCondition = 83, PlumbingCondition = 81, ElectricalCondition = 80, FloorPlanScore = 82, FutureAppreciation = 73, ResalePotential = 75, FloodRisk = 11, NoiseLevel = 29 },
            new Property { BrokeredBy = "CENTURY 21 LOIS LAUER REALTY", Status = "active", Price = 500000m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.06m, LotSqft = 1805, Street = "61 Sparrow Ct", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.173, HoaFee = 300, PropertyTax = 5000, Utilities = 240, SchoolRating = 72, CrimeScore = 56, Walkability = 58, TransitAccess = 42, AmenitiesScore = 62, CommuteMin = 22, YearBuilt = 2004, LastRenovation = null, RoofCondition = 78, AcCondition = 79, PlumbingCondition = 76, ElectricalCondition = 76, FloorPlanScore = 73, FutureAppreciation = 64, ResalePotential = 65, FloodRisk = 10, NoiseLevel = 38 },
            new Property { BrokeredBy = "CENTURY 21 TOP PRODUCERS", Status = "active", Price = 1499900m, Bedrooms = 6, Bathrooms = 4, AcreLot = 0.80m, LotSqft = 4227, Street = "31027 E Sunset Dr N", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.050, Longitude = -117.175, HoaFee = 0, PropertyTax = 15000, Utilities = 420, SchoolRating = 78, CrimeScore = 50, Walkability = 35, TransitAccess = 18, AmenitiesScore = 58, CommuteMin = 35, YearBuilt = 2001, LastRenovation = null, RoofCondition = 84, AcCondition = 85, PlumbingCondition = 82, ElectricalCondition = 81, FloorPlanScore = 84, FutureAppreciation = 78, ResalePotential = 80, FloodRisk = 12, NoiseLevel = 18 },
            new Property { BrokeredBy = "ONE WEST REALTY", Status = "active", Price = 675000m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.21m, LotSqft = 2010, Street = "1688 Camellia Ln", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.063, Longitude = -117.166, HoaFee = 0, PropertyTax = 6750, Utilities = 270, SchoolRating = 72, CrimeScore = 56, Walkability = 50, TransitAccess = 30, AmenitiesScore = 60, CommuteMin = 28, YearBuilt = 1992, LastRenovation = null, RoofCondition = 76, AcCondition = 77, PlumbingCondition = 74, ElectricalCondition = 73, FloorPlanScore = 73, FutureAppreciation = 66, ResalePotential = 67, FloodRisk = 12, NoiseLevel = 33 },
            new Property { BrokeredBy = "RE/MAX TIME REALTY", Status = "active", Price = 729000m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.25m, LotSqft = 2477, Street = "1237 Sherry Way", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.168, HoaFee = 0, PropertyTax = 7290, Utilities = 280, SchoolRating = 74, CrimeScore = 56, Walkability = 50, TransitAccess = 30, AmenitiesScore = 62, CommuteMin = 28, YearBuilt = 1995, LastRenovation = null, RoofCondition = 78, AcCondition = 79, PlumbingCondition = 76, ElectricalCondition = 75, FloorPlanScore = 76, FutureAppreciation = 68, ResalePotential = 70, FloodRisk = 11, NoiseLevel = 32 },
            new Property { BrokeredBy = "CENTURY 21 LOIS LAUER REALTY", Status = "active", Price = 1349000m, Bedrooms = 4, Bathrooms = 4, AcreLot = 0.75m, LotSqft = 3732, Street = "13049 Burns Ln", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.043, Longitude = -117.155, HoaFee = 0, PropertyTax = 13490, Utilities = 400, SchoolRating = 76, CrimeScore = 48, Walkability = 30, TransitAccess = 15, AmenitiesScore = 55, CommuteMin = 36, YearBuilt = 1999, LastRenovation = 2015, RoofCondition = 84, AcCondition = 85, PlumbingCondition = 82, ElectricalCondition = 81, FloorPlanScore = 82, FutureAppreciation = 76, ResalePotential = 78, FloodRisk = 13, NoiseLevel = 18 },
            new Property { BrokeredBy = "YUCAIPA VALLEY REAL ESTATE", Status = "active", Price = 579000m, Bedrooms = 4, Bathrooms = 2, AcreLot = 0.18m, LotSqft = 1308, Street = "1602 Glover St", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.064, Longitude = -117.165, HoaFee = 0, PropertyTax = 5790, Utilities = 250, SchoolRating = 70, CrimeScore = 56, Walkability = 50, TransitAccess = 30, AmenitiesScore = 58, CommuteMin = 28, YearBuilt = 1981, LastRenovation = null, RoofCondition = 70, AcCondition = 72, PlumbingCondition = 70, ElectricalCondition = 69, FloorPlanScore = 68, FutureAppreciation = 63, ResalePotential = 65, FloodRisk = 13, NoiseLevel = 35 },
            new Property { BrokeredBy = "THE REAL ESTATE GROUP", Status = "active", Price = 749000m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.24m, LotSqft = 2040, Street = "505 E Sunset Dr", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.056, Longitude = -117.175, HoaFee = 0, PropertyTax = 7490, Utilities = 290, SchoolRating = 80, CrimeScore = 58, Walkability = 55, TransitAccess = 35, AmenitiesScore = 68, CommuteMin = 26, YearBuilt = 1990, LastRenovation = null, RoofCondition = 80, AcCondition = 81, PlumbingCondition = 78, ElectricalCondition = 77, FloorPlanScore = 79, FutureAppreciation = 70, ResalePotential = 72, FloodRisk = 9, NoiseLevel = 28 },
            new Property { BrokeredBy = "ROA CALIFORNIA INC", Status = "active", Price = 709999m, Bedrooms = 4, Bathrooms = 2, AcreLot = 0.19m, LotSqft = 1556, Street = "15 Naomi St", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.061, Longitude = -117.170, HoaFee = 0, PropertyTax = 7100, Utilities = 270, SchoolRating = 72, CrimeScore = 56, Walkability = 52, TransitAccess = 32, AmenitiesScore = 60, CommuteMin = 27, YearBuilt = 1984, LastRenovation = null, RoofCondition = 72, AcCondition = 74, PlumbingCondition = 71, ElectricalCondition = 70, FloorPlanScore = 70, FutureAppreciation = 65, ResalePotential = 66, FloodRisk = 12, NoiseLevel = 34 },
            new Property { BrokeredBy = "THE ASSOCIATES REALTY GROUP", Status = "active", Price = 629900m, Bedrooms = 3, Bathrooms = 2, AcreLot = 0.19m, LotSqft = 1536, Street = "323 E Colton Ave", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.060, Longitude = -117.170, HoaFee = 0, PropertyTax = 6299, Utilities = 260, SchoolRating = 70, CrimeScore = 56, Walkability = 52, TransitAccess = 32, AmenitiesScore = 58, CommuteMin = 27, YearBuilt = 1982, LastRenovation = null, RoofCondition = 70, AcCondition = 72, PlumbingCondition = 70, ElectricalCondition = 69, FloorPlanScore = 68, FutureAppreciation = 63, ResalePotential = 65, FloodRisk = 13, NoiseLevel = 35 },
            new Property { BrokeredBy = "REALTY ONE GROUP HOMELINK", Status = "active", Price = 478111m, Bedrooms = 3, Bathrooms = 1, AcreLot = 0.16m, LotSqft = 1261, Street = "1225 Alta St", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.061, Longitude = -117.170, HoaFee = 0, PropertyTax = 4781, Utilities = 230, SchoolRating = 66, CrimeScore = 58, Walkability = 55, TransitAccess = 33, AmenitiesScore = 55, CommuteMin = 28, YearBuilt = 1977, LastRenovation = null, RoofCondition = 66, AcCondition = 67, PlumbingCondition = 65, ElectricalCondition = 64, FloorPlanScore = 63, FutureAppreciation = 58, ResalePotential = 59, FloodRisk = 13, NoiseLevel = 36 },
            new Property { BrokeredBy = "HOMESMART", Status = "active", Price = 1950000m, Bedrooms = 6, Bathrooms = 6, AcreLot = 0.75m, LotSqft = 6938, Street = "1377 Knoll Rd", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.057, Longitude = -117.190, HoaFee = 0, PropertyTax = 19500, Utilities = 480, SchoolRating = 86, CrimeScore = 62, Walkability = 52, TransitAccess = 30, AmenitiesScore = 74, CommuteMin = 26, YearBuilt = 2003, LastRenovation = 2021, RoofCondition = 90, AcCondition = 91, PlumbingCondition = 88, ElectricalCondition = 88, FloorPlanScore = 90, FutureAppreciation = 84, ResalePotential = 86, FloodRisk = 9, NoiseLevel = 22 },
            new Property { BrokeredBy = "RE/MAX ADVANTAGE", Status = "active", Price = 1647000m, Bedrooms = 4, Bathrooms = 4, AcreLot = 0.95m, LotSqft = 4023, Street = "748 La Solana Dr", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.050, Longitude = -117.193, HoaFee = 0, PropertyTax = 16470, Utilities = 440, SchoolRating = 82, CrimeScore = 55, Walkability = 40, TransitAccess = 20, AmenitiesScore = 64, CommuteMin = 30, YearBuilt = 1998, LastRenovation = 2016, RoofCondition = 88, AcCondition = 89, PlumbingCondition = 86, ElectricalCondition = 85, FloorPlanScore = 87, FutureAppreciation = 80, ResalePotential = 82, FloodRisk = 10, NoiseLevel = 20 },
            new Property { BrokeredBy = "REALTY MASTERS & ASSOCIATES", Status = "active", Price = 649900m, Bedrooms = 3, Bathrooms = 2, AcreLot = 0.20m, LotSqft = 1740, Street = "1326 Campus Ave", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.169, HoaFee = 0, PropertyTax = 6499, Utilities = 260, SchoolRating = 70, CrimeScore = 56, Walkability = 50, TransitAccess = 30, AmenitiesScore = 58, CommuteMin = 28, YearBuilt = 1984, LastRenovation = null, RoofCondition = 72, AcCondition = 73, PlumbingCondition = 70, ElectricalCondition = 69, FloorPlanScore = 69, FutureAppreciation = 63, ResalePotential = 64, FloodRisk = 13, NoiseLevel = 34 },
            new Property { BrokeredBy = "BHHS PERRIE MUNDY REALTY GROUP", Status = "active", Price = 1795000m, Bedrooms = 4, Bathrooms = 4, AcreLot = 0.80m, LotSqft = 3291, Street = "12669 Valley View Ln", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.045, Longitude = -117.162, HoaFee = 0, PropertyTax = 17950, Utilities = 440, SchoolRating = 82, CrimeScore = 50, Walkability = 38, TransitAccess = 20, AmenitiesScore = 62, CommuteMin = 33, YearBuilt = 2001, LastRenovation = 2018, RoofCondition = 88, AcCondition = 89, PlumbingCondition = 86, ElectricalCondition = 85, FloorPlanScore = 87, FutureAppreciation = 81, ResalePotential = 83, FloodRisk = 11, NoiseLevel = 18 },
            new Property { BrokeredBy = "CENTURY 21 LOIS LAUER REALTY", Status = "active", Price = 1600000m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.55m, LotSqft = 3094, Street = "1805 Canyon Rd", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.056, Longitude = -117.194, HoaFee = 0, PropertyTax = 16000, Utilities = 420, SchoolRating = 82, CrimeScore = 55, Walkability = 45, TransitAccess = 22, AmenitiesScore = 68, CommuteMin = 28, YearBuilt = 1997, LastRenovation = 2020, RoofCondition = 88, AcCondition = 89, PlumbingCondition = 87, ElectricalCondition = 86, FloorPlanScore = 87, FutureAppreciation = 80, ResalePotential = 83, FloodRisk = 10, NoiseLevel = 20 },
            new Property { BrokeredBy = "COLDWELL BANKER REALTY", Status = "active", Price = 920000m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.24m, LotSqft = 2232, Street = "1370 Oak St", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.055, Longitude = -117.182, HoaFee = 0, PropertyTax = 9200, Utilities = 310, SchoolRating = 82, CrimeScore = 60, Walkability = 58, TransitAccess = 38, AmenitiesScore = 70, CommuteMin = 24, YearBuilt = 1992, LastRenovation = 2016, RoofCondition = 82, AcCondition = 84, PlumbingCondition = 80, ElectricalCondition = 79, FloorPlanScore = 83, FutureAppreciation = 74, ResalePotential = 76, FloodRisk = 9, NoiseLevel = 27 },
            new Property { BrokeredBy = "PRICE REAL ESTATE GROUP INC", Status = "active", Price = 595000m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.13m, LotSqft = 1008, Street = "903 Webster St", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.061, Longitude = -117.173, HoaFee = 0, PropertyTax = 5950, Utilities = 250, SchoolRating = 70, CrimeScore = 56, Walkability = 55, TransitAccess = 34, AmenitiesScore = 58, CommuteMin = 27, YearBuilt = 1982, LastRenovation = null, RoofCondition = 70, AcCondition = 71, PlumbingCondition = 69, ElectricalCondition = 68, FloorPlanScore = 68, FutureAppreciation = 63, ResalePotential = 64, FloodRisk = 13, NoiseLevel = 35 },
            new Property { BrokeredBy = "BERKSHIRE HATHAWAY HOMESERVICES CALIFORNIA REALTY", Status = "active", Price = 1525000m, Bedrooms = 5, Bathrooms = 5, AcreLot = 1.00m, LotSqft = 4926, Street = "30693 E Sunset Dr S", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.048, Longitude = -117.178, HoaFee = 0, PropertyTax = 15250, Utilities = 430, SchoolRating = 80, CrimeScore = 50, Walkability = 35, TransitAccess = 18, AmenitiesScore = 60, CommuteMin = 33, YearBuilt = 2002, LastRenovation = null, RoofCondition = 86, AcCondition = 87, PlumbingCondition = 84, ElectricalCondition = 83, FloorPlanScore = 86, FutureAppreciation = 79, ResalePotential = 81, FloodRisk = 12, NoiseLevel = 18 },
            new Property { BrokeredBy = "CAMINO REALTY", Status = "active", Price = 759900m, Bedrooms = 5, Bathrooms = 3, AcreLot = 0.40m, LotSqft = 2687, Street = "31156 Danelaw Ave", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.047, Longitude = -117.188, HoaFee = 0, PropertyTax = 7599, Utilities = 290, SchoolRating = 74, CrimeScore = 52, Walkability = 38, TransitAccess = 20, AmenitiesScore = 58, CommuteMin = 33, YearBuilt = 1988, LastRenovation = null, RoofCondition = 76, AcCondition = 77, PlumbingCondition = 74, ElectricalCondition = 73, FloorPlanScore = 73, FutureAppreciation = 67, ResalePotential = 70, FloodRisk = 12, NoiseLevel = 22 },
            new Property { BrokeredBy = "ROBERT GELFAND BROKER", Status = "active", Price = 1770000m, Bedrooms = 5, Bathrooms = 5, AcreLot = 1.10m, LotSqft = 4650, Street = "652 Fairway Dr", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.053, Longitude = -117.194, HoaFee = 0, PropertyTax = 17700, Utilities = 450, SchoolRating = 84, CrimeScore = 55, Walkability = 45, TransitAccess = 22, AmenitiesScore = 70, CommuteMin = 27, YearBuilt = 2004, LastRenovation = 2019, RoofCondition = 88, AcCondition = 89, PlumbingCondition = 87, ElectricalCondition = 86, FloorPlanScore = 89, FutureAppreciation = 82, ResalePotential = 84, FloodRisk = 9, NoiseLevel = 20 },
            new Property { BrokeredBy = "EXP REALTY OF SOUTHERN CALIFORNIA INC", Status = "active", Price = 1150000m, Bedrooms = 4, Bathrooms = 4, AcreLot = 0.30m, LotSqft = 2466, Street = "1388 Brandon Ct", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.056, Longitude = -117.185, HoaFee = 0, PropertyTax = 11500, Utilities = 360, SchoolRating = 84, CrimeScore = 60, Walkability = 55, TransitAccess = 35, AmenitiesScore = 72, CommuteMin = 25, YearBuilt = 1996, LastRenovation = 2014, RoofCondition = 84, AcCondition = 86, PlumbingCondition = 82, ElectricalCondition = 81, FloorPlanScore = 85, FutureAppreciation = 77, ResalePotential = 79, FloodRisk = 9, NoiseLevel = 25 },
            new Property { BrokeredBy = "EXP REALTY OF SOUTHERN CALIFORNIA INC", Status = "active", Price = 1690000m, Bedrooms = 5, Bathrooms = 4, AcreLot = 0.85m, LotSqft = 4559, Street = "1608 Smiley Rdg", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.061, Longitude = -117.194, HoaFee = 0, PropertyTax = 16900, Utilities = 440, SchoolRating = 84, CrimeScore = 55, Walkability = 42, TransitAccess = 22, AmenitiesScore = 68, CommuteMin = 28, YearBuilt = 2003, LastRenovation = 2018, RoofCondition = 88, AcCondition = 89, PlumbingCondition = 86, ElectricalCondition = 85, FloorPlanScore = 88, FutureAppreciation = 81, ResalePotential = 83, FloodRisk = 10, NoiseLevel = 20 },
            new Property { BrokeredBy = "BHHS PERRIE MUNDY REALTY GROUP", Status = "active", Price = 649000m, Bedrooms = 2, Bathrooms = 1, AcreLot = 0.22m, LotSqft = 1022, Street = "1029 W Palm Ave", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.055, Longitude = -117.192, HoaFee = 0, PropertyTax = 6490, Utilities = 250, SchoolRating = 76, CrimeScore = 58, Walkability = 58, TransitAccess = 36, AmenitiesScore = 64, CommuteMin = 25, YearBuilt = 1935, LastRenovation = 2005, RoofCondition = 70, AcCondition = 72, PlumbingCondition = 68, ElectricalCondition = 67, FloorPlanScore = 72, FutureAppreciation = 65, ResalePotential = 67, FloodRisk = 9, NoiseLevel = 28 },
            new Property { BrokeredBy = "BHHS PERRIE MUNDY REALTY GROUP", Status = "active", Price = 1595000m, Bedrooms = 4, Bathrooms = 4, AcreLot = 0.65m, LotSqft = 4105, Street = "952 Creek View Ln", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.057, Longitude = -117.188, HoaFee = 0, PropertyTax = 15950, Utilities = 420, SchoolRating = 84, CrimeScore = 58, Walkability = 52, TransitAccess = 32, AmenitiesScore = 72, CommuteMin = 26, YearBuilt = 2002, LastRenovation = 2019, RoofCondition = 88, AcCondition = 89, PlumbingCondition = 87, ElectricalCondition = 86, FloorPlanScore = 88, FutureAppreciation = 81, ResalePotential = 83, FloodRisk = 9, NoiseLevel = 22 },
            new Property { BrokeredBy = "RE/MAX ADVANTAGE", Status = "active", Price = 1250000m, Bedrooms = 5, Bathrooms = 4, AcreLot = 0.55m, LotSqft = 3676, Street = "1617 Garden St", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.057, Longitude = -117.179, HoaFee = 0, PropertyTax = 12500, Utilities = 380, SchoolRating = 82, CrimeScore = 58, Walkability = 55, TransitAccess = 35, AmenitiesScore = 70, CommuteMin = 25, YearBuilt = 1997, LastRenovation = 2013, RoofCondition = 84, AcCondition = 86, PlumbingCondition = 82, ElectricalCondition = 81, FloorPlanScore = 85, FutureAppreciation = 77, ResalePotential = 79, FloodRisk = 9, NoiseLevel = 25 },
            new Property { BrokeredBy = "MOS REAL ESTATE", Status = "active", Price = 758000m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.27m, LotSqft = 2877, Street = "1744 Sunny Heights Ln", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.063, Longitude = -117.164, HoaFee = 0, PropertyTax = 7580, Utilities = 290, SchoolRating = 76, CrimeScore = 56, Walkability = 50, TransitAccess = 30, AmenitiesScore = 62, CommuteMin = 29, YearBuilt = 1995, LastRenovation = 2023, RoofCondition = 80, AcCondition = 82, PlumbingCondition = 78, ElectricalCondition = 77, FloorPlanScore = 78, FutureAppreciation = 70, ResalePotential = 72, FloodRisk = 11, NoiseLevel = 30 },
            new Property { BrokeredBy = "BERKSHIRE HATHAWAY HOMESERVICES CALIFORNIA REALTY", Status = "active", Price = 1999000m, Bedrooms = 6, Bathrooms = 6, AcreLot = 1.50m, LotSqft = 3726, Street = "30300 Live Oak Canyon Rd", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.042, Longitude = -117.155, HoaFee = 0, PropertyTax = 19990, Utilities = 480, SchoolRating = 76, CrimeScore = 45, Walkability = 28, TransitAccess = 12, AmenitiesScore = 55, CommuteMin = 40, YearBuilt = 2000, LastRenovation = 2017, RoofCondition = 88, AcCondition = 89, PlumbingCondition = 86, ElectricalCondition = 85, FloorPlanScore = 86, FutureAppreciation = 81, ResalePotential = 84, FloodRisk = 12, NoiseLevel = 12 },
            new Property { BrokeredBy = "CENTURY 21 LOIS LAUER REALTY", Status = "active", Price = 599900m, Bedrooms = 4, Bathrooms = 2, AcreLot = 0.22m, LotSqft = 1892, Street = "108 S Buena Vista St", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.054, Longitude = -117.181, HoaFee = 0, PropertyTax = 5999, Utilities = 260, SchoolRating = 72, CrimeScore = 58, Walkability = 58, TransitAccess = 36, AmenitiesScore = 62, CommuteMin = 23, YearBuilt = 1980, LastRenovation = null, RoofCondition = 70, AcCondition = 72, PlumbingCondition = 70, ElectricalCondition = 69, FloorPlanScore = 69, FutureAppreciation = 63, ResalePotential = 65, FloodRisk = 11, NoiseLevel = 33 },
            new Property { BrokeredBy = "CENTURY 21 LOIS LAUER REALTY", Status = "active", Price = 329000m, Bedrooms = 2, Bathrooms = 2, AcreLot = 0.05m, LotSqft = 1152, Street = "525 La Verne St", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.055, Longitude = -117.184, HoaFee = 280, PropertyTax = 3290, Utilities = 200, SchoolRating = 70, CrimeScore = 60, Walkability = 70, TransitAccess = 52, AmenitiesScore = 64, CommuteMin = 19, YearBuilt = 1985, LastRenovation = null, RoofCondition = 72, AcCondition = 73, PlumbingCondition = 70, ElectricalCondition = 70, FloorPlanScore = 70, FutureAppreciation = 60, ResalePotential = 61, FloodRisk = 10, NoiseLevel = 40 },
            new Property { BrokeredBy = "DYNASTY REAL ESTATE", Status = "active", Price = 589999m, Bedrooms = 3, Bathrooms = 2, AcreLot = 0.17m, LotSqft = 1325, Street = "1543 Hanford St", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.063, Longitude = -117.168, HoaFee = 0, PropertyTax = 5900, Utilities = 250, SchoolRating = 70, CrimeScore = 56, Walkability = 50, TransitAccess = 30, AmenitiesScore = 58, CommuteMin = 28, YearBuilt = 1984, LastRenovation = null, RoofCondition = 72, AcCondition = 73, PlumbingCondition = 70, ElectricalCondition = 69, FloorPlanScore = 69, FutureAppreciation = 63, ResalePotential = 65, FloodRisk = 13, NoiseLevel = 34 },
            new Property { BrokeredBy = "EXP REALTY OF SOUTHERN CALIFORNIA INC", Status = "active", Price = 2599999m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.70m, LotSqft = 3447, Street = "745 W Sunset Dr", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.056, Longitude = -117.195, HoaFee = 0, PropertyTax = 26000, Utilities = 500, SchoolRating = 84, CrimeScore = 58, Walkability = 48, TransitAccess = 25, AmenitiesScore = 70, CommuteMin = 28, YearBuilt = 1995, LastRenovation = 2021, RoofCondition = 88, AcCondition = 89, PlumbingCondition = 87, ElectricalCondition = 86, FloorPlanScore = 87, FutureAppreciation = 82, ResalePotential = 85, FloodRisk = 9, NoiseLevel = 20 },
            new Property { BrokeredBy = "LUXRE REALTY INC", Status = "active", Price = 618000m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.20m, LotSqft = 1832, Street = "1160 Via Ravenna St", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.169, HoaFee = 0, PropertyTax = 6180, Utilities = 260, SchoolRating = 72, CrimeScore = 56, Walkability = 52, TransitAccess = 32, AmenitiesScore = 60, CommuteMin = 27, YearBuilt = 1997, LastRenovation = 2023, RoofCondition = 78, AcCondition = 80, PlumbingCondition = 76, ElectricalCondition = 75, FloorPlanScore = 76, FutureAppreciation = 67, ResalePotential = 68, FloodRisk = 11, NoiseLevel = 32 },
            new Property { BrokeredBy = "BERKSHIRE HATHAWAY HOMESERVICES CALIFORNIA REALTY", Status = "active", Price = 724000m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.22m, LotSqft = 2038, Street = "1049 Evergreen Ct", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.170, HoaFee = 0, PropertyTax = 7240, Utilities = 280, SchoolRating = 74, CrimeScore = 56, Walkability = 52, TransitAccess = 32, AmenitiesScore = 62, CommuteMin = 27, YearBuilt = 1996, LastRenovation = null, RoofCondition = 78, AcCondition = 79, PlumbingCondition = 76, ElectricalCondition = 75, FloorPlanScore = 77, FutureAppreciation = 68, ResalePotential = 70, FloodRisk = 11, NoiseLevel = 32 },
            new Property { BrokeredBy = "SHAW REAL ESTATE BROKERS", Status = "active", Price = 2497000m, Bedrooms = 5, Bathrooms = 6, AcreLot = 1.20m, LotSqft = 5581, Street = "1922 Country Club Ln", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.055, Longitude = -117.193, HoaFee = 0, PropertyTax = 24970, Utilities = 520, SchoolRating = 86, CrimeScore = 58, Walkability = 48, TransitAccess = 25, AmenitiesScore = 74, CommuteMin = 27, YearBuilt = 2006, LastRenovation = 2020, RoofCondition = 90, AcCondition = 91, PlumbingCondition = 89, ElectricalCondition = 88, FloorPlanScore = 91, FutureAppreciation = 85, ResalePotential = 87, FloodRisk = 9, NoiseLevel = 20 },
            new Property { BrokeredBy = "EXP REALTY OF SOUTHERN CALIFORNIA INC", Status = "active", Price = 1799999m, Bedrooms = 4, Bathrooms = 3, AcreLot = 1.80m, LotSqft = 3880, Street = "28450 Live Oak Canyon Rd", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.041, Longitude = -117.162, HoaFee = 0, PropertyTax = 18000, Utilities = 460, SchoolRating = 74, CrimeScore = 45, Walkability = 28, TransitAccess = 12, AmenitiesScore = 52, CommuteMin = 40, YearBuilt = 1998, LastRenovation = null, RoofCondition = 84, AcCondition = 85, PlumbingCondition = 82, ElectricalCondition = 81, FloorPlanScore = 82, FutureAppreciation = 78, ResalePotential = 81, FloodRisk = 13, NoiseLevel = 12 },
            new Property { BrokeredBy = "MERITAGE HOMES OF CALIFORNIA", Status = "active", Price = 589000m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.07m, LotSqft = 1648, Street = "1114 Tropic Ct", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.165, HoaFee = 300, PropertyTax = 5890, Utilities = 250, SchoolRating = 76, CrimeScore = 55, Walkability = 62, TransitAccess = 52, AmenitiesScore = 66, CommuteMin = 24, YearBuilt = 2024, LastRenovation = null, RoofCondition = 92, AcCondition = 92, PlumbingCondition = 92, ElectricalCondition = 92, FloorPlanScore = 78, FutureAppreciation = 72, ResalePotential = 74, FloodRisk = 10, NoiseLevel = 30 },
            new Property { BrokeredBy = "REALTY MASTERS & ASSOCIATES", Status = "active", Price = 490000m, Bedrooms = 2, Bathrooms = 2, AcreLot = 0.05m, LotSqft = 1176, Street = "1580 Lisa Ln", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.063, Longitude = -117.167, HoaFee = 320, PropertyTax = 4900, Utilities = 230, SchoolRating = 72, CrimeScore = 56, Walkability = 55, TransitAccess = 38, AmenitiesScore = 60, CommuteMin = 25, YearBuilt = 2001, LastRenovation = null, RoofCondition = 78, AcCondition = 79, PlumbingCondition = 76, ElectricalCondition = 76, FloorPlanScore = 72, FutureAppreciation = 63, ResalePotential = 65, FloodRisk = 10, NoiseLevel = 35 },
            new Property { BrokeredBy = "SMART SELL REAL ESTATE", Status = "active", Price = 595000m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.20m, LotSqft = 1758, Street = "1510 Karon St", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.063, Longitude = -117.167, HoaFee = 0, PropertyTax = 5950, Utilities = 260, SchoolRating = 70, CrimeScore = 56, Walkability = 52, TransitAccess = 32, AmenitiesScore = 60, CommuteMin = 28, YearBuilt = 1985, LastRenovation = null, RoofCondition = 70, AcCondition = 72, PlumbingCondition = 70, ElectricalCondition = 69, FloorPlanScore = 69, FutureAppreciation = 63, ResalePotential = 65, FloodRisk = 12, NoiseLevel = 34 },
            new Property { BrokeredBy = "IKON PROPERTIES & INVESTMENTS", Status = "active", Price = 649900m, Bedrooms = 3, Bathrooms = 4, AcreLot = 0.22m, LotSqft = 2045, Street = "434 Sonora Cir", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.054, Longitude = -117.187, HoaFee = 0, PropertyTax = 6499, Utilities = 270, SchoolRating = 76, CrimeScore = 60, Walkability = 58, TransitAccess = 38, AmenitiesScore = 66, CommuteMin = 24, YearBuilt = 1989, LastRenovation = 2012, RoofCondition = 76, AcCondition = 78, PlumbingCondition = 75, ElectricalCondition = 74, FloorPlanScore = 77, FutureAppreciation = 68, ResalePotential = 70, FloodRisk = 10, NoiseLevel = 29 },
            new Property { BrokeredBy = "KELLER WILLIAMS REALTY", Status = "active", Price = 1300000m, Bedrooms = 5, Bathrooms = 4, AcreLot = 0.55m, LotSqft = 4641, Street = "1641 Ford Ave", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.063, Longitude = -117.164, HoaFee = 0, PropertyTax = 13000, Utilities = 400, SchoolRating = 80, CrimeScore = 54, Walkability = 50, TransitAccess = 30, AmenitiesScore = 64, CommuteMin = 30, YearBuilt = 2000, LastRenovation = null, RoofCondition = 86, AcCondition = 87, PlumbingCondition = 84, ElectricalCondition = 83, FloorPlanScore = 86, FutureAppreciation = 78, ResalePotential = 80, FloodRisk = 11, NoiseLevel = 28 },
            new Property { BrokeredBy = "REALTY ONE GROUP ROADS", Status = "active", Price = 181899m, Bedrooms = 3, Bathrooms = 2, AcreLot = 0.07m, LotSqft = 1152, Street = "1721 E Colton Ave Space 33", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.059, Longitude = -117.162, HoaFee = 180, PropertyTax = 1819, Utilities = 170, SchoolRating = 58, CrimeScore = 55, Walkability = 45, TransitAccess = 25, AmenitiesScore = 50, CommuteMin = 30, YearBuilt = 1975, LastRenovation = null, RoofCondition = 58, AcCondition = 58, PlumbingCondition = 56, ElectricalCondition = 55, FloorPlanScore = 56, FutureAppreciation = 48, ResalePotential = 49, FloodRisk = 14, NoiseLevel = 38 },
            new Property { BrokeredBy = "BERKSHIRE HATHAWAY HOMESERVICES CALIFORNIA", Status = "active", Price = 174900m, Bedrooms = 2, Bathrooms = 2, AcreLot = 0.06m, LotSqft = 1040, Street = "450 Judson #94", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.061, Longitude = -117.172, HoaFee = 200, PropertyTax = 1749, Utilities = 165, SchoolRating = 58, CrimeScore = 55, Walkability = 45, TransitAccess = 25, AmenitiesScore = 50, CommuteMin = 27, YearBuilt = 1973, LastRenovation = null, RoofCondition = 56, AcCondition = 57, PlumbingCondition = 54, ElectricalCondition = 53, FloorPlanScore = 55, FutureAppreciation = 47, ResalePotential = 48, FloodRisk = 13, NoiseLevel = 38 },
            new Property { BrokeredBy = "BERKSHIRE HATHAWAY HOMESERVICES CALIFORNIA REALTY", Status = "active", Price = 1399000m, Bedrooms = 8, Bathrooms = 5, AcreLot = 0.55m, LotSqft = 5100, Street = "31607 Florida St", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.047, Longitude = -117.183, HoaFee = 0, PropertyTax = 13990, Utilities = 420, SchoolRating = 76, CrimeScore = 50, Walkability = 35, TransitAccess = 18, AmenitiesScore = 58, CommuteMin = 36, YearBuilt = 1998, LastRenovation = null, RoofCondition = 80, AcCondition = 81, PlumbingCondition = 78, ElectricalCondition = 77, FloorPlanScore = 80, FutureAppreciation = 73, ResalePotential = 76, FloodRisk = 12, NoiseLevel = 20 },
            new Property { BrokeredBy = "HASS & JOHN REAL ESTATE", Status = "active", Price = 649999m, Bedrooms = 4, Bathrooms = 2, AcreLot = 0.22m, LotSqft = 1945, Street = "1024 Lawton St", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.061, Longitude = -117.171, HoaFee = 0, PropertyTax = 6500, Utilities = 260, SchoolRating = 70, CrimeScore = 56, Walkability = 52, TransitAccess = 32, AmenitiesScore = 60, CommuteMin = 27, YearBuilt = 1982, LastRenovation = 2022, RoofCondition = 74, AcCondition = 76, PlumbingCondition = 73, ElectricalCondition = 72, FloorPlanScore = 70, FutureAppreciation = 64, ResalePotential = 65, FloodRisk = 12, NoiseLevel = 34 },
            new Property { BrokeredBy = "BERKSHIRE HATHAWAY HOMESERVICES CALIFORNIA REALTY", Status = "active", Price = 167000m, Bedrooms = 2, Bathrooms = 2, AcreLot = 0.06m, LotSqft = 1464, Street = "1251 E Lugonia Ave Space 24", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.059, Longitude = -117.162, HoaFee = 200, PropertyTax = 1670, Utilities = 165, SchoolRating = 58, CrimeScore = 55, Walkability = 45, TransitAccess = 25, AmenitiesScore = 50, CommuteMin = 30, YearBuilt = 1974, LastRenovation = null, RoofCondition = 56, AcCondition = 57, PlumbingCondition = 55, ElectricalCondition = 54, FloorPlanScore = 55, FutureAppreciation = 47, ResalePotential = 48, FloodRisk = 14, NoiseLevel = 38 },
            new Property { BrokeredBy = "KELLER WILLIAMS REALTY", Status = "active", Price = 649999m, Bedrooms = 3, Bathrooms = 1, AcreLot = 0.16m, LotSqft = 1200, Street = "421 La Verne St", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.055, Longitude = -117.184, HoaFee = 0, PropertyTax = 6500, Utilities = 250, SchoolRating = 74, CrimeScore = 60, Walkability = 62, TransitAccess = 42, AmenitiesScore = 64, CommuteMin = 20, YearBuilt = 1940, LastRenovation = 2008, RoofCondition = 68, AcCondition = 70, PlumbingCondition = 66, ElectricalCondition = 66, FloorPlanScore = 72, FutureAppreciation = 64, ResalePotential = 66, FloodRisk = 9, NoiseLevel = 38 },
            new Property { BrokeredBy = "CENTURY 21 LOIS LAUER REALTY", Status = "active", Price = 599777m, Bedrooms = 3, Bathrooms = 2, AcreLot = 0.06m, LotSqft = 1362, Street = "1032 Ardmore Cir", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.170, HoaFee = 280, PropertyTax = 5998, Utilities = 250, SchoolRating = 72, CrimeScore = 56, Walkability = 55, TransitAccess = 38, AmenitiesScore = 60, CommuteMin = 25, YearBuilt = 2002, LastRenovation = null, RoofCondition = 78, AcCondition = 79, PlumbingCondition = 76, ElectricalCondition = 76, FloorPlanScore = 73, FutureAppreciation = 64, ResalePotential = 66, FloodRisk = 10, NoiseLevel = 35 },
            new Property { BrokeredBy = "ROA CALIFORNIA INC", Status = "active", Price = 1200000m, Bedrooms = 5, Bathrooms = 3, AcreLot = 0.25m, LotSqft = 2793, Street = "116 Franklin Ave", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.055, Longitude = -117.182, HoaFee = 0, PropertyTax = 12000, Utilities = 370, SchoolRating = 80, CrimeScore = 60, Walkability = 55, TransitAccess = 36, AmenitiesScore = 67, CommuteMin = 25, YearBuilt = 1985, LastRenovation = null, RoofCondition = 78, AcCondition = 80, PlumbingCondition = 76, ElectricalCondition = 75, FloorPlanScore = 79, FutureAppreciation = 73, ResalePotential = 75, FloodRisk = 10, NoiseLevel = 30 },
            new Property { BrokeredBy = "MERITAGE HOMES OF CALIFORNIA", Status = "active", Price = 569000m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.07m, LotSqft = 1648, Street = "1118 Tropic Ct", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.165, HoaFee = 300, PropertyTax = 5690, Utilities = 250, SchoolRating = 76, CrimeScore = 55, Walkability = 62, TransitAccess = 52, AmenitiesScore = 66, CommuteMin = 24, YearBuilt = 2024, LastRenovation = null, RoofCondition = 92, AcCondition = 92, PlumbingCondition = 92, ElectricalCondition = 92, FloorPlanScore = 78, FutureAppreciation = 72, ResalePotential = 74, FloodRisk = 10, NoiseLevel = 30 },
            new Property { BrokeredBy = "M G R REAL ESTATE", Status = "active", Price = 469900m, Bedrooms = 2, Bathrooms = 2, AcreLot = 0.03m, LotSqft = 1070, Street = "93 Kansas St APT 802", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.054, Longitude = -117.184, HoaFee = 350, PropertyTax = 4699, Utilities = 220, SchoolRating = 72, CrimeScore = 60, Walkability = 75, TransitAccess = 58, AmenitiesScore = 70, CommuteMin = 18, YearBuilt = 2000, LastRenovation = null, RoofCondition = 78, AcCondition = 80, PlumbingCondition = 76, ElectricalCondition = 76, FloorPlanScore = 72, FutureAppreciation = 63, ResalePotential = 64, FloodRisk = 8, NoiseLevel = 42 },
            new Property { BrokeredBy = "MGA ASSOCIATES", Status = "active", Price = 499900m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.05m, LotSqft = 1392, Street = "122 N Tamarisk St", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.054, Longitude = -117.183, HoaFee = 320, PropertyTax = 4999, Utilities = 230, SchoolRating = 72, CrimeScore = 60, Walkability = 72, TransitAccess = 54, AmenitiesScore = 66, CommuteMin = 19, YearBuilt = 1998, LastRenovation = null, RoofCondition = 76, AcCondition = 77, PlumbingCondition = 74, ElectricalCondition = 74, FloorPlanScore = 73, FutureAppreciation = 63, ResalePotential = 64, FloodRisk = 9, NoiseLevel = 40 },
            new Property { BrokeredBy = "CENTURY 21 LOIS LAUER REALTY", Status = "active", Price = 450000m, Bedrooms = 3, Bathrooms = 2, AcreLot = 0.05m, LotSqft = 1486, Street = "246 E Fern Ave APT 109", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.053, Longitude = -117.185, HoaFee = 340, PropertyTax = 4500, Utilities = 225, SchoolRating = 72, CrimeScore = 60, Walkability = 72, TransitAccess = 54, AmenitiesScore = 66, CommuteMin = 19, YearBuilt = 1995, LastRenovation = null, RoofCondition = 74, AcCondition = 75, PlumbingCondition = 72, ElectricalCondition = 72, FloorPlanScore = 72, FutureAppreciation = 62, ResalePotential = 63, FloodRisk = 9, NoiseLevel = 40 },
            new Property { BrokeredBy = "MERITAGE HOMES OF CALIFORNIA", Status = "active", Price = 564000m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.07m, LotSqft = 1465, Street = "1116 Tropic Ct", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.165, HoaFee = 300, PropertyTax = 5640, Utilities = 250, SchoolRating = 76, CrimeScore = 55, Walkability = 62, TransitAccess = 52, AmenitiesScore = 66, CommuteMin = 24, YearBuilt = 2024, LastRenovation = null, RoofCondition = 92, AcCondition = 92, PlumbingCondition = 92, ElectricalCondition = 92, FloorPlanScore = 78, FutureAppreciation = 72, ResalePotential = 74, FloodRisk = 10, NoiseLevel = 30 },
            new Property { BrokeredBy = "MERITAGE HOMES OF CALIFORNIA", Status = "active", Price = 549000m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.07m, LotSqft = 1465, Street = "1112 Tropic Ct", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.165, HoaFee = 300, PropertyTax = 5490, Utilities = 250, SchoolRating = 76, CrimeScore = 55, Walkability = 62, TransitAccess = 52, AmenitiesScore = 66, CommuteMin = 24, YearBuilt = 2024, LastRenovation = null, RoofCondition = 92, AcCondition = 92, PlumbingCondition = 92, ElectricalCondition = 92, FloorPlanScore = 78, FutureAppreciation = 72, ResalePotential = 74, FloodRisk = 10, NoiseLevel = 30 },
            new Property { BrokeredBy = "BHHS PERRIE MUNDY REALTY GROUP", Status = "active", Price = 415000m, Bedrooms = 2, Bathrooms = 2, AcreLot = 0.05m, LotSqft = 1188, Street = "254 E Fern Ave APT 212", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.053, Longitude = -117.185, HoaFee = 340, PropertyTax = 4150, Utilities = 220, SchoolRating = 72, CrimeScore = 60, Walkability = 72, TransitAccess = 54, AmenitiesScore = 66, CommuteMin = 19, YearBuilt = 1995, LastRenovation = null, RoofCondition = 74, AcCondition = 75, PlumbingCondition = 72, ElectricalCondition = 72, FloorPlanScore = 71, FutureAppreciation = 61, ResalePotential = 62, FloodRisk = 9, NoiseLevel = 40 },
            new Property { BrokeredBy = "MERITAGE HOMES", Status = "active", Price = 569000m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.07m, LotSqft = 1465, Street = "1119 Tropic Ct", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.165, HoaFee = 300, PropertyTax = 5690, Utilities = 250, SchoolRating = 76, CrimeScore = 55, Walkability = 62, TransitAccess = 52, AmenitiesScore = 66, CommuteMin = 24, YearBuilt = 2024, LastRenovation = null, RoofCondition = 92, AcCondition = 92, PlumbingCondition = 92, ElectricalCondition = 92, FloorPlanScore = 78, FutureAppreciation = 72, ResalePotential = 74, FloodRisk = 10, NoiseLevel = 30 },
            new Property { BrokeredBy = "RE/MAX ADVANTAGE", Status = "active", Price = 325000m, Bedrooms = 2, Bathrooms = 2, AcreLot = 0.06m, LotSqft = 1056, Street = "1174 Benbow Pl", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.173, HoaFee = 280, PropertyTax = 3250, Utilities = 190, SchoolRating = 68, CrimeScore = 56, Walkability = 52, TransitAccess = 35, AmenitiesScore = 55, CommuteMin = 26, YearBuilt = 1975, LastRenovation = null, RoofCondition = 65, AcCondition = 66, PlumbingCondition = 63, ElectricalCondition = 62, FloorPlanScore = 63, FutureAppreciation = 56, ResalePotential = 57, FloodRisk = 13, NoiseLevel = 36 },
            new Property { BrokeredBy = "REBECCA AUSTIN BROKER", Status = "active", Price = 1074990m, Bedrooms = 5, Bathrooms = 5, AcreLot = 0.22m, LotSqft = 3306, Street = "1452 Moore St", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.163, HoaFee = 350, PropertyTax = 10750, Utilities = 360, SchoolRating = 78, CrimeScore = 54, Walkability = 58, TransitAccess = 48, AmenitiesScore = 66, CommuteMin = 26, YearBuilt = 2024, LastRenovation = null, RoofCondition = 94, AcCondition = 94, PlumbingCondition = 94, ElectricalCondition = 94, FloorPlanScore = 82, FutureAppreciation = 76, ResalePotential = 78, FloodRisk = 10, NoiseLevel = 28 },
            new Property { BrokeredBy = "PRICE REAL ESTATE GROUP INC", Status = "active", Price = 429999m, Bedrooms = 2, Bathrooms = 3, AcreLot = 0.05m, LotSqft = 1394, Street = "1200 Highland Ave APT 207", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.170, HoaFee = 340, PropertyTax = 4300, Utilities = 215, SchoolRating = 70, CrimeScore = 56, Walkability = 58, TransitAccess = 42, AmenitiesScore = 60, CommuteMin = 24, YearBuilt = 1999, LastRenovation = null, RoofCondition = 74, AcCondition = 75, PlumbingCondition = 72, ElectricalCondition = 72, FloorPlanScore = 70, FutureAppreciation = 61, ResalePotential = 62, FloodRisk = 10, NoiseLevel = 38 },
            new Property { BrokeredBy = "PONCE & PONCE REALTY INC", Status = "active", Price = 475000m, Bedrooms = 2, Bathrooms = 2, AcreLot = 0.05m, LotSqft = 1105, Street = "1592 Christopher Ln", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.063, Longitude = -117.167, HoaFee = 310, PropertyTax = 4750, Utilities = 230, SchoolRating = 72, CrimeScore = 56, Walkability = 55, TransitAccess = 38, AmenitiesScore = 60, CommuteMin = 25, YearBuilt = 2002, LastRenovation = null, RoofCondition = 78, AcCondition = 79, PlumbingCondition = 76, ElectricalCondition = 76, FloorPlanScore = 72, FutureAppreciation = 63, ResalePotential = 65, FloodRisk = 10, NoiseLevel = 35 },
            new Property { BrokeredBy = "TRI POINTE HOMES HOLDINGS INC", Status = "active", Price = 939000m, Bedrooms = 5, Bathrooms = 4, AcreLot = 0.22m, LotSqft = 3608, Street = "1713 Wren Ave", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.163, HoaFee = 350, PropertyTax = 9390, Utilities = 340, SchoolRating = 78, CrimeScore = 54, Walkability = 58, TransitAccess = 48, AmenitiesScore = 66, CommuteMin = 26, YearBuilt = 2024, LastRenovation = null, RoofCondition = 94, AcCondition = 94, PlumbingCondition = 94, ElectricalCondition = 94, FloorPlanScore = 84, FutureAppreciation = 77, ResalePotential = 79, FloodRisk = 10, NoiseLevel = 28 },
            new Property { BrokeredBy = "RE/MAX CHAMPIONS", Status = "active", Price = 1350000m, Bedrooms = 5, Bathrooms = 4, AcreLot = 0.60m, LotSqft = 3800, Street = "12698 La Solana Dr", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.048, Longitude = -117.193, HoaFee = 0, PropertyTax = 13500, Utilities = 400, SchoolRating = 80, CrimeScore = 52, Walkability = 38, TransitAccess = 20, AmenitiesScore = 62, CommuteMin = 32, YearBuilt = 2000, LastRenovation = null, RoofCondition = 86, AcCondition = 87, PlumbingCondition = 84, ElectricalCondition = 83, FloorPlanScore = 85, FutureAppreciation = 78, ResalePotential = 80, FloodRisk = 11, NoiseLevel = 20 },
            new Property { BrokeredBy = "CENTURY 21 LOIS LAUER REALTY", Status = "active", Price = 559000m, Bedrooms = 3, Bathrooms = 2, AcreLot = 0.06m, LotSqft = 1643, Street = "1575 Christopher Ln", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.063, Longitude = -117.167, HoaFee = 310, PropertyTax = 5590, Utilities = 245, SchoolRating = 72, CrimeScore = 56, Walkability = 55, TransitAccess = 38, AmenitiesScore = 60, CommuteMin = 25, YearBuilt = 2001, LastRenovation = null, RoofCondition = 76, AcCondition = 77, PlumbingCondition = 74, ElectricalCondition = 74, FloorPlanScore = 72, FutureAppreciation = 63, ResalePotential = 64, FloodRisk = 10, NoiseLevel = 35 },
            new Property { BrokeredBy = "BHHS PERRIE MUNDY REALTY GROUP", Status = "active", Price = 2500000m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.50m, LotSqft = 2183, Street = "831 W Lugonia Ave", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.059, Longitude = -117.177, HoaFee = 0, PropertyTax = 25000, Utilities = 500, SchoolRating = 76, CrimeScore = 52, Walkability = 50, TransitAccess = 30, AmenitiesScore = 62, CommuteMin = 28, YearBuilt = 1990, LastRenovation = null, RoofCondition = 76, AcCondition = 77, PlumbingCondition = 74, ElectricalCondition = 73, FloorPlanScore = 72, FutureAppreciation = 72, ResalePotential = 75, FloodRisk = 10, NoiseLevel = 28 },
            new Property { BrokeredBy = "RE/MAX ADVANTAGE", Status = "active", Price = 649000m, Bedrooms = 2, Bathrooms = 2, AcreLot = 0.13m, LotSqft = 1107, Street = "509 S 4th St", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.052, Longitude = -117.180, HoaFee = 0, PropertyTax = 6490, Utilities = 250, SchoolRating = 76, CrimeScore = 60, Walkability = 62, TransitAccess = 42, AmenitiesScore = 66, CommuteMin = 20, YearBuilt = 1938, LastRenovation = 2010, RoofCondition = 70, AcCondition = 72, PlumbingCondition = 68, ElectricalCondition = 67, FloorPlanScore = 74, FutureAppreciation = 66, ResalePotential = 68, FloodRisk = 9, NoiseLevel = 35 },
            new Property { BrokeredBy = "MERITAGE HOMES OF CALIFORNIA", Status = "active", Price = 695400m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.08m, LotSqft = 2020, Street = "2084 Meyer Ln", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.163, HoaFee = 300, PropertyTax = 6954, Utilities = 280, SchoolRating = 76, CrimeScore = 55, Walkability = 62, TransitAccess = 52, AmenitiesScore = 66, CommuteMin = 24, YearBuilt = 2024, LastRenovation = null, RoofCondition = 94, AcCondition = 94, PlumbingCondition = 94, ElectricalCondition = 94, FloorPlanScore = 80, FutureAppreciation = 74, ResalePotential = 76, FloodRisk = 10, NoiseLevel = 30 },
            new Property { BrokeredBy = "TRI POINTE HOMES HOLDINGS INC", Status = "active", Price = 997000m, Bedrooms = 5, Bathrooms = 4, AcreLot = 0.22m, LotSqft = 3897, Street = "1731 Wren Ave", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.163, HoaFee = 350, PropertyTax = 9970, Utilities = 350, SchoolRating = 78, CrimeScore = 54, Walkability = 58, TransitAccess = 48, AmenitiesScore = 68, CommuteMin = 26, YearBuilt = 2024, LastRenovation = null, RoofCondition = 94, AcCondition = 94, PlumbingCondition = 94, ElectricalCondition = 94, FloorPlanScore = 86, FutureAppreciation = 78, ResalePotential = 80, FloodRisk = 10, NoiseLevel = 28 },
            new Property { BrokeredBy = "TRI POINTE HOMES HOLDINGS INC", Status = "active", Price = 898000m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.22m, LotSqft = 2813, Street = "1568 Pintail St", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.164, HoaFee = 350, PropertyTax = 8980, Utilities = 320, SchoolRating = 76, CrimeScore = 54, Walkability = 58, TransitAccess = 48, AmenitiesScore = 66, CommuteMin = 26, YearBuilt = 2024, LastRenovation = null, RoofCondition = 94, AcCondition = 94, PlumbingCondition = 94, ElectricalCondition = 94, FloorPlanScore = 82, FutureAppreciation = 76, ResalePotential = 78, FloodRisk = 10, NoiseLevel = 28 },
            new Property { BrokeredBy = "SHAW REAL ESTATE BROKERS", Status = "active", Price = 295000m, Bedrooms = 2, Bathrooms = 2, AcreLot = 0.08m, LotSqft = 1368, Street = "1331 Century St", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.172, HoaFee = 200, PropertyTax = 2950, Utilities = 185, SchoolRating = 62, CrimeScore = 56, Walkability = 50, TransitAccess = 30, AmenitiesScore = 52, CommuteMin = 28, YearBuilt = 1974, LastRenovation = null, RoofCondition = 60, AcCondition = 61, PlumbingCondition = 58, ElectricalCondition = 57, FloorPlanScore = 58, FutureAppreciation = 50, ResalePotential = 51, FloodRisk = 14, NoiseLevel = 36 },
            new Property { BrokeredBy = "MERITAGE HOMES OF CALIFORNIA", Status = "active", Price = 639800m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.07m, LotSqft = 1815, Street = "1140 Tropic Ct", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.165, HoaFee = 300, PropertyTax = 6398, Utilities = 255, SchoolRating = 76, CrimeScore = 55, Walkability = 62, TransitAccess = 52, AmenitiesScore = 66, CommuteMin = 24, YearBuilt = 2024, LastRenovation = null, RoofCondition = 92, AcCondition = 92, PlumbingCondition = 92, ElectricalCondition = 92, FloorPlanScore = 79, FutureAppreciation = 73, ResalePotential = 75, FloodRisk = 10, NoiseLevel = 30 },
            new Property { BrokeredBy = "REBECCA AUSTIN BROKER", Status = "active", Price = 1058705m, Bedrooms = 4, Bathrooms = 5, AcreLot = 0.22m, LotSqft = 2803, Street = "1479 Moore St", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.163, HoaFee = 350, PropertyTax = 10587, Utilities = 355, SchoolRating = 78, CrimeScore = 54, Walkability = 58, TransitAccess = 48, AmenitiesScore = 66, CommuteMin = 26, YearBuilt = 2024, LastRenovation = null, RoofCondition = 94, AcCondition = 94, PlumbingCondition = 94, ElectricalCondition = 94, FloorPlanScore = 83, FutureAppreciation = 76, ResalePotential = 78, FloodRisk = 10, NoiseLevel = 28 },
            new Property { BrokeredBy = "REBECCA AUSTIN BROKER", Status = "active", Price = 1034990m, Bedrooms = 4, Bathrooms = 5, AcreLot = 0.22m, LotSqft = 2803, Street = "1458 Moore St", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.163, HoaFee = 350, PropertyTax = 10350, Utilities = 355, SchoolRating = 78, CrimeScore = 54, Walkability = 58, TransitAccess = 48, AmenitiesScore = 66, CommuteMin = 26, YearBuilt = 2024, LastRenovation = null, RoofCondition = 94, AcCondition = 94, PlumbingCondition = 94, ElectricalCondition = 94, FloorPlanScore = 83, FutureAppreciation = 76, ResalePotential = 78, FloodRisk = 10, NoiseLevel = 28 },
            new Property { BrokeredBy = "BEAZER HOMES", Status = "active", Price = 1149990m, Bedrooms = 5, Bathrooms = 4, AcreLot = 0.22m, LotSqft = 3306, Street = "1472 Moore St", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.163, HoaFee = 350, PropertyTax = 11500, Utilities = 360, SchoolRating = 78, CrimeScore = 54, Walkability = 58, TransitAccess = 48, AmenitiesScore = 66, CommuteMin = 26, YearBuilt = 2024, LastRenovation = null, RoofCondition = 94, AcCondition = 94, PlumbingCondition = 94, ElectricalCondition = 94, FloorPlanScore = 84, FutureAppreciation = 77, ResalePotential = 79, FloodRisk = 10, NoiseLevel = 28 },
            new Property { BrokeredBy = "MERITAGE HOMES OF CALIFORNIA", Status = "active", Price = 739900m, Bedrooms = 5, Bathrooms = 3, AcreLot = 0.09m, LotSqft = 2418, Street = "2070 Tangelo Ln", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.163, HoaFee = 300, PropertyTax = 7399, Utilities = 290, SchoolRating = 76, CrimeScore = 55, Walkability = 62, TransitAccess = 52, AmenitiesScore = 66, CommuteMin = 24, YearBuilt = 2024, LastRenovation = null, RoofCondition = 92, AcCondition = 92, PlumbingCondition = 92, ElectricalCondition = 92, FloorPlanScore = 82, FutureAppreciation = 75, ResalePotential = 77, FloodRisk = 10, NoiseLevel = 30 },
            new Property { BrokeredBy = "MERITAGE HOMES OF CALIFORNIA", Status = "active", Price = 702700m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.08m, LotSqft = 2020, Street = "2051 Tangelo Ln", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.163, HoaFee = 300, PropertyTax = 7027, Utilities = 285, SchoolRating = 76, CrimeScore = 55, Walkability = 62, TransitAccess = 52, AmenitiesScore = 66, CommuteMin = 24, YearBuilt = 2024, LastRenovation = null, RoofCondition = 92, AcCondition = 92, PlumbingCondition = 92, ElectricalCondition = 92, FloorPlanScore = 80, FutureAppreciation = 74, ResalePotential = 76, FloodRisk = 10, NoiseLevel = 30 },
            new Property { BrokeredBy = "PONCE & PONCE REALTY INC", Status = "active", Price = 195000m, Bedrooms = 2, Bathrooms = 2, AcreLot = 0.05m, LotSqft = 1400, Street = "626 Dearborn #7", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.063, Longitude = -117.175, HoaFee = 220, PropertyTax = 1950, Utilities = 170, SchoolRating = 58, CrimeScore = 56, Walkability = 48, TransitAccess = 28, AmenitiesScore = 50, CommuteMin = 26, YearBuilt = 1972, LastRenovation = null, RoofCondition = 56, AcCondition = 57, PlumbingCondition = 54, ElectricalCondition = 53, FloorPlanScore = 55, FutureAppreciation = 47, ResalePotential = 48, FloodRisk = 13, NoiseLevel = 38 },
            new Property { BrokeredBy = "BIG BLOCK POWERHOUSE REALTY", Status = "active", Price = 740000m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.22m, LotSqft = 2449, Street = "833 Half Moon Ave", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.164, HoaFee = 320, PropertyTax = 7400, Utilities = 285, SchoolRating = 76, CrimeScore = 54, Walkability = 58, TransitAccess = 48, AmenitiesScore = 65, CommuteMin = 25, YearBuilt = 2024, LastRenovation = null, RoofCondition = 92, AcCondition = 92, PlumbingCondition = 92, ElectricalCondition = 92, FloorPlanScore = 80, FutureAppreciation = 74, ResalePotential = 76, FloodRisk = 10, NoiseLevel = 29 },
            new Property { BrokeredBy = "MERITAGE HOMES OF CALIFORNIA", Status = "active", Price = 701400m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.08m, LotSqft = 2020, Street = "2069 Tangelo Ln", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.163, HoaFee = 300, PropertyTax = 7014, Utilities = 285, SchoolRating = 76, CrimeScore = 55, Walkability = 62, TransitAccess = 52, AmenitiesScore = 66, CommuteMin = 24, YearBuilt = 2024, LastRenovation = null, RoofCondition = 92, AcCondition = 92, PlumbingCondition = 92, ElectricalCondition = 92, FloorPlanScore = 80, FutureAppreciation = 74, ResalePotential = 76, FloodRisk = 10, NoiseLevel = 30 },
            new Property { BrokeredBy = "HOME SAVER REALTY", Status = "active", Price = 1200000m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.27m, LotSqft = 3332, Street = "259 E Crescent Ave", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.058, Longitude = -117.178, HoaFee = 0, PropertyTax = 12000, Utilities = 370, SchoolRating = 80, CrimeScore = 62, Walkability = 55, TransitAccess = 35, AmenitiesScore = 67, CommuteMin = 26, YearBuilt = 1988, LastRenovation = null, RoofCondition = 78, AcCondition = 80, PlumbingCondition = 76, ElectricalCondition = 75, FloorPlanScore = 80, FutureAppreciation = 74, ResalePotential = 76, FloodRisk = 11, NoiseLevel = 30 },
            new Property { BrokeredBy = "IRN REALTY", Status = "active", Price = 1170000m, Bedrooms = 9, Bathrooms = 7, AcreLot = 0.18m, LotSqft = 4006, Street = "610 E Lugonia Ave #4", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.059, Longitude = -117.163, HoaFee = 0, PropertyTax = 11700, Utilities = 400, SchoolRating = 68, CrimeScore = 55, Walkability = 50, TransitAccess = 32, AmenitiesScore = 56, CommuteMin = 29, YearBuilt = 1985, LastRenovation = null, RoofCondition = 68, AcCondition = 69, PlumbingCondition = 66, ElectricalCondition = 65, FloorPlanScore = 64, FutureAppreciation = 63, ResalePotential = 65, FloodRisk = 13, NoiseLevel = 35 },
            new Property { BrokeredBy = "MERITAGE HOMES OF CALIFORNIA", Status = "active", Price = 661000m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.07m, LotSqft = 1816, Street = "2040 Tangelo Ln", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.163, HoaFee = 300, PropertyTax = 6610, Utilities = 260, SchoolRating = 76, CrimeScore = 55, Walkability = 62, TransitAccess = 52, AmenitiesScore = 66, CommuteMin = 24, YearBuilt = 2024, LastRenovation = null, RoofCondition = 92, AcCondition = 92, PlumbingCondition = 92, ElectricalCondition = 92, FloorPlanScore = 78, FutureAppreciation = 72, ResalePotential = 74, FloodRisk = 10, NoiseLevel = 30 },
            new Property { BrokeredBy = "BEAZER HOMES", Status = "active", Price = 1089990m, Bedrooms = 4, Bathrooms = 4, AcreLot = 0.22m, LotSqft = 2803, Street = "1476 Moore St", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.163, HoaFee = 350, PropertyTax = 10900, Utilities = 355, SchoolRating = 78, CrimeScore = 54, Walkability = 58, TransitAccess = 48, AmenitiesScore = 66, CommuteMin = 26, YearBuilt = 2024, LastRenovation = null, RoofCondition = 94, AcCondition = 94, PlumbingCondition = 94, ElectricalCondition = 94, FloorPlanScore = 83, FutureAppreciation = 76, ResalePotential = 78, FloodRisk = 10, NoiseLevel = 28 },
            new Property { BrokeredBy = "TRI POINTE HOMES HOLDINGS INC", Status = "active", Price = 799000m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.22m, LotSqft = 2676, Street = "873 Railway Ln", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.165, HoaFee = 320, PropertyTax = 7990, Utilities = 290, SchoolRating = 76, CrimeScore = 54, Walkability = 58, TransitAccess = 48, AmenitiesScore = 65, CommuteMin = 25, YearBuilt = 2024, LastRenovation = null, RoofCondition = 92, AcCondition = 92, PlumbingCondition = 92, ElectricalCondition = 92, FloorPlanScore = 80, FutureAppreciation = 74, ResalePotential = 76, FloodRisk = 10, NoiseLevel = 29 },
            new Property { BrokeredBy = "TRI POINTE HOMES HOLDINGS INC", Status = "active", Price = 819000m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.22m, LotSqft = 2676, Street = "848 Railway Ln", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.165, HoaFee = 320, PropertyTax = 8190, Utilities = 290, SchoolRating = 76, CrimeScore = 54, Walkability = 58, TransitAccess = 48, AmenitiesScore = 65, CommuteMin = 25, YearBuilt = 2024, LastRenovation = null, RoofCondition = 92, AcCondition = 92, PlumbingCondition = 92, ElectricalCondition = 92, FloorPlanScore = 81, FutureAppreciation = 75, ResalePotential = 77, FloodRisk = 10, NoiseLevel = 29 },
            new Property { BrokeredBy = "MERITAGE HOMES OF CALIFORNIA", Status = "active", Price = 687500m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.07m, LotSqft = 1816, Street = "2063 Tangelo Ln", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.163, HoaFee = 300, PropertyTax = 6875, Utilities = 265, SchoolRating = 76, CrimeScore = 55, Walkability = 62, TransitAccess = 52, AmenitiesScore = 66, CommuteMin = 24, YearBuilt = 2024, LastRenovation = null, RoofCondition = 92, AcCondition = 92, PlumbingCondition = 92, ElectricalCondition = 92, FloorPlanScore = 79, FutureAppreciation = 73, ResalePotential = 75, FloodRisk = 10, NoiseLevel = 30 },
            new Property { BrokeredBy = "COLDWELL BANKER KIVETT-TEETERS", Status = "active", Price = 173000m, Bedrooms = 2, Bathrooms = 2, AcreLot = 0.07m, LotSqft = 1595, Street = "626 N Dearborn St Spc 56", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.063, Longitude = -117.175, HoaFee = 200, PropertyTax = 1730, Utilities = 165, SchoolRating = 58, CrimeScore = 55, Walkability = 45, TransitAccess = 25, AmenitiesScore = 50, CommuteMin = 27, YearBuilt = 1973, LastRenovation = null, RoofCondition = 56, AcCondition = 57, PlumbingCondition = 54, ElectricalCondition = 53, FloorPlanScore = 54, FutureAppreciation = 46, ResalePotential = 47, FloodRisk = 13, NoiseLevel = 38 },
        };

        foreach (var prop in propertySeeds)
        {
            if (!await _db.Properties.AnyAsync(p => p.Street == prop.Street && p.City == prop.City))
            {
                _db.Properties.Add(prop);
            }
        }

        await _db.SaveChangesAsync();

        await tx.CommitAsync();
    }
}