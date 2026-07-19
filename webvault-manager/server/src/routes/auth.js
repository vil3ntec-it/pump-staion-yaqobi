// ============================================================================
//  مسیرهای احراز هویت / رمز اصلی
// ============================================================================
import {
  isInitialized, setupMaster, unlock, lock, changeMaster, autoLockMinutes,
} from '../vault.js';
import { getToken } from '../http.js';
import { meta } from '../db.js';
import { log } from '../activity.js';

export function register(router) {
  // وضعیت: آیا راه‌اندازی شده و آیا نشست جاری معتبر است
  router.get('/api/status', () => ({
    initialized: isInitialized(),
    autoLockMinutes: autoLockMinutes(),
    version: '1.0.0',
  }), { auth: false });

  // بار اول: تعیین رمز اصلی
  router.post('/api/setup', ({ body }) => {
    const token = setupMaster(body.password);
    log('setup', 'vault', null, 'راه‌اندازی اولیهٔ صندوق');
    return { token, autoLockMinutes: autoLockMinutes() };
  }, { auth: false });

  // باز کردن قفل
  router.post('/api/unlock', ({ body }) => {
    const token = unlock(body.password);
    log('unlock', 'vault', null, 'باز کردن قفل صندوق');
    return { token, autoLockMinutes: autoLockMinutes() };
  }, { auth: false });

  // قفل کردن (پایان نشست)
  router.post('/api/lock', ({ req }) => {
    lock(getToken(req));
    return { ok: true };
  });

  // تغییر رمز اصلی
  router.post('/api/change-master', ({ body }) => {
    changeMaster(body.oldPassword, body.newPassword);
    log('change-master', 'vault', null, 'تغییر رمز اصلی');
    return { ok: true };
  });

  // تنظیم مدت قفل خودکار
  router.post('/api/settings/auto-lock', ({ body }) => {
    const m = Math.max(1, Math.min(240, Number(body.minutes) || 15));
    meta.set('auto_lock_minutes', String(m));
    return { autoLockMinutes: m };
  });
}
