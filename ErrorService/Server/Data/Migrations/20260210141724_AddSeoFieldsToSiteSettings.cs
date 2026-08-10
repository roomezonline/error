using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSeoFieldsToSiteSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultOgImage",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoogleAnalyticsTag",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMaintenanceMode",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SeoDescription",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeoKeywords",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeoTitle",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultOgImage",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "GoogleAnalyticsTag",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "IsMaintenanceMode",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "SeoDescription",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "SeoKeywords",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "SeoTitle",
                table: "SiteSettings");
        }
    }
}
