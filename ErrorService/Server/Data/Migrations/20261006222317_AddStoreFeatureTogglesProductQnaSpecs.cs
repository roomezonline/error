using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreFeatureTogglesProductQnaSpecs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableDeliveryEta",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableFreeShipping",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableHomeBestSellers",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableHomeRestocked",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnablePriceHistoryChart",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableProductBadges",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableProductQna",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableProductReviews",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableRecentlyViewed",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableRelatedProducts",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableReorder",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableReturnRequests",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableReviewMedia",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableSpecsTable",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableStockAlert",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableTrustBar",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableWishlist",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FreeShippingThreshold",
                table: "SiteSettings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FreeShippingThreshold",
                table: "ShippingMethods",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaUrl",
                table: "ProductReviews",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductPriceHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPriceHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductPriceHistories_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    AppUserId = table.Column<int>(type: "int", nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Question = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Answer = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    IsAnswered = table.Column<bool>(type: "bit", nullable: false),
                    IsAdminRead = table.Column<bool>(type: "bit", nullable: false),
                    UpVotes = table.Column<int>(type: "int", nullable: false),
                    VotedUserIds = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AnsweredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductQuestions_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductQuestions_Users_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductSpecs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    SpecKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SpecValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSpecs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductSpecs_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 1,
                column: "FreeShippingThreshold",
                value: null);

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 2,
                column: "FreeShippingThreshold",
                value: null);

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 3,
                column: "FreeShippingThreshold",
                value: null);

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 4,
                column: "FreeShippingThreshold",
                value: null);

            migrationBuilder.UpdateData(
                table: "ShippingMethods",
                keyColumn: "Id",
                keyValue: 5,
                column: "FreeShippingThreshold",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_ProductPriceHistories_ProductId_RecordedAt",
                table: "ProductPriceHistories",
                columns: new[] { "ProductId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductQuestions_AppUserId",
                table: "ProductQuestions",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductQuestions_ProductId_IsApproved_CreatedAt",
                table: "ProductQuestions",
                columns: new[] { "ProductId", "IsApproved", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductSpecs_ProductId_SortOrder",
                table: "ProductSpecs",
                columns: new[] { "ProductId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductPriceHistories");

            migrationBuilder.DropTable(
                name: "ProductQuestions");

            migrationBuilder.DropTable(
                name: "ProductSpecs");

            migrationBuilder.DropColumn(
                name: "EnableDeliveryEta",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableFreeShipping",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableHomeBestSellers",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableHomeRestocked",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnablePriceHistoryChart",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableProductBadges",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableProductQna",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableProductReviews",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableRecentlyViewed",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableRelatedProducts",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableReorder",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableReturnRequests",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableReviewMedia",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableSpecsTable",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableStockAlert",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableTrustBar",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "EnableWishlist",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "FreeShippingThreshold",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "FreeShippingThreshold",
                table: "ShippingMethods");

            migrationBuilder.DropColumn(
                name: "MediaUrl",
                table: "ProductReviews");
        }
    }
}
