using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiProviderProxies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatAiProxies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProviderId = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ConsecutiveFailures = table.Column<int>(type: "int", nullable: false),
                    CooldownUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuccessCount = table.Column<long>(type: "bigint", nullable: false),
                    FailureCount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatAiProxies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatAiProxies_ChatAiProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "ChatAiProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatAiProxies_ProviderId",
                table: "ChatAiProxies",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatAiProxies_ProviderId_Priority",
                table: "ChatAiProxies",
                columns: new[] { "ProviderId", "Priority" });

            migrationBuilder.Sql(@"
                INSERT INTO ChatAiProxies (ProviderId, Url, Label, Enabled, Priority, ConsecutiveFailures, SuccessCount, FailureCount)
                SELECT Id, LTRIM(RTRIM(ProxyUrl)), N'پروکسی قبلی', 1, 1, 0, 0, 0
                FROM ChatAiProviders
                WHERE ProxyUrl IS NOT NULL AND LTRIM(RTRIM(ProxyUrl)) <> '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatAiProxies");
        }
    }
}
