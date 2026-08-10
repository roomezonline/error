using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkshopIdToRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_Key",
                table: "Roles");

            migrationBuilder.AddColumn<int>(
                name: "WorkshopId",
                table: "Roles",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_WorkshopId_Key",
                table: "Roles",
                columns: new[] { "WorkshopId", "Key" },
                unique: true,
                filter: "[WorkshopId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Workshops_WorkshopId",
                table: "Roles",
                column: "WorkshopId",
                principalTable: "Workshops",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Workshops_WorkshopId",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_WorkshopId_Key",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "WorkshopId",
                table: "Roles");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Key",
                table: "Roles",
                column: "Key",
                unique: true);
        }
    }
}
