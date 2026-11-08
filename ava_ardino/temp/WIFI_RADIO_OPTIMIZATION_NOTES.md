# ESP32-C3 Super Mini — WiFi Instability: Known Issues, Root Causes, and Fixes

**Scope of this note: the WiFi radio only.** Nothing here depends on what the rest of your
application does. You can drop these changes into any firmware for this board.

**Board:** ESP32-C3 Super Mini (ESP32-C3, single-core RISC-V, 2.4 GHz WiFi, on-board PCB antenna)
**Verified on:** Arduino-ESP32 core `esp32:esp32` **3.3.11** (ESP-IDF 5.x), FQBN `esp32:esp32:esp32c3`
**Topic:** the radio of this specific clone board connects unreliably, drops links, and can need
a manual reset to connect again. Below: the symptoms, why it happens, every fix applied, and how
to verify. Sources are the ESP-IDF WiFi reason codes, Espressif documentation for
`esp_wifi_set_max_tx_power`, and the well-known community reports about this board.

---

## 1. Symptoms (what you will see)

With **correct credentials** and the access point **2 m away (RSSI −21 … −31 dBm)**:

- Connects only *sometimes*. Often needs a reset / power cycle before it will connect.
- After a normal software reboot it frequently fails to connect, then may heal itself later.
- Serial log shows a repeating pattern like:

```
E (20241) wifi:sta is connecting, return error
[WiFi] Disconnected, reason=2        <- WIFI_REASON_AUTH_EXPIRE
[WiFi] Disconnected, reason=201      <- WIFI_REASON_NO_AP_FOUND
[WiFi] Disconnected, reason=36
```

- No crash, no exception, no watchdog reset — the failure is **silent, at the RF/analog level**.
- The board gets noticeably warm under WiFi activity.

Reason codes seen and what they mean here:

| reason | name | meaning in this context |
|---|---|---|
| 2 | `WIFI_REASON_AUTH_EXPIRE` | the 4-way handshake never completed in time (RF timing / power dip) |
| 201 | `WIFI_REASON_NO_AP_FOUND` | the AP was not seen during the attempt (marginal RF front-end) |
| 36 | — | generic disconnect during (re)association |

> **Important detail about `reason=2`:** ESP-IDF also reports `reason=2` when the firmware
> itself calls `esp_wifi_disconnect()`. So in your own firmware, log an explicit line before
> every intentional disconnect; a bare `reason=2` with no intent log = a real RF failure.

**Key observation:** this is **not** a signal-strength problem. RSSI is excellent (−21…−31 dBm),
yet association fails. That is why the fixes below are about **power, current spikes, and
driver state**, not about distance or antennas alone.

---

## 2. Root causes (why this board specifically)

These are the widely reported weaknesses of this cheap board, confirmed by our measurements
and logs:

### 2.1 Weak 3.3 V regulator + WiFi TX current spikes (the main cause)
WiFi transmission draws **≈ 350–500 mA peaks** lasting sub-milliseconds. The on-board LDO
(vendor varies by batch: SGM2212 / RT9080 / unmarked clones) has poor transient response.
The 3.3 V rail **sags** during each TX burst, the RF front-end misbehaves, and the
association/handshake fails — **without** a full brownout reset, so the firmware keeps running
while "WiFi is broken". A power cycle resets the analog state, which is why a manual reset
"fixes" it.

### 2.2 Default TX power is 19.5 dBm — the worst case for this board
The ESP32-C3 defaults to maximum TX power. More TX power = bigger current spikes = deeper rail
sag. On a well-designed board this is fine; on this board it is the biggest single trigger.

### 2.3 Marginal antenna
The tiny PCB/ceramic antenna and the 0Ω selector resistor (optionally routed to a U.FL/IPEX
connector) are marginal on many batches — sometimes the resistor/route is wrong. Scan results
vary run to run, and the AP is sometimes "not found" even when it is a few meters away.

### 2.4 Stale / broken driver state in NVS
After enough failed handshakes, the stored WiFi config in NVS can leave the driver stuck in
"connecting" (`sta is connecting, return error`) until the stored config is erased or the chip
is fully power-cycled.

### 2.5 Heat and power source quality
A warm board indicates a stressed regulator. Thin/long USB cables or weak 5 V supplies make the
rail sag worse and are, by themselves, a known cause of "works on my desk, fails on a power bank"
behavior.

---

## 3. Software fixes — apply in this order

All measurements below assume core 3.x (`WiFi.setTxPower()` accepts `wifi_power_t`).

### Step 1 — Cap TX power to 8.5 dBm
The driver **resets TX power on every connect / mode change**, so it must be re-applied
**immediately after every `WiFi.begin()`** and re-asserted periodically.

```cpp
#include <WiFi.h>
#include "esp_wifi.h"

#define WIFI_TX_POWER WIFI_POWER_8_5dBm   // 34 (value = dBm * 4)

inline void applyTxPower() {
  WiFi.setTxPower((wifi_power_t)WIFI_TX_POWER);
}

// keep the loop cheap: re-assert every 5 s (idempotent)
static unsigned long t = 0;
if (millis() - t >= 5000) { t = millis(); applyTxPower(); }
```

Power enum (Arduino-ESP32): `WIFI_POWER_19_5dBm`(78) … `WIFI_POWER_8_5dBm`(34),
`WIFI_POWER_5dBm`(20), `WIFI_POWER_2dBm`(8), `WIFI_POWER_MINUS_1dBm`(-4).
Start at 8.5 dBm; if still unstable go to 5 dBm, then 2 dBm. Home-range coverage is unaffected
when the AP is within a room.

> If your firmware never calls `applyTxPower()` after `WiFi.begin()`, you can confirm the
> problem by printing `WiFi.getTxPower()` — it will read `78` (19.5 dBm) instead of `34`.

### Step 2 — Scan once, then connect directly to the BSSID + channel
Avoid the implicit scan inside `WiFi.begin()`: do one explicit scan, pick the strongest BSSID of
your SSID, then connect straight to it. Fewer current spikes, less airtime, deterministic AP
selection.

```cpp
WiFi.begin(ssid, pass, targetChannel, targetBssid);
```

Re-scan only every few failed retries (the AP may have changed channel or rebooted).

### Step 3 — Guarantee a clean driver state before every attempt

```cpp
WiFi.disconnect(true, /*eraseStored=*/false);   // disconnect + radio off
delay(100);
WiFi.mode(WIFI_STA);                            // drop any AP/captive-portal state
WiFi.setSleep(false);                           // remove modem-sleep latency
WiFi.begin(...);
applyTxPower();                                 // AFTER begin()
```

One-time hard clear if the driver looks stuck (`sta is connecting, return error`):

```cpp
WiFi.disconnect(true, /*eraseStored=*/true);    // erase the driver's stored config in NVS
```

### Step 4 — Stop the driver from persisting credentials to NVS

```cpp
WiFi.persistent(false);
```

Store credentials yourself (e.g. with `Preferences`). This avoids NVS wear and prevents the
driver from re-using a possibly-stale config after a failed connection streak.

### Step 5 — Bounded retries, then a deterministic fallback (never retry forever)

- Retry every 8 s, re-scan every 3rd retry.
- After ~4 attempts (≈32 s) stop and take a different action (e.g. open a configuration AP,
  signal an error LED, deep-sleep and retry later). Endless retry loops keep the regulator
  stressed and hide the failure instead of surfacing it.

### Step 6 — A radio policy: AP and busy-STA must never overlap

On a single-radio, power-constrained board, running **AP + STA at the same time** (especially
under traffic) is the fastest way to reproduce the instability. Recommended policy:

| Mode | Radio usage | Extra services (HTTP server, etc.) |
|---|---|---|
| Trying to connect | **STA only** | off |
| Connected / normal operation | **STA only** | off |
| Configuration portal (only when needed) | **AP only** (STA enabled just to scan, never `WiFi.begin()`) | on, and only here |

Rules:

- The configuration AP is a *tool*, not a permanent state: raise it only when it is needed
  (no credentials saved, connect failed for ~32 s, forced by a button, or offline too long).
- Leave it automatically: while the portal is idle, scan periodically (start at 20 s,
  back-off ×2 up to 120 s); when the saved SSID appears, tear the AP down and switch to STA.
- Keep the TX power capped in **all** modes, including AP mode.

### Step 7 — Use WiFi events instead of polling

```cpp
void onWifiEvent(WiFiEvent_t event, WiFiEventInfo_t info) {
  if (event == ARDUINO_EVENT_WIFI_STA_GOT_IP) {
    /* connected; reset counters; re-apply Tx power */
  } else if (event == ARDUINO_EVENT_WIFI_STA_DISCONNECTED) {
    Serial.printf("[WiFi] reason=%d\n", info.wifi_sta_disconnected.reason);
  }
}
WiFi.onEvent(onWifiEvent);
```

Plus a belt-and-braces check in the loop:
`if (!connected && WiFi.status() == WL_CONNECTED) connected = true;`

---

## 4. Hardware fixes (do these — software only mitigates)

Ordered by impact per cost:

1. **Bulk capacitance (biggest win).** Solder **100–470 µF low-ESR electrolytic + 100 nF
   ceramic** across `3V3` and `GND` as close to the module as possible; optionally add
   **≥ 470 µF on the 5 V input** (before the LDO). This absorbs the TX current spikes.
2. **Power source.** Short, thick USB cable; a direct PC port or a quality 5 V / ≥ 1 A supply.
   Avoid weak hubs, long thin cables, and poorly regulated chargers.
3. **Antenna.** If a U.FL/IPEX connector exists, move the 0Ω selector resistor from the PCB
   antenna to the IPEX side and attach a real 2.4 GHz antenna. Otherwise solder a **~31 mm
   straight wire** (quarter wavelength at 2.45 GHz) to the antenna pad. Keep the antenna clear
   of metal, hands, and breadboards.
4. **Remove heat sources / additional loads on 3V3.** Anything else powered from the board's
   3.3 V rail (especially devices with their own current spikes) compounds the problem —
   power them separately and/or add their own bulk caps.
5. **Last resort: replace the board.** This is a known quality issue with this clone. Reliable
   alternatives with much better RF/power design: `ESP32-C3-DevKitM-1`, `Seeed XIAO ESP32-C3`,
   or any ESP32-WROOM-32 board.

---

## 5. Verification — how to know it is solved

Expected serial output on a good boot (115200 baud):

```
[WiFi] Scanning...
[WiFi] Target found ch=6 rssi=-21
[WiFi] Connected. IP: 192.168.1.50  ch=6 rssi=-25 tx=34
```

- `tx=` must be `34` (8.5 dBm). If it prints `78`, `applyTxPower()` is missing after `begin()`.
- If connection fails within ~32 s, your fallback path (portal/LED/retry policy) must trigger
  exactly as designed — the firmware must never sit in an infinite silent retry loop.
- **Stress test:** run 20–30 consecutive power cycles. With the TX cap + BSSID connect in
  software, the failure rate should visibly drop; with the bulk capacitor(s) added, a healthy
  unit should connect every time. An oscilloscope/scope probe on 3V3 during association will
  show the sag shrinking with each mitigation.

---

## 6. Copy-paste reference implementation (WiFi only)

```cpp
#include <WiFi.h>
#include "esp_wifi.h"

#define WIFI_TX_POWER              WIFI_POWER_8_5dBm
const unsigned long WIFI_RETRY_INTERVAL = 8000;   // ms between attempts
const uint8_t       STA_MAX_TRIES       = 4;      // ~32 s, then fallback action

uint8_t  targetChannel  = 0;
uint8_t  targetBssid[6] = {0};
bool     haveTarget     = false;
uint32_t wifiFailCount  = 0;
volatile bool wifiConnected = false;

inline void applyTxPower() { WiFi.setTxPower((wifi_power_t)WIFI_TX_POWER); }

void onWifiEvent(WiFiEvent_t event, WiFiEventInfo_t info) {
  if (event == ARDUINO_EVENT_WIFI_STA_GOT_IP) {
    wifiConnected = true;
    wifiFailCount = 0;
    applyTxPower();
  } else if (event == ARDUINO_EVENT_WIFI_STA_DISCONNECTED) {
    wifiConnected = false;
    Serial.printf("[WiFi] reason=%d\n", info.wifi_sta_disconnected.reason);
  }
}

void findTargetAP(const char* ssid) {
  int n = WiFi.scanNetworks(false, true);
  int best = -1, bestRssi = -1000;
  for (int i = 0; i < n; i++) {
    if (WiFi.SSID(i) == ssid && WiFi.RSSI(i) > bestRssi) { best = i; bestRssi = WiFi.RSSI(i); }
  }
  if (best >= 0) {
    targetChannel = (uint8_t)WiFi.channel(best);
    memcpy(targetBssid, WiFi.BSSID(best), 6);
    haveTarget = true;
  } else {
    haveTarget = false;
  }
  if (n > 0) WiFi.scanDelete();
}

void connectWifi(const char* ssid, const char* pass, bool hardReset) {
  WiFi.disconnect(true, hardReset);      // hardReset=true also erases stored NVS config
  delay(100);
  WiFi.mode(WIFI_STA);
  WiFi.setSleep(false);
  if (haveTarget && !hardReset) WiFi.begin(ssid, pass, targetChannel, targetBssid);
  else                          WiFi.begin(ssid, pass);
  applyTxPower();                        // MUST be after begin()
}

void wifiLoopOnce() {
  static unsigned long lastAttempt = 0;
  static unsigned long lastTx      = 0;

  if (millis() - lastTx >= 5000) { lastTx = millis(); applyTxPower(); }

  if (wifiConnected) return;

  if (millis() - lastAttempt >= WIFI_RETRY_INTERVAL) {
    lastAttempt = millis();
    wifiFailCount++;
    if ((wifiFailCount % 3 == 0) || !haveTarget) findTargetAP("YOUR_SSID");
    connectWifi("YOUR_SSID", "YOUR_PASS", false);
  }

  if (wifiFailCount >= STA_MAX_TRIES) {
    wifiFailCount = 0;
    /* fallback: open config portal / toggle LED / sleep+retry — see policy in section 3.6 */
  }
}
```

Initialization order (matters):

```cpp
void setup() {
  Serial.begin(115200);
  WiFi.persistent(false);
  WiFi.onEvent(onWifiEvent);

  WiFi.mode(WIFI_STA);
  WiFi.setSleep(false);
  WiFi.disconnect(true, false);

  findTargetAP("YOUR_SSID");
  connectWifi("YOUR_SSID", "YOUR_PASS", true /* first boot: clear stale NVS once */);
}
```

---

## 7. Final checklist

- [ ] `WiFi.setTxPower(WIFI_POWER_8_5dBm)` **after every `WiFi.begin()`** and every ~5 s after
- [ ] `WiFi.persistent(false)` + credentials stored by the app
- [ ] Scan once, connect with `BSSID + channel`
- [ ] `WiFi.setSleep(false)` for latency; `WiFi.disconnect(true,false)` + `WiFi.mode(WIFI_STA)` before every attempt
- [ ] Bounded retries with re-scan every 3rd try, then a deterministic fallback (~32 s)
- [ ] AP and busy STA never overlap; configuration AP raised only when needed, TX capped in AP mode too
- [ ] 100–470 µF + 100 nF across 3V3/GND (and ≥470 µF on 5 V if possible)
- [ ] Good cable + good 5 V supply
- [ ] Antenna fixed/improved if the board is one of the bad batches
- [ ] Verified `tx=34` in the log and 20–30 clean consecutive boots
