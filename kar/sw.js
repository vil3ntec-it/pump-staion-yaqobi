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
var CACHE = 'pump-kar-v1';
var SHELL = ['./', './index.html', './app.js', './manifest.json'];

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

  // ⚠️ «شبکه اول»: وگرنه نسخهٔ تازهٔ اپ هیچ‌وقت به گوشی نمی‌رسید و کارمند
  // هفته‌ها با نسخهٔ کهنه کار می‌کرد. کش فقط پشتیبانِ قطعیِ شبکه است.
  e.respondWith(
    fetch(req).then(function (res) {
      if (res && res.ok && new URL(req.url).origin === location.origin)
        caches.open(CACHE).then(function (c) { c.put(req, res.clone()); });
      return res;
    }).catch(function () {
      return caches.match(req).then(function (hit) {
        return hit || caches.match('./index.html');
      });
    })
  );
});
