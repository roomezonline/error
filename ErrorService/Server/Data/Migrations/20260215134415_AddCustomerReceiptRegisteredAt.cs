using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerReceiptRegisteredAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerReceipts_WorkshopId_CreatedAt",
                table: "CustomerReceipts");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RegisteredAt",
                table: "CustomerReceipts",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReceipts_WorkshopId_RegisteredAt",
                table: "CustomerReceipts",
                columns: new[] { "WorkshopId", "RegisteredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerReceipts_WorkshopId_RegisteredAt",
                table: "CustomerReceipts");

            migrationBuilder.DropColumn(
                name: "RegisteredAt",
                table: "CustomerReceipts");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReceipts_WorkshopId_CreatedAt",
                table: "CustomerReceipts",
                columns: new[] { "WorkshopId", "CreatedAt" });
        }
    }
}
