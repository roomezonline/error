window.initScrollReveal = function () {
    if (!window.__srObserver) {
        window.__srObserver = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    entry.target.classList.add('visible');
                    window.__srObserver.unobserve(entry.target);
                }
            });
        }, { threshold: 0.05, rootMargin: '0px 0px -30px 0px' });
    }

    document.querySelectorAll('.reveal:not(.visible)').forEach(function (el) {
        window.__srObserver.observe(el);
    });

    return true;
};

window.observeNewReveals = function () {
    if (window.initScrollReveal) {
        window.initScrollReveal();
    }
};