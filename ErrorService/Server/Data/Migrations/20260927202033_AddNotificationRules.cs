using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Group = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: true),
                    TitleTemplate = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    BodyTemplate = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ActionUrlTemplate = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BroadcastToAdmins = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationRules", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 27, 20, 20, 32, 683, DateTimeKind.Unspecified).AddTicks(3532), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 27, 20, 20, 32, 683, DateTimeKind.Unspecified).AddTicks(3595), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 27, 20, 20, 32, 683, DateTimeKind.Unspecified).AddTicks(3596), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 27, 20, 20, 32, 683, DateTimeKind.Unspecified).AddTicks(3597), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 27, 20, 20, 32, 683, DateTimeKind.Unspecified).AddTicks(3599), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRules_EventType",
                table: "NotificationRules",
                column: "EventType",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationRules");

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 25, 18, 28, 31, 228, DateTimeKind.Unspecified).AddTicks(6291), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 25, 18, 28, 31, 228, DateTimeKind.Unspecified).AddTicks(6355), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 25, 18, 28, 31, 228, DateTimeKind.Unspecified).AddTicks(6357), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 25, 18, 28, 31, 228, DateTimeKind.Unspecified).AddTicks(6358), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 25, 18, 28, 31, 228, DateTimeKind.Unspecified).AddTicks(6360), new TimeSpan(0, 0, 0, 0, 0)));
        }
    }
}
