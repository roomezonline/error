using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteToWorkshopAndUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "WorkshopUsers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "WorkshopUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Workshops",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Workshops",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_WorkshopUsers_IsDeleted",
                table: "WorkshopUsers",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Workshops_IsDeleted",
                table: "Workshops",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkshopUsers_IsDeleted",
                table: "WorkshopUsers");

            migrationBuilder.DropIndex(
                name: "IX_Workshops_IsDeleted",
                table: "Workshops");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "WorkshopUsers");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "WorkshopUsers");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Workshops");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Workshops");
        }
    }
}
