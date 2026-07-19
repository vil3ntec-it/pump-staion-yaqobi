// ============================================================================
//  مدیریت سایت‌ها (اطلاعات عمومی + فنی)
// ============================================================================
import { db, insertRow, updateRow } from '../db.js';
import { HttpError } from '../http.js';
import { log } from '../activity.js';
import { tagsFor, setTags } from './tags.js';

const COLS = [
  'name', 'url', 'status', 'build_date', 'sale_date', 'sale_price', 'client_id',
  'description', 'cms', 'language', 'hosting', 'server_id', 'server_ip', 'ports',
  'runtime_version', 'database_name',
];

const withMeta = (r) => ({
  ...r,
  tags: tagsFor('website', r.id),
  client_name: r.client_id
    ? db.prepare('SELECT name FROM clients WHERE id = ?').get(r.client_id)?.name || null
    : null,
});

export function register(router) {
  router.get('/api/websites', ({ query }) => {
    let sql = 'SELECT * FROM websites';
    const args = [];
    if (query.status) {
      sql += ' WHERE status = ?';
      args.push(query.status);
    }
    sql += ' ORDER BY updated_at DESC';
    return db.prepare(sql).all(...args).map(withMeta);
  });

  router.get('/api/websites/:id', ({ params }) => {
    const row = db.prepare('SELECT * FROM websites WHERE id = ?').get(params.id);
    if (!row) throw new HttpError(404, 'سایت یافت نشد');
    return {
      ...withMeta(row),
      domains: db.prepare('SELECT * FROM domains WHERE website_id = ?').all(row.id),
      credentials: db.prepare('SELECT id, title, type, username FROM credentials WHERE website_id = ?').all(row.id),
      backups: db.prepare('SELECT * FROM backups WHERE website_id = ? ORDER BY backup_date DESC').all(row.id),
      server: row.server_id ? db.prepare('SELECT id, name, ip FROM servers WHERE id = ?').get(row.server_id) : null,
    };
  });

  router.post('/api/websites', ({ body }) => {
    if (!body.name) throw new HttpError(400, 'نام سایت الزامی است');
    const row = insertRow('websites', COLS, body);
    if (body.tags) setTags('website', row.id, body.tags);
    log('create', 'website', row.id, body.name);
    return withMeta(row);
  });

  router.put('/api/websites/:id', ({ params, body }) => {
    const row = updateRow('websites', COLS, params.id, body);
    if (!row) throw new HttpError(404, 'سایت یافت نشد');
    if (body.tags) setTags('website', params.id, body.tags);
    log('update', 'website', params.id, body.name || '');
    return withMeta(row);
  });

  router.del('/api/websites/:id', ({ params }) => {
    db.prepare('DELETE FROM websites WHERE id = ?').run(params.id);
    log('delete', 'website', params.id);
    return { ok: true };
  });
}
