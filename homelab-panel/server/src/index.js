// ---------------------------------------------------------------------------
//  پنل مدیریت سرور خانگی — نقطهٔ ورود
//  یک پورت، سه کار:
//    ۱) رابط کاربری و API خودِ پنل            → /  و  /api/*
//    ۲) اطلاعات لحظه‌ای                        → /socket.io/
//    ۳) سرورِ سایتِ پمپ یعقوبی (پروتکل ws)     → همان آدرس، بدون مسیر اضافه
// ---------------------------------------------------------------------------
import http from 'node:http';
import path from 'node:path';
import fs from 'node:fs';
import express from 'express';

import { config, ensureDirs, SERVER_ROOT } from './config.js';
import { db, logEvent, pruneEvents, getSetting } from './db.js';
import { pruneSessions } from './auth.js';
import { setSiteSync, setIo, getIo } from './state.js';
import { createSiteSync } from './sitesync/index.js';
import { attachRealtime, broadcastMetrics } from './realtime.js';
import { startCollector, stopCollector } from './metrics/index.js';
import { readInterfaces } from './metrics/network.js';
import { autostartAll } from './sites/registry.js';
import { stopAll } from './sites/process.js';

import authRoutes from './routes/auth.js';
import dashboardRoutes from './routes/dashboard.js';
import sitesRoutes from './routes/sites.js';
import domainsRoutes from './routes/domains.js';
import filesRoutes from './routes/files.js';
import logsRoutes from './routes/logs.js';
import networkRoutes from './routes/network.js';
import settingsRoutes from './routes/settings.js';
import siteServerRoutes from './routes/site-server.js';

const PUBLIC_DIR = path.join(SERVER_ROOT, 'public');

ensureDirs();

const app = express();
app.disable('x-powered-by');

// در شبکهٔ خانگی، پنل ممکن است از آدرس‌های مختلف باز شود
app.use((req, res, next) => {
  res.setHeader('Access-Control-Allow-Origin', req.headers.origin || '*');
  res.setHeader('Access-Control-Allow-Credentials', 'true');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization');
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, OPTIONS');
  if (req.method === 'OPTIONS') return res.status(204).end();
  next();
});

// بدنهٔ JSON فقط برای مسیرهایی که JSON می‌گیرند (آپلود فایل خام است)
app.use((req, res, next) => {
  if (req.path === '/api/files/upload' || req.path === '/api/settings/logo') return next();
  express.json({ limit: '5mb' })(req, res, next);
});

// ------------------------------- سلامت -------------------------------------
// همان پاسخی که سرور قدیمیِ سایت می‌داد تا تستِ «آیا سرور بالاست؟» کار کند
app.get('/health', (req, res) => {
  res.json({
    ok: true,
    service: 'pump-yaqobi-server',
    panel: 'homelab-panel',
    time: new Date().toISOString(),
  });
});

// -------------------------------- API --------------------------------------
app.use('/api/auth', authRoutes);
app.use('/api/dashboard', dashboardRoutes);
app.use('/api/sites', sitesRoutes);
app.use('/api/domains', domainsRoutes);
app.use('/api/files', filesRoutes);
app.use('/api/logs', logsRoutes);
app.use('/api/network', networkRoutes);
app.use('/api/settings', settingsRoutes);
app.use('/api/site-server', siteServerRoutes);

app.use('/api', (req, res) => res.status(404).json({ error: 'not_found' }));

// ---------------------------- رابط کاربری ----------------------------------
if (fs.existsSync(PUBLIC_DIR)) {
  app.use(
    express.static(PUBLIC_DIR, {
      index: 'index.html',
      setHeaders(res, filePath) {
        if (filePath.endsWith('.html')) res.setHeader('Cache-Control', 'no-cache');
        else if (/\.[0-9a-f]{8}\.(js|css)$/.test(filePath)) {
          res.setHeader('Cache-Control', 'public, max-age=31536000, immutable');
        }
      },
    })
  );
  // مسیرهای داخلی SPA
  app.get('*', (req, res, next) => {
    if (req.path.startsWith('/api') || req.path.startsWith('/socket.io')) return next();
    res.sendFile(path.join(PUBLIC_DIR, 'index.html'));
  });
} else {
  app.get('/', (req, res) => {
    res
      .status(200)
      .type('text/plain; charset=utf-8')
      .send('رابط کاربری هنوز ساخته نشده است. در پوشهٔ web دستور «npm install && npm run build» را اجرا کنید.');
  });
}

app.use((err, req, res, next) => {
  logEvent('error', 'panel', `${req.method} ${req.path} → ${err.message}`);
  if (res.headersSent) return next(err);
  res.status(500).json({ error: 'internal_error', message: err.message });
});

// ------------------------------ راه‌اندازی ----------------------------------
const httpServer = http.createServer(app);

// ۱) Socket.IO (روی مسیر /socket.io/)
const io = attachRealtime(httpServer);
setIo(io);

// ۲) سرورِ سایت روی همان پورت — هر ارتقای WebSocket که مسیرش /socket.io نباشد
let siteSync = null;
if (config.siteSync.enabled) {
  siteSync = createSiteSync({ dataDir: config.siteSync.dataDir, token: config.siteSync.token });
  setSiteSync(siteSync);

  httpServer.on('upgrade', (req, socket, head) => {
    let pathname = '/';
    try {
      pathname = new URL(req.url, 'http://x').pathname;
    } catch { /* مسیر خراب */ }
    if (pathname.startsWith('/socket.io')) return; // مالِ Socket.IO است
    siteSync.handleUpgrade(req, socket, head);
  });
}

// ۳) معیارهای زنده
startCollector((snapshot) => {
  broadcastMetrics(getIo(), snapshot);
});

// نگهداری دوره‌ای
const housekeeping = setInterval(() => {
  pruneSessions();
  pruneEvents();
}, 15 * 60 * 1000);
housekeeping.unref?.();

async function main() {
  if (siteSync) {
    await siteSync.ensureToken();
    const loaded = await siteSync.loadFromDisk();
    if (loaded.length) console.log(`[site-server] ${loaded.length} شاخهٔ داده بازخوانی شد: ${loaded.join(', ')}`);
  }

  await autostartAll();

  httpServer.listen(config.port, config.host, () => {
    const ips = readInterfaces().map((i) => i.address);
    const name = getSetting('server_name', null);
    console.log('');
    console.log('==============================================================');
    console.log('  ✅ پنل مدیریت سرور خانگی بالا آمد' + (name ? ` — ${name}` : ''));
    console.log('==============================================================');
    console.log(`  پنل روی این کامپیوتر:   http://localhost:${config.port}`);
    for (const ip of ips) console.log(`  از شبکهٔ خانگی:          http://${ip}:${config.port}`);
    console.log('');
    if (siteSync) {
      console.log('  🔗 سرورِ سایت (همین پورت) — این‌ها را در خودِ سایت وارد کنید:');
      console.log(`     آدرس سرور:  ws://${ips[0] || 'localhost'}:${config.port}`);
      console.log(`     رمز سرور:   ${siteSync.getToken()}`);
      console.log('');
    }
    console.log(`  پوشهٔ داده:  ${config.dataDir}`);
    console.log(`  ریشهٔ سایت‌ها: ${config.sitesRoot}`);
    console.log('==============================================================');
    console.log('');
    logEvent('info', 'panel', `پنل روی پورت ${config.port} اجرا شد`);
  });

  httpServer.on('error', (e) => {
    console.error(`❌ اجرای سرور ناموفق بود: ${e.message}`);
    if (e.code === 'EADDRINUSE') {
      console.error(`   پورت ${config.port} در حال استفاده است. HLP_PORT را عوض کنید.`);
    }
    process.exit(1);
  });
}

let shuttingDown = false;
async function shutdown(signal) {
  if (shuttingDown) return;
  shuttingDown = true;
  console.log(`\n[خاموش‌سازی] ${signal} — ذخیرهٔ نهایی...`);
  clearInterval(housekeeping);
  stopCollector();
  try {
    await stopAll();
  } catch { /* بی‌خیال */ }
  try {
    if (siteSync) await siteSync.flush();
  } catch { /* بی‌خیال */ }
  try {
    db.close();
  } catch { /* بی‌خیال */ }
  httpServer.close(() => process.exit(0));
  setTimeout(() => process.exit(0), 3000).unref();
}

process.on('SIGINT', () => shutdown('SIGINT'));
process.on('SIGTERM', () => shutdown('SIGTERM'));
process.on('uncaughtException', (e) => {
  console.error('[خطای پیش‌بینی‌نشده]', e);
  logEvent('error', 'panel', `خطای پیش‌بینی‌نشده: ${e.message}`);
});
process.on('unhandledRejection', (e) => {
  logEvent('error', 'panel', `Promise رد شد: ${e?.message || e}`);
});

main().catch((err) => {
  console.error('❌ راه‌اندازی ناموفق بود:', err);
  process.exit(1);
});
