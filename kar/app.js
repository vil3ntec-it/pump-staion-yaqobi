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
    var code = String((c && c.stn) || 'pump1').trim() || 'pump1';
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
      answer: answer, askedMonth: askedMonth, monthHit: monthHit,
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
  function stnKey(suffix) { return KEY + '.' + suffix + '.' + (cfg.stn || 'pump1'); }

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
  }

  function save() { try { localStorage.setItem(KEY, JSON.stringify(cfg)); } catch (e) { } }

  /** رفتن به پمپِ دیگر: هر چه از پمپِ قبلی در گوشی مانده پاک می‌شود. */
  function switchStation(stn) {
    var next = String(stn || 'pump1');
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
      }
      localStorage.removeItem(KEY + '.snap');      // کلیدِ قدیمیِ بی‌پمپ
      localStorage.removeItem(KEY + '.told');
    } catch (e) { }
    cfg.stn = next;
    data = null;
    toldKeys = {};
    unlocked = false;
    fromCloud = false;
    mode = '';
  }

  /** فقط عکسِ همین پمپ پذیرفته می‌شود — چه از سرورِ خانگی چه از ابر. */
  function acceptSnapshot(v, viaCloud) {
    if (!v || typeof v !== 'object') return false;
    // ⚠️ عکسِ کهنه هرگز جای تازه را نگیرد
    if (data && data.seq && (v.seq || 0) < data.seq) return false;
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
  var ALL_PANES = ['paneHome', 'paneDash', 'paneStaff', 'paneBot', 'paneDebt', 'paneTank', 'paneSec'];

  function loadMode() {
    try { mode = localStorage.getItem(stnKey('mode')) || ''; } catch (e) { mode = ''; }
    if (mode !== 'owner' && mode !== 'staff') mode = '';
  }

  function setMode(m) {
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
    if (!mode) { goPane('paneHome'); return; }
    goPane(NAVS[mode][0][0]);
    render();
  }

  function buildNav() {
    var nav = $('nav');
    if (!nav) return;
    nav.innerHTML = (NAVS[mode] || []).map(function (n) {
      return '<button data-pane="' + n[0] + '">' + n[1] + '</button>';
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

  var door = 0;

  /** نشانیِ همان دری که الان امتحان می‌شود. */
  function wsUrl() { return doorsFor(cfg)[door % 2].url; }
  function livePath() { return doorsFor(cfg)[door % 2].path; }

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
      ws.send(JSON.stringify({ op: 'sub', subId: 'live', event: 'value', path: livePath() }));
    };
    ws.onmessage = function (ev) {
      var m;
      try { m = JSON.parse(ev.data); } catch (e) { return; }
      if (m.op === 'connected') {
        // این در جواب داد — تا وقتی کار می‌کند همین بماند
        retry = 0;
        return;
      }
      if (m.op === 'error') {
        // ⚠️ شاید فقط این در نبود، نه این‌که رمز غلط باشد: سرورِ قدیمی
        // ‎/station‎ ندارد و سرورِ تازه پمپِ ناشناس را نمی‌شناسد. درِ بعدی
        // را امتحان کن و تنها وقتی «رمز غلط» بگو که هر دو رد کرده باشند.
        door++;
        live(false, door % 2 === 0
          ? 'رمزِ سرور پذیرفته نشد — کیو‌آرِ تازه بگیرید'
          : 'در حالِ امتحانِ راهِ دیگر…');
        try { ws.close(); } catch (e) { }
        return;
      }
      if (m.op === 'event' && m.subId === 'live') {
        if (m.value && typeof m.value === 'object') {
          acceptSnapshot(m.value, false);
          live(true, 'زنده — تازه‌سازی ' + ((data && data.at) || ''));
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
    //  دو بار پشتِ سرِ هم نشد ⇒ عکسِ ابری، تا کارمندِ دور از پمپ دستِ خالی
    //  نماند. ‎connect()‎ همچنان تلاش می‌کند و همین که سرورِ خانگی جواب داد،
    //  عکسِ زنده جای ابری می‌نشیند (‎seq‎ی بزرگ‌تر).
    if (retry >= 2) cloudFallback();
  }

  var cloudBusy = false, cloudLastAt = 0;

  /**
   * عکسِ ده‌دقیقه‌ایِ ابر — فقط با کدِ پمپ، فقط همان پمپ.
   * از هر ۶۰ ثانیه بیشتر نمی‌پرسد؛ برنامهٔ کامپیوتر خودش هر ده دقیقه یک بار
   * به ابر می‌فرستد، پس تندتر پرسیدن چیزی نمی‌آورد.
   */
  function cloudFallback() {
    if (!cfg.code || !window.PumpCloud || cloudBusy) return;
    if (Date.now() - cloudLastAt < 60000) return;
    cloudBusy = true;
    PumpCloud.cloudLive(cfg.code).then(function (r) {
      cloudLastAt = Date.now();
      if (!r || !r.live) return;
      if (acceptSnapshot(r.live, true) || fromCloud) {
        var when = r.updatedAt ? new Date(r.updatedAt).toLocaleString('fa-IR') : '';
        live(true, 'از ابر — سرورِ پمپ در دسترس نیست · ' + when);
        gateReady();
      }
    }).catch(function () { cloudLastAt = Date.now(); })
      .then(function () { cloudBusy = false; });
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
    switchStation(home.station || st.code || 'pump1');
    cfg.srv = home.url;
    //  رمزِ فقط‌خواندنی، نه رمزِ برنامه — این همان چیزی است که روی کاغذِ
    //  کیو‌آر می‌رفت، فقط این‌بار از راهِ رمزگذاری‌شده
    cfg.tok = home.readKey || '';
    if (st.accessCode !== undefined) cfg.code = st.accessCode || '';
    cfg.name = st.name || cfg.name || '';
    save();
    door = 0;
    retry = 0;
    chips();
    return true;
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
    if (code.length !== 8) { codeMsg('کد هشت حرف و رقم است — مثلِ K7PM-3XQ2.', true); return; }
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
          cfg.srv = ''; cfg.tok = ''; cfg.code = code; cfg.name = st.name || '';
          save();
          chips();
          syncBackground();
          show('lockPane');
          live(false, 'سرورِ پمپ نشانی ندارد — عکسِ ابری');
          cloudLastAt = 0;
          cloudFallback();
          return;
        }
        adoptStation(st);
        codeMsg('');
        $('inCode').value = '';
        syncBackground();
        show('lockPane');
        gateReady();
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
    clearTimeout(timer);
    if (window.PumpCloud) PumpCloud.signOut();
    switchStation('');
    cfg.srv = ''; cfg.tok = ''; cfg.code = ''; cfg.name = ''; cfg.stn = '';
    save();
    try { localStorage.removeItem(KEY + '.snap.pump1'); localStorage.removeItem(KEY + '.told.pump1'); } catch (e) { }
    syncBackground();
    show('codePane');
    if (note) codeMsg(note, true);
  }

  /** نشستی که از قبل هست — بی آنکه دوباره از گوگل چیزی خواسته شود. */
  function resumeCloud() {
    if (!window.PumpCloud || !PumpCloud.signedIn()) return Promise.resolve(false);
    return PumpCloud.myStation().then(function (st) {
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
        if (!adoptStation(st)) return;
        syncBackground();
        show('lockPane');
        connect();
      })
      .catch(function (err) {
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
        //  ⚠️ رمز در حافظهٔ صفحه هم نمی‌ماند
        if ($('inPw')) $('inPw').value = '';
        if (!adoptStation(st)) return;
        syncBackground();
        show('lockPane');
        connect();
      })
      .catch(function (err) {
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
        if (!adoptStation(st)) return;
        syncBackground();
        show('lockPane');
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

  function gateReady() {
    var b = $('btnUnlock');
    if (b) b.disabled = !(data && data.gate);
    if (data && data.station && data.station.name) {
      cfg.name = data.station.name;
      $('lockTitle').textContent = data.station.name;
      $('stName').textContent = data.station.name;
    }
    chips();
    if (data && !data.gate)
      $('lockNote').textContent = 'برنامهٔ کامپیوتر هنوز رمزی نساخته است.';
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
      unlocked = true;
      $('inPass').value = '';
      show('appPane');
      loadMode();
      setMode(mode);          // بی در ⇒ خانه؛ با در ⇒ همان در
      render();
      try { if (window.PumpAndroid && PumpAndroid.boot) PumpAndroid.boot('render'); } catch (e) { }
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

  /** چهار عددِ نوارِ بالای برنامهٔ کامپیوتر + مخزن + کاشیِ بخش‌ها — درِ «حساب‌ها». */
  function renderDash() {
    var box = $('bannerBox');
    if (!box) return;
    var bn = (data && data.banner) || [];
    box.innerHTML = bn.length
      ? bn.map(function (b) {
          return '<div class="bn ' + esc(b[2] || '') + '"><div class="l">' + esc(b[0]) + '</div>' +
            '<div class="v">' + esc(b[1]) + ' <span class="l">افغانی</span></div></div>';
        }).join('')
      : '<div class="sub">برنامهٔ کامپیوتر باید به نسخهٔ تازه برسد تا این چهار عدد بیاید.</div>';
    $('dashTank').innerHTML = tankFigs(true);
    var secs = (data && data.sections) || {};
    $('dashTiles').innerHTML = Object.keys(secs).map(function (id) {
      var sec = secs[id], first = (sec.sum || [])[0];
      return '<button class="tile" data-sec="' + esc(id) + '"><b>' + esc(sec.t) + '</b>' +
        '<span class="sub">' + fmt((sec.rows || []).length) + ' ردیف' +
        (first ? ' · ' + esc(first[0]) + ' ' + esc(first[1]) : '') + '</span></button>';
    }).join('') || '<div class="sub">هنوز بخشی نرسیده.</div>';
  }

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
          f[1] + ' (' + fmt(c[f[0]] || 0) + ')</button>';
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
    list.innerHTML = rows.map(function (p) {
      var b = p.bal || {};
      var what = p.status === 'out' ? 'تمام شده — تیل ندهید'
        : p.status === 'low' ? 'کم مانده'
        : p.status === 'ok' ? 'موجودی دارد' : 'ردیفی ندارد';
      var bal = [b.petrol ? 'پطرول ' + fmt(b.petrol) : '', b.diesel ? 'دیزل ' + fmt(b.diesel) : '',
        b.money ? 'پول ' + fmt(b.money) : ''].filter(Boolean).join(' · ');
      return '<button class="st-row ' + esc(p.status) + '" data-pid="' + esc(p.id) + '">' +
        '<span class="light"></span><span class="n">' + esc(p.name) + '</span>' +
        '<span class="b">' + esc(what) + (bal ? ' · ' + esc(bal) : '') + '</span></button>';
    }).join('') || '<div class="sub">کسی پیدا نشد.</div>';
  }

  function tankFigs(compact) {
    var t = (data && data.tank) || {}, h = '';
    [['petrol', 'پطرول'], ['diesel', 'دیزل']].forEach(function (p) {
      var x = t[p[0]] || {};
      var cls = x.low ? ' bad' : (x.near ? ' warn' : '');
      h += '<div class="fig' + cls + '"><div class="l">' + p[1] + ' — موجودی</div>' +
        '<div class="v">' + fmt(x.show) + '</div><div class="l">لیتر' +
        (x.low ? ' · کم آمده' : (x.near ? ' · نزدیکِ حد' : '')) + '</div></div>';
      if (!compact) {
        h += '<div class="fig"><div class="l">' + p[1] + ' — وارد</div><div class="v">' + fmt(x['in']) + '</div></div>';
        h += '<div class="fig"><div class="l">' + p[1] + ' — فروش</div><div class="v">' + fmt(x.out) + '</div></div>';
      }
    });
    return h || '<div class="sub">داده‌ای نیست.</div>';
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
  try { toldKeys = JSON.parse(localStorage.getItem(TOLD()) || '{}') || {}; } catch (e) { }

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

    card.classList.toggle('hidden', list.length === 0);
    if (list.length) {
      box.innerHTML = list.map(function (a) {
        return '<div class="al ' + (a.s === 'out' ? 'out' : 'low') + '">' + esc(a.t || '') + '</div>';
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
        window.PumpAlerts.setup(cfg.srv || '', cfg.tok || '', cfg.stn || 'pump1');
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
    renderAlerts();
    renderTank();
    renderDebtors();
    renderSections();
    renderDash();
    renderStaff();
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

    // ── کدِ پمپ ──
    $('btnJoin').addEventListener('click', function () { joinWithCode($('inCode').value); });
    $('inCode').addEventListener('keydown', function (e) { if (e.key === 'Enter') joinWithCode($('inCode').value); });
    $('inCode').addEventListener('input', function () {
      //  همان‌طور که تایپ می‌کند خطِ تیره می‌نشیند: ‎K7PM-3XQ2‎
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
      if (!srv) return;
      switchStation($('inStn').value.trim() || 'pump1');
      cfg.srv = srv;
      cfg.tok = $('inTok').value.trim();
      cfg.code = '';
      save();
      chips();
      syncBackground();
      show('lockPane');
      connect();
    });

    $('btnForget').addEventListener('click', function () {
      //  «پمپِ دیگر»: هر چه از این پمپ در گوشی مانده پاک می‌شود و از کد
      //  شروع می‌کنیم — نشانی چیزی است که سرور می‌دهد، نه چیزی که کارمند
      //  بنویسد.
      forgetAll('');
    });

    $('btnManual').addEventListener('click', function () {
      $('inSrv').value = cfg.srv; $('inTok').value = cfg.tok; $('inStn').value = cfg.stn || 'pump1';
      show('setupPane');
    });

    $('btnBackSignin').addEventListener('click', function () { show('codePane'); });

    $('btnUnlock').addEventListener('click', unlock);
    $('inPass').addEventListener('keydown', function (e) { if (e.key === 'Enter') unlock(); });
    $('btnLock').addEventListener('click', function () { unlocked = false; buildNav(); show('lockPane'); });

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
      var blk = personBlock(p); blk.person = null;
      $('botOut').innerHTML = blockHtml(blk) +
        '<button class="back" id="btnBackStaff">‹ برگرد به فهرست</button>';
      goPane('paneBot');
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
      $('botOut').innerHTML = blockHtml(personBlock(p));
      goPane('paneBot');
    });

    $('nav').addEventListener('click', function (e) {
      var b = e.target.closest('button[data-pane]');
      if (b) goPane(b.getAttribute('data-pane'));
    });

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
      show('lockPane');
      gateReady();
      if (cfg.srv) connect(); else { live(false, 'سرورِ پمپ نشانی ندارد — عکسِ ابری'); cloudFallback(); }
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
        show('lockPane');
        connect();
      });
    }

    if ('serviceWorker' in navigator)
      navigator.serviceWorker.register('./sw.js').catch(function () { });

    //  ⚠️ پردهٔ لودینگِ اندروید فقط با همین برداشته می‌شود (وگرنه ۳۰ ثانیه
    //  می‌ماند). تا پیش از این هیچ‌جا صدا زده نمی‌شد.
    try { if (window.PumpAndroid && PumpAndroid.boot) PumpAndroid.boot('ready'); } catch (e) { }
  }

  function goPane(id) {
    ALL_PANES.forEach(function (p) {
      var el = $(p);
      if (el) el.classList.toggle('hidden', p !== id);
    });
    //  خبرها روی صفحهٔ خانه نه — آن‌جا فقط دو در
    var ac = $('alertCard');
    if (ac && id === 'paneHome') ac.classList.add('hidden');
    else if (ac) renderAlerts();
    Array.prototype.forEach.call($('nav').querySelectorAll('button'), function (b) {
      b.setAttribute('aria-selected', String(b.getAttribute('data-pane') === id));
    });
    window.scrollTo(0, 0);
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();
})();
