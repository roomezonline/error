// site.js - bundled from individual scripts (SEO/mobile file-count optimization). Do not edit via index.html tags.

// ==== datepicker-init.js ====
window.initJalali = () => {
    if (window.jalaliDatepicker && typeof window.jalaliDatepicker.startWatch === 'function') {
        window.jalaliDatepicker.startWatch({
            selector: "[data-jdp]",
            time: false,
            hasSecond: false,
            persianDigits: false,
            autoHide: true,
            hideAfterChange: false,
            showCloseBtn: true,
            showTodayBtn: true,
            showEmptyBtn: true,
            zIndex: 4000
        });
    }
};

window.initJalaliDatePickers = window.initJalali;

window.initJalaliForElement = (element) => {
    if (!element || !window.jalaliDatepicker) return;
    
    // Ensure the library is watching this element
    window.jalaliDatepicker.startWatch({
        selector: "[data-jdp]",
        time: false,
        persianDigits: false,
        autoHide: true,
        showCloseBtn: true,
        showTodayBtn: true,
        zIndex: 4000
    });

    // Manual show trigger
    const showPicker = (e) => {
        e.stopPropagation();
        window.jalaliDatepicker.show(element);
    };

    element.addEventListener('mousedown', showPicker);
    element.addEventListener('touchstart', showPicker);
    
    // Dispatch input event for Blazor binding when value changes
    element.addEventListener('jdp:change', () => {
        element.dispatchEvent(new Event('input', { bubbles: true }));
    });
};

window.addEventListener('load', () => {
    setTimeout(window.initJalali, 500);
});

// ==== receipt-photo-zoom.js ====
window.initReceiptPhotoZoom = (wrapId, imgId) => {
    const wrap = document.getElementById(wrapId);
    const img = document.getElementById(imgId);
    if (!wrap || !img) return true;

    // Reset
    let scale = 1;
    let tx = 0;
    let ty = 0;
    let isPanning = false;
    let startX = 0;
    let startY = 0;

    // Pinch
    let isPinching = false;
    let pinchStartDist = 0;
    let pinchStartScale = 1;
    let pinchCenter = null;

    const clamp = (v, min, max) => Math.min(max, Math.max(min, v));

    const apply = () => {
        img.style.transform = `translate3d(${tx}px, ${ty}px, 0) scale(${scale})`;
    };

    const reset = () => {
        scale = 1;
        tx = 0;
        ty = 0;
        apply();
    };

    const getDist = (t1, t2) => {
        const dx = t1.clientX - t2.clientX;
        const dy = t1.clientY - t2.clientY;
        return Math.sqrt(dx * dx + dy * dy);
    };

    const getCenter = (t1, t2) => ({
        x: (t1.clientX + t2.clientX) / 2,
        y: (t1.clientY + t2.clientY) / 2
    });

    // Make sure we don't add duplicate listeners on re-open.
    // We mark the element and if already initialized, just reset transform.
    if (wrap.dataset.zoomInit === '1') {
        reset();
        return true;
    }
    wrap.dataset.zoomInit = '1';

    wrap.addEventListener('dblclick', (e) => {
        e.preventDefault();
        if (scale > 1) {
            reset();
        } else {
            scale = 2;
            apply();
        }
    });

    wrap.addEventListener('wheel', (e) => {
        e.preventDefault();
        const delta = -e.deltaY;
        const factor = delta > 0 ? 1.08 : 0.92;
        scale = clamp(scale * factor, 1, 5);
        if (scale === 1) {
            tx = 0;
            ty = 0;
        }
        apply();
    }, { passive: false });

    // Pointer panning for desktop
    wrap.addEventListener('pointerdown', (e) => {
        if (e.pointerType === 'touch') return; // touch handled by touch events below
        if (scale <= 1) return;
        isPanning = true;
        startX = e.clientX - tx;
        startY = e.clientY - ty;
        wrap.setPointerCapture(e.pointerId);
    });

    wrap.addEventListener('pointermove', (e) => {
        if (!isPanning) return;
        tx = e.clientX - startX;
        ty = e.clientY - startY;
        apply();
    });

    wrap.addEventListener('pointerup', () => {
        isPanning = false;
    });

    wrap.addEventListener('pointercancel', () => {
        isPanning = false;
    });

    // Touch pinch + pan
    wrap.addEventListener('touchstart', (e) => {
        if (e.touches.length === 2) {
            isPinching = true;
            pinchStartDist = getDist(e.touches[0], e.touches[1]);
            pinchStartScale = scale;
            pinchCenter = getCenter(e.touches[0], e.touches[1]);
        } else if (e.touches.length === 1 && scale > 1) {
            isPanning = true;
            startX = e.touches[0].clientX - tx;
            startY = e.touches[0].clientY - ty;
        }
    }, { passive: true });

    wrap.addEventListener('touchmove', (e) => {
        if (isPinching && e.touches.length === 2) {
            e.preventDefault();
            const dist = getDist(e.touches[0], e.touches[1]);
            const next = clamp(pinchStartScale * (dist / pinchStartDist), 1, 5);
            scale = next;
            if (scale === 1) {
                tx = 0;
                ty = 0;
            }
            apply();
            return;
        }

        if (isPanning && e.touches.length === 1) {
            e.preventDefault();
            tx = e.touches[0].clientX - startX;
            ty = e.touches[0].clientY - startY;
            apply();
        }
    }, { passive: false });

    wrap.addEventListener('touchend', (e) => {
        if (e.touches.length < 2) isPinching = false;
        if (e.touches.length === 0) isPanning = false;
    });

    wrap.addEventListener('touchcancel', () => {
        isPinching = false;
        isPanning = false;
    });

    // Initial apply
    apply();
    return true;
};

// ==== money-input.js ====
(function () {
    "use strict";

    function normalizeDigit(ch) {
        var code = ch.charCodeAt(0);
        if (ch >= "0" && ch <= "9") return ch;
        if (code >= 0x06F0 && code <= 0x06F9) return String.fromCharCode(code - 0x06F0 + 48);
        if (code >= 0x0660 && code <= 0x0669) return String.fromCharCode(code - 0x0660 + 48);
        return "";
    }

    function formatDigits(digits) {
        var first = digits.length % 3;
        var out = "";
        var i = 0;
        if (first > 0) { out = digits.slice(0, first); i = first; }
        for (; i < digits.length; i += 3) {
            if (out) out += ",";
            out += digits.slice(i, i + 3);
        }
        return out;
    }

    function formatMoneyInput(el) {
        var value = el.value;
        if (!value) return;

        var caret = el.selectionStart == null ? value.length : el.selectionStart;
        var digits = "";
        var digitsBeforeCaret = 0;
        for (var i = 0; i < value.length; i++) {
            var d = normalizeDigit(value[i]);
            if (d === "") continue;
            digits += d;
            if (i < caret) digitsBeforeCaret++;
        }

        var formatted = formatDigits(digits);
        if (el.value !== formatted) {
            el.value = formatted;
        }

        var pos = 0, seen = 0;
        while (pos < formatted.length && seen < digitsBeforeCaret) {
            if (formatted[pos] >= "0" && formatted[pos] <= "9") seen++;
            pos++;
        }
        if (el.setSelectionRange) {
            try { el.setSelectionRange(pos, pos); } catch (e) { }
        }
    }

    document.addEventListener("input", function (e) {
        var el = e.target;
        if (!el || el.tagName !== "INPUT") return;
        if (!el.classList || !el.classList.contains("iv-input--money")) return;
        if (el.readOnly || el.disabled) return;
        if (!el.closest(".iv-page")) return;
        if (e.isComposing) return;
        formatMoneyInput(el);
    }, true);
})();

// ==== drag-scroll.js ====
window.initDragScroll = (element) => {
    if (!element || element.dataset.dragScrollInit === '1') return;
    element.dataset.dragScrollInit = '1';

    const isRtl = getComputedStyle(element).direction === 'rtl';

    let rtlNeg = null;
    const detectRtlNeg = () => {
        if (rtlNeg !== null) return rtlNeg;
        const div = document.createElement('div');
        div.dir = 'rtl';
        div.style.cssText = 'width:4px;height:1px;overflow:scroll;position:absolute;top:-9999px';
        const inner = document.createElement('div');
        inner.style.cssText = 'width:10px;height:1px';
        div.appendChild(inner);
        document.body.appendChild(div);
        div.scrollLeft = 1;
        rtlNeg = div.scrollLeft === 0;
        document.body.removeChild(div);
        return rtlNeg;
    };

    const getMax = () => Math.max(0, element.scrollWidth - element.clientWidth);

    const getNorm = () => {
        if (!isRtl) return element.scrollLeft;
        return detectRtlNeg() ? -element.scrollLeft : element.scrollLeft;
    };

    const setNorm = (val) => {
        const clamped = Math.max(0, Math.min(getMax(), val));
        if (!isRtl) { element.scrollLeft = clamped; return; }
        element.scrollLeft = detectRtlNeg() ? -clamped : clamped;
    };

    let isDown = false;
    let startX = 0;
    let scrollStart = 0;

    element.addEventListener('mousedown', (e) => {
        if (e.button !== 0) return;
        e.preventDefault();
        isDown = true;
        startX = e.clientX;
        scrollStart = getNorm();
        element.style.cursor = 'grabbing';
        element.style.userSelect = 'none';
    });

    document.addEventListener('mousemove', (e) => {
        if (!isDown) return;
        const dx = e.clientX - startX;
        setNorm(scrollStart + (isRtl ? dx : -dx));
    });

    document.addEventListener('mouseup', () => {
        if (!isDown) return;
        isDown = false;
        element.style.cursor = '';
        element.style.userSelect = '';
    });

    element.addEventListener('wheel', (e) => {
        if (Math.abs(e.deltaX) > Math.abs(e.deltaY)) return;
        e.preventDefault();
        setNorm(getNorm() + (isRtl ? e.deltaY : -e.deltaY) * 0.5);
    }, { passive: false });
};

// ==== lazy-video.js ====
window.lazyVideoLoader = {
    observe: function () {
        const videoObserver = new IntersectionObserver((entries, observer) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const video = entry.target;
                    // If video has a data-src, move it to src
                    if (video.dataset.src) {
                        video.src = video.dataset.src;
                        video.removeAttribute('data-src');
                    }
                    // Load the video
                    video.load();
                    observer.unobserve(video);
                }
            });
        }, {
            rootMargin: '50px 0px',
            threshold: 0.01
        });

        const videos = document.querySelectorAll('video.lazy-video');
        videos.forEach(video => videoObserver.observe(video));
    }
};

// ==== hero-typewriter.js ====
(function () {
    const RTL_REGEX = /[\u0591-\u07FF\uFB1D-\uFDFD\uFE70-\uFEFC]/;

    function isRtl(text) {
        return RTL_REGEX.test(text || "");
    }

    function sleep(ms) {
        return new Promise((r) => setTimeout(r, ms));
    }

    async function typeInto(el, text, options) {
        if (!el) return;

        const token = (Date.now() + Math.random()).toString(16);
        el.__typewriterToken = token;

        const rtl = isRtl(text);
        el.dir = rtl ? "rtl" : "ltr";

        el.classList.add("is-typing");

        // Reserve the final height before animating so typing never pushes
        // elements below the hero and causes layout shifts (CLS).
        el.style.minHeight = "";
        el.textContent = text;
        if (el.offsetHeight > 0) {
            el.style.minHeight = el.offsetHeight + "px";
        }
        el.textContent = "";
        el.style.visibility = "visible";

        const speed = options?.speedMs ?? 28;
        const startDelay = options?.startDelayMs ?? 0;
        const pauseAtEnd = options?.pauseAtEndMs ?? 250;

        if (startDelay > 0) {
            await sleep(startDelay);
        }

        for (let i = 0; i < text.length; i++) {
            if (el.__typewriterToken !== token) return;
            el.textContent += text[i];
            await sleep(speed);
        }

        if (pauseAtEnd > 0) {
            await sleep(pauseAtEnd);
        }

        if (el.__typewriterToken === token) {
            el.classList.remove("is-typing");
        }
    }

    window.heroTypewriter = {
        run: async function (rootSelector) {
            const root = document.querySelector(rootSelector || ".hero__slider");
            if (!root) return;

            const activeSlide = root.querySelector(".hero__slide.is-active");
            if (!activeSlide) return;

            const titleEl = activeSlide.querySelector(".hero__typedTitle");
            const subEl = activeSlide.querySelector(".hero__typedSubtitle");

            const title = (titleEl?.getAttribute("data-text") || "").trim();
            const subtitle = (subEl?.getAttribute("data-text") || "").trim();

            await typeInto(titleEl, title, { speedMs: 26, startDelayMs: 60, pauseAtEndMs: 220 });
            await typeInto(subEl, subtitle, { speedMs: 18, startDelayMs: 80, pauseAtEndMs: 0 });
        }
    };
})();

// ==== mobile-interactions.js ====
window.mobileInteractions = {
    hapticFeedback: (type) => {
        if (!window.navigator || !window.navigator.vibrate) return;
        switch (type) {
            case 'light': window.navigator.vibrate(10); break;
            case 'medium': window.navigator.vibrate(20); break;
            case 'error': window.navigator.vibrate([10, 30, 10]); break;
            case 'success': window.navigator.vibrate([20, 10, 20]); break;
        }
    },
    setupRippleEffect: () => {
        document.addEventListener('pointerdown', (e) => {
            const target = e.target.closest('.nav-item, .quick-link, .suggest-item, .btn.ripple');
            if (!target) return;

            const ripple = document.createElement('div');
            ripple.className = 'ripple-effect';
            
            const rect = target.getBoundingClientRect();
            const size = Math.max(rect.width, rect.height);
            ripple.style.width = ripple.style.height = `${size}px`;
            
            ripple.style.left = `${e.clientX - rect.left - size/2}px`;
            ripple.style.top = `${e.clientY - rect.top - size/2}px`;
            
            target.appendChild(ripple);
            ripple.addEventListener('animationend', () => ripple.remove());
        });
    }
};

window.mobileInteractions.setupRippleEffect();

window.formatPriceInputs = () => {
    document.querySelectorAll('.price-input-ltr').forEach(el => {
        if (el.dataset.priceInit) return;
        el.dataset.priceInit = '1';
        el.addEventListener('input', function () {
            const raw = this.value.replace(/,/g, '').replace(/[^0-9]/g, '');
            if (raw) {
                this.value = parseInt(raw, 10).toLocaleString('en-US');
            } else {
                this.value = '';
            }
        });
    });
};

window.getPriceRawValue = (id) => {
    const el = document.getElementById(id);
    if (!el) return null;
    return (el.value || '')
        .replace(/[\u06F0-\u06F9]/g, c => String.fromCharCode(c.charCodeAt(0) - 0x06F0 + 0x30))
        .replace(/[\u0660-\u0669]/g, c => String.fromCharCode(c.charCodeAt(0) - 0x0660 + 0x30))
        .replace(/[,،\u066C\s]/g, '');
};

// ==== scroll-reveal.js ====
window.initScrollReveal = function () {
    if (!window.__srObserver) {
        window.__srObserver = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    entry.target.classList.add('visible');
                    window.__srObserver.unobserve(entry.target);
                }
            });
        }, { threshold: 0.05, rootMargin: '0px 0px -30px 0px' });
    }

    document.querySelectorAll('.reveal:not(.visible)').forEach(function (el) {
        window.__srObserver.observe(el);
    });

    return true;
};

window.observeNewReveals = function () {
    if (window.initScrollReveal) {
        window.initScrollReveal();
    }
};

// ==== chat-voice.js ====
window.__chatScrollToBottom = function (selector) {
    var el = document.querySelector(selector);
    if (!el) return;
    var top = el.scrollHeight;
    try { el.scrollTo({ top: top, behavior: 'instant' }); } catch (e) { el.scrollTop = top; }
    el.scrollTop = top;
    if (window.requestAnimationFrame) {
        window.requestAnimationFrame(function () {
            try { el.scrollTo({ top: el.scrollHeight, behavior: 'instant' }); } catch (e) { el.scrollTop = el.scrollHeight; }
            el.scrollTop = el.scrollHeight;
        });
    }
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
        var Ctx = window.AudioContext || window.webkitAudioContext;
        var ctx = new Ctx();
        var now = ctx.currentTime;
        [[1046.5, 0], [1318.51, 0.11]].forEach(function (n) {
            var osc = ctx.createOscillator();
            var gain = ctx.createGain();
            osc.type = 'sine';
            osc.frequency.value = n[0];
            gain.gain.setValueAtTime(0.0001, now + n[1]);
            gain.gain.exponentialRampToValueAtTime(0.2, now + n[1] + 0.03);
            gain.gain.exponentialRampToValueAtTime(0.0001, now + n[1] + 0.4);
            osc.connect(gain);
            gain.connect(ctx.destination);
            osc.start(now + n[1]);
            osc.stop(now + n[1] + 0.45);
        });
        setTimeout(function () { try { ctx.close(); } catch (e) {} }, 1200);
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

// ==== clipboard.js ====
window.__copyToClipboard = function(text) {
    if (navigator.clipboard && navigator.clipboard.writeText) {
        return navigator.clipboard.writeText(text).catch(function() {
            fallbackCopy(text);
        });
    } else {
        fallbackCopy(text);
        return Promise.resolve();
    }
    function fallbackCopy(t) {
        var ta = document.createElement('textarea');
        ta.value = t;
        ta.style.position = 'fixed';
        ta.style.opacity = '0';
        ta.style.pointerEvents = 'none';
        document.body.appendChild(ta);
        ta.select();
        try { document.execCommand('copy'); } catch(e) {}
        document.body.removeChild(ta);
    }
};

window.__initOnlineDetection = function(dotNetRef) {
    window.__onlineHandler = function() {
        dotNetRef.invokeMethodAsync('SetOnlineStatus', true);
    };
    window.__offlineHandler = function() {
        dotNetRef.invokeMethodAsync('SetOnlineStatus', false);
    };
    window.addEventListener('online', window.__onlineHandler);
    window.addEventListener('offline', window.__offlineHandler);
    return navigator.onLine;
};

window.__removeOnlineDetection = function() {
    if (window.__onlineHandler) {
        window.removeEventListener('online', window.__onlineHandler);
    }
    if (window.__offlineHandler) {
        window.removeEventListener('offline', window.__offlineHandler);
    }
};

// ==== commitment-export.js ====
window.captureCommitmentPng = function (selector, fileName) {
    var el = document.querySelector(selector);
    if (!el) return;
    window.__ensureGroup('html2canvas').then(function () {
        html2canvas(el, { scale: 2, useCORS: true, backgroundColor: '#ffffff' }).then(function (canvas) {
            canvas.toBlob(function (blob) {
                var link = document.createElement('a');
                link.download = fileName || 'commitment.png';
                link.href = URL.createObjectURL(blob);
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
                URL.revokeObjectURL(link.href);
            });
        });
    }).catch(function (e) { console.error('html2canvas load failed:', e); });
};

window.printCommitment = function (selector) {
    var el = document.querySelector(selector);
    if (!el) return;
    var html = el.outerHTML;
    var win = window.open('', '_blank');
    if (!win) return;
    win.document.write('<!DOCTYPE html><html dir="rtl" lang="fa"><head><meta charset="utf-8">');
    win.document.write('<base href="' + document.baseURI + '">');
    var styles = document.querySelectorAll('style, link[rel="stylesheet"]');
    styles.forEach(function(s) { win.document.write(s.outerHTML); });
    win.document.write('</head><body style="padding:20px;font-family:Vazirmatn, Tahoma, sans-serif;">' + html + '</body></html>');
    win.document.close();
    win.focus();
    win.print();
};

window.shareCommitment = function (selector, fileName) {
    var el = document.querySelector(selector);
    if (!el) return;
    window.__ensureGroup('html2canvas').then(function () {
        html2canvas(el, { scale: 2, useCORS: true, backgroundColor: '#ffffff' }).then(function (canvas) {
            canvas.toBlob(function (blob) {
                var file = new File([blob], fileName || 'commitment.png', { type: 'image/png' });
                if (navigator.share) {
                    navigator.share({ files: [file], title: 'تعهدنامه' }).catch(function () { });
                }
            });
        });
    }).catch(function (e) { console.error('html2canvas load failed:', e); });
};
