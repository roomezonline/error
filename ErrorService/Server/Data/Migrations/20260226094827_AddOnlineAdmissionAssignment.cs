using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOnlineAdmissionAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedWorkshopId",
                table: "OnlineAdmissionRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedReceiptId",
                table: "OnlineAdmissionRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnlineAdmissionRequests_AssignedWorkshopId",
                table: "OnlineAdmissionRequests",
                column: "AssignedWorkshopId");

            migrationBuilder.CreateIndex(
                name: "IX_OnlineAdmissionRequests_CreatedReceiptId",
                table: "OnlineAdmissionRequests",
                column: "CreatedReceiptId");

            migrationBuilder.AddForeignKey(
                name: "FK_OnlineAdmissionRequests_CustomerReceipts_CreatedReceiptId",
                table: "OnlineAdmissionRequests",
                column: "CreatedReceiptId",
                principalTable: "CustomerReceipts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OnlineAdmissionRequests_Workshops_AssignedWorkshopId",
                table: "OnlineAdmissionRequests",
                column: "AssignedWorkshopId",
                principalTable: "Workshops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OnlineAdmissionRequests_CustomerReceipts_CreatedReceiptId",
                table: "OnlineAdmissionRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_OnlineAdmissionRequests_Workshops_AssignedWorkshopId",
                table: "OnlineAdmissionRequests");

            migrationBuilder.DropIndex(
                name: "IX_OnlineAdmissionRequests_AssignedWorkshopId",
                table: "OnlineAdmissionRequests");

            migrationBuilder.DropIndex(
                name: "IX_OnlineAdmissionRequests_CreatedReceiptId",
                table: "OnlineAdmissionRequests");

            migrationBuilder.DropColumn(
                name: "AssignedWorkshopId",
                table: "OnlineAdmissionRequests");

            migrationBuilder.DropColumn(
                name: "CreatedReceiptId",
                table: "OnlineAdmissionRequests");
        }
    }
}
