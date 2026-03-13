using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Portfolio.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddFiberFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FiberClients",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    contact_name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: false),
                    city = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiberClients", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "FiberMaterials",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    sku = table.Column<string>(type: "text", nullable: false),
                    unit_of_measure = table.Column<string>(type: "text", nullable: false),
                    qty_on_hand = table.Column<decimal>(type: "numeric", nullable: false),
                    reorder_point = table.Column<decimal>(type: "numeric", nullable: false),
                    reorder_qty = table.Column<decimal>(type: "numeric", nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric", nullable: false),
                    supplier = table.Column<string>(type: "text", nullable: false),
                    warehouse_location = table.Column<string>(type: "text", nullable: false),
                    last_updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiberMaterials", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "FiberOrders",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<int>(type: "integer", nullable: false),
                    product_name = table.Column<string>(type: "text", nullable: false),
                    product_sku = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric", nullable: false),
                    total_value = table.Column<decimal>(type: "numeric", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    order_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ship_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiberOrders", x => x.id);
                    table.ForeignKey(
                        name: "FK_FiberOrders_FiberClients_client_id",
                        column: x => x.client_id,
                        principalTable: "FiberClients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FiberInventoryTransactions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_id = table.Column<int>(type: "integer", nullable: false),
                    transaction_type = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    qty_before_transaction = table.Column<decimal>(type: "numeric", nullable: false),
                    qty_after_transaction = table.Column<decimal>(type: "numeric", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    transaction_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiberInventoryTransactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_FiberInventoryTransactions_FiberMaterials_material_id",
                        column: x => x.material_id,
                        principalTable: "FiberMaterials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FiberShipments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<int>(type: "integer", nullable: false),
                    carrier_name = table.Column<string>(type: "text", nullable: false),
                    tracking_number = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    ship_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    estimated_arrival = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    origin_lat = table.Column<double>(type: "double precision", nullable: false),
                    origin_lng = table.Column<double>(type: "double precision", nullable: false),
                    destination_lat = table.Column<double>(type: "double precision", nullable: false),
                    destination_lng = table.Column<double>(type: "double precision", nullable: false),
                    destination_city = table.Column<string>(type: "text", nullable: false),
                    destination_state = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiberShipments", x => x.id);
                    table.ForeignKey(
                        name: "FK_FiberShipments_FiberOrders_order_id",
                        column: x => x.order_id,
                        principalTable: "FiberOrders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FiberInventoryTransactions_material_id",
                table: "FiberInventoryTransactions",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "IX_FiberOrders_client_id",
                table: "FiberOrders",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "IX_FiberShipments_order_id",
                table: "FiberShipments",
                column: "order_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FiberInventoryTransactions");

            migrationBuilder.DropTable(
                name: "FiberShipments");

            migrationBuilder.DropTable(
                name: "FiberMaterials");

            migrationBuilder.DropTable(
                name: "FiberOrders");

            migrationBuilder.DropTable(
                name: "FiberClients");
        }
    }
}
