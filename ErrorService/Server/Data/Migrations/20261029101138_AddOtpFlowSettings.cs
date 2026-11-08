using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOtpFlowSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OtpAdmissionExpertiseEnabled",
                table: "SmsSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "OtpAdmissionRepairEnabled",
                table: "SmsSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "OtpAdmissionTrackingEnabled",
                table: "SmsSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "OtpCodeTtlSeconds",
                table: "SmsSettings",
                type: "int",
                nullable: false,
                defaultValue: 120);

            migrationBuilder.AddColumn<int>(
                name: "OtpMaxAttempts",
                table: "SmsSettings",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<bool>(
                name: "OtpPasswordResetEnabled",
                table: "SmsSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "OtpRegistrationEnabled",
                table: "SmsSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OtpResendCooldownSeconds",
                table: "SmsSettings",
                type: "int",
                nullable: false,
                defaultValue: 60);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OtpAdmissionExpertiseEnabled",
                table: "SmsSettings");

            migrationBuilder.DropColumn(
                name: "OtpAdmissionRepairEnabled",
                table: "SmsSettings");

            migrationBuilder.DropColumn(
                name: "OtpAdmissionTrackingEnabled",
                table: "SmsSettings");

            migrationBuilder.DropColumn(
                name: "OtpCodeTtlSeconds",
                table: "SmsSettings");

            migrationBuilder.DropColumn(
                name: "OtpMaxAttempts",
                table: "SmsSettings");

            migrationBuilder.DropColumn(
                name: "OtpPasswordResetEnabled",
                table: "SmsSettings");

            migrationBuilder.DropColumn(
                name: "OtpRegistrationEnabled",
                table: "SmsSettings");

            migrationBuilder.DropColumn(
                name: "OtpResendCooldownSeconds",
                table: "SmsSettings");
        }
    }
}
