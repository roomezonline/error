
#include <WiFi.h>
#include <WebServer.h>
#include <Preferences.h>
#include <LittleFS.h>
#include "esp_wifi.h"

#include "AudioLogger.h"
#include "AudioFileSourceHTTPRange.h"
#include "AudioFileSourceFS.h"
#include "AudioFileSourceBuffer.h"
#include "AudioGeneratorMP3.h"
#include "AudioOutputI2S.h"

#include <HTTPClient.h>
#include <ArduinoJson.h>

#include "smartgadjet_config.h"   // همهٔ پین‌ها، تایمرها، آدرس سرور و تنظیمات (سراسری)

#include <mbedtls/sha256.h>

// ==================== خروجی I2S با شمارش/بررسی خطا ====================
// کتابخانهٔ صوتی نتیجهٔ i2s_channel_write را چک نمی‌کند؛ اینجا write فقط وقتی
// موفق حساب می‌شود که هر ۴ بایت واقعاً به DMA داده شود. تعداد فریم‌های ارسال‌شده
// هم نگه داشته می‌شود تا در لاگ، مدت واقعی پخش با مدت فایل مقایسه شود.
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

// ==================== حالت‌های سیستم ====================
// M_STA    : فقط Station، در حال تلاش برای اتصال (سبک‌ترین حالت رادیویی)
// M_PLAY   : Station + ثبت‌نام/پول سرور + پخش صدا (وب‌سرور خاموش)
// M_PORTAL : AP + وب‌سرور فقط برای تنظیمات (بدون پخش، بدون STA فعال)
enum Mode : uint8_t { M_STA, M_PLAY, M_PORTAL };
Mode mode = M_STA;

// همهٔ پین‌ها، تایمرها، آدرس سرور و پارامترهای قابل تنظیم در smartgadjet_config.h هستند.

// بافر استریم و بافرهای دیکودر یک‌بار (استاتیک) رزرو می‌شوند؛
// در هر پخش malloc/free بزرگ نداریم تا heap با گذشت زمان تکه‌تکه نشود.
static uint8_t streamBuffer[STREAM_BUFFER_BYTES] alignas(16);
static uint8_t mp3Buff  [AudioGeneratorMP3::preAllocBuffSize()]   alignas(8);
static uint8_t mp3Stream[AudioGeneratorMP3::preAllocStreamSize()] alignas(8);
static uint8_t mp3Frame [AudioGeneratorMP3::preAllocFrameSize()]  alignas(8);
static uint8_t mp3Synth [AudioGeneratorMP3::preAllocSynthSize()]  alignas(8);

// ==================== آبجکت‌ها ====================
Preferences prefs;
WebServer   server(WEB_SERVER_PORT);

AudioGenerator*             gen      = nullptr;
AudioFileSourceHTTPRange*   http     = nullptr;   // منبع HTTP با ادامهٔ Range (پیام‌ها/آلارم)
AudioFileSourceFS*          fsSrc    = nullptr;   // منبع فایل کش‌شدهٔ آلارم در LittleFS
AudioFileSource*            activeSrc = nullptr;  // منبع فعال (برای getPos/getSize)
AudioFileSourceBuffer*     buffer   = nullptr;
TrackedAudioOutput*        out      = nullptr;

TaskHandle_t playerTaskHandle = nullptr;

String cfgSsid, cfgPass;

// ==================== وضعیت ====================
unsigned long lastWifiAttempt = 0;
unsigned long lastRestart     = 0;
unsigned long lastProgress    = 0;
unsigned long lastReport      = 0;
unsigned long lastDiag        = 0;

unsigned long lastHttpActivity = 0;
unsigned long lastProbe        = 0;
unsigned long probeInterval    = PROBE_MIN_MS;
unsigned long lostAtMs         = 0;
unsigned long restartAt        = 0;

uint32_t lastPos = 0;

volatile bool wifiConnected = false;
volatile bool playing  = false;
volatile bool playbackPaused = false;
volatile bool playingAlert = false;   // پخش فعلی، آلارم است (نه پیام — ack نمی‌گیرد)
volatile bool everPlayedOk = false;   // حداقل یک پیام کامل پخش شده (منطق offline → portal)

// ==================== هویت دستگاه و صف پیام‌ها ====================
String deviceNumber;                  // از NVS — اولین بوت از smartgadjet_config.h حک می‌شود
String deviceToken;                   // توکن ثبت‌نام از سرور (در NVS ذخیره می‌شود)
volatile uint32_t pendingCount = 0;   // تعداد پیام‌های معوق (برای LED پین ۱۰ + آلارم)
uint8_t  playAttempts  = 0;           // تلاش‌های فعلی برای پیام در نوبت
unsigned long lastPoll = 0;           // 0 = «الان پول کن»
unsigned long lastRegAttempt = 0;

// ── آلارم ──
String   firstSenderKey;              // فرستندهٔ پیام اول صف (بالاترین اولویت)
String   firstAlertUrl;               // مسیر آلارم او روی سرور ("" = آلارم ندارد)
String   firstAlertHash;
uint32_t firstAudioId = 0;            // audioId of firstPending
uint32_t lastAlertAudioId = 0;        // audioId of the alert that just played
unsigned long postAlertPlayUntil = 0; // millis until which Play starts lastAlertAudioId              // هش فایل آلارم (برای تشخیص تغییر)
uint16_t alertIntervalSec = 15;       // تنظیم سراسری سرور: فاصلهٔ تکرار آلارم
unsigned long lastAlertAt    = 0;     // آخرین شروع پخش آلارم
unsigned long lastCacheTry   = 0;     // آخرین تلاش دانلود آلارم
String   cachedKey, cachedHash;       // مشخصات آلارمِ کش‌شده در LittleFS

// ── صف پیام‌های معوق (به ترتیب اولویتِ تعریف‌شده در سرور) ──
uint32_t qAudioId[QUEUE_MAX];
String   qIntroUrl[QUEUE_MAX];        // مسیر صدای معرفی هر پیام ("": ندارد)
uint8_t  qCount = 0;
bool     manualPick = false;          // user selected a message with next/prev
volatile int8_t selIndex = 0;         // پیام انتخاب‌شده با دکمه‌های قبلی/بعدی

// ── پخش سری (پس از دکمهٔ پخش/تأیید) — عکس فوری از صف ──
volatile bool sessionMode = false;
bool     sessionSkipIntro = false;    // skip first item's intro (alert already played it)
uint8_t  sessionCount = 0;
uint8_t  sessionIdx   = 0;
uint32_t sAudioId[QUEUE_MAX];
String   sIntroUrl[QUEUE_MAX];
volatile bool playingIntro = false;   // پخش فعلی، صدای معرفی است (ack ندارد)
uint32_t introTargetAudio = 0;        // اینتروی کدام پیام در حال پخش است

// ── دکمه‌ها (قبلی/پخش/بعدی) ──
volatile uint8_t btnPrevEvents = 0;   // فشردن‌های تأییدشده (تسک پخش مصرف می‌کند)
volatile uint8_t btnPlayEvents = 0;
volatile uint8_t btnNextEvents = 0;
const uint8_t BTN_PINS[3] = { BTN_PREV_PIN, BTN_PLAY_PIN, BTN_NEXT_PIN };
volatile uint8_t* const BTN_EVENTS[3] = { &btnPrevEvents, &btnPlayEvents, &btnNextEvents };
bool     btnStable[3]  = { false, false, false };
bool     btnLastRaw[3] = { false, false, false };
unsigned long btnChangeAt[3] = { 0, 0, 0 };

uint8_t  targetChannel   = 0;
uint8_t  targetBssid[6]  = {0};
bool     haveTarget      = false;
uint32_t wifiFailCount   = 0;
uint8_t  failedStreak    = 0;

// ==================== WiFi helpers ====================
inline void applyTxPower() {
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
  switch (event) {
    case ARDUINO_EVENT_WIFI_STA_GOT_IP:
      wifiConnected = true;
      failedStreak  = 0;
      Serial.print("[WiFi] Connected. IP: ");
      Serial.print(WiFi.localIP());
      Serial.printf("  ch=%d rssi=%d tx=%d\n",
                    WiFi.channel(), WiFi.RSSI(), (int)WiFi.getTxPower());
      break;

    case ARDUINO_EVENT_WIFI_STA_DISCONNECTED:
      wifiConnected = false;
      Serial.printf("[WiFi] Disconnected, reason=%d\n",
                    info.wifi_sta_disconnected.reason);
      break;

    default:
      break;
  }
}

void updateLeds();

// اسکن غیرمسدودکننده با به‌روزرسانی LED در طول اسکن
int16_t scanWifiAnimated() {
  int16_t n = WiFi.scanNetworks(true, true);
  unsigned long started = millis();
  while (n == WIFI_SCAN_RUNNING && millis() - started < WIFI_SCAN_TIMEOUT_MS) {
    updateLeds();
    delay(20);
    n = WiFi.scanComplete();
  }
  if (n == WIFI_SCAN_RUNNING) {
    WiFi.scanDelete();
    n = WIFI_SCAN_FAILED;
  }
  return n;
}

// اسکن برای پیدا کردن BSSID و کانال دقیق AP هدف
void findTargetAP() {
  if (cfgSsid.isEmpty()) { haveTarget = false; return; }

  Serial.println("[WiFi] Scanning...");
  int n = scanWifiAnimated();
  int best = -1, bestRssi = -1000;

  for (int i = 0; i < n; i++) {
    if (WiFi.SSID(i) == cfgSsid && WiFi.RSSI(i) > bestRssi) {
      bestRssi = WiFi.RSSI(i);
      best = i;
    }
  }

  if (best >= 0) {
    targetChannel = (uint8_t)WiFi.channel(best);
    memcpy(targetBssid, WiFi.BSSID(best), 6);
    haveTarget = true;
    Serial.printf("[WiFi] Target found ch=%d rssi=%d\n", targetChannel, bestRssi);
  } else {
    haveTarget = false;
    Serial.printf("[WiFi] Target \"%s\" not found\n", cfgSsid.c_str());
  }
  if (n > 0) WiFi.scanDelete();
}

void connectWifi(bool eraseStored) {
  WiFi.disconnect(true, eraseStored);
  delay(100);
  WiFi.mode(WIFI_STA);
  WiFi.setSleep(false);

  if (haveTarget && !eraseStored) {
    WiFi.begin(cfgSsid.c_str(), cfgPass.c_str(), targetChannel, targetBssid);
  } else {
    WiFi.begin(cfgSsid.c_str(), cfgPass.c_str());
  }
  applyTxPower();
}

// ==================== ارتباط با سرور ====================
bool httpRequest(const char* method, const String& path, const String& body, String& response) {
  HTTPClient http;
  http.setTimeout(HTTP_TIMEOUT_MS);
  http.setConnectTimeout(HTTP_TIMEOUT_MS);

  String url = String(SERVER_BASE) + path;
  if (!http.begin(url)) {
    response = "http.begin failed";
    return false;
  }

  int code;
  if (strcmp(method, "POST") == 0) {
    http.addHeader("Content-Type", "application/json");
    code = http.POST(body);
  } else {
    code = http.GET();
  }

  bool ok = (code >= 200 && code < 300);
  response = ok ? http.getString() : ("HTTP " + String(code) + ": " + http.getString());
  http.end();
  return ok;
}

// ثبت‌نام دستگاه (یک‌بار در عمر دستگاه؛ توکن در NVS ذخیره می‌شود)
bool registerDevice() {
  String body = String("{\"uid\":\"") + WiFi.macAddress() +
                "\",\"code\":\"" + deviceNumber + "\",\"fw\":\"" DEVICE_FW "\"}";
  String resp;

  if (!httpRequest("POST", "/api/device/register", body, resp)) {
    Serial.printf("[Dev] register failed: %s\n", resp.c_str());
    if (resp.indexOf("code_taken") >= 0) {
      Serial.printf("[Dev] ERROR: number \"%s\" already used by another device — fix DEVICE_NUMBER!\n",
                    deviceNumber.c_str());
      lastRegAttempt = millis() - REG_RETRY_MS + REG_SLOW_RETRY_MS;  // تلاش کندتر
    }
    return false;
  }

  JsonDocument doc;
  if (deserializeJson(doc, resp) != DeserializationError::Ok || !doc["ok"].as<bool>()) {
    Serial.printf("[Dev] register bad response: %s\n", resp.c_str());
    return false;
  }

  String code  = doc["code"].as<String>();
  String token = doc["token"].as<String>();
  if (token.isEmpty()) {
    Serial.println("[Dev] register: empty token from server");
    return false;
  }

  prefs.begin("dev", false);
  if (!code.isEmpty()) prefs.putString("num", code);
  prefs.putString("token", token);
  prefs.end();

  if (!code.isEmpty()) deviceNumber = code;
  deviceToken = token;
  Serial.printf("[Dev] registered OK: number=%s\n", deviceNumber.c_str());
  return true;
}

// بررسی وضعیت و دریافت صف پیام‌های معوق (سرور: اولویتی که در پنل تعریف شده، نه زمان دریافت)
bool pollStatus() {
  String path = String("/api/device/status?t=") + deviceToken +
                "&rssi=" + String(WiFi.RSSI()) + "&fw=" DEVICE_FW;
  String resp;

  if (!httpRequest("GET", path, "", resp)) {
    Serial.printf("[Dev] status failed: %s\n", resp.c_str());

    // توکن نامعتبر (مثلاً دیتابیس سرور عوض/پاک شده): توکن ذخیره‌شده را دور بریز
    // تا حلقهٔ پخش در پول بعدی خودکار ثبت‌نام کند و توکن تازه بگیرد.
    if (resp.indexOf("HTTP 401") >= 0) {
      Serial.println("[Dev] unauthorized -> clearing token and re-registering");
      prefs.begin("dev", false);
      prefs.remove("token");
      prefs.end();
      deviceToken = "";
      pendingCount = 0;
      qCount = 0;
      selIndex = 0;
      manualPick = false;
      firstAudioId = 0;
      lastAlertAudioId = 0;
      postAlertPlayUntil = 0;
      lastPoll = 0;
      lastRegAttempt = millis() - REG_RETRY_MS;
    }
    return false;
  }

  JsonDocument doc;
  if (deserializeJson(doc, resp) != DeserializationError::Ok) {
    Serial.printf("[Dev] status bad json: %s\n", resp.c_str());
    return false;
  }
  if (!doc["ok"].as<bool>()) return false;

  pendingCount = doc["pending"].as<uint32_t>();

  // فاصلهٔ تکرار آلارم (تنظیم سراسری سرور)
  uint16_t iv = doc["alertIntervalSec"] | (uint16_t)15;
  if (iv < 5) iv = 5;
  if (iv > 3600) iv = 3600;
  alertIntervalSec = iv;

  // آلارم فرستندهٔ پیام اول صف
  JsonVariant fp = doc["firstPending"];
  if (!fp.isNull()) {
    firstAudioId   = fp["audioId"] | (uint32_t)0;
    firstSenderKey = fp["senderKey"] | "";
    firstAlertUrl  = fp["alertUrl"]  | "";
    firstAlertHash = fp["alertHash"] | "";
  } else {
    firstAudioId   = 0;
    firstSenderKey = "";
    firstAlertUrl  = "";
    firstAlertHash = "";
  }

  // صف پیام‌ها (به ترتیب اولویت) — با صدای معرفی هر پیام
  JsonArray q = doc["queue"].as<JsonArray>();
  qCount = 0;
  for (JsonVariant item : q) {
    if (qCount >= QUEUE_MAX) break;
    qAudioId[qCount]  = item["audioId"].as<uint32_t>();
    qIntroUrl[qCount] = item["introUrl"] | "";
    qCount++;
  }

  if (qCount == 0)       { selIndex = 0; manualPick = false; }
  else if (selIndex < 0) selIndex = 0;
  else if (selIndex >= qCount) selIndex = (int8_t)(qCount - 1);

  Serial.printf("[Dev] pending=%u queue=%u sel=%d\n",
                (unsigned)pendingCount, (unsigned)qCount, selIndex);

  // همه خوانده شد → حالت پخش سری تمام؛ پیام بعدی دوباره آلارم + دکمه لازم دارد
  if (pendingCount == 0) {
    sessionMode = false;
    manualPick = false;
    lastAlertAt = 0;
  }
  return true;
}

// اعلام نتیجهٔ پخش: played = پایان کامل پخش، failed = شکست بعد از چند تلاش
bool sendAck(uint32_t audioId, const char* status) {
  String body = String("{\"audioId\":") + String(audioId) + ",\"status\":\"" + status + "\"}";
  String resp;
  bool ok = httpRequest("POST", String("/api/device/ack?t=") + deviceToken, body, resp);
  Serial.printf("[Dev] ack %u %s -> %s\n", audioId, status, ok ? "ok" : resp.c_str());
  return ok;
}

// ==================== Audio ====================
// فقط MP3 پخش می‌شود؛ پروژهٔ ربات هر فایل صوتی را قبل از ذخیره در هاست به MP3 تبدیل می‌کند.
void stopPlayback() {
  if (gen)    { gen->stop();  delete gen;    gen = nullptr; }
  if (buffer) { delete buffer; buffer = nullptr; }
  if (out) {
    if (out->framesSent) {
      uint32_t hz = out->lastRate > 0 ? (uint32_t)out->lastRate : 44100;
      Serial.printf("[Audio] played=%.2fs at %u Hz, write-fails=%u\n",
                    (double)out->framesSent / hz, (unsigned)hz,
                    (unsigned)out->writeFails);
    }
    delete out;    out = nullptr;
  }
  if (http)   { delete http;   http = nullptr; }
  if (fsSrc)  { delete fsSrc;  fsSrc = nullptr; }
  activeSrc   = nullptr;
  playing     = false;
  playbackPaused = false;
  playingAlert = false;
  playingIntro = false;
  introTargetAudio = 0;
  postAlertPlayUntil = 0;
}

// شروع پخش با یک منبع آماده (مشترک بین پیام HTTP و آلارم کش‌شده)
// useStreamBuffer=false برای فایل محلی (LittleFS) است تا مستقیم و بدون بافر
// حلقه‌ای خوانده شود؛ در این حالت getPos دقیقاً «بایت مصرف‌شده» را می‌دهد.
bool beginDecode(AudioFileSource* src, bool useStreamBuffer = true) {
  if (useStreamBuffer) {
    buffer = new AudioFileSourceBuffer(src, streamBuffer, STREAM_BUFFER_BYTES);
  } else {
    buffer = nullptr;
  }

  out = new TrackedAudioOutput();
  out->SetPinout(I2S_BCLK, I2S_LRC, I2S_DOUT);
  out->SetGain(VOLUME_GAIN);

  gen = new AudioGeneratorMP3(mp3Buff, (int)sizeof(mp3Buff),
                              mp3Stream, (int)sizeof(mp3Stream),
                              mp3Frame, (int)sizeof(mp3Frame),
                              mp3Synth, (int)sizeof(mp3Synth));
  AudioFileSource* feed = useStreamBuffer ? (AudioFileSource*)buffer : src;
  if (!gen->begin(feed, out)) {
    Serial.println("[Player] Decoder start failed (mp3)");
    stopPlayback();
    return false;
  }

  activeSrc    = src;
  playing      = true;
  playbackPaused = false;
  lastProgress = millis();
  lastReport   = millis();
  lastPos      = 0;
  return true;
}

// پخش پیام (HTTP از سرور)
bool startPlayback(uint32_t audioId) {
  stopPlayback();

  String url = String(SERVER_BASE) + "/api/device/audio/" + String(audioId) + "?t=" + deviceToken;
  if (url.length() > 120) {
    Serial.println("[Player] URL too long for audio library buffer!");
  }

  http = new AudioFileSourceHTTPRange();
  if (!http->open(url.c_str())) {
    Serial.printf("[Player] HTTP open failed (audio %u)\n", audioId);
    stopPlayback();
    return false;
  }

  if (!beginDecode(http)) return false;
  playingAlert = false;
  playingIntro = false;
  Serial.printf("[Player] Playing audio %u (%u bytes)\n", audioId, (unsigned)http->getSize());
  return true;
}

// پخش صدای معرفی یک پیام (قبل از خود پیام) — استریم مستقیم از سرور
bool startIntroStream(uint32_t audioId, const String& introPath) {
  stopPlayback();

  String url = String(SERVER_BASE) + introPath + "?t=" + deviceToken;
  if (url.length() > 120) {
    Serial.println("[Player] intro URL too long for audio library buffer!");
  }

  http = new AudioFileSourceHTTPRange();
  if (!http->open(url.c_str())) {
    Serial.println("[Player] intro HTTP open failed -> message directly");
    stopPlayback();
    return false;
  }

  if (!beginDecode(http)) return false;
  playingIntro = true;
  introTargetAudio = audioId;
  Serial.printf("[Player] Playing intro for audio %u (%u bytes)\n",
                audioId, (unsigned)http->getSize());
  return true;
}

// Play the title/intro of the manually selected queue item (alert mode, no ACK).
bool startSelectedAlert() {
  if (selIndex < 0 || selIndex >= qCount) return false;
  if (qIntroUrl[selIndex].isEmpty()) return false;

  stopPlayback();

  String url = String(SERVER_BASE) + qIntroUrl[selIndex] + "?t=" + deviceToken;
  if (url.length() > 120) {
    Serial.println("[Player] selected title URL too long for audio library buffer!");
  }

  http = new AudioFileSourceHTTPRange();
  if (!http->open(url.c_str())) {
    Serial.println("[Player] selected title HTTP open failed");
    stopPlayback();
    return false;
  }

  if (!beginDecode(http)) return false;
  playingAlert = true;
  lastAlertAudioId = qAudioId[selIndex];
  postAlertPlayUntil = 0;
  lastAlertAt = millis();
  Serial.printf("[Player] Playing selected title #%d/%u (audio %u)\n",
                (int)selIndex + 1, (unsigned)qCount, (unsigned)qAudioId[selIndex]);
  return true;
}

// شروع پخش یک آیتم پخش سری: اول صدای معرفی (اگر دارد) و بعد خود پیام
bool startSessionItem(uint8_t idx) {
  if (idx >= sessionCount) return false;
  bool skipIntro = sessionSkipIntro;
  sessionSkipIntro = false;
  if (skipIntro) {
    Serial.printf("[Player] intro already heard -> content %u\n", (unsigned)sAudioId[idx]);
  } else if (!sIntroUrl[idx].isEmpty() &&
             startIntroStream(sAudioId[idx], sIntroUrl[idx])) return true;
  return startPlayback(sAudioId[idx]);
}

// شروع پخش سری از آیتم انتخاب‌شده (عکس فوری از صف فعلی)
void startSession(uint8_t fromIdx, bool skipFirstIntro) {
  if (fromIdx >= qCount) { sessionMode = false; sessionSkipIntro = false; return; }
  manualPick = false;
  sessionSkipIntro = skipFirstIntro;
  sessionCount = qCount;
  for (uint8_t i = 0; i < qCount; i++) {
    sAudioId[i]  = qAudioId[i];
    sIntroUrl[i] = qIntroUrl[i];
  }
  sessionIdx   = fromIdx;
  sessionMode  = true;
  playAttempts = 0;
  lastAlertAt  = 0;
  Serial.printf("[Player] session start: %u items from #%u\n",
                (unsigned)sessionCount, (unsigned)fromIdx);
}

// Start the session from the message whose alert was just played.
void startSessionForAlert() {
  int idx = -1;
  if (lastAlertAudioId) {
    for (uint8_t i = 0; i < qCount; i++) {
      if (qAudioId[i] == lastAlertAudioId) { idx = (int)i; break; }
    }
  }
  if (idx < 0) idx = (int)selIndex;
  if (idx >= 0 && idx < qCount) startSession((uint8_t)idx, true);
}

// یک آیتم جلو رفتن در پخش سری (بعد از پایان/رد پیام فعلی)
void sessionAdvance() {
  playAttempts = 0;
  if (sessionMode) sessionIdx++;
  lastPoll = 0;
}

// ── کش آلارم در LittleFS ──

// SHA-256 فایل به‌صورت ۱۲ رقم هگز — همان فرمت alertHash سرور
String fileHash12(const char* path) {
  File f = LittleFS.open(path, "r");
  if (!f) return "";
  mbedtls_sha256_context ctx;
  mbedtls_sha256_init(&ctx);
  if (mbedtls_sha256_starts(&ctx, 0) != 0) {
    mbedtls_sha256_free(&ctx);
    f.close();
    return "";
  }
  uint8_t buf[1024];
  size_t n;
  while ((n = f.read(buf, sizeof(buf))) > 0) {
    if (mbedtls_sha256_update(&ctx, buf, n) != 0) break;
  }
  uint8_t digest[32];
  int rc = mbedtls_sha256_finish(&ctx, digest);
  mbedtls_sha256_free(&ctx);
  f.close();
  if (rc != 0) return "";
  char hex[13];
  for (int i = 0; i < 6; i++) sprintf(&hex[i * 2], "%02x", digest[i]);
  hex[12] = 0;
  return String(hex);
}

void loadAlertMeta() {
  cachedKey = "";
  cachedHash = "";
  File f = LittleFS.open(ALERT_META_PATH, "r");
  if (!f) return;
  cachedKey  = f.readStringUntil('\n'); cachedKey.trim();
  cachedHash = f.readStringUntil('\n'); cachedHash.trim();
  f.close();
  Serial.printf("[FS] alert cache: key=\"%s\" hash=%s\n", cachedKey.c_str(), cachedHash.c_str());

  if (!cachedHash.isEmpty() && LittleFS.exists(ALERT_CACHE_PATH)) {
    String got = fileHash12(ALERT_CACHE_PATH);
    if (!got.equalsIgnoreCase(cachedHash)) {
      Serial.printf("[FS] cache hash mismatch (file=%s meta=%s) -> delete\n",
                    got.c_str(), cachedHash.c_str());
      LittleFS.remove(ALERT_CACHE_PATH);
      LittleFS.remove(ALERT_META_PATH);
      cachedKey = "";
      cachedHash = "";
    } else {
      Serial.println("[FS] alert cache verified");
    }
  }
}

bool alertCachedFor(const String& key, const String& hash) {
  if (key.isEmpty() || hash.isEmpty()) return false;
  if (key != cachedKey || hash != cachedHash) return false;
  return LittleFS.exists(ALERT_CACHE_PATH);
}

// دانلود آلارم به کش (اتمیک: tmp → rename)؛ خروجی true اگر کش آماده شد
bool ensureAlertCache() {
  if (firstAlertUrl.isEmpty() || firstSenderKey.isEmpty() || firstAlertHash.isEmpty()) return false;
  if (alertCachedFor(firstSenderKey, firstAlertHash)) return true;

  if (lastCacheTry != 0 && millis() - lastCacheTry < CACHE_RETRY_MS) return false;  // سرعت‌گیرِ تلاش مجدد
  lastCacheTry = millis();

  String url = String(SERVER_BASE) + firstAlertUrl + "?t=" + deviceToken;
  Serial.printf("[FS] downloading alert for \"%s\"...\n", firstSenderKey.c_str());

  HTTPClient h;
  h.setTimeout(HTTP_TIMEOUT_MS);
  h.setConnectTimeout(HTTP_TIMEOUT_MS);
  if (!h.begin(url)) {
    Serial.println("[FS] alert download: http.begin failed");
    return false;
  }
  int code = h.GET();
  if (code != 200) {
    h.end();
    Serial.printf("[FS] alert download failed: HTTP %d\n", code);
    return false;
  }
  int expected = h.getSize();

  File f = LittleFS.open(ALERT_TMP_PATH, "w");
  if (!f) {
    h.end();
    Serial.println("[FS] alert download: open tmp failed");
    return false;
  }
  int written = h.writeToStream(&f);
  f.close();
  h.end();

  if (written <= 0 || (expected > 0 && written != expected)) {
    LittleFS.remove(ALERT_TMP_PATH);
    Serial.printf("[FS] alert download incomplete: %d/%d\n", written, expected);
    return false;
  }

  String gotHash = fileHash12(ALERT_TMP_PATH);
  if (!gotHash.equalsIgnoreCase(firstAlertHash)) {
    LittleFS.remove(ALERT_TMP_PATH);
    Serial.printf("[FS] alert hash mismatch: got=%s expected=%s\n",
                  gotHash.c_str(), firstAlertHash.c_str());
    return false;
  }

  LittleFS.remove(ALERT_CACHE_PATH);
  if (!LittleFS.rename(ALERT_TMP_PATH, ALERT_CACHE_PATH)) {
    Serial.println("[FS] alert download: rename failed");
    return false;
  }

  File m = LittleFS.open(ALERT_META_PATH, "w");
  if (m) {
    m.println(firstSenderKey);
    m.println(firstAlertHash);
    m.close();
  }
  cachedKey  = firstSenderKey;
  cachedHash = firstAlertHash;
  Serial.printf("[FS] alert cached: %d bytes (key=\"%s\" hash=%s, FS %u/%u)\n",
                written, cachedKey.c_str(), cachedHash.c_str(),
                LittleFS.usedBytes(), LittleFS.totalBytes());
  return true;
}

// پخش آلارم از کش LittleFS
bool startAlertFromCache() {
  stopPlayback();
  if (!LittleFS.exists(ALERT_CACHE_PATH)) return false;

  fsSrc = new AudioFileSourceFS(LittleFS, ALERT_CACHE_PATH);
  if (!fsSrc->isOpen()) {
    Serial.println("[Player] alert cache open failed");
    stopPlayback();
    return false;
  }

  if (!beginDecode(fsSrc, false)) return false;
  playingAlert = true;
  lastAlertAudioId = firstAudioId;
  postAlertPlayUntil = 0;
  Serial.printf("[Player] Playing alert from cache (%u bytes)\n", (unsigned)fsSrc->getSize());
  return true;
}

// پخش آلارم با استریم مستقیم (وقتی کش آماده نشد)
bool startAlertFromHttp() {
  if (firstAlertUrl.isEmpty()) return false;
  stopPlayback();

  String url = String(SERVER_BASE) + firstAlertUrl + "?t=" + deviceToken;
  if (url.length() > 120) {
    Serial.println("[Player] alert URL too long for audio library buffer!");
  }

  http = new AudioFileSourceHTTPRange();
  if (!http->open(url.c_str())) {
    Serial.println("[Player] alert HTTP open failed");
    stopPlayback();
    return false;
  }

  if (!beginDecode(http)) return false;
  playingAlert = true;
  lastAlertAudioId = firstAudioId;
  postAlertPlayUntil = 0;
  Serial.printf("[Player] Playing alert (stream, %u bytes)\n", (unsigned)http->getSize());
  return true;
}

void handleStopped() {
  uint32_t size = activeSrc ? (uint32_t)activeSrc->getSize() : 0;
  uint32_t pos  = activeSrc ? (uint32_t)activeSrc->getPos()  : 0;
  bool     wasAlert = playingAlert;
  bool     wasIntro = playingIntro;
  uint32_t audioId  = wasIntro ? introTargetAudio
                               : (sessionMode && sessionIdx < sessionCount ? sAudioId[sessionIdx] : 0);

  Serial.printf("[Player] Stopped%s at %u/%u\n",
                wasAlert ? " (alert)" : (wasIntro ? " (intro)" : ""), pos, size);
  stopPlayback();

  // آلارم ack ندارد — فقط منتظر فاصلهٔ بعدی می‌مانیم
  if (wasAlert) {
    uint32_t endTolerance = size / 100;
    if (endTolerance > 32768) endTolerance = 32768;
    bool reachedEnd = (size == 0) ? (pos > 0)
                                  : (pos >= size || (pos > 0 && (size - pos) <= endTolerance));
    if (!reachedEnd) {
      Serial.printf("[Player] alert incomplete: stopped at byte %u of %u\n", pos, size);
    } else if (lastAlertAudioId) {
      postAlertPlayUntil = millis() + ALERT_PLAY_GRACE_MS;
      Serial.printf("[Player] alert finished -> play audio %u valid for %lu ms\n",
                    (unsigned)lastAlertAudioId, ALERT_PLAY_GRACE_MS);
    }
    return;
  }

  // اینترو تمام شد → خود پیام همان آیتم پخش می‌شود
  if (wasIntro) {
    if (sessionMode && sessionIdx < sessionCount) {
      startPlayback(sAudioId[sessionIdx]);
    }
    return;
  }

  if (audioId == 0) return;   // پیامی خارج از صف (نباید پیش بیاید)

  // بعضی MP3ها چند بایت تگ/پدینگ آخرشان خوانده نمی‌شود؛ اگر فقط همین مقدار
  // باقی مانده باشد، پیام را کامل بدان تا بی‌دلیل سه بار از اول پخش نشود.
  uint32_t endTolerance = size / 100;
  if (endTolerance > 32768) endTolerance = 32768;
  bool reachedEnd = (size > 0) &&
                    (pos >= size || (pos > 0 && (size - pos) <= endTolerance));

  if (reachedEnd) {
    // پیام کامل پخش شد (فایل، تا آخر خوانده شد) → ACK «پخش شد» برای جلوگیری از تکرار
    everPlayedOk = true;
    Serial.printf("[Player] Finished audio %u -> ack played\n", audioId);
    sendAck(audioId, "played");
    sessionAdvance();
  } else {
    playAttempts++;
    lastRestart = millis();   // تلاش بعدی با فاصله
    Serial.printf("[Player] Incomplete audio %u (attempt %u/%u)\n", audioId, playAttempts, PLAY_RETRY_MAX);
    if (playAttempts >= PLAY_RETRY_MAX) {
      Serial.printf("[Player] Giving up audio %u -> ack failed\n", audioId);
      sendAck(audioId, "failed");
      sessionAdvance();
    }
  }
}

// پخش در تسک جداگانه با استک بزرگ اجرا می‌شود (دیکودر به بیش از 8KB استک نیاز دارد)
// چرخهٔ فاز ۳:
//   ثبت‌نام → پول هر ۵ ثانیه (صف = به ترتیب اولویتِ تعریف‌شده در پنل، نه زمان دریافت)
//   پیام معوق هست؟ → حالت ALERT: پین ۱۰ چشمک + تکرار آلارمِ فرستندهٔ پیام اول هر N ثانیه
//   دکمهٔ قبلی/بعدی → جابه‌جایی بین پیام‌های صف (انتخاب)
//   دکمهٔ پخش/تأیید → پایان آلارم + پخش سری از پیام انتخاب‌شده؛
//     حین پخش پیام: توقف/ادامه از همان نقطه؛
//     قبل از هر پیام، صدای معرفی فرستنده (اگر ثبت شده باشد) پخش می‌شود
//   دکمهٔ بعدی حین پخش → رد کردن پیام جاری (ack = skipped)
//   همه خوانده شد → برگشت به پول/آلارم
void playerLoopTask(void* arg) {
  (void)arg;
  for (;;) {
    if (mode != M_PLAY) {
      if (playing) stopPlayback();
      sessionMode = false;
      sessionSkipIntro = false;
      manualPick = false;
      vTaskDelay(pdMS_TO_TICKS(20));
      continue;
    }

    if (!wifiConnected && !(playing && playingAlert && fsSrc)) {
      // قطع شبکه وسط پخش: بدون شمارش تلاش، بعد از اتصال مجدد از همان پیام ادامه می‌دهیم
      // استثنا: آلارمِ کش‌شده در LittleFS به شبکه نیاز ندارد و تا آخر پخش می‌شود؛
      // ولی پیام/اینترو/آلارمِ HTTP باید متوقف شوند.
      if (playing) {
        Serial.println("[Player] WiFi lost -> stop");
        stopPlayback();
      }
      vTaskDelay(pdMS_TO_TICKS(50));
      continue;
    }

    // ── ثبت‌نام (تا وقتی توکن نداریم) ──
    if (deviceToken.isEmpty()) {
      if (millis() - lastRegAttempt >= REG_RETRY_MS) {
        lastRegAttempt = millis();
        if (registerDevice()) lastPoll = 0;   // پول فوری بعد از ثبت‌نام
      }
      vTaskDelay(pdMS_TO_TICKS(20));
      continue;
    }

    // ── دکمهٔ پخش/تأیید (GPIO1): پایان آلارم + شروع پخش سری از پیام انتخاب‌شده ──
    if (btnPlayEvents > 0) {
      btnPlayEvents = 0;
      if (playing && !playingAlert && !playingIntro) {
        playbackPaused = !playbackPaused;
        lastProgress = millis();
        Serial.printf("[Btn] play -> %s\n", playbackPaused ? "pause" : "resume");
        vTaskDelay(pdMS_TO_TICKS(10));
        continue;
      } else if (playingIntro) {
        // تأیید حین پخش اینترو: اینترو را رد کن و پیام اصلی را شروع کن
        uint32_t id = introTargetAudio;
        stopPlayback();
        if (id) startPlayback(id);
        vTaskDelay(pdMS_TO_TICKS(10));
        continue;
      } else if (pendingCount == 0 || qCount == 0) {
        Serial.println("[Btn] play ignored (no pending messages)");
      } else {
        bool alertWasPlaying = playingAlert;
        bool inPostAlertGrace = (postAlertPlayUntil != 0) &&
                                ((int32_t)(millis() - postAlertPlayUntil) < 0);
        if (alertWasPlaying) stopPlayback();
        if (alertWasPlaying || inPostAlertGrace) startSessionForAlert();
        else startSession((uint8_t)selIndex, false);
        lastPoll = 0;
        vTaskDelay(pdMS_TO_TICKS(10));
        continue;
      }
    }

    // ── دکمهٔ بعدی (GPIO2): در انتظار = انتخاب بعدی؛ حین پخش = رد کردن پیام جاری ──
    if (btnNextEvents > 0) {
      btnNextEvents = 0;
      bool skipped = false;
      if (playing) {
        if (playingAlert) {
          // حین آلارم: قطع کن و در حالت انتخاب، عنوان بعدی را پخش کن
          stopPlayback();
          lastAlertAt = millis();
          playAttempts = 0;
        } else {
          uint32_t id = playingIntro ? introTargetAudio
                                     : (sessionMode && sessionIdx < sessionCount ? sAudioId[sessionIdx] : 0);
          bool hadSession = sessionMode;
          stopPlayback();
          if (id) sendAck(id, "skipped");
          playAttempts = 0;
          if (hadSession && sessionCount > 0) {
            sessionIdx = (uint8_t)((sessionIdx + 1) % sessionCount);
            lastPoll = 0;
            Serial.printf("[Btn] next during playback -> #%d/%u\n",
                          (int)sessionIdx + 1, (unsigned)sessionCount);
          }
          skipped = true;
        }
      }
      if (!skipped && !playing && !sessionMode && qCount > 0) {
        selIndex = (int8_t)((selIndex + 1) % qCount);
        manualPick = true;
        lastAlertAt = millis();
        Serial.printf("[Btn] next -> #%d/%u\n", (int)selIndex + 1, (unsigned)qCount);
        startSelectedAlert();
      }
      vTaskDelay(pdMS_TO_TICKS(10));
      continue;
    }

    // ── دکمهٔ قبلی (GPIO0): فقط در حالت انتظار (حین پخش بی‌اثر) ──
    if (btnPrevEvents > 0) {
      btnPrevEvents = 0;
      if (playing && !playingAlert) {
        uint32_t id = playingIntro ? introTargetAudio
                                   : (sessionMode && sessionIdx < sessionCount ? sAudioId[sessionIdx] : 0);
        bool hadSession = sessionMode;
        stopPlayback();
        if (id) sendAck(id, "skipped");
        playAttempts = 0;
        if (hadSession && sessionCount > 0) {
          sessionIdx = (uint8_t)((sessionIdx + sessionCount - 1) % sessionCount);
          lastPoll = 0;
          Serial.printf("[Btn] prev during playback -> #%d/%u\n",
                        (int)sessionIdx + 1, (unsigned)sessionCount);
        }
      } else if (!sessionMode && qCount > 0 && (!playing || playingAlert)) {
        if (playingAlert) stopPlayback();
        selIndex = (int8_t)((selIndex + qCount - 1) % qCount);
        manualPick = true;
        lastAlertAt = millis();
        Serial.printf("[Btn] prev -> #%d/%u\n", (int)selIndex + 1, (unsigned)qCount);
        startSelectedAlert();
      }
      vTaskDelay(pdMS_TO_TICKS(10));
      continue;
    }

    // ── پول وضعیت (فقط وقتی پخش نمی‌کنیم و پخش سری فعال نیست) ──
    if (!playing && !sessionMode && (lastPoll == 0 || millis() - lastPoll >= POLL_INTERVAL_MS)) {
      lastPoll = millis();
      pollStatus();
    }

    if (!playing) {
      // ── پخش سری: آیتم فعلی، سپس بعدی، تا پایان صف ──
      if (sessionMode) {
        if (sessionIdx >= sessionCount) {
          sessionMode = false;
          sessionSkipIntro = false;
          selIndex = 0;
          Serial.println("[Player] session finished");

          // اگر هنگام پخش پیام تازه‌ای رسیده باشد، بدون نیاز به دکمهٔ مجدد ادامه بده.
          if (pollStatus() && qCount > 0 && pendingCount > 0) {
            Serial.printf("[Player] session auto-continue (%u pending)\n",
                          (unsigned)pendingCount);
            startSession(0, false);
          } else {
            lastPoll = 0;   // پایان سری → وضعیت تازه
          }
        } else {
          // تلاش اول فوری، تلاش‌های بعدی با فاصله
          if (playAttempts > 0 && millis() - lastRestart < RESTART_INTERVAL) {
            vTaskDelay(pdMS_TO_TICKS(10));
            continue;
          }
          if (!startSessionItem(sessionIdx)) {
            playAttempts++;
            lastRestart = millis();
            if (playAttempts >= PLAY_RETRY_MAX) {
              Serial.printf("[Player] Giving up audio %u -> ack failed\n", sAudioId[sessionIdx]);
              sendAck(sAudioId[sessionIdx], "failed");
              sessionAdvance();
            }
          }
        }
        vTaskDelay(pdMS_TO_TICKS(10));
        continue;
      }

      // ── حالت ALERT: تکرار آلارم فرستندهٔ پیام اول صف ──
      if (!sessionMode && pendingCount > 0 && (manualPick || !firstAlertUrl.isEmpty())) {
        uint32_t intervalMs = (uint32_t)alertIntervalSec * 1000UL;
        bool inPostAlertGrace = (postAlertPlayUntil != 0) &&
                                ((int32_t)(millis() - postAlertPlayUntil) < 0);
        if (!inPostAlertGrace &&
            (lastAlertAt == 0 || millis() - lastAlertAt >= intervalMs)) {
          bool started = false;
          if (manualPick) {
            started = startSelectedAlert();
          } else if (alertCachedFor(firstSenderKey, firstAlertHash)) {
            started = startAlertFromCache();            // اولویت: کش آفلاین
          } else if (ensureAlertCache()) {
            started = startAlertFromCache();
          } else {
            started = startAlertFromHttp();             // fallback: استریم مستقیم
          }
          lastAlertAt = millis();
          if (!started) {
            Serial.println("[Player] alert playback failed");
          }
        }
      }

      vTaskDelay(pdMS_TO_TICKS(10));
      continue;
    }

    if (playbackPaused) {
      lastProgress = millis();
      vTaskDelay(pdMS_TO_TICKS(20));
      continue;
    }

    if (!gen->loop()) {
      handleStopped();
      continue;
    }

    uint32_t pos = activeSrc ? (uint32_t)activeSrc->getPos() : 0;
    if (pos != lastPos) {
      lastPos = pos;
      lastProgress = millis();
    } else if (millis() - lastProgress >= STALL_TIMEOUT_MS) {
      if (playingAlert) {
        // آلارم: بدون شمارش تلاش؛ تکرار بعدی طبق فاصله
        Serial.println("[Player] alert stalled");
        stopPlayback();
      } else if (playingIntro) {
        // اینترو گیر کرد → رد کن و مستقیم پیام را پخش کن
        Serial.println("[Player] intro stalled -> skip intro");
        uint32_t id = (sessionMode && sessionIdx < sessionCount) ? sAudioId[sessionIdx] : 0;
        stopPlayback();
        if (id) startPlayback(id);
      } else {
        Serial.println("[Player] stalled -> retry");
        playAttempts++;
        uint32_t id = (sessionMode && sessionIdx < sessionCount) ? sAudioId[sessionIdx] : 0;
        stopPlayback();
        if (playAttempts >= PLAY_RETRY_MAX) {
          Serial.printf("[Player] Giving up audio %u -> ack failed\n", id);
          if (id) sendAck(id, "failed");
          sessionAdvance();
        } else {
          lastRestart = millis();
        }
      }
      continue;
    }

    if (millis() - lastReport >= PROGRESS_REPORT_MS) {
      lastReport = millis();
      Serial.printf("[Player]%s %u/%u bytes\n",
                    playingAlert ? " (alert)" : (playingIntro ? " (intro)" : ""), pos,
                    activeSrc ? (unsigned)activeSrc->getSize() : 0);
    }

    vTaskDelay(pdMS_TO_TICKS(1));
  }
}

// ==================== پنل تنظیمات ====================
static const char PAGE_HTML[] PROGMEM = R"html(<!DOCTYPE html>
<html lang="fa" dir="rtl">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>تنظیمات وای‌فای</title>
<style>
*{box-sizing:border-box}
body{margin:0;padding:18px;font-family:Tahoma,system-ui,sans-serif;color:#eef1ff;
background:radial-gradient(900px 500px at 100% -10%,#20264a,transparent),#0e1020;min-height:100vh}
.card{max-width:430px;margin:0 auto;background:#151a2e;border:1px solid #262c4a;border-radius:20px;padding:20px;box-shadow:0 20px 45px rgba(0,0,0,.5)}
h1{margin:0 0 4px;font-size:18px}
.sub{color:#8f98bd;font-size:12.5px;margin:0;line-height:1.8}
.hdr{display:flex;align-items:center;justify-content:space-between;margin:16px 0 8px}
.hdr b{font-size:13px;color:#9aa3c8;font-weight:normal}
.hdr button{border:1px solid #2b3155;background:#11152c;color:#9aa3c8;border-radius:10px;padding:6px 12px;font-size:12px;cursor:pointer;font-family:inherit}
.hdr button:disabled{opacity:.5}
#nets{border:1px solid #262c4a;border-radius:14px;overflow-y:auto;max-height:230px;background:#0e1226}
.net{display:flex;align-items:center;gap:10px;padding:11px 12px;cursor:pointer;border-bottom:1px solid #1b2036;transition:background .15s}
.net:last-child{border-bottom:0}
.net:hover{background:#151b34}
.net.on{background:#1b2547}
.dot{width:9px;height:9px;border-radius:50%;flex:none}
.s{flex:1;font-size:14px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;text-align:start}
.r{font-size:11.5px;color:#8f98bd;flex:none}
.chk{color:#39d98a;flex:none;visibility:hidden}
.net.on .chk{visibility:visible}
.empty{padding:14px;color:#8f98bd;font-size:12.5px;text-align:center}
label{display:block;font-size:12.5px;color:#9aa3c8;margin:14px 0 6px}
input{width:100%;padding:12px;border-radius:12px;border:1px solid #2b3155;background:#0e1226;color:#eef1ff;font-size:15px;outline:none;font-family:inherit}
input:focus{border-color:#6d86ff}
.pw{position:relative}
.pw i{position:absolute;inset-inline-start:10px;top:50%;transform:translateY(-50%);cursor:pointer;font-style:normal;color:#8f98bd}
button{font-family:inherit}
.go{width:100%;margin-top:20px;padding:13px;border:0;border-radius:13px;color:#fff;font-size:15px;font-weight:bold;cursor:pointer;background:linear-gradient(135deg,#5872ff,#8b5cf6)}
.foot{margin-top:12px;font-size:12px;color:#8f98bd;text-align:center;line-height:1.9}
.foot a{color:#8f98bd}
.spin{display:inline-block;width:11px;height:11px;border:2px solid #2b3155;border-top-color:#6d86ff;border-radius:50%;animation:rot .7s linear infinite;vertical-align:-1px}
@keyframes rot{to{transform:rotate(360deg)}}
</style>
</head>
<body>
<div class="card">
  <h1>تنظیمات وای‌فای</h1>
  <div class="sub">شبکه را از لیست انتخاب کن، رمز را وارد کن و ذخیره بزن.</div>
  <div class="hdr"><b>شبکه‌های موجود</b><button id="rb" onclick="scan()">🔄 اسکن مجدد</button></div>
  <div id="nets"></div>
  <form method="POST" action="/save">
    <label>شبکه (SSID)</label>
    <input name="s" id="s" value="%SSID%" placeholder="از لیست انتخاب کن یا دستی بنویس" autocomplete="off" required>
    <label>رمز شبکه</label>
    <div class="pw"><input name="p" id="p" type="password" placeholder="رمز وای‌فای"><i onclick="tp()">👁</i></div>
    <button class="go" type="submit">ذخیره و اتصال</button>
  </form>
  <div class="foot">دستگاه: <b>%DEVNUM%</b> · MAC: <b>%MAC%</b><br>آدرس پنل: <b>192.168.4.1</b><br><a href="/clear" onclick="return clr()">پاک‌کردن تنظیمات ذخیره‌شده</a></div>
</div>
<script>
var saved=document.getElementById('s').value;
function tp(){var p=document.getElementById('p');p.type=(p.type=='password')?'text':'password';}
function clr(){return confirm('تنظیمات ذخیره‌شده پاک شود؟');}
function eh(t){return t.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');}
function pick(n){document.getElementById('s').value=n.dataset.s;
 var a=document.querySelectorAll('.net');for(var i=0;i<a.length;i++)a[i].classList.remove('on');
 n.classList.add('on');var p=document.getElementById('p');if(n.dataset.e==='0')p.value='';p.focus();}
function bar(r){var c=r>-60?'#39d98a':(r>-75?'#ffb020':'#ff5c7a');return '<span class="dot" style="background:'+c+'"></span>';}
async function scan(){var b=document.getElementById('nets'),rb=document.getElementById('rb');
 rb.disabled=true;b.innerHTML='<div class="empty"><span class="spin"></span> در حال اسکن...</div>';
 try{var r=await fetch('/scan?t='+Date.now());var j=await r.json();
  if(!j.length){b.innerHTML='<div class="empty">شبکه‌ای پیدا نشد</div>';}
  else{b.innerHTML=j.map(function(x){return '<div class="net'+(x.s===saved?' on':'')+'" data-s="'+eh(x.s)+'" data-e="'+x.e+'">'+bar(x.r)+
   '<span class="s">'+eh(x.s)+'</span><span class="r">'+(x.e?'🔒':'🔓')+' '+x.r+'</span><span class="chk">✓</span></div>';}).join('');}
 }catch(e){b.innerHTML='<div class="empty">اسکن ناموفق بود، دوباره بزن</div>';}
 rb.disabled=false;}
document.getElementById('nets').addEventListener('click',function(e){
 var n=e.target.closest?e.target.closest('.net'):null;if(n)pick(n);});
window.addEventListener('load',scan);
</script>
</body>
</html>)html";

String escHtml(const String& in) {
  String o = in;
  o.replace("&", "&amp;");
  o.replace("\"", "&quot;");
  o.replace("<", "&lt;");
  o.replace(">", "&gt;");
  return o;
}

String escJson(const String& in) {
  String o = in;
  o.replace("\\", "\\\\");
  o.replace("\"", "\\\"");
  return o;
}

void handleRoot() {
  lastHttpActivity = millis();
  String page = FPSTR(PAGE_HTML);
  page.replace("%SSID%", escHtml(cfgSsid));
  page.replace("%DEVNUM%", deviceNumber.isEmpty() ? "(بدون شماره)" : escHtml(deviceNumber));
  page.replace("%MAC%", WiFi.macAddress());
  server.send(200, "text/html; charset=utf-8", page);
}

void handleScan() {
  lastHttpActivity = millis();
  int n = scanWifiAnimated();

  // حذف تکراری‌ها (قوی‌ترین BSSID برای هر SSID) و مرتب‌سازی نزولی بر اساس RSSI
  int16_t idx[48];
  int cnt = 0;
  for (int i = 0; i < n && cnt < 48; i++) {
    if (WiFi.SSID(i).isEmpty()) continue;
    int dup = -1;
    for (int j = 0; j < cnt; j++) {
      if (WiFi.SSID(idx[j]) == WiFi.SSID(i)) { dup = j; break; }
    }
    if (dup < 0) idx[cnt++] = i;
    else if (WiFi.RSSI(i) > WiFi.RSSI(idx[dup])) idx[dup] = i;
  }
  for (int a = 0; a < cnt - 1; a++) {
    for (int b = a + 1; b < cnt; b++) {
      if (WiFi.RSSI(idx[b]) > WiFi.RSSI(idx[a])) {
        int16_t t = idx[a]; idx[a] = idx[b]; idx[b] = t;
      }
    }
  }

  String json = "[";
  for (int i = 0; i < cnt; i++) {
    if (i) json += ',';
    int k = idx[i];
    json += "{\"s\":\"" + escJson(WiFi.SSID(k)) + "\",\"r\":" +
            String(WiFi.RSSI(k)) + ",\"e\":" +
            String(WiFi.encryptionType(k) == WIFI_AUTH_OPEN ? 0 : 1) + "}";
  }
  json += ']';
  if (n > 0) WiFi.scanDelete();
  server.send(200, "application/json", json);
}

void saveCfg(const String& s, const String& p) {
  prefs.begin("cfg", false);
  prefs.putString("ssid", s);
  prefs.putString("pass", p);
  prefs.end();
}

void loadCfg() {
  prefs.begin("cfg", true);
  cfgSsid = prefs.getString("ssid", "");
  cfgPass = prefs.getString("pass", "");
  prefs.end();
  Serial.printf("[Cfg] ssid=\"%s\"\n", cfgSsid.c_str());
}

// ==================== هویت دستگاه (NVS) ====================
// طبق smartgadjet_config.h: اگر BURN_DEVICE_NUMBER=1 باشد شماره در هر بوت در NVS نوشته می‌شود؛
// اگر 0 باشد فقط وقتی NVS خالی است نوشته می‌شود (شمارهٔ حک‌شدهٔ قبلی معتبر می‌ماند).
void loadDeviceIdentity() {
  prefs.begin("dev", false);

  String stored = prefs.getString("num", "");
  String configured = String(DEVICE_NUMBER);
  configured.trim();
  bool wrote = false;

  if (BURN_DEVICE_NUMBER) {
    if (configured.isEmpty()) {
      Serial.println("[Dev] BURN enabled but DEVICE_NUMBER is empty!");
    } else if (stored != configured) {
      prefs.putString("num", configured);
      stored = configured;
      wrote = true;
    }
  } else if (stored.isEmpty() && !configured.isEmpty()) {
    prefs.putString("num", configured);
    stored = configured;
    wrote = true;
  }

  deviceNumber = stored;
  deviceToken  = prefs.getString("token", "");
  prefs.end();

  Serial.printf("[Dev] number=\"%s\"%s, token=%s\n",
                deviceNumber.c_str(),
                wrote ? " (written to NVS now)" : " (kept from NVS)",
                deviceToken.isEmpty() ? "none" : "stored");
}

void handleSave() {
  lastHttpActivity = millis();
  String s = server.arg("s"); s.trim();
  String p = server.arg("p");

  if (s.isEmpty()) {
    server.send(400, "text/html; charset=utf-8",
                "<meta charset='utf-8'>SSID خالی است.");
    return;
  }

  saveCfg(s, p);
  Serial.printf("[Portal] saved ssid=\"%s\" -> restart\n", s.c_str());
  server.send(200, "text/html; charset=utf-8",
              "<!DOCTYPE html><meta charset='utf-8'><body style='background:#0e1020;color:#eef1ff;font-family:Tahoma;text-align:center;padding-top:80px'>"
              "<h2>ذخیره شد ✅</h2><p>دستگاه در حال ری‌استارت و اتصال به وای‌فای است...</p></body>");
  restartAt = millis() + 1500;
}

void handleClear() {
  lastHttpActivity = millis();
  prefs.begin("cfg", false);
  prefs.clear();
  prefs.end();
  Serial.println("[Portal] config cleared -> restart");
  server.send(200, "text/html; charset=utf-8",
              "<!DOCTYPE html><meta charset='utf-8'><body style='background:#0e1020;color:#eef1ff;font-family:Tahoma;text-align:center;padding-top:80px'>"
              "<h2>پاک شد</h2><p>در حال ری‌استارت. بعد از ری‌استارت وارد پنل تنظیمات شو.</p></body>");
  restartAt = millis() + 1500;
}

void handleNotFound() {
  lastHttpActivity = millis();
  server.sendHeader("Location", "/");
  server.send(302, "text/plain", "");
}

// ==================== ورود/خروج حالت‌ها ====================
void startSta() {
  mode = M_STA;
  wifiFailCount = 0;
  wifiConnected = false;
  // فشردن‌های حالت پنل بی‌اثر باشند + صف حالت قبلی پاک شود
  btnPrevEvents = btnPlayEvents = btnNextEvents = 0;
  sessionMode = false;
  qCount = 0;
  selIndex = 0;
  WiFi.mode(WIFI_STA);
  WiFi.setSleep(false);
  findTargetAP();
  connectWifi(false);
  lastWifiAttempt = millis();
  Serial.printf("[Mode] STA connect to \"%s\"\n", cfgSsid.c_str());
}

void startPortal() {
  mode = M_PORTAL;                      // تسک پخش خودش صدا را متوقف می‌کند
  for (int i = 0; i < 200 && playing; i++) delay(5);   // حداکثر ۱ ثانیه صبر
  Serial.println("[Mode] -> PORTAL");
  wifiConnected = false;

  WiFi.disconnect(true, false);
  delay(50);
  WiFi.mode(WIFI_AP_STA);   // AP + STA آزاد (برای اسکن)
  delay(50);
  WiFi.softAP(AP_SSID, AP_PASS);
  applyTxPower();

  server.begin();
  lastHttpActivity = millis();
  lastProbe        = millis();
  probeInterval    = (failedStreak >= 2) ? PROBE_MAX_MS : PROBE_MIN_MS;
  restartAt        = 0;

  Serial.print("[Portal] AP \"" AP_SSID "\" up -> http://");
  Serial.println(WiFi.softAPIP());
}

void stopPortal() {
  server.stop();
  WiFi.softAPdisconnect(true);
  delay(50);
}

// در پنل، فقط وقتی کسی مشغول نیست چک می‌کنیم که شبکهٔ ذخیره‌شده برگشته یا نه.
// اگر برگشته باشد، پنل بسته می‌شود و دستگاه به STA سوییچ می‌کند تا وصل شود/پخش کند.
void portalProbe() {
  if (cfgSsid.isEmpty()) return;

  Serial.println("[Portal] probe: checking if saved AP is back...");
  int n = scanWifiAnimated();
  bool found = false;
  for (int i = 0; i < n; i++) {
    if (WiFi.SSID(i) == cfgSsid) { found = true; break; }
  }
  if (n > 0) WiFi.scanDelete();

  if (found) {
    Serial.println("[Portal] saved AP is back -> switch to STA");
    stopPortal();
    startSta();
  } else if (probeInterval < PROBE_MAX_MS) {
    probeInterval *= 2;
    Serial.printf("[Portal] AP not back, next probe in %lu s\n", probeInterval / 1000);
  }
}

// ==================== حلقه‌ها ====================
void loopSta() {
  keepTx();
  if (!wifiConnected && WiFi.status() == WL_CONNECTED) wifiConnected = true;

  if (wifiConnected) {
    mode = M_PLAY;
    lostAtMs = 0;
    Serial.println("[Mode] connected -> PLAY");
    return;
  }

  if (millis() - lastWifiAttempt >= WIFI_RETRY_INTERVAL) {
    lastWifiAttempt = millis();
    wifiFailCount++;
    Serial.printf("[WiFi] retry #%u...\n", wifiFailCount);
    if ((wifiFailCount % 3 == 0) || !haveTarget) findTargetAP();
    connectWifi(false);
  }

  if (wifiFailCount >= STA_MAX_TRIES) {
    failedStreak++;
    Serial.println("[WiFi] cannot connect -> setup portal");
    startPortal();
  }
}

void loopPlay() {
  keepTx();
  if (!wifiConnected && WiFi.status() == WL_CONNECTED) wifiConnected = true;

  if (!wifiConnected) {
    if (lostAtMs == 0) lostAtMs = millis();

    if (millis() - lastWifiAttempt >= WIFI_RETRY_INTERVAL) {
      lastWifiAttempt = millis();
      wifiFailCount++;
      if ((wifiFailCount % 3 == 0) || !haveTarget) findTargetAP();
      connectWifi(false);
    }

    if (!everPlayedOk && (millis() - lostAtMs >= OFFLINE_TO_PORTAL)) {
      failedStreak++;
      Serial.println("[WiFi] offline too long -> setup portal");
      startPortal();
    }
    return;
  }

  if (lostAtMs) {
    Serial.println("[WiFi] reconnected");
    lostAtMs = 0;
  }
}

void loopPortal() {
  keepTx();
  server.handleClient();

  if (restartAt && millis() >= restartAt) {
    Serial.println("[Portal] restarting...");
    delay(100);
    ESP.restart();
  }

  if (!cfgSsid.isEmpty() &&
      (millis() - lastHttpActivity >= PROBE_IDLE_MS) &&
      (millis() - lastProbe >= probeInterval)) {
    lastProbe = millis();
    portalProbe();
  }
}

// ==================== LED ها ====================
void updateLeds() {
  unsigned long now = millis();

  // LED وضعیت WiFi (پین ۸)
  bool wifiOn;
  if (mode == M_PORTAL) {
    // حالت سرور: دوتا چشمک سریع + مکث
    unsigned long p = now % 1000;
    wifiOn = (p < 100) || (p >= 200 && p < 300);
  } else if (wifiConnected) {
    wifiOn = true;                      // متصل: روشن ثابت
  } else {
    wifiOn = (now % 1000) < 500;        // سرچ/اتصال: چشمک معمولی
  }
  digitalWrite(LED_WIFI_PIN, wifiOn ? LED_WIFI_ON : LED_WIFI_OFF);

  // LED پخش صدا (پین ۹): در حال پخش ثابت، در حالت Pause چشمک‌زن
  bool playOn = false;
  if (playing) {
    if (playbackPaused) {
      playOn = (now % (PAUSE_LED_BLINK * 2)) < PAUSE_LED_BLINK;
    } else {
      playOn = true;
    }
  }
  digitalWrite(LED_PLAY_PIN, playOn ? LED_PLAY_ON : LED_PLAY_OFF);

  // LED پیام معوق (پین ۱۰): تا خوانده شدن همه چشمک می‌زند؛ حین پخش سری خاموش است
  bool msgOn;
  if (!sessionMode && pendingCount > 0) {
    msgOn = (now % (ALERT_LED_BLINK * 2)) < ALERT_LED_BLINK;
  } else {
    msgOn = false;
  }
  digitalWrite(LED_MSG_PIN, msgOn ? LED_MSG_ON : LED_MSG_OFF);
}

// ==================== دکمه‌ها: قبلی (GPIO0) / پخش (GPIO1) / بعدی (GPIO2) ====================
// سطح فعال با BTN_ACTIVE_HIGH در smartgadjet_config.h تعیین می‌شود (1 = فشردن با 3.3V).
// فشردن‌ها در شمارنده‌های جداگانه ثبت و توسط تسک پخش مصرف می‌شوند. دیبانس ۵۰ms.
void pollButton() {
  for (uint8_t i = 0; i < 3; i++) {
#if BTN_ACTIVE_HIGH
    bool raw = (digitalRead(BTN_PINS[i]) == HIGH);
#else
    bool raw = (digitalRead(BTN_PINS[i]) == LOW);
#endif
    unsigned long now = millis();

    if (raw != btnLastRaw[i]) {
      btnLastRaw[i]  = raw;
      btnChangeAt[i] = now;
    }

    if (now - btnChangeAt[i] >= BTN_DEBOUNCE_MS && raw != btnStable[i]) {
      btnStable[i] = raw;
      if (btnStable[i]) {
        (*BTN_EVENTS[i])++;
        Serial.printf("[Btn] pressed (gpio %u)\n", BTN_PINS[i]);
      }
    }
  }
}

// ==================== Setup ====================
void setup() {
  Serial.begin(115200);
  delay(300);
  Serial.println();
  Serial.println("=== ESP32-C3 Super Mini Audio Player ===");

  audioLogger = &Serial;

  loadCfg();
  loadDeviceIdentity();

  // LittleFS: کش آلارم (اولین بوت فرمت می‌شود)
  if (!LittleFS.begin(true)) {
    Serial.println("[FS] LittleFS mount FAILED");
  } else {
    Serial.printf("[FS] LittleFS ready: %u/%u bytes used\n",
                  LittleFS.usedBytes(), LittleFS.totalBytes());
  }
  loadAlertMeta();

  pinMode(BOOT_BTN_PIN, INPUT_PULLUP);
#if BTN_ACTIVE_HIGH
  pinMode(BTN_PREV_PIN, INPUT_PULLDOWN);   // فشردن = 3.3V
  pinMode(BTN_PLAY_PIN, INPUT_PULLDOWN);
  pinMode(BTN_NEXT_PIN, INPUT_PULLDOWN);
#else
  pinMode(BTN_PREV_PIN, INPUT_PULLUP);     // فشردن = GND
  pinMode(BTN_PLAY_PIN, INPUT_PULLUP);
  pinMode(BTN_NEXT_PIN, INPUT_PULLUP);
#endif
  delay(50);
  bool forcePortal = (digitalRead(BOOT_BTN_PIN) == LOW);

  // LED ها — پین ۹ همان دکمهٔ BOOT است؛ اول دکمه خوانده شد، حالا خروجی LED می‌شود
  pinMode(LED_WIFI_PIN, OUTPUT);
  digitalWrite(LED_WIFI_PIN, LED_WIFI_OFF);
  pinMode(LED_PLAY_PIN, OUTPUT);
  digitalWrite(LED_PLAY_PIN, LED_PLAY_OFF);
  pinMode(LED_MSG_PIN, OUTPUT);
  digitalWrite(LED_MSG_PIN, LED_MSG_OFF);

  WiFi.persistent(false);
  WiFi.onEvent(onWifiEvent);

  server.on("/", HTTP_GET, handleRoot);
  server.on("/save", HTTP_POST, handleSave);
  server.on("/scan", HTTP_GET, handleScan);
  server.on("/clear", HTTP_GET, handleClear);
  server.onNotFound(handleNotFound);

  xTaskCreate(playerLoopTask, "player", PLAYER_TASK_STACK, nullptr, 1, &playerTaskHandle);
  if (!playerTaskHandle) Serial.println("[Boot] player task create FAILED");

  if (cfgSsid.isEmpty()) {
    Serial.println("[Boot] no saved WiFi -> setup portal");
    startPortal();
  } else if (forcePortal) {
    Serial.println("[Boot] BOOT button held -> setup portal");
    startPortal();
  } else {
    startSta();
  }
}

// ==================== پایش سلامت ====================
// هر دقیقه heap/RSSI را لاگ می‌کند و اگر در حالت بی‌کار حافظه کم/تکه‌تکه شد
// یا ۱۲ ساعت از روشن‌شدن گذشت، خودش ری‌استارت می‌کند (مثل ریست دستی).
void maintenance() {
  if (millis() - lastDiag < DIAG_INTERVAL_MS) return;
  lastDiag = millis();

  uint32_t freeHeap = ESP.getFreeHeap();
  uint32_t maxAlloc = ESP.getMaxAllocHeap();
  Serial.printf("[Diag] free=%u max=%u rssi=%d pending=%u uptime=%lus\n",
                (unsigned)freeHeap, (unsigned)maxAlloc,
                wifiConnected ? WiFi.RSSI() : 0, (unsigned)pendingCount,
                millis() / 1000UL);

  if (mode != M_PLAY || playing || sessionMode) return;

  bool lowHeap = (freeHeap < 30000) || (maxAlloc < 20000);
  bool longUp  = (millis() >= AUTO_RESTART_MS);
  if (lowHeap || longUp) {
    Serial.printf("[Diag] auto-restart (%s)\n", lowHeap ? "low heap" : "uptime");
    delay(100);
    ESP.restart();
  }
}

// ==================== Main Loop ====================
void loop() {
  pollButton();
  updateLeds();
  maintenance();

  switch (mode) {
    case M_STA:    loopSta();    break;
    case M_PLAY:   loopPlay();   break;
    case M_PORTAL: loopPortal(); break;
  }
}
