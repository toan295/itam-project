using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAM.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReferencePriceAndForecastBreakdown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BreakdownJson",
                table: "BudgetForecasts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "GeneratedAt",
                table: "BudgetForecasts",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AssetCategoryReferencePrices",
                columns: table => new
                {
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetCategoryReferencePrices", x => x.CategoryId);
                    table.ForeignKey(
                        name: "FK_AssetCategoryReferencePrices_AssetCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "AssetCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetCategoryReferencePrices");

            migrationBuilder.DropColumn(
                name: "BreakdownJson",
                table: "BudgetForecasts");

            migrationBuilder.DropColumn(
                name: "GeneratedAt",
                table: "BudgetForecasts");
        }
    }
}
