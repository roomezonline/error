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

// ==== carousel.js ====
// Digikala-style carousels: arrow navigation + mouse/pen drag-to-scroll.
// Touch keeps native scrolling. Any element with overflow-x auto/scroll gets drag support;
// opt out with data-no-drag-scroll. window.initDragScroll is kept for backwards compatibility.
(function () {
    if (window.__carouselInstalled) return;
    window.__carouselInstalled = true;

    let rtlMode = null;
    function getRtlMode() {
        if (rtlMode !== null) return rtlMode;
        try {
            const probe = document.createElement('div');
            probe.dir = 'rtl';
            probe.style.cssText = 'width:4px;height:1px;overflow:scroll;position:absolute;top:-9999px;left:-9999px;visibility:hidden';
            const inner = document.createElement('div');
            inner.style.cssText = 'width:10px;height:1px';
            probe.appendChild(inner);
            document.body.appendChild(probe);
            probe.scrollLeft = 1;
            rtlMode = probe.scrollLeft === 0 ? 1 : 2;
            probe.remove();
        } catch (e) {
            rtlMode = 1;
        }
        return rtlMode;
    }

    function maxScroll(el) {
        return Math.max(0, el.scrollWidth - el.clientWidth);
    }

    function scrollInfo(el) {
        const rtl = getComputedStyle(el).direction === 'rtl';
        return { rtl: rtl, mode: rtl ? getRtlMode() : 0 };
    }

    // Normalized position: 0 = start (right edge in RTL), max = end.
    function readPos(el) {
        const info = scrollInfo(el);
        if (!info.rtl) return el.scrollLeft;
        return info.mode === 2 ? maxScroll(el) - el.scrollLeft : -el.scrollLeft;
    }

    function rawLeft(el, pos, info) {
        const max = maxScroll(el);
        const clamped = Math.max(0, Math.min(max, pos));
        if (!info.rtl) return clamped;
        return info.mode === 2 ? max - clamped : -clamped;
    }

    function writePos(el, pos) {
        el.scrollLeft = rawLeft(el, pos, scrollInfo(el));
    }

    function scrollSmooth(el, pos) {
        const left = rawLeft(el, pos, scrollInfo(el));
        try { el.scrollTo({ left: left, behavior: 'smooth' }); }
        catch (e) { el.scrollLeft = left; }
    }

    function isHorizScrollable(el) {
        const ox = getComputedStyle(el).overflowX;
        return (ox === 'auto' || ox === 'scroll' || ox === 'overlay') && maxScroll(el) > 2;
    }

    function scrollerOf(target) {
        let el = target instanceof Element ? target : null;
        while (el && el !== document.body && el !== document.documentElement) {
            if (el.hasAttribute('data-no-drag-scroll')) return null;
            if (isHorizScrollable(el)) return el;
            el = el.parentElement;
        }
        return null;
    }

    // ---- arrows ----
    function stepSize(track) {
        const first = track.firstElementChild;
        if (first) {
            const style = getComputedStyle(track);
            const gap = parseFloat(style.columnGap || style.gap || '0') || 0;
            const item = first.getBoundingClientRect().width + gap;
            if (item > 0) {
                const perPage = Math.max(1, Math.floor((track.clientWidth + gap) / item));
                return Math.max(item, perPage * item - gap);
            }
        }
        return Math.max(240, Math.round(track.clientWidth * 0.9));
    }

    window.initCarousel = function (trackId, prevEl, nextEl) {
        const track = document.getElementById(trackId);
        if (!track || !prevEl || !nextEl) return;
        if (track.dataset.carouselInit === '1') return;
        track.dataset.carouselInit = '1';

        const arrows = prevEl.closest('.cc-arrows') || prevEl.parentElement;
        if (arrows) arrows.classList.remove('is-hidden');
        if (arrows) arrows.removeAttribute('data-empty');

        const update = function () {
            const max = maxScroll(track);
            const pos = readPos(track);
            if (arrows) {
                if (max <= 2) arrows.classList.add('is-hidden');
                else arrows.classList.remove('is-hidden');
            }
            prevEl.disabled = pos <= 2;
            nextEl.disabled = pos >= max - 2;
        };

        prevEl.addEventListener('click', function () { stopAutoScroll(track); scrollSmooth(track, readPos(track) - stepSize(track)); });
        nextEl.addEventListener('click', function () { stopAutoScroll(track); scrollSmooth(track, readPos(track) + stepSize(track)); });

        let raf = 0;
        const schedule = function () {
            // Keep arrow state in sync even when animation frames are throttled
            // (hidden/background tabs, low-power mode).
            update();
            if (raf) return;
            raf = requestAnimationFrame(function () { raf = 0; update(); });
        };
        track.addEventListener('scroll', schedule, { passive: true });
        track.addEventListener('load', schedule, true);
        if (window.ResizeObserver) {
            const ro = new ResizeObserver(schedule);
            ro.observe(track);
            const first = track.firstElementChild;
            if (first) ro.observe(first);
        }
        window.addEventListener('resize', schedule, { passive: true });
        update();
        setTimeout(schedule, 350);
    };

    window.initDragScroll = function (element) {
        if (element && element.classList) element.classList.add('is-drag-scroll');
    };

    // ---- drag ----
    let drag = null;
    let blockClick = false;
    let clickTimer = 0;

    document.addEventListener('pointerdown', function (e) {
        if (e.pointerType === 'touch') {
            const tel = autoTrackOf(e.target);
            if (tel) stopAutoScroll(tel);
            return;
        }
        if (e.button !== 0) return;
        const t = e.target;
        if (t instanceof Element && t.closest("input, textarea, select, canvas, [contenteditable='true'], [data-no-drag-scroll]")) return;
        const el = scrollerOf(t);
        if (!el) return;
        drag = {
            el: el,
            id: e.pointerId,
            x: e.clientX,
            y: e.clientY,
            start: readPos(el),
            rtl: getComputedStyle(el).direction === 'rtl',
            axis: 0,
            prevBehavior: el.style.scrollBehavior,
            prevUserSelect: el.style.userSelect,
            prevSnap: el.style.scrollSnapType
        };
        stopAutoScroll(el);
    });

    // Cancelling mousedown stops native text selection / image & link dragging
    // while still letting the click event reach links and buttons.
    document.addEventListener('mousedown', function (e) {
        if (drag && e.button === 0) e.preventDefault();
    }, true);

    document.addEventListener('dragstart', function (e) {
        if (drag) e.preventDefault();
    }, true);

    document.addEventListener('selectstart', function (e) {
        if (drag && drag.axis === 'x') e.preventDefault();
    }, true);

    document.addEventListener('pointermove', function (e) {
        if (!drag || e.pointerId !== drag.id) return;
        const dx = e.clientX - drag.x;
        const dy = e.clientY - drag.y;

        if (!drag.axis) {
            if (Math.abs(dx) < 5 && Math.abs(dy) < 5) return;
            if (Math.abs(dx) <= Math.abs(dy)) { drag = null; return; }
            drag.axis = 'x';
            drag.el.classList.add('is-dragging');
            drag.el.style.scrollBehavior = 'auto';
            drag.el.style.scrollSnapType = 'none';
            drag.el.style.userSelect = 'none';
            try { drag.el.setPointerCapture(e.pointerId); } catch (err) { }
            const sel = window.getSelection && window.getSelection();
            if (sel && sel.removeAllRanges) sel.removeAllRanges();
        }

        // Content must follow the pointer. In RTL the scroll origin is the right
        // edge, so moving forward means increasing the normalized position while
        // the strip itself moves rightwards.
        e.preventDefault();
        writePos(drag.el, drag.start + (drag.rtl ? dx : -dx));
    }, { passive: false });

    function endDrag(e) {
        if (!drag || (e && e.pointerId !== drag.id)) return;
        const el = drag.el;
        const moved = drag.axis === 'x';
        el.classList.remove('is-dragging');
        el.style.scrollBehavior = drag.prevBehavior;
        el.style.scrollSnapType = drag.prevSnap;
        el.style.userSelect = drag.prevUserSelect;
        try { el.releasePointerCapture(drag.id); } catch (err) { }
        drag = null;
        if (!el.matches(':hover') && isAutoVisible(el)) startAutoScroll(el);
        if (moved) {
            blockClick = true;
            clearTimeout(clickTimer);
            clickTimer = setTimeout(function () { blockClick = false; }, 100);
        }
    }
    document.addEventListener('pointerup', endDrag);
    document.addEventListener('pointercancel', endDrag);
    window.addEventListener('blur', function () { endDrag(); });

    document.addEventListener('click', function (e) {
        if (!blockClick) return;
        blockClick = false;
        e.preventDefault();
        e.stopPropagation();
        e.stopImmediatePropagation();
    }, true);

    document.addEventListener('pointerover', function (e) {
        if (e.pointerType === 'touch') return;
        const t = e.target;
        if (t instanceof Element && t.closest('[data-no-drag-scroll]')) return;
        const el = scrollerOf(t);
        if (el && el.dataset.dragCursor !== '1') {
            el.dataset.dragCursor = '1';
            el.classList.add('is-drag-scroll');
        }
    }, { passive: true });

    // ---- auto-scroll ----
    // Opt in with data-auto-scroll="<px per second>". Runs while the row is in
    // view and the pointer is elsewhere; hovering pauses it so only manual
    // drag/scroll acts on the track. Desktop pointers only.
    const autoScrolls = new WeakMap();
    const autoVisible = new WeakSet();
    const reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const canAutoScroll = window.matchMedia && window.matchMedia('(hover: hover) and (pointer: fine)').matches;

    const autoObserver = canAutoScroll && 'IntersectionObserver' in window
        ? new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                const el = entry.target;
                if (entry.isIntersecting) {
                    autoVisible.add(el);
                    if (!el.matches(':hover')) startAutoScroll(el);
                } else {
                    autoVisible.delete(el);
                    stopAutoScroll(el);
                }
            });
        }, { threshold: 0.25 })
        : null;

    function isAutoVisible(el) {
        return autoObserver ? autoVisible.has(el) : true;
    }

    function registerAutoScroll(el) {
        if (!autoObserver || !el || el.dataset.autoRegistered === '1') return;
        el.dataset.autoRegistered = '1';
        autoObserver.observe(el);
    }

    function scanAutoScroll(root) {
        if (!(root instanceof Element)) return;
        if (root.matches('[data-auto-scroll]')) registerAutoScroll(root);
        root.querySelectorAll('[data-auto-scroll]').forEach(registerAutoScroll);
    }

    function autoTrackOf(target) {
        return target instanceof Element ? target.closest('[data-auto-scroll]') : null;
    }

    function stopAutoScroll(el) {
        const st = autoScrolls.get(el);
        if (!st) return;
        cancelAnimationFrame(st.raf);
        autoScrolls.delete(el);
        el.classList.remove('is-auto-scrolling');
    }

    function startAutoScroll(el) {
        if (!canAutoScroll || reduceMotion || autoScrolls.has(el) || drag) return;
        if (!isAutoVisible(el) || !isHorizScrollable(el)) return;

        const st = {
            speed: parseFloat(el.getAttribute('data-auto-scroll')) || 24,
            dir: 1,
            last: performance.now(),
            // scrollLeft rounds to whole pixels, so accumulate the fractional
            // position here; otherwise sub-pixel steps never move the track.
            pos: readPos(el),
            raf: 0
        };
        autoScrolls.set(el, st);
        el.classList.add('is-auto-scrolling');

        const step = function (now) {
            if (autoScrolls.get(el) !== st) return;
            if (!el.isConnected) { stopAutoScroll(el); return; }
            const dt = Math.min(64, Math.max(0, now - st.last)) / 1000;
            st.last = now;

            if (!(drag && drag.el === el)) {
                const max = maxScroll(el);
                if (max > 2) {
                    st.pos += st.dir * st.speed * dt;
                    if (st.pos >= max) { st.pos = max; st.dir = -1; }
                    else if (st.pos <= 0) { st.pos = 0; st.dir = 1; }
                    writePos(el, st.pos);
                }
            } else {
                st.pos = readPos(el);
            }

            st.raf = requestAnimationFrame(step);
        };
        st.raf = requestAnimationFrame(step);
    }

    document.addEventListener('pointerover', function (e) {
        if (e.pointerType === 'touch') return;
        const el = autoTrackOf(e.target);
        if (!el) return;
        if (e.relatedTarget instanceof Node && el.contains(e.relatedTarget)) return;
        stopAutoScroll(el);
    }, { passive: true });

    document.addEventListener('pointerout', function (e) {
        const el = autoTrackOf(e.target);
        if (!el) return;
        if (e.relatedTarget instanceof Node && el.contains(e.relatedTarget)) return;
        if (drag && drag.el === el) return;
        startAutoScroll(el);
    }, { passive: true });

    scanAutoScroll(document.documentElement);
    if (window.MutationObserver) {
        new MutationObserver(function (mutations) {
            mutations.forEach(function (m) {
                m.addedNodes.forEach(scanAutoScroll);
            });
        }).observe(document.documentElement, { childList: true, subtree: true });
    }
})();

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

// ==== public-hero.js ====
window.publicHero = {
    init: function (element, dotnetRef) {
        if (!element) return null;

        var startX = 0;
        var startY = 0;
        var isSwiping = false;

        function onTouchStart(e) {
            startX = e.touches[0].clientX;
            startY = e.touches[0].clientY;
            isSwiping = false;
        }

        function onTouchMove(e) {
            if (!startX) return;
            var diffX = e.touches[0].clientX - startX;
            var diffY = e.touches[0].clientY - startY;
            if (Math.abs(diffX) > Math.abs(diffY) && Math.abs(diffX) > 30) {
                isSwiping = true;
            }
        }

        function onTouchEnd(e) {
            if (!isSwiping) {
                startX = 0;
                return;
            }
            var diffX = e.changedTouches[0].clientX - startX;
            if (Math.abs(diffX) > 50) {
                dotnetRef.invokeMethodAsync(diffX > 0 ? 'Prev' : 'Next');
            }
            startX = 0;
            isSwiping = false;
        }

        element.addEventListener('touchstart', onTouchStart, { passive: true });
        element.addEventListener('touchmove', onTouchMove, { passive: true });
        element.addEventListener('touchend', onTouchEnd, { passive: true });

        return {
            dispose: function () {
                element.removeEventListener('touchstart', onTouchStart);
                element.removeEventListener('touchmove', onTouchMove);
                element.removeEventListener('touchend', onTouchEnd);
            }
        };
    },

    initVisibility: function (dotnetRef) {
        var handler = function () {
            dotnetRef.invokeMethodAsync('OnVisibilityChanged', !document.hidden);
        };
        document.addEventListener('visibilitychange', handler);
        handler();
        return {
            dispose: function () {
                document.removeEventListener('visibilitychange', handler);
            }
        };
    },
};

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
