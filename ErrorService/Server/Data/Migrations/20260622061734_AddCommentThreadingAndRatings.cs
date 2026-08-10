using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentThreadingAndRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductReviews_ProductId",
                table: "ProductReviews");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "TrainingArticleComments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "TrainingArticleComments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Rating",
                table: "TrainingArticleComments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "ProductReviews",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NewsComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NewsId = table.Column<int>(type: "int", nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Rating = table.Column<int>(type: "int", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsComments_NewsComments_ParentId",
                        column: x => x.ParentId,
                        principalTable: "NewsComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NewsComments_News_NewsId",
                        column: x => x.NewsId,
                        principalTable: "News",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrainingArticleComments_ParentId",
                table: "TrainingArticleComments",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviews_ParentId",
                table: "ProductReviews",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviews_ProductId_IsApproved",
                table: "ProductReviews",
                columns: new[] { "ProductId", "IsApproved" });

            migrationBuilder.CreateIndex(
                name: "IX_NewsComments_NewsId_IsApproved",
                table: "NewsComments",
                columns: new[] { "NewsId", "IsApproved" });

            migrationBuilder.CreateIndex(
                name: "IX_NewsComments_ParentId",
                table: "NewsComments",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductReviews_ProductReviews_ParentId",
                table: "ProductReviews",
                column: "ParentId",
                principalTable: "ProductReviews",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TrainingArticleComments_TrainingArticleComments_ParentId",
                table: "TrainingArticleComments",
                column: "ParentId",
                principalTable: "TrainingArticleComments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductReviews_ProductReviews_ParentId",
                table: "ProductReviews");

            migrationBuilder.DropForeignKey(
                name: "FK_TrainingArticleComments_TrainingArticleComments_ParentId",
                table: "TrainingArticleComments");

            migrationBuilder.DropTable(
                name: "NewsComments");

            migrationBuilder.DropIndex(
                name: "IX_TrainingArticleComments_ParentId",
                table: "TrainingArticleComments");

            migrationBuilder.DropIndex(
                name: "IX_ProductReviews_ParentId",
                table: "ProductReviews");

            migrationBuilder.DropIndex(
                name: "IX_ProductReviews_ProductId_IsApproved",
                table: "ProductReviews");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "TrainingArticleComments");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "TrainingArticleComments");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "TrainingArticleComments");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "ProductReviews");

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviews_ProductId",
                table: "ProductReviews",
                column: "ProductId");
        }
    }
}
