using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContentStudioManualDraftAndTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoKeywordTags",
                table: "ContentStudioSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxTagsPerItem",
                table: "ContentStudioSettings",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<bool>(
                name: "IsManualDraft",
                table: "ContentStudioItems",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoKeywordTags",
                table: "ContentStudioSettings");

            migrationBuilder.DropColumn(
                name: "MaxTagsPerItem",
                table: "ContentStudioSettings");

            migrationBuilder.DropColumn(
                name: "IsManualDraft",
                table: "ContentStudioItems");
        }
    }
}
