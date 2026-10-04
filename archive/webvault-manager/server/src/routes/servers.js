// ============================================================================
//  مدیریت هاست و سرور (+ تست اتصال TCP)
// ============================================================================
import net from 'node:net';
import { db, insertRow, updateRow } from '../db.js';
import { HttpError } from '../http.js';
import { log } from '../activity.js';
import { encField, decField } from '../vault.js';
import { tagsFor, setTags } from './tags.js';

const COLS = [
  'name', 'provider', 'ip', 'ssh_user', 'ssh_port', 'server_type',
  'os', 'cpu', 'ram', 'storage', 'ssh_key_enc', 'notes',
];

function present(row, vk, reveal) {
  const out = { ...row, tags: tagsFor('server', row.id) };
  out.has_ssh_key = !!row.ssh_key_enc;
  out.ssh_key = reveal ? decField(vk, row.ssh_key_enc) : undefined;
  delete out.ssh_key_enc;
  return out;
}

export function register(router) {
  router.get('/api/servers', ({ vk }) =>
    db.prepare('SELECT * FROM servers ORDER BY name').all().map((r) => present(r, vk, false))
  );

  router.get('/api/servers/:id', ({ params, vk, query }) => {
    const row = db.prepare('SELECT * FROM servers WHERE id = ?').get(params.id);
    if (!row) throw new HttpError(404, 'سرور یافت نشد');
    const websites = db.prepare('SELECT id, name, url, status FROM websites WHERE server_id = ?').all(params.id);
    return { ...present(row, vk, query.reveal === '1'), websites };
  });

  router.post('/api/servers', ({ body, vk }) => {
    if (!body.name) throw new HttpError(400, 'نام سرور الزامی است');
    const data = { ...body, ssh_key_enc: encField(vk, body.ssh_key) };
    const row = insertRow('servers', COLS, data);
    if (body.tags) setTags('server', row.id, body.tags);
    log('create', 'server', row.id, body.name);
    return present(row, vk, false);
  });

  router.put('/api/servers/:id', ({ params, body, vk }) => {
    const data = { ...body };
    if (body.ssh_key !== undefined) data.ssh_key_enc = encField(vk, body.ssh_key);
    const row = updateRow('servers', COLS, params.id, data);
    if (!row) throw new HttpError(404, 'سرور یافت نشد');
    if (body.tags) setTags('server', params.id, body.tags);
    log('update', 'server', params.id, body.name || '');
    return present(row, vk, false);
  });

  router.del('/api/servers/:id', ({ params }) => {
    db.prepare('DELETE FROM servers WHERE id = ?').run(params.id);
    log('delete', 'server', params.id);
    return { ok: true };
  });

  // تست اتصال: آیا پورت SSH (یا پورت داده‌شده) روی IP باز است؟
  router.post('/api/servers/:id/test', async ({ params, body }) => {
    const row = db.prepare('SELECT ip, ssh_port FROM servers WHERE id = ?').get(params.id);
    if (!row) throw new HttpError(404, 'سرور یافت نشد');
    const host = row.ip;
    const port = Number(body.port || row.ssh_port || 22);
    if (!host) throw new HttpError(400, 'برای این سرور IP ثبت نشده است');
    const result = await tcpProbe(host, port, 4000);
    log('test', 'server', params.id, `${host}:${port} → ${result.reachable ? 'باز' : 'بسته'}`);
    return result;
  });
}

function tcpProbe(host, port, timeout) {
  return new Promise((resolve) => {
    const start = Date.now();
    const socket = new net.Socket();
    let done = false;
    const finish = (reachable, error) => {
      if (done) return;
      done = true;
      socket.destroy();
      resolve({ host, port, reachable, latencyMs: Date.now() - start, error: error || null });
    };
    socket.setTimeout(timeout);
    socket.once('connect', () => finish(true));
    socket.once('timeout', () => finish(false, 'timeout'));
    socket.once('error', (e) => finish(false, e.code || e.message));
    socket.connect(port, host);
  });
}
