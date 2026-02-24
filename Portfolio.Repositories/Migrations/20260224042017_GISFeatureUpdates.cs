using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portfolio.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class GISFeatureUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Comment",
                table: "UserNotes",
                newName: "Note");

            migrationBuilder.AlterColumn<string>(
                name: "LayerId",
                table: "SavedFeatures",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "FeatureId",
                table: "SavedFeatures",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "SavedFeatures",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModified",
                table: "SavedFeatures",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_SavedFeatures_LayerId_FeatureId",
                table: "SavedFeatures",
                columns: new[] { "LayerId", "FeatureId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SavedFeatures_LayerId_FeatureId",
                table: "SavedFeatures");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "SavedFeatures");

            migrationBuilder.DropColumn(
                name: "LastModified",
                table: "SavedFeatures");

            migrationBuilder.RenameColumn(
                name: "Note",
                table: "UserNotes",
                newName: "Comment");

            migrationBuilder.AlterColumn<string>(
                name: "LayerId",
                table: "SavedFeatures",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "FeatureId",
                table: "SavedFeatures",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
