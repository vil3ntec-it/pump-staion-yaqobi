// ---------------------------------------------------------------------------
//  بنچمارکِ بالا آمدنِ برنامهٔ کامپیوتری (Electron)
//
//      node tools/bench-desktop.mjs            (سه بار، میانه گزارش می‌شود)
//      RUNS=5 node tools/bench-desktop.mjs
//
//  هیچ چیزی را عوض نمی‌کند: خودِ پوسته را واقعاً بالا می‌آورد و زمان را از
//  همان خطِ لاگی می‌گیرد که خودِ main.js چاپ می‌کند («[لودینگ] پایان: …»)،
//  یعنی لحظه‌ای که پنجرهٔ اصلی نشان داده می‌شود.
//
//  اجرای اول «سرد» است (کشِ کدِ V8 هنوز ساخته نشده) و اجراهای بعدی «گرم» —
//  هر دو گزارش می‌شوند، چون کاربر هر دو را تجربه می‌کند.
//
//  نیاز: وابستگی‌های desktop/ نصب باشد (npm i در پوشهٔ desktop) و روی
//  سرورِ بی‌نمایشگر، xvfb.
// ---------------------------------------------------------------------------
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

const root = path.join(import.meta.dirname, '..');
const dir = path.join(root, 'desktop');
const bin = path.join(dir, 'node_modules', 'electron', 'dist', 'electron');
const RUNS = Number(process.env.RUNS || 3);

if (!fs.existsSync(bin)) {
  console.log('الکترون نصب نیست. اول در پوشهٔ desktop یک‌بار npm i بزنید.');
  process.exit(1);
}

function once() {
  return new Promise((resolve) => {
    const t0 = Date.now();
    const useXvfb = !process.env.DISPLAY && fs.existsSync('/usr/bin/xvfb-run');
    const cmd = useXvfb ? 'xvfb-run' : bin;
    const args = useXvfb ? ['-a', bin, '--no-sandbox', dir] : ['--no-sandbox', dir];
    // detached: کلِ گروهِ فرایند را می‌کشیم. xvfb-run یک پوستهٔ واسط است و
    // کشتنِ خودش الکترون را زنده می‌گذارد؛ نسخهٔ زنده قفلِ «تک‌نمونه» را نگه
    // می‌دارد و اجرای بعدی بی‌صدا بسته می‌شود (اجرای ۲ و ۳ «نرسید» می‌شدند).
    const p = spawn(cmd, args, { cwd: dir, detached: true,
      env: { ...process.env, ELECTRON_DISABLE_SECURITY_WARNINGS: '1' } });
    const killAll = (sig) => { try { process.kill(-p.pid, sig); } catch (e) { try { p.kill(sig); } catch (e2) {} } };
    let ms = null;
    const watch = (buf) => {
      if (ms === null && String(buf).includes('[لودینگ] پایان')) {
        ms = Date.now() - t0;
        setTimeout(() => killAll('SIGTERM'), 300);
      }
    };
    p.stdout.on('data', watch);
    p.stderr.on('data', watch);
    const guard = setTimeout(() => killAll('SIGKILL'), 60000);
    p.on('exit', () => { clearTimeout(guard); killAll('SIGKILL'); setTimeout(() => resolve(ms), 800); });
  });
}

const out = [];
for (let i = 0; i < RUNS; i++) out.push(await once());

const ok = out.filter((v) => typeof v === 'number');
const sorted = [...ok].sort((a, b) => a - b);
console.log('\n── بالا آمدنِ برنامهٔ کامپیوتری ──');
out.forEach((v, i) => console.log(`  اجرای ${i + 1}${i === 0 ? ' (سرد)' : ' (گرم)'}: ${v === null ? 'نرسید' : v + 'ms'}`));
if (ok.length) console.log(`  میانه: ${sorted[Math.floor(sorted.length / 2)]}ms\n`);
else console.log('  هیچ اجرایی به پنجره نرسید.\n');
