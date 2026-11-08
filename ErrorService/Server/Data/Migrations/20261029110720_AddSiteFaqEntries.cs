using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErrorService.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteFaqEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SiteFaqEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Question = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Answer = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CategoryKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Keywords = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteFaqEntries", x => x.Id);
                });

            // محتوای اولیه از فایل FAQ.razor منتقل می‌شود تا صفحه از روز اول پر باشد.
            migrationBuilder.InsertData(
                table: "SiteFaqEntries",
                columns: new[] { "Id", "Question", "Answer", "CategoryKey", "SortOrder", "IsEnabled", "CreatedAt" },
                values: new object[,]
                {
                    { 1, "چگونه می‌توانم محصولی را خریداری کنم؟", "برای خرید کافیست محصول مورد نظر را به سبد خرید اضافه کنید. سپس از طریق گزینه \"تسویه حساب\" اطلاعات خود را وارد کرده و سفارش خود را ثبت کنید. پس از ثبت سفارش، می‌توانید رسید واریز را آپلود کنید.", "shop", 1, true, new DateTime(2026, 10, 29) },
                    { 2, "آیا امکان خرید حضوری وجود دارد؟", "بله، شما می‌توانید پس از هماهنگی با کارشناسان ما به صورت حضوری مراجعه کرده و محصول مورد نظر را خریداری کنید.", "shop", 2, true, new DateTime(2026, 10, 29) },
                    { 3, "آیا محصولات شامل گارانتی هستند؟", "بله، تمامی محصولات فروشگاه ارورسرویس دارای گارانتی اصالت کالا و ضمانت بازگشت هستند.", "shop", 3, true, new DateTime(2026, 10, 29) },
                    { 4, "چگونه درخواست تعمیر ثبت کنم؟", "از طریق صفحه \"درخواست تعمیر\" می‌توانید اطلاعات دستگاه و مشکل خود را ثبت کنید. کارشناسان ما در اسرع وقت با شما تماس خواهند گرفت.", "repair", 1, true, new DateTime(2026, 10, 29) },
                    { 5, "هزینه تعمیرات چگونه محاسبه می‌شود؟", "هزینه تعمیرات پس از تشخیص اولیه مشکل توسط کارشناس فنی و با تأیید شما تعیین می‌شود.", "repair", 2, true, new DateTime(2026, 10, 29) },
                    { 6, "آیا خدمات تعمیر در منزل ارائه می‌دهید؟", "بله، کارشناسان ما برای تعمیر انواع لوازم خانگی به صورت حضوری در محل حاضر می‌شوند.", "repair", 3, true, new DateTime(2026, 10, 29) },
                    { 7, "چه روش‌های پرداختی وجود دارد؟", "روش‌های پرداخت شامل واریز به کارت و پرداخت آنلاین است. اطلاعات حساب‌های بانکی در صفحه تسویه حساب قابل مشاهده است.", "payment", 1, true, new DateTime(2026, 10, 29) },
                    { 8, "آیا امکان پرداخت در محل وجود دارد؟", "برای سفارشات آنلاین، پرداخت از طریق واریز بانکی انجام می‌شود. برای خدمات تعمیرات می‌توانید پس از اتمام کار پرداخت کنید.", "payment", 2, true, new DateTime(2026, 10, 29) },
                    { 9, "مدت زمان ارسال چقدر است؟", "مدت زمان ارسال بسته به نوع محصول و موقعیت مکانی بین ۱ تا ۵ روز کاری متغیر است.", "shipping", 1, true, new DateTime(2026, 10, 29) },
                    { 10, "هزینه ارسال چقدر است؟", "هزینه ارسال بر اساس وزن و موقعیت مکانی محاسبه شده و در صفحه تسویه به شما نمایش داده می‌شود.", "shipping", 2, true, new DateTime(2026, 10, 29) },
                    { 11, "دوره‌های آموزشی چگونه ارائه می‌شوند؟", "دوره‌های آموزشی به صورت آنلاین و ویدئویی ارائه می‌شوند. پس از ثبت‌نام در دوره، به تمامی درس‌ها دسترسی خواهید داشت.", "academy", 1, true, new DateTime(2026, 10, 29) },
                    { 12, "آیا محتوای آموزشی رایگان هم دارید؟", "بله، مقالات و آموزش‌های رایگان زیادی در بخش آکادمی و مقالات سایت منتشر شده است.", "academy", 2, true, new DateTime(2026, 10, 29) },
                    { 13, "چگونه در سایت ثبت‌نام کنم؟", "از طریق گزینه \"ورود / ثبت‌نام\" در منوی بالای سایت می‌توانید با شماره موبایل خود ثبت‌نام کنید.", "account", 1, true, new DateTime(2026, 10, 29) },
                    { 14, "چگونه سفارشات خود را پیگیری کنم؟", "پس از ورود به حساب کاربری، از بخش \"سفارشات من\" می‌توانید وضعیت تمام سفارشات خود را مشاهده کنید.", "account", 2, true, new DateTime(2026, 10, 29) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteFaqEntries");
        }
    }
}
