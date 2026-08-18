window.__chatScrollToBottom = function (selector) {
    var el = document.querySelector(selector);
    if (el) { el.scrollTop = el.scrollHeight; }
};

window.__chatFocusInput = function (selector) {
    var el = document.querySelector(selector);
    if (el) { el.focus(); }
};

window.__chatWidgetVisitorId = function () {
    try { return localStorage.getItem('chat_visitor_id') || ''; } catch (e) { return ''; }
};

window.__chatPlayNotification = function () {
    try {
        var ctx = new (window.AudioContext || window.webkitAudioContext)();
        var osc = ctx.createOscillator();
        var gain = ctx.createGain();
        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.frequency.value = 800;
        gain.gain.value = 0.3;
        osc.start();
        osc.stop(ctx.currentTime + 0.15);
    } catch (e) {}
};

window.__chatRequestNotifyPermission = function () {
    try {
        if ('Notification' in window && Notification.permission === 'default') {
            Notification.requestPermission();
        }
    } catch (e) {}
};

window.__chatNotify = function (title, body) {
    try {
        if ('Notification' in window && Notification.permission === 'granted' && document.hidden) {
            new Notification(title, { body: body, icon: '/favicon.ico' });
        }
    } catch (e) {}
};

window.__chatFlashTitle = function (text) {
    try {
        var orig = document.title;
        if (window.__chatFlashTimer) clearInterval(window.__chatFlashTimer);
        var on = false;
        var start = Date.now();
        window.__chatFlashTimer = setInterval(function () {
            on = !on;
            document.title = on ? text : orig;
            if (Date.now() - start > 7000) {
                clearInterval(window.__chatFlashTimer);
                window.__chatFlashTimer = null;
                document.title = orig;
            }
        }, 900);
    } catch (e) {}
};

window.__chatSetDotNetRef = function (ref) {
    window.__chatWidgetDotNetRef = ref;
};

window.__chatUploadFetch = function (url, formData) {
    var token = '';
    try { token = localStorage.getItem('auth_token') || ''; } catch (e) {}
    var opts = { method: 'POST', body: formData };
    if (token) opts.headers = { 'Authorization': 'Bearer ' + token };
    return fetch(url, opts);
};

window.__chatVoiceRecorder = {
    mediaRecorder: null,
    audioChunks: [],
    stream: null,
    isRecording: false,
    mimeType: '',
    fileExt: 'webm',
    startTime: 0,
    tickTimer: null,
    pendingBlob: null,
    pendingUrl: null,
    visitorId: '',
    autoSend: false,

    _bestMimeType: function () {
        var types = ['audio/webm;codecs=opus', 'audio/webm', 'audio/mp4;codecs=mp4a.40.2', 'audio/mp4', 'audio/aac', 'audio/ogg;codecs=opus'];
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
            if (window.__chatWidgetDotNetRef) {
                window.__chatWidgetDotNetRef.invokeMethodAsync('OnRecordingTick', sec);
            }
        }, 1000);
    },

    _stopTick: function () {
        if (this.tickTimer) { clearInterval(this.tickTimer); this.tickTimer = null; }
    },

    start: function () {
        var self = this;
        if (self.isRecording) return Promise.resolve(false);
        self.visitorId = window.__chatWidgetVisitorId();
        return navigator.mediaDevices.getUserMedia({ audio: true })
            .then(function (stream) {
                self.stream = stream;
                self.audioChunks = [];
                self.mimeType = self._bestMimeType();
                var options = self.mimeType ? { mimeType: self.mimeType } : {};
                self.mediaRecorder = new MediaRecorder(stream, options);
                self.fileExt = self.mimeType.indexOf('mp4') >= 0 || self.mimeType.indexOf('aac') >= 0 ? 'mp4' : 'webm';
                self.mediaRecorder.ondataavailable = function (e) {
                    if (e.data.size > 0) self.audioChunks.push(e.data);
                };
                self.mediaRecorder.onstop = function () {
                    var type = self.mimeType || 'audio/webm';
                    var blob = new Blob(self.audioChunks, { type: type });
                    var sec = Math.max(1, Math.round((Date.now() - self.startTime) / 1000));
                    self.isRecording = false;
                    self._stopTick();
                    self.stopStream();
                    if (self.autoSend) {
                        self._upload(blob);
                    } else {
                        self.pendingBlob = blob;
                        if (self.pendingUrl) URL.revokeObjectURL(self.pendingUrl);
                        self.pendingUrl = URL.createObjectURL(blob);
                        if (window.__chatWidgetDotNetRef) {
                            window.__chatWidgetDotNetRef.invokeMethodAsync('OnVoiceStopped', sec);
                        }
                    }
                };
                self.startTime = Date.now();
                self.mediaRecorder.start();
                self.isRecording = true;
                self._tick();
                return true;
            })
            .catch(function () { return false; });
    },

    stop: function () {
        if (this.mediaRecorder && this.mediaRecorder.state !== 'inactive') {
            this.mediaRecorder.stop();
        }
        this._stopTick();
        this.isRecording = false;
    },

    stopStream: function () {
        if (this.stream) {
            this.stream.getTracks().forEach(function (t) { t.stop(); });
            this.stream = null;
        }
    },

    _upload: function (blob) {
        var self = this;
        var formData = new FormData();
        formData.append('file', blob, 'voice.' + self.fileExt);
        window.__chatUploadFetch('/api/chat/upload?visitorId=' + encodeURIComponent(self.visitorId || ''), formData)
            .then(function (r) { return r.json(); }).catch(function () { return { url: '' }; })
            .then(function (d) {
                var url = d && d.url ? d.url : '';
                if (window.__chatWidgetDotNetRef) {
                    window.__chatWidgetDotNetRef.invokeMethodAsync('OnVoiceRecorded', url);
                }
            });
    },

    clearPending: function () {
        this.pendingBlob = null;
        if (this.pendingUrl) { URL.revokeObjectURL(this.pendingUrl); this.pendingUrl = null; }
    },

    setAutoSend: function (flag) {
        this.autoSend = !!flag;
    },

    getPendingUrl: function () {
        return this.pendingUrl || '';
    },

    uploadPending: function () {
        var self = this;
        if (!self.pendingBlob) return Promise.resolve(false);
        self._upload(self.pendingBlob);
        self.clearPending();
        return Promise.resolve(true);
    },

    isSupported: function () {
        return !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia && window.MediaRecorder);
    }
};

window.__chatPickImage = function (dotNetRef) {
    var input = document.createElement('input');
    input.type = 'file';
    input.accept = 'image/*';
    input.onchange = function () {
        var file = input.files[0];
        if (!file) return;
        var formData = new FormData();
        formData.append('file', file);
        window.__chatUploadFetch('/api/chat/upload?visitorId=' + encodeURIComponent(window.__chatWidgetVisitorId()), formData)
            .then(function (r) { return r.json(); }).catch(function () { return { url: '' }; })
            .then(function (d) {
                dotNetRef.invokeMethodAsync('OnImagePicked', d && d.url ? d.url : '');
            });
    };
    input.click();
};

window.__chatPickFile = function (dotNetRef) {
    var input = document.createElement('input');
    input.type = 'file';
    input.onchange = function () {
        var file = input.files[0];
        if (!file) return;
        var formData = new FormData();
        formData.append('file', file);
        window.__chatUploadFetch('/api/chat/upload?visitorId=' + encodeURIComponent(window.__chatWidgetVisitorId()), formData)
            .then(function (r) { return r.json(); }).catch(function () { return { url: '' }; })
            .then(function (d) {
                var payload = d && d.url ? JSON.stringify({
                    url: d.url,
                    fileName: d.fileName || '',
                    fileSize: d.fileSize || 0,
                    contentType: d.contentType || ''
                }) : '';
                dotNetRef.invokeMethodAsync('OnFilePicked', payload);
            });
    };
    input.click();
};

window.__chatLoadPending = function () {
    try { return JSON.parse(localStorage.getItem('chat_pending') || '[]'); } catch (e) { return []; }
};

window.__chatSavePending = function (list) {
    try { localStorage.setItem('chat_pending', JSON.stringify(list)); } catch (e) {}
};

window.__chatKeyboardWatch = function () {
    var app = document.querySelector('.chat-app');
    if (!app) return;
    function update() {
        var kb = 0;
        if (window.visualViewport) {
            var vv = window.visualViewport;
            kb = Math.max(0, (window.innerHeight || vv.height) - vv.height - (vv.offsetTop || 0));
        }
        app.style.setProperty('--chat-kb', kb + 'px');
    }
    if (window.visualViewport) {
        window.visualViewport.addEventListener('resize', update);
        window.visualViewport.addEventListener('scroll', update);
    }
    window.addEventListener('resize', update);
    ['focusin', 'focusout'].forEach(function (evt) {
        window.addEventListener(evt, function () { setTimeout(update, 120); });
    });
    update();
};
