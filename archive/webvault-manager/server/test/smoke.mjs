// آزمون دود (Smoke Test) — گردش کامل API را بررسی می‌کند.
// اجرا:  WV_DATA_DIR=/tmp/wv-smoke WV_PORT=4699 node test/smoke.mjs
import { spawn } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PORT = process.env.WV_PORT || 4699;
const BASE = `http://127.0.0.1:${PORT}`;
let token = '';
let pass = 0, fail = 0;

const H = () => ({ 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) });
const api = async (method, p, body) => {
  const res = await fetch(BASE + p, { method, headers: H(), body: body ? JSON.stringify(body) : undefined });
  const text = await res.text();
  let data; try { data = JSON.parse(text); } catch { data = text; }
  return { status: res.status, data };
};
function check(name, cond) {
  if (cond) { pass++; console.log('  ✓', name); }
  else { fail++; console.log('  ✗', name); }
}

const srv = spawn(process.execPath, [path.join(__dirname, '..', 'src', 'server.js')], {
  env: { ...process.env, WV_PORT: String(PORT), WV_DATA_DIR: process.env.WV_DATA_DIR || '/tmp/wv-smoke' },
  stdio: 'ignore',
});

async function waitUp() {
  for (let i = 0; i < 50; i++) {
    try { const r = await fetch(BASE + '/api/status'); if (r.ok) return; } catch {}
    await new Promise((r) => setTimeout(r, 100));
  }
  throw new Error('سرور بالا نیامد');
}

try {
  await waitUp();

  let r = await api('GET', '/api/status');
  check('status: راه‌اندازی‌نشده', r.data.initialized === false);

  r = await api('POST', '/api/setup', { password: 'MasterPass123' });
  token = r.data.token;
  check('setup: توکن گرفت', !!token);

  r = await api('GET', '/api/status');
  check('status: اکنون راه‌اندازی‌شده', r.data.initialized === true);

  // بدون توکن باید 401 بدهد
  const saved = token; token = '';
  r = await api('GET', '/api/websites');
  check('محافظت: بدون توکن 401', r.status === 401);
  token = saved;

  r = await api('POST', '/api/clients', { name: 'شرکت آلفا', email: 'a@x.com', payment_status: 'partial', amount: 1000 });
  const clientId = r.data.id;
  check('client: ساخته شد', clientId > 0);

  r = await api('POST', '/api/servers', { name: 'سرور خانگی', ip: '192.168.1.10', ssh_user: 'root', ssh_port: 22, ssh_key: 'PRIVATE-KEY-DATA', tags: ['home', 'prod'] });
  const serverId = r.data.id;
  check('server: ساخته شد و کلید مخفی است', serverId > 0 && r.data.has_ssh_key === true && r.data.ssh_key === undefined);

  r = await api('POST', '/api/websites', { name: 'فروشگاه من', url: 'https://shop.example', status: 'sold', sale_price: 5000, client_id: clientId, server_id: serverId, cms: 'WordPress', tags: ['wordpress', 'sold'] });
  const siteId = r.data.id;
  check('website: ساخته شد', siteId > 0 && r.data.client_name === 'شرکت آلفا');

  r = await api('POST', '/api/domains', { website_id: siteId, domain_name: 'example.com', registrar: 'Namecheap', expiry_date: new Date(Date.now() + 10 * 86400000).toISOString().slice(0, 10) });
  check('domain: ساخته شد با شمارش روز انقضا', r.data.days_to_expiry >= 8 && r.data.days_to_expiry <= 11);

  r = await api('POST', '/api/credentials', { website_id: siteId, title: 'ورود ادمین', type: 'admin', username: 'admin', secret: 'SuperSecret!42' });
  const credId = r.data.id;
  check('credential: ساخته شد و مقدار مخفی است', credId > 0 && r.data.has_secret === true && r.data.secret === undefined);

  r = await api('GET', `/api/credentials/${credId}/reveal`);
  check('credential: رمزگشایی صحیح', r.data.secret === 'SuperSecret!42');

  r = await api('POST', '/api/backups', { website_id: siteId, type: 'full', location: '/nas/backups', status: 'ok' });
  check('backup: ثبت شد', r.data.id > 0);

  r = await api('GET', '/api/dashboard');
  check('dashboard: آمار سایت‌ها', r.data.websites.total >= 1 && r.data.counts.domains >= 1);
  check('dashboard: هشدار انقضای دامنه', r.data.alerts.some((a) => a.text.includes('example.com')));
  check('dashboard: درآمد فروش', r.data.revenue === 5000);

  r = await api('GET', '/api/search?q=example');
  check('search: دامنه پیدا شد', r.data.domains.length >= 1);
  r = await api('GET', '/api/search?q=فروشگاه');
  check('search: سایت پیدا شد', r.data.websites.length >= 1);

  r = await api('POST', '/api/generate-password', { length: 24 });
  check('generator: رمز ۲۴ نویسه‌ای', typeof r.data.password === 'string' && r.data.password.length === 24);

  r = await api('GET', '/api/tags');
  check('tags: تگ‌ها ثبت شدند', r.data.some((t) => t.name === 'wordpress'));

  // تغییر رمز اصلی و باز کردن قفل مجدد
  r = await api('POST', '/api/change-master', { oldPassword: 'MasterPass123', newPassword: 'NewMaster456' });
  check('change-master: موفق', r.data.ok === true);
  token = '';
  r = await api('POST', '/api/unlock', { password: 'NewMaster456' });
  token = r.data.token;
  check('unlock: با رمز جدید', !!token);
  r = await api('GET', `/api/credentials/${credId}/reveal`);
  check('credential: بعد از تغییر رمز هنوز رمزگشایی می‌شود', r.data.secret === 'SuperSecret!42');

  // رمز اشتباه باید رد شود
  r = await api('POST', '/api/unlock', { password: 'wrong' });
  check('unlock: رمز اشتباه رد شد', r.status !== 200 && !!r.data.error);

} catch (e) {
  console.error('خطا در آزمون:', e);
  fail++;
} finally {
  srv.kill();
  console.log(`\n  نتیجه: ${pass} موفق، ${fail} ناموفق`);
  process.exit(fail ? 1 : 0);
}
