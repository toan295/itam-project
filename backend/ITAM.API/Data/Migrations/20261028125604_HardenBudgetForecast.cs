using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAM.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardenBudgetForecast : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "DepartmentExpansionPlans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "DepartmentExpansionPlans",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "InputsHash",
                table: "BudgetForecasts",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "InputsJson",
                table: "BudgetForecasts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "BudgetForecasts",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AssetCategoryReferencePrices",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "AssetCategoryReferencePrices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "AssetCategoryReferencePrices",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AssetCategoryDeviceRatios",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "AssetCategoryDeviceRatios",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "AssetCategoryDeviceRatios",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "DepartmentExpansionPlans");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "DepartmentExpansionPlans");

            migrationBuilder.DropColumn(
                name: "InputsHash",
                table: "BudgetForecasts");

            migrationBuilder.DropColumn(
                name: "InputsJson",
                table: "BudgetForecasts");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "BudgetForecasts");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AssetCategoryReferencePrices");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "AssetCategoryReferencePrices");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "AssetCategoryReferencePrices");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AssetCategoryDeviceRatios");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "AssetCategoryDeviceRatios");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "AssetCategoryDeviceRatios");
        }
    }
}
