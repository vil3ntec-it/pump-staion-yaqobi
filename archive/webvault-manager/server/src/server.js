// ============================================================================
//  WebVault Manager — سرور اصلی
//  فقط Node.js (نسخه ۲۲ به بالا) — بدون npm install و بدون وابستگی خارجی.
//  دیتابیس: SQLite داخلی Node   |   رمزنگاری: AES-256-GCM
//
//  اجرا:  node src/server.js      (یا: npm start)
//  دسترسی از لپ‌تاپ/گوشی در شبکهٔ خانگی:  http://<IP سرور>:4600
// ============================================================================
import http from 'node:http';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { createRouter, json } from './http.js';
import { register as authRoutes } from './routes/auth.js';
import { register as dashboardRoutes } from './routes/dashboard.js';
import { register as websiteRoutes } from './routes/websites.js';
import { register as domainRoutes } from './routes/domains.js';
import { register as serverRoutes } from './routes/servers.js';
import { register as credentialRoutes } from './routes/credentials.js';
import { register as backupRoutes } from './routes/backups.js';
import { register as clientRoutes } from './routes/clients.js';
import { register as fileRoutes } from './routes/contracts.js';
import { register as tagRoutes } from './routes/tags.js';
import { register as searchRoutes } from './routes/search.js';
import { register as exportRoutes } from './routes/export.js';
import { register as discoverRoutes } from './routes/discover.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PUBLIC_DIR = path.join(__dirname, '..', 'public');

// --- بارگذاری .env (اختیاری) ------------------------------------------------
(function loadEnv() {
  try {
    const envPath = path.join(__dirname, '..', '.env');
    if (!fs.existsSync(envPath)) return;
    for (const line of fs.readFileSync(envPath, 'utf8').split(/\r?\n/)) {
      const t = line.trim();
      if (!t || t.startsWith('#')) continue;
      const eq = t.indexOf('=');
      if (eq < 0) continue;
      const k = t.slice(0, eq).trim();
      if (!(k in process.env)) process.env[k] = t.slice(eq + 1).trim();
    }
  } catch { /* ignore */ }
})();

const PORT = Number(process.env.WV_PORT || 4600);
const HOST = process.env.WV_HOST || '0.0.0.0';

// --- ثبت همهٔ مسیرها --------------------------------------------------------
const router = createRouter();
for (const reg of [
  authRoutes, dashboardRoutes, websiteRoutes, domainRoutes, serverRoutes,
  credentialRoutes, backupRoutes, clientRoutes, fileRoutes, tagRoutes,
  searchRoutes, exportRoutes, discoverRoutes,
]) reg(router);

// --- سرو کردن فایل‌های ثابت (رابط کاربری) -----------------------------------
const MIME = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8', '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml', '.png': 'image/png', '.ico': 'image/x-icon',
  '.woff2': 'font/woff2',
};

function serveStatic(req, res, pathname) {
  let rel = pathname === '/' ? '/index.html' : pathname;
  const filePath = path.normalize(path.join(PUBLIC_DIR, rel));
  if (!filePath.startsWith(PUBLIC_DIR)) { res.writeHead(403); res.end('ممنوع'); return; }
  fs.readFile(filePath, (err, data) => {
    if (err) {
      // SPA fallback → index.html
      fs.readFile(path.join(PUBLIC_DIR, 'index.html'), (e2, html) => {
        if (e2) { res.writeHead(404); res.end('یافت نشد'); return; }
        res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
        res.end(html);
      });
      return;
    }
    res.writeHead(200, { 'Content-Type': MIME[path.extname(filePath)] || 'application/octet-stream' });
    res.end(data);
  });
}

// --- سرور ------------------------------------------------------------------
const server = http.createServer((req, res) => {
  // CORS برای دسترسی از اپ دسکتاپ/شبکه
  res.setHeader('Access-Control-Allow-Origin', process.env.WV_CORS || '*');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization, X-Vault-Token');
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, OPTIONS');
  if (req.method === 'OPTIONS') { res.writeHead(204); res.end(); return; }

  const url = new URL(req.url, `http://${req.headers.host || 'localhost'}`);
  const pathname = url.pathname;
  const query = Object.fromEntries(url.searchParams);

  if (pathname.startsWith('/api/')) {
    router.dispatch(req, res, pathname, query).catch((err) => {
      if (!res.writableEnded) json(res, 500, { error: err.message || 'خطای سرور' });
    });
    return;
  }
  serveStatic(req, res, pathname);
});

server.listen(PORT, HOST, () => {
  const nets = os.networkInterfaces();
  const addrs = [];
  for (const list of Object.values(nets)) {
    for (const ni of list || []) {
      if (ni.family === 'IPv4' && !ni.internal) addrs.push(ni.address);
    }
  }
  console.log('\n  WebVault Manager — سرور خانگی مدیریت سایت‌ها');
  console.log('  ─────────────────────────────────────────────');
  console.log(`  روی این کامپیوتر:      http://localhost:${PORT}`);
  for (const a of addrs) console.log(`  از شبکهٔ خانگی (لپ‌تاپ/گوشی): http://${a}:${PORT}`);
  console.log('  ─────────────────────────────────────────────');
  console.log('  برای توقف: Ctrl + C\n');
});
