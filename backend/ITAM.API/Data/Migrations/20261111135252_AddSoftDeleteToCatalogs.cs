using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAM.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteToCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Index riêng cho khoá ngoại DepartmentId phải có TRƯỚC khi bỏ chỉ mục (DepartmentId, FullName) đang làm chỗ dựa cho khoá ngoại.
            migrationBuilder.CreateIndex(
                name: "IX_Employees_DepartmentId",
                table: "Employees",
                column: "DepartmentId");

            migrationBuilder.DropIndex(
                name: "IX_SoftwareLicenses_LicenseKey",
                table: "SoftwareLicenses");

            migrationBuilder.DropIndex(
                name: "IX_Employees_DepartmentId_FullName",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_EmployeeCode",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_DisposalStatuses_Code",
                table: "DisposalStatuses");

            migrationBuilder.DropIndex(
                name: "IX_DisposalStatuses_Name",
                table: "DisposalStatuses");

            migrationBuilder.DropIndex(
                name: "IX_Departments_Name",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_AssetCategories_Name",
                table: "AssetCategories");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "SoftwareLicenses",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "SoftwareLicenses",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Employees",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Employees",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "DisposalStatuses",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "DisposalStatuses",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Departments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Departments",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "AssetCategories",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AssetCategories",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ActiveLicenseKey",
                table: "SoftwareLicenses",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true,
                computedColumnSql: "CASE WHEN `IsDeleted` = 0 THEN `LicenseKey` END",
                stored: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ActiveDeptFullName",
                table: "Employees",
                type: "varchar(120)",
                maxLength: 120,
                nullable: true,
                computedColumnSql: "CASE WHEN `IsDeleted` = 0 THEN CONCAT(`DepartmentId`, '|', `FullName`) END",
                stored: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ActiveEmployeeCode",
                table: "Employees",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                computedColumnSql: "CASE WHEN `IsDeleted` = 0 THEN `EmployeeCode` END",
                stored: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ActiveCode",
                table: "DisposalStatuses",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                computedColumnSql: "CASE WHEN `IsDeleted` = 0 THEN `Code` END",
                stored: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ActiveName",
                table: "DisposalStatuses",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                computedColumnSql: "CASE WHEN `IsDeleted` = 0 THEN `Name` END",
                stored: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ActiveName",
                table: "Departments",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                computedColumnSql: "CASE WHEN `IsDeleted` = 0 THEN `Name` END",
                stored: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ActiveName",
                table: "AssetCategories",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                computedColumnSql: "CASE WHEN `IsDeleted` = 0 THEN `Name` END",
                stored: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "DisposalStatuses",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DeletedAt", "IsDeleted" },
                values: new object[] { null, false });

            migrationBuilder.UpdateData(
                table: "DisposalStatuses",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "DeletedAt", "IsDeleted" },
                values: new object[] { null, false });

            migrationBuilder.UpdateData(
                table: "DisposalStatuses",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "DeletedAt", "IsDeleted" },
                values: new object[] { null, false });

            migrationBuilder.UpdateData(
                table: "DisposalStatuses",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "DeletedAt", "IsDeleted" },
                values: new object[] { null, false });

            migrationBuilder.UpdateData(
                table: "DisposalStatuses",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "DeletedAt", "IsDeleted" },
                values: new object[] { null, false });

            migrationBuilder.CreateIndex(
                name: "IX_SoftwareLicenses_ActiveLicenseKey",
                table: "SoftwareLicenses",
                column: "ActiveLicenseKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ActiveDeptFullName",
                table: "Employees",
                column: "ActiveDeptFullName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ActiveEmployeeCode",
                table: "Employees",
                column: "ActiveEmployeeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisposalStatuses_ActiveCode",
                table: "DisposalStatuses",
                column: "ActiveCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisposalStatuses_ActiveName",
                table: "DisposalStatuses",
                column: "ActiveName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_ActiveName",
                table: "Departments",
                column: "ActiveName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetCategories_ActiveName",
                table: "AssetCategories",
                column: "ActiveName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SoftwareLicenses_ActiveLicenseKey",
                table: "SoftwareLicenses");

            migrationBuilder.DropIndex(
                name: "IX_Employees_ActiveDeptFullName",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_ActiveEmployeeCode",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_DisposalStatuses_ActiveCode",
                table: "DisposalStatuses");

            migrationBuilder.DropIndex(
                name: "IX_DisposalStatuses_ActiveName",
                table: "DisposalStatuses");

            migrationBuilder.DropIndex(
                name: "IX_Departments_ActiveName",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_AssetCategories_ActiveName",
                table: "AssetCategories");

            migrationBuilder.DropColumn(
                name: "ActiveLicenseKey",
                table: "SoftwareLicenses");

            migrationBuilder.DropColumn(
                name: "ActiveDeptFullName",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ActiveEmployeeCode",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ActiveCode",
                table: "DisposalStatuses");

            migrationBuilder.DropColumn(
                name: "ActiveName",
                table: "DisposalStatuses");

            migrationBuilder.DropColumn(
                name: "ActiveName",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "ActiveName",
                table: "AssetCategories");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "SoftwareLicenses");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "SoftwareLicenses");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "DisposalStatuses");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "DisposalStatuses");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "AssetCategories");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AssetCategories");

            migrationBuilder.CreateIndex(
                name: "IX_SoftwareLicenses_LicenseKey",
                table: "SoftwareLicenses",
                column: "LicenseKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DepartmentId_FullName",
                table: "Employees",
                columns: new[] { "DepartmentId", "FullName" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_Employees_DepartmentId",
                table: "Employees");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_EmployeeCode",
                table: "Employees",
                column: "EmployeeCode",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Name",
                table: "Departments",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetCategories_Name",
                table: "AssetCategories",
                column: "Name",
                unique: true);
        }
    }
}
