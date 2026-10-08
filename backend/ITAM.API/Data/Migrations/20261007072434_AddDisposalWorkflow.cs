using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ITAM.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDisposalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DisposalStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Color = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsSystem = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisposalStatuses", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DisposalRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AssetId = table.Column<int>(type: "int", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    SubStatusId = table.Column<int>(type: "int", nullable: true),
                    InspectionNote = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InspectedByUserId = table.Column<int>(type: "int", nullable: false),
                    InspectedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Reason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DisposalMethod = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProposedByUserId = table.Column<int>(type: "int", nullable: true),
                    ProposedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReviewNote = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CompletionNote = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CompletedByUserId = table.Column<int>(type: "int", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisposalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisposalRequests_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DisposalRequests_DisposalStatuses_StatusId",
                        column: x => x.StatusId,
                        principalTable: "DisposalStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DisposalRequests_DisposalStatuses_SubStatusId",
                        column: x => x.SubStatusId,
                        principalTable: "DisposalStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DisposalRequests_Users_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DisposalRequests_Users_InspectedByUserId",
                        column: x => x.InspectedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DisposalRequests_Users_ProposedByUserId",
                        column: x => x.ProposedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DisposalRequests_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "DisposalStatuses",
                columns: new[] { "Id", "Code", "Color", "Description", "IsSystem", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, "Inspected", "info", "Technician đã kiểm tra tài sản", true, "Đã kiểm tra", 1 },
                    { 2, "Proposed", "warning", "Technician đề xuất thanh lý, chờ Manager duyệt", true, "Đã đề xuất", 2 },
                    { 3, "Approved", "success", "Manager đã duyệt, chờ Admin IT thực hiện thanh lý", true, "Đã duyệt", 3 },
                    { 4, "Rejected", "danger", "Manager từ chối đề xuất", true, "Từ chối", 4 },
                    { 5, "Completed", "slate", "Admin IT đã thanh lý, tài sản chuyển sang Đã thanh lý", true, "Hoàn tất", 5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DisposalRequests_AssetId",
                table: "DisposalRequests",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_DisposalRequests_CompletedByUserId",
                table: "DisposalRequests",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DisposalRequests_InspectedByUserId",
                table: "DisposalRequests",
                column: "InspectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DisposalRequests_ProposedByUserId",
                table: "DisposalRequests",
                column: "ProposedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DisposalRequests_ReviewedByUserId",
                table: "DisposalRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DisposalRequests_StatusId",
                table: "DisposalRequests",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_DisposalRequests_SubStatusId",
                table: "DisposalRequests",
                column: "SubStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_DisposalStatuses_Code",
                table: "DisposalStatuses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisposalStatuses_Name",
                table: "DisposalStatuses",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DisposalRequests");

            migrationBuilder.DropTable(
                name: "DisposalStatuses");
        }
    }
}
