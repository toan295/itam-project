using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAM.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExpansionPlanningToBudgetForecasts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedExpansionBudget",
                table: "BudgetForecasts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedExpansionCount",
                table: "BudgetForecasts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ExpansionBreakdownJson",
                table: "BudgetForecasts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AssetCategoryDeviceRatios",
                columns: table => new
                {
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    DevicesPerPerson = table.Column<decimal>(type: "decimal(6,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetCategoryDeviceRatios", x => x.CategoryId);
                    table.ForeignKey(
                        name: "FK_AssetCategoryDeviceRatios_AssetCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "AssetCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DepartmentExpansionPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Year = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    AdditionalHeadcount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentExpansionPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepartmentExpansionPlans_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentExpansionPlans_DepartmentId",
                table: "DepartmentExpansionPlans",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentExpansionPlans_Year_DepartmentId",
                table: "DepartmentExpansionPlans",
                columns: new[] { "Year", "DepartmentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetCategoryDeviceRatios");

            migrationBuilder.DropTable(
                name: "DepartmentExpansionPlans");

            migrationBuilder.DropColumn(
                name: "EstimatedExpansionBudget",
                table: "BudgetForecasts");

            migrationBuilder.DropColumn(
                name: "EstimatedExpansionCount",
                table: "BudgetForecasts");

            migrationBuilder.DropColumn(
                name: "ExpansionBreakdownJson",
                table: "BudgetForecasts");
        }
    }
}
