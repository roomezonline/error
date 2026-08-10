using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOnlineAdmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdmissionExpertiseSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    HtmlDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionExpertiseSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OnlineAdmissionRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CustomerMobile = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CustomerAddress = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DeviceTypeId = table.Column<int>(type: "int", nullable: false),
                    DeviceBrandId = table.Column<int>(type: "int", nullable: true),
                    ProblemDescription = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    DeviceImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReceiptImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExpertiseAmount = table.Column<long>(type: "bigint", nullable: true),
                    AdminNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnlineAdmissionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnlineAdmissionRequests_DeviceBrands_DeviceBrandId",
                        column: x => x.DeviceBrandId,
                        principalTable: "DeviceBrands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnlineAdmissionRequests_DeviceTypes_DeviceTypeId",
                        column: x => x.DeviceTypeId,
                        principalTable: "DeviceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnlineAdmissionRequests_CreatedAt",
                table: "OnlineAdmissionRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OnlineAdmissionRequests_CustomerMobile",
                table: "OnlineAdmissionRequests",
                column: "CustomerMobile");

            migrationBuilder.CreateIndex(
                name: "IX_OnlineAdmissionRequests_DeviceBrandId",
                table: "OnlineAdmissionRequests",
                column: "DeviceBrandId");

            migrationBuilder.CreateIndex(
                name: "IX_OnlineAdmissionRequests_DeviceTypeId",
                table: "OnlineAdmissionRequests",
                column: "DeviceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_OnlineAdmissionRequests_Status",
                table: "OnlineAdmissionRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OnlineAdmissionRequests_Type",
                table: "OnlineAdmissionRequests",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdmissionExpertiseSettings");

            migrationBuilder.DropTable(
                name: "OnlineAdmissionRequests");
        }
    }
}
