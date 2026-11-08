// ضبط زنده صدای اکشن‌های «آوای ارورسرویس» + پخش تست قبل از ذخیره + آپلود MP3
// ---------------------------------------------------------------------------
// صدای ضبط‌شده در مرورگر با lamejs به MP3 (مونو، ۲۲.۰۵ کیلوهرتز، ۶۴ کیلوبیت)
// تبدیل می‌شود تا برد ESP32-C3 بتواند آن را بدون تبدیل اضافه و با کیفیت
// مناسب استریم و پخش کند. فقط فایل MP3 به سرور ارسال می‌شود.
window.serviceVoiceRecorder = {
    mediaRecorder: null,
    chunks: [],
    stream: null,
    pendingBlob: null,       // فایل اصلی ضبط‌شده (برای پیش‌نمایش در مرورگر)
    pendingUrl: null,
    uploadBlob: null,        // فایل MP3 آمادهٔ آپلود
    convertPromise: null,
    encodeError: '',
    recordingSeq: 0,
    targetSampleRate: 22050,
    targetBitrateKbps: 64,
    startTime: 0,
    tickTimer: null,
    dotNetRef: null,
    mimeType: '',
    fileExt: 'webm',

    _hasEncoder: function () {
        return !!(window.lamejs && window.lamejs.Mp3Encoder);
    },

    isSupported: function () {
        return !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia &&
                  window.MediaRecorder && this._hasEncoder());
    },

    _bestMimeType: function () {
        var types = [
            'audio/webm;codecs=opus',
            'audio/webm',
            'audio/mp4;codecs=mp4a.40.2',
            'audio/mp4',
            'audio/aac',
            'audio/ogg;codecs=opus'
        ];
        for (var i = 0; i < types.length; i++) {
            if (MediaRecorder.isTypeSupported(types[i])) return types[i];
        }
        return '';
    },

    _tick: function () {
        var self = this;
        if (self.tickTimer) return;
        self.tickTimer = setInterval(function () {
            var sec = Math.max(1, Math.round((Date.now() - self.startTime) / 1000));
            if (self.dotNetRef) {
                self.dotNetRef.invokeMethodAsync('OnRecordingTick', sec);
            }
        }, 500);
    },

    _stopTick: function () {
        if (this.tickTimer) {
            clearInterval(this.tickTimer);
            this.tickTimer = null;
        }
    },

    _stopStream: function () {
        if (this.stream) {
            this.stream.getTracks().forEach(function (t) { t.stop(); });
            this.stream = null;
        }
    },

    // ── تبدیل به MP3 مونو ۲۲.۰۵ کیلوهرتز ───────────────────────────────────
    // میکس کانال‌ها + نمونه‌برداری مجدد خطی + تبدیل به Int16
    _encodeMp3: function (audioBuffer) {
        if (!this._hasEncoder()) {
            throw new Error('encoder_unavailable');
        }

        var targetRate = this.targetSampleRate;
        var srcRate = audioBuffer.sampleRate;
        var channels = audioBuffer.numberOfChannels;
        var inLen = audioBuffer.length;
        var outLen = Math.max(1, Math.round(inLen * targetRate / srcRate));
        var chData = [];
        for (var c = 0; c < channels; c++) chData.push(audioBuffer.getChannelData(c));

        var mono = new Float32Array(outLen);
        var step = srcRate / targetRate;

        for (var i = 0; i < outLen; i++) {
            var pos = i * step;
            var i0 = Math.floor(pos);
            if (i0 >= inLen) i0 = inLen - 1;
            var i1 = (i0 + 1 < inLen) ? i0 + 1 : i0;
            var frac = pos - i0;
            var sum = 0;
            for (var ch = 0; ch < channels; ch++) {
                var d = chData[ch];
                sum += d[i0] * (1 - frac) + d[i1] * frac;
            }
            mono[i] = sum / channels;
        }

        var pcm = new Int16Array(outLen);
        for (var j = 0; j < outLen; j++) {
            var s = mono[j];
            if (s > 1) s = 1; else if (s < -1) s = -1;
            pcm[j] = s < 0 ? s * 0x8000 : s * 0x7fff;
        }

        var encoder = new lamejs.Mp3Encoder(1, targetRate, this.targetBitrateKbps);
        var blockSize = 1152;
        var parts = [];

        for (var offset = 0; offset < pcm.length; offset += blockSize) {
            var block = pcm.subarray(offset, Math.min(offset + blockSize, pcm.length));
            var buf = encoder.encodeBuffer(block);
            if (buf && buf.length > 0) parts.push(new Int8Array(buf));
        }

        var tail = encoder.flush();
        if (tail && tail.length > 0) parts.push(new Int8Array(tail));

        return new Blob(parts, { type: 'audio/mpeg' });
    },

    _decodeAudio: function (arrayBuffer) {
        var Ctx = window.AudioContext || window.webkitAudioContext;
        if (!Ctx) return Promise.reject(new Error('AudioContext پشتیبانی نمی‌شود'));

        var ctx = new Ctx();
        return new Promise(function (resolve, reject) {
            var finish = function (audioBuffer) {
                try { ctx.close(); } catch (e) { }
                resolve(audioBuffer);
            };
            var fail = function (err) {
                try { ctx.close(); } catch (e) { }
                reject(err || new Error('decode_failed'));
            };
            try {
                var maybe = ctx.decodeAudioData(arrayBuffer, finish, fail);
                if (maybe && typeof maybe.then === 'function') maybe.then(finish, fail);
            } catch (e) {
                fail(e);
            }
        });
    },

    _convertToMp3: function (blob) {
        var self = this;
        return blob.arrayBuffer()
            .then(function (buf) { return self._decodeAudio(buf); })
            .then(function (audioBuffer) { return self._encodeMp3(audioBuffer); });
    },

    start: function (dotNetRef) {
        var self = this;
        self.dotNetRef = dotNetRef;

        if (!self.isSupported()) {
            var reason = self._hasEncoder()
                ? 'مرورگر از ضبط صدا پشتیبانی نمی‌کند.'
                : 'کتابخانهٔ تبدیل MP3 بارگذاری نشد؛ صفحه را دوباره باز کنید.';
            return Promise.resolve({ Ok: false, Error: reason });
        }
        if (self.mediaRecorder && self.mediaRecorder.state === 'recording') {
            return Promise.resolve({ Ok: false, Error: 'ضبط در حال انجام است.' });
        }

        return navigator.mediaDevices.getUserMedia({
            audio: {
                echoCancellation: false,
                noiseSuppression: false,
                autoGainControl: true,
                channelCount: 1
            }
        })
            .then(function (stream) {
                self.clearPending();
                self.recordingSeq++;
                self.stream = stream;
                self.chunks = [];
                self.mimeType = self._bestMimeType();
                var options = self.mimeType ? { mimeType: self.mimeType } : {};
                self.mediaRecorder = new MediaRecorder(stream, options);

                if (self.mimeType.indexOf('mp4') >= 0 || self.mimeType.indexOf('aac') >= 0) {
                    self.fileExt = 'm4a';
                } else if (self.mimeType.indexOf('ogg') >= 0) {
                    self.fileExt = 'ogg';
                } else {
                    self.fileExt = 'webm';
                }

                self.mediaRecorder.ondataavailable = function (e) {
                    if (e.data && e.data.size > 0) self.chunks.push(e.data);
                };

                self.mediaRecorder.onstop = function () {
                    var type = self.mimeType || 'audio/webm';
                    var blob = new Blob(self.chunks, { type: type });
                    var sec = Math.max(1, Math.round((Date.now() - self.startTime) / 1000));
                    var seq = self.recordingSeq;

                    self._stopTick();
                    self._stopStream();

                    if (blob.size > 0) {
                        self.pendingBlob = blob;
                        if (self.pendingUrl) URL.revokeObjectURL(self.pendingUrl);
                        self.pendingUrl = URL.createObjectURL(blob);
                    }

                    // اعلام پایان ضبط به کامپوننت (پیش‌نمایش فوراً آماده است)
                    if (self.dotNetRef) {
                        self.dotNetRef.invokeMethodAsync('OnRecordingStopped', sec);
                    }

                    // تبدیل در پس‌زمینه؛ آپلود تا پایان تبدیل صبر می‌کند
                    if (blob.size > 0) {
                        self.encodeError = '';
                        self.convertPromise = self._convertToMp3(blob)
                            .then(function (mp3) {
                                if (seq === self.recordingSeq && mp3 && mp3.size > 0) {
                                    self.uploadBlob = mp3;
                                    return mp3;
                                }
                                return null;
                            })
                            .catch(function (err) {
                                console.warn('serviceVoiceRecorder: MP3 encode failed', err);
                                self.uploadBlob = null;
                                self.encodeError = (err && err.message) || 'encode_failed';
                                return null;
                            });
                    }
                };

                self.startTime = Date.now();
                self.mediaRecorder.start();
                self._tick();
                return { Ok: true, Error: '' };
            })
            .catch(function (err) {
                self._stopTick();
                self._stopStream();
                var message = 'دسترسی به میکروفون ممکن نشد.';
                if (err && (err.name === 'NotAllowedError' || err.name === 'SecurityError')) {
                    message = 'اجازه دسترسی به میکروفون داده نشد.';
                } else if (err && err.name === 'NotFoundError') {
                    message = 'میکروفونی روی این دستگاه پیدا نشد.';
                }
                return { Ok: false, Error: message };
            });
    },

    stop: function () {
        if (this.mediaRecorder && this.mediaRecorder.state !== 'inactive') {
            this.mediaRecorder.stop();
        }
        this._stopTick();
    },

    clearPending: function () {
        this.recordingSeq++;
        this.pendingBlob = null;
        this.uploadBlob = null;
        this.convertPromise = null;
        this.encodeError = '';
        if (this.pendingUrl) {
            URL.revokeObjectURL(this.pendingUrl);
            this.pendingUrl = null;
        }
    },

    getPendingUrl: function () {
        return this.pendingUrl || '';
    },

    hasPending: function () {
        return !!this.pendingBlob;
    },

    scrollToEditor: function (id) {
        if (window.matchMedia && !window.matchMedia('(max-width: 1099.98px)').matches) return;
        var el = document.getElementById(id);
        if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    },

    uploadPending: function (url) {
        var self = this;
        if (!self.pendingBlob) {
            return Promise.resolve({ Ok: false, Status: 0, Body: 'no_pending' });
        }

        // فقط MP3 آپلود می‌شود؛ اگر تبدیل شکست بخورد فایل خام ارسال نمی‌شود.
        var wait = self.convertPromise || Promise.resolve(null);

        return wait.then(function () {
            if (!self.uploadBlob) {
                return {
                    Ok: false,
                    Status: 0,
                    Body: self.encodeError === 'encoder_unavailable'
                        ? 'کتابخانهٔ تبدیل MP3 بارگذاری نشد؛ صفحه را دوباره باز کنید.'
                        : 'تبدیل صدا به MP3 انجام نشد؛ لطفاً دوباره ضبط کنید.'
                };
            }

            var formData = new FormData();
            formData.append('file', self.uploadBlob, 'voice.mp3');

            var headers = {};
            try {
                var token = localStorage.getItem('auth_token');
                if (token) headers['Authorization'] = 'Bearer ' + token;
            } catch (e) { }

            return fetch(url, { method: 'POST', headers: headers, body: formData })
                .then(function (r) {
                    return r.text().then(function (text) {
                        if (r.ok) self.clearPending();
                        return { Ok: r.ok, Status: r.status, Body: text };
                    });
                })
                .catch(function (e) {
                    return { Ok: false, Status: 0, Body: (e && e.message) || 'network_error' };
                });
        });
    }
};
