(function () {
    if (window.mdChartOverlay) return;

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
