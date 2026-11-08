// ضبط زنده صدای اکشن‌های «آوای ارورسرویس» + پخش تست قبل از ذخیره + آپلود پس از ثبت اکشن
window.serviceVoiceRecorder = {
    mediaRecorder: null,
    chunks: [],
    stream: null,
    pendingBlob: null,
    pendingUrl: null,
    startTime: 0,
    tickTimer: null,
    dotNetRef: null,
    mimeType: '',
    fileExt: 'webm',

    isSupported: function () {
        return !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia && window.MediaRecorder);
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

    start: function (dotNetRef) {
        var self = this;
        self.dotNetRef = dotNetRef;

        if (!self.isSupported()) {
            return Promise.resolve({ Ok: false, Error: 'مرورگر از ضبط صدا پشتیبانی نمی‌کند.' });
        }
        if (self.mediaRecorder && self.mediaRecorder.state === 'recording') {
            return Promise.resolve({ Ok: false, Error: 'ضبط در حال انجام است.' });
        }

        return navigator.mediaDevices.getUserMedia({ audio: true })
            .then(function (stream) {
                self.clearPending();
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

                    self._stopTick();
                    self._stopStream();

                    if (blob.size > 0) {
                        self.pendingBlob = blob;
                        if (self.pendingUrl) URL.revokeObjectURL(self.pendingUrl);
                        self.pendingUrl = URL.createObjectURL(blob);
                    }

                    if (self.dotNetRef) {
                        self.dotNetRef.invokeMethodAsync('OnRecordingStopped', sec);
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
        this.pendingBlob = null;
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

        var formData = new FormData();
        formData.append('file', self.pendingBlob, 'voice.' + self.fileExt);

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
    }
};
