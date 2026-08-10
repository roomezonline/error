using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMonitoringDataRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MonitoringId",
                table: "MonitoringDeviceChangeLogs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MonitoringDataRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MonitoringId = table.Column<int>(type: "int", nullable: true),
                    CustomerReceiptId = table.Column<int>(type: "int", nullable: true),
                    TemperatureRef = table.Column<float>(type: "real", nullable: true),
                    TemperatureFreez = table.Column<float>(type: "real", nullable: true),
                    TemperatureEnv = table.Column<float>(type: "real", nullable: true),
                    MotorState = table.Column<bool>(type: "bit", nullable: false),
                    Bargh = table.Column<bool>(type: "bit", nullable: false),
                    Element1 = table.Column<bool>(type: "bit", nullable: false),
                    Element2 = table.Column<bool>(type: "bit", nullable: false),
                    Fdc1 = table.Column<bool>(type: "bit", nullable: false),
                    Fac1 = table.Column<bool>(type: "bit", nullable: false),
                    Jaryan = table.Column<float>(type: "real", nullable: true),
                    Power = table.Column<float>(type: "real", nullable: true),
                    Kw = table.Column<float>(type: "real", nullable: true),
                    SumKw = table.Column<float>(type: "real", nullable: true),
                    State = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringDataRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonitoringDataRecords_CustomerReceipts_CustomerReceiptId",
                        column: x => x.CustomerReceiptId,
                        principalTable: "CustomerReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDeviceChangeLogs_MonitoringId",
                table: "MonitoringDeviceChangeLogs",
                column: "MonitoringId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDataRecords_CustomerReceiptId",
                table: "MonitoringDataRecords",
                column: "CustomerReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDataRecords_DeviceCode",
                table: "MonitoringDataRecords",
                column: "DeviceCode");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDataRecords_DeviceCode_Timestamp",
                table: "MonitoringDataRecords",
                columns: new[] { "DeviceCode", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDataRecords_MonitoringId",
                table: "MonitoringDataRecords",
                column: "MonitoringId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDataRecords_Timestamp",
                table: "MonitoringDataRecords",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonitoringDataRecords");

            migrationBuilder.DropIndex(
                name: "IX_MonitoringDeviceChangeLogs_MonitoringId",
                table: "MonitoringDeviceChangeLogs");

            migrationBuilder.DropColumn(
                name: "MonitoringId",
                table: "MonitoringDeviceChangeLogs");
        }
    }
}
