(function() {
    'use strict';

    console.log('Admin header scroll script loaded');

    let lastScrollTop = 0;
    let scrollTimeout;
    const header = document.querySelector('.admin-header');
    const scrollThreshold = 50; // Minimum scroll distance to trigger hide/show
    const hideDelay = 100; // Delay before hiding to prevent flickering

    console.log('Header element:', header);

    if (!header) {
        console.error('Admin header not found');
        return;
    }

    function handleScroll() {
        const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
        const scrollDirection = scrollTop > lastScrollTop ? 'down' : 'up';
        const scrollDistance = Math.abs(scrollTop - lastScrollTop);

        console.log('Scroll:', { scrollTop, lastScrollTop, direction: scrollDirection, distance: scrollDistance });

        // Don't hide if at the top of the page
        if (scrollTop < 10) {
            header.classList.remove('admin-header--hidden');
            lastScrollTop = scrollTop;
            return;
        }

        // Clear previous timeout
        if (scrollTimeout) {
            clearTimeout(scrollTimeout);
        }

        // Apply scroll behavior with delay
        scrollTimeout = setTimeout(function() {
            if (scrollDirection === 'down' && scrollDistance > scrollThreshold) {
                console.log('Hiding header');
                header.classList.add('admin-header--hidden');
            } else if (scrollDirection === 'up' && scrollDistance > scrollThreshold) {
                console.log('Showing header');
                header.classList.remove('admin-header--hidden');
            }
        }, hideDelay);

        lastScrollTop = scrollTop;
    }

    // Throttle scroll events
    let ticking = false;
    function throttledScroll() {
        if (!ticking) {
            window.requestAnimationFrame(function() {
                handleScroll();
                ticking = false;
            });
            ticking = true;
        }
    }

    // Add scroll event listener
    window.addEventListener('scroll', throttledScroll, { passive: true });
    console.log('Scroll event listener added');

    // Initial check
    handleScroll();
})();
