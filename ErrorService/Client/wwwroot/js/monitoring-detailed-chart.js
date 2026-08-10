var detailedChart = null;
var resizeObserver = null;

window.monitoringDetailedChart = {
    _timestamps: [],
    _timestampsFa: [],
    _noteData: [],
    _liveTimer: null,
    _isLive: false,
    _monitoringId: 0,
    _lastData: null,

    init: function (canvasId, monitoringId) {
        var canvas = document.getElementById(canvasId);
        if (!canvas) return;

        if (detailedChart) {
            detailedChart.destroy();
            detailedChart = null;
        }
        if (resizeObserver) {
            resizeObserver.disconnect();
            resizeObserver = null;
        }

        this._timestamps = [];
        this._timestampsFa = [];
        this._noteData = [];
        this._monitoringId = monitoringId;
        this._isLive = false;
        if (this._liveTimer) { clearInterval(this._liveTimer); this._liveTimer = null; }

        var isMobile = window.innerWidth < 768;

        detailedChart = new Chart(canvas.getContext('2d'), {
            type: 'line',
            data: {
                labels: [],
                datasets: [
                    { label: 'دمای یخچال', data: [], borderColor: 'rgba(53, 162, 235, 1)', backgroundColor: 'rgba(53, 162, 235, 0.1)', borderWidth: 2, pointRadius: 1, pointHoverRadius: 4, tension: 0.3, yAxisID: 'y1', order: 7 },
                    { label: 'دمای فریزر', data: [], borderColor: 'rgba(255, 0, 0, 1)', backgroundColor: 'rgba(255, 0, 0, 0.1)', borderWidth: 2, pointRadius: 1, pointHoverRadius: 4, tension: 0.3, yAxisID: 'y1', order: 6 },
                    { label: 'موتور', data: [], borderColor: 'rgba(255, 99, 132, 1)', backgroundColor: 'rgba(255, 99, 132, 0.2)', borderWidth: 2, pointRadius: 0, pointHoverRadius: 3, steppedLine: true, yAxisID: 'y2', order: 5 },
                    { label: 'برق دستگاه', data: [], borderColor: 'rgba(0, 0, 0, 1)', backgroundColor: 'rgba(0, 0, 0, 0)', borderWidth: 2, pointRadius: 0, pointHoverRadius: 3, steppedLine: true, yAxisID: 'y2', order: 4 },
                    { label: 'هیتر 1', data: [], borderColor: 'rgba(153, 102, 255, 1)', backgroundColor: 'rgba(153, 102, 255, 0.2)', borderWidth: 2, pointRadius: 0, pointHoverRadius: 3, steppedLine: true, yAxisID: 'y2', order: 3 },
                    { label: 'هیتر 2', data: [], borderColor: 'rgba(75, 192, 192, 1)', backgroundColor: 'rgba(75, 192, 192, 0.2)', borderWidth: 2, pointRadius: 0, pointHoverRadius: 3, steppedLine: true, yAxisID: 'y2', order: 2 },
                    { label: 'جریان', data: [], borderColor: 'rgba(0, 100, 0, 1)', backgroundColor: 'rgba(0, 100, 0, 0.1)', borderWidth: 2, pointRadius: 1, pointHoverRadius: 4, tension: 0.3, yAxisID: 'y1', order: 1 },
                    { label: 'توان', data: [], borderColor: 'rgba(255, 205, 86, 1)', backgroundColor: 'rgba(255, 205, 86, 0.1)', borderWidth: 2, pointRadius: 1, pointHoverRadius: 4, tension: 0.3, yAxisID: 'y1', order: 0 }
                ]
            },
            options: {
                responsive: false,
                maintainAspectRatio: false,
                animation: { duration: isMobile ? 0 : 800 },
                legend: { display: false },
                scales: {
                    xAxes: [{
                        type: 'category',
                        ticks: { maxTicksLimit: isMobile ? 8 : 20, fontFamily: 'Vazirmatn', maxRotation: isMobile ? 90 : 45, autoSkip: true, autoSkipPadding: 30 },
                        gridLines: { display: true, color: 'rgba(0,0,0,0.05)' }
                    }],
                    yAxes: [
                        { id: 'y1', position: 'left', ticks: { fontFamily: 'Vazirmatn', callback: function (v) { return v + '\u00B0C'; } }, gridLines: { display: true, color: 'rgba(0,0,0,0.05)' } },
                        { id: 'y2', position: 'right', ticks: { min: 0, max: 1, stepSize: 1, display: false }, gridLines: { display: false } }
                    ]
                },
                tooltips: {
                    enabled: true, mode: 'index', intersect: false,
                    backgroundColor: 'rgba(0,0,0,0.8)', titleFontFamily: 'Vazirmatn', bodyFontFamily: 'Vazirmatn',
                    callbacks: {
                        label: function (item, data) {
                            var label = data.datasets[item.datasetIndex].label || '';
                            var val = item.yLabel;
                            if (item.datasetIndex >= 2 && item.datasetIndex <= 5) {
                                return label + ': ' + (val === 1 ? 'روشن' : 'خاموش');
                            }
                            if (item.datasetIndex === 6) return label + ': ' + (val != null ? val.toFixed(2) + ' A' : '---');
                            if (item.datasetIndex === 7) return label + ': ' + (val != null ? val.toFixed(1) + ' W' : '---');
                            return label + ': ' + (val != null ? val.toFixed(1) + '\u00B0C' : '---');
                        }
                    }
                },
                hover: { mode: 'index', intersect: false },
                pan: { enabled: true, mode: 'x', speed: 20, threshold: 10 },
                zoom: { enabled: true, mode: 'x', sensitivity: 3, speed: 0.1 }
            }
        });

        // مخفی کردن سری‌های پیش‌فرض (فقط دمای یخچال و فریزر نمایش داده شود)
        detailedChart.getDatasetMeta(2).hidden = true;
        detailedChart.getDatasetMeta(3).hidden = true;
        detailedChart.getDatasetMeta(4).hidden = true;
        detailedChart.getDatasetMeta(5).hidden = true;
        detailedChart.getDatasetMeta(6).hidden = true;
        detailedChart.getDatasetMeta(7).hidden = true;
        detailedChart.update();

        var self = this;
        resizeObserver = new ResizeObserver(function () {
            if (!detailedChart || !detailedChart.canvas) return;
            var container = detailedChart.canvas.parentElement;
            if (!container) return;
            var dpr = window.devicePixelRatio || 1;
            var newHeight = Math.round(container.clientHeight * dpr);
            if (detailedChart.canvas.height !== newHeight) {
                detailedChart.canvas.style.height = container.clientHeight + 'px';
                detailedChart.canvas.height = newHeight;
                detailedChart.resize();
            }
        });
        resizeObserver.observe(canvas.parentElement);

        // کلیک برای نمایش یادداشت
        canvas.onclick = function (evt) {
            self._handleChartClick(evt);
        };
    },

    updateData: function (data) {
        if (!detailedChart) return;
        this._lastData = data;

        this._timestamps = data.timestamps || [];
        this._timestampsFa = data.timestampsFa || [];
        this._noteData = data.note || [];

        detailedChart.data.labels = this._timestampsFa.length > 0 ? this._timestampsFa : this._timestamps;
        detailedChart.data.datasets[0].data = data.temperatureRef || [];
        detailedChart.data.datasets[1].data = data.temperatureFreez || [];
        detailedChart.data.datasets[2].data = data.motorState || [];
        detailedChart.data.datasets[3].data = data.bargh || [];
        detailedChart.data.datasets[4].data = data.element1 || [];
        detailedChart.data.datasets[5].data = data.element2 || [];
        detailedChart.data.datasets[6].data = data.jaryan || [];
        detailedChart.data.datasets[7].data = data.power || [];
        detailedChart.update();

        this._adjustCanvasWidth();

        this._updateStatusTable(data);
        this._updateEnergyCards(data);

        document.getElementById('connRecordCount').textContent = data.totalCount || data.timestamps.length;
        if (this._timestampsFa.length > 0) {
            document.getElementById('connLastTs').textContent = 'آخرین: ' + this._timestampsFa[this._timestampsFa.length - 1];
        } else if (data.timestamps.length > 0) {
            document.getElementById('connLastTs').textContent = 'آخرین: ' + data.timestamps[data.timestamps.length - 1];
        }
    },

    toggleLine: function (checkbox) {
        var index = parseInt(checkbox.getAttribute('data-index'));
        if (detailedChart && detailedChart.data.datasets[index]) {
            var meta = detailedChart.getDatasetMeta(index);
            meta.hidden = !checkbox.checked;
            detailedChart.update();
        }
    },

    startLive: function () {
        if (this._isLive) return;
        this._isLive = true;
        var self = this;
        this._liveTimer = setInterval(function () {
            self._fetchLiveData();
        }, 10000);
    },

    stopLive: function () {
        this._isLive = false;
        if (this._liveTimer) {
            clearInterval(this._liveTimer);
            this._liveTimer = null;
        }
    },

    destroy: function () {
        this.stopLive();
        if (resizeObserver) {
            resizeObserver.disconnect();
            resizeObserver = null;
        }
        if (detailedChart) {
            detailedChart.destroy();
            detailedChart = null;
        }
    },

    _fetchLiveData: function () {
        var self = this;
        var now = new Date();
        var fiveMinAgo = new Date(now.getTime() - 5 * 60 * 1000);
        var url = '/api/monitoring/chart/' + self._monitoringId + '/detailed-records?count=500&startDate=' + fiveMinAgo.toISOString();
        fetch(url)
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (data && data.timestamps && data.timestamps.length) {
                    self.updateData(data);
                }
            }).catch(function () { });
    },

    _handleChartClick: function (evt) {
        if (!detailedChart) return;
        var active = detailedChart.getElementsAtEvent(evt);
        if (active && active.length) {
            var idx = active[0]._index;
            if (idx >= 0 && this._noteData && this._noteData[idx]) {
                this._showNoteModal(idx);
            }
        }
    },

    _showNoteModal: function (index) {
        if (!this._timestamps[index]) return;
        var noteText = this._noteData[index] || '';

        var existing = document.getElementById('dcNoteModal');
        if (existing) existing.remove();

        var modal = document.createElement('div');
        modal.id = 'dcNoteModal';
        modal.style.cssText = 'position:fixed;top:0;left:0;right:0;bottom:0;background:rgba(0,0,0,0.5);z-index:99999;display:flex;align-items:center;justify-content:center;font-family:Vazirmatn,Tahoma;';
        modal.onclick = function () { modal.remove(); };

        var card = document.createElement('div');
        card.style.cssText = 'background:#fff;border-radius:12px;padding:24px;max-width:500px;width:90%;box-shadow:0 20px 60px rgba(0,0,0,0.3);direction:rtl;';
        card.onclick = function (e) { e.stopPropagation(); };

        card.innerHTML = '<div style="font-size:18px;font-weight:800;margin-bottom:16px;color:#1e293b;">یادداشت</div>' +
            '<div style="font-size:13px;color:#64748b;margin-bottom:8px;">زمان: ' + this._timestamps[index] + '</div>' +
            '<textarea id="dcNoteText" style="width:100%;min-height:100px;border:1px solid #e2e8f0;border-radius:8px;padding:12px;font-family:inherit;font-size:13px;resize:vertical;box-sizing:border-box;" placeholder="متن یادداشت...">' + noteText + '</textarea>' +
            '<div style="display:flex;gap:10px;margin-top:16px;justify-content:flex-end;">' +
            '<button id="dcNoteCancel" style="padding:8px 20px;border:1px solid #e2e8f0;border-radius:8px;background:#fff;cursor:pointer;font-family:inherit;font-size:13px;">انصراف</button>' +
            '<button id="dcNoteSave" style="padding:8px 20px;border:none;border-radius:8px;background:#2563eb;color:#fff;cursor:pointer;font-family:inherit;font-size:13px;font-weight:700;">ذخیره</button>' +
            '</div>';

        modal.appendChild(card);
        document.body.appendChild(modal);

        document.getElementById('dcNoteCancel').onclick = function () { modal.remove(); };

        var self = this;
        document.getElementById('dcNoteSave').onclick = function () {
            var text = document.getElementById('dcNoteText').value;
            self._saveNote(index, text);
            modal.remove();
        };
    },

    _saveNote: function (index, text) {
        if (!this._timestamps[index]) return;
        var id = 0;
        if (window.__detailedChartData && window.__detailedChartData.ids && window.__detailedChartData.ids[index]) {
            id = window.__detailedChartData.ids[index];
        }
        if (!id) return;

        fetch('/api/monitoring/note', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ recordId: id, note: text })
        }).then(function (r) { return r.json(); }).then(function (res) {
            if (res && res.success) {
                if (window.__detailedChartData && window.__detailedChartData.note) {
                    window.__detailedChartData.note[index] = text;
                }
                if (self._noteData) self._noteData[index] = text;
            }
        }).catch(function () { });
    },

    _adjustCanvasWidth: function () {
        if (!detailedChart) return;
        var canvas = detailedChart.canvas;
        if (!canvas) return;
        var container = canvas.parentElement;
        if (!container) return;

        var dpr = window.devicePixelRatio || 1;
        var labels = detailedChart.data.labels || [];
        var n = labels.length;
        if (n > 0) {
            var spacingPx = 50;
            var idealWidth = n * spacingPx;
            var minWidth = container.parentElement ? container.parentElement.clientWidth || 1200 : 1200;
            var useWidth = Math.max(minWidth, idealWidth);
            var maxWidth = parseInt(canvas.getAttribute('data-max-scroll-width')) || 25000;
            if (useWidth > maxWidth) useWidth = maxWidth;
            var contHeight = container.clientHeight;
            canvas.style.width = useWidth + 'px';
            canvas.style.height = contHeight + 'px';
            canvas.width = Math.round(useWidth * dpr);
            canvas.height = Math.round(contHeight * dpr);
            void container.offsetWidth;
        } else {
            var contHeight = container.clientHeight || 400;
            var contWidth = container.clientWidth || 1200;
            canvas.style.width = '100%';
            canvas.style.height = '100%';
            canvas.width = Math.round(contWidth * dpr);
            canvas.height = Math.round(contHeight * dpr);
        }
        detailedChart.resize();
    },

    _updateStatusTable: function (data) {
        var motor = data.motorState || [];
        var el1 = data.element1 || [];
        var el2 = data.element2 || [];

        function getLastValue(arr) { return arr.length > 0 ? arr[arr.length - 1] : 0; }
        function getStatus(val) { return val === 1 ? 'روشن' : 'خاموش'; }
        function getBadgeClass(val) { return val === 1 ? 'badge-on' : 'badge-off'; }
        function getStopBeforeStart(arr) {
            var lastStart = -1, stopDuration = 0;
            for (var i = arr.length - 1; i >= 0; i--) {
                if (arr[i] === 1 && lastStart === -1) lastStart = i;
                if (arr[i] === 0 && lastStart !== -1) {
                    var count = 0;
                    for (var j = i; j >= 0 && arr[j] === 0; j--) count++;
                    stopDuration = count;
                    break;
                }
            }
            return stopDuration > 0 ? stopDuration + ' نقطه' : '---';
        }

        document.getElementById('statusMotor').textContent = getStatus(getLastValue(motor));
        document.getElementById('statusMotor').className = 'badge-status ' + getBadgeClass(getLastValue(motor));
        document.getElementById('stopBeforeStartMotor').textContent = getStopBeforeStart(motor);
        document.getElementById('lastStartMotor').textContent = this._getLastStartTime(motor);

        document.getElementById('statusHeater1').textContent = getStatus(getLastValue(el1));
        document.getElementById('statusHeater1').className = 'badge-status ' + getBadgeClass(getLastValue(el1));
        document.getElementById('stopBeforeStartHeater1').textContent = getStopBeforeStart(el1);
        document.getElementById('lastStartHeater1').textContent = this._getLastStartTime(el1);

        document.getElementById('statusHeater2').textContent = getStatus(getLastValue(el2));
        document.getElementById('statusHeater2').className = 'badge-status ' + getBadgeClass(getLastValue(el2));
        document.getElementById('stopBeforeStartHeater2').textContent = getStopBeforeStart(el2);
        document.getElementById('lastStartHeater2').textContent = this._getLastStartTime(el2);
    },

    _getLastStartTime: function (arr) {
        for (var i = arr.length - 1; i >= 0; i--) {
            if (arr[i] === 1) {
                return this._timestampsFa[i] || this._timestamps[i] || '---';
            }
        }
        return '---';
    },

    _updateEnergyCards: function (data) {
        var ts = data.timestamps || [];
        var powerVals = data.power || data.kw || [];
        var motor = data.motorState || [];
        var el1 = data.element1 || [];
        var el2 = data.element2 || [];

        if (!ts.length || !powerVals.length) {
            document.getElementById('totalEnergy').textContent = '0.00 kWh';
            document.getElementById('totalActiveTime').textContent = '00:00:00 فعالیت';
            document.getElementById('equipmentEnergyDetails').innerHTML = '<div class="text-muted text-center py-2"><small>داده‌ای موجود نیست</small></div>';
            document.getElementById('totalDailyEstimate').textContent = '-- kWh';
            document.getElementById('totalMonthlyEstimate').textContent = '-- kWh';
            document.getElementById('totalYearlyEstimate').textContent = '-- kWh';
            return;
        }

        var totalEnergy = 0, activeCount = 0, motorEnergy = 0, motorCount = 0, el1Energy = 0, el1Count = 0, el2Energy = 0, el2Count = 0;

        for (var i = 0; i < ts.length; i++) {
            var p = parseFloat(powerVals[i]) || 0;
            if (p <= 0) continue;

            var anyActive = false;
            if (motor[i] === 1) { motorEnergy += p; motorCount++; anyActive = true; }
            if (el1[i] === 1) { el1Energy += p; el1Count++; anyActive = true; }
            if (el2[i] === 1) { el2Energy += p; el2Count++; anyActive = true; }
            if (anyActive) { totalEnergy += p; activeCount++; }
        }

        var dataPointsPerHour = ts.length > 1 ? ts.length / ((new Date(ts[ts.length - 1]).getTime() - new Date(ts[0]).getTime()) / 3600000) : 1;
        if (dataPointsPerHour < 1) dataPointsPerHour = 1;

        totalEnergy = (totalEnergy / 1000) / dataPointsPerHour;
        var totalHours = activeCount / dataPointsPerHour;

        function fmtHours(h) {
            var totalSec = Math.round(h * 3600);
            var hh = Math.floor(totalSec / 3600);
            var mm = Math.floor((totalSec % 3600) / 60);
            var ss = totalSec % 60;
            return (hh < 10 ? '0' + hh : hh) + ':' + (mm < 10 ? '0' + mm : mm) + ':' + (ss < 10 ? '0' + ss : ss);
        }

        document.getElementById('totalEnergy').textContent = totalEnergy.toFixed(2) + ' kWh';
        document.getElementById('totalActiveTime').textContent = fmtHours(totalHours) + ' فعالیت';

        var detailsHtml = '';
        var equipments = [
            { name: 'موتور', energy: (motorEnergy / 1000) / dataPointsPerHour, time: motorCount / dataPointsPerHour },
            { name: 'هیتر 1', energy: (el1Energy / 1000) / dataPointsPerHour, time: el1Count / dataPointsPerHour },
            { name: 'هیتر 2', energy: (el2Energy / 1000) / dataPointsPerHour, time: el2Count / dataPointsPerHour }
        ];

        equipments.forEach(function (eq) {
            detailsHtml += '<div style="display:flex;justify-content:space-between;align-items:center;padding:8px 0;border-bottom:1px solid #f1f5f9;">' +
                '<span style="font-weight:700;font-size:13px;color:#334155;">' + eq.name + '</span>' +
                '<div style="display:flex;gap:16px;direction:ltr;">' +
                '<span style="font-weight:800;font-size:13px;color:#16a34a;">' + eq.energy.toFixed(2) + ' kWh</span>' +
                '<span style="font-weight:600;font-size:12px;color:#64748b;">' + fmtHours(eq.time) + '</span>' +
                '</div></div>';
        });

        document.getElementById('equipmentEnergyDetails').innerHTML = detailsHtml;

        var durationDays = ts.length > 1 ? (new Date(ts[ts.length - 1]).getTime() - new Date(ts[0]).getTime()) / 86400000 : 1;
        if (durationDays < 0.01) durationDays = 0.01;

        var daily = totalEnergy / durationDays;
        document.getElementById('totalDailyEstimate').textContent = daily.toFixed(1) + ' kWh';
        document.getElementById('totalMonthlyEstimate').textContent = (daily * 30).toFixed(0) + ' kWh';
        document.getElementById('totalYearlyEstimate').textContent = (daily * 365).toFixed(0) + ' kWh';

        var estRows = '';
        equipments.forEach(function (eq) {
            var d = eq.energy / durationDays;
            estRows += '<tr><td>' + eq.name + '</td><td>' + d.toFixed(1) + ' kWh</td><td>' + (d * 30).toFixed(0) + ' kWh</td><td>' + (d * 365).toFixed(0) + ' kWh</td></tr>';
        });
        var estBody = document.getElementById('estimationTableBody');
        if (estBody) estBody.innerHTML = estRows;
    }
};

// --------------------------------------------------------------
// توابع سراسری (برای استفاده در onclick های HTML)
// --------------------------------------------------------------
function toggleDetailedLine(checkbox) {
    if (window.monitoringDetailedChart) {
        window.monitoringDetailedChart.toggleLine(checkbox);
    }
}

function loadDetailedChartData() {
    var startInput = document.getElementById('startDateTime');
    var endInput = document.getElementById('endDateTime');
    var startVal = startInput ? startInput.value.trim() : '';
    var endVal = endInput ? endInput.value.trim() : '';

    if (!startVal || !endVal) {
        showDetailedError('لطفاً فیلدهای شروع و پایان را تکمیل کنید.');
        return;
    }

    var btn = document.getElementById('btnLoadData');
    if (btn) { btn.disabled = true; btn.textContent = 'در حال بارگذاری...'; }

    // استفاده از monitoringId ذخیره شده
    var id = window.monitoringDetailedChart ? window.monitoringDetailedChart._monitoringId : 0;
    if (!id) {
        // fallback: از URL استخراج کن
        var match = window.location.pathname.match(/\/detailed-chart\/(\d+)/);
        id = match ? match[1] : 0;
    }

    var url = '/api/monitoring/chart/' + id + '/detailed-records?count=100000&maxPoints=500';
    url += '&startDate=' + encodeURIComponent(startVal);
    url += '&endDate=' + encodeURIComponent(endVal);

    fetch(url)
        .then(function (r) {
            if (!r.ok) throw new Error('خطا در دریافت داده');
            return r.json();
        })
        .then(function (data) {
            hideDetailedError();
            window.__detailedChartData = data;
            if (window.monitoringDetailedChart) {
                window.monitoringDetailedChart.updateData(data);
            }
        })
        .catch(function (err) {
            showDetailedError(err.message || 'خطا در دریافت داده');
        })
        .finally(function () {
            if (btn) { btn.disabled = false; btn.textContent = 'بارگذاری داده'; }
        });
}

function toggleDetailedLive(checkbox) {
    if (checkbox.checked) {
        if (window.monitoringDetailedChart) {
            window.monitoringDetailedChart.startLive();
        }
    } else {
        if (window.monitoringDetailedChart) {
            window.monitoringDetailedChart.stopLive();
        }
    }
}

function showDetailedError(msg) {
    var el = document.getElementById('errorMessage');
    if (el) { el.textContent = msg; el.style.display = 'block'; }
}

function hideDetailedError() {
    var el = document.getElementById('errorMessage');
    if (el) { el.style.display = 'none'; }
}

function updateDetailedDateRange(firstFa, lastFa, totalCount, isOnline) {
    var startInput = document.getElementById('startDateTime');
    var endInput = document.getElementById('endDateTime');
    var loadBtn = document.getElementById('btnLoadData');

    if (!startInput || !endInput) return;

    if (firstFa) {
        startInput.setAttribute('data-jdp-min-date', firstFa);
        startInput.value = firstFa;
    }
    if (lastFa) {
        endInput.setAttribute('data-jdp-max-date', lastFa);
        endInput.value = lastFa;
    }

    if (loadBtn) {
        loadBtn.disabled = false;
    }

    if (totalCount != null) {
        var rc = document.getElementById('connRecordCount');
        if (rc) rc.textContent = totalCount;
    }

    if (lastFa) {
        var lt = document.getElementById('connLastTs');
        if (lt) lt.textContent = 'آخرین: ' + lastFa;
    }

    if (isOnline != null) {
        var badge = document.getElementById('connBadge');
        if (badge) {
            badge.textContent = isOnline ? 'آنلاین' : 'آفلاین';
            badge.className = 'dc-conn-badge' + (isOnline ? ' online' : '');
        }
    }
}