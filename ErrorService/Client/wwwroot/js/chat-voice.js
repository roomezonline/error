window.__chatScrollToBottom = function (selector) {
    var el = document.querySelector(selector);
    if (el) { el.scrollTop = el.scrollHeight; }
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

window.__chatSetDotNetRef = function (ref) {
    window.__chatWidgetDotNetRef = ref;
};

window.__chatVoiceRecorder = {
    mediaRecorder: null,
    audioChunks: [],
    stream: null,
    isRecording: false,
    mimeType: '',
    fileExt: 'webm',

    _bestMimeType: function () {
        var types = ['audio/webm;codecs=opus', 'audio/webm', 'audio/mp4;codecs=mp4a.40.2', 'audio/mp4', 'audio/aac', 'audio/ogg;codecs=opus'];
        for (var i = 0; i < types.length; i++) {
            if (MediaRecorder.isTypeSupported(types[i])) return types[i];
        }
        return '';
    },

    start: function () {
        var self = this;
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
                    self.uploadRecording(blob);
                    self.stopStream();
                };
                self.mediaRecorder.start();
                self.isRecording = true;
                return true;
            })
            .catch(function () { return false; });
    },

    stop: function () {
        if (this.mediaRecorder && this.mediaRecorder.state !== 'inactive') {
            this.mediaRecorder.stop();
            this.isRecording = false;
        }
    },

    stopStream: function () {
        if (this.stream) {
            this.stream.getTracks().forEach(function (t) { t.stop(); });
            this.stream = null;
        }
    },

    uploadRecording: function (blob) {
        var formData = new FormData();
        formData.append('file', blob, 'voice.' + this.fileExt);
        fetch('/api/chat/upload', { method: 'POST', body: formData })
            .then(function (r) { return r.json(); })
            .then(function (d) {
                if (window.__chatWidgetDotNetRef) {
                    window.__chatWidgetDotNetRef.invokeMethodAsync('OnVoiceRecorded', d && d.url ? d.url : '');
                }
            })
            .catch(function () {
                if (window.__chatWidgetDotNetRef) {
                    window.__chatWidgetDotNetRef.invokeMethodAsync('OnVoiceRecorded', '');
                }
            });
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
        fetch('/api/chat/upload', { method: 'POST', body: formData })
            .then(function (r) { return r.json(); })
            .then(function (d) {
                dotNetRef.invokeMethodAsync('OnImagePicked', d && d.url ? d.url : '');
            })
            .catch(function () {
                dotNetRef.invokeMethodAsync('OnImagePicked', '');
            });
    };
    input.click();
};
