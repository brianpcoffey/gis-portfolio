using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portfolio.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class InitialSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // -------------------------------------------------------
            // Seeds FiberClients, FiberMaterials, FiberOrders,
            // FiberShipments, FiberInventoryTransactions, and Properties
            // for every row currently in UserProfiles.
            //
            // FK resolution strategy:
            //   Child tables (Orders, Shipments, Transactions) resolve
            //   their parent IDs via correlated subqueries keyed on
            //   (user_id + a stable natural key such as name or sku),
            //   so IDs never collide between users regardless of how
            //   many users exist or what IDs the DB assigns.
            //
            // To seed a newly registered user at runtime, call the same
            // SQL from your user-registration service, substituting the
            // new UserId for the CROSS JOIN source.
            // -------------------------------------------------------

            // ===================================================
            // FIBER CLIENTS
            // ===================================================
            migrationBuilder.Sql(@"
INSERT INTO FiberClients
    (user_id, name, contact_name, email, phone, city, state, latitude, longitude, created_date)
SELECT u.UserId, s.name, s.contact_name, s.email, s.phone, s.city, s.state, s.latitude, s.longitude, s.created_date
FROM UserProfiles u
CROSS JOIN (VALUES
    ('Gulf Coast Chemical',   'Linda Martinez',   'linda@gulfchem.com',          '713-555-0101', 'Houston',       'TX', 29.7604, -95.3698, '2024-01-05T00:00:00Z'),
    ('Lone Star Refining',    'James Carter',     'jcarter@lonestarref.com',     '409-555-0112', 'Beaumont',      'TX', 30.0860, -94.1018, '2024-01-12T00:00:00Z'),
    ('Delta Processing Co',   'Sarah Lee',        'slee@deltaproc.com',          '225-555-0123', 'Baton Rouge',   'LA', 30.4515, -91.1871, '2024-02-03T00:00:00Z'),
    ('Bayou Industrial',      'Mike Johnson',     'mjohnson@bayouind.com',       '504-555-0134', 'New Orleans',   'LA', 29.9511, -90.0715, '2024-02-18T00:00:00Z'),
    ('Sooner Plant Services', 'Emily White',      'ewhite@soonerplant.com',      '405-555-0145', 'Oklahoma City', 'OK', 35.4676, -97.5164, '2024-03-07T00:00:00Z'),
    ('Arkansas Fabricators',  'Robert King',      'rking@arkfab.com',            '501-555-0156', 'Little Rock',   'AR', 34.7465, -92.2896, '2024-03-22T00:00:00Z'),
    ('Magnolia Chemical',     'Patricia Green',   'pgreen@magnoliachem.com',     '601-555-0167', 'Jackson',       'MS', 32.2988, -90.1848, '2024-04-09T00:00:00Z'),
    ('Steel City Industries', 'William Brown',    'wbrown@steelcity.com',        '205-555-0178', 'Birmingham',    'AL', 33.5186, -86.8104, '2024-04-25T00:00:00Z'),
    ('Cumberland Manufacturing','Jessica Adams',  'jadams@cumberlandmfg.com',    '615-555-0189', 'Nashville',     'TN', 36.1627, -86.7816, '2024-05-14T00:00:00Z'),
    ('Peach State Processing','David Wilson',     'dwilson@peachstate.com',      '404-555-0190', 'Atlanta',       'GA', 33.7490, -84.3880, '2024-06-02T00:00:00Z')
) AS s(name, contact_name, email, phone, city, state, latitude, longitude, created_date)
WHERE NOT EXISTS (
    SELECT 1 FROM FiberClients x WHERE x.user_id = u.UserId AND x.name = s.name
);
");

            // ===================================================
            // FIBER MATERIALS
            // ===================================================
            migrationBuilder.Sql(@"
INSERT INTO FiberMaterials
    (user_id, name, sku, unit_of_measure, qty_on_hand, reorder_point, reorder_qty, unit_cost, supplier, warehouse_location, last_updated)
SELECT u.UserId, s.name, s.sku, s.uom, s.qty_on_hand, s.reorder_point, s.reorder_qty, s.unit_cost, s.supplier, s.wh_loc, '2025-01-10T00:00:00Z'
FROM UserProfiles u
CROSS JOIN (VALUES
    ('Fiberglass Woven Roving', 'RM-FGW',  'rolls',   85,  20,  40,  42.00, 'TexMat Co',    'A1'),
    ('Polyester Resin',         'RM-PR',   'gal',     12,  30,  60,  18.50, 'ResinWorks',   'B2'),
    ('Hardener MEKP',           'RM-MEK',  'gal',      8,  15,  30,  24.75, 'ChemCore',     'B2'),
    ('Release Wax',             'RM-RW',   'lbs',     45,  10,  20,   8.25, 'ReleasePro',   'C3'),
    ('Fiberglass Mat 1.5oz',    'RM-FGM',  'rolls',   60,  25,  40,  31.00, 'TexMat Co',    'A2'),
    ('Pigment - Gray',          'RM-PGR',  'lbs',     22,  10,  20,  12.50, 'PigmentX',     'D1'),
    ('Pigment - White',         'RM-PWH',  'lbs',      6,  10,  20,  12.50, 'PigmentX',     'D1'),
    ('PVC Core Sheet',          'RM-PVC',  'sheets', 140,  50, 100,   5.80, 'CorePlastics', 'E1'),
    ('Flanges 4in',             'RM-FL4',  'units',  320, 100, 200,   2.15, 'DuctParts',    'F1'),
    ('Flanges 6in',             'RM-FL6',  'units',   88, 100, 200,   3.40, 'DuctParts',    'F1'),
    ('Sandpaper 80-grit',       'RM-SP80', 'sheets', 500, 200, 400,   0.35, 'AbrasiveCo',   'G1'),
    ('Acetone Solvent',         'RM-ACE',  'gal',     30,  10,  20,  14.00, 'Solvex',       'H1'),
    ('Epoxy Resin',             'RM-EPX',  'gal',     25,  15,  30,  27.00, 'ResinWorks',   'B3'),
    ('Glass Microspheres',      'RM-GMS',  'lbs',     18,   8,  16,  19.00, 'MicroFill',    'C2'),
    ('Chopped Strand Mat',      'RM-CSM',  'rolls',   33,  12,  24,  29.00, 'TexMat Co',    'A3')
) AS s(name, sku, uom, qty_on_hand, reorder_point, reorder_qty, unit_cost, supplier, wh_loc)
WHERE NOT EXISTS (
    SELECT 1 FROM FiberMaterials x WHERE x.user_id = u.UserId AND x.sku = s.sku
);
");

            // ===================================================
            // FIBER ORDERS
            // Client FKs resolved by (user_id + client name).
            // A stable integer seed_seq (1–30) is carried through so
            // child tables can reference orders by (user_id + seed_seq).
            // ===================================================
            migrationBuilder.Sql(@"
INSERT INTO FiberOrders
    (user_id, client_id, product_name, product_sku, quantity, unit_price, total_value, status, order_date, ship_date, seed_seq)
SELECT
    u.UserId,
    (SELECT id FROM FiberClients c WHERE c.user_id = u.UserId AND c.name = s.client_name),
    s.product_name, s.product_sku, s.quantity, s.unit_price, s.total_value,
    s.status, s.order_date, s.ship_date, s.seed_seq
FROM UserProfiles u
CROSS JOIN (VALUES
    -- Delivered (7)
    (1,  'Gulf Coast Chemical',    '4"" Round Fiberglass Duct',        'FD-4R',      200, 14.50,  2900.00, 'Delivered',     '2024-08-05T00:00:00Z', '2024-08-14T00:00:00Z'),
    (2,  'Delta Processing Co',    '6"" Round Fiberglass Duct',        'FD-6R',      150, 22.00,  3300.00, 'Delivered',     '2024-08-18T00:00:00Z', '2024-08-27T00:00:00Z'),
    (3,  'Magnolia Chemical',      'Chemical-Resistant 90 Elbow 4""',  'EL-90-4CR',   75, 67.00,  5025.00, 'Delivered',     '2024-09-02T00:00:00Z', '2024-09-12T00:00:00Z'),
    (4,  'Cumberland Manufacturing','12"" Duct Section',               'FD-12S',      50,118.00,  5900.00, 'Delivered',     '2024-09-20T00:00:00Z', '2024-09-30T00:00:00Z'),
    (5,  'Lone Star Refining',     'High-Temp Flanged Fitting 6""',    'FF-6HT',      40, 95.50,  3820.00, 'Delivered',     '2024-10-08T00:00:00Z', '2024-10-18T00:00:00Z'),
    (6,  'Sooner Plant Services',  '8"" Rectangular Duct',             'FD-8X',      120, 38.75,  4650.00, 'Delivered',     '2024-10-25T00:00:00Z', '2024-11-04T00:00:00Z'),
    (7,  'Peach State Processing', 'Custom Exhaust Hood',              'EH-CUST',     10,285.00,  2850.00, 'Delivered',     '2024-11-05T00:00:00Z', '2024-11-16T00:00:00Z'),
    -- Shipped (8)
    (8,  'Bayou Industrial',       '4"" Round Fiberglass Duct',        'FD-4R',      500, 14.50,  7250.00, 'Shipped',       '2024-11-12T00:00:00Z', '2024-11-22T00:00:00Z'),
    (9,  'Arkansas Fabricators',   '6"" Round Fiberglass Duct',        'FD-6R',      200, 22.00,  4400.00, 'Shipped',       '2024-11-19T00:00:00Z', '2024-11-29T00:00:00Z'),
    (10, 'Steel City Industries',  'Chemical-Resistant 90 Elbow 4""',  'EL-90-4CR',  100, 67.00,  6700.00, 'Shipped',       '2024-11-28T00:00:00Z', '2024-12-08T00:00:00Z'),
    (11, 'Gulf Coast Chemical',    'High-Temp Flanged Fitting 6""',    'FF-6HT',      60, 95.50,  5730.00, 'Shipped',       '2024-12-03T00:00:00Z', '2024-12-13T00:00:00Z'),
    (12, 'Delta Processing Co',    '12"" Duct Section',                'FD-12S',      80,118.00,  9440.00, 'Shipped',       '2024-12-10T00:00:00Z', '2024-12-20T00:00:00Z'),
    (13, 'Magnolia Chemical',      '8"" Rectangular Duct',             'FD-8X',      175, 38.75,  6781.25, 'Shipped',       '2024-12-17T00:00:00Z', '2024-12-27T00:00:00Z'),
    (14, 'Cumberland Manufacturing','4"" Round Fiberglass Duct',       'FD-4R',      300, 14.50,  4350.00, 'Shipped',       '2024-12-22T00:00:00Z', '2025-01-01T00:00:00Z'),
    (15, 'Lone Star Refining',     'Custom Exhaust Hood',              'EH-CUST',     15,285.00,  4275.00, 'Shipped',       '2024-12-29T00:00:00Z', '2025-01-08T00:00:00Z'),
    -- In Production (7)
    (16, 'Bayou Industrial',       '6"" Round Fiberglass Duct',        'FD-6R',      250, 22.00,  5500.00, 'In Production', '2025-01-02T00:00:00Z', NULL),
    (17, 'Sooner Plant Services',  'Chemical-Resistant 90 Elbow 4""',  'EL-90-4CR',   90, 67.00,  6030.00, 'In Production', '2025-01-06T00:00:00Z', NULL),
    (18, 'Arkansas Fabricators',   'High-Temp Flanged Fitting 6""',    'FF-6HT',      35, 95.50,  3342.50, 'In Production', '2025-01-09T00:00:00Z', NULL),
    (19, 'Steel City Industries',  '12"" Duct Section',                'FD-12S',      60,118.00,  7080.00, 'In Production', '2025-01-13T00:00:00Z', NULL),
    (20, 'Peach State Processing', '8"" Rectangular Duct',             'FD-8X',      200, 38.75,  7750.00, 'In Production', '2025-01-16T00:00:00Z', NULL),
    (21, 'Gulf Coast Chemical',    '4"" Round Fiberglass Duct',        'FD-4R',      400, 14.50,  5800.00, 'In Production', '2025-01-19T00:00:00Z', NULL),
    (22, 'Delta Processing Co',    'Custom Exhaust Hood',              'EH-CUST',     20,285.00,  5700.00, 'In Production', '2025-01-22T00:00:00Z', NULL),
    -- Confirmed (5)
    (23, 'Lone Star Refining',     '6"" Round Fiberglass Duct',        'FD-6R',      180, 22.00,  3960.00, 'Confirmed',     '2025-01-24T00:00:00Z', NULL),
    (24, 'Bayou Industrial',       'Chemical-Resistant 90 Elbow 4""',  'EL-90-4CR',   50, 67.00,  3350.00, 'Confirmed',     '2025-01-26T00:00:00Z', NULL),
    (25, 'Magnolia Chemical',      'High-Temp Flanged Fitting 6""',    'FF-6HT',      45, 95.50,  4297.50, 'Confirmed',     '2025-01-27T00:00:00Z', NULL),
    (26, 'Cumberland Manufacturing','12"" Duct Section',               'FD-12S',      70,118.00,  8260.00, 'Confirmed',     '2025-01-28T00:00:00Z', NULL),
    (27, 'Sooner Plant Services',  '8"" Rectangular Duct',             'FD-8X',      130, 38.75,  5037.50, 'Confirmed',     '2025-01-29T00:00:00Z', NULL),
    -- Draft (3)
    (28, 'Arkansas Fabricators',   '4"" Round Fiberglass Duct',        'FD-4R',      100, 14.50,  1450.00, 'Draft',         '2025-01-30T00:00:00Z', NULL),
    (29, 'Steel City Industries',  'Custom Exhaust Hood',              'EH-CUST',     12,285.00,  3420.00, 'Draft',         '2025-01-31T00:00:00Z', NULL),
    (30, 'Peach State Processing', '6"" Round Fiberglass Duct',        'FD-6R',      220, 22.00,  4840.00, 'Draft',         '2025-02-01T00:00:00Z', NULL)
) AS s(seed_seq, client_name, product_name, product_sku, quantity, unit_price, total_value, status, order_date, ship_date)
WHERE NOT EXISTS (
    SELECT 1 FROM FiberOrders x WHERE x.user_id = u.UserId AND x.seed_seq = s.seed_seq
);
");

            // ===================================================
            // FIBER SHIPMENTS
            // Order FKs resolved by (user_id + seed_seq).
            // ===================================================
            migrationBuilder.Sql(@"
INSERT INTO FiberShipments
    (user_id, order_id, carrier_name, tracking_number, status, ship_date, estimated_arrival,
     origin_lat, origin_lng, destination_lat, destination_lng, destination_city, destination_state)
SELECT
    u.UserId,
    (SELECT id FROM FiberOrders o WHERE o.user_id = u.UserId AND o.seed_seq = s.order_seed_seq),
    s.carrier, s.tracking, s.status, s.ship_date, s.est_arrival,
    29.7604, -95.3698,
    s.dest_lat, s.dest_lng, s.dest_city, s.dest_state
FROM UserProfiles u
CROSS JOIN (VALUES
    -- Delivered shipments (order seed_seqs 1–7)
    (1,  'FedEx Freight', 'FX-2024-10041', 'Delivered',  '2024-08-14T00:00:00Z', '2024-08-19T00:00:00Z', 29.7604, -95.3698, 'Houston',       'TX'),
    (2,  'XPO Logistics', 'XP-2024-20083', 'Delivered',  '2024-08-27T00:00:00Z', '2024-09-01T00:00:00Z', 30.4515, -91.1871, 'Baton Rouge',   'LA'),
    (3,  'Old Dominion',  'OD-2024-30127', 'Delivered',  '2024-09-12T00:00:00Z', '2024-09-17T00:00:00Z', 32.2988, -90.1848, 'Jackson',       'MS'),
    (4,  'Estes Express', 'ES-2024-40162', 'Delivered',  '2024-09-30T00:00:00Z', '2024-10-05T00:00:00Z', 36.1627, -86.7816, 'Nashville',     'TN'),
    (5,  'FedEx Freight', 'FX-2024-50198', 'Delivered',  '2024-10-18T00:00:00Z', '2024-10-23T00:00:00Z', 30.0860, -94.1018, 'Beaumont',      'TX'),
    (6,  'XPO Logistics', 'XP-2024-60214', 'Delivered',  '2024-11-04T00:00:00Z', '2024-11-09T00:00:00Z', 35.4676, -97.5164, 'Oklahoma City', 'OK'),
    (7,  'Old Dominion',  'OD-2024-70251', 'Delivered',  '2024-11-16T00:00:00Z', '2024-11-21T00:00:00Z', 33.7490, -84.3880, 'Atlanta',       'GA'),
    -- In Transit (order seed_seqs 8–13, 15)
    (8,  'FedEx Freight', 'FX-2024-80289', 'In Transit', '2024-11-22T00:00:00Z', '2025-02-10T00:00:00Z', 29.9511, -90.0715, 'New Orleans',   'LA'),
    (9,  'Estes Express', 'ES-2024-90312', 'In Transit', '2024-11-29T00:00:00Z', '2025-02-14T00:00:00Z', 34.7465, -92.2896, 'Little Rock',   'AR'),
    (10, 'XPO Logistics', 'XP-2024-10348', 'In Transit', '2024-12-08T00:00:00Z', '2025-02-18T00:00:00Z', 33.5186, -86.8104, 'Birmingham',    'AL'),
    (11, 'Old Dominion',  'OD-2024-11371', 'In Transit', '2024-12-13T00:00:00Z', '2025-02-20T00:00:00Z', 29.7604, -95.3698, 'Houston',       'TX'),
    (12, 'FedEx Freight', 'FX-2024-12405', 'In Transit', '2024-12-20T00:00:00Z', '2025-02-24T00:00:00Z', 30.4515, -91.1871, 'Baton Rouge',   'LA'),
    (13, 'Estes Express', 'ES-2024-13438', 'In Transit', '2024-12-27T00:00:00Z', '2025-02-28T00:00:00Z', 32.2988, -90.1848, 'Jackson',       'MS'),
    (15, 'XPO Logistics', 'XP-2025-14012', 'In Transit', '2025-01-08T00:00:00Z', '2025-03-04T00:00:00Z', 30.0860, -94.1018, 'Beaumont',      'TX'),
    -- Delayed (order seed_seqs 14 and 10)
    (14, 'Old Dominion',  'OD-2025-15051', 'Delayed',    '2025-01-01T00:00:00Z', '2025-03-08T00:00:00Z', 36.1627, -86.7816, 'Nashville',     'TN'),
    (10, 'FedEx Freight', 'FX-2024-16092', 'Delayed',    '2024-12-09T00:00:00Z', '2025-03-12T00:00:00Z', 33.5186, -86.8104, 'Birmingham',    'AL'),
    -- Extra historical (order seed_seqs 1–6)
    (1,  'Estes Express', 'ES-2024-17001', 'Delivered',  '2024-07-05T00:00:00Z', '2024-07-12T00:00:00Z', 33.7490, -84.3880, 'Atlanta',       'GA'),
    (2,  'FedEx Freight', 'FX-2024-18004', 'Delivered',  '2024-07-18T00:00:00Z', '2024-07-25T00:00:00Z', 35.4676, -97.5164, 'Oklahoma City', 'OK'),
    (3,  'XPO Logistics', 'XP-2024-19009', 'Delivered',  '2024-07-30T00:00:00Z', '2024-08-06T00:00:00Z', 34.7465, -92.2896, 'Little Rock',   'AR'),
    (4,  'Old Dominion',  'OD-2024-20014', 'Delivered',  '2024-08-02T00:00:00Z', '2024-08-09T00:00:00Z', 29.9511, -90.0715, 'New Orleans',   'LA'),
    (5,  'FedEx Freight', 'FX-2024-21019', 'Delivered',  '2024-08-15T00:00:00Z', '2024-08-22T00:00:00Z', 32.2988, -90.1848, 'Jackson',       'MS'),
    (6,  'Estes Express', 'ES-2024-22024', 'Delivered',  '2024-09-01T00:00:00Z', '2024-09-08T00:00:00Z', 33.5186, -86.8104, 'Birmingham',    'AL')
) AS s(order_seed_seq, carrier, tracking, status, ship_date, est_arrival, dest_lat, dest_lng, dest_city, dest_state)
WHERE NOT EXISTS (
    SELECT 1 FROM FiberShipments x
    WHERE x.user_id = u.UserId AND x.tracking_number = s.tracking
);
");

            // ===================================================
            // FIBER INVENTORY TRANSACTIONS
            // Material FKs resolved by (user_id + sku).
            // ===================================================
            migrationBuilder.Sql(@"
INSERT INTO FiberInventoryTransactions
    (user_id, material_id, transaction_type, quantity, qty_before_transaction, qty_after_transaction, notes, transaction_date)
SELECT
    u.UserId,
    (SELECT id FROM FiberMaterials m WHERE m.user_id = u.UserId AND m.sku = s.sku),
    s.tx_type, s.quantity, s.qty_before, s.qty_after, s.notes, s.tx_date
FROM UserProfiles u
CROSS JOIN (VALUES
    -- Aug 2024 initial stock receives
    ('RM-FGW',  'Receive',   100,   0,   100, 'Initial stock receipt — TexMat Q3 order',           '2024-08-01T00:00:00Z'),
    ('RM-PR',   'Receive',    80,   0,    80, 'Initial stock receipt — ResinWorks Q3 order',        '2024-08-01T00:00:00Z'),
    ('RM-MEK',  'Receive',    40,   0,    40, 'Initial stock receipt — ChemCore Q3 order',          '2024-08-01T00:00:00Z'),
    ('RM-FL4',  'Receive',   500,   0,   500, 'Initial stock receipt — DuctParts flanges',          '2024-08-02T00:00:00Z'),
    ('RM-FL6',  'Receive',   250,   0,   250, 'Initial stock receipt — DuctParts flanges 6in',      '2024-08-02T00:00:00Z'),
    -- Aug–Sep 2024 consumption for orders 1–3
    ('RM-FGW',  'Consume',   -15, 100,    85, 'Consumed for Order #1 — 4in Round Duct run',         '2024-08-10T00:00:00Z'),
    ('RM-PR',   'Consume',   -30,  80,    50, 'Consumed for Order #1 — resin for duct batch',       '2024-08-10T00:00:00Z'),
    ('RM-MEK',  'Consume',   -10,  40,    30, 'Consumed for Order #1 — hardener',                   '2024-08-10T00:00:00Z'),
    ('RM-FGM',  'Consume',   -12,  72,    60, 'Consumed for Order #2 — 6in Round Duct run',         '2024-08-22T00:00:00Z'),
    ('RM-PR',   'Consume',   -18,  50,    32, 'Consumed for Order #2 — resin',                      '2024-08-22T00:00:00Z'),
    -- Sep 2024 restock
    ('RM-PR',   'Receive',    60,  32,    92, 'Emergency restock — resin below reorder point',      '2024-09-05T00:00:00Z'),
    ('RM-MEK',  'Receive',    30,  30,    60, 'Restock — hardener MEKP from ChemCore',              '2024-09-05T00:00:00Z'),
    -- Sep–Oct 2024 production
    ('RM-RW',   'Consume',    -8,  53,    45, 'Consumed for Order #3 — release wax application',   '2024-09-08T00:00:00Z'),
    ('RM-FL4',  'Consume',  -120, 500,   380, 'Consumed for Order #3 — 4in flanges installed',      '2024-09-08T00:00:00Z'),
    ('RM-FL6',  'Consume',   -80, 250,   170, 'Consumed for Order #4 — 6in flanges installed',      '2024-09-25T00:00:00Z'),
    ('RM-FGW',  'Consume',   -20,  85,    65, 'Consumed for Order #5 — flanged fitting woven mat',  '2024-10-12T00:00:00Z'),
    ('RM-PGR',  'Consume',    -5,  27,    22, 'Consumed for Order #5 — gray pigment batch',         '2024-10-12T00:00:00Z'),
    -- Oct 2024 physical count adjustments
    ('RM-PVC',  'Adjust',    -10, 150,   140, 'Physical count adjustment — PVC core overstated',    '2024-10-31T00:00:00Z'),
    ('RM-SP80', 'Adjust',     50, 450,   500, 'Physical count adjustment — sandpaper understated',  '2024-10-31T00:00:00Z'),
    -- Nov 2024 production
    ('RM-FGM',  'Consume',   -22,  60,    38, 'Consumed for Order #6 — 8in rect duct mat',          '2024-11-02T00:00:00Z'),
    ('RM-PR',   'Consume',   -40,  92,    52, 'Consumed for Order #6 — resin for rect duct',        '2024-11-02T00:00:00Z'),
    ('RM-MEK',  'Consume',   -20,  60,    40, 'Consumed for Order #7 — custom hood hardener',       '2024-11-08T00:00:00Z'),
    ('RM-PWH',  'Consume',    -8,  14,     6, 'Consumed for Order #7 — white pigment finish coat',  '2024-11-08T00:00:00Z'),
    -- Nov 2024 Q4 restock
    ('RM-PR',   'Receive',    60,  52,   112, 'Scheduled restock — resin for Q4 production',        '2024-11-15T00:00:00Z'),
    ('RM-MEK',  'Receive',    30,  40,    70, 'Scheduled restock — hardener for Q4 production',     '2024-11-15T00:00:00Z'),
    ('RM-FL6',  'Receive',   200, 170,   370, 'Restock — 6in flanges from DuctParts',               '2024-11-20T00:00:00Z'),
    -- Dec 2024 heavy production
    ('RM-FGW',  'Consume',   -30,  65,    35, 'Consumed for Orders #9–10 — duct runs',              '2024-12-05T00:00:00Z'),
    ('RM-PR',   'Consume',  -100, 112,    12, 'Consumed for Orders #9–13 — resin batch',            '2024-12-05T00:00:00Z'),
    ('RM-MEK',  'Consume',   -62,  70,     8, 'Consumed for Orders #9–13 — hardener',               '2024-12-05T00:00:00Z'),
    -- Dec 2024 write-off
    ('RM-PWH',  'WriteOff',   -8,   6,    -2, 'WriteOff — pigment white contaminated batch',         '2024-12-20T00:00:00Z'),
    -- Jan 2025 partial restock
    ('RM-PR',   'Receive',    12,  12,    12, 'Partial receive — resin backorder partial fill',      '2025-01-08T00:00:00Z'),
    ('RM-MEK',  'Receive',     8,   8,     8, 'Partial receive — hardener backorder partial fill',   '2025-01-08T00:00:00Z'),
    -- Jan 2025 current production consumption
    ('RM-FGM',  'Consume',   -10,  38,    28, 'Consumed for Order #16 — 6in duct mat',              '2025-01-15T00:00:00Z'),
    ('RM-PGR',  'Consume',    -6,  22,    16, 'Consumed for Order #17 — gray pigment',              '2025-01-15T00:00:00Z'),
    ('RM-FL6',  'Consume',  -282, 370,    88, 'Consumed for Orders #16–22 — 6in flanges',           '2025-01-20T00:00:00Z')
) AS s(sku, tx_type, quantity, qty_before, qty_after, notes, tx_date)
WHERE NOT EXISTS (
    SELECT 1 FROM FiberInventoryTransactions x
    WHERE x.user_id = u.UserId AND x.notes = s.notes AND x.transaction_date = s.tx_date
);
");

            // ===================================================
            // PROPERTIES
            // Not user-scoped in the original migration, but we seed
            // once globally (idempotent guard on Street + City).
            // ===================================================
            migrationBuilder.Sql(@"
INSERT INTO Properties
    (BrokeredBy, Status, Price, Bedrooms, Bathrooms, AcreLot, LotSqft,
     Street, City, State, ZipCode, Latitude, Longitude,
     HoaFee, PropertyTax, Utilities,
     SchoolRating, CrimeScore, Walkability, TransitAccess, AmenitiesScore,
     CommuteMin, YearBuilt, LastRenovation,
     RoofCondition, AcCondition, PlumbingCondition, ElectricalCondition,
     FloorPlanScore, FutureAppreciation, ResalePotential,
     FloodRisk, NoiseLevel)
SELECT s.*
FROM (VALUES
    ('BHHS PERRIE MUNDY REALTY GROUP','active',995000,4,3,0.25,2131,'120 Franklin Ave','Redlands','CA','92373',34.055,-117.182,0,9950,300,80,60,55,40,65,25,1985,NULL,80,80,75,75,78,70,72,10,30),
    ('CROWN REAL ESTATE TEAM','active',439000,4,2,0.18,1504,'915 Alta St','Redlands','CA','92374',34.060,-117.170,0,4390,250,65,58,60,35,55,28,1978,NULL,70,72,70,68,65,60,62,12,35),
    ('COLDWELL BANKER REALTY','active',1395000,3,3,0.35,2776,'543 E Mariposa Dr','Redlands','CA','92373',34.052,-117.188,0,13950,350,88,70,52,30,75,24,1992,2018,85,85,83,82,88,80,85,8,20),
    ('KELLER WILLIAMS REALTY','active',999900,4,3,0.28,2310,'1582 Franklin Ave','Redlands','CA','92373',34.056,-117.181,0,9990,320,82,64,58,40,68,26,1987,2016,82,84,78,79,80,75,76,9,25),
    ('KELLER WILLIAMS REALTY','active',850000,4,3,0.30,2138,'31397 Mesa Dr','Redlands','CA','92373',34.050,-117.195,0,8500,300,78,60,45,25,60,30,1984,NULL,75,76,74,72,73,68,70,11,28),
    ('CENTURY 21 LOIS LAUER REALTY','active',650000,3,2,0.40,1795,'947 Nottingham Dr','Redlands','CA','92373',34.058,-117.187,0,6500,280,75,55,52,32,60,27,1975,2009,72,70,69,68,71,65,66,12,30),
    ('EXP REALTY OF GREATER LOS ANGELES','active',610000,4,2,0.22,1308,'1539 Robyn St','Redlands','CA','92374',34.064,-117.166,0,6100,260,70,57,50,30,55,29,1980,NULL,70,72,71,70,68,63,64,13,34),
    ('CENTURY 21 LOIS LAUER REALTY','active',949000,3,3,0.27,2451,'171 Bellevue Ave','Redlands','CA','92373',34.057,-117.183,0,9490,310,83,60,60,38,72,24,1988,2015,80,82,79,78,82,72,74,10,27),
    ('KELLER WILLIAMS EMPIRE ESTATES','active',365000,3,3,0.05,1543,'135 E Cypress Ave','Redlands','CA','92373',34.053,-117.183,350,3650,250,68,62,70,55,65,20,1995,NULL,74,75,72,72,70,60,61,10,40),
    ('SHAW REAL ESTATE BROKERS','active',797000,3,3,0.26,2068,'162 Lakeside Ave','Redlands','CA','92373',34.055,-117.178,0,7970,300,80,58,55,35,68,26,1990,2017,82,84,82,80,83,72,74,9,25),
    ('FIRST TEAM REAL ESTATE','active',575000,3,2,0.20,1324,'1427 Laramie Ave','Redlands','CA','92374',34.061,-117.169,0,5750,260,70,56,52,32,58,28,1983,NULL,70,72,71,70,68,63,65,13,34),
    ('EXP REALTY OF CALIFORNIA INC.','active',870000,5,4,0.33,3091,'1678 Harrison Ln','Redlands','CA','92374',34.063,-117.168,0,8700,320,77,55,50,30,60,30,1994,2020,85,87,83,82,85,75,76,10,30),
    ('LPT REALTY INC','active',899900,5,3,0.30,3116,'1802 Pummelo Dr','Redlands','CA','92374',34.064,-117.167,0,8999,320,78,58,48,28,62,29,1996,NULL,82,83,81,80,82,72,74,11,30),
    ('COLDWELL BANKER REALTY','active',425000,2,2,0.03,1070,'93 Kansas St APT 202','Redlands','CA','92373',34.054,-117.184,350,4250,220,72,60,75,58,70,18,2000,NULL,78,80,76,76,72,62,63,8,42),
    ('SHAW REAL ESTATE BROKERS','active',725000,3,2,0.18,1751,'1612 Bellevue Rd','Redlands','CA','92373',34.057,-117.183,0,7250,280,78,58,58,38,68,25,1988,2014,78,80,76,75,76,68,70,9,28),
    ('KELLER WILLIAMS RIVERSIDE CENT','active',465000,3,2,0.17,1512,'1076 Occidental Cir','Redlands','CA','92374',34.062,-117.171,0,4650,240,68,56,52,32,58,28,1982,NULL,70,71,70,69,67,62,63,13,35),
    ('EXP REALTY OF GREATER LOS ANGELES','active',875000,5,3,0.26,2484,'527 W Palm Ave','Redlands','CA','92373',34.055,-117.192,0,8750,310,80,60,58,38,68,25,1986,2012,80,82,79,78,80,72,74,9,27),
    ('EXP REALTY OF SOUTHERN CALIFORNIA INC','active',869000,3,3,0.25,2650,'356 Campbell Ave','Redlands','CA','92373',34.053,-117.186,0,8690,320,82,62,56,36,70,24,1991,2016,82,84,80,79,83,73,75,9,25),
    ('RE/MAX ADVANTAGE','active',239000,2,1,0.03,918,'48 N Center St','Redlands','CA','92373',34.054,-117.182,300,2390,180,68,62,78,60,72,16,1975,NULL,65,66,64,63,66,55,56,10,45),
    ('BERKSHIRE HATHAWAY HOMESERVICES CALIFORNIA REALTY','active',939000,5,3,0.24,2208,'1525 Garden St','Redlands','CA','92373',34.056,-117.179,0,9390,310,80,60,56,36,68,25,1990,2014,80,82,79,78,81,72,74,9,28),
    ('ARCH PACIFIC REALTY','active',685000,4,3,0.18,1746,'622 Esther Way','Redlands','CA','92373',34.054,-117.185,0,6850,270,76,58,60,40,66,23,1988,NULL,76,78,75,74,75,67,68,10,30),
    ('ADAM CUNNINGHAM BROKER','active',489900,3,2,0.13,987,'838 W Brockton Ave','Redlands','CA','92374',34.060,-117.176,0,4899,240,68,56,55,34,58,27,1978,2022,72,74,70,70,68,63,64,12,35),
    ('SHAW REAL ESTATE BROKERS','active',585000,2,2,0.14,1151,'936 Judson St','Redlands','CA','92374',34.061,-117.172,0,5850,250,70,57,55,35,60,26,1985,NULL,72,73,71,70,70,64,65,12,33),
    ('CENTURY 21 LOIS LAUER REALTY','active',425000,3,2,0.20,1960,'1412 Medallion St','Redlands','CA','92374',34.063,-117.168,0,4250,230,66,56,50,30,55,29,1979,NULL,68,69,67,66,65,60,61,13,36),
    ('EXPERT REAL ESTATE & INVESTMENT','active',649900,5,4,0.28,3136,'261 E Crescent Ave','Redlands','CA','92373',34.058,-117.178,0,6499,280,78,62,55,35,65,26,1987,NULL,74,75,72,71,76,67,68,11,30),
    ('KELLER WILLIAMS REALTY','active',690000,3,3,0.14,1375,'1221 San Jacinto St','Redlands','CA','92373',34.056,-117.181,0,6900,260,76,60,62,42,66,22,1992,NULL,76,78,74,74,74,67,68,10,32),
    ('KELLER WILLIAMS REALTY','active',645000,3,3,0.22,2045,'416 Sonora Cir','Redlands','CA','92373',34.054,-117.187,0,6450,270,76,60,58,38,66,24,1989,2011,76,78,75,74,75,67,69,10,30),
    ('BERKSHIRE HATHAWAY HOMESERVICES CALIFORNIA REALTY','active',849000,5,3,0.25,2448,'1235 W Cypress Ave','Redlands','CA','92373',34.053,-117.191,0,8490,300,78,60,55,35,65,27,1985,NULL,78,79,77,76,77,70,72,11,29),
    ('TOWN & COUNTRY REAL ESTATE','active',569900,3,2,0.17,1176,'1323 Kingswood Dr','Redlands','CA','92374',34.062,-117.170,0,5699,250,68,56,50,30,57,28,1983,NULL,70,71,69,68,67,62,63,13,35),
    ('RE/MAX ADVANTAGE','active',699900,4,2,0.20,1678,'1030 Fallbrook Ave','Redlands','CA','92373',34.055,-117.180,0,6999,270,76,58,56,36,64,25,1987,NULL,74,76,73,72,73,67,69,10,30),
    ('E HOMES','active',539999,3,3,0.07,1805,'56 Dearborn Cir','Redlands','CA','92374',34.063,-117.175,280,5400,240,72,57,58,42,62,22,2005,NULL,78,79,76,76,74,65,66,10,38),
    ('REAL BROKER','active',1199999,4,3,0.55,2364,'11891 San Timoteo Canyon Rd','Redlands','CA','92373',34.041,-117.148,0,12000,380,74,45,30,15,50,38,1998,2015,82,83,80,79,78,75,78,14,15),
    ('EXP REALTY OF CALIFORNIA INC','active',849000,4,3,0.35,1894,'30993 Palo Alto Dr','Redlands','CA','92373',34.049,-117.196,0,8490,300,76,52,38,20,58,32,1983,NULL,74,75,72,71,72,68,70,12,22),
    ('COLDWELL BANKER LEADERS','active',819900,5,3,0.24,2244,'1410 Pleasant View Dr','Redlands','CA','92374',34.063,-117.167,0,8199,310,78,56,50,30,62,29,1994,NULL,80,81,79,78,79,72,74,11,30),
    ('HOME WORKS REALTY','active',220000,3,2,0.06,1000,'455 Judson St Space 9','Redlands','CA','92374',34.061,-117.172,250,2200,180,58,56,48,28,52,27,1972,NULL,60,60,58,57,58,50,51,13,38),
    ('COLDWELL BANKER REALTY','active',125000,1,1,0.02,671,'167 N Center St','Redlands','CA','92373',34.055,-117.182,300,1250,150,65,62,78,60,68,16,1968,NULL,58,60,55,55,60,50,51,9,48),
    ('REAL ESTATE MAVENS','active',1949990,5,6,1.20,4367,'31615 Live Oak Canyon Rd','Redlands','CA','92373',34.040,-117.158,0,19500,480,76,45,28,12,52,40,2005,NULL,88,89,86,86,88,82,85,12,12),
    ('CENTURY 21 EXPERIENCE','active',1550000,5,5,0.65,4458,'215 San Rafael St','Redlands','CA','92373',34.058,-117.190,0,15500,420,86,62,55,32,74,25,2000,2019,88,89,86,85,89,82,84,9,22),
    ('EXP REALTY OF GREATER LOS ANGELES','active',760000,3,3,0.23,2232,'412 Phlox Ct','Redlands','CA','92373',34.053,-117.186,0,7600,290,80,60,56,36,68,25,1991,NULL,80,81,78,77,79,70,72,9,27),
    ('PATRICIA HICKS REALTOR','active',949900,4,4,0.28,2854,'1575 Grove St','Redlands','CA','92374',34.063,-117.167,0,9499,320,80,56,50,30,64,28,1996,NULL,82,83,81,80,82,73,75,11,29),
    ('CENTURY 21 LOIS LAUER REALTY','active',500000,3,3,0.06,1805,'61 Sparrow Ct','Redlands','CA','92374',34.062,-117.173,300,5000,240,72,56,58,42,62,22,2004,NULL,78,79,76,76,73,64,65,10,38),
    ('CENTURY 21 TOP PRODUCERS','active',1499900,6,4,0.80,4227,'31027 E Sunset Dr N','Redlands','CA','92373',34.050,-117.175,0,15000,420,78,50,35,18,58,35,2001,NULL,84,85,82,81,84,78,80,12,18),
    ('ONE WEST REALTY','active',675000,4,3,0.21,2010,'1688 Camellia Ln','Redlands','CA','92374',34.063,-117.166,0,6750,270,72,56,50,30,60,28,1992,NULL,76,77,74,73,73,66,67,12,33),
    ('RE/MAX TIME REALTY','active',729000,4,3,0.25,2477,'1237 Sherry Way','Redlands','CA','92374',34.062,-117.168,0,7290,280,74,56,50,30,62,28,1995,NULL,78,79,76,75,76,68,70,11,32),
    ('CENTURY 21 LOIS LAUER REALTY','active',1349000,4,4,0.75,3732,'13049 Burns Ln','Redlands','CA','92373',34.043,-117.155,0,13490,400,76,48,30,15,55,36,1999,2015,84,85,82,81,82,76,78,13,18),
    ('YUCAIPA VALLEY REAL ESTATE','active',579000,4,2,0.18,1308,'1602 Glover St','Redlands','CA','92374',34.064,-117.165,0,5790,250,70,56,50,30,58,28,1981,NULL,70,72,70,69,68,63,65,13,35),
    ('THE REAL ESTATE GROUP','active',749000,3,3,0.24,2040,'505 E Sunset Dr','Redlands','CA','92373',34.056,-117.175,0,7490,290,80,58,55,35,68,26,1990,NULL,80,81,78,77,79,70,72,9,28),
    ('ROA CALIFORNIA INC','active',709999,4,2,0.19,1556,'15 Naomi St','Redlands','CA','92374',34.061,-117.170,0,7100,270,72,56,52,32,60,27,1984,NULL,72,74,71,70,70,65,66,12,34),
    ('THE ASSOCIATES REALTY GROUP','active',629900,3,2,0.19,1536,'323 E Colton Ave','Redlands','CA','92374',34.060,-117.170,0,6299,260,70,56,52,32,58,27,1982,NULL,70,72,70,69,68,63,65,13,35),
    ('REALTY ONE GROUP HOMELINK','active',478111,3,1,0.16,1261,'1225 Alta St','Redlands','CA','92374',34.061,-117.170,0,4781,230,66,58,55,33,55,28,1977,NULL,66,67,65,64,63,58,59,13,36),
    ('HOMESMART','active',1950000,6,6,0.75,6938,'1377 Knoll Rd','Redlands','CA','92373',34.057,-117.190,0,19500,480,86,62,52,30,74,26,2003,2021,90,91,88,88,90,84,86,9,22),
    ('RE/MAX ADVANTAGE','active',1647000,4,4,0.95,4023,'748 La Solana Dr','Redlands','CA','92373',34.050,-117.193,0,16470,440,82,55,40,20,64,30,1998,2016,88,89,86,85,87,80,82,10,20),
    ('REALTY MASTERS & ASSOCIATES','active',649900,3,2,0.20,1740,'1326 Campus Ave','Redlands','CA','92374',34.062,-117.169,0,6499,260,70,56,50,30,58,28,1984,NULL,72,73,70,69,69,63,64,13,34),
    ('BHHS PERRIE MUNDY REALTY GROUP','active',1795000,4,4,0.80,3291,'12669 Valley View Ln','Redlands','CA','92373',34.045,-117.162,0,17950,440,82,50,38,20,62,33,2001,2018,88,89,86,85,87,81,83,11,18),
    ('CENTURY 21 LOIS LAUER REALTY','active',1600000,4,3,0.55,3094,'1805 Canyon Rd','Redlands','CA','92373',34.056,-117.194,0,16000,420,82,55,45,22,68,28,1997,2020,88,89,87,86,87,80,83,10,20),
    ('COLDWELL BANKER REALTY','active',920000,3,3,0.24,2232,'1370 Oak St','Redlands','CA','92373',34.055,-117.182,0,9200,310,82,60,58,38,70,24,1992,2016,82,84,80,79,83,74,76,9,27),
    ('PRICE REAL ESTATE GROUP INC','active',595000,4,3,0.13,1008,'903 Webster St','Redlands','CA','92374',34.061,-117.173,0,5950,250,70,56,55,34,58,27,1982,NULL,70,71,69,68,68,63,64,13,35),
    ('BERKSHIRE HATHAWAY HOMESERVICES CALIFORNIA REALTY','active',1525000,5,5,1.00,4926,'30693 E Sunset Dr S','Redlands','CA','92373',34.048,-117.178,0,15250,430,80,50,35,18,60,33,2002,NULL,86,87,84,83,86,79,81,12,18),
    ('CAMINO REALTY','active',759900,5,3,0.40,2687,'31156 Danelaw Ave','Redlands','CA','92373',34.047,-117.188,0,7599,290,74,52,38,20,58,33,1988,NULL,76,77,74,73,73,67,70,12,22),
    ('ROBERT GELFAND BROKER','active',1770000,5,5,1.10,4650,'652 Fairway Dr','Redlands','CA','92373',34.053,-117.194,0,17700,450,84,55,45,22,70,27,2004,2019,88,89,87,86,89,82,84,9,20),
    ('EXP REALTY OF SOUTHERN CALIFORNIA INC','active',1150000,4,4,0.30,2466,'1388 Brandon Ct','Redlands','CA','92373',34.056,-117.185,0,11500,360,84,60,55,35,72,25,1996,2014,84,86,82,81,85,77,79,9,25),
    ('EXP REALTY OF SOUTHERN CALIFORNIA INC','active',1690000,5,4,0.85,4559,'1608 Smiley Rdg','Redlands','CA','92373',34.061,-117.194,0,16900,440,84,55,42,22,68,28,2003,2018,88,89,86,85,88,81,83,10,20),
    ('BHHS PERRIE MUNDY REALTY GROUP','active',649000,2,1,0.22,1022,'1029 W Palm Ave','Redlands','CA','92373',34.055,-117.192,0,6490,250,76,58,58,36,64,25,1935,2005,70,72,68,67,72,65,67,9,28),
    ('BHHS PERRIE MUNDY REALTY GROUP','active',1595000,4,4,0.65,4105,'952 Creek View Ln','Redlands','CA','92373',34.057,-117.188,0,15950,420,84,58,52,32,72,26,2002,2019,88,89,87,86,88,81,83,9,22),
    ('RE/MAX ADVANTAGE','active',1250000,5,4,0.55,3676,'1617 Garden St','Redlands','CA','92373',34.057,-117.179,0,12500,380,82,58,55,35,70,25,1997,2013,84,86,82,81,85,77,79,9,25),
    ('MOS REAL ESTATE','active',758000,4,3,0.27,2877,'1744 Sunny Heights Ln','Redlands','CA','92374',34.063,-117.164,0,7580,290,76,56,50,30,62,29,1995,2023,80,82,78,77,78,70,72,11,30),
    ('BERKSHIRE HATHAWAY HOMESERVICES CALIFORNIA REALTY','active',1999000,6,6,1.50,3726,'30300 Live Oak Canyon Rd','Redlands','CA','92373',34.042,-117.155,0,19990,480,76,45,28,12,55,40,2000,2017,88,89,86,85,86,81,84,12,12),
    ('CENTURY 21 LOIS LAUER REALTY','active',599900,4,2,0.22,1892,'108 S Buena Vista St','Redlands','CA','92373',34.054,-117.181,0,5999,260,72,58,58,36,62,23,1980,NULL,70,72,70,69,69,63,65,11,33),
    ('CENTURY 21 LOIS LAUER REALTY','active',329000,2,2,0.05,1152,'525 La Verne St','Redlands','CA','92373',34.055,-117.184,280,3290,200,70,60,70,52,64,19,1985,NULL,72,73,70,70,70,60,61,10,40),
    ('DYNASTY REAL ESTATE','active',589999,3,2,0.17,1325,'1543 Hanford St','Redlands','CA','92374',34.063,-117.168,0,5900,250,70,56,50,30,58,28,1984,NULL,72,73,70,69,69,63,65,13,34),
    ('EXP REALTY OF SOUTHERN CALIFORNIA INC','active',2599999,4,3,0.70,3447,'745 W Sunset Dr','Redlands','CA','92373',34.056,-117.195,0,26000,500,84,58,48,25,70,28,1995,2021,88,89,87,86,87,82,85,9,20),
    ('LUXRE REALTY INC','active',618000,3,3,0.20,1832,'1160 Via Ravenna St','Redlands','CA','92374',34.062,-117.169,0,6180,260,72,56,52,32,60,27,1997,2023,78,80,76,75,76,67,68,11,32),
    ('BERKSHIRE HATHAWAY HOMESERVICES CALIFORNIA REALTY','active',724000,3,3,0.22,2038,'1049 Evergreen Ct','Redlands','CA','92374',34.062,-117.170,0,7240,280,74,56,52,32,62,27,1996,NULL,78,79,76,75,77,68,70,11,32),
    ('SHAW REAL ESTATE BROKERS','active',2497000,5,6,1.20,5581,'1922 Country Club Ln','Redlands','CA','92373',34.055,-117.193,0,24970,520,86,58,48,25,74,27,2006,2020,90,91,89,88,91,85,87,9,20),
    ('EXP REALTY OF SOUTHERN CALIFORNIA INC','active',1799999,4,3,1.80,3880,'28450 Live Oak Canyon Rd','Redlands','CA','92373',34.041,-117.162,0,18000,460,74,45,28,12,52,40,1998,NULL,84,85,82,81,82,78,81,13,12),
    ('MERITAGE HOMES OF CALIFORNIA','active',589000,3,3,0.07,1648,'1114 Tropic Ct','Redlands','CA','92374',34.062,-117.165,300,5890,250,76,55,62,52,66,24,2024,NULL,92,92,92,92,78,72,74,10,30),
    ('REALTY MASTERS & ASSOCIATES','active',490000,2,2,0.05,1176,'1580 Lisa Ln','Redlands','CA','92374',34.063,-117.167,320,4900,230,72,56,55,38,60,25,2001,NULL,78,79,76,76,72,63,65,10,35),
    ('SMART SELL REAL ESTATE','active',595000,4,3,0.20,1758,'1510 Karon St','Redlands','CA','92374',34.063,-117.167,0,5950,260,70,56,52,32,60,28,1985,NULL,70,72,70,69,69,63,65,12,34),
    ('IKON PROPERTIES & INVESTMENTS','active',649900,3,4,0.22,2045,'434 Sonora Cir','Redlands','CA','92373',34.054,-117.187,0,6499,270,76,60,58,38,66,24,1989,2012,76,78,75,74,77,68,70,10,29),
    ('KELLER WILLIAMS REALTY','active',1300000,5,4,0.55,4641,'1641 Ford Ave','Redlands','CA','92374',34.063,-117.164,0,13000,400,80,54,50,30,64,30,2000,NULL,86,87,84,83,86,78,80,11,28),
    ('REALTY ONE GROUP ROADS','active',181899,3,2,0.07,1152,'1721 E Colton Ave Space 33','Redlands','CA','92374',34.059,-117.162,180,1819,170,58,55,45,25,50,30,1975,NULL,58,58,56,55,56,48,49,14,38),
    ('BERKSHIRE HATHAWAY HOMESERVICES CALIFORNIA','active',174900,2,2,0.06,1040,'450 Judson #94','Redlands','CA','92374',34.061,-117.172,200,1749,165,58,55,45,25,50,27,1973,NULL,56,57,54,53,55,47,48,13,38),
    ('BERKSHIRE HATHAWAY HOMESERVICES CALIFORNIA REALTY','active',1399000,8,5,0.55,5100,'31607 Florida St','Redlands','CA','92373',34.047,-117.183,0,13990,420,76,50,35,18,58,36,1998,NULL,80,81,78,77,80,73,76,12,20),
    ('HASS & JOHN REAL ESTATE','active',649999,4,2,0.22,1945,'1024 Lawton St','Redlands','CA','92374',34.061,-117.171,0,6500,260,70,56,52,32,60,27,1982,2022,74,76,73,72,70,64,65,12,34),
    ('BERKSHIRE HATHAWAY HOMESERVICES CALIFORNIA REALTY','active',167000,2,2,0.06,1464,'1251 E Lugonia Ave Space 24','Redlands','CA','92374',34.059,-117.162,200,1670,165,58,55,45,25,50,30,1974,NULL,56,57,55,54,55,47,48,14,38),
    ('KELLER WILLIAMS REALTY','active',649999,3,1,0.16,1200,'421 La Verne St','Redlands','CA','92373',34.055,-117.184,0,6500,250,74,60,62,42,64,20,1940,2008,68,70,66,66,72,64,66,9,38),
    ('CENTURY 21 LOIS LAUER REALTY','active',599777,3,2,0.06,1362,'1032 Ardmore Cir','Redlands','CA','92374',34.062,-117.170,280,5998,250,72,56,55,38,60,25,2002,NULL,78,79,76,76,73,64,66,10,35),
    ('ROA CALIFORNIA INC','active',1200000,5,3,0.25,2793,'116 Franklin Ave','Redlands','CA','92373',34.055,-117.182,0,12000,370,80,60,55,36,67,25,1985,NULL,78,80,76,75,79,73,75,10,30),
    ('MERITAGE HOMES OF CALIFORNIA','active',569000,3,3,0.07,1648,'1118 Tropic Ct','Redlands','CA','92374',34.062,-117.165,300,5690,250,76,55,62,52,66,24,2024,NULL,92,92,92,92,78,72,74,10,30),
    ('M G R REAL ESTATE','active',469900,2,2,0.03,1070,'93 Kansas St APT 802','Redlands','CA','92373',34.054,-117.184,350,4699,220,72,60,75,58,70,18,2000,NULL,78,80,76,76,72,63,64,8,42),
    ('MGA ASSOCIATES','active',499900,3,3,0.05,1392,'122 N Tamarisk St','Redlands','CA','92373',34.054,-117.183,320,4999,230,72,60,72,54,66,19,1998,NULL,76,77,74,74,73,63,64,9,40),
    ('CENTURY 21 LOIS LAUER REALTY','active',450000,3,2,0.05,1486,'246 E Fern Ave APT 109','Redlands','CA','92373',34.053,-117.185,340,4500,225,72,60,72,54,66,19,1995,NULL,74,75,72,72,72,62,63,9,40),
    ('MERITAGE HOMES OF CALIFORNIA','active',564000,3,3,0.07,1465,'1116 Tropic Ct','Redlands','CA','92374',34.062,-117.165,300,5640,250,76,55,62,52,66,24,2024,NULL,92,92,92,92,78,72,74,10,30),
    ('MERITAGE HOMES OF CALIFORNIA','active',549000,3,3,0.07,1465,'1112 Tropic Ct','Redlands','CA','92374',34.062,-117.165,300,5490,250,76,55,62,52,66,24,2024,NULL,92,92,92,92,78,72,74,10,30),
    ('BHHS PERRIE MUNDY REALTY GROUP','active',415000,2,2,0.05,1188,'254 E Fern Ave APT 212','Redlands','CA','92373',34.053,-117.185,340,4150,220,72,60,72,54,66,19,1995,NULL,74,75,72,72,71,61,62,9,40),
    ('MERITAGE HOMES','active',569000,3,3,0.07,1465,'1119 Tropic Ct','Redlands','CA','92374',34.062,-117.165,300,5690,250,76,55,62,52,66,24,2024,NULL,92,92,92,92,78,72,74,10,30),
    ('RE/MAX ADVANTAGE','active',325000,2,2,0.06,1056,'1174 Benbow Pl','Redlands','CA','92374',34.062,-117.173,280,3250,190,68,56,52,35,55,26,1975,NULL,65,66,63,62,63,56,57,13,36),
    ('REBECCA AUSTIN BROKER','active',1074990,5,5,0.22,3306,'1452 Moore St','Redlands','CA','92374',34.062,-117.163,350,10750,360,78,54,58,48,66,26,2024,NULL,94,94,94,94,82,76,78,10,28),
    ('PRICE REAL ESTATE GROUP INC','active',429999,2,3,0.05,1394,'1200 Highland Ave APT 207','Redlands','CA','92374',34.062,-117.170,340,4300,215,70,56,58,42,60,24,1999,NULL,74,75,72,72,70,61,62,10,38),
    ('PONCE & PONCE REALTY INC','active',475000,2,2,0.05,1105,'1592 Christopher Ln','Redlands','CA','92374',34.063,-117.167,310,4750,230,72,56,55,38,60,25,2002,NULL,78,79,76,76,72,63,65,10,35),
    ('TRI POINTE HOMES HOLDINGS INC','active',939000,5,4,0.22,3608,'1713 Wren Ave','Redlands','CA','92374',34.062,-117.163,350,9390,340,78,54,58,48,66,26,2024,NULL,94,94,94,94,84,77,79,10,28),
    ('RE/MAX CHAMPIONS','active',1350000,5,4,0.60,3800,'12698 La Solana Dr','Redlands','CA','92373',34.048,-117.193,0,13500,400,80,52,38,20,62,32,2000,NULL,86,87,84,83,85,78,80,11,20),
    ('CENTURY 21 LOIS LAUER REALTY','active',559000,3,2,0.06,1643,'1575 Christopher Ln','Redlands','CA','92374',34.063,-117.167,310,5590,245,72,56,55,38,60,25,2001,NULL,76,77,74,74,72,63,64,10,35),
    ('BHHS PERRIE MUNDY REALTY GROUP','active',2500000,4,3,0.50,2183,'831 W Lugonia Ave','Redlands','CA','92374',34.059,-117.177,0,25000,500,76,52,50,30,62,28,1990,NULL,76,77,74,73,72,72,75,10,28),
    ('RE/MAX ADVANTAGE','active',649000,2,2,0.13,1107,'509 S 4th St','Redlands','CA','92373',34.052,-117.180,0,6490,250,76,60,62,42,66,20,1938,2010,70,72,68,67,74,66,68,9,35),
    ('MERITAGE HOMES OF CALIFORNIA','active',695400,4,3,0.08,2020,'2084 Meyer Ln','Redlands','CA','92374',34.062,-117.163,300,6954,280,76,55,62,52,66,24,2024,NULL,94,94,94,94,80,74,76,10,30),
    ('TRI POINTE HOMES HOLDINGS INC','active',997000,5,4,0.22,3897,'1731 Wren Ave','Redlands','CA','92374',34.062,-117.163,350,9970,350,78,54,58,48,68,26,2024,NULL,94,94,94,94,86,78,80,10,28),
    ('TRI POINTE HOMES HOLDINGS INC','active',898000,4,3,0.22,2813,'1568 Pintail St','Redlands','CA','92374',34.062,-117.164,350,8980,320,76,54,58,48,66,26,2024,NULL,94,94,94,94,82,76,78,10,28),
    ('SHAW REAL ESTATE BROKERS','active',295000,2,2,0.08,1368,'1331 Century St','Redlands','CA','92374',34.062,-117.172,200,2950,185,62,56,50,30,52,28,1974,NULL,60,61,58,57,58,50,51,14,36),
    ('MERITAGE HOMES OF CALIFORNIA','active',639800,3,3,0.07,1815,'1140 Tropic Ct','Redlands','CA','92374',34.062,-117.165,300,6398,255,76,55,62,52,66,24,2024,NULL,92,92,92,92,79,73,75,10,30),
    ('REBECCA AUSTIN BROKER','active',1058705,4,5,0.22,2803,'1479 Moore St','Redlands','CA','92374',34.062,-117.163,350,10587,355,78,54,58,48,66,26,2024,NULL,94,94,94,94,83,76,78,10,28),
    ('REBECCA AUSTIN BROKER','active',1034990,4,5,0.22,2803,'1458 Moore St','Redlands','CA','92374',34.062,-117.163,350,10350,355,78,54,58,48,66,26,2024,NULL,94,94,94,94,83,76,78,10,28),
    ('BEAZER HOMES','active',1149990,5,4,0.22,3306,'1472 Moore St','Redlands','CA','92374',34.062,-117.163,350,11500,360,78,54,58,48,66,26,2024,NULL,94,94,94,94,84,77,79,10,28),
    ('MERITAGE HOMES OF CALIFORNIA','active',739900,5,3,0.09,2418,'2070 Tangelo Ln','Redlands','CA','92374',34.062,-117.163,300,7399,290,76,55,62,52,66,24,2024,NULL,92,92,92,92,82,75,77,10,30),
    ('MERITAGE HOMES OF CALIFORNIA','active',702700,4,3,0.08,2020,'2051 Tangelo Ln','Redlands','CA','92374',34.062,-117.163,300,7027,285,76,55,62,52,66,24,2024,NULL,92,92,92,92,80,74,76,10,30),
    ('PONCE & PONCE REALTY INC','active',195000,2,2,0.05,1400,'626 Dearborn #7','Redlands','CA','92374',34.063,-117.175,220,1950,170,58,56,48,28,50,26,1972,NULL,56,57,54,53,55,47,48,13,38),
    ('BIG BLOCK POWERHOUSE REALTY','active',740000,4,3,0.22,2449,'833 Half Moon Ave','Redlands','CA','92374',34.062,-117.164,320,7400,285,76,54,58,48,65,25,2024,NULL,92,92,92,92,80,74,76,10,29),
    ('MERITAGE HOMES OF CALIFORNIA','active',701400,4,3,0.08,2020,'2069 Tangelo Ln','Redlands','CA','92374',34.062,-117.163,300,7014,285,76,55,62,52,66,24,2024,NULL,92,92,92,92,80,74,76,10,30),
    ('HOME SAVER REALTY','active',1200000,4,3,0.27,3332,'259 E Crescent Ave','Redlands','CA','92373',34.058,-117.178,0,12000,370,80,62,55,35,67,26,1988,NULL,78,80,76,75,80,74,76,11,30),
    ('IRN REALTY','active',1170000,9,7,0.18,4006,'610 E Lugonia Ave #4','Redlands','CA','92374',34.059,-117.163,0,11700,400,68,55,50,32,56,29,1985,NULL,68,69,66,65,64,63,65,13,35),
    ('MERITAGE HOMES OF CALIFORNIA','active',661000,3,3,0.07,1816,'2040 Tangelo Ln','Redlands','CA','92374',34.062,-117.163,300,6610,260,76,55,62,52,66,24,2024,NULL,92,92,92,92,78,72,74,10,30),
    ('BEAZER HOMES','active',1089990,4,4,0.22,2803,'1476 Moore St','Redlands','CA','92374',34.062,-117.163,350,10900,355,78,54,58,48,66,26,2024,NULL,94,94,94,94,83,76,78,10,28),
    ('TRI POINTE HOMES HOLDINGS INC','active',799000,4,3,0.22,2676,'873 Railway Ln','Redlands','CA','92374',34.062,-117.165,320,7990,290,76,54,58,48,65,25,2024,NULL,92,92,92,92,80,74,76,10,29),
    ('TRI POINTE HOMES HOLDINGS INC','active',819000,4,3,0.22,2676,'848 Railway Ln','Redlands','CA','92374',34.062,-117.165,320,8190,290,76,54,58,48,65,25,2024,NULL,92,92,92,92,81,75,77,10,29),
    ('MERITAGE HOMES OF CALIFORNIA','active',687500,3,3,0.07,1816,'2063 Tangelo Ln','Redlands','CA','92374',34.062,-117.163,300,6875,265,76,55,62,52,66,24,2024,NULL,92,92,92,92,79,73,75,10,30),
    ('COLDWELL BANKER KIVETT-TEETERS','active',173000,2,2,0.07,1595,'626 N Dearborn St Spc 56','Redlands','CA','92374',34.063,-117.175,200,1730,165,58,55,45,25,50,27,1973,NULL,56,57,54,53,54,46,47,13,38)
) AS s(BrokeredBy,Status,Price,Bedrooms,Bathrooms,AcreLot,LotSqft,Street,City,State,ZipCode,Latitude,Longitude,HoaFee,PropertyTax,Utilities,SchoolRating,CrimeScore,Walkability,TransitAccess,AmenitiesScore,CommuteMin,YearBuilt,LastRenovation,RoofCondition,AcCondition,PlumbingCondition,ElectricalCondition,FloorPlanScore,FutureAppreciation,ResalePotential,FloodRisk,NoiseLevel)
WHERE NOT EXISTS (
    SELECT 1 FROM Properties x WHERE x.Street = s.Street AND x.City = s.City AND x.ZipCode = s.ZipCode
);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove FiberFlow data for all users
            migrationBuilder.Sql("DELETE FROM FiberInventoryTransactions;");
            migrationBuilder.Sql("DELETE FROM FiberShipments;");
            migrationBuilder.Sql("DELETE FROM FiberOrders;");
            migrationBuilder.Sql("DELETE FROM FiberMaterials;");
            migrationBuilder.Sql("DELETE FROM FiberClients;");

            // Remove Properties seed data
            migrationBuilder.Sql("DELETE FROM Properties WHERE City = 'Redlands' AND State = 'CA';");
        }
    }
}