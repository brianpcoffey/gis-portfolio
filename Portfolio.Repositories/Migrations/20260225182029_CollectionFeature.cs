using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portfolio.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class CollectionFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CollectionId",
                table: "SavedFeatures",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedFeatures_CollectionId",
                table: "SavedFeatures",
                column: "CollectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_SavedFeatures_Collections_CollectionId",
                table: "SavedFeatures",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SavedFeatures_Collections_CollectionId",
                table: "SavedFeatures");

            migrationBuilder.DropIndex(
                name: "IX_SavedFeatures_CollectionId",
                table: "SavedFeatures");

            migrationBuilder.DropColumn(
                name: "CollectionId",
                table: "SavedFeatures");
        }
    }
}
