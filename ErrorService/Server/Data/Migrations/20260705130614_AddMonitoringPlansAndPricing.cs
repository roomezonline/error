using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMonitoringPlansAndPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MonitoringPlanId",
                table: "MonitoringRenewalRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceAtRequest",
                table: "MonitoringRenewalRequests",
                type: "decimal(18,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "MonitoringPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Months = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,0)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringPlans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringRenewalRequests_MonitoringPlanId",
                table: "MonitoringRenewalRequests",
                column: "MonitoringPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringPlans_IsActive",
                table: "MonitoringPlans",
                column: "IsActive");

            migrationBuilder.AddForeignKey(
                name: "FK_MonitoringRenewalRequests_MonitoringPlans_MonitoringPlanId",
                table: "MonitoringRenewalRequests",
                column: "MonitoringPlanId",
                principalTable: "MonitoringPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MonitoringRenewalRequests_MonitoringPlans_MonitoringPlanId",
                table: "MonitoringRenewalRequests");

            migrationBuilder.DropTable(
                name: "MonitoringPlans");

            migrationBuilder.DropIndex(
                name: "IX_MonitoringRenewalRequests_MonitoringPlanId",
                table: "MonitoringRenewalRequests");

            migrationBuilder.DropColumn(
                name: "MonitoringPlanId",
                table: "MonitoringRenewalRequests");

            migrationBuilder.DropColumn(
                name: "PriceAtRequest",
                table: "MonitoringRenewalRequests");
        }
    }
}
