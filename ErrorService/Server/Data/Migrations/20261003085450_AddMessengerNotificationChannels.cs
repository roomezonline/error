using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMessengerNotificationChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableBaleNotifications",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableEitaaNotifications",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableTelegramNotifications",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "Channels",
                table: "NotificationRules",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "MessengerEndpoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    AppUserId = table.Column<int>(type: "int", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExternalUserName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessengerEndpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessengerLinkCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    AppUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessengerLinkCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserNotificationSettings",
                columns: table => new
                {
                    AppUserId = table.Column<int>(type: "int", nullable: false),
                    Channels = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotificationSettings", x => x.AppUserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_RecipientId",
                table: "NotificationDeliveries",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_MessengerEndpoints_Channel_AppUserId",
                table: "MessengerEndpoints",
                columns: new[] { "Channel", "AppUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessengerEndpoints_Channel_ExternalId",
                table: "MessengerEndpoints",
                columns: new[] { "Channel", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessengerLinkCodes_AppUserId_Channel",
                table: "MessengerLinkCodes",
                columns: new[] { "AppUserId", "Channel" });

            migrationBuilder.CreateIndex(
                name: "IX_MessengerLinkCodes_Code",
                table: "MessengerLinkCodes",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationDeliveries_NotificationRecipients_RecipientId",
                table: "NotificationDeliveries",
                column: "RecipientId",
                principalTable: "NotificationRecipients",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NotificationDeliveries_NotificationRecipients_RecipientId",
                table: "NotificationDeliveries");

            migrationBuilder.DropTable(
                name: "MessengerEndpoints");

            migrationBuilder.DropTable(
                name: "MessengerLinkCodes");

            migrationBuilder.DropTable(
                name: "UserNotificationSettings");

            migrationBuilder.DropIndex(
                name: "IX_NotificationDeliveries_RecipientId",
                table: "NotificationDeliveries");

            migrationBuilder.DropColumn(
                name: "EnableBaleNotifications",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableEitaaNotifications",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableTelegramNotifications",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "Channels",
                table: "NotificationRules");
        }
    }
}
