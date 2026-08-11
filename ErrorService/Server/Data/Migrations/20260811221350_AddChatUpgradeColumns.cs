using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatUpgradeColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF COL_LENGTH('dbo.SiteSettings', 'ShowFeaturedProducts') IS NOT NULL ALTER TABLE dbo.SiteSettings DROP COLUMN ShowFeaturedProducts;");
            migrationBuilder.AddColumn<bool>(
                name: "EnableTelegramChat",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ChatAiApiKey",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChatAiApiUrl",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChatAiModel",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChatAiProvider",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChatAiSystemPrompt",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChatAutoMessageSeconds",
                table: "SiteSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ChatEnableAiAssistant",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ChatEnableAutoMessage",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ChatPhoneRequired",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ChatWelcomeMessage",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EitaaBotToken",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EitaaGroupId",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableBaleChat",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableEitaaChat",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TelegramBotToken",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramGroupId",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserPhone",
                table: "ChatSessions",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EditedAt",
                table: "ChatMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ChatMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReplyToId",
                table: "ChatMessages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CannedResponses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsShared = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CannedResponses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatFaqEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Question = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Answer = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Keywords = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatFaqEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CannedResponses_IsShared",
                table: "CannedResponses",
                column: "IsShared");

            migrationBuilder.CreateIndex(
                name: "IX_ChatFaqEntries_IsEnabled",
                table: "ChatFaqEntries",
                column: "IsEnabled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CannedResponses");

            migrationBuilder.DropTable(
                name: "ChatFaqEntries");

            migrationBuilder.DropColumn(
                name: "ChatAiApiKey",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "ChatAiApiUrl",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "ChatAiModel",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "ChatAiProvider",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "ChatAiSystemPrompt",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "ChatAutoMessageSeconds",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "ChatEnableAiAssistant",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "ChatEnableAutoMessage",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "ChatPhoneRequired",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "ChatWelcomeMessage",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EitaaBotToken",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EitaaGroupId",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableBaleChat",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableEitaaChat",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "TelegramBotToken",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "TelegramGroupId",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "UserPhone",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "EditedAt",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "ReplyToId",
                table: "ChatMessages");

            migrationBuilder.Sql("IF COL_LENGTH('dbo.SiteSettings', 'EnableTelegramChat') IS NOT NULL ALTER TABLE dbo.SiteSettings DROP COLUMN EnableTelegramChat;");
            migrationBuilder.AddColumn<bool>(
                name: "ShowFeaturedProducts",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
