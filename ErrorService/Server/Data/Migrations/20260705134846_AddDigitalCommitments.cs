using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDigitalCommitments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DigitalCommitments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerReceiptId = table.Column<int>(type: "int", nullable: false),
                    WorkshopId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    CustomerFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CustomerMobile = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DeviceTypeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DeviceBrandName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReceiptNumber = table.Column<int>(type: "int", nullable: false),
                    RegisteredAtFa = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WorkshopName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WorkshopLogoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExtraBodyText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FullBodyText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigitalCommitments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DigitalCommitments_CustomerReceipts_CustomerReceiptId",
                        column: x => x.CustomerReceiptId,
                        principalTable: "CustomerReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DigitalCommitments_WorkshopCustomers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "WorkshopCustomers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DigitalCommitments_WorkshopUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "WorkshopUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DigitalCommitments_Workshops_WorkshopId",
                        column: x => x.WorkshopId,
                        principalTable: "Workshops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalCommitments_CreatedByUserId",
                table: "DigitalCommitments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalCommitments_CustomerId",
                table: "DigitalCommitments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalCommitments_CustomerMobile",
                table: "DigitalCommitments",
                column: "CustomerMobile");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalCommitments_CustomerReceiptId",
                table: "DigitalCommitments",
                column: "CustomerReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DigitalCommitments_WorkshopId",
                table: "DigitalCommitments",
                column: "WorkshopId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DigitalCommitments");
        }
    }
}
