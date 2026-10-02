using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingMethods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ShippingCost",
                table: "Orders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ShippingMethodId",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingMethodName",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShippingMethods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EstimatedDays = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingMethods", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShippingMethods");

            migrationBuilder.DropColumn(
                name: "ShippingCost",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingMethodId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingMethodName",
                table: "Orders");
        }
    }
}
