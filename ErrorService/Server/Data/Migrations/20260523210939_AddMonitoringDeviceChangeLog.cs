using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMonitoringDeviceChangeLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonitoringDeviceChangeLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ChangeType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ChangeDescription = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DataSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerReceiptId = table.Column<int>(type: "int", nullable: true),
                    WorkshopId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtFa = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringDeviceChangeLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDeviceChangeLogs_ChangeType",
                table: "MonitoringDeviceChangeLogs",
                column: "ChangeType");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDeviceChangeLogs_DeviceCode",
                table: "MonitoringDeviceChangeLogs",
                column: "DeviceCode");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDeviceChangeLogs_DeviceCode_CreatedAt",
                table: "MonitoringDeviceChangeLogs",
                columns: new[] { "DeviceCode", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonitoringDeviceChangeLogs");
        }
    }
}
