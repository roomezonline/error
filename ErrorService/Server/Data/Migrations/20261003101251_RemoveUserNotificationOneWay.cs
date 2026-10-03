using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUserNotificationOneWay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessengerLinkCodes");

            migrationBuilder.DropTable(
                name: "UserNotificationSettings");

            migrationBuilder.AddColumn<string>(
                name: "BaleSafirApiKey",
                table: "SiteSettings",
                type: "nvarchar(max)",
                nullable: true);





        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaleSafirApiKey",
                table: "SiteSettings");

            migrationBuilder.CreateTable(
                name: "MessengerLinkCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppUserId = table.Column<int>(type: "int", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessengerLinkCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserNotificationSettings",
                columns: table => new
                {
                    AppUserId = table.Column<int>(type: "int", nullable: false),
                    Channels = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotificationSettings", x => x.AppUserId);
                });






            migrationBuilder.CreateIndex(
                name: "IX_MessengerLinkCodes_AppUserId_Channel",
                table: "MessengerLinkCodes",
                columns: new[] { "AppUserId", "Channel" });

            migrationBuilder.CreateIndex(
                name: "IX_MessengerLinkCodes_Code",
                table: "MessengerLinkCodes",
                column: "Code",
                unique: true);
        }
    }
}
