using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceProductsAndErrorCodesWithTechnicalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompatibilityInfo",
                table: "Products",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DatasheetUrl",
                table: "Products",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureSymptoms",
                table: "Products",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelatedProductIds",
                table: "Products",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelNames",
                table: "ErrorCodes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelatedProductIds",
                table: "ErrorCodes",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompatibilityInfo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DatasheetUrl",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "FailureSymptoms",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RelatedProductIds",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ModelNames",
                table: "ErrorCodes");

            migrationBuilder.DropColumn(
                name: "RelatedProductIds",
                table: "ErrorCodes");
        }
    }
}
