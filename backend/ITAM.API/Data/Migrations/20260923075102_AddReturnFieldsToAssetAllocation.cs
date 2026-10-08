using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAM.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnFieldsToAssetAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "ReturnCondition",
                table: "AssetAllocations",
                type: "tinyint unsigned",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnNote",
                table: "AssetAllocations",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReturnCondition",
                table: "AssetAllocations");

            migrationBuilder.DropColumn(
                name: "ReturnNote",
                table: "AssetAllocations");
        }
    }
}
