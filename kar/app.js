/* ══ دستیارِ پمپ یعقوبی — اپِ کارمندان و ربات ═════════════════════════════════
 *
 * سه کار می‌کند و بس:
 *   ۱) به شاخهٔ زندهٔ ایستگاه روی سرورِ خانگی گوش می‌دهد،
 *   ۲) با رمزِ خودِ برنامهٔ کامپیوتر باز می‌شود،
 *   ۳) و موتورِ جست‌وجوی همان داده است — «ربات».
 *
 * ⚠️ هیچ‌وقت چیزی نمی‌نویسد. تنها ‎op‎هایی که می‌فرستد ‎sub‎ و ‎get‎ است.
 */
(function () {
  'use strict';

  // ══════════════════════════════════════════════════════════════════════
  //  ابزارِ کوچک
  // ══════════════════════════════════════════════════════════════════════

  var $ = function (id) { return document.getElementById(id); };

  function esc(s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  /** رقمِ فارسی/عربی ⇒ لاتین. بی این، «۱۴۰۵» با «1405» یکی شمرده نمی‌شود. */
  function toEn(s) {
    return String(s == null ? '' : s)
      .replace(/[۰-۹]/g, function (c) { return String.fromCharCode(c.charCodeAt(0) - 0x06F0 + 48); })
      .replace(/[٠-٩]/g, function (c) { return String.fromCharCode(c.charCodeAt(0) - 0x0660 + 48); });
  }

  /**
   * یکسان‌سازیِ نوشتهٔ فارسی/دری برای مقایسه.
   * ⚠️ «ی»ِ عربی و فارسی، «ک»ِ عربی و فارسی، و نیم‌فاصله سه دامِ همیشگیِ
   * جست‌وجو در این زبان‌اند: «علی» تایپ‌شده با کیبوردِ عربی با «علی»ِ داخلِ
   * دیتابیس برابر نیست مگر این‌که این‌جا یکی شوند.
   */
  function norm(s) {
    return toEn(s).toLowerCase()
      .replace(/[يى]/g, 'ی')      // ي ى ⇒ ی
      .replace(/ك/g, 'ک')              // ك ⇒ ک
      .replace(/[‌‏‎]/g, ' ')     // نیم‌فاصله و نشانه‌های جهت
      .replace(/[ً-ْٰ]/g, '')     // اعراب
      .replace(/[^\w؀-ۿ]+/g, ' ')
      .replace(/\s+/g, ' ').trim();
  }

  /** «۱٬۲۳۴٫۵» ⇒ 1234.5 — همان راهی که خودِ برنامه عدد را می‌خواند. */
  function num(s) {
    if (typeof s === 'number') return isFinite(s) ? s : 0;
    var t = toEn(s).replace(/[,٬،]/g, '').trim();
    var v = parseFloat(t);
    return isFinite(v) ? v : 0;
  }

  function fmt(v) {
    var n = typeof v === 'number' ? v : num(v);
    if (!isFinite(n)) return '0';
    var r = Math.round(n * 100) / 100;
    return r.toLocaleString('en-US', { maximumFractionDigits: 2 });
  }

  // ══════════════════════════════════════════════════════════════════════
  //  ماه‌ها — «ماهِ سنبله» و «ماه ۶» و «1405/06» هر سه یک چیزند
  // ══════════════════════════════════════════════════════════════════════

  var MONTHS = [
    ['حمل', 'فروردین'], ['ثور', 'اردیبهشت'], ['جوزا', 'خرداد'], ['سرطان', 'تیر'],
    ['اسد', 'مرداد'], ['سنبله', 'شهریور'], ['میزان', 'مهر'], ['عقرب', 'ابان', 'آبان'],
    ['قوس', 'اذر', 'آذر'], ['جدی', 'دی'], ['دلو', 'بهمن'], ['حوت', 'اسفند']
  ];

  function pad2(n) { return (n < 10 ? '0' : '') + n; }

  /**
   * ماهِ خواسته‌شده را از متنِ پرسش بیرون می‌کشد.
   * خروجی: «1405/06» یا «/06» (یعنی هر سالی، همان ماه) یا «1405/» یا null.
   */
  function askedMonth(q, years) {
    var t = norm(q);
    // ⚠️ ‎norm‎ عمداً «/» را برمی‌دارد (برای مقایسهٔ نام‌ها خوب است)، پس
    // الگوی «1405/06» باید روی نوشتهٔ **نیم‌پخته** بخورد، وگرنه هیچ‌وقت
    // نمی‌گیرد — یک‌بار همین‌جا شکست.
    var raw = toEn(q);

    // «1405/06» یا «1405-06»
    var m = raw.match(/\b(1[34]\d{2})\s*[\/\-]\s*(\d{1,2})\b/);
    if (m) return m[1] + '/' + pad2(parseInt(m[2], 10));

    // نامِ ماه («ماهِ سنبله»، «سنبله»)
    var idx = -1;
    for (var i = 0; i < MONTHS.length && idx < 0; i++)
      for (var k = 0; k < MONTHS[i].length; k++)
        if (t.indexOf(norm(MONTHS[i][k])) >= 0) { idx = i; break; }

    // «ماه ۶» / «ماه 6»
    if (idx < 0) {
      var mm = t.match(/ماه\s*(\d{1,2})\b/);
      if (mm) {
        var v = parseInt(mm[1], 10);
        if (v >= 1 && v <= 12) idx = v - 1;
      }
    }

    var yr = t.match(/\b(1[34]\d{2})\b/);
    if (idx >= 0) return (yr ? yr[1] : '') + '/' + pad2(idx + 1);
    if (yr) {
      // «سالِ ۱۴۰۵» — همهٔ ماه‌های آن سال
      if (years && years.indexOf(yr[1]) < 0) return yr[1] + '/';
      return yr[1] + '/';
    }
    return null;
  }

  /** آیا ماهِ ردیف با چیزی که پرسیده شده می‌خواند؟ */
  function monthHit(rowMonth, want) {
    if (!want) return true;
    var rm = toEn(rowMonth || '');
    if (!rm) return false;
    if (want.charAt(0) === '/') return rm.slice(-3) === want;          // هر سال، همان ماه
    if (want.charAt(want.length - 1) === '/') return rm.indexOf(want) === 0; // یک سالِ کامل
    return rm === want;
  }

  function monthLabel(want) {
    if (!want) return 'همهٔ ماه‌ها';
    if (want.charAt(0) === '/') return 'ماهِ ' + (MONTHS[parseInt(want.slice(1), 10) - 1] || [''])[0] + ' (هر سال)';
    if (want.charAt(want.length - 1) === '/') return 'سالِ ' + want.slice(0, -1);
    var p = want.split('/');
    return (MONTHS[parseInt(p[1], 10) - 1] || [''])[0] + ' ' + p[0];
  }

  // ══════════════════════════════════════════════════════════════════════
  //  رمز — PBKDF2-SHA256، دقیقاً همان چیزی که برنامهٔ کامپیوتر می‌پزد
  // ══════════════════════════════════════════════════════════════════════

  function b64bytes(s) {
    var bin = atob(s.replace(/-/g, '+').replace(/_/g, '/'));
    var out = new Uint8Array(bin.length);
    for (var i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
    return out;
  }

  /**
   * ‎pbkdf2$sha256$<دور>$<نمک>$<هش>‎ — همان قالبِ ‎PasswordHasher‎ی برنامه.
   * ⚠️ رمز با UTF-8 کد می‌شود، چون دات‌نت هم همان کار را می‌کند.
   */
  async function verifyPassword(password, stored) {
    var p = String(stored || '').split('$');
    if (p.length !== 5 || p[0] !== 'pbkdf2' || p[1] !== 'sha256') return false;
    var iter = parseInt(p[2], 10);
    if (!(iter > 0)) return false;
    var salt = b64bytes(p[3]), expect = b64bytes(p[4]);
    var key = await crypto.subtle.importKey(
      'raw', new TextEncoder().encode(password), 'PBKDF2', false, ['deriveBits']);
    var bits = await crypto.subtle.deriveBits(
      { name: 'PBKDF2', salt: salt, iterations: iter, hash: 'SHA-256' }, key, expect.length * 8);
    var got = new Uint8Array(bits);
    if (got.length !== expect.length) return false;
    var diff = 0;
    for (var i = 0; i < got.length; i++) diff |= got[i] ^ expect[i];
    return diff === 0;
  }

  // ══════════════════════════════════════════════════════════════════════
  //  ربات — موتورِ جست‌وجوی همان دادهٔ برنامه
  // ══════════════════════════════════════════════════════════════════════
  //
  //  ⚠️ عمداً هیچ حسابِ تازه‌ای نمی‌کند. هر عددی که نشان می‌دهد همان عددی است
  //  که برنامهٔ کامپیوتر ساخته و فرستاده. جایی که جمع می‌زند (مثلاً «مصارفِ
  //  ماهِ فلان») همان ستونِ آمادهٔ همان بخش را جمع می‌کند و صریح می‌گوید
  //  کدام ستون را جمع زده.

  var SEC_WORDS = {
    safe: ['گاوصندوق', 'صندوق', 'گاو صندوق'],
    sarrafi: ['صرافی', 'دالر', 'تبادله'],
    expense: ['مصارف', 'مصرف', 'خرج', 'خرچ'],
    chakana: ['چکنه', 'خرده', 'چکنه فروشی'],
    extraincome: ['عایدات', 'عاید', 'درامد', 'درآمد'],
    company: ['شرکت', 'شرکت ها', 'شرکتها', 'تیل شرکت'],
    amanat: ['امانت'],
    invoice: ['فاکتور', 'فاکتورها', 'بل'],
    storage: ['خرید', 'خریدها', 'خرید تیل'],
    staff: ['کارمند', 'کارمندان', 'معاش', 'پرسونل']
  };

  var TANK_WORDS = ['مخزن', 'موجودی تیل', 'ذخیره', 'تانک', 'استاک'];
  var ALL_WORDS = ['همه بخش', 'همه بخشها', 'همه بخش ها', 'خلاصه', 'کل', 'گزارش کل', 'همه چیز'];
  var DEBT_WORDS = ['قرضدار', 'قرض دار', 'قرضداران', 'بدهکار', 'مقروض', 'قرض'];

  function anyWord(t, words) {
    for (var i = 0; i < words.length; i++) if (t.indexOf(norm(words[i])) >= 0) return true;
    return false;
  }

  /** ستونی که جمع زدنش معنی دارد — آخرین ستونِ عددیِ «مبلغ/مقدار». */
  function sumColumn(sec) {
    var head = sec.head || [];
    var prefer = ['مبلغ', 'مقدار', 'جمله افغانی', 'بردگی', 'الباقی'];
    for (var p = 0; p < prefer.length; p++)
      for (var i = 0; i < head.length; i++)
        if (norm(head[i]) === norm(prefer[p])) return i;
    // وگرنه: آخرین ستونی که در بیشترِ ردیف‌ها عدد است
    for (var c = head.length - 1; c >= 0; c--) {
      var hits = 0, seen = 0;
      for (var r = 0; r < (sec.rows || []).length && seen < 20; r++, seen++)
        if (/\d/.test(toEn((sec.rows[r] || [])[c] || ''))) hits++;
      if (seen > 0 && hits > seen / 2) return c;
    }
    return -1;
  }

  function sectionBlock(id, sec, want) {
    var rows = [], m = sec.m || [];
    for (var i = 0; i < (sec.rows || []).length; i++)
      if (monthHit(m[i], want)) rows.push(sec.rows[i]);

    var kv = [];
    if (!want) {
      // بی ماه، همان جمع‌های آمادهٔ خودِ برنامه — دست‌نخورده
      for (var s = 0; s < (sec.sum || []).length; s++)
        kv.push([sec.sum[s][0], sec.sum[s][1]]);
    } else {
      var c = sumColumn(sec), total = 0;
      for (var r = 0; r < rows.length; r++) total += num(rows[r][c]);
      kv.push(['شمارِ ردیف', fmt(rows.length)]);
      if (c >= 0) kv.push(['جمعِ ستونِ «' + (sec.head[c] || '') + '»', fmt(total)]);
    }
    return {
      title: sec.t + (want ? ' — ' + monthLabel(want) : ''),
      kv: kv,
      table: { head: sec.head || [], rows: rows.slice(-60) },
      note: rows.length > 60 ? 'فقط ۶۰ ردیفِ آخر نشان داده شد (از ' + fmt(rows.length) + ' ردیف).' : ''
    };
  }

  function tankBlock(d) {
    var t = (d && d.tank) || {}, kv = [];
    [['petrol', 'پطرول'], ['diesel', 'دیزل']].forEach(function (p) {
      var x = t[p[0]] || {};
      kv.push([p[1] + ' — موجودی', fmt(x.show) + ' لیتر' +
        (x.low ? ' ⛔ کم آمده' : (x.near ? ' ⚠️ نزدیکِ حدِ کمبود' : ''))]);
      kv.push([p[1] + ' — وارد / فروش', fmt(x['in']) + ' / ' + fmt(x.out) + ' لیتر']);
    });
    return { title: 'مخزن', kv: kv, table: null, note: '' };
  }

  function personBlock(p) {
    var kv = [
      ['حال', p.status === 'out' ? 'تمام شده — تیلِ اضافه ندهید'
        : p.status === 'low' ? 'کم مانده' : p.status === 'ok' ? 'موجودی دارد' : 'ردیفی ندارد'],
      ['الباقیِ پول', fmt(p.bal && p.bal.money) + ' افغانی'],
      ['الباقیِ پطرول', fmt(p.bal && p.bal.petrol) + ' لیتر'],
      ['الباقیِ دیزل', fmt(p.bal && p.bal.diesel) + ' لیتر']
    ];
    if (p.phone) kv.push(['تلفن', p.phone]);
    return {
      title: p.name + (p.noinv ? ' (بی‌فاکتور)' : ''), kv: kv,
      table: null, note: '', person: p
    };
  }

  /** نامِ کدام قرض‌داران در این جمله آمده؟ بلندترین برابری برنده است. */
  function findPeople(q, people) {
    var t = norm(q), hit = [];
    for (var i = 0; i < people.length; i++) {
      var n = norm(people[i].name);
      if (n.length >= 2 && t.indexOf(n) >= 0) hit.push(people[i]);
    }
    hit.sort(function (a, b) { return norm(b.name).length - norm(a.name).length; });
    return hit.slice(0, 5);
  }

  /** جست‌وجوی آزاد در همهٔ ردیف‌های همهٔ بخش‌ها — راهِ آخر. */
  function freeSearch(q, d) {
    var words = norm(q).split(' ').filter(function (w) { return w.length >= 2; });
    if (!words.length) return [];
    var out = [];
    var secs = d.sections || {};
    Object.keys(secs).forEach(function (id) {
      var sec = secs[id], rows = [];
      for (var i = 0; i < (sec.rows || []).length && rows.length < 25; i++) {
        var line = norm((sec.rows[i] || []).join(' '));
        var ok = true;
        for (var w = 0; w < words.length; w++) if (line.indexOf(words[w]) < 0) { ok = false; break; }
        if (ok) rows.push(sec.rows[i]);
      }
      if (rows.length) out.push({
        title: sec.t + ' — ' + fmt(rows.length) + ' ردیفِ هم‌خوان',
        kv: [], table: { head: sec.head || [], rows: rows }, note: ''
      });
    });
    return out;
  }

  /**
   * ══ خودِ ربات ══════════════════════════════════════════════════════════
   * پرسش ⟶ چند «بلوک» برای نشان دادن. هیچ‌وقت خالی برنمی‌گردد: اگر چیزی
   * پیدا نشد، خودش می‌گوید چه چیزهایی را بلد است.
   */
  function answer(q, d) {
    var t = norm(q);
    if (!d) return [{ title: 'هنوز داده‌ای نرسیده', kv: [], table: null, note: 'برنامهٔ کامپیوتر باید روشن باشد.' }];
    if (!t) return [{ title: 'چه بپرسم؟', kv: [], table: null, note: 'یک نام، یک بخش، یا یک ماه بنویسید.' }];

    var people = d.debtors || [], secs = d.sections || {};
    var want = askedMonth(q);
    var out = [];

    // ۱) «همه بخش‌ها» / «خلاصه»
    if (anyWord(t, ALL_WORDS)) {
      var kv = [];
      var low = people.filter(function (p) { return p.status === 'out' || p.status === 'low'; });
      kv.push(['شمارِ قرض‌دار', fmt(people.length)]);
      kv.push(['قرض‌دارِ تمام‌شده/کم‌مانده', fmt(low.length)]);
      var tk = d.tank || {};
      kv.push(['پطرولِ مخزن', fmt((tk.petrol || {}).show) + ' لیتر']);
      kv.push(['دیزلِ مخزن', fmt((tk.diesel || {}).show) + ' لیتر']);
      out.push({ title: 'خلاصهٔ ایستگاه' + (want ? ' — ' + monthLabel(want) : ''), kv: kv, table: null, note: '' });
      Object.keys(secs).forEach(function (id) {
        var b = sectionBlock(id, secs[id], want);
        b.table = null;                     // خلاصه یعنی خلاصه، نه صد جدول
        out.push(b);
      });
      return out;
    }

    // ۲) نامِ یک قرض‌دار
    var named = findPeople(q, people);
    for (var i = 0; i < named.length; i++) out.push(personBlock(named[i]));

    // ۳) مخزن
    if (anyWord(t, TANK_WORDS)) out.push(tankBlock(d));

    // ۴) بخش‌ها
    Object.keys(SEC_WORDS).forEach(function (id) {
      if (secs[id] && anyWord(t, SEC_WORDS[id])) out.push(sectionBlock(id, secs[id], want));
    });

    // ۵) «کی بدهکار است» / فهرستِ قرض‌داران
    if (!out.length && anyWord(t, DEBT_WORDS)) {
      var bad = people.filter(function (p) { return p.status === 'out' || p.status === 'low'; });
      out.push({
        title: 'قرض‌دارانی که موجودی ندارند یا کم دارند — ' + fmt(bad.length) + ' نفر',
        kv: [], note: '',
        table: {
          head: ['نام', 'حال', 'الباقی پول', 'الباقی پطرول', 'الباقی دیزل'],
          rows: bad.map(function (p) {
            return [p.name, p.status === 'out' ? 'تمام شده' : 'کم مانده',
              fmt(p.bal && p.bal.money), fmt(p.bal && p.bal.petrol), fmt(p.bal && p.bal.diesel)];
          })
        }
      });
    }

    // ۶) فقط یک ماه گفته شده و هیچ بخشی — همهٔ بخش‌ها در همان ماه
    if (!out.length && want) {
      out.push({ title: 'همهٔ بخش‌ها در ' + monthLabel(want), kv: [], table: null, note: '' });
      Object.keys(secs).forEach(function (id) {
        var b = sectionBlock(id, secs[id], want);
        if (b.table && b.table.rows.length) out.push(b);
      });
      if (out.length === 1) out[0].note = 'در این ماه ردیفی ثبت نشده.';
      return out;
    }

    // ۷) راهِ آخر: جست‌وجوی آزاد
    if (!out.length) out = freeSearch(q, d);

    if (!out.length) out.push({
      title: 'چیزی پیدا نشد',
      kv: [],
      table: null,
      note: 'می‌توانید بپرسید: نامِ یک قرض‌دار · «مخزن» · «مصارف ماه ۱۴۰۵/۰۶» · '
        + '«گاوصندوق» · «شرکت‌ها» · «فاکتورها» · «همه بخش‌ها».'
    });
    return out;
  }

  // بیرون گذاشتن برای آزمون در Node — در مرورگر بی‌اثر است
  if (typeof module !== 'undefined' && module.exports)
    module.exports = {
      answer: answer, askedMonth: askedMonth, monthHit: monthHit,
      norm: norm, num: num, verifyPassword: verifyPassword
    };

  if (typeof document === 'undefined') return;   // آزمونِ Node این‌جا می‌ایستد

  // ══════════════════════════════════════════════════════════════════════
  //  تنظیمات، وصل شدن، قفل
  // ══════════════════════════════════════════════════════════════════════

  var KEY = 'pumpKar.v1';
  var cfg = { srv: '', tok: '', stn: 'pump1' };
  var data = null, ws = null, retry = 0, unlocked = false, timer = 0;

  function load() {
    try {
      var raw = localStorage.getItem(KEY);
      if (raw) { var o = JSON.parse(raw); cfg.srv = o.srv || ''; cfg.tok = o.tok || ''; cfg.stn = o.stn || 'pump1'; }
    } catch (e) { }
    try {
      var cached = localStorage.getItem(KEY + '.snap');
      if (cached) data = JSON.parse(cached);      // تا بی‌اینترنت هم چیزی باشد
    } catch (e) { }
  }

  function save() { try { localStorage.setItem(KEY, JSON.stringify(cfg)); } catch (e) { } }

  /** نشانیِ داخلِ لینک/کیو‌آر برداشته و از نوارِ نشانی پاک می‌شود. */
  function readUrl() {
    var p = new URLSearchParams(location.search);
    var got = false;
    if (p.get('server')) { cfg.srv = p.get('server'); got = true; }
    if (p.get('token')) { cfg.tok = p.get('token'); got = true; }
    if (p.get('station')) { cfg.stn = p.get('station'); got = true; }
    if (got) {
      save();
      // ⚠️ رمز نباید در نوارِ نشانی بماند و در تاریخچهٔ مرورگر ثبت شود
      try { history.replaceState(null, '', location.pathname); } catch (e) { }
    }
  }

  function wsUrl() {
    var b = String(cfg.srv || '').trim().replace(/\/+$/, '');
    if (/^https:\/\//i.test(b)) b = 'wss://' + b.slice(8);
    else if (/^http:\/\//i.test(b)) b = 'ws://' + b.slice(7);
    else if (!/^wss?:\/\//i.test(b)) b = 'wss://' + b;
    return b + (cfg.tok ? '/?token=' + encodeURIComponent(cfg.tok) : '');
  }

  function live(on, text) {
    var dot = $('liveDot'), t = $('liveText');
    if (dot) dot.className = 'dot ' + (on ? 'on' : 'off');
    if (t) t.textContent = text;
    var st = $('lockState');
    if (st) st.textContent = text;
  }

  function connect() {
    if (!cfg.srv) return;
    try { if (ws) ws.close(); } catch (e) { }
    live(false, 'در حالِ وصل شدن…');
    try { ws = new WebSocket(wsUrl()); } catch (e) { schedule(); return; }

    ws.onopen = function () {
      retry = 0;
      ws.send(JSON.stringify({
        op: 'sub', subId: 'live', event: 'value',
        path: 'stations/' + String(cfg.stn || 'pump1').trim() + '-live'
      }));
    };
    ws.onmessage = function (ev) {
      var m;
      try { m = JSON.parse(ev.data); } catch (e) { return; }
      if (m.op === 'error') { live(false, 'رمزِ سرور پذیرفته نشد'); return; }
      if (m.op === 'event' && m.subId === 'live') {
        if (m.value && typeof m.value === 'object') {
          // ⚠️ عکسِ کهنه هرگز جای تازه را نگیرد
          if (!data || !data.seq || (m.value.seq || 0) >= data.seq) {
            data = m.value;
            try { localStorage.setItem(KEY + '.snap', JSON.stringify(data)); } catch (e) { }
            render();
          }
          live(true, 'زنده — تازه‌سازی ' + (data.at || ''));
        } else {
          live(true, 'وصل است، ولی برنامهٔ کامپیوتر هنوز چیزی نفرستاده');
        }
        gateReady();
      }
    };
    ws.onclose = function () { live(false, 'قطع شد — دوباره وصل می‌شوم'); schedule(); };
    ws.onerror = function () { try { ws.close(); } catch (e) { } };
  }

  function schedule() {
    clearTimeout(timer);
    retry = Math.min(retry + 1, 6);
    timer = setTimeout(connect, 1000 * retry);
  }

  // ── قفل ────────────────────────────────────────────────────────────────

  function gateReady() {
    var b = $('btnUnlock');
    if (b) b.disabled = !(data && data.gate);
    if (data && data.station && data.station.name) {
      $('lockTitle').textContent = data.station.name;
      $('stName').textContent = data.station.name;
    }
    if (data && !data.gate)
      $('lockNote').textContent = 'برنامهٔ کامپیوتر هنوز رمزی نساخته است.';
  }

  function show(which) {
    ['setupPane', 'lockPane', 'appPane'].forEach(function (id) {
      $(id).classList.toggle('hidden', id !== which);
    });
    $('nav').classList.toggle('hidden', which !== 'appPane');
  }

  async function unlock() {
    var pass = $('inPass').value;
    var err = $('lockErr');
    err.classList.add('hidden');
    if (!data || !data.gate) return;
    $('btnUnlock').disabled = true;
    try {
      var ok = await verifyPassword(pass, data.gate);
      if (!ok) {
        err.textContent = 'رمز درست نیست.';
        err.classList.remove('hidden');
        return;
      }
      unlocked = true;
      $('inPass').value = '';
      show('appPane');
      render();
    } catch (e) {
      err.textContent = 'رمز سنجیده نشد: ' + e;
      err.classList.remove('hidden');
    } finally { $('btnUnlock').disabled = false; }
  }

  // ══════════════════════════════════════════════════════════════════════
  //  نمایش
  // ══════════════════════════════════════════════════════════════════════

  function tableHtml(head, rows) {
    if (!rows || !rows.length) return '<div class="sub">ردیفی نیست.</div>';
    var h = '<div class="scroll"><table><thead><tr><th>#</th>';
    for (var c = 0; c < head.length; c++) h += '<th>' + esc(head[c]) + '</th>';
    h += '</tr></thead><tbody>';
    for (var r = 0; r < rows.length; r++) {
      h += '<tr><td>' + (r + 1) + '</td>';
      for (var k = 0; k < head.length; k++) h += '<td>' + esc((rows[r] || [])[k]) + '</td>';
      h += '</tr>';
    }
    return h + '</tbody></table></div>';
  }

  function blockHtml(b) {
    var h = '<div class="card ans"><h2>' + esc(b.title) + '</h2>';
    for (var i = 0; i < (b.kv || []).length; i++)
      h += '<div class="kv"><span class="sub">' + esc(b.kv[i][0]) + '</span><b>' + esc(b.kv[i][1]) + '</b></div>';
    if (b.person) h += personAccountsHtml(b.person);
    if (b.table) h += tableHtml(b.table.head, b.table.rows);
    if (b.note) h += '<div class="sub">' + esc(b.note) + '</div>';
    return h + '</div>';
  }

  /**
   * حساب‌های یک شخص.
   *
   * ⚠️ اگر دفترِ پمپ آن‌قدر بزرگ باشد که ردیف‌ها به گوشی نیامده باشند
   * (‎detail === false‎)، به‌جای یک جدولِ خالی صریح گفته می‌شود چرا — وگرنه
   * کارمند خیال می‌کند حساب خالی است.
   */
  function personAccountsHtml(p) {
    var h = '', detail = !data || data.detail !== false;
    (p.accounts || []).forEach(function (a) {
      h += '<div style="margin-top:10px"><h2>' +
        esc(a.title || 'حسابِ اصلی') + ' — ' + (a.unit === 'money' ? 'واحد پول' : 'واحد تیل') + '</h2>';
      h += '<div class="figs">';
      (a.sum || []).forEach(function (s) {
        h += '<div class="fig"><div class="l">' + esc(s[0]) + '</div><div class="v">' + esc(s[1]) + '</div></div>';
      });
      h += '</div>';
      h += detail
        ? tableHtml(a.head || [], (a.rows || []).slice(-40))
        : '<div class="sub">جمع‌ها درست‌اند، ولی ردیف‌های این حساب در این عکس '
          + 'نیامده‌اند — دفترِ پمپ بزرگ‌تر از آن است که همه‌اش به گوشی بیاید. '
          + 'ردیف‌ها را در خودِ برنامهٔ کامپیوتر ببینید.</div>';
      h += '</div>';
    });
    return h;
  }

  function renderTank() {
    var t = (data && data.tank) || {}, h = '';
    [['petrol', 'پطرول'], ['diesel', 'دیزل']].forEach(function (p) {
      var x = t[p[0]] || {};
      var cls = x.low ? ' bad' : (x.near ? ' warn' : '');
      h += '<div class="fig' + cls + '"><div class="l">' + p[1] + ' — موجودی</div>' +
        '<div class="v">' + fmt(x.show) + '</div><div class="l">لیتر' +
        (x.low ? ' · کم آمده' : (x.near ? ' · نزدیکِ حد' : '')) + '</div></div>';
      h += '<div class="fig"><div class="l">' + p[1] + ' — وارد</div><div class="v">' + fmt(x['in']) + '</div></div>';
      h += '<div class="fig"><div class="l">' + p[1] + ' — فروش</div><div class="v">' + fmt(x.out) + '</div></div>';
    });
    $('tankBox').innerHTML = h || '<div class="sub">داده‌ای نیست.</div>';

    var buys = ((data && data.sections) || {}).storage;
    $('buyBox').innerHTML = buys
      ? tableHtml(buys.head, (buys.rows || []).slice(-30))
      : '<div class="sub">داده‌ای نیست.</div>';
  }

  function renderDebtors() {
    var q = norm($('inFind').value), want = $('selSt').value;
    var people = (data && data.debtors) || [];
    var h = '';
    people.forEach(function (p) {
      if (want && p.status !== want) return;
      if (q && norm(p.name).indexOf(q) < 0) return;
      h += '<button class="p-card ' + esc(p.status) + '" data-pid="' + esc(p.id) + '">' +
        '<div class="n">' + esc(p.name) + '</div>' +
        '<div class="sub">پول ' + fmt(p.bal && p.bal.money) +
        ' · پطرول ' + fmt(p.bal && p.bal.petrol) +
        ' · دیزل ' + fmt(p.bal && p.bal.diesel) + '</div>' +
        '<span class="badge ' + esc(p.status) + '">' +
        (p.status === 'out' ? 'تمام شده' : p.status === 'low' ? 'کم مانده'
          : p.status === 'ok' ? 'موجودی دارد' : 'ردیف ندارد') + '</span></button>';
    });
    $('debtList').innerHTML = h || '<div class="sub">کسی پیدا نشد.</div>';
  }

  var secTab = '';

  function renderSections() {
    var secs = (data && data.sections) || {};
    var ids = Object.keys(secs);
    if (!ids.length) { $('secTabs').innerHTML = ''; $('secBox').innerHTML = '<div class="card sub">داده‌ای نیست.</div>'; return; }
    if (ids.indexOf(secTab) < 0) secTab = ids[0];

    $('secTabs').innerHTML = ids.map(function (id) {
      return '<button data-sec="' + esc(id) + '" aria-selected="' + (id === secTab) + '">' +
        esc(secs[id].t) + '</button>';
    }).join('');

    var sec = secs[secTab];
    var months = {}, list = [];
    (sec.m || []).forEach(function (m) { if (m && !months[m]) { months[m] = 1; list.push(m); } });
    list.sort().reverse();
    var sel = $('selMonth'), keep = sel.value;
    sel.innerHTML = '<option value="">همهٔ ماه‌ها</option>' +
      list.map(function (m) { return '<option value="' + esc(m) + '">' + esc(m) + '</option>'; }).join('');
    if (list.indexOf(keep) >= 0) sel.value = keep;

    $('secBox').innerHTML = blockHtml(sectionBlock(secTab, sec, sel.value || null));
  }

  function render() {
    if (!unlocked) return;
    if (data && data.station && data.station.name) $('stName').textContent = data.station.name;
    renderTank();
    renderDebtors();
    renderSections();
  }

  // ══════════════════════════════════════════════════════════════════════
  //  سیم‌کشیِ دکمه‌ها
  // ══════════════════════════════════════════════════════════════════════

  function ask() {
    var q = $('inAsk').value;
    var blocks = answer(q, data);
    $('botOut').innerHTML = blocks.map(blockHtml).join('');
  }

  /** جست‌وجوی صوتی — اگر مرورگر نداشته باشد، دکمه پنهان می‌شود. */
  function setupMic() {
    var SR = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SR) { $('btnMic').classList.add('hidden'); return; }
    var rec = new SR();
    rec.lang = 'fa-IR';
    rec.interimResults = false;
    rec.maxAlternatives = 1;
    var on = false;
    rec.onresult = function (e) {
      $('inAsk').value = e.results[0][0].transcript;
      ask();
    };
    rec.onend = function () { on = false; $('btnMic').textContent = '🎙️'; };
    rec.onerror = function () { on = false; $('btnMic').textContent = '🎙️'; };
    $('btnMic').addEventListener('click', function () {
      if (on) { try { rec.stop(); } catch (e) { } return; }
      try { rec.start(); on = true; $('btnMic').textContent = '⏹️'; } catch (e) { }
    });
  }

  function boot() {
    load();
    readUrl();

    $('btnSetup').addEventListener('click', function () {
      cfg.srv = $('inSrv').value.trim();
      cfg.tok = $('inTok').value.trim();
      cfg.stn = $('inStn').value.trim() || 'pump1';
      if (!cfg.srv) return;
      save();
      show('lockPane');
      connect();
    });

    $('btnForget').addEventListener('click', function () {
      $('inSrv').value = cfg.srv; $('inTok').value = cfg.tok; $('inStn').value = cfg.stn;
      show('setupPane');
    });

    $('btnUnlock').addEventListener('click', unlock);
    $('inPass').addEventListener('keydown', function (e) { if (e.key === 'Enter') unlock(); });
    $('btnLock').addEventListener('click', function () { unlocked = false; show('lockPane'); });

    $('btnAsk').addEventListener('click', ask);
    $('inAsk').addEventListener('keydown', function (e) { if (e.key === 'Enter') ask(); });
    $('inFind').addEventListener('input', renderDebtors);
    $('selSt').addEventListener('change', renderDebtors);
    $('selMonth').addEventListener('change', renderSections);

    $('secTabs').addEventListener('click', function (e) {
      var b = e.target.closest('button[data-sec]');
      if (!b) return;
      secTab = b.getAttribute('data-sec');
      $('selMonth').value = '';
      renderSections();
    });

    $('debtList').addEventListener('click', function (e) {
      var b = e.target.closest('button[data-pid]');
      if (!b) return;
      var id = b.getAttribute('data-pid');
      var p = ((data && data.debtors) || []).filter(function (x) { return String(x.id) === id; })[0];
      if (!p) return;
      $('botOut').innerHTML = blockHtml(personBlock(p));
      goPane('paneBot');
    });

    $('nav').addEventListener('click', function (e) {
      var b = e.target.closest('button[data-pane]');
      if (b) goPane(b.getAttribute('data-pane'));
    });

    setupMic();
    show(cfg.srv ? 'lockPane' : 'setupPane');
    gateReady();
    connect();

    if ('serviceWorker' in navigator)
      navigator.serviceWorker.register('./sw.js').catch(function () { });
  }

  function goPane(id) {
    ['paneBot', 'paneDebt', 'paneTank', 'paneSec'].forEach(function (p) {
      $(p).classList.toggle('hidden', p !== id);
    });
    Array.prototype.forEach.call($('nav').querySelectorAll('button'), function (b) {
      b.setAttribute('aria-selected', String(b.getAttribute('data-pane') === id));
    });
    window.scrollTo(0, 0);
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();
})();
