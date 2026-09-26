// ⛔ این سرویس‌ورکر فقط یک کار دارد: پاک کردنِ سایتِ قدیم از گوشی‌ها.
//
// سایتِ قدیم (۱۴۰۵/۰۷/۱۴ به archive/old-site رفت) یک سرویس‌ورکر روی همین نشانی
// (/sw.js، دامنهٔ /) ثبت کرده بود که صفحه‌اش را کش می‌کرد؛ پس هر گوشی‌ای که یک بار
// آن را باز کرده بود، حتی پس از برداشتنِ سایت هم همان صفحهٔ کهنه را می‌دید.
// مرورگر با هر باز شدنِ صفحه همین فایل را دوباره می‌گیرد؛ این نسخه جایش
// می‌نشیند، کشِ سایتِ قدیم را پاک می‌کند، خودش را برمی‌دارد و صفحه را دوباره باز
// می‌کند — این بار صفحهٔ تازه، از شبکه.
//
// ⚠️ فقط کش‌های خودِ سایتِ قدیم ('pump-yaqobi-…') پاک می‌شوند. اپِ کارمندان
// (/kar/)، صفحهٔ کیو‌آرِ مشتری (/view/) و پیام‌رسان (/payam/) کش و سرویس‌ورکرِ
// خودشان را دارند و نباید دست بخورند.
// ⛔ این فایل را پاک نکنید: بی آن، مرورگرِ گوشی سرویس‌ورکرِ قدیم را نگه می‌دارد.

self.addEventListener('install', () => self.skipWaiting());

self.addEventListener('activate', event => {
  event.waitUntil((async () => {
    try {
      const keys = await caches.keys();
      await Promise.all(keys.filter(k => k.indexOf('pump-yaqobi-') === 0).map(k => caches.delete(k)));
    } catch (_) {}
    try { await self.registration.unregister(); } catch (_) {}
    try {
      const list = await self.clients.matchAll({ type: 'window' });
      for (const c of list) {
        try { c.navigate(c.url); } catch (_) {}
      }
    } catch (_) {}
  })());
});

// هیچ درخواستی گرفته نمی‌شود — همه‌چیز مستقیم از شبکه.
