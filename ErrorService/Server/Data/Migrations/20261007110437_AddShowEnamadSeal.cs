using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShowEnamadSeal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowEnamadSeal",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: true)
                .Annotation("SqlServer:DefaultValue", true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowEnamadSeal",
                table: "SiteSettings");
        }
    }
}
