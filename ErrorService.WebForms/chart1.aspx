<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="chart1.aspx.cs" Inherits="ErrorService.WebForms.admin.ardino.chart1" Culture="fa-IR" UICulture="fa-IR" ResponseEncoding="utf-8" %>
<% Response.ContentEncoding = System.Text.Encoding.UTF8; Response.Charset = "utf-8"; Response.ContentType = "text/html; charset=utf-8"; %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml" lang="fa" dir="rtl">
<head runat="server">
    <title>نمودار دما و وضعیت موتور</title>
    <meta charset="utf-8" />
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <meta http-equiv="Content-Language" content="fa-IR" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no" />
    <!-- بوت استرپ -->
<%--    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css" rel="stylesheet" />--%>
    <link href="chartstyle/bootstrap.min.css" rel="stylesheet" />

<%--    <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/js/bootstrap.bundle.min.js"></script>--%>
    <script src="chartstyle/bootstrap.bundle.min.js" rel="stylesheet"></script>

   
<%--    <link href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.10.0/font/bootstrap-icons.css" rel="stylesheet" />--%>
    <link href="chartstyle/bootstrap-icons.css" rel="stylesheet" />

    <!-- کتابخانه‌های نمودار -->
<%--    <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>--%>
    <script src="chartstyle/chart.js" rel="stylesheet"></script>

<%--    <script src="https://cdn.jsdelivr.net/npm/chartjs-plugin-annotation"></script>--%>
    <script src="chartstyle/chartjs-plugin-annotation.js" rel="stylesheet"></script>

<%--    <script src="https://cdn.jsdelivr.net/npm/hammerjs"></script>--%>
        <script src="chartstyle/hammerjs.js" rel="stylesheet"></script>

<%--    <script src="https://cdn.jsdelivr.net/npm/chartjs-plugin-zoom"></script>--%>
    <script src="chartstyle/chartjs-plugin-zoom.js" rel="stylesheet"></script>
    <!-- JalaliDatePicker (local) -->
    <link href="./jalalidatepicker/jalalidatepicker.min.css" rel="stylesheet" />
    <script>
        (function () {
            // تلاش برای بارگذاری کتابخانه jalaliDatepicker از چندین منبع
            function loadJalaliDatepicker() {
                console.log('Attempting to load jalaliDatepicker library...');
                
                // لیست منابع مختلف برای بارگذاری
                const sources = [
                    {
                        css: './jalalidatepicker/jalalidatepicker.min.css',
                        js: './jalalidatepicker/jalalidatepicker.min.js',
                        name: 'Local'
                    },
                    {
                        css: 'https://cdn.jsdelivr.net/gh/majidh1/JalaliDatePicker@master/jalalidatepicker.min.css',
                        js: 'https://cdn.jsdelivr.net/gh/majidh1/JalaliDatePicker@master/jalalidatepicker.min.js',
                        name: 'CDN1'
                    },
                    {
                        css: 'https://unpkg.com/jalalidatepicker@1.2.0/dist/jalalidatepicker.min.css',
                        js: 'https://unpkg.com/jalalidatepicker@1.2.0/dist/jalalidatepicker.min.js',
                        name: 'CDN2'
                    }
                ];
                
                let currentSourceIndex = 0;
                
                function tryLoadFromSource(sourceIndex) {
                    if (sourceIndex >= sources.length) {
                        console.error('All jalaliDatepicker sources failed to load');
                        // فعال‌سازی fallback mode
                        window.jalaliDatepickerFallback = true;
                        return;
                    }
                    
                    const source = sources[sourceIndex];
                    console.log(`Trying to load jalaliDatepicker from ${source.name}...`);
                    
                    // بارگذاری CSS
                    const link = document.createElement('link');
                    link.rel = 'stylesheet';
                    link.href = source.css;
                    document.head.appendChild(link);
                    
                    // بارگذاری JS
                    const script = document.createElement('script');
                    script.src = source.js;
                    
                    script.onload = function () {
                        console.log(`jalaliDatepicker loaded successfully from ${source.name}`);
                        if (window.jalaliDatepicker && typeof window.jalaliDatepicker.startWatch === 'function') {
                            window.jalaliDatepicker.startWatch({ time: true });
                            console.log('jalaliDatepicker initialized successfully');
                        }
                    };
                    
                    script.onerror = function () {
                        console.warn(`Failed to load jalaliDatepicker from ${source.name}, trying next source...`);
                        tryLoadFromSource(sourceIndex + 1);
                    };
                    
                    document.head.appendChild(script);
                }
                
                // شروع تلاش از اولین منبع
                tryLoadFromSource(0);
            }
            
            function loadCdnFallback() {
                loadJalaliDatepicker();
            }
            
            // بررسی وجود کتابخانه و بارگذاری در صورت عدم وجود
            if (!window.jalaliDatepicker) {
                if (document.readyState === 'loading') {
                    document.addEventListener('DOMContentLoaded', function () { 
                        if (!window.jalaliDatepicker) loadCdnFallback(); 
                    });
                } else {
                    loadCdnFallback();
                }
            }
        })();
        
        // توابع مربوط به نوت‌ها
        (function() {
            // ادغام همه نقاط نوت (از دیتاست و نوت‌های اضافه) و تشخیص نزدیک‌ترین نوت بر اساس کلیک روی محور x
            function getAllNotePoints() {
                        const result = [];
                        try {
                            const ts = (window.__chartData && Array.isArray(window.__chartData.Timestamps)) ? window.__chartData.Timestamps : [];
                            const ids = (window.__chartData && Array.isArray(window.__chartData.Ids)) ? window.__chartData.Ids : [];
                            const notes = (window.__chartData && Array.isArray(window.__chartData.Notes)) ? window.__chartData.Notes : [];
                            for (let i = 0; i < ts.length; i++) {
                                const n = notes[i]; if (n && String(n).trim() !== '') result.push({ ts: ts[i], id: ids[i], note: n, index: i });
                            }
                            const extra = window.__extraNotes || {};
                            const ets = Array.isArray(extra.Timestamps) ? extra.Timestamps : [];
                            const eids = Array.isArray(extra.Ids) ? extra.Ids : [];
                            const enotes = Array.isArray(extra.Notes) ? extra.Notes : [];
                            for (let j = 0; j < ets.length; j++) {
                                const n = enotes[j]; if (!n || String(n).trim() === '') continue;
                                // اگر در لیست موجود است تکراری اضافه نکن
                                if (!result.find(r => r.ts === ets[j])) {
                                    result.push({ ts: ets[j], id: eids[j], note: n, index: (ts ? ts.indexOf(ets[j]) : -1) });
                                }
                            }
                        } catch (e) { console.warn('getAllNotePoints error', e); }
                        return result;
                    }

                    function findNearestNoteByClick(evt, chart) {
                        try {
                            const xScale = chart.scales && chart.scales['x'];
                            if (!xScale) return null;
                            const canvasPosition = Chart.helpers.getRelativePosition(evt, chart);
                            const dataX = xScale.getValueForPixel(canvasPosition.x);
                            if (dataX == null) return null;
                            const allNotes = getAllNotePoints();
                            if (allNotes.length === 0) return null;
                            let nearest = null, minDist = Infinity;
                            for (const np of allNotes) {
                                const dist = Math.abs(new Date(np.ts).getTime() - new Date(dataX).getTime());
                                if (dist < minDist) { minDist = dist; nearest = np; }
                            }
                            return nearest;
                        } catch (e) { console.warn('findNearestNoteByClick error', e); return null; }
                    }

                    // تابع نمایش مودال نوت
                    function showNoteModal(noteData) {
                        try {
                            const modal = document.getElementById('noteModal');
                            const title = document.getElementById('noteModalTitle');
                            const body = document.getElementById('noteModalBody');
                            const deleteBtn = document.getElementById('deleteNoteBtn');
                            if (!modal || !title || !body || !deleteBtn) return;
                            title.textContent = `نوت - ${noteData.ts}`;
                            body.innerHTML = `<p><strong>زمان:</strong> ${noteData.ts}</p><p><strong>شناسه:</strong> ${noteData.id}</p><p><strong>متن:</strong> ${noteData.note}</p>`;
                            deleteBtn.onclick = function () { deleteNote(noteData.id, noteData.ts); };
                            const bsModal = new bootstrap.Modal(modal);
                            bsModal.show();
                        } catch (e) { console.warn('showNoteModal error', e); }
                    }

                    // تابع حذف نوت
                    function deleteNote(recordId, timestamp) {
                        if (!confirm('آیا مطمئن هستید که می‌خواهید این نوت را حذف کنید؟')) return;
                        const params = { recordId: recordId, timestamp: timestamp };
                        PageMethods.DeleteNote(params.recordId, params.timestamp, function (result) {
                            if (result && result.success) {
                                showSuccess('نوت با موفقیت حذف شد');
                                // بستن مودال
                                const modal = bootstrap.Modal.getInstance(document.getElementById('noteModal'));
                                if (modal) modal.hide();
                                // رفرش چارت
                                setTimeout(() => { location.reload(); }, 1000);
                            } else {
                                showError(result ? result.message : 'خطا در حذف نوت');
                            }
                        }, function (error) {
                            console.error('Delete note error:', error);
                            showError('خطا در ارتباط با سرور');
                        });
                    }

                    // اضافه کردن event listener برای کلیک روی چارت
                    window.addChartClickListener = function (chart) {
                        try {
                            if (!chart || !chart.canvas) return;
                            chart.canvas.addEventListener('click', function (evt) {
                                const nearest = findNearestNoteByClick(evt, chart);
                                if (nearest) showNoteModal(nearest);
                            });
                        } catch (e) { console.warn('addChartClickListener error', e); }
                    };

                    // تابع حذف نوت از طریق AJAX
            function deleteNoteAjax(recordId, timestamp) {
                return fetch('chart1.aspx/DeleteNote', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json; charset=utf-8' },
                    body: JSON.stringify({ recordId: recordId, timestamp: timestamp })
                }).then(response => response.json()).then(data => {
                    if (data.d && data.d.success) return data.d;
                    throw new Error(data.d ? data.d.message : 'خطا در حذف نوت');
                }).catch(e => {
                    showError('خطا در حذف نوت'); 
                    console.error(e);
                });
            }
        })();
    </script>
    <style>
        @font-face {
            font-family: 'Vazir';
            src: url('fonts/Vazir-Regular.woff2') format('woff2');
            font-weight: normal;
            font-style: normal;
            font-display: swap;
        }
        @font-face {
            font-family: 'Vazir';
            src: url('fonts/Vazir-Medium.woff2') format('woff2');
            font-weight: 500;
            font-style: normal;
            font-display: swap;
        }
        @font-face {
            font-family: 'Vazir';
            src: url('fonts/Vazir-Bold.woff2') format('woff2');
            font-weight: bold;
            font-style: normal;
            font-display: swap;
        }
        
        body {
            font-family: 'Vazir', Tahoma, Arial, sans-serif;
            direction: rtl;
            background-color: #f8f9fa;
            margin: 0;
            padding: 0;
        }
        
        /* استایل‌های سفارشی برای دیتاپیکر جلالی (طبق CSS رسمی کتابخانه) */
        /* عنصر ریشه پاپ‌آپ: jdp-container و روزها: .jdp-day و روز غیرفعال: .disabled-day */
        jdp-container .jdp-day.disabled-day {
            /* تاکید روی غیرقابل کلیک بودن و ظاهر غیرفعال */
            opacity: 0.45 !important;
            color: #9aa0a6 !important;
            background-color: #eeeeee !important;
            cursor: not-allowed !important;
            pointer-events: none !important;
            transform: none !important;
            box-shadow: none !important;
        }
        /* اطمینان از اعمال در تمام موارد (در صورت نبودن jdp-container در سلسله‌مراتب) */
        .jdp-day.disabled-day {
            opacity: 0.45 !important;
            color: #9aa0a6 !important;
            background-color: #eeeeee !important;
            cursor: not-allowed !important;
            pointer-events: none !important;
            transform: none !important;
            box-shadow: none !important;
        }
        
        jdp-container .jdp-day:not(.disabled-day) {
            cursor: pointer;
            transition: all 0.15s linear;
        }
        
        jdp-container .jdp-day:not(.disabled-day):hover {
            background-color: rgba(0, 0, 0, 0.08) !important;
        }
        
        .chart-container {
            position: relative;
            height: 80vh;
            width: 100%;
            margin-bottom: 8px;
            background-color: #fff;
            border-radius: 6px;
            box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
            padding: 1px;
            overflow-x: auto;
            -webkit-overflow-scrolling: touch;
            will-change: transform, scroll-position;
            transform: translateZ(0);
            touch-action: pan-x; /* اجازه اسکرول افقی روی موبایل */
            cursor: grab; /* برای درگ دسکتاپ */
            direction: ltr; /* جلوگیری از اسکرول اولیه به انتها در حالت RTL صفحه */
            user-select: none;
            -webkit-user-select: none;
            overscroll-behavior-x: contain;
        }
        .chart-container.dragging { cursor: grabbing; }
        
        @media (max-width: 768px) {
            .chart-container {
                height: 70vh;
                padding: 1px;
                -webkit-overflow-scrolling: touch;
                overflow-x: scroll;
                -webkit-transform: translate3d(0,0,0);
                transform: translate3d(0,0,0);
                backface-visibility: hidden;
                -webkit-backface-visibility: hidden;
                perspective: 1000;
                -webkit-perspective: 1000;
                will-change: scroll-position;
            }

        /* حداقل عرض برای ایجاد اسکرول افقی؛ مقدار دقیق‌تر را در جاوااسکریپت تنظیم می‌کنیم */
        #temperatureChartWrapper { min-width: 1200px; }
        @media (max-width: 768px) {
            #temperatureChartWrapper { min-width: 900px; }
        }
            
            #temperatureChartWrapper {
                min-width: 250%;
                height: 100%;
                transform: translateZ(0);
                -webkit-transform: translateZ(0);
            }
        }
        #temperatureChartWrapper {
            min-width: 100%;
            height: 100%;
            transform: translateZ(0); /* بهبود عملکرد رندرینگ */
        }
        
        .chart-title {
            text-align: center;
            margin-bottom: 10px;
            font-size: 18px;
            font-weight: bold;
            color: #495057;
        }
        /* Page header: customer(disc) (start), center title, device code (end) */
        .page-header { display: grid; grid-template-columns: auto 1fr auto; align-items: center; gap: 10px; padding: 6px 12px; background:#ffffff; border:1px solid #e9ecef; border-radius:8px; box-shadow: 0 1px 2px rgba(0,0,0,.04); }
        .header-left { justify-self: start; font-weight: 700; color: #212529; }
        .header-title { justify-self: center; font-size: 20px; font-weight: 900; color: #343a40; line-height: 1.2; }
        .header-right { justify-self: end; font-weight: 700; color: #212529; }
        .muted { color: #6c757d; font-weight: 500; }
        @media (max-width: 768px) {
            .page-header { grid-template-columns: 1fr; text-align: center; }
            .header-left, .header-right { justify-self: center; font-size: 13px; }
            .header-title { font-size: 18px; }
        }
        
        .chart-controls {
            display: flex;
            justify-content: center;
            margin-bottom: 8px;
            gap: 6px;
            flex-wrap: wrap;
        }
        
        .chart-btn {
            padding: 5px 10px;
            background-color: #f8f9fa;
            border: 1px solid #ced4da;
            border-radius: 4px;
            font-size: 14px;
            cursor: pointer;
            transition: background-color 0.2s;
            display: flex;
            align-items: center;
            gap: 5px;
        }
        
        .chart-btn:hover {
            background-color: #e9ecef;
        }
        
        .chart-legend {
            display: flex;
            justify-content: center;
            gap: 15px;
            margin-top: 10px;
            flex-wrap: wrap;
        }
        
        .legend-item {
            display: flex;
            align-items: center;
            gap: 5px;
            font-size: 14px;
            padding: 3px 8px;
            border-radius: 4px;
            background-color: #f8f9fa;
        }
        
        .legend-color {
            width: 12px;
            height: 12px;
            border-radius: 50%;
        }
        
        .error-message {
            display: none;
            color: #dc3545;
            text-align: center;
            margin-bottom: 10px;
            padding: 8px;
            background-color: #f8d7da;
            border-radius: 4px;
            font-size: 14px;
        }
        
        @media (max-width: 768px) {
            .chart-container {
                height: 70vh;
                padding: 3px;
                touch-action: pan-x;
                -webkit-backface-visibility: hidden; /* بهبود عملکرد رندرینگ در موبایل */
                backface-visibility: hidden;
            }
            
            .chart-title {
                font-size: 16px;
                margin-bottom: 8px;
            }
            
            .chart-controls {
                margin-bottom: 6px;
            }
            
            .chart-btn {
                padding: 4px 8px;
                font-size: 12px;
            }
            
            .legend-item {
                padding: 2px 6px;
                font-size: 11px;
            }
        }
        
        .chart-control-box {
            background-color: #fff;
            padding: 15px;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
        }
        
        .form-check {
            margin: 0;
            padding: 10px;
            border-radius: 6px;
            background-color: #f8f9fa;
            transition: all 0.2s ease;
            height: 100%;
            display: flex;
            align-items: center;
            border: 1px solid #e9ecef;
            position: relative;
        }
        
        .form-check:hover {
            background-color: #e9ecef;
            transform: translateY(-1px);
            box-shadow: 0 2px 4px rgba(0, 0, 0, 0.05);
        }
        
        .form-check-input {
            cursor: pointer;
            margin: 0;
            width: 18px;
            height: 18px;
            border: 2px solid #6c757d;
            border-radius: 4px;
            transition: all 0.2s ease;
            position: absolute;
            right: 10px;
            top: 50%;
            transform: translateY(-50%);
        }
        
        /* اجازه بده رنگ چک باکس توسط قوانین اختصاصی هر آیتم تعیین شود */
        .form-check-input:checked {
            background-color: inherit;
            border-color: inherit;
        }
        
        .form-check-input:focus {
            box-shadow: 0 0 0 0.2rem rgba(13, 110, 253, 0.25);
            border-color: #86b7fe;
        }
        
        .form-check-label {
            cursor: pointer;
            user-select: none;
            font-size: 14px;
            font-weight: 500;
            color: #495057;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
            transition: color 0.2s ease;
            padding-right: 35px; /* فاصله برای چک‌باکس */
            width: 100%;
            display: block;
        }
        
        .form-check:hover .form-check-label {
            color: #0d6efd;
        }

        /* رنگ چک‌باکس‌ها مطابق رنگ دیتاست‌ها */
        #showTemperature.form-check-input { accent-color: rgba(53, 162, 235, 1); }
        #showFreezer.form-check-input { accent-color: rgba(255, 0, 0, 1); }
        #showMotor.form-check-input { accent-color: rgba(255, 99, 132, 1); }
        #showPower.form-check-input { accent-color: rgba(0, 0, 0, 1); }
        #showElement1.form-check-input { accent-color: rgba(153, 102, 255, 1); }
        #showElement2.form-check-input { accent-color: rgba(75, 192, 192, 1); }
        #showFDC.form-check-input { accent-color: rgba(255, 205, 86, 1); }
        #showFAC.form-check-input { accent-color: rgba(54, 162, 235, 1); }
        #showCurrent.form-check-input { accent-color: rgba(0, 100, 0, 1); }
        #showPowerConsumption.form-check-input { accent-color: rgba(75, 192, 192, 1); }

        /* اورراید قطعی رنگ در حالت checked برای سازگاری با Bootstrap */
        #showTemperature.form-check-input:checked { background-color: rgba(53, 162, 235, 1); border-color: rgba(53, 162, 235, 1); }
        #showFreezer.form-check-input:checked { background-color: rgba(255, 0, 0, 1); border-color: rgba(255, 0, 0, 1); }
        #showMotor.form-check-input:checked { background-color: rgba(255, 99, 132, 1); border-color: rgba(255, 99, 132, 1); }
        #showPower.form-check-input:checked { background-color: rgba(0, 0, 0, 1); border-color: rgba(0, 0, 0, 1); }
        #showElement1.form-check-input:checked { background-color: rgba(153, 102, 255, 1); border-color: rgba(153, 102, 255, 1); }
        #showElement2.form-check-input:checked { background-color: rgba(75, 192, 192, 1); border-color: rgba(75, 192, 192, 1); }
        #showFDC.form-check-input:checked { background-color: rgba(255, 205, 86, 1); border-color: rgba(255, 205, 86, 1); }
        #showFAC.form-check-input:checked { background-color: rgba(54, 162, 235, 1); border-color: rgba(54, 162, 235, 1); }
        #showCurrent.form-check-input:checked { background-color: rgba(0, 100, 0, 1); border-color: rgba(0, 100, 0, 1); }
        #showPowerConsumption.form-check-input:checked { background-color: rgba(75, 192, 192, 1); border-color: rgba(75, 192, 192, 1); }

        /* بک‌گراند و رنگ لیبل‌ها مطابق رنگ دیتاست‌ها */
        label[for="showTemperature"] { background-color: rgba(53, 162, 235, 0.08); border-right: 4px solid rgba(53, 162, 235, 0.35); }
        label[for="showFreezer"] { background-color: rgba(255, 0, 0, 0.08); border-right: 4px solid rgba(255, 0, 0, 0.35); }
        label[for="showMotor"] { background-color: rgba(255, 99, 132, 0.08); border-right: 4px solid rgba(255, 99, 132, 0.35); }
        label[for="showPower"] { background-color: rgba(0, 0, 0, 0.06); border-right: 4px solid rgba(0, 0, 0, 0.25); }
        label[for="showElement1"] { background-color: rgba(153, 102, 255, 0.08); border-right: 4px solid rgba(153, 102, 255, 0.35); }
        label[for="showElement2"] { background-color: rgba(75, 192, 192, 0.08); border-right: 4px solid rgba(75, 192, 192, 0.35); }
        label[for="showFDC"] { background-color: rgba(255, 205, 86, 0.08); border-right: 4px solid rgba(255, 205, 86, 0.35); }
        label[for="showFAC"] { background-color: rgba(54, 162, 235, 0.08); border-right: 4px solid rgba(54, 162, 235, 0.35); }
        label[for="showCurrent"] { background-color: rgba(0, 100, 0, 0.08); border-right: 4px solid rgba(0, 100, 0, 0.35); }
        label[for="showPowerConsumption"] { background-color: rgba(75, 192, 192, 0.08); border-right: 4px solid rgba(75, 192, 192, 0.35); }

        /* در حالت انتخاب شده: افزایش غلظت رنگ پس‌زمینه و همرنگ شدن متن */
        #showTemperature:checked + .form-check-label { color: rgba(53, 162, 235, 1); background-color: rgba(53, 162, 235, 0.15); }
        #showFreezer:checked + .form-check-label { color: rgba(255, 0, 0, 1); background-color: rgba(255, 0, 0, 0.15); }
        #showMotor:checked + .form-check-label { color: rgba(255, 99, 132, 1); background-color: rgba(255, 99, 132, 0.15); }
        #showPower:checked + .form-check-label { color: rgba(0, 0, 0, 1); background-color: rgba(0, 0, 0, 0.12); }
        #showElement1:checked + .form-check-label { color: rgba(153, 102, 255, 1); background-color: rgba(153, 102, 255, 0.15); }
        #showElement2:checked + .form-check-label { color: rgba(75, 192, 192, 1); background-color: rgba(75, 192, 192, 0.15); }
        #showFDC:checked + .form-check-label { color: rgba(255, 205, 86, 1); background-color: rgba(255, 205, 86, 0.2); }
        #showFAC:checked + .form-check-label { color: rgba(54, 162, 235, 1); background-color: rgba(54, 162, 235, 0.15); }
        #showCurrent:checked + .form-check-label { color: rgba(0, 100, 0, 1); background-color: rgba(0, 100, 0, 0.15); }
        #showPowerConsumption:checked + .form-check-label { color: rgba(75, 192, 192, 1); background-color: rgba(75, 192, 192, 0.15); }
        
        @media (max-width: 768px) {
            .chart-control-box {
                padding: 10px;
            }
            
            .form-check {
                padding: 8px;
            }
            
            .form-check-label {
                font-size: 12px;
                padding-right: 30px; /* فاصله کمتر برای موبایل */
            }
            
            .form-check-input {
                width: 16px;
                height: 16px;
                right: 8px;
            }
            
            .row {
                margin: 0 -4px;
            }
            
            .col-4 {
                padding: 0 4px;
            }
        }
    </style>
    <style>
        /* Live toggle: bigger and colored */
        #liveToggle.form-check-input { width: 3.2em; height: 1.8em; cursor: pointer; vertical-align: middle; }
        #liveToggle.form-check-input { background-color: #adb5bd; border-color: #adb5bd; }
        #liveToggle.form-check-input:checked { background-color: #198754; border-color: #198754; }
        .live-switch { display: flex; align-items: center; gap: .5rem; flex-wrap: nowrap; justify-content: flex-start; }
        /* Override global absolute positioning for the live switch input */
        .live-switch .form-check-input { position: static !important; right: auto !important; top: auto !important; transform: none !important; margin-left: .5rem; margin-right: 0; }
        .live-switch .form-check-label { font-size: 1.1rem; font-weight: 700; user-select: none; white-space: nowrap; display: inline-block; }

        /* Dim and block manual controls when disabled */
        .disabled-card { opacity: 0.55; pointer-events: none; filter: grayscale(0.2); }

        /* Live card styling to match manual card */
        #liveCard.live-card {
            background: #f8f9fa;
            border: 1px solid #e9ecef;
            border-radius: 6px;
        }
        /* Connection status card */
        #connStatusCard { background:#f8f9fa; border:1px solid #e9ecef; border-radius:6px; padding:8px 10px; }
        .conn-row { display:flex; align-items:center; justify-content:space-between; gap:8px; }
        .conn-left { font-weight:800; color:#212529; }
        .conn-right { display:flex; align-items:center; gap:10px; }
        .status-badge { border-radius:9999px; padding:3px 10px; font-weight:800; font-size:12px; border:1px solid transparent; }
        .status-online { background:#d1e7dd; color:#0f5132; border-color:#badbcc; }
        .status-offline { background:#f8d7da; color:#842029; border-color:#f5c2c7; }
        .conn-muted { color:#6c757d; font-weight:600; font-size:12px; }
        /* Status table styles */
        .status-table-wrapper { margin-top: 10px; overflow-x: auto; -webkit-overflow-scrolling: touch; max-width: 100%; }
        table.status-table { width: 100%; border-collapse: collapse; background: #fff; border: 1px solid #e9ecef; border-radius: 6px; overflow: hidden; }
        table.status-table th, table.status-table td { padding: 8px 10px; border-bottom: 1px solid #f1f3f5; font-size: 13px; text-align: center; white-space: nowrap; }
        table.status-table th { background: #f8f9fa; font-weight: 700; color: #343a40; }
        table.status-table tr:last-child td { border-bottom: none; }
        table.status-table tbody td:nth-child(3),
        table.status-table tbody td:nth-child(4) { font-weight: 800; }
        .badge-status { display: inline-block; padding: 3px 8px; border-radius: 9999px; font-size: 12px; font-weight: 700; }
        .badge-on { background: #d1e7dd; color: #0f5132; border: 1px solid #badbcc; }
        .badge-off { background: #f8d7da; color: #842029; border: 1px solid #f5c2c7; }
        /* row colors to match graph colors */
        .row-motor td { background: rgba(255, 99, 132, 0.06); }
        .row-motor { border-right: 4px solid rgba(255, 99, 132, 0.8); }
        .row-heater1 td { background: rgba(153, 102, 255, 0.06); }
        .row-heater1 { border-right: 4px solid rgba(153, 102, 255, 0.8); }
        .row-heater2 td { background: rgba(75, 192, 192, 0.06); }
        .row-heater2 { border-right: 4px solid rgba(75, 192, 192, 0.8); }
        .device-card { background: #e7f1ff; color: #0b5ed7 !important; border: 1px solid #cfe2ff; padding: 4px 10px; border-radius: 10px; display: inline-block; font-weight: 900; box-shadow: inset 0 0 0 1px rgba(11,94,215,.05); }
        .disc-text { color: #dc3545 !important; font-weight: 800; }
        @media (max-width: 768px) {
            table.status-table th, table.status-table td { font-size: 12px; padding: 6px; }
        }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <asp:ScriptManager ID="ScriptManager1" runat="server" EnablePageMethods="true" />
        <!-- Error Toast Container -->
        <div id="toastContainer" class="position-fixed p-3" style="z-index: 1080; bottom: 1rem; left: 50%; transform: translateX(-50%);">
            <div id="appErrorToast" class="toast align-items-center text-bg-danger border-0" role="alert" aria-live="assertive" aria-atomic="true">
                <div class="d-flex">
                    <div class="toast-body" id="appErrorToastBody">خطایی رخ داد.</div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
                </div>
            </div>
        </div>
        
        <div class="container-fluid mt-3 mb-5 px-0">
            <div class="row g-0">
                <div class="col-12 px-0">
                    <div class="page-header mb-1">
                        <div class="header-left" id="headerCustomerDisc"><span class="header-label">مشتری:</span> <span id="customerNameSpan">-</span> (<span class="disc-text" id="customerMobileSpan">—</span>)</div>
                        <div class="header-title">نمودار عملکرد (با جزيبات)</div>
                        <div class="header-right" id="headerDeviceCode"><span class="device-card">کد دستگاه: <span id="deviceCodeSpan">-</span></span></div>
                    </div>
                    <div id="monitorInfoCard" style="display:none;background:#fff;border-radius:8px;margin-bottom:10px;box-shadow:0 1px 4px rgba(0,0,0,0.08);font-size:13px;direction:rtl;text-align:right;overflow:hidden;">
                        <div style="background:linear-gradient(135deg,#00A8A8,#007a7a);color:#fff;padding:8px 12px;font-size:13px;font-weight:700;display:flex;align-items:center;gap:8px;">
                            <svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' width='14' height='14'><rect x='3' y='3' width='18' height='18' rx='2'/><path d='M9 3v18M15 3v18M3 9h18M3 15h18'/></svg>
                            مشخصات مشتری و رسید
                        </div>
                        <div style="padding:8px 12px;">
                            <div style="display:grid;grid-template-columns:1fr 1fr;gap:6px;">
                                <div style="display:flex;flex-direction:column;padding:4px 8px;background:#f8fafc;border-radius:6px;">
                                    <span style="font-size:10px;color:#8896a6;font-weight:600;">مشتری</span>
                                    <span id="infoCustomerName" style="font-size:12px;color:#1e2a3a;font-weight:500;">-</span>
                                </div>
                                <div style="display:flex;flex-direction:column;padding:4px 8px;background:#f8fafc;border-radius:6px;">
                                    <span style="font-size:10px;color:#8896a6;font-weight:600;">موبایل</span>
                                    <span id="infoCustomerMobile" style="font-size:12px;color:#1e2a3a;font-weight:500;direction:ltr;text-align:left;">-</span>
                                </div>
                                <div style="display:flex;flex-direction:column;padding:4px 8px;background:#f8fafc;border-radius:6px;">
                                    <span style="font-size:10px;color:#8896a6;font-weight:600;">دستگاه</span>
                                    <span id="infoDeviceTitle" style="font-size:12px;color:#1e2a3a;font-weight:500;">-</span>
                                </div>
                                <div style="display:flex;flex-direction:column;padding:4px 8px;background:#f8fafc;border-radius:6px;">
                                    <span style="font-size:10px;color:#8896a6;font-weight:600;">تعمیرگاه</span>
                                    <span id="infoWorkshopName" style="font-size:12px;color:#1e2a3a;font-weight:500;">-</span>
                                </div>
                                <div style="display:flex;flex-direction:column;padding:4px 8px;background:#f8fafc;border-radius:6px;">
                                    <span style="font-size:10px;color:#8896a6;font-weight:600;">شماره رسید</span>
                                    <span id="infoReceiptId" style="font-size:12px;color:#1e2a3a;font-weight:500;">-</span>
                                </div>
                                <div style="display:flex;flex-direction:column;padding:4px 8px;background:#f8fafc;border-radius:6px;">
                                    <span style="font-size:10px;color:#8896a6;font-weight:600;">تاریخ شروع</span>
                                    <span id="infoCreatedAt" style="font-size:12px;color:#1e2a3a;font-weight:500;">-</span>
                                </div>
                                <div style="grid-column:1/-1;display:flex;flex-direction:column;padding:4px 8px;background:#f8fafc;border-radius:6px;">
                                    <span style="font-size:10px;color:#8896a6;font-weight:600;">شرح مشکل</span>
                                    <span id="infoProblemDesc" style="font-size:12px;color:#1e2a3a;font-weight:500;">-</span>
                                </div>
                            </div>
                        </div>
                    </div>
                    <div id="errorMessage" class="error-message"></div>
                    
                    <!-- باکس کنترل نمودارها -->
                    <div class="chart-control-box mb-3">
                        <div class="row g-3 align-items-stretch">
                            <!-- ستون سمت چپ: همه چک‌باکس‌ها -->
                            <div class="col-12 col-lg-8 order-lg-2">
                                <div class="row g-2">
                                    <div class="col-6 col-md-4">
                                        <div class="form-check">
                                            <input class="form-check-input" type="checkbox" id="showTemperature" checked>
                                            <label class="form-check-label" for="showTemperature">دمای یخچال</label>
                                        </div>
                                    </div>
                                    <div class="col-6 col-md-4">
                                        <div class="form-check">
                                            <input class="form-check-input" type="checkbox" id="showFreezer" checked>
                                            <label class="form-check-label" for="showFreezer">دمای فریزر</label>
                                        </div>
                                    </div>
                                    <div class="col-6 col-md-4">
                                        <div class="form-check">
                                            <input class="form-check-input" type="checkbox" id="showMotor">
                                            <label class="form-check-label" for="showMotor"> موتور</label>
                                        </div>
                                    </div>
                                    <div class="col-6 col-md-4">
                                        <div class="form-check">
                                            <input class="form-check-input" type="checkbox" id="showPower">
                                            <label class="form-check-label" for="showPower">برق دستگاه</label>
                                        </div>
                                    </div>
                                    <div class="col-6 col-md-4">
                                        <div class="form-check">
                                            <input class="form-check-input" type="checkbox" id="showElement1">
                                            <label class="form-check-label" for="showElement1">هیتر 1</label>
                                        </div>
                                    </div>
                                    <div class="col-6 col-md-4">
                                        <div class="form-check">
                                            <input class="form-check-input" type="checkbox" id="showElement2">
                                            <label class="form-check-label" for="showElement2">هیتر 2</label>
                                        </div>
                                    </div>
                                    <div class="col-6 col-md-4 d-none">
                                        <div class="form-check">
                                            <input class="form-check-input" type="checkbox" id="showFDC">
                                            <label class="form-check-label" for="showFDC">فن دی سی</label>
                                        </div>
                                    </div>
                                    <div class="col-6 col-md-4 d-none">
                                        <div class="form-check">
                                            <input class="form-check-input" type="checkbox" id="showFAC">
                                            <label class="form-check-label" for="showFAC">فن اسی</label>
                                        </div>
                                    </div>
                                    <div class="col-6 col-md-4">
                                        <div class="form-check">
                                            <input class="form-check-input" type="checkbox" id="showCurrent">
                                            <label class="form-check-label" for="showCurrent">جریان</label>
                                        </div>
                                    </div>
                                    <div class="col-6 col-md-4">
                                        <div class="form-check">
                                            <input class="form-check-input" type="checkbox" id="showPowerConsumption">
                                            <label class="form-check-label" for="showPowerConsumption">توان</label>
                                        </div>
                                    </div>
                                </div>
                                <!-- Status table INSIDE the checkbox column, below the checkboxes -->
                                <div class="status-table-wrapper">
                                    <table class="status-table" id="statusTable">
                                        <thead>
                                            <tr>
                                                <th>تجهیز</th>
                                                <th>وضعیت فعلی</th>
                                                <th>میزان توقف قبل از آخرین استارت</th>
                                                <th>زمان آخرین استارت</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            <tr class="row-motor">
                                                <td>موتور</td>
                                                <td><span class="badge-status" id="statusMotor">-</span></td>
                                                <td id="stopBeforeStartMotor">-</td>
                                                <td id="lastStartMotor">-</td>
                                            </tr>
                                            <tr class="row-heater1">
                                                <td>هیتر 1</td>
                                                <td><span class="badge-status" id="statusHeater1">-</span></td>
                                                <td id="stopBeforeStartHeater1">-</td>
                                                <td id="lastStartHeater1">-</td>
                                            </tr>
                                            <tr class="row-heater2">
                                                <td>هیتر 2</td>
                                                <td><span class="badge-status" id="statusHeater2">-</span></td>
                                                <td id="stopBeforeStartHeater2">-</td>
                                                <td id="lastStartHeater2">-</td>
                                            </tr>
                                        </tbody>
                                    </table>
                                </div>
                            </div>

                            <!-- ستون دست راست: انتخاب بازه زمانی و دکمه بارگذاری -->
                            <div class="col-12 col-lg-4 order-lg-1" lang="fa-IR" dir="rtl">
                                <div id="manualControlsCard" class="p-2 mb-2" style="background:#f8f9fa;border:1px solid #e9ecef;border-radius:6px;">
                                    <div class="mb-2">
                                        <label for="startDateTime" class="form-label mb-1">شروع زمانی فیلتر</label>
                                        <input type="text" class="form-control" id="startDateTime" placeholder="تاریخ و ساعت شروع" data-jdp data-jdp-time="true" lang="fa-IR" dir="ltr" autocomplete="off" />
                                        <div id="startDateTimeFeedback" class="invalid-feedback">این فیلد الزامی است.</div>
                                    </div>
                                    <div class="mb-2">
                                        <label for="endDateTime" class="form-label mb-1">پایان زمانی فیلتر</label>
                                        <input type="text" class="form-control" id="endDateTime" placeholder="تاریخ و ساعت پایان" data-jdp data-jdp-time="true" lang="fa-IR" dir="ltr" autocomplete="off" />
                                        <div id="endDateTimeFeedback" class="invalid-feedback">این فیلد الزامی است.</div>
                                    </div>
                                    <div class="d-grid">
                                        <button type="button" id="btnLoadData" class="btn btn-primary" disabled>
                                            <i class="bi bi-arrow-repeat ms-1"></i>
                                            در حال بارگذاری تاریخ‌ها...
                                        </button>
                                    </div>
                                </div>
                                <div id="liveCard" class="live-card p-2 mb-2" dir="rtl">
                                    <div class="form-check form-switch live-switch m-0">
                                        <input class="form-check-input" type="checkbox" id="liveToggle">
                                        <label class="form-check-label" for="liveToggle">نمایش زنده (<span id="liveCountdownText">هر ۱۰ ثانیه</span>)</label>
                                    </div>
                                </div>
                                <div id="connStatusCard" class="mb-2" dir="rtl">
                                    <div class="conn-row">
                                        <div class="conn-left">
                                            کل رکوردها: <span id="connRecordCount">-</span>
                                        </div>
                                        <div class="conn-right">
                                            <span class="conn-muted" id="connLastTs">آخرین: -</span>
                                            <span class="status-badge status-offline" id="connBadge">آفلاین</span>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                    
                    <div class="chart-container">
                        <div id="temperatureChartWrapper">
                            <canvas id="temperatureChart"></canvas>
                        </div>
                    </div>
                    
                    <div class="chart-legend">
                        <div class="legend-item">
                            <div class="legend-color" style="background-color: rgba(53, 162, 235, 0.8);"></div>
                            <span>دمای یخچال</span>
                        </div>
                        <div class="legend-item">
                            <div class="legend-color" style="background-color: rgba(255, 99, 132, 0.8);"></div>
                            <span>وضعیت موتور</span>
                        </div>
                        <div class="legend-item">
                            <div class="legend-color" style="background-color: rgba(153, 102, 255, 0.8);"></div>
                            <span>هیتر 1</span>
                        </div>
                        <div class="legend-item">
                            <div class="legend-color" style="background-color: rgba(75, 192, 192, 0.8);"></div>
                            <span>هیتر 2</span>
                        </div>
                        <div class="legend-item">
                            <div class="legend-color" style="background-color: rgba(153, 102, 255, 0.8);"></div>
                            <span>توان</span>
                        </div>
                        <div class="legend-item">
                            <div class="legend-color" style="background-color: rgba(0, 100, 0, 0.8);"></div>
                            <span>جریان</span>
                        </div>
                        <div class="legend-item">
                            <div class="legend-color" style="background-color: rgba(0, 0, 0, 0.8);"></div>
                            <span>برق دستگاه</span>
                        </div>
                    </div>
                </div>
            </div>
            
            <!-- Energy Consumption + Estimation (side-by-side on desktop) -->
            <div class="row g-3 align-items-stretch mt-3">
                <!-- Energy Consumption Card -->
                <div class="col-lg-6 col-md-12 mb-3 d-flex">
                    <div class="card border-success shadow-sm h-100 flex-fill">
                        <div class="card-header bg-gradient" style="background: linear-gradient(135deg, #28a745, #20c997);">
                            <h5 class="mb-0 text-white">
                                <i class="bi bi-lightning-charge-fill me-2"></i>
                                مصرف انرژی در بازه انتخابی
                            </h5>
                        </div>
                        <div class="card-body">
                            <div id="energyDetails">
                                <!-- انرژی کل شاخص -->
                                <div class="text-center mb-4 p-3 rounded" style="background: linear-gradient(135deg, #e8f5e8, #f0fff0); border: 2px solid #28a745;">
                                    <h2 class="text-success mb-2" id="totalEnergy" style="font-weight: bold; font-size: 2.5rem;">0.00 kWh</h2>
                                    <div class="d-flex justify-content-center align-items-center">
                                        <i class="bi bi-clock me-2 text-info"></i>
                                        <span class="text-muted" id="totalActiveTime">0.0 ساعت فعالیت</span>
                                    </div>
                                </div>
                                 
                                <!-- جزئیات تفکیکی تجهیزات -->
                                <div class="mb-3">
                                    <h6 class="text-muted fw-bold mb-3">
                                        <i class="bi bi-list-ul me-2"></i>
                                        جزئیات مصرف هر تجهیز
                                    </h6>
                                    <div id="equipmentEnergyDetails">
                                        <!-- Equipment details will be populated here -->
                                    </div>
                                </div>
                            </div>
                            <div id="energyNoData" class="text-center text-muted py-4" style="display: none;">
                                <i class="bi bi-info-circle fs-1 mb-3 d-block"></i>
                                <h6>داده‌ای برای نمایش وجود ندارد</h6>
                                <small>لطفاً ابتدا داده‌ها را بارگذاری کنید</small>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Energy Estimation Card -->
                <div class="col-lg-6 col-md-12 mb-3 d-flex">
                    <div class="card border-warning h-100 flex-fill">
                        <div class="card-header bg-warning text-dark">
                        <h6 class="mb-0">
                            <i class="bi bi-graph-up-arrow"></i>
                            تخمین مصرف (بر اساس الگوی فعلی)
                        </h6>
                    </div>
                    <div class="card-body d-flex flex-column">
                        <div id="energyEstimation" class="flex-grow-1">
                            <div class="table-responsive">
                                <table class="table table-sm">
                                    <thead>
                                        <tr>
                                            <th>تجهیز</th>
                                            <th>روزانه</th>
                                            <th>ماهانه</th>
                                            <th>سالانه</th>
                                        </tr>
                                    </thead>
                                    <tbody id="estimationTableBody">
                                        <!-- Estimation data will be populated here -->
                                    </tbody>
                                    <tfoot>
                                        <tr class="table-success">
                                            <th>مجموع</th>
                                            <th id="totalDailyEstimate">-- kWh</th>
                                            <th id="totalMonthlyEstimate">-- kWh</th>
                                            <th id="totalYearlyEstimate">-- kWh</th>
                                        </tr>
                                    </tfoot>
                                </table>
                            </div>
                            <small class="text-muted mt-2">
                                <i class="bi bi-info-circle"></i>
                                تخمین بر اساس الگوی مصرف در بازه انتخابی محاسبه شده است
                            </small>
                        </div>
                        <div id="estimationNoData" class="text-center text-muted py-3" style="display: none;">
                            <i class="bi bi-info-circle"></i>
                            برای تخمین، حداقل یک روز داده نیاز است
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <asp:HiddenField ID="hdnChartData" runat="server" />
        
        <!-- اسکریپت‌های بوت استرپ -->
<%--        <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/js/bootstrap.bundle.min.js"></script>--%>
            <script src="chartstyle/bootstrap.bundle.min.js" rel="stylesheet"></script>

        <script type="text/javascript">
            document.addEventListener('DOMContentLoaded', function () {
                try {
                    // راه‌اندازی JDP برای ورودی‌های دارای data-jdp
                    if (window.jalaliDatepicker && typeof window.jalaliDatepicker.startWatch === 'function') {
                        window.jalaliDatepicker.startWatch({ time: true });
                    }
                    
                    // تابع محدود کردن بازه دیتاپیکر با retry mechanism
                    window.updateDatePickerRange = function(firstTimestamp, lastTimestamp, retryCount = 0) {
                        try {
                            if (!firstTimestamp || !lastTimestamp) return;
                            
                            // بررسی وجود کتابخانه jalaliDatepicker
                            if (!window.jalaliDatepicker) {
                                console.warn('jalaliDatepicker not available, retry attempt:', retryCount);
                                
                                // تلاش مجدد تا 5 بار با تاخیر
                                if (retryCount < 5) {
                                    setTimeout(function() {
                                        window.updateDatePickerRange(firstTimestamp, lastTimestamp, retryCount + 1);
                                    }, 500 * (retryCount + 1)); // تاخیر افزایشی
                                } else {
                                    console.error('jalaliDatepicker library failed to load after 5 retries');
                                }
                                return;
                            }
                            
                            // بررسی اضافی برای اطمینان از قابلیت استفاده
                            console.log('jalaliDatepicker detected:', typeof window.jalaliDatepicker, window.jalaliDatepicker);
                            
                            // تبدیل timestamp های جلالی به فرمت مناسب دیتاپیکر
                            function parseJalaliForDatePicker(jalaliStr) {
                                // فرمت ورودی: "1404/07/21 22:31:20"
                                // فرمت خروجی: "1404/07/21"
                                if (!jalaliStr || jalaliStr === '-') return null;
                                const parts = jalaliStr.split(' ');
                                return parts[0]; // فقط بخش تاریخ
                            }
                            
                            const minDate = parseJalaliForDatePicker(firstTimestamp);
                            const maxDate = parseJalaliForDatePicker(lastTimestamp);
                            
                            if (!minDate || !maxDate) return;
                            
                            console.log('Setting datepicker range:', { minDate, maxDate, firstTimestamp, lastTimestamp });
                            
                            // ذخیره بازه تاریخ برای استفاده در validation
                            window.dateRange = { minDate, maxDate };
                            
                            // تنظیم محدوده تاریخ برای دیتاپیکر
                            const startInput = document.getElementById('startDateTime');
                            const endInput = document.getElementById('endDateTime');
                            
                            if (startInput && endInput) {
                                // پاک کردن همه attributes قبلی
                                const attributesToRemove = ['data-jdp', 'data-jdp-min-date', 'data-jdp-max-date', 'data-jdp-format', 'data-jdp-time', 'data-jdp-auto-close', 'data-jdp-disable-before', 'data-jdp-disable-after'];
                                attributesToRemove.forEach(attr => {
                                    startInput.removeAttribute(attr);
                                    endInput.removeAttribute(attr);
                                });
                                
                                // تنظیم ویژگی‌های دیتاپیکر با data attributes صحیح
                                startInput.setAttribute('data-jdp', '');
                                startInput.setAttribute('data-jdp-min-date', minDate);
                                startInput.setAttribute('data-jdp-max-date', maxDate);
                                startInput.setAttribute('data-jdp-format', 'YYYY/MM/DD HH:mm:ss');
                                startInput.setAttribute('data-jdp-time', 'true');
                                
                                endInput.setAttribute('data-jdp', '');
                                endInput.setAttribute('data-jdp-min-date', minDate);
                                endInput.setAttribute('data-jdp-max-date', maxDate);
                                endInput.setAttribute('data-jdp-format', 'YYYY/MM/DD HH:mm:ss');
                                endInput.setAttribute('data-jdp-time', 'true');
                                
                                // تنظیم مقادیر پیش‌فرض در بازه مجاز
                                if (!startInput.value || !window.validateSelectedDate(startInput.value, 'start')) {
                                    startInput.value = minDate + ' 00:00:00';
                                }
                                if (!endInput.value || !window.validateSelectedDate(endInput.value, 'end')) {
                                    endInput.value = maxDate + ' 23:59:59';
                                }
                                
                                // تاخیر کوتاه برای اطمینان از پاک شدن کامل تنظیمات قبلی
                                setTimeout(() => {
                                    try {
                                        // تابع کمکی برای بررسی معتبر بودن تاریخ
                                        function isValidDate(year, month, day) {
                                            try {
                                                if (!window.dateRange) return false;
                                                
                                                const { minDate, maxDate } = window.dateRange;
                                                const dateStr = `${year}/${month.toString().padStart(2, '0')}/${day.toString().padStart(2, '0')}`;
                                                
                                                // تبدیل تاریخ‌ها به فرمت قابل مقایسه
                                                function dateToComparable(dateStr) {
                                                    const parts = dateStr.split('/');
                                                    if (parts.length !== 3) return 0;
                                                    return parseInt(parts[0]) * 10000 + parseInt(parts[1]) * 100 + parseInt(parts[2]);
                                                }
                                                
                                                const dateNum = dateToComparable(dateStr);
                                                const minNum = dateToComparable(minDate);
                                                const maxNum = dateToComparable(maxDate);
                                                
                                                const isValid = dateNum >= minNum && dateNum <= maxNum;
                                                console.log(`Date validation: ${dateStr} -> ${isValid} (range: ${minDate} to ${maxDate})`);
                                                return isValid;
                                            } catch (error) {
                                                console.warn('Error in isValidDate:', error);
                                                return false;
                                            }
                                        }
                                        
                                        // راه‌اندازی دیتاپیکر طبق مثال رسمی کتابخانه
                                        window.jalaliDatepicker.startWatch({
                                            minDate: "attr", // استفاده از data-jdp-min-date
                                            maxDate: "attr", // استفاده از data-jdp-max-date
                                            time: true,
                                            date: true,
                                            hasSecond: true,
                                            hideAfterChange: true,
                                            autoHide: true,
                                            showTodayBtn: true,
                                            showEmptyBtn: true,
                                            dayRendering: function(dayOptions, input) {
                                                const isValid = isValidDate(dayOptions.year, dayOptions.month, dayOptions.day);
                                                console.log(`dayRendering called for ${dayOptions.year}/${dayOptions.month}/${dayOptions.day} -> isValid: ${isValid}`);
                                                
                                                if (!isValid) {
                                                    return {
                                                        isValid: false, // غیرفعال کردن کامل روز
                                                        className: 'disabled-day' // کلاس غیرفعال
                                                    };
                                                }
                                                
                                                return {
                                                    isValid: true
                                                };
                                            }
                                        });
                                        
                                        // بررسی DOM واقعی و اعمال استایل مستقیم
                                        setTimeout(() => {
                                            const forceDisableInvalidDates = () => {
                                                console.log('Checking for disabled days in DOM...');
                                                
                                                // بررسی تمام selectors ممکن
                                                const selectors = [
                                                    'jdp-container .jdp-day.disabled-day',
                                                    '.jdp-day.disabled-day',
                                                    '.jdp-container .jdp-day.disabled-day',
                                                    '[class*="disabled-day"]',
                                                    '.jdp-day[class*="disabled"]'
                                                ];
                                                
                                                let foundDisabled = false;
                                                selectors.forEach(selector => {
                                                    const elements = document.querySelectorAll(selector);
                                                    if (elements.length > 0) {
                                                        console.log(`Found ${elements.length} disabled days with selector: ${selector}`);
                                                        foundDisabled = true;
                                                        elements.forEach(d => {
                                                            // اعمال استایل مستقیم
                                                            d.style.opacity = '0.3';
                                                            d.style.backgroundColor = '#f0f0f0';
                                                            d.style.color = '#999';
                                                            d.style.cursor = 'not-allowed';
                                                            d.style.pointerEvents = 'none';
                                                            d.style.textDecoration = 'line-through';
                                                            console.log('Applied disabled styles to:', d);
                                                        });
                                                    }
                                                });
                                                
                                                // اگر هیچ disabled day پیدا نشد، بررسی کلی DOM
                                                if (!foundDisabled) {
                                                    console.log('No disabled days found, checking all day elements...');
                                                    const allDays = document.querySelectorAll('.jdp-day, [class*="jdp-day"], [class*="day"]');
                                                    console.log(`Found ${allDays.length} day elements total`);
                                                    allDays.forEach(day => {
                                                        console.log('Day element classes:', day.className, 'Text:', day.textContent);
                                                    });
                                                }
                                            };

                                            // اجرای اولیه
                                            forceDisableInvalidDates();

                                            // نظارت مداوم
                                            const observer = new MutationObserver(() => {
                                                setTimeout(forceDisableInvalidDates, 100);
                                            });
                                            observer.observe(document.body, { childList: true, subtree: true });
                                            window.calendarObserver = observer;
                                        }, 500);
                                        
                                        console.log(`Date picker range updated with dayRendering: ${minDate} to ${maxDate}`);
                                        
                                    } catch (error) {
                                        console.warn('Error in advanced datepicker setup:', error);
                                        // fallback به تنظیمات ساده
                                        window.jalaliDatepicker.startWatch({ time: true });
                                    }
                                    
                                    console.log(`Date picker range updated: ${minDate} to ${maxDate}`);
                                }, 100);
                                
                                // اضافه کردن event listener برای اعتبارسنجی
                                startInput.addEventListener('change', function() {
                                    window.validateSelectedDate(this.value, 'start');
                                });
                                
                                endInput.addEventListener('change', function() {
                                    window.validateSelectedDate(this.value, 'end');
                                });
                            }        
                        } catch (error) {
                            console.warn('Error updating datepicker range:', error);
                            
                            // تلاش مجدد در صورت خطا
                            if (retryCount < 3) {
                                setTimeout(function() {
                                    window.updateDatePickerRange(firstTimestamp, lastTimestamp, retryCount + 1);
                                }, 1000);
                            }
                        }
                    };
                    
                    // تابع اعتبارسنجی تاریخ انتخابی (بدون alert)
                    window.validateSelectedDate = function(selectedDate, fieldType) {
                        try {
                            if (!window.dateRange || !selectedDate) return true;
                            
                            const { minDate, maxDate } = window.dateRange;
                            const selectedDateOnly = selectedDate.split(' ')[0]; // فقط بخش تاریخ
                            
                            // تبدیل تاریخ‌ها به فرمت قابل مقایسه
                            function dateToComparable(dateStr) {
                                const parts = dateStr.split('/');
                                return parseInt(parts[0]) * 10000 + parseInt(parts[1]) * 100 + parseInt(parts[2]);
                            }
                            
                            const selectedNum = dateToComparable(selectedDateOnly);
                            const minNum = dateToComparable(minDate);
                            const maxNum = dateToComparable(maxDate);
                            
                            if (selectedNum < minNum || selectedNum > maxNum) {
                                // تصحیح خودکار تاریخ به بازه مجاز بدون نمایش پیام خطا
                                const inputId = fieldType === 'start' ? 'startDateTime' : 'endDateTime';
                                const input = document.getElementById(inputId);
                                if (input) {
                                    const correctedDate = selectedNum < minNum ? minDate : maxDate;
                                    input.value = correctedDate + ' 00:00:00';
                                }
                                
                                console.log(`Date auto-corrected from ${selectedDate} to valid range`);
                                return false;
                            }
                            
                            return true;
                        } catch (error) {
                            console.warn('Error validating selected date:', error);
                            return true;
                        }
                    };

                    // توابع کمکی برای کنترل بازه تاریخ در دیتاپیکر
                    window.isDateInValidRange = function(dateText) {
                        try {
                            if (!window.dateRange || !dateText) return false;
                            
                            const { minDate, maxDate } = window.dateRange;
                            
                            // تبدیل تاریخ‌ها به فرمت قابل مقایسه
                            function dateToComparable(dateStr) {
                                const parts = dateStr.split('/');
                                if (parts.length !== 3) return 0;
                                return parseInt(parts[0]) * 10000 + parseInt(parts[1]) * 100 + parseInt(parts[2]);
                            }
                            
                            const dateNum = dateToComparable(dateText);
                            const minNum = dateToComparable(minDate);
                            const maxNum = dateToComparable(maxDate);
                            
                            return dateNum >= minNum && dateNum <= maxNum;
                        } catch (error) {
                            console.warn('Error in isDateInValidRange:', error);
                            return false;
                        }
                    };

                    window.disableInvalidDates = function() {
                        try {
                            if (!window.dateRange) return;
                            
                            const { minDate, maxDate } = window.dateRange;
                            
                            // پیدا کردن همه روزهای تقویم
                            const dayElements = document.querySelectorAll('.jdp-calendar .jdp-day');
                            
                            dayElements.forEach(dayElement => {
                                const dateText = dayElement.textContent.trim();
                                if (dateText && !window.isDateInValidRange(dateText)) {
                                    // غیرفعال کردن تاریخ‌های خارج از بازه
                                    dayElement.classList.add('jdp-disabled');
                                    dayElement.style.pointerEvents = 'none';
                                    dayElement.style.opacity = '0.3';
                                    dayElement.style.textDecoration = 'line-through';
                                    dayElement.style.backgroundColor = '#f5f5f5';
                                    dayElement.style.color = '#ccc';
                                    dayElement.setAttribute('disabled', 'true');
                                    dayElement.setAttribute('aria-disabled', 'true');
                                }
                            });
                            
                            console.log(`Disabled invalid dates outside range: ${minDate} to ${maxDate}`);
                        } catch (error) {
                            console.warn('Error in disableInvalidDates:', error);
                        }
                    };

                    // تابع تست عملکرد دیتاپیکر (قابل فراخوانی از کنسول)
                    window.testDatePickerFunctionality = function() {
                        console.log('=== Testing DatePicker Functionality ===');
                        
                        const startInput = document.getElementById('startDateTime');
                        const endInput = document.getElementById('endDateTime');
                        
                        if (!startInput || !endInput) {
                            console.error('Date input elements not found');
                            return false;
                        }
                        
                        console.log('Current date range:', window.dateRange);
                        console.log('Start input value:', startInput.value);
                        console.log('End input value:', endInput.value);
                        
                        // بررسی وجود attributes
                        const startAttrs = {
                            'data-jdp': startInput.getAttribute('data-jdp'),
                            'data-jdp-min-date': startInput.getAttribute('data-jdp-min-date'),
                            'data-jdp-max-date': startInput.getAttribute('data-jdp-max-date'),
                            'data-jdp-format': startInput.getAttribute('data-jdp-format'),
                            'data-jdp-time': startInput.getAttribute('data-jdp-time')
                        };
                        
                        console.log('Start input attributes:', startAttrs);
                        
                        // تست validation function
                        if (window.dateRange) {
                            const { minDate, maxDate } = window.dateRange;
                            
                            // تست تاریخ معتبر
                            const validDate = minDate + ' 12:00:00';
                            const validResult = window.validateSelectedDate(validDate, 'start');
                            console.log(`Valid date test (${validDate}):`, validResult);
                            
                            // تست تاریخ نامعتبر (قبل از بازه)
                            const invalidEarlyDate = '1404/01/01 12:00:00';
                            const invalidEarlyResult = window.validateSelectedDate(invalidEarlyDate, 'start');
                            console.log(`Invalid early date test (${invalidEarlyDate}):`, invalidEarlyResult);
                            
                            // تست تاریخ نامعتبر (بعد از بازه)
                            const invalidLateDate = '1404/12/29 12:00:00';
                            const invalidLateResult = window.validateSelectedDate(invalidLateDate, 'start');
                            console.log(`Invalid late date test (${invalidLateDate}):`, invalidLateResult);
                        }
                        
                        console.log('=== DatePicker Test Complete ===');
                        return true;
                    };

                    // غیرفعال کردن تولتیپ پیش‌فرض Chart.js
                    Chart.defaults.plugins.tooltip.enabled = false;

                    // ثبت اجباری پلاگین زوم برای Chart.js v4 (در برخی بیلدها خودکار نیست)
                    try {
                        if (window.ChartZoom && Chart && Chart.register) { Chart.register(window.ChartZoom); }
                    } catch (e) { console.warn('ChartZoom register failed', e); }

                    // توابع نمایش لودینگ زوم در وسط نمای فعلی چارت
                    window.showZoomLoading = function () {
                        let zoomLoader = document.getElementById('zoomLoadingOverlay');
                        if (!zoomLoader) {
                            zoomLoader = document.createElement('div');
                            zoomLoader.id = 'zoomLoadingOverlay';
                            zoomLoader.innerHTML = `
                                <div id="zoomLoadingContent" style="
                                    position: fixed;
                                    background: rgba(40, 167, 69, 0.95);
                                    color: white;
                                    padding: 12px 20px;
                                    border-radius: 8px;
                                    font-family: Vazir, sans-serif;
                                    font-size: 14px;
                                    font-weight: bold;
                                    box-shadow: 0 4px 12px rgba(0,0,0,0.3);
                                    z-index: 9999;
                                    display: flex;
                                    align-items: center;
                                    gap: 8px;
                                    pointer-events: none;
                                ">
                                    <div style="
                                        width: 16px;
                                        height: 16px;
                                        border: 2px solid rgba(255,255,255,0.3);
                                        border-top: 2px solid white;
                                        border-radius: 50%;
                                        animation: spin 1s linear infinite;
                                    "></div>
                                    در حال دریافت جزییات زوم...
                                </div>
                                <style>
                                    @keyframes spin {
                                        0% { transform: rotate(0deg); }
                                        100% { transform: rotate(360deg); }
                                    }
                                </style>
                            `;
                            document.body.appendChild(zoomLoader);
                        }

                        // محاسبه موقعیت وسط نمای فعلی چارت
                        const chartContainer = document.querySelector('.chart-container');
                        const loadingContent = document.getElementById('zoomLoadingContent');
                        if (chartContainer && loadingContent) {
                            const containerRect = chartContainer.getBoundingClientRect();
                            const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
                            const scrollLeft = window.pageXOffset || document.documentElement.scrollLeft;

                            // موقعیت وسط نمای قابل مشاهده چارت
                            const centerX = containerRect.left + scrollLeft + (containerRect.width / 2);
                            const centerY = containerRect.top + scrollTop + (containerRect.height / 2);

                            // اگر چارت خارج از نمای صفحه است، در وسط viewport نمایش بده
                            const viewportCenterX = window.innerWidth / 2;
                            const viewportCenterY = window.innerHeight / 2;

                            const finalX = (containerRect.top < 0 || containerRect.bottom > window.innerHeight) ? viewportCenterX : centerX;
                            const finalY = (containerRect.top < 0 || containerRect.bottom > window.innerHeight) ? viewportCenterY + scrollTop : centerY;

                            loadingContent.style.left = finalX + 'px';
                            loadingContent.style.top = finalY + 'px';
                            loadingContent.style.transform = 'translate(-50%, -50%)';
                        }

                        zoomLoader.style.display = 'block';
                    };

                    window.hideZoomLoading = function () {
                        const zoomLoader = document.getElementById('zoomLoadingOverlay');
                        if (zoomLoader) {
                            zoomLoader.style.display = 'none';
                        }
                    };

                    // تابع فیلتر داده‌ها بر اساس بازه زمانی
                    window.filterDataByRange = function (data, startJ, endJ) {
                        if (!data || !data.Timestamps || !data.Timestamps.length) return data;

                        const filtered = {
                            Timestamps: [], Temperatures: [], FreezerTemperatures: [], MotorStates: [], PowerStates: [],
                            Element1States: [], Element2States: [], FDCStates: [], FACStates: [], CurrentStates: [],
                            PowerConsumptionStates: [], CurrentValues: [], PowerValues: [], Notes: [], Ids: []
                        };

                        for (let i = 0; i < data.Timestamps.length; i++) {
                            const timestamp = data.Timestamps[i];
                            if (timestamp >= startJ && timestamp <= endJ) {
                                filtered.Timestamps.push(timestamp);
                                if (data.Temperatures && data.Temperatures[i] !== undefined) filtered.Temperatures.push(data.Temperatures[i]);
                                if (data.FreezerTemperatures && data.FreezerTemperatures[i] !== undefined) filtered.FreezerTemperatures.push(data.FreezerTemperatures[i]);
                                if (data.MotorStates && data.MotorStates[i] !== undefined) filtered.MotorStates.push(data.MotorStates[i]);
                                if (data.PowerStates && data.PowerStates[i] !== undefined) filtered.PowerStates.push(data.PowerStates[i]);
                                if (data.Element1States && data.Element1States[i] !== undefined) filtered.Element1States.push(data.Element1States[i]);
                                if (data.Element2States && data.Element2States[i] !== undefined) filtered.Element2States.push(data.Element2States[i]);
                                if (data.FDCStates && data.FDCStates[i] !== undefined) filtered.FDCStates.push(data.FDCStates[i]);
                                if (data.FACStates && data.FACStates[i] !== undefined) filtered.FACStates.push(data.FACStates[i]);
                                if (data.CurrentStates && data.CurrentStates[i] !== undefined) filtered.CurrentStates.push(data.CurrentStates[i]);
                                if (data.PowerConsumptionStates && data.PowerConsumptionStates[i] !== undefined) filtered.PowerConsumptionStates.push(data.PowerConsumptionStates[i]);
                                if (data.CurrentValues && data.CurrentValues[i] !== undefined) filtered.CurrentValues.push(data.CurrentValues[i]);
                                if (data.PowerValues && data.PowerValues[i] !== undefined) filtered.PowerValues.push(data.PowerValues[i]);
                                if (data.Notes && data.Notes[i] !== undefined) filtered.Notes.push(data.Notes[i]);
                                if (data.Ids && data.Ids[i] !== undefined) filtered.Ids.push(data.Ids[i]);
                            }
                        }

                        return filtered;
                    };

                    // متغیر سراسری برای ذخیره داده انرژی (جلوگیری از خواندن مکرر از دیتابیس)
                    window.__energyData = null;
                    window.__energyDataParams = null;

                    // تابع تبدیل ساعت اعشاری به فرمت HH:MM:SS
                    window.formatHoursToHHMMSS = function(hours) {
                        if (typeof hours !== 'number' || isNaN(hours) || hours < 0) return '00:00:00';
                        
                        const totalSeconds = Math.round(hours * 3600);
                        const h = Math.floor(totalSeconds / 3600);
                        const m = Math.floor((totalSeconds % 3600) / 60);
                        const s = totalSeconds % 60;
                        
                        return String(h).padStart(2, '0') + ':' + 
                               String(m).padStart(2, '0') + ':' + 
                               String(s).padStart(2, '0');
                    };

                    // تابع جدید: دریافت تمام داده‌های خام برای محاسبه انرژی
                    window.fetchAllDataForEnergy = async function (monitorId, startJalali, endJalali) {
                        const allData = {
                            Timestamps: [], Temperatures: [], FreezerTemperatures: [], MotorStates: [], PowerStates: [],
                            Element1States: [], Element2States: [], FDCStates: [], FACStates: [], CurrentStates: [],
                            PowerConsumptionStates: [], CurrentValues: [], PowerValues: [], Notes: [], Ids: []
                        };

                        let page = 0;
                        const pageSize = 1000; // حداکثر تعداد رکورد در هر درخواست
                        let hasMore = true;
                        let totalFetched = 0;

                        console.log('ENERGY DEBUG - Starting to fetch all data for energy calculation');

                        while (hasMore) {
                            try {
                                console.log(`ENERGY DEBUG - Fetching chunk ${page + 1} with params:`, {
                                    monitorId, startJalali, endJalali, page, pageSize
                                });

                                const chunk = await new Promise((resolve, reject) => {
                                    // تایم‌اوت برای جلوگیری از انتظار بی‌نهایت
                                    const timeoutId = setTimeout(() => {
                                        reject(new Error('Request timeout'));
                                    }, 30000); // 30 ثانیه تایم‌اوت

                                    PageMethods.GetChartDataChunk(
                                        monitorId, startJalali, endJalali, page, pageSize,
                                        function (res) { 
                                            clearTimeout(timeoutId);
                                            try { 
                                                console.log(`ENERGY DEBUG - Raw response for chunk ${page + 1}:`, typeof res, res ? 'has data' : 'no data');
                                                const parsed = (typeof res === 'string') ? JSON.parse(res) : res;
                                                resolve(parsed); 
                                            } catch (e) { 
                                                console.error('ENERGY DEBUG - Parse error:', e);
                                                reject(e); 
                                            } 
                                        },
                                        function (err) { 
                                            clearTimeout(timeoutId);
                                            console.error('ENERGY DEBUG - Server error:', err);
                                            reject(err); 
                                        }
                                    );
                                });

                                console.log(`ENERGY DEBUG - Parsed chunk ${page + 1}:`, {
                                    hasTimestamps: !!(chunk && chunk.Timestamps),
                                    timestampCount: chunk && chunk.Timestamps ? chunk.Timestamps.length : 0,
                                    hasMore: chunk ? chunk.HasMore : false,
                                    totalCount: chunk ? chunk.TotalCount : 0
                                });

                                if (!chunk || !chunk.Timestamps || chunk.Timestamps.length === 0) {
                                    console.log(`ENERGY DEBUG - No more data in chunk ${page + 1}, stopping`);
                                    hasMore = false;
                                    break;
                                }

                                // اضافه کردن داده‌های این تکه به مجموعه کل
                                const pushAll = (arr, src) => { 
                                    if (Array.isArray(src) && src.length) {
                                        Array.prototype.push.apply(arr, src); 
                                    }
                                };

                                pushAll(allData.Timestamps, chunk.Timestamps);
                                pushAll(allData.Temperatures, chunk.Temperatures);
                                pushAll(allData.FreezerTemperatures, chunk.FreezerTemperatures);
                                pushAll(allData.MotorStates, chunk.MotorStates);
                                pushAll(allData.PowerStates, chunk.PowerStates);
                                pushAll(allData.Element1States, chunk.Element1States);
                                pushAll(allData.Element2States, chunk.Element2States);
                                pushAll(allData.FDCStates, chunk.FDCStates);
                                pushAll(allData.FACStates, chunk.FACStates);
                                pushAll(allData.CurrentStates, chunk.CurrentStates);
                                pushAll(allData.PowerConsumptionStates, chunk.PowerConsumptionStates);
                                pushAll(allData.CurrentValues, chunk.CurrentValues);
                                pushAll(allData.PowerValues, chunk.PowerValues);
                                pushAll(allData.Notes, chunk.Notes);
                                pushAll(allData.Ids, chunk.Ids);

                                totalFetched += chunk.Timestamps.length;
                                hasMore = chunk.HasMore;
                                page++;

                                console.log(`ENERGY DEBUG - Fetched chunk ${page}: ${chunk.Timestamps.length} records, total: ${totalFetched}, hasMore: ${hasMore}`);

                            } catch (error) {
                                console.error('ENERGY DEBUG - Error fetching chunk:', error);
                                hasMore = false;
                            }
                        }

                        console.log('ENERGY DEBUG - Finished fetching all data:', {
                            totalRecords: allData.Timestamps.length,
                            samplePowerValues: allData.PowerValues.slice(0, 10),
                            powerValuesRange: allData.PowerValues.length > 0 ? {
                                min: Math.min(...allData.PowerValues.map(p => parseFloat(p) || 0)),
                                max: Math.max(...allData.PowerValues.map(p => parseFloat(p) || 0))
                            } : null
                        });

                        return allData;
                    };

                    // Energy Calculation Functions - اصلاح شده برای استفاده از داده‌های کامل
                    window.calculateEnergyConsumption = function (data) {
                        if (!data || !data.Timestamps || !data.PowerValues) {
                            console.log('No data or PowerValues available for energy calculation');
                            return { totalEnergy: 0, equipmentDetails: [], totalActiveTime: 0 };
                        }

                        const timestamps = data.Timestamps;
                        const powerValues = data.PowerValues;
                        const motorStates = data.MotorStates || [];
                        const element1States = data.Element1States || [];
                        const element2States = data.Element2States || [];
                        const powerStates = data.PowerStates || [];
                        const currentStates = data.CurrentStates || [];
                        const powerConsumptionStates = data.PowerConsumptionStates || [];

                        console.log('ENERGY DEBUG - Input data:', {
                            timestamps: timestamps.length,
                            powerValues: powerValues.length,
                            samplePowerValues: powerValues.slice(0, 10),
                            sampleMotorStates: motorStates.slice(0, 10),
                            sampleElement1States: element1States.slice(0, 10),
                            sampleElement2States: element2States.slice(0, 10),
                            powerValuesType: typeof powerValues[0],
                            motorStatesType: typeof motorStates[0],
                            powerValuesRange: {
                                min: Math.min(...powerValues.slice(0, 100).map(p => parseFloat(p) || 0)),
                                max: Math.max(...powerValues.slice(0, 100).map(p => parseFloat(p) || 0))
                            }
                        });

                        const equipmentMap = {
                            'showMotor': { states: motorStates, name: 'موتور', energy: 0, activeTime: 0 },
                            'showElement1': { states: element1States, name: 'هیتر 1', energy: 0, activeTime: 0 },
                            'showElement2': { states: element2States, name: 'هیتر 2', energy: 0, activeTime: 0 }
                        };

                        let totalEnergy = 0;
                        let totalActiveTime = 0;
                        const activeIntervals = new Set();

                        // تابع تبدیل تاریخ فارسی به میلی‌ثانیه
                        function parseJalaliToMs(jalaliStr) {
                            try {
                                // فرمت: "1404/07/04 07:58:00"
                                const parts = jalaliStr.split(' ');
                                const datePart = parts[0]; // "1404/07/04"
                                const timePart = parts[1]; // "07:58:00"

                                const [year, month, day] = datePart.split('/').map(Number);
                                const [hour, minute, second] = timePart.split(':').map(Number);

                                // تبدیل تقریبی تاریخ جلالی به میلادی (برای محاسبه فاصله زمانی)
                                // سال 1404 = 2025, ماه 7 = اکتبر
                                const gregorianYear = year + 621;
                                const gregorianMonth = month + 3; // تقریبی

                                const date = new Date(gregorianYear, gregorianMonth - 1, day, hour, minute, second);
                                return date.getTime();
                            } catch (e) {
                                return NaN;
                            }
                        }

                        // محاسبه انرژی دقیق بر اساس جمع توان در بازه‌های فعال
                        for (let i = 0; i < timestamps.length; i++) {
                            const currentPower = parseFloat(powerValues[i]) || 0;

                            if (currentPower <= 0) continue;

                            let anyEquipmentActive = false;

                            // بررسی هر تجهیز برای این نقطه زمانی
                            Object.keys(equipmentMap).forEach(checkboxId => {
                                const checkbox = document.getElementById(checkboxId);
                                if (checkbox && checkbox.checked) {
                                    const equipment = equipmentMap[checkboxId];
                                    const isActive = equipment.states[i] === 1;

                                    if (isActive && currentPower > 0) {
                                        // جمع مستقیم توان در هر نقطه فعال (Wh)
                                        equipment.energy += currentPower; // Wh
                                        equipment.activeTime += 1; // تعداد نقاط فعال
                                        anyEquipmentActive = true;

                                        if (i < 10) {
                                            console.log(`ENERGY DEBUG - ${checkboxId} [${i}]:`, {
                                                isActive: isActive,
                                                currentPower: currentPower,
                                                accumulatedEnergy_Wh: equipment.energy,
                                                activePoints: equipment.activeTime
                                            });
                                        }
                                    }
                                }
                            });

                            // محاسبه انرژی کل (بدون همپوشانی)
                            if (anyEquipmentActive && currentPower > 0) {
                                const intervalKey = `point-${i}`;
                                if (!activeIntervals.has(intervalKey)) {
                                    totalEnergy += currentPower; // Wh
                                    totalActiveTime += 1; // تعداد نقاط فعال
                                    activeIntervals.add(intervalKey);

                                    if (i < 10) {
                                        console.log(`ENERGY DEBUG - Adding to total [${i}]:`, {
                                            currentPower: currentPower,
                                            runningTotalEnergy_Wh: totalEnergy,
                                            runningActivePoints: totalActiveTime
                                        });
                                    }
                                }
                            }
                        }

                        // تبدیل Wh به kWh و نقاط به ساعت (تقریبی)
                        const dataPointsPerHour = timestamps.length > 1 ?
                            timestamps.length / ((parseJalaliToMs(timestamps[timestamps.length - 1]) - parseJalaliToMs(timestamps[0])) / (1000 * 60 * 60)) : 1;

                        Object.keys(equipmentMap).forEach(checkboxId => {
                            const equipment = equipmentMap[checkboxId];
                            equipment.energy = equipment.energy / 1000; // تبدیل Wh به kWh
                            equipment.activeTime = equipment.activeTime / dataPointsPerHour; // تبدیل نقاط به ساعت
                        });

                        totalEnergy = totalEnergy / 1000; // تبدیل Wh به kWh
                        totalActiveTime = totalActiveTime / dataPointsPerHour; // تبدیل نقاط به ساعت

                        console.log('ENERGY DEBUG - Conversion info:', {
                            totalDataPoints: timestamps.length,
                            timeSpanHours: (parseJalaliToMs(timestamps[timestamps.length - 1]) - parseJalaliToMs(timestamps[0])) / (1000 * 60 * 60),
                            dataPointsPerHour: dataPointsPerHour
                        });

                        // آماده‌سازی جزئیات تجهیزات
                        const equipmentDetails = [];
                        Object.keys(equipmentMap).forEach(checkboxId => {
                            const checkbox = document.getElementById(checkboxId);
                            if (checkbox && checkbox.checked) {
                                const equipment = equipmentMap[checkboxId];
                                equipmentDetails.push({
                                    name: equipment.name,
                                    energy: isFinite(equipment.energy) ? equipment.energy : 0,
                                    activeTime: isFinite(equipment.activeTime) ? equipment.activeTime : 0,
                                    checkboxId: checkboxId
                                });
                            }
                        });

                        const result = {
                            totalEnergy: isFinite(totalEnergy) ? totalEnergy : 0,
                            equipmentDetails: equipmentDetails,
                            totalActiveTime: isFinite(totalActiveTime) ? totalActiveTime : 0
                        };

                        console.log('ENERGY DEBUG - Final result:', result);
                        console.log('ENERGY DEBUG - Equipment map final state:', {
                            showMotor: equipmentMap.showMotor,
                            showElement1: equipmentMap.showElement1,
                            showElement2: equipmentMap.showElement2
                        });
                        return result;
                    };

                    window.calculateEnergyEstimation = function (energyData, startDate, endDate) {
                        if (!energyData || !energyData.equipmentDetails || energyData.equipmentDetails.length === 0) {
                            return { equipmentEstimations: [], totalDaily: 0, totalMonthly: 0, totalYearly: 0 };
                        }

                        const start = new Date(startDate);
                        const end = new Date(endDate);
                        const durationDays = (end - start) / (1000 * 60 * 60 * 24);

                        if (durationDays <= 0) {
                            return { equipmentEstimations: [], totalDaily: 0, totalMonthly: 0, totalYearly: 0 };
                        }

                        const equipmentEstimations = [];
                        let totalDaily = 0;
                        let totalMonthly = 0;
                        let totalYearly = 0;

                        energyData.equipmentDetails.forEach(equipment => {
                            const dailyEnergy = equipment.energy / durationDays;
                            const monthlyEnergy = dailyEnergy * 30;
                            const yearlyEnergy = dailyEnergy * 365;

                            equipmentEstimations.push({
                                name: equipment.name,
                                daily: dailyEnergy,
                                monthly: monthlyEnergy,
                                yearly: yearlyEnergy,
                                checkboxId: equipment.checkboxId
                            });

                            totalDaily += dailyEnergy;
                            totalMonthly += monthlyEnergy;
                            totalYearly += yearlyEnergy;
                        });

                        return {
                            equipmentEstimations: equipmentEstimations,
                            totalDaily: totalDaily,
                            totalMonthly: totalMonthly,
                            totalYearly: totalYearly,
                            baseDurationDays: durationDays
                        };
                    };

                    // تابع بارگذاری داده انرژی از دیتابیس (فقط یکبار)
                    window.loadEnergyData = async function () {
                        try {
                            // بررسی وجود بازه زمانی انتخابی
                            const startInput = document.getElementById('startDateTime');
                            const endInput = document.getElementById('endDateTime');
                            const startVal = startInput?.value?.trim();
                            const endVal = endInput?.value?.trim();
                            const monitorId = (function () { 
                                try { 
                                    const u = new URL(window.location.href); 
                                    return u.searchParams.get('Id') || ''; 
                                } catch (e) { 
                                    return ''; 
                                } 
                            })();

                            if (!startVal || !endVal || !monitorId) {
                                window.__energyData = null;
                                window.__energyDataParams = null;
                                return null;
                            }

                            // نرمال‌سازی تاریخ‌های جلالی
                            function pad2(n) { return String(n).padStart(2, '0'); }
                            function normalizeJalaliDateTime(s) {
                                const parts = s.trim().split(/[T\s]+/);
                                const datePart = parts[0];
                                const timePart = parts[1] || '00:00';
                                const d = datePart.split(/[-\/]/).map(x => pad2(parseInt(x, 10)));
                                const t = timePart.split(':').map(x => pad2(parseInt(x || '0', 10)));
                                return `${d[0]}/${d[1]}/${d[2]} ${t[0]}:${t[1]}:${t[2] || '00'}`;
                            }

                            const startJalali = normalizeJalaliDateTime(startVal);
                            const endJalali = normalizeJalaliDateTime(endVal);
                            const currentParams = { monitorId, startJalali, endJalali };

                            // بررسی اینکه آیا داده قبلاً برای همین پارامترها بارگذاری شده یا نه
                            if (window.__energyData && window.__energyDataParams && 
                                window.__energyDataParams.monitorId === currentParams.monitorId &&
                                window.__energyDataParams.startJalali === currentParams.startJalali &&
                                window.__energyDataParams.endJalali === currentParams.endJalali) {
                                console.log('ENERGY DEBUG - Using cached energy data');
                                return window.__energyData;
                            }

                            console.log('ENERGY DEBUG - Loading fresh energy data from database:', currentParams);

                            // نمایش لودینگ
                            document.getElementById('totalEnergy').textContent = 'در حال بارگذاری...';
                            document.getElementById('totalActiveTime').textContent = 'در حال بارگذاری...';

                            // استفاده از HTTP Handler برای دریافت مستقیم از دیتابیس
                            const allData = await new Promise((resolve, reject) => {
                                const url = `EnergyDataHandler.ashx?monitorId=${encodeURIComponent(monitorId)}&startJalali=${encodeURIComponent(startJalali)}&endJalali=${encodeURIComponent(endJalali)}`;
                                fetch(url)
                                    .then(response => {
                                        if (!response.ok) {
                                            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                                        }
                                        return response.json();
                                    })
                                    .then(parsed => {
                                        console.log('ENERGY DEBUG - EnergyDataHandler response:', {
                                            timestampCount: parsed && parsed.Timestamps ? parsed.Timestamps.length : 0,
                                            powerValuesCount: parsed && parsed.PowerValues ? parsed.PowerValues.length : 0,
                                            motorStatesCount: parsed && parsed.MotorStates ? parsed.MotorStates.length : 0,
                                            element1StatesCount: parsed && parsed.Element1States ? parsed.Element1States.length : 0,
                                            element2StatesCount: parsed && parsed.Element2States ? parsed.Element2States.length : 0
                                        });
                                        resolve(parsed);
                                    })
                                    .catch(err => {
                                        console.error('ENERGY DEBUG - EnergyDataHandler error:', err);
                                        reject(err);
                                    });
                            });

                            if (!allData || !allData.Timestamps || !allData.PowerValues || allData.Timestamps.length === 0) {
                                window.__energyData = null;
                                window.__energyDataParams = null;
                                return null;
                            }

                            // ذخیره داده و پارامترها در کش
                            window.__energyData = allData;
                            window.__energyDataParams = currentParams;
                            console.log('ENERGY DEBUG - Energy data cached successfully');
                            
                            return allData;
                        } catch (error) {
                            console.error('Error loading energy data:', error);
                            window.__energyData = null;
                            window.__energyDataParams = null;
                            return null;
                        }
                    };

                    // تابع محاسبه و نمایش کارت‌های انرژی (بدون بارگذاری مجدد از دیتابیس)
                    window.updateEnergyCards = async function () {
                        try {
                            // بارگذاری داده (فقط در صورت نیاز)
                            const allData = await loadEnergyData();
                            
                            if (!allData) {
                                document.getElementById('energyDetails').style.display = 'none';
                                document.getElementById('energyNoData').style.display = 'block';
                                document.getElementById('energyEstimation').style.display = 'none';
                                document.getElementById('estimationNoData').style.display = 'block';
                                return;
                            }

                            // محاسبه انرژی با داده‌های کش‌شده
                            const energyResult = calculateEnergyConsumption(allData);
                            console.log('ENERGY DEBUG - updateEnergyCards result (from cache):', energyResult);


                            // همیشه کارت‌ها را نمایش بده، حتی اگر هیچ تجهیزی انتخاب نشده باشد
                            document.getElementById('energyDetails').style.display = 'block';
                            document.getElementById('energyNoData').style.display = 'none';

                            // به‌روزرسانی انرژی کل
                            document.getElementById('totalEnergy').textContent = energyResult.totalEnergy.toFixed(2) + ' kWh';
                            document.getElementById('totalActiveTime').textContent = formatHoursToHHMMSS(energyResult.totalActiveTime) + ' فعالیت';
                            // به‌روزرسانی جزئیات تجهیزات
                            const detailsContainer = document.getElementById('equipmentEnergyDetails');
                            detailsContainer.innerHTML = '';

                            if (energyResult.equipmentDetails.length === 0) {
                                detailsContainer.innerHTML = '<div class="text-muted text-center py-3"><i class="bi bi-gear me-2"></i><small>برای نمایش جزئیات، تجهیزات مورد نظر را انتخاب کنید</small></div>';
                            } else {
                                energyResult.equipmentDetails.forEach((equipment, index) => {
                                    const equipmentDiv = document.createElement('div');
                                    equipmentDiv.className = 'equipment-item';

                                    // اضافه کردن خط جداکننده قبل از تجهیز (به جز اولین مورد)
                                    if (index > 0) {
                                        const separator = document.createElement('hr');
                                        separator.className = 'my-3';
                                        separator.style.borderColor = '#dee2e6';
                                        separator.style.borderWidth = '1px';
                                        detailsContainer.appendChild(separator);
                                    }

                                    equipmentDiv.innerHTML = `
                                        <div class="d-flex justify-content-between align-items-center py-2">
                                            <div class="equipment-name">
                                                <i class="bi bi-gear-fill me-2 text-primary"></i>
                                                <span class="fw-bold text-dark">${equipment.name}</span>
                                            </div>
                                        </div>
                                        <div class="equipment-stats d-flex justify-content-between align-items-center mt-2">
                                            <div class="text-center">
                                                <div class="badge bg-success fs-6 px-3 py-2">${equipment.energy.toFixed(2)} kWh</div>
                                                <small class="text-muted d-block mt-1">انرژی مصرفی</small>
                                            </div>
                                            <div class="text-center">
                                                <div class="badge bg-info fs-6 px-3 py-2">${formatHoursToHHMMSS(equipment.activeTime)}</div>
                                                <small class="text-muted d-block mt-1">زمان فعالیت</small>
                                            </div>
                                        </div>
                                    `;
                                    detailsContainer.appendChild(equipmentDiv);
                                });
                            }
                            
                            // محاسبه تخمین بر اساس بازه زمانی انتخابی
                            const startDate = new Date(startJalali.replace(/(\d{4})\/(\d{2})\/(\d{2})/, '$1-$2-$3'));
                            const endDate = new Date(endJalali.replace(/(\d{4})\/(\d{2})\/(\d{2})/, '$1-$2-$3'));
                            const estimation = calculateEnergyEstimation(energyResult, startDate, endDate);

                            if (estimation.totalMonthly > 0) {
                                document.getElementById('energyEstimation').style.display = 'block';
                                document.getElementById('estimationNoData').style.display = 'none';

                                // به‌روزرسانی تخمین‌ها
                                document.getElementById('totalDailyEstimate').textContent = estimation.totalDaily.toFixed(1) + ' kWh';
                                document.getElementById('totalMonthlyEstimate').textContent = estimation.totalMonthly.toFixed(0) + ' kWh';
                                document.getElementById('totalYearlyEstimate').textContent = estimation.totalYearly.toFixed(0) + ' kWh';
                                
                                // پر کردن جدول تخمین‌ها
                                const tableBody = document.getElementById('estimationTableBody');
                                tableBody.innerHTML = '';
                                estimation.equipmentEstimations.forEach(equipment => {
                                    const row = document.createElement('tr');
                                    row.innerHTML = `
                                        <td><i class="bi bi-gear me-2 text-primary"></i>${equipment.name}</td>
                                        <td>${equipment.daily.toFixed(1)} kWh</td>
                                        <td>${equipment.monthly.toFixed(0)} kWh</td>
                                        <td>${equipment.yearly.toFixed(0)} kWh</td>
                                    `;
                                    tableBody.appendChild(row);
                                });
                            } else {
                                document.getElementById('energyEstimation').style.display = 'none';
                                document.getElementById('estimationNoData').style.display = 'block';
                            }

                        } catch (error) {
                            console.error('Error updating energy cards:', error);
                        }
                    };

                    // اضافه کردن event listener برای چک‌باکس‌ها
                    const energyCheckboxes = ['showMotor', 'showElement1', 'showElement2', 'showPower', 'showCurrent'];
                    // تابع سریع محاسبه مجدد انرژی (بدون بارگذاری از دیتابیس)
                    window.recalculateEnergyCards = function () {
                        try {
                            if (!window.__energyData) {
                                console.log('ENERGY DEBUG - No cached energy data available for recalculation');
                                return;
                            }

                            console.log('ENERGY DEBUG - Recalculating energy cards from cached data');
                            
                            // محاسبه انرژی با داده‌های کش‌شده
                            const energyResult = calculateEnergyConsumption(window.__energyData);
                            console.log('ENERGY DEBUG - Recalculated energy result:', energyResult);

                            // همیشه کارت‌ها را نمایش بده
                            document.getElementById('energyDetails').style.display = 'block';
                            document.getElementById('energyNoData').style.display = 'none';

                            // به‌روزرسانی انرژی کل
                            document.getElementById('totalEnergy').textContent = energyResult.totalEnergy.toFixed(2) + ' kWh';
                            document.getElementById('totalActiveTime').textContent = formatHoursToHHMMSS(energyResult.totalActiveTime) + ' فعالیت';
                            
                            // به‌روزرسانی جزئیات تجهیزات
                            const detailsContainer = document.getElementById('equipmentEnergyDetails');
                            detailsContainer.innerHTML = '';

                            if (energyResult.equipmentDetails.length === 0) {
                                detailsContainer.innerHTML = '<div class="text-muted text-center py-3"><i class="bi bi-gear me-2"></i><small>برای نمایش جزئیات، تجهیزات مورد نظر را انتخاب کنید</small></div>';
                            } else {
                                energyResult.equipmentDetails.forEach((equipment, index) => {
                                    const equipmentDiv = document.createElement('div');
                                    equipmentDiv.className = 'equipment-item';
                                    equipmentDiv.innerHTML = `
                                        <div class="equipment-card p-3 mb-3 border rounded" style="background: linear-gradient(135deg, #f8f9fa, #e9ecef); border-left: 4px solid #007bff !important;">
                                            <div class="d-flex justify-content-between align-items-center">
                                                <div class="equipment-name">
                                                    <i class="bi bi-gear-fill me-2 text-primary"></i>
                                                    <span class="fw-bold text-dark">${equipment.name}</span>
                                                </div>
                                            </div>
                                            <div class="equipment-stats d-flex justify-content-between align-items-center mt-2">
                                                <div class="text-center">
                                                    <div class="badge bg-success fs-6 px-3 py-2">${equipment.energy.toFixed(2)} kWh</div>
                                                    <small class="text-muted d-block mt-1">انرژی مصرفی</small>
                                                </div>
                                                <div class="text-center">
                                                    <div class="badge bg-info fs-6 px-3 py-2">${formatHoursToHHMMSS(equipment.activeTime)}</div>
                                                    <small class="text-muted d-block mt-1">زمان فعالیت</small>
                                                </div>
                                            </div>
                                        </div>
                                    `;
                                    detailsContainer.appendChild(equipmentDiv);
                                });
                            }
                            
                            // محاسبه تخمین بر اساس بازه زمانی انتخابی
                            if (window.__energyDataParams) {
                                const startDate = new Date(window.__energyDataParams.startJalali.replace(/(\d{4})\/(\d{2})\/(\d{2})/, '$1-$2-$3'));
                                const endDate = new Date(window.__energyDataParams.endJalali.replace(/(\d{4})\/(\d{2})\/(\d{2})/, '$1-$2-$3'));
                                const estimation = calculateEnergyEstimation(energyResult, startDate, endDate);

                                if (estimation.totalMonthly > 0) {
                                    document.getElementById('energyEstimation').style.display = 'block';
                                    document.getElementById('estimationNoData').style.display = 'none';

                                    // به‌روزرسانی تخمین‌ها
                                    document.getElementById('totalDailyEstimate').textContent = estimation.totalDaily.toFixed(1) + ' kWh';
                                    document.getElementById('totalMonthlyEstimate').textContent = estimation.totalMonthly.toFixed(0) + ' kWh';
                                    document.getElementById('totalYearlyEstimate').textContent = estimation.totalYearly.toFixed(0) + ' kWh';
                                    
                                    // پر کردن جدول تخمین‌ها
                                    const tableBody = document.getElementById('estimationTableBody');
                                    tableBody.innerHTML = '';
                                    estimation.equipmentEstimations.forEach(equipment => {
                                        const row = document.createElement('tr');
                                        row.innerHTML = `
                                            <td><i class="bi bi-gear me-2 text-primary"></i>${equipment.name}</td>
                                            <td>${equipment.daily.toFixed(1)} kWh</td>
                                            <td>${equipment.monthly.toFixed(0)} kWh</td>
                                            <td>${equipment.yearly.toFixed(0)} kWh</td>
                                        `;
                                        tableBody.appendChild(row);
                                    });
                                } else {
                                    document.getElementById('energyEstimation').style.display = 'none';
                                    document.getElementById('estimationNoData').style.display = 'block';
                                }
                            }
                        } catch (error) {
                            console.error('Error recalculating energy cards:', error);
                        }
                    };

                    energyCheckboxes.forEach(checkboxId => {
                        const checkbox = document.getElementById(checkboxId);
                        if (checkbox) {
                            checkbox.addEventListener('change', function () {
                                // فقط محاسبه مجدد (بدون بارگذاری از دیتابیس)
                                setTimeout(() => {
                                    recalculateEnergyCards();
                                }, 50); // تأخیر کمتر برای پاسخ سریع‌تر
                            });
                        }
                    });

                    // نمایش پیام راهنما تا زمانی که داده بارگذاری نشود
                    showError('برای نمایش نمودار، بازه تاریخ و ساعت را انتخاب کرده و روی «بارگذاری داده» کلیک کنید.');

                    // نمایش اولیه کارت‌های انرژی (خالی)
                    document.getElementById('energyDetails').style.display = 'block';

                    // تست فوری محاسبه انرژی پس از بارگذاری داده
                    window.testEnergyCalculation = function () {
                        console.log('ENERGY DEBUG - Testing energy calculation with current data');
                        if (window.__chartData) {
                            console.log('ENERGY DEBUG - Chart data available, testing calculation');
                            const result = calculateEnergyConsumption(window.__chartData);
                            console.log('ENERGY DEBUG - Test result:', result);
                            updateEnergyCards();
                        } else {
                            console.log('ENERGY DEBUG - No chart data available for testing');
                        }
                    };
                    document.getElementById('energyNoData').style.display = 'none';
                    document.getElementById('totalEnergy').textContent = '0.00 kWh';
                    document.getElementById('totalActiveTime').textContent = '00:00:00 فعالیت';
                    document.getElementById('equipmentEnergyDetails').innerHTML = '<div class="text-muted text-center py-2"><small>برای نمایش جزئیات، تجهیزات مورد نظر را انتخاب کنید</small></div>';

                    document.getElementById('energyEstimation').style.display = 'none';
                    document.getElementById('estimationNoData').style.display = 'block';

                    // هندل بارگذاری داده بر اساس بازه انتخابی
                    function getQueryParam(name) { const url = new URL(window.location.href); return url.searchParams.get(name); }
                    function persianizeErrorMessage(msg) {
                        if (!msg) return 'خطای نامشخص رخ داد.';
                        const s = String(msg);
                        if (/timeout/i.test(s)) return 'زمان درخواست به پایان رسید.';
                        if (/network|failed to fetch|canceled/i.test(s)) return 'ارتباط با سرور برقرار نشد.';
                        if (/not found|404/i.test(s)) return 'منبع درخواستی یافت نشد.';
                        if (/500|server error/i.test(s)) return 'خطای داخلی سرور رخ داد.';
                        return s; // نمایش همان پیام در صورت عدم تطابق
                    }
                    const btn = document.getElementById('btnLoadData');
                    // سراسری: وضعیت بارگذاری تکه‌ای و اسکرول
                    let chunkLoader = null;
                    const getContainer = () => document.querySelector('.chart-container');
                    const renderFromAcc = () => {
                        try {
                            if (chartInstance) { try { chartInstance.destroy(); } catch (e) { } chartInstance = null; }
                            document.getElementById('<%= hdnChartData.ClientID %>').value = JSON.stringify(chunkLoader.acc);
                            initializeChart(null, null);
                            updateEnergyCards();
                            // اگر همچنان لبه راست هستیم، بلافاصله تکه بعدی را بخوان
                            setTimeout(async () => {
                                try { if (atRightEdge()) await loadNextChunk(true); } catch (e) { }
                            }, 0);
                        } catch (e) { console.warn('renderFromAcc error', e); }
                    };

                    // Global: build note annotations (lines + labels) for both dataset notes and extra notes
                    window.createNoteAnnotations = function (data) {
                        try {
                            const anns = [];
                            const ts = Array.isArray(data.Timestamps) ? data.Timestamps : [];
                            const notes = Array.isArray(data.Notes) ? data.Notes : [];
                            const isMobile = window.innerWidth < 768;
                            // dataset-bound notes
                            for (let i = 0; i < ts.length; i++) {
                                const n = notes[i];
                                if (n && String(n).trim() !== '') {
                                    // line on y1 from 0 up to a bit above power band (centered at 1)
                                    anns.push({
                                        id: `note-line-${i}`,
                                        type: 'line', xMin: ts[i], xMax: ts[i], xScaleID: 'x', yScaleID: 'y1', yMin: 0, yMax: 1.06,
                                        borderColor: '#000', borderWidth: 1, borderDash: [3, 3], display: true, clip: false, z: 1800
                                    });
                                    // label clickable
                                    anns.push({
                                        id: `note-label-${i}`,
                                        type: 'label', xValue: ts[i], xScaleID: 'x', yScaleID: 'y1', yValue: 1.06, yAdjust: -2,
                                        display: true, clip: false,
                                        backgroundColor: 'rgba(255,255,255,0.85)', borderColor: '#000', borderWidth: 1, color: '#000',
                                        content: [' حاوی توضیحات'], font: { size: isMobile ? 9 : 11, weight: 'bold', family: 'Vazir' }, padding: { top: 2, bottom: 2, left: 4, right: 4 },
                                        rotation: 0, textAlign: 'center', xAdjust: 8, z: 2000,
                                        listeners: { click: function () { try { const monitorId = (function () { try { const u = new URL(window.location.href); return u.searchParams.get('Id') || ''; } catch (e) { return ''; } })(); const id = (window.__chartData && window.__chartData.Ids) ? window.__chartData.Ids[i] : ''; if (window.openNoteModal) { window.openNoteModal({ monitorId, index: i, id: id, ts: ts[i], note: n }); } } catch (e) { console.warn('note label click error', e); } } }
                                    });
                                }
                            }
                            // extra notes from server
                            const extra = window.__extraNotes || {};
                            const ets = Array.isArray(extra.Timestamps) ? extra.Timestamps : [];
                            const eids = Array.isArray(extra.Ids) ? extra.Ids : [];
                            const enotes = Array.isArray(extra.Notes) ? extra.Notes : [];
                            for (let j = 0; j < ets.length; j++) {
                                const nts = ets[j];
                                const ntext = enotes[j];
                                if (!ntext || String(ntext).trim() === '') continue;
                                // skip if already covered by dataset note
                                if (ts.indexOf(nts) !== -1 && notes[ts.indexOf(nts)] && String(notes[ts.indexOf(nts)]).trim() !== '') continue;
                                // line
                                anns.push({ id: `note-line-extra-${j}`, type: 'line', xMin: nts, xMax: nts, xScaleID: 'x', yScaleID: 'y1', yMin: 0, yMax: 1.06, borderColor: '#000', borderWidth: 1, borderDash: [3, 3], display: true, clip: false, z: 1800 });
                                // label
                                anns.push({
                                    id: `note-label-extra-${j}`,
                                    type: 'label',
                                    xValue: nts,
                                    xScaleID: 'x',
                                    yScaleID: 'y1',
                                    yValue: 1.06,
                                    yAdjust: -2,
                                    display: true,
                                    clip: false,
                                    backgroundColor: 'rgba(255,255,255,0.85)',
                                    borderColor: '#000',
                                    borderWidth: 1,
                                    color: '#000',
                                    content: ['📝 حاوی توضیحات'],
                                    font: { size: isMobile ? 9 : 11, weight: 'bold', family: 'Vazir' },
                                    padding: { top: 2, bottom: 2, left: 4, right: 4 },
                                    rotation: 0,
                                    textAlign: 'center',
                                    xAdjust: 8,
                                    z: 2000,
                                    listeners: {
                                        click: function () {
                                            try {
                                                const monitorId = (function () {
                                                    try { const u = new URL(window.location.href); return u.searchParams.get('Id') || ''; } catch (e) { return ''; }
                                                })();
                                                const idx = ts.indexOf(nts);
                                                const id = (Array.isArray(eids) ? eids[j] : null) || ((window.__chartData && Array.isArray(window.__chartData.Ids) && idx >= 0) ? window.__chartData.Ids[idx] : null);
                                                if (window.openNoteModal) { window.openNoteModal({ monitorId, index: idx, id: id, ts: nts, note: ntext }); }
                                            } catch (e) { console.warn('note label click error', e); }
                                        }
                                    }
                                });
                            }
                            return anns;
                        } catch (e) { console.warn('createNoteAnnotations error', e); return []; }
                    };
                    const atRightEdge = () => {
                        const c = getContainer(); if (!c) return false;
                        const pxNear = (c.scrollLeft + c.clientWidth) >= (c.scrollWidth - 200);
                        const ratioNear = (c.scrollLeft + c.clientWidth) / Math.max(1, c.scrollWidth) >= 0.85;
                        return pxNear || ratioNear;
                    };
                    const estimateInitialTargetPoints = () => {
                        const c = getContainer();
                        const px = (c && c.clientWidth) ? c.clientWidth : 1200;
                        // فرض: حدود 1 نقطه به ازای هر 3 پیکسل برای نمایش مناسب
                        const target = Math.max(600, Math.min(2000, Math.floor(px / 3)));
                        return target;
                    };
                    const attachScrollLoader = () => {
                        const c = getContainer(); if (!c) return;
                        if (c._scrollBound) return; // جلوگیری از دوبار بستن
                        c._scrollBound = true;
                        let ticking = false;
                        c.addEventListener('scroll', async function () {
                            if (ticking) return; ticking = true;
                            try {
                                if (!chunkLoader || chunkLoader.done || chunkLoader.loading) return;
                                if (atRightEdge()) {
                                    await loadNextChunk(true); // با رندر بعد از هر تکه
                                }
                            } finally { setTimeout(() => { ticking = false; }, 50); }
                        });
                    };
                    // Promise wrapper برای PageMethods
                    const fetchChunk = (monitorId, startJ, endJ, page, pageSize) => new Promise((resolve, reject) => {
                        try {
                            PageMethods.GetChartDataChunk(
                                monitorId, startJ, endJ, page, pageSize,
                                function (res) { try { resolve((typeof res === 'string') ? JSON.parse(res) : res); } catch (e) { reject(e); } },
                                function (err) {
                                    try { var d = (err && err.get_message) ? err.get_message() : (err && err.message) ? err.message : (err && err.responseText) ? err.responseText : ''; reject(new Error(d || 'Server error')); }
                                    catch (ex) { reject(err || ex); }
                                }
                            );
                        } catch (callErr) { reject(callErr); }
                    });
                    // بارگذاری تکه بعدی؛ اگر forceRender=true پس از دریافت، رندر کن
                    async function loadNextChunk(forceRender) {
                        if (!chunkLoader || chunkLoader.loading || chunkLoader.done) return;
                        chunkLoader.loading = true;
                        try {
                            const chunk = await fetchChunk(chunkLoader.monitorId, chunkLoader.startJ, chunkLoader.endJ, chunkLoader.nextPage, chunkLoader.pageSize);
                            if (!chunk) { chunkLoader.done = true; return; }
                            if (chunk.TotalCount != null && chunkLoader.total == null) chunkLoader.total = chunk.TotalCount;
                            const pushAll = (arr, src) => { if (Array.isArray(src) && src.length) Array.prototype.push.apply(arr, src); };
                            pushAll(chunkLoader.acc.Timestamps, chunk.Timestamps);
                            pushAll(chunkLoader.acc.Temperatures, chunk.Temperatures);
                            pushAll(chunkLoader.acc.FreezerTemperatures, chunk.FreezerTemperatures);
                            pushAll(chunkLoader.acc.MotorStates, chunk.MotorStates);
                            pushAll(chunkLoader.acc.PowerStates, chunk.PowerStates);
                            pushAll(chunkLoader.acc.Element1States, chunk.Element1States);
                            pushAll(chunkLoader.acc.Element2States, chunk.Element2States);
                            pushAll(chunkLoader.acc.FDCStates, chunk.FDCStates);
                            pushAll(chunkLoader.acc.FACStates, chunk.FACStates);
                            pushAll(chunkLoader.acc.CurrentStates, chunk.CurrentStates);
                            pushAll(chunkLoader.acc.PowerConsumptionStates, chunk.PowerConsumptionStates);
                            pushAll(chunkLoader.acc.CurrentValues, chunk.CurrentValues);
                            pushAll(chunkLoader.acc.PowerValues, chunk.PowerValues);
                            chunkLoader.nextPage++;
                            chunkLoader.done = !chunk.HasMore;
                            if (forceRender) renderFromAcc();
                            // بروزرسانی متن دکمه
                            if (chunkLoader.total != null) btn.innerText = `در حال بارگذاری... ${chunkLoader.acc.Timestamps.length} / ${chunkLoader.total}`;
                        } catch (e) { console.warn('loadNextChunk error', e); }
                        finally { chunkLoader.loading = false; }
                    }
                    if (btn) {
                        btn.addEventListener('click', async function () {
                            const startInput = document.getElementById('startDateTime');
                            const endInput = document.getElementById('endDateTime');
                            const startVal = startInput?.value?.trim();
                            const endVal = endInput?.value?.trim();

                            // حالت CSV: فیلتر محلی داده‌ها
                            if (window.isCsvMode && window.__chartData && Array.isArray(window.__chartData.Timestamps)) {
                                if (!startVal) { startInput.classList.add('is-invalid'); showError('لطفاً فیلد «شروع» را تکمیل کنید.'); return; }
                                startInput.classList.remove('is-invalid');
                                if (!endVal) { endInput.classList.add('is-invalid'); showError('لطفاً فیلد «پایان» را تکمیل کنید.'); return; }
                                endInput.classList.remove('is-invalid');
                                const allData = window.__chartData;
                                const startTs = new Date(startVal.replace(/[-\/]/g, '/').replace(/(\d{4})\/(\d{2})\/(\d{2})\s+(\d{2}):(\d{2})(?::(\d{2}))?/, function(m, y, mo, d, h, mi, s) { return y + '/' + mo + '/' + d + ' ' + h + ':' + mi + ':' + (s || '00'); }));
                                const endTs = new Date(endVal.replace(/[-\/]/g, '/').replace(/(\d{4})\/(\d{2})\/(\d{2})\s+(\d{2}):(\d{2})(?::(\d{2}))?/, function(m, y, mo, d, h, mi, s) { return y + '/' + mo + '/' + d + ' ' + h + ':' + mi + ':' + (s || '00'); }));
                                if (isNaN(startTs) || isNaN(endTs)) { showError('فرمت تاریخ نامعتبر است.'); return; }
                                const indices = [];
                                for (let i = 0; i < allData.Timestamps.length; i++) {
                                    const ts = new Date(allData.Timestamps[i]);
                                    if (!isNaN(ts) && ts >= startTs && ts <= endTs) indices.push(i);
                                }
                                if (indices.length === 0) { showError('در بازه انتخابی، داده‌ای یافت نشد.'); return; }
                                hideError();
                                const pick = (arr) => Array.isArray(arr) ? indices.map(i => arr[i]) : arr;
                                const filtered = {};
                                const arrayFields = ['Timestamps', 'Temperatures', 'FreezerTemperatures', 'MotorStates', 'PowerStates', 'Element1States', 'Element2States', 'FDCStates', 'FACStates', 'CurrentStates', 'PowerConsumptionStates', 'CurrentValues', 'PowerValues', 'Ids', 'Notes'];
                                arrayFields.forEach(k => { if (allData[k] !== undefined) filtered[k] = pick(allData[k]); });
                                const filteredData = Object.assign({}, allData, filtered);
                                if (chartInstance) { try { chartInstance.destroy(); } catch (e) { } chartInstance = null; }
                                document.getElementById('<%= hdnChartData.ClientID %>').value = JSON.stringify(filteredData);
                                initializeChart(null, null);
                                updateEnergyCards();
                                return;
                            }
                            console.log('========== LOAD DATA CLICKED ==========');
                            console.log('Raw input values:', { startVal, endVal });
                            let hasError = false;
                            if (!startVal) { startInput.classList.add('is-invalid'); hasError = true; }
                            else { startInput.classList.remove('is-invalid'); }
                            if (!endVal) { endInput.classList.add('is-invalid'); hasError = true; }
                            else { endInput.classList.remove('is-invalid'); }
                            if (hasError) { showError('لطفاً فیلدهای «شروع» و «پایان» را تکمیل کنید.'); return; }
                            function pad2(n) { return String(n).padStart(2, '0'); }
                            function normalizeJalaliDateTime(s) {
                                const parts = s.trim().split(/[T\s]+/);
                                const datePart = parts[0];
                                const timePart = parts[1] || '00:00';
                                const d = datePart.split(/[-\/]/).map(x => pad2(parseInt(x, 10)));
                                const t = timePart.split(':').map(x => pad2(parseInt(x || '0', 10)));
                                return `${d[0]}/${d[1]}/${d[2]} ${t[0]}:${t[1]}:${t[2] || '00'}`;
                            }
                            const startJalali = normalizeJalaliDateTime(startVal);
                            const endJalali = normalizeJalaliDateTime(endVal);
                            const monitorId = getMonitorIdFlexible();
                            console.log('Normalized dates:', { startJalali, endJalali, monitorId });
                            console.log('Checking PageMethods.GetChartDataDecimated:', typeof PageMethods?.GetChartDataDecimated);
                            if (PageMethods && typeof PageMethods.GetChartDataDecimated === 'function') {
                                btn.disabled = true; btn.innerText = 'در حال بارگذاری...';
                                try {
                                    const c = document.querySelector('.chart-container');
                                    const px = (c && c.clientWidth) ? c.clientWidth : 1200;
                                    const maxPoints = Math.max(800, Math.min(2000, Math.floor(px / 2)));
                                    console.log('Calling GetChartDataDecimated with:', { monitorId, startJalali, endJalali, maxPoints });
                                    const data = await new Promise((resolve, reject) => {
                                        try {
                                            PageMethods.GetChartDataDecimated(
                                                monitorId, startJalali, endJalali, maxPoints,
                                                function (res) {
                                                    console.log('GetChartDataDecimated RAW response:', typeof res === 'string' ? res.substring(0, 500) : res);
                                                    try { resolve((typeof res === 'string') ? JSON.parse(res) : res); } catch (e) { console.error('JSON parse error:', e); reject(e); }
                                                },
                                                function (err) {
                                                    console.error('GetChartDataDecimated SERVER ERROR:', err?.get_message?.() || err?.message || err?.responseText || err);
                                                    try { var d = (err && err.get_message) ? err.get_message() : (err && err.message) ? err.message : (err && err.responseText) ? err.responseText : ''; reject(new Error(d || 'Server error')); } catch (ex) { reject(err || ex); }
                                                }
                                            );
                                        } catch (callErr) { console.error('PageMethod call error:', callErr); reject(callErr); }
                                    });
                                    console.log('GetChartDataDecimated PARSED data:', data ? { Timestamps_len: data.Timestamps?.length, Temps_len: data.Temperatures?.length, sample_ts: data.Timestamps?.[0] } : 'NULL');
                                    if (!data || !data.Timestamps || !data.Timestamps.length) {
                                        showError('داده‌ای در بازه انتخابی یافت نشد.');
                                        console.warn('EMPTY DATA from GetChartDataDecimated - full response:', JSON.stringify(data).substring(0, 2000));
                                        return;
                                    }
                                    console.log('SUCCESS: Got', data.Timestamps.length, 'data points');
                                    hideError();
                                    if (chartInstance) { try { chartInstance.destroy(); } catch (e) { } chartInstance = null; }
                                    document.getElementById('<%= hdnChartData.ClientID %>').value = JSON.stringify(data);
                                    initializeChart(null, null);
                                    updateEnergyCards();
                                } catch (e) {
                                    showError('خطا در دریافت/پردازش داده: ' + persianizeErrorMessage(e && e.message ? e.message : e));
                                    console.error('LoadData outer catch:', e);
                                } finally {
                                    btn.disabled = false; btn.innerText = 'بارگذاری داده';
                                }
                            }
                            // اگر متد decimated نبود، از لود تکه‌ای وابسته به اسکرول استفاده می‌کنیم
                            else if (PageMethods && typeof PageMethods.GetChartDataChunk === 'function') {
                                btn.disabled = true; btn.innerText = 'در حال بارگذاری...';
                                try {
                                    // آماده‌سازی وضعیت لودر
                                    chunkLoader = {
                                        monitorId, startJ: startJalali, endJ: endJalali,
                                        pageSize: 800, nextPage: 0, total: null, loading: false, done: false,
                                        acc: { Timestamps: [], Temperatures: [], FreezerTemperatures: [], MotorStates: [], PowerStates: [], Element1States: [], Element2States: [], FDCStates: [], FACStates: [], CurrentStates: [], PowerConsumptionStates: [], CurrentValues: [], PowerValues: [] }
                                    };
                                    attachScrollLoader();
                                    // پیش‌بارگذاری تا پر شدن بخش قابل مشاهده
                                    const target = estimateInitialTargetPoints();
                                    let safety = 0;
                                    while (chunkLoader.acc.Timestamps.length < target && !chunkLoader.done && safety++ < 10) {
                                        await loadNextChunk(false);
                                    }
                                    if (!chunkLoader.acc.Timestamps.length) {
                                        showError('داده‌ای در بازه انتخابی یافت نشد.');
                                        return;
                                    }
                                    hideError();
                                    renderFromAcc();
                                    // اگر کاربر نزدیک انتهاست، بارگذاری بعدی را هم شروع کن
                                    if (atRightEdge()) { await loadNextChunk(false); }
                                } catch (e) {
                                    showError('خطا در دریافت/پردازش داده: ' + persianizeErrorMessage(e && e.message ? e.message : e));
                                    console.error(e);
                                } finally {
                                    btn.disabled = false; btn.innerText = 'بارگذاری داده';
                                }
                            } else if (PageMethods && typeof PageMethods.GetChartData === 'function') {
                                // Fallback به متد قبلی در صورت نبود متد chunk
                                btn.disabled = true; btn.innerText = 'در حال بارگذاری...';
                                PageMethods.GetChartData(monitorId, startJalali, endJalali,
                                    function (res) {
                                        try {
                                            const data = (typeof res === 'string') ? JSON.parse(res) : res;
                                            if (!data || !data.Timestamps || !data.Timestamps.length) {
                                                showError('داده‌ای در بازه انتخابی یافت نشد.');
                                                btn.disabled = false; btn.innerText = 'بارگذاری داده';
                                                return;
                                            }
                                            hideError();
                                            if (chartInstance) { try { chartInstance.destroy(); } catch (e) { } chartInstance = null; }
                                            document.getElementById('<%= hdnChartData.ClientID %>').value = JSON.stringify(data);
                                            initializeChart(null, null);
                                            updateEnergyCards();
                                        } catch (e) {
                                            showError('خطا در پردازش داده‌های دریافتی: ' + persianizeErrorMessage(e && e.message ? e.message : e));
                                            console.error(e);
                                        } finally { btn.disabled = false; btn.innerText = 'بارگذاری داده'; }
                                    },
                                    function (err) {
                                        try {
                                            var detail = (err && err.get_message) ? err.get_message() : (err && err.message) ? err.message : (err && err.responseText) ? err.responseText : '';
                                            showError('خطا در دریافت داده از سرور: ' + persianizeErrorMessage(detail));
                                        } catch (ex) { showError('خطا در دریافت داده از سرور.'); }
                                        console.error('GetChartData error:', err);
                                        btn.disabled = false; btn.innerText = 'بارگذاری داده';
                                    }
                                );
                            } else {
                                showError('متد دریافت داده در دسترس نیست.');
                            }
                        });
                    }

                    // ایجاد المان tooltip در ابتدای بارگذاری صفحه
                    const tooltip = document.createElement('div');
                    tooltip.id = 'chartjs-tooltip';
                    tooltip.style.position = 'absolute';
                    tooltip.style.zIndex = '99999';
                    tooltip.style.pointerEvents = 'none';
                    tooltip.style.opacity = '0';
                    tooltip.style.transition = 'opacity 0.1s ease';
                    tooltip.style.backgroundColor = 'rgba(255, 255, 255, 0.20)';
                    tooltip.style.borderRadius = '4px';
                    tooltip.style.padding = '10px';
                    tooltip.style.boxShadow = '0 2px 5px rgba(0,0,0,0.2)';
                    tooltip.style.fontFamily = 'Vazir, Tahoma, Arial, sans-serif';
                    tooltip.style.fontSize = '12px';
                    tooltip.style.color = '#333';
                    tooltip.style.border = '1px solid rgba(0,0,0,0.1)';
                    tooltip.style.direction = 'rtl';
                    tooltip.style.minWidth = '180px'; // افزایش عرض تولتیپ
                    tooltip.style.maxWidth = '250px'; // حداکثر عرض تولتیپ
                    document.body.appendChild(tooltip);

                    // تنظیم حالت با کیفیت پایین برای موبایل
                    if (window.innerWidth < 768) {
                        Chart.defaults.font.size = 8;
                        Chart.defaults.elements.line.borderWidth = 1.5;
                        Chart.defaults.elements.point.radius = 0;
                        Chart.defaults.animation.duration = 0;
                    }

                    // غیرفعال کردن قابلیت زوم در Chart.js
                    // در Chart.js v4 و chartjs-plugin-zoom باید ساختار گزینه‌ها معتبر باشد
                    Chart.defaults.plugins.zoom = {
                        pan: { enabled: false },
                        zoom: {
                            wheel: { enabled: false },
                            pinch: { enabled: false },
                            drag: { enabled: false }
                        }
                    };

                    // از این پس نمودار تنها پس از کلیک روی «بارگذاری داده» ساخته می‌شود
                    setupEventListeners();
                    if (window.isCsvMode) {
                        // حالت آرشیو آفلاین: پر کردن اطلاعات از csvMetaData
                        try { populateFromCsvMetaData(); } catch (e) { console.warn('populateFromCsvMetaData error', e); }
                        // رندر اولیه نمودار با تمام داده‌ها (همین الان)
                        try { initializeChart(null, null); } catch (e) { console.warn('initializeChart csv error', e); }
                    } else {
                        // پس از آماده شدن صفحه، جدول وضعیت و هدر را مستقیم از سرور (DB) واکشی کن
                        try { fetchAndRenderStatusTable(); } catch (e) { console.warn('fetchAndRenderStatusTable init error', e); }
                        try { fetchAndRenderHeader(); } catch (e) { console.warn('fetchAndRenderHeader init error', e); }
                        try { fetchAndRenderConnectionStatus(); } catch (e) { console.warn('fetchAndRenderConnectionStatus init error', e); }
                    }
                } catch (error) {
                    showError("خطا در بارگذاری نمودار: " + error.message);
                    console.error("Chart initialization error:", error);
                }
            });

            // تابع پر کردن هدر از csvMetaData (حالت آفلاین)
            function populateFromCsvMetaData() {
                if (!window.csvMetaData) return;
                const m = window.csvMetaData;
                const customerName = m.CustomerName || '-';
                const customerMobile = m.CustomerMobile || '-';
                const deviceTitle = m.MonitoringDeviceTitle || '-';
                const deviceNumber = m.MonitoringDeviceNumber || '-';
                const nameSpan = document.getElementById('customerNameSpan');
                const mobileSpan = document.getElementById('customerMobileSpan');
                const deviceSpan = document.getElementById('deviceCodeSpan');
                if (nameSpan) nameSpan.textContent = customerName;
                if (mobileSpan) mobileSpan.textContent = customerMobile !== '-' ? customerMobile : '—';
                if (deviceSpan) deviceSpan.textContent = deviceNumber;
                const infoCard = document.getElementById('monitorInfoCard');
                const infoCustomerName = document.getElementById('infoCustomerName');
                const infoCustomerMobile = document.getElementById('infoCustomerMobile');
                const infoDeviceTitle = document.getElementById('infoDeviceTitle');
                const infoWorkshopName = document.getElementById('infoWorkshopName');
                const infoReceiptId = document.getElementById('infoReceiptId');
                const infoCreatedAt = document.getElementById('infoCreatedAt');
                const infoProblemDesc = document.getElementById('infoProblemDesc');
                if (infoCustomerName) infoCustomerName.textContent = customerName;
                if (infoCustomerMobile) infoCustomerMobile.textContent = customerMobile;
                if (infoDeviceTitle) infoDeviceTitle.textContent = deviceTitle + (deviceNumber !== '-' ? ' (' + deviceNumber + ')' : '');
                if (infoWorkshopName) infoWorkshopName.textContent = 'آرشیو آفلاین';
                if (infoReceiptId) infoReceiptId.textContent = String(m.ConnectionId || '-');
                if (infoCreatedAt) infoCreatedAt.textContent = '-';
                if (infoProblemDesc) infoProblemDesc.textContent = '-';
                if (infoCard) infoCard.style.display = '';
                const badge = document.getElementById('connBadge');
                const lastEl = document.getElementById('connLastTs');
                const cntEl = document.getElementById('connRecordCount');
                if (badge) { badge.classList.remove('status-online'); badge.classList.add('status-offline'); badge.textContent = 'آفلاین'; }
                if (lastEl) { lastEl.textContent = 'آخرین: ' + (m.LastTimestampFa || '-'); }
                if (cntEl) { cntEl.textContent = String(m.RecordCount || 0); }
                const startLabel = document.querySelector('label[for="startDateTime"]');
                const endLabel = document.querySelector('label[for="endDateTime"]');
                if (startLabel && m.FirstTimestampFa) {
                    startLabel.innerHTML = 'شروع زمانی فیلتر <span style="color: #007bff; font-weight: bold;">(' + m.FirstTimestampFa + ')</span>';
                }
                if (endLabel && m.LastTimestampFa) {
                    endLabel.innerHTML = 'پایان زمانی فیلتر <span style="color: #dc3545; font-weight: bold;">(' + m.LastTimestampFa + ')</span>';
                }
                const btn = document.getElementById('btnLoadData');
                if (btn) {
                    btn.disabled = false;
                    btn.innerHTML = '<i class="bi bi-arrow-repeat ms-1"></i> نمایش داده';
                }
            }

            let chartInstance = null;
            let dragEventsBound = false; // جلوگیری از ثبت چندباره رویدادهای درگ
            // Helper: دریافت پارامتر از QueryString در اسکوپ سراسری
            function getQueryParamGlobal(name) { try { const url = new URL(window.location.href); const p = new URLSearchParams(url.search); const key = [...p.keys()].find(k => k.toLowerCase() === String(name).toLowerCase()); return key ? p.get(key) : null; } catch (e) { return null; } }
            function getMonitorIdFlexible() {
                const keys = ['Id', 'id', 'Request_Id', 'request_id', 'requestid', 'RequestId'];
                for (let k of keys) { const v = getQueryParamGlobal(k); if (v) return v; }
                return '';
            }
            // Helper to format duration in Persian like HH:MM:SS
            function formatDurationMs(ms) {
                if (!isFinite(ms) || ms < 0) return '-';
                const totalSec = Math.floor(ms / 1000);
                const h = Math.floor(totalSec / 3600);
                const m = Math.floor((totalSec % 3600) / 60);
                const s = totalSec % 60;
                const pad = (n) => String(n).padStart(2, '0');
                if (h > 0) return `${pad(h)}:${pad(m)}:${pad(s)}`;
                return `${pad(m)}:${pad(s)}`;
            }
            // Compute latest status, last start time, and stop duration before the latest start
            function computeLatestStatusInfo(timestamps, states) {
                const n = Array.isArray(states) ? states.length : 0;
                if (!Array.isArray(timestamps) || n === 0 || timestamps.length !== n) return { current: '-', lastStart: '-', stopBeforeStart: '-' };
                const lastIdx = n - 1;
                const currentOn = states[lastIdx] === 1;
                // find latest start index (0->1 transition) near the end, including case already ON from earlier
                let startIdx = -1;
                for (let i = lastIdx; i > 0; i--) {
                    if (states[i] === 1 && states[i - 1] === 0) { startIdx = i; break; }
                }
                if (startIdx === -1 && currentOn) {
                    // never saw 0->1, started from the first sample
                    startIdx = 0;
                }
                let lastStartTs = '-';
                if (startIdx >= 0) {
                    lastStartTs = timestamps[startIdx] || '-';
                }
                // compute stop duration immediately before this start
                let stopMs = null;
                if (startIdx > 0) {
                    // walk backward to find where previous OFF run started
                    let offEnd = startIdx - 1; // this index is OFF (0)
                    let j = offEnd;
                    while (j > 0 && states[j - 1] === 0) j--;
                    // now [j .. offEnd] is the OFF segment; its start time is timestamps[j]
                    const offStartTs = timestamps[j];
                    const startTs = timestamps[startIdx];
                    // تبدیل جلالی به ISO برای ایجاد آبجکت Date معتبر
                    const t1Iso = parseJalaliDateTimeToIso(offStartTs);
                    const t2Iso = parseJalaliDateTimeToIso(startTs);
                    const t1 = new Date(t1Iso);
                    const t2 = new Date(t2Iso);
                    if (!isNaN(t1) && !isNaN(t2) && t2 >= t1) stopMs = (t2 - t1);
                }
                const currentBadge = currentOn ? { text: 'روشن', cls: 'badge-on' } : { text: 'خاموش', cls: 'badge-off' };
                return { current: currentBadge, lastStart: lastStartTs || '-', stopBeforeStart: stopMs != null ? formatDurationMs(stopMs) : '-' };
            }
            function setBadge(el, badge) {
                if (!el) return;
                el.classList.remove('badge-on', 'badge-off');
                el.classList.add('badge-status');
                if (badge && badge.cls) el.classList.add(badge.cls);
                el.textContent = badge && badge.text ? badge.text : '-';
            }
            function updateStatusTableFromData(data) {
                if (!data) return;
                const ts = data.Timestamps || [];
                // Support alternative keys if exist
                const motorStates = data.MotorStates || [];
                const heater1States = data.Heater1States || data.Element1States || [];
                const heater2States = data.Heater2States || data.Element2States || [];
                const motorInfo = computeLatestStatusInfo(ts, motorStates);
                const h1Info = computeLatestStatusInfo(ts, heater1States);
                const h2Info = computeLatestStatusInfo(ts, heater2States);
                setBadge(document.getElementById('statusMotor'), motorInfo.current);
                document.getElementById('lastStartMotor').textContent = motorInfo.lastStart || '-';
                document.getElementById('stopBeforeStartMotor').textContent = motorInfo.stopBeforeStart || '-';
                setBadge(document.getElementById('statusHeater1'), h1Info.current);
                document.getElementById('lastStartHeater1').textContent = h1Info.lastStart || '-';
                document.getElementById('stopBeforeStartHeater1').textContent = h1Info.stopBeforeStart || '-';
                setBadge(document.getElementById('statusHeater2'), h2Info.current);
                document.getElementById('lastStartHeater2').textContent = h2Info.lastStart || '-';
                document.getElementById('stopBeforeStartHeater2').textContent = h2Info.stopBeforeStart || '-';
            }

            // از سرور (DB) واکشی و جدول وضعیت را پر می‌کند
            function fetchAndRenderStatusTable() {
                const monitorId = getMonitorIdFlexible();
                if (!monitorId || !(window.PageMethods) || typeof PageMethods.GetEquipmentStatus !== 'function') return;
                PageMethods.GetEquipmentStatus(monitorId,
                    function (res) {
                        try {
                            const data = (typeof res === 'string') ? JSON.parse(res) : res;
                            if (!data) return;
                            const map = [
                                { key: 'Motor', badgeEl: 'statusMotor', lastStartEl: 'lastStartMotor', stopEl: 'stopBeforeStartMotor' },
                                { key: 'Heater1', badgeEl: 'statusHeater1', lastStartEl: 'lastStartHeater1', stopEl: 'stopBeforeStartHeater1' },
                                { key: 'Heater2', badgeEl: 'statusHeater2', lastStartEl: 'lastStartHeater2', stopEl: 'stopBeforeStartHeater2' }
                            ];
                            map.forEach(m => {
                                const item = data[m.key] || {};
                                const isOn = String(item.CurrentStatus || '').toLowerCase() === 'on';
                                setBadge(document.getElementById(m.badgeEl), isOn ? { text: 'روشن', cls: 'badge-on' } : { text: 'خاموش', cls: 'badge-off' });
                                document.getElementById(m.lastStartEl).textContent = item.LastStart || '-';
                                const sec = Number(item.StopBeforeLastStartSeconds || 0);
                                document.getElementById(m.stopEl).textContent = sec > 0 ? formatDurationMs(sec * 1000) : '-';
                            });
                        } catch (e) { console.warn('Parse GetEquipmentStatus error', e); }
                    },
                    function (err) { console.warn('GetEquipmentStatus error', err); }
                );
            }

            // واکشی و نمایش هدر (مشتری، دستگاه، رسید) از DB
            function fetchAndRenderHeader() {
                const monitorId = getMonitorIdFlexible();
                if (!monitorId || !(window.PageMethods) || typeof PageMethods.GetMonitorHeaderInfo !== 'function') return;
                PageMethods.GetMonitorHeaderInfo(monitorId,
                    function (res) {
                        try {
                            const data = (typeof res === 'string') ? JSON.parse(res) : res;
                            const customerName = (data && data.CustomerName) ? String(data.CustomerName).trim() : '-';
                            const customerMobile = (data && data.CustomerMobile) ? String(data.CustomerMobile).trim() : '-';
                            const deviceTitle = (data && data.DeviceTitle) ? String(data.DeviceTitle).trim() : '-';
                            const deviceNumber = (data && data.DeviceNumber) ? String(data.DeviceNumber).trim() : '-';
                            const workshop = (data && data.WorkshopName) ? String(data.WorkshopName).trim() : '-';
                            const problem = (data && data.ProblemDescription) ? String(data.ProblemDescription).trim() : '-';
                            const receiptId = (data && data.ReceiptId) ? String(data.ReceiptId).trim() : '-';
                            const createdAtFa = (data && data.CreatedAtFa) ? String(data.CreatedAtFa).trim() : '-';

                            // Update main header
                            const nameSpan = document.getElementById('customerNameSpan');
                            const mobileSpan = document.getElementById('customerMobileSpan');
                            const deviceSpan = document.getElementById('deviceCodeSpan');
                            if (nameSpan) nameSpan.textContent = customerName;
                            if (mobileSpan) mobileSpan.textContent = customerMobile !== '-' ? customerMobile : '—';
                            if (deviceSpan) deviceSpan.textContent = deviceNumber;

                            // Update info card
                            const infoCard = document.getElementById('monitorInfoCard');
                            const infoCustomerName = document.getElementById('infoCustomerName');
                            const infoCustomerMobile = document.getElementById('infoCustomerMobile');
                            const infoDeviceTitle = document.getElementById('infoDeviceTitle');
                            const infoWorkshopName = document.getElementById('infoWorkshopName');
                            const infoReceiptId = document.getElementById('infoReceiptId');
                            const infoCreatedAt = document.getElementById('infoCreatedAt');
                            const infoProblemDesc = document.getElementById('infoProblemDesc');
                            if (infoCustomerName) infoCustomerName.textContent = customerName;
                            if (infoCustomerMobile) infoCustomerMobile.textContent = customerMobile;
                            if (infoDeviceTitle) infoDeviceTitle.textContent = deviceTitle + (deviceNumber !== '-' ? ' (' + deviceNumber + ')' : '');
                            if (infoWorkshopName) infoWorkshopName.textContent = workshop;
                            if (infoReceiptId) infoReceiptId.textContent = receiptId;
                            if (infoCreatedAt) infoCreatedAt.textContent = createdAtFa;
                            if (infoProblemDesc) infoProblemDesc.textContent = problem;
                            if (infoCard) infoCard.style.display = '';
                        } catch (e) { console.warn('Parse GetMonitorHeaderInfo error', e); }
                    },
                    function (err) { console.warn('GetMonitorHeaderInfo error', err); }
                );
            }

            function fetchAndRenderConnectionStatus() {
                const monitorId = getMonitorIdFlexible();
                const badge = document.getElementById('connBadge');
                const lastEl = document.getElementById('connLastTs');
                const cntEl = document.getElementById('connRecordCount');
                if (!monitorId) {
                    if (badge) { badge.classList.remove('status-online'); badge.classList.add('status-offline'); badge.textContent = 'آفلاین'; }
                    if (lastEl) { lastEl.textContent = 'آخرین: شناسه یافت نشد'; }
                    if (cntEl) { cntEl.textContent = '-'; }
                    console.warn('fetchAndRenderConnectionStatus: monitorId not found in querystring');
                    return;
                }
                if (!(window.PageMethods) || typeof PageMethods.GetConnectionStatus !== 'function') {
                    console.warn('fetchAndRenderConnectionStatus: PageMethods.GetConnectionStatus not available');
                    return;
                }
                PageMethods.GetConnectionStatus(monitorId,
                    function (res) {
                        try {
                            const data = (typeof res === 'string') ? JSON.parse(res) : res;
                            console.log('ConnectionStatus response for', monitorId, data);
                            const isOnline = !!(data && data.IsOnline);
                            const lastTs = (data && data.LastTimestamp) ? data.LastTimestamp : '-';
                            const firstTs = (data && data.FirstTimestamp) ? data.FirstTimestamp : '-';
                            const cnt = (data && typeof data.RecordCount === 'number') ? data.RecordCount : 0;
                            if (badge) {
                                badge.classList.remove('status-online', 'status-offline');
                                badge.classList.add(isOnline ? 'status-online' : 'status-offline');
                                badge.textContent = isOnline ? 'آنلاین' : 'آفلاین';
                            }
                            if (lastEl) { lastEl.textContent = `آخرین: ${lastTs}`; }
                            if (cntEl) { cntEl.textContent = String(cnt); }
                            
                            // به‌روزرسانی label های فیلدهای تاریخ با اولین و آخرین رکورد
                            if (firstTs !== '-' && lastTs !== '-') {
                                const startLabel = document.querySelector('label[for="startDateTime"]');
                                const endLabel = document.querySelector('label[for="endDateTime"]');
                                if (startLabel) {
                                    startLabel.innerHTML = `شروع زمانی فیلتر <span style="color: #007bff; font-weight: bold;">(${firstTs})</span>`;
                                }
                                if (endLabel) {
                                    endLabel.innerHTML = `پایان زمانی فیلتر <span style="color: #dc3545; font-weight: bold;">(${lastTs})</span>`;
                                }
                                
                                // محدود کردن دیتاپیکر به بازه موجود در لوگ‌ها
                                updateDatePickerRange(firstTs, lastTs);
                                
                                // فعال کردن دکمه بارگذاری پس از لود شدن تاریخ‌ها
                                const btn = document.getElementById('btnLoadData');
                                if (btn) {
                                    btn.disabled = false;
                                    btn.innerHTML = '<i class="bi bi-arrow-repeat ms-1"></i> بارگذاری داده';
                                }
                            }
                        } catch (e) { console.warn('Parse GetConnectionStatus error', e); }
                    },
                    function (err) { console.warn('GetConnectionStatus error', err); }
                );
            }

            function pad2(n) { return String(n).padStart(2, '0'); }
            // ورودی نمونه: "1403/07/28 08:15" یا "1403-07-28 08:15"
            function parseJalaliDateTimeToIso(s) {
                if (!s) return null;
                const parts = s.trim().split(/[T\s]+/);
                const datePart = parts[0];
                const timePart = parts[1] || '00:00';
                const d = datePart.split(/[-\/]/);
                if (d.length < 3) return null;
                const jy = parseInt(d[0], 10), jm = parseInt(d[1], 10), jd = parseInt(d[2], 10);
                const t = timePart.split(':');
                const hh = parseInt(t[0] || '0', 10), mm = parseInt(t[1] || '0', 10), ss = parseInt(t[2] || '0', 10);
                const [gy, gm, gd] = jalaliToGregorian(jy, jm, jd);
                // تولید ISO محلی بدون منطقه زمانی: YYYY-MM-DDTHH:mm:ss
                return `${gy}-${pad2(gm)}-${pad2(gd)}T${pad2(hh)}:${pad2(mm)}:${pad2(ss)}`;
            }

            function initializeChart(startIso, endIso) {
                // Check for CSV load error
                if (window.csvLoadError) {
                    showError(window.csvLoadError);
                    return;
                }
                var rawData = document.getElementById('<%= hdnChartData.ClientID %>').value;
                console.log("Raw JSON data:", rawData); // نمایش داده‌های خام برای بررسی

                if (!rawData || rawData.trim() === '') {
                    showError("داده‌ای برای نمایش وجود ندارد. لطفاً مطمئن شوید که پارامتر Id/منبع داده موجود است.");
                    return;
                }

                try {
                    var chartData = JSON.parse(rawData);
                    // نگه داشتن دیتا به‌صورت سراسری برای عملیات نوت
                    window.__chartData = chartData;
                    // نرمال‌سازی مقادیر عددی برای اطمینان از عدد بودن دماها
                    const toNumber = (v) => {
                        if (v === null || v === undefined) return 0;
                        if (typeof v === 'number' && isFinite(v)) return v;
                        let s = String(v).trim();
                        // تبدیل ارقام فارسی/عربی و جداکننده‌ها
                        const persian = '۰۱۲۳۴۵۶۷۸۹';
                        const arabic = '٠١٢٣٤٥٦٧٨٩';
                        for (let i = 0; i < 10; i++) {
                            s = s.replaceAll(persian[i], String(i));
                            s = s.replaceAll(arabic[i], String(i));
                        }
                        s = s.replace(/٬/g, '');
                        s = s.replace(/٫/g, '.');
                        s = s.replace(/،/g, '.');
                        if (s.includes(',') && s.includes('.')) s = s.replace(/,/g, ''); else s = s.replace(/,/g, '.');
                        const n = parseFloat(s);
                        return isNaN(n) ? 0 : n;
                    };
                    if (chartData && Array.isArray(chartData.Temperatures)) {
                        chartData.Temperatures = chartData.Temperatures.map(toNumber);
                    }
                    if (chartData && Array.isArray(chartData.FreezerTemperatures)) {
                        chartData.FreezerTemperatures = chartData.FreezerTemperatures.map(toNumber);
                    }
                    const t = chartData.Temperatures || [];
                    const f = chartData.FreezerTemperatures || [];
                    const minT = t.length ? Math.min(...t) : null;
                    const maxT = t.length ? Math.max(...t) : null;
                    const minF = f.length ? Math.min(...f) : null;
                    const maxF = f.length ? Math.max(...f) : null;
                    console.log('Temp stats -> len:', t.length, 'min:', minT, 'max:', maxT, 'sample:', t.slice(0, 5));
                    console.log('Freezer stats -> len:', f.length, 'min:', minF, 'max:', maxF, 'sample:', f.slice(0, 5));
                    console.log("Parsed chart data:", chartData); // نمایش داده‌های پارس شده

                    if (!chartData || !chartData.Timestamps || chartData.Timestamps.length === 0) {
                        showError("داده‌ای برای نمایش وجود ندارد یا ساختار داده نامعتبر است.");
                        return;
                    }

                    // در صورت داشتن بازه، داده‌ها را فیلتر کن
                    if (startIso && endIso) {
                        try {
                            const start = new Date(startIso);
                            const end = new Date(endIso);
                            if (isNaN(start) || isNaN(end) || start > end) {
                                showError('بازه زمانی نامعتبر است.');
                                return;
                            }
                            const idx = [];
                            for (let i = 0; i < chartData.Timestamps.length; i++) {
                                const ts = new Date(chartData.Timestamps[i]);
                                if (!isNaN(ts) && ts >= start && ts <= end) idx.push(i);
                            }
                            if (idx.length === 0) {
                                showError('در بازه انتخابی، داده‌ای یافت نشد.');
                                return;
                            }
                            function pick(arr) { return Array.isArray(arr) ? idx.map(i => arr[i]) : arr; }
                            const filtered = {};
                            // فیلدهای آرایه‌ای که باید فیلتر شوند
                            const arrayFields = ['Timestamps', 'Temperatures', 'FreezerTemperatures', 'MotorStates', 'Heater1States', 'Heater2States', 'Element1States', 'Element2States', 'FanACStates', 'FACStates', 'FanDCStates', 'FDCStates', 'PowerStates', 'CurrentStates', 'PowerConsumptionStates', 'CurrentValues', 'PowerValues'];
                            arrayFields.forEach(k => { if (chartData[k] !== undefined) filtered[k] = pick(chartData[k]); });
                            // سایر فیلدها دست‌نخورده می‌مانند
                            chartData = Object.assign({}, chartData, filtered);
                        } catch (fe) {
                            console.warn('Filter error:', fe);
                        }
                    }

                    // تنظیم ابعاد container قبل از رندر نمودار
                    const container = document.querySelector('.chart-container');
                    container.style.height = '80vh';
                    container.style.width = '100%';

                    // تاخیر کوتاه برای اطمینان از اعمال تغییرات CSS
                    setTimeout(() => {
                        // جدول وضعیت و هدر را به‌صورت مستقیم از سرور بروز می‌کنیم (کل دیتابیس)
                        renderTemperatureChart(chartData);
                        // پس از رندر، تمام نوت‌های بازه را جداگانه از سرور می‌گیریم تا حتی در رقیق‌سازی نیز از دست نروند
                        // در حالت CSV نوت‌ها درون داده هستند و نیاز به واکشی سرور نیست
                        try { if (!window.isCsvMode && window.fetchAndOverlayNotes) window.fetchAndOverlayNotes(chartData); } catch (e) { console.warn('fetchAndOverlayNotes error', e); }
                    }, 100);

                } catch (error) {
                    showError("خطا در تجزیه داده‌های JSON: " + error.message);
                    console.error("JSON parse error:", error);
                    console.error("Raw data causing error:", rawData);
                }
            }

            function renderTemperatureChart(data) {
                const canvas = document.getElementById('temperatureChart');
                if (!canvas) {
                    showError("المان canvas با شناسه temperatureChart یافت نشد.");
                    return;
                }

                const ctx = canvas.getContext('2d');
                const container = document.querySelector('.chart-container');

                // محاسبه مدت زمان کارکرد برای هر دوره
                function calculateDurations(timestamps, states) {
                    const durations = [];
                    let startTime = null;
                    let startIndex = null;

                    for (let i = 0; i < states.length; i++) {
                        if (states[i] === 1 && startTime === null) {
                            startTime = new Date(timestamps[i]);
                            startIndex = i;
                        } else if (states[i] === 0 && startTime !== null) {
                            const endTime = new Date(timestamps[i]);
                            const duration = (endTime - startTime) / 1000; // تبدیل به ثانیه

                            durations.push({
                                startTime: startTime,
                                endTime: endTime,
                                duration: duration,
                                startIndex: startIndex,
                                endIndex: i,
                                start: timestamps[startIndex],
                                end: timestamps[i]
                            });
                            startTime = null;
                            startIndex = null;
                        }
                    }

                    // اضافه کردن دوره آخر اگر هنوز روشن است
                    if (startTime !== null) {
                        const endTime = new Date(timestamps[timestamps.length - 1]);
                        const duration = (endTime - startTime) / 1000;
                        durations.push({
                            startTime: startTime,
                            endTime: endTime,
                            duration: duration,
                            startIndex: startIndex,
                            endIndex: timestamps.length - 1,
                            start: timestamps[startIndex],
                            end: timestamps[timestamps.length - 1]
                        });
                    }

                    return durations;
                }

                // محاسبه مدت زمان توقف (targetState=0) برای هر دوره
                function calculateDurationsForState(timestamps, states, targetState) {
                    const durations = [];
                    if (!Array.isArray(states) || !Array.isArray(timestamps)) return durations;
                    let startTime = null;
                    let startIndex = null;
                    for (let i = 0; i < states.length; i++) {
                        const s = states[i];
                        if (s === targetState && startTime === null) {
                            startTime = new Date(timestamps[i]);
                            startIndex = i;
                        } else if (s !== targetState && startTime !== null) {
                            const endTime = new Date(timestamps[i]);
                            const duration = (endTime - startTime) / 1000;
                            durations.push({
                                startTime, endTime, duration,
                                startIndex, endIndex: i,
                                start: timestamps[startIndex], end: timestamps[i]
                            });
                            startTime = null;
                            startIndex = null;
                        }
                    }
                    if (startTime !== null) {
                        const endTime = new Date(timestamps[timestamps.length - 1]);
                        const duration = (endTime - startTime) / 1000;
                        durations.push({
                            startTime, endTime, duration,
                            startIndex, endIndex: timestamps.length - 1,
                            start: timestamps[startIndex], end: timestamps[timestamps.length - 1]
                        });
                    }
                    return durations;
                }

                const motorDurations = calculateDurations(data.Timestamps, data.MotorStates || []);
                const heater1Durations = calculateDurations(data.Timestamps, data.Heater1States || data.Element1States || []);
                const heater2Durations = calculateDurations(data.Timestamps, data.Heater2States || data.Element2States || []);
                const fanACDurations = calculateDurations(data.Timestamps, data.FanACStates || data.FACStates || []);
                const fanDCDurations = calculateDurations(data.Timestamps, data.FanDCStates || data.FDCStates || []);
                const powerDurations = calculateDurations(data.Timestamps, data.PowerStates || []);
                // توقف‌ها (0)
                const motorStops = calculateDurationsForState(data.Timestamps, data.MotorStates || [], 0);
                const heater1Stops = calculateDurationsForState(data.Timestamps, (data.Heater1States || data.Element1States || []), 0);
                const heater2Stops = calculateDurationsForState(data.Timestamps, (data.Heater2States || data.Element2States || []), 0);
                const powerStops = calculateDurationsForState(data.Timestamps, data.PowerStates || [], 0);
                const currentDurations = calculateDurations(data.Timestamps, data.CurrentStates || []);
                const powerConsumptionDurations = calculateDurations(data.Timestamps, data.PowerConsumptionStates || []);

                const isMobile = window.innerWidth < 768;
                // افزایش ضخامت پیش‌فرض خطوط برای تمام دیتاست‌ها
                Chart.defaults.elements.line.borderWidth = isMobile ? 2 : 3;
                // ضخامت قوی‌تر برای سری‌های غیر دمایی
                const strongLineWidth = isMobile ? 3 : 4;

                // گرادیان پس‌زمینه برای دمای یخچال (آبی) و دمای فریزر (قرمز)
                const fridgeGradient = ctx.createLinearGradient(0, 0, 0, canvas.height);
                fridgeGradient.addColorStop(0, 'rgba(53, 162, 235, 0.40)');
                fridgeGradient.addColorStop(1, 'rgba(53, 162, 235, 0.05)');
                const freezerGradient = ctx.createLinearGradient(0, 0, 0, canvas.height);
                freezerGradient.addColorStop(0, 'rgba(255, 0, 0, 0.40)');
                freezerGradient.addColorStop(1, 'rgba(255, 0, 0, 0.05)');

                // محاسبه ماکزیمم برای تنظیم محورهای y2 و y3
                const safeNums = arr => (Array.isArray(arr) ? arr.map(v => (typeof v === 'number' && isFinite(v) ? v : parseFloat(v))).filter(v => isFinite(v)) : []);
                const currentVals = (data.CurrentValues && data.CurrentValues.length) ? safeNums(data.CurrentValues) : [];
                const powerVals = (data.PowerValues && data.PowerValues.length) ? safeNums(data.PowerValues) : [];
                const maxCurrent = currentVals.length ? Math.max(...currentVals) : null;
                const maxPower = powerVals.length ? Math.max(...powerVals) : null;
                // محاسبه مین/ماکس دما برای تنظیم محور y به‌گونه‌ای که 0 را شامل شود
                const tempVals = safeNums((data.Temperatures || []).concat(data.FreezerTemperatures || []));
                const minTempVal = tempVals.length ? Math.min(...tempVals) : -5;
                const maxTempVal = tempVals.length ? Math.max(...tempVals) : 5;
                const suggestedMinY = Math.min(0, minTempVal - 2);
                const suggestedMaxY = Math.max(0, maxTempVal + 2);

                // (قدیمی) — حالا از نسخه‌ی گلوبال استفاده می‌کنیم: window.createNoteAnnotations

                // محاسبه عرض مناسب برای نمودار
                const dataPointCount = data.Timestamps.length;
                const minDistance = isMobile ? 6 : 12; // فاصله افقی بین نقاط برای ایجاد اسکرول مناسب
                const calculatedWidth = Math.max(dataPointCount * minDistance, container.clientWidth);
                const chartWidth = Math.max(calculatedWidth, container.clientWidth);

                // تنظیم عرض رپر و بروز کردن ابعاد بوم
                const wrapper = document.getElementById('temperatureChartWrapper');
                wrapper.style.width = chartWidth + 'px';
                canvas.width = chartWidth;
                canvas.height = container.clientHeight;
                // اگر لایو فعال است یا قبلاً در انتهای نمودار بودیم، بعد از رندر به انتها اسکرول کنیم
                const liveToggleEl = document.getElementById('liveToggle');
                const liveOn = !!(liveToggleEl && liveToggleEl.checked);
                const wasAtEnd = Math.abs((container.scrollLeft + container.clientWidth) - container.scrollWidth) < 8;
                if (liveOn || wasAtEnd) {
                    container.scrollLeft = container.scrollWidth;
                    requestAnimationFrame(() => { container.scrollLeft = container.scrollWidth; });
                } else {
                    // در غیراینصورت به ابتدای نمودار اسکرول نکن
                }

                // فعال‌سازی درگ برای اسکرول افقی روی دسکتاپ
                if (!dragEventsBound) {
                    let isDown = false;
                    let startX = 0;
                    let scrollLeft = 0;

                    const startDrag = (e) => {
                        if (window.__ctrlDragZoom || (e && e.ctrlKey)) return; // disable container drag while zooming
                        isDown = true;
                        container.classList.add('dragging');
                        startX = e.pageX || (e.touches && e.touches[0].pageX) || 0;
                        scrollLeft = container.scrollLeft;
                    };
                    const moveDrag = (e) => {
                        if (!isDown) return;
                        if (window.__ctrlDragZoom) return; // don't scroll when ctrl drag-zoom
                        const x = e.pageX || (e.touches && e.touches[0].pageX) || 0;
                        const walk = (x - startX);
                        container.scrollLeft = scrollLeft - walk;
                    };
                    const endDrag = () => {
                        isDown = false;
                        container.classList.remove('dragging');
                    };

                    // ماوس (دسکتاپ)
                    container.addEventListener('mousedown', startDrag, { passive: true });
                    container.addEventListener('mousemove', moveDrag, { passive: true });
                    container.addEventListener('mouseup', endDrag, { passive: true });
                    container.addEventListener('mouseleave', endDrag, { passive: true });

                    // تاچ (موبایل)
                    container.addEventListener('touchstart', startDrag, { passive: true });
                    container.addEventListener('touchmove', moveDrag, { passive: true });
                    container.addEventListener('touchend', endDrag, { passive: true });

                    dragEventsBound = true;
                }

                // گروه‌بندی برچسب‌های زمان
                const hourlyLabels = groupLabelsByHour(data.Timestamps);

                // بررسی وضعیت چک‌باکس‌ها
                const showMotor = document.getElementById('showMotor').checked;
                const showElement1 = document.getElementById('showElement1').checked;
                const showElement2 = document.getElementById('showElement2').checked;
                const showFDC = document.getElementById('showFDC').checked;
                const showFAC = document.getElementById('showFAC').checked;
                const showPower = document.getElementById('showPower') ? document.getElementById('showPower').checked : false;
                const showCurrent = document.getElementById('showCurrent') ? document.getElementById('showCurrent').checked : false;
                const showPowerConsumption = document.getElementById('showPowerConsumption') ? document.getElementById('showPowerConsumption').checked : false;

                // رنگ‌های ثابت مطابق دیتاست‌ها
                const motorColor = isMobile ? 'rgba(255, 99, 132, 0.6)' : 'rgba(255, 99, 132, 1)';
                // Heater1: align label color with dataset color (purple)
                const heater1Color = isMobile ? 'rgba(153, 102, 255, 0.6)' : 'rgba(153, 102, 255, 1)';
                const heater2Color = isMobile ? 'rgba(75, 192, 192, 0.6)' : 'rgba(75, 192, 192, 1)';
                const fanACColor = isMobile ? 'rgba(153, 102, 255, 0.6)' : 'rgba(153, 102, 255, 1)';
                const fanDCColor = isMobile ? 'rgba(54, 162, 235, 0.6)' : 'rgba(54, 162, 235, 1)';
                // رنگ‌های توقف (کمرنگ)
                const motorStopColor = 'rgba(255, 99, 132, 0.35)';
                const heater1StopColor = 'rgba(153, 102, 255, 0.35)';
                const heater2StopColor = 'rgba(75, 192, 192, 0.35)';
                const powerStopColor = 'rgba(0, 0, 0, 0.30)';

                // تنظیم برچسب‌های کارکرد بر اساس وضعیت چک‌باکس‌ها
                // تابع ایجاد برچسب‌های مدت زمان کارکرد/توقف
                // options: { horizontal?:bool, placeAtStart?:bool, placeAtEnd?:bool, xOffset?:number }
                //  - horizontal=true => نمایش افقی (rotation=0)
                //  - placeAtStart=true => قرارگیری لیبل روی لحظه شروع دوره (به‌جای وسط)، مناسب توقف‌ها
                //  - xOffset => جابجایی افقی چند پیکسل (منفی یعنی قبل از شروع)
                function createDurationLabels(durations, color, yPosition, timestamps, options) {
                    return durations.map((duration, index) => {
                        if (options && options.skipIfEndsAtLast && duration && duration.endIndex === (timestamps.length - 1)) {
                            return null; // عدم نمایش لیبل برای توقفی که تا انتهای چارت ادامه دارد
                        }
                        const totalSeconds = Math.floor(duration.duration);
                        const hours = Math.floor(totalSeconds / 3600);
                        const minutes = Math.floor((totalSeconds % 3600) / 60);
                        const seconds = totalSeconds % 60;

                        const formatNumber = (num) => num.toString().padStart(2, '0');
                        const durationText = `${formatNumber(hours)}:${formatNumber(minutes)}:${formatNumber(seconds)}`;

                        // محاسبه موقعیت عمودی برای هر برچسب
                        // برچسب‌ها از پایین به بالا نمایش داده می‌شوند
                        const yAdjust = 0; // لیبل در مرکز باند نمایش داده شود

                        // استفاده از مقدار برچسب واقعی محور x (timestamp) به جای ایندکس
                        const midIndex = Math.floor((duration.startIndex + duration.endIndex) / 2);
                        const xValueAtMid = timestamps && timestamps[midIndex] ? timestamps[midIndex] : midIndex;
                        const xValueAtStart = timestamps && timestamps[duration.startIndex] ? timestamps[duration.startIndex] : duration.startIndex;
                        const xValueAtEnd = timestamps && timestamps[duration.endIndex] ? timestamps[duration.endIndex] : duration.endIndex;
                        const useStart = options && options.placeAtStart;
                        const useEnd = options && options.placeAtEnd;
                        const horizontal = options && options.horizontal;
                        const xOffset = (options && typeof options.xOffset === 'number') ? options.xOffset : 0;

                        return {
                            type: 'label',
                            xValue: useStart ? xValueAtStart : (useEnd ? xValueAtEnd : xValueAtMid),
                            xScaleID: 'x',
                            yValue: yPosition,
                            yScaleID: 'y1',
                            yAdjust: yAdjust,
                            xAdjust: xOffset,
                            backgroundColor: color,
                            borderColor: color,
                            borderWidth: 1,
                            content: [durationText],
                            font: {
                                size: isMobile ? 10 : 12,
                                weight: 'bold',
                                family: 'Vazir'
                            },
                            color: '#fff',
                            padding: {
                                top: 4,
                                bottom: 4,
                                left: 6,
                                right: 6
                            },
                            borderRadius: 4,
                            rotation: horizontal ? 0 : -90,
                            display: true,
                            textAlign: 'center'
                        };
                    }).filter(Boolean);
                }

                // ایجاد برچسب‌های عمودی برای هر تجهیز
                // مرکز هر باند برای قرارگیری لیبل‌ها
                // باندها: برق 1.0، موتور 0.9، هیتر1 0.8، هیتر2 0.7، فن AC 0.6، فن DC 0.5، جریان-State 0.4، مصرف توان-State 0.3
                const motorLabels = createDurationLabels(motorDurations, motorColor, 0.9, data.Timestamps);
                const heater1Labels = createDurationLabels(heater1Durations, heater1Color, 0.8, data.Timestamps);
                const heater2Labels = createDurationLabels(heater2Durations, heater2Color, 0.7, data.Timestamps);
                const fanACLabels = createDurationLabels(fanACDurations, fanACColor, 0.6, data.Timestamps);
                const fanDCLabels = createDurationLabels(fanDCDurations, fanDCColor, 0.5, data.Timestamps);
                const powerLabelsCenter = 1.0;
                const currentLabelsCenter = 0.4;
                const powerConsumptionLabelsCenter = 0.3;
                const powerLabels = createDurationLabels(powerDurations, 'rgba(0,0,0,0.85)', powerLabelsCenter, data.Timestamps, { horizontal: false });
                // لیبل‌های توقف
                const powerStopLabels = createDurationLabels(powerStops, powerStopColor, powerLabelsCenter, data.Timestamps, { horizontal: true, placeAtEnd: true, xOffset: -12, skipIfEndsAtLast: true });
                const motorStopLabels = createDurationLabels(motorStops, motorStopColor, 0.9, data.Timestamps, { horizontal: true, placeAtEnd: true, xOffset: -12, skipIfEndsAtLast: true });
                const heater1StopLabels = createDurationLabels(heater1Stops, heater1StopColor, 0.8, data.Timestamps, { horizontal: true, placeAtEnd: true, xOffset: -12, skipIfEndsAtLast: true });
                const heater2StopLabels = createDurationLabels(heater2Stops, heater2StopColor, 0.7, data.Timestamps, { horizontal: true, placeAtEnd: true, xOffset: -12, skipIfEndsAtLast: true });

                // ترکیب همه برچسب‌ها
                const allLabels = [
                    ...motorLabels.map((label, i) => ({ ...label, id: `motor-label-${i}`, display: showMotor })),
                    ...motorStopLabels.map((label, i) => ({ ...label, id: `motor-stop-label-${i}`, display: showMotor })),
                    ...heater1Labels.map((label, i) => ({ ...label, id: `heater1-label-${i}`, display: showElement1 })),
                    ...heater1StopLabels.map((label, i) => ({ ...label, id: `heater1-stop-label-${i}`, display: showElement1 })),
                    ...heater2Labels.map((label, i) => ({ ...label, id: `heater2-label-${i}`, display: showElement2 })),
                    ...heater2StopLabels.map((label, i) => ({ ...label, id: `heater2-stop-label-${i}`, display: showElement2 })),
                    ...fanACLabels.map((label, i) => ({ ...label, id: `fanAC-label-${i}`, display: showFAC })),
                    ...fanDCLabels.map((label, i) => ({ ...label, id: `fanDC-label-${i}`, display: showFDC })),
                    ...powerLabels.map((label, i) => ({ ...label, id: `power-label-${i}`, display: showPower })),
                    ...powerStopLabels.map((label, i) => ({ ...label, id: `power-stop-label-${i}`, display: showPower }))
                ];

                const motorAnnotations = motorDurations.map((duration, index) => {
                    const totalSeconds = Math.floor(duration.duration);
                    const hours = Math.floor(totalSeconds / 3600);
                    const minutes = Math.floor((totalSeconds % 3600) / 60);
                    const seconds = totalSeconds % 60;

                    const formatNumber = (num) => num.toString().padStart(2, '0');
                    const durationText = `${formatNumber(hours)}:${formatNumber(minutes)}:${formatNumber(seconds)}`;

                    console.log('Creating motor annotation:', {
                        start: duration.start,
                        end: duration.end,
                        text: durationText,
                        showMotor: showMotor
                    });

                    return {
                        id: `motor-box-${index}`,
                        type: 'box',
                        xMin: duration.start,
                        xMax: duration.end,
                        yMin: 0.85,
                        yMax: 0.95,
                        yScaleID: 'y1',
                        backgroundColor: 'rgba(255, 99, 132, 0.1)',
                        borderColor: 'rgba(255, 99, 132, 0.5)',
                        borderWidth: 1,
                        drawTime: 'beforeDatasetsDraw',
                        display: showMotor,
                        label: {
                            display: false,
                            content: durationText,
                            position: 'center',
                            backgroundColor: 'rgba(255, 99, 132, 0.9)',
                            color: '#fff',
                            font: {
                                size: isMobile ? 10 : 12,
                                weight: 'bold',
                                family: 'Vazir'
                            },
                            rotation: 0,
                            yAdjust: -5,
                            xAdjust: 0,
                            textAlign: 'center',
                            padding: {
                                top: 6,
                                bottom: 6,
                                left: 6,
                                right: 6
                            },
                            borderWidth: 1,
                            borderColor: 'rgba(255, 255, 255, 0.5)',
                            borderRadius: 4,
                            drawTime: 'afterDatasetsDraw',
                            z: 999,
                            enabled: true
                        }
                    };
                });

                const heater1Annotations = heater1Durations.map((duration, index) => {
                    const totalSeconds = Math.floor(duration.duration);
                    const hours = Math.floor(totalSeconds / 3600);
                    const minutes = Math.floor((totalSeconds % 3600) / 60);
                    const seconds = totalSeconds % 60;

                    const formatNumber = (num) => num.toString().padStart(2, '0');
                    const durationText = `${formatNumber(hours)}:${formatNumber(minutes)}:${formatNumber(seconds)}`;

                    return {
                        id: `heater1-box-${index}`,
                        type: 'box',
                        xMin: duration.start,
                        xMax: duration.end,
                        yMin: 0.75,
                        yMax: 0.85,
                        yScaleID: 'y1',
                        backgroundColor: 'rgba(153, 102, 255, 0.1)',
                        borderColor: 'rgba(153, 102, 255, 0.5)',
                        borderWidth: 1,
                        drawTime: 'beforeDatasetsDraw',
                        display: showElement1,
                        label: {
                            display: false,
                            content: durationText,
                            position: 'center',
                            backgroundColor: 'rgba(153, 102, 255, 0.9)',
                            color: '#fff',
                            font: { size: isMobile ? 10 : 12, weight: 'bold', family: 'Vazir' },
                            rotation: 0,
                            yAdjust: -5,
                            xAdjust: 0,
                            textAlign: 'center',
                            padding: { top: 6, bottom: 6, left: 6, right: 6 },
                            borderWidth: 1,
                            borderColor: 'rgba(255,255,255,0.5)',
                            borderRadius: 4,
                            drawTime: 'afterDatasetsDraw'
                        }
                    };
                });

                const heater2Annotations = heater2Durations.map((duration, index) => {
                    const totalSeconds = Math.floor(duration.duration);
                    const hours = Math.floor(totalSeconds / 3600);
                    const minutes = Math.floor((totalSeconds % 3600) / 60);
                    const seconds = totalSeconds % 60;
                    const formatNumber = (num) => num.toString().padStart(2, '0');
                    const durationText = `${formatNumber(hours)}:${formatNumber(minutes)}:${formatNumber(seconds)}`;
                    return {
                        id: `heater2-box-${index}`,
                        type: 'box',
                        xMin: duration.start,
                        xMax: duration.end,
                        yMin: 0.65,
                        yMax: 0.75,
                        yScaleID: 'y1',
                        backgroundColor: 'rgba(75, 192, 192, 0.1)',
                        borderColor: 'rgba(75, 192, 192, 0.5)',
                        borderWidth: 1,
                        drawTime: 'beforeDatasetsDraw',
                        display: showElement2,
                        label: {
                            display: false,
                            content: durationText,
                            position: 'center',
                            backgroundColor: 'rgba(75, 192, 192, 0.9)',
                            color: '#fff',
                            font: { size: isMobile ? 10 : 12, weight: 'bold', family: 'Vazir' },
                            rotation: 0,
                            yAdjust: -25,
                            xAdjust: 0,
                            textAlign: 'center',
                            padding: { top: 6, bottom: 6, left: 6, right: 6 },
                            borderWidth: 1,
                            borderColor: 'rgba(255,255,255,0.5)',
                            borderRadius: 4,
                            drawTime: 'afterDatasetsDraw'
                        }
                    };
                });

                const fanACAnnotations = fanACDurations.map((duration, index) => {
                    const totalSeconds = Math.floor(duration.duration);
                    const hours = Math.floor(totalSeconds / 3600);
                    const minutes = Math.floor((totalSeconds % 3600) / 60);
                    const seconds = totalSeconds % 60;

                    const formatNumber = (num) => num.toString().padStart(2, '0');
                    const durationText = `${formatNumber(hours)}:${formatNumber(minutes)}:${formatNumber(seconds)}`;

                    return {
                        id: `fanAC-box-${index}`,
                        type: 'box',
                        xMin: duration.start,
                        xMax: duration.end,
                        yMin: 0.55,
                        yMax: 0.65,
                        yScaleID: 'y1',
                        backgroundColor: 'rgba(54, 162, 235, 0.1)',
                        borderColor: 'rgba(54, 162, 235, 0.5)',
                        borderWidth: 1,
                        drawTime: 'beforeDatasetsDraw',
                        display: showFAC,
                        label: {
                            display: false,
                            content: durationText,
                            position: 'center',
                            backgroundColor: 'rgba(54, 162, 235, 0.9)',
                            color: '#fff',
                            font: { size: isMobile ? 10 : 12, weight: 'bold', family: 'Vazir' },
                            rotation: 0,
                            yAdjust: -25,
                            xAdjust: 0,
                            textAlign: 'center',
                            padding: { top: 6, bottom: 6, left: 6, right: 6 },
                            borderWidth: 1,
                            borderColor: 'rgba(255,255,255,0.5)',
                            borderRadius: 4,
                            drawTime: 'afterDatasetsDraw'
                        }
                    };
                });

                const fanDCAnnotations = fanDCDurations.map((duration, index) => {
                    const totalSeconds = Math.floor(duration.duration);
                    const hours = Math.floor(totalSeconds / 3600);
                    const minutes = Math.floor((totalSeconds % 3600) / 60);
                    const seconds = totalSeconds % 60;

                    const formatNumber = (num) => num.toString().padStart(2, '0');
                    const durationText = `${formatNumber(hours)}:${formatNumber(minutes)}:${formatNumber(seconds)}`;

                    return {
                        id: `fanDC-box-${index}`,
                        type: 'box',
                        xMin: duration.start,
                        xMax: duration.end,
                        yMin: 0.45,
                        yMax: 0.55,
                        yScaleID: 'y1',
                        backgroundColor: 'rgba(54, 162, 235, 0.1)',
                        borderColor: 'rgba(54, 162, 235, 0.5)',
                        borderWidth: 1,
                        drawTime: 'beforeDatasetsDraw',
                        display: showFDC,
                        label: {
                            display: false,
                            content: durationText,
                            position: 'center',
                            backgroundColor: 'rgba(54, 162, 235, 0.7)',
                            color: '#fff',
                            font: { size: isMobile ? 8 : 10 },
                            yAdjust: -5
                        }
                    };
                });

                // باکس‌های بازه زمانی برای برق دستگاه
                const powerAnnotations = powerDurations.map((duration, index) => {
                    const totalSeconds = Math.floor(duration.duration);
                    const hours = Math.floor(totalSeconds / 3600);
                    const minutes = Math.floor((totalSeconds % 3600) / 60);
                    const seconds = totalSeconds % 60;
                    const formatNumber = (num) => num.toString().padStart(2, '0');
                    const durationText = `${formatNumber(hours)}:${formatNumber(minutes)}:${formatNumber(seconds)}`;
                    return {
                        id: `power-box-${index}`,
                        type: 'box',
                        xMin: duration.start,
                        xMax: duration.end,
                        yMin: 0.95,
                        yMax: 1.05,
                        yScaleID: 'y1',
                        backgroundColor: 'rgba(0, 0, 0, 0.10)',
                        borderColor: 'rgba(0, 0, 0, 0.5)',
                        borderWidth: 1,
                        drawTime: 'beforeDatasetsDraw',
                        z: 100,
                        clip: false,
                        display: showPower,
                        label: {
                            display: false,
                            content: durationText,
                            position: 'center',
                            backgroundColor: 'rgba(0, 0, 0, 0.85)',
                            color: '#fff',
                            font: { size: isMobile ? 10 : 12, weight: 'bold', family: 'Vazir' },
                            rotation: 0,
                            yAdjust: -5,
                            xAdjust: 0,
                            textAlign: 'center',
                            padding: { top: 6, bottom: 6, left: 6, right: 6 },
                            borderWidth: 1,
                            borderColor: 'rgba(255,255,255,0.5)',
                            borderRadius: 4,
                            drawTime: 'afterDatasetsDraw',
                            z: 999,
                            enabled: true
                        }
                    };
                });

                // باکس‌های بازه زمانی برای جریان
                const currentAnnotations = currentDurations.map((duration, index) => {
                    const totalSeconds = Math.floor(duration.duration);
                    const hours = Math.floor(totalSeconds / 3600);
                    const minutes = Math.floor((totalSeconds % 3600) / 60);
                    const seconds = totalSeconds % 60;
                    const formatNumber = (num) => num.toString().padStart(2, '0');
                    const durationText = `${formatNumber(hours)}:${formatNumber(minutes)}:${formatNumber(seconds)}`;
                    return {
                        id: `current-box-${index}`,
                        type: 'box',
                        xMin: duration.start,
                        xMax: duration.end,
                        yMin: 0.35,
                        yMax: 0.45,
                        yScaleID: 'y1',
                        backgroundColor: 'rgba(255, 99, 132, 0.1)',
                        borderColor: 'rgba(255, 99, 132, 0.5)',
                        borderWidth: 1,
                        drawTime: 'beforeDatasetsDraw',
                        display: showCurrent,
                        label: {
                            display: false,
                            content: durationText,
                            position: 'center',
                            backgroundColor: 'rgba(255, 99, 132, 0.9)',
                            color: '#fff',
                            font: { size: isMobile ? 10 : 12, weight: 'bold', family: 'Vazir' },
                            rotation: 0,
                            yAdjust: -25,
                            xAdjust: 0,
                            textAlign: 'center',
                            padding: { top: 6, bottom: 6, left: 6, right: 6 },
                            borderWidth: 1,
                            borderColor: 'rgba(255,255,255,0.5)',
                            borderRadius: 4,
                            drawTime: 'afterDatasetsDraw'
                        }
                    };
                });

                // باکس‌های بازه زمانی برای مصرف توان
                const powerConsumptionAnnotations = powerConsumptionDurations.map((duration, index) => {
                    const totalSeconds = Math.floor(duration.duration);
                    const hours = Math.floor(totalSeconds / 3600);
                    const minutes = Math.floor((totalSeconds % 3600) / 60);
                    const seconds = totalSeconds % 60;
                    const formatNumber = (num) => num.toString().padStart(2, '0');
                    const durationText = `${formatNumber(hours)}:${formatNumber(minutes)}:${formatNumber(seconds)}`;
                    return {
                        id: `powerConsumption-box-${index}`,
                        type: 'box',
                        xMin: duration.start,
                        xMax: duration.end,
                        yMin: 0.25,
                        yMax: 0.35,
                        yScaleID: 'y1',
                        backgroundColor: 'rgba(75, 192, 192, 0.1)',
                        borderColor: 'rgba(75, 192, 192, 0.5)',
                        borderWidth: 1,
                        drawTime: 'beforeDatasetsDraw',
                        display: showPowerConsumption,
                        label: {
                            display: false,
                            content: durationText,
                            position: 'center',
                            backgroundColor: 'rgba(75, 192, 192, 0.9)',
                            color: '#fff',
                            font: { size: isMobile ? 10 : 12, weight: 'bold', family: 'Vazir' },
                            rotation: 0,
                            yAdjust: -25,
                            xAdjust: 0,
                            textAlign: 'center',
                            padding: { top: 6, bottom: 6, left: 6, right: 6 },
                            borderWidth: 1,
                            borderColor: 'rgba(255,255,255,0.5)',
                            borderRadius: 4,
                            drawTime: 'afterDatasetsDraw'
                        }
                    };
                });

                // باندهای جداکننده برای سری‌های صفر/یک تا همپوشانی نداشته باشند
                const toBand = (arr, center) => (Array.isArray(arr) ? arr.map(v => ((v === 1 || v === '1') ? center : 0)) : []);
                const powerBand = toBand(data.PowerStates || [], 1.0);
                const motorBand = toBand(data.MotorStates || [], 0.9);
                const heater1Band = toBand(data.Heater1States || data.Element1States || [], 0.8);
                const heater2Band = toBand(data.Heater2States || data.Element2States || [], 0.7);
                const fanACBand = toBand(data.FanACStates || data.FACStates || [], 0.6);
                const fanDCBand = toBand(data.FanDCStates || data.FDCStates || [], 0.5);
                const currentStateBand = toBand(data.CurrentStates || [], 0.4);
                const powerConsBand = toBand(data.PowerConsumptionStates || [], 0.3);

                try {
                    if (chartInstance) {
                        chartInstance.destroy();
                    }
                    // در صورت وجود نمونه‌ای ثبت نشده روی همین canvas آن را نابود کن
                    const existing = typeof Chart !== 'undefined' ? Chart.getChart(canvas) : null;
                    if (existing) {
                        try { existing.destroy(); } catch (e) { /* ignore */ }
                    }

                    console.log('Motor annotations:', motorAnnotations);

                    chartInstance = new Chart(ctx, {
                        type: 'line',
                        data: {
                            labels: data.Timestamps,
                            datasets: [
                                {
                                    label: 'دمای یخچال',
                                    data: data.Temperatures,
                                    borderColor: isMobile ? 'rgba(53, 162, 235, 0.6)' : 'rgba(53, 162, 235, 1)',

                                    borderWidth: strongLineWidth,
                                    pointRadius: 0,
                                    pointHoverRadius: isMobile ? 3 : 5,
                                    tension: 0,
                                    // پر شدن نسبت به نقطه صفر محور y
                                    fill: { target: { value: 0 } },
                                    backgroundColor: fridgeGradient,
                                    yAxisID: 'y',
                                    order: 1,
                                    hidden: !document.getElementById('showTemperature').checked
                                },
                                {
                                    label: 'دمای فریزر',
                                    data: data.FreezerTemperatures,
                                    borderColor: isMobile ? 'rgba(255, 0, 0, 0.7)' : 'rgba(255, 0, 0, 1)',

                                    borderWidth: isMobile ? 2 : 3,
                                    pointRadius: 0,
                                    pointHoverRadius: isMobile ? 3 : 5,
                                    tension: 0,
                                    // پر شدن نسبت به نقطه صفر محور y
                                    fill: { target: { value: 0 } },
                                    backgroundColor: freezerGradient,
                                    yAxisID: 'y',
                                    order: 2,
                                    hidden: !document.getElementById('showFreezer').checked
                                },
                                {
                                    label: 'برق دستگاه',
                                    data: powerBand,
                                    borderColor: isMobile ? 'rgba(0, 0, 0, 0.7)' : 'rgba(0, 0, 0, 1)',

                                    borderWidth: strongLineWidth,
                                    pointRadius: 0,
                                    pointHoverRadius: isMobile ? 3 : 5,
                                    stepped: true,
                                    fill: false,
                                    yAxisID: 'y1',
                                    order: 3,
                                    hidden: !document.getElementById('showPower').checked
                                },
                                {
                                    label: 'وضعیت موتور',
                                    data: motorBand,
                                    borderColor: isMobile ? 'rgba(255, 99, 132, 0.6)' : 'rgba(255, 99, 132, 1)',

                                    borderWidth: strongLineWidth,
                                    pointRadius: 0,
                                    pointHoverRadius: isMobile ? 3 : 5,
                                    stepped: true,
                                    fill: false,
                                    yAxisID: 'y1',
                                    order: 4,
                                    segment: {
                                        borderColor: ctx => {
                                            const index = ctx.p0DataIndex;
                                            const duration = motorDurations.find(d =>
                                                d.startIndex <= index && d.endIndex >= index
                                            );
                                            return duration ? 'rgba(255, 99, 132, 1)' : 'rgba(255, 99, 132, 0.5)';
                                        }
                                    },
                                    hidden: !showMotor
                                },
                                {
                                    label: 'هیتر 1',
                                    data: heater1Band.length ? heater1Band : Array(data.Timestamps.length).fill(0),
                                    borderColor: isMobile ? 'rgba(153, 102, 255, 0.6)' : 'rgba(153, 102, 255, 1)',

                                    borderWidth: strongLineWidth,
                                    pointRadius: 0,
                                    pointHoverRadius: isMobile ? 3 : 5,
                                    stepped: true,
                                    fill: false,
                                    yAxisID: 'y1',
                                    order: 5,
                                    segment: {
                                        borderColor: ctx => {
                                            const index = ctx.p0DataIndex;
                                            const duration = heater1Durations.find(d =>
                                                d.startIndex <= index && d.endIndex >= index
                                            );
                                            return duration ? 'rgba(153, 102, 255, 1)' : 'rgba(153, 102, 255, 0.5)';
                                        }
                                    },
                                    hidden: !showElement1
                                },
                                {
                                    label: 'هیتر 2',
                                    data: heater2Band.length ? heater2Band : Array(data.Timestamps.length).fill(0),
                                    borderColor: isMobile ? 'rgba(75, 192, 192, 0.6)' : 'rgba(75, 192, 192, 1)',

                                    borderWidth: strongLineWidth,
                                    pointRadius: 0,
                                    pointHoverRadius: isMobile ? 3 : 5,
                                    stepped: true,
                                    fill: false,
                                    yAxisID: 'y1',
                                    order: 6,
                                    segment: {
                                        borderColor: ctx => {
                                            const index = ctx.p0DataIndex;
                                            const duration = heater2Durations.find(d =>
                                                d.startIndex <= index && d.endIndex >= index
                                            );
                                            return duration ? 'rgba(75, 192, 192, 1)' : 'rgba(75, 192, 192, 0.5)';
                                        }
                                    },
                                    hidden: !showElement2
                                },
                                {
                                    label: 'فن دی سی',
                                    data: fanDCBand.length ? fanDCBand : Array(data.Timestamps.length).fill(0),
                                    borderColor: isMobile ? 'rgba(255, 205, 86, 0.6)' : 'rgba(255, 205, 86, 1)',

                                    borderWidth: strongLineWidth,
                                    pointRadius: 0,
                                    pointHoverRadius: isMobile ? 3 : 5,
                                    stepped: true,
                                    fill: false,
                                    yAxisID: 'y1',
                                    order: 7,
                                    segment: {
                                        borderColor: ctx => {
                                            const index = ctx.p0DataIndex;
                                            const duration = fanDCDurations.find(d =>
                                                d.startIndex <= index && d.endIndex >= index
                                            );
                                            return duration ? 'rgba(255, 205, 86, 1)' : 'rgba(255, 205, 86, 0.5)';
                                        }
                                    },
                                    hidden: !showFDC
                                },
                                {
                                    label: 'فن اسی',
                                    data: fanACBand.length ? fanACBand : Array(data.Timestamps.length).fill(0),
                                    borderColor: isMobile ? 'rgba(54, 162, 235, 0.6)' : 'rgba(54, 162, 235, 1)',

                                    borderWidth: strongLineWidth,
                                    pointRadius: 0,
                                    pointHoverRadius: isMobile ? 3 : 5,
                                    stepped: true,
                                    fill: false,
                                    yAxisID: 'y1',
                                    order: 8,
                                    segment: {
                                        borderColor: ctx => {
                                            const index = ctx.p0DataIndex;
                                            const duration = fanACDurations.find(d =>
                                                d.startIndex <= index && d.endIndex >= index
                                            );
                                            return duration ? 'rgba(54, 162, 235, 1)' : 'rgba(54, 162, 235, 0.5)';
                                        }
                                    },
                                    hidden: !showFAC
                                },
                                {
                                    label: 'جریان',
                                    data: (data.CurrentValues && data.CurrentValues.length ? data.CurrentValues : currentStateBand),
                                    borderColor: isMobile ? 'rgba(0, 100, 0, 0.6)' : 'rgba(0, 100, 0, 1)',

                                    borderWidth: strongLineWidth,
                                    pointRadius: (data.CurrentValues && data.CurrentValues.length ? (isMobile ? 0 : 1.5) : 0),
                                    pointHoverRadius: isMobile ? 3 : 5,
                                    stepped: false,
                                    fill: false,
                                    spanGaps: true,
                                    yAxisID: (data.CurrentValues && data.CurrentValues.length ? 'y2' : 'y1'),
                                    order: 9,
                                    hidden: !document.getElementById('showCurrent').checked
                                },
                                {
                                    label: 'توان',
                                    data: (data.PowerValues && data.PowerValues.length ? data.PowerValues : powerConsBand),
                                    borderColor: isMobile ? 'rgba(75, 192, 192, 0.6)' : 'rgba(75, 192, 192, 1)',

                                    borderWidth: strongLineWidth,
                                    pointRadius: (data.PowerValues && data.PowerValues.length ? (isMobile ? 0 : 1.5) : 0),
                                    pointHoverRadius: isMobile ? 3 : 5,
                                    stepped: false,
                                    fill: false,
                                    spanGaps: true,
                                    yAxisID: (data.PowerValues && data.PowerValues.length ? 'y3' : 'y1'),
                                    order: 10,
                                    hidden: !document.getElementById('showPowerConsumption').checked
                                }
                            ]
                        },
                        options: {
                            responsive: true,
                            maintainAspectRatio: false,
                            animation: false,
                            responsiveAnimationDuration: 0,
                            elements: {
                                line: {
                                    tension: 0
                                },
                                point: {
                                    radius: 0,
                                    hitRadius: isMobile ? 20 : 10,
                                    hoverRadius: isMobile ? 4 : 5
                                }
                            },
                            layout: {
                                padding: {
                                    top: 5,
                                    right: 1,
                                    bottom: 5,
                                    left: 1
                                }
                            },
                            interaction: {
                                mode: 'nearest',
                                axis: 'x',
                                intersect: false
                            },
                            plugins: {
                                // Zoom plugin: Ctrl + Drag to zoom on X axis (selection rectangle visible)
                                zoom: {
                                    pan: { enabled: false },
                                    zoom: {
                                        wheel: { enabled: false },
                                        pinch: { enabled: false },
                                        drag: {
                                            enabled: true,
                                            modifierKey: 'ctrl',
                                            backgroundColor: 'rgba(255, 193, 7, 0.35)',
                                            borderColor: 'rgba(255, 193, 7, 0.95)',
                                            borderWidth: 3,
                                            threshold: 2,
                                            drawTime: 'afterDatasetsDraw'
                                        },
                                        mode: 'x',
                                        onZoomComplete: ({ chart }) => {
                                            try {
                                                // جلوگیری از اجرای onZoomComplete پس از دابل کلیک
                                                if (window.__preventZoomComplete) return;

                                                // ذخیره داده‌های اصلی قبل از زوم (فقط یکبار)
                                                if (!window.__originalChartData) {
                                                    const currentData = document.getElementById('<%= hdnChartData.ClientID %>').value;
                                                    if (currentData) {
                                                        window.__originalChartData = currentData;
                                                    }
                                                }
                                                
                                                // نمایش لودینگ جزییات زوم در وسط چارت
                                                showZoomLoading();
                                                // Get visible range indices from x scale
                                                const xScale = chart && chart.scales && chart.scales['x'];
                                                if (!xScale) { hideZoomLoading(); return; }
                                                const data = chart.data || {};
                                                const labels = Array.isArray(data.labels) ? data.labels : [];
                                                if (!labels.length) { hideZoomLoading(); return; }
                                                const i0 = Math.max(0, Math.min(labels.length - 1, Math.ceil(xScale.min || 0)));
                                                const i1 = Math.max(0, Math.min(labels.length - 1, Math.floor(xScale.max || (labels.length - 1))));
                                                if (i1 <= i0) { hideZoomLoading(); return; }
                                                const startJ = labels[i0];
                                                const endJ = labels[i1];
                                                if (!startJ || !endJ) { hideZoomLoading(); return; }
                                                // Prevent spamming requests while dragging
                                                if (window.__zoomFetchInFlight) { hideZoomLoading(); return; }
                                                window.__zoomFetchInFlight = true;
                                                const monitorId = (function(){ try { const u=new URL(window.location.href); return u.searchParams.get('Id')||''; } catch(e){ return ''; } })();
                                                if (window.isCsvMode || !monitorId || !(window.PageMethods) || typeof PageMethods.GetChartDataChunk !== 'function') { window.__zoomFetchInFlight = false; hideZoomLoading(); return; }
                                                // محاسبه تعداد نقاط بیشتر برای زوم بر اساس عرض صفحه و رنج داده
                                                const container = document.querySelector('.chart-container');
                                                const px = (container && container.clientWidth) ? container.clientWidth : 1200;
                                                
                                                // تخمین طول بازه زمانی انتخابی (بر حسب ساعت)
                                                let rangeHours = 24;
                                                try {
                                                    const parseJalali = (s) => {
                                                        const parts = s.split(' ');
                                                        if (parts.length === 2) {
                                                            const [datePart, timePart] = parts;
                                                            const [y, m, d] = datePart.split('/').map(x => parseInt(x, 10));
                                                            const [h, min, sec] = timePart.split(':').map(x => parseInt(x || '0', 10));
                                                            return new Date(y + 621, m - 1, d, h, min, sec || 0);
                                                        }
                                                        return null;
                                                    };
                                                    const start = parseJalali(startJ);
                                                    const end = parseJalali(endJ);
                                                    if (start && end) {
                                                        rangeHours = Math.abs(end - start) / (1000 * 60 * 60);
                                                    }
                                                } catch(_) {}
                                                
                                                // محاسبه تعداد نقاط بر اساس رنج و عرض صفحه (بیشتر از داده اصلی)
                                                let targetPoints, pageSize;
                                                if (rangeHours <= 1) {
                                                    targetPoints = Math.min(8000, Math.floor(px * 3)); // 3 نقطه به ازای هر پیکسل
                                                    pageSize = 1000;
                                                } else if (rangeHours <= 6) {
                                                    targetPoints = Math.min(6000, Math.floor(px * 2.5));
                                                    pageSize = 800;
                                                } else if (rangeHours <= 24) {
                                                    targetPoints = Math.min(4000, Math.floor(px * 2));
                                                    pageSize = 600;
                                                } else if (rangeHours <= 168) { // یک هفته
                                                    targetPoints = Math.min(3000, Math.floor(px * 1.5));
                                                    pageSize = 500;
                                                } else {
                                                    targetPoints = Math.min(2000, Math.floor(px));
                                                    pageSize = 400;
                                                }
                                                targetPoints = Math.max(800, targetPoints); // حداقل 800 نقطه برای زوم
                                                
                                                console.log(`Zoom range: ${rangeHours.toFixed(2)} hours, targetPoints: ${targetPoints}, pageSize: ${pageSize}`);
                                                
                                                // راه‌اندازی chunk loader برای زوم با تنظیمات بیشتر
                                                chunkLoader = {
                                                    monitorId: monitorId,
                                                    startJ: startJ,
                                                    endJ: endJ,
                                                    nextPage: 0,
                                                    pageSize: pageSize,
                                                    done: false,
                                                    loading: false,
                                                    total: null,
                                                    targetPoints: targetPoints,
                                                    acc: {
                                                        Timestamps: [], Temperatures: [], FreezerTemperatures: [], MotorStates: [], PowerStates: [],
                                                        Element1States: [], Element2States: [], FDCStates: [], FACStates: [], CurrentStates: [],
                                                        PowerConsumptionStates: [], CurrentValues: [], PowerValues: [], Notes: [], Ids: []
                                                    }
                                                };
                                                
                                                // بارگذاری چندین تکه برای داده‌های بیشتر در زوم
                                                const loadMultipleChunks = async () => {
                                                    try {
                                                        let totalLoaded = 0;
                                                        let currentPage = 0;
                                                        const maxChunks = Math.ceil(targetPoints / pageSize);
                                                        
                                                        console.log(`Loading up to ${maxChunks} chunks for zoom`);
                                                        
                                                        while (totalLoaded < targetPoints && currentPage < maxChunks) {
                                                            await new Promise((resolve, reject) => {
                                                                PageMethods.GetChartDataChunk(
                                                                    monitorId, startJ, endJ, currentPage, pageSize,
                                                                    function(res) {
                                                                        try {
                                                                            const chunk = (typeof res === 'string') ? JSON.parse(res) : res;
                                                                            if (!chunk || !chunk.Timestamps || !chunk.Timestamps.length) {
                                                                                resolve(); // No more data
                                                                                return;
                                                                            }
                                                                            
                                                                            // ادغام داده‌ها
                                                                            for (let i = 0; i < chunk.Timestamps.length && totalLoaded < targetPoints; i++) {
                                                                                chunkLoader.acc.Timestamps.push(chunk.Timestamps[i]);
                                                                                if (chunk.Temperatures && chunk.Temperatures[i] !== undefined) chunkLoader.acc.Temperatures.push(chunk.Temperatures[i]);
                                                                                if (chunk.FreezerTemperatures && chunk.FreezerTemperatures[i] !== undefined) chunkLoader.acc.FreezerTemperatures.push(chunk.FreezerTemperatures[i]);
                                                                                if (chunk.MotorStates && chunk.MotorStates[i] !== undefined) chunkLoader.acc.MotorStates.push(chunk.MotorStates[i]);
                                                                                if (chunk.PowerStates && chunk.PowerStates[i] !== undefined) chunkLoader.acc.PowerStates.push(chunk.PowerStates[i]);
                                                                                if (chunk.Element1States && chunk.Element1States[i] !== undefined) chunkLoader.acc.Element1States.push(chunk.Element1States[i]);
                                                                                if (chunk.Element2States && chunk.Element2States[i] !== undefined) chunkLoader.acc.Element2States.push(chunk.Element2States[i]);
                                                                                if (chunk.FDCStates && chunk.FDCStates[i] !== undefined) chunkLoader.acc.FDCStates.push(chunk.FDCStates[i]);
                                                                                if (chunk.FACStates && chunk.FACStates[i] !== undefined) chunkLoader.acc.FACStates.push(chunk.FACStates[i]);
                                                                                if (chunk.CurrentStates && chunk.CurrentStates[i] !== undefined) chunkLoader.acc.CurrentStates.push(chunk.CurrentStates[i]);
                                                                                if (chunk.PowerConsumptionStates && chunk.PowerConsumptionStates[i] !== undefined) chunkLoader.acc.PowerConsumptionStates.push(chunk.PowerConsumptionStates[i]);
                                                                                if (chunk.CurrentValues && chunk.CurrentValues[i] !== undefined) chunkLoader.acc.CurrentValues.push(chunk.CurrentValues[i]);
                                                                                if (chunk.PowerValues && chunk.PowerValues[i] !== undefined) chunkLoader.acc.PowerValues.push(chunk.PowerValues[i]);
                                                                                if (chunk.Notes && chunk.Notes[i] !== undefined) chunkLoader.acc.Notes.push(chunk.Notes[i]);
                                                                                if (chunk.Ids && chunk.Ids[i] !== undefined) chunkLoader.acc.Ids.push(chunk.Ids[i]);
                                                                                totalLoaded++;
                                                                            }
                                                                            
                                                                            console.log(`Loaded chunk ${currentPage}, total points: ${totalLoaded}`);
                                                                            resolve();
                                                                        } catch(e) {
                                                                            reject(e);
                                                                        }
                                                                    },
                                                                    function(err) {
                                                                        reject(err);
                                                                    }
                                                                );
                                                            });
                                                            currentPage++;
                                                        }
                                                        
                                                        if (chunkLoader.acc.Timestamps.length > 0) {
                                                            console.log(`Zoom loaded ${chunkLoader.acc.Timestamps.length} points`);
                                                            // آپدیت hidden field با داده‌های زوم
                                                            document.getElementById('<%= hdnChartData.ClientID %>').value = JSON.stringify(chunkLoader.acc);
                                                            // علامت‌گذاری که در حالت زوم هستیم
                                                            window.__isZoomedView = true;
                                                            // رندر مجدد با داده‌های زوم
                                                            initializeChart(null, null);
                                                        } else {
                                                            // fallback: فیلتر داده‌های موجود
                                                            console.log('No server data, trying to filter existing data');
                                                            const currentData = JSON.parse(document.getElementById('<%= hdnChartData.ClientID %>').value || '{}');
                                                            if (currentData && currentData.Timestamps && currentData.Timestamps.length) {
                                                                const filtered = filterDataByRange(currentData, startJ, endJ);
                                                                if (filtered.Timestamps.length > 0) {
                                                                    console.log(`Filtered ${filtered.Timestamps.length} points from existing data`);
                                                                    document.getElementById('<%= hdnChartData.ClientID %>').value = JSON.stringify(filtered);
                                                                    window.__isZoomedView = true;
                                                                    initializeChart(null, null);
                                                                } else {
                                                                    // اگر فیلتر هم نتیجه نداد، فقط زوم visual انجام بده
                                                                    console.log('No filtered data, performing visual zoom only');
                                                                    if (chartInstance && chartInstance.resetZoom) {
                                                                        // محدود کردن نمای چارت به بازه انتخابی
                                                                        const toIso = (s) => { try { return parseJalaliDateTimeToIso(s); } catch(_) { return null; } };
                                                                        const sIso = toIso(startJ);
                                                                        const eIso = toIso(endJ);
                                                                        if (sIso && eIso) {
                                                                            chartInstance.options.scales.x.min = sIso;
                                                                            chartInstance.options.scales.x.max = eIso;
                                                                            chartInstance.update('none');
                                                                        }
                                                                    }
                                                                }
                                                            } else {
                                                                throw new Error('هیچ داده‌ای برای نمایش موجود نیست');
                                                            }
                                                        }
                                                        
                                                        hideZoomLoading();
                                                    } catch(e) {
                                                        console.error('Zoom data error:', e);
                                                        hideZoomLoading();
                                                        showError('خطا در پردازش داده‌های زوم: ' + (e.message || 'خطای نامشخص'));
                                                    } finally {
                                                        window.__zoomFetchInFlight = false;
                                                    }
                                                };
                                                
                                                loadMultipleChunks();
                                            } catch(_) { window.__zoomFetchInFlight = false; hideZoomLoading(); }
                                        }
                                    }
                                },
                                crosshair: {
                                    line: {
                                        color: '#000000',
                                        width: 1,
                                        dashPattern: [5, 5]
                                    },
                                    sync: {
                                        enabled: true
                                    },
                                    zoom: {
                                        enabled: false
                                    }
                                },
                                annotation: {
                            // ensure annotation click events are processed
                            events: ['click','mouseenter','mouseleave'],
                            drawTime: 'afterDatasetsDraw',
                            common: {
                                drawTime: 'afterDatasetsDraw'
                            },
                            annotations: [
                                // vertical hover guideline (replaces crosshair line)
                                {
                                    id: 'hover-line',
                                    type: 'line',
                                    xScaleID: 'x',
                                    xMin: null,
                                    xMax: null,
                                    borderColor: 'rgba(0,0,0,0.7)',
                                    borderWidth: 1,
                                    borderDash: [6,4],
                                    clip: false,
                                    z: 3000,
                                    display: false
                                },
                                {
                                    id: 'zero-line',
                                    type: 'line',
                                    yMin: 0,
                                    yMax: 0,
                                    yScaleID: 'y',
                                    borderColor: 'rgba(0, 0, 0, 0.35)',
                                    borderWidth: isMobile ? 1 : 1.5,
                                    borderDash: [4, 4],
                                    label: {
                                        display: !isMobile,
                                        content: '0°C',
                                        backgroundColor: 'rgba(0,0,0,0.6)',
                                        color: '#fff',
                                        position: 'start',
                                        font: { size: isMobile ? 9 : 11 }
                                    }
                                },
                                ...allLabels,
                                ...motorAnnotations,
                                ...heater1Annotations,
                                ...heater2Annotations,
                                ...fanACAnnotations,
                                ...fanDCAnnotations,
                                ...powerAnnotations,
                                ...currentAnnotations,
                                ...powerConsumptionAnnotations,
                                ...(window.createNoteAnnotations ? window.createNoteAnnotations(window.__chartData || {}) : [])
                            ]
                        },
                                tooltip: {
                                    enabled: false,
                                    external: function(context) {
                                        // استفاده از تولتیپ سفارشی
                                        const tooltipEl = document.getElementById('chartjs-tooltip');
                                        
                                        // مخفی کردن تولتیپ در صورت عدم وجود داده
                                        if (context.tooltip.opacity === 0) {
                                            tooltipEl.style.opacity = 0;
                                            return;
                                        }
                                        
                                        // نمایش تولتیپ
                                        const dataPoints = context.tooltip.dataPoints;
                                        if (dataPoints && dataPoints.length > 0) {
                                            // تاریخ و زمان با فونت بولد و اندازه بزرگتر
                                            let innerHtml = '<div style="font-weight:bold;font-size:14px;margin-bottom:8px;word-wrap:break-word;">' + dataPoints[0].label + '</div>';
                                            
                                            // ایجاد جدول برای نمایش بهتر مقادیر
                                            innerHtml += '<table style="width:100%;border-collapse:collapse;">';
                                            
                                            dataPoints.forEach(function(dataPoint) {
                                                let value = dataPoint.raw;
                                                let valueText = '';
                                                
                                                const ds = dataPoint.dataset;
                                                // سری‌های حالت روشن/خاموش (stepped روی y1)
                                                if (ds && ds.yAxisID === 'y1' && ds.stepped === true) {
                                                    const v = (typeof value === 'number') ? value : parseFloat(value);
                                                    valueText = (v > 0) ? 'روشن' : 'خاموش';
                                                } else if (ds && ds.label === 'جریان') {
                                                    // نمایش جریان بر حسب آمپر
                                                    const v = (typeof value === 'number') ? value : parseFloat(value);
                                                    valueText = (isNaN(v) ? '-' : v.toFixed(2)) + ' A';
                                                } else if (ds && ds.label === 'توان') {
                                                    // نمایش توان بر حسب وات
                                                    const v = (typeof value === 'number') ? value : parseFloat(value);
                                                    valueText = (isNaN(v) ? '-' : v.toFixed(2)) + ' W';
                                                } else {
                                                    // دما بر حسب درجه سانتی‌گراد
                                                    const v = (typeof value === 'number') ? value : parseFloat(value);
                                                    valueText = (isNaN(v) ? '-' : v.toFixed(2)) + ' °C';
                                                }
                                                
                                                const color = dataPoint.dataset.borderColor;
                                                innerHtml += '<tr style="margin-bottom:5px;">' +
                                                    '<td style="padding:4px 0;">' +
                                                    '<div style="display:flex;align-items:center;">' +
                                                    '<div style="width:12px;height:12px;background:' + color + ';margin-left:8px;border-radius:50%;"></div>' +
                                                    '<span style="font-weight:bold;">' + dataPoint.dataset.label + ':</span>' +
                                                    '</div>' +
                                                    '</td>' +
                                                    '<td style="padding:4px 0;text-align:left;direction:ltr;">' + valueText + '</td>' +
                                                    '</tr>';
                                            });
                                            
                                            innerHtml += '</table>';
                                            
                                            tooltipEl.innerHTML = innerHtml;
                                            tooltipEl.style.opacity = 1;
                                            
                                            // تنظیم موقعیت تولتیپ
                                            const position = context.chart.canvas.getBoundingClientRect();
                                            const positionX = position.left + window.pageXOffset + context.tooltip.caretX;
                                            const positionY = position.top + window.pageYOffset + context.tooltip.caretY;
                                            
                                            // تنظیم موقعیت با توجه به اندازه صفحه
                                            if (window.innerWidth < 768) {
                                                tooltipEl.style.left = Math.min(Math.max(positionX, 90), window.innerWidth - 90) + 'px';
                                            } else {
                                                tooltipEl.style.left = positionX + 'px';
                                            }
                                            
                                            tooltipEl.style.top = positionY + 'px';
                                            tooltipEl.style.transform = 'translate(-50%, -100%)';
                                        }
                                    }
                                }
                            },
                            scales: {
                                x: {
                                    type: 'category',
                                    display: true,
                                    grid: {
                                        display: false
                                    },
                                    ticks: {
                                        maxRotation: 45,
                                        minRotation: 45,
                                        font: {
                                            size: isMobile ? 10 : 12,
                                            family: 'Vazir'
                                        },
                                        autoSkip: true,
                                        maxTicksLimit: isMobile ? 8 : 15,
                                        callback: function(value, index, values) {
                                            if (hourlyLabels.includes(index)) {
                                                const timestamp = this.getLabelForValue(value);
                                                if (!timestamp) return '';
                                                
                                                const parts = timestamp.split(' ');
                                                if (parts.length !== 2) return '';
                                                
                                                const timePart = parts[1];
                                                const timeComponents = timePart.split(':');
                                                if (timeComponents.length !== 3) return '';
                                                
                                                return timeComponents[0] + ':' + timeComponents[1];
                                            }
                                            return '';
                                        }
                                    }
                                },
                                y: {
                                    type: 'linear',
                                    display: true,
                                    position: 'left',
                                    title: {
                                        display: !isMobile,
                                        text: 'دما (°C)',
                                        font: {
                                            size: isMobile ? 12 : 14
                                        }
                                    },
                                    grid: {
                                        color: 'rgba(0, 0, 0, 0.05)'
                                    },
                                    ticks: {
                                        font: {
                                            size: isMobile ? 10 : 12
                                        },
                                        maxTicksLimit: isMobile ? 5 : 8
                                    },
                                    suggestedMin: suggestedMinY,
                                    suggestedMax: suggestedMaxY
                                },
                                y1: {
                                    type: 'linear',
                                    display: true,
                                    position: 'right',
                                    min: -0.1,
                                    max: 1.1,
                                    title: {
                                        display: !isMobile,
                                        text: 'وضعیت موتور',
                                        font: {
                                            size: isMobile ? 12 : 14
                                        }
                                    },
                                    grid: {
                                        drawOnChartArea: false
                                    },
                                    ticks: {
                                        callback: function(value) {
                                            return value === 0 ? 'خاموش' : value === 1 ? 'روشن' : '';
                                        },
                                        font: {
                                            size: isMobile ? 10 : 12
                                        }
                                    }
                                },
                                y2: {
                                    type: 'linear',
                                    display: true,
                                    position: 'right',
                                    grid: {
                                        drawOnChartArea: false
                                    },
                                    title: {
                                        display: !isMobile,
                                        text: 'جریان (A)'
                                    },
                                    ticks: {
                                        font: {
                                            size: isMobile ? 10 : 12
                                        }
                                    },
                                    // فاصله برای تفکیک از y1
                                    offset: true,
                                    min: 0,
                                    suggestedMax: (maxCurrent && isFinite(maxCurrent) ? (maxCurrent === 0 ? 1 : maxCurrent * 1.15) : undefined)
                                },
                                y3: {
                                    type: 'linear',
                                    display: true,
                                    position: 'left',
                                    grid: {
                                        drawOnChartArea: false
                                    },
                                    title: {
                                        display: !isMobile,
                                        text: 'توان (W)'
                                    },
                                    ticks: {
                                        font: {
                                            size: isMobile ? 10 : 12
                                        }
                                    },
                                    offset: true,
                                    min: 0,
                                    suggestedMax: (maxPower && isFinite(maxPower) ? (maxPower === 0 ? 1 : maxPower * 1.15) : undefined)
                                }
                            }
                        }
                    });

                    // ذخیره نمونه نمودار به صورت سراسری برای استفاده در به‌روزرسانی پس از ذخیره نوت
                    try { window.chartInstance = chartInstance; } catch(e){}

                    // --- Ctrl + Drag to Zoom (Chart.js zoom plugin) ---
                    try {
                        const toggleDragZoom = (enable) => {
                            try {
                                if (!chartInstance || !chartInstance.options || !chartInstance.options.plugins || !chartInstance.options.plugins.zoom) return;
                                const z = chartInstance.options.plugins.zoom;
                                if (z.zoom && z.zoom.drag) {
                                    z.zoom.drag.enabled = !!enable;
                                    chartInstance.update('none');
                                    // visual cursor hint
                                    canvas.style.cursor = enable ? 'crosshair' : '';
                                }
                            } catch(_){}
                        };
                        let dragArmed = false;
                        const onKeyDown = (e) => {
                            if (e.key === 'Control' && !dragArmed) { dragArmed = true; toggleDragZoom(true); window.__ctrlDragZoom = true; }
                        };
                        const onKeyUp = (e) => {
                            if (e.key === 'Control') { dragArmed = false; toggleDragZoom(false); window.__ctrlDragZoom = false; }
                        };
                        const onWindowBlur = () => { dragArmed = false; toggleDragZoom(false); window.__ctrlDragZoom = false; };
                        // bind once per page
                        if (!window.__zoomKeyBindingsApplied) {
                            window.addEventListener('keydown', onKeyDown);
                            window.addEventListener('keyup', onKeyUp);
                            window.addEventListener('blur', onWindowBlur);
                            window.__zoomKeyBindingsApplied = true;
                        }
                        // Double-click to reset zoom and restore original data
                        const onDblClick = (e) => {
                            try {
                                // اگر در حالت زوم هستیم، داده‌های اصلی را بازگردانی کن
                                if (window.__isZoomedView && window.__originalChartData) {
                                    // بازگردانی داده‌های اصلی
                                    document.getElementById('<%= hdnChartData.ClientID %>').value = window.__originalChartData;
                                    // پاک کردن علامت زوم و داده‌های زوم
                                    window.__isZoomedView = false;
                                    delete window.__originalChartData; // حذف داده‌های ذخیره شده زوم
                                    // جلوگیری از اجرای مجدد onZoomComplete
                                    window.__preventZoomComplete = true;
                                    // رندر مجدد با داده‌های اصلی (بدون فیلتر)
                                    initializeChart(null, null);
                                    // بازگردانی امکان زوم پس از رندر
                                    setTimeout(() => { window.__preventZoomComplete = false; }, 100);
                                } else {
                                    // رفتار عادی ریست زوم
                                    if (chartInstance && chartInstance.resetZoom) {
                                        chartInstance.resetZoom();
                                    } else {
                                        // Fallback: update with no animation to recompute scales
                                        chartInstance.update();
                                    }
                                }
                            } catch(_){}
                        };
                        canvas.addEventListener('dblclick', onDblClick);

                        // Temporarily hide annotations while Ctrl-dragging so the selection rectangle is visible
                        const setAnnotationsVisible = (visible) => {
                            try{
                                if (!chartInstance || !chartInstance.options || !chartInstance.options.plugins || !chartInstance.options.plugins.annotation) return;
                                const anns = chartInstance.options.plugins.annotation.annotations || [];
                                for (let i=0;i<anns.length;i++){
                                    const a = anns[i];
                                    if (!a) continue;
                                    if (a.id === 'hover-line') continue; // keep hover-line logic intact
                                    if (visible){
                                        if (a.__hiddenByZoom){ a.display = a.__prevDisplay !== undefined ? a.__prevDisplay : true; a.__hiddenByZoom = false; }
                                    } else {
                                        a.__prevDisplay = a.display !== undefined ? a.display : true;
                                        a.display = false;
                                        a.__hiddenByZoom = true;
                                    }
                                }
                                chartInstance.update('none');
                            }catch(_){}
                        };
                        let isDraggingZoom = false;
                        canvas.addEventListener('mousedown', (e)=>{
                            if (e && (e.ctrlKey || window.__ctrlDragZoom)){
                                isDraggingZoom = true;
                                setAnnotationsVisible(false);
                            }
                        });
                        const endZoomDrag = ()=>{
                            if (isDraggingZoom){ isDraggingZoom = false; setAnnotationsVisible(true); }
                        };
                        canvas.addEventListener('mouseup', endZoomDrag);
                        canvas.addEventListener('mouseleave', endZoomDrag);
                        window.addEventListener('keyup', (e)=>{ if (e.key === 'Control') endZoomDrag(); });

                        // Note: we do NOT intercept canvas events here so the zoom plugin can handle Ctrl+Drag.
                    } catch(_){}

                    // راست‌کلیک برای درج/ویرایش/حذف توضیحات روی نزدیک‌ترین نقطه
                    try {
                        const monitorIdForNotes = getMonitorIdFlexible();
                        canvas.addEventListener('contextmenu', function(evt){
                            try {
                                evt.preventDefault();
                                if (!chartInstance) return;
                                // ابتدا تلاش برای یافتن نزدیک‌ترین نوت بر اساس مختصات x
                                const hit = (window.findNearestNoteByClick ? window.findNearestNoteByClick(evt, chartInstance) : null);
                                if (hit) {
                                    openNoteModal({ monitorId: monitorIdForNotes, index: hit.index, id: hit.id, ts: hit.ts, note: hit.note });
                                    return;
                                }
                                // در غیر اینصورت fallback نزدیک‌ترین نقطه دیتاست
                                const elements = chartInstance.getElementsAtEventForMode(evt, 'nearest', { intersect: false, axis: 'x' }, true);
                                if (!elements || !elements.length) return;
                                const el = elements[0];
                                const index = el.index;
                                const ts = (window.__chartData && window.__chartData.Timestamps) ? window.__chartData.Timestamps[index] : '';
                                const note = (window.__chartData && window.__chartData.Notes) ? (window.__chartData.Notes[index] || '') : '';
                                openNoteModal({ monitorId: monitorIdForNotes, index, ts, note });
                            } catch(e) { console.warn('contextmenu error', e); }
                        });
                        // کلیک چپ به‌عنوان fallback برای باز کردن مودال نوت
                        canvas.addEventListener('click', function(evt){
                            try{
                                if (!chartInstance) return;
                                const hit = findNearestNoteByClick(evt, chartInstance);
                                if (hit) {
                                    const monitorId = (function(){ try { const u=new URL(window.location.href); return u.searchParams.get('Id')||''; } catch(e){ return ''; } })();
                                    openNoteModal({ monitorId, index: hit.index, id: hit.id, ts: hit.ts, note: hit.note });
                                }
                            }catch(e){ console.warn('click note fallback error', e); }
                        });
                        // نمایش خط عمودی راهنما هنگام حرکت ماوس
                        const updateHoverLine = (evt) => {
                            try{
                                // هنگام Ctrl+Drag برای زوم، خط راهنما را به‌روزرسانی نکن تا با کادر انتخاب تداخل نداشته باشد
                                if ((evt && evt.ctrlKey) || window.__ctrlDragZoom) return;
                                if (!chartInstance || !chartInstance.options || !chartInstance.options.plugins || !chartInstance.options.plugins.annotation) return;
                                const anns = chartInstance.options.plugins.annotation.annotations || [];
                                const hoverAnn = (Array.isArray(anns) ? anns.find(a => a && a.id === 'hover-line') : null);
                                if (!hoverAnn) return;
                                const xScale = chartInstance.scales && chartInstance.scales['x'];
                                if (!xScale) return;
                                const pos = Chart.helpers.getRelativePosition(evt, chartInstance);
                                const idxFloat = xScale.getValueForPixel ? xScale.getValueForPixel(pos.x) : null;
                                const labels = (chartInstance.data && chartInstance.data.labels) ? chartInstance.data.labels : [];
                                if (idxFloat == null || !labels.length) return;
                                // snap to nearest index within bounds
                                let idx = Math.round(idxFloat);
                                if (idx < 0) idx = 0; if (idx >= labels.length) idx = labels.length - 1;
                                const xLabel = labels[idx];
                                if (xLabel !== undefined){
                                    hoverAnn.xMin = xLabel;
                                    hoverAnn.xMax = xLabel;
                                    hoverAnn.display = true;
                                    chartInstance.update('none');
                                }
                            }catch(e){ /* ignore */ }
                        };
                        const hideHoverLine = () => {
                            try{
                                if (!chartInstance || !chartInstance.options || !chartInstance.options.plugins || !chartInstance.options.plugins.annotation) return;
                                const anns = chartInstance.options.plugins.annotation.annotations || [];
                                const hoverAnn = (Array.isArray(anns) ? anns.find(a => a && a.id === 'hover-line') : null);
                                if (hoverAnn){ hoverAnn.display = false; chartInstance.update('none'); }
                            }catch(e){ /* ignore */ }
                        };
                        canvas.addEventListener('mousemove', updateHoverLine);
                        canvas.addEventListener('mouseleave', hideHoverLine);
                    } catch(e) { console.warn('bind contextmenu error', e); }

                    // تنظیم برچسب‌ها برای موبایل بعد از ایجاد نمودار
                    if (window.innerWidth < 768 && chartInstance && chartInstance.options && 
                        chartInstance.options.plugins && chartInstance.options.plugins.annotation) {
                        const annotations = chartInstance.options.plugins.annotation.annotations;
                        if (annotations) {
                            for (let i = 0; i < annotations.length; i++) {
                                if (annotations[i] && annotations[i].label) {
                                    annotations[i].label.font = {
                                        size: 10
                                    };
                                    annotations[i].label.yAdjust = 20;
                                    annotations[i].label.padding = {
                                        top: 2,
                                        bottom: 2,
                                        left: 4,
                                        right: 4
                                    };
                                }
                            }
                            chartInstance.update();
                        }
                    }

                    // اضافه کردن event listener برای بررسی وضعیت برچسب‌ها
                    // chartInstance.options.plugins.annotation.annotations.forEach((annotation, index) => {
                    //     console.log(`Annotation ${index}:`, {
                    //         display: annotation.display,
                    //         labelDisplay: annotation.label?.display,
                    //         type: annotation.type,
                    //         xMin: annotation.xMin,
                    //         xMax: annotation.xMax
                    //     });
                    // });

                } catch (error) {
                    console.error('Error rendering chart:', error);
                    showError('خطا در نمایش نمودار: ' + error.message);
                }
            }
            
            function setupEventListeners() {
                // تنظیم نمایش اولیه برچسب‌ها بر اساس وضعیت چک‌باکس‌ها
                updateLabelsVisibility();
                
                const chartContainer = document.querySelector('.chart-container');
                
                // تابع تغییر وضعیت نمایش دیتاست و برچسب‌های کارکرد
                function toggleDataset(index, visible) {
                    if (chartInstance && chartInstance.data && chartInstance.data.datasets && chartInstance.data.datasets[index]) {
                        // تغییر وضعیت نمایش دیتاست
                        chartInstance.data.datasets[index].hidden = !visible;
                        
                        // به‌روزرسانی نمایش برچسب‌ها
                        updateLabelsVisibility();
                        
                        chartInstance.update();
                    }
                }
                
                function updateLabelsVisibility() {
                    if (!chartInstance || !chartInstance.options || !chartInstance.options.plugins || !chartInstance.options.plugins.annotation) {
                        return;
                    }
                    
                    const annotations = chartInstance.options.plugins.annotation.annotations;
                    if (!annotations) return;
                    
                    // بررسی وضعیت چک‌باکس‌ها و تنظیم نمایش برچسب‌ها
                    const showMotor = document.getElementById('showMotor').checked;
                    const showElement1 = document.getElementById('showElement1').checked;
                    const showElement2 = document.getElementById('showElement2').checked;
                    const showFAC = document.getElementById('showFAC').checked;
                    const showFDC = document.getElementById('showFDC').checked;
                    const showPower = document.getElementById('showPower').checked;
                    
                    for (let i = 0; i < annotations.length; i++) {
                        const annotation = annotations[i];
                        // console.log(`Annotation ${i}:`, annotation); // disabled for cleaner logs
                        
                        if (annotation.id && annotation.id.startsWith('motor-')) {
                            annotation.display = showMotor;
                        } else if (annotation.id && annotation.id.startsWith('heater1-')) {
                            annotation.display = showElement1;
                        } else if (annotation.id && annotation.id.startsWith('heater2-')) {
                            annotation.display = showElement2;
                        } else if (annotation.id && annotation.id.startsWith('fanAC-')) {
                            annotation.display = showFAC;
                        } else if (annotation.id && annotation.id.startsWith('fanDC-')) {
                            annotation.display = showFDC;
                        } else if (annotation.id && annotation.id.startsWith('power-')) {
                            annotation.display = showPower;
                        }
                    }
                    
                    chartInstance.update();
                }
                
                // رویدادهای تغییر وضعیت چک‌باکس‌ها
                document.getElementById('showTemperature').addEventListener('change', function() {
                    toggleDataset(0, this.checked);
                });
                
                document.getElementById('showFreezer').addEventListener('change', function() {
                    toggleDataset(1, this.checked);
                });
                
                document.getElementById('showMotor').addEventListener('change', function() {
                    toggleDataset(3, this.checked);
                    updateLabelsVisibility();
                });
                
                document.getElementById('showPower').addEventListener('change', function() {
                    toggleDataset(2, this.checked);
                    updateLabelsVisibility();
                });
                
                document.getElementById('showElement1').addEventListener('change', function() {
                    toggleDataset(4, this.checked);
                    updateLabelsVisibility();
                });
                
                document.getElementById('showElement2').addEventListener('change', function() {
                    toggleDataset(5, this.checked);
                    updateLabelsVisibility();
                });
                
                document.getElementById('showFDC').addEventListener('change', function() {
                    toggleDataset(6, this.checked);
                    updateLabelsVisibility();
                });
                
                document.getElementById('showFAC').addEventListener('change', function() {
                    toggleDataset(7, this.checked);
                    updateLabelsVisibility();
                });
                
                document.getElementById('showCurrent').addEventListener('change', function() {
                    toggleDataset(8, this.checked);
                });
                
                document.getElementById('showPowerConsumption').addEventListener('change', function() {
                    toggleDataset(9, this.checked);
                });
                
                // عدم کلون/تعویض کانتینر؛ از همان عنصر موجود استفاده می‌کنیم
                const container = chartContainer;
                let startX, startScrollLeft;
                let isDragging = false;
                let lastDeltaX = 0;
                let velocity = 0;
                let lastTimestamp = 0;
                let animationFrameId = null;
                
                function applyMomentumScroll() {
                    if (Math.abs(velocity) > 0.5) {
                        container.scrollLeft += velocity;
                        velocity *= 0.92; // کاهش تدریجی سرعت
                        animationFrameId = requestAnimationFrame(applyMomentumScroll);
                    }
                }

                function handleTouchStart(e) {
                    isDragging = true;
                    startX = e.touches[0].pageX;
                    startScrollLeft = container.scrollLeft;
                    lastTimestamp = Date.now();
                    velocity = 0;
                    lastDeltaX = 0;
                    
                    if (animationFrameId) {
                        cancelAnimationFrame(animationFrameId);
                    }
                }

                function handleTouchMove(e) {
                    if (!isDragging) return;
                    
                    const currentX = e.touches[0].pageX;
                    const deltaX = startX - currentX;
                    const currentTimestamp = Date.now();
                    const timeElapsed = currentTimestamp - lastTimestamp;
                    
                    if (timeElapsed > 0) {
                        velocity = (deltaX - lastDeltaX) / timeElapsed * 10; // تنظیم ضریب سرعت
                    }
                    
                    container.scrollLeft = startScrollLeft + deltaX;
                    lastDeltaX = deltaX;
                    lastTimestamp = currentTimestamp;
                    
                    e.preventDefault(); // جلوگیری از اسکرول صفحه
                }

                function handleTouchEnd() {
                    if (!isDragging) return;
                    isDragging = false;
                    
                    // محدود کردن سرعت حداکثر
                    velocity = Math.max(-15, Math.min(15, velocity));
                    
                    // شروع انیمیشن مومنتوم فقط اگر سرعت کافی وجود داشته باشد
                    if (Math.abs(velocity) > 0.5) {
                        requestAnimationFrame(applyMomentumScroll);
                    }
                }

                // اضافه کردن event listenerهای جدید
                if ('ontouchstart' in window) {
                    container.addEventListener('touchstart', handleTouchStart, { passive: false });
                    container.addEventListener('touchmove', handleTouchMove, { passive: false });
                    container.addEventListener('touchend', handleTouchEnd);
                }

                // --- Live Update ---
                const LIVE_INTERVAL_MS = 10000; // 10s
                let liveCountdownTimerId = null; // 1s ticker
                let liveCountdownSec = 10;
                let liveIsFetching = false;
                function toPersianDigits(num){
                    try{
                        const fa = ['۰','۱','۲','۳','۴','۵','۶','۷','۸','۹'];
                        return String(num).replace(/[0-9]/g, d => fa[d]);
                    }catch(_){ return String(num); }
                }
                function setLiveLabel(text){
                    const el = document.getElementById('liveCountdownText');
                    if (el) el.textContent = text || '';
                }

                function getMonitorId() {
                    try { const url = new URL(window.location.href); return url.searchParams.get('Id') || ''; } catch(e){ return ''; }
                }

                function fetchLatestAndRender(done) {
                    if (window.isCsvMode) { console.log('Live mode disabled in CSV archive mode'); return; }
                    const monitorId = getMonitorId();
                    if (!monitorId || !(window.PageMethods) || typeof PageMethods.GetLatestChartData !== 'function') return;
                    PageMethods.GetLatestChartData(monitorId, 1000,
                        function(res){
                            try{
                                const data = (typeof res === 'string') ? JSON.parse(res) : res;
                                if (!data || !data.Timestamps || !data.Timestamps.length) return;
                                document.getElementById('<%= hdnChartData.ClientID %>').value = JSON.stringify(data);
                                // ایجاد/نوسازی نمودار (از منطق موجود استفاده می‌کنیم)
                                initializeChart(null, null);
                                // به‌روزرسانی کارت‌های انرژی
                                updateEnergyCards();
                                // بروز کردن وضعیت اتصال و جدول تجهیزات همزمان با لایو
                                try { fetchAndRenderConnectionStatus(); } catch (e) { console.warn('Live connection status refresh error', e); }
                                try { fetchAndRenderStatusTable(); } catch (e) { console.warn('Live equipment status refresh error', e); }
                                // در صورت وجود، نوت‌ها را نیز روی نمودار اعمال کن
                                try { if (window.fetchAndOverlayNotes) window.fetchAndOverlayNotes(data); } catch (e) { console.warn('Live notes overlay error', e); }
                                if (typeof done === 'function') { try { done(); } catch (_) { } }
                            } catch (err) { console.warn('Live parse error', err); }
                        },
                        function (err) { console.warn('Live fetch error', err); if (typeof done === 'function') { try { done(); } catch (_) { } } }
                    );
                }

                function startLive() {
                    if (window.isCsvMode) { console.log('Live mode disabled in CSV archive mode'); return; }
                    if (liveCountdownTimerId) return;
                    liveIsFetching = true;
                    setLiveLabel('در حال دریافت...');
                    // Initial fetch then start countdown
                    fetchLatestAndRender(function () {
                        liveIsFetching = false;
                        liveCountdownSec = 10;
                        setLiveLabel(toPersianDigits(liveCountdownSec) + ' ثانیه تا به‌روزرسانی');
                        liveCountdownTimerId = setInterval(function () {
                            if (liveIsFetching) return;
                            if (liveCountdownSec > 0) {
                                liveCountdownSec--;
                                if (liveCountdownSec > 0) {
                                    setLiveLabel(toPersianDigits(liveCountdownSec) + ' ثانیه تا به‌روزرسانی');
                                } else {
                                    // time to fetch
                                    liveIsFetching = true;
                                    setLiveLabel('در حال دریافت...');
                                    fetchLatestAndRender(function () {
                                        liveIsFetching = false;
                                        liveCountdownSec = 10;
                                        setLiveLabel(toPersianDigits(liveCountdownSec) + ' ثانیه تا به‌روزرسانی');
                                    });
                                }
                            }
                        }, 1000);
                    });
                }
                function stopLive() {
                    if (liveCountdownTimerId) { clearInterval(liveCountdownTimerId); liveCountdownTimerId = null; }
                    liveIsFetching = false;
                    setLiveLabel('هر ۱۰ ثانیه');
                }

                const liveToggle = document.getElementById('liveToggle');
                if (liveToggle) {
                    liveToggle.addEventListener('change', function () {
                        const card = document.getElementById('manualControlsCard');
                        const btn = document.getElementById('btnLoadData');
                        const startInput = document.getElementById('startDateTime');
                        const endInput = document.getElementById('endDateTime');
                        if (this.checked) {
                            // Disable manual controls visually and functionally
                            if (card) card.classList.add('disabled-card');
                            if (btn) btn.disabled = true;
                            if (startInput) startInput.disabled = true;
                            if (endInput) endInput.disabled = true;
                            startLive();
                        } else {
                            // Enable manual controls back
                            if (card) card.classList.remove('disabled-card');
                            if (btn) btn.disabled = false;
                            if (startInput) startInput.disabled = false;
                            if (endInput) endInput.disabled = false;
                            stopLive();
                        }
                    });
                }
                // متوقف کردن در پس‌زمینه و ادامه در بازگشت
                document.addEventListener('visibilitychange', function () {
                    if (document.hidden) {
                        stopLive();
                    } else if (liveToggle && liveToggle.checked) {
                        // اطمینان از غیرفعال بودن کارت دستی هنگام بازگشت
                        const card = document.getElementById('manualControlsCard');
                        const btn = document.getElementById('btnLoadData');
                        const startInput = document.getElementById('startDateTime');
                        const endInput = document.getElementById('endDateTime');
                        if (card) card.classList.add('disabled-card');
                        if (btn) btn.disabled = true;
                        if (startInput) startInput.disabled = true;
                        if (endInput) endInput.disabled = true;
                        startLive();
                    }
                });
            }

            function showError(message) {
                // Update legacy inline message (kept for accessibility)
                var errorElement = document.getElementById('errorMessage');
                if (errorElement) {
                    errorElement.textContent = message || '';
                    errorElement.style.display = message ? 'block' : 'none';
                }
                // Show Bootstrap toast
                var toastEl = document.getElementById('appErrorToast');
                var toastBody = document.getElementById('appErrorToastBody');
                if (toastEl && toastBody && window.bootstrap && bootstrap.Toast) {
                    toastBody.textContent = message || 'خطایی رخ داد.';
                    var t = bootstrap.Toast.getOrCreateInstance(toastEl, { delay: 5000, autohide: true });
                    t.show();
                }
            }
            function hideError() {
                var errorElement = document.getElementById('errorMessage');
                if (errorElement) {
                    errorElement.textContent = '';
                    errorElement.style.display = 'none';
                }
                var toastEl = document.getElementById('appErrorToast');
                if (toastEl && window.bootstrap && bootstrap.Toast) {
                    var t = bootstrap.Toast.getOrCreateInstance(toastEl);
                    t.hide();
                }
            }

            function groupLabelsByHour(labels) {
                if (!labels || labels.length === 0) return [];

                const hourlyLabels = [];
                const processedHours = {};

                // بررسی هر برچسب زمان
                for (let i = 0; i < labels.length; i++) {
                    const label = labels[i];
                    const parts = label.split(' ');
                    if (parts.length !== 2) continue;

                    const timeParts = parts[1].split(':');
                    if (timeParts.length !== 3) continue;

                    const hour = timeParts[0];
                    const minute = timeParts[1];

                    // اگر این ساعت قبلاً پردازش نشده است، آن را به لیست اضافه کن
                    const hourKey = parts[0] + '_' + hour; // ترکیب تاریخ و ساعت به عنوان کلید
                    if (!processedHours[hourKey]) {
                        processedHours[hourKey] = true;
                        hourlyLabels.push(i); // ذخیره شاخص برچسب به جای خود برچسب
                    }
                }

                return hourlyLabels;
            }

            // تابع تأخیر برای جلوگیری از فراخوانی مکرر
            function debounce(func, wait) {
                let timeout;
                return function () {
                    const context = this, args = arguments;
                    clearTimeout(timeout);
                    timeout = setTimeout(function () {
                        func.apply(context, args);
                    }, wait);
                };
            }

            // Global helpers for note hit-testing
            window.getAllNotePoints = function () {
                const result = [];
                try {
                    const ts = (window.__chartData && Array.isArray(window.__chartData.Timestamps)) ? window.__chartData.Timestamps : [];
                    const ids = (window.__chartData && Array.isArray(window.__chartData.Ids)) ? window.__chartData.Ids : [];
                    const notes = (window.__chartData && Array.isArray(window.__chartData.Notes)) ? window.__chartData.Notes : [];
                    for (let i = 0; i < ts.length; i++) {
                        const n = notes[i]; if (n && String(n).trim() !== '') result.push({ ts: ts[i], id: ids[i], note: n, index: i });
                    }
                    const extra = window.__extraNotes || {};
                    const ets = Array.isArray(extra.Timestamps) ? extra.Timestamps : [];
                    const eids = Array.isArray(extra.Ids) ? extra.Ids : [];
                    const enotes = Array.isArray(extra.Notes) ? extra.Notes : [];
                    for (let j = 0; j < ets.length; j++) {
                        const n = enotes[j]; if (!n || String(n).trim() === '') continue;
                        if (!result.find(r => r.ts === ets[j])) {
                            result.push({ ts: ets[j], id: eids[j], note: n, index: (ts ? ts.indexOf(ets[j]) : -1) });
                        }
                    }
                } catch (e) { console.warn('getAllNotePoints error', e); }
                return result;
            };

            window.findNearestNoteByClick = function (evt, chart) {
                try {
                    const xScale = chart.scales && chart.scales['x'];
                    if (!xScale) return null;
                    const canvasPos = Chart.helpers.getRelativePosition(evt, chart);
                    const x = canvasPos.x;
                    const notes = window.getAllNotePoints();
                    if (!notes.length) return null;
                    let best = null; let bestDist = Infinity;
                    for (const p of notes) {
                        const px = xScale.getPixelForValue(p.ts);
                        const d = Math.abs(px - x);
                        if (d < bestDist) { bestDist = d; best = p; }
                    }
                    if (best && bestDist <= 16) return best;
                    return null;
                } catch (e) { console.warn('findNearestNoteByClick error', e); return null; }
            };

            // Expose note functions globally so contextmenu handler can call them
            window.openNoteModal = function (ctx) {
                try {
                    const modalEl = document.getElementById('noteModal');
                    if (!modalEl) return;
                    modalEl.dataset.index = String(ctx.index);
                    modalEl.dataset.monitorId = String(ctx.monitorId || '');
                    const tsEl = document.getElementById('noteTimestamp');
                    const ta = document.getElementById('noteTextarea');
                    if (tsEl) tsEl.textContent = ctx.ts || '-';
                    if (ta) ta.value = ctx.note || '';
                    const delBtn = document.getElementById('btnDeleteNote');
                    if (delBtn) delBtn.style.display = (ctx.note && ctx.note.trim() !== '') ? 'inline-block' : 'none';
                    const bsModal = bootstrap.Modal.getOrCreateInstance(modalEl);
                    bsModal.show();
                } catch (e) { console.warn('openNoteModal error', e); }
            };

            function refreshNoteAnnotations() {
                try {
                    if (!window.chartInstance || !window.chartInstance.options || !window.chartInstance.options.plugins || !window.chartInstance.options.plugins.annotation) return;
                    const ann = window.chartInstance.options.plugins.annotation;
                    const current = Array.isArray(ann.annotations) ? ann.annotations : [];
                    const withoutNotes = current.filter(a => !(a && a.id && String(a.id).startsWith('note-')));
                    const newNotes = (window.createNoteAnnotations ? window.createNoteAnnotations(window.__chartData || {}) : []);
                    ann.annotations = withoutNotes.concat(newNotes);
                } catch (e) { console.warn('refreshNoteAnnotations error', e); }
            }

            window.saveNote = async function () {
                const modalEl = document.getElementById('noteModal');
                const idx = parseInt(modalEl.dataset.index || '-1', 10);
                const monitorId = modalEl.dataset.monitorId || '';
                const explicitId = modalEl.dataset.id ? parseInt(modalEl.dataset.id, 10) : null;
                const ta = document.getElementById('noteTextarea');
                const noteText = ta ? ta.value.trim() : '';
                // CSV mode: notes are read-only
                if (window.isCsvMode) {
                    showError('در حالت آرشیو آفلاین، ویرایش توضیحات امکان‌پذیر نیست.');
                    return;
                }
                let id = explicitId;
                if (!id && idx >= 0 && window.__chartData && window.__chartData.Ids) {
                    id = parseInt(window.__chartData.Ids[idx], 10);
                }
                if (!monitorId || !id) { console.warn('Missing monitorId or id'); return; }
                return new Promise((resolve, reject) => {
                    try {
                        PageMethods.UpsertNote(monitorId, id, noteText,
                            function (res) { try { const r = (typeof res === 'string') ? JSON.parse(res) : res; resolve(r); } catch (e) { resolve({ Ok: false, Message: String(e) }); } },
                            function (err) { try { reject(err && (err.get_message ? err.get_message() : err.message)); } catch (ex) { reject(err || ex); } }
                        );
                    } catch (e) { reject(e); }
                }).then(r => {
                    if (r && r.Ok) {
                        // به‌روزرسانی منبع محلی
                        if (idx >= 0 && window.__chartData && window.__chartData.Notes) { window.__chartData.Notes[idx] = noteText; }
                        else {
                            // اگر ایندکس دیتاست نامعتبر است، در __extraNotes ادغام کن تا بلافاصله نمایش داده شود
                            try {
                                window.__extraNotes = window.__extraNotes || { Timestamps: [], Ids: [], Notes: [] };
                                const tsEl = document.getElementById('noteTimestamp');
                                const tsVal = tsEl ? tsEl.textContent : null;
                                const idEl = modalEl.dataset.id ? parseInt(modalEl.dataset.id, 10) : null;
                                if (tsVal && idEl) {
                                    const pos = window.__extraNotes.Timestamps.indexOf(tsVal);
                                    if (pos >= 0) {
                                        window.__extraNotes.Notes[pos] = noteText;
                                    } else {
                                        window.__extraNotes.Timestamps.push(tsVal);
                                        window.__extraNotes.Ids.push(idEl);
                                        window.__extraNotes.Notes.push(noteText);
                                    }
                                }
                            } catch (e) { console.warn('merge __extraNotes on save error', e); }
                        }
                        // ابتدا رفرش محلی، سپس رفرش از سرور برای اطمینان
                        try { refreshNoteAnnotations(); if (window.chartInstance) window.chartInstance.update(); } catch (e) { }
                        try { if (window.fetchAndOverlayNotes) window.fetchAndOverlayNotes(window.__chartData || {}); } catch (e) { }
                        bootstrap.Modal.getOrCreateInstance(modalEl).hide();
                    } else {
                        showError('خطا در ذخیره توضیحات' + (r && r.Message ? (': ' + r.Message) : ''));
                    }
                }).catch(e => {
                    showError('خطا در ذخیره توضیحات'); console.error(e);
                });
            };

            window.deleteNote = async function () {
                const modalEl = document.getElementById('noteModal');
                const idx = parseInt(modalEl.dataset.index || '-1', 10);
                const monitorId = modalEl.dataset.monitorId || '';
                const explicitId = modalEl.dataset.id ? parseInt(modalEl.dataset.id, 10) : null;
                // CSV mode: notes are read-only
                if (window.isCsvMode) {
                    showError('در حالت آرشیو آفلاین، حذف توضیحات امکان‌پذیر نیست.');
                    return;
                }
                let id = explicitId;
                if (!id && idx >= 0 && window.__chartData && window.__chartData.Ids) { id = parseInt(window.__chartData.Ids[idx], 10); }
                if (!monitorId || !id) { console.warn('Missing monitorId or id'); return; }
                return new Promise((resolve, reject) => {
                    try {
                        PageMethods.DeleteNote(monitorId, id,
                            function (res) { try { const r = (typeof res === 'string') ? JSON.parse(res) : res; resolve(r); } catch (e) { resolve({ Ok: false, Message: String(e) }); } },
                            function (err) { try { reject(err && (err.get_message ? err.get_message() : err.message)); } catch (ex) { reject(err || ex); } }
                        );
                    } catch (e) { reject(e); }
                }).then(r => {
                    if (r && r.Ok) {
                        if (idx >= 0 && window.__chartData && window.__chartData.Notes) { window.__chartData.Notes[idx] = ''; }
                        else {
                            try {
                                const tsEl = document.getElementById('noteTimestamp');
                                const tsVal = tsEl ? tsEl.textContent : null;
                                if (window.__extraNotes && tsVal) {
                                    const pos = window.__extraNotes.Timestamps.indexOf(tsVal);
                                    if (pos >= 0) {
                                        window.__extraNotes.Notes[pos] = '';
                                    }
                                }
                            } catch (e) { console.warn('merge __extraNotes on delete error', e); }
                        }
                        // ابتدا رفرش محلی، سپس گرفتن از سرور برای همگام‌سازی کامل
                        try { refreshNoteAnnotations(); if (window.chartInstance) window.chartInstance.update(); } catch (e) { }
                        try { if (window.fetchAndOverlayNotes) window.fetchAndOverlayNotes(window.__chartData || {}); } catch (e) { }
                        bootstrap.Modal.getOrCreateInstance(modalEl).hide();
                    } else {
                        showError('خطا در حذف توضیحات' + (r && r.Message ? (': ' + r.Message) : ''));
                    }
                }).catch(e => {
                    showError('خطا در حذف توضیحات'); console.error(e);
                });
            };
        </script>
    <!-- Note Modal -->
    <div class="modal fade" id="noteModal" tabindex="-1" aria-labelledby="noteModalLabel" aria-hidden="true" dir="rtl">
        <div class="modal-dialog">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title" id="noteModalLabel">توضیحات نقطه</h5>
                    <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="بستن"></button>
                </div>
                <div class="modal-body">
                    <div class="mb-2">
                        <div class="form-text">زمان نمونه:</div>
                        <div class="fw-bold" id="noteTimestamp">-</div>
                    </div>
                    <div class="mb-2">
                        <label class="form-label">توضیحات</label>
                        <textarea id="noteTextarea" rows="5" class="form-control" placeholder="متن توضیحات را وارد کنید..."></textarea>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-danger" id="btnDeleteNote" onclick="deleteNote()">حذف توضیحات</button>
                    <button type="button" class="btn btn-primary" onclick="saveNote()">ذخیره</button>
                    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">بستن</button>
                </div>
            </div>
        </div>
    </div>
    </form>
</body>
</html>
