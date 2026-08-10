using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMonitoringReceiptConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonitoringReceiptConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerReceiptId = table.Column<int>(type: "int", nullable: false),
                    WorkshopId = table.Column<int>(type: "int", nullable: false),
                    MonitoringDeviceId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringReceiptConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonitoringReceiptConnections_CustomerReceipts_CustomerReceiptId",
                        column: x => x.CustomerReceiptId,
                        principalTable: "CustomerReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonitoringReceiptConnections_MonitoringDevices_MonitoringDeviceId",
                        column: x => x.MonitoringDeviceId,
                        principalTable: "MonitoringDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonitoringReceiptConnections_Workshops_WorkshopId",
                        column: x => x.WorkshopId,
                        principalTable: "Workshops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringReceiptConnections_CreatedAt",
                table: "MonitoringReceiptConnections",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringReceiptConnections_CustomerReceiptId",
                table: "MonitoringReceiptConnections",
                column: "CustomerReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringReceiptConnections_CustomerReceiptId_EndedAt",
                table: "MonitoringReceiptConnections",
                columns: new[] { "CustomerReceiptId", "EndedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringReceiptConnections_EndedAt",
                table: "MonitoringReceiptConnections",
                column: "EndedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringReceiptConnections_MonitoringDeviceId",
                table: "MonitoringReceiptConnections",
                column: "MonitoringDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringReceiptConnections_WorkshopId",
                table: "MonitoringReceiptConnections",
                column: "WorkshopId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonitoringReceiptConnections");
        }
    }
}
