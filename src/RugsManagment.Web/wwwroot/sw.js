/*
 * Service Worker محافظه‌کار برای PWA سامانهٔ مدیریت فرش.
 * راهبرد:
 *   • دارایی‌های ساکن (/dist، /js، /icons، /fonts، آیکون‌ها) → cache-first (سریع + آفلاین)
 *   • ناوبری صفحات و API → network-only (چون به کوکی احراز هویت و دادهٔ زنده وابسته‌اند)
 *     اگر صفحه‌ای آفلاین باز شود، صفحهٔ offline نمایش داده می‌شود.
 * دادهٔ حساس/احرازشده هرگز کش نمی‌شود.
 */

// نسخهٔ کش. هر بار محتوای PRECACHE (یا خودِ فایل‌های داخل آن) عوض شود این عدد باید بالا برود؛
// وگرنه نصب‌های قبلی همان نسخهٔ قدیمی را از کش برمی‌دارند و هرگز به‌روز نمی‌شوند
// (رویداد activate فقط کش‌هایی را پاک می‌کند که نامشان با این مقدار فرق دارد).
const CACHE = 'rugs-static-v6';

// اسپرایت آیکون هم پیش‌کش می‌شود: بدون آن، صفحهٔ آفلاین و نشانگر صف بی‌آیکون می‌مانند.
// هر چه offline.html می‌خواهد (فونت Vazirmatn) هم این‌جاست: آن صفحه وقتی نشان داده می‌شود که شبکه نیست، پس
// هر نشانی‌ای که در آن است باید از همین فهرست بیاید (آزمون P1FixSourceTests.OfflinePage_EveryLocalUrl_IsPrecached همین را می‌سنجد).
const PRECACHE = [
  '/icon.svg',
  '/icons.svg',
  '/icons/icon-192.png',
  '/icons/icon-512.png',
  '/manifest.webmanifest',
  '/offline.html',
  '/fonts/Vazirmatn-Variable.woff2',
];

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

// /fonts/: فونت‌های خودمیزبان (Vazirmatn) — هش ندارند ولی عملاً هرگز عوض نمی‌شوند؛ بدون کش، صفحهٔ آفلاین و
// چاپ بدون فونت فارسی می‌ماندند
function isStaticAsset(url) {
  return url.pathname.startsWith('/dist/') || url.pathname.startsWith('/icons/') || url.pathname.startsWith('/fonts/') ||
    url.pathname === '/icon.svg' || url.pathname === '/icons.svg' ||
    url.pathname === '/favicon.ico' || url.pathname.startsWith('/js/');
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
          // فقط پاسخ سالم کش می‌شود؛ یک ۴۰۴ کش‌شده تا نسخهٔ بعدی کش ماندگار می‌ماند
          if (res.ok) {
            const copy = res.clone();
            caches.open(CACHE).then((c) => c.put(request, copy));
          }
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
