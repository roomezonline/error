using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSensorFinder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SensorFinderDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SensorFinderDevices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SensorFinderRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    SensorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SensorType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SizeText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SensorFinderRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SensorFinderRecords_SensorFinderDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "SensorFinderDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SensorFinderImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecordId = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SensorFinderImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SensorFinderImages_SensorFinderRecords_RecordId",
                        column: x => x.RecordId,
                        principalTable: "SensorFinderRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SensorFinderDevices_Name",
                table: "SensorFinderDevices",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SensorFinderImages_RecordId_SortOrder",
                table: "SensorFinderImages",
                columns: new[] { "RecordId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SensorFinderRecords_DeviceId_SensorName",
                table: "SensorFinderRecords",
                columns: new[] { "DeviceId", "SensorName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SensorFinderImages");

            migrationBuilder.DropTable(
                name: "SensorFinderRecords");

            migrationBuilder.DropTable(
                name: "SensorFinderDevices");
        }
    }
}
