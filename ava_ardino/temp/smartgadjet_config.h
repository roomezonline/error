#pragma once

// ==================== پیکربندی سراسری پروژه ====================
// تمام پین‌ها، آدرس سرور، تایمرها، تعداد تلاش‌ها، شمارهٔ دستگاه و پارامترهای
// قابل تنظیم اینجاست. برای تغییر رفتار دستگاه فقط همین فایل را ویرایش کن.
// واحد همهٔ زمان‌ها میلی‌ثانیه (ms) است مگر خلافش نوشته شده باشد.

// ==================== شناسهٔ دستگاه (خط تولید) ====================
// شمارهٔ این دستگاه (مثلاً smart-001 ... smart-999).
// در خط تولید: برای هر دستگاه این مقدار را عوض کن، فلش کن و برچسب بزن.
#define DEVICE_NUMBER         "smart-002"

// «حک شدن» شماره در حافظهٔ دستگاه (NVS — معادل EEPROM در ESP32، با فلش مجدد پاک نمی‌شود):
//   1 = هنگام بوت، شمارهٔ بالا در حافظه نوشته می‌شود (بار اول / خط تولید)
//   0 = چیزی نوشته نمی‌شود؛ شمارهٔ موجود در حافظه معتبر می‌ماند
//       (قبل از فلش فرمور جدید روی دستگاه‌های موجود این را 0 کن تا شماره‌ها دست‌نخورده بمانند)
//
// اگر فلگ 0 باشد و حافظه هم خالی باشد (دستگاه بدون شماره)، سرور خودش شمارهٔ خودکار می‌دهد.
#define BURN_DEVICE_NUMBER    1

// ==================== سرور و API ====================
// آدرسی که دستگاه پیام‌ها را از آن می‌خواند = همان آدرس پنل وب.
// - HTTP خام (بدون TLS) است چون AudioFileSourceHTTPStream از https پشتیبانی نمی‌کند.
// - طول URL (شامل توکن ~۳۲ کاراکتری) باید زیر ۱۲۸ کاراکتر بماند (بافر کتابخانهٔ صوتی).
#define SERVER_BASE  "http://bot.errorservice.ir"

// برای تست روی کامپیوتر خودت (آی‌پی کامپیوتر در شبکهٔ وای‌فای) این خط را فعال کن:
// #define SERVER_BASE  "http://192.168.1.50:5181"

#define DEVICE_FW             "1.3.10"
#define WEB_SERVER_PORT       80

const unsigned long POLL_INTERVAL_MS   = 5000;    // فاصلهٔ بررسی پیام جدید روی سرور
const unsigned long REG_RETRY_MS       = 10000;   // تلاش مجدد ثبت‌نام (خطای شبکه)
const unsigned long REG_SLOW_RETRY_MS  = 30000;   // شمارهٔ تکراری: تلاش کندتر + خطای واضح در سریال
const unsigned long HTTP_TIMEOUT_MS    = 8000;    // تایم‌اوت درخواست‌های API

// ==================== WiFi و پنل تنظیمات ====================
#define AP_SSID               "SmartGadjet-Setup"
#define AP_PASS               "12345678"

#define WIFI_TX_POWER         WIFI_POWER_8_5dBm   // رادیو ضعیف این برد؛ طبق تست عوض کن
const unsigned long WIFI_RETRY_INTERVAL = 8000;     // فاصلهٔ تلاش‌های اتصال
const uint8_t       STA_MAX_TRIES       = 4;        // تعداد تلاش اولیه؛ بعد از آن پنل
const unsigned long OFFLINE_TO_PORTAL   = 2UL * 60UL * 1000UL;  // ۲ دقیقه بی‌اتصال → پنل
const unsigned long PROBE_IDLE_MS       = 15000;    // بی‌خبری HTTP تا پروب مجدد
const unsigned long PROBE_MIN_MS        = 20000;    // فاصلهٔ اول پروب‌ها در پنل
const unsigned long PROBE_MAX_MS        = 120000;   // سقف backoff
const unsigned long WIFI_SCAN_TIMEOUT_MS = 15000;   // حداکثر زمان یک اسکن
const unsigned long WIFI_TX_REFRESH_MS   = 5000;    // تازه‌سازی دوره‌ای قدرت رادیو

// ==================== پین‌ها ====================
// LED ها:
//   GPIO8 : LED وای‌فای خارجی (اکتیو-های) → وضعیت WiFi
//           - در حال سرچ/اتصال : چشمک معمولی (۵۰۰ روشن / ۵۰۰ خاموش)
//           - متصل             : روشن ثابت
//           - حالت سرور (پنل)  : دوتا چشمک سریع + مکث (۱۰۰/۱۰۰/۱۰۰/۷۰۰)
//   GPIO9 : LED «پیام معوق» (خارجی) → تا وقتی پیام خوانده‌نشده هست چشمک می‌زند
//           (۴۰۰/۴۰۰). حین پخش سری پیام‌ها خاموش است.
//           توجه: پین ۹ همان پایهٔ دکمهٔ BOOT است؛ فقط در بوت به‌عنوان ورودی خوانده می‌شود.
//   GPIO10: LED پخش صدا (خارجی) → در حین پخش ثابت، در Pause چشمک‌زن، در پایان/توقف خاموش
#define LED_WIFI_PIN         8
#define LED_PLAY_PIN         10
#define LED_MSG_PIN          9

// LED وای‌فای خارجی روی GPIO8 اکتیو-های است؛ LED آنبورد برعکس آن نمایش داده می‌شود.
#define LED_WIFI_ACTIVE_LOW  0
#define LED_PLAY_ACTIVE_LOW  0
#define LED_MSG_ACTIVE_LOW   0

// دکمه‌ها — سه پین مجاور GPIO0/1/2:
//   BTN_ACTIVE_HIGH = 1 : فشردن = دادنِ 3.3V به پین (pull-down داخلی)
//   BTN_ACTIVE_HIGH = 0 : فشردن = وصل‌کردن پین به GND (pull-up داخلی)
//   GPIO0 : «قبلی»  → در انتظار، یک پیام عقب می‌رود (حین پخش بی‌اثر)
//   GPIO1 : «پخش/تأیید» → پایان آلارم + شروع پخش از پیام انتخاب‌شده، بعد ادامهٔ صف
//           حین پخش پیام: توقف/ادامه از همان نقطه
//   GPIO2 : «بعدی»  → در انتظار، یک پیام جلو می‌رود؛ حین پخش = رد کردن پیام فعلی
//   هشدار: GPIO2 پین strapping است — هنگام روشن‌شدن/ریست به GND نگه‌داشته نشود
//   (طبق دیتاشیت C3 پیش‌فرض این پین شناور است و بوت نرمال انجام می‌شود).
#define BTN_ACTIVE_HIGH      1
#define BTN_PREV_PIN         0
#define BTN_PLAY_PIN         1
#define BTN_NEXT_PIN         2

// دکمهٔ BOOT برد (GPIO9)؛ فقط هنگام بوت خوانده می‌شود (نگه‌داشتن = ورود به پنل تنظیمات)
#define BOOT_BTN_PIN         9

// ==================== I2S (آمپلی‌فایر MAX98357) ====================
#define I2S_BCLK  5
#define I2S_LRC   4
#define I2S_DOUT  6

// ==================== پخش صدا ====================
#define VOLUME_GAIN                   3.0f
const uint32_t STREAM_BUFFER_BYTES  = 32768;   // بافر استریم MP3
const uint32_t PLAYER_TASK_STACK    = 48 * 1024;
const uint8_t  PLAY_RETRY_MAX       = 3;       // تلاش پخش یک پیام قبل از اعلام «ناموفق»
const unsigned long RESTART_INTERVAL = 3000;   // فاصلهٔ تلاش مجدد پخش
const unsigned long STALL_TIMEOUT_MS = 30000;  // توقف طولانی = تلاش/رد کردن
const unsigned long PROGRESS_REPORT_MS = 3000; // فاصلهٔ لاگ پیشرفت پخش

// ==================== دکمه، آلارم و صف ====================
const unsigned long BTN_DEBOUNCE_MS = 50;      // دیبانس دکمه‌ها (GPIO0/1/2)
const unsigned long ALERT_LED_BLINK = 400;     // نیم‌دورهٔ چشمک LED پیام (پین ۱۰)
const unsigned long PAUSE_LED_BLINK = 300;     // نیم‌دورهٔ چشمک LED پخش در حالت Pause (پین ۹)
const unsigned long ALERT_PLAY_GRACE_MS = 10000; // فرصت پخش ۱۰ ثانیه پس از پایان آلارم
const unsigned long CACHE_RETRY_MS  = 30000;   // تلاش مجدد دانلود آلارم بعد از شکست
#define QUEUE_MAX 10                           // حداکثر آیتم صف دریافتی از سرور

// حافظهٔ کش آلارم در LittleFS
#define ALERT_CACHE_PATH "/alert-cache.mp3"    // فایل آلارمِ کش‌شده (فقط آلارمِ در نوبت)
#define ALERT_TMP_PATH   "/alert-tmp.mp3"      // فایل موقت هنگام دانلود
#define ALERT_META_PATH  "/alert.meta"         // خط۱: شناسهٔ فرستنده، خط۲: هش

// ==================== نگهداری و ری‌استارت ====================
const unsigned long DIAG_INTERVAL_MS = 60000;                         // هر دقیقه لاگ heap/RSSI
const unsigned long AUTO_RESTART_MS  = 12UL * 60UL * 60UL * 1000UL;   // ری‌استارت دوره‌ای ۱۲ ساعته

// ==================== مشتقات LED (دست نزن) ====================
#define LED_WIFI_ON   ((LED_WIFI_ACTIVE_LOW) ? LOW : HIGH)
#define LED_WIFI_OFF  ((LED_WIFI_ACTIVE_LOW) ? HIGH : LOW)
#define LED_PLAY_ON   ((LED_PLAY_ACTIVE_LOW) ? LOW : HIGH)
#define LED_PLAY_OFF  ((LED_PLAY_ACTIVE_LOW) ? HIGH : LOW)
#define LED_MSG_ON    ((LED_MSG_ACTIVE_LOW) ? LOW : HIGH)
#define LED_MSG_OFF   ((LED_MSG_ACTIVE_LOW) ? HIGH : LOW)
