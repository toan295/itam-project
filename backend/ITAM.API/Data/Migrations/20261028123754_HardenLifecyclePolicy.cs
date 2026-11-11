using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAM.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardenLifecyclePolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AssetCategoryLifecyclePolicies",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "AssetCategoryLifecyclePolicies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "AssetCategoryLifecyclePolicies",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AssetCategoryLifecyclePolicies");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "AssetCategoryLifecyclePolicies");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "AssetCategoryLifecyclePolicies");
        }
    }
}
