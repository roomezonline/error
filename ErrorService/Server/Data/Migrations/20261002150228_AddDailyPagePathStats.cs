using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyPagePathStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyPagePathStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Path = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PageViews = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyPagePathStats", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyPagePathStats_Date_Path",
                table: "DailyPagePathStats",
                columns: new[] { "Date", "Path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyPagePathStats_Path",
                table: "DailyPagePathStats",
                column: "Path");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyPagePathStats");
        }
    }
}
