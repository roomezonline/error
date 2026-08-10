using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOnlineAdmissionProvinceCity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CityId",
                table: "OnlineAdmissionRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProvinceId",
                table: "OnlineAdmissionRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnlineAdmissionRequests_CityId",
                table: "OnlineAdmissionRequests",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_OnlineAdmissionRequests_ProvinceId",
                table: "OnlineAdmissionRequests",
                column: "ProvinceId");

            migrationBuilder.AddForeignKey(
                name: "FK_OnlineAdmissionRequests_Cities_CityId",
                table: "OnlineAdmissionRequests",
                column: "CityId",
                principalTable: "Cities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OnlineAdmissionRequests_Provinces_ProvinceId",
                table: "OnlineAdmissionRequests",
                column: "ProvinceId",
                principalTable: "Provinces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OnlineAdmissionRequests_Cities_CityId",
                table: "OnlineAdmissionRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_OnlineAdmissionRequests_Provinces_ProvinceId",
                table: "OnlineAdmissionRequests");

            migrationBuilder.DropIndex(
                name: "IX_OnlineAdmissionRequests_CityId",
                table: "OnlineAdmissionRequests");

            migrationBuilder.DropIndex(
                name: "IX_OnlineAdmissionRequests_ProvinceId",
                table: "OnlineAdmissionRequests");

            migrationBuilder.DropColumn(
                name: "CityId",
                table: "OnlineAdmissionRequests");

            migrationBuilder.DropColumn(
                name: "ProvinceId",
                table: "OnlineAdmissionRequests");
        }
    }
}
