using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderReturns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    RefundAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RefundCardNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RefundBankName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RefundDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReturnReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AdminNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsRefunded = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderReturns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderReturns_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 20, 6, 38, 15, 550, DateTimeKind.Unspecified).AddTicks(6892), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 20, 6, 38, 15, 550, DateTimeKind.Unspecified).AddTicks(6953), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 20, 6, 38, 15, 550, DateTimeKind.Unspecified).AddTicks(6955), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 20, 6, 38, 15, 550, DateTimeKind.Unspecified).AddTicks(6956), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 20, 6, 38, 15, 550, DateTimeKind.Unspecified).AddTicks(6959), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_OrderReturns_OrderId",
                table: "OrderReturns",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderReturns");

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 20, 6, 24, 3, 105, DateTimeKind.Unspecified).AddTicks(9309), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 20, 6, 24, 3, 105, DateTimeKind.Unspecified).AddTicks(9371), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 20, 6, 24, 3, 105, DateTimeKind.Unspecified).AddTicks(9373), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 20, 6, 24, 3, 105, DateTimeKind.Unspecified).AddTicks(9374), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 20, 6, 24, 3, 105, DateTimeKind.Unspecified).AddTicks(9375), new TimeSpan(0, 0, 0, 0, 0)));
        }
    }
}
