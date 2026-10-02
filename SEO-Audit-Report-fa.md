# گزارش حرفه‌ای تحلیل SEO، بازدید و رتبه‌گیری ارورسرویس

**تاریخ بررسی:** ۱۹ سپتامبر ۲۰۲۶  
**دامنه‌ی مبنا در کد:** `https://errorservice.ir/`  
**دامنه‌ی بررسی‌شده:** پروژه‌ی `errorservice_new`  
**نتیجه‌ی مدیریتی:** پروژه از نظر پیاده‌سازی اولیه‌ی SEO ضعیف نیست، اما معماری فعلی برای رشد پایدار در Google یک ریسک مهم دارد: بخش زیادی از SEO پس از اجرای Blazor WebAssembly و از طریق JavaScript ساخته می‌شود، در حالی که HTML اولیه عمدتاً یک App Shell است. این روش ممکن است توسط Google در بسیاری از صفحات رندر شود، اما برای سرعت کشف، کنترل Canonical، وضعیت 404، پایداری Snippet و سازگاری با همه‌ی crawlerها بهترین معماری نیست. اولویت اول باید ایجاد خروجی HTML قابل ایندکس برای صفحات عمومی و سپس تکمیل زیرساخت Sitemap، گزارش Search Console، رویدادهای GA4 و معماری محتوایی باشد.

## ۱. جمع‌بندی اجرایی

پروژه در بخش‌هایی مانند عنوان صفحه، توضیحات، Open Graph، Canonical داینامیک، JSON-LD، صفحه‌های محتوایی آکادمی و ثبت Google Analytics امکانات قابل توجهی دارد. صفحه‌ی اصلی در `Client/wwwroot/index.html` دارای عنوان، توضیحات، زبان فارسی، `dir="rtl"`، Canonical، Open Graph، Twitter Card و verification مربوط به Search Console است. صفحه‌ی اصلی نیز از `SeoHeader` و JSON-LD سازمان و وب‌سایت استفاده می‌کند. صفحات کد خطا و مقالات آکادمی هم عنوان، توضیحات، تصویر و داده‌ی ساختاریافته‌ی اختصاصی دارند.

با این حال، این نقاط مثبت با چند ریسک مهم خنثی می‌شوند. Canonical اولیه‌ی HTML برای همه‌ی مسیرها روی `/` قرار دارد و Canonical صحیح بعداً توسط JavaScript تغییر می‌کند. `SeoHeader` نیز در `OnAfterRenderAsync` با JS Interop متادیتا را عوض می‌کند. Google می‌تواند JavaScript را رندر کند، اما مستندات رسمی آن استفاده از HTML اولیه یا رندر سمت سرور را برای سرعت و اطمینان بیشتر ترجیح می‌دهد.[1]

در بررسی مستقیم، شواهدی از Sitemap و Robots قابل اتکا در فایل‌های خوانده‌شده مشاهده نشد. تا زمانی که وجود واقعی این دو فایل در Production و وضعیت آن‌ها در Search Console تأیید نشود، باید آن‌ها را **ریسک باز و اولویت بالا** در نظر گرفت. همچنین وضعیت Google Analytics به یک مقدار `GoogleAnalyticsTag` در تنظیمات پایگاه داده وابسته است و از `App.razor` با `loadGoogleAnalytics` بارگذاری می‌شود؛ بنابراین نصب بودن GA4 از روی سورس قطعی نیست و باید با مرورگر و DebugView تأیید شود.

داده‌ی رتبه، Impression، Click و Query از داخل سورس قابل استخراج نیست. برای اعلام رتبه‌ی واقعی باید Search Console متصل باشد و حداقل داده‌ی ۲۸ تا ۹۰ روزه از گزارش Performance بررسی شود. Search Console داده‌ی حضور در نتایج Google را می‌دهد، در حالی که Analytics رفتار کاربر بعد از ورود را اندازه‌گیری می‌کند؛ این دو ابزار جایگزین یکدیگر نیستند و اعدادشان دقیقاً برابر نخواهد بود.[3]

## ۲. وضعیت فعلی پروژه بر اساس شواهد کد

| حوزه | وضعیت مشاهده‌شده | ارزیابی |
|---|---|---|
| زبان و RTL | `lang="fa"` و `dir="rtl"` در HTML اصلی | مناسب |
| عنوان و توضیحات پایه | در `wwwroot/index.html` موجود است | مناسب برای صفحه‌ی پایه، ناکافی برای همه‌ی Routeها |
| Canonical | مقدار اولیه `/` و تغییر داینامیک پس از Render | ریسک بالا |
| Open Graph و Twitter | در HTML پایه وجود دارد و توسط `SeoHeader` تغییر می‌کند | متوسط؛ نیازمند URL مطلق و کنترل صفحه‌ای |
| JSON-LD | Organization، WebSite، برخی Article/ErrorCodeها وجود دارد | خوب اما نیازمند اعتبارسنجی و پوشش یکدست |
| Search Console verification | در `index.html` وجود دارد | وجود verification به معنی فعال بودن Property و داده نیست |
| Sitemap | در فایل‌های بررسی‌شده تأیید نشد | اولویت بالا برای بررسی و ساخت |
| Robots | در فایل‌های بررسی‌شده تأیید نشد | اولویت بالا برای بررسی و ساخت |
| GA4 | Tag از تنظیمات DB خوانده و با JS بارگذاری می‌شود | نیازمند تست واقعی و رویدادهای تکمیلی |
| ثبت بازدید داخلی | در مقاله‌ی آکادمی `ViewTracker` استفاده شده است | باید ضدتقلب و از GA4 تفکیک شود |
| جست‌وجوی داخلی | مسیر `/search` دارای `noindex, nofollow` است | تصمیم درست؛ باید لینک‌های قابل Crawl به محتوای اصلی وجود داشته باشد |
| صفحات عمومی | محصولات، کدهای خطا، آکادمی، اخبار و صفحات خدمات وجود دارد | ظرفیت محتوایی بالا |
| SSR/Prerender عمومی | معماری فعلی Blazor WebAssembly با App Shell | ریسک مهم برای Crawl و سرعت ایندکس |
| Status Code | در SPA امکان Soft 404 وجود دارد | باید با تست URL ناموجود و Search Console اصلاح شود |
| امنیت تنظیمات | Connection String و JWT Key در `appsettings.json` دیده شد | **بحرانی؛ باید فوراً Rotate شود** |

## ۳. مهم‌ترین مشکل SEO: App Shell و SEO بعد از JavaScript

فایل `Client/wwwroot/index.html` اطلاعات متای پایه را ارائه می‌کند، اما محتوای هر Route در ابتدا از سمت Blazor WebAssembly ساخته می‌شود. کامپوننت `SeoHeader.razor` در `OnAfterRenderAsync` این موارد را به‌وسیله‌ی `seoHelper.replaceAll` تغییر می‌دهد:

- عنوان صفحه؛
- Meta Description؛
- Keywords؛
- Canonical؛
- Open Graph؛
- Twitter؛
- JSON-LD.

این پیاده‌سازی برای کاربر عادی کار می‌کند، اما دو مشکل دارد. نخست، HTML اولیه برای یک URL جزئیات کد خطا هنوز Canonical صفحه‌ی اصلی را دارد. دوم، اگر رندر Google یا هر crawler دیگر ناقص، کند یا متوقف شود، ممکن است صفحه با عنوان و Canonical اشتباه دیده شود. Google رسماً اعلام می‌کند که صفحات JavaScript را در سه مرحله‌ی Crawl، Render و Index پردازش می‌کند، اما رندر ممکن است در صف بماند و رندر سمت سرور یا Pre-rendering برای سرعت کاربر و crawler همچنان ایده‌ی بهتری است.[1]

### راهکار معماری پیشنهادی

برای صفحات عمومی و قابل رتبه‌گیری، یکی از این دو مسیر باید انتخاب شود:

1. **مهاجرت صفحات عمومی به Blazor Web App با SSR/Prerender و Interactive Auto یا Server.** این گزینه برای Home، ErrorCodeDetail، AcademyArticleViewer، AcademyCourseDetails، News، Products و صفحات دسته‌بندی مناسب است.
2. **ایجاد یک لایه‌ی Public SEO جداگانه در ASP.NET Core MVC/Razor Pages.** این لایه HTML کامل و متادیتای نهایی را مستقیماً در پاسخ HTTP تولید کند و اپلیکیشن مدیریتی و تعاملی فعلی جدا بماند.

انتقال کامل پنل Admin به SSR لازم نیست. تمرکز باید روی صفحاتی باشد که قرار است از Google ورودی بگیرند.

## ۴. Canonical، عنوان و Snippet

در `index.html` مقدار زیر به‌صورت اولیه وجود دارد:

```html
<link rel="canonical" href="https://errorservice.ir/" />
```

سپس `SeoHeader` Canonical را با مسیر فعلی تغییر می‌دهد. این الگو ریسک دارد، چون Canonical صحیح باید برای هر صفحه در HTML قابل دسترس باشد. راهنمای Google توصیه می‌کند Canonical در HTML تعیین شود و اگر JavaScript استفاده می‌شود، مقدار نهایی با Canonical اولیه تضاد نداشته باشد.[1]

### اصلاح پیشنهادی

برای هر صفحه‌ی عمومی باید در پاسخ اولیه‌ی HTML این مجموعه وجود داشته باشد:

```html
<title>عنوان یکتای صفحه</title>
<meta name="description" content="توضیح یکتای صفحه">
<link rel="canonical" href="https://errorservice.ir/technical/error-codes/...">
<meta property="og:title" content="عنوان صفحه">
<meta property="og:description" content="توضیح صفحه">
<meta property="og:url" content="https://errorservice.ir/...">
```

عنوان‌ها باید کوتاه، یکتا و دقیقاً مطابق محتوای صفحه باشند. Meta Description باید برای هر URL یکتا و خلاصه‌ی واقعی همان صفحه باشد. Google ممکن است Snippet را از متن صفحه بسازد و همیشه عین Meta Description را نشان نمی‌دهد، اما Description خوب همچنان به فهم و انتخاب نتیجه کمک می‌کند.[2]

## ۵. ظرفیت محتوایی و معماری رتبه‌گیری

پروژه ظرفیت خوبی برای ساخت یک Topic Cluster فارسی دارد. مهم‌ترین دارایی‌های فعلی عبارت‌اند از:

- بانک کدهای خطا با مسیر ساختاریافته‌ی `/technical/error-codes/{Brand}/{DeviceType}/{Code}`؛
- سنسور‌یاب؛
- آکادمی و مقالات؛
- محصولات و دسته‌بندی‌ها؛
- اخبار و خدمات تعمیر؛
- جست‌وجوی داخلی.

این معماری می‌تواند برای عبارت‌هایی مثل «کد خطای ماشین لباسشویی [برند]»، «علت ارور [کد]»، «روش رفع ارور [کد]»، «تعمیر [دستگاه]» و «قطعه‌ی [نام قطعه]» رتبه بسازد. اما برای جلوگیری از صفحات کم‌ارزش باید هر URL محتوای واقعی و متمایز داشته باشد.

### الگوی صفحه‌ی کد خطا

صفحه‌ی `ErrorCodeDetail.razor` از نظر ساختاری شروع خوبی دارد: Breadcrumb، H1، برند، نوع دستگاه، کد، راه‌حل، یادداشت فنی، تصویر و JSON-LD. برای ارتقا باید این موارد اضافه شود:

- بخش «علائم مشاهده‌شده»؛
- علت‌های محتمل با اولویت؛
- مراحل بررسی ایمن؛
- قطعات مرتبط؛
- مدل‌های سازگار؛
- لینک به خطاهای مشابه؛
- تاریخ آخرین بازبینی و نویسنده/بازبین متخصص؛
- FAQ واقعی در صورت وجود سؤال و پاسخ واقعی، نه FAQ مصنوعی برای گرفتن Rich Result.

صفحه‌ی مقاله‌ی آکادمی نیز عنوان، خلاصه، تصویر، تاریخ، نویسنده‌ی تیم و زمان مطالعه دارد. این صفحه باید Article JSON-LD کامل‌تری با `headline`، `description`، `image`، `datePublished`، `dateModified` و `author` داشته باشد. محتوا باید از نظر تجربه و اعتماد تقویت شود؛ مخصوصاً برای موضوعات تعمیراتی که دستورالعمل اشتباه می‌تواند خسارت یا خطر ایمنی ایجاد کند.

## ۶. Sitemap، Robots و Crawl Budget

وجود Sitemap و Robots باید از سه مسیر تأیید شود:

1. `GET https://errorservice.ir/robots.txt`؛
2. `GET https://errorservice.ir/sitemap.xml`؛
3. گزارش Sitemaps و Page Indexing در Search Console.

در سورس‌هایی که مستقیماً بررسی شد، پیاده‌سازی قطعی این دو Endpoint مشاهده نشد. نبود Sitemap الزاماً مانع ایندکس نیست، اما برای سایتی با محصولات، مقالات، اخبار و تعداد زیاد کد خطا باعث می‌شود Google مسیر صفحات مهم را دیرتر یا ناقص‌تر کشف کند. Sitemap باید فقط URLهای Canonical، عمومی، 200 و قابل ایندکس را شامل شود.

Robots باید مسیرهای خصوصی و غیرارزشمند را ببندد، اما نباید CSS، JavaScript یا تصاویر لازم برای رندر صفحات عمومی را مسدود کند. `/admin/`، `/auth/profile`، `/my-orders`، `/wishlist` و مسیرهای داخلی باید از ایندکس خارج باشند. صفحه‌ی `/search` در کد `noindex, nofollow` دارد که تصمیم مناسبی است؛ با این حال لینک‌های نتایج جست‌وجو باید از صفحات عمومی و Crawlable به URLهای اصلی منتهی شوند.

## ۷. جست‌وجوی داخلی و صفحات کم‌ارزش

مسیر `/search` به‌درستی `noindex, nofollow` دارد، زیرا صفحات نتیجه‌ی جست‌وجوی داخلی معمولاً Landing Page مناسب برای Google نیستند. باید مطمئن شد که پارامترهای جست‌وجو، فیلترهای محصولات و Query Stringها صفحات بی‌نهایت تولید نمی‌کنند.

پیشنهاد فنی:

- URLهای Filter و Sort غیرضروری Canonical به دسته‌ی اصلی بدهند.
- صفحات بدون نتیجه‌ی واقعی 404 یا noindex مناسب داشته باشند.
- صفحات کد خطا که داده ندارند Soft 404 نباشند.
- برای Slug و مسیر عددی فقط یک URL نهایی نگه‌داری شود و بقیه 301 شوند.
- نسخه‌ی `/academy/{id}` و `/academy/{slug}` در صورت نمایش یک محتوا باید Canonical واحد داشته باشند.

Google تأکید می‌کند URLهای تکراری منابع Crawl را مصرف می‌کنند و بهتر است برای هر محتوای واحد یک URL ترجیحی مشخص شود.[2]

## ۸. Google Analytics و ثبت بازدید

در `App.razor` مقدار `GoogleAnalyticsTag` از API تنظیمات سایت خوانده می‌شود و سپس `loadGoogleAnalytics` فراخوانی می‌گردد. این یعنی GA4 از نظر طراحی قابل تنظیم از پنل است، اما فقط وقتی واقعاً کار می‌کند که:

- مقدار ذخیره‌شده Measurement ID معتبر باشد؛
- تابع JavaScript در فایل‌های لودشده وجود داشته باشد؛
- اسکریپت با خطای CSP یا ترتیب بارگذاری متوقف نشود؛
- در SPA هنگام تغییر Route، `page_view` جدید ثبت شود؛
- Viewهای Admin، Login و صفحات خصوصی از گزارش عمومی جدا شوند؛
- Consent و حریم خصوصی مطابق بازار هدف پیاده‌سازی شود.

همچنین در مقاله‌ی آکادمی `ViewTracker` استفاده شده و `ViewCount` در UI نمایش داده می‌شود. این شمارنده‌ی داخلی نباید به‌عنوان معیار واقعی بازدید یا رتبه‌گیری تلقی شود. شمارنده‌ی داخلی می‌تواند با Refresh، Bot، Pre-render، کاربر تکراری و درخواست‌های مستقیم دچار خطا شود. باید آن را از Analytics جدا نگه داشت و در سرور با محدودیت زمانی، Cookie/Session، IP Hash امن و حذف Botهای شناخته‌شده محافظت کرد.

### رویدادهای ضروری GA4

برای کسب‌وکار این پروژه، فقط `page_view` کافی نیست. حداقل این رویدادها باید تعریف و با Event/Key Event مناسب گزارش شوند:

| رویداد | زمان ثبت | هدف |
|---|---|---|
| `view_error_code` | باز شدن صفحه‌ی کد خطا | شناخت تقاضای واقعی برای خطاها |
| `search` | اجرای جست‌وجوی داخلی | کشف عبارت‌های بدون پاسخ |
| `view_article` | باز شدن مقاله | ارزیابی محتوا |
| `repair_request_start` | شروع فرم تعمیر | سنجش قیف تبدیل |
| `repair_request_submit` | ارسال موفق فرم | Lead اصلی |
| `contact_click` | کلیک تماس/پیام‌رسان | Lead کمکی |
| `product_view` | باز شدن محصول | تحلیل فروش |
| `add_to_cart` و `purchase` | مراحل فروش | اندازه‌گیری درآمد |

پارامترهای پیشنهادی عبارت‌اند از `content_type`، `content_id`، `brand`، `device_type`، `error_code`، `source` و `landing_page`.

## ۹. Search Console و سنجش رتبه

بدون دسترسی واقعی به Property در Search Console نمی‌توان گفت سایت اکنون رتبه‌ی چندم دارد یا کدام Queryها بیشترین کلیک را می‌گیرند. Verification Tag در `index.html` فقط نشان می‌دهد برای اتصال آماده‌سازی شده و به‌تنهایی داده‌ی Performance را ثابت نمی‌کند.

گزارش پایه باید این ستون‌ها را برای ۲۸، ۹۰ و ۱۸۰ روز داشته باشد:

| بخش | شاخص‌ها |
|---|---|
| Queries | Impressions، Clicks، CTR، Average Position |
| Pages | Landing Page، Clicks، CTR، Position |
| Device | Mobile/Desktop، CTR، Position |
| Country | کشور، Impression، Click |
| Indexing | Indexed، Excluded، Crawled، Discovered |
| Experience | Core Web Vitals، HTTPS، Mobile Usability |

Search Console روی قبل از ورود کاربر از Google تمرکز دارد. Analytics روی رفتار بعد از ورود تمرکز می‌کند. Google صراحتاً توصیه می‌کند این دو را کنار هم تحلیل کنید و تفاوت Click و Session را طبیعی بدانید.[3]

## ۱۰. Core Web Vitals و سرعت

در `index.html` چند Preload و Preconnect وجود دارد که نشان می‌دهد به PageSpeed توجه شده است. با این حال، وجود Bootstrap، چند کتابخانه‌ی نمودار، Quill، TinyMCE، Plyr، HLS، Jalali Datepicker و چند فایل JS در App Shell می‌تواند LCP و INP را در موبایل افزایش دهد. صفحه‌ی اصلی نیز پس از اجرای اپ داده‌ها را از API می‌گیرد و Skeleton نمایش می‌دهد؛ این رفتار برای UX خوب است، اما برای SEO جای HTML اولیه‌ی محتوایی را نمی‌گیرد.

اهداف فنی توصیه‌شده توسط Google عبارت‌اند از LCP حداکثر ۲٫۵ ثانیه، INP کمتر از ۲۰۰ میلی‌ثانیه و CLS کمتر از ۰٫۱.[4]

اقدامات پیشنهادی:

- لود شرطی Chart.js، TinyMCE، Plyr و HLS فقط در صفحات لازم؛
- تبدیل تصاویر Hero و OG به WebP/AVIF با ابعاد واقعی؛
- تعیین `width` و `height` یا `aspect-ratio` برای تصاویر؛
- حذف Preloadهای غیرضروری؛
- فعال‌سازی Brotli در Production در کنار Gzip؛
- Cache بلندمدت برای فایل‌های versioned؛
- اندازه‌گیری LCP/INP/CLS در موبایل واقعی از PageSpeed و CrUX؛
- کاهش JS اولیه و انتقال محتوای عمومی مهم به HTML SSR.

## ۱۱. ریسک بحرانی امنیتی که روی SEO و اعتماد هم اثر دارد

در `ErrorService/Server/appsettings.json` Connection String پایگاه داده با نام کاربری و رمز عبور و همچنین `Auth:JwtKey` به‌صورت متن ساده دیده می‌شود. این موضوع مستقیماً یک مشکل SEO نیست، اما از نظر امنیتی بحرانی است. افشای Database Credential می‌تواند باعث خرابکاری، تزریق محتوا، ایجاد صفحات اسپم، تغییر Redirectها، سرقت داده و در نهایت حذف سایت از نتایج Google شود.

اقدام فوری:

1. رمز دیتابیس را Rotate کنید.
2. JWT Key را فوراً تغییر دهید و همه‌ی Tokenهای قبلی را بی‌اعتبار کنید.
3. Secretها را از Git و فایل Production خارج کنید.
4. از Environment Variable یا Secret Store استفاده کنید.
5. تاریخچه‌ی Git را برای Credentialهای قبلی پاک‌سازی امن کنید.
6. Connection String را با حداقل دسترسی و ترجیحاً Encrypt تنظیم کنید.

## ۱۲. نقشه راه اولویت‌بندی‌شده

### فاز صفر: اقدامات فوری، ۱ روز

- Rotate کردن Database Password و JWT Key؛
- تأیید Production با `curl -I` برای HTTPS، www/non-www، صفحه‌ی اصلی و یک URL ناموجود؛
- بررسی واقعی وجود `robots.txt` و `sitemap.xml`؛
- اتصال Search Console و ثبت Sitemap؛
- بررسی GA4 با Tag Assistant و DebugView؛
- جلوگیری از ایندکس Admin، Login، Profile، Orders، Wishlist و Search.

### فاز یک: زیرساخت SEO، ۲ تا ۵ روز

- ساخت Sitemap داینامیک برای ErrorCode، Article، Course، Product و News؛
- ایجاد Robots صحیح و اشاره به Sitemap؛
- اصلاح 404 واقعی برای داده‌ی ناموجود و حذف Soft 404؛
- ایجاد Canonical نهایی در HTML اولیه؛
- حذف Canonical اولیه‌ی `/` از مسیرهایی که صفحه‌ی اختصاصی دارند؛
- اضافه کردن `dateModified` و `author` به Article JSON-LD؛
- اعتبارسنجی JSON-LD با Rich Results Test و Schema Markup Validator.

### فاز دو: قابلیت ایندکس و محتوا، ۱ تا ۳ هفته

- SSR/Prerender برای صفحات عمومی؛
- تعریف قالب محتوایی استاندارد برای هر کد خطا؛
- ایجاد صفحات Hub برای برند، دستگاه و دسته‌ی خطا؛
- لینک‌سازی داخلی بین ErrorCode، Article، Product و Service؛
- اصلاح Slugهای فارسی/لاتین و حذف URLهای تکراری؛
- تولید محتوای مسئله‌محور بر اساس Queryهای واقعی Search Console و جست‌وجوی داخلی.

### فاز سه: سنجش رشد، مستمر

- تعریف Eventهای GA4 و Key Eventهای Lead؛
- ساخت داشبورد Looker Studio با Search Console و GA4؛
- گزارش هفتگی صفحات با Impression بالا و CTR پایین؛
- بهینه‌سازی Title و Description صفحات دارای Impression بالا؛
- گزارش ماهانه‌ی Queryهای در رتبه‌ی ۴ تا ۲۰ برای فشار دادن آن‌ها به صفحه‌ی اول؛
- پایش Core Web Vitals و Crawl Errors.

## ۱۳. اولویت نهایی اصلاحات

اگر فقط امکان انجام پنج کار وجود داشته باشد، ترتیب پیشنهادی من این است:

1. **چرخش فوری Secretهای افشاشده.**
2. **Sitemap، Robots و تأیید Page Indexing در Search Console.**
3. **رفع معماری Canonical و تولید HTML قابل ایندکس برای صفحات عمومی.**
4. **تکمیل GA4 با page_view در SPA و Eventهای تبدیل.**
5. **ساخت Topic Cluster واقعی برای کد خطا و مقالات آموزشی.**

### نتیجه نهایی

پروژه ظرفیت رشد SEO دارد و از نظر تعداد بخش‌های محتوایی حتی از یک سایت معرفی ساده قوی‌تر است. مشکل اصلی کمبود «متاتگ» نیست؛ مشکل اصلی **اتکای بیش از حد به تغییرات بعد از Render، نبود شواهد قطعی Sitemap/Robots، نبود اندازه‌گیری یکپارچه‌ی Search Console و GA4، و نبود برنامه‌ی محتوایی مبتنی بر Query واقعی** است. با رفع این چهار محور، پروژه می‌تواند از یک اپلیکیشن دارای محتوای پراکنده به یک پلتفرم قابل اندازه‌گیری و قابل رشد در جست‌وجوی فارسی تبدیل شود.

## References

[1]: https://developers.google.com/search/docs/crawling-indexing/javascript/javascript-seo-basics "Understand JavaScript SEO basics - Google Search Central"
[2]: https://developers.google.com/search/docs/fundamentals/seo-starter-guide "SEO Starter Guide - Google Search Central"
[3]: https://developers.google.com/search/docs/monitor-debug/google-analytics-search-console "Using Search Console and Google Analytics data for SEO - Google Search Central"
[4]: https://developers.google.com/search/docs/appearance/core-web-vitals "Understanding Core Web Vitals and Google Search results - Google Search Central"
[5]: https://developers.google.com/search/docs/crawling-indexing/robots/intro "Robots.txt Introduction and Guide - Google Search Central"
[6]: https://search.google.com/search-console/about "Google Search Console - official overview"

---

**یادداشت دامنه‌ی بررسی:** این گزارش بر اساس خواندن فایل‌های کلیدی پروژه و مقایسه با مستندات رسمی Google تهیه شده است. رتبه، Impression، Click و وضعیت واقعی Crawl بدون دسترسی به Search Console و تست مستقیم Production قابل اعلام نیستند و باید در فاز اندازه‌گیری تأیید شوند.
