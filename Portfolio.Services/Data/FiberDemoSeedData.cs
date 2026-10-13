using Portfolio.Common.Models;

namespace Portfolio.Services.Data
{
    /// <summary>
    /// The fixed Fiber demo dataset that <c>UserProfileSeedService</c> gives every new
    /// user. Pure data: each method builds fresh, untracked entities for one user, in the
    /// order they are inserted. The seeder owns the existence checks and the saves.
    /// </summary>
    internal static class FiberDemoSeedData
    {
        private static DateTime Utc(string iso) => DateTime.Parse(iso).ToUniversalTime();

        public static IReadOnlyList<FiberClient> Clients(Guid userId) =>
        [
            new FiberClient
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
                CreatedDate = Utc("2024-01-05T00:00:00Z")
            },
            new FiberClient
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
                CreatedDate = Utc("2024-01-12T00:00:00Z")
            },
            new FiberClient
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
                CreatedDate = Utc("2024-02-03T00:00:00Z")
            },
            new FiberClient
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
                CreatedDate = Utc("2024-02-18T00:00:00Z")
            },
            new FiberClient
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
                CreatedDate = Utc("2024-03-07T00:00:00Z")
            },
            new FiberClient
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
                CreatedDate = Utc("2024-03-22T00:00:00Z")
            },
            new FiberClient
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
                CreatedDate = Utc("2024-04-10T00:00:00Z")
            },
            new FiberClient
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
                CreatedDate = Utc("2024-04-29T00:00:00Z")
            },
            new FiberClient
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
                CreatedDate = Utc("2024-05-14T00:00:00Z")
            },
            new FiberClient
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
                CreatedDate = Utc("2024-06-02T00:00:00Z")
            },
            new FiberClient
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
                CreatedDate = Utc("2024-06-20T00:00:00Z")
            },
            new FiberClient
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
                CreatedDate = Utc("2024-07-08T00:00:00Z")
            }
        ];

        public static IReadOnlyList<FiberMaterial> Materials(Guid userId) =>
        [
            new FiberMaterial
            {
                UserId = userId,
                Name = "Fiberglass Woven Roving",
                Sku = "RM-FGW",
                UnitOfMeasure = "rolls",
                Category = "Reinforcements",
                QtyOnHand = 85,
                ReorderPoint = 20,
                ReorderQty = 40,
                UnitCost = 42.00m,
                Supplier = "TexMat Co",
                WarehouseLocation = "A1",
                LastUpdated = Utc("2025-01-10T00:00:00Z")
            },
            new FiberMaterial
            {
                UserId = userId,
                Name = "Chopped Strand Mat 1.5 oz",
                Sku = "RM-CSM",
                UnitOfMeasure = "rolls",
                Category = "Reinforcements",
                QtyOnHand = 120,
                ReorderPoint = 30,
                ReorderQty = 60,
                UnitCost = 28.50m,
                Supplier = "TexMat Co",
                WarehouseLocation = "A2",
                LastUpdated = Utc("2025-01-10T00:00:00Z")
            },
            new FiberMaterial
            {
                UserId = userId,
                Name = "Vinyl Ester Resin",
                Sku = "RM-VE",
                UnitOfMeasure = "drums",
                Category = "Resins",
                QtyOnHand = 18,
                ReorderPoint = 5,
                ReorderQty = 10,
                UnitCost = 415.00m,
                Supplier = "ChemSource Direct",
                WarehouseLocation = "B1",
                LastUpdated = Utc("2025-01-14T00:00:00Z")
            },
            new FiberMaterial
            {
                UserId = userId,
                Name = "Orthophthalic Polyester Resin",
                Sku = "RM-OR",
                UnitOfMeasure = "drums",
                Category = "Resins",
                QtyOnHand = 24,
                ReorderPoint = 6,
                ReorderQty = 12,
                UnitCost = 295.00m,
                Supplier = "ChemSource Direct",
                WarehouseLocation = "B2",
                LastUpdated = Utc("2025-01-14T00:00:00Z")
            },
            new FiberMaterial
            {
                UserId = userId,
                Name = "Isophthalic Corrosion Liner Resin",
                Sku = "RM-CR",
                UnitOfMeasure = "drums",
                Category = "Resins",
                QtyOnHand = 9,
                ReorderPoint = 4,
                ReorderQty = 8,
                UnitCost = 510.00m,
                Supplier = "ChemSource Direct",
                WarehouseLocation = "B3",
                LastUpdated = Utc("2025-01-18T00:00:00Z")
            },
            new FiberMaterial
            {
                UserId = userId,
                Name = "Fiberglass Veil (C-Glass)",
                Sku = "RM-GV",
                UnitOfMeasure = "rolls",
                Category = "Reinforcements",
                QtyOnHand = 44,
                ReorderPoint = 10,
                ReorderQty = 20,
                UnitCost = 58.75m,
                Supplier = "Gulf Fiber Supply",
                WarehouseLocation = "A3",
                LastUpdated = Utc("2025-01-20T00:00:00Z")
            },
            new FiberMaterial
            {
                UserId = userId,
                Name = "MEKP Catalyst",
                Sku = "RM-MK",
                UnitOfMeasure = "gallons",
                Category = "Catalysts",
                QtyOnHand = 32,
                ReorderPoint = 8,
                ReorderQty = 16,
                UnitCost = 22.00m,
                Supplier = "ChemSource Direct",
                WarehouseLocation = "C1",
                LastUpdated = Utc("2025-01-22T00:00:00Z")
            },
            new FiberMaterial
            {
                UserId = userId,
                Name = "Carbon Fiber Tow 12K",
                Sku = "RM-CF",
                UnitOfMeasure = "spools",
                Category = "Reinforcements",
                QtyOnHand = 15,
                ReorderPoint = 4,
                ReorderQty = 8,
                UnitCost = 185.00m,
                Supplier = "Apex Composites LLC",
                WarehouseLocation = "A4",
                LastUpdated = Utc("2025-02-01T00:00:00Z")
            },
            new FiberMaterial
            {
                UserId = userId,
                Name = "Foam Core (Divinycell H80)",
                Sku = "RM-FC",
                UnitOfMeasure = "sheets",
                Category = "Cores",
                QtyOnHand = 60,
                ReorderPoint = 15,
                ReorderQty = 30,
                UnitCost = 74.25m,
                Supplier = "Gulf Fiber Supply",
                WarehouseLocation = "D1",
                LastUpdated = Utc("2025-02-05T00:00:00Z")
            },
            new FiberMaterial
            {
                UserId = userId,
                Name = "Gel Coat (White ISO)",
                Sku = "RM-GC",
                UnitOfMeasure = "gallons",
                Category = "Gel Coats",
                QtyOnHand = 55,
                ReorderPoint = 12,
                ReorderQty = 24,
                UnitCost = 38.50m,
                Supplier = "TexMat Co",
                WarehouseLocation = "C2",
                LastUpdated = Utc("2025-02-08T00:00:00Z")
            },
            new FiberMaterial
            {
                UserId = userId,
                Name = "Acetone Solvent (55-gal)",
                Sku = "RM-AJ",
                UnitOfMeasure = "drums",
                Category = "Solvents",
                QtyOnHand = 7,
                ReorderPoint = 2,
                ReorderQty = 4,
                UnitCost = 195.00m,
                Supplier = "ChemSource Direct",
                WarehouseLocation = "C3",
                LastUpdated = Utc("2025-02-10T00:00:00Z")
            },
            new FiberMaterial
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
                LastUpdated = Utc("2025-02-12T00:00:00Z")
            }
        ];

        /// <summary>Each order is seeded only if the client named by its <c>ClientName</c> exists.</summary>
        public static IReadOnlyList<FiberOrder> Orders(Guid userId) =>
        [
            new FiberOrder
            {
                UserId = userId,
                ClientName = "Gulf Coast Chemical",
                ProductName = "4\" Round Fiberglass Duct",
                Quantity = 200,
                UnitPrice = 14.50m,
                Status = "Delivered",
                OrderDate = Utc("2024-08-05T00:00:00Z"),
                ShipDate = Utc("2024-08-14T00:00:00Z")
            },
            new FiberOrder
            {
                UserId = userId,
                ClientName = "Gulf Coast Chemical",
                ProductName = "3000-Gallon FRP Storage Tank",
                Quantity = 4,
                UnitPrice = 8750.00m,
                Status = "Delivered",
                OrderDate = Utc("2024-09-12T00:00:00Z"),
                ShipDate = Utc("2024-09-28T00:00:00Z")
            },
            new FiberOrder
            {
                UserId = userId,
                ClientName = "Gulf Coast Chemical",
                ProductName = "6\" Vinyl Ester Elbow 90°",
                Quantity = 80,
                UnitPrice = 62.00m,
                Status = "In Production",
                OrderDate = Utc("2025-02-14T00:00:00Z"),
                ShipDate = Utc("2025-02-20T00:00:00Z")
            },
            new FiberOrder
            {
                UserId = userId,
                ClientName = "Lone Star Refining",
                ProductName = "8\" FRP Straight Pipe (20 ft)",
                Quantity = 150,
                UnitPrice = 88.00m,
                Status = "Delivered",
                OrderDate = Utc("2024-07-20T00:00:00Z"),
                ShipDate = Utc("2024-08-02T00:00:00Z")
            },
            new FiberOrder
            {
                UserId = userId,
                ClientName = "Lone Star Refining",
                ProductName = "6\" Round Fiberglass Duct",
                Quantity = 120,
                UnitPrice = 22.75m,
                Status = "Shipped",
                OrderDate = Utc("2025-01-08T00:00:00Z"),
                ShipDate = Utc("2025-01-19T00:00:00Z")
            },
            new FiberOrder
            {
                UserId = userId,
                ClientName = "Bayou Industrial Group",
                ProductName = "500-Gallon FRP Containment Basin",
                Quantity = 10,
                UnitPrice = 3200.00m,
                Status = "Delivered",
                OrderDate = Utc("2024-06-01T00:00:00Z"),
                ShipDate = Utc("2024-06-20T00:00:00Z")
            },
            new FiberOrder
            {
                UserId = userId,
                ClientName = "Bayou Industrial Group",
                ProductName = "FRP Grating — Clear Span 1\" x 4\"",
                Quantity = 300,
                UnitPrice = 41.00m,
                Status = "Delivered",
                OrderDate = Utc("2024-10-05T00:00:00Z"),
                ShipDate = Utc("2024-10-18T00:00:00Z")
            },
            new FiberOrder
            {
                UserId = userId,
                ClientName = "Permian Basin Composites",
                ProductName = "4\" FRP Straight Pipe (20 ft)",
                Quantity = 400,
                UnitPrice = 54.00m,
                Status = "Delivered",
                OrderDate = Utc("2024-05-10T00:00:00Z"),
                ShipDate = Utc("2024-05-25T00:00:00Z")
            },
            new FiberOrder
            {
                UserId = userId,
                ClientName = "Permian Basin Composites",
                ProductName = "4\" FRP Flange Set (Pair)",
                Quantity = 200,
                UnitPrice = 38.50m,
                Status = "Pending",
                OrderDate = Utc("2025-03-01T00:00:00Z"),
                ShipDate = Utc("2025-03-10T00:00:00Z")
            },
            new FiberOrder
            {
                UserId = userId,
                ClientName = "Delta Tank & Vessel",
                ProductName = "1000-Gallon Vinyl Ester Tank",
                Quantity = 6,
                UnitPrice = 5400.00m,
                Status = "Delivered",
                OrderDate = Utc("2024-11-14T00:00:00Z"),
                ShipDate = Utc("2024-12-01T00:00:00Z")
            },
            new FiberOrder
            {
                UserId = userId,
                ClientName = "Coastal Pipe & Fittings",
                ProductName = "12\" FRP Straight Pipe (20 ft)",
                Quantity = 60,
                UnitPrice = 165.00m,
                Status = "Shipped",
                OrderDate = Utc("2025-01-22T00:00:00Z"),
                ShipDate = Utc("2025-02-05T00:00:00Z")
            },
            new FiberOrder
            {
                UserId = userId,
                ClientName = "Sunbelt Process Equipment",
                ProductName = "FRP Scrubber Tower 24\" Dia",
                Quantity = 2,
                UnitPrice = 14500.00m,
                Status = "In Production",
                OrderDate = Utc("2025-02-01T00:00:00Z"),
                ShipDate = Utc("2025-02-10T00:00:00Z")
            },
            new FiberOrder
            {
                UserId = userId,
                ClientName = "Tri-State Corrosion Solutions",
                ProductName = "FRP Structural I-Beam 4\"",
                Quantity = 100,
                UnitPrice = 92.00m,
                Status = "Delivered",
                OrderDate = Utc("2024-12-03T00:00:00Z"),
                ShipDate = Utc("2024-12-17T00:00:00Z")
            },
            new FiberOrder
            {
                UserId = userId,
                ClientName = "Saguaro Structural Composites",
                ProductName = "FRP Composite Panel 4' x 8' x 1/4\"",
                Quantity = 250,
                UnitPrice = 78.00m,
                Status = "Delivered",
                OrderDate = Utc("2024-09-28T00:00:00Z"),
                ShipDate = Utc("2024-10-12T00:00:00Z")
            },
            new FiberOrder
            {
                UserId = userId,
                ClientName = "Appalachian Containment Systems",
                ProductName = "2500-Gallon FRP Double-Wall Tank",
                Quantity = 3,
                UnitPrice = 11200.00m,
                Status = "Pending",
                OrderDate = Utc("2025-03-05T00:00:00Z"),
                ShipDate = Utc("2025-03-15T00:00:00Z")
            },
            new FiberOrder
            {
                UserId = userId,
                ClientName = "Great Lakes Fiberglass Works",
                ProductName = "10\" Round Fiberglass Duct",
                Quantity = 90,
                UnitPrice = 44.00m,
                Status = "Delivered",
                OrderDate = Utc("2024-08-22T00:00:00Z"),
                ShipDate = Utc("2024-09-04T00:00:00Z")
            },
            new FiberOrder
            {
                UserId = userId,
                ClientName = "Pacific Rim Tank Systems",
                ProductName = "5000-Gallon FRP Chemical Storage Tank",
                Quantity = 2,
                UnitPrice = 17800.00m,
                Status = "Shipped",
                OrderDate = Utc("2025-01-30T00:00:00Z"),
                ShipDate = Utc("2025-02-18T00:00:00Z")
            }
        ];

        /// <summary>Each shipment is paired with the client that must exist for it to be seeded.</summary>
        public static IReadOnlyList<(string ClientName, FiberShipment Shipment)> Shipments(Guid userId) =>
        [
            ("Gulf Coast Chemical", new FiberShipment
            {
                UserId = userId,
                CarrierName = "FedEx Freight",
                TrackingNumber = "FX-2024-10041",
                Status = "Delivered",
                EstimatedArrival = Utc("2024-08-19T00:00:00Z"),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 29.7604,
                DestinationLng = -95.3698,
                DestinationCity = "Houston",
                DestinationState = "TX"
            }),
            ("Lone Star Refining", new FiberShipment
            {
                UserId = userId,
                CarrierName = "FedEx Freight",
                TrackingNumber = "FX-2024-10558",
                Status = "Delivered",
                EstimatedArrival = Utc("2024-08-08T00:00:00Z"),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 30.0860,
                DestinationLng = -94.1018,
                DestinationCity = "Beaumont",
                DestinationState = "TX"
            }),
            ("Bayou Industrial Group", new FiberShipment
            {
                UserId = userId,
                CarrierName = "R+L Carriers",
                TrackingNumber = "RL-2024-44102",
                Status = "Delivered",
                EstimatedArrival = Utc("2024-06-27T00:00:00Z"),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 30.4515,
                DestinationLng = -91.1871,
                DestinationCity = "Baton Rouge",
                DestinationState = "LA"
            }),
            ("Bayou Industrial Group", new FiberShipment
            {
                UserId = userId,
                CarrierName = "R+L Carriers",
                TrackingNumber = "RL-2024-55988",
                Status = "Delivered",
                EstimatedArrival = Utc("2024-10-24T00:00:00Z"),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 30.4515,
                DestinationLng = -91.1871,
                DestinationCity = "Baton Rouge",
                DestinationState = "LA"
            }),
            ("Gulf Coast Chemical", new FiberShipment
            {
                UserId = userId,
                CarrierName = "XPO Logistics",
                TrackingNumber = "XPO-2024-33471",
                Status = "Delivered",
                EstimatedArrival = Utc("2024-06-02T00:00:00Z"),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 31.9974,
                DestinationLng = -102.0779,
                DestinationCity = "Midland",
                DestinationState = "TX"
            }),
            ("Gulf Coast Chemical", new FiberShipment
            {
                UserId = userId,
                CarrierName = "FedEx Freight",
                TrackingNumber = "FX-2024-19984",
                Status = "Delivered",
                EstimatedArrival = Utc("2024-12-08T00:00:00Z"),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 32.2988,
                DestinationLng = -90.1848,
                DestinationCity = "Jackson",
                DestinationState = "MS"
            }),
            ("Gulf Coast Chemical", new FiberShipment
            {
                UserId = userId,
                CarrierName = "UPS Freight",
                TrackingNumber = "UPF-2025-00342",
                Status = "In Transit",
                EstimatedArrival = Utc("2025-02-12T00:00:00Z"),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 30.6954,
                DestinationLng = -88.0399,
                DestinationCity = "Mobile",
                DestinationState = "AL"
            }),
            ("Gulf Coast Chemical", new FiberShipment
            {
                UserId = userId,
                CarrierName = "R+L Carriers",
                TrackingNumber = "RL-2024-63310",
                Status = "Delivered",
                EstimatedArrival = Utc("2024-12-23T00:00:00Z"),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 36.1540,
                DestinationLng = -95.9928,
                DestinationCity = "Tulsa",
                DestinationState = "OK"
            }),
            ("Gulf Coast Chemical", new FiberShipment
            {
                UserId = userId,
                CarrierName = "XPO Logistics",
                TrackingNumber = "XPO-2024-44892",
                Status = "Delivered",
                EstimatedArrival = Utc("2024-10-20T00:00:00Z"),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 33.4484,
                DestinationLng = -112.0740,
                DestinationCity = "Phoenix",
                DestinationState = "AZ"
            }),
            ("Gulf Coast Chemical", new FiberShipment
            {
                UserId = userId,
                CarrierName = "UPS Freight",
                TrackingNumber = "UPF-2024-77541",
                Status = "Delivered",
                EstimatedArrival = Utc("2024-09-11T00:00:00Z"),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 42.3314,
                DestinationLng = -83.0458,
                DestinationCity = "Detroit",
                DestinationState = "MI"
            }),
            ("Gulf Coast Chemical", new FiberShipment
            {
                UserId = userId,
                CarrierName = "FedEx Freight",
                TrackingNumber = "FX-2025-20815",
                Status = "In Transit",
                EstimatedArrival = Utc("2025-02-27T00:00:00Z"),
                OriginLat = 29.7604,
                OriginLng = -95.3698,
                DestinationLat = 45.5051,
                DestinationLng = -122.6750,
                DestinationCity = "Portland",
                DestinationState = "OR"
            })
        ];

        /// <summary>
        /// Each transaction is paired with the SKU of the material it moves; the seeder fills
        /// in <c>MaterialId</c> from that material's stored row.
        /// </summary>
        public static IReadOnlyList<(string Sku, FiberInventoryTransaction Transaction)> InventoryTransactions(Guid userId) =>
        [
            ("RM-FGW", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Receive",
                Quantity = 100,
                QtyBeforeTransaction = 0,
                QtyAfterTransaction = 100,
                Notes = "Initial stock receipt — TexMat Q3 order",
                TransactionDate = Utc("2024-08-01T00:00:00Z")
            }),
            ("RM-FGW", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Consume",
                Quantity = -30,
                QtyBeforeTransaction = 100,
                QtyAfterTransaction = 70,
                Notes = "Consumed for Gulf Coast FD-4R production run",
                TransactionDate = Utc("2024-08-10T00:00:00Z")
            }),
            ("RM-FGW", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Receive",
                Quantity = 40,
                QtyBeforeTransaction = 70,
                QtyAfterTransaction = 110,
                Notes = "TexMat Q4 restock received — Warehouse A1",
                TransactionDate = Utc("2024-11-05T00:00:00Z")
            }),
            ("RM-FGW", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Consume",
                Quantity = -25,
                QtyBeforeTransaction = 110,
                QtyAfterTransaction = 85,
                Notes = "Consumed for Lone Star FP-8S production run",
                TransactionDate = Utc("2024-11-20T00:00:00Z")
            }),
            ("RM-CSM", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Receive",
                Quantity = 150,
                QtyBeforeTransaction = 0,
                QtyAfterTransaction = 150,
                Notes = "Initial CSM stock — TexMat Q3 shipment",
                TransactionDate = Utc("2024-08-01T00:00:00Z")
            }),
            ("RM-CSM", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Consume",
                Quantity = -30,
                QtyBeforeTransaction = 150,
                QtyAfterTransaction = 120,
                Notes = "Consumed — Bayou FT-500 containment basin build",
                TransactionDate = Utc("2024-08-28T00:00:00Z")
            }),
            ("RM-VE", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Receive",
                Quantity = 20,
                QtyBeforeTransaction = 0,
                QtyAfterTransaction = 20,
                Notes = "ChemSource initial vinyl ester delivery",
                TransactionDate = Utc("2024-09-01T00:00:00Z")
            }),
            ("RM-VE", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Consume",
                Quantity = -5,
                QtyBeforeTransaction = 20,
                QtyAfterTransaction = 15,
                Notes = "Consumed — Delta Tank FT-1000VE production",
                TransactionDate = Utc("2024-11-25T00:00:00Z")
            }),
            ("RM-VE", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Adjustment",
                Quantity = 3,
                QtyBeforeTransaction = 15,
                QtyAfterTransaction = 18,
                Notes = "Qty adjustment after Q4 physical inventory count",
                TransactionDate = Utc("2024-12-31T00:00:00Z")
            }),
            ("RM-OR", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Receive",
                Quantity = 30,
                QtyBeforeTransaction = 0,
                QtyAfterTransaction = 30,
                Notes = "ChemSource initial ortho resin delivery",
                TransactionDate = Utc("2024-09-01T00:00:00Z")
            }),
            ("RM-OR", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Consume",
                Quantity = -6,
                QtyBeforeTransaction = 30,
                QtyAfterTransaction = 24,
                Notes = "Consumed — Saguaro composite panel production batch",
                TransactionDate = Utc("2024-10-08T00:00:00Z")
            }),
            ("RM-CR", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Receive",
                Quantity = 10,
                QtyBeforeTransaction = 0,
                QtyAfterTransaction = 10,
                Notes = "Initial corrosion liner resin — ChemSource",
                TransactionDate = Utc("2024-10-15T00:00:00Z")
            }),
            ("RM-CR", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Consume",
                Quantity = -1,
                QtyBeforeTransaction = 10,
                QtyAfterTransaction = 9,
                Notes = "Consumed — Gulf Coast FT-3000 tank liner coats",
                TransactionDate = Utc("2024-11-02T00:00:00Z")
            }),
            ("RM-GV", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Receive",
                Quantity = 50,
                QtyBeforeTransaction = 0,
                QtyAfterTransaction = 50,
                Notes = "Gulf Fiber Supply initial veil delivery",
                TransactionDate = Utc("2024-09-10T00:00:00Z")
            }),
            ("RM-GV", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Consume",
                Quantity = -6,
                QtyBeforeTransaction = 50,
                QtyAfterTransaction = 44,
                Notes = "Consumed — Permian Basin FP-4S inner liner lamination",
                TransactionDate = Utc("2024-10-30T00:00:00Z")
            }),
            ("RM-MK", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Receive",
                Quantity = 40,
                QtyBeforeTransaction = 0,
                QtyAfterTransaction = 40,
                Notes = "MEKP catalyst — initial ChemSource order",
                TransactionDate = Utc("2024-09-01T00:00:00Z")
            }),
            ("RM-MK", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Consume",
                Quantity = -8,
                QtyBeforeTransaction = 40,
                QtyAfterTransaction = 32,
                Notes = "Consumed — routine production Q4 cure cycles",
                TransactionDate = Utc("2024-12-15T00:00:00Z")
            }),
            ("RM-CF", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Receive",
                Quantity = 20,
                QtyBeforeTransaction = 0,
                QtyAfterTransaction = 20,
                Notes = "Carbon fiber tow — Apex Composites pilot order",
                TransactionDate = Utc("2024-12-01T00:00:00Z")
            }),
            ("RM-CF", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Consume",
                Quantity = -5,
                QtyBeforeTransaction = 20,
                QtyAfterTransaction = 15,
                Notes = "Consumed — Tri-State structural beam prototype layup",
                TransactionDate = Utc("2024-12-20T00:00:00Z")
            }),
            ("RM-FC", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Receive",
                Quantity = 80,
                QtyBeforeTransaction = 0,
                QtyAfterTransaction = 80,
                Notes = "Foam core initial stock — Gulf Fiber Supply",
                TransactionDate = Utc("2024-10-01T00:00:00Z")
            }),
            ("RM-FC", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Consume",
                Quantity = -20,
                QtyBeforeTransaction = 80,
                QtyAfterTransaction = 60,
                Notes = "Consumed — Saguaro sandwich panel core material",
                TransactionDate = Utc("2024-10-09T00:00:00Z")
            }),
            ("RM-GC", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Receive",
                Quantity = 60,
                QtyBeforeTransaction = 0,
                QtyAfterTransaction = 60,
                Notes = "White ISO gel coat — TexMat first shipment",
                TransactionDate = Utc("2024-09-15T00:00:00Z")
            }),
            ("RM-GC", new FiberInventoryTransaction
            {
                UserId = userId,
                TransactionType = "Consume",
                Quantity = -5,
                QtyBeforeTransaction = 60,
                QtyAfterTransaction = 55,
                Notes = "Consumed — Gulf Coast tank exterior finish coats",
                TransactionDate = Utc("2024-10-02T00:00:00Z")
            })
        ];
    }
}
