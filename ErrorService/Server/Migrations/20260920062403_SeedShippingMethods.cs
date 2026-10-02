using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErrorService.Server.Migrations
{
    /// <inheritdoc />
    public partial class SeedShippingMethods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ShippingMethods",
                columns: new[] { "Id", "CreatedAt", "Description", "EstimatedDays", "IsActive", "Name", "Price", "SortOrder" },
                values: new object[,]
                {
                    { 1, new DateTimeOffset(new DateTime(2026, 9, 20, 6, 24, 3, 105, DateTimeKind.Unspecified).AddTicks(9309), new TimeSpan(0, 0, 0, 0, 0)), "ارسال از طریق پست جمهوری اسلامی - سرویس پیشتاز", "۲ تا ۳ روز کاری", true, "پست پیشتاز", 35000m, 1 },
                    { 2, new DateTimeOffset(new DateTime(2026, 9, 20, 6, 24, 3, 105, DateTimeKind.Unspecified).AddTicks(9371), new TimeSpan(0, 0, 0, 0, 0)), "ارسال از طریق پست جمهوری اسلامی - سرویس سفارشی", "۴ تا ۷ روز کاری", true, "پست سفارشی", 20000m, 2 },
                    { 3, new DateTimeOffset(new DateTime(2026, 9, 20, 6, 24, 3, 105, DateTimeKind.Unspecified).AddTicks(9373), new TimeSpan(0, 0, 0, 0, 0)), "ارسال سریع از طریق تیپاکس", "۱ تا ۲ روز کاری", true, "تیپاکس", 45000m, 3 },
                    { 4, new DateTimeOffset(new DateTime(2026, 9, 20, 6, 24, 3, 105, DateTimeKind.Unspecified).AddTicks(9374), new TimeSpan(0, 0, 0, 0, 0)), "ارسال فوری با اسنپ‌باکس (فقط تهران)", "همان روز", true, "اسنپ‌باکس", 25000m, 4 },
                    { 5, new DateTimeOffset(new DateTime(2026, 9, 20, 6, 24, 3, 105, DateTimeKind.Unspecified).AddTicks(9375), new TimeSpan(0, 0, 0, 0, 0)), "ارسال رایگان برای سفارش‌های بالای ۵۰۰ هزار تومان", "۳ تا ۵ روز کاری", true, "ارسال رایگان", 0m, 5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShippingMethods_IsActive_SortOrder",
                table: "ShippingMethods",
                columns: new[] { "IsActive", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShippingMethods_IsActive_SortOrder",
                table: "ShippingMethods");

            migrationBuilder.DeleteData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 5);
        }
    }
}
