namespace ErrorService.Shared;

public sealed record CalculatorCategory(string Key, string Title, string Subtitle, string Icon);

public sealed record CalculatorTool(
    string Key,
    string CategoryKey,
    string Title,
    string ShortDescription,
    string Icon,
    string Keywords);

public sealed record CalculatorFaqItem(string Question, string Answer);

/// <summary>
/// فهرست مشترک ابزارهای صفحه /technical/calculators؛ هم برای رندر صفحه، هم
/// برای کنترل فعال/غیرفعال در پنل ادمین و هم برای محتوای پیش‌رندر خزنده.
/// </summary>
public static class CalculatorCatalog
{
    public static readonly IReadOnlyList<CalculatorCategory> Categories = new List<CalculatorCategory>
    {
        new("components", "قطعات و مدار", "مقاومت، خازن، LED و محاسبات پایه مدار", "oi-list"),
        new("power", "برق، سیم و کابل", "جریان، افت ولتاژ، سطح مقطع و مصرف انرژی", "oi-bolt"),
        new("motors", "موتور و باتری", "خازن موتور تک‌فاز، دور اینورتر و عمر باتری", "oi-power-standby")
    };

    public static readonly IReadOnlyList<CalculatorTool> Tools = new List<CalculatorTool>
    {
        new("ohm-law", "components", "قانون اهم و توان",
            "با وارد کردن دو مقدار از ولتاژ، جریان یا مقاومت، مقدار سوم و توان مصرفی را محاسبه می‌کند.",
            "oi-bolt", "قانون اهم, محاسبه ولتاژ, محاسبه جریان, محاسبه مقاومت, توان الکتریکی, v=ir"),
        new("resistor-color", "components", "کد رنگ مقاومت",
            "مقدار مقاومت را از روی نوارهای رنگی (۴ و ۵ نواری) بخوانید یا برای یک مقدار، رنگ‌های استاندارد را پیدا کنید.",
            "oi-grid-three-up", "کد رنگ مقاومت, رنگ مقاومت, نوار مقاومت, مقاومت ۴ نواری, مقاومت ۵ نواری"),
        new("led-resistor", "components", "مقاومت LED",
            "مقاومت سری و توان لازم برای روشن کردن LED را بر اساس ولتاژ تغذیه، ولتاژ LED و جریان محاسبه می‌کند.",
            "oi-lightbulb", "مقاومت led, مقاومت ال ای دی, محاسبه led, جریان led"),
        new("voltage-divider", "components", "تقسیم ولتاژ",
            "ولتاژ خروجی یک مقسم مقاومتی (R1 و R2) و جریان عبوری آن را محاسبه می‌کند.",
            "oi-resize-height", "تقسیم ولتاژ, مقسم ولتاژ, vout, r1 r2, تقسیم مقاومتی"),
        new("series-parallel", "components", "سری و موازی مقاومت",
            "مقاومت معادل دو تا چهار مقاومت را در اتصال سری یا موازی محاسبه می‌کند.",
            "oi-loop-square", "مقاومت سری, مقاومت موازی, مقاومت معادل, سری و موازی"),
        new("capacitor-code", "components", "کد خازن",
            "کد سه‌رقمی خازن (مثل ۱۰۴ یا ۴۷۳) را به پیکوفاراد، نانوفاراد و میکروفاراد تبدیل می‌کند.",
            "oi-layers", "کد خازن, خازن 104, نانوفاراد, میکروفاراد, کدگذاری خازن"),
        new("rc-time", "components", "زمان ثابت RC",
            "ثابت زمانی مدار RC، فرکانس قطع فیلتر و زمان شارژ خازن تا ۶۳، ۹۵ و ۹۹ درصد را محاسبه می‌کند.",
            "oi-clock", "زمان ثابت rc, فیلتر rc, فرکانس قطع, شارژ خازن, تاو"),
        new("voltage-drop", "power", "افت ولتاژ و سطح مقطع سیم",
            "افت ولتاژ کابل و سطح مقطع پیشنهادی سیم را بر اساس جریان، طول مسیر، جنس هادی و افت مجاز محاسبه می‌کند.",
            "oi-resize-width", "افت ولتاژ کابل, سطح مقطع سیم, انتخاب سیم, محاسبه کابل, ولتاژ دراپ"),
        new("motor-current", "power", "جریان موتور",
            "جریان نامی موتور تک‌فاز و سه‌فاز را از روی توان، ولتاژ، ضریب توان و راندمان محاسبه می‌کند.",
            "oi-pulse", "جریان موتور, آمپر موتور, موتور سه فاز, موتور تک فاز, جریان نامی"),
        new("power-cost", "power", "مصرف برق و هزینه",
            "مصرف روزانه، ماهانه و هزینه تقریبی برق هر وسیله را بر اساس وات، ساعت کارکرد و نرخ هر کیلووات‌ساعت محاسبه می‌کند.",
            "oi-bar-chart", "محاسبه مصرف برق, کیلووات ساعت, هزینه قبض برق, مصرف لوازم خانگی, kwh"),
        new("motor-capacitor", "motors", "خازن موتور تک‌فاز",
            "ظرفیت تقریبی خازن راه‌انداز و خازن دائم‌کار موتور تک‌فاز را بر اساس توان موتور تخمین می‌زند.",
            "oi-power-standby", "خازن راه انداز, خازن دائم کار, خازن موتور تک فاز, میکروفاراد موتور"),
        new("inverter-rpm", "motors", "دور موتور با اینورتر",
            "دور synchronous و واقعی موتور را از فرکانس اینورتر، تعداد قطب و درصد لغزش محاسبه می‌کند.",
            "oi-loop-circular", "دور موتور, rpm, اینورتر, فرکانس, قطب, vfd"),
        new("battery-life", "motors", "عمر باتری",
            "مدت کارکرد باتری را بر اساس ظرفیت (mAh یا Ah)، جریان مصرفی و درصد ظرفیت قابل استفاده محاسبه می‌کند.",
            "oi-battery-full", "عمر باتری, مدت کارکرد باتری, mah, ah, زمان شارژ")
    };

    public static readonly IReadOnlyList<CalculatorFaqItem> Faq = new List<CalculatorFaqItem>
    {
        new("این ماشین‌حساب‌ها رایگان هستند؟",
            "بله، همه ابزارهای صفحه محاسبات فنی ارورسرویس رایگان و بدون نیاز به ثبت‌نام در دسترس هستند. محاسبات مستقیماً در مرورگر انجام می‌شود و اطلاعاتی به سرور ارسال نمی‌شود."),
        new("آیا مقادیر واردشده ذخیره می‌شوند؟",
            "بله، آخرین ورودی هر ابزار در حافظه همان مرورگر (localStorage) ذخیره می‌شود؛ با بازگشت دوباره به صفحه، مقادیر قبلی باقی می‌مانند. اگر حافظه مرورگر را پاک کنید، این اطلاعات هم پاک می‌شود."),
        new("کد رنگ مقاومت را چطور بخوانم؟",
            "در مقاومت چهار نواری، دو نوار اول رقم‌های مقدار، نوار سوم ضریب و نوار چهارم تلرانس است. مثلاً قهوه‌ای، مشکی، قرمز و طلایی یعنی ۱ کیلواهم با تلرانس ۵ درصد. ابزار کد رنگ مقاومت همین محاسبه را برعکس هم انجام می‌دهد."),
        new("افت ولتاژ مجاز کابل چقدر است؟",
            "معمولاً برای مدارهای روشنایی تا ۳ درصد و برای موتورها تا ۵ درصد در نظر گرفته می‌شود. افت بیشتر باعث داغ شدن سیم، کاهش گشتاور موتور و خطای تجهیزات حساس می‌شود."),
        new("جریان موتور سه‌فاز چگونه حساب می‌شود؟",
            "برای موتور سه‌فاز از فرمول I = P ÷ (√۳ × V × cos φ × η) استفاده می‌شود؛ توان بر حسب وات، ولتاژ خط، ضریب توان و راندمان موتور در آن قرار می‌گیرند. برای انتخاب کلید و کنتاکتور، جریان پلاک موتور مرجع اصلی است."),
        new("خازن موتور تک‌فاز را از کجا بفهمم؟",
            "به‌عنوان تخمین، خازن دائم‌کار حدود ۳۰ تا ۵۰ میکروفاراد به ازای هر کیلووات توان موتور است و خازن راه‌انداز چند برابر آن. مقدار قطعی را از پلاک موتور، کاتالوگ سازنده یا خازن قبلی بردارید."),
        new("مصرف برق لوازم خانگی چگونه محاسبه می‌شود؟",
            "کیلووات‌ساعت = توان (وات) × ساعت کارکرد ÷ ۱۰۰۰. با ضرب آن در نرخ هر کیلووات‌ساعت قبض، هزینه تقریبی به‌دست می‌آید؛ توجه کنید که تعرفه برق پلکانی است و مصرف کل خانه در پله‌های بالاتر نرخ بیشتری دارد."),
        new("آیا این ابزارها در موبایل هم کار می‌کنند؟",
            "بله، صفحه و همه ابزارها برای نمایشگر موبایل طراحی شده‌اند و بدون نصب اپلیکیشن در مرورگر کار می‌کنند. می‌توانید صفحه را به صفحه اصلی گوشی اضافه کنید.")
    };

    public static IEnumerable<CalculatorTool> InCategory(string categoryKey)
        => Tools.Where(t => string.Equals(t.CategoryKey, categoryKey, StringComparison.OrdinalIgnoreCase));

    public static CalculatorTool? Find(string key)
        => Tools.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));

    public static HashSet<string> ParseDisabled(string? csv)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(csv)) return set;

        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Find(part) != null) set.Add(part);
        }

        return set;
    }
}
