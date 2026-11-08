// تبدیل خودکار متن کپی‌شده (HTML) به متن نشانه‌دار در ویرایشگر پیش‌نویس دستی استودیو محتوا.
(function () {
    let converter = null;

    window.csSetDraftPasteConverter = function (dotNetRef) {
        converter = dotNetRef || null;
    };

    function isDraftText(el) {
        return !!el && el.tagName === 'TEXTAREA' && el.classList.contains('cs-draft-text');
    }

    function insert(area, text, start, end) {
        if (!text) return;
        area.focus();
        const max = parseInt(area.getAttribute('maxlength') || '0', 10);
        if (max > 0) {
            const room = max - (area.value.length - (end - start));
            if (room <= 0) return;
            if (text.length > room) text = text.slice(0, room);
        }
        area.setRangeText(text, start, end, 'end');
        area.dispatchEvent(new Event('input', { bubbles: true }));
    }

    document.addEventListener('paste', function (ev) {
        const area = ev.target;
        if (!converter || !isDraftText(area)) return;

        const data = ev.clipboardData || window.clipboardData;
        if (!data) return;

        const html = data.getData('text/html');
        if (!html) return;

        const plain = data.getData('text/plain');
        const start = area.selectionStart;
        const end = area.selectionEnd;
        ev.preventDefault();

        converter.invokeMethodAsync('ConvertPastedHtml', html).then(function (markers) {
            insert(area, markers || plain, start, end);
        }).catch(function () {
            insert(area, plain, start, end);
        });
    }, true);
})();
