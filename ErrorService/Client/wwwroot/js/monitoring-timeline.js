window.monitoringTimeline = {
    _instances: {},

    init: function (elementId) {
        var container = document.getElementById(elementId);
        if (!container) return;

        var self = this;
        this._instances[elementId] = {
            container: container,
            isFullscreen: false
        };

        container.addEventListener('wheel', function (e) {
            if (e.deltaY !== 0) {
                e.preventDefault();
                container.scrollLeft += e.deltaY * 2;
            }
        }, { passive: false });

        var isDown = false;
        var startX, scrollLeft;

        container.addEventListener('mousedown', function (e) {
            isDown = true;
            container.classList.add('grabbing');
            startX = e.pageX - container.offsetLeft;
            scrollLeft = container.scrollLeft;
        });

        container.addEventListener('mouseleave', function () {
            isDown = false;
            container.classList.remove('grabbing');
        });

        container.addEventListener('mouseup', function () {
            isDown = false;
            container.classList.remove('grabbing');
        });

        container.addEventListener('mousemove', function (e) {
            if (!isDown) return;
            e.preventDefault();
            var x = e.pageX - container.offsetLeft;
            var walk = (x - startX) * 1.5;
            container.scrollLeft = scrollLeft - walk;
        });
    },

    toggleFullscreen: function (elementId) {
        var inst = this._instances[elementId];
        if (!inst) return false;

        if (!document.fullscreenElement) {
            var el = document.documentElement;
            if (el.requestFullscreen) {
                el.requestFullscreen();
                inst.isFullscreen = true;
            } else if (el.webkitRequestFullscreen) {
                el.webkitRequestFullscreen();
                inst.isFullscreen = true;
            } else if (el.msRequestFullscreen) {
                el.msRequestFullscreen();
                inst.isFullscreen = true;
            }
            return true;
        } else {
            if (document.exitFullscreen) {
                document.exitFullscreen();
            } else if (document.webkitExitFullscreen) {
                document.webkitExitFullscreen();
            } else if (document.msExitFullscreen) {
                document.msExitFullscreen();
            }
            inst.isFullscreen = false;
            return false;
        }
    },

    isFullscreen: function () {
        return !!document.fullscreenElement;
    },

    scrollToTimestamp: function (elementId, percent) {
        var container = document.getElementById(elementId);
        if (!container) return;
        container.scrollLeft = percent * (container.scrollWidth - container.clientWidth);
    }
};
