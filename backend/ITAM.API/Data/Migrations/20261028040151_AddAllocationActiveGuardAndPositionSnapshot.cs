using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAM.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAllocationActiveGuardAndPositionSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActiveAssetId",
                table: "AssetAllocations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientPosition",
                table: "AssetAllocations",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Dữ liệu cũ: chức danh lúc giao = chức danh hiện tại của nhân viên (không còn dữ liệu nào tốt hơn).
            migrationBuilder.Sql(@"
UPDATE AssetAllocations a
JOIN Employees e ON e.Id = a.EmployeeId
SET a.RecipientPosition = e.Position
WHERE a.RecipientPosition IS NULL;");

            // Mỗi tài sản đánh dấu phân bổ đang mở MỚI NHẤT; nếu trước đó đã lỡ có nhiều phân bổ mở trên cùng một tài sản thì các
            // dòng còn lại giữ NULL (để chỉ mục duy nhất không làm migration thất bại) — nên rà soát và thu hồi tay.
            migrationBuilder.Sql(@"
UPDATE AssetAllocations r
JOIN (
    SELECT AssetId, MAX(Id) AS LatestId
    FROM AssetAllocations
    WHERE ReturnedDate IS NULL AND Status = 0
    GROUP BY AssetId
) x ON x.LatestId = r.Id
SET r.ActiveAssetId = r.AssetId;");

            migrationBuilder.CreateIndex(
                name: "IX_AssetAllocations_ActiveAssetId",
                table: "AssetAllocations",
                column: "ActiveAssetId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AssetAllocations_ActiveAssetId",
                table: "AssetAllocations");

            migrationBuilder.DropColumn(
                name: "ActiveAssetId",
                table: "AssetAllocations");

            migrationBuilder.DropColumn(
                name: "RecipientPosition",
                table: "AssetAllocations");
        }
    }
}
