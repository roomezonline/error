using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceItemsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerReceiptInvoiceItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerReceiptBillingId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActionDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    UnitPrice = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    TechnicianId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerReceiptInvoiceItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerReceiptInvoiceItem_CustomerReceiptBillings_CustomerReceiptBillingId",
                        column: x => x.CustomerReceiptBillingId,
                        principalTable: "CustomerReceiptBillings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerReceiptInvoiceItem_WorkshopUsers_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "WorkshopUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReceiptInvoiceItem_CustomerReceiptBillingId",
                table: "CustomerReceiptInvoiceItem",
                column: "CustomerReceiptBillingId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReceiptInvoiceItem_TechnicianId",
                table: "CustomerReceiptInvoiceItem",
                column: "TechnicianId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerReceiptInvoiceItem");
        }
    }
}
