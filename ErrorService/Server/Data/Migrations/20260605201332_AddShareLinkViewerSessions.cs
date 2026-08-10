using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShareLinkViewerSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShareLinkViewerSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShareLinkId = table.Column<int>(type: "int", nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DeviceInfo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EnteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastPingAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExitedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PingCount = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShareLinkViewerSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShareLinkViewerSessions_IsActive",
                table: "ShareLinkViewerSessions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ShareLinkViewerSessions_LastPingAt",
                table: "ShareLinkViewerSessions",
                column: "LastPingAt");

            migrationBuilder.CreateIndex(
                name: "IX_ShareLinkViewerSessions_SessionId",
                table: "ShareLinkViewerSessions",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShareLinkViewerSessions_ShareLinkId",
                table: "ShareLinkViewerSessions",
                column: "ShareLinkId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShareLinkViewerSessions");
        }
    }
}
