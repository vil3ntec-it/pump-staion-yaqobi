// ---------------------------------------------------------------------------
//  آزمونِ «جست‌وجو در صفحه» ی برنامهٔ کامپیوتری
//
//      node tools/check-desktop-find.mjs
//
//  پوستهٔ واقعی را با PUMP_BENCH=find بالا می‌آورد. main.js در همان حالت نوارِ
//  جست‌وجو را باز می‌کند و یک واژهٔ حتماً موجود را می‌جوید؛ اگر موتورِ جست‌وجو
//  درست وصل باشد، شمارشِ نتیجه در لاگ چاپ می‌شود و این آزمون سبز است.
//
//  نیاز: وابستگی‌های desktop/ نصب باشد و روی سرورِ بی‌نمایشگر، xvfb.
// ---------------------------------------------------------------------------
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

const root = path.join(import.meta.dirname, '..');
const dir = path.join(root, 'desktop');
const bin = path.join(dir, 'node_modules', 'electron', 'dist', 'electron');
if (!fs.existsSync(bin)) { console.log('الکترون نصب نیست (npm i در desktop).'); process.exit(1); }

const useXvfb = !process.env.DISPLAY && fs.existsSync('/usr/bin/xvfb-run');
const cmd = useXvfb ? 'xvfb-run' : bin;
const args = useXvfb ? ['-a', bin, '--no-sandbox', dir] : ['--no-sandbox', dir];
const p = spawn(cmd, args, { cwd: dir, detached: true, env: { ...process.env, PUMP_BENCH: 'find' } });
const killAll = (sig) => { try { process.kill(-p.pid, sig); } catch (e) { try { p.kill(sig); } catch (e2) {} } };

let out = '';
const watch = (b) => { out += String(b); if (/\[یافتن\] نتیجه:/.test(out)) setTimeout(() => killAll('SIGTERM'), 200); };
p.stdout.on('data', watch);
p.stderr.on('data', watch);
setTimeout(() => killAll('SIGKILL'), 60000);

p.on('exit', () => {
  killAll('SIGKILL');
  const m = out.match(/\[یافتن\] نتیجه:\s*(\d+)\/(\d+)/);
  console.log('\nآزمونِ جست‌وجوی صفحه (برنامهٔ کامپیوتری)\n');
  if (!m) {
    console.log('  ❌ نوارِ جست‌وجو نتیجه‌ای نداد — موتورِ findInPage وصل نیست.\n');
    process.exit(1);
  }
  if (Number(m[2]) < 1) {
    console.log(`  ❌ صفر نتیجه (${m[0]}) — واژهٔ آزمون باید در صفحه باشد.\n`);
    process.exit(1);
  }
  console.log(`  ✅ نوار باز شد و موتورِ جست‌وجو ${m[2]} نتیجه برگرداند (فعال: ${m[1]})\n`);
  process.exit(0);
});
