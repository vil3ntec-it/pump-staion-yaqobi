// ============================================================================
//  فایل منیجر / قراردادها — آپلود و دانلود فایل (بدون وابستگی، بدنهٔ خام)
//  ساختار پوشه‌ها:  data/files/<websiteId>/<Category>/<storedName>
// ============================================================================
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { db, insertRow, FILES_DIR } from '../db.js';
import { HttpError, json } from '../http.js';
import { log } from '../activity.js';

const COLS = ['website_id', 'client_id', 'filename', 'stored_name', 'mime', 'size', 'category'];
const CATEGORIES = ['Contracts', 'Documents', 'Images', 'Credentials', 'Backup'];

function withMeta(r) {
  return {
    ...r,
    website_name: r.website_id
      ? db.prepare('SELECT name FROM websites WHERE id = ?').get(r.website_id)?.name || null
      : null,
  };
}

function targetDir(websiteId, category) {
  const cat = CATEGORIES.includes(category) ? category : 'Documents';
  const dir = path.join(FILES_DIR, String(websiteId || 'general'), cat);
  fs.mkdirSync(dir, { recursive: true });
  return { dir, cat };
}

export function register(router) {
  router.get('/api/files', ({ query }) => {
    let sql = 'SELECT * FROM contracts';
    const args = [];
    const where = [];
    if (query.website_id) { where.push('website_id = ?'); args.push(query.website_id); }
    if (query.client_id) { where.push('client_id = ?'); args.push(query.client_id); }
    if (where.length) sql += ' WHERE ' + where.join(' AND ');
    sql += ' ORDER BY uploaded_at DESC';
    return db.prepare(sql).all(...args).map(withMeta);
  });

  // آپلود: بدنهٔ خام فایل + متادیتا در query
  router.post('/api/files', ({ query, raw }) => {
    const filename = (query.filename || 'file').replace(/[/\\]/g, '_');
    if (!raw || raw.length === 0) throw new HttpError(400, 'فایلی دریافت نشد');
    const websiteId = query.website_id ? Number(query.website_id) : null;
    const { dir, cat } = targetDir(websiteId, query.category);
    const storedName = crypto.randomUUID() + path.extname(filename);
    fs.writeFileSync(path.join(dir, storedName), raw);
    const row = insertRow('contracts', COLS, {
      website_id: websiteId,
      client_id: query.client_id ? Number(query.client_id) : null,
      filename,
      stored_name: path.join(String(websiteId || 'general'), cat, storedName),
      mime: query.mime || 'application/octet-stream',
      size: raw.length,
      category: cat,
    });
    log('upload', 'file', row.id, filename);
    return withMeta(row);
  }, { raw: true });

  // دانلود
  router.get('/api/files/:id/download', ({ params, res }) => {
    const row = db.prepare('SELECT * FROM contracts WHERE id = ?').get(params.id);
    if (!row) throw new HttpError(404, 'فایل یافت نشد');
    const full = path.join(FILES_DIR, row.stored_name);
    if (!full.startsWith(FILES_DIR) || !fs.existsSync(full)) throw new HttpError(404, 'فایل روی دیسک نیست');
    res.writeHead(200, {
      'Content-Type': row.mime || 'application/octet-stream',
      'Content-Disposition': `attachment; filename*=UTF-8''${encodeURIComponent(row.filename)}`,
      'Content-Length': fs.statSync(full).size,
    });
    fs.createReadStream(full).pipe(res);
    log('download', 'file', row.id, row.filename);
  });

  router.del('/api/files/:id', ({ params }) => {
    const row = db.prepare('SELECT * FROM contracts WHERE id = ?').get(params.id);
    if (!row) throw new HttpError(404, 'فایل یافت نشد');
    const full = path.join(FILES_DIR, row.stored_name);
    if (full.startsWith(FILES_DIR)) fs.rmSync(full, { force: true });
    db.prepare('DELETE FROM contracts WHERE id = ?').run(params.id);
    log('delete', 'file', params.id, row.filename);
    return { ok: true };
  });
}

export { CATEGORIES };
