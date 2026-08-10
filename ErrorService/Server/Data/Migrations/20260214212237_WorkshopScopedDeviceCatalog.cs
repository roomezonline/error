using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkshopScopedDeviceCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeviceTypes_Name",
                table: "DeviceTypes");

            migrationBuilder.DropIndex(
                name: "IX_DeviceBrands_DeviceTypeId_Name",
                table: "DeviceBrands");

            migrationBuilder.DropIndex(
                name: "IX_DeviceBrands_DeviceTypeId_SortOrder",
                table: "DeviceBrands");

            migrationBuilder.AddColumn<int>(
                name: "WorkshopId",
                table: "DeviceTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WorkshopId",
                table: "DeviceBrands",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("DELETE FROM [DeviceBrands];");
            migrationBuilder.Sql("DELETE FROM [DeviceTypes];");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTypes_WorkshopId_Name",
                table: "DeviceTypes",
                columns: new[] { "WorkshopId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceBrands_DeviceTypeId",
                table: "DeviceBrands",
                column: "DeviceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceBrands_WorkshopId_DeviceTypeId_Name",
                table: "DeviceBrands",
                columns: new[] { "WorkshopId", "DeviceTypeId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceBrands_WorkshopId_DeviceTypeId_SortOrder",
                table: "DeviceBrands",
                columns: new[] { "WorkshopId", "DeviceTypeId", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceBrands_Workshops_WorkshopId",
                table: "DeviceBrands",
                column: "WorkshopId",
                principalTable: "Workshops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceTypes_Workshops_WorkshopId",
                table: "DeviceTypes",
                column: "WorkshopId",
                principalTable: "Workshops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeviceBrands_Workshops_WorkshopId",
                table: "DeviceBrands");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviceTypes_Workshops_WorkshopId",
                table: "DeviceTypes");

            migrationBuilder.DropIndex(
                name: "IX_DeviceTypes_WorkshopId_Name",
                table: "DeviceTypes");

            migrationBuilder.DropIndex(
                name: "IX_DeviceBrands_DeviceTypeId",
                table: "DeviceBrands");

            migrationBuilder.DropIndex(
                name: "IX_DeviceBrands_WorkshopId_DeviceTypeId_Name",
                table: "DeviceBrands");

            migrationBuilder.DropIndex(
                name: "IX_DeviceBrands_WorkshopId_DeviceTypeId_SortOrder",
                table: "DeviceBrands");

            migrationBuilder.DropColumn(
                name: "WorkshopId",
                table: "DeviceTypes");

            migrationBuilder.DropColumn(
                name: "WorkshopId",
                table: "DeviceBrands");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTypes_Name",
                table: "DeviceTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceBrands_DeviceTypeId_Name",
                table: "DeviceBrands",
                columns: new[] { "DeviceTypeId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceBrands_DeviceTypeId_SortOrder",
                table: "DeviceBrands",
                columns: new[] { "DeviceTypeId", "SortOrder" });
        }
    }
}
