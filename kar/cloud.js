/* ══ ابر — حساب، و «دیگر از کسی آدرس نپرس» ═══════════════════════════════════
 *
 * خواستهٔ صریحِ صاحب ریپو: «برنامه پمپ یعقوبی کارمندان چرا ادرس اینترنتی
 * میخان؟ این رو نخان، توی اپ است و این‌جا لازم نباشه و اونو بردار.»
 *
 * ── مشکلی که این حل می‌کند ─────────────────────────────────────────────────
 * کارمند برای دیدنِ دفتر دو چیز لازم دارد: نشانیِ سرورِ خانگیِ پمپ، و رمزِ
 * فقط‌خواندنی‌اش. تا امروز هر دو یا دستی نوشته می‌شد یا از روی کاغذِ کیو‌آر
 * اسکن می‌شد — و چون آی‌پیِ خانگی با هر بار روشن شدنِ مودم عوض می‌شود، همان
 * نشانی چند روز بعد بی‌صدا از کار می‌افتاد و کسی نمی‌فهمید چرا.
 *
 * حالا کارمند فقط با گوگل وارد می‌شود و بقیه‌اش کارِ سرور است:
 *
 *     ورود با گوگل ──▶ POST /api/auth/google  ──▶ توکنِ حساب
 *                  ──▶ GET  /api/pump/me      ──▶ { url, readKey, station }
 *                  ──▶ همان سرورِ خانگی، مثل همیشه
 *
 * ── نشانیِ ابر قفل است ────────────────────────────────────────────────────
 * ‎CLOUD‎ در خودِ برنامه نوشته شده و از هیچ‌جا خوانده نمی‌شود — نه از تنظیمات،
 * نه از نوارِ نشانی. پس هیچ‌چیز روی گوشیِ کارمند نمی‌تواند اپ را به سرورِ
 * دیگری ببرد و حسابش را آن‌جا خالی کند. همان قاعده‌ای که در ریپوی shop برای
 * ‎AppConfig.kt‎ و ‎api-config.js‎ گذاشته شده.
 *
 * ── چرا راهِ قدیمی برداشته نشد ────────────────────────────────────────────
 * ⚠️ کیو‌آر و لینک هنوز کار می‌کنند و **نباید** برداشته شوند. دو حالت هست که
 * ابر در آن‌ها به درد نمی‌خورد:
 *   • پمپی که هنوز حساب نساخته — برنامهٔ کامپیوترش به‌روز نشده است
 *   • جایی که به شبکهٔ خودِ پمپ وصل است ولی اینترنت ندارد
 * در هر دو، کیو‌آر تنها راه است. پس ابر «راهِ اول» است، نه «تنها راه».
 * ══════════════════════════════════════════════════════════════════════════ */
(function (root) {
  'use strict';

  /** نشانیِ ابر. قفل — از تنظیمات و نوارِ نشانی خوانده نمی‌شود. */
  var CLOUD = 'https://api.vill3n.top';

  var TOKEN_KEY = 'pumpKar.cloud.v1';

  /* ── نگه‌داریِ نشست ─────────────────────────────────────────────────── */

  function loadSession() {
    try {
      var raw = localStorage.getItem(TOKEN_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch (e) { return null; }
  }

  function saveSession(s) {
    try {
      if (s) localStorage.setItem(TOKEN_KEY, JSON.stringify(s));
      else localStorage.removeItem(TOKEN_KEY);
    } catch (e) { }
  }

  /**
   * شناسهٔ این دستگاه.
   *
   * یک بار ساخته می‌شود و می‌ماند. سرور با همین «دستگاه» را می‌شناسد؛ اگر
   * هر بار تازه می‌ساختیم، فهرستِ دستگاه‌های کاربر با هر بار باز کردنِ اپ
   * یک ردیف بلندتر می‌شد.
   */
  function deviceId() {
    var k = 'pumpKar.device';
    try {
      var v = localStorage.getItem(k);
      if (v) return v;
      v = 'kar-' + Math.random().toString(36).slice(2) + Date.now().toString(36);
      localStorage.setItem(k, v);
      return v;
    } catch (e) {
      return 'kar-' + Date.now().toString(36);
    }
  }

  /* ── گفت‌وگو با ابر ────────────────────────────────────────────────── */

  /**
   * یک درخواست به ابر.
   *
   * خطاها همیشه ‎Error‎ با پیامِ فارسیِ خودِ سرور می‌شوند، و ‎.code‎ همان
   * کدِ ماشینیِ سرور را نگه می‌دارد — تا صداکننده بتواند «۴۰۱ یعنی دوباره
   * وارد شو» را از «اینترنت نبود» جدا کند.
   */
  function call(method, path, body, token) {
    var headers = { 'Content-Type': 'application/json' };
    if (token) headers.Authorization = 'Bearer ' + token;
    return fetch(CLOUD + path, {
      method: method,
      headers: headers,
      body: body === undefined ? undefined : JSON.stringify(body)
    }).then(function (res) {
      return res.text().then(function (text) {
        var json = null;
        try { json = text ? JSON.parse(text) : null; } catch (e) { }
        if (!res.ok) {
          var err = new Error((json && json.error && json.error.message) || ('خطای ' + res.status));
          err.status = res.status;
          err.code = (json && json.error && json.error.code) || '';
          throw err;
        }
        return json;
      });
    });
  }

  /**
   * ورود با حساب گوگل.
   *
   * ‎idToken‎ همان چیزی است که گوگل به صفحه می‌دهد. سرور امضایش را با
   * کلیدهای عمومیِ گوگل می‌سنجد، پس این صفحه نمی‌تواند ادعای دروغ بکند.
   * اگر حساب نباشد، همان‌جا ساخته می‌شود — کارمندی که تازه اپ را گرفته
   * نباید جای دیگری ثبت‌نام کند.
   */
  function signInWithGoogle(idToken) {
    return call('POST', '/api/auth/google', {
      idToken: idToken,
      device: { deviceId: deviceId(), name: 'اپِ کارمندان', platform: 'web' }
    }).then(function (out) {
      var s = {
        token: out.accessToken,
        refresh: out.refreshToken,
        expiresAt: out.accessExpiresAt || 0,
        name: (out.user && out.user.name) || '',
        email: (out.user && out.user.email) || ''
      };
      saveSession(s);
      return s;
    });
  }

  /**
   * نشستِ تازه از روی ‎refreshToken‎.
   *
   * توکنِ دسترسی عمرِ کوتاهی دارد. بی این، کارمند هر چند روز یک بار باید
   * دوباره با گوگل وارد می‌شد — و همان چیزی می‌شد که قرار بود نباشد.
   */
  function refresh() {
    var s = loadSession();
    if (!s || !s.refresh) return Promise.reject(new Error('نشستی نیست'));
    return call('POST', '/api/auth/refresh', {
      refreshToken: s.refresh,
      device: { deviceId: deviceId() }
    }).then(function (out) {
      s.token = out.accessToken;
      s.refresh = out.refreshToken || s.refresh;
      s.expiresAt = out.accessExpiresAt || 0;
      saveSession(s);
      return s;
    });
  }

  /**
   * همان درخواست، ولی اگر توکن منقضی بود یک بار تازه‌اش می‌کند.
   *
   * ⚠️ فقط **یک** بار — اگر بعد از تازه‌سازی هم ۴۰۱ گرفتیم، یعنی نشست
   * واقعاً باطل است و حلقه زدن جز پنهان کردنِ مشکل کاری نمی‌کند.
   */
  function authed(method, path, body) {
    var s = loadSession();
    if (!s || !s.token) return Promise.reject(new Error('وارد نشده‌اید'));
    return call(method, path, body, s.token).catch(function (err) {
      if (err.status !== 401) throw err;
      return refresh().then(function (fresh) {
        return call(method, path, body, fresh.token);
      });
    });
  }

  /**
   * پمپِ این کارمند — و مهم‌تر: نشانی و رمزِ سرورِ خانگی‌اش.
   *
   * این همان چیزی است که فرمِ «نشانیِ سرور» را بی‌کار می‌کند.
   */
  function myStation() {
    return authed('GET', '/api/pump/me').then(function (out) {
      if (!out || !out.station) return null;
      return {
        id: out.station.id,
        code: out.station.code,
        name: out.station.name,
        role: out.role || 'staff',
        entitlement: out.entitlement || null,
        home: out.home || { url: '', readKey: '', station: out.station.code }
      };
    });
  }

  /** پیامی در صندوقِ ورودیِ پمپ — راهِ برگشتِ داده از گوشیِ کارمند. */
  function postInbox(data) {
    return authed('PUT', '/api/pump/files/inbox.json', { data: data });
  }

  function signOut() { saveSession(null); }

  function session() { return loadSession(); }

  /** آیا نشستی هست که بشود رویش حساب کرد؟ */
  function signedIn() {
    var s = loadSession();
    return !!(s && (s.token || s.refresh));
  }

  root.PumpCloud = {
    CLOUD: CLOUD,
    call: call,
    authed: authed,
    deviceId: deviceId,
    signInWithGoogle: signInWithGoogle,
    refresh: refresh,
    myStation: myStation,
    postInbox: postInbox,
    signOut: signOut,
    session: session,
    signedIn: signedIn
  };

  if (typeof module !== 'undefined' && module.exports) module.exports = root.PumpCloud;
})(typeof window !== 'undefined' ? window : globalThis);
