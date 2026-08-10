<%@ Page Language="C#" AutoEventWireup="true" Inherits="ErrorService.WebForms.admin.ardino.chart" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>نمودار دما</title>
    <meta charset="utf-8">
    <meta http-equiv="X-UA-Compatible" content="IE=edge">
    
    <meta name="msapplication-TileColor" content="#da532c" />
    <meta name="theme-color" content="#ffffff" />
    <meta content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no" name="viewport">
    
    <!-- فونت ایران سنس -->
    <link href="css/vazirfont.css?family=Vazirmatn:wght@400;700&display=swap" rel="stylesheet">    
    <!-- Bootstrap 3.3.7 -->
    <link rel="stylesheet" href="dist/css/bootstrap-theme.css">
    <!-- Bootstrap rtl -->
    <link rel="stylesheet" href="dist/css/rtl.css">
    <!-- Font Awesome -->
    
    <style>
        body, h1, h2, h3, h4, h5, h6, p, span, div, label, input, button, select, textarea {
            font-family: 'Vazirmatn', Tahoma, Arial, sans-serif !important;
        }
        
        .chart-container {
            width: 100%;
            height: 900px;
            margin: 10px auto;
            background-color: #fff;
            padding: 15px 10px;
            border-radius: 5px;
            box-shadow: 0 0 10px rgba(0,0,0,0.1);
        }
        .chart-title {
            text-align: center;
            font-size: 24px;
            margin-bottom: 20px;
            color: #333;
        }
        body {
            direction: rtl;
            background-color: #f4f4f4;
            padding: 0px;
        }
        .chart-scroll-container {
            width: 100%;
            overflow-x: auto;
            overflow-y: hidden;
            -webkit-overflow-scrolling: touch;
            direction: rtl;
        }
        .chart-wrapper {
            height: 600px;
            min-width: 100%;
        }
        .chart-help-text {
            text-align: center;
            font-size: 11px;
            color: #666;
            margin-top: 3px;
        }
        .chart-controls {
            background-color: #f8f8f8;
            border-radius: 5px;
            padding: 10px;
            box-shadow: 0 1px 3px rgba(0,0,0,0.1);
            margin-bottom: 30px; /* افزایش فاصله از 15px به 30px */
        }
        .checkbox-container label {
            font-size: 14px;
            font-weight: bold;
            padding: 5px 10px;
            border-radius: 4px;
            transition: background-color 0.2s;
        }
        .checkbox-container label:hover {
            background-color: #eaeaea;
        }
        .checkbox-container input {
            margin-left: 5px;
            transform: scale(1.2);
        }
        
        /* استایل‌های محورهای Y ثابت */
        .fixed-y-axis {
            position: fixed;
            top: 0;
            height: 600px;
            width: 60px;
            background-color: rgba(255, 255, 255, 0.98);
            z-index: 100;
            pointer-events: none;
            display: none;
            box-shadow: 0 0 10px rgba(0,0,0,0.1);
            font-family: 'Vazirmatn', Tahoma, Arial, sans-serif !important;
        }
        .fixed-y-axis-left {
            left: 0;
            border-right: 1px solid rgba(0,0,0,0.1);
        }
        .fixed-y-axis-right {
            right: 0;
            border-left: 1px solid rgba(0,0,0,0.1);
        }
        .fixed-y-axis-tick {
            position: absolute;
            width: 100%;
            text-align: right;
            padding-right: 10px;
        }
        .fixed-y-axis-tick-line {
            position: absolute;
            right: 0;
            width: 30px;
            height: 1px;
            background-color: rgba(0,0,0,0.1);
        }
        .fixed-y-axis-tick-text {
            font-size: 12px;
            color: #333;
            font-weight: bold;
            font-family: 'Vazirmatn', Tahoma, Arial, sans-serif !important;
        }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <asp:ScriptManager ID="ScriptManager1" runat="server"></asp:ScriptManager>
        
        <div class="container">
            <div class="row">
                <div class="col-md-12">
                    <h1 class="chart-title">نمودار عملکرد در یک نگاه</h1>

                    <asp:Literal ID="litInfo" runat="server"></asp:Literal>

                    <asp:UpdatePanel ID="UpdatePanel1" runat="server" UpdateMode="Conditional">
                        <ContentTemplate>
                            <div class="chart-container">
            <div class="chart-controls" style="margin-bottom: 30px; text-align: center;text-align:right">
    <div style="display: flex; flex-wrap: wrap; justify-content: center; max-width: 100%;">
        <div style="width: 33.33%; min-width: 100px; padding: 5px; box-sizing: border-box;">
            <div class="checkbox-container" style="display: block; margin: 0; color:rgba(54,162,235,1)">
                <label style="cursor: pointer; user-select: none; display: block; padding: 8px 0;">
                    <input type="checkbox" class="line-toggle" data-index="0" checked /> دمای یخچال
                </label>
            </div>
        </div>
        <div style="width: 33.33%; min-width: 100px; padding: 5px; box-sizing: border-box;">
            <div class="checkbox-container" style="display: block; margin: 0; color:rgba(255, 99, 132, 1)">
                <label style="cursor: pointer; user-select: none; display: block; padding: 8px 0;">
                    <input type="checkbox" class="line-toggle" data-index="1" checked /> دمای فریزر
                </label>
            </div>
        </div>
        <div style="width: 33.33%; min-width: 100px; padding: 5px; box-sizing: border-box;">
            <div class="checkbox-container" style="display: block; margin: 0; color:rgba(255, 159, 64, 1)">
                <label style="cursor: pointer; user-select: none; display: block; padding: 8px 0;">
                    <input type="checkbox" class="line-toggle" data-index="2" /> موتور
                </label>
            </div>
        </div>
        <div style="width: 33.33%; min-width: 100px; padding: 5px; box-sizing: border-box;">
            <div class="checkbox-container" style="display: block; margin: 0; color:rgba(128, 0, 0, 1)">
                <label style="cursor: pointer; user-select: none; display: block; padding: 8px 0;">
                    <input type="checkbox" class="line-toggle" data-index="3" /> هیتر ۱
                </label>
            </div>
        </div>
        <div style="width: 33.33%; min-width: 100px; padding: 5px; box-sizing: border-box;">
            <div class="checkbox-container" style="display: block; margin: 0; color:rgba(75, 75, 192, 1)">
                <label style="cursor: pointer; user-select: none; display: block; padding: 8px 0;">
                    <input type="checkbox" class="line-toggle" data-index="4" /> هیتر ۲
                </label>
            </div>
        </div>
        <div style="width: 33.33%; min-width: 100px; padding: 5px; box-sizing: border-box;">
            <div class="checkbox-container" style="display: block; margin: 0; color:rgba(44, 44, 46, 1)">
                <label style="cursor: pointer; user-select: none; display: block; padding: 8px 0;">
                    <input type="checkbox" class="line-toggle" data-index="5" /> برق دستگاه
                </label>
            </div>
        </div>
    </div>
</div>
                                <button onclick="downloadChartAsImage()" style="width:100%">دانلود به صورت تصویر</button>




                                <div style="height: 15px;"></div> <!-- افزودن یک فضای خالی اضافی -->
                                <div class="chart-scroll-container">
                                    <div class="chart-wrapper">
                                        <canvas id="temperatureChart"></canvas>
                                    </div>
                                </div>
                                <div class="chart-help-text">برای اسکرول نمودار: در موبایل نگه دارید، در دسکتاپ با کلیک راست بکشید</div>
                            </div>
                            
                            <div class="text-center">
                                <asp:Label ID="lblLastUpdate" runat="server" CssClass="text-muted"></asp:Label>
                            </div>
                            
                            <asp:HiddenField ID="hdnTemperatureData" runat="server" />
                            <asp:HiddenField ID="hdnTimeLabels" runat="server" />
                            <asp:HiddenField ID="hdnStatusData" runat="server" />
                            <asp:HiddenField ID="hdnFreezerData" runat="server" />
                            <asp:HiddenField ID="hdnHeater1Data" runat="server" />
                            <asp:HiddenField ID="hdnHeater2Data" runat="server" />
                            <asp:HiddenField ID="hdnPowerData" runat="server" />
                        </ContentTemplate>
                    </asp:UpdatePanel>
                </div>
            </div>
        </div>
        
        <!-- jQuery 3 -->
        <script src="css/jquery.min.js"></script>
      

        <script src="css/Chart.min.js"></script>
        <script src="css/html2canvas.min.js"></script>
        <script type="text/javascript">
            // متغیر عمومی برای نمودار
            var temperatureChart = null;
            var chartInitialized = false;



            function calculateOperationTime(dataset, timeLabels) {
                var operationTimes = [];
                var startTime = null;
                var startIndex = 0;
                var lastValue = 0;

                for (var i = 0; i < dataset.length; i++) {
                    var currentValue = dataset[i];

                    // اگر از 0 به 1 تغییر کرده (روشن شده)
                    if (lastValue === 0 && currentValue === 1) {
                        startTime = timeLabels[i];
                        startIndex = i;
                    }

                    // اگر از 1 به 0 تغییر کرده (خاموش شده) و زمان شروع داریم
                    if (lastValue === 1 && currentValue === 0 && startTime !== null) {
                        var endTime = timeLabels[i];

                        // محاسبه اختلاف زمانی به صورت دستی
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

            // تابع محاسبه اختلاف زمانی بین دو زمان با فرمت HH:MM:SS
            function calculateTimeDifference(startTimeStr, endTimeStr) {
                try {
                    // Extract time part from full datetime format (e.g. "1403/01/15 14:30:25")
                    var startTimeOnly = startTimeStr.includes(' ') ? startTimeStr.split(' ')[1] : startTimeStr;
                    var endTimeOnly = endTimeStr.includes(' ') ? endTimeStr.split(' ')[1] : endTimeStr;
                    // تبدیل رشته‌های زمان به آرایه [ساعت, دقیقه, ثانیه]
                    var startParts = startTimeOnly.split(':').map(Number);
                    var endParts = endTimeOnly.split(':').map(Number);

                    // محاسبه کل ثانیه‌ها
                    var startTotalSeconds = startParts[0] * 3600 + startParts[1] * 60 + startParts[2];
                    var endTotalSeconds = endParts[0] * 3600 + endParts[1] * 60 + endParts[2];

                    // اگر زمان پایان کمتر از زمان شروع است، یعنی به روز بعد رسیده‌ایم
                    if (endTotalSeconds < startTotalSeconds) {
                        endTotalSeconds += 24 * 3600; // اضافه کردن یک روز
                    }

                    var diffSeconds = endTotalSeconds - startTotalSeconds;

                    // تبدیل به ساعت:دقیقه:ثانیه
                    var hours = Math.floor(diffSeconds / 3600);
                    var minutes = Math.floor((diffSeconds % 3600) / 60);
                    var seconds = diffSeconds % 60;

                    // فرمت‌بندی به صورت 00:00:00
                    return (hours < 10 ? '0' + hours : hours) + ':' +
                        (minutes < 10 ? '0' + minutes : minutes) + ':' +
                        (seconds < 10 ? '0' + seconds : seconds);
                } catch (e) {
                    console.error("خطا در محاسبه اختلاف زمانی:", e, startTimeStr, endTimeStr);
                    return "00:00:00";
                }
            }

            // پلاگین نمایش زمان کارکرد
            Chart.plugins.register({
                afterDatasetsDraw: function (chart) {
                    if (!chart.data || !chart.data.datasets) return;

                    var ctx = chart.ctx;
                    var timeLabels = chart.data.labels;

                    // بررسی داده‌های موتور، هیتر ۱ و هیتر ۲
                    var datasets = [
                        { index: 2, name: 'موتور', color: 'rgba(255, 159, 64, 1)' },
                        { index: 3, name: 'هیتر ۱', color: 'rgba(128, 0, 0, 1)' },
                        { index: 4, name: 'هیتر ۲', color: 'rgba(75, 75, 192, 1)' },
                        { index: 5, name: 'برق دستگاه', color: 'rgba(19, 19, 20, 1)' }
                    ];

                    datasets.forEach(function (dataset) {
                        // بررسی اینکه آیا دیتاست فعال است
                        
                        var meta = chart.getDatasetMeta(dataset.index);
                        if (meta.hidden) return;

                        var data = chart.data.datasets[dataset.index].data;
                        var operationTimes = calculateOperationTime(data, timeLabels);

                        // نمایش زمان کارکرد داخل هر پله
                        operationTimes.forEach(function (opTime) {
                            // محاسبه موقعیت وسط پله
                            var startIndex = opTime.startIndex;
                            var endIndex = opTime.endIndex;

                            // اطمینان از وجود داده در این ایندکس‌ها
                            if (!meta.data || startIndex >= meta.data.length || !meta.data[startIndex] ||
                                endIndex >= meta.data.length || !meta.data[endIndex]) return;

                            try {
                                // محاسبه موقعیت وسط پله
                                var startX = meta.data[startIndex]._model ? meta.data[startIndex]._model.x : 0;
                                var endX = meta.data[endIndex]._model ? meta.data[endIndex]._model.x : 0;
                                var middleX = (startX + endX) / 2;

                                // موقعیت Y (وسط پله)
                                var yPos = meta.data[startIndex]._model ? meta.data[startIndex]._model.y : 0;
                                var yBottom = chart.scales['y-axis-1'].bottom;

                                // محاسبه ارتفاع پله
                                var stepHeight = yBottom - yPos;

                                // اگر ارتفاع پله خیلی کم است، نمایش ندهیم
                                if (stepHeight < 30) return;

                                // متن زمان کارکرد
                                var text = opTime.time;

                                ctx.save();

                                // تنظیم فونت برای اندازه‌گیری عرض متن
                                ctx.font = 'bold 12px Vazirmatn';
                                var textWidth = ctx.measureText(text).width;
                                var textHeight = 15; // تقریبی

                                // چرخش متن به صورت عمودی


                                if (dataset.index == 2) ctx.translate(middleX, yPos + stepHeight / 1.5);
                                if (dataset.index == 3) ctx.translate(middleX, yPos + stepHeight / 2);
                                if (dataset.index == 4) ctx.translate(middleX, yPos + stepHeight / 3.3);
                                // رسم پس‌زمینه
                                ctx.fillStyle = 'rgba(255, 255, 255, 0.8)';
                                ctx.fillRect(-textHeight / 2 - 5, -textWidth / 2 - 5, textHeight + 10, textWidth + 10);

                                // چرخش متن
                                ctx.rotate(-Math.PI / 2);

                                // رسم متن
                                ctx.fillStyle = dataset.color;
                                ctx.textAlign = 'center';
                                ctx.textBaseline = 'middle';

                               

                                ctx.fillText(text, 0, 0);

                                ctx.restore();
                            } catch (e) {
                                console.error("خطا در رسم متن زمان کارکرد:", e);
                            }
                        });
                    });
                }
            });







            // تعریف پلاگین خط عمودی
            Chart.plugins.register({
                afterDraw: function (chart) {
                    // در موبایل این پلاگین را محدود کن
                    if (window.innerWidth < 768 && !chart.tooltip._active) return;

                    if (chart.tooltip._active && chart.tooltip._active.length) {
                        var activePoint = chart.tooltip._active[0];
                        var ctx = chart.ctx;
                        var x = activePoint.tooltipPosition().x;
                        var topY = chart.scales['y-axis-1'].top;
                        var bottomY = chart.scales['y-axis-1'].bottom;

                        // خط عمودی
                        ctx.save();
                        ctx.beginPath();
                        ctx.moveTo(x, topY);
                        ctx.lineTo(x, bottomY);
                        ctx.lineWidth = 2;
                        ctx.strokeStyle = 'rgba(0, 0, 0, 1)';
                        ctx.stroke();
                        ctx.restore();
                    }
                }
            });

            // حذف نمودار قبلی
            function destroyChart() {
                if (temperatureChart) {
                    temperatureChart.destroy();
                    temperatureChart = null;
                    chartInitialized = false;
                }
            }

            // تنظیم رویدادهای فعال/غیرفعال کردن خطوط نمودار
            function setupLineToggleEvents() {
                var checkboxes = document.querySelectorAll('.line-toggle');

                checkboxes.forEach(function (checkbox) {
                    checkbox.addEventListener('change', function () {
                        var datasetIndex = parseInt(this.getAttribute('data-index'));

                        if (temperatureChart && temperatureChart.data.datasets[datasetIndex]) {
                            // تغییر وضعیت نمایش خط
                            var meta = temperatureChart.getDatasetMeta(datasetIndex);
                            meta.hidden = !this.checked;

                            // به‌روزرسانی نمودار
                            temperatureChart.update();
                        }
                    });
                });
            }



            // تنظیم رویدادهای لمسی و موس برای اسکرول نمودار
            function setupTouchEvents() {
                console.log("Setting up touch events...");

                var scrollContainer = document.querySelector('.chart-scroll-container');
                var canvas = document.getElementById('temperatureChart');

                if (!scrollContainer || !canvas) {
                    console.error("Scroll container or canvas not found!");
                    return;
                }

                // متغیرهای مورد نیاز برای اسکرول
                var startX = 0;
                var startScrollLeft = 0;
                var isScrolling = false;
                var isMouseDown = false;
                var lastTouchTime = 0;
                var touchThrottle = 16; // محدود کردن به حدود 60fps

                // اضافه کردن پشتیبانی از تاچ برای موبایل با محدودیت فراخوانی
                scrollContainer.addEventListener('touchstart', function (e) {
                    if (e.touches.length === 1) {
                        startX = e.touches[0].clientX;
                        startScrollLeft = scrollContainer.scrollLeft;
                        isScrolling = true;
                    }
                }, { passive: true });

                scrollContainer.addEventListener('touchmove', function (e) {
                    if (!isScrolling) return;

                    // محدود کردن فراخوانی‌های مکرر
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

                // اضافه کردن پشتیبانی از موس برای دسکتاپ
                scrollContainer.addEventListener('mousedown', function (e) {
                    startX = e.clientX;
                    startScrollLeft = scrollContainer.scrollLeft;

                    if (e.button === 1 || e.button === 2) { // کلیک وسط یا راست
                        isScrolling = true;
                        isMouseDown = true;
                        scrollContainer.style.cursor = 'grabbing';
                        e.preventDefault();
                    } else if (e.button === 0) { // کلیک چپ
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
                    scrollContainer.style.cursor = 'default';
                });

                scrollContainer.addEventListener('contextmenu', function (e) {
                    if (isScrolling) {
                        e.preventDefault();
                    }
                });
            }

            // مقداردهی اولیه نمودار
            function initChart() {
                console.log("Initializing chart...");

                var canvas = document.getElementById('temperatureChart');
                if (!canvas) {
                    console.error("Canvas element not found!");
                    return;
                }

                destroyChart();

                var ctx = canvas.getContext('2d');

                // تنظیم عرض wrapper نمودار بر اساس تعداد داده‌ها
                var chartWrapper = document.querySelector('.chart-wrapper');
                if (chartWrapper) {
                    // دریافت تعداد داده‌ها
                    var timeLabelsField = document.getElementById('<%= hdnTimeLabels.ClientID %>');
                    var timeLabels = [];

                    try {
                        timeLabels = JSON.parse(timeLabelsField.value || '[]');
                    } catch (e) {
                        console.error("Error parsing time labels: " + e.message);
                    }

                    // محاسبه عرض مناسب بر اساس تعداد داده‌ها
                    var dataCount = timeLabels.length;
                    var isMobile = window.innerWidth < 768;
                    var minWidth = window.innerWidth; // حداقل عرض برابر با عرض پنجره
                    var widthPerPoint = isMobile ? 20 : 50; // عرض تخمینی برای هر نقطه داده (کمتر در موبایل)

                    // محاسبه عرض کلی نمودار (حداقل برابر با عرض پنجره)
                    var totalWidth = Math.max(minWidth, dataCount * widthPerPoint);
                    chartWrapper.style.width = totalWidth + 'px';
                    console.log("Chart width set to: " + totalWidth + "px for " + dataCount + " data points");
                }

                // تنظیمات نمودار
                var options = {
                    responsive: true,
                    maintainAspectRatio: false,
                    responsiveAnimationDuration: 0, // حذف انیمیشن برای عملکرد بهتر
                    animation: {
                        duration: isMobile ? 0 : 1000 // حذف انیمیشن در موبایل
                    },
                    layout: {
                        padding: {
                            top: 30, // فضای بیشتر برای نمایش زمان کارکرد
                            right: 20,
                            bottom: 10,
                            left: 20
                        }
                    },
                    legend: {
                        display: false // عدم نمایش راهنما (از چک‌باکس‌ها استفاده می‌کنیم)
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
                                maxRotation: isMobile ? 90 : 0, // چرخش برچسب‌ها در موبایل
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
                        bodySpacing: 8,  // افزایش فاصله بین خطوط در بدنه تولتیپ
                        titleSpacing: 10,  // افزایش فاصله بین عنوان و بدنه تولتیپ
                        callbacks: {
                            title: function (tooltipItems, data) {
                                return tooltipItems[0].xLabel;
                            },
                            label: function (tooltipItem, data) {
                                var datasetLabel = data.datasets[tooltipItem.datasetIndex].label || '';
                                var value = tooltipItem.yLabel;

                                // برای موتور و هیترها، نمایش وضعیت روشن/خاموش
                                if (tooltipItem.datasetIndex >= 2) {
                                    return datasetLabel + ': ' + (value === 1 ? 'روشن' : 'خاموش');
                                }

                                // برای دما، نمایش مقدار با درجه سانتیگراد
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
                            tension: 0.2 // کمی انحنا برای خطوط
                        },
                        point: {
                            radius: 0, // عدم نمایش نقاط برای عملکرد بهتر
                            hitRadius: 10,
                            hoverRadius: 5
                        }
                    }
                };

                // ایجاد نمودار
                temperatureChart = new Chart(ctx, {
                    type: 'line',
                    data: {
                        labels: [],
                        datasets: [{
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
                                order: 5


                            
                        }]
                    },
                    options: options
                });

                // بارگذاری داده‌ها
                loadChartData();

                // تنظیم رویدادها
                setupLineToggleEvents();
                setupTouchEvents();

                // مخفی کردن خطوط موتور، هیتر ۱ و هیتر ۲ در ابتدا
                temperatureChart.getDatasetMeta(2).hidden = true; // موتور
                temperatureChart.getDatasetMeta(3).hidden = true; // هیتر ۱
                temperatureChart.getDatasetMeta(4).hidden = true; // هیتر ۲
                temperatureChart.getDatasetMeta(5).hidden = true; // برق دستگاه ۲
                temperatureChart.update();

                chartInitialized = true;
            }

            // بارگذاری داده‌ها از فیلدهای مخفی
            function loadChartData() {
                try {
                    // دریافت داده‌ها از فیلدهای مخفی
                    var temperatureField = document.getElementById('<%= hdnTemperatureData.ClientID %>');
                    var freezerField = document.getElementById('<%= hdnFreezerData.ClientID %>');
                    var timeLabelsField = document.getElementById('<%= hdnTimeLabels.ClientID %>');
                    var statusField = document.getElementById('<%= hdnStatusData.ClientID %>');
                    var heater1Field = document.getElementById('<%= hdnHeater1Data.ClientID %>');
                    var heater2Field = document.getElementById('<%= hdnHeater2Data.ClientID %>');
                    var powerField = document.getElementById('<%= hdnPowerData.ClientID %>');

                    // تبدیل داده‌ها به آرایه
                    var temperatureData = JSON.parse(temperatureField.value || '[]');
                    var freezerData = JSON.parse(freezerField.value || '[]');
                    var timeLabels = JSON.parse(timeLabelsField.value || '[]');
                    var statusData = JSON.parse(statusField.value || '[]');
                    var heater1Data = JSON.parse(heater1Field.value || '[]');
                    var heater2Data = JSON.parse(heater2Field.value || '[]');
                    var powerData = JSON.parse(powerField.value || '[]');

                    // به‌روزرسانی داده‌های نمودار
                    temperatureChart.data.labels = timeLabels;
                    temperatureChart.data.datasets[0].data = temperatureData;
                    temperatureChart.data.datasets[1].data = freezerData;
                    temperatureChart.data.datasets[2].data = statusData;
                    temperatureChart.data.datasets[3].data = heater1Data;
                    temperatureChart.data.datasets[4].data = heater2Data;
                    temperatureChart.data.datasets[5].data = powerData;

                    // به‌روزرسانی نمودار
                    temperatureChart.update();

                    console.log("Chart data loaded successfully");
                } catch (e) {
                    console.error("Error loading chart data:", e);
                }
            }

            // اجرایی تابع مقداردهی اولیه پس از بارگذاری صفحه
            document.addEventListener('DOMContentLoaded', function () {
                console.log("DOM loaded, initializing chart...");
                initChart();

                // تنظیم به‌روزرسانی خودکار نمودار پس از هر به‌روزرسانی پنل
                var prm = Sys.WebForms.PageRequestManager.getInstance();

                prm.add_endRequest(function () {
                    console.log("UpdatePanel refresh detected, reinitializing chart...");

                    // اگر نمودار قبلاً مقداردهی شده، فقط داده‌ها را به‌روزرسانی کن
                    if (chartInitialized && temperatureChart) {
                        loadChartData();
                    } else {
                        // در غیر این صورت، نمودار را از ابتدا مقداردهی کن
                        initChart();
                    }
                });
            });

            function downloadChartAsImage() {
                html2canvas(document.querySelector("#temperatureChart")).then(canvas => {
                    var link = document.createElement('a');
                    link.download = 'temperature_chart.png';
                    link.href = canvas.toDataURL();
                    link.click();
                    refreshPageWithId();
                });
            }

            function refreshPageWithId() {
                // دریافت پارامتر Id از URL فعلی
                var urlParams = new URLSearchParams(window.location.search);
                var id = urlParams.get('Id');

                if (id) {
                    // اگر Id وجود داشته باشد، صفحه را با همان Id رفرش می‌کنیم
                    window.location.href = window.location.pathname + '?Id=' + id;
                } else {
                    // اگر Id وجود نداشته باشد، فقط صفحه را رفرش می‌کنیم
                    window.location.reload();
                }
            }
        </script>


        
    </form>
</body>
</html>



