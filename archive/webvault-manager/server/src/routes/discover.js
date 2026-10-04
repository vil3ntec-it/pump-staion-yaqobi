// ============================================================================
//  کشف خودکار (Auto-Discovery)
//  سرور را اسکن می‌کند و: (۱) مشخصات همین کامپیوتر را برمی‌گرداند تا خودکار
//  به‌عنوان «سرور» ثبت شود، و (۲) سایت‌های میزبانی‌شده روی این سرور را پیدا و
//  آمادهٔ افزودن خودکار می‌کند (از روی پوشه‌های وب و کانفیگ nginx/apache).
// ============================================================================
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { db, insertRow } from '../db.js';
import { log } from '../activity.js';

const IS_WIN = process.platform === 'win32';

// پوشه‌های رایجِ ریشهٔ وب
const WEB_ROOTS = IS_WIN
  ? ['C:\\xampp\\htdocs', 'C:\\laragon\\www', 'C:\\wamp\\www', 'C:\\wamp64\\www', 'C:\\inetpub\\wwwroot', 'C:\\Apache24\\htdocs']
  : ['/var/www', '/var/www/html', '/srv/www', '/srv/http', '/usr/share/nginx/html', '/home'];

// پوشه‌های کانفیگ وب‌سرور (فقط لینوکس/یونیکس)
const VHOST_DIRS = IS_WIN
  ? []
  : ['/etc/nginx/sites-enabled', '/etc/nginx/conf.d', '/etc/apache2/sites-enabled', '/etc/httpd/conf.d'];

// --- مشخصات همین کامپیوتر ----------------------------------------------------
export function serverInfo() {
  const cpus = os.cpus() || [];
  const ips = [];
  for (const list of Object.values(os.networkInterfaces())) {
    for (const ni of list || []) if (ni.family === 'IPv4' && !ni.internal) ips.push(ni.address);
  }
  return {
    name: os.hostname(),
    ip: ips[0] || '127.0.0.1',
    all_ips: ips,
    provider: 'خانگی',
    server_type: 'Home',
    os: `${os.type()} ${os.release()}`.trim(),
    cpu: cpus.length ? `${cpus[0].model.trim()} (${cpus.length} هسته)` : '',
    ram: `${Math.round(os.totalmem() / 1073741824)} GB`,
    storage: '',
    platform: process.platform,
  };
}

// --- تشخیص نوع CMS از روی محتوای پوشه ---------------------------------------
function detectCms(dir) {
  const has = (f) => { try { return fs.existsSync(path.join(dir, f)); } catch { return false; } };
  if (has('wp-config.php') || has(path.join('wp-includes'))) return { cms: 'WordPress', language: 'PHP' };
  if (has('artisan') && has('composer.json')) return { cms: 'Laravel', language: 'PHP' };
  if (has('composer.json')) return { cms: 'PHP (Composer)', language: 'PHP' };
  if (has('manage.py')) return { cms: 'Django', language: 'Python' };
  if (has('next.config.js') || has('next.config.mjs')) return { cms: 'Next.js', language: 'Node' };
  if (has('package.json')) return { cms: 'Node.js', language: 'Node' };
  if (has('index.php')) return { cms: 'Custom PHP', language: 'PHP' };
  if (has('index.html') || has('index.htm')) return { cms: 'Static', language: 'HTML' };
  return null; // پوشه‌ای که سایت نیست
}

// --- خواندن کانفیگ‌های nginx/apache و استخراج دامنه ↔ مسیر --------------------
function parseVhosts() {
  const map = []; // { domain, root }
  for (const dir of VHOST_DIRS) {
    let files;
    try { files = fs.readdirSync(dir); } catch { continue; }
    for (const f of files) {
      let content;
      try { content = fs.readFileSync(path.join(dir, f), 'utf8'); } catch { continue; }
      // nginx
      const serverNames = [...content.matchAll(/server_name\s+([^;]+);/gi)].map((m) => m[1].trim().split(/\s+/)[0]);
      const roots = [...content.matchAll(/\broot\s+([^;]+);/gi)].map((m) => m[1].trim());
      // apache
      serverNames.push(...[...content.matchAll(/ServerName\s+(\S+)/gi)].map((m) => m[1].trim()));
      roots.push(...[...content.matchAll(/DocumentRoot\s+"?([^"\n]+?)"?\s*$/gim)].map((m) => m[1].trim()));
      const n = Math.max(serverNames.length, roots.length);
      for (let i = 0; i < n; i++) {
        const domain = serverNames[i];
        const root = roots[i];
        if (domain && domain !== '_' && domain !== 'localhost') map.push({ domain, root: root || null });
      }
    }
  }
  return map;
}

// --- اسکن اصلی: بازگرداندن سایت‌های کشف‌شده (بدون ذخیره) ---------------------
export function scanSites(extraPath) {
  const roots = [...WEB_ROOTS];
  if (extraPath) roots.unshift(extraPath);
  const vhosts = parseVhosts();
  const domainForPath = (p) => {
    const hit = vhosts.find((v) => v.root && (v.root === p || v.root.startsWith(p + path.sep) || p.startsWith(v.root)));
    return hit ? hit.domain : null;
  };

  const found = [];
  const seen = new Set();
  const addCandidate = (dir, name) => {
    const real = path.resolve(dir);
    if (seen.has(real)) return;
    const info = detectCms(real);
    if (!info) return;
    seen.add(real);
    const domain = domainForPath(real);
    found.push({
      name: name || path.basename(real),
      path: real,
      cms: info.cms,
      language: info.language,
      domain: domain || null,
      url: domain ? `https://${domain}` : '',
    });
  };

  for (const root of roots) {
    let stat;
    try { stat = fs.statSync(root); } catch { continue; }
    if (!stat.isDirectory()) continue;
    // خودِ ریشه اگر مستقیماً یک سایت باشد
    addCandidate(root, path.basename(root));
    // زیرپوشه‌ها (عمق ۱)
    let entries;
    try { entries = fs.readdirSync(root, { withFileTypes: true }).slice(0, 300); } catch { continue; }
    for (const e of entries) {
      if (!e.isDirectory()) continue;
      if (e.name.startsWith('.')) continue;
      const sub = path.join(root, e.name);
      // برای /home → یک لایه عمیق‌تر به public_html نگاه کن
      if (root === '/home') {
        for (const ph of ['public_html', 'www', 'htdocs']) {
          const p2 = path.join(sub, ph);
          if (fs.existsSync(p2)) addCandidate(p2, e.name);
        }
        continue;
      }
      addCandidate(sub, e.name);
    }
  }

  // دامنه‌هایی که در کانفیگ بودند ولی پوشه‌شان پیدا نشد هم اضافه کن
  for (const v of vhosts) {
    if (!v.domain) continue;
    if (found.some((f) => f.domain === v.domain)) continue;
    found.push({ name: v.domain, path: v.root || '', cms: 'Unknown', language: '', domain: v.domain, url: `https://${v.domain}` });
  }

  return found;
}

export function register(router) {
  // مشخصات همین سرور (برای افزودن خودکار)
  router.get('/api/discover/server-info', () => serverInfo());

  // اسکن سایت‌های میزبانی‌شده روی این سرور
  router.get('/api/discover/sites', ({ query }) => {
    const scanRoots = IS_WIN ? WEB_ROOTS : WEB_ROOTS;
    const sites = scanSites(query.path);
    return { platform: process.platform, scannedRoots: scanRoots, count: sites.length, sites };
  });

  // افزودن خودکار: سرور و/یا سایت‌های انتخاب‌شده
  router.post('/api/discover/import', ({ body }) => {
    let serverId = body.server_id || null;
    let addedServer = null;

    // ۱) در صورت درخواست، همین سرور را ثبت کن (اگر با همان IP قبلاً نبود)
    if (body.addServer && body.server) {
      const s = body.server;
      const exists = s.ip ? db.prepare('SELECT id FROM servers WHERE ip = ?').get(s.ip) : null;
      if (exists) { serverId = exists.id; }
      else {
        addedServer = insertRow('servers',
          ['name', 'provider', 'ip', 'server_type', 'os', 'cpu', 'ram', 'storage', 'ssh_port'],
          { name: s.name, provider: s.provider || 'خانگی', ip: s.ip, server_type: s.server_type || 'Home',
            os: s.os, cpu: s.cpu, ram: s.ram, storage: s.storage, ssh_port: 22 });
        serverId = addedServer.id;
        log('discover', 'server', addedServer.id, `افزودن خودکار سرور ${s.name}`);
      }
    }

    // ۲) سایت‌ها را اضافه کن (تکراری‌ها را رد کن — بر اساس نام یا آدرس)
    const sites = Array.isArray(body.sites) ? body.sites : [];
    let added = 0, skipped = 0;
    const createdDomains = [];
    for (const site of sites) {
      const dup = db.prepare("SELECT id FROM websites WHERE name = ? OR (url != '' AND url = ?)").get(site.name, site.url || ' ');
      if (dup) { skipped++; continue; }
      const w = insertRow('websites',
        ['name', 'url', 'status', 'cms', 'language', 'hosting', 'server_id', 'server_ip', 'description'],
        { name: site.name, url: site.url || '', status: 'active', cms: site.cms || '', language: site.language || '',
          hosting: 'سرور خانگی', server_id: serverId, server_ip: (body.server && body.server.ip) || '',
          description: site.path ? `مسیر روی سرور: ${site.path}` : '' });
      added++;
      log('discover', 'website', w.id, `افزودن خودکار سایت ${site.name}`);
      // اگر دامنه داشت، رکورد دامنه هم بساز
      if (site.domain && !db.prepare('SELECT id FROM domains WHERE domain_name = ?').get(site.domain)) {
        insertRow('domains', ['website_id', 'domain_name', 'notes'],
          { website_id: w.id, domain_name: site.domain, notes: 'کشف خودکار از کانفیگ سرور' });
        createdDomains.push(site.domain);
      }
    }

    return { added, skipped, addedServer: !!addedServer, serverId, domains: createdDomains.length };
  });
}
