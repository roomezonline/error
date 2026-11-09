#pragma once

// ============================================================
//  آوای ارورسرویس — پیکربندی پروژهٔ ESP32-C3
//  همهٔ پین‌ها، آدرس سرور، تایمرها و پارامترهای قابل تنظیم اینجاست.
//  واحد همهٔ زمان‌ها میلی‌ثانیه است مگر خلافش نوشته شود.
// ============================================================

// ---------------- شناسهٔ دستگاه ----------------
#define DEVICE_NAME "ava-001"
#define DEVICE_FW   "1.0.0"

// کد دستگاه (همان DeviceNumber در پنل /admin/monitoring-devices).
// فقط اکشن‌های همین دستگاه از سرور دریافت می‌شود.
// اگر خالی باشد، دستگاه با پنل تنظیمات بالا می‌آید تا کد را وارد کنید.
// نکته: بعد از یک‌بار ذخیره از پنل، مقدار NVS اولویت دارد؛ برای اعمال دوبارهٔ
// این مقدار، از پنل دستگاه «پاک کردن تنظیمات» را بزنید.
#define DEVICE_CODE ""

// ---------------- وای‌فای پیش‌فرض ----------------
// اگر خالی باشند، دستگاه با پنل تنظیمات بالا می‌آید.
// بعد از یک‌بار ذخیره از پنل، مقدار ذخیره‌شده در حافظه اولویت دارد.
#define WIFI_SSID ""
#define WIFI_PASS ""

// ---------------- سرور ----------------
// سایت اصلی روی HTTP به HTTPS ریدایرکت می‌شود؛ پس آدرس باید https باشد.
#define SERVER_BASE  "https://errorservice.ir"
#define VOICES_PATH  "/api/device/service-voices"

// رویدادهای پخش فوری (تغییر برق/موتور/المنت‌ها در پایش)
#define VOICE_EVENTS_PATH         "/api/device/voice-events/next"
#define VOICE_EVENTS_CURSOR_PATH  "/api/device/voice-events/cursor"

// گواهی TLS بررسی نمی‌شود (سبک و ساده). برای سخت‌گیری بیشتر
// می‌توان گواهی ریشه را در کد گذاشت و این را 0 کرد.
#define TLS_INSECURE 1

// ---------------- پین‌ها ----------------
// آمپلی‌فایر I2S — MAX98357
#define I2S_BCLK 5
#define I2S_LRC  4
#define I2S_DOUT 6

// دکمه‌ها — فشردن = وصل شدن به 3.3V (pull-down داخلی)
//   GPIO0 : قبلی    (انتخاب آوای قبلی / حین پخش: پخش قبلی)
//   GPIO1 : پخش     (شروع پخش / توقف و ادامه)
//   GPIO2 : بعدی    (انتخاب آوای بعدی / حین پخش: پخش بعدی)
#define BTN_ACTIVE_HIGH 1
#define BTN_PREV_PIN    0
#define BTN_PLAY_PIN    1
#define BTN_NEXT_PIN    2

// LED ها — اکتیو-های (LED_ACTIVE_LOW = 0 یعنی روشن با سطح HIGH)
#define LED_ACTIVE_LOW 0
#define LED_WIFI_PIN   8   // وضعیت وای‌فای
#define LED_STATUS_PIN 9   // فهرست آواها آماده است
#define LED_PLAY_PIN   10  // در حال پخش / توقف موقت

// دکمهٔ BOOT برد (GPIO9) — نگه‌داشتن هنگام روشن‌شدن = ورود به پنل تنظیمات
#define BOOT_BTN_PIN 9

// ---------------- پخش صدا ----------------
#define VOLUME_GAIN 3.0f

const uint32_t STREAM_BUFFER_BYTES = 12 * 1024;   // بافر استریم شبکه (~۱.۵ ثانیه صدا)
const uint32_t PLAYER_TASK_STACK   = 32 * 1024;   // استک تسک پخش (دیکودر MP3 + TLS)

// رویدادها: تسک کم‌اولویت long-poll + صف پخش
const uint32_t EVENT_TASK_STACK     = 16 * 1024;
const uint32_t EVENT_TASK_PRIORITY  = 1;         // پایین‌تر از تسک پخش (۲)
const uint32_t EVENT_QUEUE_SIZE     = 10;
const int      EVENT_POLL_WAIT_SEC  = 20;        // مدت باز ماندن درخواست long-poll
const unsigned long EVENT_HTTP_TIMEOUT_MS = 30 * 1000;
// رویدادی که بیشتر از این مدت در صف دستگاه مانده باشد پخش نمی‌شود.
const unsigned long EVENT_MAX_AGE_MS = 90UL * 1000UL;

#define MAX_ACTIONS 20  // حداکثر تعداد آوای دریافتی از سرور

// پخش خودکار همهٔ آواها بعد از اولین دریافت فهرست (1 = روشن، 0 = خاموش)
#define AUTO_PLAY_ON_BOOT 0

// ---------------- زمان‌بندی ----------------
const unsigned long POLL_INTERVAL_MS    = 60000; // تازه‌سازی فهرست در حالت بی‌کار
const unsigned long HTTP_TIMEOUT_MS     = 12000; // تایم‌اوت درخواست‌های API
const unsigned long RETRY_DELAY_MS      = 3000;  // فاصلهٔ تلاش مجدد پخش
const unsigned long BTN_DEBOUNCE_MS     = 50;    // دیبانس دکمه‌ها
const unsigned long WIFI_RETRY_INTERVAL = 8000;  // فاصلهٔ تلاش‌های اتصال وای‌فای
const unsigned long OFFLINE_TO_PORTAL   = 2UL * 60UL * 1000UL; // ۲ دقیقه بی‌اتصال → پنل
const unsigned long WIFI_TX_REFRESH_MS  = 5000;  // تازه‌سازی دوره‌ای قدرت رادیو
const unsigned long WIFI_SCAN_TIMEOUT_MS = 15000;
const uint16_t      LED_BLINK_MS        = 400;

// قدرت رادیو — این برد در قدرت کم پایدارتر است (طبق تست میدانی)
#define WIFI_TX_POWER WIFI_POWER_8_5dBm

// ---------------- پنل تنظیمات وای‌فای ----------------
#define AP_SSID         "AVA-Setup"
#define AP_PASS         "12345678"
#define WEB_PORTAL_PORT 80
