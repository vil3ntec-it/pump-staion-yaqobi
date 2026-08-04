// سرویس‌ورکر پمپ یعقوبی — پوستهٔ برنامه (این صفحه + آیکون‌ها) را کش می‌کند تا
// برنامه بعد از نصب، هم آنلاین و هم کاملاً آفلاین باز شود. نسخهٔ کش را هر بار
// که APP_VERSION در index.html عوض می‌شود، این‌جا هم عوض کنید تا کش کهنه پاک شود.
const CACHE_NAME = 'pump-yaqobi-shell-v2.9.186';
const APP_SHELL = [
  './',
  './index.html',
  './manifest.json',
  './icons/icon-192.png',
  './icons/icon-512.png',
  './icons/icon-maskable-192.png',
  './icons/icon-maskable-512.png',
  './icons/apple-touch-icon.png',
  './app.html',
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
  // دارایی‌های ثابتِ CDN (فونتِ وزیرمتن + کتابخانه‌های QR/Excel/jsQR/HLS) را برای
  // «اجرای کاملِ آفلاین روی همهٔ سیستم‌ها» کش می‌کنیم: بعد از فقط یک‌بار باز شدنِ
  // آنلاین، دفعه‌های بعد بدون اینترنت هم فونت و این قابلیت‌ها کار می‌کنند (قبلاً
  // کراس‌اوریجین اصلاً کش نمی‌شد و آفلاین همیشه شکست می‌خورد). استریم‌ها/APIها
  // (دوربین، ntfy، سرور شخصی/وب‌سوکت) عمداً کش نمی‌شوند تا حجم کش پر نشود.
  if (url.origin !== self.location.origin) {
    var CDN = /(^|\.)fonts\.googleapis\.com$|(^|\.)fonts\.gstatic\.com$|(^|\.)cdnjs\.cloudflare\.com$/;
    if (CDN.test(url.hostname)) {
      event.respondWith(
        caches.match(req).then(function(cached){
          if (cached) return cached;
          return fetch(req).then(function(res){
            try { var copy = res.clone(); caches.open(CACHE_NAME).then(function(c){ c.put(req, copy); }).catch(function(){}); } catch(e){}
            return res;
          }).catch(function(){ return cached; });   // آفلاین و کش‌نشده → برنامه خودش گارد دارد
        })
      );
    }
    // بقیهٔ مبداهای خارجی دست‌نخورده از شبکه بروند
    return;
  }

  // فایل‌های نصب (downloads/) و کارتِ نسخه (version.json) هرگز کش نمی‌شوند:
  // فایل نصب ده‌ها مگابایت است و کشِ برنامه را پر می‌کند، و version.json باید
  // همیشه تازه خوانده شود وگرنه برنامه نسخهٔ کهنه را «آخرین نسخه» می‌بیند.
  if (/\/downloads\//.test(url.pathname) || /\/version\.json$/.test(url.pathname)) return;

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
          // نسخهٔ تازه در کش نشست — همین حالا به خودِ برنامه خبر بده تا کاربر
          // بتواند با یک زدن آن را بیاورد (قبلاً باید برنامه را می‌بست و باز
          // می‌کرد و اصلاً نمی‌دانست نسخهٔ تازه‌ای آمده است).
          try {
            res.clone().text().then(txt => {
              const m = txt.match(/const\s+APP_VERSION\s*=\s*['"]([0-9.]+)['"]/);
              if (!m) return;
              self.clients.matchAll({ includeUncontrolled: true }).then(cs => {
                cs.forEach(c => { try { c.postMessage({ type: 'app-version', version: m[1] }); } catch (e) {} });
              });
            }).catch(() => {});
          } catch (e) {}
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

// ---------------------------------------------------------------------------
//  پیام‌رسان — نوتیفیکیشن وقتی برنامه بسته یا گوشی قفل است
//
//  سرورِ خانگی (پنل) پیام را با Web Push می‌فرستد و مرورگر همین Service Worker
//  را بیدار می‌کند — حتی اگر برنامه اصلاً باز نباشد. اینجا آن را به یک
//  نوتیفیکیشنِ واقعی تبدیل می‌کنیم.
// ---------------------------------------------------------------------------
self.addEventListener('push', event => {
  let data = {};
  try {
    data = event.data ? event.data.json() : {};
  } catch (e) {
    try { data = { body: event.data.text() }; } catch (e2) { data = {}; }
  }

  const title = data.title || 'پیام تازه';
  const options = {
    body: data.body || '',
    icon: data.icon || './icons/icon-192.png',
    badge: data.badge || './icons/icon-192.png',
    // با tag، پیام‌های یک گفت‌وگو روی هم می‌نشینند و صفحه شلوغ نمی‌شود
    tag: data.tag || 'pump-message',
    renotify: true,
    dir: 'rtl',
    lang: 'fa',
    timestamp: Date.now(),
    data: { chatId: data.chatId || null, messageId: data.messageId || null, url: data.url || './' },
    // روی اندروید، لرزشِ کوتاه تا در جیب هم حس شود
    vibrate: [80, 40, 80]
  };

  event.waitUntil(self.registration.showNotification(title, options));
});

// با زدنِ نوتیفیکیشن: اگر برنامه باز است همان را جلو بیاور، وگرنه بازش کن
self.addEventListener('notificationclick', event => {
  event.notification.close();
  const info = event.notification.data || {};
  const target = new URL(info.url || './', self.location.origin).href;

  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(list => {
      for (const client of list) {
        if ('focus' in client) {
          // به خودِ برنامه بگو کدام گفت‌وگو را باز کند
          try { client.postMessage({ type: 'open-chat', chatId: info.chatId }); } catch (e) {}
          return client.focus();
        }
      }
      if (self.clients.openWindow) return self.clients.openWindow(target);
      return undefined;
    })
  );
});

// اگر مرورگر اشتراک را تازه کرد، سرور باید اشتراکِ تازه را بداند
self.addEventListener('pushsubscriptionchange', event => {
  event.waitUntil(
    self.registration.pushManager
      .subscribe({ userVisibleOnly: true, applicationServerKey: event.oldSubscription && event.oldSubscription.options.applicationServerKey })
      .then(sub => self.clients.matchAll({ includeUncontrolled: true }).then(list => {
        for (const client of list) {
          try { client.postMessage({ type: 'push-resubscribed', subscription: sub.toJSON() }); } catch (e) {}
        }
      }))
      .catch(() => {})
  );
});
