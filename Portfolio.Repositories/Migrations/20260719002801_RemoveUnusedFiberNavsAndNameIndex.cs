using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portfolio.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedFiberNavsAndNameIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FiberOrders_FiberClients_FiberClientId",
                table: "FiberOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_FiberShipments_FiberOrders_FiberOrderId",
                table: "FiberShipments");

            migrationBuilder.DropIndex(
                name: "IX_FiberShipments_FiberOrderId",
                table: "FiberShipments");

            migrationBuilder.DropIndex(
                name: "IX_FiberOrders_FiberClientId",
                table: "FiberOrders");

            migrationBuilder.DropColumn(
                name: "FiberOrderId",
                table: "FiberShipments");

            migrationBuilder.DropColumn(
                name: "FiberClientId",
                table: "FiberOrders");

            // Composite functional index so case-insensitive saved-search name checks
            // (WHERE "UserId" = ... AND lower("Name") = lower(...)) are index-seekable
            // instead of scanning every row.
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_SavedSearches_UserId_LowerName\" " +
                "ON \"SavedSearches\" (\"UserId\", lower(\"Name\"));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_SavedSearches_UserId_LowerName\";");

            migrationBuilder.AddColumn<int>(
                name: "FiberOrderId",
                table: "FiberShipments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FiberClientId",
                table: "FiberOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FiberShipments_FiberOrderId",
                table: "FiberShipments",
                column: "FiberOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_FiberOrders_FiberClientId",
                table: "FiberOrders",
                column: "FiberClientId");

            migrationBuilder.AddForeignKey(
                name: "FK_FiberOrders_FiberClients_FiberClientId",
                table: "FiberOrders",
                column: "FiberClientId",
                principalTable: "FiberClients",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_FiberShipments_FiberOrders_FiberOrderId",
                table: "FiberShipments",
                column: "FiberOrderId",
                principalTable: "FiberOrders",
                principalColumn: "id");
        }
    }
}
