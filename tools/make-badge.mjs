/* ساختنِ آیکونِ کوچکِ اعلان (badge) — icons/badge-96.png
 * ---------------------------------------------------------------------------
 * چرا لازم شد: اندروید برای «آیکونِ کوچکِ» اعلان فقط کانالِ آلفا را می‌خواند و
 * رنگ‌ها را دور می‌ریزد. تا حالا همان icon-192.png (تصویرِ رنگیِ پُر) به‌عنوان
 * badge داده می‌شد، پس اندروید یک مربعِ سفیدِ تو‌خالی نشان می‌داد و معلوم نبود
 * اعلان از کدام برنامه است. این اسکریپت یک نشانِ تک‌رنگ (سیلوئتِ پمپِ تیل) با
 * پس‌زمینهٔ کاملاً شفاف می‌سازد که اندروید درست نشانش می‌دهد.
 *
 * اجرا:  node tools/make-badge.mjs      (خروجی: icons/badge-96.png)
 * وابستگی: هیچ — PNG با zlibِ خودِ Node کدگذاری می‌شود.
 */
import { deflateSync } from 'node:zlib';
import { writeFileSync } from 'node:fs';

const S = 96, SS = 4, N = S * SS;          // با ۴ برابر رسم و بعد کوچک‌سازی = لبه‌های نرم

const on = (x, y) => {
  // مختصات را به فضای ۹۶تایی برگردان
  const px = x / SS, py = y / SS;
  const rect = (x0, y0, x1, y1, r = 0) => {
    if (px < x0 || px > x1 || py < y0 || py > y1) return false;
    if (!r) return true;
    const cx = Math.min(Math.max(px, x0 + r), x1 - r);
    const cy = Math.min(Math.max(py, y0 + r), y1 - r);
    return (px - cx) ** 2 + (py - cy) ** 2 <= r * r;
  };
  const line = (x0, y0, x1, y1, w) => {           // خطِ ضخیم بین دو نقطه
    const dx = x1 - x0, dy = y1 - y0;
    const L2 = dx * dx + dy * dy;
    let t = L2 ? ((px - x0) * dx + (py - y0) * dy) / L2 : 0;
    t = Math.min(1, Math.max(0, t));
    const qx = x0 + t * dx, qy = y0 + t * dy;
    return (px - qx) ** 2 + (py - qy) ** 2 <= (w / 2) ** 2;
  };

  // بدنهٔ پمپ
  const body = rect(16, 16, 54, 78, 7);
  const screen = rect(23, 25, 47, 43, 3);         // نمایشگر (سوراخ)
  const slot = rect(23, 50, 47, 56, 3);           // شیارِ زیرِ نمایشگر (سوراخ)
  const base = rect(10, 78, 60, 87, 4);           // پایه
  // شیلنگ و نازل در سمتِ راست
  const hoseUp = line(54, 34, 70, 34, 7);
  const hoseDn = line(70, 34, 70, 62, 7);
  const nozzle = rect(63, 20, 78, 30, 4);

  const solid = body || base || hoseUp || hoseDn || nozzle;
  const hole = screen || slot;
  return solid && !hole;
};

// ── رسم با supersampling ────────────────────────────────────────────────────
const acc = new Float32Array(S * S);
for (let y = 0; y < N; y++) {
  for (let x = 0; x < N; x++) {
    if (!on(x, y)) continue;
    acc[(y / SS | 0) * S + (x / SS | 0)] += 1;
  }
}

// ── بایت‌های خام RGBA (سفیدِ توپر، آلفا = پوششِ همان پیکسل) ──────────────────
const raw = Buffer.alloc(S * (S * 4 + 1));
let p = 0;
for (let y = 0; y < S; y++) {
  raw[p++] = 0;                                   // filter = None
  for (let x = 0; x < S; x++) {
    const a = Math.round(255 * Math.min(1, acc[y * S + x] / (SS * SS)));
    raw[p++] = 255; raw[p++] = 255; raw[p++] = 255; raw[p++] = a;
  }
}

// ── بسته‌بندیِ PNG ──────────────────────────────────────────────────────────
const crcTable = (() => {
  const t = new Int32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c;
  }
  return t;
})();
const crc32 = buf => {
  let c = -1;
  for (let i = 0; i < buf.length; i++) c = crcTable[(c ^ buf[i]) & 0xff] ^ (c >>> 8);
  return (c ^ -1) >>> 0;
};
const chunk = (type, data) => {
  const len = Buffer.alloc(4); len.writeUInt32BE(data.length);
  const body = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(body));
  return Buffer.concat([len, body, crc]);
};
const ihdr = Buffer.alloc(13);
ihdr.writeUInt32BE(S, 0); ihdr.writeUInt32BE(S, 4);
ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;   // 8bit RGBA
const png = Buffer.concat([
  Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
  chunk('IHDR', ihdr),
  chunk('IDAT', deflateSync(raw, { level: 9 })),
  chunk('IEND', Buffer.alloc(0)),
]);
writeFileSync(new URL('../icons/badge-96.png', import.meta.url), png);
console.log('✅ icons/badge-96.png ساخته شد —', png.length, 'بایت');
