using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceVoiceEventDeliveredAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeliveredAt",
                table: "DeviceVoiceEvents",
                type: "datetimeoffset",
                nullable: true);

            // رویدادهای ثبت‌شده پیش از فعال‌شدن ردیابی تحویل، در گزارش با برچسب
            // «پخش نشد» نمایش داده می‌شدند؛ از این پس زمان تحویل واقعی ثبت می‌شود
            // و برای ردیف‌های قدیمی فرض می‌کنیم در همان لحظهٔ ثبت تحویل شده‌اند.
            migrationBuilder.Sql(
                "UPDATE [DeviceVoiceEvents] SET [DeliveredAt] = [CreatedAt] WHERE [DeliveredAt] IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "DeviceVoiceEvents");
        }
    }
}
