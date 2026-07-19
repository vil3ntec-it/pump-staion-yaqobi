// ============================================================================
//  مدیریت مشتری‌ها
// ============================================================================
import { db, insertRow, updateRow } from '../db.js';
import { HttpError } from '../http.js';
import { log } from '../activity.js';
import { tagsFor, setTags } from './tags.js';

const COLS = ['name', 'email', 'phone', 'delivery_date', 'amount', 'payment_status', 'notes'];

export function register(router) {
  router.get('/api/clients', () => {
    const rows = db.prepare('SELECT * FROM clients ORDER BY name').all();
    return rows.map((r) => ({ ...r, tags: tagsFor('client', r.id) }));
  });

  router.get('/api/clients/:id', ({ params }) => {
    const row = db.prepare('SELECT * FROM clients WHERE id = ?').get(params.id);
    if (!row) throw new HttpError(404, 'مشتری یافت نشد');
    const websites = db.prepare('SELECT id, name, url, status FROM websites WHERE client_id = ?').all(params.id);
    return { ...row, tags: tagsFor('client', row.id), websites };
  });

  router.post('/api/clients', ({ body }) => {
    if (!body.name) throw new HttpError(400, 'نام مشتری الزامی است');
    const row = insertRow('clients', COLS, body);
    if (body.tags) setTags('client', row.id, body.tags);
    log('create', 'client', row.id, body.name);
    return { ...row, tags: tagsFor('client', row.id) };
  });

  router.put('/api/clients/:id', ({ params, body }) => {
    const row = updateRow('clients', COLS, params.id, body);
    if (!row) throw new HttpError(404, 'مشتری یافت نشد');
    if (body.tags) setTags('client', params.id, body.tags);
    log('update', 'client', params.id, body.name || '');
    return { ...row, tags: tagsFor('client', params.id) };
  });

  router.del('/api/clients/:id', ({ params }) => {
    db.prepare('DELETE FROM clients WHERE id = ?').run(params.id);
    log('delete', 'client', params.id);
    return { ok: true };
  });
}
