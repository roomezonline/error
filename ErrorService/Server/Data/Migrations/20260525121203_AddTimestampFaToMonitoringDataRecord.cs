using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTimestampFaToMonitoringDataRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TimestampFa",
                table: "MonitoringDataRecords",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_MonitoringDataArchives_MonitoringReceiptConnections_MonitoringReceiptConnectionId",
                table: "MonitoringDataArchives",
                column: "MonitoringReceiptConnectionId",
                principalTable: "MonitoringReceiptConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MonitoringDataArchives_MonitoringReceiptConnections_MonitoringReceiptConnectionId",
                table: "MonitoringDataArchives");

            migrationBuilder.DropColumn(
                name: "TimestampFa",
                table: "MonitoringDataRecords");
        }
    }
}
