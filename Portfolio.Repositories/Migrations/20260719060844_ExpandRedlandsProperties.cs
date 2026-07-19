using Microsoft.EntityFrameworkCore.Migrations;
using Portfolio.Repositories.Seed;

#nullable disable

namespace Portfolio.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class ExpandRedlandsProperties : Migration
    {
        // Column order used by the seed InsertData below. EF maps by name, so order
        // only needs to be internally consistent between Columns and each Values row.
        private static readonly string[] SeedColumns =
        [
            "Id", "BrokeredBy", "Status", "Price", "Bedrooms", "Bathrooms", "AcreLot", "LotSqft",
            "PropertyType", "GarageSpaces", "HasPool", "Stories", "DaysOnMarket",
            "Street", "City", "State", "ZipCode", "Latitude", "Longitude",
            "HoaFee", "PropertyTax", "Utilities",
            "SchoolRating", "CrimeScore", "Walkability", "TransitAccess", "AmenitiesScore",
            "CommuteMin", "YearBuilt", "LastRenovation",
            "RoofCondition", "AcCondition", "PlumbingCondition", "ElectricalCondition", "FloorPlanScore",
            "FutureAppreciation", "ResalePotential", "FloodRisk", "NoiseLevel"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DaysOnMarket",
                table: "Properties",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GarageSpaces",
                table: "Properties",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "HasPool",
                table: "Properties",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PropertyType",
                table: "Properties",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Single Family");

            migrationBuilder.AddColumn<int>(
                name: "Stories",
                table: "Properties",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Replace the legacy demo listings with the curated Redlands dataset.
            // Existing rows were seeded outside migrations and nothing references
            // Properties by FK, so a clean delete-and-reseed is safe and deterministic.
            migrationBuilder.Sql("DELETE FROM \"Properties\";");

            foreach (var p in RedlandsPropertySeedData.All)
            {
                migrationBuilder.InsertData(
                    table: "Properties",
                    columns: SeedColumns,
                    values:
                    [
                        p.Id, p.BrokeredBy, p.Status, p.Price, p.Bedrooms, p.Bathrooms, p.AcreLot, p.LotSqft,
                        p.PropertyType, p.GarageSpaces, p.HasPool, p.Stories, p.DaysOnMarket,
                        p.Street, p.City, p.State, p.ZipCode, p.Latitude, p.Longitude,
                        p.HoaFee, p.PropertyTax, p.Utilities,
                        p.SchoolRating, p.CrimeScore, p.Walkability, p.TransitAccess, p.AmenitiesScore,
                        p.CommuteMin, p.YearBuilt, (object)p.LastRenovation,
                        p.RoofCondition, p.AcCondition, p.PlumbingCondition, p.ElectricalCondition, p.FloorPlanScore,
                        p.FutureAppreciation, p.ResalePotential, p.FloodRisk, p.NoiseLevel
                    ]);
            }

            // Realign the identity sequence so future inserts don't collide with the
            // explicit IDs we just inserted.
            migrationBuilder.Sql(
                "SELECT setval(pg_get_serial_sequence('\"Properties\"', 'Id'), " +
                "(SELECT COALESCE(MAX(\"Id\"), 1) FROM \"Properties\"), true);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The pre-migration demo rows cannot be restored, so simply drop the added
            // columns; the curated listings remain (minus the new feature columns).
            migrationBuilder.DropColumn(
                name: "DaysOnMarket",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "GarageSpaces",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "HasPool",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "PropertyType",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "Stories",
                table: "Properties");
        }
    }
}
