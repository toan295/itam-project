using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAM.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveImportLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportLogs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ImportedBy = table.Column<int>(type: "int", nullable: false),
                    ErrorsJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailedRows = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FileSha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ImportedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Mode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "skip-errors")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SuccessRows = table.Column<int>(type: "int", nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportLogs_Users_ImportedBy",
                        column: x => x.ImportedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ImportLogs_FileSha256",
                table: "ImportLogs",
                column: "FileSha256");

            migrationBuilder.CreateIndex(
                name: "IX_ImportLogs_ImportedBy",
                table: "ImportLogs",
                column: "ImportedBy");
        }
    }
}
