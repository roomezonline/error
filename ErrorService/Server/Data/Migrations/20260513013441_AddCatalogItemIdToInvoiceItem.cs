using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogItemIdToInvoiceItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CatalogItemId",
                table: "CustomerReceiptInvoiceItem",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReceiptInvoiceItem_CatalogItemId",
                table: "CustomerReceiptInvoiceItem",
                column: "CatalogItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerReceiptInvoiceItem_WorkshopInvoiceCatalogItems_CatalogItemId",
                table: "CustomerReceiptInvoiceItem",
                column: "CatalogItemId",
                principalTable: "WorkshopInvoiceCatalogItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerReceiptInvoiceItem_WorkshopInvoiceCatalogItems_CatalogItemId",
                table: "CustomerReceiptInvoiceItem");

            migrationBuilder.DropIndex(
                name: "IX_CustomerReceiptInvoiceItem_CatalogItemId",
                table: "CustomerReceiptInvoiceItem");

            migrationBuilder.DropColumn(
                name: "CatalogItemId",
                table: "CustomerReceiptInvoiceItem");
        }
    }
}
