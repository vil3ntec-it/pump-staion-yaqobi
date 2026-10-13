'use strict';
/**
 * ════════════════════════════════════════════════════════════════════
 *  باتِ تلگرامِ پمپ بنزین — جای خودش: ریپوی پمپ (از ۱۴۰۵/۰۷/۲۰)
 * ════════════════════════════════════════════════════════════════════
 *
 *  خواستهٔ صاحب سامانه: «باتِ پمپ بنزین را بیاور توی ریپوی پمپ… به شاپ ربطی
 *  نده؛ بعداً برای شاپ بات جدا می‌سازم.»
 *
 *  این بات **افزونهٔ سرورِ حساب** است: همان‌جا اجرا می‌شود (همان دیتابیس، همان
 *  حساب‌ها، همان هشدارها) ولی کدش این‌جاست. سرورِ حساب هنگامِ ساخت این پوشه را
 *  در `server/bots/pump/` می‌گذارد و با `src/lib/bots.js` بارش می‌کند:
 *
 *      const bot = require('<این پوشه>')({ req });   // req = require از src/libِ سرورِ حساب
 *
 *  ⛔ هیچ `require('../…')` یا `require('./…')`ی این‌جا نیست؛ هر چیزِ سرورِ حساب
 *     از `req` می‌آید (`req('../db')`، `req('./pump-state')`…) — همان مسیرهای
 *     نسبی به `src/lib`ِ سرورِ حساب. بی سرورِ حساب این بات کاری نمی‌کند.
 *  ⛔ سرورِ حسابِ بی این پوشه بی‌صدا «بی بات» بالا می‌آید (`bots.js` ⇒ جانشینِ خاموش).
 *  آزمون‌ها: `telegram-bot/test/` (روی سرورِ حسابِ واقعی، با `run-tests.sh`).
 */
module.exports = function createPumpBot({ req }) {
  if (typeof req !== 'function') throw new Error('باتِ پمپ: req (require از src/libِ سرورِ حساب) لازم است');

  /**
   * باتِ تلگرامِ پمپ — هشدارِ «حسابِ قرض‌دار» و «مخزن»، در تلگرام.
   *
   * ── خواستهٔ صاحب مخزن (۱۴۰۵/۰۷/۱۳) ──────────────────────────────────
   * «بات تلگرام رو روی سرور بساز و برای پمپ باشد و دیگه جای نشتی نکنه و از
   *  دکان هم اون رو ول کن… هر حساب کاربری که روی سرور است و حساب پمپ داره
   *  اون‌ها بتونن برن توی بات و اون‌جا استارت رو که زدن بگه حساب کاربری‌شونو
   *  وارد کنن… و سرور پیدا کنه و به اون حساب متصل بشه و وقتی داشت تیل یارو
   *  تمام می‌شد پیام هشدار تیل بره، قرض‌داری که حسابش تموم شده و مخزن اگه
   *  تیل تموم شد پیام هشدار مخزن بره درجا… دکمه برای برنامه اندروید و
   *  آیفون… و بتونه بات رو به گروه تلگرامش وصل کنه.»
   *
   * ── راه ───────────────────────────────────────────────────────────
   *
   *   کامپیوترِ پمپ ──POST /api/pump/device/events──▶ station_events
   *                                                  └──▶ telegram_outbox ──▶ تلگرام
   *   کاربر در تلگرام: /start ⇒ ایمیل ⇒ کدِ شش‌رقمی به **همان ایمیل** ⇒ وصل
   *
   * ⛔ **هیچ دادهٔ تازه‌ای ساخته نمی‌شود.** فهرستِ هشدار همان است که برنامهٔ
   * پمپ از قبل می‌فرستاد (`StationSnapshot.Alerts` ⇒ `CloudEvents`) و همان
   * سه نوعی که پوش می‌شوند (`events.PUSH_KINDS`). قاعدهٔ جدا بنویسید و روزی
   * کارتِ قرض‌دار سرخ است و تلگرام ساکت.
   *
   * ── «جای نشتی نکنه» — پنج قید ────────────────────────────────────
   *
   * 1. **ایمیل تنها کافی نیست.** کسی که فقط ایمیلِ دیگری را بداند نباید
   *    هشدارهای پمپِ او را بگیرد؛ پس کدِ شش‌رقمی به **همان ایمیل** می‌رود
   *    (همان `otp`ِ ثبت‌نام، با `purpose='telegram'`).
   * 2. **پاسخِ «این ایمیل حساب دارد؟» یکی است** — چه باشد چه نباشد. بات
   *    نباید دفترچهٔ ایمیل‌های مشتری‌ها شود.
   * 3. **عضویت در لحظهٔ فرستادن سنجیده می‌شود**، نه فقط هنگامِ وصل شدن:
   *    کارمندی که از پمپ بیرون شد یا حسابی که بسته شد، از همان لحظه هیچ
   *    هشداری نمی‌گیرد — در خصوصی و در گروهی که او وصل کرده بود.
   * 4. **گروه فقط با نشانیِ یک‌بارمصرفِ پانزده‌دقیقه‌ای** وصل می‌شود، که فقط
   *    از گفت‌وگوی خصوصیِ **وصل‌شده** ساخته می‌شود. خودِ نشانه ذخیره نمی‌شود،
   *    فقط هشش.
   * 5. **رمزِ بات فقط روی همین سرور است** (رمزشده در `app_config` یا در
   *    محیط) — نه در برنامهٔ کامپیوتر، نه در اپِ گوشی، نه در هیچ پاسخی.
   *
   * ⚠️ هیچ `parse_mode`ی به کار نمی‌رود: نامِ قرض‌دار را کاربر نوشته، و
   * متنِ ساده یعنی هیچ نشانه‌ای از آن نام نمی‌تواند پیام را دست‌کاری کند.
   *
   * ⚠️ «گرفتنِ پیام» با long polling است، نه webhook: سرور پشتِ تونلِ
   * خانگی است و webhook یک نشانیِ عمومیِ دیگر می‌خواست. فقط **یک** فرآیند
   * باید با یک رمز بگیرد؛ دومی از تلگرام ۴۰۹ می‌گیرد و خودش صبر می‌کند.
   */
  const crypto = require('crypto');
  const { query, one, many, newId, now } = req('../db');
  const config = req('../config');
  const plans = req('./plans');
  const stations = req('./stations');

  const CFG_TOKEN = 'telegram_token';        // رمزشده با کلیدِ پمپ‌ها
  const CFG_ENABLED = 'telegram_enabled';    // '1' | '0'
  const CFG_OFFSET = 'telegram_offset';      // شمارهٔ آخرین پیامِ گرفته‌شده + ۱
  const CFG_USERNAME = 'telegram_username';  // نامِ بات، برای لینک‌ها — راز نیست

  /** کدِ شش‌رقمیِ همین راه — جدا از ثبت‌نام، تا هیچ‌کدام کدِ دیگری را نپذیرد. */
  const PURPOSE = 'telegram';

  /** همان سه نوعی که پوش می‌شوند — `events.PUSH_KINDS`. یک فهرست، نه دو. */
  const ALERT_KINDS = ['stock_out', 'low_stock', 'debt', 'shift'];

  /** کسریِ صندوق (`cash-…`) یا شیفتِ بسته‌نشده (`shift-…`) — از ورقِ برنامه (`AlertWatch.ShiftAlerts`). */
  const shiftIcon = (k) => (String(k || '').startsWith('cash-') ? '💰' : String(k || '').startsWith('shift-') ? '⏰' : '');

  const LINK_TTL_MS = 15 * 60 * 1000;
  const CODE_WINDOW_MS = 60 * 60 * 1000;
  const CODE_MAX_PER_WINDOW = 5;
  const MAX_ATTEMPTS = 8;
  const GIVE_UP_MS = 24 * 3600 * 1000;
  const KEEP_MS = 7 * 24 * 3600 * 1000;
  /**
   * ⛔ «محدودیت هم نداشته باشه» (۱۴۰۵/۰۷/۱۴): هیچ فهرستی بریده نمی‌شود —
   * نه هشدارها، نه نتیجهٔ جست‌وجو. تلگرام یک پیام را تا ۴۰۹۶ نویسه می‌پذیرد،
   * پس متنِ بلند به چند پیامِ پشتِ سرِ هم شکسته می‌شود (`chunks`)، همیشه سرِ
   * مرزِ یک بند یا یک خط — هیچ نامی وسطش بریده نمی‌شود.
   */
  const CHUNK = 3800;

  /** متنِ بلند ⇒ تکه‌های ≤ CHUNK، سرِ مرزِ بند (خطِ خالی) و بعد خط. */
  function chunks(text, max = CHUNK) {
    const out = [];
    let cur = '';
    const push = (piece, sep) => {
      if (!cur) { cur = piece; return; }
      if (cur.length + sep.length + piece.length <= max) { cur += sep + piece; return; }
      out.push(cur); cur = piece;
    };
    for (const block of String(text).split('\n\n')) {
      if (block.length <= max) { push(block, '\n\n'); continue; }
      //  بندی که خودش بلند است: خط‌به‌خط
      let first = true;
      for (const line of block.split('\n')) {
        const safe = line.length > max ? line.slice(0, max) : line;
        push(safe, first ? '\n\n' : '\n');
        first = false;
      }
    }
    if (cur || !out.length) out.push(cur);
    return out;
  }

  /** چند پیامِ پشتِ سرِ هم برای یک متنِ بلند. */
  async function sendLong(chatId, text) {
    let res = null;
    for (const part of chunks(text)) {
      res = await send(chatId, part);
      if (!res.ok) break;
    }
    return res;
  }

  /**
   * ستونِ دکمه‌های صف (‎markup‎) — بات خودش می‌سازدش، تا جدولِ سرورِ حساب برای
   * دکمه‌ای که فقط مالِ بات است عوض نشود. یک بار در هر پروسه.
   */
  let outboxReady = null;
  function ensureOutbox() {
    if (!outboxReady) {
      outboxReady = (async () => {
        await query(`ALTER TABLE telegram_outbox ADD COLUMN IF NOT EXISTS markup TEXT NOT NULL DEFAULT ''`);
        //  🔕 کنترلِ شلوغی (۱۴۰۵/۰۷/۲۱): ساعتِ آرام و شمارِ هشدارهای «کم مانده»ِ امروز
        await query(`ALTER TABLE telegram_chats ADD COLUMN IF NOT EXISTS quiet BOOLEAN NOT NULL DEFAULT TRUE`);
        await query(`ALTER TABLE telegram_chats ADD COLUMN IF NOT EXISTS calm_day TEXT NOT NULL DEFAULT ''`);
        await query(`ALTER TABLE telegram_chats ADD COLUMN IF NOT EXISTS calm_n INTEGER NOT NULL DEFAULT 0`);
      })().catch((err) => { outboxReady = null; throw err; });
    }
    return outboxReady;
  }

  /**
   * یک متن ⇒ چند ردیفِ صف، به ترتیب (created_at پشتِ سرِ هم).
   * `keyboard` (اختیاری) فقط زیرِ **آخرین** تکه می‌آید.
   */
  async function queueLong(chatId, stationId, text, t, keyboard = null) {
    await ensureOutbox();
    const parts = chunks(text);
    for (let i = 0; i < parts.length; i++) {
      const kb = keyboard && i === parts.length - 1 ? JSON.stringify(keyboard) : '';
      await query(
        `INSERT INTO telegram_outbox (id, chat_id, station_id, body, next_at, created_at, markup)
         VALUES ($1,$2,$3,$4,$5,$6,$7)`,
        [newId('tgo'), chatId, stationId, parts[i], t, t + i, kb]
      );
    }
  }

  /* ══════════════════════════════════════════════════════════════════
     درگاهِ تلگرام
     ══════════════════════════════════════════════════════════════════ */

  /**
   * ⚠️ **فقط آزمون مقدار می‌دهد** — همان الگوی `push.setDeliver`. تلگرام از
   * ماشینِ آزمون در دسترس نیست؛ بی این، هر سنجه‌ای یا شبکه می‌زد یا سبزِ
   * دروغ می‌داد.
   */
  let transport = null;
  function setTransport(fn) { transport = typeof fn === 'function' ? fn : null; }

  const bot = {
    username: '',
    running: false,
    lastError: '',
    lastOkAt: 0,
    startedAt: 0,
  };

  let tokenCache = null;

  /** رمزِ بات: محیط جلوتر، بعد پنل. هرگز بیرون از این فایل نمی‌رود. */
  async function token() {
    if (config.telegram.token) return config.telegram.token;
    if (tokenCache !== null) return tokenCache;
    const sealed = await plans.getConfig(CFG_TOKEN, '');
    tokenCache = sealed ? await stations.decryptKey(sealed) : '';
    return tokenCache;
  }

  async function enabled() {
    return (await plans.getConfig(CFG_ENABLED, '1')) !== '0';
  }

  /** بات واقعاً کار می‌کند: رمز هست و خاموش نشده. */
  async function active() {
    return (await enabled()) && Boolean(await token());
  }

  /** هیچ متنی که به لاگ یا پنل می‌رود رمز را با خودش نمی‌برد. */
  function hide(text, tok) {
    let s = String(text == null ? '' : text);
    if (tok) s = s.split(tok).join('***');
    return s.replace(/bot\d{5,}:[A-Za-z0-9_-]{20,}/g, 'bot***').slice(0, 300);
  }

  /**
   * یک فراخوانِ API تلگرام.
   *
   * ⚠️ هرگز استثنا بیرون نمی‌دهد: خطای شبکه همان شکلِ پاسخِ تلگرام را
   * می‌گیرد (`ok:false`, `error_code:0`)، تا صدا‌زننده یک راه برای خطا داشته
   * باشد، نه دو.
   */
  async function call(method, params = {}, { tok = null, timeoutMs = 15000 } = {}) {
    const t = tok || await token();
    if (!t) return { ok: false, error_code: 0, description: 'رمزِ بات تنظیم نشده است' };
    try {
      if (transport) return await transport(method, params, t);
      const res = await fetch(`${config.telegram.apiBase}/bot${t}/${method}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(params),
        signal: AbortSignal.any([AbortSignal.timeout(timeoutMs), stopSignal()]),
      });
      let json = null;
      try { json = await res.json(); } catch { /* پاسخِ غیرِ JSON */ }
      return json && typeof json === 'object'
        ? json
        : { ok: false, error_code: res.status, description: `پاسخِ نامعتبر (${res.status})` };
    } catch (err) {
      return { ok: false, error_code: 0, description: hide(err.cause?.message || err.message, t) };
    }
  }

  /** یک پیامِ متنی. ⛔ بی `parse_mode` — بالا را ببینید. */
  function send(chatId, text, keyboard = null) {
    const params = {
      chat_id: String(chatId),
      text: String(text).slice(0, 4000),
      disable_web_page_preview: true,
    };
    if (keyboard) params.reply_markup = { inline_keyboard: keyboard };
    return call('sendMessage', params);
  }

  /**
   * فرستادنِ یک فایل (عکس یا PDF) — `multipart/form-data`.
   * ⛔ فایل فقط از حافظه می‌رود؛ هیچ‌جا روی دیسک نوشته نمی‌شود. همان شکلِ پاسخِ `call`.
   * ⚠️ در آزمون `transport(method, params, tok, files)` صدا زده می‌شود.
   */
  async function callFile(method, params, field, file, { timeoutMs = 90000 } = {}) {
    const t = await token();
    if (!t) return { ok: false, error_code: 0, description: 'رمزِ بات تنظیم نشده است' };
    try {
      if (transport) return await transport(method, params, t, { [field]: file });
      const form = new FormData();
      for (const [k, v] of Object.entries(params)) {
        if (v === undefined || v === null) continue;
        form.append(k, typeof v === 'object' ? JSON.stringify(v) : String(v));
      }
      form.append(field, new Blob([file.data], { type: file.mime }), file.name);
      const res = await fetch(`${config.telegram.apiBase}/bot${t}/${method}`, {
        method: 'POST',
        body: form,
        signal: AbortSignal.any([AbortSignal.timeout(timeoutMs), stopSignal()]),
      });
      let json = null;
      try { json = await res.json(); } catch { /* پاسخِ غیرِ JSON */ }
      return json && typeof json === 'object'
        ? json
        : { ok: false, error_code: res.status, description: `پاسخِ نامعتبر (${res.status})` };
    } catch (err) {
      return { ok: false, error_code: 0, description: hide(err.cause?.message || err.message, t) };
    }
  }

  /** نامِ بات را از خودِ تلگرام می‌پرسد و نگه می‌دارد. */
  async function refreshMe(tok = null) {
    const res = await call('getMe', {}, { tok });
    if (res.ok && res.result?.username) {
      bot.username = String(res.result.username);
      await plans.setConfig(CFG_USERNAME, bot.username);
    }
    return res;
  }

  async function username() {
    if (bot.username) return bot.username;
    bot.username = await plans.getConfig(CFG_USERNAME, '');
    return bot.username;
  }

  /* ══════════════════════════════════════════════════════════════════
     ابزارهای کوچک
     ══════════════════════════════════════════════════════════════════ */

  const FA_DIGITS = '۰۱۲۳۴۵۶۷۸۹';
  const AR_DIGITS = '٠١٢٣٤٥٦٧٨٩';

  /** رقمِ فارسی و عربی ⇒ لاتین. کاربرِ افغان با صفحه‌کلیدِ فارسی کد را می‌زند. */
  function asciiDigits(s) {
    return String(s || '').replace(/[۰-۹٠-٩]/g, (ch) => {
      const i = FA_DIGITS.indexOf(ch);
      return String(i >= 0 ? i : AR_DIGITS.indexOf(ch));
    });
  }

  function faNum(n) {
    return String(n).replace(/\d/g, (d) => FA_DIGITS[Number(d)]);
  }

  /** ایمیلِ تمیز یا رشتهٔ خالی. */
  function normEmail(raw) {
    const s = String(raw || '').trim().toLowerCase();
    if (s.length > 160) return '';
    return /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(s) ? s : '';
  }

  function ago(ms) {
    const d = Math.max(0, now() - Number(ms));
    if (d < 60_000) return 'همین حالا';
    if (d < 3_600_000) return `${faNum(Math.floor(d / 60_000))} دقیقه پیش`;
    if (d < 86_400_000) return `${faNum(Math.floor(d / 3_600_000))} ساعت پیش`;
    return `${faNum(Math.floor(d / 86_400_000))} روز پیش`;
  }

  /**
   * فرمانِ تلگرام: `/start`، `/start@MyBot abc`.
   *
   * ⚠️ فرمانی که به باتِ **دیگری** نشانی دارد (در گروهِ چندباته) مالِ ما
   * نیست و نادیده گرفته می‌شود.
   */
  function parseCommand(text, me = '') {
    const m = /^\/([A-Za-z_]+)(?:@([A-Za-z0-9_]+))?(?:\s+([\s\S]*))?$/.exec(String(text || '').trim());
    if (!m) return null;
    if (m[2] && me && m[2].toLowerCase() !== me.toLowerCase()) return null;
    return { name: m[1].toLowerCase(), arg: String(m[3] || '').trim() };
  }

  function hashToken(raw) {
    return crypto.createHash('sha256').update(String(raw)).digest('hex');
  }

  /* ══════════════════════════════════════════════════════════════════
     دکمه‌ها و «صفحه‌ها»
     ══════════════════════════════════════════════════════════════════

     ⛔ گزارشِ صاحب سامانه (۱۴۰۵/۰۷/۱۴، با دو عکس): «محض این که چیزی
     می‌گه چندین دکمه میان زیرش و خیلی شلوغی به وجود میاره… من برای یک
     پیام منو رو باز کردم ولی بعدِ هر پیام منو هم پشتِ سرش است.»

     سه قاعده، و هر سه آزمون دارند:

     ۱) **دکمه صفحه را عوض می‌کند، پیامِ تازه نمی‌سازد.** هر دکمهٔ منو همان
        پیام را ویرایش می‌کند (`editMessageText`)، پس گفت‌وگو با هر کلیک یک
        پیام بلندتر نمی‌شود.
     ۲) **فقط یک منوی دکمه‌دار در هر گفت‌وگو.** وقتی منوی تازه‌ای فرستاده
        می‌شود، دکمه‌های منوی قبلی برداشته می‌شوند (`menu_msg_id`).
     ۳) **پیامِ هشدار، پاسخِ جست‌وجو و پیامِ «وصل شد» هیچ دکمه‌ای ندارند.**
        منو فقط با `/menu` یا دکمهٔ «منو»ی خودِ تلگرام می‌آید.

     منوی اصلی چهار دکمه است در دو ردیف — نه شش ردیفِ پیشین.
  */

  /**
   * لینکِ اپِ کارمندان — همان دو نشانیِ `KarLink.ApkUrl`/`IphoneUrl`ِ برنامهٔ
   * پمپ، روی دامنهٔ خودِ پمپ.
   *
   * ⛔ فایلِ نصبِ سایتِ قدیم (`PumpYaqobi.apk`) هیچ‌وقت داده نمی‌شود —
   * قاعدهٔ ریپوی پمپ: «فایلِ نصبِ اندروید فقط یکی است».
   */
  function appLinks() {
    const site = config.telegram.pumpSite;
    return {
      android: `${site}/downloads/PumpYaqobiKar.apk`,
      iphone: `${site}/kar/`,
    };
  }

  function downloadRow() {
    const l = appLinks();
    return [
      { text: '📱 اندروید', url: l.android },
      { text: '🍎 آیفون', url: l.iphone },
    ];
  }

  const BACK = { text: '‹ منو', callback_data: 'menu' };

  /**
   * 🏷️ دکمهٔ نرخ: ⛔ **هیچ پیامی نمی‌فرستد** — قالبِ «نرخ پطرول … دیزل …» را در
   * کادرِ نوشتنِ همان گفت‌وگو می‌گذارد (`switch_inline_query_current_chat`)؛ کاربر
   * عددها را عوض می‌کند و خودش می‌فرستد. خواستهٔ صاحب سامانه (۱۴۰۵/۰۷/۲۰): «از منو
   * که انتخاب می‌کند نباید پیام فرستاده شود؛ قالبِ آماده توی پیام بیاید».
   * ⚠️ `/rate` از فهرستِ «/»ِ تلگرام برداشته شد: تلگرام فرمانِ آن فهرست را همان
   * لحظه می‌فرستد و راهی برای «فقط بنویس» ندارد.
   */
  //  بی تاریخچه: جای عدد خالی است (نه «۰» که رد می‌شود) — فقط عددِ همان سوخت را بنویسید
  const EMPTY_RATE = 'نرخ پطرول  دیزل ';

  function rateButton(template) {
    return { text: '🏷️ نرخِ اتحادیه', switch_inline_query_current_chat: template || EMPTY_RATE };
  }

  /** قالبِ نرخ با آخرین نرخِ نشسته (یک شعبه)، تا فقط عددِ تازه عوض شود. */
  async function rateTemplate(links) {
    if (!links || links.length !== 1) return EMPTY_RATE;
    try {
      const list = await req('./station-rates').recent(links[0].station_id, 10);
      const last = list.find((r) => r.status === 'applied') || list[0];
      if (!last) return EMPTY_RATE;
      const v = (n) => (n === null || n === undefined ? '۰' : faNum(n));
      return `نرخ پطرول ${v(last.petrol)} دیزل ${v(last.diesel)}`;
    } catch { return EMPTY_RATE; }
  }

  function menuKeyboard(row, template = '') {
    if (row.kind === 'channel') return null;
    const top = [
      { text: '📊 وضعیت', callback_data: 'status' },
      { text: '📱 اپ و کدِ پمپ', callback_data: 'app' },
    ];
    const mirza = [{ text: '💬 چت‌های میرزا', callback_data: 'mirza' }, rateButton(template)];
    if (row.kind === 'private') {
      return [top, [
        { text: '👥 گروه و کانال', callback_data: 'share' },
        { text: '⚙️ تنظیمات', callback_data: 'settings' },
      ], mirza];
    }
    return [top, [{ text: '⚙️ تنظیمات (مدیرِ گروه)', callback_data: 'settings' }], mirza];
  }

  /* ══════════════════════════════════════════════════════════════════
     گفت‌وگوها
     ══════════════════════════════════════════════════════════════════ */

  function isLinked(row) {
    return Boolean(row && row.station_id && row.linked_at);
  }

  function titleOf(chat) {
    if (chat.title) return String(chat.title).slice(0, 120);
    return [chat.first_name, chat.last_name].filter(Boolean).join(' ').slice(0, 120);
  }

  async function chatRow(chatId) {
    return one('SELECT * FROM telegram_chats WHERE chat_id=$1', [String(chatId)]);
  }

  async function ensureChat(chat, kind) {
    const t = now();
    return one(
      `INSERT INTO telegram_chats (chat_id, kind, title, created_at, updated_at)
       VALUES ($1,$2,$3,$4,$4)
       ON CONFLICT (chat_id) DO UPDATE SET title=excluded.title, kind=excluded.kind, updated_at=excluded.updated_at
       RETURNING *`,
      [String(chat.id), kind, titleOf(chat), t]
    );
  }

  async function setState(chatId, stateName, email = '') {
    await query(
      'UPDATE telegram_chats SET state=$2, pending_email=$3, updated_at=$4 WHERE chat_id=$1',
      [String(chatId), stateName, email, now()]
    );
  }

  /**
   * پمپ‌هایی (شعبه‌هایی) که این گفت‌وگو به آن‌ها وصل است — **فقط آن‌هایی که
   * هنوز معتبرند**.
   *
   * خواستهٔ صاحب سامانه (۱۴۰۵/۰۷/۱۴): «یکی شاید چندین شعبه داشته باشه و
   * می‌خواد همه رو توی همون گروه یا کانال ببینه… حساب‌هاشون قاطی نشن.» هر
   * پیوند یک ردیفِ `telegram_chat_links` است با کلیدِ (گفت‌وگو، پمپ)؛ پس هر
   * شعبه جداست و هیچ پاسخی دادهٔ دو شعبه را یکی نمی‌کند.
   *
   * ⛔ همان سنجشِ قیدِ ۳: عضوِ فعال، پمپِ فعال، حسابِ فعال — برای هر پیوند.
   */
  /*
   *  ⛔ باتِ تلگرام فقط برای پمپی که «بات» در پلنش دارد (۱۴۰۵/۰۷/۲۰): استاندارد
   *  و دائمیِ بی‌تمدیدِ خدمات هیچ پاسخ و هیچ هشداری نمی‌گیرند. یک جا، برای هر
   *  فهرستِ شعبه — ‎lib/pump-services.js‎. خطای سنجش ⇒ آن شعبه نه (بستن امن‌تر است).
   */
  async function botStations(rows) {
    const ent = req('./entitlement').pump;
    const out = [];
    const seen = new Map();
    for (const r of rows) {
      const sid = r.station_id;
      if (!seen.has(sid)) {
        let ok = false;
        try { ok = (await ent.entitlementOf(sid)).features.includes('bot'); } catch { ok = false; }
        seen.set(sid, ok);
      }
      if (seen.get(sid)) out.push(r);
    }
    return out;
  }

  async function linksOf(row) {
    if (!row || !row.chat_id) return [];
    return botStations(await many(
      `SELECT l.station_id, l.user_id, s.name AS station_name, u.email, m.role
         FROM telegram_chat_links l
         JOIN stations s ON s.id=l.station_id AND s.status='active'
         JOIN station_members m ON m.station_id=l.station_id AND m.user_id=l.user_id AND m.status='active'
         JOIN users u ON u.id=l.user_id AND u.status='active'
        WHERE l.chat_id=$1
        ORDER BY l.linked_at, l.station_id`,
      [String(row.chat_id)]
    ));
  }

  /** نخستین شعبهٔ معتبر — برای جاهایی که یکی کافی است. */
  async function linkOf(row) {
    if (!isLinked(row)) return null;
    return (await linksOf(row))[0] || null;
  }

  /** `telegram_chats.station_id/user_id` همیشه نخستین پیوند است (سازگاری). */
  async function syncPrimary(chatId) {
    const id = String(chatId);
    const first = await one(
      `SELECT station_id, user_id, linked_at FROM telegram_chat_links
        WHERE chat_id=$1 ORDER BY linked_at, station_id LIMIT 1`,
      [id]
    );
    await query(
      'UPDATE telegram_chats SET station_id=$2, user_id=$3, linked_at=$4, updated_at=$5 WHERE chat_id=$1',
      [id, first ? first.station_id : null, first ? first.user_id : null, first ? first.linked_at : null, now()]
    );
  }

  /** یک شعبه به این گفت‌وگو — شعبه‌های دیگرش دست نمی‌خورند. `true` یعنی تازه بود. */
  async function addLink(chatId, stationId, userId, t = now()) {
    const had = await one('SELECT 1 FROM telegram_chat_links WHERE chat_id=$1 AND station_id=$2',
      [String(chatId), stationId]);
    await query(
      `INSERT INTO telegram_chat_links (chat_id, station_id, user_id, linked_at)
       VALUES ($1,$2,$3,$4)
       ON CONFLICT (chat_id, station_id) DO UPDATE SET user_id=excluded.user_id`,
      [String(chatId), stationId, userId, t]
    );
    await syncPrimary(chatId);
    return !had;
  }

  /** یک شعبه از این گفت‌وگو جدا — بقیه می‌مانند. */
  async function removeLink(chatId, stationId) {
    await query('DELETE FROM telegram_chat_links WHERE chat_id=$1 AND station_id=$2', [String(chatId), stationId]);
    await query(
      `DELETE FROM telegram_outbox
        WHERE chat_id=$1 AND station_id=$2 AND sent_at IS NULL AND failed_at IS NULL`,
      [String(chatId), stationId]
    );
    await syncPrimary(chatId);
  }

  async function unlink(chatId) {
    await query('DELETE FROM telegram_chat_links WHERE chat_id=$1', [String(chatId)]);
    await query(
      `UPDATE telegram_chats
          SET station_id=NULL, user_id=NULL, linked_at=NULL, state='', pending_email='', updated_at=$2
        WHERE chat_id=$1`,
      [String(chatId), now()]
    );
    await query(
      'DELETE FROM telegram_outbox WHERE chat_id=$1 AND sent_at IS NULL AND failed_at IS NULL',
      [String(chatId)]
    );
  }

  /** بات را از گروه بیرون کردند یا کاربر بات را بست — هیچ‌چیزی از آن نمی‌ماند. */
  async function forgetChat(chatId) {
    await query('DELETE FROM telegram_outbox WHERE chat_id=$1 AND sent_at IS NULL', [String(chatId)]);
    await query('DELETE FROM telegram_chats WHERE chat_id=$1', [String(chatId)]);
  }

  /** گروهِ ساده که «ابرگروه» شد شناسهٔ تازه می‌گیرد؛ پیوند نباید گم شود. */
  async function migrateChat(oldId, newId_) {
    const from = String(oldId);
    const to = String(newId_);
    if (!to || from === to) return;
    await query('DELETE FROM telegram_chats WHERE chat_id=$1 AND station_id IS NULL', [to]);
    await query('UPDATE telegram_chats SET chat_id=$2, updated_at=$3 WHERE chat_id=$1', [from, to, now()])
      .catch(() => {});
    await query('UPDATE telegram_outbox SET chat_id=$2 WHERE chat_id=$1 AND sent_at IS NULL', [from, to]);
  }

  /* ── نشاندنِ یک «صفحه» ────────────────────────────────────────── */

  /**
   * آخرین پیامِ دکمه‌دارِ این گفت‌وگو را به‌یاد می‌سپارد و دکمه‌های قبلی را
   * برمی‌دارد (قاعدهٔ ۲). هر خطایی بلعیده می‌شود: پیامی که کاربر پاکش کرده
   * یا کهنه‌تر از ۴۸ ساعت است دیگر ویرایش نمی‌شود، و این نباید چیزی را بشکند.
   */
  async function rememberMenu(chatId, messageId) {
    const id = Number(messageId) || 0;
    if (!id) return;
    const row = await chatRow(chatId);
    const old = row ? Number(row.menu_msg_id) || 0 : 0;
    if (old && old !== id) {
      await call('editMessageReplyMarkup', {
        chat_id: String(chatId), message_id: old, reply_markup: { inline_keyboard: [] },
      }).catch(() => {});
    }
    if (row) {
      await query('UPDATE telegram_chats SET menu_msg_id=$2 WHERE chat_id=$1', [String(chatId), id]);
    }
  }

  /**
   * یک صفحه (متن + دکمه): اگر از دکمه آمده، **همان پیام** ویرایش می‌شود؛
   * وگرنه پیامِ تازه، و منوی قبلی دکمه‌هایش را از دست می‌دهد.
   *
   * ⚠️ «message is not modified» خطا نیست: کاربر دوبار روی همان دکمه زده.
   */
  async function show(chatId, text, keyboard, editId = 0) {
    const res = await showOnce(chatId, text, keyboard, editId);
    //  ⚠️ اگر تلگرام دکمهٔ «قالب در کادرِ نوشتن» را نپذیرفت (مثلاً حالتِ inlineِ ربات
    //  خاموش است)، همان دکمه به «🏷️ نرخ» با راهنما (‎rate_help‎) بدل می‌شود — منو نمی‌افتد.
    if (!res.ok && keyboard && keyboard.some((r) => r.some((b) => b.switch_inline_query_current_chat !== undefined))) {
      const plain = keyboard.map((r) => r.map((b) => (b.switch_inline_query_current_chat !== undefined
        ? { text: b.text, callback_data: 'rate_help' } : b)));
      return showOnce(chatId, text, plain, editId);
    }
    return res;
  }

  async function showOnce(chatId, text, keyboard, editId = 0) {
    const parts = chunks(text);
    if (parts.length > 1) {
      //  متنِ بلند: تکهٔ اول جای همان پیام، میانی‌ها پیامِ تازه، و دکمه‌ها زیرِ آخری
      if (editId) {
        await call('editMessageText', {
          chat_id: String(chatId), message_id: Number(editId), text: parts[0],
          disable_web_page_preview: true, reply_markup: { inline_keyboard: [] },
        });
      } else {
        await send(chatId, parts[0]);
      }
      for (const mid of parts.slice(1, -1)) await send(chatId, mid);
      const last = parts[parts.length - 1];
      const res = await send(chatId, last, keyboard && keyboard.length ? keyboard : null);
      if (res.ok && keyboard && keyboard.length) await rememberMenu(chatId, res.result?.message_id);
      return res;
    }
    const body = parts[0];
    const markup = { inline_keyboard: keyboard || [] };
    if (editId) {
      const res = await call('editMessageText', {
        chat_id: String(chatId), message_id: Number(editId), text: body,
        disable_web_page_preview: true, reply_markup: markup,
      });
      if (res.ok || /not modified/i.test(String(res.description || ''))) {
        if (keyboard && keyboard.length) await rememberMenu(chatId, editId);
        return res;
      }
    }
    const res = await send(chatId, body, keyboard && keyboard.length ? keyboard : null);
    if (res.ok && keyboard && keyboard.length) await rememberMenu(chatId, res.result?.message_id);
    return res;
  }

  /* ── خصوصی ─────────────────────────────────────────────────────── */

  const WELCOME =
    'سلام 👋 این باتِ پمپ است — تازه آمده‌اید؟ از همین‌جا شروع کنید.\n\n'
    + 'این بات سه کار می‌کند:\n'
    + '🚨 هشدار: حسابِ قرض‌داری تمام شد یا کم ماند، یا مخزن ته کشید — همین‌جا خبر می‌گیرید، حتی وقتی برنامه بسته است.\n'
    + '💬 چت‌های میرزا: وقتی برنامهٔ کامپیوتر خاموش است، پیامِ مشتری‌های کیو‌آر همین‌جا می‌آید و جوابشان را می‌دهید.\n'
    + '🏷️ نرخِ اتحادیه: «مدیرانِ نرخ»ی که صاحبِ پمپ تعیین کرده بنویسند «پطرول ۷۹ دیزل ۸۰» و در برنامهٔ کامپیوترِ پمپ می‌نشیند.\n\n'
    + 'گامِ ۱ — ✉️ ایمیلِ حسابِ پمپتان را همین‌جا بفرستید (همان که در برنامهٔ پمپ با آن وارد شده‌اید).\n'
    + 'گامِ ۲ — 🔢 یک کدِ شش‌رقمی به همان ایمیل می‌رود (پوشهٔ اسپم را هم ببینید)؛ کد را همین‌جا بفرستید. تمام.\n\n'
    + 'فقط می‌خواهید پیامِ مشتری‌ها را بگیرید؟ «💬 چت‌های میرزا» را بزنید — آن‌جا فقط کدِ هشت‌رقمیِ پمپ لازم است.';

  /** راهنما — برای کسی که هنوز وصل نیست هم هست. */
  const HELP =
    '📖 راهنمای بات — قدم‌به‌قدم\n\n'
    + '✉️ وصل شدن: ایمیلِ حسابِ پمپ را بفرستید ⇐ کدِ شش‌رقمیِ ایمیل را همین‌جا بزنید.\n\n'
    + '💬 چت‌های میرزا (پیامِ مشتری‌های کیو‌آر):\n'
    + '  ۱) «💬 چت‌های میرزا» ⇐ «➕ وصل کردنِ پمپ» را بزنید.\n'
    + '  ۲) کدِ هشت‌رقمیِ پمپ را بفرستید (برنامهٔ کامپیوتر ← پروفایل ← کدِ اپِ کارمندان).\n'
    + '  ۳) وقتی برنامهٔ کامپیوتر خاموش است، پیامِ مشتری این‌جا می‌آید. روی آن «Reply» بزنید و بنویسید — جواب به گوشیِ همان مشتری می‌رسد.\n'
    + '  در گروه می‌خواهید؟ بات را به گروه اضافه کنید، در گروه /mirza بزنید و همان کد را بفرستید (فقط مدیرِ گروه).\n\n'
    + '🏷️ نرخِ اتحادیه: در منو «🏷️ نرخِ اتحادیه» را بزنید — قالب در کادرِ نوشتن می‌نشیند و چیزی فرستاده نمی‌شود؛ عددهای تازه را بنویسید و بفرستید. یا خودتان بنویسید «پطرول ۷۹ دیزل ۸۰» (یکی هم کافی است).\n'
    + '  فقط «مدیرانِ نرخ»ی که صاحبِ پمپ تعیین کرده (⚙️ تنظیمات ← 👮 مدیرانِ نرخ) — هر کسِ دیگری بنویسد هیچ اتفاقی نمی‌افتد.\n'
    + '  شناسهٔ تلگرامِ هر کس: /myid. در گروه با واژهٔ «نرخ» یا /rate. نشست، خبر می‌دهم.\n\n'
    + '🔎 جست‌وجو: نامِ قرض‌دار را بنویسید (در گروه: /find نام).\n'
    + '📊 وضعیت: هشدارهای بازِ همین حالا و موجودیِ مخزن.\n'
    + '📱 اپ و کدِ پمپ: لینکِ اپِ اندروید و آیفون، و کدی که کارمندان در اپ می‌زنند.\n'
    + '👥 گروه و کانال: هشدارها در گروه یا کانالِ تلگرامِ شما هم بیاید.\n'
    + '⚙️ تنظیمات: فقط «تمام شد» بیاید، شعبهٔ دیگر، یا جدا شدن.\n\n'
    + 'منو همیشه با /menu یا دکمهٔ «منو»ی پایینِ صفحه می‌آید. انصراف از هر کاری: /cancel';

  function welcomeKeyboard() {
    return [downloadRow(), [
      { text: '💬 چت‌های میرزا', callback_data: 'mirza' },
      { text: '❓ راهنما', callback_data: 'help' },
    ]];
  }

  /** وقتی گفت‌وگو دیگر به هیچ پمپی وصل نیست — همان خوش‌آمد، با دلیلش. */
  async function notLinked(row, editId = 0) {
    const was = isLinked(row);
    if (was) await unlink(row.chat_id);
    if (row.kind === 'private') {
      await setState(row.chat_id, 'email');
      return show(row.chat_id,
        (was ? 'این گفت‌وگو دیگر به هیچ پمپی وصل نیست — حساب از آن پمپ بیرون شده است.\n\n' : '')
        + WELCOME, welcomeKeyboard(), editId);
    }
    return send(row.chat_id,
      'این گروه به هیچ پمپی وصل نیست. در گفت‌وگوی خصوصی با بات، «👥 گروه و کانال» را بزنید.');
  }

  /** هشدارها کجا می‌آیند — همان قاعدهٔ `alertChats`، به زبانِ آدم. */
  async function whereText(row) {
    const g = await groupOfUser(row);
    if (g) return `📣 هشدارها در «${g}» می‌آیند، نه این‌جا. (جدا کردنِ گروه ⇐ هشدارها دوباره این‌جا.)`;
    return row.only_out ? '🔕 فقط هشدارهای «تمام شد» می‌آید.' : '🔔 همهٔ هشدارها می‌آید: «تمام شد» و «کم مانده».';
  }

  async function menuText(row, links) {
    const ps = req('./pump-state');
    let head;
    if (links.length === 1) {
      const state = await ps.get(links[0].station_id);
      const open = state ? state.alerts.length : 0;
      head = `⛽ پمپِ «${links[0].station_name}»\n`
        + (open ? `🚨 ${faNum(open)} هشدارِ باز — «📊 وضعیت» را بزنید.\n` : '✅ همین حالا هشدارِ بازی نیست.\n');
    } else {
      //  چند شعبه: هر کدام خطِ خودش — هیچ عددی با هم جمع نمی‌شود
      const lines = [`⛽ ${faNum(links.length)} شعبه در همین ${row.kind === 'private' ? 'گفت‌وگو' : (row.kind === 'channel' ? 'کانال' : 'گروه')}:`];
      for (const l of links) {
        const state = await ps.get(l.station_id);
        const open = state ? state.alerts.length : 0;
        lines.push(`🏷️ «${l.station_name}» — ${open ? `🚨 ${faNum(open)} هشدارِ باز` : '✅ بی هشدار'}`);
      }
      head = `${lines.join('\n')}\n`;
    }
    return head
      + await whereText(row)
      + (row.kind === 'private'
        ? '\n\n🔎 برای جست‌وجو، نامِ قرض‌دار را همین‌جا بنویسید.'
        : (row.kind === 'group' ? '\n\n🔎 جست‌وجو: /find نامِ قرض‌دار' : ''));
  }

  async function sendMenu(row, lead = '', editId = 0) {
    const links = isLinked(row) ? await linksOf(row) : [];
    if (!links.length) return notLinked(row, editId);
    const text = (lead ? `${lead}\n\n` : '') + await menuText(row, links);
    //  ⚠️ در کانال دکمه نمی‌گذاریم: هر خواننده‌ای می‌دیدش
    if (row.kind === 'channel') return send(row.chat_id, text);
    return show(row.chat_id, text, menuKeyboard(row, await rateTemplate(links)), editId);
  }

  /* ── وضعیت: حالِ زنده، نه فقط تاریخچه ─────────────────────────── */

  const STALE_MS = 30 * 60 * 1000;

  function fmtNum(n) {
    const v = Math.round(Number(n) || 0);
    return (v < 0 ? '−' : '') + faNum(Math.abs(v).toLocaleString('en-US')).replace(/,/g, '٬');
  }

  function alertLine(a) {
    const out = a.s === 'out';
    const who = shiftIcon(a.k) || (String(a.k || '').startsWith('tank-') ? '🛢️' : '👤');
    return `${who}${out ? '🔴' : '🟡'} ${a.t || a.n || ''}`;
  }

  /**
   * متنِ «وضعیت».
   *
   * ⛔ **راستش را می‌گوید**: اگر برنامهٔ کامپیوتر هرگز حالش را نفرستاده (نسخهٔ
   * کهنه، یا به سرور بند نشده) یا مدتی است خبری نداده، همین را می‌گوید —
   * نه «هیچ هشداری نیست». سکوتِ برنامه با «همه‌چیز خوب است» یکی نیست.
   */
  async function statusText(link) {
    const state = await req('./pump-state').get(link.station_id);
    const head = `📊 پمپِ «${link.station_name}»\n`;
    if (!state) {
      const list = await many(
        `SELECT kind, title, data, created_at FROM station_events
          WHERE station_id=$1 AND kind = ANY($2::text[])
          ORDER BY created_at DESC LIMIT 8`,
        [link.station_id, ALERT_KINDS]
      );
      const lines = list.map(e => `${iconOf(e)} ${e.title || ''} — ${ago(e.created_at)}`);
      return head
        + '⚠️ برنامهٔ کامپیوترِ این پمپ هنوز حالِ زنده‌اش را به سرور نفرستاده است — '
        + 'برنامهٔ پمپ را به‌روز کنید و مطمئن شوید با حسابِ همین پمپ وارد شده است.\n\n'
        + (lines.length ? `آخرین هشدارهایی که آمده:\n${lines.join('\n')}` : 'هنوز هیچ هشداری نیامده است.');
    }

    const parts = [head];
    const age = now() - state.updatedAt;
    parts.push(age > STALE_MS
      ? `⚠️ برنامهٔ کامپیوتر ${ago(state.updatedAt)} آخرین بار خبر داد — شاید بسته است.`
      : `🖥️ برنامهٔ کامپیوتر ${ago(state.updatedAt)} خبر داد.`);

    const tanks = tankLines(state.tank);
    if (tanks.length) parts.push('', '🛢️ مخزن:', ...tanks);

    parts.push('');
    if (state.alerts.length) {
      parts.push(`🚨 هشدارهای باز (${faNum(state.alerts.length)}):`);
      for (const a of state.alerts) parts.push(alertLine(a));
    } else {
      parts.push('✅ همین حالا هیچ هشدارِ بازی نیست.');
    }
    const owe = oweLines(state.owe);
    if (owe.length) parts.push('', '💼 بدهیِ پمپ به شرکت‌ها:', ...owe);
    const todo = actionLines(state.alerts.map(a => a.a));
    if (todo.length) parts.push('', '📋 دستورِ کار:', ...todo);
    return parts.join('\n');
  }

  /** هر مخزن در یک خط: چند لیتر مانده و حدِ هشدار — نه فقط یک عدد. */
  function tankLines(tank) {
    const out = [];
    for (const [key, label] of [['petrol', 'پطرول'], ['diesel', 'دیزل']]) {
      const t = tank?.[key];
      if (!t) continue;
      const mark = t.low ? '🔴' : (t.near ? '🟡' : '🟢');
      const word = Number(t.show) <= 0 ? 'خالی است' : (t.low ? 'کم است' : (t.near ? 'نزدیکِ حد است' : 'پُر است'));
      const lim = Number(t.threshold) > 0 ? ` (حدِ هشدار ${fmtNum(t.threshold)} لیتر)` : '';
      out.push(`${mark} ${label}: ${fmtNum(t.show)} لیتر مانده — ${word}${lim}`);
    }
    return out;
  }

  /** بدهیِ پمپ به هر شرکت — همان الباقی‌ای که برنامه حساب کرده. */
  function oweLines(owe) {
    return (Array.isArray(owe) ? owe : []).map((o) => {
      const parts = [];
      if (Number(o.afn) > 0) parts.push(`${fmtNum(o.afn)} افغانی`);
      if (Number(o.usd) > 0) parts.push(`${fmtNum(o.usd)} دالر`);
      return `▫️ ${o.n}: ${parts.join(' · ')}`;
    });
  }

  /** میانِ دو شعبه — تا هیچ خطی از یک شعبه زیرِ شعبهٔ دیگر خوانده نشود. */
  const BRANCH_SEP = '\n\n━━━━━━━━━━\n\n';

  /* ── 📷 عکس و PDF از خودِ برنامهٔ کامپیوتر (۱۴۰۵/۰۷/۲۱) ─────────────

     خواستهٔ صاحب سامانه: «از مخزن‌ها همین عکس باشد و پیام زیرش، و شرکت‌های تیل و
     خریدها با یک فایل پایینش… حسابِ قرض‌دار: عکس با رسید، بردگی، الباقی و واحد،
     و PDF و لینکِ کیو‌آرش… پیام ساده… بشود به واتساپ و جاهای دیگر هم فرستاد.»

     بات ⇒ `station-reports.request` (سرورِ حساب، فقط حافظه) ⇒ برنامهٔ کامپیوترِ
     همان پمپ ⇒ عکس و PDF ⇒ همین‌جا ⇒ تلگرام. ⛔ هیچ چیز ذخیره نمی‌شود و هیچ نقاشی
     این‌جا نیست: عکس و سند همان است که خودِ برنامه می‌کشد و چاپ می‌کند. */

  /** یک درخواست در یک زمان برای هر گفت‌وگو، و دست‌کم این فاصله میانِ دو درخواست. */
  const REPORT_GAP_MS = 4000;
  const reportBusy = new Map();

  /** دکمه‌های نتیجهٔ جست‌وجو: نام ⇒ نشانیِ کوتاه (نام ممکن است از ۶۴ بایت بلندتر باشد). */
  const nameRefs = new Map();
  const NAME_REF_TTL_MS = 6 * 3600 * 1000;
  function nameRef(stationId, name) {
    const t = now();
    for (const [k, v] of nameRefs) if (t - v.at > NAME_REF_TTL_MS) nameRefs.delete(k);
    if (nameRefs.size > 2000) nameRefs.delete(nameRefs.keys().next().value);
    const k = crypto.randomBytes(6).toString('base64url');
    nameRefs.set(k, { stationId, name: String(name), at: t });
    return k;
  }

  /** پاسخِ برنامه ⇒ تلگرام: عکس‌ها (اولی با متن)، بعد PDFها، بعد لینکِ کیو‌آر. */
  async function deliverReport(chatId, out) {
    const files = Array.isArray(out?.files) ? out.files : [];
    const photos = files.filter(f => String(f.mime).startsWith('image/'));
    const docs = files.filter(f => f.mime === 'application/pdf');
    const caption = String(out?.caption || '').trim();
    const short = caption.length <= 1000;
    let captioned = false;
    for (const p of photos) {
      const res = await callFile('sendPhoto', {
        chat_id: String(chatId), ...(!captioned && short && caption ? { caption } : {}),
      }, 'photo', p);
      //  عکسِ خیلی بزرگ برای «عکس» ⇒ همان فایل به شکلِ سند (کیفیت دست نمی‌خورد)
      if (!res.ok) await callFile('sendDocument', { chat_id: String(chatId) }, 'document', p);
      captioned = captioned || (short && Boolean(caption));
    }
    if (caption && !captioned) await sendLong(chatId, caption);
    for (const d of docs) await callFile('sendDocument', { chat_id: String(chatId) }, 'document', d);
    const qr = String(out?.extra?.qr || '');
    if (/^https?:\/\//.test(qr)) {
      await send(chatId, `🔗 لینکِ حساب (همان کیو‌آر) — برای واتساپ یا هر جای دیگر:\n${qr}`);
    }
  }

  /**
   * یک درخواست از برنامهٔ کامپیوترِ یک شعبه. ⛔ `link` همیشه شعبه‌ای است که همین
   * گفت‌وگو به آن وصل است (`branch()`/`linksOf`) — هرگز شناسه‌ای خام از دکمه.
   */
  async function sendReport(row, link, kind, arg = {}) {
    const key = String(row.chat_id);
    const t = now();
    if ((reportBusy.get(key) || 0) > t) return send(row.chat_id, '⏳ درخواستِ قبلی هنوز در راه است؛ چند لحظه صبر کنید.');
    reportBusy.set(key, t + 120_000);
    try {
      let reports;
      try { reports = req('./station-reports'); } catch {
        return send(row.chat_id, '⚠️ سرورِ حساب هنوز این را ندارد؛ پس از به‌روزرسانیِ سرور دوباره بزنید.');
      }
      //  برنامه‌ای که مدتی است خبری نداده ⇒ همین حالا راستش را بگو، نه ۴۵ ثانیه انتظار
      const state = await req('./pump-state').get(link.station_id).catch(() => null);
      if (state && t - state.updatedAt > STALE_MS) {
        return send(row.chat_id, `🖥 برنامهٔ کامپیوترِ پمپِ «${link.station_name}» ${ago(state.updatedAt)} آخرین بار خبر داد — `
          + 'شاید بسته است. وقتی روشن شد دوباره بزنید.');
      }
      await call('sendChatAction', { chat_id: key, action: kind === 'tanks' ? 'upload_photo' : 'upload_document' });
      let out;
      try {
        out = await reports.request(link.station_id, kind, arg);
      } catch (err) {
        const why = err.code === 'timeout'
          ? `🖥 برنامهٔ کامپیوترِ پمپِ «${link.station_name}» جواب نداد — باز و به اینترنت وصل است؟ `
            + '(برنامه‌ای که هنوز به‌روز نشده هم جواب نمی‌دهد.)'
          : err.code === 'busy' ? '⏳ چند درخواست هنوز در راه است؛ کمی صبر کنید.'
          : `⚠️ ${String(err.message || 'نشد').slice(0, 300)}`;
        return send(row.chat_id, why);
      }
      return deliverReport(row.chat_id, out);
    } catch (err) {
      console.error('[telegram:report]', hide(err.message));
      return send(row.chat_id, '⚠️ نشد؛ دوباره امتحان کنید.');
    } finally {
      reportBusy.set(key, now() + REPORT_GAP_MS);
    }
  }

  /** «🛢 مخزن‌ها» بی شعبهٔ معیّن: یک شعبه ⇒ همان؛ چند شعبه ⇒ انتخاب. */
  async function askTanks(row, editId = 0) {
    const links = isLinked(row) ? await linksOf(row) : [];
    if (!links.length) return notLinked(row, editId);
    if (links.length === 1) return sendReport(row, links[0], 'tanks');
    return show(row.chat_id, '🛢 مخزن‌های کدام شعبه؟',
      links.map(l => [{ text: `⛽ ${l.station_name}`.slice(0, 40), callback_data: `tanks:${l.station_id}` }]), editId);
  }

  /** «/acct نام» — حسابِ یک قرض‌دار؛ چند شعبه ⇒ هر شعبه‌ای که این نام را دارد، دکمه. */
  async function askAcct(row, name) {
    const links = isLinked(row) ? await linksOf(row) : [];
    if (!links.length) return notLinked(row);
    const q = String(name || '').trim();
    if (!q) return send(row.chat_id, '📷 بعد از /acct نامِ قرض‌دار را بنویسید؛ مثلاً: /acct هارون');
    if (links.length === 1) return sendReport(row, links[0], 'debtor', { name: q });
    return show(row.chat_id, `📷 حسابِ «${q}» در کدام شعبه؟`,
      links.map(l => [{ text: `⛽ ${l.station_name}`.slice(0, 40), callback_data: `acctn:${nameRef(l.station_id, q)}` }]));
  }

  async function sendStatus(row, editId = 0) {
    const links = isLinked(row) ? await linksOf(row) : [];
    if (!links.length) return notLinked(row, editId);
    const texts = [];
    for (const l of links) texts.push(await statusText(l));
    const text = texts.join(BRANCH_SEP);
    const kb = row.kind === 'channel' ? null
      : [[{ text: '🛢 عکسِ مخزن‌ها', callback_data: 'tanks' }], [{ text: '🔄 تازه کن', callback_data: 'status' }, BACK]];
    if (!kb) return sendLong(row.chat_id, text);
    return show(row.chat_id, text, kb, editId);
  }

  /* ── جست‌وجوی قرض‌دار ─────────────────────────────────────────── */

  /** نامِ قابلِ مقایسه: «ي/ك» عربی، نیم‌فاصله و فاصلهٔ اضافه یکی می‌شوند. */
  function normName(s) {
    return String(s || '')
      .replace(/[يى]/g, 'ی').replace(/ك/g, 'ک').replace(/[ۀة]/g, 'ه')
      .replace(/[‌‍ً-ٟ]/g, '')
      .replace(/\s+/g, ' ').trim().toLowerCase();
  }

  const ST_LABEL = {
    out: '🔴 تمام شد — اضافه نده',
    low: '🟡 کم مانده',
    ok: '🟢 موجودی دارد',
  };

  function debtorLines(d) {
    const lines = [`👤 ${d.n}`];
    for (const [st, val, label, unit] of [
      [d.sp, d.p, 'پطرول', 'لیتر'], [d.sd, d.d, 'دیزل', 'لیتر'], [d.sm, d.m, 'پول', 'افغانی'],
    ]) {
      if (!ST_LABEL[st]) continue;
      lines.push(`   ${label}: ${ST_LABEL[st]} · الباقی ${fmtNum(val)} ${unit}`);
    }
    if (lines.length === 1) lines.push('   هنوز هیچ حسابی ندارد.');
    return lines.join('\n');
  }

  /**
   * «بات دنبالِ چیزی نمی‌گرده» — حالا نامِ قرض‌دار را در خلاصه‌ای که
   * برنامهٔ کامپیوتر فرستاده می‌گردد.
   *
   * ⛔ **هیچ عددی این‌جا حساب نمی‌شود**: حال و الباقی همان است که کارتِ
   * قرض‌دار در برنامه نشان می‌دهد (`StationSnapshot`). اگر برنامه فهرست را
   * نفرستاده، همین گفته می‌شود — «پیدا نشد»ِ دروغ نه.
   */
  async function branchHits(link, qn) {
    const state = await req('./pump-state').get(link.station_id);
    if (!state || !Array.isArray(state.debtors)) return null;
    const hits = state.debtors.filter(d => normName(d.n).includes(qn));
    //  دقیق‌ترین اول: نامِ برابر، بعد آن‌که با همین آغاز می‌شود
    hits.sort((a, b) => {
      const ra = normName(a.n) === qn ? 0 : (normName(a.n).startsWith(qn) ? 1 : 2);
      const rb = normName(b.n) === qn ? 0 : (normName(b.n).startsWith(qn) ? 1 : 2);
      return ra - rb || a.n.localeCompare(b.n, 'fa');
    });
    return { hits, at: state.debtorsAt || state.updatedAt };
  }

  /**
   * ⚠️ چند شعبه: هر شعبه جدا جست‌وجو و جدا نوشته می‌شود. «کریم»ِ شعبهٔ الف و
   * «کریم»ِ شعبهٔ ب دو حسابِ جدا هستند — هم‌نامی دلیلِ یکی بودن نیست، پس هرگز
   * با هم جمع یا یکی نمی‌شوند.
   */
  async function searchText(links, q) {
    const list = Array.isArray(links) ? links : [links];
    const qn = normName(q);
    if (qn.length < 2) return '🔎 دستِ‌کم دو حرف از نامِ قرض‌دار را بنویسید.';
    const shown = String(q).trim().slice(0, 60);
    const NOT_SENT = 'برنامهٔ کامپیوترِ این پمپ هنوز فهرستِ قرض‌داران را به سرور نفرستاده است — '
      + 'برنامهٔ پمپ را به‌روز و روشن کنید؛ چند دقیقه بعد جست‌وجو کار می‌کند.';

    if (list.length === 1) {
      const link = list[0];
      const r = await branchHits(link, qn);
      if (!r) return `🔎 ${NOT_SENT}`;
      if (!r.hits.length) return `🔎 «${shown}» در قرض‌دارانِ پمپِ «${link.station_name}» پیدا نشد.`;
      const count = r.hits.length > 1 ? ` — ${faNum(r.hits.length)} نفر` : '';
      return `🔎 پمپِ «${link.station_name}»${count}\n\n${r.hits.map(debtorLines).join('\n\n')}`
        + `\n\n🕒 ${ago(r.at)}`;
    }

    const sections = [];
    const missing = [];
    for (const link of list) {
      const r = await branchHits(link, qn);
      if (!r) { missing.push(link.station_name); continue; }
      if (!r.hits.length) continue;
      sections.push(`🏷️ شعبهٔ «${link.station_name}» — ${faNum(r.hits.length)} نفر\n\n`
        + `${r.hits.map(debtorLines).join('\n\n')}\n\n🕒 ${ago(r.at)}`);
    }
    const note = missing.length ? `\n\n⚠️ ${missing.map(n => `«${n}»`).join('، ')}: ${NOT_SENT}` : '';
    if (!sections.length) {
      return `🔎 «${shown}» در قرض‌دارانِ هیچ‌کدام از ${faNum(list.length)} شعبه پیدا نشد.${note}`;
    }
    return `🔎 «${shown}»\n\n${sections.join(BRANCH_SEP)}${note}`;
  }

  /* ── اپ و کدِ پمپ ────────────────────────────────────────────── */

  /**
   * «سرور و برنامه برای هر حساب یک کدِ هشت‌رقمی ساخته‌اند که به کارمندان
   * بدهند تا اپِ اندروید و آیفون را برای همان حساب باز کنند» — همان کدِ
   * دسترسیِ پمپ (`station-access`). بات فقط نشانش می‌دهد؛ ساختن و عوض
   * کردنش همان‌جای همیشگی است (پروفایلِ برنامهٔ کامپیوتر).
   *
   * ⛔ در گروه کد روی پیام نوشته نمی‌شود: فقط به مدیرِ گروه، در پنجرهٔ
   * خصوصیِ همان کلیک (`show_alert`) — کسی که تازه به گروه آمده نباید با یک
   * نگاه کلیدِ حساب‌های پمپ را ببیند.
   */
  async function accessCodeOf(stationId) {
    const access = req('./station-access');
    return access.format(await access.ensure(stationId));
  }

  async function sendApp(row, editId = 0) {
    const links = isLinked(row) ? await linksOf(row) : [];
    if (!links.length) return notLinked(row, editId);
    const common = '📱 اپِ کارمندانِ پمپ\n\n'
      + '۱) اپ را نصب کنید: اندروید (فایلِ نصب) یا آیفون (در Safari باز کنید و «افزودن به صفحهٔ اصلی»).\n'
      + '۲) کدِ پمپ را در اپ بزنید — حساب‌های همین پمپ باز می‌شود و با پمپ‌های دیگر قاطی نمی‌شود.\n'
      + '۳) رمزِ اپ همان رمزِ برنامهٔ کامپیوتر است.';
    if (row.kind === 'private') {
      if (links.length === 1) {
        const code = await accessCodeOf(links[0].station_id);
        return show(row.chat_id,
          `${common}\n\n🔑 کدِ پمپِ «${links[0].station_name}»:  ${code}\n\n`
          + '⚠️ این کد را فقط به کارمندانِ همین پمپ بدهید. عوض کردنش: برنامهٔ کامپیوتر ← پروفایل.',
          [downloadRow(), [BACK]], editId);
      }
      //  ⛔ هر شعبه کدِ خودش — کارمندِ یک شعبه حساب‌های شعبهٔ دیگر را نمی‌بیند
      const lines = [];
      for (const l of links) lines.push(`🔑 «${l.station_name}»:  ${await accessCodeOf(l.station_id)}`);
      return show(row.chat_id,
        `${common}\n\nهر شعبه کدِ خودش را دارد:\n${lines.join('\n')}\n\n`
        + '⚠️ کدِ هر شعبه را فقط به کارمندانِ همان شعبه بدهید. عوض کردنش: برنامهٔ کامپیوترِ همان شعبه ← پروفایل.',
        [downloadRow(), [BACK]], editId);
    }
    const codeRows = links.length === 1
      ? [[{ text: '🔑 کدِ پمپ (مدیر)', callback_data: 'code' }, BACK]]
      : [...links.map(l => [{ text: `🔑 کدِ «${l.station_name}» (مدیر)`.slice(0, 60), callback_data: `code:${l.station_id}` }]), [BACK]];
    return show(row.chat_id,
      `${common}\n\n🔑 کدِ پمپ را مدیرِ گروه با دکمهٔ زیر می‌بیند (فقط برای خودش نشان داده می‌شود).`,
      [downloadRow(), ...codeRows], editId);
  }

  /* ── گروه و کانال ─────────────────────────────────────────────── */

  /**
   * نشانیِ یک‌بارمصرفِ «افزودن به گروه» (قیدِ ۴) و راهِ کانال — یک صفحه.
   *
   * `t.me/<بات>?startgroup=<نشانه>` ⇒ کاربر گروه را برمی‌گزیند ⇒ تلگرام در
   * همان گروه `/start <نشانه>` را برای بات می‌فرستد.
   *
   * ⚠️ نشانه به **همین گفت‌وگو** بسته است (`from_chat`)، پس هر شعبه‌ای که این
   * گفت‌وگو دارد به گروه می‌رود. گروهی که از قبل شعبه‌ای دارد، شعبهٔ تازه را
   * **اضافه** می‌کند — هیچ شعبه‌ای جای دیگری را نمی‌گیرد.
   */
  async function sendShare(row, editId = 0) {
    const links = isLinked(row) ? await linksOf(row) : [];
    if (!links.length) return notLinked(row, editId);
    const name = await username();
    if (!name) return show(row.chat_id, 'نامِ بات هنوز از تلگرام نیامده است؛ چند لحظه بعد دوباره بزنید.', [[BACK]], editId);

    const raw = crypto.randomBytes(18).toString('base64url');
    const t = now();
    await query(
      `INSERT INTO telegram_link_tokens (token_hash, station_id, user_id, expires_at, created_at, from_chat)
       VALUES ($1,$2,$3,$4,$5,$6)`,
      [hashToken(raw), links[0].station_id, links[0].user_id, t + LINK_TTL_MS, t, String(row.chat_id)]
    );
    //  نشانه‌های کهنه همان‌جا جارو می‌شوند — این جدول نباید بزرگ شود
    await query('DELETE FROM telegram_link_tokens WHERE expires_at < $1', [t - LINK_TTL_MS]);

    const what = links.length === 1
      ? `پمپِ «${links[0].station_name}»`
      : `هر ${faNum(links.length)} شعبه (${links.map(l => `«${l.station_name}»`).join('، ')})`;
    const kb = [
      [{ text: '➕ افزودن به گروه', url: `https://t.me/${name}?startgroup=${raw}` }],
      [{ text: '📣 افزودن به کانال', url: `https://t.me/${name}?startchannel&admin=post_messages` }],
    ];
    if (await myChannels(row)) kb.push([{ text: '📣 شعبه‌ها را به کانالِ وصل‌شده هم بفرست', callback_data: 'chansync' }]);
    kb.push([BACK]);
    return show(row.chat_id,
      `👥 هشدارهای ${what} در گروه یا کانالِ شما:\n\n`
      + 'گروه: دکمهٔ «افزودن به گروه» ⇒ گروه را برگزینید ⇒ تمام، بات خودش خبر می‌دهد.\n'
      + 'گروهی که از قبل شعبهٔ دیگری دارد، این شعبه‌ها را هم اضافه می‌گیرد؛ هر شعبه با نامِ خودش می‌آید.\n'
      + '⏳ این دکمه پانزده دقیقه و فقط یک بار کار می‌کند.\n\n'
      + 'کانال: دکمهٔ «افزودن به کانال» ⇒ کانال را برگزینید ⇒ بات را «مدیر» کنید '
      + '(فقط «ارسالِ پیام» کافی است).\n\n'
      + '⚙️ در گروه، فقط مدیرانِ گروه تنظیمات را عوض می‌کنند.',
      kb, editId);
  }

  /** کانال‌هایی که یکی از حساب‌های همین گفت‌وگوی خصوصی وصل کرده است. */
  async function myChannels(row) {
    if (!row || row.kind !== 'private') return [];
    const list = await many(
      `SELECT DISTINCT c.chat_id, c.title
         FROM telegram_chat_links p
         JOIN telegram_chat_links g ON g.user_id=p.user_id
         JOIN telegram_chats c ON c.chat_id=g.chat_id AND c.kind='channel'
        WHERE p.chat_id=$1`,
      [String(row.chat_id)]
    );
    return list.length ? list : null;
  }

  /** هر شعبهٔ این گفت‌وگوی خصوصی ⇒ هر کانالی که یکی از همین حساب‌ها وصل کرده. */
  async function syncChannels(row) {
    const links = await linksOf(row);
    const chans = (await myChannels(row)) || [];
    let added = 0;
    const t = now();
    for (const c of chans) {
      for (const l of links) {
        if (await addLink(c.chat_id, l.station_id, l.user_id, t)) added += 1;
      }
    }
    return { added, channels: chans };
  }

  /* ── تنظیمات ──────────────────────────────────────────────────── */

  async function sendSettings(row, editId = 0, lead = '') {
    const links = isLinked(row) ? await linksOf(row) : [];
    if (!links.length) return notLinked(row, editId);
    const place = row.kind === 'private' ? 'گفت‌وگو' : (row.kind === 'channel' ? 'کانال' : 'گروه');
    let text = (lead ? `${lead}\n\n` : '') + `⚙️ تنظیماتِ این ${place}\n\n`
      + (row.only_out
        ? '🔕 الان فقط هشدارهای «تمام شد» می‌آید.'
        : '🔔 الان همهٔ هشدارها می‌آید: «تمام شد» و «کم مانده».');
    text += row.quiet === false
      ? '\n🌙 ساعتِ آرام خاموش است: «کم مانده» شب هم می‌آید.'
      : '\n🌙 ساعتِ آرام: از ۱۰ شب تا ۷ صبح فقط هشدارهای فوری («تمام شد»، مخزنِ خالی) می‌آید.';
    text += `\n📏 در روز بیش از ${faNum(CALM_CAP)} پیامِ «کم مانده» نمی‌آید؛ بقیه در /status و اعلامیهٔ صبح است.`;
    text += `\n\n🏷️ شعبه‌های وصل (${faNum(links.length)}):\n${links.map(l => `▫️ ${l.station_name}`).join('\n')}`;
    if (row.kind === 'private') {
      text += '\n\n➕ شعبهٔ دیگری دارید (حسابِ پمپِ دیگر، با ایمیلِ دیگر)؟ «افزودنِ شعبه» را بزنید — هر شعبه جدا می‌ماند.';
    } else {
      text += '\n\n➕ افزودنِ شعبهٔ دیگر به همین گروه: در گفت‌وگوی خصوصی با بات، با حسابِ همان شعبه «👥 گروه و کانال» ⇐ «افزودن به گروه» ⇐ همین گروه.';
    }
    const kb = [[{ text: row.only_out ? '🔔 «کم مانده» را هم بفرست' : '🔕 فقط «تمام شد» را بفرست', callback_data: 'toggle' }],
      [{ text: row.quiet === false ? '🌙 ساعتِ آرام را روشن کن' : '☀️ ساعتِ آرام را خاموش کن', callback_data: 'quiet' }]];
    if (row.kind === 'private') kb.push([{ text: '➕ افزودنِ شعبه (حسابِ دیگر)', callback_data: 'addacct' }]);
    if (row.kind === 'private' && links.some(l => l.role === 'owner')) {
      kb.push([{ text: '👮 مدیرانِ نرخِ اتحادیه', callback_data: 'rad' }]);
    }
    if (links.length > 1) {
      for (const l of links) {
        kb.push([{ text: `🔌 جدا کردنِ «${l.station_name}»`.slice(0, 60), callback_data: `rm:${l.station_id}` }]);
      }
    }
    kb.push([{
      text: links.length > 1
        ? '🔌 جدا شدن از همهٔ شعبه‌ها'
        : (row.kind === 'private' ? '🔌 جدا شدن از پمپ' : '🔌 جدا کردنِ این گروه'),
      callback_data: 'unlink',
    }]);
    kb.push([BACK]);
    return show(row.chat_id, text, kb, editId);
  }

  function askUnlink(row, editId = 0) {
    return show(row.chat_id,
      row.kind === 'private'
        ? 'این گفت‌وگو از پمپ جدا شود؟ دیگر هیچ هشداری این‌جا نمی‌آید. (گروه‌هایی که وصل کرده‌اید جدا وصل‌اند.)'
        : 'این گروه از پمپ جدا شود؟ دیگر هیچ هشداری این‌جا نمی‌آید.',
      [[
        { text: '🔌 بله، جدا کن', callback_data: 'unlink_yes' },
        { text: 'نه', callback_data: 'settings' },
      ]], editId);
  }

  /**
   * گامِ ایمیل.
   *
   * ⛔ **پاسخ برای ایمیلِ ناموجود همان است** (قیدِ ۲). کد فقط وقتی واقعاً
   * می‌رود که حساب هست و پمپ دارد.
   */
  async function onEmail(row, text) {
    const email = normEmail(text);
    if (!email) {
      return send(row.chat_id,
        'این نشانیِ ایمیل درست نیست. ایمیلِ حسابِ پمپتان را بفرستید؛ مثلاً name@gmail.com');
    }

    const t = now();
    let count = Number(row.code_requests) || 0;
    let windowAt = Number(row.window_at) || 0;
    if (t - windowAt > CODE_WINDOW_MS) { count = 0; windowAt = t; }
    if (count >= CODE_MAX_PER_WINDOW) {
      return send(row.chat_id, 'درخواستِ کد از این گفت‌وگو زیاد شد. یک ساعت بعد دوباره امتحان کنید.');
    }

    //  ⛔ «رفت» با «در لاگ چاپ شد» یکی نیست: روی سرورِ واقعی بی سرویسِ
    //  ایمیل، کد به دستِ هیچ‌کس نمی‌رسد — پس راست می‌گوییم.
    const mailer = req('./mailer');
    const mc = await mailer.current();
    if (mc.provider === 'log' && config.env === 'production') {
      return send(row.chat_id,
        'سرویسِ ایمیلِ سرور هنوز تنظیم نشده و کد به ایمیل نمی‌رسد. لطفاً به مدیرِ سامانه بگویید.');
    }

    await query(
      `UPDATE telegram_chats SET state='code', pending_email=$2, code_requests=$3, window_at=$4, updated_at=$5
        WHERE chat_id=$1`,
      [row.chat_id, email, count + 1, windowAt, t]
    );

    const user = await one("SELECT id FROM users WHERE email=$1 AND status='active'", [email]);
    const member = user ? await stations.membershipOf(user.id) : null;
    if (!member) {
      /*
       *  ⛔ گزارشِ صاحب سامانه (۱۴۰۵/۰۷/۱۳): «سرور کد را ارسال نکرد.» ایمیلی
       *  که حسابِ پمپ ندارد تا امروز **هیچ** نامه‌ای نمی‌گرفت و کاربر نمی‌دانست
       *  چرا. پاسخِ بات همان می‌ماند (قیدِ ۲ — دفترچهٔ ایمیل‌ها نشویم)، ولی خودِ
       *  صندوقِ همان ایمیل می‌گوید چه شد: فقط صاحبِ همان صندوق آن را می‌بیند.
       */
      tellNoAccount(email, Boolean(user)).catch((e) => console.error('[telegram] نامهٔ «حساب نیست» نرفت:', e.message));
    }
    const other = [[{ text: '✉️ ایمیلِ دیگر', callback_data: 'reset' }]];
    if (member) {
      try {
        await req('./otp').request(email, { purpose: PURPOSE, app: 'pump' });
      } catch (err) {
        if (err.code === 'otp_resend_wait') {
          return show(row.chat_id, `${err.message}. اگر کدِ قبلی رسیده، همان را بفرستید.`, other);
        }
        if (err.code === 'otp_daily_limit') return send(row.chat_id, err.message);
        console.error('[telegram] کد نرفت:', err.message);
        return send(row.chat_id,
          'کد به ایمیل فرستاده نشد. کمی بعد دوباره امتحان کنید؛ اگر باز هم نشد، به مدیرِ سامانه بگویید.');
      }
    }

    return show(row.chat_id,
      `اگر «${email}» حسابِ پمپ داشته باشد، یک کدِ شش‌رقمی همین حالا به آن رفت `
      + '(پوشهٔ اسپم را هم ببینید).\n\n🔢 کد را همین‌جا بفرستید.', other);
  }

  /** نامه به همان صندوق: «با این ایمیل حسابِ پمپی نیست» — همان مدلِ ایمیلِ پمپ. */
  async function tellNoAccount(email, hasUser) {
    const mailer = req('./mailer');
    const templates = req('./mail-templates');
    const title = 'این ایمیل به هیچ پمپی وصل نیست';
    const body = (hasUser
      ? 'کسی در باتِ تلگرامِ پمپ خواست با همین ایمیل وصل شود، ولی این حساب هنوز عضوِ هیچ پمپی نیست.'
      : 'کسی در باتِ تلگرامِ پمپ خواست با همین ایمیل وصل شود، ولی با این ایمیل هیچ حسابی ساخته نشده است.')
      + '\n\nاول در برنامهٔ پمپ با همین ایمیل حساب بسازید و پمپتان را بسازید، بعد دوباره در بات همین ایمیل را بفرستید.'
      + '\n\nاگر شما نبودید، این نامه را نادیده بگیرید.';
    const html = templates.messageHtml({ app: 'pump', title, body })
      || mailer.card({ title, lead: body });
    await mailer.send({ to: email, subject: title, text: `${title}\n\n${body}`, html, fromName: templates.brandOf('pump') || undefined });
  }

  /** گامِ کد. ⛔ حسابِ ناموجود هم فقط «کد درست نیست» می‌گیرد. */
  async function onCode(row, text) {
    const digits = asciiDigits(text).replace(/\D/g, '');
    if (digits.length !== 6) {
      return show(row.chat_id, 'کدِ شش‌رقمیِ ایمیل را بفرستید — یا «✉️ ایمیلِ دیگر» را بزنید.',
        [[{ text: '✉️ ایمیلِ دیگر', callback_data: 'reset' }]]);
    }
    const email = row.pending_email;
    const user = email ? await one("SELECT id FROM users WHERE email=$1 AND status='active'", [email]) : null;
    const member = user ? await stations.membershipOf(user.id) : null;
    if (!member) return send(row.chat_id, '❌ کد درست نیست.');

    try {
      await req('./otp').verify(email, digits, { purpose: PURPOSE });
    } catch (err) {
      if (err.code === 'otp_wrong') return send(row.chat_id, '❌ کد درست نیست. دوباره بفرستید.');
      await setState(row.chat_id, 'email');
      return send(row.chat_id,
        err.code === 'otp_locked'
          ? 'تلاشِ نادرست زیاد شد. ایمیل را دوباره بفرستید تا کدِ تازه برود.'
          : 'مهلتِ این کد تمام شده است. ایمیل را دوباره بفرستید.');
    }

    const t = now();
    const before = isLinked(row) ? (await linksOf(row)).length : 0;
    //  ⛔ شعبهٔ تازه **اضافه** می‌شود؛ شعبه‌های پیشینِ همین گفت‌وگو دست نمی‌خورند
    const fresh = await addLink(row.chat_id, member.station_id, user.id, t);
    await setState(row.chat_id, '');
    await req('./audit').log({
      actorType: 'user', userId: user.id, action: 'pump.telegram_link',
      targetType: 'station', targetId: member.station_id, detail: { kind: 'private' },
    });
    const lead = !before
      ? '✅ وصل شد. از این به بعد هشدارهای همین پمپ این‌جا می‌آید — حتی وقتی برنامه بسته است.'
      : (fresh
        ? '✅ شعبهٔ تازه اضافه شد. هر شعبه با نامِ خودش جدا می‌آید و حساب‌هایشان با هم قاطی نمی‌شود.'
        : 'این شعبه از قبل در همین گفت‌وگو وصل بود.');
    return sendMenu(await chatRow(row.chat_id), lead);
  }

  async function onPrivate(chat, cmd, text, from = null) {
    let row = await ensureChat(chat, 'private');
    const linked = isLinked(row);

    if (cmd) {
      if (cmd.name === 'mirza' || cmd.name === 'chats') return sendMirza(row);
      if (cmd.name === 'myid' || cmd.name === 'id') {
        return send(row.chat_id, `🆔 شناسهٔ تلگرامِ شما: ${(from || chat).id}\nاین عدد را به صاحبِ پمپ بدهید تا شما را «مدیرِ نرخ» کند.`);
      }
      if (cmd.name === 'cancel' && row.state === 'rateadmin') {
        await query(`UPDATE telegram_chats SET state='', pending_station='' WHERE chat_id=$1`, [row.chat_id]);
        return sendMenu(row);
      }
      if (cmd.name === 'rate') {
        const parsed = req('./station-rates').parse(cmd.arg);
        return parsed ? onRate(row, parsed, from || chat) : rateHelp(row);
      }
      if (cmd.name === 'cancel') {
        await setState(row.chat_id, linked ? '' : 'email');
        return linked ? sendMenu(row) : send(row.chat_id, 'باشد. هر وقت خواستید ایمیلِ حسابِ پمپ را بفرستید.');
      }
      if (cmd.name === 'help') {
        return show(row.chat_id, HELP, linked ? [[BACK]] : welcomeKeyboard());
      }
      if (linked) {
        if (cmd.name === 'status') return sendStatus(row);
        if (cmd.name === 'code' || cmd.name === 'app') return sendApp(row);
        if (cmd.name === 'group' || cmd.name === 'channel') return sendShare(row);
        if (cmd.name === 'settings') return sendSettings(row);
        if (cmd.name === 'stop') return askUnlink(row);
        if (cmd.name === 'find') return onSearch(row, cmd.arg);
        if (cmd.name === 'tanks') return askTanks(row);
        if (cmd.name === 'acct') return askAcct(row, cmd.arg);
        return sendMenu(row);
      }
      await setState(row.chat_id, 'email');
      return show(row.chat_id, WELCOME, welcomeKeyboard());
    }

    //  💬 در حالِ وصل کردنِ «چت‌های میرزا»: هر نوشته کدِ هشت‌رقمی است
    if (row.state === 'relay') return onRelayCode(row, text);
    //  👮 صاحبِ پمپ در حالِ افزودنِ شناسه‌های مدیرانِ نرخ
    if (row.state === 'rateadmin') return onRateAdminInput(row, text);
    //  🏷️ «پطرول ۷۹ دیزل ۸۰» — نرخِ اتحادیه، نه جست‌وجو و نه ایمیل
    const rate = req('./station-rates').parse(text);
    if (rate && row.state !== 'code') return onRate(row, rate, from || chat);

    //  ⛔ وصل‌شده: هر نوشته‌ای جست‌وجو است، نه یک منوی دیگر (قاعدهٔ ۳) —
    //  مگر همین حالا در حالِ افزودنِ شعبهٔ دیگر باشد (ایمیل ⇐ کد).
    if (linked && row.state !== 'email' && row.state !== 'code') return onSearch(row, text);
    //  ایمیلِ تازه در گامِ کد یعنی «این یکی را می‌خواهم»
    if (row.state === 'code' && !normEmail(text)) return onCode(row, text);
    row = { ...row, state: 'email' };
    return onEmail(row, text);
  }

  async function onSearch(row, q) {
    const links = isLinked(row) ? await linksOf(row) : [];
    if (!links.length) return notLinked(row);
    if (!String(q || '').trim()) {
      return send(row.chat_id, row.kind === 'private'
        ? '🔎 نامِ قرض‌دار را بنویسید.'
        : '🔎 بعد از /find نامِ قرض‌دار را بنویسید؛ مثلاً: /find کریم');
    }
    const text = await searchText(links, q);
    //  📷 زیرِ نتیجه: «حسابِ فلانی» برای پنج نامِ اول (هر شعبه جدا) — عکس و PDF از خودِ برنامه
    const kb = [];
    if (row.kind !== 'channel') {
      const qn = normName(q);
      for (const l of links) {
        const found = await branchHits(l, qn).catch(() => null);
        for (const d of found?.hits || []) {
          if (kb.length >= 5) break;
          const label = links.length > 1 ? `${d.n} · ${l.station_name}` : d.n;
          kb.push([{ text: `📷 ${label}`.slice(0, 48), callback_data: `acctn:${nameRef(l.station_id, d.n)}` }]);
        }
      }
    }
    if (!kb.length) return sendLong(row.chat_id, text);
    const parts = chunks(text);
    for (let i = 0; i < parts.length - 1; i++) await send(row.chat_id, parts[i]);
    return send(row.chat_id, parts[parts.length - 1], kb);
  }

  /* ── دسترسی در گروه ───────────────────────────────────────────── */

  /*
   *  ⛔ گزارشِ صاحب سامانه (۱۴۰۵/۰۷/۱۴): «چرا توی بات هر کسی می‌تونه
   *  دست‌کاری کنه؟» تا امروز هر عضوِ گروه می‌توانست «فقط تمام شد» را روشن
   *  کند و هشدارِ همه را ساکت کند. حالا در گروه هر چیزی که **عوض** می‌کند
   *  (تنظیمات، جدا کردن، دیدنِ کدِ پمپ) فقط مالِ این دو نفر است:
   *    • مدیر یا سازندهٔ همان گروه (از خودِ تلگرام پرسیده می‌شود)
   *    • همان حسابی که گروه را وصل کرد (گفت‌وگوی خصوصیِ وصل‌شده‌اش)
   *  دیدنِ وضعیت و جست‌وجو برای همه باز است — گروه، گروهِ کارکنان است.
   */
  const adminCache = new Map();
  const ADMIN_TTL_MS = 60 * 1000;

  async function canManage(row, from) {
    if (row.kind === 'private') return true;
    const uid = from?.id;
    if (!uid) return false;
    //  وصل‌کنندهٔ **یکی از** شعبه‌های همین گروه
    const own = await one(
      `SELECT 1 FROM telegram_chat_links p
         JOIN telegram_chats pc ON pc.chat_id=p.chat_id AND pc.kind='private'
        WHERE p.chat_id=$1
          AND p.user_id IN (SELECT user_id FROM telegram_chat_links WHERE chat_id=$2)
        LIMIT 1`,
      [String(uid), String(row.chat_id)]
    );
    if (own) return true;
    const key = `${row.chat_id}:${uid}`;
    const hit = adminCache.get(key);
    if (hit && now() - hit.at < ADMIN_TTL_MS) return hit.ok;
    const res = await call('getChatMember', { chat_id: String(row.chat_id), user_id: uid });
    const ok = Boolean(res.ok && ['creator', 'administrator'].includes(res.result?.status));
    adminCache.set(key, { ok, at: now() });
    if (adminCache.size > 1000) adminCache.delete(adminCache.keys().next().value);
    return ok;
  }

  /**
   * کانال — تلگرام در کانال هیچ `/start`ی نمی‌فرستد؛ فقط `my_chat_member`
   * می‌آید با **کسی که بات را مدیر کرد** (`from`). پس ملاک همان است: اگر
   * گفت‌وگوی خصوصیِ همان شخص با بات به پمپی وصل است، کانال به همان پمپ وصل
   * می‌شود. ⛔ کسِ دیگری نمی‌تواند کانالی را به پمپِ شما ببندد.
   */
  async function onChannelAdmin(u) {
    const chat = u.chat;
    const who = u.from?.id ? await chatRow(u.from.id) : null;
    const owned = who && who.kind === 'private' && isLinked(who) ? await linksOf(who) : [];
    if (!owned.length) {
      //  کسی که وصل نیست: فقط به خودش می‌گوییم، نه در کانال
      if (u.from?.id) {
        await send(u.from.id,
          `بات مدیرِ کانالِ «${titleOf(chat)}» شد، ولی شما هنوز به هیچ پمپی وصل نیستید. `
          + 'اول همین‌جا ایمیلِ حسابِ پمپ را بفرستید و وصل شوید، بعد بات را یک بار از کانال بردارید و دوباره مدیر کنید.');
      }
      return null;
    }
    const row = await ensureChat(chat, 'channel');
    const t = now();
    //  ⛔ هر شعبهٔ همان شخص — اضافه، نه جایگزین
    for (const l of owned) {
      await addLink(row.chat_id, l.station_id, l.user_id, t);
      await req('./audit').log({
        actorType: 'user', userId: l.user_id, action: 'pump.telegram_link',
        targetType: 'station', targetId: l.station_id, detail: { kind: 'channel' },
      });
    }
    const names = owned.map(l => `«${l.station_name}»`).join('، ');
    const what = owned.length === 1 ? `پمپِ ${names}` : `${faNum(owned.length)} شعبه (${names})`;
    await send(row.chat_id, `✅ این کانال به ${what} وصل شد. هشدارهای پمپ از این به بعد همین‌جا منتشر می‌شود.`);
    //  ⚠️ بی دکمه: خبرِ «وصل شد» یک خبر است، نه منو (قاعدهٔ ۳)
    return send(who.chat_id,
      `✅ کانالِ «${titleOf(chat)}» به ${what} وصل شد. از این پس هشدارها در همان کانال می‌آید، نه این‌جا.\nبرای جدا کردن، بات را از مدیرانِ کانال بردارید — هشدارها دوباره همین‌جا می‌آید.`);
  }

  async function onGroup(chat, cmd, from, text = '') {
    const row = await ensureChat(chat, 'group');
    if (!cmd) {
      //  ⛔ گفت‌وگوی گروه مالِ ما نیست — مگر همان کسی که «وصل کردنِ پمپ» را زد
      //  کدِ هشت‌رقمی را بفرستد، یا کسی صریح «نرخ …» بنویسد.
      if (row.state === 'relay' && from && String(from.id) === String(row.relay_by)) {
        return onRelayCode(row, text);
      }
      const t = String(text || '');
      if (/نرخ/.test(t)) {
        const parsed = req('./station-rates').parse(t);
        if (parsed) return onRate(row, parsed, from);
      }
      return null;
    }

    if (cmd.name === 'mirza' || cmd.name === 'chats') return sendMirza(row);
    if (cmd.name === 'myid' || cmd.name === 'id') {
      return send(row.chat_id, `🆔 شناسهٔ تلگرامِ ${from?.first_name || 'شما'}: ${from?.id || '—'}`);
    }
    if (cmd.name === 'cancel' && row.state === 'relay') {
      await setState(row.chat_id, '');
      return send(row.chat_id, 'باشد.');
    }
    if (cmd.name === 'rate') {
      const parsed = req('./station-rates').parse(cmd.arg);
      return parsed ? onRate(row, parsed, from) : rateHelp(row);
    }

    if (cmd.name === 'start' && cmd.arg) {
      const t = now();
      const tok = await one(
        `UPDATE telegram_link_tokens SET used_at=$2
          WHERE token_hash=$1 AND used_at IS NULL AND expires_at > $2
          RETURNING station_id, user_id, from_chat`,
        [hashToken(cmd.arg), t]
      );
      if (!tok) {
        return send(row.chat_id,
          'این دکمه باطل یا کهنه شده است. در گفت‌وگوی خصوصی با بات دوباره «👥 گروه و کانال» را بزنید.');
      }
      //  شعبه‌هایی که می‌آیند: همهٔ شعبه‌های گفت‌وگوی خصوصیِ سازندهٔ دکمه
      //  (نشانهٔ کهنه‌تر از این نسخه: همان یک پمپ) — هر کدام همین لحظه سنجیده.
      let incoming = [];
      if (tok.from_chat) {
        const src = await chatRow(tok.from_chat);
        if (src && src.kind === 'private' && isLinked(src)) incoming = await linksOf(src);
      } else {
        const v = await one(
          `SELECT s.id AS station_id, s.name AS station_name, m.user_id
             FROM stations s
             JOIN station_members m ON m.station_id=s.id AND m.user_id=$2 AND m.status='active'
             JOIN users u ON u.id=m.user_id AND u.status='active'
            WHERE s.id=$1 AND s.status='active'`,
          [tok.station_id, tok.user_id]
        );
        if (v) incoming = [v];
      }
      if (!incoming.length) {
        return send(row.chat_id, 'حسابی که این دکمه را ساخت دیگر عضوِ آن پمپ نیست.');
      }
      const had = isLinked(row) ? (await linksOf(row)).length : 0;
      for (const l of incoming) {
        await addLink(row.chat_id, l.station_id, l.user_id, t);
        await req('./audit').log({
          actorType: 'user', userId: l.user_id, action: 'pump.telegram_link',
          targetType: 'station', targetId: l.station_id, detail: { kind: 'group' },
        });
      }
      const names = incoming.map(l => `«${l.station_name}»`).join('، ');
      //  ⚠️ بی دکمه: خبر است، نه منو. و می‌گوید چرا خصوصی ساکت می‌شود (alertChats).
      if (tok.from_chat) {
        await send(tok.from_chat,
          `✅ گروهِ «${titleOf(chat)}» وصل شد (${names}). از این پس هشدارها در همان گروه می‌آید، نه این‌جا.\n`
          + 'اگر گروه را جدا کنید، هشدارها دوباره همین‌جا می‌آید.');
      } else {
        const priv = await one(
          `SELECT chat_id FROM telegram_chats
            WHERE kind='private' AND user_id=$1 AND station_id=$2 AND linked_at IS NOT NULL`,
          [tok.user_id, tok.station_id]
        );
        if (priv) {
          await send(priv.chat_id,
            `✅ گروهِ «${titleOf(chat)}» وصل شد. از این پس هشدارها در همان گروه می‌آید، نه این‌جا.\n`
            + 'اگر گروه را جدا کنید، هشدارها دوباره همین‌جا می‌آید.');
        }
      }
      return sendMenu(await chatRow(row.chat_id),
        (had
          ? `✅ شعبهٔ تازه به این گروه اضافه شد (${names}). هر شعبه با نامِ خودش جدا می‌آید.\n`
          : '✅ این گروه وصل شد. هشدارهای پمپ از این به بعد همین‌جا برای همه می‌آید.\n')
        + 'تنظیمات فقط در دستِ مدیرانِ گروه است.');
    }

    if (!isLinked(row)) {
      return ['start', 'menu', 'help'].includes(cmd.name) ? notLinked(row) : null;
    }
    if (cmd.name === 'status') return sendStatus(row);
    if (cmd.name === 'find') return onSearch(row, cmd.arg);
    if (cmd.name === 'tanks') return askTanks(row);
    if (cmd.name === 'acct') return askAcct(row, cmd.arg);
    if (cmd.name === 'code' || cmd.name === 'app') return sendApp(row);
    if (cmd.name === 'settings') {
      if (!(await canManage(row, from))) return send(row.chat_id, '⛔ تنظیماتِ بات فقط در دستِ مدیرانِ گروه است.');
      return sendSettings(row);
    }
    if (['start', 'menu', 'help'].includes(cmd.name)) return sendMenu(row);
    return null;
  }

  /* ══════════════════════════════════════════════════════════════════
     💬 چت‌های میرزا — پیامِ مشتریِ کیو‌آر، وقتی برنامهٔ کامپیوتر خاموش است
     ══════════════════════════════════════════════════════════════════

     خواستهٔ صاحب سامانه (۱۴۰۵/۰۷/۱۶): «توی منو قابلیتِ جدید بذار به اسمِ
     چت‌های میرزا… بات کدِ هشت‌رقمیِ برنامه رو بخواد… وقتی برنامه خاموش بود
     پیام‌های مشتری‌ها بیاد تلگرام… و میرزا انتخاب کنه کجا بیاد.»

       مشتری (view/) ──chat.post──▶ relayCustomer ──(برنامه خاموش؟)──▶ تلگرام
       تلگرام: «Reply» روی همان پیام ──▶ chat.post(from:'o') ──▶ پوش به مشتری

     ⛔ **فقط وقتی برنامهٔ کامپیوتر خاموش است** (`desktopOnline`): برنامهٔ
     روشن خودش صندوقِ مشتری‌ها را دارد و دو جا یعنی دو جوابِ ناهم‌خوان.
     ⛔ **یک پمپ، یک مقصد** (`telegram_relays`، کلید = پمپ).
     ⛔ **کد که عوض شد، پیوند مُرد** (`code_hash`): صاحبِ پمپ با عوض کردنِ کد
     در پروفایل، هر تلگرامی را که با کدِ قبلی وصل شده بود بیرون می‌گذارد.
     ⛔ جواب فقط روی **همان** پیامِ فرستاده‌شده به **همان** گفت‌وگو پذیرفته
     می‌شود (`telegram_relay_msgs`)؛ هیچ پیامی از گفت‌وگوی دیگر به مشتریِ این
     پمپ نمی‌رسد.
  */

  /** برنامهٔ کامپیوترِ پمپ «روشن» است اگر یکی از دستگاه‌هایش در این فاصله پرسیده باشد. */
  const DESKTOP_ONLINE_MS = 3 * 60 * 1000;
  const RELAY_TRY_MAX = 6;
  const RELAY_TRY_WINDOW_MS = 60 * 60 * 1000;
  const RELAY_MSG_KEEP_MS = 14 * 24 * 3600 * 1000;

  async function desktopOnline(stationId, t = now()) {
    const r = await one(
      `SELECT MAX(last_seen_at) AS at FROM station_devices WHERE station_id=$1 AND status='active'`,
      [stationId]);
    const at = Number(r && r.at) || 0;
    return at > 0 && t - at < DESKTOP_ONLINE_MS;
  }

  /** مقصدهای این گفت‌وگو — فقط آن‌هایی که کدشان هنوز همان است. */
  async function relaysOf(chatId) {
    const list = await many(
      `SELECT r.station_id, r.code_hash, r.linked_at, s.name AS station_name, s.access_code, s.access_code_old
         FROM telegram_relays r JOIN stations s ON s.id=r.station_id AND s.status='active'
        WHERE r.chat_id=$1 ORDER BY r.linked_at`, [String(chatId)]);
    return list.filter(codeStillValid);
  }

  function codeStillValid(r) {
    if (!r.code_hash) return false;
    return [r.access_code, r.access_code_old].filter(Boolean).some(c => hashToken(c) === r.code_hash);
  }

  const MIRZA_INTRO =
    '💬 چت‌های میرزا\n\n'
    + 'مشتری‌هایی که کیو‌آرِ حسابشان را اسکن کرده‌اند می‌توانند به پمپ پیام بدهند. '
    + 'وقتی برنامهٔ کامپیوترِ پمپ خاموش است، همان پیام‌ها این‌جا می‌آید.\n\n'
    + '↩️ جواب دادن: روی پیامِ مشتری «Reply» بزنید و بنویسید — جواب به گوشیِ همان مشتری می‌رسد.\n'
    + '🖥️ برنامه که روشن باشد، پیام‌ها فقط در خودِ برنامه می‌آیند (دو جا نمی‌آیند).';

  async function sendMirza(row, editId = 0, lead = '') {
    const relays = await relaysOf(row.chat_id);
    let text = (lead ? `${lead}\n\n` : '') + MIRZA_INTRO + '\n\n';
    if (relays.length) {
      text += `📍 پمپ‌هایی که پیامشان این‌جا می‌آید:\n${relays.map(r => `▫️ ${r.station_name}`).join('\n')}`;
    } else {
      text += '📍 هنوز هیچ پمپی به این‌جا وصل نیست.';
    }
    if (row.kind === 'private') {
      text += '\n\n👥 می‌خواهید پیام‌ها در یک گروه بیاید؟ بات را به گروه اضافه کنید، در گروه /mirza را بزنید و همین کد را همان‌جا بفرستید (فقط مدیرِ گروه). '
        + 'هر پمپ فقط یک مقصد دارد: هر جا آخر وصل شد، پیام‌ها همان‌جا می‌آید.';
    }
    const kb = [[{ text: '➕ وصل کردنِ پمپ با کدِ هشت‌رقمی', callback_data: 'mzadd' }]];
    for (const r of relays) {
      kb.push([{ text: `🔌 خاموش کردنِ «${r.station_name}»`.slice(0, 60), callback_data: `mzrm:${r.station_id}` }]);
    }
    kb.push([isLinked(row) ? BACK : { text: '❓ راهنما', callback_data: 'help' }]);
    return show(row.chat_id, text, kb, editId);
  }

  async function askRelayCode(row, from, editId = 0) {
    await query(`UPDATE telegram_chats SET state='relay', relay_by=$2, updated_at=$3 WHERE chat_id=$1`,
      [row.chat_id, String(from?.id || row.chat_id), now()]);
    return show(row.chat_id,
      '🔢 کدِ هشت‌رقمیِ پمپ را بفرستید — همان کدی که کارمندان در اپِ گوشی می‌زنند.\n'
      + 'کجا پیدایش کنم؟ برنامهٔ کامپیوترِ پمپ ← پروفایل ← «کدِ اپِ کارمندان»؛ یا همین بات ← «📱 اپ و کدِ پمپ».\n\n'
      + '(رقمِ فارسی هم درست است. انصراف: /cancel)', null, editId);
  }

  async function onRelayCode(row, text) {
    const access = req('./station-access');
    const t = now();
    let tries = Number(row.relay_tries) || 0;
    let win = Number(row.relay_window) || 0;
    if (t - win > RELAY_TRY_WINDOW_MS) { tries = 0; win = t; }
    if (tries >= RELAY_TRY_MAX) {
      await setState(row.chat_id, '');
      return send(row.chat_id, 'تلاشِ نادرست زیاد شد. یک ساعت بعد دوباره «💬 چت‌های میرزا» را بزنید.');
    }
    const code = access.normalize(text);
    const st = access.isValid(code) ? await access.byCode(code) : null;
    if (!st) {
      await query('UPDATE telegram_chats SET relay_tries=$2, relay_window=$3 WHERE chat_id=$1',
        [row.chat_id, tries + 1, win]);
      return send(row.chat_id, '❌ این کد پیدا نشد. کدِ هشت‌رقمیِ پمپ را دوباره بفرستید — یا /cancel.');
    }
    const before = await one('SELECT chat_id FROM telegram_relays WHERE station_id=$1', [st.id]);
    await query(
      `INSERT INTO telegram_relays (station_id, chat_id, by_tg, linked_at, code_hash) VALUES ($1,$2,$3,$4,$5)
       ON CONFLICT (station_id) DO UPDATE SET chat_id=excluded.chat_id, by_tg=excluded.by_tg,
         linked_at=excluded.linked_at, code_hash=excluded.code_hash`,
      [st.id, String(row.chat_id), String(row.relay_by || ''), t, hashToken(code)]);
    await query(`UPDATE telegram_chats SET state='', relay_tries=0, relay_by='', updated_at=$2 WHERE chat_id=$1`,
      [row.chat_id, t]);
    await req('./audit').log({
      actorType: 'system', action: 'pump.telegram_relay',
      targetType: 'station', targetId: st.id, detail: { chatKind: row.kind },
    });
    //  مقصدِ قبلی باید بداند که دیگر پیامی نمی‌گیرد
    if (before && before.chat_id !== String(row.chat_id)) {
      await send(before.chat_id,
        `💬 از این پس پیامِ مشتری‌های «${st.name}» جای دیگری می‌آید (کسی با کدِ پمپ آن را به گفت‌وگوی دیگری برد). `
        + 'اگر شما نبودید، کدِ پمپ را در برنامهٔ کامپیوتر ← پروفایل عوض کنید.').catch(() => {});
    }
    return sendMirza(await chatRow(row.chat_id), 0,
      `✅ وصل شد: از این پس وقتی برنامهٔ کامپیوترِ «${st.name}» خاموش است، پیامِ مشتری‌هایش این‌جا می‌آید.`);
  }

  function mediaWord(kind) {
    return kind === 'image' ? '📷 عکس' : kind === 'video' ? '🎥 ویدیو' : kind === 'audio' ? '🎤 پیامِ صوتی' : '';
  }

  /**
   * پیامِ تازهٔ مشتری ⇒ تلگرام، اگر برنامهٔ کامپیوتر خاموش است.
   * ⚠️ هیچ‌وقت استثنا بیرون نمی‌دهد: پیام در صندوقِ پمپ نشسته و این رفاه است.
   */
  async function relayCustomer({ station, acct, message }) {
    try {
      if (!station || !message || message.from !== 'c') return { sent: false, why: 'not_customer' };
      const r = await one(
        `SELECT r.chat_id, r.code_hash, s.name AS station_name, s.access_code, s.access_code_old
           FROM telegram_relays r JOIN stations s ON s.id=r.station_id AND s.status='active'
          WHERE r.station_id=$1`, [station.id]);
      if (!r || !codeStillValid(r)) return { sent: false, why: 'no_relay' };
      if (!(await active())) return { sent: false, why: 'bot_off' };
      if (await desktopOnline(station.id)) return { sent: false, why: 'desktop_online' };
      const body = message.kind === 'text'
        ? message.text
        : `${mediaWord(message.kind)} — در برنامهٔ کامپیوتر یا اپِ پمپ دیده می‌شود.`;
      const who = message.name ? `👤 ${message.name}` : '👤 مشتری';
      const text = `💬 پیامِ مشتری — «${r.station_name}»\n${who}\n\n${body}\n\n↩️ برای جواب، روی همین پیام «Reply» بزنید.`;
      const res = await send(r.chat_id, text.slice(0, 4000));
      if (!res.ok) return { sent: false, why: 'telegram' };
      const t = now();
      await query(
        `INSERT INTO telegram_relay_msgs (chat_id, message_id, station_id, acct, created_at)
         VALUES ($1,$2,$3,$4,$5) ON CONFLICT DO NOTHING`,
        [r.chat_id, Number(res.result?.message_id) || 0, station.id, acct, t]);
      await query('DELETE FROM telegram_relay_msgs WHERE created_at < $1', [t - RELAY_MSG_KEEP_MS]);
      return { sent: true };
    } catch (err) {
      console.error('[telegram:relay]', hide(err.message));
      return { sent: false, why: 'error' };
    }
  }

  /** جوابِ میرزا با «Reply» ⇒ همان مشتری. ‎true‎ یعنی این پیام مالِ همین‌جا بود. */
  async function onRelayReply(m) {
    const to = m.reply_to_message;
    if (!to || !to.message_id) return false;
    const chatId = String(m.chat.id);
    const map = await one(
      'SELECT station_id, acct FROM telegram_relay_msgs WHERE chat_id=$1 AND message_id=$2',
      [chatId, Number(to.message_id)]);
    if (!map) return false;
    //  ⛔ همین حالا هم باید مقصدِ همان پمپ و با همان کد باشد
    const live = (await relaysOf(chatId)).find(r => r.station_id === map.station_id);
    if (!live) {
      await send(chatId, 'این گفت‌وگو دیگر پیامِ مشتری‌های آن پمپ را نمی‌گیرد؛ جواب نرفت.');
      return true;
    }
    const text = typeof m.text === 'string' ? m.text.trim() : '';
    if (!text) {
      await send(chatId, 'از تلگرام فقط جوابِ نوشتاری می‌رود. عکس و صدا را از برنامهٔ کامپیوتر بفرستید.');
      return true;
    }
    const chat = req('./station-chat');
    try {
      await chat.post({ stationId: map.station_id, acct: map.acct, from: 'o', name: 'پمپ', kind: 'text', text });
      await chat.pushTo(map.station_id, map.acct, {
        title: live.station_name || 'پمپ', body: text.slice(0, 120), tag: 'chat-' + map.acct,
      }).catch(() => {});
      await call('sendMessage', {
        chat_id: chatId, text: '✅ جواب به مشتری رسید.', reply_to_message_id: Number(m.message_id),
        allow_sending_without_reply: true,
      });
    } catch (err) {
      await send(chatId, `❌ جواب نرفت: ${hide(err.message)}`);
    }
    return true;
  }

  /* ══════════════════════════════════════════════════════════════════
     🏷️ نرخِ اتحادیه از تلگرام — فقط برای پمپِ همان حساب
     ══════════════════════════════════════════════════════════════════

     ⛔ **فقط از شناسه‌های تلگرامی که صاحبِ پمپ داده** (`station_rate_admins`،
     «👮 مدیرانِ نرخ») — خواستهٔ صاحب سامانه: «به بات چند ایدیِ تلگرام می‌دهم…
     فقط از اون‌ها اطاعت بشه که هر کس و ناکس نتونه بگه این نرخ رو بذار».
     ملاک **شناسهٔ فرستنده** (`from.id`) است، نه گفت‌وگو: در گروه هم فقط
     همان شناسه‌ها، هر کس دیگری (حتی مدیرِ گروه) نه. خودِ صاحبِ پمپ (با ایمیل
     وصل) همیشه مجاز است — فهرست را او می‌سازد.
     کدِ هشت‌رقمی (که دستِ کارمندان است) نرخ را عوض **نمی‌کند**.
     ⛔ در گروه فقط وقتی «نرخ» در پیام هست (یا /rate): «پطرول ۵۰ لیتر دادم»
     حرفِ کارمندان است، نه نرخ.
  */

  function rateLine(p) {
    const parts = [];
    if (p.petrol !== null && p.petrol !== undefined) parts.push(`پطرول ${faNum(p.petrol)}`);
    if (p.diesel !== null && p.diesel !== undefined) parts.push(`دیزل ${faNum(p.diesel)}`);
    return parts.join(' · ');
  }

  /** شناسهٔ تلگرامِ فرستنده ⇒ پمپ‌هایی که او مدیرِ نرخشان است (یا صاحبشان). */
  async function rateStations(from) {
    const uid = String(from?.id || '');
    if (!uid) return [];
    const uname = String(from?.username || '').toLowerCase();
    //  ⛔ نامِ کاربری عوض‌شدنی است و پس از رها شدن به دیگری می‌رسد: نخستین بار
    //  که با نام شناخته شد، همان‌جا به شناسهٔ عددیِ همین شخص سنجاق می‌شود و از آن
    //  پس فقط شناسه ملاک است — کسی که بعداً همان نام را بگیرد هیچ حقی ندارد.
    if (uname) {
      await query(
        `UPDATE station_rate_admins a SET tg_id=$1
          WHERE a.tg_id='' AND a.tg_username=$2
            AND NOT EXISTS (SELECT 1 FROM station_rate_admins b
                             WHERE b.station_id=a.station_id AND b.tg_id=$1)`,
        [uid, uname]).catch(() => {});
      //  همان شخص از قبل با شناسه در فهرست بود ⇒ ردیفِ نامیِ اضافه می‌رود، تا نامِ
      //  رهاشده هیچ دری را باز نگه ندارد
      await query(
        `DELETE FROM station_rate_admins a
          WHERE a.tg_id='' AND a.tg_username=$2
            AND EXISTS (SELECT 1 FROM station_rate_admins b
                         WHERE b.station_id=a.station_id AND b.tg_id=$1)`,
        [uid, uname]).catch(() => {});
    }
    return many(
      `SELECT s.id AS station_id, s.name AS station_name
         FROM stations s
        WHERE s.status='active' AND (
          EXISTS (SELECT 1 FROM station_rate_admins a
                   WHERE a.station_id=s.id
                     AND ((a.tg_id<>'' AND a.tg_id=$1)
                          OR (a.tg_id='' AND a.tg_username<>'' AND $2<>'' AND a.tg_username=$2)))
          OR EXISTS (SELECT 1 FROM telegram_chat_links l
                       JOIN telegram_chats c ON c.chat_id=l.chat_id AND c.kind='private'
                       JOIN station_members m ON m.station_id=l.station_id AND m.user_id=l.user_id
                            AND m.status='active' AND m.role='owner'
                       JOIN users u ON u.id=l.user_id AND u.status='active'
                      WHERE l.station_id=s.id AND l.chat_id=$1))
        ORDER BY s.name, s.id`,
      [uid, uname]).then(botStations);
  }

  /** راهنمای نرخ با همان دکمهٔ «قالب در کادرِ نوشتن». */
  async function rateHelp(row, editId = 0) {
    const links = isLinked(row) ? await linksOf(row) : [];
    const text = '🏷️ نرخِ اتحادیه\n\n'
      + '«✍️ نوشتنِ نرخ» را بزنید: قالبِ «نرخ پطرول … دیزل …» در کادرِ نوشتن می‌نشیند (چیزی فرستاده نمی‌شود). '
      + 'عددهای تازهٔ اتحادیه را جای عددها بنویسید و بفرستید — یکی هم کافی است (فقط پطرول یا فقط دیزل).\n\n'
      + 'یا خودتان بنویسید: نرخ پطرول ۷۹ دیزل ۸۰\n'
      + 'فقط «مدیرانِ نرخ»ی که صاحبِ پمپ تعیین کرده — نشست، همین‌جا خبر می‌دهم.';
    const kb = [[{ text: '✍️ نوشتنِ نرخ', switch_inline_query_current_chat: await rateTemplate(links) }]];
    if (editId) kb.push([BACK]);
    return show(row.chat_id, text, kb, editId);
  }

  async function onRate(row, parsed, from) {
    const rates = req('./station-rates');
    if (parsed.bad && parsed.bad.length) {
      const names = parsed.bad.map(b => (b === 'petrol' ? 'پطرول' : 'دیزل')).join(' و ');
      return send(row.chat_id,
        `⚠️ عددِ ${names} باورکردنی نیست — نرخ باید بینِ ${faNum(rates.MIN_RATE)} و ${faNum(rates.MAX_RATE)} افغانی باشد. `
        + 'هیچ چیزی عوض نشد؛ دوباره بنویسید، مثلاً: پطرول ۷۹ دیزل ۸۰');
    }
    if (parsed.petrol === null && parsed.diesel === null) return null;
    const allowed = await rateStations(from);
    if (!allowed.length) {
      if (row.kind !== 'private') {
        return send(row.chat_id, '⛔ نرخِ اتحادیه را فقط «مدیرانِ نرخ»ی که صاحبِ پمپ تعیین کرده عوض می‌کنند. هیچ چیزی عوض نشد.');
      }
      return send(row.chat_id,
        '⛔ شما «مدیرِ نرخ»ِ هیچ پمپی نیستید، پس هیچ نرخی عوض نشد.\n'
        + `🆔 شناسهٔ تلگرامِ شما: ${from?.id || '—'}\n`
        + 'این شناسه را به صاحبِ پمپ بدهید تا در بات ← ⚙️ تنظیمات ← «👮 مدیرانِ نرخ» اضافه کند.');
    }
    if (allowed.length === 1) return createRate(row, allowed[0], parsed, from);
    //  چند شعبه: ⛔ هیچ نرخی «برای همه» نمی‌نشیند — هر شعبه با کلیکِ خودش
    const p = parsed.petrol === null ? '-' : String(parsed.petrol);
    const d = parsed.diesel === null ? '-' : String(parsed.diesel);
    return show(row.chat_id,
      `🏷️ نرخِ تازه: ${rateLine(parsed)}\nبرای کدام شعبه؟ (هر شعبه جدا — روی بقیه اثری ندارد)`,
      allowed.map(l => [{ text: `⛽ ${l.station_name}`.slice(0, 50), callback_data: `rg:${l.station_id}:${p}:${d}` }]));
  }

  /* ── 👮 مدیرانِ نرخ — فقط صاحبِ پمپ فهرست را می‌سازد ────────────── */

  const RATE_ADMIN_MAX = 20;

  /** پمپ‌هایی که صاحبشان همین گفت‌وگوی خصوصی است (با ایمیل وصل). */
  async function ownedStations(row) {
    if (!row || row.kind !== 'private' || !isLinked(row)) return [];
    return (await linksOf(row)).filter(l => l.role === 'owner');
  }

  async function sendRateAdmins(row, stationId, editId = 0, lead = '') {
    const own = (await ownedStations(row)).find(l => l.station_id === stationId);
    if (!own) return sendSettings(row, editId);
    const list = await many(
      'SELECT * FROM station_rate_admins WHERE station_id=$1 ORDER BY added_at', [stationId]);
    let text = (lead ? `${lead}\n\n` : '') + `👮 مدیرانِ نرخِ «${own.station_name}»\n\n`
      + 'فقط این افراد (و خودِ شما) می‌توانند از تلگرام نرخِ اتحادیهٔ همین پمپ را عوض کنند — '
      + 'هر کسِ دیگری بنویسد، بات هیچ کاری نمی‌کند.\n\n';
    text += list.length
      ? list.map(a => `▫️ ${a.label ? `${a.label} — ` : ''}${a.tg_id ? `🆔 ${a.tg_id}` : `@${a.tg_username}`}`).join('\n')
      : '▫️ هنوز کسی اضافه نشده (فقط خودِ شما).';
    text += '\n\nهر کس شناسه‌اش را نمی‌داند: به همین بات /myid بفرستد.';
    const kb = [[{ text: '➕ افزودنِ شناسه', callback_data: `radadd:${stationId}` }]];
    for (const a of list) {
      kb.push([{ text: `🗑 برداشتنِ ${a.label || a.tg_id || '@' + a.tg_username}`.slice(0, 60), callback_data: `radrm:${a.id}` }]);
    }
    kb.push([{ text: '‹ تنظیمات', callback_data: 'settings' }]);
    return show(row.chat_id, text, kb, editId);
  }

  /** «123456789 @mirza_user 987654321» ⇒ شناسه‌ها و نام‌های کاربری. */
  function parseRateAdmins(text) {
    const out = [];
    for (const raw of asciiDigits(text).split(/[\s,،;]+/)) {
      const t = raw.trim();
      if (!t) continue;
      if (/^-?\d{5,15}$/.test(t)) out.push({ tg_id: t.replace(/^-/, ''), tg_username: '' });
      else if (/^@?[A-Za-z][A-Za-z0-9_]{4,31}$/.test(t)) out.push({ tg_id: '', tg_username: t.replace(/^@/, '').toLowerCase() });
      else return null;
    }
    return out.length ? out : null;
  }

  async function onRateAdminInput(row, text) {
    const sid = row.pending_station;
    const own = (await ownedStations(row)).find(l => l.station_id === sid);
    if (!own) { await setState(row.chat_id, ''); return sendSettings(row); }
    const items = parseRateAdmins(text);
    if (!items) {
      return send(row.chat_id,
        'این شناسه درست نیست. شناسهٔ عددیِ تلگرام را بفرستید (مثلاً 123456789) — چند تا با فاصله هم می‌شود. انصراف: /cancel');
    }
    const have = Number((await one('SELECT count(*)::int AS n FROM station_rate_admins WHERE station_id=$1', [sid])).n) || 0;
    let added = 0;
    const t = now();
    for (const it of items) {
      if (have + added >= RATE_ADMIN_MAX) break;
      const r = await one(
        `INSERT INTO station_rate_admins (id, station_id, tg_id, tg_username, added_by, added_at)
         VALUES ($1,$2,$3,$4,$5,$6) ON CONFLICT DO NOTHING RETURNING id`,
        [newId('rad'), sid, it.tg_id, it.tg_username, String(row.chat_id), t]);
      if (r) added += 1;
    }
    await query(`UPDATE telegram_chats SET state='', pending_station='', updated_at=$2 WHERE chat_id=$1`, [row.chat_id, t]);
    await req('./audit').log({
      actorType: 'user', userId: own.user_id, action: 'pump.rate_admins_added',
      targetType: 'station', targetId: sid, detail: { added },
    });
    return sendRateAdmins(await chatRow(row.chat_id), sid, 0,
      added ? `✅ ${faNum(added)} شناسه اضافه شد.` : 'همهٔ این شناسه‌ها از قبل در فهرست بودند (یا فهرست پر است).');
  }

  async function createRate(row, link, parsed, from) {
    const rates = req('./station-rates');
    const by = [from?.first_name, from?.last_name].filter(Boolean).join(' ') || (from?.username ? `@${from.username}` : '');
    await rates.create(link.station_id, { petrol: parsed.petrol, diesel: parsed.diesel, chatId: row.chat_id, by });
    //  نامِ مدیرِ نرخ را یاد بگیر تا فهرست به‌جای عدد نام نشان بدهد
    if (by && from?.id) {
      await query(`UPDATE station_rate_admins SET label=$3 WHERE station_id=$1 AND tg_id=$2 AND label=''`,
        [link.station_id, String(from.id), by.slice(0, 60)]).catch(() => {});
    }
    const online = await desktopOnline(link.station_id);
    return send(row.chat_id,
      `⏳ نرخِ اتحادیهٔ «${link.station_name}»: ${rateLine(parsed)}\n`
      + 'به برنامهٔ کامپیوترِ همین پمپ فرستاده شد و اگر برنامه روشن است همین حالا می‌نشیند؛ نشست، همین‌جا خبر می‌دهم.\n'
      + (online ? '' : '🖥️ برنامهٔ کامپیوتر الان خاموش است — هر وقت (تا ۲۴ ساعت) روشن شد، خودش می‌گیرد.\n')
      + 'فقط همین پمپ عوض می‌شود؛ پمپ‌ها و حساب‌های دیگر دست نمی‌خورند.');
  }

  /** برنامه گفت نشست (یا نشد) — از `routes/pump-device.js`. */
  async function rateDone(row) {
    if (!row || !row.chat_id) return;
    const st = await one('SELECT name FROM stations WHERE id=$1', [row.station_id]);
    const p = { petrol: row.petrol === null ? null : Number(row.petrol), diesel: row.diesel === null ? null : Number(row.diesel) };
    await send(row.chat_id, row.status === 'applied'
      ? `✅ نرخِ اتحادیهٔ «${st ? st.name : 'پمپ'}» در برنامهٔ کامپیوتر نشست: ${rateLine(p)}`
      : `⚠️ نرخِ «${st ? st.name : 'پمپ'}» در برنامهٔ کامپیوتر ننشست${row.note ? `: ${row.note}` : ''}. از خودِ برنامه (مفاد/ضرر ← نرخ اتحادیه) بنویسید.`);
  }

  /* ── ورودی ─────────────────────────────────────────────────────── */

  async function onMessage(m) {
    const chat = m.chat;
    if (!chat || chat.type === 'channel') return null;
    if (m.migrate_to_chat_id) return migrateChat(chat.id, m.migrate_to_chat_id);

    //  ↩️ جوابِ میرزا به پیامِ یک مشتری — پیش از هر چیزِ دیگر
    if (m.reply_to_message && (chat.type === 'private' || chat.type === 'group' || chat.type === 'supergroup')) {
      if (await onRelayReply(m)) return null;
    }

    const cmd = parseCommand(m.text, await username());
    if (chat.type === 'group' || chat.type === 'supergroup') return onGroup(chat, cmd, m.from, m.text || '');
    if (chat.type !== 'private') return null;
    if (typeof m.text !== 'string' || !m.text.trim()) return null;
    return onPrivate(chat, cmd, m.text, m.from);
  }

  /** کارهایی که در گروه فقط مدیر (یا وصل‌کننده) می‌کند. */
  const MANAGE = new Set(['settings', 'toggle', 'quiet', 'unlink', 'unlink_yes', 'code', 'rm', 'rmy', 'addacct', 'chansync', 'mzadd', 'mzrm']);

  async function onCallback(q) {
    const answer = (text = '', alert = false) => call('answerCallbackQuery', {
      callback_query_id: q.id, ...(text ? { text: String(text).slice(0, 190), show_alert: alert } : {}),
    });
    const chat = q.message?.chat;
    if (!chat) return answer();
    let row = await chatRow(chat.id);
    //  «💬 چت‌های میرزا» به وصل شدن با ایمیل بند نیست — گروهی که بات تازه در آن
    //  آمده و هنوز هیچ فرمانی نگرفته هم باید بتواند آن را باز کند.
    if (!row && ['mirza', 'mzadd', 'help'].includes(String(q.data || '').split(':')[0])) {
      const kind = chat.type === 'private' ? 'private' : (chat.type === 'channel' ? 'channel' : 'group');
      if (kind !== 'channel') row = await ensureChat(chat, kind);
    }
    if (!row) return answer();
    const editId = Number(q.message?.message_id) || 0;

    //  دکمه‌های هر شعبه شناسهٔ همان پمپ را پشتِ «:» دارند (`code:<id>`)
    const [act, arg = ''] = String(q.data || '').split(':');
    if (MANAGE.has(act) && !(await canManage(row, q.from))) {
      return answer('⛔ فقط مدیرانِ همین گروه این را عوض می‌کنند.', true);
    }
    //  ⛔ شعبه‌ای که به همین گفت‌وگو وصل نیست هیچ‌وقت از روی دکمه پذیرفته نمی‌شود
    const branch = async () => {
      const links = isLinked(row) ? await linksOf(row) : [];
      return links.find(l => l.station_id === arg) || null;
    };

    switch (act) {
      //  📷 عکس و PDF از خودِ برنامه — ⛔ شعبه فقط اگر به همین گفت‌وگو وصل است
      case 'tanks': {
        if (!arg) { await answer(); return askTanks(row); }
        const l = await branch();
        if (!l) return answer('این شعبه به این گفت‌وگو وصل نیست.', true);
        await answer('⏳ از کامپیوترِ پمپ می‌گیرم…');
        return sendReport(row, l, 'tanks');
      }
      case 'acct': {
        const id = Number(String(q.data || '').split(':')[2]);
        const l = await branch();
        if (!l || !(id > 0)) return answer('این شعبه به این گفت‌وگو وصل نیست.', true);
        await answer('⏳ از کامپیوترِ پمپ می‌گیرم…');
        return sendReport(row, l, 'debtor', { id });
      }
      case 'acctn': {
        const ref = nameRefs.get(arg);
        if (!ref) return answer('این دکمه کهنه شده؛ دوباره نام را بنویسید.', true);
        const links = isLinked(row) ? await linksOf(row) : [];
        const l = links.find(x => x.station_id === ref.stationId);
        if (!l) return answer('این شعبه به این گفت‌وگو وصل نیست.', true);
        await answer('⏳ از کامپیوترِ پمپ می‌گیرم…');
        return sendReport(row, l, 'debtor', { name: ref.name });
      }
      case 'menu': await answer(); return sendMenu(row, '', editId);
      case 'status': await answer(); return sendStatus(row, editId);
      case 'app': await answer(); return sendApp(row, editId);
      case 'code': {
        if (arg) {
          //  ⛔ شعبهٔ بیگانه یا جداشده: فقط «نیست» — هرگز جدا کردنِ کلِ گفت‌وگو
          const l = await branch();
          if (!l) return answer('این شعبه به این گفت‌وگو وصل نیست.', true);
          return answer(`🔑 کدِ پمپِ «${l.station_name}»:  ${await accessCodeOf(l.station_id)}`, true);
        }
        const link = await linkOf(row);
        if (!link) { await answer(); return notLinked(row, editId); }
        //  ⚠️ فقط برای همان کسی که زد — پنجرهٔ خصوصیِ تلگرام، نه پیامِ گروه
        return answer(`🔑 کدِ پمپِ «${link.station_name}»:  ${await accessCodeOf(link.station_id)}`, true);
      }
      case 'share':
      case 'group':
      case 'channel':
        await answer();
        return row.kind === 'private' ? sendShare(row, editId) : null;
      case 'settings': await answer(); return sendSettings(row, editId);
      case 'help':
        await answer();
        return show(row.chat_id, HELP, isLinked(row) ? [[BACK]] : welcomeKeyboard(), editId);
      case 'quiet': {
        if (!isLinked(row)) { await answer(); return notLinked(row, editId); }
        await ensureOutbox();
        await query('UPDATE telegram_chats SET quiet = NOT quiet, updated_at=$2 WHERE chat_id=$1', [row.chat_id, now()]);
        await answer('✔️ ذخیره شد');
        return sendSettings(await chatRow(row.chat_id), editId, '✔️ ذخیره شد.');
      }
      case 'toggle': {
        if (!isLinked(row)) { await answer(); return notLinked(row, editId); }
        await query('UPDATE telegram_chats SET only_out = NOT only_out, updated_at=$2 WHERE chat_id=$1',
          [row.chat_id, now()]);
        await answer('✔️ ذخیره شد');
        return sendSettings(await chatRow(row.chat_id), editId, '✔️ ذخیره شد.');
      }
      case 'unlink':
        await answer();
        return isLinked(row) ? askUnlink(row, editId) : notLinked(row, editId);
      case 'unlink_yes': {
        await answer();
        await unlink(row.chat_id);
        if (row.kind !== 'private') {
          return show(row.chat_id, '🔌 این گروه از پمپ جدا شد. دیگر هیچ هشداری این‌جا نمی‌آید.', null, editId);
        }
        await setState(row.chat_id, 'email');
        return show(row.chat_id,
          '🔌 جدا شد. هر وقت خواستید دوباره وصل شوید، ایمیلِ حسابِ پمپ را بفرستید.', [downloadRow()], editId);
      }
      case 'reset': {
        await answer();
        if (row.kind !== 'private' || (isLinked(row) && !row.state)) return null;
        await setState(row.chat_id, 'email');
        return show(row.chat_id, '✉️ ایمیلِ حسابِ پمپ را بفرستید.', null, editId);
      }
      case 'addacct': {
        //  شعبهٔ دیگر = حسابِ پمپِ دیگر: همان ایمیل ⇐ کدِ همان ایمیل
        await answer();
        if (row.kind !== 'private' || !isLinked(row)) return null;
        await setState(row.chat_id, 'email');
        return show(row.chat_id,
          '➕ ایمیلِ حسابِ پمپِ شعبهٔ دیگر را بفرستید. یک کدِ شش‌رقمی به همان ایمیل می‌رود.\n'
          + 'شعبه‌های فعلی دست نمی‌خورند؛ هر شعبه با نامِ خودش جدا می‌آید.\n\n(انصراف: /cancel)',
          null, editId);
      }
      case 'rm': {
        await answer();
        const l = await branch();
        if (!l) return sendSettings(row, editId);
        return show(row.chat_id,
          `شعبهٔ «${l.station_name}» از این ${row.kind === 'private' ? 'گفت‌وگو' : 'گروه'} جدا شود؟ شعبه‌های دیگر می‌مانند.`,
          [[
            { text: '🔌 بله، جدا کن', callback_data: `rmy:${l.station_id}` },
            { text: 'نه', callback_data: 'settings' },
          ]], editId);
      }
      case 'rmy': {
        await answer();
        const l = await branch();
        if (!l) return sendSettings(row, editId);
        await removeLink(row.chat_id, l.station_id);
        const left = await chatRow(row.chat_id);
        if (!isLinked(left)) return notLinked(left, editId);
        return sendSettings(left, editId, `🔌 شعبهٔ «${l.station_name}» جدا شد.`);
      }
      case 'chansync': {
        await answer();
        if (row.kind !== 'private' || !isLinked(row)) return null;
        const r = await syncChannels(row);
        const where = (r.channels || []).map(c => `«${c.title || 'کانال'}»`).join('، ');
        return show(row.chat_id,
          r.added
            ? `✅ ${faNum(r.added)} پیوندِ تازه — هر شعبهٔ این گفت‌وگو حالا در ${where} هم می‌آید.`
            : `همهٔ شعبه‌ها از قبل در ${where || 'کانال'} بودند.`,
          [[BACK]], editId);
      }
      case 'mirza': await answer(); return sendMirza(row, editId);
      case 'mzadd': await answer(); return askRelayCode(row, q.from, editId);
      case 'mzrm': {
        const had = (await relaysOf(row.chat_id)).find(r => r.station_id === arg);
        if (!had) { await answer(); return sendMirza(row, editId); }
        await query('DELETE FROM telegram_relays WHERE station_id=$1 AND chat_id=$2', [arg, row.chat_id]);
        await answer('✔️ خاموش شد');
        return sendMirza(await chatRow(row.chat_id), editId,
          `🔌 پیامِ مشتری‌های «${had.station_name}» دیگر این‌جا نمی‌آید.`);
      }
      case 'rad': {
        await answer();
        const own = await ownedStations(row);
        if (!own.length) {
          return show(row.chat_id, '👮 «مدیرانِ نرخ» را فقط صاحبِ پمپ (با ایمیل وصل) در گفت‌وگوی خصوصی تعیین می‌کند.', [[BACK]], editId);
        }
        if (own.length === 1) return sendRateAdmins(row, own[0].station_id, editId);
        return show(row.chat_id, '👮 مدیرانِ نرخِ کدام شعبه؟',
          [...own.map(l => [{ text: `⛽ ${l.station_name}`.slice(0, 50), callback_data: `radst:${l.station_id}` }]), [BACK]], editId);
      }
      case 'radst': await answer(); return sendRateAdmins(row, arg, editId);
      case 'radadd': {
        await answer();
        if (!(await ownedStations(row)).some(l => l.station_id === arg)) return sendSettings(row, editId);
        await query(`UPDATE telegram_chats SET state='rateadmin', pending_station=$2, updated_at=$3 WHERE chat_id=$1`,
          [row.chat_id, arg, now()]);
        return show(row.chat_id,
          '🆔 شناسهٔ عددیِ تلگرامِ مدیر، کارفرما یا میرزا را بفرستید — چند تا با فاصله هم می‌شود.\n'
          + 'شناسه را نمی‌داند؟ همان شخص به همین بات /myid بفرستد، عدد را می‌گیرد.\n\n(انصراف: /cancel)', null, editId);
      }
      case 'radrm': {
        await answer();
        const a = await one('SELECT * FROM station_rate_admins WHERE id=$1', [arg]);
        //  ⛔ فقط صاحبِ همان پمپ از فهرستِ همان پمپ برمی‌دارد
        if (!a || !(await ownedStations(row)).some(l => l.station_id === a.station_id)) return sendSettings(row, editId);
        await query('DELETE FROM station_rate_admins WHERE id=$1', [a.id]);
        return sendRateAdmins(await chatRow(row.chat_id), a.station_id, editId, '🗑 برداشته شد.');
      }
      case 'rg': {
        //  ‎rg:<پمپ>:<پطرول>:<دیزل>‎ — ⛔ فقط شعبه‌ای که به همین گفت‌وگو وصل است
        //  ⛔ کسی که دکمه را می‌زند باید خودش مدیرِ نرخِ همان پمپ باشد
        const parts = String(q.data || '').split(':');
        const l = (await rateStations(q.from)).find(x => x.station_id === arg);
        if (!l) return answer('⛔ شما مدیرِ نرخِ این پمپ نیستید.', true);
        const num = (v) => (v === '-' || v === undefined ? null : Number(v));
        const parsed = req('./station-rates').parse(
          `پطرول ${parts[2] === '-' ? '' : parts[2]} دیزل ${parts[3] === '-' ? '' : parts[3]}`);
        if (!parsed || parsed.bad || (num(parts[2]) === null && num(parts[3]) === null)) {
          return answer('این دکمه کهنه است؛ نرخ را دوباره بنویسید.', true);
        }
        await answer('⏳ فرستاده شد');
        return createRate(row, l, parsed, q.from);
      }
      case 'rate_help': await answer(); return rateHelp(row, editId);
      default: return answer();
    }
  }

  /** بات را از گروه بیرون کردند، یا کاربر بات را بست — یا مدیرِ کانال شد. */
  async function onMembership(u) {
    const status = u.new_chat_member?.status;
    if (status === 'left' || status === 'kicked') return forgetChat(u.chat.id);
    if (u.chat?.type === 'channel') {
      //  مدیر شد و می‌تواند بنویسد ⇒ وصل؛ مدیری بی «ارسالِ پیام» ⇒ فراموش
      const canPost = status === 'administrator' && u.new_chat_member?.can_post_messages !== false;
      return canPost ? onChannelAdmin(u) : forgetChat(u.chat.id);
    }
    return null;
  }

  /*
   *  ⛔ **هر به‌روزرسانی یک بار** — گزارشِ صاحب سامانه (۱۴۰۵/۰۷/۱۳، با عکس):
   *  «پیام‌ها را دو بار ارسال می‌کند.» ریشه در `start`/`reload` بود (پایین را
   *  ببینید)؛ این فقط کمربندِ دوم است: شمارهٔ به‌روزرسانی‌های اخیر نگه داشته
   *  می‌شود و تکراری همان‌جا رد می‌شود.
   */
  const seenUpdates = new Set();
  function firstTime(id) {
    if (id === undefined || id === null) return true;
    if (seenUpdates.has(id)) return false;
    seenUpdates.add(id);
    if (seenUpdates.size > 500) seenUpdates.delete(seenUpdates.values().next().value);
    return true;
  }

  /** یک «به‌روزرسانی»ِ تلگرام. هر خطا همین‌جا می‌ماند و بقیه را نمی‌شکند. */
  async function handleUpdate(u) {
    if (!firstTime(u?.update_id)) return null;
    try {
      if (u.message) return await onMessage(u.message);
      if (u.callback_query) return await onCallback(u.callback_query);
      if (u.my_chat_member) return await onMembership(u.my_chat_member);
    } catch (err) {
      console.error('[telegram:update]', hide(err.message));
    }
    return null;
  }

  /* ══════════════════════════════════════════════════════════════════
     هشدار ⇒ صفِ فرستادن
     ══════════════════════════════════════════════════════════════════ */

  function isOut(e) {
    return e.kind === 'stock_out' || (e.kind === 'debt' && e.data?.state === 'out');
  }

  function iconOf(e) {
    const out = isOut(e);
    const who = e.kind === 'shift' ? (shiftIcon(e.data?.key) || '💰') : e.kind === 'debt' ? '👤' : '🛢️';
    return `${who}${out ? '🔴' : '🟡'}`;
  }

  /**
   * «📋 دستورِ کار» — کارهایی که همین حالا باید انجام شود.
   *
   * ⛔ هیچ قاعده‌ای این‌جا ساخته نمی‌شود: هر خط همان `a`ی است که برنامهٔ
   * پمپ کنارِ هر هشدار فرستاده (`StationSnapshot.Alerts`). این‌جا فقط کنارِ
   * هم چیده و تکراری‌ها یکی می‌شوند.
   */
  function actionLines(actions) {
    const seen = new Set();
    const out = [];
    for (const a of actions) {
      const v = String(a || '').trim();
      if (!v || seen.has(v)) continue;
      seen.add(v);
      out.push(`▫️ ${v}`);
    }
    return out;
  }

  /**
   * ══ یک هشدار = یک پیامِ کوتاه (۱۴۰۵/۰۷/۲۱، بارِ دوم) ═══════════════════════
   * صاحب سامانه: «همه پیام‌ها یک‌جا هستند… همه متنی‌اند… گفتم عالی باشد، ساده و دقیق.»
   * پس هر هشدار پیامِ خودش را دارد: یک خطِ خبر، یک خطِ «👉 چه کنم»، و دکمهٔ خودش (‎📷‎).
   * نامِ پمپ فقط وقتی می‌آید که این گفت‌وگو به بیش از یک پمپ وصل است.
   * هشدارِ مخزن عکسِ خودِ مخزن را می‌گیرد (`tankAlertImage`)، نه متن.
   */
  function shortAlert(e, stationName = '') {
    const action = String(e.data?.action || '').trim();
    return `${iconOf(e)} ${e.title || e.body || ''}`
      + (action ? `\n👉 ${action}` : '')
      + (stationName ? `\n⛽ «${stationName}»` : '');
  }

  /** بیشترین پیامِ جدا در یک دور برای یک گفت‌وگو؛ بیشتر ⇒ یک خطِ «و N هشدارِ دیگر». */
  const MAX_SEPARATE = 5;

  /** متنِ چند هشدار در یک پیام — فقط برای «/status»ِ کهنه و آزمون؛ هشدارِ تازه `shortAlert` است. */
  function formatAlert(stationName, items) {
    const lines = items.map(e => `${iconOf(e)} ${e.title || e.body || ''}`);
    const todo = actionLines(items.map(e => e.data?.action));
    //  ⚠️ هشدار خبر است، نه منو (قاعدهٔ ۳) — تنها دکمه‌هایش «📷 حسابِ فلانی» و «🛢 مخزن‌ها»
    //  است (`alertKeyboard`)، به خواستهٔ صاحب سامانه (۱۴۰۵/۰۷/۲۱): «پیامِ کوچک بیاید؛ اگر
    //  زدم، عکس را بدهد.»
    return `🚨 هشدارِ پمپِ «${stationName}»\n\n${lines.join('\n')}`
      + (todo.length ? `\n\n📋 دستورِ کار:\n${todo.join('\n')}` : '')
      + '\n\n📊 همهٔ هشدارهای باز: /status';
  }

  /**
   * دکمه‌های زیرِ هشدار: «📷 حسابِ فلانی» برای هر قرض‌دار (تا ۴ تا) و «🛢 مخزن‌ها» اگر هشدارِ
   * مخزنی بود. ⇒ عکس و PDF از **خودِ** برنامهٔ کامپیوتر (`sendReport`).
   * ⚠️ شناسهٔ قرض‌دار از کلیدِ خودِ هشدار (`d<id>-…`)؛ دکمه‌ای که از ۶۴ بایتِ تلگرام
   * بلندتر شود ساخته نمی‌شود.
   */
  function alertKeyboard(stationId, items) {
    const rows = [];
    const seen = new Set();
    for (const e of items) {
      const m = /^d(\d+)-/.exec(String(e.data?.key || ''));
      if (e.kind !== 'debt' || !m || seen.has(m[1]) || seen.size >= 4) continue;
      const data = `acct:${stationId}:${m[1]}`;
      if (Buffer.byteLength(data) > 64) continue;
      seen.add(m[1]);
      rows.push([{ text: `📷 حسابِ ${String(e.data?.who || '').slice(0, 28)}`, callback_data: data }]);
    }
    if (items.some(e => e.kind === 'stock_out' || e.kind === 'low_stock') && Buffer.byteLength(`tanks:${stationId}`) <= 64) {
      rows.push([{ text: '🛢 مخزن‌ها', callback_data: `tanks:${stationId}` }]);
    }
    return rows.length ? rows : null;
  }

  /** کلیدِ یک هشدار بی حالش — همان `pump-state.subjectOf`. */
  function subjectOf(key) {
    return req('./pump-state').subjectOf(key);
  }

  /**
   * هشدارهایی که بسته شدند ⇒ «✅ برطرف شد» در همان گفت‌وگوها.
   *
   * خواستهٔ صاحب سامانه: «لایو آپدیت توی گروه». کارمندی که دیروز «اضافه
   * نده» شنید، باید امروز هم بشنود که آن حساب دوباره موجودی دارد — وگرنه
   * مشتری را بی‌دلیل برمی‌گرداند.
   *
   * صدا‌زننده `pump-state.publish` است. ⛔ هیچ‌وقت استثنا بیرون نمی‌دهد.
   * ⚠️ «تمام شد ⇒ کم مانده» برطرف شدن نیست، فقط عوض شدنِ حال است؛ آن را
   * خودِ `publish` کنار می‌گذارد (هشدارِ تازه‌اش جداگانه می‌رود).
   */
  /**
   * گفت‌وگوهایی که هشدارِ یک پمپ به آن‌ها می‌رود — تنها جای این تصمیم.
   *
   * خواستهٔ صاحب سامانه: «وقتی گروه وصل کردم، پیام‌ها توی گروه بیاید؛ اگر
   * گروهی انتخاب نکردم، توی خودِ بات برای یارو.» پس گفت‌وگوی **خصوصیِ** کسی
   * که خودش گروه یا کانالی برای همین پمپ وصل کرده، هشدار نمی‌گیرد — همان خبر
   * در گروهش می‌آید و دو بار شنیدنش همان «تکرار» است.
   *
   * ⚠️ به‌ازای **هر نفر**، نه کلِ پمپ: عضوی که خودش گروهی وصل نکرده (شاید
   * در گروهِ صاحبِ پمپ هم نیست) هشدارش را همچنان در خصوصی می‌گیرد.
   * ⚠️ گروهی که صاحبش دیگر عضوِ پمپ نیست حساب نمی‌شود — وگرنه خصوصی ساکت
   * می‌ماند و گروه هم هیچ نمی‌گرفت.
   * ⛔ قیدِ ۳ سرِ جایش است: عضو، پمپ و حساب — همین لحظه.
   */
  async function alertChats(stationId) {
    if (!(await botStations([{ station_id: stationId }])).length) return [];
    await ensureOutbox();
    return many(
      `SELECT c.chat_id, c.kind, c.only_out, c.quiet, c.calm_day, c.calm_n, l.announced_day, s.name AS station_name
         FROM telegram_chat_links l
         JOIN telegram_chats c ON c.chat_id=l.chat_id
         JOIN stations s ON s.id=l.station_id AND s.status='active'
         JOIN station_members m ON m.station_id=l.station_id AND m.user_id=l.user_id AND m.status='active'
         JOIN users u ON u.id=l.user_id AND u.status='active'
        WHERE l.station_id=$1
          AND NOT (c.kind='private' AND EXISTS (
            SELECT 1 FROM telegram_chat_links g
              JOIN telegram_chats gc ON gc.chat_id=g.chat_id AND gc.kind IN ('group','channel')
             WHERE g.station_id=l.station_id AND g.user_id=l.user_id))`,
      [stationId]
    );
  }

  /** نامِ نخستین گروه/کانالی که یکی از شعبه‌های این گفت‌وگوی خصوصی را دارد — یا تهی. */
  async function groupOfUser(row) {
    if (!row || row.kind !== 'private' || !isLinked(row)) return null;
    const g = await one(
      `SELECT gc.title FROM telegram_chat_links p
         JOIN telegram_chat_links g ON g.station_id=p.station_id AND g.user_id=p.user_id
         JOIN telegram_chats gc ON gc.chat_id=g.chat_id AND gc.kind IN ('group','channel')
        WHERE p.chat_id=$1
        ORDER BY g.linked_at LIMIT 1`,
      [String(row.chat_id)]
    );
    return g ? (g.title || 'گروهِ شما') : null;
  }

  async function notifyResolved(stationId, closed) {
    try {
      const items = (closed || []).filter(a => a && a.k);
      if (!items.length || !stationId) return { queued: 0 };
      if (!(await active())) return { queued: 0, skipped: 'off' };
      const chats = await alertChats(stationId);
      const t = now();
      let queued = 0;
      for (const c of chats) {
        const mine = c.only_out ? items.filter(a => a.s === 'out') : items;
        if (!mine.length) continue;
        const lines = mine.map((a) => (String(a.k).startsWith('tank-')
          ? `🛢️🟢 ${a.n || 'مخزن'} — دیگر کم نیست`
          : String(a.k).startsWith('cash-') ? `💰🟢 ${a.n}: کسریِ صندوق درست شد`
            : String(a.k).startsWith('shift-') ? `⏰🟢 ${a.n}: شیفت بسته شد`
              : `👤🟢 ${a.n}${a.f ? ` — ${a.f}` : ''}: دیگر هشدار ندارد`));
        await queueLong(c.chat_id, stationId, `✅ برطرف شد — پمپِ «${c.station_name}»\n\n${lines.join('\n')}`, t);
        queued += 1;
      }
      if (queued) kick();
      return { queued };
    } catch (err) {
      console.error('[telegram:resolved]', hide(err.message));
      return { queued: 0, error: err.message };
    }
  }

  /**
   * هشدارهای تازهٔ یک پمپ ⇒ صفِ تلگرامِ هر گفت‌وگوی وصل‌شده.
   *
   * صدا‌زننده `events.notify` است، درست کنارِ پوش.
   *
   * ⛔ **هیچ‌وقت استثنا بیرون نمی‌دهد.** ثبتِ خبر کارِ اصلی است و تلگرام
   * رفاه — همان قاعدهٔ پوش.
   */
  /* ── 🔕 کنترلِ شلوغی (۱۴۰۵/۰۷/۲۱) ───────────────────────────────────
     خواستهٔ صاحب سامانه: «جوری نباشد که شلوغی ایجاد شود و صدها پیام روزی بیاید —
     این را هم کنترل‌شده درست کن.»
       • ساعتِ آرام (پیش‌فرض روشن، ۱۰ شب تا ۷ صبحِ کابل): فقط هشدارِ فوری.
       • سقفِ روزانه: بیش از `CALM_CAP` پیامِ «کم مانده» در روز نه؛ یک بار گفته می‌شود.
     ⛔ هشدارِ فوری («تمام شد»، «اضافه داده شد»، مخزنِ خالی) هرگز نگه داشته نمی‌شود.
     ⛔ چیزی گم نمی‌شود: هشدارِ باز در «/status» و اعلامیهٔ صبح هست. */
  const CALM_CAP = 12;
  let QUIET_FROM = 22;
  let QUIET_TO = 7;
  const isQuietHour = (t) => {
    if (QUIET_FROM === QUIET_TO) return false;
    const hr = kabulHour(t);
    return hr >= QUIET_FROM || hr < QUIET_TO;
  };
  /** فقط برای آزمون‌ها: ساعتِ آرام را جابه‌جا یا (با `from === to`) خاموش می‌کند — آزمون‌ها به ساعتِ کابل وابسته نباشند. */
  function setQuietHours(from = 22, to = 7) { QUIET_FROM = from; QUIET_TO = to; }

  /** این پیامِ «کم مانده» برود؟ ⇒ ‎{ ok, notice }‎ — شمارنده همین‌جا یکی بالا می‌رود. */
  async function calmGate(c, t) {
    if (c.quiet !== false && isQuietHour(t)) return { ok: false };
    const day = kabulDay(t);
    const r = await one(
      `UPDATE telegram_chats
          SET calm_n = CASE WHEN calm_day=$2 THEN calm_n + 1 ELSE 1 END, calm_day=$2
        WHERE chat_id=$1 RETURNING calm_n`,
      [String(c.chat_id), day]
    );
    const n = Number(r?.calm_n) || 1;
    if (n <= CALM_CAP) return { ok: true };
    if (n === CALM_CAP + 1) {
      return {
        ok: false,
        notice: `🔕 امروز ${faNum(CALM_CAP)} هشدارِ «کم مانده» آمد؛ بقیهٔ «کم مانده»های امروز دیگر جدا نمی‌آیند `
          + '(«تمام شد» همچنان همان لحظه می‌آید). همه را در /status و اعلامیهٔ فردا صبح ببینید.',
      };
    }
    return { ok: false };
  }

  /** فقط برای آزمون‌ها: هشدارِ مخزن بی درخواستِ عکس از برنامه (آزمونی که برنامهٔ واقعی ندارد). */
  let AUTO_TANK_IMAGE = true;
  function setAutoTankImage(on) { AUTO_TANK_IMAGE = Boolean(on); }

  /**
   * هشدارِ مخزن ⇒ همان عکسِ مخزنِ برنامه (`BotReports`)، خبرِ هشدار بالای زیرنویسش.
   * یک درخواست برای همهٔ گفت‌وگوهای این پمپ. ⛔ نشد (برنامهٔ کهنه، خاموش، دیر) ⇒ همان
   * خبر به شکلِ متنِ کوتاه با دکمهٔ «🛢 مخزن‌ها» — هشدار هیچ‌وقت گم نمی‌شود.
   */
  async function tankAlertImage(stationId, targets) {
    let out = null;
    try {
      const state = await req('./pump-state').get(stationId).catch(() => null);
      if (state && now() - state.updatedAt <= STALE_MS) {
        out = await req('./station-reports').request(stationId, 'tanks', {});
      }
    } catch { out = null; }
    const hasPhoto = Array.isArray(out?.files) && out.files.some(f => String(f.mime).startsWith('image/'));
    for (const { c, tanks, name } of targets) {
      const head = tanks.map(e => shortAlert(e, '')).join('\n') + (name ? `\n⛽ «${name}»` : '');
      if (hasPhoto) {
        try {
          await deliverReport(c.chat_id, { ...out, caption: `${head}\n\n${String(out.caption || '')}`.trim() });
          continue;
        } catch { /* ⇒ متن */ }
      }
      await queueLong(c.chat_id, stationId, head, now(), c.kind === 'channel' ? null : alertKeyboard(stationId, tanks));
      kick();
    }
  }

  async function notifyStation(stationId, saved, at = 0) {
    try {
      const worthy = (saved || []).filter(e => ALERT_KINDS.includes(e.kind));
      if (!worthy.length || !stationId) return { queued: 0 };
      if (!(await active())) return { queued: 0, skipped: 'off' };

      //  ⛔ قیدِ ۳: عضو، پمپ و حساب — همین لحظه
      const chats = await alertChats(stationId);
      const t = at || now();
      let queued = 0;
      //  گفت‌وگویی که به چند پمپ وصل است ⇒ نامِ پمپ زیرِ هر هشدار
      const multi = new Set();
      try {
        const ids = chats.map(c => String(c.chat_id));
        if (ids.length) {
          for (const r of await many(
            `SELECT chat_id FROM telegram_chat_links WHERE chat_id = ANY($1) GROUP BY chat_id HAVING COUNT(*) > 1`, [ids]
          )) multi.add(String(r.chat_id));
        }
      } catch { /* نامِ پمپ رفاه است */ }
      const tankChats = [];
      for (const c of chats) {
        let items = c.only_out ? worthy.filter(isOut) : worthy;
        //  🔕 کنترلِ شلوغی: «تمام شد»/«اضافه داده شد»/مخزنِ خالی همیشه همان لحظه؛ «کم مانده» در
        //  ساعتِ آرام نمی‌آید و در روز سقف دارد — همه در «/status» و اعلامیهٔ صبح هست.
        //  💰⏰ کسری و شیفتِ بسته‌نشده: سقفِ «کم مانده» را نمی‌خورند (پولِ گم‌شده است)، ولی ساعتِ
        //  آرام را رعایت می‌کنند — شب در «/status» و اعلامیهٔ صبح هست.
        const calm = items.filter(e => !isOut(e) && e.kind !== 'shift');
        if (calm.length) {
          const gate = await calmGate(c, t);
          if (!gate.ok) items = items.filter(e => isOut(e) || e.kind === 'shift');
          if (gate.notice) await queueLong(c.chat_id, stationId, gate.notice, now());
        }
        if (c.quiet !== false && isQuietHour(t)) items = items.filter(e => e.kind !== 'shift');
        if (!items.length) continue;
        const name = multi.has(String(c.chat_id)) ? c.station_name : '';
        const isTank = (e) => e.kind === 'stock_out' || e.kind === 'low_stock';
        const tanks = items.filter(isTank);
        const rest = items.filter(e => !isTank(e));
        //  مخزن ⇒ عکسِ خودِ مخزن با همین خبر در زیرش (پس از حلقه، یک درخواست برای همه)
        if (tanks.length && AUTO_TANK_IMAGE) tankChats.push({ c, tanks, name });
        else rest.unshift(...tanks);
        for (const e of rest.slice(0, MAX_SEPARATE)) {
          await queueLong(c.chat_id, stationId, shortAlert(e, name), now(),
            c.kind === 'channel' ? null : alertKeyboard(stationId, [e]));
          queued += 1;
        }
        //  ⛔ بی سقف (قاعدهٔ «همهٔ هشدارها می‌آیند، بریده نمی‌شود»)، ولی نه صد پیام: بقیه در **یک**
        //  فهرستِ کوتاه — هر هشدار یک خط.
        if (rest.length > MAX_SEPARATE) {
          const more = rest.slice(MAX_SEPARATE);
          await queueLong(c.chat_id, stationId,
            `➕ ${faNum(more.length)} هشدارِ دیگرِ همین لحظه${name ? ` — ⛽ «${name}»` : ''}:\n`
              + more.map(e => `${iconOf(e)} ${e.title || e.body || ''}`).join('\n'), now());
          queued += 1;
        }
      }
      if (queued) kick();
      if (tankChats.length) {
        tankAlertImage(stationId, tankChats).catch(err => console.error('[telegram:tankalert]', hide(err.message)));
      }
      return { queued, tanks: tankChats.length };
    } catch (err) {
      console.error('[telegram:notify]', hide(err.message));
      return { queued: 0, error: err.message };
    }
  }

  /* ══════════════════════════════════════════════════════════════════
     📢 اعلامیهٔ صبح — «یکم ریس‌مدلی»
     ══════════════════════════════════════════════════════════════════

     خواستهٔ صاحب سامانه (۱۴۰۵/۰۷/۱۴): «یک اعلامیه بده که تیل بگیرید یا به
     یارو تیل ندید یا از شرکت انقد قرض‌دار استین… یکم به فکرِ پمپ هم باش.»

     هر روز یک بار، از ساعتِ ۸ صبحِ کابل، به همان گفت‌وگوهایی که هشدار
     می‌گیرند (`alertChats` — گروهِ وصل ⇒ فقط گروه). ⛔ هیچ عددی این‌جا حساب
     نمی‌شود: مخزن، هشدارها و بدهیِ شرکت‌ها همه از حالِ زنده‌ای است که
     برنامهٔ پمپ فرستاده؛ این‌جا فقط چیده می‌شوند. */

  const ANNOUNCE_HOUR = 8;
  const KABUL_MS = 4.5 * 3600 * 1000;
  const kabulDay = (t) => new Date(t + KABUL_MS).toISOString().slice(0, 10);
  const kabulHour = (t) => new Date(t + KABUL_MS).getUTCHours();

  /** حالِ یک هشدارِ قرض‌دار از کلیدش — همان پسوندهای `StationSnapshot.Alerts`. */
  function levelOf(k) {
    const m = /-(out|low|w70|w90|over)$/.exec(String(k || ''));
    return m ? m[1] : '';
  }

  function announcementText(stationName, state, t = now()) {
    const parts = [`📢 اعلامیهٔ امروزِ پمپِ «${stationName}»`, 'صبح بخیر، تیم! اول این‌ها را ببینید:'];
    if (t - state.updatedAt > STALE_MS) {
      parts.push('', `⚠️ برنامهٔ کامپیوتر ${ago(state.updatedAt)} آخرین بار خبر داد — اول آن را روشن کنید تا عددها تازه باشند.`);
    }
    const tanks = tankLines(state.tank);
    if (tanks.length) parts.push('', '🛢️ مخزن:', ...tanks);

    const debts = state.alerts.filter(a => !String(a.k).startsWith('tank-') && !shiftIcon(a.k));
    const groups = [
      ['over', '🔴 بیشتر از حسابشان تیل/پول برده‌اند — بازپرسی شود:'],
      ['out', '🔴 حسابشان تمام شده — دیگر ندهید:'],
      ['w90', '🟡 ۹۰٪ِ حسابشان رفته — بگویید حساب را پر کنند:'],
      ['low', '🟡 حسابشان کم مانده:'],
      ['w70', '🟡 ۷۰٪ِ حسابشان رفته — متوجه باشید:'],
    ];
    const debtParts = [];
    for (const [lv, label] of groups) {
      const names = debts.filter(a => levelOf(a.k) === lv).map(a => `${a.n}${a.f ? ` (${a.f})` : ''}`);
      if (names.length) debtParts.push(label, `   ${names.join('، ')}`);
    }
    if (debtParts.length) parts.push('', '👥 قرض‌داران:', ...debtParts);

    //  💰⏰ کسریِ صندوق و شیفتِ بسته‌نشده — همان متنِ برنامه
    const shifts = state.alerts.filter(a => shiftIcon(a.k)).map(a => `${shiftIcon(a.k)} ${a.t || a.n || ''}`);
    if (shifts.length) parts.push('', '🧾 صندوق و شیفت‌ها:', ...shifts);

    const owe = oweLines(state.owe);
    if (owe.length) parts.push('', '💼 بدهیِ پمپ به شرکت‌ها:', ...owe);

    const todo = actionLines(state.alerts.map(a => a.a));
    if (owe.length) todo.push('▫️ بدهیِ شرکت‌ها را فراموش نکنید؛ پیش از خریدِ تازه، تسویه را برنامه بریزید.');
    if (todo.length) {
      parts.push('', '📋 دستورِ کارِ امروز:', ...todo);
      parts.push('', '💪 با دقت کار کنید — هر لیتر حساب دارد.');
    } else {
      parts.push('', '✅ همه‌چیز رو‌به‌راه است: مخزن‌ها سالم‌اند و هیچ قرض‌داری از حسابش بیرون نزده. همین‌طور ادامه بدهید.');
    }
    return parts.join('\n');
  }

  let lastAnnounceTry = 0;
  let lastPcWatch = 0;
  let lastSubTick = 0;

  /**
   * اعلامیهٔ صبح برای هر گفت‌وگویی که امروز هنوز نگرفته است.
   * ⛔ هیچ‌وقت استثنا بیرون نمی‌دهد. `t` فقط برای آزمون.
   */
  async function announceTick(t = now()) {
    try {
      if (!(await active())) return { queued: 0, skipped: 'off' };
      if (kabulHour(t) < ANNOUNCE_HOUR) return { queued: 0, skipped: 'early' };
      const day = kabulDay(t);
      const stations = await many('SELECT station_id FROM station_live_state');
      const ps = req('./pump-state');
      let queued = 0;
      for (const { station_id: sid } of stations) {
        const chats = (await alertChats(sid)).filter(c => c.announced_day !== day);
        if (!chats.length) continue;
        const state = await ps.get(sid);
        if (!state) continue;
        for (const c of chats) {
          //  ⚠️ اول مُهر، بعد صف: دو دورِ هم‌زمان هرگز دو اعلامیه نمی‌سازند
          //  ⚠️ مُهر روی **هر شعبه** است: گروهی با سه شعبه سه اعلامیهٔ جدا می‌گیرد
          const took = await one(
            `UPDATE telegram_chat_links SET announced_day=$3
              WHERE chat_id=$1 AND station_id=$2 AND announced_day IS DISTINCT FROM $3 RETURNING chat_id`,
            [c.chat_id, sid, day]
          );
          if (!took) continue;
          await queueLong(c.chat_id, sid, announcementText(c.station_name, state, t), now());
          queued += 1;
        }
      }
      if (queued) kick();
      return { queued };
    } catch (err) {
      console.error('[telegram:announce]', hide(err.message));
      return { queued: 0, error: err.message };
    }
  }

  /* ── 🖥 برنامهٔ کامپیوتر قطع شد / دوباره وصل شد (۱۴۰۵/۰۷/۲۱) ──────────
     تا امروز فقط اعلامیهٔ صبح می‌گفت «برنامه خبر نداده» — اگر ظهر قطع می‌شد تا فردا
     کسی نمی‌فهمید. حالا: بیش از `PC_DOWN_MS` بی خبر و در ساعتِ کار (۷ صبح تا ۱۰ شب)
     ⇒ یک پیام؛ برگشت ⇒ «✅ دوباره وصل شد». ⛔ هر قطعی یک بار (مُهر در `app_config`)،
     و پمپی که چند روز است خاموش است دیگر هر روز یادآوری نمی‌گیرد. */
  const PC_DOWN_MS = 2 * 3600 * 1000;
  const PC_FORGET_MS = 3 * 24 * 3600 * 1000;

  async function pcWatchTick(t = now()) {
    try {
      if (!(await active())) return { queued: 0, skipped: 'off' };
      const ps = req('./pump-state');
      let queued = 0;
      for (const { station_id: sid } of await many('SELECT station_id FROM station_live_state')) {
        const state = await ps.get(sid);
        if (!state) continue;
        const key = `tg_pcdown:${sid}`;
        const down = (await plans.getConfig(key, '')) === '1';
        const stale = t - state.updatedAt > PC_DOWN_MS;
        let text = '';
        if (stale && !down && !isQuietHour(t) && t - state.updatedAt < PC_FORGET_MS) {
          await plans.setConfig(key, '1');
          text = `🖥 برنامهٔ کامپیوترِ پمپِ «{name}» ${ago(state.updatedAt)} آخرین بار خبر داد.\n`
            + 'اگر باید باز باشد، روشن بودنِ کامپیوتر و اینترنتش را ببینید؛ تا برگردد هشدارهای تازه نمی‌آیند.';
        } else if (!stale && down) {
          await plans.setConfig(key, '');
          text = isQuietHour(t) ? '' : '✅ برنامهٔ کامپیوترِ پمپِ «{name}» دوباره وصل شد.';
        }
        if (!text) continue;
        for (const c of await alertChats(sid)) {
          await queueLong(c.chat_id, sid, text.replace('{name}', c.station_name), now());
          queued += 1;
        }
      }
      if (queued) kick();
      return { queued };
    } catch (err) {
      console.error('[telegram:pcwatch]', hide(err.message));
      return { queued: 0, error: err.message };
    }
  }

  /* ── ⏳ اشتراکِ برنامه رو به پایان (۱۴۰۵/۰۷/۲۱) ─────────────────────
     فقط به گفت‌وگوی **خصوصیِ صاحبِ پمپ** (نه گروهِ کارکنان) — ۵، ۲ و ۱ روز مانده و روزِ
     تمام شدن، هر کدام یک بار، از ساعتِ ۹ صبحِ کابل. ⛔ تاریخ از خودِ سرور (`entitlement`). */
  const SUB_WARN_DAYS = [5, 2, 1, 0];

  async function subTick(t = now()) {
    try {
      if (!(await active())) return { queued: 0, skipped: 'off' };
      if (kabulHour(t) < 9 || isQuietHour(t)) return { queued: 0, skipped: 'hour' };
      const owners = await many(
        `SELECT l.chat_id, l.station_id, s.name AS station_name
           FROM telegram_chat_links l
           JOIN telegram_chats c ON c.chat_id=l.chat_id AND c.kind='private'
           JOIN stations s ON s.id=l.station_id AND s.status='active'
           JOIN station_members m ON m.station_id=l.station_id AND m.user_id=l.user_id
                AND m.status='active' AND m.role='owner'`);
      const ent = req('./entitlement').pump;
      let queued = 0;
      for (const o of owners) {
        const e = await ent.entitlementOf(o.station_id).catch(() => null);
        if (!e) continue;
        const sub = e.subscription?.active ? e.subscription : (e.trial?.active ? e.trial : null);
        const left = sub ? Number(sub.daysLeft) : (e.subscription?.status === 'expired' ? 0 : -1);
        if (!SUB_WARN_DAYS.includes(left)) continue;
        const key = `tg_subwarn:${o.station_id}:${o.chat_id}`;
        const mark = `${left}:${kabulDay(t)}`;
        const prev = await plans.getConfig(key, '');
        if (prev.startsWith(`${left}:`)) continue;             //  همین پله را گفته‌ایم
        await plans.setConfig(key, mark);
        const trial = sub && sub === e.trial;
        const text = left > 0
          ? `⏳ ${trial ? 'دورهٔ آزمایشیِ' : 'اشتراکِ'} برنامهٔ پمپِ «${o.station_name}» ${faNum(left)} روزِ دیگر تمام می‌شود.\n`
            + 'برای تمدید به پشتیبانی پیام بدهید تا برنامه و بات بی‌وقفه کار کنند.'
          : `⚠️ اشتراکِ برنامهٔ پمپِ «${o.station_name}» تمام شد؛ دفترِ شما سالم است. برای تمدید به پشتیبانی پیام بدهید.`;
        await queueLong(o.chat_id, o.station_id, text, now());
        queued += 1;
      }
      if (queued) kick();
      return { queued };
    } catch (err) {
      console.error('[telegram:sub]', hide(err.message));
      return { queued: 0, error: err.message };
    }
  }

  let flushing = false;
  let again = false;

  /**
   * فرستادنِ صف.
   *
   * - ۴۲۹: همان‌قدر که تلگرام گفت صبر، و این دور تمام.
   * - ۴۰۳ یا «گفت‌وگو نیست»: بات را بسته‌اند یا بیرون کرده‌اند ⇒ گفت‌وگو فراموش.
   * - گروهی که ابرگروه شد: شناسهٔ تازه و دوباره.
   * - بقیه (و قطعیِ اینترنت): دوباره، با فاصلهٔ دوبرابرشونده، تا هشت بار —
   *   و هشداری که یک روز نرفت دیگر کهنه است.
   */
  async function flushOutbox({ limit = 25 } = {}) {
    if (flushing) { again = true; return { sent: 0, failed: 0 }; }
    flushing = true;
    let sent = 0;
    let failed = 0;
    try {
      if (!(await token())) return { sent, failed };
      await ensureOutbox();
      const t = now();
      await query(
        `UPDATE telegram_outbox SET failed_at=$1, error=CASE WHEN error='' THEN 'کهنه شد' ELSE error END
          WHERE sent_at IS NULL AND failed_at IS NULL AND created_at < $2`,
        [t, t - GIVE_UP_MS]
      );
      const rows = await many(
        `SELECT * FROM telegram_outbox
          WHERE sent_at IS NULL AND failed_at IS NULL AND next_at <= $1
          ORDER BY created_at LIMIT $2`,
        [t, limit]
      );

      for (const r of rows) {
        let kb = null;
        try { kb = r.markup ? JSON.parse(r.markup) : null; } catch { kb = null; }
        const res = await send(r.chat_id, r.body, kb);
        if (res.ok) {
          await query('UPDATE telegram_outbox SET sent_at=$2, error=\'\' WHERE id=$1', [r.id, now()]);
          sent += 1;
          bot.lastOkAt = now();
          continue;
        }
        const code = Number(res.error_code) || 0;
        const desc = hide(res.description || 'نرفت');
        const migrate = res.parameters?.migrate_to_chat_id;
        if (migrate) {
          await migrateChat(r.chat_id, migrate);
          continue;                                  // دورِ بعد با شناسهٔ تازه
        }
        if (code === 429) {
          const wait = (Number(res.parameters?.retry_after) || 5) * 1000;
          await query('UPDATE telegram_outbox SET next_at=$2, error=$3 WHERE id=$1', [r.id, now() + wait, desc]);
          break;
        }
        if (code === 403 || (code === 400 && /chat not found/i.test(desc))) {
          await query('UPDATE telegram_outbox SET failed_at=$2, error=$3 WHERE id=$1', [r.id, now(), desc]);
          await forgetChat(r.chat_id);
          failed += 1;
          continue;
        }
        const attempts = (Number(r.attempts) || 0) + 1;
        if (attempts >= MAX_ATTEMPTS) {
          await query('UPDATE telegram_outbox SET attempts=$2, failed_at=$3, error=$4 WHERE id=$1',
            [r.id, attempts, now(), desc]);
          failed += 1;
        } else {
          const backoff = Math.min(30_000 * 2 ** (attempts - 1), 30 * 60_000);
          await query('UPDATE telegram_outbox SET attempts=$2, next_at=$3, error=$4 WHERE id=$1',
            [r.id, attempts, now() + backoff, desc]);
        }
        bot.lastError = desc;
      }

      await query(
        `DELETE FROM telegram_outbox
          WHERE (sent_at IS NOT NULL AND sent_at < $1) OR (failed_at IS NOT NULL AND failed_at < $1)`,
        [t - KEEP_MS]
      );
    } catch (err) {
      bot.lastError = hide(err.message);
      console.error('[telegram:outbox]', bot.lastError);
    } finally {
      flushing = false;
    }
    if (again) { again = false; if (bot.running) kick(); }
    return { sent, failed };
  }

  function kick() {
    if (!bot.running) return;
    setImmediate(() => { flushOutbox().catch(() => {}); });
  }

  /* ══════════════════════════════════════════════════════════════════
     حلقهٔ گرفتنِ پیام
     ══════════════════════════════════════════════════════════════════ */

  /** یک دورِ `getUpdates`. شمارهٔ پیام پس از **هر** پیام ذخیره می‌شود. */
  async function pollOnce({ timeout = 25 } = {}) {
    const offset = Number(await plans.getConfig(CFG_OFFSET, '0')) || 0;
    const res = await call('getUpdates', {
      offset,
      timeout,
      allowed_updates: ['message', 'callback_query', 'my_chat_member'],
    }, { timeoutMs: (timeout + 15) * 1000 });
    if (!res.ok) {
      const err = new Error(hide(res.description || 'getUpdates نشد'));
      err.tg = res;
      throw err;
    }
    let next = offset;
    for (const u of res.result || []) {
      next = Math.max(next, Number(u.update_id) + 1);
      await handleUpdate(u);
      await plans.setConfig(CFG_OFFSET, String(next));
    }
    bot.lastOkAt = now();
    bot.lastError = '';
    return (res.result || []).length;
  }

  let stopper = new AbortController();
  function stopSignal() { return stopper.signal; }

  function sleep(ms) {
    return new Promise((resolve) => {
      const timer = setTimeout(resolve, ms);
      timer.unref?.();
      stopper.signal.addEventListener('abort', () => { clearTimeout(timer); resolve(); }, { once: true });
    });
  }

  let outboxTimer = null;

  /*
   *  ⛔ **یک حلقه، نه دو** — ریشهٔ «پیام‌ها دو بار می‌روند».
   *
   *  `reload()` (ذخیرهٔ رمز یا روشن/خاموش در پنل) `stop()` و بی‌درنگ
   *  `start()` می‌زد. حلقهٔ قبلی هنوز وسطِ `getUpdates` بود؛ `start()`
   *  `bot.running` را دوباره راست می‌کرد، پس حلقهٔ قبلی هم **زنده** ادامه
   *  می‌داد — دو حلقه، یک صندوق، هر پیام دو پاسخ. حالا هر حلقه شمارهٔ نسلِ
   *  خودش را دارد و با `start()`ِ تازه کنار می‌رود.
   */
  let generation = 0;

  /** فرمان‌های دکمهٔ «منو»ی تلگرام — «منو نداره». */
  async function setCommands() {
    await call('setMyCommands', {
      scope: { type: 'all_private_chats' },
      commands: [
        { command: 'menu', description: 'منو' },
        { command: 'status', description: 'هشدارهای باز و موجودیِ مخزن' },
        { command: 'find', description: 'جست‌وجوی قرض‌دار (یا فقط نام را بنویسید)' },
        { command: 'tanks', description: 'عکسِ مخزن‌ها، بدهیِ شرکت‌ها و خریدها' },
        { command: 'acct', description: 'عکس و PDFِ حسابِ قرض‌دار: /acct نام' },
        { command: 'code', description: 'اپِ اندروید و آیفون و کدِ پمپ' },
        { command: 'group', description: 'وصل کردنِ گروه یا کانال' },
        { command: 'mirza', description: 'چت‌های میرزا — پیامِ مشتری‌های کیو‌آر' },
        { command: 'myid', description: 'شناسهٔ تلگرامِ من (برای مدیرِ نرخ شدن)' },
        { command: 'settings', description: 'تنظیمات' },
        { command: 'help', description: 'راهنما' },
      ],
    });
    await call('setMyCommands', {
      scope: { type: 'all_group_chats' },
      commands: [
        { command: 'status', description: 'هشدارهای باز و موجودیِ مخزن' },
        { command: 'find', description: 'جست‌وجوی قرض‌دار: /find نام' },
        { command: 'tanks', description: 'عکسِ مخزن‌ها' },
        { command: 'acct', description: 'عکس و PDFِ حساب: /acct نام' },
        { command: 'mirza', description: 'چت‌های میرزا در همین گروه' },
        { command: 'myid', description: 'شناسهٔ تلگرامِ من' },
        { command: 'menu', description: 'منو' },
      ],
    });
    await call('setChatMenuButton', { menu_button: { type: 'commands' } });
    //  نوشتهٔ «پروفایلِ» بات — همان چیزی که پیش از Start دیده می‌شود
    await call('setMyShortDescription', {
      short_description: 'هشدارهای پمپ: حسابِ قرض‌دار تمام شد، کم مانده، مخزن ته کشید — حتی وقتی برنامه بسته است.',
    });
  }

  /** راه‌اندازی — از `index.js`. تا رمز داده نشده، فقط هر چند ثانیه نگاه می‌کند. */
  function start() {
    if (bot.running) return;
    bot.running = true;
    bot.startedAt = now();
    stopper = new AbortController();
    const mine = ++generation;
    const alive = () => bot.running && mine === generation;

    (async () => {
      let backoff = 2000;
      let prepared = '';
      while (alive()) {
        try {
          if (!(await active())) { await sleep(10_000); continue; }
          const tok = await token();
          if (prepared !== tok) {
            //  webhookِ جامانده نمی‌گذارد getUpdates کار کند (۴۰۹)
            await call('deleteWebhook', { drop_pending_updates: false });
            const me = await refreshMe();
            if (!me.ok) throw new Error(me.description || 'رمزِ بات پذیرفته نشد');
            await setCommands().catch(() => {});
            prepared = tok;
          }
          if (!alive()) break;
          await pollOnce();
          backoff = 2000;
        } catch (err) {
          if (!alive()) break;
          bot.lastError = hide(err.message);
          await sleep(backoff);
          backoff = Math.min(backoff * 2, 60_000);
        }
      }
    })();

    outboxTimer = setInterval(() => {
      flushOutbox().catch(() => {});
      //  اعلامیهٔ صبح — دقیقه‌ای یک بار سنجیده می‌شود، روزی یک بار می‌رود
      if (now() - lastAnnounceTry >= 60_000) { lastAnnounceTry = now(); announceTick().catch(() => {}); }
      //  🖥 قطع/وصلِ برنامه — پنج دقیقه‌ای؛ ⏳ اشتراک — ساعتی
      if (now() - lastPcWatch >= 5 * 60_000) { lastPcWatch = now(); pcWatchTick().catch(() => {}); }
      if (now() - lastSubTick >= 60 * 60_000) { lastSubTick = now(); subTick().catch(() => {}); }
    }, 5000);
    outboxTimer.unref?.();
  }

  function stop() {
    bot.running = false;
    generation++;
    stopper.abort();
    if (outboxTimer) clearInterval(outboxTimer);
    outboxTimer = null;
  }

  /** رمز عوض شد: دورِ جاری بریده می‌شود تا رمزِ تازه همان لحظه بنشیند. */
  function reload() {
    tokenCache = null;
    bot.username = '';
    if (bot.running) { stop(); start(); }
  }

  /* ══════════════════════════════════════════════════════════════════
     پنلِ مدیر و درِ عمومی
     ══════════════════════════════════════════════════════════════════ */

  /** حالِ بات برای پنل. ⛔ رمز هرگز — فقط این‌که هست و چهار نویسهٔ آخرش. */
  async function status() {
    const tok = await token();
    const [chats, outbox] = await Promise.all([
      one(`SELECT
             COUNT(*) FILTER (WHERE kind='private' AND linked_at IS NOT NULL)::int AS private,
             COUNT(*) FILTER (WHERE kind='group' AND linked_at IS NOT NULL)::int AS groups,
             COUNT(*) FILTER (WHERE kind='channel' AND linked_at IS NOT NULL)::int AS channels
           FROM telegram_chats`),
      one(`SELECT
             COUNT(*) FILTER (WHERE sent_at IS NULL AND failed_at IS NULL)::int AS pending,
             COUNT(*) FILTER (WHERE sent_at IS NOT NULL AND sent_at > $1)::int AS sent,
             COUNT(*) FILTER (WHERE failed_at IS NOT NULL AND failed_at > $1)::int AS failed
           FROM telegram_outbox`, [now() - 24 * 3600 * 1000]),
    ]);
    const name = await username();
    return {
      configured: Boolean(tok),
      fromEnv: Boolean(config.telegram.token),
      tokenHint: tok ? `…${tok.slice(-4)}` : '',
      enabled: await enabled(),
      running: bot.running,
      username: name,
      link: name ? `https://t.me/${name}` : '',
      lastError: bot.lastError,
      lastOkAt: bot.lastOkAt,
      chats: { private: chats?.private || 0, groups: chats?.groups || 0, channels: chats?.channels || 0 },
      outbox: { pending: outbox?.pending || 0, sent24h: outbox?.sent || 0, failed24h: outbox?.failed || 0 },
      links: appLinks(),
    };
  }

  /**
   * ذخیرهٔ تنظیم از پنل.
   *
   * رمزِ تازه اول از خودِ تلگرام پرسیده می‌شود: رمزِ غلط ذخیره نمی‌شود. اگر
   * تلگرام همان لحظه در دسترس نبود، ذخیره می‌شود و بعداً امتحان می‌شود —
   * و پاسخ همین را می‌گوید.
   */
  async function configure({ token: raw, clearToken = false, enabled: on } = {}) {
    const out = { checked: false, warning: '' };
    if (clearToken) {
      await plans.setConfig(CFG_TOKEN, '');
      await plans.setConfig(CFG_USERNAME, '');
    } else if (typeof raw === 'string' && raw.trim()) {
      const tok = raw.trim();
      if (!/^\d{5,}:[A-Za-z0-9_-]{30,}$/.test(tok)) {
        const e = new Error('رمزِ بات شکلِ درستی ندارد (از BotFather: «123456:ABC…»)');
        e.code = 'bad_token';
        throw e;
      }
      const me = await call('getMe', {}, { tok });
      if (me.ok) {
        out.checked = true;
        await plans.setConfig(CFG_USERNAME, String(me.result?.username || ''));
      } else if (me.error_code === 401 || me.error_code === 404) {
        const e = new Error('تلگرام این رمز را نپذیرفت');
        e.code = 'token_rejected';
        throw e;
      } else {
        out.warning = 'تلگرام همین حالا در دسترس نبود؛ رمز ذخیره شد و سرور خودش دوباره امتحان می‌کند.';
      }
      await plans.setConfig(CFG_TOKEN, await stations.encryptKey(tok));
      await plans.setConfig(CFG_OFFSET, '0');
    }
    if (typeof on === 'boolean') await plans.setConfig(CFG_ENABLED, on ? '1' : '0');
    reload();
    return out;
  }

  /** برای برنامهٔ پمپ و اپِ کارمندان: بات هست؟ نامش چیست؟ ⛔ هیچ رازی. */
  async function publicInfo() {
    const on = await active();
    const name = on ? await username() : '';
    return { enabled: Boolean(on && name), username: name, url: name ? `https://t.me/${name}` : '' };
  }

  /** فقط آزمون: همه‌چیزِ حافظه از نو. */
  function _reset() {
    seenUpdates.clear();
    adminCache.clear();
    tokenCache = null;
    bot.username = '';
    bot.lastError = '';
    bot.lastOkAt = 0;
  }

  return {
    PURPOSE, ALERT_KINDS,
    relayCustomer, rateDone, desktopOnline, DESKTOP_ONLINE_MS,
    setTransport, handleUpdate, pollOnce, flushOutbox, notifyStation,
    start, stop, reload, status, configure, publicInfo, appLinks,
    formatAlert, announcementText, announceTick, pcWatchTick, subTick, alertKeyboard, CALM_CAP, setQuietHours, setAutoTankImage, shortAlert, parseCommand, asciiDigits, normEmail, normName, notifyResolved, searchText, chunks,
    _reset,
  };
};
