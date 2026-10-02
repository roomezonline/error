using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderShipmentsAndEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderEvents_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderShipments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Carrier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TrackingNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ShippedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderShipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderShipments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderEvents_OrderId_CreatedAt",
                table: "OrderEvents",
                columns: new[] { "OrderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderShipments_OrderId_AttemptNumber",
                table: "OrderShipments",
                columns: new[] { "OrderId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderShipments_TrackingNumber",
                table: "OrderShipments",
                column: "TrackingNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderEvents");

            migrationBuilder.DropTable(
                name: "OrderShipments");
        }
    }
}
