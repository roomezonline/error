using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddErrorCodeCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ErrorCodeCatalogItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorCodeCatalogItems", x => x.Id);
                });

            // Seed brands from existing error codes
            migrationBuilder.Sql(@"
INSERT INTO ErrorCodeCatalogItems (Kind, Name)
SELECT DISTINCT 0, LTRIM(RTRIM(Brand))
FROM ErrorCodes
WHERE Brand IS NOT NULL AND LTRIM(RTRIM(Brand)) <> ''
ORDER BY LTRIM(RTRIM(Brand));
");

            // Seed device types from existing error codes
            migrationBuilder.Sql(@"
INSERT INTO ErrorCodeCatalogItems (Kind, Name)
SELECT DISTINCT 1, LTRIM(RTRIM(DeviceType))
FROM ErrorCodes
WHERE DeviceType IS NOT NULL AND LTRIM(RTRIM(DeviceType)) <> ''
ORDER BY LTRIM(RTRIM(DeviceType));
");

            // Seed categories: defaults + existing values on error codes
            migrationBuilder.Sql(@"
INSERT INTO ErrorCodeCatalogItems (Kind, Name)
VALUES
(2, N'سنسورها'),
(2, N'موتور و پمپ'),
(2, N'برد و الکترونیک'),
(2, N'آب و نشتی'),
(2, N'دما و گرمایش'),
(2, N'صفحه نمایش و پنل'),
(2, N'منبع تغذیه'),
(2, N'اتصالات و سیم‌کشی'),
(2, N'نرم‌افزار و فریمور'),
(2, N'سایر');

INSERT INTO ErrorCodeCatalogItems (Kind, Name)
SELECT DISTINCT 2, LTRIM(RTRIM(Category))
FROM ErrorCodes
WHERE Category IS NOT NULL AND LTRIM(RTRIM(Category)) <> ''
  AND NOT EXISTS (
    SELECT 1 FROM ErrorCodeCatalogItems c
    WHERE c.Kind = 2 AND LOWER(c.Name) = LOWER(LTRIM(RTRIM(Category)))
  );
");

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 23, 8, 58, 59, 449, DateTimeKind.Unspecified).AddTicks(7493), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 23, 8, 58, 59, 449, DateTimeKind.Unspecified).AddTicks(7555), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 23, 8, 58, 59, 449, DateTimeKind.Unspecified).AddTicks(7557), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 23, 8, 58, 59, 449, DateTimeKind.Unspecified).AddTicks(7558), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 23, 8, 58, 59, 449, DateTimeKind.Unspecified).AddTicks(7559), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_ErrorCodeCatalogItems_Kind_Name",
                table: "ErrorCodeCatalogItems",
                columns: new[] { "Kind", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErrorCodeCatalogItems");

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 23, 8, 25, 26, 881, DateTimeKind.Unspecified).AddTicks(8945), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 23, 8, 25, 26, 881, DateTimeKind.Unspecified).AddTicks(9011), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 23, 8, 25, 26, 881, DateTimeKind.Unspecified).AddTicks(9012), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 23, 8, 25, 26, 881, DateTimeKind.Unspecified).AddTicks(9014), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 9, 23, 8, 25, 26, 881, DateTimeKind.Unspecified).AddTicks(9017), new TimeSpan(0, 0, 0, 0, 0)));
        }
    }
}
