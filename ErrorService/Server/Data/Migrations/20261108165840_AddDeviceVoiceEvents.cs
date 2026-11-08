using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceVoiceEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceVoiceEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MonitoringDeviceId = table.Column<int>(type: "int", nullable: false),
                    DeviceCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ActionKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DescriptionFa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsTest = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceVoiceEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceVoiceEvents_MonitoringDevices_MonitoringDeviceId",
                        column: x => x.MonitoringDeviceId,
                        principalTable: "MonitoringDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceVoiceEvents_DeviceCode_CreatedAt",
                table: "DeviceVoiceEvents",
                columns: new[] { "DeviceCode", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceVoiceEvents_ExpiresAt",
                table: "DeviceVoiceEvents",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceVoiceEvents_MonitoringDeviceId_Id",
                table: "DeviceVoiceEvents",
                columns: new[] { "MonitoringDeviceId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceVoiceEvents");
        }
    }
}
