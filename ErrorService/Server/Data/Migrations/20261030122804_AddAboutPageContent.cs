using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAboutPageContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AboutPageContents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HeroBadge = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    HeroTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HeroSubtitle = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    HeroImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StoryTitle = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    StoryText = table.Column<string>(type: "nvarchar(max)", maxLength: 6000, nullable: true),
                    MissionTitle = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    MissionText = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    VisionTitle = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    VisionText = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    ValuesTitle = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ValuesText = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    FeaturesTitle = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    FeaturesSubtitle = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    FeaturesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShowStats = table.Column<bool>(type: "bit", nullable: false),
                    ShowStatErrorCodes = table.Column<bool>(type: "bit", nullable: false),
                    ShowStatServices = table.Column<bool>(type: "bit", nullable: false),
                    ShowStatProducts = table.Column<bool>(type: "bit", nullable: false),
                    ShowStatAcademy = table.Column<bool>(type: "bit", nullable: false),
                    ShowStatMonitoring = table.Column<bool>(type: "bit", nullable: false),
                    ShowStatWorkshops = table.Column<bool>(type: "bit", nullable: false),
                    ShowStatExperience = table.Column<bool>(type: "bit", nullable: false),
                    FoundingYear = table.Column<int>(type: "int", nullable: false),
                    ShowTeam = table.Column<bool>(type: "bit", nullable: false),
                    ShowTestimonials = table.Column<bool>(type: "bit", nullable: false),
                    CtaTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CtaText = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    CtaButtonText = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CtaButtonUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SeoTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SeoDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SeoKeywords = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AboutPageContents", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AboutPageContents");
        }
    }
}
