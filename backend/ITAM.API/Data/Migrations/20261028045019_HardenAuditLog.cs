using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAM.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardenAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorName",
                table: "AuditLogs",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ActorRole",
                table: "AuditLogs",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Detail",
                table: "AuditLogs",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "AuditLogs",
                type: "varchar(45)",
                maxLength: 45,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "AuditLogs",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Outcome",
                table: "AuditLogs",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Success")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RequestId",
                table: "AuditLogs",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RowHash",
                table: "AuditLogs",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "StatusCode",
                table: "AuditLogs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityName_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Outcome",
                table: "AuditLogs",
                column: "Outcome");

            // Đăng nhập thất bại là yêu cầu bị từ chối (trước đây không có cột kết quả nên ghi chung như sự kiện thường).
            migrationBuilder.Sql("UPDATE `AuditLogs` SET `Outcome` = 'Rejected', `StatusCode` = 401 WHERE `Action` = 'LoginFailed';");

            // Nhật ký chỉ được THÊM: mọi UPDATE/DELETE (kể cả từ công cụ SQL) bị chặn ở tầng database. Tạo SAU bước điền dữ liệu ở trên.
            migrationBuilder.Sql(
                "CREATE TRIGGER `trg_AuditLogs_no_update` BEFORE UPDATE ON `AuditLogs` FOR EACH ROW " +
                "SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'AuditLogs la nhat ky chi-them: khong duoc sua';");
            migrationBuilder.Sql(
                "CREATE TRIGGER `trg_AuditLogs_no_delete` BEFORE DELETE ON `AuditLogs` FOR EACH ROW " +
                "SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'AuditLogs la nhat ky chi-them: khong duoc xoa';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS `trg_AuditLogs_no_update`;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS `trg_AuditLogs_no_delete`;");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_EntityName_EntityId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Outcome",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ActorName",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ActorRole",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Detail",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Outcome",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "RowHash",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "StatusCode",
                table: "AuditLogs");
        }
    }
}
