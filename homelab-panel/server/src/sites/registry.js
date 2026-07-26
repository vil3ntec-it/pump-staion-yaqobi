// ---------------------------------------------------------------------------
// دفترچهٔ سایت‌ها: افزودن با «مسیر پروژه»، کشف خودکار، وضعیت واقعی، حجم و تاریخ
// ---------------------------------------------------------------------------
import fs from 'node:fs';
import fsp from 'node:fs/promises';
import path from 'node:path';
import { db, logEvent, getSetting } from '../db.js';
import { config } from '../config.js';
import { detectProject, defaultScanRoots } from './detect.js';
import { ensureWorkspace, removeWorkspace, slugify, workspacePaths } from './workspace.js';
import { isRunning, processInfo, probePort, stopSite } from './process.js';
import { dirSize } from '../metrics/system.js';

const PORT_RANGE_START = 8100;
const PORT_RANGE_END = 8999;

export function listSitesRaw() {
  return db.prepare('SELECT * FROM sites ORDER BY name COLLATE NOCASE').all();
}

export function getSite(id) {
  return db.prepare('SELECT * FROM sites WHERE id = ?').get(Number(id));
}

export function getSiteBySlug(slug) {
  return db.prepare('SELECT * FROM sites WHERE slug = ?').get(String(slug));
}

function uniqueSlug(base) {
  let slug = slugify(base);
  let n = 2;
  while (getSiteBySlug(slug)) {
    slug = `${slugify(base)}-${n++}`;
  }
  return slug;
}

// پورت آزاد: هم با سایت‌های ثبت‌شده تداخل نداشته باشد، هم واقعاً روی سیستم آزاد باشد
export async function allocatePort(preferred) {
  const used = new Set(
    db
      .prepare('SELECT port FROM sites WHERE port IS NOT NULL')
      .all()
      .map((r) => r.port)
  );
  used.add(config.port);
  if (preferred && !used.has(Number(preferred))) return Number(preferred);
  for (let p = PORT_RANGE_START; p <= PORT_RANGE_END; p++) {
    if (used.has(p)) continue;
    if (await probePort(p)) continue; // چیز دیگری روی این پورت گوش می‌دهد
    return p;
  }
  return null;
}

// -------------------------------- افزودن ----------------------------------
export async function addSite({ rootPath, name, domain, port, kind, startCommand, autostart = false }) {
  const resolved = path.resolve(String(rootPath || '').trim());
  if (!resolved || !fs.existsSync(resolved)) return { ok: false, error: 'path_not_found' };
  if (!fs.statSync(resolved).isDirectory()) return { ok: false, error: 'not_a_directory' };

  const existing = db.prepare('SELECT * FROM sites WHERE root_path = ?').get(resolved);
  if (existing) return { ok: false, error: 'already_exists', site: existing };

  const detected = detectProject(resolved);
  const finalName = (name || detected.name || path.basename(resolved)).trim();
  const slug = uniqueSlug(finalName);
  const now = Date.now();

  const row = {
    slug,
    name: finalName,
    root_path: resolved,
    domain: domain?.trim() || null,
    port: await allocatePort(port || detected.port),
    kind: kind || detected.kind || 'static',
    start_command: startCommand ?? detected.startCommand ?? null,
    autostart: autostart ? 1 : 0,
  };

  db.prepare(
    `INSERT INTO sites(slug, name, root_path, domain, port, kind, start_command, autostart, enabled, created_at, updated_at)
     VALUES(?, ?, ?, ?, ?, ?, ?, ?, 1, ?, ?)`
  ).run(
    row.slug,
    row.name,
    row.root_path,
    row.domain,
    row.port,
    row.kind,
    row.start_command,
    row.autostart,
    now,
    now
  );

  ensureWorkspace(slug);
  const site = getSiteBySlug(slug);

  if (site.domain) {
    const exists = db.prepare('SELECT id FROM domains WHERE name = ?').get(site.domain);
    if (!exists) {
      db.prepare('INSERT INTO domains(name, site_id, created_at) VALUES(?, ?, ?)').run(
        site.domain,
        site.id,
        now
      );
    }
  }

  logEvent('info', 'panel', `سایت «${site.name}» از مسیر ${resolved} اضافه شد`, site.id);
  return { ok: true, site };
}

export function updateSite(id, patch) {
  const site = getSite(id);
  if (!site) return { ok: false, error: 'not_found' };

  const fields = [];
  const values = [];
  const allowed = {
    name: 'name',
    domain: 'domain',
    port: 'port',
    kind: 'kind',
    startCommand: 'start_command',
    autostart: 'autostart',
    enabled: 'enabled',
  };
  for (const [key, column] of Object.entries(allowed)) {
    if (!(key in patch)) continue;
    let v = patch[key];
    if (column === 'autostart' || column === 'enabled') v = v ? 1 : 0;
    if (column === 'port') v = v ? Number(v) : null;
    if (typeof v === 'string') v = v.trim() || null;
    fields.push(`${column} = ?`);
    values.push(v);
  }
  if (!fields.length) return { ok: true, site };

  fields.push('updated_at = ?');
  values.push(Date.now(), site.id);
  db.prepare(`UPDATE sites SET ${fields.join(', ')} WHERE id = ?`).run(...values);
  logEvent('info', 'panel', `تنظیمات سایت «${site.name}» تغییر کرد`, site.id);
  return { ok: true, site: getSite(site.id) };
}

export async function removeSite(id, { deleteWorkspace = true } = {}) {
  const site = getSite(id);
  if (!site) return { ok: false, error: 'not_found' };
  await stopSite(site);
  db.prepare('DELETE FROM sites WHERE id = ?').run(site.id);
  if (deleteWorkspace) await removeWorkspace(site.slug);
  logEvent('warn', 'panel', `سایت «${site.name}» از پنل حذف شد (فایل‌های خود سایت دست‌نخورده است)`);
  return { ok: true };
}

// ------------------------- ساخت سایت جدید با پوشهٔ مستقل -------------------
export async function createSiteFolder({ name, kind = 'static', domain, port }) {
  const clean = slugify(name);
  if (!clean) return { ok: false, error: 'bad_name' };
  const dir = path.join(config.sitesRoot, clean);
  if (fs.existsSync(dir)) return { ok: false, error: 'folder_exists' };

  fs.mkdirSync(dir, { recursive: true });
  if (kind === 'static') {
    fs.writeFileSync(
      path.join(dir, 'index.html'),
      `<!doctype html>\n<html lang="fa" dir="rtl">\n<head>\n  <meta charset="utf-8">\n  <meta name="viewport" content="width=device-width, initial-scale=1">\n  <title>${name}</title>\n</head>\n<body>\n  <h1>${name}</h1>\n</body>\n</html>\n`,
      'utf8'
    );
  }
  return addSite({ rootPath: dir, name, kind, domain, port });
}

// ------------------------------ کشف خودکار --------------------------------
export async function discoverSites({ roots } = {}) {
  const scanRoots = (roots?.length ? roots : getSetting('scan_roots', null) || defaultScanRoots(config.sitesRoot))
    .map((r) => path.resolve(r))
    .filter((r, i, arr) => arr.indexOf(r) === i);

  const known = new Set(listSitesRaw().map((s) => s.root_path));
  const found = [];

  for (const root of scanRoots) {
    let entries;
    try {
      entries = await fsp.readdir(root, { withFileTypes: true });
    } catch {
      continue; // این ریشه وجود ندارد — رد شو
    }
    for (const e of entries) {
      if (!e.isDirectory()) continue;
      if (e.name.startsWith('.')) continue;
      const dir = path.join(root, e.name);
      const detected = detectProject(dir);
      if (!detected.kind) continue; // پوشهٔ پروژهٔ وب نیست
      found.push({
        path: dir,
        name: detected.name,
        kind: detected.kind,
        port: detected.port,
        startCommand: detected.startCommand,
        alreadyAdded: known.has(dir),
      });
    }
    // خودِ ریشه هم ممکن است یک سایت باشد
    const rootDetected = detectProject(root);
    if (rootDetected.kind && !found.some((f) => f.path === root)) {
      const isEmptyRoot = !entries.length;
      if (!isEmptyRoot && (rootDetected.kind !== 'static' || fs.existsSync(path.join(root, 'index.html')))) {
        found.push({
          path: root,
          name: rootDetected.name,
          kind: rootDetected.kind,
          port: rootDetected.port,
          startCommand: rootDetected.startCommand,
          alreadyAdded: known.has(root),
        });
      }
    }
  }

  return { roots: scanRoots, found };
}

export async function importDiscovered(items) {
  const added = [];
  const skipped = [];
  for (const item of items || []) {
    const res = await addSite({
      rootPath: item.path,
      name: item.name,
      kind: item.kind,
      port: item.port,
      startCommand: item.startCommand,
    });
    if (res.ok) added.push(res.site);
    else skipped.push({ path: item.path, error: res.error });
  }
  return { added, skipped };
}

// --------------------------- وضعیت کامل هر سایت ----------------------------
const sizeCache = new Map(); // slug -> { at, value }

export async function siteSize(site, { force = false } = {}) {
  const cached = sizeCache.get(site.slug);
  if (!force && cached && Date.now() - cached.at < 5 * 60 * 1000) return cached.value;
  const value = fs.existsSync(site.root_path)
    ? await dirSize(site.root_path)
    : { bytes: 0, files: 0, newest: 0, truncated: false };
  sizeCache.set(site.slug, { at: Date.now(), value });
  return value;
}

export function invalidateSizeCache(slug) {
  if (slug) sizeCache.delete(slug);
  else sizeCache.clear();
}

export async function describeSite(site, { withSize = true } = {}) {
  const managed = processInfo(site.slug);
  const online = site.port ? await probePort(site.port) : false;
  const size = withSize ? await siteSize(site) : null;
  const ws = workspacePaths(site.slug);
  let lastModified = size?.newest || null;
  if (!lastModified) {
    try {
      lastModified = fs.statSync(site.root_path).mtimeMs;
    } catch {
      lastModified = null;
    }
  }

  const errors = db
    .prepare("SELECT COUNT(*) AS n FROM events WHERE site_id = ? AND level = 'error'")
    .get(site.id).n;

  return {
    id: site.id,
    slug: site.slug,
    name: site.name,
    domain: site.domain,
    path: site.root_path,
    pathExists: fs.existsSync(site.root_path),
    port: site.port,
    kind: site.kind,
    startCommand: site.start_command,
    autostart: Boolean(site.autostart),
    enabled: Boolean(site.enabled),
    online,
    managed: Boolean(managed),
    process: managed,
    sizeBytes: size?.bytes ?? null,
    fileCount: size?.files ?? null,
    lastModified,
    errorCount: errors,
    workspace: ws.base,
    url: site.port ? `http://${'{host}'}:${site.port}` : site.domain ? `http://${site.domain}` : null,
    createdAt: site.created_at,
    updatedAt: site.updated_at,
  };
}

export async function listSites(options = {}) {
  const rows = listSitesRaw();
  return Promise.all(rows.map((s) => describeSite(s, options)));
}

export async function autostartAll() {
  const rows = listSitesRaw().filter((s) => s.autostart && s.enabled);
  const { startSite } = await import('./process.js');
  for (const site of rows) {
    if (isRunning(site.slug)) continue;
    try {
      await startSite(site);
    } catch (e) {
      logEvent('error', 'process', `اجرای خودکار سایت ${site.name} ناموفق بود: ${e.message}`, site.id);
    }
  }
}
