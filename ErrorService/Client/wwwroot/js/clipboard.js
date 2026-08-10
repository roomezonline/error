window.__copyToClipboard = function(text) {
    if (navigator.clipboard && navigator.clipboard.writeText) {
        return navigator.clipboard.writeText(text).catch(function() {
            fallbackCopy(text);
        });
    } else {
        fallbackCopy(text);
        return Promise.resolve();
    }
    function fallbackCopy(t) {
        var ta = document.createElement('textarea');
        ta.value = t;
        ta.style.position = 'fixed';
        ta.style.opacity = '0';
        ta.style.pointerEvents = 'none';
        document.body.appendChild(ta);
        ta.select();
        try { document.execCommand('copy'); } catch(e) {}
        document.body.removeChild(ta);
    }
};

window.__initOnlineDetection = function(dotNetRef) {
    window.__onlineHandler = function() {
        dotNetRef.invokeMethodAsync('SetOnlineStatus', true);
    };
    window.__offlineHandler = function() {
        dotNetRef.invokeMethodAsync('SetOnlineStatus', false);
    };
    window.addEventListener('online', window.__onlineHandler);
    window.addEventListener('offline', window.__offlineHandler);
    return navigator.onLine;
};

window.__removeOnlineDetection = function() {
    if (window.__onlineHandler) {
        window.removeEventListener('online', window.__onlineHandler);
    }
    if (window.__offlineHandler) {
        window.removeEventListener('offline', window.__offlineHandler);
    }
};
