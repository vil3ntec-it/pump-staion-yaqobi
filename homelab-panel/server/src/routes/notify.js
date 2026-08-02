// ---------------------------------------------------------------------------
//  API اعلان — عمداً مثل ntfy ساده، تا هر برنامه‌ای بتواند خبر بدهد
//
//      curl -d "تیل مخزن ۲ کم شد"  http://<سرور>/api/notify/pump
//      curl -H "X-Title: هشدار" -H "X-Priority: 5" -d "..." .../api/notify/pump
//      curl -H "Content-Type: application/json" \
//           -d '{"title":"هشدار","message":"...","tags":["fuel"]}' .../api/notify/pump
//
//  فرستادن باز است (مگر موضوع رمزِ نوشتن داشته باشد). خواندنِ فهرستِ موضوع‌ها و
//  عوض کردنِ رمز، فقط از پنل و با حساب مدیر.
// ---------------------------------------------------------------------------
import express, { Router } from 'express';
import { requireAuth } from '../auth.js';
import * as notify from '../notify/index.js';
import { vapidPublicKey } from '../messenger/push.js';

const router = Router();

// متن خام هم قبول می‌شود، نه فقط JSON — تا «curl -d» ساده کار کند
router.use(express.text({ type: ['text/*', 'application/x-www-form-urlencoded'], limit: '1mb' }));

/**
 * هدرهای HTTP فقط حروف انگلیسی می‌پذیرند، پس عنوانِ فارسی را نمی‌شود مستقیم
 * در هدر گذاشت. اگر درصدی‌کدشده باشد بازش می‌کنیم — یعنی این هم کار می‌کند:
 *     curl -H "X-Title: %D9%87%D8%B4%D8%AF%D8%A7%D8%B1" ...
 * (برای متنِ فارسی بدون دردسر، حالت JSON را استفاده کنید.)
 */
function headerText(value) {
  if (!value) return null;
  const raw = String(value);
  if (!/%[0-9a-f]{2}/i.test(raw)) return raw;
  try {
    return decodeURIComponent(raw);
  } catch {
    return raw;
  }
}

const bearer = (req) =>
  String(req.headers.authorization || '').replace(/^Bearer\s+/i, '') || req.query.token || null;

router.get('/config', (req, res) => {
  res.json({ vapidPublicKey: vapidPublicKey() });
});

// ------------------------------ فرستادن اعلان ------------------------------
async function send(req, res) {
  const topic = req.params.topic;
  const isJson = req.body && typeof req.body === 'object';

  const payload = isJson
    ? {
      title: req.body.title,
      body: req.body.message ?? req.body.body,
      priority: req.body.priority,
      tags: req.body.tags,
      click: req.body.click,
    }
    : {
      // هدرها همان‌هایی است که ntfy هم می‌پذیرد
      title: headerText(req.headers['x-title'] || req.headers.title),
      body: typeof req.body === 'string' ? req.body : '',
      priority: req.headers['x-priority'] || req.headers.priority,
      tags: headerText(req.headers['x-tags'] || req.headers.tags),
      click: req.headers['x-click'] || req.headers.click,
    };

  const result = await notify.publish(topic, { ...payload, token: bearer(req) });
  if (!result.ok) {
    return res.status(result.error === 'forbidden' ? 403 : 400).json(result);
  }
  res.json(result);
}

router.post('/:topic', send);
router.put('/:topic', send);

// ------------------------------ خواندنِ پیام‌ها -----------------------------
router.get('/:topic/json', (req, res) => {
  res.json({
    topic: notify.cleanTopic(req.params.topic),
    messages: notify.history(req.params.topic, { since: req.query.since, limit: req.query.limit }),
  });
});

// ---------------------- ثبت دستگاه برای نوتیفیکیشن -------------------------
router.post('/:topic/devices', (req, res) => {
  const sub = (req.body && req.body.subscription) || req.body || {};
  const result = notify.subscribeDevice(req.params.topic, {
    label: req.body?.label,
    endpoint: sub.endpoint,
    p256dh: sub.keys?.p256dh,
    auth: sub.keys?.auth,
  });
  if (!result.ok) return res.status(400).json(result);
  res.json(result);
});

router.delete('/:topic/devices', (req, res) => {
  res.json(notify.unsubscribeDevice(req.params.topic, req.query.endpoint));
});

export default router;

// ------------------------- مسیرهای مدیریتی (پنل) ---------------------------
export const adminRouter = Router();
adminRouter.use(requireAuth);

adminRouter.get('/', (req, res) => {
  res.json({ topics: notify.listTopics(), stats: notify.snapshot() });
});

adminRouter.post('/', (req, res) => {
  const topic = notify.ensureTopic(req.body?.name, { title: req.body?.title });
  if (!topic) return res.status(400).json({ error: 'invalid_topic' });
  res.json({ ok: true, topic: topic.name });
});

adminRouter.post('/:topic/token', (req, res) => {
  const result = req.body?.clear ? notify.setWriteToken(req.params.topic, null) : notify.newWriteToken(req.params.topic);
  if (!result.ok) return res.status(400).json(result);
  res.json(result);
});

// فرستادنِ دستی از داخل پنل
adminRouter.post('/:topic/send', async (req, res) => {
  const topicRow = notify.getTopic(req.params.topic);
  const result = await notify.publish(req.params.topic, {
    title: req.body?.title,
    body: req.body?.message ?? req.body?.body,
    priority: req.body?.priority,
    tags: req.body?.tags,
    // مدیرِ پنل رمزِ موضوع را لازم ندارد
    token: topicRow?.write_token,
  });
  if (!result.ok) return res.status(400).json(result);
  res.json(result);
});
