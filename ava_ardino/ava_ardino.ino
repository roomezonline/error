// ============================================================
//  آوای ارورسرویس — پخش‌کنندهٔ صدا برای ESP32-C3
//  ------------------------------------------------------------
//  دستگاه فهرست آواهای فعالِ **همان دستگاه** را از این آدرس می‌گیرد:
//      GET https://errorservice.ir/api/device/service-voices?code=<DEVICE_CODE>
//  و فایل هر آوا را مستقیماً از سرور استریم و پخش می‌کند.
//  همان آواهایی که در پنل /admin/service-voices برای آن دستگاه ضبط می‌شوند.
//
//  دکمه‌ها: قبلی (GPIO0) / پخش-توقف (GPIO1) / بعدی (GPIO2)
//  آمپلی‌فایر: MAX98357 روی I2S (BCLK=5, LRC=4, DOUT=6)
// ============================================================

#include <WiFi.h>
#include <WiFiClientSecure.h>
#include <WebServer.h>
#include <Preferences.h>
#include <HTTPClient.h>
#include <ArduinoJson.h>
#include "esp_wifi.h"
#include <freertos/queue.h>
#include <freertos/semphr.h>

#include "AudioLogger.h"
#include "AudioFileSourceHTTPSRange.h"
#include "AudioFileSourceBuffer.h"
#include "AudioGeneratorMP3.h"
#include "AudioOutputI2S.h"

#include "ava_config.h"

// ==================== خروجی I2S با شمارش خطا ====================
class TrackedAudioOutput : public AudioOutputI2S {
public:
  uint32_t framesSent = 0;
  uint32_t writeFails = 0;
  int32_t  lastRate   = 0;

  bool SetRate(int hz) override {
    bool ok = AudioOutputI2S::SetRate(hz);
    lastRate = hz;
    Serial.printf("[Audio] rate=%d%s\n", hz, ok ? "" : " (FAILED)");
    return ok;
  }

  bool ConsumeSample(int16_t sample[2]) override {
    if (!i2sOn) return false;

    int16_t ms[2] = { sample[0], sample[1] };
    MakeSampleStereo16(ms);

    if (mono) {
      int32_t ttl = ms[LEFTCHANNEL] + ms[RIGHTCHANNEL];
      ms[LEFTCHANNEL] = ms[RIGHTCHANNEL] = (ttl >> 1) & 0xffff;
    }

    uint32_t s32 = ((uint32_t)(uint16_t)Amplify(ms[RIGHTCHANNEL]) << 16) |
                   (uint16_t)Amplify(ms[LEFTCHANNEL]);
    size_t written = 0;
    esp_err_t err = i2s_channel_write(_tx_handle, (const char*)&s32,
                                      sizeof(s32), &written, 0);
    if (err != ESP_OK || written != sizeof(s32)) {
      writeFails++;
      return false;
    }
    framesSent++;
    return true;
  }
};

// ==================== ساختار یک آوا ====================
struct VoiceAction {
  int     id = 0;
  String  key;
  String  title;
  String  url;
  uint8_t repeat = 1;
  int     priority = 10;
};

// ==================== رویداد پخش فوری ====================
// پیام صف بین تسک long-poll و تسک پخش؛ رشته‌ها ثابت‌اند تا حافظه تکه‌تکه نشود.
struct VoiceEventItem {
  long long id = 0;
  uint8_t   repeat = 1;
  int       priority = 10;
  bool      isTest = false;
  uint32_t  queuedAt = 0;
  char      key[40]   = {0};
  char      title[64] = {0};
  char      url[220]  = {0};
};

QueueHandle_t eventQueue = nullptr;

// فقط یک درخواست HTTPS هم‌زمان؛ دو TLS موازی روی ESP32-C3 حافظه را
// تکه‌تکه و پخش صدا را ناپایدار می‌کند. هر تسک قفل را با تایم‌اوت صفر
// می‌گیرد و اگر مشغول بود بعداً تلاش می‌کند (بدون بلاک شدن صدا).
SemaphoreHandle_t netLock = nullptr;

static bool netAcquire() {
  return netLock && xSemaphoreTake(netLock, 0) == pdTRUE;
}

static void netRelease() {
  if (netLock) xSemaphoreGive(netLock);
}

VoiceEventItem currentEvent;
volatile bool    eventPlaying = false;
volatile uint8_t eventRepeatLeft = 0;
uint8_t          eventAttempts = 0;
unsigned long long lastEventId = 0;
bool             eventCursorLoaded = false;

// ==================== حالت‌های سیستم ====================
enum : uint8_t { M_STA = 0, M_PLAY = 1, M_PORTAL = 2 };
enum : uint8_t { PS_IDLE = 0, PS_PLAYING = 1, PS_PAUSED = 2 };

volatile uint8_t mode = M_STA;
volatile bool    wifiConnected = false;

Preferences prefs;
WebServer   portal(WEB_PORTAL_PORT);

String cfgSsid, cfgPass;
String deviceCode;    // کد دستگاه برای فیلتر آواها در سرور

unsigned long lastWifiAttempt = 0;
unsigned long lostAt          = 0;

// ==================== فهرست آواها ====================
VoiceAction voices[MAX_ACTIONS];
volatile uint8_t voiceCount = 0;
long     listVersion = -1;
bool     lastFetchOk = false;
bool     listLoaded  = false;
bool     autoPlayDone = false;
unsigned long lastPoll = 0;      // آخرین تازه‌سازی موفق فهرست
unsigned long lastPollTry = 0;   // آخرین تلاش (برای فاصله‌گذاری تلاش‌ها)

// ==================== وضعیت پخش ====================
volatile uint8_t  playState = PS_IDLE;
volatile int8_t   currentIndex = 0;
volatile uint8_t  repeatLeft = 0;
volatile bool     sessionActive = false;
volatile unsigned long selectFeedbackUntil = 0;

unsigned long retryAt = 0;

// بافرهای ثابت (یک‌بار رزرو می‌شوند تا heap تکه‌تکه نشود)
static uint8_t streamBuffer[STREAM_BUFFER_BYTES] alignas(16);
static uint8_t mp3Buff  [AudioGeneratorMP3::preAllocBuffSize()]   alignas(8);
static uint8_t mp3Stream[AudioGeneratorMP3::preAllocStreamSize()] alignas(8);
static uint8_t mp3Frame [AudioGeneratorMP3::preAllocFrameSize()]  alignas(8);
static uint8_t mp3Synth [AudioGeneratorMP3::preAllocSynthSize()]  alignas(8);

// ==================== آبجکت‌های صوتی ====================
AudioGenerator*             gen     = nullptr;
AudioFileSourceBuffer*      buffer  = nullptr;
AudioFileSourceHTTPSRange*  httpSrc = nullptr;
TrackedAudioOutput*         out     = nullptr;

// ==================== دکمه‌ها ====================
volatile uint8_t btnPrevEvents = 0;
volatile uint8_t btnPlayEvents = 0;
volatile uint8_t btnNextEvents = 0;

const uint8_t BTN_PINS[3] = { BTN_PREV_PIN, BTN_PLAY_PIN, BTN_NEXT_PIN };
volatile uint8_t* const BTN_EVENTS[3] = { &btnPrevEvents, &btnPlayEvents, &btnNextEvents };
bool     btnStable[3]  = { false, false, false };
bool     btnLastRaw[3] = { false, false, false };
unsigned long btnChangeAt[3] = { 0, 0, 0 };

// ==================== اعلان توابع ====================
void updateLeds();
void stopPlayback();
bool startItem(int idx);
bool startFile(const String& url);
void startEvent(const VoiceEventItem& ev);
void audioTask(void *arg);
void eventTask(void *arg);
bool fetchVoices();
static bool fetchEventCursorInternal();
static void fetchEventsInternal();
void fetchEvents();
bool fetchEventCursor();
void saveLastEventId();
bool dequeueEvent(VoiceEventItem& out);
void handleAudioButtons();
void handleTrackFinished();
void moveSelection(int delta);
void startSession(int idx);
void startPortal();
void startSta();
void loopSta();
void pollButtons();
void applyTxPower();
void keepTx();
void handlePortalRoot();
void handleSave();
void handleClear();
void handlePortalNotFound();
void onWifiEvent(WiFiEvent_t event, WiFiEventInfo_t info);

// ==================== LED ====================
inline void ledWrite(uint8_t pin, bool on) {
#if LED_ACTIVE_LOW
  digitalWrite(pin, on ? LOW : HIGH);
#else
  digitalWrite(pin, on ? HIGH : LOW);
#endif
}

void updateLeds() {
  unsigned long now = millis();

  // LED وای‌فای: در پنل چشمک دوتایی، متصل ثابت، در حال اتصال چشمک معمولی
  bool wifiOn;
  if (mode == M_PORTAL) {
    unsigned long p = now % 1000;
    wifiOn = (p < 100) || (p >= 200 && p < 300);
  } else if (wifiConnected) {
    wifiOn = true;
  } else {
    wifiOn = (now % 1000) < 500;
  }
  ledWrite(LED_WIFI_PIN, wifiOn);

  // LED پخش: در حال پخش ثابت، توقف موقت چشمک‌زن، انتخاب جدید یک چشمک کوتاه
  bool playOn = false;
  if (playState == PS_PLAYING) {
    playOn = true;
  } else if (playState == PS_PAUSED) {
    playOn = (now % (LED_BLINK_MS * 2)) < LED_BLINK_MS;
  } else if (selectFeedbackUntil != 0 &&
             (int32_t)(now - selectFeedbackUntil) < 0) {
    playOn = true;
  }
  ledWrite(LED_PLAY_PIN, playOn);

  // LED وضعیت: فهرست آماده و دارای آوا = ثابت؛ خطای دریافت = چشمک
  bool statusOn;
  if (lastFetchOk) {
    statusOn = (voiceCount > 0);
  } else {
    statusOn = (now % 1000) < 500;
  }
  ledWrite(LED_STATUS_PIN, statusOn);
}

// ==================== وای‌فای ====================
void applyTxPower() {
  WiFi.setTxPower((wifi_power_t)WIFI_TX_POWER);
}

void keepTx() {
  static unsigned long t = 0;
  if (millis() - t >= WIFI_TX_REFRESH_MS) {
    t = millis();
    applyTxPower();
  }
}

void onWifiEvent(WiFiEvent_t event, WiFiEventInfo_t info) {
  if (event == ARDUINO_EVENT_WIFI_STA_GOT_IP) {
    wifiConnected = true;
    lostAt = 0;
    if (mode != M_PORTAL) mode = M_PLAY;
    Serial.printf("[WiFi] connected ip=%s rssi=%d\n",
                  WiFi.localIP().toString().c_str(), WiFi.RSSI());
  } else if (event == ARDUINO_EVENT_WIFI_STA_DISCONNECTED) {
    wifiConnected = false;
    if (mode == M_PLAY) {
      mode = M_STA;
      lostAt = millis();
    }
    Serial.printf("[WiFi] disconnected reason=%d\n",
                  info.wifi_sta_disconnected.reason);
  }
}

void connectWifi() {
  WiFi.disconnect(true, false);
  delay(50);
  WiFi.mode(WIFI_STA);
  WiFi.setSleep(false);
  WiFi.begin(cfgSsid.c_str(), cfgPass.c_str());
  applyTxPower();
}

void loadCfg() {
  prefs.begin("cfg", true);
  cfgSsid = prefs.getString("ssid", WIFI_SSID);
  cfgPass = prefs.getString("pass", WIFI_PASS);
  deviceCode = prefs.getString("devcode", DEVICE_CODE);
  String ev = prefs.getString("evlast", "");
  prefs.end();

  deviceCode.trim();
  eventCursorLoaded = ev.length() > 0;
  lastEventId = strtoull(ev.c_str(), nullptr, 10);

  Serial.printf("[Cfg] ssid=\"%s\" code=\"%s\" eventCursor=%llu\n",
                cfgSsid.c_str(), deviceCode.c_str(), lastEventId);
}

void saveLastEventId() {
  char buf[24];
  snprintf(buf, sizeof(buf), "%llu", lastEventId);
  prefs.begin("cfg", false);
  prefs.putString("evlast", buf);
  prefs.end();
}

void saveCfg(const String& s, const String& p, const String& code) {
  prefs.begin("cfg", false);
  prefs.putString("ssid", s);
  prefs.putString("pass", p);
  prefs.putString("devcode", code);
  prefs.end();
}

void startSta() {
  mode = M_STA;
  wifiConnected = false;
  btnPrevEvents = btnPlayEvents = btnNextEvents = 0;
  sessionActive = false;
  playState = PS_IDLE;
  lastPoll = 0;
  lastPollTry = 0;
  lostAt = millis();
  lastWifiAttempt = millis();

  Serial.printf("[Mode] STA -> \"%s\"\n", cfgSsid.c_str());
  connectWifi();
}

void loopSta() {
  keepTx();

  unsigned long now = millis();
  if (wifiConnected) {
    mode = M_PLAY;
    return;
  }

  if (lostAt == 0) lostAt = now;

  // بی‌اتصال طولانی → پنل تنظیمات
  if (now - lostAt >= OFFLINE_TO_PORTAL) {
    Serial.println("[Mode] offline too long -> portal");
    startPortal();
    return;
  }

  if (now - lastWifiAttempt >= WIFI_RETRY_INTERVAL) {
    lastWifiAttempt = now;
    Serial.println("[WiFi] retry...");
    connectWifi();
  }
}

// ==================== پنل تنظیمات ====================
static const char PORTAL_HTML[] PROGMEM = R"html(<!DOCTYPE html>
<html lang="fa" dir="rtl"><head>
<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>تنظیمات آوای ارورسرویس</title>
<style>
*{box-sizing:border-box}
body{margin:0;padding:18px;font-family:Tahoma,system-ui,sans-serif;color:#eef1ff;
background:radial-gradient(800px 420px at 100% -10%,#1d2a52,transparent),#0e1020;min-height:100vh}
.card{max-width:430px;margin:0 auto;background:#151a2e;border:1px solid #262c4a;border-radius:18px;
padding:20px;box-shadow:0 18px 40px rgba(0,0,0,.45)}
h1{font-size:18px;margin:0 0 4px}
.sub{color:#8f98bd;font-size:12.5px;margin:0 0 16px;line-height:1.8}
label{display:block;font-size:12.5px;color:#9aa3c8;margin:14px 0 6px}
.hint{color:#6f789c;font-size:11px;line-height:1.7;margin-top:5px}
input{width:100%;padding:11px 12px;border-radius:10px;border:1px solid #2b3155;
background:#11152c;color:#eef1ff;font-family:inherit;font-size:14px}
button{width:100%;margin-top:16px;padding:12px;border:0;border-radius:12px;
background:linear-gradient(135deg,#4f7cff,#2e5be0);color:#fff;font-family:inherit;font-size:14px;cursor:pointer}
button.danger{background:#3a1f2a;color:#ff8fa3}
.muted{color:#6f789c;font-size:11.5px;text-align:center;margin:18px 0 0;line-height:1.9}
</style></head><body><div class="card">
<h1>آوای ارورسرویس</h1>
<p class="sub">وای‌فای دستگاه را انتخاب و کد دستگاه را وارد کنید. بعد از ذخیره، دستگاه خودش ری‌استارت می‌شود.</p>
<form method="POST" action="/save">
<label for="code">کد دستگاه</label>
<input id="code" name="code" dir="ltr" value="%CODE%" placeholder="مثلاً AVA-001" required>
<div class="hint">همان «شماره دستگاه» ثبت‌شده در پنل مدیریت دستگاه‌ها است؛ فقط آواهای همین دستگاه پخش می‌شوند.</div>
<label for="ssid">شبکهٔ وای‌فای</label>
<input id="ssid" name="ssid" list="nets" value="%SSID%" placeholder="نام شبکه" required>
<datalist id="nets">%NETS%</datalist>
<label for="pass">رمز شبکه</label>
<input id="pass" name="pass" type="password" value="%PASS%" placeholder="رمز (برای شبکهٔ باز خالی بگذارید)">
<button type="submit">ذخیره و اتصال</button>
</form>
<form method="GET" action="/clear"><button class="danger" type="submit">پاک کردن تنظیمات</button></form>
<p class="muted">%NAME% — فرمور %FW%</p>
</div></body></html>)html";

static const char PORTAL_OK[] PROGMEM = R"html(<!DOCTYPE html><html lang="fa" dir="rtl"><head>
<meta charset="utf-8"><title>ذخیره شد</title></head>
<body style="background:#0e1020;color:#eef1ff;font-family:Tahoma;text-align:center;padding-top:80px">
<h2>ذخیره شد ✅</h2><p>دستگاه در حال ری‌استارت و اتصال به وای‌فای است...</p></body></html>)html";

String escHtml(const String& in) {
  String o = in;
  o.replace("&", "&amp;");
  o.replace("\"", "&quot;");
  o.replace("<", "&lt;");
  o.replace(">", "&gt;");
  return o;
}

int scanWifi() {
  int n = WiFi.scanNetworks(false, true);
  if (n == WIFI_SCAN_RUNNING) {
    unsigned long started = millis();
    while (n == WIFI_SCAN_RUNNING && millis() - started < WIFI_SCAN_TIMEOUT_MS) {
      updateLeds();
      delay(20);
      n = WiFi.scanComplete();
    }
  }
  if (n == WIFI_SCAN_RUNNING) {
    WiFi.scanDelete();
    return WIFI_SCAN_FAILED;
  }
  return n;
}

void handlePortalRoot() {
  Serial.println("[Portal] scanning networks...");
  int n = scanWifi();

  // حذف تکراری‌ها (قوی‌ترین BSSID) و مرتب‌سازی نزولی بر اساس قدرت سیگنال
  int16_t idx[40];
  int cnt = 0;
  for (int i = 0; i < n && cnt < 40; i++) {
    if (WiFi.SSID(i).isEmpty()) continue;
    int dup = -1;
    for (int j = 0; j < cnt; j++) {
      if (WiFi.SSID(idx[j]) == WiFi.SSID(i)) { dup = j; break; }
    }
    if (dup < 0) idx[cnt++] = (int16_t)i;
    else if (WiFi.RSSI(i) > WiFi.RSSI(idx[dup])) idx[dup] = (int16_t)i;
  }
  for (int a = 0; a < cnt - 1; a++) {
    for (int b = a + 1; b < cnt; b++) {
      if (WiFi.RSSI(idx[b]) > WiFi.RSSI(idx[a])) {
        int16_t t = idx[a]; idx[a] = idx[b]; idx[b] = t;
      }
    }
  }

  String nets;
  nets.reserve(cnt * 48);
  for (int i = 0; i < cnt; i++) {
    String s = WiFi.SSID(idx[i]);
    nets += "<option value=\"" + escHtml(s) + "\">" +
            escHtml(s) + " (" + String(WiFi.RSSI(idx[i])) + "dBm)</option>";
  }
  if (n > 0) WiFi.scanDelete();

  String page = FPSTR(PORTAL_HTML);
  page.replace("%CODE%", escHtml(deviceCode));
  page.replace("%SSID%", escHtml(cfgSsid));
  page.replace("%PASS%", escHtml(cfgPass));
  page.replace("%NETS%", nets);
  page.replace("%NAME%", DEVICE_NAME);
  page.replace("%FW%", DEVICE_FW);

  portal.send(200, "text/html; charset=utf-8", page);
}

void handleSave() {
  String s = portal.arg("ssid"); s.trim();
  String p = portal.arg("pass");
  String code = portal.arg("code"); code.trim();

  if (s.isEmpty()) {
    portal.send(400, "text/html; charset=utf-8",
                "<meta charset='utf-8'>نام شبکهٔ وای‌فای خالی است.");
    return;
  }

  if (code.isEmpty()) {
    portal.send(400, "text/html; charset=utf-8",
                "<meta charset='utf-8'>کد دستگاه خالی است؛ همان شمارهٔ دستگاه در پنل را وارد کنید.");
    return;
  }

  saveCfg(s, p, code);
  Serial.printf("[Portal] saved ssid=\"%s\" code=\"%s\" -> restart\n",
                s.c_str(), code.c_str());
  portal.send(200, "text/html; charset=utf-8", FPSTR(PORTAL_OK));
  delay(800);
  ESP.restart();
}

void handleClear() {
  prefs.begin("cfg", false);
  prefs.clear();
  prefs.end();
  Serial.println("[Portal] config cleared -> restart");
  portal.send(200, "text/html; charset=utf-8",
              "<meta charset='utf-8'><body style='background:#0e1020;color:#eef1ff;"
              "font-family:Tahoma;text-align:center;padding-top:80px'>"
              "<h2>پاک شد</h2><p>در حال ری‌استارت...</p></body>");
  delay(800);
  ESP.restart();
}

void handlePortalNotFound() {
  portal.sendHeader("Location", "/");
  portal.send(302, "text/plain", "");
}

void startPortal() {
  mode = M_PORTAL;   // تسک پخش خودش صدا را متوقف می‌کند
  for (int i = 0; i < 200 && playState != PS_IDLE; i++) delay(5);

  Serial.println("[Mode] PORTAL");
  wifiConnected = false;
  lostAt = 0;

  WiFi.disconnect(true, false);
  delay(50);
  WiFi.mode(WIFI_AP_STA);   // AP + STA آزاد برای اسکن شبکه‌ها
  delay(50);
  WiFi.softAP(AP_SSID, AP_PASS);
  applyTxPower();

  portal.begin();
  Serial.printf("[Portal] AP \"%s\" -> http://%s\n",
                AP_SSID, WiFi.softAPIP().toString().c_str());
}

// ==================== دریافت فهرست آواها ====================
static String urlEncodeValue(const String& in) {
  static const char hex[] = "0123456789ABCDEF";
  String out;
  out.reserve(in.length() * 3);
  for (size_t i = 0; i < in.length(); i++) {
    uint8_t c = (uint8_t)in[i];
    if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
        (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.' || c == '~') {
      out += (char)c;
    } else {
      out += '%';
      out += hex[c >> 4];
      out += hex[c & 0x0F];
    }
  }
  return out;
}

static bool fetchVoicesInternal() {
  if (deviceCode.isEmpty()) {
    Serial.println("[API] device code is empty -> set DEVICE_CODE or use the setup panel");
    lastFetchOk = false;
    return false;
  }

  WiFiClientSecure client;
  client.setInsecure();
  client.setHandshakeTimeout(15);

  HTTPClient http;
  http.setConnectTimeout(HTTP_TIMEOUT_MS);
  http.setTimeout(HTTP_TIMEOUT_MS);
  http.setReuse(false);

  String url = String(SERVER_BASE) + VOICES_PATH + "?code=" + urlEncodeValue(deviceCode);
  Serial.printf("[API] GET %s\n", url.c_str());

  if (!http.begin(client, url)) {
    Serial.println("[API] begin failed");
    lastFetchOk = false;
    return false;
  }
  http.addHeader("Accept", "application/json");

  int code = http.GET();
  if (code != 200) {
    Serial.printf("[API] HTTP %d\n", code);
    http.end();
    lastFetchOk = false;
    return false;
  }

  String payload = http.getString();
  http.end();

  JsonDocument doc;
  DeserializationError err = deserializeJson(doc, payload);
  if (err) {
    Serial.printf("[API] json error: %s\n", err.c_str());
    lastFetchOk = false;
    return false;
  }

  JsonArray arr = doc["actions"].as<JsonArray>();
  if (arr.isNull()) {
    Serial.println("[API] no actions array");
    lastFetchOk = false;
    return false;
  }

  VoiceAction tmp[MAX_ACTIONS];
  uint8_t n = 0;

  for (JsonVariant v : arr) {
    if (n >= MAX_ACTIONS) break;

    String u = v["audioUrl"] | "";
    if (u.length() == 0) continue;

    VoiceAction& t = tmp[n];
    t.id       = v["id"] | 0;
    t.key      = v["key"] | "";
    t.title    = v["titleFa"] | "";
    t.url      = u;
    t.priority = v["priority"] | 10;

    // فقط MP3 پشتیبانی می‌شود؛ هم پسوند و هم فیلد format بررسی می‌شود.
    bool isMp3 = u.endsWith(".mp3") ||
                 String(v["format"] | "").equalsIgnoreCase("mp3");
    if (!isMp3) {
      Serial.printf("[API] skip \"%s\" (not MP3)\n", t.title.c_str());
      continue;
    }

    int rep = v["repeatCount"] | 1;
    if (rep < 1) rep = 1;
    if (rep > 50) rep = 50;
    t.repeat = (uint8_t)rep;

    n++;
  }

  for (uint8_t i = 0; i < n; i++) voices[i] = tmp[i];
  for (uint8_t i = n; i < voiceCount; i++) voices[i] = VoiceAction();

  voiceCount = n;
  listVersion = doc["version"] | 0L;
  lastFetchOk = true;
  listLoaded = true;

  if (currentIndex >= voiceCount) currentIndex = 0;

  Serial.printf("[API] %u voice(s), version=%ld\n",
                (unsigned)voiceCount, listVersion);
  for (uint8_t i = 0; i < voiceCount; i++) {
    Serial.printf("  #%u %s [%s] x%u\n",
                  (unsigned)(i + 1), voices[i].title.c_str(),
                  voices[i].key.c_str(), voices[i].repeat);
  }
  return true;
}

/// تازه‌سازی فهرست آواها؛ اگر درخواست دیگری در جریان باشد false برمی‌گرداند.
bool fetchVoices() {
  if (!netAcquire()) return false;
  bool ok = fetchVoicesInternal();
  netRelease();
  return ok;
}

// ==================== پخش ====================
void stopPlayback() {
  if (gen) { gen->stop(); delete gen; gen = nullptr; }
  if (buffer) { delete buffer; buffer = nullptr; }
  if (out) {
    if (out->framesSent) {
      uint32_t hz = out->lastRate > 0 ? (uint32_t)out->lastRate : 22050;
      Serial.printf("[Audio] played %.2fs, write-fails=%u\n",
                    (double)out->framesSent / hz, (unsigned)out->writeFails);
    }
    delete out; out = nullptr;
  }
  if (httpSrc) { delete httpSrc; httpSrc = nullptr; }
  playState = PS_IDLE;
}

/// پخش مستقیم یک فایل MP3 از روی سرور (استریم + بافر ۲۴ کیلوبایتی).
bool startFile(const String& url) {
  stopPlayback();

  if (url.length() == 0) return false;

  Serial.printf("[Play] url=%s\n", url.c_str());

  httpSrc = new AudioFileSourceHTTPSRange();
  if (!httpSrc->open(url.c_str())) {
    Serial.println("[Play] open failed");
    stopPlayback();
    return false;
  }

  buffer = new AudioFileSourceBuffer(httpSrc, streamBuffer, STREAM_BUFFER_BYTES);

  out = new TrackedAudioOutput();
  out->SetPinout(I2S_BCLK, I2S_LRC, I2S_DOUT);
  out->SetGain(VOLUME_GAIN);

  gen = new AudioGeneratorMP3(mp3Buff,   (int)sizeof(mp3Buff),
                              mp3Stream, (int)sizeof(mp3Stream),
                              mp3Frame,  (int)sizeof(mp3Frame),
                              mp3Synth,  (int)sizeof(mp3Synth));

  if (!gen->begin(buffer, out)) {
    Serial.println("[Play] decoder begin failed");
    stopPlayback();
    return false;
  }

  playState = PS_PLAYING;
  return true;
}

bool startItem(int idx) {
  if (idx < 0 || idx >= voiceCount) return false;

  VoiceAction& a = voices[idx];
  if (a.url.length() == 0) return false;

  // پخش دستی، حالت رویداد را پایان می‌دهد
  eventPlaying = false;
  eventRepeatLeft = 0;
  eventAttempts = 0;

  Serial.printf("[Play] #%d/%u %s\n", idx + 1, (unsigned)voiceCount, a.title.c_str());
  return startFile(a.url);
}

void startEvent(const VoiceEventItem& ev) {
  currentEvent = ev;
  eventPlaying = true;
  eventRepeatLeft = ev.repeat > 0 ? ev.repeat : 1;
  eventAttempts = 0;
  sessionActive = false;
  repeatLeft = 0;

  Serial.printf("[Event] #%lld %s (%s)\n", ev.id, ev.title, ev.key);
  if (!startFile(String(ev.url))) {
    eventAttempts++;
    retryAt = millis() + RETRY_DELAY_MS;
  }
}

void startSession(int idx) {
  if (voiceCount == 0) return;
  if (idx < 0 || idx >= voiceCount) idx = 0;

  sessionActive = true;
  eventPlaying = false;
  currentIndex = (int8_t)idx;
  repeatLeft = voices[idx].repeat;
  retryAt = 0;

  if (!startItem(idx)) retryAt = millis() + RETRY_DELAY_MS;
}

void moveSelection(int delta) {
  if (voiceCount == 0) return;

  int idx = (int)currentIndex + delta;
  if (idx < 0) idx = (int)voiceCount - 1;
  if (idx >= (int)voiceCount) idx = 0;

  currentIndex = (int8_t)idx;
  selectFeedbackUntil = millis() + 300;

  if (playState != PS_IDLE || eventPlaying) {
    // حین پخش، آوای قبلی/بعدی فوراً پخش می‌شود
    sessionActive = true;
    eventPlaying = false;
    repeatLeft = voices[idx].repeat;
    retryAt = 0;
    if (!startItem(idx)) retryAt = millis() + RETRY_DELAY_MS;
  } else {
    Serial.printf("[Select] #%d/%u %s\n", idx + 1,
                  (unsigned)voiceCount, voices[idx].title.c_str());
  }
}

void handleTrackFinished() {
  stopPlayback();

  // پایان/تکرار رویداد جاری
  if (eventPlaying) {
    if (eventRepeatLeft > 1) {
      eventRepeatLeft--;
      Serial.printf("[Event] repeat (%u left)\n", (unsigned)eventRepeatLeft);
      if (!startFile(String(currentEvent.url))) {
        eventAttempts++;
        retryAt = millis() + RETRY_DELAY_MS;
      }
      return;
    }
    eventPlaying = false;
    eventRepeatLeft = 0;
  }

  // رویدادهای در صف بر ادامهٔ پخش دستی اولویت دارند
  VoiceEventItem nextEvent;
  if (dequeueEvent(nextEvent)) {
    startEvent(nextEvent);
    return;
  }

  if (!sessionActive) return;

  // تکرار همان آوا طبق تعداد تکرار تعریف‌شده در پنل
  if (repeatLeft > 1) {
    repeatLeft--;
    Serial.printf("[Play] repeat (%u left)\n", (unsigned)repeatLeft);
    if (!startItem(currentIndex)) retryAt = millis() + RETRY_DELAY_MS;
    return;
  }
  repeatLeft = 0;

  // آوای بعدی در فهرست؛ بعد از آخری، پخش تمام می‌شود
  int next = (int)currentIndex + 1;
  if (next < (int)voiceCount) {
    currentIndex = (int8_t)next;
    repeatLeft = voices[next].repeat;
    retryAt = 0;
    if (!startItem(next)) retryAt = millis() + RETRY_DELAY_MS;
  } else {
    sessionActive = false;
    currentIndex = 0;
    lastPollTry = 0;   // فهرست تازه برای دفعهٔ بعد
    Serial.println("[Play] session finished");
  }
}

void handleAudioButtons() {
  if (btnPlayEvents > 0) {
    btnPlayEvents = 0;

    if (playState == PS_PLAYING) {
      playState = PS_PAUSED;
      Serial.println("[Play] pause");
    } else if (playState == PS_PAUSED) {
      playState = PS_PLAYING;
      Serial.println("[Play] resume");
    } else if (voiceCount == 0) {
      Serial.println("[Play] list is empty");
    } else {
      startSession(currentIndex);
    }
  }

  if (btnNextEvents > 0) {
    btnNextEvents = 0;
    moveSelection(+1);
  }

  if (btnPrevEvents > 0) {
    btnPrevEvents = 0;
    moveSelection(-1);
  }
}

// ==================== صف رویدادها ====================
// رویدادی که مدت زیادی در صف مانده (مثلاً دستگاه در حالت توقف بوده) پخش نمی‌شود؛
// صدای «برق قطع شد» بعد از چند دقیقه معنا ندارد.
bool dequeueEvent(VoiceEventItem& out) {
  if (!eventQueue) return false;

  while (xQueueReceive(eventQueue, &out, 0) == pdTRUE) {
    if ((uint32_t)(millis() - out.queuedAt) <= EVENT_MAX_AGE_MS) return true;
    Serial.printf("[Event] drop stale: %s\n", out.title);
  }

  return false;
}

static void pushEvent(const VoiceEventItem& item) {
  if (!eventQueue) return;

  if (xQueueSend(eventQueue, &item, 0) != pdTRUE) {
    // اگر صف پر شد، قدیمی‌ترین رویداد حذف می‌شود تا رویداد تازه‌تر از دست نرود.
    VoiceEventItem dropped;
    xQueueReceive(eventQueue, &dropped, 0);
    xQueueSend(eventQueue, &item, 0);
    Serial.println("[Event] queue full -> dropped oldest");
  }
}

static void copyText(char* dst, size_t len, const char* src) {
  if (len == 0) return;
  if (!src) { dst[0] = 0; return; }
  strlcpy(dst, src, len);
}

// ==================== تسک رویداد (long-poll) ====================
void eventTask(void *arg) {
  (void)arg;

  for (;;) {
    // فقط وقتی دستگاه آنلاین و بی‌کار است poll می‌کنیم تا دیکود صدا روان بماند.
    if (mode != M_PLAY || !wifiConnected || deviceCode.isEmpty() || playState != PS_IDLE) {
      vTaskDelay(pdMS_TO_TICKS(500));
      continue;
    }

    // اول فهرست آواها در تسک پخش (اولویت بالاتر) بارگذاری شود تا
    // دو درخواست TLS هم‌زمان در لحظهٔ اتصال شکل نگیرد.
    if (!listLoaded && millis() < 30000UL) {
      vTaskDelay(pdMS_TO_TICKS(300));
      continue;
    }

    if (!eventCursorLoaded) {
      if (!fetchEventCursor()) {
        vTaskDelay(pdMS_TO_TICKS(3000));
        continue;
      }
    }

    fetchEvents();
    vTaskDelay(pdMS_TO_TICKS(200));
  }
}

bool fetchEventCursor() {
  if (!netAcquire()) return false;
  bool ok = fetchEventCursorInternal();
  netRelease();
  return ok;
}

static bool fetchEventCursorInternal() {
  WiFiClientSecure client;
  client.setInsecure();
  client.setHandshakeTimeout(15);

  HTTPClient http;
  http.setConnectTimeout(HTTP_TIMEOUT_MS);
  http.setTimeout(HTTP_TIMEOUT_MS);
  http.setReuse(false);

  String url = String(SERVER_BASE) + VOICE_EVENTS_CURSOR_PATH +
               "?code=" + urlEncodeValue(deviceCode);
  Serial.printf("[Event] cursor %s\n", url.c_str());

  if (!http.begin(client, url)) return false;
  int code = http.GET();
  if (code != 200) {
    Serial.printf("[Event] cursor HTTP %d\n", code);
    http.end();
    return false;
  }

  String payload = http.getString();
  http.end();

  JsonDocument doc;
  if (deserializeJson(doc, payload)) return false;

  lastEventId = doc["afterId"].as<unsigned long long>();
  eventCursorLoaded = true;
  saveLastEventId();
  Serial.printf("[Event] initial cursor=%llu\n", lastEventId);
  return true;
}

void fetchEvents() {
  if (!netAcquire()) return;
  fetchEventsInternal();
  netRelease();
}

static void fetchEventsInternal() {
  WiFiClientSecure client;
  client.setInsecure();
  client.setHandshakeTimeout(15);

  HTTPClient http;
  http.setConnectTimeout(HTTP_TIMEOUT_MS);
  http.setTimeout(EVENT_HTTP_TIMEOUT_MS);
  http.setReuse(false);

  String url = String(SERVER_BASE) + VOICE_EVENTS_PATH +
               "?code=" + urlEncodeValue(deviceCode) +
               "&afterId=" + String(lastEventId) +
               "&wait=" + String(EVENT_POLL_WAIT_SEC);

  if (!http.begin(client, url)) {
    Serial.println("[Event] begin failed");
    delay(2000);
    return;
  }
  http.addHeader("Accept", "application/json");

  int code = http.GET();
  if (code != 200) {
    Serial.printf("[Event] HTTP %d\n", code);
    http.end();
    delay(3000);
    return;
  }

  String payload = http.getString();
  http.end();

  JsonDocument doc;
  DeserializationError err = deserializeJson(doc, payload);
  if (err) {
    Serial.printf("[Event] json error: %s\n", err.c_str());
    return;
  }

  unsigned long long after = doc["afterId"].as<unsigned long long>();
  JsonArray arr = doc["events"].as<JsonArray>();

  uint8_t count = 0;
  for (JsonVariant v : arr) {
    String u = v["audioUrl"] | "";
    if (u.length() == 0) continue;

    VoiceEventItem item;
    item.id       = v["id"].as<long long>();
    item.repeat   = (uint8_t)constrain((int)(v["repeatCount"] | 1), 1, 50);
    item.priority = v["priority"] | 10;
    item.isTest   = v["isTest"] | false;
    copyText(item.key,   sizeof(item.key),   v["actionKey"] | "");
    copyText(item.title, sizeof(item.title), v["titleFa"] | "");
    copyText(item.url,   sizeof(item.url),   u.c_str());
    item.queuedAt = millis();

    pushEvent(item);
    count++;
  }

  if (after > lastEventId) {
    lastEventId = after;
    saveLastEventId();
  }

  if (count > 0) {
    Serial.printf("[Event] received %u event(s), cursor=%llu\n",
                  (unsigned)count, lastEventId);
  }
}

// ==================== تسک پخش ====================
void audioTask(void *arg) {
  (void)arg;

  for (;;) {
    if (mode != M_PLAY) {
      if (playState != PS_IDLE) stopPlayback();
      sessionActive = false;
      eventPlaying = false;
      eventRepeatLeft = 0;
      eventAttempts = 0;
      btnPrevEvents = btnPlayEvents = btnNextEvents = 0;

      VoiceEventItem dropped;
      while (dequeueEvent(dropped)) { }

      vTaskDelay(pdMS_TO_TICKS(20));
      continue;
    }

    handleAudioButtons();

    if (playState == PS_PLAYING) {
      bool running = gen ? gen->loop() : false;
      if (!running) handleTrackFinished();
      vTaskDelay(pdMS_TO_TICKS(1));
      continue;
    }

    if (playState == PS_PAUSED) {
      vTaskDelay(pdMS_TO_TICKS(20));
      continue;
    }

    // بی‌کار: رویدادهای در صف بلافاصله پخش می‌شوند
    if (!eventPlaying) {
      VoiceEventItem ev;
      if (dequeueEvent(ev)) {
        startEvent(ev);
        continue;
      }
    }

    // تلاش مجدد برای شروع رویدادی که شبکه‌اش خطا داد (حداکثر ۳ بار)
    if (eventPlaying && (int32_t)(millis() - retryAt) >= 0) {
      if (eventAttempts >= 3) {
        Serial.printf("[Event] give up: %s\n", currentEvent.title);
        eventPlaying = false;
      } else {
        eventAttempts++;
        if (!startFile(String(currentEvent.url))) retryAt = millis() + RETRY_DELAY_MS;
      }
      continue;
    }

    // حالت بی‌کار: فهرست را دوره‌ای تازه کن
    if (!eventPlaying && !sessionActive &&
        (lastPollTry == 0 || millis() - lastPollTry >= POLL_INTERVAL_MS)) {
      lastPollTry = millis();
      if (fetchVoices()) lastPoll = millis();

      if (AUTO_PLAY_ON_BOOT && !autoPlayDone && voiceCount > 0) {
        autoPlayDone = true;
        startSession(0);
        continue;
      }
    }

    // تلاش/تلاش مجدد برای شروع پخش سری
    if (sessionActive && millis() >= retryAt) {
      if (!startItem(currentIndex)) {
        retryAt = millis() + RETRY_DELAY_MS;
      }
    }

    vTaskDelay(pdMS_TO_TICKS(20));
  }
}

// ==================== دکمه‌ها ====================
void pollButtons() {
  for (uint8_t i = 0; i < 3; i++) {
#if BTN_ACTIVE_HIGH
    bool raw = (digitalRead(BTN_PINS[i]) == HIGH);
#else
    bool raw = (digitalRead(BTN_PINS[i]) == LOW);
#endif
    unsigned long now = millis();

    if (raw != btnLastRaw[i]) {
      btnLastRaw[i] = raw;
      btnChangeAt[i] = now;
    }

    if (now - btnChangeAt[i] >= BTN_DEBOUNCE_MS && raw != btnStable[i]) {
      btnStable[i] = raw;
      if (btnStable[i]) {
        (*BTN_EVENTS[i])++;
        Serial.printf("[Btn] gpio %u\n", BTN_PINS[i]);
      }
    }
  }
}

// ==================== نگهداری ====================
void maintenance() {
  static unsigned long lastDiag = 0;
  if (millis() - lastDiag < 300000) return;   // هر ۵ دقیقه
  lastDiag = millis();

  Serial.printf("[Diag] heap=%u rssi=%d voices=%u state=%u uptime=%lus\n",
                (unsigned)ESP.getFreeHeap(),
                wifiConnected ? WiFi.RSSI() : 0,
                (unsigned)voiceCount, (unsigned)playState,
                millis() / 1000UL);
}

// ==================== Setup / Loop ====================
void setup() {
  Serial.begin(115200);
  delay(300);
  Serial.println();
  Serial.println("=== AVA ESP32-C3 Player ===");

  audioLogger = &Serial;

  loadCfg();

  // دکمهٔ BOOT قبل از تبدیل شدن GPIO9 به خروجی LED خوانده می‌شود
  pinMode(BOOT_BTN_PIN, INPUT_PULLUP);

#if BTN_ACTIVE_HIGH
  pinMode(BTN_PREV_PIN, INPUT_PULLDOWN);
  pinMode(BTN_PLAY_PIN, INPUT_PULLDOWN);
  pinMode(BTN_NEXT_PIN, INPUT_PULLDOWN);
#else
  pinMode(BTN_PREV_PIN, INPUT_PULLUP);
  pinMode(BTN_PLAY_PIN, INPUT_PULLUP);
  pinMode(BTN_NEXT_PIN, INPUT_PULLUP);
#endif

  delay(50);
  bool forcePortal = (digitalRead(BOOT_BTN_PIN) == LOW);

  pinMode(LED_WIFI_PIN, OUTPUT);
  ledWrite(LED_WIFI_PIN, false);
  pinMode(LED_STATUS_PIN, OUTPUT);
  ledWrite(LED_STATUS_PIN, false);
  pinMode(LED_PLAY_PIN, OUTPUT);
  ledWrite(LED_PLAY_PIN, false);

  WiFi.persistent(false);
  WiFi.onEvent(onWifiEvent);

  portal.on("/", HTTP_GET, handlePortalRoot);
  portal.on("/save", HTTP_POST, handleSave);
  portal.on("/clear", HTTP_GET, handleClear);
  portal.onNotFound(handlePortalNotFound);

  // صف رویدادهای پخش فوری و تسک long-poll کم‌اولویت
  eventQueue = xQueueCreate(EVENT_QUEUE_SIZE, sizeof(VoiceEventItem));
  if (!eventQueue) Serial.println("[Boot] event queue create FAILED");

  netLock = xSemaphoreCreateMutex();
  if (!netLock) Serial.println("[Boot] network lock create FAILED");

  TaskHandle_t task = nullptr;
  xTaskCreate(audioTask, "audio", PLAYER_TASK_STACK, nullptr, 2, &task);
  if (!task) Serial.println("[Boot] audio task create FAILED");

  TaskHandle_t evTask = nullptr;
  if (eventQueue) {
    xTaskCreate(eventTask, "event", EVENT_TASK_STACK, nullptr,
                EVENT_TASK_PRIORITY, &evTask);
    if (!evTask) Serial.println("[Boot] event task create FAILED");
  }

  if (cfgSsid.isEmpty()) {
    Serial.println("[Boot] no saved WiFi -> portal");
    startPortal();
  } else if (deviceCode.isEmpty()) {
    Serial.println("[Boot] no device code -> portal");
    startPortal();
  } else if (forcePortal) {
    Serial.println("[Boot] BOOT held -> portal");
    startPortal();
  } else {
    startSta();
  }
}

void loop() {
  pollButtons();
  updateLeds();
  maintenance();

  switch (mode) {
    case M_STA:    loopSta();             break;
    case M_PLAY:   keepTx();              break;
    case M_PORTAL: portal.handleClient(); break;
  }

  delay(2);
}
