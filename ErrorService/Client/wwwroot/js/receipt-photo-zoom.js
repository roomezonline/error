window.initReceiptPhotoZoom = (wrapId, imgId) => {
    const wrap = document.getElementById(wrapId);
    const img = document.getElementById(imgId);
    if (!wrap || !img) return true;

    // Reset
    let scale = 1;
    let tx = 0;
    let ty = 0;
    let isPanning = false;
    let startX = 0;
    let startY = 0;

    // Pinch
    let isPinching = false;
    let pinchStartDist = 0;
    let pinchStartScale = 1;
    let pinchCenter = null;

    const clamp = (v, min, max) => Math.min(max, Math.max(min, v));

    const apply = () => {
        img.style.transform = `translate3d(${tx}px, ${ty}px, 0) scale(${scale})`;
    };

    const reset = () => {
        scale = 1;
        tx = 0;
        ty = 0;
        apply();
    };

    const getDist = (t1, t2) => {
        const dx = t1.clientX - t2.clientX;
        const dy = t1.clientY - t2.clientY;
        return Math.sqrt(dx * dx + dy * dy);
    };

    const getCenter = (t1, t2) => ({
        x: (t1.clientX + t2.clientX) / 2,
        y: (t1.clientY + t2.clientY) / 2
    });

    // Make sure we don't add duplicate listeners on re-open.
    // We mark the element and if already initialized, just reset transform.
    if (wrap.dataset.zoomInit === '1') {
        reset();
        return true;
    }
    wrap.dataset.zoomInit = '1';

    wrap.addEventListener('dblclick', (e) => {
        e.preventDefault();
        if (scale > 1) {
            reset();
        } else {
            scale = 2;
            apply();
        }
    });

    wrap.addEventListener('wheel', (e) => {
        e.preventDefault();
        const delta = -e.deltaY;
        const factor = delta > 0 ? 1.08 : 0.92;
        scale = clamp(scale * factor, 1, 5);
        if (scale === 1) {
            tx = 0;
            ty = 0;
        }
        apply();
    }, { passive: false });

    // Pointer panning for desktop
    wrap.addEventListener('pointerdown', (e) => {
        if (e.pointerType === 'touch') return; // touch handled by touch events below
        if (scale <= 1) return;
        isPanning = true;
        startX = e.clientX - tx;
        startY = e.clientY - ty;
        wrap.setPointerCapture(e.pointerId);
    });

    wrap.addEventListener('pointermove', (e) => {
        if (!isPanning) return;
        tx = e.clientX - startX;
        ty = e.clientY - startY;
        apply();
    });

    wrap.addEventListener('pointerup', () => {
        isPanning = false;
    });

    wrap.addEventListener('pointercancel', () => {
        isPanning = false;
    });

    // Touch pinch + pan
    wrap.addEventListener('touchstart', (e) => {
        if (e.touches.length === 2) {
            isPinching = true;
            pinchStartDist = getDist(e.touches[0], e.touches[1]);
            pinchStartScale = scale;
            pinchCenter = getCenter(e.touches[0], e.touches[1]);
        } else if (e.touches.length === 1 && scale > 1) {
            isPanning = true;
            startX = e.touches[0].clientX - tx;
            startY = e.touches[0].clientY - ty;
        }
    }, { passive: true });

    wrap.addEventListener('touchmove', (e) => {
        if (isPinching && e.touches.length === 2) {
            e.preventDefault();
            const dist = getDist(e.touches[0], e.touches[1]);
            const next = clamp(pinchStartScale * (dist / pinchStartDist), 1, 5);
            scale = next;
            if (scale === 1) {
                tx = 0;
                ty = 0;
            }
            apply();
            return;
        }

        if (isPanning && e.touches.length === 1) {
            e.preventDefault();
            tx = e.touches[0].clientX - startX;
            ty = e.touches[0].clientY - startY;
            apply();
        }
    }, { passive: false });

    wrap.addEventListener('touchend', (e) => {
        if (e.touches.length < 2) isPinching = false;
        if (e.touches.length === 0) isPanning = false;
    });

    wrap.addEventListener('touchcancel', () => {
        isPinching = false;
        isPanning = false;
    });

    // Initial apply
    apply();
    return true;
};
