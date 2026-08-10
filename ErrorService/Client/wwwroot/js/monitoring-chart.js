var temperatureChart = null;
var chartInitialized = false;

function calculateOperationTime(dataset, timeLabels) {
    var operationTimes = [];
    var startTime = null;
    var startIndex = 0;
    var lastValue = 0;

    for (var i = 0; i < dataset.length; i++) {
        var currentValue = dataset[i];
        if (lastValue === 0 && currentValue === 1) {
            startTime = timeLabels[i];
            startIndex = i;
        }
        if (lastValue === 1 && currentValue === 0 && startTime !== null) {
            var endTime = timeLabels[i];
            var diffTime = calculateTimeDifference(startTime, endTime);
            operationTimes.push({
                startIndex: startIndex,
                endIndex: i,
                time: diffTime,
                startTime: startTime,
                endTime: endTime
            });
            startTime = null;
        }
        lastValue = currentValue;
    }
    return operationTimes;
}

function calculateTimeDifference(startTimeStr, endTimeStr) {
    try {
        var startParts = startTimeStr.split(':').map(Number);
        var endParts = endTimeStr.split(':').map(Number);
        var startTotalSeconds = startParts[0] * 3600 + startParts[1] * 60 + startParts[2];
        var endTotalSeconds = endParts[0] * 3600 + endParts[1] * 60 + endParts[2];
        if (endTotalSeconds < startTotalSeconds) {
            endTotalSeconds += 24 * 3600;
        }
        var diffSeconds = endTotalSeconds - startTotalSeconds;
        var hours = Math.floor(diffSeconds / 3600);
        var minutes = Math.floor((diffSeconds % 3600) / 60);
        var seconds = diffSeconds % 60;
        return (hours < 10 ? '0' + hours : hours) + ':' +
            (minutes < 10 ? '0' + minutes : minutes) + ':' +
            (seconds < 10 ? '0' + seconds : seconds);
    } catch (e) {
        return "00:00:00";
    }
}

Chart.plugins.register({
    afterDatasetsDraw: function (chart) {
        if (!chart.data || !chart.data.datasets) return;
        var ctx = chart.ctx;
        var timeLabels = chart.data.labels;
        var datasets = [
            { index: 2, name: 'موتور', color: 'rgba(255, 159, 64, 1)' },
            { index: 3, name: 'هیتر ۱', color: 'rgba(128, 0, 0, 1)' },
            { index: 4, name: 'هیتر ۲', color: 'rgba(75, 75, 192, 1)' },
            { index: 5, name: 'برق دستگاه', color: 'rgba(19, 19, 20, 1)' }
        ];
        datasets.forEach(function (dataset) {
            var meta = chart.getDatasetMeta(dataset.index);
            if (meta.hidden) return;
            var data = chart.data.datasets[dataset.index].data;
            var operationTimes = calculateOperationTime(data, timeLabels);
            operationTimes.forEach(function (opTime) {
                var startIndex = opTime.startIndex;
                var endIndex = opTime.endIndex;
                if (!meta.data || startIndex >= meta.data.length || !meta.data[startIndex] ||
                    endIndex >= meta.data.length || !meta.data[endIndex]) return;
                try {
                    var startX = meta.data[startIndex]._model ? meta.data[startIndex]._model.x : 0;
                    var endX = meta.data[endIndex]._model ? meta.data[endIndex]._model.x : 0;
                    var middleX = (startX + endX) / 2;
                    var yPos = meta.data[startIndex]._model ? meta.data[startIndex]._model.y : 0;
                    var yBottom = chart.scales['y-axis-1'].bottom;
                    var stepHeight = yBottom - yPos;
                    if (stepHeight < 30) return;
                    var text = opTime.time;
                    ctx.save();
                    ctx.font = 'bold 12px Vazirmatn';
                    var textWidth = ctx.measureText(text).width;
                    var textHeight = 15;
                    if (dataset.index == 2) ctx.translate(middleX, yPos + stepHeight / 1.5);
                    if (dataset.index == 3) ctx.translate(middleX, yPos + stepHeight / 2);
                    if (dataset.index == 4) ctx.translate(middleX, yPos + stepHeight / 3.3);
                    if (dataset.index == 5) ctx.translate(middleX, yPos + stepHeight / 2.5);
                    ctx.fillStyle = 'rgba(255, 255, 255, 0.8)';
                    ctx.fillRect(-textHeight / 2 - 5, -textWidth / 2 - 5, textHeight + 10, textWidth + 10);
                    ctx.rotate(-Math.PI / 2);
                    ctx.fillStyle = dataset.color;
                    ctx.textAlign = 'center';
                    ctx.textBaseline = 'middle';
                    ctx.fillText(text, 0, 0);
                    ctx.restore();
                } catch (e) { }
            });
        });
    }
});

Chart.plugins.register({
    afterDraw: function (chart) {
        var ys = chart.scales['y-axis-1'];
        if (!ys) return;
        if (window.innerWidth < 768 && !chart.tooltip._active) return;
        if (chart.tooltip._active && chart.tooltip._active.length) {
            var activePoint = chart.tooltip._active[0];
            var pos = activePoint.tooltipPosition();
            if (!pos) return;
            var ctx = chart.ctx;
            ctx.save();
            ctx.beginPath();
            ctx.moveTo(pos.x, ys.top);
            ctx.lineTo(pos.x, ys.bottom);
            ctx.lineWidth = 2;
            ctx.strokeStyle = 'rgba(0, 0, 0, 1)';
            ctx.stroke();
            ctx.restore();
        }
    }
});

function toggleLine(checkbox) {
    var datasetIndex = parseInt(checkbox.getAttribute('data-index'));
    if (temperatureChart && temperatureChart.data.datasets[datasetIndex]) {
        var meta = temperatureChart.getDatasetMeta(datasetIndex);
        meta.hidden = !checkbox.checked;
        temperatureChart.update();
    }
}

function downloadChartAsImage() {
    html2canvas(document.querySelector("#temperatureChart")).then(function (canvas) {
        var link = document.createElement('a');
        link.download = 'temperature_chart.png';
        link.href = canvas.toDataURL();
        link.click();
    });
}

window.monitoringChart = {
    _labelsFa: [],

    init: function (canvasId) {
        var canvas = document.getElementById(canvasId);
        if (!canvas) return;

        if (temperatureChart) {
            temperatureChart.destroy();
            temperatureChart = null;
            chartInitialized = false;
        }

        this._labelsFa = [];

        var ctx = canvas.getContext('2d');
        var isMobile = window.innerWidth < 768;

        temperatureChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: [],
                datasets: [
                    {
                        label: 'دمای یخچال',
                        data: [],
                        backgroundColor: 'rgba(54, 162, 235, 0.2)',
                        borderColor: 'rgba(54, 162, 235, 1)',
                        borderWidth: 3,
                        pointRadius: 1,
                        pointBackgroundColor: 'rgba(54, 162, 235, 1)',
                        pointBorderColor: '#fff',
                        pointBorderWidth: 1,
                        pointHoverRadius: 5,
                        tension: 0.3,
                        yAxisID: 'y-axis-1',
                        order: 5
                    },
                    {
                        label: 'دمای فریزر',
                        data: [],
                        backgroundColor: 'rgba(255, 99, 132, 0.2)',
                        borderColor: 'rgba(255, 99, 132, 1)',
                        borderWidth: 3,
                        pointRadius: 1,
                        pointBackgroundColor: 'rgba(255, 99, 132, 1)',
                        pointBorderColor: '#fff',
                        pointBorderWidth: 1,
                        pointHoverRadius: 5,
                        tension: 0.3,
                        yAxisID: 'y-axis-1',
                        order: 4
                    },
                    {
                        label: 'موتور',
                        data: [],
                        backgroundColor: 'rgba(255, 159, 64, 0.2)',
                        borderColor: 'rgba(255, 159, 64, 1)',
                        borderWidth: 2,
                        pointRadius: 0,
                        pointHoverRadius: 3,
                        steppedLine: true,
                        yAxisID: 'y-axis-2',
                        order: 3
                    },
                    {
                        label: 'هیتر ۱',
                        data: [],
                        backgroundColor: 'rgba(128, 0, 0, 0.2)',
                        borderColor: 'rgba(128, 0, 0, 1)',
                        borderWidth: 2,
                        pointRadius: 0,
                        pointHoverRadius: 3,
                        steppedLine: true,
                        yAxisID: 'y-axis-2',
                        order: 2
                    },
                    {
                        label: 'هیتر ۲',
                        data: [],
                        backgroundColor: 'rgba(75, 75, 192, 0.2)',
                        borderColor: 'rgba(75, 75, 192, 1)',
                        borderWidth: 2,
                        pointRadius: 0,
                        pointHoverRadius: 3,
                        steppedLine: true,
                        yAxisID: 'y-axis-2',
                        order: 1
                    },
                    {
                        label: 'برق دستگاه',
                        data: [],
                        backgroundColor: 'rgba(44, 44, 46, 0)',
                        borderColor: 'rgba(44, 44, 46, 1)',
                        borderWidth: 4,
                        pointRadius: 0,
                        pointHoverRadius: 3,
                        steppedLine: true,
                        yAxisID: 'y-axis-2',
                        order: 0
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                responsiveAnimationDuration: 0,
                animation: {
                    duration: isMobile ? 0 : 1000
                },
                layout: {
                    padding: {
                        top: 30,
                        right: 20,
                        bottom: 10,
                        left: 20
                    }
                },
                legend: {
                    display: false
                },
                scales: {
                    xAxes: [{
                        type: 'category',
                        gridLines: {
                            display: true,
                            color: 'rgba(0, 0, 0, 0.05)'
                        },
                        ticks: {
                            fontColor: '#666',
                            fontFamily: 'Vazirmatn',
                            maxRotation: isMobile ? 90 : 0,
                            autoSkip: true,
                            autoSkipPadding: isMobile ? 10 : 50,
                            maxTicksLimit: isMobile ? 10 : 20
                        }
                    }],
                    yAxes: [{
                        id: 'y-axis-1',
                        position: 'left',
                        gridLines: {
                            display: true,
                            color: 'rgba(0, 0, 0, 0.05)'
                        },
                        ticks: {
                            fontColor: '#666',
                            fontFamily: 'Vazirmatn',
                            beginAtZero: false,
                            callback: function (value) {
                                return value + '°C';
                            }
                        }
                    },
                    {
                        id: 'y-axis-2',
                        position: 'right',
                        gridLines: {
                            display: false
                        },
                        ticks: {
                            min: 0,
                            max: 1,
                            stepSize: 1,
                            display: false
                        }
                    }]
                },
                tooltips: {
                    enabled: true,
                    mode: 'index',
                    intersect: false,
                    backgroundColor: 'rgba(0, 0, 0, 0.8)',
                    titleFontFamily: 'Vazirmatn',
                    bodyFontFamily: 'Vazirmatn',
                    titleFontSize: 14,
                    bodyFontSize: 13,
                    xPadding: 20,
                    yPadding: 20,
                    displayColors: true,
                    bodySpacing: 8,
                    titleSpacing: 10,
                    callbacks: {
                        title: function (tooltipItems) {
                            var index = tooltipItems[0].index;
                            if (window.monitoringChart._labelsFa && window.monitoringChart._labelsFa[index]) {
                                return window.monitoringChart._labelsFa[index];
                            }
                            return tooltipItems[0].xLabel;
                        },
                        label: function (tooltipItem, data) {
                            var datasetLabel = data.datasets[tooltipItem.datasetIndex].label || '';
                            var value = tooltipItem.yLabel;
                            if (tooltipItem.datasetIndex >= 2) {
                                return datasetLabel + ': ' + (value === 1 ? 'روشن' : 'خاموش');
                            }
                            return datasetLabel + ': ' + value + ' °C';
                        }
                    }
                },
                hover: {
                    mode: 'index',
                    intersect: false
                },
                elements: {
                    line: {
                        tension: 0.2
                    },
                    point: {
                        radius: 0,
                        hitRadius: 10,
                        hoverRadius: 5
                    }
                }
            }
        });

        temperatureChart.getDatasetMeta(2).hidden = true;
        temperatureChart.getDatasetMeta(3).hidden = true;
        temperatureChart.getDatasetMeta(4).hidden = true;
        temperatureChart.getDatasetMeta(5).hidden = true;
        temperatureChart.update();

        setupTouchScroll();
        chartInitialized = true;
    },

    updateData: function (canvasId, data) {
        if (!temperatureChart) return;

        var chartWrapper = document.querySelector('.chart-wrapper');
        if (chartWrapper) {
            var dataCount = data.labels.length;
            var isMobile = window.innerWidth < 768;
            var minWidth = window.innerWidth;
            var widthPerPoint = isMobile ? 50 : 70;
            var totalWidth = Math.max(minWidth, dataCount * widthPerPoint);
            chartWrapper.style.width = totalWidth + 'px';
        }

        this._labelsFa = data.labelsFa || [];
        temperatureChart.data.labels = data.labels;
        temperatureChart.data.datasets[0].data = data.temperatureRef;
        temperatureChart.data.datasets[1].data = data.temperatureFreez;
        temperatureChart.data.datasets[2].data = data.motor;
        temperatureChart.data.datasets[3].data = data.heater1;
        temperatureChart.data.datasets[4].data = data.heater2;
        temperatureChart.data.datasets[5].data = data.power;
        temperatureChart.update();
        setTimeout(function() {
            if (temperatureChart) temperatureChart.resize();
        }, 10);
    },

    destroy: function () {
        if (temperatureChart) {
            temperatureChart.destroy();
            temperatureChart = null;
            chartInitialized = false;
        }
    }
};

function setupTouchScroll() {
    var scrollContainer = document.querySelector('.chart-scroll');
    if (!scrollContainer) return;

    var startX = 0;
    var startScrollLeft = 0;
    var isScrolling = false;
    var isMouseDown = false;
    var lastTouchTime = 0;
    var touchThrottle = 16;

    scrollContainer.addEventListener('touchstart', function (e) {
        if (e.touches.length === 1) {
            startX = e.touches[0].clientX;
            startScrollLeft = scrollContainer.scrollLeft;
            isScrolling = true;
        }
    }, { passive: true });

    scrollContainer.addEventListener('touchmove', function (e) {
        if (!isScrolling) return;
        var now = Date.now();
        if (now - lastTouchTime < touchThrottle) return;
        lastTouchTime = now;
        if (e.touches.length === 1) {
            var dx = startX - e.touches[0].clientX;
            scrollContainer.scrollLeft = startScrollLeft + dx;
        }
    }, { passive: true });

    scrollContainer.addEventListener('touchend', function () {
        isScrolling = false;
    });

    scrollContainer.addEventListener('mousedown', function (e) {
        startX = e.clientX;
        startScrollLeft = scrollContainer.scrollLeft;
        if (e.button === 1 || e.button === 2) {
            isScrolling = true;
            isMouseDown = true;
            scrollContainer.style.cursor = 'grabbing';
            e.preventDefault();
        } else if (e.button === 0) {
            isMouseDown = true;
        }
    });

    scrollContainer.addEventListener('mousemove', function (e) {
        if (!isMouseDown) return;
        var dx = startX - e.clientX;
        if (isScrolling || Math.abs(dx) > 10) {
            if (!isScrolling && Math.abs(dx) > 10) {
                isScrolling = true;
                scrollContainer.style.cursor = 'grabbing';
            }
            if (isScrolling) {
                scrollContainer.scrollLeft = startScrollLeft + dx;
                e.preventDefault();
            }
        }
    });

    document.addEventListener('mouseup', function () {
        isMouseDown = false;
        isScrolling = false;
        if (scrollContainer) scrollContainer.style.cursor = 'default';
    });

    scrollContainer.addEventListener('contextmenu', function (e) {
        if (isScrolling) e.preventDefault();
    });
}

document.addEventListener('DOMContentLoaded', function () {
    setupTouchScroll();
});
