var progressiveChart = null;

// ── State ──
var _progState = {
    timestamps: [],
    labelsFa: [],
    cycles: null,
    pointMetadata: [],
    fullRange: { startTime: null, endTime: null },
    currentRange: { startTime: null, endTime: null },
    pendingRange: { startTime: null, endTime: null },
    isLoading: false,
    _lastFetchKey: '',
    _requestSeq: 0,
    _abortController: null,
    mode: 'overview',
    segments: [],
    segmentCache: {},
    currentSegment: -1,
    monitoringId: 0,
    lastRawTs: null,
    baseTotal: 0,
    appendedCount: 0,
    totalRecords: 0
};

// ── Live update (delta fetch every 10s) ──
var _liveState = {
    enabled: false,
    fetchTimer: null,
    countdownTimer: null,
    secondsLeft: 10,
    busy: false
};
var LIVE_INTERVAL_SEC = 10;

function _liveStop() {
    _liveState.enabled = false;
    _liveState.busy = false;
    if (_liveState.fetchTimer) { clearInterval(_liveState.fetchTimer); _liveState.fetchTimer = null; }
    if (_liveState.countdownTimer) { clearInterval(_liveState.countdownTimer); _liveState.countdownTimer = null; }
    _liveUpdateUi(false);
}

function _liveUpdateUi(on, seconds) {
    var wrap = document.getElementById('dpc-live');
    var count = document.getElementById('dpc-live-countdown');
    if (wrap) wrap.classList.toggle('is-on', !!on);
    if (count) {
        if (!on) {
            count.hidden = true;
            count.textContent = '';
        } else {
            count.hidden = false;
            var s = seconds === undefined ? _liveState.secondsLeft : seconds;
            count.textContent = 'تا اپدیت ' + s.toLocaleString('fa-IR');
        }
    }
    if (on) _liveUpdateRecordsBadge();
}

function _liveUpdateRecordsBadge() {
    var el = document.getElementById('dpc-zoom-info');
    if (!el) return;
    var total = (_progState.baseTotal || 0) + (_progState.appendedCount || 0);
    if (total > 0) el.textContent = total.toLocaleString('fa-IR') + ' records';
}

function _liveScrollToEnd() {
    var scrollEl = document.getElementById('dpc-scroll');
    if (!scrollEl) return;
    var max = Math.max(0, scrollEl.scrollWidth - scrollEl.clientWidth);
    try { scrollEl.scrollTo({ left: max, behavior: 'smooth' }); }
    catch (e) { scrollEl.scrollLeft = max; }
}

function _liveFetchDelta() {
    if (!_liveState.enabled || _liveState.busy) return Promise.resolve();
    if (!progressiveChart || !_progState.monitoringId) return Promise.resolve();
    if (_progState.mode !== 'overview') return Promise.resolve();

    var lastRaw = _progState.lastRawTs;
    var lastEpoch = _progState.timestamps.length
        ? _progState.timestamps[_progState.timestamps.length - 1]
        : null;
    var hasLast = lastRaw || lastEpoch !== null;

    var params = new URLSearchParams();
    if (hasLast) {
        params.set('fromTime', lastRaw || new Date(lastEpoch).toISOString());
    } else {
        params.set('fromTime', new Date(Date.now() - 24 * 3600 * 1000).toISOString());
    }
    params.set('maxPoints', '500');

    _liveState.busy = true;

    return fetch('/api/monitoring/chart/' + _progState.monitoringId + '/progressive-records?' + params.toString())
        .then(function (r) {
            if (!r.ok) throw new Error('HTTP ' + r.status);
            return r.json();
        })
        .then(function (data) {
            if (!_liveState.enabled) return;
            var added = _liveAppendDelta(data, lastEpoch);
            if (added > 0) _liveScrollToEnd();
            _liveUpdateRecordsBadge();
        })
        .catch(function () { /* keep countdown; retry next tick */ })
        .finally(function () {
            _liveState.busy = false;
            _liveState.secondsLeft = LIVE_INTERVAL_SEC;
            if (_liveState.enabled) _liveUpdateUi(true, LIVE_INTERVAL_SEC);
        });
}

function _liveAppendDelta(data, lastEpoch) {
    if (!progressiveChart || !data || !data.timestamps || !data.timestamps.length) {
        if (data && data.fullRangeEnd) {
            var fe0 = _tsToEpoch(data.fullRangeEnd);
            if (fe0 && (!_progState.fullRange.endTime || fe0 > _progState.fullRange.endTime)) {
                _progState.fullRange.endTime = fe0;
                _progUpdateRangeLimits();
            }
        }
        return 0;
    }

    var keepIdx = [];
    var keepEpoch = [];
    for (var i = 0; i < data.timestamps.length; i++) {
        var e = _tsToEpoch(data.timestamps[i]);
        if (e === null) continue;
        if (lastEpoch !== null && lastEpoch !== undefined && e <= lastEpoch) continue;
        keepIdx.push(i);
        keepEpoch.push(e);
    }
    if (!keepIdx.length) {
        if (data.fullRangeEnd) {
            var fe1 = _tsToEpoch(data.fullRangeEnd);
            if (fe1 && (!_progState.fullRange.endTime || fe1 > _progState.fullRange.endTime)) {
                _progState.fullRange.endTime = fe1;
                _progUpdateRangeLimits();
                _syncMobileRangeSelector();
            }
        }
        return 0;
    }

    var wasAtEnd = true;
    if (_progState.pendingRange && _progState.pendingRange.endTime !== null && _progState.fullRange.endTime) {
        var slack = 60000;
        wasAtEnd = _progState.pendingRange.endTime >= (_progState.fullRange.endTime - slack);
    }

    var base = _progState.timestamps.length;
    var n = keepIdx.length;

    for (var k = 0; k < n; k++) {
        _progState.timestamps.push(keepEpoch[k]);
    }

    if (data.labelsFa) {
        for (var k = 0; k < n; k++) {
            _progState.labelsFa.push(data.labelsFa[keepIdx[k]] || '');
        }
    } else {
        for (var k = 0; k < n; k++) _progState.labelsFa.push('');
    }

    if (data.pointMetadata) {
        for (var k = 0; k < n; k++) {
            var meta = data.pointMetadata[keepIdx[k]];
            if (!meta) continue;
            var mCamel = meta.isEquipmentTransition !== undefined || meta.hasNote !== undefined || meta.index !== undefined;
            _progState.pointMetadata.push({
                index: base + k,
                isEquipmentTransition: !!(mCamel ? meta.isEquipmentTransition : meta.IsEquipmentTransition),
                transitionType: (mCamel ? meta.transitionType : meta.TransitionType) || '',
                hasNote: !!(mCamel ? meta.hasNote : meta.HasNote),
                tempChange: mCamel ? meta.tempChange : meta.TempChange,
                currentChange: mCamel ? meta.currentChange : meta.CurrentChange
            });
        }
    }

    if (data.cycles) {
        _progState.cycles = _progState.cycles || { power: [], motor: [], heater1: [], heater2: [] };
        Object.keys(data.cycles).forEach(function (key) {
            if (!Array.isArray(data.cycles[key])) return;
            if (!Array.isArray(_progState.cycles[key])) _progState.cycles[key] = [];
            data.cycles[key].forEach(function (c) {
                if (!c) return;
                var shifted = {
                    startIndex: typeof c.startIndex === 'number' ? c.startIndex + base : c.startIndex,
                    endIndex: typeof c.endIndex === 'number' ? c.endIndex + base : c.endIndex,
                    startTime: c.startTime,
                    endTime: c.endTime,
                    duration: c.duration,
                    durationSeconds: c.durationSeconds,
                    isOpen: !!c.isOpen
                };
                // JSON from server may be camelCase or Pascal depending on serializer
                if (shifted.startIndex === undefined && c.StartIndex !== undefined) {
                    shifted.startIndex = c.StartIndex + base;
                    shifted.endIndex = c.EndIndex + base;
                    shifted.startTime = c.StartTime;
                    shifted.endTime = c.EndTime;
                    shifted.duration = c.Duration;
                    shifted.durationSeconds = c.DurationSeconds;
                    shifted.isOpen = !!c.IsOpen;
                }
                _progState.cycles[key].push(shifted);
            });
        });
        _transitionCache = {};
    }

    function pushPoints(dsIdx, src, mapper) {
        var ds = progressiveChart.data.datasets[dsIdx];
        if (!ds) return;
        if (!Array.isArray(ds.data)) ds.data = [];
        for (var k = 0; k < n; k++) {
            var raw = src ? src[keepIdx[k]] : null;
            var y = mapper ? mapper(raw) : raw;
            ds.data.push({ x: keepEpoch[k], y: y });
        }
    }

    pushPoints(0, data.temperatureRef, function (v) { return v; });
    pushPoints(1, data.temperatureFreez, function (v) { return v; });
    pushPoints(2, data.power, function (v) { return v === 1 ? 1.0 : 0; });
    pushPoints(3, data.motor, function (v) { return v === 1 ? 0.9 : 0; });
    pushPoints(4, data.heater1, function (v) { return v === 1 ? 0.8 : 0; });
    pushPoints(5, data.heater2, function (v) { return v === 1 ? 0.7 : 0; });
    pushPoints(6, data.tavan, function (v) { return v; });
    pushPoints(7, data.jaryan, function (v) { return v; });

    if (_progGradients[0]) progressiveChart.data.datasets[0].backgroundColor = _progGradients[0];
    if (_progGradients[1]) progressiveChart.data.datasets[1].backgroundColor = _progGradients[1];

    if (!_progState.fullRange.startTime && keepEpoch[0] !== null && keepEpoch[0] !== undefined) {
        _progState.fullRange.startTime = keepEpoch[0];
    }
    if (data.fullRangeEnd) {
        var fe = _tsToEpoch(data.fullRangeEnd);
        if (fe && (!_progState.fullRange.endTime || fe > _progState.fullRange.endTime)) {
            _progState.fullRange.endTime = fe;
        }
    } else {
        var lastNew = keepEpoch[n - 1];
        if (!_progState.fullRange.endTime || lastNew > _progState.fullRange.endTime) {
            _progState.fullRange.endTime = lastNew;
        }
    }

    var lastIso = data.timestamps[keepIdx[n - 1]];
    if (lastIso) _progState.lastRawTs = lastIso;

    _progState.appendedCount = (_progState.appendedCount || 0) + n;

    // Grow wrapper so new points are scrollable
    var count = _progState.timestamps.length;
    var wrapper = document.getElementById('dpc-wrapper');
    var scrollEl = document.getElementById('dpc-scroll');
    var dpr = window.innerWidth < 768 ? 1 : (window.devicePixelRatio || 1);
    var safeCssMax = Math.floor(12000 / dpr);
    var pxPerPoint = 7;
    var containerWidth = scrollEl ? scrollEl.clientWidth : window.innerWidth;
    if (window.innerWidth >= 768 && count * pxPerPoint <= containerWidth) {
        if (wrapper) wrapper.style.width = '100%';
    } else {
        if (wrapper) wrapper.style.width = Math.min(count * pxPerPoint, safeCssMax) + 'px';
    }

    // If user was not tracking the end, keep their zoom window
    if (wasAtEnd) {
        _progState.pendingRange.startTime = null;
        _progState.pendingRange.endTime = null;
        _progState.currentRange.startTime = null;
        _progState.currentRange.endTime = null;
        var xs = progressiveChart.scales['x-axis-0'];
        if (xs && xs.options && xs.options.ticks) {
            xs.options.ticks.min = undefined;
            xs.options.ticks.max = undefined;
        }
    } else if (_progState.pendingRange && _progState.pendingRange.endTime !== null) {
        var xScale = progressiveChart.scales['x-axis-0'];
        if (xScale) {
            xScale.options.ticks.min = _progState.pendingRange.startTime;
            xScale.options.ticks.max = _progState.pendingRange.endTime;
        }
    }

    try { progressiveChart.update(0); } catch (e) {}
    _progUpdateRangeLimits();
    _syncMobileRangeSelector();

    if (wasAtEnd) {
        requestAnimationFrame(function () { _liveScrollToEnd(); });
    }

    return n;
}

function _liveStart() {
    if (!_liveState.enabled) {
        // ensure no stale timers
        _liveStop();
    } else {
        return;
    }
    if (!progressiveChart || !_progState.monitoringId) {
        var toggle = document.getElementById('dpc-live-toggle');
        if (toggle) toggle.checked = false;
        return;
    }

    // Live always watches the latest data in overview mode
    if (_progState.mode !== 'overview') {
        if (typeof _setChartMode === 'function') {
            try { _setChartMode('overview'); } catch (e) {}
        }
        if (_progState.mode !== 'overview') {
            var t2 = document.getElementById('dpc-live-toggle');
            if (t2) t2.checked = false;
            return;
        }
    }

    _liveState.enabled = true;
    _liveState.secondsLeft = LIVE_INTERVAL_SEC;
    _liveUpdateUi(true, LIVE_INTERVAL_SEC);

    // Leave any zoomed window and watch the full/latest range
    try {
        if (progressiveChart.resetZoom) progressiveChart.resetZoom();
    } catch (e) {}
    _progState.pendingRange.startTime = null;
    _progState.pendingRange.endTime = null;
    _progState.currentRange.startTime = null;
    _progState.currentRange.endTime = null;
    _progUpdateRangeLimits();
    _syncMobileRangeSelector();

    // Immediate fetch + scroll to latest
    _liveFetchDelta().then(function () {
        if (_liveState.enabled) _liveScrollToEnd();
    });

    _liveState.countdownTimer = setInterval(function () {
        if (!_liveState.enabled) return;
        _liveState.secondsLeft = Math.max(0, _liveState.secondsLeft - 1);
        _liveUpdateUi(true, _liveState.secondsLeft);
    }, 1000);

    _liveState.fetchTimer = setInterval(function () {
        _liveFetchDelta();
    }, LIVE_INTERVAL_SEC * 1000);
}

// ── Navigation helpers ──

var _transitionCache = {};

function _buildAllTransitions(dsIdx) {
    if (_transitionCache[dsIdx]) return _transitionCache[dsIdx];
    var cycleKeys = { 2: 'power', 3: 'motor', 4: 'heater1', 5: 'heater2' };
    var key = cycleKeys[dsIdx];
    if (!key) { _transitionCache[dsIdx] = []; return []; }
    var cycles = _progState.cycles;
    if (!cycles || !cycles[key] || !cycles[key].length) { _transitionCache[dsIdx] = []; return []; }
    var arr = cycles[key];
    var set = {};
    for (var i = 0; i < arr.length; i++) {
        if (arr[i].startIndex !== undefined) set[arr[i].startIndex] = 1;
        if (arr[i].endIndex !== undefined) set[arr[i].endIndex] = 1;
    }
    var result = Object.keys(set).map(function(k) { return parseInt(k, 10); });
    result.sort(function(a, b) { return a - b; });
    _transitionCache[dsIdx] = result;
    return result;
}

function _findTransitionAt(dsIdx, fromIdx, dir) {
    var arr = _buildAllTransitions(dsIdx);
    if (!arr.length) return -1;
    if (dir === 'next') {
        for (var i = 0; i < arr.length; i++) {
            if (arr[i] > fromIdx) return arr[i];
        }
        return arr[0];
    } else {
        for (var i = arr.length - 1; i >= 0; i--) {
            if (arr[i] < fromIdx) return arr[i];
        }
        return arr[arr.length - 1];
    }
}

function _getScrollCenterIdx() {
    var scrollEl = document.getElementById('dpc-scroll');
    if (!scrollEl || !progressiveChart) return 0;
    var xScale = progressiveChart.scales['x-axis-0'];
    if (!xScale) return 0;
    var centerX = scrollEl.scrollLeft + scrollEl.clientWidth / 2;
    var centerTs = xScale.getValueForPixel(centerX);
    if (centerTs === undefined || centerTs === null) return 0;
    var ts = _progState.timestamps;
    if (!ts || ts.length === 0) return 0;
    var best = 0, bestDist = Infinity;
    for (var i = 0; i < ts.length; i++) {
        var d = Math.abs(ts[i] - centerTs);
        if (d < bestDist) { bestDist = d; best = i; }
    }
    return best;
}

function _navToTransition(dsIdx, dir) {
    var centerIdx = _getScrollCenterIdx();
    // Clamp to chart data bounds
    var ds = progressiveChart ? progressiveChart.data.datasets[dsIdx] : null;
    if (ds && ds.data && ds.data.length > 0) {
        centerIdx = Math.max(0, Math.min(centerIdx, ds.data.length - 1));
    }
    var idx = _findTransitionAt(dsIdx, centerIdx, dir);
    if (idx < 0) return;
    _scrollToIndex(idx);
}

function _scrollToIndex(idx) {
    var scrollEl = document.getElementById('dpc-scroll');
    if (!scrollEl || !progressiveChart) return;
    var ts = _progState.timestamps;
    if (!ts || idx < 0 || idx >= ts.length) return;
    var xScale = progressiveChart.scales['x-axis-0'];
    if (!xScale) return;
    var pixelX = xScale.getPixelForValue(ts[idx]);
    if (pixelX === undefined || pixelX === null) return;
    var targetX = pixelX - scrollEl.clientWidth / 2;
    var maxScroll = Math.max(0, scrollEl.scrollWidth - scrollEl.clientWidth);
    targetX = Math.max(0, Math.min(targetX, maxScroll));
    scrollEl.scrollTo({ left: targetX, behavior: 'smooth' });
}

// ── Helpers ──
function _tsToEpoch(isoStr) {
    if (!isoStr) return null;
    if (!/Z|[+-]\d{2}:\d{2}$/.test(isoStr)) {
        isoStr += 'Z';
    }
    var d = new Date(isoStr);
    return isNaN(d.getTime()) ? null : d.getTime();
}

function _epochToTimeStr(epochMs) {
    if (epochMs === null || epochMs === undefined) return '';
    var d = new Date(epochMs);
    var month = d.getMonth() + 1;
    var day = d.getDate();
    var hh = ('0' + d.getHours()).slice(-2);
    var mm = ('0' + d.getMinutes()).slice(-2);
    return month + '/' + day + ' ' + hh + ':' + mm;
}

function _epochToFaAxisStr(epochMs) {
    if (epochMs === null || epochMs === undefined) return '';
    var scale = progressiveChart && progressiveChart.scales ? progressiveChart.scales['x-axis-0'] : null;
    var range = scale && scale.min !== undefined && scale.max !== undefined ? scale.max - scale.min : 0;
    var options = range > 86400000 * 2
        ? { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', hourCycle: 'h23' }
        : { month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' };
    try {
        return new Intl.DateTimeFormat('fa-IR-u-ca-persian-nu-latn', options).format(new Date(epochMs));
    } catch (e) {
        return _epochToTimeStr(epochMs);
    }
}

function _buildXY(dataArray, timestamps) {
    return (dataArray || []).map(function(v, i) {
        return { x: timestamps[i], y: v };
    });
}

function _epochToFaStr(epochMs) {
    if (epochMs === null || epochMs === undefined) return '';
    return new Date(epochMs).toLocaleString('fa-IR', { hour: '2-digit', minute: '2-digit' });
}

// ── Span measure tool (press-drag-release) ──
var _spanState = {
    enabled: false,
    anchor: null,   // epoch ms — first press
    end: null,      // epoch ms — current / released
    dragging: false,
    locked: false
};

function _spanFormatDuration(ms) {
    if (ms === null || ms === undefined || isNaN(ms)) return '—';
    var neg = ms < 0;
    ms = Math.abs(ms);
    var totalSec = Math.floor(ms / 1000);
    var d = Math.floor(totalSec / 86400);
    var h = Math.floor((totalSec % 86400) / 3600);
    var m = Math.floor((totalSec % 3600) / 60);
    var s = totalSec % 60;
    var fa = function (n) { return n.toLocaleString('fa-IR'); };
    var parts = [];
    if (d > 0) parts.push(fa(d) + ' روز');
    if (h > 0) parts.push(fa(h) + ' ساعت');
    if (m > 0) parts.push(fa(m) + ' دقیقه');
    if (s > 0 || parts.length === 0) parts.push(fa(s) + ' ثانیه');
    if (parts.length === 1) {
        return (neg ? '−' : '') + parts[0];
    }
    var str = parts.slice(0, -1).join(' ') + ' و ' + parts[parts.length - 1];
    return (neg ? '−' : '') + str;
}

function _spanFormatClock(epoch) {
    if (epoch === null || epoch === undefined || isNaN(epoch)) return '—';
    try {
        return new Date(epoch).toLocaleString('fa-IR', {
            year: 'numeric', month: '2-digit', day: '2-digit',
            hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23'
        });
    } catch (e) {
        return new Date(epoch).toLocaleString();
    }
}

function _spanClear() {
    _spanState.anchor = null;
    _spanState.end = null;
    _spanState.dragging = false;
    _spanState.locked = false;
    _spanUpdateHud();
    if (progressiveChart) { try { progressiveChart.draw(); } catch (e) {} }
}

function _spanUpdateHud() {
    var hud = document.getElementById('dpc-span-hud');
    var metaEl = document.getElementById('dpc-span-meta');
    if (!hud || !metaEl) return;

    if (_spanState.anchor === null) {
        hud.hidden = true;
        return;
    }

    hud.hidden = false;
    var a = _spanState.anchor;
    var b = _spanState.end !== null ? _spanState.end : a;

    if (_spanState.dragging) {
        metaEl.textContent = 'در حال اندازه‌گیری…';
    } else if (_spanState.locked) {
        var a1 = Math.min(a, b), b1 = Math.max(a, b);
        metaEl.textContent = _spanFormatClock(a1) + ' → ' + _spanFormatClock(b1);
    } else {
        metaEl.textContent = 'نقطه اول را بگیر و بکش';
    }
}

function _spanEpochFromClientX(clientX) {
    if (!progressiveChart) return null;
    var xScale = progressiveChart.scales['x-axis-0'];
    if (!xScale) return null;
    var canvas = progressiveChart.canvas;
    if (!canvas) return null;
    var rect = canvas.getBoundingClientRect();
    var x = clientX - rect.left;
    if (x < 0) x = 0;
    if (x > rect.width) x = rect.width;
    var ts = xScale.getValueForPixel(x);
    if (ts === undefined || ts === null || isNaN(ts)) return null;
    return ts;
}

function _spanSetEnabled(on) {
    _spanState.enabled = !!on;
    if (!on) {
        _spanClear();
    }
    var btn = document.getElementById('dpc-span-tool');
    if (btn) btn.classList.toggle('is-on', _spanState.enabled);
    var root = document.getElementById('dpc-chart-root');
    if (root) root.classList.toggle('is-span', _spanState.enabled);
    var canvas = document.getElementById('dpc-canvas');
    if (canvas) canvas.classList.toggle('is-span', _spanState.enabled);

    // Disable pan/wheel zoom interference while measuring
    if (progressiveChart && progressiveChart.options && progressiveChart.options.plugins && progressiveChart.options.plugins.zoom) {
        var z = progressiveChart.options.plugins.zoom;
        if (z.pan) z.pan.enabled = !_spanState.enabled;
        if (z.zoom) z.zoom.enabled = !_spanState.enabled;
    }
    if (_spanState.enabled) {
        _spanUpdateHud();
    }
    return _spanState.enabled;
}

function _spanBindPointer(chart) {
    if (!chart || !chart.canvas) return;
    var canvas = chart.canvas;
    if (canvas._spanBound) return;
    canvas._spanBound = true;

    function onDown(e) {
        if (!_spanState.enabled) return;
        if (e.button !== undefined && e.button !== 0) return;
        e.preventDefault();
        e.stopPropagation();
        var clientX = e.clientX;
        if (clientX === undefined && e.touches && e.touches[0]) clientX = e.touches[0].clientX;
        if (clientX === undefined) return;
        var ts = _spanEpochFromClientX(clientX);
        if (ts === null) return;
        _spanState.anchor = ts;
        _spanState.end = ts;
        _spanState.dragging = true;
        _spanState.locked = false;
        _spanUpdateHud();
        try { progressiveChart.draw(); } catch (err) {}
    }

    function onMove(e) {
        if (!_spanState.enabled || !_spanState.dragging) return;
        e.preventDefault();
        e.stopPropagation();
        var clientX = e.clientX;
        if (clientX === undefined && e.touches && e.touches[0]) clientX = e.touches[0].clientX;
        if (clientX === undefined && e.changedTouches && e.changedTouches[0]) clientX = e.changedTouches[0].clientX;
        if (clientX === undefined) return;
        var ts = _spanEpochFromClientX(clientX);
        if (ts === null) return;
        _spanState.end = ts;
        _spanUpdateHud();
        try { progressiveChart.draw(); } catch (err) {}
    }

    function onUp(e) {
        if (!_spanState.enabled || !_spanState.dragging) return;
        e.preventDefault();
        e.stopPropagation();
        var clientX = e.clientX;
        if (clientX === undefined && e.changedTouches && e.changedTouches[0]) clientX = e.changedTouches[0].clientX;
        if (clientX !== undefined) {
            var ts = _spanEpochFromClientX(clientX);
            if (ts !== null) _spanState.end = ts;
        }
        _spanState.dragging = false;
        _spanState.locked = true;
        _spanUpdateHud();
        try { progressiveChart.draw(); } catch (err) {}
    }

    // Mouse
    canvas.addEventListener('mousedown', onDown, true);
    window.addEventListener('mousemove', onMove, true);
    window.addEventListener('mouseup', onUp, true);
    // Touch
    canvas.addEventListener('touchstart', onDown, { capture: true, passive: false });
    window.addEventListener('touchmove', onMove, { capture: true, passive: false });
    window.addEventListener('touchend', onUp, { capture: true, passive: false });
    window.addEventListener('touchcancel', onUp, { capture: true, passive: false });
}

// Draw anchor line, live line, shaded band, and duration chip
Chart.plugins.register({
    id: 'prog-span',
    afterDatasetsDraw: function (chart) {
        if (chart !== progressiveChart) return;
        if (_spanState.anchor === null) return;
        var xScale = chart.scales['x-axis-0'];
        var area = chart.chartArea;
        if (!xScale || !area) return;

        var a = _spanState.anchor;
        var b = _spanState.end !== null ? _spanState.end : a;
        var pa = xScale.getPixelForValue(Math.min(a, b));
        var pb = xScale.getPixelForValue(Math.max(a, b));
        if (pa === undefined || pb === undefined || isNaN(pa) || isNaN(pb)) return;

        var ctx = chart.ctx;
        var top = area.top;
        var bottom = area.bottom;
        var left = Math.min(pa, pb);
        var right = Math.max(pa, pb);
        if (right - left < 2) right = left + 2;

        ctx.save();

        // Shaded band
        ctx.fillStyle = 'rgba(251, 191, 36, 0.14)';
        ctx.fillRect(left, top, right - left, bottom - top);

        // Anchor (first) line — solid amber
        ctx.strokeStyle = '#f59e0b';
        ctx.lineWidth = 2;
        ctx.setLineDash([]);
        ctx.beginPath();
        ctx.moveTo(pa, top);
        ctx.lineTo(pa, bottom);
        ctx.stroke();

        // End (live) line — dashed blue while dragging, solid amber when locked
        ctx.strokeStyle = _spanState.dragging ? '#38bdf8' : '#f59e0b';
        ctx.lineWidth = 2;
        ctx.setLineDash(_spanState.dragging ? [6, 4] : [4, 3]);
        ctx.beginPath();
        ctx.moveTo(pb, top);
        ctx.lineTo(pb, bottom);
        ctx.stroke();
        ctx.setLineDash([]);

        // Endpoint dots
        ctx.fillStyle = '#f59e0b';
        [pa, pb].forEach(function (px) {
            ctx.beginPath();
            ctx.arc(px, top + 6, 4, 0, Math.PI * 2);
            ctx.fill();
        });

        // Duration chip centered in the band
        var delta = b - a;
        var label = _spanFormatDuration(delta);
        var canRtl = ('direction' in ctx);
        if (canRtl) ctx.direction = 'rtl';
        ctx.font = '800 13px Vazirmatn, sans-serif';
        var tw = ctx.measureText(label).width;
        var padX = 12, padY = 6;
        var bw = tw + padX * 2;
        var bh = 26;
        var cx = (left + right) / 2;
        var bx = cx - bw / 2;
        var by = top + 14;
        if (bx < area.left) bx = area.left;
        if (bx + bw > area.right) bx = area.right - bw;

        ctx.fillStyle = 'rgba(15, 23, 42, 0.92)';
        ctx.strokeStyle = 'rgba(251, 191, 36, 0.7)';
        ctx.lineWidth = 1;
        var r = 8;
        ctx.beginPath();
        ctx.moveTo(bx + r, by);
        ctx.lineTo(bx + bw - r, by);
        ctx.quadraticCurveTo(bx + bw, by, bx + bw, by + r);
        ctx.lineTo(bx + bw, by + bh - r);
        ctx.quadraticCurveTo(bx + bw, by + bh, bx + bw - r, by + bh);
        ctx.lineTo(bx + r, by + bh);
        ctx.quadraticCurveTo(bx, by + bh, bx, by + bh - r);
        ctx.lineTo(bx, by + r);
        ctx.quadraticCurveTo(bx, by, bx + r, by);
        ctx.closePath();
        ctx.fill();
        ctx.stroke();

        ctx.fillStyle = '#fcd34d';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(label, bx + bw / 2, by + bh / 2 + 1);
        if (canRtl) ctx.direction = 'ltr';

        ctx.restore();
    }
});

// ── Cycle labels plugin ──
Chart.plugins.register({
    id: 'prog-cycle-labels',
    afterDatasetsDraw: function(chart) {
        if (chart !== progressiveChart) return;
        var cycles = _progState.cycles;
        if (!cycles) return;
        var xScale = chart.scales['x-axis-0'];
        var yEq = chart.scales['y-eq'];
        if (!xScale || !yEq) return;

        var params = [
            { key: 'power', dsIdx: 2, bandY: 0.95, color: '#eab308', textColor: '#ffffff' },
            { key: 'motor', dsIdx: 3, bandY: 0.85, color: '#10b981' },
            { key: 'heater1', dsIdx: 4, bandY: 0.75, color: '#f97316' },
            { key: 'heater2', dsIdx: 5, bandY: 0.35, color: '#ef4444' }
        ];

        var ctx = chart.ctx;
        ctx.save();

        for (var pi = 0; pi < params.length; pi++) {
            var p = params[pi];
            if (chart.data.datasets[p.dsIdx] && chart.data.datasets[p.dsIdx].hidden) continue;
            var paramCycles = cycles[p.key];
            if (!paramCycles || !paramCycles.length) continue;

            for (var ci = 0; ci < paramCycles.length; ci++) {
                var c = paramCycles[ci];
                var ts = _progState.timestamps;
                var startTs = ts[c.startIndex], endTs = ts[c.endIndex];
                if (startTs === undefined || endTs === undefined) continue;
                var midX = xScale.getPixelForValue((startTs + endTs) / 2);
                var y = yEq.getPixelForValue(p.bandY);
                if (midX === undefined || y === undefined || isNaN(midX) || isNaN(y)) continue;

                ctx.save();
                ctx.translate(midX, y);
                ctx.rotate(-Math.PI / 2);

                ctx.font = 'bold 10px Vazirmatn, Consolas, sans-serif';
                var tw = c.duration.length * 6.5;
                var padX = 5, padY = 3;
                var bw = tw + padX * 2;
                var bh = 12 + padY * 2;
                var bx = -bw / 2, by = -bh / 2;

                ctx.fillStyle = p.color;
                ctx.beginPath();
                ctx.moveTo(bx + 4, by);
                ctx.lineTo(bx + bw - 4, by);
                ctx.quadraticCurveTo(bx + bw, by, bx + bw, by + 4);
                ctx.lineTo(bx + bw, by + bh - 4);
                ctx.quadraticCurveTo(bx + bw, by + bh, bx + bw - 4, by + bh);
                ctx.lineTo(bx + 4, by + bh);
                ctx.quadraticCurveTo(bx, by + bh, bx, by + bh - 4);
                ctx.lineTo(bx, by + 4);
                ctx.quadraticCurveTo(bx, by, bx + 4, by);
                ctx.closePath();
                ctx.fill();

                ctx.fillStyle = p.textColor || '#1e293b';
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillText(c.duration, 0, 0);
                ctx.restore();
            }
        }

        ctx.restore();
    }
});

// ── Crosshair ──
var _progChX = null;
var _progChCache = { total: 0, step: 0, x: 0, bestIdx: 0, timestamp: 0 };

Chart.plugins.register({
    id: 'prog-crosshair',
    beforeDraw: function(chart) {
        if (chart !== progressiveChart) return;
        // Draw 0°C reference line on y-temp axis
        if (!chart.scales['y-temp']) return;
        var xScale = chart.scales['x-axis-0'];
        if (!xScale) return;
        var yScale = chart.scales['y-temp'];
        var y0 = yScale.getPixelForValue(0);
        if (y0 === undefined || y0 === null) return;
        var left = xScale.left, right = xScale.right;
        var ctx = chart.ctx;
        ctx.save();
        ctx.beginPath();
        ctx.setLineDash([5, 5]);
        ctx.moveTo(left, y0);
        ctx.lineTo(right, y0);
        ctx.lineWidth = 1.5;
        ctx.strokeStyle = 'rgba(100, 116, 139, 0.35)';
        ctx.stroke();
        // Label at right end
        ctx.setLineDash([]);
        ctx.font = '10px Vazirmatn, sans-serif';
        ctx.fillStyle = 'rgba(100, 116, 139, 0.5)';
        ctx.textAlign = 'right';
        ctx.textBaseline = 'bottom';
        ctx.fillText('0°C', right - 4, y0 - 3);
        ctx.restore();
    },
    afterDatasetsDraw: function(chart) {
        if (chart !== progressiveChart || _progChX === null) return;
        var xScale = chart.scales['x-axis-0'];
        if (!xScale) return;

        var yAxis = null;
        var keys = Object.keys(chart.scales || {});
        for (var k = 0; k < keys.length; k++) {
            var s = chart.scales[keys[k]];
            if (s && s.isHorizontal && !s.isHorizontal()) { yAxis = s; break; }
        }
        if (!yAxis) return;
        var topY = yAxis.top, bottomY = yAxis.bottom;
        if (topY === undefined || bottomY === undefined) return;

        var dataLen = chart.data.datasets[0] ? chart.data.datasets[0].data.length : 0;
        if (dataLen === 0) return;

        var step = Math.max(1, Math.floor(dataLen / 300));

        var now = Date.now();
        if (_progChCache.total !== dataLen || _progChCache.step !== step || Math.abs(_progChX - (_progChCache.x || 0)) > 2 || now - _progChCache.timestamp > 100) {
            var bestIdx = 0, bestDist = Infinity;
            for (var i = 0; i < dataLen; i += step) {
                var ts = _progState.timestamps[i];
                if (ts === undefined) continue;
                var px = xScale.getPixelForValue(ts);
                if (px === undefined || px === null) continue;
                var d = Math.abs(px - _progChX);
                if (d < bestDist) { bestDist = d; bestIdx = i; }
            }
            var rs = Math.max(0, bestIdx - step);
            var re = Math.min(dataLen - 1, bestIdx + step);
            for (var i = rs; i <= re; i++) {
                var ts = _progState.timestamps[i];
                if (ts === undefined) continue;
                var px = xScale.getPixelForValue(ts);
                if (px === undefined || px === null) continue;
                var d = Math.abs(px - _progChX);
                if (d < bestDist) { bestDist = d; bestIdx = i; }
            }
            _progChCache = { x: _progChX, bestIdx: bestIdx, step: step, total: dataLen, timestamp: now };
        }

        var bestIdx = _progChCache.bestIdx;
        var bestTs = _progState.timestamps[bestIdx];
        if (bestTs === undefined) return;
        var x = xScale.getPixelForValue(bestTs);
        var ctx = chart.ctx;
        ctx.save();

        ctx.beginPath();
        ctx.setLineDash([]);
        ctx.moveTo(x, topY);
        ctx.lineTo(x, bottomY);
        ctx.lineWidth = 2;
        ctx.strokeStyle = 'rgba(71, 85, 105, 0.92)';
        ctx.stroke();

        for (var i = 0; i < chart.data.datasets.length; i++) {
            var ds = chart.data.datasets[i];
            if (ds.hidden) continue;
            var meta = chart.getDatasetMeta(i);
            if (!meta || !meta.data || !meta.data[bestIdx]) continue;
            var pt = meta.data[bestIdx];
            if (!pt._model || pt._model.skip) continue;
            ctx.beginPath();
            ctx.arc(pt._model.x, pt._model.y, 4.5, 0, Math.PI * 2);
            ctx.fillStyle = pt._model.borderColor || ds.borderColor;
            ctx.fill();
            ctx.strokeStyle = '#fff';
            ctx.lineWidth = 2;
            ctx.stroke();
        }

        ctx.restore();
    }
});

function _progBindCrosshair(chart) {
    if (!chart || !chart.canvas) return;
    var lastDraw = 0, touchId = false;
    var scrollEl = document.getElementById('dpc-scroll');
    function isDragging() { return scrollEl && scrollEl.dataset._progDrag === '1'; }
    function onMove(e) {
        if (touchId || isDragging()) { _progChX = null; return; }
        var rect = chart.canvas.getBoundingClientRect();
        _progChX = e.clientX - rect.left;
        var now = Date.now();
        if (now - lastDraw > 40) { lastDraw = now; chart.draw(); }
    }
    function onLeave() { _progChX = null; chart.draw(); }
    function onTouchStart() { touchId = true; _progChX = null; }
    function onTouchEnd() { touchId = false; _progChX = null; chart.draw(); }
    function onTouchMove() { _progChX = null; }
    chart.canvas.addEventListener('mousemove', onMove);
    chart.canvas.addEventListener('mouseleave', onLeave);
    chart.canvas.addEventListener('touchstart', onTouchStart, { passive: true });
    chart.canvas.addEventListener('touchmove', onTouchMove, { passive: true });
    chart.canvas.addEventListener('touchend', onTouchEnd, { passive: true });
}

// ── Custom Ctrl+Drag zoom (replaces plugin drag which lacks modifierKey) ──
function _progSetupCtrlDragZoom(chart) {
    if (!chart || !chart.canvas) return;
    var canvas = chart.canvas;
    var wrapper = canvas.parentElement;
    if (!wrapper) return;

    var overlay = document.createElement('div');
    overlay.style.cssText = 'position:absolute;display:none;border:2px dashed #2563eb;background:rgba(37,99,235,0.12);pointer-events:none;z-index:10;top:0;bottom:0;';
    wrapper.appendChild(overlay);

    var startX = 0;
    var isDragging = false;

    canvas.addEventListener('mousedown', function (e) {
        if (_spanState.enabled) return;
        if (!e.ctrlKey || e.button !== 0) return;
        e.preventDefault();
        e.stopPropagation();

        var rect = canvas.getBoundingClientRect();
        startX = e.clientX - rect.left;
        isDragging = true;

        overlay.style.display = 'block';
        overlay.style.left = startX + 'px';
        overlay.style.width = '0px';
    });

    document.addEventListener('mousemove', function (e) {
        if (!isDragging) return;
        var rect = canvas.getBoundingClientRect();
        var curX = Math.max(0, Math.min(rect.width, e.clientX - rect.left));
        var left = Math.min(startX, curX);
        var w = Math.abs(curX - startX);
        overlay.style.left = left + 'px';
        overlay.style.width = w + 'px';
    });

    document.addEventListener('mouseup', function (e) {
        try {
        if (!isDragging) return;
        isDragging = false;
        overlay.style.display = 'none';

        var rect = canvas.getBoundingClientRect();
        var endX = Math.max(0, Math.min(rect.width, e.clientX - rect.left));

        var xScale = chart.scales['x-axis-0'];
        if (!xScale) return;

        var px1 = Math.min(startX, endX);
        var px2 = Math.max(startX, endX);
        if (px2 - px1 < 5) return;

        var ts1 = xScale.getValueForPixel(px1);
        var ts2 = xScale.getValueForPixel(px2);
        if (ts1 === undefined || ts2 === undefined || ts1 >= ts2) return;

        // Apply zoom by setting scale min/max
        xScale.options.ticks.min = ts1;
        xScale.options.ticks.max = ts2;
        try { chart.update(0); } catch (e) {}

        _updateZoomRatio();
        _progOnZoomEnd();
        } catch (err) { console.error('dpc drag-zoom error:', err); }
    });
}

// ── Band separator lines ──
Chart.plugins.register({
    afterDraw: function(chart) {
        if (chart !== progressiveChart) return;
        var yEq = chart.scales['y-eq'];
        if (!yEq) return;
        var ctx = chart.ctx;
        ctx.save();
        ctx.setLineDash([3, 4]);
        ctx.strokeStyle = 'rgba(148, 163, 184, 0.18)';
        ctx.lineWidth = 1;
        [0.35, 0.65, 0.85].forEach(function(v) {
            var y = yEq.getPixelForValue(v);
            if (y === undefined || y === null) return;
            ctx.beginPath();
            ctx.moveTo(yEq.left, y);
            ctx.lineTo(yEq.right, y);
            ctx.stroke();
        });
        ctx.restore();
    }
});

// ── Scroll setup ──
function _progSetupScroll() {
    var el = document.getElementById('dpc-scroll');
    if (!el || el.dataset._progScroll === '1') return;
    el.dataset._progScroll = '1';

    var _clampTimer = null;
    function clampScroll() {
        var max = el.scrollWidth - el.clientWidth;
        if (max <= 0) { el.scrollLeft = 0; return; }
        if (el.scrollLeft < 0) el.scrollLeft = 0;
        if (el.scrollLeft > max) el.scrollLeft = max;
    }
    // Debounced clamp — only runs 100ms after last scroll event
    function debouncedClamp() {
        if (_clampTimer) clearTimeout(_clampTimer);
        _clampTimer = setTimeout(clampScroll, 16);
    }

    var isDown = false, startX = 0, startLeft = 0, dragged = false;
    var threshold = 3;

    function down(x) { if (_spanState.enabled) return; isDown = true; dragged = false; startX = x; startLeft = el.scrollLeft; el.dataset._progDrag = '0'; }
    function move(x) {
        if (_spanState.enabled) { isDown = false; return; }
        if (!isDown) return;
        var dx = startX - x;
        if (Math.abs(dx) > threshold) { dragged = true; el.dataset._progDrag = '1'; }
        if (dragged) { el.scrollLeft = Math.max(0, Math.min(el.scrollWidth - el.clientWidth, startLeft + dx)); }
    }
    function up() { isDown = false; el.dataset._progDrag = '0'; }

    // ── Mouse events ──
    el.addEventListener('mousedown', function (e) { if (_spanState.enabled) return; if (e.button !== 0) return; down(e.clientX); });
    el.addEventListener('mousemove', function (e) { move(e.clientX); });
    document.addEventListener('mouseup', function () { if (dragged) { up(); } else { isDown = false; } });
    el.addEventListener('click', function (e) { if (dragged) { e.stopPropagation(); e.preventDefault(); } }, true);

    // ── Touch events ──
    el.addEventListener('touchstart', function (e) { if (_spanState.enabled) return; if (e.touches.length !== 1) return; down(e.touches[0].clientX); }, { passive: true });
    el.addEventListener('touchmove', function (e) { if (_spanState.enabled) return; if (e.touches.length !== 1) return; move(e.touches[0].clientX); }, { passive: true });
    el.addEventListener('touchend', up, { passive: true });

    // ── Wheel handler — smooth rAF-accumulated scrolling ──
    var _wheelPending = 0;
    var _wheelRafId = null;
    el.addEventListener('wheel', function (e) {
        // Only horizontal scroll
        var delta = (Math.abs(e.deltaX) > Math.abs(e.deltaY)) ? e.deltaX : e.deltaY;
        if (Math.abs(delta) < 1) return;
        _wheelPending += delta;
        if (!_wheelRafId) {
            _wheelRafId = requestAnimationFrame(function flush() {
                if (Math.abs(_wheelPending) < 0.5) { _wheelPending = 0; _wheelRafId = null; return; }
                var max = el.scrollWidth - el.clientWidth;
                var prev = el.scrollLeft;
                el.scrollLeft = Math.max(0, Math.min(max, prev + _wheelPending));
                var consumed = el.scrollLeft - prev;
                _wheelPending -= consumed;
                if (Math.abs(_wheelPending) > 0.5) {
                    _wheelRafId = requestAnimationFrame(flush);
                } else {
                    _wheelPending = 0;
                    _wheelRafId = null;
                }
            });
        }
        e.preventDefault();
    }, { passive: false });

    // ── Scroll end detection ──
    el.addEventListener('scroll', function () {
        debouncedClamp();
        if (_progState.mode !== 'progressive' || _progState.isLoading) return;
        var max = el.scrollWidth - el.clientWidth;
        if (max > 0 && el.scrollLeft / max > 0.88 && _progState.currentSegment < _progState.segments.length - 1) {
            _loadSegment(_progState.currentSegment + 1);
        }
    });
}

// ── Custom tooltip positioner ──
Chart.Tooltip.positioners.progTop = function(elements, eventPosition) {
    if (!elements || !elements.length) return false;
    var chart = elements[0]._chart;
    var ca = chart.chartArea;
    if (!ca) return false;
    var mx = eventPosition ? eventPosition.x : elements[0]._view.x;
    if (mx === undefined || mx === null) return false;

    var xScale = chart.scales['x-axis-0'];
    var x = mx;
    if (xScale) {
        var dataLen = chart.data.datasets[0] ? chart.data.datasets[0].data.length : 0;
        var ts = _progState.timestamps;
        if (dataLen > 0 && ts && ts.length > 0) {
            var bestD = Infinity, bestI = 0;
            var step = Math.max(1, Math.floor(dataLen / 300));
            for (var i = 0; i < dataLen; i += step) {
                if (ts[i] === undefined) continue;
                var px = xScale.getPixelForValue(ts[i]);
                if (px === undefined) continue;
                var d = Math.abs(px - mx);
                if (d < bestD) { bestD = d; bestI = i; }
            }
            var rs = Math.max(0, bestI - step);
            var re = Math.min(dataLen - 1, bestI + step);
            for (var i = rs; i <= re; i++) {
                if (ts[i] === undefined) continue;
                var px = xScale.getPixelForValue(ts[i]);
                if (px === undefined) continue;
                var d = Math.abs(px - mx);
                if (d < bestD) { bestD = d; bestI = i; }
            }
            x = xScale.getPixelForValue(ts[bestI]);
        }
    }
    return { x: x, y: ca.top + 6 };
};

// ── Gradient fills (built once) ──
var _progGradients = [null, null];

// ── Fetch data from server ──
function _progFetchData(monitoringId, fromTime, toTime, maxPoints) {
    if (_progState._abortController) _progState._abortController.abort();

    var params = new URLSearchParams();
    var requestedMaxPoints = maxPoints === undefined || maxPoints === null ? 3000 : maxPoints;
    params.set('maxPoints', String(requestedMaxPoints));
    if (fromTime) params.set('fromTime', fromTime);
    if (toTime) params.set('toTime', toTime);

    // Collect note indices from current chart data
    var noteIdx = [];
    for (var idxStr in _progNotes) {
        var idx = parseInt(idxStr, 10);
        if (!isNaN(idx) && idx >= 0) noteIdx.push(idx);
    }
    if (noteIdx.length > 0) {
        params.set('noteIndices', noteIdx.join(','));
    }

    var key = monitoringId + '|' + (fromTime || '') + '|' + (toTime || '') + '|' + String(requestedMaxPoints);
    // Skip if same as last fetch (user already has this data)
    if (key === _progState._lastFetchKey) return Promise.reject(new Error('already loaded'));

    var requestSeq = ++_progState._requestSeq;
    _progState._abortController = new AbortController();
    _progState.isLoading = true;
    _progState._lastFetchKey = key;
    _progState.pendingRange = fromTime && toTime
        ? { startTime: _tsToEpoch(fromTime), endTime: _tsToEpoch(toTime) }
        : { startTime: null, endTime: null };
    _notifyLoading(true);

    return fetch('/api/monitoring/chart/' + monitoringId + '/progressive-records?' + params.toString(), {
        signal: _progState._abortController.signal
    })
        .then(function (r) {
            if (!r.ok) throw new Error('HTTP ' + r.status);
            return r.json();
        })
        .then(function (data) {
            if (requestSeq !== _progState._requestSeq) throw new Error('stale request');
            _progState.isLoading = false;
            _progState._abortController = null;
            _notifyLoading(false);
            return data;
        })
        .catch(function (err) {
            if (requestSeq === _progState._requestSeq) {
                _progState.isLoading = false;
                _progState._abortController = null;
                _notifyLoading(false);
            }
            throw err;
        });
}

function _notifyLoading(isLoading) {
    var el = document.getElementById('dpc-chart-loading');
    if (el) el.style.display = isLoading ? 'flex' : 'none';
    var root = document.getElementById('dpc-chart-root');
    var scrollEl = document.getElementById('dpc-scroll');
    if (root) root.classList.toggle('dpc-chart-root--loading', isLoading);
    if (scrollEl) {
        scrollEl.classList.toggle('dpc-chart-scroll--locked', isLoading);
        scrollEl.style.pointerEvents = isLoading ? 'none' : '';
    }
    _updateSegmentStatus();
}

function _mobileRangeLabel(epoch) {
    if (epoch === null || epoch === undefined) return '—';
    try {
        return new Intl.DateTimeFormat('fa-IR-u-ca-persian-nu-latn', {
            month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23'
        }).format(new Date(epoch));
    } catch (e) { return _epochToTimeStr(epoch); }
}

function _syncMobileRangeSelector() {
    var root = document.getElementById('dpc-range-selector');
    if (!root || !_progState.fullRange.startTime || !_progState.fullRange.endTime) return;
    var start = _progState.fullRange.startTime;
    var end = _progState.fullRange.endTime;
    var duration = end - start;
    var fromLabel = root.querySelector('[data-range-from-label]');
    var toLabel = root.querySelector('[data-range-to-label]');
    var fill = root.querySelector('[data-range-fill]');
    if (!fill || duration <= 0) return;
    var currentStart = _progState.pendingRange.startTime !== null ? _progState.pendingRange.startTime : start;
    var currentEnd = _progState.pendingRange.endTime !== null ? _progState.pendingRange.endTime : end;
    var a = Math.max(0, Math.min(100, ((currentStart - start) / duration) * 100));
    var b = Math.max(0, Math.min(100, ((currentEnd - start) / duration) * 100));
    root.dataset.rangeStart = String(a * 10);
    root.dataset.rangeEnd = String(b * 10);
    fill.style.left = a + '%';
    fill.style.width = Math.max(0, b - a) + '%';
    if (fromLabel) fromLabel.textContent = _mobileRangeLabel(currentStart);
    if (toLabel) toLabel.textContent = _mobileRangeLabel(currentEnd);
}

function _applyMobileRangeSelector() {
    var root = document.getElementById('dpc-range-selector');
    if (!root || !_progState.fullRange.startTime || !_progState.fullRange.endTime || _progState.isLoading) return;
    if (_liveState.enabled) return;
    var a = Math.min(parseFloat(root.dataset.rangeStart || '0'), parseFloat(root.dataset.rangeEnd || '1000'));
    var b = Math.max(parseFloat(root.dataset.rangeStart || '0'), parseFloat(root.dataset.rangeEnd || '1000'));
    if (b - a < 5) return;
    var start = _progState.fullRange.startTime;
    var end = _progState.fullRange.endTime;
    var duration = end - start;
    var from = new Date(start + duration * a / 1000).toISOString();
    var to = new Date(start + duration * b / 1000).toISOString();
    _progState.currentRange.startTime = from;
    _progState.currentRange.endTime = to;
    _progFetchData(_progState.monitoringId, from, to, window.innerWidth < 768 ? 1400 : 2500)
        .then(_updateChartData)
        .catch(function (err) { if (err && err.name !== 'AbortError' && err.message !== 'stale request') console.warn(err); });
}

function _updateProgressiveModeUi() {
    var overview = document.getElementById('dpc-mode-overview');
    var progressive = document.getElementById('dpc-mode-progressive');
    var nav = document.getElementById('dpc-progressive-nav');
    if (overview) overview.classList.toggle('is-active', _progState.mode === 'overview');
    if (progressive) progressive.classList.toggle('is-active', _progState.mode === 'progressive');
    if (nav) nav.hidden = _progState.mode !== 'progressive';
    var range = document.getElementById('dpc-range-selector');
    if (range) range.classList.toggle('dpc-range-selector--hidden', _progState.mode === 'progressive');
}

function _updateSegmentStatus() {
    var status = document.getElementById('dpc-segment-status');
    var prev = document.getElementById('dpc-segment-prev');
    var next = document.getElementById('dpc-segment-next');
    if (!status) return;
    var current = _progState.currentSegment;
    var segment = current >= 0 ? _progState.segments[current] : null;
    var fa = function (n) { return (n || 0).toLocaleString('fa-IR'); };
    if (segment) {
        var loaded = segment.recordCount != null ? segment.recordCount : 0;
        var total = _progState.totalRecords || _progState.baseTotal || 0;
        var rangeChunk = '<span class="dpc-seg-chunk">بازه ' + fa(current + 1) + ' از ' + fa(_progState.segments.length) + '</span>';
        var dot = '<span class="dpc-seg-dot">·</span>';
        var recordsChunk = total > 0
            ? '<span class="dpc-seg-chunk dpc-seg-chunk--records">' + fa(loaded) + ' رکورد از ' + fa(total) + ' رکورد</span>'
            : '<span class="dpc-seg-chunk dpc-seg-chunk--records">' + fa(loaded) + ' رکورد</span>';
        status.innerHTML = rangeChunk + dot + recordsChunk;
    } else {
        status.textContent = 'در حال آماده‌سازی بازه‌ها…';
    }
    if (prev) prev.disabled = current <= 0 || _progState.isLoading;
    if (next) next.disabled = current < 0 || current >= _progState.segments.length - 1 || _progState.isLoading;
}

function _loadSegment(index) {
    if (_progState.mode !== 'progressive' || index < 0 || index >= _progState.segments.length) return Promise.resolve();
    var segment = _progState.segments[index];
    var cacheKey = _progState.monitoringId + '|' + index;
    _progState.currentSegment = index;
    _updateSegmentStatus();
    if (_progState.segmentCache[cacheKey]) {
        _progState.pendingRange = { startTime: _tsToEpoch(segment.from), endTime: _tsToEpoch(segment.to) };
        _updateChartData(_progState.segmentCache[cacheKey]);
        _updateSegmentStatus();
        return Promise.resolve();
    }
    return _progFetchData(_progState.monitoringId, segment.from, segment.to, 0)
        .then(function (data) {
            _progState.segmentCache[cacheKey] = data;
            _updateChartData(data);
            _updateSegmentStatus();
        })
        .catch(function (err) {
            _updateSegmentStatus();
            if (err && err.name !== 'AbortError' && err.message !== 'stale request') throw err;
        });
}

function _loadProgressiveSegments() {
    _notifyLoading(true);
    _updateSegmentStatus();
    return fetch('/api/monitoring/chart/' + _progState.monitoringId + '/segments?hours=12')
        .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })
        .then(function (data) {
            _progState.segments = data.segments || [];
            _progState.segmentCache = {};
            _progState.totalRecords = data.totalCount || data.TotalCount || _progState.baseTotal || 0;
            if (!_progState.segments.length) throw new Error('برای این مانیتور بازه‌ای پیدا نشد');
            _updateSegmentStatus();
            return _loadSegment(0);
        })
        .finally(function () { _notifyLoading(false); _updateSegmentStatus(); });
}

function _setChartMode(mode) {
    try {
    if (mode !== 'overview' && mode !== 'progressive') return;
    if (_progState.mode === mode && (mode !== 'progressive' || _progState.segments.length)) return;

    if (mode === 'progressive') {
        _liveStop();
        var liveToggle = document.getElementById('dpc-live-toggle');
        if (liveToggle) liveToggle.checked = false;
    }

    if (_progState.isLoading && _progState._abortController) {
        try { _progState._abortController.abort(); } catch (e) {}
        _progState._abortController = null;
        _progState.isLoading = false;
        _notifyLoading(false);
    }

    _progState.mode = mode;
    _updateProgressiveModeUi();
    if (mode === 'progressive') return _loadProgressiveSegments();
    _progState.currentSegment = -1;
    _updateSegmentStatus();
    return _progResetZoom();
    } catch (err) { console.error('dpc setChartMode error:', err); _notifyLoading(false); }
}

function _ensureMobileRangeSelector(container) {
    var root = document.getElementById('dpc-range-selector');
    if (root) return root;
    root = document.createElement('div');
    root.id = 'dpc-range-selector';
    root.className = 'dpc-range-selector';
    root.setAttribute('dir', 'rtl');
    root.innerHTML =
        '<div class="dpc-range-selector__head"><strong>انتخاب محدوده برای Zoom</strong><span>روی نوار بکشید</span></div>' +
        '<div class="dpc-range-selector__labels"><span data-range-from-label>—</span><span data-range-to-label>—</span></div>' +
        '<div class="dpc-range-selector__track" data-range-track role="slider" aria-label="انتخاب بازه زمانی">' +
            '<span class="dpc-range-selector__fill" data-range-fill></span>' +
        '</div>';
    container.appendChild(root);
    root.dataset.rangeStart = '0';
    root.dataset.rangeEnd = '1000';
    var track = root.querySelector('[data-range-track]');
    var dragging = false;
    var startPercent = 0;
    function percentFromEvent(e) {
        var rect = track.getBoundingClientRect();
        return Math.max(0, Math.min(1000, ((e.clientX - rect.left) / rect.width) * 1000));
    }
    function renderSelection(a, b) {
        var low = Math.min(a, b), high = Math.max(a, b);
        root.dataset.rangeStart = String(low);
        root.dataset.rangeEnd = String(high);
        var fill = root.querySelector('[data-range-fill]');
        if (fill) { fill.style.left = (low / 10) + '%'; fill.style.width = ((high - low) / 10) + '%'; }
        var start = _progState.fullRange.startTime, end = _progState.fullRange.endTime;
        if (start && end) {
            var duration = end - start;
            root.querySelector('[data-range-from-label]').textContent = _mobileRangeLabel(start + duration * low / 1000);
            root.querySelector('[data-range-to-label]').textContent = _mobileRangeLabel(start + duration * high / 1000);
        }
    }
    track.addEventListener('pointerdown', function (e) {
        if (_progState.isLoading || _progState.mode !== 'overview') return;
        dragging = true; startPercent = percentFromEvent(e); renderSelection(startPercent, startPercent);
        track.setPointerCapture(e.pointerId); e.preventDefault();
    });
    track.addEventListener('pointermove', function (e) { if (dragging) { renderSelection(startPercent, percentFromEvent(e)); e.preventDefault(); } });
    track.addEventListener('pointerup', function (e) {
        if (!dragging) return; dragging = false; track.releasePointerCapture(e.pointerId); _applyMobileRangeSelector();
    });
    track.addEventListener('pointercancel', function () { dragging = false; });
    return root;
}

function _ensureLoadingOverlay(container) {
    var overlay = document.getElementById('dpc-chart-loading');
    if (overlay) return overlay;

    overlay = document.createElement('div');
    overlay.id = 'dpc-chart-loading';
    overlay.className = 'dpc-chart-loading';
    overlay.setAttribute('role', 'status');
    overlay.setAttribute('aria-live', 'polite');
    overlay.innerHTML =
        '<div class="dpc-chart-loading__card">' +
            '<div class="dpc-chart-loading__icon"><span></span></div>' +
            '<div class="dpc-chart-loading__copy" dir="rtl">' +
                '<strong>در حال دریافت جزئیات</strong>' +
                '<small>لطفاً چند لحظه منتظر بمانید…</small>' +
            '</div>' +
            '<div class="dpc-chart-loading__dots"><i></i><i></i><i></i></div>' +
        '</div>';
    container.appendChild(overlay);
    return overlay;
}

// ── Handle zoom / pan end ──
function _progOnZoomEnd() {
    try {
    if (!progressiveChart || _progState.isLoading) return;

    var xScale = progressiveChart.scales['x-axis-0'];
    if (!xScale) return;

    var ca = progressiveChart.chartArea;
    if (!ca) return;

    var leftTs = xScale.getValueForPixel(ca.left);
    var rightTs = xScale.getValueForPixel(ca.right);

    if (leftTs === undefined || rightTs === undefined || leftTs >= rightTs) return;

    var visibleFrom = new Date(leftTs).toISOString();
    var visibleTo = new Date(rightTs).toISOString();

    _progState.currentRange.startTime = visibleFrom;
    _progState.currentRange.endTime = visibleTo;

    var fullDuration = _progState.fullRange.endTime - _progState.fullRange.startTime;
    var visibleDuration = rightTs - leftTs;
    var ratio = fullDuration > 0 ? visibleDuration / fullDuration : 1;

    // If showing most of the data, just update info
    if (ratio > 0.5) {
        _updateZoomInfo(visibleFrom, visibleTo, Math.round(ratio * 100) + '%');
        return;
    }

    // While live is on, keep the in-memory series (delta appends) intact.
    if (_liveState.enabled) {
        _updateZoomInfo(visibleFrom, visibleTo, Math.round(ratio * 100) + '%');
        return;
    }

    // Calculate how many data points currently visible
    var dataLen = progressiveChart.data.datasets[0] ? progressiveChart.data.datasets[0].data.length : 0;
    var visibleCount = 0;
    for (var i = 0; i < dataLen; i++) {
        var ts = _progState.timestamps[i];
        if (ts !== undefined && ts >= leftTs && ts <= rightTs) visibleCount++;
    }

    // Request a detail level appropriate for the viewport.
    var scrollEl = document.getElementById('dpc-scroll');
    var containerWidth = scrollEl ? scrollEl.clientWidth : 1200;
    var PX_PER_POINT = 7;
    var desiredPoints = Math.max(Math.floor(containerWidth / PX_PER_POINT), 100);
    var newMaxPoints = Math.min(desiredPoints, window.innerWidth < 768 ? 1500 : 5000);

    var canvas = document.getElementById('dpc-canvas');
    var monitoringId = canvas ? parseInt(canvas.dataset.monitoringId || '0') : 0;
    if (!monitoringId) return;

    _progFetchData(monitoringId, visibleFrom, visibleTo, newMaxPoints)
        .then(function (data) {
            _updateChartData(data);
            _updateZoomInfo(visibleFrom, visibleTo, data.labels.length + ' pts');
        })
        .catch(function () {});
    } catch (err) { console.error('dpc onZoomEnd error:', err); }
}

function _updateZoomInfo(fromTime, toTime, info) {
    var el = document.getElementById('dpc-zoom-info');
    if (!el) return;
    var fromLabel = fromTime ? fromTime.substring(11, 19) : '...';
    var toLabel = toTime ? toTime.substring(11, 19) : '...';
    el.textContent = (info || '') + ' | ' + fromLabel + ' - ' + toLabel;
}

function _updateZoomRatio() {
    var el = document.getElementById('dpc-zoom-pct');
    if (!el || !_progState.fullRange.startTime || !_progState.fullRange.endTime) return;    if (!progressiveChart) return;

    var xScale = progressiveChart.scales['x-axis-0'];
    if (!xScale) return;

    var ca = progressiveChart.chartArea;
    if (!ca) return;

    var leftVal = xScale.getValueForPixel(ca.left);
    var rightVal = xScale.getValueForPixel(ca.right);
    if (leftVal === undefined || rightVal === undefined) return;

    var fullDuration = _progState.fullRange.endTime - _progState.fullRange.startTime;
    if (fullDuration <= 0) return;

    var visibleDuration = rightVal - leftVal;
    var ratio = Math.min(100, Math.max(0.1, (visibleDuration / fullDuration) * 100));
    el.textContent = ratio.toFixed(1) + '%';
}

function _updateChartData(data) {
    if (!progressiveChart || !data || !data.labels) return;

    _progState.labelsFa = data.labelsFa || [];
    _progState.cycles = data.cycles || null;
    _progState.pointMetadata = data.pointMetadata || [];
    _transitionCache = {};

    var count = data.labels.length;
    if (count === 0) return;

    // Convert server ISO timestamps to epoch ms
    var epochTs = (data.timestamps || []).map(function(iso) { return _tsToEpoch(iso); });
    _progState.timestamps = epochTs;

    // Keep the original full range when replacing the chart with a zoomed slice.
    // Otherwise each zoom would redefine 100% as the latest slice and break
    // zoom ratio calculations and later range decisions.
    var firstTs = epochTs[0];
    var lastTs = epochTs[count - 1];
    var responseFullStart = data.fullRangeStart ? _tsToEpoch(data.fullRangeStart) : firstTs;
    var responseFullEnd = data.fullRangeEnd ? _tsToEpoch(data.fullRangeEnd) : lastTs;
    if (_progState.mode === 'progressive' && data.fullRangeStart && data.fullRangeEnd) {
        _progState.fullRange.startTime = responseFullStart;
        _progState.fullRange.endTime = responseFullEnd;
        _progState.currentRange.startTime = new Date(responseFullStart).toISOString();
        _progState.currentRange.endTime = new Date(responseFullEnd).toISOString();
    } else if (firstTs !== null && lastTs !== null &&
        (!_progState.fullRange.startTime || !_progState.fullRange.endTime)) {
        _progState.fullRange.startTime = responseFullStart;
        _progState.fullRange.endTime = responseFullEnd;
    }
    if (firstTs !== null && lastTs !== null && !_progState.currentRange.startTime) {
        _progState.currentRange.startTime = new Date(firstTs).toISOString();
        _progState.currentRange.endTime = new Date(lastTs).toISOString();
    }
    _syncMobileRangeSelector();

    // ── Dynamic wrapper resize based on time span ──
    var wrapper = document.getElementById('dpc-wrapper');
    var dpr = window.innerWidth < 768 ? 1 : (window.devicePixelRatio || 1);
        var safeCssMax = Math.floor(12000 / dpr);
    // Target: ~7px per data point average, capped by safe canvas size
    var pxPerPoint = 7;
    var scrollEl = document.getElementById('dpc-scroll');
    var containerWidth = scrollEl ? scrollEl.clientWidth : window.innerWidth;
    var isDesktop = window.innerWidth >= 768;
    // On desktop, if data fits in viewport use 100% width (no scroll needed)
    if (isDesktop && count * pxPerPoint <= containerWidth) {
        if (wrapper) wrapper.style.width = '100%';
    } else {
        var targetWidth = Math.min(count * pxPerPoint, safeCssMax);
        if (wrapper) wrapper.style.width = targetWidth + 'px';
    }
    if (_progState.mode === 'progressive' && scrollEl) scrollEl.scrollLeft = 0;

    // Build {x,y} datasets
    progressiveChart.data.labels = [];
    progressiveChart.data.datasets[0].data = _buildXY(data.temperatureRef, epochTs);
    progressiveChart.data.datasets[1].data = _buildXY(data.temperatureFreez, epochTs);
    progressiveChart.data.datasets[2].data = _buildXY((data.power || []).map(function(v) { return v === 1 ? 1.0 : 0; }), epochTs);
    progressiveChart.data.datasets[3].data = _buildXY((data.motor || []).map(function(v) { return v === 1 ? 0.9 : 0; }), epochTs);
    progressiveChart.data.datasets[4].data = _buildXY((data.heater1 || []).map(function(v) { return v === 1 ? 0.8 : 0; }), epochTs);
    progressiveChart.data.datasets[5].data = _buildXY((data.heater2 || []).map(function(v) { return v === 1 ? 0.7 : 0; }), epochTs);
    progressiveChart.data.datasets[6].data = _buildXY(data.tavan, epochTs);
    progressiveChart.data.datasets[7].data = _buildXY(data.jaryan, epochTs);

    if (_progGradients[0]) progressiveChart.data.datasets[0].backgroundColor = _progGradients[0];
    if (_progGradients[1]) progressiveChart.data.datasets[1].backgroundColor = _progGradients[1];

    try { progressiveChart.update(0); } catch (e) {}

    // Set zoom/pan range limits to exact data bounds
    _progUpdateRangeLimits();

    // Keep the exact user-selected window after replacing the dataset.
    var pending = _progState.pendingRange;
    if (pending && pending.startTime !== null && pending.endTime !== null) {
        var xScale = progressiveChart.scales['x-axis-0'];
        if (xScale) {
            xScale.options.ticks.min = pending.startTime;
            xScale.options.ticks.max = pending.endTime;
            try { progressiveChart.update(0); } catch (e) {}
        }
    }

    _notifyLoading(false);
}

function _progUpdateRangeLimits() {
    if (!progressiveChart || !_progState.fullRange.startTime) return;
    var opts = progressiveChart.options.plugins.zoom;
    if (opts) {
        opts.pan.rangeMin = { x: _progState.fullRange.startTime };
        opts.pan.rangeMax = { x: _progState.fullRange.endTime };
        opts.zoom.rangeMin = { x: _progState.fullRange.startTime };
        opts.zoom.rangeMax = { x: _progState.fullRange.endTime };
    }
}

// ── Reset zoom to full range ──
function _progResetZoom() {
    if (!progressiveChart) return;

    if (_progState._abortController) {
        try { _progState._abortController.abort(); } catch (e) {}
        _progState._abortController = null;
    }
    _progState.isLoading = false;

    if (progressiveChart.resetZoom) {
        try { progressiveChart.resetZoom(); } catch (e) {}
    }

    _progUpdateRangeLimits();

    _progState.currentRange.startTime = null;
    _progState.currentRange.endTime = null;
    _progState.pendingRange.startTime = null;
    _progState.pendingRange.endTime = null;
    var rangeRoot = document.getElementById('dpc-range-selector');
    if (rangeRoot) {
        rangeRoot.dataset.rangeStart = '0';
        rangeRoot.dataset.rangeEnd = '1000';
        _syncMobileRangeSelector();
    }

    var canvas = document.getElementById('dpc-canvas');
    var monitoringId = canvas ? parseInt(canvas.dataset.monitoringId || '0') : 0;
    if (!monitoringId) return;

    if (_progState.mode === 'progressive') {
        // Keep progressive mode: restore current segment, not full overview data.
        if (_progState.currentSegment >= 0) {
            var segIdx = _progState.currentSegment;
            var cacheKey = _progState.monitoringId + '|' + segIdx;
            _progState._lastFetchKey = '';
            var cached = _progState.segmentCache[cacheKey];
            if (cached) {
                var segment = _progState.segments[segIdx];
                _progState.pendingRange = {
                    startTime: segment ? _tsToEpoch(segment.from) : null,
                    endTime: segment ? _tsToEpoch(segment.to) : null
                };
                _updateChartData(cached);
                _updateSegmentStatus();
                return;
            }
            return _loadSegment(segIdx);
        }
        return _loadProgressiveSegments();
    }

    if (_liveState.enabled) {
        // Live owns the series; just restore full zoom on current data.
        _progUpdateRangeLimits();
        return;
    }

    _progState._lastFetchKey = '';

    _progFetchData(monitoringId, null, null, 1200)
        .then(function (data) {
            _updateChartData(data);
            _updateZoomInfo(_progState.fullRange.startTime ? new Date(_progState.fullRange.startTime).toISOString() : null,
                           _progState.fullRange.endTime ? new Date(_progState.fullRange.endTime).toISOString() : null,
                           data.labels.length + ' pts');
            if (_progState.timestamps.length) {
                _progState.lastRawTs = new Date(_progState.timestamps[_progState.timestamps.length - 1]).toISOString();
            }
            _progState.baseTotal = data.TotalCount || data.totalCount || _progState.baseTotal;
            _progState.totalRecords = _progState.baseTotal;
            _progState.appendedCount = 0;
        })
        .catch(function () {});
}

// ── External API ──
function _dpcBindEvents() {
    if (window._dpcEventsBound) return;
    window._dpcEventsBound = true;

    document.addEventListener('change', function(e) {
        if (e.target && e.target.id === 'dpc-live-toggle') {
            if (window.monitoringProgressiveChart && window.monitoringProgressiveChart.setLiveMode) {
                window.monitoringProgressiveChart.setLiveMode(e.target.checked);
            }
        }
    });

    document.addEventListener('keydown', function(e) {
        if (e.target.closest('input, textarea, select, [contenteditable]')) return;
        var chart = window.monitoringProgressiveChart;
        if (!chart) return;
        if (e.key === 'Escape') {
            if (_spanState.anchor !== null) {
                chart.clearSpan();
            }
            return;
        }
        if (e.key === 'ArrowLeft') {
            e.preventDefault();
            chart.navPrev();
        } else if (e.key === 'ArrowRight') {
            e.preventDefault();
            chart.navNext();
        }
    });

    document.addEventListener('click', function(e) {
        try {
        var modeBtn = e.target.closest('[data-chart-mode]');
        if (modeBtn) {
            var mode = modeBtn.getAttribute('data-chart-mode');
            if (window.monitoringProgressiveChart) window.monitoringProgressiveChart.setMode(mode);
            return;
        }
        if (e.target.closest('#dpc-segment-prev')) {
            if (window.monitoringProgressiveChart) window.monitoringProgressiveChart.previousSegment();
            return;
        }
        if (e.target.closest('#dpc-segment-next')) {
            if (window.monitoringProgressiveChart) window.monitoringProgressiveChart.nextSegment();
            return;
        }
        if (e.target.closest('#dpc-span-clear')) {
            if (window.monitoringProgressiveChart && window.monitoringProgressiveChart.clearSpan) {
                window.monitoringProgressiveChart.clearSpan();
            }
            return;
        }
        if (e.target.closest('#dpc-span-tool')) {
            if (window.monitoringProgressiveChart && window.monitoringProgressiveChart.setSpanMode) {
                var next = !_spanState.enabled;
                window.monitoringProgressiveChart.setSpanMode(next);
            }
            return;
        }
        var btn = e.target.closest('.dpc-legend-item[data-ds-idx]');
        if (btn) {
            var idx = parseInt(btn.getAttribute('data-ds-idx'), 10);
            var isVisible = window.monitoringProgressiveChart.toggleDataset(idx);
            btn.classList.toggle('dpc-legend-item--off', !isVisible);
            var set = btn.closest('.dpc-eq-set');
            if (set) {
                set.querySelectorAll('.dpc-eq-nav-btn').forEach(function(nb) {
                    nb.style.opacity = isVisible ? '1' : '0.3';
                });
            }
            return;
        }
        var navBtn = e.target.closest('.dpc-eq-nav-btn');
        if (navBtn) {
            var set = navBtn.closest('.dpc-eq-set');
            if (!set) return;
            var ds = parseInt(set.getAttribute('data-eq-ds'), 10);
            var dir = navBtn.getAttribute('data-dir');
            if (window.monitoringProgressiveChart.isDatasetVisible) {
                if (!window.monitoringProgressiveChart.isDatasetVisible(ds)) return;
            }
            window.monitoringProgressiveChart.navToTransition(ds, dir);
        }
        } catch (err) { console.error('dpc click error:', err); }
    });
}

window.monitoringProgressiveChart = {
    // ── Combined init + load (called from Blazor) ──
    initAndLoad: function (containerId, data, monitoringId) {
        this.destroy();
        _dpcBindEvents();

        var container = document.getElementById(containerId || 'dpc-chart-root');
        if (!container) { console.error('dpc: container not found'); return; }

        container.innerHTML = '';

        var scrollDiv = document.createElement('div');
        scrollDiv.className = 'dpc-chart-scroll';
        scrollDiv.id = 'dpc-scroll';

        var wrapperDiv = document.createElement('div');
        wrapperDiv.className = 'dpc-chart-wrapper';
        wrapperDiv.id = 'dpc-wrapper';
        var labelCount = data && data.labels ? data.labels.length : 0;
        var maxCanvW = 30000;
        var dpr = window.innerWidth < 768 ? 1 : (window.devicePixelRatio || 1);
        var safeCssMax = Math.floor(maxCanvW / dpr);
        var pxPerPoint = 7;
        var w = Math.max(labelCount * pxPerPoint, 600);
        w = Math.min(w, safeCssMax);
        wrapperDiv.style.width = w + 'px';

        var canvas = document.createElement('canvas');
        canvas.id = 'dpc-canvas';
        wrapperDiv.appendChild(canvas);
        scrollDiv.appendChild(wrapperDiv);
        container.appendChild(scrollDiv);
        _ensureLoadingOverlay(container);
        _ensureMobileRangeSelector(container);

        var ctx = canvas.getContext('2d');
        if (!ctx) { console.error('dpc: canvas 2d context not available'); return; }

        // Gradients
        var gradY = ctx.createLinearGradient(0, 0, 0, ctx.canvas.height);
        gradY.addColorStop(0, 'rgba(14,165,233,0.02)');
        gradY.addColorStop(0.6, 'rgba(14,165,233,0.08)');
        gradY.addColorStop(1, 'rgba(14,165,233,0.14)');
        _progGradients[0] = gradY;
        var gradF = ctx.createLinearGradient(0, 0, 0, ctx.canvas.height);
        gradF.addColorStop(0, 'rgba(225,29,72,0.14)');
        gradF.addColorStop(0.4, 'rgba(225,29,72,0.08)');
        gradF.addColorStop(1, 'rgba(225,29,72,0.02)');
        _progGradients[1] = gradF;

        progressiveChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: [],
                datasets: [
                    { label: 'یخچال', data: [], borderColor: '#0ea5e9', borderWidth: 3, pointRadius: 0, pointHoverRadius: 6, tension: 0.3, fill: true, yAxisID: 'y-temp', order: 5 },
                    { label: 'فریزر', data: [], borderColor: '#e11d48', borderWidth: 3, pointRadius: 0, pointHoverRadius: 6, tension: 0.3, fill: true, yAxisID: 'y-temp', order: 4 },
                    { label: 'برق', data: [], borderColor: '#eab308', backgroundColor: 'rgba(234,179,8,0.06)', borderWidth: 2, pointRadius: 0, pointHoverRadius: 6, steppedLine: true, fill: 3, yAxisID: 'y-eq', order: 3, hidden: true },
                    { label: 'موتور', data: [], borderColor: '#10b981', backgroundColor: 'rgba(16,185,129,0.07)', borderWidth: 3, pointRadius: 0, pointHoverRadius: 6, steppedLine: true, fill: 4, yAxisID: 'y-eq', order: 2 },
                    { label: 'المنت ۱', data: [], borderColor: '#f97316', backgroundColor: 'rgba(249,115,22,0.06)', borderWidth: 3, pointRadius: 0, pointHoverRadius: 6, steppedLine: true, fill: 5, yAxisID: 'y-eq', order: 1, hidden: true },
                    { label: 'المنت ۲', data: [], borderColor: '#ef4444', backgroundColor: 'rgba(239,68,68,0.05)', borderWidth: 3, pointRadius: 0, pointHoverRadius: 6, steppedLine: true, fill: 'origin', yAxisID: 'y-eq', order: 0, hidden: true },
                    { label: 'توان', data: [], borderColor: '#8b5cf6', borderWidth: 3, pointRadius: 0, pointHoverRadius: 5, lineTension: 0, fill: false, yAxisID: 'y-pwr', order: -1, hidden: true },
                    { label: 'جریان', data: [], borderColor: '#ec4899', borderWidth: 2.5, pointRadius: 0, pointHoverRadius: 5, lineTension: 0, spanGaps: false, fill: false, yAxisID: 'y-amp', order: -2, hidden: true }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                responsiveAnimationDuration: 0,
                animation: { duration: 0 },
                devicePixelRatio: window.innerWidth < 768 ? 1 : window.devicePixelRatio,
                layout: { padding: { top: 10, right: 40, bottom: 10, left: 0 } },
                legend: { display: false },
                scales: {
                    xAxes: [{
                        type: 'linear',
                        gridLines: { display: true, color: 'rgba(0,0,0,0.06)', drawBorder: true, borderDash: [2, 4] },
                        afterBuildTicks: function(axis) {
                            var min = axis.min, max = axis.max, range = max - min;
                            if (range <= 0) return;
                            var iv;
                            if (range >= 86400000 * 2) iv = 43200000;
                            else if (range >= 86400000) iv = 3600000;
                            else if (range >= 43200000) iv = 1800000;
                            else if (range >= 21600000) iv = 900000;
                            else if (range >= 7200000) iv = 600000;
                            else if (range >= 3600000) iv = 300000;
                            else if (range >= 1800000) iv = 120000;
                            else iv = 60000;
                            var start = Math.ceil(min / iv) * iv;
                            var ticks = [];
                            for (var t = start; t <= max; t += iv) ticks.push(t);
                            // Add equipment transition timestamps for precise alignment
                            var tss = _progState.timestamps;
                            var cycles = _progState.cycles;
                            if (tss && tss.length > 0 && cycles) {
                                var added = {};
                                for (var k in cycles) {
                                    if (!cycles.hasOwnProperty(k)) continue;
                                    var arr = cycles[k];
                                    if (!arr || !Array.isArray(arr)) continue;
                                    for (var i = 0; i < arr.length; i++) {
                                        var c = arr[i];
                                        if (c && c.startIndex !== undefined && c.startIndex < tss.length) {
                                            var tv = tss[c.startIndex];
                                            if (tv >= min && tv <= max && !added[tv]) { ticks.push(tv); added[tv] = true; }
                                        }
                                        if (c && c.endIndex !== undefined && c.endIndex < tss.length) {
                                            var tv = tss[c.endIndex];
                                            if (tv >= min && tv <= max && !added[tv]) { ticks.push(tv); added[tv] = true; }
                                        }
                                    }
                                }
                            }
                            ticks.sort(function(a, b) { return a - b; });
                            axis.ticks = ticks;
                        },
                        ticks: { fontColor: '#64748b', fontFamily: 'Vazirmatn, sans-serif', fontSize: 11, maxRotation: 45, autoSkip: false, callback: function (value) { return _epochToFaAxisStr(value); } }
                    }],
                    yAxes: [{
                        id: 'y-temp', position: 'left',
                        gridLines: { display: true, color: 'rgba(0,0,0,0.06)', drawBorder: true },
                        ticks: { fontColor: '#0ea5e9', fontFamily: 'Vazirmatn, sans-serif', fontSize: 12, fontStyle: 'bold', beginAtZero: false, padding: 8, callback: function (v) { return v + '°'; } },
                        scaleLabel: { display: true, labelString: 'Temperature (°C)', fontColor: '#94a3b8', fontFamily: 'Vazirmatn, sans-serif', fontSize: 11 }
                    }, {
                        id: 'y-eq', position: 'right',
                        gridLines: { display: false },
                        ticks: { min: -0.05, max: 1.05, stepSize: 0.2, fontFamily: 'Vazirmatn, sans-serif', fontSize: 11, padding: 8, autoSkip: false, callback: function () { return ''; } },
                        scaleLabel: { display: true, labelString: 'Equipment State', fontColor: '#94a3b8', fontFamily: 'Vazirmatn, sans-serif', fontSize: 11 }
                    }, {
                        id: 'y-pwr', position: 'left', weight: 2,
                        gridLines: { display: false },
                    ticks: { fontColor: '#8b5cf6', fontFamily: 'Vazirmatn, sans-serif', fontSize: 10, beginAtZero: true, padding: 4, suggestedMax: 1500 },
                    scaleLabel: { display: true, labelString: 'توان (W)', fontColor: '#8b5cf6', fontFamily: 'Vazirmatn, sans-serif', fontSize: 10 }
                    }, {
                        id: 'y-amp', position: 'right', weight: 2,
                        gridLines: { display: false },
                    ticks: { fontColor: '#ec4899', fontFamily: 'Vazirmatn, sans-serif', fontSize: 10, beginAtZero: true, padding: 4, callback: function (v) { return typeof v === 'number' ? v.toFixed(1) : v; } },
                    scaleLabel: { display: true, labelString: 'جریان (A)', fontColor: '#ec4899', fontFamily: 'Vazirmatn, sans-serif', fontSize: 10 }
                    }]
                },
                tooltips: {
                    enabled: true,
                    mode: 'index', intersect: false,
                    position: 'progTop',
                    backgroundColor: 'rgba(255,255,255,0.38)',
                    titleFontFamily: 'Vazirmatn, sans-serif', bodyFontFamily: 'Vazirmatn, sans-serif',
                    titleFontColor: '#1e293b', bodyFontColor: '#334155',
                    titleFontSize: 13, bodyFontSize: 12, titleFontStyle: 'bold',
                    xPadding: 16, yPadding: 10,
                    displayColors: true, bodySpacing: 5, titleSpacing: 6,
                    cornerRadius: 8, caretSize: 6, caretPadding: 4,
                    borderColor: 'rgba(255,255,255,0.72)', borderWidth: 1,
                    filter: function (item) {
                        if (item.datasetIndex >= 2 && item.datasetIndex <= 5) {
                            var thresholds = [0.5, 0.45, 0.4, 0.35];
                            return item.yLabel >= (thresholds[item.datasetIndex - 2] || 0.5);
                        }
                        return true;
                    },
                    callbacks: {
                        title: function (items) {
                            var idx = items[0].index;
                            var t = _progState.labelsFa && _progState.labelsFa[idx] ? _progState.labelsFa[idx] : _epochToTimeStr(items[0].xLabel);
                            var meta = _progState.pointMetadata && _progState.pointMetadata[idx];
                            if (meta) {
                                if (meta.isEquipmentTransition && meta.transitionType) {
                                    t += ' | ' + meta.transitionType;
                                }
                                if (meta.hasNote) {
                                    t += ' | 📝 یادداشت';
                                }
                            }
                            return '\u202B' + t + '\u202C';
                        },
                        label: function (item, data) {
                            var ds = item.datasetIndex;
                            var label = data.datasets[ds].label || '';
                            var v = item.yLabel;
                            var rle = '\u202B', pdf = '\u202C';
                            if (ds >= 2 && ds <= 5) {
                                var thresholds = [0.5, 0.45, 0.4, 0.35];
                                var thresh = thresholds[ds - 2] || 0.5;
                                if (v < thresh) return null;
                                return rle + label + ': روشن' + pdf;
                            }
                            if (ds === 6) return rle + label + ': ' + (typeof v === 'number' ? v.toFixed(1) : v) + ' W' + pdf;
                            if (ds === 7) return rle + label + ': ' + (typeof v === 'number' ? v.toFixed(2) : v) + ' A' + pdf;
                            return rle + label + ': ' + (typeof v === 'number' ? v.toFixed(1) : v) + '°C' + pdf;
                        }
                    }
                },
                hover: { mode: 'index', intersect: false, animationDuration: 0 },
                events: window.innerWidth < 768 ? ['mousemove', 'mouseout', 'touchstart', 'touchmove'] : ['mousemove', 'mouseout'],
                elements: { point: { radius: 0, hitRadius: window.innerWidth < 768 ? 24 : 18, hoverRadius: 5 } },
                plugins: {
                    zoom: {
                        pan: { enabled: true, mode: 'x', speed: window.innerWidth < 768 ? 0.5 : 1, threshold: 5,
                            onPan: function () { _updateZoomRatio(); },
                            onPanEnd: function () { _progOnZoomEnd(); } },
                        zoom: {
                            enabled: true,
                            mode: 'x',
                            speed: window.innerWidth < 768 ? 0.05 : 0.1,
                            drag: false,
                            onZoom: function () { _updateZoomRatio(); },
                            onZoomEnd: function () { _progOnZoomEnd(); }
                        }
                    }
                }
            }
        });

        _progSetupScroll();
        _progSetupContextMenu(progressiveChart);
        _progBindCrosshair(progressiveChart);
        _progSetupCtrlDragZoom(progressiveChart);
        _progSetupNoteClick(progressiveChart);
        _spanBindPointer(progressiveChart);

        // ── Now load the data ──
        if (!progressiveChart || !data || !data.labels) return;

        canvas.dataset.monitoringId = String(monitoringId || '');
        _progState.monitoringId = monitoringId || 0;

        _progState._lastFetchKey = String(monitoringId) + '||';
        _progState.baseTotal = data.TotalCount || data.totalCount || 0;
        _progState.totalRecords = _progState.baseTotal;
        _progState.appendedCount = 0;
        _progState.lastRawTs = (data.timestamps && data.timestamps.length)
            ? data.timestamps[data.timestamps.length - 1]
            : null;
        _updateChartData(data);
        if (!_progState.lastRawTs && _progState.timestamps.length) {
            _progState.lastRawTs = new Date(_progState.timestamps[_progState.timestamps.length - 1]).toISOString();
        }

        var scrollEl = document.getElementById('dpc-scroll');
        if (scrollEl) scrollEl.scrollLeft = 0;

        // Debounced resize handler
        if (!window._dpcResizeHandler) {
            window._dpcResizeHandler = true;
            var _resizeTimer;
            window.addEventListener('resize', function() {
                if (_resizeTimer) clearTimeout(_resizeTimer);
                _resizeTimer = setTimeout(function() {
                    if (window.monitoringProgressiveChart && window.monitoringProgressiveChart.resize) {
                        window.monitoringProgressiveChart.resize();
                    }
                }, 200);
            });
        }
    },

    toggleDataset: function (index) {
        if (!progressiveChart || index < 0 || index > 7) return;
        var ds = progressiveChart.data.datasets[index];
        if (!ds) return;
        ds.hidden = !ds.hidden;
        try { progressiveChart.update(0); } catch (e) {}
        return !ds.hidden;
    },

    isDatasetVisible: function (index) {
        if (!progressiveChart || index < 0 || index > 7) return false;
        var ds = progressiveChart.data.datasets[index];
        return ds ? !ds.hidden : false;
    },

    navToTransition: function (dsIdx, dir) {
        _navToTransition(dsIdx, dir);
    },

    navNext: function () {
        var ts = _progState.timestamps;
        if (!ts || ts.length === 0) return;
        var centerIdx = _getScrollCenterIdx();
        centerIdx = Math.max(0, Math.min(ts.length - 1, centerIdx));
        var nextIdx = centerIdx + 1;
        if (nextIdx >= ts.length) return;
        _scrollToIndex(nextIdx);
    },

    navPrev: function () {
        var ts = _progState.timestamps;
        if (!ts || ts.length === 0) return;
        var centerIdx = _getScrollCenterIdx();
        centerIdx = Math.max(0, Math.min(ts.length - 1, centerIdx));
        var prevIdx = centerIdx - 1;
        if (prevIdx < 0) return;
        _scrollToIndex(prevIdx);
    },

    resetZoom: function () {
        _progResetZoom();
    },

    setMode: function (mode) {
        return _setChartMode(mode);
    },

    setLiveMode: function (on) {
        if (on) _liveStart();
        else _liveStop();
        var toggle = document.getElementById('dpc-live-toggle');
        if (toggle) toggle.checked = !!_liveState.enabled;
        return _liveState.enabled;
    },

    isLive: function () { return _liveState.enabled; },

    setSpanMode: function (on) {
        return _spanSetEnabled(!!on);
    },

    clearSpan: function () {
        _spanClear();
    },

    previousSegment: function () {
        return _loadSegment(_progState.currentSegment - 1);
    },

    nextSegment: function () {
        return _loadSegment(_progState.currentSegment + 1);
    },

    resize: function () {
        if (!progressiveChart) return;
        try { progressiveChart.resize(); progressiveChart.update(0); } catch (e) {}
    },

    destroy: function () {
            _liveStop();
            _spanState.enabled = false;
            _spanState.anchor = null;
            _spanState.end = null;
            _spanState.dragging = false;
            _spanState.locked = false;
            var spanBtn = document.getElementById('dpc-span-tool');
            if (spanBtn) spanBtn.classList.remove('is-on');
            var spanHud = document.getElementById('dpc-span-hud');
            if (spanHud) spanHud.hidden = true;
            if (progressiveChart) {
                progressiveChart.destroy();
                progressiveChart = null;
            }
            if (_progState._abortController) {
                try { _progState._abortController.abort(); } catch (e) {}
                _progState._abortController = null;
            }
            _progState.timestamps = [];
            _progState.labelsFa = [];
            _progState.cycles = null;
            _progState.pointMetadata = [];
            _progState.fullRange = { startTime: null, endTime: null };
            _progState.currentRange = { startTime: null, endTime: null };
            _progState.pendingRange = { startTime: null, endTime: null };
            _progState.isLoading = false;
            _progState._requestSeq = 0;
            _progState._lastFetchKey = '';
            _progState.mode = 'overview';
            _progState.segments = [];
            _progState.segmentCache = {};
            _progState.currentSegment = -1;
            _progState.monitoringId = 0;
        _progState.lastRawTs = null;
        _progState.baseTotal = 0;
        _progState.appendedCount = 0;
        _progState.totalRecords = 0;
        _transitionCache = {};
            var liveToggle = document.getElementById('dpc-live-toggle');
            if (liveToggle) liveToggle.checked = false;
        },

        // ── Notes API ──
        addNote: function (index, text) { _progNoteAdd(index, text); },
        getNotes: function () { return _progNoteGetAll(); },
        removeNote: function (index) { _progNoteRemove(index); }
    };

// ── Notes State & Storage ──
var _progNotes = {};

function _progGetNotesKey() {
    return 'dpc_notes_' + (_progState.monitoringId || '0');
}

function _progNoteLoad() {
    try {
        var stored = localStorage.getItem(_progGetNotesKey());
        _progNotes = stored ? JSON.parse(stored) : {};
    } catch (e) { _progNotes = {}; }
}
_progNoteLoad();

function _progNoteSave() {
    try { localStorage.setItem(_progGetNotesKey(), JSON.stringify(_progNotes)); } catch (e) {}
}

function _progNoteAdd(index, text) {
    if (text && text.trim()) {
        _progNotes[String(index)] = { text: text.trim(), time: Date.now() };
        _progNoteSave();
        if (progressiveChart) progressiveChart.update(0);
    }
}

function _progNoteRemove(index) {
    delete _progNotes[String(index)];
    _progNoteSave();
    if (progressiveChart) progressiveChart.update(0);
}

function _progNoteGetAll() { return _progNotes; }

function _progNoteHas(index) { return !!_progNotes[String(index)]; }

// ── Note markers plugin ──
Chart.plugins.register({
    id: 'prog-note-markers',
    afterDatasetsDraw: function(chart) {
        if (chart !== progressiveChart) return;
        var xScale = chart.scales['x-axis-0'];
        if (!xScale) return;
        var ctx = chart.ctx;
        ctx.save();
        var ts = _progState.timestamps;
        var top = chart.chartArea.top;
        // Draw markers for client-side notes
        for (var idxStr in _progNotes) {
            var idx = parseInt(idxStr, 10);
            if (isNaN(idx) || idx < 0 || idx >= ts.length || ts[idx] === undefined) continue;
            var px = xScale.getPixelForValue(ts[idx]);
            if (px === undefined || px === null) continue;
            var mx = px, my = top + 16;
            ctx.beginPath();
            ctx.moveTo(mx, top + 1);
            ctx.lineTo(mx, my + 12);
            ctx.strokeStyle = 'rgba(245,158,11,0.5)';
            ctx.lineWidth = 2;
            ctx.setLineDash([3, 3]);
            ctx.stroke();
            ctx.setLineDash([]);
            ctx.shadowColor = 'rgba(245,158,11,0.5)';
            ctx.shadowBlur = 12;
            ctx.beginPath();
            ctx.arc(mx, my, 13, 0, Math.PI * 2);
            ctx.fillStyle = '#f59e0b';
            ctx.fill();
            ctx.shadowColor = 'transparent';
            ctx.shadowBlur = 0;
            ctx.beginPath();
            ctx.arc(mx, my, 13, 0, Math.PI * 2);
            ctx.strokeStyle = '#fff';
            ctx.lineWidth = 2.5;
            ctx.stroke();
            ctx.font = 'bold 14px sans-serif';
            ctx.fillStyle = '#fff';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillText('!', mx, my + 0.5);
            ctx.font = '9px Vazirmatn, sans-serif';
            ctx.fillStyle = '#f59e0b';
            ctx.fillText('\u06CC\u0627\u062F\u062F\u0627\u0634\u062A', mx, my + 22);
        }
        // Draw transition markers from PointMetadata
        var meta = _progState.pointMetadata;
        if (meta && meta.length) {
            for (var mi = 0; mi < meta.length; mi++) {
                var pm = meta[mi];
                if (!pm || !pm.isEquipmentTransition) continue;
                if (_progNotes[String(mi)]) continue;
                if (mi >= ts.length || ts[mi] === undefined) continue;
                var px2 = xScale.getPixelForValue(ts[mi]);
                if (px2 === undefined || px2 === null) continue;
                ctx.beginPath();
                ctx.arc(px2, chart.chartArea.bottom - 6, 4, 0, Math.PI * 2);
                ctx.fillStyle = '#8b5cf6';
                ctx.fill();
                ctx.strokeStyle = '#fff';
                ctx.lineWidth = 1.5;
                ctx.stroke();
            }
        }
        ctx.restore();
    }
});

// ── Note marker click handler ──
function _progSetupNoteClick(chart) {
    if (!chart || !chart.canvas) return;
    chart.canvas.addEventListener('click', function(e) {
        if (_spanState.enabled) return;
        if (e.ctrlKey || e.button !== 0) return;
        var rect = chart.canvas.getBoundingClientRect();
        var x = e.clientX - rect.left;
        var xScale = chart.scales['x-axis-0'];
        if (!xScale) return;
        var ts = _progState.timestamps;
        var top = chart.chartArea.top;
        for (var idxStr in _progNotes) {
            var idx = parseInt(idxStr, 10);
            if (isNaN(idx) || idx < 0 || idx >= ts.length || ts[idx] === undefined) continue;
            var px = xScale.getPixelForValue(ts[idx]);
            if (px === undefined) continue;
            var dy = (e.clientY - rect.top) - (top + 16);
            var dx = px - x;
            if (dx * dx + dy * dy < 400) {
                _progShowNoteModal(idx, e.clientX, e.clientY);
                e.preventDefault();
                return;
            }
        }
    });
}

// ── Context menu for notes ──
function _progSetupContextMenu(chart) {
    if (!chart || !chart.canvas) return;
    var canvas = chart.canvas;

    canvas.addEventListener('contextmenu', function (e) {
        if (_spanState.enabled) return;
        e.preventDefault();
        e.stopPropagation();

        var rect = canvas.getBoundingClientRect();
        var x = e.clientX - rect.left;
        var xScale = chart.scales['x-axis-0'];
        if (!xScale) return;

        var clickTs = xScale.getValueForPixel(x);
        if (clickTs === undefined) return;

        // Find nearest point index by time
        var ts = _progState.timestamps;
        var bestIdx = 0, bestDist = Infinity;
        for (var i = 0; i < ts.length; i++) {
            if (ts[i] === undefined) continue;
            var d = Math.abs(ts[i] - clickTs);
            if (d < bestDist) { bestDist = d; bestIdx = i; }
        }

        _progShowNoteModal(bestIdx, e.clientX, e.clientY);
    });
}

function _progShowNoteModal(index, clientX, clientY) {
    // Remove existing modal
    var existing = document.getElementById('dpc-note-modal');
    if (existing) existing.remove();

    var note = _progNotes[String(index)];
    var label = _progState.labelsFa && _progState.labelsFa[index] ? _progState.labelsFa[index] : _epochToTimeStr(_progState.timestamps[index]);

    var modal = document.createElement('div');
    modal.id = 'dpc-note-modal';
    modal.style.cssText = 'position:fixed;z-index:10000;background:#fff;border-radius:12px;box-shadow:0 20px 50px rgba(0,0,0,0.2);padding:20px;min-width:300px;max-width:400px;direction:rtl;font-family:Vazirmatn,sans-serif;';
    modal.innerHTML = '\
        <div style="font-weight:800;margin-bottom:12px;color:#0f172a;">یادداشت برای نقطه ' + (index + 1) + ' (' + label + ')</div>\
        <textarea id="dpc-note-text" style="width:100%;min-height:100px;padding:10px;border:1px solid #e2e8f0;border-radius:8px;font-family:Vazirmatn,sans-serif;font-size:13px;resize:vertical;box-sizing:border-box;">' + (note ? note.text : '') + '</textarea>\
        <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:12px;">\
            <button id="dpc-note-cancel" style="padding:8px 16px;border:1px solid #e2e8f0;background:#fff;border-radius:8px;cursor:pointer;font-family:Vazirmatn,sans-serif;">انصراف</button>\
            <button id="dpc-note-save" style="padding:8px 16px;border:none;background:#2563eb;color:#fff;border-radius:8px;cursor:pointer;font-family:Vazirmatn,sans-serif;font-weight:700;">ذخیره</button>\
            ' + (note ? '<button id="dpc-note-delete" style="padding:8px 16px;border:none;background:#ef4444;color:#fff;border-radius:8px;cursor:pointer;font-family:Vazirmatn,sans-serif;font-weight:700;">حذف</button>' : '') + '\
        </div>';

    document.body.appendChild(modal);

    // Position near click
    modal.style.left = Math.min(clientX + 10, window.innerWidth - modal.offsetWidth - 10) + 'px';
    modal.style.top = Math.min(clientY + 10, window.innerHeight - modal.offsetHeight - 10) + 'px';

    var textarea = modal.querySelector('#dpc-note-text');
    textarea.focus();

    function close() { modal.remove(); document.removeEventListener('keydown', onKey); }
    function onKey(e) { if (e.key === 'Escape') close(); }
    document.addEventListener('keydown', onKey);

    modal.querySelector('#dpc-note-cancel').onclick = close;
    modal.querySelector('#dpc-note-save').onclick = function () {
        var text = textarea.value.trim();
        _progNoteAdd(index, text);
        close();
    };
    if (note) {
        modal.querySelector('#dpc-note-delete').onclick = function () {
            _progNoteRemove(index);
            close();
        };
    }

    // Close on outside click
    setTimeout(function () {
        document.addEventListener('click', function onDocClick(e) {
            if (!modal.contains(e.target)) { close(); document.removeEventListener('click', onDocClick); }
        });
    }, 0);
}

// ── Blazor component wrappers (moved from MonitoringProgressiveChart.razor) ──
window.resetProgressiveChartZoom = function () {
    if (window.monitoringProgressiveChart && typeof window.monitoringProgressiveChart.resetZoom === 'function') {
        window.monitoringProgressiveChart.resetZoom();
    }
};

window.dpcApplyBodyClass = function () {
    if (document.querySelector('.dpc-page')) {
        document.body.classList.add('has-dpc-page');
    } else {
        document.body.classList.remove('has-dpc-page');
    }
};

window.dpcClearBodyClass = function () {
    document.body.classList.remove('has-dpc-page');
};


