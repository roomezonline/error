window.initJalali = () => {
    if (window.jalaliDatepicker && typeof window.jalaliDatepicker.startWatch === 'function') {
        window.jalaliDatepicker.startWatch({
            selector: "[data-jdp]",
            time: false,
            hasSecond: false,
            persianDigits: false,
            autoHide: true,
            hideAfterChange: false,
            showCloseBtn: true,
            showTodayBtn: true,
            showEmptyBtn: true
        });
    }
};

window.initJalaliDatePickers = window.initJalali;

window.initJalaliForElement = (element) => {
    if (!element || !window.jalaliDatepicker) return;
    
    // Ensure the library is watching this element
    window.jalaliDatepicker.startWatch({
        selector: "[data-jdp]",
        time: false,
        persianDigits: false,
        autoHide: true,
        showCloseBtn: true,
        showTodayBtn: true
    });

    // Manual show trigger
    const showPicker = (e) => {
        e.stopPropagation();
        window.jalaliDatepicker.show(element);
    };

    element.addEventListener('mousedown', showPicker);
    element.addEventListener('touchstart', showPicker);
    
    // Dispatch input event for Blazor binding when value changes
    element.addEventListener('jdp:change', () => {
        element.dispatchEvent(new Event('input', { bubbles: true }));
    });
};

window.addEventListener('load', () => {
    setTimeout(window.initJalali, 500);
});
