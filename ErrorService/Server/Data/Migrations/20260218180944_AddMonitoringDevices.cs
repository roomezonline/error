using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMonitoringDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonitoringDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DeviceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringDevices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MonitoringDeviceAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MonitoringDeviceId = table.Column<int>(type: "int", nullable: false),
                    WorkshopId = table.Column<int>(type: "int", nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringDeviceAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonitoringDeviceAssignments_MonitoringDevices_MonitoringDeviceId",
                        column: x => x.MonitoringDeviceId,
                        principalTable: "MonitoringDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonitoringDeviceAssignments_Workshops_WorkshopId",
                        column: x => x.WorkshopId,
                        principalTable: "Workshops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDeviceAssignments_EndAt",
                table: "MonitoringDeviceAssignments",
                column: "EndAt");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDeviceAssignments_MonitoringDeviceId_WorkshopId",
                table: "MonitoringDeviceAssignments",
                columns: new[] { "MonitoringDeviceId", "WorkshopId" });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDeviceAssignments_StartAt",
                table: "MonitoringDeviceAssignments",
                column: "StartAt");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDeviceAssignments_WorkshopId",
                table: "MonitoringDeviceAssignments",
                column: "WorkshopId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDevices_DeviceNumber",
                table: "MonitoringDevices",
                column: "DeviceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDevices_IsActive",
                table: "MonitoringDevices",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonitoringDeviceAssignments");

            migrationBuilder.DropTable(
                name: "MonitoringDevices");
        }
    }
}
