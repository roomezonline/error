// چارت‌های صفحات آمار بازدید و تحلیلگر هاست (Chart.js v2.9.4)
window.adminInsights = {
    _charts: {},

    _destroy: function (id) {
        if (this._charts[id]) {
            try { this._charts[id].destroy(); } catch (e) { }
            delete this._charts[id];
        }
    },

    // نمودار خطی بازدیدها و بازدیدکنندگان یکتا
    // labels: برچسب کوتاه محور X (روز/ماه شمسی)
    // fullDates: تاریخ کامل شمسی هر نقطه (برای تولتیپ و کادر اطلاعات)
    // readoutId: شناسه المانی که جزئیات نقطه کلیک‌شده در آن نمایش داده می‌شود
    renderTrend: function (canvasId, labels, visits, uniques, fullDates, readoutId) {
        var canvas = document.getElementById(canvasId);
        if (!canvas || typeof Chart === 'undefined') return false;
        this._destroy(canvasId);
        var ctx = canvas.getContext('2d');

        var dates = (fullDates && fullDates.length) ? fullDates : labels;
        var readout = readoutId || null;

        function faNum(value) {
            var s = String(value), out = '';
            for (var i = 0; i < s.length; i++) {
                var c = s.charCodeAt(i);
                out += (c >= 48 && c <= 57) ? String.fromCharCode(1776 + (c - 48)) : s.charAt(i);
            }
            return out;
        }

        function updateReadout(index) {
            if (!readout) return;
            var el = document.getElementById(readout);
            if (!el) return;

            if (index === null || index === undefined || index < 0 || index >= labels.length) {
                el.className = 'an-trend-readout';
                el.textContent = 'برای مشاهده جزئیات هر روز، روی نمودار کلیک کنید';
                return;
            }

            el.className = 'an-trend-readout an-trend-readout--active';
            el.innerHTML =
                '<span class="an-trend-readout__date">' + dates[index] + '</span>' +
                '<span class="an-trend-readout__item"><i class="dot" style="background:#6366f1"></i>بازدید: <b>' + faNum(visits[index]) + '</b></span>' +
                '<span class="an-trend-readout__item"><i class="dot" style="background:#14b8a6"></i>بازدیدکننده یکتا: <b>' + faNum(uniques[index]) + '</b></span>' +
                '<span class="an-trend-readout__hint">برای پاک کردن انتخاب، دوباره کلیک کنید</span>';
        }

        updateReadout(null);

        // خط عمودی + نشانگرهای نقطه انتخاب‌شده
        var crosshair = {
            id: 'crosshair',
            afterDatasetsDraw: function (chart) {
                var idx = chart._selected;
                if (idx === null || idx === undefined || idx < 0 || idx >= labels.length) return;

                var xScale = chart.scales['x-axis-0'] || chart.scales['x'];
                var yScale = chart.scales['y-axis-0'] || chart.scales['y'];
                if (!xScale || !yScale) return;

                var x = xScale.getPixelForValue(idx);
                var area = chart.chartArea;
                var c = chart.ctx;

                c.save();
                c.beginPath();
                c.moveTo(x, area.top);
                c.lineTo(x, area.bottom);
                c.lineWidth = 1.5;
                c.strokeStyle = 'rgba(99, 102, 241, 0.75)';
                c.setLineDash([5, 4]);
                c.stroke();
                c.setLineDash([]);

                function dot(value, color) {
                    var y = yScale.getPixelForValue(value);
                    c.beginPath();
                    c.arc(x, y, 5.5, 0, Math.PI * 2);
                    c.fillStyle = color;
                    c.fill();
                    c.lineWidth = 2.5;
                    c.strokeStyle = '#fff';
                    c.stroke();
                }

                dot(visits[idx], '#6366f1');
                dot(uniques[idx], '#14b8a6');
                c.restore();
            }
        };

        var gradVisits = ctx.createLinearGradient(0, 0, 0, 300);
        gradVisits.addColorStop(0, 'rgba(99, 102, 241, 0.28)');
        gradVisits.addColorStop(1, 'rgba(99, 102, 241, 0.02)');

        var gradUniques = ctx.createLinearGradient(0, 0, 0, 300);
        gradUniques.addColorStop(0, 'rgba(20, 184, 166, 0.20)');
        gradUniques.addColorStop(1, 'rgba(20, 184, 166, 0.02)');

        this._charts[canvasId] = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'بازدیدها',
                        data: visits,
                        borderColor: '#6366f1',
                        backgroundColor: gradVisits,
                        borderWidth: 2.5,
                        fill: true,
                        lineTension: 0.35,
                        pointRadius: 0,
                        pointHoverRadius: 5,
                        pointHoverBackgroundColor: '#6366f1',
                        pointHoverBorderColor: '#fff',
                        pointHoverBorderWidth: 2
                    },
                    {
                        label: 'بازدیدکنندگان یکتا',
                        data: uniques,
                        borderColor: '#14b8a6',
                        backgroundColor: gradUniques,
                        borderWidth: 2.5,
                        fill: true,
                        lineTension: 0.35,
                        pointRadius: 0,
                        pointHoverRadius: 5,
                        pointHoverBackgroundColor: '#14b8a6',
                        pointHoverBorderColor: '#fff',
                        pointHoverBorderWidth: 2
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                legend: {
                    position: 'top',
                    align: 'end',
                    labels: {
                        fontFamily: 'Vazir, Tahoma, sans-serif',
                        fontSize: 12,
                        usePointStyle: true,
                        padding: 16,
                        fontColor: '#475569'
                    }
                },
                tooltips: {
                    mode: 'index',
                    intersect: false,
                    backgroundColor: 'rgba(15, 23, 42, 0.92)',
                    titleFont: { family: 'Vazir, Tahoma, sans-serif', size: 12 },
                    bodyFont: { family: 'Vazir, Tahoma, sans-serif', size: 12 },
                    cornerRadius: 8,
                    displayColors: true,
                    boxWidth: 8,
                    boxHeight: 8,
                    padding: 10,
                    callbacks: {
                        title: function (items) {
                            if (!items || !items.length) return '';
                            return dates[items[0].index];
                        },
                        label: function (item, data) {
                            var dsLabel = data.datasets[item.datasetIndex].label || '';
                            return ' ' + dsLabel + ': ' + faNum(item.yLabel !== undefined ? item.yLabel : item.value);
                        }
                    }
                },
                hover: { mode: 'index', intersect: false },
                onHover: function (event) {
                    if (event && event.target && event.target.style) {
                        event.target.style.cursor = 'pointer';
                    }
                },
                onClick: function (event, active) {
                    var chart = this;
                    var area = chart.chartArea;
                    if (!area) return;

                    var idx = null;
                    if (active && active.length) {
                        idx = (active[0]._index !== undefined && active[0]._index !== null)
                            ? active[0]._index
                            : active[0].index;
                    }

                    var clientX = null, clientY = null;
                    if (event && typeof event.clientX === 'number') {
                        clientX = event.clientX;
                        clientY = event.clientY;
                    } else if (event && event.touches && event.touches.length) {
                        clientX = event.touches[0].clientX;
                        clientY = event.touches[0].clientY;
                    }

                    if (clientX === null || clientY === null) {
                        if (idx === null) return;
                    } else {
                        var rect = chart.canvas.getBoundingClientRect();
                        var x = clientX - rect.left;
                        var y = clientY - rect.top;
                        if (x < area.left || x > area.right || y < area.top || y > area.bottom) return;

                        if (idx === null || idx === undefined) {
                            var xScale = chart.scales['x-axis-0'] || chart.scales['x'];
                            if (xScale && xScale.getValueForPixel) {
                                var value = xScale.getValueForPixel(x);
                                if (typeof value === 'number' && !isNaN(value)) idx = Math.round(value);
                            }
                        }
                    }

                    if (typeof idx === 'number' && (idx < 0 || idx >= labels.length)) idx = null;
                    if (idx !== null && chart._selected === idx) idx = null;

                    chart._selected = idx;
                    updateReadout(idx);
                    chart.update(0);
                },
                scales: {
                    xAxes: [{
                        gridLines: { display: false, drawBorder: false },
                        ticks: {
                            maxTicksLimit: 12,
                            autoSkip: true,
                            maxRotation: 0,
                            fontColor: '#94a3b8',
                            fontSize: 10,
                            fontFamily: 'Vazir, Tahoma, sans-serif'
                        }
                    }],
                    yAxes: [{
                        beginAtZero: true,
                        ticks: {
                            precision: 0,
                            fontColor: '#94a3b8',
                            fontSize: 10,
                            fontFamily: 'Vazir, Tahoma, sans-serif',
                            padding: 8
                        },
                        gridLines: {
                            color: 'rgba(148, 163, 184, 0.14)',
                            drawBorder: false
                        }
                    }]
                }
            },
            plugins: [crosshair]
        });

        return true;
    },

    // نمودار دایره‌ای اشغال فضا با متن وسط
    renderDonut: function (canvasId, usedPercent, usedLabel, freeLabel) {
        var canvas = document.getElementById(canvasId);
        if (!canvas || typeof Chart === 'undefined') return;
        this._destroy(canvasId);
        var ctx = canvas.getContext('2d');
        var pct = Math.max(0, Math.min(100, usedPercent || 0));

        var centerText = {
            id: 'centerText',
            afterDraw: function (chart) {
                var w = chart.width, h = chart.height;
                var c = chart.ctx;
                c.save();
                c.textAlign = 'center';
                c.textBaseline = 'middle';
                c.fillStyle = '#334155';
                c.font = '900 24px Vazir, Tahoma, sans-serif';
                var txt = String(pct).replace(/\./g, '٫') + '٪';
                c.fillText(txt, w / 2, h / 2 - 8);
                c.fillStyle = '#94a3b8';
                c.font = '600 11px Vazir, Tahoma, sans-serif';
                c.fillText('فضای اشغال‌شده', w / 2, h / 2 + 16);
                c.restore();
            }
        };

        this._charts[canvasId] = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: [usedLabel, freeLabel],
                datasets: [{
                    data: [pct, Math.round((100 - pct) * 10) / 10],
                    backgroundColor: ['#6366f1', '#e2e8f0'],
                    hoverBackgroundColor: ['#4f46e5', '#cbd5e1'],
                    borderWidth: 0,
                    hoverOffset: 6
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutoutPercentage: 76,
                legend: {
                    position: 'bottom',
                    labels: {
                        fontFamily: 'Vazir, Tahoma, sans-serif',
                        fontSize: 12,
                        usePointStyle: true,
                        padding: 14,
                        fontColor: '#475569'
                    }
                },
                tooltips: {
                    backgroundColor: 'rgba(15, 23, 42, 0.92)',
                    titleFont: { family: 'Vazir, Tahoma, sans-serif', size: 12 },
                    bodyFont: { family: 'Vazir, Tahoma, sans-serif', size: 12 },
                    cornerRadius: 8,
                    callbacks: {
                        label: function (item, data) {
                            return ' ' + data.labels[item.index] + ': ' + String(item.yLabel !== undefined ? item.yLabel : item.value).replace(/\./g, '٫') + '٪';
                        }
                    }
                }
            },
            plugins: [centerText]
        });
    }
};
