using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemEventLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemEventLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    PersianTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PersianDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TechnicalTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TechnicalDetails = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClientIp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequestPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HttpMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    RequestParameters = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseStatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseTimeMs = table.Column<long>(type: "bigint", nullable: true),
                    Component = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AdminNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OccurrenceCount = table.Column<int>(type: "int", nullable: false),
                    LastOccurrenceAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DeviceType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsNotified = table.Column<bool>(type: "bit", nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolvedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemEventLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemEventLogs_Category",
                table: "SystemEventLogs",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_SystemEventLogs_Category_OccurredAt",
                table: "SystemEventLogs",
                columns: new[] { "Category", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemEventLogs_ErrorCode",
                table: "SystemEventLogs",
                column: "ErrorCode");

            migrationBuilder.CreateIndex(
                name: "IX_SystemEventLogs_IsResolved",
                table: "SystemEventLogs",
                column: "IsResolved");

            migrationBuilder.CreateIndex(
                name: "IX_SystemEventLogs_IsResolved_Severity",
                table: "SystemEventLogs",
                columns: new[] { "IsResolved", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemEventLogs_OccurredAt",
                table: "SystemEventLogs",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_SystemEventLogs_Severity",
                table: "SystemEventLogs",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_SystemEventLogs_Severity_OccurredAt",
                table: "SystemEventLogs",
                columns: new[] { "Severity", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemEventLogs_Status",
                table: "SystemEventLogs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SystemEventLogs_UserId_OccurredAt",
                table: "SystemEventLogs",
                columns: new[] { "UserId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemEventLogs");
        }
    }
}
