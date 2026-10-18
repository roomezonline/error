const CACHE_NAME = 'errorservice-cache-v1.7.0';
const urlsToCache = [
    './',
    './index.html',
    './js/inline.js',
    './manifest.json',
    './css/app.css',
    './css/theme.css',
    './css/bootstrap/bootstrap.min.css',
    './icon-192.png',
    './icon-512.png',
    './releases.json'
];

self.addEventListener('install', event => {
    self.skipWaiting();
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => Promise.all(
                urlsToCache.map(url =>
                    cache.add(url).catch(err => {
                        console.warn('[SW] precache failed, skipping:', url, err);
                    })
                )
            ))
    );
});

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys().then(cacheNames => {
            return Promise.all(
                cacheNames.map(cacheName => {
                    if (cacheName !== CACHE_NAME) {
                        return caches.delete(cacheName);
                    }
                })
            );
        }).then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', event => {
    const url = new URL(event.request.url);

    if (url.pathname.endsWith('/releases.json'))
    {
        event.respondWith(
            fetch(event.request)
                .then(networkResponse => {
                    if (networkResponse && networkResponse.ok)
                    {
                        const responseClone = networkResponse.clone();
                        // Normalize cache key by stripping query string for releases.json
                        const cacheKey = new Request('./releases.json', { method: 'GET' });
                        caches.open(CACHE_NAME).then(cache => cache.put(cacheKey, responseClone));
                    }
                    return networkResponse;
                })
                .catch(() => {
                    // Try to match normalized cache key (without query string)
                    const cacheKey = new Request('./releases.json', { method: 'GET' });
                    return caches.match(cacheKey);
                })
        );
        return;
    }

    // Network-first for the Blazor manifest, framework DLLs, and navigations,
    // so a stale service-worker cache can never serve files from an old build
    if (url.pathname.includes('blazor.boot.json') || url.pathname.includes('/_framework/') || event.request.mode === 'navigate')
    {
        event.respondWith(
            fetch(event.request)
                .then(networkResponse => {
                    if (networkResponse && networkResponse.ok)
                    {
                        const responseClone = networkResponse.clone();
                        caches.open(CACHE_NAME).then(cache => cache.put(event.request, responseClone));
                    }
                    return networkResponse;
                })
                .catch(() => {
                    if (event.request.mode === 'navigate')
                        return caches.match('./index.html');
                    return caches.match(event.request);
                })
        );
        return;
    }

    // Binary assets only (fonts/images/media): stale-while-revalidate.
    // Deliberately NOT js/css/json/txt: those are un-versioned build output —
    // serving them stale-first broke the app right after a deploy (old
    // monitoring-details-overlay.js was served from SW cache). They now go
    // straight to the network with no-cache revalidation (set app-side).
    const STATIC_EXT = /\.(woff2?|ttf|otf|eot|png|jpe?g|webp|gif|svg|ico|avif|mp3|m4a|wav|mp4|webm)$/i;
    const isStaticAsset = url.origin === self.location.origin
        && event.request.method === 'GET'
        && !event.request.headers.has('range')
        && !url.pathname.startsWith('/api/')
        && !url.pathname.startsWith('/_blazor')
        && !url.pathname.startsWith('/hubs/')
        && !url.pathname.startsWith('/_framework/')
        && STATIC_EXT.test(url.pathname);

    if (isStaticAsset) {
        const cacheOpen = caches.open(CACHE_NAME);
        const networkPromise = fetch(event.request).then(networkResponse => {
            if (networkResponse && networkResponse.status === 200) {
                const responseClone = networkResponse.clone();
                return cacheOpen
                    .then(cache => cache.put(event.request, responseClone))
                    .catch(() => { })
                    .then(() => networkResponse);
            }
            return networkResponse;
        });
        event.waitUntil(networkPromise.catch(() => { }));
        event.respondWith(
            cacheOpen
                .then(cache => cache.match(event.request).then(cached => cached || networkPromise))
                .catch(() => networkPromise)
        );
        return;
    }

    // API, hubs and anything else: not intercepted, never cached by the SW
    return;
});

self.addEventListener('message', event => {
    if (event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});
