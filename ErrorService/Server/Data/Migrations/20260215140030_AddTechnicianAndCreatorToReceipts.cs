using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTechnicianAndCreatorToReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTechnician",
                table: "WorkshopUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "CustomerReceipts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TechnicianId",
                table: "CustomerReceipts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReceipts_CreatedByUserId",
                table: "CustomerReceipts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReceipts_TechnicianId",
                table: "CustomerReceipts",
                column: "TechnicianId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerReceipts_WorkshopUsers_CreatedByUserId",
                table: "CustomerReceipts",
                column: "CreatedByUserId",
                principalTable: "WorkshopUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerReceipts_WorkshopUsers_TechnicianId",
                table: "CustomerReceipts",
                column: "TechnicianId",
                principalTable: "WorkshopUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerReceipts_WorkshopUsers_CreatedByUserId",
                table: "CustomerReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerReceipts_WorkshopUsers_TechnicianId",
                table: "CustomerReceipts");

            migrationBuilder.DropIndex(
                name: "IX_CustomerReceipts_CreatedByUserId",
                table: "CustomerReceipts");

            migrationBuilder.DropIndex(
                name: "IX_CustomerReceipts_TechnicianId",
                table: "CustomerReceipts");

            migrationBuilder.DropColumn(
                name: "IsTechnician",
                table: "WorkshopUsers");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "CustomerReceipts");

            migrationBuilder.DropColumn(
                name: "TechnicianId",
                table: "CustomerReceipts");
        }
    }
}
