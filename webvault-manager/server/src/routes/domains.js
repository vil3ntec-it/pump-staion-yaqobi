// ============================================================================
//  مدیریت دامنه‌ها (+ یادآوری انقضا)
// ============================================================================
import { db, insertRow, updateRow } from '../db.js';
import { HttpError } from '../http.js';
import { log } from '../activity.js';
import { tagsFor, setTags } from './tags.js';

const COLS = [
  'website_id', 'domain_name', 'registrar', 'purchase_date', 'expiry_date',
  'dns_provider', 'nameservers', 'cloudflare', 'auto_renew', 'notes',
];

function withMeta(r) {
  const out = { ...r, tags: tagsFor('domain', r.id) };
  out.website_name = r.website_id
    ? db.prepare('SELECT name FROM websites WHERE id = ?').get(r.website_id)?.name || null
    : null;
  if (r.expiry_date) {
    const days = Math.ceil((new Date(r.expiry_date) - Date.now()) / 86400000);
    out.days_to_expiry = Number.isFinite(days) ? days : null;
  } else {
    out.days_to_expiry = null;
  }
  return out;
}

export function register(router) {
  router.get('/api/domains', () =>
    db.prepare('SELECT * FROM domains ORDER BY expiry_date IS NULL, expiry_date').all().map(withMeta)
  );

  router.get('/api/domains/:id', ({ params }) => {
    const row = db.prepare('SELECT * FROM domains WHERE id = ?').get(params.id);
    if (!row) throw new HttpError(404, 'دامنه یافت نشد');
    return withMeta(row);
  });

  router.post('/api/domains', ({ body }) => {
    if (!body.domain_name) throw new HttpError(400, 'نام دامنه الزامی است');
    const row = insertRow('domains', COLS, body);
    if (body.tags) setTags('domain', row.id, body.tags);
    log('create', 'domain', row.id, body.domain_name);
    return withMeta(row);
  });

  router.put('/api/domains/:id', ({ params, body }) => {
    const row = updateRow('domains', COLS, params.id, body);
    if (!row) throw new HttpError(404, 'دامنه یافت نشد');
    if (body.tags) setTags('domain', params.id, body.tags);
    log('update', 'domain', params.id, body.domain_name || '');
    return withMeta(row);
  });

  router.del('/api/domains/:id', ({ params }) => {
    db.prepare('DELETE FROM domains WHERE id = ?').run(params.id);
    log('delete', 'domain', params.id);
    return { ok: true };
  });
}
