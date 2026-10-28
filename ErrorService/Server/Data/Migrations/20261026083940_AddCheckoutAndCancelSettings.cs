using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutAndCancelSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CustomerCancelRequireReason",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerCancelWindowHours",
                table: "SiteSettings",
                type: "int",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.AddColumn<bool>(
                name: "EnableCustomerOrderCancel",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableStructuredAddress",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequirePostalCode",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequireProvinceCity",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerCancelRequireReason",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "CustomerCancelWindowHours",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableCustomerOrderCancel",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableStructuredAddress",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "RequirePostalCode",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "RequireProvinceCity",
                table: "SiteSettings");
        }
    }
}
