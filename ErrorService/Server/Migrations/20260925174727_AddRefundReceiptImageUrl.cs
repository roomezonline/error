using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundReceiptImageUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefundReceiptImageUrl",
                table: "OrderReturns",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 25, 17, 47, 26, 937, DateTimeKind.Unspecified).AddTicks(9824), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 25, 17, 47, 26, 937, DateTimeKind.Unspecified).AddTicks(9886), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 25, 17, 47, 26, 937, DateTimeKind.Unspecified).AddTicks(9888), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 25, 17, 47, 26, 937, DateTimeKind.Unspecified).AddTicks(9889), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 25, 17, 47, 26, 937, DateTimeKind.Unspecified).AddTicks(9890), new TimeSpan(0, 0, 0, 0, 0)));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefundReceiptImageUrl",
                table: "OrderReturns");

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
        }
    }
}
