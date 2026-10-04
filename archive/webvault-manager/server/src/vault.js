// ============================================================================
//  WebVault Manager — مدیریت رمز اصلی و نشست‌ها (Master Password + Sessions)
//
//  مدل امنیتی:
//   • یک «کلید صندوق» (Vault Key = VK) تصادفی ۲۵۶ بیتی ساخته می‌شود.
//   • همهٔ مقادیر حساس (رمزها، SSH Key، API Key و ...) با VK رمزنگاری می‌شوند.
//   • خودِ VK با کلیدی که از «رمز اصلی» مشتق می‌شود (KEK) رمز شده و ذخیره می‌گردد.
//   • رمز اصلی هرگز ذخیره نمی‌شود؛ فقط با آن می‌توان VK را باز کرد.
//   • VK فقط در حافظهٔ سرور و فقط در طول نشستِ باز نگه داشته می‌شود (قفل خودکار).
// ============================================================================
import { meta } from './db.js';
import {
  deriveKek, newSalt, newVaultKey, encrypt, decrypt, randomToken,
} from './crypto.js';

// نشست‌های باز: token -> { vk, lastActive, expiresAt }
const sessions = new Map();

// مدت قفل خودکار (دقیقه) — قابل تنظیم در تنظیمات
export function autoLockMinutes() {
  return Number(meta.get('auto_lock_minutes') || 15);
}

export function isInitialized() {
  return meta.get('kdf_salt') != null && meta.get('vk_enc') != null;
}

/** بار اول: تعیین رمز اصلی و ساخت کلید صندوق. */
export function setupMaster(password) {
  if (isInitialized()) throw new Error('قبلاً راه‌اندازی شده است');
  if (!password || String(password).length < 6) {
    throw new Error('رمز اصلی باید حداقل ۶ نویسه باشد');
  }
  const salt = newSalt();
  const kek = deriveKek(password, salt);
  const vk = newVaultKey();
  meta.set('kdf_salt', salt);
  meta.set('vk_enc', encrypt(kek, vk.toString('base64')));
  meta.set('auto_lock_minutes', String(autoLockMinutes()));
  return openSession(vk);
}

/** باز کردن قفل با رمز اصلی — توکن نشست برمی‌گرداند. */
export function unlock(password) {
  if (!isInitialized()) throw new Error('هنوز راه‌اندازی نشده');
  const salt = meta.get('kdf_salt');
  const kek = deriveKek(password, salt);
  let vkB64;
  try {
    vkB64 = decrypt(kek, meta.get('vk_enc'));
  } catch {
    throw new Error('رمز اصلی نادرست است');
  }
  return openSession(Buffer.from(vkB64, 'base64'));
}

/** تغییر رمز اصلی — VK همان می‌ماند، فقط با KEK جدید دوباره رمز می‌شود. */
export function changeMaster(oldPassword, newPassword) {
  const salt = meta.get('kdf_salt');
  const oldKek = deriveKek(oldPassword, salt);
  let vkB64;
  try {
    vkB64 = decrypt(oldKek, meta.get('vk_enc'));
  } catch {
    throw new Error('رمز فعلی نادرست است');
  }
  if (!newPassword || String(newPassword).length < 6) {
    throw new Error('رمز جدید باید حداقل ۶ نویسه باشد');
  }
  const newSaltVal = newSalt();
  const newKek = deriveKek(newPassword, newSaltVal);
  meta.set('kdf_salt', newSaltVal);
  meta.set('vk_enc', encrypt(newKek, vkB64));
}

function openSession(vk) {
  const token = randomToken();
  touch(token, vk);
  return token;
}

function touch(token, vk) {
  const ttl = autoLockMinutes() * 60 * 1000;
  const now = Date.now();
  sessions.set(token, { vk, lastActive: now, expiresAt: now + ttl });
}

/** VK نشست را برمی‌گرداند و زمان فعالیت را تازه می‌کند؛ اگر منقضی/نامعتبر باشد null. */
export function getSessionVk(token) {
  const s = sessions.get(token);
  if (!s) return null;
  if (Date.now() > s.expiresAt) {
    sessions.delete(token);
    return null;
  }
  touch(token, s.vk); // تمدید با هر فعالیت
  return s.vk;
}

export function lock(token) {
  sessions.delete(token);
}

// پاک‌سازی دوره‌ای نشست‌های منقضی
setInterval(() => {
  const now = Date.now();
  for (const [t, s] of sessions) if (now > s.expiresAt) sessions.delete(t);
}, 60 * 1000).unref?.();

// --- کمکی رمزنگاری فیلد با VK نشست -----------------------------------------
export function encField(vk, value) {
  return value == null || value === '' ? null : encrypt(vk, value);
}
export function decField(vk, blob) {
  try {
    return decrypt(vk, blob);
  } catch {
    return null;
  }
}
