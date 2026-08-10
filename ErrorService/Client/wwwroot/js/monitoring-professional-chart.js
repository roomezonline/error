var professionalChart = null;

// ── On-chart per-cycle duration labels (data from C# Cycles dict) ──
// Must register BEFORE crosshair so labels draw first, crosshair on top.
Chart.plugins.register({
    id: 'cycle-labels',
    afterDatasetsDraw: function(chart) {
        if (chart !== professionalChart) return;
        var cycles = window.monitoringProfessionalChart._cycles;
        if (!cycles) return;
        var xScale = chart.scales['x-axis-0'];
        var yEq = chart.scales['y-eq'];
        if (!xScale || !yEq) return;

        var params = [
            { key: 'power', dsIdx: 2, bandY: 0.45, color: '#6b7280', textColor: '#ffffff' },
            { key: 'motor', dsIdx: 3, bandY: 2.5, color: '#22c55e' },
            { key: 'heater1', dsIdx: 4, bandY: 4.5, color: '#3b82f6' },
            { key: 'heater2', dsIdx: 5, bandY: 6.5, color: '#f59e0b' }
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
                var midIdx = Math.round((c.startIndex + c.endIndex) / 2);
                var midX = xScale.getPixelForValue(undefined, midIdx);
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

// ── Crosshair — tracks mouse on canvas directly (no tooltip dependency) ──
var _proChX = null; // canvas-relative x, null = hidden

Chart.plugins.register({
    id: 'crosshair-direct',
    afterDatasetsDraw: function(chart) {
        if (chart !== professionalChart || _proChX === null) return;

        var xScale = chart.scales['x-axis-0'];
        if (!xScale) return;

        // Find any vertical axis for top/bottom bounds
        var yAxis = null;
        var keys = Object.keys(chart.scales || {});
        for (var k = 0; k < keys.length; k++) {
            var s = chart.scales[keys[k]];
            if (s && s.isHorizontal && !s.isHorizontal()) { yAxis = s; break; }
        }
        if (!yAxis) return;
        var topY = yAxis.top, bottomY = yAxis.bottom;
        if (topY === undefined || bottomY === undefined) return;

        // Find nearest data index from mouse pixel
        var total = chart.data.labels ? chart.data.labels.length : 0;
        if (total === 0) return;
        var bestIdx = 0, bestDist = Infinity;
        var step = Math.max(1, Math.floor(total / 300));
        for (var i = 0; i < total; i += step) {
            var px = xScale.getPixelForValue(undefined, i);
            var d = Math.abs(px - _proChX);
            if (d < bestDist) { bestDist = d; bestIdx = i; }
        }
        var rs = Math.max(0, bestIdx - step);
        var re = Math.min(total - 1, bestIdx + step);
        for (var i = rs; i <= re; i++) {
            var px = xScale.getPixelForValue(undefined, i);
            var d = Math.abs(px - _proChX);
            if (d < bestDist) { bestDist = d; bestIdx = i; }
        }

        var x = xScale.getPixelForValue(undefined, bestIdx);
        var ctx = chart.ctx;
        ctx.save();

        // Vertical line
        ctx.beginPath();
        ctx.setLineDash([]);
        ctx.moveTo(x, topY);
        ctx.lineTo(x, bottomY);
        ctx.lineWidth = 2;
        ctx.strokeStyle = 'rgba(71, 85, 105, 0.92)';
        ctx.stroke();

        // Highlight dots
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

// Crosshair mouse event setup — call after chart is created
function _proBindCrosshair(chart) {
    if (!chart || !chart.canvas) return;
    var lastDraw = 0, touchId = false;
    var scrollEl = document.getElementById('mpc-scroll');
    function isDragging() { return scrollEl && scrollEl.dataset._proDrag === '1'; }
    function onMove(e) {
        if (touchId || isDragging()) { _proChX = null; return; }
        var rect = chart.canvas.getBoundingClientRect();
        _proChX = e.clientX - rect.left;
        var now = Date.now();
        if (now - lastDraw > 40) { lastDraw = now; chart.draw(); }
    }
    function onLeave() { _proChX = null; chart.draw(); }
    function onTouchStart(e) { touchId = true; _proChX = null; }
    function onTouchEnd(e) { touchId = false; _proChX = null; chart.draw(); }
    function onTouchMove(e) { _proChX = null; }
    chart.canvas.addEventListener('mousemove', onMove);
    chart.canvas.addEventListener('mouseleave', onLeave);
    chart.canvas.addEventListener('touchstart', onTouchStart, { passive: true });
    chart.canvas.addEventListener('touchmove', onTouchMove, { passive: true });
    chart.canvas.addEventListener('touchend', onTouchEnd, { passive: true });
}

// ── Gradient fill for temperature datasets (built once in init) ──
var _proGradients = [null, null];

// ── Band separator lines ──
Chart.plugins.register({
    afterDraw: function(chart) {
        if (chart !== professionalChart) return;
        var yEq = chart.scales['y-eq'];
        if (!yEq) return;
        var ctx = chart.ctx;
        ctx.save();
        ctx.setLineDash([3, 4]);
        ctx.strokeStyle = 'rgba(148, 163, 184, 0.18)';
        ctx.lineWidth = 1;
        [1.5, 3.5, 5.5].forEach(function(v) {
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

function _proSetupScroll() {
    var el = document.getElementById('mpc-scroll');
    if (!el || el.dataset._proScroll === '1') return;
    el.dataset._proScroll = '1';

    var isDown = false, startX = 0, startLeft = 0, dragged = false;
    var threshold = 8;

    function down(x) { isDown = true; dragged = false; startX = x; startLeft = el.scrollLeft; el.dataset._proDrag = '0'; }
    function move(x) {
        if (!isDown) return;
        var dx = startX - x;
        if (Math.abs(dx) > threshold) { dragged = true; el.dataset._proDrag = '1'; }
        if (dragged) { el.scrollLeft = startLeft + dx; }
    }
    function up() { isDown = false; el.dataset._proDrag = '0'; }

    el.addEventListener('mousedown', function (e) { if (e.button !== 0) return; down(e.clientX); });
    el.addEventListener('mousemove', function (e) { move(e.clientX); });
    document.addEventListener('mouseup', function () { if (dragged) { up(); } else { isDown = false; } });
    el.addEventListener('click', function (e) { if (dragged) { e.stopPropagation(); e.preventDefault(); } }, true);

    el.addEventListener('touchstart', function (e) { if (e.touches.length !== 1) return; down(e.touches[0].clientX); }, { passive: true });
    el.addEventListener('touchmove', function (e) { if (e.touches.length !== 1) return; move(e.touches[0].clientX); }, { passive: true });
    el.addEventListener('touchend', up, { passive: true });

    el.addEventListener('wheel', function (e) {
        if (Math.abs(e.deltaX) > Math.abs(e.deltaY)) {
            el.scrollLeft += e.deltaX;
            e.preventDefault();
        }
    }, { passive: false });
};

// Custom tooltip positioner: fixed at chart top, follows mouse x
Chart.Tooltip.positioners.top = function(elements, eventPosition) {
    if (!elements || !elements.length) return false;
    var chart = elements[0]._chart;
    var ca = chart.chartArea;
    if (!ca) return false;
    var mx = eventPosition ? eventPosition.x : elements[0]._view.x;
    if (mx === undefined || mx === null) return false;

    // Snap to nearest data point (same logic as crosshair)
    var xScale = chart.scales['x-axis-0'];
    var x = mx;
    if (xScale && chart.data.labels) {
        var total = chart.data.labels.length;
        var bestD = Infinity, bestI = 0;
        var step = Math.max(1, Math.floor(total / 300));
        for (var i = 0; i < total; i += step) {
            var px = xScale.getPixelForValue(undefined, i);
            var d = Math.abs(px - mx);
            if (d < bestD) { bestD = d; bestI = i; }
        }
        var rs = Math.max(0, bestI - step);
        var re = Math.min(total - 1, bestI + step);
        for (var i = rs; i <= re; i++) {
            var px = xScale.getPixelForValue(undefined, i);
            var d = Math.abs(px - mx);
            if (d < bestD) { bestD = d; bestI = i; }
        }
        x = xScale.getPixelForValue(undefined, bestI);
    }
    return { x: x, y: ca.top + 6 };
};

window.monitoringProfessionalChart = {
    _labelsFa: [], _cycles: null,

    init: function (canvasId) {
        this.destroy();

        var canvas = document.getElementById(canvasId);
        if (!canvas) return;
        var ctx = canvas.getContext('2d');
        if (!ctx) return;

        // Build temperature gradients once
        var gradY = ctx.createLinearGradient(0, 0, 0, ctx.canvas.height);
        gradY.addColorStop(0, 'rgba(20,184,166,0.28)');
        gradY.addColorStop(0.5, 'rgba(20,184,166,0.12)');
        gradY.addColorStop(1, 'rgba(20,184,166,0.02)');
        _proGradients[0] = gradY;
        var gradF = ctx.createLinearGradient(0, 0, 0, ctx.canvas.height);
        gradF.addColorStop(0, 'rgba(244,63,94,0.22)');
        gradF.addColorStop(0.5, 'rgba(244,63,94,0.10)');
        gradF.addColorStop(1, 'rgba(244,63,94,0.02)');
        _proGradients[1] = gradF;

        professionalChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: [],
                datasets: [
                    { label: 'یخچال', data: [], borderColor: '#14b8a6', borderWidth: 3, pointRadius: 0, pointHoverRadius: 6, tension: 0.3, fill: true, yAxisID: 'y-temp', order: 5 },
                    { label: 'فریزر', data: [], borderColor: '#f43f5e', borderWidth: 3, pointRadius: 0, pointHoverRadius: 6, tension: 0.3, fill: true, yAxisID: 'y-temp', order: 4 },
                    { label: 'برق', data: [], borderColor: '#6b7280', borderWidth: 3, pointRadius: 0, pointHoverRadius: 6, steppedLine: true, fill: false, yAxisID: 'y-eq', order: 3 },
                    { label: 'موتور', data: [], borderColor: '#22c55e', borderWidth: 3, pointRadius: 0, pointHoverRadius: 6, steppedLine: true, fill: false, yAxisID: 'y-eq', order: 2 },
                    { label: 'المنت ۱', data: [], borderColor: '#3b82f6', borderWidth: 3, pointRadius: 0, pointHoverRadius: 6, steppedLine: true, fill: false, yAxisID: 'y-eq', order: 1 },
                    { label: 'المنت ۲', data: [], borderColor: '#f59e0b', borderWidth: 3, pointRadius: 0, pointHoverRadius: 6, steppedLine: true, fill: false, yAxisID: 'y-eq', order: 0 },
                    { label: 'توان', data: [], borderColor: '#8b5cf6', borderWidth: 3, pointRadius: 0, pointHoverRadius: 5, tension: 0.3, fill: false, spanGaps: true, yAxisID: 'y-pwr', order: -1 },
                    { label: 'جریان', data: [], borderColor: '#ec4899', borderWidth: 3, pointRadius: 0, pointHoverRadius: 5, tension: 0.3, fill: false, spanGaps: true, yAxisID: 'y-amp', order: -2 }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                responsiveAnimationDuration: 0,
                animation: { duration: 0 },
                devicePixelRatio: window.innerWidth < 768 ? 1 : window.devicePixelRatio,
                layout: { padding: { top: 10, right: 40, bottom: 10, left: 20 } },
                legend: { display: false },
                scales: {
                    xAxes: [{
                        type: 'category',
                        gridLines: { display: true, color: 'rgba(0,0,0,0.06)', drawBorder: true, borderDash: [2, 4] },
                        ticks: { fontColor: '#94a3b8', fontFamily: 'Vazirmatn, sans-serif', fontSize: 11, maxRotation: 45, autoSkip: true, autoSkipPadding: 30, maxTicksLimit: 30 }
                    }],
                    yAxes: [{
                        id: 'y-temp', position: 'left',
                        gridLines: { display: true, color: 'rgba(0,0,0,0.06)', drawBorder: true },
                        ticks: { fontColor: '#14b8a6', fontFamily: 'Vazirmatn, sans-serif', fontSize: 12, fontStyle: 'bold', beginAtZero: false, padding: 8, callback: function (v) { return v + '°'; } },
                        scaleLabel: { display: true, labelString: 'Temperature (°C)', fontColor: '#94a3b8', fontFamily: 'Vazirmatn, sans-serif', fontSize: 11 }
                    }, {
                        id: 'y-eq', position: 'right',
                        gridLines: { display: false },
                        ticks: { min: -0.3, max: 7.3, stepSize: 1, fontFamily: 'Vazirmatn, sans-serif', fontSize: 11, padding: 8, autoSkip: false, callback: function () { return ''; } },
                        scaleLabel: { display: true, labelString: 'Equipment State', fontColor: '#94a3b8', fontFamily: 'Vazirmatn, sans-serif', fontSize: 11 }
                    }, {
                        id: 'y-pwr', position: 'left',
                        gridLines: { display: false },
                        ticks: { fontColor: '#a78bfa', fontFamily: 'Vazirmatn, sans-serif', fontSize: 10, beginAtZero: true, padding: 4 },
                        scaleLabel: { display: true, labelString: 'توان (W)', fontColor: '#a78bfa', fontFamily: 'Vazirmatn, sans-serif', fontSize: 10 }
                    }, {
                        id: 'y-amp', position: 'right',
                        gridLines: { display: false },
                        ticks: { fontColor: '#f472b6', fontFamily: 'Vazirmatn, sans-serif', fontSize: 10, beginAtZero: true, padding: 4, callback: function (v) { return typeof v === 'number' ? v.toFixed(1) : v; } },
                        scaleLabel: { display: true, labelString: 'جریان (A)', fontColor: '#f472b6', fontFamily: 'Vazirmatn, sans-serif', fontSize: 10 }
                    }]
                },
                tooltips: {
                    enabled: true,
                    mode: 'index', intersect: false,
                    position: 'top',
                    backgroundColor: 'rgba(255,255,255,0.92)',
                    titleFontFamily: 'Vazirmatn, sans-serif', bodyFontFamily: 'Vazirmatn, sans-serif',
                    titleFontColor: '#1e293b', bodyFontColor: '#334155',
                    titleFontSize: 13, bodyFontSize: 12, titleFontStyle: 'bold',
                    xPadding: 16, yPadding: 10,
                    displayColors: true, bodySpacing: 5, titleSpacing: 6,
                    cornerRadius: 8, caretSize: 6, caretPadding: 4,
                    borderColor: 'rgba(0,0,0,0.08)', borderWidth: 1,
                    callbacks: {
                        title: function (items) {
                            var idx = items[0].index;
                            var t = window.monitoringProfessionalChart._labelsFa && window.monitoringProfessionalChart._labelsFa[idx] ? window.monitoringProfessionalChart._labelsFa[idx] : items[0].xLabel;
                            return '\u202B' + t + '\u202C';
                        },
                        label: function (item, data) {
                            var ds = item.datasetIndex;
                            var label = data.datasets[ds].label || '';
                            var v = item.yLabel;
                            var rle = '\u202B', pdf = '\u202C';
                            if (ds >= 2 && ds <= 5) {
                                var offsets = [0, 2, 4, 6];
                                var off = offsets[ds - 2] || 0;
                                var raw = Math.round(v - off);
                                return rle + label + ': ' + (raw >= 1 ? 'روشن' : 'خاموش') + pdf;
                            }
                            if (ds === 6) return rle + label + ': ' + (typeof v === 'number' ? v.toFixed(1) : v) + ' W' + pdf;
                            if (ds === 7) return rle + label + ': ' + (typeof v === 'number' ? v.toFixed(2) : v) + ' A' + pdf;
                            return rle + label + ': ' + (typeof v === 'number' ? v.toFixed(1) : v) + '°C' + pdf;
                        }
                    }
                },
                hover: { mode: 'index', intersect: false, animationDuration: 0 },
                events: ['mousemove', 'mouseout'],
                elements: { point: { radius: 0, hitRadius: 18, hoverRadius: 5 } }
            }
        });

        _proSetupScroll();
        _proBindCrosshair(professionalChart);
    },

    updateData: function (canvasId, data) {
        if (!professionalChart) return;

        var wrapper = document.getElementById('mpc-wrapper');
        var scrollEl = document.getElementById('mpc-scroll');

        if (wrapper) {
            var count = data.labels ? data.labels.length : 0;
            var pxPer = window.innerWidth < 768 ? 38 : 55;
            var w = Math.max(window.innerWidth, count * pxPer);
            w = Math.min(w, 80000);
            wrapper.style.width = w + 'px';
        }

        this._labelsFa = data.labelsFa || [];
        this._cycles = data.cycles || null;
        var labels = data.labels || [];
        professionalChart.data.labels = labels;
        professionalChart.data.datasets[0].data = data.temperatureRef || [];
        professionalChart.data.datasets[1].data = data.temperatureFreez || [];

        // Vertical bands: power(0-0.9), motor(2-3), heater1(4-5), heater2(6-7)
        professionalChart.data.datasets[2].data = (data.power || []).map(function(v) { return v === 1 ? 0.9 : 0; });
        professionalChart.data.datasets[3].data = (data.motor || []).map(function(v) { return v === 1 ? 3 : 2; });
        professionalChart.data.datasets[4].data = (data.heater1 || []).map(function(v) { return v === 1 ? 5 : 4; });
        professionalChart.data.datasets[5].data = (data.heater2 || []).map(function(v) { return v === 1 ? 7 : 6; });

        // Tavan (power) and Jaryan (current) — raw values
        professionalChart.data.datasets[6].data = data.tavan || [];
        professionalChart.data.datasets[7].data = data.jaryan || [];

        // Apply once-built gradient fills
        if (_proGradients[0]) professionalChart.data.datasets[0].backgroundColor = _proGradients[0];
        if (_proGradients[1]) professionalChart.data.datasets[1].backgroundColor = _proGradients[1];

        // All datasets hidden by default — user toggles via legend
        for (var i = 0; i < 8; i++) {
            if (professionalChart.data.datasets[i]) professionalChart.data.datasets[i].hidden = true;
        }

        if (wrapper) void wrapper.offsetHeight;
        professionalChart.resize();
        professionalChart.update(0);

        if (scrollEl) scrollEl.scrollLeft = 0;
    },

    toggleDataset: function (index) {
        if (!professionalChart || index < 0 || index > 7) return;
        var ds = professionalChart.data.datasets[index];
        if (!ds) return;
        ds.hidden = !ds.hidden;
        professionalChart.update(0);
        return !ds.hidden;
    },

    resize: function () {
        if (!professionalChart) return;
        try { professionalChart.resize(); professionalChart.update(0); } catch (e) {}
    },

    destroy: function () {
        if (professionalChart) {
            professionalChart.destroy();
            professionalChart = null;
        }
        this._labelsFa = [];
        this._cycles = null;
    }
};
