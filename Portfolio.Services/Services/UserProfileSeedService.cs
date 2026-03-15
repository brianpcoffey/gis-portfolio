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

        if (gulfClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientName == gulfClient.Name && o.ProductName == "4\" Round Fiberglass Duct"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientName = gulfClient.Name,
                ProductName = "4\" Round Fiberglass Duct",
                Quantity = 200,
                UnitPrice = 14.50m,
                Status = "Delivered",
                OrderDate = DateTime.Parse("2024-08-05T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2024-08-14T00:00:00Z").ToUniversalTime()
            });
        }

        if (gulfClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientName == gulfClient.Name && o.ProductName == "3000-Gallon FRP Storage Tank"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientName = gulfClient.Name,
                ProductName = "3000-Gallon FRP Storage Tank",
                Quantity = 4,
                UnitPrice = 8750.00m,
                Status = "Delivered",
                OrderDate = DateTime.Parse("2024-09-12T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2024-09-28T00:00:00Z").ToUniversalTime()
            });
        }

        if (gulfClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientName == gulfClient.Name && o.ProductName == "6\" Vinyl Ester Elbow 90°"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientName = gulfClient.Name,
                ProductName = "6\" Vinyl Ester Elbow 90°",
                Quantity = 80,
                UnitPrice = 62.00m,
                Status = "In Production",
                OrderDate = DateTime.Parse("2025-02-14T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2025-02-20T00:00:00Z").ToUniversalTime()
            });
        }

        if (loneStarClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientName == loneStarClient.Name && o.ProductName == "8\" FRP Straight Pipe (20 ft)"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientName = loneStarClient.Name,
                ProductName = "8\" FRP Straight Pipe (20 ft)",
                Quantity = 150,
                UnitPrice = 88.00m,
                Status = "Delivered",
                OrderDate = DateTime.Parse("2024-07-20T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2024-08-02T00:00:00Z").ToUniversalTime()
            });
        }

        if (loneStarClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientName == loneStarClient.Name && o.ProductName == "6\" Round Fiberglass Duct"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientName = loneStarClient.Name,
                ProductName = "6\" Round Fiberglass Duct",
                Quantity = 120,
                UnitPrice = 22.75m,
                Status = "Shipped",
                OrderDate = DateTime.Parse("2025-01-08T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2025-01-19T00:00:00Z").ToUniversalTime()
            });
        }

        if (bayouClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientName == bayouClient.Name && o.ProductName == "500-Gallon FRP Containment Basin"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientName = bayouClient.Name,
                ProductName = "500-Gallon FRP Containment Basin",
                Quantity = 10,
                UnitPrice = 3200.00m,
                Status = "Delivered",
                OrderDate = DateTime.Parse("2024-06-01T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2024-06-20T00:00:00Z").ToUniversalTime()
            });
        }

        if (bayouClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientName == bayouClient.Name && o.ProductName == "FRP Grating — Clear Span 1\" x 4\""))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientName = bayouClient.Name,
                ProductName = "FRP Grating — Clear Span 1\" x 4\"",
                Quantity = 300,
                UnitPrice = 41.00m,
                Status = "Delivered",
                OrderDate = DateTime.Parse("2024-10-05T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2024-10-18T00:00:00Z").ToUniversalTime()
            });
        }

        if (permianClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientName == permianClient.Name && o.ProductName == "4\" FRP Straight Pipe (20 ft)"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientName = permianClient.Name,
                ProductName = "4\" FRP Straight Pipe (20 ft)",
                Quantity = 400,
                UnitPrice = 54.00m,
                Status = "Delivered",
                OrderDate = DateTime.Parse("2024-05-10T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2024-05-25T00:00:00Z").ToUniversalTime()
            });
        }

        if (permianClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientName == permianClient.Name && o.ProductName == "4\" FRP Flange Set (Pair)"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientName = permianClient.Name,
                ProductName = "4\" FRP Flange Set (Pair)",
                Quantity = 200,
                UnitPrice = 38.50m,
                Status = "Pending",
                OrderDate = DateTime.Parse("2025-03-01T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2025-03-10T00:00:00Z").ToUniversalTime()
            });
        }

        if (deltaClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientName == deltaClient.Name && o.ProductName == "1000-Gallon Vinyl Ester Tank"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientName = deltaClient.Name,
                ProductName = "1000-Gallon Vinyl Ester Tank",
                Quantity = 6,
                UnitPrice = 5400.00m,
                Status = "Delivered",
                OrderDate = DateTime.Parse("2024-11-14T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2024-12-01T00:00:00Z").ToUniversalTime()
            });
        }

        if (coastalClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientName == coastalClient.Name && o.ProductName == "12\" FRP Straight Pipe (20 ft)"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientName = coastalClient.Name,
                ProductName = "12\" FRP Straight Pipe (20 ft)",
                Quantity = 60,
                UnitPrice = 165.00m,
                Status = "Shipped",
                OrderDate = DateTime.Parse("2025-01-22T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2025-02-05T00:00:00Z").ToUniversalTime()
            });
        }

        if (sunbeltClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientName == sunbeltClient.Name && o.ProductName == "FRP Scrubber Tower 24\" Dia"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientName = sunbeltClient.Name,
                ProductName = "FRP Scrubber Tower 24\" Dia",
                Quantity = 2,
                UnitPrice = 14500.00m,
                Status = "In Production",
                OrderDate = DateTime.Parse("2025-02-01T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2025-02-10T00:00:00Z").ToUniversalTime()
            });
        }

        if (triStateClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientName == triStateClient.Name && o.ProductName == "FRP Structural I-Beam 4\"") )
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientName = triStateClient.Name,
                ProductName = "FRP Structural I-Beam 4\"",
                Quantity = 100,
                UnitPrice = 92.00m,
                Status = "Delivered",
                OrderDate = DateTime.Parse("2024-12-03T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2024-12-17T00:00:00Z").ToUniversalTime()
            });
        }

        if (saguaroClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientName == saguaroClient.Name && o.ProductName == "FRP Composite Panel 4' x 8' x 1/4\""))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientName = saguaroClient.Name,
                ProductName = "FRP Composite Panel 4' x 8' x 1/4\"",
                Quantity = 250,
                UnitPrice = 78.00m,
                Status = "Delivered",
                OrderDate = DateTime.Parse("2024-09-28T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2024-10-12T00:00:00Z").ToUniversalTime()
            });
        }

        if (appalachianClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientName == appalachianClient.Name && o.ProductName == "2500-Gallon FRP Double-Wall Tank"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientName = appalachianClient.Name,
                ProductName = "2500-Gallon FRP Double-Wall Tank",
                Quantity = 3,
                UnitPrice = 11200.00m,
                Status = "Pending",
                OrderDate = DateTime.Parse("2025-03-05T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2025-03-15T00:00:00Z").ToUniversalTime()
            });
        }

        if (greatLakesClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientName == greatLakesClient.Name && o.ProductName == "10\" Round Fiberglass Duct"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientName = greatLakesClient.Name,
                ProductName = "10\" Round Fiberglass Duct",
                Quantity = 90,
                UnitPrice = 44.00m,
                Status = "Delivered",
                OrderDate = DateTime.Parse("2024-08-22T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2024-09-04T00:00:00Z").ToUniversalTime()
            });
        }

        if (pacificRimClient != null && !await _db.FiberOrders.AnyAsync(o => o.UserId == userId && o.ClientName == pacificRimClient.Name && o.ProductName == "5000-Gallon FRP Chemical Storage Tank"))
        {
            _db.FiberOrders.Add(new FiberOrder
            {
                UserId = userId,
                ClientName = pacificRimClient.Name,
                ProductName = "5000-Gallon FRP Chemical Storage Tank",
                Quantity = 2,
                UnitPrice = 17800.00m,
                Status = "Shipped",
                OrderDate = DateTime.Parse("2025-01-30T00:00:00Z").ToUniversalTime(),
                ShipDate = DateTime.Parse("2025-02-18T00:00:00Z").ToUniversalTime()
            });
        }

        await _db.SaveChangesAsync();

        // ===================================================
        // FIBER SHIPMENTS
        // ===================================================

        // Example shipment seeds (add more as needed)
        if (gulfClient != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "FX-2024-10041"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                CarrierName = "FedEx Freight",
                TrackingNumber = "FX-2024-10041",
                Status = "Delivered",
                EstimatedArrival = DateTime.Parse("2024-08-19T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 29.7604,
                DestinationLng = -95.3698,
                DestinationCity = "Houston",
                DestinationState = "TX"
            });
        }
        if (loneStarClient != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "FX-2024-10558"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                CarrierName = "FedEx Freight",
                TrackingNumber = "FX-2024-10558",
                Status = "Delivered",
                EstimatedArrival = DateTime.Parse("2024-08-08T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 30.0860,
                DestinationLng = -94.1018,
                DestinationCity = "Beaumont",
                DestinationState = "TX"
            });
        }
        if (bayouClient != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "RL-2024-44102"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                CarrierName = "R+L Carriers",
                TrackingNumber = "RL-2024-44102",
                Status = "Delivered",
                EstimatedArrival = DateTime.Parse("2024-06-27T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 30.4515,
                DestinationLng = -91.1871,
                DestinationCity = "Baton Rouge",
                DestinationState = "LA"
            });
        }
        if (bayouClient != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "RL-2024-55988"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                CarrierName = "R+L Carriers",
                TrackingNumber = "RL-2024-55988",
                Status = "Delivered",
                EstimatedArrival = DateTime.Parse("2024-10-24T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 30.4515,
                DestinationLng = -91.1871,
                DestinationCity = "Baton Rouge",
                DestinationState = "LA"
            });
        }
        if (gulfClient != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "XPO-2024-33471"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                CarrierName = "XPO Logistics",
                TrackingNumber = "XPO-2024-33471",
                Status = "Delivered",
                EstimatedArrival = DateTime.Parse("2024-06-02T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 31.9974,
                DestinationLng = -102.0779,
                DestinationCity = "Midland",
                DestinationState = "TX"
            });
        }
        if (gulfClient != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "FX-2024-19984"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                CarrierName = "FedEx Freight",
                TrackingNumber = "FX-2024-19984",
                Status = "Delivered",
                EstimatedArrival = DateTime.Parse("2024-12-08T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 32.2988,
                DestinationLng = -90.1848,
                DestinationCity = "Jackson",
                DestinationState = "MS"
            });
        }
        if (gulfClient != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "UPF-2025-00342"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                CarrierName = "UPS Freight",
                TrackingNumber = "UPF-2025-00342",
                Status = "In Transit",
                EstimatedArrival = DateTime.Parse("2025-02-12T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 30.6954,
                DestinationLng = -88.0399,
                DestinationCity = "Mobile",
                DestinationState = "AL"
            });
        }
        if (gulfClient != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "RL-2024-63310"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                CarrierName = "R+L Carriers",
                TrackingNumber = "RL-2024-63310",
                Status = "Delivered",
                EstimatedArrival = DateTime.Parse("2024-12-23T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 36.1540,
                DestinationLng = -95.9928,
                DestinationCity = "Tulsa",
                DestinationState = "OK"
            });
        }
        if (gulfClient != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "XPO-2024-44892"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                CarrierName = "XPO Logistics",
                TrackingNumber = "XPO-2024-44892",
                Status = "Delivered",
                EstimatedArrival = DateTime.Parse("2024-10-20T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 33.4484,
                DestinationLng = -112.0740,
                DestinationCity = "Phoenix",
                DestinationState = "AZ"
            });
        }
        if (gulfClient != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "UPF-2024-77541"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                CarrierName = "UPS Freight",
                TrackingNumber = "UPF-2024-77541",
                Status = "Delivered",
                EstimatedArrival = DateTime.Parse("2024-09-11T00:00:00Z").ToUniversalTime(),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 42.3314,
                DestinationLng = -83.0458,
                DestinationCity = "Detroit",
                DestinationState = "MI"
            });
        }
        if (gulfClient != null && !await _db.FiberShipments.AnyAsync(s => s.UserId == userId && s.TrackingNumber == "FX-2025-20815"))
        {
            _db.FiberShipments.Add(new FiberShipment
            {
                UserId = userId,
                CarrierName = "FedEx Freight",
                TrackingNumber = "FX-2025-20815",
                Status = "In Transit",
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
            new Property { BrokeredBy = "FIRST TEAM REAL ESTATE", Status = "active", Price = 575000m, Bedrooms = 3, Bathrooms = 2, AcreLot = 0.20m, LotSqft = 1324, Street = "1427 Laramie Ave", City = "Redlands", State = "CA",
                ZipCode = "92374",
                Latitude = 34.061,
                Longitude = -117.169,
                HoaFee = 0,
                PropertyTax = 5750,
                Utilities = 260,
                SchoolRating = 70,
                CrimeScore = 56,
                Walkability = 52,
                TransitAccess = 32,
                AmenitiesScore = 58,
                CommuteMin = 28,
                YearBuilt = 1983,
                LastRenovation = null,
                RoofCondition = 70,
                AcCondition = 72,
                PlumbingCondition = 71,
                ElectricalCondition = 70,
                FloorPlanScore = 68,
                FutureAppreciation = 63,
                ResalePotential = 65,
                FloodRisk = 13,
                NoiseLevel = 34 },
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
            new Property { BrokeredBy = "EXPERT REAL ESTATE & INVESTMENT", Status = "active", Price = 649900m, Bedrooms = 5, Bathrooms = 4, AcreLot = 0.28m, LotSqft = 3136, Street = "261 E Crescent Ave", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.058, Longitude = -117.178, HoaFee = 0, PropertyTax = 6499, Utilities = 280, SchoolRating = 78, CrimeScore = 62, Walkability = 55, TransitAccess = 35, AmenitiesScore = 65, CommuteMin = 26, YearBuilt = 1987, LastRenovation = null, RoofCondition = 74, AcCondition = 76, PlumbingCondition = 72, ElectricalCondition = 71, FloorPlanScore = 76, FutureAppreciation = 67, ResalePotential = 68, FloodRisk = 11, NoiseLevel = 30 },
            new Property { BrokeredBy = "KELLER WILLIAMS REALTY", Status = "active", Price = 1300000m, Bedrooms = 5, Bathrooms = 4, AcreLot = 0.55m, LotSqft = 4641, Street = "1641 Ford Ave", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.063, Longitude = -117.164, HoaFee = 0, PropertyTax = 13000, Utilities = 400, SchoolRating = 80, CrimeScore = 54, Walkability = 50, TransitAccess = 30, AmenitiesScore = 64, CommuteMin = 30, YearBuilt = 2000, LastRenovation = null, RoofCondition = 86, AcCondition = 87, PlumbingCondition = 84, ElectricalCondition = 83, FloorPlanScore = 86, FutureAppreciation = 78, ResalePotential = 80, FloodRisk = 11, NoiseLevel = 28 },
            new Property { BrokeredBy = "DYNASTY REAL ESTATE", Status = "active", Price = 649900m, Bedrooms = 3, Bathrooms = 2, AcreLot = 0.20m, LotSqft = 1758, Street = "1510 Karon St", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.063, Longitude = -117.167, HoaFee = 0, PropertyTax = 6499, Utilities = 260, SchoolRating = 70, CrimeScore = 56, Walkability = 50, TransitAccess = 30, AmenitiesScore = 60, CommuteMin = 28, YearBuilt = 1985, LastRenovation = null, RoofCondition = 70, AcCondition = 72, PlumbingCondition = 70, ElectricalCondition = 69, FloorPlanScore = 69, FutureAppreciation = 63, ResalePotential = 65, FloodRisk = 12, NoiseLevel = 34 },
            new Property { BrokeredBy = "EXP REALTY OF GREATER LOS ANGELES", Status = "active", Price = 679000m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.18m, LotSqft = 2232, Street = "414 Myrtle Ave", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.055, Longitude = -117.178, HoaFee = 0, PropertyTax = 6790, Utilities = 295, SchoolRating = 76, CrimeScore = 56, Walkability = 60, TransitAccess = 42, AmenitiesScore = 64, CommuteMin = 24, YearBuilt = 1992, LastRenovation = null, RoofCondition = 78, AcCondition = 79, PlumbingCondition = 76, ElectricalCondition = 75, FloorPlanScore = 76, FutureAppreciation = 67, ResalePotential = 69, FloodRisk = 10, NoiseLevel = 30 },
            new Property { BrokeredBy = "EXP REALTY OF SOUTHERN CALIFORNIA INC", Status = "active", Price = 799999m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.22m, LotSqft = 2676, Street = "839 Railway Ln", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.165, HoaFee = 320, PropertyTax = 8000, Utilities = 290, SchoolRating = 76, CrimeScore = 54, Walkability = 58, TransitAccess = 48, AmenitiesScore = 65, CommuteMin = 25, YearBuilt = 2024, LastRenovation = null, RoofCondition = 92, AcCondition = 92, PlumbingCondition = 92, ElectricalCondition = 92, FloorPlanScore = 80, FutureAppreciation = 74, ResalePotential = 76, FloodRisk = 10, NoiseLevel = 29 },
            new Property { BrokeredBy = "TRI POINTE HOMES HOLDINGS INC", Status = "active", Price = 873000m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.22m, LotSqft = 2676, Street = "859 Railway Ln", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.165, HoaFee = 320, PropertyTax = 8730, Utilities = 290, SchoolRating = 76, CrimeScore = 54, Walkability = 58, TransitAccess = 48, AmenitiesScore = 65, CommuteMin = 25, YearBuilt = 2024, LastRenovation = null, RoofCondition = 92, AcCondition = 92, PlumbingCondition = 92, ElectricalCondition = 92, FloorPlanScore = 81, FutureAppreciation = 75, ResalePotential = 77, FloodRisk = 10, NoiseLevel = 29 },
            new Property { BrokeredBy = "MERITAGE HOMES OF CALIFORNIA", Status = "active", Price = 665000m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.07m, LotSqft = 1816, Street = "2060 Tangelo Ln", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.163, HoaFee = 300, PropertyTax = 6650, Utilities = 265, SchoolRating = 76, CrimeScore = 55, Walkability = 62, TransitAccess = 52, AmenitiesScore = 66, CommuteMin = 24, YearBuilt = 2024, LastRenovation = null, RoofCondition = 92, AcCondition = 92, PlumbingCondition = 92, ElectricalCondition = 92, FloorPlanScore = 79, FutureAppreciation = 73, ResalePotential = 75, FloodRisk = 10, NoiseLevel = 30 },
            new Property { BrokeredBy = "BIG BLOCK POWERHOUSE REALTY", Status = "active", Price = 899000m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.22m, LotSqft = 2893, Street = "875 Railway Ln", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.165, HoaFee = 320, PropertyTax = 8990, Utilities = 290, SchoolRating = 76, CrimeScore = 54, Walkability = 58, TransitAccess = 48, AmenitiesScore = 65, CommuteMin = 25, YearBuilt = 2024, LastRenovation = null, RoofCondition = 92, AcCondition = 92, PlumbingCondition = 92, ElectricalCondition = 92, FloorPlanScore = 82, FutureAppreciation = 75, ResalePotential = 77, FloodRisk = 10, NoiseLevel = 29 },
            new Property { BrokeredBy = "SHAW REAL ESTATE BROKERS", Status = "active", Price = 335000m, Bedrooms = 2, Bathrooms = 2, AcreLot = 0.05m, LotSqft = 1190, Street = "14 Paseo Verde", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.063, Longitude = -117.175, HoaFee = 0, PropertyTax = 3350, Utilities = 190, SchoolRating = 62, CrimeScore = 56, Walkability = 48, TransitAccess = 28, AmenitiesScore = 52, CommuteMin = 27, YearBuilt = 1978, LastRenovation = null, RoofCondition = 56, AcCondition = 57, PlumbingCondition = 54, ElectricalCondition = 53, FloorPlanScore = 54, FutureAppreciation = 46, ResalePotential = 47, FloodRisk = 13, NoiseLevel = 38 },
            new Property { BrokeredBy = "E HOMES", Status = "active", Price = 629999m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.07m, LotSqft = 1782, Street = "37 Citrus Ct", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.061, Longitude = -117.173, HoaFee = 320, PropertyTax = 6300, Utilities = 240, SchoolRating = 70, CrimeScore = 56, Walkability = 50, TransitAccess = 30, AmenitiesScore = 58, CommuteMin = 28, YearBuilt = 1982, LastRenovation = null, RoofCondition = 70, AcCondition = 71, PlumbingCondition = 69, ElectricalCondition = 68, FloorPlanScore = 68, FutureAppreciation = 63, ResalePotential = 65, FloodRisk = 13, NoiseLevel = 35 },
            new Property { BrokeredBy = "COLDWELL BANKER REALTY", Status = "active", Price = 415000m, Bedrooms = 3, Bathrooms = 2, AcreLot = 0.05m, LotSqft = 1188, Street = "254 E Fern Ave APT 212", City = "Redlands", State = "CA", ZipCode = "92373", Latitude = 34.053, Longitude = -117.185, HoaFee = 340, PropertyTax = 4150, Utilities = 220, SchoolRating = 72, CrimeScore = 60, Walkability = 72, TransitAccess = 54, AmenitiesScore = 66, CommuteMin = 19, YearBuilt = 1995, LastRenovation = null, RoofCondition = 74, AcCondition = 75, PlumbingCondition = 72, ElectricalCondition = 72, FloorPlanScore = 71, FutureAppreciation = 61, ResalePotential = 62, FloodRisk = 9, NoiseLevel = 40 },
            new Property { BrokeredBy = "BEAZER HOMES", Status = "active", Price = 1024500m, Bedrooms = 5, Bathrooms = 4, AcreLot = 0.22m, LotSqft = 3035, Street = "1469 Moore St", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.163, HoaFee = 350, PropertyTax = 10245, Utilities = 365, SchoolRating = 78, CrimeScore = 54, Walkability = 58, TransitAccess = 48, AmenitiesScore = 66, CommuteMin = 26, YearBuilt = 2024, LastRenovation = null, RoofCondition = 94, AcCondition = 94, PlumbingCondition = 94, ElectricalCondition = 94, FloorPlanScore = 84, FutureAppreciation = 77, ResalePotential = 79, FloodRisk = 10, NoiseLevel = 28 },
            new Property { BrokeredBy = "TRI POINTE HOMES HOLDINGS INC", Status = "active", Price = 785000m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.22m, LotSqft = 2676, Street = "837 Railway Ln", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.165, HoaFee = 320, PropertyTax = 7850, Utilities = 290, SchoolRating = 76, CrimeScore = 54, Walkability = 58, TransitAccess = 48, AmenitiesScore = 65, CommuteMin = 25, YearBuilt = 2024, LastRenovation = null, RoofCondition = 92, AcCondition = 92, PlumbingCondition = 92, ElectricalCondition = 92, FloorPlanScore = 80, FutureAppreciation = 74, ResalePotential = 76, FloodRisk = 10, NoiseLevel = 29 },
            new Property { BrokeredBy = "TRI POINTE HOMES HOLDINGS INC", Status = "active", Price = 809000m, Bedrooms = 4, Bathrooms = 3, AcreLot = 0.22m, LotSqft = 2676, Street = "847 Railway Ln", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.165, HoaFee = 320, PropertyTax = 8090, Utilities = 290, SchoolRating = 76, CrimeScore = 54, Walkability = 58, TransitAccess = 48, AmenitiesScore = 65, CommuteMin = 25, YearBuilt = 2024, LastRenovation = null, RoofCondition = 92, AcCondition = 92, PlumbingCondition = 92, ElectricalCondition = 92, FloorPlanScore = 81, FutureAppreciation = 75, ResalePotential = 77, FloodRisk = 10, NoiseLevel = 29 },
            new Property { BrokeredBy = "MERITAGE HOMES OF CALIFORNIA", Status = "active", Price = 688500m, Bedrooms = 3, Bathrooms = 3, AcreLot = 0.07m, LotSqft = 1816, Street = "2072 Tangelo Ln", City = "Redlands", State = "CA", ZipCode = "92374", Latitude = 34.062, Longitude = -117.163, HoaFee = 300, PropertyTax = 6885, Utilities = 265, SchoolRating = 76, CrimeScore = 55, Walkability = 62, TransitAccess = 52, AmenitiesScore = 66, CommuteMin = 24, YearBuilt = 2024, LastRenovation = null, RoofCondition = 92, AcCondition = 92, PlumbingCondition = 92, ElectricalCondition = 92, FloorPlanScore = 79, FutureAppreciation = 73, ResalePotential = 75, FloodRisk = 10, NoiseLevel = 30 },
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