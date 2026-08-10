using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeDeviceCatalogGlobal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            // Deduplicate DeviceTypes: keep the first entry (smallest Id) per Name
            migrationBuilder.Sql(@"
                DELETE dt FROM DeviceTypes dt
                INNER JOIN (
                    SELECT Name, MIN(Id) AS KeepId
                    FROM DeviceTypes
                    GROUP BY Name
                    HAVING COUNT(*) > 1
                ) keep ON dt.Name = keep.Name AND dt.Id > keep.KeepId
            ");

            // Deduplicate DeviceBrands: keep the first entry per (DeviceTypeId, Name)
            // First, update any CustomerReceipt references pointing to duplicate brands
            migrationBuilder.Sql(@"
                UPDATE cr
                SET cr.DeviceBrandId = keep.KeepId
                FROM CustomerReceipts cr
                INNER JOIN DeviceBrands dup ON dup.Id = cr.DeviceBrandId
                INNER JOIN (
                    SELECT DeviceTypeId, Name, MIN(Id) AS KeepId
                    FROM DeviceBrands
                    GROUP BY DeviceTypeId, Name
                    HAVING COUNT(*) > 1
                ) keep ON dup.DeviceTypeId = keep.DeviceTypeId AND dup.Name = keep.Name
                WHERE dup.Id > keep.KeepId
            ");

            // Update OnlineAdmissionRequest references to duplicate brands
            migrationBuilder.Sql(@"
                UPDATE oar
                SET oar.DeviceBrandId = keep.KeepId
                FROM OnlineAdmissionRequests oar
                INNER JOIN DeviceBrands dup ON dup.Id = oar.DeviceBrandId
                INNER JOIN (
                    SELECT DeviceTypeId, Name, MIN(Id) AS KeepId
                    FROM DeviceBrands
                    GROUP BY DeviceTypeId, Name
                    HAVING COUNT(*) > 1
                ) keep ON dup.DeviceTypeId = keep.DeviceTypeId AND dup.Name = keep.Name
                WHERE dup.Id > keep.KeepId
            ");

            // Now delete duplicate DeviceBrands
            migrationBuilder.Sql(@"
                DELETE db FROM DeviceBrands db
                INNER JOIN (
                    SELECT DeviceTypeId, Name, MIN(Id) AS KeepId
                    FROM DeviceBrands
                    GROUP BY DeviceTypeId, Name
                    HAVING COUNT(*) > 1
                ) keep ON db.DeviceTypeId = keep.DeviceTypeId AND db.Name = keep.Name
                WHERE db.Id > keep.KeepId
            ");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
    }
}
