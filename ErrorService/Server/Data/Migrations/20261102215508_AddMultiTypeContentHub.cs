using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTypeContentHub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CanonicalUrl",
                table: "News",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "News",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContentType",
                table: "News",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpireAt",
                table: "News",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                table: "News",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PinnedUntil",
                table: "News",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishAt",
                table: "News",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeoDescription",
                table: "News",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeoTitle",
                table: "News",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceName",
                table: "News",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "News",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ViewCount",
                table: "News",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ContentCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ContentType = table.Column<int>(type: "int", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TipsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AnnouncementsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    NewsCommentsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    TipsCommentsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AnnouncementsCommentsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ShowViewCount = table.Column<bool>(type: "bit", nullable: false),
                    ShowTableOfContents = table.Column<bool>(type: "bit", nullable: false),
                    ShowAuthorBox = table.Column<bool>(type: "bit", nullable: false),
                    ShowRelatedContent = table.Column<bool>(type: "bit", nullable: false),
                    ShowReadingTime = table.Column<bool>(type: "bit", nullable: false),
                    ShowReadingProgress = table.Column<bool>(type: "bit", nullable: false),
                    FeaturedEnabled = table.Column<bool>(type: "bit", nullable: false),
                    PinnedEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ScheduledPublishingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    PageSize = table.Column<int>(type: "int", nullable: false),
                    RelatedCount = table.Column<int>(type: "int", nullable: false),
                    HomeCardCount = table.Column<int>(type: "int", nullable: false),
                    HomeListCount = table.Column<int>(type: "int", nullable: false),
                    HomeTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HomeTypes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DefaultAuthorName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_News_CategoryId_IsPublished",
                table: "News",
                columns: new[] { "CategoryId", "IsPublished" });

            migrationBuilder.CreateIndex(
                name: "IX_News_ContentType_IsPublished_CreatedAt",
                table: "News",
                columns: new[] { "ContentType", "IsPublished", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_News_PublishAt",
                table: "News",
                column: "PublishAt");

            migrationBuilder.CreateIndex(
                name: "IX_ContentCategories_ContentType_IsActive_SortOrder",
                table: "ContentCategories",
                columns: new[] { "ContentType", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentCategories_Slug",
                table: "ContentCategories",
                column: "Slug",
                unique: true,
                filter: "[Slug] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_News_ContentCategories_CategoryId",
                table: "News",
                column: "CategoryId",
                principalTable: "ContentCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_News_ContentCategories_CategoryId",
                table: "News");

            migrationBuilder.DropTable(
                name: "ContentCategories");

            migrationBuilder.DropTable(
                name: "ContentSettings");

            migrationBuilder.DropIndex(
                name: "IX_News_CategoryId_IsPublished",
                table: "News");

            migrationBuilder.DropIndex(
                name: "IX_News_ContentType_IsPublished_CreatedAt",
                table: "News");

            migrationBuilder.DropIndex(
                name: "IX_News_PublishAt",
                table: "News");

            migrationBuilder.DropColumn(
                name: "CanonicalUrl",
                table: "News");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "News");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "News");

            migrationBuilder.DropColumn(
                name: "ExpireAt",
                table: "News");

            migrationBuilder.DropColumn(
                name: "IsFeatured",
                table: "News");

            migrationBuilder.DropColumn(
                name: "PinnedUntil",
                table: "News");

            migrationBuilder.DropColumn(
                name: "PublishAt",
                table: "News");

            migrationBuilder.DropColumn(
                name: "SeoDescription",
                table: "News");

            migrationBuilder.DropColumn(
                name: "SeoTitle",
                table: "News");

            migrationBuilder.DropColumn(
                name: "SourceName",
                table: "News");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "News");

            migrationBuilder.DropColumn(
                name: "ViewCount",
                table: "News");
        }
    }
}
