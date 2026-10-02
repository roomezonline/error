using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCouponAssignedUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedToUserId",
                table: "Coupons",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 20, 7, 14, 25, 801, DateTimeKind.Unspecified).AddTicks(2641), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 20, 7, 14, 25, 801, DateTimeKind.Unspecified).AddTicks(2703), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 20, 7, 14, 25, 801, DateTimeKind.Unspecified).AddTicks(2705), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 20, 7, 14, 25, 801, DateTimeKind.Unspecified).AddTicks(2706), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 20, 7, 14, 25, 801, DateTimeKind.Unspecified).AddTicks(2707), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_AssignedToUserId",
                table: "Coupons",
                column: "AssignedToUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Coupons_Users_AssignedToUserId",
                table: "Coupons",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Coupons_Users_AssignedToUserId",
                table: "Coupons");

            migrationBuilder.DropIndex(
                name: "IX_Coupons_AssignedToUserId",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "AssignedToUserId",
                table: "Coupons");

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
        }
    }
}
