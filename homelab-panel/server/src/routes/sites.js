// مدیریت سایت‌ها: فهرست، افزودن با مسیر، کشف خودکار، Start/Stop/Restart، لاگ
import { Router } from 'express';
import { requireAuth } from '../auth.js';
import {
  listSites,
  getSite,
  addSite,
  updateSite,
  removeSite,
  discoverSites,
  importDiscovered,
  createSiteFolder,
  describeSite,
  siteSize,
  invalidateSizeCache,
} from '../sites/registry.js';
import { startSite, stopSite, restartSite, tailLog, clearLog } from '../sites/process.js';
import { readSiteConfig, writeSiteConfig, workspacePaths } from '../sites/workspace.js';
import { db, logEvent } from '../db.js';
import { config } from '../config.js';

const router = Router();
router.use(requireAuth);

router.get('/', async (req, res) => {
  res.json({ sites: await listSites(), sitesRoot: config.sitesRoot });
});

router.get('/:id', async (req, res) => {
  const site = getSite(req.params.id);
  if (!site) return res.status(404).json({ error: 'not_found' });
  const info = await describeSite(site);
  res.json({ site: info, config: readSiteConfig(site.slug), workspace: workspacePaths(site.slug) });
});

router.post('/', async (req, res) => {
  const { path: rootPath, name, domain, port, kind, startCommand, autostart } = req.body || {};
  const result = await addSite({ rootPath, name, domain, port, kind, startCommand, autostart });
  if (!result.ok) return res.status(400).json(result);
  res.json(result);
});

// ساخت سایت تازه با پوشهٔ کاملاً مستقل زیر ریشهٔ سایت‌ها
router.post('/create', async (req, res) => {
  const { name, kind, domain, port } = req.body || {};
  const result = await createSiteFolder({ name, kind, domain, port });
  if (!result.ok) return res.status(400).json(result);
  res.json(result);
});

router.put('/:id', (req, res) => {
  const result = updateSite(req.params.id, req.body || {});
  if (!result.ok) return res.status(404).json(result);
  invalidateSizeCache(result.site.slug);
  res.json(result);
});

router.delete('/:id', async (req, res) => {
  const result = await removeSite(req.params.id, { deleteWorkspace: req.query.keepData !== '1' });
  if (!result.ok) return res.status(404).json(result);
  res.json(result);
});

// ------------------------------ کشف خودکار --------------------------------
router.post('/discover', async (req, res) => {
  const roots = Array.isArray(req.body?.roots) ? req.body.roots.filter(Boolean) : null;
  res.json(await discoverSites({ roots }));
});

router.post('/discover/import', async (req, res) => {
  const result = await importDiscovered(req.body?.items || []);
  res.json(result);
});

// -------------------------------- عملیات ----------------------------------
async function action(req, res, fn, label) {
  const site = getSite(req.params.id);
  if (!site) return res.status(404).json({ error: 'not_found' });
  try {
    const result = await fn(site);
    if (!result.ok) return res.status(400).json(result);
    res.json({ ...result, site: await describeSite(site, { withSize: false }) });
  } catch (e) {
    logEvent('error', 'process', `${label} سایت ${site.name} ناموفق بود: ${e.message}`, site.id);
    res.status(500).json({ error: e.message });
  }
}

router.post('/:id/start', (req, res) => action(req, res, startSite, 'اجرای'));
router.post('/:id/stop', (req, res) => action(req, res, stopSite, 'توقف'));
router.post('/:id/restart', (req, res) => action(req, res, restartSite, 'راه‌اندازی مجدد'));

router.get('/:id/logs', (req, res) => {
  const site = getSite(req.params.id);
  if (!site) return res.status(404).json({ error: 'not_found' });
  const limit = Math.min(2000, Number(req.query.limit) || 300);
  const lines = tailLog(site.slug, limit).map((line) => {
    const [at, level, ...rest] = line.split('\t');
    return { at, level: level || 'info', text: rest.join('\t') || line };
  });
  const events = db
    .prepare('SELECT * FROM events WHERE site_id = ? ORDER BY id DESC LIMIT 200')
    .all(site.id);
  res.json({ lines, events });
});

router.delete('/:id/logs', (req, res) => {
  const site = getSite(req.params.id);
  if (!site) return res.status(404).json({ error: 'not_found' });
  clearLog(site.slug);
  db.prepare('DELETE FROM events WHERE site_id = ?').run(site.id);
  res.json({ ok: true });
});

// تنظیمات اختصاصی سایت (متغیرهای محیطی جدا برای هر سایت)
router.put('/:id/config', (req, res) => {
  const site = getSite(req.params.id);
  if (!site) return res.status(404).json({ error: 'not_found' });
  const current = readSiteConfig(site.slug);
  const env = req.body?.env && typeof req.body.env === 'object' ? req.body.env : current.env;
  const next = { ...current, ...req.body, env, updatedAt: Date.now() };
  writeSiteConfig(site.slug, next);
  res.json({ ok: true, config: next });
});

// محاسبهٔ دوبارهٔ حجم فایل‌ها (واقعی، با پیمایش پوشه)
router.post('/:id/recalc-size', async (req, res) => {
  const site = getSite(req.params.id);
  if (!site) return res.status(404).json({ error: 'not_found' });
  const size = await siteSize(site, { force: true });
  res.json({ ok: true, size });
});

export default router;
