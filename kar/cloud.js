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

  /**
   * شناسهٔ این برنامه نزدِ سرورِ مرکزی.
   *
   * ⛔ **بی این، ورود با گوگل بی‌صدا می‌شکست.** سرور نشست را به بخشِ
   * برنامه مهر می‌زند (`tokens.app`) و توکنِ یک بخش در بخشِ دیگر
   * **پیدا نمی‌شود**. این اپ هیچ‌وقت نمی‌گفت کیست، پس نشستش «دکان»
   * می‌شد و همان لحظه `‎GET /api/pump/me‎` می‌گفت «چنین نشستی نیست» —
   * کارمند «صاحبِ پمپ هستم» را می‌زد، گوگل را رد می‌کرد، و اپ
   * برمی‌گشت سرِ خانهٔ اول بی آن‌که بگوید چرا.
   *
   * همان مقداری که برنامهٔ کامپیوتر می‌فرستد (`CloudConfig.ApplicationId`).
   */
  var APP_ID = 'tohid-pump-app';

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
    //  ⚠️ روی **هر** درخواست، نه فقط ورود: سرور از همین می‌فهمد کدام
    //  برنامه است، و مسیرهای دیگر هم روزی ممکن است لازمش داشته باشند.
    var headers = { 'Content-Type': 'application/json', 'X-App-Id': APP_ID };
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
      //  ⚠️ در بدنه **هم** می‌آید، نه فقط در هدر: سرورِ قدیمی هدر را
      //  نمی‌خواند و همان است که امروز روی هوا است.
      app: 'pump',
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
   * ورود با ایمیل و رمز — همان حسابی که در برنامهٔ کامپیوتر ساخته شده.
   *
   * ⛔ <b>چرا لازم شد:</b> تا دیروز تنها راهِ «صاحبِ پمپ هستم» گوگل بود، و
   * ‎googleClientId‎ روی سرور می‌تواند خالی باشد (پیش‌فرضش همین است) — آن
   * وقت صفحه فقط می‌گفت «ورود با گوگل روی این سرور تنظیم نشده است» و
   * صاحبِ پمپ هیچ راهی نداشت. و مهم‌تر: حسابی که با <b>ایمیل و رمز</b>
   * ساخته شده اصلاً حسابِ گوگلی نیست، پس همان آدم روی گوشی‌اش نمی‌توانست
   * وارد شود — در حالی که قاعدهٔ «یک سرور، یک حساب» می‌گوید باید بتواند.
   *
   * ⚠️ همان بدنه و همان هدرِ ‎X-App-Id‎ی برنامهٔ کامپیوتر: نشست به بخشِ
   * <b>پمپ</b> مهر می‌خورد، وگرنه ‎/api/pump/me‎ می‌گوید «چنین نشستی نیست».
   */
  function signInWithPassword(email, password) {
    return call('POST', '/api/auth/login', {
      email: String(email || '').trim(),
      password: String(password || ''),
      app: 'pump',
      device: { deviceId: deviceId(), name: 'اپِ کارمندان', platform: 'web' }
    }).then(function (out) {
      var s = {
        token: out.accessToken || out.token,
        refresh: out.refreshToken,
        expiresAt: out.accessExpiresAt || 0,
        name: (out.user && out.user.name) || '',
        email: (out.user && out.user.email) || ''
      };
      if (!s.token) throw new Error('سرور نشست نداد');
      saveSession(s);
      return s;
    });
  }

  /**
   * «رمزم را فراموش کرده‌ام» — کد به همان ایمیل می‌رود.
   *
   * ⚠️ پیامِ سرور برای ایمیلِ موجود و ناموجود یکی است و باید همان بماند.
   */
  function forgotPassword(email) {
    return call('POST', '/api/auth/password/forgot', {
      email: String(email || '').trim(), app: 'pump'
    });
  }

  /** کدِ ایمیل + رمزِ تازه ⇒ نشستِ تازه. */
  function resetPassword(email, code, password) {
    return call('POST', '/api/auth/password/reset', {
      email: String(email || '').trim(),
      code: String(code || '').replace(/[^0-9]/g, ''),
      password: String(password || ''),
      app: 'pump',
      device: { deviceId: deviceId(), name: 'اپِ کارمندان', platform: 'web' }
    }).then(function (out) {
      if (!out || !(out.accessToken || out.token)) return null;
      var s = {
        token: out.accessToken || out.token,
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

  /* ── کدِ پمپ — «هر کسی که برنامه را نصب می‌کند باید آن کد را بزند» ─── */

  /**
   * کدِ هشت‌حرفیِ پمپ ⇒ نشانی و رمزِ فقط‌خواندنیِ **همان یک** پمپ.
   *
   * خواستهٔ صریحِ صاحب ریپو: «ادرسِ همان پمپ را در برنامه بزنم، حساب‌های
   * همان پمپ را نشان بدهد… با پمپ‌های دیگر قاطی نشود — این را خیلی جدی
   * بگیر.» هیچ حسابی لازم نیست؛ همین کد هویت است. سرور برای کدِ غلط،
   * کدِ عوض‌شده و پمپِ بسته یکسان ۴۰۴ می‌دهد.
   *
   * جواب همان شکلِ ‎myStation()‎ است تا ‎adoptStation‎ی اپ فرقی نبیند.
   */
  function joinWithCode(code) {
    var clean = normalizeCode(code);
    if (clean.length !== 8) {
      var e = new Error('کدِ پمپ هشت حرف و رقم است، مثلِ K7PM-3XQ2');
      e.code = 'bad_access_code';
      return Promise.reject(e);
    }
    return call('POST', '/api/pump/public/join', { code: clean }).then(function (out) {
      if (!out || !out.station) return null;
      return {
        code: out.station.code,
        name: out.station.name || '',
        role: 'staff',
        accessCode: clean,
        cloudLiveAt: out.cloudLiveAt || null,
        home: out.home || { url: '', readKey: '', station: out.station.code }
      };
    });
  }

  /**
   * عکسِ ابریِ پمپ — برای وقتی که سرورِ خانگی از راهِ دور جواب نمی‌دهد.
   * همان ‎live.json‎ی است که برنامهٔ کامپیوتر هر ده دقیقه به ابر می‌فرستد؛
   * تازگی‌اش را ‎updatedAt‎ می‌گوید و اپ همان را به کارمند نشان می‌دهد.
   */
  function cloudLive(code) {
    return call('GET', '/api/pump/public/live?code=' + encodeURIComponent(normalizeCode(code)))
      .then(function (out) {
        return out && out.live ? { live: out.live, updatedAt: out.updatedAt || 0 } : null;
      });
  }

  /** ‎' k7pm-3xq2 '‎ ⇒ ‎'K7PM3XQ2'‎ — همان قاعدهٔ سرور. */
  function normalizeCode(raw) {
    return String(raw || '').toUpperCase().replace(/[^A-Z0-9]/g, '');
  }

  /** برای نمایش: ‎K7PM-3XQ2‎. */
  function formatCode(raw) {
    var c = normalizeCode(raw);
    return c.length === 8 ? c.slice(0, 4) + '-' + c.slice(4) : c;
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
    signInWithPassword: signInWithPassword,
    forgotPassword: forgotPassword,
    resetPassword: resetPassword,
    refresh: refresh,
    myStation: myStation,
    joinWithCode: joinWithCode,
    cloudLive: cloudLive,
    normalizeCode: normalizeCode,
    formatCode: formatCode,
    postInbox: postInbox,
    signOut: signOut,
    session: session,
    signedIn: signedIn
  };

  if (typeof module !== 'undefined' && module.exports) module.exports = root.PumpCloud;
})(typeof window !== 'undefined' ? window : globalThis);
