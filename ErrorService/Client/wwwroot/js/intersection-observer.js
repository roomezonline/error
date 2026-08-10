window.setupIntersectionObserver = function (element, dotNetReference) {
    if (!element || !window.IntersectionObserver) return;

    const observer = new IntersectionObserver(
        (entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    dotNetReference.invokeMethodAsync('OnIntersect');
                }
            });
        },
        {
            root: null,
            rootMargin: '100px',
            threshold: 0.1
        }
    );

    observer.observe(element);

    // Return cleanup function
    return () => observer.disconnect();
};
