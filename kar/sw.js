/* ══ سرویس‌ورکرِ اپِ کارمندان ═══════════════════════════════════════════════
 *
 * فقط پوستهٔ اپ را نگه می‌دارد (صفحه، کد، مانیفست) تا روی گوشیِ کارمند مثلِ
 * یک برنامهٔ واقعی فوری باز شود و بی‌اینترنت هم بالا بیاید.
 *
 * ⚠️ هیچ داده‌ای این‌جا کش نمی‌شود: خودِ حساب‌ها از وب‌سوکتِ سرورِ خانگی
 * می‌آیند و آخرین عکس در ‎localStorage‎ی خودِ صفحه می‌ماند، نه این‌جا.
 *
 * ⚠️ این سرویس‌ورکر مالِ ‎/kar/‎ است و هیچ ربطی به ‎sw.js‎ی ریشهٔ سایت ندارد.
 * دامنه‌شان (‎scope‎) جداست، پس هیچ‌کدام دیگری را کنار نمی‌زند.
 */
//  ⚠️ با هر بار عوض شدنِ فهرستِ زیر، شمارهٔ CACHE هم باید بالا برود —
//  وگرنه گوشیِ کارمند نسخهٔ قدیمی را نگه می‌دارد و فایلِ تازه هرگز
//  نمی‌رسد. cloud.js اضافه شد و جوابِ درخواستِ ناموفق عوض شد، پس v3؛
//  صفحهٔ «کدِ پمپ» و جداسازیِ هر پمپ، پس v4؛ دو درِ «حساب‌ها/کارمندان»، پس v5؛
//  update.js اضافه شد، پس v6؛ پوشِ خبرها (app.js عوض شد)، پس v9.
var CACHE = 'pump-kar-v10';
var SHELL = ['./', './index.html', './app.js', './cloud.js', './update.js', './manifest.json'];

self.addEventListener('install', function (e) {
  e.waitUntil(caches.open(CACHE).then(function (c) { return c.addAll(SHELL); })
    .then(function () { return self.skipWaiting(); }));
});

self.addEventListener('activate', function (e) {
  e.waitUntil(caches.keys().then(function (keys) {
    return Promise.all(keys.map(function (k) { return k === CACHE ? null : caches.delete(k); }));
  }).then(function () { return self.clients.claim(); }));
});

self.addEventListener('fetch', function (e) {
  var req = e.request;
  if (req.method !== 'GET') return;

  /*
   * ⚠️ درخواستِ بیرون از این دامنه اصلاً دستِ ما نیست.
   *
   * اسکریپتِ ورودِ گوگل و خودِ ابر از دامنهٔ دیگری می‌آیند. اگر این‌جا
   * جوابشان را بسازیم — که تا امروز می‌ساختیم — روی نتِ ضعیف به جای
   * اسکریپت، صفحهٔ HTML دستِ مرورگر می‌رسید و با
   * «Unexpected token '<'» می‌شکست. رهایشان می‌کنیم تا خودِ مرورگر
   * خطای درست را بدهد و اپ بتواند «اینترنت نیست» را بفهمد.
   */
  if (new URL(req.url).origin !== location.origin) return;

  //  ⚠️ version.json هیچ‌وقت از کش نمی‌آید: تنها راهِ فهمیدنِ «نسخهٔ تازه هست»
  //  همین فایل است و کش‌شده‌اش دروغ می‌گوید.
  if (/\/version\.json(\?|$)/.test(req.url)) return;

  // ⚠️ «شبکه اول»: وگرنه نسخهٔ تازهٔ اپ هیچ‌وقت به گوشی نمی‌رسید و کارمند
  // هفته‌ها با نسخهٔ کهنه کار می‌کرد. کش فقط پشتیبانِ قطعیِ شبکه است.
  e.respondWith(
    fetch(req).then(function (res) {
      if (res && res.ok)
        caches.open(CACHE).then(function (c) { c.put(req, res.clone()); });
      return res;
    }).catch(function () {
      return caches.match(req).then(function (hit) {
        if (hit) return hit;
        /*
         * ⚠️ صفحه فقط جوابِ «رفتن به یک صفحه» است، نه جوابِ هر چیزی.
         *
         * پیش از این هر درخواستِ ناموفقی — اسکریپت، عکس، مانیفست —
         * همین صفحه را می‌گرفت. یعنی مرورگر HTML را به جای جاوااسکریپت
         * می‌خواند و اپ با خطایی می‌شکست که هیچ ربطی به علتِ واقعی
         * (قطع بودنِ شبکه) نداشت.
         */
        if (req.mode === 'navigate') return caches.match('./index.html');
        return Response.error();
      });
    })
  );
});

/*
 * ══ پوشِ خبرها — «اپ بسته باشد، پیام برسد» (۱۴۰۵/۰۷/۱۳) ═══════════════════
 *
 * سرورِ خانگی با هر خبرِ تازه در عکسِ زندهٔ پمپ پوش می‌فرستد
 * (‎stations/alert-push.js‎). روی آیفون این تنها راه است: اپِ بسته هیچ کدی
 * اجرا نمی‌کند جز همین سرویس‌ورکر، و آن هم فقط وقتی پوش برسد.
 *
 * ⚠️ ‎userVisibleOnly‎: هر پوشی **باید** اعلان نشان بدهد — وگرنه سافاری
 * اشتراک را باطل می‌کند. پس حتی بستهٔ خراب هم یک اعلانِ ساده می‌سازد.
 */
self.addEventListener('push', function (e) {
  var d = {};
  try { d = e.data ? e.data.json() : {}; } catch (err) {
    try { d = { body: e.data ? e.data.text() : '' }; } catch (err2) { d = {}; }
  }
  var title = d.title || 'خبرِ پمپ';
  var opts = {
    body: d.body || 'خبرِ تازه‌ای از پمپ رسید.',
    tag: d.tag || 'pump-alert',
    renotify: true,
    dir: 'rtl',
    lang: 'fa',
    icon: '../icons/icon-192.png',
    badge: '../icons/icon-192.png',
    data: { url: d.url || './' }
  };
  e.waitUntil(self.registration.showNotification(title, opts));
});

self.addEventListener('notificationclick', function (e) {
  e.notification.close();
  var target = new URL((e.notification.data && e.notification.data.url) || './', self.registration.scope).href;
  e.waitUntil(self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function (list) {
    for (var i = 0; i < list.length; i++) {
      if (list[i].url.indexOf(self.registration.scope) === 0 && 'focus' in list[i]) return list[i].focus();
    }
    return self.clients.openWindow ? self.clients.openWindow(target) : null;
  }));
});
