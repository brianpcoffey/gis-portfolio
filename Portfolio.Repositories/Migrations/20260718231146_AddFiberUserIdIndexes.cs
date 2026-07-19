using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portfolio.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddFiberUserIdIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FiberShipments_user_id",
                table: "FiberShipments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_FiberOrders_user_id",
                table: "FiberOrders",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_FiberMaterials_user_id",
                table: "FiberMaterials",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_FiberInventoryTransactions_user_id",
                table: "FiberInventoryTransactions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_FiberClients_user_id",
                table: "FiberClients",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FiberShipments_user_id",
                table: "FiberShipments");

            migrationBuilder.DropIndex(
                name: "IX_FiberOrders_user_id",
                table: "FiberOrders");

            migrationBuilder.DropIndex(
                name: "IX_FiberMaterials_user_id",
                table: "FiberMaterials");

            migrationBuilder.DropIndex(
                name: "IX_FiberInventoryTransactions_user_id",
                table: "FiberInventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_FiberClients_user_id",
                table: "FiberClients");
        }
    }
}
