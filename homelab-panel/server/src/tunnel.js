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
import { logEvent, getSetting, setSetting } from './db.js';

export const tunnelEvents = new EventEmitter();

const BIN_DIR = path.join(config.dataDir, 'bin');

function binaryName() {
  return process.platform === 'win32' ? 'cloudflared.exe' : 'cloudflared';
}

/**
 * معماری واقعی ویندوز — نه معماری خودِ Node.
 * اگر Node سی‌ودو بیتی روی ویندوز ۶۴ بیتی اجرا شود، process.arch می‌گوید ia32
 * ولی ویندوز ۶۴ بیتی است. متغیر PROCESSOR_ARCHITEW6432 دقیقاً همین را می‌گوید.
 */
function windowsArch() {
  const real = (process.env.PROCESSOR_ARCHITEW6432 || process.env.PROCESSOR_ARCHITECTURE || '').toUpperCase();
  if (real === 'ARM64') return 'arm64';
  if (real === 'AMD64') return 'amd64';
  if (real === 'X86') return '386';
  // اگر متغیر نبود، از خودِ Node بپرس
  if (process.arch === 'arm64') return 'arm64';
  if (process.arch === 'ia32') return '386';
  return 'amd64';
}

function downloadUrl() {
  const base = 'https://github.com/cloudflare/cloudflared/releases/latest/download/';
  if (process.platform === 'win32') return `${base}cloudflared-windows-${windowsArch()}.exe`;
  if (process.platform === 'darwin') {
    return `${base}cloudflared-darwin-${process.arch === 'arm64' ? 'arm64' : 'amd64'}.tgz`;
  }
  const arch = process.arch === 'arm64' ? 'arm64' : process.arch === 'arm' ? 'arm' : 'amd64';
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
    mode: getSetting('tunnel_mode', 'quick'),
    hostname: getSetting('tunnel_hostname', null),
    permanent: ['named', 'token'].includes(getSetting('tunnel_mode', 'quick')),
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

// ---------------------------------------------------------------------------
//  آدرس ثابت (تونل نام‌دار) — مدل فایربیس
//
//  تونل رایگانِ سریع هر بار آدرس تازه می‌دهد، پس نمی‌شود آن را یک‌بار در سایت
//  گذاشت. با «تونل نام‌دار» یک زیردامنهٔ خودتان (مثل sync.example.com) برای همیشه
//  به همین سرور وصل می‌شود؛ آن وقت آدرس یک‌بار در سایت می‌نشیند و دیگر عوض نمی‌شود.
//
//  راه‌اندازی یک‌بار است: ورود به حساب Cloudflare ← ساخت تونل ← وصل کردن زیردامنه.
// ---------------------------------------------------------------------------
const CF_DIR = path.join(config.dataDir, 'cloudflared');
const CERT_FILE = path.join(CF_DIR, 'cert.pem');
const CONFIG_FILE = path.join(CF_DIR, 'config.yml');
// اگر کاربر خودش در ترمینال «cloudflared tunnel login» زده باشد، گواهی اینجاست
const HOME_CERT = path.join(os.homedir(), '.cloudflared', 'cert.pem');

/** گواهی ورود را از هر جا که باشد پیدا می‌کند و در پوشهٔ پنل کپی می‌گیرد */
function findCert() {
  if (fs.existsSync(CERT_FILE)) return CERT_FILE;
  if (fs.existsSync(HOME_CERT)) {
    try {
      fs.mkdirSync(CF_DIR, { recursive: true });
      fs.copyFileSync(HOME_CERT, CERT_FILE);
      return CERT_FILE;
    } catch {
      return HOME_CERT;
    }
  }
  return null;
}

function runCf(args, { timeout = 120000, onLine } = {}) {
  return new Promise((resolve) => {
    const proc = spawn(state.binary, args, {
      stdio: ['ignore', 'pipe', 'pipe'],
      windowsHide: true,
      env: { ...process.env, TUNNEL_ORIGIN_CERT: CERT_FILE },
    });
    let output = '';
    const onData = (chunk) => {
      const text = chunk.toString();
      output += text;
      for (const line of text.split(/\r?\n/)) {
        if (line.trim()) onLine?.(line.trim());
      }
    };
    proc.stdout.on('data', onData);
    proc.stderr.on('data', onData);
    const timer = setTimeout(() => {
      try {
        proc.kill();
      } catch { /* بسته شده */ }
    }, timeout);
    proc.on('exit', (code) => {
      clearTimeout(timer);
      resolve({ ok: code === 0, code, output });
    });
    proc.on('error', (e) => {
      clearTimeout(timer);
      resolve({ ok: false, code: -1, output: output + e.message });
    });
  });
}

export function namedConfig() {
  return {
    loggedIn: Boolean(findCert()),
    configured: fs.existsSync(CONFIG_FILE),
    hostname: getSetting('tunnel_hostname', null),
    tunnelName: getSetting('tunnel_name', null),
    mode: getSetting('tunnel_mode', 'quick'),
    // خودِ توکن هرگز بیرون نمی‌رود — فقط اینکه تنظیم شده یا نه
    hasToken: Boolean(getSetting('tunnel_token', null)),
  };
}

/** گام ۱: ورود به حساب Cloudflare — آدرسی برمی‌گرداند که کاربر باید باز کند */
export async function namedLoginStart() {
  await ensureBinary();
  await fsp.mkdir(CF_DIR, { recursive: true });
  if (findCert()) return { alreadyLoggedIn: true, url: null };

  return new Promise((resolve) => {
    let settled = false;
    const proc = spawn(state.binary, ['tunnel', 'login'], {
      stdio: ['ignore', 'pipe', 'pipe'],
      windowsHide: true,
      env: { ...process.env, TUNNEL_ORIGIN_CERT: CERT_FILE },
    });
    loginProc = proc;

    const onData = (chunk) => {
      const text = chunk.toString();
      pushLine(text.trim().slice(0, 200));
      // cloudflared ممکن است آدرس را با قالب‌های مختلف چاپ کند
      const match =
        text.match(/https:\/\/dash\.cloudflare\.com\/\S+/) ||
        text.match(/https:\/\/[a-z0-9.-]*cloudflare[a-z0-9.-]*\/\S*argotunnel\S*/i);
      if (match && !settled) {
        settled = true;
        resolve({ alreadyLoggedIn: false, url: match[0].replace(/[.,)\]]+$/, '') });
      }
    };
    proc.stdout.on('data', onData);
    proc.stderr.on('data', onData);
    proc.on('exit', () => {
      loginProc = null;
      if (!settled) {
        settled = true;
        resolve({ alreadyLoggedIn: fs.existsSync(CERT_FILE), url: null });
      }
    });
    setTimeout(() => {
      if (!settled) {
        settled = true;
        resolve({
          alreadyLoggedIn: Boolean(findCert()),
          url: null,
          error: 'cloudflared آدرس ورود را چاپ نکرد',
          log: state.lastLines.slice(-8),
          manualCommand: `"${state.binary}" tunnel login`,
        });
      }
    }, 60000).unref?.();
  });
}

export function namedLoginDone() {
  return Boolean(findCert());
}

/** گام ۲: ساخت تونل و وصل کردن زیردامنه — بعد از آن آدرس برای همیشه ثابت است */
export async function namedSetup({ hostname, name = 'pump-yaqobi' }) {
  const host = String(hostname || '').trim().toLowerCase();
  if (!/^[a-z0-9-]+(\.[a-z0-9-]+)+$/.test(host)) return { ok: false, error: 'invalid_hostname' };
  if (!findCert()) return { ok: false, error: 'not_logged_in' };

  await ensureBinary();

  // اگر تونل با همین نام از قبل هست، دوباره ساخته نمی‌شود
  const created = await runCf(['tunnel', 'create', name]);
  const already = /already exists/i.test(created.output);
  if (!created.ok && !already) {
    return { ok: false, error: 'create_failed', detail: created.output.slice(-400) };
  }

  const listed = await runCf(['tunnel', 'list', '--output', 'json']);
  let uuid = null;
  try {
    uuid = (JSON.parse(listed.output.slice(listed.output.indexOf('[')))?.find((t) => t.name === name) || {}).id;
  } catch { /* از خروجی ساخت می‌خوانیم */ }
  if (!uuid) {
    const m = created.output.match(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i);
    uuid = m ? m[0] : null;
  }
  if (!uuid) return { ok: false, error: 'tunnel_id_not_found', detail: created.output.slice(-400) };

  const routed = await runCf(['tunnel', 'route', 'dns', '--overwrite-dns', name, host]);
  if (!routed.ok && !/already exists|record with the same/i.test(routed.output)) {
    return { ok: false, error: 'dns_failed', detail: routed.output.slice(-400) };
  }

  const credFile = path.join(CF_DIR, `${uuid}.json`);
  const homeCred = path.join(os.homedir(), '.cloudflared', `${uuid}.json`);
  if (!fs.existsSync(credFile) && fs.existsSync(homeCred)) {
    await fsp.copyFile(homeCred, credFile);
  }

  const targetPort = config.siteSync.port || config.port;
  const yml = [
    `tunnel: ${uuid}`,
    `credentials-file: ${credFile.replaceAll('\\', '/')}`,
    'ingress:',
    `  - hostname: ${host}`,
    `    service: http://127.0.0.1:${targetPort}`,
    '  - service: http_status:404',
    '',
  ].join('\n');
  await fsp.writeFile(CONFIG_FILE, yml, 'utf8');

  setSetting('tunnel_mode', 'named');
  setSetting('tunnel_hostname', host);
  setSetting('tunnel_name', name);
  logEvent('info', 'panel', `آدرس ثابت ساخته شد: ${host}`);

  stopTunnel();
  await startTunnel({});
  return { ok: true, hostname: host, tunnelId: uuid };
}

/**
 * راهِ ساده‌تر: توکنِ تونل را از داشبورد Cloudflare بردارید و اینجا بچسبانید.
 * (Zero Trust ← Networks ← Tunnels ← Create a tunnel ← Cloudflared)
 * زیردامنه هم در همان داشبورد به این تونل وصل می‌شود (Public Hostname).
 */
export async function tokenSetup({ token, hostname }) {
  const clean = String(token || '').trim();
  const host = String(hostname || '').trim().toLowerCase();
  // توکن تونل یک رشتهٔ base64 بلند است
  if (clean.length < 40 || /\s/.test(clean)) return { ok: false, error: 'invalid_token' };
  if (!/^[a-z0-9-]+(\.[a-z0-9-]+)+$/.test(host)) return { ok: false, error: 'invalid_hostname' };

  try {
    await ensureBinary();
  } catch (e) {
    return { ok: false, error: 'cloudflared_missing', detail: e.message };
  }

  setSetting('tunnel_token', clean);
  setSetting('tunnel_hostname', host);
  setSetting('tunnel_mode', 'token');
  logEvent('info', 'panel', `تونل با توکن تنظیم شد — آدرس ثابت: ${host}`);

  stopTunnel();
  const st = await startTunnel({});
  return { ok: true, hostname: host, status: st.status };
}

export async function namedReset() {
  setSetting('tunnel_token', null);
  setSetting('tunnel_mode', 'quick');
  setSetting('tunnel_hostname', null);
  stopTunnel();
  await fsp.rm(CONFIG_FILE, { force: true });
  return startTunnel({});
}

let loginProc = null;

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
  const mode = getSetting('tunnel_mode', 'quick');
  const tunnelToken = mode === 'token' ? getSetting('tunnel_token', null) : null;
  const named = !custom && mode === 'named' && fs.existsSync(CONFIG_FILE);
  // در هر دو حالتِ آدرسِ ثابت، زیردامنه از قبل معلوم است
  const namedHost = custom ? null : named || tunnelToken ? getSetting('tunnel_hostname', null) : null;

  const [command, args] = custom
    ? (() => {
        const parts = custom.split(',').map((p) => p.trim().replaceAll('{port}', String(targetPort)));
        return [parts[0], parts.slice(1)];
      })()
    : tunnelToken
      ? [state.binary, ['tunnel', '--no-autoupdate', 'run', '--token', tunnelToken]]
      : named
        ? [state.binary, ['tunnel', '--no-autoupdate', '--config', CONFIG_FILE, 'run']]
        : [
          state.binary,
          ['tunnel', '--no-autoupdate', '--url', `http://127.0.0.1:${targetPort}`, '--loglevel', 'info'],
        ];

  child = spawn(command, args, {
    stdio: ['ignore', 'pipe', 'pipe'],
    windowsHide: true,
    env: { ...process.env, TUNNEL_ORIGIN_CERT: CERT_FILE },
  });

  // در حالت آدرس ثابت، آدرس از قبل معلوم است — فقط منتظر برقراری اتصال می‌مانیم
  if (namedHost) {
    setStatus('starting', { url: null, error: null });
  }

  const onData = (chunk) => {
    const text = chunk.toString();
    for (const raw of text.split(/\r?\n/)) {
      const line = raw.trim();
      if (!line) continue;
      pushLine(line);
      // حالت آدرس ثابت: به‌محض ثبت اتصال، همان زیردامنه آدرس ماست
      if (namedHost && !state.url && /Registered tunnel connection|Connection .* registered/i.test(line)) {
        setStatus('running', { url: `https://${namedHost}`, startedAt: Date.now(), error: null });
        console.log(`[tunnel] آدرس ثابت فعال شد: https://${namedHost}`);
        logEvent('info', 'panel', `تونل با آدرس ثابت فعال شد: ${namedHost}`);
        continue;
      }

      const found = namedHost ? null : extractTunnelUrl(line, Boolean(custom));
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
