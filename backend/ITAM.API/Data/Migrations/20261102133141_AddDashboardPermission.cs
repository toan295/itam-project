using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAM.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Quyền mới cho các vai trò ĐÃ CÓ ma trận (PermissionSeeder chỉ khởi tạo mặc định khi ma trận trống). Chỉ thêm cho Admin IT và
            // Manager, và chỉ khi chưa có: chạy lại không nhân đôi; Admin gỡ quyền sau đó thì không bị migration đặt lại.
            foreach (var role in new[] { "Admin IT", "Manager" })
            {
                migrationBuilder.Sql($@"INSERT INTO `RolePermissions` (`RoleId`, `PermissionCode`)
SELECT r.`Id`, 'dashboard.view' FROM `Roles` r
WHERE r.`Name` = '{role}'
  AND NOT EXISTS (SELECT 1 FROM `RolePermissions` rp WHERE rp.`RoleId` = r.`Id` AND rp.`PermissionCode` = 'dashboard.view');");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM `RolePermissions` WHERE `PermissionCode` = 'dashboard.view';");
        }
    }
}
