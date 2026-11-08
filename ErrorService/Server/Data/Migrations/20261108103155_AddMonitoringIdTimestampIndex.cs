using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMonitoringIdTimestampIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MonitoringDataRecords_MonitoringId_Timestamp",
                table: "MonitoringDataRecords",
                columns: new[] { "MonitoringId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MonitoringDataRecords_MonitoringId_Timestamp",
                table: "MonitoringDataRecords");
        }
    }
}
