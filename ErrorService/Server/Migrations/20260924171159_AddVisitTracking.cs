using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyVisitStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Visits = table.Column<int>(type: "int", nullable: false),
                    Uniques = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyVisitStats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VisitLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VisitedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    VisitDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Ip = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    Path = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Referrer = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    VisitorId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    IsBot = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitLogs", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 24, 17, 11, 58, 788, DateTimeKind.Unspecified).AddTicks(4951), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 24, 17, 11, 58, 788, DateTimeKind.Unspecified).AddTicks(5015), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 24, 17, 11, 58, 788, DateTimeKind.Unspecified).AddTicks(5017), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 24, 17, 11, 58, 788, DateTimeKind.Unspecified).AddTicks(5018), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 24, 17, 11, 58, 788, DateTimeKind.Unspecified).AddTicks(5020), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_DailyVisitStats_Date",
                table: "DailyVisitStats",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VisitLogs_VisitDate",
                table: "VisitLogs",
                column: "VisitDate");

            migrationBuilder.CreateIndex(
                name: "IX_VisitLogs_VisitDate_IsBot",
                table: "VisitLogs",
                columns: new[] { "VisitDate", "IsBot" });

            migrationBuilder.CreateIndex(
                name: "IX_VisitLogs_VisitedAtUtc",
                table: "VisitLogs",
                column: "VisitedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_VisitLogs_VisitorId",
                table: "VisitLogs",
                column: "VisitorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyVisitStats");

            migrationBuilder.DropTable(
                name: "VisitLogs");

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 24, 11, 14, 8, 717, DateTimeKind.Unspecified).AddTicks(7028), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 24, 11, 14, 8, 717, DateTimeKind.Unspecified).AddTicks(7093), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 24, 11, 14, 8, 717, DateTimeKind.Unspecified).AddTicks(7094), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 24, 11, 14, 8, 717, DateTimeKind.Unspecified).AddTicks(7095), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 24, 11, 14, 8, 717, DateTimeKind.Unspecified).AddTicks(7097), new TimeSpan(0, 0, 0, 0, 0)));
        }
    }
}
