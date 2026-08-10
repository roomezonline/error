window.captureCommitmentPng = function (selector, fileName) {
    var el = document.querySelector(selector);
    if (!el) return;
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
    html2canvas(el, { scale: 2, useCORS: true, backgroundColor: '#ffffff' }).then(function (canvas) {
        canvas.toBlob(function (blob) {
            var file = new File([blob], fileName || 'commitment.png', { type: 'image/png' });
            if (navigator.share) {
                navigator.share({ files: [file], title: 'تعهدنامه' }).catch(function () { });
            }
        });
    });
};
