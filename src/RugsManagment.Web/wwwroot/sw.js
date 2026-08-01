/*
 * Service Worker محافظه‌کار برای PWA سامانهٔ مدیریت فرش.
 * راهبرد:
 *   • دارایی‌های ساکن (/dist, /js, آیکون) → cache-first (سریع + آفلاین)
 *   • ناوبری صفحات و API → network-only (چون به کوکی احراز هویت و دادهٔ زنده وابسته‌اند)
 *     اگر صفحه‌ای آفلاین باز شود، صفحهٔ offline نمایش داده می‌شود.
 * دادهٔ حساس/احرازشده هرگز کش نمی‌شود.
 */
// ⚠ با هر تغییر در PRECACHE (یا در فایل‌هایی که آنجا فهرست شده‌اند، مثل manifest)
// این شماره را یک واحد بالا ببرید؛ وگرنه مرورگرِ کاربرانی که PWA را نصب کرده‌اند
// تا ابد نسخهٔ قدیمی را از کش سرو می‌کند. activate کش‌های قدیمی را پاک می‌کند.
const CACHE = 'rugs-static-v3';
// اسپرایت آیکون هم پیش‌کش می‌شود: بدون آن، صفحهٔ آفلاین و نشانگر صف بی‌آیکون می‌مانند
const PRECACHE = ['/icon.svg', '/icons.svg', '/manifest.webmanifest', '/offline.html'];

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
  return url.pathname.startsWith('/dist/') || url.pathname.startsWith('/js/') ||
    url.pathname === '/icon.svg' || url.pathname === '/icons.svg' ||
    url.pathname === '/favicon.ico' || url.pathname.startsWith('/icons/');
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
