// دامنه‌ها: وضعیت DNS، گواهی SSL، تاریخ انقضا، سایت متصل و آنلاین/آفلاین
import { Router } from 'express';
import { requireAuth } from '../auth.js';
import { db, logEvent } from '../db.js';
import { checkDomain } from '../lib/domain-check.js';

const router = Router();
router.use(requireAuth);

function rowToApi(row) {
  return {
    id: row.id,
    name: row.name,
    siteId: row.site_id,
    siteName: row.site_name || null,
    sitePort: row.site_port || null,
    note: row.note,
    createdAt: row.created_at,
    checkedAt: row.checked_at,
    dns: row.dns_status ? { status: row.dns_status, records: safeJson(row.dns_records) } : null,
    ssl: row.ssl_status
      ? {
          status: row.ssl_status,
          issuer: row.ssl_issuer,
          expiresAt: row.ssl_expires,
          daysLeft: row.ssl_expires ? Math.floor((row.ssl_expires - Date.now()) / 86400000) : null,
        }
      : null,
    registrationExpiresAt: row.reg_expires,
    registrationDaysLeft: row.reg_expires ? Math.floor((row.reg_expires - Date.now()) / 86400000) : null,
    httpStatus: row.http_status,
    online: row.http_status != null && row.http_status < 500,
  };
}

function safeJson(v) {
  try {
    return JSON.parse(v);
  } catch {
    return null;
  }
}

const SELECT_DOMAINS = `
  SELECT d.*, s.name AS site_name, s.port AS site_port
  FROM domains d LEFT JOIN sites s ON s.id = d.site_id`;
const listQuery = `${SELECT_DOMAINS} ORDER BY d.name COLLATE NOCASE`;
const oneQuery = `${SELECT_DOMAINS} WHERE d.name = ?`;

router.get('/', (req, res) => {
  res.json({ domains: db.prepare(listQuery).all().map(rowToApi) });
});

router.post('/', (req, res) => {
  const name = String(req.body?.name || '').trim().toLowerCase();
  if (!/^[a-z0-9.-]+\.[a-z]{2,}$/i.test(name)) return res.status(400).json({ error: 'invalid_domain' });
  const exists = db.prepare('SELECT id FROM domains WHERE name = ?').get(name);
  if (exists) return res.status(409).json({ error: 'already_exists' });
  db.prepare('INSERT INTO domains(name, site_id, note, created_at) VALUES(?, ?, ?, ?)').run(
    name,
    req.body?.siteId ? Number(req.body.siteId) : null,
    req.body?.note || null,
    Date.now()
  );
  logEvent('info', 'panel', `دامنهٔ ${name} اضافه شد`);
  res.json({ ok: true, domain: rowToApi(db.prepare(oneQuery).get(name)) });
});

router.put('/:id', (req, res) => {
  const row = db.prepare('SELECT * FROM domains WHERE id = ?').get(Number(req.params.id));
  if (!row) return res.status(404).json({ error: 'not_found' });
  db.prepare('UPDATE domains SET site_id = ?, note = ? WHERE id = ?').run(
    req.body?.siteId ? Number(req.body.siteId) : null,
    req.body?.note ?? row.note,
    row.id
  );
  res.json({ ok: true });
});

router.delete('/:id', (req, res) => {
  const row = db.prepare('SELECT * FROM domains WHERE id = ?').get(Number(req.params.id));
  if (!row) return res.status(404).json({ error: 'not_found' });
  db.prepare('DELETE FROM domains WHERE id = ?').run(row.id);
  logEvent('warn', 'panel', `دامنهٔ ${row.name} حذف شد`);
  res.json({ ok: true });
});

// بررسی واقعی و ذخیرهٔ نتیجه
async function runCheck(row) {
  const result = await checkDomain(row.name, { whois: true });
  db.prepare(
    `UPDATE domains SET checked_at = ?, dns_status = ?, dns_records = ?, ssl_status = ?,
       ssl_issuer = ?, ssl_expires = ?, reg_expires = ?, http_status = ? WHERE id = ?`
  ).run(
    result.checkedAt,
    result.dns.status,
    JSON.stringify({ a: result.dns.a, cname: result.dns.cname, ns: result.dns.ns }),
    result.ssl.status,
    result.ssl.issuer,
    result.ssl.expiresAt,
    result.whois.expiresAt,
    result.http?.status ?? null,
    row.id
  );
  return result;
}

router.post('/:id/check', async (req, res) => {
  const row = db.prepare('SELECT * FROM domains WHERE id = ?').get(Number(req.params.id));
  if (!row) return res.status(404).json({ error: 'not_found' });
  try {
    const result = await runCheck(row);
    res.json({ ok: true, result });
  } catch (e) {
    res.status(500).json({ error: e.message });
  }
});

router.post('/check-all', async (req, res) => {
  const rows = db.prepare('SELECT * FROM domains').all();
  const results = [];
  for (const row of rows) {
    try {
      results.push({ domain: row.name, ...(await runCheck(row)) });
    } catch (e) {
      results.push({ domain: row.name, error: e.message });
    }
  }
  res.json({ ok: true, results, domains: db.prepare(listQuery).all().map(rowToApi) });
});

export default router;
