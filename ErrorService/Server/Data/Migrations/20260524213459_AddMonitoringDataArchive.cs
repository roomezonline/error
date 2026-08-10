using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMonitoringDataArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonitoringDataArchives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkshopId = table.Column<int>(type: "int", nullable: false),
                    MonitoringReceiptConnectionId = table.Column<int>(type: "int", nullable: false),
                    CustomerReceiptId = table.Column<int>(type: "int", nullable: true),
                    DeviceCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordCount = table.Column<int>(type: "int", nullable: false),
                    EstimatedBytes = table.Column<long>(type: "bigint", nullable: false),
                    Downloaded = table.Column<bool>(type: "bit", nullable: false),
                    DownloadedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringDataArchives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonitoringDataArchives_Workshops_WorkshopId",
                        column: x => x.WorkshopId,
                        principalTable: "Workshops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDataArchives_CreatedAt",
                table: "MonitoringDataArchives",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDataArchives_MonitoringReceiptConnectionId",
                table: "MonitoringDataArchives",
                column: "MonitoringReceiptConnectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDataArchives_WorkshopId",
                table: "MonitoringDataArchives",
                column: "WorkshopId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDataArchives_WorkshopId_Downloaded",
                table: "MonitoringDataArchives",
                columns: new[] { "WorkshopId", "Downloaded" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonitoringDataArchives");
        }
    }
}
