using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMonitoringAlert : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonitoringAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MonitoringId = table.Column<int>(type: "int", nullable: true),
                    CustomerReceiptId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Num = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Command = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    State = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringAlerts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringAlerts_DeviceCode",
                table: "MonitoringAlerts",
                column: "DeviceCode");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringAlerts_DeviceCode_State",
                table: "MonitoringAlerts",
                columns: new[] { "DeviceCode", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringAlerts_DeviceCode_Time",
                table: "MonitoringAlerts",
                columns: new[] { "DeviceCode", "Time" });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringAlerts_MonitoringId",
                table: "MonitoringAlerts",
                column: "MonitoringId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringAlerts_State",
                table: "MonitoringAlerts",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringAlerts_Time",
                table: "MonitoringAlerts",
                column: "Time");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonitoringAlerts");
        }
    }
}
