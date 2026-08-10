using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddErrorCodeDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ErrorCodeDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ErrorCodeId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DocType = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErrorCodeDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErrorCodeDocuments_ErrorCodes_ErrorCodeId",
                        column: x => x.ErrorCodeId,
                        principalTable: "ErrorCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorCodes_Brand_DeviceType_Code",
                table: "ErrorCodes",
                columns: new[] { "Brand", "DeviceType", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorCodeDocuments_ErrorCodeId_SortOrder",
                table: "ErrorCodeDocuments",
                columns: new[] { "ErrorCodeId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErrorCodeDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ErrorCodes_Brand_DeviceType_Code",
                table: "ErrorCodes");
        }
    }
}
