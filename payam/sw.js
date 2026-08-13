// ---------------------------------------------------------------------------
//  سرویس‌ورکرِ «پیام‌رسان یعقوبی»
//
//  این برنامه از خودِ برنامهٔ حساب‌ها جداست و دامنهٔ (scope) خودش را دارد
//  (/payam/)، پس سرویس‌ورکرِ آن با سرویس‌ورکرِ برنامهٔ اصلی قاطی نمی‌شود و هر کدام
//  اشتراکِ پوشِ مستقلِ خودشان را دارند.
//
//  کارش دو چیز است:
//    ۱) پوستهٔ برنامه را کش می‌کند تا آفلاین هم باز شود.
//    ۲) وقتی سرورِ خانگی پیامی می‌فرستد، حتی با برنامهٔ بسته و گوشیِ قفل،
//       نوتیفیکیشنِ واقعی نشان می‌دهد.
// ---------------------------------------------------------------------------
const CACHE_NAME = 'payam-yaqobi-v2.9.298';
const SHELL = [
  './',
  './index.html',
  './manifest.json',
  '../icons/icon-192.png',
  '../icons/icon-512.png'
];

self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then(cache => Promise.all(SHELL.map(url =>
        cache.add(new Request(url, { cache: 'reload' })).catch(() => cache.add(url).catch(() => {}))
      )))
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
  if (url.origin !== self.location.origin) return;      // سرورِ خانگی هرگز کش نمی‌شود
  if (/\/api\//.test(url.pathname)) return;

  // صفحهٔ برنامه: اول شبکه (تا تازه‌ترین نسخه بیاید)، اگر نشد از کش
  if (req.mode === 'navigate') {
    event.respondWith(
      fetch(req).then(res => {
        try { const copy = res.clone(); caches.open(CACHE_NAME).then(c => c.put(req, copy)); } catch (e) {}
        return res;
      }).catch(() => caches.match(req).then(c => c || caches.match('./index.html')))
    );
    return;
  }
  event.respondWith(
    caches.match(req).then(cached => cached || fetch(req).then(res => {
      try { const copy = res.clone(); caches.open(CACHE_NAME).then(c => c.put(req, copy)); } catch (e) {}
      return res;
    }))
  );
});

// ---------------------------------------------------------------------------
//  پیامِ تازه از سرورِ خانگی
//
//  اگر پنجره‌ای باز و «دیده‌شده» باشد، پیام به خودِ صفحه می‌رود (صفحه خودش نشانش
//  می‌دهد و اعلانِ تکراری روی نوارِ گوشی نمی‌نشیند). در هر حالتِ دیگری — بسته،
//  پشتِ زمینه، گوشیِ قفل — نوتیفیکیشنِ واقعی نشان داده می‌شود.
//  نکتهٔ مهم: اشتراک با userVisibleOnly گرفته شده، پس اگر پیامی برسد و هیچ
//  اعلانی نشان داده نشود، مرورگر بعد از چند بار اشتراک را باطل می‌کند.
// ---------------------------------------------------------------------------
self.addEventListener('push', event => {
  let data = {};
  try { data = event.data ? event.data.json() : {}; }
  catch (e) { try { data = { body: event.data.text() }; } catch (e2) { data = {}; } }

  const title = data.title || 'پیام تازه — پمپ یعقوبی';
  const options = {
    body: data.body || '',
    icon: '../icons/icon-192.png',
    badge: '../icons/icon-192.png',
    tag: data.tag || 'payam',
    renotify: true,
    dir: 'rtl',
    lang: 'fa',
    timestamp: Date.now(),
    data: { url: data.url || './' },
    vibrate: [90, 50, 90]
  };

  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(list => {
      const visible = (list || []).filter(c => c.visibilityState === 'visible');
      if (visible.length) {
        visible.forEach(c => { try { c.postMessage({ type: 'push-message', title: title, body: options.body }); } catch (e) {} });
        return undefined;
      }
      return self.registration.showNotification(title, options);
    }).catch(() => self.registration.showNotification(title, options))
  );
});

self.addEventListener('notificationclick', event => {
  event.notification.close();
  const info = event.notification.data || {};
  const target = new URL(info.url || './', self.location.href).href;
  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(list => {
      for (const client of list) {
        if ('focus' in client) return client.focus();
      }
      if (self.clients.openWindow) return self.clients.openWindow(target);
      return undefined;
    })
  );
});

// اگر مرورگر اشتراک را عوض کرد، به صفحه خبر بده تا دوباره نزدِ سرور ثبتش کند
self.addEventListener('pushsubscriptionchange', event => {
  event.waitUntil(
    self.clients.matchAll({ includeUncontrolled: true }).then(list => {
      list.forEach(c => { try { c.postMessage({ type: 'push-resubscribe' }); } catch (e) {} });
    }).catch(() => {})
  );
});
