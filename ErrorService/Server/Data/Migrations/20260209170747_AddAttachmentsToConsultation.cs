using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentsToConsultation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserEmail",
                table: "ConsultationTickets");

            migrationBuilder.AddColumn<string>(
                name: "AttachmentContentType",
                table: "TicketReplies",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentName",
                table: "TicketReplies",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentUrl",
                table: "TicketReplies",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileContentType",
                table: "ConsultationTickets",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "ConsultationTickets",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileUrl",
                table: "ConsultationTickets",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttachmentContentType",
                table: "TicketReplies");

            migrationBuilder.DropColumn(
                name: "AttachmentName",
                table: "TicketReplies");

            migrationBuilder.DropColumn(
                name: "AttachmentUrl",
                table: "TicketReplies");

            migrationBuilder.DropColumn(
                name: "FileContentType",
                table: "ConsultationTickets");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "ConsultationTickets");

            migrationBuilder.DropColumn(
                name: "FileUrl",
                table: "ConsultationTickets");

            migrationBuilder.AddColumn<string>(
                name: "UserEmail",
                table: "ConsultationTickets",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
