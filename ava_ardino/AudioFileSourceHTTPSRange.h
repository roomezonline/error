#pragma once

#include <Arduino.h>
#include <HTTPClient.h>
#include <WiFiClientSecure.h>
#include "AudioFileSource.h"

// منبع HTTPS مقاوم برای پخش مستقیم فایل صوتی از سرور:
// - اتصال را تا پایان فایل باز نگه می‌دارد
// - فقط در صورت قطع واقعی، با هدر Range از همان بایت ادامه می‌دهد
// - برای فایل‌های کوچک (آوای چند ثانیه‌ای) هم بدون مشکل کار می‌کند
class AudioFileSourceHTTPSRange : public AudioFileSource {
public:
    AudioFileSourceHTTPSRange();
    explicit AudioFileSourceHTTPSRange(const char *url);
    ~AudioFileSourceHTTPSRange() override;

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

    WiFiClientSecure client;
    HTTPClient http;
    String url;
    uint32_t pos = 0;
    uint32_t size = 0;
    bool failed = false;
};
