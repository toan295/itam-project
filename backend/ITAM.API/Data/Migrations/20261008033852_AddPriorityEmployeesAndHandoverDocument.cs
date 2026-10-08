using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAM.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPriorityEmployeesAndHandoverDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "Priority",
                table: "MaintenanceTickets",
                type: "tinyint unsigned",
                nullable: false,
                // Phiếu đã có trước migration coi như mức "Bình thường" (Normal = 1), không phải "Thấp" (0).
                defaultValue: (byte)1);

            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "AssetAllocations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HandedOverByUserId",
                table: "AssetAllocations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HandoverCondition",
                table: "AssetAllocations",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Tốt")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "HandoverLocation",
                table: "AssetAllocations",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "HandoverReason",
                table: "AssetAllocations",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ReceivedByUserId",
                table: "AssetAllocations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EmployeeCode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FullName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employees_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTickets_Priority",
                table: "MaintenanceTickets",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_AssetAllocations_EmployeeId",
                table: "AssetAllocations",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetAllocations_HandedOverByUserId",
                table: "AssetAllocations",
                column: "HandedOverByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetAllocations_ReceivedByUserId",
                table: "AssetAllocations",
                column: "ReceivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DepartmentId_FullName",
                table: "Employees",
                columns: new[] { "DepartmentId", "FullName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_EmployeeCode",
                table: "Employees",
                column: "EmployeeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_FullName",
                table: "Employees",
                column: "FullName");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetAllocations_Employees_EmployeeId",
                table: "AssetAllocations",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssetAllocations_Users_HandedOverByUserId",
                table: "AssetAllocations",
                column: "HandedOverByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssetAllocations_Users_ReceivedByUserId",
                table: "AssetAllocations",
                column: "ReceivedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetAllocations_Employees_EmployeeId",
                table: "AssetAllocations");

            migrationBuilder.DropForeignKey(
                name: "FK_AssetAllocations_Users_HandedOverByUserId",
                table: "AssetAllocations");

            migrationBuilder.DropForeignKey(
                name: "FK_AssetAllocations_Users_ReceivedByUserId",
                table: "AssetAllocations");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceTickets_Priority",
                table: "MaintenanceTickets");

            migrationBuilder.DropIndex(
                name: "IX_AssetAllocations_EmployeeId",
                table: "AssetAllocations");

            migrationBuilder.DropIndex(
                name: "IX_AssetAllocations_HandedOverByUserId",
                table: "AssetAllocations");

            migrationBuilder.DropIndex(
                name: "IX_AssetAllocations_ReceivedByUserId",
                table: "AssetAllocations");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "MaintenanceTickets");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "AssetAllocations");

            migrationBuilder.DropColumn(
                name: "HandedOverByUserId",
                table: "AssetAllocations");

            migrationBuilder.DropColumn(
                name: "HandoverCondition",
                table: "AssetAllocations");

            migrationBuilder.DropColumn(
                name: "HandoverLocation",
                table: "AssetAllocations");

            migrationBuilder.DropColumn(
                name: "HandoverReason",
                table: "AssetAllocations");

            migrationBuilder.DropColumn(
                name: "ReceivedByUserId",
                table: "AssetAllocations");
        }
    }
}
