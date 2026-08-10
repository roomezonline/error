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