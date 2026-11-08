#include "AudioFileSourceHTTPRange.h"

// اگر هیچ داده‌ای نیامد، بعد از این مدت اتصال را با Range دوباره باز می‌کنیم.
// اتصال را عمداً وسط فایل نمی‌بندیم؛ چرخش زودهنگام باعث قطع پخش فایل‌های طولانی می‌شد.
static const unsigned long READ_WAIT_MS      = 5000;
static const uint32_t      READ_CHUNK_TARGET  = 4096; // small first chunk = faster audio start
static const int           RECONNECT_TRIES   = 6;
static const unsigned long RECONNECT_DELAY_MS = 300;

AudioFileSourceHTTPRange::AudioFileSourceHTTPRange() {
    url.reserve(160);
}

AudioFileSourceHTTPRange::AudioFileSourceHTTPRange(const char *u) {
    url.reserve(160);
    open(u);
}

AudioFileSourceHTTPRange::~AudioFileSourceHTTPRange() {
    endRequest();
}

bool AudioFileSourceHTTPRange::open(const char *u) {
    endRequest();
    url = u ? u : "";
    pos = 0;
    size = 0;
    failed = false;

    if (url.isEmpty() || !startRequest(0)) {
        endRequest();
        failed = true;
        return false;
    }
    return true;
}

void AudioFileSourceHTTPRange::endRequest() {
    http.end();
    client.stop();
}

bool AudioFileSourceHTTPRange::startRequest(uint32_t offset) {
    endRequest();

    if (!http.begin(client, url)) {
        Serial.println("[HTTPRange] http.begin failed");
        endRequest();
        return false;
    }
    http.setReuse(false);
    http.setTimeout(15000);
    static const char *rangeHeaders[] = {"Content-Range"};
    http.collectHeaders(rangeHeaders, 1);
    if (offset > 0) {
        http.addHeader("Range", "bytes=" + String(offset) + "-");
    }

    int code = http.GET();
    if (code != 200 && code != 206) {
        Serial.printf("[HTTPRange] GET %d at byte %u\n", code, (unsigned)offset);
        endRequest();
        return false;
    }

    int len = http.getSize();
    uint32_t total = 0;

    if (code == 206) {
        String cr = http.header("Content-Range");
        int slash = cr.lastIndexOf('/');
        if (slash >= 0) {
            total = (uint32_t)strtoul(cr.substring(slash + 1).c_str(), nullptr, 10);
        }
        if (total == 0 && len > 0) total = offset + (uint32_t)len;
        // 206 با بدنهٔ صفر یعنی به انتهای فایل رسیده‌ایم.
        if (len == 0) total = offset;
    } else if (len > 0) {
        total = (uint32_t)len;
    }

    // اگر سرور Range را نادیده گرفت (200)، بایت‌های قبلی را رد می‌کنیم.
    if (code == 200 && offset > 0) {
        if (!skipBytes(offset)) {
            endRequest();
            return false;
        }
    }

    pos = offset;
    size = total;
    return true;
}

bool AudioFileSourceHTTPRange::skipBytes(uint32_t count) {
    auto *stream = http.getStreamPtr();
    if (!stream) return false;

    unsigned long lastData = millis();
    uint8_t tmp[256];
    while (count > 0) {
        if (!stream->connected() && stream->available() == 0) return false;
        int avail = stream->available();
        if (avail > 0) {
            uint32_t want = count < sizeof(tmp) ? count : sizeof(tmp);
            int n = stream->read(tmp, (int)want);
            if (n > 0) {
                count -= (uint32_t)n;
                lastData = millis();
                continue;
            }
        }
        if (millis() - lastData >= READ_WAIT_MS) return false;
        delay(1);
    }
    return true;
}

bool AudioFileSourceHTTPRange::reconnect() {
    if (size > 0 && pos >= size) return false;

    for (int i = 0; i < RECONNECT_TRIES; i++) {
        Serial.printf("[HTTPRange] reconnect %d at byte %u\n", i + 1, (unsigned)pos);
        if (startRequest(pos)) return true;
        delay(RECONNECT_DELAY_MS);
    }

    failed = true;
    Serial.println("[HTTPRange] reconnect failed");
    return false;
}

uint32_t AudioFileSourceHTTPRange::read(void *data, uint32_t len) {
    if (data == nullptr) return 0;
    return readInternal(data, len, false);
}

uint32_t AudioFileSourceHTTPRange::readNonBlock(void *data, uint32_t len) {
    if (data == nullptr) return 0;
    return readInternal(data, len, true);
}

uint32_t AudioFileSourceHTTPRange::readInternal(void *data, uint32_t len, bool nonBlock) {
    if (!data || len == 0 || failed) return 0;
    if (size > 0) {
        uint32_t remaining = size - pos;
        if (len > remaining) len = remaining;
        if (len == 0) return 0;
    }

    // readNonBlock برای پرکردن بافر است و نباید هیچ‌وقت معطل/دوباره‌وصل شود.
    if (nonBlock) {
        auto *stream = http.getStreamPtr();
        if (!stream) return 0;
        int avail = stream->available();
        if (avail <= 0) return 0;
        if ((uint32_t)avail < len) len = (uint32_t)avail;
        int n = stream->read(reinterpret_cast<uint8_t *>(data), len);
        if (n > 0) pos += (uint32_t)n;
        return n > 0 ? (uint32_t)n : 0;
    }

    for (int attempt = 0; attempt < RECONNECT_TRIES; attempt++) {
        if (size > 0 && pos >= size) return 0;

        auto *stream = http.getStreamPtr();
        if (!stream) return 0;

        // مهم: ممکن است فایل کوچک باشد و سرور قبل از خواندن، اتصال را ببندد؛
        // تا وقتی بایت آماده در سوکت هست، نباید دوباره‌وصل شویم و آن را دور بریزیم.
        if (!http.connected() && stream->available() <= 0) {
            // طول نامشخص (chunked) و اتصال بسته شده = پایان طبیعی فایل
            if (size == 0 && pos > 0) return 0;
            if (!reconnect()) return 0;
            stream = http.getStreamPtr();
            if (!stream) return 0;
        }

        // نصف بافر (۱۶KB) را هدف می‌گیریم؛ هم شروع پخش معطل نشود،
        // هم هنگام قطعی کوتاه شبکه، صدای کافی در بافر باشد.
        uint32_t target = len < READ_CHUNK_TARGET ? len : READ_CHUNK_TARGET;
        unsigned long waitStart = millis();
        while ((uint32_t)stream->available() < target) {
            if (size > 0 && (pos + (uint32_t)stream->available()) >= size) break;
            if (!http.connected()) break;
            if (millis() - waitStart >= READ_WAIT_MS) break;
            delay(1);
        }

        int avail = stream->available();
        if (avail > 0) {
            if ((uint32_t)avail < len) len = (uint32_t)avail;
            int n = stream->read(reinterpret_cast<uint8_t *>(data), len);
            if (n > 0) {
                pos += (uint32_t)n;
                return (uint32_t)n;
            }
        }

        // اگر در این مدت داده‌ای نیامد، همان‌جا با Range از pos ادامه می‌دهیم.
        endRequest();
        if (!reconnect()) return 0;
    }

    return 0;
}

bool AudioFileSourceHTTPRange::seek(int32_t, int) {
    return false;
}

bool AudioFileSourceHTTPRange::close() {
    endRequest();
    pos = 0;
    size = 0;
    failed = false;
    url = "";
    return true;
}

bool AudioFileSourceHTTPRange::isOpen() {
    if (failed) return false;
    if (size > 0) return pos < size;
    return http.connected();
}

uint32_t AudioFileSourceHTTPRange::getSize() {
    return size;
}

uint32_t AudioFileSourceHTTPRange::getPos() {
    return pos;
}
