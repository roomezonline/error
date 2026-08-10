(function () {
    const RTL_REGEX = /[\u0591-\u07FF\uFB1D-\uFDFD\uFE70-\uFEFC]/;

    function isRtl(text) {
        return RTL_REGEX.test(text || "");
    }

    function sleep(ms) {
        return new Promise((r) => setTimeout(r, ms));
    }

    async function typeInto(el, text, options) {
        if (!el) return;

        const token = (Date.now() + Math.random()).toString(16);
        el.__typewriterToken = token;

        const rtl = isRtl(text);
        el.dir = rtl ? "rtl" : "ltr";

        el.classList.add("is-typing");
        el.textContent = "";

        const speed = options?.speedMs ?? 28;
        const startDelay = options?.startDelayMs ?? 0;
        const pauseAtEnd = options?.pauseAtEndMs ?? 250;

        if (startDelay > 0) {
            await sleep(startDelay);
        }

        for (let i = 0; i < text.length; i++) {
            if (el.__typewriterToken !== token) return;
            el.textContent += text[i];
            await sleep(speed);
        }

        if (pauseAtEnd > 0) {
            await sleep(pauseAtEnd);
        }

        if (el.__typewriterToken === token) {
            el.classList.remove("is-typing");
        }
    }

    window.heroTypewriter = {
        run: async function (rootSelector) {
            const root = document.querySelector(rootSelector || ".hero__slider");
            if (!root) return;

            const activeSlide = root.querySelector(".hero__slide.is-active");
            if (!activeSlide) return;

            const titleEl = activeSlide.querySelector(".hero__typedTitle");
            const subEl = activeSlide.querySelector(".hero__typedSubtitle");

            const title = (titleEl?.getAttribute("data-text") || "").trim();
            const subtitle = (subEl?.getAttribute("data-text") || "").trim();

            await typeInto(titleEl, title, { speedMs: 26, startDelayMs: 60, pauseAtEndMs: 220 });
            await typeInto(subEl, subtitle, { speedMs: 18, startDelayMs: 80, pauseAtEndMs: 0 });
        }
    };
})();
