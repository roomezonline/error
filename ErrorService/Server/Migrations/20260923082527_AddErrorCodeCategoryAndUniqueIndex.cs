using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddErrorCodeCategoryAndUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "ErrorCodes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // Move documents from duplicate rows to the keeper (lowest Id)
            migrationBuilder.Sql(@"
;WITH d AS (
    SELECT Id,
           MIN(Id) OVER (PARTITION BY LOWER(RTRIM(Brand)), LOWER(RTRIM(DeviceType)), LOWER(RTRIM(Code))) AS KeeperId
    FROM ErrorCodes
)
UPDATE doc SET doc.ErrorCodeId = d.KeeperId
FROM ErrorCodeDocuments doc
INNER JOIN d ON doc.ErrorCodeId = d.Id
WHERE d.Id <> d.KeeperId;
");

            // Delete duplicate error codes (keep lowest Id)
            migrationBuilder.Sql(@"
;WITH d AS (
    SELECT Id,
           ROW_NUMBER() OVER (
               PARTITION BY LOWER(RTRIM(Brand)), LOWER(RTRIM(DeviceType)), LOWER(RTRIM(Code))
               ORDER BY Id) AS rn
    FROM ErrorCodes
)
DELETE FROM d WHERE rn > 1;
");

            migrationBuilder.DropIndex(
                name: "IX_ErrorCodes_Brand_DeviceType_Code",
                table: "ErrorCodes");

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

            migrationBuilder.CreateIndex(
                name: "IX_ErrorCodes_Brand_DeviceType_Code",
                table: "ErrorCodes",
                columns: new[] { "Brand", "DeviceType", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ErrorCodes_Brand_DeviceType_Code",
                table: "ErrorCodes");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "ErrorCodes");

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
                name: "IX_ErrorCodes_Brand_DeviceType_Code",
                table: "ErrorCodes",
                columns: new[] { "Brand", "DeviceType", "Code" });
        }
    }
}
