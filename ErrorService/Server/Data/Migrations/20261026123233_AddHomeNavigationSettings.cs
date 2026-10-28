using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeNavigationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableBreadcrumb",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableHomeStats",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableMegaMenu",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowOrderCountStat",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableBreadcrumb",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableHomeStats",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableMegaMenu",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "ShowOrderCountStat",
                table: "SiteSettings");
        }
    }
}
