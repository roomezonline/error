using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VisitSessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    VisitDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VisitorId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    IdentityKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Ip = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Referrer = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EntryPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    LastPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PageViews = table.Column<int>(type: "int", nullable: false),
                    IsBot = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitSessions", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 10, 2, 11, 48, 8, 881, DateTimeKind.Unspecified).AddTicks(8786), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 10, 2, 11, 48, 8, 881, DateTimeKind.Unspecified).AddTicks(8852), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 10, 2, 11, 48, 8, 881, DateTimeKind.Unspecified).AddTicks(8853), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 10, 2, 11, 48, 8, 881, DateTimeKind.Unspecified).AddTicks(8854), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 10, 2, 11, 48, 8, 881, DateTimeKind.Unspecified).AddTicks(8856), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_VisitSessions_EndedAtUtc",
                table: "VisitSessions",
                column: "EndedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_VisitSessions_IdentityKey",
                table: "VisitSessions",
                column: "IdentityKey");

            migrationBuilder.CreateIndex(
                name: "IX_VisitSessions_StartedAtUtc",
                table: "VisitSessions",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_VisitSessions_VisitDate",
                table: "VisitSessions",
                column: "VisitDate");

            migrationBuilder.CreateIndex(
                name: "IX_VisitSessions_VisitDate_IsBot",
                table: "VisitSessions",
                columns: new[] { "VisitDate", "IsBot" });

            migrationBuilder.CreateIndex(
                name: "IX_VisitSessions_VisitorId",
                table: "VisitSessions",
                column: "VisitorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VisitSessions");

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
        }
    }
}
