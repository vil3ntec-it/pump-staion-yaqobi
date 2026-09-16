/* ══ به‌روزرسانیِ خودکار — «برنامه و سایت خودشان از گیت‌هاب آپدیت شوند» ═══════
 *
 * خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۶): «کاری کن برنامه و سایت هم از خودشان
 * آپدیت شوند؛ یعنی از گیت‌هاب که نسخهٔ جدید می‌دهیم، توی برنامه آپدیت شود.»
 *
 * یک منبعِ حقیقت: ‎kar/version.json‎ که سرِ هر انتشارِ سایت خودکار ساخته می‌شود
 * (‎deploy-pages.yml‎): ‎v‎ = شمارِ کامیت‌ها، ‎apk‎ = نسخهٔ آخرین فایلِ نصب.
 * فایلِ نصبِ اندروید هم همان ‎v‎ را داخلِ خودش دارد (‎build-kar-apk.yml‎)، پس
 * «کدام تازه‌تر است» یک مقایسهٔ عددیِ ساده است.
 *
 *   • آیفون / مرورگر: ‎v‎ی تازه ⇒ نوارِ «به‌روز کن» ⇒ سرویس‌ورکر تازه می‌شود و
 *     صفحه از نو بار می‌شود (شبکه اول است، پس فایل‌های تازه می‌آیند).
 *   • اندروید (پوسته): ‎v‎ی تازه ⇒ همین چهار فایل از سایت گرفته و با پلِ
 *     ‎PumpAndroid‎ در پوشهٔ خصوصیِ برنامه نوشته می‌شوند؛ از باز شدنِ بعدی همان
 *     بالا می‌آید — بی نصبِ دوباره. اگر خراب بود، پوسته خودش به نسخهٔ همراهِ
 *     نصب برمی‌گردد (‎updateOk‎ / شمارندهٔ تلاش).
 *     ‎apk‎ی تازه‌تر از خودِ فایلِ نصب ⇒ دکمهٔ «فایلِ نصبِ تازه» (مرورگر باز
 *     می‌شود و فایل را می‌گیرد؛ نصبش کارِ خودِ کاربر است).
 *
 * ⚠️ نشانیِ سایت در خودِ کد قفل است — همان قاعدهٔ ‎cloud.js‎: از هیچ تنظیمی
 * خوانده نمی‌شود، وگرنه کسی می‌توانست اپ را از سرورِ خودش «به‌روز» کند.
 * ⚠️ هیچ‌چیز خودکار جای صفحهٔ در حالِ کار را نمی‌گیرد: همه‌چیز پشتِ نوار و
 * دکمه است. تنها کارِ بی‌صدا، نوشتنِ فایل‌ها برای بارِ بعد است.
 */
(function (root) {
  'use strict';

  var SITE = 'https://yaqobipump.top/kar/';
  var FILES = ['index.html', 'app.js', 'cloud.js', 'update.js', 'manifest.json'];
  var CHECK_MS = 30 * 60 * 1000;   // هر نیم ساعت — و هر بار که صفحه جلوی چشم بیاید
  var SNOOZE_MS = 6 * 60 * 60 * 1000;

  var $ = function (id) { return document.getElementById(id); };
  var android = function () { return !!(root.PumpAndroid && root.PumpAndroid.info); };

  function num(v) { var n = parseInt(String(v || '').replace(/\D/g, ''), 10); return isFinite(n) ? n : 0; }

  /** «1.0.12» در برابرِ «1.0.8» — همان مقایسهٔ پوسته. */
  function verCmp(a, b) {
    var pa = String(a || '').split('.'), pb = String(b || '').split('.');
    for (var i = 0; i < Math.max(pa.length, pb.length); i++) {
      var x = parseInt(pa[i] || '0', 10) || 0, y = parseInt(pb[i] || '0', 10) || 0;
      if (x !== y) return x > y ? 1 : -1;
    }
    return 0;
  }

  /** نسخهٔ وبی که همین حالا اجرا می‌شود. */
  var runningV = 0;         // مرورگر: از اولین version.json؛ اندروید: از پوسته
  var shellInfo = null;

  function readShell() {
    try { shellInfo = JSON.parse(root.PumpAndroid.info()); } catch (e) { shellInfo = null; }
    if (!shellInfo) return;
    runningV = num(shellInfo.usingUpdate ? shellInfo.version : shellInfo.bundled);
    //  سالم بالا آمدیم ⇒ شمارندهٔ تلاشِ پوسته صفر (وگرنه بارِ دوم آپدیت را دور می‌ریزد)
    try { if (shellInfo.usingUpdate && root.PumpAndroid.updateOk) root.PumpAndroid.updateOk(); } catch (e) { }
  }

  function versionUrl() {
    return (android() ? SITE : './') + 'version.json?ts=' + Date.now();
  }

  function fetchVersion() {
    return fetch(versionUrl(), { cache: 'no-store' }).then(function (r) {
      if (!r.ok) throw new Error('HTTP ' + r.status);
      return r.json();
    });
  }

  /* ── نوار ────────────────────────────────────────────────────────────── */

  var pending = null;   // {kind: 'web'|'apk', v, apk}

  function snoozed(kind, v) {
    try {
      var s = JSON.parse(localStorage.getItem('pumpKar.upd.snooze') || '{}');
      return s.kind === kind && String(s.v) === String(v) && Date.now() - (s.at || 0) < SNOOZE_MS;
    } catch (e) { return false; }
  }

  function showBar(kind, title, note, v) {
    if (snoozed(kind, v)) return;
    var bar = $('updBar');
    if (!bar) return;
    pending = { kind: kind, v: v };
    $('updTitle').textContent = title;
    $('updNote').textContent = note || '';
    $('updGo').textContent = kind === 'apk' ? 'گرفتنِ فایل' : 'به‌روز کن';
    bar.classList.remove('hidden');
  }

  function hideBar() { var b = $('updBar'); if (b) b.classList.add('hidden'); }

  function later() {
    if (pending) {
      try { localStorage.setItem('pumpKar.upd.snooze', JSON.stringify({ kind: pending.kind, v: pending.v, at: Date.now() })); } catch (e) { }
    }
    hideBar();
  }

  /* ── مرورگر / آیفون ─────────────────────────────────────────────────── */

  function reloadFresh() {
    var p = Promise.resolve();
    if ('serviceWorker' in navigator) {
      p = navigator.serviceWorker.getRegistration().then(function (r) {
        return r ? r.update().catch(function () { }) : null;
      }).catch(function () { });
    }
    return p.then(function () { location.reload(); });
  }

  /* ── اندروید: گرفتنِ فایل‌ها و نوشتن از راهِ پل ────────────────────── */

  var applying = false;

  function applyWeb(v) {
    if (applying || !android()) return Promise.resolve(false);
    applying = true;
    var A = root.PumpAndroid;
    var i = 0;
    function next() {
      if (i >= FILES.length) return Promise.resolve(true);
      var name = FILES[i++];
      return fetch(SITE + name + '?v=' + encodeURIComponent(v), { cache: 'no-store' }).then(function (r) {
        if (!r.ok) throw new Error(name + ' HTTP ' + r.status);
        return r.text();
      }).then(function (text) {
        if (!text || text.length < 200) throw new Error(name + ' خالی');
        //  همان صفحه‌ای که گرفته‌ایم باید همان نسخه باشد — index.html
        //  به update.js اشاره می‌کند، پس هر پنج فایل با هم می‌روند.
        if (!A.updateFileBegin(name)) throw new Error('begin ' + name);
        for (var p = 0; p < text.length; p += 65536)
          if (!A.updateChunk(text.slice(p, p + 65536))) throw new Error('chunk ' + name);
        if (!A.updateFileCommit()) throw new Error('commit ' + name);
        return next();
      });
    }
    return next().then(function () {
      //  version.json هم می‌رود تا ‎bundled/version‎ی پوسته درست باشد
      if (!A.updateFileBegin('version.json') || !A.updateChunk(JSON.stringify({ v: String(v) })) || !A.updateFileCommit())
        throw new Error('version');
      if (!A.updateFinish(String(v))) throw new Error('finish');
      applying = false;
      return true;
    }).catch(function (e) {
      try { A.updateAbort(); } catch (x) { }
      applying = false;
      try { console.warn('[update]', e && e.message); } catch (x) { }
      return false;
    });
  }

  /* ── چرخهٔ بررسی ────────────────────────────────────────────────────── */

  var lastCheck = 0;

  function check(force) {
    if (!force && Date.now() - lastCheck < 60 * 1000) return Promise.resolve();
    lastCheck = Date.now();
    return fetchVersion().then(function (info) {
      var v = num(info && info.v);
      if (!v) return;
      if (!runningV) { runningV = v; return; }        // مرورگر: اولین بار فقط یاد می‌گیرد
      if (android()) {
        //  ۱) فایلِ نصبِ تازه‌تر؟ (مقدمِ بر وب — پوستهٔ تازه شاید پل‌های تازه بخواهد)
        if (info.apk && shellInfo && shellInfo.app && verCmp(info.apk, shellInfo.app) > 0) {
          showBar('apk', 'فایلِ نصبِ تازه هست (' + info.apk + ')',
            'نسخهٔ نصب‌شده ' + shellInfo.app + ' است. با «گرفتنِ فایل» مرورگر باز می‌شود؛ بعد نصبش کنید.', info.apk);
          return;
        }
        //  ۲) وبِ تازه‌تر؟ بی‌صدا بگیر و برای بارِ بعد بنویس؛ بعد بگو.
        if (v > runningV) {
          return applyWeb(v).then(function (ok) {
            if (ok) showBar('web', 'نسخهٔ تازه آماده است', 'همین حالا سوار شود؟ (وگرنه با باز شدنِ بعدی خودش می‌آید)', v);
          });
        }
        return;
      }
      if (v > runningV) showBar('web', 'نسخهٔ تازه آماده است', 'یک لحظه صفحه از نو بار می‌شود.', v);
    }).catch(function () { /* اینترنت نبود — بعداً */ });
  }

  function go() {
    if (!pending) return hideBar();
    if (pending.kind === 'apk') {
      try { root.PumpAndroid.openUrl('https://github.com/vil3ntec-it/pump-staion-yaqobi/releases/download/kar-latest/PumpYaqobiKar.apk'); } catch (e) { }
      later();
      return;
    }
    hideBar();
    if (android()) { try { root.PumpAndroid.restart(); } catch (e) { location.reload(); } }
    else reloadFresh();
  }

  function boot() {
    if (android()) readShell();
    var g = $('updGo'), l = $('updLater');
    if (g) g.addEventListener('click', go);
    if (l) l.addEventListener('click', later);
    setTimeout(function () { check(true); }, 4000);
    setInterval(function () { check(false); }, CHECK_MS);
    document.addEventListener('visibilitychange', function () {
      if (document.visibilityState === 'visible') check(false);
    });
  }

  root.PumpUpdate = { check: check, verCmp: verCmp, num: num, SITE: SITE, FILES: FILES,
    _state: function () { return { runningV: runningV, pending: pending, shell: shellInfo }; } };

  if (typeof module !== 'undefined' && module.exports) module.exports = root.PumpUpdate;
  if (typeof document === 'undefined') return;
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();
})(typeof window !== 'undefined' ? window : globalThis);
