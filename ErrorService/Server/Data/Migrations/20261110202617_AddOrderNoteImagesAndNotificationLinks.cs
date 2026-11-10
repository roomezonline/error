using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderNoteImagesAndNotificationLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NotificationKey",
                table: "OrderEvents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrlsJson",
                table: "Notifications",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrderAdminNoteImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    NoteKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ImageUrlsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderAdminNoteImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderAdminNoteImages_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderEvents_NotificationKey",
                table: "OrderEvents",
                column: "NotificationKey");

            migrationBuilder.CreateIndex(
                name: "IX_OrderAdminNoteImages_OrderId_NoteKey",
                table: "OrderAdminNoteImages",
                columns: new[] { "OrderId", "NoteKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderAdminNoteImages");

            migrationBuilder.DropIndex(
                name: "IX_OrderEvents_NotificationKey",
                table: "OrderEvents");

            migrationBuilder.DropColumn(
                name: "NotificationKey",
                table: "OrderEvents");

            migrationBuilder.DropColumn(
                name: "ImageUrlsJson",
                table: "Notifications");
        }
    }
}
