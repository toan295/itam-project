using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAM.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class DisableBudgetForecast : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetCategoryDeviceRatios");

            migrationBuilder.DropTable(
                name: "AssetCategoryReferencePrices");

            migrationBuilder.DropTable(
                name: "BudgetForecasts");

            migrationBuilder.DropTable(
                name: "DepartmentExpansionPlans");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssetCategoryDeviceRatios",
                columns: table => new
                {
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    DevicesPerPerson = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
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
                name: "AssetCategoryReferencePrices",
                columns: table => new
                {
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
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

            migrationBuilder.CreateTable(
                name: "BudgetForecasts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    BreakdownJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EstimatedBudget = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EstimatedExpansionBudget = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EstimatedExpansionCount = table.Column<int>(type: "int", nullable: false),
                    EstimatedReplacementCount = table.Column<int>(type: "int", nullable: false),
                    ExpansionBreakdownJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeneratedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    InputsHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InputsJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Revision = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Year = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetForecasts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetForecasts_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DepartmentExpansionPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    AdditionalHeadcount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Year = table.Column<int>(type: "int", nullable: false)
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
                name: "IX_BudgetForecasts_DepartmentId",
                table: "BudgetForecasts",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetForecasts_Year_DepartmentId",
                table: "BudgetForecasts",
                columns: new[] { "Year", "DepartmentId" },
                unique: true);

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
    }
}
