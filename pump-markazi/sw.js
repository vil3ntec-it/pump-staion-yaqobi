// سرویس‌ورکر پمپ بنزین مرکزی — پوستهٔ برنامه (این صفحه + آیکون‌ها) را کش می‌کند تا
// برنامه بعد از نصب، هم آنلاین و هم کاملاً آفلاین باز شود. نسخهٔ کش را هر بار
// که APP_VERSION در index.html عوض می‌شود، این‌جا هم عوض کنید تا کش کهنه پاک شود.
const CACHE_NAME = 'pump-markazi-shell-v2.9.57';
const APP_SHELL = [
  './',
  './index.html',
  './manifest.json',
  './icons/icon-192.png',
  './icons/icon-512.png',
  './icons/icon-maskable-192.png',
  './icons/icon-maskable-512.png',
  './icons/apple-touch-icon.png',
  './favicon.png'
];

self.addEventListener('install', event => {
  // cache.addAll همه‌یا‌هیچ است — اگر فقط یکی از آیکون‌ها هنگام نصب (روی نت
  // ضعیف) نگیرد، کل کش خالی می‌ماند و صفحهٔ اصلی هرگز کش نمی‌شود؛ نتیجه‌اش
  // این بود که هر بار باز شدن از آیکون، دوباره منتظرِ شبکه می‌ماند. با add
  // جدا برای هر فایل، شکستِ یکی مانع کش‌شدنِ بقیه (به‌خصوص خودِ index.html) نمی‌شود.
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then(cache => Promise.all(APP_SHELL.map(url => cache.add(url).catch(() => {}))))
      .then(() => self.skipWaiting())
      .catch(() => {})
  );
});

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys()
      .then(keys => Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', event => {
  const req = event.request;
  if (req.method !== 'GET') return;
  const url = new URL(req.url);
  // کتابخانه‌های خارجی (فونت/فایربیس/کیوآر/اکسل) دست‌نخورده از شبکه بروند —
  // برنامه از قبل برای نبود آن‌ها گارد دارد (بخش‌های مربوطه فقط غیرفعال می‌شوند)
  if (url.origin !== self.location.origin) return;

  const isAppShellPage = req.mode === 'navigate' || url.pathname.endsWith('/index.html') || url.pathname.endsWith('/');
  if (isAppShellPage) {
    // صفحهٔ اصلی: «کهنه ولی فوری» — اگر نسخه‌ای در کش هست همان را بی‌درنگ نشان
    // بده (این صفحه ~1MB است؛ منتظرِ شبکه ماندن باعث می‌شد قفلِ صفحه چند ثانیه
    // دیر باز شود) و هم‌زمان در پس‌زمینه از شبکه یک نسخهٔ تازه می‌گیریم و کش را
    // به‌روز می‌کنیم — دفعهٔ بعد که باز شود، تازه‌ترین نسخه همان‌جاست.
    // cache:'reload' لازم است تا خودِ این fetch پس‌زمینه از کشِ HTTP مرورگر
    // به‌جای شبکهٔ واقعی جواب نگیرد.
    event.respondWith(
      caches.match(req).then(cached => {
        // بدون AbortController، fetch روی شبکهٔ کند/قطع می‌توانست دقیقه‌ها معلق
        // بماند و باز شدنِ برنامه از آیکون را همان‌قدر عقب بیندازد — وقتی کش
        // خالی است (اولین نصب یا کش پاک‌شده)، این تنها راه رسیدن به صفحه بود
        const ctrl = new AbortController();
        const timeout = setTimeout(() => ctrl.abort(), 4000);
        const networkUpdate = fetch(req.url, { cache: 'reload', signal: ctrl.signal }).then(res => {
          clearTimeout(timeout);
          const copy = res.clone();
          caches.open(CACHE_NAME).then(cache => cache.put(req, copy)).catch(() => {});
          return res;
        }).catch(() => { clearTimeout(timeout); return null; });
        if (cached) return cached; // فوری — networkUpdate در پس‌زمینه ادامه دارد
        return networkUpdate.then(res => res || caches.match('./index.html'));
      })
    );
    return;
  }

  // بقیهٔ فایل‌های محلی (آیکون‌ها/مانیفست): اول کش، بعد شبکه
  event.respondWith(
    caches.match(req).then(cached => cached || fetch(req).then(res => {
      const copy = res.clone();
      caches.open(CACHE_NAME).then(cache => cache.put(req, copy)).catch(() => {});
      return res;
    }))
  );
});
