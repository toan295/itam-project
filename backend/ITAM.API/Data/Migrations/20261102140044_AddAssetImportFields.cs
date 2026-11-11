using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAM.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetImportFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorsJson",
                table: "ImportLogs",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "FileSha256",
                table: "ImportLogs",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "ImportLogs",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "skip-errors")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ImportLogs_FileSha256",
                table: "ImportLogs",
                column: "FileSha256");

            // Quyền mới chỉ cho Admin IT (import hàng loạt), chỉ khi chưa có.
            migrationBuilder.Sql(@"INSERT INTO `RolePermissions` (`RoleId`, `PermissionCode`)
SELECT r.`Id`, 'assets.import' FROM `Roles` r
WHERE r.`Name` = 'Admin IT'
  AND NOT EXISTS (SELECT 1 FROM `RolePermissions` rp WHERE rp.`RoleId` = r.`Id` AND rp.`PermissionCode` = 'assets.import');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM `RolePermissions` WHERE `PermissionCode` = 'assets.import';");

            migrationBuilder.DropIndex(
                name: "IX_ImportLogs_FileSha256",
                table: "ImportLogs");

            migrationBuilder.DropColumn(
                name: "ErrorsJson",
                table: "ImportLogs");

            migrationBuilder.DropColumn(
                name: "FileSha256",
                table: "ImportLogs");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "ImportLogs");
        }
    }
}
