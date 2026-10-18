using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatAiControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChatAiBlockKeywords",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChatAiMaxQuestionChars",
                table: "SiteSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ChatAiMaxTokens",
                table: "SiteSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ChatAiSessionLimit",
                table: "SiteSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "ChatAiTemperature",
                table: "SiteSettings",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChatAiBlockKeywords",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "ChatAiMaxQuestionChars",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "ChatAiMaxTokens",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "ChatAiSessionLimit",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "ChatAiTemperature",
                table: "SiteSettings");
        }
    }
}
