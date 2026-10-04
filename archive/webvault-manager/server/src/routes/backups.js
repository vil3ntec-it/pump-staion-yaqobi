// ============================================================================
//  مدیریت بکاپ‌ها
// ============================================================================
import { db, insertRow } from '../db.js';
import { HttpError } from '../http.js';
import { log } from '../activity.js';

const COLS = ['website_id', 'location', 'backup_date', 'type', 'size', 'status', 'notes'];

function withMeta(r) {
  return {
    ...r,
    website_name: r.website_id
      ? db.prepare('SELECT name FROM websites WHERE id = ?').get(r.website_id)?.name || null
      : null,
  };
}

export function register(router) {
  router.get('/api/backups', ({ query }) => {
    let sql = 'SELECT * FROM backups';
    const args = [];
    if (query.website_id) { sql += ' WHERE website_id = ?'; args.push(query.website_id); }
    sql += ' ORDER BY backup_date DESC, id DESC';
    return db.prepare(sql).all(...args).map(withMeta);
  });

  router.post('/api/backups', ({ body }) => {
    if (!body.website_id) throw new HttpError(400, 'سایت الزامی است');
    if (!body.backup_date) body.backup_date = new Date().toISOString().slice(0, 10);
    const row = insertRow('backups', COLS, body);
    log('create', 'backup', row.id, `بکاپ سایت ${body.website_id}`);
    return withMeta(row);
  });

  router.put('/api/backups/:id', ({ params, body }) => {
    const keys = COLS.filter((c) => body[c] !== undefined);
    if (keys.length) {
      const set = keys.map((k) => `${k} = ?`).join(', ');
      db.prepare(`UPDATE backups SET ${set} WHERE id = ?`).run(...keys.map((k) => body[k]), params.id);
    }
    const row = db.prepare('SELECT * FROM backups WHERE id = ?').get(params.id);
    if (!row) throw new HttpError(404, 'بکاپ یافت نشد');
    log('update', 'backup', params.id);
    return withMeta(row);
  });

  router.del('/api/backups/:id', ({ params }) => {
    db.prepare('DELETE FROM backups WHERE id = ?').run(params.id);
    log('delete', 'backup', params.id);
    return { ok: true };
  });
}
