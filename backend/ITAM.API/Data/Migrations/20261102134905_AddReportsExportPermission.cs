using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAM.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReportsExportPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Quyền mới cho vai trò ĐÃ CÓ ma trận: chỉ Admin IT và Manager, chỉ khi chưa có (xem AddDashboardPermission).
            foreach (var role in new[] { "Admin IT", "Manager" })
            {
                migrationBuilder.Sql($@"INSERT INTO `RolePermissions` (`RoleId`, `PermissionCode`)
SELECT r.`Id`, 'reports.export' FROM `Roles` r
WHERE r.`Name` = '{role}'
  AND NOT EXISTS (SELECT 1 FROM `RolePermissions` rp WHERE rp.`RoleId` = r.`Id` AND rp.`PermissionCode` = 'reports.export');");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM `RolePermissions` WHERE `PermissionCode` = 'reports.export';");
        }
    }
}
