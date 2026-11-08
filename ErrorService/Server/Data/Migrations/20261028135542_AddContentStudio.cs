using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContentStudio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentStudioRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Trigger = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SourcesChecked = table.Column<int>(type: "int", nullable: false),
                    Fetched = table.Column<int>(type: "int", nullable: false),
                    Generated = table.Column<int>(type: "int", nullable: false),
                    Duplicates = table.Column<int>(type: "int", nullable: false),
                    Skipped = table.Column<int>(type: "int", nullable: false),
                    Published = table.Column<int>(type: "int", nullable: false),
                    Failed = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentStudioRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentStudioSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    AutoPublish = table.Column<bool>(type: "bit", nullable: false),
                    DailyRunHour = table.Column<int>(type: "int", nullable: false),
                    MaxItemsPerRun = table.Column<int>(type: "int", nullable: false),
                    DailyPublishLimit = table.Column<int>(type: "int", nullable: false),
                    DuplicateThreshold = table.Column<double>(type: "float", nullable: false),
                    MaxCopyOverlapPercent = table.Column<int>(type: "int", nullable: false),
                    MinContentChars = table.Column<int>(type: "int", nullable: false),
                    AiTemperature = table.Column<double>(type: "float", nullable: false),
                    AiMaxTokens = table.Column<int>(type: "int", nullable: false),
                    AuthorName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IncludeSourceCredit = table.Column<bool>(type: "bit", nullable: false),
                    LastRunAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastRunStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LastRunSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastScheduledRunDate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ScheduledRunAttempts = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentStudioSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentStudioSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FeedUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SiteUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DefaultTags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IncludeKeywords = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ExcludeKeywords = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    AutoPublishMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MaxItemsPerDay = table.Column<int>(type: "int", nullable: false),
                    ImageMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FallbackImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    LastFetchedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentStudioSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentStudioItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SourceUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SourceUrlHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OriginalTitle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OriginalContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SourcePublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    GeneratedTitle = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    GeneratedSummary = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    GeneratedContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GeneratedTags = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StatusDetail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CopyOverlapPercent = table.Column<double>(type: "float", nullable: true),
                    TitleSimilarity = table.Column<double>(type: "float", nullable: true),
                    NewsId = table.Column<int>(type: "int", nullable: true),
                    NewsIsPublished = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentStudioItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentStudioItems_ContentStudioSources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "ContentStudioSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentStudioItems_NewsId",
                table: "ContentStudioItems",
                column: "NewsId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentStudioItems_SourceId",
                table: "ContentStudioItems",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentStudioItems_SourceUrlHash",
                table: "ContentStudioItems",
                column: "SourceUrlHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentStudioItems_Status_CreatedAt",
                table: "ContentStudioItems",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentStudioRuns_StartedAt",
                table: "ContentStudioRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ContentStudioSources_Enabled",
                table: "ContentStudioSources",
                column: "Enabled");

            migrationBuilder.CreateIndex(
                name: "IX_ContentStudioSources_Priority",
                table: "ContentStudioSources",
                column: "Priority");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentStudioItems");

            migrationBuilder.DropTable(
                name: "ContentStudioRuns");

            migrationBuilder.DropTable(
                name: "ContentStudioSettings");

            migrationBuilder.DropTable(
                name: "ContentStudioSources");
        }
    }
}
