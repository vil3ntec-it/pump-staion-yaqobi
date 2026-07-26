// ---------------------------------------------------------------------------
// «سرور سایت» — همان سرور شخصیِ برنامهٔ پمپ یعقوبی که روی همین پنل سوار است.
// این صفحه دقیقاً همان چیزی را می‌دهد که در سایت لازم است:
//     آدرس سرور (ws://...)  +  رمز سرور
// ---------------------------------------------------------------------------
import { Router } from 'express';
import { requireAuth } from '../auth.js';
import { getSiteSync } from '../state.js';
import { config } from '../config.js';
import { readInterfaces, readPublicIp } from '../metrics/network.js';
import { logEvent } from '../db.js';

const router = Router();
router.use(requireAuth);

function addresses(req) {
  const port = config.port;
  const list = [];
  list.push({ label: 'همین کامپیوتر', host: 'localhost', ws: `ws://localhost:${port}`, http: `http://localhost:${port}` });
  for (const iface of readInterfaces()) {
    list.push({
      label: `شبکهٔ خانگی (${iface.name})`,
      host: iface.address,
      ws: `ws://${iface.address}:${port}`,
      http: `http://${iface.address}:${port}`,
    });
  }
  // آدرسی که خودِ مرورگر با آن به پنل وصل شده — مطمئن‌ترین گزینه
  const hostHeader = String(req.headers.host || '').split(':')[0];
  if (hostHeader && !list.some((a) => a.host === hostHeader)) {
    list.unshift({
      label: 'آدرسی که همین حالا با آن وصل شده‌اید',
      host: hostHeader,
      ws: `ws://${hostHeader}:${port}`,
      http: `http://${hostHeader}:${port}`,
    });
  }
  return list;
}

router.get('/', async (req, res) => {
  const sync = getSiteSync();
  if (!sync) return res.json({ enabled: false });
  const token = sync.getToken();
  res.json({
    enabled: true,
    port: config.port,
    addresses: addresses(req),
    tokenPreview: token ? `${token.slice(0, 4)}${'•'.repeat(Math.max(0, token.length - 8))}${token.slice(-4)}` : null,
    stats: sync.snapshot(),
    branches: sync.branches(),
    dataDir: config.siteSync.dataDir,
    publicIp: await readPublicIp(),
    howTo: {
      fa: 'در سایت: تنظیمات ← هم‌زمان‌سازی ← سرور شخصی. «آدرس سرور» و «رمز سرور» زیر را وارد کنید.',
    },
  });
});

// نمایش رمز کامل — عمداً جدا و ثبت‌شونده در لاگ
router.get('/token', (req, res) => {
  const sync = getSiteSync();
  if (!sync) return res.status(404).json({ error: 'disabled' });
  logEvent('warn', 'panel', `رمز سرور سایت توسط «${req.user.username}» نمایش داده شد`);
  res.json({ token: sync.getToken() });
});

router.post('/rotate-token', async (req, res) => {
  const sync = getSiteSync();
  if (!sync) return res.status(404).json({ error: 'disabled' });
  const token = await sync.rotateToken();
  logEvent('warn', 'panel', 'رمز سرور سایت عوض شد — باید در خودِ سایت هم بروزرسانی شود');
  res.json({ ok: true, token });
});

export default router;
