const CACHE_NAME = 'errorservice-cache-v1.0.9';
const urlsToCache = [
    './',
    './index.html',
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

    // Network-first for the Blazor manifest and navigations, so a stale
    // service-worker cache can never serve files deleted by a rebuild
    if (url.pathname.includes('blazor.boot.json') || event.request.mode === 'navigate')
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

    event.respondWith(
        caches.match(event.request)
            .then(response => {
                if (response) return response;
                return fetch(event.request).catch(() => {
                    if (event.request.mode === 'navigate')
                        return caches.match('./index.html');
                    return new Response('', { status: 404 });
                });
            })
    );
});

self.addEventListener('message', event => {
    if (event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});
