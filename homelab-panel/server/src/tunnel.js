// ---------------------------------------------------------------------------
//  تونل اینترنتی — تا سایت از هر دستگاهی، هر جای دنیا، به همین سرور وصل شود
//
//  چرا لازم است: صفحه‌ای که با https باز می‌شود اجازهٔ اتصال به ws:// ندارد، و
//  آی‌پی خانگی (192.168.x.x) هم از بیرون خانه پیدا نمی‌شود. تونل هر دو را حل
//  می‌کند: یک آدرس https/wss عمومی می‌دهد که به همین کامپیوتر می‌رسد.
//
//  اینجا cloudflared (ابزار رسمی و رایگان Cloudflare) خودکار دانلود و اجرا
//  می‌شود؛ خروجی‌اش خوانده می‌شود تا آدرس عمومی پیدا شود.
// ---------------------------------------------------------------------------
import { spawn } from 'node:child_process';
import { EventEmitter } from 'node:events';
import fs from 'node:fs';
import fsp from 'node:fs/promises';
import path from 'node:path';
import https from 'node:https';
import os from 'node:os';
import { config } from './config.js';
import { logEvent } from './db.js';

export const tunnelEvents = new EventEmitter();

const BIN_DIR = path.join(config.dataDir, 'bin');

function binaryName() {
  return process.platform === 'win32' ? 'cloudflared.exe' : 'cloudflared';
}

function downloadUrl() {
  const arch = process.arch === 'arm64' ? 'arm64' : process.arch === 'arm' ? 'arm' : 'amd64';
  const base = 'https://github.com/cloudflare/cloudflared/releases/latest/download/';
  if (process.platform === 'win32') return `${base}cloudflared-windows-${arch === 'arm64' ? 'arm64' : 'amd64'}.exe`;
  if (process.platform === 'darwin') return `${base}cloudflared-darwin-${arch}.tgz`;
  return `${base}cloudflared-linux-${arch}`;
}

const state = {
  status: 'stopped', // stopped | installing | starting | running | error
  url: null, // https://xxx.trycloudflare.com
  error: null,
  startedAt: null,
  restarts: 0,
  binary: null,
  lastLines: [],
};

let child = null;
let stopping = false;
let restartTimer = null;

function pushLine(line) {
  state.lastLines.push(line.slice(0, 300));
  if (state.lastLines.length > 40) state.lastLines.shift();
}

function setStatus(status, extra = {}) {
  Object.assign(state, { status, ...extra });
  tunnelEvents.emit('change', publicState());
}

export function publicState() {
  return {
    status: state.status,
    url: state.url,
    wss: state.url ? state.url.replace(/^https:/, 'wss:') : null,
    error: state.error,
    startedAt: state.startedAt,
    restarts: state.restarts,
    installed: Boolean(state.binary),
    binary: state.binary,
    log: state.lastLines.slice(-12),
  };
}

// ---------------------------------------------------------------------------
// پیدا کردن یا دانلود cloudflared
// ---------------------------------------------------------------------------
function existingBinary() {
  const local = path.join(BIN_DIR, binaryName());
  if (fs.existsSync(local)) return local;

  // شاید کاربر خودش نصب کرده و در PATH است
  const dirs = (process.env.PATH || '').split(path.delimiter).filter(Boolean);
  for (const dir of dirs) {
    const candidate = path.join(dir, binaryName());
    try {
      fs.accessSync(candidate, fs.constants.X_OK);
      return candidate;
    } catch { /* اینجا نبود */ }
  }
  return null;
}

function download(url, dest, { redirects = 0 } = {}) {
  return new Promise((resolve, reject) => {
    if (redirects > 5) return reject(new Error('too_many_redirects'));
    const req = https.get(url, { timeout: 60000 }, (res) => {
      if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
        res.resume();
        return resolve(download(res.headers.location, dest, { redirects: redirects + 1 }));
      }
      if (res.statusCode !== 200) {
        res.resume();
        return reject(new Error(`http_${res.statusCode}`));
      }
      const tmp = `${dest}.part`;
      const file = fs.createWriteStream(tmp);
      res.pipe(file);
      file.on('finish', () => {
        file.close(() => {
          try {
            fs.renameSync(tmp, dest);
            if (process.platform !== 'win32') fs.chmodSync(dest, 0o755);
            resolve(dest);
          } catch (e) {
            reject(e);
          }
        });
      });
      file.on('error', reject);
    });
    req.on('timeout', () => req.destroy(new Error('timeout')));
    req.on('error', reject);
  });
}

export async function ensureBinary() {
  const found = existingBinary();
  if (found) {
    state.binary = found;
    return found;
  }

  if (process.platform === 'darwin') {
    // نسخهٔ مک فشرده است؛ باز کردنش وابستگی می‌خواهد — از کاربر می‌خواهیم خودش نصب کند
    throw new Error('macos_manual_install');
  }

  await fsp.mkdir(BIN_DIR, { recursive: true });
  const dest = path.join(BIN_DIR, binaryName());
  setStatus('installing');
  console.log('[tunnel] در حال دانلود cloudflared (فقط همین یک‌بار)…');
  logEvent('info', 'panel', 'دانلود cloudflared برای تونل اینترنتی آغاز شد');
  await download(downloadUrl(), dest);
  state.binary = dest;
  console.log(`[tunnel] cloudflared آماده شد: ${dest}`);
  logEvent('info', 'panel', 'cloudflared با موفقیت دانلود شد');
  return dest;
}

// ---------------------------------------------------------------------------
// اجرا
// ---------------------------------------------------------------------------
// آدرس تونل باید از بین چند آدرسی که cloudflared چاپ می‌کند درست انتخاب شود.
// بنرِ خودش لینکِ قوانین و مستندات دارد؛ آن‌ها نباید به‌جای تونل برداشته شوند.
const QUICK_TUNNEL_RE = /https:\/\/[a-z0-9-]+\.trycloudflare\.com/i;
// هر آدرس https؛ درستی‌اش با new URL بررسی می‌شود (دامنه یا IP، با یا بدون پورت)
const ANY_URL_RE = /https:\/\/[^\s"'<>|]+/gi;

// دامنه‌هایی که در بنر و پیام‌های راهنما می‌آیند و آدرس تونل نیستند
const NOT_A_TUNNEL = /^(?:www\.)?cloudflare\.com$|^developers\.cloudflare\.com$|^github\.com$|^dash\.cloudflare\.com$/i;

function extractTunnelUrl(line, custom) {
  if (!custom) {
    const quick = line.match(QUICK_TUNNEL_RE);
    return quick ? quick[0] : null;
  }
  // دستور سفارشی: هر آدرس https به‌جز دامنه‌های راهنما
  for (const candidate of line.match(ANY_URL_RE) || []) {
    let host;
    try {
      host = new URL(candidate).host.replace(/:\d+$/, '');
    } catch {
      continue;
    }
    if (NOT_A_TUNNEL.test(host)) continue;
    return candidate.replace(/[.,)\]]+$/, '');
  }
  return null;
}

export async function startTunnel({ port } = {}) {
  if (child) return publicState();
  stopping = false;
  clearTimeout(restartTimer);

  const targetPort = port || config.siteSync.port || config.port;

  try {
    if (!process.env.HLP_TUNNEL_CMD) await ensureBinary();
  } catch (e) {
    setStatus('error', {
      error:
        e.message === 'macos_manual_install'
          ? 'روی مک، cloudflared را دستی نصب کنید: brew install cloudflared'
          : `دانلود cloudflared ناموفق بود: ${e.message}`,
    });
    logEvent('error', 'panel', `تونل: ${state.error}`);
    return publicState();
  }

  setStatus('starting', { url: null, error: null });

  // HLP_TUNNEL_CMD برای حالت‌های خاص: اگر کسی تونل دیگری دارد یا در آزمون‌ها.
  // مقدارش با کاما جدا می‌شود و {port} با پورت مقصد جایگزین می‌شود.
  const custom = process.env.HLP_TUNNEL_CMD;
  const [command, args] = custom
    ? (() => {
        const parts = custom.split(',').map((p) => p.trim().replaceAll('{port}', String(targetPort)));
        return [parts[0], parts.slice(1)];
      })()
    : [
        state.binary,
        ['tunnel', '--no-autoupdate', '--url', `http://127.0.0.1:${targetPort}`, '--loglevel', 'info'],
      ];

  child = spawn(command, args, { stdio: ['ignore', 'pipe', 'pipe'], windowsHide: true });

  const onData = (chunk) => {
    const text = chunk.toString();
    for (const raw of text.split(/\r?\n/)) {
      const line = raw.trim();
      if (!line) continue;
      pushLine(line);
      const found = extractTunnelUrl(line, Boolean(custom));
      if (found && !state.url) {
        setStatus('running', { url: found, startedAt: Date.now(), error: null });
        console.log(`[tunnel] آدرس عمومی آماده شد: ${found}`);
        logEvent('info', 'panel', `تونل اینترنتی فعال شد: ${found}`);
      }
    }
  };

  child.stdout.on('data', onData);
  child.stderr.on('data', onData); // cloudflared بیشتر روی stderr می‌نویسد

  child.on('error', (e) => {
    setStatus('error', { error: e.message });
    logEvent('error', 'panel', `تونل اجرا نشد: ${e.message}`);
    child = null;
  });

  child.on('exit', (code) => {
    child = null;
    if (stopping) {
      setStatus('stopped', { url: null });
      return;
    }
    state.restarts++;
    setStatus('error', { url: null, error: `تونل بسته شد (کد ${code ?? '-'}) — دوباره تلاش می‌شود` });
    logEvent('warn', 'panel', `تونل بسته شد (کد ${code}); تلاش دوباره تا ۱۰ ثانیهٔ دیگر`);
    restartTimer = setTimeout(() => startTunnel({ port: targetPort }), 10000);
    restartTimer.unref?.();
  });

  return publicState();
}

export function stopTunnel() {
  stopping = true;
  clearTimeout(restartTimer);
  if (child) {
    try {
      child.kill();
    } catch { /* بسته شده */ }
    child = null;
  }
  setStatus('stopped', { url: null, error: null });
  return publicState();
}

export function tunnelRunning() {
  return Boolean(child) && state.status === 'running';
}

// آدرس عمومیِ آمادهٔ استفاده در سایت
export function tunnelWss() {
  return state.url ? state.url.replace(/^https:/, 'wss:') : null;
}

export function localHint() {
  const iface = Object.values(os.networkInterfaces())
    .flat()
    .find((ni) => ni && !ni.internal && (ni.family === 'IPv4' || ni.family === 4));
  return iface?.address || null;
}
