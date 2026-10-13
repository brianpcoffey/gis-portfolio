using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portfolio.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class UniqueOrderNumberPerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FiberOrders_user_id_order_number",
                table: "FiberOrders",
                columns: new[] { "user_id", "order_number" },
                unique: true,
                filter: "\"order_number\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FiberOrders_user_id_order_number",
                table: "FiberOrders");
        }
    }
}
