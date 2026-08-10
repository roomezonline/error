using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceCatalogDeviceTypeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeviceTypeId",
                table: "WorkshopInvoiceCatalogItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkshopInvoiceCatalogItems_DeviceTypeId",
                table: "WorkshopInvoiceCatalogItems",
                column: "DeviceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkshopInvoiceCatalogItems_WorkshopId_DeviceTypeId",
                table: "WorkshopInvoiceCatalogItems",
                columns: new[] { "WorkshopId", "DeviceTypeId" });

            migrationBuilder.AddForeignKey(
                name: "FK_WorkshopInvoiceCatalogItems_DeviceTypes_DeviceTypeId",
                table: "WorkshopInvoiceCatalogItems",
                column: "DeviceTypeId",
                principalTable: "DeviceTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkshopInvoiceCatalogItems_DeviceTypes_DeviceTypeId",
                table: "WorkshopInvoiceCatalogItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkshopInvoiceCatalogItems_DeviceTypeId",
                table: "WorkshopInvoiceCatalogItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkshopInvoiceCatalogItems_WorkshopId_DeviceTypeId",
                table: "WorkshopInvoiceCatalogItems");

            migrationBuilder.DropColumn(
                name: "DeviceTypeId",
                table: "WorkshopInvoiceCatalogItems");
        }
    }
}
