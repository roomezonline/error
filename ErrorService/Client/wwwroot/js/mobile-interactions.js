window.mobileInteractions = {
    hapticFeedback: (type) => {
        if (!window.navigator || !window.navigator.vibrate) return;
        switch (type) {
            case 'light': window.navigator.vibrate(10); break;
            case 'medium': window.navigator.vibrate(20); break;
            case 'error': window.navigator.vibrate([10, 30, 10]); break;
            case 'success': window.navigator.vibrate([20, 10, 20]); break;
        }
    },
    setupRippleEffect: () => {
        document.addEventListener('pointerdown', (e) => {
            const target = e.target.closest('.nav-item, .quick-link, .suggest-item, .btn.ripple');
            if (!target) return;

            const ripple = document.createElement('div');
            ripple.className = 'ripple-effect';
            
            const rect = target.getBoundingClientRect();
            const size = Math.max(rect.width, rect.height);
            ripple.style.width = ripple.style.height = `${size}px`;
            
            ripple.style.left = `${e.clientX - rect.left - size/2}px`;
            ripple.style.top = `${e.clientY - rect.top - size/2}px`;
            
            target.appendChild(ripple);
            ripple.addEventListener('animationend', () => ripple.remove());
        });
    }
};

window.mobileInteractions.setupRippleEffect();

window.formatPriceInputs = () => {
    document.querySelectorAll('.price-input-ltr').forEach(el => {
        if (el.dataset.priceInit) return;
        el.dataset.priceInit = '1';
        el.addEventListener('input', function () {
            const raw = this.value.replace(/,/g, '').replace(/[^0-9]/g, '');
            if (raw) {
                this.value = parseInt(raw, 10).toLocaleString('en-US');
            } else {
                this.value = '';
            }
        });
    });
};

window.getPriceRawValue = (id) => {
    const el = document.getElementById(id);
    if (!el) return '0';
    return el.value.replace(/,/g, '');
};
