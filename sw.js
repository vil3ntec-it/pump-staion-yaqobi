// سرویس‌ورکر پمپ یعقوبی — پوستهٔ برنامه (این صفحه + آیکون‌ها) را کش می‌کند تا
// برنامه بعد از نصب، هم آنلاین و هم کاملاً آفلاین باز شود. نسخهٔ کش را هر بار
// که APP_VERSION در index.html عوض می‌شود، این‌جا هم عوض کنید تا کش کهنه پاک شود.
const CACHE_NAME = 'pump-yaqobi-shell-v2.9.343';
/* کشِ دارایی‌های سنگین و بی‌تغییر (فونتِ صفحه‌های چاپ/PDF). عمداً نسخه ندارد: با
   هر نسخهٔ تازهٔ برنامه پاک نمی‌شود، پس یک‌بار گرفته می‌شود و برای همیشه می‌ماند —
   حتی برای چاپِ آفلاین. */
const ASSET_CACHE = 'pump-yaqobi-assets-v1';
const ASSETS = ['./assets/pdf-font.css'];
/* فقط چیزهایی که برای «باز شدنِ صفحه» لازم‌اند. عمداً کوتاه است — هر فایلی که
   این‌جا بیاید، هنگامِ نصبِ سرویس‌ورکر همزمان با خودِ صفحه دانلود می‌شود و پهنای
   باند را از دستِ همان صفحه‌ای می‌گیرد که کاربر منتظرش است. */
const APP_SHELL = [
  './',
  './index.html',
  './manifest.json'
];
/* اینها برای دیده شدنِ صفحه لازم نیستند (آیکون‌های نصب ۷۰۵ کیلوبایت‌اند و فونتِ
   چاپ ۲۱۳ کیلوبایت). با تاخیر و بعد از راه افتادنِ برنامه گرفته می‌شوند تا
   «آفلاین کامل» هم داشته باشیم بی‌آنکه بارِ اول کند شود. */
const LAZY_SHELL = [
  './icons/icon-192.png',
  './icons/icon-512.png',
  './icons/icon-maskable-192.png',
  './icons/icon-maskable-512.png',
  './icons/apple-touch-icon.png',
  './icons/badge-96.png',
  './app.html',
  './favicon.png'
];

self.addEventListener('install', event => {
  // cache.addAll همه‌یا‌هیچ است — اگر فقط یکی از آیکون‌ها هنگام نصب (روی نت
  // ضعیف) نگیرد، کل کش خالی می‌ماند و صفحهٔ اصلی هرگز کش نمی‌شود؛ نتیجه‌اش
  // این بود که هر بار باز شدن از آیکون، دوباره منتظرِ شبکه می‌ماند. با add
  // جدا برای هر فایل، شکستِ یکی مانع کش‌شدنِ بقیه (به‌خصوص خودِ index.html) نمی‌شود.
  /* ── چرا cache:'reload' برداشته شد ──
     با آن پرچم، سرویس‌ورکر index.html را از خودِ شبکه می‌گرفت و کشِ HTTP را دور
     می‌زد. یعنی در بارِ اول، مرورگر یک‌بار ۱٫۲ مگابایت را برای نشان دادنِ صفحه
     می‌گرفت و سرویس‌ورکر همان لحظه همان ۱٫۲ مگابایت را دوباره — روی ۳G یعنی
     دو برابر شدنِ زمانِ انتظار، برای فایلی که همین حالا در دست است.
     حالا از کشِ HTTP خوانده می‌شود (تقریباً رایگان): یعنی دقیقاً همان نسخه‌ای
     که کاربر همین الان دارد اجرا می‌کند در کش می‌نشیند — که درست هم همین است.
     تازه‌ماندن به‌هم نمی‌خورد: در هر باز شدن، هندلرِ پایین نسخهٔ تازه را در
     پس‌زمینه می‌گیرد و با پیامِ app-version به برنامه خبر می‌دهد. */
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then(cache => Promise.all(APP_SHELL.map(url => cache.add(url).catch(() => {}))))
      .then(() => self.skipWaiting())
      .catch(() => {})
  );
});

/** آیکون‌ها و فونتِ چاپ — با تاخیر، تا با بارِ اولِ صفحه رقابت نکنند */
function _prefetchLazy() {
  return caches.open(ASSET_CACHE)
    .then(cache => Promise.all(ASSETS.map(url =>
      // اگر از قبل هست دوباره گرفته نمی‌شود — این کش با نسخه‌ها پاک نمی‌شود
      cache.match(url).then(hit => hit || cache.add(url).catch(() => {}))
    )))
    .then(() => caches.open(CACHE_NAME))
    .then(cache => Promise.all(LAZY_SHELL.map(url =>
      cache.match(url).then(hit => hit || cache.add(url).catch(() => {}))
    )))
    .catch(() => {});
}

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys()
      // کشِ دارایی‌ها عمداً نگه داشته می‌شود — پاک کردنش یعنی گرفتنِ دوبارهٔ
      // فونتِ چاپ با هر نسخهٔ تازه، بی‌آنکه چیزی از آن عوض شده باشد.
      .then(keys => Promise.all(keys
        .filter(k => k !== CACHE_NAME && k !== ASSET_CACHE)
        .map(k => caches.delete(k))))
      .then(() => self.clients.claim())
  );
  /* ⚠️ اینجا هیچ تاخیری نگذارید: تا وقتی waitUntilِ activate تمام نشود
     سرویس‌ورکر فعال نمی‌شود و صفحه منتظر می‌ماند. یک‌بار همین اشتباه شد و
     بازِ دوم را از ۰٫۷ ثانیه به ۱۱ ثانیه رساند. پیش‌گرفتنِ آیکون‌ها از
     اولین fetch راه می‌افتد (پایین) — آن‌جا جواب را عقب نمی‌اندازد. */
});

/* آیکون‌ها یک‌بار در هر عمرِ سرویس‌ورکر و با تاخیر گرفته می‌شوند. waitUntil روی
   رویدادِ fetch فقط سرویس‌ورکر را زنده نگه می‌دارد و جوابِ صفحه را کند نمی‌کند. */
let _lazyStarted = false;
function _maybeLazy(event) {
  if (_lazyStarted) return;
  _lazyStarted = true;
  try { event.waitUntil(new Promise(r => setTimeout(r, 10000)).then(_prefetchLazy)); } catch (e) {}
}

self.addEventListener('fetch', event => {
  const req = event.request;
  if (req.method !== 'GET') return;
  const url = new URL(req.url);
  // دارایی‌های ثابتِ CDN (فونتِ وزیرمتن + کتابخانه‌های QR/Excel/jsQR/HLS) را برای
  // «اجرای کاملِ آفلاین روی همهٔ سیستم‌ها» کش می‌کنیم: بعد از فقط یک‌بار باز شدنِ
  // آنلاین، دفعه‌های بعد بدون اینترنت هم فونت و این قابلیت‌ها کار می‌کنند (قبلاً
  // کراس‌اوریجین اصلاً کش نمی‌شد و آفلاین همیشه شکست می‌خورد). استریم‌ها/APIها
  // (دوربین، سرور شخصی/وب‌سوکت/پیام‌رسان) عمداً کش نمی‌شوند تا حجم کش پر نشود.
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

  // «پیام‌رسان یعقوبی» (/payam/) برنامهٔ جداگانه‌ای با سرویس‌ورکر و کشِ خودش است.
  // بدون این خط، تا پیش از ثبتِ سرویس‌ورکرِ خودش، درخواستِ بازِ آن به این‌جا
  // می‌افتاد و در حالتِ آفلاین به‌جای پیام‌رسان، صفحهٔ اصلیِ برنامه سرو می‌شد.
  if (/\/payam\//.test(url.pathname)) return;

  /* فونت‌ها: همیشه اول از کشِ بی‌نسخه. این فایل‌ها هرگز عوض نمی‌شوند، پس هیچ
     دلیلی برای پرسیدنِ دوباره از شبکه نیست — نه با نسخهٔ تازه، نه با رفرش. */
  if (/\/assets\//.test(url.pathname)) {
    event.respondWith(
      caches.open(ASSET_CACHE).then(cache => cache.match(req).then(hit => hit || fetch(req).then(res => {
        try { const copy = res.clone(); cache.put(req, copy); } catch (e) {}
        return res;
      })))
    );
    return;
  }

  const isAppShellPage = req.mode === 'navigate' || url.pathname.endsWith('/index.html') || url.pathname.endsWith('/');
  if (isAppShellPage) {
    _maybeLazy(event);   // آیکون‌ها را ۱۰ ثانیه بعد، وقتی صفحه راه افتاده، بگیر
    /* صفحهٔ اصلی — «اول کش، به‌روزرسانی در پس‌زمینه».
       ── چرا دوباره عوض شد ──
       پیش از این، شبکه و کش مسابقه می‌دادند و شبکه ۲۵۰۰ میلی‌ثانیه فرصت داشت.
       روی اینترنتِ کند نتیجه‌اش این بود که کاربر هر بار تا ۲٫۵ ثانیه به یک صفحهٔ
       سفید نگاه می‌کرد — با اینکه نسخهٔ سالم همان لحظه در کش بود. اندازه‌گیری
       روی ۳G: ۶ ثانیه تا دیده شدن و ۱۶ ثانیه تا آماده شدن.
       ── حالا ──
       اگر نسخه‌ای در کش باشد همان لحظه سرو می‌شود (بازِ آنی، حتی بی‌اینترنت) و
       دانلودِ نسخهٔ تازه در پس‌زمینه ادامه پیدا می‌کند.
       نگرانیِ «کاربر یک نسخه عقب می‌ماند» دیگر موضوعیت ندارد: به‌محضِ نشستنِ
       نسخهٔ تازه در کش، همین‌جا پیامِ app-version به برنامه می‌رود و خودِ برنامه
       (_pumpOfferUpdate) یا بی‌سروصدا سوارش می‌کند یا نوارِ «نسخهٔ تازه آمد» را
       نشان می‌دهد — بدون اینکه کسی منتظرِ شبکه بماند.
       cache:'reload' لازم است تا این fetch از کشِ HTTP مرورگر جواب نگیرد. */
    event.respondWith(
      caches.match(req).then(cached => {
        // بدون AbortController، fetch روی شبکهٔ کند/قطع می‌توانست دقیقه‌ها معلق
        // بماند. این مهلت فقط تا رسیدنِ سرآیندهاست؛ بعد از آن دانلودِ بدنه با
        // خیال راحت تا آخر ادامه پیدا می‌کند (clearTimeout پایین).
        const ctrl = new AbortController();
        const timeout = setTimeout(() => ctrl.abort(), 15000);
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

        if (!cached) return networkUpdate.then(res => res || caches.match('./index.html'));

        // کش هست → همین حالا سرو شود. دانلودِ نسخهٔ تازه در پس‌زمینه ادامه
        // می‌یابد (waitUntil تا تمام شدنش سرویس‌ورکر را زنده نگه می‌دارد).
        event.waitUntil(networkUpdate);
        return cached;
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

  /* نامِ برنامه همیشه در عنوان باشد: روی نوارِ اعلانِ اندروید بالای پیام فقط
     «Chrome • yaqobipump.top» نوشته می‌شود و کاربر نمی‌فهمد این اعلان از کدام
     برنامه است. با این پیشوند، خطِ اولِ خودِ اعلان می‌گوید «پمپ یعقوبی». */
  const APP = 'پمپ یعقوبی';
  let title = data.title || 'پیام تازه';
  if (title.indexOf(APP) < 0) title = APP + ' · ' + title;
  const options = {
    body: data.body || '',
    icon: data.icon || './icons/icon-192.png',
    // آیکونِ کوچکِ اعلان: اندروید فقط آلفای این تصویر را می‌خواند. با تصویرِ رنگیِ
    // پُر (icon-192) یک مربعِ سفیدِ خالی نشان می‌داد و معلوم نبود اعلان از کدام
    // برنامه است؛ badge-96 یک سیلوئتِ تک‌رنگِ پمپ است و درست دیده می‌شود.
    badge: data.badge || './icons/badge-96.png',
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

  /* کسی که همین حالا داخلِ برنامه است و صفحه را می‌بیند، نباید روی نوارِ اعلانِ
     گوشی هم پیام بگیرد — برای او خودِ صفحه یک توستِ کوتاه نشان می‌دهد.
     ۱) پنجره‌ای باز و «دیده‌شده» هست → اعلانِ سیستمی نمی‌آید، پیام به صفحه می‌رود.
     ۲) هر حالتِ دیگری (پنجره بسته، پشتِ زمینه، گوشی قفل، تبِ پنهان) → اعلانِ
        سیستمی همین‌جا نشان داده می‌شود.

     ── باگی که این‌جا بود (چرا هیچ اعلانی نمی‌آمد) ──────────────────────────
     قبلاً اگر «هر» پنجره‌ای باز بود — حتی پنهان و پشتِ زمینه — این‌جا هیچ اعلانی
     نشان داده نمی‌شد، با این حساب که «خودِ صفحه خبر می‌دهد». ولی خودِ صفحه هم
     دقیقاً برعکس فکر می‌کرد: چون پوشِ دستگاه روشن بود، اعلان را به سرویس‌ورکر
     واگذار می‌کرد. نتیجه: هیچ‌کدام اعلان نمی‌دادند و کاربر روی گوشی و کامپیوتر
     هیچ خبری نمی‌گرفت. حالا سرویس‌ورکر فقط وقتی ساکت می‌ماند که واقعاً یک پنجرهٔ
     «دیده‌شده» جلوی چشمِ کاربر باشد.
     نکتهٔ فنی: اشتراکِ پوش با userVisibleOnly گرفته شده؛ اگر پیام برسد و اعلانی
     نشان داده نشود، مرورگر خودش پیامِ «این سایت در پس‌زمینه به‌روز شد» را نشان
     می‌دهد و بعد از چند بار، اشتراک را باطل می‌کند. پس نشان دادنِ اعلان اجباری
     است، نه سلیقه‌ای. */
  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(list => {
      const visible = (list || []).filter(c => c.visibilityState === 'visible');
      if (visible.length) {
        visible.forEach(c => { try { c.postMessage({ type: 'push-message', title: title, body: options.body, data: options.data }); } catch (e) {} });
        return undefined;
      }
      return self.registration.showNotification(title, options);
    }).catch(() => self.registration.showNotification(title, options))
  );
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
