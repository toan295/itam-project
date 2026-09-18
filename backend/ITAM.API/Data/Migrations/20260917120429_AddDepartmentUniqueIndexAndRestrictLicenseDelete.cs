using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITAM.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentUniqueIndexAndRestrictLicenseDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetSoftwareLicenses_SoftwareLicenses_LicenseId",
                table: "AssetSoftwareLicenses");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Name",
                table: "Departments",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AssetSoftwareLicenses_SoftwareLicenses_LicenseId",
                table: "AssetSoftwareLicenses",
                column: "LicenseId",
                principalTable: "SoftwareLicenses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetSoftwareLicenses_SoftwareLicenses_LicenseId",
                table: "AssetSoftwareLicenses");

            migrationBuilder.DropIndex(
                name: "IX_Departments_Name",
                table: "Departments");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetSoftwareLicenses_SoftwareLicenses_LicenseId",
                table: "AssetSoftwareLicenses",
                column: "LicenseId",
                principalTable: "SoftwareLicenses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
