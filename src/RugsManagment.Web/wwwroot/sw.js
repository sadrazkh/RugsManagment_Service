/*
 * Service Worker محافظه‌کار برای PWA سامانهٔ مدیریت فرش.
 * راهبرد:
 *   • دارایی‌های ساکن (/dist, /lib, آیکون، فونت) → cache-first (سریع + آفلاین)
 *   • ناوبری صفحات و API → network-only (چون به کوکی احراز هویت و دادهٔ زنده وابسته‌اند)
 *     اگر صفحه‌ای آفلاین باز شود، صفحهٔ offline نمایش داده می‌شود.
 * دادهٔ حساس/احرازشده هرگز کش نمی‌شود.
 */
const CACHE = 'rugs-static-v1';
const PRECACHE = ['/icon.svg', '/manifest.webmanifest', '/offline.html'];

self.addEventListener('install', (event) => {
  event.waitUntil(caches.open(CACHE).then((c) => c.addAll(PRECACHE)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

function isStaticAsset(url) {
  return url.pathname.startsWith('/dist/') || url.pathname.startsWith('/lib/') ||
    url.pathname === '/icon.svg' || url.pathname === '/favicon.ico';
}

self.addEventListener('fetch', (event) => {
  const { request } = event;
  if (request.method !== 'GET') return;

  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return; // فونت/CDN را دست نمی‌زنیم

  if (isStaticAsset(url)) {
    event.respondWith(
      caches.match(request).then((cached) =>
        cached || fetch(request).then((res) => {
          const copy = res.clone();
          caches.open(CACHE).then((c) => c.put(request, copy));
          return res;
        }).catch(() => cached)
      )
    );
    return;
  }

  // ناوبری صفحات: شبکه، با fallback آفلاین
  if (request.mode === 'navigate') {
    event.respondWith(fetch(request).catch(() => caches.match('/offline.html')));
  }
});
