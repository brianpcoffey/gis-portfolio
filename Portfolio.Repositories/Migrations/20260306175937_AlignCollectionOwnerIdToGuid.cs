using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portfolio.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AlignCollectionOwnerIdToGuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId_New",
                table: "Collections",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty);


            migrationBuilder.Sql(@"
                UPDATE c
                SET c.[OwnerId_New] = COALESCE(
                    (SELECT TOP 1 uc.[UserId]
                     FROM [UserClaims] uc
                     WHERE uc.[ClaimValue] = c.[OwnerId]),
                    NEWID()
                )
                FROM [Collections] c;
            ");

            migrationBuilder.DropIndex(
                name: "IX_Collections_OwnerId_Name",
                table: "Collections");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Collections");

            migrationBuilder.RenameColumn(
                name: "OwnerId_New",
                table: "Collections",
                newName: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_OwnerId_Name",
                table: "Collections",
                columns: new[] { "OwnerId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Collections_OwnerId_Name",
                table: "Collections");

            migrationBuilder.AlterColumn<string>(
                name: "OwnerId",
                table: "Collections",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_OwnerId_Name",
                table: "Collections",
                columns: new[] { "OwnerId", "Name" },
                unique: true);
        }
    }
}