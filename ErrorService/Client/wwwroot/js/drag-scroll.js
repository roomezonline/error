window.initDragScroll = (element) => {
    if (!element || element.dataset.dragScrollInit === '1') return;
    element.dataset.dragScrollInit = '1';

    const isRtl = getComputedStyle(element).direction === 'rtl';

    let rtlNeg = null;
    const detectRtlNeg = () => {
        if (rtlNeg !== null) return rtlNeg;
        const div = document.createElement('div');
        div.dir = 'rtl';
        div.style.cssText = 'width:4px;height:1px;overflow:scroll;position:absolute;top:-9999px';
        const inner = document.createElement('div');
        inner.style.cssText = 'width:10px;height:1px';
        div.appendChild(inner);
        document.body.appendChild(div);
        div.scrollLeft = 1;
        rtlNeg = div.scrollLeft === 0;
        document.body.removeChild(div);
        return rtlNeg;
    };

    const getMax = () => Math.max(0, element.scrollWidth - element.clientWidth);

    const getNorm = () => {
        if (!isRtl) return element.scrollLeft;
        return detectRtlNeg() ? -element.scrollLeft : element.scrollLeft;
    };

    const setNorm = (val) => {
        const clamped = Math.max(0, Math.min(getMax(), val));
        if (!isRtl) { element.scrollLeft = clamped; return; }
        element.scrollLeft = detectRtlNeg() ? -clamped : clamped;
    };

    let isDown = false;
    let startX = 0;
    let scrollStart = 0;

    element.addEventListener('mousedown', (e) => {
        if (e.button !== 0) return;
        e.preventDefault();
        isDown = true;
        startX = e.clientX;
        scrollStart = getNorm();
        element.style.cursor = 'grabbing';
        element.style.userSelect = 'none';
    });

    document.addEventListener('mousemove', (e) => {
        if (!isDown) return;
        const dx = e.clientX - startX;
        setNorm(scrollStart + (isRtl ? dx : -dx));
    });

    document.addEventListener('mouseup', () => {
        if (!isDown) return;
        isDown = false;
        element.style.cursor = '';
        element.style.userSelect = '';
    });

    element.addEventListener('wheel', (e) => {
        if (Math.abs(e.deltaX) > Math.abs(e.deltaY)) return;
        e.preventDefault();
        setNorm(getNorm() + (isRtl ? e.deltaY : -e.deltaY) * 0.5);
    }, { passive: false });
};
