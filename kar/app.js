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
        if (wholeWord(t, norm(MONTHS[i][k]))) { idx = i; break; }

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
    staff: ['کارمند', 'کارمندان', 'معاش', 'پرسونل'],
    parcha: ['پارچه', 'پارچه ها', 'شیفت', 'فایده', 'مفاد'],
    waraq: ['ورق', 'ورق روزانه', 'کمبودی'],
    debtrasid: ['رسید قرضدار', 'رسید قرض دار', 'رسیدها', 'رسید'],
    parcharasid: ['رسید پارچه'],
    attendance: ['حاضری', 'حضور', 'غیاب']
  };

  var TANK_WORDS = ['مخزن', 'موجودی تیل', 'ذخیره', 'تانک', 'استاک'];
  var ALL_WORDS = ['همه بخش', 'همه بخشها', 'همه بخش ها', 'خلاصه', 'کل', 'گزارش کل', 'همه چیز'];
  var DEBT_WORDS = ['قرضدار', 'قرض دار', 'قرضداران', 'بدهکار', 'مقروض', 'قرض'];

  /**
   * ══ کدام خبر تازه است ═══════════════════════════════════════════════════
   *
   * ⚠️ این تابع عمداً **پاک** است (نه ‎localStorage‎ می‌خواند نه اعلان می‌سازد)
   * تا بشود مو‌به‌مو آزمونش کرد. قلبِ خبر دادن همین است: اگر اشتباه کند، یا
   * هر بیست ثانیه زنگ می‌زند یا هیچ‌وقت زنگ نمی‌زند.
   *
   * ‎told‎ کلیدهایی است که قبلاً گفته‌ایم ⇒ ‎{fresh, told}‎ی تازه.
   *
   * ⚠️ ‎told‎ی برگشتی فقط کلیدهای **همین** فهرست را دارد، نه جمعِ همه: حسابی
   * که تسویه شد و بعد دوباره خراب شد باید دوباره خبر بدهد.
   */
  function freshAlerts(list, told) {
    var fresh = [], now = {};
    var arr = Object.prototype.toString.call(list) === '[object Array]' ? list : [];
    told = told || {};
    for (var i = 0; i < arr.length; i++) {
      var a = arr[i];
      if (!a || !a.k) continue;
      now[a.k] = 1;
      if (!told[a.k]) fresh.push(a);
    }
    return { fresh: fresh, told: now };
  }

  /**
   * ⛔ واژه باید **واژه** باشد، نه تکه‌ای از واژهٔ دیگر (۱۴۰۵/۰۷/۱۶): با سنجشِ
   * زیررشته‌ای «دی» داخلِ «دیزل» ماهِ جدی بود، «مهر» داخلِ «مهرداد» ماهِ میزان،
   * و «کل» داخلِ «کلیم» کلِ خلاصهٔ ایستگاه را به جای حسابِ کلیم می‌داد.
   *   ‎wholeWord‎ — دقیقاً همان واژه (نامِ ماه، و واژه‌های دوحرفی).
   *   ‎anyWord‎ — واژهٔ سه‌حرفی به بالا از **سرِ** یک واژه («پارچه‌ها»، «کارمندان»).
   */
  function wholeWord(t, w) {
    return !!w && (' ' + t + ' ').indexOf(' ' + w + ' ') >= 0;
  }

  function anyWord(t, words) {
    for (var i = 0; i < words.length; i++) {
      var w = norm(words[i]);
      if (!w) continue;
      if (w.length <= 2 ? wholeWord(t, w) : (' ' + t).indexOf(' ' + w) >= 0) return true;
    }
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
      table: null, note: '', person: p, who: p
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
  // ══════════════════════════════════════════════════════════════════════
  //  دو در به سرور — و چرا هر دو
  // ══════════════════════════════════════════════════════════════════════
  //
  //  ۱) «پوشه»  ‎/station?station=<کد>&token=<رمز>‎ و مسیرِ ‎live‎
  //     سرورِ به‌روز برای هر پمپ بنزین پوشه و رمزِ جدا دارد. رمزی که در
  //     کیو‌آرِ کارمند می‌نشیند فقط‌خواندنی است، پس حتی اگر کاغذِ کیو‌آر گم
  //     شود کسی نمی‌تواند چیزی را عوض کند.
  //
  //  ۲) «قدیمی» ‎/?token=<رمز>‎ و مسیرِ ‎stations/<کد>-live‎
  //     ⚠️ این را برنداریم: سرورِ خانگی‌ای که هنوز به‌روز نشده فقط همین را
  //     بلد است. روزی که کاربر اپ را تازه کند ولی سرور را نه، بی این،
  //     همان روز صفحهٔ خالی می‌دید.
  //
  //  اول درِ تازه، و اگر نشد درِ قدیمی — تصمیمش در ‎connect()‎ است.

  /** نشانیِ پایه، هر شکلی که نوشته شده باشد ⇒ ‎ws‎/‎wss‎ی درست. */
  function wsBaseOf(server) {
    var b = String(server || '').trim().replace(/\/+$/, '');
    if (/^https:\/\//i.test(b)) b = 'wss://' + b.slice(8);
    else if (/^http:\/\//i.test(b)) b = 'ws://' + b.slice(7);
    else if (!/^wss?:\/\//i.test(b)) b = 'wss://' + b;
    return b;
  }

  /** هر دو در، به ترتیبِ امتحان. ‎{name, url, path}‎ — رشته، نه تابع. */
  function doorsFor(c) {
    var base = wsBaseOf(c && c.srv);
    //  ⛔ هیچ کدِ پیش‌فرضِ مشترکی نیست (۱۴۰۵/۰۷/۱۳، «pump1 را حذف کن») — کد همیشه
    //  همان است که سرورِ حساب برای همان پمپ داده، یا کاربر در صفحهٔ دستی نوشته.
    var code = String((c && c.stn) || '').trim();
    var tok = String((c && c.tok) || '');
    return [
      {
        name: 'station',
        url: base + '/station?station=' + encodeURIComponent(code) + '&token=' + encodeURIComponent(tok),
        path: 'live'
      },
      {
        name: 'legacy',
        url: base + (tok ? '/?token=' + encodeURIComponent(tok) : ''),
        path: 'stations/' + code + '-live'
      }
    ];
  }

  /*
   *  ══ پوشِ خبرها — تنها راهِ خبر گرفتنِ آیفونِ بسته (۱۴۰۵/۰۷/۱۳) ══════
   *
   *  روی آیفون هیچ کارِ پس‌زمینه‌ای جز پوش نیست، و تا امروز این اپ هیچ جا
   *  برای پوش ثبت نمی‌شد و سرویس‌ورکرش رویدادِ ‎push‎ نداشت — یعنی اپِ بسته
   *  هیچ‌وقت هیچ خبری نمی‌گرفت. حالا گوشی با **رمزِ خواندنِ همان پمپ** در
   *  سرورِ خانگی ثبت می‌شود و خودِ سرور، با هر خبرِ تازه در عکسِ زنده،
   *  پوش می‌فرستد (‎stations/alert-push.js‎ در ریپوی ‎server‎).
   *
   *  ⚠️ دو نشانی به ترتیب: نشانیِ خانگیِ همین پمپ، و بعد همان سرورِ خانگی از
   *  راهِ تونل — نشانیِ قفل‌شدهٔ ‎cloud.js‎. گوشی‌ای که بیرون از شبکهٔ پمپ است
   *  هم ثبت می‌شود؛ و بعدِ ثبت، رسیدنِ پوش دیگر به شبکهٔ گوشی بند نیست.
   *  ⛔ نشانی از تنظیمات یا نوارِ نشانی خوانده نمی‌شود.
   */
  var TUNNEL = 'https://api.vill3n.top';

  function httpBaseOf(server) {
    var b = String(server || '').trim().replace(/\/+$/, '');
    if (!b) return '';
    if (/^wss:\/\//i.test(b)) return 'https://' + b.slice(6);
    if (/^ws:\/\//i.test(b)) return 'http://' + b.slice(5);
    if (!/^https?:\/\//i.test(b)) return 'https://' + b;
    return b;
  }

  /** نشانی‌هایی که ثبتِ پوش امتحان می‌کند — تکراری نه. */
  function pushBases(c) {
    var out = [];
    var home = httpBaseOf(c && c.srv);
    if (home) out.push(home);
    if (out.indexOf(TUNNEL) < 0) out.push(TUNNEL);
    return out;
  }

  /** ‎base64url‎ ⇒ بایت — همان شکلی که ‎applicationServerKey‎ می‌خواهد. */
  function b64uBytes(s) {
    var b = String(s || '').replace(/-/g, '+').replace(/_/g, '/');
    while (b.length % 4) b += '=';
    var raw = (typeof atob === 'function') ? atob(b) : Buffer.from(b, 'base64').toString('binary');
    var out = new Uint8Array(raw.length);
    for (var i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i);
    return out;
  }

  if (typeof module !== 'undefined' && module.exports)
    module.exports = {
      answer: answer, askedMonth: askedMonth, monthHit: monthHit, anyWord: anyWord,
      norm: norm, num: num, verifyPassword: verifyPassword,
      wsBaseOf: wsBaseOf, doorsFor: doorsFor, freshAlerts: freshAlerts,
      httpBaseOf: httpBaseOf, pushBases: pushBases, b64uBytes: b64uBytes, TUNNEL: TUNNEL
    };

  if (typeof document === 'undefined') return;   // آزمونِ Node این‌جا می‌ایستد

  // ══════════════════════════════════════════════════════════════════════
  //  تنظیمات، وصل شدن، قفل
  // ══════════════════════════════════════════════════════════════════════

  var KEY = 'pumpKar.v1';
  //  ‎code‎ کدِ هشت‌حرفیِ پمپ است (درِ اپ)؛ ‎name‎ نامش برای نمایش پیش از
  //  اولین عکس. بقیه همان سه چیزِ همیشگیِ سرورِ خانگی.
  var cfg = { srv: '', tok: '', stn: '', code: '', name: '' };
  var data = null, ws = null, retry = 0, unlocked = false, timer = 0;

  /*
   *  ⚠️ جداسازیِ پمپ‌ها — «با پمپ‌های دیگه قاطی نشه، اینو خیلی جدی بگیر»
   *
   *  هر چیزی که از یک پمپ در گوشی می‌ماند (آخرین عکس، خبرهایی که گفته
   *  شده) زیرِ کلیدِ **همان پمپ** می‌نشیند: ‎pumpKar.v1.snap.<کد>‎. پس
   *  کارمندی که با کدِ پمپِ دیگری وارد شود، حتی یک لحظه هم عکسِ پمپِ
   *  قبلی را نمی‌بیند، و عکسِ پمپِ قبلی پاک می‌شود.
   */
  function stnKey(suffix) { return KEY + '.' + suffix + '.' + (cfg.stn || ''); }

  function load() {
    try {
      var raw = localStorage.getItem(KEY);
      if (raw) {
        var o = JSON.parse(raw);
        cfg.srv = o.srv || ''; cfg.tok = o.tok || ''; cfg.stn = o.stn || '';
        cfg.code = o.code || ''; cfg.name = o.name || '';
      }
    } catch (e) { }
    try {
      var cached = localStorage.getItem(stnKey('snap'));
      if (cached) data = JSON.parse(cached);      // تا بی‌اینترنت هم چیزی باشد
    } catch (e) { }
    try { toldKeys = JSON.parse(localStorage.getItem(stnKey('told')) || '{}') || {}; } catch (e) { toldKeys = {}; }
  }

  function save() { try { localStorage.setItem(KEY, JSON.stringify(cfg)); } catch (e) { } }

  /** رفتن به پمپِ دیگر: هر چه از پمپِ قبلی در گوشی مانده پاک می‌شود. */
  function switchStation(stn) {
    var next = String(stn || '');
    if (cfg.stn === next && data) return;
    //  ⛔ خبرِ پمپِ قبلی نباید به این گوشی برسد: اشتراکِ پوش **باطل** می‌شود
    //  (نشانی‌اش برای همیشه می‌میرد، پس سرورِ پمپِ قبلی دیگر راهی ندارد) و
    //  برای پمپِ تازه از نو ساخته می‌شود.
    if (cfg.stn && cfg.stn !== next) dropPush(cfg);
    try {
      if (cfg.stn) {
        localStorage.removeItem(stnKey('snap'));
        localStorage.removeItem(stnKey('told'));
        localStorage.removeItem(stnKey('mode'));
        localStorage.removeItem(stnKey('push'));
        localStorage.removeItem(stnKey('ok'));
      }
      localStorage.removeItem(KEY + '.snap');      // کلیدِ قدیمیِ بی‌پمپ
      localStorage.removeItem(KEY + '.told');
    } catch (e) { }
    cfg.stn = next;
    ownerPassed = '';
    data = null;
    toldKeys = {};
    unlocked = false;
    pendingOwner = false;
    fromCloud = false;
    mode = '';
  }

  /** فقط عکسِ همین پمپ پذیرفته می‌شود — چه از سرورِ خانگی چه از ابر. */
  function acceptSnapshot(v, viaCloud) {
    if (!v || typeof v !== 'object') return false;
    // ⛔ شماره‌ای که از آینده آمده (ساعتِ کامپیوترِ پمپ آن لحظه جلو بود) قفل
    // نیست — وگرنه هر عکسِ درستِ بعدی تا رسیدنِ همان آینده رد می‌شد و گوشی دیگر
    // به‌روز نمی‌شد (۱۴۰۵/۰۷/۱۵). ساعتِ گوشی خودش از شبکه می‌آید.
    var future = !!(data && data.seq && data.seq > Date.now() + 600000);
    // ⚠️ عکسِ کهنه هرگز جای تازه را نگیرد
    if (!future && data && data.seq && (v.seq || 0) < data.seq) return false;
    // ⚠️ عکسِ سرورِ حساب با همان شماره جای عکسِ زندهٔ سوکت را نمی‌گیرد
    if (!future && viaCloud && data && !fromCloud && (v.seq || 0) <= (data.seq || 0)) return false;
    data = v;
    fromCloud = !!viaCloud;
    try { localStorage.setItem(stnKey('snap'), JSON.stringify(data)); } catch (e) { }
    render();
    return true;
  }

  var fromCloud = false;

  /*
   *  ══ دو در: «حساب‌های پمپ» و «کارمندان» ═══════════════════════════════
   *
   *  خواستهٔ صریحِ صاحب ریپو: «وقتی اپ باز می‌شود دو بخش باشد: یکی برای
   *  دیدنِ حساب‌های پمپ، یکی برای کارمندان تا افرادی که تیل دارند و ندارند
   *  را با مخزن ببینند.»
   *
   *  ‎mode‎ زیرِ کلیدِ همان پمپ می‌ماند (‎stnKey('mode')‎) تا با کدِ پمپِ
   *  دیگر، درِ قبلی به یاد نماند. هر دو در فقط می‌خوانند.
   */
  var mode = '';
  var NAVS = {
    owner: [
      ['paneDash', '🏠 خانه'], ['paneSec', '📚 بخش‌ها'],
      ['paneDebt', '👥 قرض‌داران'], ['paneTank', '⛽ مخزن'], ['paneBot', '🤖 ربات']
    ],
    staff: [
      ['paneStaff', '🚦 تیل دارد؟'], ['paneTank', '⛽ مخزن'], ['paneBot', '🤖 ربات']
    ]
  };
  var ALERT_PANES = ['paneDash', 'paneStaff', 'paneTank'];
  var curPane = '';
  var ALL_PANES = ['paneHome', 'paneDash', 'paneStaff', 'paneBot', 'paneDebt', 'paneTank', 'paneSec'];

  function loadMode() {
    try { mode = localStorage.getItem(stnKey('mode')) || ''; } catch (e) { mode = ''; }
    if (mode !== 'owner' && mode !== 'staff') mode = '';
  }

  function setMode(m) {
    //  ⛔ «📒 حساب‌ها» فقط با رمزِ برنامه (یا پمپِ بی‌رمز) — کارمندان آزاد
    if (m === 'owner' && !ownerOk()) { askOwner(); return; }
    mode = m === 'staff' ? 'staff' : (m === 'owner' ? 'owner' : '');
    try {
      if (mode) localStorage.setItem(stnKey('mode'), mode);
      else localStorage.removeItem(stnKey('mode'));
    } catch (e) { }
    buildNav();
    var seg = $('modeSeg');
    if (seg) {
      seg.classList.toggle('hidden', !mode);
      Array.prototype.forEach.call(seg.querySelectorAll('button'), function (b) {
        b.setAttribute('aria-selected', String(b.getAttribute('data-mode') === mode));
      });
    }
    lockButton();
    if (!mode) { goPane('paneHome'); return; }
    goPane(NAVS[mode][0][0]);
    render();
  }

  function buildNav() {
    var nav = $('nav');
    if (!nav) return;
    nav.innerHTML = (NAVS[mode] || []).map(function (n) {
      //  «🏠 خانه» ⇒ نشانه بالا، نام پایین (فقط ظاهر)
      var sp = n[1].indexOf(' ');
      return '<button data-pane="' + n[0] + '"><span class="i">' + n[1].slice(0, sp) + '</span>' +
        '<span>' + n[1].slice(sp + 1) + '</span></button>';
    }).join('');
    nav.classList.toggle('hidden', !mode || !unlocked);
  }

  /** نشانیِ داخلِ لینک/کیو‌آر برداشته و از نوارِ نشانی پاک می‌شود. */
  var pendingCode = '';

  function readUrl() {
    var p = new URLSearchParams(location.search);
    var got = false;
    //  لینک/کیو‌آرِ «با کدِ پمپ» (‎?code=‎): همان کد را خودش می‌زند
    if (p.get('code')) {
      pendingCode = p.get('code');
      try { history.replaceState(null, '', location.pathname); } catch (e) { }
    }
    if (p.get('server')) { cfg.srv = p.get('server'); got = true; }
    if (p.get('token')) { cfg.tok = p.get('token'); got = true; }
    if (p.get('station')) { switchStation(p.get('station')); got = true; }
    if (got) {
      cfg.code = '';          // کیو‌آر راهِ خودش را دارد؛ کدِ قبلی مالِ این پمپ نیست
      save();
      // ⚠️ رمز نباید در نوارِ نشانی بماند و در تاریخچهٔ مرورگر ثبت شود
      try { history.replaceState(null, '', location.pathname); } catch (e) { }
    }
  }

  /*
   *  ══ وصل شدنِ واقعی — سه در، مهلتِ هر در، و عکسِ سرورِ حساب همان لحظه ══
   *
   *  گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۴): «اندروید و آیفون هیچ‌کدام کار نمی‌کنن…
   *  یا می‌گه وصل است و جواب نمی‌ده، یا وصل است و اطلاعاتِ همون حساب رو نشون
   *  نمی‌ده.» با سرورهای واقعی بازسازی شد (‎tools/check-kar-real.mjs‎):
   *
   *   ۱) صفحهٔ ‎https‎ (آیفون، سافاری) حق ندارد به ‎ws://192.168…‎ وصل شود
   *      (Mixed Content) — و کرومیوم/وب‌کیت نه خطا می‌دهند نه ‎close‎: سوکت
   *      تا ابد «در حالِ وصل شدن» می‌ماند.
   *   ۲) گوشی روی دیتای خودش (بیرون از وای‌فایِ پمپ) به نشانیِ شبکهٔ پمپ
   *      نمی‌رسد و همان سوکت بی‌پایان آویزان می‌ماند.
   *   ۳) عکسِ سرورِ حساب فقط «پس از دو بار نشدن» پرسیده می‌شد — و چون سوکت
   *      هیچ‌وقت نشد نمی‌گفت، هیچ‌وقت.
   *
   *  پس: ⛔ هر در مهلت دارد (‎OpenWait‎)؛ ⛔ درِ ‎ws://‎ روی صفحهٔ ‎https‎ اصلاً
   *  امتحان نمی‌شود؛ ⛔ درِ سوم همان سرورِ خانگی است از راهِ تونلِ قفل‌شده
   *  (‎TUNNEL‎ — ‎wss://…/station‎، همان مسیر، همان رمزِ خواندن)؛ و ⛔ عکسِ
   *  سرورِ حساب همان لحظهٔ باز شدن پرسیده می‌شود، هم‌زمان با سوکت.
   */
  var door = 0, gen = 0, openTimer = 0, wsLive = false;
  var OpenWait = 5000;

  /** نشانیِ ‎ws://‎ از صفحهٔ ‎https‎ = Mixed Content (مگر خودِ همین دستگاه). */
  function blockedDoor(url) {
    try {
      if (location.protocol !== 'https:') return false;
      if (!/^ws:\/\//i.test(url)) return false;
      var h = url.replace(/^ws:\/\//i, '').split(/[/:?#]/)[0];
      return h !== 'localhost' && h !== '127.0.0.1';
    } catch (e) { return false; }
  }

  /** همهٔ درهای امروز، به ترتیب: شبکهٔ پمپ (دو در) و بعد تونل. */
  function doors() {
    var out = [];
    if (cfg.srv) doorsFor(cfg).forEach(function (d) { if (!blockedDoor(d.url)) out.push(d); });
    //  درِ تونل فقط با کدِ پمپ و رمزِ خواندن معنا دارد، و فقط درِ تازه
    //  (‎/station‎) — پورتِ عمومیِ سرورِ خانگی درِ قدیمی را باز نمی‌کند.
    if (cfg.stn && cfg.tok) {
      var t = doorsFor({ srv: TUNNEL, stn: cfg.stn, tok: cfg.tok })[0];
      if (!out.some(function (d) { return d.url === t.url; })) out.push({ name: 'tunnel', url: t.url, path: t.path });
    }
    return out;
  }

  /** نشانیِ همان دری که الان امتحان می‌شود. */
  function wsUrl() { var d = doors(); return d.length ? d[door % d.length].url : ''; }
  function livePath() { var d = doors(); return d.length ? d[door % d.length].path : 'live'; }

  function live(on, text) {
    var dot = $('liveDot'), t = $('liveText');
    if (dot) dot.className = 'dot ' + (on ? 'on' : 'off');
    if (t) t.textContent = text;
    var st = $('lockState');
    if (st) st.textContent = text;
  }

  /** حالی که روی صفحه نوشته می‌شود وقتی سوکت هنوز چیزی نداده — راست، نه امید. */
  function waitingText() {
    if (data && fromCloud) return cloudText();
    if (data) return 'آخرین عکسِ ذخیره‌شده — در حالِ وصل شدن…';
    if (cloudNone) return 'برنامهٔ کامپیوترِ پمپ هنوز هیچ عکسی نفرستاده — روشن و وصل است؟';
    return 'در حالِ وصل شدن…';
  }

  function connect() {
    var list = doors();
    clearTimeout(openTimer);
    try { if (ws) ws.close(); } catch (e) { }
    ws = null;
    if (!list.length) { live(false, waitingText()); cloudPoll(true); return; }
    var my = ++gen, opened = false, d = list[door % list.length];
    if (!wsLive) { live(false, waitingText()); cloudPoll(true); }
    var sock;
    try { sock = new WebSocket(d.url); } catch (e) { nextDoor(my); return; }
    ws = sock;
    //  ⛔ سوکتی که در مهلت جواب نداد رها می‌شود — مرورگر خودش هیچ‌وقت نمی‌گوید.
    openTimer = setTimeout(function () { if (my === gen && !opened) nextDoor(my); }, OpenWait);

    sock.onopen = function () {
      if (my !== gen) return;
      sock.send(JSON.stringify({ op: 'sub', subId: 'live', event: 'value', path: d.path }));
    };
    sock.onmessage = function (ev) {
      if (my !== gen) return;
      var m;
      try { m = JSON.parse(ev.data); } catch (e) { return; }
      if (m.op === 'connected') {
        // این در جواب داد — تا وقتی کار می‌کند همین بماند
        opened = true;
        clearTimeout(openTimer);
        retry = 0;
        return;
      }
      if (m.op === 'error') {
        // ⚠️ شاید فقط این در نبود، نه این‌که رمز غلط باشد: سرورِ قدیمی
        // ‎/station‎ ندارد و سرورِ تازه پمپِ ناشناس را نمی‌شناسد. درِ بعدی
        // را امتحان کن و تنها وقتی «رمز غلط» بگو که همه رد کرده باشند.
        rejected++;
        nextDoor(my);
        return;
      }
      if (m.op === 'event' && m.subId === 'live') {
        opened = true;
        clearTimeout(openTimer);
        rejected = 0;
        if (m.value && typeof m.value === 'object') {
          acceptSnapshot(m.value, false);
          wsLive = true;
          cloudPoll(false);
          live(true, (d.name === 'tunnel' ? 'زنده از راهِ اینترنت' : 'زنده') + ' — تازه‌سازی ' + ((data && data.at) || ''));
        } else {
          //  در باز است ولی برنامهٔ کامپیوتر این‌جا چیزی ننوشته ⇒ همان
          //  عکسِ سرورِ حساب، و راستش را بگو.
          wsLive = false;
          cloudPoll(true);
          live(!!data, data ? waitingText() : 'وصل است، ولی برنامهٔ کامپیوتر هنوز چیزی نفرستاده');
        }
        gateReady();
      }
    };
    sock.onclose = function () {
      if (my !== gen) return;
      clearTimeout(openTimer);
      if (!opened) { nextDoor(my); return; }
      wsLive = false;
      cloudPoll(true);
      live(false, data ? waitingText() : 'قطع شد — دوباره وصل می‌شوم');
      schedule();
    };
    sock.onerror = function () { try { sock.close(); } catch (e) { } };
  }

  var rejected = 0;

  /** این در نشد ⇒ درِ بعدی همین حالا؛ همه را که گشتیم ⇒ با مکث از نو. */
  function nextDoor(my) {
    if (my !== gen) return;
    gen++;
    clearTimeout(openTimer);
    try { if (ws) ws.close(); } catch (e) { }
    ws = null;
    var n = Math.max(1, doors().length);
    door++;
    if (door % n !== 0) { clearTimeout(timer); timer = setTimeout(connect, 150); return; }
    //  یک دورِ کامل و هیچ دری باز نشد
    wsLive = false;
    cloudPoll(true);
    if (rejected >= n) {
      live(false, 'رمزِ سرور پذیرفته نشد — کدِ پمپ را دوباره بزنید');
    } else {
      live(!!data, waitingText());
    }
    //  ⛔ شمارِ ردها مالِ **همین** دور است (۱۴۰۵/۰۷/۱۶): پیش از این یک ردِ دورِ
    //  اول و یکی در دورِ دوم «همهٔ درها رد کردند»ِ دروغ می‌ساخت.
    rejected = 0;
    schedule();
  }

  function schedule() {
    clearTimeout(timer);
    retry = Math.min(retry + 1, 6);
    timer = setTimeout(connect, 2000 * retry);
  }

  var cloudBusy = false, cloudLastAt = 0, cloudNone = false, cloudAt = 0, cloudTimer = 0, cloudGen = 0;

  function cloudText() {
    var when = cloudAt ? new Date(cloudAt).toLocaleString('fa-IR') : '';
    return 'از سرورِ حساب' + (when ? ' · ' + when : '') + ' — شبکهٔ پمپ از این‌جا در دسترس نیست';
  }

  /**
   *  نبضِ آگاهانه: تا وقتی سوکتِ زنده چیزی نمی‌دهد، هر ۶۰ ثانیه عکسِ سرورِ
   *  حساب پرسیده می‌شود — چون خودِ سرورِ حساب هیچ خبری به این صفحه نمی‌فرستد.
   *  همین که سوکت زنده شد، خاموش (‎cloudPoll(false)‎).
   */
  function cloudPoll(on) {
    clearTimeout(cloudTimer);
    if (!on) return;
    cloudFallback();
    cloudTimer = setTimeout(function () { if (!wsLive) cloudPoll(true); }, 60000);
  }

  /**
   * عکسِ سرورِ حساب — فقط با کدِ پمپ، فقط همان پمپ.
   * از هر ۲۰ ثانیه بیشتر نمی‌پرسد؛ برنامهٔ کامپیوتر خودش هر دقیقه یک بار
   * به آن‌جا می‌فرستد، پس تندتر پرسیدن چیزی نمی‌آورد.
   */
  function cloudFallback() {
    if (!cfg.code || !window.PumpCloud || cloudBusy) return;
    if (Date.now() - cloudLastAt < 20000) return;
    cloudBusy = true;
    //  ⛔ جوابِ دیررسِ پمپِ قبلی روی پمپِ تازه نمی‌نشیند (۱۴۰۵/۰۷/۱۶): «پمپِ
    //  دیگر» وسطِ این درخواست، عکسِ پمپِ الف را زیرِ کلیدِ پمپِ ب ذخیره می‌کرد.
    var my = ++cloudGen, asked = cfg.code;
    PumpCloud.cloudLive(asked).then(function (r) {
      if (my !== cloudGen || asked !== cfg.code) return;
      cloudLastAt = Date.now();
      if (!r || !r.live) { cloudNone = true; if (!data) live(false, waitingText()); return; }
      cloudNone = false;
      cloudAt = r.updatedAt || 0;
      if (acceptSnapshot(r.live, true) || fromCloud) {
        if (!wsLive) live(true, cloudText());
        gateReady();
      }
    }).catch(function (err) {
      if (my !== cloudGen || asked !== cfg.code) return;
      cloudLastAt = Date.now();
      //  ۴۰۴ یعنی سرورِ حساب هنوز عکسی از این پمپ ندارد — راستش را بگو
      if (err && err.status === 404 && /live/.test(err.code || 'live')) {
        cloudNone = true;
        if (!data && !wsLive) live(false, waitingText());
      }
    }).then(function () { if (my === cloudGen) cloudBusy = false; });
  }

  // ══════════════════════════════════════════════════════════════════════
  //  ورود با گوگل — و «دیگر از کسی آدرس نپرس»
  // ══════════════════════════════════════════════════════════════════════
  //
  //  خواستهٔ صریحِ صاحب ریپو: «چرا ادرس اینترنتی میخان؟ این رو نخان.»
  //
  //  بعد از ورود، ‎/api/pump/me‎ نشانی و رمزِ فقط‌خواندنیِ سرورِ خانگی را
  //  می‌دهد و همان در ‎cfg‎ می‌نشیند — از این‌جا به بعد هیچ‌چیز عوض نشده و
  //  ‎connect()‎ همان دو در را می‌زند.
  //
  //  ⚠️ راهِ کیو‌آر برداشته نشد: پمپی که برنامه‌اش هنوز به‌روز نشده، و
  //  جایی که شبکهٔ پمپ هست ولی اینترنت نیست، فقط همان را دارند.

  function signinMsg(text, bad) {
    var e = $('signinErr'), st = $('signinState');
    if (bad) {
      if (e) { e.textContent = text; e.classList.remove('hidden'); }
      if (st) st.textContent = '';
    } else {
      if (e) e.classList.add('hidden');
      if (st) st.textContent = text || '';
    }
  }

  /** نشانی و رمز را از ابر بردار و وصل شو. */
  function adoptStation(st) {
    if (!st) {
      signinMsg('این حساب هنوز به هیچ پمپی وصل نیست. از صاحبِ پمپ بخواهید '
        + 'شما را اضافه کند، بعد دوباره همین‌جا وارد شوید.', true);
      return false;
    }
    var home = st.home || {};
    if (!home.url) {
      signinMsg('پمپِ «' + (st.name || st.code) + '» پیدا شد، ولی برنامهٔ '
        + 'کامپیوترش هنوز به سرور وصل نشده است. وقتی روشن شد، خودش وصل می‌شود.', true);
      return false;
    }
    var nextStn = String(home.station || st.code || '');
    var moved = nextStn !== cfg.stn;
    switchStation(nextStn);
    cfg.srv = home.url;
    //  رمزِ فقط‌خواندنی، نه رمزِ برنامه — این همان چیزی است که روی کاغذِ
    //  کیو‌آر می‌رفت، فقط این‌بار از راهِ رمزگذاری‌شده
    cfg.tok = home.readKey || '';
    if (st.accessCode !== undefined) cfg.code = st.accessCode || '';
    cfg.name = st.name || cfg.name || '';
    save();
    resetLink();
    chips();
    //  ⛔ پمپ (پوشه) عوض شد در حالی که اپ باز است (۱۴۰۵/۰۷/۱۶): ‎switchStation‎
    //  داده و «باز» را پاک کرده؛ ماندن روی همان صفحه یعنی اپی یخ‌زده روی عکسِ
    //  پمپِ قبلی. برگرد به درِ ورود تا عکسِ پمپِ تازه برسد.
    if (moved && $('appPane') && !$('appPane').classList.contains('hidden')) openApp();
    return true;
  }

  /** پمپِ تازه ⇒ حالِ اتصالِ پمپِ قبلی هیچ اثری نگذارد. */
  function resetLink() {
    door = 0; retry = 0; rejected = 0; wsLive = false;
    cloudNone = false; cloudLastAt = 0; cloudAt = 0;
    cloudGen++; cloudBusy = false;      // درخواستِ ابرِ پمپِ قبلی بی‌اثر، و راه برای پمپِ تازه باز
    gen++;
    clearTimeout(openTimer); clearTimeout(cloudTimer); clearTimeout(timer);
  }

  /** نشانِ «کدام پمپ» روی صفحهٔ قفل و سربرگ — تا کارمند یک لحظه هم شک نکند. */
  function chips() {
    var label = cfg.stn ? cfg.stn : '';
    if (cfg.code && window.PumpCloud) label = PumpCloud.formatCode(cfg.code) + (label ? ' · ' + label : '');
    ['lockChip', 'stChip'].forEach(function (id) {
      var el = $(id);
      if (!el) return;
      el.textContent = label;
      el.classList.toggle('hidden', !label);
    });
    var name = cfg.name || (data && data.station && data.station.name) || '';
    if (name) { $('lockTitle').textContent = name; $('stName').textContent = name; }
  }

  // ══════════════════════════════════════════════════════════════════════
  //  کدِ پمپ — درِ اصلی
  // ══════════════════════════════════════════════════════════════════════
  //
  //  خواستهٔ صریحِ صاحب ریپو: «هر کسی که برنامه را نصب می‌کند باید آن کد را
  //  بزند تا بتواند بیاید توی حساب‌ها… با پمپ‌های دیگر قاطی نشود.»
  //
  //  کد ⇒ ‎POST /api/pump/public/join‎ ⇒ نشانی و رمزِ خواندنِ همان پمپ ⇒
  //  همان دو درِ سرورِ خانگی. اگر سرورِ خانگی از این‌جا در دسترس نبود،
  //  عکسِ ابریِ همان پمپ (‎cloudFallback‎).

  function codeMsg(text, bad) {
    var e = $('codeErr'), st = $('codeState');
    if (bad) {
      if (e) { e.textContent = text; e.classList.remove('hidden'); }
      if (st) st.textContent = '';
    } else {
      if (e) e.classList.add('hidden');
      if (st) st.innerHTML = text ? '<span class="spin"></span>' + esc(text) : '';
    }
  }

  var joining = false;

  function joinWithCode(raw) {
    if (joining || !window.PumpCloud) return;
    var code = PumpCloud.normalizeCode(raw);
    if (code.length !== 8) { codeMsg('کدِ پمپ هشت رقم است — مثلِ 4829-1736.', true); return; }
    joining = true;
    $('btnJoin').disabled = true;
    codeMsg('در حالِ پیدا کردنِ پمپ…');
    PumpCloud.joinWithCode(code)
      .then(function (st) {
        if (!st) { codeMsg('این کد به هیچ پمپی نمی‌رسد.', true); return; }
        //  پمپ پیدا شد ولی برنامهٔ کامپیوترش هنوز نشانی نداده ⇒ اگر عکسِ
        //  ابری دارد، باز هم می‌شود دید؛ وگرنه صریح بگو.
        if (!st.home || !st.home.url) {
          if (!st.cloudLiveAt) {
            codeMsg('پمپِ «' + (st.name || st.code) + '» پیدا شد، ولی برنامهٔ کامپیوترش هنوز به سرور وصل نشده. '
              + 'وقتی روشن شد، دوباره همین کد را بزنید.', true);
            return;
          }
          st.home = { url: '', readKey: '', station: st.code };
          switchStation(st.code);
          cfg.srv = ''; cfg.tok = ''; cfg.code = st.accessCode || code; cfg.name = st.name || '';
          save();
          chips();
          syncBackground();
          openApp();
          resetLink();
          connect();
          return;
        }
        adoptStation(st);
        codeMsg('');
        $('inCode').value = '';
        syncBackground();
        openApp();
        connect();
      })
      .catch(function (err) {
        var why = err && err.status === 404 ? 'این کد به هیچ پمپی نمی‌رسد. کد را از صاحبِ پمپ بگیرید.'
          : err && err.status === 429 ? 'چند بار پشتِ سرِ هم اشتباه شد؛ چند دقیقه بعد دوباره.'
          : err && err.status ? (err.message || 'خطای سرور')
          : 'به سرور نرسیدیم — اینترنت را بررسی کنید.';
        codeMsg(why, true);
      })
      .then(function () { joining = false; $('btnJoin').disabled = false; });
  }

  /** نشانیِ تازه‌تر با همان کد — آی‌پیِ خانگی با هر بار روشن شدنِ مودم عوض می‌شود. */
  function resumeCode() {
    if (!cfg.code || !window.PumpCloud) return Promise.resolve(false);
    return PumpCloud.joinWithCode(cfg.code).then(function (st) {
      if (!st || !st.home || !st.home.url) return false;
      var moved = st.home.url !== cfg.srv || (st.home.readKey || '') !== cfg.tok
        || (st.home.station || st.code) !== cfg.stn;
      if (!moved) { cfg.name = st.name || cfg.name; save(); chips(); return false; }
      adoptStation(st);
      syncBackground();
      return true;
    }).catch(function (err) {
      //  ۴۰۴ یعنی کد عوض شده (صاحبِ پمپ «عوض کردن» را زده): این گوشی دیگر
      //  راه ندارد — برگرد به صفحهٔ کد و هر چه از این پمپ مانده پاک کن.
      if (err && err.status === 404) { forgetAll('کدِ این پمپ عوض شده است. کدِ تازه را از صاحبِ پمپ بگیرید.'); }
      return false;
    });
  }

  /** خروج از پمپ: همه‌چیزِ همین پمپ از گوشی پاک می‌شود. */
  function forgetAll(note) {
    try { if (ws) ws.close(); } catch (e) { }
    resetLink();
    if (window.PumpCloud) PumpCloud.signOut();
    switchStation('');
    cfg.srv = ''; cfg.tok = ''; cfg.code = ''; cfg.name = ''; cfg.stn = '';
    save();
    //  پاک‌سازیِ کلیدهای جامانده از نسخه‌هایی که هنوز کدِ پیش‌فرض داشتند
    try { localStorage.removeItem(KEY + '.snap.pump1'); localStorage.removeItem(KEY + '.told.pump1'); } catch (e) { }
    syncBackground();
    show('codePane');
    if (note) codeMsg(note, true);
  }

  /** نشستی که از قبل هست — بی آنکه دوباره از گوگل چیزی خواسته شود. */
  function resumeCloud() {
    if (!window.PumpCloud || !PumpCloud.signedIn()) return Promise.resolve(false);
    //  نشستِ جامانده از نسخه‌های پیشین: یک بار نشانیِ تازه، بعد پاک
    return PumpCloud.myStation().then(function (st) {
      PumpCloud.dropSession();
      return adoptStation(st);
    }).catch(function (err) {
      //  ۴۰۱ یعنی نشست واقعاً رفته؛ هر چیز دیگری (نبودِ اینترنت) نباید
      //  کارمند را از کارِ آفلاین بیندازد — عکسِ ذخیره‌شده هنوز هست.
      if (err && err.status === 401) PumpCloud.signOut();
      return false;
    });
  }

  /** جوابِ گوگل ⇒ حساب ⇒ پمپ ⇒ وصل. */
  function onGoogle(resp) {
    if (!resp || !resp.credential) return;
    signinMsg('در حالِ ورود…');
    PumpCloud.signInWithGoogle(resp.credential)
      .then(function () { return PumpCloud.myStation(); })
      .then(function (st) {
        PumpCloud.dropSession();      // ⛔ گوشی نشستِ حساب نگه نمی‌دارد

        if (!adoptStation(st)) return;
        syncBackground();
        openApp();
        connect();
      })
      .catch(function (err) {
        PumpCloud.dropSession();      // شکستِ پس از ورود هم نشستی جا نگذارد
        signinMsg(err && err.message ? err.message : 'ورود نشد', true);
      });
  }

  /**
   * ورود با ایمیل و رمز — همان حسابی که در برنامهٔ کامپیوتر ساخته شده.
   *
   * ⛔ راهِ گوگل برداشته نشد؛ این کنارش نشست. دو دلیل، هر دو سنجیده:
   * ‎googleClientId‎ روی سرور می‌تواند خالی باشد (پیش‌فرضش همین است) و آن
   * وقت صاحبِ پمپ هیچ دری نداشت؛ و حسابی که با ایمیل و رمز ساخته شده
   * اصلاً گوگلی نیست، پس همان آدم روی گوشی‌اش وارد نمی‌شد.
   */
  function signInWithPassword() {
    var email = ($('inEmail') && $('inEmail').value || '').trim();
    var pass = ($('inPw') && $('inPw').value) || '';
    if (!email || email.indexOf('@') < 0) { signinMsg('ایمیل را درست بنویسید.', true); return; }
    if (!pass) { signinMsg('رمز را بنویسید.', true); return; }

    signinMsg('در حالِ ورود…');
    PumpCloud.signInWithPassword(email, pass)
      .then(function () { return PumpCloud.myStation(); })
      .then(function (st) {
        PumpCloud.dropSession();      // ⛔ گوشی نشستِ حساب نگه نمی‌دارد
        //  ⚠️ رمز در حافظهٔ صفحه هم نمی‌ماند
        if ($('inPw')) $('inPw').value = '';
        if (!adoptStation(st)) return;
        syncBackground();
        openApp();
        connect();
      })
      .catch(function (err) {
        PumpCloud.dropSession();      // شکستِ پس از ورود هم نشستی جا نگذارد
        signinMsg(err && err.message ? err.message : 'ورود نشد', true);
      });
  }

  /** «رمزم را فراموش کرده‌ام» ⇒ کد به ایمیل، و کادرِ رمزِ تازه باز می‌شود. */
  function forgotPassword() {
    var email = ($('inEmail') && $('inEmail').value || '').trim();
    if (!email || email.indexOf('@') < 0) { signinMsg('اول ایمیلتان را بنویسید.', true); return; }
    signinMsg('در حالِ فرستادنِ کد…');
    PumpCloud.forgotPassword(email).then(function () {
      //  ⚠️ پیام نمی‌گوید این ایمیل حساب دارد یا نه — خودِ سرور هم برای
      //  موجود و ناموجود یک جواب می‌دهد.
      signinMsg('اگر این ایمیل حسابی داشته باشد، کد برایش رفت.');
      if ($('resetBox')) $('resetBox').classList.remove('hidden');
    }).catch(function (err) {
      signinMsg(err && err.message ? err.message : 'نشد', true);
    });
  }

  /** کدِ ایمیل + رمزِ تازه ⇒ نشست. */
  function resetPassword() {
    var email = ($('inEmail') && $('inEmail').value || '').trim();
    var code = ($('inResetCode') && $('inResetCode').value || '').replace(/[^0-9]/g, '');
    var pass = ($('inNewPass') && $('inNewPass').value) || '';
    if (code.length !== 6) { signinMsg('کد شش رقم است.', true); return; }
    if (pass.length < 8) { signinMsg('رمزِ تازه دستِ‌کم هشت نویسه باشد.', true); return; }

    signinMsg('در حالِ ثبتِ رمزِ تازه…');
    PumpCloud.resetPassword(email, code, pass).then(function (s) {
      if ($('inNewPass')) $('inNewPass').value = '';
      if ($('resetBox')) $('resetBox').classList.add('hidden');
      //  سرور ممکن است نشست ندهد و فقط رمز را عوض کند — آن وقت کاربر
      //  همان‌جا با رمزِ تازه وارد می‌شود.
      if (!s) { signinMsg('رمز عوض شد. حالا با رمزِ تازه وارد شوید.'); return; }
      return PumpCloud.myStation().then(function (st) {
        PumpCloud.dropSession();      // ⛔ گوشی نشستِ حساب نگه نمی‌دارد
        if (!adoptStation(st)) return;
        syncBackground();
        openApp();
        connect();
      });
    }).catch(function (err) {
      signinMsg(err && err.message ? err.message : 'نشد', true);
    });
  }

  /** دکمهٔ گوگل را می‌سازد. شناسهٔ برنامه از خودِ سرور می‌آید، نه از کد. */
  function setupGoogle() {
    var box = $('gBtn');
    if (!box || !window.PumpCloud) return;
    PumpCloud.call('GET', '/api/config').then(function (cfgOut) {
      var id = cfgOut && cfgOut.googleClientId;
      if (!id) {
        signinMsg('ورود با گوگل روی این سرور تنظیم نشده است. '
          + 'فعلاً از راهِ کیو‌آر وصل شوید.', true);
        return;
      }
      //  اسکریپتِ گوگل ‎async‎ است و ممکن است هنوز نیامده باشد
      var tries = 0;
      (function waitForGsi() {
        if (window.google && google.accounts && google.accounts.id) {
          google.accounts.id.initialize({ client_id: id, callback: onGoogle });
          google.accounts.id.renderButton(box, {
            theme: 'filled_blue', size: 'large', shape: 'pill',
            text: 'signin_with', locale: 'fa'
          });
          signinMsg('');
          return;
        }
        if (++tries > 40) {
          signinMsg('دکمهٔ گوگل بالا نیامد — شاید اینترنت نیست. '
            + 'از راهِ کیو‌آر وصل شوید.', true);
          return;
        }
        setTimeout(waitForGsi, 250);
      })();
    }).catch(function () {
      signinMsg('به سرور نرسیدیم. اگر اینترنت ندارید، از راهِ کیو‌آر وصل شوید.', true);
    });
  }

  // ── قفل ────────────────────────────────────────────────────────────────

  /*
   *  ══ «کارمندان آزاد، حساب‌ها با رمزِ برنامه» (۱۴۰۵/۰۷/۱۶) ═══════════════
   *
   *  خواستهٔ صریحِ صاحب ریپو: «بخشِ کارمندان آزاد، و بخشِ حساب‌هاش رمزِ
   *  برنامه رو بخواد — همونی که میرزا گذاشته.»
   *
   *  ⛔ کدِ هشت‌رقمی یک بار زده می‌شود و گوشی مستقیم باز می‌شود — هیچ رمزی
   *  پیش از «⛽ کارمندان» پرسیده نمی‌شود.
   *  ⛔ «📒 حساب‌ها» رمزِ برنامهٔ کامپیوتر را می‌خواهد (هشِ ‎data.gate‎)، و
   *  رمزِ درست **یک بار در هر گوشی** به یاد می‌ماند (‎stnKey('ok')‎ = همان
   *  هشِ منتشرشده، نه خودِ رمز). صاحبِ پمپ که رمز را عوض کند، هش عوض می‌شود
   *  و همهٔ گوشی‌ها یک بار دیگر می‌پرسند. 🔒 همان یاد را پاک می‌کند.
   *  ⛔ تا «حساب‌ها» باز نشده، هیچ عددی از آن بخش حتی در صفحهٔ پنهان هم
   *  نوشته نمی‌شود (‎render‎)، و ربات در درِ کارمندان فقط قرض‌داران و مخزن
   *  را می‌بیند (‎staffView‎).
   *
   *  ⛔ پس گرفته شد (۱۴۰۵/۰۷/۱۶، صاحب ریپو: «بخشِ حساب هم قفلِ برنامهٔ
   *  کامپیوتر رو بخواد که نمی‌خواد»): رمز دیگر در گوشی به یاد نمی‌ماند —
   *  **هر بار که اپ باز می‌شود** یک بار پرسیده می‌شود (‎ownerPassed‎ فقط در
   *  حافظه). و ⛔ بی رمز «حساب‌ها» **باز نمی‌شود**: برنامهٔ کامپیوتری که رمز
   *  نگذاشته هیچ کلیدی ندارد، پس گوشی می‌گوید رمز را کجا بگذارند.
   *  ⚠️ «⛽ کارمندان» همچنان بی هیچ رمزی باز است.
   */
  var pendingOwner = false;
  var ownerPassed = '';   // هشِ رمزی که در همین اجرا درست زده شد — نه روی دیسک

  /** درِ «حساب‌ها» باز است؟ فقط با رمزِ درستِ همین اجرا. */
  function ownerOk() {
    return !!data && !!data.gate && ownerPassed === data.gate;
  }

  function gateReady() {
    var b = $('btnUnlock');
    if (b) b.disabled = !(data && data.gate);
    if (data && data.station && data.station.name) {
      cfg.name = data.station.name;
      $('stName').textContent = data.station.name;
    }
    chips();
    lockButton();
    var st = $('lockState');
    if (st) st.textContent = data ? (data.gate ? '' : '⛔ برنامهٔ کامپیوترِ این پمپ هنوز رمزی ندارد، پس «حساب‌ها» روی گوشی باز نمی‌شود. '
                                    + 'در برنامهٔ کامپیوتر: تنظیمات ← 🔑 رمزها و کد ← رمزِ برنامه را بگذارید.')
                                  : 'در حالِ گرفتنِ اطلاعات از پمپ…';
    var ip = $('inPass');
    if (ip) ip.disabled = !(data && data.gate);
    if (!pendingOwner || !$('lockPane') || $('lockPane').classList.contains('hidden')) return;
    if (ownerOk()) enterOwner();
  }

  /** 🔒 فقط وقتی معنا دارد که رمز هست و همین حالا در «حساب‌ها» هستیم. */
  function lockButton() {
    var lb = $('btnLock');
    if (lb) lb.classList.toggle('hidden', mode !== 'owner');
  }

  /** اپ باز می‌شود — بی هیچ پرسشی؛ فقط درِ «حساب‌ها» پرسش دارد. */
  function openApp() {
    unlocked = true;
    pendingOwner = false;
    $('inPass').value = '';
    show('appPane');
    loadMode();
    setMode(mode);          // بی در ⇒ خانه؛ با در ⇒ همان در (حساب‌ها ⇒ شاید رمز)
    render();
    try { if (window.PumpAndroid && PumpAndroid.boot) PumpAndroid.boot('render'); } catch (e) { }
  }

  /** زدنِ «📒 حساب‌ها» بی رمزِ به‌یادمانده ⇒ صفحهٔ رمز. */
  function askOwner() {
    pendingOwner = true;
    $('lockErr').classList.add('hidden');
    show('lockPane');
    gateReady();
    try { $('inPass').focus(); } catch (e) { }
  }

  function enterOwner() {
    pendingOwner = false;
    $('inPass').value = '';
    show('appPane');
    setMode('owner');
  }

  /** پنجرهٔ تایید — ‎window.confirm‎ در وب‌ویوی اندروید همیشه دیده نمی‌شود. */
  function askSure(title, text, okLabel) {
    return new Promise(function (resolve) {
      var d = $('dlg');
      if (!d) { resolve(window.confirm(title + '\n' + text)); return; }
      $('dlgTitle').textContent = title;
      $('dlgText').textContent = text;
      $('dlgOk').textContent = okLabel || 'بله';
      d.classList.remove('hidden');
      function done(v) {
        d.classList.add('hidden');
        $('dlgOk').onclick = $('dlgNo').onclick = d.onclick = null;
        resolve(v);
      }
      $('dlgOk').onclick = function () { done(true); };
      $('dlgNo').onclick = function () { done(false); };
      d.onclick = function (e) { if (e.target === d) done(false); };
    });
  }

  function askLeave() {
    var nm = (data && data.station && data.station.name) || cfg.name || 'این پمپ';
    askSure('خروج از «' + nm + '»؟',
      'کدِ پمپ و هر چه از این پمپ در این گوشی مانده پاک می‌شود. برای برگشتن باید کدِ هشت‌رقمی را دوباره بزنید. '
      + 'هیچ چیزی از دفترِ پمپ پاک نمی‌شود.', 'بله، خارج شو')
      .then(function (yes) { if (yes) forgetAll(''); });
  }

  function show(which) {
    ['codePane', 'signinPane', 'setupPane', 'lockPane', 'appPane'].forEach(function (id) {
      $(id).classList.toggle('hidden', id !== which);
    });
    $('nav').classList.toggle('hidden', which !== 'appPane' || !mode);
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
      ownerPassed = data.gate;
      enterOwner();
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
    //  ‎data-l‎ = نامِ ستون: روی گوشی هر ردیف یک کارت می‌شود و هر خانه نامِ
    //  ستونش را کنارش دارد، پس هیچ ستونی از صفحه بیرون نمی‌زند (فقط ظاهر).
    var h = '<div class="scroll cards"><table><thead><tr><th>#</th>';
    for (var c = 0; c < head.length; c++) h += '<th>' + esc(head[c]) + '</th>';
    h += '</tr></thead><tbody>';
    for (var r = 0; r < rows.length; r++) {
      h += '<tr><td class="rn" data-l="ردیف">' + (r + 1) + '</td>';
      for (var k = 0; k < head.length; k++) {
        var cell = (rows[r] || [])[k];
        var empty = cell === undefined || cell === null || String(cell).trim() === '';
        h += '<td data-l="' + esc(head[k]) + '"' + (k === 0 ? ' class="t0"' : empty ? ' class="e"' : '') + '>' + esc(cell) + '</td>';
      }
      h += '</tr>';
    }
    return h + '</tbody></table></div>';
  }

  /** حالِ یک قرض‌دار با یک جمله — همان سه حالِ برنامهٔ کامپیوتر. */
  function statusWord(s) {
    return s === 'out' ? 'تمام شده' : s === 'low' ? 'کم مانده' : s === 'ok' ? 'موجودی دارد' : 'ردیفی ندارد';
  }
  function statusIcon(s) { return s === 'out' ? '⛔' : s === 'low' ? '⚠️' : s === 'ok' ? '✅' : '•'; }

  /** سه عددِ الباقی (پول · پطرول · دیزل) — همان عددهای عکس، بی هیچ حسابی. */
  function balMini(b) {
    b = b || {};
    return [['پول', b.money, 'افغانی'], ['پطرول', b.petrol, 'لیتر'], ['دیزل', b.diesel, 'لیتر']].map(function (x) {
      return '<div class="mini"><div class="l">' + x[0] + '</div><div class="v">' + fmt(x[1]) + '</div></div>';
    }).join('');
  }

  /** سربرگِ یک شخص: حرفِ اول، نام، و «چه کنم» با رنگِ حالش. */
  function whoHtml(p, title, withAccounts) {
    var s = esc(p.status || 'none');
    var say = p.status === 'out' ? '⛔ تمام شده — تیلِ اضافه ندهید'
      : p.status === 'low' ? '⚠️ کم مانده — با احتیاط بدهید'
      : p.status === 'ok' ? '✅ موجودی دارد' : 'هنوز ردیفی ندارد';
    return '<div class="pwho ' + s + '"><div class="av">' + esc(String(p.name || '؟').charAt(0)) + '</div>' +
      '<div class="grow"><div class="nm">' + esc(title) + '</div>' +
      (p.phone ? '<div class="sub">📞 <span class="num">' + esc(p.phone) + '</span></div>' : '') + '</div></div>' +
      '<div class="say ' + s + '">' + say + '</div>' +
      //  ⛔ کارت‌های حساب زیرش هستند ⇒ سه عددِ پول/پطرول/دیزل این‌جا تکرار نمی‌شوند
      //  («سربرگ‌های هر دو داخلِ هم‌اند»)؛ هر عدد در دفترِ خودش است.
      (withAccounts ? '' : '<div class="three" style="display:grid;grid-template-columns:repeat(3,1fr);gap:6px">' + balMini(p.bal) + '</div>');
  }

  function blockHtml(b) {
    if (b.who) {
      var w = '<div class="card ans">' + whoHtml(b.who, b.title, !!(b.person && (b.person.accounts || []).length));
      if (b.person) w += personAccountsHtml(b.person, b.staff);
      if (b.note) w += '<div class="sub">' + esc(b.note) + '</div>';
      return w + '</div>';
    }
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
  function personAccountsHtml(p, staff) {
    var h = '', detail = !data || data.detail !== false;
    (p.accounts || []).forEach(function (a, i) {
      a.__pid = p.id; a.__i = i;
      h += acctCard(a, staff, detail);
    });
    return h;
  }

  /** «٪۵» از نامِ «پطرول (٪5)» — فیصدی داخلِ برچسب، همان سربرگِ برنامه. */
  function pctOf(name) {
    var m = /\(٪?([^)]*)\)/.exec(String(name || ''));
    return m ? m[1] : '';
  }

  /**
   * ══ کارتِ هر حساب — همان سربرگِ صفحهٔ حسابِ برنامهٔ کامپیوتر ══════════════
   *
   * خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۶): «هر قرض باید دقیق حسابش معلوم شه، نه این‌که
   * همهٔ صفحه را آن قرض‌دار گرفته باشد؛ مثلِ بخشِ قرض که هر حساب هدرِ مربعی
   * دارد… رنگِ کادر و کادرِ واحدها هم همان مدل.»
   *
   * هر تیل یک ردیف: کادرِ مربعیِ رنگی (⛽ پطرول سبز · 🟤 دیزل نارنجی) و چهار
   * کادر — فیصدی ما (آبی) · رسید (سبز) · برد (نارنجی) · الباقی (رنگِ حال).
   * ⛔ هیچ عددی این‌جا ساخته نمی‌شود: ‎a.fuels‎ همان ‎AcctSnapshots‎ی برنامه است.
   * ⛔ درِ کارمندان (‎staff‎) فیصدیِ پمپ و جدولِ ردیف‌ها را نمی‌بیند.
   * جدولِ ردیف‌ها پشتِ یک دکمه است تا یک حساب کلِ صفحه را نگیرد.
   */
  /**
   * ══ دو دفترِ هر حساب — «واحد تیل» و «واحد پول» ══════════════════════════
   *
   * صاحب ریپو (۱۴۰۵/۰۷/۱۶، با عکس): «نمی‌شود واحدِ تیل یا پول را عوض کرد و
   * سربرگ‌های هر دو داخلِ هم‌اند.» هر حساب در برنامهٔ کامپیوتر دو دفترِ کاملاً
   * جدا دارد و گوشی فقط دفترِ فعال را می‌گرفت. حالا ‎a.books‎ هر دو را دارد و
   * دو دکمهٔ بالای کارت یکی را نشان می‌دهند — سربرگ، جدول و PDF همه از همان
   * یک دفتر. ⛔ هیچ عددی این‌جا ساخته نمی‌شود.
   * ⚠️ برنامهٔ کامپیوترِ کهنه‌تر ‎books‎ نمی‌فرستد ⇒ فقط دفترِ فعال، و دکمهٔ دیگر
   * می‌گوید چرا خالی است.
   */
  var bookSel = {};      // ‎«pid|i» ⇒ 'fuel' | 'money'‎ — فقط در حافظه
  var acctReg = {};      // ‎«pid|i» ⇒ {a, staff, detail}‎ برای کشیدنِ دوبارهٔ همان کارت

  function booksOf(a) {
    if (a.books && a.books.length) return a.books.map(function (b) {
      return { money: !!b.money, on: !!b.on, sum: b.s || [], fuels: b.f || [], head: b.h || [], rows: b.r || [] };
    });
    return [{ money: a.unit === 'money', on: true, sum: a.sum || [], fuels: a.fuels || [],
              head: a.head || [], rows: a.rows || [], legacy: true }];
  }

  function bookOf(a) {
    var bs = booksOf(a), want = bookSel[a.__pid + '|' + a.__i];
    if (want) {
      var m = want === 'money';
      for (var i = 0; i < bs.length; i++) if (bs[i].money === m) return bs[i];
      return { money: m, on: false, sum: [], fuels: [], head: [], rows: [], empty: true, legacy: !a.books };
    }
    for (var j = 0; j < bs.length; j++) if (bs[j].on) return bs[j];
    return bs[0];
  }

  function acctCard(a, staff, detail) {
    var st = esc(a.st || 'none');
    var bk = bookOf(a), money = bk.money;
    var key = a.__pid + '|' + a.__i;
    acctReg[key] = { a: a, staff: staff, detail: detail };
    var active = a.unit === 'money';
    function tab(m) {
      return '<button class="btab ' + (m ? 'money' : 'fuel') + (m === money ? ' on' : '') + '" data-book="' + esc(key) + '|' +
        (m ? 'money' : 'fuel') + '">' + (m ? '💵 واحد پول' : '⛽ واحد تیل') + (m === active ? ' <small>•فعال</small>' : '') + '</button>';
    }
    var h = '<div class="acard ' + st + '" data-acct="' + esc(key) + '" data-staff="' + (staff ? 1 : 0) + '"><div class="ahd">' +
      '<span class="an">📒 ' + esc(a.title || 'حسابِ اصلی') + '</span>' +
      '<span class="badge ' + st + '">' + statusIcon(a.st) + ' ' + statusWord(a.st) + '</span></div>' +
      '<div class="btabs">' + tab(false) + tab(true) + '</div>';
    if (bk.empty) {
      return h + '<div class="sub" style="padding:10px 12px">' + (bk.legacy
        ? 'این دفتر هنوز به گوشی نیامده — برنامهٔ کامپیوترِ پمپ را به‌روز کنید تا هر دو دفتر بیاید.'
        : 'این حساب در «' + (money ? 'واحد پول' : 'واحد تیل') + '» هنوز هیچ ردیفی ندارد.') + '</div></div>';
    }
    var fuels = (bk.fuels || []).filter(function (f) {
      return [1, 2, 3, 4].some(function (i) { return num(f[i]) !== 0; });
    });
    if (!fuels.length) fuels = bk.fuels || [];
    //  برنامهٔ کامپیوترِ کهنه‌تر ‎fuels‎ نمی‌فرستد ⇒ همان چهار عددِ جمعِ دفتر
    if (!fuels.length && (bk.sum || []).length) {
      h += '<div class="figs" style="padding:9px 11px">';
      (bk.sum || []).forEach(function (x) {
        if (staff && /فیصدی/.test(x[0])) return;
        h += '<div class="fig"><div class="l">' + esc(x[0]) + '</div><div class="v">' + esc(x[1]) + '</div></div>';
      });
      h += '</div>';
    }
    var unitWord = money ? 'افغانی' : 'لیتر';
    fuels.forEach(function (f) {
      var diesel = /دیزل/.test(f[0]);
      var alb = num(f[4]);
      h += '<div class="frow">' +
        '<div class="fsq ' + (diesel ? 'diesel' : 'petrol') + '">' + (diesel ? '🟤' : '⛽') +
          '<b>' + (diesel ? 'دیزل' : 'پطرول') + '</b>' +
          (!staff && pctOf(f[0]) ? '<small>٪' + esc(pctOf(f[0])) + '</small>' : '') + '</div>' +
        '<div class="fbx">' +
          (staff ? '' : '<div class="fb pct"><span class="l">فیصدی' +
            '</span><span class="v">' + esc(f[3]) + '</span></div>') +
          '<div class="fb rasid"><span class="l">رسید</span><span class="v">' + esc(f[2]) + '</span></div>' +
          '<div class="fb bord"><span class="l">برد</span><span class="v">' + esc(f[1]) + '</span></div>' +
          '<div class="fb alb ' + (alb > 0 ? 'owe' : alb < 0 ? 'cred' : '') + '" title="' + unitWord + '"><span class="l">الباقی' +
            '</span><span class="v">' + esc(f[4]) + '</span></div>' +
        '</div></div>';
    });
    if (!staff) {
      var rows = bk.rows || [];
      if (detail && rows.length && a.__pid != null)
        h += '<button class="pbtn" data-print-acct="' + esc(a.__pid) + '|' + esc(a.__i) + '">🖨 PDF / چاپ — همان ورقِ برنامهٔ کامپیوتر</button>';
      if (detail && rows.length)
        h += '<details class="arows"><summary>📋 جدولِ ردیف‌ها (' + fmt(rows.length) + ')</summary>' +
          tableHtml(bk.head || [], rows.slice(-40)) + '</details>';
      else if (!detail)
        h += '<div class="sub">جمع‌ها درست‌اند، ولی ردیف‌های این حساب در این عکس '
          + 'نیامده‌اند — دفترِ پمپ بزرگ‌تر از آن است که همه‌اش به گوشی بیاید. '
          + 'ردیف‌ها را در خودِ برنامهٔ کامپیوتر ببینید.</div>';
    }
    return h + '</div>';
  }

  // ══════════════════════════════════════════════════════════════════════
  //  🖨 PDF و چاپ — همان ورقِ برنامهٔ کامپیوتر
  // ══════════════════════════════════════════════════════════════════════
  //
  //  خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۶): «PDF یا پرینت همان مدلِ برنامهٔ
  //  کامپیوتر بیاید… این‌جا متفاوت نشان می‌دهد مشکلی نیست، ولی مدلِ PDF و
  //  پرینت همان باشد.» پس این ورق رونوشتِ ‎DocStyle‎ و ‎DebtorStatementReport‎ِ
  //  ‎PumpYaqobi.Reporting‎ است: همان رنگ‌ها (‎#b45309‎ عنوان، ‎#2d3748‎ سرستون و
  //  «جمله»، ردیفِ زوجِ ‎#fafbfc‎)، همان اندازه‌ها به پوینت، همان دو کادرِ «حساب
  //  پطرول» و «حساب دیزل» با چهار خانه، و همان پاورقیِ «ورق N از M» و تاریخ‌ها.
  //  ⛔ هیچ عددی این‌جا ساخته نمی‌شود — هر خانه همان رشتهٔ عکسِ برنامه است.
  //  ⛔ یک در برای هر دو دستگاه: اندروید با ‎PumpAndroid.printHtml‎ (PrintManagerِ
  //  خودِ اندروید)، بقیه با ‎window.print()‎ی خودِ مرورگر («ذخیره به PDF»).
  var PDOC = {
    title: '#b45309', sub: '#5b6472', headBg: '#2d3748', headFg: '#e2e8f0', headLine: '#4a5568',
    cellLine: '#e3e6eb', cellFg: '#1f2430', rowAlt: '#fafbfc', foot: '#7b8494', footLine: '#d6dae1',
    index: '#718096', hawala: '#c07700', fuel: '#2b6cb0', money: '#276749', danger: '#e53e3e',
    blue: '#2b6cb0', petrol: '#b45309', diesel: '#8a5a2b'
  };

  /** نامِ ستون‌های عکسِ گوشی ⇒ همان سرستونِ ورقِ برنامه (‎DebtorStatementReport‎). */
  var PDOC_HEAD = { 'تیل': 'نوع تیل', 'فی': 'فی لیتر', 'بردگی': 'مقدار بردگی' };

  /** رنگِ هر ستون از نامِ خودش — همان ‎Td(…, DocStyle.X)‎ی گزارش‌ها. */
  function pdocColColor(h) {
    h = String(h || '');
    if (h === '#') return PDOC.index;
    if (/حواله|بردگی|^برد$/.test(h)) return PDOC.hawala;
    if (/رسید تیل|مقدار تیل|لیتر/.test(h)) return PDOC.fuel;
    if (/رسید|پول|افغانی|دالر/.test(h)) return PDOC.money;
    if (/الباقی|خالص/.test(h)) return PDOC.blue;
    if (/فیصدی/.test(h)) return PDOC.danger;
    return '';
  }

  /**
   * ‎doc‎: ‎{ title, sub, dates, boxes:[{ diesel, pct, unit, comm, rasid, bord, alb }],
   *          head:[…], rows:[[…]], total:[…]|null }‎ ⇒ یک سندِ HTMLِ کامل.
   */
  function printDocHtml(doc) {
    var P = PDOC, e = esc;
    var css =
      '@page{size:A4;margin:14mm 12mm 16mm;' +
        '@bottom-center{content:"ورق " counter(page, persian) " از " counter(pages, persian);font:8.9pt Vazirmatn,Tahoma,sans-serif;color:' + P.foot + '}' +
        '@bottom-right{content:"' + String(doc.dates || '').replace(/["\\]/g, '') + '";font:8.9pt Vazirmatn,Tahoma,sans-serif;color:' + P.foot + '}}' +
      '*{box-sizing:border-box;-webkit-print-color-adjust:exact;print-color-adjust:exact}' +
      'body{margin:0;background:#fff;color:' + P.cellFg + ';font:600 10.1pt Vazirmatn,Tahoma,"Segoe UI",sans-serif;direction:rtl}' +
      '.pd-h{border-bottom:2pt solid ' + P.title + ';padding-bottom:6pt;margin-bottom:8pt}' +
      '.pd-t{font-size:14.2pt;font-weight:800;color:' + P.title + '}' +
      '.pd-s{font-size:9.7pt;color:' + P.sub + ';margin-top:2pt;font-weight:500}' +
      '.pd-box{display:flex;gap:6pt;border:1.2pt solid;padding:6pt;margin-bottom:6pt;break-inside:avoid}' +
      '.pd-box .nm{width:112pt;flex:none;white-space:nowrap;display:flex;align-items:center;justify-content:center;border:1pt solid;font-size:10.5pt;font-weight:800;padding:5pt}' +
      '.pd-box .sb{flex:1;border:1pt solid ' + P.cellLine + ';padding:5pt;text-align:center}' +
      '.pd-box .sb .l{font-size:7.8pt;color:' + P.sub + ';font-weight:500}' +
      '.pd-box .sb .v{font-size:10.5pt;font-weight:800;white-space:nowrap}' +
      'table{width:100%;border-collapse:collapse;margin-top:4pt}' +
      'thead{display:table-header-group}tr{break-inside:avoid}' +
      'th,.tf{background:' + P.headBg + ';color:' + P.headFg + ';border:1pt solid ' + P.headLine + ';font-size:9.5pt;font-weight:800;padding:5pt 4pt;text-align:center}' +
      'td{border:1pt solid ' + P.cellLine + ';padding:5pt 4pt;text-align:center;font-size:10.1pt;white-space:nowrap}' +
      'td.w{white-space:normal}tbody tr:nth-child(even) td{background:' + P.rowAlt + '}';
    var h = '<div class="pd-h"><div class="pd-t">' + e(doc.title || '') + '</div>' +
      (doc.sub ? '<div class="pd-s">' + e(doc.sub) + '</div>' : '') + '</div>';
    (doc.boxes || []).forEach(function (b) {
      var line = b.diesel ? P.diesel : P.petrol, u = b.unit ? ' ' + e(b.unit) : '';
      function sb(label, val, color) {
        return '<div class="sb"><div class="l">' + e(label) + '</div><div class="v" style="color:' + color + '">' + e(val || '0') + u + '</div></div>';
      }
      h += '<div class="pd-box" style="border-color:' + line + '"><div class="nm" style="border-color:' + line + ';color:' + line + '">' +
        (b.diesel ? '🟤 حساب دیزل' : '⛽ حساب پطرول') + '</div>' +
        sb('فیصدی ما' + (b.pct ? ' (' + b.pct + '٪)' : ''), b.comm, P.danger) +
        sb('رسید قبلی', b.rasid, P.money) + sb('برد', b.bord, P.hawala) + sb('الباقی', b.alb, P.blue) + '</div>';
    });
    var head = ['#'].concat((doc.head || []).map(function (x) { return PDOC_HEAD[x] || x; }));
    var fuelCol = head.indexOf('نوع تیل');
    if ((doc.rows || []).length) {
      h += '<table><thead><tr>' + head.map(function (x) { return '<th>' + e(x) + '</th>'; }).join('') + '</tr></thead><tbody>';
      (doc.rows || []).forEach(function (r, i) {
        var cells = [fmt(i + 1)].concat(r);
        if (fuelCol > 0) cells[fuelCol] = cells[fuelCol] === 'دیزل' ? '🟤 دیزل' : cells[fuelCol] === 'پطرول' ? '⛽ پطرول' : cells[fuelCol];
        h += '<tr>' + cells.map(function (c, j) {
          var col = pdocColColor(head[j]), wide = /نام|شرح|توضیح|یادداشت/.test(head[j] || '');
          return '<td' + (wide ? ' class="w"' : '') + (col ? ' style="color:' + col + '"' : '') + '>' +
            (c === '' || c == null ? '—' : e(c)) + '</td>';
        }).join('') + '</tr>';
      });
      h += '</tbody>';
      if (doc.total && doc.total.length) {
        //  «جمله» مثلِ برنامه ستون‌های پیش از نخستین عدد را می‌گیرد
        //  ‎tot[k]‎ زیرِ ستونِ ‎head[k + 1]‎ است؛ «جمله» ستونِ «#» و خانه‌های خالیِ اول را می‌گیرد
        var tot = doc.total.slice(0, head.length - 1), first = 0;
        while (first < tot.length && (tot[first] == null || tot[first] === '')) first++;
        if (fuelCol > 0) first = Math.min(first, fuelCol);
        h += '<tfoot><tr><td class="tf" colspan="' + (first + 1) + '">جمله</td>' + tot.slice(first).map(function (c) {
          return '<td class="tf">' + e(c == null || c === '' ? '—' : c) + '</td>';
        }).join('') + '</tr></tfoot>';
      }
      h += '</table>';
    }
    return '<!doctype html><html lang="fa" dir="rtl"><head><meta charset="utf-8"><title>' + e(doc.title || '') +
      '</title><style>' + css + '</style></head><body>' + h + '</body></html>';
  }

  /** پاورقیِ تاریخ — همان ‎DocDates‎ی برنامه: شمسی · قمری · میلادیِ امروز. */
  function pdocDates() {
    function part(cal) {
      try {
        var f = new Intl.DateTimeFormat('en-US-u-ca-' + cal, { year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date());
        var g = {}; f.forEach(function (x) { g[x.type] = x.value; });
        return String(g.year || '').replace(/\D/g, '') + '/' + g.month + '/' + g.day;
      } catch (e) { return ''; }
    }
    var d = new Date(), pad = function (n) { return (n < 10 ? '0' : '') + n; };
    return [part('persian'), part('islamic-umalqura'), d.getFullYear() + '/' + pad(d.getMonth() + 1) + '/' + pad(d.getDate())]
      .filter(Boolean).join(' · ');
  }

  /** سندِ یک حسابِ قرض‌دار — همان ‎DebtorStatementReport‎. */
  function acctDoc(p, a) {
    //  ⛔ همان دفتری که روی صفحه است (تیل یا پول)، نه همیشه دفترِ فعال
    var bk = bookOf(a);
    var money = bk.money, unit = money ? 'افغانی' : 'لیتر';
    var pump = (data && data.station && data.station.name) || cfg.name || 'پمپ بنزین';
    var boxes = (bk.fuels || []).filter(function (f) {
      return [1, 2, 3, 4].some(function (i) { return num(f[i]) !== 0; });
    }).map(function (f) {
      return { diesel: /دیزل/.test(f[0]), pct: pctOf(f[0]), unit: unit, bord: f[1], rasid: f[2], comm: f[3], alb: f[4] };
    });
    var head = bk.head || [], rows = bk.rows || [], total = null;
    var sum = {};
    (bk.sum || []).forEach(function (x) { sum[x[0]] = x[1]; });
    if (rows.length) {
      total = head.map(function (hh) {
        if (hh === 'بردگی') return sum['جمله بردگی'] || '';
        if (/^رسید/.test(hh)) return sum['جمله رسید'] || '';
        return '';
      });
    }
    return {
      title: '⛽ ' + pump + ' — ' + p.name + (a.title ? ' — ' + a.title : ''),
      sub: 'حساب قرض‌دار — واحد ' + (money ? 'پول' : 'تیل'),
      dates: pdocDates(), boxes: boxes, head: head, rows: rows, total: total
    };
  }

  /** سندِ یک بخش (گاوصندوق، مصارف، …) با همان صافیِ ماه که روی صفحه است. */
  function sectionDoc(sec, month) {
    var pump = (data && data.station && data.station.name) || cfg.name || 'پمپ بنزین';
    var rows = [];
    (sec.rows || []).forEach(function (r, i) { if (!month || (sec.m || [])[i] === month) rows.push(r); });
    return {
      title: '⛽ ' + pump + ' — ' + (sec.t || ''),
      sub: (month ? 'ماهِ ' + month : 'همهٔ ماه‌ها') +
        ((sec.sum || []).length ? ' · ' + sec.sum.map(function (x) { return x[0] + ': ' + x[1]; }).join(' · ') : ''),
      dates: pdocDates(), boxes: [], head: sec.head || [], rows: rows, total: null
    };
  }

  function runPrint(doc) {
    var html = printDocHtml(doc);
    try {
      if (window.PumpAndroid && typeof PumpAndroid.printHtml === 'function') { PumpAndroid.printHtml(html, doc.title || ''); return; }
    } catch (e) { }
    //  مرورگر و آیفون: ورق داخلِ همین صفحه، فقط برای چاپ دیده می‌شود
    var root = $('printRoot');
    if (!root) { root = document.createElement('div'); root.id = 'printRoot'; document.body.appendChild(root); }
    var st = /<style>([\s\S]*)<\/style>/.exec(html)[1];
    var body = /<body>([\s\S]*)<\/body>/.exec(html)[1];
    root.innerHTML = '<style>@media print{' + st.replace(/body\{/, '#printRoot{') + '}</style>' + body;
    document.body.classList.add('printing');
    var done = function () { document.body.classList.remove('printing'); root.innerHTML = ''; window.removeEventListener('afterprint', done); };
    window.addEventListener('afterprint', done);
    try { window.print(); } catch (e) { done(); }
  }

  /**
   * کارتِ مربعیِ یک قرض‌دار — همان کارتِ بخشِ «قرض‌داران»ِ برنامهٔ کامپیوتر:
   * نوارِ زنده بالا، نام درشت، الباقیِ پول درشت، نشانِ «اتمام تیل/پول» و خطِ
   * پایینِ ⛽/🟤. رنگِ لبه از حالِ حساب.
   */
  function debtCard(p, extra) {
    var b = p.bal || {}, u = p.use || {};
    var pct = -1;
    ['stP', 'stD', 'stM'].forEach(function (k) {
      var x = u[k];
      if (x && Number(x[0]) > 0) pct = Math.max(pct, Math.round(Number(x[1]) / Number(x[0]) * 100));
    });
    var bar = pct >= 0 ? Math.max(0, Math.min(100, pct)) : 0;
    var fuelOut = p.stP === 'out' || p.stD === 'out', moneyOut = p.stM === 'out';
    var nAc = (p.accounts || []).length;
    return '<button class="dcard ' + (extra ? extra + ' ' : '') + esc(p.status || 'none') + '" data-pid="' + esc(p.id) + '">' +
      '<span class="meter"><i style="width:' + bar + '%"></i></span>' +
      '<span class="top"><span class="badge ' + esc(p.status || 'none') + '">' + statusIcon(p.status) + ' ' + statusWord(p.status) + '</span>' +
        (pct >= 0 ? '<span class="pc">٪' + fmt(pct) + '</span>' : '') + '</span>' +
      '<span class="n">' + esc(p.name) + '</span>' +
      '<span class="big">' + fmt(b.money) + '</span>' +
      '<span class="lbl">الباقی · افغانی</span>' +
      '<span class="flags">' + (fuelOut ? '<em>⛔ اتمام تیل</em>' : '') + (moneyOut ? '<em>⛔ اتمام پول</em>' : '') +
        (nAc > 1 ? '<i>📒 ' + fmt(nAc) + ' حساب</i>' : '') + '</span>' +
      '<span class="foot"><span>⛽ ' + fmt(b.petrol) + '</span><span>🟤 ' + fmt(b.diesel) + '</span></span>' +
      '</button>';
  }

  /** چهار عددِ نوارِ بالای برنامهٔ کامپیوتر + مخزن + کاشیِ بخش‌ها — درِ «حساب‌ها». */
  function renderDash() {
    var box = $('bannerBox');
    if (!box) return;
    var bn = (data && data.banner) || [];
    box.innerHTML = bn.length
      ? bn.map(function (b) {
          return '<div class="bn ' + esc(b[2] || '') + '"><div class="l">' + esc(b[0]) + '</div>' +
            '<div class="v">' + esc(b[1]) + '</div><div class="u">افغانی</div></div>';
        }).join('')
      : '<div class="sub">برنامهٔ کامپیوتر باید به نسخهٔ تازه برسد تا این چهار عدد بیاید.</div>';
    $('dashTank').innerHTML = tankFigs(true);
    var secs = (data && data.sections) || {};
    $('dashTiles').innerHTML = Object.keys(secs).map(function (id) {
      var sec = secs[id], first = (sec.sum || [])[0];
      return '<button class="tile" data-sec="' + esc(id) + '"><span class="ti">' + secIcon(id) + '</span><b>' + esc(sec.t) + '</b>' +
        '<span class="sub">' + fmt((sec.rows || []).length) + ' ردیف' + (first ? ' · ' + esc(first[0]) : '') + '</span>' +
        (first ? '<span class="v">' + esc(first[1]) + '</span>' : '') + '</button>';
    }).join('') || '<div class="sub">هنوز بخشی نرسیده.</div>';
  }

  /** نشانهٔ هر بخش — همان نشانه‌های نوارِ برنامهٔ کامپیوتر (فقط ظاهر). */
  var SEC_ICONS = {
    safe: '🔐', sarrafi: '💱', expense: '💸', chakana: '🛒', extraincome: '📈', invoice: '🧾',
    storage: '🛢️', staff: '🧑‍💼', parcha: '🧵', waraq: '📄', debtrasid: '📥', parcharasid: '🧾',
    attendance: '🕘', company: '🏭', amanat: '🤝', debt: '👥'
  };
  function secIcon(id) { return SEC_ICONS[id] || '📁'; }

  var staffFilter = '';

  /** چراغِ هر قرض‌دار — درِ «کارمندان». سبز: دارد · زرد: کم · سرخ: تمام/اضافه. */
  function renderStaff() {
    var list = $('staffList');
    if (!list) return;
    var people = (data && data.debtors) || [];
    var q = norm(($('inStaffFind') || {}).value || '');
    var c = { '': people.length, out: 0, low: 0, ok: 0 };
    people.forEach(function (p) { if (c[p.status] !== undefined) c[p.status]++; });
    $('staffCounts').innerHTML = [['', 'همه'], ['out', '⛔ تمام‌شده'], ['low', '⚠️ کم مانده'], ['ok', '✅ دارد']]
      .map(function (f) {
        return '<button data-f="' + f[0] + '" aria-selected="' + (staffFilter === f[0]) + '">' +
          '<b>' + fmt(c[f[0]] || 0) + '</b>' + f[1] + '</button>';
      }).join('');

    //  ⚠️ سرخ اول، بعد زرد، بعد سبز — ‎out‎ صفر است، پس ‎||‎ نه (صفر را ۳ می‌کرد
    //  و تمام‌شده‌ها تهِ فهرست می‌رفتند؛ آزمونِ مرورگر همین را گرفت).
    var order = { out: 0, low: 1, ok: 2, none: 3 };
    var rank = function (p) { return p.status in order ? order[p.status] : 3; };
    var rows = people.filter(function (p) {
      if (staffFilter && p.status !== staffFilter) return false;
      return !q || norm(p.name).indexOf(q) >= 0;
    }).sort(function (a, b) {
      return rank(a) - rank(b) || String(a.name).localeCompare(String(b.name), 'fa');
    });
    //  ⛔ همان کارتِ مربعیِ «قرض‌داران» (۱۴۰۵/۰۷/۱۶) — خواستهٔ صاحب ریپو:
    //  «برای صفحهٔ کارمندان همون مدل باشه». کلاسِ ‎st-row‎ برای آزمون‌ها می‌ماند.
    list.innerHTML = rows.map(function (p) { return debtCard(p, 'st-row'); }).join('')
      || '<div class="sub">کسی پیدا نشد.</div>';
  }

  /** کارتِ هر تیل: موجودی درشت، حال، و وارد/فروش — همان عددهای عکس. */
  function tankCard(name, x, compact) {
    var cls = x.low ? ' bad' : (x.near ? ' warn' : '');
    var badge = x.low ? '<span class="badge out">⛔ کم آمده</span>'
      : x.near ? '<span class="badge low">⚠️ نزدیکِ حد</span>' : '<span class="badge ok">✅ کافی</span>';
    //  نوارِ پرشدگی: موجودی نسبت به کلِ واردشده — فقط وقتی هر دو عدد هست
    var inL = Number(x['in']) || 0, showL = Number(x.show) || 0;
    var pct = inL > 0 ? Math.max(0, Math.min(100, Math.round(showL / inL * 100))) : -1;
    return '<div class="tank' + cls + (name === 'دیزل' ? ' diesel' : '') + '"><div class="hd"><b>' + (name === 'پطرول' ? '🟠 ' : '🔵 ') + name + '</b>' + badge + '</div>' +
      '<div class="sub">موجودی</div><div class="big">' + fmt(x.show) + ' <small>لیتر</small></div>' +
      (pct >= 0 ? '<div class="gauge" title="' + pct + '٪ از واردشده"><i style="width:' + pct + '%"></i></div>' : '') +
      (compact ? '' : '<div class="io"><div class="mini"><div class="l">وارد</div><div class="v">' + fmt(x['in']) + '</div></div>' +
        '<div class="mini"><div class="l">فروش</div><div class="v">' + fmt(x.out) + '</div></div></div>') + '</div>';
  }

  function tankFigs(compact) {
    var t = (data && data.tank) || {}, h = '';
    [['petrol', 'پطرول'], ['diesel', 'دیزل']].forEach(function (p) { h += tankCard(p[1], t[p[0]] || {}, compact); });
    return h || '<div class="sub">داده‌ای نیست.</div>';
  }

  function renderTank() {
    $('tankBox').innerHTML = tankFigs(false);

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
      h += debtCard(p, '');
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
        secIcon(id) + ' ' + esc(secs[id].t) + '</button>';
    }).join('');

    var sec = secs[secTab];
    var months = {}, list = [];
    (sec.m || []).forEach(function (m) { if (m && !months[m]) { months[m] = 1; list.push(m); } });
    list.sort().reverse();
    var sel = $('selMonth'), keep = sel.value;
    sel.innerHTML = '<option value="">همهٔ ماه‌ها</option>' +
      list.map(function (m) { return '<option value="' + esc(m) + '">' + esc(m) + '</option>'; }).join('');
    if (list.indexOf(keep) >= 0) sel.value = keep;

    //  ⚠️ عکسی که از راهِ سرورِ حساب آمده زیرِ سقفِ آن سرور بریده شده (‎from‎ =
    //  نخستین ماهی که آمده)؛ جمع‌ها کامل‌اند و فقط ردیف‌های ماه‌های پیش‌تر نیامده‌اند.
    var cut = data && data.from
      ? '<div class="card sub">ردیف‌های پیش از ' + esc(data.from) +
        ' از راهِ اینترنت نمی‌آیند — در شبکهٔ پمپ همه دیده می‌شوند. جمع‌ها کامل‌اند.</div>'
      : '';
    $('secBox').innerHTML = cut +
      ((sec.rows || []).length ? '<button class="pbtn" data-print-sec="' + esc(secTab) + '">🖨 PDF / چاپ — همان ورقِ برنامهٔ کامپیوتر</button>' : '') +
      blockHtml(sectionBlock(secTab, sec, sel.value || null));
  }

  // ══════════════════════════════════════════════════════════════════════
  //  هشدارها — «برنامه یک پیام بدهد»
  // ══════════════════════════════════════════════════════════════════════
  //
  //  خواستهٔ صریحِ صاحب ریپو: «وقتی که یک قرض‌دار اضافه برد یا کم مانده بود از
  //  حسابش، برنامه یک پیام بدهد — حتی اگر گوشی خاموش یا حتی اگر توی برنامه
  //  نبود هم پیام برود تا بفهمد.»
  //
  //  سه لایه، از نزدیک به دور:
  //    ۱) کادرِ بالای صفحه — وقتی اپ باز است.
  //    ۲) اعلانِ خودِ مرورگر — وقتی اپ باز است ولی جای دیگری نگاه می‌کند
  //       (و روی آیفون، تنها راهِ ممکن).
  //    ۳) کارِ پس‌زمینهٔ اندروید (‎PumpAlerts‎) — وقتی اپ اصلاً بسته است.
  //
  //  ⚠️ فهرست این‌جا ساخته نمی‌شود: ‎StationSnapshot.Alerts‎ی خودِ برنامهٔ
  //  کامپیوتر می‌سازدش و در ‎data.alerts‎ می‌آید. اگر این‌جا قاعده‌ای جدا
  //  نوشته می‌شد، روزی کارتِ قرض‌دار سرخ می‌بود و گوشی ساکت.

  var toldKeys = {};
  //  ⚠️ کلیدِ هر پمپ جداست (‎stnKey‎): خبرِ «گفته‌شده»ی پمپِ قبلی نباید
  //  خبرِ پمپِ تازه را ساکت کند.
  function TOLD() { return stnKey('told'); }
  //  ⛔ خوانده شدنش در ‎load()‎ است، نه این‌جا (۱۴۰۵/۰۷/۱۶): این خط پیش از
  //  ‎load()‎ می‌دوید، ‎cfg.stn‎ هنوز خالی بود و کلیدِ غلط خوانده می‌شد — پس با
  //  هر باز شدنِ اپ همهٔ خبرهای باز دوباره اعلان می‌شدند.

  function alertsOf(d) {
    var a = (d && d.alerts) || [];
    return Object.prototype.toString.call(a) === '[object Array]' ? a : [];
  }

  /** اعلانِ خودِ مرورگر برای خبرهایی که تا حالا نگفته‌ایم. */
  function pushNew(list) {
    var r = freshAlerts(list, toldKeys);
    var fresh = r.fresh;
    toldKeys = r.told;
    try { localStorage.setItem(TOLD(), JSON.stringify(toldKeys)); } catch (e) { }

    if (!fresh.length) return;
    if (typeof Notification === 'undefined' || Notification.permission !== 'granted') return;
    for (var j = 0; j < fresh.length && j < 5; j++) {
      try {
        new Notification(fresh[j].s === 'out' ? '⛔ اضافه نده' : '⚠️ کم مانده',
                         { body: fresh[j].t || '', tag: fresh[j].k });
      } catch (e) { }
    }
  }

  function renderAlerts() {
    var card = $('alertCard'), box = $('alertBox'), hint = $('alertHint');
    if (!card) return;
    var list = alertsOf(data);

    card.classList.toggle('hidden', list.length === 0 || ALERT_PANES.indexOf(curPane) < 0);
    if (list.length) {
      box.innerHTML = list.map(function (a) {
        var out = a.s === 'out';
        return '<div class="al ' + (out ? 'out' : 'low') + '"><span class="ic">' + (out ? '⛔' : '⚠️') + '</span>' +
          '<div><div class="t">' + esc(a.t || '') + '</div>' +
          (a.a ? '<div class="a">👉 ' + esc(a.a) + '</div>' : '') + '</div></div>';
      }).join('');
    }

    var btn = $('btnNotify');
    var can = typeof Notification !== 'undefined';
    if (btn) btn.classList.toggle('hidden', !can || Notification.permission === 'granted');

    if (hint) {
      hint.textContent = !can
        ? 'این مرورگر اعلان ندارد؛ خبرها همین‌جا دیده می‌شوند.'
        : Notification.permission !== 'granted'
          ? 'برای این‌که وقتی جای دیگری نگاه می‌کنید هم خبر بگیرید، «اعلان روشن شود» را بزنید.'
          : hasBackground()
            ? 'خبرها حتی وقتی برنامه بسته باشد هم می‌آیند.'
            : pushHint();
    }

    pushNew(list);
  }

  /** نوشتهٔ «برنامهٔ بسته هم خبر می‌گیرد؟» — راستش را می‌گوید، نه امیدش را. */
  function pushHint() {
    if (!('serviceWorker' in navigator) || !('PushManager' in window))
      return 'این مرورگر پوش ندارد؛ روی آیفون اپ را با «افزودن به صفحهٔ اصلی» نصب کنید و از همان‌جا باز کنید.';
    if (pushState === 'ok') return '✅ خبرها حتی وقتی اپ بسته باشد هم روی همین گوشی می‌آیند.';
    if (pushState === 'fail') return '⚠️ اعلان روشن است ولی ثبتِ خبرِ پس‌زمینه نشد — به سرورِ پمپ نرسیدیم؛ خودش دوباره امتحان می‌کند.';
    return 'اعلان روشن است؛ ثبتِ خبرِ پس‌زمینه در جریان است…';
  }

  function renderAlertHint() {
    var hint = $('alertHint');
    if (hint && typeof Notification !== 'undefined' && Notification.permission === 'granted' && !hasBackground())
      hint.textContent = pushHint();
  }

  /** روی اپِ اندروید، کارِ پس‌زمینه هست؛ در مرورگر و آیفون نیست. */
  function hasBackground() {
    try { return !!(window.PumpAlerts && window.PumpAlerts.available()); } catch (e) { return false; }
  }

  /**
   * تنظیماتِ سرور را به لایهٔ اندروید می‌دهد تا وقتی اپ بسته است هم بپرسد.
   *
   * ⚠️ هر بار که تنظیمات عوض می‌شود دوباره صدا زده می‌شود — نه فقط یک‌بار سرِ
   * بالا آمدن: کاربری که کیو‌آرِ پمپِ دیگری را اسکن کند، وگرنه تا نصبِ دوباره
   * خبرِ پمپِ قبلی را می‌گرفت.
   */
  function syncBackground() {
    try {
      if (window.PumpAlerts && window.PumpAlerts.setup)
        window.PumpAlerts.setup(cfg.srv || '', cfg.tok || '', cfg.stn || '');
    } catch (e) { }
    registerPush(false);
  }

  /** روی این دستگاه پوش شدنی است؟ (اندروید کارِ پس‌زمینهٔ خودش را دارد) */
  function pushCan() {
    return typeof navigator !== 'undefined' && 'serviceWorker' in navigator
      && typeof window !== 'undefined' && 'PushManager' in window
      && typeof Notification !== 'undefined' && Notification.permission === 'granted'
      && !hasBackground() && !!cfg.tok && !!cfg.stn;
  }

  var pushBusy = false;
  var pushState = '';          // برای نوشتهٔ زیرِ کادرِ خبرها

  /**
   * ثبتِ همین گوشی برای پوشِ خبرهای همین پمپ.
   *
   * ⚠️ روزی یک بار بس است (‎stnKey('push')‎)؛ هر بار که تنظیمات عوض شود صدا
   * زده می‌شود، و بی این ترمز هر عوض شدنِ صفحه یک درخواست می‌زد.
   */
  function registerPush(force) {
    if (!pushCan() || pushBusy) return Promise.resolve(false);
    var mark = '';
    try { mark = localStorage.getItem(stnKey('push')) || ''; } catch (e) { }
    var parts = mark.split('|');
    if (!force && parts[1] === cfg.tok && Date.now() - Number(parts[0] || 0) < 24 * 3600 * 1000) {
      pushState = 'ok';
      return Promise.resolve(true);
    }
    pushBusy = true;
    var stn = cfg.stn, tok = cfg.tok;
    var bases = pushBases(cfg);
    return navigator.serviceWorker.ready.then(function (reg) {
      var i = 0;
      function next() {
        if (i >= bases.length) return false;
        var base = bases[i++];
        var url = base + '/api/stations/' + encodeURIComponent(stn) + '/push?token=' + encodeURIComponent(tok);
        return fetch(url, { cache: 'no-store' }).then(function (r) {
          if (!r.ok) throw new Error('http ' + r.status);
          return r.json();
        }).then(function (j) {
          if (!j || !j.vapidPublicKey) throw new Error('no key');
          return reg.pushManager.getSubscription().then(function (old) {
            return old || reg.pushManager.subscribe({
              userVisibleOnly: true, applicationServerKey: b64uBytes(j.vapidPublicKey)
            });
          });
        }).then(function (sub) {
          return fetch(url, {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ subscription: sub.toJSON ? sub.toJSON() : sub, label: 'اپِ کارمندان' })
          }).then(function (r) {
            if (!r.ok) throw new Error('http ' + r.status);
            try { localStorage.setItem(stnKey('push'), Date.now() + '|' + tok); } catch (e) { }
            pushState = 'ok';
            return true;
          });
        }).catch(function () { return next(); });
      }
      return next();
    }).then(function (ok) {
      if (!ok) pushState = 'fail';
      return ok;
    }).catch(function () { pushState = 'fail'; return false; })
      .then(function (ok) { pushBusy = false; try { renderAlertHint(); } catch (e) { } return ok; });
  }

  /** اشتراکِ پوشِ پمپِ قبلی را باطل می‌کند — بهترین تلاش، بی‌صدا. */
  function dropPush(old) {
    try {
      if (typeof navigator === 'undefined' || !('serviceWorker' in navigator)) return;
      var stn = old.stn, tok = old.tok, bases = pushBases(old);
      navigator.serviceWorker.ready.then(function (reg) {
        return reg.pushManager.getSubscription();
      }).then(function (sub) {
        if (!sub) return;
        var ep = sub.endpoint;
        bases.forEach(function (base) {
          try {
            fetch(base + '/api/stations/' + encodeURIComponent(stn) + '/push?token=' + encodeURIComponent(tok)
              + '&endpoint=' + encodeURIComponent(ep), { method: 'DELETE' }).catch(function () { });
          } catch (e) { }
        });
        return sub.unsubscribe();
      }).catch(function () { });
    } catch (e) { }
  }

  function render() {
    if (!unlocked) return;
    if (data && data.station && data.station.name) $('stName').textContent = data.station.name;
    chips();
    lockButton();
    renderAlerts();
    renderTank();
    renderStaff();
    //  ⛔ دادهٔ «حساب‌ها» فقط وقتی به صفحه می‌رسد که همان در باز باشد — حتی
    //  در صفحهٔ پنهان هم نه، تا کارمند با ابزارِ مرورگر هم نبیند.
    if (mode === 'owner' && ownerOk()) {
      renderDebtors();
      renderSections();
      renderDash();
    } else clearOwnerPanes();
  }

  /** همهٔ نوشته‌های درِ «حساب‌ها» پاک — پس از 🔒 یا در درِ کارمندان. */
  function clearOwnerPanes() {
    ['bannerBox', 'dashTank', 'dashTiles', 'debtList', 'secTabs', 'secBox'].forEach(function (id) {
      var el = $(id); if (el) el.innerHTML = '';
    });
    var bo = $('botOut'); if (bo && mode !== 'owner') bo.innerHTML = '';
  }

  /** دکمهٔ تم همیشه تمِ **دیگر** را نشان می‌دهد. */
  function themeIcon() {
    var b = $('btnTheme');
    if (!b) return;
    var cur = document.documentElement.getAttribute('data-theme')
      || (window.matchMedia && matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
    b.textContent = cur === 'dark' ? '☀️' : '🌙';
    //  نوارِ وضعیتِ گوشی هم‌رنگِ سربرگ
    var m = document.querySelector('meta[name="theme-color"]');
    if (m) m.setAttribute('content', cur === 'dark' ? '#1A1A1F' : '#ffffff');
    try { if (window.PumpAndroid && PumpAndroid.bars) PumpAndroid.bars(cur === 'dark' ? '#1A1A1F' : '#ffffff', cur !== 'dark'); } catch (e) { }
  }

  /**
   *  ربات در درِ کارمندان فقط همان چیزی را می‌بیند که آن در نشان می‌دهد:
   *  حالِ هر قرض‌دار (بی دفترِ حساب)، و مخزن. هیچ بخشِ دیگری.
   */
  function staffView(d) {
    if (!d) return d;
    return {
      station: d.station, tank: d.tank, alerts: d.alerts, seq: d.seq,
      debtors: (d.debtors || []).map(function (p) {
        return {
          id: p.id, name: p.name, status: p.status, bal: p.bal, phone: p.phone,
          stP: p.stP, stD: p.stD, stM: p.stM, use: p.use,
          //  فقط سربرگِ حساب: عنوان، واحد، حال و بردگی/رسید/الباقیِ هر تیل —
          //  ⛔ نه فیصدیِ پمپ، نه ردیف‌ها، نه جمع‌های دفتر.
          accounts: (p.accounts || []).map(function (a) {
            return {
              title: a.title, unit: a.unit, st: a.st,
              fuels: (a.fuels || []).map(function (f) { return [String(f[0]).replace(/\s*\(.*\)\s*$/, ''), f[1], f[2], '', f[4]]; })
            };
          })
        };
      }),
      sections: {}
    };
  }

  // ══════════════════════════════════════════════════════════════════════
  //  سیم‌کشیِ دکمه‌ها
  // ══════════════════════════════════════════════════════════════════════

  function ask() {
    var q = $('inAsk').value;
    var blocks = answer(q, mode === 'owner' && ownerOk() ? data : staffView(data));
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

    // ── کدِ پمپ ──
    $('btnJoin').addEventListener('click', function () { joinWithCode($('inCode').value); });
    $('inCode').addEventListener('keydown', function (e) { if (e.key === 'Enter') joinWithCode($('inCode').value); });
    $('inCode').addEventListener('input', function () {
      //  همان‌طور که تایپ می‌کند خطِ تیره می‌نشیند: ‎4829-1736‎
      var el = $('inCode'), c = (window.PumpCloud ? PumpCloud.normalizeCode(el.value) : el.value).slice(0, 8);
      el.value = c.length > 4 ? c.slice(0, 4) + '-' + c.slice(4) : c;
      codeMsg('');
    });
    $('btnGoogleWay').addEventListener('click', function () { show('signinPane'); setupGoogle(); });
    $('btnBackCode').addEventListener('click', function () { show('codePane'); });
    $('btnPassIn').addEventListener('click', signInWithPassword);
    $('btnForgot').addEventListener('click', forgotPassword);
    $('btnReset').addEventListener('click', resetPassword);
    //  Enter در هر دو کادر همان دکمه را می‌زند
    ['inEmail', 'inPw'].forEach(function (id) {
      var el = $(id);
      if (el) el.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') signInWithPassword();
      });
    });

    $('btnSetup').addEventListener('click', function () {
      var srv = $('inSrv').value.trim();
      var stn = $('inStn').value.trim();
      if (!srv) return;
      //  ⛔ بی کدِ پمپ به هیچ پمپی وصل نمی‌شویم — نه به «pump1»ِ کسِ دیگر
      if (!stn) { try { $('inStn').focus(); } catch (e) { } return; }
      switchStation(stn);
      cfg.srv = srv;
      cfg.tok = $('inTok').value.trim();
      cfg.code = '';
      save();
      chips();
      syncBackground();
      openApp();
      connect();
    });

    $('btnForget').addEventListener('click', function () {
      //  «پمپِ دیگر»: هر چه از این پمپ در گوشی مانده پاک می‌شود و از کد
      //  شروع می‌کنیم — نشانی چیزی است که سرور می‌دهد، نه چیزی که کارمند
      //  بنویسد.
      askLeave();
    });

    //  همان کار از خودِ برنامه — پمپِ بی‌رمز هیچ‌وقت صفحهٔ قفل را نمی‌بیند
    //  ⛔ پیش از بیرون رفتن می‌پرسد (صاحب ریپو، ۱۴۰۵/۰۷/۱۶: «می‌زنم بیرون
    //  می‌شود و تایید نمی‌آید»). بیرون رفتن کدِ پمپ و هر چه از آن در گوشی
    //  مانده را پاک می‌کند و برگشتن یعنی زدنِ دوبارهٔ کد.
    $('btnOther').addEventListener('click', askLeave);

    $('btnManual').addEventListener('click', function () {
      $('inSrv').value = cfg.srv; $('inTok').value = cfg.tok; $('inStn').value = cfg.stn || '';
      show('setupPane');
    });

    $('btnBackSignin').addEventListener('click', function () { show('codePane'); });

    $('btnUnlock').addEventListener('click', unlock);
    $('inPass').addEventListener('keydown', function (e) { if (e.key === 'Enter') unlock(); });
    $('btnLock').addEventListener('click', function () {
      //  🔒 = «حساب‌ها» دوباره قفل.
      //  ⛔ کارمندان قفل نمی‌شوند — از همان صفحه یک دکمه تا آن‌جاست.
      ownerPassed = '';
      clearOwnerPanes();
      mode = '';
      try { localStorage.removeItem(stnKey('mode')); } catch (e) { }
      buildNav();
      askOwner();
    });
    $('btnLockBack').addEventListener('click', function () {
      pendingOwner = false;
      show('appPane');
      setMode('staff');
    });
    $('btnTheme').addEventListener('click', function () {
      var cur = document.documentElement.getAttribute('data-theme')
        || (window.matchMedia && matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
      var next = cur === 'dark' ? 'light' : 'dark';
      document.documentElement.setAttribute('data-theme', next);
      try { localStorage.setItem('pumpKar.theme', next); } catch (e) { }
      themeIcon();
    });
    themeIcon();

    // ── دو در ──
    $('paneHome').addEventListener('click', function (e) {
      var b = e.target.closest('button[data-mode]');
      if (b) setMode(b.getAttribute('data-mode'));
    });
    $('modeSeg').addEventListener('click', function (e) {
      var b = e.target.closest('button[data-mode]');
      if (b) setMode(b.getAttribute('data-mode'));
    });
    $('dashTiles').addEventListener('click', function (e) {
      var b = e.target.closest('button[data-sec]');
      if (!b) return;
      secTab = b.getAttribute('data-sec');
      $('selMonth').value = '';
      renderSections();
      goPane('paneSec');
    });
    $('inStaffFind').addEventListener('input', renderStaff);
    $('staffCounts').addEventListener('click', function (e) {
      var b = e.target.closest('button[data-f]');
      if (!b) return;
      staffFilter = b.getAttribute('data-f');
      renderStaff();
    });
    $('staffList').addEventListener('click', function (e) {
      var b = e.target.closest('button[data-pid]');
      if (!b) return;
      var id = b.getAttribute('data-pid');
      var p = ((data && data.debtors) || []).filter(function (x) { return String(x.id) === id; })[0];
      if (!p) return;
      //  کارمند حسابِ کامل را نمی‌خواهد؛ فقط حال و الباقی — همان بلوکِ ربات
      //  ⛔ همان سربرگِ مربعیِ هر حساب، ولی بی فیصدیِ پمپ و بی جدول (‎staff‎)
      var blk = personBlock(p); blk.staff = true;
      $('botOut').innerHTML = '<button class="back" id="btnBackStaff">› برگرد به فهرست</button>' + blockHtml(blk);
      goPane('paneBot');
      $('paneBot').classList.add('solo');   // فقط همین شخص، بی کادرِ پرسش
      navMark('paneStaff');
      $('btnBackStaff').addEventListener('click', function () { goPane('paneStaff'); });
    });

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
      $('botOut').innerHTML = '<button class="back" id="btnBackDebt">› برگرد به قرض‌داران</button>' + blockHtml(personBlock(p));
      goPane('paneBot');
      $('paneBot').classList.add('solo');
      $('btnBackDebt').addEventListener('click', function () { goPane('paneDebt'); });
      navMark('paneDebt');
    });

    //  🖨 یک شنونده برای همهٔ دکمه‌های چاپ — کارت‌ها با هر عکس از نو ساخته می‌شوند
    document.addEventListener('click', function (e) {
      var bt = e.target.closest && e.target.closest('[data-book]');
      if (bt) {
        var kk = String(bt.getAttribute('data-book')).split('|');
        var rk = kk[0] + '|' + kk[1], reg = acctReg[rk], card = bt.closest('.acard');
        bookSel[rk] = kk[2];
        //  ⚠️ درِ کارمندان فیصدی و جدول نمی‌بیند — همان کارت با همان در کشیده می‌شود
        if (reg && card) card.outerHTML = acctCard(reg.a, card.getAttribute('data-staff') === '1', reg.detail);
        return;
      }
      var b = e.target.closest && e.target.closest('[data-print-acct],[data-print-sec]');
      if (!b || !data) return;
      var sid = b.getAttribute('data-print-sec');
      if (sid) { if (data.sections && data.sections[sid]) runPrint(sectionDoc(data.sections[sid], $('selMonth').value || null)); return; }
      var k = String(b.getAttribute('data-print-acct')).split('|');
      var p = (data.debtors || []).filter(function (x) { return String(x.id) === k[0]; })[0];
      var a = p && (p.accounts || [])[Number(k[1])];
      if (a) runPrint(acctDoc(p, a));
    });

    var lr = $('liveRow');
    if (lr) lr.addEventListener('click', function () { lr.classList.toggle('open'); });

    $('nav').addEventListener('click', function (e) {
      var b = e.target.closest('button[data-pane]');
      if (b) goPane(b.getAttribute('data-pane'));
    });

    //  ⛔ اپِ گوشی پیام‌رسان ندارد (خواستهٔ صاحب ریپو، ۱۴۰۵/۰۷/۱۶): پیام‌رسان فقط در
    //  برنامهٔ کامپیوتر و صفحهٔ کیو‌آر است. ردِ گروهِ نسخه‌های پیشین از گوشی پاک می‌شود.
    try {
      Object.keys(localStorage).forEach(function (k) {
        if (k.indexOf(KEY + '.chat') === 0) localStorage.removeItem(k);
        //  رمزِ «حساب‌ها» دیگر در گوشی به یاد نمی‌ماند — یادِ نسخه‌های پیشین هم برود
        if (k.indexOf(KEY + '.ok.') === 0) localStorage.removeItem(k);
      });
      if (typeof indexedDB !== 'undefined' && indexedDB.deleteDatabase) indexedDB.deleteDatabase('pump-kar-media');
    } catch (e) { }

    $('btnNotify').addEventListener('click', function () {
      if (typeof Notification === 'undefined') return;
      // ⚠️ درخواستِ اجازه باید از دلِ یک کلیکِ واقعی بیاید، وگرنه مرورگر
      // بی‌صدا ردش می‌کند و کاربر فکر می‌کند خراب است.
      try {
        Notification.requestPermission().then(function () { renderAlerts(); registerPush(true); });
      } catch (e) { }
      try { if (window.PumpAlerts && window.PumpAlerts.checkNow) window.PumpAlerts.checkNow(); } catch (e) { }
    });

    setupMic();
    syncBackground();

    /*
     *  کدام صفحه اول باز شود.
     *
     *  ۱) کیو‌آر/لینک نشانی داده ⇒ همان، بی معطلی. کسی که کیو‌آر را اسکن
     *     کرده عمداً این راه را خواسته.
     *  ۲) وگرنه اگر از قبل نشانی داریم ⇒ قفل، و در پس‌زمینه از ابر
     *     می‌پرسیم که نشانی عوض نشده باشد (آی‌پیِ خانگی عوض می‌شود).
     *  ۳) وگرنه ⇒ ورود با گوگل. **هیچ فرمی نشان داده نمی‌شود.**
     */
    chips();
    if (pendingCode) {
      //  کیو‌آرِ کدِ پمپ اسکن شده ⇒ صفحهٔ کد با کدِ پُرشده، و همان لحظه برو
      show('codePane');
      $('inCode').value = window.PumpCloud ? PumpCloud.formatCode(pendingCode) : pendingCode;
      joinWithCode(pendingCode);
    } else if (cfg.srv || cfg.code) {
      openApp();
      //  درِ شبکهٔ پمپ، درِ تونل، و عکسِ سرورِ حساب — هر سه هم‌زمان
      connect();
      //  نشانیِ تازه‌تر، اگر ابر یکی دارد — بی‌صدا، چون کارِ کارمند نباید
      //  منتظرِ اینترنت بماند. اول با کدِ پمپ، وگرنه با نشستِ گوگل.
      (cfg.code ? resumeCode() : resumeCloud()).then(function (moved) {
        if (moved) connect();
      });
    } else {
      //  ⚠️ صفحهٔ اول **کدِ پمپ** است — نه فرمِ نشانی، نه گوگل. خواستهٔ صریحِ
      //  صاحب ریپو: «هر کسی که برنامه را نصب می‌کند باید آن کد را بزند.»
      show('codePane');
      gateReady();
      //  شاید نشستِ گوگلی از قبل هست (صاحبِ پمپ) و فقط نشانی پاک شده
      resumeCloud().then(function (ok) {
        if (!ok) return;
        openApp();
        connect();
      });
    }

    if ('serviceWorker' in navigator)
      navigator.serviceWorker.register('./sw.js').catch(function () { });

    //  ⚠️ پردهٔ لودینگِ اندروید فقط با همین برداشته می‌شود (وگرنه ۳۰ ثانیه
    //  می‌ماند). تا پیش از این هیچ‌جا صدا زده نمی‌شد.
    try { if (window.PumpAndroid && PumpAndroid.boot) PumpAndroid.boot('ready'); } catch (e) { }
  }

  /** کدام دکمهٔ نوارِ پایین روشن باشد — صفحهٔ یک شخص زیرِ فهرستِ خودش است. */
  function navMark(id) {
    Array.prototype.forEach.call($('nav').querySelectorAll('button'), function (b) {
      b.setAttribute('aria-selected', String(b.getAttribute('data-pane') === id));
    });
  }

  function goPane(id) {
    ALL_PANES.forEach(function (p) {
      var el = $(p);
      if (el) el.classList.toggle('hidden', p !== id);
    });
    var pb = $('paneBot');
    if (pb) pb.classList.remove('solo');
    //  خبرها فقط جایی که به کار می‌آیند: داشبورد، چراغِ کارمندان و مخزن.
    //  روی خانه، گروه، ربات و بخش‌ها نه — آن‌جا فقط جلوی کار را می‌گرفتند.
    var ac = $('alertCard');
    curPane = id;
    if (ac && ALERT_PANES.indexOf(id) < 0) ac.classList.add('hidden');
    else if (ac) renderAlerts();
    navMark(id);
    window.scrollTo(0, 0);
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();
})();
