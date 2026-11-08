#pragma once

#include <Arduino.h>
#include <HTTPClient.h>
#include <WiFiClient.h>
#include "AudioFileSource.h"

// منبع HTTP مقاوم برای فایل‌های بزرگ:
// - اتصال را تا پایان فایل باز نگه می‌دارد تا پخش فایل‌های طولانی وسط کار قطع نشود
// - فقط در صورت قطع واقعی اتصال، با هدر Range از همان بایت ادامه می‌دهد
// - انتظار کوتاه ۵۰۰ms کتابخانه را به ۵ ثانیه افزایش می‌دهد
class AudioFileSourceHTTPRange : public AudioFileSource {
public:
    AudioFileSourceHTTPRange();
    explicit AudioFileSourceHTTPRange(const char *url);
    ~AudioFileSourceHTTPRange() override;

    bool open(const char *url) override;
    uint32_t read(void *data, uint32_t len) override;
    uint32_t readNonBlock(void *data, uint32_t len) override;
    bool seek(int32_t pos, int dir) override;
    bool close() override;
    bool isOpen() override;
    uint32_t getSize() override;
    uint32_t getPos() override;

private:
    uint32_t readInternal(void *data, uint32_t len, bool nonBlock);
    bool startRequest(uint32_t offset);
    bool reconnect();
    bool skipBytes(uint32_t count);
    void endRequest();

    HTTPClient http;
    WiFiClient client;
    String url;
    uint32_t pos = 0;
    uint32_t size = 0;
    bool failed = false;
};
