using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEntitySlugs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "TrainingCourses",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "TrainingArticles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Products",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "News",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql(
                $"""
                UPDATE [Products] SET [Slug] = {Slugify("Name")} WHERE [Slug] IS NULL;
                UPDATE [News] SET [Slug] = {Slugify("Title")} WHERE [Slug] IS NULL;
                UPDATE [TrainingCourses] SET [Slug] = {Slugify("Title")} WHERE [Slug] IS NULL;
                UPDATE [TrainingArticles] SET [Slug] = {Slugify("Title")} WHERE [Slug] IS NULL;
                """);

            migrationBuilder.Sql("""
                WITH cte AS (SELECT Id, Slug, ROW_NUMBER() OVER (PARTITION BY Slug ORDER BY Id) AS rn FROM [Products])
                UPDATE cte SET Slug = LEFT(Slug + N'-' + CAST(Id AS nvarchar(20)), 200) WHERE rn > 1;
                """);
            migrationBuilder.Sql("""
                WITH cte AS (SELECT Id, Slug, ROW_NUMBER() OVER (PARTITION BY Slug ORDER BY Id) AS rn FROM [News])
                UPDATE cte SET Slug = LEFT(Slug + N'-' + CAST(Id AS nvarchar(20)), 200) WHERE rn > 1;
                """);
            migrationBuilder.Sql("""
                WITH cte AS (SELECT Id, Slug, ROW_NUMBER() OVER (PARTITION BY Slug ORDER BY Id) AS rn FROM [TrainingCourses])
                UPDATE cte SET Slug = LEFT(Slug + N'-' + CAST(Id AS nvarchar(20)), 200) WHERE rn > 1;
                """);
            migrationBuilder.Sql("""
                WITH cte AS (SELECT Id, Slug, ROW_NUMBER() OVER (PARTITION BY Slug ORDER BY Id) AS rn FROM [TrainingArticles])
                UPDATE cte SET Slug = LEFT(Slug + N'-' + CAST(Id AS nvarchar(20)), 200) WHERE rn > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingCourses_Slug",
                table: "TrainingCourses",
                column: "Slug",
                unique: true,
                filter: "[Slug] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingArticles_Slug",
                table: "TrainingArticles",
                column: "Slug",
                unique: true,
                filter: "[Slug] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Slug",
                table: "Products",
                column: "Slug",
                unique: true,
                filter: "[Slug] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_News_CreatedAt",
                table: "News",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_News_IsPublished",
                table: "News",
                column: "IsPublished");

            migrationBuilder.CreateIndex(
                name: "IX_News_Slug",
                table: "News",
                column: "Slug",
                unique: true,
                filter: "[Slug] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrainingCourses_Slug",
                table: "TrainingCourses");

            migrationBuilder.DropIndex(
                name: "IX_TrainingArticles_Slug",
                table: "TrainingArticles");

            migrationBuilder.DropIndex(
                name: "IX_Products_Slug",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_News_CreatedAt",
                table: "News");

            migrationBuilder.DropIndex(
                name: "IX_News_IsPublished",
                table: "News");

            migrationBuilder.DropIndex(
                name: "IX_News_Slug",
                table: "News");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "TrainingCourses");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "TrainingArticles");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "News");
        }

        private static string Slugify(string column)
        {
            var s = $"REPLACE(REPLACE(ISNULL([{column}], N''), N'ي', N'ی'), N'ك', N'ک')";
            foreach (var ch in new[]
                     {
                         "\u060C", "\u061B", "\u061F", "!", ".", ",", ":", ";", "(", ")", "[", "]", "{", "}",
                         "\"", "'", "<", ">", "|", "\\", "/", "*", "&", "%", "+", "=", "~", "^", "#", "@", "$",
                         "\u00AB", "\u00BB", "\u200C", "\u200F"
                     })
            {
                var lit = "'" + ch.Replace("'", "''") + "'";
                s = $"REPLACE({s}, N{lit}, N'')";
            }

            s = "REPLACE(REPLACE(" + s + $", N' ', N'-'), N'\t', N'-')";
            for (var i = 0; i < 6; i++)
                s = $"REPLACE({s}, N'--', N'-')";

            s = $"REPLACE(REPLACE(LTRIM(RTRIM(REPLACE({s}, N'-', N' '))), N' ', N'-'), N'--', N'-')";

            return $"LOWER(LEFT({s}, 190))";
        }
    }
}
