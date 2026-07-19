// ============================================================================
//  صندوق رمزها (Password Vault) — رمزنگاری AES-256-GCM
//  مقدار محرمانه هرگز به‌صورت پیش‌فرض برنمی‌گردد؛ فقط با درخواست صریح reveal.
// ============================================================================
import { db, insertRow, updateRow } from '../db.js';
import { HttpError } from '../http.js';
import { log } from '../activity.js';
import { encField, decField } from '../vault.js';
import { generatePassword } from '../crypto.js';
import { tagsFor, setTags } from './tags.js';

const COLS = ['website_id', 'server_id', 'title', 'type', 'username', 'secret_enc', 'url', 'notes'];

function present(row, vk, reveal) {
  const out = {
    id: row.id, website_id: row.website_id, server_id: row.server_id,
    title: row.title, type: row.type, username: row.username, url: row.url,
    notes: row.notes, created_at: row.created_at, updated_at: row.updated_at,
    tags: tagsFor('credential', row.id),
    has_secret: !!row.secret_enc,
  };
  if (reveal) out.secret = decField(vk, row.secret_enc);
  return out;
}

export function register(router) {
  router.get('/api/credentials', ({ query, vk }) => {
    let sql = 'SELECT * FROM credentials';
    const args = [];
    const where = [];
    if (query.website_id) { where.push('website_id = ?'); args.push(query.website_id); }
    if (query.type) { where.push('type = ?'); args.push(query.type); }
    if (where.length) sql += ' WHERE ' + where.join(' AND ');
    sql += ' ORDER BY title';
    return db.prepare(sql).all(...args).map((r) => present(r, vk, false));
  });

  // فقط مقدار محرمانهٔ یک رکورد (برای کپی بدون نمایش)
  router.get('/api/credentials/:id/reveal', ({ params, vk }) => {
    const row = db.prepare('SELECT * FROM credentials WHERE id = ?').get(params.id);
    if (!row) throw new HttpError(404, 'رکورد یافت نشد');
    log('reveal', 'credential', row.id, row.title);
    return { secret: decField(vk, row.secret_enc) };
  });

  router.post('/api/credentials', ({ body, vk }) => {
    if (!body.title) throw new HttpError(400, 'عنوان الزامی است');
    const data = { ...body, secret_enc: encField(vk, body.secret) };
    const row = insertRow('credentials', COLS, data);
    if (body.tags) setTags('credential', row.id, body.tags);
    log('create', 'credential', row.id, body.title);
    return present(row, vk, false);
  });

  router.put('/api/credentials/:id', ({ params, body, vk }) => {
    const data = { ...body };
    if (body.secret !== undefined) data.secret_enc = encField(vk, body.secret);
    const row = updateRow('credentials', COLS, params.id, data);
    if (!row) throw new HttpError(404, 'رکورد یافت نشد');
    if (body.tags) setTags('credential', params.id, body.tags);
    log('update', 'credential', params.id, body.title || '');
    return present(row, vk, false);
  });

  router.del('/api/credentials/:id', ({ params }) => {
    db.prepare('DELETE FROM credentials WHERE id = ?').run(params.id);
    log('delete', 'credential', params.id);
    return { ok: true };
  });

  // تولید رمز قوی
  router.post('/api/generate-password', ({ body }) => ({
    password: generatePassword(body || {}),
  }));
}
