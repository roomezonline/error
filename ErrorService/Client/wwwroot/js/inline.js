        window.ensureHljs = () => {
            if (window.hljs && typeof window.hljs.configure === 'function') {
                try { window.hljs.configure({ ignoreUnescapedHTML: true }); } catch (e) { }
            }
            return true;
        };

        window.uploadEditorFile = async (uploadUrl, file) => {
            const formData = new FormData();
            formData.append('file', file, file.name);
            const resp = await fetch(uploadUrl, { method: 'POST', body: formData });
            if (!resp.ok) {
                const txt = await resp.text();
                throw new Error(txt || ('Upload failed: ' + resp.status));
            }
            const url = (await resp.text()).replace(/"/g, '');
            return url;
        };

        window.pickAndUploadForQuill = (containerId, kind, uploadUrl) => {
            const container = document.getElementById(containerId);
            if (!container || !container.__quill) return;

            const input = document.createElement('input');
            input.type = 'file';
            input.accept = kind === 'video' ? 'video/*' : 'image/*';
            input.style.display = 'none';
            document.body.appendChild(input);

            input.onchange = async () => {
                try {
                    const file = input.files && input.files[0];
                    if (!file) return;
                    const url = await window.uploadEditorFile(uploadUrl, file);

                    const quill = container.__quill;
                    const range = quill.getSelection(true);
                    const index = range ? range.index : quill.getLength();

                    if (kind === 'video') {
                        quill.insertEmbed(index, 'video', url, 'user');
                    } else {
                        quill.insertEmbed(index, 'image', url, 'user');
                    }

                    quill.setSelection(index + 1, 0);
                } catch (e) {
                    console.error(e);
                    alert(e.message || 'خطا در آپلود فایل');
                } finally {
                    document.body.removeChild(input);
                }
            };

            input.click();
        };

        window.promptAndToggleLinkForQuill = (containerId) => {
            const container = document.getElementById(containerId);
            if (!container || !container.__quill) return;

            const quill = container.__quill;
            const range = quill.getSelection(true);
            if (!range) return;

            const fmt = quill.getFormat(range);
            const current = fmt && fmt.link ? String(fmt.link) : '';
            const url = prompt('لینک را وارد کنید (برای حذف لینک، خالی بگذارید)', current);
            if (url === null) return; // canceled

            const normalizeLink = (raw) => {
                const v = String(raw || '').trim();
                if (!v) return '';

                const lower = v.toLowerCase();
                // Keep explicit schemes
                if (
                    lower.startsWith('http://') ||
                    lower.startsWith('https://') ||
                    lower.startsWith('mailto:') ||
                    lower.startsWith('tel:') ||
                    lower.startsWith('#')
                ) {
                    return v;
                }

                // Protocol-relative //example.com
                if (v.startsWith('//')) {
                    return 'https:' + v;
                }

                // Internal absolute path
                if (v.startsWith('/')) {
                    return v;
                }

                // If it looks like a domain (contains a dot) treat as external
                // e.g. www.errorservice.ir or errorservice.ir/path
                if (v.includes('.')) {
                    return 'https://' + v;
                }

                // Otherwise treat as internal relative route
                // e.g. news/12 => /news/12
                return '/' + v;
            };

            const val = normalizeLink(url);
            if (!val) {
                quill.format('link', false, 'user');
                return;
            }

            quill.format('link', val, 'user');
        };

        window.initQuillEditor = (containerId, content, dotNetHelper) => {
            const container = document.getElementById(containerId);
            if (!container) return null;

            // If already initialized, just update content
            if (container.__quill && container.__quillInitialized) {
                if (content && container.__quill.root.innerHTML !== content) {
                    const oldScroll = window.scrollY;
                    container.__quill.root.innerHTML = content;
                    window.scrollTo(0, oldScroll);
                }
                return container.__quill;
            }

            const initialHtml = content || '';
            try {
                // Clear container completely
                container.innerHTML = '';

                // Create editor div
                const editorDiv = document.createElement('div');
                container.appendChild(editorDiv);

                // Quill Pro: fonts whitelist
                try {
                    const Font = Quill.import('formats/font');
                    Font.whitelist = ['vazirmatn', 'vazir', 'shabnam', 'sahel'];
                    Quill.register(Font, true);

                    // Register Callout attribute formally in Quill (stable persistence)
                    // Produces: <p data-callout="info">...</p>
                    const Parchment = Quill.import('parchment');
                    const CalloutAttr = new Parchment.Attributor.Attribute('callout', 'data-callout', {
                        scope: Parchment.Scope.BLOCK
                    });
                    Quill.register(CalloutAttr, true);
                } catch (e) { console.error('Quill register error:', e); }

                // Quill Pro: modules
                // - syntax highlight (highlight.js)
                // - image resize
                // - better table (disabled for now due to browser build incompatibilities)
                if (window.ImageResize) {
                    const mod = window.ImageResize.default ? window.ImageResize.default : window.ImageResize;
                    try { Quill.register('modules/imageResize', mod); } catch (e) { }
                }

                const quill = new Quill(editorDiv, {
                    theme: 'snow',
                    modules: {
                        toolbar: [
                            [{ 'font': ['vazirmatn', 'vazir', 'shabnam', 'sahel'] }],
                            [{ 'size': ['small', false, 'large', 'huge'] }],
                            ['bold', 'italic', 'underline', 'strike'],
                            [{ 'color': [] }, { 'background': [] }],
                            [{ 'align': [] }],
                            [{ 'list': 'ordered' }, { 'list': 'bullet' }],
                            [{ 'indent': '-1' }, { 'indent': '+1' }],
                            ['blockquote', 'code-block'],
                            ['link', 'image', 'video'],
                            ['clean']
                        ],
                        syntax: window.hljs ? { highlight: text => window.hljs.highlightAuto(text).value } : false,
                        imageResize: window.ImageResize ? {} : undefined,
                        keyboard: {}
                    },
                    placeholder: 'متن خبر را اینجا بنویسید...',
                    direction: 'rtl',
                    autofocus: false
                });

                // Prevent Quill from stealing focus when user is interacting with other inputs.
                // Allow focus ONLY after user clicks inside editor.
                try {
                    container.__allowQuillFocus = false;
                    const origFocus = quill.focus ? quill.focus.bind(quill) : null;
                    if (origFocus) {
                        quill.focus = function () {
                            if (container.__allowQuillFocus) {
                                return origFocus();
                            }
                            return;
                        };
                    }

                    quill.root.addEventListener('mousedown', () => {
                        container.__allowQuillFocus = true;
                    }, true);
                    quill.root.addEventListener('touchstart', () => {
                        container.__allowQuillFocus = true;
                    }, { passive: true, capture: true });

                    container.__quillDocMouseDownHandler = (ev) => {
                        const t = ev.target;
                        if (!t) return;
                        // Click outside editor container => disallow Quill focus
                        if (!container.contains(t)) {
                            container.__allowQuillFocus = false;
                        }
                    };
                    document.addEventListener('mousedown', container.__quillDocMouseDownHandler, true);
                    document.addEventListener('touchstart', container.__quillDocMouseDownHandler, true);
                } catch (e) { }

                window.ensureHljs();

                // Upload handlers (news)
                const toolbarModule = quill.getModule('toolbar');
                if (toolbarModule) {
                    toolbarModule.addHandler('image', function () {
                        window.pickAndUploadForQuill(containerId, 'image', '/api/news/upload');
                    });
                    toolbarModule.addHandler('video', function () {
                        // Video button: embed/link
                        window.promptAndInsertVideoLink(containerId);
                    });
                    toolbarModule.addHandler('link', function () {
                        window.promptAndToggleLinkForQuill(containerId);
                    });
                }

                // Apply RTL to toolbar
                const toolbar = container.querySelector('.ql-toolbar');
                if (toolbar) {
                    toolbar.style.direction = 'rtl';
                }

                // Keep editor RTL
                quill.root.style.direction = 'rtl';
                quill.root.style.textAlign = 'right';

                // Track last tapped image (mobile-friendly resize)
                container.__lastImageDom = null;
                quill.root.addEventListener('click', (ev) => {
                    const t = ev.target;
                    if (t && t.tagName === 'IMG') {
                        container.__lastImageDom = t;
                        try {
                            const blot = Quill.find(t);
                            if (blot) {
                                const idx = quill.getIndex(blot);
                                quill.setSelection(idx, 1, 'user');
                            }
                        } catch (e) { }
                    }
                });

                // Set initial content
                if (initialHtml) {
                    quill.root.innerHTML = initialHtml;
                }

                // Sync with Blazor on text change
                quill.on('text-change', function (delta, oldDelta, source) {
                    if (source === 'user' && dotNetHelper) {
                        dotNetHelper.invokeMethodAsync('UpdateContent', quill.root.innerHTML);
                    }
                });

                container.__quill = quill;
                container.__quillInitialized = true;
                return quill;
            } catch (e) {
                console.error('initQuillEditor failed:', e);
                // Don't leave a blank editor area
                container.innerHTML = initialHtml;
                return null;
            }
        };

        window.__initQuillEditorRaw = window.initQuillEditor;
        window.initQuillEditor = function () {
            var args = arguments;
            return window.__ensureGroup('quill').then(function () {
                return window.__initQuillEditorRaw.apply(null, args);
            });
        };

        window.getQuillContent = (containerId) => {
            const container = document.getElementById(containerId);
            if (container && container.__quill) {
                return container.__quill.root.innerHTML;
            }
            return "";
        };

        window.destroyQuillEditor = (containerId) => {
            const container = document.getElementById(containerId);
            if (!container) return false;
            try {
                if (container.__quill) {
                    try { container.__quill.off('text-change'); } catch (e) { }
                    try { container.__quill.off('selection-change'); } catch (e) { }
                }
            } catch (e) { }
            try {
                if (container.__quillDocMouseDownHandler) {
                    document.removeEventListener('mousedown', container.__quillDocMouseDownHandler, true);
                    document.removeEventListener('touchstart', container.__quillDocMouseDownHandler, true);
                }
            } catch (e) { }
            try {
                delete container.__quill;
                delete container.__quillInitialized;
                delete container.__lastImageDom;
                delete container.__quillDocMouseDownHandler;
                delete container.__allowQuillFocus;
            } catch (e) { }
            container.innerHTML = '';
            return true;
        };

        window.setQuillContent = (containerId, content) => {
            const container = document.getElementById(containerId);
            if (container && container.__quill) {
                container.__quill.root.innerHTML = content || "";
            }
        };

        window.insertHtmlAtCursor = (containerId, html) => {
            const container = document.getElementById(containerId);
            if (!container || !container.__quill) return;

            const quill = container.__quill;
            const range = quill.getSelection(true);
            const index = range ? range.index : quill.getLength();

            // Insert HTML at cursor position
            quill.clipboard.dangerouslyPasteHTML(index, html);

            // Move cursor after inserted content
            quill.setSelection(index + 1, 0);
        };

        window.insertImageAtCursor = (containerId, url) => {
            const container = document.getElementById(containerId);
            if (!container || !container.__quill) return;

            const quill = container.__quill;
            const range = quill.getSelection(true);
            const index = range ? range.index : quill.getLength();
            quill.insertEmbed(index, 'image', url, 'user');
            quill.setSelection(index + 1, 0);
        };

        window.applyCalloutToSelection = (containerId, calloutType) => {
            const container = document.getElementById(containerId);
            if (!container || !container.__quill) return;

            const quill = container.__quill;
            const range = quill.getSelection(true);
            if (!range) return;

            const len = Math.max(1, range.length || 0);

            // Clear previous callout on all lines in selection
            quill.formatLine(range.index, len, 'callout', false, 'user');

            if (!calloutType || calloutType === 'none') return;

            // Apply to all lines in selection
            quill.formatLine(range.index, len, 'callout', calloutType, 'user');
        };

        window.exitCalloutAndContinue = (containerId) => {
            const container = document.getElementById(containerId);
            if (!container || !container.__quill) return;

            const quill = container.__quill;
            const range = quill.getSelection(true);
            if (!range) return;

            // Clear current line callout formatting
            quill.formatLine(range.index, 1, 'callout', false, 'user');

            // Move to end and insert new line
            const [line] = quill.getLine(range.index);
            if (line) {
                const lineIndex = quill.getIndex(line);
                const lineLength = line.length();
                quill.setSelection(lineIndex + lineLength, 0, 'user');
                quill.insertText(lineIndex + lineLength, '\n', 'user');
                quill.setSelection(lineIndex + lineLength + 1, 0, 'user');
            }
        };

        window.bindCalloutSelectorForQuill = (containerId, selectorId) => {
            const container = document.getElementById(containerId);
            const selector = document.getElementById(selectorId);
            if (!container || !container.__quill || !selector) return false;

            const quill = container.__quill;

            const update = () => {
                const range = quill.getSelection(true);
                if (!range) return;
                const fmt = quill.getFormat(range.index, 1);
                const v = fmt && fmt.callout ? String(fmt.callout) : 'none';
                if (selector.value !== v) selector.value = v;
            };

            if (selector.dataset.boundToQuill === '1') {
                update();
                return true;
            }
            selector.dataset.boundToQuill = '1';

            quill.on('selection-change', () => update());
            quill.on('text-change', () => update());
            update();
            return true;
        };

        window.insertVideoAtCursor = (containerId, url) => {
            const container = document.getElementById(containerId);
            if (!container || !container.__quill) return;

            const quill = container.__quill;
            const range = quill.getSelection(true);
            const index = range ? range.index : quill.getLength();
            quill.insertEmbed(index, 'video', url, 'user');
            quill.setSelection(index + 1, 0);
        };

        window.insertTableForQuill = (containerId, rows, cols) => {
            const container = document.getElementById(containerId);
            if (!container || !container.__quill) return;

            const quill = container.__quill;
            const r = Math.max(1, Math.min(12, Number(rows || 3)));
            const c = Math.max(1, Math.min(8, Number(cols || 3)));

            let html = '<table><tbody>';
            for (let i = 0; i < r; i++) {
                html += '<tr>';
                for (let j = 0; j < c; j++) {
                    html += '<td><br></td>';
                }
                html += '</tr>';
            }
            html += '</tbody></table><p><br></p>';

            const range = quill.getSelection(true);
            const index = range ? range.index : quill.getLength();
            quill.clipboard.dangerouslyPasteHTML(index, html);
            quill.setSelection(index + 1, 0);
        };

        window.promptAndInsertVideoLink = (containerId) => {
            const url = prompt('لینک ویدیو را وارد کنید (mp4/webm یا embed)');
            if (!url) return;
            window.insertVideoAtCursor(containerId, url);
        };

        window.promptAndInsertTable = (containerId) => {
            const rows = prompt('تعداد سطر جدول؟', '3');
            if (rows === null) return;
            const cols = prompt('تعداد ستون جدول؟', '3');
            if (cols === null) return;
            window.insertTableForQuill(containerId, rows, cols);
        };

        window.exitCurrentBlockAndAddParagraph = (containerId) => {
            const container = document.getElementById(containerId);
            if (!container || !container.__quill) return;

            const quill = container.__quill;
            const range = quill.getSelection(true);

            if (range) {
                // Get current format
                const format = quill.getFormat(range);

                // If in a blockquote, move to end and add paragraph
                if (format.blockquote) {
                    // Move to end of current block
                    const currentLine = quill.getLine(range.index)[0];
                    if (currentLine) {
                        const lineIndex = quill.getIndex(currentLine);
                        const lineLength = currentLine.length();
                        quill.setSelection(lineIndex + lineLength, 0);
                    }
                }

                // Add empty paragraph
                quill.clipboard.dangerouslyPasteHTML(quill.getSelection().index, '<p><br></p>');
                quill.setSelection(quill.getSelection().index + 1, 0);

                // Remove blockquote format from new line
                quill.format('blockquote', false);
            }
        };

        window.resizeImage = (containerId, width) => {
            const container = document.getElementById(containerId);
            if (!container || !container.__quill) return;

            const quill = container.__quill;
            const range = quill.getSelection();

            if (container.__lastImageDom && container.__lastImageDom.tagName === 'IMG') {
                container.__lastImageDom.style.width = width + '%';
                container.__lastImageDom.style.maxWidth = '100%';
                return;
            }

            if (range) {
                const [leaf] = quill.getLeaf(range.index);
                if (leaf && leaf.domNode && leaf.domNode.tagName === 'IMG') {
                    leaf.domNode.style.width = width + '%';
                    leaf.domNode.style.maxWidth = '100%';
                }
            }
        };

        window.resizeImageBytes = async (imageBuffer, contentType, maxBytes) => {
            return new Promise((resolve, reject) => {
                const img = new Image();
                const blob = new Blob([imageBuffer], { type: contentType });
                const url = URL.createObjectURL(blob);

                img.onload = () => {
                    URL.revokeObjectURL(url);

                    const canvas = document.createElement('canvas');
                    let width = img.width;
                    let height = img.height;
                    let quality = 0.9;

                    const resize = () => {
                        canvas.width = width;
                        canvas.height = height;
                        const ctx = canvas.getContext('2d');
                        ctx.drawImage(img, 0, 0, width, height);

                        canvas.toBlob((blob) => {
                            if (!blob) {
                                reject(new Error('Failed to resize image'));
                                return;
                            }

                            if (blob.size <= maxBytes || quality <= 0.1 || width <= 300 || height <= 300) {
                                const reader = new FileReader();
                                reader.onload = () => resolve(new Uint8Array(reader.result));
                                reader.onerror = () => reject(new Error('Failed to read resized image'));
                                reader.readAsArrayBuffer(blob);
                            } else {
                                quality -= 0.1;
                                width = Math.floor(width * 0.9);
                                height = Math.floor(height * 0.9);
                                resize();
                            }
                        }, contentType, quality);
                    };

                    resize();
                };

                img.onerror = () => {
                    URL.revokeObjectURL(url);
                    reject(new Error('Failed to load image'));
                };

                img.src = url;
            });
        };

        window.initTinyMCE = (selector, dotNetHelper, uploadUrl = '/api/trainings/upload') => {
            tinymce.remove(selector);
            tinymce.init({
                selector: selector,
                promotion: false,
                branding: false,
                directionality: 'rtl',
                height: 600,
                menubar: false,
                plugins: 'advlist autolink lists link image charmap preview anchor searchreplace visualblocks code fullscreen insertdatetime media table code help wordcount',
                toolbar: 'undo redo | blocks | styles | bold italic forecolor | alignleft aligncenter alignright alignjustify | bullist numlist outdent indent | link image table | code fullscreen',
                style_formats: [
                    { title: 'پاراگراف معمولی', block: 'p' },
                    { title: 'باکس هایلایت (آبی)', block: 'blockquote', classes: 'article-callout article-callout--info' },
                    { title: 'باکس هشدار (قرمز)', block: 'blockquote', classes: 'article-callout article-callout--danger' },
                    { title: 'نقل قول بزرگ', block: 'blockquote', classes: 'large-quote' }
                ],
                content_style: `
                        body { font-family: Vazirmatn, Tahoma; direction: rtl; text-align: justify; font-size: 16px; line-height: 1.8; color: #334155; padding: 10px; }
                        .article-callout { padding: 20px; border-radius: 12px; margin: 20px 0; border-right: 5px solid #2563eb; background: #f8fafc; font-weight: bold; }
                        .article-callout--danger { border-right-color: #ef4444; background: #fffafa; }
                        .large-quote { font-size: 1.25rem; font-style: italic; color: #475569; border-right: 4px solid #ef4444; padding: 15px 25px; margin: 30px 0; background: #fffcfc; }
                        img { max-width: 100%; height: auto; border-radius: 8px; }
                    `,
                image_title: true,
                automatic_uploads: true,
                file_picker_types: 'image',
                images_upload_handler: (blobInfo, progress) => new Promise((resolve, reject) => {
                    const formData = new FormData();
                    formData.append('file', blobInfo.blob(), blobInfo.filename());

                    fetch(uploadUrl, {
                        method: 'POST',
                        body: formData
                    })
                        .then(response => {
                            if (!response.ok) throw new Error('Upload failed: ' + response.statusText);
                            return response.text();
                        })
                        .then(url => {
                            resolve(url.replace(/"/g, ''));
                        })
                        .catch(error => {
                            reject('Error: ' + error.message);
                        });
                }),
                setup: function (editor) {
                    editor.on('change keyup setcontent blur', function () {
                        dotNetHelper.invokeMethodAsync('UpdateContent', editor.getContent());
                    });
                }
            });
        };

        window.getTinyContent = (selector) => {
            const editor = tinymce.get(selector.startsWith('#') ? selector.substring(1) : selector);
            return editor ? editor.getContent() : "";
        };

        window.__initTinyMCERaw = window.initTinyMCE;
        window.initTinyMCE = function () {
            var args = arguments;
            return window.__ensureGroup('tinymce').then(function () {
                return window.__initTinyMCERaw.apply(null, args);
            });
        };
        window.__getTinyContentRaw = window.getTinyContent;
        window.getTinyContent = function () {
            var args = arguments;
            return window.__ensureGroup('tinymce').then(function () {
                return window.__getTinyContentRaw.apply(null, args);
            });
        };

        window.initPlyr = (selector) => {
            const elements = document.querySelectorAll(selector);
            elements.forEach(element => {
                const source = element.querySelector('source')?.src;
                if (!source) return;

                if (source.endsWith('.ts') || source.endsWith('.m3u8')) {
                    if (Hls.isSupported()) {
                        const hls = new Hls({
                            debug: false,
                            enableWorker: true,
                            lowLatencyMode: true,
                            backBufferLength: 90
                        });
                        hls.loadSource(source);
                        hls.attachMedia(element);
                        hls.on(Hls.Events.MANIFEST_PARSED, function () {
                            new Plyr(element);
                        });
                    } else if (element.canPlayType('application/vnd.apple.mpegurl')) {
                        element.src = source;
                        new Plyr(element);
                    }
                } else {
                    new Plyr(element);
                }
            });
            return true;
        };

        window.__initPlyrRaw = window.initPlyr;
        window.initPlyr = function () {
            var args = arguments;
            return window.__ensureGroup('media').then(function () {
                return window.__initPlyrRaw.apply(null, args);
            });
        };

        window.initLessonLightbox = () => {
            const lb = document.getElementById('lesson-lightbox');
            const img = document.getElementById('lesson-lightbox-img');
            const closeBtn = document.getElementById('lesson-lightbox-close');
            if (!lb || !img || !closeBtn) return true;

            let scale = 1;
            let tx = 0;
            let ty = 0;
            let isPanning = false;
            let panStartX = 0;
            let panStartY = 0;
            let lastTap = 0;
            let pinchStartDist = 0;
            let pinchStartScale = 1;
            let pinchCenter = { x: 0, y: 0 };

            function applyTransform() {
                img.style.transform = `translate(${tx}px, ${ty}px) scale(${scale})`;
            }

            function resetTransform() {
                scale = 1;
                tx = 0;
                ty = 0;
                applyTransform();
            }

            function clampPan() {
                const max = 520 * Math.max(0, scale - 1);
                tx = Math.max(-max, Math.min(max, tx));
                ty = Math.max(-max, Math.min(max, ty));
            }

            function distance(t1, t2) {
                const dx = t1.clientX - t2.clientX;
                const dy = t1.clientY - t2.clientY;
                return Math.sqrt(dx * dx + dy * dy);
            }

            function setScale(nextScale, centerX, centerY) {
                nextScale = Math.max(1, Math.min(4, nextScale));
                if (nextScale === scale) return;

                const rect = img.getBoundingClientRect();
                const cx = centerX - (rect.left + rect.width / 2);
                const cy = centerY - (rect.top + rect.height / 2);
                const ratio = nextScale / scale;

                tx = cx + (tx - cx) * ratio;
                ty = cy + (ty - cy) * ratio;
                scale = nextScale;
                clampPan();
                applyTransform();
            }

            function close() {
                lb.classList.remove('lesson-lightbox--show');
                lb.setAttribute('aria-hidden', 'true');
                img.removeAttribute('src');
                resetTransform();
            }

            closeBtn.onclick = close;
            lb.onclick = (e) => {
                if (e.target === lb) close();
            };

            document.onkeydown = (e) => {
                if (e.key === 'Escape') close();
            };

            document.querySelectorAll('[data-lightbox-src]').forEach(el => {
                if (el.dataset.lightboxBound === '1') return;
                el.dataset.lightboxBound = '1';
                el.addEventListener('click', () => {
                    const src = el.getAttribute('data-lightbox-src');
                    if (!src) return;
                    img.setAttribute('src', src);
                    lb.classList.add('lesson-lightbox--show');
                    lb.setAttribute('aria-hidden', 'false');
                    resetTransform();
                });
            });

            img.addEventListener('touchstart', (e) => {
                if (e.touches.length === 1) {
                    const now = Date.now();
                    const dt = now - lastTap;
                    lastTap = now;

                    // double tap
                    if (dt > 0 && dt < 280) {
                        const t = e.touches[0];
                        setScale(scale === 1 ? 2 : 1, t.clientX, t.clientY);
                        if (scale === 1) {
                            tx = 0;
                            ty = 0;
                            applyTransform();
                        }
                        e.preventDefault();
                        return;
                    }

                    if (scale > 1) {
                        isPanning = true;
                        panStartX = e.touches[0].clientX - tx;
                        panStartY = e.touches[0].clientY - ty;
                        e.preventDefault();
                    }
                }

                if (e.touches.length === 2) {
                    isPanning = false;
                    pinchStartDist = distance(e.touches[0], e.touches[1]);
                    pinchStartScale = scale;
                    pinchCenter = {
                        x: (e.touches[0].clientX + e.touches[1].clientX) / 2,
                        y: (e.touches[0].clientY + e.touches[1].clientY) / 2
                    };
                    e.preventDefault();
                }
            }, { passive: false });

            img.addEventListener('touchmove', (e) => {
                if (e.touches.length === 1 && isPanning && scale > 1) {
                    tx = e.touches[0].clientX - panStartX;
                    ty = e.touches[0].clientY - panStartY;
                    clampPan();
                    applyTransform();
                    e.preventDefault();
                }

                if (e.touches.length === 2) {
                    const dist = distance(e.touches[0], e.touches[1]);
                    const factor = dist / (pinchStartDist || dist);
                    setScale(pinchStartScale * factor, pinchCenter.x, pinchCenter.y);
                    e.preventDefault();
                }
            }, { passive: false });

            img.addEventListener('touchend', () => {
                isPanning = false;
                clampPan();
                applyTransform();
            });

            return true;
        };

        (function () {
            // تابع ساده پیش‌نمایش بدون نیاز به کتابخانه‌های خارجی
            window.openPrintPreview = function (html, title) {
                console.log('openPrintPreview called', title);

                // ایجاد مودال پیش‌نمایش
                var modalHtml = `
                    <div id="printPreviewModal" style="position:fixed;top:0;left:0;right:0;bottom:0;background:rgba(0,0,0,0.85);z-index:99999;display:flex;flex-direction:column;">
                        <div style="background:#fff;padding:12px 20px;display:flex;justify-content:space-between;align-items:center;border-bottom:1px solid #ddd;flex-wrap:wrap;gap:10px;">
                            <h3 style="margin:0;font-size:1rem;color:#333;">${escapeHtml(title)}</h3>
                            <div style="display:flex;gap:10px;flex-wrap:wrap;">
                                <button id="printBtn" style="background:#4f46e5;color:#fff;border:none;padding:8px 16px;border-radius:8px;cursor:pointer;font-weight:bold;">🖨️ چاپ مستقیم</button>
                                <button id="pdfBtn" style="background:#10b981;color:#fff;border:none;padding:8px 16px;border-radius:8px;cursor:pointer;font-weight:bold;">📄 چاپ و ذخیره PDF</button>
                                <button id="closePreviewBtn" style="background:#ef4444;color:#fff;border:none;padding:8px 16px;border-radius:8px;cursor:pointer;">✖️ بستن</button>
                            </div>
                        </div>
                        <div id="previewFrame" style="flex:1;overflow:auto;background:#eef2f7;padding:20px;"></div>
                    </div>
                `;

                document.body.insertAdjacentHTML('beforeend', modalHtml);

                var frameDiv = document.getElementById('previewFrame');
                frameDiv.innerHTML = html;

                // بستن مودال
                var closeBtn = document.getElementById('closePreviewBtn');
                if (closeBtn) {
                    closeBtn.onclick = function () {
                        var modal = document.getElementById('printPreviewModal');
                        if (modal) modal.remove();
                    };
                }

                // چاپ مستقیم (از طریق پنجره جدید)
                var printBtn = document.getElementById('printBtn');
                if (printBtn) {
                    printBtn.onclick = function () {
                        var printWindow = window.open('', '_blank');
                        printWindow.document.write(html);
                        printWindow.document.close();
                        printWindow.print();
                    };
                }

                // ذخیره PDF با استفاده از دیالوگ چاپ مرورگر
                var pdfBtn = document.getElementById('pdfBtn');
                if (pdfBtn) {
                    pdfBtn.onclick = function () {
                        // باز کردن در پنجره جدید و استفاده از قابلیت Save as PDF مرورگر
                        var printWindow = window.open('', '_blank');
                        printWindow.document.write(html);
                        printWindow.document.close();
                        printWindow.print();
                        // در دیالوگ چاپ، کاربر می‌تواند "Save as PDF" را انتخاب کند
                    };
                }

                // بستن با کلیک روی overlay
                var modal = document.getElementById('printPreviewModal');
                if (modal) {
                    modal.onclick = function (e) {
                        if (e.target === this) {
                            this.remove();
                        }
                    };
                }
            };

            // تابع کمکی برای escape کردن HTML
            function escapeHtml(str) {
                if (!str) return '';
                return str.replace(/[&<>]/g, function (m) {
                    if (m === '&') return '&amp;';
                    if (m === '<') return '&lt;';
                    if (m === '>') return '&gt;';
                    return m;
                });
            }

            // تابع برای نصب Google Analytics
            window.loadGoogleAnalytics = function (tag) {
                if (!tag || window._gaLoaded) return;
                window._gaLoaded = true;
                window.dataLayer = window.dataLayer || [];
                function gtag(){dataLayer.push(arguments);}
                gtag('js', new Date());
                gtag('config', tag);
                var s = document.createElement('script');
                s.async = true;
                s.src = 'https://www.googletagmanager.com/gtag/js?id=' + tag;
                document.head.appendChild(s);
            };

            // بررسی وجود تابع بعد از بارگذاری
            window.addEventListener('load', function () {
                console.log('Page loaded, openPrintPreview exists:', typeof window.openPrintPreview === 'function');
            });
        })();
