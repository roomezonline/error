using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceVoiceMonitoringDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServiceVoiceActions_Key",
                table: "ServiceVoiceActions");

            migrationBuilder.AddColumn<int>(
                name: "MonitoringDeviceId",
                table: "ServiceVoiceActions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceVoiceActions_MonitoringDeviceId_Key",
                table: "ServiceVoiceActions",
                columns: new[] { "MonitoringDeviceId", "Key" },
                unique: true,
                filter: "[MonitoringDeviceId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceVoiceActions_MonitoringDevices_MonitoringDeviceId",
                table: "ServiceVoiceActions",
                column: "MonitoringDeviceId",
                principalTable: "MonitoringDevices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceVoiceActions_MonitoringDevices_MonitoringDeviceId",
                table: "ServiceVoiceActions");

            migrationBuilder.DropIndex(
                name: "IX_ServiceVoiceActions_MonitoringDeviceId_Key",
                table: "ServiceVoiceActions");

            migrationBuilder.DropColumn(
                name: "MonitoringDeviceId",
                table: "ServiceVoiceActions");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceVoiceActions_Key",
                table: "ServiceVoiceActions",
                column: "Key",
                unique: true);
        }
    }
}
