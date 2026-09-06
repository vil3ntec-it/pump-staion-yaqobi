// ---------------------------------------------------------------------------
//  ══ اندازه‌گیرِ سایت ═══════════════════════════════════════════════════════
//
//      PW_CHROMIUM=/opt/pw-browsers/chromium/chrome-linux/chrome \
//      node native/tools/measure-site.mjs [out.json]
//
//  index.html را در یک کرومیومِ واقعی و پنجرهٔ ۱۴۴۰ پیکسلی باز می‌کند و
//  اندازهٔ *محاسبه‌شدهٔ* هر عنصری را می‌خواند که در نسخهٔ نیتیو همتا دارد:
//  سربرگ، نوارِ خبر، ناوبری، کارت، کادرِ خلاصه و جدول‌ها.
//
//  چرا لازم است: خواندنِ خودِ CSS جواب نمی‌دهد. فایل چهار مگابایتی است و
//  همان قاعده‌ها چند بار بازنویسی می‌شوند — مثلاً ‎.card{border-radius:14px}‎
//  نوشته شده ولی مقدارِ واقعی ۱۸ است. تنها مرجعِ درست، خودِ مرورگر است.
//
//  خروجی‌اش پشتوانهٔ عددهای PumpYaqobi.Tests/SiteMetricsTests.cs است.
// ---------------------------------------------------------------------------
import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { chromium } from 'playwright-core';

const ROOT = path.join(import.meta.dirname, '..', '..');
const exe = process.env.PW_CHROMIUM;
const browser = await chromium.launch(exe ? { executablePath: exe, args: ['--no-sandbox'] }
                                          : { args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
page.on('pageerror', () => {});
await page.goto(pathToFileURL(path.join(ROOT, 'index.html')).href, { waitUntil: 'domcontentloaded' });
await page.waitForTimeout(3500);
await page.evaluate(() => {
  const ls = document.getElementById('lockScreen'); if (ls) ls.style.display = 'none';
  const app = document.getElementById('app'); if (app) app.style.display = '';
  try { window.currentRole = 'admin'; } catch (_) {}
});
await page.waitForTimeout(600);

const out = await page.evaluate(() => {
  const px = (v) => Math.round(parseFloat(v) * 100) / 100;
  const cs = (el) => (el ? getComputedStyle(el) : null);
  const box = (el) => {
    if (!el) return null;
    const r = el.getBoundingClientRect();
    return { w: Math.round(r.width), h: Math.round(r.height) };
  };
  const m = (sel, root = document) => {
    const el = root.querySelector(sel);
    if (!el) return { sel, missing: true };
    const s = cs(el);
    return {
      sel,
      ...box(el),
      font: px(s.fontSize),
      weight: s.fontWeight,
      pad: s.padding,
      radius: s.borderRadius,
      border: s.borderWidth + ' ' + s.borderStyle,
      lineHeight: s.lineHeight,
    };
  };

  const res = { chrome: {}, table: {}, cards: {} };

  // ── پوستهٔ بالای صفحه ──
  res.chrome.header = m('.header');
  res.chrome.headerTitle = m('.header-title');
  res.chrome.headerDate = m('.header-date');
  res.chrome.lockBtn = m('#lockBtn');
  res.chrome.roleBadge = m('.role-badge');
  res.chrome.banner = m('.top-banner');
  res.chrome.tbLabel = m('.tb-label');
  res.chrome.tbValue = m('.tb-value');
  res.chrome.nav = m('.nav');
  res.chrome.navBtn = m('.nav-btn');
  res.chrome.navBtnActive = m('.nav-btn.active');
  res.chrome.main = m('.main');

  // ── کارت ──
  res.cards.card = m('.card');
  res.cards.cardHead = m('.card-head');
  res.cards.cardTitle = m('.card-title');
  res.cards.statBox = m('.stat-box');
  res.cards.statLabel = m('.stat-label');
  res.cards.statValue = m('.stat-value');

  // ── جدول‌ها: هر جدولِ دیده‌شده در هر بخش ──
  const tables = [];
  document.querySelectorAll('table.tbl, table.xls-tbl').forEach((t) => {
    const sec = t.closest('.section');
    const th = t.querySelector('th');
    const td = t.querySelector('tbody td') || t.querySelector('td');
    const tr = t.querySelector('tbody tr') || t.querySelector('tr');
    const inp = t.querySelector('input, .xls-in');
    const ts = cs(t), ths = cs(th), tds = cs(td), trs = cs(tr);
    tables.push({
      section: sec ? sec.id : '(no section)',
      cls: t.className,
      cols: t.querySelectorAll('thead th').length || (tr ? tr.children.length : 0),
      tableFont: ts ? px(ts.fontSize) : null,
      th: th ? { font: px(ths.fontSize), weight: ths.fontWeight, pad: ths.padding, h: Math.round(th.getBoundingClientRect().height) } : null,
      td: td ? { font: px(tds.fontSize), pad: tds.padding, h: Math.round(td.getBoundingClientRect().height) } : null,
      rowH: tr ? Math.round(tr.getBoundingClientRect().height) : null,
      cellInput: inp ? { font: px(cs(inp).fontSize), pad: cs(inp).padding, h: Math.round(inp.getBoundingClientRect().height) } : null,
      borderCollapse: ts ? ts.borderCollapse : null,
    });
  });
  res.table.all = tables;

  // ریشهٔ اندازهٔ فونت
  res.rootFont = px(cs(document.documentElement).fontSize);
  res.bodyFont = px(cs(document.body).fontSize);
  return res;
});

// جدول‌ها فقط وقتی اندازه دارند که بخششان دیده شود — پس هر بخش را باز می‌کنیم
const perSection = [];
const navCount = await page.evaluate(() => document.querySelectorAll('.nav .nav-btn').length);
for (let i = 0; i < navCount; i++) {
  await page.evaluate((k) => document.querySelectorAll('.nav .nav-btn')[k].click(), i);
  await page.waitForTimeout(450);
  const r = await page.evaluate(() => {
    const px = (v) => Math.round(parseFloat(v) * 100) / 100;
    const sec = document.querySelector('.section.visible');
    if (!sec) return null;
    const t = sec.querySelector('table.tbl, table.xls-tbl');
    if (!t) return { section: sec.id, table: null, width: Math.round(sec.getBoundingClientRect().width) };
    const th = t.querySelector('th');
    const tr = t.querySelector('tbody tr') || t.querySelector('tr');
    const td = t.querySelector('tbody td');
    const cols = Array.from(t.querySelectorAll('thead th')).map((h) => ({
      t: (h.textContent || '').trim().slice(0, 14),
      w: Math.round(h.getBoundingClientRect().width),
    }));
    return {
      section: sec.id,
      sectionWidth: Math.round(sec.getBoundingClientRect().width),
      tableWidth: Math.round(t.getBoundingClientRect().width),
      tableFont: px(getComputedStyle(t).fontSize),
      thFont: th ? px(getComputedStyle(th).fontSize) : null,
      thPad: th ? getComputedStyle(th).padding : null,
      thH: th ? Math.round(th.getBoundingClientRect().height) : null,
      tdFont: td ? px(getComputedStyle(td).fontSize) : null,
      tdPad: td ? getComputedStyle(td).padding : null,
      rowH: tr ? Math.round(tr.getBoundingClientRect().height) : null,
      cols,
    };
  });
  if (r) perSection.push(r);
}
out.perSection = perSection;

fs.writeFileSync(process.argv[2] || '/tmp/html-metrics.json', JSON.stringify(out, null, 2));
console.log(JSON.stringify({ chrome: out.chrome, cards: out.cards, rootFont: out.rootFont, bodyFont: out.bodyFont }, null, 1));
console.log('\n== per section ==');
for (const s of perSection) {
  if (!s.tableWidth) { console.log(`${s.section}: (بی‌جدول) عرضِ بخش ${s.sectionWidth}`); continue; }
  console.log(`${s.section}: بخش ${s.sectionWidth} · جدول ${s.tableWidth} · فونت ${s.tableFont} · th ${s.thFont}/${s.thPad}/${s.thH}px · td ${s.tdFont}/${s.tdPad} · ردیف ${s.rowH}px · ${s.cols.length} ستون`);
  console.log('   ' + s.cols.map((c) => `${c.t}=${c.w}`).join('  '));
}
await browser.close();
