using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatAiGating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChatAiDailyLimitOverride",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ChatAiDisabled",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ChatAiDailyLimit",
                table: "SiteSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ChatAiGuestEnabled",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "ChatSessions",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChatAiDailyLimitOverride",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ChatAiDisabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ChatAiDailyLimit",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "ChatAiGuestEnabled",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ChatSessions");
        }
    }
}
