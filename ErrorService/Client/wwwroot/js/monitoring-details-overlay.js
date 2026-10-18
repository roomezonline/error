(function () {
    // No "already defined" guard here on purpose: the lazy-loader pre-installs a
    // window.mdChartOverlay stub, and a guard would see it and skip the real
    // definition forever. This file is loaded exactly once (promise-cached).
    var netRef = null;
    var bound = false;

    window.mdChartOverlay = {
        register: function (ref) {
            netRef = ref;
            if (!bound) {
                bound = true;
                window.addEventListener('popstate', function (e) {
                    if (!netRef) return;
                    var mode = (e.state && e.state.__mdChartOverlay) ? e.state.__mdChartOverlay : null;
                    netRef.invokeMethodAsync('OnChartOverlayPop', mode).catch(function () {});
                });
            }
        },
        open: function (mode) {
            history.pushState({ __mdChartOverlay: mode }, '', location.href);
        },
        back: function () {
            if (history.state && history.state.__mdChartOverlay) {
                history.back();
            }
        }
    };
})();
