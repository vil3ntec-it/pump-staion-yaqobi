// ---------------------------------------------------------------------------
// پیکربندی سرور — همه‌چیز از متغیرهای محیطی یا فایل .env کنار همین پوشه
// ---------------------------------------------------------------------------
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
export const SERVER_ROOT = path.resolve(__dirname, '..');

// خواندن .env (اختیاری)
(function loadEnv() {
  try {
    const envPath = path.join(SERVER_ROOT, '.env');
    if (!fs.existsSync(envPath)) return;
    for (const raw of fs.readFileSync(envPath, 'utf8').split(/\r?\n/)) {
      const line = raw.trim();
      if (!line || line.startsWith('#')) continue;
      const eq = line.indexOf('=');
      if (eq < 0) continue;
      const key = line.slice(0, eq).trim();
      let val = line.slice(eq + 1).trim();
      if ((val.startsWith('"') && val.endsWith('"')) || (val.startsWith("'") && val.endsWith("'"))) {
        val = val.slice(1, -1);
      }
      if (!(key in process.env)) process.env[key] = val;
    }
  } catch { /* بی‌خیال */ }
})();

const num = (v, d) => {
  const n = parseInt(v ?? '', 10);
  return Number.isFinite(n) ? n : d;
};

export const config = {
  port: num(process.env.HLP_PORT, 4700),
  host: process.env.HLP_HOST || '0.0.0.0',

  // محل نگهداری دیتابیس پنل، لاگ‌ها، بکاپ‌ها و دادهٔ سرورِ سایت
  dataDir: path.resolve(process.env.HLP_DATA_DIR || path.join(SERVER_ROOT, 'data')),

  // ریشهٔ پیش‌فرضِ سایت‌ها (هر سایت یک پوشهٔ مستقل زیر همین مسیر)
  sitesRoot: path.resolve(
    process.env.HLP_SITES_ROOT ||
      (process.platform === 'win32' ? path.join(os.homedir(), 'sites') : '/sites')
  ),

  // اندازهٔ حداکثری آپلود در فایل‌منیجر (بایت)
  maxUploadBytes: num(process.env.HLP_MAX_UPLOAD, 256 * 1024 * 1024),

  // فاصلهٔ ارسال معیارهای زنده (میلی‌ثانیه)
  metricsIntervalMs: num(process.env.HLP_METRICS_INTERVAL, 2000),

  // اعتبار توکن ورود
  tokenTtlSeconds: num(process.env.HLP_TOKEN_TTL, 12 * 3600),

  // مقصدِ اندازه‌گیری پینگ (TCP) — بدون نیاز به دستور ping سیستم‌عامل
  pingTarget: process.env.HLP_PING_TARGET || '1.1.1.1:443',

  // سرویس تشخیص IP عمومی (اگر اینترنت نبود، مقدار null برمی‌گردد — داده‌ی ساختگی نداریم)
  publicIpUrl: process.env.HLP_PUBLIC_IP_URL || 'https://api.ipify.org?format=json',

  // سرورِ سایتِ پمپ یعقوبی (پروتکل realtime) روی همین پورت سوار می‌شود
  siteSync: {
    enabled: (process.env.HLP_SITESYNC ?? '1') !== '0',
    dataDir: path.resolve(
      process.env.HLP_SITESYNC_DATA_DIR ||
        path.join(process.env.HLP_DATA_DIR || path.join(SERVER_ROOT, 'data'), 'site-sync')
    ),
    token: process.env.HLP_SITESYNC_TOKEN || '',
  },
};

export const paths = {
  db: path.join(config.dataDir, 'panel.db'),
  sitesData: path.join(config.dataDir, 'sites'),   // فضای کاری هر سایت (لاگ/بکاپ/دیتابیس/تنظیمات)
  uploads: path.join(config.dataDir, 'uploads'),   // لوگو و فایل‌های خود پنل
};

export function ensureDirs() {
  for (const dir of [config.dataDir, paths.sitesData, paths.uploads, config.siteSync.dataDir]) {
    fs.mkdirSync(dir, { recursive: true });
  }
}
