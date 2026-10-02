using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    WorkshopId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsBroadcast = table.Column<bool>(type: "bit", nullable: false),
                    ActionUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDeliveries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NotificationId = table.Column<long>(type: "bigint", nullable: false),
                    RecipientId = table.Column<long>(type: "bigint", nullable: true),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExternalMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationDeliveries_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationRecipients",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NotificationId = table.Column<long>(type: "bigint", nullable: false),
                    AppUserId = table.Column<int>(type: "int", nullable: true),
                    WorkshopUserId = table.Column<int>(type: "int", nullable: true),
                    WorkshopCustomerId = table.Column<int>(type: "int", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    HiddenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationRecipients_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_NotificationId",
                table: "NotificationDeliveries",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_Status_LastAttemptAt",
                table: "NotificationDeliveries",
                columns: new[] { "Status", "LastAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRecipients_AppUserId_IsRead_HiddenAt",
                table: "NotificationRecipients",
                columns: new[] { "AppUserId", "IsRead", "HiddenAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRecipients_NotificationId",
                table: "NotificationRecipients",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedAt",
                table: "Notifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_IdempotencyKey",
                table: "Notifications",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_WorkshopId_CreatedAt",
                table: "Notifications",
                columns: new[] { "WorkshopId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationDeliveries");

            migrationBuilder.DropTable(
                name: "NotificationRecipients");

            migrationBuilder.DropTable(
                name: "Notifications");

        }
    }
}
