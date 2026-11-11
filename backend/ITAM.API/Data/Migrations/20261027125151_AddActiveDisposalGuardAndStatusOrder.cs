using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAM.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveDisposalGuardAndStatusOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActiveAssetId",
                table: "DisposalRequests",
                type: "int",
                nullable: true);

            // Dữ liệu cũ: mỗi tài sản đánh dấu phiếu đang xử lý MỚI NHẤT (nếu trước đây đã lỡ có nhiều phiếu mở thì các phiếu
            // còn lại giữ NULL để chỉ mục duy nhất không làm migration thất bại; xử lý tay nếu có).
            migrationBuilder.Sql(@"
UPDATE DisposalRequests r
JOIN (
    SELECT dr.AssetId, MAX(dr.Id) AS LatestId
    FROM DisposalRequests dr
    JOIN DisposalStatuses s ON s.Id = dr.StatusId
    WHERE s.Code NOT IN ('Rejected', 'Completed')
    GROUP BY dr.AssetId
) x ON x.LatestId = r.Id
SET r.ActiveAssetId = r.AssetId;");

            // Thứ tự: 1–5 là các bước chính của luồng; trạng thái phụ do Admin thêm được đánh lại từ 6, giữ nguyên thứ tự tương đối.
            migrationBuilder.Sql(@"
SET @r := 5;
UPDATE DisposalStatuses SET SortOrder = (@r := @r + 1) WHERE IsSystem = 0 ORDER BY SortOrder, Id;");

            migrationBuilder.CreateIndex(
                name: "IX_DisposalRequests_ActiveAssetId",
                table: "DisposalRequests",
                column: "ActiveAssetId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DisposalRequests_ActiveAssetId",
                table: "DisposalRequests");

            migrationBuilder.DropColumn(
                name: "ActiveAssetId",
                table: "DisposalRequests");
        }
    }
}
