window.lazyVideoLoader = {
    observe: function () {
        const videoObserver = new IntersectionObserver((entries, observer) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const video = entry.target;
                    // If video has a data-src, move it to src
                    if (video.dataset.src) {
                        video.src = video.dataset.src;
                        video.removeAttribute('data-src');
                    }
                    // Load the video
                    video.load();
                    observer.unobserve(video);
                }
            });
        }, {
            rootMargin: '50px 0px',
            threshold: 0.01
        });

        const videos = document.querySelectorAll('video.lazy-video');
        videos.forEach(video => videoObserver.observe(video));
    }
};
